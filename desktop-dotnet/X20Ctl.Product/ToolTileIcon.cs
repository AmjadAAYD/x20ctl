using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace X20Ctl.Product;

/// <summary>Small vector stories for Tools cards. They play on their own while visible; a hidden icon owns no frame callback.
/// Bold strokes and filled shapes with a spring in their step, one colour accent per story, and a livelier, faster
/// version of the same story while the card is hovered or focused.</summary>
internal sealed class ToolTileIcon : FrameworkElement
{
    const double Loop = 3.6;
    readonly string title;
    readonly Stopwatch clock = new();
    bool active;
    double time, lastElapsed, heat, heatTarget;
    public bool IsAnimating { get; private set; }

    static Brush Solid(byte r, byte g, byte b, byte a = 255) { var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b)); brush.Freeze(); return brush; }
    static readonly Brush Ink = Solid(255, 255, 255), Soft = Solid(255, 255, 255, 120), Blue = Solid(88, 170, 255), Green = Solid(66, 214, 140),
        Gold = Solid(255, 205, 80), Rose = Solid(255, 94, 124), Dark = Solid(8, 14, 30);
    static Pen Stroke(Brush brush, double width = 2.8) { var pen = new Pen(brush, width) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round }; pen.Freeze(); return pen; }
    static readonly Pen InkLine = Stroke(Ink), SoftLine = Stroke(Soft, 2), BlueLine = Stroke(Blue), GreenLine = Stroke(Green, 3.2), GoldLine = Stroke(Gold, 2.2), RoseLine = Stroke(Rose, 2.4);
    static readonly Geometry Star = Geometry.Parse("M24,6 L29.2,17.6 L41.8,19 L32.4,27.6 L35,40 L24,33.6 L13,40 L15.6,27.6 L6.2,19 L18.8,17.6 Z");
    static readonly Geometry Heart = Geometry.Parse("M24,40 C19,35 6,27 6,16.5 C6,8.5 15,6 20,10.5 C22,12 23.2,13.4 24,15 C24.8,13.4 26,12 28,10.5 C33,6 42,8.5 42,16.5 C42,27 29,35 24,40 Z");
    static readonly Geometry Bubble = Geometry.Parse("M9,9 H33 Q38,9 38,14 V27 Q38,32 33,32 H21 L13,39 V32 H9 Q4,32 4,27 V14 Q4,9 9,9 Z");
    // Drawing runs on the UI thread only; the glyphs must be laid out at the screen's real DPI or they blur above 100%.
    static double pixelsPerDip = 1;
    static readonly Typeface IconFace = new(new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

    public ToolTileIcon(string title)
    {
        this.title = title; Width = Height = 36; IsHitTestVisible = false;
        Loaded += (_, _) => UpdateClock(); Unloaded += (_, _) => StopClock();
        IsVisibleChanged += (_, _) => UpdateClock();
    }
    public void SetActive(bool value) { if (active == value) return; active = value; time = 0; UpdateClock(); InvalidateVisual(); }
    /// <summary>Hover or keyboard focus: the story speeds up and the icon swells with a soft halo.</summary>
    public void SetHot(bool value) { heatTarget = value ? 1 : 0; if (!IsAnimating) { heat = heatTarget; InvalidateVisual(); } }
    public void Seek(double seconds) { StopClock(); time = seconds; InvalidateVisual(); }
    void UpdateClock()
    {
        if (!active || !IsVisible || !IsLoaded || !DS.Motion) { StopClock(); time = 0; heat = heatTarget; InvalidateVisual(); return; }
        if (IsAnimating) return;
        clock.Restart(); lastElapsed = 0; IsAnimating = true; CompositionTarget.Rendering += Frame;
    }
    void StopClock() { CompositionTarget.Rendering -= Frame; clock.Stop(); IsAnimating = false; }
    void Frame(object? sender, EventArgs e)
    {
        if (!DS.Motion || !IsVisible || !IsLoaded) { StopClock(); time = 0; InvalidateVisual(); return; }
        // 30 frames a second is plenty for a 36 px icon, and keeps eight of them cheap
        double now = clock.Elapsed.TotalSeconds, dt = now - lastElapsed;
        if (dt < 1.0 / 30) return;
        lastElapsed = now; dt = Math.Min(dt, .1);
        heat += (heatTarget - heat) * Math.Min(1, dt * 9);
        time += dt * (1 + .8 * heat);
        InvalidateVisual();
    }

    // ---- easing: every move starts, overshoots a little, and settles ----
    static double Step(double t, double start, double duration) => Math.Clamp((t - start) / duration, 0, 1);
    static double Ease(double p) => p * p * (3 - 2 * p);
    static double Out(double p) => 1 - Math.Pow(1 - p, 3);
    static double Back(double p) { const double c = 1.70158; return 1 + (c + 1) * Math.Pow(p - 1, 3) + c * Math.Pow(p - 1, 2); }
    static double Spring(double p) => p <= 0 ? 0 : p >= 1 ? 1 : Math.Pow(2, -9 * p) * Math.Sin((p * 9 - .75) * 2 * Math.PI / 3) + 1;
    static double Pulse(double t, double start, double duration) { double p = Step(t, start, duration); return Math.Sin(p * Math.PI); }
    /// <summary>1 while the story is on screen, easing out over the last moments so the loop restarts from rest.</summary>
    static double Fade(double t) => 1 - Ease(Step(t, Loop - .45, .4));
    static Point P(double x, double y) => new(x, y);
    static void Line(DrawingContext d, Pen pen, double x, double y, double x2, double y2) => d.DrawLine(pen, P(x, y), P(x2, y2));
    static void Check(DrawingContext d, Pen pen, double x, double y, double s = 1) { Line(d, pen, x - 4 * s, y, x - 1 * s, y + 3 * s); Line(d, pen, x - 1 * s, y + 3 * s, x + 5 * s, y - 4 * s); }
    static void Glyph(DrawingContext d, string glyph, Brush brush, double x = 24, double y = 24, double size = 27)
    {
        var text = new FormattedText(glyph, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, IconFace, size, brush, pixelsPerDip);
        d.DrawText(text, P(x - text.Width / 2, y - text.Height / 2));
    }
    static void Scaled(DrawingContext d, double s, double cx, double cy, Action draw) { d.PushTransform(new ScaleTransform(s, s, cx, cy)); draw(); d.Pop(); }

    static double Rest(string title) => title switch
    {
        "Controller not working?" => 2.1, "Send feedback" => 1.1, "Replay Intro" => .15, "Write scan" or "PC mode" => .55,
        "Support X20CTL" => 1.5, "Star on GitHub" => 2.0, _ => 1.4,
    };

    protected override void OnRender(DrawingContext d)
    {
        // Still (reduced motion, hidden, or before the first frame): the pose that says what the card is, never a blank start.
        base.OnRender(d); double t = !IsAnimating && time == 0 ? Rest(title) : time % Loop; pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        d.PushTransform(new ScaleTransform(ActualWidth / 48, ActualHeight / 48));
        if (heat > .01)
        {   // hovered: a soft halo and a gentle swell
            d.PushOpacity(.28 * heat); d.DrawEllipse(Blue, null, P(24, 24), 23, 23); d.Pop();
            d.PushTransform(new ScaleTransform(1 + .1 * heat, 1 + .1 * heat, 24, 24));
        }
        switch (title)
        {
            case "Controller not working?": Repair(d, t); break;
            case "Send feedback": Feedback(d, t); break;
            case "Replay Intro": Replay(d, t); break;
            case "Write scan": Link(d, t, false); break;
            case "PC mode": Link(d, t, true); break;
            case "Support X20CTL": Heartbeat(d, t); break;
            case "Star on GitHub": Shine(d, t); break;
            default: About(d, t); break;
        }
        if (heat > .01) d.Pop();
        d.Pop();
    }

    // The wrench drops in, bites twice with a spark each time, then a green tick pops.
    static void Repair(DrawingContext d, double t)
    {
        double fade = Fade(t), arrive = Back(Step(t, 0, .45));
        double bite = t < .5 ? 0 : t < 1.5 ? -14 * Math.Pow(Math.Sin((t - .5) * Math.PI * 2), 2) * (t < 1.0 ? 1 : .8) : 0;
        double turn = (1 - arrive) * -50 + bite;
        d.PushOpacity(Math.Min(1, Step(t, 0, .2) * 1.2) * fade);
        d.PushTransform(new RotateTransform(turn, 30, 18)); d.PushTransform(new ScaleTransform(.82 + .18 * arrive, .82 + .18 * arrive, 24, 24));
        Glyph(d, "", Ink, 24, 24, 34); d.Pop(); d.Pop();
        foreach (double hit in new[] { .5, 1.0 })
        {
            double s = Step(t, hit, .3); if (s <= 0 || s >= 1) continue;
            d.PushOpacity((1 - s) * fade);
            for (int i = 0; i < 4; i++) { double a = -2.4 + i * .5; Line(d, GoldLine, 32 + Math.Cos(a) * (5 + 8 * s), 12 + Math.Sin(a) * (5 + 8 * s), 32 + Math.Cos(a) * (8 + 10 * s), 12 + Math.Sin(a) * (8 + 10 * s)); }
            d.Pop();
        }
        double badge = Back(Step(t, 1.7, .45)) * fade;
        if (badge > 0) { Scaled(d, badge, 36, 36, () => { d.DrawEllipse(Green, null, P(36, 36), 9, 9); Check(d, Stroke(Dark, 2.8), 36, 36, 1.05); }); }
    }

    // The note writes itself, squeezes, flies an arc into the inbox, the inbox bounces and ticks.
    static void Feedback(DrawingContext d, double t)
    {
        double fade = Fade(t);
        double write = Step(t, .1, .9), squeeze = Pulse(t, 1.0, .3), fly = Ease(Step(t, 1.25, .8)), land = Spring(Step(t, 2.05, .6));
        // the inbox, bouncing when the note lands
        double bounce = 1 + .22 * Math.Sin(Step(t, 2.05, .5) * Math.PI) * (1 - Step(t, 2.05, .5));
        Scaled(d, bounce, 37, 36, () =>
        {
            d.DrawRoundedRectangle(null, BlueLine, new Rect(29, 29, 16, 12), 3, 3);
            d.DrawGeometry(null, BlueLine, Geometry.Parse("M29,31 L37,36 L45,31"));
        });
        double x = fly * 21, y = -Math.Sin(fly * Math.PI) * 9 + fly * 8, s = (1 - .62 * fly) * (1 - .06 * squeeze);
        double opacity = fade * (1 - Step(t, 2.0, .15));
        d.PushOpacity(opacity); d.PushTransform(new TranslateTransform(x, y)); d.PushTransform(new ScaleTransform(s, s, 21, 24)); d.PushTransform(new RotateTransform(fly * 14, 21, 24));
        d.DrawGeometry(Solid(255, 255, 255, 28), InkLine, Bubble);
        for (int i = 0; i < 3; i++) Line(d, InkLine, 10, 17 + i * 5.5, 10 + (i == 2 ? 13 : 22) * Out(Step(write, i * .28, .42)), 17 + i * 5.5);
        d.Pop(); d.Pop(); d.Pop(); d.Pop();
        double tick = Back(Step(t, 2.2, .4)) * fade;
        if (tick > 0) Scaled(d, tick, 37, 36, () => { d.DrawEllipse(Green, null, P(37, 36), 6, 6); Check(d, Stroke(Dark, 2), 37, 36, .7); });
    }

    // Two bars squash and slide into a play triangle that pops and sends out two rings, then it folds back to bars.
    static void Replay(DrawingContext d, double t)
    {
        double toPlay = Back(Step(t, .35, .55)), back = Ease(Step(t, 3.0, .45)), play = toPlay * (1 - back);
        d.PushOpacity(1 - Math.Clamp(play * 1.4, 0, 1));
        double squash = 1 - .35 * play;
        d.DrawRoundedRectangle(Ink, null, new Rect(14 + play * 6, 10 + play * 6, 7, 28 * squash), 2.2, 2.2);
        d.DrawRoundedRectangle(Ink, null, new Rect(27 - play * 6, 10 + play * 6, 7, 28 * squash), 2.2, 2.2);
        d.Pop();
        d.PushOpacity(Math.Clamp(play * 1.4, 0, 1));
        Scaled(d, .5 + .5 * play, 25, 24, () => d.DrawGeometry(Blue, Stroke(Blue, 2.4), Geometry.Parse("M17,10 L38,24 L17,38 Z")));
        d.Pop();
        for (int i = 0; i < 2; i++)
        {
            double p = Step(t, .95 + i * .35, 1.0); if (p <= 0 || p >= 1) continue;
            d.PushOpacity((1 - p) * .85); d.DrawEllipse(null, Stroke(Blue, 2.4 * (1 - p) + .6), P(24, 24), 12 + p * 13, 12 + p * 13); d.Pop();
        }
    }

    // Three devices pass a glowing packet along: each pops in, the packet streaks across, and the link ticks green.
    static void Link(DrawingContext d, double t, bool pc)
    {
        string[] icons = pc ? ["", "", ""] : ["", "", ""];
        const double Seg = Loop / 3;
        int stage = Math.Min(2, (int)(t / Seg)); double local = t - stage * Seg;
        double settle = Step(local, 0, .45), leave = Ease(Step(local, .85, .3));
        void Device(int index, double x, double scale, double opacity)
        {
            if (opacity <= .01) return;
            d.PushOpacity(opacity);
            Scaled(d, scale, 24 + x, 22, () =>
            {
                if (pc && index == 2) { Glyph(d, icons[index], Ink, 15 + x, 25, 23); Glyph(d, "", Ink, 34 + x, 22, 21); Check(d, GreenLine, 33 + x, 37, .9); }
                else Glyph(d, icons[index], index == 2 ? Ink : Ink, 24 + x, 22, 31);
            });
            d.Pop();
        }
        Device(stage, -leave * 14, Back(settle) * (1 - .25 * leave), 1 - leave);
        Device((stage + 1) % 3, (1 - leave) * 14, .75 + .25 * leave, leave);
        // the packet streaks along the base line and lights it blue
        double packet = Ease(Step(local, .5, .7));
        d.PushOpacity(.45); Line(d, SoftLine, 8, 42, 40, 42); d.Pop();
        if (packet > 0 && packet < 1)
        {
            Line(d, BlueLine, 8, 42, 8 + 32 * packet, 42);
            d.DrawEllipse(Ink, null, P(8 + 32 * packet, 42), 2.8, 2.8);
        }
        double arrived = Back(Step(local, 1.1, .35));
        if (arrived > 0) Scaled(d, arrived, 40, 42, () => d.DrawEllipse(Green, null, P(40, 42), 3.2, 3.2));
    }

    // A real heartbeat: a hard lub-dub, a rose fill with a highlight, and two ripples.
    static void Heartbeat(DrawingContext d, double t)
    {
        double phase = t % 1.8;
        double beat = Math.Exp(-Math.Pow((phase - .16) / .09, 2)) + .7 * Math.Exp(-Math.Pow((phase - .42) / .1, 2));
        double scale = 1 + .24 * beat;
        for (int i = 0; i < 2; i++)
        {
            double p = Step(phase, .12 + i * .28, .9); if (p <= 0 || p >= 1) continue;
            d.PushOpacity((1 - p) * .55); d.PushTransform(new ScaleTransform(1 + p * .65, 1 + p * .65, 24, 24)); d.DrawGeometry(null, Stroke(Rose, 2.2 * (1 - p) + .5), Heart); d.Pop(); d.Pop();
        }
        Scaled(d, scale, 24, 24, () =>
        {
            d.DrawGeometry(Rose, null, Heart);
            d.PushOpacity(.55); d.DrawEllipse(Ink, null, P(15.5, 15), 2.6, 1.6); d.Pop();
        });
    }

    // The star spins in, pops, fills with gold from the bottom, bursts rays and glints, holds, then drains.
    static void Shine(DrawingContext d, double t)
    {
        double pop = Back(Step(t, 0, .5)), fill = Ease(Step(t, .6, .6)) * (1 - Ease(Step(t, 3.0, .45)));
        double burst = Step(t, 1.0, .7);
        double scale = .55 + .45 * pop + .12 * Math.Sin(Step(t, 1.15, .4) * Math.PI);
        d.PushTransform(new RotateTransform((1 - pop) * -50, 24, 24));
        Scaled(d, scale, 24, 24, () =>
        {
            d.DrawGeometry(null, Stroke(Ink, 2.6), Star);
            d.PushClip(Star); d.DrawRectangle(Gold, null, new Rect(4, 42 - fill * 38, 40, fill * 38)); d.Pop();
            d.DrawGeometry(null, Stroke(Gold, 2.6 * fill + .01), Star);
        });
        d.Pop();
        if (burst > 0 && burst < 1)
        {
            d.PushOpacity(1 - burst);
            for (int i = 0; i < 8; i++)
            {
                double a = i * Math.PI / 4 + .2, r1 = 19 + burst * 5, r2 = r1 + 3 + (1 - burst) * 4;
                Line(d, GoldLine, 24 + Math.Cos(a) * r1, 24 + Math.Sin(a) * r1, 24 + Math.Cos(a) * r2, 24 + Math.Sin(a) * r2);
            }
            d.Pop();
        }
        double glint = Pulse(t, 1.5, .5);
        if (glint > 0)
        {
            d.PushOpacity(glint); d.PushTransform(new RotateTransform(45 * glint, 33, 13));
            Line(d, Stroke(Ink, 2), 33, 7, 33, 19); Line(d, Stroke(Ink, 2), 27, 13, 39, 13); d.Pop(); d.Pop();
        }
    }

    // The info ring pops, its dot drops and bounces, the stem grows, and lines of detail slide out beside it.
    static void About(DrawingContext d, double t)
    {
        double fade = Fade(t), ring = Back(Step(t, 0, .45)), drop = Step(t, .35, .5), stem = Out(Step(t, .7, .35));
        double slide = Ease(Step(t, 1.4, .6)), shift = -7 * slide;
        d.PushOpacity(fade); d.PushTransform(new TranslateTransform(shift, -3 * slide));
        Scaled(d, ring * (1 - .12 * slide), 24, 24, () => d.DrawEllipse(null, Stroke(Blue, 3), P(24, 24), 16, 16));
        double dotY = 14.5 - (1 - Spring(drop)) * 11; d.PushOpacity(Math.Min(1, drop * 3)); d.DrawEllipse(Ink, null, P(24, dotY), 2.4, 2.4); d.Pop();
        if (stem > 0) d.DrawRoundedRectangle(Ink, null, new Rect(22.2, 20.5, 3.6, 12 * stem), 1.6, 1.6);
        d.Pop(); d.Pop();
        for (int i = 0; i < 3; i++)
        {
            double p = Out(Step(t, 1.55 + i * .22, .4)); if (p <= 0) continue;
            d.PushOpacity(fade); Line(d, i == 0 ? BlueLine : SoftLine, 34, 15 + i * 9, 34 + 10 * p * (i == 1 ? .8 : 1), 15 + i * 9); d.Pop();
        }
    }
}
