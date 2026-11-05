using System.IO.Compression;
using System.Text.Json;
using X20Ctl.Product.Scanner;

namespace X20Ctl.Zone.Tests;

/// <summary>The Write scan's logic against a fake pad: no Bluetooth is used. What the real pad does is not tested here.</summary>
public class PadScanTests
{
    // ---------- a fake pad that keeps records and answers in 15-byte chunks ----------
    sealed class FakePad
    {
        public readonly Dictionary<string, byte[]> Records = new();
        public readonly List<byte> Sent = [];
        public bool Silent, NoAck, Corrupt, IgnoreWrites;
        public byte[] Identity = [7, 0x10, 0x13, 0x20, 0x09, 0x01, 0x23, 0x02];
        public byte[] Menu = [10, 9, 0, 0, 0, 0x0F, 1, 0, 0, 0, 0]; // macros 0x0F = M1..M4, change-key on
        static string Key(byte op, byte[] p) => op is 0xB5 or 0x35 ? $"m{p[^1]}" : $"r{(op & 0x0F)}";
        readonly Dictionary<string, List<byte>> incoming = new();

        public Task<byte[]> Exchange(byte op, byte[] payload, byte serial, byte length)
        {
            Sent.Add(op);
            if (Silent) throw new TimeoutException();
            switch (op)
            {
                case 0x91: return Task.FromResult(Identity);
                case 0xB0: return Task.FromResult(Menu);
                case 0x93: return Task.FromResult(new byte[] { 18, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14 });
                case 0xB6 or 0xB1 or 0xB2 or 0xB5:
                {
                    var rec = Records[Key(op, op == 0xB5 ? [payload[0], payload[1]] : payload)];
                    int index = payload[0];
                    return Task.FromResult(rec.Skip(index * 15).Take(15).ToArray());
                }
                case 0x36 or 0x31 or 0x32 or 0x35:
                {
                    if (NoAck) throw new TimeoutException();
                    string k = op == 0x35 ? $"m{length >> 5}" : $"r{(op & 0x0F)}";
                    if (!incoming.TryGetValue(k, out var buf) || ((serial >> 3) & 0x0F) == 0) incoming[k] = buf = [];
                    buf.AddRange(payload);
                    if (!IgnoreWrites && buf.Count >= buf[0] + 1 || op == 0x35 && buf.Count >= (buf[0] == 0 ? 1 : 3 + 5 * buf[0]) && !IgnoreWrites)
                        Records[k] = Corrupt ? [.. buf.Select(b => (byte)(b ^ 1))] : [.. buf];
                    return Task.FromResult(new byte[] { 2, 0xdf, 0xaa });
                }
            }
            throw new InvalidOperationException($"unexpected op {op:X2}");
        }
        public static FakePad X20()
        {
            var p = new FakePad();
            p.Records["r6"] = [16, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]; // remap, 17 bytes
            p.Records["r1"] = [14, 1, 2, 3, 4, 5, 6, 7, 1, 2, 3, 4, 5, 6, 7];
            p.Records["r2"] = [14, 2, 2, 3, 4, 5, 6, 7, 2, 2, 3, 4, 5, 6, 7];
            for (int i = 0; i < 4; i++) p.Records[$"m{i}"] = [0];
            p.Records["m1"] = [1, 0, 0, 1, 2, 3, 4, 5];
            return p;
        }
    }
    static (PadExchange, Func<byte>) Link(FakePad p) { byte n = 0; return (p.Exchange, () => ++n); }

    // ---------- stages ----------
    [Fact]
    public void Identify_and_read_back_can_never_write()
    {
        foreach (byte op in new byte[] { 0x31, 0x32, 0x35, 0x36, 0x33, 0x16 })
        {
            Assert.False(PadScan.Allows(ScanStage.Identify, op));
            Assert.False(PadScan.Allows(ScanStage.ReadBack, op));
        }
        foreach (byte op in new byte[] { 0x91, 0x93, 0xB0 }) Assert.True(PadScan.Allows(ScanStage.Identify, op));
        foreach (byte op in new byte[] { 0xB1, 0xB2, 0xB5, 0xB6 }) { Assert.False(PadScan.Allows(ScanStage.Identify, op)); Assert.True(PadScan.Allows(ScanStage.ReadBack, op)); }
    }

    [Fact]
    public void Write_stage_allows_only_the_four_record_writes_and_never_reset_or_motors()
    {
        foreach (byte op in new byte[] { 0x31, 0x32, 0x35, 0x36 }) Assert.True(PadScan.Allows(ScanStage.WriteBack, op));
        foreach (byte op in new byte[] { 0x16, 0x33, 0x1D, 0x00, 0xFF }) Assert.False(PadScan.Allows(ScanStage.WriteBack, op));
    }

    // ---------- the four answers ----------
    [Fact] public async Task A_recorded_X20_answers_like_an_X20() { var (x, n) = Link(FakePad.X20()); var v = PadScan.Classify(await PadScan.Identify(x, n)); Assert.Equal(PadAnswer.LikeX20, v.Answer); Assert.True(v.CanReadBack); Assert.Contains("X20", v.Headline); }

    [Fact]
    public async Task A_recorded_X20_Pro_answers_like_an_X20_Pro()
    {
        var p = FakePad.X20(); p.Identity = [7, 0x10, 0x20, 0x20, 0x06, 0x01, 0x44, 0x06]; p.Menu = [10, 9, 0, 0, 0, 0x3F, 1, 0, 0, 0, 0];
        var (x, n) = Link(p); Assert.Equal(PadAnswer.LikeX20Pro, PadScan.Classify(await PadScan.Identify(x, n)).Answer);
    }

    [Fact]
    public async Task An_unrecorded_pad_is_matched_by_the_shape_of_its_reply_and_says_so()
    {
        var p = FakePad.X20(); p.Identity = [7, 0xAB, 0xCD, 0xEF, 0x01, 0x01, 0x10, 0x02];
        var (x, n) = Link(p); var v = PadScan.Classify(await PadScan.Identify(x, n));
        Assert.Equal(PadAnswer.LikeX20, v.Answer); Assert.Contains(v.Reasons, r => r.Contains("not proof"));
    }

    [Fact]
    public async Task A_pad_with_another_shape_answers_differently_and_reads_nothing_further()
    {
        var p = FakePad.X20(); p.Identity = [7, 0xAB, 0xCD, 0xEF, 0x01, 0x01, 0x10, 0x02]; p.Menu = [10, 9, 0, 0, 0, 0, 0, 0, 0, 0, 0];
        var (x, n) = Link(p); var v = PadScan.Classify(await PadScan.Identify(x, n));
        Assert.Equal(PadAnswer.Different, v.Answer); Assert.False(v.CanReadBack);
    }

    [Fact]
    public async Task A_silent_pad_doesnt_answer()
    {
        var p = FakePad.X20(); p.Silent = true; var (x, n) = Link(p);
        var v = PadScan.Classify(await PadScan.Identify(x, n));
        Assert.Equal(PadAnswer.Silent, v.Answer); Assert.False(v.CanReadBack); Assert.Contains("doesn't answer", v.Headline);
    }

    [Fact]
    public async Task Identify_sends_only_read_opcodes()
    {
        var p = FakePad.X20(); var (x, n) = Link(p); await PadScan.Identify(x, n);
        Assert.All(p.Sent, op => Assert.True(PadScan.Allows(ScanStage.Identify, op)));
    }

    // ---------- read-back ----------
    [Fact]
    public async Task Read_back_reads_remap_curves_and_macros_and_writes_nothing()
    {
        var p = FakePad.X20(); var (x, n) = Link(p);
        var records = await PadScan.ReadAll(x, n, 4);
        Assert.Equal(["remap", "sticks", "triggers", "macro M1", "macro M2", "macro M3", "macro M4"], records.Select(r => r.Name));
        Assert.Equal(p.Records["r6"], records[0].Bytes);
        Assert.Equal(p.Records["m1"], records[4].Bytes);
        Assert.All(p.Sent, op => Assert.True(PadScan.Allows(ScanStage.ReadBack, op)));
    }

    // ---------- write-back ----------
    [Fact]
    public async Task Write_back_of_the_same_bytes_is_acknowledged_identical_and_changes_nothing()
    {
        var p = FakePad.X20(); var (x, n) = Link(p);
        var before = await PadScan.ReadAll(x, n, 4); var snapshot = p.Records.ToDictionary(k => k.Key, k => k.Value.ToArray());
        var result = await PadScan.WriteBack(x, n, before);
        Assert.True(result.AllAcknowledged); Assert.True(result.AllIdentical); Assert.False(result.Stopped);
        foreach (var (k, v) in snapshot) Assert.Equal(v, p.Records[k]);
        Assert.All(p.Sent, op => Assert.True(PadScan.Allows(ScanStage.WriteBack, op)));
    }

    [Fact]
    public async Task A_pad_that_never_acknowledges_is_reported_as_not_accepting_the_command()
    {
        var p = FakePad.X20(); var (x, n) = Link(p); var before = await PadScan.ReadAll(x, n, 4);
        p.NoAck = true; var result = await PadScan.WriteBack(x, n, before);
        Assert.False(result.AllAcknowledged); Assert.True(result.AllIdentical); // unchanged, but no ack
    }

    [Fact]
    public async Task A_record_that_reads_back_different_stops_the_test_and_is_put_back()
    {
        var p = FakePad.X20(); var (x, n) = Link(p); var before = await PadScan.ReadAll(x, n, 4);
        p.Corrupt = true; // the first write lands corrupted
        int writesAtStop = 0; var counting = new PadExchange((op, pl, s, l) => { if (op is 0x31 or 0x32 or 0x35 or 0x36) { writesAtStop++; if (writesAtStop == 3) p.Corrupt = false; /* the restore lands clean */ } return p.Exchange(op, pl, s, l); });
        var result = await PadScan.WriteBack(counting, n, before);
        Assert.True(result.Stopped); Assert.Single(result.Items); Assert.False(result.Items[0].Identical); Assert.True(result.Items[0].Restored);
        Assert.Equal(before[0].Bytes, p.Records["r6"]);
    }

    [Fact]
    public async Task An_unverifiable_restore_is_reported_not_hidden()
    {
        var p = FakePad.X20(); var (x, n) = Link(p); var before = await PadScan.ReadAll(x, n, 4);
        p.Corrupt = true; var result = await PadScan.WriteBack(x, n, before);
        Assert.True(result.Stopped); Assert.False(result.Items[0].Restored); Assert.Contains("could NOT be verified", result.Items[0].Detail);
    }

    [Fact]
    public async Task Persistence_compares_a_fresh_read_with_the_first()
    {
        var p = FakePad.X20(); var (x, n) = Link(p); var a = await PadScan.ReadAll(x, n, 4); var b = await PadScan.ReadAll(x, n, 4);
        Assert.True(PadScan.Persists(a, b));
        p.Records["r1"] = [14, 9, 9, 9, 9, 9, 9, 9, 1, 2, 3, 4, 5, 6, 7]; var c = await PadScan.ReadAll(x, n, 4);
        Assert.False(PadScan.Persists(a, c));
    }

    // ---------- the report ----------
    [Fact]
    public async Task The_zip_has_a_manifest_with_hashes_and_no_address_or_machine_name()
    {
        var p = FakePad.X20(); var (x, n) = Link(p); var s = new PadScanSession { ChosenModel = "X20" };
        s.Raw = await PadScan.Identify(x, n); s.Verdict = PadScan.Classify(s.Raw); s.Records.AddRange(await PadScan.ReadAll(x, n, 4));
        s.Visual.Add(new("Triggers", "pass", ""));
        var zip = PadScanReport.Zip(s, "9.9.9");
        using var archive = new ZipArchive(new MemoryStream(zip));
        var names = archive.Entries.Select(e => e.FullName).ToList();
        Assert.Contains("manifest.json", names); Assert.Contains("experiments/write-scan/findings.json", names); Assert.Contains("experiments/write-scan/records.txt", names);
        using var m = JsonDocument.Parse(new StreamReader(archive.GetEntry("manifest.json")!.Open()).ReadToEnd());
        Assert.Equal("write-scan", m.RootElement.GetProperty("kind").GetString()); Assert.Equal(2, m.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(names.Count - 1, m.RootElement.GetProperty("files").GetArrayLength());
        string all = string.Concat(archive.Entries.Select(e => new StreamReader(e.Open()).ReadToEnd()));
        Assert.DoesNotContain(Environment.MachineName, all, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Triggers", all);
    }

    [Fact]
    public void A_report_without_a_scan_still_says_what_did_not_run()
    {
        var text = PadScanReport.Summary(new PadScanSession());
        Assert.Contains("not run", text);
        Assert.DoesNotContain("KeyLinker log", text);
    }
}
