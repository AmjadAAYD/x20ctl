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
/// Original X20CTL controller and X geometry, with travelling contour strokes, a short anticipation,
/// then the two blades slide in and lock into the X (IntroBladeFormation), a quiet logo hold and the Zone handoff.
/// A soft synthesised sound follows the beats: the eyes, the outline, the lock and its "dug dug", the slogan, the landing.
/// Skip, mute and reduced motion are retained.
/// </summary>
public sealed class IntroView:Grid
{
    private const double Total=7.1, SloganAt=5.1, FlyAt=6.0, FlyFor=.85, ReducedFor=.4, Line=9, MorphAt=2.95, MorphFor=1.15, Samples=160, XBeatAt=4.23;
    private static readonly double[] LedAt={.18,.36,.54};
    public IntroView Visual=>this;
    public Button Skip {get;} public Button Mute {get;}
    /// <summary>The player muted the intro (from its button, M or Y). The host remembers it.</summary>
    public bool Muted {get;private set;} public event Action<bool>? MutedChanged; public bool Reduced {get;} private bool finished,startRequested,built;
    public event Action? Completed; public event Action<double>? ZoneReveal;
    private readonly Storyboard story=new();
    private readonly System.Windows.Shapes.Rectangle fade=new(){Fill=Brushes.Black,Opacity=0,IsHitTestVisible=false}, dark=new(){Fill=Brushes.Black,IsHitTestVisible=false};
    private TimeSpan? seekAt; private double? pendingSeek;
    // the aura around the finished mark: a faint grey-white heart, then soft corner light and grey dots on both sides
    private readonly IntroMarkAura markAura = new();
    private readonly IntroStabilizationBackground stabilizationBackground = new();
    private readonly Grid aura=new(){IsHitTestVisible=false,Opacity=Warm}, dotsLeft=new(){IsHitTestVisible=false,Opacity=Warm,HorizontalAlignment=HorizontalAlignment.Left,CacheMode=new BitmapCache(1)}, dotsRight=new(){IsHitTestVisible=false,Opacity=Warm,HorizontalAlignment=HorizontalAlignment.Right,CacheMode=new BitmapCache(1)};
    private readonly System.Windows.Shapes.Rectangle heart=new(){IsHitTestVisible=false,Opacity=Warm,Width=980,Height=560}, shine=new(){IsHitTestVisible=false,Opacity=0};
    private readonly System.Windows.Shapes.Path body=new(){Opacity=0,IsHitTestVisible=false}, floor=new(){Opacity=0,IsHitTestVisible=false};
    private readonly System.Windows.Shapes.Path line,glow,tracer,trail,beat,morph,filled,filledBack;
    
    private readonly Dictionary<System.Windows.Shapes.Path,double> dash=new();
    private readonly List<(System.Windows.Shapes.Path core,System.Windows.Shapes.Path bloom)> leds=new();
    private Grid stage=null!;
    private readonly Grid controllerGroup=new(){IsHitTestVisible=false};
    private readonly IntroBladeFormation formation=new(){IsHitTestVisible=false};
    private readonly IntroContourFlow contours=new(){IsHitTestVisible=false};
    private readonly System.Windows.Shapes.Path morphGhost=new(){Stroke=new SolidColorBrush(Color.FromArgb(70,200,215,245)),StrokeThickness=8,StrokeLineJoin=PenLineJoin.Round,Opacity=0,IsHitTestVisible=false};
    private readonly Point[][] from=new Point[2][], to=new Point[2][];
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

    private readonly IIntroHandoff? handoff;
    // the dark is never empty (owner 12 Oct 2026): a faint graphite light in the middle and slow dust in the air
    private readonly Grid backLayer=new(), xGroup=new();
    private readonly Canvas dust=new(){IsHitTestVisible=false,Opacity=0};
    private readonly System.Windows.Shapes.Rectangle haze=new(){IsHitTestVisible=false,Opacity=0};
    // the slogan under the X (owner 12 Oct 2026: "Keep it easy, Keep it 20", in a type as cool as the X): squared-off
    // Bahnschrift capitals, tracked wide and leaning with the X's blade, white shading to silver like the mark, with its
    // glow and its own run of light
    private const string SloganText="KEEP IT EASY, KEEP IT 20";
    private readonly TextBlock slogan=new(){IsHitTestVisible=false,Opacity=0,RenderTransform=new TranslateTransform(0,8),
        Foreground=new LinearGradientBrush(new GradientStopCollection{new(Color.FromRgb(214,221,234),0),new(Color.FromRgb(214,221,234),.4),new(Colors.White,.5),new(Color.FromRgb(214,221,234),.6),new(Color.FromRgb(196,205,222),1)},new Point(0,0),new Point(1,.4)){RelativeTransform=new TranslateTransform(-1,0)},
        Effect=new System.Windows.Media.Effects.DropShadowEffect{Color=Color.FromRgb(236,241,250),BlurRadius=18,ShadowDepth=0,Opacity=0}};
    private readonly Grid sloganBox=new(){IsHitTestVisible=false,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,RenderTransform=new SkewTransform(-12,0)};
    public IntroView(bool reduced,bool muted=false,IIntroHandoff? handoff=null)
    {
        Reduced=reduced;Muted=muted;this.handoff=handoff;PrepareSound(); // open the sound now, so it is ready the moment the LEDs blink
        Children.Add(backLayer);
        backLayer.Children.Add(new System.Windows.Shapes.Rectangle{Fill=new LinearGradientBrush(Color.FromRgb(9,10,14),Color.FromRgb(3,3,5),90)}); // black, as the X20CTL aura wants
        haze.Fill=new RadialGradientBrush(new GradientStopCollection{new(Color.FromArgb(46,70,88,120),0),new(Color.FromArgb(18,40,52,76),.5),new(Color.FromArgb(0,20,26,40),1)}){RadiusX=.6,RadiusY=.7,Center=new(.5,.5),GradientOrigin=new(.5,.5)};
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
        backLayer.Children.Add(Dots(dotsLeft,Orange,true));backLayer.Children.Add(Dots(dotsRight,Orange,false));backLayer.Children.Add(aura);backLayer.Children.Add(heart);
        SizeChanged+=(_,_)=>{dotsLeft.Width=dotsRight.Width=ActualWidth/2;};
        backLayer.Children.Add(dark); // the opening darkness; it lifts as the body appears
        backLayer.Children.Add(haze);backLayer.Children.Add(dust); // above the darkness: seen from the first moment
        stabilizationBackground.Visibility=reduced?Visibility.Collapsed:Visibility.Visible;
        backLayer.Children.Add(stabilizationBackground);

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


        contours.SetPoints(from[0].Concat(from[1].Reverse()).ToArray(),to);
        formation.Configure(from,to,markPx);

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
        stage.Children.Add(contours);stage.Children.Add(formation);
        controllerGroup.RenderTransform=new ScaleTransform(1,1,stageW/2,stageH/2);
        foreach(var e in new UIElement[]{floor,body,beat,glow,trail,line,tracer})controllerGroup.Children.Add(e);
        foreach(var (core,bloom) in leds){controllerGroup.Children.Add(bloom);controllerGroup.Children.Add(core);}
        stage.Children.Add(controllerGroup);
        // the X's heartbeat (owner 12 Oct 2026): the mark swells about its own centre, so every piece of it shares one scale
        var xBounds=Rect.Union(forward.Bounds,back.Bounds);var xBeat=new ScaleTransform(1,1,xBounds.X+xBounds.Width/2,xBounds.Y+xBounds.Height/2);
        foreach(var e in new UIElement[]{morphGhost,morph,filled,filledBack,formation})e.RenderTransform=xBeat;
        foreach(var e in new UIElement[]{morphGhost,morph,filled,filledBack,shine})xGroup.Children.Add(e);xGroup.Children.Insert(0,markAura); markAura.SetGeometry(markPx); aura.Visibility=Visibility.Collapsed; dotsLeft.Visibility=dotsRight.Visibility=Visibility.Collapsed; stage.Children.Add(xGroup);this.xBounds=xBounds;
        Children.Add(stage);sloganBox.Children.Add(slogan);Children.Add(sloganBox);Children.Add(fade);
        // WPF has no letter-spacing: hair spaces between the letters track it out, a wider gap between words
        slogan.Text=string.Join("\u200A",SloganText.Select(c=>c==' '?"\u2009\u2009":c.ToString()));
        slogan.FontFamily=new FontFamily("Bahnschrift SemiBold, Bahnschrift, Segoe UI");slogan.FontWeight=FontWeights.SemiBold;slogan.FontStretch=FontStretches.SemiExpanded;
        // under the X: its ink ends 289 below the stage centre at full size
        SizeChanged+=(_,_)=>{double z=Math.Clamp(ActualHeight/1200,.55,1.8);slogan.FontSize=28*z;sloganBox.Margin=new(0,2*(289+62)*z,0,0);sloganBox.RenderTransformOrigin=new(.5,.5);};
        Skip=new Button{Content="Skip · Esc / B",HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Bottom,Margin=new(0,0,36,36),Style=DS.Style("DS.Secondary"),Opacity=.8};Skip.Click+=(_,_)=>Finish();
        // mute (owner request 12 Oct 2026): beside Skip, remembered for the next launch; reduced motion has no sound to mute
        Mute=new Button{HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Bottom,Margin=new(0,0,36,36),Style=DS.Style("DS.Secondary"),Opacity=.8,Visibility=reduced||!HasIntroScore?Visibility.Collapsed:Visibility.Visible};
        Mute.Click+=(_,_)=>ToggleMute();ShowMute();
        Children.Add(new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Bottom,Children={Mute,Skip}});
        Mute.Margin=new(0,0,12,36);
        // a click anywhere else skips; the buttons' own clicks must not
        PreviewMouseDown+=(_,e)=>{if(e.ChangedButton==MouseButton.Left&&!Skip.IsMouseOver&&!Mute.IsMouseOver)Finish();};
        Loaded+=(_,_)=>{Build();if(startRequested)Begin();if(pendingSeek is double p)Seek(p);};
        Unloaded+=(_,_)=>{CompositionTarget.Rendering-=Tick;if(!finished){soundWanted=false;StopSound();}};
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
    private Rect xBounds; private Matrix stageToView; private Rect? markTarget; private bool targetsRead;
    private const int LinePoints=260;
    // the glowing mark and its aura are drawn (invisibly) from the first black frame, so their first real appearance
    // costs nothing: an effect's first render is the slowest frame it ever has
    private const double Warm=.003;
    private const double MarkScale=1.12, StageW=780, StageH=640;

    private void Build()
    {
        if(built)return;built=true;
        BuildDust();
        var ease=new CubicEase{EasingMode=EasingMode.EaseInOut};var outE=new CubicEase{EasingMode=EasingMode.EaseOut};var inE=new CubicEase{EasingMode=EasingMode.EaseIn};
        void A(DependencyObject target,string path,double a,double b,double at,double dur,IEasingFunction? e=null){var x=new DoubleAnimation(a,b,TimeSpan.FromSeconds(dur)){BeginTime=TimeSpan.FromSeconds(at),EasingFunction=e,FillBehavior=FillBehavior.HoldEnd};Storyboard.SetTarget(x,target);Storyboard.SetTargetProperty(x,new PropertyPath(path));story.Children.Add(x);}
        void DrawOn(System.Windows.Shapes.Path p,double at,double dur,IEasingFunction e){A(p,"Opacity",0,1,at,.01);A(p,"StrokeDashOffset",dash[p],0,at,dur,e);}
        double tu0=dash[trail];
        void TraceOn(System.Windows.Shapes.Path p,double at,double dur,IEasingFunction e){double u=dash[p];A(p,"StrokeDashOffset",u*.08,u*.08-u,at,dur,e);A(p,"Opacity",0,1,at+.02,.1);A(p,"Opacity",1,0,at+dur-.15,.12);}
        if(Reduced)
        {
            foreach(var p in new[]{line,glow,tracer,trail,beat,morph,morphGhost})p.Visibility=Visibility.Collapsed;
            foreach(var (core,bloom) in leds){core.Visibility=bloom.Visibility=Visibility.Collapsed;}
            // reduced motion: the X, held for a moment, while the zone comes up underneath it
            A(dark,"Opacity",1,0,0,.1);A(haze,"Opacity",0,1,0,.1);A(filled,"Opacity",0,1,0,.1);A(filledBack,"Opacity",0,1,0,.1);foreach(var p in new UIElement[]{heart,aura,dotsLeft,dotsRight})A(p,"Opacity",0,1,0,.1);story.Duration=TimeSpan.FromSeconds(ReducedFor);
        }
        else
        {
            foreach(var old in new[]{line,glow,tracer,trail})old.Visibility=Visibility.Collapsed;
            // One continuous sequence: tracing, recognition, anticipation, transformation, settlement.
            A(haze,"Opacity",0,.65,0,.75,outE);
            A(dark,"Opacity",1,.25,.12,.9,outE);
            foreach(var (index,at) in LedAt.Select((at,index)=>(index,at)))
            {
                var (core,bloom)=leds[index];A(core,"Opacity",0,.85,at,.18,outE);
                A(bloom,"Opacity",0,.24,at,.25,outE);
                A(core,"Opacity",.85,0,MorphAt-.3,.25,inE);A(bloom,"Opacity",.24,0,MorphAt-.3,.25,inE);
            }
            TraceOn(tracer,.35,1.5,ease);
            A(trail,"StrokeDashOffset",tu0*.3+tu0*.06,tu0*.3-tu0,.45,1.55,ease);
            A(trail,"Opacity",0,.42,.45,.18);A(trail,"Opacity",.42,0,1.75,.3,outE);
            DrawOn(line,.75,1.2,ease);DrawOn(glow,.95,1.15,ease);
            A(glow,"Opacity",1,.3,2.1,.3,outE);
            // Keep the owner's controller geometry, with restrained volume rather than a filled plastic body.
            A(body,"Opacity",0,.18,1.2,.55,outE);A(floor,"Opacity",0,.25,1.3,.55,outE);
            foreach(var axis in new[]{"(UIElement.RenderTransform).(ScaleTransform.ScaleX)","(UIElement.RenderTransform).(ScaleTransform.ScaleY)"})
                A(controllerGroup,axis,1,1.10,2.45,.5,inE);
            A(line,"Opacity",1,0,MorphAt,.025);A(glow,"Opacity",.3,0,MorphAt,.08);
            A(body,"Opacity",.18,0,MorphAt-.05,.15);A(floor,"Opacity",.25,0,MorphAt-.05,.15);
            A(morph,"Opacity",0,1,MorphAt-.025,.025);
            A(morphGhost,"Opacity",0,1,MorphAt+.08,.12);A(morphGhost,"Opacity",1,0,MorphAt+MorphFor-.12,.2);
            double lit=MorphAt+MorphFor-.05;
            A(filled,"Opacity",0,1,lit,.32,outE);A(filledBack,"Opacity",0,1,lit+.09,.32,outE);
            A(morph,"Opacity",1,0,lit+.26,.22,outE);
            // the second "dug" of the heartbeat: the locked X swells once about its own centre
            foreach(var axis in new[]{"(UIElement.RenderTransform).(ScaleTransform.ScaleX)","(UIElement.RenderTransform).(ScaleTransform.ScaleY)"})
            {A(filled,axis,1,1.035,XBeatAt,.07,outE);A(filled,axis,1.035,1,XBeatAt+.07,.3,ease);}
            A(heart,"Opacity",0,.85,MorphAt+.05,.3,outE);A(heart,"Opacity",.85,.45,lit+.15,1.1,outE);
            A(aura,"Opacity",0,.85,lit,.45,outE);A(aura,"Opacity",.85,.55,5.0,.8,outE);
            A(dotsLeft,"Opacity",0,.18,lit,.3,outE);A(dotsRight,"Opacity",0,.18,lit,.3,outE);
            A(dotsLeft,"Opacity",.18,0,4.8,.8,outE);A(dotsRight,"Opacity",.18,0,4.8,.8,outE);
            A(shine,"Opacity",0,.65,lit+.25,.1);
            A(shine,"(Shape.Fill).(Brush.RelativeTransform).(TranslateTransform.X)",-.8,.8,lit+.3,.85,ease);
            A(shine,"Opacity",.65,0,lit+1.05,.2,outE);
            A(slogan,"Opacity",0,1,SloganAt,.4,outE);A(slogan,"(UIElement.RenderTransform).(TranslateTransform.Y)",5,0,SloganAt,.4,outE);
            A(slogan,"(TextBlock.Foreground).(Brush.RelativeTransform).(TranslateTransform.X)",-.8,.8,SloganAt+.1,.7,ease);
            A(slogan,"Opacity",1,0,FlyAt,.25,inE);
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
        morph.Visibility=morphGhost.Visibility=Visibility.Collapsed;
    }
    private static double Length(PathFigure f)
    {
        double sum=0;var p=f.StartPoint;foreach(var s in f.Segments){if(s is PolyLineSegment pl)foreach(var q in pl.Points){sum+=(q-p).Length;p=q;}else if(s is LineSegment l){sum+=(l.Point-p).Length;p=l.Point;}}if(f.IsClosed)sum+=(f.StartPoint-p).Length;return sum;
    }

    private double Duration=>Reduced?ReducedFor:Total;
    private double Elapsed=>seekAt?.TotalSeconds??(DateTime.UtcNow-beganAt).TotalSeconds;
    private DateTime beganAt=DateTime.UtcNow;
    public void Start(){startRequested=true;PrepareSound();if(IsLoaded)Begin();}
    /// <summary>The opening sound, made from sine waves by tools/build_intro_sound.py (no samples). Off for reduced
    /// motion, development fixtures and mute.</summary>
    public static bool HasIntroScore => System.IO.File.Exists(ScorePath);
    private static string ScorePath => System.IO.Path.Combine(AppContext.BaseDirectory,"Brand","intro.wav");
    public static bool SoundEnabled {get;set;}=true;
    private System.Windows.Media.MediaPlayer? sound;
    private bool soundReady,soundWanted;
    /// <summary>Opens the sound before the intro starts (opening it on demand made the LED pings lag their lights).</summary>
    private void PrepareSound()
    {
        if(!HasIntroScore||sound!=null||!SoundEnabled||Muted||Reduced||ReviewSandbox.Active)return;
        string path=ScorePath;if(!System.IO.File.Exists(path))return;
        // a player muted before it opened must not mark its replacement ready
        try{var player=new System.Windows.Media.MediaPlayer{Volume=.65};soundReady=false;sound=player;player.MediaOpened+=(_,_)=>{if(player!=sound)return;soundReady=true;if(soundWanted)PlayInSync();};player.Open(new Uri(path));}
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
    /// <summary>Mute fades the sound out at once; unmuting picks it up again at the picture's moment.</summary>
    public void ToggleMute()
    {
        if(!HasIntroScore||Reduced||finished)return;Muted=!Muted;ShowMute();
        if(Muted){soundWanted=false;soundReady=false;StopSound();}else if(begun)PlaySound();else PrepareSound();
        MutedChanged?.Invoke(Muted);
    }
    private void ShowMute()=>Mute.Content=Muted?"Unmute · M / Y":"Mute · M / Y";
    private bool begun;
    private void Begin(){if(finished)return;Build();beganAt=DateTime.UtcNow;begun=true;story.Begin(this,true);PlaySound();CompositionTarget.Rendering-=Tick;CompositionTarget.Rendering+=Tick;}
    public void Finish(){if(finished)return;finished=true;CompositionTarget.Rendering-=Tick;story.Stop(this);StopSound();ZoneReveal?.Invoke(1);handoff?.HandoffEnd();Completed?.Invoke();}
    /// <summary>Review hook: freeze the cutscene at time t (seconds).</summary>
    public void Seek(double t){if(!IsLoaded){pendingSeek=t;return;}Build();CompositionTarget.Rendering-=Tick;seekAt=TimeSpan.FromSeconds(t);story.Begin(this,true);story.Seek(this,TimeSpan.FromSeconds(t),TimeSeekOrigin.BeginTime);story.Pause(this);Frame(t);}
    private void Tick(object? s,EventArgs e)=>Frame(Elapsed);
    private void Frame(double t)
    {
        if(!Reduced)stabilizationBackground.SetTime(t-(MorphAt+MorphFor));
        Morph(t);
        markAura.SetTime(Reduced ? -1 : t-4.0);
        formation.SetTime(Reduced ? -1 : t-MorphAt);
        contours.SetTime(Reduced ? -1 : t);
        if(Reduced)
        {
            double start=Duration-.5,amount=Math.Clamp((t-start)/.5,0,1);amount=1-Math.Pow(1-amount,3);
            Opacity=1-amount;ZoneReveal?.Invoke(amount);handoff?.Handoff(1,1,1,1);Skip.Opacity=Mute.Opacity=.8*(1-amount);return;
        }
        // the hand-over (owner 12 Oct 2026): the X travels from the middle to the logo's place at the top left, shrinking
        // as it goes, while the dark fades and the Zone grows in around it; it lands on the header mark exactly
        if(t>=FlyAt&&!targetsRead)ReadTargets();
        double q=CubicBezier(Win(t,FlyAt,FlyAt+FlyFor),.16,.84,.24,1);
        if(markTarget is Rect target&&q>0)
        {
            var from=Rect.Transform(xBounds,stageToView);var inverse=stageToView;inverse.Invert();
            xGroup.RenderTransform=new MatrixTransform(stageToView*Fly(from,target,q)*inverse);
        }
        else xGroup.RenderTransform=null;
        double brand=markTarget==null?Smooth(Win(t,FlyAt,FlyAt+.5)):Smooth(Win(q,.85,1));
        xGroup.Opacity=1-brand;
        backLayer.Opacity=1-Smooth(Win(t,FlyAt+.05,FlyAt+.7));
        double landed=Win(t,FlyAt+FlyFor,FlyAt+FlyFor+.14);
        if(t>=FlyAt)handoff?.Handoff(Win(t,FlyAt,FlyAt+FlyFor+.1),brand,1,landed>0&&landed<1?1+.03*Math.Sin(Math.PI*landed):1);
        Skip.Opacity=Mute.Opacity=.8*(1-Smooth(Win(t,FlyAt,FlyAt+.3)));
        // nearly settled: the Zone already answers the mouse
        IsHitTestVisible=t<Total-.1;
        if(handoff==null)ZoneReveal?.Invoke(Smooth(Win(t,FlyAt,FlyAt+FlyFor)));
    }
    private void ReadTargets()
    {
        targetsRead=true;
        try{stageToView=((Transform)stage.TransformToVisual(this)).Value;markTarget=handoff?.MarkTarget(this);handoff?.HandoffBegin();}
        catch(Exception error) when(error is InvalidOperationException or InvalidCastException){markTarget=null;}
    }
    /// <summary>From the middle to the header: one transform, position and size together, along a curve that runs left
    /// before it rises into the tile from below, so it never crosses the word beside it.</summary>
    private static Matrix Fly(Rect from,Rect to,double q)
    {
        var m=new Matrix();double scale=Math.Exp(Math.Log(Math.Min(to.Width/from.Width,to.Height/from.Height))*q);
        Point a=new(from.X+from.Width/2,from.Y+from.Height/2),b=new(to.X+to.Width/2,to.Y+to.Height/2),c=new(a.X+(b.X-a.X)*.9,a.Y+(b.Y-a.Y)*.2);
        double u=1-q;var at=new Point(u*u*a.X+2*u*q*c.X+q*q*b.X,u*u*a.Y+2*u*q*c.Y+q*q*b.Y);
        m.Translate(-a.X,-a.Y);m.Scale(scale,scale);m.Translate(at.X,at.Y);return m;
    }
    /// <summary>Slow dust: a few dozen faint specks drifting upward across the dark, over the whole intro. Four layers of
    /// twenty, each cached as one bitmap and moved as one, nearer layers a little faster, so it costs four moves a frame.</summary>
    private void BuildDust()
    {
        double w=ActualWidth>0?ActualWidth:1600,h=ActualHeight>0?ActualHeight:900;var rng=new Random(20261012);
        dust.Width=w;dust.Height=h;
        for(int l=0;l<4;l++)
        {
            var layer=new Canvas{Width=w,Height=h,IsHitTestVisible=false,CacheMode=new BitmapCache(1)};var drift=new TranslateTransform();layer.RenderTransform=drift;dust.Children.Add(layer);
            for(int i=0;i<20;i++)
            {
                bool soft=rng.NextDouble()<.2;double r=soft?3+rng.NextDouble()*4:.6+rng.NextDouble()*1.2;
                var speck=new System.Windows.Shapes.Ellipse{Width=2*r,Height=2*r,Opacity=soft?.05+rng.NextDouble()*.06:.08+rng.NextDouble()*.22,
                    Fill=soft?new RadialGradientBrush(Color.FromArgb(200,220,228,240),Color.FromArgb(0,220,228,240)):new SolidColorBrush(Color.FromRgb(226,232,242))};
                Canvas.SetLeft(speck,rng.NextDouble()*w);Canvas.SetTop(speck,rng.NextDouble()*h);layer.Children.Add(speck);
            }
            foreach(var (path,to) in new[]{("(UIElement.RenderTransform).(TranslateTransform.X)",(l%2==0?1:-1)*(6+5*l)),("(UIElement.RenderTransform).(TranslateTransform.Y)",-(14+10*l))})
            {var x=new DoubleAnimation(0,to,TimeSpan.FromSeconds(Total)){FillBehavior=FillBehavior.HoldEnd};Storyboard.SetTarget(x,layer);Storyboard.SetTargetProperty(x,new PropertyPath(path));story.Children.Add(x);}
        }
    }
    private static double Win(double t,double a,double b)=>Math.Clamp((t-a)/(b-a),0,1);
    private static double Smooth(double x){x=Math.Clamp(x,0,1);return x*x*(3-2*x);}
    /// <summary>CSS-style cubic-bezier(x1, y1, x2, y2) easing.</summary>
    private static double CubicBezier(double x,double x1,double y1,double x2,double y2)
    {
        if(x<=0)return 0;if(x>=1)return 1;double u=x;
        for(int i=0;i<8;i++){double cx=Bz(u,x1,x2)-x,d=3*(1-u)*(1-u)*x1+6*(1-u)*u*(x2-x1)+3*u*u*(1-x2);if(Math.Abs(d)<1e-6)break;u=Math.Clamp(u-cx/d,0,1);}
        return Bz(u,y1,y2);
        static double Bz(double t,double a,double b)=>3*(1-t)*(1-t)*t*a+3*(1-t)*t*t*b+t*t*t;
    }
}


/// <summary>A single deterministic particle release, drawn in one visual with no per-particle controls or timers.</summary>
/// <summary>Travelling tapered fragments and delayed white recognition on the original controller contour.</summary>
internal sealed class IntroContourFlow:FrameworkElement
{
    private Point[] controller=[];private Point[][] mark=[];private double time=-1;
    public void SetPoints(Point[] source,Point[][] destination){controller=source;mark=destination;}
    public void SetTime(double value){if(value==time)return;time=value;InvalidateVisual();}
    private static double Smooth(double t){t=Math.Clamp(t,0,1);return t*t*(3-2*t);}
    protected override void OnRender(DrawingContext dc)
    {
        if(time<.12 || controller.Length==0)return;
        if(time<2.95)
        {
            double anticipate=1+.10*Smooth((time-2.45)/.5);
            dc.PushTransform(new ScaleTransform(anticipate,anticipate,390,320));
            double colour=1-Smooth((time-1.65)/.65);
            double position=(time-.12)*.61;
            Segment(dc,controller,position,.24,9,colour,true);
            Segment(dc,controller,position+.48,.20,6,colour*.34,false);
            // one smooth stroke; the sound's plucks follow it (IntroBeats)
            double white=IntroBeats.Drawn(time);
            if(white>0)Segment(dc,controller,0,white,8.5,1,false,white>.999);
            dc.Pop();
        }
        else if(time>4.35 && time<5.95)
        {
            double a=Smooth((time-4.35)/.15)*(1-Smooth((time-5.5)/.45));
            foreach(var piece in mark)Segment(dc,piece,(time-4.35)*.72,.24,4.5,a,true);
        }
    }
    private void Segment(DrawingContext dc,Point[] points,double start,double span,double width,double alpha,bool coloured,bool full=false)
    {
        if(alpha<=0 || span<=0)return;
        int count=Math.Max(2,(int)(span*points.Length));
        Point At(double v){v-=Math.Floor(v);double index=v*(points.Length-1);int i=(int)index;return points[i]+(points[Math.Min(i+1,points.Length-1)]-points[i])*(index-i);}
        for(int j=0;j<count;j++)
        {
            double u=j/(double)count;
            double tip=full?1:Math.Pow(Math.Sin(Math.PI*Math.Clamp(u,0,1)),.45);
            double phase=Math.Clamp((time-.3)/2,0,1);
            var c=coloured?Color.FromRgb((byte)(45+95*phase), (byte)(145-65*phase),255):Color.FromRgb(238,242,250);
            var ink=new SolidColorBrush(Color.FromArgb((byte)(255*alpha*tip),c.R,c.G,c.B));ink.Freeze();
            var pen=new Pen(ink,width*(.25+.75*tip)){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round};pen.Freeze();
            dc.DrawLine(pen,At(start+u*span),At(start+(j+1)/(double)count*span));
        }
    }
}

/// <summary>Light follows the original X geometry; no replacement logo or external artwork.</summary>
internal sealed class IntroMarkAura : FrameworkElement
{
    private Geometry mark=Geometry.Empty;
    private double time=-1;
    public void SetGeometry(Geometry geometry)=>mark=geometry;
    public void SetTime(double value){time=value;InvalidateVisual();}
    protected override void OnRender(DrawingContext dc)
    {
        if(time<0||time>2.0||mark.IsEmpty())return;
        double enter=Math.Clamp(time/.16,0,1),release=1-Math.Clamp((time-.55)/1.45,0,1);
        double power=enter*release;
        for(int side=0;side<2;side++)
        {
            var colour=side==0?Color.FromRgb(65,177,255):Color.FromRgb(255,154,70);
            double sign=side==0?-1:1;
            for(int layer=7;layer>=1;layer--)
            {
                double separation=layer*3*(1-Math.Clamp(time/1.25,0,1));
                dc.PushTransform(new TranslateTransform(sign*separation,-sign*separation));
                dc.PushOpacity(power*(.035+.065*(1-layer/8d)));
                dc.DrawGeometry(null,new Pen(new SolidColorBrush(colour),layer*2.8+1){LineJoin=PenLineJoin.Miter},mark);
                dc.Pop();dc.Pop();
            }
        }
        dc.PushClip(mark);
        for(int side=0;side<2;side++)
        {
            double p=Math.Clamp((time-side*.16)/.9,0,1),x=side==0?-120+p*1000:900-p*1000;
            var colour=side==0?Color.FromArgb(210,110,210,255):Color.FromArgb(180,255,185,105);
            var light=new LinearGradientBrush(new GradientStopCollection{new(Colors.Transparent,0),new(colour,.32),new(Colors.White,.5),new(colour,.68),new(Colors.Transparent,1)},0);
            dc.PushOpacity(power);dc.PushTransform(new RotateTransform(-43,390,320));
            dc.DrawRectangle(light,null,new Rect(x,0,80,700));dc.Pop();dc.Pop();
        }
        dc.Pop();
    }
}
