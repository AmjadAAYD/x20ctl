using X20Ctl.Zone.Screen;
namespace X20Ctl.Zone.Tests;
/// <summary>Bytes read from the owner's X20 Pro over COM7 on 3 Nov 2026, with nothing written by X20CTL.</summary>
public class ScreenX20ProTests
{
 [Fact]
 public void PadSignsItsHandshakeAsLt165()
 {
  var request=Convert.FromHexString("4B304C543CF9B2008AA84E5AE8170000090000000000000000000000000000001AC6889B00000000000000000000000000000000000000000000000000000000");
  var reply=Convert.FromHexString("4BD41F039EE714CC37010000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000");
  ScreenProtocol.ValidateHandshake(request,reply,ScreenProtocol.X20ProMagic);
  Assert.Throws<InvalidDataException>(()=>ScreenProtocol.ValidateHandshake(request,reply));
  Assert.Equal(ScreenProtocol.X20ProMagic,ScreenProtocol.IdentifyChip(request,reply));
 }
 [Fact]
 public void SmallAndNandFlashAreRefused(){Assert.Throws<InvalidDataException>(()=>ScreenSender.RequireSupportedFlash(0xef4013));Assert.Throws<InvalidDataException>(()=>ScreenSender.RequireSupportedFlash(0xefaa21));}
 [Fact]
 public void PadsFlashIsAccepted()=>ScreenSender.RequireSupportedFlash(0x5e4017);
 [Fact]
 public void StillHeaderMatchesWhatDiyToolWrote()
 {
  // The owner's photo as DIYTool stored it: a 25612-byte still whose CRC the header carries.
  var header=Convert.FromHexString("68457119030000000C64000005EB7164F3A48646FFFFFFFF");
  var asset=new byte[25612];
  Assert.Equal(25612,ScreenCodec.EncodeStill(new byte[160*80*4]).Length);
  var ours=ScreenCodec.Metadata(asset,false);
  Assert.Equal(header[..8],ours[..8]);
  Assert.Equal(Convert.FromHexString("FFFFFFFF"),ours[20..24]);
  Assert.Equal(0x4686A4F3u,ScreenCodec.Crc(header.AsSpan(0,16)));
 }
}
