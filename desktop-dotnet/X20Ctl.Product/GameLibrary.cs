using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;
namespace X20Ctl.Product;

public sealed record GameEntry(string Id, string Name, string Store, string? Cover, string? Hero, string? Logo, DateTime? LastPlayed, string LaunchUri);

/// <summary>Finds installed games by reading the launchers' own local files (read-only, no network):
/// Steam library manifests + its local artwork cache, and Epic Games Launcher manifests.</summary>
public static class GameLibrary
{
    private static readonly HashSet<string> SteamTools = ["228980", "1070560", "1391110", "1628350", "961940", "1493710", "2180100", "250820", "1826330"]; // redistributables, Proton/runtime, SteamVR

    public static Task<IReadOnlyList<GameEntry>> ScanAsync() => Task.Run<IReadOnlyList<GameEntry>>(() =>
    {
        var games = new List<GameEntry>();
        if (ReviewSandbox.HideLibrary) return games;
        try { games.AddRange(Steam()); } catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { }
        try { games.AddRange(Epic()); } catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { }
        return games.GroupBy(g => g.Store + g.Id).Select(g => g.First())
            .OrderByDescending(g => g.LastPlayed ?? DateTime.MinValue).ThenBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    });

    public static void Launch(GameEntry game) { if (ReviewSandbox.Active) return; Process.Start(new ProcessStartInfo(game.LaunchUri) { UseShellExecute = true }); }

    private static IEnumerable<GameEntry> Steam()
    {
        string? root = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string;
        if (root == null || !Directory.Exists(root)) yield break;
        root = Path.GetFullPath(root);
        var libraries = new List<string> { root };
        string folders = Path.Combine(root, "steamapps", "libraryfolders.vdf");
        if (File.Exists(folders)) foreach (Match m in Regex.Matches(File.ReadAllText(folders), "\"path\"\\s*\"([^\"]+)\"")) libraries.Add(m.Groups[1].Value.Replace(@"\\", @"\"));
        var played = LastPlayed(root);
        string cache = Path.Combine(root, "appcache", "librarycache");
        foreach (string lib in libraries.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            string apps = Path.Combine(lib, "steamapps"); if (!Directory.Exists(apps)) continue;
            foreach (string manifest in Directory.EnumerateFiles(apps, "appmanifest_*.acf"))
            {
                string text = File.ReadAllText(manifest);
                string? id = Value(text, "appid"), name = Value(text, "name");
                if (id == null || name == null || SteamTools.Contains(id) || name.Contains("Redistributable", StringComparison.OrdinalIgnoreCase)) continue;
                yield return new GameEntry(id, name, "Steam", Art(cache, id, "library_600x900") ?? Art(cache, id, "library_capsule") ?? Art(cache, id, "header"), Art(cache, id, "library_hero"), Art(cache, id, "logo"),
                    played.TryGetValue(id, out var t) ? t : null, "steam://rungameid/" + id);
            }
        }
    }
    private static string? Value(string vdf, string key) { var m = Regex.Match(vdf, $"\"{key}\"\\s*\"([^\"]*)\"", RegexOptions.IgnoreCase); return m.Success ? m.Groups[1].Value : null; }
    private static string? Art(string cache, string id, string kind)
    {
        string dir = Path.Combine(cache, id);
        if (Directory.Exists(dir)) { var f = Directory.EnumerateFiles(dir, kind + ".*", SearchOption.AllDirectories).FirstOrDefault(); if (f != null) return f; }
        var flat = Directory.Exists(cache) ? Directory.EnumerateFiles(cache, $"{id}_{kind}.*").FirstOrDefault() : null;
        return flat;
    }
    private static Dictionary<string, DateTime> LastPlayed(string root)
    {
        var result = new Dictionary<string, DateTime>();
        string users = Path.Combine(root, "userdata"); if (!Directory.Exists(users)) return result;
        foreach (string config in Directory.EnumerateFiles(users, "localconfig.vdf", SearchOption.AllDirectories))
        {
            string text; try { text = File.ReadAllText(config); } catch (IOException) { continue; }
            foreach (Match m in Regex.Matches(text, "\"(\\d+)\"\\s*\\{[^{}]*?\"LastPlayed\"\\s*\"(\\d+)\"", RegexOptions.Singleline))
            {
                var when = DateTimeOffset.FromUnixTimeSeconds(long.Parse(m.Groups[2].Value)).LocalDateTime;
                if (!result.TryGetValue(m.Groups[1].Value, out var old) || when > old) result[m.Groups[1].Value] = when;
            }
        }
        return result;
    }

    private static IEnumerable<GameEntry> Epic()
    {
        string dir = @"C:\ProgramData\Epic\EpicGamesLauncher\Data\Manifests"; if (!Directory.Exists(dir)) yield break;
        foreach (string item in Directory.EnumerateFiles(dir, "*.item"))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(item)); var r = doc.RootElement;
            string? name = r.TryGetProperty("DisplayName", out var n) ? n.GetString() : null, app = r.TryGetProperty("AppName", out var a) ? a.GetString() : null;
            if (name == null || app == null) continue;
            if (r.TryGetProperty("bIsIncompleteInstall", out var inc) && inc.ValueKind == JsonValueKind.True) continue;
            yield return new GameEntry(app, name, "Epic", null, null, null, File.GetLastWriteTime(item), $"com.epicgames.launcher://apps/{Uri.EscapeDataString(app)}?action=launch&silent=true");
        }
    }
}
