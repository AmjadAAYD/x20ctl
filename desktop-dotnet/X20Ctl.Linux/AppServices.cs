using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace X20Ctl.Linux;

/// <summary>Small switches the app remembers: update checks, and whether the opening intro plays and with sound. Plain JSON in the user's config folder.</summary>
internal sealed class AppSettings
{
    public static AppSettings Shared { get; } = Load();
    static string PathOf => Path.Combine(Path.GetDirectoryName(X20Ctl.Zone.PcMode.PcModeStore.DefaultPath)!, "app-settings.json");
    public bool AutoUpdate { get; set; } = true;
    public bool IntroOnLaunch { get; set; } = true;
    public bool IntroSound { get; set; } = true;

    static AppSettings Load()
    {
        try { if (File.Exists(PathOf)) return JsonSerializer.Deserialize<AppSettings>(File.ReadAllBytes(PathOf)) ?? new(); }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { }
        return new();
    }

    public void Save()
    {
        try { Directory.CreateDirectory(Path.GetDirectoryName(PathOf)!); File.WriteAllBytes(PathOf, JsonSerializer.SerializeToUtf8Bytes(this)); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }
}

internal static class ExternalLinks
{
    public const string Repository = "https://github.com/AmjadAAYD/x20ctl", Support = "https://ko-fi.com/x20ctl", ApexSenseBridge = "https://github.com/ReynArts/ApexSenseBridge", Releases = Repository + "/releases";
    public static void Open(string url)
    {
        if (!url.StartsWith("https://", StringComparison.Ordinal)) return;
        try
        {
            if (OperatingSystem.IsWindows()) System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
            else System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(OperatingSystem.IsMacOS() ? "open" : "xdg-open", url) { UseShellExecute = false, RedirectStandardError = true });
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException) { }
    }
}

internal static class AppInfo
{
    public static string Version { get; } = (typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0").Split('+')[0];
    /// <summary>The version as people see it: V0.016, V0.017.1 (see docs/versions.md). <see cref="Version"/> stays the raw string for headers and reports.</summary>
    public static string Shown => X20Ctl.Zone.ReleaseVersions.Display(Version);
    public static string Os => System.Runtime.InteropServices.RuntimeInformation.OSDescription;
}

/// <summary>Asks GitHub's public releases API whether a newer X20CTL exists. The AppImage can then replace itself (AppImageUpdater); the tarball is replaced by downloading the new one.</summary>
internal static class UpdateCheck
{
    public const string Api = "https://api.github.com/repos/AmjadAAYD/x20ctl/releases?per_page=30";
    public sealed record Release(string Version, string Title, string Page, string? AppImageName = null, string? AppImageUrl = null, string? SumsUrl = null);

    public static async Task<Release?> Latest()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("X20CTL/" + AppInfo.Version.Replace('+', '.'));
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        using var doc = JsonDocument.Parse(await http.GetStringAsync(Api));
        bool takePre = AppInfo.Version.Contains('-'); Release? best = null;
        foreach (var r in doc.RootElement.EnumerateArray())
        {
            if (r.GetProperty("draft").GetBoolean()) continue;
            if (r.GetProperty("prerelease").GetBoolean() && !takePre) continue;
            string tag = r.GetProperty("tag_name").GetString() ?? "", version = Regex.Replace(tag.TrimStart('v'), "-exe$", "");
            // only releases that carry a Linux package count
            bool linux = r.GetProperty("assets").EnumerateArray().Any(a => (a.GetProperty("name").GetString() ?? "").Contains("linux-x"));
            if (!linux || Compare(version, AppInfo.Version) <= 0 || best != null && Compare(version, best.Version) <= 0) continue;
            var assets = r.GetProperty("assets").EnumerateArray().Select(a => (Name: a.GetProperty("name").GetString() ?? "", Url: a.GetProperty("browser_download_url").GetString() ?? "")).ToList();
            string? image = UpdateWire.AppImageName(assets.Select(a => a.Name));
            best = new(version, r.GetProperty("name").GetString() ?? tag, r.GetProperty("html_url").GetString() ?? ExternalLinks.Releases,
                image, image == null ? null : assets.First(a => a.Name == image).Url, assets.FirstOrDefault(a => a.Name == "SHA256SUMS.txt").Url);
        }
        return best;
    }

    /// <summary>Orders versions, the old numbering (4.1.0-preview.7) below the new (0.017): see ReleaseVersions in the shared library.</summary>
    public static int Compare(string a, string b) => X20Ctl.Zone.ReleaseVersions.Compare(a, b);
}

/// <summary>Sends one piece of feedback to the X20CTLADMIN inbox (POST /api/feedback, JSON), the same site the Windows app uses. Only what the person typed plus the
/// app and system versions are sent, and only when they press Send.</summary>
internal static class FeedbackClient
{
    public const string Endpoint = "https://x20-admin.vercel.app/api/feedback";
    public static readonly (string Id, string Label)[] Categories = [("bug", "Something's broken"), ("feature", "Idea or request"), ("compatibility", "My controller"), ("other", "Something else")];

    public static async Task<string> Send(string category, string message, string? controller, string? connection)
    {
        var body = JsonSerializer.Serialize(new
        {
            clientSubmissionId = Guid.NewGuid().ToString(), category, message = message.Trim(), appVersion = AppInfo.Version, clientTimestamp = DateTime.UtcNow.ToString("o"),
            controllerName = string.IsNullOrWhiteSpace(controller) ? null : controller, osVersion = AppInfo.Os, connectionType = string.IsNullOrWhiteSpace(connection) ? null : connection, website = ""
        });
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        using var response = await http.PostAsync(Endpoint, new StringContent(body, Encoding.UTF8, "application/json"));
        string text = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(text.Length == 0 ? "{}" : text);
        if ((int)response.StatusCode is not (200 or 201))
            throw new HttpRequestException(doc.RootElement.TryGetProperty("error", out var e) && e.TryGetProperty("message", out var m) ? m.GetString() : $"The inbox answered HTTP {(int)response.StatusCode}.");
        string id = doc.RootElement.TryGetProperty("submissionId", out var s) ? s.GetString() ?? "" : "";
        if (!Regex.IsMatch(id, "^FB-[0-9A-F]{24}$")) throw new InvalidOperationException("The inbox didn't return a receipt.");
        return id;
    }
}
