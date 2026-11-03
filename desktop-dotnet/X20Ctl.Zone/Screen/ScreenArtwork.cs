namespace X20Ctl.Zone.Screen;
public static class ScreenArtwork
{
 public static readonly string[] WallpaperNames=["Signal","Orbit","Horizon","Circuit","Grid","X20CTL"];
 public static byte[] Adjust(byte[] rgba,double brightness,double contrast)
 {
  if(rgba.Length<4||rgba.Length%4!=0||brightness is <-1 or >1||contrast is <0.25 or >2)throw new ArgumentException("Invalid artwork adjustment.");
  var result=rgba.ToArray();for(int i=0;i<result.Length;i+=4){double alpha=rgba[i+3]/255d;for(int c=0;c<3;c++)result[i+c]=(byte)Math.Clamp(Math.Round(((rgba[i+c]*alpha/255d-.5)*contrast+.5+brightness)*255,MidpointRounding.AwayFromZero),0,255);result[i+3]=255;}return result;
 }
 public static byte[] TestImage()
 {
  var data=new byte[51200];for(int y=0;y<80;y++)for(int x=0;x<160;x++){int i=(y*160+x)*4;bool right=x>=80,bottom=y>=40;data[i]=(byte)(!right&&!bottom||right&&bottom?248:0);data[i+1]=(byte)(right&&!bottom||right&&bottom?252:0);data[i+2]=(byte)(bottom?248:0);data[i+3]=255;}return data;
 }
 public static byte[] Wallpaper(int index)
 {
  if(index is <0 or >5)throw new ArgumentOutOfRangeException(nameof(index));var data=new byte[51200];
  for(int y=0;y<80;y++)for(int x=0;x<160;x++)
  {
   int i=(y*160+x)*4;bool mark=index switch{0=>(x+y*2)%28<4,1=>Math.Abs(Math.Sqrt((x-80)*(x-80)+(y-40)*(y-40))-28)<2,2=>y>50+(int)(8*Math.Sin(x*.07)),3=>x%32<2||y%20<2&&(x/32)%2==0,4=>x%16==0||y%16==0,_=>Math.Abs((x-80)-(y-40))<4||Math.Abs((x-80)+(y-40))<4};
   data[i]=(byte)(mark?40+index*30:6+x/12);data[i+1]=(byte)(mark?220-index*20:10+y/8);data[i+2]=(byte)(mark?240-index*18:25+index*4);data[i+3]=255;
  }
  return data;
 }
}
