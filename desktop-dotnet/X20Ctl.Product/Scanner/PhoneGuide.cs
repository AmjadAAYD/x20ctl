using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
namespace X20Ctl.Product.Scanner;

public enum PhoneKind { Samsung, Android, IPhone }

/// <summary>What the drawn phone shows for a step.</summary>
public enum ScreenKind { List, QuickPanel, Recents, Notification, Buttons, Computer }

/// <summary>
/// A phone screen, drawn rather than photographed so it matches every phone closely enough and never goes out of date
/// with a screenshot: a title, the rows of a settings list, and the one row to touch, marked.
/// </summary>
public sealed record PhoneScreen(ScreenKind Kind, string Header, string[] Rows, int Highlight = -1, string? Badge = null, bool Switch = false, string? Banner = null);

/// <summary>One thing to do, a picture of where, and what to try when it doesn't go as described.</summary>
public sealed record GuideStep(string Title, string Do, PhoneScreen Screen, (string Problem, string Fix)[] Help);

/// <summary>The steps for each phone: getting it ready to record, and getting the recording to this PC.</summary>
public static class PhoneGuides
{
    public static string Name(PhoneKind p) => p switch { PhoneKind.Samsung => "Samsung", PhoneKind.Android => "Other Android", _ => "iPhone" };

    static readonly (string, string) NoBuildNumber = ("I can't find Build number", "Type \"Build number\" into the search box at the top of Settings and tap the result.");
    static readonly (string, string) NoDeveloperOptions = ("There's no Developer options", "It appears only after the Build number step. Go back one step and tap Build number seven times; the phone says when it's done.");

    public static GuideStep[] Prepare(PhoneKind phone) => phone switch
    {
        PhoneKind.Samsung =>
        [
            new("Unlock Developer options", "Open Settings, then About phone, then Software information. Tap Build number seven times, quickly. Enter your PIN if the phone asks.",
                new(ScreenKind.List, "Software information", ["One UI version", "Android version", "Baseband version", "Kernel version", "Build number", "SE for Android status"], 4, "Tap 7 times"),
                [NoBuildNumber, ("It asked for my PIN or pattern", "That's normal: it's the phone checking it's you. Enter it and carry on."),
                 ("It says Developer mode is already on", "Good, nothing to do. Go to the next step.")]),
            new("Turn on the Bluetooth log", "Go back to Settings. Developer options is now at the very bottom, under About phone. Open it and turn on \"Enable Bluetooth HCI snoop log\". If it offers a choice, pick Enabled, not Filtered.",
                new(ScreenKind.List, "Developer options", ["Take bug report", "Stay awake", "Enable Bluetooth HCI snoop log", "OEM unlocking", "Running services"], 2, "Turn on", Switch: true),
                [NoDeveloperOptions, ("I can't see the snoop log setting", "Scroll down: it's in the middle of a long list, in the Networking or Debugging part. Its name always has \"snoop\" in it."),
                 ("It only offers Filtered", "Pick Enabled if it's there at all. A filtered log keeps only part of each message, and the scan can't read it."),
                 ("It turns itself off again", "Some phones switch it off after a restart. Turn it on again just before the test.")]),
            new("Restart Bluetooth", "Swipe down from the top of the screen twice to open the quick panel. Tap Bluetooth to turn it off, wait three seconds, and tap it again to turn it on. The log only starts recording after this.",
                new(ScreenKind.QuickPanel, "Quick panel", ["Wi-Fi", "Bluetooth", "Sound", "Auto rotate", "Flashlight", "Airplane"], 1, "Off, then on"),
                [("The controller disconnected", "That's expected. You'll connect it again in KeyLinker during the test."),
                 ("I don't see a Bluetooth tile", "Swipe down once more, or swipe left on the tiles. You can also do it in Settings, Connections, Bluetooth.")]),
            new("Close KeyLinker", "Tap the Recent apps button (the three lines at the bottom left) and swipe KeyLinker up and away. The test then catches it connecting from the start.",
                new(ScreenKind.Recents, "Recent apps", ["KeyLinker"], 0, "Swipe up"),
                [("I use gestures, there are no buttons", "Swipe up from the bottom edge and hold for a moment: the recent apps appear."),
                 ("The app isn't in the list", "Then it's already closed. Go on.")]),
        ],
        PhoneKind.Android =>
        [
            new("Unlock Developer options", "Open Settings, then About phone. Tap Build number seven times, quickly. On some phones it's inside Software information first. Enter your PIN if the phone asks.",
                new(ScreenKind.List, "About phone", ["Device name", "Model", "Android version", "Build number", "Status"], 3, "Tap 7 times"),
                [NoBuildNumber, ("It asked for my PIN or pattern", "That's normal: it's the phone checking it's you. Enter it and carry on.")]),
            new("Turn on the Bluetooth log", "Open Settings, System, Developer options (or just Developer options). Turn on \"Enable Bluetooth HCI snoop log\". If it offers a choice, pick Enabled or Full, not Filtered.",
                new(ScreenKind.List, "Developer options", ["Memory", "Take bug report", "Enable Bluetooth HCI snoop log", "Stay awake", "USB debugging"], 2, "Turn on", Switch: true),
                [NoDeveloperOptions, ("I can't find the snoop log setting", "Search Settings for \"snoop\". The menu names differ between phone makers; the setting's name always has \"snoop\" in it."),
                 ("It only offers Filtered", "Pick Enabled or Full if they're there at all. A filtered log keeps only part of each message, and the scan can't read it.")]),
            new("Restart Bluetooth", "Swipe down from the top of the screen to open the quick settings. Turn Bluetooth off, wait three seconds, and turn it on again. The log only starts recording after this.",
                new(ScreenKind.QuickPanel, "Quick settings", ["Wi-Fi", "Bluetooth", "Mobile data", "Flashlight", "Auto-rotate", "Do not disturb"], 1, "Off, then on"),
                [("The controller disconnected", "That's expected. You'll connect it again in KeyLinker during the test.")]),
            new("Close KeyLinker", "Open your recent apps (swipe up from the bottom and hold, or the square button) and swipe KeyLinker away.",
                new(ScreenKind.Recents, "Recent apps", ["KeyLinker"], 0, "Swipe away"),
                [("The app isn't in the list", "Then it's already closed. Go on.")]),
        ],
        _ =>
        [
            new("Install Apple's Bluetooth profile", "On the iPhone, open developer.apple.com/bug-reporting/profiles-and-logs in Safari, find Bluetooth, and download its profile. It may ask you to sign in with an Apple Account. Then open Settings and tap Profile Downloaded, then Install.",
                new(ScreenKind.List, "Settings", ["Profile Downloaded", "Wi-Fi", "Bluetooth", "General", "Privacy & Security"], 0, "Tap, then Install"),
                [("I don't see Profile Downloaded", "Open Settings, General, VPN & Device Management: the profile waits there."),
                 ("It asks me to sign in", "Apple asks for an Apple Account to download it. A free account works."),
                 ("Safari says the file can't be opened", "Tap Allow when Safari asks to download a configuration profile, then go to Settings.")]),
            new("Follow Apple's instructions", "The profile page links Apple's own instructions for it. Do what they say before the test: if they say to restart the iPhone, restart it now.",
                new(ScreenKind.List, "Bluetooth logging", ["Instructions (PDF)", "Profile", "Restart if asked"], 0, "Read"),
                [("I can't open the instructions", "They need the same Apple Account sign-in. If you can't get them, restart the iPhone anyway: it does no harm.")]),
            new("Close KeyLinker", "Swipe up from the bottom and pause to see your apps, then swipe KeyLinker up and away.",
                new(ScreenKind.Recents, "App switcher", ["KeyLinker"], 0, "Swipe up"),
                [("The app isn't in the list", "Then it's already closed. Go on.")]),
        ],
    };

    public static GuideStep[] Bring(PhoneKind phone) => phone switch
    {
        PhoneKind.Samsung =>
        [
            new("Make a bug report", "Settings, Developer options, Take bug report (or Bug report). Choose Full report and press Report. It takes a few minutes; you can keep using the phone.",
                new(ScreenKind.List, "Developer options", ["Take bug report", "Stay awake", "Enable Bluetooth HCI snoop log"], 0, "Tap, then Full report"),
                [("There's no bug report option", "Look near the top of Developer options. If it's really not there, use the Other Android steps, or ask on Discord."),
                 ("It's taking a long time", "A full report can take five minutes or more. A notification says when it's ready; leave the phone alone until then.")]),
            new("Share it to this PC", "When the notification says the bug report is captured, tap it and choose Share. Send it to this PC with Quick Share, put it on Google Drive, or email it to yourself.",
                new(ScreenKind.Notification, "Notifications", ["Bluetooth", "Messages"], -1, "Tap to share", Banner: "Bug report captured"),
                [("The phone warns about personal information", "That's the phone being careful: a bug report holds a lot. X20CTL reads only the Bluetooth log in it, and nothing leaves this PC unless you press Send."),
                 ("I missed the notification", "Swipe down from the top: it stays there until you open it."),
                 ("It's too big to email", "Use Google Drive or Quick Share instead. Bug reports are often 20 MB or more.")]),
            new("Open it here", "On this PC, press Choose the recording and pick the ZIP, or drop it on this window. X20CTL takes only the Bluetooth log out of it.",
                new(ScreenKind.Computer, "This PC", ["bugreport-….zip"], 0, "Drop it here"),
                [("It says there's no Bluetooth log in it", "The snoop log was off, or Bluetooth wasn't restarted after turning it on. Go back to Prepare the phone, then do the test again."),
                 ("It found no KeyLinker messages", "The log may be Filtered, or taken before the test. Check the snoop log is Enabled (not Filtered) and make a new bug report after the test."),
                 ("I can't find the file", "Look in your Downloads folder, or wherever Quick Share or Drive saved it. Its name starts with bugreport.")]),
        ],
        PhoneKind.Android =>
        [
            new("Make a bug report", "Developer options, Take bug report, Full report. It takes a few minutes; a notification says when it's ready. With a cable and ADB, \"adb bugreport\" writes the same ZIP.",
                new(ScreenKind.List, "Developer options", ["Memory", "Take bug report", "Enable Bluetooth HCI snoop log"], 1, "Tap, then Full report"),
                [("It's taking a long time", "A full report can take five minutes or more. Leave the phone alone until the notification appears.")]),
            new("Share it to this PC", "Tap the notification and share the ZIP: email it to yourself, put it on Drive, or copy it over a USB cable.",
                new(ScreenKind.Notification, "Notifications", ["Messages", "System"], -1, "Tap to share", Banner: "Bug report captured"),
                [("The phone warns about personal information", "That's the phone being careful: a bug report holds a lot. X20CTL reads only the Bluetooth log in it."),
                 ("It's too big to email", "Use Drive or a USB cable instead.")]),
            new("Open it here", "Press Choose the recording and pick the ZIP, or drop it on this window. Older phones save btsnoop_hci.log on their storage instead; that file works too.",
                new(ScreenKind.Computer, "This PC", ["bugreport-….zip"], 0, "Drop it here"),
                [("It says there's no Bluetooth log in it", "The snoop log was off, or Bluetooth wasn't restarted after turning it on. Go back to Prepare the phone, then do the test again."),
                 ("It found no KeyLinker messages", "The log may be Filtered, or taken before the test. Make sure it's Enabled, then make a new bug report after the test.")]),
        ],
        _ =>
        [
            new("Take a sysdiagnose", "Press and hold both volume buttons and the side button for about a second, then let go. The iPhone vibrates. It can take ten minutes or more; Apple's instructions give the way for your model if this one doesn't work.",
                new(ScreenKind.Buttons, "Hold for one second", [], -1, "All three, 1 second"),
                [("Nothing happened", "Hold a little longer, about a second and a half, and let go before the power-off screen. If the power-off screen appears, swipe it away and try again."),
                 ("A screenshot was taken", "You pressed only two buttons. Press both volume buttons and the side button together.")]),
            new("Find it", "Settings, Privacy & Security, Analytics & Improvements, Analytics Data: the newest file starting with sysdiagnose. If it isn't there yet, wait; it appears when it's finished.",
                new(ScreenKind.List, "Analytics Data", ["sysdiagnose_2026….tar.gz", "Analytics-2026….ips", "JetsamEvent-….ips"], 0, "Tap, then Share"),
                [("It isn't in the list", "It's still being made. Wait a few more minutes and open the list again.")]),
            new("Send it to this PC", "Share it to a shared folder on this PC, or to iCloud Drive, OneDrive or Google Drive and download it here. A cloud drive keeps its own copy: delete it there afterwards.",
                new(ScreenKind.List, "Share", ["Save to Files", "iCloud Drive", "OneDrive", "Google Drive"], 0, "Pick one"),
                [("It's too big", "Sysdiagnoses are often a few hundred megabytes. A cloud drive or a shared folder handles that; email won't.")]),
            new("Open it here", "Press Choose the recording and pick the .tar.gz, or drop it on this window. X20CTL takes only the Bluetooth log out of it.",
                new(ScreenKind.Computer, "This PC", ["sysdiagnose_….tar.gz"], 0, "Drop it here"),
                [("It says there's no Bluetooth log in it", "The profile wasn't installed, or the test came before Apple's steps were finished. Go back to Prepare the phone, then do the test again.")]),
        ],
    };
}

/// <summary>
/// One step at a time: what to do in a sentence or two, a drawing of the phone with the place to touch marked, and an
/// "I'm stuck" panel with the usual problems and their fixes. The steps it walks through come from <see cref="PhoneGuides"/>.
/// </summary>
public sealed class PhoneGuide : Grid
{
    private readonly GuideStep[] steps;
    private readonly Action finished;
    private readonly string finishLabel;
    private readonly Action<int> moved;
    private readonly UIElement? lastExtra;
    private int index;

    public int Index => index;

    public PhoneGuide(string title, GuideStep[] steps, int start, Action<int> moved, Action finished, string finishLabel,
        UIElement? lastExtra = null, UIElement? above = null, Action? backOut = null, string backOutLabel = "Back")
    {
        this.steps = steps; this.finished = finished; this.finishLabel = finishLabel; this.moved = moved; this.lastExtra = lastExtra;
        this.title = title; this.above = above; this.backOut = backOut; this.backOutLabel = backOutLabel;
        index = Math.Clamp(start, 0, steps.Length - 1);
        Render();
    }

    private readonly string title;
    private readonly UIElement? above;
    private readonly Action? backOut;
    private readonly string backOutLabel;
    private bool stuck;

    public void Go(int n) { index = Math.Clamp(n, 0, steps.Length - 1); stuck = false; moved(index); Render(); }
    public void Stuck(bool open) { stuck = open; Render(); }

    private void Render()
    {
        Children.Clear(); ColumnDefinitions.Clear();
        ColumnDefinitions.Add(new() { Width = new(1.5, GridUnitType.Star) }); ColumnDefinitions.Add(new() { Width = new(20) }); ColumnDefinitions.Add(new());
        var s = steps[index];
        bool last = index == steps.Length - 1;

        var next = Button(last ? "" : "", last ? finishLabel : "Done, next step", "PrimaryButton");
        next.Click += (_, _) => { if (last) finished(); else Go(index + 1); };
        var back = Button("", index == 0 ? backOutLabel : "Back", "ActionButton"); back.IsEnabled = index > 0 || backOut != null;
        back.Click += (_, _) => { if (index > 0) Go(index - 1); else backOut?.Invoke(); };
        var help = Button("", stuck ? "Hide help" : "I'm stuck", "ActionButton"); help.Click += (_, _) => Stuck(!stuck);

        var body = new StackPanel();
        if (above != null) { Detach(above); body.Children.Add(above); }
        body.Children.Add(Dots());
        body.Children.Add(new TextBlock { Text = s.Title, FontFamily = DS.Display, FontSize = 34, FontWeight = FontWeights.Bold, Foreground = DS.Brush("DS.Text"), TextWrapping = TextWrapping.Wrap, Margin = new(0, 14, 0, 0) });
        body.Children.Add(Wrap(s.Do, 17.5, "DS.TextSoft").Also(t => t.Margin = new(0, 10, 0, 0)));
        if (last && lastExtra != null) { Detach(lastExtra); body.Children.Add(lastExtra); }
        body.Children.Add(new WrapPanel { Margin = new(0, 24, 0, 0), Children = { next, back.Also(b => b.Margin = new(12, 0, 0, 0)), help.Also(b => b.Margin = new(12, 0, 0, 0)) } });
        if (stuck) body.Children.Add(HelpPanel(s.Help));

        var left = Kit.Panel(title, $"Step {index + 1} of {steps.Length}", null, body);
        Children.Add(new ScrollViewer { Content = left, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        var right = Kit.Panel("Where to tap", null, null, new Viewbox { MaxHeight = 560, Stretch = Stretch.Uniform, Child = Phone.Draw(s.Screen) });
        SetColumn(right, 2); Children.Add(right);
    }

    /// <summary>Each step as a dot with its name; done ones ticked, the current one lit, and any of them clickable.</summary>
    private FrameworkElement Dots()
    {
        var row = new WrapPanel();
        for (int i = 0; i < steps.Length; i++)
        {
            int n = i; bool done = i < index, current = i == index;
            var c = current ? Kit.Blue : done ? Color.FromRgb(150, 175, 215) : Color.FromRgb(90, 104, 130);
            var chip = new Button
            {
                Cursor = System.Windows.Input.Cursors.Hand, Margin = new(0, 0, 8, 8), Padding = new(0), BorderThickness = new(0), Background = Brushes.Transparent, Focusable = true,
                Template = ChipTemplate(),
                Content = new Border { CornerRadius = new(13), Padding = new(10, 4, 12, 4), BorderThickness = new(1),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(current ? (byte)170 : (byte)60, c.R, c.G, c.B)), Background = new SolidColorBrush(Color.FromArgb(current ? (byte)60 : (byte)18, c.R, c.G, c.B)),
                    Child = Kit.Text((done ? "✓  " : $"{i + 1}  ") + steps[i].Title, 13, current ? "DS.Text" : "DS.TextSoft", current ? FontWeights.SemiBold : FontWeights.Normal) },
            };
            chip.Click += (_, _) => Go(n);
            row.Children.Add(chip);
        }
        return row;
    }

    private static ControlTemplate ChipTemplate()
    {
        var t = new ControlTemplate(typeof(Button));
        t.VisualTree = new FrameworkElementFactory(typeof(ContentPresenter));
        return t;
    }

    /// <summary>The usual problems for a step and what to do about each, then where to ask if none of them fits.</summary>
    public static Border HelpPanel((string Problem, string Fix)[] help)
    {
        var list = new StackPanel();
        foreach (var (problem, fix) in help)
            list.Children.Add(new StackPanel { Margin = new(0, 0, 0, 12), Children = { Kit.Text(problem, 15, "DS.Text", FontWeights.SemiBold), Wrap(fix, 14.5, "DS.TextSoft").Also(t => t.Margin = new(0, 3, 0, 0)) } });
        var copy = Button("", "Copy @" + ScanFlow.Discord, "ActionButton");
        copy.Click += (_, _) => { try { Clipboard.SetText(ScanFlow.Discord); SetLabel(copy, "Copied"); } catch (System.Runtime.InteropServices.ExternalException) { } };
        list.Children.Add(new DockPanel { Margin = new(0, 4, 0, 0), Children = {
            copy.Also(b => { DockPanel.SetDock(b, Dock.Right); b.Margin = new(12, 0, 0, 0); }),
            Wrap("Still stuck? Ask @" + ScanFlow.Discord + " on Discord: say which step you're on and what the phone shows.", 14, "DS.TextMuted").Also(t => t.VerticalAlignment = VerticalAlignment.Center) } });
        return new Border
        {
            CornerRadius = new(14), Margin = new(0, 18, 0, 0), Padding = new(18, 16, 18, 14), BorderThickness = new(1),
            BorderBrush = new SolidColorBrush(Color.FromArgb(110, 61, 139, 255)), Background = new SolidColorBrush(Color.FromArgb(40, 61, 139, 255)),
            Child = new StackPanel { Children = { Kit.Text("IF IT DIDN'T GO LIKE THAT", 12, "DS.TextMuted", FontWeights.SemiBold).Also(t => t.Margin = new(0, 0, 0, 10)), list } },
        };
    }

    private static void Detach(UIElement e)
    {
        switch ((e as FrameworkElement)?.Parent) { case Panel p: p.Children.Remove(e); break; case Decorator d: d.Child = null; break; case ContentControl c: c.Content = null; break; }
    }
    private static TextBlock Wrap(string text, double size, string brush) => new() { Text = text, FontFamily = DS.Body, FontSize = size, Foreground = DS.Brush(brush), TextWrapping = TextWrapping.Wrap, LineHeight = size * 1.45 };
    private static Button Button(string icon, string text, string style) => new()
    {
        Style = DS.Style(style), Height = 44, MinHeight = 0, Padding = new(20, 0, 22, 0), FontSize = 15,
        Content = new StackPanel { Orientation = Orientation.Horizontal, Children = { Kit.Icon(icon, 14, "DS.Text").Also(i => i.Margin = new(0, 0, 10, 0)), Kit.Text(text, 15, "DS.Text", FontWeights.SemiBold) } },
    };
    private static void SetLabel(Button b, string text) { if (b.Content is StackPanel { Children.Count: 2 } s && s.Children[1] is TextBlock t) t.Text = text; }
}

/// <summary>Draws the phone (or this PC) for a <see cref="PhoneScreen"/>.</summary>
public static class Phone
{
    static readonly Color Accent = Kit.Blue;
    const double W = 300, H = 600;

    public static FrameworkElement Draw(PhoneScreen s)
    {
        if (s.Kind == ScreenKind.Computer) return Computer(s);
        var canvas = new Canvas { Width = W + 40, Height = H };
        // the body, and for the button step its physical buttons
        var body = new Border { Width = W, Height = H, CornerRadius = new(40), BorderThickness = new(3), BorderBrush = new SolidColorBrush(Color.FromRgb(70, 82, 108)),
            Background = new SolidColorBrush(Color.FromRgb(10, 14, 26)) };
        Canvas.SetLeft(body, 20); canvas.Children.Add(body);
        if (s.Kind == ScreenKind.Buttons)
        {
            canvas.Children.Add(SideButton(12, 150, 70, true)); canvas.Children.Add(SideButton(12, 236, 70, true));
            canvas.Children.Add(SideButton(W + 20, 190, 100, true));
        }
        var screen = new Grid { Margin = new(14) };
        body.Child = new Border { CornerRadius = new(30), ClipToBounds = true, Background = new LinearGradientBrush(Color.FromRgb(16, 22, 40), Color.FromRgb(9, 12, 24), 90), Child = screen };
        screen.RowDefinitions.Add(new() { Height = new(34) }); screen.RowDefinitions.Add(new() { Height = GridLength.Auto }); screen.RowDefinitions.Add(new());
        // status bar
        var status = new DockPanel { Margin = new(18, 8, 18, 0) };
        status.Children.Add(Text("12:30", 12, 200).Also(t => DockPanel.SetDock(t, Dock.Left)));
        status.Children.Add(Text("▂▄▆ ▮", 11, 160).Also(t => t.HorizontalAlignment = HorizontalAlignment.Right));
        screen.Children.Add(status);
        var header = Text(s.Header, s.Kind == ScreenKind.List ? 26 : 20, 240, FontWeights.SemiBold); header.Margin = new(20, s.Kind == ScreenKind.List ? 24 : 10, 20, 14); header.TextWrapping = TextWrapping.Wrap;
        Grid.SetRow(header, 1); screen.Children.Add(header);
        FrameworkElement content = s.Kind switch
        {
            ScreenKind.QuickPanel => Tiles(s),
            ScreenKind.Recents => Recents(s),
            ScreenKind.Notification => Notification(s),
            ScreenKind.Buttons => Hold(s),
            _ => List(s),
        };
        Grid.SetRow(content, 2); screen.Children.Add(content);
        return canvas;
    }

    static FrameworkElement List(PhoneScreen s)
    {
        var panel = new StackPanel { Margin = new(12, 0, 12, 0) };
        for (int i = 0; i < s.Rows.Length; i++)
        {
            bool on = i == s.Highlight;
            var row = new DockPanel { LastChildFill = true };
            if (on && s.Switch) row.Children.Add(Switch().Also(x => DockPanel.SetDock(x, Dock.Right)));
            row.Children.Add(Text(s.Rows[i], 14.5, (byte)(on ? 255 : 170), on ? FontWeights.SemiBold : FontWeights.Normal).Also(t => { t.TextWrapping = TextWrapping.Wrap; t.VerticalAlignment = VerticalAlignment.Center; }));
            var cell = new Border { Padding = new(14, 13, 14, 13), Margin = new(0, 0, 0, 4), CornerRadius = new(14), Child = row,
                Background = new SolidColorBrush(on ? Color.FromArgb(70, Accent.R, Accent.G, Accent.B) : Color.FromArgb(14, 255, 255, 255)) };
            panel.Children.Add(on ? Marked(cell, s.Badge) : cell);
        }
        return panel;
    }

    static FrameworkElement Tiles(PhoneScreen s)
    {
        var grid = new UniformGrid { Columns = 3, Margin = new(14, 0, 14, 0), VerticalAlignment = VerticalAlignment.Top };
        for (int i = 0; i < s.Rows.Length; i++)
        {
            bool on = i == s.Highlight;
            var tile = new Border { Height = 86, Margin = new(5), CornerRadius = new(22),
                Background = new SolidColorBrush(on ? Color.FromArgb(200, Accent.R, Accent.G, Accent.B) : Color.FromArgb(26, 255, 255, 255)),
                Child = Text(s.Rows[i], 12.5, (byte)(on ? 255 : 180), on ? FontWeights.SemiBold : FontWeights.Normal).Also(t => { t.HorizontalAlignment = HorizontalAlignment.Center; t.VerticalAlignment = VerticalAlignment.Center; t.TextAlignment = TextAlignment.Center; t.TextWrapping = TextWrapping.Wrap; }) };
            grid.Children.Add(on ? Marked(tile, s.Badge, centre: true) : tile);
        }
        return grid;
    }

    static FrameworkElement Recents(PhoneScreen s)
    {
        var card = new Border { Width = 180, Height = 290, CornerRadius = new(22), Background = new SolidColorBrush(Color.FromArgb(60, Accent.R, Accent.G, Accent.B)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(160, Accent.R, Accent.G, Accent.B)), BorderThickness = new(2),
            Child = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = {
                Text("⬆", 40, 255).Also(t => t.HorizontalAlignment = HorizontalAlignment.Center),
                Text(s.Rows.FirstOrDefault() ?? "", 15, 255, FontWeights.SemiBold).Also(t => { t.HorizontalAlignment = HorizontalAlignment.Center; t.Margin = new(0, 10, 0, 0); }) } } };
        return new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Children = { Marked(card, s.Badge) } };
    }

    static FrameworkElement Notification(PhoneScreen s)
    {
        var panel = new StackPanel { Margin = new(12, 0, 12, 0) };
        var banner = new Border { Padding = new(14, 12, 14, 12), CornerRadius = new(18), Background = new SolidColorBrush(Color.FromArgb(80, Accent.R, Accent.G, Accent.B)),
            Child = new StackPanel { Children = { Text(s.Banner ?? "", 15, 255, FontWeights.SemiBold), Text("Tap to share your bug report", 12.5, 200).Also(t => t.Margin = new(0, 3, 0, 0)) } } };
        panel.Children.Add(Marked(banner, s.Badge));
        foreach (var r in s.Rows)
            panel.Children.Add(new Border { Padding = new(14, 12, 14, 12), Margin = new(0, 6, 0, 0), CornerRadius = new(18), Background = new SolidColorBrush(Color.FromArgb(14, 255, 255, 255)), Child = Text(r, 14, 130) });
        return panel;
    }

    static FrameworkElement Hold(PhoneScreen s) => new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new(20, 0, 20, 60), Children = {
        Badge(s.Badge ?? "").Also(b => b.HorizontalAlignment = HorizontalAlignment.Center),
        Text("Volume up + volume down + side button, together, then let go.", 14, 200).Also(t => { t.TextWrapping = TextWrapping.Wrap; t.TextAlignment = TextAlignment.Center; t.Margin = new(0, 16, 0, 0); }) } };

    static FrameworkElement Computer(PhoneScreen s)
    {
        var drop = new Border { Height = 200, Margin = new(26), CornerRadius = new(16), BorderThickness = new(2), BorderBrush = new SolidColorBrush(Color.FromArgb(200, Accent.R, Accent.G, Accent.B)),
            Background = new SolidColorBrush(Color.FromArgb(36, Accent.R, Accent.G, Accent.B)),
            Child = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = {
                Text("⬇", 40, 255).Also(t => t.HorizontalAlignment = HorizontalAlignment.Center),
                Text(s.Rows.FirstOrDefault() ?? "", 15, 255, FontWeights.SemiBold).Also(t => { t.HorizontalAlignment = HorizontalAlignment.Center; t.Margin = new(0, 8, 0, 0); }) } } };
        var monitor = new Border { Width = 440, Height = 290, CornerRadius = new(16), BorderThickness = new(3), BorderBrush = new SolidColorBrush(Color.FromRgb(70, 82, 108)),
            Background = new LinearGradientBrush(Color.FromRgb(16, 22, 40), Color.FromRgb(9, 12, 24), 90), Child = Marked(drop, s.Badge) };
        return new StackPanel { Children = { monitor,
            new Border { Width = 26, Height = 40, Background = new SolidColorBrush(Color.FromRgb(50, 60, 82)) },
            new Border { Width = 170, Height = 10, CornerRadius = new(5), Background = new SolidColorBrush(Color.FromRgb(50, 60, 82)) } } };
    }

    /// <summary>The element to touch: a ring around it that pulses a few times (then rests, so an open page costs nothing), and a badge saying what to do.</summary>
    static FrameworkElement Marked(FrameworkElement target, string? badge, bool centre = false)
    {
        var ring = new Border { CornerRadius = new(18), BorderThickness = new(2.5), Margin = new(-4), IsHitTestVisible = false,
            BorderBrush = new SolidColorBrush(Color.FromRgb(125, 182, 255)) };
        if (DS.Motion)
            ring.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, .25, TimeSpan.FromMilliseconds(650)) { AutoReverse = true, RepeatBehavior = new RepeatBehavior(4), FillBehavior = FillBehavior.Stop });
        var g = new Grid { Margin = new(0, 0, 0, 4) };
        g.Children.Add(target); g.Children.Add(ring);
        if (badge != null) g.Children.Add(Badge(badge).Also(b => { b.HorizontalAlignment = centre ? HorizontalAlignment.Center : HorizontalAlignment.Right; b.VerticalAlignment = VerticalAlignment.Top; b.Margin = centre ? new(-30, -14, -30, 0) : new(0, -14, -8, 0); }));
        return g;
    }

    static Border Badge(string text) => new()
    {
        CornerRadius = new(12), Padding = new(10, 4, 10, 5), Background = new SolidColorBrush(Color.FromRgb(125, 182, 255)), IsHitTestVisible = false,
        Child = new TextBlock { Text = text, FontFamily = DS.Body, FontSize = 12.5, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(6, 12, 28)) },
    };

    static FrameworkElement Switch() => new Border { Width = 46, Height = 26, CornerRadius = new(13), Margin = new(10, 0, 0, 0), Background = new SolidColorBrush(Accent), VerticalAlignment = VerticalAlignment.Center,
        Child = new Ellipse { Width = 20, Height = 20, Fill = Brushes.White, HorizontalAlignment = HorizontalAlignment.Right, Margin = new(0, 0, 3, 0) } };

    static FrameworkElement SideButton(double x, double y, double height, bool lit)
    {
        var b = new Border { Width = 9, Height = height, CornerRadius = new(4), Background = new SolidColorBrush(lit ? Color.FromRgb(125, 182, 255) : Color.FromRgb(70, 82, 108)) };
        if (lit && DS.Motion) b.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, .3, TimeSpan.FromMilliseconds(650)) { AutoReverse = true, RepeatBehavior = new RepeatBehavior(4), FillBehavior = FillBehavior.Stop });
        Canvas.SetLeft(b, x); Canvas.SetTop(b, y);
        return b;
    }

    static TextBlock Text(string s, double size, byte alpha, FontWeight? weight = null) => new()
    {
        Text = s, FontFamily = DS.Body, FontSize = size, FontWeight = weight ?? FontWeights.Normal, Foreground = new SolidColorBrush(Color.FromArgb(alpha, 235, 241, 255)),
    };
}
