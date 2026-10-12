using System.Buffers.Binary;
using System.Formats.Tar;
using System.IO.Compression;
using X20Ctl.Zone.Capture;
namespace X20Ctl.Zone.Tests;

/// <summary>
/// What a Write scan may and may not conclude: a write is a request until a read shows it stored, a phone's other
/// devices are not the controller, an archive's biggest log is not necessarily the test, and a shared trace holds the
/// controller's connection and nothing else.
/// </summary>
public sealed class WriteScanTrustTests
{
    static byte[] Hex(string h) => Convert.FromHexString(h);
    static byte[] Acl(int connection, byte[] att)
    {
        var l2 = new byte[4 + att.Length]; BinaryPrimitives.WriteUInt16LittleEndian(l2, (ushort)att.Length); l2[2] = 4; att.CopyTo(l2, 4);
        return [(byte)connection, (byte)(connection >> 8), (byte)l2.Length, (byte)(l2.Length >> 8), .. l2];
    }
    const int Pad = 0x40, Earbuds = 0x41;
    static (bool, byte[]) Write(byte[] f, int c = Pad) => (true, Acl(c, [0x12, 0x10, 0x00, .. f]));
    static (bool, byte[]) Notify(byte[] f, int c = Pad) => (false, Acl(c, [0x1B, 0x12, 0x00, .. f]));
    static byte[] Q(byte op, byte serial, params byte[] p) => KeyLinkerCodec.Build(op, p, serial, (byte)(serial * 7 + 3));
    static byte[] R(byte serial, string p) => KeyLinkerCodec.Build(0x19, Hex(p), serial, (byte)(serial * 5 + 1));
    static byte[] MotorWrite(byte serial, string left) => KeyLinkerCodec.Build(0x33, Hex("08" + left + "32323232ffffff"), serial, 9);

    static List<(bool, byte[])> Opening() =>
    [
        Write(Q(0x91, 1)), Notify(R(1, "0710132009012352")),
        Write(Q(0xB0, 2, 0, 1)), Notify(R(2, "0a030303000f010002c000")),
        Write(Q(0xB3, 5, 0)), Notify(R(5, "083232323232ffffff")),
    ];

    static KeyLinkerAnalysis Analyse(IEnumerable<(bool, byte[])> packets) => KeyLinkerAnalysis.From(AttTrace.From(PacketLogger.Parse(Pklg(packets))));

    /// <summary>A PacketLogger file with real-looking Unix times, starting at <paramref name="start"/>.</summary>
    static byte[] Pklg(IEnumerable<(bool FromHost, byte[] Acl)> packets, uint start = 1_760_000_000, uint stepMs = 200)
    {
        var ms = new MemoryStream(); var w = new byte[4]; long us = 0;
        void U32(uint v) { BinaryPrimitives.WriteUInt32LittleEndian(w, v); ms.Write(w); }
        foreach (var (fromHost, acl) in packets)
        {
            us += stepMs * 1000;
            U32((uint)(9 + acl.Length)); U32(start + (uint)(us / 1_000_000)); U32((uint)(us % 1_000_000)); ms.WriteByte(fromHost ? (byte)2 : (byte)3); ms.Write(acl);
        }
        return ms.ToArray();
    }

    [Fact]
    public void A_write_is_not_taken_for_what_the_controller_holds()
    {
        // two writes, no read between them: the second is compared with the last READ, not with the first write
        var kl = Analyse([.. Opening(), Write(MotorWrite(6, "50")), Write(Q(0x91, 7)), Write(MotorWrite(8, "60"))]);
        Assert.Equal(2, kl.Changes.Count);
        Assert.Equal(new ByteChange(0, 0x32, 0x60), Assert.Single(kl.Changes[1].Bytes));
        Assert.All(kl.Changes, c => Assert.Equal("not read back", c.ReadBack));
    }

    [Fact]
    public void A_reply_and_a_matching_read_back_are_what_count_as_taken()
    {
        var kl = Analyse([.. Opening(), Write(MotorWrite(6, "50")), Notify(R(6, "00")), Write(Q(0xB3, 7, 0)), Notify(R(7, "085032323232ffffff"))]);
        var change = Assert.Single(kl.Changes);
        Assert.True(change.Answered);
        Assert.Equal("matches", change.ReadBack);
    }

    [Fact]
    public void A_read_back_that_differs_says_the_write_may_not_have_stuck()
    {
        var kl = Analyse([.. Opening(), Write(MotorWrite(6, "50")), Write(Q(0xB3, 7, 0)), Notify(R(7, "083232323232ffffff"))]);
        var change = Assert.Single(kl.Changes);
        Assert.False(change.Answered);
        Assert.Equal("differs", change.ReadBack);
        var report = WriteScanReport.Build(new("X20", "KeyLinker", "Left motor", "50", "80"), [], PacketLogger.Parse(Pklg([.. Opening(), Write(MotorWrite(6, "50")), Write(Q(0xB3, 7, 0)), Notify(R(7, "083232323232ffffff"))])), "test");
        Assert.Contains("may not have kept it", report.Summary());
    }

    [Fact]
    public void A_write_in_several_packets_stays_one_write_when_each_is_answered()
    {
        // a macro write goes out as packets with a reply after each, as the client sends it
        var kl = Analyse([.. Opening(),
            Write(KeyLinkerCodec.Build(0x35, Hex("0c0102030405"), 0x21, 9)), Notify(R(0x21, "00")),
            Write(KeyLinkerCodec.Build(0x35, Hex("060708090a0b0c"), 0x22, 9)), Notify(R(0x22, "00"))]);
        var change = Assert.Single(kl.Changes);
        Assert.True(change.Answered);
        Assert.Equal("0c0102030405060708090a0b0c", change.After);
    }

    [Fact]
    public void A_read_back_that_does_not_line_up_is_unclear_not_different()
    {
        var kl = Analyse([.. Opening(), Write(MotorWrite(6, "50")), Write(Q(0xB3, 7, 0)), Notify(R(7, "0432323232"))]);
        Assert.Equal("unclear", Assert.Single(kl.Changes).ReadBack);
    }

    static List<(bool, byte[])> WithEarbuds() =>
    [
        Write(Hex("0102030405"), Earbuds), Write(Hex("0102030405"), Earbuds), Write(Hex("0102030405"), Earbuds),
        .. Opening(),
        // the earbuds' connection carries a frame whose serial collides with a pending read: it must not answer it
        Write(Q(0xB3, 9, 0)), Notify(R(9, "ffffffffff"), Earbuds), Notify(R(9, "083232323232ffffff")),
        Write(MotorWrite(6, "50")),
        Write(Hex("0a0b0c"), Earbuds),
    ];

    [Fact]
    public void The_phones_other_devices_are_left_out()
    {
        var records = PacketLogger.Parse(Pklg(WithEarbuds()));
        var report = WriteScanReport.Build(new("X20", "KeyLinker", "Left motor", "50", "80"), [], records, "test");
        Assert.Equal(Pad, report.KeyLinker.Connection);
        Assert.Equal(1, report.KeyLinker.OtherConnections);
        Assert.True(report.ConnectionByFrames);
        Assert.NotNull(report.KeyLinker.Identity);
        Assert.Equal(new ByteChange(0, 0x32, 0x50), Assert.Single(Assert.Single(report.KeyLinker.Changes).Bytes));
        Assert.All(report.Trace.Messages, m => Assert.Equal(Pad, m.Connection));
        Assert.DoesNotContain("0102030405", report.MessagesCsv());
        Assert.Contains("1 other Bluetooth connection", report.Summary());
    }

    [Fact]
    public void The_shared_trace_holds_the_controllers_connection_around_the_test_only()
    {
        // a minute of earlier traffic on the pad's own connection, then the test
        var early = Enumerable.Range(0, 300).Select(_ => Write(Hex("0102"), Pad)).ToList();
        var records = PacketLogger.Parse(Pklg([.. early, .. WithEarbuds()]));
        var report = WriteScanReport.Build(new("X20", "", "", "", ""), [], records, "test");
        var shared = report.SharedRecords();
        Assert.All(shared, r => Assert.Equal(Pad, r.Connection));
        Assert.InRange(shared.Count, 9, 30);   // the test, and up to two seconds either side of it
        Assert.Equal(shared.Count, BtSnoop.Parse(report.SharedTrace()).Count);
        var kl = KeyLinkerAnalysis.From(AttTrace.From(shared));
        Assert.NotNull(kl.Identity);
        Assert.Single(kl.Changes);
    }

    [Fact]
    public void The_log_with_the_controller_in_it_is_chosen_not_the_biggest()
    {
        var path = Path.Combine(Path.GetTempPath(), "x20ctl-sysdiagnose-" + Guid.NewGuid().ToString("N") + ".tar.gz");
        try
        {
            var big = Pklg(Enumerable.Range(0, 400).Select(_ => Write(Hex("0102030405"), Earbuds)));
            using (var file = File.Create(path))
            using (var gz = new GZipStream(file, CompressionLevel.Fastest))
            using (var tar = new TarWriter(gz))
            {
                tar.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, "sysdiagnose/logs/Bluetooth/bluetoothd-hci-older.pklg") { DataStream = new MemoryStream(big), ModificationTime = DateTimeOffset.UtcNow });
                tar.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, "sysdiagnose/logs/Bluetooth/bluetoothd-hci-latest.pklg") { DataStream = new MemoryStream(Pklg(Opening())), ModificationTime = DateTimeOffset.UtcNow.AddDays(-1) });
            }
            var (records, source, _, _) = BtSnoop.LoadAny(path);
            Assert.Contains("bluetoothd-hci-latest.pklg", source);
            Assert.Contains("one of 2 Bluetooth logs", source);
            Assert.NotNull(KeyLinkerAnalysis.From(AttTrace.From(records)).Identity);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void A_log_from_another_time_is_flagged()
    {
        var at = DateTimeOffset.FromUnixTimeSeconds(1_760_000_000).UtcDateTime;
        var records = PacketLogger.Parse(Pklg(Opening()));
        var inside = WriteScanReport.Build(new("X20", "", "", "", ""), [new("make the change", at.AddSeconds(1))], records, "test");
        Assert.Null(inside.ClockWarning);
        var later = WriteScanReport.Build(new("X20", "", "", "", ""), [new("make the change", at.AddHours(3))], records, "test");
        Assert.NotNull(later.ClockWarning);
        Assert.Contains("older log", later.Summary());
    }
}
