namespace X20Ctl.Zone;

/// <summary>The settings records an X20 Pro on firmware 1.44 keeps in its own memory.</summary>
public sealed record ControllerSnapshot(byte[] Remap, byte[] Sticks, byte[] Triggers, byte[][] Macros);

/// <summary>Converts between the Studio's drafts and those records. Layouts come from reads and writes on the owner's
/// pad: remap is a count then one target per source (0 = unchanged), curves are two 7-byte channels behind a length.</summary>
public static class ControllerRecords
{
    public const int MacroEntries = 42; // the limit the pad reports for every slot
    static readonly (string Name, byte Code)[] RemapSources =
    [
        ("A",1),("B",2),("X",3),("Y",4),("LB",5),("RB",6),("LT",7),("RT",8),
        ("DPAD_DOWN",13),("DPAD_UP",14),("DPAD_RIGHT",15),("DPAD_LEFT",16),("L3",11),("R3",12),("SELECT",93),("START",94),
    ];
    const int ChannelSize = 7, StickScale = 1, TriggerScale = 2;
    static byte Code(string name) => RemapSources.First(s => s.Name == name).Code;

    public static byte[] BuildRemap(IReadOnlyDictionary<string, string> changes)
    {
        var record = new byte[RemapSources.Length + 1];
        record[0] = (byte)RemapSources.Length;
        for (int i = 0; i < RemapSources.Length; i++)
            if (changes.TryGetValue(RemapSources[i].Name, out var target) && target != RemapSources[i].Name) record[i + 1] = Code(target);
        return record;
    }
    public static Dictionary<string, string> ParseRemap(byte[] record)
    {
        var map = new Dictionary<string, string>();
        if (record.Length == 1 && record[0] == 0) return map; // a pad that was never remapped
        if (record.Length != RemapSources.Length + 1 || record[0] != RemapSources.Length) throw new FormatException("The remap record has an unexpected length.");
        for (int i = 0; i < RemapSources.Length; i++)
        {
            byte target = record[i + 1];
            if (target == 0 || target == RemapSources[i].Code) continue;
            map[RemapSources[i].Name] = RemapSources.FirstOrDefault(s => s.Code == target).Name ?? throw new FormatException("The remap record names an unknown button.");
        }
        return map;
    }
    /// <summary>A never-remapped pad answers one zero and an all-default record alike: both mean no remaps.</summary>
    public static bool SameRemap(byte[] a, byte[] b) => ParseRemap(a).Count == ParseRemap(b).Count && !ParseRemap(a).Except(ParseRemap(b)).Any();

    /// <summary>The factory reset command for a pad's identity flags: mode in bits 0-3, generation in bits 5-7. The last
    /// byte is the generation KeyLinker routes on, where raw 2 counts as 1 in modes 0, 7 and 8. Null when it is neither.</summary>
    public static byte[]? FactoryResetPayload(byte flags)
    {
        int mode = flags & 15, generation = flags >> 5;
        if (generation == 2 && mode is 0 or 7 or 8) generation = 1;
        return generation is 1 or 2 ? [3, 0xdf, 0xa9, (byte)generation] : null;
    }

    /// <summary>Which of the tabs' records differ. Both no-remap forms count as the same.</summary>
    public static int Changes(ControllerSnapshot before, ControllerSnapshot after) =>
        (SameRemap(before.Remap, after.Remap) ? 0 : 1) + (before.Sticks.SequenceEqual(after.Sticks) ? 0 : 1) + (before.Triggers.SequenceEqual(after.Triggers) ? 0 : 1)
        + before.Macros.Zip(after.Macros).Count(pair => !pair.First.SequenceEqual(pair.Second));

    public static CurveSetting ParseChannel(byte[] record, int channel, bool trigger)
    {
        Check(record);
        int at = 1 + channel * ChannelSize, scale = trigger ? TriggerScale : StickScale;
        (int, int) first = (record[at + 2], record[at + 3]), second = (record[at + 4], record[at + 5]);
        string preset = CurveDraft.Presets.FirstOrDefault(p => p.Value.First == first && p.Value.Second == second).Key ?? "custom";
        return new CurveSetting(preset, record[at] / scale, 100 - record[at + 1] / scale, first, second) { BasePreset = preset == "custom" ? "default" : preset };
    }
    /// <summary>Only a channel that differs is changed, so the pad's own odd values and flags survive.</summary>
    public static byte[] BuildCurves(byte[] current, CurveSetting first, CurveSetting second, bool trigger)
    {
        Check(current);
        var next = (byte[])current.Clone();
        for (int channel = 0; channel < 2; channel++)
        {
            var wanted = channel == 0 ? first : second;
            var held = ParseChannel(current, channel, trigger);
            if ((held.Inner, held.Outer, held.Point1, held.Point2) == (wanted.Inner, wanted.Outer, wanted.Point1, wanted.Point2)) continue;
            int at = 1 + channel * ChannelSize, scale = trigger ? TriggerScale : StickScale;
            next[at] = (byte)(wanted.Inner * scale); next[at + 1] = (byte)((100 - wanted.Outer) * scale);
            next[at + 2] = (byte)wanted.Point1.X; next[at + 3] = (byte)wanted.Point1.Y; next[at + 4] = (byte)wanted.Point2.X; next[at + 5] = (byte)wanted.Point2.Y;
        }
        return next;
    }
    static void Check(byte[] record) { if (record.Length != 1 + 2 * ChannelSize || record[0] != 2 * ChannelSize) throw new FormatException("The curve record has an unexpected length."); }

    /// <summary>The events as a stored macro, keeping the loop bytes of what the slot held. An empty sequence stays one
    /// resting step: the pad's empty form has never been written.</summary>
    public static byte[] BuildMacro(IReadOnlyList<MacroEvent> events, byte[] current)
    {
        var entries = new List<byte[]>();
        foreach (var e in events)
        {
            entries.Add(MacroRecord.Entry(e.Buttons, e.Left, e.Right, e.HoldMs));
            if (e.PauseMs > 0) entries.Add(MacroRecord.Entry([], StickHeading.Neutral, StickHeading.Neutral, e.PauseMs));
        }
        if (entries.Count == 0) entries.Add(MacroRecord.Entry([], StickHeading.Neutral, StickHeading.Neutral, 40));
        if (entries.Count > MacroEntries) throw new InvalidOperationException($"A macro holds {MacroEntries} steps on this controller.");
        bool held = current.Length >= 3 && current[0] > 0;
        return [(byte)entries.Count, held ? current[1] : (byte)0, held ? current[2] : (byte)0, .. entries.SelectMany(e => e)];
    }

    /// <summary>What the pad should hold after applying the drafts. Anything the drafts leave as it is stays byte for byte.</summary>
    public static ControllerSnapshot Compose(ControllerSnapshot current, ButtonDraft buttons, CurveDraft curves, MacroDraft macros)
    {
        var remap = BuildRemap(buttons.Changes);
        var slots = new byte[current.Macros.Length][];
        for (int i = 0; i < slots.Length; i++)
        {
            var wanted = macros[macros.SlotNames[i]];
            var built = BuildMacro(wanted, current.Macros[i]);
            var held = MacroRecord.Parse(current.Macros[i]);
            slots[i] = wanted.Count == held.Count && built.SequenceEqual(BuildMacro(held, current.Macros[i])) ? current.Macros[i] : built;
        }
        return new(SameRemap(remap, current.Remap) ? current.Remap : remap,
            BuildCurves(current.Sticks, curves[CurveChannel.LeftStick], curves[CurveChannel.RightStick], false),
            BuildCurves(current.Triggers, curves[CurveChannel.LeftTrigger], curves[CurveChannel.RightTrigger], true), slots);
    }

    /// <summary>Fills the drafts from what the pad holds.</summary>
    public static void Load(ControllerSnapshot read, ButtonDraft buttons, CurveDraft curves, MacroDraft macros)
    {
        buttons.ResetAll();
        foreach (var (source, target) in ParseRemap(read.Remap)) buttons.Set(source, target);
        curves.Set(CurveChannel.LeftStick, ParseChannel(read.Sticks, 0, false)); curves.Set(CurveChannel.RightStick, ParseChannel(read.Sticks, 1, false));
        curves.Set(CurveChannel.LeftTrigger, ParseChannel(read.Triggers, 0, true)); curves.Set(CurveChannel.RightTrigger, ParseChannel(read.Triggers, 1, true));
        for (int i = 0; i < read.Macros.Length; i++) macros.Replace(macros.SlotNames[i], MacroRecord.Parse(read.Macros[i]));
    }
}
