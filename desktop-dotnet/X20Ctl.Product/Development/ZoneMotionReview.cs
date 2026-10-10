using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using X20Ctl.Zone;
namespace X20Ctl.Product.Development;

/// <summary>Plays the Controller Zone's hero swap with full motion inside a real window, captures frames through the move
/// and measures the frame rate (--review-zone-motion DIRECTORY).</summary>
internal static class ZoneMotionReview
{
    public static async Task Run(string directory)
    {
        Directory.CreateDirectory(directory);
        var window = new ProductWindow(false, Path.Combine(directory, "assignments.json"));
        try
        {
            window.Show(); window.WindowState = WindowState.Maximized; await Task.Delay(500);
            var zone = window.Scene; await Task.Delay(300); Save("overview-empty.png");
            zone.LoadScenario(ZoneScenario.MultipleControllers); window.UpdateLayout(); await Task.Delay(300); Save("overview-populated.png");
            await zone.FocusPlayerAsync(0); await Task.Delay(300);
            Save("swap-0-start.png");
            // frames of one swap (capturing stalls the window, so this pass is for looks only)
            var swap = zone.FocusPlayerAsync(1);
            for (int i = 1; i <= 9; i++) { await Task.Delay(70); Save($"swap-{i}.png"); }
            await swap; await Task.Delay(200); Save("swap-z-end.png");
            // timing of two more swaps with nothing else running
            var probe = ShowcaseReviewProbe.Run(2200);
            await zone.FocusPlayerAsync(3); await Task.Delay(250); await zone.FocusPlayerAsync(0);
            var fps = await probe;
            // the controller picker, after its cards have risen in
            window.Host.OpenSelector(2); await Task.Delay(900); Save("picker.png");
            window.Host.CloseSelector();
            File.WriteAllText(Path.Combine(directory, "fps.json"), System.Text.Json.JsonSerializer.Serialize(fps, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
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
