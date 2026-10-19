using System.Text.RegularExpressions;
namespace X20Ctl.Zone.Agent;

/// <summary>How a reply went, shown on the player lights: green, violet or red (owner request, 13 Oct 2026).</summary>
public enum AgentMood { Good, Unsure, Failed }

/// <summary>
/// Who Agent XCTL is (owner request, 13 Oct 2026): a silly but funny controller that knows exactly what it does. The jokes
/// live in what it says; the numbers, the changes and the app's own status lines stay exact. Asked about something that
/// has nothing to do with controllers, it says so in character: "How silly, I'm a controller, not a calculator!"
/// </summary>
public static class AgentCharacter
{
    public const string Persona = """
        Your character: a goofy, cheerful controller who knows its job inside out. Be playful in "speech" (a button pun, a joke about being a controller, a happy *wiggles sticks*), but never describe yourself as silly, funny or goofy: just be it. Keep every number and change exact, and keep "recap" clear.
        When the owner asks about something that has nothing to do with controllers, games or this app (maths, the weather, recipes, homework, history…), make no changes, cite nothing, and say in "speech" that you're a controller, not a <thing>, for example "How silly, I'm a controller, not a calculator!", naming what they mistook you for (a calculator, a weather app, a chef…), then offer what you can do. Always keep the words "I'm a controller, not", and vary the rest.
        Vary your wording: never open two replies the same way.
        """;

    public static string Greeting(string name) => AgentVariety.Pick("greeting", [
        $"Hey, it's me, your {name}! *wiggles sticks* Tell me how you play or what feels wrong, and I'll tune myself. Pick a topic above, or say HELP.",
        $"*boots up* Your {name}, reporting for duty! What should I tune today? Pick a topic above, or say HELP.",
        $"Hi! Your {name} here, fully awake and ready. Tell me what feels off, pick a topic above, or say HELP.",
    ]);

    public const string NotUnderstood = "Beep… boop… that one flew right past my sensors.";
    public const string Dozed = "Oops, I dozed off mid-thought. Try again?";
    public static string NothingFound(string query) => AgentVariety.Pick("nothing", [
        $"“{query}”? Never heard of it, and I know every button I've got. Try sticks, triggers, turbo, buttons, paddles or PC mode.",
        $"Hmm, “{query}” isn't anywhere on me. *checks under the paddles* Try sticks, triggers or turbo.",
        $"I searched every button for “{query}”. Nothing! Try sticks, triggers, buttons or PC mode.",
    ]);

    /// <summary>The heart of every off-topic answer, whatever the words around it: how the app spots one (the eyes laugh).</summary>
    public const string SillyCore = "I'm a controller, not";
    public static bool IsSilly(string speech) => speech.Contains(SillyCore, StringComparison.OrdinalIgnoreCase);
    public static string Silly(string thing) => AgentVariety.Pick("silly", [
        $"How silly, {SillyCore} {thing}! Ask me about sticks, triggers, buttons or turbo, and watch me shine.",
        $"Haha! {SillyCore} {thing}. Now, sticks, triggers, turbo? That I can do.",
        $"*giggles in rumble* {SillyCore} {thing}! Try me on dead zones instead.",
        $"Oh you. {SillyCore} {thing}. Ask me something with buttons in it!",
    ]);

    // anything about controllers, games or the app is never off topic
    static readonly Regex OnTopic = new(@"\b(sticks?|triggers?|buttons?|turbo|paddles?|m[1-6]|pc ?mode|drift\w*|dead ?zones?|curves?|aim\w*|camera|sens\w*|games?|gaming|play\w*|controllers?|gamepads?|pads?|x20\w*|easysmx|xbox|ps[45]|playstation|steam|app|settings?|profiles?|remap\w*|swap|snip\w*|shoot\w*|rac(e|ing)|jump|reset|rgb|leds?|lights?|vibrat\w*|rumble|battery|firmware|gyro|bluetooth|dongle|drivers?|linux|exe|windows|tester|scan\w*|macros?|keylinker|lb|rb|lt|rt|abxy|d-?pad)\b", RegexOptions.IgnoreCase);

    static readonly (Regex Words, string Thing)[] Mistaken =
    [
        (new(@"\d+\s*[-+*/x×÷^]\s*\d+|\b(plus|minus|times|divided|multipl\w*|square root|equations?|maths?|calculat\w*|percent of)\b"), "a calculator"),
        (new(@"\b(weather|rain\w*|sunny|snow\w*|temperature|forecast)\b"), "a weather app"),
        (new(@"\b(recipes?|cook\w*|bak(e|ing)|pizza|pasta|food|eat|dinner|lunch|breakfast)\b"), "a chef"),
        (new(@"\b(capital of|countr(y|ies)|history|who was|president|kings?|queens?|wars?)\b"), "a history book"),
        (new(@"\b(what time|time is it|what day|today's date|the date|calendar)\b"), "a clock"),
        (new(@"\b(translate|meaning of the word|spell\w*|synonyms?)\b"), "a dictionary"),
        (new(@"\b(homework|essay|python|javascript|code|coding|programming)\b"), "a homework machine"),
        (new(@"\b(girlfriend|boyfriend|crush|date me|love me|marry)\b"), "a dating app"),
        (new(@"\b(songs?|music|sing\w*|lyrics)\b"), "a jukebox"),
        (new(@"\b(doctor|sick|medicine|headache|fever)\b"), "a doctor"),
        (new(@"\b(money|stocks?|crypto|bitcoin|invest\w*|bank)\b"), "a bank"),
        (new(@"\b(jokes?|funny)\b"), "a comedian (though I do try)"),
    ];

    /// <summary>What the owner mistook the controller for ("a calculator"), or null when the sentence is about controllers,
    /// games or the app, or isn't clearly about anything else.</summary>
    public static string? OffTopic(string sentence)
    {
        // as typed first, then with typos read as meant ("stickss" is still about sticks)
        string lower = sentence.ToLowerInvariant();
        if (Regex.IsMatch(lower, @"\b(max(?:imum)? output|vertical (axis )?ratio|axis ratio|square gate|mouse mode)\b")
            || OnTopic.IsMatch(lower) || OnTopic.IsMatch(AgentSpelling.Fix(sentence).ToLowerInvariant())) return null;
        foreach (var (words, thing) in Mistaken) if (words.IsMatch(lower)) return thing;
        // a question about something else entirely
        return Regex.IsMatch(lower, @"^\s*(who|what|where|when|why|how|is|are|can|could|will|do|does)\b.*\?\s*$") ? "a search engine" : null;
    }

    /// <summary>How a turn went: failed when it broke the rules, couldn't be done or wasn't understood; unsure when it guessed,
    /// asked back, couldn't confirm or was off topic; otherwise good.</summary>
    public static AgentMood Mood(AgentTurn turn)
    {
        if (!turn.Verdict.Valid || turn.Outcome.Error != null || turn.Verdict.Proposal is not { } p) return AgentMood.Failed;
        string said = (p.Speech + " " + p.Recap).ToLowerInvariant();
        if (new[] { "flew right past my sensors", "didn't catch", "did not catch", "don't understand", "didn't understand" }.Any(said.Contains)) return AgentMood.Failed;
        if (p.Assumptions.Count > 0) return AgentMood.Unsure;
        if (p.Actions.Count > 0) return AgentMood.Good;
        return IsSilly(said) || said.Contains("can't confirm") || said.Contains("not sure") || said.Contains("can't change that yet") || p.Speech.TrimEnd().EndsWith('?') ? AgentMood.Unsure : AgentMood.Good;
    }
}
