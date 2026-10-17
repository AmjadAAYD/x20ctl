using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using X20Ctl.Zone;
using X20Ctl.Zone.Diagnostics;
using X20Ctl.Zone.PcMode;
namespace X20Ctl.Linux;

/// <summary>The response a game gets for a push from 0 to 100%, drawn with the same function the engine uses.</summary>
internal sealed class ResponseChart : Control
{
    public SetupCurve? Curve { get; set; } public StickTuning? Tuning { get; set; }
    public ResponseChart() { Height = 150; MinWidth = 300; }
    public override void Render(DrawingContext ctx)
    {
        double w = Bounds.Width - 40, h = Bounds.Height - 30; var axis = new Pen(T.B("#668A97B4"), 1); var line = new Pen(T.B("#7DB6FF"), 2.2, lineJoin: PenLineJoin.Round);
        ctx.DrawLine(axis, new Point(30, h + 6), new Point(30 + w, h + 6)); ctx.DrawLine(axis, new Point(30, 6), new Point(30, h + 6));
        var geo = new StreamGeometry(); using (var c = geo.Open())
        {
            for (int v = 0; v <= 100; v++) { var p = new Point(30 + v / 100.0 * w, h + 6 - PadProcessor.Response(Curve, Tuning, v / 100.0) * h); if (v == 0) c.BeginFigure(p, false); else c.LineTo(p); }
            c.EndFigure(false);
        }
        ctx.DrawGeometry(null, line, geo);
        var f = new FormattedText("Physical input → Game output", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(T.Font), 12, T.Soft);
        ctx.DrawText(f, new Point(36, Bounds.Height - 18));
    }
}

/// <summary>Curves → Advanced: the fine stick tools. Dead zones in 0.1% steps, a square gate, vertical sensitivity, drift measuring and a table of what the game gets.
/// PC mode applies them on this PC; nothing is written to the controller. They replace the curve's whole-percent dead zones.</summary>
internal sealed class StickToolsView : UserControl
{
    static readonly int[] Table = [5, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100];
    static readonly (string Name, double? Inner, double? Outer, double? Anti, double? Max)[] Presets =
    [("Linear range", 0, 100, 0, 100), ("Dead zone 5%", 5, null, null, null), ("Full at 80%", null, 80, null, null), ("Full at 65%", null, 65, null, null), ("Anti-deadzone 8%", null, null, 8, null), ("Anti-deadzone 15%", null, null, 15, null), ("Anti-deadzone off", null, null, 0, null)];
    readonly StudioState state; readonly Action done; CurveChannel stick = CurveChannel.LeftStick; bool linked; string message = ""; StickTuning? undo; StickTuning? undoOther;
    (DriftObservation Left, DriftObservation Right)? sampled; bool measuring; double progress;

    public StickToolsView(StudioState state, Action done) { this.state = state; this.done = done; Build(); }
    CurveChannel Other => stick == CurveChannel.LeftStick ? CurveChannel.RightStick : CurveChannel.LeftStick;
    static string Name(CurveChannel c) => c == CurveChannel.LeftStick ? "Left stick" : "Right stick";

    SetupCurve CurveOf(CurveChannel c) { var v = state.Curves[c]; var s = new SetupCurve(v.Preset, v.BasePreset, v.Inner, v.Outer, v.Point1.X, v.Point1.Y, v.Point2.X, v.Point2.Y); return PcModeProfile.IsDefault(s) ? null! : s; }

    void Edit(Action<StickTuning> change)
    {
        var t = state.Tune(stick); change(t); Fix(t);
        if (linked) { var o = state.Tune(Other); Copy(t, o); }
        message = ""; Build();
    }
    static void Fix(StickTuning t) { t.Inner = Math.Round(Math.Clamp(t.Inner, 0, StickTuning.MaxInner), 1); t.Outer = Math.Round(Math.Clamp(t.Outer, StickTuning.MinOuter, 100), 1); if (t.Inner >= t.Outer) t.Inner = Math.Round(t.Outer - .1, 1); if (t.AntiDeadzone >= t.MaxOutput) t.AntiDeadzone = Math.Round(t.MaxOutput - .1, 1); }
    static void Copy(StickTuning a, StickTuning b) { b.Inner = a.Inner; b.Outer = a.Outer; b.AntiDeadzone = a.AntiDeadzone; b.MaxOutput = a.MaxOutput; b.SquareGate = a.SquareGate; b.Raw = a.Raw; b.AxisRatio = a.AxisRatio; }
    static StickTuning Clone(StickTuning a) { var b = new StickTuning(); Copy(a, b); return b; }

    Control Slider(string label, double min, double max, double value, string unit, Action<StickTuning, double> set, bool enabled = true)
    {
        var s = new FlatSlider { Minimum = min, Maximum = max, Step = label is "Vertical sensitivity" ? 1 : .1, Value = value, Opacity = enabled ? 1 : .4, IsEnabled = enabled };
        s.PointerReleased += (_, _) => Edit(t => set(t, Math.Round(s.Value, 1)));
        var g = new Grid { ColumnDefinitions = new("170,*,80") }; g.Children.Add(T.Label(label, 14.5, T.Text));
        Grid.SetColumn(s, 1); s.VerticalAlignment = VerticalAlignment.Center; g.Children.Add(s);
        var v = T.Label(Math.Round(value, 1).ToString("0.0") + unit, 14, T.Soft); v.HorizontalAlignment = HorizontalAlignment.Right; Grid.SetColumn(v, 2); g.Children.Add(v); return g;
    }

    void Build()
    {
        var t = state.Tune(stick); bool raw = t.Raw; var curve = CurveOf(stick);
        var page = new StackPanel { Spacing = 12, Margin = new Thickness(36, 6, 36, 18) };
        page.Children.Add(T.Label("Advanced stick tools", 34, null, FontWeight.Bold));
        var sub = T.Label("PC-side processing for accessible analog sticks · Nothing is written to controller memory. Sliders adjust in 0.1% steps. Apply Changes saves them.", 15, T.Soft); sub.TextWrapping = TextWrapping.Wrap; page.Children.Add(sub);

        var pick = T.Line(8, new Pill("Left stick", stick == CurveChannel.LeftStick ? Pill.Kinds.Light : Pill.Kinds.Ghost, () => { stick = CurveChannel.LeftStick; Build(); }), new Pill("Right stick", stick == CurveChannel.RightStick ? Pill.Kinds.Light : Pill.Kinds.Ghost, () => { stick = CurveChannel.RightStick; Build(); }),
            Switches.Make(linked, v => { linked = v; if (v) Copy(t, state.Tune(Other)); Build(); }), T.Label("Link stick settings", 14.5, T.Text));
        foreach (var c in pick.Children.OfType<TextBlock>()) c.VerticalAlignment = VerticalAlignment.Center; page.Children.Add(pick);

        var copy = new Pill("Copy to " + Name(Other).ToLowerInvariant(), Pill.Kinds.Ghost, () => { undo = Clone(state.Tune(Other)); undoOther = null; Copy(t, state.Tune(Other)); message = $"{Name(stick)} settings copied to {Name(Other).ToLowerInvariant()}."; Build(); }) { Padding = new Thickness(18, 8) };
        var head = new Grid { ColumnDefinitions = new("*,Auto") }; head.Children.Add(T.Label(Name(stick), 21, null, FontWeight.SemiBold)); Grid.SetColumn(copy, 1); head.Children.Add(copy);
        var rawRow = T.Line(12, Switches.Make(raw, v => Edit(x => x.Raw = v)), T.Label("Bypass X20CTL processing", 14.5, T.Text), T.Label("Uses the values before X20CTL curves, range and gate adjustments.", 13, T.Muted));
        foreach (var c in rawRow.Children.OfType<TextBlock>()) c.VerticalAlignment = VerticalAlignment.Center;
        var gate = T.Line(12, Switches.Make(t.SquareGate, v => Edit(x => x.SquareGate = v), !raw), T.Label("Gate: ○ Circular / □ Square", 14.5, T.Text), T.Label("Diagonals reach full on both axes, as on a square-gated stick.", 13, T.Muted));
        foreach (var c in gate.Children.OfType<TextBlock>()) c.VerticalAlignment = VerticalAlignment.Center;
        var presets = T.Wrap(6, Presets.Select(p => (Control)new Pill(p.Name, Pill.Kinds.Ghost, () => Edit(x => { if (p.Inner is { } a) x.Inner = a; if (p.Outer is { } b) x.Outer = b; if (p.Anti is { } c) x.AntiDeadzone = c; if (p.Max is { } d) x.MaxOutput = d; })) { Padding = new Thickness(14, 7), Enabled = !raw }).ToArray());
        var body = T.Stack(10, head, rawRow, Parts.Caps("INPUT RANGE"),
            Slider("Inner dead zone", 0, StickTuning.MaxInner, t.Inner, "%", (x, v) => x.Inner = v, !raw), Slider("Full output at", StickTuning.MinOuter, 100, t.Outer, "%", (x, v) => x.Outer = v, !raw),
            Parts.Caps("OUTPUT RANGE"), Slider("Minimum output", 0, StickTuning.MaxAnti, t.AntiDeadzone, "%", (x, v) => x.AntiDeadzone = v, !raw), Slider("Maximum output", StickTuning.MinOuter, 100, t.MaxOutput, "%", (x, v) => x.MaxOutput = v, !raw),
            gate, Slider("Vertical sensitivity", StickTuning.MinRatio, StickTuning.MaxRatio, t.AxisRatio, "%", (x, v) => x.AxisRatio = (int)Math.Round(v), !raw), presets);
        page.Children.Add(T.Card(body, 24));

        var chart = new ResponseChart { Curve = curve, Tuning = t.IsDefault ? null : t };
        var line = string.Join("   ", Table.Select(p => $"{p}% → {PadProcessor.Response(curve, t.IsDefault ? null : t, p / 100.0) * 100:0}%"));
        var tl = T.Label(raw ? "Direct input · X20CTL processing bypassed" : line, 12.5, T.Muted); tl.TextWrapping = TextWrapping.Wrap;
        page.Children.Add(T.Card(T.Stack(8, Parts.Caps("WHAT THE GAME GETS"), chart, tl), 22));

        var actions = T.Wrap(10, new Pill("Done", Pill.Kinds.Primary, done) { Padding = new Thickness(26, 12) },
            new Pill(measuring ? $"Sampling {progress * 5:0.0} / 5 s" : "Measure drift", Pill.Kinds.Ghost, Measure) { Padding = new Thickness(24, 12), Enabled = !measuring },
            new Pill("Reset selected advanced settings", Pill.Kinds.Ghost, () => { Copy(new StickTuning(), t); if (linked) Copy(new StickTuning(), state.Tune(Other)); message = ""; Build(); }) { Padding = new Thickness(24, 12) });
        if (undo != null) actions.Children.Add(new Pill("Undo copy", Pill.Kinds.Ghost, () => { Copy(undo, state.Tune(Other)); undo = null; message = "Copy undone."; Build(); }) { Padding = new Thickness(24, 12), Margin = new Thickness(0, 0, 10, 10) });
        if (sampled is { } r && r.Left.Stable && r.Right.Stable) actions.Children.Add(new Pill("Apply recommendations", Pill.Kinds.Primary, () => { state.Tune(CurveChannel.LeftStick).Inner = r.Left.RecommendedInner; state.Tune(CurveChannel.RightStick).Inner = r.Right.RecommendedInner; Fix(state.Tune(CurveChannel.LeftStick)); Fix(state.Tune(CurveChannel.RightStick)); sampled = null; message = "Suggested inner dead zones applied."; Build(); }) { Padding = new Thickness(24, 12), Margin = new Thickness(0, 0, 10, 10) });
        page.Children.Add(actions);
        if (message.Length > 0) { var m = T.Label(message, 14, T.Soft); m.TextWrapping = TextWrapping.Wrap; page.Children.Add(m); }
        Content = new ScrollViewer { Content = page };
    }

    /// <summary>Centre guard: five seconds of readings with the sticks left alone. Each stick's inner dead zone goes just past the farthest it wandered, so drift and jitter never reach the game. Read-only.</summary>
    async void Measure()
    {
        using var reader = new GamepadReader(); reader.Start();
        if (!reader.Connected) { message = "No controller input found. Connect one and try again."; Build(); return; }
        measuring = true; progress = 0; message = "Release both sticks · Sampling…"; Build();
        var left = new DriftObservation(); var right = new DriftObservation(); var watch = System.Diagnostics.Stopwatch.StartNew();
        while (watch.ElapsedMilliseconds < 5000)
        {
            await Task.Delay(8); var f = reader.Snapshot();
            left.Add((short)Math.Clamp(f.LX * 32767, -32768, 32767), (short)Math.Clamp(-f.LY * 32767, -32768, 32767)); right.Add((short)Math.Clamp(f.RX * 32767, -32768, 32767), (short)Math.Clamp(-f.RY * 32767, -32768, 32767));
            progress = watch.ElapsedMilliseconds / 5000.0;
        }
        measuring = false;
        if (!left.Stable || !right.Stable) { message = "A stick moved during sampling. Release both sticks and try again."; sampled = null; }
        else { sampled = (left, right); message = $"Observed peak deviation · Left {left.Peak:0.0}% · Right {right.Peak:0.0}%. Suggested inner dead zone · Left {left.RecommendedInner:0.0}% · Right {right.RecommendedInner:0.0}%."; }
        Build();
    }
}
