using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using X20Ctl.Zone;
namespace X20Ctl.Product.Development;

/// <summary>Optional local render review, invoked explicitly with --review-zone. No desktop capture.</summary>
internal static class ZoneReview
{
    public static async Task Run(string directory)
    {
        Directory.CreateDirectory(directory);
        var checks = new List<object>(); var shots = new List<string>();
        void Check(bool passed, string name) { checks.Add(new { name, passed }); if (!passed) throw new InvalidOperationException(name); }
        foreach (var (width, height, label) in new[] { (1220d, 800d, "normal"), (1040d, 700d, "minimum") })
        foreach (double scale in new[] { 1d, 1.25, 1.5 })
        {
            var scene = new ZoneScene(true) { Width = width, Height = height };
            VisualTreeHelper.SetRootDpi(scene, new DpiScale(scale, scale));
            scene.Measure(new Size(width, height)); scene.Arrange(new Rect(0, 0, width, height)); scene.UpdateLayout();
            Check(scene.Docks.Count == 4 && scene.State.Players.All(p => p.Model == null) && scene.State.FocusedPlayer == null, $"{label}/{scale}: empty equal overview");
            Render(scene, $"overview-{label}-{scale * 100:0}.png");
            await scene.FocusPlayerAsync(0); scene.UpdateLayout();
            Check(scene.State.FocusedPlayer == 0 && !scene.IsTransitioning, $"{label}/{scale}: empty Player 1 can expand");
            Render(scene, $"empty-player1-{label}-{scale * 100:0}.png");
            var player1 = scene.Docks[0];
            await scene.FocusPlayerAsync(1); scene.UpdateLayout();
            Check(ReferenceEquals(player1, scene.Docks[0]) && scene.State.FocusedPlayer == 1, $"{label}/{scale}: same Player 1 view retracts while Player 2 expands");
            Render(scene, $"empty-player2-{label}-{scale * 100:0}.png");
            await scene.FocusPlayerAsync(null); scene.UpdateLayout();
            Check(scene.State.FocusedPlayer == null && scene.Docks.All(d => Math.Abs(d.Width - scene.Docks[0].Width) < .01), $"{label}/{scale}: return to equal overview");
            scene.LoadScenario(ZoneScenario.MultipleControllers); scene.UpdateLayout();
            await scene.FocusPlayerAsync(0); scene.UpdateLayout();
            Render(scene, $"populated-player1-{label}-{scale * 100:0}.png");
            await scene.FocusPlayerAsync(1); scene.UpdateLayout();
            Check(scene.Docks[0].Player.Model == "EasySMX X20" && scene.Docks[1].Player.Model == "EasySMX X15" && scene.Docks[3].Player.Model == "EasySMX X05 Pro", $"{label}/{scale}: each dock retains model/art assignment");
            Render(scene, $"populated-player2-{label}-{scale * 100:0}.png");

            void Render(FrameworkElement root, string file)
            {
                var image = new RenderTargetBitmap((int)(width * scale), (int)(height * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
                image.Render(root);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
                using (var stream = File.Create(Path.Combine(directory, file))) encoder.Save(stream);
                shots.Add(file);
            }
        }
        File.WriteAllText(Path.Combine(directory, "review.json"), JsonSerializer.Serialize(new {
            status = "passed", hardwareAccess = false, desktopCapture = false,
            motionVerified = false, reducedMotion = true,
            note = "Static built-WPF renders; animated transitions require manual review. This helper never captures the desktop or opens a window.",
            checks, shots
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
