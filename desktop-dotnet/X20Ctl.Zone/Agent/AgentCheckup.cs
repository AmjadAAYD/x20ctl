namespace X20Ctl.Zone.Agent;

/// <summary>
/// The agent's own check-up (owner, 8 Nov 2026: "whatever happens in the agent must not happen in the background"): the
/// sticks at rest, each stick rolled round its edge, both triggers, every button, measured from the live controller on the
/// agent's screen. These are the measurements; the screen is AgentPanel.Checkup.cs. Nothing is written to the controller.
/// </summary>
public static class AgentCheckup
{
    public const int Sectors = 36;
    /// <summary>A sector counts as reached once the stick has been at least this far out in it.</summary>
    public const double Reached = .7;
    /// <summary>Resting further off centre than this is drift worth fixing.</summary>
    public const double DriftLimit = .05;

    public static double Unit(short v) => Math.Clamp(v / 32767.0, -1, 1);
    public static double Unit(byte v) => v / 255.0;
}

/// <summary>A stick rolled round its edge: how far out it got in each of 36 directions.</summary>
public sealed class StickSweep
{
    readonly double[] reach = new double[AgentCheckup.Sectors];
    public IReadOnlyList<double> Reach => reach;

    public void Add(double x, double y)
    {
        double r = Math.Sqrt(x * x + y * y);
        if (r < .3) return;
        int sector = (int)Math.Floor((Math.Atan2(y, x) + Math.PI) / (2 * Math.PI) * AgentCheckup.Sectors) % AgentCheckup.Sectors;
        reach[sector] = Math.Max(reach[sector], Math.Min(r, 1.5));
    }

    /// <summary>The share of directions the stick has been pushed out in (0–1).</summary>
    public double Coverage => reach.Count(r => r >= AgentCheckup.Reached) / (double)AgentCheckup.Sectors;
    public bool Done => Coverage >= 33.0 / AgentCheckup.Sectors;
    /// <summary>How far it reaches at its edge, on average over the directions it reached (1 is full tilt).</summary>
    public double AverageReach => reach.Where(r => r >= AgentCheckup.Reached).DefaultIfEmpty(0).Average();
    /// <summary>Its shortest reach among the directions it reached, and which way that is.</summary>
    public (double Reach, string Where) Weakest
    {
        get
        {
            int at = -1; double low = double.MaxValue;
            for (int i = 0; i < reach.Length; i++) if (reach[i] >= AgentCheckup.Reached && reach[i] < low) { low = reach[i]; at = i; }
            return at < 0 ? (0, "") : (low, Direction(at));
        }
    }
    static string Direction(int sector)
    {
        double degrees = (sector + .5) * 360.0 / AgentCheckup.Sectors - 180; // atan2's angle, y up
        string[] names = ["left", "down-left", "down", "down-right", "right", "up-right", "up", "up-left"];
        return names[(int)Math.Round((degrees + 180) / 45) % 8];
    }
    public void Reset() => Array.Clear(reach);
}

/// <summary>A stick left alone: where it sits and how much it wanders.</summary>
public sealed class RestReading
{
    double sumX, sumY; int count; double furthest;
    public void Add(double x, double y) { sumX += x; sumY += y; count++; furthest = Math.Max(furthest, Math.Sqrt(x * x + y * y)); }
    public int Count => count;
    /// <summary>How far off centre it rests (0–1).</summary>
    public double Offset => count == 0 ? 0 : Math.Sqrt(sumX / count * (sumX / count) + sumY / count * (sumY / count));
    public double Furthest => furthest;
    /// <summary>A hand on the stick, not drift: it went a quarter of the way out.</summary>
    public bool Touched => furthest > .25;
    public void Reset() { sumX = sumY = 0; count = 0; furthest = 0; }
}

/// <summary>What the check-up found, and what to say about it.</summary>
public sealed record CheckupResult(
    double LeftRest, double RightRest, StickSweep Left, StickSweep Right, double LtMax, double RtMax,
    IReadOnlyList<string> Pressed, IReadOnlyList<string> Missed, double UpdatesPerSecond, IReadOnlyList<string> Skipped)
{
    /// <summary>The dead zone that hides a resting offset with some room to spare, in whole percent.</summary>
    public static int DeadZoneFor(double rest) => (int)Math.Ceiling(rest * 100 + 3);

    public IReadOnlyList<(bool Good, string Line)> Findings()
    {
        var lines = new List<(bool, string)>();
        foreach (var (name, rest) in new[] { ("left", LeftRest), ("right", RightRest) })
            lines.Add(rest > AgentCheckup.DriftLimit
                ? (false, $"Your {name} stick rests {rest * 100:0}% off centre with nobody touching it. That's drift.")
                : (true, $"{Cap(name)} stick rests in the middle ({rest * 100:0.#}% off)."));
        foreach (var (name, sweep) in new[] { ("left", Left), ("right", Right) })
        {
            if (Skipped.Contains(name + " stick")) { lines.Add((true, $"{Cap(name)} stick's circle was skipped.")); continue; }
            if (sweep.Coverage < .5) { lines.Add((false, $"The {name} stick only reached {sweep.Coverage * 100:0}% of its edge. Roll it all the way round to check it.")); continue; }
            var (low, where) = sweep.Weakest;
            lines.Add(sweep.AverageReach < .9
                ? (false, $"The {name} stick reaches {sweep.AverageReach * 100:0}% at its edge on average, so games may never see full tilt.")
                : low < .85 ? (false, $"The {name} stick falls short going {where} ({low * 100:0}%), the rest of its edge is fine.")
                : (true, $"The {name} stick reaches its whole edge ({sweep.AverageReach * 100:0}% on average)."));
        }
        if (Skipped.Contains("triggers")) lines.Add((true, "Triggers were skipped."));
        else foreach (var (name, max) in new[] { ("LT", LtMax), ("RT", RtMax) })
            lines.Add(max >= .95 ? (true, $"{name} goes all the way ({max * 100:0}%).") : (false, $"{name} only got to {max * 100:0}%. If you squeezed it fully, it isn't reaching the end."));
        if (Skipped.Contains("buttons")) lines.Add((true, "Buttons were skipped."));
        else lines.Add(Missed.Count == 0 ? (true, $"All {Pressed.Count} buttons registered.") : (false, $"{string.Join(", ", Missed)} never registered. If you pressed {(Missed.Count == 1 ? "it" : "them")}, that's worth reporting."));
        if (UpdatesPerSecond > 0)
            lines.Add(UpdatesPerSecond >= 60 ? (true, $"It reports {UpdatesPerSecond:0} times a second while moving, no lag from the connection.")
                : (false, $"It only reports {UpdatesPerSecond:0} times a second while moving. A cable or closer Bluetooth usually helps."));
        return lines;
    }

    static string Cap(string s) => char.ToUpperInvariant(s[0]) + s[1..];
}
