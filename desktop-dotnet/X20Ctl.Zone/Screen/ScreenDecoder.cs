using System.Buffers.Binary;
namespace X20Ctl.Zone.Screen;
public static partial class ScreenCodec
{
 public static bool IsAnimationAsset(byte[] asset)=>asset.Length>=4&&BinaryPrimitives.ReadUInt32LittleEndian(asset)!=12;
 /// <summary>Strict decoder of the actual encoded asset, returned in 160x80 display orientation.</summary>
 public static IReadOnlyList<byte[]> DecodeAsset(byte[] asset,bool animation)
 {
  try
  {
   if(asset.Length<12||asset.Length>AssetLimit)throw new InvalidDataException("Invalid screen asset length.");
   int count=animation?BinaryPrimitives.ReadUInt16LittleEndian(asset):1,table=animation?checked(2+count*16):12;
   if(count<1||count>512||table>asset.Length)throw new InvalidDataException("Invalid frame table.");var frames=new List<byte[]>();int previous=table;
   for(int f=0;f<count;f++)
   {
    int row=animation?2+f*16:0;int offset=checked((int)BinaryPrimitives.ReadUInt32LittleEndian(asset.AsSpan(row))),w=BinaryPrimitives.ReadUInt16LittleEndian(asset.AsSpan(row+(animation?8:4))),h=BinaryPrimitives.ReadUInt16LittleEndian(asset.AsSpan(row+(animation?10:6)));
    int lengthAt=row+(animation?12:8),length=asset[lengthAt]|asset[lengthAt+1]<<8|asset[lengthAt+2]<<16;byte kind=asset[lengthAt+3];
    if(w!=80||h!=160||offset!=previous||length<1||(long)offset+length>asset.Length)throw new InvalidDataException("Invalid screen frame geometry or bounds.");
    var payload=asset.AsSpan(offset,length);var words=new ushort[12800];
    if(kind==0)
    {
     if(length!=25600)throw new InvalidDataException("Invalid raw frame length.");for(int i=0;i<words.Length;i++)words[i]=animation?BinaryPrimitives.ReadUInt16LittleEndian(payload[(i*2)..]):BinaryPrimitives.ReadUInt16BigEndian(payload[(i*2)..]);
    }
    else if(kind==3)
    {
     if(length<12||payload[0]!=0x4c||payload[1]!=0x54||BinaryPrimitives.ReadUInt16LittleEndian(payload[2..])!=80||BinaryPrimitives.ReadUInt16LittleEndian(payload[4..])!=160||payload[7]!=3)throw new InvalidDataException("Invalid LT header.");
     int n=payload[6],declared=checked((int)BinaryPrimitives.ReadUInt32LittleEndian(payload[8..])),cursor=12+n*516,pixel=0;
     if(n<1||declared<cursor||declared>length||length!=((declared+1)&~1))throw new InvalidDataException("Invalid LT length.");
     for(int b=0;b<n;b++)
     {
      int record=12+b*516,size=payload[record]|payload[record+1]<<8|payload[record+2]<<16;byte mode=payload[record+3];if(mode>1||size<1||cursor+size>declared||mode==1&&(size&1)!=0)throw new InvalidDataException("Invalid LT stream.");
      int end=cursor+size;while(cursor<end){int run=mode==1?payload[cursor++]+1:1;int index=payload[cursor++];ushort color=BinaryPrimitives.ReadUInt16LittleEndian(payload[(record+4+index*2)..]);if(pixel+run>words.Length)throw new InvalidDataException("LT frame overrun.");Array.Fill(words,color,pixel,run);pixel+=run;}
     }
     if(cursor!=declared||pixel!=words.Length)throw new InvalidDataException("Incomplete LT frame.");
    }
    else throw new InvalidDataException("Unsupported screen pixel format.");
    var rgba=new byte[51200];int at=0;for(int y=0;y<160;y++)for(int x=0;x<80;x++){ushort word=words[at++];int i=(x*160+159-y)*4;rgba[i]=(byte)((word>>8)&248);rgba[i+1]=(byte)((word>>3)&252);rgba[i+2]=(byte)((word<<3)&248);rgba[i+3]=255;}frames.Add(rgba);previous=offset+length;
   }
   if(previous!=asset.Length)throw new InvalidDataException("Unexpected trailing asset bytes.");return frames;
  }
  catch(Exception e)when(e is ArgumentOutOfRangeException or IndexOutOfRangeException or OverflowException){throw new InvalidDataException("Malformed screen asset.",e);}
 }
}
