using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using X20Ctl.Zone.Agent;
using Sh = System.Windows.Shapes;

namespace X20Ctl.Product.Agent;

/// <summary>
/// The check-up runs on the agent's own screen (owner, 8 Nov 2026: "whatever happens in the agent must not happen in the
/// background"): it first shows what it can reach on the controller (live input, the settings stored on it, whether it may
/// write), then walks through the sticks at rest, each stick rolled round its edge, both triggers and every button, read
/// live while the Studio's own controller navigation is paused. Nothing is written to the controller.
/// </summary>
public partial class AgentPanel
{
    /// <summary>The check-up is reading the pad: the Studio must not act on its buttons meanwhile.</summary>
    public bool CapturesController => checkRunning;
    public event Action? CaptureChanged;

    /// <summary>A review feeds the check-up a pretend controller: the step's name and the seconds into it, to a reading.</summary>
    internal static Func<string, double, (short LX, short LY, short RX, short RY, byte LT, byte RT, ushort Buttons, uint Packet)?>? CheckInput;
    /// <summary>A review answers the stored-settings read instead of the real pad.</summary>
    internal static Func<Task<string?>>? SettingsProbe;

    enum CheckStep { Rest, Left, Right, Triggers, Buttons, Done }
    static readonly (ushort Flag, string Name)[] CheckButtons =
        [(0x1000, "A"), (0x2000, "B"), (0x4000, "X"), (0x8000, "Y"), (0x0100, "LB"), (0x0200, "RB"), (0x0020, "View"), (0x0010, "Menu"), (0x0040, "L3"), (0x0080, "R3"), (0x0001, "Up"), (0x0002, "Down"), (0x0004, "Left"), (0x0008, "Right")];

    readonly System.Windows.Threading.DispatcherTimer checkTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    bool checkRunning; CheckStep step; DateTime stepAt;
    X20Ctl.Product.Scanner.WindowsReader? checkReader; int checkSlot = -1;
    readonly RestReading restLeft = new(), restRight = new();
    readonly StickSweep sweepLeft = new(), sweepRight = new();
    double ltMax, rtMax; bool ltDone, rtDone; ushort pressedMask; uint lastPacket; int packets; double movingSeconds; DateTime lastTick;
    readonly List<string> skipped = [];

    // the screen: the menu (what it can reach, the ways to check) and the stage (a check in progress)
    StackPanel checkMenu = null!; Grid checkStage = null!;
    readonly StackPanel accessRows = new(); bool accessShown;
    TextBlock stageStep = null!, stageTitle = null!, stageSays = null!, stageProgress = null!;
    Grid stageVisual = null!; StackPanel stageActions = null!; StackPanel stageFindings = null!;
    Canvas stickCanvas = null!; Sh.Polyline stickTrail = null!; Sh.Ellipse stickDot = null!; readonly Sh.Path[] sectorMarks = new Sh.Path[AgentCheckup.Sectors];
    Border ltBar = null!, rtBar = null!; StackPanel triggerVisual = null!; WrapPanel buttonVisual = null!; readonly Dictionary<ushort, Border> buttonChips = [];
    const double StickBox = 210;

    /// <summary>The check-up page: what it can reach, then the ways to check; a check in progress takes the whole page.</summary>
    ScrollViewer BuildCheckup()
    {
        checkMenu = new StackPanel { Margin = new(0, 0, 8, 0) };
        checkMenu.Children.Add(Wrap("Controller check-up", 20, "DS.Text", 0).Also(t => t.FontWeight = FontWeights.SemiBold));
        checkMenu.Children.Add(Wrap("Everything happens here, on my screen. I read your controller live and write nothing to it.", 13.5, "DS.TextMuted", 4));

        // what it can reach
        var again = Small("Check again"); again.Click += async (_, _) => await ShowAccess(); register(again);
        var accessHead = new DockPanel { Margin = new(0, 14, 0, 6), Children = { again.Also(b => { DockPanel.SetDock(b, Dock.Right); b.Height = 26; }), Kit.Text("WHAT I CAN REACH", 12, "DS.TextMuted", FontWeights.SemiBold).Also(t => t.VerticalAlignment = VerticalAlignment.Center) } };
        checkMenu.Children.Add(accessHead);
        checkMenu.Children.Add(new Border { CornerRadius = new(12), Padding = new(14, 10, 14, 10), BorderThickness = new(1), BorderBrush = new SolidColorBrush(Line), Background = new SolidColorBrush(Card), Child = accessRows });

        // the guided check, and the symptoms that start it
        var start = PadKey(new Border
        {
            CornerRadius = new(12), Padding = new(14, 12, 14, 12), Margin = new(0, 12, 0, 8), BorderThickness = new(1.5), BorderBrush = new SolidColorBrush(Amber), Background = new SolidColorBrush(Card),
            Child = new DockPanel { Children = { Kit.Icon("", 22, "DS.Text").Also(i => { i.Margin = new(0, 0, 14, 0); i.VerticalAlignment = VerticalAlignment.Center; i.Foreground = new SolidColorBrush(Amber); }),
                new StackPanel { Children = { Kit.Text("Check it here", 15.5, "DS.Text", FontWeights.SemiBold), Wrap("Sticks at rest, a circle with each stick, both triggers, every button. About a minute, on this screen.", 12.5, "DS.TextMuted", 2) } } } },
        }, "Check it here", () => StartCheck());
        register(start); checkMenu.Children.Add(start);
        var symptoms = new WrapPanel { Margin = new(0, 2, 0, 4) };
        foreach (var (glyph, label) in new[] { ("", "A stick drifts"), ("", "A button doesn't register"), ("", "Triggers feel off"), ("", "Lag or stutter"), ("", "It won't connect"), ("", "Settings won't save") })
        {
            var look = new Border { CornerRadius = new(17), Height = 34, Padding = new(12, 0, 14, 0), Margin = new(0, 0, 8, 8), BorderThickness = new(1), BorderBrush = new SolidColorBrush(Line), Background = new SolidColorBrush(Card),
                Child = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Children = { Kit.Icon(glyph, 13, "DS.TextSoft").Also(i => i.Margin = new(0, 0, 8, 0)), Kit.Text(label, 13.5, "DS.TextSoft", FontWeights.SemiBold) } } };
            var chip = PadKey(look, label, () => Symptom(label)); register(chip); symptoms.Children.Add(chip);
        }
        checkMenu.Children.Add(symptoms);

        // the full tools stay, said for what they are: they leave the agent
        checkMenu.Children.Add(Kit.Text("FULL TOOLS · THESE PUT ME AWAY", 12, "DS.TextMuted", FontWeights.SemiBold).Also(t => t.Margin = new(0, 10, 0, 6)));
        var tools = new WrapPanel();
        foreach (var (label, page) in new[] { ("Read scan report", "ReadScan"), ("Write scan", "WriteScan") })
        {
            var b = Small(label); b.Margin = new(0, 0, 8, 0); b.Click += (_, _) => PageRequested?.Invoke(page); register(b); tools.Children.Add(b);
        }
        var describe = Small("Describe it yourself"); describe.Click += (_, _) => StartDiagnostic(); register(describe); tools.Children.Add(describe);
        checkMenu.Children.Add(tools);

        checkStage = BuildStage();
        checkTimer.Tick += (_, _) => CheckTick();
        return new ScrollViewer { Content = new Grid { Children = { checkMenu, checkStage } }, Visibility = Visibility.Collapsed, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    Grid BuildStage()
    {
        stageStep = Kit.Text("", 12, "DS.TextMuted", FontWeights.SemiBold);
        stageTitle = Kit.Text("", 20, "DS.Text", FontWeights.SemiBold).Also(t => t.Margin = new(0, 2, 0, 0));
        stageSays = Wrap("", 14, "DS.TextSoft", 4);
        stageProgress = Kit.Text("", 12.5, "DS.TextMuted").Also(t => t.Margin = new(0, 8, 0, 0));

        // a stick: its edge, 36 marks that light as the stick reaches them, the path it took, and where it is now
        stickCanvas = new Canvas { Width = StickBox, Height = StickBox, HorizontalAlignment = HorizontalAlignment.Left };
        stickCanvas.Children.Add(new Sh.Ellipse { Width = StickBox, Height = StickBox, Stroke = new SolidColorBrush(Line), StrokeThickness = 1.5, Fill = new SolidColorBrush(ThemeCatalog.R(Color.FromRgb(15, 18, 23))) });
        double c = StickBox / 2;
        for (int i = 0; i < AgentCheckup.Sectors; i++)
        {
            double a0 = -Math.PI + i * 2 * Math.PI / AgentCheckup.Sectors + .02, a1 = a0 + 2 * Math.PI / AgentCheckup.Sectors - .04;
            Point At(double a, double r) => new(c + r * Math.Cos(a), c - r * Math.Sin(a));
            var arc = new PathGeometry { Figures = { new PathFigure { StartPoint = At(a0, c - 6), Segments = { new ArcSegment(At(a1, c - 6), new Size(c - 6, c - 6), 0, false, SweepDirection.Counterclockwise, true) } } } };
            sectorMarks[i] = new Sh.Path { Data = arc, Stroke = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)), StrokeThickness = 6, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
            stickCanvas.Children.Add(sectorMarks[i]);
        }
        stickTrail = new Sh.Polyline { Stroke = new SolidColorBrush(Color.FromArgb(150, Amber.R, Amber.G, Amber.B)), StrokeThickness = 2, StrokeLineJoin = PenLineJoin.Round };
        stickCanvas.Children.Add(stickTrail);
        stickDot = new Sh.Ellipse { Width = 16, Height = 16, Fill = new SolidColorBrush(Amber), Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Amber, BlurRadius = 14, ShadowDepth = 0 } };
        Canvas.SetLeft(stickDot, c - 8); Canvas.SetTop(stickDot, c - 8); stickCanvas.Children.Add(stickDot);

        Border Bar(string label, out Border fill)
        {
            fill = new Border { Height = 0, VerticalAlignment = VerticalAlignment.Bottom, CornerRadius = new(6), Background = new SolidColorBrush(Amber) };
            return new Border { Child = new StackPanel { Margin = new(0, 0, 22, 0), Children =
            {
                new Border { Width = 34, Height = 180, CornerRadius = new(8), BorderThickness = new(1), BorderBrush = new SolidColorBrush(Line), Background = new SolidColorBrush(ThemeCatalog.R(Color.FromRgb(15, 18, 23))), Child = fill },
                Kit.Text(label, 13, "DS.TextSoft", FontWeights.SemiBold).Also(t => { t.HorizontalAlignment = HorizontalAlignment.Center; t.Margin = new(0, 6, 0, 0); }),
            } } };
        }
        triggerVisual = new StackPanel { Orientation = Orientation.Horizontal, Children = { Bar("LT", out ltBar), Bar("RT", out rtBar) } };
        buttonVisual = new WrapPanel { MaxWidth = 520, HorizontalAlignment = HorizontalAlignment.Left };
        foreach (var (flag, name) in CheckButtons)
        {
            var chip = new Border { MinWidth = 54, Height = 38, Padding = new(10, 0, 10, 0), Margin = new(0, 0, 8, 8), CornerRadius = new(10), BorderThickness = new(1), BorderBrush = new SolidColorBrush(Line), Background = new SolidColorBrush(Card),
                Child = Kit.Text(name, 14, "DS.TextSoft", FontWeights.SemiBold).Also(t => { t.HorizontalAlignment = HorizontalAlignment.Center; t.VerticalAlignment = VerticalAlignment.Center; }) };
            buttonChips[flag] = chip; buttonVisual.Children.Add(chip);
        }
        stageFindings = new StackPanel();
        stageVisual = new Grid { Margin = new(0, 14, 0, 0), MinHeight = StickBox, Children = { stickCanvas, triggerVisual, buttonVisual, stageFindings } };
        stageActions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 14, 0, 0) };
        return new Grid { Visibility = Visibility.Collapsed, Children = { new StackPanel { Margin = new(0, 0, 8, 0), Children = { stageStep, stageTitle, stageSays, stageVisual, stageProgress, stageActions } } } };
    }

    // ---- what it can reach ----
    sealed record AccessLine(bool Ok, string Text);
    async Task<List<AccessLine>> CheckAccess()
    {
        var lines = new List<AccessLine>();
        int slot = CheckInput != null ? 1 : await Task.Run(() => XInputDriftMeter.Slot(player));
        lines.Add(slot >= 0
            ? new(true, $"I can see your {padName} live (Windows controller slot {slot + 1}): every stick, trigger and button.")
            : new(false, $"I can't see your {padName} right now. Connect it by cable, Bluetooth or its dongle, in Xbox (XInput) mode."));
        if (FinishModel(modelId) == "x20_pro")
        {
            string? problem;
            try
            {
                if (SettingsProbe != null) problem = await SettingsProbe();
                else if (ReviewSandbox.Active) problem = "a review never touches the real controller";
                else
                {
                    var read = LightingBleClient.ReadConfigAsync();
                    if (await Task.WhenAny(read, Task.Delay(TimeSpan.FromSeconds(12))) == read) { await read; problem = null; }
                    else problem = "it didn't answer within 12 seconds";
                }
            }
            catch (Exception error) when (PadFailure(error)) { problem = error.Message; }
            lines.Add(problem == null
                ? new(true, "I can read the settings stored on it (mappings, curves, macros) over Bluetooth.")
                : new(false, $"I couldn't read the settings stored on it: {problem.TrimEnd('.')}. Is it paired over Bluetooth and awake?"));
        }
        else lines.Add(new(false, $"The app can't read the settings stored on a {padName} yet, so I work through PC mode on this PC."));
        if (ControllerWrites?.Invoke() == true)
            lines.Add(new(true, "I can save changes on the controller itself. I show you each one first and read it back after."));
        else if (link != null)
            lines.Add(new(link.Service.Enabled, link.Service.Enabled
                ? "My changes go to PC mode on this PC, which is on: games feel them straight away. The controller's own memory isn't written while PC mode is on."
                : "My changes go to PC mode on this PC, but PC mode is off, so games won't feel them until you turn it on."));
        else lines.Add(new(false, "Here I can only preview changes. Turn on PC mode, or use an X20 Pro with PC mode off, to apply them."));
        return lines;
    }

    async Task ShowAccess()
    {
        accessRows.Children.Clear();
        accessRows.Children.Add(Wrap("Checking…", 13, "DS.TextMuted", 0));
        var lines = await CheckAccess();
        accessRows.Children.Clear();
        foreach (var line in lines) accessRows.Children.Add(AccessRow(line));
    }

    static FrameworkElement AccessRow(AccessLine line) => new DockPanel
    {
        Margin = new(0, 3, 0, 3),
        Children =
        {
            Kit.Text(line.Ok ? "✓" : "!", 13, "DS.Text", FontWeights.Bold).Also(t => { t.Width = 18; t.Foreground = line.Ok ? new SolidColorBrush(ThemeCatalog.R(Color.FromRgb(61, 214, 140))) : DS.Brush("DS.Warn"); }),
            Wrap(line.Text, 13, "DS.TextSoft", 0),
        },
    };

    /// <summary>"Can you read my controller?": the same check, answered in the chat.</summary>
    async Task AccessInChat()
    {
        Searching(true);
        var lines = await CheckAccess();
        Searching(false);
        var body = new StackPanel { Children = { Wrap("Here's what I can reach on your controller, checked just now:", 14, "DS.Text") } };
        foreach (var line in lines) body.Children.Add(AccessRow(line).Also(r => r.Margin = new(0, 6, 0, 0)));
        Bubble("", false).Child = body; scroller.ScrollToEnd();
        _ = Lights(lines.All(l => l.Ok) ? AgentMood.Good : AgentMood.Unsure);
    }

    // ---- the guided check ----
    /// <summary>Opens the check-up on the agent's screen and starts the guided check, once it can see the controller.</summary>
    void StartCheck()
    {
        if (busy) return;
        ShowSettingsPage(false); ShowView(View.Checkup);
        StopCheck(false);
        checkReader = CheckInput == null ? OpenReader() : null;
        checkSlot = CheckInput != null ? 1 : checkReader == null ? -1 : XInputDriftMeter.Slot(player);
        checkMenu.Visibility = Visibility.Collapsed; checkStage.Visibility = Visibility.Visible;
        if (checkSlot < 0)
        {
            checkReader?.Dispose(); checkReader = null;
            Stage("Can't start", "I can't see your controller", $"Connect your {padName} by cable, Bluetooth or its dongle, in Xbox (XInput) mode, then try again. I'll check it right here.", null);
            Actions(("Try again", () => StartCheck()), ("Back", () => StopCheck(true)));
            return;
        }
        restLeft.Reset(); restRight.Reset(); sweepLeft.Reset(); sweepRight.Reset();
        ltMax = rtMax = 0; ltDone = rtDone = false; pressedMask = 0; packets = 0; movingSeconds = 0; skipped.Clear();
        checkRunning = true; CaptureChanged?.Invoke();
        Go(CheckStep.Rest);
        lastTick = DateTime.UtcNow; checkTimer.Start();
    }

    /// <summary>Stops a check in progress; back to the menu when asked.</summary>
    void StopCheck(bool toMenu)
    {
        checkTimer.Stop();
        checkReader?.Dispose(); checkReader = null;
        if (checkRunning) { checkRunning = false; CaptureChanged?.Invoke(); }
        if (toMenu && checkStage != null) { checkStage.Visibility = Visibility.Collapsed; checkMenu.Visibility = Visibility.Visible; }
    }

    void Go(CheckStep next)
    {
        step = next; stepAt = DateTime.UtcNow;
        stickCanvas.Visibility = next is CheckStep.Rest or CheckStep.Left or CheckStep.Right ? Visibility.Visible : Visibility.Collapsed;
        triggerVisual.Visibility = next == CheckStep.Triggers ? Visibility.Visible : Visibility.Collapsed;
        buttonVisual.Visibility = next == CheckStep.Buttons ? Visibility.Visible : Visibility.Collapsed;
        stageFindings.Visibility = next == CheckStep.Done ? Visibility.Visible : Visibility.Collapsed;
        stickTrail.Points.Clear();
        foreach (var mark in sectorMarks) mark.Stroke = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255));
        switch (next)
        {
            case CheckStep.Rest: Stage("Step 1 of 5", "Let go of both sticks", "Put the controller down, or hold it without touching the sticks. I'm watching where they rest.", null); break;
            case CheckStep.Left: Stage("Step 2 of 5", "Roll the left stick round its edge", "Push it all the way out and go round slowly, twice. The ring fills in as you go.", null); break;
            case CheckStep.Right: Stage("Step 3 of 5", "Now the right stick", "All the way out, round slowly, twice.", null); break;
            case CheckStep.Triggers: Stage("Step 4 of 5", "Squeeze both triggers all the way", "Squeeze LT and RT to the end, then let them go.", null); break;
            case CheckStep.Buttons: Stage("Step 5 of 5", "Press every button once", "Each one lights up when I see it. Don't worry, B won't close me while I'm checking.", null);
                foreach (var chip in buttonChips.Values) { chip.Background = new SolidColorBrush(Card); chip.BorderBrush = new SolidColorBrush(Line); }
                break;
        }
        if (next != CheckStep.Done) Actions((next == CheckStep.Buttons ? "Finish (report what didn't light)" : "Skip this step", Skip), ("Stop", () => { StopCheck(true); Say("Check-up stopped. Nothing was changed.", null); }));
    }

    void Skip()
    {
        // once a button has been seen, leaving the button step reports the ones that never lit: a broken button is a finding
        if (step == CheckStep.Buttons && pressedMask != 0) { Next(); return; }
        skipped.Add(step switch { CheckStep.Left => "left stick", CheckStep.Right => "right stick", CheckStep.Triggers => "triggers", CheckStep.Buttons => "buttons", _ => "rest" });
        Next();
    }
    void Next()
    {
        Go(step switch { CheckStep.Rest => CheckStep.Left, CheckStep.Left => CheckStep.Right, CheckStep.Right => CheckStep.Triggers, CheckStep.Triggers => CheckStep.Buttons, _ => CheckStep.Done });
        if (step == CheckStep.Done) Finish();
    }

    void Stage(string stepText, string title, string says, string? progress)
    {
        stageStep.Text = stepText; stageTitle.Text = title; stageSays.Text = says; stageProgress.Text = progress ?? "";
    }
    void Actions(params (string Label, Action Act)[] actions)
    {
        stageActions.Children.Clear();
        foreach (var (label, act) in actions) { var b = Small(label); b.Margin = new(0, 0, 8, 0); b.Click += (_, _) => act(); register(b); stageActions.Children.Add(b); }
    }

    void CheckTick()
    {
        if (!checkRunning) return;
        var now = DateTime.UtcNow; double dt = (now - lastTick).TotalSeconds; lastTick = now;
        double into = (now - stepAt).TotalSeconds;
        (short LX, short LY, short RX, short RY, byte LT, byte RT, ushort Buttons, uint Packet) s;
        if (CheckInput != null) { if (CheckInput(step.ToString(), into) is not { } fake) return; s = fake; }
        else if (checkReader != null && checkReader.Read(checkSlot, out var st)) s = (st.Pad.LX, st.Pad.LY, st.Pad.RX, st.Pad.RY, st.Pad.LT, st.Pad.RT, st.Pad.Buttons, st.Packet);
        else { stageProgress.Text = "I lost the controller. Is it still connected?"; return; }
        double lx = AgentCheckup.Unit(s.LX), ly = AgentCheckup.Unit(s.LY), rx = AgentCheckup.Unit(s.RX), ry = AgentCheckup.Unit(s.RY);
        bool newPacket = s.Packet != lastPacket; lastPacket = s.Packet;
        switch (step)
        {
            case CheckStep.Rest:
                ShowStick(lx, ly, false);
                if (into < 1) { stageProgress.Text = "Hands off…"; break; }
                restLeft.Add(lx, ly); restRight.Add(rx, ry);
                if (restLeft.Touched || restRight.Touched)
                {
                    restLeft.Reset(); restRight.Reset(); stepAt = now;
                    stageProgress.Text = "A stick moved. Let go of both, and I'll start again.";
                    break;
                }
                stageProgress.Text = $"Reading where they rest… {Math.Max(0, 3 - into):0.0}s";
                if (into >= 3) Next();
                break;
            case CheckStep.Left or CheckStep.Right:
                bool left = step == CheckStep.Left;
                double x = left ? lx : rx, y = left ? ly : ry;
                var sweep = left ? sweepLeft : sweepRight;
                sweep.Add(x, y); ShowStick(x, y, true);
                if (Math.Sqrt(x * x + y * y) > .3) { movingSeconds += dt; if (newPacket) packets++; }
                for (int i = 0; i < sectorMarks.Length; i++)
                    if (sweep.Reach[i] >= AgentCheckup.Reached && sectorMarks[i].Stroke is SolidColorBrush b && b.Color != Amber) sectorMarks[i].Stroke = new SolidColorBrush(Amber);
                stageProgress.Text = $"{sweep.Coverage * 100:0}% of the edge reached";
                if (sweep.Done) Next();
                break;
            case CheckStep.Triggers:
                double lt = AgentCheckup.Unit(s.LT), rt = AgentCheckup.Unit(s.RT);
                ltMax = Math.Max(ltMax, lt); rtMax = Math.Max(rtMax, rt);
                ltBar.Height = 178 * lt; rtBar.Height = 178 * rt;
                if (ltMax >= .95 && lt < .1) ltDone = true; if (rtMax >= .95 && rt < .1) rtDone = true;
                stageProgress.Text = $"LT {ltMax * 100:0}% {(ltDone ? "✓" : "")}   ·   RT {rtMax * 100:0}% {(rtDone ? "✓" : "")}";
                if (ltDone && rtDone) Next();
                break;
            case CheckStep.Buttons:
                foreach (var (flag, chip) in buttonChips)
                {
                    bool down = (s.Buttons & flag) != 0;
                    if (down) pressedMask |= flag;
                    bool seen = (pressedMask & flag) != 0;
                    chip.Background = new SolidColorBrush(down ? Amber : seen ? ThemeCatalog.R(Color.FromRgb(28, 60, 44)) : Card);
                    chip.BorderBrush = new SolidColorBrush(seen ? ThemeCatalog.R(Color.FromRgb(61, 214, 140)) : Line);
                }
                int got = CheckButtons.Count(b => (pressedMask & b.Flag) != 0);
                stageProgress.Text = $"{got} of {CheckButtons.Length} seen";
                if (got == CheckButtons.Length && s.Buttons == 0) Next();
                break;
        }
    }

    void ShowStick(double x, double y, bool trail)
    {
        double c = StickBox / 2, r = c - 14;
        double px = c + Math.Clamp(x, -1, 1) * r, py = c - Math.Clamp(y, -1, 1) * r;
        Canvas.SetLeft(stickDot, px - 8); Canvas.SetTop(stickDot, py - 8);
        if (!trail) return;
        stickTrail.Points.Add(new Point(px, py));
        if (stickTrail.Points.Count > 240) stickTrail.Points.RemoveAt(0);
    }

    CheckupResult? lastCheck;
    void Finish()
    {
        StopCheck(false);
        var result = lastCheck = new CheckupResult(restLeft.Offset, restRight.Offset, sweepLeft, sweepRight, ltMax, rtMax,
            CheckButtons.Where(b => (pressedMask & b.Flag) != 0).Select(b => b.Name).ToList(),
            CheckButtons.Where(b => (pressedMask & b.Flag) == 0).Select(b => b.Name).ToList(),
            // the reads here run on the screen's timer, about 60 a second, so how often the pad reports can't be measured: not said
            0, skipped.ToList());
        var findings = result.Findings();
        int bad = findings.Count(f => !f.Good);
        Stage("Done", bad == 0 ? "Your controller checks out" : $"{bad} thing{(bad == 1 ? "" : "s")} to look at", "Measured just now from your controller. Nothing was written to it.", null);
        stageFindings.Children.Clear();
        foreach (var (good, line) in findings) stageFindings.Children.Add(AccessRow(new(good, line)));
        var actions = new List<(string, Action)>();
        foreach (var (name, rest) in new[] { ("left", result.LeftRest), ("right", result.RightRest) })
            if (rest > AgentCheckup.DriftLimit)
            {
                string ask = $"set the {name} stick dead zone to {CheckupResult.DeadZoneFor(rest)}%";
                actions.Add(($"Hide the {name} drift (dead zone {CheckupResult.DeadZoneFor(rest)}%)", () => { StopCheck(true); ShowView(View.Chat); _ = Ask(ask); }));
            }
        if (result.Missed.Count > 0 && !skipped.Contains("buttons")) actions.Add(("Prepare a report", () => FeedbackRequested?.Invoke($"Controller: {padName}\nCheck-up findings:\n" + string.Join("\n", findings.Where(f => !f.Good).Select(f => "- " + f.Line)) + "\n")));
        actions.Add(("Run it again", () => StartCheck()));
        actions.Add(("Back", () => StopCheck(true)));
        Actions(actions.ToArray());
        // the agent says it in the chat too, so the result stays in the conversation
        Say(bad == 0 ? "Check-up done: everything I measured looks healthy. *proud wiggle*" : "Check-up done. " + string.Join(" ", findings.Where(f => !f.Good).Select(f => f.Line)), null);
        _ = Lights(bad == 0 ? AgentMood.Good : AgentMood.Unsure);
        if (bad == 0) Applied();
    }
}
