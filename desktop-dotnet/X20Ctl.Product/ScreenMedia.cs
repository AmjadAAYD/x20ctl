using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace X20Ctl.Product;
internal static class ScreenMedia
{
 internal sealed record Decoded(IReadOnlyList<BitmapSource> Frames,int Delay,bool UnequalDelays=false);
 // WIC decodes individual GIF rectangles; compose them before cropping.
 public static Decoded Decode(BitmapDecoder decoder)
 {
  if(decoder.Frames.Count==0)throw new ArgumentException("Image contains no frames.");
  if(decoder is not GifBitmapDecoder){var f=decoder.Frames[0];CheckSize(f.PixelWidth,f.PixelHeight,1);f.Freeze();return new([f],100);}
  int width=Query(decoder.Metadata as BitmapMetadata,"/logscrdesc/Width",decoder.Frames[0].PixelWidth),height=Query(decoder.Metadata as BitmapMetadata,"/logscrdesc/Height",decoder.Frames[0].PixelHeight);CheckSize(width,height,1);double shrink=ShrinkFor(width,height,decoder.Frames.Count);
  var canvas=new byte[checked(width*height*4)];var result=new List<BitmapSource>();int delay=100;bool unequal=false;
  foreach(var frame in decoder.Frames)
  {
   var meta=frame.Metadata as BitmapMetadata;int left=Query(meta,"/imgdesc/Left",0),top=Query(meta,"/imgdesc/Top",0),disposal=Query(meta,"/grctlext/Disposal",0);
   int currentDelay=Math.Clamp(Query(meta,"/grctlext/Delay",10)*10,10,2550);if(result.Count==0)delay=currentDelay;else unequal|=delay!=currentDelay;
   if(left<0||top<0||left+frame.PixelWidth>width||top+frame.PixelHeight>height)throw new ArgumentException("GIF frame exceeds its canvas.");
   byte[]? previous=disposal==3?canvas.ToArray():null;var converted=new FormatConvertedBitmap(frame,PixelFormats.Bgra32,null,0);var pixels=new byte[frame.PixelWidth*frame.PixelHeight*4];converted.CopyPixels(pixels,frame.PixelWidth*4,0);
   for(int y=0;y<frame.PixelHeight;y++)for(int x=0;x<frame.PixelWidth;x++){int s=(y*frame.PixelWidth+x)*4,d=((top+y)*width+left+x)*4;if(pixels[s+3]!=0)Array.Copy(pixels,s,canvas,d,4);}
   BitmapSource snapshot=BitmapSource.Create(width,height,96,96,PixelFormats.Bgra32,null,canvas,width*4);if(shrink<1)snapshot=new TransformedBitmap(snapshot,new ScaleTransform(shrink,shrink));snapshot.Freeze();result.Add(snapshot);
   if(disposal==2)for(int y=0;y<frame.PixelHeight;y++)Array.Clear(canvas,((top+y)*width+left)*4,frame.PixelWidth*4);
   else if(previous!=null)canvas=previous;
  }
  return new(result,delay,unequal);
 }
 /// <summary>How far to scale each composed GIF frame down so every frame together stays inside the preview budget. The screen is
 /// 160 × 80, so a big GIF only needs enough pixels to crop from, not its full size.</summary>
 internal static double ShrinkFor(int width,int height,int frames)=>Math.Min(1,Math.Sqrt(Budget/((double)width*height*4*Math.Max(1,frames))));
 const long Budget=64L*1024*1024;
 static int Query(BitmapMetadata? metadata,string path,int fallback){try{return metadata?.ContainsQuery(path)==true?Convert.ToInt32(metadata.GetQuery(path)):fallback;}catch(NotSupportedException){return fallback;}}
 static void CheckSize(int width,int height,int frames){if(width<1||height<1||(long)width*height*4*frames>Budget)throw new ArgumentException("Decoded artwork exceeds the 64 MiB preview budget. Resize it first.");}
}
