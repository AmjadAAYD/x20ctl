using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace X20Ctl.Product.Development;

// The same fixture runs against untouched main and the theme checkout, without showing a window.
internal static class DefaultBaselineReview
{
    internal static async Task Run(string directory)
    {
        ReviewSandbox.Active = true; ReviewSandbox.HideLibrary = true; Directory.CreateDirectory(directory);
        var host = new ProductHost(new ZoneScene(true), Path.Combine(directory, "assignments.json"));
        var stage = new DesignStage { Child = host };
        using var source = new HwndSource(new HwndSourceParameters("default baseline") { WindowStyle = unchecked((int)0x80000000), Width = 1600, Height = 900, PositionX = -32000, PositionY = -32000 }) { RootVisual = stage };
        await Shot("zone-empty");
        host.Zone.AssignController(0, host.Catalog.Get("x20")); host.Zone.AssignController(1, host.Catalog.Get("x15"));
        await host.Zone.FocusPlayerAsync(null); await Shot("zone");
        await host.Zone.FocusPlayerAsync(0); await Shot("hero"); await host.Zone.FocusPlayerAsync(null);
        host.EnterStudio(0); await Shot("dashboard"); var studio = host.Studio!;
        studio.ShowButtons(); studio.SelectControl("A"); await Shot("buttons");
        studio.ShowCurves(true); await Shot("curves");
        studio.ShowMacros(true); await Shot("macros");
        studio.ShowVibration(true); await Shot("vibration");
        foreach (string page in new[] { "Device", "PcMode", "Mouse", "Setups", "Tester", "Diagnostics" }) { studio.ShowManagement(page); await Shot(page.ToLowerInvariant()); }
        host.ReturnToZone(); host.ShowSettings(); await Shot("hub");
        host.Settings!.ShowAbout(true); await Shot("about");
        host.Settings.ShowAbout(false); host.Settings.ShowFeedback(true); await Shot("feedback");
        File.WriteAllText(Path.Combine(directory, "complete.txt"), "17 Default pages rendered without a window.");

        async Task Shot(string name)
        {
            await host.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle); await Task.Delay(700);
            stage.Measure(new Size(1600, 900)); stage.Arrange(new Rect(0, 0, 1600, 900)); stage.UpdateLayout();
            foreach (var clock in Walk(host).OfType<HeaderClock>()) clock.Text = "19:24";
            stage.UpdateLayout();
            var image = new RenderTargetBitmap(1600, 900, 96, 96, PixelFormats.Pbgra32); image.Render(stage);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
            using var file = File.Create(Path.Combine(directory, name + ".png")); encoder.Save(file);
        }
    }
    private static IEnumerable<DependencyObject> Walk(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i); if (child == null) continue; yield return child;
            foreach (var nested in Walk(child)) yield return nested;
        }
    }
}

