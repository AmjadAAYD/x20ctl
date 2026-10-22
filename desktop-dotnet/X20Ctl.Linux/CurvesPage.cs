using Path = Avalonia.Controls.Shapes.Path;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Controls.Shapes;
using X20Ctl.Zone;
using X20Ctl.Zone.PcMode;
namespace X20Ctl.Linux;

/// <summary>Response curves for both sticks and both triggers: presets, a draggable curve, and the dead zones.</summary>
internal sealed class CurvesPage : UserControl, IKeyPage
{
    static readonly string[] Short = ["L Stick", "R Stick", "LT", "RT"], Long = ["Left stick", "Right stick", "Left trigger", "Right trigger"];
    readonly StudioState state; CurveChannel channel = CurveChannel.LeftStick;
    readonly GamepadReader reader = new(); readonly HashSet<Key> held = [];
    readonly Avalonia.Threading.DispatcherTimer ramp = new() { Interval = TimeSpan.FromMilliseconds(16) };
    CurveEditor? graph; TextBlock? hint; bool keyboard; PadFrame frame = PadFrame.Empty; double kx, ky, kl, kr;

    public CurvesPage(StudioState state)
    {
        this.state = state; Build();
        reader.Frame += f => { frame = f; Live(); }; reader.Start();
        ramp.Tick += (_, _) => Ramp();
        AttachedToVisualTree += (_, _) => ramp.Start();
        DetachedFromVisualTree += (_, _) => { ramp.Stop(); reader.Dispose(); };
    }

    /// <summary>Keyboard stand-in for the sticks and triggers: a held key eases the value to full over a fifth of a second, like a thumb.</summary>
    void Ramp()
    {
        if (!keyboard) return;
        static double Toward(double v, double target) => v + Math.Clamp(target - v, -.08, .08);
        double tx = (held.Contains(Key.D) ? 1 : 0) - (held.Contains(Key.A) ? 1 : 0), ty = (held.Contains(Key.W) ? 1 : 0) - (held.Contains(Key.S) ? 1 : 0);
        double rx = (held.Contains(Key.Right) ? 1 : 0) - (held.Contains(Key.Left) ? 1 : 0), ry = (held.Contains(Key.Up) ? 1 : 0) - (held.Contains(Key.Down) ? 1 : 0);
        bool right = channel == CurveChannel.RightStick; (double x, double y) = right ? (rx, ry) : (tx, ty);
        kx = Toward(kx, x); ky = Toward(ky, y); kl = Toward(kl, held.Contains(Key.Q) ? 1 : 0); kr = Toward(kr, held.Contains(Key.E) ? 1 : 0);
        Live();
    }

    void Live()
    {
        if (graph == null) return;
        double? push = keyboard
            ? channel switch { CurveChannel.LeftStick or CurveChannel.RightStick => Math.Min(1, Math.Sqrt(kx * kx + ky * ky)), CurveChannel.LeftTrigger => kl, _ => kr }
            : reader.Connected ? channel switch
            {
                CurveChannel.LeftStick => Math.Min(1, Math.Sqrt(frame.LX * frame.LX + frame.LY * frame.LY)), CurveChannel.RightStick => Math.Min(1, Math.Sqrt(frame.RX * frame.RX + frame.RY * frame.RY)),
                CurveChannel.LeftTrigger => frame.LT, _ => frame.RT
            } : null;
        graph.Show(push);
        if (hint != null) hint.Text = keyboard ? "Keyboard test · WASD, arrows, Q and E" : reader.Connected ? "Live input from the controller" : "Connect a controller or use Keyboard test";
    }

    public void OnKey(KeyEventArgs e, bool down)
    {
        if (!keyboard) return;
        if (e.Key is Key.W or Key.A or Key.S or Key.D or Key.Up or Key.Down or Key.Left or Key.Right or Key.Q or Key.E)
        { if (down) held.Add(e.Key); else held.Remove(e.Key); e.Handled = true; }
    }

    void Build()
    {
        var cur = state.Curves[channel]; var tune = state.Tune(channel);
        var page = new Grid { Margin = new Thickness(36, 4, 36, 14), RowDefinitions = new("*,Auto") };
        var upper = new Grid { ColumnDefinitions = new("1*,1.45*"), MinHeight = 480 };

        var art = new ControllerArt(state.Model.Id) { Margin = new Thickness(0, 4, 0, 0) };
        string? stick = channel switch { CurveChannel.LeftStick => "L3", CurveChannel.RightStick => "R3", _ => null };
        if (stick != null && state.Model.FrontButtons.TryGetValue(stick, out var p))
        {
            var glow = new Border { Width = 70, Height = 70, CornerRadius = new CornerRadius(35), Background = T.B("#992A62C8"), BorderBrush = T.B("#9CCBFF"), BorderThickness = new Thickness(3), IsHitTestVisible = false, BoxShadow = new BoxShadows(new BoxShadow { Blur = 26, Color = Color.Parse("#AA3D8BFF") }) };
            ControllerArt.SetAnchor(glow, new Point(p.X, p.Y)); art.Children.Add(glow);
        }
        var title = channel is CurveChannel.LeftStick or CurveChannel.RightStick ? "STICK RESPONSE" : "TRIGGER TRAVEL";
        var leftBody = new Grid { RowDefinitions = new("Auto,*") }; leftBody.Children.Add(T.Label(title, 14.5, T.Text, FontWeight.Medium)); leftBody.Children.Add(Place(art, 1));
        var left = T.Card(leftBody, 18);
        upper.Children.Add(left);

        var notice = (Phone.Active ? Parts.Notice("Curves · Draft", "Kept on this phone for the selected player. Nothing is written to the controller.") : Parts.Notice("Curves · PC mode", "Saved on this PC for the selected player. Nothing is written to the controller."));
        var tools = T.Line(10, new Pill("Reset curve", Pill.Kinds.Ghost, () => { state.Curves.Reset(channel); state.ResetTune(channel); Build(); }) { Enabled = !IsDefault(cur, tune), Padding = new Thickness(22, 11) }, new Pill("Advanced", Pill.Kinds.Ghost, () => Content = new StickToolsView(state, () => Build())) { Padding = new Thickness(22, 11) },
            new Pill("Reset options ⌄", Pill.Kinds.Ghost, () => { state.Curves.ResetAll(); state.ResetTunes(); Build(); }) { Padding = new Thickness(22, 11) }, new Pill(keyboard ? "Stop keyboard test" : "Keyboard test", keyboard ? Pill.Kinds.Light : Pill.Kinds.Ghost, () => { keyboard = !keyboard; held.Clear(); kx = ky = kl = kr = 0; Build(); }) { Padding = new Thickness(22, 11) });
        var channels = T.Line(10, Enum.GetValues<CurveChannel>().Select(c => (Control)new Pill(Short[(int)c], c == channel ? Pill.Kinds.Light : Pill.Kinds.Ghost, () => { channel = c; Build(); })).ToArray());
        // the presets stand in a column beside the graph, so the graph has the card's height
        var presets = T.Stack(8, CurveDraft.Presets.Keys.Select(k => Preset(k, cur)).ToArray());
        string state2 = IsDefault(cur, tune) ? "Unmodified" : "Modified";
        graph = new CurveEditor(state, channel, () => Build()) { MinHeight = 250, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
        var rightBody = new Grid { RowDefinitions = new("Auto,Auto,Auto,*") };
        void Add(Control c, int row, double top = 0) { c.Margin = new Thickness(0, top, 0, 0); Grid.SetRow(c, row); rightBody.Children.Add(c); }
        // the channel beside its name
        var heading = new Grid { ColumnDefinitions = new("*,Auto") };
        heading.Children.Add(T.Stack(0, T.Label(Long[(int)channel], 34, null, FontWeight.SemiBold), T.Label(Cap(cur.Preset) + " curve · " + state2, 16, T.Soft)));
        channels.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(channels, 1); heading.Children.Add(channels);
        var body = new Grid { ColumnDefinitions = new("*,Auto") };
        body.Children.Add(graph); presets.Margin = new Thickness(16, 6, 0, 0); Grid.SetColumn(presets, 1); body.Children.Add(presets);
        Add(notice, 0); Add(heading, 1, 14); Add(tools, 2, 12); Add(body, 3, 14);
        upper.Children.Add(Wrap(T.Card(rightBody, 18, 18), 1));
        page.Children.Add(upper);

        var inner = new Grid { ColumnDefinitions = new("*,*,*"), Margin = new Thickness(0, 14, 0, 0) };
        var a = T.Stack(10, Slide("Inner dead zone", tune.Inner, 0, 40, v => state.SetInner(channel, v), false), Slide("Anti-dead zone", tune.AntiDeadzone, 0, 40, v => { tune.AntiDeadzone = v; Build(); }, false));
        var b = T.Stack(10, Slide("Full output at", tune.Outer, 60, 100, v => state.SetOuter(channel, v), true), Slide("Max output", tune.MaxOutput, 60, 100, v => { tune.MaxOutput = v; Build(); }, true));
        b.Margin = new Thickness(26, 0, 0, 0); Grid.SetColumn(b, 1);
        var msg = hint = T.Label("Connect a controller or use Keyboard test", 14, T.Soft); msg.VerticalAlignment = VerticalAlignment.Center; msg.HorizontalAlignment = HorizontalAlignment.Center; Grid.SetColumn(msg, 2);
        inner.Children.Add(a); inner.Children.Add(b); inner.Children.Add(msg);
        var sliders = T.Card(inner, 22); Grid.SetRow(sliders, 1); page.Children.Add(sliders);
        // the curve takes the height the sliders leave; on a short window the page scrolls
        Content = new FillScroll(page, 680); Live();
    }

    static Control Place(Control c, int row) { Grid.SetRow(c, row); return c; }
    static Control Wrap(Control c, int column) { Grid.SetColumn(c, column); c.Margin = new Thickness(18, 0, 0, 0); return c; }
    static string Cap(string s) => s.Length == 0 ? s : char.ToUpper(s[0]) + s[1..];
    static bool IsDefault(CurveSetting c, StickTuning t) => c == CurveSetting.Default && t.AntiDeadzone == 0 && t.MaxOutput == 100;

    Control Slide(string label, double value, double min, double max, Action<double> set, bool fillBlue)
    {
        var text = T.Label(label + "   " + Math.Round(value) + "%", 14.5, T.Text);
        var slider = new FlatSlider { Minimum = min, Maximum = max, Fill = fillBlue, Value = value };
        slider.ValueChanged += v => text.Text = label + "   " + Math.Round(v) + "%";
        slider.PointerReleased += (_, _) => set(slider.Value);
        return T.Stack(2, text, slider);
    }

    Control Preset(string key, CurveSetting cur)
    {
        bool on = cur.Preset == key; var pts = CurveDraft.Presets[key];
        var icon = new Path { Width = 30, Height = 18, Stretch = Stretch.None, Stroke = on ? T.B("#2F7BFF") : T.AccentHi, StrokeThickness = 1.6, Data = Mini(pts.First, pts.Second) };
        var label = T.Label(Cap(key), 14.5, on ? T.B("#0A1226") : T.Text, FontWeight.Medium); label.VerticalAlignment = VerticalAlignment.Center;
        var chip = new Border { Background = on ? T.B("#F4F7FD") : T.ButtonFill, BorderBrush = on ? Brushes.Transparent : T.Hairline, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(40), Padding = new Thickness(16, 8), Cursor = new Cursor(StandardCursorType.Hand), Child = T.Line(8, icon, label) };
        chip.OnTap(() => { state.Curves.SetPreset(channel, key); Build(); });
        return chip;
    }

    static Geometry Mini((int X, int Y) a, (int X, int Y) b)
    {
        var g = new StreamGeometry();
        using var c = g.Open();
        for (int i = 0; i <= 24; i++)
        {
            double x = i / 24.0, y = PadProcessor.Through(x, a.X / 255.0, a.Y / 255.0, b.X / 255.0, b.Y / 255.0);
            var pt = new Point(1 + x * 28, 17 - y * 16); if (i == 0) c.BeginFigure(pt, false); else c.LineTo(pt);
        }
        c.EndFigure(false); return g;
    }
}

/// <summary>The graph a game's input runs through, with two handles to drag. Drawn from the same maths PC mode runs.</summary>
internal sealed class CurveEditor : Control
{
    readonly StudioState state; readonly CurveChannel channel; readonly Action changed; int dragging = -1; double? live;
    public void Show(double? push) { if (live == push) return; live = push; InvalidateVisual(); }
    const double Left = 34, Right = 40, Top = 24, Bottom = 44;

    public CurveEditor(StudioState state, CurveChannel channel, Action changed) { this.state = state; this.channel = channel; this.changed = changed; Cursor = new Cursor(StandardCursorType.Arrow); }

    /// <summary>The plot fills the control from its top left: wider than tall, as on Windows (408 x 240), never wider than
    /// 2.2 times its height nor narrower than square.</summary>
    Rect Plot
    {
        get
        {
            double w = Math.Max(60, Bounds.Width - Left - Right), h = Math.Max(60, Bounds.Height - Top - Bottom);
            if (w < h) h = w; else w = Math.Min(w, h * 2.2);
            return new Rect(Left, Top, w, h);
        }
    }
    SetupCurve Curve => CurveGraph.Of(state.Curves[channel]);

    Point Handle(int i)
    {
        var c = state.Curves[channel]; var tune = state.Tune(channel); var (hx, hy) = i == 0 ? c.Point1 : c.Point2;
        double inner = tune.Inner / 100, outer = tune.Outer / 100, push = inner + hx / 255.0 * (outer - inner);
        double y = PadProcessor.Response(Curve, tune, push); var r = Plot;
        return new Point(r.X + push * r.Width, r.Bottom - y * r.Height);
    }

    public override void Render(DrawingContext ctx)
    {
        var r = Plot; var tune = state.Tune(channel);
        var grid = new Pen(T.B("#26FFFFFF"), 1);
        for (int i = 0; i <= 4; i++) { double x = r.X + r.Width * i / 4, y = r.Bottom - r.Height * i / 4; ctx.DrawLine(grid, new Point(x, r.Y), new Point(x, r.Bottom)); ctx.DrawLine(grid, new Point(r.X, y), new Point(r.Right, y)); Tick(ctx, (i * 25).ToString(), new Point(x, r.Bottom + 8), true); Tick(ctx, (i * 25).ToString(), new Point(r.X - 10, y), false); }
        Words(ctx, "OUTPUT %", new Point(r.X + 4, r.Y - 18), 11.5, T.Soft); Words(ctx, "INPUT %", new Point(r.Center.X - 24, r.Bottom + 32), 11.5, T.Soft);
        var area = new StreamGeometry(); var line = new StreamGeometry();
        using (var a = area.Open()) using (var l = line.Open())
        {
            a.BeginFigure(new Point(r.X, r.Bottom), true);
            for (int i = 0; i <= 120; i++)
            {
                double x = i / 120.0, y = PadProcessor.Response(Curve, tune, x); var pt = new Point(r.X + x * r.Width, r.Bottom - y * r.Height);
                a.LineTo(pt); if (i == 0) l.BeginFigure(pt, false); else l.LineTo(pt);
            }
            a.LineTo(new Point(r.Right, r.Bottom)); a.EndFigure(true); l.EndFigure(false);
        }
        ctx.DrawGeometry(new LinearGradientBrush { StartPoint = new(0, 0, RelativeUnit.Relative), EndPoint = new(0, 1, RelativeUnit.Relative), GradientStops = { new(Color.Parse("#402F7BFF"), 0), new(Color.Parse("#082F7BFF"), 1) } }, null, area);
        ctx.DrawGeometry(null, new Pen(T.B("#4DA3FF"), 3.2, lineCap: PenLineCap.Round), line);
        // the dead-zone and full-output markers on the axes
        Marker(ctx, new Point(r.X + tune.Inner / 100 * r.Width - 4, r.Bottom), "#FF5C7A", true); Marker(ctx, new Point(r.X + tune.Outer / 100 * r.Width, r.Bottom), "#34D399", true);
        Marker(ctx, new Point(r.Right + 8, r.Bottom - tune.MaxOutput / 100 * r.Height), "#F5B45B", false); Marker(ctx, new Point(r.Right + 8, r.Bottom - tune.AntiDeadzone / 100 * r.Height), "#F5B45B", false);
        if (live is double lv)
        {
            double ly = PadProcessor.Response(Curve, tune, lv); var at = new Point(r.X + lv * r.Width, r.Bottom - ly * r.Height);
            ctx.DrawLine(new Pen(T.B("#80F5B45B"), 1.5, dashStyle: DashStyle.Dash), new Point(at.X, r.Bottom), at); ctx.DrawLine(new Pen(T.B("#80F5B45B"), 1.5, dashStyle: DashStyle.Dash), new Point(r.X, at.Y), at);
            ctx.DrawEllipse(T.B("#F5B45B"), new Pen(T.B("#FFFFFF"), 2), at, 7, 7);
        }
        for (int i = 0; i < 2; i++) { var h = Handle(i); ctx.DrawEllipse(T.B("#55000000"), null, new Point(h.X, h.Y + 2), 11, 11); ctx.DrawEllipse(T.B("#EAF2FF"), new Pen(T.B("#7DB6FF"), 2.5), h, 9, 9); }
    }

    static void Marker(DrawingContext ctx, Point p, string colour, bool vertical)
    {
        var rect = vertical ? new Rect(p.X - 3.5, p.Y - 8, 7, 16) : new Rect(p.X - 8, p.Y - 3.5, 16, 7);
        ctx.DrawRectangle(T.B(colour), new Pen(T.B("#80FFFFFF"), 1), new RoundedRect(rect, 3.5));
    }
    static void Tick(DrawingContext ctx, string s, Point at, bool below) => Words(ctx, s, below ? new Point(at.X - 8, at.Y) : new Point(at.X - 22, at.Y - 9), 12, T.Soft);
    static void Words(DrawingContext ctx, string s, Point at, double size, IBrush brush)
    {
        var f = new FormattedText(s, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(T.Font), size, brush); ctx.DrawText(f, at);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        var p = e.GetPosition(this); double d0 = Dist(p, Handle(0)), d1 = Dist(p, Handle(1));
        dragging = Math.Min(d0, d1) < 22 ? (d0 <= d1 ? 0 : 1) : -1; if (dragging >= 0) e.Pointer.Capture(this);
    }
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        var p = e.GetPosition(this);
        if (dragging < 0) { Cursor = new Cursor(Dist(p, Handle(0)) < 22 || Dist(p, Handle(1)) < 22 ? StandardCursorType.Hand : StandardCursorType.Arrow); return; }
        var r = Plot; var c = state.Curves[channel]; var tune = state.Tune(channel);
        double push = Math.Clamp((p.X - r.X) / r.Width, 0, 1), outv = Math.Clamp((r.Bottom - p.Y) / r.Height, 0, 1);
        double inner = tune.Inner / 100, outer = tune.Outer / 100;
        int x = (int)Math.Round(Math.Clamp((push - inner) / Math.Max(.01, outer - inner), 0, 1) * 255);
        double floor = tune.AntiDeadzone / 100, ceil = tune.MaxOutput / 100;
        int y = (int)Math.Round(Math.Clamp((outv - floor) / Math.Max(.01, ceil - floor), 0, 1) * 255);
        var next = dragging == 0 ? c with { Point1 = (Math.Min(x, c.Point2.X), y), Preset = "custom" } : c with { Point2 = (Math.Max(x, c.Point1.X), y), Preset = "custom" };
        try { state.Curves.Set(channel, next); } catch (ArgumentException) { }
        InvalidateVisual();
    }
    protected override void OnPointerReleased(PointerReleasedEventArgs e) { if (dragging >= 0) { dragging = -1; e.Pointer.Capture(null); changed(); } }
    static double Dist(Point a, Point b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
}

/// <summary>Kept for the curve maths shared with the table and the graph.</summary>
internal static class CurveGraph
{
    public static SetupCurve Of(CurveSetting c) => new(c.Preset, c.BasePreset, c.Inner, c.Outer, c.Point1.X, c.Point1.Y, c.Point2.X, c.Point2.Y);
}
