using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using X20Ctl.Zone;
namespace X20Ctl.Linux;

/// <summary>Four player slots and which controller each one holds. Everything else (Studio, chooser) hangs off this.</summary>
internal sealed class Session
{
    public ControllerCatalog Catalog { get; }
    public ControllerModel?[] Players { get; } = new ControllerModel?[4];
    public int Selected { get; set; }
    public StudioState?[] Studios { get; } = new StudioState?[4];
    public Session()
    {
        Catalog = new ControllerCatalog(File.ReadAllText(Path.Combine(AppContent.Root, "Data", "catalog.json")));
        Players[0] = Catalog.Find("x20_pro");
    }
    public StudioState? StudioFor(int slot) => Players[slot] is { } m ? Studios[slot] ??= Made(m, slot) : null;
    static StudioState Made(ControllerModel m, int slot) { var s = new StudioState(m, slot); PcModeHub.Shared.Prepare(s); return s; }
}

/// <summary>The desktop window: it holds the <see cref="MainView"/>, which is the whole app.</summary>
public sealed class MainWindow : Window
{
    public MainView View { get; } = new();
    public MainWindow()
    {
        Title = "X20CTL"; Width = 1500; Height = 900; MinWidth = 1100; MinHeight = 680; FontFamily = T.Font; Background = new SolidColorBrush(T.Void);
        Content = View;
    }
    /// <summary>--shots DIRECTORY: renders the screens, then closes.</summary>
    public async Task Shots(string directory) { await View.Shots(directory); Close(); }
}

/// <summary>The app: the stage behind, a header, the current screen, and the hint row. Screens swap in place, the old
/// one fading away as the new one arrives, with the Windows app's values (X20Ctl.Product/ProductHost.cs). The desktop
/// window holds it; the Android app shows it on its own.</summary>
public sealed class MainView : UserControl, IIntroHandoff
{
    readonly Session session = new();
    readonly Grid root = new() { RowDefinitions = new("Auto,*,Auto") };
    readonly ContentControl body = new();
    readonly ContentControl header = new(), footer = new();
    readonly List<TextBlock> clocks = [];
    TextBlock NewClock() { var c = T.Label(DateTime.Now.ToString("HH:mm"), 14, T.Soft); c.VerticalAlignment = VerticalAlignment.Center; clocks.Add(c); return c; }
    // the logo alone for the intro; the app icon (the logo on its white tile) everywhere else
    readonly Bitmap? mark = Load("x20ctl-mark.png"), icon = Load("x20ctl-icon.png");
    StudioView? studio;
    ZonePage? zone;
    // the header's logo, name, divider, section and right-hand controls: the intro hands over to them
    Control? brandTile, brandMark, brandWord, brandRule, brandLabel, headerActions;
    string screen = "zone";
    int choosing;

    public MainView()
    {
        FontFamily = T.Font; Background = new SolidColorBrush(T.Void);
        var stage = new Grid(); stage.Children.Add(new Backdrop()); stage.Children.Add(root); Content = stage;
        root.Children.Add(header); Grid.SetRow(body, 1); root.Children.Add(body); Grid.SetRow(footer, 2); root.Children.Add(footer);
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) }; timer.Tick += (_, _) => { foreach (var c in clocks) c.Text = DateTime.Now.ToString("HH:mm"); }; timer.Start();
        ShowZone(animate: false);
        bool live = !ReviewMode.Headless && Environment.GetCommandLineArgs().All(a => a != "--shots");
        // keys reach the window (or the phone's view) wherever focus is; the window's render clock drives every animation
        TopLevel? top = null;
        void KeyUpHandler(object? _, KeyEventArgs e) { if (screen == "studio") studio?.HandleKey(e, false); }
        AttachedToVisualTree += async (_, _) =>
        {
            if (top != null || TopLevel.GetTopLevel(this) is not { } t) return;
            top = t; Motion.Clock = t;
            t.AddHandler(KeyDownEvent, OnKey); t.AddHandler(KeyUpEvent, KeyUpHandler);
            if (!live) return;
            live = false;
            if (AppSettings.Shared.IntroOnLaunch) { ShowIntro(); while (intro != null) await Task.Delay(100); }
            if (AppSettings.Shared.AutoUpdate && !Phone.Active) await OfferUpdate();
        };
        DetachedFromVisualTree += (_, _) => { if (top == null) return; top.RemoveHandler(KeyDownEvent, OnKey); top.RemoveHandler(KeyUpEvent, KeyUpHandler); top = null; };
    }

    static Bitmap? Load(string file)
    {
        try { return new Bitmap(AssetLoader.Open(new Uri("avares://x20ctl-studio/Assets/" + file))); } catch (Exception) { return null; }
    }

    void OnKey(object? sender, KeyEventArgs e)
    {
        if (screen == "zone")
        {
            if (e.Key >= Key.D1 && e.Key <= Key.D4) zone?.Select(e.Key - Key.D1);
            else if (e.Key == Key.Enter) EnterStudio();
        }
        else if (screen is "choose" or "tools" && e.Key == Key.Escape) ShowZone();
        else if (screen is "about" or "feedback" or "check" && e.Key == Key.Escape) ShowTools();
        else if (screen == "studio") { studio?.HandleKey(e); if (!e.Handled && e.Key == Key.Escape) ShowZone(); }
    }

    // ---- chrome -------------------------------------------------------------------------------------------------

    Control ZoneHeader(string section, Control? right)
    {
        // a phone's header is a little shorter, leaving the room to the screen under it
        double size = Phone.Active ? 40 : 46;
        var mark = new Image { Source = icon, Width = size, Height = size };
        var tile = new Border { Width = size, Height = size, Child = icon == null ? null : mark, RenderTransformOrigin = RelativePoint.Center };
        var word = T.Label("X20CTL", Phone.Active ? 22 : 24, null, FontWeight.Bold);
        var rule = new Border { Width = 1, Height = 22, Background = T.Hairline, VerticalAlignment = VerticalAlignment.Center };
        var title = T.Label(section, Phone.Active ? 17 : 18, T.Soft); title.VerticalAlignment = VerticalAlignment.Center;
        var left = T.Line(14, tile, word, rule, title); left.VerticalAlignment = VerticalAlignment.Center;
        var bar = new Grid { Margin = Phone.Active ? new Thickness(24, 8, 24, 0) : new Thickness(26, 16, 26, 0), Height = Phone.Active ? 50 : 56, ColumnDefinitions = new("*,Auto") };
        bar.Children.Add(left);
        var tail = T.Line(18, right ?? new Border(), NewClock()); tail.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(tail, 1); bar.Children.Add(tail);
        (brandTile, brandMark, brandWord, brandRule, brandLabel, headerActions) = (tile, mark, word, rule, title, tail);
        return bar;
    }

    /// <summary>Puts a screen in: the old one fades and drifts away behind it while the new one arrives from
    /// <paramref name="fromScale"/>.</summary>
    void Swap(Control head, Control page, Control foot, double fromScale, int ms, bool animate = true)
    {
        if (animate && Motion.Enabled && Content is Grid stage && header.Content is Control oldHead && body.Content is Control oldPage && footer.Content is Control oldFoot)
        {
            header.Content = null; body.Content = null; footer.Content = null;
            var leaving = new Grid { RowDefinitions = new("Auto,*,Auto") };
            leaving.Children.Add(oldHead); Grid.SetRow(oldPage, 1); leaving.Children.Add(oldPage); Grid.SetRow(oldFoot, 2); leaving.Children.Add(oldFoot);
            stage.Children.Insert(stage.Children.IndexOf(root), leaving);
            Motion.Leave(leaving, () => stage.Children.Remove(leaving), fromScale < 1 ? 1.03 : .985, fromScale < 1 ? 220 : 200);
        }
        header.Content = head; body.Content = page; footer.Content = foot;
        if (animate) Motion.Appear(root, fromScale, ms);
    }

    Control FootBar(Control hints, string? headline = null)
    {
        // a phone has no keys to name: the screen ends where its content does
        if (Phone.Active && headline == null) return new Border();
        var inner = new StackPanel { Spacing = 10 };
        if (headline != null) inner.Children.Add(T.Label(headline, 18, null, FontWeight.Medium));
        inner.Children.Add(hints);
        return new Border { BorderBrush = T.Hairline, BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(30, 14, 30, 16), Child = inner, Background = T.B("#33040814") };
    }

    /// <summary>The Zone's Support pill, as on Windows (ZoneSurfaces.xaml Zone.Support): a warm gradient, a white heart
    /// badge, the word, and a small menu mark; it opens Ko-fi in the browser.</summary>
    static Control SupportPill()
    {
        var heart = new Border { Width = 28, Height = 28, CornerRadius = new CornerRadius(14), Background = Brushes.White, Child = new TextBlock { Text = "♥", FontSize = 19, FontWeight = FontWeight.Bold, Foreground = T.B("#EF4367"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, -2, 0, 0) } };
        var menu = new Border { Width = 25, Height = 22, CornerRadius = new CornerRadius(11), BorderBrush = T.B("#D9FFE6D5"), BorderThickness = new Thickness(1), Background = T.B("#14FFFFFF"), Child = new TextBlock { Text = "≡", FontSize = 15, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, -2, 0, 0) } };
        var word = new TextBlock { Text = "Support", FontSize = 14, FontWeight = FontWeight.SemiBold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var row = new Grid { Margin = new Thickness(5, 0, 8, 0), ColumnDefinitions = new("28,*,25") };
        row.Children.Add(heart); Grid.SetColumn(word, 1); row.Children.Add(word); Grid.SetColumn(menu, 2); row.Children.Add(menu);
        var face = new Border
        {
            Width = 150, Height = 40, CornerRadius = new CornerRadius(20), BorderBrush = T.B("#FFB29B"), BorderThickness = new Thickness(1), Child = row, Cursor = new Cursor(StandardCursorType.Hand),
            Background = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, .2, RelativeUnit.Relative), GradientStops = { new(Color.Parse("#F33B67"), 0), new(Color.Parse("#F1653D"), .7), new(Color.Parse("#FA843D"), 1) } }
        };
        face.PointerEntered += (_, _) => face.BorderBrush = Brushes.White; face.PointerExited += (_, _) => face.BorderBrush = T.B("#FFB29B");
        face.PointerPressed += (_, _) => face.Opacity = .75; face.PointerCaptureLost += (_, _) => face.Opacity = 1;
        face.PointerReleased += (_, e) => { face.Opacity = 1; if (face.Bounds.Contains(e.GetPosition(face.Parent as Visual))) ExternalLinks.Open(ExternalLinks.Support); };
        Avalonia.Automation.AutomationProperties.SetName(face, "Support");
        return face;
    }

    // ---- screens ------------------------------------------------------------------------------------------------

    /// <summary>The Controller Zone, coming back from a deeper screen (as on Windows: from .94 over 380 ms).</summary>
    void ShowZone() => ShowZone(true);
    void ShowZone(bool animate)
    {
        screen = "zone"; studio = null;
        var tools = new Pill("Tools", Pill.Kinds.Ghost, ShowTools); tools.Padding = new Thickness(20, 10);
        zone = new ZonePage(session, EnterStudio, Choose, Remove);
        Swap(ZoneHeader("Controller Zone", T.Line(12, tools, SupportPill())), zone, FootBar(Hints.Row(("A", "Select player"), ("B", "Back"), ("≡", "Tools"))), .94, 380, animate);
    }

    // ---- tools --------------------------------------------------------------------------------------------------

    /// <summary>Tools and the screens under it: arriving from a little larger (Windows: 1.04 over 320 ms), or from a
    /// little smaller when it is a step back.</summary>
    void Tools(string screenName, string section, Control page, string back = "Controller Zone")
    {
        bool returning = screenName == "tools" && screen is "about" or "feedback" or "check";
        screen = screenName; studio = null; zone = null;
        var pill = new Pill("←  " + back, Pill.Kinds.Ghost, back == "Controller Zone" ? ShowZone : ShowTools) { Padding = new Thickness(20, 10) };
        Swap(ZoneHeader(section, pill), page, FootBar(Hints.Row(("A", "Select"), ("B", "Back"))), returning ? .96 : 1.04, returning ? 340 : 320);
    }

    void ShowTools() => Tools("tools", "Tools", new ToolsPage(new ToolsActions(ShowCheck, ShowWriteScan, () => { session.Selected = Math.Max(0, Array.FindIndex(session.Players, p => p != null)); EnterStudio("PC mode"); }, ShowIntro, ShowAbout, ShowFeedback)));
    void ShowAbout() => Tools("about", "Tools", new AboutPage(ShowTools, icon));
    void ShowFeedback() => Tools("feedback", "Tools", new FeedbackPage(ShowTools, session.Catalog.Models.Select(m => m.Name)));
    void ShowCheck() => Tools("check", "Controller Check", new CheckPage(ShowTools, () => { session.Selected = Math.Max(0, Array.FindIndex(session.Players, p => p != null)); EnterStudio("Tester"); }), "Tools");
    void ShowWriteScan() => Tools("check", "Write scan", new WriteScanPage(), "Tools");
    async Task OfferUpdate()
    {
        UpdateCheck.Release? r; try { r = await UpdateCheck.Latest(); } catch (Exception) { return; }
        if (r == null || Content is not Grid stage) return;
        // an AppImage updates itself (checked against the release's checksums); everything else is pointed to the release
        string? installed = null;
        if (AppImageUpdater.CanInstall) { var (done, message) = await AppImageUpdater.Install(r); if (done) installed = message; }
        Border? card = null;
        void Close() { if (card != null) stage.Children.Remove(card); }
        var text = new StackPanel { Spacing = 12, Children = { T.Label(installed != null ? "Update installed" : "A newer version is out", 18, null, FontWeight.Medium), T.Label(installed ?? $"{r.Title}  ·  you have {AppInfo.Shown}", 14, T.Soft),
            T.Line(10, new Pill(installed != null ? "Release notes" : "View release", Pill.Kinds.Primary, () => { ExternalLinks.Open(r.Page); Close(); }), new Pill(installed != null ? "OK" : "Later", Pill.Kinds.Ghost, Close)) } };
        card = new Border { Background = T.B("#F0101A33"), BorderBrush = T.Hairline, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14), Padding = new Thickness(24), Child = text,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        stage.Children.Add(card);
    }
    IntroView? intro;
    /// <summary>The opening, over the Controller Zone it hands over to (Replay Intro comes back to the Zone too, as on Windows).</summary>
    void ShowIntro()
    {
        if (intro != null || Content is not Grid stage) return;
        if (screen != "zone") ShowZone(animate: false);
        intro = new IntroView(mark, this, () => { stage.Children.Remove(intro!); intro = null; body.Focus(); });
        stage.Children.Add(intro);
    }

    // ---- the intro's hand-over ---------------------------------------------------------------------------------

    // the logo inside the header's app icon (tools/build_brand_mark.py tile(): 86% wide, centred, its middle at 47%)
    static Rect MarkInk(double s) => new(.07 * s, (.47 - .43 * 662 / 951) * s, .86 * s, .86 * 662 / 951 * s);
    Rect? IIntroHandoff.MarkTarget(Visual to) =>
        brandTile is { Bounds.Width: > 0 } tile && tile.TransformToVisual(to) is { } m ? MarkInk(tile.Bounds.Width).TransformToAABB(m) : null;
    IEnumerable<Control> Revealed => new[] { headerActions, brandRule, brandLabel, brandTile, zone?.Heading, footer }.Concat(zone?.Cards ?? []).OfType<Control>();
    void IIntroHandoff.HandoffBegin()
    {
        foreach (var c in Revealed) c.Opacity = 0;
        if (brandMark != null) brandMark.Opacity = 0;
        if (brandWord != null) brandWord.Opacity = 0;
    }
    /// <summary>Header first, then the heading, the cards about 60 ms apart, the footer last; the logo and name as it lands.</summary>
    void IIntroHandoff.Handoff(double reveal, double brand, double pulse)
    {
        static double In(double t, double a, double b) { double x = Math.Clamp((t - a) / (b - a), 0, 1); return x * x * (3 - 2 * x); }
        foreach (var c in new[] { headerActions, brandRule, brandLabel }) if (c != null) c.Opacity = In(reveal, .15, .55);
        if (brandTile != null) { brandTile.Opacity = In(reveal, .35, .7); brandTile.RenderTransform = pulse == 1 ? null : new ScaleTransform(pulse, pulse); }
        if (zone != null) { zone.Heading.Opacity = In(reveal, .25, .65); var cards = zone.Cards; for (int i = 0; i < cards.Count; i++) cards[i].Opacity = In(reveal, .3 + .06 * i, .6 + .06 * i); }
        footer.Opacity = In(reveal, .45, .85);
        if (brandMark != null) brandMark.Opacity = brand;
        if (brandWord != null) brandWord.Opacity = brand;
    }
    void IIntroHandoff.HandoffEnd()
    {
        foreach (var c in Revealed) c.Opacity = 1;
        if (brandMark != null) brandMark.Opacity = 1;
        if (brandWord != null) brandWord.Opacity = 1;
        if (brandTile != null) brandTile.RenderTransform = null;
    }

    /// <summary>The phone's Back: one screen up, as Escape does. False on the Controller Zone, where Back leaves the app.</summary>
    public bool Back()
    {
        if (intro != null) { intro.Finish(); return true; }
        switch (screen)
        {
            case "zone": return false;
            case "about" or "feedback" or "check": ShowTools(); return true;
            default: ShowZone(); return true;
        }
    }

    void Choose(int slot)
    {
        screen = "choose"; choosing = slot; zone = null;
        var back = new Pill("Back", Pill.Kinds.Ghost, ShowZone);
        var page = new ChooserPage(session, slot, back, model => { session.Players[slot] = model; session.Studios[slot] = null; session.Selected = slot; ShowZone(); });
        Swap(ZoneHeader("Choose a controller", null), page, FootBar(Hints.Row(("A", "Choose"), ("B", "Back"), ("✥", "Move"))), .97, 300);
    }

    void Remove(int slot) { session.Players[slot] = null; session.Studios[slot] = null; ShowZone(); }

    void EnterStudio() => EnterStudio(null);
    void EnterStudio(string? tab)
    {
        if (session.StudioFor(session.Selected) is not { } state) return;
        screen = "studio"; zone = null;
        studio = new StudioView(state, session.Selected, ShowZone, NewClock(), icon);
        if (tab != null) studio.ShowTab(tab);
        Swap(studio.Header, studio, studio.Footer, 1.02, 300);
    }

    /// <summary>--shots DIRECTORY: renders the screens from the app's own visuals.</summary>
    public async Task Shots(string directory)
    {
        Directory.CreateDirectory(directory);
        async Task Shot(string name, bool wait = true)
        {
            if (wait) await Task.Delay(700);
            var size = new PixelSize((int)Bounds.Width, (int)Bounds.Height);
            using var bmp = new RenderTargetBitmap(size);
            bmp.Render(this); bmp.Save(Path.Combine(directory, name + ".png"));
        }
        await Shot("zone");
        // the Zone's glide, caught in the middle: Player 2 holds an X20 for it, then the slots go back as they were
        session.Players[1] = session.Catalog.Find("x20"); ShowZone(animate: false); await Task.Delay(400);
        zone!.Select(1); await Task.Delay(130 + 200); await Shot("zone-glide", wait: false); await Task.Delay(700); await Shot("zone-player-2", wait: false);
        session.Players[1] = null; session.Selected = 0; ShowZone(animate: false);
        Choose(1); await Shot("choose");
        ShowTools(); await Shot("tools"); ShowAbout(); await Shot("about"); ShowFeedback(); await Shot("feedback"); ShowCheck(); await Shot("check");
        ShowZone(); EnterStudio();
        foreach (string tab in StudioView.Tabs)
        {
            studio?.ShowTab(tab);
            await Shot(tab.ToLowerInvariant().Replace(' ', '-'));
        }
        ReviewMode.Pads = [new("/dev/input/event1", "Xbox Wireless Controller", 0x045E, 0x0B13, new PadCaps(new HashSet<int> { 0x130, 0x131, 0x133, 0x134, 0x136, 0x137, 0x138, 0x139, 0x13A, 0x13B, 0x13C, 0x13D, 0x13E }, new Dictionary<int, AbsInfo> { [0] = new(-32768, 32767, 0), [1] = new(-32768, 32767, 0), [3] = new(-32768, 32767, 0), [4] = new(-32768, 32767, 0), [2] = new(0, 255, 0), [5] = new(0, 255, 0), [0x10] = new(-1, 1, 0), [0x11] = new(-1, 1, 0) })),
            new("/dev/input/event2", "Generic USB Joystick", 0x1234, 0x5678, new PadCaps(Enumerable.Range(0x120, 12).ToHashSet(), new Dictionary<int, AbsInfo> { [0] = new(0, 255, 128), [1] = new(0, 255, 128), [2] = new(0, 255, 128), [5] = new(0, 255, 128), [0x10] = new(-1, 1, 0), [0x11] = new(-1, 1, 0) }))];
        studio?.ShowTab("PC mode"); await Shot("pc-mode-check"); ReviewMode.Pads = null;
        body.Content = new StickToolsView(session.StudioFor(session.Selected)!, () => { }); await Shot("advanced");
        ShowZone(animate: false); await Task.Delay(500);
        // one intro, held at each moment in turn: the film's frames, then the hand-over onto the header
        var film = new IntroView(mark, this, () => { }, silent: true); film.Freeze(0); ((Grid)Content!).Children.Add(film);
        foreach (double at in new[] { .5, 2.0, 3.6, 5.0, 6.3, 6.5, 6.75, 6.95, 7.2, 7.5 })
        {
            for (int i = 0; i < 100 && !film.Freeze(at); i++) await Task.Delay(20);
            await Shot("intro-" + at.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), wait: false);
        }
        film.Finish(); ((Grid)Content!).Children.Remove(film);
    }
}
