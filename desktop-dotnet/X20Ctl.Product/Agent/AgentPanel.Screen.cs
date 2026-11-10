using System.IO;
using System.Windows.Controls.Primitives;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using X20Ctl.Zone.Agent;
using X20Ctl.Zone.PcMode;
using Sh = System.Windows.Shapes;

namespace X20Ctl.Product.Agent;

/// <summary>
/// The screen's rail and its pages (owner, 8 Nov 2026: "rework it and add more stuff … anything related to the ai agent"):
/// a labelled rail of pages, Chat, Ideas, Setups, History, Live, Check-up and Look, with the agent itself at its foot (its
/// eyes, its brain's state, its settings); a welcome in the empty chat with topic cards and a strip of what is true now.
/// Every action on a page goes the same way as when it is asked for in the chat: listed, in the history, undoable.
/// </summary>
public partial class AgentPanel
{
    ScrollViewer setupsView = null!, historyView = null!, liveView = null!, lookView = null!;
    readonly StackPanel setupsList = new(), historyList = new(), lookPage = new();
    readonly Dictionary<View, (Border Badge, TextBlock Count)> badges = [];
    readonly TextBlock brainText = Kit.Text("", 10, "DS.TextMuted").Also(t => { t.HorizontalAlignment = HorizontalAlignment.Center; t.TextTrimming = TextTrimming.CharacterEllipsis; });
    readonly Border brainDot = new() { Width = 7, Height = 7, CornerRadius = new(3.5), Margin = new(0, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center };
    readonly List<Sh.Rectangle> pupils = [];
    StackPanel? welcome; WrapPanel? strip; bool started;
    Border? pillsRow;
    readonly Border padSwatch = new() { Width = 10, Height = 10, CornerRadius = new(5), Margin = new(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center, BorderThickness = new(1), BorderBrush = new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)) };
    string? battery; bool batteryAsked;

    static readonly SolidColorBrush RailOn = new(ThemeCatalog.R(Color.FromRgb(34, 39, 47)));

    // ---- the rail ----
    Border BuildRail()
    {
        var top = new StackPanel();
        Border Item(View? view, string glyph, string label, string tip, Action act, Panel into)
        {
            var stripe = new Border { Width = 3, Height = 20, CornerRadius = new(1.5), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, Background = Brushes.Transparent };
            var icon = Kit.Icon(glyph, 15, "DS.TextSoft").Also(i => i.HorizontalAlignment = HorizontalAlignment.Center);
            var name = Kit.Text(label, 9.5, "DS.TextMuted", FontWeights.SemiBold).Also(t => { t.HorizontalAlignment = HorizontalAlignment.Center; t.Margin = new(0, 3, 0, 0); });
            var count = Kit.Text("", 9, "DS.Text", FontWeights.Bold).Also(t => { t.HorizontalAlignment = HorizontalAlignment.Center; t.VerticalAlignment = VerticalAlignment.Center; t.Foreground = new SolidColorBrush(ThemeCatalog.R(Color.FromRgb(24, 20, 8))); });
            var badge = new Border { MinWidth = 15, Height = 15, CornerRadius = new(7.5), Padding = new(3, 0, 3, 0), Background = new SolidColorBrush(Amber), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new(0, 2, 4, 0), Visibility = Visibility.Collapsed, Child = count, IsHitTestVisible = false };
            var look = new Border { Width = 56, Height = 42, CornerRadius = new(11), Margin = new(0, 1, 0, 1), BorderThickness = new(1), BorderBrush = Brushes.Transparent, Background = Brushes.Transparent,
                Child = new Grid { Children = { stripe, new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { icon, name } }, badge } } };
            var item = PadKey(look, tip, act); register(item); into.Children.Add(item);
            if (view is View v) { rail[v] = look; badges[v] = (badge, count); }
            return look;
        }
        Item(View.Chat, "", "Chat", "Conversation", () => ShowView(View.Chat), top);
        Item(View.Ideas, "", "Ideas", "Ideas: everything you can ask", () => { ShowView(View.Ideas); Search.Focus(); }, top);
        Item(View.Setups, "", "Setups", "Your saved setups", () => ShowView(View.Setups), top);
        Item(View.History, "", "History", "Every change made here", () => ShowView(View.History), top);
        Item(View.Live, "", "Live", "The controller, live", () => ShowView(View.Live), top);
        Item(View.Checkup, "", "Check-up", "Controller check-up", () => ShowView(View.Checkup), top);
        Item(View.Look, "", "Look", "The agent's colour, the pad's colour, its eyes", () => ShowView(View.Look), top);

        // the foot of the rail: the agent itself, its eyes in their colour, what its brain is doing, and its settings
        var foot = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom };
        var face = new Border { Width = 40, Height = 22, CornerRadius = new(8), HorizontalAlignment = HorizontalAlignment.Center, Background = new SolidColorBrush(ThemeCatalog.R(Color.FromRgb(9, 10, 13))), BorderThickness = new(1), BorderBrush = new SolidColorBrush(Line), ToolTip = "Agent XCTL" };
        var eyesRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        for (int i = 0; i < 2; i++)
        {
            var mini = new Sh.Rectangle { Width = 5, Height = 10, RadiusX = 2.5, RadiusY = 2.5, Margin = new(i == 0 ? 0 : 7, 0, 0, 0), Fill = new SolidColorBrush(eyeColor), RenderTransformOrigin = new(.5, .5), RenderTransform = new ScaleTransform(1, 1) };
            eyesRow.Children.Add(mini); ledParts.Add(mini); miniEyes.Add(mini);
        }
        face.Child = eyesRow; foot.Children.Add(face);
        foot.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new(0, 5, 0, 4), Children = { brainDot, brainText } });
        settingsRail = Item(null, "", "Settings", "Agent settings", () => ShowSettingsPage(!SettingsOpen), foot);
        var dock = new DockPanel { LastChildFill = false };
        DockPanel.SetDock(top, Dock.Top); dock.Children.Add(top);
        DockPanel.SetDock(foot, Dock.Bottom); dock.Children.Add(foot);
        return new Border { Width = 66, Padding = new(4, 6, 4, 6), Margin = new(0, 0, 14, 0), CornerRadius = new(16), BorderThickness = new(1), BorderBrush = new SolidColorBrush(Line), Background = new SolidColorBrush(Card), Child = dock };
    }
    readonly List<Sh.Rectangle> miniEyes = [];

    /// <summary>A rail item lit (the page shown) or not.</summary>
    static void LightRail(Border look, bool on)
    {
        look.Background = on ? RailOn : Brushes.Transparent;
        if (look.Child is not Grid { Children: [Border stripe, StackPanel { Children: [TextBlock icon, TextBlock name] }, ..] }) return;
        stripe.Background = on ? new SolidColorBrush(Amber) : Brushes.Transparent;
        icon.Foreground = on ? DS.Brush("DS.Text") : DS.Brush("DS.TextSoft");
        name.Foreground = on ? DS.Brush("DS.Text") : DS.Brush("DS.TextMuted");
    }

    /// <summary>The brain's state at the foot of the rail, in a word.</summary>
    void ShowBrain()
    {
        brainText.Text = busy ? "Thinking" : "Offline";
        brainDot.Background = statusDot.Background;
        brainText.ToolTip = status.Text;
    }

    // ---- the welcome: topic cards and what is true now ----
    void AddWelcome()
    {
        started = false;
        var cards = new UniformGrid { Columns = 3, Margin = new(0, 6, 0, 0) };
        foreach (var (glyph, title, example, act) in new (string, string, string, Action)[]
        {
            ("", "Aim & sticks", "“My right stick drifts a little”", () => { Search.Text = "sticks"; ShowView(View.Ideas); }),
            ("", "Triggers", "“Faster triggers for shooting”", () => { Search.Text = "triggers"; ShowView(View.Ideas); }),
            ("", "Buttons", "“Turbo on X, 12 a second”", () => { Search.Text = "buttons"; ShowView(View.Ideas); }),
            ("", "Lights", "“Make my lights red”", () => ShowView(View.Look)),
            ("", "PC mode", "“Turn PC mode on”", () => { Search.Text = "PC mode"; ShowView(View.Ideas); }),
            ("", "Setups", "“Save this as Sniper”", () => ShowView(View.Setups)),
        })
        {
            var look = new Border
            {
                CornerRadius = new(12), Padding = new(12, 9, 10, 10), Margin = new(0, 0, 8, 8), BorderThickness = new(1), BorderBrush = new SolidColorBrush(Line), Background = new SolidColorBrush(Card),
                Child = new StackPanel { Children =
                {
                    new StackPanel { Orientation = Orientation.Horizontal, Children = { Kit.Icon(glyph, 14, "DS.TextSoft").Also(i => { i.Margin = new(0, 0, 8, 0); i.Foreground = new SolidColorBrush(Amber); }), Kit.Text(title, 14, "DS.Text", FontWeights.SemiBold) } },
                    Kit.Text(example, 12, "DS.TextMuted").Also(t => { t.Margin = new(0, 5, 0, 0); t.TextTrimming = TextTrimming.CharacterEllipsis; }),
                } },
            };
            var card = PadKey(look, title, act); register(card); cards.Children.Add(card);
        }
        strip = new WrapPanel { Margin = new(0, 4, 0, 0) };
        welcome = new StackPanel { Margin = new(0, 4, 0, 0), Children = { cards, strip } };
        transcript.Children.Add(welcome);
        if (pillsRow != null) pillsRow.Visibility = Visibility.Collapsed;
        RefreshStrip();
        if (!batteryAsked) { batteryAsked = true; _ = ReadBattery(); }
    }

    async Task ReadBattery()
    {
        battery = await BatteryNow();
        RefreshStrip();
    }

    /// <summary>The first thing said folds the welcome away; the topics stay, as a slim row above the chat bar.</summary>
    void Started()
    {
        if (started) return; started = true;
        if (welcome != null) welcome.Visibility = Visibility.Collapsed;
        if (pillsRow != null) pillsRow.Visibility = scroller.Visibility == Visibility.Visible || ideasView.Visibility == Visibility.Visible ? Visibility.Visible : Visibility.Collapsed;
    }

    internal void RefreshStrip()
    {
        string model = FinishModel(modelId), finish = PadColours.For(player, model);
        var named = PadColours.Catalog.For(model).FirstOrDefault(f => f.Id == finish);
        var swatch = named != null ? (Color)ColorConverter.ConvertFromString(named.Swatch) : Colors.Gray;
        padSwatch.Background = new SolidColorBrush(swatch);
        padChip.ToolTip = named == null ? padName : $"{named.Name} {padName}";
        if (strip == null) return;
        strip.Children.Clear();
        int changed = AgentApplier.Diff(new PcModeProfile(), Current).Count;
        Fact(Dot(swatch), named == null ? padName : $"{named.Name} {padName}");
        Fact(Icon(""), changed == 0 ? "All settings default" : $"{changed} setting{(changed == 1 ? "" : "s")} changed");
        Fact(Dot(Rest), worn == null ? "Agent in its own green" : $"Agent wearing {Hex(Rest)}");
        if (link?.Service.Enabled != true)
            Fact(Icon(""), battery switch { null => "Battery not reported", "wired" => "Wired", var level => $"Battery {level.Replace(" (Charging)", " · charging")}" });
        Fact(Icon(""), setups.Items.Count == 0 ? "No setups saved" : $"{setups.Items.Count} setup{(setups.Items.Count == 1 ? "" : "s")} saved");

        void Fact(FrameworkElement mark, string text) => strip.Children.Add(new Border
        {
            CornerRadius = new(13), Height = 26, Padding = new(9, 0, 11, 0), Margin = new(0, 0, 6, 6), BorderThickness = new(1), BorderBrush = new SolidColorBrush(Line), Background = new SolidColorBrush(ThemeCatalog.R(Color.FromRgb(15, 18, 23))),
            Child = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Children = { mark, Kit.Text(text, 12, "DS.TextSoft").Also(t => t.VerticalAlignment = VerticalAlignment.Center) } },
        });
        static FrameworkElement Dot(Color c) => new Border { Width = 9, Height = 9, CornerRadius = new(4.5), Margin = new(0, 0, 7, 0), VerticalAlignment = VerticalAlignment.Center, Background = new SolidColorBrush(c), BorderThickness = new(1), BorderBrush = new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)) };
        static FrameworkElement Icon(string glyph) => Kit.Icon(glyph, 11, "DS.TextMuted").Also(i => { i.Margin = new(0, 0, 7, 0); i.VerticalAlignment = VerticalAlignment.Center; });
    }
    static string Hex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    // ---- the pages ----
    static ScrollViewer Page(string title, string about, UIElement body, out StackPanel page)
    {
        page = new StackPanel { Margin = new(0, 0, 8, 0) };
        page.Children.Add(Kit.Text(title, 20, "DS.Text", FontWeights.SemiBold));
        page.Children.Add(Wrap(about, 13, "DS.TextMuted", 3));
        page.Children.Add(body.Also(b => { if (b is FrameworkElement f) f.Margin = new(0, 14, 0, 0); }));
        return new ScrollViewer { Content = page, Visibility = Visibility.Collapsed, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    void BuildPages()
    {
        setupsView = Page("Setups", "Your settings saved under a name, ready to bring back. Loading one is a change like any other: in the history, undoable.", setupsList, out _);
        historyView = Page("History", "Every change made here, newest first. Go back to before any of them.", historyList, out _);
        liveView = BuildLive();
        lookView = Page("Look", "How the agent and your pad look.", lookPage, out _);
        BuildLook();
        padColour += who => { if (who == player) { RefreshStrip(); BuildLook(); } };
    }

    /// <summary>Called whenever the settings, the history or the setups change: the pages and the badges follow.</summary>
    void RefreshPages()
    {
        if (setupsView == null) return;
        // setups
        setupsList.Children.Clear();
        var name = new TextBox { FontSize = 14, MinHeight = 36, Padding = new(10, 6, 10, 6), VerticalContentAlignment = VerticalAlignment.Center, MaxLength = AgentSetupStore.MaxName, ToolTip = "A name for what you have now" };
        var nameHint = Kit.Text("Name it, for example Rocket League", 14, "DS.TextMuted").Also(h => { h.IsHitTestVisible = false; h.Margin = new(12, 0, 0, 0); h.VerticalAlignment = VerticalAlignment.Center; });
        name.TextChanged += (_, _) => nameHint.Visibility = name.Text.Length > 0 ? Visibility.Collapsed : Visibility.Visible;
        var save = Small("Save what I have now");
        void SaveIt() { if (busy || AgentSetupStore.Clean(name.Text) is not { Length: > 0 } n) return; SetupFromChat(SetupAsk.Save, n, "save this as " + n); }
        save.Click += (_, _) => SaveIt(); name.KeyDown += (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; SaveIt(); } };
        register(save);
        var saveRow = new DockPanel { Margin = new(0, 0, 0, 14) };
        save.Margin = new(10, 0, 0, 0); DockPanel.SetDock(save, Dock.Right); saveRow.Children.Add(save); saveRow.Children.Add(new Grid { Children = { name, nameHint } });
        setupsList.Children.Add(saveRow);
        if (setups.Items.Count == 0) setupsList.Children.Add(Wrap("Nothing saved yet. Name what you have now and save it, or say “save this as Rocket League”.", 13.5, "DS.TextMuted"));
        foreach (var setup in setups.Items.OrderByDescending(s => s.Saved))
        {
            int differ = AgentApplier.Diff(Current, setup.Profile).Count, away = AgentApplier.Diff(new PcModeProfile(), setup.Profile).Count;
            var load = Small(differ == 0 ? "In use" : "Load"); load.IsEnabled = differ > 0;
            var s = setup; load.Click += (_, _) => { Load(s, "load " + s.Name); RefreshPages(); };
            var delete = Small("Delete"); bool armed = false;
            delete.Click += (_, _) =>
            {
                if (!armed) { armed = true; ((TextBlock)delete.Content).Text = "Sure? Delete"; ((TextBlock)delete.Content).Foreground = DS.Brush("DS.Warn"); return; }
                SetupFromChat(SetupAsk.Delete, s.Name, "delete " + s.Name);
            };
            register(load); register(delete);
            var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Children = { load, delete.Also(d => d.Margin = new(8, 0, 0, 0)) } };
            setupsList.Children.Add(PageCard(actions, s.Name,
                $"{ModelName(s.Model)} · saved {s.Saved.ToLocalTime():d MMM, HH:mm} · {(away == 0 ? "all default" : $"{away} change{(away == 1 ? "" : "s")} from default")}" + (differ == 0 ? " · what you have now" : $" · {differ} differ from now")));
        }
        if (!setups.Healthy) setupsList.Children.Add(Wrap("The setups file couldn't be read, so nothing is saved over it.", 12.5, "DS.Warn", 6));

        // history
        historyList.Children.Clear();
        if (history.Steps.Count == 0) historyList.Children.Add(Wrap("Nothing changed yet. Each change I make is listed here, with the way back to before it.", 13.5, "DS.TextMuted"));
        else
        {
            var all = Small($"Undo all {history.Steps.Count}"); int first = history.Steps[0].Id; all.Click += (_, _) => { GoBack(first); RefreshPages(); }; register(all);
            historyList.Children.Add(new DockPanel { Margin = new(0, 0, 0, 10), Children = { all.Also(a => { DockPanel.SetDock(a, Dock.Right); }), Kit.Text($"{history.Steps.Count} change{(history.Steps.Count == 1 ? "" : "s")} this session", 13, "DS.TextSoft").Also(t => t.VerticalAlignment = VerticalAlignment.Center) } });
        }
        int n = history.Steps.Count;
        foreach (var step in history.Steps.Reverse())
        {
            int id = step.Id;
            var back = Small(step == history.Latest ? "Undo" : "Back to before this"); back.Click += (_, _) => { GoBack(id); RefreshPages(); }; register(back);
            var detail = new StackPanel();
            foreach (var c in step.Changes.Take(4)) detail.Children.Add(Wrap($"{c.What}   {c.Before} → {c.After}", 12.5, "DS.AccentHi", 2));
            if (step.Changes.Count > 4) detail.Children.Add(Wrap($"and {step.Changes.Count - 4} more", 12, "DS.TextMuted", 2));
            var card = PageCard(back, $"{n--}. “{Short(step.Asked)}”", null);
            ((StackPanel)((DockPanel)card.Child).Children[1]).Children.Add(detail);
            historyList.Children.Add(card);
        }

        // badges
        Badge(View.Setups, setups.Items.Count); Badge(View.History, history.Steps.Count);
        RefreshStrip();

        void Badge(View v, int count)
        {
            if (!badges.TryGetValue(v, out var b)) return;
            b.Badge.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed; b.Count.Text = count > 99 ? "99+" : count.ToString();
        }
    }

    static Border PageCard(FrameworkElement action, string title, string? detail)
    {
        var row = new DockPanel();
        action.VerticalAlignment = VerticalAlignment.Top; action.Margin = new(12, 0, 0, 0);
        DockPanel.SetDock(action, Dock.Right); row.Children.Add(action);
        var words = new StackPanel { Children = { Wrap(title, 14.5, "DS.Text").Also(t => t.FontWeight = FontWeights.SemiBold) } };
        if (detail != null) words.Children.Add(Wrap(detail, 12.5, "DS.TextMuted", 3));
        row.Children.Add(words);
        return new Border { CornerRadius = new(12), Padding = new(14, 11, 12, 12), Margin = new(0, 0, 0, 8), BorderThickness = new(1), BorderBrush = new SolidColorBrush(Line), Background = new SolidColorBrush(Card), Child = row };
    }

    // ---- Look: the agent's colour, the pad's colour, the eyes ----
    static readonly (string Name, Color? Colour)[] Wearable =
    [
        ("Own green", null), ("Ice", ThemeCatalog.R(Color.FromRgb(150, 220, 255))), ("Blue", ThemeCatalog.R(Color.FromRgb(40, 110, 255))), ("Purple", ThemeCatalog.R(Color.FromRgb(160, 80, 255))),
        ("Pink", ThemeCatalog.R(Color.FromRgb(255, 90, 180))), ("Red", ThemeCatalog.R(Color.FromRgb(255, 40, 40))), ("Orange", ThemeCatalog.R(Color.FromRgb(255, 130, 20))), ("Yellow", ThemeCatalog.R(Color.FromRgb(255, 220, 0))), ("White", Colors.White),
    ];
    void BuildLook()
    {
        lookPage.Children.Clear();
        Section("THE AGENT'S COLOUR", "Its eyes, the rings round them and its light bars. Asking for a lights colour in the chat changes it too.");
        var colours = new WrapPanel();
        foreach (var (name, colour) in Wearable)
        {
            var c = colour ?? Green; bool on = worn is Color w ? colour is Color k && k == w : colour == null;
            colours.Children.Add(Swatch(c, name, on, () => { Wear(colour); RefreshStrip(); BuildLook(); }));
        }
        lookPage.Children.Add(colours);
        string named = Wearable.FirstOrDefault(x => x.Colour is Color k && worn is Color w && k == w).Name ?? "";
        if (named.Length > 0 && named != "Own green")
        {
            var onPad = Small($"Put {named.ToLowerInvariant()} on the controller's lights too");
            onPad.Click += async (_, _) => { if (!busy) await Ask($"change my lights to {named.ToLowerInvariant()}"); }; register(onPad);
            lookPage.Children.Add(onPad.Also(b => { b.Margin = new(0, 4, 0, 0); b.HorizontalAlignment = HorizontalAlignment.Left; }));
        }

        string model = FinishModel(modelId), finish = PadColours.For(player, model);
        Section("YOUR PAD'S COLOUR", "Which colour your pad is. The handheld round this screen, and the pad everywhere in the app, follow it.");
        var finishes = new WrapPanel();
        foreach (var f in PadColours.Catalog.For(model))
        {
            var id = f.Id;
            finishes.Children.Add(Swatch((Color)ColorConverter.ConvertFromString(f.Swatch), f.Name, id == finish, () => PadColours.Set(player, model, id)));
        }
        lookPage.Children.Add(finishes);

        Section("ITS EYES", "The shape of the LED on each stick.");
        var shapes = new WrapPanel();
        foreach (var style in new[] { "Pill", "Round", "Slit" })
        {
            var look = new Border { CornerRadius = new(15), Height = 32, Padding = new(14, 0, 14, 0), Margin = new(0, 0, 8, 8), BorderThickness = new(1),
                BorderBrush = new SolidColorBrush(style == eyeStyle ? Amber : Line), Background = new SolidColorBrush(Card),
                Child = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Children =
                {
                    new Sh.Rectangle { Width = EyeSize(style).W / 3, Height = EyeSize(style).H / 3, RadiusX = EyeSize(style).R / 3, RadiusY = EyeSize(style).R / 3, Fill = new SolidColorBrush(Rest), Margin = new(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center },
                    Kit.Text(style, 13, style == eyeStyle ? "DS.Text" : "DS.TextSoft", FontWeights.SemiBold),
                } } };
            var s = style; var b = PadKey(look, style + " eyes", () => { SetEyeStyle(s); BuildLook(); }); register(b); shapes.Children.Add(b);
        }
        lookPage.Children.Add(shapes);

        void Section(string title, string about)
        {
            lookPage.Children.Add(Kit.Text(title, 12, "DS.TextMuted", FontWeights.SemiBold).Also(t => t.Margin = new(0, lookPage.Children.Count == 0 ? 0 : 18, 0, 2)));
            lookPage.Children.Add(Wrap(about, 12.5, "DS.TextMuted", 0).Also(t => t.Margin = new(0, 0, 0, 10)));
        }
        Button Swatch(Color c, string name, bool on, Action act)
        {
            var look = new Border { CornerRadius = new(15), Height = 32, Padding = new(6, 0, 12, 0), Margin = new(0, 0, 8, 8), BorderThickness = new(on ? 2 : 1),
                BorderBrush = new SolidColorBrush(on ? Amber : Line), Background = new SolidColorBrush(Card),
                Child = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Children =
                {
                    new Border { Width = 20, Height = 20, CornerRadius = new(10), Margin = new(0, 0, 8, 0), Background = new SolidColorBrush(c), BorderThickness = new(1), BorderBrush = new SolidColorBrush(Color.FromArgb(110, 255, 255, 255)) },
                    Kit.Text(name, 13, on ? "DS.Text" : "DS.TextSoft", FontWeights.SemiBold).Also(t => t.VerticalAlignment = VerticalAlignment.Center),
                } } };
            var b = PadKey(look, name, act); register(b); return b;
        }
    }

    static string eyeStyle = ReadEyeStyle();
    static (double W, double H, double R) EyeSize(string style) => style switch { "Round" => (40, 40, 20), "Slit" => (16, 60, 8), _ => (30, 54, 15) };
    static string ReadEyeStyle()
    {
        try { return File.Exists(LookFile) && File.ReadAllLines(LookFile) is { Length: > 1 } lines && lines[1].Trim() is "Round" or "Slit" or "Pill" ? lines[1].Trim() : "Pill"; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { return "Pill"; }
    }
    void SetEyeStyle(string style)
    {
        eyeStyle = style; ApplyEyeStyle(); SaveLook();
    }
    void ApplyEyeStyle()
    {
        var (w, h, r) = EyeSize(eyeStyle);
        foreach (var p in pupils) { p.Width = w; p.Height = h; p.RadiusX = p.RadiusY = r; }
    }
    /// <summary>The look kept between runs: the worn colour on the first line ("-" for its own), the eye shape on the second.</summary>
    static void SaveLook()
    {
        if (ReviewSandbox.Active && !LookFileChosen) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LookFile)!);
            if (worn == null && eyeStyle == "Pill") File.Delete(LookFile);
            else File.WriteAllLines(LookFile, [worn is Color w ? Hex(w) : "-", eyeStyle]);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
    }

    // ---- Live: the controller as it is right now ----
    readonly System.Windows.Threading.DispatcherTimer liveScreen = new() { Interval = TimeSpan.FromMilliseconds(33) };
    X20Ctl.Product.Scanner.WindowsReader? padReader; int padSlot = -1;
    Ellipse2? leftDot, rightDot; Border? ltFill, rtFill; TextBlock? liveSays, liveNumbers;
    readonly Dictionary<ushort, Border> liveKeys = [];
    sealed record Ellipse2(Sh.Ellipse Dot, double Box);

    ScrollViewer BuildLive()
    {
        var page = new StackPanel { Margin = new(0, 0, 8, 0) };
        page.Children.Add(Kit.Text("Live", 20, "DS.Text", FontWeights.SemiBold));
        page.Children.Add(Wrap("What your controller is doing right now. Nothing is written to it.", 13, "DS.TextMuted", 3));
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 14, 0, 0) };
        Ellipse2 Stick(string label)
        {
            const double S = 104;
            var box = new Canvas { Width = S, Height = S, ClipToBounds = true };
            box.Children.Add(new Sh.Ellipse { Width = S, Height = S, Stroke = new SolidColorBrush(Line), StrokeThickness = 1.5, Fill = new SolidColorBrush(ThemeCatalog.R(Color.FromRgb(15, 18, 23))) });
            box.Children.Add(new Sh.Line { X1 = S / 2, X2 = S / 2, Y1 = 6, Y2 = S - 6, Stroke = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)) });
            box.Children.Add(new Sh.Line { Y1 = S / 2, Y2 = S / 2, X1 = 6, X2 = S - 6, Stroke = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)) });
            var dot = new Sh.Ellipse { Width = 14, Height = 14, Fill = new SolidColorBrush(Amber), Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Amber, BlurRadius = 12, ShadowDepth = 0 } };
            Canvas.SetLeft(dot, S / 2 - 7); Canvas.SetTop(dot, S / 2 - 7); box.Children.Add(dot);
            row.Children.Add(new StackPanel { Margin = new(0, 0, 18, 0), Children = { box, Kit.Text(label, 12, "DS.TextMuted").Also(t => { t.HorizontalAlignment = HorizontalAlignment.Center; t.Margin = new(0, 6, 0, 0); }) } });
            return new(dot, S);
        }
        Border Trigger(string label)
        {
            var fill = new Border { Height = 0, VerticalAlignment = VerticalAlignment.Bottom, CornerRadius = new(5), Background = new SolidColorBrush(Amber) };
            var bar = new Border { Width = 22, Height = 104, CornerRadius = new(6), BorderThickness = new(1), BorderBrush = new SolidColorBrush(Line), Background = new SolidColorBrush(ThemeCatalog.R(Color.FromRgb(15, 18, 23))), Child = fill };
            row.Children.Add(new StackPanel { Margin = new(0, 0, 12, 0), Children = { bar, Kit.Text(label, 12, "DS.TextMuted").Also(t => { t.HorizontalAlignment = HorizontalAlignment.Center; t.Margin = new(0, 6, 0, 0); }) } });
            return fill;
        }
        leftDot = Stick("Left stick"); rightDot = Stick("Right stick"); ltFill = Trigger("LT"); rtFill = Trigger("RT");
        // the buttons, each lit while it is held
        var keys = new WrapPanel { Width = 170, Margin = new(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Top };
        foreach (var (flag, label) in new (ushort, string)[] { (0x1000, "A"), (0x2000, "B"), (0x4000, "X"), (0x8000, "Y"), (0x0100, "LB"), (0x0200, "RB"), (0x0020, "View"), (0x0010, "Menu"), (0x0040, "L3"), (0x0080, "R3"), (0x0001, "↑"), (0x0002, "↓"), (0x0004, "←"), (0x0008, "→") })
        {
            var key = new Border { MinWidth = 36, Height = 26, Padding = new(6, 0, 6, 0), Margin = new(0, 0, 5, 5), CornerRadius = new(7), BorderThickness = new(1), BorderBrush = new SolidColorBrush(Line), Background = new SolidColorBrush(Card),
                Child = Kit.Text(label, 11.5, "DS.TextSoft", FontWeights.SemiBold).Also(t => { t.HorizontalAlignment = HorizontalAlignment.Center; t.VerticalAlignment = VerticalAlignment.Center; }) };
            liveKeys[flag] = key; keys.Children.Add(key);
        }
        row.Children.Add(keys);
        page.Children.Add(row);
        liveNumbers = Kit.Text("", 12.5, "DS.TextMuted").Also(t => t.Margin = new(0, 12, 0, 0));
        liveSays = Wrap("", 14, "DS.TextSoft", 6);
        page.Children.Add(liveNumbers); page.Children.Add(liveSays);
        var checkup = Small("Run a check-up"); checkup.Click += (_, _) => ShowView(View.Checkup); register(checkup);
        page.Children.Add(checkup.Also(c => { c.Margin = new(0, 12, 0, 0); c.HorizontalAlignment = HorizontalAlignment.Left; }));
        liveScreen.Tick += (_, _) => LiveScreenTick();
        return new ScrollViewer { Content = page, Visibility = Visibility.Collapsed, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    void LiveScreen(bool on)
    {
        if (on && IsShown)
        {
            if (LiveStick == null && padReader == null) { padReader = OpenReader(); padSlot = -1; FindPad(); }
            LiveScreenTick(); liveScreen.Start();
        }
        else { liveScreen.Stop(); padReader?.Dispose(); padReader = null; padSlot = -1; }
    }

    // which slot the pad is in is asked off the screen's thread, and asked again every second while there is none, so a
    // controller plugged in with this page open shows up by itself
    bool findingPad; DateTime padLooked;
    async void FindPad()
    {
        if (findingPad || padReader == null) return;
        findingPad = true; padLooked = DateTime.UtcNow;
        int slot = await Task.Run(() => XInputDriftMeter.Slot(player));
        findingPad = false;
        if (padReader != null) padSlot = slot;
    }

    void LiveScreenTick()
    {
        if (LiveStick == null && padReader != null && padSlot < 0 && (DateTime.UtcNow - padLooked).TotalSeconds >= 1) FindPad();
        short lx, ly, rx, ry; byte lt = 0, rt = 0; ushort held = 0;
        if (LiveStick?.Invoke() is { } fake) (lx, ly, rx, ry) = fake;
        else if (padReader is { } r && padSlot >= 0 && r.Read(padSlot, out var s)) { (lx, ly, rx, ry, lt, rt, held) = (s.Pad.LX, s.Pad.LY, s.Pad.RX, s.Pad.RY, s.Pad.LT, s.Pad.RT, s.Pad.Buttons); }
        else
        {
            padSlot = -1;   // it was unplugged: look for it again
            if (liveSays != null) liveSays.Text = "I can't read a controller right now. Plug it in or connect it, and this fills in by itself.";
            if (liveNumbers != null) liveNumbers.Text = "";
            return;
        }
        static double P(short v) => Math.Clamp(v / 32767.0, -1, 1);
        void Put(Ellipse2? d, short x, short y) { if (d == null) return; double half = d.Box / 2 - 9; Canvas.SetLeft(d.Dot, d.Box / 2 - 7 + P(x) * half); Canvas.SetTop(d.Dot, d.Box / 2 - 7 - P(y) * half); }
        Put(leftDot, lx, ly); Put(rightDot, rx, ry);
        if (ltFill != null) ltFill.Height = 102 * lt / 255.0; if (rtFill != null) rtFill.Height = 102 * rt / 255.0;
        foreach (var (flag, key) in liveKeys)
        {
            bool down = (held & flag) != 0;
            key.Background = new SolidColorBrush(down ? Amber : Card);
            if (key.Child is TextBlock t) t.Foreground = down ? new SolidColorBrush(ThemeCatalog.R(Color.FromRgb(24, 20, 8))) : DS.Brush("DS.TextSoft");
        }
        double l = Math.Sqrt(P(lx) * P(lx) + P(ly) * P(ly)) * 100, rr = Math.Sqrt(P(rx) * P(rx) + P(ry) * P(ry)) * 100;
        if (liveNumbers != null) liveNumbers.Text = $"Left stick {l:0}% · Right stick {rr:0}% · LT {lt * 100 / 255}% · RT {rt * 100 / 255}%";
        if (liveSays != null)
            liveSays.Text = held != 0 || lt > 10 || rt > 10 || l > 25 || rr > 25 ? "I see it. Everything you press shows up here."
                : l > 8 || rr > 8 ? $"Hands off, your {(l >= rr ? "left" : "right")} stick still sits {Math.Max(l, rr):0}% off centre. That's drift: a check-up measures it properly."
                : "Both sticks rest in the middle. Move one, or press a button.";
    }

    /// <summary>A review run opens a page by name: Chat, Ideas, Setups, History, Live, Checkup or Look.</summary>
    internal void ReviewShow(string page) { if (Enum.TryParse<View>(page, out var view)) ShowView(view); }
}
