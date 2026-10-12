using System.IO.Compression;
using X20Ctl.Zone.Capture;
namespace X20Ctl.Zone.Tests;

/// <summary>The Write scan's reading of a recording: frames, identity, descriptor, limits and what the app changed.</summary>
public sealed class WriteScanTests
{
    static byte[] Hex(string h) => Convert.FromHexString(h);

    // ---- a Bluetooth log built by hand: btsnoop header, then ACL packets carrying ATT ----
    static void Be32(Stream s, uint v) { s.WriteByte((byte)(v >> 24)); s.WriteByte((byte)(v >> 16)); s.WriteByte((byte)(v >> 8)); s.WriteByte((byte)v); }
    static byte[] Log(IEnumerable<(bool FromHost, byte[] Acl)> packets, uint link = 1002)
    {
        var ms = new MemoryStream();
        ms.Write([(byte)'b', (byte)'t', (byte)'s', (byte)'n', (byte)'o', (byte)'o', (byte)'p', 0]);
        Be32(ms, 1); Be32(ms, link);
        long stamp = 0;
        foreach (var (fromHost, acl) in packets)
        {
            var body = link == 1002 ? new byte[] { 2 }.Concat(acl).ToArray() : acl;
            Be32(ms, (uint)body.Length); Be32(ms, (uint)body.Length); Be32(ms, fromHost ? 0u : 1u); Be32(ms, 0);
            stamp += 100_000;
            Be32(ms, (uint)(stamp >> 32)); Be32(ms, (uint)stamp);
            ms.Write(body);
        }
        return ms.ToArray();
    }
    static byte[] Acl(int connection, int boundary, byte[] data)
        => [(byte)connection, (byte)((connection >> 8) | (boundary << 4)), (byte)data.Length, (byte)(data.Length >> 8), .. data];
    static byte[] L2cap(byte[] att) => [(byte)att.Length, (byte)(att.Length >> 8), 4, 0, .. att];
    static byte[] Att(byte opcode, int handle, byte[] value) => [opcode, (byte)handle, (byte)(handle >> 8), .. value];
    const int Tx = 0x0010, Rx = 0x0012;
    static (bool, byte[]) Write(byte[] frame) => (true, Acl(0x40, 0, L2cap(Att(0x12, Tx, frame))));
    static (bool, byte[]) Notify(byte[] frame) => (false, Acl(0x40, 0, L2cap(Att(0x1B, Rx, frame))));

    static byte[] Query(byte opcode, byte serial, params byte[] payload) => KeyLinkerCodec.Build(opcode, payload, serial, (byte)(serial * 7 + 3));
    static byte[] Reply(byte serial, params byte[] payload) => KeyLinkerCodec.Build(0x19, payload, serial, (byte)(serial * 5 + 1));

    // ---- the X20, as recorded: identity, descriptor, limits, then a vibration change ----
    static List<(bool, byte[])> X20Session(byte newLeft = 0x50) =>
    [
        Write(Query(0x91, 1)), Notify(Reply(1, Hex("0710132009012352"))),
        Write(Query(0xB0, 2, 0, 1)), Notify(Reply(2, Hex("0a030303000f010002c000"))),
        Write(Query(0xB0, 3, 0, 6)), Notify(Reply(3, Hex("10" + "2a002a002a002a00000000000000"))),   // 16 bytes: a 14-byte head, then the rest
        Write(Query(0xB0, 4, 1, 6)), Notify(Reply(4, Hex("0000"))),
        Write(Query(0xB3, 5, 0)), Notify(Reply(5, Hex("08" + "3232323232" + "ffffff"))),   // [8, L1 L2 R1 R2, t0..t3]
        Write(KeyLinkerCodec.Build(0x33, Hex("08" + $"{newLeft:x2}" + "32323232" + "ffffff"), 6, 9)),
    ];

    [Fact]
    public void The_frame_codec_matches_the_Python_one_byte_for_byte()
    {
        Assert.Equal("5ef486bee6", Convert.ToHexString(KeyLinkerCodec.IdentityQuery(1, 7)).ToLowerInvariant());
        Assert.Equal("88c184b0028320", Convert.ToHexString(KeyLinkerCodec.MenuQuery(0, 1, 2, 9)).ToLowerInvariant());
        Assert.Equal("d2c082ba23388224ebb5092bff", Convert.ToHexString(KeyLinkerCodec.Build(0x19, Hex("0710132009012352"), 1, 3)).ToLowerInvariant());
        var frame = KeyLinkerCodec.TryDecode(Hex("d4e384bb2c299c01e4b92d7fb4114a31"))!;
        Assert.True(frame.CrcValid);
        Assert.Equal((byte)0x19, frame.Opcode);
        Assert.Equal("0a030303000f010002c000", Convert.ToHexString(frame.Payload).ToLowerInvariant());
        Assert.Equal(frame.Payload.Length + 5, frame.DeclaredLength);
    }

    [Fact]
    public void A_damaged_frame_is_not_a_frame()
    {
        var bytes = Hex("d2c082ba23388224ebb5092bff"); bytes[^1] ^= 1;
        Assert.False(KeyLinkerCodec.TryDecode(bytes)!.CrcValid);
        Assert.Null(KeyLinkerCodec.TryDecode(new byte[3]));
        Assert.Null(KeyLinkerCodec.TryDecode(new byte[21]));
    }

    [Fact]
    public void The_identity_the_descriptor_and_the_limits_are_read_from_the_recording()
    {
        var kl = KeyLinkerAnalysis.From(AttTrace.From(BtSnoop.Parse(Log(X20Session()))));
        var id = kl.Identity!;
        Assert.Equal(("1013", "2009", "1.23", 2), (id.Vid, id.Pid, id.Version, id.DeviceId));
        Assert.Equal((2, 1, 2, 2), (id.Mode, id.Sensor, id.RawGeneration, id.EffectiveGeneration));
        Assert.Equal("030303000f010002c000", KeyLinkerAnalysis.Hex(kl.Descriptor!));
        Assert.Equal(new[] { 42, 42, 42, 42, 0, 0, 0, 0 }, kl.ToFingerprint().Lists["macroLimits"]);
        Assert.Equal(["D6.bit0 (remapping)"], Descriptor.Unexplained(kl.Descriptor!).Where(x => x.StartsWith("D6")));
    }

    [Fact]
    public void The_one_byte_the_app_changed_is_found()
    {
        var kl = KeyLinkerAnalysis.From(AttTrace.From(BtSnoop.Parse(Log(X20Session()))));
        var change = Assert.Single(kl.Changes);
        Assert.Equal("Vibration and shutdown timer", change.Setting);
        var b = Assert.Single(change.Bytes);
        Assert.Equal((0, (byte)0x32, (byte)0x50), (b.Offset, b.Before, b.After));
    }

    [Fact]
    public void A_write_with_nothing_to_compare_against_says_so()
    {
        var only = new List<(bool, byte[])> { Write(KeyLinkerCodec.Build(0x33, Hex("083232323232ffffff"), 6, 9)) };
        var change = Assert.Single(KeyLinkerAnalysis.From(AttTrace.From(BtSnoop.Parse(Log(only)))).Changes);
        Assert.Contains("no read of this setting before the write", change.Note);
        Assert.False(change.Answered); Assert.Equal("not read back", change.ReadBack);
        Assert.Empty(change.Bytes);
    }

    [Fact]
    public void A_reply_that_answers_nothing_is_ignored()
    {
        var kl = KeyLinkerAnalysis.From(AttTrace.From(BtSnoop.Parse(Log([Notify(Reply(1, Hex("0710132009012352")))]))));
        Assert.Null(kl.Identity);
    }

    [Fact]
    public void Messages_in_another_protocol_are_counted_not_decoded()
    {
        var other = new List<(bool, byte[])> { (true, Acl(0x40, 0, L2cap(Att(0x12, 0x20, Hex("0102030405060708"))))) };
        var kl = KeyLinkerAnalysis.From(AttTrace.From(BtSnoop.Parse(Log(other))));
        Assert.Empty(kl.Frames); Assert.Equal(1, kl.Other);
        var report = WriteScanReport.Build(new("X05", "Some app", "Vibration", "1", "2"), [], BtSnoop.Parse(Log(other)), "snoop log");
        Assert.False(report.IsKeyLinker);
        Assert.Contains("inconclusive", report.Summary());
        Assert.DoesNotContain("speaks something else", report.Summary());
    }

    [Fact]
    public void A_message_split_over_two_packets_is_put_back_together()
    {
        var whole = L2cap(Att(0x1B, Rx, Reply(2, Hex("0a030303000f010002c000"))));
        var records = new List<(bool, byte[])>
        {
            Write(Query(0xB0, 2, 0, 1)),
            (false, Acl(0x40, 2, whole[..9])),
            (false, Acl(0x40, 1, whole[9..])),
        };
        var kl = KeyLinkerAnalysis.From(AttTrace.From(BtSnoop.Parse(Log(records))));
        Assert.Equal("030303000f010002c000", KeyLinkerAnalysis.Hex(kl.Descriptor!));
    }

    [Fact]
    public void Characteristic_declarations_in_the_recording_name_the_handles()
    {
        // a Read By Type request for 0x2803, then its response: one declaration for value handle 0x0010 with a 128-bit UUID
        var uuid = Convert.FromHexString("d7f010e1660d46e996c319c4148bdab5").Reverse().ToArray();
        byte[] declaration = [0x0F, 0x00, 0x0C, 0x10, 0x00, .. uuid];
        var records = new List<(bool, byte[])>
        {
            (true, Acl(0x40, 0, L2cap([0x08, 0x01, 0x00, 0xFF, 0xFF, 0x03, 0x28]))),
            (false, Acl(0x40, 0, L2cap([0x09, 21, .. declaration]))),
            Write(Query(0x91, 1)),
        };
        var trace = AttTrace.From(BtSnoop.Parse(Log(records)));
        Assert.Equal("d7f010e1-660d-46e9-96c3-19c4148bdab5", trace.Characteristics[0x0010]);
        Assert.Equal("d7f010e1-660d-46e9-96c3-19c4148bdab5", trace.Name(0x0010));
    }

    [Fact]
    public void What_the_recording_asked_for_matches_the_recorded_X20()
    {
        var kl = KeyLinkerAnalysis.From(AttTrace.From(BtSnoop.Parse(Log(X20Session()))));
        var differences = FingerprintCompare.Compare(Fingerprint.X20Reference, kl.ToFingerprint());
        // the three key lists weren't asked for in this session; everything that was asked matches
        Assert.All(differences, d => Assert.Equal("missing on one side", d.Meaning));
        Assert.Equal(new[] { "changekeySources", "macroKeys", "turboKeys" }, differences.Select(d => d.Field).Order());
    }

    [Fact]
    public void The_report_counts_lists_it_never_saw_as_not_recorded_not_as_differences()
    {
        var report = WriteScanReport.Build(new("X20", "KeyLinker", "Left motor", "50", "80"), [], BtSnoop.Parse(Log(X20Session())), "snoop log");
        Assert.Empty(report.Differences);
        Assert.Equal(new[] { "changekeySources", "macroKeys", "turboKeys" }, report.NotRecorded.Order());
        Assert.Contains("No differences in anything both report.", report.Summary());
        Assert.Contains("Not in this recording, so not compared:", report.Summary());
    }

    [Fact]
    public void A_different_pad_differs_where_it_should()
    {
        var other = Fingerprint.X20Reference with
        {
            Identity = IdentityInfo.Parse(Hex("10133001012807")),
            Descriptor = "030303000f030002c001",
        };
        var d = FingerprintCompare.Compare(Fingerprint.X20Reference, other);
        Assert.Equal("effective generation", d[0].Field);
        Assert.Contains(d, x => x.Field == "D6.bit1 (remapping)" && x.Area == "protocol");
        Assert.Contains(d, x => x.Field == "D10.bit0 (sensor)" && x.Area == "unexplained");
        var order = new[] { "protocol", "identity", "features", "unexplained" };
        Assert.Equal(d.Select(x => x.Area).OrderBy(a => Array.IndexOf(order, a)).ToList(), d.Select(x => x.Area).ToList());
    }

    [Fact]
    public void Generation_two_in_modes_zero_seven_and_eight_counts_as_one()
    {
        int? Effective(int mode, int raw) => IdentityInfo.Parse([0x10, 0x13, 0x20, 0x09, 0x01, 0x23, (byte)(raw << 5 | 0x10 | mode)])!.EffectiveGeneration;
        Assert.Equal(1, Effective(0, 2)); Assert.Equal(1, Effective(7, 2)); Assert.Equal(1, Effective(8, 2));
        Assert.Equal(2, Effective(2, 2)); Assert.Equal(3, Effective(3, 3)); Assert.Equal(1, Effective(7, 1));
        Assert.Null(IdentityInfo.Parse([0x10, 0x13, 0x20, 0x09, 0x01, 0x23])!.EffectiveGeneration);
    }

    [Fact]
    public void A_bug_report_gives_up_its_Bluetooth_log_and_only_that()
    {
        var path = Path.Combine(Path.GetTempPath(), "x20ctl-bugreport-" + Guid.NewGuid().ToString("N") + ".zip");
        try
        {
            using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
            {
                using (var note = zip.CreateEntry("dumpstate.txt").Open()) note.WriteByte(1);
                using (var log = zip.CreateEntry("FS/data/misc/bluetooth/logs/btsnoop_hci.log").Open()) log.Write(Log(X20Session()));
            }
            var records = BtSnoop.ReadFile(path, out var source);
            Assert.Contains("btsnoop_hci.log", source);
            Assert.NotEmpty(records);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Files_that_are_not_recordings_are_refused_with_a_reason()
    {
        Assert.Throws<InvalidDataException>(() => BtSnoop.Parse(new byte[40]));
        var truncated = Log(X20Session())[..^3];
        Assert.Throws<InvalidDataException>(() => BtSnoop.Parse(truncated));
        Assert.Throws<InvalidDataException>(() => BtSnoop.Parse(Log([])));
    }

    [Fact]
    public void The_report_is_saved_without_the_raw_recording_unless_asked()
    {
        var records = BtSnoop.Parse(Log(X20Session()));
        var report = WriteScanReport.Build(new("X20", "KeyLinker", "Vibration", "50", "80"), [new("app_connected", DateTime.UtcNow)], records, "snoop log");
        var root = Path.Combine(Path.GetTempPath(), "x20ctl-writescan-" + Guid.NewGuid().ToString("N"));
        try
        {
            var folder = report.Save(root);
            Assert.True(File.Exists(Path.Combine(folder, "summary.txt")));
            Assert.True(File.Exists(Path.Combine(folder, "findings.json")));
            Assert.False(File.Exists(Path.Combine(folder, "recording.btsnoop")));
            var summary = File.ReadAllText(Path.Combine(folder, "summary.txt"));
            Assert.Contains("1013", summary); Assert.Contains("byte 0: 32 → 50", summary);
            System.Threading.Thread.Sleep(1100);   // folder names carry the second
            var withRaw = report.Save(root, Log(X20Session()));
            Assert.True(File.Exists(Path.Combine(withRaw, "recording.btsnoop")));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void Two_recordings_show_the_messages_only_one_holds()
    {
        var a = KeyLinkerAnalysis.From(AttTrace.From(BtSnoop.Parse(Log(X20Session(0x32)))));
        var b = KeyLinkerAnalysis.From(AttTrace.From(BtSnoop.Parse(Log(X20Session(0x50)))));
        var (onlyA, onlyB) = WriteScanReport.CompareRecordings(a, b);
        Assert.Single(onlyA); Assert.Single(onlyB);
        Assert.Contains("0832323232", onlyA[0]);
        Assert.Contains("0850323232", onlyB[0]);
    }
}
