using System.Text.Json;
using X20Ctl.Zone.Agent;
namespace X20Ctl.Zone.Tests;

/// <summary>What the owner reads when something is refused: setting names as words, never code names like stick_deadzone.</summary>
public sealed class AgentWordingTests
{
    static AgentAction Act(string action, string target) => new(action, target, JsonDocument.Parse("0").RootElement, "");

    [Theory]
    [InlineData("stick_deadzone", "LeftStick", "Left stick dead zone")]
    [InlineData("stick_curve_points", "RightStick", "Custom right stick curve")]
    [InlineData("trigger_full_at", "RightTrigger", "Right trigger full at")]
    [InlineData("turbo", "X", "Turbo on X")]
    [InlineData("paddle", "M5", "Paddle on M5")]
    [InlineData("pc_mode", "PcMode", "PC mode")]
    [InlineData("reset", "All", "Reset for everything")]
    public void Settings_are_named_in_words(string action, string target, string expected) =>
        Assert.Equal(expected, AgentSession.Label(Act(action, target)));

    [Fact]
    public async Task A_refused_value_reads_plainly()
    {
        var turn = await new AgentSession(new OfflineAgent(), "x20_pro", "X20 Pro").Ask("Set my left stick dead zone to 260", new());
        string said = turn.Verdict.Proposal?.Speech ?? AgentSession.Problems(turn.Verdict);
        Assert.Contains("Left stick dead zone: 0 to 40 only; 260 is outside.", said);
        Assert.DoesNotContain("stick_deadzone", said);
        Assert.DoesNotContain("LeftStick", said);
    }

    [Fact]
    public async Task A_missing_paddle_reads_plainly()
    {
        var turn = await new AgentSession(new OfflineAgent(), "x20", "X20").Ask("Put jump on M5", new());
        string said = turn.Verdict.Proposal?.Speech ?? AgentSession.Problems(turn.Verdict);
        Assert.Contains("Paddle on M5: This controller has no M5.", said);
    }
}
