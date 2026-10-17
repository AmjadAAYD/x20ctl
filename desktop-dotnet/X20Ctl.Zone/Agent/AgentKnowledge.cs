using System.Globalization;
using System.Text;
using X20Ctl.Zone.PcMode;
namespace X20Ctl.Zone.Agent;

/// <summary>What one setting means: what it does, what raising and lowering it does, what it is good for and what it costs.</summary>
public sealed record Meaning(string Action, string Name, string What, string More, string Less, string Good, string Careful);
public enum TourKind { Stick, Trigger, Button }
public sealed record TourRow(AgentChange Change, Meaning Meaning);
/// <summary>One stop of the walk-through: a control and everything that changed on it.</summary>
public sealed record TourStep(string Target, TourKind Kind, IReadOnlyList<TourRow> Rows);

/// <summary>
/// The meaning of every setting the agent can change (owner request, 12 Oct 2026: "each single feature has a meaning").
/// One source for both the model's prompt and the app's own explanations, so what the model is told and what the app shows
/// can't disagree. Curve shapes are described from <see cref="PadProcessor.Response"/>, the function PC mode really runs,
/// not from the presets' names: "fine" turned out to be slightly quick, not precise.
/// </summary>
public static class AgentKnowledge
{
    public static readonly Meaning[] Settings =
    [
        new(AgentVocabulary.StickDeadzone, "Stick dead zone", "the part of the stick's travel around the centre that is ignored",
            "hides drift: a stick that moves on its own, or aim that creeps", "small moves register sooner",
            "just above the drift, usually 3 to 8", "too high and fine aim feels sticky: the start of every move is lost"),
        new(AgentVocabulary.StickFullAt, "Stick full at", "how far the stick must be pushed to reach full speed",
            "100 uses the whole travel: the most control", "full speed with less push: faster turns, harder to control",
            "fast turning games at 80 to 90", "below 75 small corrections get jumpy; keep it at 100 for sniping"),
        new(AgentVocabulary.StickAntiDeadzone, "Stick anti-dead zone", "starts the output above zero, to cancel a game's own dead zone",
            "small moves get past the game's dead zone", "the game's own dead zone swallows small moves",
            "games where small moves do nothing, usually 10 to 20", "more than the game's dead zone and the aim creeps when barely touched"),
        new(AgentVocabulary.StickCurve, "Stick curve", "how output grows as the stick is pushed",
            "", "", "slow for sniping and precise aim; quick for fast turning; smooth for steady tracking", "a slow curve turns slower at the same push"),
        new(AgentVocabulary.StickCurvePoints, "Custom stick curve", "how output grows as the stick is pushed, shaped by two points the curve runs through",
            "", "", "a curve made for this owner and this controller rather than a preset", "a point far below the diagonal makes that part of the push feel dead"),
        new(AgentVocabulary.StickMaxOutput, "Stick max output", "the most the game gets at full push",
            "full speed at full push", "a ceiling on speed: calmer, slower turns",
            "lowering to 85 to 95 tames a game whose turning is too fast", "too low and full push never reaches full speed"),
        new(AgentVocabulary.StickAxisRatio, "Stick vertical ratio", "vertical output as a percent of horizontal",
            "faster up and down than side to side", "slower up and down: steadier vertical aim",
            "80 to 90 for shooters where looking up and down is too twitchy", "far from 100 and diagonal moves feel bent"),
        new(AgentVocabulary.StickSquareGate, "Stick square gate", "lets diagonals reach full output on both axes, like a square gate",
            "", "", "games that expect full speed on diagonals, and older games", "circle-gate games may feel faster on diagonals"),
        new(AgentVocabulary.TriggerDeadzone, "Trigger dead zone", "how far the trigger travels before it registers",
            "no accidental presses from a resting finger", "registers at the first touch: the quickest shot",
            "0 to 3 for shooters", "high values delay every press"),
        new(AgentVocabulary.TriggerFullAt, "Trigger full at", "how far the trigger must be pulled to count as fully pressed",
            "100 uses the whole pull: fine throttle and brake control", "a hair trigger: full press from a short pull",
            "30 to 50 for shooters; 100 for racing", "low values make throttle all or nothing"),
        new(AgentVocabulary.TriggerCurve, "Trigger curve", "how output grows as the trigger is pulled",
            "", "", "slow for racing throttle and brakes; quick for an early response", "a slow curve needs a deeper pull for the same output"),
        new(AgentVocabulary.Remap, "Remap", "a button sends a different button",
            "", "", "putting an action where it is comfortable, or swapping A and B for a Nintendo layout", "the game's on-screen prompts no longer match the buttons"),
        new(AgentVocabulary.Turbo, "Turbo", "holding the button repeats presses a set number of times a second",
            "more presses a second", "fewer presses a second",
            "semi-automatic weapons and mashing, usually 8 to 15", "too fast and some games miss presses; it breaks actions that need a hold"),
        new(AgentVocabulary.Paddle, "Paddle", "a rear paddle sends A or B",
            "", "", "jumping or dodging without taking a thumb off the right stick", "on this controller a paddle can only send A or B"),
        new(AgentVocabulary.MeasureDrift, "Drift reading", "reads where a stick rests with no hand on it, for three seconds, and how far it wanders",
            "", "", "finding the real drift before choosing a dead zone", "the stick must be left alone while it reads"),
        new(AgentVocabulary.PcMode, "PC mode", "switches PC mode on or off: games on this PC get the settings through a virtual Xbox controller",
            "", "", "making the settings reach games at all; nothing is written to the controller", "while it is off, games see the controller as it is, without these settings"),
        new(AgentVocabulary.TriggerCurvePoints, "Custom trigger curve", "how output grows as the trigger is pulled, shaped by two points the curve runs through",
            "", "", "a trigger response made for this owner's game rather than a preset", "a point far below the diagonal makes the first part of the pull feel dead"),
        new(AgentVocabulary.MouseMode, "Mouse mode", "the controller moves the mouse pointer: left stick moves it, A clicks",
            "", "", "using the desktop or a launcher from the couch", "while it is on, the controller drives the pointer instead of the app"),
        new(AgentVocabulary.OpenPage, "Open a page", "takes the owner to a page of the app: Diagnostics, Tools, Mouse, Curves, Buttons, Macros, Vibration, PC mode or the controller overview",
            "", "", "testing the controller (Diagnostics) or anything better done by hand", "the agent window steps aside"),
        new(AgentVocabulary.Reset, "Reset", "puts a stick, trigger or button back to its default", "", "", "starting over", "the change is undone"),
    ];

    public static Meaning Of(string action) => Settings.First(m => m.Action == action);

    /// <summary>A curve preset in numbers: what half and three-quarter pushes give, from the function PC mode runs.</summary>
    public static (int Half, int ThreeQuarter) Shape(string preset)
    {
        var (first, second) = CurveDraft.Presets[preset];
        var curve = new SetupCurve(preset, preset, 0, 100, first.Item1, first.Item2, second.Item1, second.Item2);
        return ((int)Math.Round(PadProcessor.Response(curve, null, .5) * 100), (int)Math.Round(PadProcessor.Response(curve, null, .75) * 100));
    }

    static readonly Dictionary<string, string> PresetUse = new()
    {
        ["default"] = "straight: output matches the push",
        ["quick"] = "more output early: fast turns, less precise",
        ["slow"] = "less output until the edge: precise aim, slower turns",
        ["smooth"] = "a gentle S: soft around the centre, full speed toward the edge: steady tracking",
        ["fine"] = "slightly quick",
    };

    /// <summary>Each preset in one line, with its real numbers.</summary>
    public static string Preset(string preset)
    {
        var (half, three) = Shape(preset);
        return string.Create(CultureInfo.InvariantCulture, $"{preset}: half a push gives {half}%, three quarters gives {three}%; {PresetUse[preset]}");
    }

    /// <summary>What people usually want, by game.</summary>
    public static readonly string[] Games =
    [
        "Shooters (Call of Duty, Apex, Battlefield): the right trigger (RightTrigger) fires and the left (LeftTrigger) aims, so faster shooting means the right trigger. Hair triggers (trigger_full_at 30 to 50, trigger_deadzone 0), a small stick dead zone, a slow right stick curve for sniping.",
        "Racing: slow trigger curves, trigger_full_at 100, a small trigger dead zone, for throttle and brake control.",
        "Rocket League: jump and dodge are A (a dodge is a second A with a direction), boost is B. A paddle on A dodges without leaving the right stick; quick or default sticks.",
    ];

    /// <summary>The walk-through after a change: one step per stick, trigger or button that changed, in the app's order,
    /// each change with the meaning of its setting.</summary>
    public static IReadOnlyList<TourStep> Tour(PcModeProfile before, PcModeProfile after)
    {
        var changes = AgentApplier.Diff(before, after);
        var steps = new List<TourStep>();
        foreach (var target in AgentVocabulary.Sticks.Concat(AgentVocabulary.Triggers).Concat(AgentVocabulary.Buttons))
        {
            string prefix = AgentApplier.Name(target) + " ";
            bool stick = AgentVocabulary.Sticks.Contains(target), trigger = AgentVocabulary.Triggers.Contains(target);
            var rows = changes.Where(c => c.What.StartsWith(prefix, StringComparison.Ordinal)).Select(c => new TourRow(c, Of(c.What[prefix.Length..] switch
            {
                "dead zone" => stick ? AgentVocabulary.StickDeadzone : AgentVocabulary.TriggerDeadzone,
                "full at" => stick ? AgentVocabulary.StickFullAt : AgentVocabulary.TriggerFullAt,
                "anti-deadzone" => AgentVocabulary.StickAntiDeadzone,
                "curve" => stick ? AgentVocabulary.StickCurve : AgentVocabulary.TriggerCurve,
                "max output" => AgentVocabulary.StickMaxOutput,
                "vertical ratio" => AgentVocabulary.StickAxisRatio,
                "square gate" => AgentVocabulary.StickSquareGate,
                "sends" => AgentVocabulary.Remap,
                _ => AgentVocabulary.Turbo,
            }))).ToList();
            if (rows.Count > 0) steps.Add(new(target, stick ? TourKind.Stick : trigger ? TourKind.Trigger : TourKind.Button, rows));
        }
        return steps;
    }

    /// <summary>The meanings as prompt text.</summary>
    public static string Prompt(bool compact = false)
    {
        var s = new StringBuilder("What each setting means:\n");
        foreach (var m in Settings.Where(m => m.Action != AgentVocabulary.Reset))
        {
            s.Append($"- {m.Name} ({m.Action}): {m.What}.");
            if (compact) { s.Append($" Watch out: {m.Careful}.\n"); continue; }
            if (m.More.Length > 0) s.Append($" Higher: {m.More}. Lower: {m.Less}.");
            s.Append($" Good: {m.Good}. Watch out: {m.Careful}.\n");
        }
        s.Append("Curves (sticks and triggers):\n");
        foreach (var preset in CurveDraft.Presets.Keys) s.Append("- ").Append(Preset(preset)).Append('\n');
        s.Append("Games:\n");
        foreach (var g in Games) s.Append("- ").Append(g).Append('\n');
        return s.ToString().TrimEnd();
    }
}
