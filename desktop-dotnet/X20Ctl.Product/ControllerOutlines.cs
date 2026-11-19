using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using X20Ctl.Zone;

namespace X20Ctl.Product;

/// <summary>
/// Shared traced controller silhouette and edge-island loader from <c>Data/vibration-outlines.json</c>
/// in the standard 1536×1024 controller artwork coordinate space.
/// </summary>
internal static class ControllerOutlines
{
    internal sealed record Island(Point[] Points, double[] Along, double Length, bool Left, bool Grip, bool Closed, Geometry Line);

    private static bool TryGetEntry(JsonElement section, string model, string finish, out JsonElement entry) =>
        section.TryGetProperty(model + "/" + finish, out entry) || section.TryGetProperty(model, out entry);

    public static Geometry? LoadSilhouette(string model, string finish = FinishCatalog.Original)
    {
        try
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Data", "vibration-outlines.json");
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (!doc.RootElement.TryGetProperty("silhouettes", out var all) || !TryGetEntry(all, model, finish, out var pts))
                return null;
            var a = pts.EnumerateArray().Select(q => new Point(q[0].GetDouble() * 1536, q[1].GetDouble() * 1024)).ToList();
            if (a.Count < 3) return null;
            var g = new StreamGeometry();
            using (var ctx = g.Open())
            {
                ctx.BeginFigure(a[0], true, true);
                ctx.PolyLineTo(a.Skip(1).ToList(), false, true);
            }
            g.Freeze();
            return g;
        }
        catch (Exception e) when (e is IOException or JsonException or InvalidOperationException)
        {
            return null;
        }
    }

    public static List<Island> LoadIslands(string model, string finish = FinishCatalog.Original)
    {
        var list = new List<Island>();
        try
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Data", "vibration-outlines.json");
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (!TryGetEntry(doc.RootElement.GetProperty("models"), model, finish, out var shapes))
                return list;
            foreach (var shape in shapes.EnumerateArray())
            {
                var pts = shape.GetProperty("points").EnumerateArray().Select(q => new Point(q[0].GetDouble() * 1536, q[1].GetDouble() * 1024)).ToList();
                if (pts.Count < 8) continue;
                bool closed = shape.TryGetProperty("closed", out var cl) && cl.GetBoolean();
                if (closed) pts.Add(pts[0]);
                var a = pts.ToArray();
                var along = new double[a.Length];
                for (int k = 1; k < a.Length; k++) along[k] = along[k - 1] + (a[k] - a[k - 1]).Length;
                var line = new StreamGeometry();
                using (var ctx = line.Open())
                {
                    ctx.BeginFigure(a[0], false, closed);
                    ctx.PolyLineTo(a.Skip(1).ToList(), true, true);
                }
                line.Freeze();
                list.Add(new Island(a, along, along[^1], shape.GetProperty("side").GetString() == "left", shape.GetProperty("part").GetString() == "grip", closed, line));
            }
        }
        catch (Exception e) when (e is IOException or JsonException or KeyNotFoundException or InvalidOperationException) { }
        return list;
    }
}
