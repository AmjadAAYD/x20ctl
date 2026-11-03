using System.Net.Http;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
namespace X20Ctl.Zone;

/// <summary>How this copy of X20CTL was installed, which decides the release file an update takes.</summary>
public enum InstallKind { WindowsExe, WindowsFolder, LinuxAppImage, LinuxFlatpak, LinuxOther }

/// <summary>One file attached to a GitHub release.</summary>
public sealed record UpdateAsset(string Name, string Url, long Size);

/// <summary>The release file for an install kind, and the checksum list that vouches for it.</summary>
public sealed record UpdatePackage(UpdateAsset File, UpdateAsset Sums);

/// <summary>One-click update, the parts shared by Windows and Linux and free of UI: which release file fits the install, a verified download
/// with progress and cancel, and the wording of the button. Only this project's own GitHub release downloads are ever fetched.</summary>
public static class UpdateKit
{
    public const string TrustedPrefix = "https://github.com/AmjadAAYD/x20ctl/releases/download/";
    public static bool Trusted(string url) => url.StartsWith(TrustedPrefix, StringComparison.Ordinal);

    /// <summary>The file an install takes from a release, or null when that install can't update itself (a folder build or tarball:
    /// the release page is opened instead). Needs SHA256SUMS.txt beside it; both must come from the project's releases.</summary>
    public static UpdatePackage? Pick(InstallKind kind, IEnumerable<UpdateAsset> assets)
    {
        var list = assets.Where(a => Trusted(a.Url)).ToList();
        var sums = list.FirstOrDefault(a => a.Name == "SHA256SUMS.txt");
        var file = kind switch
        {
            InstallKind.WindowsExe => list.FirstOrDefault(a => a.Name.EndsWith("-windows-x64.exe", StringComparison.OrdinalIgnoreCase) && !a.Name.EndsWith("-setup.exe", StringComparison.OrdinalIgnoreCase)),
            InstallKind.LinuxAppImage => list.FirstOrDefault(a => a.Name.EndsWith("-linux-x86_64.AppImage", StringComparison.Ordinal)),
            InstallKind.LinuxFlatpak => list.FirstOrDefault(a => a.Name.EndsWith("-linux-x86_64.flatpak", StringComparison.Ordinal)),
            _ => null,
        };
        return file == null || sums == null ? null : new(file, sums);
    }

    /// <summary>The lowercase SHA-256 listed for a file in a sha256sum-style list ("hash  name" or "hash *name"), or null.</summary>
    public static string? Checksum(string sums, string fileName)
    {
        foreach (string raw in sums.Split('\n'))
        {
            string line = raw.Trim();
            if (!(line.EndsWith(" " + fileName, StringComparison.Ordinal) || line.EndsWith("*" + fileName, StringComparison.Ordinal))) continue;
            return Regex.IsMatch(line, "^[0-9a-fA-F]{64}(\\s|\\*)") ? line[..64].ToLowerInvariant() : null;
        }
        return null;
    }

    public enum Outcome { Verified, Cancelled, Failed }
    public sealed record Result(Outcome Outcome, string Message);

    /// <summary>Downloads <paramref name="package"/> to <paramref name="destination"/> and checks its size (the release's own figure) and SHA-256
    /// (from the release's SHA256SUMS.txt). Anything but Verified leaves no file at <paramref name="destination"/>; nothing else is touched.</summary>
    public static async Task<Result> Fetch(UpdatePackage package, string destination, IProgress<double>? progress, CancellationToken cancel, HttpMessageHandler? handler = null)
    {
        try
        {
            using var http = handler == null ? new HttpClient { Timeout = TimeSpan.FromMinutes(30) } : new HttpClient(handler, false) { Timeout = TimeSpan.FromMinutes(30) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("X20CTL-updater");
            string? expected = Checksum(await http.GetStringAsync(package.Sums.Url, cancel), package.File.Name);
            if (expected == null) return new(Outcome.Failed, "The release's checksum list doesn't include this file, so the update was skipped.");
            using var response = await http.GetAsync(package.File.Url, HttpCompletionOption.ResponseHeadersRead, cancel);
            response.EnsureSuccessStatusCode();
            long total = response.Content.Headers.ContentLength ?? package.File.Size;
            if (package.File.Size > 0 && total > 0 && total != package.File.Size) return new(Outcome.Failed, "The download isn't the size the release lists, so it was skipped.");
            long written = 0;
            await using (var source = await response.Content.ReadAsStreamAsync(cancel))
            await using (var target = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var buffer = new byte[81920]; int n;
                while ((n = await source.ReadAsync(buffer, cancel)) > 0)
                {
                    await target.WriteAsync(buffer.AsMemory(0, n), cancel); written += n;
                    if (total > 0) progress?.Report(Math.Min(1, (double)written / total));
                }
            }
            if (package.File.Size > 0 && written != package.File.Size) { Discard(destination); return new(Outcome.Failed, "The download was cut short, so it was thrown away."); }
            string actual; await using (var check = File.OpenRead(destination)) actual = Convert.ToHexString(await SHA256.HashDataAsync(check, cancel)).ToLowerInvariant();
            if (actual != expected) { Discard(destination); return new(Outcome.Failed, "The download didn't match the release's checksum, so it was thrown away."); }
            return new(Outcome.Verified, "");
        }
        catch (OperationCanceledException) { Discard(destination); return new(Outcome.Cancelled, "Update cancelled."); }
        catch (Exception e) when (e is HttpRequestException or IOException or UnauthorizedAccessException) { Discard(destination); return new(Outcome.Failed, e.Message); }
    }

    static void Discard(string path) { try { if (File.Exists(path)) File.Delete(path); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { } }

    /// <summary>What the update button reads. Progress is 0..1.</summary>
    public static string Label(string versionName, bool downloading, double progress = 0, bool failed = false) =>
        downloading ? $"Downloading {versionName} · {(int)Math.Round(Math.Clamp(progress, 0, 1) * 100)}%"
        : failed ? "Update failed · open release page"
        : "Update to version " + versionName;
}
