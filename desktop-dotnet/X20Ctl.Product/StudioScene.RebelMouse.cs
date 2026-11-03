using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using X20Ctl.Product.Rebel;
using X20Ctl.Zone.Mouse;
namespace X20Ctl.Product;

/// <summary>The Rebel Controller as mouse page (owner board mouse.png): the buttons are a stencilled wall, the chosen input's action is picked on
/// a sheet of torn paper. Pointer and scroll settings sit on the same sheet. Every value is the Mouse workbench's own.</summary>
public partial class StudioScene
{
    private Grid? rebelMouse; private MouseWorkbench? rebelMouseBench; private string rebelMouseTab = "assign", rebelMouseGroup = ""; private TextBlock? rebelMouseReadout;

    private void RebelMousePage()
    {
        var m = Mouse; var g = rebelMouse!; g.Children.Clear();
        if (m == null) { g.Children.Add(Place(Ink.Text("Controller as mouse isn't available in this window.", 18, Ink.B(Ink.C("#cfc7c9")), false), 268, 200)); return; }
        if (!ReferenceEquals(m, rebelMouseBench))
        {
            rebelMouseBench = m; m.Watch(() => { if (rebelPage == "mouse" && ReferenceEquals(rebelMouseBench, Mouse)) RebelLater(); });
            m.WatchReadout(t => { if (rebelMouseReadout != null) rebelMouseReadout.Text = t; });
        }
        string? focused = (Keyboard.FocusedElement as FrameworkElement) is { Tag: string t } fe && g.IsAncestorOf(fe) ? t : null;
        string sel = m.SelectedKey;

        // the two rows over the wall
        var sw = new RowSlab(0, 52) { Width = 636, Tag = "ms:switch" }; RPut(sw, 264, 160);
        sw.Left.Children.Add(sw.Tint(Ink.Text("Controller as mouse", 19), Ink.White, Ink.Black));
        sw.Right.Children.Add(sw.Tint(Ink.Text(m.StatusText, 15, null, false, FontWeights.SemiBold), Ink.C("#c9c1c3"), Ink.Black).Also(s => s.Margin = new(0, 0, 14, 0)));
        var track = RebelToggle(m.IsOn, () => sw.IsKeyboardFocused); sw.Right.Children.Add(track);
        sw.GotKeyboardFocus += (_, _) => { sw.On = true; (track.Tag as Action)?.Invoke(); }; sw.LostKeyboardFocus += (_, _) => { sw.On = false; (track.Tag as Action)?.Invoke(); };
        sw.Click += (_, _) => m.FlipSwitch(); g.Children.Add(sw);
        var src = new RowSlab(1, 52) { Width = 636, Tag = "ms:source" }; RPut(src, 266, 222);
        src.Left.Children.Add(src.Tint(Ink.Text("Desktop input source", 19), Ink.White, Ink.Black));
        src.Right.Children.Add(new Border { Height = 34, Padding = new(14, 0, 14, 0), Background = Ink.B(Ink.Black), BorderBrush = Ink.B(Ink.A(Ink.White, .55)), BorderThickness = new(1.5), RenderTransform = new SkewTransform(-10, 0),
            Child = new StackPanel { Orientation = Orientation.Horizontal, Children = { Ink.Text(m.SourceBox.SelectedItem as string ?? "Choose input source", 15, Ink.B(Ink.White), false, FontWeights.SemiBold), Glyphs.Make("", 10, Ink.B(Ink.Hot)).Also(i => i.Margin = new(12, 0, 0, 0)) } } });
        src.GotKeyboardFocus += (_, _) => src.On = true; src.LostKeyboardFocus += (_, _) => src.On = false; src.Click += (_, _) => RebelMouseStep(m.SourceBox, 1); g.Children.Add(src);

        // the wall
        var wall = new Canvas { Width = 640, Height = 510, IsHitTestVisible = false }; RPut(wall, 262, 318);
        wall.Children.Add(new Rectangle { Width = 640, Height = 510, Fill = Ink.B(Ink.C("#171517")) });
        wall.Children.Add(Dots.Field(0, 0, 640, 510, Ink.A(Colors.Black, .5), 12, 2.4, Dots.Fade(40, .9)));
        for (int c = 1; c < 4; c++) wall.Children.Add(new Line { X1 = c * 160, Y1 = 56, X2 = c * 160, Y2 = 510, Stroke = Ink.B(Ink.C("#0e0d0f")), StrokeThickness = 3 });
        for (int r = 1; r < 5; r++) wall.Children.Add(new Line { X1 = 0, Y1 = 56 + r * 91, X2 = 640, Y2 = 56 + r * 91, Stroke = Ink.B(Ink.C("#0e0d0f")), StrokeThickness = 3 });
        g.Children.Add(wall);
        var head = Pieces.Tag($"Button mappings · {m.MappedText}"); head.HorizontalAlignment = HorizontalAlignment.Left; head.Margin = new(18, 12, 0, 0); head.VerticalAlignment = VerticalAlignment.Top; head.Margin = new(280, 330, 0, 0); g.Children.Add(head);
        string[] order = ["A", "B", "X", "Y", "LB", "RB", "LT", "RT", "L3", "R3", "START", "HOME", "SELECT", "DPAD_UP", "DPAD_DOWN", "DPAD_LEFT", "DPAD_RIGHT"]; var inputs = m.Inputs.OrderBy(k => { int o = Array.IndexOf(order, k); return o < 0 ? 99 : o; }).ToList();
        for (int i = 0; i < inputs.Count; i++)
        {
            string key = inputs[i]; bool on = key == sel; string name = RebelInputGlyph(key);
            var letters = Ink.Text(name, name.Length > 2 ? 26 : 38, Ink.B(on ? Ink.Red : Ink.C("#d8d2d4")));
            var action = Ink.Text(m.ActionFor(key), 13, Ink.B(on ? Ink.Hot : Ink.C("#b8b0b3")), false); action.Margin = new(0, 4, 0, 0);
            var col = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center }; col.Children.Add(new Border { Child = letters, HorizontalAlignment = HorizontalAlignment.Center, Effect = Ink.Shadow(2, 3, 0, Colors.Black, .9) }); col.Children.Add(new Border { Child = action, HorizontalAlignment = HorizontalAlignment.Center });
            var cell = new Grid { Width = 160, Height = 91, Background = Brushes.Transparent }; cell.Children.Add(col);
            if (on) cell.Children.Add(new Border { Height = 5, Width = 96, Background = Ink.B(Ink.Red), VerticalAlignment = VerticalAlignment.Bottom, Margin = new(0, 0, 0, 10), RenderTransform = new SkewTransform(-20, 0), RenderTransformOrigin = new(.5, .5) });
            var frame = new Border { BorderThickness = new(2), BorderBrush = Brushes.Transparent, Child = cell };
            var b = new Button { Content = frame, Tag = "ms:in:" + key, Template = RebelBare(), FocusVisualStyle = null, Cursor = Cursors.Hand }; RPut(b, 262 + i % 4 * 160, 374 + i / 4 * 91);
            void Paint() { frame.BorderBrush = b.IsKeyboardFocused || b.IsMouseOver ? Ink.B(Ink.A(Ink.White, .8)) : Brushes.Transparent; }
            b.GotKeyboardFocus += (_, _) => Paint(); b.LostKeyboardFocus += (_, _) => Paint(); b.MouseEnter += (_, _) => Paint(); b.MouseLeave += (_, _) => Paint(); b.Click += (_, _) => m.Select(key);
            g.Children.Add(b);
        }

        // the sheet: tabs on top, then the page of the tab
        var tabs = new StackPanel { Orientation = Orientation.Horizontal }; RPut(tabs, 964, 108);
        foreach (var (key, label) in new[] { ("assign", "Assign"), ("pointer", "Pointer"), ("scroll", "Scroll") })
        { string k = key; var chip = new ChipTab(label, 15) { On = rebelMouseTab == k, Tag = "ms:tab:" + k, Width = 98 }; chip.Click += (_, _) => { rebelMouseTab = k; RebelLater(); }; tabs.Children.Add(chip); }
        g.Children.Add(tabs);
        var paper = new Grid { Width = 590, Height = 660, RenderTransformOrigin = new(.5, .5), RenderTransform = new RotateTransform(1) }; RPut(paper, 960, 156);
        paper.Children.Add(new TornPaper { Seed = 21 });
        var body = new StackPanel { Margin = new(36, 34, 36, 20) }; paper.Children.Add(body);
        switch (rebelMouseTab)
        {
            case "pointer": RebelMousePointer(body, m); break;
            case "scroll": RebelMouseScroll(body, m); break;
            default: RebelMouseAssign(body, m); break;
        }
        g.Children.Add(paper);
        var note = new StackPanel { Orientation = Orientation.Horizontal }; note.Children.Add(new Canvas { Width = 22, Height = 22, Children = { Star.Make(11, 11, 8, 0, 1) } });
        note.Children.Add(Ink.Text($"Controller as mouse · {m.SavedText}", 14, Ink.B(Ink.C("#9d9598")), false).Also(x => { x.Margin = new(6, 0, 0, 0); x.RenderTransform = Ink.Slant; }));
        g.Children.Add(Place(note, 262, 842));

        string want = focused ?? "ms:in:" + sel;
        Dispatcher.BeginInvoke(() => FindTagged(g, want)?.Focus(), DispatcherPriority.Input);
    }
    private static void RebelMouseStep(ComboBox box, int step) { int n = box.Items.Count; if (n > 0) box.SelectedIndex = ((box.SelectedIndex < 0 ? 0 : box.SelectedIndex) + step + n) % n; }
    private static void RebelMouseSlide(Slider s, int step, bool big) { double d = s.TickFrequency * (big ? 5 : 1) * step, v = s.Value + d; s.Value = v > s.Maximum ? s.Minimum : v < s.Minimum ? s.Maximum : v; }

    private void RebelMouseAssign(StackPanel body, MouseWorkbench m)
    {
        body.Children.Add(Ink.Text($"Assign Input · {m.LabelFor(m.SelectedKey)}", 30, Ink.B(Ink.Black)));
        body.Children.Add(RebelBody("Choose its desktop action", 16, "#4a4145").Also(t => { t.Margin = new(0, 4, 0, 8); t.RenderTransform = Ink.Slant; }));
        var current = m.CurrentAction; var groups = m.Groups; if (!groups.Any(x => x.Group == rebelMouseGroup)) rebelMouseGroup = groups.FirstOrDefault(x => x.Actions.Contains(current)).Group ?? "MOUSE";
        foreach (var (group, actions) in groups)
        {
            string gk = group; bool open = gk == rebelMouseGroup;
            var head = new Button { Tag = "ms:g:" + gk, Template = RebelBare(), FocusVisualStyle = null, Cursor = Cursors.Hand, HorizontalContentAlignment = HorizontalAlignment.Left, Margin = new(0, 6, 0, 0) };
            var label = Ink.Label(gk, 12, Ink.B(Ink.Red)); head.Content = new Border { Padding = new(0, 5, 0, 5), Child = label, Background = Brushes.Transparent };
            head.Click += (_, _) => { rebelMouseGroup = gk; RebelLater(); }; head.GotKeyboardFocus += (_, _) => label.Foreground = Ink.B(Ink.Black); head.LostKeyboardFocus += (_, _) => label.Foreground = Ink.B(Ink.Red);
            body.Children.Add(head);
            if (!open) continue;
            var list = new StackPanel(); int i = 0;
            foreach (var a in actions)
            {
                var act = a; bool on = a == current; var row = RebelOption(MouseWorkbench.Friendly(a), on, () => m.Assign(act), "ms:a:" + (int)a, i++);
                list.Children.Add(row);
            }
            body.Children.Add(new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden, MaxHeight = actions.Length > 6 ? 208 : double.PositiveInfinity, Margin = new(0, 2, 0, 4), Focusable = false });
        }
        var presets = new WrapPanel { Margin = new(0, 14, 0, 0) };
        foreach (var n in MouseWorkbench.PresetNames) { string name = n; presets.Children.Add(RebelBt(name, null, () => m.ApplyPreset(name), "ms:p:" + name, 12.5, 30, true).Also(b => b.Margin = new(0, 0, 6, 6))); }
        presets.Children.Add(RebelBt("Reset Mouse Mode", null, m.ResetMode, "ms:reset", 12.5, 30, true).Also(b => b.Margin = new(0, 0, 6, 6)));
        body.Children.Add(presets);
        body.Children.Add(RebelBody("One player controls the desktop at a time. PC mode output pauses while Mouse Mode is on; physical game input can still reach games unless it is hidden by your PC mode setup.", 12.5, "#6b6064").Also(t => { t.Margin = new(0, 8, 0, 0); t.RenderTransform = Ink.Slant; }));
    }
    /// <summary>One action on the paper (.op): a grey slab, black with a red edge when it is the action chosen or in focus.</summary>
    private static Button RebelOption(string text, bool on, Action click, string tag, int index)
    {
        var label = Ink.Text(text, 16, null, true); label.RenderTransform = null; label.VerticalAlignment = VerticalAlignment.Center;
        var face = new Border { Height = 36, Margin = new(index % 2 * 3, 0, 0, 3), Padding = new(24, 0, 12, 0), Child = label, BorderThickness = new(0, 0, 0, 0) };
        var b = new Button { Content = face, Tag = tag, Template = RebelBare(), FocusVisualStyle = null, Cursor = Cursors.Hand };
        void Paint() { bool f = b.IsKeyboardFocused || b.IsMouseOver; face.Background = Ink.B(on || f ? Ink.Black : Ink.C("#d9d0be")); label.Foreground = Ink.B(on || f ? Ink.White : Ink.Black); face.BorderBrush = Ink.B(Ink.Red); face.BorderThickness = new(0, 0, 0, on ? 4 : 0); }
        b.GotKeyboardFocus += (_, _) => Paint(); b.LostKeyboardFocus += (_, _) => Paint(); b.MouseEnter += (_, _) => Paint(); b.MouseLeave += (_, _) => Paint(); b.Click += (_, _) => click(); Paint();
        return b;
    }
    /// <summary>A setting on the paper: its name, its value, and left and right step it (a click steps on).</summary>
    private static Button RebelMouseRow(string label, Func<string> value, Action<int, bool> step, string tag)
    {
        var name = Ink.Text(label, 17, Ink.B(Ink.Black)); name.VerticalAlignment = VerticalAlignment.Center;
        var val = Ink.Text(value(), 15, Ink.B(Ink.C("#2e2629")), false, FontWeights.SemiBold); val.VerticalAlignment = VerticalAlignment.Center; val.HorizontalAlignment = HorizontalAlignment.Right;
        var inner = new Grid(); inner.Children.Add(name); inner.Children.Add(val);
        var face = new Border { Height = 38, Padding = new(14, 0, 14, 0), Margin = new(0, 0, 0, 4), Child = inner };
        var b = new Button { Content = face, Tag = tag, Template = RebelBare(), FocusVisualStyle = null, Cursor = Cursors.Hand };
        void Paint() { bool f = b.IsKeyboardFocused || b.IsMouseOver; face.Background = Ink.B(f ? Ink.Black : Ink.C("#d9d0be")); name.Foreground = Ink.B(f ? Ink.White : Ink.Black); val.Foreground = Ink.B(f ? Ink.C("#f1e9ea") : Ink.C("#2e2629")); }
        b.GotKeyboardFocus += (_, _) => Paint(); b.LostKeyboardFocus += (_, _) => Paint(); b.MouseEnter += (_, _) => Paint(); b.MouseLeave += (_, _) => Paint(); b.Click += (_, _) => step(1, false);
        b.Tag = tag; b.Uid = "step"; b.DataContext = step; Paint(); return b;
    }
    private void RebelMousePointer(StackPanel body, MouseWorkbench m)
    {
        body.Children.Add(Ink.Text("Pointer", 30, Ink.B(Ink.Black)));
        body.Children.Add(RebelBody("Left and right change a value.", 15, "#4a4145").Also(t => { t.Margin = new(0, 4, 0, 12); t.RenderTransform = Ink.Slant; }));
        body.Children.Add(RebelMouseRow("Pointer source", () => m.PointerBox.SelectedItem as string ?? "", (s, _) => RebelMouseStep(m.PointerBox, s), "ms:pointer"));
        body.Children.Add(RebelMouseRow("Speed", () => $"{m.SpeedSlider.Value:0} px/s", (s, big) => RebelMouseSlide(m.SpeedSlider, s, big), "ms:speed"));
        body.Children.Add(RebelMouseRow("Acceleration", () => $"{m.CurveSlider.Value:0.0} · None → Fast", (s, big) => RebelMouseSlide(m.CurveSlider, s, big), "ms:curve"));
        body.Children.Add(RebelMouseRow("Dead zone", () => $"{m.DeadzoneSlider.Value:0}%", (s, big) => RebelMouseSlide(m.DeadzoneSlider, s, big), "ms:deadzone"));
        body.Children.Add(RebelBt(m.Testing ? "Stop pointer test" : "Test pointer", null, m.TestPointer, "ms:test", 15, 40, true).Also(b => b.Margin = new(0, 14, 0, 0)));
        rebelMouseReadout = RebelBody(m.ReadoutText, 14, "#2e2629").Also(t => t.Margin = new(0, 12, 0, 0)); body.Children.Add(rebelMouseReadout);
    }
    private void RebelMouseScroll(StackPanel body, MouseWorkbench m)
    {
        body.Children.Add(Ink.Text("Scroll & precision", 30, Ink.B(Ink.Black)));
        body.Children.Add(RebelBody("Left and right change a value.", 15, "#4a4145").Also(t => { t.Margin = new(0, 4, 0, 10); t.RenderTransform = Ink.Slant; }));
        static string OnOff(CheckBox c) => c.IsChecked == true ? "On" : "Off";
        Action<int, bool> Flip(CheckBox c) => (_, _) => c.IsChecked = c.IsChecked != true;
        body.Children.Add(RebelMouseRow("Scrolling", () => OnOff(m.ScrollsBox), Flip(m.ScrollsBox), "ms:scrolls"));
        body.Children.Add(RebelMouseRow("Scroll speed", () => $"{m.ScrollSlider.Value:0} lines/s", (s, big) => RebelMouseSlide(m.ScrollSlider, s, big), "ms:scroll"));
        body.Children.Add(RebelMouseRow("Precision speed", () => $"{m.PrecisionSlider.Value:0}%", (s, big) => RebelMouseSlide(m.PrecisionSlider, s, big), "ms:precision"));
        body.Children.Add(RebelMouseRow("Horizontal scroll", () => OnOff(m.HorizontalBox), Flip(m.HorizontalBox), "ms:horizontal"));
        body.Children.Add(RebelMouseRow("Invert vertical", () => OnOff(m.InvertVBox), Flip(m.InvertVBox), "ms:invv"));
        body.Children.Add(RebelMouseRow("Invert horizontal", () => OnOff(m.InvertHBox), Flip(m.InvertHBox), "ms:invh"));
        body.Children.Add(RebelMouseRow("Quick toggle", () => OnOff(m.ComboToggle), Flip(m.ComboToggle), "ms:combo"));
        body.Children.Add(RebelMouseRow("Shortcut", () => $"{(m.Chord1Box.SelectedItem is { } a ? a.ToString() : "")} + {(m.Chord2Box.SelectedItem is { } b ? b.ToString() : "")}", (s, _) => RebelMouseStep(m.Chord1Box, s), "ms:chord1"));
        body.Children.Add(RebelMouseRow("Hold for", () => m.HoldBox.SelectedItem as string ?? "", (s, _) => RebelMouseStep(m.HoldBox, s), "ms:hold"));
        body.Children.Add(RebelBody(m.ScrollSourceText + " · " + m.PrecisionSourceText, 12.5, "#6b6064").Also(t => { t.Margin = new(0, 8, 0, 0); t.RenderTransform = Ink.Slant; }));
        body.Children.Add(RebelBody("When enabled, both shortcut buttons are reserved, including single presses. Their normal mouse mappings will not run.", 12.5, "#6b6064").Also(t => { t.Margin = new(0, 4, 0, 0); t.RenderTransform = Ink.Slant; }));
    }
    /// <summary>Left and Right step the value of a row on the sheet (Shift by five); on the source row they change the source.</summary>
    private bool RebelMouseKey(KeyEventArgs e)
    {
        if (rebelPage != "mouse" || Mouse == null || e.Key is not (Key.Left or Key.Right) || Keyboard.FocusedElement is not Button { Tag: string tag } b) return false;
        int step = e.Key == Key.Right ? 1 : -1;
        if (tag == "ms:source") { if (!e.IsRepeat) RebelMouseStep(Mouse.SourceBox, step); e.Handled = true; return true; }
        if (tag == "ms:switch") return false;
        if (b.DataContext is Action<int, bool> act) { act(step, (Keyboard.Modifiers & ModifierKeys.Shift) != 0); e.Handled = true; return true; }
        return false;
    }
}
