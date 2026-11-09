using X20Ctl.Zone.X10;
namespace X20Ctl.Zone.Tests;

/// <summary>The X10's KRLY protocol against the real fixtures in Edgard's EasyGamepad docs/protocol.md.</summary>
public class KrlyTests
{
    static byte[] H(string hex) => Convert.FromHexString(hex.Replace(" ", ""));

    [Fact]
    public void Battery_reads_offset_five()
    {
        var b = Krly.ParseBattery(H("08 31 01 00 01 3C 00 00"))!;
        Assert.Equal((60, false, false), (b.Percent, b.Charging, b.Low));
        Assert.Null(Krly.ParseBattery(H("08 31 01 00 01 65 00 00"))); // 101%
        Assert.Null(Krly.ParseBattery(H("08 32 01 00 01 3C 00 00")));
    }

    [Fact]
    public void Version_reads_the_real_reply()
    {
        var v = Krly.ParseVersion(H("12 84 01 00 56 02 87 03 00 40 02 40 71 9A 01 00 A0 02"))!;
        Assert.Equal(("560287", "030040", "0240719A", "0100A0"), (v.Project, v.Protocol, v.Firmware, v.Hardware));
    }

    [Fact]
    public void Settings_read_the_real_two_frames()
    {
        var pages = new Krly.SettingsPages();
        Assert.Null(pages.Accept(H("14 77 02 02 01 00 04 03 61 03 61 09 05 64 00 00 05 64 00 01")));
        var s = pages.Accept(H("0B 77 02 02 02 00 00 02 64 64 01"))!;
        Assert.Equal(new Krly.Range(3, 97), s.LeftTrigger);
        Assert.Equal(new Krly.Range(3, 97), s.RightTrigger);
        Assert.Equal(new Krly.Stick(5, 100, false, false), s.LeftStick);
        Assert.Equal((100, 100), (s.LeftMotor, s.RightMotor));
        Assert.Empty(s.Turbo);
    }

    [Fact]
    public void Settings_pages_out_of_order_fail()
    {
        var pages = new Krly.SettingsPages();
        Assert.Throws<FormatException>(() => pages.Accept(H("0B 77 02 03 03 00 00 02 64 64 01"))); // starts at page 3
    }

    [Fact]
    public void Mappings_read_paged_and_empty()
    {
        var pages = new Krly.MappingPages();
        Assert.Null(pages.Accept(H("08 50 02 01 06 00 01 09")));
        var map = pages.Accept(H("09 50 02 02 A0 00 A1 A2 0A"))!;
        Assert.Equal("A", map["M1"]); Assert.Equal("X", map["B"]); Assert.Equal(2, map.Count);
        Assert.Empty(new Krly.MappingPages().Accept(H("06 50 01 01 00 00"))!);
    }

    [Fact]
    public void Frame_reader_splits_and_joins_notifications()
    {
        var reader = new Krly.FrameReader();
        Assert.Empty(reader.Feed(H("08 31 01")));
        var frames = reader.Feed(H("00 01 3C 00 00 04 3D 00 01"));
        Assert.Equal(2, frames.Count);
        Assert.Equal(60, Krly.ParseBattery(frames[0])!.Percent);
        Assert.True(Krly.IsAck(frames[1], Krly.Mapping));
    }

    [Fact]
    public void Writes_encode_like_the_documented_examples()
    {
        Assert.Equal(H("05 3D 01 A0 00"), Krly.Frame(Krly.Mapping, Krly.MappingPayload("M1", "A"), 0));
        Assert.Equal(H("0B 3E 05 5A 01 00 03 5F 00 01 00"), Krly.Frame(Krly.Sticks, Krly.SticksPayload(new(5, 90, true, false), new(3, 95, false, true)), 0));
        Assert.Equal(H("0B 3A 02 00 00 01 2C 03 E8 A2 00"), Krly.Frame(Krly.MacroStep, Krly.MacroStepPayload("M2", true, 0, 300, 1000, "X"), 0));
        Assert.Equal(H("03 31 00"), Krly.Frame(Krly.QueryBattery, [], 0));
    }

    [Fact]
    public void Apply_plan_follows_easygamepad_order_and_validates()
    {
        var s = new Krly.Settings(new(3, 97), new(3, 97), new(5, 100, false, false), new(5, 100, false, false), 80, 60, []);
        var plan = Krly.ApplyPlan(s, new Dictionary<string, string> { ["M1"] = "A" });
        Assert.Equal(new byte[] { 0x01, 0x36, 0x39, 0x3E, 0x3D, 0x35, 0x34, 0x01 }, plan.Select(p => p.Command));
        Assert.Throws<FormatException>(() => Krly.ApplyPlan(s with { LeftTrigger = new(50, 40) }, new Dictionary<string, string>()));
        Assert.Throws<ArgumentException>(() => Krly.MappingPayload("M9", "A"));
    }
}
