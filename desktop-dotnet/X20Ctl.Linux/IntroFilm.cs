using System.IO.Compression;
using System.Text.Json;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SkiaSharp;
namespace X20Ctl.Linux;

/// <summary>The opening film's picture: the Windows app's intro.mp4 as standalone WebP stills in a stored zip,
/// Brand/intro-film.frames (tools/linux-studio/build_intro_film.py), so it needs no video codec on the system; distros
/// differ in which ones they ship. A background thread decodes the frame the clock is on, with the Skia the app already
/// uses; the UI thread copies the newest one into the bitmap it draws. Every still stands alone, so a slow machine skips frames rather than lagging.</summary>
internal sealed class IntroFilm : IDisposable
{
    public static string FilePath => Path.Combine(AppContent.Root, "Brand", "intro-film.frames");
    public static bool Installed => File.Exists(FilePath);

    readonly ZipArchive pack; readonly ZipArchiveEntry[] stills; readonly double fps;
    readonly SKImageInfo info; readonly object gate = new();
    // two pinned frame buffers: the worker decodes into the back one, then swaps it to the front under the lock
    byte[] front, back; int frontFrame = -1, shownFrame = -1;
    readonly Thread worker; volatile bool stop; double clock;
    WriteableBitmap? picture;
    public PixelSize Size => new(info.Width, info.Height);
    public double Duration => stills.Length / fps;
    /// <summary>The first frame is decoded: the film can start.</summary>
    public bool Ready { get { lock (gate) return frontFrame >= 0; } }
    /// <summary>A still could not be read or decoded; the intro moves on without the film.</summary>
    public bool Failed => failed;
    volatile bool failed;

    IntroFilm(ZipArchive pack, ZipArchiveEntry[] stills, double fps, int width, int height)
    {
        this.pack = pack; this.stills = stills; this.fps = fps;
        info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        front = GC.AllocateArray<byte>(info.BytesSize, pinned: true); back = GC.AllocateArray<byte>(info.BytesSize, pinned: true);
        worker = new Thread(Decode) { IsBackground = true, Name = "X20CTL intro film" }; worker.Start();
    }

    /// <summary>Opens the film, or null when it is missing or unreadable (the intro then starts at the logo).</summary>
    public static IntroFilm? Open()
    {
        ZipArchive? pack = null;
        try
        {
            if (!Installed) return null;
            pack = ZipFile.OpenRead(FilePath);
            using var meta = JsonDocument.Parse(pack.GetEntry("film.json")?.Open() ?? throw new InvalidDataException("film.json missing"));
            var root = meta.RootElement;
            double fps = root.GetProperty("fps").GetDouble(); int width = root.GetProperty("width").GetInt32(), height = root.GetProperty("height").GetInt32();
            var stills = pack.Entries.Where(e => e.Name.StartsWith('f') && e.Name.EndsWith(".webp", StringComparison.Ordinal)).OrderBy(e => e.Name, StringComparer.Ordinal).ToArray();
            if (stills.Length < 2 || fps <= 0 || width <= 0 || height <= 0) throw new InvalidDataException("empty film");
            return new IntroFilm(pack, stills, fps, width, height);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or JsonException or KeyNotFoundException or InvalidOperationException)
        { pack?.Dispose(); return null; }
    }

    /// <summary>Where the film should be, in seconds; the worker decodes the frame for that moment.</summary>
    public void Seek(double seconds) => Volatile.Write(ref clock, Math.Max(0, seconds));
    /// <summary>The frame for <paramref name="seconds"/> is the one decoded (review shots wait for it).</summary>
    public bool Holds(double seconds) { lock (gate) return frontFrame == FrameAt(seconds); }

    int FrameAt(double t) => Math.Clamp((int)(t * fps), 0, stills.Length - 1);

    void Decode()
    {
        int decoded = -1;
        try
        {
            while (!stop)
            {
                int target = FrameAt(Volatile.Read(ref clock));
                if (target == decoded) { Thread.Sleep(2); continue; }
                byte[] bytes;
                using (var stream = stills[target].Open()) { bytes = new byte[stills[target].Length]; stream.ReadExactly(bytes); }
                using (var data = SKData.CreateCopy(bytes))
                using (var codec = SKCodec.Create(data))
                {
                    if (codec == null) { failed = true; return; }
                    unsafe { fixed (byte* pixels = back) if (codec.GetPixels(info, (IntPtr)pixels) is not (SKCodecResult.Success or SKCodecResult.IncompleteInput)) { failed = true; return; } }
                }
                decoded = target;
                lock (gate) { (front, back) = (back, front); frontFrame = decoded; }
            }
        }
        catch (Exception e) when (e is IOException or InvalidDataException or ObjectDisposedException) { failed = true; }
    }

    /// <summary>The newest decoded frame, as a bitmap to draw (UI thread). Null until the first frame is ready.</summary>
    public Bitmap? Picture()
    {
        lock (gate)
        {
            if (frontFrame < 0 || frontFrame == shownFrame) return picture;
            picture ??= new WriteableBitmap(new PixelSize(info.Width, info.Height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
            using (var target = picture.Lock())
            {
                int row = info.RowBytes;
                if (target.RowBytes == row) System.Runtime.InteropServices.Marshal.Copy(front, 0, target.Address, row * info.Height);
                else for (int y = 0; y < info.Height; y++) System.Runtime.InteropServices.Marshal.Copy(front, y * row, target.Address + y * target.RowBytes, Math.Min(row, target.RowBytes));
            }
            shownFrame = frontFrame;
            return picture;
        }
    }

    public void Dispose()
    {
        stop = true; if (worker.IsAlive && Thread.CurrentThread != worker) worker.Join(500);
        pack.Dispose(); picture?.Dispose(); picture = null;
    }
}
