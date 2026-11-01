using System.Buffers.Binary;
namespace X20Ctl.Zone.Screen;
public interface IScreenTransport
{
 bool IsEmulated {get;}
 ValueTask<byte[]> ExchangeAsync(ReadOnlyMemory<byte> request,CancellationToken cancellationToken);
}
public enum ScreenTransferState { Idle,Connecting,Uploading,Checking,Completed,Cancelled,Failed }
public sealed record ScreenProgress(ScreenTransferState State,int Sent,int Total);
public static class ScreenHardwareEvidence
{
 // Enable only after the identity, restore, ACK, readback and physical test plan passes.
 public static bool VerifiedDevice => false;
}
public sealed class ScreenSender(IScreenTransport transport)
{
 public ScreenTransferState State {get;private set;}=ScreenTransferState.Idle;
 private readonly SemaphoreSlim transaction=new(1,1);
 public Task UploadAsync(byte[] asset,bool animation,int delayMs,IProgress<ScreenProgress>? progress,CancellationToken cancellationToken)
 {
  var stable=asset.ToArray();var metadata=ScreenCodec.Metadata(stable,animation,delayMs);
  return TransferAsync(stable,metadata,progress,cancellationToken,false);
 }
 /// <summary>Vendor reset blanks screen content. It does not restore factory artwork.</summary>
 public Task ResetAsync(IProgress<ScreenProgress>? progress,CancellationToken cancellationToken)=>TransferAsync([255,255,255,255],Enumerable.Repeat((byte)255,4096).ToArray(),progress,cancellationToken,true);
 async Task TransferAsync(byte[] asset,byte[] metadata,IProgress<ScreenProgress>? progress,CancellationToken cancellationToken,bool vendorReset)
 {
  if(!transport.IsEmulated&&!ScreenHardwareEvidence.VerifiedDevice)throw new InvalidOperationException("Screen writes await device verification.");
  if(!await transaction.WaitAsync(0,CancellationToken.None))throw new InvalidOperationException("A screen transfer is already running.");
  try
  {
   cancellationToken.ThrowIfCancellationRequested();int sent=0,total=asset.Length+metadata.Length;
   foreach(var (address,data) in new[]{(ScreenProtocol.AssetAddress,asset),(ScreenProtocol.MetadataAddress,metadata)})
   {
    State=ScreenTransferState.Connecting;progress?.Report(new(State,sent,total));
    foreach(byte command in new byte[]{0x30,0x32}){var request=ScreenProtocol.Handshake(command,ScreenProtocol.Nonce(DateTime.Now));ScreenProtocol.ValidateHandshake(request,await Exchange(request,cancellationToken));}
    var identity=await Exchange(ScreenProtocol.FlashQuery(),cancellationToken);
    if(identity.Length<5||identity[0]!=0x7d||BinaryPrimitives.ReadUInt32LittleEndian(identity.AsSpan(1))!=ScreenProtocol.FlashId)throw new InvalidDataException("Flash profile differs from the captured profile.");
    State=ScreenTransferState.Uploading;
    for(int offset=0;offset<data.Length;offset+=2048)
    {
     int n=Math.Min(2048,data.Length-offset);byte mode=(byte)((offset%4096)==0?0:1);
     var reply=await Exchange(ScreenProtocol.Data(address+(uint)offset,data.AsSpan(offset,n),mode),cancellationToken);
     if(reply.Length<2||reply[0]!=0x7c||reply[1]!=0)throw new InvalidDataException("Screen write was rejected.");sent+=n;progress?.Report(new(State,sent,total));
    }
    State=ScreenTransferState.Checking;progress?.Report(new(State,sent,total));
    // Captured vendor reset retains the four-byte content CRC in its metadata query.
    uint? requestedCrc=vendorReset&&address==ScreenProtocol.MetadataAddress?ScreenCodec.Crc(asset):null;
    var crc=await Exchange(ScreenProtocol.CrcQuery(address,data,requestedCrc),cancellationToken);
    if(crc.Length<6||crc[0]!=0x7e||crc[1] is not (0x31 or 0x32)||BinaryPrimitives.ReadUInt32LittleEndian(crc.AsSpan(2))!=ScreenCodec.Crc(data))throw new InvalidDataException("Screen readback CRC mismatch.");
   }
   State=ScreenTransferState.Completed;progress?.Report(new(State,total,total));
  }
  catch(OperationCanceledException){State=ScreenTransferState.Cancelled;throw;}
  catch{State=ScreenTransferState.Failed;throw;}
  finally{transaction.Release();}
 }
 async ValueTask<byte[]> Exchange(byte[] request,CancellationToken cancellationToken)
 {
  cancellationToken.ThrowIfCancellationRequested();using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);double seconds=request[0]==0x6e?25*Math.Ceiling(System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(request.AsSpan(5))/1000000d):5;timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(5,seconds)));
  try{return await transport.ExchangeAsync(request,timeout.Token).AsTask().WaitAsync(timeout.Token);}
  catch(OperationCanceledException) when(!cancellationToken.IsCancellationRequested){throw new TimeoutException("Screen device reply timed out.");}
 }
}
