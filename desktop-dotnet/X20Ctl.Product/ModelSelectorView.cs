using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using X20Ctl.Zone;
namespace X20Ctl.Product;

/// <summary>"Choose a controller" for one player, in the Zone's Steam restraint: all seven models as cards in a grid
/// (four, then three centred), each with its art, name, a short description built from the catalog's hardware facts and
/// an honest support line. Cards rise in one after another; focus lifts a card with a white edge. Arrows / D-pad move,
/// A / Enter chooses, B / Esc goes back.</summary>
public sealed class ModelSelectorView : Grid
{
    public event Action<ControllerModel>? Chosen;
    public event Action? Cancelled;
    public int Player { get; }
    private readonly Dictionary<string, Button> choices = new();
    private readonly List<Button> cards = new();
    public void ChooseModel(string id) => choices[id].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static readonly Color Accent = DockView.Accent;

    public ModelSelectorView(ControllerCatalog catalog, int player)
    {
        Player = player;
        Children.Add(new FacetBackdrop());
        var facts = ReadFacts();

        // window chrome, as on every full screen
        var chrome = new DockPanel { Height = 64, VerticalAlignment = VerticalAlignment.Top, Margin = new(32, 0, 14, 0) };
        var commands = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        foreach (var (glyph, label) in new[] { ("", "Minimize"), ("", "Maximize or restore"), ("", "Close") })
        {
            var command = new Button { Content = glyph, Style = (Style)FindResource("ChromeAction") };
            System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(command, true);
            System.Windows.Automation.AutomationProperties.SetName(command, label);
            command.Click += (_, _) => { if (Window.GetWindow(this) is not Window window) return; if (label == "Close") window.Close(); else if (label == "Minimize") SystemCommands.MinimizeWindow(window); else window.WindowState = window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; };
            commands.Children.Add(command);
        }
        DockPanel.SetDock(commands, Dock.Right); chrome.Children.Add(commands);
        var brand = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        brand.Children.Add(new Image { Source = new BitmapImage(new Uri("pack://application:,,,/X20Ctl;component/Assets/icon.png")), Width = 40, Height = 40 });
        brand.Children.Add(new TextBlock { Text = "X20CTL", FontFamily = DS.Display, FontSize = 22, FontWeight = FontWeights.Bold, Margin = new(18, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center, Foreground = DS.Brush("DS.Text") });
        brand.Children.Add(new TextBlock { Text = "Choose a controller", FontFamily = DS.Display, FontSize = 17, Foreground = DS.Brush("DS.TextSoft"), VerticalAlignment = VerticalAlignment.Center });
        chrome.Children.Add(brand); Children.Add(chrome);
        Children.Add(new Border { Height = 0 });

        var body = new Grid { Margin = new(47, 94, 47, 0) };
        body.RowDefinitions.Add(new() { Height = GridLength.Auto }); body.RowDefinitions.Add(new()); body.RowDefinitions.Add(new() { Height = new(56) });
        Children.Add(body);
        var header = new DockPanel { Margin = new(9, 0, 9, 26) };
        var back = new Button { Content = "Back", Style = (Style)FindResource("ActionButton"), MinHeight = 38, Height = 38, Padding = new(18, 0, 18, 0), FontSize = 14.5, VerticalAlignment = VerticalAlignment.Center };
        back.Click += (_, _) => Cancelled?.Invoke(); DockPanel.SetDock(back, Dock.Right); header.Children.Add(back);
        var title = new StackPanel();
        title.Children.Add(new Border { CornerRadius = new(11), Height = 30, Padding = new(10, 0, 10, 0), HorizontalAlignment = HorizontalAlignment.Left, Background = new SolidColorBrush(Color.FromArgb(28, 255, 255, 255)), BorderBrush = new SolidColorBrush(Color.FromArgb(38, 255, 255, 255)), BorderThickness = new(1), Child = new TextBlock { Text = $"PLAYER {player + 1}", FontFamily = DS.Display, FontSize = 17, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(184, 198, 217)), VerticalAlignment = VerticalAlignment.Center } });
        title.Children.Add(new TextBlock { Text = "Which controller is this?", Style = (Style)FindResource("DS.Display"), FontWeight = FontWeights.SemiBold, FontSize = 32, Margin = new(0, 10, 0, 4) });
        title.Children.Add(new TextBlock { Text = "Pick the model in your hands. This sets its layout and tools; connecting the hardware is a separate step.", Style = (Style)FindResource("DS.Body"), Foreground = DS.Brush("Zone.TextSecondary") });
        header.Children.Add(title); body.Children.Add(header);

        // four, then three centred
        var grid = new Grid(); Grid.SetRow(grid, 1); body.Children.Add(grid);
        grid.RowDefinitions.Add(new()); grid.RowDefinitions.Add(new());
        var rows = new[] { new UniformGrid4(4), new UniformGrid4(4) };
        Grid.SetRow(rows[1], 1);
        foreach (var r in rows) grid.Children.Add(r);
        int n = 0;
        foreach (var model in catalog.Models)
        {
            var card = Card(model, facts.TryGetValue(model.Id, out var f) ? f : null);
            card.Click += (_, _) => Chosen?.Invoke(model);
            choices[model.Id] = card; cards.Add(card);
            rows[n < 4 ? 0 : 1].Add(card); n++;
        }
        var footer = GlyphHint.Footer(("A", "Choose"), ("B", "Back"), ("✥", "Move"));
        footer.Margin = new(9, 0, 0, 0); Grid.SetRow(footer, 2); body.Children.Add(footer);

        Unloaded += (_, _) => turn.Stop();
        Loaded += (_, _) =>
        {
            if (cards.Count > 0) cards[0].Focus();
            StartTurning();
            if(!DS.Motion)return;
            // the cards rise in one after another
            for (int i = 0; i < cards.Count; i++)
            {
                var c = cards[i]; var shift = new TranslateTransform(0, 18); ((TransformGroup)c.RenderTransform).Children.Add(shift);
                var at = TimeSpan.FromMilliseconds(40 * i); var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
                c.Opacity = 0;
                c.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(260)) { BeginTime = at, EasingFunction = ease });
                shift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(18, 0, TimeSpan.FromMilliseconds(320)) { BeginTime = at, EasingFunction = ease });
            }
        };
    }

    // every model that comes in more than one colour turns to its next colour at the same moment, then holds it (owner
    // direction 24 Oct 2026: all together, not one card after another); nothing runs between changes, so idle stays cheap
    private readonly List<FinishMorph> morphs = new();
    public IReadOnlyList<FinishMorph> Morphs => morphs;
    private readonly System.Windows.Threading.DispatcherTimer turn = new() { Interval = TimeSpan.FromMilliseconds(3800) };
    /// <summary>Every card starts in its model's default colour and then cycles like the rest (owner, 24 Oct 2026).</summary>
    private static bool Turns(FinishMorph m) => PadColours.Catalog.For(m.ModelId).Count > 1;
    private void StartTurning()
    {
        if (!DS.Motion || !morphs.Any(Turns)) return;
        turn.Tick -= Turn; turn.Tick += Turn; turn.Start();
    }
    private void Turn(object? sender, EventArgs e)
    {
        foreach (var morph in morphs.Where(Turns))
            _ = morph.ChangeTo(PadColours.Catalog.Next(morph.ModelId, morph.Finish).Id);
    }

    /// <summary>Every card shows its controller at the same size (owner, 24 Oct 2026: the X20 Pro and Dune looked far away,
    /// since each picture fills a different share of its canvas). The view is set round the controller's traced outline,
    /// with a little room, in one shape for every card; nothing is clipped, so the lift and glow still spill over.</summary>
    private static FrameworkElement Framed(FrameworkElement art, string model, string finish)
    {
        if (ControllerOutline.For(model, false, finish)?.Bounds is not { IsEmpty: false } b) return art;
        const double Aspect = 1.5, Room = 1.05;
        double width = Math.Max(b.Width, b.Height * Aspect) * Room, height = width / Aspect;
        var frame = new Canvas { Width = width, Height = height };
        Canvas.SetLeft(art, width / 2 - (b.X + b.Width / 2)); Canvas.SetTop(art, height / 2 - (b.Y + b.Height / 2));
        frame.Children.Add(art);
        return frame;
    }

    public void FocusFirst() { if (cards.Count > 0) cards[0].Focus(); }
    public IReadOnlyList<Button> Cards => cards;

    /// <summary>One controller card: art, name, a short description and the support line.</summary>
    private Button Card(ControllerModel model, Facts? facts)
    {
        var lift = new ScaleTransform(1, 1);
        var edge = new Border { CornerRadius = new(16), BorderThickness = new(1), BorderBrush = DS.Brush("Zone.BorderDefault"), Background = DS.Brush("Zone.ContentSurface"), Padding = new(20, 18, 20, 18) };
        var stack = new Grid();
        stack.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) }); stack.RowDefinitions.Add(new() { Height = GridLength.Auto }); stack.RowDefinitions.Add(new() { Height = GridLength.Auto }); stack.RowDefinitions.Add(new() { Height = GridLength.Auto });
        string shown = PadColours.Catalog.Default(model.Id);
        var picture = new Image { Source = ControllerArt.Front(model.Id, shown), Stretch = Stretch.Fill };
        RenderOptions.SetBitmapScalingMode(picture, BitmapScalingMode.HighQuality);
        var morph = new FinishMorph(picture); morph.Show(model.Id, shown); morphs.Add(morph);
        var art = new Viewbox { Stretch = Stretch.Uniform, Margin = new(0, 0, 0, 12), Child = Framed(new Grid { Width = 1536, Height = 1024, Children = { picture, morph } }, model.Id, shown) };
        stack.Children.Add(art);
        var name = new TextBlock { Text = model.Name, FontFamily = DS.Display, FontSize = 19, FontWeight = FontWeights.SemiBold, Foreground = DS.Brush("DS.Text"), TextTrimming = TextTrimming.CharacterEllipsis }; Grid.SetRow(name, 1); stack.Children.Add(name);
        var about = new TextBlock { Text = facts?.Description ?? "", FontFamily = DS.Body, FontSize = 12.5, Foreground = DS.Brush("Zone.TextSecondary"), TextWrapping = TextWrapping.Wrap, Margin = new(0, 5, 0, 10), MaxHeight = 36 }; Grid.SetRow(about, 2); stack.Children.Add(about);
        bool full = model.Support == "SUPPORTED";
        var line = new StackPanel { Orientation = Orientation.Horizontal };
        line.Children.Add(new System.Windows.Shapes.Ellipse { Width = 7, Height = 7, Margin = new(1, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center, Fill = new SolidColorBrush(full ? Accent : Color.FromRgb(98, 112, 134)) });
        line.Children.Add(new TextBlock { Text = facts?.SupportLine ?? model.Summary, FontFamily = DS.Display, FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(full ? Color.FromRgb(150, 205, 255) : Color.FromRgb(150, 162, 182)), VerticalAlignment = VerticalAlignment.Center });
        Grid.SetRow(line, 3); stack.Children.Add(line);
        edge.Child = stack;
        var button = new Button { Content = edge, Margin = new(9, 0, 9, 18), Padding = new(0), Background = Brushes.Transparent, BorderThickness = new(0), FocusVisualStyle = null, Cursor = Cursors.Hand, RenderTransformOrigin = new(.5, .5), RenderTransform = new TransformGroup { Children = { lift } } };
        button.Template = new ControlTemplate(typeof(Button)) { VisualTree = new FrameworkElementFactory(typeof(ContentPresenter)) };
        System.Windows.Automation.AutomationProperties.SetName(button, $"Assign {model.Name} to Player {Player + 1}, {model.Support}");
        void Light()
        {
            bool focused=button.IsKeyboardFocused,hover=button.IsMouseOver;
            edge.BorderBrush=focused?Brushes.White:DS.Brush("Zone.BorderDefault");edge.BorderThickness=new(focused?2:1);
            edge.Background=DS.Brush(hover?"Zone.ContentSurfaceHover":"Zone.ContentSurface");edge.Effect=null;
            lift.BeginAnimation(ScaleTransform.ScaleXProperty,null);lift.BeginAnimation(ScaleTransform.ScaleYProperty,null);lift.ScaleX=lift.ScaleY=1;

        }
        button.GotKeyboardFocus += (_, _) => Light(); button.LostKeyboardFocus += (_, _) => Light(); button.MouseEnter += (_, _) => Light(); button.MouseLeave += (_, _) => Light();
        return button;
    }

    private sealed record Facts(string Description, string SupportLine);
    /// <summary>Short, honest descriptions from the catalog's hardware facts (sticks, triggers, lighting, paddles, extras)
    /// and its software status. Nothing here is a capability claim beyond what the catalog records.</summary>
    private static Dictionary<string, Facts> ReadFacts()
    {
        var facts = new Dictionary<string, Facts>();
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Data", "catalog.json")));
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                string id = item.GetProperty("id").GetString()!; var parts = new List<string>();
                if (item.TryGetProperty("hardware", out var hw))
                {
                    string? Text(string key) => hw.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
                    bool Has(string key) => hw.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.True;
                    if (Text("sticks") is string s) parts.Add($"{s} sticks");
                    if (Text("triggers") is string t) parts.Add($"{t} triggers");
                    if (Has("rgb")) parts.Add("RGB");
                    if (Has("display")) parts.Add("screen");
                    if (Has("triggerHaptics")) parts.Add("trigger haptics");
                }
                if (item.TryGetProperty("macroSlots", out var slots) && slots.GetArrayLength() > 0) parts.Add($"{slots.GetArrayLength()} macro paddles");
                string availability = item.TryGetProperty("availability", out var a) ? a.GetString() ?? "" : "";
                string support = availability switch { "configuration" => "Full configuration", "preview" => "Preview · layout and art", "input_experimental" => "Input testing · experimental", _ => "Not available yet" };
                facts[id] = new(string.Join(" · ", parts), support);
            }
        }
        catch (Exception error) when (error is IOException or JsonException or KeyNotFoundException or InvalidOperationException) { }
        return facts;
    }

    /// <summary>A row of equal cards; a short row keeps the same card width and centres itself.</summary>
    private sealed class UniformGrid4 : Grid
    {
        private readonly int count; private int used;
        public UniformGrid4(int count) { this.count = count; HorizontalAlignment = HorizontalAlignment.Center; }
        public void Add(Button card) { ColumnDefinitions.Add(new()); SetColumn(card, used++); Children.Add(card); }
        protected override Size MeasureOverride(Size available)
        {
            // every card is a quarter of the full width, so the row of three matches the row of four
            double cell = double.IsInfinity(available.Width) ? 300 : available.Width / 4;
            foreach (var c in ColumnDefinitions) c.Width = new(cell);
            return base.MeasureOverride(new Size(cell * count, available.Height));
        }
    }
}
