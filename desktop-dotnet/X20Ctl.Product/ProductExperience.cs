using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
namespace X20Ctl.Product;

public static class ProductLinks
{
    // Preserved desktop/service.py open_support action; no guessed donation destination.
    public const string Support="https://ko-fi.com/x20ctl";
    public static void OpenSupport(string url){if(url!=Support)throw new ArgumentException("Unsupported destination.");if(ReviewSandbox.Active)return;Process.Start(new ProcessStartInfo(url){UseShellExecute=true});}
}
// ProductSettings (the Tools hub) lives in ToolsHub.cs.

/// <summary>
/// The launch cutscene, every launch, in the spirit of Steam Big Picture's opening, told X20CTL's way in blue and
/// white (owner direction 10 Oct 2026: white instead of red, the dots and glow a grey modern white). Total darkness;
/// the X20 Pro's three status LEDs blink on white one after another like eyes opening; the dark lifts and the
/// controller draws itself out from them, top centre down both sides - a blue head like a pencil, a soft orange trail
/// behind it like a marker - leaving a white line with a soft glow, its body and floor shadow surfacing; a blue-and-
/// orange glow beats around it twice like a heart; the lights go out and the line itself re-routes itself - not zooms - onto the outline of the
/// X20CTL mark, the left half of the controller gliding onto its left piece and the right half onto its right at the
/// same thickness throughout; white light fills the closed outline and
/// the edge melts away. The mark glows white on black at half the screen's height; its aura comes up - a faint
/// grey-white heart, soft corner light, fine orange dots on both sides - a light runs through it, it slowly darkens, black,
/// and the Controller Zone comes up. Timed fades are one Storyboard; only the morph is per frame. Skippable with
/// B / Esc / click; reduced motion shows the mark briefly.
/// </summary>
public sealed class IntroView:Grid
{
    private const double Total=6.25, BeatAt=1.9, ReducedFor=.4, Line=9, MorphAt=2.55, MorphFor=1.0, Samples=160;
    private static readonly double[] LedAt={.3,.52,.74};
    public IntroView Visual=>this;
    public Button Skip {get;} public bool Reduced {get;} private bool finished,startRequested,built;
    public event Action? Completed; public event Action<double>? ZoneReveal;
    private readonly Storyboard story=new();
    private readonly System.Windows.Shapes.Rectangle fade=new(){Fill=Brushes.Black,Opacity=0,IsHitTestVisible=false}, dark=new(){Fill=Brushes.Black,IsHitTestVisible=false};
    private TimeSpan? seekAt; private double? pendingSeek;
    // the aura around the finished mark: a faint grey-white heart, then soft corner light and grey dots on both sides
    private readonly Grid aura=new(){IsHitTestVisible=false,Opacity=Warm}, dotsLeft=new(){IsHitTestVisible=false,Opacity=Warm,HorizontalAlignment=HorizontalAlignment.Left,CacheMode=new BitmapCache(1)}, dotsRight=new(){IsHitTestVisible=false,Opacity=Warm,HorizontalAlignment=HorizontalAlignment.Right,CacheMode=new BitmapCache(1)};
    private readonly System.Windows.Shapes.Rectangle heart=new(){IsHitTestVisible=false,Opacity=Warm,Width=980,Height=560}, shine=new(){IsHitTestVisible=false,Opacity=0};
    private readonly System.Windows.Shapes.Path body=new(){Opacity=0,IsHitTestVisible=false}, floor=new(){Opacity=0,IsHitTestVisible=false};
    private readonly System.Windows.Shapes.Path line,glow,tracer,trail,beat,morph,filled,filledBack;
    
    private readonly Dictionary<System.Windows.Shapes.Path,double> dash=new();
    private readonly List<(System.Windows.Shapes.Path core,System.Windows.Shapes.Path bloom)> leds=new();
    private Grid stage=null!;
    private readonly Point[][] from=new Point[2][], to=new Point[2][];
    private double lastMorph=-1;
    // Owner direction 10 Oct 2026 (latest): the outline is blue and orange - a blue pencil with an orange trail and a
    // blue-and-orange heartbeat - the side dots are orange, and the X with its glow and aura is white, a touch brighter
    private static readonly Color Glow=Color.FromRgb(214,222,236), GlowHi=Color.FromRgb(236,241,250), Orange=Color.FromRgb(255,140,38), Blue=Color.FromRgb(60,132,255), BlueHi=Color.FromRgb(160,200,255);

    // the EasySMX X20 Pro, drawn clean from its own artwork (1536x1024 space, centre x=766): the raised screen
    // housing, the bumper shoulders and the long grips as one closed line, in three pieces - left grip from the
    // bottom centre up to the side, the top arch side to side, the right grip. Drawn top-down in two halves.
    private const string LeftHalf="M766,50 L616,50 C596,50 588,58 582,68 C576,78 570,84 552,85 L410,82 C372,81 342,90 320,104 C288,126 254,162 230,200 C192,262 142,372 110,446 C80,520 52,620 36,720 C22,820 40,900 100,948 C150,988 220,990 262,950 C300,912 360,830 410,758 C438,718 462,700 510,698 L766,698";
    private const string LeftGrip="M766,698 L510,698 C462,700 438,718 410,758 C360,830 300,912 262,950 C220,990 150,988 100,948 C40,900 22,820 36,720 C52,620 80,520 110,446";
    private const string Arch="M110,446 C142,372 192,262 230,200 C254,162 288,126 320,104 C342,90 372,81 410,82 L552,85 C570,84 576,78 582,68 C588,58 596,50 616,50 L766,50 L916,50 C936,50 944,58 950,68 C956,78 962,84 980,85 L1122,82 C1160,81 1190,90 1212,104 C1244,126 1278,162 1302,200 C1340,262 1390,372 1422,446";
    private static string RightHalf=>Mirror(LeftHalf); private static string RightGrip=>Mirror(LeftGrip);
    private static string Mirror(string data)=>System.Text.RegularExpressions.Regex.Replace(data,@"(-?\d+(?:\.\d+)?),(-?\d+(?:\.\d+)?)",m=>$"{(2*PadMidX-double.Parse(m.Groups[1].Value,System.Globalization.CultureInfo.InvariantCulture)).ToString(System.Globalization.CultureInfo.InvariantCulture)},{m.Groups[2].Value}");
    // the three status LEDs are its eyes
    private static readonly Point[] Leds={new(728,295),new(766,295),new(804,295)};
    private const double PadWidth=430, PadTop=50, PadBottom=990, PadLeft=22, PadMidX=766, LedR=13;

    public IntroView(bool reduced)
    {
        Reduced=reduced;PrepareSound(); // open the sound now, so it is ready the moment the LEDs blink
        Children.Add(new System.Windows.Shapes.Rectangle{Fill=new LinearGradientBrush(Color.FromRgb(9,10,14),Color.FromRgb(3,3,5),90)}); // black, as the X20CTL aura wants
        // the reveal: a faint grey-white heart behind the mark, soft light from the corners, and a fine orange dot grid on each
        // side fading out toward the middle and the edges
        heart.Fill=new RadialGradientBrush(new GradientStopCollection{new(Color.FromArgb(56,GlowHi.R,GlowHi.G,GlowHi.B),0),new(Color.FromArgb(18,GlowHi.R,GlowHi.G,GlowHi.B),.5),new(Color.FromArgb(0,Glow.R,Glow.G,Glow.B),1)});heart.Width=1400;heart.Height=900;
        RadialGradientBrush Corner(Color colour,double x,double y,byte strength)=>new(new GradientStopCollection{new(Color.FromArgb(strength,colour.R,colour.G,colour.B),0),new(Color.FromArgb((byte)(strength/3),colour.R,colour.G,colour.B),.45),new(Color.FromArgb(0,colour.R,colour.G,colour.B),1)}){Center=new(x,y),GradientOrigin=new(x,y),RadiusX=.5,RadiusY=.62};
        foreach(var corner in new[]{Corner(GlowHi,0,0,76),Corner(GlowHi,0,1,58),Corner(GlowHi,1,1,76),Corner(GlowHi,1,0,40)})aura.Children.Add(new System.Windows.Shapes.Rectangle{Fill=corner});
        Grid Dots(Grid side,Color colour,bool left)
        {
            var tile=new DrawingGroup();using(var dc=tile.Open()){dc.DrawRectangle(Brushes.Transparent,null,new(0,0,15,15));dc.DrawEllipse(new SolidColorBrush(colour),null,new(7.5,7.5),1.05,1.05);}tile.Freeze();
            side.Children.Add(new System.Windows.Shapes.Rectangle{Fill=new DrawingBrush(tile){TileMode=TileMode.Tile,Viewport=new(0,0,15,15),ViewportUnits=BrushMappingMode.Absolute},
                OpacityMask=new RadialGradientBrush(new GradientStopCollection{new(Color.FromArgb(130,255,255,255),0),new(Color.FromArgb(60,255,255,255),.55),new(Color.FromArgb(0,255,255,255),1)}){Center=new(left?.42:.58,.42),GradientOrigin=new(left?.42:.58,.42),RadiusX=.62,RadiusY=.6}});
            return side;
        }
        Children.Add(Dots(dotsLeft,Orange,true));Children.Add(Dots(dotsRight,Orange,false));Children.Add(aura);Children.Add(heart);
        SizeChanged+=(_,_)=>{dotsLeft.Width=dotsRight.Width=ActualWidth/2;};
        Children.Add(dark); // the opening darkness; it lifts as the body appears

        // the controller and the X share one stage, both centred on it, so the line reshapes in place
        double s=PadWidth/(2*(PadMidX-PadLeft)), stageW=StageW, stageH=StageH; var mid=new Point(stageW/2,stageH/2);
        Transform Pad()=>new TransformGroup{Children={new TranslateTransform(-PadMidX,-(PadTop+PadBottom)/2),new ScaleTransform(s,s),new TranslateTransform(mid.X,mid.Y)}};
        var pad=Pad().Value;
        Geometry G(string data,Transform t){var g=Geometry.Parse(data).Clone();g.Transform=t;return g;}
        double Len(Geometry g)=>g.GetFlattenedPathGeometry(.1,ToleranceType.Absolute).Figures.Sum(Length);
        var markPx=Mark(mid);
        // the transition: the controller's own line re-routes itself onto the outline of the mark. Each half of the controller
        // (an open line, top centre to bottom centre) glides point for point onto the outline of one piece of the X, at the
        // same thickness all the way, so it never thickens into a blob; its two ends meet as the outline closes
        for(int i=0;i<2;i++)
        {
            from[i]=ResampleOpen(G(i==0?LeftHalf:RightHalf,Pad()).GetFlattenedPathGeometry(.2,ToleranceType.Absolute).Figures[0],LinePoints);
            to[i]=AlignOpen(from[i],Orient(Resample(markPx.Figures[i],LinePoints)));
        }


        // both halves are figures of one geometry: the dash runs down each from the top centre at once, and where they
        // meet (top and bottom centre) the caps and glow are painted once, so there is no seam or bright spot
        var left=G(LeftHalf,Pad());double halfLen=Len(left);var halves=new PathGeometry();halves.AddGeometry(left);halves.AddGeometry(G(RightHalf,Pad()));halves.Freeze();
        // the body: the closed silhouette filled dark, with a floor shadow, so the line reads as a real object
        var leftPts=Resample(G(LeftHalf,Pad()).GetFlattenedPathGeometry(.2,ToleranceType.Absolute).Figures[0]);var rightPts=Resample(G(RightHalf,Pad()).GetFlattenedPathGeometry(.2,ToleranceType.Absolute).Figures[0]);
        var silhouette=new StreamGeometry();using(var c=silhouette.Open()){c.BeginFigure(leftPts[0],true,true);c.PolyLineTo(leftPts.Skip(1).Concat(rightPts.Reverse()).ToList(),true,true);}silhouette.Freeze();
        body.Data=silhouette;body.Fill=new LinearGradientBrush(new GradientStopCollection{new(Color.FromRgb(40,48,66),0),new(Color.FromRgb(20,25,36),.55),new(Color.FromRgb(10,13,20),1)},90);
        var sb=silhouette.Bounds;floor.Data=new EllipseGeometry(new Point(sb.X+sb.Width/2,sb.Bottom+8),sb.Width*.44,15);
        floor.Fill=new RadialGradientBrush(Color.FromArgb(180,0,0,0),Color.FromArgb(0,0,0,0));

        // a line that draws itself: dash lengths are in multiples of the stroke width, so each path keeps its own unit count
        System.Windows.Shapes.Path Draw(Geometry g,Brush b,double w,double lengthPx)
        {
            double u=lengthPx/w+1;var p=new System.Windows.Shapes.Path{Data=g,Stroke=b,StrokeThickness=w,StrokeLineJoin=PenLineJoin.Round,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,StrokeDashArray=new DoubleCollection{u,u},StrokeDashOffset=u,Opacity=0};
            dash[p]=u;return p;
        }
        // a tracer: a short bright head that runs along a path just ahead of the line
        System.Windows.Shapes.Path Trace(Geometry g,Brush b,double w,double lengthPx)
        {
            double u=lengthPx/w;var p=new System.Windows.Shapes.Path{Data=g,Stroke=b,StrokeThickness=w,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,StrokeDashArray=new DoubleCollection{u*.08,u*2},StrokeDashOffset=u*.08,Opacity=0};
            dash[p]=u;return p;
        }
        // the line: white shading to silver, a soft drop shadow under it and a faint glow around it
        Brush lineBrush=new LinearGradientBrush(new GradientStopCollection{new(Colors.White,0),new(Color.FromRgb(238,242,250),.5),new(Color.FromRgb(200,210,228),1)},90);
        line=Draw(halves,lineBrush,Line,halfLen);line.Effect=new System.Windows.Media.Effects.DropShadowEffect{Color=Colors.Black,Direction=270,ShadowDepth=5,BlurRadius=14,Opacity=.6};
        var glowBrush=new SolidColorBrush(Color.FromArgb(70,200,215,255));
        glow=Draw(halves,glowBrush,Line+12,halfLen);glow.Effect=new System.Windows.Media.Effects.BlurEffect{Radius=14};
        // the orange trail: a soft blurred stretch of line that follows just behind the blue head, like a marker behind a
        // pencil; once it has passed, the controller is left white
        double tw=Line+10,tu=halfLen/tw;
        trail=new System.Windows.Shapes.Path{Data=halves,Stroke=new SolidColorBrush(Color.FromArgb(190,Orange.R,Orange.G,Orange.B)),StrokeThickness=tw,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,StrokeDashCap=PenLineCap.Round,
            StrokeDashArray=new DoubleCollection{tu*.3,tu*3},StrokeDashOffset=tu*.3,Opacity=0,Effect=new System.Windows.Media.Effects.BlurEffect{Radius=12}};dash[trail]=tu;
        // the heartbeat: once the controller is whole, a glow of blue (left) and orange (right) pulses around its outline twice
        beat=new System.Windows.Shapes.Path{Data=halves,StrokeThickness=Line+16,StrokeLineJoin=PenLineJoin.Round,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,Opacity=0,
            Stroke=new LinearGradientBrush(new GradientStopCollection{new(Color.FromArgb(235,Blue.R,Blue.G,Blue.B),.15),new(Color.FromArgb(235,Orange.R,Orange.G,Orange.B),.85)},new Point(0,.5),new Point(1,.5)),
            Effect=new System.Windows.Media.Effects.BlurEffect{Radius=18},RenderTransformOrigin=new(.5,.5),RenderTransform=new ScaleTransform(1,1)};
        var tracerBrush=new SolidColorBrush(Blue);
        tracer=Trace(halves,tracerBrush,Line+1,halfLen);
        tracer.Effect=new System.Windows.Media.Effects.DropShadowEffect{Color=Blue,BlurRadius=12,ShadowDepth=0,Opacity=1};
        

        // the eyes: a hot core and a soft bloom per LED, the bloom scaled about the LED itself
        foreach(var at in Leds)
        {
            var c=pad.Transform(at);double r=LedR*s;
            var core=new System.Windows.Shapes.Path{Data=new EllipseGeometry(c,r,r),Fill=new RadialGradientBrush(Colors.White,Glow),Opacity=0};
            var bloom=new System.Windows.Shapes.Path{Data=new EllipseGeometry(c,r*7,r*7),Opacity=0,RenderTransform=new ScaleTransform(.3,.3,c.X,c.Y),
                Fill=new RadialGradientBrush(new GradientStopCollection{new(Color.FromArgb(200,Glow.R,Glow.G,Glow.B),0),new(Color.FromArgb(70,Glow.R,Glow.G,Glow.B),.3),new(Color.FromArgb(0,Glow.R,Glow.G,Glow.B),1)})};
            leds.Add((core,bloom));
        }
        // the morph is the line itself: same brush, thickness, joins and shadow, so the hand-over is seamless
        morph=new System.Windows.Shapes.Path{Stroke=lineBrush,StrokeThickness=Line,StrokeLineJoin=PenLineJoin.Round,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,Opacity=0,Effect=new System.Windows.Media.Effects.DropShadowEffect{Color=Colors.Black,Direction=270,ShadowDepth=5,BlurRadius=14,Opacity=.6}};
        var forward=new PathGeometry();forward.Figures.Add(markPx.Figures[0].Clone());forward.Freeze();
        var back=new PathGeometry();back.Figures.Add(markPx.Figures[1].Clone());back.Freeze();
        // the mark in white with a soft grey-white glow, the same mark as the app icon
        var markFill=new LinearGradientBrush(Color.FromRgb(255,255,255),Color.FromRgb(218,224,236),90);
        filled=new System.Windows.Shapes.Path{Data=forward,Fill=markFill,Opacity=Warm,Effect=new System.Windows.Media.Effects.DropShadowEffect{Color=GlowHi,BlurRadius=40,ShadowDepth=0,Opacity=.85}};
        filledBack=new System.Windows.Shapes.Path{Data=back,Fill=markFill,Opacity=Warm,Effect=new System.Windows.Media.Effects.DropShadowEffect{Color=GlowHi,BlurRadius=40,ShadowDepth=0,Opacity=.85}};
        // the light lives inside the X: a band masked by the mark itself, so nothing floats over the backdrop
        shine.Fill=new LinearGradientBrush(new GradientStopCollection{new(Color.FromArgb(0,255,255,255),.38),new(Color.FromArgb(210,255,255,255),.5),new(Color.FromArgb(0,255,255,255),.62)},new Point(0,0),new Point(1,.35)){RelativeTransform=new TranslateTransform(-1,0)};
        shine.OpacityMask=new DrawingBrush(new GeometryDrawing(Brushes.White,null,markPx)){Stretch=Stretch.None,AlignmentX=AlignmentX.Left,AlignmentY=AlignmentY.Top,ViewboxUnits=BrushMappingMode.Absolute,Viewbox=new(0,0,stageW,stageH),ViewportUnits=BrushMappingMode.Absolute,Viewport=new(0,0,stageW,stageH)};
        stage=new Grid{Width=stageW,Height=stageH,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,RenderTransformOrigin=new(.5,.5),RenderTransform=new ScaleTransform(1,1)};
        // the stage is designed for a 1200-pixel-tall screen and scales with the window, so the mark stays about half its height
        SizeChanged+=(_,_)=>{double z=Math.Clamp(ActualHeight/1200,.55,1.8);((ScaleTransform)stage.RenderTransform).ScaleX=((ScaleTransform)stage.RenderTransform).ScaleY=z;};
        foreach(var e in new UIElement[]{floor,body,beat,glow,trail,line,tracer})stage.Children.Add(e);
        foreach(var (core,bloom) in leds){stage.Children.Add(bloom);stage.Children.Add(core);}
        foreach(var e in new UIElement[]{morph,filled,filledBack,shine})stage.Children.Add(e);
        Children.Add(stage);Children.Add(fade);
        Skip=new Button{Content="Skip · Esc / B",HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Bottom,Margin=new(0,0,36,36),Style=DS.Style("DS.Secondary"),Opacity=.8};Skip.Click+=(_,_)=>Finish();Children.Add(Skip);
        PreviewMouseDown+=(_,e)=>{if(e.ChangedButton==MouseButton.Left&&!Skip.IsMouseOver)Finish();};
        Loaded+=(_,_)=>{Build();if(startRequested)Begin();if(pendingSeek is double p)Seek(p);};
        Unloaded+=(_,_)=>CompositionTarget.Rendering-=Tick;
    }

    /// <summary>The X20CTL mark (owner's chosen X, 10 Oct 2026), built exactly in screen pixels so its edges stay crisp:
    /// two identical hooked pieces, one turned half a turn about the centre. Each is half of a hollow band running "\"
    /// at 45 degrees with its end cut level, joined to half of a long blade running "/" that tapers to a needle tip; the
    /// two pieces meet across a narrow gap at the heart. Figures: left piece, right piece. Traced from the owner's
    /// reference and squared up (parallel edges, exact half-turn symmetry).</summary>
    private static PathGeometry Mark(Point c)
    {
        (double x,double y)[] piece={(-53,-127),(-187,-127),(-64,-4),(-314,258),(-20,-2),(-116,-98),(-66,-98),(8,-24),(29,-45)};
        PathFigure Poly(IEnumerable<Point> p){var a=p.ToArray();return new(a[0],new[]{new PolyLineSegment(a.Skip(1),true)},true){IsFilled=true};}
        var g=new PathGeometry();
        g.Figures.Add(Poly(piece.Select(q=>new Point(c.X+q.x*MarkScale,c.Y+q.y*MarkScale))));
        g.Figures.Add(Poly(piece.Select(q=>new Point(c.X-q.x*MarkScale,c.Y-q.y*MarkScale))));
        g.Freeze();return g;
    }
    private const int LinePoints=260;
    // the glowing mark and its aura are drawn (invisibly) from the first black frame, so their first real appearance
    // costs nothing: an effect's first render is the slowest frame it ever has
    private const double Warm=.003;
    private const double MarkScale=1.12, StageW=780, StageH=640;

    private void Build()
    {
        if(built)return;built=true;
        var ease=new CubicEase{EasingMode=EasingMode.EaseInOut};var outE=new CubicEase{EasingMode=EasingMode.EaseOut};var inE=new CubicEase{EasingMode=EasingMode.EaseIn};
        void A(DependencyObject target,string path,double a,double b,double at,double dur,IEasingFunction? e=null){var x=new DoubleAnimation(a,b,TimeSpan.FromSeconds(dur)){BeginTime=TimeSpan.FromSeconds(at),EasingFunction=e,FillBehavior=FillBehavior.HoldEnd};Storyboard.SetTarget(x,target);Storyboard.SetTargetProperty(x,new PropertyPath(path));story.Children.Add(x);}
        void DrawOn(System.Windows.Shapes.Path p,double at,double dur,IEasingFunction e){A(p,"Opacity",0,1,at,.01);A(p,"StrokeDashOffset",dash[p],0,at,dur,e);}
        double tu0=dash[trail];
        void TraceOn(System.Windows.Shapes.Path p,double at,double dur,IEasingFunction e){double u=dash[p];A(p,"StrokeDashOffset",u*.08,u*.08-u,at,dur,e);A(p,"Opacity",0,1,at+.02,.1);A(p,"Opacity",1,0,at+dur-.15,.12);}
        if(Reduced)
        {
            foreach(var p in new[]{line,glow,tracer,trail,beat,morph})p.Visibility=Visibility.Collapsed;
            foreach(var (core,bloom) in leds){core.Visibility=bloom.Visibility=Visibility.Collapsed;}
            // reduced motion: the X, held for a moment, while the zone comes up underneath it
            A(dark,"Opacity",1,0,0,.1);A(filled,"Opacity",0,1,0,.1);A(filledBack,"Opacity",0,1,0,.1);foreach(var p in new UIElement[]{heart,aura,dotsLeft,dotsRight})A(p,"Opacity",0,1,0,.1);story.Duration=TimeSpan.FromSeconds(ReducedFor);
        }
        else
        {
            // 1. darkness, then the three LEDs blink on one after another - eyes opening
            var pop=new BackEase{EasingMode=EasingMode.EaseOut,Amplitude=.6};
            for(int i=0;i<leds.Count;i++)
            {
                var (core,bloom)=leds[i];double t=LedAt[i];
                A(core,"Opacity",0,1,t,.06);
                A(bloom,"Opacity",0,1,t,.08);A(bloom,"Opacity",1,.5,t+.12,.4,outE);
                A(bloom,"(UIElement.RenderTransform).(ScaleTransform.ScaleX)",.3,1,t,.35,pop);A(bloom,"(UIElement.RenderTransform).(ScaleTransform.ScaleY)",.3,1,t,.35,pop);
            }
            // 2. the dark lifts and the body draws itself out from the eyes, top centre down both sides (0.95-1.7)
            A(dark,"Opacity",1,0,.95,.8,ease);A(body,"Opacity",0,1,1.15,.6,outE);A(floor,"Opacity",0,1,1.25,.6,outE);
            DrawOn(line,.95,.75,ease);TraceOn(tracer,.95,.75,ease);
            // the orange marker runs just behind the blue pencil, then lifts off at the bottom; the soft white glow settles after it
            A(trail,"StrokeDashOffset",tu0*.3+tu0*.06,tu0*.3-tu0,1.0,.78,ease);A(trail,"Opacity",0,1,1.0,.12);A(trail,"Opacity",1,0,1.62,.35,outE);
            DrawOn(glow,1.2,.75,ease);
            // 3. the controller is whole
            // the heartbeat, lub-dub: a sudden blue-and-orange glow around the whole controller, swelling a touch with each beat
            const string bx="(UIElement.RenderTransform).(ScaleTransform.ScaleX)",by="(UIElement.RenderTransform).(ScaleTransform.ScaleY)";
            foreach(var (at,peak,swell,rest) in new[]{(BeatAt,1.0,1.035,.14),(BeatAt+.2,.8,1.025,.5)})
            {
                A(beat,"Opacity",at==BeatAt?0:.18,peak,at,.06);A(beat,"Opacity",peak,at==BeatAt?.18:0,at+.06,rest,outE);
                foreach(var axis in new[]{bx,by}){A(beat,axis,1,swell,at,.06);A(beat,axis,swell,1,at+.06,rest,outE);}
            }
            // 4. the eyes go out, then the line itself reshapes into the X (per frame from MorphAt); the drawn strokes hand over
            
            foreach(var (core,bloom) in leds){A(core,"Opacity",1,0,MorphAt-.2,.3,inE);A(bloom,"Opacity",.5,0,MorphAt-.2,.3,inE);}
            A(glow,"Opacity",1,0,MorphAt-.1,.3,inE);
            A(line,"Opacity",1,0,MorphAt,.02);
            A(body,"Opacity",1,0,MorphAt-.05,.35,inE);A(floor,"Opacity",1,0,MorphAt,.4,inE);
            A(morph,"Opacity",0,1,MorphAt-.02,.02);
            // 5. the finished X glows
            A(filled,"Opacity",0,1,MorphAt+MorphFor-.05,.4,outE);A(filledBack,"Opacity",0,1,MorphAt+MorphFor-.05,.4,outE);A(morph,"Opacity",1,0,MorphAt+MorphFor+.2,.4,ease);
            // 6. the aura comes up: the faint grey-white heart first, then the corner light, then the dots
            double lit=MorphAt+MorphFor-.05;
            A(heart,"Opacity",0,1,lit,.6,outE);A(aura,"Opacity",0,1,lit+.2,.9,outE);A(dotsLeft,"Opacity",0,1,lit+.35,1.0,outE);A(dotsRight,"Opacity",0,1,lit+.35,1.0,outE);
            A(shine,"Opacity",0,1,lit+.2,.05);A(shine,"(Shape.Fill).(Brush.RelativeTransform).(TranslateTransform.X)",-.7,.7,lit+.25,.6,ease);A(shine,"Opacity",1,0,lit+.85,.1);
            // 7. it holds, then the X slowly darkens and the aura dims with it
            double dim=lit+1.15;A(filled,"Opacity",1,.12,dim,.8,ease);A(filledBack,"Opacity",1,.12,dim,.8,ease);foreach(var p in new UIElement[]{heart,aura,dotsLeft,dotsRight})A(p,"Opacity",1,.35,dim,.8,ease);
            // 8. black, then the zone comes up from Frame
            A(fade,"Opacity",0,1,Total-.85,.35,inE);
            story.Duration=TimeSpan.FromSeconds(Total);
        }
        story.Completed+=(_,_)=>Finish();
    }

    // ---- the morph: each controller piece and its X piece resampled to the same number of points, then blended ----
    private static Rect Bounds(PathFigure f){var g=new PathGeometry{Figures={f.Clone()}};return g.Bounds;}
    private static Point[] Resample(PathFigure f,int count=(int)Samples)
    {
        var pts=new List<Point>{f.StartPoint};foreach(var s in f.Segments){if(s is PolyLineSegment pl)pts.AddRange(pl.Points);else if(s is LineSegment l)pts.Add(l.Point);}
        pts.Add(f.StartPoint);double total=0;for(int i=1;i<pts.Count;i++)total+=(pts[i]-pts[i-1]).Length;
        var o=new Point[count];int j=1;double walked=0;
        for(int n=0;n<o.Length;n++)
        {
            double want=total*n/o.Length;
            while(j<pts.Count-1&&walked+(pts[j]-pts[j-1]).Length<want){walked+=(pts[j]-pts[j-1]).Length;j++;}
            double seg=(pts[j]-pts[j-1]).Length,u=seg>0?(want-walked)/seg:0;o[n]=pts[j-1]+(pts[j]-pts[j-1])*Math.Clamp(u,0,1);
        }
        return o;
    }
    /// <summary>An open line resampled to evenly spaced points, first and last on its own ends.</summary>
    private static Point[] ResampleOpen(PathFigure f,int count)
    {
        var pts=new List<Point>{f.StartPoint};foreach(var s in f.Segments){if(s is PolyLineSegment pl)pts.AddRange(pl.Points);else if(s is LineSegment l)pts.Add(l.Point);}
        double total=0;for(int i=1;i<pts.Count;i++)total+=(pts[i]-pts[i-1]).Length;
        var o=new Point[count];int j=1;double walked=0;
        for(int n=0;n<count;n++)
        {
            double want=total*n/(count-1);
            while(j<pts.Count-1&&walked+(pts[j]-pts[j-1]).Length<want){walked+=(pts[j]-pts[j-1]).Length;j++;}
            double seg=(pts[j]-pts[j-1]).Length,u=seg>0?(want-walked)/seg:0;o[n]=pts[j-1]+(pts[j]-pts[j-1])*Math.Clamp(u,0,1);
        }
        return o;
    }
    /// <summary>Choose where on the closed target outline the open line's first point lands (and which way round it
    /// runs), so every point travels the shortest way, compared around each shape's centre at a uniform scale.</summary>
    private static Point[] AlignOpen(Point[] line,Point[] ring)
    {
        Point[] Norm(Point[] p){double x0=p.Min(q=>q.X),x1=p.Max(q=>q.X),y0=p.Min(q=>q.Y),y1=p.Max(q=>q.Y),size=Math.Max(Math.Max(x1-x0,y1-y0),1e-6);double cx=p.Average(q=>q.X),cy=p.Average(q=>q.Y);return p.Select(q=>new Point((q.X-cx)/size,(q.Y-cy)/size)).ToArray();}
        var a=Norm(line);var b=Norm(ring);int n=a.Length;double best=double.MaxValue;int bestOff=0;bool bestRev=false;
        foreach(bool rev in new[]{false,true})for(int off=0;off<n;off++){double sum=0;for(int i=0;i<n&&sum<best;i++){int k=rev?(off-i+n*2)%n:(off+i)%n;sum+=(a[i]-b[k]).LengthSquared;}if(sum<best){best=sum;bestOff=off;bestRev=rev;}}
        var o=new Point[n];for(int i=0;i<n;i++)o[i]=ring[bestRev?(bestOff-i+n*2)%n:(bestOff+i)%n];return o;
    }
    /// <summary>Every ring winds the same way: the pieces are filled together (non-zero), so a piece wound the other
    /// way would cancel out where two of them overlap and leave a dark spot.</summary>
    private static Point[] Orient(Point[] p){double area=0;for(int i=0;i<p.Length;i++){var a=p[i];var b=p[(i+1)%p.Length];area+=a.X*b.Y-b.X*a.Y;}return area<0?p.Reverse().ToArray():p;}
    /// <summary>Rotate the source ring (both rings already wound the same way) so each point travels to its closest
    /// counterpart, compared around each shape's centre at a uniform scale, which keeps the reshape from twisting.</summary>
    private static Point[] Align(Point[] src,Point[] dst)
    {
        src=Orient(src);dst=Orient(dst);
        Point[] Norm(Point[] p){double x0=p.Min(q=>q.X),x1=p.Max(q=>q.X),y0=p.Min(q=>q.Y),y1=p.Max(q=>q.Y),size=Math.Max(Math.Max(x1-x0,y1-y0),1e-6);double cx=p.Average(q=>q.X),cy=p.Average(q=>q.Y);return p.Select(q=>new Point((q.X-cx)/size,(q.Y-cy)/size)).ToArray();}
        var a=Norm(src);var b=Norm(dst);int n=a.Length;double best=double.MaxValue;int bestOff=0;bool bestRev=false;
        foreach(bool rev in new[]{false})for(int off=0;off<n;off++){double sum=0;for(int i=0;i<n&&sum<best;i++){int s=rev?(off-i+n*2)%n:(off+i)%n;sum+=(a[s]-b[i]).LengthSquared;}if(sum<best){best=sum;bestOff=off;bestRev=rev;}}
        var o=new Point[n];for(int i=0;i<n;i++)o[i]=src[bestRev?(bestOff-i+n*2)%n:(bestOff+i)%n];return o;
    }
    private void Morph(double t)
    {
        if(Reduced)return;double key=Math.Round(t,3);if(key==lastMorph)return;lastMorph=key;
        // the two halves of the line are open strokes that close as they land on the outline
        StreamGeometry Shape(params int[] figures)
        {
            var g=new StreamGeometry{FillRule=FillRule.Nonzero};
            using(var c=g.Open())foreach(int f in figures)
            {
                double u=Math.Clamp((t-MorphAt)/(MorphFor-.1),0,1);u=u<.5?4*u*u*u:1-Math.Pow(-2*u+2,3)/2;
                var a=from[f];var b=to[f];c.BeginFigure(a[0]+(b[0]-a[0])*u,false,u>=.999);
                var r=new Point[a.Length-1];for(int i=1;i<a.Length;i++)r[i-1]=a[i]+(b[i]-a[i])*u;c.PolyLineTo(r,true,true);
            }
            g.Freeze();return g;
        }
        morph.Data=Shape(0,1);
    }
    private static double Length(PathFigure f)
    {
        double sum=0;var p=f.StartPoint;foreach(var s in f.Segments){if(s is PolyLineSegment pl)foreach(var q in pl.Points){sum+=(q-p).Length;p=q;}else if(s is LineSegment l){sum+=(l.Point-p).Length;p=l.Point;}}if(f.IsClosed)sum+=(f.StartPoint-p).Length;return sum;
    }

    private double Duration=>Reduced?ReducedFor:Total;
    private double Elapsed=>seekAt?.TotalSeconds??(DateTime.UtcNow-beganAt).TotalSeconds;
    private DateTime beganAt=DateTime.UtcNow;
    public void Start(){startRequested=true;PrepareSound();if(IsLoaded)Begin();}
    /// <summary>The intro's own sound (tools/build_intro_sound.py: LED pings, the "fwaaah" swell, the heartbeat, a riser,
    /// then "ba-pa!" as the X lands). Off for reduced motion and for development reviews.</summary>
    public static bool SoundEnabled {get;set;}=true;
    private System.Windows.Media.MediaPlayer? sound;
    private bool soundReady,soundWanted;
    /// <summary>Opens the sound before the intro starts (opening it on demand made the LED pings lag their lights).</summary>
    private void PrepareSound()
    {
        if(sound!=null||!SoundEnabled||Reduced||ReviewSandbox.Active)return;
        string path=System.IO.Path.Combine(AppContext.BaseDirectory,"Brand","intro.wav");if(!System.IO.File.Exists(path))return;
        try{sound=new System.Windows.Media.MediaPlayer{Volume=.85};sound.MediaOpened+=(_,_)=>{soundReady=true;if(soundWanted)PlayInSync();};sound.Open(new Uri(path));}
        catch(Exception error) when(error is InvalidOperationException or System.IO.IOException or UriFormatException){sound=null;}
    }
    private void PlaySound(){PrepareSound();soundWanted=true;if(soundReady)PlayInSync();}
    /// <summary>Start the sound at the intro's current moment, so a late open can never put the audio behind the picture.</summary>
    private void PlayInSync()
    {
        if(sound==null||finished)return;
        var elapsed=DateTime.UtcNow-beganAt;if(elapsed<TimeSpan.Zero||elapsed.TotalSeconds>Total)elapsed=TimeSpan.Zero;
        sound.Position=elapsed;sound.Play();
    }
    private void StopSound()
    {
        var playing=sound;sound=null;if(playing==null)return;
        var fade=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(30)};int step=0;double from=playing.Volume;
        fade.Tick+=(_,_)=>{step++;playing.Volume=from*Math.Max(0,1-step/10.0);if(step>=10){fade.Stop();playing.Stop();playing.Close();}};fade.Start();
    }
    private void Begin(){if(finished)return;Build();beganAt=DateTime.UtcNow;story.Begin(this,true);PlaySound();CompositionTarget.Rendering-=Tick;CompositionTarget.Rendering+=Tick;}
    public void Finish(){if(finished)return;finished=true;CompositionTarget.Rendering-=Tick;story.Stop(this);if((DateTime.UtcNow-beganAt).TotalSeconds<Total-.3)StopSound();else sound=null;ZoneReveal?.Invoke(1);Completed?.Invoke();}
    /// <summary>Review hook: freeze the cutscene at time t (seconds).</summary>
    public void Seek(double t){if(!IsLoaded){pendingSeek=t;return;}Build();CompositionTarget.Rendering-=Tick;seekAt=TimeSpan.FromSeconds(t);story.Begin(this,true);story.Seek(this,TimeSpan.FromSeconds(t),TimeSeekOrigin.BeginTime);story.Pause(this);Frame(t);}
    private void Tick(object? s,EventArgs e)=>Frame(Elapsed);
    private void Frame(double t)
    {
        Morph(t);
        double start=Duration-.5,amount=Math.Clamp((t-start)/.5,0,1);amount=1-Math.Pow(1-amount,3);
        Opacity=1-amount;ZoneReveal?.Invoke(amount);Skip.Opacity=.8*(1-amount);
    }
}
