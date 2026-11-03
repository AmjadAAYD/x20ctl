using System.Buffers.Binary;
namespace X20Ctl.Zone.Screen;
public static class ScreenProtocol
{
 public const uint AssetAddress=0x410000,MetadataAddress=0x400000;
 // This profile reproduces the captured synthetic LT7589 / W25Q128 transaction only.
 public const uint ModelMagic=0x19717589,FlashId=0xef4018;
 // DIYTool tries a table of chip IDs on Open Port. The X20 Pro's screen chip signs its handshake reply with
 // 0x19711650, the table's "LT165" entry (owner's pad, 3 Nov 2026).
 public const uint X20ProMagic=0x19711650;
 // DIYTool's chip table (DAT_00431080), in its order. The chip signs its reply with its own entry whatever was sent,
 // so the reply names the chip. Only LT165 has run on a real pad.
 public static readonly uint[] KnownChips=[0x19717589,0x19711650,0x19710680,0x19711630,0x19713630,0x1971168a,0x1971168b,0x1971168c,0x1971168d,0x1971268a,0x1971268b,0x19712690,0x1971268c,0x1971268d,0x19717689,0x19712650,0x19717790];
 public static uint? IdentifyChip(byte[] request,byte[] reply)
 {
  foreach(uint magic in KnownChips){try{ValidateHandshake(request,reply,magic);return magic;}catch(InvalidDataException){}}
  return null;
 }
 // DIYTool's Flash.ini NOR chips with at least 8 MiB, enough for metadata at 0x400000 and a 3 MiB asset at 0x410000.
 // Proven: the captured profile's W25Q128 and the owner's X20 Pro ZB25VQ64 (0x5E4017).
 public static readonly uint[] SupportedFlash=[0xef4017,0xba3217,0xf83217,0xef6017,0x856017,0x5e4017,0x684017,0xef4018,0xef6018,0x856018,0x5e4018,0xc84018,0x0b4018,0x852018,0x201985,0x684018,0x522217,0x522118,0x1c7018,0xef4019,0xc84019,0xef4020,0x0b4019,0xc22019];
 /// <summary>Recovered local-time challenge; this is not a cryptographic random number.</summary>
 public static uint Nonce(DateTime local)=>unchecked((uint)(((local.Month*12+local.Day)*86400)+(local.Hour*3600)+(local.Minute*60)+local.Second+(local.Year-2000)*local.Millisecond));
 public static byte[] Handshake(byte command,uint nonce,byte baudIndex=0x7f,uint magic=ModelMagic)
 {
  if(command is not (0x30 or 0x32))throw new ArgumentOutOfRangeException(nameof(command));
  var b=new byte[64];b[0]=0x4b;b[1]=command;b[2]=0x4c;b[3]=0x54;ScreenCodec.Put32(b,4,nonce);ScreenCodec.Put32(b,8,magic);ScreenCodec.Put32(b,8,ScreenCodec.Crc(b.AsSpan(0,12)));ScreenCodec.Put32(b,12,0x17e8);b[16]=baudIndex;ScreenCodec.Put32(b,32,ScreenCodec.Crc(b.AsSpan(0,32)));return b;
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
 public static void ValidateHandshake(byte[] request,byte[] reply,uint magic=ModelMagic)
 {
  var check=request.AsSpan(0,16).ToArray();ScreenCodec.Put32(check,8,magic);ScreenCodec.Put32(check,12,0x4c544f50);
  if(reply.Length<32||reply[0]!=0x4b||BinaryPrimitives.ReadUInt32LittleEndian(reply.AsSpan(1))!=ScreenCodec.Crc(check))throw new InvalidDataException("Invalid screen handshake reply.");
 }
}
