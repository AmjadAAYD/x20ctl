using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace X20Ctl.Product.Development;

/// <summary>Drives the InputMap through the real host (the same path controller buttons take) on every
/// Studio page and records whether each button did its job and what the live footer showed.</summary>
internal static class InputReview
{
    public static async Task Run(string directory)
    {
        Directory.CreateDirectory(directory); var checks = new List<object>(); int failed = 0;
        var window = new ProductWindow(false, Path.Combine(directory, "assignment-fixture.json"));
        try
        {
            window.Show(); window.WindowState = WindowState.Maximized; await Task.Delay(300);
            var model = window.Host.Catalog.Models.First(m => m.Id == "x20");
            window.Scene.AssignController(0, model); await window.Scene.FocusPlayerAsync(0); window.Host.EnterStudio(0); await Task.Delay(1200);
            var s = window.Host.Studio!;
            void Check(string name, bool ok, string detail = "") { checks.Add(new { name, ok, detail, page = s.CurrentPage, hints = Hints(s) }); if (!ok) failed++; }
            Check("opens on Dashboard", s.CurrentPage == "Dashboard");
            s.Dashboard!.Showcase.Focus(); await Settle(); Send(Key.Tab); await Settle();
            Check("Dashboard footer has one A hint", Hints(s).Split("A ").Length - 1 <= 1, Hints(s));
            await Capture("dashboard.png");
            Send(Key.X); await Settle(); Check("Dashboard X opens Buttons", s.CurrentPage == "Buttons");
            bool rear = false; Send(InputMap.SectionNext); await Settle(); rear = Hints(s).Contains("Front / Back"); Check("Buttons LT/RT hint shown", rear, Hints(s));
            Send(Key.X); await Settle(); Check("Buttons X starts Try it", s.IsTrying && Hints(s).Contains("Stop Try it"), Hints(s));
            Send(Key.X); await Settle(); Check("Buttons X stops Try it", !s.IsTrying);
            await Capture("buttons.png");
            Send(Key.PageDown); await Settle(); Check("RB → Curves", s.CurrentPage == "Curves");
            var ch = s.Curves.Channel; Send(InputMap.SectionNext); await Settle(); Check("Curves RT next channel", s.Curves.Channel != ch, $"{ch} → {s.Curves.Channel}");
            var preset = s.Curves.Draft[s.Curves.Channel].Preset; Send(Key.Y); await Settle(); Check("Curves Y next preset", s.Curves.Draft[s.Curves.Channel].Preset != preset, $"{preset} → {s.Curves.Draft[s.Curves.Channel].Preset}");
            Send(Key.PageDown); await Settle(); Check("RB → Macros", s.CurrentPage == "Macros");
            Send(InputMap.SectionNext); await Settle(); Check("Macros RT next slot", s.Macros?.Slot == "M2", s.Macros?.Slot ?? "");
            Send(Key.PageDown); await Settle(); Check("RB → Vibration", s.CurrentPage == "Vibration");
            Send(Key.PageDown); await Settle(); Check("RB → Device", s.CurrentPage == "Device");
            Send(Key.PageDown); await Settle(); Check("RB → Setups (no Tester tab)", s.CurrentPage == "Setups");
            for (int i = 0; i < 6; i++) { Send(Key.PageUp); await Settle(); }
            Check("LB walks back to Dashboard", s.CurrentPage == "Dashboard");
            Send(InputMap.ControllerZone); await Settle(); Check("View returns to Controller Zone", window.Host.Studio == null);
            File.WriteAllText(Path.Combine(directory, "input-review.json"), JsonSerializer.Serialize(new { failed, checks }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally { window.Close(); }

        void Send(Key key)
        {
            var src = PresentationSource.FromVisual(window)!;
            window.Host.OnKeyDown(window, new KeyEventArgs(Keyboard.PrimaryDevice, src, 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
            window.Host.OnKeyUp(window, new KeyEventArgs(Keyboard.PrimaryDevice, src, 0, key) { RoutedEvent = Keyboard.PreviewKeyUpEvent });
        }
        static Task Settle() => Task.Delay(260);
        static string Hints(StudioScene s)
        {
            var parts = new List<string>();
            void Walk(DependencyObject d) { foreach (var c in LogicalTreeHelper.GetChildren(d)) if (c is DependencyObject o) { if (o is TextBlock t) parts.Add(t.Text); Walk(o); } }
            Walk(s.LiveHints); return string.Join(" ", parts);
        }
        async Task Capture(string name)
        {
            await Task.Delay(200); window.UpdateLayout();
            var bmp = new RenderTargetBitmap((int)window.Stage.ActualWidth, (int)window.Stage.ActualHeight, 96, 96, PixelFormats.Pbgra32); bmp.Render(window.Stage);
            var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(bmp)); using var f = File.Create(Path.Combine(directory, name)); enc.Save(f);
        }
    }
}
