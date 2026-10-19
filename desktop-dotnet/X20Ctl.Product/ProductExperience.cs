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
/// The opening film (owner 19 Oct 2026): Brand/intro.mp4, rendered by logo-preview/intro-remotion with its own sound.
/// The camera rides with the bird drawing the controller, pulls back, the bird charges on the top edge, strikes, and the
/// logo resolves. Near the end an exact copy of the logo (Brand/x20ctl-mark.png, the same art) is laid over the film's,
/// the film fades out under it, and it flies onto the header mark while the Zone grows in around it.
/// Skip, mute and reduced motion are retained; reduced motion plays no film and no sound.
/// </summary>
public sealed class IntroView:Grid
{
    private const double FilmEnd=7.1, CoverAt=6.45, FlyAt=6.6, FlyFor=.8, Total=FlyAt+FlyFor, ReducedFor=.4, OpenWithin=2.5;
    // the logo's ink in the film's 1920x1080 frame (layout.ts: 660 px wide about (960, 470), then FINAL_PUSH 1.035)
    private static readonly Rect FilmLogo=new(622.42,235.10,674.90,469.80);
    // the logo's ink inside Brand/x20ctl-mark.png (512 px): its alpha box
    private static readonly Rect MarkInk=new(10,85,492,342);
    public IntroView Visual=>this;
    public Button Skip {get;} public Button Mute {get;}
    /// <summary>The player muted the intro (from its button, M or Y). The host remembers it.</summary>
    public bool Muted {get;private set;} public event Action<bool>? MutedChanged; public bool Reduced {get;}
    public event Action? Completed; public event Action<double>? ZoneReveal;
    public static bool SoundEnabled {get;set;}=true;
    /// <summary>The film carries the sound, so "has a score" means the film is installed beside the app.</summary>
    public static bool HasIntroScore => System.IO.File.Exists(FilmPath);
    private static string FilmPath => System.IO.Path.Combine(AppContext.BaseDirectory,"Brand","intro.mp4");

    private readonly MediaElement? film;
    private readonly Image cover=new(){IsHitTestVisible=false,Opacity=0,Stretch=Stretch.Fill,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top};
    private readonly System.Windows.Shapes.Rectangle backdrop=new(){Fill=new SolidColorBrush(Color.FromRgb(2,4,10)),IsHitTestVisible=false};
    private readonly IIntroHandoff? handoff;
    private readonly System.Windows.Threading.DispatcherTimer clock=new(System.Windows.Threading.DispatcherPriority.Render){Interval=TimeSpan.FromMilliseconds(15)};
    private bool finished,startRequested,began,opened,handing,targetsRead;
    private DateTime beganAt;private double? frozenAt;private Rect? markTarget;

    public IntroView(bool reduced,bool muted=false,IIntroHandoff? handoff=null)
    {
        Reduced=reduced;Muted=muted;this.handoff=handoff;
        Children.Add(backdrop);
        if(!reduced&&HasIntroScore)
        {
            film=new MediaElement{LoadedBehavior=MediaState.Manual,UnloadedBehavior=MediaState.Close,Stretch=Stretch.UniformToFill,ScrubbingEnabled=true,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,IsHitTestVisible=false,Volume=.8};
            ApplyMute();
            // centred: WPF pins an element larger than its slot to the top left otherwise, and the cover expects it centred
            // playback starts when the file opens; the clock is set from that moment so the hand-over meets the picture
            film.MediaOpened+=(_,_)=>{opened=true;if(frozenAt==null)beganAt=DateTime.UtcNow;};
            film.MediaFailed+=(_,_)=>Finish();
            film.Source=new Uri(FilmPath);
            Children.Add(film);
        }
        cover.Source=new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/X20Ctl;component/Assets/brand.png"));
        Children.Add(cover);
        Skip=new Button{Content="Skip · Esc / B",HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Bottom,Margin=new(0,0,36,36),Style=DS.Style("DS.Secondary"),Opacity=.8};Skip.Click+=(_,_)=>Finish();
        // mute (owner request 12 Oct 2026): beside Skip, remembered for the next launch; reduced motion has no sound to mute
        Mute=new Button{HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Bottom,Margin=new(0,0,36,36),Style=DS.Style("DS.Secondary"),Opacity=.8,Visibility=reduced||!HasIntroScore?Visibility.Collapsed:Visibility.Visible};
        Mute.Click+=(_,_)=>ToggleMute();ShowMute();
        Children.Add(new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Bottom,Children={Mute,Skip}});
        PreviewMouseDown+=(_,e)=>{if(e.ChangedButton==MouseButton.Left&&!Skip.IsMouseOver&&!Mute.IsMouseOver)Finish();};
        clock.Tick+=(_,_)=>Frame(Elapsed);
        Loaded+=(_,_)=>{if(startRequested)Begin();};
    }

    private double Elapsed=>frozenAt??(DateTime.UtcNow-beganAt).TotalSeconds;
    private void ApplyMute(){if(film!=null)film.IsMuted=Muted||!SoundEnabled||ReviewSandbox.Active;}
    private void ShowMute()=>Mute.Content=Muted?"Unmute · M / Y":"Mute · M / Y";

    public void Start(){startRequested=true;if(IsLoaded)Begin();}
    private void Begin()
    {
        if(finished||began)return;began=true;beganAt=DateTime.UtcNow;
        film?.Play();clock.Start();
    }
    /// <summary>Mute silences the film at once; unmuting brings it back where the picture is.</summary>
    public void ToggleMute()
    {
        if(!HasIntroScore||Reduced||finished)return;Muted=!Muted;ShowMute();ApplyMute();
        MutedChanged?.Invoke(Muted);
    }
    public void Finish()
    {
        if(finished)return;finished=true;clock.Stop();
        if(film!=null){film.Stop();film.Close();}
        ZoneReveal?.Invoke(1);handoff?.HandoffEnd();Completed?.Invoke();
    }
    /// <summary>Review hook: hold the intro at time t (seconds).</summary>
    public void Seek(double t)
    {
        frozenAt=t;clock.Stop();
        if(film!=null){film.Play();film.Pause();film.Position=TimeSpan.FromSeconds(Math.Min(t,FilmEnd));}
        Frame(t);
    }

    private void Frame(double t)
    {
        if(finished)return;
        if(Reduced)
        {
            double amount=Smooth(Win(t,0,ReducedFor));
            Opacity=1-amount;ZoneReveal?.Invoke(amount);handoff?.Handoff(1,1,1,1);Skip.Opacity=Mute.Opacity=.8*(1-amount);
            if(t>=ReducedFor)Finish();return;
        }
        // a film that never opens (a missing codec) must not hold the app behind a black screen
        if(film!=null&&!opened&&frozenAt==null&&t>OpenWithin){Finish();return;}
        if(film==null&&t<CoverAt)t=CoverAt; // no film installed: start at the logo and hand over at once

        // the cover: the film's logo, laid exactly over it, then the film fades out from under it
        var logo=OnScreen(FilmLogo);
        cover.Width=logo.Width*512/MarkInk.Width;cover.Height=cover.Width;
        // centred on the film's logo: the ink's centre in the image lands on the logo's centre
        double k=cover.Width/512;var at=new Point(logo.X+logo.Width/2-(MarkInk.X+MarkInk.Width/2)*k,logo.Y+logo.Height/2-(MarkInk.Y+MarkInk.Height/2)*k);
        double shown=t>=CoverAt?1:0;
        if(film!=null)film.Opacity=1-Smooth(Win(t,CoverAt+.01,FlyAt));

        // the hand-over (owner 12 Oct 2026): the logo travels to the header mark, shrinking as it goes, while the dark
        // fades and the Zone grows in around it; it lands on the header mark exactly
        if(t>=FlyAt&&!targetsRead){targetsRead=true;try{markTarget=handoff?.MarkTarget(this);handoff?.HandoffBegin();handing=true;}catch(Exception error) when(error is InvalidOperationException or InvalidCastException){markTarget=null;}}
        double q=CubicBezier(Win(t,FlyAt,FlyAt+FlyFor),.16,.84,.24,1);
        // the image is placed by its top-left corner; the fly matrix works on the logo's box, so place first, then fly
        var place=new Matrix();place.Translate(at.X,at.Y);if(markTarget is Rect target&&q>0)place.Append(Fly(logo,target,q));
        cover.RenderTransform=new MatrixTransform(place);
        double brand=markTarget==null?Smooth(Win(t,FlyAt,FlyAt+.5)):Smooth(Win(q,.85,1));
        cover.Opacity=shown*(1-brand);
        backdrop.Opacity=1-Smooth(Win(t,FlyAt+.05,FlyAt+.7));
        double landed=Win(t,FlyAt+FlyFor,FlyAt+FlyFor+.14);
        if(handing)handoff?.Handoff(Win(t,FlyAt,FlyAt+FlyFor+.1),brand,1,landed>0&&landed<1?1+.03*Math.Sin(Math.PI*landed):1);
        Skip.Opacity=Mute.Opacity=.8*(1-Smooth(Win(t,FlyAt,FlyAt+.3)));
        // nearly settled: the Zone already answers the mouse
        IsHitTestVisible=t<Total-.1;
        if(handoff==null)ZoneReveal?.Invoke(Smooth(Win(t,FlyAt,FlyAt+FlyFor)));
        if(t>=Total+.15&&frozenAt==null)Finish();
    }
    /// <summary>A box in the film's 1920x1080 frame, where it lands on this view (the film fills it, cropping the edges).</summary>
    private Rect OnScreen(Rect r)
    {
        double w=ActualWidth>0?ActualWidth:1920,h=ActualHeight>0?ActualHeight:1080,k=Math.Max(w/1920,h/1080);
        double ox=(w-1920*k)/2,oy=(h-1080*k)/2;return new(ox+r.X*k,oy+r.Y*k,r.Width*k,r.Height*k);
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
