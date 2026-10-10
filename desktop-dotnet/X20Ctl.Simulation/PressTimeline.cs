namespace X20Ctl.Simulation;

public record ButtonEdge(string Control, bool Down, double TimeMs, double? DurationMs);
public sealed class PressTimeline
{
    private readonly Dictionary<string, double> started = [];
    private readonly Dictionary<string, double> holds = [];
    private string? source;
    private int slot;
    public IReadOnlyDictionary<string, double> Holds => holds;
    public double? LastDurationMs { get; private set; }
    public bool Interrupted { get; private set; }

    public IReadOnlyList<ButtonEdge> Observe(InputFrame frame)
    {
        bool changed = source != null && (source != frame.SourceKey || slot != frame.Slot);
        source = frame.SourceKey;
        slot = frame.Slot;
        if (!frame.Connected || changed)
        {
            started.Clear(); holds.Clear(); LastDurationMs = null; Interrupted = true;
            return [];
        }
        var pressed = Enum.GetValues<Buttons>().Where(b => b != Buttons.None && frame.Buttons.HasFlag(b))
            .Select(b => frame.SourceKey.Contains("raw-input") ? $"Button {Array.IndexOf(Enum.GetValues<Buttons>(), b)}" : b.ToString())
            .ToHashSet();
        if (frame.LT > 0) pressed.Add(frame.SourceKey.Contains("raw-input") ? "Usage Z" : "LT");
        if (frame.RT > 0) pressed.Add(frame.SourceKey.Contains("raw-input") ? "Usage Rz" : "RT");
        var edges = new List<ButtonEdge>();
        foreach (var control in started.Keys.ToArray())
        {
            if (pressed.Contains(control)) continue;
            LastDurationMs = Math.Max(0, frame.TimeMs - started[control]);
            edges.Add(new(control, false, frame.TimeMs, LastDurationMs));
            started.Remove(control);
        }
        foreach (var control in pressed.Order())
        {
            if (started.ContainsKey(control)) continue;
            started[control] = frame.TimeMs;
            edges.Add(new(control, true, frame.TimeMs, null));
        }
        holds.Clear();
        foreach (var pair in started) holds[pair.Key] = Math.Max(0, frame.TimeMs - pair.Value);
        return edges;
    }

    public void Reset()
    {
        started.Clear(); holds.Clear(); source = null; LastDurationMs = null; Interrupted = false;
    }
}
