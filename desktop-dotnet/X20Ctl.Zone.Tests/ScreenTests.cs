using X20Ctl.Zone.Screen;
namespace X20Ctl.Zone.Tests;
public class ScreenTests
{
 static byte[] Data(string name)=>File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Data","Screen",name));
 [Theory]
 [InlineData("gradient")][InlineData("palette-255")][InlineData("palette-256")][InlineData("palette-257")][InlineData("run-255")][InlineData("run-256")][InlineData("run-257")]
 public void StillMatchesOriginalTool(string name)=>Assert.Equal(Data(name+".bin"),ScreenCodec.EncodeStill(Data(name+".rgba"),160,80));
 [Theory][InlineData("uniform",3)][InlineData("single-frame",1)][InlineData("unequal",3)][InlineData("transparent-disposal",3)]
 public void AnimationMatchesOriginalTool(string name,int count)
 {
  var raw=Data(name+".rgba");var frames=Enumerable.Range(0,count).Select(i=>raw.AsMemory(i*51200,51200).ToArray()).ToArray();
  Assert.Equal(Data(name+".bin"),ScreenCodec.EncodeAnimation(frames,160,80));
 }
 [Theory][InlineData("palette-255")][InlineData("palette-256")][InlineData("palette-257")][InlineData("odd-padding")][InlineData("odd-padding-green")]
 public void CompressedPayloadMatchesHiddenOriginalStillPath(string name)
 {
  // Both paths use the same inner LT compressor; compare actual original compressed-still payload.
  Assert.Equal(Data("compressed-"+name+".bin").AsSpan(12).ToArray(),ScreenCodec.EncodeAnimation(new[]{Data("compressed-"+name+".rgba")}).AsSpan(18).ToArray());
 }
 [Fact] public void NonceUsesRecoveredLocalTimeFormula()=>Assert.Equal(11557010u,ScreenProtocol.Nonce(new DateTime(2026,11,1,12,34,56,789)));
 [Fact] public void OpeningBaudNegotiationMatchesCapturedOriginal()
 {
  var original=Data("connect-first.bin");uint nonce=System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(original.AsSpan(4));Assert.Equal(original,ScreenProtocol.Handshake(0x30,nonce,9));
 }
 [Fact] public void HandshakeMatchesCapturedOriginal()
 {
  var original=Data("handshake.bin");uint nonce=System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(original.AsSpan(4));Assert.Equal(original,ScreenProtocol.Handshake(original[1],nonce));
 }
 [Fact] public void DataAndCrcQueryMatchCapturedOriginal()
 {
  var asset=Data("gradient.bin");Assert.Equal(Data("first-data.bin"),ScreenProtocol.Data(0x410000,asset.AsSpan(0,2048),0));Assert.Equal(Data("crc-query.bin"),ScreenProtocol.CrcQuery(0x410000,asset));
 }
 [Theory][InlineData("gradient",false,100)][InlineData("unequal",true,40)][InlineData("transparent-disposal",true,60)]
 public void MetadataMatchesOriginal(string name,bool animation,int delay)=>Assert.Equal(Data(name+".metadata"),ScreenCodec.Metadata(Data(name+".bin"),animation,delay));
 [Fact] public void ComplexPalettePayloadMatchesOriginalFrame()
 {
  var encoded=ScreenCodec.EncodeAnimation(new[]{Data("large-first.rgba")});Assert.Equal(Data("large-first.lt"),encoded.AsSpan(18).ToArray());
 }
 [Fact] public void VendorLimitTruncationIsExplicitAndDefaultDoesNotSilentlyDropFrames()
 {
  var frames=Enumerable.Repeat(Data("large-first.rgba"),236).ToArray();Assert.Throws<ArgumentException>(()=>ScreenCodec.EncodeAnimation(frames));
  var result=ScreenCodec.EncodeAnimation(frames,truncateToLimit:true);Assert.Equal(235,System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(result));Assert.Equal(3135842,result.Length);
 }
 [Fact] public void EncoderHonorsCancelledWork()
 {
  Assert.Throws<OperationCanceledException>(()=>ScreenCodec.EncodeAnimation(new[]{Data("large-first.rgba")},cancellationToken:new CancellationToken(true)));
 }
 [Fact] public void CrcUsesRecoveredVariant()=>Assert.Equal(0x2dfd2d88u,ScreenCodec.Crc("123456789"u8));
 [Fact] public async Task EmulatorRejectsInvalidPrimaryHandshakeCrcEvenWithValidFrameCrc()
 {
  var packet=ScreenProtocol.Handshake(0x30,123);packet[8]^=1;System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(32),ScreenCodec.Crc(packet.AsSpan(0,32)));
  await Assert.ThrowsAsync<InvalidDataException>(async()=>await new ScreenEmulator().ExchangeAsync(packet,CancellationToken.None));
 }
 [Fact] public async Task NonRespondingPeerCanBeCancelled()
 {
  var sender=new ScreenSender(new SilentPeer());using var stop=new CancellationTokenSource();var operation=sender.UploadAsync(Data("uniform.bin"),true,100,null,stop.Token);stop.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>operation.WaitAsync(TimeSpan.FromSeconds(2)));Assert.Equal(ScreenTransferState.Cancelled,sender.State);
 }
 sealed class SilentPeer:IScreenTransport
 {
  public bool IsEmulated=>true;
  public ValueTask<byte[]> ExchangeAsync(ReadOnlyMemory<byte> request,CancellationToken token)=>new(new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously).Task);
 }
 [Fact] public void PacketRejectsCorruption()
 {
  var packet=ScreenProtocol.Data(0x410000,[1,2,3],2);packet[9]^=1;
  Assert.Throws<InvalidDataException>(()=>ScreenProtocol.ParseData(packet));
 }
 [Fact] public async Task SenderUploadsAssetThenMetadataAndChecksStoredCrc()
 {
  var transport=new ScreenEmulator();var sender=new ScreenSender(transport);
  var asset=Data("uniform.bin");await sender.UploadAsync(asset,true,100,null,CancellationToken.None);
  Assert.Equal(asset,transport.Read(0x410000,asset.Length));
  Assert.Equal(ScreenCodec.Metadata(asset,true,100),transport.Read(0x400000,4096));
  Assert.Equal(ScreenTransferState.Completed,sender.State);
 }
 [Fact] public async Task SenderSequenceMatchesOriginalToolApartFromTimeNonce()
 {
  var emulator=new ScreenEmulator();await new ScreenSender(emulator).UploadAsync(Data("gradient.bin"),false,100,null,CancellationToken.None);
  using var stream=new MemoryStream(Data("vendor-upload.frames"));using var reader=new BinaryReader(stream);int i=0;
  while(stream.Position<stream.Length)
  {
   var expected=reader.ReadBytes(reader.ReadInt32());var actual=emulator.Requests[i++];
   if(expected[0]==0x4b){Assert.Equal(expected.AsSpan(0,4).ToArray(),actual.AsSpan(0,4).ToArray());Assert.Equal(expected.AsSpan(12,20).ToArray(),actual.AsSpan(12,20).ToArray());}
   else Assert.Equal(expected,actual);
  }
  Assert.Equal(i,emulator.Requests.Count);
 }
 [Fact] public async Task ResetSequenceMatchesOriginalToolApartFromTimeNonce()
 {
  var emulator=new ScreenEmulator();await new ScreenSender(emulator).ResetAsync(null,CancellationToken.None);
  using var stream=new MemoryStream(Data("vendor-reset.frames"));using var reader=new BinaryReader(stream);int i=0;
  while(stream.Position<stream.Length){var expected=reader.ReadBytes(reader.ReadInt32());var actual=emulator.Requests[i++];if(expected[0]!=0x4b)Assert.Equal(expected,actual);}
  Assert.Equal(i,emulator.Requests.Count);
 }
 [Fact] public async Task CancellationPreventsFurtherWrites()
 {
  var transport=new ScreenEmulator();var sender=new ScreenSender(transport);using var stop=new CancellationTokenSource();stop.Cancel();
  await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>sender.UploadAsync(Data("uniform.bin"),true,100,null,stop.Token));
  Assert.Empty(transport.Requests);Assert.Equal(ScreenTransferState.Cancelled,sender.State);
 }
 [Fact] public async Task PhysicalWritesDefaultToLocked()
 {
  var sender=new ScreenSender(new PhysicalStub());
  await Assert.ThrowsAsync<InvalidOperationException>(()=>sender.UploadAsync(Data("uniform.bin"),true,100,null,CancellationToken.None));
 }
 [Fact] public async Task ResetBlanksContentAndMetadataWithoutPretendingToRestoreFactoryArt()
 {
  var transport=new ScreenEmulator();var sender=new ScreenSender(transport);await sender.UploadAsync(Data("uniform.bin"),true,100,null,CancellationToken.None);await sender.ResetAsync(null,CancellationToken.None);
  Assert.Equal(new byte[]{255,255,255,255},transport.Read(0x410000,4));Assert.All(transport.Read(0x400000,4096),b=>Assert.Equal(255,b));
 }
 [Fact] public async Task CancellationAfterFirstBlockLeavesMetadataUntouched()
 {
  var transport=new ScreenEmulator();var sender=new ScreenSender(transport);using var stop=new CancellationTokenSource();
  await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>sender.UploadAsync(Data("gradient.bin"),false,100,new ProgressNow(p=>{if(p.State==ScreenTransferState.Uploading)stop.Cancel();}),stop.Token));
  Assert.All(transport.Read(0x400000,4096),b=>Assert.Equal(255,b));Assert.Equal(ScreenTransferState.Cancelled,sender.State);
 }
 [Fact] public async Task WrongAckStopsBeforeMetadata()
 {
  var emulator=new ScreenEmulator();var transport=new BrokenReply(emulator,false);var sender=new ScreenSender(transport);
  await Assert.ThrowsAsync<InvalidDataException>(()=>sender.UploadAsync(Data("gradient.bin"),false,100,null,CancellationToken.None));Assert.Equal(ScreenTransferState.Failed,sender.State);Assert.All(emulator.Read(0x400000,4096),b=>Assert.Equal(255,b));
 }
 [Fact] public async Task BadReadbackCrcStopsBeforeMetadata()
 {
  var emulator=new ScreenEmulator();var sender=new ScreenSender(new BrokenReply(emulator,true));
  await Assert.ThrowsAsync<InvalidDataException>(()=>sender.UploadAsync(Data("uniform.bin"),true,100,null,CancellationToken.None));Assert.All(emulator.Read(0x400000,4096),b=>Assert.Equal(255,b));
 }
 sealed class ProgressNow(Action<ScreenProgress> report):IProgress<ScreenProgress>{public void Report(ScreenProgress value)=>report(value);}
 sealed class BrokenReply(ScreenEmulator emulator,bool crc):IScreenTransport
 {
  public bool IsEmulated=>true;
  public async ValueTask<byte[]> ExchangeAsync(ReadOnlyMemory<byte> request,CancellationToken token){var reply=await emulator.ExchangeAsync(request,token);if(crc&&request.Span[0]==0x6e)reply[2]^=1;if(!crc&&request.Span[0]==0x6c)reply[1]=1;return reply;}
 }
 sealed class PhysicalStub:IScreenTransport
 {
  public bool IsEmulated=>false;
  public ValueTask<byte[]> ExchangeAsync(ReadOnlyMemory<byte> request,CancellationToken token)=>throw new Exception("Must not reach physical transport");
 }
}
