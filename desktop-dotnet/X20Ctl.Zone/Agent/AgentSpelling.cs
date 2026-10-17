using System.Text.RegularExpressions;
namespace X20Ctl.Zone.Agent;

/// <summary>
/// Common sense about typing (owner request, 13 Oct 2026): "stickss" means sticks, "trigers" triggers. A word the agent
/// doesn't know is read as the closest word it does, one slip away (two for long words): a letter extra, missing, wrong
/// or swapped with its neighbour. Words under five letters (lb, rt, x, m1, rain) are never touched, so a button can't become another.
/// </summary>
public static class AgentSpelling
{
    static readonly Lazy<HashSet<string>> known = new(() =>
    {
        var words = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (_, sentence, _) in OfflineAgent.Examples(OfflineAgent.Phrasebook())) words.UnionWith(Words(sentence));
        foreach (var i in OfflineAgent.Intents) words.UnionWith(Words(i.Meaning));
        foreach (var s in AgentHelp.Situations) { words.UnionWith(Words(s.Have)); words.UnionWith(Words(s.Say)); }
        words.UnionWith(["sticks", "stick", "triggers", "trigger", "buttons", "button", "turbo", "paddles", "paddle", "drift", "deadzone", "curves", "curve", "battery", "controller", "controllers", "donate", "star", "remap", "sensitivity",
            "raise", "lower", "increase", "decrease", "reduce", "boost", "speed", "slower", "faster", "camera", "movement", "looking", "turning"]);
        return words;
    });
    // the words a topic search is usually about: preferred when two known words are equally close
    static readonly HashSet<string> Preferred = ["sticks", "stick", "triggers", "trigger", "buttons", "button", "turbo", "paddles", "paddle", "drift", "deadzone", "dead", "zone", "curves", "curve", "battery", "controller", "aim", "camera", "sniping", "racing", "smooth", "swap", "reset"];

    static IEnumerable<string> Words(string text) => Regex.Matches(text.ToLowerInvariant(), @"[a-z]+").Select(m => m.Value);

    /// <summary>The word as the agent understands it: itself when known or short, else the closest known word, or itself
    /// when nothing is close.</summary>
    public static string Word(string word)
    {
        string lower = word.ToLowerInvariant();
        if (lower.Length < 5 || !Regex.IsMatch(lower, "^[a-z]+$") || known.Value.Contains(lower)) return word;
        int allowed = lower.Length >= 7 ? 2 : 1;
        string? best = null; int bestDistance = int.MaxValue;
        foreach (var k in known.Value)
        {
            if (Math.Abs(k.Length - lower.Length) > allowed || k.Length < 3) continue;
            int d = Distance(lower, k);
            if (d > allowed) continue;
            if (d < bestDistance || d == bestDistance && Preferred.Contains(k) && (best == null || !Preferred.Contains(best)) || d == bestDistance && best != null && Preferred.Contains(k) == Preferred.Contains(best) && string.CompareOrdinal(k, best) < 0)
            { best = k; bestDistance = d; }
        }
        return best ?? word;
    }

    /// <summary>The sentence with every misspelt word read as the agent understands it; everything else left as typed.</summary>
    public static string Fix(string sentence) => Regex.Replace(sentence, @"[A-Za-z]+", m => Word(m.Value) is var w && w != m.Value ? w : m.Value);

    /// <summary>Optimal string alignment distance: insertions, deletions, substitutions and swaps of neighbours.</summary>
    static int Distance(string a, string b)
    {
        var d = new int[a.Length + 1, b.Length + 1];
        for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (int j = 0; j <= b.Length; j++) d[0, j] = j;
        for (int i = 1; i <= a.Length; i++)
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1]) d[i, j] = Math.Min(d[i, j], d[i - 2, j - 2] + 1);
            }
        return d[a.Length, b.Length];
    }
}

/// <summary>Different words each time (owner request, 13 Oct 2026: "saying the same thing is a bit annoying"): a reply is
/// picked from its phrasings, never the same one twice in a row.</summary>
public static class AgentVariety
{
    static readonly Dictionary<string, int> last = new();
    static readonly Random random = new();

    public static string Pick(string key, IReadOnlyList<string> options)
    {
        lock (last)
        {
            if (options.Count == 1) return options[0];
            int previous = last.GetValueOrDefault(key, -1), next;
            do next = random.Next(options.Count); while (next == previous);
            last[key] = next;
            return options[next];
        }
    }
}
