using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using X20Ctl.Product.Agent;
using X20Ctl.Zone.Agent;
namespace X20Ctl.Product.Development;

/// <summary>
/// --review-talk DIRECTORY: the agent window and its phrasebook, as an owner uses them. Opens an X20 Pro, sends sentences through it
/// exactly as typed, then an X20 for the paddle it doesn't have, and writes captures (rendered, never the screen) and a log
/// of every turn: the model's raw output, each check, what changed, and the token counts and times the model reported.
/// Settings go to a fixture file in DIRECTORY; PC mode stays off.
/// </summary>
internal static class TalkReview
{
    public static async Task Run(string directory)
    {
        Directory.CreateDirectory(directory);
        var log = new StringBuilder();
        var window = new ProductWindow(false, Path.Combine(directory, "assignments.json"));
        try
        {
            window.Show(); window.WindowState = WindowState.Maximized; await Task.Delay(500);
            await Controller("x20_pro", 0, "1-pro", ["Make my right stick good for sniping", "What's good and bad about hair triggers?", "I want faster triggers for shooting", "Turbo on X, 12 presses a second", "Put jump on M5", "Set my left stick dead zone to 260", "My right stick drifts a little, can you fix it?"]);
            window.Host.ReturnToZone(); await Task.Delay(700);
            await Controller("x20", 1, "2-x20", ["Put jump on M5"]);
            log.AppendLine("== fixture file after the run");
            string fixture = Path.Combine(directory, "pc-mode-fixture.json");
            log.AppendLine(File.Exists(fixture) ? File.ReadAllText(fixture) : "(none written)");
        }
        catch (Exception failure) { log.AppendLine("FAILED: " + failure); }
        finally { File.WriteAllText(Path.Combine(directory, "talk-log.txt"), log.ToString()); window.Close(); }

        async Task Controller(string model, int player, string prefix, string[] sentences)
        {
            window.Scene.AssignController(player, window.Host.Catalog.Get(model)); window.Host.EnterStudio(player); await Task.Delay(1400);
            var studio = window.Host.Studio!; studio.ShowButtons();
            log.AppendLine($"#### {model}");
            await Shot(prefix + "-0-closed");
            int stop = 0;
            // the walk-through: a capture of each step once it has finished moving (the card holds it for a few seconds)
            studio.AgentTourStarted += tour => tour.Held += step => _ = Shot($"{prefix}-tour-{++stop}-{step.Target}");
            // the drift reading: a labelled stand-in (no controller is read here), with a capture mid-countdown
            studio.AgentMeter = progress => new FixtureMeter(progress, () => Shot($"{prefix}-measuring"));
            studio.OpenAgent(); var talk = studio.AgentWindow!;
            await Shot(prefix + "-0-open");
            int n = 0;
            foreach (var sentence in sentences)
            {
                var turn = await talk.Ask(sentence); n++;
                log.AppendLine($"\n== \"{sentence}\"");
                if (turn == null) { log.AppendLine("no turn (model unreachable or not ready)"); continue; }
                for (int a = 0; a < turn.Attempts.Count; a++) log.AppendLine($"output {a + 1}: {turn.Attempts[a].Content.ReplaceLineEndings(" ")}");
                if (turn.Verdict.StructureError != null) log.AppendLine("structure: " + turn.Verdict.StructureError);
                foreach (var c in turn.Verdict.Checks) log.AppendLine($"  {c.State,-11} {c.Action.Action} {c.Action.Target} {c.Action.ValueText} · {c.Message}");
                if (turn.Verdict.Proposal?.Speech is { Length: > 0 } said) log.AppendLine("said: " + said);
                log.AppendLine("valid: " + turn.Verdict.Valid + (turn.Outcome.Error != null ? " · " + turn.Outcome.Error : ""));
                foreach (var c in turn.Outcome.Changes) log.AppendLine($"  changed {c.What}: {c.Before} -> {c.After}");
                if (turn.Measured != null) log.AppendLine("measured (fixture): " + turn.Measured.Describe());
                if (turn.Verdict.Proposal?.Recap is { Length: > 0 } recap) log.AppendLine("recap: " + recap);
                log.AppendLine("stats: " + AgentSession.Stats(turn));
            }
            await Shot($"{prefix}-1-conversation");
            talk.ExpandDetails(); talk.ScrollTo(0); await Shot($"{prefix}-2-details-top");
        }

        async Task Shot(string name)
        {
            await Task.Delay(900); window.UpdateLayout();
            foreach (var (what, e) in new (string, FrameworkElement?)[] { ("pill", window.Host.Studio?.AgentButton), ("window", window.Host.Studio?.AgentWindow?.Body) })
                if (e is { IsVisible: true }) log.AppendLine($"[{name}] {what} at {e.TransformToAncestor(window.Stage).TransformBounds(new Rect(e.RenderSize))}");
            var bmp = new RenderTargetBitmap((int)window.Stage.ActualWidth, (int)window.Stage.ActualHeight, 96, 96, PixelFormats.Pbgra32); bmp.Render(window.Stage);
            var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(bmp)); using var f = File.Create(Path.Combine(directory, name + ".png")); enc.Save(f);
        }
    }

    /// <summary>A review stand-in for the drift reading: counts down like the real one, then reports a right stick that rests
    /// a little off centre and wanders up to 6.4%, a left stick that is clean. Labelled fixture in the log.</summary>
    sealed class FixtureMeter(Action<string, int> progress, Func<Task> shot) : IDriftMeter
    {
        public async Task<DriftReport> Measure(IReadOnlyList<string> sticks, CancellationToken cancel)
        {
            for (int s = 5; s > 0; s--) { progress(s > 3 ? "Let go of the sticks" : "Reading the sticks", s); await Task.Delay(1000, cancel); if (s == 3) await shot(); }
            var all = new[] { new StickDrift("LeftStick", 0.2, -0.1, 0.6, 3000), new StickDrift("RightStick", 3.1, -2.4, 6.4, 3000) };
            return new(all.Where(d => sticks.Contains(d.Stick)).ToList(), null);
        }
    }
}
