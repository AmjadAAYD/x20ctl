using System.Buffers.Binary;
namespace X20Ctl.Zone.Screen;

/// <summary>Independent format implementation. Inputs are already decoded/cropped RGBA pixels.</summary>
public static partial class ScreenCodec
{
 public const int Width=160,Height=80,InputLimit=64*1024*1024,AssetLimit=3*1024*1024;
 public static uint Crc(ReadOnlySpan<byte> data)
 {
  uint crc=0;foreach(byte b in data){crc^=b;for(int bit=0;bit<8;bit++)crc=(crc>>1)^((crc&1)!=0?0xedb88320u:0);}return crc;
 }
 static ushort[] Words(byte[] rgba,int width,int height)
 {
  if(width<1||height<1||width>65535||height>65535||rgba.Length!=checked(width*height*4))throw new ArgumentException("Expected decoded RGBA pixels.");
  var words=new ushort[width*height];int n=0;
  for(int y=0;y<width;y++)for(int x=0;x<height;x++){int i=(x*width+width-1-y)*4;words[n++]=(ushort)(((rgba[i]&248)<<8)|((rgba[i+1]&252)<<3)|(rgba[i+2]>>3));}
  return words;
 }
 public static byte[] EncodeStill(byte[] rgba,int width=Width,int height=Height)
 {
  var words=Words(rgba,width,height);int length=checked(words.Length*2);if(length+12>AssetLimit)throw new ArgumentException("Image exceeds 3 MiB.");
  var result=new byte[length+12];Put32(result,0,12);Put16(result,4,height);Put16(result,6,width);Put24(result,8,length);
  for(int i=0;i<words.Length;i++)BinaryPrimitives.WriteUInt16BigEndian(result.AsSpan(12+i*2),words[i]);return result;
 }
 public static byte[] EncodeAnimation(IReadOnlyList<byte[]> frames,int width=Width,int height=Height,bool truncateToLimit=false,CancellationToken cancellationToken=default,Action<string>? diagnostic=null)
 {
  if(frames.Count is <1 or >65535)throw new ArgumentException("Animation must contain 1 to 65535 frames.");
  var parts=new List<(byte[] Data,byte Kind)>();int payloadSize=0;
  foreach(var frame in frames){cancellationToken.ThrowIfCancellationRequested();var part=Compress(Words(frame,width,height),height,width,diagnostic);if(truncateToLimit&&2+16*parts.Count+payloadSize+part.Data.Length>AssetLimit)break;parts.Add(part);payloadSize=checked(payloadSize+part.Data.Length);if(!truncateToLimit&&2+16*parts.Count+payloadSize>AssetLimit)throw new ArgumentException("Animation exceeds 3 MiB. Reduce its frames.");}
  if(parts.Count==0)throw new ArgumentException("No animation frame fits.");
  return Assemble(parts,width,height);
 }
 static byte[] Assemble(List<(byte[] Data,byte Kind)> parts,int width,int height)
 {
  int payloadSize=parts.Sum(p=>p.Data.Length),offset=2+parts.Count*16+payloadSize;
  var result=new byte[offset];Put16(result,0,parts.Count);offset=2+parts.Count*16;
  for(int i=0;i<parts.Count;i++){int row=2+i*16;var part=parts[i];Put32(result,row,(uint)offset);Put16(result,row+8,height);Put16(result,row+10,width);Put24(result,row+12,part.Data.Length);result[row+15]=part.Kind;part.Data.CopyTo(result,offset);offset+=part.Data.Length;}return result;
 }
 /// <summary>The whole animation when it fits the 3 MiB limit; otherwise every second, third, … frame (the first is always kept),
 /// with the delay stretched by the same step so the loop keeps its speed and length. Returns how many frames were used.</summary>
 public static byte[] EncodeAnimationFitting(IReadOnlyList<byte[]> frames,int delayMs,out int used,out int delayUsed,int width=Width,int height=Height,CancellationToken cancellationToken=default)
 {
  if(frames.Count is <1 or >65535)throw new ArgumentException("Animation must contain 1 to 65535 frames.");
  var compressed=new (byte[] Data,byte Kind)[frames.Count];
  foreach(var i in Enumerable.Range(0,frames.Count)){cancellationToken.ThrowIfCancellationRequested();compressed[i]=Compress(Words(frames[i],width,height),height,width,null);}
  for(int step=1;step<=frames.Count;step++)
  {
   var kept=Enumerable.Range(0,(frames.Count+step-1)/step).Select(n=>compressed[n*step]).ToList();
   if(2+16*kept.Count+kept.Sum(p=>p.Data.Length)>AssetLimit)continue;
   used=kept.Count;delayUsed=Math.Clamp(delayMs*step,10,2550);return Assemble(kept,width,height);
  }
  throw new ArgumentException("Not even one frame fits. Use a simpler image.");
 }
 static (byte[] Data,byte Kind) Compress(ushort[] words,int width,int height,Action<string>? diagnostic)
 {
  var raw=new byte[words.Length*2];for(int i=0;i<words.Length;i++)Put16(raw,i*2,words[i]);
  var blocks=new List<(ushort[] Palette,byte Mode,byte[] Stream)>();int start=0;
  while(start<words.Length)
  {
   var palette=new List<ushort>{words[start]};var lookup=new Dictionary<ushort,byte>{{words[start],0}};int end=start+1;
   while(end<words.Length){ushort value=words[end];if(!lookup.ContainsKey(value)){lookup[value]=(byte)palette.Count;palette.Add(value);if(palette.Count==256)break;}end++;}
   var indices=new byte[end-start];for(int i=start;i<end;i++)indices[i-start]=lookup[words[i]];
   var runs=new List<byte>();for(int i=0;i<indices.Length;){int n=1;while(n<256&&i+n<indices.Length&&indices[i+n]==indices[i])n++;runs.Add((byte)(n-1));runs.Add(indices[i]);i+=n;}
   byte mode=(byte)(runs.Count<indices.Length?1:0);blocks.Add((palette.ToArray(),mode,mode==1?runs.ToArray():indices));start=end;if(blocks.Count>255)return(raw,0);
  }
  if(blocks.Sum(b=>b.Stream.Length)>=raw.Length*7/10)return(raw,0);
  int size=12+blocks.Count*516+blocks.Sum(b=>b.Stream.Length);if((size&1)!=0&&size>=raw.Length){diagnostic?.Invoke("Raw fallback: odd LT payload exceeds the raw buffer.");return(raw,0);}var result=new byte[(size+1)&~1];result[0]=0x4c;result[1]=0x54;Put16(result,2,width);Put16(result,4,height);result[6]=(byte)blocks.Count;result[7]=3;Put32(result,8,(uint)size);
  int row=12,offset=12+516*blocks.Count;foreach(var block in blocks){Put24(result,row,block.Stream.Length);result[row+3]=block.Mode;for(int i=0;i<block.Palette.Length;i++)Put16(result,row+4+i*2,block.Palette[i]);row+=516;block.Stream.CopyTo(result,offset);offset+=block.Stream.Length;}
  // Original compressor overwrites its raw buffer; odd alignment retains the raw byte at this offset.
  // Padding beyond the original raw allocation is not established by the captured cases.
  if((size&1)!=0&&size<raw.Length)result[size]=raw[size];
  return(result,3);
 }
 public static byte[] Metadata(byte[] asset,bool animation,int delayMs=100)
 {
  if(asset.Length is <1 or >AssetLimit)throw new ArgumentException("Invalid screen asset size.");
  var block=Enumerable.Repeat((byte)255,4096).ToArray();new byte[]{0x68,0x45,0x71,0x19}.CopyTo(block,0);Put32(block,4,animation?5u:3u);Put32(block,8,(uint)asset.Length);Put32(block,12,Crc(asset));Put32(block,16,Crc(block.AsSpan(0,16)));if(animation)block[20]=(byte)Math.Clamp(delayMs/10,1,255);return block;
 }
 internal static void Put16(byte[] b,int i,int n)=>BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(i),checked((ushort)n));
 internal static void Put24(byte[] b,int i,int n){if(n<0||n>0xffffff)throw new ArgumentOutOfRangeException(nameof(n));b[i]=(byte)n;b[i+1]=(byte)(n>>8);b[i+2]=(byte)(n>>16);}
 internal static void Put32(byte[] b,int i,uint n)=>BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(i),n);
}
