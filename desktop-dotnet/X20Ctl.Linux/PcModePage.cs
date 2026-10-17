using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using X20Ctl.Zone;
using X20Ctl.Zone.PcMode;
namespace X20Ctl.Linux;

/// <summary>Route the controller through the saved settings to a virtual Xbox controller. The switch, what the engine is doing, the notice that must be read
/// first, sharing and reset. The Buttons, Curves and Macros tabs are where the settings are changed.</summary>
internal sealed class PcModePage : UserControl
{
    readonly StudioState state; readonly Action<string> go; readonly PcModeHub hub = PcModeHub.Shared;
    string message = ""; bool confirmReset, details, picking, turbo; int Player => state.Slot;
    static readonly int[] TurboRates = [0, 5, 8, 10, 15, 20, 30];
    static readonly (string Id, string Label)[] TurboButtons = [("A", "A"), ("B", "B"), ("X", "X"), ("Y", "Y"), ("LB", "Left bumper"), ("RB", "Right bumper"), ("LT", "Left trigger"), ("RT", "Right trigger"), ("L3", "Left stick click"), ("R3", "Right stick click"), ("DPAD_UP", "D-pad up"), ("DPAD_DOWN", "D-pad down"), ("DPAD_LEFT", "D-pad left"), ("DPAD_RIGHT", "D-pad right"), ("SELECT", "View"), ("START", "Menu"), ("HOME", "Guide")];

    public PcModePage(StudioState state, Action<string> go)
    {
        this.state = state; this.go = go;
        hub.Changed += OnHub; DetachedFromVisualTree += (_, _) => hub.Changed -= OnHub;
        Build();
    }
    void OnHub() => Avalonia.Threading.Dispatcher.UIThread.Post(Build);

    void Build()
    {
        var stack = new StackPanel { Spacing = 0 };
        stack.Children.Add(T.Label("PC mode", 44, null, FontWeight.Bold));
        var sub = T.Label("Route gameplay input through your saved PC profile to a virtual Xbox controller. Nothing is written to hardware.", 16.5, T.Soft); sub.Margin = new Thickness(0, 6, 0, 18); stack.Children.Add(sub);

        var engine = hub.Engine; bool on = hub.Enabled && PcEngine.Supported;
        (string label, string detail, IBrush dot) = !PcEngine.Supported ? ("Not available", "PC mode needs Linux: it uses the kernel's evdev and uinput.", T.Warn)
            : !on ? ("Off", "PC mode is off. Your controller goes straight to the game.", T.B("#8A97B4"))
            : engine.Active ? ("Active", engine.Detail, T.B("#34D399"))
            : engine.Headline == "Needs attention" ? ("Needs attention", engine.Detail, T.Warn)
            : (engine.Headline.Length > 0 && engine.Headline != "Off" ? engine.Headline : "Starting", engine.Detail, T.B("#8A97B4"));
        var head = T.Line(12, new Border { Width = 13, Height = 13, CornerRadius = new CornerRadius(7), Background = dot, VerticalAlignment = VerticalAlignment.Center }, T.Label(label, 21, null, FontWeight.SemiBold));
        var detailText = T.Label(detail, 15, T.Soft); detailText.Margin = new Thickness(25, 0, 0, 0);
        stack.Children.Add(new Border { Background = T.B("#66060B18"), BorderBrush = T.Hairline, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(16), Padding = new Thickness(22, 18), Child = T.Stack(5, head, detailText) });

        var route = T.Label($"Physical pad {Player + 1} (evdev) → PC profile → Virtual Xbox 360 (uinput) → Game\n" + (engine.Routes.Any(r => r.Player == Player) ? "Controller held exclusively while active" : "No active route for this player") + "\n" + (PcEngine.CanCreateVirtualPad() ? "Virtual controller allowed · Processing " + (engine.Active ? "running" : "inactive") : "Virtual controller not allowed yet · see below"), 14.5, T.Soft);
        route.Margin = new Thickness(0, 16, 0, 0); stack.Children.Add(route);

        var toggle = Switch(on, v => { if (!hub.SetEnabled(v)) { message = "Read the notice below first."; } Build(); });
        var row = T.Line(18, toggle, T.Label("PC mode", 19, null, FontWeight.Medium)); row.Margin = new Thickness(0, 16, 0, 0); stack.Children.Add(row);
        if (message.Length > 0) { var m = T.Label(message, 14, T.Soft); m.Margin = new Thickness(0, 10, 0, 0); stack.Children.Add(m); }

        stack.Children.Add(Section("Where you change the settings", "Right here, in this controller's tabs. While PC mode is on, Buttons, Curves (with Advanced) and Macros apply on this PC, for any controller. Apply Changes saves them."));
        var edit = new Pill("Edit buttons", Pill.Kinds.Ghost, () => go("Buttons")) { Padding = new Thickness(22, 12), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 12, 0, 0) }; stack.Children.Add(edit);

        stack.Children.Add(Expander("Routing details", details, v => { details = v; Build(); }, T.Stack(10,
            Block("Input routing", "Physical pad → Buttons / Curves / Macros → Virtual Xbox output → Game. While it is active the physical pad is hidden from games; they see one Xbox controller."),
            Block("Reported controls", "Only inputs the pad exposes as an Xbox-layout gamepad can be mapped here. Independent rear buttons and motion data are not available."),
            Block("Bypass shortcut", "Hold View + Menu for one second to switch your settings off or back on without leaving PC mode."))));

        if (!hub.NoticeSeen) stack.Children.Add(Notice());
        else if (PcEngine.Supported && !PcEngine.CanCreateVirtualPad()) stack.Children.Add(Permission());

        stack.Children.Add(Section("Settings for", $"Player {Player + 1} · " + (GameApps.CanSeeForeground ? "A game's own settings apply while it is in front." : "A game's own settings apply while it is running (the window in front can't be told on this desktop).")));
        var items = new List<(string Key, string Label)> { (PcModeStore.DefaultKey, "Every game") }; items.AddRange(hub.Games(Player).Select(g => (g.Key, g.Name + "  ·  " + g.Exe)));
        var pick = new ComboBox { ItemsSource = items.Select(i => i.Label).ToList(), SelectedIndex = Math.Max(0, items.FindIndex(i => i.Key == hub.ProfileKey(Player))), MinWidth = 340, FontSize = 15 };
        pick.SelectionChanged += (_, _) => { string key = items[Math.Max(0, pick.SelectedIndex)].Key; if (key != hub.ProfileKey(Player)) { hub.Pick(Player, key); state.ResetAll(); hub.Prepare(state); message = key == PcModeStore.DefaultKey ? "Editing the settings every game gets." : "Editing this game's own settings, here and in the tabs."; } };
        var games = T.Line(12, pick, new Pill("Give a game its own settings", Pill.Kinds.Ghost, () => { picking = !picking; Build(); }) { Padding = new Thickness(20, 11) });
        if (hub.ProfileKey(Player) != PcModeStore.DefaultKey) games.Children.Add(new Pill("Remove this game", Pill.Kinds.Ghost, () => { message = hub.RemoveGame(Player); state.ResetAll(); hub.Prepare(state); Build(); }) { Padding = new Thickness(20, 11) });
        games.Margin = new Thickness(0, 12, 0, 0); stack.Children.Add(games);
        if (picking)
        {
            var running = GameApps.Running();
            var apps = new ComboBox { ItemsSource = running.Select(a => a.Title + "  ·  " + a.Exe).ToList(), SelectedIndex = running.Count > 0 ? 0 : -1, MinWidth = 420, FontSize = 14.5 };
            var add = new Pill("Add", Pill.Kinds.Primary, () => { if (apps.SelectedIndex < 0) return; var a = running[apps.SelectedIndex]; message = hub.AddGame(Player, a.Exe, a.Title); picking = false; state.ResetAll(); hub.Prepare(state); Build(); }) { Padding = new Thickness(22, 11) };
            stack.Children.Add(new Border { Margin = new Thickness(0, 12, 0, 0), Background = T.B("#33FFFFFF"), CornerRadius = new CornerRadius(14), Padding = new Thickness(18, 14), Child = T.Stack(10,
                T.Label(running.Count > 0 ? "Pick the game that is running." : "No game is running. Start it, come back to X20CTL, and press this again.", 14.5, T.Soft), T.Line(12, apps, add)) });
        }

        stack.Children.Add(Expander("Turbo bindings", turbo, v => { turbo = v; Build(); }, TurboGrid()));
        stack.Children.Add(Section("Share", "Only the settings go in, never anything about you or your PC."));
        var share = T.Wrap(10, new Pill("Save to a file", Pill.Kinds.Ghost, ExportFile) { Padding = new Thickness(20, 11) }, new Pill("Open a file", Pill.Kinds.Ghost, ImportFile) { Padding = new Thickness(20, 11) },
            new Pill("Copy as a code", Pill.Kinds.Ghost, CopyCode) { Padding = new Thickness(20, 11) }, new Pill("Paste a code", Pill.Kinds.Ghost, PasteCode) { Padding = new Thickness(20, 11) });
        share.Margin = new Thickness(0, 12, 0, 0); stack.Children.Add(share);

        if (confirmReset)
        {
            var yes = new Pill("Reset profile", Pill.Kinds.Primary, () => { confirmReset = false; Apply(new PcModeProfile(), "Reset. Every PC mode setting is cleared."); }) { Padding = new Thickness(22, 11) };
            var no = new Pill("Keep it", Pill.Kinds.Ghost, () => { confirmReset = false; Build(); }) { Padding = new Thickness(22, 11) };
            stack.Children.Add(new Border { Margin = new Thickness(0, 24, 0, 0), Background = T.B("#26F5B45B"), BorderBrush = T.B("#55F5B45B"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14), Padding = new Thickness(20, 16),
                Child = T.Stack(12, T.Label("Reset the current PC profile? This clears its buttons, curves, stick tools, macros and turbo. The controller itself is unchanged.", 14.5, T.Soft), T.Line(10, yes, no)) });
        }
        else stack.Children.Add(new Pill("Reset profile", Pill.Kinds.Ghost, () => { confirmReset = true; Build(); }) { Padding = new Thickness(22, 12), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 24, 0, 0) });
        stack.Children.Add(new Border { Height = 24 });

        Content = new ScrollViewer { Content = T.Card(stack, 34), Padding = new Thickness(0), Margin = new Thickness(36, 6, 36, 14) };
    }

    // ---------------------------------------------------------------------------------------------- pieces

    static Control Switch(bool on, Action<bool> set)
    {
        var knob = new Border { Width = 22, Height = 22, CornerRadius = new CornerRadius(11), Background = Brushes.White, HorizontalAlignment = on ? HorizontalAlignment.Right : HorizontalAlignment.Left, Margin = new Thickness(3), VerticalAlignment = VerticalAlignment.Center };
        var track = new Border { Width = 56, Height = 30, CornerRadius = new CornerRadius(15), Background = on ? T.PrimaryFill : T.B("#33FFFFFF"), BorderBrush = T.B("#55FFFFFF"), BorderThickness = new Thickness(1), Child = knob, Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand) };
        track.PointerPressed += (_, _) => set(!on); return track;
    }

    static Control Section(string title, string text)
    {
        var s = T.Stack(5, T.Label(title, 18, null, FontWeight.SemiBold), T.Label(text, 15, T.Soft)); s.Margin = new Thickness(0, 28, 0, 0); return s;
    }
    static Control Block(string title, string text) => T.Stack(2, T.Label(title, 15, null, FontWeight.SemiBold), T.Label(text, 14, T.Soft));

    static Control Expander(string title, bool open, Action<bool> set, Control body)
    {
        var header = T.Line(10, T.Label(open ? "⌄" : "›", 16, T.Soft), T.Label(title, 15.5, T.Text)); header.Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand);
        header.PointerPressed += (_, _) => set(!open);
        var s = T.Stack(10, header); if (open) { body.Margin = new Thickness(26, 0, 0, 0); s.Children.Add(body); }
        s.Margin = new Thickness(0, 18, 0, 0); return s;
    }

    Control Notice()
    {
        var ok = new Pill("I understand", Pill.Kinds.Ghost, () => { hub.MarkNoticeSeen(); hub.AutoFor(state.Model.Id); message = hub.Enabled ? "Thanks. PC mode is on for your X20." : "Thanks. You can switch PC mode on now."; }) { Padding = new Thickness(26, 12), HorizontalAlignment = HorizontalAlignment.Left };
        return new Border
        {
            Margin = new Thickness(0, 28, 0, 0), Background = T.B("#1FF5B45B"), BorderBrush = T.B("#55B8863A"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(18), Padding = new Thickness(26, 22),
            Child = T.Stack(10, T.Label("Before PC mode starts", 19, null, FontWeight.SemiBold),
                T.Label("1. Your settings only work while X20CTL is open. Close it, or if it crashes, and your controller goes back to normal.", 15, T.Soft),
                T.Label("2. Use turbo and macros with care in online games. Some games and anti-cheat systems treat them as cheating and may ban your account. You've been warned: you are responsible for the rules of the games you play, and X20CTL can't be held accountable for a ban.", 15, T.Soft),
                T.Label("3. It reads your controller exclusively and creates a virtual one, so Linux needs to let your user write /dev/uinput. It never changes permissions itself.", 15, T.Soft), ok)
        };
    }

    static Control Permission()
    {
        var rule = new TextBox { Text = $"echo '{PcEngine.UdevRule}' | sudo tee /etc/udev/rules.d/60-x20ctl-uinput.rules\nsudo udevadm control --reload-rules && sudo udevadm trigger", IsReadOnly = true, FontFamily = new FontFamily("monospace"), FontSize = 13, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Background = T.B("#66060B18"), Foreground = T.Text, BorderBrush = T.Hairline };
        return new Border
        {
            Margin = new Thickness(0, 28, 0, 0), Background = T.B("#1FF5B45B"), BorderBrush = T.B("#55B8863A"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(18), Padding = new Thickness(26, 20),
            Child = T.Stack(10, T.Label("Allow the virtual controller", 19, null, FontWeight.SemiBold), T.Label("/dev/uinput isn't writable for your user. Run this once, then unplug and replug the controller (or reboot):", 15, T.Soft), rule)
        };
    }

    // ---------------------------------------------------------------------------------------------- sharing

    Control TurboGrid()
    {
        var p = hub.Store.Get(hub.StoreKey(Player)); var grid = new UniformGrid { Columns = 3 };
        foreach (var (id, label) in TurboButtons)
        {
            var rate = new ComboBox { Width = 130, FontSize = 14.5, ItemsSource = TurboRates.Select(r => r == 0 ? "No turbo" : r + " / s").ToList(), SelectedIndex = Math.Max(0, Array.IndexOf(TurboRates, p.Turbo.GetValueOrDefault(id))) };
            string button = id;
            rate.SelectionChanged += (_, _) => { var next = hub.Build(state); int hz = TurboRates[Math.Max(0, rate.SelectedIndex)]; if (hz == 0) next.Turbo.Remove(button); else next.Turbo[button] = hz; if (next.Turbo.GetValueOrDefault(button) == hub.Store.Get(hub.StoreKey(Player)).Turbo.GetValueOrDefault(button)) return; message = hub.SaveTo(hub.StoreKey(Player), next); };
            grid.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 18, 10), Spacing = 12, Children = { new TextBlock { Text = label, Width = 130, VerticalAlignment = VerticalAlignment.Center, Foreground = T.Text, FontSize = 15 }, rate } });
        }
        return grid;
    }

    void Apply(PcModeProfile profile, string done)
    {
        string line = hub.SaveTo(hub.StoreKey(Player), profile);
        if (line.StartsWith("Saved") || line.StartsWith("Nothing"))
        {
            state.ResetAll(); hub.Prepare(state); message = done;
        }
        else message = line;
        Build();
    }

    async void ExportFile()
    {
        var top = TopLevel.GetTopLevel(this); if (top == null) return;
        var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title = "Save PC mode settings", SuggestedFileName = "settings.x20pc", DefaultExtension = "x20pc" });
        if (file == null) return;
        try { await using var stream = await file.OpenWriteAsync(); stream.SetLength(0); await stream.WriteAsync(System.Text.Encoding.UTF8.GetBytes(PcModeShare.ToJson(hub.Build(state)))); message = "Saved to " + file.Name + "."; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { message = "Couldn't save the file: " + e.Message; }
        Build();
    }

    async void ImportFile()
    {
        var top = TopLevel.GetTopLevel(this); if (top == null) return;
        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Open PC mode settings", AllowMultiple = false });
        if (files.Count == 0) return;
        try
        {
            await using var stream = await files[0].OpenReadAsync();
            if (stream.Length > 64 * 1024) throw new InvalidDataException("That file is too big to be PC mode settings.");
            using var reader = new StreamReader(stream); Use(PcModeShare.FromJson(await reader.ReadToEndAsync()), "file");
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException) { message = "Couldn't use that file: " + e.Message; Build(); }
    }

    async void CopyCode()
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clip) { message = "The clipboard is not available."; Build(); return; }
        await Avalonia.Input.Platform.ClipboardExtensions.SetTextAsync(clip, PcModeShare.ToCode(hub.Build(state)));
        message = "Code copied. Paste it wherever you share it; anyone can paste it into their PC mode."; Build();
    }

    async void PasteCode()
    {
        string? text = TopLevel.GetTopLevel(this)?.Clipboard is { } clip ? await Avalonia.Input.Platform.ClipboardExtensions.TryGetTextAsync(clip) : null;
        if (string.IsNullOrWhiteSpace(text)) { message = "Copy a code first, then choose Paste a code."; Build(); return; }
        try { Use(PcModeShare.FromCode(text), "code"); } catch (InvalidDataException e) { message = "Couldn't use that code: " + e.Message; Build(); }
    }

    void Use(PcModeProfile p, string from) => Apply(p, $"Settings from the {from} are in.");
}
