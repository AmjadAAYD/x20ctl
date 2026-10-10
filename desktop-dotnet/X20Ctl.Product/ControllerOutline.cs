using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
namespace X20Ctl.Product;

/// <summary>
/// A crisp outline on each controller's own silhouette (owner direction 10 Oct 2026: hide the cut-out's rough,
/// pixelated edge). The silhouettes in lighting.json are 2-pixel-step polylines; they are smoothed (Chaikin, three
/// rounds) into a clean curve and drawn as a fine light rim exactly on the edge of the 1536x1024 art, in the
/// same space every controller picture uses.
/// </summary>
/// <summary>Each controller's whole picture, front or back. It is the same file the live layers come from, so the app carries
/// every picture once (the old Assets/controllers copies doubled the download).</summary>
public static class ControllerArt
{
    public static System.Windows.Media.Imaging.BitmapImage Front(string model) => LiveController.Bitmap($"Assets/native/{model}/native/front-full.png");
    public static System.Windows.Media.Imaging.BitmapImage Rear(string model) => LiveController.Bitmap($"Assets/native/{model}/native/rear-full.png");
}

public sealed class ControllerOutline : FrameworkElement
{
    private static readonly Dictionary<string, Geometry?> cache = new();
    private Geometry geometry = Geometry.Empty;
    // a crisp edge, no glow (owner, 10 Oct 2026: the soft halo read as fog): a dark line seats the controller on the
    // page and covers the cut-out's soft band, a fine light line on top of it catches the light from above
    private static readonly Pen Seat = Freeze(new Pen(new SolidColorBrush(Color.FromArgb(200, 6, 11, 24)), 4.2) { LineJoin = PenLineJoin.Round });
    private static readonly Pen Catch = Freeze(new Pen(new LinearGradientBrush(Color.FromArgb(220, 228, 236, 250), Color.FromArgb(90, 150, 170, 205), 90), 1.3) { LineJoin = PenLineJoin.Round });
    private static Pen Freeze(Pen pen) { pen.Freeze(); return pen; }

    public ControllerOutline() { Width = 1536; Height = 1024; IsHitTestVisible = false; }
    /// <summary>Point the outline at a model and view; unknown models simply draw nothing.</summary>
    public void Show(string model, bool rear) { geometry = For(model, rear) ?? Geometry.Empty; InvalidateVisual(); }
    protected override void OnRender(DrawingContext dc) { if (geometry.IsEmpty()) return; dc.DrawGeometry(null, Seat, geometry); dc.DrawGeometry(null, Catch, geometry); }
    /// <summary>Trims an art layer to the traced outline, so the cut-out's pale, jagged fringe never shows outside the rim.</summary>
    public static void Trim(UIElement art, string model, bool rear) => art.Clip = For(model, rear);

    public static Geometry? For(string model, bool rear)
    {
        string key = model + (rear ? ":back" : ":front");
        if (cache.TryGetValue(key, out var known)) return known;
        Geometry? result = null;
        try
        {
            // outlines.json is traced from the art itself (tools/build_controller_outlines.py); lighting.json is the coarse fallback
            foreach (var (file, rounds) in new[] { ("outlines.json", 1), ("lighting.json", 3) })
            {
                string source = System.IO.Path.Combine(AppContext.BaseDirectory, "Data", file); if (!File.Exists(source)) continue;
                using var doc = JsonDocument.Parse(File.ReadAllText(source));
                if (doc.RootElement.GetProperty("models").TryGetProperty(model, out var m) && m.TryGetProperty(rear ? "back" : "front", out var path) && (result = Smooth(path.GetString() ?? "", rounds)) != null) break;
            }
        }
        catch (Exception e) when (e is IOException or JsonException or KeyNotFoundException or InvalidOperationException) { }
        return cache[key] = result;
    }

    /// <summary>Each "M x y L x y …" contour becomes a closed curve: Chaikin corner-cutting rounds off the stair steps.</summary>
    private static Geometry? Smooth(string data, int rounds)
    {
        var figures = new List<List<Point>>();
        foreach (Match part in Regex.Matches(data, @"([ML])\s*(-?[\d.]+)[\s,]+(-?[\d.]+)|([Zz])"))
        {
            if (part.Groups[4].Success) continue;
            var p = new Point(double.Parse(part.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture), double.Parse(part.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture));
            if (part.Groups[1].Value == "M" || figures.Count == 0) figures.Add(new());
            figures[^1].Add(p);
        }
        figures = figures.Where(f => f.Count >= 8).ToList();
        if (figures.Count == 0) return null;
        var g = new StreamGeometry();
        using (var ctx = g.Open())
            foreach (var raw in figures)
            {
                var pts = raw;
                for (int round = 0; round < rounds; round++)
                {
                    var next = new List<Point>(pts.Count * 2);
                    for (int i = 0; i < pts.Count; i++)
                    {
                        Point a = pts[i], b = pts[(i + 1) % pts.Count];
                        next.Add(new(a.X * .75 + b.X * .25, a.Y * .75 + b.Y * .25)); next.Add(new(a.X * .25 + b.X * .75, a.Y * .25 + b.Y * .75));
                    }
                    pts = next;
                }
                ctx.BeginFigure(pts[0], true, true); ctx.PolyLineTo(pts.Skip(1).ToList(), true, true);
            }
        g.Freeze(); return g;
    }
}
