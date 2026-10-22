using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using X20Ctl.Zone;
using X20Ctl.Zone.Mouse;
using X20Ctl.Zone.PcMode;
namespace X20Ctl.Linux;

/// <summary>Controller as mouse: one stick moves the pointer, the other scrolls, and every button picks an action.</summary>
internal sealed class MousePage : UserControl, IKeyPage
{
    static readonly string[] Order = ["DPAD_UP", "DPAD_DOWN", "DPAD_LEFT", "DPAD_RIGHT", "START", "SELECT", "L3", "R3", "LB", "RB", "A", "B", "X", "Y", "LT", "RT"];
    static readonly string[] Groups = ["DISABLED", "MOUSE", "PRECISION", "KEYBOARD", "WINDOWS", "MEDIA"];
    readonly StudioState state; readonly MouseHub hub = MouseHub.Shared; string selected = "A"; string open = "MOUSE";

    public MousePage(StudioState state)
    {
        this.state = state; hub.Changed += OnHub; DetachedFromVisualTree += (_, _) => hub.Changed -= OnHub; Build();
    }
    void OnHub() => Avalonia.Threading.Dispatcher.UIThread.Post(Build);
    public void OnKey(KeyEventArgs e, bool down) { if (down && e.Key == Key.Escape) { } }

    static string Input(string key) => key switch { "SELECT" => "View", "START" => "Menu", "HOME" => "Home", "DPAD_UP" => "D-pad ↑", "DPAD_DOWN" => "D-pad ↓", "DPAD_LEFT" => "D-pad ←", "DPAD_RIGHT" => "D-pad →", _ => key };
    public static string Friendly(MouseAction a) => a switch
    {
        MouseAction.None => "Disabled", MouseAction.Precision => "Precision mode", MouseAction.OnScreenKeyboard => "On-screen keyboard", MouseAction.TaskView => "Overview", MouseAction.Windows => "Super key",
        MouseAction.LeftClick => "Left click", MouseAction.RightClick => "Right click", MouseAction.MiddleClick => "Middle click", MouseAction.PageUp => "Page up", MouseAction.PageDown => "Page down",
        MouseAction.VolumeUp => "Volume up", MouseAction.VolumeDown => "Volume down", MouseAction.PlayPause => "Play / pause", _ => a.ToString()
    };
    static string Category(MouseAction a) => a switch
    {
        MouseAction.None => "DISABLED", <= MouseAction.Forward => "MOUSE", MouseAction.Precision => "PRECISION", >= MouseAction.VolumeUp => "MEDIA",
        MouseAction.Windows or MouseAction.TaskView or MouseAction.OnScreenKeyboard => "WINDOWS", _ => "KEYBOARD"
    };

    void Save(Action<MouseProfile> change) { var next = hub.Profile.Clone(); change(next); hub.Save(next); }
    string Action(string key) { var p = hub.Profile; return p.Combo && p.ToggleButtons.Contains(key) ? "Shortcut reserved" : Friendly(p.Buttons.GetValueOrDefault(key)); }

    void Build()
    {
        var p = hub.Profile; var engine = hub.Engine;
        var page = new StackPanel { Spacing = 14, Margin = new Thickness(36, 6, 36, 18) };

        string status = !PcEngine.Supported ? "Not available" : !hub.Enabled ? "Off" : engine.Pointing ? "Active" : engine.Headline;
        var head = new Grid { ColumnDefinitions = new("*,Auto") };
        head.Children.Add(T.Stack(2, T.Label("Controller as mouse", 36, null, FontWeight.Bold), T.Label("Player 1 · " + state.Model.Name, 16, T.Soft)));
        var sw = T.Line(14, T.Label(status, 16, T.Soft), Toggle(hub.Enabled, v => { if (PcEngine.Supported) hub.SetEnabled(v); }));
        sw.VerticalAlignment = VerticalAlignment.Top; sw.Margin = new Thickness(0, 6, 0, 0); Grid.SetColumn(sw, 1); head.Children.Add(sw); page.Children.Add(head);
        page.Children.Add(T.Label(hub.Enabled && engine.Detail.Length > 0 ? engine.Detail : "The first controller found drives the desktop. PC mode steps aside while the mouse is on; hold the shortcut to switch it off and on from the pad.", 14.5, T.Soft));

        var editor = new Grid { ColumnDefinitions = new("1.15*,1*") };
        editor.Children.Add(Visual());
        var assign = Assign(); Grid.SetColumn(assign, 1); assign.Margin = new Thickness(16, 0, 0, 0); editor.Children.Add(assign);
        page.Children.Add(editor);

        var tune = new Grid { ColumnDefinitions = new("1*,1*") };
        tune.Children.Add(Pointer(p));
        var extra = Extras(p); Grid.SetColumn(extra, 1); extra.Margin = new Thickness(16, 0, 0, 0); tune.Children.Add(extra);
        page.Children.Add(tune);

        var presets = T.Wrap(8, new Pill("Desktop", Pill.Kinds.Ghost, () => Preset("Desktop")), new Pill("Web browsing", Pill.Kinds.Ghost, () => Preset("Web browsing")), new Pill("Media", Pill.Kinds.Ghost, () => Preset("Media")),
            new Pill("Minimal", Pill.Kinds.Ghost, () => Preset("Minimal")), new Pill("Reset Mouse Mode", Pill.Kinds.Ghost, () => hub.Save(new MouseProfile())));
        page.Children.Add(T.Stack(4, T.Label("PRESETS", 12, T.Soft, FontWeight.SemiBold), presets));
        page.Children.Add(T.Label(hub.Status, 14, T.Soft));
        Content = new ScrollViewer { Content = page };
    }

    static Control Toggle(bool on, Action<bool> set)
    {
        var knob = new Border { Width = 22, Height = 22, CornerRadius = new CornerRadius(11), Background = Brushes.White, HorizontalAlignment = on ? HorizontalAlignment.Right : HorizontalAlignment.Left, Margin = new Thickness(3), VerticalAlignment = VerticalAlignment.Center };
        var track = new Border { Width = 56, Height = 30, CornerRadius = new CornerRadius(15), Background = on ? T.PrimaryFill : T.B("#33FFFFFF"), BorderBrush = T.B("#55FFFFFF"), BorderThickness = new Thickness(1), Child = knob, Cursor = new Cursor(StandardCursorType.Hand) };
        track.OnTap(() => set(!on)); return track;
    }

    // ---- the pad and its buttons -----------------------------------------------------------------------------------

    Control Visual()
    {
        var p = hub.Profile; int configured = Order.Count(k => p.Buttons.GetValueOrDefault(k) != MouseAction.None);
        var art = new ControllerArt(state.Model.Id) { Height = 300, Margin = new Thickness(0, 4, 0, 4) };
        foreach (var (name, pt) in state.Model.FrontButtons) if (Order.Contains(name)) art.Children.Add(Spot(name, pt));
        var chips = new WrapPanel();
        foreach (string key in Order)
        {
            string k = key; bool on = k == selected;
            var chip = new Border
            {
                Padding = new Thickness(14, 8), Margin = new Thickness(0, 0, 8, 8), CornerRadius = new CornerRadius(20), Cursor = new Cursor(StandardCursorType.Hand), BorderThickness = new Thickness(on ? 1.6 : 1),
                Background = on ? T.B("#552A62C8") : T.B("#33FFFFFF"), BorderBrush = on ? T.B("#9CCBFF") : T.B("#22FFFFFF"),
                Child = T.Line(10, T.Label(Input(k), 13.5, T.Text, FontWeight.SemiBold), T.Label(Action(k), 13.5, T.Soft))
            };
            chip.OnTap(() => { selected = k; Build(); }); chips.Children.Add(chip);
        }
        var note = T.Label("Select an input on the artwork or below. Gyro, touch surfaces and independent rear inputs are unavailable from this source.", 12.5, T.Soft);
        return T.Card(T.Stack(6, Parts.Caps("BUTTON MAPPINGS"), T.Label($"{configured} / {Order.Length} configured", 13.5, T.Soft), art, chips, note), 24);
    }

    Control Spot(string name, ControlPoint pt)
    {
        bool on = selected == name; bool set = hub.Profile.Buttons.GetValueOrDefault(name) != MouseAction.None;
        var ring = new Border
        {
            Width = on ? 40 : 34, Height = on ? 40 : 34, CornerRadius = new CornerRadius(20), Cursor = new Cursor(StandardCursorType.Hand), Background = on ? T.B("#CC2A62C8") : T.B("#01000000"),
            BorderBrush = on ? T.B("#CFE3FF") : set ? T.B("#667DB6FF") : T.B("#14FFFFFF"), BorderThickness = new Thickness(on ? 2.4 : 1),
            BoxShadow = on ? new BoxShadows(new BoxShadow { Blur = 18, Color = Color.Parse("#993D8BFF") }) : default
        };
        if (name is "LB" or "RB" or "LT" or "RT") ring.Width = ring.Height = on ? 34 : 30;
        string key = name; ring.OnTap(() => { selected = key; Build(); });
        ControllerArt.SetAnchor(ring, new Point(pt.X, pt.Y)); return ring;
    }

    Control Assign()
    {
        var current = hub.Profile.Buttons.GetValueOrDefault(selected);
        var col = T.Stack(10, Parts.Caps("ASSIGN INPUT"), T.Label(Input(selected), 26, null, FontWeight.SemiBold));
        if (hub.Profile.Combo && hub.Profile.ToggleButtons.Contains(selected)) { col.Children.Add(T.Label("This input belongs to the mouse shortcut. Turn the quick toggle off to use it.", 14, T.Soft)); return T.Card(col, 24); }
        col.Children.Add(T.Label("Choose its desktop action", 14, T.Soft));
        foreach (string group in Groups)
        {
            string g = group; bool isOpen = open == g;
            var header = T.Line(10, T.Label(isOpen ? "⌄" : "›", 15, T.Soft), T.Label(g, 14, T.Text)); header.Cursor = new Cursor(StandardCursorType.Hand); header.Margin = new Thickness(0, 6, 0, 0);
            header.OnTap(() => { open = isOpen ? "" : g; Build(); }); col.Children.Add(header);
            if (!isOpen) continue;
            var wrap = new WrapPanel { Margin = new Thickness(22, 0, 0, 0) };
            foreach (var action in Enum.GetValues<MouseAction>().Where(a => Category(a) == g))
            {
                var a = action; bool on = a == current;
                var chip = new Pill(Friendly(a), on ? Pill.Kinds.Primary : Pill.Kinds.Ghost, () => Save(next => next.Buttons[selected] = a)) { Padding = new Thickness(16, 8), Margin = new Thickness(0, 0, 8, 8) };
                wrap.Children.Add(chip);
            }
            col.Children.Add(wrap);
        }
        return T.Card(col, 24);
    }

    // ---- the numbers -------------------------------------------------------------------------------------------------

    Control Row(string label, Control control, string value)
    {
        var g = new Grid { ColumnDefinitions = new("150,*,120") }; g.Children.Add(T.Label(label, 14.5, T.Text));
        Grid.SetColumn(control, 1); control.VerticalAlignment = VerticalAlignment.Center; g.Children.Add(control);
        var v = T.Label(value, 13.5, T.Soft); v.HorizontalAlignment = HorizontalAlignment.Right; v.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(v, 2); g.Children.Add(v); return g;
    }

    Control Slider(double min, double max, double value, Action<MouseProfile, double> set)
    {
        var s = new FlatSlider { Minimum = min, Maximum = max, Value = value };
        s.PointerReleased += (_, _) => Save(next => set(next, s.Value)); s.KeyUp += (_, _) => Save(next => set(next, s.Value));
        return s;
    }

    Control Pointer(MouseProfile p) => T.Card(T.Stack(12, Parts.Caps("POINTER"),
        Row("Pointer source", Segmented.Make(["Left stick", "Right stick"], p.PointerStick == "Right" ? "Right stick" : "Left stick", v => Save(n => n.PointerStick = v.StartsWith("Right") ? "Right" : "Left"), 13), ""),
        Row("Speed", Slider(MouseProfile.MinSpeed, MouseProfile.MaxSpeed, p.Speed, (n, v) => n.Speed = (int)v), $"{p.Speed} px/s"),
        Row("Acceleration", Slider(MouseProfile.MinCurve, MouseProfile.MaxCurve, p.Curve, (n, v) => n.Curve = Math.Round(v, 1)), $"{p.Curve:0.0} · None → Fast"),
        Row("Dead zone", Slider(0, MouseProfile.MaxDeadzone, p.Deadzone, (n, v) => n.Deadzone = (int)v), p.Deadzone + "%")), 24);

    Control Extras(MouseProfile p) => T.Card(T.Stack(12, Parts.Caps("SCROLLING AND SHORTCUT"),
        Row("Scrolling", Segmented.Make(["On", "Off"], p.Scroll ? "On" : "Off", v => Save(n => n.Scroll = v == "On"), 13), "Scroll source · " + (p.PointerStick == "Left" ? "Right" : "Left") + " stick"),
        Row("Scroll speed", Slider(MouseProfile.MinScroll, MouseProfile.MaxScroll, p.ScrollSpeed, (n, v) => n.ScrollSpeed = (int)v), p.ScrollSpeed + " lines/s"),
        Row("Precision speed", Slider(MouseProfile.MinPrecision, MouseProfile.MaxPrecision, p.PrecisionPercent, (n, v) => n.PrecisionPercent = Math.Round(v)), $"{p.PrecisionPercent:0}%"),
        Row("Quick toggle", Segmented.Make(["On", "Off"], p.Combo ? "On" : "Off", v => Save(n => n.Combo = v == "On"), 13), p.Combo ? string.Join(" + ", p.ToggleButtons.Select(Input)) + " held" : "")), 24);

    void Preset(string name)
    {
        Save(next =>
        {
            next.Buttons = MouseProfile.Defaults();
            if (name == "Minimal") foreach (string key in next.Buttons.Keys.ToArray()) if (key is not ("A" or "B" or "LT")) next.Buttons[key] = MouseAction.None;
            if (name == "Web browsing") { next.Buttons["X"] = MouseAction.PageDown; next.Buttons["Y"] = MouseAction.PageUp; }
            if (name == "Media") { next.Buttons["A"] = MouseAction.PlayPause; next.Buttons["B"] = MouseAction.Mute; next.Buttons["LT"] = MouseAction.VolumeDown; next.Buttons["RT"] = MouseAction.VolumeUp; }
        });
    }
}
