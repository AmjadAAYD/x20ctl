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
        if (url is not (Repository or Inspiration or ProductLinks.Support)) throw new ArgumentException("Unsupported destination.");
        if (ReviewSandbox.Active) return;
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
}

/// <summary>
/// Tools (owner direction 10 Oct 2026, replacing the plain Settings page and the Zone's gear): a console-style hub
/// with the Controller Check as its hero, then Replay Intro, About, Support and the GitHub repository.
/// About credits ReynArts' ApexSenseBridge as the design inspiration.
/// </summary>
public sealed class ProductSettings : Grid
{
    public Button Replay { get; }
    public Button Support { get; }
    public Button Check { get; }
    public Button About { get; }
    public Button Star { get; }
    public Button Back { get; }
    private readonly Grid hub = new(), about = new() { Visibility = Visibility.Collapsed };
    private readonly Button aboutBack;
    public bool ShowingAbout => about.Visibility == Visibility.Visible;

    public ProductSettings(Action support, Action replay, Action close, Action check)
    {
        Background = new SolidColorBrush(Color.FromRgb(5, 9, 20));
        Children.Add(new FacetBackdrop());
        Children.Add(new Border { Background = new RadialGradientBrush(Color.FromArgb(0, 5, 9, 20), Color.FromArgb(170, 3, 6, 14)) { RadiusX = .8, RadiusY = .8 }, IsHitTestVisible = false });
        var page = new Grid { Margin = new(64, 40, 64, 40) };
        page.RowDefinitions.Add(new() { Height = GridLength.Auto }); page.RowDefinitions.Add(new());
        Children.Add(page);

        // header: mark, title, back
        var header = new DockPanel { Margin = new(0, 0, 0, 30) };
        Back = Pill("", "Controller Zone"); Back.Click += (_, _) => close();
        DockPanel.SetDock(Back, Dock.Right); header.Children.Add(Back);
        var mark = new Image { Source = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Assets/brand.png")), Width = 44, Height = 44, Margin = new(0, 0, 18, 0) };
        RenderOptions.SetBitmapScalingMode(mark, BitmapScalingMode.HighQuality);
        header.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Children = { mark, new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = {
            Kit.Text("Tools", 44, "DS.Text", FontWeights.Bold), Kit.Text("Help, extras and everything about X20CTL", 17, "DS.TextSoft") } } } });
        page.Children.Add(header);

        // hub: the Controller Check hero on the left, four tiles on the right
        hub.ColumnDefinitions.Add(new() { Width = new(1.05, GridUnitType.Star) }); hub.ColumnDefinitions.Add(new() { Width = new(24) }); hub.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        Grid.SetRow(hub, 1); page.Children.Add(hub);
        Check = Card("", "Controller not working?", "Open the Controller Check. See every button, stick and trigger live, run a guided test, and save a report you can share.", Kit.Blue, hero: true);
        Check.Click += (_, _) => check(); hub.Children.Add(Check);
        var tiles = new Grid(); tiles.RowDefinitions.Add(new()); tiles.RowDefinitions.Add(new() { Height = new(24) }); tiles.RowDefinitions.Add(new());
        tiles.ColumnDefinitions.Add(new()); tiles.ColumnDefinitions.Add(new() { Width = new(24) }); tiles.ColumnDefinitions.Add(new());
        Grid.SetColumn(tiles, 2); hub.Children.Add(tiles);
        Replay = Card("", "Replay Intro", "Watch the X20CTL opening again.", Kit.Blue);
        About = Card("", "About", "What X20CTL is, who made it, and credits.", Color.FromRgb(150, 175, 215));
        Support = Card("", "Support X20CTL", "Help keep it going on Ko-fi.", Color.FromRgb(255, 92, 122));
        Star = Card("", "Star our GitHub repo", "github.com/AmjadAAYD/x20ctl", Color.FromRgb(245, 200, 80));
        Replay.Click += (_, _) => replay(); Support.Click += (_, _) => support(); Star.Click += (_, _) => Try(() => ExternalLinks.Open(ExternalLinks.Repository));
        About.Click += (_, _) => ShowAbout(true);
        Place(tiles, Replay, 0, 0); Place(tiles, About, 0, 2); Place(tiles, Support, 2, 0); Place(tiles, Star, 2, 2);

        // about
        Grid.SetRow(about, 1); page.Children.Add(about);
        aboutBack = Pill("", "Back to Tools"); aboutBack.Click += (_, _) => ShowAbout(false);
        about.Children.Add(BuildAbout());

        if (DS.Motion) Loaded += (_, _) => Enter(hub);
    }

    public void ShowAbout(bool on)
    {
        hub.Visibility = on ? Visibility.Collapsed : Visibility.Visible; about.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        if (DS.Motion) Enter(on ? about : hub);
        (on ? aboutBack : About).Focus();
    }
    /// <summary>B / Esc: from About back to the hub, from the hub back to the Zone.</summary>
    public bool StepBack() { if (!ShowingAbout) return false; ShowAbout(false); return true; }
    public void FocusFirst() => Check.Focus();

    private UIElement BuildAbout()
    {
        var version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "preview";
        var panel = new Border
        {
            CornerRadius = new(22), BorderThickness = new(1), Padding = new(48, 40, 48, 40), MaxWidth = 1100, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
            Background = new LinearGradientBrush(Color.FromArgb(215, 14, 24, 52), Color.FromArgb(200, 8, 14, 34), 90),
            BorderBrush = new LinearGradientBrush(Color.FromArgb(110, 120, 170, 255), Color.FromArgb(30, 120, 170, 255), 90)
        };
        var stack = new StackPanel(); panel.Child = stack;
        var mark = new Image { Source = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Assets/brand.png")), Width = 72, Height = 72, Margin = new(0, 0, 24, 0) };
        RenderOptions.SetBitmapScalingMode(mark, BitmapScalingMode.HighQuality);
        stack.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Children = { mark, new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = {
            Kit.Text("X20CTL", 38, "DS.Text", FontWeights.Bold), Kit.Text($"Native Controller Studio · version {version}", 16, "DS.TextSoft") } } } });
        TextBlock Para(string text, double top = 18, string brush = "DS.TextSoft", double size = 16.5) => new() { Text = text, FontFamily = DS.Body, FontSize = size, Foreground = DS.Brush(brush), TextWrapping = TextWrapping.Wrap, LineHeight = 26, Margin = new(0, top, 0, 0) };
        stack.Children.Add(Para("X20CTL is an independent, open-source controller studio for EasySMX pads. Remap buttons, shape stick and trigger response, build back-paddle macros, set vibration, and check that a controller works, all on your own PC. There is no account, and nothing is uploaded.", 28, "DS.Text", 18));
        stack.Children.Add(Para("Every change stays a local draft until it can be sent to a connected, verified controller. X20CTL never writes to a controller it cannot read back."));
        stack.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), Margin = new(0, 30, 0, 24) });
        stack.Children.Add(Kit.Text("MADE BY", 13, "DS.TextMuted", FontWeights.SemiBold));
        stack.Children.Add(Para("Amjad AAYD, who designs and builds X20CTL.", 6, "DS.Text", 18));
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

    private static Button Pill(string icon, string text)
    {
        var b = new Button { Style = DS.Style("DS.ActionSecondary"), Height = 46, MinHeight = 0, Padding = new(22, 0, 24, 0), FontSize = 15.5,
            Content = new StackPanel { Orientation = Orientation.Horizontal, Children = { Kit.Icon(icon, 15, "DS.Text").Also(i => i.Margin = new(0, 0, 10, 0)), Kit.Text(text, 15.5, "DS.Text", FontWeights.SemiBold) } } };
        return b;
    }

    /// <summary>A glossy tile: glowing icon disc, title, one line of description, an arrow badge; lifts and lights on focus.</summary>
    private static Button Card(string icon, string title, string detail, Color accent, bool hero = false)
    {
        var art = new Grid();
        var glow = new Ellipse { Width = hero ? 420 : 220, Height = hero ? 420 : 220, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new(0, hero ? -120 : -80, hero ? -120 : -70, 0), IsHitTestVisible = false,
            Fill = new RadialGradientBrush(Color.FromArgb(hero ? (byte)70 : (byte)52, accent.R, accent.G, accent.B), Color.FromArgb(0, accent.R, accent.G, accent.B)) };
        art.Children.Add(glow);
        if (hero)
        {   // the Controller Check card shows a controller being scanned: a line of light sweeping down over it
            var pad = new Grid { Width = 1536, Height = 1024 }; pad.Children.Add(new PhotoController("x20"));
            var sweep = new Rectangle { Height = 90, VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false,
                Fill = new LinearGradientBrush(new GradientStopCollection { new GradientStop(Color.FromArgb(0, 125, 182, 255), 0), new GradientStop(Color.FromArgb(150, 125, 182, 255), .5), new GradientStop(Color.FromArgb(0, 125, 182, 255), 1) }, 90) };
            var move = new TranslateTransform(); sweep.RenderTransform = move; pad.Children.Add(sweep);
            pad.OpacityMask = new VisualBrush(new PhotoController("x20")) { Stretch = Stretch.None };
            if (DS.Motion) move.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(80, 900, TimeSpan.FromSeconds(2.6)) { RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }, AutoReverse = true });
            art.Children.Add(new Viewbox { Child = pad, Width = 520, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new(0, 40, -10, 0), IsHitTestVisible = false });
        }
        else art.Children.Add(new TextBlock { Text = icon, FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 150, Foreground = new SolidColorBrush(Color.FromArgb(14, 255, 255, 255)), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new(0, 26, 10, 0), IsHitTestVisible = false });
        var disc = new Grid { Width = hero ? 92 : 64, Height = hero ? 92 : 64, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
            Children = { new Ellipse { Fill = new LinearGradientBrush(Color.FromArgb(150, accent.R, accent.G, accent.B), Color.FromArgb(60, accent.R, accent.G, accent.B), 90), Stroke = new SolidColorBrush(Color.FromArgb(200, accent.R, accent.G, accent.B)), StrokeThickness = 1.5,
                Effect = new DropShadowEffect { Color = accent, BlurRadius = 26, ShadowDepth = 0, Opacity = .6 } },
                new TextBlock { Text = icon, FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = hero ? 38 : 26, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } } };
        art.Children.Add(disc);
        var words = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom, Margin = new(0, 0, hero ? 70 : 54, 0) };
        words.Children.Add(Kit.Text(title, hero ? 38 : 24, "DS.Text", FontWeights.Bold).Also(t => { t.TextWrapping = TextWrapping.Wrap; t.TextTrimming = TextTrimming.None; }));
        var d = Kit.Text(detail, hero ? 17 : 14.5, "DS.TextSoft"); d.TextWrapping = TextWrapping.Wrap; d.TextTrimming = TextTrimming.None; d.Margin = new(0, 8, 0, 0); words.Children.Add(d);
        if (hero) words.Children.Add(new WrapPanel { Margin = new(0, 20, 0, 0), Children = { Chip("Live input"), Chip("Guided test"), Chip("Device list"), Chip("Report") } });
        art.Children.Add(words);
        var arrow = new Border { Width = 42, Height = 42, CornerRadius = new(21), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Background = new SolidColorBrush(Color.FromArgb(34, 255, 255, 255)), BorderBrush = new SolidColorBrush(Color.FromArgb(70, 255, 255, 255)), BorderThickness = new(1),
            Child = Kit.Icon("", 15, "DS.Text").Also(i => i.HorizontalAlignment = HorizontalAlignment.Center) };
        art.Children.Add(arrow);
        art.Children.Add(new TextBlock { Text = "///", FontFamily = DS.Display, FontSize = 16, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromArgb(70, 255, 255, 255)), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top });
        var face = new Border
        {
            CornerRadius = new(hero ? 24 : 20), BorderThickness = new(1.2), Padding = hero ? new(40, 38, 32, 32) : new(28, 24, 22, 22), ClipToBounds = true, Child = art,
            Background = new LinearGradientBrush(Color.FromArgb(225, 22, 36, 72), Color.FromArgb(215, 9, 16, 38), 90),
            BorderBrush = new LinearGradientBrush(Color.FromArgb(120, 150, 190, 255), Color.FromArgb(28, 150, 190, 255), 90)
        };
        var shine = new Border { CornerRadius = face.CornerRadius, IsHitTestVisible = false, Background = new LinearGradientBrush(Color.FromArgb(26, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), new Point(0, 0), new Point(.6, .6)) };
        var scale = new ScaleTransform(1, 1);
        var button = new Button { Content = new Grid { Children = { face, shine } }, Cursor = Cursors.Hand, FocusVisualStyle = null, RenderTransformOrigin = new(.5, .5), RenderTransform = scale, ToolTip = detail,
            Template = BareTemplate() };
        System.Windows.Automation.AutomationProperties.SetName(button, title);
        void Light(bool on)
        {
            face.BorderBrush = on ? Brushes.White : new LinearGradientBrush(Color.FromArgb(120, 150, 190, 255), Color.FromArgb(28, 150, 190, 255), 90);
            face.Effect = on ? new DropShadowEffect { Color = accent, BlurRadius = 34, ShadowDepth = 0, Opacity = .6 } : null;
            double to = on ? 1.025 : 1;
            if (DS.Motion) { scale.BeginAnimation(ScaleTransform.ScaleXProperty, DS.To(to, 170)); scale.BeginAnimation(ScaleTransform.ScaleYProperty, DS.To(to, 170)); } else scale.ScaleX = scale.ScaleY = to;
        }
        button.MouseEnter += (_, _) => Light(true); button.MouseLeave += (_, _) => Light(button.IsKeyboardFocused);
        button.GotKeyboardFocus += (_, _) => Light(true); button.LostKeyboardFocus += (_, _) => Light(button.IsMouseOver);
        return button;
    }
    private static Border Chip(string text) => new() { CornerRadius = new(14), Padding = new(12, 5, 12, 5), Margin = new(0, 0, 8, 8), Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)), BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)), BorderThickness = new(1), Child = Kit.Text(text, 13, "DS.Text", FontWeights.SemiBold) };
    private static ControlTemplate BareTemplate() { var t = new ControlTemplate(typeof(Button)) { VisualTree = new FrameworkElementFactory(typeof(ContentPresenter)) }; t.Seal(); return t; }
}
