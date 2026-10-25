using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
namespace X20Ctl.Product;

public sealed record GameEntry(string Id, string Name, string Store, string? Cover, string? Hero, string? Logo, DateTime? LastPlayed, string LaunchUri, string? Dir = null, string? LaunchArgs = null);

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
        try { games.AddRange(Riot()); } catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { }
        try { games.AddRange(Gog()); } catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
        try { games.AddRange(Added()); } catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { }
        return games.GroupBy(g => g.Store + g.Id).Select(g => g.First())
            .OrderByDescending(g => g.LastPlayed ?? DateTime.MinValue).ThenBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    });

    public static void Launch(GameEntry game)
    {
        if (ReviewSandbox.Active) return;
        var info = new ProcessStartInfo(game.LaunchUri) { UseShellExecute = true };
        if (game.LaunchArgs != null) info.Arguments = game.LaunchArgs;
        if (game.Dir != null && Directory.Exists(game.Dir) && File.Exists(game.LaunchUri)) info.WorkingDirectory = Path.GetDirectoryName(game.LaunchUri)!;
        Process.Start(info);
    }

    /// <summary>True once a process is running from the game's own folder: the game really started, not just the click.</summary>
    public static bool IsRunning(GameEntry game)
    {
        string? exact = File.Exists(game.LaunchUri) && game.Store is "Added" or "Folder" or "GOG" ? Path.GetFullPath(game.LaunchUri) : null;
        if (exact == null && game.Dir == null) return false;
        string? dir = game.Dir == null ? null : Path.GetFullPath(game.Dir).TrimEnd('\\') + "\\";
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                if (p.MainModule?.FileName is { } f && (exact != null
                    ? f.Equals(exact, StringComparison.OrdinalIgnoreCase)
                    : dir != null && f.StartsWith(dir, StringComparison.OrdinalIgnoreCase) && !NotAGame.IsMatch(Path.GetFileName(f)))) return true;
            }
            catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException) { }
            finally { p.Dispose(); }
        }
        return false;
    }

    // ---- games the player adds by hand: single exes and whole folders (portable or launcher-less installs) ----
    private static string AddedPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "x20ctl", "games.json");
    private sealed class AddedList { public List<string> Exes { get; set; } = []; public List<string> Folders { get; set; } = []; }
    private static AddedList LoadAdded() { try { return File.Exists(AddedPath) ? JsonSerializer.Deserialize<AddedList>(File.ReadAllText(AddedPath)) ?? new() : new(); } catch (Exception e) when (e is IOException or JsonException) { return new(); } }
    private static void SaveAdded(AddedList a) { Directory.CreateDirectory(Path.GetDirectoryName(AddedPath)!); File.WriteAllText(AddedPath, JsonSerializer.Serialize(a)); }
    public static void AddGame(string exe) { var a = LoadAdded(); if (!a.Exes.Contains(exe, StringComparer.OrdinalIgnoreCase)) { a.Exes.Add(exe); SaveAdded(a); } }
    public static void AddFolder(string folder) { var a = LoadAdded(); if (!a.Folders.Contains(folder, StringComparer.OrdinalIgnoreCase)) { a.Folders.Add(folder); SaveAdded(a); } }

    private static IEnumerable<GameEntry> Added()
    {
        var a = LoadAdded();
        foreach (string exe in a.Exes) if (File.Exists(exe)) yield return FromExe(exe, Path.GetFileNameWithoutExtension(exe), "Added");
        foreach (string folder in a.Folders.Concat(KnownFolders()).Where(Directory.Exists))
        {
            var direct = BestExe(folder, 0);
            if (direct != null) { yield return FromExe(direct, Path.GetFileName(folder.TrimEnd('\\')), "Folder"); continue; }
            List<string> subs; try { subs = Directory.EnumerateDirectories(folder).ToList(); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { continue; }
            foreach (string sub in subs) if (BestExe(sub, 2) is { } exe) yield return FromExe(exe, Path.GetFileName(sub), "Folder", sub);
        }
    }
    /// <summary>Folders where launcher-less games usually sit (HoYoPlay keeps its games under its own folder).</summary>
    private static IEnumerable<string> KnownFolders()
    {
        string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        yield return Path.Combine(pf, "HoYoPlay", "games"); yield return @"C:\Games"; yield return @"D:\Games";
    }
    private static readonly Regex NotAGame = new(@"unins|setup|install|crash|redist|dxsetup|dotnet|update|patch|helper|report|cef|webview|launcher", RegexOptions.IgnoreCase);
    /// <summary>The folder's main program: the largest .exe that isn't an installer, updater or crash reporter.</summary>
    private static string? BestExe(string folder, int depth)
    {
        try
        {
            var opts = new EnumerationOptions { RecurseSubdirectories = depth > 0, MaxRecursionDepth = Math.Max(depth, 1), IgnoreInaccessible = true };
            return Directory.EnumerateFiles(folder, "*.exe", opts).Where(f => !NotAGame.IsMatch(Path.GetFileName(f))).Select(f => new FileInfo(f)).Where(f => f.Length > 1_000_000)
                .OrderByDescending(f => f.Length).FirstOrDefault()?.FullName;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; }
    }
    private static GameEntry FromExe(string exe, string name, string store, string? dir = null)
    {
        dir ??= Path.GetDirectoryName(exe)!;
        string key = Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(System.Text.Encoding.UTF8.GetBytes(exe.ToLowerInvariant())))[..12];
        return new GameEntry("exe:" + exe.ToLowerInvariant(), name, store, ExeIcon(exe, "exe-" + key), null, null, File.GetLastAccessTime(exe), exe, dir);
    }

    private static IEnumerable<GameEntry> Riot()
    {
        string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Riot Games");
        string installs = Path.Combine(root, "RiotClientInstalls.json"), meta = Path.Combine(root, "Metadata");
        if (!File.Exists(installs) || !Directory.Exists(meta)) yield break;
        string? client; using (var doc = JsonDocument.Parse(File.ReadAllText(installs)))
            client = doc.RootElement.TryGetProperty("rc_live", out var l) ? l.GetString() : doc.RootElement.TryGetProperty("rc_default", out var d) ? d.GetString() : null;
        if (client == null || !File.Exists(client)) yield break;
        foreach (string dir in Directory.EnumerateDirectories(meta))
        {
            string yaml = Path.Combine(dir, Path.GetFileName(dir) + ".product_settings.yaml"); if (!File.Exists(yaml)) continue;
            var m = Regex.Match(File.ReadAllText(yaml), "product_install_full_path:\\s*\"?([^\"\\r\\n]+)\"?"); if (!m.Success || !Directory.Exists(m.Groups[1].Value.Trim())) continue;
            string where = m.Groups[1].Value.Trim(), product = Path.GetFileName(dir), id = product.Split('.')[0], patch = product.Contains('.') ? product[(product.IndexOf('.') + 1)..] : "live";
            string name = id switch { "valorant" => "VALORANT", "league_of_legends" => "League of Legends", "bacon" or "lor" => "Legends of Runeterra", "tft" => "Teamfight Tactics", _ => string.Join(' ', id.Split('_').Select(w => char.ToUpper(w[0]) + w[1..])) };
            // VALORANT keeps its real program four folders down and a small VALORANT.exe at the top, so look in the
            // product folder first, then deep, and last use the Riot Client's own icon rather than a letter tile
            string? exe = Directory.EnumerateFiles(where, "*.exe").FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).Replace(" ", "").Replace("_", "").StartsWith(id.Replace("_", ""), StringComparison.OrdinalIgnoreCase))
                ?? BestExe(where, 5) ?? client;
            yield return new GameEntry("riot:" + product, name, "Riot", exe != null ? ExeIcon(exe, "riot-" + id) : null, null, null, File.GetLastWriteTime(yaml), client, where, $"--launch-product={id} --launch-patchline={patch}");
        }
    }

    private static IEnumerable<GameEntry> Gog()
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\GOG.com\Games"); if (key == null) yield break;
        foreach (string id in key.GetSubKeyNames())
        {
            using var k = key.OpenSubKey(id); string? name = k?.GetValue("gameName") as string, exe = k?.GetValue("exe") as string, path = k?.GetValue("path") as string;
            if (name == null || exe == null || !File.Exists(exe)) continue;
            yield return new GameEntry("gog:" + id, name, "GOG", ExeIcon(exe, "gog-" + id), null, null, File.GetLastAccessTime(exe), exe, path ?? Path.GetDirectoryName(exe));
        }
    }

    // ---- a real picture for a game with no store art: the program's own 256-pixel icon, kept as a PNG ----
    [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory { [PreserveSig] int GetImage(SIZE size, int flags, out IntPtr bitmap); }
    [StructLayout(LayoutKind.Sequential)] private struct SIZE { public int cx, cy; }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)] private static extern void SHCreateItemFromParsingName(string path, IntPtr bind, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory item);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr h);

    public static string? ExeIcon(string exe, string key)
    {
        try
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "x20ctl", "game-art"), png = Path.Combine(dir, key + ".png");
            if (File.Exists(png)) return png;
            if (ReviewSandbox.Active) return null;
            Directory.CreateDirectory(dir);
            var iid = typeof(IShellItemImageFactory).GUID; SHCreateItemFromParsingName(exe, IntPtr.Zero, ref iid, out var factory);
            if (factory.GetImage(new SIZE { cx = 256, cy = 256 }, 0x4 | 0x1, out var hbmp) != 0 || hbmp == IntPtr.Zero) return null;
            try
            {
                var source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(hbmp, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(source));
                using var file = File.Create(png); encoder.Save(file);
            }
            finally { DeleteObject(hbmp); }
            return png;
        }
        catch (Exception e) when (e is IOException or COMException or UnauthorizedAccessException or ArgumentException) { return null; }
    }

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
                    played.TryGetValue(id, out var t) ? t : null, "steam://rungameid/" + id, Value(text, "installdir") is { } folder ? Path.Combine(apps, "common", folder) : null);
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
            string? where = r.TryGetProperty("InstallLocation", out var il) ? il.GetString() : null, run = r.TryGetProperty("LaunchExecutable", out var le) ? le.GetString() : null;
            string? exe = where != null && run != null ? Path.Combine(where, run) : null;
            yield return new GameEntry(app, name, "Epic", exe != null && File.Exists(exe) ? ExeIcon(exe, "epic-" + app) : null, null, null, File.GetLastWriteTime(item),
                $"com.epicgames.launcher://apps/{Uri.EscapeDataString(app)}?action=launch&silent=true", where);
        }
    }
}
