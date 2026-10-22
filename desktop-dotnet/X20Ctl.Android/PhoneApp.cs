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
            if (TopLevel.GetTopLevel(this) is { } top) top.BackRequested += (_, e) => e.Handled = view.Back();
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
