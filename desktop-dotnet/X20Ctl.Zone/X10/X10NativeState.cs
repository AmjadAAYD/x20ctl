using X20Ctl.Zone.PcMode;

namespace X20Ctl.Zone.X10;

public sealed record X10HardwareSnapshot(
    string DeviceName,
    Krly.Version Version,
    Krly.Battery? Battery,
    Krly.Settings Settings,
    IReadOnlyDictionary<string, string> FaceMappings,
    IReadOnlyDictionary<string, Krly.PaddleAssignment> Paddles)
{
    public static X10HardwareSnapshot FromRaw(
        string deviceName,
        Krly.Version version,
        Krly.Battery? battery,
        Krly.Settings settings,
        IReadOnlyDictionary<string, string> mappings0x50,
        IReadOnlyDictionary<string, Krly.PaddleAssignment> macros0x79)
    {
        var face = new Dictionary<string, string>(StringComparer.Ordinal);
        var paddles = new Dictionary<string, Krly.PaddleAssignment>(StringComparer.Ordinal);

        foreach (var (source, target) in mappings0x50)
        {
            if (Krly.RearPaddles.Contains(source))
                paddles[source] = new Krly.PaddleAssignment(source, Krly.PaddleSlotKind.Passthrough, target, false, []);
            else
                face[source] = target;
        }

        // Query 0x79 is authoritative for rear paddles and overrides any legacy M1/M2 rows from 0x50.
        foreach (var (slot, assignment) in macros0x79)
            paddles[slot] = assignment;

        return new X10HardwareSnapshot(deviceName, version, battery, settings, face, paddles);
    }
}

public sealed record X10HardwareProfile(
    Krly.Settings Settings,
    IReadOnlyDictionary<string, string> FaceMappings,
    IReadOnlyList<Krly.PaddleAssignment> Paddles);

public sealed record X10SyncReport(
    Krly.VerificationStatus Status,
    IReadOnlyList<string> DifferingGroups,
    X10HardwareSnapshot? ReadBack);

/// <summary>Holds X10-native controller configuration not covered by standard drafts: hardware turbo and rear paddle mode/passthrough.</summary>
public sealed class X10NativeDraft
{
    public static readonly string[] PaddleSlots = ["M1", "M2"];
    public static readonly string[] TurboKeys = ["A", "B", "X", "Y", "LB", "RB", "LT", "RT", "L3", "R3", "DPAD_UP", "DPAD_DOWN", "DPAD_LEFT", "DPAD_RIGHT"];

    private readonly Dictionary<string, Krly.TurboEntry> turbo = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Krly.PaddleSlotKind> paddleModes = new(StringComparer.Ordinal) { ["M1"] = Krly.PaddleSlotKind.Passthrough, ["M2"] = Krly.PaddleSlotKind.Passthrough };
    private readonly Dictionary<string, string> paddleTargets = new(StringComparer.Ordinal) { ["M1"] = "A", ["M2"] = "B" };
    private readonly Dictionary<string, bool> paddleRepeat = new(StringComparer.Ordinal) { ["M1"] = false, ["M2"] = false };

    public IReadOnlyList<Krly.TurboEntry> Turbo => TurboKeys.Where(turbo.ContainsKey).Select(k => turbo[k]).ToArray();

    public Krly.TurboEntry? GetTurbo(string key) => turbo.GetValueOrDefault(key);

    public void SetTurbo(string key, bool enabled, bool automatic, int rate)
    {
        if (!TurboKeys.Contains(key)) throw new ArgumentException($"Unsupported turbo key '{key}'.", nameof(key));
        if (!enabled) { turbo.Remove(key); return; }
        turbo[key] = new Krly.TurboEntry(key, automatic, Math.Clamp(rate, 1, 20));
    }

    public void ReplaceTurbo(IEnumerable<Krly.TurboEntry> entries)
    {
        turbo.Clear();
        foreach (var e in entries)
            if (TurboKeys.Contains(e.Key))
                turbo[e.Key] = new Krly.TurboEntry(e.Key, e.Automatic, Math.Clamp(e.Rate, 1, 20));
    }

    public void ClearTurbo() => turbo.Clear();

    public Krly.PaddleSlotKind PaddleMode(string slot) => paddleModes.GetValueOrDefault(slot, Krly.PaddleSlotKind.Default);
    public string PaddleTarget(string slot) => paddleTargets.GetValueOrDefault(slot, slot == "M2" ? "B" : "A");
    public bool PaddleRepeat(string slot) => paddleRepeat.GetValueOrDefault(slot, false);

    public void SetPaddlePassthrough(string slot, string targetKey)
    {
        ValidateSlot(slot);
        if (!Krly.Keys.ContainsKey(targetKey) || Krly.RearPaddles.Contains(targetKey))
            throw new ArgumentException($"Invalid paddle target '{targetKey}'.", nameof(targetKey));
        paddleModes[slot] = Krly.PaddleSlotKind.Passthrough;
        paddleTargets[slot] = targetKey;
    }

    public void SetPaddleMacro(string slot, bool repeat)
    {
        ValidateSlot(slot);
        paddleModes[slot] = Krly.PaddleSlotKind.Macro;
        paddleRepeat[slot] = repeat;
    }

    public void SetPaddleDisabled(string slot)
    {
        ValidateSlot(slot);
        paddleModes[slot] = Krly.PaddleSlotKind.Disabled;
    }

    public void Reset()
    {
        turbo.Clear();
        paddleModes["M1"] = Krly.PaddleSlotKind.Passthrough;
        paddleModes["M2"] = Krly.PaddleSlotKind.Passthrough;
        paddleTargets["M1"] = "A";
        paddleTargets["M2"] = "B";
        paddleRepeat["M1"] = false;
        paddleRepeat["M2"] = false;
    }

    static void ValidateSlot(string slot)
    {
        if (!PaddleSlots.Contains(slot)) throw new ArgumentException($"Unknown X10 rear paddle '{slot}'.", nameof(slot));
    }
}

public static class X10ProfileBridge
{
    public static void LoadIntoDrafts(
        X10HardwareSnapshot snapshot,
        ButtonDraft buttons,
        CurveDraft curves,
        MacroDraft macros,
        VibrationDraft vibration,
        X10NativeDraft x10)
    {
        buttons.ResetAll();
        foreach (var (source, target) in snapshot.FaceMappings)
        {
            if (ButtonDraft.Sources.Contains(source) && ButtonDraft.Targets.Contains(target))
                buttons.Set(source, target);
        }

        LoadTriggerCurve(curves, CurveChannel.LeftTrigger, snapshot.Settings.LeftTrigger);
        LoadTriggerCurve(curves, CurveChannel.RightTrigger, snapshot.Settings.RightTrigger);
        LoadStickCurve(curves, CurveChannel.LeftStick, snapshot.Settings.LeftStick);
        LoadStickCurve(curves, CurveChannel.RightStick, snapshot.Settings.RightStick);

        vibration.SetMotors(snapshot.Settings.LeftMotor, snapshot.Settings.RightMotor, 0);
        x10.ReplaceTurbo(snapshot.Settings.Turbo);

        foreach (var slot in X10NativeDraft.PaddleSlots)
        {
            macros.Clear(slot);
            if (!snapshot.Paddles.TryGetValue(slot, out var assignment))
            {
                x10.SetPaddlePassthrough(slot, slot == "M2" ? "B" : "A");
                continue;
            }
            switch (assignment.Kind)
            {
                case Krly.PaddleSlotKind.Disabled:
                    x10.SetPaddleDisabled(slot);
                    break;
                case Krly.PaddleSlotKind.Passthrough when assignment.TargetKey != null:
                    x10.SetPaddlePassthrough(slot, assignment.TargetKey);
                    break;
                case Krly.PaddleSlotKind.Macro:
                    x10.SetPaddleMacro(slot, assignment.Repeat);
                    var imported = assignment.Steps
                        .Where(s => MacroDraft.Inputs.Contains(s.Key))
                        .Select(s =>
                        {
                            int hold = Math.Clamp(((s.HoldMs + 2) / 5) * 5, 5, 65535);
                            int pause = Math.Clamp(((s.WaitMs + 2) / 5) * 5, 0, 65535);
                            return new MacroEvent(Guid.NewGuid(), Array.AsReadOnly([s.Key]), StickHeading.Neutral, StickHeading.Neutral, hold, pause);
                        })
                        .Take(47)
                        .ToArray();
                    if (imported.Length > 0) macros.Replace(slot, imported);
                    var currentOpt = macros.Options(slot);
                    macros.SetOptions(slot, new MacroOptions
                    {
                        Mode = assignment.Repeat ? MacroPlayMode.WhileHeld : MacroPlayMode.Once,
                        RepeatCount = currentOpt.RepeatCount,
                        HumanJitterMs = currentOpt.HumanJitterMs,
                    });
                    break;
                default:
                    x10.SetPaddlePassthrough(slot, slot == "M2" ? "B" : "A");
                    break;
            }
        }
    }

    public static X10HardwareProfile BuildProfile(
        ButtonDraft buttons,
        CurveDraft curves,
        MacroDraft macros,
        VibrationDraft vibration,
        X10NativeDraft x10)
    {
        var lt = ToRange(curves[CurveChannel.LeftTrigger]);
        var rt = ToRange(curves[CurveChannel.RightTrigger]);
        var ls = ToStick(curves[CurveChannel.LeftStick]);
        var rs = ToStick(curves[CurveChannel.RightStick]);
        int lm = Math.Clamp(vibration.SupportsNativeMotors ? vibration.Left : vibration.Strength, 0, 100);
        int rm = Math.Clamp(vibration.SupportsNativeMotors ? vibration.Right : vibration.Strength, 0, 100);

        var settings = new Krly.Settings(lt, rt, ls, rs, lm, rm, x10.Turbo);
        settings.Validate();

        var face = ButtonDraft.Sources.ToDictionary(s => s, buttons.Target, StringComparer.Ordinal);
        var paddles = new List<Krly.PaddleAssignment>();

        foreach (var slot in X10NativeDraft.PaddleSlots)
        {
            var mode = x10.PaddleMode(slot);
            var draftSteps = macros[slot];
            // If user authored macro steps in Macros tab without touching X10 Native mode selector, honor them cleanly.
            if (mode == Krly.PaddleSlotKind.Macro || (mode != Krly.PaddleSlotKind.Disabled && draftSteps.Count > 0))
            {
                var steps = draftSteps
                    .Select(s => new
                    {
                        Key = s.Buttons.FirstOrDefault(b => Krly.Keys.ContainsKey(b) && !Krly.RearPaddles.Contains(b)),
                        Hold = Math.Clamp(s.HoldMs <= 0 ? 40 : s.HoldMs, 1, 65535),
                        Wait = Math.Clamp(s.PauseMs, 0, 65535),
                    })
                    .Where(s => s.Key != null)
                    .Take(Krly.MaxMacroSteps)
                    .Select(s => new Krly.PaddleMacroStep(s.Hold, s.Wait, s.Key!))
                    .ToArray();

                if (steps.Length > 0)
                {
                    bool repeat = x10.PaddleMode(slot) == Krly.PaddleSlotKind.Macro
                        ? x10.PaddleRepeat(slot)
                        : macros.Options(slot).Mode != MacroPlayMode.Once;
                    paddles.Add(new Krly.PaddleAssignment(slot, Krly.PaddleSlotKind.Macro, null, repeat, steps));
                    continue;
                }
            }

            if (mode == Krly.PaddleSlotKind.Disabled)
                paddles.Add(new Krly.PaddleAssignment(slot, Krly.PaddleSlotKind.Disabled, null, false, []));
            else
                paddles.Add(new Krly.PaddleAssignment(slot, Krly.PaddleSlotKind.Passthrough, x10.PaddleTarget(slot), false, []));
        }

        return new X10HardwareProfile(settings, face, paddles);
    }

    public static IReadOnlyList<string> Compare(X10HardwareProfile expected, X10HardwareSnapshot actual)
    {
        var diffs = new List<string>();
        if (expected.Settings.LeftTrigger != actual.Settings.LeftTrigger || expected.Settings.RightTrigger != actual.Settings.RightTrigger)
            diffs.Add("Triggers");
        if (expected.Settings.LeftStick != actual.Settings.LeftStick || expected.Settings.RightStick != actual.Settings.RightStick)
            diffs.Add("Sticks");
        if (expected.Settings.LeftMotor != actual.Settings.LeftMotor || expected.Settings.RightMotor != actual.Settings.RightMotor)
            diffs.Add("Motors");

        var expTurbo = expected.Settings.Turbo.OrderBy(t => t.Key, StringComparer.Ordinal).ToArray();
        var actTurbo = actual.Settings.Turbo.OrderBy(t => t.Key, StringComparer.Ordinal).ToArray();
        if (!expTurbo.SequenceEqual(actTurbo))
            diffs.Add("Turbo");

        foreach (var source in ButtonDraft.Sources)
        {
            string expTarget = expected.FaceMappings.GetValueOrDefault(source, source);
            string actTarget = actual.FaceMappings.GetValueOrDefault(source, source);
            if (!string.Equals(expTarget, actTarget, StringComparison.Ordinal))
            {
                diffs.Add("Buttons");
                break;
            }
        }

        foreach (var expPaddle in expected.Paddles)
        {
            if (!actual.Paddles.TryGetValue(expPaddle.Paddle, out var actPaddle))
            {
                diffs.Add("Paddles");
                break;
            }
            if (expPaddle.Kind != actPaddle.Kind)
            {
                diffs.Add("Paddles");
                break;
            }
            if (expPaddle.Kind == Krly.PaddleSlotKind.Passthrough && !string.Equals(expPaddle.TargetKey, actPaddle.TargetKey, StringComparison.Ordinal))
            {
                diffs.Add("Paddles");
                break;
            }
            if (expPaddle.Kind == Krly.PaddleSlotKind.Macro && (expPaddle.Steps.Count != actPaddle.Steps.Count || actPaddle.Steps.Count == 0))
            {
                diffs.Add("Paddles");
                break;
            }
        }

        return diffs;
    }

    static void LoadTriggerCurve(CurveDraft curves, CurveChannel channel, Krly.Range range)
    {
        int inner = Math.Clamp(range.Min, 0, 40);
        int outer = Math.Clamp(range.Max, Math.Max(60, inner + 1), 100);
        var current = curves[channel];
        curves.Set(channel, current with { Inner = inner, Outer = outer });
    }

    static void LoadStickCurve(CurveDraft curves, CurveChannel channel, Krly.Stick stick)
    {
        int inner = Math.Clamp(stick.Min, 0, 40);
        int outer = Math.Clamp(stick.Max, Math.Max(60, inner + 1), 100);
        byte flags = (byte)((stick.InvertX ? (byte)CurveHardwareFlags.InvertX : 0) | (stick.InvertY ? (byte)CurveHardwareFlags.InvertY : 0));
        var current = curves[channel];
        curves.Set(channel, current with { Inner = inner, Outer = outer, Flags = flags });
    }

    static Krly.Range ToRange(CurveSetting setting)
    {
        int min = Math.Clamp(setting.Inner, 0, 99);
        int max = Math.Clamp(setting.Outer, min + 1, 100);
        return new Krly.Range(min, max);
    }

    static Krly.Stick ToStick(CurveSetting setting)
    {
        var range = ToRange(setting);
        bool invX = (setting.Flags & (byte)CurveHardwareFlags.InvertX) != 0;
        bool invY = (setting.Flags & (byte)CurveHardwareFlags.InvertY) != 0;
        return new Krly.Stick(range.Min, range.Max, invX, invY);
    }
}
