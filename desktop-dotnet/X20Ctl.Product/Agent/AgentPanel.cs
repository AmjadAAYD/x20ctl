using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using X20Ctl.Zone.Agent;
using X20Ctl.Zone.PcMode;
namespace X20Ctl.Product.Agent;

/// <summary>
/// The agent (12 Oct 2026): a small window that opens from the round button over the Studio. A sentence
/// goes to the agent, its reply is checked against the agent's fixed rules, and only a valid one changes the
/// settings, straight away, with an Undo. The model's words are what it proposed; the line under them is the app saying
/// what really happened. The window can be dragged by its title and put away without losing the conversation.
/// It is drawn as a controller (owner, 13 Oct 2026: "we are talking to a controller, it must look like one"): the
/// conversation is its screen, its sticks are eyes that follow what it's doing, the player lights chase while it thinks,
/// and its own buttons work it: LB start over, RB the settings, A send, B away, X start over, Y HELP, the D-pad scrolls and
/// flips through ideas. A change it applies makes the real controller rumble.
/// </summary>
public sealed class AgentPanel
{
    // the agent that answers: a phrasebook that runs on this PC, with nothing to install
    static readonly OfflineAgent offlineAgent = new();
    // the docs (M5), searched by words
    static readonly Lazy<IReadOnlyList<DocChunk>> docs = new(() => AgentDocs.Load(Path.Combine(AppContext.BaseDirectory, "Help")));
    static readonly Lazy<KeywordSearch> keywords = new(() => new KeywordSearch(docs.Value));

    public Border Body { get; }
    public TextBox Input { get; }
    /// <summary>The ideas' own search box (the Ideas view of the screen).</summary>
    public TextBox Search { get; }
    /// <summary>Minimise or close was chosen: the host puts the window away (the conversation stays).</summary>
    public event Action? CloseRequested;
    /// <summary>"Change controller" was asked: the host goes to the Controller Zone.</summary>
    public event Action? ChangeControllerRequested;
    readonly string padName;
    /// <summary>Plays the walk-through of a change (before, after) on the host's pages, and returns when it is over.</summary>
    public Func<PcModeProfile, PcModeProfile, DriftReport?, Task>? Walkthrough { get; set; }
    /// <summary>The agent started or stopped thinking.</summary>
    public event Action<bool>? Thinking;
    /// <summary>The agent takes the owner to a page (Diagnostics, Mouse, Curves…); the host goes there.</summary>
    public event Action<string>? PageRequested;
    /// <summary>Switches mouse mode; returns why not, or null when it worked.</summary>
    public Func<bool, string?>? MouseSwitch { get; set; }
    readonly string modelId, greeting;
    readonly PcModeLink? link;
    readonly AgentSession session;
    readonly StackPanel transcript = new() { Margin = new(0, 0, 10, 0) };
    readonly ScrollViewer scroller;
    readonly Button send = null!;
    readonly Action<Button> register;
    readonly StackPanel ideasList = new();
    // the screen's own pieces (owner's design, 19 Oct 2026): the chat or the ideas, picked from the rail or a topic pill
    enum View { Chat, Ideas }
    readonly DockPanel ideasView;
    readonly Button sendRound;
    readonly TextBlock padChip = Kit.Text("", 13, "DS.TextSoft", FontWeights.SemiBold);
    readonly List<(string Query, Border Look, TextBlock Text, TextBlock Icon)> topics = [];
    readonly Dictionary<View, Border> rail = [];
    // the screen's colours: charcoal, a hairline, and one amber accent for what the agent proposes
    static readonly Color Amber = Color.FromRgb(245, 184, 61), Card = Color.FromRgb(21, 25, 31), Line = Color.FromRgb(42, 47, 55),
        ScreenTop = Color.FromRgb(16, 19, 24), ScreenBottom = Color.FromRgb(10, 12, 16);
    Button? undoLatest;
    // the agent speaks up once the owner stops typing a search (owner request, 13 Oct 2026)
    readonly System.Windows.Threading.DispatcherTimer settle = new() { Interval = TimeSpan.FromSeconds(1.2) };
    string announced = "";
    int lightShow; bool lightsOn;
    readonly TextBlock ideasFor = Kit.Text("", 12.5, "DS.TextMuted").Also(t => { t.TextWrapping = TextWrapping.Wrap; t.TextTrimming = TextTrimming.None; t.Margin = new(0, 2, 0, 0); });
    readonly TextBlock status = Kit.Text("", 13, "DS.TextSoft");
    readonly Border statusDot = new() { Width = 8, Height = 8, CornerRadius = new(4), Margin = new(0, 0, 7, 0), VerticalAlignment = VerticalAlignment.Center };
    readonly StackPanel settings = new();
    readonly Border settingsBox;
    readonly TextBlock settingsToggle = Kit.Text("", 13.5, "DS.TextSoft");
    readonly TextBlock target = Kit.Text("", 13, "DS.TextMuted").Also(t => { t.TextWrapping = TextWrapping.Wrap; t.TextTrimming = TextTrimming.None; });
    readonly TranslateTransform offset = new();
    readonly ScaleTransform pop = new(1, 1);
    PcModeProfile preview = new(); // the settings when PC mode isn't reachable from this window
    bool busy;
    readonly int player;
    readonly Border[] leds = new Border[4];
    readonly List<(TranslateTransform Look, ScaleTransform Lid, int Inward)> eyes = [];
    readonly TranslateTransform shake = new();
    // a silly question: the eyes squeeze into > < for a moment (owner request, 13 Oct 2026)
    readonly List<(UIElement Pupil, UIElement Squint)> squints = [];
    int laughing;
    readonly System.Windows.Threading.DispatcherTimer blink = new() { Interval = TimeSpan.FromSeconds(4) };

    /// <summary>The controller is drawn at this size and scaled as a whole to the room it's given: wide, across the Studio
    /// (owner's sketch, 13 Oct); its screen follows the owner's design of 19 Oct: a rail, a header, topic pills and the chat.</summary>
    public const double DesignWidth = 1740, DesignHeight = 800;
    // the art's own places: the display, the stick caps, the D-pad's centre
    const double ScreenZoom = 1.2, ScreenX = 382, ScreenY = 71, LeftX = 238, RightX = 1510, EyeY = 196, PadX = 270, PadY = 446;

    public AgentPanel(string modelId, string controllerName, PcModeLink? link, Action<Button> register, int player = 0, Func<Action<string, int>, IDriftMeter>? meter = null, bool reducedMotion = false)
    {
        this.reducedMotion = reducedMotion; this.modelId = modelId; this.link = link; this.player = player; this.register = register;
        string name = controllerName.Replace("EasySMX ", ""); padName = name;
        // drift is read from the real controller; a review harness can hand in its own reading instead
        session = new AgentSession(offlineAgent, modelId, name) { Docs = keywords.Value, Meter = (meter ?? (progress => new XInputDriftMeter(player, progress)))(Measuring), ControllerFacts = Facts(modelId) };
        greeting = AgentCharacter.Greeting(name);

        // ---- the screen (owner's design, 19 Oct 2026): a rail of views on the left; a header with the controller and the
        // agent's state; topic pills; the conversation or the ideas; and the chat bar along the bottom ----
        var privacy = Wrap("What you type and what I answer stay on this PC.", 12.5, "DS.TextMuted", 16);
        var backToChat = Small("← Back to chat");
        backToChat.Click += (_, _) => ShowSettingsPage(false); register(backToChat);
        var settingsHead = new DockPanel { Margin = new(0, 0, 0, 12) };
        DockPanel.SetDock(backToChat, Dock.Right); settingsHead.Children.Add(backToChat);
        settingsHead.Children.Add(Kit.Text("Agent settings", 22, "DS.Text", FontWeights.SemiBold));
        var settingsPage = new DockPanel(); DockPanel.SetDock(settingsHead, Dock.Top); settingsPage.Children.Add(settingsHead);
        settingsPage.Children.Add(new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = new StackPanel { Children = { target.Also(t => t.Margin = new(0, 0, 0, 12)), settings, privacy } } });
        settingsBox = new Border { Visibility = Visibility.Collapsed, Padding = new(16), CornerRadius = new(12), Background = new SolidColorBrush(ScreenTop), Child = settingsPage };
        settingsToggle.Cursor = Cursors.Hand; settingsToggle.ToolTip = "Open agent settings";
        settingsToggle.MouseLeftButtonUp += (_, _) => ShowSettingsPage(true);

        // header: the mark and the name, then the controller and the agent's own state as two chips
        var mark = new System.Windows.Controls.Image { Width = 30, Height = 30, Margin = new(0, 0, 10, 0), Source = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/X20Ctl;component/Assets/brand.png")) };
        padChip.Text = name + " · Player " + (Math.Clamp(player, 0, 3) + 1);
        var heading = new DockPanel { Margin = new(0, 0, 0, 10) };
        DockPanel.SetDock(settingsToggle, Dock.Right); settingsToggle.VerticalAlignment = VerticalAlignment.Center; heading.Children.Add(settingsToggle);
        statusDot.Margin = new(0, 0, 8, 0);
        heading.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children =
            {
                mark, Kit.Text("Agent XCTL", 24, "DS.Text", FontWeights.Bold).Also(t => t.VerticalAlignment = VerticalAlignment.Center),
                Rule(), Chip(new Border { Width = 9, Height = 9, CornerRadius = new(4.5), BorderThickness = new(2), BorderBrush = new SolidColorBrush(Amber), Margin = new(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center }, padChip),
                Rule(), Chip(statusDot, status),
            },
        });

        // the conversation
        scroller = new ScrollViewer { Content = transcript, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        transcript.LayoutTransform = new ScaleTransform(.95, .95);

        // the ideas: a search, a line saying what's listed, and the list
        Search = new TextBox { FontSize = 14, MinHeight = 38, Padding = new(24, 8, 10, 7), VerticalContentAlignment = VerticalAlignment.Center, ToolTip = "Search the ideas: sticks, triggers, turbo…" };
        Search.TextChanged += (_, _) =>
        {
            ShowIdeas(Search.Text); settle.Stop(); Pills();
            bool typing = Search.Text.Trim().Length > 0;
            if (!busy) { Look(typing ? Gaze.Ideas : Gaze.Idle); Searching(typing); }
            if (typing) { if (ideasView is { Visibility: not Visibility.Visible }) ShowView(View.Ideas); settle.Start(); }
        };
        Search.KeyDown += (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; settle.Stop(); Announce(); } };
        settle.Tick += (_, _) => { settle.Stop(); Announce(); };
        var searchBar = new Grid { Children = { Search, Kit.Icon("\uE721", 13, "DS.TextMuted").Also(i => { i.Margin = new(10, 0, 0, 0); i.HorizontalAlignment = HorizontalAlignment.Left; i.IsHitTestVisible = false; }) } };
        var ideasHead = new StackPanel { Margin = new(0, 0, 0, 10), Children = { searchBar, ideasFor.Also(t => t.Margin = new(2, 6, 0, 0)) } };
        ideasView = new DockPanel { Visibility = Visibility.Collapsed }; DockPanel.SetDock(ideasHead, Dock.Top); ideasView.Children.Add(ideasHead);
        ideasView.Children.Add(new ScrollViewer { Content = ideasList, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new(0, 0, 8, 0) });

        // topic pills: each narrows the ideas to it
        var pills = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 0, 0, 10) };
        foreach (var (label, glyph, query) in new[] { ("Aim & sticks", "\uE7FC", "sticks"), ("Triggers", "\uE9E9", "triggers"), ("Buttons", "\uE765", "buttons"), ("PC mode", "\uE7F4", "PC mode") })
        {
            var text = Kit.Text(label, 13.5, "DS.TextSoft", FontWeights.SemiBold); var icon = Kit.Icon(glyph, 14, "DS.TextSoft").Also(i => i.Margin = new(0, 0, 8, 0));
            var look = new Border { CornerRadius = new(17), Height = 34, Padding = new(14, 0, 16, 0), Margin = new(0, 0, 8, 0), BorderThickness = new(1), Child = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Children = { icon, text } } };
            var pill = PadKey(look, "Ideas for " + label.ToLowerInvariant(), () => { Search.Text = query; ShowView(View.Ideas); settle.Stop(); Announce(); });
            topics.Add((query, look, text, icon)); register(pill); pills.Children.Add(pill);
        }

        // the chat bar: a field with a hint, and a round send button
        Input = new TextBox { AcceptsReturn = false, TextWrapping = TextWrapping.Wrap, MinHeight = 42, MaxHeight = 84, MaxLength = 600, FontSize = 14.5, Padding = new(4, 9, 12, 8), VerticalContentAlignment = VerticalAlignment.Center, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, ToolTip = "Talk to the agent: Enter or A to send", Background = Brushes.Transparent, BorderThickness = new(0), FocusVisualStyle = null,
            Foreground = DS.Brush("DS.Text"), CaretBrush = DS.Brush("DS.Text"), Template = Bare() };
        Input.KeyDown += (_, e) => { if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None) { e.Handled = true; Send(); } };
        var hint = Kit.Text("Tell me what feels off…", 14.5, "DS.TextMuted").Also(h => { h.IsHitTestVisible = false; h.Margin = new(6, 0, 0, 0); h.VerticalAlignment = VerticalAlignment.Center; });
        Input.TextChanged += (_, _) => { hint.Visibility = Input.Text.Length > 0 ? Visibility.Collapsed : Visibility.Visible; if (!busy) { Look(Input.Text.Length > 0 ? Gaze.Typing : Gaze.Idle); Typing(Input.Text.Length > 0); } };
        var sendGlyph = Kit.Icon("\uE724", 16, "DS.Text").Also(i => { i.Foreground = new SolidColorBrush(Color.FromRgb(24, 20, 8)); i.HorizontalAlignment = HorizontalAlignment.Center; });
        sendRound = PadKey(new Border { Width = 40, Height = 40, CornerRadius = new(20), Background = new SolidColorBrush(Amber), BorderThickness = new(1), BorderBrush = new SolidColorBrush(Amber), Child = sendGlyph }, "Send", Send);
        register(sendRound);
        var field = new Grid { ColumnDefinitions = { new() { Width = GridLength.Auto }, new() { Width = GridLength.Auto }, new(), new() { Width = GridLength.Auto } } };
        field.Children.Add(Kit.Icon("\uE8BD", 16, "DS.TextMuted").Also(i => i.Margin = new(4, 0, 12, 0)));
        var sep = new Border { Width = 1, Height = 22, Margin = new(0, 0, 12, 0), Background = new SolidColorBrush(Line) }; Grid.SetColumn(sep, 1); field.Children.Add(sep);
        var typed = new Grid { Children = { hint, Input } }; Grid.SetColumn(typed, 2); field.Children.Add(typed);
        Grid.SetColumn(sendRound, 3); field.Children.Add(sendRound);
        var bar = new Border { Margin = new(0, 10, 0, 0), CornerRadius = new(26), Padding = new(12, 4, 6, 4), BorderThickness = new(1), BorderBrush = new SolidColorBrush(Line), Background = new SolidColorBrush(Card), Child = field };

        // the rail: the conversation, the ideas, another controller, the settings now, PC mode; the agent's settings last
        var railItems = new StackPanel();
        Border RailItem(string glyph, string tip, Action act, Panel into)
        {
            var stripe = new Border { Width = 3, Height = 22, CornerRadius = new(1.5), HorizontalAlignment = HorizontalAlignment.Left, Background = Brushes.Transparent };
            var look = new Border { Width = 46, Height = 46, CornerRadius = new(12), Margin = new(0, 2, 0, 2), BorderThickness = new(1), BorderBrush = Brushes.Transparent, Background = Brushes.Transparent,
                Child = new Grid { Children = { stripe, Kit.Icon(glyph, 18, "DS.TextSoft").Also(i => i.HorizontalAlignment = HorizontalAlignment.Center) } } };
            var item = PadKey(look, tip, act); register(item); into.Children.Add(item);
            return look;
        }
        rail[View.Chat] = RailItem("\uE8BD", "Conversation", () => ShowView(View.Chat), railItems);
        rail[View.Ideas] = RailItem("\uE82F", "Ideas", () => { ShowView(View.Ideas); Search.Focus(); }, railItems);
        RailItem("\uE7FC", "Another controller", () => ChangeControllerRequested?.Invoke(), railItems);
        RailItem("\uE9E9", "Settings now", () => ShowSettingsPage(!SettingsOpen), railItems);
        RailItem("\uE7F4", "PC mode", () => PageRequested?.Invoke("PcMode"), railItems);
        var railFoot = new StackPanel();
        railFoot.Children.Add(new Border { Height = 1, Margin = new(8, 6, 8, 8), Background = new SolidColorBrush(Line) });
        RailItem("\uE713", "Agent settings", () => ShowSettingsPage(!SettingsOpen), railFoot);
        var railDock = new DockPanel(); DockPanel.SetDock(railFoot, Dock.Bottom); railDock.Children.Add(railFoot); railDock.Children.Add(railItems);
        var railBox = new Border { Width = 58, Padding = new(5, 8, 5, 6), Margin = new(0, 0, 14, 0), CornerRadius = new(16), BorderThickness = new(1), BorderBrush = new SolidColorBrush(Line), Background = new SolidColorBrush(Card), Child = railDock };

        var views = new Grid { Children = { scroller, ideasView } };
        var content = new DockPanel();
        DockPanel.SetDock(heading, Dock.Top); content.Children.Add(heading);
        DockPanel.SetDock(pills, Dock.Top); content.Children.Add(pills);
        DockPanel.SetDock(bar, Dock.Bottom); content.Children.Add(bar);
        content.Children.Add(views);
        var layout = new DockPanel(); DockPanel.SetDock(railBox, Dock.Left); layout.Children.Add(railBox); layout.Children.Add(new Grid { Children = { content, settingsBox } });
        var screen = new Border
        {
            Width = 984, Height = 631,
            Child = new Viewbox { Stretch = Stretch.Fill, Child = new Border { Width = 984 / ScreenZoom, Height = 631 / ScreenZoom, Padding = new(18, 15, 20, 13), Child = layout } },
            Background = new LinearGradientBrush(ScreenTop, ScreenBottom, 90),
        };

        // ---- the controller around it: the owner's concept art (19 Oct 2026, "literally, same design"), cut from its
        // backdrop with the screen emptied; the live screen sits in its display, and each drawn control is a real button ----
        var pad = new Canvas { Width = DesignWidth, Height = DesignHeight, RenderTransform = shake };
        var art = new System.Windows.Controls.Image { Width = DesignWidth, Height = DesignHeight, Stretch = Stretch.Fill, Source = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/X20Ctl;component/Agent/agent-shell.png")),
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 44, ShadowDepth = 12, Opacity = .6 } };
        pad.Children.Add(art); Drag(art);
        // the player lights, over the drawn ones: this player's lit in the agent's state; they chase while it thinks
        for (int i = 0; i < 4; i++)
        {
            leds[i] = new Border { Width = 28, Height = 10, CornerRadius = new(5), IsHitTestVisible = false };
            Canvas.SetLeft(leds[i], 795 + i * 43); Canvas.SetTop(leds[i], 48); pad.Children.Add(leds[i]);
        }
        // the display is an octagon: the screen is cut to it
        screen.Clip = Geometry.Parse("M 37,2 L 938,2 L 984,49 L 984,558 L 915,631 L 66,631 L 0,563 L 0,54 Z");
        Canvas.SetLeft(screen, ScreenX); Canvas.SetTop(screen, ScreenY); pad.Children.Add(screen);

        // the bumpers, as drawn: LB the ideas, RB the settings
        var ideasBumper = Hit(280, 10, 200, 50, 18, "LB · Ideas", () => { ShowView(View.Ideas); Search.Focus(); Look(Gaze.Ideas); });
        var settingsButton = Hit(1265, 10, 205, 50, 18, "RB · Settings", () => ShowSettingsPage(!SettingsOpen));
        pad.Children.Add(ideasBumper); pad.Children.Add(settingsButton);

        // the sticks are its eyes
        // each eye at the centre of its stick's dish, measured on the art
        Eye(pad, LeftX, EyeY); Eye(pad, RightX, EyeY - 2);

        // left: the D-pad scrolls the conversation, and steps through the ideas
        foreach (var (x, y, tip, act) in new (double, double, string, Action)[]
        {
            (0, -1, "Scroll up", () => scroller.ScrollToVerticalOffset(scroller.VerticalOffset - 140)),
            (0, 1, "Scroll down", () => scroller.ScrollToVerticalOffset(scroller.VerticalOffset + 140)),
            (-1, 0, "Previous idea", () => Idea(-1)),
            (1, 0, "Next idea", () => Idea(1)),
        })
            pad.Children.Add(Hit(PadX - 24 + x * 52, PadY - 24 + y * 52, 48, 48, 8, tip, act));

        // right: the face buttons, drawn pulled in so B clears the grip (owner, 19 Oct 2026). A sends, B puts it away, X starts
        // over, Y searches the ideas
        foreach (var (letter, x, y, tip, act) in new (string, double, double, string, Action)[]
        {
            ("Y", 1490, 359, "Search the ideas", () => { ShowView(View.Ideas); Search.Focus(); Search.SelectAll(); Look(Gaze.Ideas); }),
            ("X", 1438, 417, "New conversation", Restart),
            ("B", 1540, 424, "Put away", () => { if (SettingsOpen) ShowSettingsPage(false); else CloseRequested?.Invoke(); }),
            ("A", 1490, 481, "Send", Send),
        })
        {
            var cap = Hit(x - 35, y - 35, 70, 70, 35, tip, act);
            pad.Children.Add(cap);
            if (letter == "A") send = cap;
        }
        send ??= new Button();
        ShowIdeas(""); ShowView(View.Chat);

        Body = new Border
        {
            Visibility = Visibility.Collapsed,
            Background = Brushes.Transparent, Child = new Viewbox { Stretch = Stretch.Uniform, Child = pad },
            RenderTransform = new TransformGroup { Children = { pop, offset } }, RenderTransformOrigin = new(1, 1),
        };
        blink.Tick += (_, _) => Blink();
        Say(greeting, null);
        ShowSettings(new HashSet<string>());
        Body.Unloaded += (_, _) => StopCharacterMotion();
        ShowState(); Look(Gaze.Idle);
    }

    /// <summary>A control on the controller: its own drawing, a lit edge when hovered or focused.</summary>
    static Button PadKey(Border look, string tip, Action? act)
    {
        var button = new Button { Content = look, ToolTip = tip, Cursor = Cursors.Hand, FocusVisualStyle = null, RenderTransformOrigin = new(.5, .5), RenderTransform = new ScaleTransform(1, 1),
            Template = new ControlTemplate(typeof(Button)) { VisualTree = new FrameworkElementFactory(typeof(ContentPresenter)) } };
        System.Windows.Automation.AutomationProperties.SetName(button, tip);
        var rest = look.BorderBrush;
        void Lit(bool on) => look.BorderBrush = on ? DS.Brush("DS.AccentHi") : rest;
        button.MouseEnter += (_, _) => Lit(true); button.MouseLeave += (_, _) => Lit(button.IsKeyboardFocused);
        button.GotKeyboardFocus += (_, _) => Lit(true); button.LostKeyboardFocus += (_, _) => Lit(button.IsMouseOver);
        if (act != null) button.Click += (_, _) => act();
        return button;
    }

    void Idea(int step)
    {
        var listed = ideasList.Children.OfType<Button>().ToList();
        if (listed.Count == 0) return;
        int at = listed.FindIndex(b => b.IsKeyboardFocusWithin);
        var next = listed[at < 0 ? (step > 0 ? 0 : listed.Count - 1) : Math.Clamp(at + step, 0, listed.Count - 1)];
        next.Focus(); next.BringIntoView(); Look(Gaze.Ideas);
    }

    /// <summary>The ideas half: empty until a word is searched, then what matches it; HELP shows everything.</summary>
    void ShowIdeas(string query, bool everything = false)
    {
        query = query.Trim();
        IReadOnlyList<AgentIdea> found = query.Length == 0 && !everything ? [] : AgentIdeas.Search(query, everything ? 30 : 10);
        ideasFor.Text = everything ? "Everything you can ask." : query.Length == 0 ? "Type a word: sticks, triggers, turbo, buttons, paddles or PC mode." : found.Count == 0 ? $"Nothing for “{query}”. Try sticks, triggers, turbo, buttons, paddles or PC mode." : $"For “{query}”";
        ideasList.Children.Clear();
        foreach (var idea in found)
        {
            var card = PadKey(new Border
            {
                CornerRadius = new(12), Padding = new(12, 8, 12, 9), Margin = new(0, 0, 0, 6), BorderThickness = new(1),
                Background = new SolidColorBrush(Card), BorderBrush = new SolidColorBrush(Line),
                Child = new StackPanel { Children = { Wrap(idea.Have, 12.5, "DS.TextMuted"), Wrap("“" + idea.Say + "”", 14, "DS.Text", 3) } },
            }, "Say it", null);
            string say = idea.Say;
            card.Click += (_, _) => { if (!busy) { ShowView(View.Chat); Input.Text = say; Send(); } };
            register(card); ideasList.Children.Add(card);
        }
    }

    /// <summary>The screen shows the conversation or the ideas; the rail and the pills say which.</summary>
    void ShowView(View view)
    {
        scroller.Visibility = view == View.Chat ? Visibility.Visible : Visibility.Collapsed;
        ideasView.Visibility = view == View.Ideas ? Visibility.Visible : Visibility.Collapsed;
        foreach (var (v, look) in rail)
        {
            bool on = v == view;
            look.Background = on ? new SolidColorBrush(Color.FromRgb(34, 39, 47)) : Brushes.Transparent;
            if (look.Child is Grid { Children: [Border stripe, TextBlock icon] }) { stripe.Background = on ? new SolidColorBrush(Amber) : Brushes.Transparent; icon.Foreground = on ? DS.Brush("DS.Text") : DS.Brush("DS.TextSoft"); }
        }
        if (view == View.Chat) scroller.ScrollToEnd();
        Pills();
    }

    /// <summary>The pill whose topic is listed is lit amber.</summary>
    void Pills()
    {
        string q = ideasView.Visibility == Visibility.Visible ? Search.Text.Trim() : "";
        foreach (var (query, look, text, icon) in topics)
        {
            bool on = string.Equals(q, query, StringComparison.OrdinalIgnoreCase);
            look.Background = new SolidColorBrush(on ? Amber : Card);
            look.BorderBrush = new SolidColorBrush(on ? Amber : Line);
            var ink = on ? new SolidColorBrush(Color.FromRgb(24, 20, 8)) : DS.Brush("DS.TextSoft");
            text.Foreground = ink; icon.Foreground = ink;
        }
    }

    /// <summary>A text box drawn as its text alone: the chat bar around it is the box.</summary>
    static ControlTemplate Bare()
    {
        var host = new FrameworkElementFactory(typeof(ScrollViewer)) { Name = "PART_ContentHost" };
        host.SetValue(Control.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
        host.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        return new ControlTemplate(typeof(TextBox)) { VisualTree = host };
    }

    static Border Rule() => new() { Width = 1, Height = 22, Margin = new(14, 0, 14, 0), VerticalAlignment = VerticalAlignment.Center, Background = new SolidColorBrush(Line) };
    static Border Chip(FrameworkElement dot, TextBlock text) => new()
    {
        CornerRadius = new(14), Height = 28, Padding = new(10, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center, BorderThickness = new(1), BorderBrush = new SolidColorBrush(Line), Background = new SolidColorBrush(Card),
        Child = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Children = { dot, text.Also(t => t.VerticalAlignment = VerticalAlignment.Center) } },
    };

    // ---- the eyes: where it looks says what it's doing ----
    enum Gaze { Idle, Typing, Thinking, Down, Ideas }
    void Eye(Canvas pad, double cx, double cy)
    {
        var look = new TranslateTransform(); var lid = new ScaleTransform(1, 1);
        var pupil = new System.Windows.Shapes.Rectangle { Width = 13, Height = 24, RadiusX = 6.5, RadiusY = 6.5, Fill = new SolidColorBrush(AgentMark.Ice), RenderTransformOrigin = new(.5, .5), RenderTransform = lid,
            Effect = new DropShadowEffect { Color = AgentMark.Ice, BlurRadius = 18, ShadowDepth = 0, Opacity = 1 } };
        var cap = new Grid { Width = 74, Height = 74, IsHitTestVisible = false, RenderTransform = look, Children = { pupil, Squint(cx < DesignWidth / 2) } };
        Canvas.SetLeft(cap, cx - 37); Canvas.SetTop(cap, cy - 37); pad.Children.Add(cap);
        eyes.Add((look, lid, cx < DesignWidth / 2 ? 1 : -1));
        squints.Add((pupil, cap.Children[^1]));
    }

    /// <summary>A drawn control made real: an invisible button over it, lit round its edge when hovered or focused.</summary>
    Button Hit(double x, double y, double w, double h, double radius, string tip, Action act)
    {
        var button = PadKey(new Border { Width = w, Height = h, CornerRadius = new(radius), Background = Brushes.Transparent, BorderThickness = new(2), BorderBrush = Brushes.Transparent }, tip, act);
        Canvas.SetLeft(button, x); Canvas.SetTop(button, y); register(button);
        return button;
    }

    /// <summary>The left eye's ">" or the right eye's "<": lit like the pupil, hidden until it laughs.</summary>
    static System.Windows.Shapes.Path Squint(bool left) => new()
    {
        Data = Geometry.Parse(left ? "M 0,0 L 24,15 L 0,30" : "M 24,0 L 0,15 L 24,30"), Width = 24, Height = 30,
        Stroke = new SolidColorBrush(AgentMark.Ice), StrokeThickness = 6, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round,
        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Visibility = Visibility.Collapsed,
        Effect = new DropShadowEffect { Color = AgentMark.Ice, BlurRadius = 18, ShadowDepth = 0, Opacity = 1 },
    };

    /// <summary>"> <": it laughs at a silly question, then looks normal again.</summary>
    async void Laugh()
    {
        int me = ++laughing;
        foreach (var (pupil, squint) in squints) { pupil.Visibility = Visibility.Hidden; squint.Visibility = Visibility.Visible; if (Motion) Fx.Appear((FrameworkElement)squint, .6, 220); }
        if (Motion)
        {
            var giggle = new System.Windows.Media.Animation.DoubleAnimationUsingKeyFrames();
            double t = 0; foreach (double y in new[] { -3.0, 2, -3, 2, -2, 0 }) giggle.KeyFrames.Add(new System.Windows.Media.Animation.LinearDoubleKeyFrame(y, TimeSpan.FromMilliseconds(t += 90)));
            shake.BeginAnimation(TranslateTransform.YProperty, giggle);
        }
        await Task.Delay(2400);
        if (me != laughing) return;
        foreach (var (pupil, squint) in squints) { squint.Visibility = Visibility.Collapsed; pupil.Visibility = Visibility.Visible; }
    }

    // ---- the eyes' colour (owner request, 14 Oct 2026): blue while it thinks, green when it answered, purple while the
    // owner types, red when it found nothing or the answer was bad; the usual ice otherwise ----
    Color eyeColor = AgentMark.Ice;
    void EyeColor(Color color)
    {
        if (color == eyeColor) return; eyeColor = color;
        foreach (var (pupil, squint) in squints)
        {
            if (pupil is System.Windows.Shapes.Shape p) { p.Fill = new SolidColorBrush(color); if (p.Effect is DropShadowEffect g) g.Color = color; }
            if (squint is System.Windows.Shapes.Shape s) { s.Stroke = new SolidColorBrush(color); if (s.Effect is DropShadowEffect g) g.Color = color; }
        }
    }
    bool typingLit;
    void Typing(bool on)
    {
        if (on == typingLit) return; typingLit = on;
        if (on) { lightShow++; lightsOn = true; EyeColor(Violet); foreach (var led in leds) { led.BeginAnimation(UIElement.OpacityProperty, null); led.Opacity = 1; led.Background = new SolidColorBrush(Violet); } }
        else { EyeColor(AgentMark.Ice); Steady(); }
    }
    /// <summary>Thinking: the sticks turn slowly round their edge.</summary>
    void Circling(bool on)
    {
        foreach (var (look, _, _) in eyes)
        {
            if (!on || !Motion) { look.BeginAnimation(TranslateTransform.XProperty, null); look.BeginAnimation(TranslateTransform.YProperty, null); continue; }
            const double r = 9; var quarter = TimeSpan.FromSeconds(.7);
            // x = sin, y = -cos: each quarter eases out to a peak or in to the middle, so the path is a circle
            var outOf = new System.Windows.Media.Animation.KeySpline(.39, .575, .565, 1); var into = new System.Windows.Media.Animation.KeySpline(.47, 0, .745, .715);
            System.Windows.Media.Animation.DoubleAnimationUsingKeyFrames Round(double[] at, System.Windows.Media.Animation.KeySpline[] ease)
            {
                var a = new System.Windows.Media.Animation.DoubleAnimationUsingKeyFrames { RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever };
                a.KeyFrames.Add(new System.Windows.Media.Animation.DiscreteDoubleKeyFrame(at[0], TimeSpan.Zero));
                for (int i = 1; i < at.Length; i++) a.KeyFrames.Add(new System.Windows.Media.Animation.SplineDoubleKeyFrame(at[i], quarter * i, ease[i - 1]));
                return a;
            }
            look.BeginAnimation(TranslateTransform.XProperty, Round([0, r, 0, -r, 0], [outOf, into, outOf, into]));
            look.BeginAnimation(TranslateTransform.YProperty, Round([-r, 0, r, 0, -r], [into, outOf, into, outOf]));
        }
    }
    /// <summary>"Wiggling my sticks": they really wiggle, side to side and a little up and down, then settle.</summary>
    void Wiggle()
    {
        if (!Motion) return;
        foreach (var (look, _, inward) in eyes)
        {
            var x = new System.Windows.Media.Animation.DoubleAnimationUsingKeyFrames(); var y = new System.Windows.Media.Animation.DoubleAnimationUsingKeyFrames();
            double t = 0;
            foreach (var (dx, dy) in new[] { (-11.0, -4.0), (11, 3), (-10, 4), (10, -4), (-8, 3), (8, -3), (-5, 2), (5, -1), (0, 0) })
            {
                t += 85;
                x.KeyFrames.Add(new System.Windows.Media.Animation.EasingDoubleKeyFrame(dx * inward, TimeSpan.FromMilliseconds(t), new System.Windows.Media.Animation.SineEase()));
                y.KeyFrames.Add(new System.Windows.Media.Animation.EasingDoubleKeyFrame(dy, TimeSpan.FromMilliseconds(t), new System.Windows.Media.Animation.SineEase()));
            }
            look.BeginAnimation(TranslateTransform.XProperty, x); look.BeginAnimation(TranslateTransform.YProperty, y);
        }
    }
    static bool Wiggles(string? speech) => speech != null && System.Text.RegularExpressions.Regex.IsMatch(speech, @"\b(w(r)?iggl\w*)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    /// <summary>What this controller is, for the model to choose values that fit it.</summary>
    static string Facts(string modelId)
    {
        var p = ModelProfiles.For(modelId);
        return $"Sticks: {p.Sticks}. Triggers: {p.Triggers}. Rumble motors: {p.Motors}. Polling: {p.Polling}. Motion: {p.Motion}." + (p.Paddles.Length > 0 ? $" Back paddles: {string.Join(", ", p.Paddles)}." : "")
            + (p.Sticks.Contains("Hall", StringComparison.OrdinalIgnoreCase) || p.Sticks.Contains("TMR", StringComparison.OrdinalIgnoreCase) ? " Magnetic sticks barely drift: dead zones can stay small (2-5)." : " Potentiometer sticks can drift: keep the dead zone at or above the measured drift.");
    }

    void Look(Gaze gaze)
    {
        foreach (var (look, _, inward) in eyes)
        {
            var (x, y) = gaze switch { Gaze.Typing => (5.0 * inward, 9.0), Gaze.Thinking => (0.0, -10.0), Gaze.Down => (0.0, 10.0), Gaze.Ideas => (-9.0, 3.0), _ => (6.0 * inward, 0.0) };
            if (!Motion) { look.X = x; look.Y = y; continue; }
            look.BeginAnimation(TranslateTransform.XProperty, DS.To(x, 260)); look.BeginAnimation(TranslateTransform.YProperty, DS.To(y, 260));
        }
    }

    void Blink()
    {
        blink.Interval = TimeSpan.FromSeconds(3.5 + Random.Shared.NextDouble() * 3);
        if (!Motion) return;
        foreach (var (_, lid, _) in eyes)
            lid.BeginAnimation(ScaleTransform.ScaleYProperty, new System.Windows.Media.Animation.DoubleAnimation(1, .1, TimeSpan.FromMilliseconds(80)) { AutoReverse = true });
    }

    /// <summary>A change landed: the eyes smile, the controller shakes on screen and rumbles in the hand.</summary>
    void Applied()
    {
        _ = AgentRumble.Pulse(player);
        if (!Motion) return;
        foreach (var (_, lid, _) in eyes)
            lid.BeginAnimation(ScaleTransform.ScaleYProperty, new System.Windows.Media.Animation.DoubleAnimationUsingKeyFrames { KeyFrames = { new System.Windows.Media.Animation.LinearDoubleKeyFrame(.35, TimeSpan.FromMilliseconds(120)), new System.Windows.Media.Animation.LinearDoubleKeyFrame(.35, TimeSpan.FromMilliseconds(900)), new System.Windows.Media.Animation.LinearDoubleKeyFrame(1, TimeSpan.FromMilliseconds(1050)) } });
        var jolt = new System.Windows.Media.Animation.DoubleAnimationUsingKeyFrames();
        double t = 0; foreach (double x in new[] { -6.0, 6, -5, 4, -2, 0 }) jolt.KeyFrames.Add(new System.Windows.Media.Animation.LinearDoubleKeyFrame(x, TimeSpan.FromMilliseconds(t += 45)));
        shake.BeginAnimation(TranslateTransform.XProperty, jolt);
    }

    async Task Do(AgentCommand command)
    {
        switch (command)
        {
            case AgentCommand.AboutController:
                Say(AgentCommands.AboutController(padName), null); Look(Gaze.Typing); _ = Lights(AgentMood.Good); break;
            case AgentCommand.Battery:
                Searching(true);
                string? level = await Task.Run(() => XInputDriftMeter.BatteryLevel(player));
                Searching(false); Say(AgentCommands.Battery(level, padName), null); _ = Lights(level == null ? AgentMood.Unsure : AgentMood.Good);
                break;
            case AgentCommand.ChangeController:
                Say(AgentCommands.ChangeController(), null); _ = Lights(AgentMood.Good);
                await Task.Delay(1400); ChangeControllerRequested?.Invoke(); break;
            case AgentCommand.Star:
                Say(AgentCommands.Star(), null); _ = Lights(AgentMood.Good); Open(ExternalLinks.Repository); break;
            case AgentCommand.Donate:
                Say(AgentCommands.Donate(), null); _ = Lights(AgentMood.Good); Open(ProductLinks.Support); break;
            case AgentCommand.Diagnostics:
                Say(AgentCommands.Diagnostics(), null); _ = Lights(AgentMood.Good);
                await Task.Delay(900); PageRequested?.Invoke("Diagnostics"); break;
        }
    }

    static void Open(string url)
    {
        if (ReviewSandbox.Active) return;
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true })?.Dispose(); }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException) { }
    }

    /// <summary>The search settled: the agent speaks up about it, once per search.</summary>
    void Announce()
    {
        string q = Search.Text.Trim();
        if (q.Length < 2 || q == announced || busy) return;
        announced = q; Look(Gaze.Ideas);
        if (AgentIdeas.Search(q).Count > 0) { Say(AgentIdeas.TopicReply(q), null); _ = Lights(AgentMood.Good); }
        else { Say(AgentCharacter.NothingFound(q), null); _ = Lights(AgentMood.Unsure); }
    }

    // ---- the player lights (owner request, 13 Oct 2026): blinking blue while it searches or thinks, then green for a good
    // result, violet when it isn't sure, red when it failed; afterwards the steady light of this player ----
    static readonly Color Blue = Color.FromRgb(70, 150, 255), Green = Color.FromRgb(61, 214, 140), Violet = Color.FromRgb(168, 110, 255);
    void Blink(Color color, int ms, bool wave)
    {
        for (int i = 0; i < 4; i++)
        {
            leds[i].Background = new SolidColorBrush(color);
            leds[i].BeginAnimation(UIElement.OpacityProperty, Motion ? new System.Windows.Media.Animation.DoubleAnimation(1, .15, TimeSpan.FromMilliseconds(ms)) { AutoReverse = true, BeginTime = TimeSpan.FromMilliseconds(wave ? 120 * i : 0), RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever } : null);
        }
    }
    void Steady()
    {
        lightsOn = false;
        foreach (var led in leds) { led.BeginAnimation(UIElement.OpacityProperty, null); led.Opacity = 1; }
        ShowState();
    }
    /// <summary>Searching or thinking: blue, blinking along the row until a result.</summary>
    void Searching(bool on)
    {
        lightShow++;
        typingLit = false;
        if (on) { lightsOn = true; Blink(Blue, 300, wave: true); EyeColor(Blue); } else { Steady(); EyeColor(AgentMark.Ice); }
    }
    async Task Lights(AgentMood mood)
    {
        int me = ++lightShow; lightsOn = true;
        typingLit = false;
        var color = mood == AgentMood.Good ? Green : AgentMark.Red;
        Blink(color, mood == AgentMood.Good ? 320 : 200, wave: false); EyeColor(color);
        await Task.Delay(3000);
        if (me == lightShow) { Steady(); EyeColor(AgentMark.Ice); }
    }

    /// <summary>Move the window by its title bar, kept inside its host.</summary>
    void Drag(FrameworkElement handle)
    {
        Point? from = null; double x0 = 0, y0 = 0;
        handle.MouseLeftButtonDown += (_, e) => { if (e.OriginalSource is DependencyObject d && OnButton(d)) return; from = e.GetPosition(null); x0 = offset.X; y0 = offset.Y; handle.CaptureMouse(); e.Handled = true; };
        handle.MouseMove += (_, e) =>
        {
            if (from == null || Body.Parent is not FrameworkElement host) return;
            var now = e.GetPosition(null);
            // the window sits at the host's bottom right; it may travel left and up until it meets the host's edge
            double maxLeft = host.ActualWidth - Body.ActualWidth - Body.Margin.Right, maxUp = host.ActualHeight - Body.ActualHeight - Body.Margin.Bottom;
            offset.X = Math.Clamp(x0 + now.X - from.Value.X, -Math.Max(0, maxLeft), Body.Margin.Right - 8);
            offset.Y = Math.Clamp(y0 + now.Y - from.Value.Y, -Math.Max(0, maxUp), Body.Margin.Bottom - 8);
        };
        handle.MouseLeftButtonUp += (_, _) => { from = null; handle.ReleaseMouseCapture(); };
        static bool OnButton(DependencyObject d) { for (var e = d; e != null; e = e is Visual ? VisualTreeHelper.GetParent(e) : LogicalTreeHelper.GetParent(e)) if (e is Button) return true; return false; }
    }

    /// <summary>Opens out of the round button (the window's bottom-right corner), and folds back into it.</summary>
    readonly bool reducedMotion;
    bool Motion => !reducedMotion && DS.Motion;
    public void Show()
    {
        Body.Visibility = Visibility.Visible; Body.IsHitTestVisible = true; if(Motion) blink.Start();
        if (!Motion) { Body.Opacity = 1; pop.ScaleX = pop.ScaleY = 1; return; }
        Body.BeginAnimation(UIElement.OpacityProperty, DS.To(1, 180));
        pop.BeginAnimation(ScaleTransform.ScaleXProperty, new System.Windows.Media.Animation.DoubleAnimation(.6, 1, TimeSpan.FromMilliseconds(260)) { EasingFunction = DS.Spring });
        pop.BeginAnimation(ScaleTransform.ScaleYProperty, new System.Windows.Media.Animation.DoubleAnimation(.6, 1, TimeSpan.FromMilliseconds(260)) { EasingFunction = DS.Spring });
    }
    public void Hide()
    {
        Body.IsHitTestVisible = false; StopCharacterMotion();
        if (!Motion) { Body.Visibility = Visibility.Collapsed; return; }
        var fade = DS.To(0, 160); fade.Completed += (_, _) => { if (!Body.IsHitTestVisible) Body.Visibility = Visibility.Collapsed; };
        Body.BeginAnimation(UIElement.OpacityProperty, fade);
        pop.BeginAnimation(ScaleTransform.ScaleXProperty, DS.To(.6, 160)); pop.BeginAnimation(ScaleTransform.ScaleYProperty, DS.To(.6, 160));
    }
    public bool IsShown => Body.Visibility == Visibility.Visible && Body.IsHitTestVisible;
    void StopCharacterMotion()
    {
        blink.Stop(); lightShow++; Circling(false); Steady();
        foreach(var (look,_,_) in eyes) { look.BeginAnimation(TranslateTransform.XProperty,null); look.BeginAnimation(TranslateTransform.YProperty,null); }
    }

    void Restart()
    {
        if (busy) return;
        session.Forget(); transcript.Children.Clear(); Input.Text = ""; announced = ""; Search.Text = ""; settle.Stop(); Say(greeting, null); Input.Focus();
    }

    PcModeProfile Current => link == null ? preview : link.Store.Get(PcModePage.StoreKey);

    void Store(PcModeProfile next)
    {
        if (link == null) { preview = next; return; }
        link.Store.Save(PcModePage.StoreKey, next); link.Service.Reload(); PcModePage.Replaced();
    }

    void ShowState()
    {
        status.Text = "Ready";
        statusDot.Background = DS.Brush("DS.Success");
        if (!lightsOn) for (int i = 0; i < 4; i++) leds[i].Background = i == Math.Clamp(player, 0, 3) || busy ? statusDot.Background : new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
        send.IsEnabled = !busy; sendRound.IsEnabled = !busy; sendRound.Opacity = busy ? .45 : 1;
    }

    async void Send() { if (Input.Text.Trim() is { Length: > 0 } sentence && !busy) { Input.Text = ""; await Ask(sentence); } }

    /// <summary>The whole round: the sentence, the model, the checks, and (when valid) the settings changed. Returns the
    /// turn, or null when the model couldn't be reached.</summary>
    public async Task<AgentTurn?> Ask(string sentence)
    {
        if (busy) return null;
        ShowView(View.Chat);
        // broken English is read as plain English first; the owner sees how (Preview 6)
        string said = sentence; sentence = AgentGrammar.Plain(said); string? reading = AgentGrammar.Reading(said, sentence);
        // the help list is the app's own answer: instant, the same online and offline
        if (AgentHelp.Asks(sentence)) { Mine(said, reading); announced = ""; Search.Text = ""; settle.Stop(); ShowIdeas("", everything: true); ShowView(View.Ideas); Searching(false); Look(Gaze.Ideas); Say(AgentVariety.Pick("help", ["Here's everything I can do. Pick one, or search it. *proudly clicks every button*", "Ta-da! My whole skill list. Pick one.", "Everything I'm good at, listed. It's a long list. *ahem*"]), null); _ = Lights(AgentMood.Good); return null; }
        // a topic alone is a search: the ideas narrow to it, and the app answers itself
        // the app's own commands: the controller's name, the battery, another controller, a star, a donation
        if (AgentCommands.Parse(sentence) is var command and not AgentCommand.None && (!session.HasPendingClarification || command != AgentCommand.AboutController)) { Mine(said, reading); session.CancelClarification(); await Do(command); return null; }
        if (!session.HasPendingClarification && AgentIdeas.Topic(sentence) is { } topic)
        {
            Mine(said, reading); announced = topic; Search.Text = topic; settle.Stop(); ShowView(View.Ideas);
            Look(Gaze.Ideas); Say(AgentIdeas.TopicReply(topic), null); _ = Lights(AgentMood.Good); return null;
        }
        busy = true; settle.Stop(); Searching(true); ShowState(); Thinking?.Invoke(true); Circling(true);
        Mine(said, reading);
        var thinking = Bubble("Working on your request…", mine: false);
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        string phase = "Reading your request";
        session.Progress = text => phase = text;
        var waitTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        waitTimer.Tick += (_, _) => { if (thinking.Child is TextBlock label) label.Text = $"{phase} · {elapsed.Elapsed.TotalSeconds:0}s"; };
        waitTimer.Start();
        var before = Current;
        AgentMood? mood = null;
        try
        {
            var turn = await session.Ask(sentence, before);
            mood = AgentCharacter.Mood(turn);
            transcript.Children.Remove(thinking);
            // PC mode on or off, when asked: the switch itself, not a setting
            string? switched = turn.Outcome.PcMode is { } on ? (link == null ? "PC mode isn't reachable from this window." : await PcModePage.SwitchFromAgent(link, on) ?? "") : null;
            string? mouse = turn.Outcome.Mouse is { } mouseOn ? (MouseSwitch == null ? "Mouse mode isn't reachable from this window." : MouseSwitch(mouseOn) ?? "") : null;
            if (turn.Outcome.Next is { } next && turn.Outcome.Changes.Count > 0)
            {
                try { Store(next); Applied(); }
                catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException) { Say(turn, null, "Couldn't save: " + error.Message); mood = AgentMood.Failed; return turn; }
                // the walk-through first, so the answer and its recap are waiting when the window comes back
                if (Walkthrough != null)
                {
                    waitTimer.Stop(); Circling(false); Searching(false);
                    await Walkthrough(before, next, turn.Measured);
                }
                Say(turn, before, switched: switched, mouse: mouse); ShowSettings(turn.Outcome.Changes.Select(c => c.What).ToHashSet());
            }
            else { Say(turn, null, switched: switched, mouse: mouse); if (switched != null) ShowSettings(new HashSet<string>()); }
            Circling(false);
            if (turn.Verdict.Proposal?.Speech is { } spoken && AgentCharacter.IsSilly(spoken)) Laugh();
            if (!turn.Verdict.Valid || turn.Outcome.Error != null) { Look(Gaze.Down); await Task.Delay(1400); }
            else if (Wiggles(turn.Verdict.Proposal?.Speech)) { Wiggle(); await Task.Delay(800); }
            if (turn.Verdict.Valid && turn.Outcome.Page is { } page) { await Task.Delay(700); PageRequested?.Invoke(page); }
            return turn;
        }
        catch (Exception error) when (error is TaskCanceledException or System.Text.Json.JsonException)
        {
            transcript.Children.Remove(thinking);
            Say(error is TaskCanceledException ? AgentCharacter.Dozed : "I hit a snag: " + error.Message, null);
            mood = AgentMood.Failed; return null;
        }
        finally { session.Progress = null; waitTimer.Stop(); busy = false; Circling(false); if (measuring != null) { transcript.Children.Remove(measuring); measuring = null; } Searching(false); Look(Gaze.Idle); Thinking?.Invoke(false); Input.Focus(); if (mood is { } m) _ = Lights(m); }
    }

    // ---- the drift reading: a countdown while the sticks are read ----
    Border? measuring;
    void Measuring(string phase, int seconds)
    {
        if (measuring == null)
        {
            measuring = Bubble("", mine: false); measuring.BorderBrush = DS.Brush("DS.Warn"); Look(Gaze.Down);
            measuring.Child = new StackPanel { Children = { Kit.Text("", 16, "DS.Text", FontWeights.SemiBold), Wrap("Hands off the controller: I'm reading where the sticks rest.", 13.5, "DS.TextSoft", 4) } };
        }
        ((TextBlock)((StackPanel)measuring.Child).Children[0]).Text = $"{phase} · {seconds}";
        scroller.ScrollToEnd();
    }
        public PcModeProfile Settings => Current;
    /// <summary>Opens every answer's details (validation and model output), for review captures.</summary>
    public void ExpandDetails() { foreach (var b in transcript.Children.OfType<Border>()) if (b.Child is StackPanel { Children.Count: > 1 } s && s.Children[^1] is StackPanel d) d.Visibility = Visibility.Visible; }
    public void ScrollTo(double offset) { Body.UpdateLayout(); scroller.ScrollToVerticalOffset(offset); }

    // ---- the conversation ----
    Border Bubble(string text, bool mine)
    {
        var bubble = new Border
        {
            CornerRadius = new(16), Padding = new(14, 10, 14, 10), Margin = mine ? new(50, 5, 0, 5) : new(0, 5, 24, 5),
            HorizontalAlignment = mine ? HorizontalAlignment.Right : HorizontalAlignment.Left, BorderThickness = new(1),
            Background = new SolidColorBrush(mine ? Color.FromRgb(34, 39, 47) : Card),
            BorderBrush = new SolidColorBrush(mine ? Color.FromRgb(58, 64, 74) : Line),
            Child = Wrap(text, 14, "DS.Text"),
        };
        transcript.Children.Add(bubble); Fx.Appear(bubble, .97, 200); scroller.ScrollToEnd(); return bubble;
    }

    /// <summary>The owner's own words, and under them how the agent read them when that was more than tidying.</summary>
    Border Mine(string said, string? reading)
    {
        var bubble = Bubble(said, mine: true);
        lastMine = (said, [bubble]);
        if (reading != null)
        {
            var read = Wrap("I read that as: " + reading, 12, "DS.TextMuted").Also(t => { t.HorizontalAlignment = HorizontalAlignment.Right; t.Margin = new(50, 0, 6, 6); });
            transcript.Children.Add(read); lastMine.Value.Shown.Add(read);
            scroller.ScrollToEnd();
        }
        return bubble;
    }
    (string Said, List<UIElement> Shown)? lastMine;

    void Say(string text, string? detail)
    {
        var b = Bubble(text, false);
        if (detail != null) ((TextBlock)b.Child).Text += "\n" + detail;
        if (Wiggles(text)) Application.Current.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(Wiggle));
    }

    /// <summary>One answer: the model's words, then what the app did, then the details on request.</summary>
    void Say(AgentTurn turn, PcModeProfile? undo, string? failure = null, string? switched = null, string? mouse = null)
    {
        var stack = new StackPanel();
        var v = turn.Verdict;
        var sticks = failure == null && undo != null && turn.Outcome.Next is { } curved ? AgentResponsePreview.ChangedSticks(undo, curved) : [];
        bool hero = sticks.Count > 0;
        string spoken = v.Proposal?.Speech is { Length: > 0 } speech ? speech : "I didn't give a usable answer.";
        if (hero)
        {
            stack.Children.Add(Wrap(spoken, 23, "DS.Text").Also(t => { t.FontWeight = FontWeights.Bold; t.TextAlignment = TextAlignment.Center; t.HorizontalAlignment = HorizontalAlignment.Center; t.MaxWidth = 640; }));
            if (lastMine is { } asked)
            {
                foreach (var shown in asked.Shown) transcript.Children.Remove(shown);
                lastMine = null;
                stack.Children.Add(new Border { CornerRadius = new(10), Padding = new(16, 7, 16, 8), Margin = new(0, 10, 0, 2), HorizontalAlignment = HorizontalAlignment.Center, BorderThickness = new(1),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(58, 64, 74)), Background = new SolidColorBrush(Color.FromRgb(34, 39, 47)), Child = Wrap(asked.Said, 14, "DS.Text") });
            }
        }
        else stack.Children.Add(Wrap(spoken, 17, "DS.Text").Also(t => t.FontWeight = FontWeights.SemiBold));
        // what the app itself measured, before what the model made of it
        if (turn.Measured is { } m)
        {
            if (m.Problem != null) stack.Children.Add(Wrap("Not measured · " + m.Problem, 14, "DS.Warn", 8));
            foreach (var d in m.Sticks)
                stack.Children.Add(Wrap(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Measured · {AgentApplier.Name(d.Stick)} rests at {d.RestX:+0.0;-0.0;0.0}%, {d.RestY:+0.0;-0.0;0.0}% and wanders up to {d.Peak:0.0}%"), 14, "DS.Warn", 8));
        }
        foreach (var c in turn.Outcome.Changes)
            stack.Children.Add(Wrap($"{c.What}   {c.Before} → {c.After}", hero ? 12.5 : 14, hero ? "DS.TextMuted" : "DS.AccentHi", 6).Also(t => { if (hero) t.HorizontalAlignment = HorizontalAlignment.Center; }));
        // a stick that changed: its curve now and the one proposed, side by side
        if (hero && undo != null && turn.Outcome.Next is { } saved)
            foreach (var stick in sticks)
            {
                var points = AgentResponsePreview.Curve(undo, saved, stick);
                var pair = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2, Margin = new(0, 10, 0, 0) };
                pair.Children.Add(CurveCard("Current", AgentApplier.Name(stick), points.Select(p => (p.Input, p.Before)).ToList(), proposed: false));
                pair.Children.Add(CurveCard("Proposed", AgentApplier.Name(stick), points.Select(p => (p.Input, p.After)).ToList(), proposed: true));
                stack.Children.Add(pair);
                if (AgentResponsePreview.Samples(undo, saved).FirstOrDefault(s => s.Target == stick)?.ContextualNote is { } note) stack.Children.Add(Wrap(note, 12, "DS.TextMuted", 6));
            }
        // why it fits, or the answer to a question: the model's words, from the settings' meanings
        if (v.Valid && v.Proposal?.Recap is { Length: > 0 } recap && hero)
            stack.Children.Add(Wrap(recap, 14.5, "DS.Text", 12).Also(t => { t.TextAlignment = TextAlignment.Center; t.HorizontalAlignment = HorizontalAlignment.Center; t.MaxWidth = 680; }));
        else if (v.Valid && v.Proposal?.Recap is { Length: > 0 } other)
            stack.Children.Add(new Border { BorderBrush = DS.Brush("DS.AccentHi"), BorderThickness = new(2, 0, 0, 0), Padding = new(12, 2, 0, 2), Margin = new(0, 10, 0, 0), Child = Wrap(other, 13.5, "DS.TextSoft") });
        // the switch: "" when it worked, otherwise why not
        if (switched != null && turn.Outcome.PcMode is { } pc)
            stack.Children.Add(Wrap(switched.Length == 0 ? $"PC mode   {(pc ? "on" : "off")}" : "PC mode · " + switched, 14, switched.Length == 0 ? "DS.AccentHi" : "DS.Warn", 6));
        if (mouse != null && turn.Outcome.Mouse is { } mo)
            stack.Children.Add(Wrap(mouse.Length == 0 ? $"Mouse mode   {(mo ? "on" : "off")}" : "Mouse mode · " + mouse, 14, mouse.Length == 0 ? "DS.AccentHi" : "DS.Warn", 6));
        // what it asks back: the missing detail, or whether the change feels right
        if (v.Proposal?.Question is { Length: > 0 } question)
            stack.Children.Add(new Border { CornerRadius = new(10), Background = new SolidColorBrush(Color.FromArgb(60, 70, 150, 255)), Padding = new(12, 8, 12, 8), Margin = new(0, 10, 0, 0), Child = Wrap(question, 14, "DS.Text").Also(t => t.FontWeight = FontWeights.SemiBold) });
        // where the answer came from: the doc passages it cites, each opening to its full text
        foreach (var skipped in v.Checks.Where(c => c.State == CheckState.Unavailable))
            stack.Children.Add(Wrap($"{skipped.Action.Target}: {skipped.Message}", 14, "DS.Warn", 6));
        // the app's own line: the truth about what happened, whatever the model said
        string verdict = failure ?? (turn.Outcome.Next != null && turn.Outcome.Changes.Count == 0 && switched == "" ? "Applied · PC mode switched " + (turn.Outcome.PcMode == true ? "on" : "off")
            : turn.Outcome.Next != null && turn.Outcome.Changes.Count == 0 && mouse == "" ? "Applied · Mouse mode " + (turn.Outcome.Mouse == true ? "on" : "off")
            : v.Valid && turn.Outcome.Changes.Count == 0 && turn.Outcome.Page is { } goTo ? "Opening " + goTo
            : turn.Outcome.Next != null && turn.Outcome.Changes.Count > 0 ? $"Applied · {turn.Outcome.Changes.Count} change{(turn.Outcome.Changes.Count == 1 ? "" : "s")}{(link == null ? " · preview only" : "")}"
            : !v.Valid ? "Rejected · nothing changed · " + AgentSession.Problems(v)
            : turn.Outcome.Error != null ? "Rejected · " + turn.Outcome.Error : "No changes");
        var verdictText = Wrap(verdict, 14, failure != null || !v.Valid || turn.Outcome.Error != null ? "DS.Danger" : turn.Outcome.Changes.Count > 0 ? "DS.Success" : "DS.TextMuted").Also(t => { t.FontWeight = FontWeights.SemiBold; t.VerticalAlignment = VerticalAlignment.Center; });
        var footer = new DockPanel { Margin = new(0, 12, 0, 0), LastChildFill = true };
        // applied straight away, so the way back sits beside it: only the latest change can be undone
        if (failure == null && undo != null && turn.Outcome.Next != null && turn.Outcome.Changes.Count > 0)
        {
            var back = undo;
            var undoButton = PadKey(new Border { CornerRadius = new(15), Height = 30, Padding = new(14, 0, 14, 0), BorderThickness = new(1.5), BorderBrush = new SolidColorBrush(Amber), Background = Brushes.Transparent,
                Child = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Children = { Kit.Icon("", 12, "DS.Text").Also(i => { i.Foreground = new SolidColorBrush(Amber); i.Margin = new(0, 0, 7, 0); }), Kit.Text("Undo", 13, "DS.Text", FontWeights.SemiBold).Also(t => t.Foreground = new SolidColorBrush(Amber)) } } }, "Undo this change", null);
            undoButton.Click += (_, _) => Undo(undoButton, back);
            if (undoLatest != null) { undoLatest.IsEnabled = false; undoLatest.Opacity = .35; }
            undoLatest = undoButton; register(undoButton);
            DockPanel.SetDock(undoButton, Dock.Right); footer.Children.Add(undoButton);
        }
        footer.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Children = { Kit.Icon("", 13, "DS.TextMuted").Also(i => i.Margin = new(0, 0, 8, 0)), verdictText } });
        stack.Children.Add(footer);

        var bubble = Bubble("", false); bubble.Child = stack; bubble.HorizontalAlignment = HorizontalAlignment.Stretch;
        if (hero) { bubble.Background = Brushes.Transparent; bubble.BorderBrush = Brushes.Transparent; bubble.Margin = new(0, 10, 12, 6); }
        scroller.ScrollToEnd();
    }

    void Undo(Button button, PcModeProfile back)
    {
        if (busy || button != undoLatest) return;
        try { Store(back); }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException) { Say("Couldn't undo: " + error.Message, null); _ = Lights(AgentMood.Failed); return; }
        button.IsEnabled = false; button.Opacity = .35; undoLatest = null;
        ShowSettings(new HashSet<string>());
        Say("Undone. Back to how it was.", null); _ = Lights(AgentMood.Good);
    }

    /// <summary>A stick's curve as a small card: push across, output up; the proposed one in amber with its area filled.</summary>
    static Border CurveCard(string title, string stick, IReadOnlyList<(double Input, double Output)> points, bool proposed)
    {
        const double W = 300, H = 92;
        var accent = proposed ? Amber : Color.FromRgb(150, 158, 170);
        var plot = new Canvas { Width = W, Height = H, ClipToBounds = true, Margin = new(0, 8, 0, 0) };
        for (int i = 1; i < 4; i++)
        {
            plot.Children.Add(new System.Windows.Shapes.Line { X1 = W * i / 4, X2 = W * i / 4, Y1 = 0, Y2 = H, Stroke = new SolidColorBrush(Color.FromArgb(22, 255, 255, 255)), StrokeThickness = 1 });
            plot.Children.Add(new System.Windows.Shapes.Line { X1 = 0, X2 = W, Y1 = H * i / 4, Y2 = H * i / 4, Stroke = new SolidColorBrush(Color.FromArgb(22, 255, 255, 255)), StrokeThickness = 1 });
        }
        var line = new PointCollection(points.Select(p => new Point(W * p.Input / 100, H - H * Math.Clamp(p.Output, 0, 100) / 100)));
        if (proposed)
        {
            var area = new PointCollection(line) { new Point(W, H), new Point(0, H) };
            plot.Children.Add(new System.Windows.Shapes.Polygon { Points = area, Fill = new LinearGradientBrush(Color.FromArgb(90, Amber.R, Amber.G, Amber.B), Color.FromArgb(0, Amber.R, Amber.G, Amber.B), 90) });
        }
        plot.Children.Add(new System.Windows.Shapes.Polyline { Points = line, Stroke = new SolidColorBrush(accent), StrokeThickness = proposed ? 2.5 : 2, StrokeLineJoin = PenLineJoin.Round });
        // the points marked along it, the last one the brightest
        for (int i = 0; i < line.Count; i += Math.Max(1, (line.Count - 1) / 6))
        {
            bool end = i >= line.Count - 1 - Math.Max(1, (line.Count - 1) / 6) / 2; double r = end ? 4.5 : 3;
            var dot = new System.Windows.Shapes.Ellipse { Width = 2 * r, Height = 2 * r, Fill = new SolidColorBrush(accent) };
            Canvas.SetLeft(dot, Math.Clamp(line[i].X, r, W - r) - r); Canvas.SetTop(dot, Math.Clamp(line[i].Y, r, H - r) - r); plot.Children.Add(dot);
        }
        var head = new DockPanel();
        var name = Kit.Text(stick, 12, "DS.TextMuted"); DockPanel.SetDock(name, Dock.Right); head.Children.Add(name);
        head.Children.Add(Kit.Text(title, 13.5, "DS.Text", FontWeights.SemiBold).Also(t => { if (proposed) t.Foreground = new SolidColorBrush(Amber); }));
        return new Border
        {
            CornerRadius = new(12), Padding = new(14, 10, 14, 10), Margin = proposed ? new(6, 0, 0, 0) : new(0, 0, 6, 0), BorderThickness = new(1),
            BorderBrush = new SolidColorBrush(proposed ? Color.FromArgb(150, Amber.R, Amber.G, Amber.B) : Line), Background = new SolidColorBrush(Color.FromRgb(15, 18, 23)),
            Child = new StackPanel { Children = { head, Axes(new Viewbox { Stretch = Stretch.Fill, Height = H, Child = plot }) } },
        };
    }

    /// <summary>The plot with its axes named: output up the side, the stick's push along the bottom.</summary>
    static Grid Axes(UIElement plot)
    {
        var grid = new Grid { ColumnDefinitions = { new() { Width = GridLength.Auto }, new() }, RowDefinitions = { new(), new() { Height = GridLength.Auto } } };
        var output = Kit.Text("Output", 10.5, "DS.TextMuted").Also(t => { t.LayoutTransform = new RotateTransform(-90); t.Margin = new(0, 0, 6, 0); t.HorizontalAlignment = HorizontalAlignment.Center; });
        grid.Children.Add(output);
        Grid.SetColumn(plot, 1); grid.Children.Add(plot);
        var push = Kit.Text("Stick deflection →", 11, "DS.TextMuted").Also(t => { t.HorizontalAlignment = HorizontalAlignment.Center; t.Margin = new(0, 4, 0, 0); });
        Grid.SetRow(push, 1); Grid.SetColumn(push, 1); grid.Children.Add(push);
        return grid;
    }

    public bool SettingsOpen => settingsBox.Visibility == Visibility.Visible;
    public void ShowSettingsPage(bool show)
    {
        settingsBox.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        if (show) { ShowSettings(new HashSet<string>()); settingsBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.First)); }
        else Input.Focus();
    }
    // ---- the settings panel ----
    void ShowSettings(IReadOnlySet<string> changed)
    {
        target.Text = link == null ? "Preview · PC mode isn't available in this window"
            : $"PC mode · Player {PcModePage.Player + 1}, {(PcModePage.ProfileKey == PcModeStore.DefaultKey ? "every game" : PcModePage.ProfileKey[PcModeStore.GamePrefix.Length..])}{(link.Service.Enabled ? "" : " · PC mode is off, so games don't see these yet")}";
        settings.Children.Clear();
        var rows = AgentApplier.Diff(new PcModeProfile(), Current);
        settingsToggle.Text = (rows.Count == 0 ? "Settings now · all default" : $"Settings now · {rows.Count} changed");
        if (rows.Count == 0) settings.Children.Add(Wrap("Everything is at its default.", 14, "DS.TextSoft"));
        foreach (var r in rows)
        {
            bool fresh = changed.Contains(r.What);
            var line = new DockPanel { Margin = new(0, 0, 0, 6) };
            var value = Kit.Text(r.After, 14, fresh ? "DS.AccentHi" : "DS.Text", FontWeights.SemiBold); DockPanel.SetDock(value, Dock.Right); line.Children.Add(value);
            line.Children.Add(Kit.Text(r.What, 14, "DS.TextSoft"));
            settings.Children.Add(line);
            if (fresh) Fx.Appear(line, 1.04, 320);
        }
        var paddles = AgentVocabulary.Paddles(modelId);
        if (paddles.Length > 0) settings.Children.Add(Wrap($"Paddles {paddles[0]}–{paddles[^1]} are programmed on the controller, which isn't supported yet.", 12.5, "DS.TextMuted", 8));
    }

    UIElement Source(DocHit hit)
    {
        string text = hit.Chunk.Text, shortText = text.Length > 170 ? text[..170].TrimEnd() + "…" : text;
        var body = Wrap(shortText, 13.5, "DS.TextSoft", 4);
        var card = new Button { Style = DS.Style("ActionButton"), MinHeight = 0, Padding = new(12, 8, 12, 8), Margin = new(0, 8, 0, 0), HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left,
            Content = new StackPanel { Children = { Wrap("Source · " + hit.Chunk.Where, 12.5, "DS.AccentHi").Also(t => t.FontWeight = FontWeights.SemiBold), body } } };
        card.Click += (_, _) => body.Text = body.Text == shortText ? text : shortText;
        return card;
    }

    static Button Small(string text) => new() { Style = DS.Style("ActionButton"), Height = 30, MinHeight = 0, Padding = new(12, 0, 12, 0), Content = Kit.Text(text, 13, "DS.TextSoft") };
    static TextBlock Wrap(string text, double size, string brush, double top = 0) => Kit.Text(text, size, brush).Also(t => { t.TextWrapping = TextWrapping.Wrap; t.TextTrimming = TextTrimming.None; t.Margin = new(0, top, 0, 0); });
}
