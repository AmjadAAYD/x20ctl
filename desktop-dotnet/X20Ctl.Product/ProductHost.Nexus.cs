using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using X20Ctl.Product.Nexus;

namespace X20Ctl.Product;

public sealed partial class ProductHost
{
    public Grid? ActiveNexusNotice { get; private set; }

    public void ShowNexusThemeRestartNotice(string? themeName = null)
    {
        string chosen = themeName ?? ThemeCatalog.Find(preferencesHealthy ? preferences.Theme : ThemeCatalog.Current.Id).Name;
        DismissNexusNotice();

        var notice = NexusNotice.BuildNotice(
            title: $"Restart X20CTL to switch to {chosen}",
            bodyLines: new[]
            {
                $"X20CTL needs to restart to apply the {chosen} presentation.",
                $"App will reopen using {chosen} styles, colors and layouts.",
                "Unsaved controller drafts will be lost. Saved setups and PC mode settings stay on this PC.",
                "Closing the app now will apply the new presentation cleanly.",
                $"Restart X20CTL now to switch to {chosen}?"
            },
            primaryAction: "Restart now",
            cancelAction: "Cancel",
            onPrimary: () =>
            {
                DismissNexusNotice();
                if (!ReviewSandbox.Active) ThemeCatalog.Restart();
            },
            onCancel: () => DismissNexusNotice(),
            toasts: new[]
            {
                ("Update", $"{chosen} selected · Restart required to load presentation", "\uE777", "#107c10"),
                ("While restarting", "Current configuration will be saved automatically", "\uE896", "#2d6fd0"),
                ("After Yes", $"App reopens immediately in {chosen} mode", "\uE73E", "#107c10"),
                ("After Cancel", "Continues running in current presentation", "\uE711", "#5a5d63")
            });

        ShowNexusNotice(notice);
    }

    public void ShowNexusDriverNotice(IReadOnlyList<string>? names = null, Action<bool>? decided = null)
    {
        DismissNexusNotice();
        var list = names is { Count: > 0 } ? string.Join(", ", names) : "ViGEmBus, HidHide";

        var notice = NexusNotice.BuildNotice(
            title: "X20CTL · PC mode drivers",
            bodyLines: new[]
            {
                "PC mode needs two small drivers:",
                "ViGEmBus: the virtual Xbox controller that games see",
                "HidHide: hides the physical pad so games don't see it twice",
                "They are the signed installers from Nefarius Software Solutions, packaged with X20CTL. Windows will ask for permission and a setup window may flash by. You can remove them any time in Windows Apps.",
                "Install now?"
            },
            primaryAction: "Yes",
            cancelAction: "No",
            onPrimary: () =>
            {
                DismissNexusNotice();
                decided?.Invoke(true);
            },
            onCancel: () =>
            {
                DismissNexusNotice();
                decided?.Invoke(false);
            },
            toasts: new[]
            {
                ("Update", "V0.017 · Driver setup ready to install", "\uE777", "#107c10"),
                ("While it installs", "Windows may ask for administrator approval", "\uE896", "#2d6fd0"),
                ("After Yes", "Ready for any game · Virtual controller active", "\uE73E", "#107c10"),
                ("After No", "Virtual controller disabled · Local mode only", "\uE711", "#5a5d63")
            });

        ShowNexusNotice(notice);
    }

    private void ShowNexusNotice(Grid notice)
    {
        ActiveNexusNotice = notice;
        Children.Add(notice); ArmThemeNotice(notice);
        Panel.SetZIndex(notice, 60);
        _ = UpdateNativeContext();
    }

    public void DismissNexusNotice()
    {
        if (ActiveNexusNotice == null) return;
        var current = ActiveNexusNotice;
        ActiveNexusNotice = null;
        ReleaseThemeNotice(current); Children.Remove(current);
        _ = UpdateNativeContext();
    }
}
