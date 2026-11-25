namespace X20Ctl.Zone;

/// <summary>
/// Resolves persistent storage locations for X20CTL configs, profiles, and telemetry.
/// Supports Portable USB-Stick Mode automatically when a "portable.txt" marker file or a "./data" folder exists
/// next to the executable (or <see cref="OverrideBaseDirectory"/> in tests).
/// </summary>
public static class AppDataPaths
{
    public static string? OverrideBaseDirectory { get; set; }

    public static string BaseDirectory =>
        string.IsNullOrWhiteSpace(OverrideBaseDirectory) ? AppContext.BaseDirectory : OverrideBaseDirectory;

    public static bool IsPortable
    {
        get
        {
            string baseDir = BaseDirectory;
            return File.Exists(Path.Combine(baseDir, "portable.txt"))
                || Directory.Exists(Path.Combine(baseDir, "data"));
        }
    }

    public static string RoamingRoot => IsPortable
        ? Path.Combine(BaseDirectory, "data", "roaming")
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "x20ctl");

    public static string LocalRoot => IsPortable
        ? Path.Combine(BaseDirectory, "data", "local")
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "x20ctl");

    public static string Roaming(params string[] parts) =>
        Path.Combine([RoamingRoot, .. parts]);

    public static string Local(params string[] parts) =>
        Path.Combine([LocalRoot, .. parts]);
}
