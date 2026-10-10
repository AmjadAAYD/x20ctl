using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
namespace X20Ctl.Product;

/// <summary>
/// The controller as a physical object. In Showcase mode it is a real 3D body (photo front face,
/// side walls extruded from the model's own silhouette, stick caps floating slightly forward) that
/// sways left and right. ToHologram() turns it to face the viewer and sweeps a scan line down it,
/// leaving the model-specific hologram. Lives in the 1536x1024 source space like LiveController.
/// </summary>
public sealed class ControllerShowcase : Grid
{
    private const double W = 1536, H = 1024, Depth = 70, CapLift = 18, Fov = 30;
    private readonly Viewport3D view = new() { Width = W, Height = H, ClipToBounds = false };
    private readonly AxisAngleRotation3D yaw = new(new Vector3D(0, 1, 0), 0), pitch = new(new Vector3D(1, 0, 0), 0);
    private readonly TranslateTransform3D bob = new(), leftCap = new(), rightCap = new();
    private readonly Image holo = new() { Width = W, Height = H, Opacity = 0, IsHitTestVisible = false };
    private readonly Rectangle scan = new() { Width = W, Height = 26, VerticalAlignment = VerticalAlignment.Top, Opacity = 0, IsHitTestVisible = false };
    private readonly TranslateTransform scanMove = new();
    private readonly Ellipse floor = new() { Width = 900, Height = 70, VerticalAlignment = VerticalAlignment.Bottom, Margin = new(0, 0, 0, 40), IsHitTestVisible = false };
    private readonly LinearGradientBrush photoMask = Mask(true), holoMask = Mask(false);
    private bool swaying, motion = DS.Motion; private TimeSpan start; private double swayWeight = 1, manualYaw = double.NaN;
    private double travelL = 36, travelR = 36;
    public string ModelId { get; private set; } = "";
    /// <summary>Fixed presentation angle (degrees, negative turns the face to the left). The float keeps running.</summary>
    public double Tilt { get; set; }
    public bool IsHologram { get; private set; }

    public ControllerShowcase()
    {
        Width = W; Height = H; IsHitTestVisible = false;
        floor.Fill = new RadialGradientBrush(Color.FromArgb(150, 0, 2, 8), Color.FromArgb(0, 0, 2, 8));
        Children.Add(floor);
        view.Camera = new PerspectiveCamera(new Point3D(0, 0, W / 2 / Math.Tan(Fov / 2 * Math.PI / 180)), new Vector3D(0, 0, -1), new Vector3D(0, 1, 0), Fov);
        view.OpacityMask = photoMask; Children.Add(view);
        holo.OpacityMask = holoMask; RenderOptions.SetBitmapScalingMode(holo, BitmapScalingMode.HighQuality); Children.Add(holo);
        scan.Fill = new LinearGradientBrush(new GradientStopCollection { new(Color.FromArgb(0, 120, 190, 255), 0), new(Color.FromArgb(230, 190, 230, 255), .5), new(Color.FromArgb(0, 120, 190, 255), 1) }, 90);
        scan.Effect = new DropShadowEffect { Color = Color.FromRgb(80, 160, 255), BlurRadius = 30, ShadowDepth = 0, Opacity = 1 };
        scan.RenderTransform = scanMove; Children.Add(scan);
        Loaded += (_, _) => { if (swaying && !IsHologram) StartSway(); };
        Unloaded += (_, _) => StopSway();
    }

    public void Show(string model, bool reducedMotion = false)
    {
        ModelId = model; motion = !reducedMotion && DS.Motion; IsHologram = false;
        string dir = $"Assets/native/{model}/native/";
        var outline = Silhouette(model);
        var group = new Model3DGroup();
        group.Children.Add(new AmbientLight(Color.FromRgb(196, 200, 212)));
        group.Children.Add(new DirectionalLight(Color.FromRgb(120, 128, 150), new Vector3D(-.5, -.35, -1)));
        group.Children.Add(new DirectionalLight(Color.FromRgb(46, 70, 120), new Vector3D(.8, .2, -.4)));
        group.Children.Add(Walls(outline));                                           // drawn first: the face's transparent edge must blend over them
        group.Children.Add(Face(LiveController.Bitmap(dir + "front-body.png"), 0, null));
        var (l, r) = Sticks(model);
        group.Children.Add(Face(LiveController.Bitmap(dir + "stick-l.png"), CapLift, leftCap));
        group.Children.Add(Face(LiveController.Bitmap(dir + "stick-r.png"), CapLift, rightCap));
        travelL = l; travelR = r;
        var transform = new Transform3DGroup(); transform.Children.Add(new RotateTransform3D(pitch)); transform.Children.Add(new RotateTransform3D(yaw)); transform.Children.Add(bob);
        group.Transform = transform;
        view.Children.Clear(); view.Children.Add(new ModelVisual3D { Content = group });
        holo.Source = LiveController.Bitmap(dir + "holo-front.png");
        SetSweep(0); holo.Opacity = 0; scan.Opacity = 0;
        swaying = true; if (IsLoaded) StartSway();
    }

    /// <summary>Stick deflection, -1..1 with +Y up. Caps move inside the 3D body, so they keep their parallax.</summary>
    public void SetSticks(double lx, double ly, double rx, double ry) { leftCap.OffsetX = lx * travelL; leftCap.OffsetY = ly * travelL; rightCap.OffsetX = rx * travelR; rightCap.OffsetY = ry * travelR; }
    /// <summary>Freeze at a yaw angle (review / pointer drag); NaN resumes the sway.</summary>
    public void SetYaw(double degrees) { manualYaw = degrees; if (!double.IsNaN(degrees)) { yaw.Angle = degrees; pitch.Angle = 0; bob.OffsetY = 0; UpdateFloor(0); } }

    /// <summary>Review hook: render the scan transition at an exact progress (0 = photo, 1 = hologram).</summary>
    internal void PreviewSweep(double v) { StopSway(); yaw.Angle = pitch.Angle = 0; bob.OffsetY = 0; holo.Opacity = v > 0 ? 1 : 0; SetSweep(v); scan.BeginAnimation(OpacityProperty, null); scan.Opacity = v is > 0 and < 1 ? 1 : 0; scanMove.Y = v * (H - 40); }

    public Task ToHologram()
    {
        if (IsHologram) return Task.CompletedTask; IsHologram = true;
        var done = new TaskCompletionSource();
        if (!motion) { StopSway(); yaw.Angle = pitch.Angle = 0; SetSweep(1); holo.Opacity = 1; done.SetResult(); return done.Task; }
        // 1. settle to face the viewer (the sway weight eases out so there is no snap)
        Animate(v => swayWeight = 1 - v, 420, () =>
        {
            StopSway();
            // 2. scan line sweeps down; above it the photo dissolves into the hologram
            holo.Opacity = 1; scan.Opacity = 1;
            Animate(v => { SetSweep(v); scanMove.Y = v * (H - 40); }, 760, () => { scan.BeginAnimation(OpacityProperty, DS.To(0, 260)); done.SetResult(); }, new SineEase { EasingMode = EasingMode.EaseInOut });
        });
        return done.Task;
    }

    public Task ToPhoto()
    {
        if (!IsHologram) return Task.CompletedTask; IsHologram = false;
        var done = new TaskCompletionSource();
        if (!motion) { SetSweep(0); holo.Opacity = 0; StartSway(); done.SetResult(); return done.Task; }
        scan.BeginAnimation(OpacityProperty, null); scan.Opacity = 1;
        Animate(v => { SetSweep(1 - v); scanMove.Y = (1 - v) * (H - 40); }, 620, () => { scan.Opacity = 0; holo.Opacity = 0; swayWeight = 0; StartSway(); Animate(v => swayWeight = v, 700, null); done.SetResult(); }, new SineEase { EasingMode = EasingMode.EaseInOut });
        return done.Task;
    }

    // ----- sway -----
    private void StartSway() { if (!motion) { yaw.Angle = 0; UpdateFloor(0); return; } if (rendering) return; rendering = true; start = TimeSpan.Zero; CompositionTarget.Rendering += Tick; }
    private void StopSway() { if (!rendering) return; rendering = false; CompositionTarget.Rendering -= Tick; }
    private bool rendering;
    private void Tick(object? sender, EventArgs e)
    {
        var now = ((RenderingEventArgs)e).RenderingTime; if (start == TimeSpan.Zero) start = now;
        if (!double.IsNaN(manualYaw)) return;
        double t = (now - start).TotalSeconds;
        // weightless float: a slow rise and fall, nothing else (the owner rejected the left/right turn)
        double lift = (Math.Sin(t * 2 * Math.PI / 5.6) * .5 + .5) * swayWeight;
        yaw.Angle = Tilt; pitch.Angle = Tilt == 0 ? 0 : 6; bob.OffsetY = lift * 22;
        UpdateFloor(lift);
    }    private readonly ScaleTransform floorScale = new(1, 1, 450, 35);
    /// <summary>The floor shadow tightens and fades as the controller rises.</summary>
    private void UpdateFloor(double lift) { floor.RenderTransform = floorScale; floor.Opacity = .8 - lift * .3; floorScale.ScaleX = floorScale.ScaleY = 1 - lift * .12; }

    // ----- scan sweep: one gradient boundary shared by both masks -----
    private void SetSweep(double v)
    {
        double a = Math.Clamp(v, 0, 1) * 1.04 - .02, b = a + .02;
        photoMask.GradientStops[1].Offset = photoMask.GradientStops[0].Offset = Math.Clamp(a, 0, 1); photoMask.GradientStops[2].Offset = photoMask.GradientStops[3].Offset = Math.Clamp(b, 0, 1);
        holoMask.GradientStops[1].Offset = holoMask.GradientStops[0].Offset = Math.Clamp(a, 0, 1); holoMask.GradientStops[2].Offset = holoMask.GradientStops[3].Offset = Math.Clamp(b, 0, 1);
    }
    private static LinearGradientBrush Mask(bool photo)
    {
        Color on = Colors.White, off = Colors.Transparent;
        // photo is hidden above the boundary, the hologram is visible above it
        return new LinearGradientBrush(new GradientStopCollection { new(photo ? off : on, 0), new(photo ? off : on, 0), new(photo ? on : off, 0), new(photo ? on : off, 0) }, new Point(.5, 0), new Point(.5, 1));
    }
    private void Animate(Action<double> step, double ms, Action? done, IEasingFunction? ease = null)
    {
        ease ??= DS.EaseOut; var begin = DateTime.UtcNow;
        void Frame(object? s, EventArgs e)
        {
            double p = Math.Clamp((DateTime.UtcNow - begin).TotalMilliseconds / ms, 0, 1); step(ease.Ease(p));
            if (p >= 1) { CompositionTarget.Rendering -= Frame; done?.Invoke(); }
        }
        CompositionTarget.Rendering += Frame;
    }

    // ----- geometry -----
    private static Point3D P(double x, double y, double z) => new(x - W / 2, H / 2 - y, z);
    private static GeometryModel3D Face(BitmapSource image, double z, TranslateTransform3D? move)
    {
        var mesh = new MeshGeometry3D { Positions = { P(0, 0, z), P(W, 0, z), P(W, H, z), P(0, H, z) }, TextureCoordinates = { new(0, 0), new(1, 0), new(1, 1), new(0, 1) }, TriangleIndices = { 0, 3, 2, 0, 2, 1 } };
        var brush = new ImageBrush(image) { ViewportUnits = BrushMappingMode.Absolute, Viewport = new Rect(0, 0, 1, 1) }; RenderOptions.SetBitmapScalingMode(brush, BitmapScalingMode.HighQuality);
        var material = new DiffuseMaterial(brush);
        return new GeometryModel3D(mesh, material) { Transform = move };
    }
    private static GeometryModel3D Walls(IReadOnlyList<Point> outline)
    {
        var mesh = new MeshGeometry3D();
        for (int i = 0; i < outline.Count; i++)
        {
            var a = outline[i]; var b = outline[(i + 1) % outline.Count]; int k = mesh.Positions.Count;
            mesh.Positions.Add(P(a.X, a.Y, 0)); mesh.Positions.Add(P(b.X, b.Y, 0)); mesh.Positions.Add(P(b.X, b.Y, -Depth)); mesh.Positions.Add(P(a.X, a.Y, -Depth));
            mesh.TriangleIndices.Add(k); mesh.TriangleIndices.Add(k + 1); mesh.TriangleIndices.Add(k + 2); mesh.TriangleIndices.Add(k); mesh.TriangleIndices.Add(k + 2); mesh.TriangleIndices.Add(k + 3);
        }
        var shell = new MaterialGroup { Children = { new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(58, 64, 82))), new SpecularMaterial(new SolidColorBrush(Color.FromRgb(90, 110, 150)), 18) } };
        return new GeometryModel3D(mesh, shell) { BackMaterial = shell };
    }
    private static IReadOnlyList<Point> Silhouette(string model)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "Data/lighting.json")));
        string d = doc.RootElement.GetProperty("models").GetProperty(model).GetProperty("front").GetString()!;
        var nums = Regex.Matches(d, @"-?\d+(?:\.\d+)?").Select(m => double.Parse(m.Value, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        var pts = new List<Point>(); for (int i = 0; i + 1 < nums.Length; i += 2) pts.Add(new(nums[i], nums[i + 1]));
        // keep the outer contour only, inset by a pixel so the wall sits under the face's soft edge
        var c = new Point(pts.Average(p => p.X), pts.Average(p => p.Y));
        return pts.Select(p => { var v = p - c; double len = v.Length; return len > 2 ? c + v * ((len - 3) / len) : p; }).ToList();
    }
    private static (double, double) Sticks(string model)
    {
        string path = System.IO.Path.Combine(AppContext.BaseDirectory, "Data/native-layers.json"); if (!File.Exists(path)) return (36, 36);
        using var doc = JsonDocument.Parse(File.ReadAllText(path)); var s = doc.RootElement.GetProperty("models").GetProperty(model).GetProperty("sticks");
        return (s.GetProperty("left").GetProperty("travel").GetDouble(), s.GetProperty("right").GetProperty("travel").GetDouble());
    }
}
