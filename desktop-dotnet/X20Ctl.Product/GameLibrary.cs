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
        try { games.AddRange(Steam()); } catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or System.Security.SecurityException) { }
        try { games.AddRange(Epic()); } catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or System.Security.SecurityException) { }
        try { games.AddRange(XboxGamePass()); } catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
        try { games.AddRange(EaApp()); } catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
        try { games.AddRange(Ubisoft()); } catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
        try { games.AddRange(BattleNet()); } catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
        try { games.AddRange(Riot()); } catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { }
        try { games.AddRange(Gog()); } catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
        try { games.AddRange(Added()); } catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { }
        return DeduplicateGames(games)
            .OrderByDescending(g => g.LastPlayed ?? DateTime.MinValue)
            .ThenBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    });

    private static IEnumerable<GameEntry> DeduplicateGames(IEnumerable<GameEntry> raw)
    {
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var g in raw)
        {
            string idKey = g.Store + ":" + g.Id;
            if (!seenIds.Add(idKey)) continue;
            if (!string.IsNullOrWhiteSpace(g.Dir))
            {
                string dirKey = g.Dir.Trim().TrimEnd('\\', '/');
                if (dirKey.Length > 0 && !seenDirs.Add(dirKey)) continue;
            }
            string cleanName = Regex.Replace(g.Name ?? "", @"[^a-zA-Z0-9]", "").ToLowerInvariant();
            if (cleanName.Length > 2 && !seenNames.Add(cleanName)) continue;
            yield return g;
        }
    }

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
    /// <summary>Folders across ready fixed drives where standalone or launcher-less games live.</summary>
    private static IEnumerable<string> KnownFolders()
    {
        string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string pfx86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        yield return Path.Combine(pf, "HoYoPlay", "games");
        yield return Path.Combine(pf, "EA Games");
        yield return Path.Combine(pfx86, "EA Games");
        yield return Path.Combine(local, "Programs");

        string[] driveRoots;
        try
        {
            driveRoots = DriveInfo.GetDrives()
                .Where(d => d.DriveType == DriveType.Fixed && d.IsReady)
                .Select(d => d.RootDirectory.FullName)
                .ToArray();
        }
        catch { driveRoots = [@"C:\", @"D:\"]; }

        string[] folderNames = ["Games", "PC Games", "Epic Games", "GOG Games", @"HoYoPlay\games", "Wuthering Waves"];
        foreach (string drive in driveRoots)
            foreach (string name in folderNames)
                yield return Path.Combine(drive, name);
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
        string? root = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string
            ?? Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", null) as string
            ?? Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam", "InstallPath", null) as string;
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            string pfx86 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam");
            if (Directory.Exists(pfx86)) root = pfx86;
        }

        var libraries = new List<string>();
        if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root))
        {
            root = Path.GetFullPath(root);
            libraries.Add(root);
            string folders = Path.Combine(root, "steamapps", "libraryfolders.vdf");
            if (File.Exists(folders))
                foreach (Match m in Regex.Matches(File.ReadAllText(folders), "\"path\"\\s*\"([^\"]+)\""))
                    libraries.Add(m.Groups[1].Value.Replace(@"\\", @"\"));
        }

        // Also check common SteamLibrary / Steam roots across every ready fixed drive.
        try
        {
            foreach (var d in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady))
            {
                libraries.Add(Path.Combine(d.RootDirectory.FullName, "SteamLibrary"));
                libraries.Add(Path.Combine(d.RootDirectory.FullName, "Steam"));
            }
        }
        catch { }

        if (libraries.Count == 0) yield break;
        var played = root != null ? LastPlayed(root) : new Dictionary<string, DateTime>();
        string cache = root != null ? Path.Combine(root, "appcache", "librarycache") : "";
        foreach (string lib in libraries.Where(Directory.Exists).Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            string apps = Path.Combine(lib, "steamapps"); if (!Directory.Exists(apps)) continue;
            foreach (string manifest in Directory.EnumerateFiles(apps, "appmanifest_*.acf"))
            {
                string text; try { text = File.ReadAllText(manifest); } catch { continue; }
                string? id = Value(text, "appid"), name = Value(text, "name");
                if (id == null || name == null || SteamTools.Contains(id) || name.Contains("Redistributable", StringComparison.OrdinalIgnoreCase)) continue;
                yield return new GameEntry(id, name, "Steam", cache.Length > 0 ? Art(cache, id, "library_600x900") ?? Art(cache, id, "library_capsule") ?? Art(cache, id, "header") : null, cache.Length > 0 ? Art(cache, id, "library_hero") : null, cache.Length > 0 ? Art(cache, id, "logo") : null,
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
        string dir = @"C:\ProgramData\Epic\EpicGamesLauncher\Data\Manifests";
        if (Directory.Exists(dir))
        {
            foreach (string item in Directory.EnumerateFiles(dir, "*.item"))
            {
                JsonDocument? doc = null;
                try { doc = JsonDocument.Parse(File.ReadAllText(item)); } catch { continue; }
                using (doc)
                {
                    var r = doc.RootElement;
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

        string installedDat = @"C:\ProgramData\Epic\UnrealEngineLauncher\LauncherInstalled.dat";
        if (File.Exists(installedDat))
        {
            JsonDocument? doc = null;
            try { doc = JsonDocument.Parse(File.ReadAllText(installedDat)); } catch { doc = null; }
            if (doc != null)
            {
                using (doc)
                {
                    if (doc.RootElement.TryGetProperty("InstallationList", out var list) && list.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var el in list.EnumerateArray())
                        {
                            string? app = el.TryGetProperty("AppName", out var a) ? a.GetString() : null;
                            string? loc = el.TryGetProperty("InstallLocation", out var l) ? l.GetString() : null;
                            if (string.IsNullOrWhiteSpace(app) || string.IsNullOrWhiteSpace(loc) || !Directory.Exists(loc)) continue;
                            if (app.StartsWith("UE_", StringComparison.OrdinalIgnoreCase)) continue;
                            string name = Path.GetFileName(loc.TrimEnd('\\', '/'));
                            string? exe = BestExe(loc, 3);
                            yield return new GameEntry(app, name, "Epic", exe != null ? ExeIcon(exe, "epic-" + app) : null, null, null, Directory.GetLastWriteTime(loc),
                                $"com.epicgames.launcher://apps/{Uri.EscapeDataString(app)}?action=launch&silent=true", loc);
                        }
                    }
                }
            }
        }
    }

    private static IEnumerable<GameEntry> XboxGamePass()
    {
        DriveInfo[] drives;
        try { drives = DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady).ToArray(); }
        catch { yield break; }

        foreach (var d in drives)
        {
            string xboxDir = Path.Combine(d.RootDirectory.FullName, "XboxGames");
            if (!Directory.Exists(xboxDir)) continue;
            IEnumerable<string> subs;
            try { subs = Directory.EnumerateDirectories(xboxDir); } catch { continue; }
            foreach (string sub in subs)
            {
                string contentDir = Path.Combine(sub, "Content");
                string searchDir = Directory.Exists(contentDir) ? contentDir : sub;
                string? exe = BestExe(searchDir, 2);
                if (exe == null) continue;
                string name = Path.GetFileName(sub.TrimEnd('\\', '/'));
                string cfg = Path.Combine(searchDir, "MicrosoftGame.Config");
                if (File.Exists(cfg))
                {
                    try
                    {
                        var m = Regex.Match(File.ReadAllText(cfg), "DefaultDisplayName=\"([^\"]+)\"|DisplayName=\"([^\"]+)\"");
                        if (m.Success)
                        {
                            string parsed = !string.IsNullOrWhiteSpace(m.Groups[1].Value) ? m.Groups[1].Value : m.Groups[2].Value;
                            if (!string.IsNullOrWhiteSpace(parsed)) name = parsed.Trim();
                        }
                    }
                    catch { }
                }
                yield return new GameEntry("xbox:" + name.ToLowerInvariant(), name, "Xbox", ExeIcon(exe, "xbox-" + name.GetHashCode()), null, null, File.GetLastAccessTime(exe), exe, sub);
            }
        }
    }

    private static IEnumerable<GameEntry> EaApp()
    {
        foreach (string regPath in new[] { @"SOFTWARE\WOW6432Node\Electronic Arts", @"SOFTWARE\Electronic Arts", @"SOFTWARE\WOW6432Node\EA Games", @"SOFTWARE\EA Games" })
        {
            using var rootKey = Registry.LocalMachine.OpenSubKey(regPath);
            if (rootKey == null) continue;
            foreach (string sub in rootKey.GetSubKeyNames())
            {
                using var k = rootKey.OpenSubKey(sub);
                string? installDir = k?.GetValue("Install Dir") as string ?? k?.GetValue("InstallDir") as string;
                if (string.IsNullOrWhiteSpace(installDir) || !Directory.Exists(installDir)) continue;
                if (sub.Contains("EA Desktop", StringComparison.OrdinalIgnoreCase) || sub.Contains("Origin", StringComparison.OrdinalIgnoreCase)) continue;
                string? exe = BestExe(installDir, 2);
                if (exe == null) continue;
                string title = (k?.GetValue("DisplayName") as string) ?? sub;
                yield return new GameEntry("ea:" + sub.ToLowerInvariant(), title, "EA", ExeIcon(exe, "ea-" + sub.GetHashCode()), null, null, File.GetLastAccessTime(exe), exe, installDir);
            }
        }
    }

    private static IEnumerable<GameEntry> Ubisoft()
    {
        using var rootKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Ubisoft\Launcher\Installs");
        if (rootKey == null) yield break;
        foreach (string id in rootKey.GetSubKeyNames())
        {
            using var k = rootKey.OpenSubKey(id);
            string? dir = k?.GetValue("InstallDir") as string;
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) continue;
            string? exe = BestExe(dir, 2);
            if (exe == null) continue;
            string name = Path.GetFileName(dir.TrimEnd('\\', '/'));
            yield return new GameEntry(id, name, "Ubisoft", ExeIcon(exe, "ubi-" + id), null, null, File.GetLastAccessTime(exe), $"uplay://launch/{id}/0", dir);
        }
    }

    private static IEnumerable<GameEntry> BattleNet()
    {
        foreach (string regPath in new[] { @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall" })
        {
            using var rootKey = Registry.LocalMachine.OpenSubKey(regPath);
            if (rootKey == null) continue;
            foreach (string sub in rootKey.GetSubKeyNames())
            {
                using var k = rootKey.OpenSubKey(sub);
                string? pub = k?.GetValue("Publisher") as string;
                if (string.IsNullOrWhiteSpace(pub) || !pub.Contains("Blizzard", StringComparison.OrdinalIgnoreCase)) continue;
                string? name = k?.GetValue("DisplayName") as string;
                string? loc = k?.GetValue("InstallLocation") as string;
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(loc) || !Directory.Exists(loc)) continue;
                if (name.Contains("Battle.net", StringComparison.OrdinalIgnoreCase)) continue;
                string? exe = BestExe(loc, 3);
                if (exe == null) continue;
                yield return new GameEntry("bnet:" + sub.ToLowerInvariant(), name, "Battle.net", ExeIcon(exe, "bnet-" + sub.GetHashCode()), null, null, File.GetLastAccessTime(exe), exe, loc);
            }
        }
    }
}
