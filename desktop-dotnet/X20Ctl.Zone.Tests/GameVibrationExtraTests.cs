using X20Ctl.Zone.PcMode;
namespace X20Ctl.Zone.Tests;

public class GameVibrationExtraTests
{
    [Fact]
    public void An_X20_Pro_PC_mode_setup_above_100_percent_opens_both_ways()
    {
        var vib = new VibrationDraft("x20_pro") { PcMode = true }; vib.Set(150); vib.SetMotors(180, 90, 10);
        var snap = SetupSnapshot.Capture("x20_pro", new ButtonDraft("x20_pro"), new CurveDraft("x20_pro"), new MacroDraft("x20_pro") { PcMode = true }, vib);
        snap.Validate();
        var pc = new VibrationDraft("x20_pro") { PcMode = true };
        snap.LoadInto(new ButtonDraft("x20_pro") { PcMode = true }, new CurveDraft("x20_pro") { PcMode = true }, new MacroDraft("x20_pro") { PcMode = true }, pc);
        Assert.Equal((150, 180, 90, 10), (pc.Strength, pc.Left, pc.Right, pc.Floor));
        var pad = new VibrationDraft("x20_pro");
        snap.LoadInto(new ButtonDraft("x20_pro"), new CurveDraft("x20_pro"), new MacroDraft("x20_pro"), pad);
        Assert.Equal(100, pad.Strength); // on the pad itself, 100% is the most
    }
}
