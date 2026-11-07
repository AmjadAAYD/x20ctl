namespace X20Ctl.Zone.PcMode;

public enum PressKind
{
    /// <summary>Down when the buttons are held together, Up when released: the output follows the buttons.</summary>
    Press,
    /// <summary>Once, on release, when held shorter than the hold time.</summary>
    Tap,
    /// <summary>Once, when held for the hold time.</summary>
    Hold,
    /// <summary>Once, on the second tap inside the double-tap window.</summary>
    DoubleTap,
    /// <summary>Once, on release.</summary>
    Release,
}

public enum ChordMode
{
    /// <summary>A single button fires at once even if it becomes part of a chord a moment later.</summary>
    Instant,
    /// <summary>A single button waits the chord window first, and is swallowed if a chord that contains it completes.</summary>
    WaitForChord,
}

public enum ChordPhase { Down, Up, Once }

/// <param name="Mask">One or more buttons. Two or more is a chord: all must go down within the chord window.</param>
public sealed record ChordBinding(string Id, uint Mask, PressKind Kind = PressKind.Press, ChordMode Mode = ChordMode.Instant);

public sealed record ChordEvent(string Id, ChordPhase Phase);

/// <summary>
/// Turns the buttons held on the real pad into binding events (rework plan, 6 Nov 2026). Pure: the caller supplies the clock,
/// so behaviour is the same in tests and live. It only decides when a binding fires; what the binding outputs is separate.
/// </summary>
public sealed class ChordEngine
{
    public int ChordWindowMs { get; init; } = 40;
    public int HoldMs { get; init; } = 350;
    public int DoubleTapMs { get; init; } = 300;

    readonly IReadOnlyList<ChordBinding> bindings;
    readonly long[] downSince = new long[32];
    readonly State[] states;
    uint previous;

    sealed class State { public bool Active, HoldFired, Suppressed; public long ActiveSince = -1, LastTapAt = long.MinValue / 2; }

    public ChordEngine(IReadOnlyList<ChordBinding> bindings)
    {
        this.bindings = bindings;
        states = bindings.Select(_ => new State()).ToArray();
    }

    static int Bits(uint mask) => System.Numerics.BitOperations.PopCount(mask);

    public IReadOnlyList<ChordEvent> Update(long nowMs, uint down)
    {
        var events = new List<ChordEvent>();
        uint pressed = down & ~previous;
        for (int bit = 0; bit < 32; bit++) if ((pressed >> bit & 1) != 0) downSince[bit] = nowMs;

        // Chords first, so the singles inside them can be swallowed in the same tick.
        for (int pass = 0; pass < 2; pass++)
            for (int i = 0; i < bindings.Count; i++)
            {
                var b = bindings[i]; var s = states[i];
                bool chord = Bits(b.Mask) > 1;
                if (chord != (pass == 0)) continue;
                bool allDown = (down & b.Mask) == b.Mask;

                if (!s.Active && !s.Suppressed && allDown)
                {
                    if (chord)
                    {
                        long first = long.MaxValue, last = long.MinValue;
                        for (int bit = 0; bit < 32; bit++) if ((b.Mask >> bit & 1) != 0) { first = Math.Min(first, downSince[bit]); last = Math.Max(last, downSince[bit]); }
                        if (last - first <= ChordWindowMs) Activate(i, nowMs, events);
                        else s.Suppressed = true; // pressed far apart: not a chord, and not retried until released
                    }
                    else if (SwallowedByChord(i, down)) s.Suppressed = true;
                    else if (b.Mode == ChordMode.Instant || nowMs - downSince[System.Numerics.BitOperations.TrailingZeroCount(b.Mask)] >= ChordWindowMs) Activate(i, nowMs, events);
                }
                else if (s.Active && !allDown) Deactivate(i, nowMs, events);

                if (!allDown) s.Suppressed = false;
                if (s.Active && b.Kind == PressKind.Hold && !s.HoldFired && nowMs - s.ActiveSince >= HoldMs) { s.HoldFired = true; events.Add(new(b.Id, ChordPhase.Once)); }
            }
        previous = down;
        return events;
    }

    bool SwallowedByChord(int single, uint down)
    {
        uint m = bindings[single].Mask;
        for (int j = 0; j < bindings.Count; j++)
            if (Bits(bindings[j].Mask) > 1 && (bindings[j].Mask & m) == m && states[j].Active) return true;
        return false;
    }

    void Activate(int i, long now, List<ChordEvent> events)
    {
        var s = states[i]; s.Active = true; s.ActiveSince = now; s.HoldFired = false;
        if (bindings[i].Kind == PressKind.Press) events.Add(new(bindings[i].Id, ChordPhase.Down));
    }

    void Deactivate(int i, long now, List<ChordEvent> events)
    {
        var b = bindings[i]; var s = states[i]; s.Active = false;
        long held = now - s.ActiveSince;
        switch (b.Kind)
        {
            case PressKind.Press: events.Add(new(b.Id, ChordPhase.Up)); break;
            case PressKind.Release: events.Add(new(b.Id, ChordPhase.Once)); break;
            case PressKind.Tap when held < HoldMs: events.Add(new(b.Id, ChordPhase.Once)); break;
            case PressKind.DoubleTap when held < HoldMs:
                if (now - s.LastTapAt <= DoubleTapMs) { events.Add(new(b.Id, ChordPhase.Once)); s.LastTapAt = long.MinValue / 2; }
                else s.LastTapAt = now;
                break;
        }
    }
}
