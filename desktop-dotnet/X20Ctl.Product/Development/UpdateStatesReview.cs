using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using X20Ctl.Zone;
namespace X20Ctl.Product.Development;
/// <summary>Offscreen renders of the update screens (the notice, the question, the download, both failures, What's new). Nothing is downloaded, installed or shown on screen.</summary>
internal static class UpdateStatesReview
{
    public static void Run(string directory)
    {
        ReviewSandbox.Active = true;
        Directory.CreateDirectory(directory);
        const string name = "V0.099";
        UpdateFlow Flow(bool canInstall, Action<UpdateFlow>? step = null) { var f = new UpdateFlow(name, canInstall); f.Open(); step?.Invoke(f); return f; }
        var changelog = "### Added\n\n- **One-click update.** The top-bar notice reads \"Update to version <latest>\".\n- **A new thing.** It does a [useful](https://example.com) job.\n\n### Fixed\n\n- A `bug` is gone.\n";
        var shots = new List<(string File, FrameworkElement View)>
        {
            ("notice-ready", Notice()),
            ("confirm", new UpdateFlowView(Flow(true))),
            ("progress", new UpdateFlowView(Flow(true, f => { f.Yes(); f.Report(.42); }))),
            ("failed-retry", new UpdateFlowView(Flow(true, f => { f.Yes(); f.Fail("The download didn't match the release's checksum, so it was thrown away."); }))),
            ("failed-folder", new UpdateFlowView(Flow(false))),
            ("whats-new", UpdateFlowView.WhatsNew(name, changelog)),
        };
        foreach (var (file, view) in shots)
        {
            const int w = 1100, h = 640;
            var root = new Grid { Width = w, Height = h, Background = DS.Brush("Ink") };
            root.Children.Add(view);
            root.Measure(new Size(w, h)); root.Arrange(new Rect(0, 0, w, h)); root.UpdateLayout();
            if (view is UpdateFlowView) { root.UpdateLayout(); (view as UpdateFlowView)!.Render(); root.UpdateLayout(); }
            var bitmap = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32); bitmap.Render(root);
            var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(directory, file + ".png")); png.Save(stream);
        }
        File.WriteAllText(Path.Combine(directory, "review.txt"), "Production views with synthetic version, progress and changelog text; no window, download, installation or restart.");
    }
    static FrameworkElement Notice()
    {
        var label = Kit.Text(UpdateKit.Label("V0.099", false), 14, "DS.Text", FontWeights.SemiBold); label.Foreground = DS.Brush("Zone.TextSecondary");
        return new Border { Padding = new Thickness(20), VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Center, Child = ProductHost.CreateUpdateNoticeButton(label, null) };
    }
}
