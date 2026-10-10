using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using X20Ctl.Zone;
namespace X20Ctl.Product.Development;

/// <summary>Renders the Controller Zone and the Studio Dashboard in a restored window at the minimum and a medium size,
/// to catch anything cut off or spilling under the footer when the app is not fullscreen (--review-windowed DIRECTORY).</summary>
internal static class WindowedReview
{
    public static async Task Run(string directory)
    {
        Directory.CreateDirectory(directory);
        var window = new ProductWindow(false, Path.Combine(directory, "assignments.json"), startMaximized: false);
        try
        {
            window.Show(); await Task.Delay(400);
            foreach (var state in new[] { WindowState.Maximized, WindowState.Normal })
            {
                window.WindowState = state; await Task.Delay(700); string w = state == WindowState.Maximized ? "fullscreen" : "window", h = $"{window.ActualWidth:0}";
                window.Host.ReturnToZone(); window.Scene.LoadScenario(ZoneScenario.MultipleControllers); await window.Scene.FocusPlayerAsync(0); await Task.Delay(400);
                Save($"zone-{w}-{h}.png");
                window.Host.EnterStudio(0); await Task.Delay(1400);
                Save($"dashboard-{w}-{h}.png");
            }
        }
        finally { window.Close(); }

        void Save(string name)
        {
            window.UpdateLayout();
            var bmp = new RenderTargetBitmap((int)window.Stage.ActualWidth, (int)window.Stage.ActualHeight, 96, 96, PixelFormats.Pbgra32); bmp.Render(window.Stage);
            var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(bmp)); using var f = File.Create(Path.Combine(directory, name)); enc.Save(f);
        }
    }
}
