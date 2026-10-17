using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace X20Ctl.Zone.Agent;
/// <summary>Only complete, literal commands; open-ended requests go to the phrasebook matcher.</summary>
public static class AgentDirect
{
    public static AgentProposal? Resolve(string sentence)
    {
        if (Regex.IsMatch(sentence.Trim(), @"^(?:please\s+)?reset (?:all |the )?curves[.!]?$", RegexOptions.IgnoreCase))
            return new("I'll reset both stick and trigger responses.", AgentVocabulary.Sticks.Concat(AgentVocabulary.Triggers).Select(t => new AgentAction(AgentVocabulary.Reset,t,JsonSerializer.SerializeToElement(0),"Reset response only.")).ToArray(),"Buttons and turbo stay unchanged.",[],[],"");
        var match = Regex.Match(sentence.Trim(), @"^(?:please\s+)?(?:set|change)\s+(?:the\s+)?(?<target>left stick|right stick|both sticks|left trigger|right trigger|both triggers)(?:'s)?\s+(?<setting>dead ?zone|full at|anti[ -]?dead ?zone|max(?:imum)? output|(?:vertical )?axis ratio)\s+(?:(?:to|at)\s+)?(?<number>-?\d+(?:\.\d+)?)%?[.!]?$", RegexOptions.IgnoreCase);
        if (!match.Success) return null;
        string target = match.Groups["target"].Value.ToLowerInvariant(), setting = match.Groups["setting"].Value.ToLowerInvariant();
        bool trigger = target.Contains("trigger");
        string[] targets = target.StartsWith("both") ? trigger ? AgentVocabulary.Triggers : AgentVocabulary.Sticks
            : [trigger ? target.StartsWith("left") ? "LeftTrigger" : "RightTrigger" : target.StartsWith("left") ? "LeftStick" : "RightStick"];
        string? action = setting.StartsWith("anti") ? trigger ? null : AgentVocabulary.StickAntiDeadzone
            : setting.Replace(" ", "") == "deadzone" ? trigger ? AgentVocabulary.TriggerDeadzone : AgentVocabulary.StickDeadzone
            : setting == "full at" ? trigger ? AgentVocabulary.TriggerFullAt : AgentVocabulary.StickFullAt
            : setting.Contains("output") ? trigger ? null : AgentVocabulary.StickMaxOutput
            : setting.Contains("ratio") ? trigger ? null : AgentVocabulary.StickAxisRatio : null;
        if (action == null) return null;
        double value = double.Parse(match.Groups["number"].Value,CultureInfo.InvariantCulture);
        return new($"I'll set {target} {setting} to {value.ToString(CultureInfo.InvariantCulture)}%.",
            targets.Select(t => new AgentAction(action,t,JsonSerializer.SerializeToElement(value),"Exact requested value.")).ToArray(),
            "", [], [], "");
    }
}
