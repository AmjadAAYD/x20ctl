using System.Text.Json.Serialization;
namespace X20Ctl.Zone.Capture;

/// <summary>What a controller says about itself in its 0x91 reply. Vendor and product ids are the vendor's own, not USB ids.</summary>
public sealed record IdentityInfo(string Vid, string Pid, string Version, int DeviceId, int? Mode, int? Sensor, int? RawGeneration, int? EffectiveGeneration, string Record)
{
    /// <summary>Decodes the identity record: the reply payload without its size byte. The flags byte counts only in a seven-byte record.</summary>
    public static IdentityInfo? Parse(ReadOnlySpan<byte> record)
    {
        if (record.Length < 6) return null;
        string vid = $"{record[0]:x2}{record[1]:x2}", pid = $"{record[2]:x2}{record[3]:x2}";
        string version = $"{record[4]:x}.{record[5]:x2}";
        int deviceId = Convert.ToInt32(pid, 16) >> 12;
        int? mode = null, sensor = null, raw = null, effective = null;
        if (record.Length == 7)
        {
            mode = record[6] & 0x0F; sensor = (record[6] >> 4) & 1; raw = (record[6] >> 5) & 7;
            // the app routes raw generation 2 in modes 0, 7 and 8 as generation 1; other raw values are used as sent
            effective = raw == 2 && mode is 0 or 7 or 8 ? 1 : raw;
        }
        return new(vid, pid, version, deviceId, mode, sensor, raw, effective, Convert.ToHexString(record).ToLowerInvariant());
    }
}

/// <summary>
/// Everything a controller reports about itself, in one comparable shape: identity, the raw capability descriptor
/// (kept whole so a bit understood later can be read from an old recording) and the key lists. It names no product, so
/// it can be collected from anything and compared with anything without implying they are the same model. The same
/// shape x20ctl/scanning/fingerprint.py produces.
/// </summary>
public sealed record Fingerprint(IdentityInfo? Identity, string? Descriptor, IReadOnlyDictionary<string, IReadOnlyList<int>> Lists)
{
    public static readonly string[] ListNames = ["changekeySources", "turboKeys", "macroKeys", "macroLimits", "selectList"];

    /// <summary>The owner's X20 on firmware 1.23, from recorded replies (docs/01-protocol.md).</summary>
    public static readonly Fingerprint X20Reference = new(
        IdentityInfo.Parse(Convert.FromHexString("10132009012352")),
        "030303000f010002c000",
        new Dictionary<string, IReadOnlyList<int>>
        {
            ["changekeySources"] = [1, 2, 3, 4, 5, 6, 7, 8, 13, 14, 15, 16, 11, 12, 93, 94],
            ["turboKeys"] = [1, 2, 3, 4, 5, 6, 7, 8, 13, 14, 15, 16],
            ["macroKeys"] = [18, 19, 13, 14, 15, 16, 1, 2, 3, 4, 5, 6, 7, 8, 11, 12, 93, 94],
            ["macroLimits"] = [42, 42, 42, 42, 0, 0, 0, 0],
        });
}

/// <summary>The capability descriptor (the 0xB0 menu reply, kind 1): what each byte and bit means, as far as it is traced.</summary>
public static class Descriptor
{
    static readonly string[] Fields = ["sticks", "triggers", "motors", "turbo", "macros", "remapping", "lighting", "macro engine", "NFC and pad flags", "sensor"];

    /// <summary>Every bit whose meaning is traced in the official app, by (offset, bit) with the size byte stripped.</summary>
    static readonly Dictionary<(int, int), string> Names = Build();
    /// <summary>Bits that change how the app talks to the pad, not only which pages it shows.</summary>
    static readonly HashSet<(int, int)> EngineBits = [(5, 1), (7, 1)];

    static Dictionary<(int, int), string> Build()
    {
        var n = new Dictionary<(int, int), string>
        {
            [(0, 0)] = "right stick", [(0, 1)] = "left stick", [(1, 0)] = "right trigger", [(1, 1)] = "left trigger",
            [(2, 0)] = "left-up motor", [(2, 1)] = "left-down motor", [(2, 2)] = "right-up motor", [(2, 3)] = "right-down motor",
            [(5, 1)] = "remap select list (kind 7)", [(7, 1)] = "macro limits asked per slot (kind 6)",
            [(8, 4)] = "right gamepad flag", [(8, 5)] = "left gamepad flag", [(8, 6)] = "\"Pro\" gamepad flag", [(8, 7)] = "has NFC",
        };
        string[] slots = ["M1", "M2", "M3", "M4", "M5", "M6", "ML", "MR"];
        for (int i = 0; i < 8; i++) n[(4, i)] = "macro " + slots[i];
        for (int i = 0; i < 4; i++) { n[(6, i)] = "lighting zone " + (i + 1); n[(8, i)] = "NFC slot " + (i + 1); }
        return n;
    }

    public static string Label(int offset, int bit)
        => $"D{offset + 1}.bit{bit} ({(offset < Fields.Length ? Fields[offset] : "extra")})";

    /// <summary>The descriptor's set bits that nobody has traced yet.</summary>
    public static List<string> Unexplained(ReadOnlySpan<byte> data)
    {
        var found = new List<string>();
        for (int offset = 0; offset < data.Length; offset++)
            for (int bit = 0; bit < 8; bit++)
                if ((data[offset] >> bit & 1) != 0 && !Names.ContainsKey((offset, bit))) found.Add(Label(offset, bit));
        return found;
    }

    /// <summary>The named features the descriptor switches on, in plain words.</summary>
    public static List<string> Offers(ReadOnlySpan<byte> data)
    {
        var found = new List<string>();
        for (int offset = 0; offset < data.Length; offset++)
            for (int bit = 0; bit < 8; bit++)
                if ((data[offset] >> bit & 1) != 0 && Names.TryGetValue((offset, bit), out var name)) found.Add(name);
        return found;
    }

    public static bool Is(ReadOnlySpan<byte> data, int offset, int bit) => offset < data.Length && (data[offset] >> bit & 1) != 0;

    public static string? Name(int offset, int bit) => Names.TryGetValue((offset, bit), out var n) ? n : null;
    public static bool IsEngineBit(int offset, int bit) => EngineBits.Contains((offset, bit));
}

/// <summary>One difference between two controllers' reports.</summary>
public sealed record Difference(string Area, string Field, string A, string B, string Meaning);

public static class FingerprintCompare
{
    static readonly string[] Areas = ["protocol", "identity", "features", "unexplained"];

    /// <summary>Every reported difference, most telling first: generation and mode, identity, descriptor bits, key lists.</summary>
    public static List<Difference> Compare(Fingerprint a, Fingerprint b)
    {
        var d = new List<Difference>();
        var ia = a.Identity; var ib = b.Identity;
        void Id(string area, string field, string? x, string? y, string meaning) { if (x != y) d.Add(new(area, field, x ?? "—", y ?? "—", meaning)); }
        Id("protocol", "effective generation", ia?.EffectiveGeneration?.ToString(), ib?.EffectiveGeneration?.ToString(), "the app picks command variants by this");
        Id("protocol", "raw generation", ia?.RawGeneration?.ToString(), ib?.RawGeneration?.ToString(), "generation as sent");
        Id("protocol", "operating mode", ia?.Mode?.ToString(), ib?.Mode?.ToString(), "the app routes on it");
        Id("identity", "vendor id", ia?.Vid, ib?.Vid, "");
        Id("identity", "product id", ia?.Pid, ib?.Pid, "");
        Id("identity", "device id", ia?.DeviceId.ToString(), ib?.DeviceId.ToString(), "top nibble of the product id");
        Id("identity", "firmware", ia?.Version, ib?.Version, "");
        Id("identity", "sensor flag", ia?.Sensor?.ToString(), ib?.Sensor?.ToString(), "");

        var ra = a.Descriptor is null ? Array.Empty<byte>() : Convert.FromHexString(a.Descriptor);
        var rb = b.Descriptor is null ? Array.Empty<byte>() : Convert.FromHexString(b.Descriptor);
        if (!ra.AsSpan().SequenceEqual(rb))
        {
            if (ra.Length != rb.Length) d.Add(new("protocol", "descriptor length", ra.Length.ToString(), rb.Length.ToString(), "a longer descriptor carries bytes nothing here decodes"));
            for (int offset = 0; offset < Math.Max(ra.Length, rb.Length); offset++)
            {
                int? va = offset < ra.Length ? ra[offset] : null, vb = offset < rb.Length ? rb[offset] : null;
                if (va == vb) continue;
                for (int bit = 0; bit < 8; bit++)
                {
                    bool? x = va is null ? null : (va >> bit & 1) != 0, y = vb is null ? null : (vb >> bit & 1) != 0;
                    if (x == y) continue;
                    var name = Descriptor.Name(offset, bit);
                    string area = Descriptor.IsEngineBit(offset, bit) ? "protocol" : name is not null ? "features" : "unexplained";
                    d.Add(new(area, Descriptor.Label(offset, bit), x?.ToString() ?? "—", y?.ToString() ?? "—", name ?? "not traced"));
                }
            }
        }

        foreach (var list in Fingerprint.ListNames)
        {
            a.Lists.TryGetValue(list, out var xa); b.Lists.TryGetValue(list, out var xb);
            if (xa is null && xb is null) continue;
            if (xa is not null && xb is not null && xa.SequenceEqual(xb)) continue;
            string meaning = xa is null || xb is null ? "missing on one side"
                : xa.Except(xb).Any() || xb.Except(xa).Any() ? $"only in A: {Join(xa.Except(xb))}; only in B: {Join(xb.Except(xa))}" : "same entries, different order";
            d.Add(new("protocol", list, xa is null ? "—" : Join(xa), xb is null ? "—" : Join(xb), meaning));
        }
        return d.OrderBy(x => Array.IndexOf(Areas, x.Area)).ToList();
    }

    static string Join(IEnumerable<int> v) { var s = string.Join(",", v); return s.Length == 0 ? "none" : s; }
}
