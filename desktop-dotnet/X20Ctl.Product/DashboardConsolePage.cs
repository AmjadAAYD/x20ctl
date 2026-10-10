using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using X20Ctl.Zone;
namespace X20Ctl.Product;

/// <summary>
/// The Studio landing page (owner request 10 Oct 2026): a tilted, floating controller hero that opens
/// Buttons when selected, a status/headline block with the controller pill and stats, a featured-game
/// banner, and the installed-games shelf found on this PC. Fully navigable with D-pad / arrows and A.
/// </summary>
public sealed class DashboardConsolePage : Grid
{
    private static Task<IReadOnlyList<GameEntry>>? scan;
    private readonly StackPanel shelf = new() { Orientation = Orientation.Horizontal };
    private readonly ScrollViewer shelfScroll;
    private readonly Grid featured = new() { ClipToBounds = true };
    private readonly Image heroArt = new() { Stretch = Stretch.UniformToFill, HorizontalAlignment = HorizontalAlignment.Right, Opacity = 0 };
    private readonly Image logo = new() { Stretch = Stretch.Uniform, MaxHeight = 86, MaxWidth = 380, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly TextBlock featuredTitle = Kit.Text("", 34, "DS.Text", FontWeights.SemiBold), featuredMeta = Kit.Text("", 16, "DS.TextSoft");
    private readonly TextBlock gamesCount; private readonly Button play;
    private readonly List<(Button Card, GameEntry Game)> cards = new();
    private readonly bool animate;
    private GameEntry? current;
    public event Action? ConfigureRequested;
    private Button? heroFocus;
    /// <summary>What A does on the highlighted item, for the live footer.</summary>
    public string FocusAction => heroFocus?.IsKeyboardFocused == true ? "Configure" : play.IsKeyboardFocused || cards.Any(c => c.Card.IsKeyboardFocusWithin) ? "Play" : "Select";
    /// <summary>Y: scan the launchers again.</summary>
    public async void Rescan() { scan = null; foreach (var (card, _) in cards) shelf.Children.Remove(card); cards.Clear(); shelf.Children.Clear(); gamesCount.Text = "…"; await Populate(); }
    /// <summary>LT/RT: jump a page (five covers) along the shelf.</summary>
    public void PageShelf(int step) { if (cards.Count == 0) return; int i = Math.Max(0, cards.FindIndex(c => c.Card.IsKeyboardFocusWithin)); cards[Math.Clamp(i + step * 5, 0, cards.Count - 1)].Card.Focus(); }
    public ControllerShowcase Showcase { get; } = new() { Tilt = 0 };
    private readonly List<(Border art, TextBlock title)> covers = new();
    private double coverHeight = FullCover;
    // tuned for the fixed 1600 x 900 design: hero 250, featured 168, a shelf of 190-pixel square games
    private const double FullCover = 190, FullFeatured = 168, FullTop = 250;

    public DashboardConsolePage(ControllerModel model, int player, int draftChanges, int savedSetups, bool reducedMotion)
    {
        animate = !reducedMotion && DS.Motion;
        ClipToBounds = true; // in a short window nothing may spill under the Studio footer
        RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star), MinHeight = FullTop }); RowDefinitions.Add(new() { Height = new(12) });
        RowDefinitions.Add(new() { Height = new(FullFeatured) }); RowDefinitions.Add(new() { Height = new(12) }); RowDefinitions.Add(new() { Height = GridLength.Auto });

        // ---- top: controller hero (left) + status block (right) ----
        var top = new Grid(); top.ColumnDefinitions.Add(new() { Width = new(.95, GridUnitType.Star) }); top.ColumnDefinitions.Add(new() { Width = new(1.05, GridUnitType.Star) }); Children.Add(top);
        Showcase.Show(model.Id, reducedMotion);
        var heroButton = new Button { Content = new Grid { Children = { new Viewbox { Child = Showcase } } }, Template = Bare(), Cursor = Cursors.Hand, ToolTip = "Configure this controller (opens Buttons)", FocusVisualStyle = null };
        heroButton.Click += (_, _) => ConfigureRequested?.Invoke();
        var hint = new Border { CornerRadius = new(14), Padding = new(14, 6, 14, 6), Background = new SolidColorBrush(Color.FromArgb(150, 8, 14, 32)), BorderBrush = new SolidColorBrush(Color.FromArgb(70, 140, 180, 255)), BorderThickness = new(1), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom, Margin = new(24, 0, 0, 6),
            Child = GlyphHint.Footer(("A", "Configure buttons")) };
        heroButton.GotKeyboardFocus += (_, _) => hint.BorderBrush = Brushes.White; heroButton.LostKeyboardFocus += (_, _) => hint.BorderBrush = new SolidColorBrush(Color.FromArgb(70, 140, 180, 255));
        heroFocus = heroButton; top.Children.Add(new Grid { Children = { heroButton } });

        var status = new StackPanel { Width = 560 };
        status.Children.Add(new StatusChip("Controller offline", ChipKind.Warn));
        status.Children.Add(new TextBlock { Text = "Ready to play", Style = DS.Style("DS.Display"), FontWeight = FontWeights.Light, FontSize = 48, Margin = new(0, 10, 0, 8) });
        var pill = new Border { CornerRadius = new(17), Height = 34, Padding = new(14, 0, 16, 0), Background = new SolidColorBrush(Color.FromArgb(34, 255, 255, 255)), BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)), BorderThickness = new(1), HorizontalAlignment = HorizontalAlignment.Left,
            Child = new StackPanel { Orientation = Orientation.Horizontal, Children = { Kit.Icon("", 16, "DS.Text").Also(i => i.Margin = new(0, 0, 10, 0)), Kit.Text($"{model.Name} · Player {player + 1}", 14.5) } } };
        status.Children.Add(pill);
        status.Children.Add(Kit.Text("Select the controller to configure buttons, curves and macros.", 14, "DS.TextSoft").Also(t => t.Margin = new(2, 10, 0, 14)));
        var tiles = new System.Windows.Controls.Primitives.UniformGrid { Rows = 1, Margin = new(0, 0, -12, 0) };
        var games = new StatTile("", "…", "Games") { Margin = new(0, 0, 12, 0) }; gamesCount = games.Value; tiles.Children.Add(games);
        tiles.Children.Add(new StatTile("", draftChanges.ToString(), "Changes") { Margin = new(0, 0, 12, 0) });
        tiles.Children.Add(new StatTile("", savedSetups.ToString(), "Setups") { Margin = new(0, 0, 12, 0) });
        status.Children.Add(tiles);
        // the status block keeps its proportions and scales down when the window is short or narrow, rather than being cut off
        var statusBox = new Viewbox { Child = status, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, Margin = new(30, 6, 0, 6) };
        Grid.SetColumn(statusBox, 1); top.Children.Add(statusBox);

        // ---- middle: featured game banner ----
        featured.Children.Add(new Border { CornerRadius = new(20), Background = new LinearGradientBrush(Color.FromArgb(220, 14, 26, 60), Color.FromArgb(200, 8, 14, 34), 0) });
        var artHost = new Border { CornerRadius = new(20), ClipToBounds = true, Child = heroArt };
        artHost.OpacityMask = new LinearGradientBrush(new GradientStopCollection { new(Colors.Transparent, 0), new(Color.FromArgb(120, 0, 0, 0), .32), new(Colors.White, .62) }, new Point(0, .5), new Point(1, .5));
        featured.Children.Add(artHost);
        featured.Children.Add(new Border { CornerRadius = new(20), BorderThickness = new(1), BorderBrush = new LinearGradientBrush(Color.FromArgb(110, 120, 170, 255), Color.FromArgb(30, 120, 170, 255), 90) });
        play = new Button { Style = DS.Style("DS.ActionPrimary"), Width = 200, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new(0, 0, 30, 26), Content = new StackPanel { Orientation = Orientation.Horizontal, Children = { Kit.Icon("", 17, "DS.Text").Also(i => i.Margin = new(0, 0, 12, 0)), Kit.Text("Play", 16, "DS.Text", FontWeights.SemiBold) } } };
        play.Click += (_, _) => { if (current != null) GameLibrary.Launch(current); };
        // the banner: the game's logo and when it was last played, large on the left; Play in blue at the bottom right
        var info = new StackPanel { Width = 520,
            Children = { Kit.Text("FEATURED", 12.5, "DS.AccentHi", FontWeights.SemiBold).Also(t => t.Margin = new(0, 0, 0, 8)), logo, featuredTitle, featuredMeta.Also(t => t.Margin = new(0, 8, 0, 0)) } };
        // like the status block, the banner's words and Play button scale down with a short banner instead of being cut
        featured.Children.Add(new Viewbox { Child = info, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, Margin = new(36, 10, 0, 10) });
        featured.Children.Add(play);
        Grid.SetRow(featured, 2); Children.Add(featured);

        // ---- bottom: games shelf ----
        shelfScroll = new ScrollViewer { Content = shelf, HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = false, Padding = new(4, 6, 4, 6) };
        var shelfPanel = new DockPanel { Children = { new DockPanel { Margin = new(2, 0, 0, 8), Children = { Kit.Text("Your games", 19, "DS.Text", FontWeights.SemiBold) } }.Also(d => DockPanel.SetDock(d, Dock.Top)), shelfScroll } };
        Grid.SetRow(shelfPanel, 4); Children.Add(shelfPanel);
        Feature(null);
        Loaded += async (_, _) => await Populate();
        SizeChanged += (_, e) => Fit(e.NewSize.Height);
        PreviewKeyDown += OnKey;
    }

    /// <summary>Fit the page to its height. Full size needs about 730 px; below that the featured banner gives way first,
    /// then the game covers, then the hero row, so every part stays whole and nothing spills under the footer.</summary>
    private void Fit(double height)
    {
        if (height <= 0) return;
        double Shelf(double cover) => cover + 64; // "Your games", padding, cover and its title
        double deficit = Math.Max(0, FullTop + 12 + FullFeatured + 12 + Shelf(FullCover) - height);
        double featured = FullFeatured - Math.Min(76, deficit); deficit -= FullFeatured - featured;
        double cover = FullCover - Math.Min(88, deficit); deficit -= FullCover - cover;
        double top = FullTop - Math.Min(100, deficit);
        RowDefinitions[0].MinHeight = top; RowDefinitions[2].Height = new(featured);
        if (Math.Abs(cover - coverHeight) > .5) { coverHeight = cover; foreach (var (art, title) in covers) SizeCover(art, title); }
    }
    private void SizeCover(Border art, TextBlock title)
    {
        double w = Math.Round(coverHeight), h = Math.Round(coverHeight); // big square tiles
        art.Width = w; art.Height = h; art.Clip = new RectangleGeometry(new Rect(0, 0, w, h), 14, 14); title.Width = w;
    }

    /// <summary>Moves keyboard/gamepad focus to the hero so the page is immediately navigable.</summary>
    public void FocusHero() => Dispatcher.BeginInvoke(() => (cards.FirstOrDefault().Card ?? (UIElement)play).Focus(), System.Windows.Threading.DispatcherPriority.Input);

    private async Task Populate()
    {
        if (cards.Count > 0) return;
        scan ??= GameLibrary.ScanAsync();
        IReadOnlyList<GameEntry> list; try { list = await scan; } catch { list = []; }
        gamesCount.Text = list.Count.ToString();
        if (list.Count == 0) { shelf.Children.Add(Kit.Info("No installed Steam or Epic games were found on this PC.")); return; }
        int i = 0;
        foreach (var g in list) { var card = Card(g); shelf.Children.Add(card); cards.Add((card, g)); if (animate) Enter(card, i++ * 35); }
        Feature(list[0]);
    }

    private Button Card(GameEntry g)
    {
        UIElement face;
        var cover = Bitmap(g.Cover);
        if (cover != null) face = new Image { Source = cover, Stretch = Stretch.UniformToFill };
        else
        {
            string initials = string.Concat(g.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => char.IsLetterOrDigit(w[0])).Take(2).Select(w => char.ToUpperInvariant(w[0])));
            int hue = Math.Abs(g.Name.GetHashCode()) % 360; var c = Hsl(hue, .45, .28);
            face = new Grid { Background = new LinearGradientBrush(c, Hsl(hue, .5, .16), 90), Children = { Kit.Text(initials, 40, "DS.Text").Also(t => { t.HorizontalAlignment = HorizontalAlignment.Center; t.Opacity = .85; }) } };
        }
        var art = new Border { CornerRadius = new(14), ClipToBounds = true, Child = face, BorderThickness = new(3), BorderBrush = Brushes.Transparent };
        var store = new Border { CornerRadius = new(9), Padding = new(8, 2, 8, 3), Background = new SolidColorBrush(Color.FromArgb(200, 6, 10, 22)), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new(0, 0, 10, 10), Child = Kit.Text(g.Store, 11.5, "DS.TextSoft", FontWeights.SemiBold) };
        var title = Kit.Text(g.Name, 14, "DS.TextSoft"); title.Margin = new(2, 6, 0, 0);
        covers.Add((art, title)); SizeCover(art, title);
        var stack = new StackPanel { Children = { new Grid { Children = { art, store } }, title }, RenderTransformOrigin = new(.5, .4), RenderTransform = new ScaleTransform(1, 1) };
        var b = new Button { Content = stack, Template = Bare(), Margin = new(0, 0, 18, 0), Cursor = Cursors.Hand, FocusVisualStyle = null, ToolTip = $"{g.Name} · {g.Store}" };
        b.GotKeyboardFocus += (_, _) => { Feature(g); art.BorderBrush = Brushes.White; art.Effect = new DropShadowEffect { Color = Kit.Blue, BlurRadius = 26, ShadowDepth = 0, Opacity = .9 }; Lift(stack, 1.06); title.Foreground = DS.Brush("DS.Text"); b.BringIntoView(new Rect(-40, 0, coverHeight + 100, coverHeight + 70)); };
        b.LostKeyboardFocus += (_, _) => { art.BorderBrush = Brushes.Transparent; art.Effect = null; Lift(stack, 1); title.Foreground = DS.Brush("DS.TextSoft"); };
        b.MouseEnter += (_, _) => Feature(g);
        b.Click += (_, _) => GameLibrary.Launch(g);
        return b;
    }

    private void Feature(GameEntry? g)
    {
        if (g != null && ReferenceEquals(g, current)) return; current = g;
        var hero = Bitmap(g?.Hero); var mark = Bitmap(g?.Logo);
        if (animate && heroArt.Source != hero) { heroArt.BeginAnimation(OpacityProperty, null); heroArt.Opacity = 0; }
        heroArt.Source = hero; if (hero != null) { if (animate) heroArt.BeginAnimation(OpacityProperty, DS.To(1, 320)); else heroArt.Opacity = 1; }
        logo.Source = mark; logo.Visibility = mark != null ? Visibility.Visible : Visibility.Collapsed; logo.Margin = new(0, 8, 0, 6);
        featuredTitle.Text = g?.Name ?? "Your library"; featuredTitle.Visibility = mark == null ? Visibility.Visible : Visibility.Collapsed;
        featuredMeta.Text = g == null ? "Scanning installed Steam and Epic games…" : $"{g.Store} · {(g.LastPlayed is { } t ? "Last played " + t.ToString("d MMM yyyy") : "Installed")}";
        play.IsEnabled = g != null;
    }

    private void OnKey(object sender, KeyEventArgs e)
    {
        int index = cards.FindIndex(c => c.Card.IsKeyboardFocusWithin);
        if (e.Key is Key.Right or Key.Left && index >= 0)
        {
            int next = Math.Clamp(index + (e.Key == Key.Right ? 1 : -1), 0, cards.Count - 1); cards[next].Card.Focus(); e.Handled = true;
        }
        else if (e.Key is Key.Enter or Key.A && Keyboard.FocusedElement is Button focused && !e.IsRepeat) { focused.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); e.Handled = true; }
    }

    private void Lift(FrameworkElement e, double to)
    {
        // a transform shared from a style is frozen; give the element its own before animating it
        if (e.RenderTransform is not ScaleTransform s || s.IsFrozen || s.IsSealed) { s = new ScaleTransform(e.RenderTransform is ScaleTransform old ? old.ScaleX : 1, e.RenderTransform is ScaleTransform o2 ? o2.ScaleY : 1); e.RenderTransform = s; e.RenderTransformOrigin = new(.5, .5); }
        if (!animate) { s.ScaleX = s.ScaleY = to; return; }
        s.BeginAnimation(ScaleTransform.ScaleXProperty, DS.To(to, 160)); s.BeginAnimation(ScaleTransform.ScaleYProperty, DS.To(to, 160));
    }
    private static void Enter(FrameworkElement e, int delay)
    {
        e.Opacity = 0; var t = new TranslateTransform(0, 18); e.RenderTransform = t;
        e.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(260)) { BeginTime = TimeSpan.FromMilliseconds(delay) });
        t.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(320)) { BeginTime = TimeSpan.FromMilliseconds(delay), EasingFunction = DS.EaseOut });
    }
    private static BitmapImage? Bitmap(string? path)
    {
        if (path == null || !File.Exists(path)) return null;
        try { var b = new BitmapImage(); b.BeginInit(); b.UriSource = new Uri(path); b.CacheOption = BitmapCacheOption.OnLoad; b.DecodePixelWidth = 900; b.EndInit(); b.Freeze(); return b; } catch { return null; }
    }
    private static Color Hsl(double h, double s, double l)
    {
        double c = (1 - Math.Abs(2 * l - 1)) * s, x = c * (1 - Math.Abs(h / 60 % 2 - 1)), m = l - c / 2;
        var (r, g, b) = h < 60 ? (c, x, 0d) : h < 120 ? (x, c, 0d) : h < 180 ? (0d, c, x) : h < 240 ? (0d, x, c) : h < 300 ? (x, 0d, c) : (c, 0d, x);
        return Color.FromRgb((byte)((r + m) * 255), (byte)((g + m) * 255), (byte)((b + m) * 255));
    }
    private static ControlTemplate Bare() { var t = new ControlTemplate(typeof(Button)) { VisualTree = new FrameworkElementFactory(typeof(ContentPresenter)) }; t.Seal(); return t; }
}
