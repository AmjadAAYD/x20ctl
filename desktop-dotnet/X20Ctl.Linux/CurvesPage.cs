using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using X20Ctl.Zone;
using X20Ctl.Zone.PcMode;
namespace X20Ctl.Linux;

/// <summary>Response curves for both sticks and both triggers: presets, dead zones, and the line a game really gets.</summary>
internal sealed class CurvesPage : UserControl
{
    readonly StudioState state; CurveChannel channel = CurveChannel.LeftStick;
    public CurvesPage(StudioState state) { this.state = state; Build(); }

    static readonly string[] Names = ["Left stick", "Right stick", "Left trigger", "Right trigger"];

    void Build()
    {
        var tabs = Ui.Row(); int i = 0;
        foreach (var c in Enum.GetValues<CurveChannel>()) { var ch = c; tabs.Children.Add(Ui.Chip(Names[i++], ch == channel, () => { channel = ch; Build(); })); }
        var cur = state.Curves[channel];
        var presets = Ui.Row();
        foreach (string p in CurveDraft.Presets.Keys) { string name = p; presets.Children.Add(Ui.Chip(char.ToUpper(name[0]) + name[1..], cur.Preset == name, () => { state.Curves.SetPreset(channel, name); Build(); })); }
        var graph = Ui.Panel(new CurveGraph(cur) { Width = 360, Height = 360 }, 10); graph.HorizontalAlignment = HorizontalAlignment.Left;
        var left = Ui.Column(tabs, graph, presets);
        var right = Ui.Column(
            Ui.Label(Names[(int)channel], 18, null, FontWeight.SemiBold),
            Ui.Label("Pushed this far, the game gets", 12, Ui.Quiet), Table(cur),
            Slider("Inner dead zone", cur.Inner, 0, 40, v => Apply(cur with { Inner = v })),
            Slider("Outer travel limit", cur.Outer, 60, 100, v => Apply(cur with { Outer = v })),
            Ui.Chip("Reset", false, () => { state.Curves.Reset(channel); Build(); }));
        var grid = new Grid { ColumnDefinitions = new("Auto,*") };
        var side = Ui.Panel(right); side.Margin = new Thickness(14, 0, 0, 0); Grid.SetColumn(side, 1);
        grid.Children.Add(left); grid.Children.Add(side);
        Content = new ScrollViewer { Content = grid };
    }

    void Apply(CurveSetting next) { try { state.Curves.Set(channel, next); } catch (ArgumentException) { } Build(); }

    Control Slider(string label, int value, int min, int max, Action<int> set)
    {
        var s = new Slider { Minimum = min, Maximum = max, Value = value, Width = 300, HorizontalAlignment = HorizontalAlignment.Left };
        var text = Ui.Label(label + " · " + value + "%", 13, Ui.Quiet);
        s.PointerCaptureLost += (_, _) => set((int)Math.Round(s.Value));
        s.PropertyChanged += (_, e) => { if (e.Property == Avalonia.Controls.Primitives.RangeBase.ValueProperty) text.Text = label + " · " + (int)Math.Round(s.Value) + "%"; };
        return Ui.Column(text, s);
    }

    static Control Table(CurveSetting c)
    {
        var sc = CurveGraph.Of(c);
        var g = new Grid { ColumnDefinitions = new("*,*,*,*,*") };
        int col = 0;
        foreach (int p in new[] { 20, 40, 60, 80, 100 })
        {
            var cell = Ui.Column(Ui.Label(p + "%", 11, Ui.Quiet), Ui.Label(Math.Round(PadProcessor.Response(sc, null, p / 100.0) * 100) + "%", 15));
            Grid.SetColumn(cell, col++); g.Children.Add(cell);
        }
        return g;
    }
}

/// <summary>The curve a game receives, drawn from the same maths PC mode runs.</summary>
internal sealed class CurveGraph(CurveSetting c) : Control
{
    public static SetupCurve Of(CurveSetting c) => new(c.Preset, c.BasePreset, c.Inner, c.Outer, c.Point1.X, c.Point1.Y, c.Point2.X, c.Point2.Y);

    public override void Render(DrawingContext ctx)
    {
        double w = Bounds.Width, h = Bounds.Height;
        var grid = new Pen(Ui.Line, 1);
        for (int i = 0; i <= 4; i++) { ctx.DrawLine(grid, new Point(w * i / 4, 0), new Point(w * i / 4, h)); ctx.DrawLine(grid, new Point(0, h * i / 4), new Point(w, h * i / 4)); }
        ctx.DrawLine(new Pen(Ui.Quiet, 1, DashStyle.Dash), new Point(0, h), new Point(w, 0));
        var sc = Of(c);
        var line = new StreamGeometry();
        using (var g = line.Open())
            for (int i = 0; i <= 100; i++)
            {
                var p = new Point(w * i / 100, h - h * PadProcessor.Response(sc, null, i / 100.0));
                if (i == 0) g.BeginFigure(p, false); else g.LineTo(p);
            }
        ctx.DrawGeometry(null, new Pen(Ui.Accent, 3), line);
    }
}
