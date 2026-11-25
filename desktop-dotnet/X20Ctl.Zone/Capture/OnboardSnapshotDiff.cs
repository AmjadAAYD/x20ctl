using System.Text.Json;
using X20Ctl.Zone.Lighting;
using X20Ctl.Zone.PcMode;

namespace X20Ctl.Zone.Capture;

/// <summary>
/// Read-only snapshot of verified KeyLinker memory blocks on supported EasySMX controllers (0x91, 0xB6, 0xB1, 0xB2, 0xB5, 0xB7),
/// plus deterministic byte-level diff and PC Mode profile comparison.
/// </summary>
public sealed record OnboardHardwareSnapshot(
    DateTimeOffset CapturedUtc,
    string ModelId,
    string IdentityHex,
    string RemapHex,
    string SticksHex,
    string TriggersHex,
    string[] MacrosHex,
    string LightingHex,
    double GattLatencyMs);

public sealed record ByteDiffRow(string BlockName, int ByteIndex, byte Before, byte After);

public static class OnboardSnapshotDiff
{
    public static IReadOnlyList<string> DecodeSummary(OnboardHardwareSnapshot snap)
    {
        var lines = new List<string>
        {
            $"Captured {snap.CapturedUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss} · Model {snap.ModelId} · GATT {snap.GattLatencyMs:0} ms",
        };

        byte[] id = ParseHex(snap.IdentityHex);
        if (id.Length >= 7)
            lines.Add($"0x91 Identity: VID {id[1]:X2}{id[2]:X2} · PID {id[3]:X2}{id[4]:X2} · FW {id[5]:X}.{id[6]:X2} (raw {snap.IdentityHex})");
        else
            lines.Add($"0x91 Identity: {FallbackHex(snap.IdentityHex)}");

        byte[] remap = ParseHex(snap.RemapHex);
        try
        {
            var map = ControllerRecords.ParseRemap(remap);
            lines.Add(map.Count == 0
                ? $"0xB6 Remap ({remap.Length}B): All factory default ({snap.RemapHex})"
                : $"0xB6 Remap ({remap.Length}B): " + string.Join(", ", map.Select(kv => $"{kv.Key}->{kv.Value}")));
        }
        catch (FormatException)
        {
            lines.Add($"0xB6 Remap ({remap.Length}B): {FallbackHex(snap.RemapHex)}");
        }

        byte[] sticks = ParseHex(snap.SticksHex);
        try
        {
            var ls = ControllerRecords.ParseChannel(sticks, 0, false);
            var rs = ControllerRecords.ParseChannel(sticks, 1, false);
            lines.Add($"0xB1 Sticks ({sticks.Length}B): L DZ {ls.Inner}-{ls.Outer}% ({ls.Preset}) · R DZ {rs.Inner}-{rs.Outer}% ({rs.Preset})");
        }
        catch (FormatException)
        {
            lines.Add($"0xB1 Sticks ({sticks.Length}B): {FallbackHex(snap.SticksHex)}");
        }

        byte[] triggers = ParseHex(snap.TriggersHex);
        try
        {
            var lt = ControllerRecords.ParseChannel(triggers, 0, true);
            var rt = ControllerRecords.ParseChannel(triggers, 1, true);
            lines.Add($"0xB2 Triggers ({triggers.Length}B): LT {lt.Inner}-{lt.Outer}% ({lt.Preset}) · RT {rt.Inner}-{rt.Outer}% ({rt.Preset})");
        }
        catch (FormatException)
        {
            lines.Add($"0xB2 Triggers ({triggers.Length}B): {FallbackHex(snap.TriggersHex)}");
        }

        for (int i = 0; i < (snap.MacrosHex?.Length ?? 0); i++)
        {
            byte[] m = ParseHex(snap.MacrosHex![i]);
            int entries = m.Length > 0 ? m[0] : 0;
            lines.Add($"0xB5 M{i + 1} ({m.Length}B): {(entries == 0 ? "empty" : $"{entries} entries")} · {FallbackHex(snap.MacrosHex[i])}");
        }

        byte[] led = ParseHex(snap.LightingHex);
        if (led.Length == 24)
        {
            double pct = LightingRecord.BrightnessPercentFor(led[5]);
            lines.Add($"0xB7 Lighting (24B): #{led[0]:X2}{led[1]:X2}{led[2]:X2} · Mode {LightingRecord.ModeName(led[6])} · Brightness {pct:0}% (0x{led[5]:X2}) · Tail {Convert.ToHexString(led, 7, 17)}");
        }
        else if (led.Length > 0)
        {
            lines.Add($"0xB7 Lighting ({led.Length}B): {FallbackHex(snap.LightingHex)}");
        }

        return lines;
    }

    public static IReadOnlyList<ByteDiffRow> Diff(OnboardHardwareSnapshot before, OnboardHardwareSnapshot after)
    {
        var diffs = new List<ByteDiffRow>();
        CompareBlock("0x91 Identity", before.IdentityHex, after.IdentityHex, diffs);
        CompareBlock("0xB6 Remap", before.RemapHex, after.RemapHex, diffs);
        CompareBlock("0xB1 Sticks", before.SticksHex, after.SticksHex, diffs);
        CompareBlock("0xB2 Triggers", before.TriggersHex, after.TriggersHex, diffs);
        int maxMacros = Math.Max(before.MacrosHex?.Length ?? 0, after.MacrosHex?.Length ?? 0);
        for (int i = 0; i < maxMacros; i++)
        {
            string a = i < (before.MacrosHex?.Length ?? 0) ? before.MacrosHex![i] : "";
            string b = i < (after.MacrosHex?.Length ?? 0) ? after.MacrosHex![i] : "";
            CompareBlock($"0xB5 M{i + 1}", a, b, diffs);
        }
        CompareBlock("0xB7 Lighting", before.LightingHex, after.LightingHex, diffs);
        return diffs;
    }

    public static IReadOnlyList<string> CompareWithPcMode(OnboardHardwareSnapshot onboard, PcModeProfile pc)
    {
        var lines = new List<string>();
        Dictionary<string, string> hwRemap = new();
        try { hwRemap = ControllerRecords.ParseRemap(ParseHex(onboard.RemapHex)); } catch (FormatException) { }

        if (hwRemap.Count != pc.Remap.Count || hwRemap.Any(kv => pc.Remap.GetValueOrDefault(kv.Key) != kv.Value))
            lines.Add($"Remap differs: Onboard ({hwRemap.Count} mapped) vs PC Mode ({pc.Remap.Count} mapped)");
        else
            lines.Add($"Remap matches PC Mode ({hwRemap.Count} mapped)");

        try
        {
            var ls = ControllerRecords.ParseChannel(ParseHex(onboard.SticksHex), 0, false);
            var rs = ControllerRecords.ParseChannel(ParseHex(onboard.SticksHex), 1, false);
            int pcLz = (int)Math.Round(pc.Sticks.GetValueOrDefault("LeftStick")?.Inner ?? pc.Curves.GetValueOrDefault("LeftStick")?.Inner ?? 0);
            int pcRz = (int)Math.Round(pc.Sticks.GetValueOrDefault("RightStick")?.Inner ?? pc.Curves.GetValueOrDefault("RightStick")?.Inner ?? 0);
            if (ls.Inner != pcLz || rs.Inner != pcRz)
                lines.Add($"Stick inner dead zone differs: Onboard L {ls.Inner}% / R {rs.Inner}% vs PC Mode L {pcLz}% / R {pcRz}%");
            else
                lines.Add($"Stick inner dead zones match ({ls.Inner}% / {rs.Inner}%)");
        }
        catch (FormatException) { }

        int hwMacros = onboard.MacrosHex?.Count(h => ParseHex(h).FirstOrDefault() > 0) ?? 0;
        if (hwMacros != pc.Macros.Count)
            lines.Add($"Macro slots differ: Onboard {hwMacros} non-empty vs PC Mode {pc.Macros.Count} active");
        else
            lines.Add($"Macro slot count matches ({hwMacros} active)");

        return lines;
    }

    static void CompareBlock(string name, string hexA, string hexB, List<ByteDiffRow> diffs)
    {
        byte[] a = ParseHex(hexA), b = ParseHex(hexB);
        int len = Math.Max(a.Length, b.Length);
        for (int i = 0; i < len; i++)
        {
            byte ba = i < a.Length ? a[i] : (byte)0;
            byte bb = i < b.Length ? b[i] : (byte)0;
            if (ba != bb) diffs.Add(new ByteDiffRow(name, i, ba, bb));
        }
    }

    static byte[] ParseHex(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return [];
        try { return Convert.FromHexString(hex.Trim()); }
        catch (FormatException) { return []; }
    }

    static string FallbackHex(string? hex) => string.IsNullOrWhiteSpace(hex) ? "unavailable" : hex;
}

public sealed class OnboardSnapshotStore(string? path = null)
{
    readonly string filePath = string.IsNullOrWhiteSpace(path) ? DefaultPath : Path.GetFullPath(path);
    readonly List<OnboardHardwareSnapshot> items = new();
    bool loaded;

    public static string DefaultPath => AppDataPaths.Local("telemetry", "onboard-snapshots.json");
    public IReadOnlyList<OnboardHardwareSnapshot> Items { get { EnsureLoaded(); return items.AsReadOnly(); } }

    public void Load()
    {
        loaded = true;
        items.Clear();
        if (!File.Exists(filePath)) return;
        try
        {
            var parsed = JsonSerializer.Deserialize<List<OnboardHardwareSnapshot>>(File.ReadAllBytes(filePath));
            if (parsed != null) items.AddRange(parsed.Where(x => x != null).Take(16));
        }
        catch (Exception ex) when (ex is JsonException or IOException) { }
    }

    public void SaveSnapshot(OnboardHardwareSnapshot snap)
    {
        EnsureLoaded();
        items.Insert(0, snap);
        if (items.Count > 16) items.RemoveRange(16, items.Count - 16);
        Write();
    }

    void EnsureLoaded() { if (!loaded) Load(); }

    void Write()
    {
        string dir = Path.GetDirectoryName(filePath)!;
        Directory.CreateDirectory(dir);
        string tmp = filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllBytes(tmp, JsonSerializer.SerializeToUtf8Bytes(items, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(tmp, filePath, true);
        }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }
}
