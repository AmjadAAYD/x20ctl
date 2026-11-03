using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using X20Ctl.Product.Rebel;
namespace X20Ctl.Product;

/// <summary>The Rebel theme's player card (owner boards zone.png and hero.png): a calling card pinned or taped on the wall, or, for the
/// chosen player, the pad thrown large with its command cluster. The dock's own controls and events stay; this draws and places.</summary>
public partial class DockView
{
    private const double CardW = 250;
    private static readonly double[] Tilt = [-4, 3, -2, 3.5];
    private bool rebel, lit; private Canvas? face, heroLayer; private string faceKey = ""; private RotateTransform? cardTilt, padTilt;
    private SlabButton? studio, change, colour, back, remove, settings;
    public event Action? BackRequested;
    public event Action<string>? PageRequested;
    /// <summary>The name of this player's pad finish, as the colour picker lists it.</summary>
    public string FinishName => Player.ModelId is string m ? PadColours.Catalog.For(m).FirstOrDefault(f => f.Id == Morph.Finish)?.Name ?? "" : "";
    public void SetRebelOn(bool on) { if (lit == on) return; lit = on; if (rebel && !double.IsNaN(Width)) PresentRebel(isHero); }
    private void InitRebel()
    {
        if (ThemeCatalog.Current.Id != "rebel") return;
        rebel = true; var root = (Grid)Content; root.ClipToBounds = false; Card.Visibility = Visibility.Collapsed;
        face = new Canvas { IsHitTestVisible = false }; root.Children.Insert(0, face);
        heroLayer = new Canvas { Visibility = Visibility.Collapsed }; root.Children.Add(heroLayer);
        cardTilt = new RotateTransform(); RenderTransformOrigin = new(.5, .5); RenderTransform = cardTilt;
        padTilt = new RotateTransform(); Art.RenderTransform = new TransformGroup { Children = { ArtScale, padTilt } };
        var red = Ink.C("#ff6a7a");
        studio = new SlabButton("Open Studio", 300, 66, 104, -5, 27, Ink.Black, true); change = new SlabButton("Test controller", 236, 50, 102, -4, 20, Ink.White, false, HorizontalAlignment.Right);
        colour = new SlabButton("Manage setups", 236, 48, 101, -4, 20, Ink.White); back = new SlabButton("Back", 130, 50, 103, -4, 20, Ink.White);
        settings = new SlabButton("Controller settings", 250, 44, 105, -2, 17, Ink.White, false, HorizontalAlignment.Left, "\uE713");
        remove = new SlabButton("Remove from slot", 230, 44, 106, -2, 17, red, false, HorizontalAlignment.Left, "");
        studio.Click += (_, _) => ActionRequested?.Invoke(Index, false); change.Click += (_, _) => PageRequested?.Invoke("Tester");
        colour.Click += (_, _) => PageRequested?.Invoke("Setups"); settings.Click += (_, _) => PageRequested?.Invoke("Device"); back.Click += (_, _) => BackRequested?.Invoke(); remove.Click += (_, _) => RemoveRequested?.Invoke(Index);
        foreach (var b in new Button[] { studio, change, colour, back, settings, remove }) heroLayer.Children.Add(b);
        System.Windows.Automation.AutomationProperties.SetName(back, $"Player {Index + 1}, back to the Controller Zone");
    }
    private static void At(Canvas c, UIElement e, double x, double y) { Canvas.SetLeft(e, x); Canvas.SetTop(e, y); c.Children.Add(e); }
    private static FrameworkElement Centered(double w, FrameworkElement e) { var g = new Grid { Width = w, IsHitTestVisible = false }; e.HorizontalAlignment = HorizontalAlignment.Center; g.Children.Add(e); return g; }
    private string ShortName => Player.ModelId switch { "x20" => "X20", "x20_pro" => "X20 Pro", "x15" => "X15", "x05_pro" => "X05 Pro", "d10" => "D10", "dune" => "Dune", "x10" => "X10", _ => (Player.Model ?? "").Replace("EasySMX ", "") };
    private void PresentRebel(bool hero)
    {
        if (face == null || heroLayer == null) return;
        bool empty = Player.Model == null; double w = Width, h = Height;
        foreach (UIElement e in new UIElement[] { PlayerPill, Details, Halo, ArtShadow, AddControllerButton, RemoveControllerButton, ActionCanvas, ArrowBadge, PlusBadge }) e.Visibility = Visibility.Collapsed;
        cardTilt!.Angle = hero ? 0 : Tilt[Index % 4]; FocusButton.IsHitTestVisible = !hero; heroLayer.Visibility = hero ? Visibility.Visible : Visibility.Collapsed;
        padTilt!.Angle = hero ? -7 : 0; Art.Effect = hero && !empty ? Ink.Shadow(18, 20, 0, Ink.Black, 1) : null;
        double u = w / CardW;
        if (hero) GlideArt(1100 - 410, 430 - 820 / 1.5 / 2, 820); else if (empty) GlideArt(20 * u, 160 * u, 210 * u); else GlideArt((w - 200 * u) / 2, 171 * u - 200 * u / 1.5 / 2, 200 * u);
        SilhouettePath.Stroke = Ink.B(Ink.A(Ink.White, empty && !hero ? .7 : .5)); SilhouettePath.Fill = Ink.B(Ink.A(Ink.White, .06));
        string key = $"{hero}|{empty}|{lit}|{Player.ModelId}|{Morph.Finish}|{(int)w}x{(int)h}|{Player.Number}|{Connection.Text}";
        if (key == faceKey) return; faceKey = key; face.Children.Clear();
        if (hero) HeroFace(empty); else CardFace(empty, u);
    }
    private void CardFace(bool empty, double u)
    {
        double w = Width, h = Height; var number = Ink.Text(Player.Number.ToString(), 24 * u, Ink.B(empty ? Ink.Grey : Ink.White)); number.HorizontalAlignment = HorizontalAlignment.Center; number.VerticalAlignment = VerticalAlignment.Center;
        if (empty)
        {
            face!.Children.Add(new Slab { Width = w, Height = h, Fill = Ink.B(Ink.C("#0e0d0f")), Line = Ink.B(Ink.A(Ink.White, .4)), LineWidth = 2, Dash = new DoubleCollection([5, 4]), Seed = 70 + Index, IsHitTestVisible = false });
            var ring = new Border { Width = 44 * u, Height = 44 * u, CornerRadius = new(22 * u), BorderBrush = Ink.B(Ink.A(Ink.White, .4)), BorderThickness = new(2), Child = number }; At(face, ring, 18 * u, 16 * u);
            At(face, Centered(w, Ink.Text("Empty", 24 * u, Ink.B(Ink.Grey))), 0, 350 * u);
            return;
        }
        At(face!, new TornPaper { Width = w, Height = h, Seed = Index + 21, UnderSheet = lit ? Ink.B(Ink.Red) : null }, 0, 0);
        if (lit) { var tape = new System.Windows.Shapes.Rectangle { Width = 120 * u, Height = 26 * u, Fill = Ink.B(Ink.A(Ink.Red, .92)), RenderTransform = new RotateTransform(-4), RenderTransformOrigin = new(.5, .5) }; At(face, tape, w / 2 - 60 * u, -14 * u); }
        else At(face, new System.Windows.Shapes.Ellipse { Width = 18 * u, Height = 18 * u, Fill = new RadialGradientBrush(Ink.C("#ff9aa4"), Ink.C("#4a030b")) { GradientOrigin = new(.35, .3), Center = new(.45, .45) }, Effect = Ink.Shadow(3, 6, 4, Colors.Black, .6) }, w / 2 - 9 * u, -1 * u);
        var badge = new Border { Width = 44 * u, Height = 44 * u, CornerRadius = new(22 * u), Background = Ink.B(lit ? Ink.Red : Ink.Black), Child = number }; At(face, badge, 18 * u, 18 * u);
        var tag = Ink.Label($"Player {Player.Number}", 12 * u, Ink.B(Ink.Black)); var tagBox = new Grid { Width = w - 36 * u, Height = 44 * u, IsHitTestVisible = false }; tag.HorizontalAlignment = HorizontalAlignment.Right; tag.VerticalAlignment = VerticalAlignment.Center; tagBox.Children.Add(tag); At(face, tagBox, 18 * u, 18 * u);
        var window = new Border { Width = w - 36 * u, Height = 190 * u, Background = Ink.B(Ink.Black), ClipToBounds = true, Child = new System.Windows.Shapes.Rectangle { Fill = Dots.Tile(Ink.A(Ink.Red, .5), 9 * u, 1.9 * u) } }; At(face, window, 18 * u, 76 * u);
        At(face, Centered(w, Ransom.Make(ShortName, 46 * u, (Index + 1) * 7)), 0, 284 * u);
        At(face, Centered(w, Ink.Text(FinishName, 20 * u, Ink.B(Ink.Cut))), 0, 346 * u);
    }
    private void HeroFace(bool empty)
    {
        var f = face!; var s = Ink.White;
        var crumb = new StackPanel { Orientation = Orientation.Horizontal }; crumb.Children.Add(Ink.Text("‹", 22, Ink.B(Ink.Hot))); var t = Ink.Text($"  Controller Zone / Player {Player.Number}", 18, Ink.B(Ink.C("#e6dfe1"))); t.VerticalAlignment = VerticalAlignment.Center; crumb.Children.Add(t); At(f, crumb, 80, 40);
        var block = new StackPanel(); block.Children.Add(Ink.Label($"Player {Player.Number}", 12));
        var ransom = Ransom.Make(empty ? "Empty" : ShortName, 150, 12); ransom.HorizontalAlignment = HorizontalAlignment.Left; ransom.Margin = new(0, 10, 0, 0); block.Children.Add(ransom);
        var name = new TextBlock { FontFamily = Ink.Heavy, FontSize = 28, RenderTransform = Ink.Slant, RenderTransformOrigin = new(.5, .5), Margin = new(0, 20, 0, 0), Foreground = Ink.B(s) };
        if (empty) name.Text = "Assign a controller to this player"; else { name.Inlines.Add(new System.Windows.Documents.Run($"{Player.Model} · ")); name.Inlines.Add(new System.Windows.Documents.Run(FinishName) { Foreground = Ink.B(Ink.Hot) }); }
        block.Children.Add(name);
        if (!empty) { var sm = Ink.Text(Connection.Text, 15, Ink.B(Ink.C("#9d9598")), false); sm.Margin = new(0, 6, 0, 0); block.Children.Add(sm); }
        At(f, block, 80, 108);
        if (empty) { studio!.Caption = "Add controller"; change!.Visibility = colour!.Visibility = remove!.Visibility = settings!.Visibility = Visibility.Collapsed; }
        else { studio!.Caption = "Open Studio"; change!.Visibility = colour!.Visibility = remove!.Visibility = settings!.Visibility = Visibility.Visible; }
        double kx = 400, ky = 592, d = 80;
        if (!empty)
        {
            var diamond = new System.Windows.Shapes.Rectangle { Width = 124, Height = 124, Fill = Ink.B(Ink.Black), Stroke = Ink.B(Ink.Red), StrokeThickness = 3, RenderTransformOrigin = new(.5, .5), RenderTransform = new RotateTransform(45) }; At(f, diamond, kx - 62, ky - 62);
            foreach (var (c, dx, dy) in new[] { ("Y", 0, -1), ("X", -1, 0), ("B", 1, 0), ("A", 0, 1) })
            {
                bool on = c == "A"; var g = Ink.Text(c, 30, Ink.B(on ? Colors.White : Ink.Black)); g.HorizontalAlignment = HorizontalAlignment.Center; g.VerticalAlignment = VerticalAlignment.Center;
                var cap = new Border { Width = 60, Height = 60, CornerRadius = new(30), Background = Ink.B(on ? Ink.Red : Ink.White), Child = g, Effect = on ? Ink.Shadow(6, 6, 0, Colors.Black, 1) : Ink.Shadow(5, 5, 0, Colors.Black, 1) };
                if (on) { cap.BorderBrush = Ink.B(Ink.White); cap.BorderThickness = new(4); }
                At(f, cap, kx + dx * d - 30, ky + dy * d - 30);
            }
        }
        Canvas.SetLeft(studio!, kx + 46); Canvas.SetTop(studio!, ky + d + 8); Canvas.SetLeft(change!, kx - d - 290); Canvas.SetTop(change!, ky - 26);
        Canvas.SetLeft(colour!, kx + 44); Canvas.SetTop(colour!, ky - d - 66); Canvas.SetLeft(back!, kx + d + 44); Canvas.SetTop(back!, ky - 26);
        Canvas.SetLeft(remove!, 334); Canvas.SetTop(remove!, 788); Canvas.SetLeft(settings!, 64); Canvas.SetTop(settings!, 792);
        if (empty) { Canvas.SetLeft(studio!, 80); Canvas.SetTop(studio!, 560); Canvas.SetLeft(back!, 420); Canvas.SetTop(back!, 570); }
    }
}
