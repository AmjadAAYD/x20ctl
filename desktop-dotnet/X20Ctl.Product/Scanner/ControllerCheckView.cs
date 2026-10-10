using System.Net.Http;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
namespace X20Ctl.Product.Scanner;

/// <summary>
/// "Controller not working?" (Tools): the Controller Scanner 2.0.0 flow rebuilt as an X20CTL screen. It shows all four
/// XInput player slots live, draws the selected one, runs the 2.0.0 guided checks (verify A + LT + RT, then every
/// control with a 3-second countdown and its recording window), lists Windows interfaces, offers the focused Raw Input
/// fallback, and saves the same hashed local report to Documents\X20CTLInputReports. Additions over 2.0.0: the slot
/// that responds is picked for you while nothing is recorded yet, a progress strip, per-control result chips, plain
/// "what to try" help when no slot responds, and a live stick-offset readout. Read-only throughout.
/// </summary>
public sealed class ControllerCheckView : Grid
{
    private static readonly string[] Actions = ["neutral", "A", "B", "X", "Y", "LB", "RB", "dpad_up", "dpad_right", "dpad_down", "dpad_left",
        "dpad_up_right", "dpad_down_right", "dpad_down_left", "dpad_up_left", "Start", "Back", "L3", "R3", "LT", "RT", "left_stick", "right_stick", "rear_left", "rear_right", "turbo"];
    private static readonly string[] Models = ["Unknown", "X05", "X05 Pro", "X10", "X15", "D10", "Dune / D15", "X20 Pro", "X20"];
    private static readonly string[] Transports = ["Receiver", "USB cable", "Bluetooth", "Not sure"];
    private static readonly Color Green = Color.FromRgb(52, 211, 153), Amber = Color.FromRgb(245, 180, 91), Blue = Kit.Blue;

    // ---- the 2.0.0 engine state ----
    private readonly Action close;
    private readonly PressTracker[] trackers = [new(), new(), new(), new()];
    private readonly Stopwatch liveClock = Stopwatch.StartNew(), clock = new();
    private readonly DispatcherTimer timer = new(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(20) };
    private WindowsReader? reader; private Evidence evidence = new(); private string? active; private int index;
    private string? lastSaved; private bool dirty, closeArmed, autoPicked;
    private int slot; private string library = WindowsReader.Libraries[0];
    private Inventory inventory = new(); private bool inventoryRead;
    private readonly Dictionary<string, string> results = new();
    private readonly bool[] connected = new bool[4];

    // ---- interface ----
    private readonly SlotCard[] slots = new SlotCard[4];
    private readonly List<RadioButton> readers = new();
    private readonly Button start, fresh, verify, record, skip, save, detect, details, rawToggle;
    private readonly ComboBox model = Combo(Models, "Unknown"), transport = Combo(Transports, "Not sure");
    private readonly CheckBox serial = new() { Style = DS.Style("DS.Toggle"), VerticalAlignment = VerticalAlignment.Center, ToolTip = "Serial numbers are private identifiers; they are only read if you turn this on" };
    private readonly TextBlock liveTitle = Kit.Text("Live input", 18.5, "DS.Text", FontWeights.SemiBold), liveSubtitle = Kit.Text("Start the live check to read your controller", 14, "DS.TextSoft");
    private readonly TextBlock big = new() { FontFamily = DS.Display, FontSize = 40, FontWeight = FontWeights.Bold, Foreground = DS.Brush("DS.Text"), TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock prompt = Wrap("", 16, "DS.TextSoft"), outcome = Wrap("Read-only input. No settings, vibration, firmware changes or uploads. Sampling begins after READY and a three-second countdown.", 13.5, "DS.TextSoft");
    private readonly TextBlock inventoryText = Kit.Text("Windows devices not listed yet", 13.5, "DS.TextSoft");
    private readonly WrapPanel chips = new();
    private readonly Dictionary<string, Border> chipFor = new();
    private readonly StepStrip steps = new();
    private readonly Diagram diagram = new();
    private readonly Grid center = new(), noSignal = new(), rawPage = new() { Visibility = Visibility.Collapsed };
    private readonly Border overlay = new() { Visibility = Visibility.Collapsed };
    private readonly TextBlock fixTitle = Kit.Text("", 24, "DS.Text", FontWeights.Bold);

    // ---- Raw Input fallback ----
    private RawObserver? raw; private HwndSource? hook; private int rawReceived;
    private readonly CheckBox rawBytes = new() { Content = "Also keep the raw report bytes in the private results (optional)", Style = DS.Style("DS.Toggle") };
    private readonly TextBlock rawStatus = Wrap("Press Start, keep X20CTL focused, then press buttons and move sticks. Keyboard and mouse are excluded.", 15, "DS.TextSoft"), rawValues = Wrap("", 15, "DS.Text");
    private readonly Dictionary<string, Dictionary<int, double>> rawHolds = new();
    private readonly Stopwatch rawClock = Stopwatch.StartNew();
    private Button rawStart = null!;

    public ControllerCheckView(Action close)
    {
        this.close = close;
        Background = new SolidColorBrush(Color.FromRgb(5, 9, 20)); ClipToBounds = true;
        Children.Add(new FacetBackdrop());
        var page = new Grid { Margin = new(40, 0, 40, 28) }; Children.Add(page);
        page.RowDefinitions.Add(new() { Height = new(84) }); page.RowDefinitions.Add(new() { Height = new(64) }); page.RowDefinitions.Add(new()); page.RowDefinitions.Add(new() { Height = new(18) }); page.RowDefinitions.Add(new() { Height = GridLength.Auto });

        // title bar: this is its own app inside X20CTL
        var bar = new DockPanel { VerticalAlignment = VerticalAlignment.Center };
        var back = Action("", "Back to Tools", "DS.ActionSecondary"); back.Click += (_, _) => close(); DockPanel.SetDock(back, Dock.Right); bar.Children.Add(back);
        var safe = new Border { CornerRadius = new(16), Padding = new(14, 7, 16, 7), Margin = new(0, 0, 16, 0), VerticalAlignment = VerticalAlignment.Center, Background = new SolidColorBrush(Color.FromArgb(40, Green.R, Green.G, Green.B)), BorderBrush = new SolidColorBrush(Color.FromArgb(120, Green.R, Green.G, Green.B)), BorderThickness = new(1),
            Child = new StackPanel { Orientation = Orientation.Horizontal, Children = { Kit.Icon("", 14, "DS.Success").Also(i => i.Margin = new(0, 0, 9, 0)), Kit.Text("Read-only · nothing is changed or uploaded", 13.5, "DS.Text", FontWeights.SemiBold) } } };
        DockPanel.SetDock(safe, Dock.Right); bar.Children.Add(safe);
        var mark = new Image { Source = LiveController.Bitmap("Assets/brand.png"), Width = 34, Height = 34, Margin = new(0, 0, 14, 0) }; RenderOptions.SetBitmapScalingMode(mark, BitmapScalingMode.HighQuality);
        bar.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Children = { mark, Kit.Text("X20CTL", 22, "DS.Text", FontWeights.Bold), new Border { Width = 1, Height = 22, Background = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)), Margin = new(18, 0, 18, 0) },
            Kit.Text("Controller Check", 22, "DS.Text", FontWeights.SemiBold), Kit.Text("   Scanner 2.0.0 engine", 14, "DS.TextMuted") } });
        page.Children.Add(bar);
        Grid.SetRow(steps, 1); page.Children.Add(steps);

        var body = new Grid(); Grid.SetRow(body, 2); page.Children.Add(body);
        body.ColumnDefinitions.Add(new() { Width = new(370) }); body.ColumnDefinitions.Add(new() { Width = new(20) }); body.ColumnDefinitions.Add(new()); body.ColumnDefinitions.Add(new() { Width = new(20) }); body.ColumnDefinitions.Add(new() { Width = new(430) });

        // left: the four player slots, the Windows reader, start / new source
        var left = new DockPanel();
        var slotStack = new StackPanel();
        for (int i = 0; i < 4; i++) { int n = i; slots[i] = new SlotCard(i); slots[i].Click += (_, _) => PickSlot(n, false); slotStack.Children.Add(slots[i]); }
        var readerRow = new UniformGrid { Rows = 1 }; string group = "reader" + Guid.NewGuid().ToString("N");
        foreach (var (lib, label) in new[] { ("XInput1_4.dll", "XInput 1.4"), ("XInput9_1_0.dll", "9.1.0"), ("XInput1_3.dll", "1.3") })
        {
            var r = new RadioButton { Content = label, Style = DS.Style("DS.Segment"), GroupName = group, IsChecked = lib == library, ToolTip = "Windows input reader: " + lib }; r.Checked += (_, _) => library = lib; readers.Add(r); readerRow.Children.Add(r);
        }
        start = Action("", "Start check", "DS.ActionPrimary"); start.Click += (_, _) => StartMonitoring();
        fresh = Action("", "New source", "DS.ActionSecondary"); fresh.Click += (_, _) => NewSource(); fresh.ToolTip = "Save what was recorded, then start again with another slot or reader";
        var leftFoot = new StackPanel { Margin = new(0, 14, 0, 0), Children = {
            Kit.Text("WINDOWS READER", 12, "DS.TextMuted", FontWeights.SemiBold).Also(t => t.Margin = new(2, 0, 0, 8)),
            new Border { CornerRadius = new(12), Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)), BorderBrush = new SolidColorBrush(Color.FromArgb(34, 255, 255, 255)), BorderThickness = new(1), Padding = new(1), Child = readerRow },
            new Grid { Margin = new(0, 14, 0, 0), ColumnDefinitions = { new(), new() { Width = new(10) }, new() { Width = GridLength.Auto } }, Children = { start, fresh.Also(f => Grid.SetColumn(f, 2)) } } } };
        DockPanel.SetDock(leftFoot, Dock.Bottom); left.Children.Add(leftFoot); left.Children.Add(slotStack);
        body.Children.Add(Kit.Panel("Player slots", "Press A: the slot that lights up is yours", null, left));

        // centre: the live diagram, the "no signal" help, or the Raw Input page
        center.Children.Add(new Viewbox { Child = diagram, Stretch = Stretch.Uniform, Margin = new(0, 4, 0, 0) });
        BuildNoSignal(); center.Children.Add(noSignal);
        BuildRawPage(); center.Children.Add(rawPage);
        var centerPanel = Kit.Panel(null, null, null, new DockPanel { Children = { new StackPanel { Margin = new(0, 0, 0, 10), Children = { liveTitle, liveSubtitle } }.Also(s => DockPanel.SetDock(s, Dock.Top)), center } });
        Grid.SetColumn(centerPanel, 2); body.Children.Add(centerPanel);

        // right: guided check
        verify = Action("", "Confirm it's your controller", "DS.ActionPrimary"); verify.Click += (_, _) => Begin("preflight");
        verify.ToolTip = "Press and release A, then pull and release LT, then RT. That proves the check is reading the controller in your hands before it records anything.";
        record = Action("", "Record next control", "DS.ActionPrimary"); record.Click += (_, _) => { if (evidence.Verified && index < Actions.Length) Begin(Actions[index]); };
        skip = Action("", "Skip", "DS.ActionSecondary"); skip.Click += (_, _) => { if (active == null && index < Actions.Length) { evidence.Event("skipped", Actions[index]); results[Actions[index]] = "skipped"; index++; dirty = true; UpdateControls(); } };
        skip.ToolTip = "Skip a control your controller does not have";
        foreach (var a in Actions) { var chip = Chip(Friendly(a)); chipFor[a] = chip; chips.Children.Add(chip); }
        var guide = new DockPanel();
        var actions = new StackPanel { Margin = new(0, 16, 0, 0), Children = { verify, new Grid { Margin = new(0, 10, 0, 0), ColumnDefinitions = { new(), new() { Width = new(10) }, new() { Width = GridLength.Auto } }, Children = { record, skip.Also(s => Grid.SetColumn(s, 2)) } } } };
        var top = new StackPanel { Children = { big, prompt.Also(p => p.Margin = new(0, 6, 0, 0)), actions } };
        DockPanel.SetDock(top, Dock.Top); guide.Children.Add(top);
        DockPanel.SetDock(outcome, Dock.Bottom); outcome.Margin = new(0, 12, 0, 0); guide.Children.Add(outcome);
        guide.Children.Add(new ScrollViewer { Margin = new(0, 18, 0, 0), VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = new StackPanel { Children = { Kit.Text("EVERY CONTROL", 12, "DS.TextMuted", FontWeights.SemiBold).Also(t => t.Margin = new(2, 0, 0, 8)), chips } } });
        var right = Kit.Panel("Guided check", "Checks run on the slot you picked", null, guide); Grid.SetColumn(right, 4); body.Children.Add(right);

        // bottom: claims, Windows inventory, Raw Input, save
        detect = Action("", "List Windows devices", "DS.ActionSecondary"); detect.Click += async (_, _) => await Detect();
        details = Action("", "Details", "DS.ActionSecondary"); details.IsEnabled = false; details.Click += (_, _) => ShowDetails();
        rawToggle = Action("", "Raw Input fallback", "DS.ActionSecondary"); rawToggle.Click += (_, _) => ToggleRaw(); rawToggle.ToolTip = "If no player slot responds: read the controller through Windows Raw Input instead";
        save = Action("", "Save report", "DS.ActionPrimary"); save.Click += (_, _) => Save();
        var open = Action("", "Open reports", "DS.ActionSecondary"); open.Click += (_, _) => OpenReports();
        model.SelectionChanged += (_, _) => evidence.Model = (string)model.SelectedItem; transport.SelectionChanged += (_, _) => evidence.Transport = (string)transport.SelectedItem;
        var claims = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Children = {
            Labelled("PRINTED MODEL (YOUR CLAIM)", model), Labelled("CONNECTED BY", transport).Also(s => s.Margin = new(16, 0, 0, 0)) } };
        var inv = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new(28, 0, 0, 0), Children = { detect, details.Also(d => d.Margin = new(10, 0, 0, 0)), new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new(16, 0, 0, 0), Children = { inventoryText, new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 6, 0, 0), Children = { serial, Kit.Text("Include serial numbers (optional, private)", 12.5, "DS.TextMuted").Also(t => t.Margin = new(10, 0, 0, 0)) } } } } } };
        var savers = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Children = { rawToggle, open.Also(o => o.Margin = new(10, 0, 10, 0)), save } };
        var foot = new DockPanel(); DockPanel.SetDock(savers, Dock.Right); foot.Children.Add(savers); foot.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Children = { claims, inv } });
        var footPanel = Kit.Panel(null, null, null, foot, new(22, 14, 22, 14)); Grid.SetRow(footPanel, 4); page.Children.Add(footPanel);

        overlay.Background = new SolidColorBrush(Color.FromArgb(225, 3, 6, 14)); Children.Add(overlay);
        BuildWarning(); Children.Add(warning); Children.Add(resultsLayer);

        timer.Tick += (_, _) => TickInput();
        Loaded += (_, _) => { start.Focus(); if (DS.Motion) { Opacity = 0; BeginAnimation(OpacityProperty, DS.To(1, 240)); } };
        Unloaded += (_, _) => StopRaw();
        UpdateControls(); RefreshSlots();
    }

    // ================= engine flow (2.0.0 ScanForm, unchanged in behaviour) =================
    /// <summary>Start live check (the button; reviews call it too).</summary>
    public void StartMonitoring()
    {
        try
        {
            reader = new WindowsReader(library);
            evidence.Reader = reader.Identity; foreach (var r in readers) r.IsEnabled = false;
            evidence.XInputCapabilities.Clear(); for (int i = 0; i < 4; i++) evidence.XInputCapabilities.Add(reader.ReportedCapabilities(i));
            timer.Start();
            outcome.Text = "Reader READY. Source: documented Windows XInput. Watch live input and select the responsive slot. Monitoring alone is not saved.";
        }
        catch (Exception error) { outcome.Text = error.Message; }
        UpdateControls();
    }
    private void Begin(string action)
    {
        if (reader == null || active != null) return;
        if (!reader.Read(slot, out _)) { outcome.Text = "Selected slot is disconnected. Choose a responsive slot first."; return; }
        active = action; evidence.Slot = slot; evidence.Model = (string)model.SelectedItem; evidence.Transport = (string)transport.SelectedItem;
        if (action == "preflight") evidence.Verified = false;
        evidence.Records[action] = new(); dirty = true;
        clock.Restart(); UpdateControls();
    }
    private void TickInput()
    {
        if (reader == null) return;
        try
        {
            State selected = new(); bool selectedPresent = false;
            for (int i = 0; i < 4; i++)
            {
                bool present = reader.Read(i, out State state);
                var edges = present ? trackers[i].Update(state, liveClock.Elapsed.TotalMilliseconds) : new List<PressEdge>();
                if (!present) trackers[i].Disconnect();
                if (i == slot && active != null && clock.Elapsed.TotalSeconds >= 3)
                    foreach (var edge in edges)
                    {
                        evidence.Events.Add(new { timestamp = DateTime.UtcNow.ToString("o"), slot = i, action = active, control = edge.control, down = edge.down, duration_ms = edge.duration_ms });
                        if (scanMode && !edge.down && edge.control == active) holdErrors.Add(Math.Abs(edge.duration_ms - 2000));
                    }
                bool input = present && (state.Pad.Buttons != 0 || state.Pad.LT > 0 || state.Pad.RT > 0);
                slots[i].Show(present, input, trackers[i].HeldText, state, i == slot);
                connected[i] = present;
                // nothing recorded yet: the slot whose buttons move is the controller being checked
                if (input && i != slot && evidence.Records.Count == 0 && active == null && !connected[slot]) { PickSlot(i, true); }
                if (i == slot) { selected = state; selectedPresent = present; }
            }
            diagram.Show(selected, selectedPresent, trackers[slot], liveClock.Elapsed.TotalMilliseconds);
            noSignal.Visibility = selectedPresent || rawPage.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            liveSubtitle.Text = selectedPresent ? $"Slot {slot + 1} · {System.IO.Path.GetFileName(reader.Identity)}{(autoPicked ? " · picked because it responded" : "")}" : $"Slot {slot + 1} is not connected";
            if (!selectedPresent)
            {
                evidence.Verified = false;
                if (active != null) { evidence.Event("disconnected", active); results[active] = "inconclusive"; active = null; dirty = true; outcome.Text = "Source disconnected. Partial results retained; use New source and verify again."; }
                UpdateControls(); return;
            }
            if (active == null) { if (scanMode) ScanWaiting(); return; }
            double seconds = clock.Elapsed.TotalSeconds;
            prompt.Text = active == "preflight" ? "Release everything, press and release A, pull and release LT, then pull and release RT." :
                active.StartsWith("rear_") ? "Optional: press and release the " + active.Replace('_', ' ') + " button (M1/M2 if present). Skip if absent. This records ordinary output, not independent M keys." :
                active == "turbo" ? "Optional: hold a normal button with turbo already enabled using controls you know. No settings are changed. Skip if unavailable." :
                active == "neutral" ? "Keep your hands off the controller. This measures how far the sticks drift at rest." : active.EndsWith("stick") ? "Roll the " + active.Replace('_', ' ') + " slowly round its edge, twice, then let it centre." :
                scanMode && active is "LT" or "RT" ? $"Pull {active} slowly all the way in, hold it for 2 seconds, then let it out. Twice." :
                scanMode ? $"Press and hold {Friendly(active)} for 2 seconds, then let go. Do it twice." : "Press / pull and release " + Friendly(active) + " three times.";
            big.Text = seconds < 3 ? "READY · " + Math.Ceiling(3 - seconds) : "RECORDING · " + Math.Ceiling((active == "preflight" ? 23 : 11) - seconds) + " s";
            // the step-by-step scan shows the hold as it happens: "Holding B · 1.32 s", then "Held B · 2.04 s"
            if (scanMode && seconds >= 3 && (Analysis.Masks.ContainsKey(active) || active is "LT" or "RT"))
            {
                var held = trackers[slot].HeldFor(active, liveClock.Elapsed.TotalMilliseconds); var last = trackers[slot].LastFor(active);
                big.Text = held is { } h ? $"Holding {Friendly(active)} · {h / 1000:0.00} s" : last is { } l ? $"Held {Friendly(active)} · {l / 1000:0.00} s" : $"Press {Friendly(active)}… {Math.Ceiling(11 - seconds)} s";
                big.Foreground = new SolidColorBrush(held is { } hh && hh >= 2000 || held == null && last is >= 1800 ? Green : Kit.Ice);
            }
            big.Foreground = new SolidColorBrush(seconds < 3 ? Amber : Green);
            if (seconds < 3) return;
            evidence.Add(active, new Sample(active, evidence.Slot, selected, (seconds - 3) * 1000));
            if (seconds < (active == "preflight" ? 23 : 11)) return;
            string completed = active; active = null; clock.Stop();
            var rows = evidence.Records[completed]; var summary = Analysis.Summary(completed, rows);
            if (completed == "preflight")
            {
                evidence.Verified = Analysis.Verify(rows);
                outcome.Text = evidence.Verified ? "Source VERIFIED: A and both triggers pressed and released. You can record the controls now." :
                    "Source NOT verified. Check live values / selected slot. Save this failed check; use New source to compare another Windows reader.";
            }
            else
            {
                string status = (string)summary["status"]; results[completed] = status.Contains("observed") && !status.Contains("not_observed") && !status.StartsWith("inconclusive") ? "observed" : "inconclusive";
                index++; outcome.Text = Friendly(completed) + ": " + status.Replace('_', ' ') + ". " + (index < Actions.Length ? "Next: " + Friendly(Actions[index]) + (scanMode ? "." : ". Press Record when ready.") : "Input pass complete. Save the report.");
            }
            UpdateControls();
            if (scanMode) ScanNext(completed);
        }
        catch (Exception error)
        {
            timer.Stop(); evidence.Verified = false;
            if (active != null) { evidence.Event("failed", active + ": " + error.Message); active = null; dirty = true; }
            outcome.Text = "Read stopped: " + error.Message; UpdateControls();
        }
    }
    private void UpdateControls()
    {
        bool idle = active == null;
        start.IsEnabled = reader == null;
        verify.IsEnabled = reader != null && idle && !evidence.Records.ContainsKey("preflight");
        record.IsEnabled = skip.IsEnabled = reader != null && idle && evidence.Verified && index < Actions.Length;
        ((FrameworkElement)record.Parent).Visibility = evidence.Verified || evidence.Records.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        foreach (var s in slots) s.IsEnabled = idle && evidence.Records.Count == 0;
        model.IsEnabled = transport.IsEnabled = idle && evidence.Records.Count == 0;
        fresh.IsEnabled = reader != null && idle;
        save.IsEnabled = idle && (evidence.Records.Count > 0 || evidence.RawRecords.Count > 0);
        rawToggle.IsEnabled = idle;
        verify.Visibility = evidence.Verified || evidence.Records.ContainsKey("preflight") && evidence.Verified ? Visibility.Collapsed : Visibility.Visible;
        SetLabel(record, index < Actions.Length ? "Record " + Friendly(Actions[index]) : "All controls recorded");
        if (idle)
        {
            big.Foreground = DS.Brush("DS.Text");
            (big.Text, prompt.Text) = reader == null ? ("Let's check it", "Start the live check, then press A. Whatever lights up on the left is your controller.") :
                !evidence.Records.ContainsKey("preflight") ? ("Verify first", "Press Verify, release everything, then press A and pull both triggers once each. The full test unlocks after this works.") :
                !evidence.Verified ? ("Not verified", "The check didn't see A and both triggers on this slot. Save it, then try New source with another slot or Windows reader.") :
                index < Actions.Length ? ("Next: " + Friendly(Actions[index]), "Press Record, wait for the countdown, then follow the prompt. Skip controls your controller doesn't have.") :
                ("All done", "Every control has a result. Save the report to keep it, or share the ZIP if you need help.");
        }
        foreach (var (a, chip) in chipFor)
        {
            string state = a == active ? "active" : results.GetValueOrDefault(a, evidence.Verified && index < Actions.Length && Actions[index] == a ? "next" : "pending");
            Paint(chip, state);
        }
        steps.Show(reader == null ? 0 : !connected[slot] ? 1 : !evidence.Verified ? 2 : index < Actions.Length ? 3 : 4, lastSaved != null && !dirty);
    }
    private bool Save()
    {
        if (ReviewSandbox.Active) { dirty = false; outcome.Text = "Review sandbox: report not written."; UpdateControls(); return true; }
        try
        {
            string parent = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "X20CTLInputReports");
            Directory.CreateDirectory(parent); evidence.Inventory = inventory; lastSaved = evidence.Export(parent); dirty = false; outcome.Text = "Saved locally: " + lastSaved;
            UpdateControls(); return true;
        }
        catch (Exception error) { outcome.Text = "Save failed: " + error.Message; return false; }
    }
    private void NewSource()
    {
        if (dirty) { Save(); if (dirty) return; }
        timer.Stop(); reader?.Dispose(); reader = null;
        evidence = new Evidence { Inventory = inventory }; index = 0; results.Clear(); autoPicked = false; foreach (var r in readers) r.IsEnabled = true;
        outcome.Text = "Previous results retained" + (lastSaved == null ? "." : ": " + lastSaved) + " Choose a slot / Windows reader, then Start live check.";
        UpdateControls(); RefreshSlots();
    }
    /// <summary>Leaving keeps 2.0.0's protection: unsaved evidence is saved first; if that fails, a second Back leaves anyway.</summary>
    public bool TryClose()
    {
        if (active != null) { evidence.Event("cancelled", active); evidence.Verified = false; active = null; dirty = true; }
        if (dirty && !Save() && !closeArmed) { closeArmed = true; outcome.Text += " Press Back again to leave without saving."; return false; }
        return true;
    }
    public void Shutdown() { timer.Stop(); StopRaw(); reader?.Dispose(); reader = null; }

    private void PickSlot(int n, bool automatic)
    {
        if (n == slot || evidence.Records.Count > 0 || active != null) return;
        slot = n; evidence.Verified = false; autoPicked = automatic; RefreshSlots(); UpdateControls();
    }
    private void RefreshSlots() { for (int i = 0; i < 4; i++) slots[i].Selected = i == slot; }

    // ================= the full step-by-step scan (owner direction 10 Oct 2026) =================
    private bool scanMode, sendReport, scanQueued, finishing, probing, probeOwnsRaw;
    private readonly List<double> holdErrors = new();
    private readonly Dictionary<string, double> reportRates = new();
    private readonly Dictionary<string, Queue<double>> rateWindow = new();
    private readonly Stopwatch rateClock = new();
    private readonly Border warning = new(), resultsLayer = new() { Visibility = Visibility.Collapsed };
    public bool ScanRunning => scanMode && !finishing;
    private static bool Excluded(string action) => action is "rear_left" or "rear_right" or "turbo";

    private void BuildWarning()
    {
        warning.Background = new SolidColorBrush(Color.FromArgb(246, 5, 9, 20));
        var cards = new UniformGrid { Columns = 2, Margin = new(-8, 22, -8, 0) };
        foreach (var (title, text) in ScanFlow.Expectations)
            cards.Children.Add(new Border { CornerRadius = new(14), Margin = new(8), Padding = new(18, 14, 18, 14), BorderThickness = new(1), BorderBrush = new SolidColorBrush(Color.FromArgb(46, 140, 180, 255)),
                Background = new LinearGradientBrush(Color.FromArgb(160, 20, 34, 70), Color.FromArgb(120, 10, 18, 40), 90),
                Child = new StackPanel { Children = { Kit.Text(title, 16.5, "DS.Text", FontWeights.SemiBold), Wrap(text, 14, "DS.TextSoft").Also(t => t.Margin = new(0, 6, 0, 0)) } } });
        var send = Action("", "Scan and send the report", "DS.ActionPrimary"); send.Click += (_, _) => StartScan(true);
        var local = Action("", "Scan, keep it on this PC", "DS.ActionSecondary"); local.Click += (_, _) => StartScan(false);
        var watch = Action("", "Just watch live input", "DS.ActionSecondary"); watch.Click += (_, _) => Fx.Leave(warning, () => warning.Visibility = Visibility.Collapsed, 1.02, 200);
        var leave = Action("", "Back to Tools", "DS.ActionSecondary"); leave.Click += (_, _) => close();
        // which system is the controller on? This app scans on Windows; a Linux PC gets the Linux Controller Check
        var choices = new WrapPanel { Margin = new(0, 22, 0, 0), Children = { send, local.Also(b => b.Margin = new(12, 0, 0, 0)), watch.Also(b => b.Margin = new(12, 0, 0, 0)), leave.Also(b => b.Margin = new(12, 0, 0, 0)) } };
        var linux = BuildLinuxPanel(); linux.Visibility = Visibility.Collapsed;
        var systems = new UniformGrid { Rows = 1, Width = 460 }; string group = "os" + Guid.NewGuid().ToString("N");
        foreach (var (label, isLinux) in new[] { ("This Windows PC", false), ("A Linux PC", true) })
        {
            var r = new RadioButton { Content = label, Style = DS.Style("DS.Segment"), GroupName = group, IsChecked = !isLinux };
            r.Checked += (_, _) => { linux.Visibility = isLinux ? Visibility.Visible : Visibility.Collapsed; choices.Visibility = isLinux ? Visibility.Collapsed : Visibility.Visible; };
            systems.Children.Add(r);
        }
        var systemRow = new StackPanel { Margin = new(0, 20, 0, 0), Children = { Kit.Text("WHICH SYSTEM IS THE CONTROLLER CONNECTED TO?", 12, "DS.TextMuted", FontWeights.SemiBold).Also(t => t.Margin = new(2, 0, 0, 8)),
            new Border { CornerRadius = new(12), Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)), BorderBrush = new SolidColorBrush(Color.FromArgb(34, 255, 255, 255)), BorderThickness = new(1), Padding = new(1), HorizontalAlignment = HorizontalAlignment.Left, Child = systems } } };
        var panel = new StackPanel { MaxWidth = 1180, HorizontalAlignment = HorizontalAlignment.Center, Margin = new(40, 36, 40, 30), Children = {
            Kit.Text("Before you scan", 40, "DS.Text", FontWeights.Bold),
            Wrap("This is a full controller scan: you'll be guided through every button, stick and trigger, one step at a time, while X20CTL records exactly what the controller reports. Here is what it does and what to expect.", 17, "DS.TextSoft").Also(t => t.Margin = new(0, 8, 0, 0)),
            systemRow,
            cards,
            new Border { CornerRadius = new(14), Margin = new(0, 18, 0, 0), Padding = new(18, 14, 18, 14), BorderThickness = new(1), BorderBrush = new SolidColorBrush(Color.FromArgb(110, 61, 139, 255)), Background = new SolidColorBrush(Color.FromArgb(40, 61, 139, 255)),
                Child = Wrap("Your report: the full ZIP is always saved to Documents\\X20CTLInputReports. \"Scan and send\" also sends a compact summary (controller names, IDs and results, no personal files) to X20CTLADMIN, the X20CTL team's report site, when the scan ends. You can send the ZIP to @" + ScanFlow.Discord + " on Discord too.", 14.5, "DS.Text") },
            choices, linux } };
        warning.Child = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }
    /// <summary>For a controller on a Linux PC: the bundled Linux Controller Check (tools/linux_controller_check), the same
    /// steps and report in a terminal, for every distro with Python 3.</summary>
    private FrameworkElement BuildLinuxPanel()
    {
        string folder = System.IO.Path.Combine(AppContext.BaseDirectory, "Tools", "linux");
        const string command = "python3 x20ctl-check.py";
        var open = Action("\uE838", "Open the Linux scanner", "DS.ActionPrimary"); open.Click += (_, _) => { if (ReviewSandbox.Active) return; try { Process.Start(new ProcessStartInfo("explorer.exe", "\"" + folder + "\"") { UseShellExecute = true }); } catch (System.ComponentModel.Win32Exception) { } };
        var copy = Action("\uE8C8", "Copy the command", "DS.ActionSecondary"); copy.Click += (_, _) => { try { Clipboard.SetText(command); SetLabel(copy, "Copied"); } catch (System.Runtime.InteropServices.ExternalException) { } };
        var back = Action("\uE72B", "Back to Tools", "DS.ActionSecondary"); back.Click += (_, _) => close();
        Border Step(string n, string title, string text) => new() { CornerRadius = new(14), Margin = new(0, 0, 0, 10), Padding = new(18, 12, 18, 12), BorderThickness = new(1), BorderBrush = new SolidColorBrush(Color.FromArgb(46, 140, 180, 255)), Background = new SolidColorBrush(Color.FromArgb(70, 16, 28, 58)),
            Child = new DockPanel { Children = { new Grid { Width = 30, Height = 30, Margin = new(0, 0, 14, 0), VerticalAlignment = VerticalAlignment.Top, Children = { new Ellipse { Fill = new SolidColorBrush(Kit.Blue) }, Kit.Text(n, 14, "DS.Text", FontWeights.Bold).Also(t => t.HorizontalAlignment = HorizontalAlignment.Center) } }.Also(g => DockPanel.SetDock(g, Dock.Left)),
                new StackPanel { Children = { Kit.Text(title, 16.5, "DS.Text", FontWeights.SemiBold), Wrap(text, 14, "DS.TextSoft").Also(t => t.Margin = new(0, 4, 0, 0)) } } } } };
        return new StackPanel { Margin = new(0, 22, 0, 0), Children = {
            Step("1", "Copy the Linux Controller Check to the Linux PC", "It is one file, x20ctl-check.py, in the folder \"Open the Linux scanner\" shows. A USB stick or any cloud drive works."),
            Step("2", "Run it in a terminal there", "Open a terminal in that folder and run:   " + command + "   If it says it has no permission to read the controller, run it with sudo, or add yourself to the input group."),
            Step("3", "Follow the same steps", "It asks the same questions, runs the same hold-and-release steps, saves the ZIP in ~/X20CTLInputReports, and can send the summary to X20CTLADMIN. Send the ZIP to @" + ScanFlow.Discord + " on Discord."),
            Wrap("Works on Ubuntu, Debian, Mint, Pop!_OS, Fedora, Nobara, openSUSE, Arch, Garuda, Manjaro, EndeavourOS, SteamOS and other distros with Python 3, which they ship by default. Nothing else to install.", 13.5, "DS.TextMuted").Also(t => t.Margin = new(2, 4, 0, 16)),
            new WrapPanel { Children = { open, copy.Also(b => b.Margin = new(12, 0, 0, 0)), back.Also(b => b.Margin = new(12, 0, 0, 0)) } } } };
    }
    private void StartScan(bool send)
    {
        scanMode = true; sendReport = send; Fx.Leave(warning, () => warning.Visibility = Visibility.Collapsed, 1.02, 200);
        if (reader == null) StartMonitoring();
        _ = Detect();
        outcome.Text = "Scan started. Press A on the controller you want to scan; its slot is picked for you, then the steps run by themselves.";
        UpdateControls();
    }
    /// <summary>Between steps: once the controller answers, verify it; everything after runs on from ScanNext.</summary>
    private void ScanWaiting()
    {
        if (scanQueued || finishing || evidence.Records.ContainsKey("preflight") || !connected[slot]) return;
        Queue(() => Begin("preflight"), 900);
    }
    private void Queue(Action step, int ms)
    {
        scanQueued = true; var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
        t.Tick += (_, _) => { t.Stop(); scanQueued = false; if (scanMode && !finishing && active == null && reader != null) step(); };
        t.Start();
    }
    private void ScanNext(string completed)
    {
        if (completed is "left_stick" or "right_stick") StopRateProbe();
        if (completed == "preflight" && !evidence.Verified) { big.Text = "Not verified"; prompt.Text = "The scan didn't see A and both triggers on this slot. Press New source to try again, or another Windows reader."; return; }
        // macro paddles and turbo are left out by design: paddles send ordinary buttons, turbo is a firmware script
        while (index < Actions.Length && Excluded(Actions[index])) { evidence.Event("skipped", Actions[index] + ": excluded by design"); results[Actions[index]] = "skipped"; index++; }
        UpdateControls();
        if (index < Actions.Length) { string next = Actions[index]; Queue(() => { if (next is "left_stick" or "right_stick") StartRateProbe(); Begin(next); }, 1300); }
        else _ = FinishScan();
    }
    private void StartRateProbe()
    {
        probeOwnsRaw = raw == null; if (probeOwnsRaw) StartRaw();
        rateWindow.Clear(); rateClock.Restart(); probing = raw != null;
    }
    private void StopRateProbe() { probing = false; if (probeOwnsRaw) StopRaw(); probeOwnsRaw = false; }
    private async Task FinishScan()
    {
        if (finishing) return; finishing = true;
        big.Text = "Finishing up"; prompt.Text = "Reading the battery, comparing identities and saving the report…";
        var battery = reader?.Battery(slot) ?? ("unavailable", "unavailable", false);
        if (!inventoryRead) await Detect();
        var findings = ScanFlow.Interpret(evidence, inventory, battery, reportRates, holdErrors.Count > 0 ? holdErrors.Average() : -1);
        evidence.Extra["scan-summary.json"] = JsonSerializer.Serialize(new { findings, battery = new { battery.Item1, battery.Item2 }, reportRatesPerSecond = reportRates,
            excludedByDesign = new[] { "RGB lighting (firmware)", "macro paddles (send ordinary buttons)", "gyro", "turbo (firmware script)" } }, new JsonSerializerOptions(Evidence.Json) { WriteIndented = true });
        bool saved = Save();
        string? receipt = null, failure = null;
        if (sendReport)
        {
            prompt.Text = "Sending the summary to X20CTLADMIN…";
            try { receipt = await ScanFlow.Upload(ScanFlow.CompactReport((string)model.SelectedItem, (string)transport.SelectedItem, evidence, inventory, findings), (string)model.SelectedItem, (string)transport.SelectedItem); evidence.Event("submitted", receipt); }
            catch (Exception error) when (error is HttpRequestException or TaskCanceledException or InvalidDataException or JsonException or IOException) { failure = error.Message; evidence.Event("submission_failed", error.Message); }
        }
        ShowResults(findings, saved, receipt, failure);
    }
    private void ShowResults(List<Finding> findings, bool saved, string? receipt, string? failure)
    {
        var list = new StackPanel();
        foreach (var f in findings)
            list.Children.Add(new Border { CornerRadius = new(12), Margin = new(0, 0, 0, 10), Padding = new(16, 12, 16, 12), BorderThickness = new(1), BorderBrush = new SolidColorBrush(Color.FromArgb(40, 140, 180, 255)), Background = new SolidColorBrush(Color.FromArgb(70, 16, 28, 58)),
                Child = new DockPanel { Children = {
                    Kit.Text(f.Confidence.ToUpperInvariant(), 11, "DS.TextMuted", FontWeights.SemiBold).Also(t => { DockPanel.SetDock(t, Dock.Right); t.VerticalAlignment = VerticalAlignment.Top; }),
                    new StackPanel { Children = { Kit.Text(f.Area, 13, "DS.TextSoft", FontWeights.SemiBold), Wrap(f.Value, 17, "DS.Text"), Wrap(f.Detail, 13, "DS.TextMuted").Also(t => t.Margin = new(0, 3, 0, 0)) } } } } });
        string status = !saved ? "The report couldn't be saved: " + outcome.Text : "Saved: " + lastSaved;
        string sent = receipt != null ? $"Sent to X20CTLADMIN · receipt {receipt}" : failure != null ? $"Not sent to X20CTLADMIN ({failure}). The ZIP is saved; you can share it by hand." : "Kept on this PC (not sent).";
        var copy = Action("", "Copy @" + ScanFlow.Discord, "DS.ActionSecondary"); copy.Click += (_, _) => { try { Clipboard.SetText(ScanFlow.Discord); SetLabel(copy, "Copied"); } catch (System.Runtime.InteropServices.ExternalException) { } };
        var open = Action("", "Open reports folder", "DS.ActionSecondary"); open.Click += (_, _) => OpenReports();
        var done = Action("", "Done", "DS.ActionPrimary"); done.Click += (_, _) => Fx.Leave(resultsLayer, () => resultsLayer.Visibility = Visibility.Collapsed, 1.02, 200);
        var side = new StackPanel { Width = 420, Margin = new(28, 0, 0, 0), Children = {
            Kit.Text("YOUR REPORT", 12, "DS.TextMuted", FontWeights.SemiBold), Wrap(status, 14, "DS.TextSoft").Also(t => t.Margin = new(0, 6, 0, 0)), Wrap(sent, 15, receipt != null ? "DS.Success" : "DS.Text").Also(t => t.Margin = new(0, 12, 0, 0)),
            new Border { CornerRadius = new(14), Margin = new(0, 20, 0, 0), Padding = new(18, 16, 18, 16), Background = new SolidColorBrush(Color.FromArgb(50, 88, 101, 242)), BorderBrush = new SolidColorBrush(Color.FromArgb(150, 88, 101, 242)), BorderThickness = new(1),
                Child = new StackPanel { Children = { Kit.Text("Send it on Discord", 18, "DS.Text", FontWeights.SemiBold), Wrap($"Send the report ZIP to @{ScanFlow.Discord} on Discord. It helps add full support for your controller.", 14.5, "DS.TextSoft").Also(t => t.Margin = new(0, 6, 0, 12)), copy } } },
            new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 20, 0, 0), Children = { open, done.Also(d => d.Margin = new(10, 0, 0, 0)) } } } };
        var body = new DockPanel { Margin = new(40, 32, 40, 30) };
        DockPanel.SetDock(side, Dock.Right); body.Children.Add(side);
        body.Children.Add(new DockPanel { Children = { new StackPanel { Margin = new(0, 0, 0, 16), Children = { Kit.Text("Scan complete", 40, "DS.Text", FontWeights.Bold), Wrap("Everything X20CTL could read from this controller, and how sure each reading is.", 16, "DS.TextSoft") } }.Also(s => DockPanel.SetDock(s, Dock.Top)),
            new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } } });
        resultsLayer.Background = new SolidColorBrush(Color.FromArgb(248, 5, 9, 20)); resultsLayer.Child = body; resultsLayer.Visibility = Visibility.Visible; Fx.Appear(resultsLayer, 1.03, 320);
        big.Text = "All done"; prompt.Text = "The scan is complete. The results are on screen and the report is saved."; scanMode = false; done.Focus();
    }

    // ================= Windows inventory =================
    private async Task Detect()
    {
        detect.IsEnabled = false; serial.IsEnabled = false; inventoryText.Text = "Inspecting HID, USB, SetupAPI and Raw Input controller interfaces…"; bool include = serial.IsChecked == true;
        try { inventory = await Inventory.Bound(Task.Run(() => WindowsInventory.Read(include)), 15000); }
        catch (Exception error) { inventory = new Inventory(); inventory.errors.Add(error.GetBaseException().Message); inventoryText.Text = "Windows inventory failed. Live testing still works."; }
        evidence.Inventory = inventory; inventoryRead = true; detect.IsEnabled = serial.IsEnabled = true; details.IsEnabled = true;
        var groups = Inventory.Group(inventory.interfaces);
        var names = groups.Select(g => (g.FirstOrDefault(x => x.usagePage == 1 && new[] { 4, 5, 8 }.Contains(x.usage)) ?? g[0]).product).Where(p => !string.IsNullOrWhiteSpace(p)).Distinct().Take(2).ToList();
        inventoryText.Text = $"{groups.Count} Windows group{(groups.Count == 1 ? "" : "s")}{(names.Count > 0 ? " · " + string.Join(", ", names) : "")} · {inventory.rawInput.Count} Raw Input · model unverified";
    }
    private void ShowDetails()
    {
        string json = JsonSerializer.Serialize(inventory, new JsonSerializerOptions(Evidence.Json) { WriteIndented = true });
        var text = new TextBox { Text = json, IsReadOnly = true, FontFamily = new FontFamily("Consolas"), FontSize = 13, TextWrapping = TextWrapping.Wrap, VerticalContentAlignment = VerticalAlignment.Top, Padding = new(14) };
        var shut = Action("", "Close", "DS.ActionSecondary"); shut.Click += (_, _) => overlay.Visibility = Visibility.Collapsed;
        var dock = new DockPanel { Margin = new(120, 70, 120, 70) };
        var head = new DockPanel { Margin = new(0, 0, 0, 16) }; DockPanel.SetDock(shut, Dock.Right); head.Children.Add(shut);
        head.Children.Add(new StackPanel { Children = { Kit.Text("Private Windows evidence", 26, "DS.Text", FontWeights.Bold), Kit.Text("Interface paths can contain identifiers. Review before sharing. Grouped by Windows container, not by name or VID/PID.", 14, "DS.TextSoft") } });
        DockPanel.SetDock(head, Dock.Top); dock.Children.Add(head); dock.Children.Add(text);
        overlay.Child = dock; overlay.Visibility = Visibility.Visible; shut.Focus();
    }
    private void OpenReports()
    {
        if (ReviewSandbox.Active) return;
        string parent = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "X20CTLInputReports");
        try { Directory.CreateDirectory(parent); Process.Start(new ProcessStartInfo("explorer.exe", "\"" + parent + "\"") { UseShellExecute = true }); } catch (Exception error) { outcome.Text = "Could not open the folder: " + error.Message; }
    }

    // ================= Raw Input fallback (focused, explicitly started, separate stream) =================
    private void BuildRawPage()
    {
        rawPage.Background = new SolidColorBrush(Color.FromArgb(235, 8, 14, 32));
        rawStart = Action("", "Start Raw Input check", "DS.ActionPrimary"); rawStart.HorizontalAlignment = HorizontalAlignment.Left;
        rawStart.Click += (_, _) => StartRaw();
        var shut = Action("", "Back to live input", "DS.ActionSecondary"); shut.HorizontalAlignment = HorizontalAlignment.Left; shut.Click += (_, _) => ToggleRaw();
        rawPage.Children.Add(new StackPanel { Margin = new(30, 26, 30, 20), Children = {
            Kit.Text("Raw Input fallback", 26, "DS.Text", FontWeights.Bold),
            Wrap("A separate Windows input stream for when no player slot responds. Numbered HID buttons come from the controller's own descriptor; they are not guessed as A, B, X or Y, and they don't prove independent rear buttons.", 15, "DS.TextSoft").Also(t => t.Margin = new(0, 8, 0, 14)),
            rawBytes, new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 18, 0, 18), Children = { rawStart, shut.Also(s => s.Margin = new(12, 0, 0, 0)) } },
            rawStatus, rawValues.Also(v => v.Margin = new(0, 14, 0, 0)) } });
    }
    private void ToggleRaw()
    {
        bool on = rawPage.Visibility != Visibility.Visible;
        if (on && active != null) return;
        if (!on) StopRaw();
        rawPage.Visibility = on ? Visibility.Visible : Visibility.Collapsed; noSignal.Visibility = Visibility.Collapsed;
        SetLabel(rawToggle, on ? "Back to live input" : "Raw Input fallback");
        if (on) { evidence.Model = (string)model.SelectedItem; evidence.Transport = (string)transport.SelectedItem; rawStart.Focus(); }
    }
    private void StartRaw()
    {
        if (raw != null || Window.GetWindow(this) is not Window window) return;
        try
        {
            hook = HwndSource.FromHwnd(new WindowInteropHelper(window).Handle); raw = new RawObserver(hook!.Handle); hook.AddHook(RawMessage);
            rawStart.IsEnabled = false; rawStatus.Text = "READY. Keep X20CTL focused, then press and release buttons and move the sticks. Results join the local report.";
            evidence.Event("raw_input_started", "Controller usage pages only; no XInput slot association");
        }
        catch (Exception error) { rawStatus.Text = error.Message; raw?.Dispose(); raw = null; }
    }
    private void StopRaw()
    {
        hook?.RemoveHook(RawMessage); hook = null; raw?.Dispose(); raw = null;
        if (rawStart != null) rawStart.IsEnabled = true;
        dirty = dirty || evidence.RawRecords.Count > 0; UpdateControls();
    }
    private IntPtr RawMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != 0xFF || raw == null) return IntPtr.Zero;
        try
        {
            foreach (var row in raw.Read(lParam, rawBytes.IsChecked == true))
            {
                rawReceived++;
                if (probing) { var q = rateWindow.TryGetValue(row.path, out var w) ? w : rateWindow[row.path] = new(); double t = rateClock.Elapsed.TotalMilliseconds; q.Enqueue(t); while (q.Count > 0 && t - q.Peek() > 1000) q.Dequeue(); reportRates[row.path] = Math.Max(reportRates.GetValueOrDefault(row.path), q.Count); }
                bool kept = evidence.AddRaw(new { timestamp = DateTime.UtcNow.ToString("o"), input = row });
                double now = rawClock.Elapsed.TotalMilliseconds; if (!rawHolds.ContainsKey(row.path)) rawHolds[row.path] = new();
                var held = rawHolds[row.path]; string last = "";
                foreach (int button in held.Keys.ToArray()) if (!row.buttons.Contains(button)) { last = "Button " + button + " released after " + (now - held[button]).ToString("0") + " ms"; held.Remove(button); }
                foreach (int button in row.buttons) if (!held.ContainsKey(button)) held[button] = now;
                rawValues.Text = "PRESSED: " + (held.Count == 0 ? "none" : string.Join("   ", held.Select(x => "Button " + x.Key + "  " + (now - x.Value).ToString("0") + " ms"))) + "\n" + last + "\nRaw usages: " + string.Join("   ", row.axes.Select(x => x.Key + " = " + x.Value));
                rawStatus.Text = rawReceived + " controller reports observed. " + (kept ? "" : "Capture limit reached; further reports are live only. ") + "Source: Windows Raw Input (separate from XInput).\nInterface: " + row.path;
            }
        }
        catch (Exception error) { rawStatus.Text = "Raw Input error: " + error.Message; }
        return IntPtr.Zero;
    }

    // ================= help when nothing responds =================
    private void BuildNoSignal()
    {
        noSignal.Background = new SolidColorBrush(Color.FromArgb(252, 9, 16, 36));
        var list = new StackPanel { Margin = new(36, 30, 36, 24), VerticalAlignment = VerticalAlignment.Center };
        list.Children.Add(new Grid { Width = 74, Height = 74, HorizontalAlignment = HorizontalAlignment.Left, Margin = new(0, 0, 0, 16), Children = {
            new Ellipse { Fill = new RadialGradientBrush(Color.FromArgb(80, Amber.R, Amber.G, Amber.B), Color.FromArgb(0, Amber.R, Amber.G, Amber.B)), Margin = new(-18) },
            new Ellipse { Stroke = new SolidColorBrush(Amber), StrokeThickness = 2, Fill = new SolidColorBrush(Color.FromArgb(40, Amber.R, Amber.G, Amber.B)) },
            Kit.Icon("", 30, "DS.Warn").Also(i => i.HorizontalAlignment = HorizontalAlignment.Center) } });
        fixTitle.Text = "No controller on this slot yet"; list.Children.Add(fixTitle);
        list.Children.Add(Wrap("Press A on the controller. If no slot lights up on the left, try these one at a time:", 16, "DS.TextSoft").Also(t => t.Margin = new(0, 6, 0, 14)));
        foreach (var (title, tip) in new[] {
            ("Wake it up", "Press the Home button. Many pads sleep after a few minutes."),
            ("Plug in directly", "Use a USB port on the PC itself, not a hub, and try another cable if you have one."),
            ("Check the mode", "On receivers and multi-mode pads, switch to the PC / XInput mode and re-pair."),
            ("Close other controller apps", "Steam Input, DS4Windows and similar tools can take the controller first."),
            ("Re-pair Bluetooth", "Remove the controller in Windows Bluetooth settings, then pair it again."),
            ("Still nothing?", "Try another Windows reader on the left, or the Raw Input fallback below, then save the report.") })
            list.Children.Add(new DockPanel { Margin = new(0, 0, 0, 10), Children = { new Ellipse { Width = 8, Height = 8, Fill = new SolidColorBrush(Kit.Ice), Margin = new(2, 8, 14, 0), VerticalAlignment = VerticalAlignment.Top }.Also(e => DockPanel.SetDock(e, Dock.Left)),
                new TextBlock { TextWrapping = TextWrapping.Wrap, FontFamily = DS.Body, FontSize = 15.5, Foreground = DS.Brush("DS.TextSoft"), Inlines = { new System.Windows.Documents.Run(title + (title.EndsWith('?') ? " " : ". ")) { FontWeight = FontWeights.SemiBold, Foreground = DS.Brush("DS.Text") }, new System.Windows.Documents.Run(tip) } } } });
        noSignal.Children.Add(new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
    }

    // ================= small pieces =================
    private static string Friendly(string action) => action switch
    {
        "neutral" => "Hands off", "dpad_up" => "D-pad ↑", "dpad_down" => "D-pad ↓", "dpad_left" => "D-pad ←", "dpad_right" => "D-pad →",
        "dpad_up_right" => "D-pad ↗", "dpad_down_right" => "D-pad ↘", "dpad_down_left" => "D-pad ↙", "dpad_up_left" => "D-pad ↖",
        "left_stick" => "Left stick", "right_stick" => "Right stick", "rear_left" => "Rear left", "rear_right" => "Rear right", "turbo" => "Turbo", "preflight" => "Verify",
        _ => action
    };
    private static Border Chip(string text) => new() { CornerRadius = new(14), Padding = new(12, 6, 12, 6), Margin = new(0, 0, 8, 8), BorderThickness = new(1), Child = Kit.Text(text, 13, "DS.Text", FontWeights.SemiBold) };
    private static void Paint(Border chip, string state)
    {
        var c = state switch { "observed" => Green, "inconclusive" => Amber, "active" => Blue, "next" => Kit.Ice, _ => Color.FromRgb(150, 165, 192) };
        byte fill = state switch { "observed" or "active" => 70, "inconclusive" => 55, "next" => 40, "skipped" => 10, _ => 18 };
        chip.Background = new SolidColorBrush(Color.FromArgb(fill, c.R, c.G, c.B));
        chip.BorderBrush = new SolidColorBrush(Color.FromArgb(state is "pending" or "skipped" ? (byte)40 : (byte)170, c.R, c.G, c.B));
        chip.Opacity = state == "skipped" ? .5 : 1;
        chip.Effect = state == "active" ? new DropShadowEffect { Color = Blue, BlurRadius = 16, ShadowDepth = 0, Opacity = .8 } : null;
    }
    private static TextBlock Wrap(string text, double size, string brush) => new() { Text = text, FontFamily = DS.Body, FontSize = size, Foreground = DS.Brush(brush), TextWrapping = TextWrapping.Wrap, LineHeight = size * 1.45 };
    private static ComboBox Combo(string[] items, string selected) { var c = new ComboBox { Width = 170, MinHeight = 40, FontSize = 14.5, Padding = new(12, 8, 12, 8) }; foreach (var i in items) c.Items.Add(i); c.SelectedItem = selected; return c; }
    private static StackPanel Labelled(string label, FrameworkElement control) => new() { Children = { Kit.Text(label, 11.5, "DS.TextMuted", FontWeights.SemiBold).Also(t => t.Margin = new(2, 0, 0, 6)), control } };
    private static Button Action(string icon, string text, string style)
    {
        var b = new Button { Style = DS.Style(style), Height = 44, MinHeight = 0, Padding = new(20, 0, 22, 0), FontSize = 15,
            Content = new StackPanel { Orientation = Orientation.Horizontal, Children = { Kit.Icon(icon, 14, "DS.Text").Also(i => i.Margin = new(0, 0, 10, 0)), Kit.Text(text, 15, "DS.Text", FontWeights.SemiBold) } } };
        return b;
    }
    private static void SetLabel(Button b, string text) { if (b.Content is StackPanel { Children.Count: 2 } s && s.Children[1] is TextBlock t) t.Text = text; }

    /// <summary>One player slot: connected or not, a dot that lights on input, what is held, and both triggers.</summary>
    private sealed class SlotCard : Button
    {
        private readonly Border face = new() { CornerRadius = new(14), BorderThickness = new(1.2), Padding = new(14, 8, 14, 9), Margin = new(0, 0, 0, 8) };
        private readonly TextBlock status = Kit.Text("Not started", 15, "DS.Text", FontWeights.SemiBold), held = Kit.Text("", 13, "DS.TextSoft");
        private readonly Ellipse dot = new() { Width = 12, Height = 12 };
        private readonly Border lt = Bar(), rt = Bar();
        private bool selected, present, input;
        public bool Selected { get => selected; set { selected = value; Paint(); } }
        public SlotCard(int i)
        {
            Template = BareTemplate(); Cursor = Cursors.Hand; FocusVisualStyle = null;
            System.Windows.Automation.AutomationProperties.SetName(this, $"Player slot {i + 1}");
            var bars = new UniformGrid { Rows = 1, Margin = new(0, 6, 0, 0), Children = { Track("LT", lt), Track("RT", rt).Also(t => t.Margin = new(10, 0, 0, 0)) } };
            var headRow = new DockPanel { Children = { dot.Also(d => { DockPanel.SetDock(d, Dock.Right); d.VerticalAlignment = VerticalAlignment.Center; }), new StackPanel { Children = { Kit.Text($"PLAYER SLOT {i + 1}", 11.5, "DS.TextMuted", FontWeights.SemiBold), status } } } };
            face.Child = new StackPanel { Children = { headRow, held.Also(h => h.Margin = new(0, 4, 0, 0)), bars } };
            Content = face; Paint();
            MouseEnter += (_, _) => Paint(); MouseLeave += (_, _) => Paint(); GotKeyboardFocus += (_, _) => Paint(); LostKeyboardFocus += (_, _) => Paint();
        }
        public void Show(bool present, bool input, string heldText, State state, bool isSelected)
        {
            if (this.present != present || this.input != input) { this.present = present; this.input = input; Paint(); }
            status.Text = present ? input ? "Connected · responding" : "Connected" : "Not connected";
            held.Text = present ? "Held: " + heldText : "No signal on this slot";
            lt.Width = 120 * (present ? state.Pad.LT / 255.0 : 0); rt.Width = 120 * (present ? state.Pad.RT / 255.0 : 0);
        }
        private void Paint()
        {
            var accent = input ? Green : Kit.Blue;
            face.Background = input ? new SolidColorBrush(Color.FromArgb(60, Green.R, Green.G, Green.B)) : new SolidColorBrush(Color.FromArgb(selected ? (byte)40 : (byte)18, 120, 160, 230));
            face.BorderBrush = IsKeyboardFocused || IsMouseOver ? Brushes.White : selected ? new SolidColorBrush(Color.FromArgb(220, accent.R, accent.G, accent.B)) : new SolidColorBrush(Color.FromArgb(36, 255, 255, 255));
            face.Effect = selected ? new DropShadowEffect { Color = accent, BlurRadius = 20, ShadowDepth = 0, Opacity = .55 } : null;
            dot.Fill = new SolidColorBrush(!present ? Color.FromRgb(90, 100, 122) : input ? Green : Color.FromRgb(120, 175, 255));
            dot.Effect = present ? new DropShadowEffect { Color = input ? Green : Kit.Blue, BlurRadius = 12, ShadowDepth = 0, Opacity = .9 } : null;
            Opacity = IsEnabled || selected ? 1 : .7;
        }
        private static Border Bar() => new() { Height = 6, CornerRadius = new(3), HorizontalAlignment = HorizontalAlignment.Left, Background = new LinearGradientBrush(Color.FromRgb(90, 165, 255), Color.FromRgb(52, 211, 153), 0) };
        private static StackPanel Track(string label, Border bar) => new() { Children = { Kit.Text(label, 11, "DS.TextMuted", FontWeights.SemiBold), new Grid { Width = 120, HorizontalAlignment = HorizontalAlignment.Left, Margin = new(0, 4, 0, 0), Children = { new Border { Height = 6, CornerRadius = new(3), Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)) }, bar } } } };
    }

    /// <summary>The five stages, lit as the check moves along.</summary>
    private sealed class StepStrip : StackPanel
    {
        private readonly List<(Grid Disc, TextBlock Number, TextBlock Label)> items = new();
        public StepStrip()
        {
            Orientation = Orientation.Horizontal; VerticalAlignment = VerticalAlignment.Center;
            string[] names = ["Start the check", "Find your slot", "Confirm it's yours", "Test every control", "Save the report"];
            for (int i = 0; i < names.Length; i++)
            {
                var number = Kit.Text((i + 1).ToString(), 14, "DS.Text", FontWeights.Bold).Also(t => t.HorizontalAlignment = HorizontalAlignment.Center);
                var disc = new Grid { Width = 30, Height = 30, Children = { new Ellipse(), number } };
                var label = Kit.Text(names[i], 15, "DS.TextSoft", FontWeights.SemiBold); label.Margin = new(10, 0, 0, 0);
                Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Children = { disc, label } });
                if (i < names.Length - 1) Children.Add(new Border { Width = 46, Height = 2, CornerRadius = new(1), Margin = new(16, 0, 16, 0), Background = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)), VerticalAlignment = VerticalAlignment.Center });
                items.Add((disc, number, label));
            }
        }
        public void Show(int current, bool saved)
        {
            for (int i = 0; i < items.Count; i++)
            {
                bool done = i < current || saved && i == 4, now = i == current && !done;
                var (disc, number, label) = items[i];
                var ring = (Ellipse)disc.Children[0];
                ring.Fill = new SolidColorBrush(done ? Color.FromArgb(200, Green.R, Green.G, Green.B) : now ? Kit.Blue : Color.FromArgb(30, 255, 255, 255));
                ring.Stroke = new SolidColorBrush(done ? Green : now ? Color.FromRgb(160, 200, 255) : Color.FromArgb(70, 255, 255, 255)); ring.StrokeThickness = 1.4;
                ring.Effect = now ? new DropShadowEffect { Color = Kit.Blue, BlurRadius = 16, ShadowDepth = 0, Opacity = .8 } : null;
                number.Text = done ? "✓" : (i + 1).ToString();
                label.Foreground = DS.Brush(now || done ? "DS.Text" : "DS.TextMuted");
            }
        }
    }

    /// <summary>The selected slot drawn like a pad: triggers, bumpers, Back/Start, both sticks with their raw values and
    /// offset from centre, the D-pad and the face buttons. Every value is the raw XInput state; nothing is smoothed away.</summary>
    private sealed class Diagram : Canvas
    {
        private readonly Dictionary<string, Shape> lights = new();
        private readonly Dictionary<string, Color> tone = new() { ["A"] = Color.FromRgb(70, 220, 150), ["B"] = Color.FromRgb(255, 100, 125), ["X"] = Color.FromRgb(70, 170, 255), ["Y"] = Color.FromRgb(245, 205, 80) };
        private readonly Ellipse leftDot = Dot(), rightDot = Dot();
        private readonly TextBlock leftText = Small(), rightText = Small(), ltText = Small(), rtText = Small(), heldText = Small(), lastText = Small();
        private readonly Rectangle ltFill = new() { Height = 14, RadiusX = 7, RadiusY = 7 }, rtFill = new() { Height = 14, RadiusX = 7, RadiusY = 7 };
        private readonly Ellipse leftRing = new(), rightRing = new();
        private const double StickR = 62, Travel = 52;
        public Diagram()
        {
            Width = 760; Height = 470;
            // a soft pad silhouette behind everything
            Children.Add(new System.Windows.Shapes.Path { Data = Geometry.Parse("M150,96 C210,70 300,74 380,80 C460,74 550,70 610,96 C690,128 735,270 740,360 C745,430 700,462 655,452 C610,442 585,392 552,350 C520,320 470,318 380,318 C290,318 240,320 208,350 C175,392 150,442 105,452 C60,462 15,430 20,360 C25,270 70,128 150,96 Z"),
                Fill = new LinearGradientBrush(Color.FromArgb(70, 40, 60, 110), Color.FromArgb(25, 20, 30, 60), 90), Stroke = new SolidColorBrush(Color.FromArgb(70, 125, 182, 255)), StrokeThickness = 1.5 });
            Trigger("LT", 70, 10, ltFill, ltText); Trigger("RT", 470, 10, rtFill, rtText);
            Pill("LB", 120, 62, 120); Pill("RB", 520, 62, 120);
            Pill("Back", 300, 150, 70, "View"); Pill("Start", 390, 150, 70, "Menu");
            Stick("L3", 190, 205, leftRing, leftDot, leftText); Stick("R3", 470, 330, rightRing, rightDot, rightText);
            // D-pad: arms light for straight and diagonal presses
            foreach (var (key, x, y, w, h) in new[] { ("dpad_up", 274.0, 276.0, 32.0, 40.0), ("dpad_down", 274, 344, 32, 40), ("dpad_left", 236, 314, 40, 32), ("dpad_right", 304, 314, 40, 32) })
                Add(key, new Rectangle { Width = w, Height = h, RadiusX = 7, RadiusY = 7 }, x, y, Glyph(key));
            Add("dpad_center", new Rectangle { Width = 32, Height = 32, Fill = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)) }, 274, 314, null);
            foreach (var (key, x, y) in new[] { ("Y", 575.0, 150.0), ("X", 525, 200), ("B", 625, 200), ("A", 575, 250) }) Add(key, new Ellipse { Width = 46, Height = 46, StrokeThickness = 2 }, x - 23, y - 23, key);
            Put(heldText, 40, 420); Put(lastText, 40, 442);
            foreach (var key in lights.Keys) Paint(key, false);
        }
        public void Show(State s, bool present, PressTracker tracker, double now)
        {
            foreach (var (key, mask) in Analysis.Masks)
            {
                if (key.Contains('_') && key.Count(c => c == '_') > 1) continue; // diagonals light both straight arms
                Paint(key, present && (s.Pad.Buttons & mask) == mask);
            }
            Paint("LB", present && (s.Pad.Buttons & 256) != 0);
            StickShow(leftDot, leftRing, leftText, present ? s.Pad.LX : (short)0, present ? s.Pad.LY : (short)0, 190, 205, present && (s.Pad.Buttons & 64) != 0, "Left");
            StickShow(rightDot, rightRing, rightText, present ? s.Pad.RX : (short)0, present ? s.Pad.RY : (short)0, 470, 330, present && (s.Pad.Buttons & 128) != 0, "Right");
            TriggerShow(ltFill, ltText, present ? s.Pad.LT : (byte)0, "LT", tracker, now, present); TriggerShow(rtFill, rtText, present ? s.Pad.RT : (byte)0, "RT", tracker, now, present);
            heldText.Text = present ? "Held: " + tracker.HeldText : "No signal";
            lastText.Text = present ? tracker.LastRelease : "";
        }
        private void StickShow(Ellipse dot, Ellipse ring, TextBlock text, short x, short y, double cx, double cy, bool click, string side)
        {
            double nx = x / 32768.0, ny = y / 32768.0;
            SetLeft(dot, cx + nx * Travel - 9); SetTop(dot, cy - ny * Travel - 9);
            ring.Stroke = new SolidColorBrush(click ? Green : Color.FromArgb(150, 125, 182, 255)); ring.StrokeThickness = click ? 3 : 1.6;
            ring.Effect = click ? new DropShadowEffect { Color = Green, BlurRadius = 18, ShadowDepth = 0, Opacity = .9 } : null;
            double offset = Math.Min(100, Math.Sqrt(nx * nx + ny * ny) * 100);
            text.Text = $"{side} {x}, {y}\n{nx:0.00}, {ny:0.00} · {offset:0.0}% from centre";
        }
        private static void TriggerShow(Rectangle fill, TextBlock text, byte value, string key, PressTracker tracker, double now, bool present)
        {
            fill.Width = Math.Max(0, 220 * value / 255.0);
            text.Text = $"{key}  {value} / 255 · {100 * value / 255}%   {(present ? tracker.Duration(key, now) : "No signal")}";
        }
        private void Trigger(string key, double x, double y, Rectangle fill, TextBlock text)
        {
            Children.Add(new Rectangle { Width = 220, Height = 14, RadiusX = 7, RadiusY = 7, Fill = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)) }.Also(r => { SetLeft(r, x); SetTop(r, y + 22); }));
            fill.Fill = new LinearGradientBrush(Color.FromRgb(90, 165, 255), Color.FromRgb(52, 211, 153), 0); SetLeft(fill, x); SetTop(fill, y + 22); Children.Add(fill);
            Put(text, x, y);
        }
        private void Pill(string key, double x, double y, double w, string? label = null) => Add(key, new Rectangle { Width = w, Height = 34, RadiusX = 17, RadiusY = 17, StrokeThickness = 1.5 }, x, y, label ?? key);
        private void Stick(string key, double cx, double cy, Ellipse ring, Ellipse dot, TextBlock text)
        {
            ring.Width = ring.Height = StickR * 2; ring.Fill = new SolidColorBrush(Color.FromArgb(30, 20, 40, 90)); SetLeft(ring, cx - StickR); SetTop(ring, cy - StickR); Children.Add(ring);
            // the Windows SDK deadzone, so a resting offset can be read against it
            double dz = Travel * 7849 / 32768.0;
            Children.Add(new Ellipse { Width = dz * 2, Height = dz * 2, Stroke = new SolidColorBrush(Color.FromArgb(70, 255, 255, 255)), StrokeDashArray = new() { 2, 3 }, ToolTip = "Windows default deadzone" }.Also(e => { SetLeft(e, cx - dz); SetTop(e, cy - dz); }));
            Children.Add(new Line { X1 = cx - StickR + 8, X2 = cx + StickR - 8, Y1 = cy, Y2 = cy, Stroke = new SolidColorBrush(Color.FromArgb(46, 140, 175, 255)) });
            Children.Add(new Line { X1 = cx, X2 = cx, Y1 = cy - StickR + 8, Y2 = cy + StickR - 8, Stroke = new SolidColorBrush(Color.FromArgb(46, 140, 175, 255)) });
            Children.Add(dot); Put(text, cx - 90, cy + StickR + 6); text.Width = 200; text.TextAlignment = TextAlignment.Center;
        }
        private void Add(string key, Shape shape, double x, double y, string? label)
        {
            SetLeft(shape, x); SetTop(shape, y); Children.Add(shape); lights[key] = shape;
            if (label == null) return;
            var t = new TextBlock { Text = label, FontFamily = DS.Display, FontSize = label.Length > 2 ? 13 : 16, FontWeight = FontWeights.Bold, Width = shape.Width, Height = shape.Height, TextAlignment = TextAlignment.Center, IsHitTestVisible = false,
                Padding = new(0, (shape.Height - (label.Length > 2 ? 17 : 21)) / 2, 0, 0), Foreground = new SolidColorBrush(tone.TryGetValue(key, out var c) ? c : Color.FromRgb(225, 232, 245)) };
            SetLeft(t, x); SetTop(t, y); Children.Add(t);
        }
        private void Paint(string key, bool on)
        {
            if (!lights.TryGetValue(key, out var shape) || key == "dpad_center") return;
            var c = tone.TryGetValue(key, out var face) ? face : Color.FromRgb(90, 165, 255);
            shape.Fill = on ? new SolidColorBrush(Color.FromArgb(220, c.R, c.G, c.B)) : new SolidColorBrush(Color.FromArgb(34, 255, 255, 255));
            shape.Stroke = new SolidColorBrush(on ? Colors.White : Color.FromArgb(tone.ContainsKey(key) ? (byte)170 : (byte)60, c.R, c.G, c.B));
            shape.Effect = on ? new DropShadowEffect { Color = c, BlurRadius = 20, ShadowDepth = 0, Opacity = .95 } : null;
        }
        private static string Glyph(string key) => key switch { "dpad_up" => "↑", "dpad_down" => "↓", "dpad_left" => "←", _ => "→" };
        private void Put(UIElement e, double x, double y) { SetLeft(e, x); SetTop(e, y); Children.Add(e); }
        private static Ellipse Dot() => new() { Width = 18, Height = 18, Fill = Brushes.White, Effect = new DropShadowEffect { Color = Kit.Blue, BlurRadius = 16, ShadowDepth = 0, Opacity = 1 } };
        private static TextBlock Small() => new() { FontFamily = DS.Body, FontSize = 13.5, Foreground = DS.Brush("DS.TextSoft") };
    }
    private static ControlTemplate BareTemplate() { var t = new ControlTemplate(typeof(Button)) { VisualTree = new FrameworkElementFactory(typeof(ContentPresenter)) }; t.Seal(); return t; }
}
