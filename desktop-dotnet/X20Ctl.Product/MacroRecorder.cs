using System.Diagnostics;
using X20Ctl.Product.Scanner;
using X20Ctl.Zone;
namespace X20Ctl.Product;

/// <summary>
/// "Record" on the Macros page (owner direction 10 Oct 2026): play the sequence on the controller and it becomes the
/// slot's events. A background thread reads documented XInput (read-only, the Scanner 2.0.0 reader) about every
/// millisecond, locks onto the first player slot that moves, and keeps every change of what is held. Each held
/// combination becomes one event; the rest that follows it becomes its pause. Times are rounded to the 5 ms grid the
/// controller stores, blips shorter than 15 ms between two presses are folded away, and it stops by itself after
/// 2.5 s of rest following the last input or after 30 s. Nothing is ever sent to the controller.
/// </summary>
public sealed class MacroRecorder : IDisposable
{
    private sealed record Snap(string Buttons, StickHeading Left, StickHeading Right) { public bool Neutral => Buttons.Length == 0 && Left == StickHeading.Neutral && Right == StickHeading.Neutral; }
    private sealed record Segment(Snap State, double Start, double End);
    private readonly List<Segment> segments = new();
    private readonly object gate = new();
    private readonly Stopwatch clock = new();
    private WindowsReader? reader; private Thread? worker; private volatile bool running;
    private Snap current = new("", StickHeading.Neutral, StickHeading.Neutral); private double currentStart; private double lastInput = -1;
    public int Slot { get; private set; } = -1;
    public bool IsRunning => running;
    public double Elapsed => clock.Elapsed.TotalMilliseconds;
    /// <summary>Time since the last change, while something has been played.</summary>
    public double IdleMs => lastInput < 0 ? 0 : Elapsed - lastInput;
    public bool Heard => lastInput >= 0;
    public string Holding { get { lock (gate) return Describe(current); } }
    public event Action? Stopped;

    public void Start()
    {
        reader = new WindowsReader(WindowsReader.Libraries[0]);
        running = true; clock.Restart(); currentStart = 0;
        worker = new Thread(Loop) { IsBackground = true, Name = "X20CTL macro recorder", Priority = ThreadPriority.AboveNormal };
        worker.Start();
    }
    private void Loop()
    {
        while (running)
        {
            double now = clock.Elapsed.TotalMilliseconds;
            if (Slot < 0)
                for (int i = 0; i < 4 && Slot < 0; i++) if (reader!.Read(i, out var probe) && !Read(probe).Neutral) Slot = i;
            if (Slot >= 0 && reader!.Read(Slot, out var state)) Feed(Read(state), now);
            if (Heard && now - lastInput > 2500 || now > 30000) { running = false; break; }
            Thread.Sleep(1);
        }
        Close(clock.Elapsed.TotalMilliseconds);
        Stopped?.Invoke();
    }
    public void Stop() { running = false; worker?.Join(200); }
    private void Feed(Snap snap, double now)
    {
        lock (gate)
        {
            if (snap == current) return;
            segments.Add(new(current, currentStart, now)); current = snap; currentStart = now; lastInput = now;
        }
    }
    private void Close(double now) { lock (gate) { if (now > currentStart) segments.Add(new(current, currentStart, now)); currentStart = now; } }

    /// <summary>The recording as macro events: each held combination with the rest after it.</summary>
    public List<MacroEvent> Events()
    {
        List<Segment> rows; lock (gate) rows = segments.Where(s => s.End > s.Start).ToList();
        // fold blips: a state that lasted under 15 ms between two others joins the one before it
        var merged = new List<Segment>();
        foreach (var s in rows)
        {
            if (merged.Count > 0 && (s.End - s.Start < 15 || merged[^1].State == s.State)) { merged[^1] = merged[^1] with { End = s.End }; continue; }
            merged.Add(s);
        }
        while (merged.Count > 0 && merged[0].State.Neutral) merged.RemoveAt(0);
        var events = new List<MacroEvent>();
        for (int i = 0; i < merged.Count; i++)
        {
            if (merged[i].State.Neutral) continue;
            int hold = Grid(merged[i].End - merged[i].Start, 5);
            int pause = i + 1 < merged.Count && merged[i + 1].State.Neutral ? (i + 2 < merged.Count ? Grid(merged[i + 1].End - merged[i + 1].Start, 0) : 0) : 0;
            var s = merged[i].State;
            events.Add(new(Guid.NewGuid(), s.Buttons.Length == 0 ? Array.Empty<string>() : s.Buttons.Split('+'), s.Left, s.Right, hold, pause));
        }
        return events;
    }
    private static int Grid(double ms, int minimum) => Math.Clamp((int)Math.Round(ms / 5) * 5, minimum, 327675);

    private static readonly (int Mask, string Key)[] Map = [(4096, "A"), (8192, "B"), (16384, "X"), (32768, "Y"), (256, "LB"), (512, "RB"), (64, "L3"), (128, "R3"), (1, "DPAD_UP"), (2, "DPAD_DOWN"), (4, "DPAD_LEFT"), (8, "DPAD_RIGHT"), (32, "SELECT"), (16, "START")];
    private static Snap Read(State s)
    {
        var keys = Map.Where(m => (s.Pad.Buttons & m.Mask) != 0).Select(m => m.Key).ToList();
        if (s.Pad.LT > 30) keys.Add("LT"); if (s.Pad.RT > 30) keys.Add("RT");
        return new(string.Join("+", keys.Where(MacroDraft.Inputs.Contains)), Heading(s.Pad.LX, s.Pad.LY), Heading(s.Pad.RX, s.Pad.RY));
    }
    /// <summary>A stick held past about half its travel, in one of eight directions (+Y is up in XInput).</summary>
    private static StickHeading Heading(short x, short y)
    {
        double nx = x / 32768.0, ny = y / 32768.0;
        if (Math.Sqrt(nx * nx + ny * ny) < .55) return StickHeading.Neutral;
        int sector = ((int)Math.Round(Math.Atan2(nx, ny) / (Math.PI / 4)) + 8) % 8;
        return (StickHeading)(sector + 1);
    }
    private static string Describe(Snap s) => s.Neutral ? "nothing" : string.Join(" + ", new[] { s.Buttons.Replace("+", " + ") }.Where(t => t.Length > 0)
        .Concat(s.Left == StickHeading.Neutral ? [] : new[] { "L " + s.Left }).Concat(s.Right == StickHeading.Neutral ? [] : new[] { "R " + s.Right }));
    public void Dispose() { Stop(); reader?.Dispose(); reader = null; }
}
