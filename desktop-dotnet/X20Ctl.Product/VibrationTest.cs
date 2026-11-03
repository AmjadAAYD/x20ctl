using System.Text;

namespace X20Ctl.Product;

/// <summary>One framed packet out, the reply with the same serial back; null on timeout or a failed write.</summary>
public interface IPadLink
{
    Task<byte[]?> SendAsync(byte[] packet, byte serial);
}

/// <summary>
/// Part B of the controller check: one write test, on vibration only. Save the motor record, change both motors, read back,
/// put the saved record back, read back again. It runs only on an identity, operating mode and firmware that are proven
/// (x20ctl/validation.py, exact firmware only), and only when the pad's capability record exposes motors.
/// Packets match the Python client byte for byte: X20.set_vibration, save_button_serial, host_length and the counters of
/// CodeHelper. The motor record also carries the idle timeout; the whole record is preserved and restored.
/// </summary>
public static class VibrationTest
{
    public const byte ReadMotor = 0xB3, WriteMotor = 0x33;

    /// <summary>Everything a write-test session may send: identify, capability menu, the motor record and its write.</summary>
    public static readonly byte[] SessionOpcodes = [0x91, 0xB0, ReadMotor, WriteMotor];

    private sealed record Proven(string Vid, string Pid, int Mode, string Firmware, string Name);

    private static readonly Proven[] Table =
    [
        new("1013", "2009", 2, "1.23", "EasySMX X20"),
        new("1020", "2006", 6, "1.44", "EasySMX X20 Pro"),
    ];

    /// <summary>The proven product this identity is, or null. Mode and firmware must both match exactly.</summary>
    public static string? ProvenFor(PadCheck.Identity? id) =>
        id is null ? null : Table.FirstOrDefault(t => t.Vid == id.Vid && t.Pid == id.Pid && t.Mode == id.Mode && t.Firmware == id.Firmware)?.Name;

    public static bool MayRun(PadCheck.Identity? id, PadCheck.Capabilities? caps) => ProvenFor(id) is not null && caps is { Motors: not 0 };

    /// <summary>The app's counters: serials alternate the high bit, the host counter counts down from 8, saves count up.</summary>
    public sealed class Session
    {
        private int serial, host = 8, save;
        public Func<byte> Nonce { get; init; } = () => (byte)Random.Shared.Next(256);
        public byte NextSerial() { serial++; return (byte)(serial % 2 == 0 ? serial % 128 + 128 : serial % 128); }
        public byte NextHost() { if (host == 0) host = 8; return (byte)--host; }
        public int NextSave() => ++save;
    }

    public static byte SaveSerial(int slot, int counter) => (byte)((counter % 2) << 7 | (slot & 0x0F) << 3 | counter % 8);
    public static byte HostLength(int payloadLength, int counter) => payloadLength <= 0 ? (byte)5 : (byte)((counter % 8) << 5 | (payloadLength + 5) & 0x1F);
    public static byte ToLevel(int percent) => (byte)Math.Round(percent * 255 / 100.0, MidpointRounding.ToEven);
    public static int ToPercent(byte level) => (int)Math.Round(level * 100 / 255.0, MidpointRounding.ToEven);

    public static Task<byte[]?> QueryAsync(IPadLink link, Session s, byte opcode, byte[] payload)
    {
        byte serial = s.NextSerial();
        return link.SendAsync(X20Codec.Frame(opcode, (byte)(payload.Length == 0 ? 5 : payload.Length + 5), serial, s.Nonce(), payload), serial);
    }

    public sealed record Step(string Name, bool Ok, string Detail);
    public sealed record Result(bool Passed, bool Restored, IReadOnlyList<Step> Steps, string? SavedHex, IReadOnlyList<string> Sent);

    public static async Task<Result> RunAsync(IPadLink link, PadCheck.Identity? id, PadCheck.Capabilities? caps, int percent, Session? session = null)
    {
        var s = session ?? new Session();
        var steps = new List<Step>(); var sent = new List<string>();
        Result Refuse(string why) { steps.Add(new("Not started", false, why + " Nothing was sent.")); return new(false, true, steps, null, sent); }
        if (ProvenFor(id) is null) return Refuse("This controller, mode and firmware are not on the proven list.");
        if (caps is not { Motors: not 0 }) return Refuse("The controller doesn't expose vibration over this protocol.");
        if (percent is < 0 or > 100) return Refuse("Pick a value from 0 to 100.");

        var saved = await ReadRecordAsync(link, s);
        if (saved is not { Data.Length: >= 2 } before)
        {
            steps.Add(new("Save", false, "No complete motor record came back. Nothing was changed."));
            return new(false, true, steps, null, sent);
        }
        string savedHex = Hex(before.Data);
        steps.Add(new("Save", true, $"Motors at {ToPercent(before.Data[0])}% and {ToPercent(before.Data[1])}%, record {savedHex}."));
        byte level = ToLevel(percent);
        if (before.Data[0] == level && before.Data[1] == level) return Refuse($"Both motors are already at {percent}%. Pick another value.");

        var changed = (byte[])before.Data.Clone(); changed[0] = changed[1] = level;
        var ack = await WriteRecordAsync(link, s, before.Declared, changed, sent);
        steps.Add(new("Change", ack is not null, ack is not null ? $"Both motors set to {percent}%; the controller answered." : "No answer. Treated as unknown and read back before anything else."));

        var after = await ReadRecordAsync(link, s);
        bool took = after is not null && after.Value.Data.AsSpan().SequenceEqual(changed);
        steps.Add(new("Read back", took, after is null ? "No complete record came back." : took ? "The record holds the new value, and nothing else changed." : $"The record reads {Hex(after.Value.Data)}, not what was written."));

        // put back whatever happened above: the change may have landed even without an answer
        var ack2 = await WriteRecordAsync(link, s, before.Declared, before.Data, sent);
        steps.Add(new("Put back", ack2 is not null, ack2 is not null ? "The saved record was sent; the controller answered." : "No answer to the restore."));

        var final = await ReadRecordAsync(link, s);
        bool restored = final is not null && final.Value.Data.AsSpan().SequenceEqual(before.Data);
        steps.Add(new("Read back again", restored, restored ? "The record matches the one saved before the test."
            : $"Not confirmed. The saved record was {savedHex}: set vibration back to {ToPercent(before.Data[0])}% in X20CTL or KeyLinker, and check the idle timeout."));
        return new(ack is not null && took && ack2 is not null && restored, restored, steps, savedHex, sent);
    }

    private static async Task<(int Declared, byte[] Data)?> ReadRecordAsync(IPadLink link, Session s)
    {
        var first = await QueryAsync(link, s, ReadMotor, [0]);
        if (first is not { Length: >= 1 }) return null;
        int declared = first[0]; var data = first.Skip(1).ToList();
        for (byte i = 1; data.Count < declared && i <= 8; i++)
        {
            var more = await QueryAsync(link, s, ReadMotor, [i]);
            if (more is null) break;
            data.AddRange(more);
        }
        return data.Count == declared ? (declared, data.ToArray()) : null;
    }

    private static Task<byte[]?> WriteRecordAsync(IPadLink link, Session s, int declared, byte[] data, List<string> sent)
    {
        byte[] payload = [(byte)declared, .. data];
        if (payload.Length > 15) throw new InvalidOperationException("The motor record is too long for one packet.");
        byte serial = SaveSerial(0, s.NextSave());
        byte[] packet = X20Codec.Frame(WriteMotor, HostLength(payload.Length, s.NextHost()), serial, s.Nonce(), payload);
        sent.Add($"0x33 {Hex(payload)}");
        return link.SendAsync(packet, serial);
    }

    public static string Describe(Result r)
    {
        var s = new StringBuilder();
        s.AppendLine("Write test · vibration · Bluetooth");
        s.AppendLine(new string('─', 62));
        foreach (var step in r.Steps) s.AppendLine($"  {(step.Ok ? "✓" : "✗")} {step.Name,-16} {step.Detail}");
        foreach (var line in r.Sent) s.AppendLine("  sent            " + line);
        s.AppendLine(r.Passed ? "  Passed: changed, read back, put back and confirmed." : r.Restored ? "  Not passed, and the controller is back as it was." : "  Not passed, and putting it back is not confirmed. See the last step.");
        return s.ToString();
    }

    private static string Hex(byte[] b) => Convert.ToHexString(b).ToLowerInvariant();
}
