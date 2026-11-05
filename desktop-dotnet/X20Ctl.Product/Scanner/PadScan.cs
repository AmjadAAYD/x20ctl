using System.IO;
using System.Text;

namespace X20Ctl.Product.Scanner;

/// <summary>Which questions each stage of the Write scan may send. Identify and read-back can never write.</summary>
public enum ScanStage { Identify, ReadBack, WriteBack }

public enum PadAnswer { LikeX20, LikeX20Pro, Different, Silent }

public sealed record PadVerdict(PadAnswer Answer, string Headline, IReadOnlyList<string> Reasons)
{
    /// <summary>Only a pad that answered like the X20 family goes on to read its records.</summary>
    public bool CanReadBack => Answer is PadAnswer.LikeX20 or PadAnswer.LikeX20Pro;
}

public delegate Task<byte[]> PadExchange(byte op, byte[] payload, byte serial, byte length);

/// <summary>One stored record: what the pad returned for a read, and the command that would put it back.</summary>
public sealed record PadRecord(string Name, byte ReadOp, byte WriteOp, byte[] Args, byte[] Bytes);

public sealed record WriteBackItem(string Name, bool Acknowledged, bool Identical, bool Restored, string Detail);

/// <summary>The write test: per record, what the pad said and what it holds now.</summary>
public sealed record WriteBackResult(IReadOnlyList<WriteBackItem> Items, bool Stopped, bool Persisted, string Summary)
{
    public bool AllAcknowledged => Items.Count > 0 && Items.All(i => i.Acknowledged);
    public bool AllIdentical => Items.Count > 0 && Items.All(i => i.Identical);
}

public sealed record VisualResult(string Test, string Outcome, string Note);

public static class PadScan
{
    public static bool Allows(ScanStage stage, byte op) => stage switch
    {
        ScanStage.Identify => op is 0x91 or 0x93 or 0xB0,
        ScanStage.ReadBack => op is 0x91 or 0x93 or 0xB0 or 0xB1 or 0xB2 or 0xB3 or 0xB5 or 0xB6,
        ScanStage.WriteBack => Allows(ScanStage.ReadBack, op) || op is 0x31 or 0x32 or 0x35 or 0x36,
        _ => false,
    };

    static readonly byte[] Ack = [2, 0xdf, 0xaa];

    // ---------- stage 1: who answers ----------
    /// <summary>Raw reply payloads of the three identify questions, length prefix included; null when the pad stayed silent.</summary>
    public static async Task<PadCheck.Raw> Identify(PadExchange x, Func<byte> next)
    {
        async Task<byte[]?> Ask(byte op, byte[] args) { try { return await x(op, args, next(), (byte)(args.Length == 0 ? 5 : args.Length + 5)); } catch (TimeoutException) { return null; } }
        var identity = await Ask(0x91, []);
        if (identity is null) return new(null, null, null, null);
        var guid0 = await Ask(0x93, [0]);
        var guid1 = guid0 is { Length: > 1 } && guid0[0] == 18 && guid0.Length - 1 < 18 ? await Ask(0x93, [1]) : null;
        var menu = await Ask(0xB0, [0, 1]);
        return new(identity, menu, guid0, guid1);
    }

    /// <summary>The verdict comes from the replies: a recorded identity, or the shape of the capability menu. It says "answers like", never "is".</summary>
    public static PadVerdict Classify(PadCheck.Raw raw)
    {
        var id = PadCheck.ParseIdentity(raw.Identity);
        if (id is null) return new(PadAnswer.Silent, "Your pad doesn't answer.", ["No reply to the identity question (0x91). Silence is not a result: wake the pad, make sure it is on Bluetooth, and try again."]);
        var caps = PadCheck.ParseCapabilities(raw.Menu);
        var guid = PadCheck.ParseGuid(raw.Guid0, raw.Guid1);
        var why = new List<string> { $"Identity: vendor {id.Vid}, product {id.Pid}, firmware {id.Firmware}." };
        if (guid is not null) why.Add($"Updater id: model code {guid.ModelCode}" + (guid.Model is null ? " (not in the known list)." : $" ({guid.Model})."));
        if (caps is not null) why.Add($"Capability menu: {(caps.MacroSlots.Count == 0 ? "no macro slots" : string.Join(", ", caps.MacroSlots))}, remap {(caps.ChangeKey == 0 ? "off" : "on")}.");
        else why.Add("No capability menu came back (0xB0).");

        if (id is { Vid: "1013", Pid: "2009" }) return new(PadAnswer.LikeX20, "Your pad answers like an X20.", [.. why, "Its identity is the recorded X20's."]);
        if (id is { Vid: "1020", Pid: "2006" }) return new(PadAnswer.LikeX20Pro, "Your pad answers like an X20 Pro.", [.. why, "Its identity is the recorded X20 Pro's."]);
        // an identity nobody recorded: learn from the shape of the reply
        if (caps is { ChangeKey: not 0 } && caps.MacroSlots.Count is 4 or 6)
        {
            bool pro = caps.MacroSlots.Count == 6;
            return new(pro ? PadAnswer.LikeX20Pro : PadAnswer.LikeX20, pro ? "Your pad answers like an X20 Pro." : "Your pad answers like an X20.",
                [.. why, $"Its identity was never recorded; it is matched only by the shape of its replies ({caps.MacroSlots.Count} macro slots, remap on). That is not proof it takes the same commands."]);
        }
        return new(PadAnswer.Different, "Your pad answers differently.", [.. why, "Its replies don't have the X20 family's shape, so nothing further is read or written."]);
    }

    // ---------- stage 2: read the records ----------
    static async Task<byte[]> ReadRecord(PadExchange x, Func<byte> next, byte op, byte[] args, Func<byte[], int> size)
    {
        byte length = (byte)(5 + args.Length);
        var first = await x(op, args, next(), length);
        if (first.Length == 0) throw new IOException("The pad returned an empty record.");
        int expected = size(first);
        var data = new List<byte>(first);
        for (byte index = 1; data.Count < expected; index++)
        {
            if (index >= 16) throw new IOException("The pad's record is too long.");
            data.AddRange(await x(op, [index, .. args[1..]], next(), length));
        }
        if (data.Count != expected) throw new IOException("The pad's record has an unexpected length.");
        return [.. data];
    }
    static int Plain(byte[] first) => first[0] + 1;
    static int MacroSize(byte[] first) => first[0] == 0 ? 1 : 3 + 5 * first[0];

    /// <summary>Remap, sticks, triggers and every macro slot the capability menu lists (M1 to M6 are the ones readable).</summary>
    public static async Task<List<PadRecord>> ReadAll(PadExchange x, Func<byte> next, int macroSlots)
    {
        var all = new List<PadRecord>();
        await ReadRecord(x, next, 0xB0, [0, 6], Plain); // the macro limits, so macro records count entries
        async Task Add(string name, byte read, byte write, byte[] args, Func<byte[], int> size) => all.Add(new(name, read, write, args, await ReadRecord(x, next, read, args, size)));
        await Add("remap", 0xB6, 0x36, [0], Plain);
        await Add("sticks", 0xB1, 0x31, [0], Plain);
        await Add("triggers", 0xB2, 0x32, [0], Plain);
        for (int slot = 0; slot < Math.Clamp(macroSlots, 0, 6); slot++) await Add($"macro M{slot + 1}", 0xB5, 0x35, [0, (byte)slot], MacroSize);
        return all;
    }

    // ---------- stage 3: write back exactly what was read ----------
    static async Task<bool> Send(PadExchange x, PadRecord r)
    {
        bool acked = true;
        for (int i = 0; i * 15 < r.Bytes.Length; i++)
        {
            var chunk = r.Bytes.Skip(i * 15).Take(15).ToArray();
            byte serial, length;
            if (r.WriteOp == 0x35) { serial = (byte)((i << 3) | ((i % 2) << 7)); length = (byte)((r.Args[1] << 5) | (chunk.Length + 5)); }
            else { int counter = i + 1; serial = (byte)(((counter & 1) << 7) | (i << 3) | (counter & 7)); length = (byte)(((counter & 7) << 5) | (chunk.Length + 5)); }
            byte[] reply;
            try { reply = await x(r.WriteOp, chunk, serial, length); }
            catch (TimeoutException) { acked = false; await Task.Delay(150); continue; }
            if (!reply.SequenceEqual(Ack)) acked = false;
            await Task.Delay(150);
        }
        return acked;
    }

    /// <summary>For each record: write back the bytes just read, read again. A record that reads back different stops the test and is put back once.</summary>
    public static async Task<WriteBackResult> WriteBack(PadExchange x, Func<byte> next, IReadOnlyList<PadRecord> records)
    {
        var items = new List<WriteBackItem>(); bool stopped = false;
        foreach (var r in records)
        {
            bool acked = await Send(x, r);
            byte[]? again; try { again = await ReadRecord(x, next, r.ReadOp, r.Args, r.WriteOp == 0x35 ? MacroSize : Plain); } catch (Exception e) when (e is TimeoutException or IOException) { again = null; }
            bool same = again is not null && (r.WriteOp == 0x36 ? SameRemap(r.Bytes, again) : again.SequenceEqual(r.Bytes));
            if (same) { items.Add(new(r.Name, acked, true, false, acked ? "acknowledged, reads back identical" : "no acknowledgement, but reads back identical")); continue; }
            // different, or unreadable: stop, put the original back once, and say whether that worked
            await Send(x, r);
            byte[]? check; try { check = await ReadRecord(x, next, r.ReadOp, r.Args, r.WriteOp == 0x35 ? MacroSize : Plain); } catch (Exception e) when (e is TimeoutException or IOException) { check = null; }
            bool restored = check is not null && (r.WriteOp == 0x36 ? SameRemap(r.Bytes, check) : check.SequenceEqual(r.Bytes));
            items.Add(new(r.Name, acked, false, restored, (again is null ? "could not be read again" : "read back different") + (restored ? "; the original was put back and verified" : "; the original could NOT be verified after putting it back")));
            stopped = true; break;
        }
        string summary = stopped ? "Stopped at the first record that did not read back identical." : "Every record read back identical.";
        return new(items, stopped, false, summary);
    }

    /// <summary>Writes arbitrary bytes to a record the scan has already read, for the button-map test; true when the pad acknowledged.</summary>
    public static Task<bool> Put(PadExchange x, PadRecord record, byte[] bytes) => Send(x, record with { Bytes = bytes });

    /// <summary>Reads one record again.</summary>
    public static Task<byte[]> Reread(PadExchange x, Func<byte> next, PadRecord r) => ReadRecord(x, next, r.ReadOp, r.Args, r.WriteOp == 0x35 ? MacroSize : Plain);

    // a never-remapped pad answers a lone zero or an all-default record: both mean no remaps
    static bool SameRemap(byte[] a, byte[] b) { try { return X20Ctl.Zone.ControllerRecords.SameRemap(a, b); } catch (FormatException) { return a.SequenceEqual(b); } }

    /// <summary>After a fresh connection: does the pad still hold what it held before the write test?</summary>
    public static bool Persists(IReadOnlyList<PadRecord> before, IReadOnlyList<PadRecord> after) =>
        before.Count == after.Count && before.Zip(after).All(p => p.First.Name == p.Second.Name && (p.First.WriteOp == 0x36 ? SameRemap(p.First.Bytes, p.Second.Bytes) : p.First.Bytes.SequenceEqual(p.Second.Bytes)));

    public static string Hex(byte[] bytes) => Convert.ToHexString(bytes);

    public static string Describe(IReadOnlyList<PadRecord> records)
    {
        var s = new StringBuilder();
        foreach (var r in records) s.AppendLine($"  {r.Name,-12} {r.Bytes.Length,3} bytes  {Hex(r.Bytes)}");
        return s.ToString();
    }
}
