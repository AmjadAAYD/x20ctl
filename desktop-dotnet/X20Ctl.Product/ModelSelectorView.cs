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
        brand.Children.Add(new Border { Width = 40, Height = 40, CornerRadius = new(10), Background = new SolidColorBrush(Color.FromRgb(12, 16, 28)), BorderBrush = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)), BorderThickness = new(1), Child = new Image { Source = new BitmapImage(new Uri("pack://application:,,,/Assets/brand.png")), Width = 24, Height = 24 } });
        brand.Children.Add(new TextBlock { Text = "X20CTL", FontFamily = DS.Display, FontSize = 22, FontWeight = FontWeights.Bold, Margin = new(18, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center, Foreground = DS.Brush("DS.Text") });
        brand.Children.Add(new TextBlock { Text = "Choose a controller", FontFamily = DS.Display, FontSize = 17, Foreground = DS.Brush("DS.TextSoft"), VerticalAlignment = VerticalAlignment.Center });
        chrome.Children.Add(brand); Children.Add(chrome);
        Children.Add(new Border { Height = 1, VerticalAlignment = VerticalAlignment.Top, Margin = new(0, 64, 0, 0), Background = new SolidColorBrush(Color.FromArgb(28, 255, 255, 255)) });

        var body = new Grid { Margin = new(47, 94, 47, 0) };
        body.RowDefinitions.Add(new() { Height = GridLength.Auto }); body.RowDefinitions.Add(new()); body.RowDefinitions.Add(new() { Height = new(56) });
        Children.Add(body);
        var header = new DockPanel { Margin = new(9, 0, 9, 26) };
        var back = new Button { Content = "Back", Style = (Style)FindResource("DS.Secondary"), MinHeight = 38, Height = 38, Padding = new(18, 0, 18, 0), FontSize = 14.5, VerticalAlignment = VerticalAlignment.Center };
        back.Click += (_, _) => Cancelled?.Invoke(); DockPanel.SetDock(back, Dock.Right); header.Children.Add(back);
        var title = new StackPanel();
        title.Children.Add(new Border { CornerRadius = new(11), Height = 22, Padding = new(10, 0, 10, 0), HorizontalAlignment = HorizontalAlignment.Left, Background = new SolidColorBrush(Color.FromArgb(28, 255, 255, 255)), BorderBrush = new SolidColorBrush(Color.FromArgb(38, 255, 255, 255)), BorderThickness = new(1), Child = new TextBlock { Text = $"PLAYER {player + 1}", FontFamily = DS.Display, FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(184, 198, 217)), VerticalAlignment = VerticalAlignment.Center } });
        title.Children.Add(new TextBlock { Text = "Which controller is this?", Style = (Style)FindResource("DS.Display"), FontWeight = FontWeights.SemiBold, FontSize = 32, Margin = new(0, 10, 0, 4) });
        title.Children.Add(new TextBlock { Text = "Pick the model in your hands. This sets its layout and tools; connecting the hardware is a separate step.", Style = (Style)FindResource("DS.Body"), Foreground = new SolidColorBrush(Color.FromRgb(140, 155, 178)) });
        header.Children.Add(title); body.Children.Add(header);

        // four, then three centred
        var grid = new Grid(); Grid.SetRow(grid, 1); body.Children.Add(grid);
        grid.RowDefinitions.Add(new()); grid.RowDefinitions.Add(new());
        var rows = new[] { new UniformGrid4(4), new UniformGrid4(3) };
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

        Loaded += (_, _) =>
        {
            if (cards.Count > 0) cards[0].Focus();
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

    /// <summary>One controller card: art, name, a short description and the support line.</summary>
    private Button Card(ControllerModel model, Facts? facts)
    {
        var lift = new ScaleTransform(1, 1);
        var edge = new Border { CornerRadius = new(16), BorderThickness = new(1), BorderBrush = new SolidColorBrush(Color.FromArgb(128, 74, 98, 128)), Background = new LinearGradientBrush(Color.FromArgb(196, 30, 41, 58), Color.FromArgb(206, 17, 23, 34), 90), Padding = new(20, 18, 20, 18) };
        var stack = new Grid();
        stack.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) }); stack.RowDefinitions.Add(new() { Height = GridLength.Auto }); stack.RowDefinitions.Add(new() { Height = GridLength.Auto }); stack.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var picture = new Image { Source = ControllerArt.Front(model.Id), Stretch = Stretch.Fill };
        RenderOptions.SetBitmapScalingMode(picture, BitmapScalingMode.HighQuality);
        var rim = new ControllerOutline(); rim.Show(model.Id, false); ControllerOutline.Trim(picture, model.Id, false);
        var art = new Viewbox { Stretch = Stretch.Uniform, Margin = new(0, 0, 0, 12), Child = new Grid { Width = 1536, Height = 1024, Children = { picture, rim } } };
        stack.Children.Add(art);
        var name = new TextBlock { Text = model.Name, FontFamily = DS.Display, FontSize = 19, FontWeight = FontWeights.SemiBold, Foreground = DS.Brush("DS.Text"), TextTrimming = TextTrimming.CharacterEllipsis }; Grid.SetRow(name, 1); stack.Children.Add(name);
        var about = new TextBlock { Text = facts?.Description ?? "", FontFamily = DS.Body, FontSize = 12.5, Foreground = new SolidColorBrush(Color.FromRgb(140, 155, 178)), TextWrapping = TextWrapping.Wrap, Margin = new(0, 5, 0, 10), MaxHeight = 36 }; Grid.SetRow(about, 2); stack.Children.Add(about);
        bool full = model.Support == "SUPPORTED";
        var line = new StackPanel { Orientation = Orientation.Horizontal };
        line.Children.Add(new System.Windows.Shapes.Ellipse { Width = 7, Height = 7, Margin = new(1, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center, Fill = new SolidColorBrush(full ? Accent : Color.FromRgb(98, 112, 134)) });
        line.Children.Add(new TextBlock { Text = facts?.SupportLine ?? model.Summary, FontFamily = DS.Display, FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(full ? Color.FromRgb(150, 205, 255) : Color.FromRgb(150, 162, 182)), VerticalAlignment = VerticalAlignment.Center });
        Grid.SetRow(line, 3); stack.Children.Add(line);
        // gloss: a sheen falling from the top and a lit top edge, as on the Zone's cards
        var gloss = new Grid { Margin = new(-20, -18, -20, -18), IsHitTestVisible = false };
        gloss.Children.Add(new Border { CornerRadius = new(15, 15, 0, 0), Height = 110, VerticalAlignment = VerticalAlignment.Top, Background = new LinearGradientBrush(new GradientStopCollection { new(Color.FromArgb(34, 255, 255, 255), 0), new(Color.FromArgb(6, 255, 255, 255), .5), new(Color.FromArgb(0, 255, 255, 255), 1) }, 90) });
        gloss.Children.Add(new Border { CornerRadius = new(15), BorderThickness = new(0, 1, 0, 0), BorderBrush = new SolidColorBrush(Color.FromArgb(64, 255, 255, 255)) });
        var layered = new Grid(); layered.Children.Add(gloss); layered.Children.Add(stack);
        edge.Child = layered;
        var button = new Button { Content = edge, Margin = new(9, 0, 9, 18), Padding = new(0), Background = Brushes.Transparent, BorderThickness = new(0), FocusVisualStyle = null, Cursor = Cursors.Hand, RenderTransformOrigin = new(.5, .5), RenderTransform = new TransformGroup { Children = { lift } } };
        button.Template = new ControlTemplate(typeof(Button)) { VisualTree = new FrameworkElementFactory(typeof(ContentPresenter)) };
        System.Windows.Automation.AutomationProperties.SetName(button, $"Assign {model.Name} to Player {Player + 1}, {model.Support}");
        void Light()
        {
            bool on = button.IsKeyboardFocused || button.IsMouseOver;
            edge.BorderBrush = on ? Brushes.White : new SolidColorBrush(Color.FromArgb(128, 74, 98, 128)); edge.BorderThickness = new(on ? 2 : 1);
            edge.Effect = on ? new System.Windows.Media.Effects.DropShadowEffect { Color = Accent, BlurRadius = 30, ShadowDepth = 0, Opacity = .45 } : null;
            foreach (var axis in new[] { ScaleTransform.ScaleXProperty, ScaleTransform.ScaleYProperty })
                lift.BeginAnimation(axis, new DoubleAnimation(on ? 1.03 : 1, TimeSpan.FromMilliseconds(160)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
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
