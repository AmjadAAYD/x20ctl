using System.Text.Json;
using X20Ctl.Zone.Agent;
using X20Ctl.Zone.PcMode;
namespace X20Ctl.Zone.Tests;
public sealed class AgentLatencyTests
{
    [Fact] public async Task Exact_numeric_command_does_not_generate_a_model_reply()
    {
        var model = new Model(); var session = new AgentSession(model, "x20_pro", "X20 Pro");
        var turn = await session.Ask("Set both sticks dead zone to 3", new());
        Assert.True(turn.Verdict.Valid); Assert.Equal(3, turn.Outcome.Next!.Sticks["RightStick"].Inner);
        Assert.Equal(0, model.Calls); Assert.Equal("Direct command", turn.Resolver);
        var repeat = await session.Ask("Set both sticks dead zone to 3", turn.Outcome.Next);
        Assert.Empty(repeat.Outcome.Changes); Assert.Equal(0, model.Calls);
    }
    [Fact] public async Task Direct_numeric_values_are_validated_including_negative_values()
    {
        var session = new AgentSession(new Model(), "x20_pro", "X20 Pro");
        Assert.False((await session.Ask("Set right stick dead zone to -3", new())).Verdict.Valid);
        Assert.False((await session.Ask("Set right stick dead zone to 260", new())).Verdict.Valid);
    }
    [Fact] public void Qualitative_or_compound_requests_still_need_the_model()
    {
        Assert.Null(AgentDirect.Resolve("Make my sticks slightly smoother"));
        Assert.Null(AgentDirect.Resolve("Set right stick dead zone to 3 and max output to 90"));
        Assert.Null(AgentDirect.Resolve("Why set right stick dead zone to 3?"));
    }
    [Fact] public async Task Invalid_replies_cannot_cascade_into_many_model_roundtrips()
    {
        var model = new Model("not json", "not json");
        var turn = await new AgentSession(model,"x20_pro","X20 Pro").Ask("Make my aim a little steadier",new());
        Assert.Equal(2,model.Calls); Assert.False(turn.Verdict.Valid);
    }
    [Fact] public void Less_sensitive_request_rejects_a_faster_curve()
    {
        var json = JsonSerializer.Serialize(new { speech = "Less sensitive.", actions = new[] { new { action = "stick_curve_points", target = "RightStick", value = "50,52,100,98", reason = "" } }, recap = "", assumptions = Array.Empty<string>(), sources = Array.Empty<int>(), question = "" });
        var verdict = ProposalValidator.Check(json,"x20_pro");
        Assert.NotNull(AgentSession.CurveIntentProblem("Make my right stick a little less sensitive for aiming",new(),verdict));
    }
    sealed class Model(params string[] replies) : IAgentModel
    {
        public int Calls; public string Name => "fake";
        public Task<ModelReply> Chat(IReadOnlyList<ChatMessage> messages,string schema,CancellationToken cancel)
        { Assert.True(Calls < replies.Length,"Unnecessary model request"); return Task.FromResult(new ModelReply(replies[Calls++],1,1,0,0)); }
    }
}
