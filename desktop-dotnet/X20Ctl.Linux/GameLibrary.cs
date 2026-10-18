using System.Diagnostics;
using System.Text.Json;
namespace X20Ctl.Linux;

/// <summary>The rest of the game shelf: Heroic and Lutris libraries, games and folders added by hand, and the cover art Steam already cached. All local; nothing goes online.</summary>
internal static partial class GameShelf
{
    static string Home => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    /// <summary>The cover Steam already cached on this PC, if any.</summary>
    static string? SteamArt(IEnumerable<string> roots, string appId)
    {
        foreach (string root in roots)
        {
            string dir = Path.Combine(root, "appcache", "librarycache");
            try
            {
                string flat = Path.Combine(dir, appId + "_library_600x900.jpg"); if (File.Exists(flat)) return flat;
                string own = Path.Combine(dir, appId);
                if (Directory.Exists(own)) foreach (string f in Directory.EnumerateFiles(own, "library_600x900.jpg", SearchOption.AllDirectories)) return f;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
        return null;
    }

    /// <summary>Heroic's installed Epic and GOG games, from its own JSON files. Launched through its heroic:// handler.</summary>
    static IEnumerable<InstalledGame> Heroic()
    {
        var found = new List<InstalledGame>();
        foreach (string root in new[] { ".config/heroic", ".var/app/com.heroicgameslauncher.hgl/config/heroic" }.Select(r => Path.Combine(Home, r)).Where(Directory.Exists))
            foreach (var (file, runner) in new[] { ("legendaryConfig/legendary/installed.json", "legendary"), ("gog_store/installed.json", "gog") })
            {
                try
                {
                    string path = Path.Combine(root, file); if (!File.Exists(path)) continue;
                    using var doc = JsonDocument.Parse(File.ReadAllText(path));
                    if (doc.RootElement.ValueKind == JsonValueKind.Object)
                        foreach (var g in doc.RootElement.EnumerateObject())
                            found.Add(new(g.Value.TryGetProperty("title", out var t) ? t.GetString() ?? g.Name : g.Name, $"heroic://launch/{runner}/{g.Name}", "Heroic"));
                    else if (doc.RootElement.TryGetProperty("installed", out var list))
                        foreach (var g in list.EnumerateArray())
                        {
                            string id = g.TryGetProperty("appName", out var a) ? a.GetString() ?? "" : "";
                            if (id.Length > 0) found.Add(new(id, $"heroic://launch/{runner}/{id}", "Heroic"));
                        }
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { }
            }
        return found;
    }

    /// <summary>Lutris's installed games, asked of the lutris command itself.</summary>
    static IEnumerable<InstalledGame> Lutris()
    {
        var found = new List<InstalledGame>();
        try
        {
            using var p = Process.Start(new ProcessStartInfo("lutris", "--list-games --json") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false });
            if (p == null) return found;
            string output = p.StandardOutput.ReadToEnd();
            if (!p.WaitForExit(4000)) { try { p.Kill(); } catch (InvalidOperationException) { } return found; }
            int at = output.IndexOf('['); if (at < 0) return found;
            using var doc = JsonDocument.Parse(output[at..]);
            foreach (var g in doc.RootElement.EnumerateArray())
            {
                string name = g.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "", slug = g.TryGetProperty("slug", out var sl) ? sl.GetString() ?? "" : "";
                if (name.Length > 0 && slug.Length > 0) found.Add(new(name, "lutris:rungame/" + slug, "Lutris"));
            }
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or IOException or JsonException) { }
        return found;
    }

    // ---- games added by hand ------------------------------------------------------------------------------------

    static string AddedPath => Path.Combine(Path.GetDirectoryName(X20Ctl.Zone.PcMode.PcModeStore.DefaultPath)!, "games.json");

    public static List<InstalledGame> Added()
    {
        try { if (File.Exists(AddedPath)) return JsonSerializer.Deserialize<List<InstalledGame>>(File.ReadAllBytes(AddedPath)) ?? []; }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { }
        return [];
    }

    static void SaveAdded(List<InstalledGame> games)
    {
        try { Directory.CreateDirectory(Path.GetDirectoryName(AddedPath)!); File.WriteAllBytes(AddedPath, JsonSerializer.SerializeToUtf8Bytes(games)); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    static bool IsProgram(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext is ".exe" or ".sh" or ".appimage" or ".x86_64") return true;
        try { return !OperatingSystem.IsWindows() && ext.Length == 0 && (File.GetUnixFileMode(path) & UnixFileMode.UserExecute) != 0; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return false; }
    }

    public static int AddProgram(string path)
    {
        var all = Added(); if (!IsProgram(path) || all.Any(g => g.Launch == path)) return 0;
        all.Add(new(Path.GetFileNameWithoutExtension(path), path, "Added")); SaveAdded(all); return 1;
    }

    /// <summary>Each sub-folder with a program in it becomes a game named for the folder; a folder with a program of its own is one game.</summary>
    public static int AddFolder(string folder)
    {
        int added = 0;
        try
        {
            static string? Best(string dir) => Directory.EnumerateFiles(dir).Where(IsProgram)
                .OrderBy(f => Path.GetExtension(f).Equals(".exe", StringComparison.OrdinalIgnoreCase) ? 0 : 1).ThenByDescending(f => new FileInfo(f).Length).FirstOrDefault();
            var all = Added();
            foreach (string dir in Directory.EnumerateDirectories(folder).Prepend(folder))
            {
                string? exe = Best(dir); if (exe == null || all.Any(g => g.Launch == exe)) continue;
                all.Add(new(new DirectoryInfo(dir).Name, exe, "Added")); added++;
            }
            SaveAdded(all);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        return added;
    }

    public static void Forget(InstalledGame game) { var all = Added(); if (all.RemoveAll(g => g.Launch == game.Launch) > 0) SaveAdded(all); }

    static bool IsPath(string launch) => launch.StartsWith('/') || launch.Contains(":\\");

    static void Start(InstalledGame game)
    {
        if (!IsPath(game.Launch)) { Process.Start(new ProcessStartInfo(OperatingSystem.IsWindows() ? "explorer" : "xdg-open", game.Launch) { UseShellExecute = false }); return; }
        string dir = Path.GetDirectoryName(game.Launch) ?? "";
        if (game.Launch.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && !OperatingSystem.IsWindows()) Process.Start(new ProcessStartInfo("wine", $"\"{game.Launch}\"") { UseShellExecute = false, WorkingDirectory = dir });
        else Process.Start(new ProcessStartInfo(game.Launch) { UseShellExecute = false, WorkingDirectory = dir });
    }
}
