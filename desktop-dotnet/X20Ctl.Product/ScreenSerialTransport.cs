using System.IO;
using System.IO.Ports;
using System.Text.RegularExpressions;
using X20Ctl.Zone.Screen;
namespace X20Ctl.Product;
/// <summary>Lazy, exclusive COM transport. Never opens a port while the hardware gate is locked.</summary>
internal sealed class ScreenSerialTransport(string portName):IScreenTransport,IDisposable
{
 public bool IsEmulated=>false;
 SerialPort? port;bool negotiated;readonly SemaphoreSlim exchange=new(1,1);
 public static bool ValidPortName(string name)=>Regex.IsMatch(name,@"^COM[1-9][0-9]{0,3}$",RegexOptions.CultureInvariant);
 public async ValueTask<byte[]> ExchangeAsync(ReadOnlyMemory<byte> request,CancellationToken cancellationToken)
 {
  if(!ScreenHardwareEvidence.VerifiedDevice)throw new InvalidOperationException("Screen writes await device verification.");
  if(!ValidPortName(portName))throw new ArgumentException("Enter the verified controller COM port.");
  await exchange.WaitAsync(cancellationToken);
  try
  {
   var bytes=request.ToArray();
   return await Task.Run(()=>
   {
    cancellationToken.ThrowIfCancellationRequested();
    port??=new SerialPort(portName,115200,Parity.None,8,StopBits.One){ReadTimeout=1000,WriteTimeout=5000,Handshake=Handshake.None,DtrEnable=false,RtsEnable=false};
    if(!port.IsOpen)port.Open();using var cancellation=cancellationToken.Register(()=>{try{port.Close();}catch(IOException){}});
    try
    {
     byte[] WriteRead(byte[] outgoing)
     {
      port.DiscardInBuffer();port.Write(outgoing,0,outgoing.Length);
      // Reply framing is derived from DIYTool and awaits the first physical trace.
      while(true)
      {
       var response=new byte[63];int n=0;
       while(n<response.Length){cancellationToken.ThrowIfCancellationRequested();int read;try{read=port.Read(response,n,response.Length-n);}catch(TimeoutException){continue;}if(read<=0)throw new EndOfStreamException("Screen connection closed.");n+=read;}
       if(outgoing[0]==0x6e&&response[0]==0x7e&&response[1]==0x30)continue;return response;
      }
     }
     if(!negotiated)
     {
      // Captured Open Port: 115200 -> baud index 9 (1382400), then repeat 4B30 at the new rate.
      var first=ScreenProtocol.Handshake(0x30,ScreenProtocol.Nonce(DateTime.Now),9);ScreenProtocol.ValidateHandshake(first,WriteRead(first));port.BaudRate=1382400;
      var second=ScreenProtocol.Handshake(0x30,ScreenProtocol.Nonce(DateTime.Now));ScreenProtocol.ValidateHandshake(second,WriteRead(second));negotiated=true;
     }
     return WriteRead(bytes);
    }
    catch(Exception)when(cancellationToken.IsCancellationRequested){throw new OperationCanceledException(cancellationToken);}
   },cancellationToken);
  }
  finally{exchange.Release();}
 }
 public void Dispose(){port?.Dispose();port=null;negotiated=false;}
}
