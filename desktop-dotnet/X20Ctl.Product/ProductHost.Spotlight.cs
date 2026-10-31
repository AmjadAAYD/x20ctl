using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using X20Ctl.Product.Spotlight;

namespace X20Ctl.Product;

public sealed partial class ProductHost
{
    public Grid? ActiveSpotlightNotice { get; private set; }
    private Action<bool>? spotlightNoticeDecision;

    public void ShowSpotlightThemeRestartNotice(string? themeName = null)
    {
        string chosen = themeName ?? ThemeCatalog.Find(preferencesHealthy ? preferences.Theme : ThemeCatalog.Current.Id).Name;
        DismissSpotlightNotice(false);

        var notice = SpotlightNotice.CreateNotice(
            pill: "THEME · RESTART",
            title: $"Restart X20CTL to switch to {chosen}",
            lead: "The new theme will take effect on launch.",
            numberedSteps: new[]
            {
                $"X20CTL will reopen using the {chosen} presentation.",
                "Unsaved controller drafts will be lost. Saved setups and PC mode settings stay on this PC."
            },
            actionNote: "Closing the app now will apply the new presentation.",
            callout: "RESTART X20CTL NOW?",
            noText: "Cancel",
            yesText: "Restart",
            onNo: () => DismissSpotlightNotice(false),
            onYes: () =>
            {
                DismissSpotlightNotice(false);
                if (!ReviewSandbox.Active) ThemeCatalog.Restart();
            },
            footerStates: new[]
            {
                ("Preference", $"{chosen} saved"),
                ("On restart", "Theme applied to whole app"),
                ("After yes", "App reopens immediately"),
                ("After cancel", "Stays in current theme")
            });

        ShowSpotlightNotice(notice);
    }

    public void ShowSpotlightDriverNotice(IReadOnlyList<string>? names = null, Action<bool>? decided = null)
    {
        DismissSpotlightNotice(false);
        var list = names is { Count: > 0 } ? string.Join(", ", names) : "ViGEmBus, HidHide";

        var notice = SpotlightNotice.CreateNotice(
            pill: "DRIVERS · PERMISSION",
            title: "PC mode requires controller drivers",
            lead: "The system needs virtual bus components to translate mappings.",
            numberedSteps: new[]
            {
                $"Required drivers: {list}.",
                "Windows will show a standard User Account Control prompt for installation."
            },
            actionNote: "Without these components, PC mode cannot create virtual controller devices.",
            callout: "INSTALL REQUIRED DRIVERS?",
            noText: "Cancel",
            yesText: "Install",
            onNo: () =>
            {
                DismissSpotlightNotice(false);
                decided?.Invoke(false);
            },
            onYes: () =>
            {
                DismissSpotlightNotice(false);
                decided?.Invoke(true);
            },
            footerStates: new[]
            {
                ("Status", "Driver setup needed"),
                ("During install", "UAC prompt will appear"),
                ("After install", "PC mode becomes active"),
                ("If declined", "PC mode stays off")
            });

        ShowSpotlightNotice(notice);
    }

    private void ShowSpotlightNotice(Grid notice)
    {
        ActiveSpotlightNotice = notice;
        Children.Add(notice); ArmThemeNotice(notice);
        Panel.SetZIndex(notice, 60);
        _ = UpdateNativeContext();
    }

    public void DismissSpotlightNotice(bool invokeSecondary = true)
    {
        if (ActiveSpotlightNotice == null) return;
        var current = ActiveSpotlightNotice;
        ActiveSpotlightNotice = null;
        ReleaseThemeNotice(current); Children.Remove(current);
        _ = UpdateNativeContext();
    }

    internal void NavigateSpotlightPage(string target)
    {
        if (Check != null) { CloseCheck(); if (Check != null) return; }
        if (WriteScan != null) { CloseWriteScan(); if (WriteScan != null) return; }
        if (target == "Zone")
        {
            CloseSettings();
            ReturnToZone();
            return;
        }
        if (target == "Tools" || target == "tools")
        {
            ShowSettings();
            return;
        }
        if (target == "ReadScan" || target == "check")
        {
            if (Settings == null) ShowSettings();
            OpenCheck();
            return;
        }
        if (target == "WriteScan" || target == "writescan")
        {
            if (Settings == null) ShowSettings();
            OpenWriteScan();
            return;
        }
        CloseSettings();
        EnsureSpotlightStudio();
        Studio?.SpotlightSelectPage(target);
    }

    private void EnsureSpotlightStudio()
    {
        if (Studio != null) return;
        int player = studioPlayer ?? Zone.State.FocusedPlayer ?? 0;
        var assigned = Zone.State.Players[player];
        if (assigned.Model == null)
            { OpenSelector(player); return; }
        EnterStudio(player);
    }
}
