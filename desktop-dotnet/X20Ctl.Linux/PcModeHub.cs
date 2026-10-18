using System.Text.Json;
using X20Ctl.Zone;
using X20Ctl.Zone.PcMode;
namespace X20Ctl.Linux;

/// <summary>PC mode on Linux, in one place: the saved settings, whether it is switched on, whether the notice was read, and the engine that applies it.
/// The Studio's Buttons, Curves and Macros tabs are its editor, exactly as on Windows. Each player has an every-game profile and optional own profiles per game.</summary>
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

    // ---- which profile is being edited, and which one applies ---------------------------------------------------------------

    readonly string[] editing = [Key, Key, Key, Key];
    /// <summary>"default" (every game) or "game:&lt;exe&gt;": the profile the Studio tabs of this player are editing.</summary>
    public string ProfileKey(int player) => editing[player];
    public string StoreKey(int player) => PcModeStore.For(player, editing[player]);
    public void Pick(int player, string key) { editing[player] = key; Changed?.Invoke(); }
    /// <summary>This player's saved games: (profile key, exe, display name).</summary>
    public IReadOnlyList<(string Key, string Exe, string Name)> Games(int player) => Store.Keys.Select(PcModeStore.Split).Where(k => k.Player == player && k.Key.StartsWith(PcModeStore.GamePrefix))
        .Select(k => (k.Key, k.Key[PcModeStore.GamePrefix.Length..], Store.Get(PcModeStore.For(player, k.Key)).Name ?? k.Key[PcModeStore.GamePrefix.Length..])).OrderBy(g => g.Item3).ToList();

    string? seenForeground; long seenAt = -10000; HashSet<string> seenRunning = [];
    void Look()
    {
        long now = Environment.TickCount64; if (now - seenAt < 500) return; seenAt = now;
        seenForeground = GameApps.Foreground();
        if (!GameApps.CanSeeForeground) seenRunning = GameApps.Running().Select(a => a.Exe).ToHashSet();
    }

    /// <summary>The profile that applies to a player right now: a game's own while it is in front (or running, where the front can't be told), else every game's.</summary>
    public PcModeProfile ProfileFor(int player)
    {
        Look();
        foreach (var (key, exe, _) in Games(player))
            if (GameApps.CanSeeForeground ? seenForeground == exe : seenRunning.Contains(exe)) return Store.Get(PcModeStore.For(player, key));
        return Store.Get(PcModeStore.For(player, Key));
    }

    public string AddGame(int player, string exe, string title)
    {
        string key = PcModeStore.GameKey(exe);
        try
        {
            if (!Store.Has(PcModeStore.For(player, key)))
            {   // a game's settings start as a copy of this player's every-game ones, so nothing changes until they are edited
                var copy = Store.Get(PcModeStore.For(player, Key)); copy.Name = title.Length > 0 ? title[..Math.Min(title.Length, 120)] : exe;
                Store.Save(PcModeStore.For(player, key), copy); Engine.Reload();
            }
            editing[player] = key; Changed?.Invoke(); return "Added. Change anything in the tabs and it applies only while this game is in front.";
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException) { return "Couldn't add it: " + e.Message; }
    }

    public string RemoveGame(int player)
    {
        if (editing[player] == Key) return "";
        try { Store.Remove(StoreKey(player)); Engine.Reload(); editing[player] = Key; Changed?.Invoke(); return "Removed. That game gets the every-game settings again."; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return "Couldn't remove it: " + e.Message; }
    }

    sealed record Saved(bool Enabled, bool NoticeSeen, bool UserOff = false);
    /// <summary>The user switched PC mode off themselves, so entering an X20 Studio must not switch it back on.</summary>
    public bool UserOff { get; private set; }

    PcModeHub()
    {
        Store.Load();
        try { if (File.Exists(statePath) && JsonSerializer.Deserialize<Saved>(File.ReadAllBytes(statePath)) is { } s) { Enabled = s.Enabled; NoticeSeen = s.NoticeSeen; UserOff = s.UserOff; } }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { }
        Engine.ProfileFor = ProfileFor;
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
        var p = Store.Get(StoreKey(s.Slot));
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
        var p = PcModeDrafts.Merge(Store.Get(StoreKey(s.Slot)), s.Buttons, s.Curves, s.Macros);
        p.Sticks = new();
        foreach (var channel in new[] { CurveChannel.LeftStick, CurveChannel.RightStick })
            if (!s.Tune(channel).IsDefault) p.Sticks[channel.ToString()] = s.Tune(channel);
        return p;
    }

    /// <summary>Saves the Studio's drafts as the PC mode profile and tells the engine. Returns the line to show.</summary>
    public string Save(StudioState s) => SaveTo(StoreKey(s.Slot), Build(s));

    public string SaveTo(string storeKey, PcModeProfile next)
    {
        try { next.Validate(); }
        catch (InvalidDataException e) { return Status = "Not saved: " + e.Message; }
        if (!next.IsNeutral && !NoticeSeen) return Status = "Read the notice on the PC mode tab first";
        try { Store.Save(storeKey, next); Engine.Reload(); Status = next.IsNeutral ? "Nothing to apply: your controller goes straight to the game" : Enabled ? "Saved · Applies right away" : "Saved · PC mode is off"; }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException) { Status = "Couldn't save: " + e.Message; }
        Changed?.Invoke(); return Status;
    }
}
