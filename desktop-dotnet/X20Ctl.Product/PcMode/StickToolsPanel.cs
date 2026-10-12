using System.IO;
using X20Ctl.Product.Scanner;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using X20Ctl.Zone;
using X20Ctl.Zone.PcMode;
namespace X20Ctl.Product;

/// <summary>What the Studio needs to reach PC mode: its settings file, the running service and the saved choices.</summary>
public sealed record PcModeLink(PcModeStore Store, PcModeService Service, ProductPreferences? Preferences);

/// <summary>
/// Curves → Advanced (owner request, 11 Oct 2026): the fine stick tools. Dead zones in 0.1% steps, a square gate, the
/// up/down speed, and a number table of what the game gets. PC mode applies them on this PC for any controller, EasySMX
/// or not; nothing is written to the controller. They replace PC mode's whole-percent stick dead zones.
/// </summary>
public sealed class StickToolsPanel : Border
{
    static readonly (string Name, CurveChannel Channel)[] Sticks = [("Left stick", CurveChannel.LeftStick), ("Right stick", CurveChannel.RightStick)];
    static readonly int[] TablePoints = [5, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100];
    readonly PcModeLink link;
    sealed record Row(Slider Inner, Slider Outer, Slider Anti, Slider Max, CheckBox Square, CheckBox Raw, Slider Ratio, TextBlock Table, Panel Tuning);
    readonly Dictionary<CurveChannel, Row> rows = new();
    /// <summary>One-click starting points: (name, tooltip, inner, outer, anti-deadzone, max output). Null keeps that value.</summary>
    static readonly (string Name, string Tip, double? Inner, double? Outer, double? Anti, double? Max)[] Presets =
    [
        ("Clean 1:1 range", "No dead zones, no floor, full output", 0, 100, 0, 100),
        ("Dead zone 5%", "A common factory dead zone", 5, null, null, null),
        ("Aim: full at 80%", "Reaches full speed a little early, for steadier aiming at the edge", null, 80, null, null),
        ("Flick: full at 65%", "Full speed very early, for fast turns", null, 65, null, null),
        ("Anti-deadzone 8%", "Small moves register in games with a dead zone of their own", null, null, 8, null),
        ("Anti-deadzone 15%", "For games with a large dead zone of their own", null, null, 15, null),
        ("Anti-deadzone off", "No output floor", null, null, 0, null),
    ];
    readonly TextBlock status, saved;
    readonly Border notice;
    bool loading, pending;
    readonly Button measure;
    public Button Done { get; }

    public StickToolsPanel(PcModeLink link, Action close)
    {
        this.link = link;
        CornerRadius = new(22); BorderThickness = new(1); Padding = new(40, 30, 40, 30); MaxWidth = 1080; HorizontalAlignment = HorizontalAlignment.Center; VerticalAlignment = VerticalAlignment.Center;
        Background = new LinearGradientBrush(Color.FromArgb(250, 14, 24, 52), Color.FromArgb(250, 8, 14, 34), 90);
        BorderBrush = new LinearGradientBrush(Color.FromArgb(120, 120, 170, 255), Color.FromArgb(40, 120, 170, 255), 90);
        var stack = new StackPanel(); Child = new ScrollViewer { Content = stack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 720 };
        stack.Children.Add(Kit.Text("Advanced stick tools", 30, "DS.Text", FontWeights.Bold));
        stack.Children.Add(Wrap("Dead zones in 0.1% steps, anti-deadzone, max output, a square gate, up/down speed and pure raw 1:1, for any controller. PC mode applies them on this PC; nothing is written to the controller. Click a slider, then the arrow keys nudge it by 0.1%.", 15, "DS.TextSoft", 8));
        status = Wrap("", 14, "DS.TextMuted", 10); stack.Children.Add(status);

        notice = new Border { CornerRadius = new(14), Padding = new(20, 16, 20, 16), Margin = new(0, 16, 0, 0), BorderThickness = new(1), Visibility = Visibility.Collapsed,
            Background = new SolidColorBrush(Color.FromArgb(70, 120, 80, 10)), BorderBrush = new SolidColorBrush(Color.FromArgb(160, 245, 200, 80)) };
        var accept = Pill("I understand"); accept.Margin = new(0, 12, 0, 0); accept.HorizontalAlignment = HorizontalAlignment.Left; accept.Click += (_, _) => Accept();
        notice.Child = new StackPanel { Children = { Kit.Text("Before PC mode starts", 17, "DS.Text", FontWeights.SemiBold),
            Wrap(PcModePage.NoticeLines[0], 14.5, "DS.TextSoft", 8), Wrap(PcModePage.NoticeLines[1], 14.5, "DS.TextSoft", 4), Wrap(PcModePage.NoticeLines[2], 14.5, "DS.TextSoft", 4), accept } };
        stack.Children.Add(notice);

        foreach (var (name, channel) in Sticks)
        {
            var inner = Fine(0, StickTuning.MaxInner, 0); var outer = Fine(StickTuning.MinOuter, 100, 100);
            var anti = Fine(0, StickTuning.MaxAnti, 0); var max = Fine(StickTuning.MinOuter, 100, 100);
            var innerText = Value("0.0%"); var outerText = Value("100.0%"); var antiText = Value("0.0%"); var maxText = Value("100.0%");
            var square = new CheckBox { Style = DS.Style("DS.Toggle"), VerticalAlignment = VerticalAlignment.Center };
            var rawBox = new CheckBox { Style = DS.Style("DS.Toggle"), VerticalAlignment = VerticalAlignment.Center };
            var ratio = new Slider { Minimum = StickTuning.MinRatio, Maximum = StickTuning.MaxRatio, Width = 170, Value = 100, TickFrequency = 5, IsSnapToTickEnabled = true, SmallChange = 5, LargeChange = 10, VerticalAlignment = VerticalAlignment.Center };
            var ratioText = Value("100%");
            var table = Kit.Text("", 12.5, "DS.TextMuted").Also(t => { t.Margin = new(0, 10, 0, 0); t.FontFamily = new FontFamily("Consolas"); });
            inner.ValueChanged += (_, _) => { innerText.Text = $"{inner.Value:0.0}%"; Changed(); };
            outer.ValueChanged += (_, _) => { outerText.Text = $"{outer.Value:0.0}%"; Changed(); };
            anti.ValueChanged += (_, _) => { antiText.Text = $"{anti.Value:0.0}%"; Changed(); };
            max.ValueChanged += (_, _) => { maxText.Text = $"{max.Value:0.0}%"; Changed(); };
            square.Checked += (_, _) => Changed(); square.Unchecked += (_, _) => Changed();
            rawBox.Checked += (_, _) => Changed(); rawBox.Unchecked += (_, _) => Changed();
            ratio.ValueChanged += (_, _) => { ratioText.Text = $"{ratio.Value:0}%"; Changed(); };
            var other = channel == CurveChannel.LeftStick ? CurveChannel.RightStick : CurveChannel.LeftStick;
            var copy = Pill(channel == CurveChannel.LeftStick ? "Copy to right stick" : "Copy to left stick"); copy.Height = 34; copy.Padding = new(14, 0, 16, 0);
            copy.Click += (_, _) => CopyStick(channel, other); DockPanel.SetDock(copy, Dock.Right);
            var title = new DockPanel { Children = { copy, Kit.Text(name, 18, "DS.Text", FontWeights.SemiBold) } };
            var tuning = new StackPanel { Children = {
                new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 12, 0, 0), Children = {
                    Label("Inner dead zone").Also(Wide), inner, innerText, Label("Outer dead zone", 22).Also(Wide), outer, outerText } },
                new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 10, 0, 0), Children = {
                    Label("Anti-deadzone").Also(Wide).Also(t => t.ToolTip = "The least the game gets once the stick leaves the dead zone, so small moves register in games with a dead zone of their own"), anti, antiText,
                    Label("Max output", 22).Also(Wide).Also(t => t.ToolTip = "The most the game gets at full push"), max, maxText } },
                new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 10, 0, 0), Children = {
                    square, Kit.Text("Square gate", 14, "DS.TextSoft").Also(t => { t.Margin = new(10, 0, 0, 0); t.VerticalAlignment = VerticalAlignment.Center; t.ToolTip = "Diagonals reach full on both axes, as on a square-gated stick"; }),
                    Label("Up/down speed", 30).Also(t => t.ToolTip = "How fast up and down move compared with left and right"), ratio, ratioText } },
                PresetRow(channel) } };
            rows[channel] = new(inner, outer, anti, max, square, rawBox, ratio, table, tuning);
            stack.Children.Add(new Border { CornerRadius = new(14), Padding = new(20, 16, 20, 16), Margin = new(0, 18, 0, 0), Background = new SolidColorBrush(Color.FromArgb(90, 4, 8, 20)),
                Child = new StackPanel { Children = {
                    title,
                    new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 10, 0, 0), Children = {
                        rawBox, Kit.Text("Pure raw 1:1", 14, "DS.TextSoft").Also(t => { t.Margin = new(10, 0, 0, 0); t.VerticalAlignment = VerticalAlignment.Center; }),
                        Kit.Text("the game gets the stick untouched: no curve, dead zones or anything below", 13, "DS.TextMuted").Also(t => t.Margin = new(12, 0, 0, 0)) } },
                    tuning, table } } });
        }

        var reset = Pill("Reset sticks"); reset.Margin = new(12, 0, 0, 0);
        reset.Click += (_, _) => { loading = true; foreach (var r in rows.Values) { r.Inner.Value = 0; r.Outer.Value = 100; r.Anti.Value = 0; r.Max.Value = 100; r.Square.IsChecked = false; r.Raw.IsChecked = false; r.Ratio.Value = 100; } loading = false; Changed(); };
        measure = Pill("Measure drift"); measure.Margin = new(12, 0, 0, 0); measure.ToolTip = "Leave both sticks alone for 3 seconds; each inner dead zone is set just past how far it wanders";
        measure.Click += async (_, _) => await MeasureDrift();
        Done = Pill("Done"); Done.Click += (_, _) => close();
        stack.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 22, 0, 0), Children = { Done, measure, reset } });
        saved = Wrap("", 13.5, "DS.TextMuted", 10); stack.Children.Add(saved);

        Load();
        Action refresh = () => Dispatcher.BeginInvoke(ShowStatus);
        Loaded += (_, _) => { link.Service.Changed -= refresh; link.Service.Changed += refresh; ShowStatus(); };
        Unloaded += (_, _) => link.Service.Changed -= refresh;
        ShowStatus();
    }

    void Load()
    {
        loading = true;
        var p = link.Store.Get(PcModePage.StoreKey);
        foreach (var (_, channel) in Sticks)
        {
            var r = rows[channel]; var curve = p.Curves.GetValueOrDefault(channel.ToString());
            var t = p.Sticks.GetValueOrDefault(channel.ToString());
            // with no fine settings yet, start from PC mode's whole-percent dead zones so nothing jumps
            r.Inner.Value = t?.Inner ?? curve?.Inner ?? 0; r.Outer.Value = t?.Outer ?? curve?.Outer ?? 100;
            r.Anti.Value = t?.AntiDeadzone ?? 0; r.Max.Value = t?.MaxOutput ?? 100;
            r.Square.IsChecked = t?.SquareGate == true; r.Raw.IsChecked = t?.Raw == true; r.Ratio.Value = t?.AxisRatio ?? 100;
        }
        loading = false; UpdateTables(p);
        foreach (var r in rows.Values) r.Tuning.IsEnabled = r.Raw.IsChecked != true;
    }

    PcModeProfile Build()
    {
        var p = link.Store.Get(PcModePage.StoreKey); p.Sticks = new();
        foreach (var (channel, r) in rows)
        {
            var t = new StickTuning { Inner = Math.Round(r.Inner.Value, 1), Outer = Math.Round(r.Outer.Value, 1), AntiDeadzone = Math.Round(r.Anti.Value, 1), MaxOutput = Math.Round(r.Max.Value, 1),
                SquareGate = r.Square.IsChecked == true, Raw = r.Raw.IsChecked == true, AxisRatio = (int)Math.Round(r.Ratio.Value) };
            // the outer and max sliders start at 60%, the inner and anti ones end at 40%, so these only clash at the seams
            if (t.Inner >= t.Outer) t.Inner = Math.Round(t.Outer - .1, 1); if (t.AntiDeadzone >= t.MaxOutput) t.AntiDeadzone = Math.Round(t.MaxOutput - .1, 1);
            if (!t.IsDefault) p.Sticks[channel.ToString()] = t;
        }
        return p;
    }

    void Changed()
    {
        if (loading) return;
        foreach (var r in rows.Values) r.Tuning.IsEnabled = r.Raw.IsChecked != true;
        var p = Build(); UpdateTables(p);
        if (!p.IsNeutral && link.Preferences is { PcModeNoticeSeen: false })
        {
            pending = true; notice.Visibility = Visibility.Visible; saved.Text = "Read the notice above and press \"I understand\" to start PC mode."; return;
        }
        Save(p);
    }

    void Save(PcModeProfile p)
    {
        try { p.Validate(); link.Store.Save(PcModePage.StoreKey, p); link.Service.Reload(); saved.Text = "Saved. PC mode applies it straight away."; }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException) { saved.Text = "Not saved: " + error.Message; }
    }

    void Accept()
    {
        try { link.Preferences?.MarkPcModeNoticeSeen(); } catch (Exception error) when (error is IOException or InvalidOperationException or UnauthorizedAccessException) { saved.Text = "Couldn't remember this: " + error.Message; }
        notice.Visibility = Visibility.Collapsed;
        if (pending) { pending = false; Save(Build()); }
    }

    void ShowStatus()
    {
        status.Text = link.Service.Enabled
            ? (PcModeSetup.Ready ? "PC mode: " + link.Service.Detail : "PC mode needs its two drivers first: the PC mode tab → Set up PC mode.")
            : "PC mode is off, so these don't apply yet. Turn it on in the PC mode tab.";
        if (PcModePage.ProfileKey != X20Ctl.Zone.PcMode.PcModeStore.DefaultKey)
            status.Text += $" Editing {link.Store.Get(PcModePage.StoreKey).Name ?? "this game"}'s own settings (picked on the PC mode tab).";
    }

    /// <summary>How far the stick is pushed (straight, not diagonal), and what the game gets.</summary>
    void UpdateTables(PcModeProfile p)
    {
        foreach (var (channel, r) in rows)
        {
            var curve = p.Curves.GetValueOrDefault(channel.ToString()); var t = p.Sticks.GetValueOrDefault(channel.ToString());
            string Row(Func<int, string> cell) => string.Concat(TablePoints.Select(v => cell(v).PadLeft(7)));
            r.Table.Text = "push" + Row(v => v + "%") + Environment.NewLine + "game" + Row(v => $"{PadProcessor.Response(curve, t, v / 100.0) * 100:0.#}%")
                + (t is { Raw: true } ? $"{Environment.NewLine}pure raw: the game gets exactly what the stick sends" : "")
                + (t is { Raw: false, AxisRatio: not 100 } ? $"{Environment.NewLine}up and down move at {t.AxisRatio}% of left and right" : "")
                + (t is { Raw: false, SquareGate: true } ? $"{Environment.NewLine}square gate: a full diagonal gives full on both axes" : "");
        }
    }

    WrapPanel PresetRow(CurveChannel channel)
    {
        var row = new WrapPanel { Margin = new(0, 12, 0, 0) };
        foreach (var (name, tip, inner, outer, anti, max) in Presets)
        {
            var b = Pill(name); b.Height = 32; b.Padding = new(12, 0, 14, 0); b.Margin = new(0, 0, 8, 8); b.ToolTip = tip;
            ((TextBlock)b.Content).FontSize = 13;
            b.Click += (_, _) =>
            {
                var r = rows[channel]; loading = true; r.Raw.IsChecked = false;
                if (outer is { } o) r.Outer.Value = o;
                if (inner is { } i) r.Inner.Value = i;
                if (max is { } m) r.Max.Value = m;
                if (anti is { } a) r.Anti.Value = a;
                if (inner == 0 && outer == 100) { r.Square.IsChecked = false; r.Ratio.Value = 100; }
                loading = false; Changed();
            };
            row.Children.Add(b);
        }
        return row;
    }

    void CopyStick(CurveChannel from, CurveChannel to)
    {
        var a = rows[from]; var b = rows[to]; loading = true;
        b.Inner.Value = a.Inner.Value; b.Outer.Value = a.Outer.Value; b.Anti.Value = a.Anti.Value; b.Max.Value = a.Max.Value;
        b.Square.IsChecked = a.Square.IsChecked; b.Raw.IsChecked = a.Raw.IsChecked; b.Ratio.Value = a.Ratio.Value;
        loading = false; Changed();
    }

    /// <summary>Centre guard: three seconds of readings with the sticks left alone. Each stick's inner dead zone goes just
    /// past the farthest it wandered from the centre (plus 0.5%), so drift and jitter never reach the game. Read-only.</summary>
    async Task MeasureDrift()
    {
        measure.IsEnabled = false; saved.Text = "Measuring: leave both sticks alone for 3 seconds...";
        try
        {
            var result = await Task.Run(() =>
            {
                using var reader = new WindowsReader(WindowsReader.Libraries[0]);
                int slot = -1; for (int i = 0; i < 4 && slot < 0; i++) if (!PcModeService.IsVirtual(i) && reader.Read(i, out _)) slot = i;
                if (slot < 0) return ((double Left, double Right)?)null;
                double left = 0, right = 0; var clock = System.Diagnostics.Stopwatch.StartNew();
                while (clock.ElapsedMilliseconds < 3000)
                {
                    if (reader.Read(slot, out var s))
                    {
                        left = Math.Max(left, Math.Sqrt((double)s.Pad.LX * s.Pad.LX + (double)s.Pad.LY * s.Pad.LY) / 32767);
                        right = Math.Max(right, Math.Sqrt((double)s.Pad.RX * s.Pad.RX + (double)s.Pad.RY * s.Pad.RY) / 32767);
                    }
                    Thread.Sleep(2);
                }
                return (left * 100, right * 100);
            });
            if (result is not { } r) { saved.Text = "No controller found to measure. Connect it and try again."; return; }
            if (r.Left > 25 || r.Right > 25) { saved.Text = "A stick moved a lot while measuring. Let go of both sticks and try again."; return; }
            static double Zone(double wander) => Math.Min(StickTuning.MaxInner, Math.Ceiling((wander + .5) * 10) / 10);
            loading = true;
            foreach (var (channel, wander) in new[] { (CurveChannel.LeftStick, r.Left), (CurveChannel.RightStick, r.Right) })
            { var row = rows[channel]; row.Raw.IsChecked = false; row.Inner.Value = Zone(wander); if (row.Outer.Value <= row.Inner.Value) row.Outer.Value = 100; }
            loading = false; Changed();
            saved.Text = $"Left stick wandered up to {r.Left:0.0}%, right up to {r.Right:0.0}%: inner dead zones set to {Zone(r.Left):0.0}% and {Zone(r.Right):0.0}%. " + saved.Text;
        }
        catch (Exception error) when (error is IOException or ArgumentException) { saved.Text = "Couldn't read the controller: " + error.Message; }
        finally { loading = false; measure.IsEnabled = true; }
    }

    static void Wide(TextBlock t) => t.Width = 132;
    static Slider Fine(double min, double max, double value) => new() { Minimum = min, Maximum = max, Value = value, Width = 210, TickFrequency = .1, IsSnapToTickEnabled = true, SmallChange = .1, LargeChange = 1, VerticalAlignment = VerticalAlignment.Center };
    static TextBlock Value(string text) => Kit.Text(text, 14, "DS.TextSoft").Also(t => { t.Width = 62; t.Margin = new(10, 0, 0, 0); t.VerticalAlignment = VerticalAlignment.Center; });
    static TextBlock Label(string text, double left = 0) => Kit.Text(text, 14, "DS.TextMuted").Also(t => { t.Margin = new(left, 0, 10, 0); t.VerticalAlignment = VerticalAlignment.Center; });
    static TextBlock Wrap(string text, double size, string brush, double top) => Kit.Text(text, size, brush).Also(t => { t.TextWrapping = TextWrapping.Wrap; t.TextTrimming = TextTrimming.None; t.Margin = new(0, top, 0, 0); });
    static Button Pill(string text) => new() { Style = DS.Style("DS.ActionSecondary"), Height = 44, MinHeight = 0, Padding = new(22, 0, 24, 0), Content = Kit.Text(text, 15, "DS.Text", FontWeights.SemiBold) };
}
