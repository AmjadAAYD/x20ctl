using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using X20Ctl.Desktop.Controls;
using X20Ctl.Desktop.Views;
namespace X20Ctl.Desktop;
internal static class ScannerVisualReview
{
    public static async Task Run(string directory)
    {
        Directory.CreateDirectory(directory); var shots = new List<object>();
        foreach (bool compact in new[] { false, true })
        {
            var window = new ScannerWindow { ShowActivated = false, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual, Left = -10000, Top = -10000 };
            window.Model.Paused = true;
            if (compact) { window.Width = 1120; window.Height = 780; }
            window.Show();
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
            await Task.Delay(250);
            window.Model.Update(0); window.Model.Update(250); window.Model.Update(667); window.UpdateLayout(); await Task.Delay(220);
            var page = Descendants(window).OfType<ScannerInputsPage>().Single();
            var hero = Descendants(window).OfType<ControllerVisualizer>().Single();
            if (Descendants(window).OfType<ComboBox>().Any(c => c.IsVisible)) throw new InvalidOperationException("Developer selectors are visible in the normal scene.");
            foreach (double scale in new[] { 1d, 1.25, 1.5 })
            {
                var surface = (FrameworkElement)window.Content;
                var render = new RenderTargetBitmap((int)Math.Ceiling(surface.ActualWidth * scale), (int)Math.Ceiling(surface.ActualHeight * scale), scale * 96, scale * 96, PixelFormats.Pbgra32); render.Render(surface);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(render));
                string name = $"scanner-inputs-{(compact ? "compact" : "normal")}-{scale * 100:0}.png";
                using (var output = File.Create(Path.Combine(directory, name))) encoder.Save(output);
                shots.Add(new { file = name, scale, hero_width = hero.ActualWidth, hero_height = hero.ActualHeight, developer_controls_visible = false });
            }
            page.ToggleDeveloperDrawer(); window.UpdateLayout();
            if (!Descendants(window).OfType<ComboBox>().Any(c => c.IsVisible)) throw new InvalidOperationException("Developer drawer cannot be opened.");
            page.ToggleDeveloperDrawer(); window.Close();
        }
        File.WriteAllText(Path.Combine(directory, "render-report.json"), JsonSerializer.Serialize(new { status = "passed", hardware_access = false, desktop_capture = false, rendering = "built WPF visual tree, own offscreen windows", shots }, new JsonSerializerOptions { WriteIndented = true }));
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root) { for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) { var child = VisualTreeHelper.GetChild(root, i); yield return child; foreach (var value in Descendants(child)) yield return value; } }
}
