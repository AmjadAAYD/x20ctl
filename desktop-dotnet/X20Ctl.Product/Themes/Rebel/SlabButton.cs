using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace X20Ctl.Product.Rebel;

/// <summary>A button that is a jagged slab: dark at rest, flipped to white over a red spike when focused or hovered (or always, when it is the main one).</summary>
internal sealed class SlabButton : Button
{
    private readonly Slab slab; private readonly TextBlock label; private readonly TextBlock? icon; private readonly bool primary; private readonly Color rest;
    public string Caption { get => label.Text; set => label.Text = value; }
    /// <summary>At rest a black slab with a hard white edge (.btn.ghost), rather than the dark panel.</summary>
    public bool Outlined { get => outlined; init { outlined = value; Paint(); } }
    private readonly bool outlined;
    public SlabButton(string text, double w, double h, int seed, double rot, double size, Color textRest, bool primary = false, HorizontalAlignment align = HorizontalAlignment.Left, string? glyph = null)
    {
        this.primary = primary; rest = textRest; Width = w; Height = h; FocusVisualStyle = null; Cursor = System.Windows.Input.Cursors.Hand; RenderTransformOrigin = new(.5, .5); RenderTransform = new RotateTransform(rot);
        var t = new ControlTemplate(typeof(Button)); t.VisualTree = new FrameworkElementFactory(typeof(ContentPresenter)); Template = t;
        slab = new Slab { Seed = seed, Amp = 6 };
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = align, VerticalAlignment = VerticalAlignment.Center, Margin = new(align == HorizontalAlignment.Left ? 22 : 0, 0, align == HorizontalAlignment.Right ? 22 : 0, 0) };
        if (glyph != null) { icon = new TextBlock { Text = glyph, FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = size * .9, Margin = new(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center }; row.Children.Add(icon); }
        label = Ink.Text(text, size); row.Children.Add(label); slab.Children.Add(row); Content = slab;
        GotKeyboardFocus += (_, _) => Paint(); LostKeyboardFocus += (_, _) => Paint(); MouseEnter += (_, _) => Paint(); MouseLeave += (_, _) => Paint(); Paint();
    }
    private void Paint()
    {
        bool lit = primary || IsKeyboardFocused || IsMouseOver;
        slab.Fill = Ink.B(lit ? Ink.White : Outlined ? Ink.Black : Ink.Panel); slab.Line = lit ? null : Ink.B(Outlined ? Ink.White : Ink.A(Ink.White, .26)); slab.LineWidth = Outlined ? 2.2 : 1.4; slab.Under = lit ? Ink.B(Ink.Red) : null; slab.Spike = lit ? 18 : 0;
        var fg = Ink.B(lit ? Ink.Black : rest); label.Foreground = fg; if (icon != null) icon.Foreground = fg;
        slab.InvalidateVisual();
    }
}
