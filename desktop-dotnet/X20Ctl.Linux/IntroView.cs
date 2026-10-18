using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
namespace X20Ctl.Linux;

/// <summary>The opening, in time with Brand/intro.wav: three LED eyes, the controller's own outline drawn in white, two blades sliding in and locking into the X, the slogan,
/// then the mark flying to the header logo. Any key or click skips it; M mutes the score.</summary>
public sealed class IntroView : Control
{
    // the same beats as the score (tools/build_intro_sound.py)
    const double Total = 7.1, EyesAt = .18, OutlineFrom = 1.45, OutlineFor = 1.0, SlideFrom = 2.95, LockAt = 4.03, SloganAt = 5.1, FlyFrom = 5.95, FlyFor = .85;
    static readonly Color Blue = Color.FromRgb(81, 190, 255), Orange = Color.FromRgb(255, 163, 84);
    readonly Bitmap? mark; readonly Action done; readonly DispatcherTimer tick; readonly DateTime start = DateTime.UtcNow;
    readonly Point[] outline; readonly IntroSound sound = new(); bool finished;

    public IntroView(Bitmap? mark, string modelId, Action done, bool silent = false)
    {
        this.mark = mark; this.done = done; outline = Outline(modelId); IsHitTestVisible = true; Focusable = true;
        tick = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) }; tick.Tick += (_, _) => { if (Elapsed >= Total) Finish(); else InvalidateVisual(); }; tick.Start();
        PointerPressed += (_, _) => Finish(); KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.M) sound.Stop(); else Finish(); };
        if (!silent && AppSettings.Shared.IntroSound) sound.Start();
        AttachedToVisualTree += (_, _) => Focus();
    }

    static Point[] Outline(string modelId)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Data", "vibration-outlines.json")));
            var all = doc.RootElement.GetProperty("silhouettes");
            var list = all.TryGetProperty(modelId, out var own) ? own : all.EnumerateObject().First().Value;
            return list.EnumerateArray().Select(p => new Point(p[0].GetDouble(), p[1].GetDouble())).ToArray();
        }
        catch (Exception e) when (e is IOException or JsonException or KeyNotFoundException or InvalidOperationException) { return []; }
    }

    /// <summary>Set by the review run to draw a chosen moment instead of the clock.</summary>
    public double? Frozen { get; set; }
    double Elapsed => Frozen ?? (DateTime.UtcNow - start).TotalSeconds;
    static double Smooth(double x) { x = Math.Clamp(x, 0, 1); return x * x * (3 - 2 * x); }
    static double Lerp(double a, double b, double f) => a + (b - a) * f;

    public void Finish() { if (finished) return; finished = true; tick.Stop(); sound.Stop(); done(); }

    public override void Render(DrawingContext dc)
    {
        double t = Elapsed, w = Bounds.Width, h = Bounds.Height; if (w < 10) return;
        double fade = 1 - Smooth((t - (Total - .35)) / .35);
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(4, 8, 20), fade), null, new Rect(0, 0, w, h));
        double cx = w / 2, cy = h / 2 - 20, reach = Math.Min(w, h) * .24;

        // the three eyes, left to right
        for (int i = 0; i < 3; i++)
        {
            double on = Smooth((t - EyesAt - i * .18) / .2) * (1 - Smooth((t - OutlineFrom - .6) / .5));
            if (on <= 0) continue;
            var at = new Point(cx + (i - 1) * 34, cy - reach * 1.5);
            dc.DrawEllipse(new SolidColorBrush(Blue, on * .3), null, at, 14, 14); dc.DrawEllipse(new SolidColorBrush(Blue, on), null, at, 5, 5);
        }

        // the controller's own outline, drawn by a single smooth stroke
        double drawn = Smooth((t - OutlineFrom) / OutlineFor), outlineFade = 1 - Smooth((t - SlideFrom - .3) / .7);
        if (outline.Length > 1 && drawn > 0 && outlineFade > 0)
        {
            double width = Math.Min(w * .5, 760), height = width * 1024 / 1536, ox = cx - width / 2, oy = cy - height / 2 + 10;
            int last = Math.Max(1, (int)(outline.Length * drawn)); var geo = new StreamGeometry();
            using (var g = geo.Open())
            {
                g.BeginFigure(new Point(ox + outline[0].X * width, oy + outline[0].Y * height), false);
                for (int i = 1; i < last; i++) g.LineTo(new Point(ox + outline[i].X * width, oy + outline[i].Y * height));
                g.EndFigure(drawn >= 1);
            }
            dc.DrawGeometry(null, new Pen(new SolidColorBrush(Colors.White, .18 * outlineFade), 9, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), geo);
            dc.DrawGeometry(null, new Pen(new SolidColorBrush(Colors.White, .9 * outlineFade), 2.4, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), geo);
        }

        // the blades slide in from opposite corners and lock at the heart
        double glide = Smooth((t - SlideFrom) / (LockAt - SlideFrom)), lock_ = Smooth((t - LockAt) / .4);
        if (glide > 0)
            for (int i = 0; i < 2; i++)
            {
                double dir = i == 0 ? 1 : -1; var colour = i == 0 ? Blue : Orange; double off = (1 - glide) * reach * 2.2;
                Point a = new(cx - dir * reach - dir * off, cy - reach - off * .6), b = new(cx + dir * reach - dir * off, cy + reach - off * .6);
                double alpha = Math.Clamp(glide * 1.4, 0, 1) * (1 - Smooth((t - LockAt - .1) / .6));
                dc.DrawLine(new Pen(new SolidColorBrush(colour, alpha * .25), 44, lineCap: PenLineCap.Round), a, b);
                dc.DrawLine(new Pen(new SolidColorBrush(colour, alpha), 18 + 6 * lock_, lineCap: PenLineCap.Round), a, b);
            }
        double flash = Math.Exp(-Math.Pow((t - LockAt - .1) / .14, 2));
        if (flash > .01) dc.DrawEllipse(new RadialGradientBrush { GradientStops = { new GradientStop(Color.FromArgb((byte)(200 * flash), 255, 255, 255), 0), new GradientStop(Color.FromArgb(0, 255, 255, 255), 1) } }, null, new Point(cx, cy), reach * .9, reach * .9);

        // the mark settles, then flies to the header logo
        double settle = Smooth((t - LockAt - .2) / .6), fly = Smooth((t - FlyFrom) / FlyFor);
        if (mark != null && settle > 0)
        {
            double size = Lerp(reach * 1.5, 46 * .6, fly); Point pos = new(Lerp(cx, 26 + 23, fly), Lerp(cy, 16 + 28, fly));
            using (dc.PushOpacity(settle)) dc.DrawImage(mark, new Rect(pos.X - size / 2, pos.Y - size / 2, size, size));
        }
        if (t > SloganAt)
        {
            double a = Smooth((t - SloganAt) / .4) * (1 - Smooth((t - FlyFrom) / .35));
            var text = new FormattedText("Keep it 20", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(T.Font, FontStyle.Normal, FontWeight.Medium), 30, new SolidColorBrush(Color.FromRgb(236, 241, 250), a));
            dc.DrawText(text, new Point(cx - text.Width / 2, cy + reach * 1.25));
        }
    }
}
