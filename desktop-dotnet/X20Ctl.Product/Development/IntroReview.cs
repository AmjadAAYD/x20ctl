using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace X20Ctl.Product.Development;

/// <summary>Renders the first-launch cutscene at fixed moments inside the real host (Controller Zone behind it).</summary>
internal static class IntroReview
{
    public static async Task Run(string directory)
    {
        Directory.CreateDirectory(directory);
        // first launch: the very first frame must already be the intro (black), never the Controller Zone
        var first = new ProductWindow(false, Path.Combine(directory, "first-run", "assignments.json"), firstRun: true); string? firstFrame = null;
        void OnFirstFrame(object? s, EventArgs e) { if (firstFrame == null && first.IsVisible) firstFrame = first.Host.Intro != null && first.Host.Zone.Opacity == 0 ? "intro" : $"zone visible (intro {(first.Host.Intro != null)}, zone opacity {first.Host.Zone.Opacity}, played {first.Host.IntroWasPlayed}, feedback {first.Scene.FeedbackText.Text})"; }
        System.Windows.Media.CompositionTarget.Rendering += OnFirstFrame; first.Show(); await Task.Delay(400); System.Windows.Media.CompositionTarget.Rendering -= OnFirstFrame; first.Host.Intro?.Finish(); first.Close();
        File.WriteAllText(Path.Combine(directory, "first-frame.txt"), firstFrame ?? "no frame");
        var window = new ProductWindow(false, Path.Combine(directory, "assignments.json"));
        try
        {
            window.Show(); window.WindowState = WindowState.Maximized; await Task.Delay(400);
            window.Host.StartIntro(); var fps = await ShowcaseReviewProbe.Run(6200); await Task.Delay(1500);
            File.WriteAllText(Path.Combine(directory, "fps.json"), System.Text.Json.JsonSerializer.Serialize(fps, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            window.Host.StartIntro(); await Task.Delay(150); if (window.Host.Intro == null) throw new InvalidOperationException("Intro did not start: " + window.Scene.FeedbackText.Text);
            foreach (double t in new[] { 1.3, 1.5, 1.75, 1.97, 2.4, 2.6, 2.9, 3.3, 3.7, 4.4, 5.0, 5.4 })
            {
                window.Host.Intro!.Seek(t); await Task.Delay(90); window.UpdateLayout();
                var bmp = new RenderTargetBitmap((int)window.Stage.ActualWidth, (int)window.Stage.ActualHeight, 96, 96, PixelFormats.Pbgra32); bmp.Render(window.Stage);
                var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(bmp)); using var s = File.Create(Path.Combine(directory, $"intro-{t:0.00}s.png")); enc.Save(s);
            }
            window.Host.Intro?.Finish();
        }
        finally { window.Close(); }
    }
}
