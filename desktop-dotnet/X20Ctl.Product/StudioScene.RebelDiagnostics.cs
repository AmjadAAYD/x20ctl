using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using X20Ctl.Product.Rebel;
namespace X20Ctl.Product;

/// <summary>The Rebel Diagnostics page (owner board diagnostics.png): the sticks are two scopes, the triggers tall gauges on a red sheet, what
/// was observed runs along a strip. The live shapes and every value are the Diagnostics workbench's own; a Details view holds the buttons and reports.</summary>
public partial class StudioScene
{
    private Grid? rebelDiag; private Diagnostics.DiagnosticsWorkbench? rebelDiagBench; private bool rebelDiagDetails;
    private List<(Border Cell, TextBlock Count, TextBlock Name)>? rebelDiagCells; private Border? rebelDiagStrip; private TextBlock? rebelDiagPlayer, rebelDiagRate;

    private void RebelDiagnosticsPage()
    {
        var w = Diagnostics; var g = rebelDiag!; g.Children.Clear();
        if (w == null) return;
        if (!ReferenceEquals(w, rebelDiagBench)) { rebelDiagBench = w; w.Refreshed += () => { if (rebelPage == "diagnostics" && ReferenceEquals(rebelDiagBench, Diagnostics)) RebelDiagRefresh(); }; }
        string? focused = (Keyboard.FocusedElement as FrameworkElement) is { Tag: string t } fe && g.IsAncestorOf(fe) ? t : null;
        foreach (var tb in new[] { w.SourceBlock, w.RateBlock, w.RawBlock, w.NumericBlock, w.DriftBlock }) (tb.Parent as Panel)?.Children.Remove(tb);

        // the toolbar
        var bar = new StackPanel { Orientation = Orientation.Horizontal };
        int shown = w.ChosenSlot >= 0 ? w.ChosenSlot : w.ActiveSlot >= 0 ? w.ActiveSlot : w.PreferredSlot;
        var drop = new StackPanel { Orientation = Orientation.Horizontal, Children = { Ink.Text("Player input", 14, Ink.B(Colors.White), false, FontWeights.SemiBold),
            new Border { Margin = new(10, 0, 0, 0), Padding = new(10, 2, 10, 2), Background = Ink.B(Ink.Black), Child = new StackPanel { Orientation = Orientation.Horizontal, Children = { Ink.Text($"XInput {shown + 1}", 13, Ink.B(Colors.White), false, FontWeights.SemiBold), Glyphs.Make("", 9, Ink.B(Ink.Hot)).Also(i => i.Margin = new(8, 0, 0, 0)) } } } } };
        var player = new Button { Content = new Border { Height = 34, Padding = new(14, 0, 14, 0), Background = Ink.B(Ink.Red), BorderBrush = Ink.B(Colors.White), BorderThickness = new(1.4), RenderTransform = new SkewTransform(-12, 0), Child = drop }, Tag = "dg:player", Template = RebelBare(), FocusVisualStyle = null, Cursor = Cursors.Hand, Margin = new(0, 0, 8, 0) };
        player.Click += (_, _) => w.ChooseSlot(((w.ChosenSlot < 0 ? w.PreferredSlot : w.ChosenSlot) + 1) % 4); player.Click += (_, _) => RebelLater();
        bar.Children.Add(player);
        bar.Children.Add(RebelBt("Retry player input", null, () => { w.ChooseSlot(-1); RebelLater(); }, "dg:retry", 13.5, 34));
        bar.Children.Add(RebelDiagToggle("Show integer XInput values", w.RawValues, () => { w.ToggleRaw(); RebelLater(); }, "dg:raw"));
        bar.Children.Add(RebelDiagToggle("Measure centre deviation", w.Measuring, () => { w.Measure(); RebelLater(); }, "dg:measure"));
        bar.Children.Add(RebelBt("Clear measurements", null, () => w.ClearAll(), "dg:clear", 13.5, 34));
        bar.Children.Add(RebelBt("Copy report", null, w.CopyReport, "dg:copy", 13.5, 34));
        bar.Children.Add(RebelBt(rebelDiagDetails ? "Scopes" : "Details", null, () => { rebelDiagDetails = !rebelDiagDetails; RebelLater(); }, "dg:view", 13.5, 34));
        g.Children.Add(Place(bar, 268, 204));
        w.DriftBlock.FontFamily = Ink.Body; w.DriftBlock.FontSize = 13.5; w.DriftBlock.Foreground = Ink.B(Ink.C("#cfc7c9")); w.DriftBlock.Margin = new(0); w.DriftBlock.TextWrapping = TextWrapping.NoWrap; w.DriftBlock.RenderTransform = Ink.Slant; w.DriftBlock.RenderTransformOrigin = new(0, .5);
        g.Children.Add(Place(w.DriftBlock, 272, 250));

        if (!rebelDiagDetails)
        {
            RebelDiagScope(g, w, true, 500); RebelDiagScope(g, w, false, 880);
            // the triggers on the red sheet
            g.Children.Add(RPoly(Ink.B(Ink.Red), 1120, 282, 1540, 262, 1556, 676, 1104, 690));
            var dots = RPoly(Dots.Tile(Ink.A(Ink.Black, .5), 11, 2.4), 1120, 282, 1540, 262, 1556, 676, 1104, 690);
            dots.OpacityMask = Dots.Fade(135, .7); g.Children.Add(dots);
            RebelDiagGauge(g, "LT", w.LtView, 1185, 3); RebelDiagGauge(g, "RT", w.RtView, 1388, -3);
        }
        else RebelDiagDetailsView(g, w);

        // the strip along the bottom
        rebelDiagStrip = new Border { Width = 1277, Height = 72, Background = Ink.B(Ink.C("#121012")), BorderBrush = Ink.B(Ink.A(Ink.White, .22)), BorderThickness = new(1.4), RenderTransformOrigin = new(.5, .5), RenderTransform = new RotateTransform(-.5) };
        var inner = new Grid { Margin = new(28, 8, 28, 6) }; rebelDiagStrip.Child = inner;
        var left = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left }; left.Children.Add(Ink.Label("Observed input updates", 11, Ink.B(Ink.Hot)));
        rebelDiagRate = Ink.Text("", 20, Ink.B(Ink.White)); left.Children.Add(rebelDiagRate); inner.Children.Add(left);
        w.SourceBlock.FontFamily = Ink.Body; w.SourceBlock.FontSize = 15; w.SourceBlock.Foreground = Ink.B(Ink.C("#cfc7c9")); w.SourceBlock.Margin = new(0); w.SourceBlock.TextTrimming = TextTrimming.CharacterEllipsis; w.SourceBlock.MaxWidth = 720; w.SourceBlock.TextWrapping = TextWrapping.NoWrap; w.SourceBlock.RenderTransform = Ink.Slant; w.SourceBlock.VerticalAlignment = VerticalAlignment.Center; w.SourceBlock.HorizontalAlignment = HorizontalAlignment.Right;
        inner.Children.Add(w.SourceBlock);
        g.Children.Add(Place(rebelDiagStrip, 268, 708));
        var note = new StackPanel { Orientation = Orientation.Horizontal }; note.Children.Add(new Canvas { Width = 22, Height = 22, Children = { Star.Make(11, 11, 8, 0, 1) } });
        note.Children.Add(Ink.Text("Read-only input capture · Use Esc or the mouse to leave", 14, Ink.B(Ink.C("#9d9598")), false).Also(x => { x.Margin = new(6, 0, 0, 0); x.RenderTransform = Ink.Slant; }));
        g.Children.Add(Place(note, 262, 842));
        RebelDiagRefresh();
        string want = focused ?? "dg:player"; Dispatcher.BeginInvoke(() => FindTagged(g, want)?.Focus(), DispatcherPriority.Input);
    }
    private static Button RebelDiagToggle(string text, bool on, Action click, string tag)
    {
        var label = Ink.Text(text, 13.5, null, true); label.RenderTransform = null; label.VerticalAlignment = VerticalAlignment.Center;
        var track = RebelToggle(on, () => false); track.RenderTransform = new SkewTransform(0, 0); track.Margin = new(12, 0, 0, 0); track.LayoutTransform = new ScaleTransform(.7, .7);
        var face = new Border { Height = 34, Padding = new(14, 0, 12, 0), Child = new StackPanel { Orientation = Orientation.Horizontal, Children = { label, track } }, BorderThickness = new(1.4), RenderTransformOrigin = new(.5, .5), RenderTransform = new SkewTransform(-12, 0) };
        var b = new Button { Content = face, Tag = tag, Template = RebelBare(), FocusVisualStyle = null, Cursor = Cursors.Hand, Margin = new(0, 0, 8, 0) };
        void Paint() { bool f = b.IsKeyboardFocused || b.IsMouseOver; face.Background = Ink.B(f ? Ink.White : Ink.Black); face.BorderBrush = Ink.B(f ? Colors.Transparent : Ink.White); label.Foreground = Ink.B(f ? Ink.Black : Ink.White); face.Effect = f ? Ink.Shadow(5, 5, 0, Ink.Red, 1) : null; }
        b.GotKeyboardFocus += (_, _) => Paint(); b.LostKeyboardFocus += (_, _) => Paint(); b.MouseEnter += (_, _) => Paint(); b.MouseLeave += (_, _) => Paint(); b.Click += (_, _) => click(); Paint(); return b;
    }
    /// <summary>A stick as a scope: rings, ticks and a crosshair, with the workbench's own reach, dot and resting mark inside.</summary>
    private void RebelDiagScope(Grid g, Diagnostics.DiagnosticsWorkbench w, bool leftStick, double cx)
    {
        const double cy = 438, r = 140; double f = r / X20Ctl.Product.Diagnostics.DiagnosticsWorkbench.UnitSize, plot = X20Ctl.Product.Diagnostics.DiagnosticsWorkbench.PlotSize;
        var (reach, dot, rest, stats) = leftStick ? w.LeftView : w.RightView;
        foreach (UIElement e in new UIElement[] { reach, dot, rest, stats }) { if (e is FrameworkElement fe && fe.Parent is Panel p) p.Children.Remove(fe); }
        var c = new Canvas { Width = 400, Height = 400, IsHitTestVisible = false }; double o = 200;
        c.Children.Add(new Ellipse { Width = 2 * (r + 6), Height = 2 * (r + 6), Fill = Ink.B(Colors.Black) }.Also(e => { Canvas.SetLeft(e, o - r - 6 + 8); Canvas.SetTop(e, o - r - 6 + 10); }));
        c.Children.Add(new Ellipse { Width = 2 * (r + 6), Height = 2 * (r + 6), Fill = Ink.B(Ink.C("#0f0e10")), Stroke = Ink.B(Ink.Red), StrokeThickness = 5 }.Also(e => { Canvas.SetLeft(e, o - r - 6); Canvas.SetTop(e, o - r - 6); }));
        foreach (var k in new[] { .33, .66, 1 }) c.Children.Add(new Ellipse { Width = 2 * r * k, Height = 2 * r * k, Stroke = Ink.B(Ink.A(Ink.White, k < 1 ? .2 : .5)), StrokeThickness = 1.4 }.Also(e => { Canvas.SetLeft(e, o - r * k); Canvas.SetTop(e, o - r * k); }));
        for (int i = 0; i < 36; i++)
        {
            double a = i * Math.PI / 18, len = i % 9 == 0 ? 14 : 7;
            c.Children.Add(new Line { X1 = o + r * Math.Cos(a), Y1 = o + r * Math.Sin(a), X2 = o + (r - len) * Math.Cos(a), Y2 = o + (r - len) * Math.Sin(a), Stroke = Ink.B(Ink.White), StrokeThickness = i % 9 == 0 ? 2 : 1, Opacity = .7 });
        }
        foreach (var (x1, y1, x2, y2) in new[] { (o - r + 16, o, o - 18, o), (o + 18, o, o + r - 16, o), (o, o - r + 16, o, o - 18), (o, o + 18, o, o + r - 16) })
            c.Children.Add(new Line { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Stroke = Ink.B(Ink.Red), StrokeThickness = 2.2 });
        var live = new Canvas { Width = plot, Height = plot, RenderTransform = new ScaleTransform(f, f, plot / 2, plot / 2), IsHitTestVisible = false };
        reach.Stroke = Ink.B(Ink.Hot); reach.StrokeThickness = 1.6; reach.Fill = Ink.B(Ink.A(Ink.Red, .28)); rest.Fill = Ink.B(Ink.C("#ffb347"));
        live.Children.Add(reach); live.Children.Add(rest); live.Children.Add(dot); Canvas.SetLeft(live, o - plot / 2); Canvas.SetTop(live, o - plot / 2); c.Children.Add(live);
        g.Children.Add(Place(c, cx - o, cy - o));
        var label = new StackPanel { Width = 300, HorizontalAlignment = HorizontalAlignment.Left };
        label.Children.Add(Ink.Text(leftStick ? "Left stick" : "Right stick", 19).Also(t => t.HorizontalAlignment = HorizontalAlignment.Center));
        stats.FontFamily = Ink.Body; stats.FontSize = 12.5; stats.Foreground = Ink.B(Ink.C("#9d9598")); stats.Margin = new(0, 3, 0, 0); stats.LineHeight = 17; stats.TextAlignment = TextAlignment.Center; stats.TextWrapping = TextWrapping.Wrap; stats.RenderTransform = Ink.Slant; stats.RenderTransformOrigin = new(.5, .5);
        label.Children.Add(stats);
        if (w.UseFor(leftStick) is { } use) { (use.Use.Parent as Panel)?.Children.Remove(use.Use); (use.Said.Parent as Panel)?.Children.Remove(use.Said); use.Use.Height = 28; use.Use.Margin = new(0, 6, 0, 0); use.Use.HorizontalAlignment = HorizontalAlignment.Center; use.Use.Tag = leftStick ? "dg:use-l" : "dg:use-r"; use.Said.FontSize = 12; use.Said.TextAlignment = TextAlignment.Center; use.Said.TextWrapping = TextWrapping.Wrap; label.Children.Add(use.Use); label.Children.Add(use.Said); }
        g.Children.Add(Place(label, cx - 150, 598));
    }
    private void RebelDiagGauge(Grid g, string name, (Rectangle Bar, Rectangle Range, TextBlock Stats) v, double x, double tilt)
    {
        foreach (UIElement e in new UIElement[] { v.Bar, v.Range, v.Stats }) { if (e is FrameworkElement fe && fe.Parent is Panel p) p.Children.Remove(fe); }
        v.Bar.Fill = Ink.B(Ink.Red); v.Bar.RadiusX = v.Bar.RadiusY = 0; v.Range.Fill = Ink.B(Ink.A(Ink.White, .3));
        var well = new Grid { Width = 42, Height = 150 }; well.Children.Add(new Rectangle { Fill = Ink.B(Ink.C("#16141a")), Stroke = Ink.B(Ink.White), StrokeThickness = 1.4 });
        well.Children.Add(v.Range); well.Children.Add(v.Bar);
        var ticks = new Canvas { IsHitTestVisible = false }; for (int i = 1; i < 14; i++) ticks.Children.Add(new Line { X1 = 3, X2 = 39, Y1 = i * 150 / 14.0, Y2 = i * 150 / 14.0, Stroke = Ink.B(Ink.A(Ink.White, .22)) }); well.Children.Add(ticks);
        var slab = new Viewbox { Child = well, Stretch = Stretch.Fill, Width = 66, Height = 260, RenderTransformOrigin = new(.5, .5), RenderTransform = new RotateTransform(tilt), Effect = Ink.Shadow(5, 6, 0, Colors.Black, .8) };
        g.Children.Add(Place(slab, x, 302));
        var col = new StackPanel { Width = 150, HorizontalAlignment = HorizontalAlignment.Left }; col.Children.Add(Ink.Text(name, 20, Ink.B(Colors.White)).Also(t => t.HorizontalAlignment = HorizontalAlignment.Center));
        v.Stats.FontFamily = Ink.Body; v.Stats.FontSize = 12.5; v.Stats.Foreground = Ink.B(Ink.A(Ink.White, .75)); v.Stats.TextAlignment = TextAlignment.Center; v.Stats.Margin = new(0, 2, 0, 0); v.Stats.LineHeight = 16; v.Stats.TextWrapping = TextWrapping.Wrap; v.Stats.RenderTransform = Ink.Slant; v.Stats.RenderTransformOrigin = new(.5, .5);
        col.Children.Add(v.Stats); g.Children.Add(Place(col, x - 42, 586));
    }
    /// <summary>The buttons pressed and the reports seen.</summary>
    private void RebelDiagDetailsView(Grid g, Diagnostics.DiagnosticsWorkbench w)
    {
        g.Children.Add(new Rectangle { Width = 1280, Height = 410, Fill = Ink.B(Ink.C("#0f0e10")), Stroke = Ink.B(Ink.A(Ink.White, .2)), StrokeThickness = 1.4, RenderTransform = new RotateTransform(-.3) }.Also(r => RPut(r, 268, 276)));
        g.Children.Add(Place(Ink.Label("Buttons", 12, Ink.B(Ink.Hot)), 296, 292));
        g.Children.Add(Place(Ink.Text("Reported XInput buttons only. Three rapid transitions within 15 ms are flagged for review, not diagnosed as a worn switch.", 13, Ink.B(Ink.C("#9d9598")), false).Also(t => { t.Width = 700; t.TextWrapping = TextWrapping.Wrap; }), 296, 316));
        rebelDiagCells = new();
        var wrap = new WrapPanel { Width = 760 };
        foreach (var row in w.ButtonRows())
        {
            var name = Ink.Text(row.Name, 17, Ink.B(Colors.White)); var count = Ink.Text(row.Count, 12.5, Ink.B(Ink.C("#9d9598")), false);
            var cell = new Border { Width = 140, Height = 66, Margin = new(0, 0, 10, 10), Padding = new(12, 8, 12, 6), BorderThickness = new(1.4), Child = new StackPanel { Children = { name, count } } };
            wrap.Children.Add(cell); rebelDiagCells.Add((cell, count, name));
        }
        g.Children.Add(Place(wrap, 296, 372));
        var right = new StackPanel { Width = 420 }; right.Children.Add(Ink.Label("Reports", 12, Ink.B(Ink.Hot)));
        foreach (var tb in new[] { w.RateBlock, w.NumericBlock, w.RawBlock })
        { tb.FontFamily = Ink.Body; tb.FontSize = 13.5; tb.Foreground = Ink.B(Ink.C("#cfc7c9")); tb.TextWrapping = TextWrapping.Wrap; tb.Margin = new(0, 10, 0, 0); tb.RenderTransform = Ink.Slant; tb.RenderTransformOrigin = new(0, .5); right.Children.Add(tb); }
        right.Children.Add(Ink.Text("XInput state changes are not hardware polling rate. Raw Input report counts are separate, unassociated devices; they are not verified as this selected controller.", 12.5, Ink.B(Ink.C("#9d9598")), false).Also(t => { t.TextWrapping = TextWrapping.Wrap; t.Margin = new(0, 12, 0, 0); }));
        g.Children.Add(Place(right, 1090, 292));
    }
    private void RebelDiagRefresh()
    {
        var w = Diagnostics; if (w == null) return;
        if (rebelDiagRate != null) rebelDiagRate.Text = w.RateBlock.Text.Split('\n')[0];
        if (rebelDiagCells != null)
        {
            var rows = w.ButtonRows();
            for (int i = 0; i < rows.Count && i < rebelDiagCells.Count; i++)
            {
                var (cell, count, name) = rebelDiagCells[i]; var r = rows[i];
                cell.Background = Ink.B(r.Down ? Ink.Red : Ink.C("#171517")); cell.BorderBrush = Ink.B(r.Warn ? Ink.C("#ffb347") : r.Used ? Ink.A(Ink.White, .55) : Ink.A(Ink.White, .18)); cell.ToolTip = r.Tip;
                count.Text = r.Count; count.Foreground = Ink.B(r.Warn ? Ink.C("#ffb347") : r.Down ? Colors.White : Ink.C("#9d9598"));
            }
        }
    }
}
