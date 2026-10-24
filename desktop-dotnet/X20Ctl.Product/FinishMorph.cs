using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace X20Ctl.Product;

/// <summary>
/// A controller changing colour, Venom styled but clean and modern (owner direction 24 Oct 2026; a literal dark film
/// with jagged tendrils was rejected the same day): the new colour flows over the shell from where the hands hold the
/// grips, in a few smooth lobes with a soft edge, and a thin light line rides the edge. It sits over the picture it
/// changes, in the same 1536x1024 art space; when it ends the picture itself shows the new colour and this layer is empty.
///
/// The spread follows a field worked out once per model on a quarter-size grid: the travel time from both grips through
/// the silhouette, where each pixel costs a little more or less by one broad, smooth noise, so the edge flows in gentle
/// lobes instead of a plain circle. Frames run only while a change is under way.
/// </summary>
public sealed class FinishMorph : Grid
{
    private const int FW = 384, FH = 256;
    private const double Feather = .035, Milliseconds = 1800;
    private static readonly Dictionary<string, (float[] Field, float[] Body)> fields = new();
    private readonly Image picture;
    private readonly Image incoming = Layer(), film = Layer();
    private readonly WriteableBitmap mask = new(FW, FH, 96, 96, PixelFormats.Pbgra32, null), gloss = new(FW, FH, 96, 96, PixelFormats.Pbgra32, null), keep = new(FW, FH, 96, 96, PixelFormats.Pbgra32, null);
    private readonly byte[] maskPixels = new byte[FW * FH * 4], glossPixels = new byte[FW * FH * 4], keepPixels = new byte[FW * FH * 4];
    private float[] field = [], body = [];
    private void Load(string model)
    {
        (field, body) = Field(model);
        inside = Enumerable.Range(0, body.Length).Where(i => body[i] > 0).ToArray();
        // pixels off the shell stay clear for good
        Array.Clear(maskPixels); Array.Clear(glossPixels); Array.Fill(keepPixels, (byte)255);
        // the picture being replaced fades out under the new colour, so a part of it that the new colour does not have (a
        // bumper cut differently) is gone once the edge has passed instead of lingering until the swap
        picture.OpacityMask = new ImageBrush(keep);
    }
    private DateTime began, painted;
    private Action? ending;
    public bool IsChanging => ending != null;
    public string ModelId { get; private set; } = "";
    public string Finish { get; private set; } = X20Ctl.Zone.FinishCatalog.Original;

    /// <param name="picture">the picture in the current colour; it receives the new colour when the change ends</param>
    public FinishMorph(Image picture)
    {
        this.picture = picture; Width = 1536; Height = 1024; IsHitTestVisible = false; Visibility = Visibility.Collapsed;
        incoming.OpacityMask = new ImageBrush(mask); film.Source = gloss;
        Children.Add(incoming); Children.Add(film);
        Unloaded += (_, _) => Complete();
    }

    /// <summary>Shows a colour at once (no change), e.g. when a pad is assigned.</summary>
    public void Show(string model, string finish)
    {
        Complete(); ModelId = model; Finish = finish; picture.Source = ControllerArt.Front(model, finish);
        ControllerOutline.Trim(picture, model, false, finish);
    }

    /// <summary>Turns the picture into this colour; with motion off it simply swaps.</summary>
    public Task ChangeTo(string finish, bool animate = true)
    {
        Complete();
        if (ModelId == "" || finish == Finish) return Task.CompletedTask;
        var target = ControllerArt.Front(ModelId, finish); Finish = finish;
        if (!animate || !DS.Motion) { picture.Source = target; ControllerOutline.Trim(picture, ModelId, false, finish); return Task.CompletedTask; }
        Load(ModelId);
        incoming.Source = target; ControllerOutline.Trim(incoming, ModelId, false, finish); Paint(0); Visibility = Visibility.Visible;
        var done = new TaskCompletionSource();
        began = DateTime.UtcNow;
        string model = ModelId;
        ending = () => { picture.OpacityMask = null; picture.Source = target; ControllerOutline.Trim(picture, model, false, finish); incoming.Source = null; Visibility = Visibility.Collapsed; done.TrySetResult(); };
        CompositionTarget.Rendering += Frame;
        return done.Task;
    }

    /// <summary>Review hook: the change frozen at a given progress, 0..1.</summary>
    /// <summary>Review hook: how long one frame of the change takes to work out.</summary>
    internal static double PaintCost(FinishMorph morph, int frames)
    {
        morph.Load(morph.ModelId);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < frames; i++) morph.Paint(i / (double)frames);
        return watch.Elapsed.TotalMilliseconds / frames;
    }

    internal void Preview(string finish, double progress)
    {
        Complete(); Load(ModelId); incoming.Source = ControllerArt.Front(ModelId, finish); ControllerOutline.Trim(incoming, ModelId, false, finish); Visibility = Visibility.Visible; Paint(progress);
    }

    private void Frame(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;
        double p = Math.Clamp((now - began).TotalMilliseconds / Milliseconds, 0, 1);
        // the edge moves slowly, so about 40 frames a second is smooth and leaves room for every card changing at once
        if (p < 1 && (now - painted).TotalMilliseconds < 24) return;
        painted = now;
        // eases in and out, unhurried
        Paint(p < .5 ? 2 * p * p : 1 - Math.Pow(-2 * p + 2, 2) / 2);
        if (p >= 1) Complete();
    }

    private void Complete()
    {
        CompositionTarget.Rendering -= Frame;
        var end = ending; ending = null; end?.Invoke();
    }

    /// <summary>The new colour where the edge has passed, feathered; a thin cool-white line on the edge itself, strongest
    /// mid-change and gone at both ends so nothing pops. Both curves are read from small tables, so a frame is only
    /// lookups and multiplies (owner, 24 Oct 2026: several cards changing at once must not lag).</summary>
    private const int Steps = 2048;
    private static readonly byte[] RevealTable = new byte[Steps];
    private static readonly float[] LineTable = new float[Steps];
    static FinishMorph()
    {
        // index i covers "how far the edge has passed this pixel" from -2 to +2 in field units
        for (int i = 0; i < Steps; i++)
        {
            double behind = i * 4.0 / (Steps - 1) - 2;
            double t = Math.Clamp(behind / Feather * .5 + .5, 0, 1); t = t * t * (3 - 2 * t);
            RevealTable[i] = (byte)(t * 255); RevealPacked[i] = RevealTable[i] * 0x01010101u;
            // the old picture stays whole until the new colour has nearly covered a pixel, then goes
            KeepPacked[i] = (uint)Math.Round(255 * (1 - Math.Clamp((RevealTable[i] / 255.0 - .75) / .25, 0, 1))) * 0x01010101u;
            LineTable[i] = (float)Math.Exp(-Math.Pow(behind / (Feather * .45), 2));
        }
    }
    private void Paint(double progress)
    {
        double front = progress * (1 + 2 * Feather) - Feather;
        float glow = (float)(Math.Sin(Math.PI * progress) * .55);
        // this frame's light line, packed once per table step; only the soft rim (body under 1) works a pixel out alone
        for (int k = 0; k < Steps; k++) lineNow[k] = Pack(LineTable[k] * glow);
        float scale = (Steps - 1) / 4f, offset = (float)((front + 2) * scale);
        var reveal = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, uint>(maskPixels.AsSpan());
        var line = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, uint>(glossPixels.AsSpan());
        var old = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, uint>(keepPixels.AsSpan());
        foreach (int i in inside)
        {
            int k = (int)(offset - field[i] * scale); // field and front are both in 0..1, so k stays inside the table
            reveal[i] = RevealPacked[k]; old[i] = KeepPacked[k];
            float b = body[i];
            line[i] = b >= 1 ? lineNow[k] : Pack(LineTable[k] * glow * b);
        }
        mask.WritePixels(new Int32Rect(0, 0, FW, FH), maskPixels, FW * 4, 0);
        gloss.WritePixels(new Int32Rect(0, 0, FW, FH), glossPixels, FW * 4, 0);
        keep.WritePixels(new Int32Rect(0, 0, FW, FH), keepPixels, FW * 4, 0);
    }
    private readonly uint[] lineNow = new uint[Steps];
    private int[] inside = [];
    private static readonly uint[] RevealPacked = new uint[Steps], KeepPacked = new uint[Steps];
    /// <summary>A cool white at this strength, premultiplied BGRA.</summary>
    private static uint Pack(float a) => (uint)(255 * a) | (uint)(236 * a) << 8 | (uint)(214 * a) << 16 | (uint)(255 * a) << 24;

    // ----- the spread field -----
    private static (float[], float[]) Field(string model)
    {
        if (fields.TryGetValue(model, out var cached)) return cached;
        // the body is every colour's silhouette together: a colour cut a little differently (a bumper, a grip rim) must still
        // be reached, or the old colour would stay there until the end
        var body = new float[FW * FH];
        foreach (var finish in PadColours.Catalog.For(model))
        {
            var small = new FormatConvertedBitmap(new TransformedBitmap(ControllerArt.Front(model, finish.Id), new ScaleTransform(FW / 1536.0, FH / 1024.0)), PixelFormats.Pbgra32, null, 0);
            var pixels = new byte[FW * FH * 4]; small.CopyPixels(pixels, FW * 4, 0);
            for (int i = 0; i < body.Length; i++) body[i] = Math.Max(body[i], pixels[i * 4 + 3] / 255f);
        }
        int seed = model.Aggregate(17, (h, c) => h * 31 + c);
        var cost = new float[FW * FH];
        for (int y = 0; y < FH; y++) for (int x = 0; x < FW; x++)
        {
            double n = Noise(x / 46.0, y / 46.0, seed);
            cost[y * FW + x] = body[y * FW + x] > .5f ? (float)(1 + 1.1 * n) : 1f;
        }
        // it starts where the hands hold: the lowest point of each grip, the right one a moment later
        var dist = new float[FW * FH]; Array.Fill(dist, float.MaxValue);
        var queue = new PriorityQueue<int, float>();
        void Seed(int x0, int x1, float start)
        {
            for (int y = FH - 1; y >= 0; y--) for (int x = x0; x < x1; x++)
                if (body[y * FW + x] > .9f) { int k = y * FW + x; dist[k] = start; queue.Enqueue(k, start); return; }
        }
        Seed((int)(FW * .08), (int)(FW * .38), 0); Seed((int)(FW * .62), (int)(FW * .92), 14);
        while (queue.TryDequeue(out int k, out float d))
        {
            if (d > dist[k]) continue;
            int x = k % FW, y = k / FW;
            for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
            {
                if ((dx | dy) == 0) continue; int nx = x + dx, ny = y + dy; if (nx < 0 || ny < 0 || nx >= FW || ny >= FH) continue;
                int j = ny * FW + nx; float step = (dx != 0 && dy != 0 ? 1.4142f : 1f) * (cost[k] + cost[j]) / 2, next = d + step;
                if (next < dist[j]) { dist[j] = next; queue.Enqueue(j, next); }
            }
        }
        float max = 1; for (int i = 0; i < dist.Length; i++) if (body[i] > .5f && dist[i] < float.MaxValue) max = Math.Max(max, dist[i]);
        var result = new float[FW * FH]; for (int i = 0; i < dist.Length; i++) result[i] = Math.Min(1f, dist[i] / max);
        return fields[model] = (result, body);
    }
    private static double Noise(double x, double y, int seed)
    {
        int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y); double fx = x - x0, fy = y - y0;
        fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
        double a = Hash(x0, y0, seed), b = Hash(x0 + 1, y0, seed), c = Hash(x0, y0 + 1, seed), d = Hash(x0 + 1, y0 + 1, seed);
        return a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy;
    }
    private static double Hash(int x, int y, int seed) { unchecked { uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041); h = (h ^ (h >> 13)) * 1274126177; return ((h ^ (h >> 16)) & 0xFFFF) / 65535.0; } }
    private static Image Layer() { var image = new Image { Width = 1536, Height = 1024, Stretch = Stretch.Fill, IsHitTestVisible = false }; RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality); return image; }
}
