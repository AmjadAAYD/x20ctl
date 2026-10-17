using System.Windows;
using System.Windows.Media;
namespace X20Ctl.Product;

/// <summary>Quiet coloured atmosphere opens into a single light impact when the X seats.</summary>
internal sealed class IntroStabilizationBackground : FrameworkElement
{
    private double time=-10;
    public IntroStabilizationBackground(){IsHitTestVisible=false;}
    public void SetTime(double secondsFromLanding){time=secondsFromLanding;InvalidateVisual();}
    protected override void OnRender(DrawingContext dc)
    {
        double w=ActualWidth,h=ActualHeight;if(w<=0||h<=0)return;
        // A fast eased arrival, followed by a longer release; no flash, shake or repeating pulse.
        double hit=time<0?0:(1-Math.Exp(-time/0.055))*Math.Exp(-time/.28);
        double travel=time<0?0:1-Math.Exp(-time/.38);
        void Pool(double x,double y,Color colour,double alpha,double rx,double ry)
        {
            var brush=new RadialGradientBrush(new GradientStopCollection{
                new(Color.FromArgb((byte)Math.Clamp(alpha*255,0,255),colour.R,colour.G,colour.B),0),
                new(Color.FromArgb((byte)Math.Clamp(alpha*90,0,255),colour.R,colour.G,colour.B),.42),
                new(Color.FromArgb(0,colour.R,colour.G,colour.B),1) });
            dc.DrawEllipse(brush,null,new Point(w*x,h*y),w*rx,h*ry);
        }
        Pool(.27-travel*.12,.53,Color.FromRgb(48,135,228),.045+hit*.30,.51,.62);
        Pool(.73+travel*.12,.47,Color.FromRgb(224,115,48),.032+hit*.23,.51,.62);
        if(hit<=.001)return;
        Pool(.5,.50,Color.FromRgb(179,208,244),hit*.20,.46+travel*.18,.40+travel*.12);
        // Two broad, feathered cuts extend the X directions into its surrounding environment.
        foreach(double angle in new[]{-43d,43d})
        {
            dc.PushTransform(new RotateTransform(angle,w/2,h/2));
            var light=new LinearGradientBrush(new GradientStopCollection{
                new(Colors.Transparent,0),new(Color.FromArgb((byte)(hit*30),165,198,236),.5),new(Colors.Transparent,1)},90);
            dc.DrawRectangle(light,null,new Rect(w*.08,h*.5-18,w*.84,36));dc.Pop();
        }
    }
}
