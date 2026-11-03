using System.Buffers.Binary;
namespace X20Ctl.Zone.Screen;
public interface IScreenByteChannel:IDisposable
{
 ValueTask WriteAsync(ReadOnlyMemory<byte> bytes,CancellationToken token);
 ValueTask<int> ReadAsync(Memory<byte> bytes,CancellationToken token);
 void SetBaudRate(int baud);
}
/// <summary>One exclusive byte stream, fixed captured 63-byte replies, no implicit retries.</summary>
public sealed class ScreenStreamTransport(IScreenByteChannel channel,bool isEmulated=false,TimeSpan? timeout=null,uint? nonce=null,uint magic=ScreenProtocol.ModelMagic,bool alreadyFast=false):IScreenTransport,IDisposable
{
 public bool IsEmulated=>isEmulated;
 uint chip=magic;
 /// <summary>The chip the reply identified once connected; before that, the one asked for.</summary>
 public uint Magic=>chip;
 readonly SemaphoreSlim exchange=new(1,1);readonly Queue<byte> pending=[];bool negotiated,failed;
 public async ValueTask<byte[]> ExchangeAsync(ReadOnlyMemory<byte> request,CancellationToken cancellationToken)
 {
  if(!IsEmulated&&!ScreenHardwareEvidence.VerifiedDevice)throw new InvalidOperationException("Screen writes are disabled.");
  await exchange.WaitAsync(cancellationToken);
  try
  {
   if(failed)throw new IOException("Connection closed. Reconnect and check the transfer.");
   using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
   var limit=timeout??TimeSpan.FromSeconds(request.Span[0]==0x6e?Math.Max(5,25*Math.Ceiling(BinaryPrimitives.ReadUInt32LittleEndian(request.Span[5..])/1000000d)):5);deadline.CancelAfter(limit);
   using var abort=deadline.Token.Register(channel.Dispose);
   try
   {
    if(!negotiated)
    {
     // A chip left at 1382400 by an earlier session (pad still plugged in) only answers the fast handshake.
     if(alreadyFast){channel.SetBaudRate(1382400);var fast=ScreenProtocol.Handshake(0x30,nonce??ScreenProtocol.Nonce(DateTime.Now),magic:magic);chip=ScreenProtocol.IdentifyChip(fast,await Send(fast,deadline.Token))??throw new InvalidDataException("Unknown screen chip.");negotiated=true;return await Send(request,deadline.Token);}
     var first=ScreenProtocol.Handshake(0x30,nonce??ScreenProtocol.Nonce(DateTime.Now),9,magic);chip=ScreenProtocol.IdentifyChip(first,await Send(first,deadline.Token))??throw new InvalidDataException("Unknown screen chip.");channel.SetBaudRate(1382400);
     var second=ScreenProtocol.Handshake(0x30,nonce??ScreenProtocol.Nonce(DateTime.Now),magic:chip);ScreenProtocol.ValidateHandshake(second,await Send(second,deadline.Token),chip);negotiated=true;
    }
    return await Send(request,deadline.Token);
   }
   catch(OperationCanceledException)when(!cancellationToken.IsCancellationRequested){throw new TimeoutException("Reply timed out. Check the transfer before retrying.");}
  }
  catch{failed=true;channel.Dispose();throw;}
  finally{exchange.Release();}
 }
 async Task<byte[]> Send(ReadOnlyMemory<byte> request,CancellationToken token)
 {
  await channel.WriteAsync(request,token);var buffer=new byte[512];int progress=0;
  while(true)
  {
   while(pending.Count<63){int n=await channel.ReadAsync(buffer,token);if(n<=0||n>buffer.Length)throw new EndOfStreamException("Port closed during reply.");for(int i=0;i<n;i++)pending.Enqueue(buffer[i]);}
   var reply=new byte[63];for(int i=0;i<63;i++)reply[i]=pending.Dequeue();
   byte expected=request.Span[0]==0x4b?(byte)0x4b:(byte)(request.Span[0]+0x10);if(reply[0]!=expected)throw new InvalidDataException("Unexpected reply. Reconnect and check the transfer.");
   if(request.Span[0]==0x6e&&reply[1]==0x30){if(++progress>1024)throw new InvalidDataException("Too many progress replies.");continue;}return reply;
  }
 }
 public void Dispose(){failed=true;channel.Dispose();}
}
