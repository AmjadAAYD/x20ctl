using X20Ctl.Zone.Agent;
using X20Ctl.Zone.PcMode;
using Xunit.Abstractions;
namespace X20Ctl.Zone.Tests;

/// <summary>Offline mode: the phrasebook agent, scored on its evaluation sentences, and on sentences it has never seen.</summary>
public sealed class AgentOfflineTests(ITestOutputHelper output)
{
    async Task<(int Right, int Total)> Score(string name, IEnumerable<BenchCase> cases, OfflineAgent? agent = null)
    {
        int right = 0, total = 0;
        foreach (var c in cases)
        {
            var turn = await new AgentSession(agent ?? new OfflineAgent(), c.ModelId, "X20 Pro") { Meter = new BenchMeter() }.Ask(c.Sentence, new PcModeProfile());
            bool ok = AgentBench.Matches(c, turn.Verdict); right += ok ? 1 : 0; total++;
            if (!ok) output.WriteLine($"{name} MISS \"{c.Sentence}\" expected {c.Expected}; got: {turn.Reply.Content}");
        }
        output.WriteLine($"{name}: {right}/{total}");
        return (right, total);
    }

    [Fact]
    public async Task Offline_mode_answers_the_evaluation_sentences()
    {
        var (right, total) = await Score("bench", AgentBench.Cases.Concat(AgentBench.DriftCases).Concat(AgentBench.M3Cases));
        Assert.True(right >= total * 0.95, $"{right}/{total}");
    }

    [Fact]
    public async Task Offline_mode_handles_wording_it_has_never_seen()
    {
        var (right, total) = await Score("held out", AgentBench.OfflineHeldOut);
        // measured 13 Oct: 16/20 with the hand-written examples alone, 15/20 with the raw phrasebook and single-nearest
        // matching, 16/20 with the phrasebook and weighted voting. A guard against getting worse, not a target met.
        Assert.True(right >= total * 0.8, $"{right}/{total}");
    }

    [Fact]
    public async Task Phrasebooks_compared_on_held_out_wording()
    {
        await Score("examples only", AgentBench.OfflineHeldOut, new OfflineAgent(null));
        // v1: the first prompt; v2: the stricter one (similar requests named as off limits, what each sentence must contain)
        foreach (var version in new[] { "phrasebook-raw.json", "phrasebook-v2-raw.json" })
        {
            string file = Path.Combine(Repo(), "docs", "agent", version);
            if (!File.Exists(file)) continue;
            var raw = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string[]>>(File.ReadAllText(file))!;
            var (cleaned, dropped) = PhrasebookCleaner.Clean(raw);
            output.WriteLine($"{version}: {raw.Values.Sum(v => v.Length)} sentences, cleaned {cleaned.Values.Sum(v => v.Length)}, dropped {dropped.Count}: " +
                string.Join(", ", dropped.GroupBy(d => d.Why.StartsWith("closer") ? "closer to another intent" : d.Why).Select(g => $"{g.Key} {g.Count()}")));
            await Score(version + " raw", AgentBench.OfflineHeldOut, new OfflineAgent(raw));
            await Score(version + " cleaned", AgentBench.OfflineHeldOut, new OfflineAgent(cleaned));
        }
        var both = new[] { "phrasebook-raw.json", "phrasebook-v2-raw.json" }.Select(v => Path.Combine(Repo(), "docs", "agent", v)).Where(File.Exists)
            .Select(f => PhrasebookCleaner.Clean(System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string[]>>(File.ReadAllText(f))!).Kept)
            .SelectMany(d => d).GroupBy(e => e.Key).ToDictionary(g => g.Key, g => g.SelectMany(e => e.Value).Distinct().ToArray());
        await Score("both cleaned", AgentBench.OfflineHeldOut, new OfflineAgent(both));
        // OfflineFinal was evaluated once. Its archived results stay in offline-mode.md;
        // routine regression checks and phrasebook iteration use the development set only.
    }
    static string Repo() { var d = new DirectoryInfo(AppContext.BaseDirectory); while (d != null && !Directory.Exists(Path.Combine(d.FullName, "docs"))) d = d.Parent; return d!.FullName; }

    [Fact]
    public async Task Offline_mode_measures_drift_before_it_sets_a_dead_zone()
    {
        var turn = await new AgentSession(new OfflineAgent(), "x20_pro", "X20 Pro") { Meter = new BenchMeter() }.Ask("my right stick drifts", new PcModeProfile());
        Assert.Equal(2, turn.Attempts.Count);
        Assert.Equal(8, turn.Outcome.Next!.Sticks["RightStick"].Inner); // 6.4% measured: 8 hides it
        Assert.Contains("6.4", turn.Verdict.Proposal!.Recap);
    }

    [Fact]
    public async Task Offline_mode_says_so_when_it_does_not_understand()
    {
        var turn = await new AgentSession(new OfflineAgent(), "x20_pro", "X20 Pro").Ask("banana telescope", new PcModeProfile());
        Assert.True(turn.Verdict.Valid);
        Assert.Empty(turn.Verdict.Proposal!.Actions);
        Assert.StartsWith(AgentCharacter.NotUnderstood, turn.Verdict.Proposal.Speech);
    }
}
