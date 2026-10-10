using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace X20Ctl.Product.Development;

/// <summary>Renders every model's layered controller through the real Studio "Try it" key path:
/// neutral, sticks deflected, triggers pressed (rear) and C/T + paddle routing checks.</summary>
internal static class LiveReview
{
    public static async Task Run(string directory)
    {
        Directory.CreateDirectory(directory); var rows = new List<object>();
        var window = new ProductWindow(false, Path.Combine(directory, "assignment-fixture.json"));
        try
        {
            window.Show(); window.WindowState = WindowState.Maximized; await Task.Delay(300);
            foreach (var model in window.Host.Catalog.Models)
            {
                window.Host.ReturnToZone(); window.Scene.AssignController(0, model); await window.Scene.FocusPlayerAsync(0); await Capture(model.Id + "-1-zone.png");
                window.Host.EnterStudio(0); var scene = window.Host.Studio!; await Capture(model.Id + "-0-dashboard.png", 1400); scene.ShowButtons(); await Capture(model.Id + "-2-front-neutral.png");
                scene.StartTry(); bool trying = scene.IsTrying;
                Send(scene, Key.W, true); Send(scene, Key.D, true); Send(scene, Key.Left, true); Send(scene, Key.Down, true);
                await Capture(model.Id + "-3-front-sticks.png", 450);
                Send(scene, Key.W, false); Send(scene, Key.D, false); Send(scene, Key.Left, false); Send(scene, Key.Down, false);
                scene.ShowController(true); Send(scene, Key.Q, true); await Capture(model.Id + "-4-rear-lt-pressed.png", 450);
                Send(scene, Key.E, true); await Capture(model.Id + "-5-rear-both-pressed.png", 450);
                window.Host.ResetTransientInput(); await Capture(model.Id + "-6-rear-released-on-deactivate.png", 450);
                bool hasCT = scene.TargetButtons.Any(b => Equals(b.Tag, "CAPTURE") || Equals(b.Tag, "TURBO"));
                scene.StopTry(); scene.ShowController(false);
                bool paddleOpensMacros = false;
                if (scene.MacrosNav.IsEnabled) { scene.SelectControl("M1"); await Task.Delay(150); await Capture(model.Id + "-6b-rear-paddles.png"); var paddle = FindHotspot(scene.ButtonsPageHost, "M1"); paddle?.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)); await Task.Delay(150); paddleOpensMacros = scene.IsMacros && scene.Macros?.Slot == "M1"; if (paddleOpensMacros) await Capture(model.Id + "-7-paddle-opens-macros.png"); scene.ShowMacros(false); }
                rows.Add(new { model = model.Id, tryAvailable = trying, onboardCtOffered = hasCT, paddleOpensMacros, macrosAvailable = scene.MacrosNav.IsEnabled });
            }
            File.WriteAllText(Path.Combine(directory, "live-review.json"), JsonSerializer.Serialize(new { status = "completed", models = rows, hardwareWrites = false, input = "Studio Try-it keyboard path (labelled preview)" }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally { window.Close(); }

        static void Send(StudioScene scene, Key key, bool down)
        {
            var source = PresentationSource.FromVisual(scene)!;
            var e = new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, key) { RoutedEvent = down ? Keyboard.PreviewKeyDownEvent : Keyboard.PreviewKeyUpEvent };
            if (down) scene.OnKeyDown(scene, e); else scene.OnKeyUp(scene, e);
        }
        static PhysicalHotspot? FindHotspot(DependencyObject root, string key)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is PhysicalHotspot hotspot && Equals(hotspot.Tag, key)) return hotspot;
                if (FindHotspot(child, key) is { } found) return found;
            }
            return null;
        }
        async Task Capture(string name, int delay = 250)
        {
            await Task.Delay(delay); window.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(window.Host.ActualWidth), (int)Math.Ceiling(window.Host.ActualHeight), 96, 96, PixelFormats.Pbgra32); bitmap.Render(window.Host);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var stream = File.Create(Path.Combine(directory, name)); encoder.Save(stream);
        }
    }
}
