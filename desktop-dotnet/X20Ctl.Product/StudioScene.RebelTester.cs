using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using X20Ctl.Product.Rebel;
namespace X20Ctl.Product;

/// <summary>The Rebel Tester page (owner board tester.png): the pad sits in a ring of comic bursts, what it says is typed on a sheet of paper,
/// the sticks are scopes and the triggers are tall gauges. The live picture and every value are the Tester workbench's own.</summary>
public partial class StudioScene
{
    private Grid? rebelTester; private TesterWorkbench? rebelTesterBench;
    private TextBlock? rebelTSource, rebelTPressed, rebelTLeft, rebelTRight, rebelTLt, rebelTRt; private Ellipse? rebelTDotL, rebelTDotR; private Rectangle? rebelTFillL, rebelTFillR; private Button? rebelTKeys; private TextBlock? rebelTKeysLabel;
    private ChipTab? rebelTFront, rebelTBack;
    private const double ScopeR = 26;

    private void RebelTesterPage()
    {
        var w = Tester; var g = rebelTester!; g.Children.Clear();
        if (w == null) return;
        if (!ReferenceEquals(w, rebelTesterBench)) { rebelTesterBench = w; w.Updated += () => { if (rebelPage == "tester" && ReferenceEquals(rebelTesterBench, Tester)) RebelTesterRefresh(); }; }

        // the ring of bursts round the pad
        var bursts = new Canvas { Width = 1600, Height = 900, IsHitTestVisible = false };
        foreach (var (x, y, r) in new[] { (445d, 218d, 38d), (740, 222, 38), (290, 298, 42), (888, 298, 38), (262, 457, 40), (920, 462, 40), (322, 600, 40), (864, 603, 38), (481, 638, 38), (697, 645, 38) })
        {
            var b = RebelBurstShape(x, y, r * .55, r, Ink.C("#1b191c")); b.Stroke = Ink.B(Ink.A(Ink.White, .08)); b.StrokeThickness = 1.2; bursts.Children.Add(b);
        }
        g.Children.Add(bursts);
        var stage = w.TakeStage(); var view = new Viewbox { Child = stage, Stretch = Stretch.Uniform, Width = 520, Height = 347, IsHitTestVisible = false, Effect = Ink.Shadow(10, 12, 0, Ink.Black, 1) }; g.Children.Add(Place(view, 330, 246));

        // the front and the back, and whose pad it is
        var tabs = new StackPanel { Orientation = Orientation.Horizontal };
        rebelTFront = new ChipTab("Front", 14) { Tag = "te:front", Width = 84, On = !w.RearShown }; rebelTFront.Click += (_, _) => { w.ShowRear(false); RebelTesterRefresh(); };
        rebelTBack = new ChipTab("Back", 14) { Tag = "te:back", Width = 84, On = w.RearShown }; rebelTBack.Click += (_, _) => { w.ShowRear(true); RebelTesterRefresh(); };
        tabs.Children.Add(rebelTFront); tabs.Children.Add(rebelTBack);
        tabs.Children.Add(new Border { Margin = new(14, 0, 0, 0), Padding = new(14, 5, 14, 5), BorderBrush = Ink.B(Ink.White), BorderThickness = new(1.6), Child = Ink.Text($"PLAYER {Player + 1}", 12.5, Ink.B(Ink.White), false, FontWeights.Bold), RenderTransform = Ink.Slant, VerticalAlignment = VerticalAlignment.Center });
        g.Children.Add(Place(tabs, 400, 680));

        // the sheet
        var paper = new Grid { Width = 590, Height = 440, RenderTransformOrigin = new(.5, .5), RenderTransform = new RotateTransform(1.2) };
        paper.Children.Add(new TornPaper { Seed = 17, UnderSheet = Ink.B(Ink.Red) });
        var body = new StackPanel { Margin = new(34, 34, 34, 20) }; paper.Children.Add(body);
        body.Children.Add(Ink.Label("Gameplay source", 12, Ink.B(Ink.Red)));
        rebelTSource = Ink.Text(w.SourceText, 28, Ink.B(Ink.Black)); rebelTSource.Margin = new(0, 6, 0, 0); body.Children.Add(rebelTSource);
        body.Children.Add(RebelBody(w.OwnershipText, 15).Also(t => { t.Margin = new(0, 12, 0, 0); t.RenderTransform = Ink.Slant; }));
        rebelTPressed = RebelBody(w.PressedText, 15); rebelTPressed.Margin = new(0, 8, 0, 0); rebelTPressed.RenderTransform = Ink.Slant; body.Children.Add(rebelTPressed);
        body.Children.Add(RebelBody("Hardware configuration remains separate. Independent paddle input is shown only when exposed by the input source.", 15).Also(t => { t.Margin = new(0, 8, 0, 0); t.RenderTransform = Ink.Slant; }));
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 22, 0, 0) };
        row.Children.Add(RebelPrimary("Exit Tester", w.ClickExit, "te:exit", true, "B").Also(b => b.Margin = new(0, 0, 14, 0)));
        rebelTKeys = RebelBt(w.KeyboardText, null, () => { w.ClickKeyboard(); RebelTesterRefresh(); }, "te:keys", 15, 44, true); rebelTKeysLabel = null; row.Children.Add(rebelTKeys);
        body.Children.Add(row);
        body.Children.Add(RebelBody("WASD · Left stick     Arrows · Right stick     Q · Hold LT     E · Hold RT", 13, "#6b6064").Also(t => { t.Margin = new(0, 14, 0, 0); t.RenderTransform = Ink.Slant; }));
        g.Children.Add(Place(paper, 958, 172));

        // the sticks as scopes, the triggers as gauges
        FrameworkElement Scope(string label, double x, out Ellipse dot, out TextBlock val)
        {
            var c = new Canvas { Width = 56, Height = 56, IsHitTestVisible = false };
            c.Children.Add(new Ellipse { Width = 52, Height = 52, Stroke = Ink.B(Ink.Red), StrokeThickness = 2.6, Fill = Ink.B(Ink.C("#0f0e10")) }.Also(e => { Canvas.SetLeft(e, 2); Canvas.SetTop(e, 2); }));
            c.Children.Add(new Line { X1 = 8, X2 = 48, Y1 = 28, Y2 = 28, Stroke = Ink.B(Ink.A(Ink.White, .35)) }); c.Children.Add(new Line { X1 = 28, X2 = 28, Y1 = 8, Y2 = 48, Stroke = Ink.B(Ink.A(Ink.White, .35)) });
            dot = new Ellipse { Width = 8, Height = 8, Fill = Ink.B(Ink.White) }; Canvas.SetLeft(dot, 24); Canvas.SetTop(dot, 24); c.Children.Add(dot);
            var text = new StackPanel { Margin = new(14, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center }; text.Children.Add(Ink.Text(label, 20));
            val = Ink.Text("X — Y —", 13, Ink.B(Ink.C("#9d9598")), false); text.Children.Add(val);
            var rowp = new StackPanel { Orientation = Orientation.Horizontal }; rowp.Children.Add(c); rowp.Children.Add(text); return Place(rowp, x, 734);
        }
        FrameworkElement Gauge(string label, double x, out Rectangle fill, out TextBlock val)
        {
            var c = new Canvas { Width = 30, Height = 60, IsHitTestVisible = false, RenderTransform = new SkewTransform(-10, 0) };
            c.Children.Add(new Rectangle { Width = 26, Height = 56, Stroke = Ink.B(Ink.A(Ink.White, .8)), StrokeThickness = 1.8, Fill = Ink.B(Ink.C("#0f0e10")) }.Also(e => { Canvas.SetLeft(e, 2); Canvas.SetTop(e, 2); }));
            fill = new Rectangle { Width = 22, Height = 0, Fill = Ink.B(Ink.Red) }; Canvas.SetLeft(fill, 4); Canvas.SetTop(fill, 58); c.Children.Add(fill);
            var text = new StackPanel { Margin = new(14, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center }; text.Children.Add(Ink.Text(label, 20));
            val = Ink.Text("Unavailable", 13, Ink.B(Ink.C("#9d9598")), false); text.Children.Add(val);
            var rowp = new StackPanel { Orientation = Orientation.Horizontal }; rowp.Children.Add(c); rowp.Children.Add(text); return Place(rowp, x, 732);
        }
        g.Children.Add(Scope("Left Stick", 270, out var dl, out var tl)); rebelTDotL = dl; rebelTLeft = tl;
        g.Children.Add(Scope("Right Stick", 595, out var dr, out var tr)); rebelTDotR = dr; rebelTRight = tr;
        g.Children.Add(Gauge("LT", 915, out var fl, out var t1)); rebelTFillL = fl; rebelTLt = t1;
        g.Children.Add(Gauge("RT", 1240, out var fr, out var t2)); rebelTFillR = fr; rebelTRt = t2;

        var note = new StackPanel { Orientation = Orientation.Horizontal }; note.Children.Add(new Canvas { Width = 22, Height = 22, Children = { Star.Make(11, 11, 8, 0, 1) } });
        note.Children.Add(Ink.Text("Tester owns gameplay input · UI input navigation paused", 14, Ink.B(Ink.C("#9d9598")), false).Also(x => { x.Margin = new(6, 0, 0, 0); x.RenderTransform = Ink.Slant; }));
        g.Children.Add(Place(note, 262, 842));
        RebelTesterRefresh();
        Dispatcher.BeginInvoke(() => FindTagged(g, "te:exit")?.Focus(), DispatcherPriority.Input);
    }
    /// <summary>Moves what changes with the input: the source and what is held, the scopes and the gauges.</summary>
    private void RebelTesterRefresh()
    {
        var w = Tester; if (w == null || rebelTSource == null) return;
        rebelTSource.Text = w.SourceText; rebelTPressed!.Text = w.PressedText;
        if (rebelTFront != null) { rebelTFront.On = !w.RearShown; rebelTBack!.On = w.RearShown; }
        if (rebelTKeys?.Content is Border { Child: StackPanel { Children.Count: > 0 } sp } && sp.Children[^1] is TextBlock kt) kt.Text = w.KeyboardText;
        void Stick(Ellipse dot, TextBlock val, double? x, double? y)
        {
            Canvas.SetLeft(dot, 24 + (x ?? 0) * ScopeR * .8); Canvas.SetTop(dot, 24 - (y ?? 0) * ScopeR * .8);
            val.Text = x.HasValue ? $"X {x:0.00}   Y {y:0.00}" : "X —   Y —";
        }
        Stick(rebelTDotL!, rebelTLeft!, w.LeftX, w.LeftY); Stick(rebelTDotR!, rebelTRight!, w.RightX, w.RightY);
        void Trigger(Rectangle fill, TextBlock val, double? v)
        {
            double h = Math.Clamp(v ?? 0, 0, 1) * 52; fill.Height = h; Canvas.SetTop(fill, 56 - h + 2); val.Text = v.HasValue ? $"{v * 100:0}%" : "Unavailable";
        }
        Trigger(rebelTFillL!, rebelTLt!, w.LeftTrigger); Trigger(rebelTFillR!, rebelTRt!, w.RightTrigger);
    }
}
