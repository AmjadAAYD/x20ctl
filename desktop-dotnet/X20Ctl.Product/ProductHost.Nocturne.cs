using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using X20Ctl.Product.Nocturne;

namespace X20Ctl.Product;

public sealed partial class ProductHost
{
    public Canvas? ActiveNocturneNotice { get; private set; }

    public void ShowNocturneThemeRestartNotice(string? themeName = null)
    {
        string chosen = themeName ?? ThemeCatalog.Find(preferencesHealthy ? preferences.Theme : ThemeCatalog.Current.Id).Name;
        DismissNocturneNotice();

        var notice = new NocturneNotice(
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
                DismissNocturneNotice();
                if (!ReviewSandbox.Active) ThemeCatalog.Restart();
            },
            onNo: () => DismissNocturneNotice(),
            notes: new[]
            {
                ("THEME SWITCH", $"Unsaved controller drafts will be lost. Saved setups stay safe."),
                ("AFTER RESTART", $"X20CTL reopens in {chosen} theme")
            });

        ShowNocturneNotice(notice);
    }

    public void ShowNocturneDriverNotice(IReadOnlyList<string>? names = null, Action<bool>? decided = null)
    {
        DismissNocturneNotice();
        var list = names is { Count: > 0 } ? string.Join(", ", names) : "ViGEmBus, HidHide";

        var notice = new NocturneNotice(
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
                DismissNocturneNotice();
                decided?.Invoke(true);
            },
            onNo: () =>
            {
                DismissNocturneNotice();
                decided?.Invoke(false);
            },
            notes: new[]
            {
                ("COMPONENTS", list),
                ("PROVENANCE", "Signed drivers by Nefarius Software Solutions")
            });

        ShowNocturneNotice(notice);
    }

    private void ShowNocturneNotice(NocturneNotice notice)
    {
        ActiveNocturneNotice = notice;
        Panel.SetZIndex(notice, 100);
        Children.Add(notice);
        ArmThemeNotice(notice);
    }

    public void DismissNocturneNotice()
    {
        if (ActiveNocturneNotice == null) return;
        ReleaseThemeNotice(ActiveNocturneNotice);
        Children.Remove(ActiveNocturneNotice);
        ActiveNocturneNotice = null;
        _ = UpdateNativeContext();
    }
}
