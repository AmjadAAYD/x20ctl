using System.Text.Json;
using X20Ctl.Zone.PcMode;
namespace X20Ctl.Zone.Agent;

/// <summary>
/// Everything the agent may do, as a fixed list. The language model picks from it; nothing outside it can run. Ranges come
/// from the settings' own limits (StickTuning, PcModeProfile), so a proposal the validator passes is one the app can hold.
/// </summary>
public static class AgentVocabulary
{
    public const string StickDeadzone = "stick_deadzone", StickFullAt = "stick_full_at", StickAntiDeadzone = "stick_anti_deadzone", StickCurve = "stick_curve",
        TriggerDeadzone = "trigger_deadzone", TriggerFullAt = "trigger_full_at", TriggerCurve = "trigger_curve", Remap = "remap", Turbo = "turbo", Reset = "reset", Paddle = "paddle", MeasureDrift = "measure_drift", PcMode = "pc_mode",
        StickCurvePoints = "stick_curve_points", TriggerCurvePoints = "trigger_curve_points", StickMaxOutput = "stick_max_output", StickAxisRatio = "stick_axis_ratio", StickSquareGate = "stick_square_gate",
        MouseMode = "mouse_mode", OpenPage = "open_page";
    public static readonly string[] Actions = [StickDeadzone, StickFullAt, StickAntiDeadzone, StickCurve, StickCurvePoints, StickMaxOutput, StickAxisRatio, StickSquareGate, TriggerDeadzone, TriggerFullAt, TriggerCurve, TriggerCurvePoints, Remap, Turbo, Reset, Paddle, MeasureDrift, PcMode, MouseMode, OpenPage];
    /// <summary>Pages the agent can take the owner to.</summary>
    public static readonly string[] Pages = ["Diagnostics", "Tools", "Mouse", "Curves", "Buttons", "Macros", "Vibration", "PcMode", "Device"];
    public static readonly string[] Sticks = ["LeftStick", "RightStick"], Triggers = ["LeftTrigger", "RightTrigger"];
    public static readonly string[] Buttons = [.. PadButtons.Mask.Keys, "LT", "RT"];
    public static readonly string[] AllPaddles = ["M1", "M2", "M3", "M4", "M5", "M6"];
    public static IReadOnlyCollection<string> Curves => CurveDraft.Presets.Keys.ToArray();
    public const string All = "All", PcModeTarget = "PcMode", MouseTarget = "Mouse", PageTarget = "Page";
    public static readonly string[] Targets = [.. Sticks, .. Triggers, .. Buttons, .. AllPaddles, All, PcModeTarget, MouseTarget, PageTarget];
    public const int MaxTriggerDeadzone = 40, MinTriggerFullAt = 10, MaxActions = 8;

    /// <summary>The rear paddles a model has, from x20ctl/controllers/catalog.json (macroSlots).</summary>
    public static string[] Paddles(string modelId) => MacroDraft.PhysicalSlotsFor(modelId);

}

public sealed record AgentAction(string Action, string Target, JsonElement Value, string Reason)
{
    public string ValueText => Value.ValueKind == JsonValueKind.String ? Value.GetString() ?? "" : Value.GetRawText();
}
/// <summary>Speech: what the agent says, short. Recap: why the changes fit what was asked (or the answer to a question), from
/// the settings' meanings in <see cref="AgentKnowledge"/>.
/// Sources: the numbers of the doc passages the recap was taken from (M5), empty when none were used.
/// Question: what the agent asks back: the detail it still needs, or a check that the change fits; empty when none.</summary>
public sealed record AgentProposal(string Speech, IReadOnlyList<AgentAction> Actions, string Recap, IReadOnlyList<string> Assumptions, IReadOnlyList<int> Sources, string Question = "");

public enum CheckState { Ok, Rejected, Unavailable }
public sealed record ActionCheck(AgentAction Action, CheckState State, string Message);

/// <summary>What validation made of one model reply: either a structure error (nothing usable), or one check per action.
/// One rejected action rejects the whole proposal; an unavailable one is skipped and the rest still apply.</summary>
public sealed record AgentVerdict(AgentProposal? Proposal, string? StructureError, IReadOnlyList<ActionCheck> Checks)
{
    public bool Valid => StructureError == null && Checks.All(c => c.State != CheckState.Rejected);
    public IEnumerable<AgentAction> Applicable => Valid ? Checks.Where(c => c.State == CheckState.Ok).Select(c => c.Action) : [];
}

/// <summary>
/// Layer 1 (structure): the reply is one JSON object with exactly speech, actions, recap and assumptions, typed as expected.
/// Layer 2 (rules): every action is in the vocabulary, aimed at a target it accepts, with a value in range, on a control
/// this model has. Pure: the same reply and model always give the same verdict.
/// </summary>
public static class ProposalValidator
{
    static readonly string[] Fields = ["speech", "actions", "recap", "assumptions"], ActionFields = ["action", "target", "value", "reason"];

    /// <param name="measured">What the app measured earlier in this turn, if anything: a dead zone below a stick's measured
    /// drift is then rejected, because it would let the drift through.</param>
    /// <param name="offered">How many doc passages the model was given this turn: citing any other number is a made-up
    /// source, and the reply is refused.</param>
    public static AgentVerdict Check(string json, string modelId, DriftReport? measured = null, int offered = 0)
    {
        AgentProposal proposal;
        try { proposal = Parse(json); }
        catch (Exception error) when (error is JsonException or InvalidDataException) { return new(null, error is JsonException ? "Not valid JSON." : error.Message, []); }
        var made = proposal.Sources.Where(n => n < 1 || n > offered).ToList();
        if (made.Count > 0) return new(proposal, offered == 0 ? $"Cites source [{made[0]}], but no doc passages were given." : $"Cites source [{made[0]}], but only [1] to [{offered}] were given.", []);
        var checks = new List<ActionCheck>();
        for (int i = 0; i < proposal.Actions.Count; i++)
        {
            var action = proposal.Actions[i];
            var earlier = proposal.Actions.Take(i).FirstOrDefault(a => a.Action == action.Action && a.Target == action.Target);
            if (earlier != null && earlier.ValueText != action.ValueText) { checks.Add(new(action, CheckState.Rejected, $"Conflicts with an earlier change to {action.Target}.")); continue; }
            var (state, message) = Rule(action, modelId, measured);
            checks.Add(new(action, state, earlier != null && state == CheckState.Ok ? "Same as an earlier change." : message));
        }
        return new(proposal, null, checks);
    }

    static AgentProposal Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new InvalidDataException("The reply isn't a JSON object.");
        Only(root, Fields, "reply", optional: ["sources", "question"]);
        string speech = Text(root, "speech", "reply");
        if (!root.TryGetProperty("actions", out var list) || list.ValueKind != JsonValueKind.Array) throw new InvalidDataException("\"actions\" is missing or not a list.");
        if (list.GetArrayLength() > AgentVocabulary.MaxActions) throw new InvalidDataException($"More than {AgentVocabulary.MaxActions} actions.");
        var actions = new List<AgentAction>();
        foreach (var item in list.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) throw new InvalidDataException("An action isn't an object.");
            Only(item, ActionFields, "action");
            if (!item.TryGetProperty("value", out var value) || value.ValueKind is not (JsonValueKind.Number or JsonValueKind.String)) throw new InvalidDataException("An action's \"value\" is missing or not a number or text.");
            actions.Add(new(Text(item, "action", "action"), Text(item, "target", "action"), value.Clone(), Text(item, "reason", "action")));
        }
        if (!root.TryGetProperty("assumptions", out var assumed) || assumed.ValueKind != JsonValueKind.Array || assumed.EnumerateArray().Any(a => a.ValueKind != JsonValueKind.String))
            throw new InvalidDataException("\"assumptions\" is missing or not a list of text.");
        string recap = Text(root, "recap", "reply");
        var sources = new List<int>();
        if (root.TryGetProperty("sources", out var cited))
        {
            if (cited.ValueKind != JsonValueKind.Array || cited.EnumerateArray().Any(n => n.ValueKind != JsonValueKind.Number || !n.TryGetInt32(out _))) throw new InvalidDataException("\"sources\" is not a list of whole numbers.");
            sources.AddRange(cited.EnumerateArray().Select(n => n.GetInt32()).Distinct());
        }
        string question = root.TryGetProperty("question", out var q) && q.ValueKind == JsonValueKind.String ? q.GetString()!.Trim() : "";
        return new(speech, actions, recap, assumed.EnumerateArray().Select(a => a.GetString()!).ToArray(), sources, question);
    }

    static void Only(JsonElement element, string[] fields, string what, string[]? optional = null)
    {
        foreach (var property in element.EnumerateObject())
            if (!fields.Contains(property.Name) && optional?.Contains(property.Name) != true) throw new InvalidDataException($"Unknown field \"{property.Name}\" in the {what}.");
        foreach (var field in fields)
            if (!element.TryGetProperty(field, out _)) throw new InvalidDataException($"The {what} has no \"{field}\".");
    }
    static string Text(JsonElement element, string field, string what) =>
        element.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()! : throw new InvalidDataException($"The {what}'s \"{field}\" is missing or isn't text.");

    static (CheckState, string) Rule(AgentAction a, string modelId, DriftReport? measured)
    {
        static (CheckState, string) No(string why) => (CheckState.Rejected, why);
        if (!AgentVocabulary.Actions.Contains(a.Action)) return No($"\"{a.Action}\" isn't something I can do.");
        bool stick = AgentVocabulary.Sticks.Contains(a.Target), trigger = AgentVocabulary.Triggers.Contains(a.Target), button = AgentVocabulary.Buttons.Contains(a.Target);
        // a paddle named anywhere: first, does this controller have it at all
        foreach (var named in new[] { a.Target, a.ValueText }.Where(AgentVocabulary.AllPaddles.Contains))
            if (!AgentVocabulary.Paddles(modelId).Contains(named)) return No($"This controller has no {named}.");
        if (AgentVocabulary.AllPaddles.Contains(a.ValueText)) return No($"Buttons can't send {a.ValueText}: it's a paddle. Use {{\"action\":\"paddle\",\"target\":\"{a.ValueText}\",\"value\":\"A\"}} (or \"B\").");
        if (AgentVocabulary.AllPaddles.Contains(a.Target) && a.Action != AgentVocabulary.Paddle) return No($"{a.Target} is a paddle. Use {{\"action\":\"paddle\",\"target\":\"{a.Target}\",\"value\":\"A\"}} (or \"B\").");
        switch (a.Action)
        {
            case AgentVocabulary.StickDeadzone or AgentVocabulary.StickFullAt or AgentVocabulary.StickAntiDeadzone:
                if (!stick) return No($"{a.Action} needs LeftStick or RightStick, not {a.Target}.");
                var (low, high) = a.Action switch { AgentVocabulary.StickDeadzone => (0.0, StickTuning.MaxInner), AgentVocabulary.StickAntiDeadzone => (0.0, StickTuning.MaxAnti), _ => (StickTuning.MinOuter, 100.0) };
                var range = Number(a, low, high, decimals: true);
                // grounded in the measurement: a dead zone the measured drift reaches past doesn't fix it
                if (range.Item1 == CheckState.Ok && a.Action == AgentVocabulary.StickDeadzone && measured?.For(a.Target) is { Suggested: > 0 } drift && a.Value.GetDouble() <= drift.Peak)
                    return No(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"The {AgentApplier.Name(a.Target).ToLowerInvariant()} was measured wandering up to {drift.Peak:0.0}% with no hand on it, so a {a.Value.GetDouble():0.#}% dead zone lets the drift through. Use at least {drift.Suggested}."));
                return range;
            case AgentVocabulary.TriggerDeadzone or AgentVocabulary.TriggerFullAt:
                if (!trigger) return No($"{a.Action} needs LeftTrigger or RightTrigger, not {a.Target}.");
                return a.Action == AgentVocabulary.TriggerDeadzone ? Number(a, 0, AgentVocabulary.MaxTriggerDeadzone, decimals: false) : Number(a, AgentVocabulary.MinTriggerFullAt, 100, decimals: false);
            case AgentVocabulary.StickCurve or AgentVocabulary.TriggerCurve:
                if (a.Action == AgentVocabulary.StickCurve ? !stick : !trigger) return No($"{a.Action} can't be aimed at {a.Target}.");
                return Word(a, AgentVocabulary.Curves);
            case AgentVocabulary.Remap:
                if (!button) return No($"{a.Target} isn't a button that can be remapped.");
                return Word(a, AgentVocabulary.Buttons);
            case AgentVocabulary.Turbo:
                if (!button) return No($"{a.Target} isn't a button that can have turbo.");
                if (a.Value.ValueKind != JsonValueKind.Number || !a.Value.TryGetInt32(out int hz)) return No("Turbo needs a whole number of presses a second.");
                return hz == 0 || hz is >= PcModeProfile.MinTurboHz and <= PcModeProfile.MaxTurboHz ? (CheckState.Ok, "In range.") : No($"Turbo is {PcModeProfile.MinTurboHz} to {PcModeProfile.MaxTurboHz} presses a second, or 0 for off; {hz} is outside.");
            case AgentVocabulary.PcMode:
                if (a.Target != AgentVocabulary.PcModeTarget) return No($"pc_mode needs the target PcMode, not {a.Target}.");
                return Word(a, ["on", "off"]);
            case AgentVocabulary.MouseMode:
                if (a.Target != AgentVocabulary.MouseTarget) return No($"mouse_mode needs the target Mouse, not {a.Target}.");
                return Word(a, ["on", "off"]);
            case AgentVocabulary.OpenPage:
                if (a.Target != AgentVocabulary.PageTarget) return No($"open_page needs the target Page, not {a.Target}.");
                return Word(a, AgentVocabulary.Pages);
            case AgentVocabulary.StickCurvePoints or AgentVocabulary.TriggerCurvePoints:
                if (a.Action == AgentVocabulary.StickCurvePoints ? !stick : !trigger) return No($"{a.Action} can't be aimed at {a.Target}.");
                return CurvePoints.TryParse(a.ValueText, out _, out string why) ? (CheckState.Ok, "A custom curve.") : No(why);
            case AgentVocabulary.StickMaxOutput:
                if (!stick) return No($"{a.Action} needs LeftStick or RightStick, not {a.Target}.");
                return Number(a, 60, 100, decimals: true);
            case AgentVocabulary.StickAxisRatio:
                if (!stick) return No($"{a.Action} needs LeftStick or RightStick, not {a.Target}.");
                return Number(a, StickTuning.MinRatio, StickTuning.MaxRatio, decimals: false);
            case AgentVocabulary.StickSquareGate:
                if (!stick) return No($"{a.Action} needs LeftStick or RightStick, not {a.Target}.");
                return Word(a, ["on", "off"]);
            case AgentVocabulary.MeasureDrift:
                return stick ? (CheckState.Ok, "A reading: changes nothing.") : No($"measure_drift needs LeftStick or RightStick, not {a.Target}.");
            case AgentVocabulary.Reset:
                return stick || trigger || button || a.Target == AgentVocabulary.All ? (CheckState.Ok, "Back to default.") : No($"{a.Target} has nothing to reset.");
            default: // paddle
                if (!AgentVocabulary.AllPaddles.Contains(a.Target)) return No($"{a.Target} isn't a paddle.");
                if (!AgentVocabulary.Paddles(modelId).Contains(a.Target)) return No($"This controller has no {a.Target}.");
                var output = Word(a, ["A", "B"]);
                return output.Item1 == CheckState.Rejected ? (CheckState.Rejected, "Paddles can only send A or B.") : (CheckState.Unavailable, "Valid, but paddles are programmed on the controller itself, which isn't supported yet.");
        }
    }

    static (CheckState, string) Number(AgentAction a, double low, double high, bool decimals)
    {
        if (a.Value.ValueKind != JsonValueKind.Number || !a.Value.TryGetDouble(out double v)) return (CheckState.Rejected, $"Needs a number, not \"{a.ValueText}\".");
        if (!decimals && v != Math.Floor(v)) return (CheckState.Rejected, "Needs a whole number.");
        return v >= low && v <= high ? (CheckState.Ok, "In range.") : (CheckState.Rejected, $"{low:0.#} to {high:0.#} only; {v:0.#} is outside.");
    }
    static (CheckState, string) Word(AgentAction a, IReadOnlyCollection<string> allowed) =>
        a.Value.ValueKind == JsonValueKind.String && allowed.Contains(a.Value.GetString()) ? (CheckState.Ok, "Known value.") : (CheckState.Rejected, $"\"{a.ValueText}\" isn't one of: {string.Join(", ", allowed)}.");
}

/// <summary>A custom curve from the agent: "x1,y1,x2,y2", two points as push and output percents (0–100), the first push
/// before the second. The curve runs through them (PadProcessor.Through), like the points on the Curves page.</summary>
public static class CurvePoints
{
    public static bool TryParse(string text, out (int X1, int Y1, int X2, int Y2) points, out string why)
    {
        points = default; why = "";
        var parts = text.Replace(" ", "").Split(',');
        if (parts.Length != 4 || !parts.All(p => int.TryParse(p, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out _)))
        { why = $"Curve points are four whole numbers \"push1,output1,push2,output2\", such as \"25,15,70,60\"; not \"{text}\"."; return false; }
        var n = parts.Select(p => int.Parse(p, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        if (n.Any(v => v is < 0 or > 100)) { why = "Curve points are percents, 0 to 100."; return false; }
        if (n[0] >= n[2]) { why = "The first point's push must be before the second's."; return false; }
        points = (n[0], n[1], n[2], n[3]); return true;
    }
    public static int ToByte(int percent) => (int)Math.Round(percent * 2.55);
}
