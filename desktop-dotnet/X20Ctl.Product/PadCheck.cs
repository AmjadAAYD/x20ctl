using System.Text;

namespace X20Ctl.Product;

/// <summary>
/// The read-only controller check: what the pad says about itself over the KeyLinker service, turned into a report.
/// Only three questions are ever asked (identity 0x91, updater id 0x93, capability menu 0xB0 kind 1). Nothing is written,
/// no mode is entered, and nothing here claims a pad is supported: a match with a recorded pad only says the replies agree.
/// Decoding follows x20ctl/protocol.py (parse_capabilities, parse_host_guid) and the HostActivity menu record.
/// </summary>
public static class PadCheck
{
    /// <summary>The only opcodes the check may send. The BLE client refuses anything else while a check runs.</summary>
    public static readonly byte[] QueryOpcodes = [0x91, 0x93, 0xB0];

    public static bool IsQuery(byte opcode) => Array.IndexOf(QueryOpcodes, opcode) >= 0;

    /// <summary>Reply payloads exactly as they arrived (byte 0 is the length prefix); null when the pad stayed silent.</summary>
    public sealed record Raw(byte[]? Identity, byte[]? Menu, byte[]? Guid0, byte[]? Guid1);

    /// <summary>Mode is the low nibble of the flags byte, decoded only when the record is the full seven bytes (parse_device_info).</summary>
    public sealed record Identity(string Vid, string Pid, string Firmware, int? Mode = null);

    public sealed record Capabilities(int Sticks, int Triggers, int Motors, int Turbo, int Macros, int ChangeKey, int Lighting, int Engine, int Nfc, int Sensor, string RawHex)
    {
        public static readonly string[] SlotNames = ["M1", "M2", "M3", "M4", "M5", "M6", "ML", "MR"];
        public IReadOnlyList<string> MacroSlots => SlotNames.Where((_, i) => (Macros >> i & 1) != 0).ToArray();
        public int MotorCount => Enumerable.Range(0, 4).Count(i => (Motors >> i & 1) != 0);
    }

    public sealed record HostGuid(string HardwareIdHex, string ModelCode, string? Model, string Version);

    /// <summary>A pad whose replies were recorded and published, kept here only to say whether the replies agree.</summary>
    private sealed record Known(string Vid, string Pid, string Name, string UpdaterCode, string[] MacroSlots);

    private static readonly Known[] Recorded =
    [
        new("1013", "2009", "EasySMX X20", "2091", ["M1", "M2", "M3", "M4"]),
        new("1020", "2006", "EasySMX X20 Pro", "2395", ["M1", "M2", "M3", "M4", "M5", "M6"]),
    ];

    private static readonly byte[] HardwareIdPrefix = Convert.FromHexString("a1ca51da724cb6103658c05d7d4e");

    private static readonly Dictionary<string, string> UpdaterModels = new()
    {
        ["2091"] = "EasySMX X20", ["2177"] = "EasySMX X15", ["2267"] = "EasySMX D05", ["2093"] = "EasySMX S15",
        ["2350"] = "EasySMX S10", ["2371"] = "EasySMX S10 Lite", ["2395"] = "EasySMX X20 Pro",
    };

    public sealed record Report(Identity? Identity, Capabilities? Caps, HostGuid? Guid, string? Matches, IReadOnlyList<string> Findings, IReadOnlyList<string> Unanswered);

    public static Identity? ParseIdentity(byte[]? payload)
    {
        if (payload is null || payload.Length < 7) return null;
        int? mode = payload[0] == 7 && payload.Length >= 8 ? payload[7] & 0x0F : null;
        return new($"{payload[1]:x2}{payload[2]:x2}", $"{payload[3]:x2}{payload[4]:x2}", $"{payload[5]:x}.{payload[6]:x2}", mode);
    }

    /// <summary>The capability record: byte 0 is the length prefix, then ten decoded bytes. Anything shorter is not a record.</summary>
    public static Capabilities? ParseCapabilities(byte[]? payload)
    {
        if (payload is null || payload.Length < 11) return null;
        var d = payload.AsSpan(1);
        return new(d[0], d[1], d[2], d[3], d[4], d[5], d[6], d[7], d[8], d[9], Convert.ToHexString(payload.AsSpan(1)).ToLowerInvariant());
    }

    /// <summary>The 18-byte updater record: the first reply carries the start, the continuation (payload [1]) the rest.</summary>
    public static HostGuid? ParseGuid(byte[]? first, byte[]? second)
    {
        if (first is null || first.Length < 2) return null;
        var data = first.Skip(1).Concat(second ?? []).ToArray();
        if (first[0] != 18 || data.Length != 18) return null;
        string code = Convert.ToHexString(data.AsSpan(14, 2)).ToLowerInvariant();
        bool known = data.AsSpan(0, 14).SequenceEqual(HardwareIdPrefix);
        int raw = data[16] | data[17] << 8;
        return new(Convert.ToHexString(data.AsSpan(0, 16)).ToLowerInvariant(), code, known && UpdaterModels.TryGetValue(code, out var name) ? name : null, $"{raw / 100}.{raw % 100:00}");
    }

    public static Report Build(Raw raw)
    {
        var id = ParseIdentity(raw.Identity);
        var caps = ParseCapabilities(raw.Menu);
        var guid = ParseGuid(raw.Guid0, raw.Guid1);
        var silent = new List<string>();
        if (id is null) silent.Add("identity (0x91)");
        if (guid is null) silent.Add("updater id (0x93)");
        if (caps is null) silent.Add("capability menu (0xB0)");

        var findings = new List<string>();
        var recorded = id is null ? null : Recorded.FirstOrDefault(k => k.Vid == id.Vid && k.Pid == id.Pid);
        if (id is not null && recorded is null) findings.Add($"Not a pad with a recorded identity: vendor {id.Vid}, product {id.Pid}. Its replies are shown as they came and nothing is assumed about it.");
        if (recorded is not null && guid is not null)
            findings.Add(guid.ModelCode == recorded.UpdaterCode ? $"The updater id names the same model ({guid.Model ?? guid.ModelCode})." : $"The updater id says {guid.Model ?? guid.ModelCode}, not {recorded.Name}.");
        if (recorded is not null && caps is not null)
        {
            var slots = caps.MacroSlots;
            findings.Add(slots.SequenceEqual(recorded.MacroSlots) ? $"Macro slots match the recorded {recorded.Name}: {string.Join(", ", slots)}." : $"Macro slots differ from the recorded {recorded.Name}: the pad reports {(slots.Count == 0 ? "none" : string.Join(", ", slots))}, the record has {string.Join(", ", recorded.MacroSlots)}.");
        }
        if (id is not null && guid is not null && id.Firmware != guid.Version) findings.Add($"Firmware reads {id.Firmware} from the identity reply and {guid.Version} from the updater id.");
        if (caps is not null) findings.Add("A zero in the capability record means the setting is not configurable over this protocol, not that the hardware lacks the feature.");
        return new(id, caps, guid, recorded?.Name, findings, silent);
    }

    public static string Describe(Report r)
    {
        var s = new StringBuilder();
        s.AppendLine("Controller check · Bluetooth · three read-only questions");
        s.AppendLine(new string('─', 62));
        if (r.Identity is { } i) s.AppendLine($"  identity        vendor {i.Vid}, product {i.Pid}, firmware {i.Firmware}" + (i.Mode is { } m ? $", mode {m}" : "") + (r.Matches is null ? "" : $"  ·  matches the recorded {r.Matches}"));
        if (r.Guid is { } g) s.AppendLine($"  updater id      model code {g.ModelCode}" + (g.Model is null ? " (not in the known list)" : $" ({g.Model})") + $", version {g.Version}");
        if (r.Caps is { } c)
        {
            s.AppendLine($"  sticks          {Bits(c.Sticks, "left stick", "right stick")}");
            s.AppendLine($"  triggers        {Bits(c.Triggers, "left trigger", "right trigger")}");
            s.AppendLine($"  motors          {c.MotorCount}");
            s.AppendLine($"  macro slots     {(c.MacroSlots.Count == 0 ? "none" : string.Join(", ", c.MacroSlots))}");
            s.AppendLine($"  turbo / remap   {Flag(c.Turbo)} / {Flag(c.ChangeKey)}");
            s.AppendLine($"  lighting        {Flag(c.Lighting)}   nfc {Flag(c.Nfc)}   sensor {Flag(c.Sensor)}");
            s.AppendLine($"  record (hex)    {c.RawHex}");
        }
        foreach (var f in r.Findings) s.AppendLine("  · " + f);
        if (r.Unanswered.Count > 0) s.AppendLine("  Not answered: " + string.Join(", ", r.Unanswered) + ". Silence is not an error and not a result; run the check again before reading anything into it.");
        return s.ToString();
    }

    private static string Flag(int v) => v == 0 ? "off" : $"on ({v:x2})";
    private static string Bits(int v, string bit1, string bit0) => (v >> 1 & 1, v & 1) switch { (1, 1) => $"{bit1}, {bit0}", (1, 0) => bit1, (0, 1) => bit0, _ => "none" };
}
