using System.IO.Compression;
using System.Text;
using System.Text.Json;
namespace X20Ctl.Zone.PcMode;

/// <summary>
/// Sharing PC mode settings: a small file, or a code short enough to paste in a chat ("X20PC1." then the settings,
/// deflated and in URL-safe base64). Both carry only the settings, never anything about the PC or the player. What
/// comes in is untrusted: size-capped, then checked like any saved profile before it is used.
/// </summary>
public static class PcModeShare
{
    public const string CodePrefix = "X20PC1.";
    const int MaxBytes = 64 * 1024;

    public static string ToJson(PcModeProfile profile)
    {
        var copy = JsonSerializer.Deserialize<PcModeProfile>(JsonSerializer.SerializeToUtf8Bytes(profile))!; copy.Name = null;
        return JsonSerializer.Serialize(new Envelope { Profile = copy }, new JsonSerializerOptions { WriteIndented = true });
    }

    public static PcModeProfile FromJson(string text)
    {
        if (Encoding.UTF8.GetByteCount(text) > MaxBytes) throw new InvalidDataException("That file is too big to be PC mode settings.");
        Envelope? envelope;
        try { envelope = JsonSerializer.Deserialize<Envelope>(text); }
        catch (JsonException) { throw new InvalidDataException("That isn't a PC mode settings file."); }
        if (envelope is not { X20ctlPcMode: 1, Profile: { } p }) throw new InvalidDataException("That isn't a PC mode settings file.");
        p.Name = null; p.Validate(); return p;
    }

    public static string ToCode(PcModeProfile profile)
    {
        var copy = JsonSerializer.Deserialize<PcModeProfile>(JsonSerializer.SerializeToUtf8Bytes(profile))!; copy.Name = null;
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(copy);
        using var output = new MemoryStream();
        using (var deflate = new DeflateStream(output, CompressionLevel.SmallestSize, leaveOpen: true)) deflate.Write(json);
        return CodePrefix + Convert.ToBase64String(output.ToArray()).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static PcModeProfile FromCode(string code)
    {
        code = code.Trim();
        if (!code.StartsWith(CodePrefix, StringComparison.Ordinal)) throw new InvalidDataException("That isn't an X20CTL PC mode code. It starts with " + CodePrefix);
        string body = code[CodePrefix.Length..].Replace('-', '+').Replace('_', '/');
        if (body.Length > MaxBytes) throw new InvalidDataException("That code is too long.");
        body = body.PadRight(body.Length + (4 - body.Length % 4) % 4, '=');
        byte[] packed;
        try { packed = Convert.FromBase64String(body); } catch (FormatException) { throw new InvalidDataException("That code is damaged. Copy it again in one piece."); }
        using var input = new DeflateStream(new MemoryStream(packed), CompressionMode.Decompress);
        using var json = new MemoryStream(); var buffer = new byte[8192]; int read;
        try { while ((read = input.Read(buffer)) > 0) { json.Write(buffer, 0, read); if (json.Length > MaxBytes) throw new InvalidDataException("That code is too long."); } }
        catch (InvalidDataException error) when (error.Message != "That code is too long.") { throw new InvalidDataException("That code is damaged. Copy it again in one piece."); }
        PcModeProfile? p;
        try { p = JsonSerializer.Deserialize<PcModeProfile>(json.ToArray()); } catch (JsonException) { throw new InvalidDataException("That code is damaged. Copy it again in one piece."); }
        if (p == null) throw new InvalidDataException("That code is empty.");
        p.Name = null; p.Validate(); return p;
    }

    sealed class Envelope
    {
        [System.Text.Json.Serialization.JsonPropertyName("x20ctlPcMode")] public int X20ctlPcMode { get; set; } = 1;
        [System.Text.Json.Serialization.JsonPropertyName("profile")] public PcModeProfile? Profile { get; set; }
    }
}
