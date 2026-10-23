using Avalonia.Controls.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Controls.Shapes;
using X20Ctl.Zone;
namespace X20Ctl.Linux;

/// <summary>Sequences for the back paddles: events laid out on a timeline, each holding buttons for a time and then pausing.
/// In PC mode a button you pick plays the macro, because EasySMX paddles send ordinary buttons.</summary>
internal sealed class MacrosPage : UserControl
{
    static readonly string[] EditTabs = ["Buttons", "Sticks", "Timing", "Actions"];
    readonly StudioState state; string slot; int index = -1; string tab = "Buttons"; double zoom = 1;
    public MacrosPage(StudioState state)
    {
        this.state = state; slot = state.Macros.SlotNames.FirstOrDefault() ?? "M1";
        index = state.Macros[slot].Count > 0 ? 0 : -1; Build();
    }

    MacroDraft M => state.Macros;

    /// <summary>Shows what the pad holds, as the Bluetooth client prints it. Read-only; the page's own slots stay as they are.</summary>
    async void ReadPad()
    {
        recordNote = "Reading the macros from the controller…"; Build();
        var r = await PadBridge.Macros();
        recordNote = r.Ok ? (r.Text.Length > 0 ? r.Text : "The controller has no macros.") : r.Text; Build();
    }

    CancellationTokenSource? preview;

    /// <summary>Walks the sequence at its own timing, lighting each event in turn. Nothing is sent anywhere.</summary>
    async void TogglePreview()
    {
        if (preview != null) { preview.Cancel(); return; }
        var events = M[slot]; if (events.Count == 0) return;
        var cts = preview = new CancellationTokenSource(); Build();
        try
        {
            for (int i = 0; i < events.Count && !cts.IsCancellationRequested; i++)
            {
                index = i; Build();
                await Task.Delay(Math.Max(40, events[i].HoldMs + events[i].PauseMs), cts.Token);
            }
        }
        catch (TaskCanceledException) { }
        finally { preview = null; Build(); }
    }

    MacroRecorder? recorder; Avalonia.Threading.DispatcherTimer? watch; string recordNote = "";

    void ToggleRecord()
    {
        if (recorder != null) { FinishRecord(); return; }
        var r = new MacroRecorder();
        if (!r.Start()) { r.Dispose(); recordNote = "No controller found to record from."; Build(); return; }
        recorder = r; recordNote = "Recording. Play the sequence on the controller.";
        watch = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        watch.Tick += (_, _) => { if (recorder == null) return; if (recorder.ShouldStop) FinishRecord(); };
        watch.Start(); Build();
    }

    void FinishRecord()
    {
        var r = recorder; if (r == null) return;
        watch?.Stop(); watch = null; recorder = null; r.Stop();
        var events = r.Events(); r.Dispose();
        if (events.Count == 0) { recordNote = "Nothing was played."; Build(); return; }
        int kept = 0;
        try
        {
            M.Clear(slot);
            foreach (var e in events)
            {
                try { M.Add(slot); M.Update(slot, kept, e with { Id = M[slot][kept].Id }); kept++; }
                catch (InvalidOperationException) { if (M[slot].Count > kept) M.Remove(slot, M[slot].Count - 1); break; }
            }
        }
        catch (Exception e) when (e is InvalidOperationException or ArgumentException) { }
        index = kept > 0 ? 0 : -1;
        recordNote = kept == events.Count ? $"Recorded {kept} events." : $"Recorded {kept} of {events.Count} events; the rest didn't fit.";
        Build();
    }

    void Build()
    {
        var page = new Grid { Margin = new Thickness(36, 4, 36, 14), RowDefinitions = new("Auto,Auto,Auto,Auto") };
        var draft = Parts.Draft(state, () => { index = -1; Build(); }); draft.Margin = new Thickness(0, 0, 0, 10); page.Children.Add(draft);
        var notice = (Phone.Active ? Parts.Notice("Macros · Draft", "Kept on this phone. Macros play through PC mode on a computer; nothing is written to the controller.") : Parts.Notice("Macros · PC mode", "Applies on this PC, to games, for any controller. PC mode can't see a back paddle, so pick the button that plays each slot.")); notice.Margin = new Thickness(0, 0, 0, 12); Grid.SetRow(notice, 1); page.Children.Add(notice);

        var mid = new Grid { ColumnDefinitions = new("360,*"), Margin = new Thickness(0, 0, 0, 14) }; Grid.SetRow(mid, 2);
        mid.Children.Add(Slots());
        var seq = Sequence(); seq.Margin = new Thickness(16, 0, 0, 0); Grid.SetColumn(seq, 1); mid.Children.Add(seq);
        page.Children.Add(mid);
        var edit = Editor(); Grid.SetRow(edit, 3); page.Children.Add(edit);
        Content = new ScrollViewer { Content = page };
    }

    // ---- slots --------------------------------------------------------------------------------------------------

    Control Slots()
    {
        var art = new ControllerArt(state.Model.Id, rear: true, shadow: false) { Height = 150 };
        var tiles = new UniformGrid { Columns = Math.Max(1, M.SlotNames.Length), Margin = new Thickness(0, 6, 0, 0) };
        foreach (string s in M.SlotNames)
        {
            string name = s; bool on = name == slot; int n = M[name].Count;
            var cell = new Border
            {
                CornerRadius = new CornerRadius(12), Margin = new Thickness(2, 0), Padding = new Thickness(0, 8), Cursor = new Cursor(StandardCursorType.Hand),
                Background = on ? T.PrimaryFill : T.B("#4D14213F"), BorderBrush = on ? T.B("#9CCBFF") : T.Hairline, BorderThickness = new Thickness(1),
                Child = T.Stack(1, Parts.Center(T.Label(name, 19, null, FontWeight.SemiBold)), Parts.Center(T.Label(n == 0 ? "Empty" : n + (n == 1 ? " event" : " events"), 11.5, on ? T.Text : T.Soft)))
            };
            cell.OnTap(() => { slot = name; index = M[name].Count > 0 ? 0 : -1; Build(); });
            tiles.Children.Add(cell);
        }
        var played = new Grid { ColumnDefinitions = new("Auto,*"), Margin = new Thickness(0, 12, 0, 0) };
        var lbl = T.Label("In PC mode, played by", 14, T.Soft); lbl.VerticalAlignment = VerticalAlignment.Center; played.Children.Add(lbl);
        var combo = new ComboBox { Margin = new Thickness(12, 0, 0, 0), HorizontalAlignment = HorizontalAlignment.Stretch, Background = T.B("#33FFFFFF"), FontSize = 14, ItemsSource = new[] { "Not used" }.Concat(MacroDraft.TriggerInputs.Select(Names.Of)).ToList(), SelectedItem = M.Trigger(slot) is { } t ? Names.Of(t) : "Not used" };
        combo.SelectionChanged += (_, _) =>
        {
            string? pick = combo.SelectedItem as string; string? key = pick == null || pick == "Not used" ? null : MacroDraft.TriggerInputs.FirstOrDefault(i => Names.Of(i) == pick);
            if ((M.Trigger(slot) ?? null) == key) return;
            try { M.SetTrigger(slot, key); } catch (Exception e) when (e is ArgumentException or InvalidOperationException) { }
        };
        Grid.SetColumn(combo, 1); played.Children.Add(combo);
        return T.Card(T.Stack(6, Parts.Heading("Programmable buttons", "Pick a slot, then the button that plays it"), art, tiles, played), 20);
    }

    // ---- sequence -----------------------------------------------------------------------------------------------

    Control Sequence()
    {
        var events = M[slot];
        var head = new Grid { ColumnDefinitions = new("*,Auto") };
        head.Children.Add(T.Stack(1, T.Label(slot + " · Sequence", 28, null, FontWeight.SemiBold), T.Label($"Events {events.Count} · Capacity {M.EntryCount(slot)} / 47 · {M.Duration(slot)} ms", 13.5, T.Soft)));
        var acts = T.Flow(10, new Pill(recorder == null ? "●  Record" : "■  Stop", Pill.Kinds.Ghost, ToggleRecord) { Enabled = M.CanEdit, Padding = new Thickness(20, 11) }, new Pill("⭳  Read controller", Pill.Kinds.Ghost, ReadPad) { Padding = new Thickness(20, 11) },
            new Pill("+ Add event", Pill.Kinds.Primary, () => { try { M.Add(slot); index = M[slot].Count - 1; } catch (Exception e) when (e is InvalidOperationException or ArgumentException) { } Build(); }) { Padding = new Thickness(22, 11) },
            new Pill(preview == null ? "Preview timeline" : "■  Stop preview", Pill.Kinds.Ghost, TogglePreview) { Enabled = events.Count > 0, Padding = new Thickness(20, 11) },
            new Pill("···", Pill.Kinds.Ghost, () => { M.Clear(slot); index = -1; Build(); }) { Padding = new Thickness(16, 11) });
        acts.VerticalAlignment = VerticalAlignment.Top; Grid.SetColumn(acts, 1); head.Children.Add(acts);

        var chips = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
        if (recordNote.Length > 0) chips.Children.Add(new TextBlock { Text = recordNote, FontSize = 13.5, Foreground = T.Soft, Margin = new Thickness(0, 0, 14, 8), VerticalAlignment = VerticalAlignment.Center });
        for (int i = 0; i < events.Count; i++)
        {
            int at = i; var e = events[i]; bool on = at == index;
            string what = e.Buttons.Count == 0 ? "Wait" : string.Join(" + ", e.Buttons.Select(Names.Of));
            var chip = new Border
            {
                CornerRadius = new CornerRadius(30), Padding = new Thickness(18, 10), Margin = new Thickness(0, 0, 10, 8), Cursor = new Cursor(StandardCursorType.Hand), MinWidth = 210,
                Background = on ? T.B("#CC2A62C8") : T.B("#4D14213F"), BorderBrush = on ? T.B("#CFE3FF") : T.Hairline, BorderThickness = new Thickness(on ? 2 : 1),
                Child = T.Stack(1, T.Line(8, T.Label((at + 1).ToString("00"), 16, null, FontWeight.SemiBold), T.Label(what, 16, null, FontWeight.SemiBold)), T.Label($"{e.HoldMs} ms hold · {e.PauseMs} ms pause", 12.5, on ? T.B("#DCEBFF") : T.Soft))
            };
            chip.OnTap(() => { index = at; Build(); }); chips.Children.Add(chip);
        }
        if (events.Count == 0) chips.Children.Add(T.Label("No events yet. Add one to start " + slot + ".", 14, T.Soft));

        var timeline = new Timeline(events, index, zoom, i => { index = i; Build(); });
        var scroll = new ScrollViewer { Content = timeline, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled, Height = 178 };
        var foot = new Grid { ColumnDefinitions = new("*,Auto"), Margin = new Thickness(0, 8, 0, 0) };
        var tip = T.Label("Pick the button that plays this slot (left), or PC mode won't play it.", 13, T.Soft); tip.VerticalAlignment = VerticalAlignment.Center; foot.Children.Add(tip);
        var zl = T.Label("Timeline zoom", 13, T.Soft); zl.VerticalAlignment = VerticalAlignment.Center;
        var zoomRow = T.Line(10, zl, new Pill("−", Pill.Kinds.Ghost, () => { zoom = Math.Max(.5, zoom - .25); Build(); }) { Padding = new Thickness(13, 6) }, T.Label(Math.Round(zoom * 100) + "%", 13.5, null, FontWeight.SemiBold), new Pill("+", Pill.Kinds.Ghost, () => { zoom = Math.Min(4, zoom + .25); Build(); }) { Padding = new Thickness(13, 6) });
        ((Control)zoomRow.Children[2]).VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(zoomRow, 1); foot.Children.Add(zoomRow);
        return T.Card(T.Stack(0, head, chips, scroll, foot), 20);
    }

    // ---- the event editor ---------------------------------------------------------------------------------------

    Control Editor()
    {
        var events = M[slot];
        var head = new Grid { ColumnDefinitions = new("*,Auto") };
        if (index < 0 || index >= events.Count)
        {
            head.Children.Add(Parts.Heading("No event selected", "Add an event to the sequence, then choose what it presses."));
            return T.Card(head, 20);
        }
        var e = events[index];
        string what = e.Buttons.Count == 0 ? "nothing" : string.Join(" + ", e.Buttons.Select(Names.Of));
        head.Children.Add(Parts.Heading($"Event {index + 1:00} · what it presses", $"{what} · held {e.HoldMs} ms · then {e.PauseMs} ms pause"));
        var seg = Segmented.Make(EditTabs, tab, v => { tab = v; Build(); }); seg.VerticalAlignment = VerticalAlignment.Top; Grid.SetColumn(seg, 1); head.Children.Add(seg);

        Control body = tab switch { "Sticks" => Sticks(e), "Timing" => Timing(e), "Actions" => Actions(e), _ => Buttons(e) };
        body.Margin = new Thickness(0, 12, 0, 0);
        return T.Card(T.Stack(0, head, body), 20);
    }

    void Set(MacroEvent next) { try { M.Update(slot, index, next); } catch (Exception e) when (e is ArgumentException or InvalidOperationException) { } Build(); }

    Control Buttons(MacroEvent e)
    {
        Control Toggle(string key, Func<bool, Control> make)
        {
            bool on = e.Buttons.Contains(key); var c = make(on); c.Cursor = new Cursor(StandardCursorType.Hand);
            c.OnTap(() => Set(e with { Buttons = on ? e.Buttons.Where(b => b != key).ToList() : [.. e.Buttons, key] }));
            return c;
        }
        Control Group(string title, Control body) => T.Stack(12, Parts.Center(Parts.Caps(title)), Parts.Center(body));

        var face = new Canvas { Width = 150, Height = 124 };
        void At(Control c, double x, double y) { Canvas.SetLeft(c, x); Canvas.SetTop(c, y); face.Children.Add(c); }
        foreach (var (k, x, y) in new[] { ("Y", 52.0, 0.0), ("X", 6.0, 40.0), ("B", 98.0, 40.0), ("A", 52.0, 80.0) }) { string key = k; At(Toggle(key, on => Parts.Cap(key, 44, Names.FaceColour(key), on, font: 16)), x, y); }
        var shoulders = new Grid { RowDefinitions = new("Auto,Auto"), ColumnDefinitions = new("Auto,Auto") };
        foreach (var (k, r, c) in new[] { ("LB", 0, 0), ("RB", 0, 1), ("LT", 1, 0), ("RT", 1, 1) }) { string key = k; var cell = Toggle(key, on => Parts.Key(key, 98, 46, on)); cell.Margin = new Thickness(c == 0 ? 0 : 8, r == 0 ? 0 : 8, 0, 0); Grid.SetRow(cell, r); Grid.SetColumn(cell, c); shoulders.Children.Add(cell); }
        var dpad = new Grid { Width = 130, Height = 130, RowDefinitions = new("*,*,*"), ColumnDefinitions = new("*,*,*") };
        foreach (var (k, r, c) in new[] { ("DPAD_UP", 0, 1), ("DPAD_LEFT", 1, 0), ("DPAD_RIGHT", 1, 2), ("DPAD_DOWN", 2, 1) }) { string key = k; var cell = Toggle(key, on => Parts.Key(Names.Of(key), 40, 40, on, 17)); cell.HorizontalAlignment = HorizontalAlignment.Center; cell.VerticalAlignment = VerticalAlignment.Center; Grid.SetRow(cell, r); Grid.SetColumn(cell, c); dpad.Children.Add(cell); }
        var sys = new Grid { RowDefinitions = new("Auto,Auto"), ColumnDefinitions = new("Auto,Auto") };
        foreach (var (k, r, c) in new[] { ("L3", 0, 0), ("R3", 0, 1), ("SELECT", 1, 0), ("START", 1, 1) }) { string key = k; var cell = Toggle(key, on => Parts.Key(Names.Of(key), 98, 46, on)); cell.Margin = new Thickness(c == 0 ? 0 : 8, r == 0 ? 0 : 8, 0, 0); Grid.SetRow(cell, r); Grid.SetColumn(cell, c); sys.Children.Add(cell); }
        var guide = Parts.Key("Guide", 98, 46); guide.Opacity = .4;

        var grid = new Grid { ColumnDefinitions = new("*,*,*,*,*") };
        var groups = new[] { Group("FACE", face), Group("SHOULDERS", shoulders), Group("D-PAD", dpad), Group("STICKS / SYSTEM", sys), Group("SPECIAL", guide) };
        for (int i = 0; i < groups.Length; i++) { Grid.SetColumn(groups[i], i); grid.Children.Add(groups[i]); }
        return grid;
    }

    Control Sticks(MacroEvent e)
    {
        Control Pad(string title, StickHeading current, Func<StickHeading, MacroEvent> make)
        {
            var g = new Grid { RowDefinitions = new("Auto,Auto,Auto"), ColumnDefinitions = new("Auto,Auto,Auto") };
            var cells = new (StickHeading H, string Glyph, int R, int C)[] { (StickHeading.UpLeft, "↖", 0, 0), (StickHeading.Up, "↑", 0, 1), (StickHeading.UpRight, "↗", 0, 2), (StickHeading.Left, "←", 1, 0), (StickHeading.Neutral, "●", 1, 1), (StickHeading.Right, "→", 1, 2), (StickHeading.DownLeft, "↙", 2, 0), (StickHeading.Down, "↓", 2, 1), (StickHeading.DownRight, "↘", 2, 2) };
            foreach (var (h, glyph, r, c) in cells)
            {
                var heading = h; var key = Parts.Key(glyph, 52, 46, current == heading, 18); key.Margin = new Thickness(3); key.Cursor = new Cursor(StandardCursorType.Hand); key.OnTap(() => Set(make(heading)));
                Grid.SetRow(key, r); Grid.SetColumn(key, c); g.Children.Add(key);
            }
            return T.Stack(8, Parts.Center(Parts.Caps(title)), Parts.Center(g));
        }
        var row = T.Line(70, Pad("LEFT STICK", e.Left, h => e with { Left = h }), Pad("RIGHT STICK", e.Right, h => e with { Right = h })); row.HorizontalAlignment = HorizontalAlignment.Center; return row;
    }

    Control Timing(MacroEvent e)
    {
        Control Row(string label, int value, int min, int max, Func<int, MacroEvent> make)
        {
            var text = T.Label($"{label}   {value} ms", 15); var s = new FlatSlider { Minimum = min, Maximum = max, Value = value, Width = 460 };
            s.ValueChanged += v => text.Text = $"{label}   {(int)v} ms"; s.PointerReleased += (_, _) => Set(make((int)s.Value));
            return T.Stack(4, text, s);
        }
        return T.Stack(14, Row("Hold", e.HoldMs, 10, 1000, v => e with { HoldMs = v }), Row("Pause after", e.PauseMs, 0, 1000, v => e with { PauseMs = v }));
    }

    Control Actions(MacroEvent e)
    {
        var row = T.Line(12, new Pill("Duplicate", Pill.Kinds.Ghost, () => { M.Duplicate(slot, index); index++; Build(); }) { Padding = new Thickness(22, 11) },
            new Pill("Move earlier", Pill.Kinds.Ghost, () => { if (index > 0) { M.Move(slot, index, index - 1); index--; Build(); } }) { Padding = new Thickness(22, 11), Enabled = index > 0 },
            new Pill("Move later", Pill.Kinds.Ghost, () => { if (index < M[slot].Count - 1) { M.Move(slot, index, index + 1); index++; Build(); } }) { Padding = new Thickness(22, 11), Enabled = index < M[slot].Count - 1 },
            new Pill("Remove", Pill.Kinds.Ghost, () => { M.Remove(slot, index); index = Math.Min(index, M[slot].Count - 1); Build(); }) { Padding = new Thickness(22, 11) });
        row.HorizontalAlignment = HorizontalAlignment.Center; return row;
    }
}

/// <summary>The macro laid out in time: a lane for buttons, one per stick, and one for the pauses between.</summary>
internal sealed class Timeline : Control
{
    readonly IReadOnlyList<MacroEvent> events; readonly int selected; readonly double zoom; readonly Action<int> pick;
    const double Lane = 32, Head = 30, Label = 118, PerMs = .62;
    public Timeline(IReadOnlyList<MacroEvent> events, int selected, double zoom, Action<int> pick)
    {
        this.events = events; this.selected = selected; this.zoom = zoom; this.pick = pick; Height = Head + Lane * 4 + 4;
        Width = Math.Max(760, Label + (events.Sum(e => e.HoldMs + e.PauseMs) + 120) * PerMs * zoom);
    }
    double X(double ms) => Label + ms * PerMs * zoom;

    public override void Render(DrawingContext ctx)
    {
        ctx.DrawRectangle(T.B("#4D060B18"), new Pen(T.B("#14FFFFFF"), 1), new RoundedRect(new Rect(0, 0, Width, Height), 10));
        string[] lanes = ["Buttons", "Left stick", "Right stick", "Pause"];
        for (int i = 0; i < 4; i++)
        {
            double y = Head + i * Lane; ctx.DrawLine(new Pen(T.B("#14FFFFFF"), 1), new Point(Label - 8, y), new Point(Width, y));
            var pill = new Rect(8, y + 4, Label - 24, Lane - 8); ctx.DrawRectangle(T.B("#33FFFFFF"), null, new RoundedRect(pill, 13));
            ctx.DrawEllipse(i == 3 ? T.Muted : T.AccentHi, null, new Point(22, y + Lane / 2), 3.5, 3.5);
            Text(ctx, lanes[i], new Point(34, y + 8), 12.5, T.Text);
        }
        int total = events.Sum(e => e.HoldMs + e.PauseMs), step = zoom >= 2 ? 25 : zoom >= 1 ? 65 : 130;
        for (int ms = 0; ms <= total + step; ms += step) { Text(ctx, ms + " ms", new Point(X(ms) + 2, 6), 12, T.Soft); ctx.DrawLine(new Pen(T.B("#10FFFFFF"), 1), new Point(X(ms), Head - 4), new Point(X(ms), Height)); }
        double t = 0;
        for (int i = 0; i < events.Count; i++)
        {
            var e = events[i]; bool on = i == selected;
            var hold = new Rect(X(t), Head + 4, e.HoldMs * PerMs * zoom, Lane - 8); string name = e.Buttons.Count == 0 ? "" : string.Join(" + ", e.Buttons.Select(Names.Of));
            if (e.Buttons.Count > 0) Block(ctx, hold, name, on, true);
            if (e.Left != StickHeading.Neutral) Block(ctx, new Rect(X(t), Head + Lane + 4, e.HoldMs * PerMs * zoom, Lane - 8), Arrow(e.Left), on, true);
            if (e.Right != StickHeading.Neutral) Block(ctx, new Rect(X(t), Head + Lane * 2 + 4, e.HoldMs * PerMs * zoom, Lane - 8), Arrow(e.Right), on, true);
            if (e.PauseMs > 0) Block(ctx, new Rect(X(t + e.HoldMs), Head + Lane * 3 + 4, e.PauseMs * PerMs * zoom, Lane - 8), e.PauseMs + " ms", on, false);
            t += e.HoldMs + e.PauseMs;
        }
    }

    static string Arrow(StickHeading h) => h switch { StickHeading.Up => "↑", StickHeading.Down => "↓", StickHeading.Left => "←", StickHeading.Right => "→", StickHeading.UpLeft => "↖", StickHeading.UpRight => "↗", StickHeading.DownLeft => "↙", StickHeading.DownRight => "↘", _ => "" };
    static void Block(DrawingContext ctx, Rect r, string label, bool on, bool blue)
    {
        r = r.WithWidth(Math.Max(r.Width, 22));
        ctx.DrawRectangle(blue ? T.B("#CC2A62C8") : T.B("#66556A94"), on ? new Pen(T.B("#CFE3FF"), 2) : null, new RoundedRect(r, 11));
        Text(ctx, label, new Point(r.X + 10, r.Y + 5), 12.5, T.Text);
    }
    static void Text(DrawingContext ctx, string s, Point at, double size, IBrush brush)
        => ctx.DrawText(new FormattedText(s, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(T.Font), size, brush), at);

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        var p = e.GetPosition(this); double t = 0;
        for (int i = 0; i < events.Count; i++) { double x0 = X(t), x1 = X(t + events[i].HoldMs + events[i].PauseMs); if (p.X >= x0 && p.X <= x1 && p.Y > Head) { pick(i); return; } t += events[i].HoldMs + events[i].PauseMs; }
    }
}
