using X20Ctl.Product;
using Xunit;

namespace X20Ctl.Zone.Tests;

/// <summary>
/// The read-only controller check. X20 replies are the recorded ones from tests/test_protocol.py (identity, HOST_MENU kind 1,
/// HOST_GUID); the X20 Pro HOST_GUID continuation is the one in test_host_guid_identifies_x20_pro. The X20 Pro capability
/// record below is synthetic: no recorded one is in the repository, so it only checks the M5/M6 bits decode.
/// </summary>
public class PadCheckTests
{
    static readonly byte[] X20Identity = Convert.FromHexString("0710132009012352");
    static readonly byte[] X20Menu = Convert.FromHexString("0a030303000f010002c000");
    static readonly byte[] GuidFirst = Convert.FromHexString("12a1ca51da724cb6103658c05d7d4e");
    static readonly byte[] X20GuidRest = Convert.FromHexString("20917b00");
    static readonly byte[] ProGuidRest = Convert.FromHexString("23959000");

    [Fact]
    public void Only_three_read_only_questions_are_allowed()
    {
        Assert.Equal(new byte[] { 0x91, 0x93, 0xB0 }, PadCheck.QueryOpcodes);
        foreach (byte write in new byte[] { 0x1D, 0x1E, 0x16 }) Assert.False(PadCheck.IsQuery(write)); // set mode, normal mode, recover
    }

    [Fact]
    public void Check_queries_are_framed_exactly_like_the_python_client()
    {
        Assert.Equal(new byte[] { 90, 201, 128, 199, 58, 107 }, X20Codec.Frame(0x93, 6, 2, 0, [0]));
        Assert.Equal(new byte[] { 91, 202, 131, 199, 36, 224 }, X20Codec.Frame(0x93, 6, 3, 0, [1]));
        Assert.Equal(new byte[] { 123, 202, 130, 199, 36, 41, 163 }, X20Codec.Frame(0xB0, 7, 4, 0, [0, 1]));
    }

    [Fact]
    public void Recorded_x20_replies_match_the_recorded_x20()
    {
        var report = PadCheck.Build(new(X20Identity, X20Menu, GuidFirst, X20GuidRest));
        Assert.Equal(new PadCheck.Identity("1013", "2009", "1.23", 2), report.Identity);
        Assert.Equal("EasySMX X20", report.Matches);
        Assert.Equal(["M1", "M2", "M3", "M4"], report.Caps!.MacroSlots);
        Assert.Equal(2, report.Caps.MotorCount);
        Assert.Equal(0, report.Caps.Lighting);
        Assert.Equal("EasySMX X20", report.Guid!.Model);
        Assert.Equal("1.23", report.Guid.Version);
        Assert.Empty(report.Unanswered);
        Assert.Contains(report.Findings, f => f.StartsWith("Macro slots match"));
        Assert.DoesNotContain(report.Findings, f => f.StartsWith("Firmware reads"));
    }

    [Fact]
    public void Pro_guid_and_its_six_slots_decode()
    {
        byte[] syntheticProMenu = Convert.FromHexString("0a030303003f010002c000"); // synthetic: X20 record with M5/M6 bits set
        // synthetic identity: vid 1020, pid 2006, firmware 1.44, flags 0x56 (mode 6, the proven Pro mode)
        var report = PadCheck.Build(new(Convert.FromHexString("0710202006014456"), syntheticProMenu, GuidFirst, ProGuidRest));
        Assert.Equal(6, report.Identity!.Mode);
        Assert.Equal("EasySMX X20 Pro", report.Matches);
        Assert.Equal("EasySMX X20 Pro", report.Guid!.Model);
        Assert.Equal("1.44", report.Guid.Version);
        Assert.Equal(["M1", "M2", "M3", "M4", "M5", "M6"], report.Caps!.MacroSlots);
        Assert.Contains(report.Findings, f => f.StartsWith("The updater id names the same model"));
    }

    [Fact]
    public void A_pad_that_disagrees_is_reported_not_assumed()
    {
        var report = PadCheck.Build(new(X20Identity, Convert.FromHexString("0a0303030003010002c000"), GuidFirst, ProGuidRest));
        Assert.Contains(report.Findings, f => f.StartsWith("Macro slots differ"));
        Assert.Contains(report.Findings, f => f.StartsWith("The updater id says EasySMX X20 Pro"));
        Assert.Contains(report.Findings, f => f.StartsWith("Firmware reads 1.23") && f.Contains("1.44"));
    }

    [Fact]
    public void Unknown_pads_and_silence_stay_unknown()
    {
        var report = PadCheck.Build(new(Convert.FromHexString("0712342345010052"), null, GuidFirst, null));
        Assert.Null(report.Matches);
        Assert.Null(report.Caps);
        Assert.Null(report.Guid); // half a HOST_GUID is refused, as in parse_host_guid
        Assert.Equal(["updater id (0x93)", "capability menu (0xB0)"], report.Unanswered);
        Assert.Contains(report.Findings, f => f.StartsWith("Not a pad with a recorded identity"));
        Assert.Contains("Not answered", PadCheck.Describe(report));
    }

    [Fact]
    public void Foreign_hardware_id_prefix_names_no_model()
    {
        var foreign = new byte[] { 18 }.Concat(new byte[14]).ToArray();
        Assert.Null(PadCheck.ParseGuid(foreign, X20GuidRest)!.Model);
        Assert.Null(PadCheck.ParseCapabilities(Convert.FromHexString("0a0303")));
    }
}
