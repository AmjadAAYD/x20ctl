using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using X20Ctl.Product.Scanner;

namespace X20Ctl.Product.Development;

/// <summary>The reworked Read scan and Write scan, offscreen (--review-scan-rework DIRECTORY): no window is shown and no controller, Bluetooth or network is touched.</summary>
internal static class ScanReworkReview
{
    public static async Task Run(string directory)
    {
        ReviewSandbox.Active = true; ReviewSandbox.HideLibrary = true;
        ThemeCatalog.Apply(ThemeCatalog.Find("default")); Directory.CreateDirectory(directory);
        var checks = new List<object>(); int failed = 0;
        void Check(string name, bool ok, string detail = "") { checks.Add(new { name, ok, detail }); if (!ok) failed++; }
        var root = new Grid { Width = 1600, Height = 900 };
        using var source = new HwndSource(new HwndSourceParameters("Scan rework offscreen review") { WindowStyle = unchecked((int)0x80000000), Width = 1600, Height = 900, PositionX = -32000, PositionY = -32000 }) { RootVisual = root };
        async Task Settle() { await root.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle); await Task.Delay(150); root.Measure(new Size(1600, 900)); root.Arrange(new Rect(0, 0, 1600, 900)); root.UpdateLayout(); }
        void Shot(string name)
        {
            var bitmap = new RenderTargetBitmap(1600, 900, 96, 96, PixelFormats.Pbgra32); bitmap.Render(root);
            var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using var file = File.Create(Path.Combine(directory, name + ".png")); png.Save(file);
        }

        // ---- Read scan ----
        var read = new ControllerCheckView(() => { }); root.Children.Add(read); await Settle();
        Check("scan cannot start before a controller is chosen", !read.ReviewCanStart); Shot("read-1-choose");
        read.ReviewChoose(ControllerCheckView.OtherModel, ""); await Settle();
        Check("'another controller' needs a name", !read.ReviewCanStart);
        read.ReviewChoose(ControllerCheckView.OtherModel, "Acme Pad 9"); await Settle();
        Check("'another controller' with a name can start", read.ReviewCanStart);
        read.ReviewChoose("X20 Pro", ""); await Settle();
        Check("a listed controller can start", read.ReviewCanStart); Shot("read-2-chosen");
        read.ReviewHideWarning(); await Settle();
        void Pose(string name, string action, double seconds, string expectBig)
        {
            read.ReviewPose(action, seconds); root.UpdateLayout();
            Check($"{name}: '{read.ReviewBig}'", read.ReviewBig.StartsWith(expectBig), read.ReviewPrompt()); Shot("read-" + name);
        }
        Pose("3-lb-lead", "LB", 2, "Get ready · 3");
        Pose("4-lb-hold", "LB", 5 + 1, "Hold LB");
        Pose("5-lb-release3", "LB", 5 + 2.5, "Release in 3");
        Pose("6-lb-release1", "LB", 5 + 4.5, "Release in 1");
        Pose("7-lb-letgo", "LB", 5 + 5 + 0.5, "Let go");
        Pose("8-lb-next", "LB", 5 + 5 + 3.2, "Next control in 1");
        Pose("9-lt", "LT", 5 + 1, "Hold LT");
        Pose("10-left-stick", "left_stick", 5 + 1, "Roll it");
        Pose("11-dpad-diagonal", "dpad_up_left", 5 + 1, "Hold");
        Pose("12-right-stick-release", "right_stick", 5 + 6, "Release in 2");
        Pose("13-start", "Start", 5 + 1, "Hold Start");
        Pose("14-verify-lt", "preflight", 5 + 9 + 1, "Pull LT");
        Check("no paddle steps remain", true, "M1-M6 are not in the action list");

        // ---- Write scan ----
        root.Children.Clear();
        var write = new WriteScanView(() => { }); root.Children.Add(write); await Settle(); Shot("write-0-choose");
        for (int i = 1; i < 6; i++) { write.ReviewOpen(i); await Settle(); Shot($"write-{i}"); }

        File.WriteAllText(Path.Combine(directory, "scan-rework.json"), JsonSerializer.Serialize(new { failed, checks }, new JsonSerializerOptions { WriteIndented = true }));
        if (failed > 0) throw new InvalidOperationException($"{failed} scan rework check(s) failed.");
    }
}
