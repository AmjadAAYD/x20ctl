using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
namespace X20Ctl.Product;

/// <summary>
/// Motion used everywhere (owner direction 10 Oct 2026: "whenever i click on ANYTHING, EVERYTHING must have an
/// animation"). Screens arrive with a short zoom-and-fade ("fwoosh") and leave the same way instead of snapping, and
/// every button, tab, card and toggle in the app gets a press-in and spring-back, whether it is clicked, tapped with
/// the keyboard or pressed on the controller. Reduced motion (Windows animation setting off) turns it all off.
/// </summary>
public static class Fx
{
    private static bool registered;
    private static readonly IEasingFunction Out = Freeze(new CubicEase { EasingMode = EasingMode.EaseOut });
    private static readonly IEasingFunction Spring = Freeze(new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = .55 });
    private static IEasingFunction Freeze(EasingFunctionBase e) { e.Freeze(); return e; }

    /// <summary>A screen or panel arriving: from a little larger (or smaller) and transparent, to rest.</summary>
    public static void Appear(FrameworkElement e, double fromScale = 1.05, int ms = 340, Action? done = null)
    {
        if (!SystemParameters.ClientAreaAnimation) { e.Opacity = 1; done?.Invoke(); return; }
        var s = OwnScale(e);
        e.CacheMode ??= new BitmapCache(1);
        var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(ms * .8)) { EasingFunction = Out };
        fade.Completed += (_, _) => { e.CacheMode = null; done?.Invoke(); };
        e.BeginAnimation(UIElement.OpacityProperty, fade);
        var grow = new DoubleAnimation(fromScale, 1, TimeSpan.FromMilliseconds(ms)) { EasingFunction = Out };
        s.BeginAnimation(ScaleTransform.ScaleXProperty, grow); s.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
    }
    /// <summary>A screen or panel leaving: it stops taking input at once, fades and drifts in scale, then is gone.</summary>
    public static void Leave(FrameworkElement e, Action gone, double toScale = 1.04, int ms = 240)
    {
        e.IsHitTestVisible = false; e.IsEnabled = false;
        if (!SystemParameters.ClientAreaAnimation) { gone(); return; }
        var s = OwnScale(e);
        var fade = new DoubleAnimation(e.Opacity, 0, TimeSpan.FromMilliseconds(ms)) { EasingFunction = Out };
        fade.Completed += (_, _) => gone();
        e.BeginAnimation(UIElement.OpacityProperty, fade);
        var shift = new DoubleAnimation(1, toScale, TimeSpan.FromMilliseconds(ms)) { EasingFunction = Out };
        s.BeginAnimation(ScaleTransform.ScaleXProperty, shift); s.BeginAnimation(ScaleTransform.ScaleYProperty, shift);
    }

    /// <summary>Press-in / spring-back for every ButtonBase in the app, registered once.</summary>
    public static void RegisterPressFeedback()
    {
        if (registered) return; registered = true;
        EventManager.RegisterClassHandler(typeof(ButtonBase), UIElement.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler((s, _) => Press((ButtonBase)s, true)), true);
        EventManager.RegisterClassHandler(typeof(ButtonBase), UIElement.PreviewMouseLeftButtonUpEvent, new MouseButtonEventHandler((s, _) => Press((ButtonBase)s, false)), true);
        EventManager.RegisterClassHandler(typeof(ButtonBase), UIElement.MouseLeaveEvent, new MouseEventHandler((s, _) => { if (pressed == s) Press((ButtonBase)s, false); }), true);
        // keyboard, gamepad and automation activation have no mouse-down: give them the whole tap
        EventManager.RegisterClassHandler(typeof(ButtonBase), ButtonBase.ClickEvent, new RoutedEventHandler((s, _) => { if (pressed != s) Tap((ButtonBase)s); }), true);
    }
    private static ButtonBase? pressed;
    private static void Press(ButtonBase b, bool down)
    {
        if (!SystemParameters.ClientAreaAnimation || !b.IsEnabled || Huge(b)) return;
        if (down) pressed = b; else if (pressed == b) pressed = null; else return;
        if (OwnScaleOrNull(b) is not { } s) return;
        var a = new DoubleAnimation(down ? .94 : 1, TimeSpan.FromMilliseconds(down ? 80 : 260)) { EasingFunction = down ? Out : Spring };
        s.BeginAnimation(ScaleTransform.ScaleXProperty, a); s.BeginAnimation(ScaleTransform.ScaleYProperty, a);
    }
    private static void Tap(ButtonBase b)
    {
        if (!SystemParameters.ClientAreaAnimation || Huge(b) || OwnScaleOrNull(b) is not { } s) return;
        var a = new DoubleAnimationUsingKeyFrames();
        a.KeyFrames.Add(new EasingDoubleKeyFrame(.93, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(70)), Out));
        a.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(330)), Spring));
        s.BeginAnimation(ScaleTransform.ScaleXProperty, a); s.BeginAnimation(ScaleTransform.ScaleYProperty, a);
    }
    // full-card hit targets (the Zone's dock buttons) would shrink the whole card: they animate their own way
    private static bool Huge(FrameworkElement b) => b.ActualWidth > 700 && b.ActualHeight > 300;

    /// <summary>The element's own, animatable ScaleTransform, centred. A transform shared from a style is frozen, so it
    /// is replaced by a private one; any other kind of transform is wrapped so its owner's reference keeps working.</summary>
    private static ScaleTransform OwnScale(FrameworkElement e)
    {
        e.RenderTransformOrigin = new(.5, .5);
        switch (e.RenderTransform)
        {
            case ScaleTransform own when !own.IsFrozen && !own.IsSealed: return own;
            case ScaleTransform frozen: { var copy = new ScaleTransform(frozen.ScaleX, frozen.ScaleY); e.RenderTransform = copy; return copy; }
            case null: case MatrixTransform { Matrix.IsIdentity: true }: { var fresh = new ScaleTransform(1, 1); e.RenderTransform = fresh; return fresh; }
            case TransformGroup group when !group.IsFrozen && group.Children.OfType<ScaleTransform>().FirstOrDefault(t => !t.IsFrozen) is { } inside: return inside;
            default: { var scale = new ScaleTransform(1, 1); e.RenderTransform = new TransformGroup { Children = { e.RenderTransform, scale } }; return scale; }
        }
    }
    private static ScaleTransform? OwnScaleOrNull(FrameworkElement e) =>
        e.RenderTransform is null or ScaleTransform or MatrixTransform { Matrix.IsIdentity: true } ? OwnScale(e) : e.RenderTransform is TransformGroup { IsFrozen: false } g ? g.Children.OfType<ScaleTransform>().FirstOrDefault(t => !t.IsFrozen) : null;
}
