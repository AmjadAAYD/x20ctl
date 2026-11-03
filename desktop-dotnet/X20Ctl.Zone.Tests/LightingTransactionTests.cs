using X20Ctl.Zone.Lighting;
namespace X20Ctl.Zone.Tests;
/// <summary>A virtual X20 Pro lighting store behind the real two-chunk read and write framing, seeded from the owner's pad (2 Nov and 3 Nov 2026).</summary>
public class LightingTransactionTests
{
 const string Stored="ff00000000500401001080400000ff408080808080a080ff";
 sealed class VirtualPad(string hex,bool dropWrites=false,bool wrongAck=false)
 {
  public byte[] Record=Convert.FromHexString(hex);public List<(byte op,byte serial,byte length,int size)> Sent=[];
  public Task<byte[]> Exchange(byte op,byte[] payload,byte serial,byte length)
  {
   Sent.Add((op,serial,length,payload.Length));
   if(op==0xb7)return Task.FromResult(payload[0]==0?[24,..Record[..14]]:Record[14..]);
   if(op==0x37)
   {
    int chunk=((length>>5)&7)-1;var framed=new List<byte>{24};framed.AddRange(Record);
    if(!dropWrites){var all=framed.ToArray();payload.CopyTo(all,chunk*15);Record=all[1..];}
    return Task.FromResult(wrongAck?new byte[]{2,0xdf,0xab}:new byte[]{2,0xdf,0xaa});
   }
   throw new InvalidOperationException("Outside the lighting allowlist.");
  }
 }
 static string Folder()=>Path.Combine(Path.GetTempPath(),"x20ctl-lighting-test-"+Guid.NewGuid().ToString("N"));
 static Func<byte> Serials(){byte n=160;return ()=>++n;}
 [Fact]
 public async Task BrightnessChangesOnlyOffsetFiveAndReadsBack()
 {
  var pad=new VirtualPad(Stored);
  var result=await LightingTransaction.RunAsync(pad.Exchange,Serials(),null,null,0xff,Folder(),TimeSpan.Zero);
  var expected=Convert.FromHexString(Stored);expected[5]=0xff;
  Assert.Equal(expected,result);Assert.Equal(expected,pad.Record);
  Assert.Equal(2,pad.Sent.Count(s=>s.op==0x37));
 }
 [Fact]
 public async Task WriteUsesTheCapturedChunkFraming()
 {
  var pad=new VirtualPad(Stored);
  await LightingTransaction.RunAsync(pad.Exchange,Serials(),[0,255,255],2,0x28,Folder(),TimeSpan.Zero);
  var writes=pad.Sent.Where(s=>s.op==0x37).ToArray();
  Assert.Equal([(byte)0x37,(byte)0x37],writes.Select(w=>w.op).ToArray());
  Assert.Equal([15,10],writes.Select(w=>w.size).ToArray());
  Assert.Equal([(byte)0x81,(byte)0x0a],writes.Select(w=>w.serial).ToArray());
  Assert.Equal([(byte)0x34,(byte)0x4f],writes.Select(w=>w.length).ToArray());
  Assert.Equal("00FFFF",Convert.ToHexString(pad.Record[..3]));Assert.Equal(0x28,pad.Record[5]);Assert.Equal(2,pad.Record[6]);
 }
 [Fact]
 public async Task IdenticalTargetSendsNothing()
 {
  var pad=new VirtualPad(Stored);
  await LightingTransaction.RunAsync(pad.Exchange,Serials(),null,null,0x50,Folder(),TimeSpan.Zero);
  Assert.DoesNotContain(pad.Sent,s=>s.op==0x37);
 }
 [Fact]
 public async Task DroppedWriteIsCaughtByReadBackAndLeavesACheckpoint()
 {
  var pad=new VirtualPad(Stored,dropWrites:true);var folder=Folder();
  var error=await Assert.ThrowsAsync<IOException>(()=>LightingTransaction.RunAsync(pad.Exchange,Serials(),null,null,0xff,folder,TimeSpan.Zero));
  Assert.Contains("read-back differs",error.Message);
  Assert.Equal(Stored.ToUpperInvariant(),File.ReadAllText(Directory.GetFiles(folder,"*-before.txt").Single()));
 }
 [Fact]
 public async Task WrongAckStopsAfterTheFirstChunk()
 {
  var pad=new VirtualPad(Stored,wrongAck:true);
  await Assert.ThrowsAsync<IOException>(()=>LightingTransaction.RunAsync(pad.Exchange,Serials(),null,null,0xff,Folder(),TimeSpan.Zero));
  Assert.Single(pad.Sent,s=>s.op==0x37);
 }
 [Theory]
 [InlineData(0,0x10)] [InlineData(100,0xff)]
 public void BrightnessEndsMapToTheTestedRange(double percent,byte expected)=>Assert.Equal(expected,LightingRecord.BrightnessByteFor(percent));
 [Fact]
 public void TheStoredDefaultReadsAsAboutAQuarter()=>Assert.Equal(27,Math.Round(LightingRecord.BrightnessPercentFor(0x50)));
}
