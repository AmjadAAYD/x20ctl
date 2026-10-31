using System.Windows.Threading;

namespace X20Ctl.Product;

public sealed partial class ProductHost
{
    internal void NavigateThemePage(string target)
    {
        if (Check != null) { CloseCheck(); if (Check != null) return; }
        if (WriteScan != null) { CloseWriteScan(); if (WriteScan != null) return; }
        switch (target.ToLowerInvariant())
        {
            case "zone": CloseSettings(); ReturnToZone(); return;
            case "tools": if (Settings == null) ShowSettings(); else { Settings.ShowThemes(false); Settings.ShowAbout(false); Settings.ShowFeedback(false); Settings.ShowPcMode(false); } return;
            case "readscan": case "check": if (Settings == null) ShowSettings(); OpenCheck(); return;
            case "writescan": if (Settings == null) ShowSettings(); OpenWriteScan(); return;
        }
        CloseSettings();
        if (Studio == null)
        {
            int player = studioPlayer ?? Zone.State.FocusedPlayer ?? 0;
            if (Zone.State.Players[player].Model == null) { OpenSelector(player); return; }
            EnterStudio(player);
        }
        Dispatcher.BeginInvoke(() =>
        {
            switch (target.ToLowerInvariant())
            {
                case "buttons": Studio?.ShowButtons(); break;
                case "curves": Studio?.ShowCurves(true); break;
                case "macros": Studio?.ShowMacros(true); break;
                case "vibration": Studio?.ShowVibration(true); break;
                case "device": Studio?.ShowManagement("Device"); break;
                case "pcmode": Studio?.ShowManagement("PcMode"); break;
                case "mouse": Studio?.ShowManagement("Mouse"); break;
                case "setups": Studio?.ShowManagement("Setups"); break;
                case "tester": Studio?.ShowManagement("Tester"); break;
                case "diagnostics": Studio?.ShowManagement("Diagnostics"); break;
                case "dashboard": Studio?.ShowManagement("Dashboard"); break;
            }
        }, DispatcherPriority.Loaded);
    }

    private void RequestCurrentThemeRestart()
    {
        switch (ThemeCatalog.Current.Id)
        {
            case "spotlight": ShowSpotlightThemeRestartNotice(); break;
            case "nexus": ShowNexusThemeRestartNotice(); break;
            case "tide": ShowTideThemeRestartNotice(); break;
            case "dusk": ShowDuskThemeRestartNotice(); break;
            case "volt": ShowVoltThemeRestartNotice(); break;
            case "blossom": ShowBlossomThemeRestartNotice(); break;
            default: if (!ReviewSandbox.Active) ThemeCatalog.Restart(); break;
        }
    }
}
