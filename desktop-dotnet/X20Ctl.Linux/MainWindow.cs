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
        Catalog = new ControllerCatalog(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "catalog.json")));
        Players[0] = Catalog.Find("x20_pro");
    }
    public StudioState? StudioFor(int slot) => Players[slot] is { } m ? Studios[slot] ??= Made(m, slot) : null;
    static StudioState Made(ControllerModel m, int slot) { var s = new StudioState(m, slot); PcModeHub.Shared.Prepare(s); return s; }
}

/// <summary>The window: the stage behind, a header, the current screen, and the hint row. Screens swap in place.</summary>
public sealed class MainWindow : Window
{
    readonly Session session = new();
    readonly Grid root = new() { RowDefinitions = new("Auto,*,Auto") };
    readonly ContentControl body = new();
    readonly ContentControl header = new(), footer = new();
    readonly List<TextBlock> clocks = [];
    TextBlock NewClock() { var c = T.Label(DateTime.Now.ToString("HH:mm"), 14, T.Soft); c.VerticalAlignment = VerticalAlignment.Center; clocks.Add(c); return c; }
    readonly Bitmap? mark = LoadMark();
    StudioView? studio;
    string screen = "zone";
    int choosing;

    public MainWindow()
    {
        Title = "X20CTL"; Width = 1500; Height = 900; MinWidth = 1100; MinHeight = 680; FontFamily = T.Font; Background = new SolidColorBrush(T.Void);
        var stage = new Grid(); stage.Children.Add(new Backdrop()); stage.Children.Add(root); Content = stage;
        root.Children.Add(header); Grid.SetRow(body, 1); root.Children.Add(body); Grid.SetRow(footer, 2); root.Children.Add(footer);
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) }; timer.Tick += (_, _) => { foreach (var c in clocks) c.Text = DateTime.Now.ToString("HH:mm"); }; timer.Start();
        KeyDown += OnKey; KeyUp += (_, e) => { if (screen == "studio") studio?.HandleKey(e, false); }; ShowZone();
        bool live = Environment.GetCommandLineArgs().All(a => a != "--shots");
        if (live) Opened += async (_, _) =>
        {
            if (AppSettings.Shared.IntroOnLaunch) { ShowIntro(); while (intro != null) await Task.Delay(100); }
            if (AppSettings.Shared.AutoUpdate) await OfferUpdate();
        };
    }

    static Bitmap? LoadMark()
    {
        try { return new Bitmap(AssetLoader.Open(new Uri("avares://x20ctl-studio/Assets/x20ctl-mark.png"))); } catch (Exception) { return null; }
    }

    void OnKey(object? sender, KeyEventArgs e)
    {
        if (screen == "zone")
        {
            if (e.Key >= Key.D1 && e.Key <= Key.D4) { session.Selected = e.Key - Key.D1; ShowZone(); }
            else if (e.Key == Key.Enter) EnterStudio();
        }
        else if (screen is "choose" or "tools" && e.Key == Key.Escape) ShowZone();
        else if (screen is "about" or "feedback" or "check" && e.Key == Key.Escape) ShowTools();
        else if (screen == "studio") { studio?.HandleKey(e); if (!e.Handled && e.Key == Key.Escape) ShowZone(); }
    }

    // ---- chrome -------------------------------------------------------------------------------------------------

    Control Logo(double size = 46) => new Border
    {
        Width = size, Height = size, CornerRadius = new CornerRadius(12), Background = T.B("#66101A33"), BorderBrush = T.Hairline, BorderThickness = new Thickness(1),
        Child = mark == null ? null : new Image { Source = mark, Margin = new Thickness(size * .2) }
    };

    Control ZoneHeader(string section, Control? right)
    {
        var left = T.Line(14, Logo(), T.Label("X20CTL", 24, null, FontWeight.Bold));
        left.Children.Add(new Border { Width = 1, Height = 22, Background = T.Hairline, VerticalAlignment = VerticalAlignment.Center });
        var title = T.Label(section, 18, T.Soft); title.VerticalAlignment = VerticalAlignment.Center; left.Children.Add(title); left.VerticalAlignment = VerticalAlignment.Center;
        var bar = new Grid { Margin = new Thickness(26, 16, 26, 0), Height = 56, ColumnDefinitions = new("*,Auto") };
        bar.Children.Add(left);
        var tail = T.Line(18, right ?? new Border(), NewClock()); tail.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(tail, 1); bar.Children.Add(tail);
        return bar;
    }

    Control FootBar(Control hints, string? headline = null)
    {
        var inner = new StackPanel { Spacing = 10 };
        if (headline != null) inner.Children.Add(T.Label(headline, 18, null, FontWeight.Medium));
        inner.Children.Add(hints);
        return new Border { BorderBrush = T.Hairline, BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(30, 14, 30, 16), Child = inner, Background = T.B("#33040814") };
    }

    // ---- screens ------------------------------------------------------------------------------------------------

    void ShowZone()
    {
        screen = "zone"; studio = null;
        var tools = new Pill("Tools", Pill.Kinds.Ghost, ShowTools); tools.Padding = new Thickness(20, 10);
        header.Content = ZoneHeader("Controller Zone", tools);
        body.Content = new ZonePage(session, Rebuild, EnterStudio, Choose, Remove);
        footer.Content = FootBar(Hints.Row(("A", "Select player"), ("B", "Back"), ("≡", "Tools")));
    }
    void Rebuild() => ShowZone();

    // ---- tools --------------------------------------------------------------------------------------------------

    void Tools(string screenName, string section, Control page, string back = "Controller Zone")
    {
        screen = screenName; studio = null;
        var pill = new Pill("←  " + back, Pill.Kinds.Ghost, back == "Controller Zone" ? ShowZone : ShowTools) { Padding = new Thickness(20, 10) };
        header.Content = ZoneHeader(section, pill); body.Content = page;
        footer.Content = FootBar(Hints.Row(("A", "Select"), ("B", "Back")));
    }

    void ShowTools() => Tools("tools", "Tools", new ToolsPage(new ToolsActions(ShowCheck, ShowWriteScan, () => { session.Selected = Math.Max(0, Array.FindIndex(session.Players, p => p != null)); EnterStudio("PC mode"); }, ShowIntro, ShowAbout, ShowFeedback)));
    void ShowAbout() => Tools("about", "Tools", new AboutPage(ShowTools, mark));
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
    void ShowIntro()
    {
        if (intro != null || Content is not Grid stage) return;
        intro = new IntroView(mark, session.Players.FirstOrDefault(pl => pl != null)?.Id ?? "x20_pro", () => { stage.Children.Remove(intro!); intro = null; body.Focus(); });
        stage.Children.Add(intro);
    }

    void Choose(int slot)
    {
        screen = "choose"; choosing = slot;
        var back = new Pill("Back", Pill.Kinds.Ghost, ShowZone);
        header.Content = ZoneHeader("Choose a controller", null);
        body.Content = new ChooserPage(session, slot, back, model => { session.Players[slot] = model; session.Studios[slot] = null; session.Selected = slot; ShowZone(); });
        footer.Content = FootBar(Hints.Row(("A", "Choose"), ("B", "Back"), ("✥", "Move")));
    }

    void Remove(int slot) { session.Players[slot] = null; session.Studios[slot] = null; ShowZone(); }

    void EnterStudio() => EnterStudio(null);
    void EnterStudio(string? tab)
    {
        if (session.StudioFor(session.Selected) is not { } state) return;
        screen = "studio";
        studio = new StudioView(state, session.Selected, ShowZone, NewClock(), mark);
        if (tab != null) studio.ShowTab(tab);
        header.Content = studio.Header; body.Content = studio; footer.Content = studio.Footer;
    }

    /// <summary>--shots DIRECTORY: renders the screens from the window's own visuals, then exits.</summary>
    public async Task Shots(string directory)
    {
        Directory.CreateDirectory(directory);
        async Task Shot(string name)
        {
            await Task.Delay(700);
            var size = new PixelSize((int)Bounds.Width, (int)Bounds.Height);
            using var bmp = new RenderTargetBitmap(size);
            bmp.Render(this); bmp.Save(Path.Combine(directory, name + ".png"));
        }
        await Shot("zone");
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
        ShowZone();
        foreach (double at in new[] { .5, 2.0, 3.6, 4.4, 5.4, 6.4 })
        {
            var frame = new IntroView(mark, "x20_pro", () => { }, silent: true) { Frozen = at }; ((Grid)Content!).Children.Add(frame);
            await Shot("intro-" + at.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)); ((Grid)Content!).Children.Remove(frame);
        }
        Close();
    }
}
