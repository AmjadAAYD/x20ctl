using System.IO;
using X20Ctl.Zone;

namespace X20Ctl.Zone.Tests;

public class VibrationPatternsTests
{
    [Fact]
    public void BuiltIns_HaveDistinctNamesValidStepsAndNonSilentFrames()
    {
        Assert.Equal(8, VibrationPatternEngine.BuiltIns.Count);
        Assert.Equal(8, VibrationPatternEngine.BuiltIns.Select(p => p.Id).Distinct().Count());

        foreach (var pat in VibrationPatternEngine.BuiltIns)
        {
            var norm = pat.Normalized();
            Assert.InRange(norm.Steps.Length, 4, 16);
            Assert.InRange(norm.StepMs, VibrationPattern.MinStepMs, VibrationPattern.MaxStepMs);

            bool anyActive = false;
            for (int i = 0; i < norm.Steps.Length; i++)
            {
                var hf = VibrationPatternEngine.EvaluateAt(norm, i * norm.StepMs + 1, 100, out int step, out bool finished);
                Assert.False(finished);
                Assert.Equal(i, step);
                if (!hf.IsSilent) anyActive = true;
            }
            Assert.True(anyActive, $"Pattern {pat.Id} must have at least one active step.");
        }
    }

    [Fact]
    public void EvaluateAt_SupportsSmoothInterpolationLoopingOneShotAndTriggerStiction()
    {
        var pat = new VibrationPattern
        {
            Id = "test-ramp",
            Name = "Ramp Test",
            StepMs = 100,
            Loop = false,
            SmoothRamp = true,
            Steps =
            [
                new(0, 100, 10, 0),
                new(100, 0, 0, 50),
                new(0, 0, 0, 0),
                new(0, 0, 0, 0),
            ],
        };

        // Zero master strength always silences output.
        var muted = VibrationPatternEngine.EvaluateAt(pat, 50, 0, out _, out _);
        Assert.True(muted.IsSilent);

        // Midpoint between Step 0 (LG=0, RG=100, LT=10) and Step 1 (LG=100, RG=0, LT=0, RT=50).
        var mid = VibrationPatternEngine.EvaluateAt(pat, 50, 100, out int step0, out bool fin0);
        Assert.False(fin0);
        Assert.Equal(0, step0);
        Assert.InRange(mid.LeftGrip, 0.45, 0.55);
        Assert.InRange(mid.RightGrip, 0.45, 0.55);
        // Active non-zero trigger gets 24% ERM stiction lift.
        Assert.InRange(mid.LeftTrigger, 0.24, 0.40);
        Assert.InRange(mid.RightTrigger, 0.24, 0.60);

        // One-shot stops cleanly after total duration.
        var afterEnd = VibrationPatternEngine.EvaluateAt(pat, pat.TotalDurationMs + 5, 100, out _, out bool finEnd);
        Assert.True(finEnd);
        Assert.True(afterEnd.IsSilent);

        // Looping wraps back to Step 0 smoothly.
        var looped = VibrationPatternEngine.EvaluateAt(pat with { Loop = true }, pat.TotalDurationMs + 1, 100, out int loopStep, out bool finLoop);
        Assert.False(finLoop);
        Assert.Equal(0, loopStep);
        Assert.True(looped.RightGrip > 0.90);
    }

    [Fact]
    public void VibrationPatternStore_SavesReloadsFindsAndDeletesAtomically()
    {
        string dir = Path.Combine(Path.GetTempPath(), "x20ctl-vibpat-" + Guid.NewGuid().ToString("N"));
        try
        {
            string path = Path.Combine(dir, "patterns.json");
            var store = new VibrationPatternStore(path);
            store.Load();
            Assert.Empty(store.Patterns);

            var draft = VibrationPatternEngine.CreateBlankDraft() with { StepMs = 95, SmoothRamp = true };
            var saved = store.SavePattern("Turbo Pulse", draft);
            Assert.StartsWith("user-", saved.Id);

            var reloaded = new VibrationPatternStore(path);
            reloaded.Load();
            Assert.Single(reloaded.Patterns);
            var foundCustom = reloaded.FindPattern(saved.Id);
            Assert.NotNull(foundCustom);
            Assert.Equal("Turbo Pulse", foundCustom!.Name);
            Assert.Equal(95, foundCustom.StepMs);
            Assert.True(foundCustom.SmoothRamp);
            Assert.NotNull(reloaded.FindPattern("builtin-heartbeat"));

            Assert.True(reloaded.DeletePattern(saved.Id));
            Assert.Null(reloaded.FindPattern(saved.Id));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }
}
