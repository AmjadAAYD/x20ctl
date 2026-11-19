using System.Net.Http;
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

    /// <summary>The install this copy is: an AppImage when $APPIMAGE points at one, a Flatpak when /.flatpak-info exists, otherwise a tarball or a package.</summary>
    public static X20Ctl.Zone.InstallKind Kind => CanInstall ? X20Ctl.Zone.InstallKind.LinuxAppImage : File.Exists("/.flatpak-info") ? X20Ctl.Zone.InstallKind.LinuxFlatpak : X20Ctl.Zone.InstallKind.LinuxOther;

    public static async Task<(bool Done, string Message)> Install(UpdateCheck.Release release, IProgress<double>? progress = null, CancellationToken cancel = default)
    {
        if (Path is not { } target) return (false, "This copy isn't an AppImage, so it can't update itself. Download the new version from GitHub.");
        if (release.AppImageName == null || release.AppImageUrl == null || release.SumsUrl == null)
            return (false, "The release doesn't carry an AppImage with checksums, so the update was skipped.");
        var package = X20Ctl.Zone.UpdateKit.Pick(Kind, [new(release.AppImageName, release.AppImageUrl, release.AppImageSize), new("SHA256SUMS.txt", release.SumsUrl, 0)]);
        if (package == null) return (false, "The release doesn't carry an AppImage with checksums, so the update was skipped.");
        string download = target + ".download";
        var fetched = await X20Ctl.Zone.UpdateKit.Fetch(package, download, progress, cancel);
        if (fetched.Outcome != X20Ctl.Zone.UpdateKit.Outcome.Verified) return (false, fetched.Message);
        try
        {
            File.SetUnixFileMode(download, File.GetUnixFileMode(target) | UnixFileMode.UserExecute);
            File.Move(download, target, true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            try { if (File.Exists(download)) File.Delete(download); } catch (IOException) { }
            return (false, $"Couldn't update the AppImage ({e.Message}). Download the new version from GitHub instead.");
        }
        Installed = release.Version;
        return (true, $"{release.Title} is installed. It starts the next time you open X20CTL.");
    }

    /// <summary>Starts the updated AppImage; the caller then closes this process.</summary>
    public static bool Restart()
    {
        try { return Path is { } p && System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(p) { UseShellExecute = false }) != null; }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException) { return false; }
    }
}
