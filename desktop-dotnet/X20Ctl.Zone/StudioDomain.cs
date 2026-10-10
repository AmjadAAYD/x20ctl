using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
namespace X20Ctl.Zone;

public record ControlPoint(double X, double Y);
public record ControllerModel(string Id, string Name, string Support, string Summary, string Asset, IReadOnlyDictionary<string, ControlPoint> FrontButtons);
public sealed class ControllerCatalog
{
    public IReadOnlyList<ControllerModel> Models { get; }
    public ControllerCatalog(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var models = new List<ControllerModel>();
        foreach (var item in doc.RootElement.EnumerateArray().Where(m => m.GetProperty("visible").GetBoolean()))
        {
            string id = item.GetProperty("id").GetString()!;
            if (!Regex.IsMatch(id, "^[a-z0-9_]+$")) throw new InvalidDataException("Invalid model ID.");
            var visual = item.GetProperty("visual");
            var points = visual.GetProperty("buttons").EnumerateObject().ToDictionary(p => p.Name, p => new ControlPoint(p.Value.GetProperty("x").GetDouble(), p.Value.GetProperty("y").GetDouble()));
            var sticks = visual.GetProperty("sticks");
            foreach (var (name, side) in new[] { ("L3", "left"), ("R3", "right") })
            { var stick = sticks.GetProperty(side); var center = stick.TryGetProperty("cap", out var cap) && cap.ValueKind == JsonValueKind.Object ? cap : stick; points[name] = new(center.GetProperty("x").GetDouble(), center.GetProperty("y").GetDouble()); }
            points["LSTICK_ANALOG"] = points["L3"]; points["RSTICK_ANALOG"] = points["R3"];
            if (id == "x20") { points["HOME"] = new(.5, .301); points["CAPTURE"] = new(646d / 1536, 262d / 1024); points["TURBO"] = new(879d / 1536, 264d / 1024); }
            bool supported = id == "x20" && item.GetProperty("backend").GetString() == "x20_keylinker";
            string support = supported ? "SUPPORTED" : item.GetProperty("availability").GetString() == "preview" ? "PREVIEW" : "RESEARCH";
            string summary = supported ? "Verified legacy configuration. Native connection pending." : "Configuration unverified. Model preview available.";
            models.Add(new(id, "EasySMX " + item.GetProperty("name").GetString(), support, summary, visual.GetProperty("asset").GetString()!, points));
        }
        if (models.Select(m => m.Id).Distinct().Count() != models.Count) throw new InvalidDataException("Duplicate catalog model.");
        Models = models.AsReadOnly();
    }
    public ControllerModel Get(string id) => Find(id) ?? throw new ArgumentException("Unknown controller model.", nameof(id));
    public ControllerModel? Find(string id) => Models.FirstOrDefault(m => m.Id == id);
}

public sealed class AssignmentStore
{
    private readonly string path;
    private string? baselineHash;
    private bool loaded;
    private AssignmentData data = new();
    public AssignmentStore(string path) { this.path = Path.GetFullPath(path); }
    public string?[] Load()
    {
        loaded = false;
        if (!File.Exists(path)) { baselineHash = null; data = new(); loaded = true; return new string?[4]; }
        byte[] bytes = File.ReadAllBytes(path);
        using (var schema = JsonDocument.Parse(bytes))
        {
            var root = schema.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("version", out var version) || version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out int number) || number != 1 ||
                !root.TryGetProperty("players", out var players) || players.ValueKind != JsonValueKind.Array || players.GetArrayLength() != 4)
                throw new InvalidDataException("Unsupported assignment format.");
        }
        var parsed = JsonSerializer.Deserialize<AssignmentData>(bytes) ?? throw new InvalidDataException("Empty assignment file.");
        if (parsed.Version != 1 || parsed.Players.Length != 4) throw new InvalidDataException("Unsupported assignment format.");
        Validate(parsed.Players);
        data = parsed; baselineHash = Convert.ToHexString(SHA256.HashData(bytes)); loaded = true;
        return (string?[])data.Players.Clone();
    }
    public void Save(IReadOnlyList<string?> ids)
    {
        if (!loaded) throw new InvalidOperationException("Read and validate assignment file before writing.");
        Validate(ids);
        string directory = Path.GetDirectoryName(path)!; Directory.CreateDirectory(directory);
        // Exclusive advisory lock serializes native app instances; hash detects stale snapshots.
        using var transaction = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        string? current = File.Exists(path) ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) : null;
        if (current != baselineHash) throw new IOException("Assignments changed in another app instance. Reload before saving.");
        var next = new AssignmentData { Players = ids.ToArray(), Extra = data.Extra };
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(next, new JsonSerializerOptions { WriteIndented = true });
        string temporary = Path.Combine(directory, ".assignments-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { stream.Write(bytes); stream.Flush(true); }
            var verified = JsonSerializer.Deserialize<AssignmentData>(File.ReadAllBytes(temporary))!;
            Validate(verified.Players);
            File.Move(temporary, path, true);
            data = next; baselineHash = Convert.ToHexString(SHA256.HashData(bytes));
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    private static void Validate(IReadOnlyList<string?> ids)
    {
        if (ids.Count != 4 || ids.Any(id => id != null && !Regex.IsMatch(id, "^[a-z0-9_]+$")))
            throw new InvalidDataException("Assignments must contain four valid model IDs.");
    }
    private sealed class AssignmentData
    {
        [JsonPropertyName("version")] public int Version { get; set; } = 1;
        [JsonPropertyName("players")] public string?[] Players { get; set; } = new string?[4];
        [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
    }
}

public sealed class ButtonDraft
{
    public static readonly string[] Sources = ["A", "B", "X", "Y", "LB", "RB", "LT", "RT", "L3", "R3", "DPAD_UP", "DPAD_DOWN", "DPAD_LEFT", "DPAD_RIGHT"];
    public static readonly string[] Targets = [.. Sources, "SELECT", "START"];
    private readonly Dictionary<string, string> changes = new();
    public string ModelId { get; }
    public int UnsentChanges => changes.Count;
    public IReadOnlyDictionary<string, string> Changes => new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(changes);
    public bool HardwareStateKnown => false;
    public bool CanApplyHardware => false;
    public ButtonDraft(string modelId) { ModelId = modelId; }
    public bool CanEdit(string source) => ModelId == "x20" && Sources.Contains(source);
    public string Target(string source) => changes.GetValueOrDefault(source, source);
    public void Set(string source, string target)
    {
        if (!CanEdit(source)) throw new InvalidOperationException("This control has no verified remapping source.");
        if (!Targets.Contains(target)) throw new ArgumentException("Unsupported target.", nameof(target));
        if (source == target) changes.Remove(source); else changes[source] = target;
    }
    public void Reset(string source) => changes.Remove(source);
    public void ResetAll() => changes.Clear();
}
