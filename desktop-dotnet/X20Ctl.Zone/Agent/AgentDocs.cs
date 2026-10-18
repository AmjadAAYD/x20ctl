using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace X20Ctl.Zone.Agent;

/// <summary>A piece of the docs: the file, the heading path it sits under, and its text.</summary>
public sealed record DocChunk(string File, string Section, string Text)
{
    public string Where => $"{File} › {Section}";
    public string Key => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Where + "\n" + Text)))[..24];
}
/// <summary>A chunk that matched a question, and how closely (cosine, 0 to 1).</summary>
public sealed record DocHit(DocChunk Chunk, double Score);

/// <summary>Finds the passages of the docs closest to a question.</summary>
public interface IDocSearch
{
    string Method { get; }
    /// <summary>The least similarity worth offering the model: below it a passage isn't about the question.</summary>
    double Enough { get; }
    Task<IReadOnlyList<DocHit>> Search(string question, int count, CancellationToken cancel);
}

/// <summary>
/// The docs Agent XCTL answers app questions from : the user-facing guides, split by heading so every
/// passage keeps its file and section for the source card, long sections cut into overlapping pieces of about
/// <see cref="Words"/> words.
/// </summary>
public static class AgentDocs
{
    /// <summary>The guides an owner reads. The validation logs under docs/ are internal and stay out.</summary>
    public static readonly string[] Files = ["README.md", "IF-YOUR-CONTROLLER-ISNT-WORKING.md", "PRIVACY.md"];
    public const int Words = 140, Overlap = 30;

    public static List<DocChunk> Chunk(string file, string markdown)
    {
        var chunks = new List<DocChunk>();
        var path = new List<(int Level, string Title)>();
        var body = new StringBuilder();
        bool fenced = false;
        void Flush()
        {
            string text = Clean(body.ToString()); body.Clear();
            if (text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length < 8) return;
            string section = path.Count == 0 ? "Introduction" : string.Join(" › ", path.Select(p => p.Title));
            var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (int start = 0; start < words.Length; start += Words - Overlap)
            {
                chunks.Add(new(file, section, string.Join(' ', words.Skip(start).Take(Words))));
                if (start + Words >= words.Length) break;
            }
        }
        foreach (var raw in markdown.Replace("\r", "").Split('\n'))
        {
            if (raw.TrimStart().StartsWith("```")) { fenced = !fenced; continue; }
            if (fenced) continue; // code isn't prose to answer from
            var heading = Regex.Match(raw, @"^(#{1,4})\s+(.+?)\s*#*\s*$");
            if (heading.Success)
            {
                Flush();
                int level = heading.Groups[1].Length;
                path.RemoveAll(p => p.Level >= level);
                if (level > 1) path.Add((level, Clean(heading.Groups[2].Value))); // the page title would only repeat the file name
                continue;
            }
            body.AppendLine(raw);
        }
        Flush();
        return chunks;
    }

    /// <summary>Markdown to plain sentences: images, badges, HTML and link targets go; link text stays.</summary>
    static string Clean(string text)
    {
        text = Regex.Replace(text, @"!\[[^\]]*\]\([^)]*\)", " ");
        text = Regex.Replace(text, @"\[([^\]]*)\]\([^)]*\)", "$1");
        text = Regex.Replace(text, @"<[^>]+>", " ");
        text = Regex.Replace(text, @"[*_`>|]+", " ");
        text = Regex.Replace(text, @"^\s*[-+]\s+", "", RegexOptions.Multiline);
        return Regex.Replace(text, @"\s+", " ").Trim();
    }

    /// <summary>A guide's text is read from its plain-text twin (README.plain.md) when there is one: the README itself is a page of pictures
    /// with nothing to search. Sources are still cited under the guide's own name.</summary>
    public static List<DocChunk> Load(string folder)
    {
        string Source(string f) { string plain = Path.Combine(folder, Path.GetFileNameWithoutExtension(f) + ".plain.md"); return File.Exists(plain) ? plain : Path.Combine(folder, f); }
        return Files.Where(f => File.Exists(Source(f))).SelectMany(f => Chunk(f, File.ReadAllText(Source(f)))).ToList();
    }

    /// <summary>The passages, numbered, as the model is given them.</summary>
    public static string Prompt(IReadOnlyList<DocHit> hits) =>
        "From the X20CTL docs (cite the numbers you use in \"sources\"):\n" + string.Join("\n", hits.Select((h, i) => $"[{i + 1}] {h.Chunk.Where}: {h.Chunk.Text}"));

    internal static double Cosine(float[] a, float[] b)
    {
        double dot = 0, na = 0, nb = 0;
        for (int i = 0; i < a.Length && i < b.Length; i++) { dot += a[i] * b[i]; na += a[i] * a[i]; nb += b[i] * b[i]; }
        return na == 0 || nb == 0 ? 0 : dot / Math.Sqrt(na * nb);
    }
}

/// <summary>Search by words: TF-IDF over each passage's words, cosine similarity with the question's.</summary>
public sealed class KeywordSearch : IDocSearch
{
    public string Method => "keywords";
    public double Enough { get; init; } = 0.12;
    readonly IReadOnlyList<DocChunk> chunks;
    readonly List<Dictionary<string, double>> rows;
    readonly Dictionary<string, double> idf = new();
    static readonly HashSet<string> Stop = ["the", "a", "an", "and", "or", "to", "of", "in", "on", "is", "it", "for", "my", "i", "you", "your", "with", "this", "that", "be", "are", "can", "do", "does", "how", "what", "why", "when", "if", "as", "at", "by", "from", "not", "no", "so", "me"];

    public KeywordSearch(IReadOnlyList<DocChunk> chunks)
    {
        this.chunks = chunks;
        var docs = chunks.Select(c => Words(c.Section + " " + c.Text)).ToList();
        foreach (var w in docs.SelectMany(d => d.Distinct())) idf[w] = idf.GetValueOrDefault(w) + 1;
        foreach (var w in idf.Keys.ToList()) idf[w] = Math.Log((1.0 + docs.Count) / (1.0 + idf[w])) + 1;
        rows = docs.Select(Vector).ToList();
    }
    static List<string> Words(string text) => Regex.Matches(text.ToLowerInvariant(), @"[a-z0-9]+").Select(m => m.Value).Where(w => !Stop.Contains(w)).Select(w => w.Length > 4 && w.EndsWith('s') ? w[..^1] : w).ToList();
    Dictionary<string, double> Vector(List<string> words)
    {
        var v = words.GroupBy(w => w).ToDictionary(g => g.Key, g => g.Count() * idf.GetValueOrDefault(g.Key, 0));
        double n = Math.Sqrt(v.Values.Sum(x => x * x));
        if (n > 0) foreach (var k in v.Keys.ToList()) v[k] /= n;
        return v;
    }
    public Task<IReadOnlyList<DocHit>> Search(string question, int count, CancellationToken cancel)
    {
        var q = Vector(Words(question));
        IReadOnlyList<DocHit> hits = chunks.Select((c, i) => new DocHit(c, q.Sum(e => rows[i].TryGetValue(e.Key, out var y) ? e.Value * y : 0))).OrderByDescending(h => h.Score).Take(count).ToList();
        return Task.FromResult(hits);
    }
}
