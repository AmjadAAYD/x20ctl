using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
namespace X20Ctl.Linux;

/// <summary>What the opening hands over to: the Controller Zone under it. The intro flies its logo onto the header's mark,
/// so its last frame and the Zone's first are the same picture (as on Windows, X20Ctl.Product/ZoneScene.Intro.cs).</summary>
internal interface IIntroHandoff
{
    /// <summary>The ink box of the header's logo, in <paramref name="to"/>'s coordinates; null when it isn't laid out.</summary>
    Rect? MarkTarget(Visual to);
    /// <summary>The Zone shows, with everything the intro still has to reveal hidden.</summary>
    void HandoffBegin();
    /// <summary>reveal: the interface around the logo (0..1); brand: the header's logo and name; pulse: the logo's one
    /// landing pulse (a scale, 1 at rest).</summary>
    void Handoff(double reveal, double brand, double pulse);
    /// <summary>Everything back to normal.</summary>
    void HandoffEnd();
}

/// <summary>The opening: the Windows app's film, picture and sound, then its logo flies onto the header's mark while the
/// Zone grows in around it, with the Windows timings and curve (X20Ctl.Product/ProductExperience.cs, IntroView). Esc, any
/// other key or a click skips it; M mutes it, and the choice is remembered as the intro's Sound setting.</summary>
internal sealed class IntroView : Panel
{
    const double CoverAt = 6.45, FlyAt = 6.6, FlyFor = .8, Total = FlyAt + FlyFor, OpenWithin = 2.5;
    // the logo's ink in the film's 1920x1080 frame, and inside Brand/x20ctl-mark.png (512 px): the same boxes as Windows
    static readonly Rect FilmLogo = new(622.42, 235.10, 674.90, 469.80), MarkInk = new(10, 85, 492, 342);
    static readonly Color Dark = Color.FromRgb(2, 4, 10);

    readonly Bitmap? mark; readonly IIntroHandoff? handoff; readonly Action done; readonly bool silent;
    readonly IntroFilm? film = IntroFilm.Open(); readonly IntroSound sound = new();
    readonly Pill skip, mute; readonly Surface picture;
    readonly DateTime created = DateTime.UtcNow;
    bool finished, began, muted, handing, targetsRead; DateTime beganAt; double? frozen; Rect? markTarget;
    // what Render draws, worked out each frame by Step
    double backdrop = 1, filmOpacity = 1, coverOpacity, coverSize; Matrix coverPlace = Matrix.Identity;

    public IntroView(Bitmap? mark, IIntroHandoff? handoff, Action done, bool silent = false)
    {
        this.mark = mark; this.handoff = handoff; this.done = done; this.silent = silent || ReviewMode.Headless;
        muted = !AppSettings.Shared.IntroSound; Focusable = true; ClipToBounds = true;
        skip = new Pill(Phone.Active ? "Skip" : "Skip · Esc", Pill.Kinds.Ghost, Finish) { Padding = new Thickness(20, 10), Opacity = .8 };
        mute = new Pill("", Pill.Kinds.Ghost, ToggleMute) { Padding = new Thickness(20, 10), Opacity = .8, IsVisible = film != null };
        ShowMute();
        Children.Add(picture = new Surface(this));
        Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 36, 36), Children = { mute, skip } });
        PointerPressed += (_, e) => { if (!e.Handled) Finish(); };
        KeyDown += (_, e) => { e.Handled = true; if (e.Key == Key.M) ToggleMute(); else Finish(); };
        AttachedToVisualTree += (_, _) => { Focus(); if (frozen == null) TopLevel.GetTopLevel(this)?.RequestAnimationFrame(Tick); };
        Step(0);
    }

    void ShowMute() => mute.Text = Phone.Active ? (muted ? "Unmute" : "Mute") : muted ? "Unmute · M" : "Mute · M";
    /// <summary>Mute stops the sound at once; unmuting brings it back where the picture is.</summary>
    public void ToggleMute()
    {
        if (film == null || finished) return;
        muted = !muted; ShowMute();
        if (muted) sound.Stop(); else if (began && !silent) sound.Start(Elapsed);
        if (!silent) { AppSettings.Shared.IntroSound = !muted; AppSettings.Shared.Save(); }
    }

    public void Finish()
    {
        if (finished) return; finished = true;
        sound.Stop(); film?.Dispose();
        handoff?.HandoffEnd(); done();
    }

    /// <summary>Review hook: hold the intro at <paramref name="t"/> seconds, and say when the film's frame for it is decoded.</summary>
    public bool Freeze(double t) { frozen = t; Step(t); return film == null || film.Holds(t); }

    double Elapsed => frozen ?? (began ? (DateTime.UtcNow - beganAt).TotalSeconds : 0);

    void Tick(TimeSpan _)
    {
        if (finished || frozen != null) return;
        if (!began)
        {
            // the clock starts with the film's first picture, and the sound with it
            if (film == null || film.Ready) { began = true; beganAt = DateTime.UtcNow; if (film != null && !muted && !silent) sound.Start(); }
            // a film that never shows must not hold the app behind a dark screen
            else if (film.Failed || (DateTime.UtcNow - created).TotalSeconds > OpenWithin) { Finish(); return; }
        }
        Step(Elapsed);
        if (!finished) TopLevel.GetTopLevel(this)?.RequestAnimationFrame(Tick);
    }

    void Step(double t)
    {
        if (finished) return;
        if (film == null && t < CoverAt) t = CoverAt; // no film: start at the logo and hand over at once
        // decode one frame ahead: the picture shown next frame is the one asked for now
        film?.Seek(frozen == null ? t + 1 / 60.0 : t);

        // the cover: the film's logo, laid exactly over it; then the film fades out from under it
        var logo = OnScreen(FilmLogo);
        coverSize = logo.Width * 512 / MarkInk.Width; double k = coverSize / 512;
        var at = new Point(logo.Center.X - MarkInk.Center.X * k, logo.Center.Y - MarkInk.Center.Y * k);
        filmOpacity = film == null ? 0 : 1 - Smooth(Win(t, CoverAt + .01, FlyAt));

        // the hand-over: the logo travels to the header's mark, shrinking as it goes, while the dark fades and the Zone
        // grows in around it; it lands on the header's mark exactly
        if (t >= FlyAt && !targetsRead) { targetsRead = true; markTarget = handoff?.MarkTarget(this); handoff?.HandoffBegin(); handing = handoff != null; }
        double q = CubicBezier(Win(t, FlyAt, FlyAt + FlyFor), .16, .84, .24, 1);
        coverPlace = Matrix.CreateTranslation(at.X, at.Y); if (markTarget is Rect target && q > 0) coverPlace *= Fly(logo, target, q);
        double brand = markTarget == null ? Smooth(Win(t, FlyAt, FlyAt + .5)) : Smooth(Win(q, .85, 1));
        coverOpacity = (t >= CoverAt ? 1 : 0) * (1 - brand);
        backdrop = 1 - Smooth(Win(t, FlyAt + .05, FlyAt + .7));
        double landed = Win(t, FlyAt + FlyFor, FlyAt + FlyFor + .14);
        if (handing) handoff!.Handoff(Win(t, FlyAt, FlyAt + FlyFor + .1), brand, landed > 0 && landed < 1 ? 1 + .03 * Math.Sin(Math.PI * landed) : 1);
        skip.Opacity = mute.Opacity = .8 * (1 - Smooth(Win(t, FlyAt, FlyAt + .3)));
        // nearly settled: the Zone already answers the mouse
        IsHitTestVisible = t < Total - .1;
        picture.InvalidateVisual();
        if (t >= Total + .15 && frozen == null) Finish();
    }

    /// <summary>The dark, the film and the logo, drawn under the buttons.</summary>
    sealed class Surface(IntroView owner) : Control
    {
        public override void Render(DrawingContext dc) => owner.Paint(dc);
    }
    void Paint(DrawingContext dc)
    {
        var bounds = new Rect(Bounds.Size); if (bounds.Width < 2 || finished) return;
        if (backdrop > 0) dc.FillRectangle(new SolidColorBrush(Dark, backdrop), bounds);
        if (filmOpacity > 0 && film?.Picture() is { } frame)
            using (dc.PushOpacity(filmOpacity)) dc.DrawImage(frame, new Rect(frame.Size), OnScreen(new Rect(0, 0, 1920, 1080)));
        if (mark != null && coverOpacity > 0)
            using (dc.PushTransform(coverPlace)) using (dc.PushOpacity(coverOpacity)) dc.DrawImage(mark, new Rect(0, 0, coverSize, coverSize));
    }

    /// <summary>A box in the film's 1920x1080 frame, where it lands on this view (the film fills it, cropping the edges).</summary>
    Rect OnScreen(Rect r)
    {
        double w = Bounds.Width > 0 ? Bounds.Width : 1920, h = Bounds.Height > 0 ? Bounds.Height : 1080, k = Math.Max(w / 1920, h / 1080);
        double ox = (w - 1920 * k) / 2, oy = (h - 1080 * k) / 2; return new(ox + r.X * k, oy + r.Y * k, r.Width * k, r.Height * k);
    }
    /// <summary>From the middle to the header: position and size together, along a curve that runs left before it rises
    /// into the tile from below, so it never crosses the name beside it.</summary>
    static Matrix Fly(Rect from, Rect to, double q)
    {
        double scale = Math.Exp(Math.Log(Math.Min(to.Width / from.Width, to.Height / from.Height)) * q);
        Point a = from.Center, b = to.Center, c = new(a.X + (b.X - a.X) * .9, a.Y + (b.Y - a.Y) * .2);
        double u = 1 - q; var at = new Point(u * u * a.X + 2 * u * q * c.X + q * q * b.X, u * u * a.Y + 2 * u * q * c.Y + q * q * b.Y);
        return Matrix.CreateTranslation(-a.X, -a.Y) * Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(at.X, at.Y);
    }
    static double Win(double t, double a, double b) => Math.Clamp((t - a) / (b - a), 0, 1);
    static double Smooth(double x) { x = Math.Clamp(x, 0, 1); return x * x * (3 - 2 * x); }
    /// <summary>CSS-style cubic-bezier(x1, y1, x2, y2) easing.</summary>
    static double CubicBezier(double x, double x1, double y1, double x2, double y2)
    {
        if (x <= 0) return 0; if (x >= 1) return 1; double u = x;
        for (int i = 0; i < 8; i++) { double cx = Bz(u, x1, x2) - x, d = 3 * (1 - u) * (1 - u) * x1 + 6 * (1 - u) * u * (x2 - x1) + 3 * u * u * (1 - x2); if (Math.Abs(d) < 1e-6) break; u = Math.Clamp(u - cx / d, 0, 1); }
        return Bz(u, y1, y2);
        static double Bz(double t, double a, double b) => 3 * (1 - t) * (1 - t) * t * a + 3 * (1 - t) * t * t * b + t * t * t;
    }
}
