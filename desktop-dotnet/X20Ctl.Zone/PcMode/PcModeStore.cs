using System.Text.Json;
using System.Text.Json.Serialization;
namespace X20Ctl.Zone.PcMode;

/// <summary>
/// The PC-mode profiles, one per controller model, in one JSON file. Written the careful way (a temporary file moved over
/// the old one, with a .bak kept), because a half-written file would silently turn a player's settings off.
/// </summary>
public sealed class PcModeStore
{
    readonly string path;
    readonly object gate = new();
    Data data = new();
    public PcModeStore(string path) { this.path = Path.GetFullPath(path); }
    /// <summary>False when the file existed but couldn't be read, in which case nothing is overwritten.</summary>
    public bool Healthy { get; private set; } = true;

    public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "x20ctl", "pc-mode.json");

    public void Load()
    {
        lock (gate)
        {
            data = new(); Healthy = true;
            if (!File.Exists(path)) return;
            try
            {
                var parsed = JsonSerializer.Deserialize<Data>(File.ReadAllBytes(path)) ?? throw new InvalidDataException("Empty file.");
                if (parsed.Version != 1) throw new InvalidDataException("Unsupported PC mode file.");
                foreach (var profile in parsed.Profiles.Values) profile.Validate();
                data = parsed;
            }
            catch (Exception error) when (error is JsonException or InvalidDataException or IOException or UnauthorizedAccessException) { Healthy = false; }
        }
    }

    /// <summary>The profile for a model (a fresh neutral one if none was saved). Changes are not stored until Save.</summary>
    public PcModeProfile Get(string modelId) { lock (gate) return data.Profiles.TryGetValue(modelId, out var p) ? Clone(p) : new(); }
    public bool Has(string key) { lock (gate) return data.Profiles.ContainsKey(key); }
    /// <summary>Every saved profile key: "default" (every game) and "game:&lt;exe&gt;" ones, each with its player's prefix.</summary>
    public IReadOnlyList<string> Keys { get { lock (gate) return data.Profiles.Keys.ToList(); } }
    /// <summary>Whether any saved profile changes anything, so PC mode has a reason to run.</summary>
    public bool AnySetting { get { lock (gate) return data.Profiles.Values.Any(p => !p.IsNeutral); } }

    /// <summary>The key for a game's own profile, from its exe name ("eldenring.exe" → "game:eldenring.exe").</summary>
    public static string GameKey(string exe) => GamePrefix + Path.GetFileName(exe).ToLowerInvariant();
    public const string GamePrefix = "game:", DefaultKey = "default";

    /// <summary>Each controller has its own settings: player 1's keep the plain keys (so files from before stay player
    /// 1's), players 2 to 4 put "p2:", "p3:" or "p4:" in front of the same keys.</summary>
    public static string For(int player, string key) => player <= 0 ? key : $"p{player + 1}:{key}";
    /// <summary>A stored key back into its player (0 to 3) and its plain key ("default" or "game:…").</summary>
    public static (int Player, string Key) Split(string stored) =>
        stored.Length > 3 && stored[0] == 'p' && stored[1] is >= '2' and <= '4' && stored[2] == ':' ? (stored[1] - '1', stored[3..]) : (0, stored);
    public static bool IsGame(string stored) => Split(stored).Key.StartsWith(GamePrefix);

    public void Remove(string key)
    {
        lock (gate)
        {
            if (!data.Profiles.ContainsKey(key)) return;
            var next = new Data { Profiles = new(data.Profiles) }; next.Profiles.Remove(key); Write(next);
        }
    }

    public void Save(string modelId, PcModeProfile profile)
    {
        profile.Validate();
        lock (gate)
        {
            if (!Healthy) throw new IOException("The PC mode file couldn't be read, so it won't be overwritten.");
            var next = new Data { Profiles = new(data.Profiles) };
            // a game's own profile is kept even when it changes nothing: it means "this game gets no settings"
            if (profile.IsNeutral && !IsGame(modelId)) next.Profiles.Remove(modelId); else next.Profiles[modelId] = Clone(profile);
            Write(next);
        }
    }

    void Write(Data next)
    {
        lock (gate) // re-entrant: Save and Remove already hold it
        {
            if (!Healthy) throw new IOException("The PC mode file couldn't be read, so it won't be overwritten.");
            string folder = Path.GetDirectoryName(path)!; Directory.CreateDirectory(folder);
            string temporary = Path.Combine(folder, ".pc-mode-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                File.WriteAllBytes(temporary, JsonSerializer.SerializeToUtf8Bytes(next, new JsonSerializerOptions { WriteIndented = true }));
                JsonSerializer.Deserialize<Data>(File.ReadAllBytes(temporary));
                if (File.Exists(path)) File.Copy(path, path + ".bak", true);
                File.Move(temporary, path, true);
                data = next;
            }
            finally { try { if (File.Exists(temporary)) File.Delete(temporary); } catch (IOException) { } }
        }
    }

    static PcModeProfile Clone(PcModeProfile p) => JsonSerializer.Deserialize<PcModeProfile>(JsonSerializer.SerializeToUtf8Bytes(p))!;

    sealed class Data
    {
        [JsonPropertyName("version")] public int Version { get; set; } = 1;
        [JsonPropertyName("profiles")] public Dictionary<string, PcModeProfile> Profiles { get; set; } = new();
    }
}
