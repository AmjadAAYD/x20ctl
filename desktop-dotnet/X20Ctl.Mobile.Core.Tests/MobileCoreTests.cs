using X20Ctl.Mobile.Core;
using X20Ctl.Zone.Capture;
namespace X20Ctl.Mobile.Core.Tests;

/// <summary>The phone app's Bluetooth limits, advertisement labels, gamepad mapping and scans.</summary>
public sealed class MobileCoreTests
{
    static readonly Guid Battery = GattPolicy.Standard(0x2A19), Model = GattPolicy.Standard(0x2A24), Serial = GattPolicy.Standard(0x2A25);
    static readonly Guid KeyLinkerWrite = Guid.Parse("d7f010e1-660d-46e9-96c3-19c4148bdab5"), KeyLinkerRead = Guid.Parse("d7f010e2-660d-46e9-96c3-19c4148bdab5");

    [Fact]
    public void NothingCanBeWritten()
    {
        Assert.False(GattPolicy.WritesAllowed);
        foreach (var (service, characteristic) in new[] { (GattPolicy.KeyLinkerService, KeyLinkerWrite), (GattPolicy.BatteryService, Battery), (GattPolicy.OtaService, Guid.Parse("2de516f1-50f5-45d5-a1b2-c3565de543ae")) })
            Assert.False(GattPolicy.CanWrite(service, characteristic));
    }

    [Fact]
    public void OnlyStandardValuesAreRead()
    {
        Assert.True(GattPolicy.CanRead(GattPolicy.BatteryService, Battery));
        Assert.True(GattPolicy.CanRead(GattPolicy.DeviceInformationService, Model));
        Assert.False(GattPolicy.CanRead(GattPolicy.DeviceInformationService, Serial)); // identifying
        Assert.False(GattPolicy.CanRead(GattPolicy.DeviceInformationService, Battery)); // right characteristic, wrong service
        Assert.False(GattPolicy.CanRead(GattPolicy.KeyLinkerService, KeyLinkerRead));
        Assert.False(GattPolicy.CanRead(GattPolicy.OtaService, Battery));
        Assert.False(GattPolicy.CanRead(Guid.Parse("2de516f4-50f5-45d5-a1b2-c3565de543ae"), Model));
    }

    [Fact]
    public void AdvertisementsAreSeenNeverSupported()
    {
        Assert.Equal("KeyLinker-family prefix: standard gamepad", DeviceLabel.PrefixHint("98:b6:e9:01:02:03"));
        Assert.Null(DeviceLabel.PrefixHint("00:11:22:33:44:55"));
        var status = DeviceLabel.Status("98:B6:E9:01:02:03", [GattPolicy.KeyLinkerService]);
        Assert.StartsWith("Seen", status);
        Assert.DoesNotContain("support", status, StringComparison.OrdinalIgnoreCase);
        Assert.True(DeviceLabel.LooksRelevant("Gamepad", "00:11:22:33:44:55", [GattPolicy.KeyLinkerService]));
        Assert.False(DeviceLabel.LooksRelevant("Headphones", "00:11:22:33:44:55", []));
    }

    [Fact]
    public void AndroidKeysMapToXInputButtons()
    {
        var pad = new PadSession();
        Assert.True(pad.Key(96, true, 0));
        Assert.Equal(PadSession.A, pad.State);
        Assert.True(pad.Key(96, false, 80));
        Assert.Equal(0, pad.State);
        Assert.Equal(1, pad.Presses[PadSession.A].Presses);
        Assert.Equal(80, pad.Presses[PadSession.A].LastMs);
        Assert.False(pad.Key(4, true, 100)); // Back key is not a gamepad button
    }

    [Fact]
    public void HatAndKeysCombineWithoutReleasingEachOther()
    {
        var pad = new PadSession();
        pad.Key(96, true, 0);
        pad.Motion(0, 0, 0, 0, 0, 0, 0, -1, 10);
        Assert.Equal(PadSession.A | PadSession.Up, pad.State);
        pad.Motion(0, 0, 0, 0, 0, 0, 0, 0, 20);
        Assert.Equal(PadSession.A, pad.State);
    }

    [Fact]
    public void AndroidAxesBecomeXInputRanges()
    {
        var pad = new PadSession();
        pad.Motion(1, -1, -1, 1, 1, 0, 0, 0, 0); // Android Y points down
        Assert.Equal(32767, pad.LeftStats.X);
        Assert.Equal(32767, pad.LeftStats.Y);
        Assert.Equal(-32767, pad.RightStats.X);
        Assert.Equal(-32767, pad.RightStats.Y);
        Assert.Equal(255, pad.LeftTrigger.Value);
        Assert.Equal(0, pad.RightTrigger.Value);
        Assert.Contains("Left stick", pad.Summary(0));
    }

    [Fact]
    public void ReadScanShowsValuesAndSaysNothingWasWritten()
    {
        Assert.Equal("87%", ReadScan.Show(Battery, [87]));
        Assert.Equal("X20", ReadScan.Show(Model, "X20\0"u8.ToArray()));
        var scan = new ReadScan { Name = "Gamepad", Address = "98:B6:E9:00:00:01" };
        scan.Services.Add(GattPolicy.BatteryService); scan.Services.Add(GattPolicy.KeyLinkerService);
        scan.Entries.Add(new(GattPolicy.BatteryService, Battery, "read", [55]));
        var text = scan.Text();
        Assert.Contains("Nothing was written", text);
        Assert.Contains("55%", text);
        Assert.Contains("does not prove the model", text);
        Assert.True(scan.HasKeyLinkerService);
    }

    [Fact]
    public void WriteScanReadsAPhoneLog()
    {
        // one ATT write command (opcode 0x52, handle 0x0010) on connection 0x40 that isn't a KeyLinker frame
        byte[] att = [0x52, 0x10, 0x00, 0x01], l2cap = [(byte)att.Length, 0, 4, 0, .. att], acl = [0x40, 0x00, (byte)l2cap.Length, 0, .. l2cap];
        var (report, log, extension) = Load(BtSnoopWriter.Write([new HciRecord(0, true, acl)]));
        Assert.False(report.IsKeyLinker);
        Assert.Equal(1, report.AclPackets);
        Assert.Equal("btsnoop", extension);
        Assert.NotEmpty(log);
    }

    [Fact]
    public void WriteScanRejectsALogWithoutPackets() =>
        Assert.Throws<InvalidDataException>(() => Load(BtSnoopWriter.Write([])));

    static (WriteScanReport Report, byte[] Log, string Extension) Load(byte[] bytes)
    {
        var path = Path.Combine(Path.GetTempPath(), "x20ctl-mobile-" + Guid.NewGuid().ToString("N") + ".log");
        try { File.WriteAllBytes(path, bytes); return PhoneWriteScan.Load(new TestPlan("X20", "KeyLinker", "Vibration", "50", "80"), path); }
        finally { File.Delete(path); }
    }
}
