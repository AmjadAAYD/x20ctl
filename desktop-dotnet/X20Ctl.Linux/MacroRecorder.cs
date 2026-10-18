using System.Diagnostics;
using X20Ctl.Zone;
namespace X20Ctl.Linux;

/// <summary>Record on the Macros page: play the sequence on the controller and it becomes the slot's events. Reads the pad through the joystick interface (read-only).
/// Each held combination becomes one event and the rest after it its pause. Times round to the 5 ms grid, blips under 15 ms fold away, and it stops by itself after 2.5 s of rest following the last input or after 30 s.</summary>
internal sealed class MacroRecorder : IDisposable
{
    sealed record Snap(string Buttons, StickHeading Left, StickHeading Right) { public bool Neutral => Buttons.Length == 0 && Left == StickHeading.Neutral && Right == StickHeading.Neutral; }
    sealed record Segment(Snap State, double Start, double End);
    readonly List<Segment> segments = [];
    readonly Stopwatch clock = new();
    readonly GamepadReader reader = new();
    Snap current = new("", StickHeading.Neutral, StickHeading.Neutral); double currentStart, lastInput = -1;
    public bool Running { get; private set; }
    public double Elapsed => clock.Elapsed.TotalMilliseconds;
    public double IdleMs => lastInput < 0 ? 0 : Elapsed - lastInput;
    public bool Heard => lastInput >= 0;
    public bool ShouldStop => Heard && IdleMs > 2500 || Elapsed > 30000;
    public string Holding => current.Neutral ? "nothing" : string.Join(" + ", new[] { current.Buttons.Replace("+", " + ") }.Where(t => t.Length > 0)
        .Concat(current.Left == StickHeading.Neutral ? [] : new[] { "L " + current.Left }).Concat(current.Right == StickHeading.Neutral ? [] : new[] { "R " + current.Right }));

    /// <summary>False when no controller is exposed.</summary>
    public bool Start()
    {
        reader.Start(); if (!reader.Connected) return false;
        reader.Frame += OnFrame; Running = true; clock.Restart(); currentStart = 0; return true;
    }

    void OnFrame(PadFrame f)
    {
        if (!Running) return;
        var keys = f.Down.Where(MacroDraft.Inputs.Contains).OrderBy(k => Array.IndexOf(MacroDraft.Inputs, k));
        var snap = new Snap(string.Join("+", keys), Heading(f.LX, -f.LY), Heading(f.RX, -f.RY));
        if (snap == current) return;
        double now = Elapsed; segments.Add(new(current, currentStart, now)); current = snap; currentStart = now; lastInput = now;
    }

    public void Stop()
    {
        if (!Running) return;
        Running = false; reader.Frame -= OnFrame; double now = Elapsed; if (now > currentStart) segments.Add(new(current, currentStart, now));
    }

    public List<MacroEvent> Events()
    {
        var merged = new List<Segment>();
        foreach (var s in segments.Where(s => s.End > s.Start))
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
    static int Grid(double ms, int minimum) => Math.Clamp((int)Math.Round(ms / 5) * 5, minimum, 327675);

    /// <summary>A stick held past about half its travel, in one of eight directions (y positive is up here).</summary>
    static StickHeading Heading(double nx, double ny)
    {
        if (Math.Sqrt(nx * nx + ny * ny) < .55) return StickHeading.Neutral;
        int sector = ((int)Math.Round(Math.Atan2(nx, ny) / (Math.PI / 4)) + 8) % 8;
        return (StickHeading)(sector + 1);
    }
    public void Dispose() { Stop(); reader.Dispose(); }
}
