using System.Text.RegularExpressions;
namespace X20Ctl.Zone.Agent;

/// <summary>Things the app does itself when asked in the chat, instantly and without the model.</summary>
public enum AgentCommand { None, AboutController, Battery, ChangeController, Star, Donate, Diagnostics, Access }

/// <summary>
/// The agent's app commands (owner request, 13 Oct 2026): naming the controller ("EasySMX X20 Pro") gets "That's your
/// controller, what's wrong with it?"; asking for the battery reads it; "change controller" goes to the Controller Zone;
/// "I want to give a star" opens the GitHub page and "donate" the support page. Recognised by the app before any model is
/// asked, so they are instant and always the same.
/// </summary>
public static class AgentCommands
{
    static readonly string[] Models = ["x20 pro", "x20pro", "x20", "x05 pro", "dune", "x15", "easysmx"];

    public static AgentCommand Parse(string sentence)
    {
        sentence = AgentSpelling.Fix(sentence);
        string lower = " " + Regex.Replace(sentence.ToLowerInvariant().Replace('’', '\''), @"[^a-z0-9' ]", " ") + " ";
        lower = Regex.Replace(lower, @"\s+", " ");
        // "can you read my controller?": what the agent can reach, checked for real (owner, 8 Nov 2026)
        if (Regex.IsMatch(lower, @"\b(can|do|are|could) you\b.*\b(read|access|see|write|reach|save|talk to|connect\w*|detect\w*)\b.*\b(controllers?|pads?|gamepads?|settings|config\w*|memory)\b|\b(check|test) (your |the )?access\b|\baccess check\b")) return AgentCommand.Access;
        // owner, 14 Oct 2026: asking for a diagnostic starts the check-up (since 8 Nov 2026, on the agent's own screen)
        if (Regex.IsMatch(lower, @"\b(diagnos\w*|diagnost\w*|test (my |the )?(controller|pad|sticks?|buttons?|triggers?)|check (my |the )?(controller|pad|sticks?|buttons?))\b")) return AgentCommand.Diagnostics;
        // the battery now, not how long batteries last in general (that's a docs question)
        if (Regex.IsMatch(lower, @"\b(battery|batteries|charge left|charged|how much charge|power left)\b") && !Regex.IsMatch(lower, @"\b(hours?|last|lasts|life|lifespan|long|replace|buy)\b")) return AgentCommand.Battery;
        // the whole sentence counts (owner, 8 Nov 2026: "change my controller to green" is about its colour, not another
        // controller): only a request that is about switching and nothing more goes to the Controller Zone
        if (Regex.IsMatch(lower, @"^ ?(?:i want to |i'd like to |let me |can i |could i |please |i need to )*(?:change|switch|swap|pick|choose|use)(?: to)? (?:my |the |a |an )?(?:another |different |other |new |second )?(?:controllers?|pads?|gamepads?)(?: please)? ?$")
            || Regex.IsMatch(lower, @"\b(?:another|a different|a new|other|second) (?:controller|pad|gamepad)s? ?(?:please )?$")
            || Regex.IsMatch(lower, @"\bcontroller zone\b")
            || Regex.IsMatch(lower, @"\b(use|switch to|change to) (my |the )?(x20 pro|x20|x05 pro|dune)\b")) return AgentCommand.ChangeController;
        if (Regex.IsMatch(lower, @"\b(give|leave|drop) (you |it |the (repo|project) )?(a |one )?stars?\b|\bstar (the |this |your )?(repo|project|app|it|you)\b|\bstar on github\b|^ ?star ?$")) return AgentCommand.Star;
        if (Regex.IsMatch(lower, @"\b(donate|donation|donating|tip|support (you|the (dev|developer|project|app))|ko ?fi|buy (you|the dev) a coffee)\b")) return AgentCommand.Donate;
        // only the controller's name, maybe as a question: "EasySMX X20 Pro", "what is the x20 pro?"
        string rest = lower;
        foreach (var m in Models) rest = rest.Replace(" " + m + " ", " ");
        rest = Regex.Replace(rest, @"\b(what|whats|what's|is|the|a|an|my|this|about|tell|me|who|are|you|it|controller|gamepad|pad|easy|smx|do|know|of)\b", " ").Trim();
        return rest.Length == 0 && Models.Any(m => lower.Contains(" " + m + " ")) ? AgentCommand.AboutController : AgentCommand.None;
    }

    public static string AboutController(string name) => AgentVariety.Pick("about", [
        "That's your controller, what's wrong with it?",
        $"Hey, that's me! Your {name}. Something feel off?",
        $"You called? That's your {name}, right here. What needs fixing?",
        "That's me! *waves a stick* What's bothering you?",
        $"Yep, your {name}, at your service. What's wrong?",
    ]);
    public static string Diagnostics() => AgentVariety.Pick("diagnostics", [
        "Check-up time! It runs right here on my screen: let go, roll each stick, squeeze, press. I'll watch.",
        "Let's check you over, here on my screen. Follow the steps and I'll measure everything.",
        "Check-up on my screen, starting now. Wiggle, squeeze, press: I'm all yours.",
    ]);
    public static string ChangeController() => AgentVariety.Pick("change", [
        "Taking you to the Controller Zone. Pick another controller there, I'll wait right here. *taps foot*",
        "Off to the Controller Zone! Don't forget about me. *sniff*",
        "Sure, the Controller Zone it is. Choose wisely!",
    ]);
    public static string Star() => AgentVariety.Pick("star", [
        "Opening the X20CTL page on GitHub: the star is at the top right. *blushes in RGB*",
        "A star? For me? Opening GitHub, it's the button at the top right!",
        "Opening GitHub. Top right, the little star. *wiggles happily*",
    ]);
    public static string Donate() => AgentVariety.Pick("donate", [
        "Opening the support page. Every coffee goes straight into more features. *happy rumble*",
        "Aww, thank you! Opening the support page now.",
        "Opening the support page. You're the best player I've ever had.",
    ]);

    /// <summary>What the battery reading says, in character. Level: empty, low, medium, full; wired; the controller's own
    /// reading over Bluetooth, "N/4" with " (Charging)" while it charges; or null when nothing reported it.</summary>
    public static string Battery(string? level, string controllerName)
    {
        var bars = level == null ? null : Regex.Match(level, @"^\s*([0-4])\s*/\s*4");
        if (bars is { Success: true })
        {
            int n = bars.Groups[1].Value[0] - '0';
            bool charging = level!.Contains("charging", StringComparison.OrdinalIgnoreCase);
            string said = n switch
            {
                4 => AgentVariety.Pick("battery-bt-4", ["My battery is full, 4 of 4 bars. Let's play all night!", "All 4 bars! I could go for hours."]),
                3 => AgentVariety.Pick("battery-bt-3", ["My battery is at 3 of 4 bars. Plenty left.", "3 of 4 bars. We're good for a good while."]),
                2 => AgentVariety.Pick("battery-bt-2", ["My battery is at 2 of 4 bars, about half.", "Half way: 2 of 4 bars left."]),
                1 => AgentVariety.Pick("battery-bt-1", ["My battery is down to 1 of 4 bars. Plug me in soon, I'm getting sleepy.", "1 bar of 4 left! *yawns* A charge would be lovely."]),
                _ => AgentVariety.Pick("battery-bt-0", ["My battery is nearly empty! Charge me before I nap mid-game.", "Running on fumes! Plug me in, please."]),
            };
            return said + (charging ? " And I'm charging right now." : "");
        }
        return Words(level, controllerName);
    }

    static string Words(string? level, string controllerName) => level switch
    {
        "full" => AgentVariety.Pick("battery-full", ["My battery is full. Let's play all night!", "Battery full! I could go for hours.", "Fully charged and ready to rumble."]),
        "medium" => AgentVariety.Pick("battery-medium", ["My battery is about half full. Plenty left for a few rounds.", "Battery at medium. We're good for a while."]),
        "low" => AgentVariety.Pick("battery-low", ["My battery is low. Plug me in soon, I'm getting sleepy.", "Battery low! *yawns* A charge would be lovely."]),
        "empty" => AgentVariety.Pick("battery-empty", ["My battery is nearly empty! Charge me before I nap mid-game.", "Running on fumes! Plug me in, please."]),
        "wired" => AgentVariety.Pick("battery-wired", ["I'm on a cable, so I'm running on wall power. No battery worries!", "Plugged in by cable, so no battery to worry about."]),
        null => AgentVariety.Pick("battery-none", [$"Neither Windows nor Bluetooth told me my battery level. Is your {controllerName} connected by Bluetooth, or in Xbox (XInput) mode?", $"I can't feel my battery right now. Bluetooth is the surest way for me to read it: is your {controllerName} connected that way?"]),
        _ => $"Windows says my battery is: {level}.",
    };
}
