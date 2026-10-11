using System.IO;
using System.Text.Json;
using X20Ctl.Zone;
using X20Ctl.Zone.PcMode;
namespace X20Ctl.Product;

/// <summary>
/// While PC mode is on, a controller's Buttons, Curves and Macros tabs are PC mode's editor: every change in them is
/// saved to the PC mode settings picked on the PC mode tab (every game's, or a game's own) and applies right away.
/// </summary>
public sealed class PcModeSync(PcModeLink link, ButtonDraft buttons, CurveDraft curves, MacroDraft macros)
{
    /// <summary>One line for the Studio's status bar: what happened to the last change.</summary>
    public string Status { get; private set; } = "";
    // what the tabs held when they were last filled or saved: until they change, nothing is written, so settings the tabs
    // can't show (a fifth macro, Home inside a macro, odd timings) survive just being looked at
    string baseline = Json(PcModeDrafts.Merge(link.Store.Get(PcModePage.StoreKey), buttons, curves, macros));

    /// <summary>Before a Studio opens: fill its drafts from PC mode, unless PC mode holds nothing yet and the drafts do
    /// (work done before PC mode was on is kept, and saved with the next change).</summary>
    public static void Prepare(PcModeStore store, ButtonDraft buttons, CurveDraft curves, MacroDraft macros)
    {
        var p = store.Get(PcModePage.StoreKey);
        if (p.IsNeutral && PcModeDrafts.HasChanges(buttons, curves, macros)) return;
        try { PcModeDrafts.Load(p, buttons, curves, macros); }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException) { }
    }

    /// <summary>PC mode is switching off: a model that can't otherwise be edited shouldn't keep showing its settings.</summary>
    public static void Clear(ButtonDraft buttons, CurveDraft curves, MacroDraft macros)
    {
        buttons.ResetAll(); curves.ResetAll();
        foreach (var slot in MacroDraft.Slots) { macros.Clear(slot); macros.SetTrigger(slot, null); }
    }

    public void Reload()
    {
        try { var p = link.Store.Get(PcModePage.StoreKey); PcModeDrafts.Load(p, buttons, curves, macros); baseline = Json(PcModeDrafts.Merge(p, buttons, curves, macros)); Status = "PC mode · Showing the settings for " + Target(); }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException) { Status = "PC mode · Some settings couldn't be shown here: " + error.Message; }
    }

    /// <summary>Saves the tabs' settings if they changed. Returns whether anything was written.</summary>
    public bool Save()
    {
        string key = PcModePage.StoreKey;
        var stored = link.Store.Get(key);
        var next = PcModeDrafts.Merge(stored, buttons, curves, macros);
        string json = Json(next);
        if (json == baseline || json == Json(stored)) { if (Status.Length == 0) Status = Describe(stored); return false; }
        try { next.Validate(); }
        catch (InvalidDataException error) { Status = "PC mode · Not applied: " + error.Message; return false; }
        if (!next.IsNeutral && !PcModePage.MaySave(link.Preferences)) { Status = "PC mode · Open the PC mode tab and read the notice to start applying this"; return false; }
        try { link.Store.Save(key, next); link.Service.Reload(); baseline = json; Status = Describe(next); return true; }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException) { Status = "PC mode · Couldn't save: " + error.Message; return false; }
    }

    string Describe(PcModeProfile p) => p.IsNeutral ? "PC mode · No changes yet: the controller goes straight to games" : "PC mode · Saved for " + Target() + " · Applies right away";
    string Target() => $"Player {PcModePage.Player + 1}, " + (PcModePage.ProfileKey == PcModeStore.DefaultKey ? "every game" : link.Store.Get(PcModePage.StoreKey).Name ?? PcModePage.ProfileKey[PcModeStore.GamePrefix.Length..]);
    static string Json(PcModeProfile p) => JsonSerializer.Serialize(p);
}
