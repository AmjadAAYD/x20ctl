using System.Windows;
using System.Windows.Controls;
using X20Ctl.Product.Rebel;
using System.Windows.Threading;
namespace X20Ctl.Product;

public sealed partial class ProductHost
{
    private void ShowRebelSettings()
    {
        CloseSettings();
        var origin = Studio;
        if (origin != null) origin.IsEnabled = false;
        Zone.IsEnabled = false;
        Settings = new(OpenSupport, StartIntro, CloseSettings, OpenCheck, preferencesHealthy ? preferences : null, PcMode, pcModeStore, OpenWriteScan);
        Settings.RestartRequested = RequestRebelThemeRestart;
        Settings.RebelReturnToStudio = origin != null;
        Settings.RebelNavigate = NavigateRebelPage;
        Children.Add(Settings); Panel.SetZIndex(Settings, 20); Fx.Appear(Settings, 1.04, 320); Settings.FocusFirst();
        _ = UpdateNativeContext();
    }
    private void NavigateRebelPage(string key)
    {
        CloseSettings();
        if (Studio == null)
        {
            int player = Zone.State.FocusedPlayer ?? (studioPlayer is >= 0 ? studioPlayer.Value : 0);
            if (Zone.State.Players[player].Model == null) return;
            EnterStudio(player);
        }
        Dispatcher.BeginInvoke(() => Studio?.RebelGoPage(key), DispatcherPriority.Loaded);
    }
    internal RebelNotice? ActiveRebelNotice => Children.OfType<RebelNotice>().LastOrDefault();
    internal bool HasUnsavedThemeDrafts => drafts.Values.Any(d => !d.PcMode && d.UnsentChanges > 0) || curves.Values.Any(d => !d.PcMode && d.ModifiedChannels > 0) || macros.Values.Any(d => !d.PcMode && d.ModifiedSlots > 0) || vibrations.Values.Any(d => d.Modified);
    internal void ShowRebelNotice(string title, string summary, IReadOnlyList<string> lines, string question, string yes, Action<bool> answer)
    {
        if (ActiveRebelNotice != null) return;
        var prior = System.Windows.Input.Keyboard.FocusedElement;
        RebelNotice? notice = null;
        notice = new(title, summary, lines, question, yes, accepted =>
        {
            Children.Remove(notice!); _ = UpdateNativeContext();
            if (prior is UIElement element && element.IsVisible && element.IsEnabled) element.Focus();
            answer(accepted);
        });
        Children.Add(notice); Panel.SetZIndex(notice, 100); _ = UpdateNativeContext(); notice.No.Focus();
    }
    internal void RequestRebelThemeRestart()
    {
        if (!HasUnsavedThemeDrafts) { ThemeCatalog.Restart(); return; }
        ShowRebelNotice("Restart X20CTL?", "Some controller drafts are unsaved.", ["Unsaved button, curve, macro and vibration drafts will be lost.", "Saved setups and PC mode settings stay on this PC."], "Go back to save your work, or restart into the selected theme.", "Restart", accepted => { if (accepted) ThemeCatalog.Restart(); });
    }
    internal bool ConfirmRebelDrivers(IReadOnlyList<string> names)
    {
        if (ActiveRebelNotice != null) return false;
        bool accepted = false; var frame = new DispatcherFrame();
        ShowRebelNotice("PC mode drivers", names.Count == 1 ? "PC mode needs a small driver." : "PC mode needs two small drivers.", names.Select(name => name == "virtual controller driver" ? "ViGEmBus: the virtual Xbox controller that games see" : "HidHide: hides the physical pad so games don’t see it twice").ToArray(), "They are the signed installers from Nefarius Software Solutions, packaged with X20CTL. Windows will ask for permission and a setup window may flash by. You can remove them any time in Windows Apps.", "Install now?", choice => { accepted = choice; frame.Continue = false; });
        Dispatcher.PushFrame(frame); return accepted;
    }
}
