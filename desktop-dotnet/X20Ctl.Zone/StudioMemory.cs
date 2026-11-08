using System.Text.Json;
using System.Text.Json.Serialization;
namespace X20Ctl.Zone;

/// <summary>What a controller's Studio last held, per player and model, kept on this PC so going back in, even after
/// X20CTL restarts, shows the same settings and tab (owner, 7 Nov 2026). It is the Studio's own state, never a pad
/// read-back: PC mode's applied settings still live in pc-mode.json, and a pad write still needs Apply.</summary>
public sealed class StudioMemory
{
    public sealed record Step(string[] Buttons, StickHeading Left, StickHeading Right, int HoldMs, int PauseMs);
    public sealed class Entry
    {
        public Dictionary<string, string> Mappings { get; set; } = new();
        public Dictionary<string, SetupCurve> Curves { get; set; } = new();
        public Dictionary<string, Step[]> Macros { get; set; } = new();
        public Dictionary<string, string?> Triggers { get; set; } = new();
        public int Vibration { get; set; } = VibrationDraft.DefaultStrength;
        public int VibrationLeft { get; set; } = 100;
        public int VibrationRight { get; set; } = 100;
        public int VibrationFloor { get; set; }
        public string? Page { get; set; }
        /// <summary>PC mode's stored settings when this was kept; edits not yet applied come back only if those are unchanged.</summary>
        public string? PcBase { get; set; }
        [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
    }
    sealed class Data { public int Version { get; set; } = 1; public Dictionary<string, Entry> Entries { get; set; } = new(); }
    static readonly JsonSerializerOptions Options = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

    readonly string? path; Data data = new();
    /// <summary>False when the file existed but couldn't be read; it is then left untouched.</summary>
    public bool Healthy { get; private set; } = true;
    /// <summary>A null path keeps everything in memory only (review windows).</summary>
    public StudioMemory(string? path) { this.path = path == null ? null : Path.GetFullPath(path); Load(); }

    void Load()
    {
        if (path == null || !File.Exists(path)) return;
        try
        {
            var parsed = JsonSerializer.Deserialize<Data>(File.ReadAllBytes(path), Options) ?? throw new InvalidDataException("Empty file.");
            if (parsed.Version != 1 || parsed.Entries == null) throw new InvalidDataException("Unsupported Studio memory file.");
            data = parsed;
        }
        catch (Exception error) when (error is JsonException or InvalidDataException or IOException or UnauthorizedAccessException) { Healthy = false; }
    }

    public static string Key(int player, string model) => player + "|" + model;
    public Entry? Get(int player, string model) => data.Entries.GetValueOrDefault(Key(player, model));

    public static Entry Capture(ButtonDraft buttons, CurveDraft curves, MacroDraft macros, VibrationDraft vibration, string? page, string? pcBase) => new()
    {
        Mappings = new(buttons.Changes),
        Curves = Enum.GetValues<CurveChannel>().ToDictionary(c => c.ToString(), c => { var v = curves[c]; return new SetupCurve(v.Preset, v.BasePreset, v.Inner, v.Outer, v.Point1.X, v.Point1.Y, v.Point2.X, v.Point2.Y); }),
        Macros = macros.SlotNames.ToDictionary(s => s, s => macros[s].Select(e => new Step(e.Buttons.ToArray(), e.Left, e.Right, e.HoldMs, e.PauseMs)).ToArray()),
        Triggers = macros.SlotNames.ToDictionary(s => s, macros.Trigger),
        Vibration = vibration.Strength, VibrationLeft = vibration.Left, VibrationRight = vibration.Right, VibrationFloor = vibration.Floor, Page = page, PcBase = pcBase,
    };

    /// <summary>Fills the drafts from an entry. Each part is tried on its own, so a setting this model can't hold (or a
    /// locked editor) leaves that part as it is and the rest still comes back.</summary>
    public static void Restore(Entry e, ButtonDraft buttons, CurveDraft curves, MacroDraft macros, VibrationDraft vibration)
    {
        void Try(Action part) { try { part(); } catch (Exception error) when (error is ArgumentException or InvalidOperationException or KeyNotFoundException) { } }
        Try(() => { buttons.ResetAll(); foreach (var (source, target) in e.Mappings) Try(() => buttons.Set(source, target)); });
        foreach (var (name, c) in e.Curves)
            if (Enum.TryParse<CurveChannel>(name, out var channel)) Try(() => curves.Set(channel, new CurveSetting(c.Preset, c.Inner, c.Outer, (c.X1, c.Y1), (c.X2, c.Y2)) { BasePreset = c.BasePreset }));
        foreach (var slot in macros.SlotNames)
        {
            if (e.Macros.TryGetValue(slot, out var steps))
                Try(() => macros.Replace(slot, steps.Select(s => new MacroEvent(Guid.NewGuid(), Array.AsReadOnly(s.Buttons.ToArray()), s.Left, s.Right, s.HoldMs, s.PauseMs))));
            if (e.Triggers.TryGetValue(slot, out var trigger)) Try(() => macros.SetTrigger(slot, trigger));
        }
        if (vibration.CanEdit) Try(() => vibration.Set(e.Vibration));
        if (vibration.PcMode) Try(() => vibration.SetMotors(e.VibrationLeft, e.VibrationRight, e.VibrationFloor));
    }

    public void Put(int player, string model, Entry entry)
    {
        data.Entries[Key(player, model)] = entry;
        if (path == null || !Healthy) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllBytes(temp, JsonSerializer.SerializeToUtf8Bytes(data, Options));
            File.Move(temp, path, true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
    }
}
