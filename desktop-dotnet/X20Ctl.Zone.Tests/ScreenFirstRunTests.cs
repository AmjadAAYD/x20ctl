using System.Buffers.Binary;
using X20Ctl.Zone.Screen;
namespace X20Ctl.Zone.Tests;
public class ScreenFirstRunTests
{
 static byte[] Fixture(string name)=>File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Data","Screen",name));
 [Theory][InlineData(1)][InlineData(7)][InlineData(63)][InlineData(126)]
 public async Task StreamTransportHandlesSplitAndMergedProgressReplies(int fragment)
 {
  var channel=new Channel(fragment);using var transport=new ScreenStreamTransport(channel,true,TimeSpan.FromSeconds(1));
  var asset=Fixture("uniform.bin");await new ScreenSender(transport).UploadAsync(asset,true,100,null,CancellationToken.None);
  Assert.Equal(asset,channel.Emulator.Read(ScreenProtocol.AssetAddress,asset.Length));Assert.Equal(new[]{1382400},channel.Bauds);
 }
 [Theory][InlineData("timeout")][InlineData("short")][InlineData("closed")][InlineData("bad-crc")]
 public async Task StreamFailuresStopWithoutAutomaticRetry(string fault)
 {
  var channel=new Channel(9,fault);using var transport=new ScreenStreamTransport(channel,true,TimeSpan.FromMilliseconds(80));
  await Assert.ThrowsAnyAsync<Exception>(()=>new ScreenSender(transport).UploadAsync(Fixture("gradient.bin"),false,100,null,CancellationToken.None));
  transport.Dispose();Assert.True(channel.Disposed);var writes=channel.Writes.Where(b=>b[0]==0x6c).Select(b=>BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(1))).ToArray();Assert.Equal(writes.Length,writes.Distinct().Count());
 }
 [Fact] public async Task ReadbackCheckDoesNotWriteOrErase()
 {
  var emulator=new ScreenEmulator();var asset=Fixture("uniform.bin");var sender=new ScreenSender(emulator);
  Assert.False(await sender.VerifyAsync(asset,true,100,CancellationToken.None));await sender.UploadAsync(asset,true,100,null,CancellationToken.None);emulator.Requests.Clear();
  Assert.True(await sender.VerifyAsync(asset,true,100,CancellationToken.None));Assert.DoesNotContain(emulator.Requests,b=>b[0]==0x6c);
 }
 [Fact] public async Task DryRunUsesTheSameNonceAndBytesAsTheStreamSender()
 {
  var channel=new Channel(126);using var transport=new ScreenStreamTransport(channel,true,nonce:123);var asset=Fixture("gradient.bin");
  await new ScreenSender(transport).UploadAsync(asset,false,100,null,CancellationToken.None,123);
  var expected=new[]{ScreenProtocol.Handshake(0x30,123,9),ScreenProtocol.Handshake(0x30,123)}.Concat(ScreenTransferPlan.Build(asset,false,100,123)).ToArray();Assert.Equal(expected.Length,channel.Writes.Count);for(int i=0;i<expected.Length;i++)Assert.Equal(expected[i],channel.Writes[i]);
 }
 [Fact] public void DryRunMatchesExactSenderPacketList()
 {
  var plan=ScreenTransferPlan.Build(Fixture("gradient.bin"),false,100,123);
  Assert.Equal(0x4b,plan[0][0]);Assert.Equal(0x6e,plan[^1][0]);Assert.Equal(0x400000u,BinaryPrimitives.ReadUInt32LittleEndian(plan[^1].AsSpan(1)));
 }
 [Theory][InlineData("gradient",false)][InlineData("uniform",true)][InlineData("transparent-disposal",true)][InlineData("compressed-odd-padding",false)][InlineData("compressed-palette-257",false)]
 public void DecoderRestoresQuantizedPixelsInDisplayOrientation(string name,bool animation)
 {
  var decoded=ScreenCodec.DecodeAsset(Fixture(name+".bin"),animation);var raw=Fixture(name+".rgba");Assert.Equal(raw.Length/51200,decoded.Count);
  var actual=decoded.SelectMany(f=>f).ToArray();for(int i=0;i<raw.Length;i+=4){Assert.Equal(raw[i]&248,actual[i]);Assert.Equal(raw[i+1]&252,actual[i+1]);Assert.Equal(raw[i+2]&248,actual[i+2]);Assert.Equal(255,actual[i+3]);}
 }
 [Fact] public void UnmeasuredBeyondRawPaddingUsesRawFallback()
 {
  var words=Enumerable.Range(0,12495).Select(i=>(ushort)i).Concat(Enumerable.Repeat((ushort)60000,305)).ToArray();var rgba=new byte[51200];
  for(int y=0;y<160;y++)for(int x=0;x<80;x++){ushort word=words[y*80+x];int i=(x*160+159-y)*4;rgba[i]=(byte)((word>>8)&248);rgba[i+1]=(byte)((word>>3)&252);rgba[i+2]=(byte)((word<<3)&248);rgba[i+3]=255;}
  var notices=new List<string>();var encoded=ScreenCodec.EncodeAnimation(new[]{rgba},diagnostic:notices.Add);Assert.Single(notices);Assert.Equal(0,encoded[17]);Assert.Equal(rgba,ScreenCodec.DecodeAsset(encoded,true)[0]);
 }
 [Fact] public void TransparentPixelsAreFlattenedBeforeBrightnessAndContrast()
 {
  Assert.Equal(new byte[]{0,0,0,255,128,0,0,255},ScreenArtwork.Adjust(new byte[]{255,200,100,0,255,0,0,128},0,1));
 }
 [Fact] public void BuiltInWallpapersAreDistinctAndOpaque()
 {
  var frames=Enumerable.Range(0,6).Select(ScreenArtwork.Wallpaper).ToArray();Assert.Equal(6,frames.Select(Convert.ToBase64String).Distinct().Count());Assert.All(frames,f=>{Assert.Equal(51200,f.Length);for(int i=3;i<f.Length;i+=4)Assert.Equal(255,f[i]);});
 }
 [Fact] public void DecoderRejectsTruncatedOrInvalidAsset()
 {
  var bytes=Fixture("uniform.bin");Assert.Throws<InvalidDataException>(()=>ScreenCodec.DecodeAsset(bytes[..^1],true));bytes[2]=0xff;Assert.Throws<InvalidDataException>(()=>ScreenCodec.DecodeAsset(bytes,true));
 }
 sealed class Channel(int fragment,string? fault=null):IScreenByteChannel
 {
  public ScreenEmulator Emulator {get;}=new();public List<byte[]> Writes {get;}=[];public List<int> Bauds {get;}=[];public bool Disposed;readonly Queue<byte> pending=[];
  public void SetBaudRate(int baud)=>Bauds.Add(baud);
  public async ValueTask WriteAsync(ReadOnlyMemory<byte> bytes,CancellationToken token)
  {
   var request=bytes.ToArray();Writes.Add(request);var reply=await Emulator.ExchangeAsync(bytes,token);
   if(request[0]==0x6c&&fault!=null){if(fault=="closed")throw new IOException("Port closed");if(fault=="timeout")return;if(fault=="short"){foreach(var b in reply[..9])pending.Enqueue(b);return;}}
   if(request[0]==0x6e){var progress=new byte[63];progress[0]=0x7e;progress[1]=0x30;foreach(byte b in progress)pending.Enqueue(b);if(fault=="bad-crc")reply[2]^=1;}
   foreach(byte b in reply)pending.Enqueue(b);
  }
  public async ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken token)
  {
   while(pending.Count==0)await Task.Delay(2,token);int n=Math.Min(Math.Min(fragment,buffer.Length),pending.Count);for(int i=0;i<n;i++)buffer.Span[i]=pending.Dequeue();return n;
  }
  public void Dispose()=>Disposed=true;
 }
}
