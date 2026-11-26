using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace X20Ctl.Zone.Community;

public sealed class CommunityCache
{
    private readonly string _rootDir;
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web) { WriteIndented = false };

    public CommunityCache(string? rootDir = null)
    {
        if (!string.IsNullOrWhiteSpace(rootDir))
        {
            _rootDir = rootDir;
        }
        else
        {
            var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(baseDir))
                baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache");
            _rootDir = Path.Combine(baseDir, "X20Ctl", "community-cache");
        }
    }

    public void SaveCatalog(CatalogResponse catalog)
    {
        try
        {
            Directory.CreateDirectory(_rootDir);
            File.WriteAllText(Path.Combine(_rootDir, "catalog.json"), JsonSerializer.Serialize(catalog, JsonOpts), Encoding.UTF8);
        }
        catch
        {
            // Ignore non-fatal cache write failures
        }
    }

    public CatalogResponse? LoadCatalog()
    {
        try
        {
            var file = Path.Combine(_rootDir, "catalog.json");
            if (!File.Exists(file)) return null;
            return JsonSerializer.Deserialize<CatalogResponse>(File.ReadAllText(file, Encoding.UTF8), JsonOpts);
        }
        catch
        {
            return null;
        }
    }

    public void SaveMedia(string cacheKey, byte[] bytes)
    {
        try
        {
            var safe = SanitizeKey(cacheKey);
            var dir = Path.Combine(_rootDir, "media");
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, safe), bytes);
        }
        catch
        {
            // Ignore cache errors
        }
    }

    public byte[]? TryLoadMedia(string cacheKey)
    {
        try
        {
            var safe = SanitizeKey(cacheKey);
            var file = Path.Combine(_rootDir, "media", safe);
            return File.Exists(file) ? File.ReadAllBytes(file) : null;
        }
        catch
        {
            return null;
        }
    }

    private static string SanitizeKey(string key)
    {
        var sb = new StringBuilder(key.Length);
        foreach (var ch in key)
            sb.Append(char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' ? ch : '_');
        return sb.ToString();
    }
}
