using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Path = System.IO.Path;
using System.Windows.Threading;
using X20Ctl.Zone;
namespace X20Ctl.Product.Scanner;

/// <summary>
/// The Write scan (Tools). It talks to the pad itself over Bluetooth, no phone and no KeyLinker log: choose the controller,
/// read what will happen, Identify (reads only), Read-back (reads only), the Write test (writes back the bytes just read,
/// after a visible confirmation), visual tests the tester judges, then a zip that stays local until it is reviewed and sent.
/// </summary>
public sealed class WriteScanView : Grid
{
    public const string Title = "Write scan";
    static readonly string[] StepNames = ["Controller", "Identify", "Read-back", "Write test", "Visual tests", "Send"];
    static readonly string Folder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "X20CTLWriteScans");

    readonly Action close;
    readonly ReadReport? read;
    readonly PadScanSession session = new();
    readonly Grid stage = new();
    readonly StackPanel strip = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
    readonly UIElement[] pages = new UIElement[6];
    readonly TextBox otherName = new() { Width = 300, MinHeight = 40, FontSize = 14.5, Padding = new(12, 8, 12, 8), MaxLength = 60, Visibility = Visibility.Collapsed };
    readonly ComboBox model = new() { Width = 220, MinHeight = 40, FontSize = 14.5, Padding = new(12, 8, 12, 8) };
    readonly TextBlock identifyText = Wrap("", 15, "DS.TextSoft"), readText = Wrap("", 14, "DS.TextSoft"), writeText = Wrap("", 14.5, "DS.TextSoft"), sendText = Wrap("", 14.5, "DS.TextSoft");
    readonly TextBox summary = new() { IsReadOnly = true, FontFamily = new FontFamily("Consolas"), FontSize = 13, TextWrapping = TextWrapping.Wrap, Padding = new(14), MinHeight = 180, MaxHeight = 320, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, BorderThickness = new(0) };
    readonly CheckBox sendsWrite = new() { Style = DS.Style("DS.Toggle"), VerticalAlignment = VerticalAlignment.Center }, reviewed = new() { Style = DS.Style("DS.Toggle"), VerticalAlignment = VerticalAlignment.Center };
    Button identifyGo = null!, readGo = null!, writeGo = null!, sendGo = null!, saveGo = null!;
    int step; bool busy; List<PadRecord>? original; string? mapRestoreNote; PadRecord? mapped; string? savedTo;

    public WriteScanView(Action close, ReadReport? read = null)
    {
        this.close = close; this.read = read;
        Background = new SolidColorBrush(ThemeCatalog.R(Color.FromRgb(5, 9, 20))); ClipToBounds = true;
        Children.Add(new FacetBackdrop());
        var page = new Grid { Margin = new(40, 0, 40, 28) }; Children.Add(page);
        page.RowDefinitions.Add(new() { Height = new(84) }); page.RowDefinitions.Add(new() { Height = new(64) }); page.RowDefinitions.Add(new());
        var bar = new DockPanel { VerticalAlignment = VerticalAlignment.Center };
        var back = Action("", "Back to Tools", "ActionButton"); back.Click += (_, _) => close(); DockPanel.SetDock(back, Dock.Right); bar.Children.Add(back);
        bar.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Children = { Kit.Text("X20CTL", 22, "DS.Text", FontWeights.Bold), Kit.Text("   " + Title, 22, "DS.Text", FontWeights.SemiBold), Kit.Text("   asks the pad itself", 14, "DS.TextMuted") } });
        page.Children.Add(bar); Grid.SetRow(strip, 1); page.Children.Add(strip); Grid.SetRow(stage, 2); page.Children.Add(stage);
        foreach (var m in ControllerCheckView.Models) model.Items.Add(m);
        model.SelectedItem = ControllerCheckView.Pick;
        pages[0] = ChoosePage(); pages[1] = IdentifyPage(); pages[2] = ReadPage(); pages[3] = WritePage(); pages[4] = VisualPage(); pages[5] = SendPage();
        foreach (var p in pages) { p.Visibility = Visibility.Collapsed; stage.Children.Add(p); }
        model.SelectionChanged += (_, _) => otherName.Visibility = model.SelectedItem is ControllerCheckView.OtherModel ? Visibility.Visible : Visibility.Collapsed;
        poll.Tick += (_, _) => Poll();
        Show(0);
    }

    internal void ReviewOpen(int n) => Show(n);

    /// <summary>Close is refused while a test mapping is still on the pad: restore it first.</summary>
    public bool TryClose() => !busy && mapped is null;
    public void Shutdown() { poll.Stop(); ControllerRumble.Set(0, 0, 0); }

    // ---------------- steps ----------------
    void Show(int n)
    {
        step = n;
        for (int i = 0; i < pages.Length; i++) pages[i].Visibility = i == n ? Visibility.Visible : Visibility.Collapsed;
        strip.Children.Clear();
        for (int i = 0; i < StepNames.Length; i++)
        {
            bool on = i == n, done = i < n;
            strip.Children.Add(new Border { CornerRadius = new(15), Padding = new(14, 6, 14, 6), Margin = new(0, 0, 8, 0), Background = new SolidColorBrush(Color.FromArgb(on ? (byte)90 : done ? (byte)40 : (byte)16, 110, 160, 235)), Child = Kit.Text($"{i + 1}  {StepNames[i]}", 13, on ? "DS.Text" : "DS.TextSoft", on ? FontWeights.Bold : FontWeights.Normal) });
        }
        if (n == 4) poll.Start(); else poll.Stop();
        if (n == 5) RefreshSummary();
    }

    UIElement Scroll(params UIElement[] items)
    {
        var panel = new StackPanel { MaxWidth = 1100, HorizontalAlignment = HorizontalAlignment.Center, Margin = new(0, 8, 0, 24) };
        foreach (var i in items) panel.Children.Add(i);
        return new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    UIElement ChoosePage()
    {
        var go = Action("", "Next: Identify", "PrimaryButton"); go.IsEnabled = false; model.SelectionChanged += (_, _) => go.IsEnabled = model.SelectedItem is string m && m != ControllerCheckView.Pick && (m != ControllerCheckView.OtherModel || otherName.Text.Trim().Length >= 2); otherName.TextChanged += (_, _) => go.IsEnabled = otherName.Text.Trim().Length >= 2 || model.SelectedItem is string m2 && m2 != ControllerCheckView.OtherModel && m2 != ControllerCheckView.Pick;
        go.Click += (_, _) => { session.ChosenModel = model.SelectedItem is ControllerCheckView.OtherModel ? "Other: " + otherName.Text.Trim() : (string)model.SelectedItem; Show(1); };
        return Scroll(
            Kit.Text("Which controller are you testing?", 36, "DS.Text", FontWeights.Bold),
            Wrap("Pick the pad that is connected over Bluetooth. This is only a label for the report: the scan learns what the pad is from how it answers.", 16, "DS.TextSoft").Also(t => t.Margin = new(0, 8, 0, 18)),
            Kit.Text("Required: your report states this controller.", 13, "DS.TextMuted").Also(t => t.Margin = new(0, 0, 0, 6)),
            new StackPanel { Orientation = Orientation.Horizontal, Children = { model, otherName.Also(o => o.Margin = new(12, 0, 0, 0)) } },
            Card("What is going to happen", "1  Identify: the scan asks the pad who it is and what it reports. It only reads.\n2  Read-back: if the pad answers like an X20, it reads its button, curve and macro records and saves them to a file you can send.\n3  Write test: only if you confirm, it writes each record back exactly as read, then reads it again. Nothing should change.\n4  Visual tests: you check buttons, curves, triggers, vibration and macros on screen.\n5  A zip is made. It stays on this PC until you review it and press Send."),
            Card("Before you start", "Wake the pad and connect it by Bluetooth. Close KeyLinker on your phone: the pad talks to one app at a time. Nothing is sent online until the last step."),
            read is null ? new Border() : Card("Your Read scan comes along", "The Read scan you just finished (" + read.Describe() + ") is added to the report."),
            go.Also(b => { b.Margin = new(0, 18, 0, 0); b.HorizontalAlignment = HorizontalAlignment.Left; }));
    }

    UIElement IdentifyPage()
    {
        identifyGo = Action("", "Identify (reads only)", "PrimaryButton");
        identifyGo.Click += async (_, _) => await Run(identifyGo, async () =>
        {
            identifyText.Text = "Asking the pad…";
            var (raw, problem) = await PadScanBle.RunAsync(session.ChosenModel, ScanStage.Identify, (x, next) => PadScan.Identify(x, next));
            session.Raw = raw; session.Verdict = raw is null ? new(PadAnswer.Silent, "Your pad doesn't answer.", [problem ?? "No connection."]) : PadScan.Classify(raw);
            identifyText.Text = session.Verdict.Headline + "\n\n" + string.Join("\n", session.Verdict.Reasons.Select(r => "· " + r));
            nextIdentify.IsEnabled = session.Verdict.CanReadBack;
        });
        nextIdentify = Action("", "Next: Read-back", "PrimaryButton"); nextIdentify.IsEnabled = false; nextIdentify.Click += (_, _) => Show(2);
        var skip = Action("", "Skip to Send", "ActionButton"); skip.Click += (_, _) => Show(5);
        var x05 = Action("", "X05 Pro: probe D-Input channel", "ActionButton");
        x05.Click += async (_, _) => await Run(x05, async () =>
        {
            if (session.ChosenModel != "X05 Pro") { identifyText.Text = "This probe is only for the X05 Pro. Choose it in step 1."; return; }
            if (MessageBox.Show("This sends ONE command to the pad's D-Input channel (output report 05 01 00…), the one another owner reported. What it does is not known. Put the pad in D-Input mode first (hold Back + Start) and connect it by USB. Continue?", "This sends a command", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK) return;
            identifyText.Text = "Listing devices and probing…";
            var inventory = await Task.Run(() => WindowsInventory.Read(false));
            var r = session.X05 = await X05Probe.RunAsync(inventory);
            identifyText.Text = $"X05 channel: {r.Status}.\nReports before: {r.Before.Count}, after: {r.After.Count}. Byte positions that changed: {(r.ChangedBytes.Count == 0 ? "none" : string.Join(", ", r.ChangedBytes))}.\nThis shows the pad listens, not what the command means. It is saved in the report.";
        });
        return Scroll(Kit.Text("Identify", 36, "DS.Text", FontWeights.Bold),
            Wrap("One click. The scan asks the pad three questions — who it is (0x91), what it reports (0xB0) and its updater id (0x93) — and reads the answers. It sends nothing else.", 16, "DS.TextSoft").Also(t => t.Margin = new(0, 8, 0, 18)),
            Row(identifyGo, nextIdentify, skip, x05), identifyText.Also(t => t.Margin = new(0, 18, 0, 0)));
    }
    Button nextIdentify = null!;

    UIElement ReadPage()
    {
        readGo = Action("", "Read the records (reads only)", "PrimaryButton");
        readGo.Click += async (_, _) => await Run(readGo, async () =>
        {
            readText.Text = "Reading remap, curves and macros…";
            int slots = Math.Min(6, PadCheck.ParseCapabilities(session.Raw?.Menu)?.MacroSlots.Count(s => s.Length == 2 && s[0] == 'M' && char.IsDigit(s[1])) ?? 4);
            var (records, problem) = await PadScanBle.RunAsync(session.ChosenModel, ScanStage.ReadBack, (x, next) => PadScan.ReadAll(x, next, slots));
            session.Records.Clear();
            if (records is null) { readText.Text = problem ?? "The read failed."; session.Failure = problem; return; }
            session.Records.AddRange(records); original = [.. records];
            savedTo = SaveNow();
            readText.Text = $"{records.Count} records read.\n\n{PadScan.Describe(records)}\nSaved to {savedTo}";
            nextRead.IsEnabled = true;
        });
        nextRead = Action("", "Next: Write test", "PrimaryButton"); nextRead.IsEnabled = false; nextRead.Click += (_, _) => Show(3);
        var skip = Action("", "Skip to Visual tests", "ActionButton"); skip.Click += (_, _) => Show(4);
        return Scroll(Kit.Text("Read-back", 36, "DS.Text", FontWeights.Bold),
            Wrap("Reads the pad's button, curve and macro records and shows them. This proves the read path on your model. Every record is saved to a file you can send us.", 16, "DS.TextSoft").Also(t => t.Margin = new(0, 8, 0, 18)),
            Row(readGo, nextRead, skip), readText.Also(t => { t.Margin = new(0, 18, 0, 0); t.FontFamily = new FontFamily("Consolas"); }));
    }
    Button nextRead = null!;

    UIElement WritePage()
    {
        writeGo = Action("", "Run the write test", "PrimaryButton"); writeGo.IsEnabled = false;
        sendsWrite.Checked += (_, _) => writeGo.IsEnabled = session.Records.Count > 0 && !busy; sendsWrite.Unchecked += (_, _) => writeGo.IsEnabled = false;
        writeGo.Click += async (_, _) => await Run(writeGo, async () =>
        {
            if (sendsWrite.IsChecked != true || session.Records.Count == 0) return;
            writeText.Text = "Writing each record back and reading it again…";
            var before = session.Records.ToList();
            var (result, problem) = await PadScanBle.RunAsync(session.ChosenModel, ScanStage.WriteBack, (x, next) => PadScan.WriteBack(x, next, before));
            if (result is null) { writeText.Text = problem ?? "The write test could not run."; session.Failure = problem; return; }
            // a fresh connection: does the pad still hold it?
            var (after, p2) = await PadScanBle.RunAsync(session.ChosenModel, ScanStage.ReadBack, (x, next) => PadScan.ReadAll(x, next, before.Count(r => r.Name.StartsWith("macro"))));
            bool persisted = after is not null && !result.Stopped && PadScan.Persists(before, after);
            session.WriteBack = result with { Persisted = persisted };
            writeText.Text = result.Summary + "\n" + string.Join("\n", result.Items.Select(i => $"· {i.Name}: {i.Detail}")) + "\n\n"
                + $"Acknowledged: {(result.AllAcknowledged ? "yes" : "not every record")}  ·  Identical: {(result.AllIdentical ? "yes" : "no")}  ·  After reconnecting: {(persisted ? "still identical" : p2 ?? "not confirmed")}\n"
                + (result.AllAcknowledged ? "The pad accepts the X20's write command." : "The pad did not acknowledge every write: its commands may differ from the X20's.");
        });
        var next = Action("", "Next: Visual tests", "PrimaryButton"); next.Click += (_, _) => Show(4);
        return Scroll(Kit.Text("Write test", 36, "DS.Text", FontWeights.Bold),
            Wrap("For each record the scan writes back exactly the bytes it just read, then reads again. Nothing changes if it works. We learn whether the pad acknowledges the write, whether it keeps it after a reconnect, and whether it accepts the X20's command at all. If a record reads back different, the scan stops and puts that record back.", 16, "DS.TextSoft").Also(t => t.Margin = new(0, 8, 0, 18)),
            Card("This sends a write", "The write goes to the pad's own memory. It is the same bytes the pad just gave, but it is still a write. Only continue if you agree."),
            new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 14, 0, 0), Children = { sendsWrite, Kit.Text("   I understand this sends a write to my controller", 15, "DS.Text") } },
            Row(writeGo, next).Also(r => r.Margin = new(0, 16, 0, 0)), writeText.Also(t => t.Margin = new(0, 18, 0, 0)));
    }

    // ---------------- visual tests ----------------
    [StructLayout(LayoutKind.Sequential)] struct XState { public uint Packet; public ushort Buttons; public byte LT, RT; public short LX, LY, RX, RY; }
    [DllImport("xinput1_4.dll")] static extern uint XInputGetState(uint slot, out XState state);
    readonly DispatcherTimer poll = new() { Interval = TimeSpan.FromMilliseconds(40) };
    readonly Dictionary<string, Ellipse> faceDots = new();
    readonly Dictionary<string, Border> faceChips = new();
    readonly Border ltBar = new() { Height = 10, Width = 0, Background = Brushes.SteelBlue, HorizontalAlignment = HorizontalAlignment.Left, CornerRadius = new(5) }, rtBar = new() { Height = 10, Width = 0, Background = Brushes.SteelBlue, HorizontalAlignment = HorizontalAlignment.Left, CornerRadius = new(5) };
    readonly Ellipse stickDot = new() { Width = 12, Height = 12, Fill = Brushes.Orange };
    readonly Canvas curveCanvas = new() { Width = 260, Height = 260, Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)) };
    string? src, dst; readonly TextBlock mapText = Wrap("Pick the button you press, then the button the game should get.", 14.5, "DS.TextSoft"), macroText = Wrap("", 14, "DS.TextSoft"), vibText = Wrap("", 14.5, "DS.TextSoft"), resultsText = Wrap("", 13, "DS.TextMuted");
    static readonly (string Name, double X, double Y, Color C)[] Face = [("Y", 50, 0, Colors.Goldenrod), ("X", 0, 50, Colors.SteelBlue), ("B", 100, 50, Colors.IndianRed), ("A", 50, 100, Colors.SeaGreen)];

    UIElement VisualPage()
    {
        var face = new Canvas { Width = 170, Height = 170, Margin = new(0, 8, 28, 0) };
        foreach (var (name, x, y, color) in Face)
        {
            var dot = new Ellipse { Width = 50, Height = 50, Fill = new SolidColorBrush(Color.FromArgb(60, color.R, color.G, color.B)), Stroke = new SolidColorBrush(color), StrokeThickness = 2 };
            var chip = new Border { Width = 50, Height = 50, CornerRadius = new(25), Background = Brushes.Transparent, Cursor = System.Windows.Input.Cursors.Hand, Child = new TextBlock { Text = name, FontSize = 20, FontWeight = FontWeights.Bold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
            string n = name; chip.MouseLeftButtonUp += (_, _) => PickFace(n);
            Canvas.SetLeft(dot, x * 1.2); Canvas.SetTop(dot, y * 1.2); Canvas.SetLeft(chip, x * 1.2); Canvas.SetTop(chip, y * 1.2);
            face.Children.Add(dot); face.Children.Add(chip); faceDots[name] = dot; faceChips[name] = chip;
        }
        var applyMap = Action("", "Put this test mapping on the pad", "ActionButton"); var restoreMap = Action("", "Restore my mapping", "PrimaryButton");
        applyMap.Click += async (_, _) => await Run(applyMap, ApplyMap); restoreMap.Click += async (_, _) => await Run(restoreMap, RestoreMap);
        var buttonTest = TestBox("Map this button to this button",
            "Click the button you will press, then the button it should become. Putting it on the pad is a write: it needs the write test to have passed, and the scan restores your mapping afterwards. Then press the source on the pad: the target should light here.",
            new StackPanel { Orientation = Orientation.Horizontal, Children = { face, new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { mapText, Row(applyMap, restoreMap).Also(r => r.Margin = new(0, 12, 0, 0)) } } } }, "Button map");

        var curveTest = TestBox("Curves", "The sticks' stored curve drawn from the record, with your stick moving live on it. Move the left stick slowly: the orange dot should follow the line.",
            new StackPanel { Orientation = Orientation.Horizontal, Children = { curveCanvas, new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new(24, 0, 0, 0), Children = { Wrap("Drawn from the stored record, not measured from the pad.", 13, "DS.TextMuted") } } } }, "Curves");
        var triggerTest = TestBox("Triggers", "Pull each trigger slowly. The bars should follow your finger from 0 to full.",
            new StackPanel { Width = 360, Children = { Kit.Text("LT", 12, "DS.TextMuted"), ltBar, Kit.Text("RT", 12, "DS.TextMuted"), rtBar } }, "Triggers");
        var buzz = Action("", "Buzz both motors", "ActionButton"); buzz.Click += async (_, _) => { vibText.Text = await ControllerRumble.Test(true, true, 60) ? "Did you feel both motors?" : "No controller took the vibration command."; };
        var vibTest = TestBox("Vibration", "Plays a short buzz through Windows' own vibration call. It writes nothing to the pad's memory.", new StackPanel { Children = { buzz, vibText.Also(t => t.Margin = new(0, 8, 0, 0)) } }, "Vibration");
        var macroTest = TestBox("Macro reading", "The macro slots the scan read, as the pad holds them. Compare the step counts with what you saved in KeyLinker.", macroText, "Macro reading");
        return Scroll(Kit.Text("Visual tests", 36, "DS.Text", FontWeights.Bold),
            Wrap("Judge each one yourself and press the verdict. Skip anything that does not apply.", 16, "DS.TextSoft").Also(t => t.Margin = new(0, 8, 0, 14)),
            buttonTest, curveTest, triggerTest, vibTest, macroTest, resultsText.Also(t => t.Margin = new(0, 8, 0, 0)),
            Action("", "Next: Send", "PrimaryButton").Also(b => { b.Margin = new(0, 14, 0, 0); b.HorizontalAlignment = HorizontalAlignment.Left; b.Click += (_, _) => Show(5); }));
    }

    Border TestBox(string title, string help, UIElement body, string key)
    {
        var pass = Action("", "Works", "PrimaryButton"); var fail = Action("", "Doesn't match", "ActionButton"); var skip = Action("", "Skip", "ActionButton");
        pass.Click += (_, _) => Record(key, "pass"); fail.Click += (_, _) => Record(key, "fail"); skip.Click += (_, _) => Record(key, "skipped");
        return new Border { CornerRadius = new(14), Padding = new(20), Margin = new(0, 0, 0, 14), Background = new SolidColorBrush(Color.FromArgb(24, 255, 255, 255)), Child = new StackPanel { Children = {
            Kit.Text(title, 20, "DS.Text", FontWeights.Bold), Wrap(help, 14, "DS.TextSoft").Also(t => t.Margin = new(0, 4, 0, 12)), body, Row(pass, fail, skip).Also(r => r.Margin = new(0, 14, 0, 0)) } } };
    }
    void Record(string test, string outcome)
    {
        session.Visual.RemoveAll(v => v.Test == test); session.Visual.Add(new(test, outcome, ""));
        resultsText.Text = "Recorded: " + string.Join("  ·  ", session.Visual.Select(v => $"{v.Test} {v.Outcome}"));
    }

    void PickFace(string name)
    {
        if (src is null || dst is not null) { src = name; dst = null; } else dst = name;
        mapText.Text = dst is null ? $"You press {src}. Now choose what the game should get." : src == dst ? "Pick a different button." : $"{src} → {dst}. Put it on the pad, then press {src}.";
        foreach (var (n, chip) in faceChips) chip.BorderBrush = n == src || n == dst ? Brushes.White : null; foreach (var chip in faceChips.Values) chip.BorderThickness = new Thickness(chip.BorderBrush is null ? 0 : 2);
    }

    async Task ApplyMap()
    {
        if (src is null || dst is null || src == dst) { mapText.Text = "Pick two different buttons first."; return; }
        if (session.WriteBack is not { Stopped: false, AllAcknowledged: true } || original?.FirstOrDefault(r => r.WriteOp == 0x36) is not { } remap) { mapText.Text = "The write test has to pass first: this is a write."; return; }
        if (MessageBox.Show($"This writes a test mapping ({src} → {dst}) to your controller. The scan restores your mapping afterwards. Continue?", "This sends a write", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK) return;
        var bytes = ControllerRecords.BuildRemap(new Dictionary<string, string> { [src] = dst });
        mapped = remap;
        var (ok, problem) = await PadScanBle.RunAsync(session.ChosenModel, ScanStage.WriteBack, async (x, next) => await PadScan.Put(x, remap, bytes) && (await PadScan.Reread(x, next, remap)).SequenceEqual(bytes));
        mapText.Text = ok ? $"On the pad now: {src} → {dst}. Press {src} on the pad and watch {dst} light. Then restore." : (problem ?? "The pad did not take the test mapping.") + " Restore to be safe.";
    }
    async Task RestoreMap()
    {
        if (mapped is null) { mapText.Text = "Nothing to restore."; return; }
        var remap = mapped;
        var (ok, problem) = await PadScanBle.RunAsync(session.ChosenModel, ScanStage.WriteBack, async (x, next) => { await PadScan.Put(x, remap, remap.Bytes); return X20Ctl.Zone.ControllerRecords.SameRemap(await PadScan.Reread(x, next, remap), remap.Bytes); });
        if (ok) { mapped = null; mapRestoreNote = "restored and verified"; mapText.Text = "Your mapping is back and verified."; }
        else mapText.Text = (problem ?? "The pad does not show your mapping yet.") + " Try Restore again.";
    }

    void Poll()
    {
        XState s = default; bool found = false;
        for (uint i = 0; i < 4 && !found; i++) found = XInputGetState(i, out s) == 0;
        if (!found) return;
        var held = new Dictionary<string, bool> { ["A"] = (s.Buttons & 0x1000) != 0, ["B"] = (s.Buttons & 0x2000) != 0, ["X"] = (s.Buttons & 0x4000) != 0, ["Y"] = (s.Buttons & 0x8000) != 0 };
        foreach (var (name, dot) in faceDots) dot.Opacity = held[name] ? 1 : .55;
        ltBar.Width = s.LT / 255.0 * 360; rtBar.Width = s.RT / 255.0 * 360;
        DrawCurve(s.LX / 32767.0, s.LY / 32767.0);
    }

    void DrawCurve(double lx, double ly)
    {
        curveCanvas.Children.Clear();
        const double Size = 260;
        if (session.Records.FirstOrDefault(r => r.Name == "sticks") is not { } rec) return;
        CurveSetting c; try { c = ControllerRecords.ParseChannel(rec.Bytes, 0, false); } catch (FormatException) { return; }
        double Out(double t)
        {
            double inner = c.Inner / 100.0, outer = c.Outer / 100.0;
            if (t <= inner) return 0; if (t >= outer) return 1; double u = (t - inner) / (outer - inner);
            double p1x = c.Point1.X / 255.0, p1y = c.Point1.Y / 255.0, p2x = c.Point2.X / 255.0, p2y = c.Point2.Y / 255.0;
            double best = 0, bestD = 9; for (double v = 0; v <= 1.0001; v += .01) { double x = 3 * (1 - v) * (1 - v) * v * p1x + 3 * (1 - v) * v * v * p2x + v * v * v; if (Math.Abs(x - u) < bestD) { bestD = Math.Abs(x - u); best = 3 * (1 - v) * (1 - v) * v * p1y + 3 * (1 - v) * v * v * p2y + v * v * v; } }
            return best;
        }
        var line = new Polyline { Stroke = Brushes.LightSkyBlue, StrokeThickness = 2 };
        for (int i = 0; i <= 100; i++) { double t = i / 100.0; line.Points.Add(new Point(t * Size, Size - Out(t) * Size)); }
        curveCanvas.Children.Add(line);
        double mag = Math.Min(1, Math.Sqrt(lx * lx + ly * ly));
        Canvas.SetLeft(stickDot, mag * Size - 6); Canvas.SetTop(stickDot, Size - Out(mag) * Size - 6); curveCanvas.Children.Add(stickDot);
        var rows = session.Records.Where(r => r.Name.StartsWith("macro")).Select(r => $"{r.Name}: {(r.Bytes[0] == 0 ? "empty" : r.Bytes[0] + " steps")}");
        macroText.Text = rows.Any() ? string.Join("\n", rows) : "No macro records were read.";
    }

    // ---------------- send ----------------
    UIElement SendPage()
    {
        saveGo = Action("", "Save the zip on this PC", "ActionButton"); saveGo.Click += (_, _) => { savedTo = SaveNow(); sendText.Text = "Saved to " + savedTo; };
        sendGo = Action("", "Send to X20CTLADMIN", "PrimaryButton"); sendGo.IsEnabled = false;
        reviewed.Checked += (_, _) => sendGo.IsEnabled = !busy; reviewed.Unchecked += (_, _) => sendGo.IsEnabled = false;
        sendGo.Click += async (_, _) => await Run(sendGo, async () =>
        {
            try
            {
                var attachments = read is null ? null : (IReadOnlyList<(string, byte[])>)[("input/read-scan.txt", System.Text.Encoding.UTF8.GetBytes(read.Describe()))];
                var zip = PadScanReport.Zip(session, ScanFlow.AppVersion, attachments?.Select(a => (a.Item1, a.Item2)).ToList());
                string receipt = await WriteScanUpload.Send(zip, session.ChosenModel);
                sendText.Text = "Sent. Your receipt is " + receipt + ".";
            }
            catch (Exception e) when (e is WriteScanUpload.NotReady or System.Net.Http.HttpRequestException or InvalidDataException or TaskCanceledException) { sendText.Text = e.Message + "\nThe zip is still saved on this PC."; }
        });
        return Scroll(Kit.Text("Send", 36, "DS.Text", FontWeights.Bold),
            Wrap("This is everything that would go to X20CTLADMIN. It holds the replies and records above, your test verdicts and the app version. No Bluetooth address, no PC name, no account.", 16, "DS.TextSoft").Also(t => t.Margin = new(0, 8, 0, 14)),
            new Border { Child = summary, CornerRadius = new(12), Background = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)) },
            new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 16, 0, 0), Children = { reviewed, Kit.Text("   I reviewed this and agree to send it", 15, "DS.Text") } },
            Row(saveGo, sendGo).Also(r => r.Margin = new(0, 14, 0, 0)), sendText.Also(t => t.Margin = new(0, 14, 0, 0)));
    }
    void RefreshSummary() => summary.Text = PadScanReport.Summary(session);
    string SaveNow() => PadScanReport.Save(session, Folder, ScanFlow.AppVersion);

    // ---------------- helpers ----------------
    async Task Run(Button b, Func<Task> work)
    {
        if (busy) return; busy = true; b.IsEnabled = false;
        try { await work(); } catch (Exception e) when (e is IOException or InvalidOperationException or TimeoutException) { session.Failure = e.Message; sendText.Text = e.Message; }
        finally { busy = false; b.IsEnabled = true; if (b == writeGo) b.IsEnabled = sendsWrite.IsChecked == true; if (b == sendGo) b.IsEnabled = reviewed.IsChecked == true; }
    }
    static StackPanel Row(params UIElement[] items) { var p = new StackPanel { Orientation = Orientation.Horizontal }; foreach (var i in items) { if (i is FrameworkElement f) f.Margin = new(p.Children.Count == 0 ? 0 : 12, 0, 0, 0); p.Children.Add(i); } return p; }
    static Border Card(string title, string text) => new() { CornerRadius = new(14), Padding = new(20), Margin = new(0, 14, 0, 0), Background = new SolidColorBrush(Color.FromArgb(24, 255, 255, 255)), Child = new StackPanel { Children = { Kit.Text(title, 18, "DS.Text", FontWeights.Bold), Wrap(text, 14.5, "DS.TextSoft").Also(t => t.Margin = new(0, 6, 0, 0)) } } };
    static TextBlock Wrap(string text, double size, string brush) => new() { Text = text, FontFamily = DS.Body, FontSize = size, Foreground = DS.Brush(brush), TextWrapping = TextWrapping.Wrap, LineHeight = size * 1.45 };
    static Button Action(string icon, string text, string style) => new() { Style = DS.Style(style), Height = 44, MinHeight = 0, Padding = new(20, 0, 22, 0), FontSize = 15,
        Content = new StackPanel { Orientation = Orientation.Horizontal, Children = { Kit.Icon(icon, 14, "DS.Text").Also(i => i.Margin = new(0, 0, 10, 0)), Kit.Text(text, 15, "DS.Text", FontWeights.SemiBold) } } };
}
