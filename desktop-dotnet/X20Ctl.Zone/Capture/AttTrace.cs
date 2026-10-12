using System.Buffers.Binary;
namespace X20Ctl.Zone.Capture;

public enum AttKind { Write, WriteCommand, Notification, Indication, ReadResponse }

/// <summary>One value that went over Bluetooth's attribute protocol: what the phone's app wrote to the controller, or what the controller sent back.</summary>
/// <c>Connection</c> is the HCI connection handle: a phone talks to its earbuds, its watch and the controller at once, each on its own.
public sealed record AttMessage(double Seconds, bool FromPhone, AttKind Kind, int Handle, byte[] Value, int Connection = 0);

/// <summary>
/// The messages of a recording, in time order. ACL packets are put back together into L2CAP frames, ATT frames are cut
/// out of those, and the writes, notifications and reads become <see cref="AttMessage"/>s. When the recording also holds the
/// phone discovering the controller's services, each value handle is matched to its characteristic's UUID.
/// </summary>
public sealed class AttTrace
{
    public List<AttMessage> Messages { get; } = new();
    /// <summary>Value handle to UUID, when the recording includes discovery. Often it doesn't: Android caches it.
    /// Handles are per connection; in a trace of several connections this holds every one, the later winning.</summary>
    public Dictionary<int, string> Characteristics { get; } = new();
    readonly Dictionary<(int Connection, int Handle), string> uuids = new();
    public int AclPackets { get; private set; }
    public int AttPackets { get; private set; }
    /// <summary>The connections this trace holds, in the order they first appear.</summary>
    public List<int> Connections { get; } = new();

    /// <summary>Only one connection's messages and characteristics.</summary>
    public AttTrace Only(int connection)
    {
        var t = new AttTrace { AclPackets = AclPackets, AttPackets = AttPackets };
        t.Connections.Add(connection);
        t.Messages.AddRange(Messages.Where(m => m.Connection == connection));
        foreach (var ((c, h), uuid) in uuids) if (c == connection) { t.uuids[(c, h)] = uuid; t.Characteristics[h] = uuid; }
        return t;
    }

    /// <summary>
    /// The connection the controller app used: the one with the most valid KeyLinker frames, or, when none decode, the
    /// one the phone wrote to most. -1 when the trace holds no messages. The second value says whether KeyLinker frames
    /// picked it; when they didn't, which connection is the controller is a guess.
    /// </summary>
    public (int Connection, bool ByFrames) ControllerConnection()
    {
        if (Messages.Count == 0) return (-1, false);
        var frames = Messages.Where(m => KeyLinkerCodec.TryDecode(m.Value) is { CrcValid: true } f && f.DeclaredLength == m.Value.Length)
            .GroupBy(m => m.Connection).OrderByDescending(g => g.Count()).FirstOrDefault();
        if (frames is not null) return (frames.Key, true);
        var writes = Messages.Where(m => m.FromPhone && m.Kind is AttKind.Write or AttKind.WriteCommand).GroupBy(m => m.Connection).OrderByDescending(g => g.Count()).FirstOrDefault();
        return (writes?.Key ?? Messages[0].Connection, false);
    }

    public string Name(int handle) => Characteristics.TryGetValue(handle, out var uuid) ? uuid : $"handle 0x{handle:X4}";

    public static AttTrace From(IEnumerable<HciRecord> records)
    {
        var trace = new AttTrace();
        // L2CAP frames can span several ACL packets; keep one partial frame per connection and direction
        var partial = new Dictionary<(int Connection, bool FromHost), (byte[] Buffer, int Expected, int Have)>();
        // a read request and its response, and a discovery request and its reply, pair up within one connection
        var lastReadHandle = new Dictionary<int, int>(); var requestedType = new Dictionary<int, int>();
        foreach (var record in records)
        {
            var d = record.Data;
            if (d.Length < 4) continue;
            trace.AclPackets++;
            ushort head = BinaryPrimitives.ReadUInt16LittleEndian(d);
            int connection = head & 0x0FFF, boundary = (head >> 12) & 3;
            int length = BinaryPrimitives.ReadUInt16LittleEndian(d.AsSpan(2));
            if (length > d.Length - 4) continue;
            var chunk = d.AsSpan(4, length);
            var key = (connection, record.FromHost);
            byte[]? frame = null;
            if (boundary is 0 or 2)
            {   // the start of an L2CAP frame: its own 4-byte header says how long it is and which channel
                if (chunk.Length < 4) continue;
                int l2 = BinaryPrimitives.ReadUInt16LittleEndian(chunk), cid = BinaryPrimitives.ReadUInt16LittleEndian(chunk[2..]);
                if (cid != 4) { partial.Remove(key); continue; }   // 4 is ATT; other channels aren't a controller app's messages
                var body = chunk[4..];
                if (body.Length >= l2) frame = body[..l2].ToArray();
                else { var buffer = new byte[l2]; body.CopyTo(buffer); partial[key] = (buffer, l2, body.Length); }
            }
            else if (boundary == 1 && partial.TryGetValue(key, out var open))
            {
                int take = Math.Min(chunk.Length, open.Expected - open.Have);
                chunk[..take].CopyTo(open.Buffer.AsSpan(open.Have));
                open.Have += take;
                if (open.Have >= open.Expected) { frame = open.Buffer; partial.Remove(key); } else partial[key] = open;
            }
            if (frame is null || frame.Length == 0) continue;
            trace.AttPackets++;
            if (!trace.Connections.Contains(connection)) trace.Connections.Add(connection);
            Process(trace, record.Seconds, record.FromHost, connection, frame, lastReadHandle, requestedType);
        }
        return trace;
    }

    static void Process(AttTrace trace, double seconds, bool fromHost, int connection, byte[] pdu, Dictionary<int, int> lastRead, Dictionary<int, int> requested)
    {
        byte opcode = pdu[0];
        int lastReadHandle = lastRead.GetValueOrDefault(connection), requestedType = requested.GetValueOrDefault(connection);
        switch (opcode)
        {
            case 0x12 or 0x52 when pdu.Length >= 3:
                trace.Messages.Add(new(seconds, fromHost, opcode == 0x12 ? AttKind.Write : AttKind.WriteCommand, BinaryPrimitives.ReadUInt16LittleEndian(pdu.AsSpan(1)), pdu[3..], connection));
                break;
            case 0x1B or 0x1D when pdu.Length >= 3:
                trace.Messages.Add(new(seconds, fromHost, opcode == 0x1B ? AttKind.Notification : AttKind.Indication, BinaryPrimitives.ReadUInt16LittleEndian(pdu.AsSpan(1)), pdu[3..], connection));
                break;
            case 0x0A when pdu.Length >= 3:
                lastRead[connection] = BinaryPrimitives.ReadUInt16LittleEndian(pdu.AsSpan(1));
                break;
            case 0x0B when pdu.Length >= 1 && lastReadHandle != 0:
                trace.Messages.Add(new(seconds, fromHost, AttKind.ReadResponse, lastReadHandle, pdu[1..], connection));
                break;
            case 0x08 when pdu.Length is 7 or 21:
                // Read By Type request: a 16-bit type of 0x2803 asks for characteristic declarations
                requested[connection] = pdu.Length == 7 ? BinaryPrimitives.ReadUInt16LittleEndian(pdu.AsSpan(5)) : 0;
                break;
            case 0x09 when pdu.Length >= 2 && requestedType == 0x2803:
                int item = pdu[1];
                if (item is not (7 or 21)) break;
                for (int at = 2; at + item <= pdu.Length; at += item)
                {   // handle(2) properties(1) value handle(2) UUID(2 or 16)
                    int valueHandle = BinaryPrimitives.ReadUInt16LittleEndian(pdu.AsSpan(at + 3));
                    trace.Characteristics[valueHandle] = trace.uuids[(connection, valueHandle)] = Uuid(pdu.AsSpan(at + 5, item - 5));
                }
                break;
        }
    }

    /// <summary>A 16- or 128-bit UUID from its little-endian wire bytes, in the usual dashed form.</summary>
    public static string Uuid(ReadOnlySpan<byte> wire)
    {
        if (wire.Length == 2) return "0x" + BinaryPrimitives.ReadUInt16LittleEndian(wire).ToString("X4");
        if (wire.Length != 16) return Convert.ToHexString(wire);
        var h = new byte[16];
        for (int i = 0; i < 16; i++) h[i] = wire[15 - i];
        var s = Convert.ToHexString(h).ToLowerInvariant();
        return $"{s[..8]}-{s[8..12]}-{s[12..16]}-{s[16..20]}-{s[20..]}";
    }
}
