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
    /// <summary>Agent XCTL changed this control: it glows a few times, apart from the selection. Steady without motion.</summary>
    public void Mark(int times = 4)
    {
        var glow = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop };
        if (animate)
        {
            glow.KeyFrames.Add(new EasingDoubleKeyFrame(.85, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(420)), new SineEase()));
            glow.KeyFrames.Add(new EasingDoubleKeyFrame(.15, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1000)), new SineEase()));
            glow.RepeatBehavior = new RepeatBehavior(times);
        }
        else { glow.KeyFrames.Add(new DiscreteDoubleKeyFrame(.7, KeyTime.FromTimeSpan(TimeSpan.Zero))); glow.KeyFrames.Add(new DiscreteDoubleKeyFrame(.7, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(times)))); }
        BeginAnimation(RevealProperty, glow);
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
        Effect = active ? new DropShadowEffect { ShadowDepth = 0, BlurRadius = 10, Color = Color.FromRgb(66, 145, 255), Opacity = .4 } : null;
        InvalidateVisual();
    }
    protected override HitTestResult? HitTestCore(PointHitTestParameters p) => geometry.FillContains(p.HitPoint) ? new PointHitTestResult(this, p.HitPoint) : null;
    protected override void OnRender(DrawingContext dc)
    {
        double strength = Math.Max(Light, (double)GetValue(RevealProperty));
        Brush fill = Region.Role == "macro" ? new RadialGradientBrush(new GradientStopCollection {
            new(Color.FromArgb((byte)(strength * (compact?145:75) + (IsMouseOver ? 15 : 0)), 43, 136, 246), 0),
            new(Color.FromArgb((byte)(strength * 50), 20, 95, 188), 1)
        }) { RadiusX = .72, RadiusY = .72 } : new SolidColorBrush(Color.FromArgb((byte)(strength * 70 + (IsMouseOver ? 22 : 0)), 39, 117, 222));
        var line = new SolidColorBrush(Color.FromArgb((byte)(45 + strength * 210), 191, 222, 255));
        dc.DrawGeometry(fill, new Pen(line, strength > .05 ? Region.Role == "macro" ? compact?6:3 : 3.5 : 1.25), geometry);
        if (navigationFocus && IsKeyboardFocused) dc.DrawGeometry(null, new Pen(Brushes.White, 4), geometry);
        if (Region.Role == "macro")
        {
            var anchor = Region.LabelAnchor ?? new(Region.Bounds.X + Region.Bounds.Width / 2, Region.Bounds.Y + Region.Bounds.Height / 2);
            var font = new Typeface(new FontFamily("Segoe UI Variable Text, Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
            double fontSize = Math.Clamp(Height * .42, 24, 38);
            FormattedText Label(Brush brush) => new(Region.Key, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                font, fontSize, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            Brush ink = selected ? Brushes.White : new SolidColorBrush(Color.FromRgb(218, 231, 249));
            var text = Label(ink);
            if(text.Width > Width * .76) { fontSize *= Width * .76 / text.Width; text = Label(ink); }
            var location = new Point((anchor.X - Region.Bounds.X) * 1536 - text.Width / 2, (anchor.Y - Region.Bounds.Y) * 1024 - text.Height / 2);
            dc.DrawText(Label(new SolidColorBrush(Color.FromArgb(220, 5, 12, 24))), new(location.X, location.Y + 2));
            dc.DrawText(text, location);
        }
    }
    private static Geometry RoundCorners(Geometry source)
    {
        var flat = source.GetFlattenedPathGeometry();
        if (flat.Figures.Count != 1 || !flat.Figures[0].IsClosed) return source;
        var figure=flat.Figures[0]; var points=new List<Point>{figure.StartPoint};
        foreach(var segment in figure.Segments)
            if(segment is PolyLineSegment poly) points.AddRange(poly.Points);
            else if(segment is LineSegment line) points.Add(line.Point);
        if(points.Count>1 && (points[^1]-points[0]).Length<.01)points.RemoveAt(points.Count-1);
        if(points.Count<3)return source;
        var result=new StreamGeometry();
        using(var context=result.Open())
        {
            Point Before(int i) { var delta=points[(i+points.Count-1)%points.Count]-points[i];return points[i]+delta*Math.Min(.18,8/Math.Max(1,delta.Length)); }
            Point After(int i) { var delta=points[(i+1)%points.Count]-points[i];return points[i]+delta*Math.Min(.18,8/Math.Max(1,delta.Length)); }
            context.BeginFigure(Before(0),true,true);
            for(int i=0;i<points.Count;i++){if(i>0)context.LineTo(Before(i),true,false);context.QuadraticBezierTo(points[i],After(i),true,false);}
        }
        return result;
    }

    private static Geometry CreateGeometry(ControlRegion region)
    {
        double w = region.Bounds.Width * 1536, h = region.Bounds.Height * 1024;
        Geometry value;
        if (region.PathData != null)
        {
            value = Geometry.Parse(region.PathData).Clone();
            if (!region.PathData.Any(c => "CcQqAaSsTt".Contains(c))) value = RoundCorners(value);
            var transforms = new TransformGroup();
            if (region.Transform != null)
            {
                if (region.Transform != "translate(1536 0) scale(-1 1)") throw new InvalidDataException("Unsupported owned geometry transform.");
                transforms.Children.Add(new ScaleTransform(-1, 1, 768, 0));
            }
            transforms.Children.Add(new TranslateTransform(-region.Bounds.X * 1536, -region.Bounds.Y * 1024)); value.Transform = transforms;
        }
        else if (region.Shape == ControlShape.Polygon && region.Points.Count >= 3)
        {
            var polygon = new StreamGeometry();
            using (var context = polygon.Open())
            {
                var points = region.Points.Select(p => new Point((p.X-region.Bounds.X)*1536,(p.Y-region.Bounds.Y)*1024)).ToArray();
                context.BeginFigure(points[0],true,true); context.PolyLineTo(points.Skip(1).ToArray(),true,false);
            }
            value = polygon;
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
