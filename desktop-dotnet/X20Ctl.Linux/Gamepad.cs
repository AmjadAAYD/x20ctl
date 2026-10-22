using System.Diagnostics;
using Avalonia.Threading;
namespace X20Ctl.Linux;

/// <summary>One reading of a controller: pressed buttons (by the app's names), both sticks (-1 to 1, up is negative) and both triggers (0 to 1).</summary>
internal sealed record PadFrame(IReadOnlySet<string> Down, double LX, double LY, double RX, double RY, double LT, double RT, long Stamp = 0)
{
    public static readonly PadFrame Empty = new(new HashSet<string>(), 0, 0, 0, 0, 0, 0);
}

/// <summary>Reads the first controller the kernel exposes through the joystick interface (/dev/input/js*), the same way the
/// xpad and xone drivers present an Xbox-layout pad. Read-only; nothing is written. Does nothing off Linux.</summary>
internal sealed class GamepadReader : IDisposable
{
    static readonly string[] ButtonNames = ["A", "B", "X", "Y", "LB", "RB", "SELECT", "START", "HOME", "L3", "R3"];
    readonly CancellationTokenSource stop = new();
    readonly object gate = new();
    readonly short[] axes = new short[8]; readonly bool[] buttons = new bool[16];
    long updates; DateTime started = DateTime.UtcNow;
    public string? Device { get; private set; }
    public bool Connected => Device != null;
    public event Action<PadFrame>? Frame;

    /// <summary>Set by a platform that delivers controller input itself (the Android app). Readers then follow it
    /// instead of /dev/input.</summary>
    public static ExternalPad? External { get; set; }

    public static string? Find()
    {
        if (!OperatingSystem.IsLinux()) return null;
        try { return Directory.EnumerateFiles("/dev/input", "js*").OrderBy(f => f, StringComparer.Ordinal).FirstOrDefault(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; }
    }

    public void Start()
    {
        if (External is { } outside)
        {
            Device = outside.Name ?? "Controller"; started = DateTime.UtcNow;
            outside.Frame += FromOutside;
            return;
        }
        if (Find() is not { } path) return;
        Device = path; started = DateTime.UtcNow;
        _ = Task.Run(() => Read(path), stop.Token);
    }

    void Read(string path)
    {
        try
        {
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var buffer = new byte[8];
            while (!stop.IsCancellationRequested)
            {
                int n = 0; while (n < 8) { int r = file.Read(buffer, n, 8 - n); if (r <= 0) return; n += r; }
                short value = BitConverter.ToInt16(buffer, 4); byte type = buffer[6], number = buffer[7];
                lock (gate)
                {
                    if ((type & 0x7F) == 1 && number < buttons.Length) buttons[number] = value != 0;
                    else if ((type & 0x7F) == 2 && number < axes.Length) axes[number] = value;
                    updates++;
                }
                var frame = Snapshot();
                Dispatcher.UIThread.Post(() => Frame?.Invoke(frame));
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Device = null; }
    }

    /// <summary>Input updates per second since the reader started.</summary>
    public double Rate { get { lock (gate) return updates / Math.Max(.001, (DateTime.UtcNow - started).TotalSeconds); } }

    void FromOutside(PadFrame frame)
    {
        lock (gate) updates++;
        Device = External?.Name ?? Device;
        Frame?.Invoke(frame);
    }

    public PadFrame Snapshot()
    {
        if (External is { } outside) return outside.Last;
        lock (gate)
        {
            var down = new HashSet<string>();
            for (int i = 0; i < ButtonNames.Length; i++) if (buttons[i]) down.Add(ButtonNames[i]);
            if (axes[6] < 0) down.Add("DPAD_LEFT"); if (axes[6] > 0) down.Add("DPAD_RIGHT"); if (axes[7] < 0) down.Add("DPAD_UP"); if (axes[7] > 0) down.Add("DPAD_DOWN");
            static double Stick(short v) => Math.Clamp(v / 32767.0, -1, 1);
            static double Trig(short v) => Math.Clamp((v + 32767) / 65534.0, 0, 1);
            double lt = Trig(axes[2]), rt = Trig(axes[5]);
            if (lt > .5) down.Add("LT"); if (rt > .5) down.Add("RT");
            return new PadFrame(down, Stick(axes[0]), Stick(axes[1]), Stick(axes[3]), Stick(axes[4]), lt, rt, Stopwatch.GetTimestamp());
        }
    }

    public void Dispose() { stop.Cancel(); if (External is { } outside) outside.Frame -= FromOutside; }
}

/// <summary>Controller input handed over by the platform, already in the app's names. Raised on the UI thread.</summary>
public sealed class ExternalPad
{
    internal PadFrame Last { get; private set; } = PadFrame.Empty;
    public string? Name { get; private set; }
    internal event Action<PadFrame>? Frame;
    /// <summary>A new reading: <paramref name="down"/> holds button names (A, B, X, Y, LB, RB, LT, RT, SELECT, START, HOME,
    /// L3, R3, DPAD_UP/DOWN/LEFT/RIGHT); sticks -1 to 1 with up negative, triggers 0 to 1.</summary>
    public void Push(string? name, IReadOnlySet<string> down, double lx, double ly, double rx, double ry, double lt, double rt)
    {
        Name = name ?? Name;
        Last = new PadFrame(down, lx, ly, rx, ry, lt, rt, Stopwatch.GetTimestamp());
        Frame?.Invoke(Last);
    }
}
