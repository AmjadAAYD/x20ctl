using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
namespace X20Ctl.Product;
public sealed class NativeEngineClient:IAsyncDisposable
{
    public const int MaxFrameBytes=2*1024*1024;
    private readonly Process process;
    private readonly SemaphoreSlim writer=new(1,1);
    private readonly CancellationTokenSource lifetime=new();
    private readonly ConcurrentDictionary<string,TaskCompletionSource<JsonElement>> pending=new();
    private readonly Task reader;
    public event Action<JsonElement>? EventReceived;
    public event Action<string>? Disconnected;
    public NativeEngineClient()
    {
        string executable=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"x20ctl-engine.exe"));if(!File.Exists(executable))throw new FileNotFoundException("Bundled native engine is unavailable.",executable);
        process=new(){StartInfo=new(executable){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true}};process.Start();_ = process.StandardError.ReadToEndAsync(lifetime.Token);reader=ReadAsync();
    }
    public async Task<JsonElement> Request(string method,object parameters)
    {
        if(pending.Count>=64)throw new IOException("Native request queue is full.");string id=Guid.NewGuid().ToString("N");var completion=new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);if(!pending.TryAdd(id,completion))throw new InvalidOperationException();
        byte[] json=JsonSerializer.SerializeToUtf8Bytes(new{protocolVersion=1,requestId=id,method,@params=parameters});if(json.Length>MaxFrameBytes){pending.TryRemove(id,out _);throw new InvalidDataException("Native request exceeds the frame limit.");}
        try{await writer.WaitAsync(lifetime.Token);try{await process.StandardInput.BaseStream.WriteAsync(BitConverter.GetBytes(json.Length),lifetime.Token);await process.StandardInput.BaseStream.WriteAsync(json,lifetime.Token);await process.StandardInput.BaseStream.FlushAsync(lifetime.Token);}finally{writer.Release();}var response=await completion.Task.WaitAsync(TimeSpan.FromSeconds(3),lifetime.Token);if(!response.GetProperty("ok").GetBoolean())throw new InvalidOperationException(response.GetProperty("error").GetProperty("message").GetString());return response.GetProperty("result").Clone();}finally{pending.TryRemove(id,out _);}
    }
    private async Task ReadAsync()
    {
        try{while(!lifetime.IsCancellationRequested){byte[] prefix=new byte[4];await process.StandardOutput.BaseStream.ReadExactlyAsync(prefix,lifetime.Token);int length=BitConverter.ToInt32(prefix);if(length<1||length>MaxFrameBytes)throw new InvalidDataException("Invalid native frame length.");byte[] json=new byte[length];await process.StandardOutput.BaseStream.ReadExactlyAsync(json,lifetime.Token);using var doc=JsonDocument.Parse(json);var message=doc.RootElement;if(message.GetProperty("protocolVersion").GetInt32()!=1)throw new InvalidDataException("Native protocol version mismatch.");if(message.TryGetProperty("kind",out var kind)&&kind.GetString()=="event")EventReceived?.Invoke(message.Clone());else if(message.TryGetProperty("requestId",out var id)&&pending.TryRemove(id.GetString()??"",out var completion))completion.TrySetResult(message.Clone());}}
        catch(Exception error) when(error is IOException or JsonException or InvalidOperationException or OperationCanceledException){foreach(var completion in pending.Values)completion.TrySetException(error);pending.Clear();if(!lifetime.IsCancellationRequested)Disconnected?.Invoke(error.Message);}
    }
    public async ValueTask DisposeAsync()
    {
        lifetime.Cancel();process.StandardInput.Close();try{await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(1));}catch(TimeoutException){if(!process.HasExited)process.Kill(entireProcessTree:true);}try{await reader;}catch(OperationCanceledException){}process.Dispose();lifetime.Dispose();writer.Dispose();
    }
}
