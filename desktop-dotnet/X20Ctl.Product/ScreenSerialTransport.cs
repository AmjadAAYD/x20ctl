using System.IO;
using System.IO.Ports;
using System.Text.Json;
using System.Text.RegularExpressions;
using X20Ctl.Zone.Screen;
namespace X20Ctl.Product;
/// <summary>Lazy Windows COM channel; parsing and negotiation use the same tested stream transport.</summary>
internal sealed class ScreenSerialTransport(string portName,uint? nonce=null,string? tracePath=null,uint magic=ScreenProtocol.X20ProMagic,bool dtr=false):IScreenTransport,IDisposable
{
 public bool IsEmulated=>false;public uint Magic=>stream?.Magic??magic;ScreenStreamTransport? stream;
 /// <summary>The COM name Windows gave a screen chip (hold M while plugging in), if one is present now. The X20 Pro
 /// shows as VID_EEEF&amp;PID_268A; any EEEF product counts, since other pads' IDs are unseen.</summary>
 public static string? FindScreenPort()
 {
  var present=SerialPort.GetPortNames();
  using var usb=Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\USB");
  foreach(var device in (usb?.GetSubKeyNames()??[]).Where(n=>n.StartsWith("VID_EEEF&",StringComparison.OrdinalIgnoreCase)).OrderBy(n=>!n.Equals("VID_EEEF&PID_268A",StringComparison.OrdinalIgnoreCase)))
  {
   using var root=usb!.OpenSubKey(device);
   foreach(var instance in root?.GetSubKeyNames()??[]){using var parameters=root!.OpenSubKey(instance+@"\Device Parameters");if(parameters?.GetValue("PortName") is string name&&present.Contains(name))return name;}
  }
  return null;
 }
 public static bool ValidPortName(string name)=>Regex.IsMatch(name,@"^COM[1-9][0-9]{0,3}$",RegexOptions.CultureInvariant);
 bool opened;
 public async ValueTask<byte[]> ExchangeAsync(ReadOnlyMemory<byte> request,CancellationToken token)
 {
  if(!ScreenHardwareEvidence.VerifiedDevice)throw new InvalidOperationException("Screen writes are disabled.");
  if(!ValidPortName(portName))throw new ArgumentException("Choose the controller COM port.");
  if(opened)return await stream!.ExchangeAsync(request,token);
  // The chip often misses the first handshake after plug-in, and stays at 1382400 after a session while plugged in.
  // Openings alternate between 115200 and the fast rate, DTR on from the third; only an opening handshake is retried, never data.
  for(int attempt=1;;attempt++)
  {
   stream?.Dispose();stream=new ScreenStreamTransport(new PortChannel(portName,tracePath,dtr||attempt>2),nonce:nonce,magic:magic,alreadyFast:attempt%2==0);
   try{var reply=await stream.ExchangeAsync(request,token);opened=true;return reply;}
   catch(Exception e)when(e is TimeoutException or InvalidDataException&&attempt<4&&request.Span[0]==0x4b){}
  }
 }
 public void Dispose(){stream?.Dispose();stream=null;opened=false;}
 sealed class PortChannel(string name,string? tracePath,bool dtr):IScreenByteChannel
 {
  readonly SerialPort port=new(name,115200,Parity.None,8,StopBits.One){ReadTimeout=100,WriteTimeout=5000,Handshake=Handshake.None,DtrEnable=dtr,RtsEnable=false};bool disposed;
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
