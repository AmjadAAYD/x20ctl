using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using X20Ctl.Product.Rebel;
using X20Ctl.Zone;
namespace X20Ctl.Product;

/// <summary>The Rebel Curves page (owner board curves.png): the response is a slash of red over halftone, the dead zone is hatched off,
/// and the values are blades. The sliders, presets and draft are the Curves workbench's own.</summary>
public partial class StudioScene
{
    private Grid? rebelCurves; private bool rebelCurvesWired;
    private static readonly string[] RebelPresets = ["default", "quick", "slow", "smooth", "fine"];
    private (string label, RangeBase slider)[] RebelCurveRows => Curves.Channel is CurveChannel.LeftStick or CurveChannel.RightStick
        ? [("Inner dead zone", Curves.Inner), ("Anti-dead zone", Curves.Floor), ("Full output at", Curves.Outer), ("Max output", Curves.Ceiling)]
        : [("Start point", Curves.Inner), ("Anti-dead zone", Curves.Floor), ("Full press point", Curves.Outer), ("Max output", Curves.Ceiling)];
    /// <summary>A value as a blade: a red wedge thickening toward a star knob.</summary>
    private static FrameworkElement RebelBlade(double pct, double w = 220)
    {
        double x = w * Math.Clamp(pct, 0, 100) / 100; var c = new Canvas { Width = w, Height = 28, IsHitTestVisible = false };
        var track = new Rectangle { Width = w, Height = 3, Fill = Ink.B(Ink.A(Ink.White, .2)), RenderTransform = new SkewTransform(-20, 0) }; Canvas.SetTop(track, 13); c.Children.Add(track);
        if (x > 1) c.Children.Add(new Polygon { Points = new PointCollection([new(0, 8 + 12 * .75), new(x, 8), new(x, 20), new(0, 20)]), Fill = Ink.B(Ink.Red) });
        c.Children.Add(Star.Make(x, 14, 12));
        return c;
    }
    private void RebelCurvesPage()
    {
        if (!rebelCurvesWired)
        {
            rebelCurvesWired = true; Curves.Changed += RebelLater; Curves.ChannelSelected += _ => RebelLater();
            foreach (var s in new RangeBase[] { Curves.Inner, Curves.Outer, Curves.Floor, Curves.Ceiling }) s.ValueChanged += (_, _) => { if (rebelPage == "curves") RebelLater(); };
        }
        var g = rebelCurves!; string? focused = (Keyboard.FocusedElement as FrameworkElement) is { Tag: string t } fe && g.IsAncestorOf(fe) ? t : null;
        g.Children.Clear();
        var tabs = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var ch in Enum.GetValues<CurveChannel>())
        {
            var chip = new ChipTab(ch switch { CurveChannel.LeftStick => "Left stick", CurveChannel.RightStick => "Right stick", CurveChannel.LeftTrigger => "Left trigger · LT", _ => "Right trigger · RT" }, 16) { On = ch == Curves.Channel, Tag = "ch:" + ch };
            var c = ch; chip.Click += (_, _) => Curves.SelectChannel(c); tabs.Children.Add(chip);
        }
        RPut(tabs, 268, 164); g.Children.Add(tabs);
        // the chart
        g.Children.Add(Place(new Slab { Width = 670, Height = 478, Seed = 120, Fill = Ink.B(Ink.C("#0f0e10")), Line = Ink.B(Ink.A(Ink.White, .26)), LineWidth = 1.6, Rot = -1.2, IsHitTestVisible = false }, 268, 232));
        double gx = 336, gy = 270, gw = 540, gh = 380; var plot = new Canvas { Width = 1600, Height = 900, IsHitTestVisible = false };
        for (int i = 1; i < 10; i++)
        {
            plot.Children.Add(new Line { X1 = gx + gw * i / 10, Y1 = gy, X2 = gx + gw * i / 10, Y2 = gy + gh, Stroke = Ink.B(Ink.A(Ink.White, .05)) });
            plot.Children.Add(new Line { X1 = gx, Y1 = gy + gh * i / 10, X2 = gx + gw, Y2 = gy + gh * i / 10, Stroke = Ink.B(Ink.A(Ink.White, .05)) });
        }
        double inner = Curves.Inner.Value;
        var hatch = new DrawingBrush(new GeometryDrawing(Ink.B(Ink.A(Ink.Red, .55)), null, new RectangleGeometry(new Rect(0, 0, 3, 10)))) { TileMode = TileMode.Tile, Viewport = new(0, 0, 10, 10), ViewportUnits = BrushMappingMode.Absolute, Viewbox = new(0, 0, 10, 10), ViewboxUnits = BrushMappingMode.Absolute, Transform = new RotateTransform(45) };
        var dead = new Rectangle { Width = Math.Max(0, gw * inner / 100), Height = gh, Fill = hatch }; Canvas.SetLeft(dead, gx); Canvas.SetTop(dead, gy); plot.Children.Add(dead);
        var pts = Enumerable.Range(0, 101).Select(i => { double x = i / 100.0, y = Math.Clamp(Curves.Output(x), 0, 1); return new Point(x * gw, gh - y * gh); }).ToList();
        var area = new Polygon { Points = new PointCollection(pts.Append(new Point(gw, gh)).Append(new Point(0, gh))), Fill = Dots.Tile(Ink.Red, 9, 1.9), Opacity = .55 }; Canvas.SetLeft(area, gx); Canvas.SetTop(area, gy); plot.Children.Add(area);
        plot.Children.Add(new Polyline { Points = new PointCollection([new(gx, gy), new(gx, gy + gh), new(gx + gw, gy + gh)]), Stroke = Ink.B(Ink.White), StrokeThickness = 2 });
        foreach (var (dx, dy, color, w, op) in new[] { (5.0, 5.0, Colors.Black, 12.0, 1.0), (0, 0, Ink.Red, 8, 1), (-2, -3, Ink.White, 1.6, .8) })
        { var line = new Polyline { Points = new PointCollection(pts), Stroke = Ink.B(color), StrokeThickness = w, Opacity = op, StrokeLineJoin = PenLineJoin.Miter }; Canvas.SetLeft(line, gx + dx); Canvas.SetTop(line, gy + dy); plot.Children.Add(line); }
        var knob = pts[55]; plot.Children.Add(Star.Make(gx + knob.X, gy + knob.Y, 18, 10));
        g.Children.Add(plot);
        var input = Ink.Text("Input %", 13.5, Ink.B(Ink.C("#9d9598")), false); g.Children.Add(Place(input, gx, gy + gh + 16));
        var outp = Ink.Text("Output % ↑", 13.5, Ink.B(Ink.C("#9d9598")), false); outp.Width = 90; outp.TextAlignment = TextAlignment.Right; g.Children.Add(Place(outp, gx + gw - 90, gy + gh + 16));
        var pct = Ink.Text($"{inner:0}%", 13.5, Ink.B(Ink.Hot), false); g.Children.Add(Place(pct, gx + 4, gy - 26));
        // the channel's name, then its values as blades
        string channel = CurveWorkbench.Name(Curves.Channel); var title = Ransom.Make(channel, 40, channel == "Left stick" ? 6 : null); g.Children.Add(Place(title, 990, 226));
        var list = new StackPanel { Width = 560 }; int n = 0;
        foreach (var (label, slider) in RebelCurveRows)
        {
            var row = new RowSlab(n, 56) { Margin = new(n * 5, 0, 0, 6), Tag = "curve:" + label, IsEnabled = Curves.Draft.CanEdit };
            row.Left.Children.Add(row.Tint(Ink.Text(label, 19), Ink.White, Ink.Black));
            row.Right.Children.Add(RebelBlade(slider.Value)); var v = row.Tint(Ink.Text($"{slider.Value:0}%", 15, null, false, FontWeights.SemiBold), Ink.C("#c9c1c3"), Ink.Black); v.MinWidth = 60; v.TextAlignment = TextAlignment.Right; v.Margin = new(12, 0, 0, 0); row.Right.Children.Add(v);
            row.GotKeyboardFocus += (_, _) => row.On = true; row.LostKeyboardFocus += (_, _) => row.On = false;
            list.Children.Add(row); n++;
        }
        g.Children.Add(Place(list, 990, 316));
        // the response preset (the board leaves it out; it stays reachable beside the channel's name)
        string preset = Curves.Draft[Curves.Channel].Preset;
        var response = RebelBt("Response · ‹ " + char.ToUpper(preset[0]) + preset[1..] + " ›", null, () => RebelCyclePreset(1), "curve:preset", 14, 34, enabled: Curves.Draft.CanEdit);
        response.HorizontalAlignment = HorizontalAlignment.Right; response.VerticalAlignment = VerticalAlignment.Top; response.Margin = new(0, 240, 50, 0); g.Children.Add(response);
        var test = new SlabButton("Keyboard test", 164, 46, 131, 0, 17, Ink.White) { Tag = "curve:test", Outlined = true }; test.Click += (_, _) => StartTry();
        g.Children.Add(Place(test, 990, 606));
        var note = Ink.Text("Connect a controller or use Keyboard test", 15, Ink.B(Ink.C("#9d9598")), false); note.VerticalAlignment = VerticalAlignment.Center; g.Children.Add(Place(note, 1176, 618));
        string want = focused ?? "curve:" + RebelCurveRows[0].label;
        Dispatcher.BeginInvoke(() => FindTagged(g, want)?.Focus(), DispatcherPriority.Input);
    }
    private static FrameworkElement Place(FrameworkElement e, double x, double y) { RPut(e, x, y); return e; }
    private void RebelCyclePreset(int step)
    {
        string now = Curves.Draft[Curves.Channel].Preset; int i = Array.IndexOf(RebelPresets, now); Curves.SetPreset(RebelPresets[((i < 0 ? 0 : i) + step + RebelPresets.Length) % RebelPresets.Length]);
    }
    /// <summary>Left and Right on a value row move its blade; on the response row they change the preset.</summary>
    private bool RebelCurveKey(KeyEventArgs e)
    {
        if (rebelPage != "curves" || e.Key is not (Key.Left or Key.Right) || Keyboard.FocusedElement is not FrameworkElement { Tag: string tag } || !tag.StartsWith("curve:")) return false;
        int step = e.Key == Key.Right ? 1 : -1; e.Handled = true;
        if (tag == "curve:preset") { if (!e.IsRepeat) RebelCyclePreset(step); return true; }
        var hit = RebelCurveRows.FirstOrDefault(r => "curve:" + r.label == tag);
        if (hit.slider != null) hit.slider.Value = Math.Clamp(hit.slider.Value + step, hit.slider.Minimum, hit.slider.Maximum);
        return true;
    }
}
