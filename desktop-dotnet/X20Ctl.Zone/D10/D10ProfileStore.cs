using System.Text.Json;
using System.Text.Json.Serialization;

namespace X20Ctl.Zone.D10;

public sealed class D10NamedProfile
{
    public string Id { get; set; } = "default";
    public string Name { get; set; } = "Default Motion Profile";
    public int? QuickSlot { get; set; } = 1;
    public List<string> GameExes { get; set; } = new();
    public D10MotionSettings Settings { get; set; } = new();
}

public sealed class D10SavedConfig
{
    public int Version { get; set; } = 2;
    public string ActiveProfileId { get; set; } = "default";
    public bool AutoSwitchByGame { get; set; } = true;
    public List<D10NamedProfile> Profiles { get; set; } = new();
    public D10MotionSettings Settings { get; set; } = new();
    public D10CalibrationData Calibration { get; set; } = D10CalibrationData.Default;

    public D10SavedConfig EnsureDefaults()
    {
        Settings = (Settings ?? new D10MotionSettings()).Validated();
        Calibration ??= D10CalibrationData.Default;
        Profiles ??= new List<D10NamedProfile>();

        if (Profiles.Count == 0)
        {
            Profiles.Add(new D10NamedProfile
            {
                Id = "default",
                Name = "Default Motion Profile",
                QuickSlot = 1,
                Settings = Settings.Clone().Validated(),
            });
            ActiveProfileId = "default";
        }
        else
        {
            foreach (var p in Profiles)
            {
                p.Id = string.IsNullOrWhiteSpace(p.Id) ? Guid.NewGuid().ToString("N")[..8] : p.Id.Trim();
                p.Name = string.IsNullOrWhiteSpace(p.Name) ? "Untitled Profile" : p.Name.Trim();
                p.GameExes = (p.GameExes ?? new())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim().ToLowerInvariant())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                p.QuickSlot = p.QuickSlot is >= 1 and <= 4 ? p.QuickSlot : null;
                p.Settings = (p.Settings ?? new D10MotionSettings()).Validated();
            }

            if (string.IsNullOrWhiteSpace(ActiveProfileId) || !Profiles.Any(p => p.Id == ActiveProfileId))
                ActiveProfileId = Profiles[0].Id;

            Settings = GetActiveProfile().Settings.Clone().Validated();
        }

        Version = 2;
        return this;
    }

    public D10NamedProfile GetActiveProfile() =>
        Profiles.FirstOrDefault(p => p.Id == ActiveProfileId) ?? Profiles[0];

    public D10NamedProfile SetActiveProfile(string id)
    {
        EnsureDefaults();
        var found = Profiles.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
        if (found != null)
        {
            ActiveProfileId = found.Id;
            Settings = found.Settings.Clone().Validated();
        }
        return GetActiveProfile();
    }

    public D10NamedProfile? FindByQuickSlot(int slot)
    {
        if (slot is < 1 or > 4) return null;
        EnsureDefaults();
        return Profiles.FirstOrDefault(p => p.QuickSlot == slot);
    }

    public D10NamedProfile? FindByProcessExe(string? exe)
    {
        if (string.IsNullOrWhiteSpace(exe)) return null;
        EnsureDefaults();
        string needle = Path.GetFileName(exe.Trim()).ToLowerInvariant();
        return Profiles.FirstOrDefault(p => p.GameExes.Any(g => string.Equals(g, needle, StringComparison.OrdinalIgnoreCase)));
    }
}

public sealed class D10ProfileStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string DefaultPath => AppDataPaths.Roaming("d10-native.json");

    private readonly string path;

    public D10ProfileStore(string? path = null)
    {
        this.path = Path.GetFullPath(path ?? DefaultPath);
    }

    public D10SavedConfig Load()
    {
        if (!File.Exists(path)) return new D10SavedConfig().EnsureDefaults();
        try
        {
            var cfg = JsonSerializer.Deserialize<D10SavedConfig>(File.ReadAllBytes(path), JsonOptions) ?? new D10SavedConfig();
            return cfg.EnsureDefaults();
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return new D10SavedConfig().EnsureDefaults();
        }
    }

    public void Save(D10SavedConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        config.EnsureDefaults();
        string dir = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(dir);
        string tmp = Path.Combine(dir, $".d10-{Guid.NewGuid():N}.tmp");
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(config, JsonOptions);
        try
        {
            using (var fs = new FileStream(tmp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                fs.Write(bytes);
                fs.Flush(true);
            }
            File.Move(tmp, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }
}
