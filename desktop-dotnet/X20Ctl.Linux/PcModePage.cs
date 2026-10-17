using Avalonia;
using Avalonia.Controls;
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
    string message = ""; bool confirmReset, details;

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

        var route = T.Label("Physical pad (evdev) → PC profile → Virtual Xbox 360 (uinput) → Game\n" + (engine.Active ? "Controller held exclusively while active" : "No active route") + "\n" + (PcEngine.CanCreateVirtualPad() ? "Virtual controller allowed · Processing " + (engine.Active ? "running" : "inactive") : "Virtual controller not allowed yet · see below"), 14.5, T.Soft);
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

        stack.Children.Add(Section("Profile", "Every game shares this profile for now. Per-game profiles come with game detection."));
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
        var ok = new Pill("I understand", Pill.Kinds.Ghost, () => { hub.MarkNoticeSeen(); message = "Thanks. You can switch PC mode on now."; }) { Padding = new Thickness(26, 12), HorizontalAlignment = HorizontalAlignment.Left };
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

    void Apply(PcModeProfile profile, string done)
    {
        string line = hub.Store_Save(profile);
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
