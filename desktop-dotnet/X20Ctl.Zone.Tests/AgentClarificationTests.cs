using System.Text.Json;
using X20Ctl.Zone.Agent;
using X20Ctl.Zone.PcMode;
namespace X20Ctl.Zone.Tests;

public sealed class AgentClarificationTests
{
    static string Question(string text) => JsonSerializer.Serialize(new { speech = "One detail first.", actions = Array.Empty<object>(), recap = "", assumptions = Array.Empty<string>(), sources = Array.Empty<int>(), question = text });
    [Fact]
    public async Task Short_answer_completes_pending_turbo_without_another_model_guess()
    {
        var model = new Model(Question("Which button and what rate?"));
        var session = new AgentSession(model, "x20_pro", "X20 Pro");
        await session.Ask("Turn turbo on", new());
        Assert.True(session.HasPendingClarification);
        var done = await session.Ask("X at 12", new());
        Assert.Equal(12, done.Outcome.Next!.Turbo["X"]);
        Assert.False(session.HasPendingClarification);
        Assert.Single(model.Seen);
        Assert.Equal("App clarification", done.Resolver);
    }
    [Fact]
    public async Task Generic_answer_receives_explicit_request_and_question_context()
    {
        var model = new Model(Question("Which game?"), Question("Which stick?"));
        var session = new AgentSession(model, "x20_pro", "X20 Pro");
        await session.Ask("Tune my aim", new());
        await session.Ask("Valorant", new());
        Assert.Contains(model.Seen[1], m => m.Role == "system" && m.Content.Contains("Which game?"));
        Assert.Contains("Tune my aim", model.Seen[1][^1].Content);
        Assert.Contains("Valorant", session.Pending!.Request);
    }
    [Theory]
    [InlineData("swap A", "Which other button?", "B", "A", "B")]
    [InlineData("remap LB", "What should it send?", "X", "LB", "X")]
    [InlineData("map to A", "Which source button?", "LB", "LB", "A")]
    public async Task Mapping_preserves_source_and_destination(string request, string question, string answer, string source, string target)
    {
        var session = new AgentSession(new Model(Question(question)), "x20_pro", "X20 Pro");
        await session.Ask(request, new());
        var turn = await session.Ask(answer, new());
        Assert.Equal(target, turn.Outcome.Next!.Remap[source]);
    }
    [Fact]
    public async Task Bad_numeric_answer_is_rejected_and_pending_question_is_retained()
    {
        var session = new AgentSession(new Model(Question("What dead zone?")), "x20_pro", "X20 Pro");
        await session.Ask("Set right stick dead zone", new());
        var bad = await session.Ask("260", new());
        Assert.False(bad.Verdict.Valid); Assert.True(session.HasPendingClarification);
        var good = await session.Ask("8", new());
        Assert.Equal(8, good.Outcome.Next!.Sticks["RightStick"].Inner);
    }
    [Fact]
    public async Task Cancel_and_new_session_clear_pending_state()
    {
        var session = new AgentSession(new Model(Question("Which button?"), Question("Which button?")), "x20_pro", "X20 Pro");
        await session.Ask("Turn turbo on", new());
        var cancelled = await session.Ask("Never mind", new());
        Assert.Empty(cancelled.Outcome.Changes); Assert.False(session.HasPendingClarification);
        await session.Ask("Turn turbo on", new()); session.Forget();
        Assert.False(session.HasPendingClarification);
    }
    [Fact]
    public async Task Offline_question_is_pending_and_answer_applies()
    {
        var session = new AgentSession(new OfflineAgent(), "x20_pro", "X20 Pro");
        await session.Ask("Turn turbo on", new());
        Assert.True(session.HasPendingClarification);
        var turn = await session.Ask("A at 12", new());
        Assert.True(turn.Verdict.Valid);
        Assert.Equal(12, turn.Outcome.Next!.Turbo["A"]);
        Assert.False(session.HasPendingClarification);
    }
    sealed class Model(params string[] replies) : IAgentModel
    {
        public List<ChatMessage[]> Seen { get; } = [];
        public string Name => "Fake";
        public Task<ModelReply> Chat(IReadOnlyList<ChatMessage> messages, string schema, CancellationToken cancel)
        { Seen.Add(messages.ToArray()); return Task.FromResult(new ModelReply(replies[Math.Min(Seen.Count - 1, replies.Length - 1)], 0, 0, 0, 0)); }
    }
}
