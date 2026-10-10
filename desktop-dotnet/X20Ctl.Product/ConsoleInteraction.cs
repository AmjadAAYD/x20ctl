using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using X20Ctl.Zone;
namespace X20Ctl.Product;

/// <summary>Owned-art geometry hit testing and local illumination; never device input state.</summary>
public sealed class PhysicalHotspot : Button
{
    public ControlRegion Region { get; }
    private readonly Geometry geometry;
    private readonly bool animate;
    private readonly bool compact;
    private bool selected, navigationFocus;
    public static readonly DependencyProperty LightProperty = DependencyProperty.Register(nameof(Light), typeof(double), typeof(PhysicalHotspot), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));
    public double Light { get => (double)GetValue(LightProperty); set => SetValue(LightProperty, value); }
    private static readonly DependencyProperty RevealProperty = DependencyProperty.Register("Reveal", typeof(double), typeof(PhysicalHotspot), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));
    public void RevealOnce(int delay)
    {
        if (!animate) return;
        var reveal = new DoubleAnimationUsingKeyFrames { BeginTime = TimeSpan.FromMilliseconds(delay) };
        reveal.KeyFrames.Add(new LinearDoubleKeyFrame(.28, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(75))));
        reveal.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(290))));
        BeginAnimation(RevealProperty, reveal);
    }
    public PhysicalHotspot(ControlRegion region, bool animations,bool compactPreview=false)
    {
        Region = region; animate = animations; compact=compactPreview; Width = region.Bounds.Width * 1536; Height = region.Bounds.Height * 1024;
        Template = new ControlTemplate(typeof(Button)) { VisualTree = new FrameworkElementFactory(typeof(Grid)) };
        FocusVisualStyle = null; Cursor = System.Windows.Input.Cursors.Hand; Background = Brushes.Transparent;
        geometry = CreateGeometry(region);
        MouseEnter += (_, _) => InvalidateVisual(); MouseLeave += (_, _) => InvalidateVisual();
        IsKeyboardFocusWithinChanged += (_, _) => InvalidateVisual();
        System.Windows.Automation.AutomationProperties.SetName(this, "Select " + region.Key);
    }
    public void Illuminate(bool active, bool controllerFocus)
    {
        selected = active; navigationFocus = controllerFocus;
        double previous = Light; BeginAnimation(LightProperty, null); Light = active ? 1 : 0;
        if (animate) BeginAnimation(LightProperty, new DoubleAnimation(previous, Light, TimeSpan.FromMilliseconds(160)) { FillBehavior = FillBehavior.Stop });
        Effect = active && Region.Role != "macro" ? new DropShadowEffect { ShadowDepth = 0, BlurRadius = 20, Color = Color.FromRgb(66, 145, 255), Opacity = .65 } : null;
        InvalidateVisual();
    }
    protected override HitTestResult? HitTestCore(PointHitTestParameters p) => geometry.FillContains(p.HitPoint) ? new PointHitTestResult(this, p.HitPoint) : null;
    protected override void OnRender(DrawingContext dc)
    {
        double strength = Math.Max(Light, (double)GetValue(RevealProperty));
        Brush fill = Region.Role == "macro" ? new RadialGradientBrush(new GradientStopCollection {
            new(Color.FromArgb((byte)(strength * (compact?210:115) + (IsMouseOver ? 15 : 0)), 43, 136, 246), 0),
            new(Color.FromArgb((byte)(strength * 50), 20, 95, 188), 1)
        }) { RadiusX = .72, RadiusY = .72 } : new SolidColorBrush(Color.FromArgb((byte)(strength * 95 + (IsMouseOver ? 22 : 0)), 39, 117, 222));
        var line = new SolidColorBrush(Color.FromArgb((byte)(45 + strength * 210), 191, 222, 255));
        dc.DrawGeometry(fill, new Pen(line, strength > .05 ? Region.Role == "macro" ? compact?12:3 : 5 : 1.5), geometry);
        if (navigationFocus && IsKeyboardFocused) dc.DrawGeometry(null, new Pen(Brushes.White, 8), geometry);
        if (Region.Role == "macro")
        {
            var anchor = Region.LabelAnchor ?? new(Region.Bounds.X + Region.Bounds.Width / 2, Region.Bounds.Y + Region.Bounds.Height / 2);
            var text = new FormattedText(Region.Key, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal), 46, selected ? Brushes.White : new SolidColorBrush(Color.FromRgb(179, 207, 246)), VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(text, new((anchor.X - Region.Bounds.X) * 1536 - text.Width / 2, (anchor.Y - Region.Bounds.Y) * 1024 - text.Height / 2));
        }
    }
    private static Geometry CreateGeometry(ControlRegion region)
    {
        double w = region.Bounds.Width * 1536, h = region.Bounds.Height * 1024;
        Geometry value;
        if (region.PathData != null)
        {
            value = Geometry.Parse(region.PathData).Clone();
            var transforms = new TransformGroup();
            if (region.Transform != null)
            {
                if (region.Transform != "translate(1536 0) scale(-1 1)") throw new InvalidDataException("Unsupported owned geometry transform.");
                transforms.Children.Add(new ScaleTransform(-1, 1, 768, 0));
            }
            transforms.Children.Add(new TranslateTransform(-region.Bounds.X * 1536, -region.Bounds.Y * 1024)); value.Transform = transforms;
        }
        else if (region.Shape == ControlShape.Circle) value = new EllipseGeometry(new Point(w / 2, h / 2), w / 2, h / 2);
        else value = new RectangleGeometry(new Rect(0, 0, w, h), 7, 7);
        value.Freeze(); return value;
    }
}

/// <summary>Detached console focus, authored independently; scoped to Studio controls.</summary>
internal sealed class ConsoleFocusAdorner : Adorner
{
    public ConsoleFocusAdorner(UIElement target, Vector travel, bool animate) : base(target)
    {
        IsHitTestVisible = false;
        if (!animate) return;
        var shift = new TranslateTransform(); RenderTransform = shift;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        shift.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(travel.X, 0, TimeSpan.FromMilliseconds(140)) { EasingFunction = ease });
        shift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(travel.Y, 0, TimeSpan.FromMilliseconds(140)) { EasingFunction = ease });
    }
    protected override void OnRender(DrawingContext dc)
    {
        var bounds = new Rect(AdornedElement.RenderSize); bounds.Inflate(4, 4);
        double radius = AdornedElement is StickDirectionSelector ? bounds.Width/2 : AdornedElement.RenderSize.Width <= 52 ? 28 : 14;
        dc.DrawRoundedRectangle(null, new Pen(new SolidColorBrush(Color.FromArgb(65, 67, 139, 255)), 7), bounds, radius, radius);
        dc.DrawRoundedRectangle(null, new Pen(Brushes.White, 2.5), bounds, radius, radius);
    }
}
