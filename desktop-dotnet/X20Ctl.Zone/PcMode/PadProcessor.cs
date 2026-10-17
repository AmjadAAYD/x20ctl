namespace X20Ctl.Zone.PcMode;

/// <summary>One Xbox-layout controller state, the shape XInput reads and a virtual Xbox pad is fed.</summary>
public readonly record struct PadState(ushort Buttons, byte LeftTrigger, byte RightTrigger, short LeftX, short LeftY, short RightX, short RightY);

public static class PadButtons
{
    public const ushort Up = 0x0001, Down = 0x0002, Left = 0x0004, Right = 0x0008, Start = 0x0010, Back = 0x0020, L3 = 0x0040, R3 = 0x0080,
        LB = 0x0100, RB = 0x0200, Guide = 0x0400, A = 0x1000, B = 0x2000, X = 0x4000, Y = 0x8000;
    /// <summary>Button names exactly as the Studio's drafts use them. LT and RT are analog and handled separately.</summary>
    public static readonly IReadOnlyDictionary<string, ushort> Mask = new Dictionary<string, ushort>
    {
        ["DPAD_UP"] = Up, ["DPAD_DOWN"] = Down, ["DPAD_LEFT"] = Left, ["DPAD_RIGHT"] = Right, ["START"] = Start, ["SELECT"] = Back,
        ["L3"] = L3, ["R3"] = R3, ["LB"] = LB, ["RB"] = RB, ["A"] = A, ["B"] = B, ["X"] = X, ["Y"] = Y, ["HOME"] = Guide,
    };
    /// <summary>XInput's own trigger threshold: a trigger counts as "pressed" above this.</summary>
    public const byte TriggerThreshold = 30;
}

/// <summary>
/// What PC mode does to a pad, per controller model: remaps, stick and trigger curves, turbo and macros. It lives on the PC
/// (nothing is written to the controller) and is stored as plain JSON.
/// </summary>
public sealed class PcModeProfile
{
    /// <summary>Source button → target button. Identity entries are never stored.</summary>
    public Dictionary<string, string> Remap { get; set; } = new();
    /// <summary>CurveChannel name → curve. Missing means the default (straight) curve.</summary>
    public Dictionary<string, SetupCurve> Curves { get; set; } = new();
    /// <summary>Button → presses per second while held.</summary>
    public Dictionary<string, int> Turbo { get; set; } = new();
    /// <summary>Trigger button → the sequence it plays once per press. The trigger itself is not passed to the game.</summary>
    public Dictionary<string, SetupMacro[]> Macros { get; set; } = new();
    /// <summary>"LeftStick"/"RightStick" → fine dead zones, square gate and axis ratio. When present, its dead zones
    /// replace the curve's whole-percent ones for that stick.</summary>
    public Dictionary<string, StickTuning> Sticks { get; set; } = new();
    /// <summary>A per-game profile's display name (the game's window title when it was added). Not a setting.</summary>
    public string? Name { get; set; }

    public bool IsNeutral => Remap.Count == 0 && Turbo.Count == 0 && Macros.Count == 0 && Curves.Values.All(IsDefault) && Sticks.Values.All(s => s.IsDefault);
    public static bool IsDefault(SetupCurve c) => c.Inner == 0 && c.Outer == 100 && c.X1 == 85 && c.Y1 == 85 && c.X2 == 170 && c.Y2 == 170;
    public const int MinTurboHz = 2, MaxTurboHz = 30;

    public void Validate()
    {
        if (Name is { Length: > 120 }) throw new InvalidDataException("Name too long.");
        if (Remap == null || Curves == null || Turbo == null || Macros == null || Sticks == null) throw new InvalidDataException("Missing settings.");
        if (Macros.Count > 32 || Remap.Count > 64) throw new InvalidDataException("Too many settings.");
        foreach (var (source, target) in Remap)
            if (source == null || target == null || !Known(source) || !(Known(target)||target=="NONE")) throw new InvalidDataException("Unknown button in a remap.");
        foreach (var (button, hz) in Turbo)
            if (!Known(button) || hz < MinTurboHz || hz > MaxTurboHz) throw new InvalidDataException("Turbo needs a known button and 2 to 30 presses a second.");
        foreach (var (name, curve) in Curves)
            if (curve == null || !Enum.TryParse<CurveChannel>(name, out _) || curve.Inner < 0 || curve.Outer > 100 || curve.Inner >= curve.Outer || curve.X1 > curve.X2 || new[] { curve.X1, curve.Y1, curve.X2, curve.Y2 }.Any(v => v < 0 || v > 255))
                throw new InvalidDataException("Invalid curve.");
        foreach (var (name, s) in Sticks)
            if (name is not ("LeftStick" or "RightStick") || s == null || !double.IsFinite(s.Inner) || !double.IsFinite(s.Outer) || s.Inner < 0 || s.Inner > StickTuning.MaxInner
                || s.Outer < StickTuning.MinOuter || s.Outer > 100 || s.Inner >= s.Outer || s.AxisRatio < StickTuning.MinRatio || s.AxisRatio > StickTuning.MaxRatio
                || !double.IsFinite(s.AntiDeadzone) || !double.IsFinite(s.MaxOutput) || s.AntiDeadzone < 0 || s.AntiDeadzone > StickTuning.MaxAnti || s.MaxOutput < StickTuning.MinOuter || s.MaxOutput > 100 || s.AntiDeadzone >= s.MaxOutput)
                throw new InvalidDataException("Invalid stick settings.");
        foreach (var (trigger, steps) in Macros)
        {
            if (!Known(trigger)) throw new InvalidDataException("Unknown macro trigger.");
            if (steps == null || steps.Length > 64 || steps.Any(s => s == null || s.Buttons == null || s.Buttons.Any(b => b == null) || s.Buttons.Any(b => !Known(b)) || s.HoldMs < 0 || s.PauseMs < 0 || s.HoldMs > 10000 || s.PauseMs > 10000))
                throw new InvalidDataException("Invalid macro.");
        }
    }
    static bool Known(string name) => name is "LT" or "RT" || PadButtons.Mask.ContainsKey(name);
}

/// <summary>Fine stick settings for PC mode: dead zones in 0.1% steps, a square gate, and how fast up/down moves
/// compared with left/right.</summary>
public sealed class StickTuning
{
    public const double MaxInner = 40, MinOuter = 60, MaxAnti = 40; public const int MinRatio = 50, MaxRatio = 150;
    /// <summary>Percent of travel ignored at the centre (0–40, in 0.1 steps).</summary>
    public double Inner { get; set; }
    /// <summary>Percent of travel where full output is reached (60–100, in 0.1 steps).</summary>
    public double Outer { get; set; } = 100;
    public bool SquareGate { get; set; }
    /// <summary>Vertical output as a percent of horizontal (50–150).</summary>
    public int AxisRatio { get; set; } = 100;
    /// <summary>Anti-deadzone: the least the game gets once the stick leaves the inner dead zone (0–40%), to cancel a
    /// game's own dead zone so small moves register.</summary>
    public double AntiDeadzone { get; set; }
    /// <summary>The most the game gets at full push (60–100%).</summary>
    public double MaxOutput { get; set; } = 100;
    /// <summary>Pure raw 1:1: the stick passes through untouched; its curve, dead zones and the rest are ignored.</summary>
    public bool Raw { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] public bool IsDefault => Inner == 0 && Outer == 100 && !SquareGate && AxisRatio == 100 && AntiDeadzone == 0 && MaxOutput == 100 && !Raw;
}

/// <summary>
/// Turns a physical pad's state into what the game should see. One instance per pad: it keeps the turbo clock and the
/// macro that is playing. Pure logic, no Windows calls, so it is tested directly.
/// </summary>
public sealed class PadProcessor
{
    static readonly string[] Names = [.. PadButtons.Mask.Keys, "LT", "RT"];
    readonly PcModeProfile profile;
    readonly Dictionary<string, long> turboSince = new();
    readonly HashSet<string> previous = new();
    SetupMacro[]? playing; long playingSince;

    public PadProcessor(PcModeProfile profile) { profile.Validate(); this.profile = profile; }
    public bool PlayingMacro => playing != null;

    public PadState Process(PadState input, long nowMs)
    {
        var down = new Dictionary<string, byte>();
        foreach (var (name, mask) in PadButtons.Mask) if ((input.Buttons & mask) != 0) down[name] = 255;
        if (input.LeftTrigger > PadButtons.TriggerThreshold) down["LT"] = input.LeftTrigger;
        if (input.RightTrigger > PadButtons.TriggerThreshold) down["RT"] = input.RightTrigger;

        // a macro starts on the press (not while held) and plays once; its trigger button is kept from the game
        foreach (var (trigger, steps) in profile.Macros)
            if (down.ContainsKey(trigger) && !previous.Contains(trigger) && playing == null && steps.Length > 0) { playing = steps; playingSince = nowMs; }
        var raw = down.Keys.ToHashSet();
        foreach (var trigger in profile.Macros.Keys) down.Remove(trigger);

        var target = new Dictionary<string, byte>();
        foreach (var (source, value) in down)
        {
            string to = profile.Remap.GetValueOrDefault(source, source);if(to=="NONE")continue;
            target[to] = (byte)Math.Max(target.GetValueOrDefault(to), value);
        }
        // a light trigger pull (below the "pressed" threshold) still reaches a trigger target, so feathering survives
        foreach (var (name, value) in new[] { ("LT", input.LeftTrigger), ("RT", input.RightTrigger) })
        {
            if (value == 0 || value > PadButtons.TriggerThreshold || profile.Macros.ContainsKey(name)) continue;
            string to = profile.Remap.GetValueOrDefault(name, name);if(to=="NONE")continue;
            if (to is "LT" or "RT") target[to] = (byte)Math.Max(target.GetValueOrDefault(to), value);
        }

        foreach (var (button, hz) in profile.Turbo)
        {
            if (!target.ContainsKey(button)) { turboSince.Remove(button); continue; }
            long since = turboSince.TryAdd(button, nowMs) ? nowMs : turboSince[button];
            double half = 500.0 / hz;
            if (((long)((nowMs - since) / half) & 1) == 1) target.Remove(button);
        }

        short lx = input.LeftX, ly = input.LeftY, rx = input.RightX, ry = input.RightY;
        (lx, ly) = Stick(lx, ly, Curve(CurveChannel.LeftStick), Tuning("LeftStick"));
        (rx, ry) = Stick(rx, ry, Curve(CurveChannel.RightStick), Tuning("RightStick"));

        if (playing != null)
        {
            var step = StepAt(nowMs - playingSince, out bool holding);
            if (step == null) playing = null;
            else if (holding)
            {
                foreach (var b in step.Buttons) target[b] = 255;
                if (step.Left != StickHeading.Neutral) (lx, ly) = Heading(step.Left);
                if (step.Right != StickHeading.Neutral) (rx, ry) = Heading(step.Right);
            }
        }

        previous.Clear(); foreach (var name in raw) previous.Add(name);

        ushort buttons = 0;
        foreach (var (name, mask) in PadButtons.Mask) if (target.ContainsKey(name)) buttons |= mask;
        byte lt = Trigger(target.GetValueOrDefault("LT"), Curve(CurveChannel.LeftTrigger)), rt = Trigger(target.GetValueOrDefault("RT"), Curve(CurveChannel.RightTrigger));
        return new(buttons, lt, rt, lx, ly, rx, ry);
    }

    StickTuning? Tuning(string stick) => profile.Sticks.TryGetValue(stick, out var s) && !s.IsDefault ? s : null;
    SetupCurve? Curve(CurveChannel channel) => profile.Curves.TryGetValue(channel.ToString(), out var c) && !PcModeProfile.IsDefault(c) ? c : null;

    SetupMacro? StepAt(long elapsed, out bool holding)
    {
        long at = 0;
        foreach (var step in playing!)
        {
            if (elapsed < at + step.HoldMs) { holding = true; return step; }
            at += step.HoldMs;
            if (elapsed < at + step.PauseMs) { holding = false; return step; }
            at += step.PauseMs;
        }
        holding = false; return null;
    }

    static (short, short) Heading(StickHeading h)
    {
        const double d = 0.7071;
        var (x, y) = h switch
        {
            StickHeading.Up => (0.0, 1.0), StickHeading.UpRight => (d, d), StickHeading.Right => (1.0, 0.0), StickHeading.DownRight => (d, -d),
            StickHeading.Down => (0.0, -1.0), StickHeading.DownLeft => (-d, -d), StickHeading.Left => (-1.0, 0.0), StickHeading.UpLeft => (-d, d), _ => (0.0, 0.0),
        };
        return ((short)Math.Round(x * 32767), (short)Math.Round(y * 32767));
    }

    /// <summary>Dead zones then the response curve, applied to how far the stick is pushed and not to each axis, so
    /// diagonals keep their direction.</summary>
    /// <summary>The stick tuning then adds a square gate (diagonals reach full on both axes, as on a square-gated
    /// stick) and an axis ratio (vertical speed against horizontal).</summary>
    internal static (short, short) Stick(short x, short y, SetupCurve? curve, StickTuning? tuning = null)
    {
        if (curve == null && tuning == null || tuning?.Raw == true) return (x, y);
        double nx = x / 32768.0, ny = y / 32768.0, m = Math.Min(1, Math.Sqrt(nx * nx + ny * ny));
        if (m <= 0) return (0, 0);
        double shaped = Response(curve, tuning, m), ux = nx / m, uy = ny / m;
        if (tuning?.SquareGate == true)
        {   // the direction alone, stretched until its larger axis is full: a push to the circular edge at 45° gives (1, 1)
            double length = Math.Sqrt(nx * nx + ny * ny), edge = Math.Max(Math.Abs(nx), Math.Abs(ny)) / length;
            ux = nx / length / edge; uy = ny / length / edge;
        }
        double ratio = (tuning?.AxisRatio ?? 100) / 100.0;
        return (ToShort(Math.Clamp(ux * shaped, -1, 1)), ToShort(Math.Clamp(uy * shaped * ratio, -1, 1)));
    }
    internal static byte Trigger(byte value, SetupCurve? curve) => curve == null ? value : (byte)Math.Clamp(Math.Round(Response(curve, null, value / 255.0) * 255), 0, 255);
    static short ToShort(double v) => (short)Math.Clamp(Math.Round(v * 32767), -32768, 32767);

    /// <summary>Output (0–1) for how far a stick or trigger is pushed (0–1): dead zones, then the curve. The page's
    /// number table shows exactly this.</summary>
    public static double Response(SetupCurve? curve, StickTuning? tuning, double pushed)
    {
        if (tuning?.Raw == true) return Math.Clamp(pushed, 0, 1);
        double inner = (tuning?.Inner ?? curve?.Inner ?? 0) / 100.0, outer = (tuning?.Outer ?? curve?.Outer ?? 100) / 100.0;
        double scaled = Math.Clamp((pushed - inner) / (outer - inner), 0, 1);
        double y = curve == null ? scaled : Through(scaled, curve.X1 / 255.0, curve.Y1 / 255.0, curve.X2 / 255.0, curve.Y2 / 255.0);
        if (tuning == null || y <= 0) return y;
        // anti-deadzone and max output squeeze the curve between a floor and a ceiling; inside the dead zone stays 0
        double floor = tuning.AntiDeadzone / 100.0, ceiling = tuning.MaxOutput / 100.0;
        return floor + (ceiling - floor) * y;
    }

    /// <summary>The smooth line through (0,0), (x1,y1), (x2,y2), (1,1), all 0–1: a monotone cubic, so it never overshoots
    /// between points. The Curves page draws this same line through the two points it lets you drag (owner, 14 Oct 2026:
    /// the graph and what the agent said disagreed, because the page drew through the points and games used them as
    /// Bezier handles).</summary>
    public static double Through(double x, double x1, double y1, double x2, double y2)
    {
        var knots = new[] { (X: 0d, Y: 0d), (X: x1, Y: y1), (X: x2, Y: y2), (X: 1d, Y: 1d) }.OrderBy(p => p.X).GroupBy(p => p.X).Select(g => g.Last()).ToArray();
        var deltas = Enumerable.Range(0, knots.Length - 1).Select(i => (knots[i + 1].Y - knots[i].Y) / (knots[i + 1].X - knots[i].X)).ToArray();
        var slopes = Enumerable.Range(0, knots.Length).Select(i => i == 0 ? deltas[0] : i == knots.Length - 1 ? deltas[^1] : deltas[i - 1] * deltas[i] <= 0 ? 0 : (deltas[i - 1] + deltas[i]) / 2).ToArray();
        for (int i = 0; i < deltas.Length; i++)
        {
            double d = deltas[i]; if (d == 0) { slopes[i] = slopes[i + 1] = 0; continue; }
            double a = slopes[i] / d, b = slopes[i + 1] / d;
            if (a * a + b * b > 9) { double scale = 3 / Math.Sqrt(a * a + b * b); slopes[i] = scale * a * d; slopes[i + 1] = scale * b * d; }
        }
        if (x <= knots[0].X) return Math.Clamp(knots[0].Y, 0, 1); if (x >= knots[^1].X) return Math.Clamp(knots[^1].Y, 0, 1);
        int s = Enumerable.Range(0, knots.Length - 1).First(i => x >= knots[i].X && x < knots[i + 1].X);
        double span = knots[s + 1].X - knots[s].X, t = (x - knots[s].X) / span, t2 = t * t, t3 = t2 * t;
        return Math.Clamp((2 * t3 - 3 * t2 + 1) * knots[s].Y + (t3 - 2 * t2 + t) * span * slopes[s] + (-2 * t3 + 3 * t2) * knots[s + 1].Y + (t3 - t2) * span * slopes[s + 1], 0, 1);
    }
}
