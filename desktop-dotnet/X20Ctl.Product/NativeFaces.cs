using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Data;
using System.Windows.Media;

namespace X20Ctl.Product;

/// <summary>The five console skins on Default's layout: each draws its own key, tab and panel shapes around the real
/// controls and content. Flat (no glow), and nothing here ever touches controller art.</summary>
internal static class NativeFaces
{
    private static Brush B(Color c)=>IllustratedThemeUI.Brush(c);
    private static Color C(uint rgb)=>IllustratedThemeUI.C(rgb);
    private static Color A(Color c,double a)=>Color.FromArgb((byte)Math.Clamp(a*255,0,255),c.R,c.G,c.B);
    private static Geometry Poly(params Point[] points)
    {
        var g=new StreamGeometry();using(var c=g.Open()){c.BeginFigure(points[0],true,true);c.PolyLineTo(points.Skip(1).ToArray(),true,false);}g.Freeze();return g;
    }
    /// <summary>Corner cuts: tl, tr, br, bl, each 0 for square.</summary>
    private static Geometry Cut(Rect r,double tl,double tr,double br,double bl)=>Poly(
        new(r.Left+tl,r.Top),new(r.Right-tr,r.Top),new(r.Right,r.Top+tr),new(r.Right,r.Bottom-br),new(r.Right-br,r.Bottom),
        new(r.Left+bl,r.Bottom),new(r.Left,r.Bottom-bl),new(r.Left,r.Top+tl));
    private static void Corners(DrawingContext dc,Rect r,Brush brush,double length,double thick)
    {
        foreach(var (x,y,sx,sy) in new[]{(r.Left,r.Top,1,1),(r.Right,r.Top,-1,1),(r.Left,r.Bottom,1,-1),(r.Right,r.Bottom,-1,-1)})
        {
            dc.DrawRectangle(brush,null,new Rect(sx>0?x:x-length,sy>0?y:y-thick,length,thick));
            dc.DrawRectangle(brush,null,new Rect(sx>0?x:x-thick,sy>0?y:y-length,thick,length));
        }
    }

    // ---------------------------------------------------------------- keys, tabs and buttons
    public static void Control(DrawingContext dc,IllustratedThemeSpec s,double w,double h,bool hot,bool down,bool on,double blend)
    {
        var t=ThemeCatalog.Current;double dy=down?2:0;
        switch(s.Surface)
        {
            case "p4":
            {   // a black key with a pressed edge; chosen keys are solid yellow
                var face=new Rect(0,dy,w,Math.Max(1,h-4));
                dc.DrawRoundedRectangle(B(on?ThemeCatalog.Mix(t.Accent,Colors.Black,.3):Colors.Black),null,new Rect(0,4,w,h-4),3,3);
                dc.DrawRoundedRectangle(B(on?t.Accent:ThemeCatalog.Mix(C(0x1C1C1A),t.Accent,.08*blend)),new Pen(B(on?t.Accent:hot?t.Accent:C(0x34342F)),hot&&!on?2:1.5),face,3,3);
                break;
            }
            case "p3":
            {   // a skewed blade; chosen is white with a red notch and underline
                double k=Math.Min(12,h*.32);var blade=Poly(new(k,dy),new(w,dy),new(w-k,h-3+dy),new(0,h-3+dy));
                dc.DrawGeometry(B(on?Colors.White:A(ThemeCatalog.Mix(C(0x040A26),C(0x0A2F9C),.25*blend),.86)),hot&&!on?new Pen(B(Colors.White),1.5):null,blade);
                if(on){dc.DrawGeometry(B(C(0xFF2A44)),null,Poly(new(w-14,dy),new(w,dy),new(w-5,dy+14)));dc.DrawRectangle(B(C(0xFF2A44)),null,new Rect(4,h-3,Math.Max(0,w*.55),3));}
                else if(hot)dc.DrawRectangle(B(t.Accent),null,new Rect(4,h-3,Math.Max(0,w-k-4),3));
                break;
            }
            case "alert":
            {   // cut-corner plate; focus and primary actions in lime
                double c=Math.Min(10,h/3);var plate=Cut(new Rect(.5,.5+dy,w-1,h-1-dy),c,0,c,0);
                dc.DrawGeometry(B(on?t.Accent:A(ThemeCatalog.Mix(C(0x0C0716),C(0x2A1650),.6*blend),.94)),new Pen(B(on||hot?t.Accent:A(C(0x9B6BFF),.7)),hot&&!on?1.5:1),plate);
                if(!on)dc.DrawRectangle(B(hot?t.Accent:A(C(0x9B6BFF),.8)),null,new Rect(w-9,3+dy,5,2));
                break;
            }
            case "slate":
            {   // a chamfered tablet key; action and focus in orange, with bracket corners when hot
                double c=Math.Min(7,h/4);var key=Cut(new Rect(.5,.5+dy,w-1,h-1-dy),c,c,c,c);
                dc.DrawGeometry(B(on?t.Accent:A(ThemeCatalog.Mix(C(0x0B2B36),C(0x174556),blend),.95)),new Pen(B(on?t.Accent:A(C(0x3FE0FF),hot?.95:.5)),1),key);
                if(hot)Corners(dc,new Rect(-2,-2+dy,w+4,h+4-dy),B(C(0xF1F8F9)),8,2);
                break;
            }
            default:
            {   // spider: a print with a red misregistration offset; chosen and primary are solid red
                var face=new Rect(3,dy,w-3,h-dy);
                dc.DrawRectangle(B(on||hot?t.Accent:C(0x7A0B1A)),null,new Rect(0,dy+(on?0:2),w-3,h-dy-(on?0:2)));
                dc.DrawRectangle(B(on?t.Accent:C(0x0F0D10)),new Pen(B(on?t.Accent:A(C(0xF4F1F1),hot?.95:.45)),1),new Rect(face.X+.5,face.Y+.5,face.Width-1,face.Height-1));
                if(hot&&!on)Corners(dc,new Rect(face.X-3,face.Y-3,face.Width+6,face.Height+6),B(C(0xF4F1F1)),6,2);
                break;
            }
        }
    }

    // ---------------------------------------------------------------- panels and cards
    private static readonly DependencyProperty WorldProperty=DependencyProperty.RegisterAttached("World",typeof(int),typeof(NativeFaces),new PropertyMetadata(-1));
    private static int worlds;
    private static readonly string[] World=["wash","home","future","zine","bazaar","noir"];
    public static void Dress(Border border,IllustratedThemeSpec s)
    {
        if(border.TemplatedParent!=null||border.BorderThickness==new Thickness(0))return; // template parts keep their control's look
        if(!Candidate(border.Background))return;
        border.SetValue(WorldProperty,worlds++%World.Length);
        border.BorderBrush=Brushes.Transparent;border.CornerRadius=new(0);
        void Paint()
        {
            double w=border.ActualWidth,h=border.ActualHeight;if(w<4||h<4)return;
            var group=new DrawingGroup();
            using(var dc=group.Open()){dc.DrawRectangle(Brushes.Transparent,null,new Rect(0,0,w,h));Panel(dc,s,w,h,(int)border.GetValue(WorldProperty));}
            var brush=new DrawingBrush(group){Stretch=Stretch.None,AlignmentX=AlignmentX.Left,AlignmentY=AlignmentY.Top,
                Viewbox=new Rect(0,0,w,h),ViewboxUnits=BrushMappingMode.Absolute,Viewport=new Rect(0,0,w,h),ViewportUnits=BrushMappingMode.Absolute};
            brush.Freeze();border.Background=brush;
        }
        border.SizeChanged+=(_,_)=>Paint();Paint();
    }
    /// <summary>Default's glass and navy surfaces; bright or semantic fills (status, artwork) are left alone.</summary>
    private static bool Candidate(Brush? background)=>background switch
    {
        SolidColorBrush {Color:var c}=>c.A>0&&(Math.Max(c.R,Math.Max(c.G,c.B))<=110||c.A<=0x40),
        GradientBrush g=>g.GradientStops.Count>0&&g.GradientStops.All(x=>Math.Max(x.Color.R,Math.Max(x.Color.G,x.Color.B))<=110||x.Color.A<=0x40),
        _=>false
    };
    private static void Panel(DrawingContext dc,IllustratedThemeSpec s,double w,double h,int world)
    {
        bool big=w>=180&&h>=90;var r=new Rect(.5,.5,w-1,h-1);
        switch(s.Surface)
        {
            case "p4":
                dc.DrawRoundedRectangle(B(A(C(0x121210),.94)),new Pen(B(C(0x2A2A27)),1),r,4,4);
                if(big)Corners(dc,new Rect(0,0,w,h),B(C(0xFFE11A)),22,3);
                break;
            case "p3":
            {
                double k=big?18:8;
                dc.DrawGeometry(B(A(C(0x06227E),.9)),null,Poly(new(0,0),new(w,0),new(w-k,h),new(0,h)));
                dc.DrawRectangle(B(C(0x8FF0FF)),null,new Rect(0,0,Math.Min(big?160:40,w*.4),2));
                if(big)dc.DrawRectangle(B(C(0xFF2A44)),null,new Rect(Math.Min(160,w*.4),0,18,2));
                break;
            }
            case "alert":
            {
                double c=big?14:8;
                dc.DrawGeometry(B(A(C(0x0C0716),.93)),new Pen(B(A(C(0x9B6BFF),.65)),1),Cut(r,c,0,c,0));
                if(big){dc.DrawRectangle(B(C(0xB4F03C)),null,new Rect(w-34,4,22,3));dc.DrawRectangle(B(A(C(0x9B6BFF),.5)),null,new Rect(c+6,h-3,Math.Min(120,w*.3),2));}
                break;
            }
            case "slate":
            {
                double c=big?12:6;
                dc.DrawGeometry(B(A(C(0x08202A),.96)),new Pen(B(A(C(0x3FE0FF),.55)),1),Cut(r,c,c,c,c));
                if(big)
                {
                    dc.DrawRectangle(B(A(C(0x3FE0FF),.8)),null,new Rect(w-96,0,60,3));
                    dc.DrawEllipse(null,new Pen(B(C(0x3FE0FF)),1.5),new Point(w-22,12),3.5,3.5);
                }
                break;
            }
            default:
                Multiverse(dc,big?World[world]:"home",w,h,r,big);
                break;
        }
    }
    /// <summary>Every big panel is a window into another world: its own palette, shape and marks (original styles).</summary>
    private static void Multiverse(DrawingContext dc,string world,double w,double h,Rect r,bool big)
    {
        Color red=C(0xFF1F3D);
        switch(world)
        {
            case "wash":
                dc.DrawRoundedRectangle(B(A(C(0x1C1626),.95)),new Pen(B(C(0xFF8FC8)),1.5),r,14,14);
                dc.PushClip(new RectangleGeometry(r,14,14));
                dc.DrawEllipse(B(A(C(0xFF8FC8),.14)),null,new Point(w*.78,h*.25),w*.22,h*.4);
                dc.DrawEllipse(B(A(C(0x5FE3D6),.12)),null,new Point(w*.92,h*.7),w*.16,h*.35);
                dc.Pop();break;
            case "zine":
                dc.DrawRectangle(B(C(0xFF2E88)),null,new Rect(5,5,w-5,h-5));
                dc.DrawRectangle(B(C(0x141414)),new Pen(B(C(0xB6FF3B)),2),new Rect(1,1,w-7,h-7));
                for(double x=w-60;x<w-14;x+=8)dc.DrawRectangle(B(A(C(0xB6FF3B),.5)),null,new Rect(x,h-22,4,4));
                break;
            case "future":
            {
                dc.DrawGeometry(B(A(C(0x08102A),.96)),new Pen(B(C(0x2F7BFF)),1.5),Cut(r,22,0,22,0));
                var line=new Pen(B(A(C(0x2F7BFF),.22)),1);double y0=h*.62;
                dc.PushClip(Cut(r,22,0,22,0));
                for(int k=-6;k<=6;k++)dc.DrawLine(line,new Point(w*.5,y0),new Point(w*.5+k*w*.14,h));
                for(double y=y0+10;y<h;y+=(y-y0)*.6+8)dc.DrawLine(line,new Point(0,y),new Point(w,y));
                dc.Pop();
                dc.DrawRectangle(B(C(0xFF7A1A)),null,new Rect(w-70,6,40,2));
                break;
            }
            case "bazaar":
            {
                var arch=new StreamGeometry();using(var c=arch.Open())
                {double rr=Math.Min(26,h/3);c.BeginFigure(new(r.Left,r.Bottom),true,true);c.LineTo(new(r.Left,r.Top+rr),true,false);
                 c.ArcTo(new(r.Left+rr,r.Top),new(rr,rr),0,false,SweepDirection.Clockwise,true,false);c.LineTo(new(r.Right-rr,r.Top),true,false);
                 c.ArcTo(new(r.Right,r.Top+rr),new(rr,rr),0,false,SweepDirection.Clockwise,true,false);c.LineTo(new(r.Right,r.Bottom),true,false);}
                arch.Freeze();
                dc.DrawGeometry(B(A(C(0x1E1109),.96)),new Pen(B(C(0xFFB000)),1.5),arch);
                for(double x=18;x<w-12;x+=14)dc.DrawEllipse(B(A(C(0x00B3A4),.7)),null,new Point(x,h-8),2,2);
                break;
            }
            case "noir":
            {
                dc.DrawRectangle(B(A(C(0x101010),.97)),new Pen(B(C(0xE8E8E8)),1),r);
                var rain=new Pen(B(A(Colors.White,.06)),1);dc.PushClip(new RectangleGeometry(r));
                for(double x=-h;x<w;x+=18)dc.DrawLine(rain,new Point(x,0),new Point(x+h*.35,h));
                dc.Pop();break;
            }
            default:
                dc.DrawGeometry(B(A(C(0x0F0D10),.95)),new Pen(B(red),big?1.5:1),Cut(r,0,big?14:6,0,big?14:6));
                if(big){dc.DrawRectangle(B(red),null,new Rect(6,6,6,6));dc.DrawRectangle(B(A(C(0xF4F1F1),.85)),null,new Rect(w-46,h-6,30,2));}
                break;
        }
    }
}

/// <summary>Content of a native-skinned key: its text takes the face's ink, so it stays readable on a solid accent.</summary>
internal sealed class NativePresenter:ContentPresenter
{
    public NativePresenter()
    {
        var text=new Style(typeof(TextBlock));
        text.Setters.Add(new Setter(TextBlock.ForegroundProperty,new Binding("Ink"){RelativeSource=new RelativeSource(RelativeSourceMode.FindAncestor,typeof(IllustratedControlFace),1)}));
        Resources.Add(typeof(TextBlock),text);
        SetBinding(TextElement.ForegroundProperty,new Binding("Ink"){RelativeSource=new RelativeSource(RelativeSourceMode.FindAncestor,typeof(IllustratedControlFace),1)});
    }
}
