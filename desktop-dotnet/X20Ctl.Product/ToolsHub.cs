using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
namespace X20Ctl.Product;

/// <summary>Links X20CTL may open, all in the default browser and only when chosen.</summary>
public static class ExternalLinks
{
    public const string Repository = "https://github.com/AmjadAAYD/x20ctl";
    public const string Inspiration = "https://github.com/ReynArts/ApexSenseBridge";
    public static void Open(string url)
    {
        if (url is not (Repository or Inspiration or ProductLinks.Support or PcModeSetup.BusPage or PcModeSetup.HiderPage) && !url.StartsWith(Repository + "/releases", StringComparison.Ordinal)) throw new ArgumentException("Unsupported destination.");
        if (ReviewSandbox.Active) return;
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
}

/// <summary>
/// Tools (owner direction 10 Oct 2026, replacing the plain Settings page and the Zone's gear): a console-style hub
/// with the Controller Check as its hero, then Replay Intro, About, Support and the GitHub repository.
/// About credits ReynArts' ApexSenseBridge as the design inspiration.
/// </summary>
public sealed partial class ProductSettings : Grid
{
    public Button Replay { get; }
    public Button Support { get; }
    public Button Check { get; }
    public Button About { get; }
    public Button Star { get; }
    public Button Back { get; }
    public Button Feedback { get; }
    public Button PcMode { get; }
    public Button WriteScan { get; }
    public Button Themes { get; }
    internal Action<string>? ThemeNavigate { get; set; }
    internal Action? RestartRequested { get; set; }
    private void RequestThemeRestart() => (RestartRequested ?? ThemeCatalog.Restart)();
    private Grid? pageGrid;
    // the feedback and PC mode pages are built the first time they're opened: Tools is rebuilt on every visit, and most
    // visits never open them
    private FeedbackPage? feedbackForm;
    private readonly Func<PcModePage>? buildPcMode;
    public FeedbackPage FeedbackForm => feedbackForm ??= BuildFeedback();
    private readonly Grid hub = new(), about = new() { Visibility = Visibility.Collapsed }, feedback = new() { Visibility = Visibility.Collapsed }, pcMode = new() { Visibility = Visibility.Collapsed }, themes = new() { Visibility = Visibility.Collapsed };
    public bool ShowingThemes => themes.Visibility == Visibility.Visible;
    public bool ShowingPcMode => pcMode.Visibility == Visibility.Visible;
    private readonly Button aboutBack;
    public bool ShowingAbout => about.Visibility == Visibility.Visible;
    public bool ShowingFeedback => feedback.Visibility == Visibility.Visible;

    private readonly X20Ctl.Zone.ProductPreferences? preferences;
    private readonly PcModeService? pcModeService;
    private readonly X20Ctl.Zone.PcMode.PcModeStore? pcModeStore;
    private readonly Border updates = new(), introBar = new();
    private readonly Grid footer = new() { Margin = new(0, 24, 0, 0) };
    private readonly CheckBox introLaunch = new() { Style = DS.Style("DS.Toggle"), VerticalAlignment = VerticalAlignment.Center },
        introSound = new() { Style = DS.Style("DS.Toggle"), VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock introStatus = Kit.Text("", 13.5, "DS.TextSoft").Also(t => { t.TextWrapping = TextWrapping.Wrap; t.TextTrimming = TextTrimming.None; });
    private readonly TextBlock updateStatus = Kit.Text("", 13.5, "DS.TextSoft");
    private readonly Button updateCheck, updateGo;
    private readonly CheckBox autoUpdate = new() { Style = DS.Style("DS.Toggle"), VerticalAlignment = VerticalAlignment.Center };

    public ProductSettings(Action support, Action replay, Action close, Action check, X20Ctl.Zone.ProductPreferences? preferences = null, PcModeService? pcModeService = null, X20Ctl.Zone.PcMode.PcModeStore? pcModeStore = null, Action? writeScan = null)
    {
        this.preferences = preferences;
        this.pcModeService = pcModeService;
        this.pcModeStore = pcModeStore;
        Background = new SolidColorBrush(ThemeCatalog.Tone(ThemeCatalog.R(Color.FromRgb(5, 9, 20)), t => t.Void));
        if (ThemeCatalog.Current.Id == "pulse")
        {
            var cymatic = Pulse.PulseKit.VoidBackdrop(hero: false);
            var wave = Pulse.PulseKit.WaveformRibbon(785, [320, 800, 1280], [true, true, true], 1, 1.45);
            wave.Opacity = 0.55;
            cymatic.Children.Add(wave);
            Children.Add(new Viewbox { Child = cymatic, Stretch = Stretch.Fill, IsHitTestVisible = false });
        }
        else Children.Add(new FacetBackdrop());
        Children.Add(new Border { Background = new RadialGradientBrush(ThemeCatalog.Tone(ThemeCatalog.R(Color.FromArgb(0, 5, 9, 20)), t => ThemeCatalog.Alpha(t.Void, 0)), ThemeCatalog.Tone(ThemeCatalog.R(Color.FromArgb(170, 3, 6, 14)), t => ThemeCatalog.Alpha(t.Void, 170))) { RadiusX = .8, RadiusY = .8 }, IsHitTestVisible = false });
        var page = pageGrid = new Grid { Margin = new(64, 40, 64, 40) };
        page.RowDefinitions.Add(new() { Height = GridLength.Auto }); page.RowDefinitions.Add(new()); page.RowDefinitions.Add(new() { Height = GridLength.Auto });
        Children.Add(page); pageGrid = page;

        // header: mark, title, back
        var header = new DockPanel { Margin = new(0, 0, 0, 30) };
        Back = Pill("", "Controller Zone"); Back.Click += (_, _) => close();
        DockPanel.SetDock(Back, Dock.Right); header.Children.Add(Back);
        var mark = ThemeCatalog.Current.Id == "pulse" ? Pulse.PulseKit.LogoMark(64).Also(m => m.Margin = new(0, 0, 18, 0)) : new Image { Source = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Assets/icon.png")), Width = 44, Height = 44, Margin = new(0, 0, 18, 0) };
        RenderOptions.SetBitmapScalingMode(mark, BitmapScalingMode.HighQuality);
        header.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Children = { mark, new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = {
            Kit.Text("Tools", 44, "DS.Text", FontWeights.Bold), Kit.Text("Help, extras and everything about X20CTL", 17, "DS.TextSoft") } } } });
        page.Children.Add(header);

        // hub: the Controller Check hero on the left, four tiles on the right
        hub.ColumnDefinitions.Add(new() { Width = new(1.05, GridUnitType.Star) }); hub.ColumnDefinitions.Add(new() { Width = new(24) }); hub.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        Grid.SetRow(hub, 1); page.Children.Add(hub);
        Check = Card("", "Controller not working?", "Read scan, then Write scan, then send both reports online together.", Kit.Blue, hero: true);
        Check.Click += (_, _) => check(); hub.Children.Add(Check);
        var tiles = new Grid(); tiles.RowDefinitions.Add(new()); tiles.RowDefinitions.Add(new() { Height = new(24) }); tiles.RowDefinitions.Add(new());
        tiles.RowDefinitions.Add(new() { Height = new(24) }); tiles.RowDefinitions.Add(new());
        tiles.RowDefinitions.Add(new() { Height = new(24) }); tiles.RowDefinitions.Add(new());
        tiles.ColumnDefinitions.Add(new()); tiles.ColumnDefinitions.Add(new() { Width = new(24) }); tiles.ColumnDefinitions.Add(new());
        Grid.SetColumn(tiles, 2); hub.Children.Add(tiles);
        Replay = Card("", "Replay Intro", "Watch the X20CTL opening again.", Kit.Blue);
        About = Card("", "About", "What X20CTL is, who made it, and credits.", ThemeCatalog.R(Color.FromRgb(150, 175, 215)));
        Support = Card("", "Support X20CTL", "Help keep it going on Ko-fi.", ThemeCatalog.R(Color.FromRgb(255, 92, 122)));
        Star = Card("", "Star on GitHub", "AmjadAAYD/x20ctl", ThemeCatalog.R(Color.FromRgb(245, 200, 80))); // short enough that the tile never cuts it
        Replay.Click += (_, _) => replay(); Support.Click += (_, _) => support(); Star.Click += (_, _) => Try(() => ExternalLinks.Open(ExternalLinks.Repository));
        About.Click += (_, _) => ShowAbout(true);
        PcMode = Card("", "PC mode", "Remaps, turbo and macros for any pad.", ThemeCatalog.R(Color.FromRgb(52, 211, 153)));
        PcMode.Click += (_, _) => ShowPcMode(true);
        Place(tiles, PcMode, 0, 0);
        // the Read scan's partner: learns what an official app writes, from a phone's Bluetooth log
        WriteScan = Card("", "Write scan", "What an app writes. 20 to 40 min.", ThemeCatalog.R(Color.FromRgb(110, 160, 235)));
        WriteScan.Click += (_, _) => writeScan?.Invoke();
        Place(tiles, WriteScan, 0, 2);
        Place(tiles, Replay, 2, 0); Place(tiles, About, 2, 2); Place(tiles, Support, 4, 0); Place(tiles, Star, 4, 2);
        Feedback = Card("", "Send feedback", "Report a bug, ask for a feature or tell us about your controller.", ThemeCatalog.R(Color.FromRgb(110, 160, 235)));
        Feedback.Click += (_, _) => ShowFeedback(true);
        Place(tiles, Feedback, 6, 2);
        // the look of the whole app (owner request 26 Oct 2026): ten themes, each with its own opening
        Themes = Card("", "Themes", "Layouts, controls and the opening intro.", Kit.Blue);
        Themes.Click += (_, _) => ShowThemes(true);
        Place(tiles, Themes, 6, 0);
        if (pcModeService == null || pcModeStore == null) { PcMode.Visibility = Visibility.Collapsed; Grid.SetColumn(WriteScan, 0); Grid.SetColumnSpan(WriteScan, 3); }
        if (writeScan == null) { WriteScan.Visibility = Visibility.Collapsed; Grid.SetColumnSpan(PcMode, 3); }

        // updates: a slim bar under the hub (owner request, 10 Oct 2026): check GitHub releases at launch, off by default
        updateCheck = Pill("", "Check now"); updateCheck.Click += async (_, _) => await CheckForUpdate();
        updateGo = Pill("", "Update now"); updateGo.Visibility = Visibility.Collapsed; updateGo.Margin = new(12, 0, 0, 0);
        updateGo.Click += async (_, _) => await GetUpdate();
        autoUpdate.IsChecked = preferences?.AutoUpdate == true; autoUpdate.IsEnabled = preferences != null;
        autoUpdate.Checked += (_, _) => SetAuto(true); autoUpdate.Unchecked += (_, _) => SetAuto(false);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Children = { updateCheck, updateGo } };
        DockPanel.SetDock(buttons, Dock.Right);
        updates.Child = new DockPanel { Children = { buttons, new StackPanel { Orientation = Orientation.Horizontal, Children = { autoUpdate, new StackPanel { Margin = new(14, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Children = {
            Kit.Text("Keep X20CTL up to date", 16, "DS.Text", FontWeights.SemiBold), updateStatus } } } } } };
        updateStatus.TextWrapping = TextWrapping.Wrap; updateStatus.TextTrimming = TextTrimming.None;

        // the opening intro (owner request 12 Oct 2026): play it at launch or not, with sound or without; the intro's own
        // Mute button sets the same switch
        introLaunch.IsChecked = preferences?.IntroOnLaunch != false; introSound.IsChecked = IntroView.HasIntroScore && preferences?.IntroSound != false;
        introLaunch.IsEnabled = preferences != null; introSound.IsEnabled=preferences!=null && IntroView.HasIntroScore;
        introLaunch.Checked += (_, _) => SetIntro(true, null); introLaunch.Unchecked += (_, _) => SetIntro(false, null);
        introSound.Checked += (_, _) => SetIntro(null, true); introSound.Unchecked += (_, _) => SetIntro(null, false);
        StackPanel Switch(CheckBox box, string label, double gap) => new() { Orientation = Orientation.Horizontal, Margin = new(gap, 0, 0, 0), Children = { box, Kit.Text(label, 15, "DS.Text").Also(t => t.Margin = new(10, 0, 0, 0)) } };
        // one row, as tall as the updates bar beside it: a taller bar squeezed the tiles above until their words were cut
        var soundSwitch=Switch(introSound,"Sound",22);soundSwitch.Visibility=IntroView.HasIntroScore?Visibility.Visible:Visibility.Collapsed;
        var switches = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Children = { Switch(introLaunch, "At launch", 0), soundSwitch } };
        DockPanel.SetDock(switches, Dock.Right);
        introBar.Child = new DockPanel { Children = { switches, new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new(0, 0, 16, 0), Children = {
            Kit.Text("Opening intro", 16, "DS.Text", FontWeights.SemiBold), introStatus } } } };
        ShowIntroStatus();

        foreach (var bar in new[] { updates, introBar })
        {
            bar.CornerRadius = ThemeCatalog.Round(16); bar.Padding = new(22, 14, 18, 14); bar.BorderThickness = new(1);
            bar.Background = new SolidColorBrush(ThemeCatalog.Tone(ThemeCatalog.R(Color.FromArgb(200, 14, 23, 48)), t => ThemeCatalog.Alpha(ThemeCatalog.Mix(t.Night, t.Text, .04), 210)));
            bar.BorderBrush = new SolidColorBrush(ThemeCatalog.Tone(ThemeCatalog.R(Color.FromArgb(58, 120, 160, 230)), t => ThemeCatalog.Alpha(ThemeCatalog.Mix(t.Rule, t.Text, .3), 110)));
            if (ThemeCatalog.Current.Id == "pulse") { bar.Background = Pulse.PulseInk.PearlCardBg; bar.BorderBrush = Pulse.PulseInk.B(Pulse.PulseInk.A(Pulse.PulseInk.C("#88c8ff"), 0.34)); }
        }
        footer.ColumnDefinitions.Add(new() { Width = new(1.3, GridUnitType.Star) }); footer.ColumnDefinitions.Add(new() { Width = new(24) }); footer.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        footer.Children.Add(updates); Grid.SetColumn(introBar, 2); footer.Children.Add(introBar);
        Grid.SetRow(footer, 2); page.Children.Add(footer);
        ShowUpdateStatus();

        // feedback
        Grid.SetRow(feedback, 1); page.Children.Add(feedback);

        // PC mode
        Grid.SetRow(pcMode, 1); page.Children.Add(pcMode);
        Grid.SetRow(themes, 1); page.Children.Add(themes);
        if (pcModeService != null && pcModeStore != null)
            buildPcMode = () => { var pcBack = Pill("", "Back to Tools"); pcBack.Click += (_, _) => ShowPcMode(false); return new PcModePage(pcModeService, pcModeStore, preferences, pcBack); };

        // about
        Grid.SetRow(about, 1); page.Children.Add(about);
        aboutBack = Pill("", "Back to Tools"); aboutBack.Click += (_, _) => ShowAbout(false);
        about.Children.Add(BuildAbout());

        if (ThemeCatalog.Current.Id == "rebel") BuildRebel();
        if (ThemeCatalog.Current.Id == "hybrid") BuildHybridSettings();
        if (DS.Motion) Loaded += (_, _) => Enter(hub);
        if (ThemeCatalog.Current.Id == "pulse") BuildPulse();
        if (ThemeCatalog.Current.Id == "spotlight") BuildSpotlight();
        if (ThemeCatalog.Current.Id == "nexus") BuildNexus();
        if (ThemeCatalog.Current.Id == "tide") BuildTide();
        if (ThemeCatalog.Current.Id == "hybrid") BuildHybridSettings();
        if (ThemeCatalog.Current.Id == "dusk") BuildDusk();
        if (ThemeCatalog.Current.Id == "volt") BuildVolt();
        if (ThemeCatalog.Current.Id == "blossom") BuildBlossom();
    }

    public void ShowAbout(bool on)
    {
        hub.Visibility = on ? Visibility.Collapsed : Visibility.Visible; about.Visibility = on ? Visibility.Visible : Visibility.Collapsed; footer.Visibility = hub.Visibility; RebelSync(); HybridSettingsSync();
        if (DS.Motion) Enter(on ? about : hub);
        (on ? aboutBack : About).Focus();
        PulseSync(); SpotlightSync(); NexusSync(); TideSync(); HybridSettingsSync(); DuskSync(); VoltSync(); BlossomSync();
    }
    /// <summary>B / Esc: from About back to the hub, from the hub back to the Zone.</summary>
    private FeedbackPage BuildFeedback()
    {
        var feedbackBack = Pill("", "Back to Tools"); feedbackBack.Click += (_, _) => ShowFeedback(false);
        var form = new FeedbackPage(feedbackBack, Scanner.ControllerCheckView.Models.Where(m => m != "Unknown"));
        feedback.Children.Add(new ScrollViewer { Content = form, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        return form;
    }
    /// <summary>Puts the real feedback form (message, category, controller, Send) on a themed page.</summary>
    internal void MountFeedbackForm(Panel host, double left, double top, double width, double height)
    {
        var form = FeedbackForm;
        FeedbackPage.Detach(form);
        form.Background = Brushes.Transparent;
        form.BorderThickness = new Thickness(0);
        form.Padding = new Thickness(0, 0, 18, 0);
        form.MaxWidth = width - 20;
        var scroll = new ScrollViewer { Width = width, Height = height, Content = form, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Canvas.SetLeft(scroll, left);
        Canvas.SetTop(scroll, top);
        host.Children.Add(scroll);
    }
    public void ShowFeedback(bool on)
    {
        if (on) _ = FeedbackForm;
        hub.Visibility = on ? Visibility.Collapsed : Visibility.Visible; feedback.Visibility = on ? Visibility.Visible : Visibility.Collapsed; footer.Visibility = hub.Visibility; RebelSync(); HybridSettingsSync();
        if (DS.Motion) Enter(on ? feedback : hub);
        if (on) FeedbackForm.Message.Focus(); else Feedback.Focus();
        PulseSync(); SpotlightSync(); NexusSync(); TideSync(); HybridSettingsSync(); DuskSync(); VoltSync(); BlossomSync();
    }
    public void ShowPcMode(bool on)
    {
        if (on && pcMode.Children.Count == 0 && buildPcMode != null)
            pcMode.Children.Add(new ScrollViewer { Content = buildPcMode(), VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        hub.Visibility = on ? Visibility.Collapsed : Visibility.Visible; pcMode.Visibility = on ? Visibility.Visible : Visibility.Collapsed; footer.Visibility = hub.Visibility; RebelSync(); HybridSettingsSync();
        if (DS.Motion) Enter(on ? pcMode : hub);
        if (!on) PcMode.Focus();
        PulseSync(); SpotlightSync(); NexusSync(); TideSync(); HybridSettingsSync(); DuskSync(); VoltSync(); BlossomSync();
    }
    public void ShowThemes(bool on)
    {
        if (on && themes.Children.Count == 0) themes.Children.Add(BuildThemes());
        hub.Visibility = on ? Visibility.Collapsed : Visibility.Visible; themes.Visibility = on ? Visibility.Visible : Visibility.Collapsed; footer.Visibility = hub.Visibility; RebelSync(); HybridSettingsSync();
        if (DS.Motion) Enter(on ? themes : hub);
        if (on) themeCards.FirstOrDefault(c => c.Tag as string == ChosenTheme)?.Focus(); else Themes.Focus();
        PulseSync(); SpotlightSync(); NexusSync(); TideSync(); HybridSettingsSync(); DuskSync(); VoltSync(); BlossomSync();
    }

    public bool StepBack()
    {
        if (ShowingThemes) { ShowThemes(false); return true; }
        if (ShowingPcMode) { ShowPcMode(false); return true; }
        if (ShowingFeedback) { ShowFeedback(false); return true; }
        if (!ShowingAbout) return false; ShowAbout(false); return true;
    }
    public void FocusFirst() { if (rebelRoot != null && rebelRoot.IsVisible) RebelFocus("t:pcmode"); else if (hybridSettingsRoot != null && hybridSettingsRoot.IsVisible) HybridSettingsFocus("hybrid:tools:check"); else (pulseRoot != null && pulseRoot.IsVisible && pulseTiles.Count > 0 ? pulseTiles[0] : Check).Focus(); }

    private void SetAuto(bool on)
    {
        try { preferences?.SetAutoUpdate(on); ShowUpdateStatus(); }
        catch (Exception error) when (error is System.IO.IOException or InvalidOperationException or UnauthorizedAccessException) { updateStatus.Text = "Couldn't save this setting: " + error.Message; }
    }
    private void SetIntro(bool? launch, bool? sound)
    {
        try { if (launch is bool l) preferences?.SetIntroOnLaunch(l); if (sound is bool s) preferences?.SetIntroSound(s); ShowIntroStatus(); }
        catch (Exception error) when (error is System.IO.IOException or InvalidOperationException or UnauthorizedAccessException) { introStatus.Text = "Couldn't save this setting: " + error.Message; }
    }
    private void ShowIntroStatus() => introStatus.Text = introLaunch.IsChecked != true ? "Off. Replay Intro still plays it."
        : !IntroView.HasIntroScore ? "Esc skips it." : introSound.IsChecked == true ? "Esc skips it, M mutes it." : "Plays without sound.";
    private void ShowUpdateStatus()
    {
        updateGo.Visibility = Updater.Ready != null && Updater.Installed == null ? Visibility.Visible : Visibility.Collapsed;
        if (Updater.Ready is { } r) SetPill(updateGo, Updater.CanInstall ? "Update now" : "Get it on GitHub");
        updateStatus.Text = Updater.Installed is { } v ? $"{X20Ctl.Zone.ReleaseVersions.Display(v)} is installed. It starts the next time you open X20CTL."
            : Updater.Ready is { } ready ? $"{ready.Title} is out. You have {Updater.Shown}."
            : autoUpdate.IsChecked == true ? $"On: each launch checks GitHub for a newer version and installs it. You have {Updater.Shown}."
            : $"Off. Turn it on to check GitHub for a newer version each launch. You have {Updater.Shown}.";
    }
    private async Task CheckForUpdate()
    {
        updateCheck.IsEnabled = false; updateStatus.Text = "Checking GitHub…";
        try { var r = await Updater.Latest(); Updater.Ready = r; ShowUpdateStatus(); if (r == null) updateStatus.Text = $"You have the newest version ({Updater.Shown})."; }
        catch (Exception error) when (error is System.Net.Http.HttpRequestException or TaskCanceledException or System.Text.Json.JsonException or InvalidOperationException or KeyNotFoundException) { updateStatus.Text = "Couldn't reach GitHub: " + error.Message; }
        finally { updateCheck.IsEnabled = true; }
    }
    private async Task GetUpdate()
    {
        if (Updater.Ready is not { } r) return;
        if (!Updater.CanInstall) { Try(() => ExternalLinks.Open(r.Page)); return; }
        updateGo.IsEnabled = false; updateStatus.Text = $"Downloading {r.Title}…";
        try { var (_, message) = await Updater.Install(r); ShowUpdateStatus(); updateStatus.Text = message; }
        catch (Exception error) when (error is System.Net.Http.HttpRequestException or TaskCanceledException) { updateStatus.Text = "The download failed: " + error.Message; }
        finally { updateGo.IsEnabled = true; }
    }
    private static void SetPill(Button b, string text) { if (b.Content is StackPanel s && s.Children.Count > 1 && s.Children[1] is TextBlock t) t.Text = text; }

    private UIElement BuildAbout()
    {
        var version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "preview";
        var panel = new Border
        {
            CornerRadius = ThemeCatalog.Round(22), BorderThickness = new(1), Padding = new(48, 40, 48, 40), MaxWidth = 1100, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
            Background = new LinearGradientBrush(ThemeCatalog.Tone(ThemeCatalog.R(Color.FromArgb(215, 14, 24, 52)), t => ThemeCatalog.Alpha(ThemeCatalog.Mix(t.Night, t.Text, .05), 225)), ThemeCatalog.Tone(ThemeCatalog.R(Color.FromArgb(200, 8, 14, 34)), t => ThemeCatalog.Alpha(t.Deep, 215)), 90),
            BorderBrush = new LinearGradientBrush(ThemeCatalog.Tone(ThemeCatalog.R(Color.FromArgb(110, 120, 170, 255)), t => ThemeCatalog.Alpha(ThemeCatalog.Mix(t.Rule, t.Text, .35), 130)), ThemeCatalog.Tone(ThemeCatalog.R(Color.FromArgb(30, 120, 170, 255)), t => ThemeCatalog.Alpha(t.Rule, 40)), 90)
        };
        var stack = new StackPanel(); panel.Child = stack;
        stack.Children.Add(Kit.Text("Credits · Desktop-control layout inspired by Gopher360 — Tylemagne, GPL-3.0. See the distributed open-source license notices.",13,"DS.TextMuted").Also(t=>{t.TextWrapping=TextWrapping.Wrap;t.Margin=new(0,12,0,16);}));
        var mark = ThemeCatalog.Current.Id == "pulse" ? Pulse.PulseKit.LogoMark(92).Also(m => m.Margin = new(0, 0, 24, 0)) : new Image { Source = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Assets/icon.png")), Width = 72, Height = 72, Margin = new(0, 0, 24, 0) };
        if (ThemeCatalog.Current.Id == "pulse") { panel.Background = Pulse.PulseInk.PearlCardBg; stack.Children.Add(Pulse.PulseKit.PrismTopBar(0).Also(b => b.Margin = new(0, -40, 0, 16))); stack.Children.Add(Pulse.PulseKit.MiniEcgStrip("432 Hz · HARMONIC STUDIO ARCHITECTURE")); }
        RenderOptions.SetBitmapScalingMode(mark, BitmapScalingMode.HighQuality);
        stack.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Children = { mark, new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = {
            Kit.Text("X20CTL", 38, "DS.Text", FontWeights.Bold), Kit.Text($"Native Controller Studio · {X20Ctl.Zone.ReleaseVersions.Display(version)}", 16, "DS.TextSoft") } } } });
        TextBlock Para(string text, double top = 18, string brush = "DS.TextSoft", double size = 16.5) => new() { Text = text, FontFamily = DS.Body, FontSize = size, Foreground = DS.Brush(brush), TextWrapping = TextWrapping.Wrap, LineHeight = 26, Margin = new(0, top, 0, 0) };
        stack.Children.Add(Para("X20CTL is an independent, open-source controller studio for EasySMX pads. Remap buttons, shape stick and trigger response, build back-paddle macros, set vibration, and check that a controller works, all on your own PC. There is no account, and nothing leaves your PC unless you choose to send a report or feedback.", 28, "DS.Text", 18));
        stack.Children.Add(Para("Every change stays a local draft until it can be sent to a connected, verified controller. X20CTL never writes to a controller it cannot read back."));
        stack.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(ThemeCatalog.R(Color.FromArgb(40, 255, 255, 255))), Margin = new(0, 30, 0, 24) });
        stack.Children.Add(Kit.Text("MADE BY", 13, "DS.TextMuted", FontWeights.SemiBold));
        stack.Children.Add(Para("Amjad AAYD, who designs and builds X20CTL.", 6, "DS.Text", 18));
        stack.Children.Add(Kit.Text("THANKS", 13, "DS.TextMuted", FontWeights.SemiBold).Also(t => t.Margin = new(0, 24, 0, 0)));
        stack.Children.Add(Para("Special thanks to Kiin from Team EasySMX for sending me a free X20 Pro controller.", 6, "DS.Text", 18));
        stack.Children.Add(Kit.Text("INSPIRATION", 13, "DS.TextMuted", FontWeights.SemiBold).Also(t => t.Margin = new(0, 24, 0, 0)));
        stack.Children.Add(Para("The console-style look of X20CTL was inspired by ApexSenseBridge by ReynArts. Thank you for showing how good a controller app can feel.", 6, "DS.Text", 18));
        var links = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 32, 0, 0) };
        var repo = Pill("", "X20CTL on GitHub"); repo.Click += (_, _) => Try(() => ExternalLinks.Open(ExternalLinks.Repository));
        var apex = Pill("", "ApexSenseBridge by ReynArts"); apex.Click += (_, _) => Try(() => ExternalLinks.Open(ExternalLinks.Inspiration));
        repo.Margin = apex.Margin = aboutBack.Margin = new(0, 0, 12, 0);
        links.Children.Add(aboutBack); links.Children.Add(repo); links.Children.Add(apex); stack.Children.Add(links);
        stack.Children.Add(Para("X20CTL is not affiliated with, endorsed by or sponsored by EasySMX. EasySMX and its product names belong to their owners.", 26, "DS.TextSoft", 15));
        return new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    private static void Try(Action open) { try { open(); } catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException) { } }
    private static void Place(Grid grid, UIElement e, int row, int column) { Grid.SetRow(e, row); Grid.SetColumn(e, column); grid.Children.Add(e); }
    private static void Enter(FrameworkElement e)
    {
        e.Opacity = 0; var move = new TranslateTransform(0, 18); e.RenderTransform = move;
        e.BeginAnimation(OpacityProperty, DS.To(1, 260)); move.BeginAnimation(TranslateTransform.YProperty, DS.To(0, 320));
    }

    // ---- Themes: every theme as a card drawn in its own colours, type and logo; the choice is saved and starts with the next launch
    private readonly List<Button> themeCards = new();
    private string ChosenTheme => preferences?.Theme is { } id && ThemeCatalog.Find(id).Id == id ? id : ThemeCatalog.Current.Id;
    private UIElement BuildThemes()
    {
        var root = new Grid(); root.RowDefinitions.Add(new() { Height = GridLength.Auto }); root.RowDefinitions.Add(new());
        var status = Kit.Text("", 15, "DS.TextSoft");
        var back = Pill("", "Back to Tools"); back.Click += (_, _) => ShowThemes(false);
        var restart = Pill("", "Restart now"); restart.Margin = new(0, 0, 12, 0); restart.Click += (_, _) => RequestThemeRestart();
        var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Children = { restart, back } };
        DockPanel.SetDock(actions, Dock.Right);
        root.Children.Add(new DockPanel { Margin = new(0, 0, 0, 22), Children = { actions, new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { Kit.Text("Themes", 26, "DS.Text", FontWeights.SemiBold), status } } } });
        var grid = new System.Windows.Controls.Primitives.UniformGrid { Columns = 5, Rows = 2 }; Grid.SetRow(grid, 1); root.Children.Add(grid);
        void Show()
        {
            string chosen = ChosenTheme; var next = ThemeCatalog.Find(chosen);
            status.Text = next == ThemeCatalog.Current ? "Layouts, controls and the opening intro." : next.Name + " starts the next time X20CTL opens.";
            restart.Visibility = next == ThemeCatalog.Current || ReviewSandbox.Active ? Visibility.Collapsed : Visibility.Visible;
            foreach (var card in themeCards) if (card.Content is Border face && face.Child is Grid g && g.Children[^1] is Border tag)
            {
                string id = (string)card.Tag; bool now = id == ThemeCatalog.Current.Id, picked = id == chosen;
                face.BorderBrush = picked ? DS.Brush("DS.AccentHi") : new SolidColorBrush(ThemeCatalog.Tone(ThemeCatalog.R(Color.FromArgb(58, 120, 160, 230)), t => ThemeCatalog.Alpha(ThemeCatalog.Mix(t.Rule, t.Text, .25), 120)));
                face.BorderThickness = new(picked ? 2 : 1); face.Padding = new(picked ? 11 : 12);
                tag.Visibility = now || picked ? Visibility.Visible : Visibility.Collapsed; ((TextBlock)tag.Child).Text = now ? "In use" : "Next";
            }
        }
        foreach (var theme in ThemeCatalog.All)
        {
            var card = ThemeCard(theme); card.Margin = new(0, 0, 18, 18); card.IsEnabled = preferences != null; themeCards.Add(card); grid.Children.Add(card);
            card.Click += (_, _) =>
            {
                try { preferences?.SetTheme(theme.Id); Show(); }
                catch (Exception error) when (error is System.IO.IOException or InvalidOperationException or UnauthorizedAccessException) { status.Text = "Couldn't save this setting: " + error.Message; }
            };
        }
        Show(); return root;
    }
    /// <summary>A small picture of the theme: its night, a pool of its light, its logo, a panel and its accent, and its name in its own type.</summary>
    private static Button ThemeCard(AppTheme theme)
    {
        var corner = new CornerRadius(Math.Min(12, theme.Radius * .75));
        var swatch = new Grid { ClipToBounds = true };
        swatch.Children.Add(new Border { CornerRadius = corner, Background = new LinearGradientBrush(theme.Night, theme.Void, 90) });
        swatch.Children.Add(new Border { CornerRadius = corner, Background = new RadialGradientBrush(ThemeCatalog.Alpha(theme.PoolA, 150), ThemeCatalog.Alpha(theme.PoolA, 0)) { Center = new(.95, .1), GradientOrigin = new(.95, .1), RadiusX = .8, RadiusY = .9 } });
        swatch.Children.Add(new Border { CornerRadius = corner, Background = new RadialGradientBrush(ThemeCatalog.Alpha(theme.PoolB, 120), ThemeCatalog.Alpha(theme.PoolB, 0)) { Center = new(.05, .9), GradientOrigin = new(.05, .9), RadiusX = .7, RadiusY = .8 } });
        if (ThemeLogo(theme) is { } logo) swatch.Children.Add(new Image { Source = logo, Height = 58, Margin = new(14, 14, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top }.Also(i => RenderOptions.SetBitmapScalingMode(i, BitmapScalingMode.HighQuality)));
        var accentChip = new Border { Width = 50, Height = 22, CornerRadius = new(theme.Radius == 0 ? 0 : 11), Background = new SolidColorBrush(theme.Accent), Margin = new(10, 0, 0, 0),
            Child = new Border { Width = 24, Height = 4, CornerRadius = new(2), Background = new SolidColorBrush(theme.OnAccent), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
        DockPanel.SetDock(accentChip, Dock.Right);
        var lines = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = {
            new Border { Width = 70, Height = 6, CornerRadius = new(3), Background = new SolidColorBrush(theme.Text), HorizontalAlignment = HorizontalAlignment.Left },
            new Border { Width = 46, Height = 5, CornerRadius = new(2.5), Margin = new(0, 6, 0, 0), Background = new SolidColorBrush(theme.Muted), HorizontalAlignment = HorizontalAlignment.Left } } };
        swatch.Children.Add(new Border
        {
            CornerRadius = new(theme.Radius * .5), Margin = new(14, 0, 14, 14), Padding = new(12, 10, 12, 10), VerticalAlignment = VerticalAlignment.Bottom, BorderThickness = new(1),
            Background = new SolidColorBrush(ThemeCatalog.Alpha(ThemeCatalog.Mix(theme.Night, theme.Text, .07), 230)), BorderBrush = new SolidColorBrush(theme.Rule),
            Child = new DockPanel { LastChildFill = false, Children = { accentChip, lines } }
        });
        var name = new TextBlock { Text = theme.Name, FontFamily = new FontFamily(theme.Display), FontSize = 20, FontWeight = FontWeights.SemiBold, Foreground = DS.Brush("DS.Text"), Margin = new(4, 12, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis };
        var line = Kit.Text(theme.Line, 13.5, "DS.TextSoft"); line.Margin = new(4, 3, 0, 2); line.TextWrapping = TextWrapping.Wrap; line.TextTrimming = TextTrimming.None;
        var tag = new Border { CornerRadius = new(10), Padding = new(10, 3, 10, 4), Margin = new(0, 10, 10, 0), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Visibility = Visibility.Collapsed,
            Background = new SolidColorBrush(ThemeCatalog.R(Color.FromArgb(220, 8, 10, 16))), Child = Kit.Text("In use", 12.5, "DS.Text", FontWeights.SemiBold) };
        var content = new Grid(); content.RowDefinitions.Add(new()); content.RowDefinitions.Add(new() { Height = GridLength.Auto }); content.RowDefinitions.Add(new() { Height = GridLength.Auto });
        content.Children.Add(swatch); Grid.SetRow(name, 1); content.Children.Add(name); Grid.SetRow(line, 2); content.Children.Add(line); content.Children.Add(tag);
        var face = new Border
        {
            CornerRadius = ThemeCatalog.Round(18), BorderThickness = new(1), Padding = new(12), Child = content,
            Background = new LinearGradientBrush(ThemeCatalog.Tone(ThemeCatalog.R(Color.FromArgb(230, 17, 28, 58)), t => ThemeCatalog.Alpha(ThemeCatalog.Mix(t.Night, t.Text, .05), 235)), ThemeCatalog.Tone(ThemeCatalog.R(Color.FromArgb(225, 11, 19, 42)), t => ThemeCatalog.Alpha(t.Night, 230)), 90)
        };
        var lift = new TranslateTransform();
        var button = new Button { Content = face, Tag = theme.Id, Cursor = Cursors.Hand, FocusVisualStyle = null, RenderTransform = lift, Template = BareTemplate() };
        System.Windows.Automation.AutomationProperties.SetName(button, theme.Name);
        void Lift(bool on) { if (DS.Motion) lift.BeginAnimation(TranslateTransform.YProperty, DS.To(on ? -3 : 0, 150)); else lift.Y = on ? -3 : 0; }
        UiSound.SetCue(button, UiSound.Cue.Click); UiSound.Hover(button, UiSound.Cue.HoverTool);
        button.MouseEnter += (_, _) => Lift(true); button.MouseLeave += (_, _) => Lift(button.IsKeyboardFocused);
        button.GotKeyboardFocus += (_, _) => Lift(true); button.LostKeyboardFocus += (_, _) => Lift(button.IsMouseOver);
        return button;
    }
    private static ImageSource? ThemeLogo(AppTheme theme)
    {
        try
        {
            if (theme.Intro is { } intro && System.IO.File.Exists(intro.LogoPath))
            { var image = new System.Windows.Media.Imaging.BitmapImage(); image.BeginInit(); image.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad; image.DecodePixelHeight = 160; image.UriSource = new Uri(intro.LogoPath); image.EndInit(); image.Freeze(); return image; }
            return theme == ThemeCatalog.Default ? new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Assets/brand.png")) : null;
        }
        catch (Exception error) when (error is System.IO.IOException or NotSupportedException or UnauthorizedAccessException) { return null; }
    }

    private static Button Pill(string icon, string text)
    {
        var b = new Button { Style = DS.Style("ActionButton"), Height = 46, MinHeight = 0, Padding = new(22, 0, 24, 0), FontSize = 15.5,
            Content = new StackPanel { Orientation = Orientation.Horizontal, Children = { Kit.Icon(icon, 15, "DS.Text").Also(i => i.Margin = new(0, 0, 10, 0)), Kit.Text(text, 15.5, "DS.Text", FontWeights.SemiBold) } } };
        return b;
    }

    /// <summary>A flat tile in the Studio's own style: a quiet navy panel with a hairline border, an icon chip, a title and one
    /// line of description. Hover brightens the border and nudges the chevron; no glow, no per-tile colour.</summary>
    private static Button Card(string icon, string title, string detail, Color accent, bool hero = false)
    {
        var line = ThemeCatalog.Tone(ThemeCatalog.R(Color.FromArgb(58, 120, 160, 230)), t => ThemeCatalog.Alpha(ThemeCatalog.Mix(t.Rule, t.Text, .25), 120));
        var lineOn = ThemeCatalog.Tone(ThemeCatalog.R(Color.FromArgb(210, 90, 160, 255)), t => ThemeCatalog.Alpha(t.Accent, 220));
        accent = ThemeCatalog.Tone(accent, t => t.Accent);
        Color rest0 = ThemeCatalog.Tone(ThemeCatalog.R(Color.FromArgb(230, 17, 28, 58)), t => ThemeCatalog.Alpha(ThemeCatalog.Mix(t.Night, t.Text, .05), 235)), rest1 = ThemeCatalog.Tone(ThemeCatalog.R(Color.FromArgb(225, 11, 19, 42)), t => ThemeCatalog.Alpha(t.Night, 230));
        Color lit0 = ThemeCatalog.Tone(ThemeCatalog.R(Color.FromArgb(240, 22, 36, 74)), t => ThemeCatalog.Alpha(ThemeCatalog.Mix(t.Night, t.Text, .1), 240)), lit1 = ThemeCatalog.Tone(ThemeCatalog.R(Color.FromArgb(235, 14, 24, 52)), t => ThemeCatalog.Alpha(ThemeCatalog.Mix(t.Night, t.Text, .04), 235));
        var art = new Grid();
        if (hero)
        {   // the Controller Check card shows a computer running a scan (owner request 13 Oct 2026), not a controller
            var computer = new Scanner.PcScanArt();
            if (ThemeCatalog.Current.Id == "pulse") { var halo = Pulse.PulseKit.ControllerChamberHalo(showRipples: true, opacity: 0.55); halo.Width = 420; halo.HorizontalAlignment = HorizontalAlignment.Right; halo.VerticalAlignment = VerticalAlignment.Top; halo.Margin = new(0, -12, 12, 0); art.Children.Add(halo); }
            art.Children.Add(new Viewbox { Child = computer, Width = 470, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new(0, 6, 0, 0), IsHitTestVisible = false, Opacity = .95 });
        }
        double chipSize = hero ? 60 : 48;
        // the stories play on their own while the card is on screen (they stop when it is hidden or motion is reduced)
        var motionIcon = new ToolTileIcon(title) { Width = hero ? 42 : 36, Height = hero ? 42 : 36, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        motionIcon.SetActive(true);
        var chip = new Border { Width = chipSize, Height = chipSize, CornerRadius = new(hero ? 18 : 14), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = hero ? VerticalAlignment.Top : VerticalAlignment.Center,
            Background = new LinearGradientBrush(Color.FromArgb(175, accent.R, accent.G, accent.B), Color.FromArgb(70, accent.R, accent.G, accent.B), 90), BorderBrush = new SolidColorBrush(Color.FromArgb(200, accent.R, accent.G, accent.B)), BorderThickness = new(1),
            Child = motionIcon };
        art.Children.Add(chip);
        var words = new StackPanel { VerticalAlignment = hero ? VerticalAlignment.Bottom : VerticalAlignment.Center, Margin = new(hero ? 0 : 66, 0, hero ? 0 : 34, 0) };
        words.Children.Add(Kit.Text(title, hero ? 34 : 19, "DS.Text", FontWeights.SemiBold).Also(t => { t.TextWrapping = TextWrapping.Wrap; t.TextTrimming = TextTrimming.None; }));
        var d = Kit.Text(detail, hero ? 16 : 14, "DS.TextSoft"); d.TextWrapping = TextWrapping.Wrap; d.TextTrimming = TextTrimming.None; d.Margin = new(0, hero ? 8 : 4, 0, 0); words.Children.Add(d);
        if (hero) words.Children.Add(new WrapPanel { Margin = new(0, 20, 0, 0), Children = { Chip("1  Read scan"), Chip("2  Write scan"), Chip("3  Send online") } });
        art.Children.Add(words);
        var arrowMove = new TranslateTransform();
        var arrow = new TextBlock { Text = "", FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 15, Foreground = DS.Brush("DS.TextMuted"), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = hero ? VerticalAlignment.Bottom : VerticalAlignment.Center, RenderTransform = arrowMove };
        art.Children.Add(arrow);
        if (ThemeCatalog.Current.Id == "pulse")
        {
            var topBar = Pulse.PulseKit.PrismTopBar(hero ? 16 : 8);
            topBar.Margin = new(0, hero ? -32 : -18, 0, 0);
            art.Children.Add(topBar);
        }
        var face = new Border
        {
            CornerRadius = ThemeCatalog.Round(hero ? 22 : 18), BorderThickness = new(1), Padding = hero ? new(34, 32, 30, 30) : new(22, 18, 20, 18), ClipToBounds = true, Child = art,
            Background = new LinearGradientBrush(rest0, rest1, 90), BorderBrush = new SolidColorBrush(line)
        };
        var lift = new TranslateTransform();
        var button = new Button { Content = face, Cursor = Cursors.Hand, FocusVisualStyle = null, RenderTransform = lift, ToolTip = detail, Template = BareTemplate() };
        System.Windows.Automation.AutomationProperties.SetName(button, title);
        void Light(bool on)
        {
            face.BorderBrush = new SolidColorBrush(on ? lineOn : line);
            face.Background = new LinearGradientBrush(on ? lit0 : rest0, on ? lit1 : rest1, 90);
            arrow.Foreground = DS.Brush(on ? "DS.Text" : "DS.TextMuted");
            if (DS.Motion) { lift.BeginAnimation(TranslateTransform.YProperty, DS.To(on ? -2 : 0, 150)); arrowMove.BeginAnimation(TranslateTransform.XProperty, DS.To(on ? 4 : 0, 150)); }
            else { lift.Y = on ? -2 : 0; arrowMove.X = on ? 4 : 0; }
        }
        UiSound.SetCue(button, UiSound.Cue.Click); UiSound.Hover(button, UiSound.Cue.HoverTool);
        button.MouseEnter += (_, _) => Light(true); button.MouseLeave += (_, _) => Light(button.IsKeyboardFocused);
        button.GotKeyboardFocus += (_, _) => Light(true); button.LostKeyboardFocus += (_, _) => Light(button.IsMouseOver);
        return button;
    }
    private static Border Chip(string text) => new() { CornerRadius = new(14), Padding = new(12, 5, 12, 5), Margin = new(0, 0, 8, 8), Background = new SolidColorBrush(ThemeCatalog.R(Color.FromArgb(30, 255, 255, 255))), BorderBrush = new SolidColorBrush(ThemeCatalog.R(Color.FromArgb(50, 255, 255, 255))), BorderThickness = new(1), Child = Kit.Text(text, 13, "DS.Text", FontWeights.SemiBold) };
    private static ControlTemplate BareTemplate() { var t = new ControlTemplate(typeof(Button)) { VisualTree = new FrameworkElementFactory(typeof(ContentPresenter)) }; t.Seal(); return t; }
}
