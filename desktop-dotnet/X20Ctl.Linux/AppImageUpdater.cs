using System.Net.Http;
using System.Security.Cryptography;
namespace X20Ctl.Linux;

/// <summary>Updates the AppImage from inside the app: downloads the new one from the project's GitHub release, checks it against that release's SHA256SUMS.txt,
/// and swaps it in over the running file (Linux lets a running file be replaced by a rename, so the next launch is the new version). Only for an AppImage,
/// which tells the app where it lives in $APPIMAGE; a tarball or Flatpak is updated by its own means.</summary>
internal static class AppImageUpdater
{
    public static string? Path => Environment.GetEnvironmentVariable("APPIMAGE") is { Length: > 0 } p && File.Exists(p) ? p : null;
    public static bool CanInstall => Path != null;
    /// <summary>Set after a successful install: the version waiting for the next launch.</summary>
    public static string? Installed { get; private set; }

    public static async Task<(bool Done, string Message)> Install(UpdateCheck.Release release)
    {
        if (Path is not { } target) return (false, "This copy isn't an AppImage, so it can't update itself. Download the new version from GitHub.");
        if (release.AppImageName == null || release.AppImageUrl == null || release.SumsUrl == null || !UpdateWire.Trusted(release.AppImageUrl) || !UpdateWire.Trusted(release.SumsUrl))
            return (false, "The release doesn't carry an AppImage with checksums, so the update was skipped.");
        string download = target + ".download";
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("X20CTL/" + AppInfo.Version.Replace('+', '.'));
            string? expected = UpdateWire.Checksum(await http.GetStringAsync(release.SumsUrl), release.AppImageName);
            if (expected == null) return (false, "The release's checksum list doesn't include the AppImage, so the update was skipped.");
            await using (var source = await http.GetStreamAsync(release.AppImageUrl))
            await using (var file = new FileStream(download, FileMode.Create, FileAccess.Write, FileShare.None)) await source.CopyToAsync(file);
            string actual; await using (var check = File.OpenRead(download)) actual = Convert.ToHexString(await SHA256.HashDataAsync(check)).ToLowerInvariant();
            if (actual != expected) { File.Delete(download); return (false, "The download didn't match the release's checksum, so it was thrown away."); }
            File.SetUnixFileMode(download, File.GetUnixFileMode(target) | UnixFileMode.UserExecute);
            File.Move(download, target, true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or HttpRequestException or TaskCanceledException)
        {
            try { if (File.Exists(download)) File.Delete(download); } catch (IOException) { }
            return (false, $"Couldn't update the AppImage ({e.Message}). Download the new version from GitHub instead.");
        }
        Installed = release.Version;
        return (true, $"{release.Title} is installed. It starts the next time you open X20CTL.");
    }
}
