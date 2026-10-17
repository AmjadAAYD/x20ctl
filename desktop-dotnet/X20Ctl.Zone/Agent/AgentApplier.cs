using System.Globalization;
using System.Text.Json;
using X20Ctl.Zone.PcMode;
namespace X20Ctl.Zone.Agent;

public sealed record AgentChange(string What, string Before, string After);
/// <summary>The profile a verdict leads to, and what changed. Next is null when the proposal was rejected: nothing moves.</summary>
/// <param name="PcMode">PC mode switched on (true) or off (false) by the proposal; null when it doesn't say.</param>
/// <param name="Mouse">Mouse mode switched on or off; null when the proposal doesn't say.</param>
/// <param name="Page">The page the owner should be taken to, if any.</param>
public sealed record AgentOutcome(PcModeProfile? Next, IReadOnlyList<AgentChange> Changes, string? Error, bool? PcMode = null, bool? Mouse = null, string? Page = null);

/// <summary>
/// Turns a validated proposal into the next PC mode profile. Pure: it copies the current profile, never touches it, and
/// ends with the profile's own Validate, so a proposal that slips past the action rules still can't store a bad setting.
/// </summary>
public static class AgentApplier
{
    public static AgentOutcome Apply(PcModeProfile current, AgentVerdict verdict)
    {
        if (!verdict.Valid) return new(null, [], verdict.StructureError ?? "A change was rejected, so nothing was changed.");
        var next = Copy(current);
        foreach (var action in verdict.Applicable) Do(next, action);
        try { next.Validate(); }
        catch (InvalidDataException error) { return new(null, [], "The result wouldn't be a valid setting (" + error.Message.TrimEnd('.') + "), so nothing was changed."); }
        bool? pc = verdict.Applicable.LastOrDefault(a => a.Action == AgentVocabulary.PcMode) is { } s ? s.ValueText == "on" : null;
        bool? mouse = verdict.Applicable.LastOrDefault(a => a.Action == AgentVocabulary.MouseMode) is { } m ? m.ValueText == "on" : null;
        string? page = verdict.Applicable.LastOrDefault(a => a.Action == AgentVocabulary.OpenPage)?.ValueText;
        return new(next, Diff(current, next), null, pc, mouse, page);
    }

    static void Do(PcModeProfile p, AgentAction a)
    {
        switch (a.Action)
        {
            case AgentVocabulary.StickDeadzone: Stick(p, a.Target).Inner = Round(a.Value.GetDouble()); break;
            case AgentVocabulary.StickFullAt: Stick(p, a.Target).Outer = Round(a.Value.GetDouble()); break;
            case AgentVocabulary.StickAntiDeadzone: Stick(p, a.Target).AntiDeadzone = Round(a.Value.GetDouble()); break;
            case AgentVocabulary.TriggerDeadzone: p.Curves[a.Target] = Curve(p, a.Target) with { Inner = a.Value.GetInt32() }; break;
            case AgentVocabulary.TriggerFullAt: p.Curves[a.Target] = Curve(p, a.Target) with { Outer = a.Value.GetInt32() }; break;
            case AgentVocabulary.StickCurve or AgentVocabulary.TriggerCurve:
                string preset = a.Value.GetString()!; var (first, second) = CurveDraft.Presets[preset];
                p.Curves[a.Target] = Curve(p, a.Target) with { Preset = preset, BasePreset = preset, X1 = first.Item1, Y1 = first.Item2, X2 = second.Item1, Y2 = second.Item2 }; break;
            case AgentVocabulary.StickCurvePoints or AgentVocabulary.TriggerCurvePoints:
                CurvePoints.TryParse(a.ValueText, out var pts, out _);
                p.Curves[a.Target] = Curve(p, a.Target) with { Preset = "custom", X1 = CurvePoints.ToByte(pts.X1), Y1 = CurvePoints.ToByte(pts.Y1), X2 = CurvePoints.ToByte(pts.X2), Y2 = CurvePoints.ToByte(pts.Y2) }; break;
            case AgentVocabulary.StickMaxOutput: Stick(p, a.Target).MaxOutput = Round(a.Value.GetDouble()); break;
            case AgentVocabulary.StickAxisRatio: Stick(p, a.Target).AxisRatio = a.Value.GetInt32(); break;
            case AgentVocabulary.StickSquareGate: Stick(p, a.Target).SquareGate = a.ValueText == "on"; break;
            case AgentVocabulary.Remap:
                string output = a.Value.GetString()!; if (output == a.Target) p.Remap.Remove(a.Target); else p.Remap[a.Target] = output; break;
            case AgentVocabulary.Turbo:
                int hz = a.Value.GetInt32(); if (hz == 0) p.Turbo.Remove(a.Target); else p.Turbo[a.Target] = hz; break;
            case AgentVocabulary.Reset: Reset(p, a.Target); break;
        }
    }

    static void Reset(PcModeProfile p, string target)
    {
        if (target == AgentVocabulary.All) { p.Remap.Clear(); p.Curves.Clear(); p.Turbo.Clear(); p.Sticks.Clear(); return; }
        p.Curves.Remove(target); p.Sticks.Remove(target); p.Remap.Remove(target); p.Turbo.Remove(target);
    }

    /// <summary>A stick's fine settings, created at their defaults when the profile has none yet. When present, their
    /// dead zones replace the curve's whole-percent ones, so they start from the curve's values.</summary>
    static StickTuning Stick(PcModeProfile p, string stick)
    {
        if (p.Sticks.TryGetValue(stick, out var s)) return s;
        var c = Curve(p, stick);
        return p.Sticks[stick] = new StickTuning { Inner = c.Inner, Outer = c.Outer };
    }
    static SetupCurve Curve(PcModeProfile p, string channel) => p.Curves.TryGetValue(channel, out var c) ? c : new("default", "default", 0, 100, 85, 85, 170, 170);
    static double Round(double v) => Math.Round(v, 1);
    static PcModeProfile Copy(PcModeProfile p) => JsonSerializer.Deserialize<PcModeProfile>(JsonSerializer.SerializeToUtf8Bytes(p))!;

    /// <summary>Every setting that differs between two profiles, as a person would read it.</summary>
    public static IReadOnlyList<AgentChange> Diff(PcModeProfile before, PcModeProfile after)
    {
        var changes = new List<AgentChange>();
        void Add(string what, string from, string to) { if (from != to) changes.Add(new(what, from, to)); }
        foreach (var stick in AgentVocabulary.Sticks)
        {
            var (b, a) = (Tuning(before, stick), Tuning(after, stick)); string name = Name(stick);
            Add(name + " dead zone", Pct(b.Inner), Pct(a.Inner)); Add(name + " full at", Pct(b.Outer), Pct(a.Outer)); Add(name + " anti-deadzone", Pct(b.AntiDeadzone), Pct(a.AntiDeadzone));
            Add(name + " max output", Pct(b.MaxOutput), Pct(a.MaxOutput)); Add(name + " vertical ratio", Pct(b.AxisRatio), Pct(a.AxisRatio)); Add(name + " square gate", b.SquareGate ? "on" : "off", a.SquareGate ? "on" : "off");
            Add(name + " curve", Shape(Curve(before, stick)), Shape(Curve(after, stick)));
        }
        foreach (var trigger in AgentVocabulary.Triggers)
        {
            var (b, a) = (Curve(before, trigger), Curve(after, trigger)); string name = Name(trigger);
            Add(name + " dead zone", Pct(b.Inner), Pct(a.Inner)); Add(name + " full at", Pct(b.Outer), Pct(a.Outer)); Add(name + " curve", Shape(b), Shape(a));
        }
        foreach (var button in AgentVocabulary.Buttons)
        {
            Add(button + " sends", before.Remap.GetValueOrDefault(button, button), after.Remap.GetValueOrDefault(button, button));
            Add(button + " turbo", Hz(before.Turbo.GetValueOrDefault(button)), Hz(after.Turbo.GetValueOrDefault(button)));
        }
        return changes;
    }
    static StickTuning Tuning(PcModeProfile p, string stick) => p.Sticks.TryGetValue(stick, out var s) ? s : new StickTuning { Inner = Curve(p, stick).Inner, Outer = Curve(p, stick).Outer };
    /// <summary>A preset by name; a custom curve by its two points, as push → output percents.</summary>
    public static string Shape(SetupCurve c) => c.Preset != "custom" ? c.Preset : string.Create(CultureInfo.InvariantCulture, $"custom {Math.Round(c.X1 / 2.55)}→{Math.Round(c.Y1 / 2.55)}, {Math.Round(c.X2 / 2.55)}→{Math.Round(c.Y2 / 2.55)}");
    static string Pct(double v) => v.ToString("0.#", CultureInfo.InvariantCulture) + "%";
    static string Hz(int hz) => hz == 0 ? "off" : hz + "/s";
    public static string Name(string target) => target switch { "LeftStick" => "Left stick", "RightStick" => "Right stick", "LeftTrigger" => "Left trigger", "RightTrigger" => "Right trigger", _ => target };
}
