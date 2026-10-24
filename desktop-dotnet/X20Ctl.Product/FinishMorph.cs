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
    private readonly WriteableBitmap mask = new(FW, FH, 96, 96, PixelFormats.Pbgra32, null), gloss = new(FW, FH, 96, 96, PixelFormats.Pbgra32, null);
    private readonly byte[] maskPixels = new byte[FW * FH * 4], glossPixels = new byte[FW * FH * 4];
    private float[] field = [], body = [];
    private DateTime began;
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
        (field, body) = Field(ModelId);
        incoming.Source = target; ControllerOutline.Trim(incoming, ModelId, false, finish); Paint(0); Visibility = Visibility.Visible;
        var done = new TaskCompletionSource();
        began = DateTime.UtcNow;
        string model = ModelId;
        ending = () => { picture.Source = target; ControllerOutline.Trim(picture, model, false, finish); incoming.Source = null; Visibility = Visibility.Collapsed; done.TrySetResult(); };
        CompositionTarget.Rendering += Frame;
        return done.Task;
    }

    /// <summary>Review hook: the change frozen at a given progress, 0..1.</summary>
    internal void Preview(string finish, double progress)
    {
        Complete(); (field, body) = Field(ModelId); incoming.Source = ControllerArt.Front(ModelId, finish); ControllerOutline.Trim(incoming, ModelId, false, finish); Visibility = Visibility.Visible; Paint(progress);
    }

    private void Frame(object? sender, EventArgs e)
    {
        double p = Math.Clamp((DateTime.UtcNow - began).TotalMilliseconds / Milliseconds, 0, 1);
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
    /// mid-change and gone at both ends so nothing pops.</summary>
    private void Paint(double progress)
    {
        double front = progress * (1 + 2 * Feather) - Feather, glow = Math.Sin(Math.PI * progress) * .55;
        for (int i = 0, o = 0; i < field.Length; i++, o += 4)
        {
            double behind = front - field[i];
            double t = Math.Clamp(behind / Feather * .5 + .5, 0, 1); t = t * t * (3 - 2 * t);
            byte shown = (byte)(t * 255);
            maskPixels[o] = maskPixels[o + 1] = maskPixels[o + 2] = maskPixels[o + 3] = shown;
            double a = Math.Exp(-Math.Pow(behind / (Feather * .45), 2)) * glow * body[i];
            glossPixels[o] = (byte)(255 * a); glossPixels[o + 1] = (byte)(236 * a); glossPixels[o + 2] = (byte)(214 * a); glossPixels[o + 3] = (byte)(255 * a);
        }
        mask.WritePixels(new Int32Rect(0, 0, FW, FH), maskPixels, FW * 4, 0);
        gloss.WritePixels(new Int32Rect(0, 0, FW, FH), glossPixels, FW * 4, 0);
    }

    // ----- the spread field -----
    private static (float[], float[]) Field(string model)
    {
        if (fields.TryGetValue(model, out var cached)) return cached;
        var small = new FormatConvertedBitmap(new TransformedBitmap(ControllerArt.Front(model), new ScaleTransform(FW / 1536.0, FH / 1024.0)), PixelFormats.Pbgra32, null, 0);
        var pixels = new byte[FW * FH * 4]; small.CopyPixels(pixels, FW * 4, 0);
        var body = new float[FW * FH]; for (int i = 0; i < body.Length; i++) body[i] = pixels[i * 4 + 3] / 255f;
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
