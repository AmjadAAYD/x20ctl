using X20Ctl.Zone.PcMode;

namespace X20Ctl.Zone.Tests;

public class ExternalProfileImporterTests
{
    [Fact]
    public void Import_ParsesDs4WindowsXmlDeadzonesAntiDeadzoneAndRumbleBoost()
    {
        const string xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <DS4Windows>
              <LSDeadZone>0.12</LSDeadZone>
              <RSDeadZone>8</RSDeadZone>
              <LSAntiDeadZone>0.15</LSAntiDeadZone>
              <L2DeadZone>10</L2DeadZone>
              <RumbleBoost>140</RumbleBoost>
            </DS4Windows>
            """;

        var result = ExternalProfileImporter.Import("shooter.xml", xml);
        Assert.Equal("DS4Windows", result.SourceFormat);
        Assert.Equal(12.0, result.Profile.Sticks["LeftStick"].Inner);
        Assert.Equal(15.0, result.Profile.Sticks["LeftStick"].AntiDeadzone);
        Assert.Equal(8.0, result.Profile.Sticks["RightStick"].Inner);
        Assert.Equal(10, result.Profile.Curves["LeftTrigger"].Inner);
        Assert.Equal(140, result.Profile.Vibration);
        result.Profile.Validate();
    }

    [Fact]
    public void Import_ParsesSteamInputVdfDeadzonesAndDigitalRemaps()
    {
        const string vdf = """
            "controller_mappings"
            {
                "settings"
                {
                    "deadzone_inner_radius" "0.18"
                    "deadzone_outer_radius" "0.95"
                }
                "bindings"
                {
                    "button_a" "xinput_button B"
                    "left_bumper" "shoulder_right"
                }
            }
            """;

        var result = ExternalProfileImporter.Import("action.vdf", vdf);
        Assert.Equal("Steam VDF", result.SourceFormat);
        Assert.Equal(18.0, result.Profile.Sticks["LeftStick"].Inner);
        Assert.Equal(95.0, result.Profile.Sticks["RightStick"].Outer);
        Assert.Equal("B", result.Profile.Remap["A"]);
        Assert.Equal("RB", result.Profile.Remap["LB"]);
        result.Profile.Validate();
    }

    [Fact]
    public void Import_PassesThroughNativeX20PcAndRejectsMalformedXml()
    {
        var original = new PcModeProfile { Vibration = 165 };
        string json = PcModeShare.ToJson(original);
        var nativeResult = ExternalProfileImporter.Import("my.x20pc", json);
        Assert.Equal("X20CTL", nativeResult.SourceFormat);
        Assert.Equal(165, nativeResult.Profile.Vibration);

        Assert.Throws<InvalidDataException>(() => ExternalProfileImporter.Import("broken.xml", "<DS4Windows><Unclosed>"));
    }
}
