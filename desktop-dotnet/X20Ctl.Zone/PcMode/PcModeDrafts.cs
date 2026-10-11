using System.Text.Json;
namespace X20Ctl.Zone.PcMode;

/// <summary>
/// The Studio's Buttons, Curves and Macros drafts are PC mode's editor: while PC mode is on, a controller's tabs edit
/// the PC mode settings directly. This moves settings between the two without losing what the tabs can't show (turbo,
/// the Advanced stick tools, a remap of View, Menu or Home, a game's name).
/// </summary>
public static class PcModeDrafts
{
    /// <summary>Fills the drafts from a profile. The drafts must already be unlocked for PC mode (or be an X20's).</summary>
    public static void Load(PcModeProfile p, ButtonDraft buttons, CurveDraft curves, MacroDraft macros)
    {
        buttons.ResetAll();
        foreach (var source in ButtonDraft.Sources)
            if (p.Remap.TryGetValue(source, out var target) && ButtonDraft.Targets.Contains(target)) buttons.Set(source, target);

        foreach (var channel in Enum.GetValues<CurveChannel>())
        {
            if (p.Curves.TryGetValue(channel.ToString(), out var c))
                try { curves.Set(channel, new CurveSetting(c.Preset, c.Inner, c.Outer, (c.X1, c.Y1), (c.X2, c.Y2)) { BasePreset = c.BasePreset }); }
                catch (ArgumentException) { curves.Reset(channel); }
            else curves.Reset(channel);
        }

        foreach (var slot in MacroDraft.Slots) { macros.Clear(slot); macros.SetTrigger(slot, null); }
        int next = 0;
        foreach (var (trigger, steps) in p.Macros)
        {
            if (next >= MacroDraft.Slots.Length) break;
            if (!MacroDraft.TriggerInputs.Contains(trigger)) continue;
            string slot = MacroDraft.Slots[next++];
            macros.SetTrigger(slot, trigger);
            foreach (var s in steps)
            {
                // the Studio's macro editor works in 5 ms steps and up to 47 entries (the X20's own limit)
                var names = s.Buttons.Where(MacroDraft.Inputs.Contains).Distinct().ToArray();
                int hold = Five(s.HoldMs), pause = Five(s.PauseMs);
                if (macros.EntryCount(slot) + 1 + (pause > 0 ? 1 : 0) > 47) break;
                macros.Add(slot, pause);
                macros.Update(slot, macros[slot].Count - 1, new(s.Id == Guid.Empty ? Guid.NewGuid() : s.Id, names, s.Left, s.Right, hold, pause));
            }
        }
    }

    /// <summary>The profile the drafts describe, built on the saved one so turbo, stick tools and the rest are kept.</summary>
    public static PcModeProfile Merge(PcModeProfile saved, ButtonDraft buttons, CurveDraft curves, MacroDraft macros)
    {
        var p = JsonSerializer.Deserialize<PcModeProfile>(JsonSerializer.SerializeToUtf8Bytes(saved))!;
        p.Remap = saved.Remap.Where(r => !ButtonDraft.Sources.Contains(r.Key)).ToDictionary(r => r.Key, r => r.Value);
        foreach (var source in ButtonDraft.Sources)
        {
            string target = buttons.Target(source);
            if (target != source) p.Remap[source] = target;
            // a remap the Buttons tab can't show (to Home) stays until it's changed there
            else if (saved.Remap.TryGetValue(source, out var kept) && !ButtonDraft.Targets.Contains(kept)) p.Remap[source] = kept;
        }

        p.Curves = new();
        foreach (var channel in Enum.GetValues<CurveChannel>())
        {
            var v = curves[channel];
            var curve = new SetupCurve(v.Preset, v.BasePreset, v.Inner, v.Outer, v.Point1.X, v.Point1.Y, v.Point2.X, v.Point2.Y);
            if (!PcModeProfile.IsDefault(curve)) p.Curves[channel.ToString()] = curve;
        }

        p.Macros = new();
        foreach (var slot in MacroDraft.Slots)
            if (macros.Trigger(slot) is { } trigger && macros[slot].Count > 0)
                p.Macros[trigger] = macros[slot].Select(e => new SetupMacro(e.Id, e.Buttons.ToArray(), e.Left, e.Right, Math.Min(e.HoldMs, 10000), Math.Min(e.PauseMs, 10000))).ToArray();
        return p;
    }

    /// <summary>Whether the drafts hold anything, so turning PC mode on can keep work done before.</summary>
    public static bool HasChanges(ButtonDraft buttons, CurveDraft curves, MacroDraft macros) =>
        buttons.UnsentChanges > 0 || curves.ModifiedChannels > 0 || macros.ModifiedSlots > 0;

    static int Five(int ms) => Math.Clamp((int)Math.Round(ms / 5.0) * 5, 0, 10000);
}
