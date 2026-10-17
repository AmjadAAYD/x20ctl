using System.Text.Json;
using X20Ctl.Zone;
using X20Ctl.Zone.PcMode;
namespace X20Ctl.Linux;

/// <summary>PC mode on Linux, in one place: the saved settings, whether it is switched on, whether the notice was read, and the engine that applies it.
/// The Studio's Buttons, Curves and Macros tabs are its editor, exactly as on Windows. Every game shares one profile for now.</summary>
internal sealed class PcModeHub
{
    public static PcModeHub Shared { get; } = new();
    public const string Key = PcModeStore.DefaultKey;
    readonly string statePath = Path.Combine(Path.GetDirectoryName(PcModeStore.DefaultPath)!, "pc-mode-state.json");
    public PcModeStore Store { get; } = new(PcModeStore.DefaultPath);
    public PcEngine Engine { get; } = new();
    public bool Enabled { get; private set; }
    public bool NoticeSeen { get; private set; }
    /// <summary>One line for the Studio footer: what happened to the last change.</summary>
    public string Status { get; private set; } = "";
    public event Action? Changed;

    sealed record Saved(bool Enabled, bool NoticeSeen, bool UserOff = false);
    /// <summary>The user switched PC mode off themselves, so entering an X20 Studio must not switch it back on.</summary>
    public bool UserOff { get; private set; }

    PcModeHub()
    {
        Store.Load();
        try { if (File.Exists(statePath) && JsonSerializer.Deserialize<Saved>(File.ReadAllBytes(statePath)) is { } s) { Enabled = s.Enabled; NoticeSeen = s.NoticeSeen; UserOff = s.UserOff; } }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { }
        Engine.Profile = () => Store.Get(Key);
        Engine.Changed += () => Changed?.Invoke();
        if (Enabled && NoticeSeen) Engine.Start();
    }

    void Remember()
    {
        try { Directory.CreateDirectory(Path.GetDirectoryName(statePath)!); File.WriteAllBytes(statePath, JsonSerializer.SerializeToUtf8Bytes(new Saved(Enabled, NoticeSeen, UserOff))); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Status = "Couldn't remember this: " + e.Message; }
    }

    public void MarkNoticeSeen() { NoticeSeen = true; Remember(); Changed?.Invoke(); }

    /// <summary>Switches PC mode. Turning it on needs the notice read first; returns false when it was held back.</summary>
    public bool SetEnabled(bool on) { if (on && !NoticeSeen) return false; UserOff = !on; return Turn(on); }

    /// <summary>An X20 gets PC mode switched on by itself as soon as its Studio opens, once the notice has been read and unless the user turned it off.
    /// Returns true when the Studio should open on the PC mode tab so the notice can be read first.</summary>
    public bool AutoFor(string modelId)
    {
        if (modelId != "x20" || !PcEngine.Supported || Enabled || UserOff) return false;
        if (!NoticeSeen) return true;
        Turn(true); return false;
    }

    bool Turn(bool on)
    {
        Enabled = on; Remember();
        if (on) Engine.Start(); else Engine.Stop();
        Changed?.Invoke(); return true;
    }

    /// <summary>Before a Studio opens: fill its drafts from what is saved, unless nothing is saved and the drafts hold work.</summary>
    public void Prepare(StudioState s)
    {
        var p = Store.Get(Key);
        if (p.IsNeutral && PcModeDrafts.HasChanges(s.Buttons, s.Curves, s.Macros)) return;
        try
        {
            PcModeDrafts.Load(p, s.Buttons, s.Curves, s.Macros);
            s.ResetTunes();
            foreach (var channel in new[] { CurveChannel.LeftStick, CurveChannel.RightStick })
                if (p.Sticks.TryGetValue(channel.ToString(), out var t)) s.SetTune(channel, t);
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException) { Status = "Some saved settings couldn't be shown: " + e.Message; }
    }

    public PcModeProfile Build(StudioState s)
    {
        var p = PcModeDrafts.Merge(Store.Get(Key), s.Buttons, s.Curves, s.Macros);
        p.Sticks = new();
        foreach (var channel in new[] { CurveChannel.LeftStick, CurveChannel.RightStick })
            if (!s.Tune(channel).IsDefault) p.Sticks[channel.ToString()] = s.Tune(channel);
        return p;
    }

    /// <summary>Saves the Studio's drafts as the PC mode profile and tells the engine. Returns the line to show.</summary>
    public string Save(StudioState s) => Store_Save(Build(s));

    public string Store_Save(PcModeProfile next)
    {
        try { next.Validate(); }
        catch (InvalidDataException e) { return Status = "Not saved: " + e.Message; }
        if (!next.IsNeutral && !NoticeSeen) return Status = "Read the notice on the PC mode tab first";
        try { Store.Save(Key, next); Engine.Reload(); Status = next.IsNeutral ? "Nothing to apply: your controller goes straight to the game" : Enabled ? "Saved · Applies right away" : "Saved · PC mode is off"; }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException) { Status = "Couldn't save: " + e.Message; }
        Changed?.Invoke(); return Status;
    }
}
