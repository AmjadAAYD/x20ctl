using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace X20Ctl.Zone.Agent;

/// <summary>One thing an owner can ask for offline: an id, what it means (also what the phrasebook was
/// written from), hand-written examples, and how the request becomes actions once the sticks, buttons and numbers in the
/// sentence are known.</summary>
public sealed record OfflineIntent(string Id, string Meaning, string[] Seeds, Func<OfflineSlots, OfflinePlan> Build);

/// <summary>What a sentence names: sticks, triggers, buttons (in the order said), paddles, numbers and a curve word.</summary>
public sealed class OfflineSlots
{
    public readonly List<string> Sticks = new(), Triggers = new(), Buttons = new(), Paddles = new(), Assumed = new();
    public readonly List<double> Numbers = new();
    public string? Preset { get; set; }
    /// <summary>The sentence talks about aiming or the camera (the right stick) or moving (the left).</summary>
    public string? Implied { get; set; }
    public string Text { get; set; } = "";
}

/// <summary>An intent's answer: actions (action, target, value) and the words around them.</summary>
public sealed record OfflinePlan(IReadOnlyList<(string Action, string Target, object Value)> Actions, string Speech, string Recap, IReadOnlyList<string> Assumptions)
{
    public IReadOnlyList<int> Sources { get; init; } = [];
    public string Question { get; init; } = "";
    public static OfflinePlan Say(string speech, string recap = "") => new([], speech, recap, []) { Question = System.Text.RegularExpressions.Regex.IsMatch(speech, @"^(Which|What maximum|What vertical|To what|Reset what)\b") ? speech : "" };
}

/// <summary>
/// Offline mode (13 Oct 2026): Agent XCTL with no language model. A phrasebook of things owners say,
/// written ahead of time from the hand-written examples here, is matched against the
/// sentence by TF-IDF cosine similarity over words and word pairs, after the sticks, buttons, paddles and numbers are
/// lifted out as slots. The closest example's intent builds the reply in the same JSON the model writes, so the
/// validator, the drift reading and the walk-through work exactly as they do online. It answers in milliseconds,
/// says plainly when it didn't understand, and never invents a setting.
/// </summary>
public sealed class OfflineAgent : IAgentModel
{
    public string Name => "Offline phrasebook";
    public const double Threshold = 0.3;
    public static readonly OfflineIntent[] Intents = BuildIntents();
    static readonly Lazy<Index> shared = new(() => new Index(Examples(Phrasebook())));
    readonly Index index;
    /// <summary>The agent with the embedded phrasebook.</summary>
    public OfflineAgent() { index = shared.Value; }
    /// <summary>The agent with another phrasebook (none: the hand-written examples only), for comparing them.</summary>
    public OfflineAgent(IReadOnlyDictionary<string, string[]>? phrasebook) { index = new Index(Examples(phrasebook ?? new Dictionary<string, string[]>())); }

    public Task<ModelReply> Chat(IReadOnlyList<ChatMessage> messages, string schema, CancellationToken cancel)
    {
        var clock = Stopwatch.StartNew();
        string sentence = messages.LastOrDefault(m => m.Role == "user")?.Content ?? "";
        // Carry only a pending clarification, not arbitrary prior requests, into a short answer.
        var previous = messages.Take(messages.Count - 1).LastOrDefault(m => m.Role == "assistant");
        if (previous?.Content.TrimEnd().EndsWith('?') == true && Regex.Matches(sentence, @"\S+").Count <= 4
            && !Regex.IsMatch(sentence, @"\b(turn|set|make|open|show|switch|reset|stop)\b", RegexOptions.IgnoreCase))
        {
            var request = messages.Take(messages.Count - 1).LastOrDefault(m => m.Role == "user");
            if (request != null) sentence = request.Content + " " + sentence;
        }
        sentence = OfflineSupport.FollowUp(messages, sentence);
        var plan = Answer(sentence);
        if (Regex.IsMatch(sentence, @"\b(slightly|tiny|a (little|bit))\b", RegexOptions.IgnoreCase)
            && !Regex.IsMatch(sentence, @"\d+\s*,\s*\d+\s*,\s*\d+\s*,\s*\d+")
            && plan.Actions.Any(a => a.Action is AgentVocabulary.StickCurvePoints or AgentVocabulary.TriggerCurvePoints))
        {
            string settings = messages.First(m => m.Role == "system").Content.Split("My settings right now:").Last();
            plan = Plan(Slots(sentence), plan.Actions.Select(a =>
            {
                if (a.Action is not (AgentVocabulary.StickCurvePoints or AgentVocabulary.TriggerCurvePoints)) return a;
                CurvePoints.TryParse((string)a.Value, out var desired, out _);
                var line = Regex.Match(settings, Regex.Escape(AgentApplier.Name(a.Target)) + @" curve: ([^\r\n]+)", RegexOptions.IgnoreCase).Groups[1].Value;
                var custom = Regex.Match(line, @"custom (\d+)→(\d+), (\d+)→(\d+)");
                int[] p;
                if (custom.Success) p = custom.Groups.Cast<Group>().Skip(1).Select(g => int.Parse(g.Value, CultureInfo.InvariantCulture)).ToArray();
                else if (CurveDraft.Presets.TryGetValue(line, out var preset)) p = [ (int)Math.Round(preset.Item1.Item1 / 2.55), (int)Math.Round(preset.Item1.Item2 / 2.55), (int)Math.Round(preset.Item2.Item1 / 2.55), (int)Math.Round(preset.Item2.Item2 / 2.55) ];
                else p = [33,33,67,67];
                p[1] = Math.Clamp(p[1] + (desired.Y1 < desired.X1 ? -2 : 2), 0, 100);
                p[3] = Math.Clamp(p[3] + (desired.Y2 < desired.X2 ? -2 : 2), 0, 100);
                return (a.Action, a.Target, (object)string.Join(",", p));
            }).ToArray());
        }
        // a question the docs answer by name ("I closed the app but it's still running"): a short answer from that section,
        // cited. Only when the section's own heading matches the question, so an unrelated request never gets a README dump
        string? docs = messages.LastOrDefault(m => m.Role == "system" && m.Content.StartsWith("From the X20CTL docs", StringComparison.Ordinal))?.Content;
        if (docs != null && plan.Actions.Count == 0 && plan.Question.Length == 0
            && Regex.Match(docs, @"^\[1\] (?<where>.+?) › (?<section>.+?): (?<text>.+)$", RegexOptions.Multiline) is { Success: true } top
            && Answers(top.Groups["section"].Value, sentence))
            plan = OfflinePlan.Say($"From {top.Groups["section"].Value}:", Sentences(top.Groups["text"].Value, 3)) with { Sources = [1] };
        return Task.FromResult(new ModelReply(Json(plan), 0, 0, 0, clock.Elapsed.TotalMilliseconds));
    }

    /// <summary>A doc heading names the question: most of its words (three letters or more, not filler) are in the sentence.</summary>
    static bool Answers(string heading, string sentence)
    {
        static HashSet<string> Words(string t) => Regex.Matches(t.ToLowerInvariant().Replace("'", ""), @"[a-z]{3,}").Select(m => m.Value)
            .Where(w => w is not ("the" or "and" or "but" or "you" or "your" or "its" or "for" or "with" or "what" or "when" or "only" or "this" or "that")).ToHashSet();
        var head = Words(heading); var said = Words(sentence);
        return head.Count >= 2 && head.Count(said.Contains) * 2 >= head.Count + 1;
    }

    /// <summary>The intent closest to a sentence, and how close (cosine, 0 to 1).</summary>
    public (OfflineIntent? Intent, double Score) Classify(string sentence)
    {
        var (id, score) = index.Nearest(Tokens(sentence), Allowed(sentence));
        return (score >= Threshold ? Intents.First(i => i.Id == id) : null, score);
    }

    public OfflinePlan Answer(string sentence)
    {
        // what the app sends back mid-turn: a drift reading, or why the last reply was refused
        if (sentence.StartsWith("The app measured", StringComparison.Ordinal) || sentence.StartsWith("The app couldn't measure", StringComparison.Ordinal)) return Measured(sentence);
        if (sentence.StartsWith("The app rejected that reply", StringComparison.Ordinal))
        {
            string why = Regex.Match(sentence, @"nothing changed: (.*?) Answer again", RegexOptions.Singleline) is { Success: true } m ? m.Groups[1].Value : "";
            return OfflinePlan.Say("I can't do that, so I changed nothing. " + why.Trim());
        }
        sentence = OfflineSupport.Normalize(AgentGrammar.Plain(sentence));
        if (OfflineSupport.Explain(sentence) is { } explanation) return explanation;
        sentence = AgentSpelling.Fix(sentence);
        string lower = sentence.ToLowerInvariant();
        if (IsQuestion(lower)) return AgentCharacter.OffTopic(sentence) is { } unrelated ? OfflinePlan.Say(AgentCharacter.Silly(unrelated)) : Question(lower);
        var slots = Slots(sentence);
        var stickTargets = slots.Sticks.Count > 0 ? slots.Sticks : slots.Implied != null ? [slots.Implied] : AgentVocabulary.Sticks.ToList();
        var pointText = Regex.Match(sentence, @"\d+\s*,\s*\d+\s*,\s*\d+\s*,\s*\d+");
        if (pointText.Success && Regex.IsMatch(lower, @"\b(curve|points?|sticks?|triggers?|aim)\b"))
        {
            bool trigger = slots.Triggers.Count > 0 || lower.Contains("trigger");
            var targets = trigger ? slots.Triggers.Count > 0 ? slots.Triggers : AgentVocabulary.Triggers.ToList() : stickTargets;
            return Plan(slots, targets.Select(t => (trigger ? AgentVocabulary.TriggerCurvePoints : AgentVocabulary.StickCurvePoints, t, (object)pointText.Value)).ToArray());
        }
        var clauses = Regex.Split(sentence, @"\s+(?:and(?: then)?|also|then)\s+(?=(?:set|make|turn|switch|enable|disable|remap|swap|reset)\b)", RegexOptions.IgnoreCase);
        if (clauses.Length > 1)
        {
            var plans = clauses.Select(Answer).ToList();
            if (plans.All(p => p.Actions.Count > 0 && p.Question.Length == 0)) return Merge(plans);
            return new([],"I need the missing detail before combining those requests.","Try one complete command per setting.",[]) { Question = plans.FirstOrDefault(p => p.Question.Length > 0)?.Question ?? "Which control and value should each change use?" };
        }
        if (slots.Numbers.Count > 1 && slots.Sticks.Count + slots.Triggers.Count + slots.Buttons.Count > 1)
            return new([],"Those controls have multiple values, so I won't guess which goes where.","",[]) { Question="Please name each setting and value, for example: set left stick dead zone to 5 and set right stick dead zone to 8." };
        if (Regex.IsMatch(lower, @"\b(max(?:imum)? output|cap|limit)\b") && lower.Contains("output"))
            return slots.Numbers.Count > 0 ? Plan(slots, stickTargets.Select(t => (AgentVocabulary.StickMaxOutput, t, (object)slots.Numbers[0])).ToArray()) : OfflinePlan.Say("What maximum output percentage should I use?");
        if (Regex.IsMatch(lower, @"\b(vertical|axis)\b.*\bratio\b"))
            return slots.Numbers.Count > 0 ? Plan(slots, stickTargets.Select(t => (AgentVocabulary.StickAxisRatio, t, (object)slots.Numbers[0])).ToArray()) : OfflinePlan.Say("What vertical axis ratio percentage should I use?");
        if (Regex.IsMatch(lower, @"\bsquare gate\b"))
            return Plan(slots, stickTargets.Select(t => (AgentVocabulary.StickSquareGate, t, (object)(Regex.IsMatch(lower, @"\b(off|disable|stop)\b") ? "off" : "on"))).ToArray());
        if (Regex.IsMatch(lower, @"\b(turbo|rapid fire)\b") && !IsQuestion(lower) && Slots(sentence).Buttons.Count == 0)
            return OfflinePlan.Say("Which button should get turbo?");
        if (Regex.IsMatch(lower, @"\b(rgb|leds?|lights?|lighting|colou?rs?|vibration|vibrate|rumble|haptics?|battery|firmware|gyro|motion|volume|sound|mic|microphone|bluetooth)\b"))
            return OfflinePlan.Say("I can't change that yet. I can set sticks, triggers, buttons, turbo, paddles, PC mode, and measure drift.");
        if (Regex.IsMatch(lower, @"^\s*(hi|hello|hey|help|what can you do|who are you)\W*$")) return Intents.First(i => i.Id == "greeting").Build(new OfflineSlots());
        if (Regex.IsMatch(lower, @"\bmouse\s*mode\b"))
            return Intents.First(i => i.Id == (Regex.IsMatch(lower, @"\b(off|disable|stop)\b") ? "mouse_mode_off" : "mouse_mode_on")).Build(Slots(sentence));
        if (Regex.IsMatch(lower, @"\b(open|show|go to|take me to|diagnostics?|diagnose)\b")
            && AgentVocabulary.Pages.FirstOrDefault(page => Regex.IsMatch(lower, $@"\b{Regex.Escape(page.ToLowerInvariant())}\b") || page == "Diagnostics" && Regex.IsMatch(lower, @"\bdiagnos(tic|e)\b")) is { } page)
            return Plan(Slots(sentence), (AgentVocabulary.OpenPage, AgentVocabulary.PageTarget, page));
        // not about controllers at all: in character
        if (AgentCharacter.OffTopic(sentence) is { } thing) return OfflinePlan.Say(AgentCharacter.Silly(thing));
        if (IsQuestion(lower)) return Question(lower);

        // A button and a number alone need a pending question; otherwise they aren't a command.
        if (Regex.IsMatch(lower, @"^\s*(a|b|x|y|lb|rb|lt|rt)\s+(?:at\s+)?\d+\s*$")) return NotUnderstood();

        // stick speed in any words: "make the camera slower", "reduce stick speed", "how to make the aim slower"
        if (Speed(lower) is { } slower)
        {
            var targets = slots.Sticks.Count > 0 || slots.Implied != null ? stickTargets
                : Regex.IsMatch(lower, @"\b(turns?|turning|rotat\w*|view)\b") ? ["RightStick"] : AgentVocabulary.Sticks.ToList();
            return Plan(slots, targets.Select(t => (AgentVocabulary.StickCurvePoints, t, (object)(slower ? "30,23,72,65" : "25,33,70,79"))).ToArray());
        }

        // several requests in one sentence: each part on its own, when each part is clearly a different request
        var parts = Regex.Split(sentence, @"\s*(?:,|;|\band then\b|\bthen\b|\balso\b|\band\b|\bplus\b)\s*", RegexOptions.IgnoreCase).Where(p => p.Trim().Length > 0).ToList();
        if (parts.Count > 1)
        {
            var found = parts.Select(p => (Part: p, Hit: Classify(p))).ToList();
            double whole = Classify(sentence).Score;
            // only when every part is clearly its own request, closer to the phrasebook than the sentence as a whole
            if (found.All(f => f.Hit.Intent != null && Tokens(f.Part).Count >= 2 && f.Hit.Score > whole) && found.Select(f => f.Hit.Intent!.Id).Distinct().Count() == found.Count)
                return Merge(found.Select(f => f.Hit.Intent!.Build(Slots(f.Part))).ToList());
        }
        var (intent, _) = Classify(sentence);
        return intent == null ? NotUnderstood(sentence) : intent.Build(Slots(sentence));
    }

    /// <summary>A sentence about how fast the sticks, aim or camera turn: true for slower, false for faster, null when it
    /// isn't one, says both, or names a number, a trigger, the mouse, turbo or drift.</summary>
    public static bool? Speed(string lower)
    {
        if (!Regex.IsMatch(lower, @"\b(sticks?|aim|aiming|camera|look|looking|turns?|turning|movement|moving|sensitivity|crosshair|speed)\b")
            || Regex.IsMatch(lower, @"\d|\b(triggers?|lt|rt|mouse|turbo|drifts?|drifting|by itself|on its own|dead ?zone|curve)\b")) return null;
        bool slower = Regex.IsMatch(lower, @"\btoo (fast|quick|sensitive|twitchy|jumpy|high)\b|(?<!\btoo )\bslow(er)?\b|\bslow (it |them )?down\b|\bless (sensitive|fast|speed|sensitivity)\b|\b(reduce|lower|decrease|drop|cut|turn down|lessen)\b.*\b(speed|sensitivity)\b");
        bool faster = Regex.IsMatch(lower, @"\btoo (slow|sluggish|low)\b|\b(faster|quicker)\b|\bspeed (it |them )?up\b|\bmore (sensitive|responsive)\b|\b(increase|raise|boost|turn up|higher)\b.*\b(speed|sensitivity)\b");
        return slower == faster ? null : slower;
    }

    static string Sentences(string text, int count) => string.Join(" ", Regex.Split(text.Trim(), @"(?<=[.!?])\s+").Take(count));

    /// <summary>
    /// What the sentence's own words rule out, before any matching: a sentence that names a stick and no trigger isn't a
    /// trigger request (and the other way round), and switching something off needs a word that says off.
    /// </summary>
    public static Func<string, bool> Allowed(string sentence)
    {
        string lower = " " + sentence.ToLowerInvariant() + " ";
        bool stick = Regex.IsMatch(lower, @"\b(sticks?|analogs?|thumb ?sticks?|joysticks?|ls|rs|aim|aiming|camera|crosshair)\b");
        bool trigger = Regex.IsMatch(lower, @"\b(triggers?|lt|rt|l2|r2|throttle|brakes?|gas|pedals?)\b");
        bool off = Regex.IsMatch(lower, @"\b(off|stop|disable|kill|remove|no more|get rid|turn it off|without)\b");
        return id =>
            !(id.StartsWith("trigger_") && stick && !trigger) && !(id.StartsWith("stick_") && trigger && !stick)
            && !(id.StartsWith("mouse_mode") && !lower.Contains("mouse")) && !(id == "open_page" && !AgentVocabulary.Pages.Any(p => lower.Contains(p.ToLowerInvariant())))
            && !(id.EndsWith("_off") && !off) && !(id.EndsWith("_on") && off);
    }

    /// <summary>A question about a setting, answered with its meaning rather than a change.</summary>
    public static bool IsQuestion(string lower) =>
        Regex.IsMatch(lower, @"^\s*(what|why|explain|tell me about|is it|does|do i need|should i|which)\b|^\s*how (does|do|is|would)\b(?!.*\b(make|set|change|get|fix|turn)\b)");

    static OfflinePlan NotUnderstood(string sentence = "")
    {
        var suggestions = AgentIdeas.Search(sentence,3);
        string examples = suggestions.Count > 0 ? "Try: " + string.Join(" · ",suggestions.Select(s => "\"" + s.Say + "\""))
            : "Try \"make my sticks smooth\", \"hair triggers\" or \"turbo on X at 12\".";
        return OfflinePlan.Say(AgentCharacter.NotUnderstood + " " + examples, "Say HELP for situations and example commands. Nothing was changed.");
    }

    static OfflinePlan Merge(IReadOnlyList<OfflinePlan> plans) => new(
        plans.SelectMany(p => p.Actions).GroupBy(a => (a.Action, a.Target)).Select(g => g.Last()).ToList(),
        string.Join(" ", plans.Select(p => p.Speech)), string.Join(" ", plans.Select(p => p.Recap).Where(r => r.Length > 0)), plans.SelectMany(p => p.Assumptions).Distinct().ToList());

    // ---- slots ----
    static readonly (string Pattern, string Button)[] ButtonWords =
    [
        (@"\b(lb|l1|left bumper|left shoulder)\b", "LB"), (@"\b(rb|r1|right bumper|right shoulder)\b", "RB"),
        (@"\b(lt|l2|left trigger)\b", "LT"), (@"\b(rt|r2|right trigger)\b", "RT"),
        (@"\b(l3|left stick (click|press))\b", "L3"), (@"\b(r3|right stick (click|press))\b", "R3"),
        (@"\b(d-?pad|dpad) ?up\b|\bup on the d-?pad\b", "DPAD_UP"), (@"\b(d-?pad|dpad) ?down\b|\bdown on the d-?pad\b", "DPAD_DOWN"),
        (@"\b(d-?pad|dpad) ?left\b|\bleft on the d-?pad\b", "DPAD_LEFT"), (@"\b(d-?pad|dpad) ?right\b|\bright on the d-?pad\b", "DPAD_RIGHT"),
        (@"\bstart button\b|\bmenu button\b|\bstart\b(?! over| again)|\bmenu\b", "START"), (@"\b(select|view button|back button|share button)\b", "SELECT"),
        (@"\b(home|guide|xbox) button\b", "HOME"),
        // a single letter: B, X and Y as written; "a" only where it can't be the article
        (@"(?<![\w'])[bxy](?![\w'])", ""), (@"(?<![\w'])a(?= button\b)|(?<=\b(?:swap|remap|map|replace|make|on|to|as|onto|with|and|press|like)\s)a(?=\s*(?:$|[,.?!]|\band\b|\bbutton\b|\bwith\b|\bto\b|\bonto\b|\bsend\b|\bact\b|\bat\b|\d))", "A"),
    ];

    public static OfflineSlots Slots(string sentence)
    {
        string text = sentence.ToLowerInvariant().Replace('’', '\'');
        var s = new OfflineSlots { Text = text };
        bool left = Regex.IsMatch(text, @"\b(left|l)[\s-]*(analog|thumb ?stick|joystick|stick)\b|\bls\b|\bmovement stick\b|\bmove stick\b");
        bool right = Regex.IsMatch(text, @"\b(right|r)[\s-]*(analog|thumb ?stick|joystick|stick)\b|\brs\b|\b(aim|aiming|camera|look) stick\b");
        if (left) s.Sticks.Add("LeftStick");
        if (right) s.Sticks.Add("RightStick");
        if (!left && !right && Regex.IsMatch(text, @"\b(sticks|analogs|thumb ?sticks|joysticks)\b")) s.Sticks.AddRange(AgentVocabulary.Sticks);
        if (Regex.IsMatch(text, @"\b(aim|aiming|camera|look|looking|crosshair|sniping|snipe|sniper|reticle)\b")) s.Implied = "RightStick";
        else if (Regex.IsMatch(text, @"\b(walk|walking|moving|movement|character|run|running|strafe)\b")) s.Implied = "LeftStick";
        bool lt = Regex.IsMatch(text, @"\b(left|l)[\s-]*triggers?\b|\blt\b|\bl2\b"), rt = Regex.IsMatch(text, @"\b(right|r)[\s-]*triggers?\b|\brt\b|\br2\b");
        if (lt) s.Triggers.Add("LeftTrigger");
        if (rt) s.Triggers.Add("RightTrigger");
        if (!lt && !rt && Regex.IsMatch(text, @"\b(both triggers|triggers)\b")) s.Triggers.AddRange(AgentVocabulary.Triggers);

        var buttons = new List<(int At, string Button)>();
        foreach (var (pattern, button) in ButtonWords)
            foreach (Match m in Regex.Matches(text, pattern))
                buttons.Add((m.Index, button.Length > 0 ? button : m.Value.ToUpperInvariant()));
        foreach (Match m in Regex.Matches(text, @"\bjump\b")) { buttons.Add((m.Index, "A")); s.Assumed.Add("Jump is A in most games."); }
        foreach (Match m in Regex.Matches(text, @"\bboost\b")) { buttons.Add((m.Index, "B")); s.Assumed.Add("Boost is B in Rocket League."); }
        s.Buttons.AddRange(buttons.OrderBy(b => b.At).Select(b => b.Button).Distinct());

        foreach (Match m in Regex.Matches(text, @"(?:\b(?:m|p|paddle\s*|back paddle\s*))([1-6])\b")) s.Paddles.Add("M" + m.Groups[1].Value);
        foreach (Match m in Regex.Matches(text, @"(?<![a-z\d.])(-?\d+(?:\.\d+)?)(?![\d])")) s.Numbers.Add(double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture));
        s.Preset = Regex.Match(text, @"\b(smooth|smoother|quick|quicker|fast|faster|responsive|snappy|slow|slower|precise|precision|fine|default|normal|linear)\b") is { Success: true } p
            ? p.Value switch { "smooth" or "smoother" => "smooth", "quick" or "quicker" or "fast" or "faster" or "responsive" or "snappy" => "quick", "slow" or "slower" or "precise" or "precision" => "slow", "fine" => "fine", _ => "default" } : null;
        return s;
    }

    // ---- the matcher: TF-IDF over words and word pairs, nearest example wins ----
    static readonly HashSet<string> Stop = ["my", "the", "i", "please", "can", "you", "could", "would", "me", "it", "its", "is", "be", "so", "just", "an", "this", "that", "im", "i'm", "id", "i'd", "some", "hey", "pls", "plz", "ok", "okay", "now", "will", "do", "does", "are", "of", "in"];

    public static List<string> Tokens(string sentence)
    {
        string t = " " + sentence.ToLowerInvariant().Replace('’', '\'') + " ";
        // slots become placeholders, so "left stick 8" and "right stick 12" read the same
        t = Regex.Replace(t, @"\b(left|right|l|r)[\s-]*(analog|thumb ?stick|joystick|stick)s?\b|\b(ls|rs)\b", " STICK ");
        t = Regex.Replace(t, @"\b(left|right|l|r)[\s-]*triggers?\b|\b(lt|rt|l2|r2)\b", " TRIGGER ");
        t = Regex.Replace(t, @"\b(m[1-6]|p[1-6]|paddle\s*[1-6])\b", " PADDLE ");
        t = Regex.Replace(t, @"\b(lb|rb|l1|r1|l3|r3)\b|(?<![\w'])[bxy](?![\w'])|(?<![\w'])a(?= button\b)|(?<=\b(?:swap|on|to|as|onto|with|and)\s)a(?=\s*(?:$|[,.?!]|\band\b|\bbutton\b|\bat\b|\d))", " BTN ");
        t = Regex.Replace(t, @"(?<![a-z\d.])\d+(?:\.\d+)?\s*(%|percent)?", " NUM ");
        t = Regex.Replace(t, @"\b(sticks?|analogs?|thumb ?sticks?|joysticks?)\b", " STICK ");
        t = Regex.Replace(t, @"\btriggers?\b", " TRIGGER ");
        t = t.Replace("dead zone", "deadzone").Replace("dead-zone", "deadzone").Replace("anti deadzone", "antideadzone").Replace("anti-deadzone", "antideadzone").Replace("pc mode", "pcmode").Replace("full speed", "fullspeed");
        var words = Regex.Matches(t, @"[A-Za-z']+").Select(m => m.Value).Where(w => !Stop.Contains(w)).Select(Stem).ToList();
        var tokens = new List<string>(words);
        for (int i = 0; i + 1 < words.Count; i++) tokens.Add(words[i] + "_" + words[i + 1]);
        return tokens;
    }

    static string Stem(string w)
    {
        if (w.Length > 0 && char.IsUpper(w[0])) return w; // a placeholder
        if (w.EndsWith("ing") && w.Length > 5) return w[..^3];
        if (w.EndsWith("ed") && w.Length > 4) return w[..^2];
        if (w.EndsWith('s') && !w.EndsWith("ss") && w.Length > 3) return w[..^1];
        return w;
    }

    internal sealed class Index
    {
        readonly List<(string Intent, double Weight, Dictionary<string, double> Vector)> rows = new();
        const int Neighbours = 7;
        readonly Dictionary<string, double> idf = new();
        public Index(IReadOnlyList<(string Intent, string Sentence, double Weight)> examples)
        {
            var docs = examples.Select(e => (e.Intent, e.Weight, Tokens: Tokens(e.Sentence))).ToList();
            foreach (var token in docs.SelectMany(d => d.Tokens.Distinct())) idf[token] = idf.GetValueOrDefault(token) + 1;
            foreach (var token in idf.Keys.ToList()) idf[token] = Math.Log((1.0 + docs.Count) / (1.0 + idf[token])) + 1;
            foreach (var d in docs) rows.Add((d.Intent, d.Weight, Vector(d.Tokens)));
        }
        Dictionary<string, double> Vector(IEnumerable<string> tokens)
        {
            var v = tokens.GroupBy(t => t).ToDictionary(g => g.Key, g => g.Count() * idf.GetValueOrDefault(g.Key, 0));
            double norm = Math.Sqrt(v.Values.Sum(x => x * x));
            if (norm > 0) foreach (var k in v.Keys.ToList()) v[k] /= norm;
            return v;
        }
        public (string Intent, double Score) Nearest(List<string> tokens, Func<string, bool>? allowed = null)
        {
            var q = Vector(tokens);
            if (q.Count == 0) return ("", 0);
            // the closest examples vote, weighted by how close they are (hand-written ones count more): one stray
            // paraphrase can't decide alone. The score is the winner's closest example.
            var near = rows.Where(r => allowed == null || allowed(r.Intent)).Select(r => (r.Intent, r.Weight, Sim: Dot(q, r.Vector))).Where(r => r.Sim > 0).OrderByDescending(r => r.Sim).Take(Neighbours).ToList();
            if (near.Count == 0) return ("", 0);
            var winner = near.GroupBy(r => r.Intent).OrderByDescending(g => g.Sum(r => r.Sim * r.Weight)).First();
            return (winner.Key, winner.Max(r => r.Sim));
        }
        public int Count => rows.Count;
        /// <summary>How the nearest examples vote for one row's sentence with that row left out (for cleaning).</summary>
        public Dictionary<string, double> VotesWithout(int row) =>
            rows.Select((r, i) => (r.Intent, r.Weight, Sim: i == row ? 0 : Dot(rows[row].Vector, r.Vector))).Where(r => r.Sim > 0).OrderByDescending(r => r.Sim).Take(Neighbours)
                .GroupBy(r => r.Intent).ToDictionary(g => g.Key, g => g.Sum(r => r.Sim * r.Weight));
        static double Dot(Dictionary<string, double> q, Dictionary<string, double> v)
        {
            double dot = 0; foreach (var (k, x) in q) if (v.TryGetValue(k, out var y)) dot += x * y;
            return dot;
        }
    }

    /// <summary>The hand-written examples plus the phrasebook written from them (an embedded file).</summary>
    public const double SeedWeight = 2;
    public static IReadOnlyList<(string Intent, string Sentence, double Weight)> Examples(IReadOnlyDictionary<string, string[]> phrasebook)
    {
        var all = Intents.SelectMany(i => i.Seeds.Select(s => (i.Id, s, SeedWeight))).ToList();
        foreach (var (intent, sentences) in phrasebook) if (Intents.Any(i => i.Id == intent)) all.AddRange(sentences.Select(s => (intent, s, 1.0)));
        return all;
    }
    public static IReadOnlyDictionary<string, string[]> Phrasebook()
    {
        using var stream = typeof(OfflineAgent).Assembly.GetManifestResourceStream("X20Ctl.Zone.Agent.phrasebook.json");
        if (stream == null) return new Dictionary<string, string[]>();
        return JsonSerializer.Deserialize<Dictionary<string, string[]>>(stream) ?? new();
    }

    // ---- the replies ----
    static string Called(string target) => AgentVocabulary.Sticks.Contains(target) || AgentVocabulary.Triggers.Contains(target) ? AgentApplier.Name(target).ToLowerInvariant() : target;
    static string Words(IEnumerable<string> parts) { var p = parts.ToList(); return p.Count <= 1 ? string.Concat(p) : string.Join(", ", p.Take(p.Count - 1)) + " and " + p[^1]; }
    static string Pct(object v) => Convert.ToString(v, CultureInfo.InvariantCulture) + "%";

    static string Phrase(string action, IReadOnlyList<string> targets, object value) => action switch
    {
        AgentVocabulary.StickDeadzone or AgentVocabulary.TriggerDeadzone => $"set my {Words(targets.Select(Called))} dead zone to {Pct(value)}",
        AgentVocabulary.StickFullAt => $"make my {Words(targets.Select(Called))} reach full speed at {Pct(value)}",
        AgentVocabulary.TriggerFullAt => $"make my {Words(targets.Select(Called))} count as fully pressed at {Pct(value)}",
        AgentVocabulary.StickAntiDeadzone => $"set my {Words(targets.Select(Called))} anti-dead zone to {Pct(value)}",
        AgentVocabulary.StickCurve or AgentVocabulary.TriggerCurve => $"give my {Words(targets.Select(Called))} the {value} curve",
        AgentVocabulary.Remap => $"make {Words(targets)} send {value}",
        AgentVocabulary.Turbo => Convert.ToInt32(value, CultureInfo.InvariantCulture) == 0 ? $"turn turbo off on {Words(targets)}" : $"put turbo on {Words(targets)} at {value} presses a second",
        AgentVocabulary.Reset => targets.Contains(AgentVocabulary.All) ? "put everything back to default" : $"put my {Words(targets.Select(Called))} back to default",
        AgentVocabulary.Paddle => $"put {value} on {Words(targets)}",
        AgentVocabulary.MeasureDrift => $"measure my {Words(targets.Select(Called))} first, so hands off the controller for a few seconds",
        AgentVocabulary.StickCurvePoints or AgentVocabulary.TriggerCurvePoints => $"shape my {Words(targets.Select(Called))} with points {value} (push, output)",
        AgentVocabulary.StickMaxOutput => $"cap my {Words(targets.Select(Called))} output at {Pct(value)}",
        AgentVocabulary.StickAxisRatio => $"set my {Words(targets.Select(Called))} vertical ratio to {Pct(value)}",
        AgentVocabulary.StickSquareGate => $"switch my {Words(targets.Select(Called))} square gate {value}",
        AgentVocabulary.MouseMode => $"switch mouse mode {value}",
        AgentVocabulary.OpenPage => $"open {value}",
        _ => $"switch PC mode {value}",
    };

    /// <summary>A plan from actions: "I'll …" from each action, and a recap from each setting's meaning.</summary>
    static OfflinePlan Plan(OfflineSlots s, params (string Action, string Target, object Value)[] actions)
    {
        var groups = actions.GroupBy(a => (a.Action, Value: Convert.ToString(a.Value, CultureInfo.InvariantCulture))).ToList();
        string speech = "I'll " + Words(groups.Select(g => Phrase(g.Key.Action, g.Select(a => a.Target).ToList(), g.First().Value))) + ".";
        var recap = new List<string>();
        foreach (var action in actions.Select(a => a.Action).Distinct())
        {
            var m = AgentKnowledge.Of(action);
            string line = $"{m.Name}: {m.What}.";
            if (action is AgentVocabulary.StickCurve or AgentVocabulary.TriggerCurve && actions.First(a => a.Action == action).Value is string preset) line += " " + AgentKnowledge.Preset(preset).TrimEnd('.') + ".";
            else if (m.Good.Length > 0) line += $" Good for {m.Good}.";
            if (m.Careful.Length > 0) line += $" Watch out: {m.Careful}.";
            recap.Add(line);
        }
        return new(actions, speech, string.Join(" ", recap.Take(3)), s.Assumed.Distinct().ToList());
    }

    static OfflinePlan Measured(string sentence)
    {
        if (sentence.StartsWith("The app couldn't measure", StringComparison.Ordinal))
            return OfflinePlan.Say("I couldn't measure, so I changed nothing.", Regex.Match(sentence, @"measure: (.*?) Now answer", RegexOptions.Singleline).Groups[1].Value.Trim());
        var actions = new List<(string, string, object)>(); var recap = new List<string>();
        foreach (Match m in Regex.Matches(sentence, @"(Left|Right) stick: .*?wandered up to ([\d.]+)%.*?(?:Smallest dead zone that hides it: (\d+)%|No drift worth a dead zone)", RegexOptions.Singleline))
        {
            string stick = m.Groups[1].Value + "Stick", name = m.Groups[1].Value.ToLowerInvariant() + " stick";
            if (m.Groups[3].Success)
            {
                actions.Add((AgentVocabulary.StickDeadzone, stick, int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture)));
                recap.Add($"My {name} wandered up to {m.Groups[2].Value}% with nobody touching it, so a {m.Groups[3].Value}% dead zone hides that with a percent to spare.");
            }
            else recap.Add($"My {name} only wandered {m.Groups[2].Value}%, which no game will notice, so I left it alone.");
        }
        if (actions.Count == 0) return OfflinePlan.Say("No drift worth fixing: I changed nothing.", string.Join(" ", recap));
        var plan = Plan(new OfflineSlots(), [.. actions]);
        return plan with { Recap = string.Join(" ", recap) + " Watch out: " + AgentKnowledge.Of(AgentVocabulary.StickDeadzone).Careful + "." };
    }

    static OfflinePlan Question(string lower)
    {
        bool trigger = Regex.IsMatch(lower, @"\btriggers?\b");
        string? action =
            Regex.IsMatch(lower, @"max(?:imum)? output") ? AgentVocabulary.StickMaxOutput
            : Regex.IsMatch(lower, @"axis ratio|vertical ratio") ? AgentVocabulary.StickAxisRatio
            : Regex.IsMatch(lower, @"square gate") ? AgentVocabulary.StickSquareGate
            : Regex.IsMatch(lower, @"mouse mode") ? AgentVocabulary.MouseMode
            : Regex.IsMatch(lower, @"hair[\s-]*trigger|full[\s-]*(at|press)") ? AgentVocabulary.TriggerFullAt
            : Regex.IsMatch(lower, @"anti[\s-]*dead") ? AgentVocabulary.StickAntiDeadzone
            : Regex.IsMatch(lower, @"dead[\s-]*zone|deadzone|drift") ? (trigger ? AgentVocabulary.TriggerDeadzone : AgentVocabulary.StickDeadzone)
            : Regex.IsMatch(lower, @"full speed|outer") ? AgentVocabulary.StickFullAt
            : Regex.IsMatch(lower, @"curve|smooth|quick|slow|fine|preset") ? (trigger ? AgentVocabulary.TriggerCurve : AgentVocabulary.StickCurve)
            : Regex.IsMatch(lower, @"turbo|rapid") ? AgentVocabulary.Turbo
            : Regex.IsMatch(lower, @"remap|swap|rebind") ? AgentVocabulary.Remap
            : Regex.IsMatch(lower, @"paddle|\bm[1-6]\b") ? AgentVocabulary.Paddle
            : Regex.IsMatch(lower, @"pc mode") ? AgentVocabulary.PcMode : null;
        if (action == null) return OfflinePlan.Say("Ask me about dead zones, anti-dead zones, curves, hair triggers, turbo, remaps, paddles or PC mode, and I'll explain.");
        var m = AgentKnowledge.Of(action);
        var answer = new StringBuilder($"{m.Name}: {m.What}.");
        if (m.More.Length > 0) answer.Append($" Higher: {m.More}.");
        if (m.Less.Length > 0) answer.Append($" Lower: {m.Less}.");
        if (action is AgentVocabulary.StickCurve or AgentVocabulary.TriggerCurve)
            foreach (var preset in AgentVocabulary.Curves.Where(p => p != "default")) answer.Append(' ').Append(AgentKnowledge.Preset(preset));
        if (m.Good.Length > 0) answer.Append($" Good: {m.Good}.");
        if (m.Careful.Length > 0) answer.Append($" Bad: {m.Careful}.");
        return OfflinePlan.Say($"Here's what {m.Name.ToLowerInvariant()} does.", answer.ToString());
    }

    static string Json(OfflinePlan plan)
    {
        using var buffer = new MemoryStream();
        using (var w = new Utf8JsonWriter(buffer))
        {
            w.WriteStartObject();
            w.WriteString("speech", plan.Speech);
            w.WriteString("question", plan.Question);
            w.WriteStartArray("actions");
            foreach (var (action, target, value) in plan.Actions)
            {
                w.WriteStartObject(); w.WriteString("action", action); w.WriteString("target", target);
                if (value is string text) w.WriteString("value", text); else w.WriteNumber("value", Convert.ToDouble(value, CultureInfo.InvariantCulture));
                w.WriteString("reason", "offline phrasebook"); w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteString("recap", plan.Recap);
            w.WriteStartArray("assumptions"); foreach (var a in plan.Assumptions) w.WriteStringValue(a); w.WriteEndArray();
            w.WriteStartArray("sources"); foreach (var n in plan.Sources) w.WriteNumberValue(n); w.WriteEndArray();
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    // ---- what can be asked ----
    static OfflineIntent[] BuildIntents()
    {
        // the stick(s) a sentence means: named, else implied by aiming or moving, else the fallback
        static string[] Sticks(OfflineSlots s, params string[] fallback) => s.Sticks.Count > 0 ? [.. s.Sticks] : s.Implied != null ? [s.Implied] : fallback;
        static string[] Triggers(OfflineSlots s, params string[] fallback) => s.Triggers.Count > 0 ? [.. s.Triggers] : fallback;
        static double? First(OfflineSlots s) => s.Numbers.Count > 0 ? s.Numbers[0] : null;
        static object Num(double v) => v == Math.Floor(v) ? (int)v : v;
        string[] both = AgentVocabulary.Sticks, right = ["RightStick"], triggersBoth = AgentVocabulary.Triggers, rt = ["RightTrigger"];

        OfflinePlan Each(OfflineSlots s, string action, string[] targets, object value) => Plan(s, targets.Select(t => (action, t, value)).ToArray());
        OfflinePlan Number(OfflineSlots s, string action, string[] targets, string what) =>
            First(s) is { } n ? Each(s, action, targets, Num(n)) : OfflinePlan.Say($"To what? Give me a number for the {what}.");

        return
        [
            new("stick_sniping", "make a stick (usually the right, aiming one) precise for sniping or long range aim",
                ["Make my right stick good for sniping", "Make my right stick more precise for sniping", "I need precise aim for sniping", "Set me up for snipers", "Better long range aim", "I play sniper a lot, help my aim", "Make aiming more precise", "precision aiming please", "Slower for aiming please", "Make my right stick more precise"],
                s => Each(s, AgentVocabulary.StickCurvePoints, Sticks(s, right), "30,23,72,65")),
            new("stick_twitchy", "the aim or camera is too sensitive, twitchy or jumpy",
                ["My aim feels too twitchy", "My aim is too sensitive", "The camera moves way too fast", "My crosshair is jumpy", "Aim feels shaky", "Small moves make my aim jump too far", "I keep overshooting my targets"],
                s => Each(s, AgentVocabulary.StickCurvePoints, Sticks(s, right), "30,23,72,65")),
            new("stick_smooth", "make the sticks feel smooth and steady",
                ["Make my sticks smooth", "Smooth out my sticks", "I want smoother sticks", "Give my sticks a smooth feel", "Make the left stick smooth", "Steadier tracking please", "Make my stick movement less choppy"],
                s => Each(s, AgentVocabulary.StickCurvePoints, Sticks(s, both), "30,25,72,73")),
            new("stick_fast", "make the sticks faster or more responsive",
                ["Make my sticks faster", "I want more responsive sticks", "Faster turning", "My camera turns too slowly", "Make my right stick quicker", "Speed up my sticks", "My aim feels sluggish"],
                s => Each(s, AgentVocabulary.StickCurvePoints, Sticks(s, both), "25,33,70,79")),
            new("stick_curve_set", "set a stick curve by name: default, quick, slow, smooth or fine",
                ["Set my right stick curve to slow", "Use the fine curve on my left stick", "Put both sticks on the default curve", "Change the stick curve to quick", "Left stick curve smooth"],
                s => s.Preset is { } p ? Each(s, AgentVocabulary.StickCurve, Sticks(s, both), p) : OfflinePlan.Say("Which curve? default, quick, slow, smooth or fine.")),
            new("stick_drift", "a stick drifts, moves on its own, or the aim or character creeps without touching it",
                ["My right stick drifts a little", "My left stick moves on its own", "Fix my stick drift", "My camera keeps slowly turning by itself", "Check my sticks for drift", "My character walks by himself", "Stick drift", "The aim creeps when I let go", "My stick is drifting"],
                s => Each(s, AgentVocabulary.MeasureDrift, Sticks(s, both), 0)),
            new("stick_deadzone_set", "set a stick dead zone to a number",
                ["Set my left stick dead zone to 8", "Right stick dead zone 5", "Put the dead zone on both sticks at 4", "Change my stick deadzone to 10 percent", "Left stick deadzone to 3"],
                s => Number(s, AgentVocabulary.StickDeadzone, Sticks(s, both), "stick dead zone, 0 to 40")),
            new("stick_deadzone_remove", "remove or turn off the stick dead zone",
                ["Remove the dead zone on my sticks", "No dead zone on my right stick", "Turn off the stick dead zone", "Zero dead zone on both sticks"],
                s => Each(s, AgentVocabulary.StickDeadzone, Sticks(s, both), 0)),
            new("stick_full_at_set", "set where a stick reaches full speed (outer dead zone), as a number",
                ["Right stick full speed at 85", "Make my left stick reach max at 90", "Set the outer dead zone of my sticks to 80", "Full speed at 75 on the right stick"],
                s => Number(s, AgentVocabulary.StickFullAt, Sticks(s, both), "full speed point, 60 to 100")),
            new("stick_small_moves", "small stick movements don't register in a game (needs an anti-dead zone)",
                ["Small stick movements don't register in my game", "Tiny movements of my stick do nothing in game", "The game ignores small aim adjustments", "Add an anti dead zone", "My small corrections don't register"],
                s => First(s) is { } n ? Each(s, AgentVocabulary.StickAntiDeadzone, Sticks(s, both), Num(n)) : Each(s, AgentVocabulary.StickAntiDeadzone, Sticks(s, both), 10)),
            new("trigger_fast", "faster triggers, hair triggers for shooting",
                ["I want faster triggers for shooting", "Hair triggers for Call of Duty", "Make my triggers shoot faster", "Hair trigger on my right trigger", "Quick triggers for shooters", "Shorter trigger pull", "I want to shoot quicker"],
                s => Plan(s, [.. Triggers(s, rt).SelectMany(t => new[] { (AgentVocabulary.TriggerFullAt, t, (object)40), (AgentVocabulary.TriggerDeadzone, t, (object)0) })])),
            new("trigger_slow", "slow, progressive triggers, for racing throttle and brakes",
                ["Slow, progressive triggers for racing", "Better throttle control for racing games", "Make my triggers progressive", "I need finer brake control", "Racing setup for my triggers"],
                s => Each(s, AgentVocabulary.TriggerCurvePoints, Triggers(s, triggersBoth), "30,23,72,65")),
            new("trigger_curve_set", "set a trigger curve by name: default, quick, slow, smooth or fine",
                ["Set my right trigger curve to quick", "Left trigger curve smooth", "Put my triggers on the default curve", "Use the fine curve on my triggers"],
                s => s.Preset is { } p ? Each(s, AgentVocabulary.TriggerCurve, Triggers(s, triggersBoth), p) : OfflinePlan.Say("Which curve? default, quick, slow, smooth or fine.")),
            new("trigger_deadzone_set", "set a trigger dead zone to a number",
                ["Set my right trigger dead zone to 5", "Left trigger deadzone 3", "Trigger dead zone 2 on both triggers"],
                s => Number(s, AgentVocabulary.TriggerDeadzone, Triggers(s, triggersBoth), "trigger dead zone, 0 to 40")),
            new("trigger_deadzone_remove", "remove the trigger dead zone",
                ["Remove the dead zone on both triggers", "No dead zone on my triggers", "Turn off the trigger dead zone", "Zero trigger deadzone"],
                s => Each(s, AgentVocabulary.TriggerDeadzone, Triggers(s, triggersBoth), 0)),
            new("trigger_full_at_set", "set where a trigger counts as fully pressed, as a number",
                ["Right trigger fully pressed at 50", "Set my left trigger full at 60", "Triggers full press at 40"],
                s => Number(s, AgentVocabulary.TriggerFullAt, Triggers(s, triggersBoth), "full press point, 10 to 100")),
            new("turbo_on", "turn on turbo (rapid fire) on a button, optionally with presses a second",
                ["Turbo on X, 12 presses a second", "Put turbo on RB", "Rapid fire on my right trigger", "Turbo A at 15", "Make B turbo", "Auto fire on X"],
                s => s.Buttons.Count == 0 ? OfflinePlan.Say("Which button should get turbo?")
                    : First(s) is { } n ? Each(s, AgentVocabulary.Turbo, [.. s.Buttons], Num(n))
                    : Each(s, AgentVocabulary.Turbo, [.. s.Buttons], 10) with { Assumptions = [.. s.Assumed, "10 presses a second, since no rate was given."] }),
            new("turbo_off", "turn off turbo on a button",
                ["Turn off turbo on X", "Stop the turbo on RB", "No more turbo on A", "Disable rapid fire on my right trigger", "Remove turbo from B"],
                s => s.Buttons.Count == 0 ? OfflinePlan.Say("Which button's turbo should I turn off?") : Each(s, AgentVocabulary.Turbo, [.. s.Buttons], 0)),
            new("remap_swap", "swap two buttons",
                ["Swap A and B", "Switch X and Y", "Swap my bumpers", "Exchange LB and RB", "Flip A and B like a Nintendo controller"],
                s => s.Buttons.Count >= 2 ? Plan(s, (AgentVocabulary.Remap, s.Buttons[0], s.Buttons[1]), (AgentVocabulary.Remap, s.Buttons[1], s.Buttons[0]))
                    : Regex.IsMatch(s.Text, @"bumpers") ? Plan(s, (AgentVocabulary.Remap, "LB", "RB"), (AgentVocabulary.Remap, "RB", "LB"))
                    : OfflinePlan.Say("Which two buttons should I swap?")),
            new("remap_to", "make one button act like (send) another",
                ["Make LB act like RB", "Map X to A", "I want Y to work as B", "Turn my left bumper into the A button", "Remap B to X", "Make the D-pad up send Y"],
                s => s.Buttons.Count >= 2 ? Plan(s, (AgentVocabulary.Remap, s.Buttons[0], s.Buttons[1])) : OfflinePlan.Say("Which button, and what should it send?")),
            new("paddle_set", "put a function (jump is A, or A or B) on a rear paddle M1 to M6",
                ["Put jump on M1", "Put jump on M5", "M2 should press B", "Set paddle 1 to A", "Make my back paddle M3 jump"],
                s => s.Paddles.Count == 0 ? OfflinePlan.Say("Which paddle, M1 to M6?")
                    : Plan(s, [.. s.Paddles.Select(p => (AgentVocabulary.Paddle, p, (object)(s.Buttons.FirstOrDefault(b => b is "A" or "B") ?? s.Buttons.FirstOrDefault() ?? "A")))])),
            new("reset_target", "reset one stick, trigger or button to default",
                ["Reset my right trigger", "Put my left stick back to default", "Reset the A button", "Undo everything on my right stick", "Reset X"],
                s => (s.Sticks.Concat(s.Triggers).Concat(s.Triggers.Count > 0 ? Enumerable.Empty<string>() : s.Buttons).ToArray()) is { Length: > 0 } t ? Each(s, AgentVocabulary.Reset, t, 0) : OfflinePlan.Say("Reset what? A stick, a trigger, a button, or everything?")),
            new("reset_all", "reset every setting to default",
                ["Reset everything", "Put all my settings back to default", "Start over", "Factory reset my settings", "Clear all my changes", "Restore defaults"],
                s => Plan(s, (AgentVocabulary.Reset, AgentVocabulary.All, 0))),
            new("pc_mode_on", "switch PC mode on, so settings reach games",
                ["Turn on PC mode", "My settings don't do anything in games", "Enable PC mode", "Make my settings work in games", "Switch PC mode on", "Nothing changes in game"],
                s => Plan(s, (AgentVocabulary.PcMode, AgentVocabulary.PcModeTarget, "on"))),
            new("pc_mode_off", "switch PC mode off",
                ["Switch PC mode off", "Turn off PC mode", "Disable PC mode", "Stop PC mode", "I don't want PC mode"],
                s => Plan(s, (AgentVocabulary.PcMode, AgentVocabulary.PcModeTarget, "off"))),
            new("mouse_mode_on", "switch mouse mode on so the controller moves the pointer",
                ["Turn mouse mode on", "Enable mouse mode", "Use my controller as a mouse"],
                s => Plan(s, (AgentVocabulary.MouseMode, AgentVocabulary.MouseTarget, "on"))),
            new("mouse_mode_off", "switch mouse mode off",
                ["Switch mouse mode off", "Disable mouse mode", "Stop mouse mode"],
                s => Plan(s, (AgentVocabulary.MouseMode, AgentVocabulary.MouseTarget, "off"))),
            new("open_page", "open a named controller settings page",
                ["Open diagnostics", "Show curves", "Take me to macros", "Open buttons", "Show tools"],
                s => AgentVocabulary.Pages.FirstOrDefault(p => s.Text.Contains(p, StringComparison.OrdinalIgnoreCase)) is { } page
                    ? Plan(s, (AgentVocabulary.OpenPage, AgentVocabulary.PageTarget, page)) : OfflinePlan.Say("Which page should I open?")),
            new("greeting", "hello, help, or what can you do",
                ["Hi", "Hello", "What can you do", "Help", "Hey there", "Who are you"],
                _ => OfflinePlan.Say("I'm Agent XCTL. I can set stick and trigger dead zones and curves, hair triggers, turbo, remaps, paddles, reset, switch PC mode, and measure stick drift. Tell me how you play.")),
        ];
    }
}
