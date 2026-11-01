namespace X20Ctl.Zone.Screen;
public static class ScreenTransferPlan
{
 /// <summary>Pure dry-run packet construction. A fixed nonce makes the frame list reproducible.</summary>
 public static IReadOnlyList<byte[]> Build(byte[] asset,bool animation,int delayMs,uint nonce)
 {
  _=ScreenCodec.DecodeAsset(asset,animation);var result=new List<byte[]>();
  foreach(var (address,data) in new[]{(ScreenProtocol.AssetAddress,asset),(ScreenProtocol.MetadataAddress,ScreenCodec.Metadata(asset,animation,delayMs))})
  {
   result.Add(ScreenProtocol.Handshake(0x30,nonce));result.Add(ScreenProtocol.Handshake(0x32,nonce));result.Add(ScreenProtocol.FlashQuery());
   for(int offset=0;offset<data.Length;offset+=2048)result.Add(ScreenProtocol.Data(address+(uint)offset,data.AsSpan(offset,Math.Min(2048,data.Length-offset)),(byte)(offset%4096==0?0:1)));
   result.Add(ScreenProtocol.CrcQuery(address,data));
  }
  return result;
 }
}
