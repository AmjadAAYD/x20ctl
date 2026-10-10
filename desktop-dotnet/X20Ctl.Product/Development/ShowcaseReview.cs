using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace X20Ctl.Product.Development;

/// <summary>Renders the 3D turntable at fixed angles for every model and the photo → hologram scan.</summary>
internal static class ShowcaseReview
{
    public static async Task Run(string directory)
    {
        Directory.CreateDirectory(directory);
        var show = new ControllerShowcase();
        var root = new Grid { Children = { new GalaxyBackdrop(), new Viewbox { Child = show, Margin = new(120, 60, 120, 60) } } };
        var window = new Window { Width = 1400, Height = 900, WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, Content = root, ShowInTaskbar = false, Background = Brushes.Black };
        try
        {
            window.Show(); await Task.Delay(300);
            foreach (string model in new[] { "x20", "x20_pro", "x15", "x10", "x05", "x05_pro", "d10", "dune" })
            {
                show.Show(model);
                foreach (double angle in new[] { -24.0, 0, 24 }) { show.SetYaw(angle); await Capture($"{model}-yaw{angle:+0;-0;0}.png"); }
                show.SetSticks(.8, .6, -.7, -.6); show.SetYaw(14); await Capture($"{model}-sticks.png");
                show.SetSticks(0, 0, 0, 0);
            }
            show.Show("x20");
            foreach (double v in new[] { .15, .35, .55, .75, .95, 1.0 }) { show.PreviewSweep(v); await Capture($"sweep-{v * 100:000}.png"); }
            // frame-time probe: no captures while measuring, so the numbers are what the user would see
            show.Show("x20"); show.SetYaw(double.NaN); await Task.Delay(500);
            var sway = await ShowcaseReviewProbe.Run(3000);
            var probeChange = show.ToHologram(); var transition = await ShowcaseReviewProbe.Run(1400); await probeChange;
            int tier = System.Windows.Media.RenderCapability.Tier >> 16;
            File.WriteAllText(Path.Combine(directory, "fps.json"), System.Text.Json.JsonSerializer.Serialize(new { renderTier = tier, sway, transition }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));        }
        finally { window.Close(); }

        async Task Capture(string name, int delay = 150)
        {
            if (delay > 0) await Task.Delay(delay); window.UpdateLayout();
            var bmp = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32); bmp.Render(root);
            var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(bmp)); using var s = File.Create(Path.Combine(directory, name)); enc.Save(s);
        }
    }
}

internal static class ShowcaseReviewProbe
{
        public static async Task<object> Run(int ms)
        {
            var times = new List<double>(); var slow = new List<object>(); TimeSpan last = TimeSpan.Zero, first = TimeSpan.Zero;
            void Tick(object? s, EventArgs e) { var t = ((RenderingEventArgs)e).RenderingTime; if (t == last) return; if (first == TimeSpan.Zero) first = t; if (last != TimeSpan.Zero) { double ms = (t - last).TotalMilliseconds; times.Add(ms); if (ms > 20) slow.Add(new { atMs = Math.Round((t - first).TotalMilliseconds), frameMs = Math.Round(ms, 1) }); } last = t; }
            CompositionTarget.Rendering += Tick; await Task.Delay(ms); CompositionTarget.Rendering -= Tick;
            var sorted = times.OrderBy(x => x).ToArray(); double avg = times.Count > 0 ? times.Average() : 0;
            return new { frames = times.Count, avgFps = avg > 0 ? Math.Round(1000 / avg, 1) : 0, avgFrameMs = Math.Round(avg, 2), p95FrameMs = sorted.Length > 0 ? Math.Round(sorted[(int)(sorted.Length * .95)], 2) : 0, worstFrameMs = sorted.Length > 0 ? Math.Round(sorted[^1], 2) : 0, framesOver20ms = times.Count(x => x > 20), slow };
        }
}
