using System.Text.Json;
using System.Text.RegularExpressions;
namespace X20Ctl.Zone.Agent;

public sealed record PendingClarification(string Request, string Question);

/// <summary>Completes unambiguous missing slots; broader answers still go through the model with explicit context.</summary>
public static class AgentClarification
{
    public static bool IsCancel(string text) => Regex.IsMatch(text.Trim(), @"^(cancel|never ?mind|forget (it|that)|stop that)[.!]?$", RegexOptions.IgnoreCase);
    public static bool IsNewRequest(string text) => Regex.IsMatch(text.Trim(), @"^(please\s+)?(make|set|turn|switch|enable|disable|open|show|reset|swap|remap|put|give|measure|diagnos\w*)\b", RegexOptions.IgnoreCase);

    public static string AnswerText(PendingClarification pending, string answer)
    {
        if (Regex.IsMatch(answer.Trim(), @"^(left|right)$", RegexOptions.IgnoreCase))
        {
            if (pending.Question.Contains("stick", StringComparison.OrdinalIgnoreCase)) answer += " stick";
            else if (pending.Question.Contains("trigger", StringComparison.OrdinalIgnoreCase)) answer += " trigger";
        }
        return pending.Request + " " + answer;
    }

    public static AgentProposal? Resolve(PendingClarification pending, string answer)
    {
        string text = AnswerText(pending, answer), request = pending.Request.ToLowerInvariant();
        var slots = OfflineAgent.Slots(text);
        var original = OfflineAgent.Slots(pending.Request);
        var supplied = OfflineAgent.Slots(answer);
        if (answer.Trim().Equals("A", StringComparison.OrdinalIgnoreCase) && !supplied.Buttons.Contains("A")) supplied.Buttons.Add("A");
        slots.Buttons.Clear();
        slots.Buttons.AddRange(original.Buttons.Concat(supplied.Buttons).Distinct());
        var actions = new List<AgentAction>();
        var assumptions = new List<string>();
        void Add(string action, string target, object value) => actions.Add(new(action, target, JsonSerializer.SerializeToElement(value), "Completed the pending request."));

        if (Regex.IsMatch(request, @"\b(turbo|rapid fire)\b") && slots.Buttons.Count > 0)
        {
            double rate = Regex.IsMatch(request, @"\b(off|disable|stop|remove)\b") ? 0 : supplied.Numbers.FirstOrDefault(original.Numbers.FirstOrDefault(10));
            if (original.Numbers.Count == 0 && supplied.Numbers.Count == 0 && rate != 0) assumptions.Add("10 presses a second; no rate was specified.");
            foreach (var button in slots.Buttons) Add(AgentVocabulary.Turbo, button, rate);
        }
        else if (Regex.IsMatch(request, @"\b(swap|exchange)\b") && slots.Buttons.Count == 2)
        { Add(AgentVocabulary.Remap, slots.Buttons[0], slots.Buttons[1]); Add(AgentVocabulary.Remap, slots.Buttons[1], slots.Buttons[0]); }
        else if (Regex.IsMatch(request, @"\b(remap|map|send|act like)\b") && slots.Buttons.Count == 2)
        {
            bool destinationOnly = original.Buttons.Count == 1 && Regex.IsMatch(request, @"\b(to|send|act like|into)\s+(the\s+)?(a|b|x|y|lb|rb|lt|rt)\b");
            Add(AgentVocabulary.Remap, destinationOnly ? slots.Buttons[1] : slots.Buttons[0], destinationOnly ? slots.Buttons[0] : slots.Buttons[1]);
        }
        else if (slots.Numbers.Count > 0 && slots.Sticks.Count + slots.Triggers.Count > 0)
        {
            bool trigger = slots.Triggers.Count > 0;
            var targets = trigger ? slots.Triggers : slots.Sticks;
            string? action = Regex.IsMatch(request, @"anti[ -]?dead") ? AgentVocabulary.StickAntiDeadzone
                : Regex.IsMatch(request, @"dead[ -]?zone") ? trigger ? AgentVocabulary.TriggerDeadzone : AgentVocabulary.StickDeadzone
                : Regex.IsMatch(request, @"full (at|speed|press)|fully pressed|outer") ? trigger ? AgentVocabulary.TriggerFullAt : AgentVocabulary.StickFullAt
                : request.Contains("output") ? AgentVocabulary.StickMaxOutput
                : request.Contains("ratio") ? AgentVocabulary.StickAxisRatio : null;
            if (action != null) foreach (var target in targets) Add(action, target, supplied.Numbers.FirstOrDefault(original.Numbers.FirstOrDefault()));
        }
        if (actions.Count == 0) return null;
        return new("I'll apply those details to your request.", actions, "", assumptions, [], "");
    }

    public static string Json(AgentProposal proposal) => JsonSerializer.Serialize(new { speech = proposal.Speech, actions = proposal.Actions.Select(a => new { action = a.Action, target = a.Target, value = a.Value, reason = a.Reason }), recap = proposal.Recap, assumptions = proposal.Assumptions, sources = proposal.Sources, question = proposal.Question });
}
