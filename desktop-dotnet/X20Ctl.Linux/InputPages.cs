using System.Diagnostics;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using X20Ctl.Zone;
namespace X20Ctl.Linux;

/// <summary>A page that wants the keyboard (the tester's keyboard test).</summary>
internal interface IKeyPage { void OnKey(KeyEventArgs e, bool down); }

/// <summary>A stick drawn as a circle with a crosshair and a live dot. Optionally traces the edge the stick has reached.</summary>
internal sealed class StickView : Control
{
    readonly bool trace; readonly double[] reach = new double[72];
    double x, y; public bool Live { get; set; }
    public StickView(bool trace, double size) { this.trace = trace; Width = Height = size; }
    public double X => x; public double Y => y;
    public void Set(double nx, double ny)
    {
        x = nx; y = ny; Live = true;
        if (trace) { double r = Math.Sqrt(nx * nx + ny * ny); if (r > .05) { int bin = (int)((Math.Atan2(ny, nx) + Math.PI) / (2 * Math.PI) * reach.Length) % reach.Length; reach[bin] = Math.Max(reach[bin], r); } }
        InvalidateVisual();
    }
    public void Clear() { Array.Clear(reach); x = y = 0; InvalidateVisual(); }
    /// <summary>The smallest reach over the edge divided by the largest, as a percent; null until most of the edge has been rolled.</summary>
    public double? Roundness { get { var seen = reach.Where(r => r > .5).ToArray(); return seen.Length < reach.Length * .6 ? null : seen.Min() / seen.Max() * 100; } }
    public override void Render(DrawingContext ctx)
    {
        double s = Math.Min(Bounds.Width, Bounds.Height), r = s / 2 - 4; var c = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var edge = new Pen(T.B("#3A5A96"), 1.4);
        ctx.DrawEllipse(T.B("#33060B18"), edge, c, r, r); ctx.DrawEllipse(null, new Pen(T.B("#26FFFFFF"), 1), c, r * .5, r * .5);
        var cross = new Pen(T.B("#26FFFFFF"), 1); ctx.DrawLine(cross, new Point(c.X - r, c.Y), new Point(c.X + r, c.Y)); ctx.DrawLine(cross, new Point(c.X, c.Y - r), new Point(c.X, c.Y + r));
        if (trace)
        {
            var g = new StreamGeometry(); using (var o = g.Open())
            {
                bool first = true;
                for (int i = 0; i <= reach.Length; i++) { int k = i % reach.Length; double a = (k + .5) / reach.Length * 2 * Math.PI - Math.PI, rr = Math.Max(reach[k], .02) * r; var p = new Point(c.X + Math.Cos(a) * rr, c.Y + Math.Sin(a) * rr); if (first) { o.BeginFigure(p, true); first = false; } else o.LineTo(p); }
                o.EndFigure(true);
            }
            ctx.DrawGeometry(T.B("#332F7BFF"), new Pen(T.B("#7DB6FF"), 1.8), g);
        }
        if (Live) { var dot = new Point(c.X + x * r, c.Y + y * r); ctx.DrawEllipse(T.B("#55000000"), null, new Point(dot.X, dot.Y + 1.5), 8, 8); ctx.DrawEllipse(T.B("#EAF2FF"), new Pen(T.B("#7DB6FF"), 2), dot, 7, 7); }
    }
}

/// <summary>A vertical or horizontal trigger bar.</summary>
internal sealed class TriggerBar : Control
{
    readonly bool vertical; double value; public double Minimum { get; private set; } = 1; public double Maximum { get; private set; }
    public TriggerBar(bool vertical, double w, double h) { this.vertical = vertical; Width = w; Height = h; }
    public void Set(double v) { value = v; Minimum = Math.Min(Minimum, v); Maximum = Math.Max(Maximum, v); InvalidateVisual(); }
    public void Clear() { value = Maximum = 0; Minimum = 1; InvalidateVisual(); }
    public override void Render(DrawingContext ctx)
    {
        var r = new Rect(Bounds.Size); ctx.DrawRectangle(T.B("#66060B18"), new Pen(T.B("#1AFFFFFF"), 1), new RoundedRect(r, vertical ? 10 : 14));
        var fill = vertical ? new Rect(0, r.Height * (1 - value), r.Width, r.Height * value) : new Rect(0, 0, r.Width * value, r.Height);
        if (value > .004) ctx.DrawRectangle(T.PrimaryFill, null, new RoundedRect(fill, vertical ? 10 : 14));
    }
}

/// <summary>Everything the controller sends, live: buttons light on the pad, both sticks, both triggers. The keyboard test stands in
/// for a controller (WASD, arrows, Q and E).</summary>
internal sealed class TesterPage : UserControl, IKeyPage
{
    readonly StudioState state; readonly GamepadReader reader = new(); readonly HashSet<string> keys = new();
    readonly ContentControl padHost = new(); readonly StickView left = new(false, 130), right = new(false, 130); readonly TriggerBar lt = new(false, 290, 30), rt = new(false, 290, 30);
    readonly TextBlock lx = T.Label("X —", 14), ly = T.Label("Y —", 14), rx = T.Label("X —", 14), ry = T.Label("Y —", 14), ltText = T.Label("Unavailable", 14, T.Soft), rtText = T.Label("Unavailable", 14, T.Soft);
    readonly TextBlock source = T.Label("Gameplay input unavailable", 25, null, FontWeight.SemiBold), sourceNote = T.Label("Tester owns gameplay input. UI navigation is paused.", 15, T.Soft);
    readonly TextBlock waiting = T.Label("Waiting for native gameplay input", 20, null, FontWeight.SemiBold);
    bool rear, keyboard; PadFrame frame = PadFrame.Empty; readonly Dictionary<string, Border> spots = new();

    public TesterPage(StudioState state)
    {
        this.state = state; Build(); reader.Frame += f => { frame = f; Apply(); }; reader.Start(); if (reader.Connected) Apply();
        DetachedFromVisualTree += (_, _) => reader.Dispose();
    }

    void Build()
    {
        var page = new Grid { Margin = new Thickness(36, 6, 36, 14), RowDefinitions = new("Auto,*,Auto") };
        var draft = new Grid { ColumnDefinitions = new("*,Auto") };
        var dot = new Border { Width = 7, Height = 7, CornerRadius = new CornerRadius(4), Background = T.Warn, VerticalAlignment = VerticalAlignment.Center };
        var status = T.Line(20, T.Label("Working Draft", 22, null, FontWeight.SemiBold), T.Line(6, dot, T.Label("No input", 15, T.Warn)), T.Label("Gameplay tester", 15, T.Soft)); status.VerticalAlignment = VerticalAlignment.Center; draft.Children.Add(status);
        page.Children.Add(draft);

        var mid = new Grid { ColumnDefinitions = new("1.3*,1*"), Margin = new Thickness(0, 6, 0, 0) }; Grid.SetRow(mid, 1);
        var side = T.Line(12, new Pill("Front", rear ? Pill.Kinds.Ghost : Pill.Kinds.Light, () => { rear = false; Build(); }), new Pill("Back", rear ? Pill.Kinds.Light : Pill.Kinds.Ghost, () => { rear = true; Build(); })); side.VerticalAlignment = VerticalAlignment.Top; side.Margin = new Thickness(0, 6, 0, 0);
        var art = new ControllerArt(state.Model.Id, rear) { Margin = new Thickness(10, 50, 10, 0) }; spots.Clear();
        if (!rear) foreach (var (name, p) in state.Model.FrontButtons.Where(b => ButtonDraft.Targets.Contains(b.Key) || b.Key is "HOME"))
            { var ring = new Border { Width = 40, Height = 40, CornerRadius = new CornerRadius(20), Background = T.B("#01000000"), BorderBrush = T.B("#01000000"), BorderThickness = new Thickness(3), IsHitTestVisible = false }; ControllerArt.SetAnchor(ring, new Point(p.X, p.Y)); art.Children.Add(ring); spots[name] = ring; }
        var left0 = new Grid(); left0.Children.Add(art); left0.Children.Add(side); mid.Children.Add(left0);

        var keyHint = T.Stack(2, T.Label("WASD · Left stick     Arrows · Right stick", 14, T.Soft), T.Label("Q · Hold LT     E · Hold RT", 14, T.Soft));
        var exit = new Pill(keyboard ? "Stop keyboard test" : "Keyboard Test", Pill.Kinds.Ghost, () => { keyboard = !keyboard; keys.Clear(); Apply(); Build(); }) { Padding = new Thickness(26, 12), HorizontalAlignment = HorizontalAlignment.Left };
        var info = T.Stack(14, T.Label("GAMEPLAY SOURCE", 13.5, T.Soft), T.Stack(2, source, sourceNote), new Border { Height = 6 }, T.Stack(2, waiting, T.Label("Hardware configuration remains separate. Independent paddle input is shown only when exposed by the input source.", 14.5, T.Soft)), exit, keyHint);
        info.VerticalAlignment = VerticalAlignment.Center; info.Margin = new Thickness(30, 0, 0, 0); Grid.SetColumn(info, 1); mid.Children.Add(info);
        page.Children.Add(mid);

        var bottom = new Grid { ColumnDefinitions = new("*,*,1.2*"), Margin = new Thickness(0, 10, 0, 0), Height = 190 }; Grid.SetRow(bottom, 2);
        Control Stick(string title, StickView v, TextBlock x, TextBlock y) { var row = T.Line(18, v, T.Line(12, x, y)); ((Control)row.Children[1]).VerticalAlignment = VerticalAlignment.Top; ((Control)row.Children[1]).Margin = new Thickness(0, 28, 0, 0); return T.Stack(8, T.Label(title, 20, T.Text), row); }
        bottom.Children.Add(Stick("Left Stick", left, lx, ly)); var rs = Stick("Right Stick", right, rx, ry); Grid.SetColumn(rs, 1); bottom.Children.Add(rs);
        var trig = new Grid { ColumnDefinitions = new("*,*") };
        var a = T.Stack(10, T.Label("LT", 20), lt, ltText); var b = T.Stack(10, T.Label("RT", 20), rt, rtText); b.Margin = new Thickness(26, 0, 0, 0); Grid.SetColumn(b, 1); trig.Children.Add(a); trig.Children.Add(b);
        Grid.SetColumn(trig, 2); bottom.Children.Add(trig);
        var line = new Border { BorderBrush = T.Hairline, BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(0, 14, 0, 0), Child = bottom }; Grid.SetRow(line, 2);
        page.Children.Add(line);
        Content = page; Apply();
    }

    public void OnKey(KeyEventArgs e, bool down)
    {
        if (!keyboard) return;
        string? k = e.Key switch { Key.W => "W", Key.A => "A", Key.S => "S", Key.D => "D", Key.Up => "UP", Key.Down => "DOWN", Key.Left => "LEFT", Key.Right => "RIGHT", Key.Q => "Q", Key.E => "E", _ => null };
        if (k == null) return; if (down) keys.Add(k); else keys.Remove(k); e.Handled = true; Apply();
    }

    PadFrame Current()
    {
        if (!keyboard) return reader.Connected ? frame : PadFrame.Empty;
        double Axis(string neg, string pos) => (keys.Contains(pos) ? 1 : 0) - (keys.Contains(neg) ? 1 : 0);
        double kx = Axis("A", "D"), ky = Axis("W", "S"), rx0 = Axis("LEFT", "RIGHT"), ry0 = Axis("UP", "DOWN"), n = Math.Sqrt(kx * kx + ky * ky), m = Math.Sqrt(rx0 * rx0 + ry0 * ry0);
        return new PadFrame(new HashSet<string>(), n > 1 ? kx / n : kx, n > 1 ? ky / n : ky, m > 1 ? rx0 / m : rx0, m > 1 ? ry0 / m : ry0, keys.Contains("Q") ? 1 : 0, keys.Contains("E") ? 1 : 0);
    }

    void Apply()
    {
        bool live = keyboard || reader.Connected; var f = Current();
        source.Text = keyboard ? "Keyboard test" : reader.Connected ? "Gameplay input" : "Gameplay input unavailable";
        sourceNote.Text = keyboard ? "The keyboard stands in for a controller. Nothing is read from the pad." : reader.Connected ? "Read from " + reader.Device + ". Nothing is sent to the controller." : "Tester owns gameplay input. UI navigation is paused.";
        waiting.Text = live ? (keyboard ? "Keyboard input" : "Native gameplay input") : "Waiting for native gameplay input";
        foreach (var (name, ring) in spots)
        {
            bool on = f.Down.Contains(name);
            ring.Background = on ? T.B("#CC2A62C8") : T.B("#01000000"); ring.BorderBrush = on ? T.B("#CFE3FF") : T.B("#01000000");
            ring.BoxShadow = on ? new BoxShadows(new BoxShadow { Blur = 20, Color = Color.Parse("#AA3D8BFF") }) : default;
        }
        left.Live = right.Live = live; left.Set(f.LX, f.LY); right.Set(f.RX, f.RY); left.InvalidateVisual(); right.InvalidateVisual();
        string N(double v) => live ? v.ToString("+0.00;-0.00;0.00") : "—";
        lx.Text = "X " + N(f.LX); ly.Text = "Y " + N(f.LY); rx.Text = "X " + N(f.RX); ry.Text = "Y " + N(f.RY);
        lt.Set(live ? f.LT : 0); rt.Set(live ? f.RT : 0);
        ltText.Text = live ? Math.Round(f.LT * 100) + "%" : "Unavailable"; rtText.Text = live ? Math.Round(f.RT * 100) + "%" : "Unavailable";
    }
}

/// <summary>A read-only check of the controller: how round each stick is, where it rests, how far the triggers travel, which buttons chatter, and how often it reports.</summary>
internal sealed class DiagnosticsPage : UserControl
{
    sealed class Press { public int Count; public int Quick; public long Last; public bool Down; }
    readonly StudioState state; readonly GamepadReader reader = new(); readonly DispatcherTimer tick = new() { Interval = TimeSpan.FromMilliseconds(250) };
    readonly StickView left = new(true, 175), right = new(true, 175); readonly TriggerBar lt = new(true, 44, 160), rt = new(true, 44, 160);
    readonly TextBlock leftNote = T.Label("", 14, T.Soft), rightNote = T.Label("", 14, T.Soft), rate = T.Label("", 17), rateNote = T.Label("", 14, T.Soft), ltText = T.Label("No data", 14, T.Soft), rtText = T.Label("No data", 14, T.Soft);
    readonly Dictionary<string, Press> presses = new(); readonly StackPanel buttons = new() { Spacing = 8 };
    readonly List<(double X, double Y)> leftRest = [], rightRest = [];

    public DiagnosticsPage(StudioState state)
    {
        this.state = state; Build();
        reader.Frame += Observe; reader.Start();
        tick.Tick += (_, _) => Refresh(); AttachedToVisualTree += (_, _) => tick.Start(); DetachedFromVisualTree += (_, _) => { tick.Stop(); reader.Dispose(); };
        Refresh();
    }

    void Build()
    {
        var page = new Grid { Margin = new Thickness(36, 6, 36, 18), RowDefinitions = new("Auto,Auto,Auto") };
        var head = new Grid { ColumnDefinitions = new("*,Auto") };
        head.Children.Add(T.Stack(6, T.Label("Diagnostics", 38, null, FontWeight.Bold),
            T.Label("Roll each stick slowly around its edge a few times, squeeze both triggers all the way and press every button. X20CTL only listens; nothing is sent to the controller.", 15, T.Soft),
            T.Label(reader.Connected ? "Reading " + reader.Device : "Waiting for the selected player input. Connect it, then retry.", 14.5, T.Muted)));
        var actions = T.Line(10, new Pill("Retry player input", Pill.Kinds.Ghost, () => { reader.Dispose(); }) { Enabled = false, Padding = new Thickness(20, 11) }, new Pill("Clear measurements", Pill.Kinds.Ghost, Clear) { Padding = new Thickness(20, 11) }, new Pill("Copy report", Pill.Kinds.Ghost, Copy) { Padding = new Thickness(20, 11) });
        actions.VerticalAlignment = VerticalAlignment.Top; Grid.SetColumn(actions, 1); head.Children.Add(actions); page.Children.Add(head);

        var cards = new Grid { ColumnDefinitions = new("*,*,0.8*,1.1*"), Margin = new Thickness(0, 22, 0, 0), Height = 340 }; Grid.SetRow(cards, 1);
        Control Card(string title, Control body, int col) { var c = T.Card(T.Stack(10, T.Label(title, 20, null, FontWeight.SemiBold), body), 20); c.Margin = new Thickness(col == 0 ? 0 : 14, 0, 0, 0); Grid.SetColumn(c, col); return c; }
        cards.Children.Add(Card("Left stick", T.Stack(10, Parts.Center(left), leftNote), 0)); cards.Children.Add(Card("Right stick", T.Stack(10, Parts.Center(right), rightNote), 1));
        cards.Children.Add(Card("Triggers", T.Line(22, T.Stack(6, T.Label("LT", 14), lt, ltText), T.Stack(6, T.Label("RT", 14), rt, rtText)), 2));
        cards.Children.Add(Card("Observed input updates", T.Stack(8, rate, rateNote, T.Label("Updates are what the system reports to apps, not the controller's hardware polling rate.", 12.5, T.Muted)), 3));
        page.Children.Add(cards);

        var bcard = T.Card(T.Stack(10, T.Label("Buttons", 20, null, FontWeight.SemiBold), T.Label("Three rapid transitions within 15 ms are flagged for review, not diagnosed as a worn switch.", 14, T.Soft), buttons), 20); bcard.Margin = new Thickness(0, 14, 0, 0); Grid.SetRow(bcard, 2); page.Children.Add(bcard);
        Content = new ScrollViewer { Content = page };
    }

    void Observe(PadFrame f)
    {
        left.Set(f.LX, f.LY); right.Set(f.RX, f.RY); lt.Set(f.LT); rt.Set(f.RT);
        if (Math.Abs(f.LX) < .12 && Math.Abs(f.LY) < .12) { leftRest.Add((f.LX, f.LY)); if (leftRest.Count > 400) leftRest.RemoveAt(0); }
        if (Math.Abs(f.RX) < .12 && Math.Abs(f.RY) < .12) { rightRest.Add((f.RX, f.RY)); if (rightRest.Count > 400) rightRest.RemoveAt(0); }
        foreach (string b in new[] { "A", "B", "X", "Y", "LB", "RB", "LT", "RT", "L3", "R3", "SELECT", "START", "DPAD_UP", "DPAD_DOWN", "DPAD_LEFT", "DPAD_RIGHT" })
        {
            bool down = f.Down.Contains(b); if (!presses.TryGetValue(b, out var p)) presses[b] = p = new Press();
            if (down == p.Down) continue;
            double ms = (f.Stamp - p.Last) * 1000.0 / Stopwatch.Frequency; if (p.Last != 0 && ms < 15) p.Quick++;
            p.Last = f.Stamp; p.Down = down; if (down) p.Count++;
        }
    }

    void Clear() { left.Clear(); right.Clear(); lt.Clear(); rt.Clear(); presses.Clear(); leftRest.Clear(); rightRest.Clear(); Refresh(); }

    static string Rest(List<(double X, double Y)> pts, StickView v)
    {
        if (pts.Count < 20) return "Let go of the stick to measure where it rests";
        double x = pts.Average(p => p.X), y = pts.Average(p => p.Y), drift = Math.Sqrt(x * x + y * y) * 100;
        return $"Rests at {x * 100:+0.0;-0.0}%, {y * 100:+0.0;-0.0}% · drift {drift:0.0}%";
    }

    string Report()
    {
        var sb = new StringBuilder(); sb.AppendLine("X20CTL diagnostics · " + state.Model.Name + " · " + (reader.Connected ? reader.Device : "no input"));
        sb.AppendLine("Left stick: " + (left.Roundness is { } a ? $"roundness {a:0}% · " : "") + Rest(leftRest, left)); sb.AppendLine("Right stick: " + (right.Roundness is { } b ? $"roundness {b:0}% · " : "") + Rest(rightRest, right));
        sb.AppendLine($"Triggers: LT {lt.Minimum:P0}-{lt.Maximum:P0}, RT {rt.Minimum:P0}-{rt.Maximum:P0}"); sb.AppendLine($"Updates per second: {reader.Rate:0}");
        foreach (var (k, p) in presses.Where(p => p.Value.Count > 0)) sb.AppendLine($"{k}: {p.Count} presses{(p.Quick >= 3 ? $", {p.Quick} rapid transitions flagged" : "")}");
        return sb.ToString();
    }

    async void Copy() { if (TopLevel.GetTopLevel(this)?.Clipboard is { } clip) await Avalonia.Input.Platform.ClipboardExtensions.SetTextAsync(clip, Report()); }

    void Refresh()
    {
        bool live = reader.Connected;
        leftNote.Text = live ? (left.Roundness is { } a ? $"Roundness {a:0}% · " : "Roll the stick around its edge · ") + Rest(leftRest, left) : "No input source · Connect or select a controller";
        rightNote.Text = live ? (right.Roundness is { } b ? $"Roundness {b:0}% · " : "Roll the stick around its edge · ") + Rest(rightRest, right) : "No input source · Connect or select a controller";
        ltText.Text = live && lt.Maximum > 0 ? $"{lt.Minimum:P0}–{lt.Maximum:P0}" : "No data"; rtText.Text = live && rt.Maximum > 0 ? $"{rt.Minimum:P0}–{rt.Maximum:P0}" : "No data";
        rate.Text = live ? $"{reader.Rate:0} updates per second" : "No input data · Rate unavailable"; rateNote.Text = live ? "Counted from the system's joystick interface." : "Move a stick. If this stays empty, the system isn't passing this controller's reports to apps.";
        buttons.Children.Clear();
        var row = new WrapPanel();
        foreach (var (k, p) in presses.OrderBy(p => p.Key))
        {
            bool flag = p.Quick >= 3;
            row.Children.Add(new Border { Margin = new Thickness(0, 0, 10, 10), Padding = new Thickness(14, 8), CornerRadius = new CornerRadius(12), Background = p.Down ? T.B("#CC2A62C8") : T.B("#33FFFFFF"), BorderBrush = flag ? T.Warn : T.B("#12FFFFFF"), BorderThickness = new Thickness(flag ? 2 : 1), Child = T.Line(10, T.Label(Names.Of(k), 15, null, FontWeight.SemiBold), T.Label(p.Count + "×", 13.5, T.Soft), T.Label(flag ? "review" : "", 12.5, T.Warn)) });
        }
        buttons.Children.Add(row.Children.Count == 0 ? T.Label(live ? "Press every button." : "No input data", 14, T.Muted) : row);
    }
}
