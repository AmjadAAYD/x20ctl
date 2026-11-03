namespace X20Ctl.Zone;

/// <summary>
/// The shared controller input contract (3 Nov 2026, from the input-binding research): one tab order for every
/// theme, one owner per gesture, and remap capture that needs a fresh, single, released press for each stage.
/// </summary>
public static class StudioTabs
{
    /// <summary>The page a bumper press goes to from <paramref name="current"/>: the next visible tab, wrapping.
    /// Pages that are not tabs (Tester, Diagnostics) step from Device, the tab they open from.</summary>
    public static string? Step(IReadOnlyList<string> visible, string current, int step)
    {
        if (visible.Count == 0) return null;
        string anchor = current is "Tester" or "Diagnostics" && visible.Contains("Device") ? "Device" : current;
        int index = -1; for (int i = 0; i < visible.Count; i++) if (string.Equals(visible[i], anchor, StringComparison.OrdinalIgnoreCase)) index = i;
        if (index < 0) return visible[step > 0 ? 0 : visible.Count - 1];
        return visible[((index + Math.Sign(step)) % visible.Count + visible.Count) % visible.Count];
    }
}

/// <summary>Who owns a press right now. Earlier rules win; exactly one owner per gesture.</summary>
public static class InputOwnerRules
{
    public sealed record State(bool WindowActive, bool Connected, bool ModalOpen, bool MacroRecording, bool RemapEngaged, bool TesterPage, bool TextFocused);
    public enum Owner { Nobody, Modal, MacroRecording, RemapCapture, Tester, TextField, Navigation }
    public static Owner Resolve(State s) =>
        !s.WindowActive || !s.Connected ? Owner.Nobody
        : s.ModalOpen ? Owner.Modal
        : s.MacroRecording ? Owner.MacroRecording
        : s.RemapEngaged ? Owner.RemapCapture
        : s.TesterPage ? Owner.Tester
        : s.TextFocused ? Owner.TextField
        : Owner.Navigation;
}

/// <summary>
/// Remap capture: Source then Target. Arming waits until everything is released, so the press that opened it and
/// buttons already held never count. A stage accepts exactly one fresh button; two at once is ambiguous and stays
/// armed. After a stage is accepted, everything must be released before the next one listens.
/// </summary>
public sealed class RemapCapture
{
    public enum Side { None, Source, Target }
    private HashSet<string> held = new();
    public Side Listening { get; private set; }
    /// <summary>Waiting for every button to be let go before the next press counts.</summary>
    public bool WaitingForRelease { get; private set; }
    /// <summary>The last press had more than one button in it.</summary>
    public bool Ambiguous { get; private set; }
    public void Begin(Side side, IEnumerable<string> heldNow)
    {
        Listening = side; held = new(heldNow); WaitingForRelease = held.Count > 0; Ambiguous = false;
    }
    /// <summary>Moves to the next stage after an accepted press; the release barrier stays in place.</summary>
    public void Continue(Side side) { Listening = side; if (side == Side.None) Ambiguous = false; }
    public void Cancel() { Listening = Side.None; WaitingForRelease = false; Ambiguous = false; }
    /// <summary>Feeds the full set of held buttons. Returns the one button this stage accepts, or null.</summary>
    public string? Observe(IReadOnlyCollection<string> now)
    {
        var fresh = now.Where(b => !held.Contains(b)).ToArray(); held = new(now);
        if (Listening == Side.None) return null;
        if (WaitingForRelease) { if (now.Count == 0) WaitingForRelease = false; return null; }
        if (fresh.Length == 0) return null;
        WaitingForRelease = true;
        if (now.Count > 1) { Ambiguous = true; return null; }
        Ambiguous = false; return fresh[0];
    }
}

/// <summary>
/// Delivery between the engine thread and the UI: keeps every change of button state in order, and only merges
/// consecutive samples whose buttons are identical (analog-only movement). Bounded; overflow drops the oldest.
/// </summary>
public sealed class EdgeQueue<T>
{
    private readonly LinkedList<(T Item, string Key)> items = new();
    private readonly int capacity;
    public EdgeQueue(int capacity = 64) => this.capacity = capacity;
    public long Coalesced { get; private set; }
    public long Dropped { get; private set; }
    public int Count => items.Count;
    public void Offer(T item, string key)
    {
        if (items.Last is { } last && last.Value.Key == key) { last.Value = (item, key); Coalesced++; return; }
        items.AddLast((item, key));
        while (items.Count > capacity) { items.RemoveFirst(); Dropped++; }
    }
    public List<T> Drain() { var all = items.Select(i => i.Item).ToList(); items.Clear(); return all; }
}
