using System.Windows;
using System.Windows.Media;
namespace X20Ctl.Desktop.Controls;
/// <summary>Presentation interpolation only; the bound raw byte is never changed.</summary>
public sealed class AnalogBar : FrameworkElement
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(byte), typeof(AnalogBar), new FrameworkPropertyMetadata((byte)0, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty ReducedMotionProperty = DependencyProperty.Register(nameof(ReducedMotion), typeof(bool), typeof(AnalogBar), new PropertyMetadata(false));
    public byte Value { get => (byte)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public bool ReducedMotion { get => (bool)GetValue(ReducedMotionProperty); set => SetValue(ReducedMotionProperty, value); }
    private double shown;
    public AnalogBar() { Height = 25; IsHitTestVisible = false; Loaded += (_, _) => CompositionTarget.Rendering += RenderFrame; Unloaded += (_, _) => CompositionTarget.Rendering -= RenderFrame; }
    private void RenderFrame(object? sender, EventArgs e) { double next = ReducedMotion ? Value : shown + (Value - shown) * .28; if (Math.Abs(next - Value) < .1) next = Value; if (Math.Abs(shown - next) > .01) { shown = next; InvalidateVisual(); } }
    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(20, 36, 60)), new Pen(new SolidColorBrush(Color.FromRgb(42, 64, 95)), .7), new(0, 0, ActualWidth, 8), 4, 4);
        double width = ActualWidth * shown / 255;
        if (width > 0) dc.DrawRoundedRectangle(new LinearGradientBrush(Color.FromRgb(32, 130, 248), Color.FromRgb(120, 218, 255), 0), null, new(0, 0, width, 8), 4, 4);
        var scale = new Pen(new SolidColorBrush(Color.FromRgb(65, 93, 130)), 1);
        for (int i = 0; i <= 8; i++) { double x = Math.Clamp(ActualWidth * i / 8, .5, ActualWidth - .5); dc.DrawLine(scale, new(x, 11), new(x, i % 4 == 0 ? 16 : 14)); }
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        foreach (var (text, position) in new[] { ("0", 0d), ("128", ActualWidth / 2), ("255", ActualWidth) })
        {
            var label = new FormattedText(text, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Bahnschrift"), 11.5, new SolidColorBrush(Color.FromRgb(123, 152, 191)), dpi);
            dc.DrawText(label, new(Math.Clamp(position - label.Width / 2, 0, Math.Max(0, ActualWidth - label.Width)), 16));
        }
    }
}
