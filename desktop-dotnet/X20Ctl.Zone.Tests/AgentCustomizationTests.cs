using System.Text.Json;
using X20Ctl.Zone.Agent;
using X20Ctl.Zone.PcMode;

namespace X20Ctl.Zone.Tests;

public class AgentCustomizationTests
{
    static object Action(string action, string target, object value) => new { action, target, value, reason = "Requested" };
    static string Reply(string speech = "Here are the settings.", string question = "", params object[] actions) =>
        JsonSerializer.Serialize(new { speech, actions, recap = "Local settings", assumptions = Array.Empty<string>(), sources = Array.Empty<int>(), question });
    static AgentVerdict Check(string action, string target, object value) =>
        ProposalValidator.Check(Reply(actions: [Action(action, target, value)]), "x20_pro");

    [Theory]
    [InlineData("0,0,100,100", 0, 0, 100, 100)]
    [InlineData(" 35, 20, 75, 62 ", 35, 20, 75, 62)]
    [InlineData("25,100,70,0", 25, 100, 70, 0)]
    public void Curve_points_parse_whole_percents(string text, int x1, int y1, int x2, int y2)
    {
        Assert.True(CurvePoints.TryParse(text, out var points, out var why));
        Assert.Equal((x1, y1, x2, y2), points);
        Assert.Empty(why);
    }

    [Theory]
    [InlineData("")]
    [InlineData("25,15,70")]
    [InlineData("25,15,70,60,90")]
    [InlineData("25.5,15,70,60")]
    [InlineData("25%,15,70,60")]
    [InlineData("2147483648,15,70,60")]
    [InlineData("-1,15,70,60")]
    [InlineData("25,101,70,60")]
    [InlineData("70,15,70,60")]
    [InlineData("71,15,70,60")]
    public void Invalid_curve_points_have_a_reason_and_no_result(string text)
    {
        Assert.False(CurvePoints.TryParse(text, out var points, out var why));
        Assert.Equal(default, points);
        Assert.NotEmpty(why);
    }

    [Theory]
    [InlineData("default")]
    [InlineData("quick")]
    [InlineData("slow")]
    [InlineData("smooth")]
    [InlineData("fine")]
    public void Presets_pass_through_points_and_have_monotone_bounded_endpoints(string preset)
    {
        var (a, b) = CurveDraft.Presets[preset];
        double At(double x) => PadProcessor.Through(x, a.Item1 / 255d, a.Item2 / 255d, b.Item1 / 255d, b.Item2 / 255d);
        Assert.Equal(0, At(0)); Assert.Equal(1, At(1));
        Assert.Equal(0, At(-.1)); Assert.Equal(1, At(1.1));
        Assert.Equal(a.Item2 / 255d, At(a.Item1 / 255d), 12);
        Assert.Equal(b.Item2 / 255d, At(b.Item1 / 255d), 12);
        double previous = 0;
        for (int i = 0; i <= 100; i++)
        {
            double x = i / 100d, y = At(x);
            Assert.True(double.IsFinite(y)); Assert.InRange(y, 0, 1);
            Assert.True(y >= previous - 1e-12, $"{preset} decreases at {x}");
            if (preset == "default") Assert.Equal(x, y, 12);
            if (preset is "quick" or "fine") Assert.True(y >= x - 1e-12);
            if (preset == "slow") Assert.True(y <= x + 1e-12);
            previous = y;
        }
        if (preset == "smooth") { Assert.True(At(.3) < .3); Assert.True(At(.7) > .7); }
    }

    [Theory]
    [InlineData("stick_curve_points", "LeftStick")]
    [InlineData("stick_curve_points", "RightStick")]
    [InlineData("trigger_curve_points", "LeftTrigger")]
    [InlineData("trigger_curve_points", "RightTrigger")]
    public void Custom_points_apply_without_changing_existing_travel_or_input(string action, string target)
    {
        var current = new PcModeProfile { Curves = { [target] = new("slow", "slow", 5, 90, 89, 51, 191, 140) } };
        string before = JsonSerializer.Serialize(current);
        var verdict = Check(action, target, "20,10,80,70");
        Assert.True(verdict.Valid);
        var outcome = AgentApplier.Apply(current, verdict);
        Assert.Null(outcome.Error); Assert.Single(outcome.Changes);
        var curve = outcome.Next!.Curves[target];
        Assert.Equal("custom", curve.Preset); Assert.Equal("slow", curve.BasePreset);
        Assert.Equal((5, 90), (curve.Inner, curve.Outer));
        Assert.Equal((51, 26, 204, 178), (curve.X1, curve.Y1, curve.X2, curve.Y2));
        Assert.Equal(before, JsonSerializer.Serialize(current));
        outcome.Next.Validate();
    }

    [Theory]
    [InlineData(60, 50)]
    [InlineData(90.5, 125)]
    [InlineData(100, 150)]
    public void Advanced_stick_values_apply_at_boundaries(double max, int ratio)
    {
        foreach (string target in AgentVocabulary.Sticks)
        {
            var current = new PcModeProfile();
            var verdict = ProposalValidator.Check(Reply(actions: [Action("stick_max_output", target, max), Action("stick_axis_ratio", target, ratio), Action("stick_square_gate", target, "on")]), "x20_pro");
            Assert.True(verdict.Valid);
            var next = AgentApplier.Apply(current, verdict).Next!;
            Assert.Equal(max, next.Sticks[target].MaxOutput); Assert.Equal(ratio, next.Sticks[target].AxisRatio);
            Assert.True(next.Sticks[target].SquareGate); Assert.Empty(current.Sticks);
            var off = AgentApplier.Apply(next, Check("stick_square_gate", target, "off"));
            Assert.False(off.Next!.Sticks[target].SquareGate); Assert.Single(off.Changes);
        }
    }

    [Theory]
    [InlineData("stick_curve_points", "LeftTrigger", "20,10,80,70")]
    [InlineData("trigger_curve_points", "LeftStick", "20,10,80,70")]
    [InlineData("stick_curve_points", "LeftStick", "80,10,20,70")]
    [InlineData("stick_curve_points", "LeftStick", 20)]
    [InlineData("stick_max_output", "LeftStick", 59.9)]
    [InlineData("stick_max_output", "LeftStick", 100.1)]
    [InlineData("stick_max_output", "LeftTrigger", 90)]
    [InlineData("stick_max_output", "LeftStick", "90")]
    [InlineData("stick_axis_ratio", "RightStick", 49)]
    [InlineData("stick_axis_ratio", "RightStick", 151)]
    [InlineData("stick_axis_ratio", "RightStick", 100.5)]
    [InlineData("stick_axis_ratio", "A", 100)]
    [InlineData("stick_square_gate", "A", "on")]
    [InlineData("stick_square_gate", "LeftStick", "maybe")]
    [InlineData("mouse_mode", "Page", "on")]
    [InlineData("mouse_mode", "Mouse", "maybe")]
    [InlineData("mouse_mode", "Mouse", 1)]
    [InlineData("open_page", "Mouse", "Diagnostics")]
    [InlineData("open_page", "Page", "Settings")]
    public void Invalid_customization_rejects_the_whole_proposal(string action, string target, object value)
    {
        var current = new PcModeProfile();
        var verdict = ProposalValidator.Check(Reply(actions: [Action("turbo", "X", 12), Action(action, target, value)]), "x20_pro");
        Assert.False(verdict.Valid);
        var outcome = AgentApplier.Apply(current, verdict);
        Assert.Null(outcome.Next); Assert.NotNull(outcome.Error); Assert.Empty(outcome.Changes);
        Assert.Null(outcome.Mouse); Assert.Null(outcome.Page); Assert.True(current.IsNeutral);
    }

    [Theory]
    [InlineData("on", true)]
    [InlineData("off", false)]
    public void Mouse_mode_is_an_app_effect_without_profile_changes(string value, bool expected)
    {
        var verdict = Check("mouse_mode", "Mouse", value);
        Assert.True(verdict.Valid);
        var outcome = AgentApplier.Apply(new(), verdict);
        Assert.Equal(expected, outcome.Mouse); Assert.Null(outcome.Page);
        Assert.Empty(outcome.Changes); Assert.True(outcome.Next!.IsNeutral);
    }

    public static IEnumerable<object[]> Pages => AgentVocabulary.Pages.Select(p => new object[] { p });
    [Theory]
    [MemberData(nameof(Pages))]
    public void Each_supported_page_is_valid_and_applies_as_navigation(string page)
    {
        var verdict = Check("open_page", "Page", page);
        Assert.True(verdict.Valid);
        var outcome = AgentApplier.Apply(new(), verdict);
        Assert.Equal(page, outcome.Page); Assert.Null(outcome.Mouse);
        Assert.Empty(outcome.Changes); Assert.True(outcome.Next!.IsNeutral);
    }

    [Theory]
    [InlineData("diagnostics", AgentCommand.Diagnostics)]
    [InlineData("DIAGNOSE my controller!", AgentCommand.Diagnostics)]
    [InlineData("test my sticks", AgentCommand.Diagnostics)]
    [InlineData("test the triggers", AgentCommand.Diagnostics)]
    [InlineData("check my pad", AgentCommand.Diagnostics)]
    [InlineData("check the buttons", AgentCommand.Diagnostics)]
    [InlineData("check my controller battery", AgentCommand.Diagnostics)]
    [InlineData("make my sticks slower", AgentCommand.None)]
    [InlineData("test my internet", AgentCommand.None)]
    public void Diagnostics_commands_route_before_model_requests(string sentence, AgentCommand expected) => Assert.Equal(expected, AgentCommands.Parse(sentence));

    [Theory]
    [InlineData("I'll set the curve", true)]
    [InlineData("I will gently adjust it", true)]
    [InlineData("I've made it slower", true)]
    [InlineData("Let me tweak it", true)]
    [InlineData("Mouse mode is now on", true)]
    [InlineData("I'M SETTING the curve", true)]
    [InlineData("This setting controls speed", false)]
    [InlineData("You can change it in Curves", false)]
    [InlineData("Which stick?", false)]
    [InlineData("", false)]
    public void Promises_distinguishes_changes_from_explanation(string speech, bool expected) => Assert.Equal(expected, AgentSession.Promises(speech));

    sealed class FakeModel(params string[] replies) : IAgentModel
    {
        public string Name => "A1 fake";
        public List<ChatMessage[]> Seen { get; } = [];
        public Task<ModelReply> Chat(IReadOnlyList<ChatMessage> messages, string schema, CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();
            Assert.Equal(AgentSession.Schema, schema);
            Assert.True(Seen.Count < replies.Length, "Unexpected additional model call");
            string reply = replies[Seen.Count]; Seen.Add(messages.ToArray());
            return Task.FromResult(new ModelReply(reply, 10, 20, 0, 1));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Promise_or_unchanged_action_gets_a_semantic_retry_and_followup_state(bool unchanged)
    {
        string first = Reply("I'll lower the maximum.", actions: unchanged ? [Action("stick_max_output", "RightStick", 100)] : []);
        var model = new FakeModel(first, Reply("I'll lower it to 90.", actions: [Action("stick_max_output", "RightStick", 90)]), Reply());
        var session = new AgentSession(model, "x20_pro", "X20 Pro");
        var current = new PcModeProfile();
        var turn = await session.Ask("less sensitive please", current);
        Assert.Equal(2, turn.Attempts.Count); Assert.True(turn.Verdict.Valid);
        Assert.Equal(90, turn.Outcome.Next!.Sticks["RightStick"].MaxOutput); Assert.Empty(current.Sticks);
        Assert.Contains(unchanged ? "already my settings" : "sent no actions", model.Seen[1][^1].Content);
        await session.Ask("a bit more", turn.Outcome.Next);
        Assert.Contains("90%", model.Seen[2][0].Content);
        Assert.Contains(model.Seen[2], m => m.Role == "assistant" && m.Content.Contains("[applied:") && m.Content.Contains("100% -> 90%"));
        Assert.DoesNotContain(model.Seen[2], m => m.Content.Contains("sent no actions"));
        session.Forget();
    }

    [Theory]
    [InlineData("stick_curve", "stick_curve_points", "RightStick")]
    [InlineData("trigger_curve", "trigger_curve_points", "RightTrigger")]
    public async Task Unnamed_preset_retries_as_custom_points_and_remembers_accepted_shape(string presetAction, string pointsAction, string target)
    {
        var model = new FakeModel(Reply("A slower curve.", actions: [Action(presetAction, target, "slow")]), Reply("A custom curve.", actions: [Action(pointsAction, target, "20,10,80,70")]), Reply());
        var session = new AgentSession(model, "x20_pro", "X20 Pro");
        var turn = await session.Ask("more precise please", new());
        Assert.Equal(2, turn.Attempts.Count); Assert.True(turn.Verdict.Valid);
        Assert.Contains(pointsAction, model.Seen[1][^1].Content);
        Assert.Equal("custom", turn.Outcome.Next!.Curves[target].Preset);
        Assert.Equal(pointsAction, Assert.Single(turn.Verdict.Proposal!.Actions).Action);
        await session.Ask("a little more", turn.Outcome.Next);
        Assert.Contains("custom", model.Seen[2][0].Content);
        Assert.Contains(model.Seen[2], m => m.Role == "assistant" && m.Content.Contains("A custom curve.") && m.Content.Contains("[applied:"));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Bad_or_no_change_semantic_retry_keeps_first_verdict_outcome_and_history(bool preset, bool invalid)
    {
        string first = preset ? Reply("Original preset.", actions: [Action("stick_curve", "RightStick", "slow")]) : Reply("I'll lower it.");
        string retry = invalid ? Reply("Bad retry.", actions: [Action("stick_max_output", "RightStick", 20)]) : Reply("No change.");
        var model = new FakeModel(first, retry, Reply());
        var session = new AgentSession(model, "x20_pro", "X20 Pro");
        var turn = await session.Ask("more precise please", new());
        Assert.Equal(2, turn.Attempts.Count);
        Assert.True(turn.Verdict.Valid); Assert.Equal(ProposalValidator.Check(first, "x20_pro").Proposal!.Speech, turn.Verdict.Proposal!.Speech);
        Assert.Equal(retry, turn.Reply.Content);
        if (preset) Assert.Equal("slow", turn.Outcome.Next!.Curves["RightStick"].Preset);
        else Assert.Empty(turn.Outcome.Changes);
        await session.Ask("what happened?", turn.Outcome.Next!);
        var remembered = model.Seen[^1].Where(m => m.Role == "assistant").ToArray();
        Assert.Single(remembered); Assert.StartsWith(turn.Verdict.Proposal.Speech, remembered[0].Content);
        Assert.DoesNotContain("Bad retry", remembered[0].Content); Assert.DoesNotContain("No change", remembered[0].Content);
    }

    [Theory]
    [InlineData("use SLOW please", "", "stick_curve", "RightStick", "slow")]
    [InlineData("which stick?", "Which stick?", "", "", "")]
    [InlineData("explain the curve", "", "", "", "")]
    [InlineData("mouse on", "", "mouse_mode", "Mouse", "on")]
    [InlineData("open diagnostics", "", "open_page", "Page", "Diagnostics")]
    public async Task Named_presets_questions_explanations_and_app_effects_do_not_retry(string sentence, string question, string action, string target, string value)
    {
        var model = new FakeModel(Reply(action.Length == 0 && question.Length == 0 ? "The curve controls response." : "I'll set it.", question, action.Length == 0 ? [] : [Action(action, target, value)]));
        var turn = await new AgentSession(model, "x20_pro", "X20 Pro").Ask(sentence, new());
        Assert.Single(turn.Attempts); Assert.Single(model.Seen); Assert.True(turn.Verdict.Valid);
    }
}
