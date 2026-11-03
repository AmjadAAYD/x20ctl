using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using X20Ctl.Product.Rebel;
using X20Ctl.Zone;
namespace X20Ctl.Product;

/// <summary>The Rebel Macros page (owner board macros.png): the sequence is a comic strip, one panel per event read left to right, over a
/// lane timeline, with the event's editor on torn paper. Every edit goes through the Macros workbench and its draft.</summary>
public partial class StudioScene
{
    private Grid? rebelMacros; private bool rebelMacrosWired; private string rebelMacroTab = "buttons"; private string? rebelMacroNote;
    private readonly Stopwatch rebelPlayClock = new(); private DispatcherTimer? rebelPlayTimer; private Line? rebelPlayhead; private double rebelPlayScale, rebelPlayShift;
    private static readonly double[] RebelPanelTilt = [-1.6, 1.2, -1, 1.8];

    private static string RebelInputGlyph(string key) => key switch { "DPAD_UP" => "↑", "DPAD_DOWN" => "↓", "DPAD_LEFT" => "←", "DPAD_RIGHT" => "→", "SELECT" => "View", "START" => "Menu", "HOME" => "Home", _ => key };
    private static string RebelTriggerName(string key) => key switch
    {
        "DPAD_UP" => "D-pad up", "DPAD_DOWN" => "D-pad down", "DPAD_LEFT" => "D-pad left", "DPAD_RIGHT" => "D-pad right", "SELECT" => "View", "START" => "Menu", "HOME" => "Home",
        "L3" => "Left stick click", "R3" => "Right stick click", "LB" => "Left bumper", "RB" => "Right bumper", "LT" => "Left trigger", "RT" => "Right trigger", _ => key
    };
    private static string RebelArrow(StickHeading h) => h switch
    {
        StickHeading.Up => "↑", StickHeading.UpRight => "↗", StickHeading.Right => "→", StickHeading.DownRight => "↘", StickHeading.Down => "↓", StickHeading.DownLeft => "↙", StickHeading.Left => "←", StickHeading.UpLeft => "↖", _ => "·"
    };
    private static string RebelPresses(MacroEvent e) => string.Join(" + ", e.Buttons.Select(RebelInputGlyph)
        .Concat(e.Left == StickHeading.Neutral ? [] : ["L " + RebelArrow(e.Left)]).Concat(e.Right == StickHeading.Neutral ? [] : ["R " + RebelArrow(e.Right)])) is { Length: > 0 } t ? t : "Neutral";

    /// <summary>A skewed button (.bt): black with a white edge, white over a red offset when focused. On paper it is ink on paper.</summary>
    private static Button RebelBt(string text, string? glyph, Action click, string tag, double size = 15, double h = 42, bool paper = false, bool on = false, bool enabled = true)
    {
        var label = Ink.Text(text, size, null, !paper, paper ? FontWeights.SemiBold : null); label.RenderTransform = null;
        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        TextBlock? icon = glyph == null ? null : Glyphs.Make(glyph, size - 1, Brushes.White); if (icon != null) { icon.Margin = new(0, 0, 10, 0); row.Children.Add(icon); }
        row.Children.Add(label);
        var face = new Border { Height = h, Padding = new(paper ? 14 : 18, 0, paper ? 14 : 18, 0), Child = row, RenderTransformOrigin = new(.5, .5), RenderTransform = new SkewTransform(-12, 0) };
        var b = new Button { Content = face, Tag = tag, FocusVisualStyle = null, Cursor = Cursors.Hand, IsEnabled = enabled, Margin = new(0, 0, paper ? 8 : 12, 0), Template = RebelBare() };
        void Paint()
        {
            bool f = b.IsKeyboardFocused || b.IsMouseOver; Color ink, back, edge;
            if (paper) { back = on ? Ink.Red : f ? Ink.Black : Colors.Transparent; ink = on || f ? Colors.White : Ink.C("#151316"); edge = on ? Colors.Transparent : f ? Ink.Black : Ink.A(Ink.Black, .5); }
            else { back = f ? Ink.White : Ink.Black; ink = f ? Ink.Black : Ink.White; edge = f ? Colors.Transparent : Ink.White; }
            face.Background = Ink.B(back); face.BorderBrush = Ink.B(edge); face.BorderThickness = new(paper ? 1.4 : 2); label.Foreground = Ink.B(ink); if (icon != null) icon.Foreground = Ink.B(ink);
            face.Effect = f ? Ink.Shadow(paper ? 5 : 6, paper ? 5 : 5, 0, Ink.Red, 1) : null; b.Opacity = b.IsEnabled ? 1 : .45;
        }
        b.GotKeyboardFocus += (_, _) => Paint(); b.LostKeyboardFocus += (_, _) => Paint(); b.MouseEnter += (_, _) => Paint(); b.MouseLeave += (_, _) => Paint(); b.IsEnabledChanged += (_, _) => Paint();
        b.Click += (_, _) => click(); Paint(); return b;
    }
    private static ControlTemplate RebelBare() { var t = new ControlTemplate(typeof(Button)) { VisualTree = new FrameworkElementFactory(typeof(ContentPresenter)) }; t.Seal(); return t; }

    private void RebelMacrosPage()
    {
        var m = Macros; if (m == null) return;
        if (!rebelMacrosWired) { rebelMacrosWired = true; m.Changed += () => { rebelMacroNote = null; if (rebelPage == "macros") RebelLater(); }; }
        var g = rebelMacros!; string? focused = (Keyboard.FocusedElement as FrameworkElement) is { Tag: string t } fe && g.IsAncestorOf(fe) ? t : null;
        g.Children.Clear();
        var d = m.Draft; string slot = m.Slot; var events = d[slot]; int sel = m.SelectedIndex;

        // the slots, top right: M1 to M4 as tabs, how full each one is under it
        var names = d.SlotNames; double x0 = 1548 - names.Length * 92;
        for (int i = 0; i < names.Length; i++)
        {
            string s = names[i]; var chip = new ChipTab(d.PhysicalSlots.Count == 0 ? s.Replace("M", "PC ") : s, 15) { On = s == slot, Tag = "slot:" + s, Width = 80 };
            ((Border)chip.Content).HorizontalAlignment = HorizontalAlignment.Stretch; ((TextBlock)((Border)chip.Content).Child).HorizontalAlignment = HorizontalAlignment.Center; chip.Click += (_, _) => m.SelectSlot(s);
            g.Children.Add(Place(chip, x0 + i * 92, 34));
            int n = d[s].Count; string sub = (n == 0 ? "Empty" : $"{n} event{(n == 1 ? "" : "s")}") + (d.PcMode && d.Trigger(s) is { } key ? " · " + RebelInputGlyph(key) : "");
            var subText = Ink.Text(sub, 12, Ink.B(Ink.C("#9d9598")), false); subText.Width = 92; subText.TextAlignment = TextAlignment.Center; g.Children.Add(Place(subText, x0 + i * 92 - 6, 82));
        }
        // the sequence's name and size
        var name = new Border { Background = Ink.B(Ink.White), Padding = new(12, 3, 12, 4), Child = Ink.Text($"{slot} · Sequence", 15, Ink.B(Ink.Black)) };
        var size = Ink.Text($"Events {events.Count} · Capacity {d.EntryCount(slot)} / 47 · {d.Duration(slot)} ms", 13.5, Ink.B(Ink.C("#9d9598")), false); size.Margin = new(12, 0, 0, 0); size.VerticalAlignment = VerticalAlignment.Center;
        g.Children.Add(Place(new StackPanel { Orientation = Orientation.Horizontal, Children = { name, size } }, 268, 182));

        // the strip: three panels in view around the chosen one, then the empty panel that adds one
        int first = Math.Clamp(sel - 1, 0, Math.Max(0, events.Count - 3)), shown = Math.Min(3, events.Count - first);
        for (int k = 0; k < shown; k++) { int i = first + k; g.Children.Add(Place(RebelPanel(i, events[i], i == sel), 268 + k * 322, 222)); }
        if (shown < 4)
        {
            var add = new Button { Tag = "ev:add", FocusVisualStyle = null, Cursor = Cursors.Hand, Template = RebelBare(), IsEnabled = d.CanEdit && d.EntryCount(slot) < 47, Width = 290, Height = 200 };
            var slab = new Slab { Seed = 150, Fill = Ink.B(Ink.C("#0e0d0f")), Line = Ink.B(Ink.A(Ink.White, .4)), LineWidth = 2, Dash = new DoubleCollection { 5, 4 }, Rot = RebelPanelTilt[3] };
            var addText = Ink.Text("+ Add event", 20, Ink.B(Ink.C("#c9c1c3"))); addText.HorizontalAlignment = HorizontalAlignment.Center; addText.VerticalAlignment = VerticalAlignment.Center; slab.Children.Add(addText); add.Content = slab;
            void Lit() { bool f = add.IsKeyboardFocused || add.IsMouseOver; slab.Line = Ink.B(f ? Ink.White : Ink.A(Ink.White, .4)); addText.Foreground = Ink.B(f ? Ink.White : Ink.C("#c9c1c3")); slab.InvalidateVisual(); }
            add.GotKeyboardFocus += (_, _) => Lit(); add.LostKeyboardFocus += (_, _) => Lit(); add.MouseEnter += (_, _) => Lit(); add.MouseLeave += (_, _) => Lit(); add.Opacity = add.IsEnabled ? 1 : .5;
            add.Click += (_, _) => m.Add(); g.Children.Add(Place(add, 268 + shown * 322, 222));
        }
        if (events.Count > 3) { var more = Ink.Text($"{first + 1}–{first + shown} of {events.Count}", 13.5, Ink.B(Ink.C("#9d9598")), false); g.Children.Add(Place(more, 1100, 186)); }

        g.Children.Add(Place(RebelTimeline(events, sel), 262, 458));

        // who plays it: in PC mode a real button (the PC can't see a paddle), otherwise the paddle itself
        var played = new RowSlab(0, 56) { Width = 640, Tag = "mac:trigger", IsEnabled = d.PcMode && d.CanEdit };
        played.Left.Children.Add(played.Tint(Ink.Text(d.PcMode ? "In PC mode, played by" : "Played by", 19), Ink.White, Ink.Black));
        string by = d.PcMode ? d.Trigger(slot) is { } b ? RebelTriggerName(b) : "Not used" : d.PhysicalSlots.Contains(slot) ? $"The {slot} paddle" : "Not used";
        var dd = new Border { Height = 34, Padding = new(14, 0, 14, 0), Background = Ink.B(Ink.Black), BorderBrush = Ink.B(Ink.A(Ink.White, .55)), BorderThickness = new(1.5), RenderTransform = new SkewTransform(-10, 0),
            Child = new StackPanel { Orientation = Orientation.Horizontal, Children = { Ink.Text(by, 15, Ink.B(Ink.White), false, FontWeights.SemiBold), d.PcMode ? Glyphs.Make("", 10, Ink.B(Ink.Hot)).Also(i => i.Margin = new(12, 0, 0, 0)) : new TextBlock() } } };
        played.Right.Children.Add(dd); played.Click += (_, _) => RebelCycleTrigger(1);
        played.GotKeyboardFocus += (_, _) => played.On = true; played.LostKeyboardFocus += (_, _) => played.On = false;
        g.Children.Add(Place(played, 262, 624));
        var tools = new StackPanel { Orientation = Orientation.Horizontal };
        tools.Children.Add(RebelBt(m.IsRecording ? "Stop" : "Record", "", () => { m.ToggleRecording(); RebelLater(); }, "mac:record", enabled: d.CanEdit));
        tools.Children.Add(RebelBt("Read controller", "", () => { _ = m.ReadController(text => { rebelMacroNote = text; RebelLater(); }); }, "mac:read"));
        tools.Children.Add(RebelBt(m.IsPreviewing ? "Stop preview" : "Preview timeline", "", RebelMacroPreview, "mac:preview", enabled: d.Duration(slot) > 0));
        g.Children.Add(Place(tools, 264, 696));

        g.Children.Add(Place(RebelMacroEditor(m, events, sel), 944, 458));

        // the status, bottom left
        string note = rebelMacroNote ?? (m.IsRecording ? "Recording · press the buttons on your controller; it stops after a 2.5 s rest."
            : d.EntryCount(slot) >= 47 ? "Maximum sequence capacity reached."
            : d.PcMode ? d.Trigger(slot) is { } p ? $"PC mode plays this when you press {RebelTriggerName(p)}, instead of what {RebelInputGlyph(p)} usually does." : events.Count > 0 ? "Pick the button that plays this slot (left), or PC mode won't play it." : "PC mode · Add events, then pick the button that plays them."
            : d.CanEdit ? "Local draft · Hold and pause timing are a sequence preview." : "Macros for this controller are a preview until its settings are verified. Nothing is stored or sent.");
        var noteRow = new StackPanel { Orientation = Orientation.Horizontal, Children = { new Canvas { Width = 16, Height = 18, Children = { Star.Make(8, 9, 6) } }, Ink.Text(note, 14, Ink.B(Ink.C("#9d9598")), false).Also(t => { t.Margin = new(10, 0, 0, 0); t.MaxWidth = 540; t.TextTrimming = TextTrimming.CharacterEllipsis; }) } };
        g.Children.Add(Place(noteRow, 262, 846));

        string want = focused ?? (sel >= 0 ? "ev:" + sel : "ev:add");
        if (focused != null && FindTagged(g, focused) == null) want = sel >= 0 ? "ev:" + sel : "ev:add";
        Dispatcher.BeginInvoke(() => (FindTagged(g, want) ?? FindTagged(g, "slot:" + slot))?.Focus(), DispatcherPriority.Input);
    }

    /// <summary>One comic panel per event: its number, what it presses in a disc, its timing in a caption.</summary>
    private Button RebelPanel(int index, MacroEvent e, bool on)
    {
        double w = 300, h = 200; var face = new Grid { Width = w, Height = h, ClipToBounds = true, Background = Ink.B(on ? Ink.Red : Ink.White) };
        var dots = new Rectangle { Fill = Dots.Tile(Ink.A(Ink.Black, on ? .22 : .13), 9, 1.7) };
        var mask = new RadialGradientBrush { RadiusX = .7, RadiusY = .7 }; mask.GradientStops.Add(new(Colors.Transparent, .3)); mask.GradientStops.Add(new(Colors.Black, 1)); dots.OpacityMask = mask; face.Children.Add(dots);
        var num = new Border { Background = Ink.B(on ? Ink.Black : Ink.White), BorderBrush = Ink.B(Ink.Black), BorderThickness = new(2), Padding = new(10, 2, 10, 3), Child = Ink.Text($"{index + 1:00}", 19, Ink.B(on ? Ink.White : Ink.Black)) };
        RPut(num, 12, 12); face.Children.Add(num);
        string presses = e.Buttons.Count > 0 ? string.Join("+", e.Buttons.Select(RebelInputGlyph)) : e.Left != StickHeading.Neutral ? "L" + RebelArrow(e.Left) : e.Right != StickHeading.Neutral ? "R" + RebelArrow(e.Right) : "·";
        var disc = new Grid { Width = 88, Height = 88 };
        disc.Children.Add(new Ellipse { Fill = Ink.B(Ink.Black), Margin = new(6, 6, -6, -6) }); disc.Children.Add(new Ellipse { Fill = Ink.B(on ? Ink.White : Ink.Black) });
        var letter = new TextBlock { Text = presses, FontFamily = Ink.Heavy, FontSize = presses.Length <= 2 ? 48 : presses.Length <= 4 ? 30 : 18, Foreground = Ink.B(on ? Ink.Red : Ink.White), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 80 };
        disc.Children.Add(letter); RPut(disc, w / 2 - 44, h / 2 - 56); face.Children.Add(disc);
        var cap = new Border { Background = Ink.B(Ink.White), BorderBrush = Ink.B(Ink.Black), BorderThickness = new(2), Padding = new(10, 4, 10, 5), Margin = new(12, 0, 12, 12), VerticalAlignment = VerticalAlignment.Bottom,
            Child = Ink.Text($"{e.HoldMs} ms hold · {e.PauseMs} ms pause", 13, Ink.B(Ink.Black), false, FontWeights.SemiBold).Also(t => t.HorizontalAlignment = HorizontalAlignment.Center) };
        face.Children.Add(cap);
        face.Children.Add(new Border { BorderBrush = Ink.B(Ink.Black), BorderThickness = new(5), IsHitTestVisible = false });
        var frame = new Border { Child = face, BorderThickness = new(3), BorderBrush = Ink.B(on ? Ink.White : Ink.Black), Effect = on ? Ink.Shadow(10, 10, 0, Ink.Red, 1) : null };
        var b = new Button { Content = frame, Tag = "ev:" + index, FocusVisualStyle = null, Cursor = Cursors.Hand, Template = RebelBare(), RenderTransformOrigin = new(.5, .5), RenderTransform = new RotateTransform(RebelPanelTilt[index % 3]), Margin = new(-3) };
        b.GotKeyboardFocus += (_, _) => { if (Macros != null && Macros.SelectedIndex != index) { Macros.Select(index); RebelLater(); } };
        b.Click += (_, _) => { Macros?.Select(index); RebelLater(); };
        b.MouseEnter += (_, _) => frame.BorderBrush = Ink.B(Ink.White); b.MouseLeave += (_, _) => frame.BorderBrush = Ink.B(on ? Ink.White : Ink.Black);
        return b;
    }

    /// <summary>The lanes, as the board draws them: buttons, sticks and pauses against time, the chosen event outlined.</summary>
    private FrameworkElement RebelTimeline(IReadOnlyList<MacroEvent> events, int sel)
    {
        double w = 640, h = 150, lane = 130, tw = w - lane, spacing = (tw - 40) / 3.2;
        int duration = events.Sum(e => e.HoldMs + e.PauseMs); double interval = Math.Max(5, Math.Ceiling(Math.Max(duration, 60) * 1.08 / 3 / 5) * 5), scale = spacing / interval;
        double start0 = lane + 22, selStart = events.Take(Math.Max(0, sel)).Sum(e => e.HoldMs + e.PauseMs), selEnd = sel >= 0 ? selStart + events[sel].HoldMs + events[sel].PauseMs : 0;
        double shift = Math.Max(0, start0 + selEnd * scale + 50 - w); rebelPlayScale = scale; rebelPlayShift = shift;
        var c = new Canvas { Width = w, Height = h, ClipToBounds = true, Background = Ink.B(Ink.C("#131114")) };
        var plot = new Canvas { RenderTransform = new TranslateTransform(-shift, 0) }; c.Children.Add(plot);
        for (int i = 0; ; i++)
        {
            double x = lane + 16 + i * spacing; if (x - shift > w) break;
            plot.Children.Add(new Rectangle { Width = 1, Height = h, Fill = Ink.B(Ink.A(Ink.White, .12)) }.Also(r => Canvas.SetLeft(r, x)));
            plot.Children.Add(Ink.Text($"{i * interval:0} ms", 13, Ink.B(Ink.C("#9d9598")), false).Also(t => { Canvas.SetLeft(t, x + 6); Canvas.SetTop(t, 6); }));
        }
        double at = 0;
        for (int i = 0; i < events.Count; i++)
        {
            var e = events[i]; double x = start0 + at * scale, bw = Math.Max(6, e.HoldMs * scale); bool on = i == sel;
            int lanes = 0;
            foreach (var (row, present, text) in new[] { (0, e.Buttons.Count > 0 || e.Left == StickHeading.Neutral && e.Right == StickHeading.Neutral, e.Buttons.Count > 0 ? string.Join("+", e.Buttons.Select(RebelInputGlyph)) : "Neutral"),
                                                        (1, e.Left != StickHeading.Neutral, "L " + RebelArrow(e.Left)), (2, e.Right != StickHeading.Neutral, "R " + RebelArrow(e.Right)) })
            {
                if (!present) continue; lanes++;
                var bar = new Rectangle { Width = bw, Height = 14, RadiusX = 7, RadiusY = 7, Fill = on ? Brushes.Transparent : Ink.B(Ink.Red), Stroke = on ? Ink.B(Ink.Red) : null, StrokeThickness = 2.5 };
                Canvas.SetLeft(bar, x); Canvas.SetTop(bar, 44 + row * 27); plot.Children.Add(bar);
                if (row == 0) plot.Children.Add(Ink.Text(text, 15, Ink.B(Ink.White)).Also(t => { Canvas.SetLeft(t, x); Canvas.SetTop(t, 23); t.MaxWidth = Math.Max(bw + 20, 30); t.TextTrimming = TextTrimming.CharacterEllipsis; }));
            }
            if (e.PauseMs > 0)
            {
                var chip = new Border { CornerRadius = new(9), Background = Ink.B(Ink.C("#2a2629")), Padding = new(9, 1, 9, 2), Child = Ink.Text($"{e.PauseMs} ms", 12, Ink.B(Ink.White), false, FontWeights.SemiBold) };
                Canvas.SetLeft(chip, x + bw + 4); Canvas.SetTop(chip, h - 32); plot.Children.Add(chip);
            }
            at += e.HoldMs + e.PauseMs;
        }
        rebelPlayhead = new Line { Y1 = 0, Y2 = h, Stroke = Ink.B(Ink.White), StrokeThickness = 2, Visibility = Macros?.IsPreviewing == true ? Visibility.Visible : Visibility.Collapsed };
        plot.Children.Add(rebelPlayhead); RebelPlayheadMove();
        // the lane names sit on their own dark column
        c.Children.Add(new Rectangle { Width = lane, Height = h, Fill = Ink.B(Ink.C("#131114")) });
        c.Children.Add(new Rectangle { Width = 1, Height = h, Fill = Ink.B(Ink.A(Ink.White, .12)) }.Also(r => Canvas.SetLeft(r, lane)));
        string[] names = ["Buttons", "Left stick", "Right stick", "Pause"];
        for (int i = 0; i < 4; i++)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Children = { new Ellipse { Width = 8, Height = 8, Fill = Ink.B(Ink.Red), Margin = new(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center }, Ink.Text(names[i], 13.5, Ink.B(Ink.C("#9d9598")), false) } };
            Canvas.SetLeft(row, 14); Canvas.SetTop(row, 37 + i * 27); c.Children.Add(row);
        }
        return c;
    }
    private void RebelPlayheadMove()
    {
        if (rebelPlayhead == null) return; double x = 130 + 22 + rebelPlayClock.ElapsedMilliseconds * rebelPlayScale; rebelPlayhead.X1 = rebelPlayhead.X2 = x;
    }
    /// <summary>Plays the timeline on screen only (the workbench's preview): the playhead runs along the lanes.</summary>
    private void RebelMacroPreview()
    {
        var m = Macros; if (m == null) return; m.Preview();
        if (!m.IsPreviewing) { rebelPlayTimer?.Stop(); rebelPlayClock.Reset(); RebelLater(); return; }
        rebelPlayClock.Restart(); rebelPlayTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        rebelPlayTimer.Tick -= RebelPlayTick; rebelPlayTimer.Tick += RebelPlayTick; rebelPlayTimer.Start(); RebelLater();
    }
    private void RebelPlayTick(object? sender, EventArgs e)
    {
        if (Macros?.IsPreviewing != true || rebelPage != "macros") { rebelPlayTimer?.Stop(); rebelPlayClock.Reset(); if (rebelPlayhead != null) rebelPlayhead.Visibility = Visibility.Collapsed; RebelLater(); return; }
        if (rebelPlayhead != null) rebelPlayhead.Visibility = Visibility.Visible; RebelPlayheadMove();
    }
    private void RebelCycleTrigger(int step)
    {
        var m = Macros; if (m == null || !m.Draft.PcMode || !m.Draft.CanEdit) return;
        string?[] all = [null, .. MacroDraft.TriggerInputs]; int i = Array.IndexOf(all, m.Draft.Trigger(m.Slot));
        try { m.Draft.SetTrigger(m.Slot, all[((i < 0 ? 0 : i) + step + all.Length) % all.Length]); m.Refresh(); RebelLater(); }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException) { rebelMacroNote = error.Message; RebelLater(); }
    }

    /// <summary>The event's editor on torn paper: what it presses, its sticks, its timing, and moving or removing it.</summary>
    private FrameworkElement RebelMacroEditor(MacroWorkbench m, IReadOnlyList<MacroEvent> events, int sel)
    {
        double w = 600, h = 360; var root = new Grid { Width = w, Height = h, RenderTransformOrigin = new(.5, .5), RenderTransform = new RotateTransform(-1) };
        root.Children.Add(new TornPaper { Seed = 12 });
        var body = new StackPanel { Margin = new(32, 28, 32, 20) }; root.Children.Add(body);
        MacroEvent? e = sel >= 0 && sel < events.Count ? events[sel] : null; bool edit = e != null && m.Draft.CanEdit;
        body.Children.Add(Ink.Text(e == null ? "Event editor" : $"Event {sel + 1:00} · what it presses", 27, Ink.B(Ink.Black)));
        body.Children.Add(Ink.Text(e == null ? "Select or add an event to edit it." : $"{RebelPresses(e)} · held {e.HoldMs} ms · then {e.PauseMs} ms pause", 16, Ink.B(Ink.C("#2e2629")), false).Also(t => { t.Margin = new(0, 4, 0, 0); t.TextTrimming = TextTrimming.CharacterEllipsis; }));
        var tabs = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 14, 0, 0) };
        foreach (var (label, key) in new[] { ("Buttons", "buttons"), ("Sticks", "sticks"), ("Timing", "timing"), ("Actions", "actions") })
            tabs.Children.Add(RebelBt(label, null, () => { rebelMacroTab = key; RebelLater(); }, "mtab:" + key, 14, 32, true, rebelMacroTab == key, e != null));
        body.Children.Add(tabs);
        var area = new Grid { Margin = new(0, 18, 0, 0), Height = 180 }; body.Children.Add(area);
        if (e == null) { area.Children.Add(Ink.Text("Add an event above, then choose what it presses here.", 16, Ink.B(Ink.C("#2e2629")), false).Also(t => t.VerticalAlignment = VerticalAlignment.Top)); return root; }
        void Set(MacroEvent next) { m.Update(next); RebelLater(); }
        switch (rebelMacroTab)
        {
            case "sticks":
                var sticks = new StackPanel { Orientation = Orientation.Horizontal };
                foreach (var (label, left) in new[] { ("Left stick", true), ("Right stick", false) })
                {
                    var col = new StackPanel { Margin = new(0, 0, 44, 0) }; col.Children.Add(Ink.Text(label, 21, Ink.B(Ink.Black)));
                    var grid = new UniformGrid3(); StickHeading now = left ? e.Left : e.Right;
                    StickHeading[] order = [StickHeading.UpLeft, StickHeading.Up, StickHeading.UpRight, StickHeading.Left, StickHeading.Neutral, StickHeading.Right, StickHeading.DownLeft, StickHeading.Down, StickHeading.DownRight];
                    foreach (var hd in order) { var dir = hd; grid.Add(RebelBt(RebelArrow(dir), null, () => Set(left ? e with { Left = dir } : e with { Right = dir }), $"stick:{(left ? "L" : "R")}:{dir}", 16, 34, true, now == dir, edit).Also(b => { b.Width = 44; b.Margin = new(0, 0, 6, 6); })); }
                    col.Children.Add(grid.Panel.Also(p => p.Margin = new(0, 10, 0, 0))); sticks.Children.Add(col);
                }
                area.Children.Add(sticks); break;
            case "timing":
                var timing = new StackPanel();
                foreach (var (label, pause) in new[] { ("Hold", false), ("Pause afterward", true) })
                {
                    int v = pause ? e.PauseMs : e.HoldMs; var row = new DockPanel { Margin = new(0, 0, 0, 16) };
                    row.Children.Add(Ink.Text(label, 21, Ink.B(Ink.Black)).Also(t => { t.Width = 200; t.VerticalAlignment = VerticalAlignment.Center; }));
                    row.Children.Add(RebelBt("− 5", null, () => Set(pause ? e with { PauseMs = Math.Max(0, e.PauseMs - 5) } : e with { HoldMs = Math.Max(0, e.HoldMs - 5) }), $"time:{label}:-", 15, 36, true, false, edit));
                    row.Children.Add(Ink.Text($"{v} ms", 26, Ink.B(Ink.Black)).Also(t => { t.Width = 110; t.TextAlignment = TextAlignment.Center; t.VerticalAlignment = VerticalAlignment.Center; }));
                    row.Children.Add(RebelBt("+ 5", null, () => Set(pause ? e with { PauseMs = Math.Min(327675, e.PauseMs + 5) } : e with { HoldMs = Math.Min(327675, e.HoldMs + 5) }), $"time:{label}:+", 15, 36, true, false, edit));
                    timing.Children.Add(row);
                }
                timing.Children.Add(Ink.Text("Each hold uses one capacity unit; a pause after it uses another.", 13.5, Ink.B(Ink.C("#6b6064")), false));
                area.Children.Add(timing); break;
            case "actions":
                var acts = new WrapPanel();
                foreach (var (label, act, ok) in new (string, Action, bool)[] { ("← Move earlier", () => m.Move(-1), sel > 0), ("Move later →", () => m.Move(1), sel < events.Count - 1), ("Duplicate", m.Duplicate, m.Draft.EntryCount(m.Slot) < 47),
                                                                                  ("Delete event", m.Remove, true), ("Clear slot", m.Clear, true) })
                    acts.Children.Add(RebelBt(label, null, () => { act(); RebelLater(); }, "act:" + label, 15, 38, true, false, edit && ok).Also(b => b.Margin = new(0, 0, 10, 12)));
                area.Children.Add(acts); break;
            default:
                // the face diamond as the board draws it, then the rest of the pad's buttons as chips beside it
                var face = new Canvas { Width = 160, Height = 136, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
                foreach (var (k, cx, cy) in new[] { ("Y", 1, 0), ("X", 0, 1), ("B", 2, 1), ("A", 1, 2) }) face.Children.Add(RebelFaceKey(k, e, Set, edit).Also(b => { Canvas.SetLeft(b, cx * 52.5); Canvas.SetTop(b, cy * 52.5 * .82); }));
                area.Children.Add(face);
                var rest = new WrapPanel { Margin = new(190, 0, 0, 0), VerticalAlignment = VerticalAlignment.Top };
                foreach (var k in MacroDraft.Inputs.Where(k => k is not ("A" or "B" or "X" or "Y")))
                    rest.Children.Add(RebelBt(RebelInputGlyph(k), null, () => Set(e with { Buttons = e.Buttons.Contains(k) ? e.Buttons.Where(x => x != k).ToArray() : e.Buttons.Append(k).ToArray() }), "in:" + k, 14, 32, true, e.Buttons.Contains(k), edit).Also(b => { b.MinWidth = 58; b.Margin = new(0, 0, 6, 8); }));
                area.Children.Add(rest); break;
        }
        return root;
    }
    private static Button RebelFaceKey(string k, MacroEvent e, Action<MacroEvent> set, bool edit)
    {
        bool on = e.Buttons.Contains(k); var disc = new Ellipse { Width = 50, Height = 50 }; var letter = Ink.Text(k, 19, null).Also(t => { t.RenderTransform = null; t.HorizontalAlignment = HorizontalAlignment.Center; t.VerticalAlignment = VerticalAlignment.Center; });
        var face = new Grid { Width = 50, Height = 50, Children = { disc, letter } };
        var b = new Button { Content = face, Tag = "in:" + k, FocusVisualStyle = null, Cursor = Cursors.Hand, Template = RebelBare(), IsEnabled = edit };
        void Paint() { bool f = b.IsKeyboardFocused || b.IsMouseOver; disc.Fill = Ink.B(on ? Ink.Red : Ink.A(Ink.Black, .12)); disc.Stroke = f ? Ink.B(Ink.Black) : null; disc.StrokeThickness = 3; letter.Foreground = Ink.B(on ? Colors.White : Ink.C("#151316")); face.Effect = f || on ? Ink.Shadow(5, 5, 0, Ink.Red, 1) : null; }
        b.GotKeyboardFocus += (_, _) => Paint(); b.LostKeyboardFocus += (_, _) => Paint(); b.MouseEnter += (_, _) => Paint(); b.MouseLeave += (_, _) => Paint(); Paint();
        b.Click += (_, _) => set(e with { Buttons = on ? e.Buttons.Where(x => x != k).ToArray() : e.Buttons.Append(k).ToArray() });
        return b;
    }
    /// <summary>Three by three, for the stick directions.</summary>
    private sealed class UniformGrid3
    {
        public System.Windows.Controls.Primitives.UniformGrid Panel { get; } = new() { Columns = 3, Rows = 3, HorizontalAlignment = HorizontalAlignment.Left };
        public void Add(UIElement e) => Panel.Children.Add(e);
    }
}
