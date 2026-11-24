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

    [Fact]
    public void GameForeground_NeverEntersMusicAndSilencesBackgroundThemes()
    {
        var engine = new HapticsFusionEngine();
        var tuning = new HapticsTuning { ComfortTier = HapticsComfortTier.Feather, MasterIntensity = 80 };
        var bgmAudio = new AudioFrame() with
        {
            Level = 0.70f,
            Bass = 0.65f,
            Mid = 0.60f,
            Treble = 0.50f,
            BeatPulse = 0.70f,
            InstrumentEnergy = 0.75f,
            RawRms = 0.055f,
            Spectrum = new AudioSpectrum8(0.4f, 0.7f, 0.6f, 0.5f, 0.4f, 0.3f, 0.2f, 0.1f),
        };
        var calmPad = new LivePadInput(0, 0, 0, 0, 0, 0, 0, false, CursorVisible: false, IsIdle: false, IsGameForeground: true);

        HapticsEvaluation eval = default;
        for (int i = 0; i < 15; i++)
            eval = engine.Evaluate(bgmAudio, calmPad, HapticsGenreProfile.Auto, tuning, dtSeconds: 0.02);

        Assert.NotEqual(HapticsGenreProfile.Music, eval.ResolvedProfile);
        Assert.Equal(0.0, eval.Motors.LeftGrip, 3);
        Assert.Equal(0.0, eval.Motors.RightGrip, 3);
        Assert.Equal(0.0, eval.Motors.LeftTrigger, 3);
        Assert.Equal(0.0, eval.Motors.RightTrigger, 3);
    }

    [Fact]
    public void WalkingWithQuietRawRms_UsesFaintRightGripOnlyWithZeroLeftOrTriggers()
    {
        var engine = new HapticsFusionEngine();
        var tuning = new HapticsTuning { ComfortTier = HapticsComfortTier.Feather, MasterIntensity = 80, TextureDetail = 50 };
        var footAudio = new AudioFrame() with
        {
            Level = 0.60f,
            Mid = 0.45f,
            SpectralFlatness = 0.55f,
            SpectralFlux = 0.32f,
            RawRms = 0.025f,
            RawPeakSpike = 0.015f,
            Spectrum = new AudioSpectrum8(0.1f, 0.2f, 0.4f, 0.3f, 0.3f, 0.2f, 0.1f, 0.1f),
        };
        var walkPad = new LivePadInput(0, 0.75, 0, 0, 0, 0, 0, false, IsGameForeground: true);

        double peakLg = 0, peakRg = 0, peakLt = 0, peakRt = 0;
        for (int i = 0; i < 8; i++)
        {
            var eval = engine.Evaluate(footAudio, walkPad, HapticsGenreProfile.Auto, tuning, dtSeconds: 0.02);
            peakLg = Math.Max(peakLg, eval.Motors.LeftGrip);
            peakRg = Math.Max(peakRg, eval.Motors.RightGrip);
            peakLt = Math.Max(peakLt, eval.Motors.LeftTrigger);
            peakRt = Math.Max(peakRt, eval.Motors.RightTrigger);
        }

        Assert.Equal(0.0, peakLg, 3);
        Assert.Equal(0.0, peakLt, 3);
        Assert.Equal(0.0, peakRt, 3);
        Assert.True(peakRg is > 0.01 and <= 0.14, $"Expected faint RightGrip step in (0.01, 0.14], got {peakRg:F4}");
    }

    [Fact]
    public void DashAndDialogueCutscene_BehaveCleanly()
    {
        var engine = new HapticsFusionEngine();
        var tuning = new HapticsTuning { ComfortTier = HapticsComfortTier.Feather, MasterIntensity = 80 };
        var dashPad = new LivePadInput(0, 0.85, 0, 0, 0, 0, 1, true, PressedButtons: PadActionButtons.B, IsGameForeground: true);

        var dashEval = engine.Evaluate(SilentFrame, dashPad, HapticsGenreProfile.FpsAction, tuning, dtSeconds: 0.02);
        Assert.True(dashEval.Motors.RightGrip is > 0.02 and <= 0.14);
        Assert.Equal(0.0, dashEval.Motors.LeftGrip, 3);
        Assert.Equal(0.0, dashEval.Motors.LeftTrigger, 3);
        Assert.Equal(0.0, dashEval.Motors.RightTrigger, 3);

        // Fade to zero once dash ends
        var idlePad = new LivePadInput(0, 0, 0, 0, 0, 0, 0, false, IsGameForeground: true);
        for (int i = 0; i < 12; i++)
            dashEval = engine.Evaluate(SilentFrame, idlePad, HapticsGenreProfile.FpsAction, tuning, dtSeconds: 0.05);
        Assert.Equal(0.0, dashEval.Motors.RightGrip, 3);

        // Character speech / cutscene must stay silent across game modes
        var analyzer = new AudioSpectrumAnalyzer();
        var classifier = new HapticsAudioClassifier();
        var buf = new float[960 * 2];
        HapticsEvaluation talkEval = default;
        var talkEngine = new HapticsFusionEngine(classifier);
        for (int step = 0; step < 25; step++)
        {
            for (int i = 0; i < 960; i++)
            {
                double t = (step * 960 + i) / 48000.0;
                double s = 0.06 * (Math.Sin(2 * Math.PI * 165 * t) + 0.7 * Math.Sin(2 * Math.PI * 495 * t) + 0.5 * Math.Sin(2 * Math.PI * 1450 * t));
                buf[i * 2] = (float)s;
                buf[i * 2 + 1] = (float)s;
            }
            var af = analyzer.ProcessInterleaved(buf, 2, 48000, 0.02);
            talkEval = talkEngine.Evaluate(af, idlePad, HapticsGenreProfile.FpsAction, tuning, dtSeconds: 0.02);
        }
        Assert.Equal(0.0, talkEval.Motors.LeftGrip, 3);
        Assert.Equal(0.0, talkEval.Motors.RightGrip, 3);
        Assert.Equal(0.0, talkEval.Motors.LeftTrigger, 3);
        Assert.Equal(0.0, talkEval.Motors.RightTrigger, 3);
    }

    [Fact]
    public void OverlyLoudExplosion_ActivatesHeavyLeftGripAndTriggers()
    {
        var engine = new HapticsFusionEngine();
        var analyzer = new AudioSpectrumAnalyzer();
        var tuning = new HapticsTuning { ComfortTier = HapticsComfortTier.Balanced, MasterIntensity = 85, ImpactPunch = 90 };
        var pad = new LivePadInput(0, 0.5, 0, 0, 0, 0, 0, false, IsGameForeground: true);

        var buf = new float[960 * 2];
        var rng = new Random(99);
        double peakLg = 0, peakLt = 0, peakRt = 0;
        for (int step = 0; step < 12; step++)
        {
            for (int i = 0; i < 960; i++)
            {
                double t = (step * 960 + i) / 48000.0;
                double sub = step >= 3 ? 0.80 * Math.Sin(2 * Math.PI * 44 * t) + (rng.NextDouble() * 2 - 1) * 0.45 : 0.002;
                buf[i * 2] = (float)Math.Clamp(sub, -1.0, 1.0);
                buf[i * 2 + 1] = buf[i * 2];
            }
            var af = analyzer.ProcessInterleaved(buf, 2, 48000, 0.02);
            var eval = engine.Evaluate(af, pad, HapticsGenreProfile.FpsAction, tuning, dtSeconds: 0.02);
            peakLg = Math.Max(peakLg, eval.Motors.LeftGrip);
            peakLt = Math.Max(peakLt, eval.Motors.LeftTrigger);
            peakRt = Math.Max(peakRt, eval.Motors.RightTrigger);
        }

        Assert.True(peakLg > 0.35, $"Expected heavy LeftGrip on overly loud explosion, got {peakLg:F3}");
        Assert.True(peakLt > 0.15 && peakRt > 0.15, $"Expected trigger detail on overly loud explosion, got LT={peakLt:F3}, RT={peakRt:F3}");
    }
}
