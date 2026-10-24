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
    public FrameworkElement ControllerArtwork => Art;
    public PlayerDock Player { get; private set; }
    private bool isHero, isSecondary;
    private readonly bool reducedMotion;
    // The card and its artwork share the Zone's transition progress, including interrupted moves.
    private Rect artShown = Rect.Empty, artTarget = Rect.Empty;
    private Rect artMoveFrom = Rect.Empty, artMoveTo = Rect.Empty;
    private void GlideArt(double x, double y, double w)
    {
        artTarget = new(x, y, w, w / 1.5);
        if (!moving) { artShown = artTarget; PlaceArt(artShown); }
    }
    public void PrepareMove(DockBounds target, bool hero, bool secondary)
    {
        var from = RenderedBounds;
        artMoveFrom = artShown;
        SettleArtwork(1, 0);
        SetMoving(true);
        // Compute the destination once, then restore the displayed outer bounds before the next render.
        SetBounds(target, hero, secondary); artMoveTo = artTarget;
        Canvas.SetLeft(this, from.X); Canvas.SetTop(this, from.Y); Width = from.Width; Height = from.Height;
        if (artMoveFrom.IsEmpty) artMoveFrom = artMoveTo;
        SetMoveProgress(0);
    }
    public void SetMoveProgress(double progress)
    {
        if (artMoveFrom.IsEmpty || artMoveTo.IsEmpty) return;
        double p = Math.Clamp(progress, 0, 1);
        artShown = new(artMoveFrom.X + (artMoveTo.X - artMoveFrom.X) * p,
            artMoveFrom.Y + (artMoveTo.Y - artMoveFrom.Y) * p,
            artMoveFrom.Width + (artMoveTo.Width - artMoveFrom.Width) * p,
            artMoveFrom.Height + (artMoveTo.Height - artMoveFrom.Height) * p);
        PlaceArt(artShown);
    }
    private void PlaceArt(Rect r)
    {
        double artX = r.X, artY = r.Y, artWidth = r.Width;
        Art.Width = artWidth; Art.Height = artWidth / 1.5; Canvas.SetLeft(Art, artX); Canvas.SetTop(Art, artY);
        Halo.Width = artWidth * 1.25; Halo.Height = artWidth / 1.5 * 1.05; Canvas.SetLeft(Halo, artX - artWidth * .125); Canvas.SetTop(Halo, artY - artWidth / 1.5 * .02);
        ArtShadow.Width = artWidth * .72; ArtShadow.Height = Math.Max(8, artWidth * .075); Canvas.SetLeft(ArtShadow, artX + artWidth * .14); Canvas.SetTop(ArtShadow, artY + artWidth / 1.5 * .9);
        PlaceAddButton(r);
    }
    private void PlaceAddButton(Rect art) => AddControllerButton.Margin = new(Math.Max(12, art.X + art.Width / 2 - 73),
        Math.Max(0, Math.Min(Height - Inset - AddButton, art.Y + art.Height * .96 + 8)), 0, 0);
    /// <summary>The one accent: Steam's blue, for the chosen controller and its bloom.</summary>
    public static readonly Color Accent = Color.FromRgb(26, 159, 255);
    // the card's own 6 px margin plus breathing room; the heights of the status pill and the small buttons
    private const double Inset = 20, StatusHeight = 26, ActionGap = 12, SideButton = 32, AddButton = 36;
    public event Action<int>? Selected;
    public event Action<int>? RemoveRequested;
    private void AddController(object sender,RoutedEventArgs e){e.Handled=true;ActionRequested?.Invoke(Index,false);}
    private void RemoveController(object sender,RoutedEventArgs e){e.Handled=true;RemoveRequested?.Invoke(Index);}
    public event Action<int, bool>? ActionRequested;
    public DockView(int index, PlayerDock player, bool reducedMotion=false)
    {
        this.reducedMotion=reducedMotion||!SystemParameters.ClientAreaAnimation;
        InitializeComponent(); Index = index; Player = player;
        // the empty card's dashed controller is the X20's own traced outline, and its plus does what Choose Controller does
        SilhouettePath.Data = GamepadGlyph.Outline;
        // the three dots blink one after another, like someone about to speak (owner request, 14 Oct 2026); only while the
        // empty card shows, at a low frame rate, so an idle Zone stays cheap
        var dots = ((StackPanel)((StackPanel)PlusBadge.Child).Children[0]).Children.OfType<System.Windows.Shapes.Ellipse>().ToList();
        void Dots(bool on)
        {
            for (int i = 0; i < dots.Count; i++)
            {
                if (!on || this.reducedMotion) { dots[i].BeginAnimation(OpacityProperty, null); dots[i].Opacity = .7; continue; }
                var pulse = new DoubleAnimationUsingKeyFrames { RepeatBehavior = RepeatBehavior.Forever, Duration = TimeSpan.FromMilliseconds(1500), BeginTime = TimeSpan.FromMilliseconds(220 * i) };
                pulse.KeyFrames.Add(new LinearDoubleKeyFrame(.30, TimeSpan.Zero)); pulse.KeyFrames.Add(new EasingDoubleKeyFrame(.75, TimeSpan.FromMilliseconds(330), new SineEase()));
                pulse.KeyFrames.Add(new EasingDoubleKeyFrame(.30, TimeSpan.FromMilliseconds(760), new SineEase())); pulse.KeyFrames.Add(new LinearDoubleKeyFrame(.30, TimeSpan.FromMilliseconds(1500)));
                Timeline.SetDesiredFrameRate(pulse, 24); dots[i].Opacity = .18; dots[i].BeginAnimation(OpacityProperty, pulse);
            }
        }
        Silhouette.IsVisibleChanged += (_, _) => Dots(Silhouette.IsVisible);
        Loaded += (_, _) => Dots(Silhouette.IsVisible); Unloaded += (_, _) => Dots(false);
        var plusScale = new ScaleTransform(1, 1);
        var plusContent = (FrameworkElement)PlusBadge.Child; plusContent.RenderTransform = plusScale; plusContent.RenderTransformOrigin = new(.5, .5);
        void Grow(double to) { if (this.reducedMotion) return; plusScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(to, TimeSpan.FromMilliseconds(140)) { EasingFunction = new CubicEase() }); plusScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(to, TimeSpan.FromMilliseconds(140)) { EasingFunction = new CubicEase() }); }
        PlusBadge.MouseEnter += (_, _) => Grow(1.015); PlusBadge.MouseLeave += (_, _) => Grow(1);
        PlusBadge.PreviewMouseLeftButtonDown += (_, e) => { e.Handled = true; Grow(.92); };
        PlusBadge.PreviewMouseLeftButtonUp += (_, e) => { e.Handled = true; Grow(1.015); ActionRequested?.Invoke(Index, false); };
        FocusButton.GotKeyboardFocus += (_, _) => UpdateCard(); FocusButton.LostKeyboardFocus += (_, _) => UpdateCard();
        FocusButton.MouseEnter += (_, _) => UpdateCard(); FocusButton.MouseLeave += (_, _) => UpdateCard();
        PlayerLabel.Text = $"Player {player.Number}";
        // the pad's colour: picked here, remembered per player, changed with the Venom morph (owner direction 24 Oct 2026)
        Morph = new FinishMorph(ControllerImage); ImageClip.Children.Insert(1, Morph);
        ColourButton = new Button { Content = "Change color", Style = (Style)FindResource("ActionButton"), Height = 38, Width = 150, Visibility = Visibility.Collapsed };
        ColourButton.Click += (_, e) => { e.Handled = true; ChangeColour(); }; ActionCanvas.Children.Add(ColourButton);
        FocusButton.TabIndex = index;
        RefreshPlayer(player);
    }
    public void RefreshPlayer(PlayerDock player)
    {
        Player = player;
        AddControllerButton.Visibility=player.Model==null?Visibility.Visible:Visibility.Collapsed;
        Halo.Visibility=player.ModelId is "x20" or "x20_pro" or "x05_pro" or "d10" or "x15" or "dune"?Visibility.Visible:Visibility.Collapsed;
        Halo.Fill=new LinearGradientBrush(Color.FromArgb(20,215,69,160),Color.FromArgb(20,54,187,230),0);Halo.OpacityMask=new RadialGradientBrush(Colors.White,Colors.Transparent);
        RemoveControllerButton.Visibility=player.Model==null?Visibility.Collapsed:Visibility.Visible;
        System.Windows.Automation.AutomationProperties.SetName(FocusButton, $"Player {player.Number}, {player.Model ?? "empty"}, focus controller dock");
        if (player.Model != null && (new[] { "x20", "x20_pro", "x15", "x05_pro", "x10", "d10", "dune" }.Contains(player.ModelId) || player.IsMockConnected))
        {
            string outlineModel = player.ModelId ?? (player.Model switch { "EasySMX X15" => "x15", "EasySMX X05 Pro" => "x05_pro", _ => "x20" });
            if (Morph.ModelId != outlineModel || Morph.Finish != PadColours.For(Index, outlineModel)) Morph.Show(outlineModel, PadColours.For(Index, outlineModel));
            ArtOutline.Show(outlineModel, false, Morph.Finish);
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
        ModelName.Text = Player.Model ?? "";
        ConnectionBadge.Visibility=empty?Visibility.Collapsed:Visibility.Visible;
        ModelName.TextTrimming = TextTrimming.CharacterEllipsis;
        Connection.Text = empty ? (hero ? "Assign a controller to this player" : "Empty") : Player.IsMockConnected ? "Connected · mock" : "Not connected";
        StatusDot.Fill = DS.Brush(!empty && Player.IsMockConnected ? "Zone.SemanticSuccess" : "Zone.SemanticWarning");
        Connection.Foreground=StatusDot.Fill;ConnectionBadge.Background=new SolidColorBrush(Player.IsMockConnected?Color.FromArgb(30,120,195,157):Color.FromArgb(30,217,182,117));
        double artWidth, artX, artY, detailsX, detailsY, detailsWidth;
        if (hero)
        {
            // the hero square: the controller large in the middle, its name and the actions along the bottom
            Canvas.SetLeft(PlayerPill, 28); Canvas.SetTop(PlayerPill, 26); PlayerLabel.FontSize = 24;
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
            Canvas.SetLeft(PlayerPill, 18); Canvas.SetTop(PlayerPill, 16); PlayerLabel.FontSize = 16;
            artWidth = Math.Max(30, Math.Min(w * .48, (h - 58) * 1.5)); artX = w - artWidth - 8; artY = 48 + (h - 58 - artWidth / 1.5) / 2;
            ModelName.FontSize = 16; detailsX = 20; detailsWidth = Math.Max(40, artX - 28);
            // name, status and Remove are one block, centred in the room under the label and kept off the card's bottom
            // edge (owner report 17 Oct 2026: Remove hung out of the tile)
            double block = ModelName.FontSize * 1.34 + 2 + StatusHeight + ActionGap + SideButton;
            detailsY = Math.Clamp(44 + (h - 44 - Inset - block) / 2, 44, Math.Max(44, h - Inset - block));
            if (empty)
            {   // an empty tile is just the "add" target: the dashed controller in the middle, Add controller under it
                double room = Math.Max(20, h - 40 - Inset - AddButton - 8);
                artWidth = Math.Max(30, Math.Min(w * .42, room * 1.5)); artX = (w - artWidth) / 2; artY = 40 + (room - artWidth / 1.5) / 2;
                detailsY = h - 58; detailsWidth = Math.Max(40, artX - 28);
            }
        }
        else
        {
            // a grid card: the controller centred, its name and status bottom-left
            Canvas.SetLeft(PlayerPill, 22); Canvas.SetTop(PlayerPill, 20); PlayerLabel.FontSize = 20;
            detailsX = 28; detailsY = h - 122; detailsWidth = w - 120; ModelName.FontSize = 23;
            artWidth=Math.Max(40,Math.Min(w*.42,Math.Max(40,detailsY-20)*1.5));
            artX=(w-artWidth)/2;artY=Math.Max(10,Math.Min(46,(detailsY-artWidth/1.5)/2));
        }
        if(empty && !hero && !secondary)
        {
            double room=h-64-artWidth/1.5*.96;
            artY=Math.Max(artY,Math.Min(room,artY+Math.Clamp(h*.1,18,30)));
            detailsY=h-68; // empty slots have no model metadata; keep the placeholder/button group lower
        }
        if (roleChanged) UpdateCard();
        // the + sits in the middle of the controller body (above the grips), not the middle of the outline's box
        PlusBadge.Width=Math.Clamp(artWidth*.16,38,64);PlusBadge.Height=Math.Clamp(artWidth*.20,48,80);
        if(PlusBadge.Child is StackPanel plusContent){((TextBlock)plusContent.Children[1]).FontSize=Math.Clamp(artWidth*.09,24,38);foreach(var dot in ((StackPanel)plusContent.Children[0]).Children.OfType<System.Windows.Shapes.Ellipse>()){dot.Width=dot.Height=Math.Clamp(artWidth*.018,5,9);dot.Margin=new(3,0,3,0);}}
        PlusBadge.Margin = new(0, 0, 0, artWidth / 1.5 * .2);
        GlideArt(artX, artY, artWidth);
        // the buttons' real heights (the shared style's 42 px minimum would otherwise win) and a clear gap above the card's edge
        AddControllerButton.MinHeight=0;AddControllerButton.Height=AddButton;
        if (!artShown.IsEmpty) PlaceAddButton(artShown);
        RemoveControllerButton.HorizontalAlignment=HorizontalAlignment.Left;
        RemoveControllerButton.MinHeight=0;RemoveControllerButton.Width=hero?112:96;RemoveControllerButton.Height=hero?38:SideButton;
        RemoveControllerButton.Margin=hero?new(w-130-30-112-12,h-66,0,0):new(detailsX,Math.Min(h-Inset-SideButton,detailsY+ModelName.FontSize*1.34+2+StatusHeight+ActionGap),0,0);
        // a soft halo behind the controller (or the empty outline), and the arrow on grid cards
        // (the halo and floor shadow follow the art in PlaceArt)
        ArrowBadge.Visibility = Visibility.Collapsed; Canvas.SetLeft(ArrowBadge, w - 74); Canvas.SetTop(ArrowBadge, h - 66);
        Stripes.Visibility = Visibility.Collapsed;
        // a soft floor shadow under the controller so it stands off the glass
        // (see PlaceArt)
        ArtShadow.Visibility = Illustration.Visibility;
        Canvas.SetLeft(Details, detailsX); Canvas.SetTop(Details, detailsY); Details.Width = detailsWidth; Details.Height = hero ? 80 : secondary ? 50 : 88;
        ModelName.MaxWidth = detailsWidth;
        Canvas.SetTop(ConnectionBadge, ModelName.FontSize * 1.34 + (hero ? 4 : 2));
        ActionCanvas.Visibility = hero && !empty ? Visibility.Visible : Visibility.Collapsed;
        Primary.Content = empty ? "Add controller" : "Enter Studio";
        Primary.Width = empty ? 150 : 130;
        Canvas.SetLeft(Primary, w - Primary.Width - 30); Canvas.SetTop(Primary, h - 66);
        Canvas.SetLeft(Switch, 30); Canvas.SetTop(Switch, h - 66);
        Switch.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        Canvas.SetLeft(ColourButton, 30 + Switch.Width + 12); Canvas.SetTop(ColourButton, h - 66);
        ColourButton.Visibility = !empty && Player.ModelId != null && PadColours.Catalog.For(Player.ModelId).Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        System.Windows.Automation.AutomationProperties.SetName(ColourButton, $"Player {Player.Number}, Change color");
        System.Windows.Automation.AutomationProperties.SetName(Primary, $"Player {Player.Number}, {(empty ? "Choose Controller" : "Enter Studio")}");
        System.Windows.Automation.AutomationProperties.SetName(Switch, $"Player {Player.Number}, Switch Controller");
    }
    /// <summary>The chosen controller is lit in the accent blue with a soft glow; focus adds a white edge and a small lift.</summary>
    private void UpdateCard()
    {
        bool focused=FocusButton.IsKeyboardFocused,hover=FocusButton.IsMouseOver;
        Card.BorderThickness=new(focused?2:isHero?1.5:1);
        Card.BorderBrush=focused?Brushes.White:DS.Brush(isHero?"Zone.BorderSelected":"Zone.BorderDefault");
        Card.Background=DS.Brush(isHero?"Zone.ContentSurfaceSelected":hover?"Zone.ContentSurfaceHover":"Zone.ContentSurface");
        Card.Effect=null;
        ArrowBadge.Opacity=focused||hover||isHero?1:.55;
        LiftScale.BeginAnimation(ScaleTransform.ScaleXProperty,null);LiftScale.BeginAnimation(ScaleTransform.ScaleYProperty,null);LiftScale.ScaleX=LiftScale.ScaleY=1;
    }
    private bool moving;
    /// <summary>The Zone is gliding the cards: the glow is held back until the move ends, then fades in.</summary>
    public void SetMoving(bool on, int fadeInMs = 0)
    {
        if (moving == on) return; moving = on; UpdateCard();
        // the photo is resampled at every in-between size; high-quality scaling of a 1536 px image each frame is the other cost
        RenderOptions.SetBitmapScalingMode(ControllerImage, on ? BitmapScalingMode.Linear : BitmapScalingMode.HighQuality);
        if (on && Morph.IsChanging) _ = Morph.ChangeTo(Morph.Finish, false);
        if (!on && fadeInMs > 0 && Card.Effect is System.Windows.Media.Effects.DropShadowEffect glow)
            glow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.OpacityProperty, new DoubleAnimation(0, glow.Opacity, TimeSpan.FromMilliseconds(fadeInMs)));
    }
    public void FadeHeroDetails(double value, int duration)
    {
        foreach (var element in new UIElement[] { Details, ActionCanvas, PlayerPill, AddControllerButton, RemoveControllerButton })
            Animate(element, OpacityProperty, element.Opacity, value, reducedMotion ? 0 : duration);
        ActionCanvas.IsHitTestVisible = AddControllerButton.IsHitTestVisible = RemoveControllerButton.IsHitTestVisible = value > 0;
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
    public FinishMorph Morph { get; }
    public Button ColourButton { get; }
    /// <summary>The next colour this model comes in, wrapping round; remembered for this player and model.</summary>
    public Task ChangeColour()
    {
        if (Player.ModelId is not string model || Morph.ModelId != model) return Task.CompletedTask;
        var next = PadColours.Catalog.Next(model, Morph.Finish);
        PadColours.Set(Index, model, next.Id);
        ColourButton.ToolTip = next.Name;
        return Morph.ChangeTo(next.Id, !reducedMotion);
    }
    private void SelectPlayer(object sender, RoutedEventArgs e) => Selected?.Invoke(Index);
    private void PrimaryAction(object sender, RoutedEventArgs e) => ActionRequested?.Invoke(Index, false);
    private void SwitchAction(object sender, RoutedEventArgs e) => ActionRequested?.Invoke(Index, true);
}
