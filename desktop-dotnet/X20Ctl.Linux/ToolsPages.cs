using Path = Avalonia.Controls.Shapes.Path;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using System.Net.Http;
namespace X20Ctl.Linux;

/// <summary>A round on/off switch, the same one the PC mode and Mouse pages use.</summary>
internal static class Switches
{
    public static Control Make(bool on, Action<bool> set, bool enabled = true)
    {
        var knob = new Border { Width = 22, Height = 22, CornerRadius = new CornerRadius(11), Background = Brushes.White, HorizontalAlignment = on ? HorizontalAlignment.Right : HorizontalAlignment.Left, Margin = new Thickness(3), VerticalAlignment = VerticalAlignment.Center };
        var track = new Border { Width = 56, Height = 30, CornerRadius = new CornerRadius(15), Background = on ? T.PrimaryFill : T.B("#33FFFFFF"), BorderBrush = T.B("#55FFFFFF"), BorderThickness = new Thickness(1), Child = knob, Opacity = enabled ? 1 : .45, Cursor = new Cursor(StandardCursorType.Hand) };
        if (enabled) track.OnTap(() => set(!on));
        return track;
    }
}

internal sealed record ToolsActions(Action Check, Action WriteScan, Action PcMode, Action Replay, Action About, Action Feedback);

/// <summary>Help, extras and everything about X20CTL: the Controller Check hero, five tiles, feedback, and the switches for updates and the opening intro.</summary>
internal sealed class ToolsPage : UserControl
{
    readonly ToolsActions go; readonly AppSettings settings = AppSettings.Shared; string updateLine; bool checking;
    const string Heart = "M10,17 C-2,8 4,0 10,6 C16,0 22,8 10,17 Z", Star = "M10,1 L12.6,7 L19,7.6 L14.2,11.8 L15.6,18 L10,14.8 L4.4,18 L5.8,11.8 L1,7.6 L7.4,7 Z",
        Play = "M5,2 L17,10 L5,18 Z", Chat = "M2,3 H18 V13 H8 L4,17 V13 H2 Z", Info = "M10,9 V15 M10,5 V6", Pad = "M2,9 C2,5 4,3 7,3 H13 C16,3 18,5 18,9 C18,13 16,16 14,16 C12,16 12,13 10,13 C8,13 8,16 6,16 C4,16 2,13 2,9 Z M6,7 V11 M4,9 H8", Blue = "M5,6 L15,14 L10,18 V2 L15,6 L5,14", Tool = "M3,17 L10,10 M12,2 A5,5 0 1 0 18,8 L15,9 L11,5 Z";

    public ToolsPage(ToolsActions go)
    {
        this.go = go; updateLine = UpdateLine(settings.AutoUpdate);
        Build();
    }

    void Build()
    {
        bool phone = Phone.Active;
        var page = new Grid { Margin = phone ? new Thickness(28, 6, 28, 16) : new Thickness(60, 14, 60, 18), RowDefinitions = new("*,Auto"), MinHeight = phone ? 620 : 0 };
        var hub = new Grid { ColumnDefinitions = new(phone ? ".8*,16,1.5*" : "1.05*,24,1*") };
        hub.Children.Add(Hero());
        var gap = phone ? 14 : 24;
        var tiles = new Grid { ColumnDefinitions = new($"*,{gap},*"), RowDefinitions = new($"*,{gap},*,{gap},*,{gap},*") }; Grid.SetColumn(tiles, 2);
        // a phone has no PC mode: Write scan takes the whole first row
        if (!phone) Place(tiles, Tile(Pad, "PC mode", "Remaps, turbo and macros for any pad.", "#34D399", go.PcMode), 0, 0);
        var scan = Tile(Blue, "Write scan", "What an app writes. 20 to 40 min.", "#6EA0EB", go.WriteScan); Place(tiles, scan, 0, phone ? 0 : 2); if (phone) Grid.SetColumnSpan(scan, 3);
        Place(tiles, Tile(Play, "Replay Intro", "Watch the X20CTL opening again.", "#3D8BFF", go.Replay), 2, 0);
        Place(tiles, Tile(Info, "About", "What X20CTL is, who made it, and credits.", "#96AFD7", go.About, true), 2, 2);
        Place(tiles, Tile(Heart, "Support X20CTL", "Help keep it going on Ko-fi.", "#FF5C7A", () => ExternalLinks.Open(ExternalLinks.Support)), 4, 0);
        Place(tiles, Tile(Star, "Star on GitHub", "AmjadAAYD/x20ctl", "#F5C850", () => ExternalLinks.Open(ExternalLinks.Repository)), 4, 2);
        var fb = Tile(Chat, "Send feedback", "Report a bug, ask for a feature or tell us about your controller.", "#6EA0EB", go.Feedback); Place(tiles, fb, 6, 0); Grid.SetColumnSpan(fb, 3);
        hub.Children.Add(tiles); page.Children.Add(hub);

        // side by side on the desktop; stacked on a phone, where half the width is too narrow for either
        var bars = phone ? new Grid { RowDefinitions = new("Auto,12,Auto"), Margin = new Thickness(0, 14, 0, 0) } : new Grid { ColumnDefinitions = new("1.35*,16,1*"), Margin = new Thickness(0, 18, 0, 0) }; Grid.SetRow(bars, 1);
        var update = T.Stack(2, T.Label("Keep X20CTL up to date", 16, null, FontWeight.SemiBold), T.Label(updateLine, 13, T.Soft)); ((TextBlock)update.Children[1]).TextWrapping = TextWrapping.Wrap;
        var left = new Grid { ColumnDefinitions = new("Auto,16,*,Auto") };
        left.Children.Add(Switches.Make(settings.AutoUpdate, v => { settings.AutoUpdate = v; settings.Save(); updateLine = UpdateLine(v); Build(); }));
        Grid.SetColumn(update, 2); update.VerticalAlignment = VerticalAlignment.Center; left.Children.Add(update);
        var check = new Pill(checking ? "Checking…" : "Check now", Pill.Kinds.Ghost, CheckNow) { Padding = new Thickness(22, 11), Enabled = !checking, VerticalAlignment = VerticalAlignment.Center }; Grid.SetColumn(check, 3); left.Children.Add(check);
        // the phone app doesn't update itself from GitHub releases, so it has only the intro bar
        if (!phone) bars.Children.Add(T.Card(left, 18, 16));

        var intro = new Grid { ColumnDefinitions = new("*,Auto") };
        intro.Children.Add(T.Stack(2, T.Label("Opening intro", 16, null, FontWeight.SemiBold), T.Label(Phone.Active ? "A tap skips it." : "Esc skips it, M mutes it.", 13, T.Soft)));
        var sw = T.Line(16, Switches.Make(settings.IntroOnLaunch, v => { settings.IntroOnLaunch = v; settings.Save(); Build(); }), T.Label("At launch", 15), Switches.Make(settings.IntroSound, v => { settings.IntroSound = v; settings.Save(); Build(); }), T.Label("Sound", 15));
        foreach (var c in sw.Children.OfType<TextBlock>()) c.VerticalAlignment = VerticalAlignment.Center;
        sw.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(sw, 1); intro.Children.Add(sw);
        var ib = T.Card(intro, 18, 16); if (!phone) Grid.SetColumn(ib, 2); bars.Children.Add(ib);
        page.Children.Add(bars);
        Content = page;
    }

    static string UpdateLine(bool on) => on
        ? (AppImageUpdater.CanInstall ? "On. Checks GitHub each launch and installs a newer version by itself. You have " : "On. Checks GitHub for a newer version each launch. You have ") + AppInfo.Shown + "."
        : "Off. Turn it on to check GitHub for a newer version each launch. You have " + AppInfo.Shown + ".";

    async void CheckNow()
    {
        checking = true; updateLine = "Checking GitHub…"; Build();
        try
        {
            var r = await UpdateCheck.Latest();
            updateLine = r == null ? "You're up to date (" + AppInfo.Shown + ")." : $"{X20Ctl.Zone.ReleaseVersions.Display(r.Version)} is out. Open the releases page to download it.";
            if (r != null && AppImageUpdater.CanInstall) { updateLine = "Updating…"; Build(); updateLine = (await AppImageUpdater.Install(r)).Message; }
            else if (r != null) ExternalLinks.Open(r.Page);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException) { updateLine = "Couldn't check: " + e.Message; }
        checking = false; Build();
    }

    static void Place(Grid g, Control c, int row, int col) { Grid.SetRow(c, row); Grid.SetColumn(c, col); g.Children.Add(c); }

    static Control Icon(string data, string colour, double size)
    {
        var glyph = new Path { Data = Geometry.Parse(data), Stroke = Brushes.White, Fill = data.Contains('Z') && data != Pad && data != Tool ? Brushes.White : null, StrokeThickness = 1.7, StrokeLineCap = PenLineCap.Round, StrokeJoin = PenLineJoin.Round, Width = 20, Height = 20, Stretch = Stretch.Uniform };
        return new Border { Width = size, Height = size, CornerRadius = new CornerRadius(size / 2), Background = T.Vertical(colour + "CC", colour + "66"), BorderBrush = T.B(colour + "99"), BorderThickness = new Thickness(1), Child = new Viewbox { Width = size * .42, Height = size * .42, Child = glyph, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
    }

    static Control Arrow() => new Border
    {
        Width = 38, Height = 38, CornerRadius = new CornerRadius(19), BorderBrush = T.B("#55FFFFFF"), BorderThickness = new Thickness(1), Background = T.B("#22FFFFFF"),
        Child = Parts.Center(T.Label("→", 17, T.Text)), VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Right
    };

    static Border Tile(string icon, string title, string text, string colour, Action click, bool dim = false)
    {
        bool phone = Phone.Active;
        var body = T.Stack(phone ? 3 : 5, T.Label(title, phone ? 17 : 20, null, FontWeight.SemiBold), T.Label(text, phone ? 12.5 : 13.5, T.Soft)); ((TextBlock)body.Children[1]).TextWrapping = TextWrapping.Wrap; body.VerticalAlignment = VerticalAlignment.Center;
        var row = new Grid { ColumnDefinitions = new(phone ? "Auto,12,*" : "Auto,16,*,Auto") }; row.Children.Add(Icon(icon, colour, phone ? 42 : 56) is var i ? i : null!);
        Grid.SetColumn(body, 2); row.Children.Add(body); if (!phone) { var arrow = Arrow(); Grid.SetColumn(arrow, 3); row.Children.Add(arrow); }
        var card = T.Card(row, phone ? 14 : 20, phone ? 14 : 20, raised: true); card.Cursor = new Cursor(StandardCursorType.Hand);
        card.PointerEntered += (_, _) => card.BorderBrush = T.B("#66AFC8FF"); card.PointerExited += (_, _) => card.BorderBrush = T.Hairline;
        card.OnTap(() => click()); return card;
    }

    Control Hero()
    {
        var steps = T.Flow(8, new[] { "1  Read scan", "2  Write scan", "3  Send online" }.Select(s => (Control)new Border { Background = T.B("#33FFFFFF"), CornerRadius = new CornerRadius(14), Padding = new Thickness(13, 5), Child = T.Label(s, 12.5, T.Text) }).ToArray());
        // on a phone the monitor art shrinks into the narrow hero instead of spilling out of it
        Control screen = Phone.Active ? new Viewbox { Child = Monitor(), Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, Margin = new Thickness(0, 56, 0, 0) } : Monitor();
        var g = new Grid { RowDefinitions = new("*,Auto,Auto,Auto") };
        var top = new Grid(); top.Children.Add(Icon(Tool, "#3D8BFF", 64)); ((Control)top.Children[0]).HorizontalAlignment = HorizontalAlignment.Left; ((Control)top.Children[0]).VerticalAlignment = VerticalAlignment.Top;
        screen.HorizontalAlignment = HorizontalAlignment.Center; top.Children.Add(screen); g.Children.Add(top);
        var title = T.Label("Controller not working?", Phone.Active ? 28 : 38, null, FontWeight.Bold); title.Margin = new Thickness(0, 14, 0, 4); Grid.SetRow(title, 1); g.Children.Add(title);
        var sub = T.Label("Read scan, then Write scan, then send both reports online together.", Phone.Active ? 14 : 16.5, T.Soft); Grid.SetRow(sub, 2); g.Children.Add(sub);
        var foot = new Grid { Margin = new Thickness(0, 14, 0, 0), ColumnDefinitions = new("*,Auto") }; foot.Children.Add(steps); var arrow = Arrow(); Grid.SetColumn(arrow, 1); foot.Children.Add(arrow); Grid.SetRow(foot, 3); g.Children.Add(foot);
        var card = T.Card(g, 28, 22, raised: true); card.BorderBrush = T.B("#7FA5E0FF"); card.BorderThickness = new Thickness(1.6); card.Cursor = new Cursor(StandardCursorType.Hand); card.OnTap(() => go.Check()); return card;
    }

    /// <summary>A small drawing of a monitor running the scan: bars, a stick circle and a progress line.</summary>
    static Control Monitor()
    {
        var rows = new StackPanel { Spacing = 8, Margin = new Thickness(14, 10) };
        foreach (var (name, w) in new[] { ("A", .8), ("B", .45), ("X", .35), ("Y", .25), ("LB", .5), ("RB", .65) })
            rows.Children.Add(T.Line(10, new Border { Width = 22, Child = T.Label(name, 10, T.Soft) }, new Border { Width = 190 * w, Height = 7, CornerRadius = new CornerRadius(4), Background = T.B("#4D8BFF") }));
        var ring = new Border { Width = 96, Height = 96, CornerRadius = new CornerRadius(48), BorderBrush = T.B("#668BB8FF"), BorderThickness = new Thickness(1.4), Child = new Border { Width = 11, Height = 11, CornerRadius = new CornerRadius(6), Background = T.B("#7DB6FF"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(18, 0, 0, 14) } };
        var inner = new Grid { ColumnDefinitions = new("*,Auto"), Width = 400, Height = 190 };
        inner.Children.Add(rows); Grid.SetColumn(ring, 1); ring.Margin = new Thickness(0, 14, 24, 0); ring.VerticalAlignment = VerticalAlignment.Top; inner.Children.Add(ring);
        var bar = new Border { Height = 5, Width = 372, CornerRadius = new CornerRadius(3), Background = T.B("#22FFFFFF"), VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 12), Child = new Border { Width = 120, HorizontalAlignment = HorizontalAlignment.Left, CornerRadius = new CornerRadius(3), Background = T.B("#34D399") } };
        inner.Children.Add(bar);
        return new Border { Background = T.B("#CC060B18"), BorderBrush = T.B("#77557FCF"), BorderThickness = new Thickness(2), CornerRadius = new CornerRadius(16), Child = inner, BoxShadow = new BoxShadows(new BoxShadow { Blur = 40, Color = Color.Parse("#553D8BFF") }) };
    }
}

/// <summary>What X20CTL is, who made it, and credits.</summary>
internal sealed class AboutPage : UserControl
{
    public AboutPage(Action back, Bitmap? mark)
    {
        var logo = new Border { Width = 76, Height = 76, Child = mark == null ? null : new Avalonia.Controls.Image { Source = mark } };
        var head = T.Line(22, logo, T.Stack(2, T.Label("X20CTL", 40, null, FontWeight.Bold), T.Label("Controller Studio for Linux · " + AppInfo.Shown, 15.5, T.Soft)));
        var about = T.Label("X20CTL is an independent, open-source controller studio for EasySMX pads. Remap buttons, shape stick and trigger response, build back-paddle macros, set vibration, and check that a controller works, all on your own PC. There is no account, and nothing leaves your PC unless you choose to send a report or feedback.", 17, T.Text);
        var local = T.Label("Every change stays a local draft until it can be sent to a connected, verified controller. X20CTL never writes to a controller it cannot read back.", 15, T.Soft);
        var rule = new Border { Height = 1, Background = T.Hairline, Margin = new Thickness(0, 10) };
        var stack = T.Stack(14,
            T.Label("Credits · Desktop-control layout inspired by Gopher360 — Tylemagne, GPL-3.0. See the distributed open-source license notices.", 12.5, T.Muted),
            head, about, local, rule,
            Parts.Caps("MADE BY"), T.Label("Amjad AAYD, who designs and builds X20CTL.", 17),
            Parts.Caps("THANKS"), T.Label("Special thanks to Kiin from Team EasySMX for sending me a free X20 Pro controller.", 17),
            Parts.Caps("INSPIRATION"), T.Label("The console-style look of X20CTL was inspired by ApexSenseBridge by ReynArts. Thank you for showing how good a controller app can feel.", 17),
            T.Wrap(12, new Pill("←  Back to Tools", Pill.Kinds.Ghost, back) { Padding = new Thickness(24, 12) }, new Pill("☆  X20CTL on GitHub", Pill.Kinds.Ghost, () => ExternalLinks.Open(ExternalLinks.Repository)) { Padding = new Thickness(24, 12) },
                new Pill("ApexSenseBridge by ReynArts", Pill.Kinds.Ghost, () => ExternalLinks.Open(ExternalLinks.ApexSenseBridge)) { Padding = new Thickness(24, 12) }),
            T.Label("X20CTL is not affiliated with, endorsed by or sponsored by EasySMX. EasySMX and its product names belong to their owners.", 13.5, T.Soft));
        foreach (var t in stack.Children.OfType<TextBlock>()) t.TextWrapping = TextWrapping.Wrap;
        var card = T.Card(stack, 50, 24); card.MaxWidth = 1150; card.HorizontalAlignment = HorizontalAlignment.Left; card.VerticalAlignment = VerticalAlignment.Top; card.Margin = new Thickness(60, 14, 0, 0);
        Content = new ScrollViewer { Content = card };
    }
}

/// <summary>Send feedback: pick what it is about, write it, send it to X20CTL's inbox.</summary>
internal sealed class FeedbackPage : UserControl
{
    string category = "bug", message = "", status = ""; string? controller, connection; bool sending; readonly Action back; readonly IReadOnlyList<string> controllers;
    public FeedbackPage(Action back, IEnumerable<string> controllers) { this.back = back; this.controllers = controllers.ToList(); Build(); }

    void Build()
    {
        var kinds = Segmented.Make(FeedbackClient.Categories.Select(c => c.Label).ToList(), FeedbackClient.Categories.First(c => c.Id == category).Label, v => { category = FeedbackClient.Categories.First(c => c.Label == v).Id; Build(); }, 14.5);
        var pick = new ComboBox { MinWidth = 300, FontSize = 15, ItemsSource = new[] { "Not chosen" }.Concat(controllers).ToList(), SelectedItem = controller ?? "Not chosen" };
        pick.SelectionChanged += (_, _) => controller = pick.SelectedItem as string is { } s && s != "Not chosen" ? s : null;
        var link = new ComboBox { MinWidth = 220, FontSize = 15, ItemsSource = new[] { "Not chosen", "USB cable", "2.4G receiver", "Bluetooth" }, SelectedItem = connection ?? "Not chosen" };
        link.SelectionChanged += (_, _) => connection = link.SelectedItem as string is { } s && s != "Not chosen" ? s : null;
        var box = new TextBox { Text = message, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 170, MaxHeight = 260, MaxLength = 4000, FontSize = 15.5, Padding = new Thickness(18, 14), Watermark = "The right trigger on my X20 Pro reads about 5% before I touch it.", Background = T.B("#78040814"), Foreground = T.Text, BorderBrush = T.B("#468CB4FF") };
        var count = T.Label(message.Length + " / 4000", 12.5, T.Muted); count.HorizontalAlignment = HorizontalAlignment.Right;
        box.TextChanged += (_, _) => { message = box.Text ?? ""; count.Text = message.Length + " / 4000"; };
        var send = new Pill(sending ? "Sending…" : "➤  Send to X20CTL", Pill.Kinds.Primary, Send) { Padding = new Thickness(26, 12), Enabled = !sending };
        var stack = T.Stack(10, T.Label("Send feedback", 38, null, FontWeight.Bold), T.Label("Tell X20CTL what's broken, what you'd like, or how your controller behaves. It goes straight to the X20CTL inbox.", 16.5, T.Soft),
            new Border { Height = 6 }, Parts.Caps("WHAT IS IT ABOUT?"), kinds,
            new Border { Height = 4 }, T.Line(18, T.Stack(4, Parts.Caps("CONTROLLER (OPTIONAL)"), pick), T.Stack(4, Parts.Caps("CONNECTED BY (OPTIONAL)"), link)),
            new Border { Height = 4 }, Parts.Caps("YOUR MESSAGE"), box, count,
            T.Line(14, new Pill("←  Back to Tools", Pill.Kinds.Ghost, back) { Padding = new Thickness(24, 12) }, send));
        if (status.Length > 0) stack.Children.Add(T.Label(status, 14.5, status.StartsWith("Sent") ? T.B("#34D399") : T.Warn));
        stack.Children.Add(T.Label("What's sent: your message, what it's about, the controller and connection if you pick them, the X20CTL version and the system version. No account, files or personal details.", 13, T.Soft));
        foreach (var t in stack.Children.OfType<TextBlock>()) t.TextWrapping = TextWrapping.Wrap;
        var card = T.Card(stack, 48, 24); card.MaxWidth = 1100; card.HorizontalAlignment = HorizontalAlignment.Left; card.VerticalAlignment = VerticalAlignment.Top; card.Margin = new Thickness(60, 14, 0, 18);
        Content = new ScrollViewer { Content = card };
    }

    async void Send()
    {
        if (message.Trim().Length < 3) { status = "Write a few words first."; Build(); return; }
        sending = true; status = ""; Build();
        try { string id = await FeedbackClient.Send(category, message, controller, connection); status = "Sent. Your receipt is " + id + "."; message = ""; }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or InvalidOperationException or System.Text.Json.JsonException) { status = "Couldn't send it: " + e.Message; }
        sending = false; Build();
    }
}
