using X20Ctl.Product;
using Xunit;

namespace X20Ctl.Zone.Tests;

/// <summary>
/// The Part B write test against a fake controller. The motor record 4c4c0000c0d40100 is the recorded X20 one (30%/30% plus
/// the idle timeout); the expected packets were produced by the Python client with nonce 0 (X20.set_vibration, read_motor).
/// </summary>
public class VibrationTestTests
{
    static readonly PadCheck.Identity X20 = new("1013", "2009", "1.23", 2);
    static readonly PadCheck.Capabilities Caps = PadCheck.ParseCapabilities(Convert.FromHexString("0a030303000f010002c000"))!;
    static readonly byte[] Record = Convert.FromHexString("4c4c0000c0d40100");

    sealed class FakePad(byte[] record) : IPadLink
    {
        public byte[] Record = (byte[])record.Clone();
        public readonly List<byte[]> Packets = [];
        public bool IgnoreWrites, SilentWrites, DropRestore;
        int writes;

        public Task<byte[]?> SendAsync(byte[] packet, byte serial)
        {
            Packets.Add(packet);
            var (opcode, _, s, _, payload, crc) = X20Codec.Decode(packet);
            Assert.True(crc);
            Assert.Equal(serial, s);
            Assert.Contains(opcode, VibrationTest.SessionOpcodes);
            if (opcode == VibrationTest.ReadMotor) return Task.FromResult<byte[]?>([(byte)Record.Length, .. Record]);
            writes++;
            if (DropRestore && writes == 2) return Task.FromResult<byte[]?>(null);
            if (!IgnoreWrites) Record = payload[1..];
            return Task.FromResult<byte[]?>(SilentWrites ? null : []);
        }
    }

    static VibrationTest.Session Zero() => new() { Nonce = () => 0 };

    [Fact]
    public void Counters_and_levels_follow_the_python_client()
    {
        var s = Zero();
        Assert.Equal(new byte[] { 1, 130, 3, 132, 5 }, Enumerable.Range(0, 5).Select(_ => s.NextSerial()).ToArray());
        Assert.Equal(204, VibrationTest.ToLevel(80));
        Assert.Equal(129, VibrationTest.SaveSerial(0, 1));
        Assert.Equal(238, VibrationTest.HostLength(9, 7));
    }

    [Fact]
    public async Task A_good_run_changes_reads_back_and_puts_back_byte_for_byte()
    {
        var pad = new FakePad(Record);
        var r = await VibrationTest.RunAsync(pad, X20, Caps, 80, Zero());
        Assert.True(r.Passed);
        Assert.True(r.Restored);
        Assert.Equal(Record, pad.Record);
        Assert.Equal("4c4c0000c0d40100", r.SavedHex);
        Assert.Equal(5, pad.Packets.Count);
        Assert.Equal(new byte[] { 117, 200, 129, 199, 59, 65 }, pad.Packets[0]);
        Assert.Equal(new byte[] { 117, 160, 1, 199, 179, 91, 222, 135, 127, 251, 67, 245, 61, 247 }, pad.Packets[1]);
        Assert.Equal(new byte[] { 250, 1, 128, 199, 50, 90, 223, 6, 224, 122, 252, 122, 178, 50 }, pad.Packets[3]);
        Assert.Contains("Passed", VibrationTest.Describe(r));
    }

    [Theory]
    [InlineData("1013", "2009", "1.24", 2)] // newer firmware
    [InlineData("1013", "2009", "1.23", 6)] // another mode
    [InlineData("1020", "2006", "1.44", null)] // mode not decoded
    [InlineData("1234", "2345", "1.00", 2)]
    public async Task Anything_not_exactly_proven_is_refused_before_a_packet(string vid, string pid, string fw, int? mode)
    {
        var pad = new FakePad(Record);
        var r = await VibrationTest.RunAsync(pad, new(vid, pid, fw, mode), Caps, 80, Zero());
        Assert.False(r.Passed);
        Assert.Empty(pad.Packets);
        Assert.False(VibrationTest.MayRun(new(vid, pid, fw, mode), Caps));
    }

    [Fact]
    public async Task No_motors_or_a_bad_value_is_refused()
    {
        var noMotors = Caps with { Motors = 0 };
        var pad = new FakePad(Record);
        Assert.False((await VibrationTest.RunAsync(pad, X20, noMotors, 80, Zero())).Passed);
        Assert.False((await VibrationTest.RunAsync(pad, X20, Caps, 101, Zero())).Passed);
        Assert.False((await VibrationTest.RunAsync(pad, X20, Caps, 30, Zero())).Passed); // already at 30%
        Assert.Single(pad.Packets); // only the save read of the last run
        Assert.True(VibrationTest.MayRun(new("1020", "2006", "1.44", 6), Caps));
    }

    [Fact]
    public async Task A_write_that_does_not_land_fails_and_still_restores()
    {
        var pad = new FakePad(Record) { IgnoreWrites = true };
        var r = await VibrationTest.RunAsync(pad, X20, Caps, 80, Zero());
        Assert.False(r.Passed);
        Assert.True(r.Restored);
        Assert.Contains(r.Steps, s => s.Name == "Read back" && !s.Ok);
        Assert.Equal(5, pad.Packets.Count); // the restore is sent anyway
    }

    [Fact]
    public async Task Silence_after_a_write_is_unknown_and_the_restore_still_runs()
    {
        var pad = new FakePad(Record) { SilentWrites = true };
        var r = await VibrationTest.RunAsync(pad, X20, Caps, 80, Zero());
        Assert.False(r.Passed);
        Assert.True(r.Restored);
        Assert.Equal(Record, pad.Record);
    }

    [Fact]
    public async Task A_lost_restore_says_so_and_keeps_the_saved_record()
    {
        var pad = new FakePad(Record) { DropRestore = true };
        var r = await VibrationTest.RunAsync(pad, X20, Caps, 80, Zero());
        Assert.False(r.Passed);
        Assert.False(r.Restored);
        Assert.Contains(r.Steps, s => s.Name == "Read back again" && s.Detail.Contains("4c4c0000c0d40100") && s.Detail.Contains("30%"));
        Assert.Contains("not confirmed", VibrationTest.Describe(r));
    }
}
