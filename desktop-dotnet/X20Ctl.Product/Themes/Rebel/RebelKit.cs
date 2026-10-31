using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
namespace X20Ctl.Product.Rebel;

/// <summary>The Rebel theme's parts, drawn from the owner's boards (previews/console-concepts/themes/t_rebel.py): ransom-note
/// letters, jagged slabs, torn paper, halftone, stars, the hint bar. Coordinates are those of the 1600 by 900 board.</summary>
internal static class Ink
{
    public static readonly Color Red = C("#e3122d"), Hot = C("#ff2a44"), Dark = C("#7d0815"), White = C("#f7f3ee"), Paper = C("#ece4d4"), Black = C("#0b0a0c"), Panel = C("#161417"), Grey = C("#8f878a"), Dim = C("#a39b9e"), Cut = C("#c40f27");
    public static Color C(string hex) => (Color)ColorConverter.ConvertFromString(hex);
    public static Brush B(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }
    public static Brush B(string hex) => B(C(hex));
    public static Color A(Color c, double alpha) => Color.FromArgb((byte)Math.Round(alpha * 255), c.R, c.G, c.B);
    public static readonly FontFamily Heavy = new("Franklin Gothic, Impact"), Body = new("Bahnschrift, Segoe UI");
    /// <summary>The faces have no italic, so the board's slant is drawn: about 12 degrees, as the browser synthesised it.</summary>
    public static readonly Transform Slant = Frozen(new SkewTransform(-12, 0));
    private static Transform Frozen(Transform t) { t.Freeze(); return t; }
    public static TextBlock Text(string text, double size, Brush? fg = null, bool heavy = true, FontWeight? weight = null) => new()
    {
        Text = text, FontFamily = heavy ? Heavy : Body, FontSize = size, FontStyle = FontStyles.Normal, RenderTransform = Slant, RenderTransformOrigin = new Point(.5, .5), Foreground = fg ?? B(White),
        FontWeight = weight ?? (heavy ? FontWeights.Black : FontWeights.Normal), TextWrapping = TextWrapping.NoWrap
    };
    /// <summary>The small spaced-out red label (.sc).</summary>
    public static TextBlock Label(string text, double size = 12, Brush? fg = null) => new()
    {
        Text = string.Join(" ", text.ToUpperInvariant().ToCharArray()), FontFamily = Body, FontSize = size, RenderTransform = Slant, RenderTransformOrigin = new Point(.5, .5), FontWeight = FontWeights.Bold, Foreground = fg ?? B(Hot),
        Typography = { }, TextWrapping = TextWrapping.NoWrap
    };
    public static DropShadowEffect Shadow(double dx, double dy, double blur, Color color, double opacity) => new()
    {
        Color = color, Opacity = opacity, BlurRadius = blur, ShadowDepth = Math.Sqrt(dx * dx + dy * dy), Direction = -Math.Atan2(dy, dx) * 180 / Math.PI
    };
}

/// <summary>One letter of a ransom note: cut from its own paper, in its own font, a little crooked.</summary>
internal sealed class Letter : Border
{
    public double[]? Cut;
    protected override Size ArrangeOverride(Size size)
    {
        var result = base.ArrangeOverride(size);
        if (Cut is { } c && size.Width > 0 && size.Height > 0)
        {
            double w = size.Width, h = size.Height;
            var g = new PathGeometry(); var f = new PathFigure { StartPoint = new(c[0] * w / 100, c[1] * h / 100), IsClosed = true };
            f.Segments.Add(new PolyLineSegment(new[] { new Point(c[2] * w / 100, c[3] * h / 100), new Point(c[4] * w / 100, c[5] * h / 100), new Point(c[6] * w / 100, c[7] * h / 100) }, true)); g.Figures.Add(f); g.Freeze();
            Clip = g;
        }
        return result;
    }
}

internal static class Ransom
{
    private static readonly (string fam, double k, int wt)[] Fonts =
    [("Bodoni MT Black", 1.0, 400), ("Stencil", .9, 400), ("Cooper Black", .95, 400), ("Haettenschweiler", 1.22, 400), ("Bernard MT Condensed", 1.08, 400), ("Impact", 1.0, 400),
     ("Franklin Gothic Heavy", .98, 400), ("Showcard Gothic", .86, 400), ("Elephant", .9, 400), ("Rockwell", 1.0, 700), ("Algerian", .95, 400)];
    private record Spec(string C, string Fam, int Wt, string Sk, double Fs, double Ty, double Rot, double[]? Cp);
    private static Dictionary<string, Spec?[]>? table;
    private static Dictionary<string, Spec?[]> Table => table ??= Read();
    private static Dictionary<string, Spec?[]> Read()
    {
        var result = new Dictionary<string, Spec?[]>();
        try
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Brand", "themes", "rebel", "ransom.json");
            if (!File.Exists(path)) return result;
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            foreach (var entry in doc.RootElement.EnumerateObject())
                result[entry.Name] = entry.Value.EnumerateArray().Select(e => e.ValueKind == JsonValueKind.Null ? null : new Spec(e.GetProperty("c").GetString()!, e.GetProperty("fam").GetString()!, e.GetProperty("wt").GetInt32(), e.GetProperty("sk").GetString()!,
                    e.GetProperty("fs").GetDouble(), e.GetProperty("ty").GetDouble(), e.GetProperty("rot").GetDouble(), e.GetProperty("cp").ValueKind == JsonValueKind.Null ? null : e.GetProperty("cp").EnumerateArray().Select(v => v.GetDouble()).ToArray())).ToArray();
        }
        catch (Exception error) when (error is IOException or JsonException or InvalidOperationException or KeyNotFoundException) { }
        return result;
    }
    /// <summary>The letters the owner's board set for this exact text (when it is there), otherwise a fresh cut with the same rules.</summary>
    private static Spec?[] Specs(string text, int? seed, string first, string skins)
    {
        if (Table.TryGetValue($"{text}|{seed?.ToString() ?? ""}|{first}", out var known)) return known;
        var r = new Random(seed ?? text.Select((c, i) => c * (i + 3)).Sum()); var list = new List<Spec?>(); char last = '\0';
        for (int i = 0; i < text.Length; i++)
        {
            char ch = text[i]; if (ch == ' ') { list.Add(null); continue; }
            var (fam, k, wt) = Fonts[r.Next(Fonts.Length)];
            char sk = i == 0 && first.Length > 0 ? first[0] : skins.Where(c => c != last).ElementAt(r.Next(skins.Count(c => c != last))); last = sk;
            string c2 = char.IsLetter(ch) && r.NextDouble() < .3 ? ch.ToString().ToUpperInvariant() : ch.ToString();
            double fs = k * (.84 + r.NextDouble() * .26) * (i == 0 ? 1.12 : 1);
            double[]? cp = sk == 'n' ? null : [r.NextDouble() * 7, r.NextDouble() * 9, 93 + r.NextDouble() * 7, r.NextDouble() * 7, 94 + r.NextDouble() * 6, 91 + r.NextDouble() * 9, r.NextDouble() * 6, 92 + r.NextDouble() * 8];
            list.Add(new(c2, fam, wt, sk.ToString(), fs, (r.NextDouble() - .5) * .14, (r.NextDouble() - .5) * 16, cp));
        }
        return list.ToArray();
    }
    public static FrameworkElement Make(string text, double size, int? seed = null, string first = "r", string skins = "wwkkrpn")
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Effect = Ink.Shadow(4, 5, 0, Colors.Black, .9) };
        foreach (var s in Specs(text, seed, first, skins))
        {
            if (s == null) { row.Children.Add(new Border { Width = size * .26 }); continue; }
            var (bg, fg, inset) = s.Sk switch
            {
                "w" => (Ink.White, Ink.Black, false), "k" => (Ink.Black, Ink.White, true), "r" => (Ink.Red, Ink.White, false), "p" => (Ink.Paper, Ink.Black, false), _ => (Colors.Transparent, Ink.White, false)
            };
            double px = size * s.Fs;
            // Browser face names include the weight in the family; WPF exposes these as a family plus a typeface.
            var (family, weight, stretch) = s.Fam switch
            {
                "Franklin Gothic Heavy" => ("Franklin Gothic", FontWeights.Black, FontStretches.Normal),
                "Bodoni MT Black" => ("Bodoni MT", FontWeights.Black, FontStretches.Normal),
                "Cooper Black" => ("Cooper", FontWeights.Black, FontStretches.Normal),
                "Bernard MT Condensed" => ("Bernard MT", FontWeights.Normal, FontStretches.Condensed),
                _ => (s.Fam, s.Wt == 700 ? FontWeights.Bold : FontWeights.Normal, FontStretches.Normal)
            };
            var face = new TextBlock { Text = s.C, FontFamily = new FontFamily(family + ", Impact"), FontSize = px, FontWeight = weight, FontStretch = stretch, Foreground = Ink.B(fg), LineHeight = px * 1.08, LineStackingStrategy = LineStackingStrategy.BlockLineHeight };
            row.Children.Add(new Letter
            {
                Child = face, Background = Ink.B(bg), Padding = new(px * .13, px * .06, px * .13, px * .02), Margin = new(px * .025, 0, px * .025, 0), Cut = s.Cp,
                BorderBrush = inset ? Ink.B(Ink.White) : null, BorderThickness = new(inset ? 2 : 0), RenderTransformOrigin = new(.5, .5), SnapsToDevicePixels = false,
                RenderTransform = new TransformGroup { Children = { new RotateTransform(s.Rot), new TranslateTransform(0, s.Ty * size) } }
            });
        }
        return row;
    }
}

/// <summary>A jagged slab: a polygon with four corners knocked out of square and, when it is the chosen one, a point on the left and a
/// red shape under it. The content is laid over it; the slab turns about its centre.</summary>
internal class Slab : Grid
{
    public Brush? Fill { get; set; } = Ink.B(Ink.Panel);
    public Brush? Line { get; set; }
    public double LineWidth { get; set; } = 2;
    public DoubleCollection? Dash { get; set; }
    public int Seed { get; set; }
    public double Amp { get; set; } = 6;
    public double Spike { get; set; }
    public Brush? Under { get; set; }
    public double Rot { set { RenderTransformOrigin = new(.5, .5); RenderTransform = new RotateTransform(value); } }
    public static Point[] Jag(double w, double h, int seed, double amp, double spike)
    {
        var r = new Random(seed); double A(double k = 1) => r.NextDouble() * amp * k;
        double a0 = A(), a1 = A(.8), b0 = w - A(1.2), b1 = A(.5), c0 = w - A(.5), c1 = h - A(.9), d0 = A(1.3), d1 = h - A(.4);
        var pts = new List<Point> { new(a0, a1), new(b0, b1), new(c0, c1), new(d0, d1) };
        if (spike > 0) pts.Add(new(-spike, h * (.45 + r.NextDouble() * .17)));
        return pts.ToArray();
    }
    public static Geometry Poly(IReadOnlyList<Point> pts, double dx = 0, double dy = 0)
    {
        var f = new PathFigure { StartPoint = new(pts[0].X + dx, pts[0].Y + dy), IsClosed = true };
        f.Segments.Add(new PolyLineSegment(pts.Skip(1).Select(p => new Point(p.X + dx, p.Y + dy)).ToArray(), true)); var g = new PathGeometry(); g.Figures.Add(f); g.Freeze(); return g;
    }
    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight; if (w <= 0 || h <= 0) return;
        if (Under != null) dc.DrawGeometry(Under, null, Poly(Jag(w + 14, h + 8, Seed + 5, Amp + 4, Spike > 0 ? Spike + 16 : 0), -4, -1));
        Pen? pen = Line == null ? null : new Pen(Line, LineWidth) { LineJoin = PenLineJoin.Miter, DashStyle = Dash == null ? DashStyles.Solid : new DashStyle(Dash, 0) };
        dc.DrawGeometry(Fill, pen, Poly(Jag(w, h, Seed, Amp, Spike)));
    }
}

/// <summary>Torn paper: the calling cards and posters. Drawn once, with its own shadow, over a red sheet when it is the chosen one.</summary>
internal sealed class TornPaper : FrameworkElement
{
    public int Seed { get; set; } = 1;
    public Brush? UnderSheet { get; set; }
    public Brush? Sheet { get; set; }
    public TornPaper() { Effect = Ink.Shadow(0, 18, 22, Colors.Black, .55); IsHitTestVisible = false; }
    public static PathGeometry Torn(double w, double h, int seed, double amp = 4, double step = 18)
    {
        var r = new Random(seed); var pts = new List<Point>();
        for (double x = 0; x <= w; x += step) pts.Add(new(x, r.NextDouble() * amp));
        for (double y = step * 2; y < h; y += step * 3) pts.Add(new(w - r.NextDouble() * amp * .6, y));
        for (double x = w; x >= 0; x -= step) pts.Add(new(x, h - r.NextDouble() * amp));
        for (double y = h - step * 2; y > 0; y -= step * 3) pts.Add(new(r.NextDouble() * amp * .6, y));
        var f = new PathFigure { StartPoint = pts[0], IsClosed = true }; f.Segments.Add(new PolyLineSegment(pts.Skip(1).ToArray(), true)); var g = new PathGeometry(); g.Figures.Add(f); g.Freeze(); return g;
    }
    private static readonly Brush Paper = Make();
    private static Brush Make()
    {
        var b = new RadialGradientBrush { Center = new(.3, .2), GradientOrigin = new(.3, .2), RadiusX = 1.0, RadiusY = .8 };
        b.GradientStops.Add(new(Ink.C("#f6f1e6"), 0)); b.GradientStops.Add(new(Ink.C("#e8dfcf"), .7)); b.GradientStops.Add(new(Ink.C("#dacfba"), 1)); b.Freeze(); return b;
    }
    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight; if (w <= 0 || h <= 0) return;
        if (UnderSheet != null)
        {
            dc.PushTransform(new RotateTransform(1.6, w / 2 + 12, h / 2 + 12)); dc.PushTransform(new TranslateTransform(12, 12));
            dc.DrawGeometry(UnderSheet, null, Torn(w, h, Seed + 9, 7)); dc.Pop(); dc.Pop();
        }
        dc.DrawGeometry(Sheet ?? Paper, null, Torn(w, h, Seed));
    }
}

/// <summary>Halftone dots on a grid, faded by a mask.</summary>
internal static class Dots
{
    public static Brush Tile(Color color, double step, double radius)
    {
        var tile = new DrawingBrush(new GeometryDrawing(new SolidColorBrush(color), null, new EllipseGeometry(new Point(step / 2, step / 2), radius, radius))) { TileMode = TileMode.Tile, ViewportUnits = BrushMappingMode.Absolute, Viewport = new(0, 0, step, step), ViewboxUnits = BrushMappingMode.Absolute, Viewbox = new(0, 0, step, step) };
        tile.Freeze(); return tile;
    }
    public static System.Windows.Shapes.Rectangle Field(double x, double y, double w, double h, Color color, double step, double radius, Brush? mask = null)
    {
        var r = new System.Windows.Shapes.Rectangle { Width = w, Height = h, Fill = Tile(color, step, radius), OpacityMask = mask, IsHitTestVisible = false };
        Canvas.SetLeft(r, x); Canvas.SetTop(r, y); return r;
    }
    public static LinearGradientBrush Fade(double angle, double clearAt = 1) => new(Colors.Black, Colors.Transparent, angle) { GradientStops = { new(Colors.Black, 0), new(Colors.Transparent, clearAt) } };
}

internal static class Star
{
    public static Geometry Shape(double cx, double cy, double r, double k = .3, double rot = 0, double tall = 1.25)
    {
        var pts = new List<Point>();
        for (int i = 0; i < 8; i++)
        {
            double a = (rot + i * 45 - 90) * Math.PI / 180, rr = i % 2 == 0 ? r : r * k;
            pts.Add(new(cx + rr * Math.Cos(a), cy + rr * Math.Sin(a) * (i % 4 == 0 ? tall : 1)));
        }
        var f = new PathFigure { StartPoint = pts[0], IsClosed = true }; f.Segments.Add(new PolyLineSegment(pts.Skip(1).ToArray(), true)); var g = new PathGeometry(); g.Figures.Add(f); g.Freeze(); return g;
    }
    /// <summary>The logo's spark: a red four-point star with a white rim and a black edge.</summary>
    public static UIElement Make(double cx, double cy, double r, double rot = 0, double opacity = 1, Color? fill = null, Color? rim = null)
    {
        var g = new Canvas { Opacity = opacity, IsHitTestVisible = false };
        g.Children.Add(new System.Windows.Shapes.Path { Data = Shape(cx, cy, r * 1.24, .38, rot), Fill = Ink.B(Ink.Black) });
        g.Children.Add(new System.Windows.Shapes.Path { Data = Shape(cx, cy, r * 1.11, .35, rot), Fill = Ink.B(rim ?? Ink.White) });
        g.Children.Add(new System.Windows.Shapes.Path { Data = Shape(cx, cy, r, .3, rot), Fill = Ink.B(fill ?? Ink.Red) });
        return g;
    }
}

internal static class Parts
{
    /// <summary>A key cap: the white pill with the button letter, as in the hint bar.</summary>
    public static UIElement Key(string k, bool red = false)
    {
        var b = new Border { MinWidth = 28, Height = 28, CornerRadius = new(14), Padding = new(7, 0, 7, 0), Background = Ink.B(red ? Ink.Red : Ink.White), VerticalAlignment = VerticalAlignment.Center };
        var t = Ink.Text(k, 14, Ink.B(red ? Colors.White : Ink.Black)); t.HorizontalAlignment = HorizontalAlignment.Center; t.VerticalAlignment = VerticalAlignment.Center; b.Child = t; return b;
    }
    /// <summary>The black hint bar with its red underline and clipped corner.</summary>
    public static FrameworkElement Hints(params (string key, string text)[] items)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(38, 9, 26, 10) };
        foreach (var (k, t) in items)
        {
            var one = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 0, 24, 0) };
            one.Children.Add(Key(k)); var label = Ink.Text(t, 17); label.Margin = new(9, 0, 0, 0); label.VerticalAlignment = VerticalAlignment.Center; one.Children.Add(label); row.Children.Add(one);
        }
        return new HintSlab { Child = row };
    }
    private sealed class HintSlab : Decorator
    {
        protected override void OnRender(DrawingContext dc)
        {
            double w = ActualWidth, h = ActualHeight; if (w <= 0) return;
            var shape = Slab.Poly([new(w * .04, 0), new(w, 0), new(w * .985, h), new(0, h)]);
            dc.DrawGeometry(Ink.B(Colors.Black), null, shape);
            dc.PushClip(shape); dc.DrawRectangle(Ink.B(Ink.Red), null, new Rect(0, h - 3, w, 3)); dc.Pop();
        }
    }
    /// <summary>The status pill: "Player 1 19:24" on a black slab over a red offset.</summary>
    public static FrameworkElement Status(UIElement clock, string who = "Player 1")
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(20, 8, 20, 8) };
        var name = Ink.Text(who, 17); name.VerticalAlignment = VerticalAlignment.Center; row.Children.Add(name); row.Children.Add(clock);
        var pill = new Slab { Fill = Ink.B(Ink.Black), Line = Ink.B(Ink.A(Ink.White, .3)), LineWidth = 1.5, Amp = 0 };
        pill.Children.Add(row); return pill;
    }
    public static Canvas At(UIElement e, double x, double y) { var c = new Canvas(); Canvas.SetLeft(e, x); Canvas.SetTop(e, y); c.Children.Add(e); return c; }
}
