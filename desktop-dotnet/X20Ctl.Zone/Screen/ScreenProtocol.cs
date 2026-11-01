using System.Buffers.Binary;
namespace X20Ctl.Zone.Screen;
public static class ScreenProtocol
{
 public const uint AssetAddress=0x410000,MetadataAddress=0x400000;
 // This profile reproduces the captured synthetic LT7589 / W25Q128 transaction only.
 public const uint ModelMagic=0x19717589,FlashId=0xef4018;
 /// <summary>Recovered local-time challenge; this is not a cryptographic random number.</summary>
 public static uint Nonce(DateTime local)=>unchecked((uint)(((local.Month*12+local.Day)*86400)+(local.Hour*3600)+(local.Minute*60)+local.Second+(local.Year-2000)*local.Millisecond));
 public static byte[] Handshake(byte command,uint nonce,byte baudIndex=0x7f)
 {
  if(command is not (0x30 or 0x32))throw new ArgumentOutOfRangeException(nameof(command));
  var b=new byte[64];b[0]=0x4b;b[1]=command;b[2]=0x4c;b[3]=0x54;ScreenCodec.Put32(b,4,nonce);ScreenCodec.Put32(b,8,ModelMagic);ScreenCodec.Put32(b,8,ScreenCodec.Crc(b.AsSpan(0,12)));ScreenCodec.Put32(b,12,0x17e8);b[16]=baudIndex;ScreenCodec.Put32(b,32,ScreenCodec.Crc(b.AsSpan(0,32)));return b;
 }
 public static byte[] FlashQuery(){var b=new byte[64];b[0]=0x6d;b[1]=1;return b;}
 public static byte[] Data(uint address,ReadOnlySpan<byte> payload,byte mode=0)
 {
  if(payload.Length is <1 or >2048||mode>2)throw new ArgumentException("Invalid screen data packet.");
  var b=new byte[payload.Length+12];b[0]=0x6c;ScreenCodec.Put32(b,1,address);b[5]=mode;ScreenCodec.Put16(b,6,payload.Length);payload.CopyTo(b.AsSpan(8));ScreenCodec.Put32(b,b.Length-4,ScreenCodec.Crc(b.AsSpan(0,b.Length-4)));return b;
 }
 public static (uint Address,byte Mode,byte[] Payload) ParseData(byte[] b)
 {
  if(b.Length<13||b[0]!=0x6c||b[5]>2)throw new InvalidDataException("Unexpected screen data packet.");
  int n=BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(6));if(n>2048||b.Length!=n+12||ScreenCodec.Crc(b.AsSpan(0,b.Length-4))!=BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(b.Length-4)))throw new InvalidDataException("Screen packet length or CRC mismatch.");
  return(BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(1)),b[5],b.AsSpan(8,n).ToArray());
 }
 public static byte[] CrcQuery(uint address,byte[] asset,uint? requestedCrc=null)
 {
  var b=new byte[64];b[0]=0x6e;ScreenCodec.Put32(b,1,address);ScreenCodec.Put32(b,5,(uint)asset.Length);ScreenCodec.Put32(b,9,requestedCrc??ScreenCodec.Crc(asset));ScreenCodec.Put32(b,13,ScreenCodec.Crc(b.AsSpan(0,13)));return b;
 }
 public static void ValidateHandshake(byte[] request,byte[] reply)
 {
  var check=request.AsSpan(0,16).ToArray();ScreenCodec.Put32(check,8,ModelMagic);ScreenCodec.Put32(check,12,0x4c544f50);
  if(reply.Length<32||reply[0]!=0x4b||BinaryPrimitives.ReadUInt32LittleEndian(reply.AsSpan(1))!=ScreenCodec.Crc(check))throw new InvalidDataException("Invalid screen handshake reply.");
 }
}
