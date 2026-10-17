using System.Diagnostics;
using System.Windows;
using System.Windows.Media;

namespace X20Ctl.Product;

/// <summary>The Zone's environment: soft moving illumination below opaque device surfaces.</summary>
public sealed class ZoneBackdrop : FrameworkElement
{
    private readonly Stopwatch clock=new();
    private bool reduced;
    private readonly bool manual;
    private bool listening;
    private double time,last=-1;
    private Window? owner;
    private readonly Brush backdrop=(Brush)Application.Current.FindResource("Zone.Backdrop");
    private readonly Brush cool=Pool(Color.FromRgb(36,83,194),150);
    private readonly Brush warm=Pool(Color.FromRgb(20,53,135),100);
    public ZoneBackdrop():this(false,false){}
    public ZoneBackdrop(bool reducedMotion,bool manual=false)
    {
        reduced=reducedMotion||!SystemParameters.ClientAreaAnimation;this.manual=manual;IsHitTestVisible=false;ClipToBounds=true;
        Loaded+=(_,_)=>{owner=Window.GetWindow(this);if(owner!=null)owner.StateChanged+=WindowChanged;Refresh();};
        Unloaded+=(_,_)=>{Stop();if(owner!=null)owner.StateChanged-=WindowChanged;owner=null;};IsVisibleChanged+=(_,_)=>Refresh();
    }
    private static Brush Pool(Color c,byte alpha){var b=new RadialGradientBrush(new GradientStopCollection{new(Color.FromArgb(alpha,c.R,c.G,c.B),0),new(Color.FromArgb((byte)(alpha/3),c.R,c.G,c.B),.45),new(Color.FromArgb(0,c.R,c.G,c.B),1)});b.Freeze();return b;}
    private void WindowChanged(object? sender,EventArgs e)=>Refresh();
    private void Refresh(){if(manual||reduced||!IsLoaded||!IsVisible||owner?.WindowState==WindowState.Minimized){Stop();return;}if(listening)return;listening=true;clock.Start();CompositionTarget.Rendering+=Tick;}
    private void Tick(object? sender,EventArgs e){double now=clock.Elapsed.TotalSeconds;if(now-last<1d/15)return;last=now;SetTime(now);}
    public void Stop(){if(listening)CompositionTarget.Rendering-=Tick;listening=false;clock.Stop();}
    public bool ReducedMotion {get=>reduced;set{reduced=value||!SystemParameters.ClientAreaAnimation;if(reduced)SetTime(0);Refresh();}}
    public void SetTime(double seconds){time=reduced?0:seconds;InvalidateVisual();}
    protected override void OnRender(DrawingContext dc)
    {
        double w=ActualWidth,h=ActualHeight;if(w<=0||h<=0)return;
        dc.DrawRectangle(backdrop,null,new Rect(0,0,w,h));
        double drift=Math.Sin(time*.065)*.025;
        dc.DrawEllipse(cool,null,new Point(w*(.91+drift),h*.08),w*.66,h*.82);
        dc.DrawEllipse(warm,null,new Point(w*(.02-drift*.7),h*.63),w*.62,h*.95);
    }
}
