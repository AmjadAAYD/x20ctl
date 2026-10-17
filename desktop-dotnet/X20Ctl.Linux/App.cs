using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
namespace X20Ctl.Linux;

public sealed class App : Application
{
    public override void Initialize()
    {
        RequestedThemeVariant = ThemeVariant.Dark;
        Styles.Add(new FluentTheme());
    }
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
            var window = new MainWindow(); desktop.MainWindow = window;
            if (desktop.Args is { Length: 2 } a && a[0] == "--shots") window.Opened += async (_, _) => await window.Shots(a[1]);
        }
        base.OnFrameworkInitializationCompleted();
    }
}
