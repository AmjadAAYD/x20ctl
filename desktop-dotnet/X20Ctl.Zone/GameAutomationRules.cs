using System.Text.Json;
using System.Text.Json.Serialization;
using X20Ctl.Zone.Lighting;

namespace X20Ctl.Zone;

public sealed class GameAutomationRule
{
    public string ExeName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? RgbPresetId { get; set; }
    public HapticsGenreProfile? HapticsProfile { get; set; }
    public bool? ForceEcoMode { get; set; }
    public int? RgbBrightnessPct { get; set; }
}

/// <summary>
/// Unified per-exe automation store (Eco mode auto-switch, RGB preset/brightness dimming,
/// and Universal Haptics genre binding) saved under <c>game-automation.json</c>.
/// </summary>
public sealed class GameAutomationStore(string? path = null)
{
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    sealed class FileData
    {
        public int Version { get; set; } = 1;
        public List<GameAutomationRule> Rules { get; set; } = [];
    }

    readonly string filePath = string.IsNullOrWhiteSpace(path) ? DefaultPath : Path.GetFullPath(path);
    readonly object gate = new();
    List<GameAutomationRule> rules = [];
    bool loaded;

    public static string DefaultPath => AppDataPaths.Roaming("game-automation.json");

    public IReadOnlyList<GameAutomationRule> Rules
    {
        get { lock (gate) { EnsureLoaded(); return rules.ToArray(); } }
    }

    public static string NormalizeExe(string? exe)
    {
        if (string.IsNullOrWhiteSpace(exe)) return "";
        string clean = Path.GetFileName(exe.Trim()).ToLowerInvariant();
        if (clean.Length > 0 && !clean.EndsWith(".exe", StringComparison.Ordinal)) clean += ".exe";
        return clean;
    }

    public void Load()
    {
        lock (gate)
        {
            loaded = true;
            rules.Clear();
            if (!File.Exists(filePath)) return;
            try
            {
                var parsed = JsonSerializer.Deserialize<FileData>(File.ReadAllBytes(filePath), JsonOptions);
                if (parsed?.Version != 1 || parsed.Rules == null) return;
                rules = parsed.Rules
                    .Where(r => r != null && !string.IsNullOrWhiteSpace(r.ExeName))
                    .Select(NormalizeRule)
                    .GroupBy(r => r.ExeName, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .ToList();
            }
            catch (Exception ex) when (ex is JsonException or IOException) { }
        }
    }

    public GameAutomationRule? FindForExe(string? exe)
    {
        string key = NormalizeExe(exe);
        if (key.Length == 0) return null;
        lock (gate)
        {
            EnsureLoaded();
            return rules.FirstOrDefault(r => string.Equals(r.ExeName, key, StringComparison.OrdinalIgnoreCase));
        }
    }

    public void Upsert(GameAutomationRule rule)
    {
        var norm = NormalizeRule(rule);
        if (norm.ExeName.Length == 0) throw new ArgumentException("Executable name is required.", nameof(rule));
        lock (gate)
        {
            EnsureLoaded();
            rules.RemoveAll(r => string.Equals(r.ExeName, norm.ExeName, StringComparison.OrdinalIgnoreCase));
            rules.Add(norm);
            WriteLocked();
        }
    }

    public bool Remove(string exe)
    {
        string key = NormalizeExe(exe);
        if (key.Length == 0) return false;
        lock (gate)
        {
            EnsureLoaded();
            int n = rules.RemoveAll(r => string.Equals(r.ExeName, key, StringComparison.OrdinalIgnoreCase));
            if (n == 0) return false;
            WriteLocked();
            return true;
        }
    }

    static GameAutomationRule NormalizeRule(GameAutomationRule r) => new()
    {
        ExeName = NormalizeExe(r.ExeName),
        DisplayName = string.IsNullOrWhiteSpace(r.DisplayName) ? NormalizeExe(r.ExeName) : r.DisplayName.Trim(),
        RgbPresetId = string.IsNullOrWhiteSpace(r.RgbPresetId) ? null : r.RgbPresetId.Trim(),
        HapticsProfile = r.HapticsProfile,
        ForceEcoMode = r.ForceEcoMode,
        RgbBrightnessPct = r.RgbBrightnessPct is { } b ? Math.Clamp(b, 0, 100) : null,
    };

    void EnsureLoaded() { if (!loaded) Load(); }

    void WriteLocked()
    {
        string dir = Path.GetDirectoryName(filePath)!;
        Directory.CreateDirectory(dir);
        string tmp = filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllBytes(tmp, JsonSerializer.SerializeToUtf8Bytes(new FileData { Rules = rules }, JsonOptions));
            File.Move(tmp, filePath, true);
        }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }
}
