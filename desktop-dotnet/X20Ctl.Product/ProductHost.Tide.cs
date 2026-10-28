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
        if (ReviewSandbox.Active)
        {
            ShowTideDriverNotice();
            return;
        }

        string chosen = themeName ?? ThemeCatalog.Find(preferencesHealthy ? preferences.Theme : ThemeCatalog.Current.Id).Name;
        DismissTideNotice();

        var notice = new TideNotice(
            title: $"Restart X20CTL to switch to {chosen}",
            lines: new[]
            {
                $"X20CTL needs to restart to apply the {chosen} presentation.",
                $"App will reopen using {chosen} styles, colors and layouts.",
                "Your working drafts, saved setups and controller mappings stay as they are.",
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
                ("THEME SWITCH", $"{chosen} selected · Local drafts preserved"),
                ("WHILE RESTARTING", "Current setup written to local disk"),
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
                ("UPDATE", "Update available · V0.017"),
                ("WHILE IT INSTALLS", "Setting up PC mode. Windows may ask for permission…"),
                ("AFTER YES", "Installed. PC mode starts by itself once you change a setting."),
                ("AFTER NO", "PC mode drivers were not installed. PC mode stays off until you allow them.")
            });

        ShowTideNotice(notice);
    }

    private void ShowTideNotice(Canvas notice)
    {
        ActiveTideNotice = notice;
        Panel.SetZIndex(notice, 100);
        Children.Add(notice);
    }

    public void DismissTideNotice()
    {
        if (ActiveTideNotice != null)
        {
            Children.Remove(ActiveTideNotice);
            ActiveTideNotice = null;
        }
    }
}
