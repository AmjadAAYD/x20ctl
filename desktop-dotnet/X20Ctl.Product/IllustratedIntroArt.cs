using System.Windows;
using System.Windows.Media;

namespace X20Ctl.Product;

/// <summary>A finite, silent native story for each art direction; no video decoder or idle clock.</summary>
internal sealed class IllustratedIntroArt:FrameworkElement
{
    internal static IntroSpec CreateSpec(IllustratedThemeSpec theme)
    {
        var ink=IllustratedThemeUI.LogoInk(theme);double height=600*ink.Height/ink.Width;
        return new("illustrated",1.7,1.25,1.8,new(660,540-height/2,600,height),new(512,512),ink);
    }
    private double time;
    public double Time {get=>time;set{time=value;InvalidateVisual();}}
    protected override void OnRender(DrawingContext dc)
    {
        if(IllustratedThemeUI.Current is not {} s||IllustratedThemeUI.Image(s.Wallpaper("Intro")) is not {} image)return;
        double w=ActualWidth,h=ActualHeight,q=Math.Clamp(time/1.25,0,1);q=q*q*(3-2*q);
        if(w<=0||h<=0||q<=0)return;
        Geometry Polygon(params Point[] points){var g=new StreamGeometry();using(var c=g.Open()){c.BeginFigure(points[0],true,true);c.PolyLineTo(points.Skip(1).ToArray(),true,false);}g.Freeze();return g;}
        Geometry clip=s.Surface switch
        {
            "chrome"=>new RectangleGeometry(new Rect(0,h*(1-q),w,h*q)), // sunset rises over the water
            "gel"=>new EllipseGeometry(new Point(w*.5,h*.55),w*q,h*q), // bubble opens into the valley
            "iron"=>Polygon(new(w*.5, h*(1-q)),new(w*(.5+.5*q),h*.35*(1-q)),new(w*(.5+.5*q),h),new(w*(.5-.5*q),h),new(w*(.5-.5*q),h*.35*(1-q))),
            "ink"=>Polygon(new(0,0),new(w*q,0),new(Math.Min(w,w*q+w*.1*(1-q)),h),new(0,h)), // ink stroke sweeps the page
            "neon"=>Polygon(new(w*(.5-.5*q),0),new(w*(.5+.5*q),0),new(w,h*q),new(w,h),new(0,h),new(0,h*q)),
            "paper"=>Polygon(new(0,h*(.5-.5*q)),new(w*.25,h*(.5-.46*q)),new(w*.28,h*(.5-.5*q)),new(w,h*(.5-.5*q)),new(w,h*(.5+.5*q)),new(w*.7,h*(.5+.46*q)),new(0,h*(.5+.5*q))),
            _=>new RectangleGeometry(new Rect(0,0,w,h))
        };
        // The final frame must be full bleed, including corners outside a circle or torn reveal.
        if(q>=.999)clip=new RectangleGeometry(new Rect(0,0,w,h));
        dc.PushClip(clip);
        if(s.Surface=="glass")dc.PushOpacity(q);
        dc.DrawImage(image,new Rect(0,0,w,h));
        if(s.Surface=="glass")dc.Pop();
        dc.Pop();
    }
}
