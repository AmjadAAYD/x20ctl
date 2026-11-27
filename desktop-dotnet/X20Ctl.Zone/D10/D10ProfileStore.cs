using System.Text.Json;
using System.Text.Json.Serialization;

namespace X20Ctl.Zone.D10;

public sealed class D10SavedConfig
{
    public int Version { get; set; } = 1;
    public D10MotionSettings Settings { get; set; } = new();
    public D10CalibrationData Calibration { get; set; } = D10CalibrationData.Default;
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
        if (!File.Exists(path)) return new D10SavedConfig();
        try
        {
            var cfg = JsonSerializer.Deserialize<D10SavedConfig>(File.ReadAllBytes(path), JsonOptions) ?? new D10SavedConfig();
            cfg.Settings = (cfg.Settings ?? new D10MotionSettings()).Validated();
            cfg.Calibration ??= D10CalibrationData.Default;
            return cfg;
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return new D10SavedConfig();
        }
    }

    public void Save(D10SavedConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        config.Settings = (config.Settings ?? new D10MotionSettings()).Validated();
        config.Calibration ??= D10CalibrationData.Default;
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
