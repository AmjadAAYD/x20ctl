using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace X20Ctl.Product.Development;

/// <summary>Scratch probe for one finding at a time (--review-probe DIRECTORY).</summary>
internal static class StressProbe
{
    public static async Task Run(string directory)
    {
        Directory.CreateDirectory(directory); var log = new List<string>(); ReviewSandbox.Active = true;
        var window = new ProductWindow(false, null);
        try
        {
            window.Show(); window.WindowState = WindowState.Maximized; await Task.Delay(600); window.Host.Intro?.Finish(); await Task.Delay(500);
            log.Add("assigned at launch: " + window.Scene.State.Players.Count(p => p.Model != null)); Shot("1-zone-empty");
            window.Scene.AssignController(0, window.Host.Catalog.Get("x20")); window.Scene.AssignController(1, window.Host.Catalog.Get("x15")); window.Scene.AssignController(2, window.Host.Catalog.Get("x05_pro"));
            await window.Scene.FocusPlayerAsync(null); await Task.Delay(700); Shot("2-zone-grid");
            await window.Scene.FocusPlayerAsync(0); await Task.Delay(700); Shot("3-zone-hero");
            window.Host.EnterStudio(0); await Task.Delay(150); Shot("4-studio-entering"); await Task.Delay(900); Shot("5-dashboard");
            var s = window.Host.Studio!; s.ShowManagement("Device"); await Task.Delay(700); Shot("6-device");
            s.ShowVibration(true); s.Vibration!.Set(100); await Task.Delay(700); Shot("7-vibration-a"); await Task.Delay(330); Shot("7-vibration-b");
            s.ShowMacros(true); await Task.Delay(700); Shot("8-macros");
            window.Host.ReturnToZone(); await Task.Delay(120); Shot("9-zone-returning"); await Task.Delay(700);
            window.Host.ShowSettings(); await Task.Delay(700); window.Host.OpenCheck(); await Task.Delay(800); Shot("10-check-warning");
            var linuxChoice = Find(window.Stage, "A Linux PC"); if (linuxChoice != null) { linuxChoice.IsChecked = true; await Task.Delay(400); Shot("11-check-linux"); } else log.Add("no linux choice");
        }
        catch (Exception e) { log.Add(e.ToString()); }
        finally { File.WriteAllLines(Path.Combine(directory, "probe.txt"), log); ReviewSandbox.Active = false; window.Close(); }
        void Shot(string name)
        {
            window.UpdateLayout(); var bmp = new RenderTargetBitmap((int)window.Stage.ActualWidth, (int)window.Stage.ActualHeight, 96, 96, PixelFormats.Pbgra32); bmp.Render(window.Stage);
            var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(bmp)); using var f = File.Create(Path.Combine(directory, name + ".png")); enc.Save(f);
        }
    }
    private static System.Windows.Controls.RadioButton? Find(DependencyObject root, string content)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) { var c = VisualTreeHelper.GetChild(root, i); if (c is System.Windows.Controls.RadioButton r && Equals(r.Content, content)) return r; if (Find(c, content) is { } f) return f; }
        return null;
    }
}