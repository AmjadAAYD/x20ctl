using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using X20Ctl.Zone.Agent;
using Sh = System.Windows.Shapes;

namespace X20Ctl.Product.Agent;

/// <summary>
/// The easter egg (owner, 8 Nov 2026): told "I love you", the controller says it back. Its eyes turn into hearts, a heart
/// beats over the screen, little hearts float up from the sticks, its lights go pink, and the whole handheld beats like a
/// heart, lub-dub, for about seven seconds, in the hand too (the real pad's rumble). Nothing is changed on the controller.
/// </summary>
public partial class AgentPanel
{
    static readonly Color LovePink = ThemeCatalog.R(Color.FromRgb(255, 77, 136));
    const string HeartShape = "M 16,30 C 6,22 0,16 0,9 C 0,3 4,0 8.5,0 C 12,0 14.5,2 16,4.5 C 17.5,2 20,0 23.5,0 C 28,0 32,3 32,9 C 32,16 26,22 16,30 Z";
    readonly ScaleTransform beat = new();
    readonly List<(UIElement Led, UIElement Heart)> heartEyes = [];
    Canvas? loveLayer; Sh.Path? bigHeart; readonly ScaleTransform bigBeat = new();
    int loving;

    // How long the heartbeat lasts (owner, 8 Nov 2026: "increase the heartbeat timer by 4 seconds", from 3.6 s to 7.6 s).
    // Everything is timed from these: the beats on screen, the beats in the hand, the hearts, and the lights going back.
    const int LoveMs = 7600, BeatMs = 900, LoveBeats = LoveMs / BeatMs;
    /// <summary>How strong beat i is, 0..1: full for most of it, then it settles over the last three, so the heartbeat
    /// winds down instead of stopping dead.</summary>
    static double BeatStrength(int i) => i < LoveBeats - 3 ? 1 : 1 - (i - (LoveBeats - 4)) * .17;

    static Sh.Path Heart(double w, Color c, double glow) => new()
    {
        Data = Geometry.Parse(HeartShape), Width = w, Height = w * 30 / 32, Stretch = Stretch.Uniform, IsHitTestVisible = false,
        Fill = new RadialGradientBrush { GradientOrigin = new(.35, .3), GradientStops = { new(Color.FromRgb(255, 170, 200), 0), new(c, .55), new(Color.FromRgb((byte)(c.R * .7), (byte)(c.G * .5), (byte)(c.B * .6)), 1) } },
        Effect = glow > 0 ? new DropShadowEffect { Color = c, BlurRadius = glow, ShadowDepth = 0, Opacity = .95 } : null,
    };

    /// <summary>A stick's heart, hidden under its LED until it's needed.</summary>
    void HeartEye(Canvas host, UIElement led)
    {
        var heart = Heart(66, LovePink, 26); heart.Visibility = Visibility.Collapsed;
        Canvas.SetLeft(heart, 29); Canvas.SetTop(heart, 31); host.Children.Add(heart);
        heartEyes.Add((led, heart));
    }

    /// <summary>The heart over the screen and the layer the little hearts float in, above everything on the pad.</summary>
    void BuildLove(Canvas pad)
    {
        pad.RenderTransform = new TransformGroup { Children = { beat, sway, (Transform)pad.RenderTransform } };
        beat.CenterX = sway.CenterX = DesignWidth / 2; beat.CenterY = DesignHeight / 2; sway.CenterY = DesignHeight * .7;
        loveLayer = new Canvas { Width = DesignWidth, Height = DesignHeight, IsHitTestVisible = false };
        bigHeart = Heart(230, LovePink, 60); bigHeart.Visibility = Visibility.Collapsed; bigHeart.RenderTransformOrigin = new(.5, .5); bigHeart.RenderTransform = bigBeat;
        Canvas.SetLeft(bigHeart, ScreenX + ScreenW / 2 - 115); Canvas.SetTop(bigHeart, ScreenY + ScreenH / 2 - 150);
        loveLayer.Children.Add(bigHeart);
        Canvas.SetZIndex(loveLayer, 100); pad.Children.Add(loveLayer);
    }

    /// <summary>"I love you too!" (owner, 8 Nov 2026): it acts as if it's thinking, while really it turns the controller's own
    /// lights pink (that write is what takes the couple of seconds); then hearts for eyes, a beating heart, a strong heartbeat
    /// in the hand, and afterwards the controller's lights go back to what they were.</summary>
    async void LoveBack()
    {
        int me = ++loving;
        busy = true; Searching(true); Circling(true); ShowState(); Thinking?.Invoke(true);
        byte[]? was = null;
        try
        {
            if (modelId == AgentPad.Model)
                try
                {
                    var before = await PadLights();
                    if (before.Length == 24 && before[6] is 0 or 2 or 3 or 4)
                    {
                        was = before;
                        await PadLights([255, 30, 120], 2);
                    }
                }
                catch (Exception error) when (PadFailure(error)) { } // a pad on an effect, or out of reach: the egg plays on screen alone
            else await Task.Delay(1200);
        }
        finally { busy = false; Circling(false); Searching(false); Thinking?.Invoke(false); ShowState(); }
        if (was != null) _ = PutLightsBack(was, padLightWrites);
        if (me != loving) return;
        Say(AgentCharacter.LoveBack(), null);
        if (Dizzy) CalmDown(dizzy: true, love: false);
        laughing++; foreach (var (_, squint) in squints) squint.Visibility = Visibility.Collapsed;
        lightShow++; lightsOn = true; typingLit = false;
        SetLights(LovePink);
        foreach (var (led, heart) in heartEyes) { led.Visibility = Visibility.Hidden; heart.Visibility = Visibility.Visible; if (Motion) Fx.Appear((FrameworkElement)heart, .4, 260); }
        if (bigHeart != null) { bigHeart.BeginAnimation(UIElement.OpacityProperty, null); bigHeart.Opacity = .92; bigHeart.Visibility = Visibility.Visible; }
        _ = Heartbeat(me);
        if (Motion)
        {
            Pulse(beat, 1.022); Pulse(bigBeat, 1.18);
            if (bigHeart != null) Fx.Appear(bigHeart, .3, 320);
            FloatHearts();
        }
        await Task.Delay(LoveMs);
        if (me != loving) return;
        if (bigHeart != null)
        {
            if (Motion) { var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(500)); fade.Completed += (_, _) => { if (me == loving) bigHeart.Visibility = Visibility.Collapsed; }; bigHeart.BeginAnimation(UIElement.OpacityProperty, fade); }
            else bigHeart.Visibility = Visibility.Collapsed;
        }
        await Task.Delay(600);
        if (me != loving) return;
        foreach (var (led, heart) in heartEyes) { heart.Visibility = Visibility.Collapsed; led.Visibility = Visibility.Visible; }
        Steady();
    }

    /// <summary>Lub-dub over the whole heartbeat: a strong beat, a softer one just after, then a rest, settling at the end.</summary>
    static void Pulse(ScaleTransform s, double peak)
    {
        var a = new DoubleAnimationUsingKeyFrames();
        for (int i = 0; i < LoveBeats; i++)
        {
            double t = i * BeatMs, lub = 1 + (peak - 1) * BeatStrength(i), dub = 1 + (lub - 1) * .6;
            a.KeyFrames.Add(new EasingDoubleKeyFrame(1, TimeSpan.FromMilliseconds(t)));
            a.KeyFrames.Add(new EasingDoubleKeyFrame(lub, TimeSpan.FromMilliseconds(t + 110)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
            a.KeyFrames.Add(new EasingDoubleKeyFrame(1, TimeSpan.FromMilliseconds(t + 230)));
            a.KeyFrames.Add(new EasingDoubleKeyFrame(dub, TimeSpan.FromMilliseconds(t + 330)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
            a.KeyFrames.Add(new EasingDoubleKeyFrame(1, TimeSpan.FromMilliseconds(t + 520)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } });
        }
        s.BeginAnimation(ScaleTransform.ScaleXProperty, a); s.BeginAnimation(ScaleTransform.ScaleYProperty, a.Clone());
    }

    /// <summary>The real controller beats too, hard (owner: the vibration felt weak), in step with the screen: each lub and
    /// dub starts at its beat's own time on one clock, so the hand and the screen can't drift apart over the longer
    /// heartbeat, and it settles at the end the same way the screen does.</summary>
    async Task Heartbeat(int me)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        async Task At(double ms) { var wait = ms - clock.Elapsed.TotalMilliseconds; if (wait > 1) await Task.Delay(TimeSpan.FromMilliseconds(wait)); }
        for (int i = 0; i < LoveBeats && me == loving; i++)
        {
            double t = i * BeatMs, k = BeatStrength(i);
            // a beat already late (the app was busy a moment) is skipped rather than played on top of the next one
            if (clock.Elapsed.TotalMilliseconds > t + 250) continue;
            await At(t); if (me != loving) break;
            await AgentRumble.Pulse(player, 170, (ushort)(65535 * k), (ushort)(65535 * k), k);
            await At(t + 230); if (me != loving) break;
            await AgentRumble.Pulse(player, 130, (ushort)(50000 * k), (ushort)(65535 * k), .7 * k);
        }
    }

    /// <summary>The heartbeat over, the controller's lights go back to what they were, unless a lights change was asked
    /// for meanwhile (that one stays).</summary>
    async Task PutLightsBack(byte[] was, int writes)
    {
        await Task.Delay(LoveMs + 700);
        if (writes != padLightWrites) return;
        await RestoreLights(was);
    }

    /// <summary>Little hearts rise from both sticks and fade, a few at a time, all through the heartbeat (they stop in time
    /// to be gone when it ends).</summary>
    void FloatHearts()
    {
        if (loveLayer == null || handheld == null) return;
        var g = Handhelds[FinishModel(modelId)];
        var rng = new Random();
        int count = (LoveMs - 2000) / 380;
        foreach (var from in new[] { g.LeftStick, g.RightStick })
            for (int i = 0; i < count; i++)
            {
                double w = 18 + rng.NextDouble() * 22;
                var h = Heart(w, LovePink, 14); h.Opacity = 0;
                var move = new TranslateTransform(); var turn = new RotateTransform(rng.NextDouble() * 40 - 20);
                h.RenderTransformOrigin = new(.5, .5); h.RenderTransform = new TransformGroup { Children = { turn, move } };
                Canvas.SetLeft(h, from.X - w / 2 + rng.NextDouble() * 60 - 30); Canvas.SetTop(h, from.Y - 40);
                loveLayer.Children.Add(h);
                var start = TimeSpan.FromMilliseconds(i * 380 + rng.Next(120));
                var dur = TimeSpan.FromMilliseconds(1500 + rng.Next(500));
                move.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, -170 - rng.Next(90), dur) { BeginTime = start, EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
                move.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, rng.Next(-40, 40), dur) { BeginTime = start });
                var fade = new DoubleAnimationUsingKeyFrames { BeginTime = start, KeyFrames = { new LinearDoubleKeyFrame(1, TimeSpan.FromMilliseconds(200)), new LinearDoubleKeyFrame(1, dur * .55), new LinearDoubleKeyFrame(0, dur) } };
                fade.Completed += (_, _) => loveLayer.Children.Remove(h);
                h.BeginAnimation(UIElement.OpacityProperty, fade);
            }
    }
}
