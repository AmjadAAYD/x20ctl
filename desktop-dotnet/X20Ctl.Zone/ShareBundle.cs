using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using X20Ctl.Zone.Lighting;
using X20Ctl.Zone.PcMode;

namespace X20Ctl.Zone;

public sealed class FullSetupBundle
{
    public PcModeProfile? PcMode { get; set; }
    public LightingEffectSpec? Lighting { get; set; }
    public HapticsTuning? Haptics { get; set; }
    public HapticsGenreProfile? HapticsGenre { get; set; }
    public string? Label { get; set; }
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Unified compact share code (`X20B1.`) bundling PC mode profile, active RGB effect, and Universal Haptics feel.
/// Uses Deflate + URL-safe Base64 and enforces a strict 64 KiB cap on incoming untrusted payloads.
/// </summary>
public static class ShareBundle
{
    public const string CodePrefix = "X20B1.";
    public const int MaxBytes = 64 * 1024;

    static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string ToCode(FullSetupBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        var clean = Normalize(bundle);
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(clean, JsonOptions);
        if (json.Length > MaxBytes) throw new InvalidDataException("Setup bundle is too large to encode.");
        using var ms = new MemoryStream();
        using (var ds = new DeflateStream(ms, CompressionLevel.SmallestSize, leaveOpen: true))
            ds.Write(json);
        return CodePrefix + Convert.ToBase64String(ms.ToArray()).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static FullSetupBundle FromCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new InvalidDataException("Paste a non-empty setup bundle code.");
        code = code.Trim();
        if (!code.StartsWith(CodePrefix, StringComparison.Ordinal))
            throw new InvalidDataException($"That isn't an X20CTL full bundle code. It must start with {CodePrefix}");
        string body = code[CodePrefix.Length..].Replace('-', '+').Replace('_', '/');
        if (body.Length > MaxBytes)
            throw new InvalidDataException("That bundle code is too long.");
        body = body.PadRight(body.Length + (4 - body.Length % 4) % 4, '=');
        byte[] packed;
        try { packed = Convert.FromBase64String(body); }
        catch (FormatException) { throw new InvalidDataException("That bundle code is damaged."); }

        using var input = new DeflateStream(new MemoryStream(packed), CompressionMode.Decompress);
        using var json = new MemoryStream();
        var buffer = new byte[8192];
        int read;
        try
        {
            while ((read = input.Read(buffer)) > 0)
            {
                json.Write(buffer, 0, read);
                if (json.Length > MaxBytes)
                    throw new InvalidDataException("That bundle code exceeds the 64 KiB safety limit.");
            }
        }
        catch (InvalidDataException ex) when (!ex.Message.Contains("64 KiB", StringComparison.Ordinal))
        {
            throw new InvalidDataException("That bundle code is damaged.");
        }

        FullSetupBundle? parsed;
        try { parsed = JsonSerializer.Deserialize<FullSetupBundle>(json.ToArray(), JsonOptions); }
        catch (JsonException) { throw new InvalidDataException("That bundle code is damaged."); }
        if (parsed == null) throw new InvalidDataException("That bundle code is empty.");
        return Normalize(parsed);
    }

    static FullSetupBundle Normalize(FullSetupBundle b)
    {
        if (b.PcMode != null)
        {
            b.PcMode.Name = null;
            b.PcMode.Validate();
        }
        return new FullSetupBundle
        {
            PcMode = b.PcMode,
            Lighting = b.Lighting?.Normalized(),
            Haptics = b.Haptics?.Normalized(),
            HapticsGenre = b.HapticsGenre,
            Label = string.IsNullOrWhiteSpace(b.Label) ? null : b.Label.Trim(),
            CreatedUtc = b.CreatedUtc == default ? DateTimeOffset.UtcNow : b.CreatedUtc,
        };
    }
}

public sealed class HapticsSharePayload
{
    public int Version { get; set; } = 1;
    public string? Name { get; set; }
    public string? GameExe { get; set; }
    public HapticsGenreProfile Genre { get; set; } = HapticsGenreProfile.Auto;
    public HapticsTuning Tuning { get; set; } = new();
    public HapticsAppTuning? AppCalibration { get; set; }
}

/// <summary>
/// Export and import portable <c>.x20hap</c> JSON files or <c>X20HAP1.</c> share codes for community haptics tunings.
/// </summary>
public static class HapticsShare
{
    public const string CodePrefix = "X20HAP1.";
    public const int MaxBytes = 64 * 1024;

    static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string ToJson(HapticsSharePayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return JsonSerializer.Serialize(Normalize(payload), JsonOptions);
    }

    public static HapticsSharePayload FromJson(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || Encoding.UTF8.GetByteCount(text) > MaxBytes)
            throw new InvalidDataException("Invalid or oversized .x20hap file.");
        HapticsSharePayload? parsed;
        try { parsed = JsonSerializer.Deserialize<HapticsSharePayload>(text, JsonOptions); }
        catch (JsonException) { throw new InvalidDataException("That isn't a valid .x20hap haptics profile."); }
        if (parsed == null || parsed.Version != 1 || parsed.Tuning == null)
            throw new InvalidDataException("Unsupported .x20hap profile format.");
        return Normalize(parsed);
    }

    public static string ToCode(HapticsSharePayload payload)
    {
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(Normalize(payload), JsonOptions);
        using var ms = new MemoryStream();
        using (var ds = new DeflateStream(ms, CompressionLevel.SmallestSize, leaveOpen: true))
            ds.Write(json);
        return CodePrefix + Convert.ToBase64String(ms.ToArray()).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static HapticsSharePayload FromCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new InvalidDataException("Paste a haptics share code first.");
        code = code.Trim();
        if (!code.StartsWith(CodePrefix, StringComparison.Ordinal))
            throw new InvalidDataException($"Haptics codes must start with {CodePrefix}");
        string body = code[CodePrefix.Length..].Replace('-', '+').Replace('_', '/');
        if (body.Length > MaxBytes) throw new InvalidDataException("Haptics code is too long.");
        body = body.PadRight(body.Length + (4 - body.Length % 4) % 4, '=');
        byte[] packed;
        try { packed = Convert.FromBase64String(body); }
        catch (FormatException) { throw new InvalidDataException("That haptics code is damaged."); }

        using var input = new DeflateStream(new MemoryStream(packed), CompressionMode.Decompress);
        using var json = new MemoryStream();
        var buffer = new byte[4096];
        int read;
        try
        {
            while ((read = input.Read(buffer)) > 0)
            {
                json.Write(buffer, 0, read);
                if (json.Length > MaxBytes) throw new InvalidDataException("Haptics code exceeds size limit.");
            }
        }
        catch (InvalidDataException) { throw; }
        return FromJson(Encoding.UTF8.GetString(json.ToArray()));
    }

    static HapticsSharePayload Normalize(HapticsSharePayload p) => new()
    {
        Version = 1,
        Name = string.IsNullOrWhiteSpace(p.Name) ? null : p.Name.Trim(),
        GameExe = string.IsNullOrWhiteSpace(p.GameExe) ? null : GameAutomationStore.NormalizeExe(p.GameExe),
        Genre = Enum.IsDefined(p.Genre) ? p.Genre : HapticsGenreProfile.Auto,
        Tuning = (p.Tuning ?? new()).Normalized(),
        AppCalibration = p.AppCalibration?.Normalized(),
    };
}
