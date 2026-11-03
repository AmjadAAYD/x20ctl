namespace X20Ctl.Zone.Input;

/// <summary>Session assignments only: model selection is not physical-device identity.</summary>
public sealed class PlayerInputAssignments
{
    readonly string?[] sources = new string?[4];
    readonly long[] revisions = new long[4];
    readonly object gate = new();
    public string? Source(int player) { lock (gate) return sources[player]; }
    public long Revision(int player) { lock (gate) return revisions[player]; }
    public bool ClearIfCurrent(int player, string source, long revision)
    {
        lock (gate) { if (sources[player] != source || revisions[player] != revision) return false; sources[player] = null; revisions[player]++; return true; }
    }
    public bool Select(int player, string? source)
    {
        lock (gate)
        {
            if (source != null && sources.Where((_, i) => i != player).Contains(source)) return false;
            sources[player] = source; revisions[player]++; return true;
        }
    }
    public void RemoveMissing(IEnumerable<string> present)
    {
        var available = present.ToHashSet(StringComparer.Ordinal);
        lock (gate) for (int i = 0; i < sources.Length; i++) if (sources[i] is { } s && !available.Contains(s)) { sources[i] = null; revisions[i]++; }
    }
}

public static class InputOwnership
{
    public static bool CanNavigate(bool active, bool editingText, bool exclusive, bool mouse) => active && !editingText && !exclusive && !mouse;
}

/// <summary>Single-button capture. Each stage requires neutral; chords never choose an arbitrary element.</summary>
public sealed class ButtonCapture
{
    public string? Source { get; private set; }
    public string? Target { get; private set; }
    public bool Active { get; private set; }
    public bool WaitingForRelease { get; private set; }
    public void Begin(string? source = null) { Source = source; Target = null; Active = true; WaitingForRelease = true; }
    public void Cancel() { Active = false; Source = Target = null; WaitingForRelease = false; }
    public void Feed(IEnumerable<string> buttons)
    {
        if (!Active) return;
        var down = buttons.Distinct().ToArray();
        if (WaitingForRelease) { if (down.Length == 0) WaitingForRelease = false; return; }
        if (down.Length == 0) return;
        WaitingForRelease = true;
        if (down.Length != 1) return;
        if (Source == null) Source = down[0];
        else { Target = down[0]; Active = false; }
    }
}

/// <summary>Neutral baseline and fresh edges; only single directions repeat. Context changes reset this gate.</summary>
public sealed class NavigationGestures
{
    bool armed;
    HashSet<string> previous = [];
    long nextRepeat;
    static bool Direction(string key) => key.StartsWith("DPAD_", StringComparison.Ordinal);
    public void Reset() { armed = false; previous.Clear(); nextRepeat = 0; }
    public IReadOnlyList<string> Read(IEnumerable<string> buttons, long milliseconds)
    {
        var down = buttons.ToHashSet(StringComparer.Ordinal);
        if (!armed) { if (down.Count == 0) armed = true; previous = down; return []; }
        var pressed = down.Except(previous).ToArray();
        bool changed = !down.SetEquals(previous); previous = down;
        if (changed) nextRepeat = milliseconds + 400;
        // A single gesture has one command owner; chords are not serialized into navigation.
        if (down.Count != 1) return [];
        if (pressed.Length == 1) return pressed;
        string key = down.Single();
        if (Direction(key) && milliseconds >= nextRepeat) { nextRepeat = milliseconds + 120; return [key]; }
        return [];
    }
}

/// <summary>One dominant stick direction, with separate activation and release thresholds.</summary>
public sealed class StickNavigation
{
    string? held;
    public void Reset() => held = null;
    public string? Read(double x, double y)
    {
        bool keep = held switch { "DPAD_LEFT" => x < -.4, "DPAD_RIGHT" => x > .4, "DPAD_UP" => y < -.4, "DPAD_DOWN" => y > .4, _ => false };
        if (!keep) held = null;
        if (Math.Max(Math.Abs(x), Math.Abs(y)) >= .6)
            held = Math.Abs(x) >= Math.Abs(y) ? (x < 0 ? "DPAD_LEFT" : "DPAD_RIGHT") : (y < 0 ? "DPAD_UP" : "DPAD_DOWN");
        return held;
    }
}
