using System.Text.RegularExpressions;
namespace X20Ctl.Zone;

public enum UpdateStep { Hidden, Confirm, Downloading, Restarting, Failed }

/// <summary>The steps of the in-app update (owner, 3 Nov 2026): ask first, show the download, and on any failure say why inside the app.
/// Nothing here sends the player to a web page. Pure state and wording, so it can be tested without a window.</summary>
public sealed class UpdateFlow(string versionName, bool canSelfUpdate)
{
    public string VersionName { get; } = versionName;
    public UpdateStep Step { get; private set; } = UpdateStep.Hidden;
    public double Progress { get; private set; }
    public string Message { get; private set; } = "";
    public bool CanRetry { get; private set; }

    /// <summary>The notice was clicked: ask, or explain that this copy can't update itself.</summary>
    public void Open()
    {
        if (Step != UpdateStep.Hidden) return;
        if (canSelfUpdate) { Step = UpdateStep.Confirm; return; }
        Fail("This copy runs from a folder, so it can't update itself. Replace it with the single-file X20CTL exe to update from inside the app.", false);
    }

    /// <summary>Yes. False when nothing was waiting for an answer.</summary>
    public bool Yes() { if (Step != UpdateStep.Confirm) return false; Begin(); return true; }
    public void No() { if (Step == UpdateStep.Confirm) Step = UpdateStep.Hidden; }
    public void Report(double progress) { if (Step == UpdateStep.Downloading) Progress = Math.Clamp(progress, 0, 1); }
    public void Cancel() { if (Step == UpdateStep.Downloading) { Step = UpdateStep.Hidden; Progress = 0; } }
    public void Installed() { if (Step == UpdateStep.Downloading) { Progress = 1; Step = UpdateStep.Restarting; } }
    public void Fail(string reason, bool canRetry = true) { if (Step is UpdateStep.Hidden or UpdateStep.Downloading) { Step = UpdateStep.Failed; Message = reason; CanRetry = canRetry; } }
    /// <summary>The new exe is in place but would not start. Retrying would download it again for nothing.</summary>
    public void RestartFailed() { if (Step == UpdateStep.Restarting) { Step = UpdateStep.Failed; CanRetry = false; Message = $"{VersionName} is installed, but X20CTL could not restart itself. Close it and open it again."; } }
    public bool Retry() { if (Step != UpdateStep.Failed || !CanRetry) return false; Begin(); return true; }
    public void Close() { if (Step == UpdateStep.Failed) Step = UpdateStep.Hidden; }
    /// <summary>Esc or the controller's B: No, Cancel or Close, depending on the step.</summary>
    public void Back() { No(); Cancel(); Close(); }

    void Begin() { Step = UpdateStep.Downloading; Progress = 0; Message = ""; CanRetry = false; }

    public string Title => Step switch
    {
        UpdateStep.Confirm => $"Update to {VersionName}?",
        UpdateStep.Downloading => $"Downloading {VersionName}",
        UpdateStep.Restarting => "Restarting",
        UpdateStep.Failed => "Couldn't update",
        _ => "",
    };

    public string Body => Step switch
    {
        UpdateStep.Confirm => $"X20CTL {VersionName} will be downloaded from GitHub and installed. X20CTL then restarts.",
        UpdateStep.Downloading => $"{(int)Math.Round(Progress * 100)}%",
        UpdateStep.Restarting => $"{VersionName} is installed.",
        UpdateStep.Failed => Message + " Your current version is untouched.",
        _ => "",
    };
}

/// <summary>One version's entry in CHANGELOG.md ("## [V0.018] - 2026-10-20"), for the What's new screen. The exe carries the changelog, so it works offline.</summary>
public static class Changelog
{
    /// <summary>The text under that version's heading, up to the next "## " heading; null if the version has none. Matches V0.018, 0.018 and v0.018.1 alike.</summary>
    public static string? Entry(string markdown, string version)
    {
        string wanted = ReleaseVersions.Display(version);
        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        int start = -1;
        for (int i = 0; i < lines.Length; i++)
        {
            var m = Regex.Match(lines[i], @"^##\s*\[?([^\]\s]+)\]?");
            if (m.Success && ReleaseVersions.Display(m.Groups[1].Value) == wanted && !m.Groups[1].Value.Equals("Unreleased", StringComparison.OrdinalIgnoreCase)) { start = i + 1; break; }
        }
        if (start < 0) return null;
        int end = start; while (end < lines.Length && !lines[end].StartsWith("## ")) end++;
        string text = string.Join("\n", lines[start..end]).Trim();
        return text.Length == 0 ? null : text;
    }
}
