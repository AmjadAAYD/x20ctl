using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Rebel;
using X20Ctl.Zone;
namespace X20Ctl.Product;

/// <summary>The Rebel theme's Controller Zone (owner boards zone.png and hero.png): a wall of calling cards over a red slash, the
/// title cut out of ransom letters, and, for the chosen player, the pad thrown large with its actions around the face buttons.
/// The four docks, the keyboard and controller navigation and every action are the Zone's own; only the picture is new.</summary>
public partial class ZoneScene
{
    internal bool IsRebel { get; private set; }
    private Grid? board; private Canvas? zoneBack, heroBack; private FrameworkElement? zoneTitle, caption, hintsOverview, hintsHero, statusPill; private Button? addCard;
    private TextBlock? captionText, statusWho; private int rebelOn;
    private static readonly string[] Hints = ["A Select", "Y Change", "B Back"];
    private const double CardW = 250, CardH = 446, CardGap = 40, StageX = 96, StageY = 196;

    private static void Put(FrameworkElement e, double x, double y) { e.HorizontalAlignment = HorizontalAlignment.Left; e.VerticalAlignment = VerticalAlignment.Top; e.Margin = new(x, y, 0, 0); }
    private static Polygon Poly(Brush fill, params double[] xy) => new() { Fill = fill, Points = new PointCollection(Enumerable.Range(0, xy.Length / 2).Select(i => new Point(xy[2 * i], xy[2 * i + 1]))), IsHitTestVisible = false };
    private static Brush Radial(double cx, double cy, double rx, double ry, Color color, double stop)
    {
        var b = new RadialGradientBrush(color, Colors.Transparent) { Center = new(cx, cy), GradientOrigin = new(cx, cy), RadiusX = rx, RadiusY = ry };
        b.GradientStops.Clear(); b.GradientStops.Add(new(color, 0)); b.GradientStops.Add(new(Ink.A(color, 0), stop)); return b;
    }
    private Canvas Backdrop(bool hero)
    {
        var c = new Canvas { Width = 1600, Height = 900, IsHitTestVisible = false };
        c.Children.Add(new Rectangle { Width = 1600, Height = 900, Fill = Ink.B(Ink.Black) });
        c.Children.Add(new Rectangle { Width = 1600, Height = 900, Fill = Radial(1, 0, .7, .6, Ink.A(Ink.Red, .14), .6) });
        c.Children.Add(new Rectangle { Width = 1600, Height = 900, Fill = Radial(0, 1, .6, .5, Ink.A(Ink.C("#7d0815"), .22), .7) });
        if (!hero)
        {
            c.Children.Add(Poly(Ink.B(Ink.Red), 0, 370, 1600, 240, 1600, 610, 0, 740));
            c.Children.Add(Poly(Ink.B(Ink.White), 0, 356, 1600, 226, 1600, 234, 0, 364));
            c.Children.Add(Dots.Field(0, 220, 1600, 540, Ink.A(Ink.Black, .5), 12, 2.6, new LinearGradientBrush(Colors.Black, Colors.Transparent, new Point(0, 1), new Point(0, 0)) { GradientStops = { new(Colors.Black, 0), new(Colors.Transparent, .7) } }));
            var r = new Random(31);
            for (int i = 0; i < 5; i++) { var s = Star.Make(r.Next(100, 1540), r.Next(120, 180), 7 + r.NextDouble() * 8, (r.NextDouble() - .5) * 50, .3 + r.NextDouble() * .4); c.Children.Add(s); }
        }
        else
        {
            var ray = Ink.B(Ink.A(Ink.C("#2c060c"), .8));
            for (int i = 0; i < 22; i++)
            {
                double a = i * 2 * Math.PI / 22, L = 1500, w = .09;
                c.Children.Add(Poly(ray, 1080, 440, 1080 + L * Math.Cos(a), 440 + L * Math.Sin(a), 1080 + L * Math.Cos(a + w), 440 + L * Math.Sin(a + w)));
            }
            c.Children.Add(Poly(Ink.B(Ink.Red), 1330, -40, 1620, -40, 1170, 940, 880, 940));
            c.Children.Add(Poly(Ink.B(Ink.A(Ink.White, .9)), 1640, -40, 1656, -40, 1206, 940, 1190, 940));
            c.Children.Add(Dots.Field(820, 0, 780, 900, Ink.A(Ink.Black, .5), 13, 2.8, new LinearGradientBrush(Colors.Black, Colors.Transparent, new Point(.33, 0), new Point(.67, 1)) { GradientStops = { new(Colors.Black, .1), new(Colors.Transparent, .6) } }));
        }
        return c;
    }
    /// <summary>Replaces the Default arrangement with the Rebel boards; called once, before the first layout.</summary>
    private void BuildRebel()
    {
        IsRebel = true; var original = Surface.Children.OfType<Grid>().First();
        EnvironmentBackdrop.Visibility = Visibility.Collapsed; OuterFrame.Background = Ink.B(Ink.Black); OuterFrame.BorderBrush = Ink.B(Ink.Black);
        board = new Grid { Width = 1600, Height = 900, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, ClipToBounds = false };
        zoneBack = Backdrop(false); heroBack = Backdrop(true); heroBack.Visibility = Visibility.Collapsed; board.Children.Add(zoneBack); board.Children.Add(heroBack);
        // the title, cut from ransom letters (not the Default's "Player slots")
        var title = Ransom.Make("Controller Zone", 54); title.Name = "RebelZoneTitle"; title.RenderTransformOrigin = new(.5, .5); title.RenderTransform = new RotateTransform(-2); Put(title, 80, 30); board.Children.Add(title); zoneTitle = title;
        // the stage the four calling cards stand on: moved out of the Default's workspace
        Workspace.Children.Remove(DockStage); DockStage.ClearValue(Grid.RowProperty); DockStage.Margin = new(0); DockStage.HorizontalAlignment = HorizontalAlignment.Left; DockStage.VerticalAlignment = VerticalAlignment.Top;
        Workspace.Children.Remove(FeedbackText); board.Children.Add(FeedbackText);
        board.Children.Add(DockStage);
        // the fifth card adds a controller to the first empty slot
        var add = new Slab { Fill = Ink.B(Ink.C("#0e0d0f")), Line = Ink.B(Ink.A(Ink.White, .4)), LineWidth = 2, Dash = new DoubleCollection([5, 4]), Seed = 80, Width = CardW, Height = CardH, RenderTransformOrigin = new(.5, .5), RenderTransform = new RotateTransform(-3) };
        var star = new Canvas { Width = 100, Height = 100, Margin = new(CardW / 2 - 50, 120, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top }; star.Children.Add(Star.Make(50, 50, 40));
        var plus = new TextBlock { Text = "", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 24, Foreground = Brushes.White, Margin = new(CardW / 2 - 12, 158, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        var words = Ink.Text("Add controller", 24); words.Margin = new(0, 250, 0, 0); words.HorizontalAlignment = HorizontalAlignment.Center; words.VerticalAlignment = VerticalAlignment.Top;
        add.Children.Add(star); add.Children.Add(plus); add.Children.Add(words);
        addCard = new Button { Content = add, Width = CardW, Height = CardH, Template = Plain(), FocusVisualStyle = null, Cursor = System.Windows.Input.Cursors.Hand };
        System.Windows.Automation.AutomationProperties.SetName(addCard, "Add controller"); addCard.Click += (_, _) => { int slot = Array.FindIndex(State.Players.ToArray(), p => p.Model == null); if (slot >= 0) ControllerActionRequested?.Invoke(slot, false); };
        addCard.GotKeyboardFocus += (_, _) => add.Fill = Ink.B(Ink.C("#1b181c")); addCard.LostKeyboardFocus += (_, _) => add.Fill = Ink.B(Ink.C("#0e0d0f"));
        Put(addCard, StageX + 4 * (CardW + CardGap), StageY); board.Children.Add(addCard);
        // the caption bar: whose card is lit
        captionText = Ink.Text("", 19, Ink.B(Ink.Black)); captionText.Margin = new(24, 0, 0, 0); captionText.VerticalAlignment = VerticalAlignment.Center;
        var bar = new Slab { Fill = Ink.B(Ink.White), Seed = 91, Spike = 16, Under = Ink.B(Ink.Red), Width = 560, Height = 52, Rot = -1.5 }; bar.Children.Add(captionText); caption = bar; Put(bar, 96, 784); board.Children.Add(bar);
        // the status pill with the clock; the Default's header buttons stay as small controls in the corner
        var clock = HeaderActions.Children.OfType<HeaderClock>().First(); HeaderActions.Children.Remove(clock);
        clock.FontFamily = Ink.Heavy; clock.RenderTransform = Ink.Slant; clock.RenderTransformOrigin = new(.5, .5); clock.FontSize = 17; clock.Foreground = Ink.B(Ink.Hot); clock.Margin = new(14, 0, 0, 0); clock.MinWidth = 0;
        statusWho = Ink.Text("Player 1", 17);
        var pill = new Border
        {
            Background = Ink.B(Ink.Black), BorderBrush = Ink.B(Ink.A(Ink.White, .3)), BorderThickness = new(1.5), Padding = new(20, 8, 20, 8), Effect = Ink.Shadow(6, 6, 0, Ink.Red, 1),
            RenderTransformOrigin = new(.5, .5), RenderTransform = new TransformGroup { Children = { new SkewTransform(-10, 0), new RotateTransform(-2) } },
            Child = new StackPanel { Orientation = Orientation.Horizontal, Children = { statusWho, clock } }
        };
        pill.HorizontalAlignment = HorizontalAlignment.Right; pill.VerticalAlignment = VerticalAlignment.Top; pill.Margin = new(0, 34, 42, 0); statusPill = pill; board.Children.Add(pill);
        hintsOverview = Parts.Hints(("A", "Select"), ("Y", "Change"), ("B", "Back")); hintsHero = Parts.Hints(("A", "Open Studio"), ("B", "Back")); hintsHero.Visibility = Visibility.Collapsed;
        foreach (var h in new[] { hintsOverview, hintsHero }) { h.HorizontalAlignment = HorizontalAlignment.Right; h.VerticalAlignment = VerticalAlignment.Top; h.Margin = new(0, 836, 40, 0); board.Children.Add(h); }
        // the brand mark stays where the intro lands its X (beside the status pill)
        original.Children.Remove(HeaderBar); var tile = BrandTile; var tileParent = (Panel)tile.Parent; tileParent.Children.Remove(tile); tile.Width = tile.Height = 46; Put(tile, 1296, 40); BrandMark.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(System.IO.Path.Combine(AppContext.BaseDirectory, "Brand", "themes", "rebel", "logo.png"))); board.Children.Add(tile);
        var strip = HeaderActions; ((Panel)strip.Parent).Children.Remove(strip); Grid.SetColumn(strip, 0); var chrome = RebelChrome.Fit(strip, 580);
        var notice = UpdateNotice; ((Panel)notice.Parent).Children.Remove(notice); Grid.SetColumn(notice, 0); notice.Width = 560; notice.HorizontalAlignment = HorizontalAlignment.Right; notice.VerticalAlignment = VerticalAlignment.Top; notice.Margin = new(0, 100, 42, 0);
        original.Visibility = Visibility.Collapsed;
        Surface.Children.Add(board); board.Children.Add(chrome); board.Children.Add(notice);
        // stage geometry: the row of cards, or the whole board for the hero
        DockStage.SizeChanged += (_, _) => { };
        foreach (var d in docks) HookRebelDock(d);
        RebelSync();
    }
    private static ControlTemplate Plain()
    {
        var t = new ControlTemplate(typeof(Button)); var f = new FrameworkElementFactory(typeof(Border)); f.SetValue(Border.BackgroundProperty, Brushes.Transparent); f.AppendChild(new FrameworkElementFactory(typeof(ContentPresenter))); t.VisualTree = f; return t;
    }
    private void HookRebelDock(DockView d)
    {
        d.FocusButton.GotKeyboardFocus += (_, _) => { rebelOn = d.Index; RebelSync(); };
        d.FocusButton.MouseEnter += (_, _) => { if (!State.FocusedPlayer.HasValue && rebelOn != d.Index) { rebelOn = d.Index; RebelSync(); } };
        d.BackRequested += () => _ = FocusPlayerAsync(null);
        d.PageRequested += page => StudioPageRequested?.Invoke(d.Index, page);
    }
    /// <summary>Shows what the focus says: the overview or the hero, which card is lit, the caption and the hints.</summary>
    private void RebelSync()
    {
        if (!IsRebel || board == null) return;
        bool hero = State.FocusedPlayer.HasValue; int on = hero ? State.FocusedPlayer!.Value : Math.Clamp(rebelOn, 0, 3);
        zoneBack!.Visibility = hero ? Visibility.Collapsed : Visibility.Visible; heroBack!.Visibility = hero ? Visibility.Visible : Visibility.Collapsed;
        zoneTitle!.Visibility = caption!.Visibility = hero ? Visibility.Collapsed : Visibility.Visible;
        hintsOverview!.Visibility = hero ? Visibility.Collapsed : Visibility.Visible; hintsHero!.Visibility = hero ? Visibility.Visible : Visibility.Collapsed;
        bool free = State.Players.Any(p => p.Model == null); addCard!.Visibility = !hero && free ? Visibility.Visible : Visibility.Collapsed;
        double w = hero ? 1600 : 4 * CardW + 3 * CardGap, h = hero ? 900 : CardH; Put(DockStage, hero ? 0 : StageX, hero ? 0 : StageY); DockStage.Width = w; DockStage.Height = h;
        for (int i = 0; i < docks.Length; i++) { docks[i].Visibility = !hero || i == on ? Visibility.Visible : Visibility.Collapsed; docks[i].SetRebelOn(i == on); }
        statusWho!.Text = $"Player {on + 1}";
        var p = State.Players[on]; var dock = docks[on]; string finish = dock.FinishName;
        captionText!.Text = p.Model == null ? $"Player {on + 1} · Empty" : $"Player {on + 1} · {p.Model}{(finish.Length > 0 ? " · " + finish : "")}";
        DockStage.UpdateLayout(); if (DockStage.ActualWidth > 48) SnapLayout();
    }
    private DockBounds[] RebelBounds()
    {
        var b = new DockBounds[4]; int? hero = State.FocusedPlayer;
        for (int i = 0; i < 4; i++) b[i] = hero == i ? new(0, 0, DockStage.ActualWidth, DockStage.ActualHeight) : new(i * (CardW + CardGap), 0, CardW, CardH);
        return b;
    }
    private DockBounds[] Bounds() => IsRebel ? RebelBounds() : IsHybrid ? HybridBounds() : IsDusk ? DuskBounds() : ZoneLayout.Calculate(DockStage.ActualWidth, DockStage.ActualHeight, State.FocusedPlayer);
    /// <summary>Hard and fast, as the theme asks: the Zone snaps between overview and hero.</summary>
    private Task RebelFocus(int? index)
    {
        transition?.Cancel(); if (index.HasValue) State.Activate(index.Value); else State.Collapse();
        FeedbackText.Text = State.Feedback; UpdateOverviewPill(); if (index.HasValue) rebelOn = index.Value; RebelSync();
        if (index.HasValue) docks[index.Value].FocusButton.Focus();
        return Task.CompletedTask;
    }
}
