using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace X20Ctl.Product;

/// <summary>Small vector stories for Tools cards. Only the hovered/focused, visible card owns a frame callback.</summary>
internal sealed class ToolTileIcon : FrameworkElement
{
    readonly string title;
    readonly Stopwatch clock = new();
    bool active;
    double time;
    public bool IsAnimating { get; private set; }
    static readonly Brush Ink = Brushes.White, Gold = new SolidColorBrush(Color.FromRgb(255, 218, 108));
    static readonly Pen Edge = new(Ink, 1.8) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
    static readonly Geometry Star = Geometry.Parse("M24,8 L28.6,18.2 L40,19.5 L31.5,27.1 L33.8,38 L24,32.4 L14.2,38 L16.5,27.1 L8,19.5 L19.4,18.2 Z");
    static readonly Geometry Heart = Geometry.Parse("M24,37 C20,32 8,25 8,17 C8,7 20,7 24,15 C28,7 40,7 40,17 C40,25 28,32 24,37 Z");

    public ToolTileIcon(string title)
    {
        this.title = title; Width = Height = 36; IsHitTestVisible = false;
        Loaded += (_, _) => UpdateClock(); Unloaded += (_, _) => StopClock();
        IsVisibleChanged += (_, _) => UpdateClock();
    }
    public void SetActive(bool value) { if (active == value) return; active = value; time = 0; UpdateClock(); InvalidateVisual(); }
    public void Seek(double seconds) { StopClock(); time = seconds; InvalidateVisual(); }
    void UpdateClock()
    {
        if (!active || !IsVisible || !IsLoaded || !DS.Motion) { StopClock(); time = 0; InvalidateVisual(); return; }
        if (IsAnimating) return;
        clock.Restart(); IsAnimating = true; CompositionTarget.Rendering += Frame;
    }
    void StopClock() { CompositionTarget.Rendering -= Frame; clock.Stop(); IsAnimating = false; }
    void Frame(object? sender, EventArgs e)
    {
        if (!DS.Motion || !IsVisible || !IsLoaded) { StopClock(); time = 0; }
        else time = clock.Elapsed.TotalSeconds;
        InvalidateVisual();
    }
    static double Step(double t, double start, double duration) => Math.Clamp((t - start) / duration, 0, 1);
    static double Ease(double p) => p * p * (3 - 2 * p);
    static Point P(double x, double y) => new(x, y);
    static void Line(DrawingContext d, double x, double y, double x2, double y2) => d.DrawLine(Edge, P(x, y), P(x2, y2));
    static Geometry Shape(string path) => Geometry.Parse(path);
    static void Glyph(DrawingContext d, string glyph, double x = 24, double y = 24, double size = 27)
    {
        var text = new FormattedText(glyph, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), size, Ink, 1);
        d.DrawText(text, P(x - text.Width / 2, y - text.Height / 2));
    }
    static void Check(DrawingContext d, double x, double y) { Line(d, x - 4, y, x - 1, y + 3); Line(d, x - 1, y + 3, x + 5, y - 4); }

    protected override void OnRender(DrawingContext d)
    {
        base.OnRender(d); double t = time % 6.6;
        d.PushTransform(new ScaleTransform(ActualWidth / 48, ActualHeight / 48));
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
        d.Pop();
    }

    static void Repair(DrawingContext d, double t)
    {
        // Seat the wrench, make two tightening turns, then show a fixed check.
        double turn = t < .6 ? -18 * Ease(Step(t, 0, .6)) : t < 3.5 ? -18 + 22 * Math.Sin((t - .6) * Math.PI * 2 / 1.2) : 0;
        d.PushTransform(new RotateTransform(turn, 30, 16));
        Glyph(d, "\uE90F", 23, 24, 32); d.Pop();
        double done = Step(t, 3.5, .4) * (1 - Step(t, 5.7, .6));
        d.PushOpacity(done); d.DrawEllipse(new SolidColorBrush(Color.FromRgb(28, 116, 85)), null, P(35, 35), 8, 8); Check(d, 35, 35); d.Pop();
    }
    static void Feedback(DrawingContext d, double t)
    {
        double send = Ease(Step(t, 2.8, 1.6)), reset = 1 - Step(t, 5.8, .8);
        // Recipient waits on the right; the written message travels into their inbox.
        d.PushOpacity(.35 + .65 * send); d.DrawEllipse(Ink, null, P(39, 30), 3, 3); d.DrawRoundedRectangle(Ink, null, new Rect(33, 35, 12, 7), 3, 3); d.Pop();
        d.PushOpacity(reset * (1 - Step(t, 4.2, .4)));
        d.PushTransform(new TranslateTransform(send * 17, send * 14));
        d.PushTransform(new ScaleTransform(1 - .65 * send, 1 - .65 * send, 21, 21));
        d.DrawGeometry(null, Edge, Shape("M8,8 H34 Q38,8 38,12 V28 Q38,32 34,32 H18 L11,38 V32 H8 Q4,32 4,28 V12 Q4,8 8,8 Z"));
        for (int i = 0; i < 3; i++) Line(d, 10, 15 + i * 6, 10 + (i == 2 ? 12 : 21) * Step(t, .3 + i * .55, .55), 15 + i * 6);
        d.Pop(); d.Pop(); d.Pop();
        d.PushOpacity(Step(t, 4.3, .3) * reset); Check(d, 24, 21); d.Pop();
    }
    static void Replay(DrawingContext d, double t)
    {
        double play = Ease(Step(t, 1.4, .7)) * (1 - Ease(Step(t, 5.5, .7)));
        d.PushOpacity(1 - play);
        d.DrawRoundedRectangle(Ink, null, new Rect(15 + play * 5, 11, 6 * (1 - .5 * play), 26), 1, 1);
        d.DrawRoundedRectangle(Ink, null, new Rect(27 - play * 5, 11, 6 * (1 - .5 * play), 26), 1, 1); d.Pop();
        d.PushOpacity(play); d.PushTransform(new ScaleTransform(.6 + .4 * play, .6 + .4 * play, 24, 24));
        d.DrawGeometry(null, Edge, Shape("M16,10 L37,24 L16,38 Z")); d.Pop();
        double p = Step(t, 2.4, 2.8); d.PushOpacity(Math.Sin(p * Math.PI) * .75);
        d.DrawEllipse(null, Edge, P(24, 24), 18 + p * 3, 18 + p * 3); d.Pop(); d.Pop();
    }
    static void Link(DrawingContext d, double t, bool pc)
    {
        // Bluetooth → phone → controller, or controller → PC → the paired devices.
        string[] icons = pc ? ["\uE7FC", "\uE7F4", "\uE7FC"] : ["\uE702", "\uE8EA", "\uE7FC"];
        int stage = Math.Min(2, (int)(t / 2.2)); double local = t - stage * 2.2;
        double transition = Ease(Step(local, 1.65, .55));
        void Device(int index, double x, double opacity)
        {
            d.PushOpacity(opacity); d.PushTransform(new TranslateTransform(x, 0));
            if (pc && index == 2) { Glyph(d, icons[index], 15, 26, 24); Glyph(d, "\uE7F4", 34, 23, 23); Check(d, 33, 38); }
            else Glyph(d, icons[index], 24, 23, 31);
            d.Pop(); d.Pop();
        }
        Device(stage, -transition * 13, 1 - transition);
        Device((stage + 1) % 3, (1 - transition) * 13, transition);
        double packet = Step(local, .6, 1.1);
        d.PushOpacity(Math.Sin(packet * Math.PI));
        Line(d, 8, 41, 40, 41); d.DrawEllipse(Ink, null, P(8 + 32 * packet, 41), 2.5, 2.5); d.Pop();
    }
    static void Heartbeat(DrawingContext d, double t)
    {
        double phase = t % 2.4;
        double beat = Math.Exp(-Math.Pow((phase - .22) / .12, 2)) + .65 * Math.Exp(-Math.Pow((phase - .55) / .14, 2));
        double scale = 1 + .16 * beat;
        d.PushTransform(new ScaleTransform(scale, scale, 24, 24)); d.DrawGeometry(Ink, null, Heart); d.Pop();
        double ring = Step(phase, .6, .9); d.PushOpacity(Math.Sin(ring * Math.PI) * .4);
        d.PushTransform(new ScaleTransform(1 + ring * .4, 1 + ring * .4, 24, 24)); d.DrawGeometry(null, Edge, Heart); d.Pop(); d.Pop();
    }
    static void Shine(DrawingContext d, double t)
    {
        double shine = Math.Sin(Step(t, .25, 1.8) * Math.PI);
        double fill = Ease(Step(t, 2.2, 1.0)) * (1 - Ease(Step(t, 5.5, .8)));
        d.DrawGeometry(null, Edge, Star);
        d.PushClip(Star); d.DrawRectangle(Gold, null, new Rect(6, 40 - fill * 34, 36, fill * 34)); d.Pop();
        d.PushOpacity(shine);
        for (int i = 0; i < 8; i++)
        {
            double a = i * Math.PI / 4, inner = 18 + shine * 2, outer = 20 + shine * 4;
            d.DrawLine(new Pen(Gold, 1.5), P(24 + Math.Cos(a) * inner, 24 + Math.Sin(a) * inner), P(24 + Math.Cos(a) * outer, 24 + Math.Sin(a) * outer));
        }
        d.Pop();
        d.PushOpacity(fill * .7); d.DrawEllipse(Brushes.White, null, P(20 + Math.Sin(t * 2) * 3, 17), 1.5, 1.5); d.Pop();
    }
    static void About(DrawingContext d, double t)
    {
        double reveal = Ease(Step(t, .5, 1.2)) * (1 - Ease(Step(t, 5.4, .8)));
        d.PushTransform(new ScaleTransform(1 - .25 * reveal, 1 - .25 * reveal, 24, 24));
        d.PushTransform(new TranslateTransform(-9 * reveal, -6 * reveal)); Glyph(d, "\uE946", 24, 24, 31); d.Pop(); d.Pop();
        d.PushOpacity(reveal);
        for (int i = 0; i < 3; i++) Line(d, 17, 29 + i * 5, 17 + 24 * Step(t, 1.6 + i * .4, .4), 29 + i * 5);
        d.Pop();
    }
}
