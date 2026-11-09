using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using X20Ctl.Zone;
using X20Ctl.Zone.Mouse;
using X20Ctl.Zone.PcMode;
namespace X20Ctl.Product;

/// <summary>
/// The Keyboard tab, PC mode's macro page (owner, 7 Nov 2026): the controller on top, a full keyboard below it, and every
/// mapping listed underneath. Pick a controller button, then a key: that button now presses the key (or a mouse button)
/// and the game stops seeing it. Custom turns one press into a list of keys played in order.
/// </summary>
public sealed class KeyboardWorkbench
{
    public Grid Body { get; } = new();
    readonly PcModeLink link; readonly Action<Button> register;
    readonly ControllerBoard board; readonly KeyboardDiagram keyboard;
    readonly TextBlock selectedName = Copy("", 30, true), selectedSays = Copy("", 17), saved = Copy("", 13), target = Copy("", 15), mappedCount = Copy("", 13);
    readonly StackPanel listRows = new(), customPanel = new() { Visibility = Visibility.Collapsed }, customSteps = new();
    readonly WrapPanel mouseChips = new() { Margin = new(0, 10, 0, 0) };
    readonly Button keyMode, customMode, clear, saveCustom, cancelCustom;
    readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(33) };
    readonly List<string> inputs;
    string selection = "A"; bool custom; List<KeyStep> draft = new();
    static readonly int[] HoldChoices = [20, 50, 100, 150, 200, 300, 500, 1000], PauseChoices = [0, 20, 50, 100, 150, 200, 300, 500, 1000];

    public KeyboardWorkbench(PcModeLink link, Action<Button> register, ControllerModel model, ControllerLayout? layout)
    {
        this.link = link; this.register = register;
        var available = (layout?.Controls.Select(r => r.Key) ?? model.FrontButtons.Keys).ToHashSet();
        inputs = MouseProfile.Inputs.Where(available.Contains).ToList();
        var page = new StackPanel { Margin = new(22, 6, 22, 26) };
        Body.Children.Add(new ScrollViewer { Content = page, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });

        page.Children.Add(Copy("Keyboard", 30, true));
        target.Foreground = BoardInk.Soft; page.Children.Add(target);

        // ---- the controller, each button saying what it presses
        board = new ControllerBoard(model, layout, inputs, register) { MaxHeight = 400 }; board.Picked += Select;
        var top = new StackPanel(); top.Children.Add(Caption("CONTROLLER", "Pick a button, then the key it should press"));
        top.Children.Add(board); page.Children.Add(Card(top, new(0, 12, 0, 0)));

        // ---- the picked button: a key, or a custom list
        var picked = new StackPanel();
        var line = new WrapPanel(); line.Children.Add(selectedName); selectedSays.VerticalAlignment = VerticalAlignment.Bottom; selectedSays.Margin = new(14, 0, 0, 6); line.Children.Add(selectedSays); picked.Children.Add(line);
        var modes = new WrapPanel { Margin = new(0, 8, 0, 0) };
        keyMode = Chip("One key", true, () => SetMode(false)); customMode = Chip("Custom: several keys", false, () => SetMode(true)); clear = Pill("Clear this button", ClearSelected);
        modes.Children.Add(keyMode); modes.Children.Add(customMode); modes.Children.Add(clear); picked.Children.Add(modes);
        var warning = new Border
        {
            CornerRadius = new(12), BorderThickness = new(1.5), Padding = new(16, 10, 16, 10), Margin = new(0, 10, 0, 8),
            BorderBrush = new SolidColorBrush(Color.FromArgb(200, 245, 180, 70)), Background = new SolidColorBrush(Color.FromArgb(40, 245, 180, 70)),
            Child = Copy("⚠  A custom button presses several keys from one press. Some games treat that as a macro and may ban you for it.", 15).Also(t => t.Foreground = new SolidColorBrush(Color.FromRgb(250, 214, 150))),
        };
        customPanel.Children.Add(warning);
        customPanel.Children.Add(Copy("Click keys or mouse buttons below to add them in order. Each is held, then the next waits.", 14));
        customPanel.Children.Add(customSteps);
        var customActions = new WrapPanel { Margin = new(0, 6, 0, 0) };
        saveCustom = Pill("Save custom button", SaveCustom); cancelCustom = Pill("Start over", () => { draft = Current().Custom.GetValueOrDefault(selection)?.Select(Copy).ToList() ?? new(); ShowDraft(); });
        customActions.Children.Add(saveCustom); customActions.Children.Add(cancelCustom); customPanel.Children.Add(customActions);
        picked.Children.Add(customPanel);
        page.Children.Add(Card(picked, new(0, 14, 0, 0)));

        // ---- the keyboard, then the mouse buttons
        keyboard = new KeyboardDiagram(register) { MaxHeight = 330, Margin = new(0, 4, 0, 0) }; keyboard.Picked += PickOutput;
        var keys = new StackPanel(); keys.Children.Add(Caption("KEYBOARD", "Green keys are pressed by a controller button, named on the key"));
        keys.Children.Add(keyboard);
        mouseChips.Children.Add(Copy("Mouse", 14, true).Also(t => { t.VerticalAlignment = VerticalAlignment.Center; t.Margin = new(0, 0, 12, 0); }));
        foreach (var (id, label) in KeyCatalog.Mouse) { string pick = id; mouseChips.Children.Add(Chip(label, false, () => PickOutput(pick))); }
        keys.Children.Add(mouseChips);
        page.Children.Add(Card(keys, new(0, 14, 0, 0)));

        // ---- what every button does
        var list = new StackPanel(); var head = new WrapPanel(); head.Children.Add(Copy("MAPPINGS", 13, true)); mappedCount.Margin = new(12, 4, 0, 4); head.Children.Add(mappedCount); list.Children.Add(head);
        list.Children.Add(listRows); page.Children.Add(Card(list, new(0, 14, 0, 0)));
        page.Children.Add(saved);

        PcModePage.ProfileChanged += Refresh;
        timer.Tick += (_, _) => Live();
        Body.Loaded += (_, _) => { PcModePage.ProfileChanged -= Refresh; PcModePage.ProfileChanged += Refresh; Refresh(); timer.Start(); }; Body.Unloaded += (_, _) => { timer.Stop(); PcModePage.ProfileChanged -= Refresh; };
        Select(inputs.Contains("A") ? "A" : inputs.FirstOrDefault() ?? "A");
    }

    PcModeProfile Current() => link.Store.Get(PcModePage.StoreKey);
    static KeyStep Copy(KeyStep s) => new() { Output = s.Output, HoldMs = s.HoldMs, PauseMs = s.PauseMs };

    public void Select(string key)
    {
        selection = key; board.Select(key);
        var p = Current();
        custom = p.Custom.ContainsKey(key);
        draft = p.Custom.GetValueOrDefault(key)?.Select(Copy).ToList() ?? new();
        Refresh();
    }

    void SetMode(bool on) { custom = on; if (on && draft.Count == 0) draft = Current().Custom.GetValueOrDefault(selection)?.Select(Copy).ToList() ?? new(); Refresh(); }

    void PickOutput(string id)
    {
        if (custom) { if (draft.Count < 64) draft.Add(new KeyStep { Output = id, HoldMs = 50, PauseMs = 50 }); ShowDraft(); return; }
        Save(p => { p.Keys[selection] = id; p.Custom.Remove(selection); }, $"{BoardInk.InputName(selection)} now presses {KeyCatalog.Label(id)}");
    }

    void SaveCustom()
    {
        if (draft.Count == 0) { saved.Text = "Add at least one key first"; return; }
        var steps = draft.Select(Copy).ToArray();
        Save(p => { p.Custom[selection] = steps; p.Keys.Remove(selection); }, $"{BoardInk.InputName(selection)} now plays {steps.Length} step{(steps.Length == 1 ? "" : "s")}");
    }

    void ClearSelected() { string key = selection; draft.Clear(); Save(p => { p.Keys.Remove(key); p.Custom.Remove(key); }, $"{BoardInk.InputName(key)} goes to the game again"); }

    void Save(Action<PcModeProfile> change, string done)
    {
        try
        {
            var p = Current(); change(p); p.Validate();
            if (!p.IsNeutral && !PcModePage.MaySave(link.Preferences)) { saved.Text = "Open PC mode's Status tab and read the notice first"; return; }
            link.Store.Save(PcModePage.StoreKey, p); link.Service.Reload(); saved.Text = done;
            PcModePage.Replaced(); // the other tabs reload, and so does this one
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException) { saved.Text = "Couldn't save: " + error.Message; }
        Refresh();
    }

    void Refresh()
    {
        var p = Current();
        target.Text = $"PC mode · Player {PcModePage.Player + 1} · " + (PcModePage.ProfileKey == PcModeStore.DefaultKey ? "every game" : p.Name ?? PcModePage.ProfileKey[PcModeStore.GamePrefix.Length..]) + " · The game doesn't see a button that presses a key";
        // the controller's pills and the keyboard's green keys
        keyboard.ClearAll();
        var byKey = new Dictionary<string, List<string>>();
        foreach (var input in inputs)
        {
            string name = BoardInk.ShortName(input);
            if (p.Keys.TryGetValue(input, out var id)) { board.Show(input, KeyCatalog.Label(id), true); (byKey.TryGetValue(id, out var l) ? l : byKey[id] = new()).Add(name); }
            else if (p.Custom.TryGetValue(input, out var steps)) { board.Show(input, "Custom · " + steps.Length, true); foreach (var s in steps.Select(s => s.Output).Distinct()) (byKey.TryGetValue(s, out var l) ? l : byKey[s] = new()).Add(name); }
            else if (p.Macros.ContainsKey(input)) board.Show(input, "Controller macro", true);
            else board.Show(input, "", false);
        }
        foreach (var (id, names) in byKey) keyboard.Show(id, names);
        foreach (Button chip in mouseChips.Children.OfType<Button>()) PaintChip(chip, false);
        int i = 0; foreach (var (id, _) in KeyCatalog.Mouse) { if (byKey.ContainsKey(id)) PaintChip(mouseChips.Children.OfType<Button>().ElementAt(i), true); i++; }

        // the picked button
        selectedName.Text = BoardInk.InputName(selection);
        selectedSays.Text = custom ? "plays a custom list of keys" : p.Keys.TryGetValue(selection, out var key) ? "presses " + KeyCatalog.Label(key) : p.Macros.ContainsKey(selection) ? "plays a controller macro (see the list below)" : "goes to the game. Click a key to change it:";
        PaintChip(keyMode, !custom); PaintChip(customMode, custom);
        clear.IsEnabled = p.Keys.ContainsKey(selection) || p.Custom.ContainsKey(selection);
        customPanel.Visibility = custom ? Visibility.Visible : Visibility.Collapsed;
        ShowDraft();
        ShowList(p);
    }

    void ShowDraft()
    {
        customSteps.Children.Clear();
        for (int i = 0; i < draft.Count; i++)
        {
            var step = draft[i]; int at = i;
            var row = new DockPanel { Margin = new(0, 4, 0, 0) };
            row.Children.Add(Copy($"{i + 1}.", 15, true).Also(t => { t.Width = 34; t.VerticalAlignment = VerticalAlignment.Center; }));
            row.Children.Add(Chip(KeyCatalog.Label(step.Output), true, () => { }).Also(b => b.IsHitTestVisible = false));
            row.Children.Add(Copy("held", 14).Also(t => { t.VerticalAlignment = VerticalAlignment.Center; t.Margin = new(6, 0, 6, 0); }));
            row.Children.Add(Ms(HoldChoices, step.HoldMs, v => step.HoldMs = v));
            row.Children.Add(Copy("then wait", 14).Also(t => { t.VerticalAlignment = VerticalAlignment.Center; t.Margin = new(12, 0, 6, 0); }));
            row.Children.Add(Ms(PauseChoices, step.PauseMs, v => step.PauseMs = v));
            var remove = Pill("Remove", () => { draft.RemoveAt(at); ShowDraft(); }); remove.Margin = new(12, 3, 0, 3); row.Children.Add(remove);
            customSteps.Children.Add(row);
        }
        if (draft.Count == 0) customSteps.Children.Add(Copy("No keys yet.", 14).Also(t => t.Margin = new(0, 6, 0, 0)));
        saveCustom.IsEnabled = draft.Count > 0;
    }

    void ShowList(PcModeProfile p)
    {
        listRows.Children.Clear(); int count = 0;
        void Add(string what, string does, Action remove)
        {
            count++;
            var row = new DockPanel { Margin = new(0, 3, 0, 3) };
            var x = Pill("Remove", remove); x.Height = 30; x.Padding = new(12, 0, 12, 0); DockPanel.SetDock(x, Dock.Right); row.Children.Add(x);
            row.Children.Add(Copy(what, 15, true).Also(t => { t.Width = 150; t.VerticalAlignment = VerticalAlignment.Center; }));
            row.Children.Add(Copy("→  " + does, 15).Also(t => { t.Foreground = BoardInk.MappedLine; t.VerticalAlignment = VerticalAlignment.Center; t.TextTrimming = TextTrimming.CharacterEllipsis; t.TextWrapping = TextWrapping.NoWrap; }));
            listRows.Children.Add(new Border { CornerRadius = new(10), Padding = new(14, 2, 6, 2), Margin = new(0, 2, 0, 2), Background = BoardInk.Solid(36, BoardInk.Green), BorderBrush = BoardInk.Solid(120, BoardInk.Green), BorderThickness = new(1), Child = row });
        }
        foreach (var (button, id) in p.Keys.OrderBy(k => inputs.IndexOf(k.Key))) { string b = button; Add(BoardInk.InputName(b), KeyCatalog.Label(id), () => Save(q => q.Keys.Remove(b), $"{BoardInk.InputName(b)} goes to the game again")); }
        foreach (var (button, steps) in p.Custom.OrderBy(k => inputs.IndexOf(k.Key)))
        { string b = button; Add(BoardInk.InputName(b), "Custom: " + string.Join(", ", steps.Select(s => KeyCatalog.Label(s.Output))), () => Save(q => q.Custom.Remove(b), $"{BoardInk.InputName(b)} goes to the game again")); }
        // made before the Keyboard tab: controller macros and combos still run, so they are listed here to see and remove
        foreach (var (button, steps) in p.Macros) { string b = button; Add(BoardInk.InputName(b), $"Controller macro, {steps.Length} step{(steps.Length == 1 ? "" : "s")}", () => Save(q => q.Macros.Remove(b), "Macro removed")); }
        foreach (var combo in p.Combos) { string id = combo.Id; Add(string.Join(" + ", combo.Buttons.Select(BoardInk.InputName)), "Combo · " + (ComboRule.IsAction(combo.Output) ? combo.Output[ComboRule.ActionPrefix.Length..] : BoardInk.InputName(combo.Output)), () => Save(q => q.Combos.RemoveAll(c => c.Id == id), "Combo removed")); }
        if (count == 0) listRows.Children.Add(Copy("Nothing mapped yet. Every button goes to the game.", 14));
        mappedCount.Text = count == 0 ? "" : $"{count} in use";
    }

    void Live()
    {
        if (link.Service.Latest is not { } snap) { board.Held(new HashSet<string>()); return; }
        var held = new HashSet<string>(); var pad = snap.Input;
        foreach (var (name, mask) in PadButtons.Mask) if ((pad.Buttons & mask) != 0) held.Add(name);
        if (pad.LeftTrigger > PadButtons.TriggerThreshold) held.Add("LT"); if (pad.RightTrigger > PadButtons.TriggerThreshold) held.Add("RT");
        board.Held(held);
    }

    ComboBox Ms(int[] choices, int value, Action<int> set)
    {
        var list = choices.Contains(value) ? choices : choices.Append(value).Order().ToArray();
        var box = new ComboBox { ItemsSource = list.Select(v => v + " ms").ToList(), SelectedIndex = Array.IndexOf(list, value), MinWidth = 100, MinHeight = 32, VerticalAlignment = VerticalAlignment.Center };
        box.SelectionChanged += (_, _) => { if (box.SelectedIndex >= 0) set(list[box.SelectedIndex]); };
        return box;
    }
    void PaintChip(Button b, bool on)
    {
        if (b.Content is not Border face) { b.Opacity = on ? 1 : .75; b.FontWeight = on ? FontWeights.SemiBold : FontWeights.Normal; return; }
        face.Background = on ? BoardInk.Solid(120, BoardInk.Green) : BoardInk.Idle; face.BorderBrush = on ? BoardInk.MappedLine : BoardInk.IdleLine; face.Tag = on;
    }
    Button Chip(string text, bool on, Action click)
    {
        var face = new Border { CornerRadius = new(17), BorderThickness = new(1.5), Padding = new(14, 6, 14, 7), Margin = new(0, 4, 8, 4), Child = new TextBlock { Text = text, FontFamily = DS.Display, FontSize = 14.5, Foreground = BoardInk.Text } };
        var b = new Button { Template = BoardInk.Bare, Content = face, Cursor = Cursors.Hand }; b.Click += (_, _) => click(); PaintChip(b, on);
        b.MouseEnter += (_, _) => face.BorderBrush = BoardInk.SelectedLine; b.MouseLeave += (_, _) => face.BorderBrush = face.Tag is true ? BoardInk.MappedLine : BoardInk.IdleLine; register(b); return b;
    }
    static StackPanel Caption(string title, string hint) { var s = new StackPanel { Margin = new(0, 0, 0, 6) }; s.Children.Add(Copy(title, 13, true)); s.Children.Add(Copy(hint, 12.5)); return s; }
    static TextBlock Copy(string text, double size, bool bold = false) => Kit.Text(text, size, bold ? "DS.Text" : "DS.TextSoft", bold ? FontWeights.SemiBold : FontWeights.Normal).Also(t => { t.TextWrapping = TextWrapping.Wrap; t.TextTrimming = TextTrimming.None; t.Margin = new(0, 3, 0, 3); });
    Button Pill(string text, Action click) { var b = new Button { Content = text, Style = DS.Style("ActionButton"), Height = 36, MinHeight = 0, Padding = new(16, 0, 16, 0), Margin = new(0, 3, 8, 3), HorizontalAlignment = HorizontalAlignment.Left }; b.Click += (_, _) => click(); register(b); return b; }
    static Border Card(UIElement content, Thickness margin) => new() { Margin = margin, Padding = new(18, 14, 18, 16), CornerRadius = new(16), BorderThickness = new(1), BorderBrush = new SolidColorBrush(ThemeCatalog.R(Color.FromArgb(50, 155, 194, 240))), Background = new SolidColorBrush(ThemeCatalog.R(Color.FromArgb(110, 8, 17, 38))), Child = content };
}
