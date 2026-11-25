using System.Text.Json;
using System.Text.Json.Serialization;

namespace X20Ctl.Zone.Lighting;

/// <summary>Atomic local JSON persistence for saved custom lighting presets, last effect, and foreground-game auto rules.</summary>
public sealed class LightingPresetsStore
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private sealed class Library
    {
        public int Version { get; set; } = 1;
        public List<LightingPreset> Presets { get; set; } = [];
        public List<GameLightingRule> GameRules { get; set; } = [];
        public LightingEffectSpec? LastEffect { get; set; }
    }

    private readonly string path;
    private Library data = new();
    private bool loaded;

    public LightingPresetsStore(string path) => this.path = Path.GetFullPath(path);

    public IReadOnlyList<LightingPreset> Presets => data.Presets.AsReadOnly();
    public IReadOnlyList<GameLightingRule> GameRules => data.GameRules.AsReadOnly();
    public LightingEffectSpec? LastEffect => data.LastEffect;

    public static string DefaultPath => AppDataPaths.Local("lighting", "presets.json");

    public void Load()
    {
        loaded = true;
        if (!File.Exists(path)) { data = new(); return; }
        var parsed = JsonSerializer.Deserialize<Library>(File.ReadAllBytes(path), JsonOptions);
        if (parsed == null || parsed.Version != 1)
            throw new InvalidDataException("Unsupported lighting preset library version.");
        data = new Library
        {
            Version = 1,
            Presets = (parsed.Presets ?? [])
                .Where(p => p != null && !string.IsNullOrWhiteSpace(p.Id) && !string.IsNullOrWhiteSpace(p.Name))
                .Select(p => p with { Name = NormalizeName(p.Name), Spec = (p.Spec ?? new()).Normalized() })
                .Take(100)
                .ToList(),
            GameRules = (parsed.GameRules ?? [])
                .Where(r => r != null && !string.IsNullOrWhiteSpace(r.ExeName) && !string.IsNullOrWhiteSpace(r.PresetId))
                .Select(r => new GameLightingRule(NormalizeExe(r.ExeName), r.PresetId.Trim()))
                .GroupBy(r => r.ExeName, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList(),
            LastEffect = parsed.LastEffect?.Normalized(),
        };
    }

    public LightingPreset SavePreset(string name, LightingEffectSpec spec, string? existingId = null)
    {
        EnsureLoaded();
        string cleanName = NormalizeName(name);
        var cleanSpec = spec.Normalized();
        LightingPreset saved;
        if (!string.IsNullOrWhiteSpace(existingId) && data.Presets.FindIndex(p => p.Id == existingId) is >= 0 and int idx)
        {
            saved = data.Presets[idx] with { Name = cleanName, Spec = cleanSpec, UpdatedUtc = DateTimeOffset.UtcNow };
            data.Presets[idx] = saved;
        }
        else
        {
            if (data.Presets.Count >= 100) throw new InvalidOperationException("Preset limit (100) reached.");
            saved = new(Guid.NewGuid().ToString("N"), cleanName, cleanSpec, DateTimeOffset.UtcNow);
            data.Presets.Add(saved);
        }
        Save();
        return saved;
    }

    public bool DeletePreset(string id)
    {
        EnsureLoaded();
        int removed = data.Presets.RemoveAll(p => p.Id == id);
        if (removed == 0) return false;
        data.GameRules.RemoveAll(r => r.PresetId == id);
        Save();
        return true;
    }

    public LightingPreset? FindPreset(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        EnsureLoaded();
        return data.Presets.FirstOrDefault(p => p.Id == id)
            ?? LightingEffectEngine.BuiltInPresets.FirstOrDefault(p => p.Id == id);
    }

    public void SetGameRule(string exeName, string presetId)
    {
        EnsureLoaded();
        string exe = NormalizeExe(exeName);
        if (FindPreset(presetId) == null) throw new KeyNotFoundException("Preset does not exist.");
        data.GameRules.RemoveAll(r => string.Equals(r.ExeName, exe, StringComparison.OrdinalIgnoreCase));
        data.GameRules.Add(new(exe, presetId));
        Save();
    }

    public bool RemoveGameRule(string exeName)
    {
        EnsureLoaded();
        string exe = NormalizeExe(exeName);
        int removed = data.GameRules.RemoveAll(r => string.Equals(r.ExeName, exe, StringComparison.OrdinalIgnoreCase));
        if (removed == 0) return false;
        Save();
        return true;
    }

    public LightingPreset? ResolveForExe(string? exeName)
    {
        if (string.IsNullOrWhiteSpace(exeName)) return null;
        EnsureLoaded();
        string exe = NormalizeExe(exeName);
        var rule = data.GameRules.FirstOrDefault(r => string.Equals(r.ExeName, exe, StringComparison.OrdinalIgnoreCase));
        return rule == null ? null : FindPreset(rule.PresetId);
    }

    public void RememberLastEffect(LightingEffectSpec spec)
    {
        EnsureLoaded();
        data.LastEffect = spec.Normalized();
        Save();
    }

    static string NormalizeName(string name)
    {
        if (name == null) throw new InvalidDataException("Preset name is missing.");
        string trimmed = name.Trim();
        if (trimmed.Length is < 1 or > 60 || trimmed.Any(char.IsControl))
            throw new ArgumentException("Use a preset name of 1-60 printable characters.");
        return trimmed;
    }

    public static string NormalizeExe(string exeName)
    {
        string name = Path.GetFileName(exeName.Trim()).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Executable name is required.");
        if (!name.EndsWith(".exe", StringComparison.Ordinal)) name += ".exe";
        return name;
    }

    void EnsureLoaded() { if (!loaded) Load(); }

    void Save()
    {
        string dir = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(dir);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(data, JsonOptions);
        string temp = Path.Combine(dir, ".lighting-presets-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(true);
            }
            File.Move(temp, path, true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }
}
