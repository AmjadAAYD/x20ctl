using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using X20Ctl.Linux;

namespace X20Ctl.Droid;

/// <summary>The app on a phone: the same <see cref="MainView"/> the desktop window holds, in its touch layout.</summary>
public sealed class PhoneApp : Application
{
    public override void Initialize()
    {
        RequestedThemeVariant = ThemeVariant.Dark;
        Styles.Add(new FluentTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is ISingleViewApplicationLifetime single) single.MainView = new PhoneHost();
        base.OnFrameworkInitializationCompleted();
    }
}

/// <summary>
/// Holds the app at a size made for touch: one layout unit is <see cref="UnitDp"/> of the screen's density-independent
/// pixels, so text and buttons keep a readable, tappable size on a phone, and the screens use their compact phone
/// layouts (<see cref="Phone.Active"/>). A tablet tall enough gets the desktop layout at the same unit. Back goes one
/// screen up, as Escape does on the desktop.
/// </summary>
internal sealed class PhoneHost : Border
{
    const double UnitDp = .9, MinHeight_ = 430;
    readonly MainView view;
    readonly Viewbox box;
    /// <summary>The phone's Back, from the activity: one screen up; false on the Controller Zone, where it leaves the app.</summary>
    public static Func<bool> Back { get; private set; } = () => false;

    public PhoneHost()
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        view = new MainView();
        global::Android.Util.Log.Info("X20CTL", $"startup: screens built in {clock.ElapsedMilliseconds} ms");
        Back = view.Back;
        Background = new SolidColorBrush(T.Void);
        box = new Viewbox { Stretch = Stretch.Uniform, Child = view };
        Child = box;
        AttachedToVisualTree += (_, _) =>
        {
            if (TopLevel.GetTopLevel(this) is not { } top) return;
            // edge to edge: the dark fills the whole screen, the content keeps clear of the cutout and rounded corners
            if (top.InsetsManager is { } insets)
            {
                insets.DisplayEdgeToEdgePreference = true; insets.IsSystemBarVisible = false;
                Padding = insets.SafeAreaPadding; insets.SafeAreaChanged += (_, e) => Padding = e.SafeAreaPadding;
            }
            FrameMeter.Start(top);
        };
    }

    protected override Size MeasureOverride(Size available)
    {
        var room = available.Deflate(Padding);
        if (room.Width > 0 && room.Height > 0 && !double.IsInfinity(room.Width) && !double.IsInfinity(room.Height))
        {
            view.Height = Math.Max(MinHeight_, room.Height / UnitDp);
            view.Width = view.Height * room.Width / room.Height;
        }
        return base.MeasureOverride(available);
    }
}

/// <summary>Logs how smoothly the app draws for its first half minute (the intro and the first screens): frames per
/// second and the longest gap between frames, every two seconds, to the Android log under the X20CTL tag.</summary>
internal static class FrameMeter
{
    public static void Start(TopLevel top)
    {
        var started = System.Diagnostics.Stopwatch.StartNew(); TimeSpan? last = null; int frames = 0; double worst = 0, windowStart = 0;
        void Frame(TimeSpan now)
        {
            if (last == null) global::Android.Util.Log.Info("X20CTL", $"startup: first frame at {started.ElapsedMilliseconds} ms after the view attached");
            else { worst = Math.Max(worst, (now - last.Value).TotalMilliseconds); frames++; }
            last = now;
            double at = started.Elapsed.TotalSeconds;
            if (at - windowStart >= 2)
            {
                global::Android.Util.Log.Info("X20CTL", $"frames: {at:0.0}s {frames / (at - windowStart):0} fps, longest gap {worst:0} ms");
                frames = 0; worst = 0; windowStart = at;
            }
            if (at < 30) top.RequestAnimationFrame(Frame);
        }
        top.RequestAnimationFrame(Frame);
    }
}
