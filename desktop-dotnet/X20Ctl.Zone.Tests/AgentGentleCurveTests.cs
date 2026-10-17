using System.Text.Json;
using X20Ctl.Zone.Agent;
using X20Ctl.Zone.PcMode;
namespace X20Ctl.Zone.Tests;

public sealed class AgentGentleCurveTests
{
    [Fact]
    public void Sniping_cannot_be_described_as_slow_while_the_curve_is_fast()
    {
        var bad = ProposalValidator.Check(Output("10,5,20,45"), "x20_pro");
        Assert.NotNull(AgentSession.CurveIntentProblem("Make my stick precise for sniping", new(), bad));
        Assert.Null(AgentSession.CurveIntentProblem("Use points 10,5,20,45", new(), bad));
        Assert.Null(AgentSession.CurveIntentProblem("Slightly adjust it using exactly 10,5,20,45", new(), bad));
    }
    static string Output(string points) => JsonSerializer.Serialize(new { speech = "I'll adjust the right stick slightly.", actions = new[] { new { action = "stick_curve_points", target = "RightStick", value = points, reason = "gentle aim" } }, recap = "Small change.", assumptions = Array.Empty<string>(), sources = Array.Empty<int>(), question = "" });
    [Theory]
    [InlineData("30,28,70,68", false)]
    [InlineData("35,20,75,62", true)]
    public void Small_request_checks_the_output_not_just_point_ranges(string points, bool reject)
        => Assert.Equal(reject, AgentSession.GentleCurveProblem("Make my aim slightly slower", new(), ProposalValidator.Check(Output(points), "x20_pro")) != null);
    [Theory]
    [InlineData("30,28,70,68", true)]
    [InlineData("35,20,75,62", false)]
    public async Task Retry_only_keeps_a_genuinely_gentle_change(string repair, bool accepted)
    {
        var model = new Model(Output("35,20,75,62"), Output(repair));
        var turn = await new AgentSession(model, "x20_pro", "X20 Pro").Ask("Make my aim slightly slower", new());
        Assert.Equal(accepted, turn.Verdict.Valid);
        Assert.Equal(2, turn.Attempts.Count);
        Assert.Equal(accepted, turn.Outcome.Changes.Count > 0);
    }
    sealed class Model(params string[] replies) : IAgentModel
    {
        int index;
        public string Name => "Fake";
        public Task<ModelReply> Chat(IReadOnlyList<ChatMessage> messages, string schema, CancellationToken cancel)
            => Task.FromResult(new ModelReply(replies[Math.Min(index++, replies.Length - 1)], 0, 0, 0, 0));
    }
}
