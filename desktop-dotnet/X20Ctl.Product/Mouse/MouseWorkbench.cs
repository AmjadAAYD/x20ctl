using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using X20Ctl.Zone.Mouse;
namespace X20Ctl.Product;

/// <summary>
/// The Studio's Mouse tab: switch the controller into a mouse and tune how it moves, scrolls and clicks. Works with any
/// controller Windows reads; nothing is written to it. Changes save as they're made.
/// </summary>
public sealed class MouseWorkbench
{
    static readonly (string Id, string Label)[] Inputs =
    [
        ("A", "A"), ("B", "B"), ("X", "X"), ("Y", "Y"), ("LB", "Left bumper"), ("RB", "Right bumper"), ("LT", "Left trigger"), ("RT", "Right trigger"),
        ("L3", "Left stick click"), ("R3", "Right stick click"), ("DPAD_UP", "D-pad up"), ("DPAD_DOWN", "D-pad down"), ("DPAD_LEFT", "D-pad left"), ("DPAD_RIGHT", "D-pad right"),
        ("SELECT", "View"), ("START", "Menu"), ("HOME", "Home"),
    ];
    static readonly (MouseAction Action, string Label)[] Actions =
    [
        (MouseAction.None, "Nothing"), (MouseAction.LeftClick, "Left click"), (MouseAction.RightClick, "Right click"), (MouseAction.MiddleClick, "Middle click"),
        (MouseAction.Back, "Back"), (MouseAction.Forward, "Forward"), (MouseAction.Precision, "Slow pointer (hold)"),
        (MouseAction.Enter, "Enter"), (MouseAction.Escape, "Escape"), (MouseAction.Tab, "Tab"), (MouseAction.Space, "Space"), (MouseAction.Backspace, "Backspace"),
        (MouseAction.Windows, "Windows key"), (MouseAction.TaskView, "Task view"), (MouseAction.OnScreenKeyboard, "On-screen keyboard"),
        (MouseAction.Up, "Arrow up"), (MouseAction.Down, "Arrow down"), (MouseAction.Left, "Arrow left"), (MouseAction.Right, "Arrow right"),
        (MouseAction.PageUp, "Page up"), (MouseAction.PageDown, "Page down"), (MouseAction.VolumeUp, "Volume up"), (MouseAction.VolumeDown, "Volume down"),
        (MouseAction.Mute, "Mute"), (MouseAction.PlayPause, "Play / pause"),
    ];
    public Grid Body { get; } = new();
    public CheckBox Switch { get; } = new() { Style = DS.Style("DS.Toggle"), VerticalAlignment = VerticalAlignment.Center };
    readonly MouseModeService service;
    readonly TextBlock status, saved;
    readonly ComboBox pointer = new() { MinHeight = 38, Width = 170, FontSize = 14.5, ItemsSource = new[] { "Left stick", "Right stick" } };
    readonly (Slider Slider, TextBlock Value) speed, curve, deadzone, scroll, precision;
    readonly CheckBox scrolls = Toggle(), combo = Toggle();
    readonly Dictionary<string, ComboBox> buttons = new();
    bool loading;
    sealed record Pick(MouseAction Action, string Label);

    public MouseWorkbench(MouseModeService service, Action<Button> register)
    {
        this.service = service; loading = true; // controls fire changes while they are built
        var page = new StackPanel { Margin = new(30, 6, 30, 24), MaxWidth = 1180, HorizontalAlignment = HorizontalAlignment.Left };
        Body.Children.Add(new ScrollViewer { Content = page, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        page.Children.Add(Kit.Text("Controller as mouse", 30, "DS.Text", FontWeights.Bold));
        page.Children.Add(Wrap("Use the controller to move the pointer, scroll, click and type around Windows: from the couch, or in a game's launcher. One stick moves the pointer, the other scrolls, and every button does what you pick below. Nothing is written to the controller.", 15, "DS.TextSoft", 6));

        status = Kit.Text("", 15, "DS.TextSoft").Also(t => t.Margin = new(16, 0, 0, 0));
        Switch.Checked += (_, _) => { if (!loading) service.Active = true; }; Switch.Unchecked += (_, _) => { if (!loading) service.Active = false; };
        page.Children.Add(new Border { CornerRadius = new(16), Padding = new(22, 16, 22, 16), Margin = new(0, 20, 0, 0), BorderThickness = new(1),
            Background = new SolidColorBrush(Color.FromArgb(120, 4, 10, 26)), BorderBrush = new SolidColorBrush(Color.FromArgb(60, 120, 170, 255)),
            Child = new StackPanel { Children = {
                new StackPanel { Orientation = Orientation.Horizontal, Children = { Switch, Kit.Text("Use the controller as a mouse", 18, "DS.Text", FontWeights.SemiBold).Also(t => t.Margin = new(14, 0, 0, 0)), status } },
                Wrap("Hold View + Y for a second to switch it on and off from the controller. While it's on, X20CTL's own controller navigation pauses, and with PC mode on, games see a resting controller, so a click never also presses A in a game.", 14, "DS.TextMuted", 10) } } });

        var moving = new StackPanel();
        moving.Children.Add(Row("Pointer stick", pointer, "The other stick scrolls"));
        speed = Slide(MouseProfile.MinSpeed, MouseProfile.MaxSpeed, 100, v => $"{v:0} px/s"); moving.Children.Add(Row("Speed", speed, "How fast at full push"));
        curve = Slide(MouseProfile.MinCurve, MouseProfile.MaxCurve, .1, v => v <= 1.05 ? "straight" : $"{v:0.0}"); moving.Children.Add(Row("Acceleration", curve, "Higher keeps small pushes slow and precise"));
        deadzone = Slide(0, MouseProfile.MaxDeadzone, 1, v => $"{v:0}%"); moving.Children.Add(Row("Dead zone", deadzone, "Ignored at the centre, so a resting stick never drifts"));
        precision = Slide(MouseProfile.MinPrecision, MouseProfile.MaxPrecision, 5, v => $"{v:0}%"); moving.Children.Add(Row("Slow pointer", precision, "Speed while the slow button is held"));
        moving.Children.Add(Row("Scrolling", scrolls, "The other stick scrolls pages, up/down and sideways"));
        scroll = Slide(MouseProfile.MinScroll, MouseProfile.MaxScroll, 1, v => $"{v:0} lines/s"); moving.Children.Add(Row("Scroll speed", scroll, "At full push"));
        moving.Children.Add(Row("View + Y switch", combo, "Hold for a second to switch the mouse on and off"));
        var moveCard = Kit.Panel("Moving and scrolling", null, null, moving); moveCard.Margin = new(0, 18, 0, 0); page.Children.Add(moveCard);

        var grid = new UniformGrid { Columns = 2 };
        foreach (var (id, label) in Inputs)
        {
            var box = new ComboBox { MinHeight = 38, Width = 230, FontSize = 14.5, DisplayMemberPath = "Label", SelectedValuePath = "Action", ItemsSource = Actions.Select(a => new Pick(a.Action, a.Label)).ToList() };
            box.SelectionChanged += (_, _) => Persist(); buttons[id] = box;
            grid.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 0, 24, 10), Children = { Kit.Text(label, 15, "DS.Text").Also(t => t.Width = 160), box } });
        }
        var buttonCard = Kit.Panel("Buttons", "What each button does while the controller is the mouse. Held arrows, Backspace and Space repeat like a keyboard.", null, grid); buttonCard.Margin = new(0, 16, 0, 0);
        page.Children.Add(buttonCard);

        var reset = Pill("Reset to defaults"); reset.Margin = new(0, 18, 0, 0); reset.HorizontalAlignment = HorizontalAlignment.Left;
        reset.Click += (_, _) => { Fill(new MouseProfile()); Persist(); }; register(reset);
        page.Children.Add(reset);
        saved = Wrap("", 13.5, "DS.TextMuted", 10); page.Children.Add(saved);
        page.Children.Add(Wrap("The default layout follows Gopher360's idea (Tylemagne, GPL-3.0): left stick for the pointer, A to click. X20CTL's version is written fresh, with scrolling, acceleration, a slow pointer and every button free to change.", 12.5, "DS.TextMuted", 14));

        pointer.SelectionChanged += (_, _) => Persist();
        foreach (var box in new[] { scrolls, combo }) { box.Checked += (_, _) => Persist(); box.Unchecked += (_, _) => Persist(); }
        Fill(service.Profile);
        Action changed = () => Body.Dispatcher.BeginInvoke(Refresh);
        Body.Loaded += (_, _) => { service.Changed -= changed; service.Changed += changed; Refresh(); };
        Body.Unloaded += (_, _) => service.Changed -= changed;
        Refresh();
    }

    void Refresh()
    {
        loading = true; Switch.IsChecked = service.Active; loading = false;
        status.Text = service.Status;
    }

    void Fill(MouseProfile p)
    {
        loading = true;
        pointer.SelectedIndex = p.PointerStick == "Right" ? 1 : 0;
        speed.Slider.Value = p.Speed; curve.Slider.Value = p.Curve; deadzone.Slider.Value = p.Deadzone; precision.Slider.Value = p.PrecisionPercent; scroll.Slider.Value = p.ScrollSpeed;
        scrolls.IsChecked = p.Scroll; combo.IsChecked = p.Combo;
        foreach (var (id, box) in buttons) box.SelectedValue = p.Buttons.GetValueOrDefault(id);
        loading = false;
    }

    void Persist()
    {
        if (loading) return;
        var p = new MouseProfile
        {
            PointerStick = pointer.SelectedIndex == 1 ? "Right" : "Left", Speed = (int)speed.Slider.Value, Curve = Math.Round(curve.Slider.Value, 1), Deadzone = (int)deadzone.Slider.Value,
            PrecisionPercent = precision.Slider.Value, ScrollSpeed = (int)scroll.Slider.Value, Scroll = scrolls.IsChecked == true, Combo = combo.IsChecked == true,
            Buttons = buttons.ToDictionary(b => b.Key, b => b.Value.SelectedValue is MouseAction a ? a : MouseAction.None),
        };
        try { service.Save(p); saved.Text = "Saved. It applies straight away."; }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException) { saved.Text = "Couldn't save: " + error.Message; }
    }

    (Slider, TextBlock) Slide(double min, double max, double step, Func<double, string> show)
    {
        var value = Kit.Text("", 14, "DS.TextSoft").Also(t => { t.Width = 92; t.Margin = new(10, 0, 0, 0); });
        var s = new Slider { Minimum = min, Maximum = max, Width = 260, TickFrequency = step, IsSnapToTickEnabled = true, SmallChange = step, LargeChange = step * 5, VerticalAlignment = VerticalAlignment.Center };
        s.ValueChanged += (_, _) => { value.Text = show(s.Value); Persist(); };
        return (s, value);
    }
    static StackPanel Row(string label, (Slider Slider, TextBlock Value) control, string hint) => Row(label, new StackPanel { Orientation = Orientation.Horizontal, Children = { control.Slider, control.Value } }, hint);
    static StackPanel Row(string label, FrameworkElement control, string hint) => new()
    {
        Orientation = Orientation.Horizontal, Margin = new(0, 0, 0, 12),
        Children = { Kit.Text(label, 15, "DS.Text").Also(t => t.Width = 160), control, Kit.Text(hint, 13, "DS.TextMuted").Also(t => t.Margin = new(16, 0, 0, 0)) }
    };
    static CheckBox Toggle() => new() { Style = DS.Style("DS.Toggle"), VerticalAlignment = VerticalAlignment.Center };
    static TextBlock Wrap(string text, double size, string brush, double top) => Kit.Text(text, size, brush).Also(t => { t.TextWrapping = TextWrapping.Wrap; t.TextTrimming = TextTrimming.None; t.Margin = new(0, top, 0, 0); });
    static Button Pill(string text) => new() { Style = DS.Style("DS.ActionSecondary"), Height = 42, MinHeight = 0, Padding = new(18, 0, 20, 0), Content = Kit.Text(text, 14.5, "DS.Text", FontWeights.SemiBold) };
}
