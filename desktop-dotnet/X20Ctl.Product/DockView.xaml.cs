using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using X20Ctl.Zone;
namespace X20Ctl.Product;

public partial class DockView : UserControl
{
    public int Index { get; }
    public PlayerDock Player { get; private set; }
    private bool isHero, isSecondary;
    // The controller art glides to where its role puts it (owner report 10 Oct 2026: during a swap the art "tweaked",
    // jumping between the side tile's right-aligned spot and the hero's centred one). Each frame of a move gives a new
    // target; the shown rectangle follows it with a short, time-based ease, so a role change is a glide, never a jump.
    private Rect artShown = Rect.Empty, artTarget = Rect.Empty;
    private TimeSpan artLast; private bool artGliding;
    private void GlideArt(double x, double y, double w)
    {
        artTarget = new(x, y, w, w / 1.5);
        if (artShown.IsEmpty || !SystemParameters.ClientAreaAnimation) { artShown = artTarget; PlaceArt(artShown); return; }
        if (!artGliding) { artGliding = true; artLast = TimeSpan.Zero; CompositionTarget.Rendering += ArtFrame; }
    }
    private void ArtFrame(object? sender, EventArgs e)
    {
        var now = ((RenderingEventArgs)e).RenderingTime; double dt = artLast == TimeSpan.Zero ? 1 / 60.0 : Math.Min(.05, (now - artLast).TotalSeconds); artLast = now;
        double k = 1 - Math.Exp(-dt * 16);
        artShown = new(artShown.X + (artTarget.X - artShown.X) * k, artShown.Y + (artTarget.Y - artShown.Y) * k, artShown.Width + (artTarget.Width - artShown.Width) * k, artShown.Height + (artTarget.Height - artShown.Height) * k);
        if (Math.Abs(artShown.X - artTarget.X) + Math.Abs(artShown.Y - artTarget.Y) + Math.Abs(artShown.Width - artTarget.Width) < .6) { artShown = artTarget; CompositionTarget.Rendering -= ArtFrame; artGliding = false; }
        PlaceArt(artShown);
    }
    private void PlaceArt(Rect r)
    {
        double artX = r.X, artY = r.Y, artWidth = r.Width;
        Art.Width = artWidth; Art.Height = artWidth / 1.5; Canvas.SetLeft(Art, artX); Canvas.SetTop(Art, artY);
        Halo.Width = artWidth * 1.25; Halo.Height = artWidth / 1.5 * 1.05; Canvas.SetLeft(Halo, artX - artWidth * .125); Canvas.SetTop(Halo, artY - artWidth / 1.5 * .02);
        ArtShadow.Width = artWidth * .72; ArtShadow.Height = Math.Max(8, artWidth * .075); Canvas.SetLeft(ArtShadow, artX + artWidth * .14); Canvas.SetTop(ArtShadow, artY + artWidth / 1.5 * .9);
    }
    /// <summary>The one accent: Steam's blue, for the chosen controller and its bloom.</summary>
    public static readonly Color Accent = Color.FromRgb(26, 159, 255);
    // tinted glass: translucent enough that the galaxy shows through
    private static readonly Brush CardFill = Frozen(new LinearGradientBrush(Color.FromArgb(196, 30, 41, 58), Color.FromArgb(206, 17, 23, 34), 90));
    private static readonly Brush HeroFill = Frozen(new LinearGradientBrush(Color.FromArgb(214, 36, 52, 76), Color.FromArgb(222, 19, 27, 41), 90));
    private static readonly Brush RestEdge = Frozen(new SolidColorBrush(Color.FromArgb(128, 74, 98, 128)));
    private static readonly Brush HeroEdge = Frozen(new SolidColorBrush(Color.FromArgb(220, Accent.R, Accent.G, Accent.B)));
    private static Brush Frozen(Brush b) { b.Freeze(); return b; }
    public event Action<int>? Selected;
    public event Action<int, bool>? ActionRequested;
    public DockView(int index, PlayerDock player)
    {
        InitializeComponent(); Index = index; Player = player;
        // the empty card's dashed controller is the X20's own traced outline, and its plus does what Choose Controller does
        if (ControllerOutline.For("x20", false) is { } traced) SilhouettePath.Data = traced;
        var plusScale = new ScaleTransform(1, 1); PlusBadge.RenderTransform = plusScale;
        void Grow(double to) { plusScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(to, TimeSpan.FromMilliseconds(140)) { EasingFunction = new CubicEase() }); plusScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(to, TimeSpan.FromMilliseconds(140)) { EasingFunction = new CubicEase() }); }
        PlusBadge.MouseEnter += (_, _) => Grow(1.12); PlusBadge.MouseLeave += (_, _) => Grow(1);
        PlusBadge.PreviewMouseLeftButtonDown += (_, e) => { e.Handled = true; Grow(.92); };
        PlusBadge.PreviewMouseLeftButtonUp += (_, e) => { e.Handled = true; Grow(1.12); ActionRequested?.Invoke(Index, false); };
        FocusButton.GotKeyboardFocus += (_, _) => UpdateCard(); FocusButton.LostKeyboardFocus += (_, _) => UpdateCard();
        FocusButton.MouseEnter += (_, _) => UpdateCard(); FocusButton.MouseLeave += (_, _) => UpdateCard();
        PlayerLabel.Text = $"PLAYER {player.Number}";
        FocusButton.TabIndex = index;
        RefreshPlayer(player);
    }
    public void RefreshPlayer(PlayerDock player)
    {
        Player = player;
        System.Windows.Automation.AutomationProperties.SetName(FocusButton, $"Player {player.Number}, {player.Model ?? "empty"}, focus controller dock");
        if (player.Model != null && (new[] { "x20", "x20_pro", "x15", "x05", "x05_pro", "x10", "d10", "dune" }.Contains(player.ModelId) || player.IsMockConnected))
        {
            string outlineModel = player.ModelId ?? (player.Model switch { "EasySMX X15" => "x15", "EasySMX X05 Pro" => "x05_pro", _ => "x20" });
            ControllerImage.Source = ControllerArt.Front(outlineModel);
            ArtOutline.Show(outlineModel, false); ControllerOutline.Trim(ControllerImage, outlineModel, false);
            ImageClip.Clip = null;
            Illustration.Visibility = Visibility.Visible; Silhouette.Visibility = Visibility.Collapsed;
        }
        else { Illustration.Visibility = Visibility.Collapsed; Silhouette.Visibility = Visibility.Visible; }
        if (!double.IsNaN(Width)) Present(isHero, isSecondary);
    }
    public DockBounds RenderedBounds => double.IsNaN(Width) ? new(0, 0, 0, 0) : new(Canvas.GetLeft(this), Canvas.GetTop(this), Width, Height);
    /// <summary>Put the card at these bounds in this role and lay its content out for that size. The Zone calls this every
    /// frame of a move, so the content reflows as the card grows or shrinks instead of being stretched.</summary>
    public void SetBounds(DockBounds bounds, bool hero, bool secondary)
    {
        Canvas.SetLeft(this, bounds.X); Canvas.SetTop(this, bounds.Y); Width = bounds.Width; Height = bounds.Height;
        Present(hero, secondary);
    }
    public void Place(DockBounds target, bool hero, bool secondary, int durationMs) => SetBounds(target, hero, secondary);
    private void Present(bool hero, bool secondary)
    {
        bool roleChanged = hero != isHero || secondary != isSecondary || Card.Background == null;
        isHero = hero; isSecondary = secondary;
        double w = Width, h = Height;
        bool empty = Player.Model == null;
        ModelName.Text = Player.Model ?? (hero ? "Choose your controller" : "Add controller");
        ModelName.TextTrimming = TextTrimming.CharacterEllipsis;
        Connection.Text = empty ? (hero ? "Assign a controller to this player" : "Empty") : Player.IsMockConnected ? "Connected · mock" : "Not connected";
        StatusDot.Fill = new SolidColorBrush(!empty && Player.IsMockConnected ? Color.FromRgb(87, 203, 138) : Color.FromRgb(98, 112, 134));
        double artWidth, artX, artY, detailsX, detailsY, detailsWidth;
        if (hero)
        {
            // the hero square: the controller large in the middle, its name and the actions along the bottom
            Canvas.SetLeft(PlayerPill, 28); Canvas.SetTop(PlayerPill, 26); PlayerLabel.FontSize = 12;
            // while a small card is still growing into the hero, keep its controller at least as big as it was on the
            // side tile, so it never shrinks to a speck and then balloons (owner report 10 Oct 2026)
            double fit = Math.Min(w - 56, (h - 196) * 1.5), floor = Math.Min(Math.Min(w - 56, w * .6), (h - 30) * 1.5);
            artWidth = Math.Max(40, Math.Max(fit, floor)); artX = (w - artWidth) / 2;
            artY = artWidth / 1.5 > h - 196 ? Math.Max(10, (h - artWidth / 1.5) / 2) : Math.Max(54, (h - 170 - artWidth / 1.5) / 2 + 22);
            detailsX = 30; detailsY = h - 168; detailsWidth = w - 60; ModelName.FontSize = 34;
        }
        else if (secondary)
        {
            // a side tile: label and name on the left, the controller on the right
            Canvas.SetLeft(PlayerPill, 18); Canvas.SetTop(PlayerPill, 16); PlayerLabel.FontSize = 10.5;
            artWidth = Math.Max(30, Math.Min(w * .5, (h - 14) * 1.5)); artX = w - artWidth - 8; artY = (h - artWidth / 1.5) / 2;
            detailsX = 20; detailsY = Math.Max(44, h / 2 - 8); detailsWidth = Math.Max(40, artX - 28); ModelName.FontSize = 16;
            if (empty)
            {   // an empty tile is just the "add" target: the dashed controller in the middle, the words under the pill
                artWidth = Math.Max(30, Math.Min(w * .42, (h - 40) * 1.5)); artX = (w - artWidth) / 2; artY = (h - artWidth / 1.5) / 2 + 6;
                detailsY = h - 58; detailsWidth = Math.Max(40, artX - 28);
            }
        }
        else
        {
            // a grid card: the controller centred, its name and status bottom-left
            Canvas.SetLeft(PlayerPill, 22); Canvas.SetTop(PlayerPill, 20); PlayerLabel.FontSize = 12;
            artWidth = Math.Max(40, Math.Max(Math.Min(w * .42, (h - 128) * 1.5), Math.Min(w * .5, (h - 30) * 1.5) * .8)); artX = (w - artWidth) / 2; artY = Math.Max(10, Math.Min(46, (h - artWidth / 1.5) / 2));
            detailsX = 28; detailsY = h - 84; detailsWidth = w - 120; ModelName.FontSize = 23;
        }
        if (roleChanged) UpdateCard();
        // the + sits in the middle of the controller body (above the grips), not the middle of the outline's box
        PlusBadge.Margin = new(0, 0, 0, artWidth / 1.5 * .2);
        GlideArt(artX, artY, artWidth);
        // a soft halo behind the controller (or the empty outline), and the arrow on grid cards
        // (the halo and floor shadow follow the art in PlaceArt)
        ArrowBadge.Visibility = hero || secondary ? Visibility.Collapsed : Visibility.Visible; Canvas.SetLeft(ArrowBadge, w - 74); Canvas.SetTop(ArrowBadge, h - 76);
        Stripes.Visibility = secondary ? Visibility.Collapsed : Visibility.Visible;
        // a soft floor shadow under the controller so it stands off the glass
        // (see PlaceArt)
        ArtShadow.Visibility = Illustration.Visibility;
        Canvas.SetLeft(Details, detailsX); Canvas.SetTop(Details, detailsY); Details.Width = detailsWidth; Details.Height = hero ? 80 : 50;
        ModelName.MaxWidth = detailsWidth;
        Canvas.SetTop(StatusLine, ModelName.FontSize * 1.34 + (hero ? 4 : 2));
        ActionCanvas.Visibility = hero ? Visibility.Visible : Visibility.Collapsed;
        Primary.Content = empty ? "Choose Controller  →" : "Enter Studio  →";
        Primary.Width = empty ? 240 : 210;
        Canvas.SetLeft(Primary, w - Primary.Width - 30); Canvas.SetTop(Primary, h - 76);
        Canvas.SetLeft(Switch, 30); Canvas.SetTop(Switch, h - 76);
        Switch.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        System.Windows.Automation.AutomationProperties.SetName(Primary, $"Player {Player.Number}, {(empty ? "Choose Controller" : "Enter Studio")}");
        System.Windows.Automation.AutomationProperties.SetName(Switch, $"Player {Player.Number}, Switch Controller");
    }
    /// <summary>The chosen controller is lit in the accent blue with a soft glow; focus adds a white edge and a small lift.</summary>
    private void UpdateCard()
    {
        bool focus = FocusButton.IsKeyboardFocused || FocusButton.IsMouseOver;
        Card.BorderThickness = new(focus || isHero ? 2 : 1);
        Card.BorderBrush = focus ? Brushes.White : isHero ? HeroEdge : RestEdge;
        Card.Background = isHero ? HeroFill : CardFill;
        Card.Effect = isHero || focus ? new System.Windows.Media.Effects.DropShadowEffect { Color = isHero ? Accent : Colors.White, BlurRadius = isHero ? 46 : 22, ShadowDepth = 0, Opacity = isHero ? .5 : .16 } : null;
        double lift = focus && !isHero ? 1.025 : 1;
        foreach (var axis in new[] { ScaleTransform.ScaleXProperty, ScaleTransform.ScaleYProperty })
            LiftScale.BeginAnimation(axis, new DoubleAnimation(lift, TimeSpan.FromMilliseconds(160)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
    }
    public void FadeHeroDetails(double value, int duration)
    {
        Animate(Details, OpacityProperty, Details.Opacity, value, duration);
        Animate(ActionCanvas, OpacityProperty, ActionCanvas.Opacity, value, duration);
        Animate(PlayerPill, OpacityProperty, PlayerPill.Opacity, value, duration);
    }
    public void SettleArtwork(double from, int duration)
    {
        Animate(ArtScale, ScaleTransform.ScaleXProperty, from, 1, duration);
        Animate(ArtScale, ScaleTransform.ScaleYProperty, from, 1, duration);
    }
    private static void Animate(Animatable target, DependencyProperty property, double from, double to, int milliseconds)
    {
        target.BeginAnimation(property, null); target.SetValue(property, to);
        if (milliseconds > 0) target.BeginAnimation(property, new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(milliseconds)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }, FillBehavior = FillBehavior.Stop });
    }
    private static void Animate(UIElement target, DependencyProperty property, double from, double to, int milliseconds)
    {
        target.BeginAnimation(property, null); target.SetValue(property, to);
        if (milliseconds > 0) target.BeginAnimation(property, new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(milliseconds)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }, FillBehavior = FillBehavior.Stop });
    }
    private void SelectPlayer(object sender, RoutedEventArgs e) => Selected?.Invoke(Index);
    private void PrimaryAction(object sender, RoutedEventArgs e) => ActionRequested?.Invoke(Index, false);
    private void SwitchAction(object sender, RoutedEventArgs e) => ActionRequested?.Invoke(Index, true);
}
