using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using X20Ctl.Product.Rebel;
namespace X20Ctl.Product;

/// <summary>The Rebel Vibration page (owner board vibration.png): the controller shakes inside a comic burst, the strength is a ransom
/// number over a blade. The preview is the Vibration workbench's own visual; every value goes through its draft.</summary>
public partial class StudioScene
{
    private Grid? rebelVibration; private Viewbox? rebelVibArt; private bool rebelVibWired;
    private static readonly string[] RebelRegions = ["All", "Both grips", "Left grip", "Right grip"];
    /// <summary>The board's burst (seed 3, 15 spikes): each point's angle and its share of the outer or inner radius.</summary>
    private static readonly double[] RebelBurst = [-0.0367, 0.9433, 0.1912, 0.9966, 0.4365, 0.7997, 0.5602, 1.0340, 0.8041, 0.8503, 1.1166, 0.9752, 1.3037, 0.9229, 1.4855, 0.9241, 1.6944, 1.0404,
        1.8882, 1.0186, 2.1184, 0.7992, 2.3400, 0.9946, 2.4855, 0.7893, 2.7739, 0.9756, 2.9628, 1.0436, 3.1716, 1.0474, 3.3363, 1.0203, 3.5527, 1.0497, 3.8230, 0.8092, 3.9284, 0.9347, 4.2540, 0.9108,
        4.4160, 0.9482, 4.6087, 0.8958, 4.7962, 0.9936, 5.0383, 1.0513, 5.2615, 1.0486, 5.4953, 1.0773, 5.6788, 0.9261, 5.9148, 1.0694, 6.1304, 0.9911];
    private static Polygon RebelBurstShape(double cx, double cy, double r0, double r1, Color fill)
    {
        var pts = new PointCollection(); for (int i = 0; i < RebelBurst.Length / 2; i++) { double a = RebelBurst[2 * i], r = RebelBurst[2 * i + 1] * (i % 2 == 0 ? r1 : r0); pts.Add(new(cx + r * Math.Cos(a), cy + r * Math.Sin(a))); }
        return new Polygon { Points = pts, Fill = Ink.B(fill), IsHitTestVisible = false };
    }
    private string RebelRegion => Vibration?.PreviewRegion ?? "All";

    private void RebelVibrationPage()
    {
        var v = Vibration; if (v == null) return;
        if (!rebelVibWired) { rebelVibWired = true; v.Changed += () => { if (rebelPage == "vibration") RebelLater(); }; }
        if (rebelVibArt == null && v.Visualizer.Parent is Viewbox own) { own.Child = null; rebelVibArt = new Viewbox { Child = v.Visualizer, Stretch = Stretch.Uniform, Width = 560, Height = 420, IsHitTestVisible = false, Effect = Ink.Shadow(12, 14, 0, Ink.Black, 1) }; }
        var g = rebelVibration!; string? focused = (Keyboard.FocusedElement as FrameworkElement) is { Tag: string t } fe && g.IsAncestorOf(fe) ? t : null;
        g.Children.Clear(); var d = v.Draft; int value = (int)Math.Round(v.Strength.Value);

        // the burst, with the pad shaking inside it
        double cx = 572, cy = 452; var art = new Canvas { Width = 1600, Height = 900, IsHitTestVisible = false };
        art.Children.Add(RebelBurstShape(cx + 10, cy + 12, 226, 312, Colors.Black)); art.Children.Add(RebelBurstShape(cx, cy, 226, 312, Ink.White)); art.Children.Add(RebelBurstShape(cx, cy, 211, 292, Ink.Red));
        var mask = new RadialGradientBrush { RadiusX = .5, RadiusY = .5 }; mask.GradientStops.Add(new(Colors.Transparent, .45)); mask.GradientStops.Add(new(Colors.Black, .85)); mask.GradientStops.Add(new(Colors.Transparent, 1));
        art.Children.Add(Dots.Field(cx - 320, cy - 320, 640, 640, Ink.A(Ink.Black, .55), 11, 2.4, mask));
        g.Children.Add(art);
        if (rebelVibArt != null) { (rebelVibArt.Parent as Panel)?.Children.Remove(rebelVibArt); g.Children.Add(Place(rebelVibArt, cx - 280, cy - 192)); }
        var under = Ink.Text("Region selection changes the illustration only. This backend has one linked strength; independent trigger tuning is unavailable.", 14, Ink.B(Ink.C("#9d9598")), false);
        under.Width = 640; under.TextAlignment = TextAlignment.Center; under.TextWrapping = TextWrapping.Wrap; g.Children.Add(Place(under, cx - 320, 790));

        // the value, its blade, the presets and the rows
        var col = new StackPanel { Width = 560 };
        col.Children.Add(Ink.Label("Vibration preview"));
        col.Children.Add(Ransom.Make(value + "%", 110, value == 70 ? 70 : null).Also(r => r.Margin = new(0, 12, 0, 0)));
        col.Children.Add(RebelBlade(value, 520).Also(b => b.Margin = new(0, 22, 0, 0)));
        var presets = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 24, 0, 0) };
        foreach (int p in new[] { 0, 25, 50, 75, 100 }) { int pick = p; presets.Children.Add(RebelChip(p == 0 ? "Off" : p + "%", value == p, () => v.Set(pick), "vib:" + p, d.CanEdit)); }
        col.Children.Add(presets);
        var list = new StackPanel { Margin = new(0, 22, 0, 0) };
        var strength = new RowSlab(0, 56) { Margin = new(0, 0, 0, 6), Tag = "vib:strength", IsEnabled = d.CanEdit };
        strength.Left.Children.Add(strength.Tint(Ink.Text("Strength", 19), Ink.White, Ink.Black));
        strength.Right.Children.Add(RebelBlade(value)); var sv = strength.Tint(Ink.Text($"{value}%", 15, null, false, FontWeights.SemiBold), Ink.C("#c9c1c3"), Ink.Black); sv.MinWidth = 60; sv.TextAlignment = TextAlignment.Right; sv.Margin = new(12, 0, 0, 0); strength.Right.Children.Add(sv);
        var region = new RowSlab(1, 56) { Margin = new(3, 0, 0, 6), Tag = "vib:region" };
        region.Left.Children.Add(region.Tint(Ink.Text("Preview region", 19), Ink.White, Ink.Black));
        region.Right.Children.Add(new Border { Height = 34, Padding = new(14, 0, 14, 0), Background = Ink.B(Ink.Black), BorderBrush = Ink.B(Ink.A(Ink.White, .55)), BorderThickness = new(1.5), RenderTransform = new SkewTransform(-10, 0),
            Child = new StackPanel { Orientation = Orientation.Horizontal, Children = { Ink.Text(RebelRegion, 15, Ink.B(Ink.White), false, FontWeights.SemiBold), Glyphs.Make("", 10, Ink.B(Ink.Hot)).Also(i => i.Margin = new(12, 0, 0, 0)) } } });
        region.Click += (_, _) => RebelCycleRegion(1);
        var test = new RowSlab(2, 56) { Margin = new(6, 0, 0, 6), Tag = "vib:test" };
        test.Left.Children.Add(test.Tint(Ink.Text("Test on controller", 19), Ink.White, Ink.Black));
        test.Right.Children.Add(test.Tint(Ink.Text(v.Readback.Text, 15, null, false, FontWeights.SemiBold), Ink.C("#c9c1c3"), Ink.Black));
        test.ToolTip = "Physical testing needs a verified connection and bounded motor command.";
        foreach (var row in new[] { strength, region, test }) { var r = row; r.GotKeyboardFocus += (_, _) => r.On = true; r.LostKeyboardFocus += (_, _) => r.On = false; list.Children.Add(r); }
        col.Children.Add(list);
        col.Children.Add(Ink.Text("Preview animation · No motor telemetry or physical vibration is generated", 13.5, Ink.B(Ink.C("#9d9598")), false).Also(t => t.Margin = new(0, 12, 0, 0)));
        g.Children.Add(Place(col, 990, 190));

        string want = focused ?? "vib:strength";
        Dispatcher.BeginInvoke(() => FindTagged(g, want)?.Focus(), DispatcherPriority.Input);
    }
    /// <summary>A small chip (.ch): dark and skewed, red when it is the value shown.</summary>
    private static Button RebelChip(string text, bool on, Action click, string tag, bool enabled = true)
    {
        var label = Ink.Text(text, 14, null, false, FontWeights.SemiBold); label.RenderTransform = null;
        var face = new Border { Height = 32, Padding = new(14, 0, 14, 0), Child = label, BorderThickness = new(1.4), RenderTransformOrigin = new(.5, .5), RenderTransform = new SkewTransform(-12, 0) };
        var b = new Button { Content = face, Tag = tag, FocusVisualStyle = null, Cursor = Cursors.Hand, Template = RebelBare(), IsEnabled = enabled, Margin = new(0, 0, 10, 0) };
        void Paint()
        {
            bool f = b.IsKeyboardFocused || b.IsMouseOver; face.Background = Ink.B(on ? Ink.Red : f ? Ink.White : Ink.Panel); face.BorderBrush = Ink.B(on ? Ink.White : f ? Colors.Transparent : Ink.A(Ink.White, .28));
            label.Foreground = Ink.B(on ? Colors.White : f ? Ink.Black : Ink.C("#e6dfe1")); face.Effect = f ? Ink.Shadow(5, 5, 0, Ink.Red, 1) : null; b.Opacity = b.IsEnabled ? 1 : .45;
        }
        b.GotKeyboardFocus += (_, _) => Paint(); b.LostKeyboardFocus += (_, _) => Paint(); b.MouseEnter += (_, _) => Paint(); b.MouseLeave += (_, _) => Paint(); b.Click += (_, _) => click(); Paint();
        return b;
    }
    private void RebelCycleRegion(int step)
    {
        if (Vibration == null) return; int i = Array.IndexOf(RebelRegions, RebelRegion);
        Vibration.SelectRegion(RebelRegions[((i < 0 ? 0 : i) + step + RebelRegions.Length) % RebelRegions.Length]); RebelLater();
    }
    /// <summary>Left and Right on the strength row move it (Shift by 5); on the region row they change the region.</summary>
    private bool RebelVibrationKey(KeyEventArgs e)
    {
        if (rebelPage != "vibration" || Vibration == null || e.Key is not (Key.Left or Key.Right) || Keyboard.FocusedElement is not FrameworkElement { Tag: string tag }) return false;
        int step = e.Key == Key.Right ? 1 : -1;
        if (tag == "vib:strength" && Vibration.Draft.CanEdit) { Vibration.Set((int)Vibration.Strength.Value + step * ((Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? 5 : 1)); e.Handled = true; return true; }
        if (tag == "vib:region") { if (!e.IsRepeat) RebelCycleRegion(step); e.Handled = true; return true; }
        return false;
    }
}
