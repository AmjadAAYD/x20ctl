using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace X20Ctl.Zone;

/// <summary>One colour a model is sold in (its art lives under that model's finishes folder; "original" is the model's own art).</summary>
public sealed record Finish(string Id, string Name, string Swatch);

/// <summary>The colours each model comes in, from finishes.json, the original first. A pad's colour can't be read from its
/// USB or Bluetooth identity, so the player picks it (owner direction 24 Oct 2026).</summary>
public sealed class FinishCatalog
{
    public const string Original = "original";
    private readonly Dictionary<string, IReadOnlyList<Finish>> models = new();
    private readonly Dictionary<string, string> defaults = new();
    public FinishCatalog(string json)
    {
        using var doc = JsonDocument.Parse(json);
        foreach (var model in doc.RootElement.GetProperty("models").EnumerateObject())
        {
            var list = model.Value.EnumerateArray().Select(f => new Finish(f.GetProperty("id").GetString()!, f.GetProperty("name").GetString()!, f.GetProperty("swatch").GetString()!)).ToList();
            if (list.Count == 0 || list[0].Id != Original || list.Select(f => f.Id).Distinct().Count() != list.Count || list.Any(f => !PadColourStore.ValidId(f.Id)))
                throw new InvalidDataException($"Invalid finish list for {model.Name}.");
            models[model.Name] = list.AsReadOnly();
        }
        if (doc.RootElement.TryGetProperty("default", out var shown))
            foreach (var entry in shown.EnumerateObject())
            {
                if (entry.Value.GetString() is not { } id || !Has(entry.Name, id)) throw new InvalidDataException($"Invalid default colour for {entry.Name}.");
                defaults[entry.Name] = id;
            }
    }
    public static readonly FinishCatalog Empty = new("{\"models\":{}}");
    public IReadOnlyList<Finish> For(string model) => models.TryGetValue(model, out var list) ? list : [new(Original, "Original", "#808080")];
    public bool Has(string model, string finish) => For(model).Any(f => f.Id == finish);
    /// <summary>The colour a model is shown in until its player picks one: the original, unless finishes.json names another
    /// (owner, 24 Oct 2026: the X20 Pro shows white, the Dune black).</summary>
    public string Default(string model) => defaults.TryGetValue(model, out var id) ? id : Original;
    /// <summary>The colour after this one, wrapping round; an unknown colour starts again from the original.</summary>
    public Finish Next(string model, string? current)
    {
        var list = For(model); int at = list.ToList().FindIndex(f => f.Id == current);
        return list[(at + 1) % list.Count];
    }
}

/// <summary>The colour picked for each player's pad, remembered per player and model so a black and a white X20 Pro on two
/// players both come back as they were. Written the way the assignments are: a lock, a stale-copy check, a verified swap.</summary>
public sealed class PadColourStore
{
    private readonly string path;
    private string? baselineHash;
    private bool loaded;
    private Dictionary<string, string> colours = new();
    public PadColourStore(string path) { this.path = Path.GetFullPath(path); }
    public static bool ValidId(string id) => Regex.IsMatch(id, "^[a-z0-9_]+$");
    private static string Key(int player, string model) => $"{player + 1}:{model}";

    public void Load()
    {
        loaded = false; colours = new();
        if (File.Exists(path))
        {
            byte[] bytes = File.ReadAllBytes(path);
            colours = Parse(bytes);
            baselineHash = Convert.ToHexString(SHA256.HashData(bytes));
        }
        else baselineHash = null;
        loaded = true;
    }
    public string Get(int player, string model) => colours.TryGetValue(Key(player, model), out var finish) ? finish : FinishCatalog.Original;
    public void Set(int player, string model, string finish)
    {
        if (!loaded) throw new InvalidOperationException("Read and validate the colour file before writing.");
        if (player is < 0 or > 3) throw new ArgumentOutOfRangeException(nameof(player));
        if (!ValidId(model) || !ValidId(finish)) throw new ArgumentException("Invalid model or colour.");
        var next = new Dictionary<string, string>(colours) { [Key(player, model)] = finish };
        string directory = Path.GetDirectoryName(path)!; Directory.CreateDirectory(directory);
        using var transaction = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        string? current = File.Exists(path) ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) : null;
        if (current != baselineHash) throw new IOException("Pad colours changed in another app instance. Reload before saving.");
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(new { version = 1, colours = next }, new JsonSerializerOptions { WriteIndented = true });
        string temporary = Path.Combine(directory, ".pad-colours-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { stream.Write(bytes); stream.Flush(true); }
            Parse(File.ReadAllBytes(temporary));
            File.Move(temporary, path, true);
            colours = next; baselineHash = Convert.ToHexString(SHA256.HashData(bytes));
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    private static Dictionary<string, string> Parse(byte[] bytes)
    {
        using var doc = JsonDocument.Parse(bytes); var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("version", out var version) || version.ValueKind != JsonValueKind.Number || version.GetInt32() != 1 ||
            !root.TryGetProperty("colours", out var map) || map.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Unsupported pad colour format.");
        var result = new Dictionary<string, string>();
        foreach (var entry in map.EnumerateObject())
        {
            var parts = entry.Name.Split(':');
            if (parts.Length != 2 || parts[0] is not ("1" or "2" or "3" or "4") || !ValidId(parts[1]) || entry.Value.ValueKind != JsonValueKind.String || !ValidId(entry.Value.GetString()!))
                throw new InvalidDataException("Invalid pad colour entry.");
            result[entry.Name] = entry.Value.GetString()!;
        }
        return result;
    }
}
