using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace X20Ctl.Product;

public sealed partial class ProductHost
{
    public PulseNotice? ActivePulseNotice { get; private set; }
    private Action<bool>? pulseNoticeDecision;

    private void ShowPulseSettings()
    {
        CloseSelector();
        CloseSettings();
        Zone.IsEnabled = false;
        if (Studio != null)
        {
            Studio.IsEnabled = false;
            Studio.Visibility = Visibility.Collapsed;
        }
        else
        {
            Zone.Visibility = Visibility.Collapsed;
        }
        Settings = new(OpenSupport, StartIntro, CloseSettings, OpenCheck, preferencesHealthy ? preferences : null, PcMode, pcModeStore, OpenWriteScan)
        {
            PulseNavigate = NavigatePulsePage,
            RestartRequested = RequestPulseThemeRestart
        };
        Children.Add(Settings);
        Panel.SetZIndex(Settings, 20);
        Fx.Appear(Settings, 1.02, 220);
        Settings.FocusFirst();
        _ = UpdateNativeContext();
    }

    internal void NavigatePulsePage(string target)
    {
        if (Check != null) CloseCheck();
        if (WriteScan != null) CloseWriteScan();
        if (target == "Zone")
        {
            CloseSettings();
            ReturnToZone();
            return;
        }
        if (target == "Tools")
        {
            ShowSettings();
            return;
        }
        if (target == "ReadScan")
        {
            if (Settings == null) ShowSettings();
            OpenCheck();
            return;
        }
        if (target == "WriteScan")
        {
            if (Settings == null) ShowSettings();
            OpenWriteScan();
            return;
        }
        CloseSettings();
        EnsurePulseStudio();
        Studio?.PulseSelectPage(target);
    }

    private void EnsurePulseStudio()
    {
        if (Studio != null) return;
        int player = studioPlayer ?? Zone.State.FocusedPlayer ?? 0;
        var assigned = Zone.State.Players[player];
        if (assigned.Model == null)
            Zone.AssignController(player, Catalog.Find("x20"));
        EnterStudio(player);
    }

    public void ShowPulseThemeRestartNotice(string? themeName = null)
    {
        string chosen = themeName ?? ThemeCatalog.Find(preferencesHealthy ? preferences.Theme : ThemeCatalog.Current.Id).Name;
        ShowPulseNotice(PulseNotice.ThemeRestart(chosen, yes =>
        {
            DismissPulseNotice(false);
            if (yes && !ReviewSandbox.Active) ThemeCatalog.Restart();
        }));
    }

    public void ShowPulseDriverNotice(IReadOnlyList<string>? names = null, Action<bool>? decided = null)
    {
        var list = names is { Count: > 0 } ? names : ["virtual controller driver", "controller hider"];
        ShowPulseNotice(PulseNotice.Drivers(list, yes =>
        {
            DismissPulseNotice(false);
            decided?.Invoke(yes);
        }));
    }

    internal bool ConfirmPulseDrivers(IReadOnlyList<string> names)
    {
        if (ReviewSandbox.Active)
        {
            ShowPulseDriverNotice(names);
            return false;
        }
        bool accepted = false;
        var frame = new DispatcherFrame();
        ShowPulseDriverNotice(names, yes =>
        {
            accepted = yes;
            frame.Continue = false;
        });
        Dispatcher.PushFrame(frame);
        return accepted;
    }

    private void RequestPulseThemeRestart()
    {
        string chosen = preferencesHealthy ? preferences.Theme : ThemeCatalog.Current.Id;
        ShowPulseThemeRestartNotice(ThemeCatalog.Find(chosen).Name);
    }

    private void ShowPulseNotice(PulseNotice notice)
    {
        DismissPulseNotice(false);
        ActivePulseNotice = notice;
        pulseNoticeDecision = yes =>
        {
            var button = yes ? notice.Primary : notice.Secondary;
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        };
        Children.Add(notice);
        Panel.SetZIndex(notice, 60);
        Fx.Appear(notice, 1.02, 180);
        notice.Primary.Focus();
        _ = UpdateNativeContext();
    }

    public void DismissPulseNotice(bool invokeSecondary = true)
    {
        if (ActivePulseNotice == null) return;
        var current = ActivePulseNotice;
        var decide = pulseNoticeDecision;
        ActivePulseNotice = null;
        pulseNoticeDecision = null;
        Children.Remove(current);
        if (invokeSecondary) decide?.Invoke(false);
        _ = UpdateNativeContext();
    }

    private bool HandlePulseNoticeKey(KeyEventArgs e)
    {
        if (ActivePulseNotice == null) return false;
        if (e.Key is Key.Escape or Key.B)
        {
            DismissPulseNotice(true);
            e.Handled = true;
            return true;
        }
        if (e.Key is Key.Enter or Key.A)
        {
            var button = Keyboard.FocusedElement as Button ?? ActivePulseNotice.Primary;
            if (!e.IsRepeat && button.IsEnabled) button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            e.Handled = true;
            return true;
        }
        if (e.Key is Key.Left or Key.Up)
        {
            ActivePulseNotice.Secondary.Focus();
            e.Handled = true;
            return true;
        }
        if (e.Key is Key.Right or Key.Down)
        {
            ActivePulseNotice.Primary.Focus();
            e.Handled = true;
            return true;
        }
        e.Handled = true;
        return true;
    }
}
