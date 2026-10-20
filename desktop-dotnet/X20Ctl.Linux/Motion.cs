using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
namespace X20Ctl.Linux;

/// <summary>The app's motion, with the Windows app's values (X20Ctl.Product/Fx.cs): a screen arrives from a little larger or
/// smaller and transparent, a screen leaving fades and drifts, a button presses in and springs back. Frames come from the
/// window's render clock, so they land on the display's refresh.</summary>
internal static class Motion
{
    /// <summary>Off: every change lands at once.</summary>
    public static bool Enabled { get; set; } = true;
    /// <summary>The window whose render clock drives every animation.</summary>
    public static TopLevel? Clock { get; set; }

    /// <summary>Calls <paramref name="step"/> with 0 to 1 each frame for <paramref name="ms"/> milliseconds, then
    /// <paramref name="done"/>. Returns a stop: after it, no more steps and no done.</summary>
    public static Action Run(double ms, Action<double> step, Action? done = null)
    {
        if (!Enabled || Clock is not { } clock || ms <= 0) { step(1); done?.Invoke(); return () => { }; }
        var watch = Stopwatch.StartNew(); bool stopped = false;
        void Frame(TimeSpan _)
        {
            if (stopped) return;
            double t = Math.Min(1, watch.Elapsed.TotalMilliseconds / ms); step(t);
            if (t < 1) clock.RequestAnimationFrame(Frame); else done?.Invoke();
        }
        step(0); clock.RequestAnimationFrame(Frame);
        return () => stopped = true;
    }

    public static double CubicOut(double t) => 1 - Math.Pow(1 - Math.Clamp(t, 0, 1), 3);
    public static double CubicInOut(double t) { t = Math.Clamp(t, 0, 1); return t < .5 ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2; }
    /// <summary>Ease in-out quint: fast through the middle, soft at both ends (the Zone's card glide).</summary>
    public static double QuintInOut(double t) { t = Math.Clamp(t, 0, 1); return t < .5 ? 16 * Math.Pow(t, 5) : 1 - Math.Pow(-2 * t + 2, 5) / 2; }
    /// <summary>WPF's BackEase, EaseOut, amplitude .55: a little past the end, then back.</summary>
    public static double SpringOut(double t) { double u = 1 - Math.Clamp(t, 0, 1); return 1 - (u * u * u - u * .55 * Math.Sin(Math.PI * u)); }
    public static double Lerp(double a, double b, double f) => a + (b - a) * f;

    static readonly AttachedProperty<Action?> RunningProperty = AvaloniaProperty.RegisterAttached<Control, Control, Action?>("MotionRunning");
    /// <summary>One animation per control: a new one stops the last.</summary>
    static void Own(Control c, Action stop) { c.GetValue(RunningProperty)?.Invoke(); c.SetValue(RunningProperty, stop); }
    static ScaleTransform Scale(Control c)
    {
        c.RenderTransformOrigin = RelativePoint.Center;
        if (c.RenderTransform is ScaleTransform own) return own;
        var fresh = new ScaleTransform(1, 1); c.RenderTransform = fresh; return fresh;
    }

    /// <summary>A screen or panel arriving: from <paramref name="fromScale"/> and transparent, to rest.</summary>
    public static void Appear(Control c, double fromScale = 1.05, int ms = 340)
    {
        var s = Scale(c);
        Own(c, Run(ms, t => { c.Opacity = CubicOut(Math.Min(1, t / .8)); s.ScaleX = s.ScaleY = Lerp(fromScale, 1, CubicOut(t)); },
            () => { c.Opacity = 1; s.ScaleX = s.ScaleY = 1; }));
    }

    /// <summary>A screen or panel leaving: it stops taking input at once, fades and drifts in scale, then is gone.</summary>
    public static void Leave(Control c, Action gone, double toScale = 1.04, int ms = 240)
    {
        c.IsHitTestVisible = false; var s = Scale(c); double from = c.Opacity;
        // a window with no frames coming (minimised, hidden to the tray) must still let it go: whichever comes first, once
        bool left = false; void Gone() { if (left) return; left = true; gone(); }
        Avalonia.Threading.DispatcherTimer.RunOnce(Gone, TimeSpan.FromMilliseconds(ms + 400));
        Own(c, Run(ms, t => { c.Opacity = from * (1 - CubicOut(t)); s.ScaleX = s.ScaleY = Lerp(1, toScale, CubicOut(t)); }, Gone));
    }

    /// <summary>A page sliding in beside the last one (a Studio tab): from <paramref name="dx"/> pixels across and transparent.</summary>
    public static void SlideIn(Control c, double dx, int ms = 240)
    {
        var move = new TranslateTransform(dx, 0); c.RenderTransform = move;
        Own(c, Run(ms, t => { double e = CubicOut(t); c.Opacity = e; move.X = dx * (1 - e); }, () => { c.Opacity = 1; c.RenderTransform = null; }));
    }

    /// <summary>Press-in (.94 over 80 ms) and spring-back (260 ms) for a button.</summary>
    public static void Press(Control c, bool down)
    {
        var s = Scale(c); double from = s.ScaleX, to = down ? .94 : 1;
        if (!down && from >= 1) return;
        Own(c, Run(down ? 80 : 260, t => s.ScaleX = s.ScaleY = Lerp(from, to, down ? CubicOut(t) : SpringOut(t))));
    }
}
