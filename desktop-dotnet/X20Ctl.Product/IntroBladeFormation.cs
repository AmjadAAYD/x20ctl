using System.Windows;
using System.Windows.Media;
namespace X20Ctl.Product;

/// <summary>The X forms in three moves (owner request 17 Oct 2026: a better X animation, the controller kept): each
/// controller half glides onto the outline of its blade, held apart along the blade's own "/" line; the two blades slide
/// in and lock at the heart with a small overshoot, while light charges each one from the heart out to its needle tip;
/// the lock flashes their edges and sparks the gap. Then the white mark takes over.</summary>
internal sealed class IntroBladeFormation : FrameworkElement
{
    private Point[][] source=[], target=[];
    private Geometry[] blades=[];
    private Vector axis=new(-1,1);
    private double reach=440;
    private double time=-1;
    private static readonly Point Heart=new(390,320);
    public void Configure(Point[][] from,Point[][] to,PathGeometry mark)
    {
        source=from;target=to;
        blades=mark.Figures.Select(f=>{var g=new PathGeometry();g.Figures.Add(f.Clone());g.Freeze();return (Geometry)g;}).ToArray();
        // the "/" line the first blade's needle runs along, from the heart out to its tip
        var tip=mark.Figures[0].Segments.OfType<PolyLineSegment>().SelectMany(s=>s.Points).Append(mark.Figures[0].StartPoint).MaxBy(p=>(p-Heart).Length);
        axis=tip-Heart;reach=axis.Length;axis.Normalize();
    }
    public void SetTime(double t){time=t;InvalidateVisual();}
    private static double Win(double t,double a,double b)=>Math.Clamp((t-a)/(b-a),0,1);
    private static double Ease(double x){x=Math.Clamp(x,0,1);return 1-Math.Pow(1-x,3);}
    private const double Apart=74, LockAt=1.08;
    protected override void OnRender(DrawingContext dc)
    {
        if(time<0||time>1.65||source.Length!=2)return;
        double fade=1-Ease((time-1.22)/.4);
        // held apart, then pulled in faster and faster, a few pixels past the heart, and back: the lock
        double gap=time<LockAt?Apart*(1-Math.Pow(Win(time,.5,LockAt),2.4)):-4*Math.Sin(Math.PI*Win(time,LockAt,LockAt+.14));
        double charge=Ease(Win(time,.46,LockAt-.04))*1.2;
        double flash=time<LockAt?0:Math.Exp(-(time-LockAt)/.07)*Win(time,LockAt-.01,LockAt);
        for(int f=0;f<2;f++)
        {
            var outward=f==0?axis:-axis;
            var colour=f==0?Color.FromRgb(81,190,255):Color.FromRgb(255,163,84);
            var shift=outward*gap;
            var a=source[f];var b=target[f];
            Point At(int i)
            {
                double u=Ease((time/.62-.14*i/(a.Length-1))/.86);
                var start=new Point(Heart.X+(a[i].X-Heart.X)*1.1,Heart.Y+(a[i].Y-Heart.Y)*1.1);
                // a gentle sideways lean on the way, never an orbit or a burst
                var lean=new Vector(outward.Y,-outward.X)*(18*Math.Sin(Math.PI*u));
                return start+(b[i]+shift-start)*u+lean;
            }
            bool closed=time>=.62;
            var outline=new StreamGeometry();using(var g=outline.Open()){g.BeginFigure(At(0),false,closed);g.PolyLineTo(Enumerable.Range(1,a.Length-1).Select(At).ToArray(),true,true);}outline.Freeze();
            dc.PushOpacity(fade);
            double edge=1-Win(time,LockAt,LockAt+.18);
            if(edge>0)
            {
                dc.PushOpacity(edge);
                dc.DrawGeometry(null,new Pen(new SolidColorBrush(Color.FromArgb(28,colour.R,colour.G,colour.B)),20){LineJoin=PenLineJoin.Round},outline);
                dc.DrawGeometry(null,new Pen(new SolidColorBrush(colour),4.5){LineJoin=PenLineJoin.Round,StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round},outline);
                dc.DrawGeometry(null,new Pen(new SolidColorBrush(Color.FromArgb(200,255,255,255)),1.4){LineJoin=PenLineJoin.Round},outline);
                dc.Pop();
            }
            if(charge>0)
            {
                // the charge: the blade fills from behind the heart out to its needle, a bright front leading
                dc.PushTransform(new TranslateTransform(shift.X,shift.Y));
                var from=Heart-outward*90;var to=Heart+outward*(reach+20);
                LinearGradientBrush Mask(double behind,double ahead)=>new(new GradientStopCollection{new(Colors.White,Math.Max(0,charge-behind)),new(Colors.Transparent,charge+ahead)},from,to){MappingMode=BrushMappingMode.Absolute};
                dc.PushOpacityMask(Mask(.14,0));
                dc.DrawGeometry(new LinearGradientBrush(new GradientStopCollection{new(colour,0),new(Color.FromRgb(236,242,252),.55),new(Color.FromRgb(214,224,240),1)},from,to){MappingMode=BrushMappingMode.Absolute},null,blades[f]);
                dc.Pop();
                if(charge<1.15)
                {
                    var front=new LinearGradientBrush(new GradientStopCollection{new(Colors.Transparent,charge-.07),new(Colors.White,charge-.015),new(Colors.Transparent,charge+.01)},from,to){MappingMode=BrushMappingMode.Absolute};
                    dc.PushClip(blades[f]);dc.DrawGeometry(front,null,blades[f]);dc.Pop();
                }
                if(flash>0)dc.DrawGeometry(null,new Pen(new SolidColorBrush(Color.FromArgb((byte)(240*flash),255,255,255)),2.4){LineJoin=PenLineJoin.Miter},blades[f]);
                dc.Pop();
            }
            dc.Pop();
        }
        // the lock: one spark in the narrow gap at the heart
        if(flash>0)
        {
            double r=46+70*(1-flash);
            var spark=new RadialGradientBrush(new GradientStopCollection{new(Color.FromArgb((byte)(235*flash*fade),255,255,255),0),new(Color.FromArgb((byte)(90*flash*fade),200,220,255),.35),new(Color.FromArgb(0,200,220,255),1)});
            dc.DrawEllipse(spark,null,Heart,r,r);
            var streak=new Pen(new LinearGradientBrush(new GradientStopCollection{new(Colors.Transparent,0),new(Color.FromArgb((byte)(220*flash*fade),255,255,255),.5),new(Colors.Transparent,1)},Heart-axis*130,Heart+axis*130){MappingMode=BrushMappingMode.Absolute},2);
            dc.DrawLine(streak,Heart-axis*130,Heart+axis*130);
        }
    }
}
