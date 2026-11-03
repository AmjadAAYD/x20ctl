using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace X20Ctl.Product;

/// <summary>
/// A controller card changing colour. The same flowing change as <see cref="FinishMorph"/> (the new colour spreading from
/// the grips in soft lobes, with a thin line on its edge), but worked out on the processor into one small picture per
/// card instead of layered on the graphics card: the old picture, the new one and two masks over 1536x1024 art, seven times
/// over, made the change stutter (owner, 24 Oct 2026). Here each card is a single 576x384 picture of the controller's
/// framed view, and a frame is only a lookup and a blend per pixel of the shell, so it stays smooth, and a colour that is
/// cut a little differently (the D10's bumpers) simply fades out as the new one fades in.
/// The edge takes the new colour's own tone (owner, 25 Oct 2026): a dark wave when a pad turns black, a white one when it
/// turns white, a tinted one for the rest.
/// Everything a change needs is made ahead of time, in small steps while the screen is idle (<see cref="PrepareStep"/>), so
/// the first change starts without a stall.
/// </summary>
public sealed class FinishBlend : Image
{
    private const int PW = 576, PH = 384;
    private const double Aspect = 1.5, Room = 1.05, Milliseconds = 1800;
    private static readonly byte[] LineBytes = Enumerable.Range(0, FinishMorph.Steps).Select(k => (byte)(255 * FinishMorph.LineTable[k])).ToArray();

    private sealed class Prepared
    {
        public Rect Crop;
        public readonly Dictionary<string, uint[]> Pixels = new();
        public readonly Dictionary<string, (uint Edge, double Strength)> Edges = new();
        public float[]? Field;
        public int[] Inside = [];
    }
    private static readonly Dictionary<string, Prepared> prepared = new();
    private static Prepared Of(string model)
    {
        if (prepared.TryGetValue(model, out var known)) return known;
        Rect union = Rect.Empty;
        foreach (var f in PadColours.Catalog.For(model))
            if (ControllerOutline.For(model, false, f.Id)?.Bounds is { IsEmpty: false } b) union.Union(b);
        if (union.IsEmpty) union = new Rect(0, 0, 1536, 1024);
        double width = Math.Max(union.Width, union.Height * Aspect) * Room, height = width / Aspect;
        return prepared[model] = new Prepared { Crop = new Rect(union.X + union.Width / 2 - width / 2, union.Y + union.Height / 2 - height / 2, width, height) };
    }

    private static uint[] Pixels(string model, string finish)
    {
        var p = Of(model);
        if (p.Pixels.TryGetValue(finish, out var known)) return known;
        var visual = new DrawingVisual(); RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.HighQuality);
        using (var dc = visual.RenderOpen())
        {
            dc.PushTransform(new ScaleTransform(PW / p.Crop.Width, PH / p.Crop.Height)); dc.PushTransform(new TranslateTransform(-p.Crop.X, -p.Crop.Y));
            // the traced outline trims the cut-out's pale fringe, as it does on every other view of the pad
            if (ControllerOutline.For(model, false, finish) is Geometry clip) dc.PushClip(clip);
            dc.DrawImage(ControllerArt.Front(model, finish), new Rect(0, 0, 1536, 1024));
        }
        var target = new RenderTargetBitmap(PW, PH, 96, 96, PixelFormats.Pbgra32); target.Render(visual);
        var pixels = new uint[PW * PH]; target.CopyPixels(pixels, PW * 4, 0);
        p.Edges[finish] = EdgeOf(pixels);
        return p.Pixels[finish] = pixels;
    }

    /// <summary>The tone of the wave that brings a colour in: its shell's average, pulled toward black for a dark colour (a
    /// shadow wave, stronger so it shows on a light pad) and toward white for a light one.</summary>
    internal static (uint Edge, double Strength) EdgeOf(uint[] pixels)
    {
        double r = 0, g = 0, b = 0; int count = 0;
        for (int i = 0; i < pixels.Length; i += 3)
        {
            uint px = pixels[i]; uint a = px >> 24; if (a < 200) continue;
            b += (px & 255) * 255.0 / a; g += (px >> 8 & 255) * 255.0 / a; r += (px >> 16 & 255) * 255.0 / a; count++;
        }
        if (count == 0) return (0xFFD6ECFFu, .55);
        r /= count; g /= count; b /= count;
        double lum = (.299 * r + .587 * g + .114 * b) / 255;
        double toward = lum < .3 ? 0 : 255, mix = lum < .3 ? .45 : .55;
        static uint Channel(double c, double to, double m) => (uint)Math.Clamp(Math.Round(c + (to - c) * m), 0, 255);
        return (0xFF000000u | Channel(r, toward, mix) << 16 | Channel(g, toward, mix) << 8 | Channel(b, toward, mix), lum < .3 ? .95 : .55);
    }

    /// <summary>The spread over every colour's shell together, at half size and then smoothed up to the picture's size.</summary>
    private static void BuildField(string model)
    {
        var p = Of(model); const int LW = PW / 2, LH = PH / 2;
        foreach (var f in PadColours.Catalog.For(model)) Pixels(model, f.Id);
        var body = new float[LW * LH]; var inside = new List<int>();
        for (int i = 0; i < PW * PH; i++)
        {
            bool any = false; foreach (var pixels in p.Pixels.Values) if (pixels[i] >> 24 != 0) { any = true; break; }
            if (!any) continue; inside.Add(i);
            body[i / PW / 2 * LW + i % PW / 2] = 1;
        }
        var low = FinishMorph.Spread(body, LW, LH, model.Aggregate(17, (h, c) => h * 31 + c));
        var field = new float[PW * PH];
        for (int y = 0; y < PH; y++)
        {
            float fy = y * (LH - 1f) / (PH - 1); int y0 = (int)fy, y1 = Math.Min(y0 + 1, LH - 1); float ty = fy - y0;
            for (int x = 0; x < PW; x++)
            {
                float fx = x * (LW - 1f) / (PW - 1); int x0 = (int)fx, x1 = Math.Min(x0 + 1, LW - 1); float tx = fx - x0;
                float top = low[y0 * LW + x0] + (low[y0 * LW + x1] - low[y0 * LW + x0]) * tx, bottom = low[y1 * LW + x0] + (low[y1 * LW + x1] - low[y1 * LW + x0]) * tx;
                field[y * PW + x] = top + (bottom - top) * ty;
            }
        }
        p.Inside = inside.ToArray(); p.Field = field;
    }

    /// <summary>One small step of getting a model ready: a colour's picture, or the spread. False once all is ready.</summary>
    public static bool PrepareStep(string model)
    {
        var p = Of(model);
        foreach (var f in PadColours.Catalog.For(model)) if (!p.Pixels.ContainsKey(f.Id)) { Pixels(model, f.Id); return true; }
        if (p.Field == null) { BuildField(model); return true; }
        return false;
    }
    private static void Ready(string model) { while (PrepareStep(model)) { } }

    // ----- the card -----
    private readonly WriteableBitmap bitmap = new(PW, PH, 96, 96, PixelFormats.Pbgra32, null);
    private readonly uint[] frame = new uint[PW * PH];
    private uint[] from = [], to = [];
    private uint edge = 0xFFD6ECFFu; private double strength = .55;
    private DateTime began; private double progress;
    private TaskCompletionSource? done;
    public string ModelId { get; }
    /// <summary>The colour shown, or the one being turned into.</summary>
    public string Finish { get; private set; }
    public bool IsChanging => done != null;

    public FinishBlend(string model, string finish)
    {
        ModelId = model; Finish = finish;
        var crop = Of(model).Crop; Width = crop.Width; Height = crop.Height; Stretch = Stretch.Fill; IsHitTestVisible = false;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
        Array.Copy(Pixels(model, finish), frame, frame.Length); Commit(); Source = bitmap;
        Unloaded += (_, _) => Complete();
    }
    private void Commit() => bitmap.WritePixels(new Int32Rect(0, 0, PW, PH), frame, PW * 4, 0);

    /// <summary>Shows a colour at once.</summary>
    public void Show(string finish) { Complete(); Finish = finish; Array.Copy(Pixels(ModelId, finish), frame, frame.Length); Commit(); }

    private void Aim(string target)
    {
        from = Pixels(ModelId, Finish); to = Pixels(ModelId, target);
        (edge, strength) = Of(ModelId).Edges[target];
    }

    /// <summary>Turns into this colour; with motion off it simply swaps.</summary>
    public Task ChangeTo(string finish, bool animate = true)
    {
        Complete();
        if (finish == Finish) return Task.CompletedTask;
        if (!animate || !DS.Motion) { Show(finish); return Task.CompletedTask; }
        Ready(ModelId); Aim(finish); Finish = finish;
        began = DateTime.UtcNow; progress = 0; done = new TaskCompletionSource();
        if (active.Count == 0) CompositionTarget.Rendering += Tick;
        active.Add(this);
        return done.Task;
    }

    private void Complete()
    {
        if (done == null) return;
        Array.Copy(to, frame, frame.Length); Commit();
        active.Remove(this); if (active.Count == 0) CompositionTarget.Rendering -= Tick;
        var finished = done; done = null; finished.TrySetResult();
    }

    private static readonly List<FinishBlend> active = new();
    private static void Tick(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow; var cards = active.ToArray();
        foreach (var c in cards) c.progress = Math.Clamp((now - c.began).TotalMilliseconds / Milliseconds, 0, 1);
        // the cards are independent pictures, so they are worked out side by side
        Parallel.ForEach(cards, c => c.Compose(c.progress < .5 ? 2 * c.progress * c.progress : 1 - Math.Pow(-2 * c.progress + 2, 2) / 2));
        foreach (var c in cards) { c.Commit(); if (c.progress >= 1) c.Complete(); }
    }

    /// <summary>The new colour where the edge has passed, feathered, with a thin line in the colour's tone on the edge itself
    /// that is strongest mid-change and gone at both ends. Pixels the edge has not reached, or has fully passed, are copied.</summary>
    private void Compose(double eased)
    {
        var p = Of(ModelId); var field = p.Field!; var inside = p.Inside; var reveal = FinishMorph.RevealTable;
        double front = eased * (1 + 2 * FinishMorph.Feather) - FinishMorph.Feather;
        int glow = (int)(Math.Sin(Math.PI * eased) * strength * 256);
        float scale = (FinishMorph.Steps - 1) / 4f, offset = (float)((front + 2) * scale);
        uint[] o = from, n = to, d = frame; uint tone = edge;
        for (int j = 0; j < inside.Length; j++)
        {
            int i = inside[j], k = (int)(offset - field[i] * scale), a = reveal[k]; uint r;
            if (a == 0) r = o[i];
            else if (a == 255) r = n[i];
            else
            {
                r = Mix(o[i], n[i], a + (a >> 7));
                int g = LineBytes[k] * glow >> 8;
                if (g > 0) { int lit = (g * (int)(r >> 24) + 127) / 255; r = Mix(r, tone, lit + (lit >> 7)); }
            }
            d[i] = r;
        }
    }
    /// <summary>Two packed colours blended by a / 256, two channels at a time.</summary>
    private static uint Mix(uint o, uint n, int a)
    {
        uint keep = (uint)(256 - a), take = (uint)a;
        uint rb = (((o & 0x00FF00FFu) * keep + (n & 0x00FF00FFu) * take) >> 8) & 0x00FF00FFu;
        uint ag = ((o >> 8 & 0x00FF00FFu) * keep + (n >> 8 & 0x00FF00FFu) * take) & 0xFF00FF00u;
        return rb | ag;
    }

    /// <summary>Review hook: the change frozen at a given progress, 0..1, without changing the colour held.</summary>
    internal void Preview(string finish, double progress)
    {
        Complete(); Ready(ModelId); Aim(finish);
        Compose(progress); Commit();
    }
    /// <summary>Review hook: how long one frame of the change takes to work out.</summary>
    internal static double PaintCost(FinishBlend card, int frames)
    {
        var other = PadColours.Catalog.For(card.ModelId).First(f => f.Id != card.Finish).Id;
        Ready(card.ModelId); card.Aim(other);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < frames; i++) card.Compose(i / (double)frames);
        double cost = watch.Elapsed.TotalMilliseconds / frames;
        Array.Copy(card.from, card.frame, card.frame.Length); card.Commit(); return cost;
    }
}
