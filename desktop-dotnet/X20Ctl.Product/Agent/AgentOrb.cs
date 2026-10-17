using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
namespace X20Ctl.Product.Agent;

/// <summary>
/// The agent's mark (owner, 13 Oct 2026: "we are talking to a controller, it must look like a controller"): a controller
/// whose two sticks are lit eyes, with a player light in the brand red, on the X20CTL blue. Drawn as vectors, so it is
/// sharp at any size: the orb, the walk-through and anywhere else.
/// </summary>
public static class AgentMark
{
    public static readonly Color Red = Color.FromRgb(232, 54, 79), Ice = Color.FromRgb(169, 207, 255);
    public static Viewbox Glyph(double size)
    {
        var canvas = new Canvas { Width = 64, Height = 64 };
        var white = new SolidColorBrush(Color.FromArgb(245, 255, 255, 255));
        canvas.Children.Add(new Path { Data = Geometry.Parse("M 16,18 H 48 C 56,18 60,23 60,31 L 60,44 C 60,51 56,54 51,54 C 47,54 45,51 44,49 Q 42,46 39,46 H 25 Q 22,46 20,49 C 19,51 17,54 13,54 C 8,54 4,51 4,44 L 4,31 C 4,23 8,18 16,18 Z"),
            Stroke = white, StrokeThickness = 3, StrokeLineJoin = PenLineJoin.Round, Fill = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)) });
        // the sticks are the eyes
        foreach (double x in new[] { 19, 45 })
        {
            canvas.Children.Add(Place(new Ellipse { Width = 13, Height = 13, Stroke = white, StrokeThickness = 1.6 }, x - 6.5, 25.5));
            canvas.Children.Add(Place(new Rectangle { Width = 4.5, Height = 8, RadiusX = 2.25, RadiusY = 2.25, Fill = new SolidColorBrush(Ice), Effect = new DropShadowEffect { Color = Ice, BlurRadius = 8, ShadowDepth = 0, Opacity = 1 } }, x - 2.25, 28));
        }
        canvas.Children.Add(Place(new Ellipse { Width = 5, Height = 5, Fill = new SolidColorBrush(Red), Effect = new DropShadowEffect { Color = Red, BlurRadius = 8, ShadowDepth = 0, Opacity = .9 } }, 29.5, 23));
        return new Viewbox { Width = size, Height = size, Child = canvas };
    }
    static UIElement Place(UIElement e, double x, double y) { Canvas.SetLeft(e, x); Canvas.SetTop(e, y); return e; }
    public static Brush Field() => new RadialGradientBrush(new GradientStopCollection { new(Color.FromRgb(92, 160, 255), 0), new(Color.FromRgb(40, 104, 228), .55), new(Color.FromRgb(16, 42, 112), 1) }) { GradientOrigin = new(.32, .25), Center = new(.4, .35), RadiusX = .8, RadiusY = .8 };
}

/// <summary>
/// The round button that opens the agent. It floats over the Studio; a dot on its edge says whether the model is ready,
/// and a ring turns only while the agent is thinking (no animation runs while idle).
/// </summary>
public sealed class AgentOrb : Button
{
    readonly Ellipse dot = new() { Width = 15, Height = 15, StrokeThickness = 2.5, Stroke = new SolidColorBrush(Color.FromRgb(6, 10, 22)), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new(0, 0, 3, 3) };
    readonly Ellipse ring = new() { StrokeThickness = 3, Visibility = Visibility.Collapsed, RenderTransformOrigin = new(.5, .5), Margin = new(-6) };
    readonly ScaleTransform scale = new(1, 1);

    public AgentOrb()
    {
        Width = Height = 68; Cursor = Cursors.Hand; FocusVisualStyle = null; ToolTip = "Agent XCTL";
        System.Windows.Automation.AutomationProperties.SetName(this, "Open Agent XCTL");
        Template = new ControlTemplate(typeof(Button)) { VisualTree = new FrameworkElementFactory(typeof(ContentPresenter)) };
        ring.Stroke = new LinearGradientBrush(Color.FromArgb(0, 169, 207, 255), Color.FromArgb(255, 169, 207, 255), 0);
        ring.RenderTransform = new RotateTransform();
        var face = new Grid
        {
            Children =
            {
                ring,
                new Ellipse { Fill = AgentMark.Field(), Stroke = new SolidColorBrush(Color.FromArgb(150, 190, 220, 255)), StrokeThickness = 1.5,
                    Effect = new DropShadowEffect { Color = Color.FromRgb(52, 126, 241), BlurRadius = 26, ShadowDepth = 0, Opacity = .65 } },
                AgentMark.Glyph(40),
                dot,
            },
        };
        RenderTransformOrigin = new(.5, .5); RenderTransform = scale; Content = face;
        MouseEnter += (_, _) => Grow(1.07); MouseLeave += (_, _) => Grow(1);
        PreviewMouseLeftButtonDown += (_, _) => Grow(.95); PreviewMouseLeftButtonUp += (_, _) => Grow(IsMouseOver ? 1.07 : 1);
        dot.Fill = DS.Brush("DS.Success");
    }

    void Grow(double to)
    {
        if (!DS.Motion) { scale.ScaleX = scale.ScaleY = to; return; }
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, DS.To(to, 140)); scale.BeginAnimation(ScaleTransform.ScaleYProperty, DS.To(to, 140));
    }


    public void SetThinking(bool on)
    {
        var turn = (RotateTransform)ring.RenderTransform;
        ring.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        if (on && DS.Motion) turn.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1.1)) { RepeatBehavior = RepeatBehavior.Forever });
        else turn.BeginAnimation(RotateTransform.AngleProperty, null);
    }
}
