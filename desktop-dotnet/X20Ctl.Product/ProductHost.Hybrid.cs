using System.Windows.Controls;
using System.Windows;
using System.Windows.Threading;

namespace X20Ctl.Product;

public sealed partial class ProductHost
{
    private void ShowHybridSettings()
    {
        CloseSettings();
        if (Studio != null) Studio.IsEnabled = false;
        Zone.IsEnabled = false;
        Settings = new(OpenSupport, StartIntro, CloseSettings, OpenCheck, preferencesHealthy ? preferences : null, PcMode, pcModeStore, OpenWriteScan);
        Settings.RebelNavigate = NavigateRebelPage;
        Settings.RestartRequested = () => { if (HasUnsavedThemeDrafts) Settings?.ConfirmHybridRestart(ThemeCatalog.Restart); else ThemeCatalog.Restart(); };
        Children.Add(Settings); Panel.SetZIndex(Settings, 20); Fx.Appear(Settings, 1.04, 320); Settings.FocusFirst();
        _ = UpdateNativeContext();
    }
    internal HybridNotice? ActiveHybridNotice => Children.OfType<HybridNotice>().LastOrDefault();
    internal void ShowHybridNotice(string title, string summary, IReadOnlyList<string> lines, string question, string yes, Action<bool> answer)
    {
        if (ActiveHybridNotice != null) return;
        var prior = System.Windows.Input.Keyboard.FocusedElement;
        HybridNotice? notice = null;
        notice = new(title, summary, lines, question, yes, accepted => { Children.Remove(notice!); _ = UpdateNativeContext(); if (prior is UIElement element && element.IsVisible && element.IsEnabled) element.Focus(); answer(accepted); });
        Children.Add(notice); Panel.SetZIndex(notice, 100); _ = UpdateNativeContext(); notice.No.Focus();
    }
    internal bool ConfirmHybridDrivers(IReadOnlyList<string> names)
    {
        if (ActiveHybridNotice != null) return false;
        bool accepted = false; var frame = new DispatcherFrame();
        ShowHybridNotice("PC mode drivers", names.Count == 1 ? "PC mode needs a small driver." : "PC mode needs two small drivers.", names.Select(name => name == "virtual controller driver" ? "ViGEmBus provides the virtual Xbox controller." : "HidHide prevents duplicate physical input.").ToArray(), "Install the packaged signed drivers? Windows will ask for permission.", "Install", choice => { accepted = choice; frame.Continue = false; });
        Dispatcher.PushFrame(frame); return accepted;
    }
}
