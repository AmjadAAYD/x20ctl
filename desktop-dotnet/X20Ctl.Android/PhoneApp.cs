using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using X20Ctl.Linux;

namespace X20Ctl.Droid;

/// <summary>The Studio on a phone: the same <see cref="MainView"/> the desktop window holds, laid out at a desktop-like
/// size and scaled to fit the screen, so every screen keeps the Windows app's proportions.</summary>
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
/// Holds the app at <see cref="DesignHeight"/> units tall and as wide as the screen's shape allows (at least the desktop
/// window's minimum width), then scales it to the screen. Back goes one screen up, as Escape does on the desktop.
/// </summary>
internal sealed class PhoneHost : Border
{
    const double DesignHeight = 680, MinWidth_ = 1100;
    readonly MainView view = new();
    readonly Viewbox box;

    public PhoneHost()
    {
        Background = new SolidColorBrush(T.Void);
        box = new Viewbox { Stretch = Stretch.Uniform, Child = view };
        Child = box;
        AttachedToVisualTree += (_, _) =>
        {
            if (TopLevel.GetTopLevel(this) is not { } top) return;
            top.BackRequested += (_, e) => e.Handled = view.Back();
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
        if (available.Width > 0 && available.Height > 0 && !double.IsInfinity(available.Width) && !double.IsInfinity(available.Height))
        {
            view.Height = DesignHeight;
            view.Width = Math.Max(MinWidth_, DesignHeight * available.Width / available.Height);
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
