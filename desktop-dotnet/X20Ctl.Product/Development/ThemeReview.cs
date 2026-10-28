using System.IO;
using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace X20Ctl.Product.Development;

/// <summary>One theme applied, its pages rendered (--review-themes DIRECTORY THEME). Nothing is shown: the interface is
/// hosted in a hidden HwndSource, so it loads and lays out as in the app, and each picture is the element's own render,
/// never the screen.</summary>
internal static class ThemeReview
{
    public static async Task Run(string directory, string id)
    {
        Directory.CreateDirectory(directory);
        ReviewSandbox.Active = true; ReviewSandbox.HideLibrary = true;
        var theme = ThemeCatalog.Find(id); if (theme.Id != id) throw new ArgumentException("Unknown theme " + id);
        ThemeCatalog.Apply(theme);
        var bindings = new StringWriter(); using var listener = new TextWriterTraceListener(bindings);
        PresentationTraceSources.DataBindingSource.Listeners.Add(listener); PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
        var shots = new List<string>();
        var host = new ProductHost(new ZoneScene(true), Path.Combine(directory, "assignments-fixture.json")) { Background = DS.Brush("Ink") };
        var stage = new DesignStage { Child = host };
        using var source = new HwndSource(new HwndSourceParameters("x20ctl theme review") { WindowStyle = unchecked((int)0x80000000), Width = 1600, Height = 900, PositionX = -32000, PositionY = -32000 }) { RootVisual = stage };
        try
        {
            await Shot("1-zone");
            host.Zone.AssignController(0, host.Catalog.Get("x20")); host.Zone.AssignController(1, host.Catalog.Get("x15")); await host.Zone.FocusPlayerAsync(null); await Shot("2-zone-player");
            await host.Zone.FocusPlayerAsync(0); await Shot("2b-hero"); await host.Zone.FocusPlayerAsync(null);
            host.EnterStudio(0); await Shot("3-dashboard", 1400);
            var studio = host.Studio!;
            studio.ShowButtons(); studio.SelectControl("A"); await Shot("4-buttons");
            studio.ShowCurves(true); await Shot("5-curves");
            studio.ShowMacros(true); if (studio.Macros is { } macros && macros.Draft.CanEdit && macros.Draft[macros.Slot].Count == 0) { for (int i = 0; i < 3; i++) macros.Add(); macros.Select(1); } await Shot("6-macros");
            studio.ShowVibration(true); await Shot("7-vibration");
            studio.ShowManagement("Device"); await Shot("8-device");
            foreach (var (page, shot) in new[] { ("PcMode", "8b-pcmode"), ("Mouse", "8c-mouse"), ("Setups", "8d-setups"), ("Tester", "8e-tester"), ("Diagnostics", "8f-diagnostics") })
            {
                studio.ShowManagement(page);
                if (page == "Setups" && studio.Setups is { } setups && setups.Cards.Count == 0) { for (int i = 0; i < 3; i++) setups.Create(false); setups.Select(setups.Store.Items[0].Id); }
                await Shot(shot);
            }
            studio.ShowCurves(true); studio.OpenStickTools(); await Shot("8g-advanced"); studio.CloseStickTools();
            studio.OpenAgent(); await Shot("8h-agent-empty");
            await studio.AgentWindow!.Ask("what does the dead zone do"); await Shot("8i-agent-answer");
            studio.AgentWindow.ShowSettingsPage(true); await Shot("8j-agent-settings"); studio.AgentWindow.ShowSettingsPage(false);
            studio.CloseAgent();
            host.ReturnToZone(); host.ShowSettings(); await Shot("9-tools");
            host.Settings!.ShowPcMode(true); await Shot("9b-tools-pcmode"); host.Settings.ShowPcMode(false);
            host.Settings!.ShowThemes(true); await Shot("10-themes"); host.Settings.ShowThemes(false);
            host.Settings.ShowAbout(true); await Shot("11-about"); host.Settings.ShowAbout(false);
            host.Settings.ShowFeedback(true); await Shot("11b-feedback"); host.Settings.ShowFeedback(false);
            host.CloseSettings();
            host.OpenSelector(2); await Shot("11c-choose"); host.CloseSelector();
            host.OpenCheck(); await Shot("11d-check"); host.CloseCheck();
            host.OpenWriteScan(); await Shot("11e-writescan");
            host.WriteScan!.ReviewOpen(1, new X20Ctl.Zone.Capture.TestPlan("X20", "KeyLinker", "Vibration strength", "20", "30")); await Shot("12c-writescan-plan");
            host.CloseWriteScan();

            if (id == "pulse")
            {
                host.ShowPulseThemeRestartNotice("Pulse");
                await Shot("13-notice-restart");
                host.DismissPulseNotice();
                host.ShowPulseDriverNotice(new[] { "ViGEmBus", "HidHide" });
                await Shot("14-notice-drivers");
                host.DismissPulseNotice();
            }
            if (id == "spotlight")
            {
                host.ShowSpotlightThemeRestartNotice("Spotlight");
                await Shot("13-notice-restart");
                host.DismissSpotlightNotice();
                host.ShowSpotlightDriverNotice(new[] { "ViGEmBus", "HidHide" });
                await Shot("14-notice-drivers");
                host.DismissSpotlightNotice();
            }
            if (id == "nexus")
            {
                host.ShowNexusThemeRestartNotice("Nexus");
                await Shot("13-notice-restart");
                host.DismissNexusNotice();
                host.ShowNexusDriverNotice(new[] { "ViGEmBus", "HidHide" });
                await Shot("14-notice-drivers");
                host.DismissNexusNotice();
            }
            if (id == "tide")
            {
                host.ShowTideThemeRestartNotice("Tide");
                await Shot("13-notice-restart");
                host.DismissTideNotice();
                host.ShowTideDriverNotice(new[] { "ViGEmBus", "HidHide" });
                await Shot("14-notice-drivers");
                host.DismissTideNotice();
            }
            if (id == "hybrid")
            {
                host.ShowHybridThemeRestartNotice("Hybrid");
                await Shot("13-notice-restart");
                host.DismissHybridNotice();
                host.ShowHybridDriverNotice(new[] { "ViGEmBus", "HidHide" });
                await Shot("14-notice-drivers");
                host.DismissHybridNotice();
            }
            if (id == "dusk")
            {
                host.ShowDuskThemeRestartNotice("Dusk");
                await Shot("13-notice-restart");
                host.DismissDuskNotice();
                host.ShowDuskDriverNotice(new[] { "ViGEmBus", "HidHide" });
                await Shot("14-notice-drivers");
                host.DismissDuskNotice();
            }
            if (id == "volt")
            {
                host.ShowVoltThemeRestartNotice("Volt");
                await Shot("13-notice-restart");
                host.DismissVoltNotice();
                host.ShowVoltDriverNotice(new[] { "ViGEmBus", "HidHide" });
                await Shot("14-notice-drivers");
                host.DismissVoltNotice();
            }
        }
        finally { PresentationTraceSources.DataBindingSource.Listeners.Remove(listener); }
        listener.Flush(); File.WriteAllText(Path.Combine(directory, "bindings.txt"), bindings.ToString());
        string Hex(string key) => DS.Brush(key) is SolidColorBrush b ? b.Color.ToString() : DS.Brush(key).GetType().Name;
        File.WriteAllText(Path.Combine(directory, "review.json"), JsonSerializer.Serialize(new
        {
            theme = theme.Id, windowShown = false, shots, bindingErrors = bindings.ToString().Length,
            resolved = new { text = Hex("DS.Text"), accent = Hex("DS.Accent"), backdrop = Hex("Zone.Backdrop"), ink = Hex("Ink"), display = DS.Display.Source, body = DS.Body.Source },
            frozen = new { text = DS.Brush("DS.Text").IsFrozen, glass = DS.Brush("DS.Glass").IsFrozen }
        }, new JsonSerializerOptions { WriteIndented = true }));

        async Task Shot(string name, int settle = 700)
        {
            await host.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
            var filter = Environment.GetEnvironmentVariable("X20CTL_REVIEW_SHOTS");
            if (!string.IsNullOrEmpty(filter) && !filter.Split(',').Contains(name))
            { stage.Measure(new Size(1600, 900)); stage.Arrange(new Rect(0, 0, 1600, 900)); stage.UpdateLayout(); await Task.Delay(60); return; }
            await Task.Delay(settle);
            foreach (var (width, height, dpi, suffix) in new[] { (1600, 900, 96, ""), (1040, 700, 96, "-minimum"), (1600, 900, 120, "-dpi125"), (1600, 900, 144, "-dpi150") })
            {
                stage.Measure(new Size(width, height)); stage.Arrange(new Rect(0, 0, width, height)); stage.UpdateLayout();
                var image = new RenderTargetBitmap(width * dpi / 96, height * dpi / 96, dpi, dpi, PixelFormats.Pbgra32); image.Render(stage);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
                using var stream = File.Create(Path.Combine(directory, name + suffix + ".png")); encoder.Save(stream);
            }
            stage.Measure(new Size(1600, 900)); stage.Arrange(new Rect(0, 0, 1600, 900)); stage.UpdateLayout();
            shots.Add(name);
        }
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i); if (child == null) continue; yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
}
