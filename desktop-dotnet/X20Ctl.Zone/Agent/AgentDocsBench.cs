namespace X20Ctl.Zone.Agent;

/// <summary>A question about the app, and the doc sections that answer it (any one will do); none: the docs don't, and
/// the right answer is to say so.</summary>
public sealed record DocCase(string Question, params string[] Answers)
{
    public bool OutOfDocs => Answers.Length == 0;
    public bool Found(IEnumerable<DocHit> hits) => hits.Any(h => Answers.Any(a => h.Chunk.Where.Contains(a, StringComparison.OrdinalIgnoreCase)));
}

/// <summary>
/// M5's evaluation: questions an owner asks about the app, written from the guides before any search was tuned. Scored
/// two ways: retrieval (is an answering passage among the three offered?) and the answer (does it cite one, and does it
/// admit it when the docs don't say?).
/// </summary>
public static class AgentDocsBench
{
    public static readonly DocCase[] Cases =
    [
        new("How do I switch my settings off in the middle of a game?", "PC mode"),
        new("What drivers does PC mode install?", "PC mode"),
        new("Can I use four controllers at once?", "PC mode"),
        new("Can turbo get me banned?", "PC mode"),
        new("Can I give one game its own settings?", "PC mode"),
        new("How do I share my settings with a friend?", "PC mode"),
        new("The tester shows no input, what do I do?", "The tester shows no input"),
        new("I closed the app but it's still running", "still running"),
        new("Nothing shows up when I scan for my controller", "Nothing appears in the scan"),
        new("Where are my profiles saved?", "saved-profile error"),
        new("Does X20CTL send any data about me?", "Local first", "PRIVACY.md"),
        new("Is the X20 Pro supported?", "Supported controllers"),
        new("What does the Controller Check do?", "Controller Check"),
        new("Do I need to install .NET to run it?", "Download"),
        new("Is the exe signed?", "Download"),
        new("A write failed, what now?", "A write failed"),
        new("What's in the Tools page?", "Tools"),
        new("Does it work on Linux?", "Download", "Linux"),
        new("Does X20CTL flash firmware?", "Local first", "PRIVACY.md"),
        new("How do I report a bug?", "Reset and reporting"),
        new("How much does the X20 Pro cost?"),
        new("Does X20CTL work on a PlayStation 5?"),
        new("How many hours does the X20's battery last?"),
    ];

    /// <summary>The answer is right: an in-docs question cites a passage from an answering section; an out-of-docs one
    /// cites nothing and says it can't confirm it.</summary>
    public static bool Right(DocCase c, AgentTurn turn)
    {
        if (!turn.Verdict.Valid || turn.Verdict.Proposal is not { } p) return false;
        if (c.OutOfDocs) return p.Sources.Count == 0 && (p.Speech + " " + p.Recap).Contains("confirm", StringComparison.OrdinalIgnoreCase);
        return c.Found(turn.Cited);
    }
}
