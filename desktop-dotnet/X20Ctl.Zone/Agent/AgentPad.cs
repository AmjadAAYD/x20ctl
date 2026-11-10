using System.Globalization;
using X20Ctl.Zone.PcMode;
namespace X20Ctl.Zone.Agent;

/// <summary>What saving an agent change on the controller would do: the records the pad should hold, every setting that
/// differs as read from those records, and the parts that stay PC mode only because the pad has no proven place for them.</summary>
public sealed record AgentPadPlan(ControllerSnapshot Next, IReadOnlyList<AgentChange> Changes, IReadOnlyList<string> PcOnly)
{
    public bool Writes => Changes.Count > 0;
}

/// <summary>
/// The agent's Pad target (class project, 8 Nov 2026). Only the settings whose write and read-back are proven on the
/// owner's X20 Pro (firmware 1.44, <see cref="ControllerRecords"/>) reach the controller: button remaps, and each stick's
/// and trigger's dead zone, full-at point and two curve points. Only the fields an applied action touched change; the
/// pad's other values and its own remaps stay byte for byte. Everything else (anti-dead zone, max output, square gate,
/// vertical ratio, turbo, resets) is named as PC mode only, never approximated. Pure: the app reads, writes and reads back.
/// </summary>
public static class AgentPad
{
    public const string Model = "x20_pro";
    static readonly HashSet<string> Curves = [AgentVocabulary.StickDeadzone, AgentVocabulary.StickFullAt, AgentVocabulary.StickCurve, AgentVocabulary.StickCurvePoints,
        AgentVocabulary.TriggerDeadzone, AgentVocabulary.TriggerFullAt, AgentVocabulary.TriggerCurve, AgentVocabulary.TriggerCurvePoints];

    /// <summary>Whether any of these applied actions could be saved on the controller at all.</summary>
    public static bool Offers(string modelId, IEnumerable<AgentAction> applied) => modelId == Model && applied.Any(a => Holds(a) is null);

    /// <summary>Null when the pad has a proven place for this action; otherwise why it stays PC mode only.</summary>
    public static string? Holds(AgentAction a)
    {
        if (Curves.Contains(a.Action)) return null;
        if (a.Action == AgentVocabulary.Remap)
            return ControllerRecords.Remappable(a.Target) && ControllerRecords.Remappable(a.ValueText) ? null : $"{a.Target} sends {a.ValueText}: the controller's remap list has no slot for it";
        return a.Action switch
        {
            AgentVocabulary.PcMode or AgentVocabulary.MouseMode or AgentVocabulary.OpenPage or AgentVocabulary.MeasureDrift or AgentVocabulary.LedColor => "",
            AgentVocabulary.Reset => $"Reset {AgentApplier.Name(a.Target)}: the controller has no proven reset for one setting",
            _ => $"{Label(a.Action)} for {AgentApplier.Name(a.Target)}: the controller has no proven place for it",
        };
    }

    public static AgentPadPlan Plan(ControllerSnapshot current, IEnumerable<AgentAction> applied, PcModeProfile after)
    {
        var pcOnly = new List<string>();
        var channels = new Dictionary<string, CurveSetting>();
        CurveSetting Held(string target) => channels.TryGetValue(target, out var c) ? c
            : channels[target] = ControllerRecords.ParseChannel(IsTrigger(target) ? current.Triggers : current.Sticks, Index(target), IsTrigger(target));
        var remap = ControllerRecords.ParseRemap(current.Remap);
        bool remapTouched = false;

        foreach (var a in applied)
        {
            if (Holds(a) is { } why) { if (why.Length > 0 && !pcOnly.Contains(why)) pcOnly.Add(why); continue; }
            if (a.Action == AgentVocabulary.Remap)
            {
                if (a.ValueText == a.Target) remap.Remove(a.Target); else remap[a.Target] = a.ValueText;
                remapTouched = true; continue;
            }
            var held = Held(a.Target);
            var curve = after.Curves.TryGetValue(a.Target, out var c) ? c : new SetupCurve("default", "default", 0, 100, 85, 85, 170, 170);
            switch (a.Action)
            {
                case AgentVocabulary.StickDeadzone or AgentVocabulary.StickFullAt:
                    double value = a.Action == AgentVocabulary.StickDeadzone ? Tuning(after, a.Target).Inner : Tuning(after, a.Target).Outer;
                    if (value != Math.Round(value)) { pcOnly.Add($"{Label(a.Action)} for {AgentApplier.Name(a.Target)} at {Pct(value)}: the controller holds whole percents"); continue; }
                    channels[a.Target] = a.Action == AgentVocabulary.StickDeadzone ? held with { Inner = (int)value } : held with { Outer = (int)value };
                    break;
                case AgentVocabulary.TriggerDeadzone: channels[a.Target] = held with { Inner = curve.Inner }; break;
                case AgentVocabulary.TriggerFullAt: channels[a.Target] = held with { Outer = curve.Outer }; break;
                default: channels[a.Target] = held with { Point1 = (curve.X1, curve.Y1), Point2 = (curve.X2, curve.Y2) }; break;
            }
        }
        foreach (var (target, setting) in channels.ToList())
            if (setting.Outer <= setting.Inner) { pcOnly.Add($"{AgentApplier.Name(target)}: full at {setting.Outer}% would not be past the dead zone of {setting.Inner}% on the controller"); channels.Remove(target); }

        CurveSetting Pick(string target) => channels.TryGetValue(target, out var c) ? c
            : ControllerRecords.ParseChannel(IsTrigger(target) ? current.Triggers : current.Sticks, Index(target), IsTrigger(target));
        var sticks = channels.Keys.Any(k => !IsTrigger(k)) ? ControllerRecords.BuildCurves(current.Sticks, Pick("LeftStick"), Pick("RightStick"), false) : current.Sticks;
        var triggers = channels.Keys.Any(IsTrigger) ? ControllerRecords.BuildCurves(current.Triggers, Pick("LeftTrigger"), Pick("RightTrigger"), true) : current.Triggers;
        var remapRecord = remapTouched ? ControllerRecords.BuildRemap(remap) : current.Remap;
        if (remapTouched && ControllerRecords.SameRemap(remapRecord, current.Remap)) remapRecord = current.Remap;
        var next = current with { Remap = remapRecord, Sticks = sticks, Triggers = triggers };
        return new(next, Diff(current, next), pcOnly);
    }

    /// <summary>Restores the records the agent changed, provided each still matches what it wrote.</summary>
    public static ControllerSnapshot Restore(ControllerSnapshot held, ControllerSnapshot before, ControllerSnapshot written)
    {
        bool remap = !ControllerRecords.SameRemap(before.Remap, written.Remap);
        bool sticks = !before.Sticks.SequenceEqual(written.Sticks);
        bool triggers = !before.Triggers.SequenceEqual(written.Triggers);
        if ((remap && !ControllerRecords.SameRemap(held.Remap, written.Remap)) ||
            (sticks && !held.Sticks.SequenceEqual(written.Sticks)) ||
            (triggers && !held.Triggers.SequenceEqual(written.Triggers)))
            throw new InvalidOperationException("The controller settings changed since this save. Undo would overwrite a newer edit.");
        return held with
        {
            Remap = remap ? before.Remap : held.Remap,
            Sticks = sticks ? before.Sticks : held.Sticks,
            Triggers = triggers ? before.Triggers : held.Triggers,
        };
    }

    /// <summary>Every setting that differs between two sets of records, read back from the records themselves.</summary>
    public static IReadOnlyList<AgentChange> Diff(ControllerSnapshot before, ControllerSnapshot after)
    {
        var changes = new List<AgentChange>();
        void Add(string what, string from, string to) { if (from != to) changes.Add(new(what, from, to)); }
        foreach (var target in new[] { "LeftStick", "RightStick", "LeftTrigger", "RightTrigger" })
        {
            bool trigger = IsTrigger(target);
            var b = ControllerRecords.ParseChannel(trigger ? before.Triggers : before.Sticks, Index(target), trigger);
            var a = ControllerRecords.ParseChannel(trigger ? after.Triggers : after.Sticks, Index(target), trigger);
            string name = AgentApplier.Name(target);
            Add(name + " dead zone", b.Inner + "%", a.Inner + "%"); Add(name + " full at", b.Outer + "%", a.Outer + "%"); Add(name + " curve", Shape(b), Shape(a));
        }
        var (rb, ra) = (ControllerRecords.ParseRemap(before.Remap), ControllerRecords.ParseRemap(after.Remap));
        foreach (var button in rb.Keys.Union(ra.Keys).OrderBy(k => k, StringComparer.Ordinal))
            Add(button + " sends", rb.GetValueOrDefault(button, button), ra.GetValueOrDefault(button, button));
        return changes;
    }

    static string Shape(CurveSetting c) => AgentApplier.Shape(new SetupCurve(c.Preset, c.BasePreset, c.Inner, c.Outer, c.Point1.X, c.Point1.Y, c.Point2.X, c.Point2.Y));
    static bool IsTrigger(string target) => target is "LeftTrigger" or "RightTrigger";
    static int Index(string target) => target is "LeftStick" or "LeftTrigger" ? 0 : 1;
    static StickTuning Tuning(PcModeProfile p, string stick) => p.Sticks.TryGetValue(stick, out var s) ? s
        : new StickTuning { Inner = p.Curves.TryGetValue(stick, out var c) ? c.Inner : 0, Outer = p.Curves.TryGetValue(stick, out var o) ? o.Outer : 100 };
    static string Pct(double v) => v.ToString("0.#", CultureInfo.InvariantCulture) + "%";
    static string Label(string action) => action.Replace('_', ' ') switch { var s => char.ToUpperInvariant(s[0]) + s[1..] };
}
