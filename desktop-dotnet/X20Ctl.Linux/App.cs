using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
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
            if (desktop.Args is not { Length: 2 } || desktop.Args[0] != "--shots") AddTray(desktop, window);
            if (desktop.Args is { Length: 2 } a && a[0] == "--shots") { ReviewMode.HideLibrary = true; window.Opened += async (_, _) => await window.Shots(a[1]); }
        }
        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>The tray icon: bring the window back, turn the controller-as-mouse on or off, quit.</summary>
    void AddTray(IClassicDesktopStyleApplicationLifetime desktop, Window window)
    {
        try
        {
            var open = new NativeMenuItem("Open X20CTL"); open.Click += (_, _) => { window.Show(); window.WindowState = WindowState.Normal; window.Activate(); };
            var mouse = new NativeMenuItem("Controller as mouse") { ToggleType = MenuItemToggleType.CheckBox, IsChecked = MouseHub.Shared.Enabled };
            mouse.Click += (_, _) => MouseHub.Shared.SetEnabled(!MouseHub.Shared.Enabled);
            MouseHub.Shared.Changed += () => Avalonia.Threading.Dispatcher.UIThread.Post(() => mouse.IsChecked = MouseHub.Shared.Enabled);
            var quit = new NativeMenuItem("Quit"); quit.Click += (_, _) => desktop.Shutdown();
            var menu = new NativeMenu { open, mouse, new NativeMenuItemSeparator(), quit };
            var icon = new TrayIcon { ToolTipText = "X20CTL", Menu = menu, Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://x20ctl-studio/Assets/x20ctl-icon.png"))) };
            icon.Clicked += (_, _) => { window.Show(); window.Activate(); };
            TrayIcon.SetIcons(this, [icon]);
        }
        catch (Exception e) when (e is InvalidOperationException or IOException or NotSupportedException) { }
    }
}
