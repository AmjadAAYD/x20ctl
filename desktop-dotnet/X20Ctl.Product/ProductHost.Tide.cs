using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using X20Ctl.Product.Tide;

namespace X20Ctl.Product;

public sealed partial class ProductHost
{
    public Canvas? ActiveTideNotice { get; private set; }

    public void ShowTideThemeRestartNotice(string? themeName = null)
    {
        string chosen = themeName ?? ThemeCatalog.Find(preferencesHealthy ? preferences.Theme : ThemeCatalog.Current.Id).Name;
        DismissTideNotice();

        var notice = new TideNotice(
            title: $"Restart X20CTL to switch to {chosen}",
            lines: new[]
            {
                $"X20CTL needs to restart to apply the {chosen} presentation.",
                $"App will reopen using {chosen} styles, colors and layouts.",
                "Unsaved controller drafts will be lost. Saved setups and PC mode settings stay on this PC.",
                "Closing the app now will apply the new presentation cleanly.",
                $"Restart X20CTL now to switch to {chosen}?"
            },
            yesText: "Restart now",
            noText: "Cancel",
            onYes: () =>
            {
                DismissTideNotice();
                if (!ReviewSandbox.Active) ThemeCatalog.Restart();
            },
            onNo: () => DismissTideNotice(),
            notes: new[]
            {
                ("THEME SWITCH", $"Unsaved controller drafts will be lost. Saved setups and PC mode settings stay on this PC."),
                ("WHILE RESTARTING", "Only previously saved setups survive restart"),
                ("AFTER YES", $"X20CTL reopens in {chosen} theme"),
                ("AFTER NO", "Presentation unchanged")
            });

        ShowTideNotice(notice);
    }

    public void ShowTideDriverNotice(IReadOnlyList<string>? names = null, Action<bool>? decided = null)
    {
        DismissTideNotice();
        var list = names is { Count: > 0 } ? string.Join(", ", names) : "ViGEmBus, HidHide";

        var notice = new TideNotice(
            title: "X20CTL · PC mode drivers",
            lines: new[]
            {
                "PC mode needs two small drivers:",
                "ViGEmBus: the virtual Xbox controller that games see",
                "HidHide: hides the physical pad so games don't see it twice",
                "They are the signed installers from Nefarius Software Solutions, packaged with X20CTL. Windows will ask for permission and a setup window may flash by. You can remove them any time in Windows Apps.",
                "Install now?"
            },
            yesText: "Yes",
            noText: "No",
            onYes: () =>
            {
                DismissTideNotice();
                decided?.Invoke(true);
            },
            onNo: () =>
            {
                DismissTideNotice();
                decided?.Invoke(false);
            },
            notes: new[]
            {
                ("UPDATE", "Driver installation requires permission"),
                ("WHILE IT INSTALLS", "Setting up PC mode. Windows may ask for permission…"),
                ("AFTER YES", "Installation starts only after permission; the result is checked before PC mode starts."),
                ("AFTER NO", "PC mode drivers were not installed. PC mode stays off until you allow them.")
            });

        ShowTideNotice(notice);
    }

    private void ShowTideNotice(Canvas notice)
    {
        ActiveTideNotice = notice;
        Panel.SetZIndex(notice, 100);
        Children.Add(notice); ArmThemeNotice(notice);
    }

    public void DismissTideNotice()
    {
        if (ActiveTideNotice != null)
        {
            ReleaseThemeNotice(ActiveTideNotice); Children.Remove(ActiveTideNotice);
            ActiveTideNotice = null; _ = UpdateNativeContext();
        }
    }
}
