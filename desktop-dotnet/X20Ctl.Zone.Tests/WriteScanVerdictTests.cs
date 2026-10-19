using X20Ctl.Zone.Capture;
namespace X20Ctl.Zone.Tests;

/// <summary>The Write scan's own check of a recording, and its verdict: a Filtered log, the phone's clock against the
/// test's steps, and the change-and-change-back that says which bytes are the setting.</summary>
public sealed class WriteScanVerdictTests
{
    static byte[] Hex(string h) => Convert.FromHexString(h);

    // ---- an Android log with real times: btsnoop stamps are microseconds since the year 0 ----
    const long UnixEpoch = 0x00dcddb30f2f8000;
    static readonly DateTime T = new(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);
    static long Stamp(double secondsAfterT, double shift = 0) =>
        UnixEpoch + (long)Math.Round((new DateTimeOffset(T).ToUnixTimeMilliseconds() / 1000.0 + secondsAfterT + shift) * 1e6);

    static void Be32(Stream s, uint v) { s.WriteByte((byte)(v >> 24)); s.WriteByte((byte)(v >> 16)); s.WriteByte((byte)(v >> 8)); s.WriteByte((byte)v); }
    /// <summary>A log whose packets carry the given stamps; <paramref name="keep"/> above zero saves only that many bytes
    /// of each packet, as the Filtered log does.</summary>
    static byte[] Log(IEnumerable<(double At, bool FromHost, byte[] Acl)> packets, double shift = 0, int keep = 0)
    {
        var ms = new MemoryStream();
        ms.Write([(byte)'b', (byte)'t', (byte)'s', (byte)'n', (byte)'o', (byte)'o', (byte)'p', 0]);
        Be32(ms, 1); Be32(ms, 1002);
        foreach (var (at, fromHost, acl) in packets)
        {
            var body = new byte[] { 2 }.Concat(acl).ToArray();
            var saved = keep > 0 && body.Length > keep ? body[..keep] : body;
            Be32(ms, (uint)body.Length); Be32(ms, (uint)saved.Length); Be32(ms, fromHost ? 0u : 1u); Be32(ms, 0);
            long stamp = Stamp(at, shift);
            Be32(ms, (uint)(stamp >> 32)); Be32(ms, (uint)stamp);
            ms.Write(saved);
        }
        return ms.ToArray();
    }
    static byte[] Acl(byte[] data) => [0x40, 0x00, (byte)data.Length, (byte)(data.Length >> 8), .. data];
    static byte[] L2cap(byte[] att) => [(byte)att.Length, (byte)(att.Length >> 8), 4, 0, .. att];
    static byte[] Att(byte opcode, int handle, byte[] value) => [opcode, (byte)handle, (byte)(handle >> 8), .. value];
    static (double, bool, byte[]) Write(double at, byte[] frame) => (at, true, Acl(L2cap(Att(0x12, 0x0010, frame))));
    static (double, bool, byte[]) Notify(double at, byte[] frame) => (at, false, Acl(L2cap(Att(0x1B, 0x0012, frame))));
    static byte[] Query(byte opcode, byte serial, params byte[] payload) => KeyLinkerCodec.Build(opcode, payload, serial, (byte)(serial * 7 + 3));
    static byte[] Reply(byte serial, params byte[] payload) => KeyLinkerCodec.Build(0x19, payload, serial, (byte)(serial * 5 + 1));
    static byte[] Motors(byte left, byte serial) => KeyLinkerCodec.Build(0x33, Hex("08" + $"{left:x2}" + "32323232" + "ffffff"), serial, (byte)(serial + 2));

    /// <summary>The X20 as recorded: identity and descriptor, a read of the motors (left = 50), then the change to 80 on
    /// "Make the change" and back to 50 on "Change it back", each answered.</summary>
    static List<(double, bool, byte[])> Session(bool changeBack = true, bool alsoLighting = false)
    {
        var p = new List<(double, bool, byte[])>
        {
            Write(5, Query(0x91, 1)), Notify(5.1, Reply(1, Hex("0710132009012352"))),
            Write(6, Query(0xB0, 2, 0, 1)), Notify(6.1, Reply(2, Hex("0a030303000f010002c000"))),
            Write(22, Query(0xB3, 3, 0)), Notify(22.1, Reply(3, Hex("08" + "3232323232" + "ffffff"))),
            Write(27, Motors(0x50, 4)), Notify(27.1, Reply(4)),
        };
        if (alsoLighting) { p.Add(Write(29, Query(0xB7, 5, 0))); p.Add(Notify(29.1, Reply(5, Hex("0400000000")))); p.Add(Write(29.5, KeyLinkerCodec.Build(0x37, Hex("0401020304"), 6, 8))); }
        if (changeBack) { p.Add(Write(37, Motors(0x32, 7))); p.Add(Notify(37.1, Reply(7))); }
        return p;
    }

    /// <summary>The test as this PC saw it: a step confirmed every ten seconds.</summary>
    static List<StepNote> Steps(double offset = 0) =>
        new[] { "samsung ready", "open the app", "go to the page", "make the change", "change it back", "close the app" }
            .Select((e, i) => new StepNote(e, T.AddSeconds(i * 10 + offset))).ToList();

    static readonly TestPlan Plan = new("X20", "KeyLinker", "Left motor strength", "50", "80");
    static WriteScanReport Build(byte[] log, List<StepNote>? steps = null) => WriteScanReport.Build(Plan, steps ?? Steps(), BtSnoop.Parse(log), "Android snoop log");

    [Fact]
    public void An_Android_log_carries_the_phone_clock()
    {
        var records = BtSnoop.Parse(Log(Session()));
        Assert.Equal(new DateTimeOffset(T).ToUnixTimeSeconds() + 5, records[0].Unix, 3);
        Assert.All(records, r => Assert.False(r.Cut));
    }

    [Fact]
    public void A_change_and_its_change_back_on_the_right_steps_is_high_confidence()
    {
        var report = Build(Log(Session()));
        Assert.Equal(0, report.ClockShift);
        var changes = report.KeyLinker.Changes;
        Assert.Equal("make the change", report.StepAt(changes[0].Seconds));
        Assert.Equal("change it back", report.StepAt(changes[1].Seconds));
        var verdict = report.Verdict();
        Assert.Equal("High", verdict.Level);
        Assert.Equal("Vibration and shutdown timer, byte 0: 50 → 80", verdict.Headline);
        Assert.Contains(verdict.Checks, c => c.Passed && c.Text.Contains("put it back exactly as it was"));
        Assert.Contains(verdict.Checks, c => c.Passed && c.Text.Contains("the very values you typed"));
        Assert.Contains(verdict.Checks, c => c.Passed && c.Text.Contains("Make the change and Change it back"));
        Assert.Empty(verdict.Fixes);
        Assert.Contains("High confidence. Vibration and shutdown timer, byte 0: 50 → 80.", report.Summary());
        Assert.Contains("(on Make the change)", report.Summary());
    }

    [Fact]
    public void A_phone_that_logs_local_time_is_lined_up_by_this_PC_time_zone()
    {
        double local = TimeZoneInfo.Local.GetUtcOffset(T).TotalSeconds;
        var report = Build(Log(Session(), shift: local));
        Assert.Null(report.ClockWarning);
        Assert.Equal(local, report.ClockShift);
        Assert.Equal("make the change", report.StepAt(report.KeyLinker.Changes[0].Seconds));
        Assert.Equal("High", report.Verdict().Level);
    }

    [Fact]
    public void A_log_from_another_day_is_named_and_the_fix_given()
    {
        var report = Build(Log(Session(), shift: 3 * 86400));
        Assert.NotNull(report.ClockWarning);
        Assert.True(double.IsNaN(report.ClockShift));
        Assert.Null(report.StepAt(report.KeyLinker.Changes[0].Seconds));
        var verdict = report.Verdict();
        Assert.NotEqual("High", verdict.Level);
        Assert.Contains(verdict.Fixes, f => f.Contains("new bug report straight after"));
    }

    [Fact]
    public void A_filtered_log_says_so_and_how_to_fix_it()
    {
        var report = Build(Log(Session(), keep: 14));
        Assert.True(report.CutPackets > 0);
        Assert.True(report.Filtered);
        Assert.False(report.IsKeyLinker);
        var verdict = report.Verdict();
        Assert.Equal("None", verdict.Level);
        Assert.Contains("Filtered", verdict.Headline);
        Assert.Contains(verdict.Fixes, f => f.Contains("Enabled (or Full), not Filtered"));
        Assert.Contains("Filtered", report.Summary());
    }

    [Fact]
    public void A_change_without_its_change_back_is_not_high()
    {
        var verdict = Build(Log(Session(changeBack: false))).Verdict();
        Assert.Equal("Medium", verdict.Level);
        Assert.Contains(verdict.Checks, c => !c.Passed && c.Text.Contains("no change back"));
        Assert.Contains(verdict.Fixes, f => f.Contains("Change it back"));
    }

    [Fact]
    public void Another_setting_changed_in_the_same_test_lowers_the_verdict()
    {
        var verdict = Build(Log(Session(alsoLighting: true))).Verdict();
        Assert.Equal("Medium", verdict.Level);
        Assert.Contains(verdict.Checks, c => !c.Passed && c.Text.Contains("Other settings changed too: Lighting"));
        Assert.Contains(verdict.Fixes, f => f.Contains("only one setting"));
    }

    [Fact]
    public void Writes_that_land_on_the_wrong_steps_are_pointed_out()
    {
        // the PC's steps run a minute later than the phone's writes: the writes come before the test began
        var report = Build(Log(Session()), Steps(offset: 60));
        Assert.Null(report.ClockWarning);
        Assert.Equal("before the test", report.StepAt(report.KeyLinker.Changes[0].Seconds));
        var verdict = report.Verdict();
        Assert.Contains(verdict.Checks, c => !c.Passed && c.Text.Contains("both writes came before the test, not on the steps where you made the change"));
        Assert.Equal("High", verdict.Level);   // the round trip and the typed values still say which byte it is
    }

    [Fact]
    public void A_log_without_a_clock_still_gets_a_verdict()
    {
        var report = WriteScanReport.Build(Plan, Steps(), BtSnoop.Parse(Log(Session())).Select(r => r with { Unix = double.NaN }).ToList(), "Android snoop log");
        Assert.True(double.IsNaN(report.ClockShift));
        Assert.Equal("High", report.Verdict().Level);   // round trip and typed values
    }

    [Fact]
    public void The_findings_carry_the_verdict_and_the_steps()
    {
        string json = Build(Log(Session())).FindingsJson();
        Assert.Contains("\"verdict\"", json);
        Assert.Contains("\"changeSteps\"", json);
        Assert.Contains("make the change", json);
        string clockless = WriteScanReport.Build(Plan, [], BtSnoop.Parse(Log(Session())), "x").FindingsJson();
        Assert.Contains("\"clockShift\": null", clockless);
    }

    [Theory]
    [InlineData("btsnoop_hci.log", true)]
    [InlineData("btsnoop_hci.log.last", true)]
    [InlineData("btsnoop_hci.log.filtered", true)]
    [InlineData("btsnoop_hci.cfa", true)]
    [InlineData("btsnoop.log", true)]
    [InlineData("btsnooz_hci.log", false)]
    [InlineData("bugreport.txt", false)]
    public void Every_Bluetooth_log_a_bug_report_carries_is_a_candidate(string name, bool candidate) => Assert.Equal(candidate, BtSnoop.IsLogName(name));

    [Fact]
    public void A_research_report_fits_under_the_report_site_request_limit()
    {
        // Vercel functions refuse request bodies over 4.5 MB; the form around the ZIP needs a little room too
        Assert.True(WriteScanReport.MaxResearchZipBytes <= 4_400_000);
    }
}
