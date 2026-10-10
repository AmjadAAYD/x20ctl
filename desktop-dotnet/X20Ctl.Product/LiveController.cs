using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
namespace X20Ctl.Product;

/// <summary>
/// Layered, transparent controller artwork in the 1536x1024 source space. The body is drawn
/// without its stick caps and trigger caps; those are separate layers moved by input values
/// (keyboard demo, simulation or read-only XInput). Purely visual: it never talks to hardware.
/// </summary>
public sealed class LiveController : Grid
{
    private sealed record StickInfo(Point Center, double Travel);
    private sealed record TriggerInfo(Point A, Point B, double Rest, double Travel);
    private sealed record ModelInfo(StickInfo Left, StickInfo Right, TriggerInfo LT, TriggerInfo RT);
    private static Dictionary<string, ModelInfo>? manifest;
    private static readonly Dictionary<string, BitmapImage> cache = new();

    private readonly Image body = Layer(), capLeft = Layer(), capRight = Layer(), triggerLeft = Layer(), triggerRight = Layer();
    private readonly ControllerOutline outline = new();
    private readonly TranslateTransform leftMove = new(), rightMove = new();
    private readonly SkewTransform leftTilt = new(), rightTilt = new();
    private readonly DropShadowEffect leftShadow = CapShadow(), rightShadow = CapShadow();
    private readonly MatrixTransform ltPose = new(), rtPose = new();
    private readonly DropShadowEffect ltGlow = TriggerGlow(), rtGlow = TriggerGlow();
    private ModelInfo? info;
    private bool rear, rendering;
    // target (input) and displayed (smoothed) values: lx, ly, rx, ry, lt, rt
    private readonly double[] target = new double[6], shown = new double[6];
    private TimeSpan lastFrame;
    public bool Smooth { get; set; } = true;
    public string ModelId { get; private set; } = "";
    public bool IsRear => rear;

    public LiveController()
    {
        Width = 1536; Height = 1024; IsHitTestVisible = false;
        foreach (var image in new[] { body, triggerLeft, triggerRight, capLeft, capRight }) Children.Add(image);
        Children.Add(outline);
        capLeft.RenderTransform = new TransformGroup { Children = { leftTilt, leftMove } };
        capRight.RenderTransform = new TransformGroup { Children = { rightTilt, rightMove } };
        capLeft.Effect = leftShadow; capRight.Effect = rightShadow; capLeft.CacheMode = new BitmapCache(1); capRight.CacheMode = new BitmapCache(1);
        triggerLeft.RenderTransform = ltPose; triggerRight.RenderTransform = rtPose; triggerLeft.Effect = ltGlow; triggerRight.Effect = rtGlow;
        Unloaded += (_, _) => StopRendering();
    }

    /// <summary>Where a hotspot sits on the 1536x1024 art. Stick and stick-click circles are centred on the stick
    /// pivot measured from the layered art, so the highlight ring sits exactly around the cap.</summary>
    public static Point Place(string model, X20Ctl.Zone.ControlRegion region)
    {
        var info = Manifest().GetValueOrDefault(model);
        bool left = region.Key is "L3" or "LSTICK_ANALOG", right = region.Key is "R3" or "RSTICK_ANALOG";
        if (info != null && region.View == "front" && (left || right))
        {
            var c = (left ? info.Left : info.Right).Center;
            return new(c.X - region.Bounds.Width * 1536 / 2, c.Y - region.Bounds.Height * 1024 / 2);
        }
        return new(region.Bounds.X * 1536, region.Bounds.Y * 1024);
    }

    public void Show(string model, bool showRear)
    {
        ModelId = model; rear = showRear; info = Manifest().GetValueOrDefault(model); outline.Show(model, showRear); ControllerOutline.Trim(body, model, showRear);
        string dir = $"Assets/native/{model}/native/";
        body.Source = Bitmap(dir + (rear ? "rear-body.png" : "front-body.png"));
        capLeft.Source = rear ? null : Bitmap(dir + "stick-l.png"); capRight.Source = rear ? null : Bitmap(dir + "stick-r.png");
        triggerLeft.Source = rear ? Bitmap(dir + "trigger-l.png") : null; triggerRight.Source = rear ? Bitmap(dir + "trigger-r.png") : null;
        Apply();
    }

    /// <summary>Sticks are -1..1 with +Y up; triggers 0..1.</summary>
    public void SetInput(double lx, double ly, double rx, double ry, double lt, double rt)
    {
        target[0] = Clamp(lx, -1, 1); target[1] = Clamp(ly, -1, 1); target[2] = Clamp(rx, -1, 1); target[3] = Clamp(ry, -1, 1);
        target[4] = Clamp(lt, 0, 1); target[5] = Clamp(rt, 0, 1);
        if (!Smooth) { Array.Copy(target, shown, 6); Apply(); return; }
        if (!rendering) { rendering = true; lastFrame = TimeSpan.Zero; CompositionTarget.Rendering += Tick; }
    }
    public void Release() => SetInput(0, 0, 0, 0, 0, 0);

    private void Tick(object? sender, EventArgs e)
    {
        var now = ((RenderingEventArgs)e).RenderingTime; if (now == lastFrame) return;
        double dt = lastFrame == TimeSpan.Zero ? 1 / 60.0 : Math.Min(.05, (now - lastFrame).TotalSeconds); lastFrame = now;
        double k = 1 - Math.Exp(-dt * 26); // fast, critically-damped follow: feels physical without lag
        bool settled = true;
        for (int i = 0; i < 6; i++) { shown[i] += (target[i] - shown[i]) * k; if (Math.Abs(target[i] - shown[i]) > .0008) settled = false; else shown[i] = target[i]; }
        Apply(); if (settled) StopRendering();
    }
    private void StopRendering() { if (!rendering) return; rendering = false; CompositionTarget.Rendering -= Tick; }

    private void Apply()
    {
        if (info == null) return;
        Cap(leftMove, leftTilt, leftShadow, info.Left, shown[0], shown[1]);
        Cap(rightMove, rightTilt, rightShadow, info.Right, shown[2], shown[3]);
        ltPose.Matrix = Press(info.LT, shown[4]); rtPose.Matrix = Press(info.RT, shown[5]);
        // the light belongs to the moving cap, so it travels with it instead of sitting on a fixed outline
        ltGlow.Opacity = shown[4] * .95; ltGlow.BlurRadius = 10 + shown[4] * 26; rtGlow.Opacity = shown[5] * .95; rtGlow.BlurRadius = 10 + shown[5] * 26;
    }

    private static void Cap(TranslateTransform move, SkewTransform tilt, DropShadowEffect shadow, StickInfo stick, double x, double y)
    {
        move.X = x * stick.Travel; move.Y = -y * stick.Travel;
        // a little lean in the travel direction reads as the cap tilting on its pivot
        tilt.CenterX = stick.Center.X; tilt.CenterY = stick.Center.Y; tilt.AngleX = -x * 5; tilt.AngleY = y * 5;
        double magnitude = Math.Min(1, Math.Sqrt(x * x + y * y));
        // shadow parameters stay fixed: changing them every frame would re-run the effect on a full-size layer
    }

    /// <summary>The cap rotates about its fixed hinge toward the viewer: in the rear view that
    /// foreshortens it perpendicular to the hinge line, anchored on the hinge.</summary>
    private static Matrix Press(TriggerInfo t, double value)
    {
        double angle = Math.Atan2(t.B.Y - t.A.Y, t.B.X - t.A.X) * 180 / Math.PI;
        double k = Math.Cos((t.Rest + t.Travel * value) * Math.PI / 180) / Math.Cos(t.Rest * Math.PI / 180);
        var mid = new Point((t.A.X + t.B.X) / 2, (t.A.Y + t.B.Y) / 2);
        var m = Matrix.Identity;
        m.RotateAt(-angle, mid.X, mid.Y); m.ScaleAt(1, k, mid.X, mid.Y); m.RotateAt(angle, mid.X, mid.Y);
        return m;
    }

    private static Dictionary<string, ModelInfo> Manifest()
    {
        if (manifest != null) return manifest;
        manifest = new();
        string path = Path.Combine(AppContext.BaseDirectory, "Data/native-layers.json");
        if (!File.Exists(path)) return manifest;
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var model in doc.RootElement.GetProperty("models").EnumerateObject())
        {
            StickInfo Stick(string side) { var s = model.Value.GetProperty("sticks").GetProperty(side); var c = s.GetProperty("center"); return new(new(c[0].GetDouble(), c[1].GetDouble()), s.GetProperty("travel").GetDouble()); }
            TriggerInfo Trigger(string key) { var t = model.Value.GetProperty("triggers").GetProperty(key); var h = t.GetProperty("hinge"); return new(new(h[0][0].GetDouble(), h[0][1].GetDouble()), new(h[1][0].GetDouble(), h[1][1].GetDouble()), t.GetProperty("restAngle").GetDouble(), t.GetProperty("travelAngle").GetDouble()); }
            manifest[model.Name] = new(Stick("left"), Stick("right"), Trigger("LT"), Trigger("RT"));
        }
        return manifest;
    }

    public static BitmapImage Bitmap(string resource)
    {
        if (cache.TryGetValue(resource, out var cached)) return cached;
        var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.UriSource = new Uri("pack://application:,,,/" + resource); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.EndInit(); bitmap.Freeze();
        return cache[resource] = bitmap;
    }
    private static Image Layer() { var image = new Image { Width = 1536, Height = 1024, Stretch = Stretch.Fill }; RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality); return image; }
    private static DropShadowEffect CapShadow() => new() { Color = Colors.Black, BlurRadius = 18, ShadowDepth = 4, Direction = 270, Opacity = .55, RenderingBias = RenderingBias.Performance };
    private static DropShadowEffect TriggerGlow() => new() { Color = Color.FromRgb(96, 170, 255), BlurRadius = 10, ShadowDepth = 0, Opacity = 0, RenderingBias = RenderingBias.Performance };
    private static double Clamp(double v, double lo, double hi) => double.IsNaN(v) ? 0 : Math.Clamp(v, lo, hi);
}
