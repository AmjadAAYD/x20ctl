using X20Ctl.Product;

namespace X20Ctl.Zone.Tests;

public class ControllerIdentityTests
{
    private static DeviceIdentity Hid(
        int vid,
        int pid,
        string product = "",
        string manufacturer = "",
        int usagePage = 1,
        int usage = 5,
        string container = "",
        string path = "") =>
        new("HID", path.Length > 0 ? path : $@"\\?\hid#vid_{vid:x4}&pid_{pid:x4}#{usagePage:x4}_{usage:x2}", container, product, manufacturer, vid, pid, usagePage, usage);

    [Fact]
    public void SharedDInput_0079_181C_WithX20Pro_IsUnconfirmed_NotMismatch()
    {
        var pad = Hid(0x0079, 0x181C, product: "STK-7024X", manufacturer: "DragonRise Inc.");
        var result = ControllerIdentity.Evaluate("x20_pro", [pad]);

        Assert.Equal(VerifyOutcome.Unconfirmed, result.Outcome);
        Assert.Null(result.DetectedId);
        Assert.Contains("0079:181C", result.Detail);
        Assert.Contains("STK-7024X", result.Detail);
        Assert.NotNull(result.Controllers);
        var listed = Assert.Single(result.Controllers!);
        Assert.Equal("STK-7024X", listed.Name);
        Assert.Null(listed.ModelId);
        Assert.Contains("DInput mode (model unconfirmed)", listed.Detail);
    }

    [Fact]
    public void SharedDInput_0079_181C_EmptyProduct_UsesHonestLabel()
    {
        var pad = Hid(0x0079, 0x181C);
        var result = ControllerIdentity.Evaluate("x20_pro", [pad]);

        Assert.Equal(VerifyOutcome.Unconfirmed, result.Outcome);
        Assert.Null(result.DetectedId);
        var listed = Assert.Single(result.Controllers!);
        Assert.Equal("DInput gamepad (0079:181C)", listed.Name);
        Assert.Null(listed.ModelId);
    }

    [Fact]
    public void RetractedXenta_1D57_FA60_Alone_IsIgnoredAsNoController()
    {
        var xentaMouse = Hid(0x1D57, 0xFA60, product: "2.4G Receiver", manufacturer: "Xenta", usagePage: 1, usage: 2, container: "xenta");
        var xentaKbd = Hid(0x1D57, 0xFA60, product: "2.4G Receiver", manufacturer: "Xenta", usagePage: 1, usage: 6, container: "xenta");
        var xentaEvenIfUsage5 = Hid(0x1D57, 0xFA60, product: "2.4G Receiver", manufacturer: "EasySMX", usagePage: 1, usage: 5, container: "xenta");

        var result = ControllerIdentity.Evaluate("x20_pro", [xentaMouse, xentaKbd, xentaEvenIfUsage5]);

        Assert.Equal(VerifyOutcome.NoController, result.Outcome);
        Assert.Empty(result.Controllers!);
    }

    [Fact]
    public void Receiver_413D_2131_WithRetractedXenta_StillMatchesX20Pro()
    {
        var xenta = Hid(0x1D57, 0xFA60, product: "2.4G Receiver", usagePage: 1, usage: 2, container: "xenta");
        var dongle = Hid(0x413D, 0x2131, product: "Controller", usagePage: 0xFF70, usage: 1, container: "dongle");

        var result = ControllerIdentity.Evaluate("x20_pro", [xenta, dongle]);

        Assert.Equal(VerifyOutcome.Match, result.Outcome);
        Assert.Equal("x20_pro", result.DetectedId);
        Assert.Equal("Receiver", result.Transport);
        var listed = Assert.Single(result.Controllers!);
        Assert.Equal("EasySMX receiver (X20 Pro or Dune)", listed.Name);
    }

    [Fact]
    public void Receiver_413D_2131_MatchesDune_AndLeavesX20Unconfirmed()
    {
        var dongle = Hid(0x413D, 0x2131, usagePage: 1, usage: 5, container: "dongle");

        var duneResult = ControllerIdentity.Evaluate("dune", [dongle]);
        Assert.Equal(VerifyOutcome.Match, duneResult.Outcome);
        Assert.Equal("dune", duneResult.DetectedId);

        var x20Result = ControllerIdentity.Evaluate("x20", [dongle]);
        Assert.Equal(VerifyOutcome.Unconfirmed, x20Result.Outcome);
        Assert.Null(x20Result.DetectedId);
    }

    [Fact]
    public void MultiController_ExplicitTargetOrReceiverWinsOverOtherPad()
    {
        var x20Pad = Hid(0x0079, 0x181C, product: "EasySMX X20", container: "c1");
        var proDongle = Hid(0x413D, 0x2131, product: "Controller", usagePage: 0xFF70, usage: 1, container: "c2");
        var proPad = Hid(0x045E, 0x028E, product: "EasySMX X20 Pro", container: "c3");

        var withDongle = ControllerIdentity.Evaluate("x20_pro", [x20Pad, proDongle]);
        Assert.Equal(VerifyOutcome.Match, withDongle.Outcome);
        Assert.Equal("x20_pro", withDongle.DetectedId);

        var withNamedPro = ControllerIdentity.Evaluate("x20_pro", [x20Pad, proPad]);
        Assert.Equal(VerifyOutcome.Match, withNamedPro.Outcome);
        Assert.Equal("x20_pro", withNamedPro.DetectedId);
        Assert.Equal("EasySMX X20 Pro", withNamedPro.DetectedName);
    }

    [Theory]
    [InlineData("EasySMX X20 Pro", "X20 Pro", "x20_pro")]
    [InlineData("EasySMX X20PRO", "X20 Pro", "x20_pro")]
    [InlineData("EasySMX X20-Pro", "X20 Pro", "x20_pro")]
    [InlineData("EasySMX X20_Pro", "X20 Pro", "x20_pro")]
    [InlineData("EasySMX X20", "X20", "x20")]
    [InlineData("EasySMX X05PRO", "X05 Pro", "x05_pro")]
    [InlineData("EasySMX D15 Dune 8K", "Dune / D15", "dune")]
    public void CompactAndHyphenatedNames_ResolveCorrectModelBeforeX20(string product, string expectedModel, string expectedId)
    {
        var pad = Hid(0x045E, 0x028E, product: product);

        var detected = ControllerIdentity.DetectModel([pad]);
        Assert.NotNull(detected);
        Assert.Equal(expectedModel, detected!.Value.Model);

        var eval = ControllerIdentity.Evaluate(expectedId, [pad]);
        Assert.Equal(VerifyOutcome.Match, eval.Outcome);
        Assert.Equal(expectedId, eval.DetectedId);
    }

    [Fact]
    public void ExplicitOtherEasySMXName_ReturnsMismatchWithDetectedModel()
    {
        var explicitX20 = Hid(0x0079, 0x181C, product: "EasySMX X20", manufacturer: "ZhiXu");
        var result = ControllerIdentity.Evaluate("x20_pro", [explicitX20]);

        Assert.Equal(VerifyOutcome.Mismatch, result.Outcome);
        Assert.Equal("x20", result.DetectedId);
        Assert.Equal("EasySMX X20", result.DetectedName);
    }

    [Fact]
    public void GenericXboxXInput_045E_028E_IsUnconfirmedWithoutDInputHint()
    {
        var genericXbox = Hid(0x045E, 0x028E, product: "Controller (Xbox 360 For Windows)");
        var result = ControllerIdentity.Evaluate("x20_pro", [genericXbox]);

        Assert.Equal(VerifyOutcome.Unconfirmed, result.Outcome);
        Assert.Null(result.DetectedId);
        Assert.DoesNotContain("0079:181C", result.Detail);
    }
}
