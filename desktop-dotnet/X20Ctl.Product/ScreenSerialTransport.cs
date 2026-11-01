using System.IO;
using System.IO.Ports;
using System.Text.Json;
using System.Text.RegularExpressions;
using X20Ctl.Zone.Screen;
namespace X20Ctl.Product;
/// <summary>Lazy Windows COM channel; parsing and negotiation use the same tested stream transport.</summary>
internal sealed class ScreenSerialTransport(string portName,uint? nonce=null,string? tracePath=null):IScreenTransport,IDisposable
{
 public bool IsEmulated=>false;ScreenStreamTransport? stream;
 public static bool ValidPortName(string name)=>Regex.IsMatch(name,@"^COM[1-9][0-9]{0,3}$",RegexOptions.CultureInvariant);
 public ValueTask<byte[]> ExchangeAsync(ReadOnlyMemory<byte> request,CancellationToken token)
 {
  if(!ScreenHardwareEvidence.VerifiedDevice)throw new InvalidOperationException("Screen writes are disabled.");
  if(!ValidPortName(portName))throw new ArgumentException("Choose the controller COM port.");
  stream??=new ScreenStreamTransport(new PortChannel(portName,tracePath),nonce:nonce);return stream.ExchangeAsync(request,token);
 }
 public void Dispose(){stream?.Dispose();stream=null;}
 sealed class PortChannel(string name,string? tracePath):IScreenByteChannel
 {
  readonly SerialPort port=new(name,115200,Parity.None,8,StopBits.One){ReadTimeout=100,WriteTimeout=5000,Handshake=Handshake.None,DtrEnable=false,RtsEnable=false};bool disposed;
  void Open(){if(disposed)throw new IOException("Port closed.");if(!port.IsOpen)port.Open();}
  public ValueTask WriteAsync(ReadOnlyMemory<byte> bytes,CancellationToken token)=>new(Task.Run(()=>{token.ThrowIfCancellationRequested();Open();var data=bytes.ToArray();Trace("tx-attempt",data);port.Write(data,0,data.Length);Trace("tx-complete",data);},token));
  public ValueTask<int> ReadAsync(Memory<byte> bytes,CancellationToken token)=>new(Task.Run(()=>
  {
   var buffer=new byte[bytes.Length];while(true){token.ThrowIfCancellationRequested();Open();try{int n=port.Read(buffer,0,buffer.Length);Trace("rx",buffer.AsSpan(0,n).ToArray());buffer.AsSpan(0,n).CopyTo(bytes.Span);return n;}catch(TimeoutException){}}
  },token));
  void Trace(string direction,byte[] data){if(tracePath!=null)File.AppendAllText(tracePath,JsonSerializer.Serialize(new{utc=DateTime.UtcNow,ticks=System.Diagnostics.Stopwatch.GetTimestamp(),direction,hex=Convert.ToHexString(data),port=name})+Environment.NewLine);}
  public void SetBaudRate(int baud){Open();port.BaudRate=baud;}
  public void Dispose(){if(disposed)return;disposed=true;try{port.Dispose();}catch(IOException){}}
 }
}
