namespace X20Ctl.Zone.Agent;

/// <summary>One line of the help list: a situation the owner may be in, and a sentence that handles it.</summary>
public sealed record HelpSituation(string Have, string Say);

/// <summary>
/// "Don't know what to say? Say HELP" (owner request, 13 Oct 2026): a fixed list of situations, each with the sentence
/// that deals with it. Answered by the app itself, the same with the model or offline, so it is instant and never wrong.
/// </summary>
public static class AgentHelp
{
    public const string Hint = "Don't know what to say? Say HELP";

    public static readonly HelpSituation[] Situations =
    [
        new("Your stick moves on its own, or the aim creeps", "My right stick drifts a little"),
        new("You miss long shots", "Make my right stick good for sniping"),
        new("Your aim jumps past the target", "My aim feels too twitchy"),
        new("The camera turns too slowly", "Make my sticks faster"),
        new("Your sticks feel choppy", "Make my sticks smooth"),
        new("Small stick moves do nothing in game", "Small stick movements don't register in my game"),
        new("You want to shoot sooner", "Hair triggers for shooting"),
        new("Throttle or brakes are all or nothing", "Slow, progressive triggers for racing"),
        new("Your thumb hurts from mashing a button", "Turbo on X, 12 presses a second"),
        new("A button keeps repeating", "Turn off turbo on X"),
        new("You're used to a Nintendo layout", "Swap A and B"),
        new("Your settings do nothing in games", "Turn on PC mode"),
        new("You changed too much and want to start over", "Reset everything"),
        new("You want a custom response rather than a preset", "Set right stick curve points to 30,24,75,68"),
        new("Turning reaches too much speed", "Set right stick maximum output to 90"),
        new("Vertical movement needs a different speed", "Set right stick axis ratio to 85"),
        new("You want to control the mouse pointer", "Turn mouse mode on"),
        new("You want to check stick range and roundness", "Open Diagnostics"),
        new("You want default responses without changing buttons", "Reset curves"),
        new("Your controller is not detected", "My controller is not detected"),
        new("The game sees two controllers", "Why do I get double input?"),
        new("You want to know if I need internet", "Does the agent need internet?"),
        new("You need to build a timed combo", "How do I create a macro?"),
        new("You want to know what a setting does", "What's good and bad about hair triggers?"),
    ];

    /// <summary>The sentence is a request for this list: "help", "HELP!", "help me", "?"…</summary>
    public static bool Asks(string sentence) =>
        sentence.Trim().TrimEnd('!', '?', '.').Trim().ToLowerInvariant() is "help" or "help me" or "" or "what can i say" or "what do i say" or "commands";
}
