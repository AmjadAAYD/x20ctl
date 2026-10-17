using System.Globalization;
using System.Text;
using System.Text.Json;
using X20Ctl.Zone.PcMode;
namespace X20Ctl.Zone.Agent;

/// <summary>
/// One sentence the agent is measured on, and what a right answer contains. Each group in <see cref="Needs"/> must be met by
/// at least one action; a group lists its alternatives as "action:target" or "action:target=value". No groups means the
/// right answer is to decline: a valid reply with no actions.
/// </summary>
public sealed record BenchCase(string Sentence, string ModelId, params string[][] Needs)
{
    public bool Decline => Needs.Length == 0;
    /// <summary>A question ("What…", "Why…"): right means no actions and an answer in the recap.</summary>
    public bool Question => Decline && (Sentence.StartsWith("What") || Sentence.StartsWith("Why"));
    public string Expected => Question ? "answer, no actions" : Decline ? "decline" : string.Join(" + ", Needs.Select(g => string.Join(" or ", g)));
}

/// <summary>How a variant of the model's settings is run: temperature, and whether the reply is held to the JSON schema.</summary>
/// <summary>The bench's drift reading: no controller, the same fixed numbers every run (right stick wandering to 6.4%, left
/// to 4.2%), so a drift sentence is scored on whether the dead zone covers what was measured.</summary>
public sealed class BenchMeter : IDriftMeter
{
    public static readonly DriftReport Reading = new([new("LeftStick", 1.8, 0.9, 4.2, 3000), new("RightStick", 3.1, -2.4, 6.4, 3000)], null);
    public Task<DriftReport> Measure(IReadOnlyList<string> sticks, CancellationToken cancel) =>
        Task.FromResult(new DriftReport(Reading.Sticks.Where(d => sticks.Contains(d.Stick)).ToList(), null));
}

public sealed record BenchVariant(string Name, double Temperature, bool Schema, bool Bounded = false);

/// <summary>One sentence, one run: what the first attempt was worth, and where the whole turn (with its repair) ended.</summary>
public sealed record BenchRun(BenchVariant Variant, BenchCase Case, int Run, bool FirstJson, bool FirstStructure, bool FirstRules, bool FinalRules, bool Correct, int Attempts, double TotalMs, int OutputTokens, int PromptTokens, string FirstOutput)
{
    public static BenchRun Of(BenchVariant variant, BenchCase c, int run, AgentTurn turn)
    {
        var first = turn.Attempts[0];
        var firstVerdict = ProposalValidator.Check(first.Content, c.ModelId, offered: turn.Docs.Count);
        return new(variant, c, run, AgentBench.IsJson(first.Content), firstVerdict.StructureError == null, firstVerdict.Valid, turn.Verdict.Valid, AgentBench.Matches(c, turn.Verdict) && (!AgentBench.DriftCases.Contains(c) || turn.Measured is { Problem: null }),
            turn.Attempts.Count, turn.Attempts.Sum(a => a.TotalMs), turn.Attempts.Sum(a => a.OutputTokens), turn.Attempts.Sum(a => a.PromptTokens), first.Content);
    }
}

/// <summary>
/// The agent's evaluation: a fixed set of sentences, each with its right answer, run through the real
/// session (prompt, model, validator, one repair) under each variant. The scoring is the app's own validator plus the
/// expected actions; nothing is judged by another model.
/// </summary>
public static class AgentBench
{
    /// <summary>A one-pass refresh can be requested without changing the default three-run study.</summary>
    public static int EvaluationRepeats => int.TryParse(Environment.GetEnvironmentVariable("X20CTL_AGENT_EVAL_REPEATS"), out int n) && n is >= 1 and <= 3 ? n : 3;
    /// <summary>The drift sentences (M3), scored with <see cref="BenchMeter"/>: right means it measured first and the dead zone
    /// covers what was measured (the validator refuses less), on the stick the sentence names, or both when it names neither.</summary>
    public static readonly BenchCase[] DriftCases =
    [
        new("My right stick drifts a little", "x20_pro", ["stick_deadzone:RightStick"]),
        new("My left stick moves on its own", "x20_pro", ["stick_deadzone:LeftStick"]),
        new("Fix my stick drift", "x20_pro", ["stick_deadzone:LeftStick"], ["stick_deadzone:RightStick"]),
        new("My camera keeps slowly turning by itself", "x20_pro", ["stick_deadzone:RightStick"]),
        new("Check my sticks for drift", "x20_pro", ["stick_deadzone:LeftStick"], ["stick_deadzone:RightStick"]),
    ];

    /// <summary>M3's other sentences: resetting, turbo off, and PC mode on and off.</summary>
    public static readonly BenchCase[] M3Cases =
    [
        new("Reset everything", "x20_pro", ["reset:All"]),
        new("Put all my settings back to default", "x20_pro", ["reset:All"]),
        new("Turn off turbo on X", "x20_pro", ["turbo:X=0", "reset:X"]),
        new("Stop the turbo on RB", "x20_pro", ["turbo:RB=0", "reset:RB"]),
        new("Turn on PC mode", "x20_pro", ["pc_mode:PcMode=on"]),
        new("My settings don't do anything in games", "x20_pro", ["pc_mode:PcMode=on"]),
        new("Switch PC mode off", "x20_pro", ["pc_mode:PcMode=off"]),
        new("Make my sticks smooth and turn PC mode on", "x20_pro", ["pc_mode:PcMode=on"], ["stick_curve:LeftStick=smooth"], ["stick_curve:RightStick=smooth"]),
    ];

    public static readonly BenchCase[] CustomizationCases =
    [
        new("Make my right stick slightly slower for aiming", "x20_pro", ["stick_curve_points:RightStick=gentle_slow"]),
        new("Give my right stick a custom curve through 30,25,70,65", "x20_pro", ["stick_curve_points:RightStick=30,25,70,65"]),
        new("Cap right stick output at 90 percent", "x20_pro", ["stick_max_output:RightStick=90"]),
        new("Set right stick vertical axis ratio to 120 percent", "x20_pro", ["stick_axis_ratio:RightStick=120"]),
        new("Turn square gate on for the left stick", "x20_pro", ["stick_square_gate:LeftStick=on"]),
        new("Turn mouse mode on", "x20_pro", ["mouse_mode:Mouse=on"]),
        new("Switch mouse mode off", "x20_pro", ["mouse_mode:Mouse=off"]),
        new("Open diagnostics", "x20_pro", ["open_page:Page=Diagnostics"]),
        new("Show curves", "x20_pro", ["open_page:Page=Curves"]),
        new("Take me to macros", "x20_pro", ["open_page:Page=Macros"]),
    ];

    /// <summary>Offline mode's held-out sentences: never in its seeds, and kept out of the phrasebook, so they measure
    /// how it handles wording it has never seen.</summary>
    public static readonly BenchCase[] OfflineHeldOut =
    [
        new("can u make my right analog better for long shots", "x20_pro", ["stick_curve:RightStick=slow"]),
        new("my crosshair is way too shaky", "x20_pro", ["stick_curve:RightStick=slow"]),
        new("smoother sticks pls", "x20_pro", ["stick_curve:LeftStick=smooth"], ["stick_curve:RightStick=smooth"]),
        new("left stick deadzone 7", "x20_pro", ["stick_deadzone:LeftStick=7"]),
        new("my left thumbstick is drifting", "x20_pro", ["stick_deadzone:LeftStick"]),
        new("the camera spins slowly when i'm not touching anything", "x20_pro", ["stick_deadzone:RightStick"]),
        new("i want hair triggers", "x20_pro", ["trigger_full_at:RightTrigger", "trigger_deadzone:RightTrigger"]),
        new("set up my triggers for forza", "x20_pro", ["trigger_curve:RightTrigger=slow", "trigger_curve:LeftTrigger=slow"]),
        new("rapid fire on Y at 8", "x20_pro", ["turbo:Y=8"]),
        new("turn turbo off on Y", "x20_pro", ["turbo:Y=0", "reset:Y"]),
        new("swap X and Y", "x20_pro", ["remap:X=Y"], ["remap:Y=X"]),
        new("make RB do what LB does", "x20_pro", ["remap:RB=LB"]),
        new("jump on paddle 2", "x20_pro", ["paddle:M2=A"]),
        new("reset my left stick", "x20_pro", ["reset:LeftStick"]),
        new("go back to the default settings for everything", "x20_pro", ["reset:All"]),
        new("enable pc mode please", "x20_pro", ["pc_mode:PcMode=on"]),
        new("my changes aren't showing up in game", "x20_pro", ["pc_mode:PcMode=on"]),
        new("change the lights to blue", "x20_pro"),
        new("set the right stick dead zone to 75", "x20_pro"),
        new("What does the anti dead zone do?", "x20_pro"),
        // added 13 Oct, before the phrasebook was cleaned or regenerated
        new("i keep missing my shots at range in warzone", "x20_pro", ["stick_curve:RightStick=slow"]),
        new("aim is way too jumpy", "x20_pro", ["stick_curve:RightStick=slow"]),
        new("i want butter smooth analogs", "x20_pro", ["stick_curve:LeftStick=smooth"], ["stick_curve:RightStick=smooth"]),
        new("turning feels slow, speed it up", "x20_pro", ["stick_curve:RightStick=quick", "stick_curve:LeftStick=quick"]),
        new("put my right stick on the fine curve", "x20_pro", ["stick_curve:RightStick=fine"]),
        new("my guy keeps walking forward on his own", "x20_pro", ["stick_deadzone:LeftStick"]),
        new("is my controller drifting? check it", "x20_pro", ["stick_deadzone:LeftStick", "stick_deadzone:RightStick"]),
        new("ghost inputs on my right analog", "x20_pro", ["stick_deadzone:RightStick"]),
        new("right stick deadzone 4 percent", "x20_pro", ["stick_deadzone:RightStick=4"]),
        new("get rid of the deadzone on my left stick", "x20_pro", ["stick_deadzone:LeftStick=0"]),
        new("left stick max speed at 90", "x20_pro", ["stick_full_at:LeftStick=90"]),
        new("tiny aim adjustments don't register in apex", "x20_pro", ["stick_anti_deadzone:RightStick", "stick_anti_deadzone:LeftStick"]),
        new("shorter trigger pull for halo", "x20_pro", ["trigger_full_at:RightTrigger", "trigger_deadzone:RightTrigger"]),
        new("make my right trigger super sensitive", "x20_pro", ["trigger_full_at:RightTrigger", "trigger_deadzone:RightTrigger"]),
        new("gas pedal feels on off, make it gradual", "x20_pro", ["trigger_curve:RightTrigger=slow", "trigger_curve:LeftTrigger=slow"]),
        new("left trigger dead zone 6", "x20_pro", ["trigger_deadzone:LeftTrigger=6"]),
        new("no deadzone on the triggers at all", "x20_pro", ["trigger_deadzone:LeftTrigger=0"], ["trigger_deadzone:RightTrigger=0"]),
        new("right trigger full press at 35", "x20_pro", ["trigger_full_at:RightTrigger=35"]),
        new("quick curve on my right trigger", "x20_pro", ["trigger_curve:RightTrigger=quick"]),
        new("spam A for me", "x20_pro", ["turbo:A"]),
        new("turbo on LB 20 per second", "x20_pro", ["turbo:LB=20"]),
        new("kill the turbo on A", "x20_pro", ["turbo:A=0", "reset:A"]),
        new("switch around A and X", "x20_pro", ["remap:A=X"], ["remap:X=A"]),
        new("can you swap the bumpers", "x20_pro", ["remap:LB=RB"], ["remap:RB=LB"]),
        new("make Y work as X", "x20_pro", ["remap:Y=X"]),
        new("map B onto A", "x20_pro", ["remap:B=A"]),
        new("put jump on my back paddle M3", "x20_pro", ["paddle:M3=A"]),
        new("M4 should be B", "x20_pro", ["paddle:M4=B"]),
        new("reset the right trigger to normal", "x20_pro", ["reset:RightTrigger"]),
        new("undo all my changes", "x20_pro", ["reset:All"]),
        new("wipe every setting", "x20_pro", ["reset:All"]),
        new("pc mode on", "x20_pro", ["pc_mode:PcMode=on"]),
        new("games don't see my settings", "x20_pro", ["pc_mode:PcMode=on"]),
        new("turn pc mode off please", "x20_pro", ["pc_mode:PcMode=off"]),
        new("make my sticks smooth and turn on pc mode", "x20_pro", ["stick_curve:LeftStick=smooth"], ["pc_mode:PcMode=on"]),
        new("can you make my rumble weaker", "x20_pro"),
        new("set the right stick full speed to 40", "x20_pro"),
        new("turbo on X at 90 a second", "x20_pro"),
        new("why would i lower full at on a trigger?", "x20_pro"),
        new("what's the point of a stick curve", "x20_pro"),
    ];

    /// <summary>Offline mode's final test (13 Oct): written after the matcher and the phrasebook were chosen on
    /// <see cref="OfflineHeldOut"/> (which, once looked at, became the development set), and scored once.</summary>
    public static readonly BenchCase[] OfflineFinal =
    [
        new("my right analog creeps upward when i let go", "x20_pro", ["stick_deadzone:RightStick"]),
        new("help me hit headshots from far away", "x20_pro", ["stick_curve:RightStick=slow"]),
        new("sticks feel jerky, smooth them", "x20_pro", ["stick_curve:LeftStick=smooth"], ["stick_curve:RightStick=smooth"]),
        new("make the right stick snappier", "x20_pro", ["stick_curve:RightStick=quick"]),
        new("left stick dead zone at 5", "x20_pro", ["stick_deadzone:LeftStick=5"]),
        new("right stick should hit max at 80", "x20_pro", ["stick_full_at:RightStick=80"]),
        new("i want instant triggers for valorant", "x20_pro", ["trigger_full_at:RightTrigger", "trigger_deadzone:RightTrigger"]),
        new("smoother braking in gran turismo", "x20_pro", ["trigger_curve:LeftTrigger=slow", "trigger_curve:RightTrigger=slow"]),
        new("set the left trigger dead zone to 4", "x20_pro", ["trigger_deadzone:LeftTrigger=4"]),
        new("rapid fire on RB at 15", "x20_pro", ["turbo:RB=15"]),
        new("stop turbo on Y", "x20_pro", ["turbo:Y=0", "reset:Y"]),
        new("swap Y and B", "x20_pro", ["remap:Y=B"], ["remap:B=Y"]),
        new("make LB send X", "x20_pro", ["remap:LB=X"]),
        new("put jump on paddle M6", "x20_pro", ["paddle:M6=A"]),
        new("reset the B button", "x20_pro", ["reset:B"]),
        new("set everything back to how it was", "x20_pro", ["reset:All"]),
        new("enable pc mode", "x20_pro", ["pc_mode:PcMode=on"]),
        new("disable pc mode", "x20_pro", ["pc_mode:PcMode=off"]),
        new("change the led colour", "x20_pro"),
        new("left trigger full at 5", "x20_pro"),
    ];

    public static readonly BenchCase[] Cases =
    [
        new("Make my sticks smooth", "x20_pro", ["stick_curve:LeftStick=smooth"], ["stick_curve:RightStick=smooth"]),
        new("Make my right stick more precise for sniping", "x20_pro", ["stick_curve:RightStick=slow"]),
        new("My aim feels too twitchy", "x20_pro", ["stick_curve:RightStick=slow", "stick_curve:RightStick=smooth"]),
        new("My right stick drifts a little", "x20_pro", ["stick_deadzone:RightStick"]),
        new("My left stick moves on its own", "x20_pro", ["stick_deadzone:LeftStick"]),
        new("Set my left stick dead zone to 8", "x20_pro", ["stick_deadzone:LeftStick=8"]),
        new("Right stick full speed at 85", "x20_pro", ["stick_full_at:RightStick=85"]),
        new("Small stick movements don't register in my game", "x20_pro", ["stick_anti_deadzone:LeftStick", "stick_anti_deadzone:RightStick"]),
        new("I want faster triggers for shooting", "x20_pro", ["trigger_full_at:RightTrigger", "trigger_deadzone:RightTrigger", "trigger_curve:RightTrigger=quick"]),
        new("Hair triggers for Call of Duty", "x20_pro", ["trigger_full_at:RightTrigger", "trigger_deadzone:RightTrigger"]),
        new("Slow, progressive triggers for racing", "x20_pro", ["trigger_curve:RightTrigger=slow", "trigger_curve:LeftTrigger=slow"]),
        new("Remove the dead zone on both triggers", "x20_pro", ["trigger_deadzone:LeftTrigger=0"], ["trigger_deadzone:RightTrigger=0"]),
        new("Turbo on X, 12 presses a second", "x20_pro", ["turbo:X=12"]),
        new("Turn off turbo on X", "x20_pro", ["turbo:X=0", "reset:X"]),
        new("Swap A and B", "x20_pro", ["remap:A=B"], ["remap:B=A"]),
        new("Make LB act like RB", "x20_pro", ["remap:LB=RB"]),
        new("Put jump on M1", "x20_pro", ["paddle:M1=A"]),
        new("Put jump on M5", "x20_pro", ["paddle:M5=A"]),
        new("Reset my right trigger", "x20_pro", ["reset:RightTrigger"]),
        new("Reset everything", "x20_pro", ["reset:All"]),
        new("Put jump on M5", "x20"),
        new("Set my left stick dead zone to 260", "x20_pro"),
        new("Turbo on RB at 50 a second", "x20_pro"),
        new("Can you change my RGB to red?", "x20_pro"),
        new("Make the vibration stronger", "x20_pro"),
        new("What's good and bad about hair triggers?", "x20_pro"),
        new("Why would I use an anti-dead zone?", "x20_pro"),
    ];

    public static readonly BenchVariant[] Variants =
    [
        new("T 0.2 · schema", 0.2, true),
        new("T 0.8 · schema", 0.8, true),
        new("T 0.2 · bounded schema", 0.2, true, true),
        new("T 0.8 · bounded schema", 0.8, true, true),
        new("T 0.2 · no schema", 0.2, false),
    ];

    public static bool IsJson(string text)
    {
        try { using var d = JsonDocument.Parse(text); return d.RootElement.ValueKind == JsonValueKind.Object; }
        catch (JsonException) { return false; }
    }

    /// <summary>Whether the reply did what the sentence asked: valid, and every group met (or, for a decline, no actions).</summary>
    public static bool Matches(BenchCase c, AgentVerdict verdict)
    {
        if (!verdict.Valid || verdict.Proposal == null) return false;
        var actions = verdict.Proposal.Actions;
        if (c.Decline) return actions.Count == 0 && (!c.Question || verdict.Proposal.Recap.Trim().Length > 0);
        return c.Needs.All(group => group.Any(need => actions.Any(a => Fits(need, a))));
        static bool Fits(string need, AgentAction a)
        {
            var (head, value) = need.Split('=') is [var h, var v] ? (h, v) : (need, null);
            if (head == a.Action + ":" + a.Target && value == "gentle_slow" && CurvePoints.TryParse(a.ValueText, out var gentle, out _))
            {
                double response = PadProcessor.Through(.5, gentle.X1 / 100d, gentle.Y1 / 100d, gentle.X2 / 100d, gentle.Y2 / 100d);
                return response < .5 && response >= .445;
            }
            // Compare the requested response, not a preset name: custom curves are now preferred.
            // Archived reports used exact preset matching and are not directly comparable.
            var parts = head.Split(':');
            if (parts.Length == 2 && parts[1] == a.Target && parts[0] is "stick_curve" or "trigger_curve"
                && a.Action == parts[0] + "_points" && CurvePoints.TryParse(a.ValueText, out var p, out _))
            {
                double Response(double x) => PadProcessor.Through(x, p.X1 / 100d, p.Y1 / 100d, p.X2 / 100d, p.Y2 / 100d);
                return value switch
                {
                    "slow" => Response(.25) < .25 && Response(.5) < .5 && Response(.75) < .75,
                    "quick" => Response(.25) > .25 && Response(.5) > .5 && Response(.75) > .75,
                    "smooth" => new[] { .25, .5, .75 }.All(x => Math.Abs(Response(x) - x) <= .15),
                    _ => false
                };
            }
            return head == a.Action + ":" + a.Target && (value == null || string.Equals(value, a.ValueText, StringComparison.OrdinalIgnoreCase));
        }
    }

    static double Median(IEnumerable<double> values)
    {
        var v = values.OrderBy(x => x).ToArray();
        return v.Length == 0 ? 0 : v.Length % 2 == 1 ? v[v.Length / 2] : (v[v.Length / 2 - 1] + v[v.Length / 2]) / 2;
    }

    /// <summary>The variant to make the default: most right answers, then most valid first attempts, then the quickest.</summary>
    public static BenchVariant Best(IReadOnlyList<BenchRun> runs) =>
        runs.GroupBy(r => r.Variant).OrderByDescending(g => g.Count(r => r.Correct)).ThenByDescending(g => g.Count(r => r.FirstRules)).ThenBy(g => Median(g.Select(r => r.TotalMs))).First().Key;

    /// <summary>The results as markdown: one row per variant, then each sentence's right answers per variant.</summary>
    public static string Report(IReadOnlyList<BenchRun> runs, string model, int repeats)
    {
        var c = CultureInfo.InvariantCulture;
        var variants = runs.Select(r => r.Variant).Distinct().ToList();
        string Pct(IEnumerable<BenchRun> g, Func<BenchRun, bool> f) { var a = g.ToList(); return a.Count == 0 ? "-" : $"{100.0 * a.Count(f) / a.Count:0}%"; }
        var s = new StringBuilder();
        s.AppendLine($"# Agent evaluation · {model}");
        s.AppendLine();
        s.AppendLine($"{runs.Select(r => r.Case).Distinct().Count()} sentences × {repeats} runs × {variants.Count} variants = {runs.Count} turns, run {DateTime.Now.ToString("d MMM yyyy HH:mm", c)}. Each turn goes through the app's real prompt, validator, measurement flow and applicable repair attempts. \"First\" columns score the model's first reply alone; \"Right answer\" scores the whole turn against what the sentence asked for. Multi-call includes drift's expected measurement follow-up, not only repairs.");
        s.AppendLine();
        s.AppendLine("| Variant | First: JSON | First: schema | First: rules | Multi-call | Final valid | Right answer | Median time | Median tokens out |");
        s.AppendLine("|---|---|---|---|---|---|---|---|---|");
        foreach (var v in variants)
        {
            var g = runs.Where(r => r.Variant == v).ToList();
            s.AppendLine($"| {v.Name} | {Pct(g, r => r.FirstJson)} | {Pct(g, r => r.FirstStructure)} | {Pct(g, r => r.FirstRules)} | {Pct(g, r => r.Attempts > 1)} | {Pct(g, r => r.FinalRules)} | **{Pct(g, r => r.Correct)}** | {(Median(g.Select(r => r.TotalMs)) / 1000).ToString("0.0", c)} s | {Median(g.Select(r => (double)r.OutputTokens)).ToString("0", c)} |");
        }
        var best = Best(runs);
        s.AppendLine();
        s.AppendLine($"**Top score: {best.Name}** (most right answers, then most valid first replies, then quickest). A one-turn lead is within run-to-run noise; read the failures before choosing a default.");
        s.AppendLine();
        s.AppendLine("## Right answers per sentence");
        s.AppendLine();
        s.AppendLine("| Sentence | Pad | Expected | " + string.Join(" | ", variants.Select(v => v.Name)) + " |");
        s.AppendLine("|---|---|---|" + string.Concat(variants.Select(_ => "---|")));
        foreach (var bc in runs.Select(r => r.Case).Distinct())
            s.AppendLine($"| {bc.Sentence} | {bc.ModelId} | `{bc.Expected}` | " + string.Join(" | ", variants.Select(v => { var g = runs.Where(r => r.Case == bc && r.Variant == v).ToList(); return $"{g.Count(r => r.Correct)}/{g.Count}"; })) + " |");
        return s.ToString();
    }
}
