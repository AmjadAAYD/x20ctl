using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using X20Ctl.Zone;
namespace X20Ctl.Product;

/// <summary>Owned-silhouette visual preview driven by a local draft, never motor telemetry.
/// Since 10 Oct 2026 (owner: "just keep it as a fast heartbeat") each motor's outline beats, lub-dub, brightening and
/// swelling a touch on each thump, faster as the strength rises (72 to 180 bpm). Both grips beat together; only the
/// X20 Pro and the Dune have trigger motors, so only they beat on the triggers too. The body stays still.
/// Reduced motion shows the same picture held still.</summary>
public sealed class VibrationVisual : Grid
{
    private readonly bool animations;
    private readonly Flow behind, over;
    private int strength;
    private TimeSpan started, last;
    private bool rendering;
    private readonly TranslateTransform shake = new();
    public int Strength => strength;
    public bool IsRunning { get; private set; }
    public bool ReducedMotion => !animations;
    public VibrationVisualProfile Profile => VibrationVisualProfile.For(strength);
    public static readonly DependencyProperty PhaseProperty = DependencyProperty.Register(nameof(Phase), typeof(double), typeof(VibrationVisual), new FrameworkPropertyMetadata(0d));
    public double Phase { get => (double)GetValue(PhaseProperty); set => SetValue(PhaseProperty, value); }
    /// <summary>Preview motor levels 0..1 (heavy left, light right), for the page's meters.</summary>
    public event Action<double, double>? Levels;

    public VibrationVisual(Geometry outline, bool animate, string model = "x20")
    {
        animations = animate; IsHitTestVisible = false; Width = 1536; Height = 1024;
        var tracks = Flow.Load(model);
        var rim = Flow.LoadRim(model);
        // the strokes follow the edge of the colour the pad is shown in (each colour's shell is traced on its own)
        string finish = PadColours.Active(model);
        var islands = Flow.Islands(model, finish);
        behind = new Flow(tracks, false) { Rim = rim }; over = new Flow(tracks, true) { Rim = rim, Parts = islands, Palette = [Color.FromRgb(246,191,105),Color.FromRgb(255,229,187)], Body = Flow.Silhouette(model, finish) };
        Children.Add(behind); Children.Add(new PhotoController(model)); Children.Add(over);
        RenderTransform = shake;
        Loaded += (_, _) => UpdateClock(); Unloaded += (_, _) => Stop(); IsVisibleChanged += (_, _) => UpdateClock();
    }
    public void SetRegion(string region){over.Region=region;over.InvalidateVisual();}
    public void SetStrength(int percent) { if (percent == strength) return; strength = percent; UpdateClock(); }

    private void Stop()
    {
        if (rendering) { CompositionTarget.Rendering -= Tick; rendering = false; }
        IsRunning = false; Phase = 0; shake.X = shake.Y = 0;
        behind.Draw(strength, 0, false); over.Draw(strength, 0, false); Levels?.Invoke(0, 0);
    }
    private void UpdateClock()
    {
        Stop();
        if (strength == 0 || !IsVisible) return;
        if (!animations) { double s = strength / 100.0; Levels?.Invoke(s, s); return; }
        IsRunning = true; started = TimeSpan.Zero; rendering = true; CompositionTarget.Rendering += Tick;
    }
    private void Tick(object? sender, EventArgs e)
    {
        var now = ((RenderingEventArgs)e).RenderingTime; if (now == last) return; last = now;
        if (started == TimeSpan.Zero) started = now;
        double t = (now - started).TotalSeconds, s = strength / 100.0;
        Phase = t % 1;
        behind.Draw(strength, t, true); over.Draw(strength, t, true);
        // the meters breathe with the preview: the heavy motor rolls slowly, the light one flutters
        // a motor holds a level: the meters are fixed at the strength, and the picture shakes, harder as it rises
        Levels?.Invoke(s, s);

    }

    /// <summary>A contour resampled at an even step, with outward normals, so light can travel along it smoothly.</summary>
    private sealed class Track
    {
        public required Point[] Points; public required Vector[] Normals; public required Geometry Shape; public required Point Center;
        public required int Direction; public required bool Grip;
    }

    /// <summary>The light itself. The back layer is the warmth behind each grip; the front layer is the living outline.</summary>
    private sealed class Flow : FrameworkElement
    {
        private readonly List<Track> tracks; private readonly bool front;
        public string Region {get;set;}="Both grips";
        private int strength; private double time; private bool moving;
        private static readonly Color Deep = Color.FromRgb(56, 132, 255), Ice = Color.FromRgb(170, 214, 255), White = Color.FromRgb(240, 247, 255);
        public Track? Rim { get; init; }
        public List<Island> Parts { get; init; } = [];

        /// <summary>One of the four separate pieces that light up: a grip pad's loop or a bumper cap, resampled for the
        /// travelling pulse, with its base line built once.</summary>
        public sealed record Island(Point[] Points, double[] Along, double Length, bool Left, bool Grip, bool Closed, Geometry Line);

        // owner, 13 Oct 2026: the line follows the controller's own outline, never a drawn shape. Each stroke is the stretch
        // of the photo's silhouette round a grip (and the bumper tops on the X20 Pro and X15), set just inside the edge and
        // smoothed. Fractions of the 1536 x 1024 photo, so they scale with it.
        /// <summary>The controller's exact outline from its photo. The light is clipped to it, so it lives on the controller's
        /// edge like an edge-lit bevel and never bleeds out into the background.</summary>
        public Geometry? Body { get; init; }
        /// <summary>A colour's own entry ("model/colour") when it has one, else the model's.</summary>
        private static bool Entry(System.Text.Json.JsonElement section, string model, string finish, out System.Text.Json.JsonElement entry) =>
            section.TryGetProperty(model + "/" + finish, out entry) || section.TryGetProperty(model, out entry);
        public static Geometry? Silhouette(string model, string finish = X20Ctl.Zone.FinishCatalog.Original)
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "Data", "vibration-outlines.json")));
                if (!doc.RootElement.TryGetProperty("silhouettes", out var all) || !Entry(all, model, finish, out var pts)) return null;
                var a = pts.EnumerateArray().Select(q => new Point(q[0].GetDouble() * 1536, q[1].GetDouble() * 1024)).ToList();
                var g = new StreamGeometry();
                using (var ctx = g.Open()) { ctx.BeginFigure(a[0], true, true); ctx.PolyLineTo(a.Skip(1).ToList(), false, true); }
                g.Freeze(); return g;
            }
            catch (Exception e) when (e is System.IO.IOException or System.Text.Json.JsonException or InvalidOperationException) { return null; }
        }
        public static List<Island> Islands(string model, string finish = X20Ctl.Zone.FinishCatalog.Original)
        {
            var list = new List<Island>();
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "Data", "vibration-outlines.json")));
                if (!Entry(doc.RootElement.GetProperty("models"), model, finish, out var shapes)) return list;
                foreach (var shape in shapes.EnumerateArray())
                {
                    var pts = shape.GetProperty("points").EnumerateArray().Select(q => new Point(q[0].GetDouble() * 1536, q[1].GetDouble() * 1024)).ToList();
                    if (pts.Count < 8) continue;
                    bool closed = shape.TryGetProperty("closed", out var cl) && cl.GetBoolean();
                    if (closed) pts.Add(pts[0]);
                    var a = pts.ToArray(); var along = new double[a.Length];
                    for (int k = 1; k < a.Length; k++) along[k] = along[k - 1] + (a[k] - a[k - 1]).Length;
                    var line = new StreamGeometry();
                    using (var ctx = line.Open()) { ctx.BeginFigure(a[0], false, closed); ctx.PolyLineTo(a.Skip(1).ToList(), true, true); }
                    line.Freeze();
                    list.Add(new Island(a, along, along[^1], shape.GetProperty("side").GetString() == "left", shape.GetProperty("part").GetString() == "grip", closed, line));
                }
            }
            catch (Exception e) when (e is System.IO.IOException or System.Text.Json.JsonException or KeyNotFoundException or InvalidOperationException) { }
            return list;
        }
        /// <summary>Catmull-Rom through the traced points, so the contour reads as fitted, not hand drawn.</summary>
        private static Point[] Smooth(Point[] p, bool closed)
        {
            var o = new List<Point>(); int m = p.Length, steps = 12;
            for (int i = 0; i < (closed ? m : m - 1); i++)
            {
                Point p0 = closed || i > 0 ? p[(i - 1 + m) % m] : p[0], p1 = p[i], p2 = p[(i + 1) % m], p3 = closed || i + 2 < m ? p[(i + 2) % m] : p[m - 1];
                for (int k = 0; k < steps; k++)
                {
                    double t = (double)k / steps, t2 = t * t, t3 = t2 * t;
                    o.Add(new Point(.5 * (2 * p1.X + (-p0.X + p2.X) * t + (2 * p0.X - 5 * p1.X + 4 * p2.X - p3.X) * t2 + (-p0.X + 3 * p1.X - 3 * p2.X + p3.X) * t3),
                                    .5 * (2 * p1.Y + (-p0.Y + p2.Y) * t + (2 * p0.Y - 5 * p1.Y + 4 * p2.Y - p3.Y) * t2 + (-p0.Y + 3 * p1.Y - 3 * p2.Y + p3.Y) * t3)));
                }
            }
            o.Add(closed ? p[0] : p[m - 1]); return o.ToArray();
        }
        /// <summary>The controller's whole outline (the lighting silhouette), resampled so light can run round it.</summary>
        public static Track? LoadRim(string model)
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "Data", "lighting.json")));
                string d = doc.RootElement.GetProperty("models").GetProperty(model).GetProperty("front").GetString()!;
                return Build(Geometry.Parse(d), 1, false);
            }
            catch (Exception e) when (e is System.IO.IOException or System.Text.Json.JsonException or KeyNotFoundException or FormatException or InvalidOperationException) { return null; }
        }
        private static Color Hue(double t)
        {
            t -= Math.Floor(t);
            Color[] stops = { Color.FromRgb(70, 225, 255), Color.FromRgb(70, 120, 255), Color.FromRgb(255, 90, 215), Color.FromRgb(255, 160, 60), Color.FromRgb(70, 225, 255) };
            double x = t * 4; int i = (int)x; return Mix(stops[i], stops[Math.Min(i + 1, 4)], x - i);
        }
        public Flow(List<Track> tracks, bool front) { this.tracks = tracks; this.front = front; Width = 1536; Height = 1024; IsHitTestVisible = false; }
        public void Draw(int s, double t, bool animate) { strength = s; time = t; moving = animate; InvalidateVisual(); }

        /// <summary>The motors live in the grips (and the triggers, on models that have trigger motors): the model's
        /// haptics contours (src/assets/controllers/&lt;model&gt;/haptics-geometry.json) are where the light runs.</summary>
        public static List<Track> Load(string model)
        {
            var list = new List<Track>();
            try
            {
                string path = System.IO.Path.Combine(AppContext.BaseDirectory, "Data", "haptics", model, "haptics-geometry.json");
                using var doc = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(path));
                var c = doc.RootElement.GetProperty("contours");
                bool triggers = model is "x20_pro" or "dune"; // owner, 10 Oct 2026: every other model has the same two grip rotors
                foreach (var (key, direction, grip) in new[] { ("left_grip", 1, true), ("right_grip", -1, true), ("left_trigger", 1, false), ("right_trigger", -1, false) }.Where(k => k.Item3 || triggers))
                    if (c.TryGetProperty(key, out var data) && data.GetString() is { Length: > 0 } text && Build(Geometry.Parse(text), direction, grip) is { } track) list.Add(track);
            }
            catch (Exception e) when (e is System.IO.IOException or System.Text.Json.JsonException or KeyNotFoundException or FormatException or InvalidOperationException) { }
            return list;
        }
        private static Track? Build(Geometry shape, int direction, bool grip)
        {
            shape.Freeze();
            var flat = shape.GetFlattenedPathGeometry(.5, ToleranceType.Absolute);
            var raw = new List<Point>();
            foreach (var figure in flat.Figures)
            {
                if (raw.Count > 0) break; // one closed contour per motor
                raw.Add(figure.StartPoint);
                foreach (var segment in figure.Segments)
                    if (segment is PolyLineSegment poly) raw.AddRange(poly.Points); else if (segment is LineSegment line) raw.Add(line.Point);
            }
            if (raw.Count < 4) return null;
            raw.Add(raw[0]);
            // even 3 px steps
            const double step = 3;
            var points = new List<Point> { raw[0] }; double carry = 0;
            for (int i = 1; i < raw.Count; i++)
            {
                Vector d = raw[i] - raw[i - 1]; double len = d.Length; if (len < 1e-6) continue; d /= len;
                double at = step - carry;
                while (at <= len) { points.Add(raw[i - 1] + d * at); at += step; }
                carry = len - (at - step);
            }
            if (points.Count < 8) return null;
            var b = shape.Bounds; var center = new Point(b.X + b.Width / 2, b.Y + b.Height / 2);
            var normals = new Vector[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                Vector t = points[(i + 1) % points.Count] - points[(i - 1 + points.Count) % points.Count];
                var n = new Vector(t.Y, -t.X); if (n.Length > 0) n.Normalize();
                if (Vector.Multiply(n, points[i] - center) < 0) n = -n;
                normals[i] = n;
            }
            return new Track { Points = points.ToArray(), Normals = normals, Shape = shape, Center = center, Direction = direction, Grip = grip };
        }

        /// <summary>A heartbeat, lub then dub: 0..1 over one beat period.</summary>
        public static double Beat(double t, double s)
        {
            double period = 60 / (72 + 108 * s); // 72 bpm at the lightest, 180 bpm at 100%
            double p = t % period / period;
            static double Thump(double x) => x < 0 ? 0 : (1 - Math.Exp(-x / .018)) * Math.Exp(-x / .09);
            return Math.Clamp((Thump(p) + .62 * Thump(p - .30)) / .78, 0, 1);
        }
        protected override void OnRender(DrawingContext dc)
        {
            if (strength == 0 || tracks.Count == 0) return;
            // owner direction 10 Oct 2026: no comets or ripples, just a fast heartbeat on each motor's outline
            double s = strength / 100.0;
            if (!front || Parts.Count == 0) return;
            // owner's references, 13 Oct 2026: the controller's edge round each grip glows like a lit edge, a smooth gradient
            // of the model's own LED colours that fades out at both ends, and over it a brighter neon pulse travels down. Strength sets how fast and how often the pulse runs and how bright it is.
            // The bumper outlines echo their grip about 80 ms later. Today one strength drives both sides; each side keeps its
            // own clock so separate motor values can drive them apart.
            double left = s, right = s;
            if (Body != null) dc.PushClip(Body);
            foreach (var part in Parts)
            {
                if(Region!="All" && (Region=="Triggers"?part.Grip:!part.Grip))continue;
                if(Region=="Left grip"&&!part.Left || Region=="Right grip"&&part.Left)continue;
                double m = part.Left ? left : right; if (m <= 0) continue;
                double railCore = part.Grip ? 5 : 3.8, lit = .6 + .4 * m;
                EdgeGlow(dc, part, 60 * lit, railCore * 4, false);
                EdgeGlow(dc, part, 250 * lit, railCore, false);
                EdgeGlow(dc, part, 230 * lit, railCore * .4, true);
                double q = .4, period = 2.6 - 1.8 * m, travel = .7 + .25 * m;
                if (moving)
                {
                    double t = time - (part.Grip ? 0 : .08) + (part.Left ? 0 : .37);
                    double cycle = t / period + .04 * Math.Sin(t * .9 + (part.Left ? 0 : 2)), phase = cycle - Math.Floor(cycle);
                    q = phase / travel;
                    if (m > .6) Pulse(dc, part, ((phase - .5 + 1) % 1) / travel, m, .45 * (m - .6) / .4, time);
                }
                Pulse(dc, part, q, m, 1, moving ? time : 0);
            }
            if (Body != null) dc.Pop();
        }
        public Color[] Palette { get; init; } = [Color.FromRgb(80, 225, 255)];
        /// <summary>The colours of each model's own LEDs, read off its photo: strips, stick rings, the D-pad ring. The X10 and
        /// X15 show no lit LEDs, so they take the colour of their shell accents.</summary>
        public static Color[] LedColors(string model) => model switch
        {
            "x20" => [Color.FromRgb(255, 150, 45), Color.FromRgb(255, 60, 200), Color.FromRgb(80, 90, 255)],
            "x20_pro" => [Color.FromRgb(255, 90, 220), Color.FromRgb(160, 110, 255), Color.FromRgb(90, 230, 255)],
            "x05_pro" => [Color.FromRgb(255, 70, 200), Color.FromRgb(255, 215, 60), Color.FromRgb(80, 230, 255)],
            "x10" => [Color.FromRgb(110, 160, 255), Color.FromRgb(175, 210, 255)],
            "d10" => [Color.FromRgb(255, 190, 240), Color.FromRgb(255, 80, 210), Color.FromRgb(160, 90, 255)],
            "x15" => [Color.FromRgb(210, 175, 255), Color.FromRgb(150, 110, 255)],
            "dune" => [Color.FromRgb(170, 90, 255), Color.FromRgb(80, 140, 255), Color.FromRgb(90, 230, 255), Color.FromRgb(255, 100, 220)],
            _ => [Color.FromRgb(80, 225, 255)],
        };
        private static Color Rgb(Color[] stops, double t)
        {
            t -= Math.Floor(t); int n = stops.Length; if (n == 1) return stops[0];
            double x = t * n; int i = Math.Min((int)x, n - 1); return Mix(stops[i], stops[(i + 1) % n], x - i);
        }
        /// <summary>One pulse at progress q round the loop (outside 0..1 it is resting): a short bright strip of RGB light with
        /// feathered ends, a soft bloom under it, easing in as it starts and out as it ends.</summary>
        private void Pulse(DrawingContext dc, Island part, double q, double m, double weight, double time)
        {
            if (q is < 0 or > 1 || weight <= 0) return;
            // owner, 14 Oct 2026: the moving light is solid, so the line underneath never shows through it. Its tips taper in
            // width (never in opacity) down to the line's own width, so it stays covered end to end.
            double span = part.Length * (part.Grip ? .55 : .6), head = part.Closed ? part.Length * q : (part.Length + span) * q;
            double core = part.Grip ? 8.5 : 6, rail = part.Grip ? 6.5 : 4.5;
            for (int i = 0; i + 1 < part.Points.Length; i++)
            {
                double behind = head - part.Along[i]; if (behind < 0) { if (!part.Closed) continue; behind += part.Length; } // a loop: the tail wraps past the start
                if (behind > span) continue;
                double x = 1 - behind / span, taper = Math.Clamp(Math.Min(x, 1 - x) / .12, 0, 1); taper = taper * taper * (3 - 2 * taper);
                double width = rail + (core - rail) * taper;
                var c = Rgb(Palette, part.Along[i] / part.Length * .9 + time * .05);
                Neon(dc, c, 70 * taper * weight * (.6 + .4 * m), width * 2.6, part.Points[i], part.Points[i + 1]);
                Neon(dc, c, 255, width, part.Points[i], part.Points[i + 1]);
                Neon(dc, Mix(c, Colors.White, .7), 255, width * .38, part.Points[i], part.Points[i + 1]);
            }
        }        /// <summary>A polyline through <paramref name="count"/> points starting at <paramref name="start"/>, wrapping round the contour.</summary>
        private static StreamGeometry Poly(Point[] points, int start, int count, bool closed)
        {
            int n = points.Length; var g = new StreamGeometry();
            using (var ctx = g.Open())
            {
                ctx.BeginFigure(points[((start % n) + n) % n], false, closed);
                var rest = new List<Point>(count);
                for (int i = 1; i < count; i++) rest.Add(points[(((start + i) % n) + n) % n]);
                ctx.PolyLineTo(rest, true, true);
            }
            g.Freeze(); return g;
        }
        /// <summary>The whole stroke as one lit edge: the palette spread along it from its top end, fading to nothing at both
        /// ends. One geometry per layer, so the glow has no seams or beads.</summary>
        private void EdgeGlow(DrawingContext dc, Island part, double alpha, double width, bool hot)
        {
            // the grips run down the outer edge, round the bottom and up the inner edge, so the colours run top to bottom
            var low = part.Points.MaxBy(q => q.Y);
            var brush = new LinearGradientBrush { MappingMode = BrushMappingMode.Absolute, StartPoint = part.Points[0], EndPoint = part.Grip ? low : part.Points[^1] };
            for (int k = 0; k <= 10; k++)
            {
                double t = k / 10.0, fade = part.Closed ? 1 : Math.Min(1, (part.Grip ? t : Math.Min(t, 1 - t)) / .14);
                var c = Rgb(Palette, t * .9); if (hot) c = Mix(c, Colors.White, .75);
                brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)Math.Clamp(alpha * fade * fade * (3 - 2 * fade), 0, 255), c.R, c.G, c.B), t));
            }
            dc.DrawGeometry(null, new Pen(brush, width) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, part.Line);
        }
        private static void Neon(DrawingContext dc, Color c, double alpha, double width, Point a, Point b) =>
            dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb((byte)Math.Clamp(alpha, 0, 255), c.R, c.G, c.B)), width) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, a, b);
        private static Color Mix(Color a, Color b, double k) => Color.FromRgb((byte)(a.R + (b.R - a.R) * k), (byte)(a.G + (b.G - a.G) * k), (byte)(a.B + (b.B - a.B) * k));
    }
}
