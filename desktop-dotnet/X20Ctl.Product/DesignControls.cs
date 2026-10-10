using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
namespace X20Ctl.Product;

/// <summary>A true capsule: corner radius is half the element's own height. A fixed 999 radius makes WPF
/// scale the corners proportionally on wide buttons, which turns them into ovals.</summary>
public sealed class HalfHeightRadius : System.Windows.Data.IValueConverter
{
    public object Convert(object value, Type t, object p, System.Globalization.CultureInfo c) => new CornerRadius(value is double h && h > 0 ? h / 2 : 0);
    public object ConvertBack(object value, Type t, object p, System.Globalization.CultureInfo c) => throw new NotSupportedException();
}

internal static class DS
{
    public static Brush Brush(string key) => (Brush)Application.Current.FindResource(key);
    public static Style Style(string key) => (Style)Application.Current.FindResource(key);
    public static FontFamily Display => (FontFamily)Application.Current.FindResource("DS.FontDisplay");
    public static FontFamily Body => (FontFamily)Application.Current.FindResource("DS.FontBody");
    public static bool Motion => SystemParameters.ClientAreaAnimation;
    public static IEasingFunction EaseOut { get; } = Freeze(new CubicEase { EasingMode = EasingMode.EaseOut });
    public static IEasingFunction Spring { get; } = Freeze(new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = .32 });
    private static IEasingFunction Freeze(EasingFunctionBase e) { e.Freeze(); return e; }
    public static DoubleAnimation To(double to, double ms, IEasingFunction? ease = null) => new(to, TimeSpan.FromMilliseconds(ms)) { EasingFunction = ease ?? EaseOut };
}

/// <summary>Slowly drifting deep-space backdrop: two nebula clouds, three parallax star layers, gentle twinkle.
/// Cheap by design: a handful of cached DrawingBrush layers moved by GPU-friendly transforms.</summary>
public class GalaxyBackdrop : Grid
{
    private readonly bool motion;
    public GalaxyBackdrop() : this(DS.Motion) { }
    public GalaxyBackdrop(bool motion, bool navy = false)
    {
        this.motion = motion; IsHitTestVisible = false; ClipToBounds = true;
        if (navy)
        {
            // Steam's restraint, still alive: a navy field, deep-blue and steel-blue clouds drifting, stars on the move
            Background = new LinearGradientBrush(new GradientStopCollection { new(Color.FromRgb(16, 24, 38), 0), new(Color.FromRgb(11, 17, 28), .6), new(Color.FromRgb(7, 11, 18), 1) }, 90);
            Children.Add(Nebula(Color.FromArgb(150, 34, 82, 168), .18, .3, .7, .8, 18, 60, 0));
            Children.Add(Nebula(Color.FromArgb(110, 52, 96, 150), .84, .24, .55, .7, 21, -70, 5));
            Children.Add(Nebula(Color.FromArgb(90, 40, 64, 140), .62, .82, .5, .5, 24, 55, 9));
            Children.Add(Nebula(Color.FromArgb(70, 30, 120, 170), .06, .9, .45, .45, 16, -50, 3));
        }
        else
        {
            Background = new LinearGradientBrush(Color.FromRgb(5, 8, 20), Color.FromRgb(9, 15, 36), 90);
            // colourful, visibly moving nebulae: blue, violet, magenta and cyan clouds on 14-22 s cycles
            Children.Add(Nebula(Color.FromArgb(165, 40, 86, 210), .2, .38, .72, .85, 16, 60, 0));
            Children.Add(Nebula(Color.FromArgb(135, 128, 64, 220), .82, .22, .55, .7, 19, -70, 5));
            Children.Add(Nebula(Color.FromArgb(105, 205, 60, 170), .62, .78, .48, .5, 22, 55, 9));
            Children.Add(Nebula(Color.FromArgb(95, 30, 175, 205), .1, .92, .5, .45, 14, -50, 3));
            Children.Add(Nebula(Color.FromArgb(60, 255, 120, 80), .98, .62, .32, .4, 18, 40, 11));
        }
        var rng = new Random(20261010);
        Children.Add(Stars(rng, 260, .5, 1.1, .55, 26, 24));
        Children.Add(Stars(rng, 110, .9, 1.7, .8, 18, 40));
        Children.Add(Stars(rng, 34, 1.4, 2.6, 1, 12, 64));
        var vignette = new Rectangle { Fill = new RadialGradientBrush(Color.FromArgb(0, 0, 0, 0), Color.FromArgb(150, 2, 4, 10)) { RadiusX = .8, RadiusY = .85 } };
        vignette.CacheMode = new BitmapCache(.5); Children.Add(vignette);
    }
    private FrameworkElement Nebula(Color color, double cx, double cy, double rx, double ry, double seconds, double drift, double delay)
    {
        var shape = new Rectangle { Fill = new RadialGradientBrush(new GradientStopCollection { new(color, 0), new(Color.FromArgb((byte)(color.A / 3), color.R, color.G, color.B), .45), new(Color.FromArgb(0, color.R, color.G, color.B), 1) }) { Center = new(cx, cy), GradientOrigin = new(cx, cy), RadiusX = rx, RadiusY = ry }, Margin = new(-120) };
        shape.CacheMode = new BitmapCache(.5); // soft gradient: half resolution is invisible, and it is drawn once
        var move = new TranslateTransform(); shape.RenderTransform = move;
        if (motion)
        {
            var x = new DoubleAnimation(-drift, drift, TimeSpan.FromSeconds(seconds)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase(), BeginTime = TimeSpan.FromSeconds(-delay) };
            var y = new DoubleAnimation(drift * .6, -drift * .6, TimeSpan.FromSeconds(seconds * .73)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase(), BeginTime = TimeSpan.FromSeconds(-delay) };
            move.BeginAnimation(TranslateTransform.XProperty, x); move.BeginAnimation(TranslateTransform.YProperty, y);
            var pulse = new ScaleTransform(1, 1, 0, 0); shape.RenderTransform = new TransformGroup { Children = { pulse, move } };
            shape.RenderTransformOrigin = new(cx, cy);
            pulse.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(.92, 1.12, TimeSpan.FromSeconds(seconds * .55)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase() });
            pulse.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1.1, .94, TimeSpan.FromSeconds(seconds * .61)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase() });
            shape.BeginAnimation(OpacityProperty, new DoubleAnimation(.55, 1, TimeSpan.FromSeconds(seconds * .4)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase() });
        }
        return shape;
    }
    private FrameworkElement Stars(Random rng, int count, double min, double max, double brightness, double seconds, double drift)
    {
        var group = new DrawingGroup();
        using (var dc = group.Open())
        {
            dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, 1000, 1000));
            for (int i = 0; i < count; i++)
            {
                double r = min + rng.NextDouble() * (max - min); byte a = (byte)(255 * brightness * (.35 + rng.NextDouble() * .65));
                var tint = rng.NextDouble() < .18 ? Color.FromArgb(a, 170, 200, 255) : rng.NextDouble() < .08 ? Color.FromArgb(a, 255, 220, 200) : Color.FromArgb(a, 255, 255, 255);
                dc.DrawEllipse(new SolidColorBrush(tint), null, new Point(rng.NextDouble() * 1000, rng.NextDouble() * 1000), r, r);
            }
        }
        group.Freeze();
        var layer = new Rectangle { Fill = new DrawingBrush(group) { Stretch = Stretch.None, TileMode = TileMode.Tile, Viewport = new Rect(0, 0, 1000, 1000), ViewportUnits = BrushMappingMode.Absolute }, Margin = new(-60) };
        RenderOptions.SetCachingHint(layer.Fill, CachingHint.Cache); layer.CacheMode = new BitmapCache(1);
        var move = new TranslateTransform(); layer.RenderTransform = move;
        if (motion)
        {
            move.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(-drift, drift, TimeSpan.FromSeconds(seconds)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase() });
            layer.BeginAnimation(OpacityProperty, new DoubleAnimation(.7, 1, TimeSpan.FromSeconds(3 + rng.NextDouble() * 3)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase() });
        }
        return layer;
    }
}

/// <summary>The Controller Zone's backdrop: the navy galaxy with large faceted panels over it - angled planes catching a
/// little light along thin edges, drifting very slowly - after the owner's mock (10 Oct 2026), kept navy.</summary>
public sealed class FacetBackdrop : Grid
{
    public FacetBackdrop()
    {
        IsHitTestVisible = false; ClipToBounds = true;
        Children.Add(new NavyGalaxyBackdrop { Opacity = .85 });
        var facets = new Canvas { Width = 1600, Height = 900 };
        void Panel(string data, byte top, byte bottom, double angle)
        {
            facets.Children.Add(new Path { Data = Geometry.Parse(data), Fill = new LinearGradientBrush(Color.FromArgb(top, 120, 160, 220), Color.FromArgb(bottom, 120, 160, 220), angle), Stroke = new SolidColorBrush(Color.FromArgb(30, 190, 210, 240)), StrokeThickness = 1 });
        }
        Panel("M860,0 L1600,0 L1600,360 L1270,500 L1120,215 Z", 26, 4, 90);
        Panel("M1120,215 L1270,500 L1600,360 L1600,520 L1330,640 Z", 16, 2, 60);
        Panel("M0,540 L360,470 L600,900 L0,900 Z", 18, 3, 120);
        Panel("M1020,900 L1320,650 L1600,700 L1600,900 Z", 14, 2, 70);
        Panel("M560,0 L860,0 L760,120 L520,90 Z", 10, 1, 90);
        // a few long thin edges, like light caught on glass seams
        foreach (var edge in new[] { "M600,140 L980,128 L1120,215", "M0,420 L320,360 L520,470", "M1260,780 L1600,640" })
            facets.Children.Add(new Path { Data = Geometry.Parse(edge), Stroke = new LinearGradientBrush(Color.FromArgb(0, 190, 210, 240), Color.FromArgb(60, 190, 210, 240), 0), StrokeThickness = 1.2 });
        var host = new Viewbox { Child = facets, Stretch = Stretch.UniformToFill, CacheMode = new BitmapCache(1) };
        var drift = new TranslateTransform(); host.RenderTransform = drift;
        if (DS.Motion)
        {
            drift.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(-14, 14, TimeSpan.FromSeconds(34)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase() });
            drift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(8, -8, TimeSpan.FromSeconds(27)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase() });
        }
        host.Margin = new(-30); Children.Add(host);
    }
}

/// <summary>The galaxy in the app's navy palette (Controller Zone, controller picker).</summary>
public sealed class NavyGalaxyBackdrop : GalaxyBackdrop
{
    public NavyGalaxyBackdrop() : base(DS.Motion, navy: true) { }
}

public enum ChipKind { Success, Info, Warn, Neutral }
/// <summary>Rounded status pill with a softly pulsing dot ("● CONTROLLER DETECTED").</summary>
public sealed class StatusChip : Border
{
    private readonly TextBlock label = new() { FontSize = 12.5, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
    private readonly Ellipse dot = new() { Width = 7, Height = 7, Margin = new(0, 0, 9, 0), VerticalAlignment = VerticalAlignment.Center };
    public StatusChip(string text, ChipKind kind = ChipKind.Info)
    {
        CornerRadius = new(16); Height = 32; Padding = new(15, 0, 16, 0); BorderThickness = new(1.2); HorizontalAlignment = HorizontalAlignment.Left;
        label.FontFamily = DS.Display;
        Child = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Children = { dot, label } };
        Set(text, kind);
    }
    public void Set(string text, ChipKind kind)
    {
        // crisp like the concept: uppercase label, dark tinted fill, bright clean border, sharp dot
        label.Text = text.ToUpperInvariant();
        var c = kind switch { ChipKind.Success => Color.FromRgb(70, 225, 150), ChipKind.Warn => Color.FromRgb(255, 190, 92), ChipKind.Neutral => Color.FromRgb(160, 172, 198), _ => Color.FromRgb(125, 182, 255) };
        label.Foreground = new SolidColorBrush(c); dot.Fill = new SolidColorBrush(c);
        Background = new SolidColorBrush(Color.FromArgb(235, (byte)(6 + c.R / 14), (byte)(12 + c.G / 14), (byte)(22 + c.B / 14)));
        BorderBrush = new SolidColorBrush(Color.FromArgb(200, c.R, c.G, c.B));
        dot.Effect = new DropShadowEffect { Color = c, BlurRadius = 6, ShadowDepth = 0, Opacity = .8 };
        if (DS.Motion && kind != ChipKind.Neutral) dot.BeginAnimation(OpacityProperty, new DoubleAnimation(1, .35, TimeSpan.FromSeconds(1.1)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase() });
        else dot.BeginAnimation(OpacityProperty, null);
    }
}

/// <summary>Controller-glyph hint for the console footer: (A) Select · (B) Back · LB/RB Tabs.</summary>
public sealed class GlyphHint : StackPanel
{
    public GlyphHint(string glyph, string label)
    {
        Orientation = Orientation.Horizontal; Margin = new(0, 0, 26, 0); VerticalAlignment = VerticalAlignment.Center;
        bool round = glyph.Length == 1;
        // A/B/X/Y wear their controller colours (owner direction 10 Oct 2026); every other glyph stays a quiet grey
        var face = glyph switch { "A" => Color.FromRgb(70, 220, 150), "B" => Color.FromRgb(255, 100, 125), "X" => Color.FromRgb(70, 170, 255), "Y" => Color.FromRgb(245, 205, 80), _ => Color.FromRgb(214, 222, 234) };
        Children.Add(new Border
        {
            MinWidth = round ? 28 : 40, Height = 28, CornerRadius = new(14), Padding = new(round ? 0 : 8, 0, round ? 0 : 8, 0),
            Background = new SolidColorBrush(Color.FromArgb(40, face.R, face.G, face.B)), BorderBrush = new SolidColorBrush(Color.FromArgb(150, face.R, face.G, face.B)), BorderThickness = new(1.5),
            Child = new TextBlock { Text = glyph, FontFamily = DS.Display, FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(face), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
        });
        Children.Add(new TextBlock { Text = label, FontFamily = DS.Body, FontSize = 15, Foreground = DS.Brush("DS.TextSoft"), Margin = new(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
    }
    public static StackPanel Footer(params (string Glyph, string Label)[] hints)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        foreach (var (g, l) in hints) panel.Children.Add(new GlyphHint(g, l));
        return panel;
    }
}

/// <summary>Dashboard metric tile: icon glyph, big value, small caption.</summary>
public sealed class StatTile : Border
{
    public TextBlock Value { get; }
    public StatTile(string icon, string value, string caption)
    {
        Style = DS.Style("DS.Card"); MinHeight = 92; Padding = new(22, 16, 22, 16);
        Value = new TextBlock { Text = value, Style = DS.Style("DS.Metric") };
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new(18, 0, 0, 0), Children = { Value, new TextBlock { Text = caption, Style = DS.Style("DS.Caption") } } };
        var badge = new Border { Width = 46, Height = 46, CornerRadius = new(13), Background = DS.Brush("DS.GlassSubtle"), VerticalAlignment = VerticalAlignment.Center, Child = new TextBlock { Text = icon, FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 20, Foreground = DS.Brush("DS.AccentHi"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
        Child = new StackPanel { Orientation = Orientation.Horizontal, Children = { badge, text } };
    }
}

/// <summary>Hosts floating glass windows above the app: scrim fades in, the page behind blurs and
/// recedes slightly, the card springs up from 94% with a short rise. B / Esc closes; focus is trapped
/// inside and restored afterwards. Reduced motion swaps every animation for an instant change.</summary>
public sealed class PopupHost : Grid
{
    private readonly UIElement behind;
    private readonly Rectangle scrim = new() { Fill = new SolidColorBrush(Color.FromArgb(168, 3, 6, 17)), Opacity = 0 };
    private readonly Grid layer = new() { Visibility = Visibility.Collapsed };
    private readonly Image frozen = new() { Stretch = Stretch.Fill, Opacity = 0, IsHitTestVisible = false, Effect = new BlurEffect { Radius = 9, RenderingBias = RenderingBias.Performance }, CacheMode = new BitmapCache(1) };
    private readonly ScaleTransform recede = new(1, 1);
    private Border? card; private ScaleTransform? cardScale; private TranslateTransform? cardRise;
    private TaskCompletionSource<bool>? closed; private IInputElement? restoreFocus;
    public bool IsOpen => card != null;
    public event Action? Opened;

    public PopupHost(UIElement content)
    {
        behind = content; Children.Add(new Rectangle { Fill = new SolidColorBrush(Color.FromRgb(4, 7, 15)) }); Children.Add(content); Children.Add(frozen); ClipToBounds = true;
        frozen.RenderTransformOrigin = new(.5, .5); frozen.RenderTransform = recede;

        layer.Children.Add(scrim); Children.Add(layer);
        scrim.MouseLeftButtonDown += (_, _) => Close();
        layer.PreviewKeyDown += (_, e) => { if (e.Key is Key.Escape or Key.B && !(e.OriginalSource is TextBox)) { Close(); e.Handled = true; } };
        KeyboardNavigation.SetTabNavigation(layer, KeyboardNavigationMode.Cycle); KeyboardNavigation.SetDirectionalNavigation(layer, KeyboardNavigationMode.Cycle);
    }

    /// <summary>Shows a glass window with an eyebrow, title, optional chip, body and footer actions.</summary>
    public Task Show(string eyebrow, string title, FrameworkElement body, double width = 760, StatusChip? chip = null, params Button[] actions)
    {
        if (IsOpen) CloseNow();
        restoreFocus = Keyboard.FocusedElement;
        var header = new Grid { Margin = new(0, 0, 0, 22) };
        header.ColumnDefinitions.Add(new()); header.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        header.Children.Add(new StackPanel { Children = { new TextBlock { Text = eyebrow, Style = DS.Style("DS.Eyebrow") }, new TextBlock { Text = title, Style = DS.Style("DS.Title"), Margin = new(0, 4, 0, 0) } } });
        var right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top };
        if (chip != null) { chip.Margin = new(0, 6, 14, 0); right.Children.Add(chip); }
        var x = new Button { Content = "", Style = DS.Style("DS.Icon"), ToolTip = "Close (B / Esc)" }; x.Click += (_, _) => Close(); right.Children.Add(x);
        Grid.SetColumn(right, 1); header.Children.Add(right);
        var footer = new Grid { Margin = new(0, 24, 0, 0) };
        footer.ColumnDefinitions.Add(new()); footer.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        footer.Children.Add(GlyphHint.Footer(("A", "Select"), ("B", "Close")));
        var buttons = new StackPanel { Orientation = Orientation.Horizontal }; foreach (var a in actions) { a.Margin = new(12, 0, 0, 0); buttons.Children.Add(a); }
        Grid.SetColumn(buttons, 1); footer.Children.Add(buttons);
        var stack = new DockPanel(); DockPanel.SetDock(header, Dock.Top); DockPanel.SetDock(footer, Dock.Bottom); stack.Children.Add(header); stack.Children.Add(footer); stack.Children.Add(body);
        cardScale = new ScaleTransform(.94, .94); cardRise = new TranslateTransform(0, 18);
        card = new Border { Style = DS.Style("DS.Surface"), Background = DS.Brush("DS.GlassRaised"), Width = width, MaxHeight = 900, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Child = stack, Opacity = 0, Effect = (Effect)Application.Current.FindResource("DS.Shadow.Surface"), RenderTransformOrigin = new(.5, .5), RenderTransform = new TransformGroup { Children = { cardScale, cardRise } } };
        layer.Children.Add(card); layer.Visibility = Visibility.Visible; behind.IsHitTestVisible = false; Snapshot();
        if (DS.Motion)
        {
            scrim.BeginAnimation(OpacityProperty, DS.To(1, 220)); frozen.BeginAnimation(OpacityProperty, DS.To(1, 200));
            recede.BeginAnimation(ScaleTransform.ScaleXProperty, DS.To(1.03, 300)); recede.BeginAnimation(ScaleTransform.ScaleYProperty, DS.To(1.03, 300));
            card.BeginAnimation(OpacityProperty, DS.To(1, 200));
            cardScale.BeginAnimation(ScaleTransform.ScaleXProperty, DS.To(1, 340, DS.Spring)); cardScale.BeginAnimation(ScaleTransform.ScaleYProperty, DS.To(1, 340, DS.Spring));
            cardRise.BeginAnimation(TranslateTransform.YProperty, DS.To(0, 300));
        }
        else { scrim.Opacity = 1; frozen.Opacity = 1; card.Opacity = 1; cardScale.ScaleX = cardScale.ScaleY = 1; cardRise.Y = 0; }
        closed = new();
        card.Loaded += (_, _) => { card?.MoveFocus(new TraversalRequest(FocusNavigationDirection.First)); Opened?.Invoke(); };
        return closed.Task;
    }

    public void Close()
    {
        if (card == null) return;
        var leaving = card; card = null;
        if (!DS.Motion) { Finish(leaving); return; }
        var fade = DS.To(0, 160); fade.Completed += (_, _) => Finish(leaving);
        leaving.BeginAnimation(OpacityProperty, fade);
        cardScale?.BeginAnimation(ScaleTransform.ScaleXProperty, DS.To(.96, 160)); cardScale?.BeginAnimation(ScaleTransform.ScaleYProperty, DS.To(.96, 160));
        cardRise?.BeginAnimation(TranslateTransform.YProperty, DS.To(10, 160));
        scrim.BeginAnimation(OpacityProperty, DS.To(0, 200)); frozen.BeginAnimation(OpacityProperty, DS.To(0, 200));
        recede.BeginAnimation(ScaleTransform.ScaleXProperty, DS.To(1, 220)); recede.BeginAnimation(ScaleTransform.ScaleYProperty, DS.To(1, 220));
    }
    /// <summary>One render of the page at half resolution; blurring it costs nothing per frame afterwards.</summary>
    private void Snapshot()
    {
        if (behind is not FrameworkElement page || page.ActualWidth < 1) return;
        int w = Math.Max(1, (int)(page.ActualWidth / 2)), h = Math.Max(1, (int)(page.ActualHeight / 2));
        var visual = new DrawingVisual(); using (var dc = visual.RenderOpen()) dc.DrawRectangle(new VisualBrush(page) { Stretch = Stretch.Fill }, null, new Rect(0, 0, w, h));
        var bmp = new System.Windows.Media.Imaging.RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32); bmp.Render(visual); bmp.Freeze(); frozen.Source = bmp;
    }
    private void CloseNow() { if (card != null) { var c = card; card = null; Finish(c); } }
    private void Finish(Border leaving)
    {
        layer.Children.Remove(leaving);
        if (card != null) return; // another popup opened during the exit animation
        layer.Visibility = Visibility.Collapsed; behind.IsHitTestVisible = true; frozen.BeginAnimation(OpacityProperty, null); frozen.Opacity = 0; frozen.Source = null;
        recede.BeginAnimation(ScaleTransform.ScaleXProperty, null); recede.BeginAnimation(ScaleTransform.ScaleYProperty, null); recede.ScaleX = recede.ScaleY = 1;
        restoreFocus?.Focus(); closed?.TrySetResult(true);
    }
}

/// <summary>LB / RB drawn as the bumpers themselves (owner direction 10 Oct 2026): a rounded wedge that rises on the
/// outer side, mirrored for RB, with a soft top light. Used beside the Studio's tab row.</summary>
public sealed class BumperBadge : Grid
{
    private readonly System.Windows.Shapes.Path shape = new() { Stretch = Stretch.Fill, StrokeThickness = 1.4 };
    private readonly TextBlock text = new() { FontFamily = DS.Display, FontSize = 12.5, FontWeight = FontWeights.Bold, Foreground = DS.Brush("DS.Text"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new(0, 3, 0, 0) };
    public BumperBadge()
    {
        Width = 58; Height = 30; VerticalAlignment = VerticalAlignment.Center;
        shape.Data = Geometry.Parse("M7,29 L51,29 C53.5,29 54.5,27 53.5,24.5 L47,9 C45,5 41.5,3 37,3 L21,3 C11,3 4,10 3.5,20 L3,25 C3,27.5 4.5,29 7,29 Z");
        shape.Fill = new LinearGradientBrush(Color.FromArgb(90, 160, 190, 235), Color.FromArgb(30, 120, 150, 210), 90);
        shape.Stroke = new LinearGradientBrush(Color.FromArgb(220, 205, 222, 250), Color.FromArgb(90, 160, 185, 230), 90);
        shape.Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Color.FromRgb(61, 139, 255), BlurRadius = 10, ShadowDepth = 0, Opacity = .45 };
        Children.Add(shape); Children.Add(text);
    }
    public string Label
    {
        get => text.Text;
        set { text.Text = value; shape.RenderTransformOrigin = new(.5, .5); shape.RenderTransform = value.StartsWith('R') ? new ScaleTransform(-1, 1) : Transform.Identity; }
    }
}
