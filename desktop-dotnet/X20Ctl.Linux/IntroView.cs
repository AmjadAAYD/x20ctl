using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
namespace X20Ctl.Linux;

/// <summary>The opening: two blades cross, lock into the X, the mark settles, "Keep it 20", then the mark flies to the header logo. Silent. Any key or click skips it.</summary>
public sealed class IntroView : Control
{
    const double Total = 4.6, Cross = 1.2, Hold = 3.0;
    static readonly Color Blue = Color.FromRgb(81, 190, 255), Orange = Color.FromRgb(255, 163, 84);
    readonly Bitmap? mark; readonly Action done; readonly DispatcherTimer tick; readonly DateTime start = DateTime.UtcNow;
    bool finished;

    public IntroView(Bitmap? mark, Action done)
    {
        this.mark = mark; this.done = done; IsHitTestVisible = true; Focusable = true;
        tick = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) }; tick.Tick += (_, _) => { if (Elapsed >= Total) Finish(); else InvalidateVisual(); }; tick.Start();
        PointerPressed += (_, _) => Finish(); KeyDown += (_, _) => Finish();
        AttachedToVisualTree += (_, _) => Focus();
    }

    double Elapsed => (DateTime.UtcNow - start).TotalSeconds;
    static double Smooth(double x) { x = Math.Clamp(x, 0, 1); return x * x * (3 - 2 * x); }

    public void Finish() { if (finished) return; finished = true; tick.Stop(); done(); }

    public override void Render(DrawingContext dc)
    {
        double t = Elapsed, w = Bounds.Width, h = Bounds.Height; if (w < 10) return;
        double fade = 1 - Smooth((t - (Total - .5)) / .5);
        var veil = new SolidColorBrush(Color.FromRgb(4, 8, 20), fade); dc.DrawRectangle(veil, null, new Rect(0, 0, w, h));
        double cx = w / 2, cy = h / 2 - 20, reach = Math.Min(w, h) * .24;
        double glide = Smooth(t / Cross), lock_ = Smooth((t - Cross) / .5);
        // the blades slide in from opposite corners and meet at the heart
        for (int i = 0; i < 2; i++)
        {
            double dir = i == 0 ? 1 : -1; var colour = i == 0 ? Blue : Orange;
            double off = (1 - glide) * reach * 2.2;
            Point a = new Point(cx - dir * reach - dir * off, cy - reach - off * .6), b = new Point(cx + dir * reach - dir * off, cy + reach - off * .6);
            double alpha = Math.Clamp(glide * 1.4, 0, 1) * (1 - Smooth((t - Cross - .35) / .5) * .85);
            var pen = new Pen(new SolidColorBrush(colour, alpha), 18 + 6 * lock_, lineCap: PenLineCap.Round);
            dc.DrawLine(new Pen(new SolidColorBrush(colour, alpha * .25), 44, lineCap: PenLineCap.Round), i == 0 ? a : new Point(a.X, b.Y), i == 0 ? b : new Point(b.X, a.Y));
            dc.DrawLine(pen, i == 0 ? a : new Point(a.X, b.Y), i == 0 ? b : new Point(b.X, a.Y));
        }
        double flash = Math.Exp(-Math.Pow((t - Cross - .25) / .14, 2));
        if (flash > .01) dc.DrawEllipse(new RadialGradientBrush { GradientStops = { new GradientStop(Color.FromArgb((byte)(200 * flash), 255, 255, 255), 0), new GradientStop(Color.FromArgb(0, 255, 255, 255), 1) } }, null, new Point(cx, cy), reach * .9, reach * .9);
        // the mark settles, then flies to the header logo
        double settle = Smooth((t - Cross - .3) / .6), fly = Smooth((t - Hold) / .9);
        if (mark != null && settle > 0)
        {
            double size = Lerp(reach * 1.5, 46 * .6, fly); Point pos = new Point(Lerp(cx, 26 + 23, fly), Lerp(cy, 16 + 28, fly));
            using (dc.PushOpacity(settle)) dc.DrawImage(mark, new Rect(pos.X - size / 2, pos.Y - size / 2, size, size));
        }
        if (t > Cross + .8)
        {
            double a = Smooth((t - Cross - .8) / .5) * (1 - Smooth((t - Hold) / .4));
            var text = new FormattedText("Keep it 20", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(T.Font, FontStyle.Normal, FontWeight.Medium), 30, new SolidColorBrush(Color.FromRgb(236, 241, 250), a));
            dc.DrawText(text, new Point(cx - text.Width / 2, cy + reach * 1.25));
        }
    }
    static double Lerp(double a, double b, double f) => a + (b - a) * f;
}
