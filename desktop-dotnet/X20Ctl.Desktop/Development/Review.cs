using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using X20Ctl.Desktop.Controls;
using X20Ctl.Desktop.Views;
using X20Ctl.Simulation;

namespace X20Ctl.Desktop;

/// <summary>Development-only review of the built WPF views; never reads the desktop or hardware.</summary>
internal static class Review
{
    public static async Task Run(string directory)
    {
        Directory.CreateDirectory(directory);
        var bindingLog = new StringWriter();
        var listener = new TextWriterTraceListener(bindingLog);
        PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
        var checks = new List<object>(); var renders = new List<string>();
        void Check(bool passed, string name) { checks.Add(new { name, passed }); if (!passed) throw new InvalidOperationException("Review failed: " + name); }
        foreach (bool scanner in new[] { false, true })
        {
            foreach (bool compact in new[] { false, true })
            {
                Window window = scanner ? new ScannerWindow() : new MainWindow();
                var model = scanner ? ((ScannerWindow)window).Model : ((MainWindow)window).Model;
                model.Paused = true;
                window.WindowStartupLocation = WindowStartupLocation.Manual;
                window.Left = -10000; window.Top = -10000; window.ShowActivated = false; window.ShowInTaskbar = false;
                if (compact) { window.Width = 1044; window.Height = 704; }
                window.Show();
                await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
                model.Update(0); model.Update(250); model.Update(667);
                window.UpdateLayout();
                var view = Descendants(window).OfType<InputWorkspace>().Single();
                Check(model.PressedControl == "A" && model.HeldDuration == "0.42", $"{scanner}/{compact}: hold text is sourced from simulated edges");
                var art = Descendants(window).OfType<ControllerVisualizer>().Single();
                Check(art.ActualWidth > 440 && art.ActualHeight >= 220, $"{scanner}/{compact}: controller remains dominant ({art.ActualWidth:0}x{art.ActualHeight:0} DIPs)");
                var choices = Descendants(view).OfType<ComboBox>().ToArray();
                Check(choices.Length == 3 && choices.All(c => c.ActualWidth > 120 && c.ActualHeight >= 30), $"{scanner}/{compact}: source controls stay usable");
                foreach (double scale in new[] { 1d, 1.25, 1.5 })
                {
                    var surface = (FrameworkElement)window.Content;
                    var image = new RenderTargetBitmap((int)Math.Ceiling(surface.ActualWidth * scale), (int)Math.Ceiling(surface.ActualHeight * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
                    image.Render(surface);
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
                    string name = $"{(scanner ? "scanner-inputs" : "input-tester")}-{(compact ? "compact" : "normal")}-{scale * 100:0}.png";
                    using (var file = File.Create(Path.Combine(directory, name))) encoder.Save(file);
                    renders.Add(name);
                }
                view.CycleInspector(1); window.UpdateLayout();
                Check(model.SelectedSegment == "Sticks", $"{scanner}/{compact}: inspector navigation changes content");
                model.Scenario = Scenario.Disconnected; model.Update(500);
                Check(model.PressedControl == "No signal" && model.Frame.Buttons == Buttons.None, $"{scanner}/{compact}: disconnected does not retain pressed telemetry");
                model.Scenario = Scenario.RawInputFallback; model.Update(0); model.Update(667);
                Check(model.IsRaw && model.PressedControl != "A", $"{scanner}/{compact}: raw usages do not masquerade as named face buttons");
                model.Scenario = Scenario.Standard; model.Update(250); model.Slot = 3;
                Check(model.LastRelease.Contains("inconclusive"), $"{scanner}/{compact}: source reset cannot complete a hold");
                window.Close();
            }
        }
        listener.Flush();
        File.WriteAllText(Path.Combine(directory, "bindings.txt"), bindingLog.ToString());
        Check(string.IsNullOrEmpty(bindingLog.ToString()), "No WPF binding errors");
        File.WriteAllText(Path.Combine(directory, "review.json"), JsonSerializer.Serialize(new { status = "passed", hardware_access = false, desktop_capture = false, render_type = "built WPF visual tree in owned offscreen windows", os_dpi_changed = false, render_scales = new[] { 100, 125, 150 }, checks, renders }, new JsonSerializerOptions { WriteIndented = true }));
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}
