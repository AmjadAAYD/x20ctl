using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace X20Ctl.Product.Rebel;

/// <summary>The Studio's Rebel parts (owner boards dashboard.png, buttons.png, curves.png): the page cascade down the left, the tabs
/// along the floor, the list rows, the small tags and tabs, and the pad itself.</summary>
internal static class Glyphs
{
    public static readonly FontFamily Font = new("Segoe Fluent Icons, Segoe MDL2 Assets");
    public const string Dashboard = "", Buttons = "", Curves = "", Macros = "", Vibration = "", PcMode = "", Device = "",
        Mouse = "", Setups = "", Tools = "", Back = "", Settings = "", Test = "", Remove = "", Chevron = "", Reset = "", Play = "", Add = "", Battery = "";
    public static TextBlock Make(string g, double size, Brush fg) => new() { Text = g, FontFamily = Font, FontSize = size, Foreground = fg, VerticalAlignment = VerticalAlignment.Center };
}

internal static class Pieces
{
    /// <summary>The Rebel logo, with its hard black drop.</summary>
    public static Image Logo(double width)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Brand", "themes", "rebel", "logo.png");
        var img = new Image { Width = width, Stretch = Stretch.Uniform, Effect = Ink.Shadow(4, 5, 0, Colors.Black, 1), IsHitTestVisible = false };
        if (File.Exists(path)) img.Source = new BitmapImage(new Uri(path));
        return img;
    }
    /// <summary>The red tag (.tag): skewed, letter-spaced capitals.</summary>
    public static Border Tag(string text, bool white = false)
    {
        var t = new TextBlock { Text = text.ToUpperInvariant(), FontFamily = Ink.Body, FontSize = 12, FontWeight = FontWeights.Bold, Foreground = Ink.B(white ? Ink.Black : Colors.White) };
        return new Border { Background = Ink.B(white ? Ink.White : Ink.Red), Padding = new(12, 5, 12, 5), Child = t, RenderTransformOrigin = new(.5, .5), RenderTransform = new SkewTransform(-12, 0), VerticalAlignment = VerticalAlignment.Center };
    }
    /// <summary>The pad's photo in its chosen finish, as a 1536 by 1024 picture.</summary>
    public static Grid Pad(string model, string finish, double width)
    {
        var picture = new Image { Stretch = Stretch.Fill }; RenderOptions.SetBitmapScalingMode(picture, BitmapScalingMode.HighQuality);
        var morph = new FinishMorph(picture); var art = new Grid { Width = 1536, Height = 1024 }; art.Children.Add(picture); art.Children.Add(morph); morph.Show(model, finish);
        return new Grid { Width = width, Height = width / 1.5, IsHitTestVisible = false, Children = { new Viewbox { Child = art, Stretch = Stretch.Uniform } } };
    }
}

/// <summary>A small tab (.tb): skewed dark chip, or white over a red offset when it is the one shown.</summary>
internal sealed class ChipTab : Button
{
    private readonly Border face; private readonly TextBlock label; private bool on;
    public bool On { get => on; set { on = value; Paint(); } }
    public ChipTab(string text, double size = 17)
    {
        FocusVisualStyle = null; Cursor = System.Windows.Input.Cursors.Hand; var t = new ControlTemplate(typeof(Button)); t.VisualTree = new FrameworkElementFactory(typeof(ContentPresenter)); Template = t;
        label = new TextBlock { Text = text, FontFamily = Ink.Heavy, FontSize = size, VerticalAlignment = VerticalAlignment.Center };
        face = new Border { Height = 38, Padding = new(18, 0, 18, 0), Child = label, RenderTransformOrigin = new(.5, .5), RenderTransform = new SkewTransform(-14, 0), BorderThickness = new(1.4) };
        Content = face; Margin = new(0, 0, 8, 0);
        GotKeyboardFocus += (_, _) => Paint(); LostKeyboardFocus += (_, _) => Paint(); MouseEnter += (_, _) => Paint(); MouseLeave += (_, _) => Paint(); Paint();
    }
    private void Paint()
    {
        bool lit = on || IsKeyboardFocused; face.Background = Ink.B(lit ? Ink.White : Ink.Panel); face.BorderBrush = Ink.B(lit ? Colors.Transparent : Ink.A(Ink.White, IsMouseOver ? .5 : .22));
        face.Effect = on ? Ink.Shadow(6, 5, 0, Ink.Red, 1) : null; label.Foreground = Ink.B(lit ? Ink.Black : Ink.C("#c9c1c3"));
    }
}

/// <summary>A page slab: dark at rest, white over a red spike when it is the page shown, a lighter edge when focused. The lit slab
/// grows out of the resting one (<paramref name="grow"/> is its extra size and offset), as the boards draw it.</summary>
internal sealed class PageSlab : Button
{
    private readonly Slab slab; private readonly TextBlock glyph, name; private readonly Thickness grow; private readonly double restGlyph, litGlyph, restName, litName; private bool on;
    public bool On { get => on; set { on = value; Paint(); } }
    public PageSlab(string text, string icon, double w, double h, int seed, double rot, Thickness grow, bool vertical, double restGlyph, double litGlyph, double restName, double litName)
    {
        this.grow = grow; this.restGlyph = restGlyph; this.litGlyph = litGlyph; this.restName = restName; this.litName = litName;
        Width = w; Height = h; FocusVisualStyle = null; Cursor = System.Windows.Input.Cursors.Hand; RenderTransformOrigin = new(.5, .5); RenderTransform = new RotateTransform(rot);
        var t = new ControlTemplate(typeof(Button)); t.VisualTree = new FrameworkElementFactory(typeof(ContentPresenter)); Template = t;
        System.Windows.Automation.AutomationProperties.SetName(this, text);
        slab = new Slab { Seed = seed, Amp = 6 }; glyph = Glyphs.Make(icon, restGlyph, Ink.B(Ink.C("#e9e3e5"))); name = Ink.Text(text, restName);
        var stack = new StackPanel { Orientation = vertical ? Orientation.Vertical : Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        if (vertical) { glyph.HorizontalAlignment = HorizontalAlignment.Left; name.Margin = new(0, 5, 0, 0); stack.Margin = new(18, 0, 0, 0); }
        else { name.Margin = new(12, 0, 0, 0); stack.Margin = new(18, 0, 0, 0); }
        stack.Children.Add(glyph); stack.Children.Add(name); slab.Children.Add(stack); Content = slab;
        GotKeyboardFocus += (_, _) => Paint(); LostKeyboardFocus += (_, _) => Paint(); MouseEnter += (_, _) => Paint(); MouseLeave += (_, _) => Paint(); Paint();
    }
    public bool ShowName { get; set; } = true;
    private void Paint()
    {
        bool focus = IsKeyboardFocused || IsMouseOver;
        slab.Margin = on ? grow : new Thickness(0); slab.Fill = Ink.B(on ? Ink.White : focus ? Ink.C("#262226") : Ink.Panel);
        slab.Line = on ? null : Ink.B(Ink.A(Ink.White, focus ? .7 : .26)); slab.LineWidth = 1.5; slab.Under = on ? Ink.B(Ink.Red) : null; slab.Spike = on ? 16 : 0;
        glyph.FontSize = on ? litGlyph : restGlyph; name.FontSize = on ? litName : restName; name.Visibility = on || ShowName ? Visibility.Visible : Visibility.Collapsed;
        glyph.Foreground = Ink.B(on ? Ink.Black : Ink.C("#e9e3e5")); name.Foreground = Ink.B(on ? Ink.Black : Ink.White);
        slab.InvalidateVisual();
    }
}

/// <summary>A list row (.row): a dark band cut slightly off square, white over a red spike when it is the chosen one.</summary>
internal sealed class RowSlab : Button
{
    private readonly bool even; private bool on; private readonly List<(TextBlock t, Color rest, Color lit)> inks = new();
    public Grid Left { get; } = new() { VerticalAlignment = VerticalAlignment.Center };
    public StackPanel Right { get; } = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
    public bool On { get => on; set { if (on == value) return; on = value; Paint(); } }
    public RowSlab(int index, double h)
    {
        even = index % 2 == 1; Height = h; FocusVisualStyle = null; Cursor = System.Windows.Input.Cursors.Hand; Background = Brushes.Transparent;
        var t = new ControlTemplate(typeof(Button)); t.VisualTree = new FrameworkElementFactory(typeof(ContentPresenter)); Template = t;
        var g = new Grid { Margin = new(26, 0, 20, 0) }; g.Children.Add(Left); g.Children.Add(Right); Content = g;
        GotKeyboardFocus += (_, _) => InvalidateVisual(); LostKeyboardFocus += (_, _) => InvalidateVisual(); MouseEnter += (_, _) => InvalidateVisual(); MouseLeave += (_, _) => InvalidateVisual();
    }
    /// <summary>Text that reads dark on the lit row.</summary>
    public TextBlock Tint(TextBlock t, Color rest, Color lit) { inks.Add((t, rest, lit)); t.Foreground = Ink.B(on ? lit : rest); return t; }
    private void Paint() { foreach (var (t, rest, lit) in inks) t.Foreground = Ink.B(on ? lit : rest); InvalidateVisual(); }
    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight; if (w <= 0) return;
        if (on)
        {
            double x0 = -20, y0 = -5, W = w + 29, H = h + 13;
            dc.DrawGeometry(Ink.B(Ink.Red), null, Slab.Poly([new(x0, y0 + H * .5), new(x0 + W * .036, y0 + H * .06), new(x0 + W, y0), new(x0 + W * .986, y0 + H), new(x0 + W * .044, y0 + H * .9)]));
        }
        Point[] pts = even ? [new(w * .006, h * .07), new(w * .996, 0), new(w, h * .93), new(0, h)] : [new(0, 0), new(w, h * .06), new(w * .994, h), new(w * .007, h * .92)];
        bool focus = !on && (IsKeyboardFocused || IsMouseOver);
        dc.DrawGeometry(Ink.B(on ? Ink.White : focus ? Ink.C("#221f23") : Ink.Panel), focus ? new Pen(Ink.B(Ink.A(Ink.White, .55)), 1.5) : null, Slab.Poly(pts));
    }
}
