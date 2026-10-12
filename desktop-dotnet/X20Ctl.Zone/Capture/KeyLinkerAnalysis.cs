namespace X20Ctl.Zone.Capture;

/// <summary>One KeyLinker frame found in a recording.</summary>
public sealed record KlFrame(double Seconds, bool FromPhone, int Handle, KeyLinkerCodec.Frame Frame);

/// <summary>One byte that differs between what the controller held and what the app wrote.</summary>
public sealed record ByteChange(int Offset, byte Before, byte After);

/// <summary>
/// A write the app made, and how its content differs from what the controller last reported when read. A write is only
/// a request: <c>Answered</c> says whether the controller replied to it, and <c>ReadBack</c> whether a later read in the
/// recording held what was written ("matches", "differs", "unclear" when the two don't line up, or "not read back"). Only a matching read-back is evidence
/// the controller took it, and even that says nothing about surviving a power-off.
/// </summary>
public sealed record SettingChange(double Seconds, string Setting, byte Opcode, string Before, string After, IReadOnlyList<ByteChange> Bytes, string Note,
    bool Answered = false, string ReadBack = "not read back");

/// <summary>
/// What a recording of the official KeyLinker app says, decoded: the controller's identity and capability descriptor
/// (the same questions the app asks every pad), its key lists, the settings the app read, and every write it made with
/// the bytes that changed. Nothing in a recording is trusted blindly: a message only counts as a KeyLinker frame when
/// its CRC is valid and its declared length matches, and a reply only counts when it answers a request in the recording.
/// </summary>
public sealed class KeyLinkerAnalysis
{
    public List<KlFrame> Frames { get; } = new();
    public IdentityInfo? Identity { get; private set; }
    public byte[]? Descriptor { get; private set; }
    /// <summary>Menu kinds 3 to 7 as the bytes the controller sent (size byte removed): source keys, turbo keys, macro keys, macro limits, select list.</summary>
    public Dictionary<int, byte[]> Lists { get; } = new();
    public List<SettingChange> Changes { get; } = new();
    /// <summary>Messages that were not KeyLinker frames: another protocol, or damaged.</summary>
    public int Other { get; private set; }
    public int Writes { get; private set; }
    /// <summary>The connection analysed, and how many others the recording held (ignored: other devices).</summary>
    public int Connection { get; private set; } = -1;
    public int OtherConnections { get; private set; }

    static readonly Dictionary<byte, string> Names = new()
    {
        [0x90] = "Read name", [0x91] = "Read identity", [0x93] = "Read GUID", [0x97] = "Read button mode", [0x98] = "Load button",
        [0x9A] = "Read 3D", [0xA4] = "Read press gun", [0xA5] = "Read turbo", [0xA6] = "Read slide screen", [0xA7] = "Read macro", [0xA8] = "Two-in-one state",
        [0xB0] = "Menu / capabilities", [0xB1] = "Read stick curves", [0xB2] = "Read trigger curves", [0xB3] = "Read motors", [0xB4] = "Read turbo keys",
        [0xB5] = "Read macro", [0xB6] = "Read remaps", [0xB7] = "Read lighting",
        [0x31] = "Write stick curves", [0x32] = "Write trigger curves", [0x33] = "Write motors", [0x34] = "Write turbo", [0x35] = "Write macro",
        [0x36] = "Write remaps", [0x37] = "Write lighting", [0x16] = "Factory reset", [0x19] = "Reply", [0x1D] = "Set mode", [0x1E] = "Normal mode",
    };
    static readonly Dictionary<byte, string> Settings = new()
    {
        [0x31] = "Stick curves", [0x32] = "Trigger curves", [0x33] = "Vibration and shutdown timer", [0x34] = "Turbo",
        [0x35] = "Macro", [0x36] = "Button remapping", [0x37] = "Lighting",
    };

    public static string Describe(byte opcode) => Names.TryGetValue(opcode, out var n) ? n : $"Command 0x{opcode:X2}";

    sealed record Pending(string Kind, byte Opcode, int Position, int Key);
    sealed class Chunk { public int Declared; public List<byte> Data = new(); public int Next; }
    sealed class Write { public double At; public byte Opcode; public int Slot; public List<byte> Payload = new(); public List<byte> Serials = new(); public bool Answered; }

    public static KeyLinkerAnalysis From(AttTrace trace)
    {
        var a = new KeyLinkerAnalysis();
        // one connection only: the controller's. Others are the phone's other devices, and their serials would collide
        var (connection, _) = trace.ControllerConnection();
        a.Connection = connection; a.OtherConnections = Math.Max(0, trace.Connections.Count - 1);
        if (trace.Connections.Count > 1) trace = trace.Only(connection);
        var pending = new Dictionary<byte, Pending>();
        var chunks = new Dictionary<int, Chunk>();                 // by menu kind, or 0x100 + read opcode (+ slot << 12)
        // what the pad last reported when read, per setting. A write never goes in here: it is a request, and may fail
        var known = new Dictionary<(int Setting, int Slot), (double At, byte[] Body)>();
        var unconfirmed = new Dictionary<(int Setting, int Slot), (int Index, byte[] Payload)>();   // writes awaiting a read
        var writeSerials = new Dictionary<byte, int>();            // a write's serial, to the change it belongs to
        Write? open = null;

        void Flush()
        {
            if (open is null) return;
            a.Writes++;
            a.Changes.Add(a.Compare(open, known) with { Answered = open.Answered });
            int index = a.Changes.Count - 1;
            foreach (var serial in open.Serials) writeSerials[serial] = index;
            unconfirmed[(open.Opcode + 0x80, open.Slot)] = (index, open.Payload.ToArray());
            open = null;
        }

        foreach (var message in trace.Messages)
        {
            if (message.Kind == AttKind.ReadResponse) continue;
            var frame = KeyLinkerCodec.TryDecode(message.Value);
            if (frame is null || !frame.CrcValid || frame.DeclaredLength != message.Value.Length) { a.Other++; continue; }
            a.Frames.Add(new(message.Seconds, message.FromPhone, message.Handle, frame));
            byte op = frame.Opcode; var payload = frame.Payload;

            if (message.FromPhone)
            {
                pending.Remove(frame.Serial);
                if (op is not (>= 0x31 and <= 0x37)) Flush();
                if (op == 0x91 && payload.Length == 0) { pending[frame.Serial] = new("identity", op, 0, 0); continue; }
                if (op == 0xB0 && payload.Length == 2 && payload[1] is 1 or 3 or 4 or 5 or 6 or 7)
                {
                    pending[frame.Serial] = new("menu", op, payload[0], payload[1]);
                    if (payload[0] == 0) chunks.Remove(payload[1]);
                    continue;
                }
                if (op is >= 0xB1 and <= 0xB7 && payload.Length is 1 or 2)
                {   // a macro read carries [position, slot]; the other records a single index
                    int slot = op == 0xB5 && payload.Length == 2 ? payload[1] : 0, position = payload[0];
                    int key = 0x100 + op + (slot << 12);
                    pending[frame.Serial] = new("read", op, position, key);
                    if (position == 0) chunks.Remove(key);
                    continue;
                }
                if (op is >= 0x31 and <= 0x37)
                {
                    int slot = op == 0x35 ? frame.Length >> 5 : 0;
                    if (open is not null && (open.Opcode != op || open.Slot != slot || message.Seconds - open.At > 2.0)) Flush();
                    open ??= new Write { At = message.Seconds, Opcode = op, Slot = slot };
                    open.Payload.AddRange(payload); open.Serials.Add(frame.Serial);
                    continue;
                }
                continue;
            }

            if (op != 0x19) continue;
            if (!pending.ContainsKey(frame.Serial))
            {   // a reply to a write: the controller answered it, which is not the same as storing it. A long write goes
                // out as several packets with a reply after each, so a reply doesn't end the write it belongs to
                if (open is not null && open.Serials.Contains(frame.Serial)) open.Answered = true;
                else if (writeSerials.Remove(frame.Serial, out var w)) a.Changes[w] = a.Changes[w] with { Answered = true };
                continue;
            }
            if (!pending.Remove(frame.Serial, out var request) || payload.Length == 0) continue;
            int chunkKey = request.Kind == "menu" ? request.Key : request.Kind == "read" ? request.Key : -1;
            Chunk c;
            if (request.Position == 0)
            {
                c = new Chunk { Declared = payload[0] };
                c.Data.AddRange(payload.AsSpan(1).ToArray());
                chunks[chunkKey] = c;
            }
            else
            {
                if (!chunks.TryGetValue(chunkKey, out c!) || c.Next != request.Position - 1) { chunks.Remove(chunkKey); continue; }
                c.Data.AddRange(payload);
            }
            c.Next = request.Position;
            if (c.Data.Count < c.Declared) continue;
            if (c.Data.Count > c.Declared) c.Data.RemoveRange(c.Declared, c.Data.Count - c.Declared);
            chunks.Remove(chunkKey);
            var body = c.Data.ToArray();
            switch (request.Kind)
            {
                case "identity": a.Identity = IdentityInfo.Parse(body); break;
                case "menu":
                    if (request.Key == 1) a.Descriptor = body; else a.Lists[request.Key] = body;
                    break;
                case "read":
                    var setting = (request.Opcode, request.Key >> 12 & 0xF);
                    known[setting] = (message.Seconds, body);
                    if (unconfirmed.Remove(setting, out var u))
                        a.Changes[u.Index] = a.Changes[u.Index] with { ReadBack = Lines(u.Payload, body) switch { true => "matches", false => "differs", null => "unclear" } };
                    break;
            }
        }
        Flush();
        return a;
    }

    SettingChange Compare(Write write, Dictionary<(int Setting, int Slot), (double At, byte[] Body)> known)
    {
        string name = Settings.TryGetValue(write.Opcode, out var s) ? s : Describe(write.Opcode);
        if (write.Opcode == 0x35) name += $" M{write.Slot + 1}";
        var after = write.Payload.ToArray();
        if (!known.TryGetValue((write.Opcode + 0x80, write.Slot), out var before))
            return new(write.At, name, write.Opcode, "", Hex(after), [], "The recording holds no read of this setting before the write, so nothing to compare it with.");
        var was = before.Body;
        // the record is stored with or without its own size byte, depending on the setting; try the two layouts
        var changes = new List<ByteChange>();
        string note; bool lined = true;
        if (after.Length == was.Length) { Diff(was, after, 0, changes); note = "Same length as the controller's record."; }
        else if (after.Length == was.Length + 1) { Diff(was, after[1..], 0, changes); note = "The write carries its own size byte; the rest lines up with the controller's record."; }
        else if (after.Length + 1 == was.Length) { Diff(was[1..], after, 1, changes); note = "The controller's record carries a size byte; the rest lines up with the write."; }
        else { lined = false; note = $"The write ({after.Length} bytes) and the controller's record ({was.Length} bytes) don't line up, so both are shown whole."; }
        if (lined && changes.Count == 0) note += " Nothing changed in it.";
        return new(write.At, name, write.Opcode, Hex(was), Hex(after), changes, note);
    }

    /// <summary>Whether a write and a later read of the same record hold the same bytes, allowing for the size byte
    /// one of them may carry; null when their lengths don't line up at all.</summary>
    static bool? Lines(byte[] written, byte[] read)
    {
        if (written.Length == read.Length) return written.AsSpan().SequenceEqual(read);
        if (written.Length == read.Length + 1) return written.AsSpan(1).SequenceEqual(read);
        if (written.Length + 1 == read.Length) return written.AsSpan().SequenceEqual(read.AsSpan(1));
        return null;
    }

    static void Diff(byte[] before, byte[] after, int shift, List<ByteChange> into)
    {
        for (int i = 0; i < Math.Min(before.Length, after.Length); i++)
            if (before[i] != after[i]) into.Add(new(i + shift, before[i], after[i]));
    }

    public static string Hex(byte[] bytes) => Convert.ToHexString(bytes).ToLowerInvariant();

    /// <summary>This recording's fingerprint, comparable with another controller's or with the recorded X20.</summary>
    public Fingerprint ToFingerprint()
    {
        var lists = new Dictionary<string, IReadOnlyList<int>>();
        void Keys(string name, int kind) { if (Lists.TryGetValue(kind, out var d)) try { lists[name] = DecodeKeys(d); } catch (InvalidDataException) { } }
        Keys("changekeySources", 3); Keys("turboKeys", 4); Keys("macroKeys", 5); Keys("selectList", 7);
        if (Lists.TryGetValue(6, out var limits) && limits.Length % 2 == 0)
            lists["macroLimits"] = Enumerable.Range(0, limits.Length / 2).Select(i => limits[2 * i] | limits[2 * i + 1] << 8).ToList();
        return new(Identity, Descriptor is null ? null : Hex(Descriptor), lists);
    }

    /// <summary>A key list: a count byte, then literal key codes, with a byte whose top bit is set meaning a run of consecutive codes.</summary>
    public static List<int> DecodeKeys(ReadOnlySpan<byte> data)
    {
        var keys = new List<int>();
        if (data.IsEmpty) return keys;
        int expected = data[0];
        foreach (var b in data[1..])
        {
            if (keys.Count >= expected) break;
            if ((b & 0x80) != 0)
            {
                if (keys.Count == 0) throw new InvalidDataException("A run marker has no key before it.");
                for (int i = 0; i < (b & 0x7F) - 1; i++) keys.Add(keys[^1] + 1);
            }
            else keys.Add(b);
        }
        if (keys.Count != expected) throw new InvalidDataException("The key list declares a different count than it holds.");
        return keys;
    }

    /// <summary>The conversation in order, one line each, for reading.</summary>
    public IEnumerable<string> Timeline()
    {
        foreach (var f in Frames)
        {
            string who = f.FromPhone ? "phone → pad" : "pad → phone";
            string text = f.Frame.Opcode == 0x19 ? "Reply" : Describe(f.Frame.Opcode);
            yield return $"{f.Seconds,8:0.000}s  {who}  {text,-26} serial {f.Frame.Serial,3}  {Hex(f.Frame.Payload)}";
        }
    }
}
