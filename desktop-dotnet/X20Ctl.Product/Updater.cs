using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace X20Ctl.Product;

/// <summary>
/// Tools → "Keep X20CTL up to date" (owner request, 10 Oct 2026). When the player turns it on, each launch asks GitHub's
/// public releases API whether a newer X20CTL exists. For the one-file exe, the new exe is downloaded from that release,
/// checked against the release's own SHA256SUMS.txt, and swapped in beside the running one under the same name, so the
/// next launch is the new version (Windows lets a running exe be renamed). A folder build only says an update exists.
/// Nothing is sent but the request itself; GitHub sees it like any download.
/// </summary>
public static class Updater
{
    public const string Api = "https://api.github.com/repos/AmjadAAYD/x20ctl/releases?per_page=30";
    public const string ReleasesPage = ExternalLinks.Repository + "/releases";
    public sealed record Release(string Tag, string Version, string Title, string Page, string ExeName, string ExeUrl, string SumsUrl, bool Prerelease);

    public static string Current => Scanner.ScanFlow.AppVersion;
    /// <summary>The one-file exe can replace itself; a folder build (the dev builds, the ZIP release) can't.</summary>
    public static bool CanInstall => Setup.Installer.IsOneFileExe;
    public static Release? Ready { get; internal set; }
    public static string? Installed { get; private set; }

    static HttpClient Client()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("X20CTL/" + Current.Replace('+', '.'));
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return http;
    }

    /// <summary>The newest release above this version that carries a Windows exe and checksums. Pre-releases count
    /// while the running version is itself a pre-release.</summary>
    public static async Task<Release?> Latest()
    {
        if (ReviewSandbox.Active) return null;
        using var http = Client();
        using var doc = JsonDocument.Parse(await http.GetStringAsync(Api));
        bool takePre = Current.Contains('-');
        Release? best = null;
        foreach (var r in doc.RootElement.EnumerateArray())
        {
            if (r.GetProperty("draft").GetBoolean()) continue;
            bool pre = r.GetProperty("prerelease").GetBoolean(); if (pre && !takePre) continue;
            string tag = r.GetProperty("tag_name").GetString() ?? "", version = Regex.Replace(tag.TrimStart('v'), "-exe$", "");
            if (Compare(version, Current) <= 0 || best != null && Compare(version, best.Version) <= 0) continue;
            string? exe = null, exeUrl = null, sums = null;
            foreach (var a in r.GetProperty("assets").EnumerateArray())
            {
                string name = a.GetProperty("name").GetString() ?? "", url = a.GetProperty("browser_download_url").GetString() ?? "";
                if (name.EndsWith("-windows-x64.exe", StringComparison.OrdinalIgnoreCase)) (exe, exeUrl) = (name, url);
                if (name == "SHA256SUMS.txt") sums = url;
            }
            if (exe == null || exeUrl == null || sums == null || !Trusted(exeUrl) || !Trusted(sums)) continue;
            best = new(tag, version, r.GetProperty("name").GetString() ?? tag, r.GetProperty("html_url").GetString() ?? ReleasesPage, exe, exeUrl, sums, pre);
        }
        return best;
    }

    static bool Trusted(string url) => url.StartsWith("https://github.com/AmjadAAYD/x20ctl/releases/download/", StringComparison.Ordinal);

    /// <summary>Downloads, verifies and swaps in the new exe. Returns false (with the reason) when it can't.</summary>
    public static async Task<(bool Done, string Message)> Install(Release release)
    {
        if (!CanInstall) return (false, "This copy is a folder build, so it can't update itself. Download the new version from GitHub.");
        string exe = Environment.ProcessPath!, folder = Path.GetDirectoryName(exe)!, download = exe + ".download", old = exe + ".old";
        using var http = Client();
        string sums = await http.GetStringAsync(release.SumsUrl);
        var line = sums.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.EndsWith(" " + release.ExeName) || l.EndsWith("*" + release.ExeName));
        if (line == null || !Regex.IsMatch(line, "^[0-9a-fA-F]{64}")) return (false, "The release's checksum list doesn't include the exe, so the update was skipped.");
        string expected = line[..64].ToLowerInvariant();
        try
        {
            await using (var source = await http.GetStreamAsync(release.ExeUrl))
            await using (var target = new FileStream(download, FileMode.Create, FileAccess.Write, FileShare.None))
                await source.CopyToAsync(target);
            string actual;
            await using (var check = File.OpenRead(download)) actual = Convert.ToHexString(await SHA256.HashDataAsync(check)).ToLowerInvariant();
            if (actual != expected) { File.Delete(download); return (false, "The download didn't match the release's checksum, so it was thrown away."); }
            if (File.Exists(old)) File.Delete(old);
            File.Move(exe, old);
            try { File.Move(download, exe); } catch { File.Move(old, exe); throw; }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            try { if (File.Exists(download)) File.Delete(download); } catch (IOException) { }
            return (false, $"Couldn't replace the exe in {folder} ({error.Message}). Download the new version from GitHub instead.");
        }
        Installed = release.Version;
        return (true, $"{release.Title} is installed. It starts the next time you open X20CTL.");
    }

    /// <summary>At launch, when turned on: check, and for the one-file exe install straight away. Reports one line.</summary>
    public static async Task<string?> AutoRun()
    {
        try
        {
            CleanUp();
            var release = await Latest(); Ready = release;
            if (release == null) return null;
            if (!CanInstall) return $"{release.Title} is out. Open Tools to get it.";
            var (_, message) = await Install(release); return message;
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException or JsonException or IOException or InvalidOperationException or KeyNotFoundException) { return null; }
    }

    /// <summary>Removes the previous exe a past update left beside this one.</summary>
    public static void CleanUp()
    {
        if (!CanInstall) return;
        try { string old = Environment.ProcessPath! + ".old"; if (File.Exists(old)) File.Delete(old); } catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>Compares 4.1.0-preview.5 style versions: the numbers first, then a release beats its pre-releases,
    /// then pre-release parts number by number.</summary>
    public static int Compare(string a, string b)
    {
        static (int[] Core, string[] Pre) Split(string v)
        {
            v = v.Split('+')[0]; int dash = v.IndexOf('-');
            string core = dash < 0 ? v : v[..dash]; string[] pre = dash < 0 ? [] : v[(dash + 1)..].Split('.');
            return (core.Split('.').Select(p => int.TryParse(p, out int n) ? n : 0).Concat([0, 0, 0]).Take(3).ToArray(), pre);
        }
        var (ca, pa) = Split(a); var (cb, pb) = Split(b);
        for (int i = 0; i < 3; i++) if (ca[i] != cb[i]) return ca[i].CompareTo(cb[i]);
        if (pa.Length == 0 || pb.Length == 0) return pb.Length.CompareTo(pa.Length);
        for (int i = 0; i < Math.Min(pa.Length, pb.Length); i++)
        {
            bool na = int.TryParse(pa[i], out int x), nb = int.TryParse(pb[i], out int y);
            int c = na && nb ? x.CompareTo(y) : na ? -1 : nb ? 1 : string.CompareOrdinal(pa[i], pb[i]);
            if (c != 0) return c;
        }
        return pa.Length.CompareTo(pb.Length);
    }
}
