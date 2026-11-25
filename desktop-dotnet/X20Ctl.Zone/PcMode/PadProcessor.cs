using System.Text.Json.Serialization;

namespace X20Ctl.Zone.PcMode;

public enum SocdMode { Off, LastInput, Neutral }

public enum TriggerPreset { Linear, HairTrigger, RacingThrottle, Sniper, DigitalClick }

public enum MacroPlayMode { Once, WhileHeld, Toggle, RepeatN }

/// <summary>Rapid-trigger, force-curve preset, and dual-stage output settings for one trigger (LT or RT).</summary>
public sealed class TriggerTuning
{
    public TriggerPreset Preset { get; set; } = TriggerPreset.Linear;
    public bool RapidTrigger { get; set; }
    public int ActuationPct { get; set; } = 10;
    public int ResetDeltaPct { get; set; } = 5;
    public bool DualStage { get; set; }
    public int HalfPullPct { get; set; } = 45;
    public int FullPullPct { get; set; } = 92;
    public string? FullPullOutput { get; set; }
    [JsonIgnore] public bool IsDefault => Preset == TriggerPreset.Linear && !RapidTrigger && !DualStage && string.IsNullOrEmpty(FullPullOutput);
}

/// <summary>Hold-to-aim precision scaling and optional alternate curve applied to the right stick while held.</summary>
public sealed class SniperModifier
{
    public bool Enabled { get; set; }
    public string ModifierButton { get; set; } = "LT";
    public int ScalePct { get; set; } = 55;
    public SetupCurve? AlternateCurve { get; set; }
    [JsonIgnore] public bool IsDefault => !Enabled;
}

/// <summary>Conditional loop & humanized +/- jitter options per macro trigger button.</summary>
public sealed class MacroOptions
{
    public MacroPlayMode Mode { get; set; } = MacroPlayMode.Once;
    public int RepeatCount { get; set; } = 2;
    public int HumanJitterMs { get; set; } = 0;
    [JsonIgnore] public bool IsDefault => Mode == MacroPlayMode.Once && HumanJitterMs == 0;
}

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
    /// <summary>Front-button combos: one or more buttons, a press type, and a pad button the game gets instead (rework, 6 Nov 2026).</summary>
    public List<ComboRule> Combos { get; set; } = new();
    /// <summary>Keyboard tab: button → one key or mouse button ("K:65", "M:LeftClick"). The game no longer sees that button.</summary>
    public Dictionary<string, string> Keys { get; set; } = new();
    /// <summary>Keyboard tab's custom mappings: button → keys and mouse buttons played in order, once per press. The game no longer sees that button.</summary>
    public Dictionary<string, KeyStep[]> Custom { get; set; } = new();
    /// <summary>A per-game profile's display name (the game's window title when it was added). Not a setting.</summary>
    public string? Name { get; set; }
    /// <summary>How strongly a game's vibration reaches the real pad, in percent (owner, 7 Nov 2026: the Vibration tab sets it in PC mode).</summary>
    public int Vibration { get; set; } = 100;
    /// <summary>Game vibration section (TuskGaming's request, 8 Nov 2026): the large (left) and small (right) motor each on
    /// their own, in percent of what the game asks for, after Vibration.</summary>
    public int VibrationLeft { get; set; } = 100;
    public int VibrationRight { get; set; } = 100;
    /// <summary>The least a motor runs whenever the game rumbles it at all, in percent of full power: faint effects get felt.</summary>
    public int VibrationFloor { get; set; }
    public const int MaxVibration = 200, MaxVibrationFloor = 50;

    /// <summary>SOCD / Snap Tap cleaning for D-Pad and Left Stick.</summary>
    public SocdMode DPadSocd { get; set; } = SocdMode.Off;
    public SocdMode LeftStickSocd { get; set; } = SocdMode.Off;
    /// <summary>LT / RT rapid-trigger, preset curves, and dual-stage configuration.</summary>
    public Dictionary<string, TriggerTuning> Triggers { get; set; } = new();
    /// <summary>Hold-to-aim precision modifier for right stick.</summary>
    public SniperModifier? Sniper { get; set; }
    /// <summary>Per-trigger macro playback mode and jitter options.</summary>
    public Dictionary<string, MacroOptions> MacroOptions { get; set; } = new();
    /// <summary>PC-side virtual pad report rate cap in Hz (0 = uncapped, 250, 500, 750, 1000).</summary>
    public int OutputRateHz { get; set; }

    /// <summary>What the real pad gets for a game's motor value (0-255): scaled, lifted to the floor, never past full power.</summary>
    public int ShapeRumble(int game, bool left)
    {
        if (game <= 0) return 0;
        long level = (long)game * 257 * Vibration / 100 * (left ? VibrationLeft : VibrationRight) / 100;
        if (level <= 0) return 0;
        return (int)Math.Min(65535, Math.Max(level, VibrationFloor * 65535L / 100));
    }

    public bool IsNeutral => Remap.Count == 0 && Keys.Count == 0 && Custom.Count == 0 && Combos.Count == 0 && Turbo.Count == 0 && Macros.Count == 0
        && Curves.Values.All(IsDefault) && Sticks.Values.All(s => s.IsDefault)
        && DPadSocd == SocdMode.Off && LeftStickSocd == SocdMode.Off
        && Triggers.Values.All(t => t.IsDefault)
        && (Sniper == null || Sniper.IsDefault)
        && MacroOptions.Values.All(o => o.IsDefault)
        && OutputRateHz == 0
        && Vibration == 100 && VibrationLeft == 100 && VibrationRight == 100 && VibrationFloor == 0;
    public static bool IsDefault(SetupCurve c) => c.Inner == 0 && c.Outer == 100 && c.X1 == 85 && c.Y1 == 85 && c.X2 == 170 && c.Y2 == 170;
    public const int MinTurboHz = 2, MaxTurboHz = 30;

    public void Validate()
    {
        if (Name is { Length: > 120 }) throw new InvalidDataException("Name too long.");
        if (Remap == null || Curves == null || Turbo == null || Macros == null || Sticks == null || Combos == null) throw new InvalidDataException("Missing settings.");
        if (Vibration is < 0 or > MaxVibration || VibrationLeft is < 0 or > MaxVibration || VibrationRight is < 0 or > MaxVibration) throw new InvalidDataException("Vibration must be 0 to 200%.");
        if (VibrationFloor is < 0 or > MaxVibrationFloor) throw new InvalidDataException("The vibration minimum must be 0 to 50%.");
        if (Keys == null || Custom == null || Keys.Count > 64 || Custom.Count > 32) throw new InvalidDataException("Too many keyboard settings.");
        foreach (var (button, output) in Keys)
            if (!Known(button) || !KeyCatalog.IsValid(output)) throw new InvalidDataException("Unknown key in a keyboard mapping.");
        foreach (var (button, steps) in Custom)
            if (!Known(button) || steps == null || steps.Length is < 1 or > 64 || steps.Any(s => s == null || !KeyCatalog.IsValid(s.Output) || s.HoldMs is < 10 or > 10000 || s.PauseMs is < 0 or > 10000))
                throw new InvalidDataException("Invalid custom mapping.");
        if (Combos.Count > 64) throw new InvalidDataException("Too many combos.");
        foreach (var c in Combos)
            if (c == null || string.IsNullOrWhiteSpace(c.Id) || c.Buttons == null || c.Buttons.Length is < 1 or > 4 || c.Buttons.Any(b => b == null || !PadButtons.Mask.ContainsKey(b))
                || !Enum.IsDefined(c.Kind) || !Enum.IsDefined(c.Mode) || c.Output == null || !(PadButtons.Mask.ContainsKey(c.Output) || c.Output == "NONE" || ComboRule.IsAction(c.Output)))
                throw new InvalidDataException("Invalid combo.");
        if (Combos.Select(c => c.Id).Distinct().Count() != Combos.Count) throw new InvalidDataException("Duplicate combo.");
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
        if (!Enum.IsDefined(DPadSocd) || !Enum.IsDefined(LeftStickSocd)) throw new InvalidDataException("Invalid SOCD mode.");
        if (OutputRateHz is not (0 or 250 or 500 or 750 or 1000)) throw new InvalidDataException("Output rate must be uncapped or 250, 500, 750, or 1000 Hz.");
        if (Triggers != null)
        {
            foreach (var (trig, tt) in Triggers)
            {
                if (trig is not ("LT" or "RT") || tt == null || !Enum.IsDefined(tt.Preset)
                    || tt.ActuationPct is < 1 or > 90 || tt.ResetDeltaPct is < 1 or > 50
                    || tt.HalfPullPct is < 10 or > 80 || tt.FullPullPct is < 50 or > 100 || tt.FullPullPct <= tt.HalfPullPct)
                    throw new InvalidDataException("Invalid trigger tuning.");
                if (!string.IsNullOrEmpty(tt.FullPullOutput) && !(Known(tt.FullPullOutput) || KeyCatalog.IsValid(tt.FullPullOutput)))
                    throw new InvalidDataException("Unknown dual-stage trigger output.");
            }
        }
        if (Sniper != null && (!Known(Sniper.ModifierButton) || Sniper.ScalePct is < 15 or > 100))
            throw new InvalidDataException("Invalid sniper modifier.");
        if (MacroOptions != null)
        {
            foreach (var (trig, mo) in MacroOptions)
                if (!Known(trig) || mo == null || !Enum.IsDefined(mo.Mode) || mo.RepeatCount is < 1 or > 99 || mo.HumanJitterMs is < 0 or > 30)
                    throw new InvalidDataException("Invalid macro options.");
        }
    }
    static bool Known(string name) => name is "LT" or "RT" || PadButtons.Mask.ContainsKey(name);
}

/// <summary>One front-button combo. The buttons are consumed while they are all held, so the game never sees them as themselves.</summary>
public sealed class ComboRule
{
    public string Id { get; set; } = "";
    public string[] Buttons { get; set; } = [];
    public PressKind Kind { get; set; } = PressKind.Press;
    public ChordMode Mode { get; set; } = ChordMode.Instant;
    /// <summary>The pad button the game gets, NONE to just swallow the buttons, or "ACT:&lt;name&gt;" for a keyboard or mouse action (a Mouse tab action name).</summary>
    public string Output { get; set; } = "NONE";
    public const string ActionPrefix = "ACT:";
    public static bool IsAction(string output) => output.StartsWith(ActionPrefix) && Enum.TryParse<X20Ctl.Zone.Mouse.MouseAction>(output[ActionPrefix.Length..], out var a) && a != X20Ctl.Zone.Mouse.MouseAction.None && Enum.IsDefined(a);
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
    /// <summary>Continuously estimates tiny resting bias (&lt;= 6%) and recenters without raising dead zones.</summary>
    public bool AutoCenterDrift { get; set; }
    /// <summary>Pure raw 1:1: the stick passes through untouched; its curve, dead zones and the rest are ignored.</summary>
    public bool Raw { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] public bool IsDefault => Inner == 0 && Outer == 100 && !SquareGate && AxisRatio == 100 && AntiDeadzone == 0 && MaxOutput == 100 && !AutoCenterDrift && !Raw;
}

/// <summary>
/// Turns a physical pad's state into what the game should see. One instance per pad: it keeps the turbo clock and the
/// macro that is playing. Pure logic, no Windows calls, so it is tested directly.
/// </summary>
public sealed class PadProcessor
{
    static readonly string[] Names = [.. PadButtons.Mask.Keys, "LT", "RT"];
    readonly PcModeProfile profile;
    readonly Func<int, int, int> jitterRng;
    readonly Dictionary<string, long> turboSince = new();
    readonly HashSet<string> previous = new();
    SetupMacro[]? playing; long playingSince; string? playingTrigger; int playingRemainingLoops; SetupMacro[]? jitteredPlaying;
    readonly HashSet<string> toggledMacros = new();
    ChordEngine? combos; readonly Dictionary<string, long> comboPulse = new(); readonly HashSet<string> comboHeld = new();
    const int ComboPulseMs = 60;
    readonly List<(string Action, bool Down)> actions = new();

    // SOCD state tracking for D-Pad and Left Stick
    int dpadLastH, dpadLastV; ushort prevDpad;
    int stickLastH, stickLastV; double prevStickX, prevStickY;

    // Rapid-trigger & dual-stage state
    sealed class TriggerState { public bool Actuated; public byte MinSeen = 255, MaxSeen; public bool DualKeyHeld; }
    readonly TriggerState ltState = new(), rtState = new();

    // Drift auto-compensator state (capped at <= 6%)
    sealed class DriftState { public double BiasX, BiasY, LastX, LastY; public int RestCount; }
    readonly DriftState leftDrift = new(), rightDrift = new();

    /// <summary>Keyboard and mouse actions the combos asked for since the last call, in order. The caller sends them to Windows.</summary>
    public IReadOnlyList<(string Action, bool Down)> TakeActions() { var copy = actions.ToArray(); actions.Clear(); return copy; }

    public PadProcessor(PcModeProfile profile, Func<int, int, int>? jitterRng = null)
    {
        profile.Validate();
        this.profile = profile;
        this.jitterRng = jitterRng ?? ((min, max) => Random.Shared.Next(min, max + 1));
    }
    public bool PlayingMacro => playing != null;

    public static PadState MergeCoPilot(PadState primary, PadState secondary)
    {
        ushort buttons = (ushort)(primary.Buttons | secondary.Buttons);
        byte lt = Math.Max(primary.LeftTrigger, secondary.LeftTrigger);
        byte rt = Math.Max(primary.RightTrigger, secondary.RightTrigger);
        static (short X, short Y) PickStick(short ax, short ay, short bx, short by)
        {
            double am = (double)ax * ax + (double)ay * ay;
            double bm = (double)bx * bx + (double)by * by;
            if (am > 9000000 && bm > 9000000)
                return ((short)Math.Clamp(ax + bx, -32768, 32767), (short)Math.Clamp(ay + by, -32768, 32767));
            return bm > am ? (bx, by) : (ax, ay);
        }
        var (lx, ly) = PickStick(primary.LeftX, primary.LeftY, secondary.LeftX, secondary.LeftY);
        var (rx, ry) = PickStick(primary.RightX, primary.RightY, secondary.RightX, secondary.RightY);
        return new(buttons, lt, rt, lx, ly, rx, ry);
    }

    public PadState Process(PadState input, long nowMs)
    {
        ushort cleanButtons = CleanDpadSocd(input.Buttons);
        var down = new Dictionary<string, byte>();
        foreach (var (name, mask) in PadButtons.Mask) if ((cleanButtons & mask) != 0) down[name] = 255;
        if (input.LeftTrigger > PadButtons.TriggerThreshold) down["LT"] = input.LeftTrigger;
        if (input.RightTrigger > PadButtons.TriggerThreshold) down["RT"] = input.RightTrigger;

        var raw = down.Keys.ToHashSet();

        // Conditional macros: Once, WhileHeld, Toggle, RepeatN + optional humanized jitter
        foreach (var (trigger, steps) in profile.Macros)
        {
            if (steps.Length == 0) continue;
            bool isDown = raw.Contains(trigger), wasDown = previous.Contains(trigger);
            var opts = profile.MacroOptions.GetValueOrDefault(trigger);
            var mode = opts?.Mode ?? MacroPlayMode.Once;

            if (mode == MacroPlayMode.Toggle)
            {
                if (isDown && !wasDown)
                {
                    if (!toggledMacros.Add(trigger))
                    {
                        toggledMacros.Remove(trigger);
                        if (playingTrigger == trigger) { playing = null; jitteredPlaying = null; playingTrigger = null; }
                    }
                    else if (playing == null)
                    {
                        StartMacro(trigger, steps, 1, opts?.HumanJitterMs ?? 0, nowMs);
                    }
                }
            }
            else if (isDown && !wasDown && playing == null)
            {
                int loops = mode == MacroPlayMode.RepeatN ? Math.Clamp(opts?.RepeatCount ?? 1, 1, 99) : 1;
                StartMacro(trigger, steps, loops, opts?.HumanJitterMs ?? 0, nowMs);
            }
            if (mode == MacroPlayMode.WhileHeld && !isDown && playingTrigger == trigger)
            {
                playing = null; jitteredPlaying = null; playingTrigger = null;
            }
        }
        foreach (var trigger in profile.Macros.Keys) down.Remove(trigger);
        KeyboardOutputs(down, nowMs);
        var comboOut = ApplyCombos(cleanButtons, nowMs, down);

        var target = new Dictionary<string, byte>();
        foreach (var (source, value) in down)
        {
            string to = profile.Remap.GetValueOrDefault(source, source);if(to=="NONE")continue;
            target[to] = (byte)Math.Max(target.GetValueOrDefault(to), value);
        }
        // a light trigger pull (below the "pressed" threshold) still reaches a trigger target, so feathering survives
        foreach (var (name, value) in new[] { ("LT", input.LeftTrigger), ("RT", input.RightTrigger) })
        {
            if (value == 0 || value > PadButtons.TriggerThreshold || profile.Macros.ContainsKey(name) || profile.Keys.ContainsKey(name) || profile.Custom.ContainsKey(name)) continue;
            string to = profile.Remap.GetValueOrDefault(name, name);if(to=="NONE")continue;
            if (to is "LT" or "RT") target[to] = (byte)Math.Max(target.GetValueOrDefault(to), value);
        }

        foreach (var button in comboOut) target[button] = 255;
        foreach (var (button, hz) in profile.Turbo)
        {
            if (!target.ContainsKey(button)) { turboSince.Remove(button); continue; }
            long since = turboSince.TryAdd(button, nowMs) ? nowMs : turboSince[button];
            double half = 500.0 / hz;
            if (((long)((nowMs - since) / half) & 1) == 1) target.Remove(button);
        }

        var lTune = Tuning("LeftStick");
        var rTune = Tuning("RightStick");
        (short rawLx, short rawLy) = CleanStickSocd(input.LeftX, input.LeftY);
        if (lTune?.AutoCenterDrift == true) (rawLx, rawLy) = CompensateDrift(rawLx, rawLy, leftDrift);
        short rInX = input.RightX, rInY = input.RightY;
        if (rTune?.AutoCenterDrift == true) (rInX, rInY) = CompensateDrift(rInX, rInY, rightDrift);

        (short lx, short ly) = Stick(rawLx, rawLy, Curve(CurveChannel.LeftStick), lTune);
        var rCurve = Curve(CurveChannel.RightStick);
        bool sniperActive = profile.Sniper is { Enabled: true } sn && raw.Contains(sn.ModifierButton);
        if (sniperActive && profile.Sniper!.AlternateCurve is { } altCurve)
            rCurve = altCurve;
        (short rx, short ry) = Stick(rInX, rInY, rCurve, rTune);
        if (sniperActive)
        {
            double s = Math.Clamp(profile.Sniper!.ScalePct / 100.0, 0.15, 1.0);
            rx = ToShort(rx / 32767.0 * s);
            ry = ToShort(ry / 32767.0 * s);
        }

        if (playing != null)
        {
            var seq = jitteredPlaying ?? playing;
            var step = StepAt(seq, nowMs - playingSince, out bool holding);
            if (step == null)
            {
                var finishedTrigger = playingTrigger!;
                var opts = profile.MacroOptions.GetValueOrDefault(finishedTrigger);
                var mode = opts?.Mode ?? MacroPlayMode.Once;
                bool keepWhileHeld = mode == MacroPlayMode.WhileHeld && raw.Contains(finishedTrigger);
                bool keepToggle = mode == MacroPlayMode.Toggle && toggledMacros.Contains(finishedTrigger);
                bool keepRepeat = mode == MacroPlayMode.RepeatN && playingRemainingLoops > 1;
                if (keepWhileHeld || keepToggle || keepRepeat)
                {
                    int nextLoops = keepRepeat ? playingRemainingLoops - 1 : 1;
                    StartMacro(finishedTrigger, playing, nextLoops, opts?.HumanJitterMs ?? 0, nowMs);
                    step = StepAt(jitteredPlaying ?? playing, 0, out holding);
                }
                else
                {
                    playing = null; jitteredPlaying = null; playingTrigger = null;
                }
            }
            if (playing != null && step != null && holding)
            {
                foreach (var b in step.Buttons) target[b] = 255;
                if (step.Left != StickHeading.Neutral) (lx, ly) = Heading(step.Left);
                if (step.Right != StickHeading.Neutral) (rx, ry) = Heading(step.Right);
            }
        }

        byte ltRaw = target.GetValueOrDefault("LT");
        byte rtRaw = target.GetValueOrDefault("RT");
        byte lt = ShapeTrigger("LT", ltRaw, Curve(CurveChannel.LeftTrigger), profile.Triggers.GetValueOrDefault("LT"), ltState, target);
        byte rt = ShapeTrigger("RT", rtRaw, Curve(CurveChannel.RightTrigger), profile.Triggers.GetValueOrDefault("RT"), rtState, target);

        previous.Clear(); foreach (var name in raw) previous.Add(name);

        ushort buttons = 0;
        foreach (var (name, mask) in PadButtons.Mask) if (target.ContainsKey(name)) buttons |= mask;
        return new(buttons, lt, rt, lx, ly, rx, ry);
    }

    void StartMacro(string trigger, SetupMacro[] steps, int loops, int jitterMs, long nowMs)
    {
        playing = steps;
        playingTrigger = trigger;
        playingRemainingLoops = loops;
        playingSince = nowMs;
        if (jitterMs > 0)
        {
            jitteredPlaying = steps.Select(s => s with
            {
                HoldMs = s.HoldMs > 0 ? Math.Clamp(s.HoldMs + jitterRng(-jitterMs, jitterMs), 5, 10000) : 0,
                PauseMs = s.PauseMs > 0 ? Math.Clamp(s.PauseMs + jitterRng(-jitterMs, jitterMs), 5, 10000) : 0,
            }).ToArray();
        }
        else
        {
            jitteredPlaying = steps;
        }
    }

    ushort CleanDpadSocd(ushort buttons)
    {
        if (profile.DPadSocd == SocdMode.Off) { prevDpad = buttons; return buttons; }
        bool left = (buttons & PadButtons.Left) != 0, right = (buttons & PadButtons.Right) != 0;
        bool wasL = (prevDpad & PadButtons.Left) != 0, wasR = (prevDpad & PadButtons.Right) != 0;
        if (left && !wasL) dpadLastH = -1; else if (right && !wasR) dpadLastH = 1;
        else if (!left && !right) dpadLastH = 0;

        bool up = (buttons & PadButtons.Up) != 0, down = (buttons & PadButtons.Down) != 0;
        bool wasU = (prevDpad & PadButtons.Up) != 0, wasD = (prevDpad & PadButtons.Down) != 0;
        if (up && !wasU) dpadLastV = 1; else if (down && !wasD) dpadLastV = -1;
        else if (!up && !down) dpadLastV = 0;
        prevDpad = buttons;

        if (left && right)
        {
            buttons = (ushort)(buttons & ~(PadButtons.Left | PadButtons.Right));
            if (profile.DPadSocd == SocdMode.LastInput)
                buttons |= dpadLastH >= 0 ? PadButtons.Right : PadButtons.Left;
        }
        if (up && down)
        {
            buttons = (ushort)(buttons & ~(PadButtons.Up | PadButtons.Down));
            if (profile.DPadSocd == SocdMode.LastInput)
                buttons |= dpadLastV >= 0 ? PadButtons.Up : PadButtons.Down;
        }
        return buttons;
    }

    (short X, short Y) CleanStickSocd(short x, short y)
    {
        if (profile.LeftStickSocd == SocdMode.Off) return (x, y);
        const double thresh = 0.35;
        double nx = x / 32768.0, ny = y / 32768.0;
        if (Math.Abs(nx) > thresh && Math.Sign(nx) != Math.Sign(prevStickX)) stickLastH = Math.Sign(nx);
        if (Math.Abs(ny) > thresh && Math.Sign(ny) != Math.Sign(prevStickY)) stickLastV = Math.Sign(ny);
        prevStickX = nx; prevStickY = ny;
        // If opposing digital clean was triggered quickly across zero in one poll, Neutral or LastInput holds cleanly
        if (Math.Abs(nx) < 0.08) stickLastH = 0;
        if (Math.Abs(ny) < 0.08) stickLastV = 0;
        if (profile.LeftStickSocd == SocdMode.Neutral && (Math.Abs(nx) > thresh && stickLastH != 0 && Math.Sign(nx) != stickLastH))
            x = 0;
        return (x, y);
    }

    static (short X, short Y) CompensateDrift(short x, short y, DriftState drift)
    {
        double nx = x / 32768.0, ny = y / 32768.0;
        double r = Math.Sqrt(nx * nx + ny * ny);
        double delta = Math.Sqrt((nx - drift.LastX) * (nx - drift.LastX) + (ny - drift.LastY) * (ny - drift.LastY));
        drift.LastX = nx; drift.LastY = ny;
        if (r < 0.08 && delta < 0.015)
        {
            drift.RestCount++;
            double alpha = drift.RestCount < 40 ? 0.02 : 0.04;
            drift.BiasX += (nx - drift.BiasX) * alpha;
            drift.BiasY += (ny - drift.BiasY) * alpha;
            double bMag = Math.Sqrt(drift.BiasX * drift.BiasX + drift.BiasY * drift.BiasY);
            if (bMag > 0.06) { drift.BiasX *= 0.06 / bMag; drift.BiasY *= 0.06 / bMag; }
        }
        if (drift.RestCount < 20) return (x, y);
        return (ToShort(Math.Clamp(nx - drift.BiasX, -1, 1)), ToShort(Math.Clamp(ny - drift.BiasY, -1, 1)));
    }

    byte ShapeTrigger(string name, byte value, SetupCurve? curve, TriggerTuning? tuning, TriggerState st, Dictionary<string, byte> target)
    {
        byte shaped = Trigger(value, curve);
        if (tuning == null || tuning.IsDefault) return shaped;

        double norm = shaped / 255.0;
        norm = tuning.Preset switch
        {
            TriggerPreset.HairTrigger => norm < 0.02 ? 0.0 : Math.Clamp((norm - 0.02) / 0.35, 0.0, 1.0),
            TriggerPreset.RacingThrottle => Math.Pow(norm, 1.6),
            TriggerPreset.Sniper => norm < 0.35 ? 0.0 : Math.Clamp((norm - 0.35) / 0.65, 0.0, 1.0),
            TriggerPreset.DigitalClick => norm >= 0.15 ? 1.0 : 0.0,
            _ => norm,
        };
        shaped = (byte)Math.Clamp(Math.Round(norm * 255.0), 0, 255);

        if (tuning.RapidTrigger)
        {
            int actDelta = Math.Max(3, (int)Math.Round(tuning.ActuationPct * 2.55));
            int relDelta = Math.Max(3, (int)Math.Round(tuning.ResetDeltaPct * 2.55));
            if (value == 0) { st.Actuated = false; st.MinSeen = 255; st.MaxSeen = 0; }
            if (!st.Actuated)
            {
                if (value < st.MinSeen) st.MinSeen = value;
                if (value - st.MinSeen >= actDelta || value >= actDelta + 5)
                {
                    st.Actuated = true;
                    st.MaxSeen = value;
                }
            }
            else
            {
                if (value > st.MaxSeen) st.MaxSeen = value;
                if (st.MaxSeen - value >= relDelta || value == 0)
                {
                    st.Actuated = false;
                    st.MinSeen = value;
                }
            }
            shaped = st.Actuated ? (byte)255 : (byte)0;
        }

        if (tuning.DualStage && !string.IsNullOrEmpty(tuning.FullPullOutput))
        {
            double pct = value / 2.55;
            bool full = st.DualKeyHeld ? pct >= tuning.FullPullPct - 5 : pct >= tuning.FullPullPct;
            if (PadButtons.Mask.ContainsKey(tuning.FullPullOutput))
            {
                st.DualKeyHeld = full;
                if (full) target[tuning.FullPullOutput] = 255;
            }
            else if (full != st.DualKeyHeld)
            {
                st.DualKeyHeld = full;
                actions.Add((tuning.FullPullOutput, full));
            }
        }
        return shaped;
    }

    readonly HashSet<string> keyHeld = new();
    readonly Dictionary<string, (KeyStep[] Steps, long Since, int Sent)> running = new();

    /// <summary>The Keyboard tab: a mapped button holds its key (or mouse button) for as long as it is held; a custom
    /// mapping plays its steps once per press. Either way the game no longer sees the button.</summary>
    void KeyboardOutputs(Dictionary<string, byte> down, long nowMs)
    {
        foreach (var (button, output) in profile.Keys)
        {
            bool now = down.Remove(button), was = keyHeld.Contains(button);
            if (now && !was) { keyHeld.Add(button); actions.Add((output, true)); }
            else if (!now && was) { keyHeld.Remove(button); actions.Add((output, false)); }
        }
        foreach (var (button, steps) in profile.Custom)
            if (down.Remove(button) && !previous.Contains(button) && !running.ContainsKey(button)) running[button] = (steps, nowMs, 0);
        foreach (var button in running.Keys.ToList())
        {
            var (steps, since, sent) = running[button];
            long elapsed = nowMs - since, at = 0; int ev = 0;
            foreach (var step in steps)
            {
                if (ev == sent && elapsed >= at) { actions.Add((step.Output, true)); sent++; }
                ev++; at += step.HoldMs;
                if (ev == sent && elapsed >= at) { actions.Add((step.Output, false)); sent++; }
                ev++; at += step.PauseMs;
            }
            if (sent >= steps.Length * 2) running.Remove(button); else running[button] = (steps, since, sent);
        }
    }

    /// <summary>PC mode is stopping or pausing: lets go of every key and mouse button still held, so nothing stays stuck.</summary>
    public IReadOnlyList<(string Action, bool Down)> ReleaseAll()
    {
        var ups = new List<(string, bool)>();
        foreach (var button in keyHeld) if (profile.Keys.TryGetValue(button, out var output)) ups.Add((output, false));
        foreach (var (steps, _, sent) in running.Values) if (sent % 2 == 1) ups.Add((steps[sent / 2].Output, false));
        if (ltState.DualKeyHeld && profile.Triggers.TryGetValue("LT", out var ltCfg) && !string.IsNullOrEmpty(ltCfg.FullPullOutput) && !PadButtons.Mask.ContainsKey(ltCfg.FullPullOutput))
            ups.Add((ltCfg.FullPullOutput, false));
        if (rtState.DualKeyHeld && profile.Triggers.TryGetValue("RT", out var rtCfg) && !string.IsNullOrEmpty(rtCfg.FullPullOutput) && !PadButtons.Mask.ContainsKey(rtCfg.FullPullOutput))
            ups.Add((rtCfg.FullPullOutput, false));
        ltState.DualKeyHeld = rtState.DualKeyHeld = false;
        toggledMacros.Clear(); playing = null; jitteredPlaying = null; playingTrigger = null;
        keyHeld.Clear(); running.Clear(); actions.Clear();
        return ups;
    }

    /// <summary>Runs the combos: removes their buttons from what passes through and returns the pad buttons they output now.</summary>
    List<string> ApplyCombos(ushort buttons, long nowMs, Dictionary<string, byte> down)
    {
        var result = new List<string>();
        if (profile.Combos.Count == 0) return result;
        combos ??= new ChordEngine(profile.Combos.Select(c => new ChordBinding(c.Id, c.Buttons.Aggregate(0u, (m, b) => m | PadButtons.Mask[b]), c.Kind, c.Mode)).ToList());
        foreach (var ev in combos.Update(nowMs, buttons))
        {
            var rule = profile.Combos.First(c => c.Id == ev.Id);
            bool act = ComboRule.IsAction(rule.Output);
            string name = act ? rule.Output[ComboRule.ActionPrefix.Length..] : "";
            if (ev.Phase == ChordPhase.Down) { if (act) actions.Add((name, true)); else comboHeld.Add(rule.Id); }
            else if (ev.Phase == ChordPhase.Up) { if (act) actions.Add((name, false)); else comboHeld.Remove(rule.Id); }
            else if (act) { actions.Add((name, true)); actions.Add((name, false)); }
            else comboPulse[rule.Id] = nowMs + ComboPulseMs;
        }
        foreach (var rule in profile.Combos)
        {
            bool all = rule.Buttons.All(b => (buttons & PadButtons.Mask[b]) != 0);
            if (all) foreach (var b in rule.Buttons) down.Remove(b); // consumed while held together
            bool pulsing = comboPulse.TryGetValue(rule.Id, out var until) && nowMs < until;
            if (!pulsing) comboPulse.Remove(rule.Id);
            if ((comboHeld.Contains(rule.Id) || pulsing) && PadButtons.Mask.ContainsKey(rule.Output)) result.Add(rule.Output);
        }
        return result;
    }

    StickTuning? Tuning(string stick) => profile.Sticks.TryGetValue(stick, out var s) && !s.IsDefault ? s : null;
    SetupCurve? Curve(CurveChannel channel) => profile.Curves.TryGetValue(channel.ToString(), out var c) && !PcModeProfile.IsDefault(c) ? c : null;

    static SetupMacro? StepAt(SetupMacro[] seq, long elapsed, out bool holding)
    {
        long at = 0;
        foreach (var step in seq)
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
