using System.Text.Json;
using System.Text.RegularExpressions;
using X20Ctl.Zone.PcMode;
namespace X20Ctl.Zone.Agent;

/// <summary>One change the agent made: what the owner asked, what changed, and the settings either side of it.</summary>
public sealed record AgentStep(int Id, string Asked, IReadOnlyList<AgentChange> Changes, PcModeProfile Before, PcModeProfile After)
{
    /// <summary>The change in a line: the first setting that changed, and how many more.</summary>
    public string Summary => Changes.Count == 0 ? "No settings changed"
        : $"{Changes[0].What} {Changes[0].Before} → {Changes[0].After}" + (Changes.Count > 1 ? $" and {Changes.Count - 1} more" : "");
}

/// <summary>What going back did: the settings to store, the steps undone (newest first), and whether the settings had
/// been changed elsewhere since the agent's last change, which going back also undoes.</summary>
public sealed record AgentRewind(PcModeProfile Profile, IReadOnlyList<AgentStep> Undone, bool ChangedElsewhere);

public enum UndoAsk { None, Last, All, Show }

/// <summary>
/// Every change the agent made while X20CTL is open (owner's idea, 19 Oct 2026: undo for more than the last change).
/// Going back is always to before a step, which undoes that step and every later one, so the settings only ever return
/// to a state they were really in.
/// </summary>
public sealed class AgentHistory
{
    readonly List<AgentStep> steps = [];
    int next = 1;
    public IReadOnlyList<AgentStep> Steps => steps;
    public AgentStep? Latest => steps.Count > 0 ? steps[^1] : null;

    public AgentStep Add(string asked, IReadOnlyList<AgentChange> changes, PcModeProfile before, PcModeProfile after)
    {
        var step = new AgentStep(next++, asked, changes, Copy(before), Copy(after));
        steps.Add(step); return step;
    }

    /// <summary>Back to before the step with this id; null when it isn't in the history (already undone).</summary>
    public AgentRewind? BackTo(int id, PcModeProfile current)
    {
        var rewind = Plan(id, current);
        if (rewind != null) Forget(id);
        return rewind;
    }

    /// <summary>What going back to before this step would do, without doing it: the caller stores the settings first
    /// and only then <see cref="Forget"/>s the steps, so a failed save leaves the history as it was.</summary>
    public AgentRewind? Plan(int id, PcModeProfile current)
    {
        int at = steps.FindIndex(s => s.Id == id);
        if (at < 0) return null;
        return new(Copy(steps[at].Before), steps.Skip(at).Reverse().ToList(), !Same(current, steps[^1].After));
    }

    /// <summary>Takes this step and every later one out of the history.</summary>
    public void Forget(int id)
    {
        int at = steps.FindIndex(s => s.Id == id);
        if (at >= 0) steps.RemoveRange(at, steps.Count - at);
    }

    public static bool Same(PcModeProfile a, PcModeProfile b) => JsonSerializer.Serialize(a) == JsonSerializer.Serialize(b);
    static PcModeProfile Copy(PcModeProfile p) => JsonSerializer.Deserialize<PcModeProfile>(JsonSerializer.SerializeToUtf8Bytes(p))!;

    /// <summary>
    /// Asking to undo in the chat. "Undo", "undo that", "take that back": the last change. "Undo everything", "undo all my
    /// changes": back to before the first. "What did you change?": the list. A reset to the defaults ("undo everything,
    /// back to factory") is not an undo and is left to the phrasebook, as is any undo while nothing has been changed here.
    /// </summary>
    public static UndoAsk Parse(string sentence)
    {
        string s = " " + Regex.Replace(sentence.ToLowerInvariant().Replace('’', '\''), @"[^a-z' ]", " ").Trim() + " ";
        s = Regex.Replace(s, @"\s+", " ");
        if (Regex.IsMatch(s, @"\b(default|defaults|factory|stock|original|box|scratch|baseline)\b")) return UndoAsk.None;
        if (Regex.IsMatch(s, @"\bwhat (did|have) you (just )?chang\w*|\b(show|list|see|view) (me )?(my |the |all |your )?(changes|edits)\b|^ (my |your )?changes (so far )?$")) return UndoAsk.Show;
        if (Regex.IsMatch(s, @"^ (please )?(just )?(undo|revert) (all|everything)\b|^ (please )?(undo|revert) all (of )?(my |the |your )?(changes|edits|tweaks)\b|^ (put|set) (it all|everything) back\b|^ go back to (how|where) (it was|we started|i started)\b")) return UndoAsk.All;
        if (Regex.IsMatch(s, @"^ (please )?(just )?(undo|revert|take back)( (this|that|it|the last( change| one)?|my last change|last change|your last change))?( please)? $|^ (please )?(take|put) (that|it) back( please)? $")) return UndoAsk.Last;
        return UndoAsk.None;
    }
}
