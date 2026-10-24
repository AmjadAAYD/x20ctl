using X20Ctl.Product;
using Xunit;

namespace X20Ctl.Zone.Tests;

public class PadBridgeTelemetryTests
{
    [Fact]
    public void ParsesFullStatusOutputWithChargingBattery()
    {
        string raw = """
            Controller
            1020 / 2006
            ────────────────────────────────────────
              controller      EasySMX X20 Pro
              reports as      EasySMX X20 Pro
              firmware        1.44
              address         E4:17:D8:12:34:56
              x20ctl          0.4.0
              battery         4/4 · charging
              evidence        known controller
              playing over    2.4 GHz receiver · EasySMX X20 Pro receiver
            """;

        var data = PadBridge.ParseTelemetry(raw);

        Assert.Equal("EasySMX X20 Pro", data.Controller);
        Assert.Equal("1.44", data.Firmware);
        Assert.Equal("4/4 (Charging)", data.Battery);
        Assert.True(data.Charging);
        Assert.Equal("E4:17:D8:12:34:56", data.Address);
    }

    [Fact]
    public void ParsesDischargingBattery()
    {
        string raw = """
              controller      EasySMX X20
              firmware        1.41
              battery         3/4 · on battery
            """;

        var data = PadBridge.ParseTelemetry(raw);

        Assert.Equal("1.41", data.Firmware);
        Assert.Equal("3/4", data.Battery);
        Assert.False(data.Charging);
    }

    [Fact]
    public void GracefullyHandlesEmptyOrUnrecognizedOutput()
    {
        var data = PadBridge.ParseTelemetry("No controller found");

        Assert.Null(data.Controller);
        Assert.Null(data.Firmware);
        Assert.Null(data.Battery);
        Assert.False(data.Charging);
        Assert.Null(data.Address);
    }
}
