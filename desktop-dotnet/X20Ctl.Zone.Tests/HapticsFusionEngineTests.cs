using X20Ctl.Zone.Lighting;
using Xunit;

namespace X20Ctl.Zone.Tests;

public sealed class HapticsFusionEngineTests
{
    static readonly AudioFrame SilentFrame = new();

    [Fact]
    public void MenuAutoMute_SilencesGameProfilesWhenCursorVisible()
    {
        var engine = new HapticsFusionEngine();
        var tuning = HapticsTuning.DefaultFor(HapticsGenreProfile.Racing) with { MenuAutoMute = true };
        var loudBass = new AudioFrame() with { Level = 0.9f, Bass = 0.9f, Mid = 0.7f, Treble = 0.6f, BeatPulse = 0.9f, Energy = 0.8f };
        var cursorVisibleInput = new LivePadInput(0, 0, 0, 0, 0, 0.8, 0, false, CursorVisible: true, IsIdle: false);

        HapticsEvaluation eval = default;
        for (int i = 0; i < 20; i++)
            eval = engine.Evaluate(loudBass, cursorVisibleInput, HapticsGenreProfile.Racing, tuning, dtSeconds: 0.05);

        Assert.True(eval.IsMenuMuted);
        Assert.Equal(HapticsEventTag.MenuMuted, eval.PrimaryEvent);
        Assert.Equal(0.0, eval.Motors.LeftGrip, 3);
        Assert.Equal(0.0, eval.Motors.RightGrip, 3);
        Assert.Equal(0.0, eval.Motors.LeftTrigger, 3);
        Assert.Equal(0.0, eval.Motors.RightTrigger, 3);
    }

    [Fact]
    public void DemoEffects_SynthesizeExpectedMotorsAndEvents()
    {
        var engine = new HapticsFusionEngine();
        var tuning = HapticsTuning.DefaultFor(HapticsGenreProfile.Racing) with { ComfortTier = HapticsComfortTier.Balanced };
        var pad = new LivePadInput(0, 0, 0, 0, 0, 0, 0, false);

        engine.TriggerDemoEffect(HapticsDemoKind.AbsBrake);
        HapticsEvaluation absEval = default;
        for (int i = 0; i < 6; i++)
            absEval = engine.Evaluate(SilentFrame, pad, HapticsGenreProfile.Racing, tuning, dtSeconds: 0.02);

        Assert.Equal(HapticsEventTag.AbsBrake, absEval.PrimaryEvent);
        Assert.True(absEval.Motors.LeftTrigger > absEval.Motors.RightTrigger + 0.10);

        engine.Reset();
        engine.TriggerDemoEffect(HapticsDemoKind.GunshotBurst);
        HapticsEvaluation gunEval = default;
        double peakGunRt = 0;
        for (int i = 0; i < 4; i++)
        {
            gunEval = engine.Evaluate(SilentFrame, pad, HapticsGenreProfile.FpsAction, tuning, dtSeconds: 0.02);
            peakGunRt = Math.Max(peakGunRt, gunEval.Motors.RightTrigger);
        }

        Assert.Equal(HapticsEventTag.Gunshot, gunEval.PrimaryEvent);
        Assert.True(peakGunRt > 0.35, $"Expected Gunshot demo RightTrigger peak > 0.35, got {peakGunRt:F3}");
    }

    [Fact]
    public void FeatherTier_CapsOutputComfortablyBelowFullPunch()
    {
        var featherEngine = new HapticsFusionEngine();
        var punchEngine = new HapticsFusionEngine();

        var featherTuning = new HapticsTuning { ComfortTier = HapticsComfortTier.Feather, MasterIntensity = 80, TextureDetail = 60, ImpactPunch = 80 };
        var punchTuning = featherTuning with { ComfortTier = HapticsComfortTier.FullPunch };

        var analyzer = new AudioSpectrumAnalyzer();
        var buf = new float[960 * 2];
        var rng = new Random(19);
        HapticsEvaluation fEval = default;
        HapticsEvaluation pEval = default;

        for (int step = 0; step < 30; step++)
        {
            for (int i = 0; i < 960; i++)
            {
                double env = 0.65 * Math.Sin(2 * Math.PI * 55 * ((step * 960 + i) / 48000.0)) + (rng.NextDouble() * 2 - 1) * 0.35;
                buf[i * 2] = (float)env;
                buf[i * 2 + 1] = (float)env;
            }
            var af = analyzer.ProcessInterleaved(buf, 2, 48000, 0.02);
            var input = new LivePadInput(0, 0.9, 0, 0, 0, 0.9, 0, false);
            fEval = featherEngine.Evaluate(af, input, HapticsGenreProfile.Racing, featherTuning, dtSeconds: 0.02);
            pEval = punchEngine.Evaluate(af, input, HapticsGenreProfile.Racing, punchTuning, dtSeconds: 0.02);
        }

        Assert.True(fEval.Motors.LeftGrip <= 0.72);
        Assert.True(fEval.AverageDutyCycle <= pEval.AverageDutyCycle + 0.01);
    }
}
