namespace X20Ctl.Product;

/// <summary>The one place the active theme is named. The theme switch calls <see cref="Set"/>; everything that follows the theme
/// (today the interface sounds, later the intro sound and the look) listens to <see cref="Changed"/>. Names match the folders in
/// Brand/ui: default, pulse, spotlight, rebel, hybrid, blossom, nexus, tide, dusk, volt.</summary>
internal static class ThemeSelection
{
    public static string Current { get; private set; } = "default";
    public static event Action<string>? Changed;
    public static void Set(string? name)
    {
        string next = string.IsNullOrWhiteSpace(name) ? "default" : name.Trim().ToLowerInvariant();
        if (next == Current) return; Current = next; Changed?.Invoke(next);
    }
}
