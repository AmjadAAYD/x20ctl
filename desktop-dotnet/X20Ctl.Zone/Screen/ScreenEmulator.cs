using System.Buffers.Binary;
namespace X20Ctl.Zone.Screen;
/// <summary>Captured NOR profile emulator. Not a hardware identity or compatibility check.</summary>
public sealed class ScreenEmulator:IScreenTransport
{
 public bool IsEmulated=>true;
 public List<byte[]> Requests {get;}=[];
 readonly Dictionary<uint,byte> flash=[];
 public byte[] Read(uint address,int length)=>Enumerable.Range(0,length).Select(i=>flash.GetValueOrDefault(address+(uint)i,(byte)255)).ToArray();
 public ValueTask<byte[]> ExchangeAsync(ReadOnlyMemory<byte> request,CancellationToken cancellationToken)
 {
  cancellationToken.ThrowIfCancellationRequested();var b=request.ToArray();if(b.Length==0)throw new InvalidDataException("Empty screen request.");Requests.Add(b);var reply=new byte[63];
  switch(b[0])
  {
   case 0x4b:
    if(b.Length!=64||ScreenCodec.Crc(b.AsSpan(0,32))!=BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(32)))throw new InvalidDataException("Handshake CRC mismatch.");
    var check=b.AsSpan(0,16).ToArray();ScreenCodec.Put32(check,8,ScreenProtocol.ModelMagic);if(b[1] is not (0x30 or 0x32)||b[2]!=0x4c||b[3]!=0x54||ScreenCodec.Crc(check.AsSpan(0,12))!=BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(8)))throw new InvalidDataException("Handshake model CRC mismatch.");ScreenCodec.Put32(check,12,0x4c544f50);reply[0]=0x4b;ScreenCodec.Put32(reply,1,ScreenCodec.Crc(check));ScreenCodec.Put32(reply,9,0x100);ScreenCodec.Put32(reply,13,0xeeba0080);reply[30]=0x78;reply[31]=0x87;break;
   case 0x6d:
    if(b.Length!=64||b[1]!=1)throw new InvalidDataException("Unexpected flash query.");reply[0]=0x7d;ScreenCodec.Put32(reply,1,ScreenProtocol.FlashId);break;
   case 0x6c:
    var part=ScreenProtocol.ParseData(b);CheckRange(part.Address,part.Payload.Length);
    if(part.Mode==0){uint sector=part.Address&0xfffff000;for(uint i=0;i<4096;i++)flash.Remove(sector+i);}
    for(int i=0;i<part.Payload.Length;i++){uint a=part.Address+(uint)i;flash[a]=(byte)(flash.GetValueOrDefault(a,(byte)255)&part.Payload[i]);}reply[0]=0x7c;break;
   case 0x6e:
    if(b.Length!=64||ScreenCodec.Crc(b.AsSpan(0,13))!=BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(13)))throw new InvalidDataException("CRC query packet mismatch.");
    uint address=BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(1));int n=checked((int)BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(5)));CheckRange(address,n);reply[0]=0x7e;reply[1]=0x31;ScreenCodec.Put32(reply,2,ScreenCodec.Crc(Read(address,n)));break;
   default:throw new InvalidDataException("Unmodeled screen command.");
  }
  return ValueTask.FromResult(reply);
 }
 static void CheckRange(uint address,int n){if(n<1||n>ScreenCodec.AssetLimit||address<0x400000||(ulong)address+(uint)n>0x710000)throw new InvalidDataException("Outside screen emulator memory.");}
}
