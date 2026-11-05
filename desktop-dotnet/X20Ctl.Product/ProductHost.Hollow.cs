using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using X20Ctl.Product.Hollow;

namespace X20Ctl.Product;

public sealed partial class ProductHost
{
    public Canvas? ActiveHollowNotice { get; private set; }

    public void ShowHollowThemeRestartNotice(string? themeName = null)
    {
        string chosen = themeName ?? ThemeCatalog.Find(preferencesHealthy ? preferences.Theme : ThemeCatalog.Current.Id).Name;
        DismissHollowNotice();

        var notice = new HollowNotice(
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
                DismissHollowNotice();
                if (!ReviewSandbox.Active) ThemeCatalog.Restart();
            },
            onNo: () => DismissHollowNotice(),
            notes: new[]
            {
                ("THEME SWITCH", $"Unsaved controller drafts will be lost. Saved setups stay safe."),
                ("AFTER RESTART", $"X20CTL reopens in {chosen} theme")
            });

        ShowHollowNotice(notice);
    }

    public void ShowHollowDriverNotice(IReadOnlyList<string>? names = null, Action<bool>? decided = null)
    {
        DismissHollowNotice();
        var list = names is { Count: > 0 } ? string.Join(", ", names) : "ViGEmBus, HidHide";

        var notice = new HollowNotice(
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
                DismissHollowNotice();
                decided?.Invoke(true);
            },
            onNo: () =>
            {
                DismissHollowNotice();
                decided?.Invoke(false);
            },
            notes: new[]
            {
                ("COMPONENTS", list),
                ("PROVENANCE", "Signed drivers by Nefarius Software Solutions")
            });

        ShowHollowNotice(notice);
    }

    private void ShowHollowNotice(HollowNotice notice)
    {
        ActiveHollowNotice = notice;
        Panel.SetZIndex(notice, 100);
        Children.Add(notice);
        ArmThemeNotice(notice);
    }

    public void DismissHollowNotice()
    {
        if (ActiveHollowNotice == null) return;
        ReleaseThemeNotice(ActiveHollowNotice);
        Children.Remove(ActiveHollowNotice);
        ActiveHollowNotice = null;
        _ = UpdateNativeContext();
    }
}
