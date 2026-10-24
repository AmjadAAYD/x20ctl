using System.Text.RegularExpressions;
namespace X20Ctl.Zone.Agent;

/// <summary>Things the app does itself when asked in the chat, instantly and without the model.</summary>
public enum AgentCommand { None, AboutController, Battery, ChangeController, Star, Donate, Diagnostics }

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
        // owner, 14 Oct 2026: asking for a diagnostic goes straight to the Diagnostics page
        if (Regex.IsMatch(lower, @"\b(diagnos\w*|diagnost\w*|test (my |the )?(controller|pad|sticks?|buttons?|triggers?)|check (my |the )?(controller|pad|sticks?|buttons?))\b")) return AgentCommand.Diagnostics;
        // the battery now, not how long batteries last in general (that's a docs question)
        if (Regex.IsMatch(lower, @"\b(battery|batteries|charge left|charged|how much charge|power left)\b") && !Regex.IsMatch(lower, @"\b(hours?|last|lasts|life|lifespan|long|replace|buy)\b")) return AgentCommand.Battery;
        if (Regex.IsMatch(lower, @"\b(change|switch|swap|another|different|other|pick|choose)\b.*\b(controllers?|pads?|gamepads?)\b|\bcontroller zone\b|\b(use|switch to|change to) (my |the )?(x20 pro|x20|x05 pro|x05|dune)\b")) return AgentCommand.ChangeController;
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
        "Opening Diagnostics: roll each stick round its edge, squeeze both triggers and press every button. I'll watch.",
        "To Diagnostics! Move everything on me and we'll see what's off. Use Esc or the mouse to leave capture.",
        "Taking you to Diagnostics. Wiggle, squeeze, press: I'm all yours.",
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

    /// <summary>What the battery reading says, in character. Level: empty, low, medium, full; wired; or null when Windows
    /// didn't report it.</summary>
    public static string Battery(string? level, string controllerName) => level switch
    {
        "full" => AgentVariety.Pick("battery-full", ["My battery is full. Let's play all night!", "Battery full! I could go for hours.", "Fully charged and ready to rumble."]),
        "medium" => AgentVariety.Pick("battery-medium", ["My battery is about half full. Plenty left for a few rounds.", "Battery at medium. We're good for a while."]),
        "low" => AgentVariety.Pick("battery-low", ["My battery is low. Plug me in soon, I'm getting sleepy.", "Battery low! *yawns* A charge would be lovely."]),
        "empty" => AgentVariety.Pick("battery-empty", ["My battery is nearly empty! Charge me before I nap mid-game.", "Running on fumes! Plug me in, please."]),
        "wired" => AgentVariety.Pick("battery-wired", ["I'm on a cable, so I'm running on wall power. No battery worries!", "Plugged in by cable, so no battery to worry about."]),
        null => AgentVariety.Pick("battery-none", [$"Windows didn't tell me my battery level. Is your {controllerName} connected?", $"I can't feel my battery right now. Is your {controllerName} connected?"]),
        _ => $"Windows says my battery is: {level}.",
    };
}
