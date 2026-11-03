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
    public sealed record Release(string Tag, string Version, string Title, string Page, string ExeName, string ExeUrl, string SumsUrl, bool Prerelease, long ExeSize = 0);

    public static string Current => Scanner.ScanFlow.AppVersion;
    /// <summary>This copy's version as people see it: V0.016.</summary>
    public static string Shown => X20Ctl.Zone.ReleaseVersions.Display(Current);
    /// <summary>The one-file exe can replace itself; a folder build (the dev builds, the ZIP release) can't.</summary>
    public static bool CanInstall => TestTarget != null || Setup.Installer.IsOneFileExe;
    /// <summary>Review harness only (ignored unless <see cref="ReviewSandbox.Active"/>): a stand-in exe to replace and a fake GitHub, so the real download, check and swap can run against a temp folder.</summary>
    internal static string? TestTarget { get => ReviewSandbox.Active ? testTarget : null; set => testTarget = value; }
    internal static HttpMessageHandler? TestHandler { get => ReviewSandbox.Active ? testHandler : null; set => testHandler = value; }
    static string? testTarget; static HttpMessageHandler? testHandler;
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
            var assets = r.GetProperty("assets").EnumerateArray().Select(a => new X20Ctl.Zone.UpdateAsset(a.GetProperty("name").GetString() ?? "", a.GetProperty("browser_download_url").GetString() ?? "", a.TryGetProperty("size", out var size) ? size.GetInt64() : 0)).ToList();
            if (X20Ctl.Zone.UpdateKit.Pick(X20Ctl.Zone.InstallKind.WindowsExe, assets) is not { } package) continue;
            best = new(tag, version, r.GetProperty("name").GetString() ?? tag, r.GetProperty("html_url").GetString() ?? ReleasesPage, package.File.Name, package.File.Url, package.Sums.Url, pre, package.File.Size);
        }
        return best;
    }

    /// <summary>Downloads, verifies (size and checksum) and swaps in the new exe. Returns false (with the reason) when it can't, and the running exe is then untouched.</summary>
    public static async Task<(bool Done, string Message)> Install(Release release, IProgress<double>? progress = null, CancellationToken cancel = default)
    {
        if (!CanInstall) return (false, "This copy runs from a folder, so it can't update itself.");
        string exe = TestTarget ?? Environment.ProcessPath!, folder = Path.GetDirectoryName(exe)!, download = exe + ".download", old = exe + ".old";
        var package = new X20Ctl.Zone.UpdatePackage(new(release.ExeName, release.ExeUrl, release.ExeSize), new("SHA256SUMS.txt", release.SumsUrl, 0));
        var fetched = await X20Ctl.Zone.UpdateKit.Fetch(package, download, progress, cancel, TestHandler);
        if (fetched.Outcome != X20Ctl.Zone.UpdateKit.Outcome.Verified) return (false, fetched.Message);
        try
        {
            if (File.Exists(old)) File.Delete(old);
            File.Move(exe, old);
            try { File.Move(download, exe); } catch { File.Move(old, exe); throw; }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            try { if (File.Exists(download)) File.Delete(download); } catch (IOException) { }
            return (false, $"Couldn't replace the exe in {folder} ({error.Message}).");
        }
        Installed = release.Version;
        return (true, $"{release.Title} is installed. It starts the next time you open X20CTL.");
    }

    /// <summary>Starts the freshly installed exe (this process is then closed by the caller). False when it couldn't start, so the caller keeps running.</summary>
    /// <summary>The release exe asks for administrator permission at launch, so this process is already elevated and the new one starts without a second prompt. The shell launch (not CreateProcess) is deliberate: if this process ever were not elevated, Windows asks the normal UAC question instead of the start failing.</summary>
    public static bool Restart()
    {
        try
        {
            string exe = Environment.ProcessPath!;
            using var next = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(exe)! });
            return next != null;
        }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException) { return false; }
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
            var (done, message) = await Install(release);
            // the one-file exe unpacks itself the first time it starts (about 2 s): do that now, in the background, rather than at the next open
            if (done) try { string exe = Environment.ProcessPath!; using var warm = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe, "--warm-up") { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(exe)! }); }
                catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException) { }
            return message;
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException or JsonException or IOException or InvalidOperationException or KeyNotFoundException) { return null; }
    }

    /// <summary>Removes the previous exe a past update left beside this one.</summary>
    public static void CleanUp()
    {
        if (!CanInstall) return;
        try { string old = Environment.ProcessPath! + ".old"; if (File.Exists(old)) File.Delete(old); } catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>Orders versions, the old numbering (4.1.0-preview.6) below the new (0.017): see <see cref="X20Ctl.Zone.ReleaseVersions"/>.</summary>
    public static int Compare(string a, string b) => X20Ctl.Zone.ReleaseVersions.Compare(a, b);
}
