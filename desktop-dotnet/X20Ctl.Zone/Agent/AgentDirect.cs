using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace X20Ctl.Zone.Agent;
/// <summary>Only complete, literal commands; open-ended requests go to the phrasebook matcher.</summary>
public static class AgentDirect
{
    static readonly (string Pattern, string Button)[] ButtonWords =
    [
        (@"left bumper|l1|lb", "LB"), (@"right bumper|r1|rb", "RB"), (@"left trigger|l2|lt", "LT"), (@"right trigger|r2|rt", "RT"),
        (@"left stick (?:click|press|button)|l3|ls", "L3"), (@"right stick (?:click|press|button)|r3|rs", "R3"),
        (@"d-?pad up|dpad_up|up", "DPAD_UP"), (@"d-?pad down|dpad_down|down", "DPAD_DOWN"), (@"d-?pad left|dpad_left|left", "DPAD_LEFT"), (@"d-?pad right|dpad_right|right", "DPAD_RIGHT"),
        (@"start|menu|options", "START"), (@"select|back|view|share", "SELECT"), (@"home|guide|xbox", "HOME"),
        (@"a|cross", "A"), (@"b|circle", "B"), (@"x|square", "X"), (@"y|triangle", "Y"),
    ];
    static readonly string Button = @"(?:the\s+)?(?<NAME>" + string.Join("|", ButtonWords.Select(b => b.Pattern)) + @")(?:\s+button)?";
    static string B(string name) => Button.Replace("NAME", name);
    static string? ButtonOf(string word) => ButtonWords.FirstOrDefault(b => Regex.IsMatch(word, "^(?:" + b.Pattern + ")$")).Button;
    static AgentAction Act(string action, string target, object value, string reason) => new(action, target, JsonSerializer.SerializeToElement(value), reason);

    /// <summary>The everyday commands, answered by the app at once with no model call (owner, 8 Nov 2026: "make it really
    /// fast"): remaps, swaps, turbo, PC mode and the lights. Anything less literal still goes to the model.</summary>
    static AgentProposal? Quick(string sentence)
    {
        string s = Regex.Replace(sentence.ToLowerInvariant().Replace('’', '\'').Replace('“', ' ').Replace('”', ' ').Replace('"', ' ').Replace('`', ' '), @"\s+", " ").Trim().TrimEnd('.', '!', '?').Trim();
        s = Regex.Replace(s, @"^(?:hey |ok |okay )?(?:please |pls |can you |could you |would you |i want (?:you )?to |i'd like (?:you )?to |i want |let's )*", "");
        s = Regex.Replace(s, @"(?: please| pls| for me| now| thanks?(?: you)?)+$", "");
        if (AgentColors.Asked(s) is { } colourWords && AgentColors.TryParse(colourWords, out var colour))
            return new($"I'll light up {colour.Name} ({colour.Hex}).", [Act(AgentVocabulary.LedColor, AgentVocabulary.LightingTarget, colourWords, "Colour as asked.")], "", [], [], "");
        var swap = Regex.Match(s, "^(?:swap|switch|exchange|flip) " + B("a") + " (?:and|with|&|<->|for) " + B("b") + "$");
        if (swap.Success && ButtonOf(swap.Groups["a"].Value) is { } s1 && ButtonOf(swap.Groups["b"].Value) is { } s2 && s1 != s2)
            return new($"I'll swap {s1} and {s2}.", [Act(AgentVocabulary.Remap, s1, s2, "Swap as asked."), Act(AgentVocabulary.Remap, s2, s1, "Swap as asked.")], "", [], [], "");
        var map = Regex.Match(s, "^(?:(?:re)?map|bind|set|make|change|switch|turn|put|assign)? ?" + B("a") + " (?:to|->|=>|=|into|as|so it sends|to send|sends?|acts? (?:like|as)|works? (?:like|as)|does|do|presses|press|becomes?|be|is) " + B("b") + "$");
        if (map.Success && ButtonOf(map.Groups["a"].Value) is { } from && ButtonOf(map.Groups["b"].Value) is { } to && from != to)
            return new($"I'll make {from} send {to}.", [Act(AgentVocabulary.Remap, from, to, "Remap as asked.")], "", [], [], "");
        // "reset the right trigger" is the response of the trigger, not a remap: left to the stick and trigger handling
        var unmap = Regex.IsMatch(s, @"\b(trigger|stick|analog)\b") ? null : Regex.Match(s, "^(?:reset|unmap|restore|clear|undo the remap on) " + B("a") + "(?: back)?(?: to (?:normal|default))?$");
        if (unmap is { Success: true } && ButtonOf(unmap.Groups["a"].Value) is { } plain)
            return new($"I'll put {plain} back to normal.", [Act(AgentVocabulary.Reset, plain, 0, "Back to default.")], "", [], [], "");
        var turbo = Regex.Match(s, "^(?:(?:set |put |add |turn on |enable )?turbo (?:on |to |for )?" + B("a") + "|" + B("b") + @" turbo)(?: (?:at|to|=)?\s*(?<hz>\d{1,2})(?: ?(?:presses|clicks|times|hz|per second|a second|/s|x))*)?$");
        if (turbo.Success && ButtonOf(turbo.Groups["a"].Success ? turbo.Groups["a"].Value : turbo.Groups["b"].Value) is { } fast)
        {
            int hz = turbo.Groups["hz"].Success ? int.Parse(turbo.Groups["hz"].Value, CultureInfo.InvariantCulture) : 10;
            return new($"I'll put turbo on {fast} at {hz} presses a second.", [Act(AgentVocabulary.Turbo, fast, hz, "Turbo as asked.")], "", [], [], "");
        }
        var noTurbo = Regex.Match(s, "^(?:turn off|disable|remove|stop|no) turbo (?:on |for |from )?" + B("a") + "$|^turbo off (?:on |for )?" + B("b") + "$");
        if (noTurbo.Success && ButtonOf(noTurbo.Groups["a"].Success ? noTurbo.Groups["a"].Value : noTurbo.Groups["b"].Value) is { } slow)
            return new($"I'll turn turbo off on {slow}.", [Act(AgentVocabulary.Turbo, slow, 0, "Turbo off as asked.")], "", [], [], "");
        var pc = Regex.Match(s, @"^(?:turn|switch|put) (?<on>on|off) pc mode$|^(?:turn|switch|put) pc mode (?<on>on|off)$|^pc mode (?<on>on|off)$|^(?<on>enable|disable) pc mode$");
        if (pc.Success)
        {
            bool on = pc.Groups["on"].Value is "on" or "enable";
            return new($"I'll switch PC mode {(on ? "on" : "off")}.", [Act(AgentVocabulary.PcMode, AgentVocabulary.PcModeTarget, on ? "on" : "off", "As asked.")], "", [], [], "");
        }
        return null;
    }

    /// <summary>A literal command with a number out of range is answered with the range and no actions, never sent on.</summary>
    public static AgentProposal? Resolve(string sentence)
    {
        if (Quick(sentence) is not { } proposal) return Literal(sentence);
        var verdict = ProposalValidator.Check(AgentClarification.Json(proposal), "x20");
        return verdict.Checks.FirstOrDefault(c => c.State == CheckState.Rejected) is { } refused
            ? new(refused.Message.TrimEnd('.') + ". Nothing changed.", [], "", [], [], "") : proposal;
    }

    static AgentProposal? Literal(string sentence)
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
