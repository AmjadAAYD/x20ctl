using System.Buffers.Binary;
using System.IO.Compression;
namespace X20Ctl.Zone.Capture;

/// <summary>One ACL data packet of a Bluetooth recording: when it happened (seconds from the first packet), which way it
/// went, and its bytes. <c>Unix</c> is the phone's own clock in Unix seconds when the log carries a readable one,
/// otherwise NaN. <c>Cut</c> is set when the log kept only the start of the packet, as Android's "Filtered" log does.</summary>
public sealed record HciRecord(double Seconds, bool FromHost, byte[] Data, double Unix = double.NaN, bool Cut = false)
{
    /// <summary>The HCI connection handle this packet belongs to.</summary>
    public int Connection => Data.Length >= 2 ? (Data[0] | Data[1] << 8) & 0x0FFF : -1;
}

/// <summary>One Bluetooth log found inside an archive.</summary>
public sealed record LogCandidate(string Name, DateTimeOffset Modified, byte[] Bytes);

/// <summary>
/// Reads the Bluetooth log an Android phone keeps when "Bluetooth HCI snoop log" is on (and the bug report that
/// carries it). The format is the BTSnoop file: a 16-byte header, then records of 24 bytes plus the packet. Only the
/// ACL data packets are kept, because that is where a controller app's messages travel. Nothing is ever sent or
/// written; the bytes are only read and cut into pieces.
/// </summary>
public static class BtSnoop
{
    /// <summary>A log longer than this is refused: a short recording of one change is a few hundred kilobytes.</summary>
    public const int MaxLogBytes = 64 * 1024 * 1024;
    public const long MaxBugReportBytes = 512L * 1024 * 1024;
    /// <summary>All the logs read out of one archive together may not exceed this.</summary>
    public const long MaxCandidateBytes = 256L * 1024 * 1024;
    static readonly byte[] Magic = [(byte)'b', (byte)'t', (byte)'s', (byte)'n', (byte)'o', (byte)'o', (byte)'p', 0];
    static readonly string[] LogNames = ["btsnoop_hci.log", "btsnoop_hci.cfa", "btsnoop.log"];
    /// <summary>A btsnoop time is microseconds since the year 0; this is 1 Jan 1970 on that scale (the BTSnoop format,
    /// as Android writes it and Wireshark reads it).</summary>
    const long UnixEpoch = 0x00dcddb30f2f8000;
    /// <summary>Times outside these years are not a phone's clock (a hand-made or relative log), so they are not used.</summary>
    static readonly long ClockFrom = new DateTimeOffset(2015, 1, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds(), ClockTo = new DateTimeOffset(2045, 1, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();

    /// <summary>Whether a file in a bug report is one of the phone's Bluetooth logs: the current one, and the earlier or
    /// cut-down ones Android keeps beside it ("btsnoop_hci.log.last", "btsnoop_hci.log.filtered").</summary>
    public static bool IsLogName(string name) => LogNames.Contains(name, StringComparer.OrdinalIgnoreCase) || name.StartsWith("btsnoop_hci", StringComparison.OrdinalIgnoreCase);

    public static bool LooksLikeLog(ReadOnlySpan<byte> data) => data.Length >= 16 && data[..8].SequenceEqual(Magic);

    public static List<HciRecord> Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length > MaxLogBytes) throw new InvalidDataException("This log is over 64 MB. Record a shorter session: one connection, one change.");
        if (!LooksLikeLog(data)) throw new InvalidDataException("This is not a Bluetooth snoop log.");
        uint version = BinaryPrimitives.ReadUInt32BigEndian(data[8..]), link = BinaryPrimitives.ReadUInt32BigEndian(data[12..]);
        if (version != 1 || link is not (1001 or 1002)) throw new InvalidDataException($"This snoop log type ({version}/{link}) isn't supported. Android's own log is.");
        var records = new List<HciRecord>();
        int offset = 16; long? first = null;
        while (offset < data.Length)
        {
            if (data.Length - offset < 24) throw new InvalidDataException("The log ends in the middle of a record.");
            uint original = BinaryPrimitives.ReadUInt32BigEndian(data[offset..]), included = BinaryPrimitives.ReadUInt32BigEndian(data[(offset + 4)..]);
            uint flags = BinaryPrimitives.ReadUInt32BigEndian(data[(offset + 8)..]);
            long stamp = BinaryPrimitives.ReadInt64BigEndian(data[(offset + 16)..]);
            if (included == 0 || included > original || included > data.Length - offset - 24) throw new InvalidDataException("The log has a damaged packet.");
            var packet = data.Slice(offset + 24, (int)included);
            offset += 24 + (int)included;
            first ??= stamp;
            // bit 0 of the flags: 0 = the phone sent it, 1 = the phone received it. Type 2 is ACL data.
            bool fromHost = (flags & 1) == 0;
            byte[] body;
            if (link == 1002) { if (packet[0] != 2) continue; body = packet[1..].ToArray(); }
            else { if ((flags & 2) != 0) continue; body = packet.ToArray(); }
            double unix = (stamp - UnixEpoch) / 1e6;
            records.Add(new HciRecord((stamp - first.Value) / 1e6, fromHost, body, unix >= ClockFrom && unix < ClockTo ? unix : double.NaN, included < original));
        }
        if (records.Count == 0) throw new InvalidDataException("The log holds no data packets. Was Bluetooth switched off and on after turning the snoop log on?");
        return records;
    }

    /// <summary>The Bluetooth log inside an Android bug-report ZIP, without extracting anything else from it.</summary>
    public static byte[] ReadFromBugReport(string zipPath, out string usedName)
    {
        var info = new FileInfo(zipPath);
        if (!info.Exists) throw new FileNotFoundException("That file isn't there any more.", zipPath);
        if (info.Length > MaxBugReportBytes) throw new InvalidDataException("This bug report is over 512 MB. Ask for help extracting the Bluetooth log.");
        using var archive = ZipFile.OpenRead(zipPath);
        if (archive.Entries.Count > 100_000) throw new InvalidDataException("This bug report holds too many files.");
        var entries = archive.Entries.Where(e => e.Length > 0 && IsLogName(e.Name)).ToList();
        if (entries.Count == 0) throw new InvalidDataException("No Bluetooth log is in this bug report. Turn the snoop log on, switch Bluetooth off and on, repeat the test, then take a new bug report.");
        var candidates = new List<LogCandidate>(); long total = 0;
        foreach (var entry in entries)
        {
            if (entry.Length > MaxLogBytes || (total += entry.Length) > MaxCandidateBytes) continue;
            using var stream = entry.Open();
            var buffer = new byte[entry.Length];
            stream.ReadExactly(buffer);
            candidates.Add(new(entry.FullName, entry.LastWriteTime, buffer));
        }
        if (candidates.Count == 0) throw new InvalidDataException("The Bluetooth log in this report is over 64 MB. Record a shorter session.");
        return Pick(candidates, data => Parse(data), out usedName);
    }

    /// <summary>
    /// The log that holds the test, of those an archive carries. A phone keeps older logs beside the current one, and the
    /// biggest is not necessarily the newest or the one with the controller in it. The log with the most KeyLinker frames
    /// wins; then, when none has any, the newest; then the largest. The name says which was chosen and why.
    /// </summary>
    public static byte[] Pick(List<LogCandidate> candidates, Func<byte[], List<HciRecord>> parse, out string usedName)
    {
        int Frames(LogCandidate c)
        {
            try
            {
                var trace = AttTrace.From(parse(c.Bytes));
                return trace.Messages.Count(m => KeyLinkerCodec.TryDecode(m.Value) is { CrcValid: true } f && f.DeclaredLength == m.Value.Length);
            }
            catch (InvalidDataException) { return -1; }   // damaged or empty: last choice
        }
        var scored = candidates.Select(c => (c, frames: Frames(c))).OrderByDescending(x => x.frames).ThenByDescending(x => x.c.Modified).ThenByDescending(x => x.c.Bytes.Length).ToList();
        var (best, frames) = scored[0];
        usedName = best.Name + (candidates.Count == 1 ? "" : $" (one of {candidates.Count} Bluetooth logs; " +
            (frames > 0 ? "the one with the controller's messages)" : "none holds KeyLinker messages, so the newest)"));
        return best.Bytes;
    }

    /// <summary>A recording file of either kind: a snoop log, or a bug report that holds one.</summary>
    public static List<HciRecord> ReadFile(string path, out string source)
    {
        var (records, from, _) = Load(path);
        source = from;
        return records;
    }

    /// <summary>A recording file of any kind, with the log's own bytes (from inside the archive, if it was one) and
    /// the extension that names them: an Android snoop log or bug report, or an iPhone sysdiagnose or PacketLogger file.</summary>
    public static (List<HciRecord> Records, string Source, byte[] Log) Load(string path) { var r = LoadAny(path); return (r.Records, r.Source, r.Log); }

    public static (List<HciRecord> Records, string Source, byte[] Log, string Extension) LoadAny(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists) throw new FileNotFoundException("That file isn't there any more.", path);
        Span<byte> head = stackalloc byte[16];
        int n;
        using (var f = File.OpenRead(path)) n = f.Read(head);
        head = head[..n];
        if (head.Length >= 4 && head[0] == 'P' && head[1] == 'K')
        {
            var inner = ReadFromBugReport(path, out var name);
            return (Parse(inner), "Android bug report: " + name, inner, "btsnoop");
        }
        if (head.Length >= 2 && head[0] == 0x1F && head[1] == 0x8B || path.EndsWith(".tar", StringComparison.OrdinalIgnoreCase))
        {
            var inner = PacketLogger.ReadFromSysdiagnose(path, out var name);
            return (PacketLogger.Parse(inner), "iPhone sysdiagnose: " + name, inner, "pklg");
        }
        if (info.Length > MaxLogBytes) throw new InvalidDataException("This log is over 64 MB. Record a shorter session.");
        var log = File.ReadAllBytes(path);
        if (LooksLikeLog(log)) return (Parse(log), "Android snoop log", log, "btsnoop");
        if (PacketLogger.LooksLikeLog(log)) return (PacketLogger.Parse(log), "iPhone Bluetooth log", log, "pklg");
        throw new InvalidDataException("This isn't a Bluetooth recording X20CTL can read: an Android bug report or snoop log, or an iPhone sysdiagnose or .pklg file.");
    }
}
