using System.Text.Json;
namespace X20Ctl.Zone;

public enum ControlShape { Circle, RoundedRectangle, Polygon, Path }
public record RegionBounds(double X, double Y, double Width, double Height);
public record ControlRegion(string Key, ControlShape Shape, RegionBounds Bounds, IReadOnlyList<ControlPoint> Points, string? PathData = null, string? Transform = null, string View = "front", string Role = "button", ControlPoint? LabelAnchor = null);
public record ControllerLayout(string Model, string FrontArtwork, string BackArtwork, IReadOnlyList<ControlRegion> Controls);
public static class ControlRegions
{
    public static ControllerLayout Owned(ControllerModel model, string shapeJson, string rearJson)
    {
        using var source = JsonDocument.Parse(shapeJson);
        var item = source.RootElement.GetProperty("models").GetProperty(model.Id);
        var controls = new List<ControlRegion>();
        foreach (var shape in item.GetProperty("front").EnumerateObject()) controls.Add(ReadPath(shape.Name, shape.Value, "front", "button"));
        foreach (var shape in item.GetProperty("back").EnumerateObject()) controls.Add(ReadPath(shape.Name, shape.Value, "shoulder", "button"));
        foreach (var key in new[] { "L3", "R3", "LSTICK_ANALOG", "RSTICK_ANALOG", "HOME", "CAPTURE", "TURBO" })
        {
            if (!model.FrontButtons.TryGetValue(key, out var p)) continue;
            double radius = key.Contains("STICK") || key is "L3" or "R3" ? .049 : key == "HOME" ? .027 : .020;
            controls.Add(new(key, ControlShape.Circle, new(p.X - radius, p.Y - radius * 1.5, radius * 2, radius * 3), [], View: "front", Role: key.Contains("ANALOG") ? "axis" : key is "CAPTURE" or "TURBO" or "HOME" ? "system" : "button", LabelAnchor: p));
        }
        if (model.Id == "x20")
        {
            // Owner-approved rear labels govern; match smooth owned paths by physical location.
            var names = new Dictionary<string, string> { ["M1"] = "M1", ["M2"] = "M4", ["M3"] = "M2", ["M4"] = "M3" };
            foreach (var contour in Rear(rearJson))
            {
                var path = ReadPath(contour.Key, item.GetProperty("macros").GetProperty(names[contour.Key]), "back", "macro");
                controls.Add(path with { LabelAnchor = new(contour.Points.Average(p => p.X), contour.Points.Average(p => p.Y)) });
            }
        }
        else if(item.TryGetProperty("macros",out var rearControls))
        {
            foreach(var contour in rearControls.EnumerateObject())controls.Add(ReadPath(contour.Name,contour.Value,"back","macro"));
        }
        return new(model.Id, model.Asset, model.Id + "/controller-rear.png", controls.AsReadOnly());
    }
    private static ControlRegion ReadPath(string key, JsonElement shape, string view, string role)
    {
        var b = shape.GetProperty("bounds").EnumerateArray().Select(v => v.GetDouble()).ToArray();
        var bounds = new RegionBounds(b[0] / 1536, b[1] / 1024, b[2] / 1536, b[3] / 1024);
        return new(key, ControlShape.Path, bounds, [], shape.GetProperty("path").GetString(), shape.TryGetProperty("transform", out var transform) ? transform.GetString() : null, view, role, new(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2));
    }
    public static IReadOnlyList<ControlRegion> Front(ControllerModel model)
    {
        var result = new List<ControlRegion>();
        foreach (var (key, p) in model.FrontButtons)
        {
            if (key.StartsWith("DPAD_"))
            {
                var offsets = new[] { new ControlPoint(-.018, -.028), new ControlPoint(.018, -.028), new ControlPoint(.018, .022), new ControlPoint(.01, .035), new ControlPoint(-.01, .035), new ControlPoint(-.018, .022) };
                var points = offsets.Select(o => key switch {
                    "DPAD_DOWN" => new ControlPoint(p.X - o.X, p.Y - o.Y),
                    "DPAD_LEFT" => new ControlPoint(p.X + o.Y / 1.5, p.Y - o.X * 1.5),
                    "DPAD_RIGHT" => new ControlPoint(p.X - o.Y / 1.5, p.Y + o.X * 1.5),
                    _ => new ControlPoint(p.X + o.X, p.Y + o.Y) }).ToArray();
                result.Add(Polygon(key, points));
            }
            else if (key is "LB" or "RB" or "LT" or "RT") result.Add(new(key, ControlShape.RoundedRectangle, new(p.X - .055, p.Y - .0125, .11, .025), []));
            else
            {
                double radius = key is "L3" or "R3" ? .049 : key is "SELECT" or "START" ? .020 : key == "HOME" ? .028 : .035;
                result.Add(new(key, ControlShape.Circle, new(p.X - radius, p.Y - radius * 1.5, radius * 2, radius * 3), []));
            }
        }
        return result.AsReadOnly();
    }
    public static IReadOnlyList<ControlRegion> Rear(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("controls").EnumerateArray().Select(item => Polygon(item.GetProperty("slot").GetString()!,
            item.GetProperty("points").EnumerateArray().Select(p => new ControlPoint(p.GetProperty("x").GetDouble(), p.GetProperty("y").GetDouble())).ToArray())).ToArray();
    }
    private static ControlRegion Polygon(string key, ControlPoint[] points)
    {
        if (points.Length < 3 || points.Any(p => !double.IsFinite(p.X) || !double.IsFinite(p.Y) || p.X is < 0 or > 1 || p.Y is < 0 or > 1)) throw new InvalidDataException("Invalid normalized control contour.");
        double x = points.Min(p => p.X), y = points.Min(p => p.Y);
        return new(key, ControlShape.Polygon, new(x, y, points.Max(p => p.X) - x, points.Max(p => p.Y) - y), Array.AsReadOnly(points));
    }
}
