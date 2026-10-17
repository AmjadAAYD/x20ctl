using System.Text.RegularExpressions;
namespace X20Ctl.Zone.Agent;

/// <summary>One idea: the situation it's for, and the sentence that asks for it.</summary>
public sealed record AgentIdea(string Have, string Say, string Intent);

/// <summary>
/// The agent's search bar (owner request, 13 Oct 2026): the ideas half of the window lists what can be asked, and narrows
/// as the owner types ("sticks", even "sti"). Empty, it is the HELP list. A message that is only a topic ("Sticks") is
/// answered by the app itself, without the model: I've seen you search about "sticks". Is there any help you need?
/// </summary>
public static class AgentIdeas
{
    static readonly Lazy<(AgentIdea Idea, HashSet<string> Tokens, HashSet<string> Words)[]> catalogue = new(Build);

    /// <summary>Every idea, situations first (the HELP list), then one for each other request the agent knows.</summary>
    public static IReadOnlyList<AgentIdea> All => catalogue.Value.Select(c => c.Idea).ToList();

    static (AgentIdea, HashSet<string>, HashSet<string>)[] Build()
    {
        var offline = new OfflineAgent();
        var ideas = new List<AgentIdea>();
        foreach (var s in AgentHelp.Situations) ideas.Add(new(s.Have, s.Say, OfflineSupport.Explain(s.Say) != null ? "help_explain" : offline.Classify(s.Say).Intent?.Id ?? "help_explain"));
        foreach (var intent in OfflineAgent.Intents)
            if (intent.Id != "greeting" && ideas.All(i => i.Intent != intent.Id) && intent.Seeds.Length > 0)
                ideas.Add(new(char.ToUpperInvariant(intent.Meaning[0]) + intent.Meaning[1..], intent.Seeds[0], intent.Id));
        return ideas.Select(idea =>
        {
            var intent = OfflineAgent.Intents.FirstOrDefault(i => i.Id == idea.Intent);
            var text = new[] { idea.Have, idea.Say, intent?.Meaning ?? "" }.Concat(intent?.Seeds ?? []);
            var tokens = text.SelectMany(OfflineAgent.Tokens).Where(t => !t.Contains('_')).ToHashSet();
            var words = text.SelectMany(Words).ToHashSet();
            return (idea, tokens, words);
        }).ToArray();
    }

    static IEnumerable<string> Words(string text) => Regex.Matches(text.ToLowerInvariant().Replace("dead zone", "deadzone").Replace("pc mode", "pcmode"), @"[a-z]+").Select(m => m.Value);

    /// <summary>The ideas that match what's typed, best first; everything (the HELP list first) when nothing is typed.</summary>
    public static IReadOnlyList<AgentIdea> Search(string query, int count = 12)
    {
        var words = Words(AgentSpelling.Fix(query)).Where(w => w.Length >= 2 && !Filler.Contains(w)).Distinct().ToList();
        if (words.Count == 0) return All.Take(count).ToList();
        return catalogue.Value.Select((c, order) => (c.Idea, order, Score: words.Sum(w => Score(w, c.Tokens, c.Words))))
            .Where(r => r.Score > 0).OrderByDescending(r => r.Score).ThenBy(r => r.order).Take(count).Select(r => r.Idea).ToList();
    }

    static double Score(string word, HashSet<string> tokens, HashSet<string> words)
    {
        // a stick, a trigger, a button…: the word as the matcher reads it
        var read = OfflineAgent.Tokens(word).Where(t => !t.Contains('_')).ToList();
        if (read.Any(t => char.IsUpper(t[0]) && tokens.Contains(t))) return 2;
        if (words.Contains(word) || read.Any(tokens.Contains)) return 1.5;
        // still typing: "sti" is on its way to "stick"; a topic it could become ("tur": turbo) beats a common word (turn)
        if (word.Length < 3) return 0;
        var could = words.Where(w => w.StartsWith(word, StringComparison.Ordinal)).ToList();
        return could.Any(TopicWords.Contains) ? 1 : could.Count > 0 ? .5 : 0;
    }

    static readonly HashSet<string> Filler = ["the", "my", "a", "an", "about", "for", "on", "of", "and", "or", "to", "i", "me", "it", "setting", "settings"];
    static readonly HashSet<string> TopicWords = ["stick", "sticks", "analog", "analogs", "joystick", "joysticks", "thumbstick", "thumbsticks", "trigger", "triggers", "button", "buttons", "turbo", "paddle", "paddles",
        "drift", "deadzone", "dead", "zone", "zones", "deadzones", "curve", "curves", "pc", "mode", "pcmode", "reset", "remap", "remaps", "aim", "aiming", "camera", "sensitivity", "sniping", "racing", "shooting", "layout"];

    /// <summary>The message is only a topic ("Sticks", "dead zone", "my triggers"), not a request: the topic as typed, or null.</summary>
    public static string? Topic(string sentence)
    {
        sentence = AgentSpelling.Fix(sentence);
        var all = Words(sentence).ToList();
        if (all.Count == 0 || all.Count > 3) return null;
        var meaningful = all.Where(w => !Filler.Contains(w)).ToList();
        if (meaningful.Count == 0 || !meaningful.All(TopicWords.Contains) || Search(sentence).Count == 0) return null;
        return sentence.Trim().TrimEnd('?', '!', '.').Trim();
    }

    /// <summary>What the agent says about a search, in different words each time; a typo is read as what was meant.</summary>
    public static string TopicReply(string topic)
    {
        topic = AgentSpelling.Fix(topic.Trim());
        return AgentVariety.Pick("topic", [
            $"I've seen you search about “{topic}”. Is there any help you need?",
            $"Ooh, “{topic}”! *perks up* Need a hand with that?",
            $"“{topic}”, huh? Pick an idea on the left, or tell me what feels off.",
            $"Looking into “{topic}”? I know a thing or two. What's bugging you?",
            $"Searching “{topic}”… I've got ideas. Want me to fix something?",
            $"Aha, “{topic}”. My favourite subject today. What should I change?",
        ]);
    }
}
