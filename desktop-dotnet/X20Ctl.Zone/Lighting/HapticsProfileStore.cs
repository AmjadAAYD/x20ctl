using System.Text.Json;
using System.Text.Json.Serialization;

namespace X20Ctl.Zone.Lighting;

public sealed class HapticsProfileConfig
{
    public bool Enabled { get; set; }
    public bool AutoDetectGames { get; set; } = true;
    public bool AskOnUnknownGame { get; set; } = true;
    public HapticsGenreProfile ManualProfile { get; set; } = HapticsGenreProfile.Auto;
    public HapticsComfortTier ComfortTier { get; set; } = HapticsComfortTier.Feather;
    public NativeRumbleBlendMode BlendMode { get; set; } = NativeRumbleBlendMode.SmartBlend;
    public bool MenuAutoMute { get; set; } = true;
    public HapticsMusicMood MusicMood { get; set; } = HapticsMusicMood.Vocals;
    public bool PowerSavingMode { get; set; }
    public bool AutoPowerSaverOnLowBattery { get; set; } = true;
    public Dictionary<string, HapticsGenreProfile> PerAppProfiles { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> IgnoredExes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, HapticsTuning> PerGenreTuning { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, HapticsAppTuning> PerAppTuning { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Thread-safe persistent store for Universal Haptics settings, per-game profile bindings,
/// ignore list, comfort tier, and per-genre slider tuning under <c>%AppData%\x20ctl\haptics-profiles.json</c>.
/// </summary>
public sealed class HapticsProfileStore
{
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    readonly object gate = new();
    HapticsProfileConfig config = new();

    public string FilePath { get; }

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "x20ctl",
        "haptics-profiles.json");

    public HapticsProfileStore(string? path = null)
    {
        FilePath = string.IsNullOrWhiteSpace(path) ? DefaultPath : path;
    }

    public void Load()
    {
        lock (gate)
        {
            if (!File.Exists(FilePath))
            {
                config = new();
                return;
            }
            string json = File.ReadAllText(FilePath);
            var parsed = JsonSerializer.Deserialize<HapticsProfileConfig>(json, JsonOptions);
            if (parsed != null)
            {
                // Fresh launches always start with Universal Haptics off until the user enables it.
                parsed.Enabled = false;
                parsed.PerAppProfiles = new Dictionary<string, HapticsGenreProfile>(parsed.PerAppProfiles ?? new(), StringComparer.OrdinalIgnoreCase);
                parsed.IgnoredExes = new HashSet<string>(parsed.IgnoredExes ?? [], StringComparer.OrdinalIgnoreCase);
                parsed.PerGenreTuning = new Dictionary<string, HapticsTuning>(parsed.PerGenreTuning ?? new(), StringComparer.OrdinalIgnoreCase);
                parsed.PerAppTuning = new Dictionary<string, HapticsAppTuning>(parsed.PerAppTuning ?? new(), StringComparer.OrdinalIgnoreCase);
                config = parsed;
            }
        }
    }

    public void Save()
    {
        lock (gate)
            SaveLocked();
    }

    public HapticsProfileConfig Snapshot()
    {
        lock (gate)
        {
            return new HapticsProfileConfig
            {
                Enabled = config.Enabled,
                AutoDetectGames = config.AutoDetectGames,
                AskOnUnknownGame = config.AskOnUnknownGame,
                ManualProfile = config.ManualProfile,
                ComfortTier = config.ComfortTier,
                BlendMode = config.BlendMode,
                MenuAutoMute = config.MenuAutoMute,
                MusicMood = config.MusicMood,
                PowerSavingMode = config.PowerSavingMode,
                AutoPowerSaverOnLowBattery = config.AutoPowerSaverOnLowBattery,
                PerAppProfiles = new Dictionary<string, HapticsGenreProfile>(config.PerAppProfiles, StringComparer.OrdinalIgnoreCase),
                IgnoredExes = new HashSet<string>(config.IgnoredExes, StringComparer.OrdinalIgnoreCase),
                PerGenreTuning = new Dictionary<string, HapticsTuning>(config.PerGenreTuning, StringComparer.OrdinalIgnoreCase),
                PerAppTuning = new Dictionary<string, HapticsAppTuning>(config.PerAppTuning, StringComparer.OrdinalIgnoreCase),
            };
        }
    }

    public void UpdateSettings(Action<HapticsProfileConfig> mutate, bool persist = true)
    {
        lock (gate)
        {
            mutate(config);
            if (persist) SaveLocked();
        }
    }

    public void BindApp(string exe, HapticsGenreProfile profile, bool persist = true)
    {
        string key = NormalizeExe(exe);
        if (key.Length == 0) return;
        lock (gate)
        {
            config.IgnoredExes.Remove(key);
            config.PerAppProfiles[key] = profile;
            if (persist) SaveLocked();
        }
    }

    public void IgnoreApp(string exe, bool persist = true)
    {
        string key = NormalizeExe(exe);
        if (key.Length == 0) return;
        lock (gate)
        {
            config.PerAppProfiles.Remove(key);
            config.IgnoredExes.Add(key);
            if (persist) SaveLocked();
        }
    }

    public bool IsAppIgnored(string? exe)
    {
        string key = NormalizeExe(exe);
        if (key.Length == 0) return false;
        lock (gate)
            return config.IgnoredExes.Contains(key);
    }

    public HapticsTuning GetTuning(HapticsGenreProfile profile)
    {
        lock (gate)
        {
            var baseTuning = config.PerGenreTuning.TryGetValue(profile.ToString(), out var saved)
                ? saved
                : HapticsTuning.DefaultFor(profile);
            return (baseTuning with
            {
                ComfortTier = config.ComfortTier,
                BlendMode = config.BlendMode,
                MenuAutoMute = config.MenuAutoMute,
            }).Normalized();
        }
    }

    public void SetTuning(HapticsGenreProfile profile, HapticsTuning tuning, bool persist = true)
    {
        lock (gate)
        {
            config.PerGenreTuning[profile.ToString()] = tuning.Normalized();
            config.ComfortTier = tuning.ComfortTier;
            config.BlendMode = tuning.BlendMode;
            config.MenuAutoMute = tuning.MenuAutoMute;
            if (persist) SaveLocked();
        }
    }

    public HapticsAppTuning GetAppTuning(string? exe)
    {
        string key = NormalizeExe(exe);
        if (key.Length == 0) return new HapticsAppTuning();
        lock (gate)
        {
            return config.PerAppTuning.TryGetValue(key, out var tuning)
                ? tuning.Normalized()
                : new HapticsAppTuning();
        }
    }

    public bool HasCustomAppTuning(string? exe)
    {
        string key = NormalizeExe(exe);
        if (key.Length == 0) return false;
        lock (gate)
            return config.PerAppTuning.ContainsKey(key);
    }

    public void SetAppTuning(string exe, HapticsAppTuning tuning, bool persist = true)
    {
        string key = NormalizeExe(exe);
        if (key.Length == 0) return;
        lock (gate)
        {
            config.PerAppTuning[key] = tuning.Normalized();
            if (persist) SaveLocked();
        }
    }

    public void ResetAppTuning(string exe, bool persist = true)
    {
        string key = NormalizeExe(exe);
        if (key.Length == 0) return;
        lock (gate)
        {
            if (config.PerAppTuning.Remove(key) && persist)
                SaveLocked();
        }
    }

    public HapticsTuning ApplyAppTuning(HapticsTuning baseTuning, string? exe)
    {
        var app = GetAppTuning(exe);
        if (app.SurfaceGain == 100 && app.CombatGain == 100)
            return baseTuning;

        return (baseTuning with
        {
            TextureDetail = (int)Math.Round(baseTuning.TextureDetail * (app.SurfaceGain / 100.0)),
            ImpactPunch = (int)Math.Round(baseTuning.ImpactPunch * (app.CombatGain / 100.0)),
        }).Normalized();
    }

    public (HapticsGenreProfile Profile, bool IsUnknownGame, string DisplayName) ResolveActive(string? exe, string? windowTitle = null)
    {
        lock (gate)
        {
            if (!config.AutoDetectGames || config.ManualProfile != HapticsGenreProfile.Auto)
            {
                string label = string.IsNullOrWhiteSpace(exe) ? "Manual" : HapticsGameCatalog.Resolve(exe, windowTitle).DisplayName;
                return (config.ManualProfile, false, label);
            }

            string key = NormalizeExe(exe);
            if (key.Length > 0)
            {
                if (config.IgnoredExes.Contains(key))
                    return (HapticsGenreProfile.Auto, false, HapticsGameCatalog.Resolve(key, windowTitle).DisplayName);
                if (config.PerAppProfiles.TryGetValue(key, out var bound))
                    return (bound, false, HapticsGameCatalog.Resolve(key, windowTitle).DisplayName);
            }

            var catalog = HapticsGameCatalog.Resolve(exe, windowTitle);
            if (catalog.Profile is { } known)
                return (known, false, catalog.DisplayName);

            bool unknownGame = key.Length > 0 && !catalog.IsIgnored && !catalog.IsMediaApp;
            return (HapticsGenreProfile.Auto, unknownGame, catalog.DisplayName);
        }
    }

    static string NormalizeExe(string? exe) =>
        string.IsNullOrWhiteSpace(exe) ? string.Empty : Path.GetFileName(exe.Trim()).ToLowerInvariant();

    void SaveLocked()
    {
        string? dir = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        string tmp = FilePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(config, JsonOptions));
        File.Move(tmp, FilePath, overwrite: true);
    }
}
