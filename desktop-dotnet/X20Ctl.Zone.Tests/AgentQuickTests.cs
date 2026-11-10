using System.Text.Json;
using X20Ctl.Zone.Agent;
using X20Ctl.Zone.PcMode;

/// <summary>Owner, 8 Nov 2026: everyday commands answered at once, and the lights in any colour words.</summary>
public class AgentQuickTests
{
    static string Actions(string sentence)
    {
        var p = AgentDirect.Resolve(sentence);
        Assert.NotNull(p);
        var verdict = ProposalValidator.Check(AgentClarification.Json(p!), "x20_pro");
        Assert.True(verdict.Valid, verdict.StructureError ?? string.Join("; ", verdict.Checks.Select(c => c.Message)));
        return string.Join(" + ", p!.Actions.Select(a => $"{a.Action}:{a.Target}={a.ValueText}"));
    }

    [Theory]
    [InlineData("Map \" A \" to \" B \"", "remap:A=B")]
    [InlineData("map a to b", "remap:A=B")]
    [InlineData("please remap the X button to Y", "remap:X=Y")]
    [InlineData("make LB act like RB", "remap:LB=RB")]
    [InlineData("can you make R2 send L2 for me", "remap:RT=LT")]
    [InlineData("bind square to cross", "remap:X=A")]
    [InlineData("swap A and B", "remap:A=B + remap:B=A")]
    [InlineData("switch the bumpers? no: swap lb with rb", null)]
    [InlineData("turbo on X at 12", "turbo:X=12")]
    [InlineData("set turbo to A 15 presses a second", "turbo:A=15")]
    [InlineData("RB turbo", "turbo:RB=10")]
    [InlineData("turn off turbo on X", "turbo:X=0")]
    [InlineData("reset A", "reset:A=0")]
    [InlineData("turn on pc mode", "pc_mode:PcMode=on")]
    [InlineData("pc mode off", "pc_mode:PcMode=off")]
    public void EverydayCommandsNeedNoModel(string sentence, string? expected)
    {
        if (expected == null) { Assert.Null(AgentDirect.Resolve(sentence)); return; }
        Assert.Equal(expected, Actions(sentence));
    }

    [Theory]
    [InlineData("Change my leds to blue")]
    [InlineData("change my rgb to blueish yellow")]
    [InlineData("set the lights to dark teal please")]
    [InlineData("make my led colour light purple")]
    [InlineData("rgb neon green")]
    [InlineData("turn the lighting into warm white")]
    [InlineData("change led color to #ff8800")]
    public void LightRequestsAreReadAtOnce(string sentence) => Assert.StartsWith("led_color:Lighting=", Actions(sentence));

    [Theory]
    [InlineData("red", 255, 0, 0)]
    [InlineData("#00FF80", 0, 255, 128)]
    [InlineData("rgb(10, 20, 30)", 10, 20, 30)]
    [InlineData("blueish yellow", 166, 178, 89)]
    [InlineData("blue green", 0, 168, 128)]
    public void ColoursAreReadExactly(string words, int r, int g, int b)
    {
        Assert.True(AgentColors.TryParse(words, out var c));
        Assert.Equal((r, g, b), ((int)c.R, (int)c.G, (int)c.B));
    }

    [Fact]
    public void ShadesMoveTheColourTheRightWay()
    {
        AgentColors.TryParse("purple", out var purple);
        AgentColors.TryParse("light purple", out var light);
        AgentColors.TryParse("dark purple", out var dark);
        Assert.True(light.R + light.G + light.B > purple.R + purple.G + purple.B);
        Assert.True(dark.R + dark.G + dark.B < purple.R + purple.G + purple.B);
        AgentColors.TryParse("reddish orange", out var ro); AgentColors.TryParse("orange", out var o);
        Assert.True(ro.G < o.G);
    }

    [Theory]
    [InlineData("banana phone")]
    [InlineData("")]
    [InlineData("the best one")]
    public void WordsThatArentColoursAreNotGuessed(string words) => Assert.False(AgentColors.TryParse(words, out _));

    [Fact]
    public void TheValidatorRefusesAColourItCantRead()
    {
        var json = JsonSerializer.Serialize(new { speech = "s", actions = new[] { new { action = "led_color", target = "Lighting", value = "sparkly", reason = "r" } }, recap = "", assumptions = Array.Empty<string>() });
        Assert.False(ProposalValidator.Check(json, "x20_pro").Valid);
    }

    [Fact]
    public void TheLightsReachTheOutcomeNotTheProfile()
    {
        var verdict = ProposalValidator.Check(AgentClarification.Json(AgentDirect.Resolve("change my leds to red")!), "x20_pro");
        var outcome = AgentApplier.Apply(new PcModeProfile(), verdict);
        Assert.Equal("#FF0000", outcome.Lighting!.Hex);
        Assert.Empty(outcome.Changes);
    }
}
