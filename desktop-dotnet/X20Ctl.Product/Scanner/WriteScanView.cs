using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using X20Ctl.Zone.Capture;
namespace X20Ctl.Product.Scanner;

/// <summary>
/// The Write scan (Tools), the Read scan's partner. The Read scan watches what a controller reports; the Write scan
/// learns what KeyLinker writes to one. The owner changes one setting in KeyLinker on an Android phone
/// while the phone's own Bluetooth log records, then brings the log here. X20CTL sends nothing to the controller at
/// any point: it reads the recording, decodes KeyLinker frames when they are there, and shows the controller's identity,
/// what it says it offers, how that compares with the recorded X20, and the bytes the app changed.
/// </summary>
public sealed class WriteScanView : Grid
{
    private static readonly string[] StepNames = ["What you'll test", "Prepare the phone", "Do the test", "Bring the recording", "Results"];
    private static readonly string Folder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "X20CTLWriteScans");

    private readonly Action close;
    private readonly Grid stage = new();
    private readonly StackPanel strip = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
    private readonly Border warning = new();
    private int step;

    // what the owner plans to test
    private readonly TextBox model = Field("X20"), app = Field("KeyLinker"), setting = Field(""), original = Field(""), test = Field("");
    private readonly CheckBox undoable = new() { Style = DS.Style("DS.Toggle"), VerticalAlignment = VerticalAlignment.Center };
    private readonly List<StepNote> notes = new();
    private readonly Button planNext = Action("", "Next: prepare the phone", "PrimaryButton");

    // the guided test
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly Stopwatch handsOff = new();
    private double handsOffSeconds; private int part;
    private readonly TextBlock partTitle = new() { FontFamily = DS.Display, FontSize = 40, FontWeight = FontWeights.Bold, Foreground = DS.Brush("DS.Text"), TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock partSay = Wrap("", 17, "DS.TextSoft"), partCount = Wrap("", 15, "DS.TextMuted");
    private Button partDone = null!;

    // the recording and what came of it
    private WriteScanReport? report; private byte[]? log; private string? secondText, savedTo;
    private PhoneKind phone = PhoneKind.Samsung; private int prepareAt, bringAt; private string logExtension = "btsnoop";
    private bool iphone => phone == PhoneKind.IPhone;
    // sending: only after the owner reviewed the summary and agreed
    private readonly CheckBox sendTrace = new() { Style = DS.Style("DS.Toggle"), VerticalAlignment = VerticalAlignment.Center }, agree = new() { Style = DS.Style("DS.Toggle"), VerticalAlignment = VerticalAlignment.Center };
    private readonly Border sendPanel = new() { Visibility = Visibility.Collapsed };
    private readonly TextBlock sendStatus = Wrap("", 14, "DS.TextSoft");
    private Button? sendGo; private bool sending; private string? receipt;
    private bool unsaved, closeArmed, busy;
    private readonly TextBlock recordingStatus = Wrap("", 15, "DS.TextSoft"), resultStatus = Wrap("", 14.5, "DS.TextSoft");
    private readonly CheckBox keepRaw = new() { Style = DS.Style("DS.Toggle"), VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBox summary = new() { IsReadOnly = true, FontFamily = new FontFamily("Consolas"), FontSize = 13.5, TextWrapping = TextWrapping.Wrap, VerticalContentAlignment = VerticalAlignment.Top, Padding = new(16), VerticalScrollBarVisibility = ScrollBarVisibility.Auto, BorderThickness = new(0), Background = new SolidColorBrush(Color.FromArgb(70, 0, 0, 0)), Foreground = DS.Brush("DS.Text") };
    private readonly UniformGrid headline = new() { Rows = 1, Margin = new(-8, 0, -8, 14) };

    private readonly Border checkView = new() { Visibility = Visibility.Collapsed };
    private readonly TextBox checkText = new() { IsReadOnly = true, FontFamily = new FontFamily("Consolas"), FontSize = 13.5, TextWrapping = TextWrapping.Wrap, Padding = new(16), BorderThickness = new(0), MinHeight = 220, Background = new SolidColorBrush(Color.FromArgb(70, 0, 0, 0)), Foreground = DS.Brush("DS.Text"), Visibility = Visibility.Collapsed };
    private readonly TextBlock checkStatus = Wrap("", 14.5, "DS.TextSoft");
    private readonly Button checkSave = Action("\uE74E", "Save", "ActionButton").Also(b => b.Visibility = Visibility.Collapsed);
    private bool checking;
    private readonly ReadReport? read; // the Read scan that came just before, when the player went on from it

    public WriteScanView(Action close, ReadReport? read = null)
    {
        this.close = close; this.read = read;
        Background = new SolidColorBrush(Color.FromRgb(5, 9, 20)); ClipToBounds = true; AllowDrop = true;
        Children.Add(new FacetBackdrop());
        var page = new Grid { Margin = new(40, 0, 40, 28) }; Children.Add(page);
        page.RowDefinitions.Add(new() { Height = new(84) }); page.RowDefinitions.Add(new() { Height = new(64) }); page.RowDefinitions.Add(new());

        var bar = new DockPanel { VerticalAlignment = VerticalAlignment.Center };
        var back = Action("", "Back to Tools", "ActionButton"); back.Click += (_, _) => close(); DockPanel.SetDock(back, Dock.Right); bar.Children.Add(back);
        var blue = Kit.Blue;
        var badge = new Border { CornerRadius = new(16), Padding = new(14, 7, 16, 7), Margin = new(0, 0, 16, 0), VerticalAlignment = VerticalAlignment.Center, Background = new SolidColorBrush(Color.FromArgb(40, blue.R, blue.G, blue.B)), BorderBrush = new SolidColorBrush(Color.FromArgb(120, blue.R, blue.G, blue.B)), BorderThickness = new(1),
            Child = new StackPanel { Orientation = Orientation.Horizontal, Children = { Kit.Icon("", 14, "DS.AccentHi").Also(i => i.Margin = new(0, 0, 9, 0)), Kit.Text("X20CTL never writes · you change one setting in KeyLinker", 13.5, "DS.Text", FontWeights.SemiBold) } } };
        DockPanel.SetDock(badge, Dock.Right); bar.Children.Add(badge);
        var mark = new Image { Source = LiveController.Bitmap("Assets/icon.png"), Width = 34, Height = 34, Margin = new(0, 0, 14, 0) }; RenderOptions.SetBitmapScalingMode(mark, BitmapScalingMode.HighQuality);
        bar.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Children = { mark, Kit.Text("X20CTL", 22, "DS.Text", FontWeights.Bold), new Border { Width = 1, Height = 22, Background = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)), Margin = new(18, 0, 18, 0) },
            Kit.Text(WriteScanReport.Name, 22, "DS.Text", FontWeights.SemiBold), Kit.Text("   learns what KeyLinker writes", 14, "DS.TextMuted") } });
        page.Children.Add(bar);
        Grid.SetRow(strip, 1); page.Children.Add(strip);
        Grid.SetRow(stage, 2); page.Children.Add(stage);

        setting.TextChanged += (_, _) => Gate(); undoable.Checked += (_, _) => Gate(); undoable.Unchecked += (_, _) => Gate(); Gate();
        planNext.Click += (_, _) => Show(1);
        BuildWarning(); Children.Add(warning); BuildCheck(); Children.Add(checkView);
        timer.Tick += (_, _) => Tick();
        Drop += async (_, e) => { if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files && warning.Visibility != Visibility.Visible && checkView.Visibility != Visibility.Visible) await Read(files[0]); };
        Loaded += (_, _) => { if (DS.Motion) { Opacity = 0; BeginAnimation(OpacityProperty, DS.To(1, 240)); } };
        Show(0);
    }

    // ================= the warning: this is slow, and needs a phone =================
    private void BuildWarning()
    {
        warning.Background = new SolidColorBrush(Color.FromArgb(246, 5, 9, 20));
        var cards = new UniformGrid { Columns = 2, Margin = new(-8, 22, -8, 0) };
        foreach (var (title, text) in new[]
        {
            ("This can take a long time", "Plan on 20 to 40 minutes the first time: turning on a hidden phone setting, one small test, then getting a file from the phone to this PC. A bug report alone can take a few minutes to make."),
            ("You need a phone", "Android or iPhone, with KeyLinker and the controller connected to it by Bluetooth. Both can record Bluetooth: Android takes about five minutes to set up, an iPhone about fifteen."),
            ("X20CTL sends nothing", "X20CTL never talks to the controller here. You change one setting in KeyLinker yourself; the phone's own Bluetooth log records it, and X20CTL only reads that log."),
            read is null
                ? ("Read scan or Write scan?", "The Read scan (Controller not working?) watches what the controller reports to this PC. The Write scan learns what an app writes to the controller, so X20CTL can learn to change it too.")
                : ("Your Read scan comes along", "The Read scan you just finished (" + read.Describe() + ") is added to this report automatically. At the end you review both and press Send online, once."),
        })
            cards.Children.Add(Card(title, text));
        var check = Action("\uE702", "Check over Bluetooth, no phone", "PrimaryButton"); check.Click += (_, _) => Fx.Leave(warning, () => { warning.Visibility = Visibility.Collapsed; checkView.Visibility = Visibility.Visible; }, 1.02, 200);
        var go = Action("", "I have the time, use the phone", "ActionButton"); go.Click += (_, _) => Fx.Leave(warning, () => warning.Visibility = Visibility.Collapsed, 1.02, 200);
        var leave = Action("", "Back to Tools", "ActionButton"); leave.Click += (_, _) => close();
        var panel = new StackPanel { MaxWidth = 1180, HorizontalAlignment = HorizontalAlignment.Center, Margin = new(40, 36, 40, 30), Children = {
            Kit.Text("Before you start the " + WriteScanReport.Name, 40, "DS.Text", FontWeights.Bold),
            Wrap("You'll change one setting in KeyLinker while your phone records its Bluetooth traffic. X20CTL then shows which bytes the app sent for that change. One recording teaches one setting.", 17, "DS.TextSoft").Also(t => t.Margin = new(0, 8, 0, 0)),
            cards,
            Note("Nothing leaves this PC until you press Send online at the end, and you see what goes first. The findings are saved to Documents\\X20CTLWriteScans only when you press Save."),
            Note("No phone? Check over Bluetooth instead. It only reads, and changes nothing."),
            new WrapPanel { Margin = new(0, 22, 0, 0), Children = { check, go.Also(b => b.Margin = new(12, 0, 0, 0)), leave.Also(b => b.Margin = new(12, 0, 0, 0)) } } } };
        warning.Child = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    // ================= the controller check: Bluetooth, read-only, no phone =================
    private void BuildCheck()
    {
        checkView.Background = new SolidColorBrush(Color.FromArgb(246, 5, 9, 20));
        var askPro = Action("\uE702", "Check an X20 Pro", "PrimaryButton"); askPro.Click += async (_, _) => await RunCheck("x20_pro");
        var askX20 = Action("\uE702", "Check an X20", "ActionButton"); askX20.Click += async (_, _) => await RunCheck("x20");
        checkSave.Click += (_, _) => SaveCheck();
        var phoneRoute = Action("\uE8EA", "Use the phone recording instead", "ActionButton"); phoneRoute.Click += (_, _) => { checkView.Visibility = Visibility.Collapsed; Show(0); };
        var leave = Action("\uE72B", "Back to Tools", "ActionButton"); leave.Click += (_, _) => close();
        var panel = new StackPanel { MaxWidth = 1180, HorizontalAlignment = HorizontalAlignment.Center, Margin = new(40, 36, 40, 30), Children = {
            Kit.Text("Check over Bluetooth", 40, "DS.Text", FontWeights.Bold),
            Wrap("Three read-only questions: what the controller is, its firmware, and which settings it exposes. Nothing is written.", 17, "DS.TextSoft").Also(t => t.Margin = new(0, 8, 0, 0)),
            Note("Turn the controller on with Bluetooth. Disconnect it from KeyLinker on any phone first."),
            new WrapPanel { Margin = new(0, 22, 0, 0), Children = { askPro, askX20.Also(b => b.Margin = new(12, 0, 0, 0)) } },
            checkStatus.Also(t => t.Margin = new(0, 18, 0, 10)),
            checkText,
            new WrapPanel { Margin = new(0, 18, 0, 0), Children = { checkSave, phoneRoute.Also(b => b.Margin = new(12, 0, 0, 0)), leave.Also(b => b.Margin = new(12, 0, 0, 0)) } } } };
        checkView.Child = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    private async Task RunCheck(string modelId)
    {
        if (checking) return;
        checking = true; checkSave.Visibility = Visibility.Collapsed;
        checkStatus.Text = "Asking the controller. Keep it awake and near this PC.";
        try
        {
            var result = await PadBridge.Check(modelId);
            checkText.Text = result.Text; checkText.Visibility = Visibility.Visible;
            checkStatus.Text = result.Ok ? "Done. Nothing was changed on the controller." : "The check couldn't finish. Try again, or use the phone recording.";
            if (result.Ok) checkSave.Visibility = Visibility.Visible;
        }
        finally { checking = false; }
    }

    private void SaveCheck()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            string path = System.IO.Path.Combine(Folder, $"check-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
            File.WriteAllText(path, checkText.Text);
            checkStatus.Text = "Saved to " + path;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { checkStatus.Text = "Couldn't save: " + ex.Message; }
    }

    // ================= the five steps =================
    private void Show(int n)
    {
        step = n; timer.Stop(); handsOff.Reset();
        strip.Children.Clear();
        for (int i = 0; i < StepNames.Length; i++)
        {
            bool done = i < step, current = i == step;
            var c = current ? Kit.Blue : done ? Color.FromRgb(150, 175, 215) : Color.FromRgb(90, 104, 130);
            strip.Children.Add(new Border { CornerRadius = new(15), Padding = new(12, 6, 14, 6), Margin = new(0, 0, 10, 0), BorderThickness = new(1),
                BorderBrush = new SolidColorBrush(Color.FromArgb(current ? (byte)170 : (byte)60, c.R, c.G, c.B)), Background = new SolidColorBrush(Color.FromArgb(current ? (byte)60 : (byte)20, c.R, c.G, c.B)),
                Child = new StackPanel { Orientation = Orientation.Horizontal, Children = {
                    Kit.Text(done ? "✓" : (i + 1).ToString(), 13.5, current ? "DS.Text" : "DS.TextMuted", FontWeights.Bold).Also(t => t.Margin = new(0, 0, 8, 0)),
                    Kit.Text(StepNames[i], 14, current ? "DS.Text" : "DS.TextSoft", current ? FontWeights.SemiBold : FontWeights.Normal) } } });
        }
        stage.Children.Clear();
        stage.Children.Add(n switch { 0 => PlanPage(), 1 => PhonePage(), 2 => TestPage(), 3 => RecordingPage(), _ => ResultsPage() });
    }

    private void Gate() => planNext.IsEnabled = setting.Text.Trim().Length > 0 && undoable.IsChecked == true;
    private TestPlan Plan => new(model.Text.Trim(), app.Text.Trim(), setting.Text.Trim(), original.Text.Trim(), test.Text.Trim());

    private FrameworkElement PlanPage()
    {
        var form = new Grid { Margin = new(0, 6, 0, 0) };
        form.ColumnDefinitions.Add(new()); form.ColumnDefinitions.Add(new() { Width = new(20) }); form.ColumnDefinitions.Add(new());
        for (int i = 0; i < 5; i++) form.RowDefinitions.Add(new() { Height = GridLength.Auto });
        Put(form, Labelled("CONTROLLER (YOUR CLAIM: MODEL, OR BRAND AND MODEL)", Detach(model)), 0, 0); Put(form, Labelled("APP", Detach(app)), 0, 2);
        Put(form, Labelled("THE ONE SETTING YOU'LL CHANGE (for example: left motor strength)", Detach(setting)).Also(s => Grid.SetColumnSpan(s, 3)), 1, 0);
        Put(form, Labelled("ITS VALUE NOW", Detach(original)), 2, 0); Put(form, Labelled("WHAT YOU'LL CHANGE IT TO", Detach(test)), 2, 2);
        Put(form, new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 18, 0, 0), Children = { Detach(undoable), Kit.Text("I know how to put it back to its value now afterwards", 14.5, "DS.Text").Also(t => t.Margin = new(12, 0, 0, 0)) } }.Also(s => Grid.SetColumnSpan(s, 3)), 3, 0);
        var left = Kit.Panel("What you'll test", "One small setting you can undo", null, new StackPanel { Children = { form, new WrapPanel { Margin = new(0, 24, 0, 0), Children = { Detach(planNext) } } } });
        var right = Kit.Panel("Picking a good first test", null, null, new StackPanel { Children = {
            Wrap("Good choices: a vibration strength, a trigger or stick dead zone, one button remap, the auto-off timer.", 15, "DS.Text"),
            Wrap("Avoid: firmware updates, factory reset, calibration, pairing and mode switches. Those can't be undone by setting the value back, and they're not settings.", 15, "DS.TextSoft").Also(t => t.Margin = new(0, 12, 0, 0)),
            Wrap("Write down the value before you change it. One change per recording keeps the result readable.", 15, "DS.TextSoft").Also(t => t.Margin = new(0, 12, 0, 0)) } });
        return Columns(left, right);
    }

    private FrameworkElement PhonePage()
    {
        var choice = new UniformGrid { Rows = 1, Width = 480 }; string group = "phone" + Guid.NewGuid().ToString("N");
        foreach (var kind in new[] { PhoneKind.Samsung, PhoneKind.Android, PhoneKind.IPhone })
        {
            var r = new RadioButton { Content = PhoneGuides.Name(kind), Style = DS.Style("DS.Segment"), GroupName = group, IsChecked = phone == kind };
            r.Checked += (_, _) => { if (phone != kind) { phone = kind; prepareAt = bringAt = 0; Show(1); } };
            choice.Children.Add(r);
        }
        var picker = new StackPanel { Margin = new(0, 0, 0, 10), Children = { Kit.Text("WHICH PHONE HAS KEYLINKER?", 12, "DS.TextMuted", FontWeights.SemiBold).Also(t => t.Margin = new(2, 0, 0, 8)),
            new Border { CornerRadius = new(12), Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)), BorderBrush = new SolidColorBrush(Color.FromArgb(34, 255, 255, 255)), BorderThickness = new(1), Padding = new(1), HorizontalAlignment = HorizontalAlignment.Left, Child = choice },
            Wrap("It doesn't have to be your phone: a borrowed phone or a tablet with KeyLinker works too.", 13.5, "DS.TextMuted").Also(t => t.Margin = new(2, 8, 0, 6)) } };
        return new PhoneGuide("Prepare the phone", PhoneGuides.Prepare(phone), prepareAt, i => prepareAt = i,
            () => { notes.Clear(); notes.Add(new(PhoneGuides.Name(phone).ToLowerInvariant() + " ready", DateTime.UtcNow)); part = 0; Show(2); },
            "The phone is ready", above: picker, backOut: () => Show(0));
    }

    /// <summary>The test, one action at a time. After each one the phone is left alone for a few seconds, so the change
    /// stands apart from what comes next in the recording.</summary>
    private (string Title, string Say, string Button, double HandsOff)[] Parts()
    {
        var p = Plan;
        string a = p.App.Length > 0 ? p.App : "KeyLinker", s = p.Setting, from = p.OriginalValue.Length > 0 ? p.OriginalValue : "its value now", to = p.TestValue.Length > 0 ? p.TestValue : "the new value";
        return [
            ("Open the app", $"Open {a} on the phone and connect the controller the way you always do. Wait until the app shows it.", "It's connected", 5),
            ("Go to the page", $"Open the page with {s}. Don't change anything yet.", "I'm on that page", 5),
            ("Make the change", $"Change {s} from {from} to {to}. If the app has a Save or Apply button, press it.", "I changed it", 5),
            ("Change it back", $"Set {s} back to {from}, and save again if the app needs it.", "I changed it back", 5),
            ("Close the app", $"Close {a} completely. The recording now holds the whole test.", "I closed it", 0),
        ];
    }

    private FrameworkElement TestPage()
    {
        if (part == 0) { bringAt = 0; recordingStatus.Text = ""; }   // a new test: its recording is made from the first step
        partDone = Action("", "", "PrimaryButton"); partDone.Click += (_, _) => DonePart();
        var restart = Action("", "Start the test again", "ActionButton"); restart.Click += (_, _) => { notes.RemoveAll(n => !n.Event.EndsWith(" ready")); part = 0; Show(2); };
        var help = PhoneGuide.HelpPanel([
            ("The app doesn't find the controller", "Turn the controller off and on, in the mode you always use with the app. Check Bluetooth is on: you restarted it while preparing the phone."),
            ("I changed something else by mistake", "Press Start the test again and begin from the top. One recording should hold one change."),
            ("The app has no Save or Apply button", "That's fine: many apps save as soon as you change something."),
            ("The controller was already paired", "That's fine. Leave it paired."),
            ("I pressed the button too early", "Press Start the test again. The pauses keep each action apart, so the result is easier to read.")]);
        help.Visibility = Visibility.Collapsed;
        var stuck = Action("", "I'm stuck", "ActionButton"); stuck.Click += (_, _) => help.Visibility = help.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        var left = Kit.Panel("Do the test", "Follow each step on the phone, then confirm here", null, new StackPanel { Children = {
            Detach(partTitle), Detach(partSay).Also(t => t.Margin = new(0, 8, 0, 0)), Detach(partCount).Also(t => t.Margin = new(0, 14, 0, 0)),
            new WrapPanel { Margin = new(0, 22, 0, 0), Children = { partDone, restart.Also(b => b.Margin = new(12, 0, 0, 0)), stuck.Also(b => b.Margin = new(12, 0, 0, 0)) } }, help } });
        var list = new StackPanel();
        var parts = Parts();
        for (int i = 0; i < parts.Length; i++)
            list.Children.Add(Kit.Text((i < part ? "✓  " : i == part ? "▶  " : "     ") + parts[i].Title, 15.5, i == part ? "DS.Text" : i < part ? "DS.TextSoft" : "DS.TextMuted", i == part ? FontWeights.SemiBold : FontWeights.Normal).Also(t => t.Margin = new(0, 0, 0, 10)));
        var right = Kit.Panel("The test", "Five steps", null, new StackPanel { Children = { list,
            Wrap("The pauses matter: they keep each action apart in the recording, so the change can be told from the app's own background traffic.", 14, "DS.TextSoft").Also(t => t.Margin = new(0, 8, 0, 0)) } });
        ShowPart();
        return Columns(left, right);
    }

    private void ShowPart()
    {
        var parts = Parts();
        if (part >= parts.Length) return;
        partTitle.Text = parts[part].Title; partSay.Text = parts[part].Say; SetLabel(partDone, parts[part].Button);
        partDone.IsEnabled = true; partCount.Text = $"Step {part + 1} of {parts.Length}";
    }

    private void DonePart()
    {
        var parts = Parts();
        if (part >= parts.Length || handsOff.IsRunning) return;
        notes.Add(new(parts[part].Title.ToLowerInvariant(), DateTime.UtcNow));
        handsOffSeconds = parts[part].HandsOff;
        if (handsOffSeconds <= 0) { part++; Show(part >= parts.Length ? 3 : 2); return; }
        partDone.IsEnabled = false; partTitle.Text = "Hands off"; partSay.Text = "Leave the phone and the controller alone for a moment.";
        handsOff.Restart(); timer.Start(); Tick();
    }

    private void Tick()
    {
        if (!handsOff.IsRunning) { timer.Stop(); return; }
        double left = handsOffSeconds - handsOff.Elapsed.TotalSeconds;
        if (left > 0) { partCount.Text = $"Next step in {Math.Ceiling(left):0} s"; return; }
        handsOff.Reset(); timer.Stop(); part++;
        Show(part >= Parts().Length ? 3 : 2);   // redraws the list with this step ticked, and shows the next
    }

    private FrameworkElement RecordingPage()
    {
        if (recordingStatus.Text.Length == 0) recordingStatus.Text = "Waiting for the recording. You can also drop the file on this window.";
        string archive = iphone ? "sysdiagnose" : "bug report";
        var extra = new StackPanel { Margin = new(0, 16, 0, 0), Children = { Detach(recordingStatus),
            Wrap($"Privacy: a {archive} holds a lot about the phone. X20CTL takes only the Bluetooth log out of it and keeps nothing unless you press Save; nothing is uploaded unless you press Send. Delete the {archive} from the phone and this PC when you're done.", 13.5, "DS.TextMuted").Also(t => t.Margin = new(0, 12, 0, 0)) } };
        return new PhoneGuide("Bring the recording", PhoneGuides.Bring(phone), bringAt, i => bringAt = i, async () => await Choose(false), "Choose the recording",
            extra, backOut: () => { part = 0; Show(2); }, backOutLabel: "Back to the test");
    }

    private async Task Choose(bool second)
    {
        if (busy) return;
        var dialog = new Microsoft.Win32.OpenFileDialog { Title = second ? "Choose a second recording to compare" : "Choose the recording", Filter = "Bluetooth recordings (*.zip;*.log;*.cfa;*.gz;*.tar;*.pklg)|*.zip;*.log;*.cfa;*.gz;*.tar;*.pklg|All files (*.*)|*.*" };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
        if (second) await Compare(dialog.FileName); else await Read(dialog.FileName);
    }

    private async Task Read(string path)
    {
        if (busy) return;
        busy = true; var plan = Plan; var steps = notes.ToList();
        // the status line lives on the guide's last step: go there, wherever the file was dropped
        int open = PhoneGuides.Bring(phone).Length - 1;
        if (step != 3 || bringAt != open) { bringAt = open; Show(3); }
        recordingStatus.Text = "Reading the recording. A bug report can take a minute.";
        try
        {
            var (built, raw, extension) = await Task.Run(() => { var r = BtSnoop.LoadAny(path); return (WriteScanReport.Build(plan, steps, r.Records, r.Source), r.Log, r.Extension); });
            report = built; log = raw; logExtension = extension; secondText = null; savedTo = null; unsaved = true; closeArmed = false;
            receipt = null; agree.IsChecked = false; sendTrace.IsChecked = false; sendStatus.Text = ""; sendPanel.Visibility = Visibility.Collapsed;
            recordingStatus.Text = ""; resultStatus.Text = "Not saved yet.";
            Show(4);
        }
        catch (Exception e) when (e is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            recordingStatus.Text = "That file couldn't be used: " + e.Message;
        }
        finally { busy = false; }
    }

    private async Task Compare(string path)
    {
        if (busy || report is null) return;
        busy = true; resultStatus.Text = "Reading the second recording.";
        try
        {
            var other = await Task.Run(() => { var (records, source, _) = BtSnoop.Load(path); return WriteScanReport.Build(report.Plan, [], records, source); });
            var (onlyA, onlyB) = WriteScanReport.CompareRecordings(report.KeyLinker, other.KeyLinker);
            var s = new System.Text.StringBuilder();
            s.AppendLine("COMPARED WITH A SECOND RECORDING (" + other.Source + ")");
            if (!report.IsKeyLinker || !other.IsKeyLinker) s.AppendLine("  Both recordings need KeyLinker frames to compare them message by message.");
            else if (onlyA.Count == 0 && onlyB.Count == 0) s.AppendLine("  The two recordings hold the same messages.");
            else
            {
                s.AppendLine("  Only in this recording:"); foreach (var m in onlyA) s.AppendLine("    " + m);
                if (onlyA.Count == 0) s.AppendLine("    nothing");
                s.AppendLine("  Only in the second one:"); foreach (var m in onlyB) s.AppendLine("    " + m);
                if (onlyB.Count == 0) s.AppendLine("    nothing");
            }
            secondText = s.ToString(); unsaved = true;
            summary.Text = report.Summary() + "\n" + secondText;
            resultStatus.Text = "The comparison is added to the findings.";
        }
        catch (Exception e) when (e is InvalidDataException or IOException or UnauthorizedAccessException) { resultStatus.Text = "That file couldn't be used: " + e.Message; }
        finally { busy = false; }
    }

    static string Count(int n, string noun) => $"{n} {noun}{(n == 1 ? "" : "s")}";

    private FrameworkElement ResultsPage()
    {
        if (report is null) return RecordingPage();
        headline.Children.Clear();
        var id = report.Fingerprint.Identity;
        headline.Children.Add(Card("Protocol", report.IsKeyLinker ? $"KeyLinker: {report.KeyLinker.Frames.Count} frames decoded" : report.Filtered ? "Unreadable: the Filtered log cuts every message short"
            : "No KeyLinker frames. Inconclusive: another protocol, or a partial log; the raw writes are grouped below."));
        headline.Children.Add(Card("Controller says", id is null ? "No identity reply in this recording" : $"vendor {id.Vid}, product {id.Pid}, firmware {id.Version}"));
        headline.Children.Add(Card("Versus the X20", report.Differences.Count == 0 ? (id is null && report.KeyLinker.Descriptor is null ? "Not enough to compare" : "The same as the recorded X20 in all it reports") : $"{report.Differences.Count} differences"));
        headline.Children.Add(Card("Changes found", report.IsKeyLinker ? $"{Count(report.KeyLinker.Changes.Count, "write")}, {Count(report.KeyLinker.Changes.Sum(c => c.Bytes.Count), "byte")} changed, {report.KeyLinker.Changes.Count(c => c.ReadBack == "matches")} read back as stored" : $"{report.Shapes.Count} kinds of write"));
        summary.Text = report.Summary() + (secondText is null ? "" : "\n" + secondText);

        var save = Action("", "Save the findings", "PrimaryButton"); save.Click += (_, _) => Save();
        var open = Action("", "Open the folder", "ActionButton"); open.Click += (_, _) => OpenFolder();
        var compare = Action("", "Compare with a second recording", "ActionButton"); compare.Click += async (_, _) => await Choose(true);
        compare.ToolTip = "The same test recorded twice, or the same change on a second controller: lists the messages only one recording holds";
        var again = Action("", "Test another setting", "ActionButton"); again.Click += (_, _) => { if (unsaved && !closeArmed) { closeArmed = true; resultStatus.Text = "These findings aren't saved. Press again to start over without them."; return; } report = null; log = null; secondText = null; closeArmed = false; unsaved = false; resultStatus.Text = ""; Show(0); };
        var send = Action("\uE724", "Send online", "ActionButton"); send.Click += (_, _) => { sendPanel.Visibility = sendPanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible; };
        send.ToolTip = read is null ? "Send these findings to the X20CTL team, so your controller can be supported" : "Send these findings and the Read scan report to the X20CTL team in one go";
        BuildSendPanel();
        var actions = new WrapPanel { Children = { save, send.Also(b => b.Margin = new(12, 0, 0, 0)), open.Also(b => b.Margin = new(12, 0, 0, 0)), compare.Also(b => b.Margin = new(12, 0, 0, 0)), again.Also(b => b.Margin = new(12, 0, 0, 0)) } };
        var keep = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 14, 0, 0), Children = { Detach(keepRaw), Kit.Text("Also save a copy of the Bluetooth log (it can hold other devices' names and addresses)", 13.5, "DS.TextSoft").Also(t => t.Margin = new(12, 0, 0, 0)) } };
        var foot = new StackPanel { Margin = new(0, 14, 0, 0), Children = { actions, Detach(sendPanel), keep, Detach(resultStatus).Also(t => t.Margin = new(0, 12, 0, 0)),
            Wrap(iphone ? "When you're done: remove Apple's Bluetooth profile in Settings, General, VPN & Device Management."
                : "When you're done: in Developer options, turn \"Enable Bluetooth HCI snoop log\" off again.", 13.5, "DS.TextMuted").Also(t => t.Margin = new(0, 8, 0, 0)) } };
        var dock = new DockPanel();
        var verdict = VerdictPanel(report.Verdict());
        DockPanel.SetDock(verdict, Dock.Top); dock.Children.Add(verdict);
        DockPanel.SetDock(headline, Dock.Top); dock.Children.Add(Detach(headline));
        DockPanel.SetDock(foot, Dock.Bottom); dock.Children.Add(foot);
        dock.Children.Add(new Border { CornerRadius = new(12), ClipToBounds = true, Child = Detach(summary) });
        return Kit.Panel("Results", report.Plan.Setting.Length > 0 ? $"{report.Plan.Setting}, in {report.Source}" : report.Source, null, dock);
    }

    /// <summary>The verdict first: how sure the scan is, what it learned, what that rests on, and, when it falls short,
    /// what to do differently with a way straight back to the step that fixes it.</summary>
    private FrameworkElement VerdictPanel(WriteScanVerdict v)
    {
        var c = v.Level switch { "High" => Color.FromRgb(52, 211, 153), "Medium" => Color.FromRgb(245, 180, 91), _ => Color.FromRgb(232, 54, 79) };
        var pill = new Border { CornerRadius = new(12), Padding = new(12, 5, 12, 5), Margin = new(0, 0, 14, 0), VerticalAlignment = VerticalAlignment.Center, BorderThickness = new(1),
            Background = new SolidColorBrush(Color.FromArgb(48, c.R, c.G, c.B)), BorderBrush = new SolidColorBrush(Color.FromArgb(150, c.R, c.G, c.B)),
            Child = Kit.Text(v.Level == "None" ? "NOTHING LEARNED" : v.Level.ToUpperInvariant() + " CONFIDENCE", 12.5, "DS.Text", FontWeights.Bold).Also(t => t.Foreground = new SolidColorBrush(c)) };
        var top = new DockPanel { Children = { pill, Kit.Text(v.Headline, 21, "DS.Text", FontWeights.Bold).Also(t => { t.TextWrapping = TextWrapping.Wrap; t.VerticalAlignment = VerticalAlignment.Center; }) } };
        DockPanel.SetDock(pill, Dock.Left);
        var checks = new StackPanel { Margin = new(0, 12, 0, 0) };
        foreach (var check in v.Checks.Take(7))
            checks.Children.Add(new DockPanel { Margin = new(0, 0, 0, 5), Children = {
                Kit.Text(check.Passed ? "✓" : "✕", 14.5, "DS.Text", FontWeights.Bold).Also(t => { t.Width = 22; t.Foreground = new SolidColorBrush(check.Passed ? Color.FromRgb(52, 211, 153) : Color.FromRgb(245, 180, 91)); DockPanel.SetDock(t, Dock.Left); }),
                Wrap(check.Text, 14, check.Passed ? "DS.TextSoft" : "DS.Text") } });
        var body = new Grid(); body.ColumnDefinitions.Add(new() { Width = new(1.3, GridUnitType.Star) }); body.ColumnDefinitions.Add(new() { Width = new(24) }); body.ColumnDefinitions.Add(new());
        body.Children.Add(checks);
        if (v.Fixes.Count > 0)
        {
            bool phoneFix = report?.Filtered == true || report?.Messages == 0;
            var again = Action("", phoneFix ? "Set up the phone again" : "Do the test again", "ActionButton");
            again.Click += (_, _) => { notes.RemoveAll(n => !n.Event.EndsWith(" ready")); part = 0; if (phoneFix) { prepareAt = 0; Show(1); } else Show(2); };
            var fixes = new StackPanel { Margin = new(0, 12, 0, 0), Children = { Kit.Text("FOR A BETTER RECORDING", 12, "DS.TextMuted", FontWeights.SemiBold).Also(t => t.Margin = new(0, 0, 0, 6)) } };
            foreach (var f in v.Fixes.Take(4)) fixes.Children.Add(Wrap("•  " + f, 14, "DS.Text").Also(t => t.Margin = new(0, 0, 0, 5)));
            fixes.Children.Add(new WrapPanel { Margin = new(0, 6, 0, 0), Children = { again } });
            Grid.SetColumn(fixes, 2); body.Children.Add(fixes);
        }
        return new Border { CornerRadius = new(14), Margin = new(0, 0, 0, 6), Padding = new(20, 16, 20, 14), BorderThickness = new(1),
            BorderBrush = new SolidColorBrush(Color.FromArgb(90, c.R, c.G, c.B)), Background = new LinearGradientBrush(Color.FromArgb(150, 20, 34, 70), Color.FromArgb(110, 10, 18, 40), 90),
            Child = new StackPanel { Children = { top, body } } };
    }

    private void BuildSendPanel()
    {
        if (sendGo != null) return;
        sendGo = Action("\uE724", "Send", "PrimaryButton"); sendGo.IsEnabled = false; sendGo.Click += async (_, _) => await Send();
        agree.Checked += (_, _) => sendGo.IsEnabled = receipt is null && !sending; agree.Unchecked += (_, _) => sendGo.IsEnabled = false;
        sendPanel.CornerRadius = new(14); sendPanel.Margin = new(0, 14, 0, 0); sendPanel.Padding = new(18, 14, 18, 14); sendPanel.BorderThickness = new(1);
        sendPanel.BorderBrush = new SolidColorBrush(Color.FromArgb(110, 61, 139, 255)); sendPanel.Background = new SolidColorBrush(Color.FromArgb(40, 61, 139, 255));
        sendPanel.Child = new StackPanel { Children = {
            Kit.Text(read is null ? "Send these findings online" : "Send the Write scan and the Read scan online", 16.5, "DS.Text", FontWeights.SemiBold),
            Wrap("What goes to X20CTLADMIN, the X20CTL team's private report site: the summary above, the findings, the timeline and every decoded message (these can include the controller's name and an id). Nothing else from the phone.", 14, "DS.TextSoft").Also(t => t.Margin = new(0, 6, 0, 0)),
            new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 12, 0, 0), Children = { sendTrace, Kit.Text("Also send the Bluetooth data packets: the controller's connection only, around the test, without Bluetooth's own messages (where device names and addresses travel)", 13.5, "DS.Text").Also(t => t.Margin = new(12, 0, 0, 0)) } },
            new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 10, 0, 0), Children = { agree, Kit.Text("I've read the summary above and I'm happy to send it", 13.5, "DS.Text").Also(t => t.Margin = new(12, 0, 0, 0)) } },
            new WrapPanel { Margin = new(0, 12, 0, 0), Children = { sendGo } },
            sendStatus.Also(t => t.Margin = new(0, 10, 0, 0)),
            Wrap("You can also send the saved folder to @" + ScanFlow.Discord + " on Discord.", 13, "DS.TextMuted").Also(t => t.Margin = new(0, 8, 0, 0)) } };
    }

    private async Task Send()
    {
        if (report is null || sending || agree.IsChecked != true || receipt != null) return;
        sending = true; sendGo!.IsEnabled = false; sendStatus.Text = "Sending to X20CTLADMIN…";
        try
        {
            var attached = read?.Files(X20Ctl.Zone.Capture.WriteScanReport.MaxResearchFileBytes) ?? [];
            var zip = report.ResearchZip(sendTrace.IsChecked == true, ScanFlow.AppVersion,
                (secondText ?? "") + (attached.Count > 0 ? $"\nRead scan included: {read!.Describe()} ({attached.Count} files under input/read-scan/)\n" : ""), attached);
            receipt = await WriteScanUpload.Send(zip, report.Plan.Model);
            sendStatus.Text = $"Sent to X20CTLADMIN · receipt {receipt}. Keep the receipt if you want to ask about it.";
        }
        catch (WriteScanUpload.NotReady e) { sendStatus.Text = e.Message; }
        catch (Exception e) when (e is System.Net.Http.HttpRequestException or TaskCanceledException or InvalidDataException or IOException or System.Text.Json.JsonException) { sendStatus.Text = "Not sent: " + e.Message; }
        finally { sending = false; sendGo.IsEnabled = receipt is null && agree.IsChecked == true; }
    }

    private bool Save()
    {
        if (report is null) return true;
        if (ReviewSandbox.Active) { unsaved = false; resultStatus.Text = "Review sandbox: findings not written."; return true; }
        try
        {
            savedTo = report.Save(Folder, keepRaw.IsChecked == true ? log : null, secondText, logExtension);
            unsaved = false; closeArmed = false;
            resultStatus.Text = "Saved to " + savedTo + ". Read README.txt before sharing: the message list can include the controller's name and an id.";
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { resultStatus.Text = "Couldn't save: " + e.Message; return false; }
    }

    private void OpenFolder()
    {
        if (ReviewSandbox.Active) return;
        try { Directory.CreateDirectory(Folder); Process.Start(new ProcessStartInfo("explorer.exe", "\"" + (savedTo ?? Folder) + "\"") { UseShellExecute = true }); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception) { resultStatus.Text = "Couldn't open the folder: " + e.Message; }
    }

    /// <summary>Unsaved findings: the first Back says so, the second leaves anyway.</summary>
    public bool TryClose()
    {
        if (busy) return false;
        if (unsaved && !closeArmed) { closeArmed = true; resultStatus.Text = "These findings aren't saved. Press Back again to leave without them."; return false; }
        return true;
    }
    public void Shutdown() { timer.Stop(); handsOff.Reset(); }

    // review captures (--review-probe): render each page without a phone
    internal void ReviewOpen(int n, TestPlan? plan = null)
    {
        warning.Visibility = Visibility.Collapsed;
        if (plan != null) { model.Text = plan.Model; app.Text = plan.App; setting.Text = plan.Setting; original.Text = plan.OriginalValue; test.Text = plan.TestValue; undoable.IsChecked = true; }
        Show(n);
    }
    internal Task ReviewRead(string path) => Read(path);
    internal void ReviewPhone(bool onIphone) => phone = onIphone ? PhoneKind.IPhone : PhoneKind.Samsung;
    internal void ReviewPhone(PhoneKind kind) { phone = kind; prepareAt = bringAt = 0; }
    internal void ReviewGuide(int prepare, int bring) { prepareAt = prepare; bringAt = bring; }
    internal (string Text, bool Shown, int Guide) ReviewStatus() => (recordingStatus.Text, recordingStatus.IsVisible, stage.Children.Count > 0 && stage.Children[0] is PhoneGuide g ? g.Index : -1);
    internal void ReviewStuck() { if (stage.Children.Count > 0 && stage.Children[0] is PhoneGuide g) g.Stuck(true); }
    /// <summary>Confirms every step of the guided test with short pauses; returns the step it ends on (3 when it reaches the recording).</summary>
    internal async Task<int> ReviewWalkTest()
    {
        part = 0; Show(2);
        for (int i = 0; i < Parts().Length; i++) { DonePart(); if (handsOff.IsRunning) handsOffSeconds = 0.2; await Task.Delay(500); }
        return step;
    }
    internal async Task ReviewSend() { sendPanel.Visibility = Visibility.Visible; agree.IsChecked = true; sendTrace.IsChecked = true; await Send(); }

    // ================= small pieces =================
    private static FrameworkElement Columns(UIElement left, UIElement right)
    {
        var g = new Grid();
        g.ColumnDefinitions.Add(new() { Width = new(1.6, GridUnitType.Star) }); g.ColumnDefinitions.Add(new() { Width = new(20) }); g.ColumnDefinitions.Add(new());
        g.Children.Add(left); Grid.SetColumn(right, 2); g.Children.Add(right);
        return new ScrollViewer { Content = g, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }
    /// <summary>Controls kept across steps are moved into each new page; take them out of the old one first.</summary>
    private static T Detach<T>(T e) where T : FrameworkElement
    {
        switch (e.Parent) { case Panel p: p.Children.Remove(e); break; case Decorator d: d.Child = null; break; case ContentControl c: c.Content = null; break; }
        return e;
    }
    private static void Put(Grid g, UIElement e, int row, int column) { Grid.SetRow(e, row); Grid.SetColumn(e, column); g.Children.Add(e); }
    private static TextBox Field(string text) => new() { Text = text, MinHeight = 40, FontSize = 14.5, Padding = new(12, 8, 12, 8), MaxLength = 120, VerticalContentAlignment = VerticalAlignment.Center };
    private static StackPanel Labelled(string label, FrameworkElement control) => new() { Margin = new(0, 0, 0, 14), Children = { Kit.Text(label, 11.5, "DS.TextMuted", FontWeights.SemiBold).Also(t => t.Margin = new(2, 0, 0, 6)), control } };
    private static TextBlock Wrap(string text, double size, string brush) => new() { Text = text, FontFamily = DS.Body, FontSize = size, Foreground = DS.Brush(brush), TextWrapping = TextWrapping.Wrap, LineHeight = size * 1.45 };
    private static Border Card(string title, string text) => new()
    {
        CornerRadius = new(14), Margin = new(8), Padding = new(18, 14, 18, 14), BorderThickness = new(1), BorderBrush = new SolidColorBrush(Color.FromArgb(46, 140, 180, 255)),
        Background = new LinearGradientBrush(Color.FromArgb(160, 20, 34, 70), Color.FromArgb(120, 10, 18, 40), 90),
        Child = new StackPanel { Children = { Kit.Text(title, 16.5, "DS.Text", FontWeights.SemiBold), Wrap(text, 14, "DS.TextSoft").Also(t => t.Margin = new(0, 6, 0, 0)) } },
    };
    private static Border Note(string text) => new()
    {
        CornerRadius = new(14), Margin = new(0, 18, 0, 0), Padding = new(18, 14, 18, 14), BorderThickness = new(1), BorderBrush = new SolidColorBrush(Color.FromArgb(110, 61, 139, 255)), Background = new SolidColorBrush(Color.FromArgb(40, 61, 139, 255)),
        Child = Wrap(text, 14.5, "DS.Text"),
    };
    private static Border Numbered(string n, string title, string text) => new()
    {
        CornerRadius = new(14), Margin = new(0, 0, 0, 10), Padding = new(18, 12, 18, 12), BorderThickness = new(1), BorderBrush = new SolidColorBrush(Color.FromArgb(46, 140, 180, 255)), Background = new SolidColorBrush(Color.FromArgb(70, 16, 28, 58)),
        Child = new DockPanel { Children = {
            Kit.Text(n, 22, "DS.AccentHi", FontWeights.Bold).Also(t => { t.Width = 34; t.VerticalAlignment = VerticalAlignment.Top; DockPanel.SetDock(t, Dock.Left); }),
            new StackPanel { Children = { Kit.Text(title, 16, "DS.Text", FontWeights.SemiBold), Wrap(text, 14.5, "DS.TextSoft").Also(t => t.Margin = new(0, 4, 0, 0)) } } } },
    };
    private static Button Action(string icon, string text, string style) => new()
    {
        Style = DS.Style(style), Height = 44, MinHeight = 0, Padding = new(20, 0, 22, 0), FontSize = 15,
        Content = new StackPanel { Orientation = Orientation.Horizontal, Children = { Kit.Icon(icon, 14, "DS.Text").Also(i => i.Margin = new(0, 0, 10, 0)), Kit.Text(text, 15, "DS.Text", FontWeights.SemiBold) } },
    };
    private static void SetLabel(Button b, string text) { if (b.Content is StackPanel { Children.Count: 2 } s && s.Children[1] is TextBlock t) t.Text = text; }
}
