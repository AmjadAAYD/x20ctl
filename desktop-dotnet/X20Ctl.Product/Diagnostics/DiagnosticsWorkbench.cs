using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using X20Ctl.Product.Scanner;
using X20Ctl.Zone.Diagnostics;
using X20Ctl.Zone;
using X20Ctl.Zone.PcMode;
namespace X20Ctl.Product.Diagnostics;

/// <summary>
/// The Studio's Diagnostics tab (owner request, 11 Oct 2026, after HK560/ControllerMeta): a live, read-only look at the
/// controller. Stick range traces with roundness, resting drift and step size; trigger travel; every button's presses
/// with chatter (bounce) flags; and the real report rate from Raw Input next to how often XInput's state changes.
/// Works for every model because it only reads what Windows reports. Nothing is ever written to the controller.
/// </summary>
public sealed class DiagnosticsWorkbench
{
    static readonly (string Name, ushort Mask)[] ReportedButtons =
    [
        ("A", 0x1000), ("B", 0x2000), ("X", 0x4000), ("Y", 0x8000), ("LB", 0x0100), ("RB", 0x0200), ("View", 0x0020), ("Menu", 0x0010),
        ("L3", 0x0040), ("R3", 0x0080), ("Up", 0x0001), ("Down", 0x0002), ("Left", 0x0004), ("Right", 0x0008), ("Home", 0x0400),
    ];
    readonly (string Name,ushort Mask)[] Buttons;
    readonly ushort[] Masks;
    readonly TextBlock numericValues=Wrap("No input data",13,"DS.TextSoft",10);
    bool rawValues;readonly string assignedModel;
    readonly TextBlock driftDetails=Wrap("",13,"DS.TextSoft",8);
    CancellationTokenSource? driftCancel;
    (X20Ctl.Zone.Diagnostics.DriftObservation Left,X20Ctl.Zone.Diagnostics.DriftObservation Right)? driftResult;
    const double Plot = 196, Unit = 84; // canvas size, and the pixels for full travel (so 115% still fits)

    [DllImport("winmm.dll")] static extern uint timeBeginPeriod(uint period);
    [DllImport("winmm.dll")] static extern uint timeEndPeriod(uint period);

    public Grid Body { get; } = new();
    readonly int preferredSlot;
    double[]? keyboardPreview;
    public void PreviewKeyboard(double lx, double ly, double rx, double ry, double leftTrigger, double rightTrigger)
        => keyboardPreview = [lx, ly, rx, ry, leftTrigger, rightTrigger];
    readonly object gate = new();
    readonly StickStats left = new(), right = new();
    readonly TriggerStats lt = new(), rt = new();
    readonly ButtonStats presses = new();
    readonly RateMeter changes = new(), reads = new();
    readonly Stopwatch clock = new();
    readonly DispatcherTimer timer;
    WindowsReader? reader; Thread? worker; volatile bool running; RawRate? raw;
    volatile int slot = -1, chosen = -1; // chosen: -1 finds the controller, 0–3 stays on that slot
    uint lastPacket; bool seen;

    readonly TextBlock source, rateText, rawText;
    readonly (Polygon Reach, Ellipse Dot, Ellipse Resting, TextBlock Stats) leftView, rightView;
    readonly (Rectangle Bar, Rectangle Range, TextBlock Stats) ltView, rtView;
    readonly Dictionary<ushort, (Border Cell, TextBlock Count)> cells = new();
    readonly List<Button> slotButtons = new();
    public Button Reset { get; }
    readonly PcModeLink? link;
    readonly Dictionary<StickStats, (string Key, Button Use, TextBlock Said)> apply = new();
    readonly Dictionary<StickStats, double> suggested = new();

    public DiagnosticsWorkbench(int player, Action<Button> register, PcModeLink? link = null, bool compact = false,ControllerModel? model=null)
    {
        preferredSlot = player; this.link = link;assignedModel=model?.Name??"Unspecified";
        static string Key(string name)=>name switch{"View"=>"SELECT","Menu"=>"START","Home"=>"HOME","Up"=>"DPAD_UP","Down"=>"DPAD_DOWN","Left"=>"DPAD_LEFT","Right"=>"DPAD_RIGHT",_=>name};
        Buttons=ReportedButtons.Where(b=>model==null||model.FrontButtons.ContainsKey(Key(b.Name))).ToArray();Masks=Buttons.Select(b=>b.Mask).ToArray();
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        var page = new StackPanel { Margin = new(30, 6, 30, 24) }; scroll.Content = page; Body.Children.Add(scroll);

        var head = new DockPanel();
        var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top }; DockPanel.SetDock(actions, Dock.Right); head.Children.Add(actions);
        foreach (var (label, value) in new[] { ("Retry player input", -1), ("XInput 1", 0), ("XInput 2", 1), ("XInput 3", 2), ("XInput 4", 3) })
        {
            var b = Pill(label); b.Margin = new(8, 0, 0, 0); b.Click += (_, _) => Choose(value); register(b); slotButtons.Add(b); actions.Children.Add(b);
        }
        Reset = Pill("Clear measurements"); Reset.Margin = new(16, 0, 0, 0); Reset.Click += (_, _) => Clear(); register(Reset); actions.Children.Add(Reset);
        var copy = Pill("Copy report"); copy.Margin = new(8, 0, 0, 0); copy.ToolTip = "Copies these readings as text, to paste into a bug report or a chat";
        copy.Click += (_, _) => { try { Clipboard.SetText(Report()); source!.Text = "Copied. Paste it wherever you need it."; } catch (System.Runtime.InteropServices.ExternalException) { source!.Text = "The clipboard is busy. Try again."; } };
        register(copy); actions.Children.Add(copy);
        head.Children.Add(new StackPanel { Children = {
            Kit.Text("Diagnostics", 30, "DS.Text", FontWeights.Bold),
            Wrap("Roll each stick slowly around its edge a few times, squeeze both triggers all the way and press every button. X20CTL only listens; nothing is sent to the controller. All controller buttons are captured for testing. Use Esc or the mouse to leave.", 15, "DS.TextSoft", 6) } });
        page.Children.Add(head);
        source = Wrap("", 14, "DS.TextMuted", 8); page.Children.Add(source);
        page.Children.Add(Wrap($"Player {player+1} · {assignedModel} (assigned model) · Gameplay API: XInput · Transport and hardware identity unverified",13,"DS.TextSoft",6));
        var valuesToggle=Pill("Show integer XInput values");valuesToggle.Margin=new(0,8,0,0);valuesToggle.HorizontalAlignment=HorizontalAlignment.Left;valuesToggle.Click+=(_,_)=>{rawValues=!rawValues;valuesToggle.Content=rawValues?"Show normalized values":"Show integer XInput values";Refresh();};register(valuesToggle);page.Children.Add(valuesToggle);page.Children.Add(numericValues);
        var measureCentre=Pill("Measure centre deviation");measureCentre.HorizontalAlignment=HorizontalAlignment.Left;measureCentre.Margin=new(0,8,0,0);register(measureCentre);
        measureCentre.Click+=async(_,_)=>{measureCentre.IsEnabled=false;driftCancel?.Cancel();driftCancel=new();try{driftResult=await DriftCapture.Measure(chosen>=0?chosen:preferredSlot,new Progress<double>(v=>driftDetails.Text=$"Release both sticks · Sampling {v*5:0.0} / 5 seconds"),driftCancel.Token);driftDetails.Text=driftResult is {} d?$"Measured centre X/Y · Left {d.Left.X:0.00}% / {d.Left.Y:0.00}% · Right {d.Right.X:0.00}% / {d.Right.Y:0.00}% · Peak deviation {d.Left.Peak:0.00}% / {d.Right.Peak:0.00}%" : "Selected input unavailable · No centre samples";}catch(OperationCanceledException){}catch(Exception error)when(error is IOException or ArgumentException){driftDetails.Text=error.Message;}finally{measureCentre.IsEnabled=true;}};
        page.Children.Add(measureCentre);page.Children.Add(driftDetails);

        var top = new Grid { Margin = new(0, 18, 0, 0) };
        for (int i = 0; i < (compact ? 3 : 4); i++) top.ColumnDefinitions.Add(new() { Width = new GridLength(i < 2 ? 1.15 : 1, GridUnitType.Star) });
        leftView = StickCard(top, 0, "Left stick", left, "LeftStick", register); rightView = StickCard(top, 1, "Right stick", right, "RightStick", register);
        var triggers = new Grid(); triggers.ColumnDefinitions.Add(new()); triggers.ColumnDefinitions.Add(new());
        ltView = TriggerColumn(triggers, 0, "LT"); rtView = TriggerColumn(triggers, 1, "RT");
        Card(top, 2, "Triggers", triggers);
        rateText = Kit.Text("", 15, "DS.Text").Also(t => { t.TextWrapping = TextWrapping.Wrap; t.TextTrimming = TextTrimming.None; });
        rawText = Wrap("", 13.5, "DS.TextSoft", 12);
        Card(top, 3, "Observed input updates", new StackPanel { Children = { rateText, rawText,
            Wrap("XInput state changes are not hardware polling rate. Raw Input report counts below are separate, unassociated devices; they are not verified as this selected controller.", 12.5, "DS.TextMuted", 12) } });
        page.Children.Add(top);

        var grid = new WrapPanel();
        foreach (var (name, mask) in Buttons)
        {
            var count = Kit.Text("not pressed", 12.5, "DS.TextMuted");
            var cell = new Border { Width = 118, Height = 64, Margin = new(0, 0, 10, 10), CornerRadius = new(12), BorderThickness = new(1), Padding = new(12, 8, 12, 8),
                Background = new SolidColorBrush(Color.FromArgb(70, 4, 8, 20)), BorderBrush = new SolidColorBrush(Color.FromArgb(50, 140, 175, 255)),
                Child = new StackPanel { Children = { Kit.Text(name, 16, "DS.Text", FontWeights.SemiBold), count } } };
            cells[mask] = (cell, count); grid.Children.Add(cell);
        }
        var buttonsCard = Kit.Panel("Buttons", "Reported XInput buttons only. Three rapid transitions within 15 ms are flagged for review, not diagnosed as a worn switch.", null, grid);
        buttonsCard.Margin = new(0, 16, 0, 0); page.Children.Add(buttonsCard);

        timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(33) };
        timer.Tick += (_, _) => Refresh();
        if (compact)
        {
            head.Visibility = Visibility.Collapsed;
            buttonsCard.Visibility = Visibility.Collapsed;
            foreach (FrameworkElement card in top.Children) if (Grid.GetColumn(card) == 3) card.Visibility = Visibility.Collapsed;
        }
        Refresh();
    }

    /// <summary>Starts listening (the tab is shown). Safe to call twice.</summary>
    public void Start()
    {
        if (running) return;
        try { reader = new WindowsReader(WindowsReader.Libraries[0]); }
        catch (Exception error) when (error is IOException or ArgumentException) { source.Text = "Windows can't read controllers here: " + error.Message; return; }
        try { raw = new RawRate(); } catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException or InvalidOperationException) { raw = null; }
        running = true; clock.Restart();
        worker = new Thread(Loop) { IsBackground = true, Name = "X20CTL diagnostics", Priority = ThreadPriority.AboveNormal };
        worker.Start(); timer.Start();
    }

    /// <summary>Stops listening (the tab is left, or Controller Check needs Raw Input).</summary>
    public void Stop()
    {
        timer.Stop();driftCancel?.Cancel();
        if (running) { running = false; worker?.Join(500); }
        worker = null; raw?.Dispose(); raw = null; reader?.Dispose(); reader = null;
    }

    void Choose(int value) { chosen = value; slot = -1; Clear(); }

    void Clear()
    {
        lock (gate) { left.Reset(); right.Reset(); lt.Reset(); rt.Reset(); presses.Reset(); changes.Reset(); reads.Reset(); seen = false; }
        raw?.Reset();driftResult=null;driftDetails.Text="";
    }

    void Loop()
    {
        timeBeginPeriod(1);
        try
        {
            while (running)
            {
                double now = clock.Elapsed.TotalMilliseconds;
                if (slot < 0) slot = Find();
                if (slot >= 0 && reader!.Read(slot, out var state))
                {
                    lock (gate)
                    {
                        reads.Add(now);left.Add(state.Pad.LX,state.Pad.LY);right.Add(state.Pad.RX,state.Pad.RY);lt.Add(state.Pad.LT);rt.Add(state.Pad.RT);
                        if (!seen || state.Packet != lastPacket)
                        {
                            if (seen) changes.Add(now);
                            seen = true; lastPacket = state.Packet; var p = state.Pad;
                            presses.Add(p.Buttons, now, Masks);
                        }
                    }
                }
                else if (slot >= 0) { if (chosen < 0) slot = -1; lock (gate) seen = false; Thread.Sleep(50); }
                else Thread.Sleep(100);
                Thread.Sleep(1);
            }
        }
        finally { timeEndPeriod(1); }
    }

    /// <summary>The slot to read: the one picked, else this player's slot, else the first controller found. Never PC
    /// mode's virtual pad, which only repeats the real one.</summary>
    int Find()
    {
        if (chosen >= 0) return chosen;
        if (preferredSlot is >= 0 and < 4 && !PcModeService.IsVirtual(preferredSlot) && reader!.Read(preferredSlot, out _)) return preferredSlot;
        // Do not silently replace this player with another player's controller.
        return -1;
    }

    void Refresh()
    {
        double now = clock.Elapsed.TotalMilliseconds;
        int s = slot, c = chosen;
        for (int i = 0; i < slotButtons.Count; i++) slotButtons[i].Opacity = i - 1 == c ? 1 : .62;
        lock (gate)
        {
            source.Text = !running ? "Not listening." : s < 0 ? (c < 0 ? "Waiting for the selected player input. Connect it or choose an XInput index." : $"Nothing on slot {c + 1}.")
                : !seen ? $"Slot {s + 1}: waiting for the controller."
                : $"Reading XInput slot {s + 1}{(PcModeService.IsVirtual(s) ? " (PC mode's virtual pad: this shows PC mode's output, not the controller)" : "")} · {reads.Current(now)} reads a second";
            numericValues.Text=!seen?"No input data · Raw HID report bytes unavailable":rawValues?$"XInput integers · LX {left.X}  LY {left.Y}  RX {right.X}  RY {right.Y}  LT {lt.Value}  RT {rt.Value} · Raw HID bytes unavailable":$"Normalized · L ({left.X/32767d:0.000}, {left.Y/32767d:0.000})  R ({right.X/32767d:0.000}, {right.Y/32767d:0.000})  LT {lt.Value/255d:0.000}  RT {rt.Value/255d:0.000}";
            DrawStick(leftView, left); DrawStick(rightView, right);
            DrawTrigger(ltView, lt); DrawTrigger(rtView, rt);
            foreach(var view in new[]{leftView,rightView}) {view.Dot.Visibility=seen?Visibility.Visible:Visibility.Collapsed;view.Reach.Visibility=seen?Visibility.Visible:Visibility.Collapsed;if(!seen){view.Resting.Visibility=Visibility.Collapsed;view.Stats.Text="No input source · Connect or select a controller";}}
            if(!seen){ltView.Bar.Height=rtView.Bar.Height=0;ltView.Range.Visibility=rtView.Range.Visibility=Visibility.Collapsed;ltView.Stats.Text=rtView.Stats.Text="No data";}
            if (s < 0 && keyboardPreview is { } keys)
            {
                source.Text = "Keyboard preview · WASD / arrows · Q / E triggers. Connect a controller for measured circularity error.";
                Canvas.SetLeft(leftView.Dot, Plot / 2 + keys[0] * Unit - 6); Canvas.SetTop(leftView.Dot, Plot / 2 - keys[1] * Unit - 6);
                Canvas.SetLeft(rightView.Dot, Plot / 2 + keys[2] * Unit - 6); Canvas.SetTop(rightView.Dot, Plot / 2 - keys[3] * Unit - 6);
                leftView.Stats.Text = rightView.Stats.Text = "Keyboard preview · hardware error not measured";
                ltView.Bar.Height = keys[4] * 150; rtView.Bar.Height = keys[5] * 150;
                ltView.Stats.Text = $"{keys[4] * 100:0}% preview"; rtView.Stats.Text = $"{keys[5] * 100:0}% preview";
            }
            foreach (var (_, mask) in Buttons)
            {
                var b = presses[mask]; var (cell, count) = cells[mask];cell.ToolTip=b.Presses==0?"Not tested":$"Presses {b.Presses} · Last {b.LastMs:0} ms · Shortest {b.ShortestMs:0} ms · Longest {b.LongestMs:0} ms";
                count.Text = !seen ? "No data" : b.Presses == 0 ? "Not tested" : b.Bounces > 0 ? $"{b.Presses} · {b.Bounces} rapid sequence{(b.Bounces == 1 ? "" : "s")}" : $"{b.Presses} press{(b.Presses == 1 ? "" : "es")}";
                count.Foreground = DS.Brush(b.Bounces > 0 ? "DS.Warn" : b.Presses > 0 ? "DS.TextSoft" : "DS.TextMuted");
                cell.Background = b.Down ? Blue(150) : Dark;
                cell.BorderBrush = b.Bounces > 0 ? DS.Brush("DS.Warn") : b.Presses > 0 ? Edge(120) : Edge(50);
            }
            rateText.Text = seen ? $"Observed changes {changes.Current(now)} /s · peak {changes.Peak} /s\nControls tested {Buttons.Count(x=>presses[x.Mask].Presses>0)} / {Buttons.Length}" : "No input data · Rate unavailable";
        }
        var rawDevices = raw?.Devices();
        rawText.Text = raw == null ? "Raw Input reports (all devices): unavailable on this PC."
            : raw.Error != null ? "Raw Input reports (all devices): " + raw.Error
            : rawDevices is not { Count: > 0 } ? "Raw Input reports (all devices): none yet. Move a stick. If this stays empty, Windows isn't passing this controller's reports to apps; selected-source XInput updates remain separate."
            : string.Join(Environment.NewLine, rawDevices.Take(4).Select(d => $"{d.Name}: {d.Current} reports a second · peak {d.Peak}"));
    }

    /// <summary>The readings as plain text: what a tester pastes into a report. Only measurements, nothing identifying.</summary>
    string Report()
    {
        double now = clock.Elapsed.TotalMilliseconds; var lines = new List<string> { "X20CTL diagnostics", $"Version {X20Ctl.Zone.ReleaseVersions.Display(Scanner.ScanFlow.AppVersion)} ({Scanner.ScanFlow.AppVersion})", $"Timestamp {DateTimeOffset.Now:O}", $"Player {preferredSlot+1} · Assigned model {assignedModel} · Hardware identity unverified", "Input API: XInput · Transport, VID/PID, firmware: unknown", "Values: measured through Windows; not raw HID values or hardware resolution", $"XInput slot {(slot < 0 ? "none" : (slot + 1).ToString())}" };
        lock (gate)
        {
            foreach (var (name, s) in new[] { ("Left stick", left), ("Right stick", right) })
                lines.Add($"{name}: circularity {(s.CircularityError is { } e ? e.ToString("0.0") + "% off" : "not measured")}, rest {(s.Rest is { } r ? $"{r.X:0.0}%, {r.Y:0.0}%" : "not measured")}, step {(s.ResolutionBits is { } b ? b + " effective bits observed" : "not measured")}");
            foreach(var (name,s) in new[]{("Left stick",left),("Right stick",right)})lines.Add(s.HasSamples?$"{name} range X {s.MinX/32767d:0.00000} to {s.MaxX/32767d:0.00000}; Y {s.MinY/32767d:0.00000} to {s.MaxY/32767d:0.00000}; smallest X delta {s.SmallestXDelta?.ToString("0.00000")??"unknown"}; Y {s.SmallestYDelta?.ToString("0.00000")??"unknown"}; max radial error {s.MaximumRadialError?.ToString("0.00")??"unmeasured"}":$"{name}: no samples");
            foreach (var (name, s) in new[] { ("LT", lt), ("RT", rt) })
                lines.Add(s.Max < s.Min ? $"{name}: not pressed" : $"{name}: {s.Min * 100 / 255}-{s.Max * 100 / 255}%, {(s.FullTravel ? "full travel" : "partial travel")}{(s.ResolutionBits is { } b ? ", " + b + " effective bits observed" : "")}");
            lines.Add("Buttons: " + string.Join(", ", Buttons.Select(x => (x.Name, b: presses[x.Mask])).Where(x => x.b.Presses > 0).Select(x => $"{x.Name} {x.b.Presses}{(x.b.Bounces > 0 ? $" ({x.b.Bounces} rapid sequences, shortest {x.b.ShortestMs:0} ms)" : "")}").DefaultIfEmpty("none pressed")));
            lines.Add($"State changes: {changes.Current(now)}/s, peak {changes.Peak}/s");
        }
        if(driftResult is {} drift)lines.Add($"Five-second centre sample: left offset {drift.Left.X:0.000}%/{drift.Left.Y:0.000}%, peak {drift.Left.Peak:0.000}%; right offset {drift.Right.X:0.000}%/{drift.Right.Y:0.000}%, peak {drift.Right.Peak:0.000}%");
        foreach (var d in raw?.Devices() ?? []) lines.Add($"Reports, {d.Name}: {d.Current}/s, peak {d.Peak}/s");
        return string.Join(Environment.NewLine, lines);
    }

    static readonly Brush Dark = Freeze(new SolidColorBrush(Color.FromArgb(70, 4, 8, 20)));
    static Brush Blue(byte a) => Freeze(new SolidColorBrush(Color.FromArgb(a, 61, 139, 255)));
    static Brush Edge(byte a) => Freeze(new SolidColorBrush(Color.FromArgb(a, 140, 175, 255)));
    static Brush Freeze(Brush b) { b.Freeze(); return b; }

    void DrawStick((Polygon Reach, Ellipse Dot, Ellipse Resting, TextBlock Stats) view, StickStats stats)
    {
        var points = new PointCollection(StickStats.Sectors);
        for (int i = 0; i < StickStats.Sectors; i++)
        {
            double r = Math.Min(stats.Reach[i], 1.15) * Unit, a = (i + .5) / StickStats.Sectors * 2 * Math.PI - Math.PI;
            points.Add(new(Plot / 2 + Math.Cos(a) * r, Plot / 2 - Math.Sin(a) * r));
        }
        view.Reach.Points = points;
        Canvas.SetLeft(view.Dot, Plot / 2 + stats.X / 32767.0 * Unit - 6); Canvas.SetTop(view.Dot, Plot / 2 - stats.Y / 32767.0 * Unit - 6);
        var rest = stats.Rest; view.Resting.Visibility = rest == null ? Visibility.Collapsed : Visibility.Visible;
        if (rest is { } at) { Canvas.SetLeft(view.Resting, Plot / 2 + at.X / 100 * Unit - 4); Canvas.SetTop(view.Resting, Plot / 2 - at.Y / 100 * Unit - 4); }
        var lines = new List<string>();
        double? round = stats.CircularityError;
        lines.Add(round is { } e ? $"Circularity error: {e:0.0}% off a circle{(e > 8 ? " radial deviation" : "")}" : $"Circularity coverage {stats.Covered * 100 / StickStats.Sectors}% · Rotate around the edge");
        if (rest is { } d)
        {
            double drift = Math.Sqrt(d.X * d.X + d.Y * d.Y);
            lines.Add($"Near-center average: {d.X:+0.0;-0.0;0.0}%, {d.Y:+0.0;-0.0;0.0}% (deviation {drift:0.0}%)");
            double zone = Math.Min(StickTuning.MaxInner, Math.Ceiling((drift + .5) * 10) / 10); suggested[stats] = zone;
            if (drift >= 3) lines.Add($"That drift may show in games. An inner dead zone of {zone:0.0}% hides it.");
            if (apply.TryGetValue(stats, out var a)) a.Use.Visibility = Visibility.Visible;
        }
        else { lines.Add("Near-center average: release the stick to collect samples"); if (apply.TryGetValue(stats, out var a)) a.Use.Visibility = Visibility.Collapsed; }
        lines.Add(stats.ResolutionBits is { } bits ? $"Observed step estimate: {bits} effective bits" : "Smallest step: move it slowly to measure");
        if(stats.HasSamples){lines.Add($"Range X {stats.MinX/32767d:0.000} → {stats.MaxX/32767d:0.000} · Y {stats.MinY/32767d:0.000} → {stats.MaxY/32767d:0.000}");lines.Add($"Smallest delta X {stats.SmallestXDelta?.ToString("0.00000")??"—"} · Y {stats.SmallestYDelta?.ToString("0.00000")??"—"}");}
        view.Stats.Text = string.Join(Environment.NewLine, lines);
    }

    static void DrawTrigger((Rectangle Bar, Rectangle Range, TextBlock Stats) view, TriggerStats stats)
    {
        const double height = 150;
        view.Bar.Height = stats.Value / 255.0 * height;
        bool any = stats.Max >= stats.Min;
        view.Range.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
        if (any) { view.Range.Height = Math.Max(2, (stats.Max - stats.Min) / 255.0 * height); view.Range.Margin = new(0, 0, 0, stats.Min / 255.0 * height); }
        view.Stats.Text = !any ? "No data" : $"Current {stats.Value/255d:0.00}\nMin {stats.Min/255d:0.00} · Max {stats.Max/255d:0.00}" + Environment.NewLine
            + (stats.FullTravel ? "full travel" : stats.Max < 250 ? "squeeze fully" : "let it go") + (stats.ResolutionBits is { } bits ? Environment.NewLine + bits + " effective bits observed" : "");
    }

    /// <summary>Puts the measured drift into PC mode as this stick's inner dead zone (the profile being edited),
    /// leaving every other setting as it was.</summary>
    void UseAsDeadZone(StickStats stats)
    {
        if (link == null || !suggested.TryGetValue(stats, out double zone)) return;
        var (key, _, said) = apply[stats];
        if (link.Preferences is { PcModeNoticeSeen: false }) { said.Text = "Open Curves → Advanced once first: PC mode has a short notice to read before it starts."; return; }
        try
        {
            var profile = link.Store.Get(PcModePage.StoreKey);
            var tuning = profile.Sticks.GetValueOrDefault(key) ?? new StickTuning();
            tuning.Inner = zone; tuning.Raw = false; if (tuning.Outer <= zone) tuning.Outer = 100;
            profile.Sticks[key] = tuning; profile.Validate(); link.Store.Save(PcModePage.StoreKey, profile); link.Service.Reload();
            said.Text = $"Inner dead zone set to {zone:0.0}% in PC mode{(link.Service.Enabled ? "" : " (switch PC mode on in Tools for it to apply)")}.";
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException) { said.Text = "Couldn't save it: " + error.Message; }
    }

    (Polygon, Ellipse, Ellipse, TextBlock) StickCard(Grid row, int column, string title, StickStats source, string key, Action<Button> register)
    {
        var canvas = new Canvas { Width = Plot, Height = Plot, HorizontalAlignment = HorizontalAlignment.Left, ClipToBounds = true };
        canvas.Children.Add(Ring(Unit, 70)); canvas.Children.Add(Ring(Unit / 2, 30));
        canvas.Children.Add(new Line { X1 = Plot / 2, X2 = Plot / 2, Y1 = Plot / 2 - Unit, Y2 = Plot / 2 + Unit, Stroke = Edge(30), StrokeThickness = 1 });
        canvas.Children.Add(new Line { Y1 = Plot / 2, Y2 = Plot / 2, X1 = Plot / 2 - Unit, X2 = Plot / 2 + Unit, Stroke = Edge(30), StrokeThickness = 1 });
        var reach = new Polygon { Stroke = Blue(230), StrokeThickness = 2, Fill = Blue(40), StrokeLineJoin = PenLineJoin.Round };
        var rest = new Ellipse { Width = 8, Height = 8, Fill = DS.Brush("DS.Warn") };
        var dot = new Ellipse { Width = 12, Height = 12, Fill = Brushes.White };
        canvas.Children.Add(reach); canvas.Children.Add(rest); canvas.Children.Add(dot);
        var stats = Kit.Text("", 13.5, "DS.TextSoft").Also(t => { t.TextWrapping = TextWrapping.Wrap; t.TextTrimming = TextTrimming.None; t.Margin = new(0, 12, 0, 0); t.LineHeight = 21; });
        var content = new StackPanel { Children = { canvas, stats } };
        if (link != null)
        {
            var use = Pill("Use as dead zone"); use.Height = 34; use.Margin = new(0, 10, 0, 0); use.HorizontalAlignment = HorizontalAlignment.Left; use.Visibility = Visibility.Collapsed;
            use.ToolTip = "Sets this stick's inner dead zone in PC mode just past where it rests, so the drift never reaches games";
            var said = Wrap("", 12.5, "DS.TextMuted", 6);
            use.Click += (_, _) => UseAsDeadZone(source); register(use);
            apply[source] = (key, use, said); content.Children.Add(use); content.Children.Add(said);
        }
        Card(row, column, title, content);
        return (reach, dot, rest, stats);
    }

    static (Rectangle, Rectangle, TextBlock) TriggerColumn(Grid host, int column, string name)
    {
        var bar = new Rectangle { Fill = Blue(220), VerticalAlignment = VerticalAlignment.Bottom, RadiusX = 6, RadiusY = 6 };
        var range = new Rectangle { Fill = Edge(40), VerticalAlignment = VerticalAlignment.Bottom };
        var well = new Border { Width = 42, Height = 150, CornerRadius = new(8), Background = Dark, BorderBrush = Edge(60), BorderThickness = new(1), HorizontalAlignment = HorizontalAlignment.Left,
            Child = new Grid { Children = { range, bar } } };
        var stats = Kit.Text("", 13, "DS.TextSoft").Also(t => { t.TextWrapping = TextWrapping.Wrap; t.TextTrimming = TextTrimming.None; t.Margin = new(0, 10, 0, 0); });
        var stack = new StackPanel { Children = { Kit.Text(name, 15, "DS.Text", FontWeights.SemiBold).Also(t => t.Margin = new(0, 0, 0, 8)), well, stats } };
        Grid.SetColumn(stack, column); host.Children.Add(stack);
        return (bar, range, stats);
    }

    static Ellipse Ring(double radius, byte alpha)
    {
        var e = new Ellipse { Width = radius * 2, Height = radius * 2, Stroke = Edge(alpha), StrokeThickness = 1.2 };
        Canvas.SetLeft(e, Plot / 2 - radius); Canvas.SetTop(e, Plot / 2 - radius); return e;
    }

    static void Card(Grid row, int column, string title, UIElement content)
    {
        var card = Kit.Panel(title, null, null, content); card.Margin = new(column == 0 ? 0 : 8, 0, column == 3 ? 0 : 8, 0);
        Grid.SetColumn(card, column); row.Children.Add(card);
    }

    static TextBlock Wrap(string text, double size, string brush, double top) => Kit.Text(text, size, brush).Also(t => { t.TextWrapping = TextWrapping.Wrap; t.TextTrimming = TextTrimming.None; t.Margin = new(0, top, 0, 0); });
    static Button Pill(string text) => new() { Style = DS.Style("ActionButton"), Height = 40, MinHeight = 0, Padding = new(16, 0, 18, 0), Content = Kit.Text(text, 14, "DS.Text", FontWeights.SemiBold) };
}
