using X20Ctl.Zone.Agent;
using X20Ctl.Zone.PcMode;
namespace X20Ctl.Zone.Tests;

public sealed class AgentDemoTests
{
    [Theory]
    [InlineData("demo aim")]
    [InlineData("demo trigger")]
    [InlineData("demo reset")]
    public void Script_is_repeatable_valid_and_does_not_mutate_input(string sentence)
    {
        var before = new PcModeProfile();
        var turn = AgentDemo.Run(sentence, "x20_pro", before);
        Assert.Equal(AgentDemo.Output(sentence), AgentDemo.Output(sentence.ToUpperInvariant()));
        Assert.True(turn.Verdict.Valid);
        Assert.True(before.IsNeutral);
        Assert.NotNull(turn.Outcome.Next);
        Assert.Null(turn.Measured);
        Assert.Empty(turn.Docs);
        Assert.Equal(0, turn.Reply.OutputTokens);
        Assert.Equal(0, turn.Reply.TotalMs);
    }

    [Fact]
    public void Bad_output_uses_real_validator_and_changes_nothing()
    {
        var turn = AgentDemo.Run("demo bad output", "x20_pro", new());
        Assert.False(turn.Verdict.Valid);
        Assert.Contains(turn.Verdict.Checks, c => c.State == CheckState.Rejected);
        Assert.Null(turn.Outcome.Next);
        Assert.Empty(turn.Outcome.Changes);
    }

    [Fact]
    public void Script_applies_exact_values_then_resets_preview()
    {
        var aim = AgentDemo.Run("demo aim", "x20_pro", new());
        Assert.Equal("35,20,75,62", aim.Verdict.Proposal!.Actions.Single().ValueText);
        Assert.NotEmpty(aim.Outcome.Changes);
        var trigger = AgentDemo.Run("demo trigger", "x20_pro", aim.Outcome.Next!);
        Assert.Equal(40, trigger.Outcome.Next!.Curves["RightTrigger"].Outer);
        var reset = AgentDemo.Run("demo reset", "x20_pro", trigger.Outcome.Next);
        Assert.True(reset.Outcome.Next!.IsNeutral);
    }

    [Theory]
    [InlineData("turn mouse on")]
    [InlineData("measure drift")]
    [InlineData("open diagnostics")]
    public void Unscripted_requests_are_explicit_canned_help_without_effects(string sentence)
    {
        var turn = AgentDemo.Run(sentence, "x20_pro", new());
        Assert.Contains("Canned demo", turn.Verdict.Proposal!.Speech);
        Assert.Empty(turn.Verdict.Proposal.Actions);
        Assert.Empty(turn.Outcome.Changes);
        Assert.Null(turn.Outcome.Mouse);
        Assert.Null(turn.Outcome.Page);
        Assert.Null(turn.Outcome.PcMode);
    }
}
