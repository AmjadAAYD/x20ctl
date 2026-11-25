using System.Text.Json;
using X20Ctl.Zone.PcMode;

namespace X20Ctl.Zone;

public sealed record RadialSlice(string Label, string ActionId);

/// <summary>
/// Configuration and JSON persistence for the 8-slice controller Radial Shortcut Wheel.
/// Triggered by holding the configurable chord (default View + LB) and steering Right Stick.
/// </summary>
public sealed class RadialMenuConfig
{
    public const int SliceCount = 8;

    public bool Enabled { get; set; } = true;
    public string[] TriggerChord { get; set; } = ["SELECT", "LB"];
    public int HoldMs { get; set; } = 180;
    public List<RadialSlice> Slices { get; set; } = DefaultSlices();

    public static string DefaultPath => AppDataPaths.Roaming("radial-menu.json");

    public static List<RadialSlice> DefaultSlices() =>
    [
        new("Mic Mute", "SYS:MicMute"),
        new("Save Clip", "SYS:InstantReplay"),
        new("App Vol +", "SYS:AppVolUp"),
        new("App Vol -", "SYS:AppVolDown"),
        new("Play / Pause", "M:PlayPause"),
        new("Tournament", "SYS:TournamentToggle"),
        new("Eco Mode", "SYS:EcoToggle"),
        new("OBS Overlay", "SYS:OverlayToggle"),
    ];

    public void Validate()
    {
        if (TriggerChord == null || TriggerChord.Length is < 1 or > 4 || TriggerChord.Any(b => string.IsNullOrWhiteSpace(b) || !PadButtons.Mask.ContainsKey(b)))
            throw new InvalidDataException("Radial menu chord must contain 1 to 4 known controller buttons.");
        if (HoldMs is < 50 or > 1500)
            throw new InvalidDataException("Radial hold time must be between 50 and 1500 ms.");
        if (Slices == null || Slices.Count != SliceCount)
            throw new InvalidDataException($"Radial menu requires exactly {SliceCount} slices.");
        foreach (var s in Slices)
            if (s == null || string.IsNullOrWhiteSpace(s.Label) || string.IsNullOrWhiteSpace(s.ActionId))
                throw new InvalidDataException("Every radial slice requires a label and an action.");
    }

    public static RadialMenuConfig Load(string? path = null)
    {
        string target = string.IsNullOrWhiteSpace(path) ? DefaultPath : Path.GetFullPath(path);
        if (!File.Exists(target)) return new();
        try
        {
            var cfg = JsonSerializer.Deserialize<RadialMenuConfig>(File.ReadAllBytes(target));
            if (cfg == null) return new();
            cfg.Validate();
            return cfg;
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or IOException)
        {
            return new();
        }
    }

    public void Save(string? path = null)
    {
        Validate();
        string target = string.IsNullOrWhiteSpace(path) ? DefaultPath : Path.GetFullPath(path);
        string dir = Path.GetDirectoryName(target)!;
        Directory.CreateDirectory(dir);
        string tmp = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllBytes(tmp, JsonSerializer.SerializeToUtf8Bytes(this, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(tmp, target, true);
        }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }
}
