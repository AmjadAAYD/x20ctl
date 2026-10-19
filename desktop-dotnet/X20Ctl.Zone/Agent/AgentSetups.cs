using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using X20Ctl.Zone.PcMode;
namespace X20Ctl.Zone.Agent;

/// <summary>A named copy of the settings the agent manages: the whole PC mode profile, as it was when saved.</summary>
public sealed record AgentSetup(string Name, string Model, DateTime Saved, PcModeProfile Profile);

/// <summary>
/// The owner's saved setups (owner's idea, 19 Oct 2026: "save this as a setup"), in their own file beside the PC mode
/// one, which keeps its format. Written the careful way, as <see cref="PcModeStore"/> is: a temporary file moved over the
/// old one with a .bak kept, and a file that couldn't be read is never overwritten.
/// </summary>
public sealed class AgentSetupStore
{
    public const int MaxSetups = 50, MaxName = 40;
    readonly string path;
    readonly object gate = new();
    List<AgentSetup> items = [];
    public AgentSetupStore(string path) { this.path = Path.GetFullPath(path); }
    public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "x20ctl", "agent-setups.json");
    /// <summary>False when the file existed but couldn't be read, in which case nothing is overwritten.</summary>
    public bool Healthy { get; private set; } = true;

    public IReadOnlyList<AgentSetup> Items { get { lock (gate) return items.ToList(); } }
    public IReadOnlyList<string> Names => Items.Select(s => s.Name).ToList();

    public void Load()
    {
        lock (gate)
        {
            items = []; Healthy = true;
            if (!File.Exists(path)) return;
            try
            {
                var parsed = JsonSerializer.Deserialize<Data>(File.ReadAllBytes(path)) ?? throw new InvalidDataException("Empty file.");
                if (parsed.Version != 1) throw new InvalidDataException("Unsupported setups file.");
                foreach (var s in parsed.Setups) s.Profile.Validate();
                items = parsed.Setups.Select(s => new AgentSetup(s.Name, s.Model, s.Saved, s.Profile)).ToList();
            }
            catch (Exception error) when (error is JsonException or InvalidDataException or IOException or UnauthorizedAccessException or NullReferenceException) { Healthy = false; }
        }
    }

    /// <summary>The setup with this name, ignoring case and spaces around it.</summary>
    public AgentSetup? Find(string name) { string n = Clean(name); lock (gate) return items.FirstOrDefault(s => string.Equals(s.Name, n, StringComparison.OrdinalIgnoreCase)); }

    /// <summary>Saves the profile under the name; an empty name gets the next "Setup N". Replaced says whether one of
    /// that name was there already (it is overwritten).</summary>
    public (AgentSetup Setup, bool Replaced) Save(string name, string model, PcModeProfile profile)
    {
        profile.Validate();
        lock (gate)
        {
            if (!Healthy) throw new IOException("The setups file couldn't be read, so it won't be overwritten.");
            string n = Clean(name);
            if (n.Length == 0) { int i = 1; while (items.Any(s => string.Equals(s.Name, $"Setup {i}", StringComparison.OrdinalIgnoreCase))) i++; n = $"Setup {i}"; }
            var copy = JsonSerializer.Deserialize<PcModeProfile>(JsonSerializer.SerializeToUtf8Bytes(profile))!;
            var next = items.ToList();
            int at = next.FindIndex(s => string.Equals(s.Name, n, StringComparison.OrdinalIgnoreCase));
            // saved again under the same name: the name keeps the way it was first written
            var setup = new AgentSetup(at >= 0 ? next[at].Name : n, model, DateTime.UtcNow, copy);
            if (at < 0 && next.Count >= MaxSetups) throw new InvalidDataException($"There are already {MaxSetups} setups. Delete one first.");
            if (at >= 0) next[at] = setup; else next.Add(setup);
            Write(next);
            return (setup, at >= 0);
        }
    }

    public bool Delete(string name)
    {
        lock (gate)
        {
            var next = items.Where(s => !string.Equals(s.Name, Clean(name), StringComparison.OrdinalIgnoreCase)).ToList();
            if (next.Count == items.Count) return false;
            Write(next); return true;
        }
    }

    /// <summary>A name as stored: trimmed, quotes and end punctuation off, at most <see cref="MaxName"/> characters.</summary>
    public static string Clean(string name)
    {
        string n = Regex.Replace(name, @"\s+", " ").Trim().Trim('"', '\'', '“', '”', '‘', '’', '.', '!', '?', ',', ' ');
        return n.Length > MaxName ? n[..MaxName].TrimEnd() : n;
    }

    void Write(List<AgentSetup> next)
    {
        if (!Healthy) throw new IOException("The setups file couldn't be read, so it won't be overwritten.");
        string folder = Path.GetDirectoryName(path)!; Directory.CreateDirectory(folder);
        string temporary = Path.Combine(folder, ".agent-setups-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            var data = new Data { Setups = next.Select(s => new Entry { Name = s.Name, Model = s.Model, Saved = s.Saved, Profile = s.Profile }).ToList() };
            File.WriteAllBytes(temporary, JsonSerializer.SerializeToUtf8Bytes(data, new JsonSerializerOptions { WriteIndented = true }));
            JsonSerializer.Deserialize<Data>(File.ReadAllBytes(temporary));
            if (File.Exists(path)) File.Copy(path, path + ".bak", true);
            File.Move(temporary, path, true);
            items = next;
        }
        finally { try { if (File.Exists(temporary)) File.Delete(temporary); } catch (IOException) { } }
    }

    sealed class Data
    {
        [JsonPropertyName("version")] public int Version { get; set; } = 1;
        [JsonPropertyName("setups")] public List<Entry> Setups { get; set; } = [];
    }
    sealed class Entry
    {
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("model")] public string Model { get; set; } = "";
        [JsonPropertyName("saved")] public DateTime Saved { get; set; }
        [JsonPropertyName("profile")] public PcModeProfile Profile { get; set; } = new();
    }
}

public enum SetupAsk { None, Save, Load, List, Delete }

/// <summary>
/// Asking for setups in the chat, recognised by the app before the phrasebook: "save this as Rocket League", "load
/// Rocket League", "my setups", "delete the Apex setup". Loading and deleting need the word setup (or preset, profile)
/// or a name that is saved, so "use a quick curve" still goes to the phrasebook.
/// </summary>
public static class AgentSetupWords
{
    const string Noun = @"(setups?|presets?|profiles?)";
    static readonly Regex Save = new(@"^(please\s+)?(save|store|remember)\b(?<rest>.*)$", RegexOptions.IgnoreCase);
    static readonly Regex Load = new(@"^(please\s+)?(load|use|apply|switch\s+to|go\s+back\s+to|bring\s+back|restore|put\s+on)\b(?<rest>.*)$", RegexOptions.IgnoreCase);
    static readonly Regex Delete = new(@"^(please\s+)?(delete|remove|forget|erase)\b(?<rest>.*)$", RegexOptions.IgnoreCase);
    static readonly Regex List = new(@"\b(my|saved|all|list|show|which|what)\b.*\b(setups|presets|profiles)\b|^(setups|presets|profiles)$", RegexOptions.IgnoreCase);
    static readonly Regex Lead = new(@"^(this|these|that|it|them|everything|my|the|a|an|current|settings?|config|" + Noun[1..^1] + @"|as|under|called|named|to|for|me|one)\b\s*", RegexOptions.IgnoreCase);
    static readonly Regex Tail = new(@"\s*\b(" + Noun[1..^1] + @"|settings?|config|please|for me|now)$", RegexOptions.IgnoreCase);

    public static (SetupAsk Ask, string Name) Parse(string sentence, IReadOnlyCollection<string> saved)
    {
        string s = Regex.Replace(sentence, @"\s+", " ").Trim().TrimEnd('.', '!', '?', ' ');
        if (s.Length == 0) return (SetupAsk.None, "");
        if (Save.Match(s) is { Success: true } save)
        {
            string rest = save.Groups["rest"].Value;
            // "save this as X", "save my setup", "save it": a setup; "save battery" and the like are not
            if (Regex.IsMatch(rest, @"\b(as|called|named|under)\b|\b" + Noun + @"\b", RegexOptions.IgnoreCase)
                || Regex.IsMatch(rest, @"^\s*(this|these|that|it|everything|(my |the |these |current )?settings?)?\s*$", RegexOptions.IgnoreCase))
                return (SetupAsk.Save, Name(rest));
            return (SetupAsk.None, "");
        }
        if (List.IsMatch(s) && !Load.IsMatch(s) && !Delete.IsMatch(s)) return (SetupAsk.List, "");
        foreach (var (pattern, ask) in new[] { (Load, SetupAsk.Load), (Delete, SetupAsk.Delete) })
            if (pattern.Match(s) is { Success: true } m)
            {
                string rest = m.Groups["rest"].Value, name = Name(rest);
                bool known = saved.Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
                if (known || Regex.IsMatch(rest, @"\b" + Noun + @"\b", RegexOptions.IgnoreCase)) return (ask, name);
            }
        return (SetupAsk.None, "");
    }

    /// <summary>The name in what follows the verb: the words around it ("this as", "my … setup") taken off. After "as"
    /// or "called" the name is kept as typed, so "save this as The Last of Us" keeps its "The".</summary>
    public static string Name(string rest)
    {
        string n, before;
        if (Regex.Match(rest, @"\b(as|called|named|under)\b", RegexOptions.IgnoreCase) is { Success: true } key)
            n = Regex.Replace(rest[(key.Index + key.Length)..], @"^\s*((a|an|my)\s+)?(" + Noun + @"\s+)?((called|named)\s+)?", "", RegexOptions.IgnoreCase).Trim();
        else
        {
            n = rest.Trim();
            do { before = n; n = Lead.Replace(n, "", 1).Trim(); } while (n != before);
        }
        do { before = n; n = Tail.Replace(n, "").Trim(); } while (n != before);
        return AgentSetupStore.Clean(n);
    }
}
