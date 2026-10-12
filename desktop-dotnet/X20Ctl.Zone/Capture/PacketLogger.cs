using System.Buffers.Binary;
using System.Formats.Tar;
using System.IO.Compression;
namespace X20Ctl.Zone.Capture;

/// <summary>
/// Reads the Bluetooth log an iPhone keeps with Apple's Bluetooth logging profile installed: the PacketLogger file
/// (.pklg) found inside a sysdiagnose archive. Each record is a length, a timestamp (seconds and microseconds), a type
/// byte and the packet. Types 2 and 3 are ACL data sent and received, the same bytes an Android snoop log holds after
/// its type byte. Older files are big-endian, newer ones little-endian; the first record says which.
/// </summary>
public static class PacketLogger
{
    public const long MaxSysdiagnoseBytes = 2L * 1024 * 1024 * 1024;

    /// <summary>True when the first record reads sensibly in either byte order.</summary>
    public static bool LooksLikeLog(ReadOnlySpan<byte> data) => data.Length >= 13 && (Plausible(data, true) || Plausible(data, false));

    static bool Plausible(ReadOnlySpan<byte> data, bool big)
    {
        uint length = big ? BinaryPrimitives.ReadUInt32BigEndian(data) : BinaryPrimitives.ReadUInt32LittleEndian(data);
        return length >= 9 && length <= 65536 + 9 && length <= data.Length - 4 && data[12] is <= 0x0B or >= 0xF7;
    }

    public static List<HciRecord> Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length > BtSnoop.MaxLogBytes) throw new InvalidDataException("This log is over 64 MB. Record a shorter session: one connection, one change.");
        if (!LooksLikeLog(data)) throw new InvalidDataException("This is not an iPhone Bluetooth log.");
        bool big = Plausible(data, true);
        var records = new List<HciRecord>();
        int offset = 0; double? first = null;
        while (data.Length - offset >= 13)
        {
            var at = data[offset..];
            uint length = big ? BinaryPrimitives.ReadUInt32BigEndian(at) : BinaryPrimitives.ReadUInt32LittleEndian(at);
            if (length < 9 || length > data.Length - offset - 4) throw new InvalidDataException("The log has a damaged packet.");
            uint secs = big ? BinaryPrimitives.ReadUInt32BigEndian(at[4..]) : BinaryPrimitives.ReadUInt32LittleEndian(at[4..]);
            uint micros = big ? BinaryPrimitives.ReadUInt32BigEndian(at[8..]) : BinaryPrimitives.ReadUInt32LittleEndian(at[8..]);
            byte type = at[12];
            var packet = at.Slice(13, (int)length - 9);
            offset += 4 + (int)length;
            double stamp = secs + micros / 1e6;
            first ??= stamp;
            if (type is 2 or 3) records.Add(new HciRecord(stamp - first.Value, type == 2, packet.ToArray(), stamp));
        }
        if (records.Count == 0) throw new InvalidDataException("The log holds no data packets. Was the Bluetooth logging profile installed before the test?");
        return records;
    }

    /// <summary>The PacketLogger file in a sysdiagnose (.tar.gz) that holds the test, read without unpacking anything
    /// else. Every .pklg is a candidate; <see cref="BtSnoop.Pick"/> chooses.</summary>
    public static byte[] ReadFromSysdiagnose(string path, out string usedName)
    {
        var info = new FileInfo(path);
        if (!info.Exists) throw new FileNotFoundException("That file isn't there any more.", path);
        if (info.Length > MaxSysdiagnoseBytes) throw new InvalidDataException("This sysdiagnose is over 2 GB. Ask for help extracting the Bluetooth log.");
        using var file = File.OpenRead(path);
        Span<byte> head = stackalloc byte[2];
        int n = file.Read(head); file.Position = 0;
        using Stream tar = n == 2 && head[0] == 0x1F && head[1] == 0x8B ? new GZipStream(file, CompressionMode.Decompress) : file;
        using var reader = new TarReader(tar);
        var candidates = new List<LogCandidate>(); long total = 0; int entries = 0;
        while (reader.GetNextEntry() is { } entry)
        {
            if (++entries > 200_000) throw new InvalidDataException("This archive holds too many files.");
            if (entry.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile) || entry.DataStream is null) continue;
            if (!entry.Name.EndsWith(".pklg", StringComparison.OrdinalIgnoreCase) || entry.Length == 0) continue;
            if (entry.Length > BtSnoop.MaxLogBytes || (total += entry.Length) > BtSnoop.MaxCandidateBytes) continue;
            var buffer = new byte[entry.Length];
            entry.DataStream.ReadExactly(buffer);
            candidates.Add(new(entry.Name, entry.ModificationTime, buffer));
        }
        if (candidates.Count == 0) throw new InvalidDataException("No Bluetooth log is in this sysdiagnose. Install Apple's Bluetooth logging profile and follow its instructions, repeat the test, then take a new sysdiagnose.");
        return BtSnoop.Pick(candidates, data => Parse(data), out usedName);
    }
}

public static partial class BtSnoopWriter
{
    /// <summary>ACL packets as an Android-style snoop log (datalink 1002). Used to share a recording without the HCI
    /// events around it, which is where device names and addresses travel.</summary>
    public static byte[] Write(IEnumerable<HciRecord> records)
    {
        using var ms = new MemoryStream();
        Span<byte> word = stackalloc byte[8];
        ms.Write("btsnoop\0"u8);
        BinaryPrimitives.WriteUInt32BigEndian(word, 1); ms.Write(word[..4]);
        BinaryPrimitives.WriteUInt32BigEndian(word, 1002); ms.Write(word[..4]);
        // microseconds since 0 AD, as the format counts them; any fixed origin works since only differences are read
        const long origin = 0x00E03AB44A676000;
        foreach (var r in records)
        {
            uint size = (uint)r.Data.Length + 1;
            BinaryPrimitives.WriteUInt32BigEndian(word, size); ms.Write(word[..4]);
            ms.Write(word[..4]);
            BinaryPrimitives.WriteUInt32BigEndian(word, r.FromHost ? 0u : 1u); ms.Write(word[..4]);
            BinaryPrimitives.WriteUInt32BigEndian(word, 0); ms.Write(word[..4]);
            BinaryPrimitives.WriteInt64BigEndian(word, origin + (long)Math.Round(r.Seconds * 1e6)); ms.Write(word);
            ms.WriteByte(2); ms.Write(r.Data);
        }
        return ms.ToArray();
    }
}
