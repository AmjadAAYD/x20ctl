using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using X20Ctl.Product.Sunrise;

namespace X20Ctl.Product;

public sealed partial class ProductHost
{
    public Canvas? ActiveSunriseNotice { get; private set; }

    public void ShowSunriseThemeRestartNotice(string? themeName = null)
    {
        string chosen = themeName ?? ThemeCatalog.Find(preferencesHealthy ? preferences.Theme : ThemeCatalog.Current.Id).Name;
        DismissSunriseNotice();

        var notice = new SunriseNotice(
            title: $"Restart to switch to {chosen}",
            lines: new[]
            {
                $"X20CTL needs to restart to apply the {chosen} visual theme.",
                "Unsaved controller drafts will be lost. Saved setups and PC mode settings stay on this PC.",
                $"Restart X20CTL now to switch to {chosen}?"
            },
            yesText: "Restart now",
            noText: "Cancel",
            onYes: () =>
            {
                DismissSunriseNotice();
                if (!ReviewSandbox.Active) ThemeCatalog.Restart();
            },
            onNo: () => DismissSunriseNotice(),
            notes: new[]
            {
                ("THEME SWITCH", $"Unsaved controller drafts will be lost. Saved setups stay safe."),
                ("AFTER RESTART", $"X20CTL reopens in {chosen} theme")
            });

        ShowSunriseNotice(notice);
    }

    public void ShowSunriseDriverNotice(IReadOnlyList<string>? names = null, Action<bool>? decided = null)
    {
        DismissSunriseNotice();
        var list = names is { Count: > 0 } ? string.Join(", ", names) : "ViGEmBus, HidHide";

        var notice = new SunriseNotice(
            title: "PC Mode Drivers Required",
            lines: new[]
            {
                "PC mode requires virtual Xbox controller drivers:",
                "ViGEmBus: virtual controller emulation layer",
                "HidHide: prevents double-input detection in games",
                "Install drivers now?"
            },
            yesText: "Install drivers",
            noText: "Cancel",
            onYes: () =>
            {
                DismissSunriseNotice();
                decided?.Invoke(true);
            },
            onNo: () =>
            {
                DismissSunriseNotice();
                decided?.Invoke(false);
            },
            notes: new[]
            {
                ("COMPONENTS", list),
                ("PROVENANCE", "Signed drivers by Nefarius Software Solutions")
            });

        ShowSunriseNotice(notice);
    }

    private void ShowSunriseNotice(SunriseNotice notice)
    {
        ActiveSunriseNotice = notice;
        Panel.SetZIndex(notice, 100);
        Children.Add(notice);
        ArmThemeNotice(notice);
    }

    public void DismissSunriseNotice()
    {
        if (ActiveSunriseNotice == null) return;
        ReleaseThemeNotice(ActiveSunriseNotice);
        Children.Remove(ActiveSunriseNotice);
        ActiveSunriseNotice = null;
        _ = UpdateNativeContext();
    }
}
