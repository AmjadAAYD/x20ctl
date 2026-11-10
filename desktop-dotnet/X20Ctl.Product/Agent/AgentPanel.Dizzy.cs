using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using X20Ctl.Zone.Agent;
using Sh = System.Windows.Shapes;

namespace X20Ctl.Product.Agent;

/// <summary>
/// The second easter egg (owner, 8 Nov 2026): keep spinning the handheld round by its body and it gets dizzy. Its eyes turn
/// into spirals spinning opposite ways, the LEDs wobble round inside the sticks, the whole pad sways, and it says so. Keep
/// going and it begs you to stop; leave it and it shakes it off. Shaking back and forth is not spinning and never counts.
/// </summary>
public partial class AgentPanel
{
    readonly RotateTransform sway = new();
    readonly List<(UIElement Led, FrameworkElement Spiral)> spiralEyes = [];
    int dizzyRun, dizzyLevel; DateTime dizzyUntil;
    // the spin so far: the heading of the last stroke, the turning added up, and when the pointer last moved
    double spinTurned; double? spinHeading; Point? spinLast; DateTime spinAt;
    /// <summary>How many full turns in a row make it dizzy.</summary>
    internal const int TurnsToDizzy = 3;
    bool Dizzy => DateTime.UtcNow < dizzyUntil;

    /// <summary>A spiral for a stick's eye, hidden under its LED until it's needed; the two spin opposite ways.</summary>
    void SpiralEye(Canvas host, UIElement led, int inward)
    {
        var figure = new PathFigure { StartPoint = new(35, 35) };
        var line = new PolyLineSegment();
        for (double t = 0; t <= Math.PI * 5; t += .12) { double r = 3 + 2.0 * t; line.Points.Add(new(35 + r * Math.Cos(t * inward), 35 + r * Math.Sin(t * inward))); }
        figure.Segments.Add(line);
        var spiral = new Sh.Path
        {
            Data = new PathGeometry { Figures = { figure } }, Width = 70, Height = 70, StrokeThickness = 6.5, Stroke = new SolidColorBrush(eyeColor),
            StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round, IsHitTestVisible = false,
            Visibility = Visibility.Collapsed, RenderTransformOrigin = new(.5, .5), RenderTransform = new RotateTransform(),
            Effect = new DropShadowEffect { Color = eyeColor, BlurRadius = 20, ShadowDepth = 0, Opacity = 1 },
        };
        Canvas.SetLeft(spiral, 27); Canvas.SetTop(spiral, 27); host.Children.Add(spiral);
        ledParts.Add(spiral); spiralEyes.Add((led, spiral));
    }

    /// <summary>Called as the handheld is dragged: adds up how far the pointer's path has turned. Three full turns in a row,
    /// without stopping or doubling back, and it's dizzy.</summary>
    void SpinAt(Point p)
    {
        var now = DateTime.UtcNow;
        if ((now - spinAt).TotalMilliseconds > 450) { spinTurned = 0; spinHeading = null; spinLast = p; }
        spinAt = now;
        if (spinLast is not Point last) { spinLast = p; return; }
        var step = p - last;
        if (step.Length < 6) return;                      // too small a move to have a direction
        spinLast = p;
        double heading = Math.Atan2(step.Y, step.X);
        if (spinHeading is double before)
        {
            double turn = heading - before;
            while (turn > Math.PI) turn -= 2 * Math.PI;
            while (turn < -Math.PI) turn += 2 * Math.PI;
            if (Math.Abs(turn) > 2.0) spinTurned = 0;      // it doubled back: shaking, not spinning
            else if (spinTurned != 0 && Math.Sign(turn) != Math.Sign(spinTurned) && Math.Abs(turn) > .6) spinTurned = turn; // turned the other way
            else spinTurned += turn;
        }
        spinHeading = heading;
        if (Math.Abs(spinTurned) >= 2 * Math.PI * TurnsToDizzy) { spinTurned = 0; GetDizzy(); }
    }

    /// <summary>Spirals for eyes, the LEDs going round, a sway, and a word about it; again while dizzy and it pleads.</summary>
    async void GetDizzy()
    {
        int me = ++dizzyRun;
        bool already = Dizzy;
        dizzyLevel = already ? dizzyLevel + 1 : 1;
        dizzyUntil = DateTime.UtcNow.AddSeconds(4.5);
        if (!already)
        {
            loving++; foreach (var (led, heart) in heartEyes) { heart.Visibility = Visibility.Collapsed; led.Visibility = Visibility.Visible; }
            foreach (var (led, spiral) in spiralEyes)
            {
                led.Visibility = Visibility.Hidden; spiral.Visibility = Visibility.Visible;
                if (Motion && spiral.RenderTransform is RotateTransform spin)
                    spin.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(0, 360, TimeSpan.FromMilliseconds(900)) { RepeatBehavior = RepeatBehavior.Forever });
            }
            if (Motion)
            {
                var rock = new DoubleAnimationUsingKeyFrames { RepeatBehavior = RepeatBehavior.Forever };
                double t = 0; foreach (double a in new[] { 3.5, -3, 2.5, -3.5, 0 }) rock.KeyFrames.Add(new EasingDoubleKeyFrame(a, TimeSpan.FromMilliseconds(t += 420)) { EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } });
                sway.BeginAnimation(RotateTransform.AngleProperty, rock);
            }
            _ = AgentRumble.Pulse(player, 180);
        }
        if (!busy && dizzyLevel <= 2) Say(AgentCharacter.Dizzy(dizzyLevel), null);
        while (DateTime.UtcNow < dizzyUntil) { await Task.Delay(250); if (me != dizzyRun) return; }
        CalmDown(dizzy: true, love: false);
        if (!busy && IsShown) Say(AgentCharacter.Recovered(), null);
    }

    /// <summary>Back to its usual self: no spirals, no hearts, no sway, the usual light.</summary>
    void CalmDown(bool dizzy = true, bool love = true)
    {
        if (dizzy)
        {
            dizzyRun++; dizzyUntil = default; dizzyLevel = 0; spinTurned = 0;
            foreach (var (led, spiral) in spiralEyes) { spiral.Visibility = Visibility.Collapsed; led.Visibility = Visibility.Visible; (spiral.RenderTransform as RotateTransform)?.BeginAnimation(RotateTransform.AngleProperty, null); }
            if (Motion) sway.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(300)));
            else { sway.BeginAnimation(RotateTransform.AngleProperty, null); sway.Angle = 0; }
        }
        if (love)
        {
            loving++;
            foreach (var (led, heart) in heartEyes) { heart.Visibility = Visibility.Collapsed; led.Visibility = Visibility.Visible; }
            if (bigHeart != null) { bigHeart.BeginAnimation(UIElement.OpacityProperty, null); bigHeart.Visibility = Visibility.Collapsed; }
            beat.BeginAnimation(ScaleTransform.ScaleXProperty, null); beat.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            loveLayer?.Children.RemoveRange(1, Math.Max(0, loveLayer.Children.Count - 1));
        }
        Steady();
    }

    /// <summary>While dizzy the LEDs are pushed round and round inside their sticks, each the other way: the physics does the wobble.</summary>
    Vector DizzyPull(int index)
    {
        if (!Dizzy) return default;
        double t = Environment.TickCount64 / 1000.0 * 7.5, dir = index % 2 == 0 ? 1 : -1;
        return new Vector(Math.Cos(t * dir), Math.Sin(t * dir)) * 22;
    }

    /// <summary>A review spins the handheld by feeding the pointer a circle, as a hand dragging it round would.</summary>
    internal void ReviewSpin(int turns)
    {
        spinAt = DateTime.UtcNow; spinLast = null; spinHeading = null; spinTurned = 0;
        for (double a = 0; a <= turns * 2 * Math.PI + 1; a += .25) SpinAt(new Point(600 + 120 * Math.Cos(a), 400 + 120 * Math.Sin(a)));
    }
}
