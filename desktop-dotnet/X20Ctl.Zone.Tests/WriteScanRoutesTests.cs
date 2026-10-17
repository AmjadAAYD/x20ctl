using System.Buffers.Binary;
using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using X20Ctl.Zone.Capture;
namespace X20Ctl.Zone.Tests;

/// <summary>The Write scan beyond Android: iPhone logs and sysdiagnose archives, the shared trace, and the research report.</summary>
public sealed class WriteScanRoutesTests
{
    static byte[] Hex(string h) => Convert.FromHexString(h);
    static byte[] Acl(byte[] att) { var l2 = new byte[4 + att.Length]; BinaryPrimitives.WriteUInt16LittleEndian(l2, (ushort)att.Length); l2[2] = 4; att.CopyTo(l2, 4); return [0x40, 0x00, (byte)l2.Length, (byte)(l2.Length >> 8), .. l2]; }
    static (bool, byte[]) Write(byte[] f) => (true, Acl([0x12, 0x10, 0x00, .. f]));
    static (bool, byte[]) Notify(byte[] f) => (false, Acl([0x1B, 0x12, 0x00, .. f]));
    static byte[] Q(byte op, byte serial, params byte[] p) => KeyLinkerCodec.Build(op, p, serial, (byte)(serial * 7 + 3));
    static byte[] R(byte serial, string p) => KeyLinkerCodec.Build(0x19, Hex(p), serial, (byte)(serial * 5 + 1));
    static List<(bool, byte[])> Session() =>
    [
        Write(Q(0x91, 1)), Notify(R(1, "0710132009012352")),
        Write(Q(0xB0, 2, 0, 1)), Notify(R(2, "0a030303000f010002c000")),
        Write(Q(0xB3, 5, 0)), Notify(R(5, "083232323232ffffff")),
        Write(KeyLinkerCodec.Build(0x33, Hex("085032323232ffffff"), 6, 9)),
    ];

    /// <summary>A PacketLogger file: length, seconds, microseconds, type, packet. Big- or little-endian.</summary>
    static byte[] Pklg(IEnumerable<(bool FromHost, byte[] Acl)> packets, bool big)
    {
        var ms = new MemoryStream(); var w = new byte[4]; uint t = 1_760_000_000;
        void U32(uint v) { if (big) BinaryPrimitives.WriteUInt32BigEndian(w, v); else BinaryPrimitives.WriteUInt32LittleEndian(w, v); ms.Write(w); }
        // an HCI event first, as real logs open with: it must be skipped
        U32(9 + 3); U32(t); U32(0); ms.WriteByte(1); ms.Write([0x0E, 0x01, 0x00]);
        uint us = 0;
        foreach (var (fromHost, acl) in packets) { us += 200_000; U32((uint)(9 + acl.Length)); U32(t + us / 1_000_000); U32(us % 1_000_000); ms.WriteByte(fromHost ? (byte)2 : (byte)3); ms.Write(acl); }
        return ms.ToArray();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void An_iPhone_log_reads_like_an_Android_one(bool bigEndian)
    {
        var records = PacketLogger.Parse(Pklg(Session(), bigEndian));
        Assert.Equal(7, records.Count);
        Assert.True(records[0].FromHost); Assert.False(records[1].FromHost);
        Assert.Equal(0.2, records[0].Seconds, 3);
        var kl = KeyLinkerAnalysis.From(AttTrace.From(records));
        Assert.Equal("1013", kl.Identity!.Vid);
        var change = Assert.Single(kl.Changes);
        Assert.Equal(new ByteChange(0, 0x32, 0x50), Assert.Single(change.Bytes));
    }

    [Fact]
    public void A_sysdiagnose_gives_up_its_Bluetooth_log_and_only_that()
    {
        var path = Path.Combine(Path.GetTempPath(), "x20ctl-sysdiagnose-" + Guid.NewGuid().ToString("N") + ".tar.gz");
        try
        {
            using (var file = File.Create(path))
            using (var gz = new GZipStream(file, CompressionLevel.Fastest))
            using (var tar = new TarWriter(gz))
            {
                tar.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, "sysdiagnose/ps.txt") { DataStream = new MemoryStream([1, 2, 3]) });
                tar.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, "sysdiagnose/logs/Bluetooth/bluetoothd-hci-latest.pklg") { DataStream = new MemoryStream(Pklg(Session(), false)) });
            }
            var (records, source, log, extension) = BtSnoop.LoadAny(path);
            Assert.Contains("bluetoothd-hci-latest.pklg", source);
            Assert.Equal("pklg", extension);
            Assert.Equal(7, records.Count);
            Assert.True(PacketLogger.LooksLikeLog(log));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void A_sysdiagnose_without_a_log_says_what_to_do()
    {
        var path = Path.Combine(Path.GetTempPath(), "x20ctl-sysdiagnose-" + Guid.NewGuid().ToString("N") + ".tar.gz");
        try
        {
            using (var file = File.Create(path))
            using (var gz = new GZipStream(file, CompressionLevel.Fastest))
            using (var tar = new TarWriter(gz))
                tar.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, "sysdiagnose/ps.txt") { DataStream = new MemoryStream([1]) });
            var error = Assert.Throws<InvalidDataException>(() => BtSnoop.LoadAny(path));
            Assert.Contains("logging profile", error.Message);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Something_that_is_no_recording_is_named_as_such()
    {
        var path = Path.GetTempFileName();
        try { File.WriteAllBytes(path, new byte[64]); Assert.Contains("isn't a Bluetooth recording", Assert.Throws<InvalidDataException>(() => BtSnoop.LoadAny(path)).Message); }
        finally { File.Delete(path); }
    }

    [Fact]
    public void The_shared_trace_keeps_every_data_packet_and_nothing_else()
    {
        var records = PacketLogger.Parse(Pklg(Session(), true));
        var again = BtSnoop.Parse(BtSnoopWriter.Write(records));
        Assert.Equal(records.Count, again.Count);
        for (int i = 0; i < records.Count; i++)
        {
            Assert.Equal(records[i].FromHost, again[i].FromHost);
            Assert.Equal(records[i].Data, again[i].Data);
            // the clock starts at the first data packet now, not at the event before it; the gaps between packets are kept
            Assert.Equal(records[i].Seconds - records[0].Seconds, again[i].Seconds, 5);
        }
    }

    static WriteScanReport Report() => WriteScanReport.Build(new("X20", "KeyLinker", "Left motor", "50", "80"), [new("make the change", DateTime.UtcNow)], PacketLogger.Parse(Pklg(Session(), true)), "iPhone Bluetooth log");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void The_research_report_lists_every_file_with_its_checksum(bool trace)
    {
        var zip = Report().ResearchZip(trace, "test");
        using var archive = new ZipArchive(new MemoryStream(zip));
        var manifest = JsonDocument.Parse(archive.GetEntry("manifest.json")!.Open()).RootElement;
        Assert.Equal(2, manifest.GetProperty("schemaVersion").GetInt32());
        Assert.True(manifest.GetProperty("consent").GetBoolean()); Assert.True(manifest.GetProperty("reviewed").GetBoolean());
        Assert.Equal(trace, manifest.GetProperty("rawTrace").GetBoolean());
        var listed = manifest.GetProperty("files").EnumerateArray().ToList();
        Assert.Equal(archive.Entries.Count - 1, listed.Count);
        foreach (var f in listed)
        {
            using var s = archive.GetEntry(f.GetProperty("path").GetString()!)!.Open(); var data = new MemoryStream(); s.CopyTo(data);
            Assert.Equal(f.GetProperty("size").GetInt32(), (int)data.Length);
            Assert.Equal(f.GetProperty("sha256").GetString(), Convert.ToHexString(SHA256.HashData(data.ToArray())).ToLowerInvariant());
        }
        Assert.Equal(trace, archive.GetEntry("ble/recording.btsnoop") != null);
        // every name is one the receiver allows for schema 2, and no .jsonl (which it reads as input records)
        Assert.All(archive.Entries, e => Assert.Matches(@"^(manifest|device|system|session)\.json$|^SUMMARY\.txt$|^(ble|experiments)/.+\.(json|txt|btsnoop)$", e.FullName));
        // for checking against the receiver itself: X20CTL_EXPORT_RESEARCH_ZIP=folder
        if (Environment.GetEnvironmentVariable("X20CTL_EXPORT_RESEARCH_ZIP") is { Length: > 0 } dir) File.WriteAllBytes(Path.Combine(dir, $"write-scan-{(trace ? "trace" : "plain")}.zip"), zip);
    }

    [Fact]
    public void A_read_scan_travels_inside_the_write_report_as_plain_files_the_receiver_allows()
    {
        // the Read scan's compact report, unpacked under input/read-scan/ and listed in the same manifest
        var read = new List<(string Path, byte[] Data)>
        {
            ("input/read-scan/device.json", System.Text.Encoding.UTF8.GetBytes("{\"controllerName\":\"EasySMX X20\"}")),
            ("input/read-scan/input-mapping.json", System.Text.Encoding.UTF8.GetBytes("[]")),
            ("input/read-scan/scanner-version.txt", System.Text.Encoding.UTF8.GetBytes("Controller Scanner")),
        };
        var zip = Report().ResearchZip(false, "test", "Read scan included", read);
        using var archive = new ZipArchive(new MemoryStream(zip));
        var manifest = JsonDocument.Parse(archive.GetEntry("manifest.json")!.Open()).RootElement;
        Assert.True(manifest.GetProperty("withReadScan").GetBoolean());
        var paths = manifest.GetProperty("files").EnumerateArray().Select(f => f.GetProperty("path").GetString()!).ToList();
        foreach (var (path, _) in read) Assert.Contains(path, paths);
        Assert.Equal(archive.Entries.Count - 1, paths.Count);
        // the receiver's schema-2 name rule: input/... with json or txt
        Assert.All(read, f => Assert.Matches(@"^input/.+\.(json|txt)$", f.Path));
        using var summary = new StreamReader(archive.GetEntry("SUMMARY.txt")!.Open());
        Assert.Contains("Read scan included", summary.ReadToEnd());
    }

    [Fact]
    public void A_raw_copy_keeps_its_own_kind()
    {
        var root = Path.Combine(Path.GetTempPath(), "x20ctl-writescan-" + Guid.NewGuid().ToString("N"));
        try
        {
            var folder = Report().Save(root, Pklg(Session(), true), null, "pklg");
            Assert.True(File.Exists(Path.Combine(folder, "recording.pklg")));
            Assert.Contains("recording.pklg", File.ReadAllText(Path.Combine(folder, "README.txt")));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
