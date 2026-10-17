using System.Text.Json;
using X20Ctl.Zone.Agent;
using X20Ctl.Zone.PcMode;
namespace X20Ctl.Zone.Tests;

/// <summary>The Talk agent: what the model says is checked against fixed rules, and only a valid proposal changes settings.</summary>
public sealed class AgentTests
{
    static string Reply(params string[] actions) => $$"""{"speech":"I'll do it.","actions":[{{string.Join(",", actions)}}],"recap":"","assumptions":[]}""";
    static string Act(string action, string target, string value) => $$"""{"action":"{{action}}","target":"{{target}}","value":{{value}},"reason":"asked"}""";

    [Fact]
    public void A_valid_proposal_changes_only_what_it_names()
    {
        var verdict = ProposalValidator.Check(Reply(Act("stick_deadzone", "RightStick", "6"), Act("trigger_full_at", "RightTrigger", "40")), "x20_pro");
        Assert.True(verdict.Valid);
        var before = new PcModeProfile();
        var outcome = AgentApplier.Apply(before, verdict);
        Assert.NotNull(outcome.Next);
        Assert.Equal(6, outcome.Next!.Sticks["RightStick"].Inner);
        Assert.Equal(40, outcome.Next.Curves["RightTrigger"].Outer);
        Assert.False(outcome.Next.Sticks.ContainsKey("LeftStick"));
        Assert.Empty(outcome.Next.Remap);
        Assert.Equal(["Right stick dead zone", "Right trigger full at"], outcome.Changes.Select(c => c.What));
        Assert.True(before.IsNeutral); // the current profile is never touched
    }

    [Fact]
    public void One_rejected_action_rejects_the_whole_proposal()
    {
        var verdict = ProposalValidator.Check(Reply(Act("stick_deadzone", "RightStick", "6"), Act("stick_deadzone", "LeftStick", "260")), "x20_pro");
        Assert.False(verdict.Valid);
        Assert.Equal(CheckState.Rejected, verdict.Checks[1].State);
        var outcome = AgentApplier.Apply(new PcModeProfile(), verdict);
        Assert.Null(outcome.Next);
        Assert.Empty(outcome.Changes);
    }

    [Fact]
    public void M5_exists_on_the_X20_Pro_but_not_on_the_X20()
    {
        string m5 = Reply(Act("paddle", "M5", "\"A\""));
        var pro = ProposalValidator.Check(m5, "x20_pro");
        Assert.True(pro.Valid);
        Assert.Equal(CheckState.Unavailable, pro.Checks[0].State); // valid, but paddles aren't written yet
        var x20 = ProposalValidator.Check(m5, "x20");
        Assert.False(x20.Valid);
        Assert.Contains("no M5", x20.Checks[0].Message);
    }

    [Fact]
    public void Paddles_only_send_A_or_B()
    {
        var verdict = ProposalValidator.Check(Reply(Act("paddle", "M1", "\"X\"")), "x20_pro");
        Assert.False(verdict.Valid);
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("""{"speech":"hi","actions":[],"recap":""}""")] // assumptions missing
    [InlineData("""{"speech":"hi","actions":[],"assumptions":[]}""")] // recap missing
    [InlineData("""{"actions":[],"recap":"","assumptions":[]}""")] // speech missing (threw KeyNotFoundException before 12 Oct)
    [InlineData("""{"speech":"hi","actions":[],"recap":"","assumptions":[],"mood":"happy"}""")] // a field that isn't in the schema
    [InlineData("""{"speech":"hi","actions":[{"action":"turbo","target":"A","reason":"x"}],"recap":"","assumptions":[]}""")] // value missing
    public void Malformed_replies_are_structure_errors(string json)
    {
        var verdict = ProposalValidator.Check(json, "x20_pro");
        Assert.NotNull(verdict.StructureError);
        Assert.False(verdict.Valid);
        Assert.Null(AgentApplier.Apply(new PcModeProfile(), verdict).Next);
    }

    [Theory]
    [InlineData("stick_deadzone", "RightStick", "\"6%\"")] // text where a number belongs (seen from the live model, 12 Oct)
    [InlineData("stick_full_at", "RightStick", "40")]      // below 60
    [InlineData("stick_deadzone", "LT", "5")]             // a button, not a stick
    [InlineData("trigger_deadzone", "LeftTrigger", "5.5")] // triggers take whole percents
    [InlineData("stick_curve", "LeftStick", "\"turbo\"")]  // not a preset
    [InlineData("remap", "M1", "\"A\"")]                  // paddles aren't remapped like buttons
    [InlineData("turbo", "A", "50")]                      // above 30
    [InlineData("fly", "A", "1")]                         // not an action
    public void Rule_breaks_are_rejected(string action, string target, string value)
    {
        var verdict = ProposalValidator.Check(Reply(Act(action, target, value)), "x20_pro");
        Assert.Null(verdict.StructureError);
        Assert.False(verdict.Valid);
    }

    [Fact]
    public void A_repeated_change_is_kept_once_and_a_conflicting_one_is_rejected()
    {
        var same = ProposalValidator.Check(Reply(Act("stick_deadzone", "RightStick", "6"), Act("stick_deadzone", "RightStick", "6")), "x20_pro");
        Assert.True(same.Valid);
        Assert.Single(AgentApplier.Apply(new PcModeProfile(), same).Changes);
        var clash = ProposalValidator.Check(Reply(Act("stick_deadzone", "RightStick", "6"), Act("stick_deadzone", "RightStick", "9")), "x20_pro");
        Assert.False(clash.Valid);
    }

    [Fact]
    public void A_result_the_profile_wont_hold_is_rejected_whole()
    {
        // each value is in range alone, but a dead zone past where full speed is reached isn't a stick setting
        var verdict = ProposalValidator.Check(Reply(Act("trigger_deadzone", "LeftTrigger", "40"), Act("trigger_full_at", "LeftTrigger", "30")), "x20_pro");
        Assert.True(verdict.Valid);
        var outcome = AgentApplier.Apply(new PcModeProfile(), verdict);
        Assert.Null(outcome.Next);
        Assert.NotNull(outcome.Error);
    }

    [Fact]
    public void Presets_remaps_turbo_and_reset_land_where_PC_mode_reads_them()
    {
        var verdict = ProposalValidator.Check(Reply(Act("stick_curve", "LeftStick", "\"smooth\""), Act("remap", "A", "\"B\""), Act("turbo", "X", "12")), "x20");
        var next = AgentApplier.Apply(new PcModeProfile(), verdict).Next!;
        Assert.Equal("smooth", next.Curves["LeftStick"].Preset);
        Assert.Equal((77, 59), (next.Curves["LeftStick"].X1, next.Curves["LeftStick"].Y1));
        Assert.Equal("B", next.Remap["A"]);
        Assert.Equal(12, next.Turbo["X"]);
        var reset = AgentApplier.Apply(next, ProposalValidator.Check(Reply(Act("reset", "All", "0")), "x20")).Next!;
        Assert.True(reset.IsNeutral);
    }

    [Fact]
    public void The_schema_offers_exactly_the_vocabulary()
    {
        using var schema = JsonDocument.Parse(AgentSession.Schema);
        var item = schema.RootElement.GetProperty("properties").GetProperty("actions").GetProperty("items").GetProperty("properties");
        Assert.Equal(AgentVocabulary.Actions, item.GetProperty("action").GetProperty("enum").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal(AgentVocabulary.Targets, item.GetProperty("target").GetProperty("enum").EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public void Paddles_match_the_controller_catalog()
    {
        using var catalog = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data/catalog.json")));
        foreach (var model in catalog.RootElement.EnumerateArray())
        {
            string id = model.GetProperty("id").GetString()!;
            var slots = model.TryGetProperty("macroSlots", out var s) ? s.EnumerateArray().Select(e => e.GetString()!).ToArray() : [];
            Assert.True(slots.SequenceEqual(AgentVocabulary.Paddles(id)), $"{id}: catalog {string.Join(",", slots)} vs agent {string.Join(",", AgentVocabulary.Paddles(id))}");
        }
    }

    sealed class ScriptedModel(params string[] replies) : IAgentModel
    {
        int next;
        public string Name => "scripted";
        public List<IReadOnlyList<ChatMessage>> Seen { get; } = new();
        public Task<ModelReply> Chat(IReadOnlyList<ChatMessage> messages, string schema, CancellationToken cancel)
        { Seen.Add(messages.ToList()); return Task.FromResult(new ModelReply(replies[next++], 10, 20, 0, 100)); }
    }

    [Fact]
    public async Task Broken_JSON_gets_one_retry_and_both_attempts_are_kept()
    {
        var model = new ScriptedModel("{oops", Reply(Act("stick_deadzone", "LeftStick", "4")));
        var turn = await new AgentSession(model, "x20_pro", "X20 Pro").Ask("less drift please", new PcModeProfile());
        Assert.Equal(2, turn.Attempts.Count);
        Assert.True(turn.Verdict.Valid);
        Assert.Equal(4, turn.Outcome.Next!.Sticks["LeftStick"].Inner);
    }

    [Fact]
    public async Task A_rejected_reply_gets_one_second_chance_with_the_validators_reasons()
    {
        var model = new ScriptedModel(Reply(Act("paddle", "M5", "\"A\"")), Reply());
        var turn = await new AgentSession(model, "x20", "X20").Ask("jump on M5", new PcModeProfile());
        Assert.Equal(2, turn.Attempts.Count);
        Assert.Contains("This controller has no M5", model.Seen[1][^1].Content);
        Assert.True(turn.Verdict.Valid); // the second answer changes nothing, which is allowed
        Assert.Empty(turn.Outcome.Changes);
    }

    [Fact]
    public async Task A_second_wrong_answer_stands_and_changes_nothing()
    {
        string bad = Reply(Act("paddle", "M5", "\"A\""));
        var turn = await new AgentSession(new ScriptedModel(bad, bad), "x20", "X20").Ask("jump on M5", new PcModeProfile());
        Assert.Equal(2, turn.Attempts.Count);
        Assert.False(turn.Verdict.Valid);
        Assert.Null(turn.Outcome.Next);
    }

    [Fact]
    public void A_paddle_this_model_lacks_is_named_whatever_the_action()
    {
        var verdict = ProposalValidator.Check(Reply(Act("remap", "M5", "\"DPAD_UP\"")), "x20"); // seen live, 12 Oct
        Assert.Contains("no M5", verdict.Checks[0].Message);
        verdict = ProposalValidator.Check(Reply(Act("remap", "DPAD_UP", "\"M5\"")), "x20"); // the other way round, also live
        Assert.Contains("no M5", verdict.Checks[0].Message);
        verdict = ProposalValidator.Check(Reply(Act("remap", "M5", "\"A\"")), "x20_pro");
        Assert.Contains("\"action\":\"paddle\"", verdict.Checks[0].Message);
    }

    [Fact]
    public void The_schema_keeps_numbers_whole()
    {
        using var schema = JsonDocument.Parse(AgentSession.Schema);
        var value = schema.RootElement.GetProperty("properties").GetProperty("actions").GetProperty("items").GetProperty("properties").GetProperty("value");
        Assert.Equal(["integer", "string"], value.GetProperty("type").EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public void The_bounded_schema_supports_axis_ratios_and_custom_point_text()
    {
        using var schema = JsonDocument.Parse(AgentSession.BoundedSchema);
        var options = schema.RootElement.GetProperty("properties").GetProperty("actions").GetProperty("items").GetProperty("properties").GetProperty("value").GetProperty("anyOf");
        Assert.Equal(150, options[0].GetProperty("maximum").GetInt32());
        Assert.Equal("string", options[1].GetProperty("type").GetString());
        Assert.False(options[1].TryGetProperty("enum", out _)); // points, on/off and page names use text
        Assert.Contains("\"recap\"", AgentSession.BoundedSchema);
    }

    [Fact]
    public async Task The_prompt_carries_the_current_settings_and_recent_conversation()
    {
        var model = new ScriptedModel(Reply(), Reply());
        var session = new AgentSession(model, "x20_pro", "X20 Pro");
        var current = new PcModeProfile { Turbo = { ["X"] = 10 } };
        await session.Ask("first", current); await session.Ask("second", current);
        var second = model.Seen[1];
        Assert.Contains("X turbo: 10/s", second[0].Content);
        Assert.Equal(["system", "user", "assistant", "user"], second.Select(m => m.Role));
    }

    [Fact]
    public void The_bench_scores_what_the_sentence_asked_for()
    {
        var swap = new BenchCase("Swap A and B", "x20_pro", ["remap:A=B"], ["remap:B=A"]);
        Assert.True(AgentBench.Matches(swap, ProposalValidator.Check(Reply(Act("remap", "A", "\"B\""), Act("remap", "B", "\"A\"")), "x20_pro")));
        Assert.False(AgentBench.Matches(swap, ProposalValidator.Check(Reply(Act("remap", "A", "\"B\"")), "x20_pro"))); // half a swap
        var drift = new BenchCase("drift", "x20_pro", ["stick_deadzone:RightStick"]);
        Assert.True(AgentBench.Matches(drift, ProposalValidator.Check(Reply(Act("stick_deadzone", "RightStick", "5")), "x20_pro")));
        Assert.False(AgentBench.Matches(drift, ProposalValidator.Check(Reply(Act("stick_deadzone", "LeftStick", "5")), "x20_pro")));
        Assert.False(AgentBench.Matches(drift, ProposalValidator.Check(Reply(Act("stick_deadzone", "RightStick", "90")), "x20_pro"))); // right idea, rejected
        var decline = new BenchCase("RGB red", "x20_pro");
        Assert.True(AgentBench.Matches(decline, ProposalValidator.Check(Reply(), "x20_pro")));
        Assert.False(AgentBench.Matches(decline, ProposalValidator.Check(Reply(Act("reset", "All", "0")), "x20_pro")));
        Assert.False(AgentBench.Matches(decline, ProposalValidator.Check("```json\n" + Reply() + "\n```", "x20_pro"))); // fenced: not JSON
    }

    [Fact]
    public async Task The_bench_keeps_the_first_reply_and_the_repaired_turn_apart()
    {
        var model = new ScriptedModel("not json", Reply(Act("turbo", "X", "12")));
        var c = AgentBench.Cases.First(x => x.Sentence.StartsWith("Turbo on X"));
        var run = BenchRun.Of(AgentBench.Variants[0], c, 1, await new AgentSession(model, c.ModelId, "X20 Pro").Ask(c.Sentence, new PcModeProfile()));
        Assert.False(run.FirstJson); Assert.False(run.FirstRules);
        Assert.True(run.FinalRules); Assert.True(run.Correct);
        Assert.Equal(2, run.Attempts); Assert.Equal(200, run.TotalMs); Assert.Equal(40, run.OutputTokens);
        string report = AgentBench.Report([run], "scripted", 1);
        Assert.Contains("| T 0.2 · schema | 0% | 0% | 0% | 100% | 100% | **100%** |", report);
    }

    [Fact]
    public void Every_bench_case_names_real_actions_and_targets()
    {
        foreach (var c in AgentBench.Cases)
            foreach (var need in c.Needs.SelectMany(g => g))
            {
                var parts = need.Split('=')[0].Split(':');
                Assert.Contains(parts[0], AgentVocabulary.Actions);
                Assert.Contains(parts[1], AgentVocabulary.Targets);
            }
        Assert.Equal(27, AgentBench.Cases.Length);
    }

    [Fact]
    public void Every_action_has_a_meaning_and_the_prompt_carries_them()
    {
        Assert.Equal(AgentVocabulary.Actions.Order(), AgentKnowledge.Settings.Select(m => m.Action).Order());
        string prompt = AgentSession.DefaultPrompt("X20 Pro", "x20_pro");
        Assert.Contains("Agent XCTL", prompt);
        foreach (var m in AgentKnowledge.Settings.Where(m => m.Action != AgentVocabulary.Reset)) Assert.Contains($"{m.Name} ({m.Action})", prompt);
        foreach (var preset in CurveDraft.Presets.Keys) Assert.Contains(AgentKnowledge.Preset(preset), prompt);
    }

    [Fact]
    public void The_walkthrough_stops_once_per_control_with_each_settings_meaning()
    {
        var verdict = ProposalValidator.Check(Reply(Act("stick_curve", "RightStick", "\"slow\""), Act("stick_deadzone", "RightStick", "5"), Act("trigger_full_at", "RightTrigger", "40"), Act("turbo", "X", "12"), Act("remap", "LB", "\"RB\"")), "x20_pro");
        var after = AgentApplier.Apply(new PcModeProfile(), verdict).Next!;
        var tour = AgentKnowledge.Tour(new PcModeProfile(), after);
        Assert.Equal(["RightStick", "RightTrigger", "LB", "X"], tour.Select(s => s.Target));
        Assert.Equal([TourKind.Stick, TourKind.Trigger, TourKind.Button, TourKind.Button], tour.Select(s => s.Kind));
        Assert.Equal(["stick_deadzone", "stick_curve"], tour[0].Rows.Select(r => r.Meaning.Action));
        Assert.Equal("trigger_full_at", tour[1].Rows.Single().Meaning.Action);
        Assert.Equal("remap", tour[2].Rows.Single().Meaning.Action);
        Assert.Equal("turbo", tour[3].Rows.Single().Meaning.Action);
        Assert.Empty(AgentKnowledge.Tour(after, after));
    }

    sealed class FakeMeter(double peak) : IDriftMeter
    {
        public List<string> Asked { get; } = new();
        public Task<DriftReport> Measure(IReadOnlyList<string> sticks, CancellationToken cancel)
        { Asked.AddRange(sticks); return Task.FromResult(new DriftReport(sticks.Select(s => new StickDrift(s, 1.2, -0.4, peak, 600)).ToList(), null)); }
    }

    [Fact]
    public void A_drift_reading_keeps_the_furthest_point_and_notices_a_thumb()
    {
        var s = new DriftSampler("RightStick");
        for (int i = 0; i < 99; i++) s.Add(500, -250);
        s.Add(1000, -500); // the furthest it wandered: 3.4% from the centre, while it rests around 1.5%
        var r = s.Result();
        Assert.Equal(1.5, r.RestX); Assert.Equal(-0.8, r.RestY); Assert.Equal(3.4, r.Peak); Assert.Equal(100, r.Samples);
        Assert.Equal(5, r.Suggested); // the smallest whole dead zone past 3.4, plus one to spare
        Assert.False(s.Touched);
        s.Add(12000, 0); Assert.True(s.Touched);
        Assert.Equal(0, new StickDrift("LeftStick", 0, 0, .6, 10).Suggested);   // clean: nothing to hide
        Assert.Equal(40, new StickDrift("LeftStick", 0, 0, 45, 10).Suggested);  // never past the dead zone's range
    }

    [Fact]
    public async Task Drift_is_measured_first_and_the_dead_zone_must_hide_what_was_measured()
    {
        var meter = new FakeMeter(3.4);
        var model = new ScriptedModel(
            Reply(Act("measure_drift", "RightStick", "0")),
            Reply(Act("stick_deadzone", "RightStick", "3")),   // under the measured 3.4%: rejected
            Reply(Act("stick_deadzone", "RightStick", "5")));
        var turn = await new AgentSession(model, "x20_pro", "X20 Pro") { Meter = meter }.Ask("my right stick drifts", new PcModeProfile());
        Assert.Equal(["RightStick"], meter.Asked);
        Assert.Contains("wandered up to 3.4%", model.Seen[1][^1].Content);
        Assert.Contains("Use at least 5", model.Seen[2][^1].Content);
        Assert.Equal(3, turn.Attempts.Count);
        Assert.True(turn.Verdict.Valid);
        Assert.Equal(5, turn.Outcome.Next!.Sticks["RightStick"].Inner);
        Assert.Equal(3.4, turn.Measured!.For("RightStick")!.Peak);
    }

    [Fact]
    public async Task Without_a_meter_the_model_is_told_it_cannot_measure()
    {
        var model = new ScriptedModel(Reply(Act("measure_drift", "LeftStick", "0")), Reply());
        var turn = await new AgentSession(model, "x20_pro", "X20 Pro").Ask("left stick drifts", new PcModeProfile());
        Assert.Contains("couldn't measure", model.Seen[1][^1].Content);
        Assert.NotNull(turn.Measured!.Problem);
        Assert.Empty(turn.Outcome.Changes);
        Assert.False(ProposalValidator.Check(Reply(Act("measure_drift", "A", "0")), "x20_pro").Valid); // sticks only
    }

    [Fact]
    public void PC_mode_is_switched_by_its_own_action_and_changes_no_setting()
    {
        var on = AgentApplier.Apply(new PcModeProfile(), ProposalValidator.Check(Reply(Act("pc_mode", "PcMode", "\"on\"")), "x20_pro"));
        Assert.True(on.PcMode); Assert.Empty(on.Changes); Assert.NotNull(on.Next);
        var both = AgentApplier.Apply(new PcModeProfile(), ProposalValidator.Check(Reply(Act("pc_mode", "PcMode", "\"off\""), Act("turbo", "X", "12")), "x20_pro"));
        Assert.False(both.PcMode); Assert.Single(both.Changes);
        Assert.Null(AgentApplier.Apply(new PcModeProfile(), ProposalValidator.Check(Reply(Act("turbo", "X", "12")), "x20_pro")).PcMode);
        Assert.False(ProposalValidator.Check(Reply(Act("pc_mode", "PcMode", "\"maybe\"")), "x20_pro").Valid);
        Assert.False(ProposalValidator.Check(Reply(Act("pc_mode", "A", "\"on\"")), "x20_pro").Valid);
    }

    [Fact]
    public void Curve_meanings_come_from_the_response_PC_mode_runs()
    {
        Assert.Equal((50, 75), AgentKnowledge.Shape("default"));
        Assert.True(AgentKnowledge.Shape("slow").Half < 45);   // the sniper's curve: less output until the edge
        Assert.True(AgentKnowledge.Shape("quick").Half > 55);
        Assert.True(AgentKnowledge.Shape("fine").Half > 50);   // "fine" is slightly quick, not precise (found 12 Oct)
    }
}
