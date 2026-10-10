using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using X20Ctl.Simulation;

namespace X20Ctl.Desktop.Controls;

/// <summary>Existing X20 art and geometry, with separate live layers. This preview consumes simulation only.</summary>
public sealed class ControllerVisualizer : FrameworkElement
{
    public static readonly DependencyProperty FrameProperty = DependencyProperty.Register(nameof(Frame), typeof(InputFrame), typeof(ControllerVisualizer), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public InputFrame? Frame { get => (InputFrame?)GetValue(FrameProperty); set => SetValue(FrameProperty, value); }
    public static readonly DependencyProperty ScannerSceneProperty = DependencyProperty.Register(nameof(ScannerScene), typeof(bool), typeof(ControllerVisualizer), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));
    public bool ScannerScene { get => (bool)GetValue(ScannerSceneProperty); set => SetValue(ScannerSceneProperty, value); }
    private readonly BitmapImage cutout = Load("controller-scanner-cutout-v1.png");
    private readonly BitmapImage original = Load("controller.png");
    private readonly BitmapImage body = Load("controller-base.png");
    private readonly JsonDocument geometry;
    public ControllerVisualizer()
    {
        geometry = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Assets", "geometry.json")));
        IsHitTestVisible = false;
    }
    private static BitmapImage Load(string name)
    {
        var bitmap = new BitmapImage(new Uri($"pack://application:,,,/Assets/{name}")); bitmap.Freeze(); return bitmap;
    }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (ActualWidth <= 0 || ActualHeight <= 0) return;
        double width = ScannerScene ? 1388 : 1536, height = ScannerScene ? 982 : 1024;
        double scale = Math.Min(ActualWidth / width, ActualHeight / height);
        dc.PushTransform(new TranslateTransform((ActualWidth - width * scale) / 2 - (ScannerScene ? 74 * scale : 0), (ActualHeight - height * scale) / 2 - (ScannerScene ? 25 * scale : 0)));
        dc.PushTransform(new ScaleTransform(scale, scale));
        dc.PushOpacity(Frame?.Connected == true ? 1 : .48);
        // Independently drawn product silhouette removes the rectangular photo surround in the native composition.
        if (ScannerScene) dc.DrawImage(cutout, new(0, 0, 1536, 1024));
        else
        {
            dc.PushClip(Geometry.Parse("M347,96 C360,51 483,25 526,60 L563,81 L971,81 L1012,60 C1049,23 1184,54 1196,96 C1278,122 1306,237 1350,385 C1375,485 1428,661 1448,821 C1475,1006 1350,1034 1260,973 C1210,943 1180,865 1103,754 C1075,714 1025,702 952,702 L584,702 C511,702 461,714 433,754 C356,865 327,943 276,973 C187,1034 61,1006 88,821 C109,661 162,485 186,385 C231,237 264,122 347,96 Z"));
            dc.DrawImage(body, new(0, 0, 1536, 1024)); dc.Pop();
        }
        var data = geometry.RootElement;
        DrawStick(dc, data.GetProperty("sticks").GetProperty("left"), Frame?.LeftX ?? 0, Frame?.LeftY ?? 0, Frame?.Buttons.HasFlag(Buttons.L3) == true);
        DrawStick(dc, data.GetProperty("sticks").GetProperty("right"), InputFrame.NormalizeAxis(Frame?.RX ?? 0), InputFrame.NormalizeAxis(Frame?.RY ?? 0), Frame?.Buttons.HasFlag(Buttons.R3) == true);
        bool raw = Frame?.SourceKey.Contains("raw-input") == true;
        if (!raw && Frame?.Connected == true)
        {
            foreach (var button in data.GetProperty("buttons").EnumerateObject())
            {
                var flag = button.Name switch { "SELECT" => Buttons.Back, "START" => Buttons.Start,
                    "DPAD_UP" => Buttons.Up, "DPAD_DOWN" => Buttons.Down, "DPAD_LEFT" => Buttons.Left, "DPAD_RIGHT" => Buttons.Right,
                    _ => Enum.TryParse<Buttons>(button.Name, out var value) ? value : Buttons.None };
                if (flag == Buttons.None || !Frame.Buttons.HasFlag(flag) || flag is Buttons.LB or Buttons.RB) continue;
                var point = new Point(button.Value.GetProperty("x").GetDouble() * 1536, button.Value.GetProperty("y").GetDouble() * 1024);
                double radius = flag is Buttons.A or Buttons.B or Buttons.X or Buttons.Y ? 55 : 29;
                dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(55, 84, 244, 190)), new Pen(new SolidColorBrush(Color.FromRgb(103, 248, 204)), 6), point, radius, radius);
            }
            DrawShoulder(dc, new Rect(352, 38, 158, 20), Frame.LT / 255d, Frame.Buttons.HasFlag(Buttons.LB));
            DrawShoulder(dc, new Rect(1027, 38, 158, 20), Frame.RT / 255d, Frame.Buttons.HasFlag(Buttons.RB));
        }
        dc.Pop(); dc.Pop(); dc.Pop();
    }
    private void DrawStick(DrawingContext dc, JsonElement stick, double x, double y, bool pressed)
    {
        var cap = stick.GetProperty("cap");
        double cx = cap.GetProperty("x").GetDouble() * 1536, cy = cap.GetProperty("y").GetDouble() * 1024;
        int w = (int)Math.Round(cap.GetProperty("w").GetDouble() * 1536), h = (int)Math.Round(cap.GetProperty("h").GetDouble() * 1024);
        var crop = new CroppedBitmap(original, new((int)Math.Round(cx - w / 2d), (int)Math.Round(cy - h / 2d), w, h));
        var center = new Point(cx + x * 22, cy - y * 22);
        dc.PushClip(new EllipseGeometry(center, w / 2d, h / 2d));
        dc.DrawImage(crop, new(center.X - w / 2d, center.Y - h / 2d, w, h)); dc.Pop();
        if (pressed) dc.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromRgb(103, 248, 204)), 5), center, w / 2d + 3, h / 2d + 3);
    }
    private static void DrawShoulder(DrawingContext dc, Rect area, double value, bool bumper)
    {
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb((byte)(40 + 180 * value), 110, 211, 255)), null, area, 10, 10);
        if (bumper) dc.DrawRoundedRectangle(null, new Pen(Brushes.SpringGreen, 4), new(area.X, area.Y + 23, area.Width, 15), 7, 7);
    }
}
