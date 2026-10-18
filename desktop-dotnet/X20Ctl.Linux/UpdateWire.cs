using System.Text.RegularExpressions;
namespace X20Ctl.Linux;

/// <summary>The parts of updating that need no network or file system, so they can be tested: which release URLs to trust, and what a release's SHA256SUMS.txt says about one file.</summary>
internal static class UpdateWire
{
    public const string TrustedPrefix = "https://github.com/AmjadAAYD/x20ctl/releases/download/";

    /// <summary>Only files served from this project's own GitHub releases are ever downloaded.</summary>
    public static bool Trusted(string url) => url.StartsWith(TrustedPrefix, StringComparison.Ordinal);

    /// <summary>The lowercase SHA-256 listed for a file in a sha256sum-style list ("hash  name" or "hash *name"), or null if it isn't there or isn't a hash.</summary>
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

    /// <summary>The AppImage in a release's asset names: the x86_64 one, never a .zsync or another package.</summary>
    public static string? AppImageName(IEnumerable<string> assetNames) => assetNames.FirstOrDefault(n => n.EndsWith("-linux-x86_64.AppImage", StringComparison.Ordinal));
}
