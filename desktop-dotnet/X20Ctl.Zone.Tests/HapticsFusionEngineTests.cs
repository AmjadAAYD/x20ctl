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

    [Fact]
    public void MusicMode_FaintIntro_StaysWhisperLightWithZeroHeavyLeftGrip()
    {
        var engine = new HapticsFusionEngine();
        var tuning = HapticsTuning.DefaultFor(HapticsGenreProfile.Music);
        var pad = new LivePadInput(0, 0, 0, 0, 0, 0, 0, false);
        var quietIntro = new AudioFrame() with
        {
            Level = 0.85f,
            Bass = 0.80f,
            Mid = 0.78f,
            Treble = 0.65f,
            BeatPulse = 0.0f,
            InstrumentEnergy = 0.82f,
            VocalPresence = 0.60f,
            DynamicContrast = 0.35f,
            RawRms = 0.025f,
            RawPeakSpike = 0.005f,
            Spectrum = new AudioSpectrum8(0.3f, 0.4f, 0.7f, 0.6f, 0.5f, 0.4f, 0.3f, 0.2f),
        };

        double peakLg = 0, peakRg = 0, peakLt = 0, peakRt = 0;
        for (int i = 0; i < 25; i++)
        {
            var eval = engine.Evaluate(quietIntro, pad, HapticsGenreProfile.Music, tuning, dtSeconds: 0.02);
            peakLg = Math.Max(peakLg, eval.Motors.LeftGrip);
            peakRg = Math.Max(peakRg, eval.Motors.RightGrip);
            peakLt = Math.Max(peakLt, eval.Motors.LeftTrigger);
            peakRt = Math.Max(peakRt, eval.Motors.RightTrigger);
        }

        Assert.Equal(0.0, peakLg, 3);
        Assert.True(peakRg is > 0.01 and <= 0.16, $"Expected faint RightGrip on quiet song intro in (0.01, 0.16], got {peakRg:F4}");
        Assert.True(peakLt <= 0.15, $"Expected faint LeftTrigger on quiet intro <= 0.15, got {peakLt:F4}");
        Assert.True(peakRt <= 0.15, $"Expected faint RightTrigger on quiet intro <= 0.15, got {peakRt:F4}");
    }

    [Fact]
    public void MusicMode_SteadyDroneWithoutBeats_BreathesDownInsteadOfConstantMax()
    {
        var engine = new HapticsFusionEngine();
        var tuning = HapticsTuning.DefaultFor(HapticsGenreProfile.Music);
        var pad = new LivePadInput(0, 0, 0, 0, 0, 0, 0, false);
        var steadyDrone = new AudioFrame() with
        {
            Level = 0.88f,
            Bass = 0.85f,
            Mid = 0.80f,
            Treble = 0.60f,
            BeatPulse = 0.0f,
            BassPunch = 0.25f,
            InstrumentEnergy = 0.85f,
            DynamicContrast = 0.12f,
            RawRms = 0.22f,
            RawPeakSpike = 0.0f,
            Spectrum = new AudioSpectrum8(0.6f, 0.7f, 0.7f, 0.6f, 0.5f, 0.4f, 0.3f, 0.2f),
        };

        HapticsEvaluation eval = default;
        for (int i = 0; i < 100; i++)
            eval = engine.Evaluate(steadyDrone, pad, HapticsGenreProfile.Music, tuning, dtSeconds: 0.02);

        Assert.True(eval.Motors.LeftGrip <= 0.25, $"Expected steady beatless wall of sound to breathe down on LeftGrip <= 0.25, got {eval.Motors.LeftGrip:F4}");
        Assert.True(eval.Motors.RightGrip <= 0.25, $"Expected steady beatless wall of sound to breathe down on RightGrip <= 0.25, got {eval.Motors.RightGrip:F4}");
    }

    [Fact]
    public void MusicMode_DynamicSpectrum_ScalesFromFaintToMediumToLoudDrop()
    {
        var engine = new HapticsFusionEngine();
        var tuning = HapticsTuning.DefaultFor(HapticsGenreProfile.Music);
        var pad = new LivePadInput(0, 0, 0, 0, 0, 0, 0, false);

        var faintIntro = new AudioFrame() with
        {
            Level = 0.65f,
            Bass = 0.40f,
            Mid = 0.65f,
            Treble = 0.45f,
            BeatPulse = 0.0f,
            BassPunch = 0.15f,
            InstrumentEnergy = 0.60f,
            DynamicContrast = 0.35f,
            RawRms = 0.028f,
            RawPeakSpike = 0.0f,
            Spectrum = new AudioSpectrum8(0.2f, 0.3f, 0.5f, 0.5f, 0.4f, 0.3f, 0.2f, 0.1f),
        };
        var mediumVerse = new AudioFrame() with
        {
            Level = 0.80f,
            Bass = 0.75f,
            Mid = 0.70f,
            Treble = 0.60f,
            BeatPulse = 0.75f,
            BassPunch = 0.70f,
            InstrumentEnergy = 0.65f,
            DynamicContrast = 0.70f,
            RawRms = 0.105f,
            RawPeakSpike = 0.03f,
            Spectrum = new AudioSpectrum8(0.5f, 0.7f, 0.6f, 0.5f, 0.4f, 0.3f, 0.2f, 0.2f),
        };
        var loudDrop = new AudioFrame() with
        {
            Level = 0.95f,
            Bass = 0.92f,
            Mid = 0.82f,
            Treble = 0.78f,
            BeatPulse = 0.96f,
            BassPunch = 0.95f,
            InstrumentEnergy = 0.85f,
            DynamicContrast = 0.95f,
            RawRms = 0.25f,
            RawPeakSpike = 0.11f,
            Spectrum = new AudioSpectrum8(0.9f, 0.9f, 0.8f, 0.6f, 0.5f, 0.4f, 0.3f, 0.2f),
        };

        double faintPeak = 0, mediumPeak = 0, loudPeak = 0;
        for (int i = 0; i < 25; i++)
        {
            var e = engine.Evaluate(faintIntro, pad, HapticsGenreProfile.Music, tuning, dtSeconds: 0.02);
            faintPeak = Math.Max(faintPeak, Math.Max(e.Motors.LeftGrip, e.Motors.RightGrip));
        }
        for (int i = 0; i < 20; i++)
        {
            var e = engine.Evaluate(mediumVerse, pad, HapticsGenreProfile.Music, tuning, dtSeconds: 0.02);
            mediumPeak = Math.Max(mediumPeak, Math.Max(e.Motors.LeftGrip, e.Motors.RightGrip));
        }
        for (int i = 0; i < 20; i++)
        {
            var e = engine.Evaluate(loudDrop, pad, HapticsGenreProfile.Music, tuning, dtSeconds: 0.02);
            loudPeak = Math.Max(loudPeak, Math.Max(e.Motors.LeftGrip, e.Motors.RightGrip));
        }

        Assert.True(faintPeak <= 0.16, $"Expected faintPeak <= 0.16, got {faintPeak:F4}");
        Assert.True(mediumPeak > faintPeak + 0.08, $"Expected mediumPeak ({mediumPeak:F4}) > faintPeak ({faintPeak:F4}) + 0.08");
        Assert.True(mediumPeak < 0.45, $"Expected mediumPeak < 0.45, got {mediumPeak:F4}");
        Assert.True(loudPeak > mediumPeak + 0.20, $"Expected loudPeak ({loudPeak:F4}) > mediumPeak ({mediumPeak:F4}) + 0.20");
        Assert.True(loudPeak >= 0.55, $"Expected loudPeak >= 0.55, got {loudPeak:F4}");
    }

    [Fact]
    public void AudioHapticsEngine_QuietRawRms_RespectsFaintSpectrum()
    {
        var quietIntro = new AudioFrame(
            Level: 0.85f,
            Bass: 0.80f,
            Mid: 0.78f,
            Treble: 0.65f,
            BeatPulse: 0.70f,
            BeatCount: 2,
            StereoPan: 0f,
            VocalPresence: 0.75f,
            VocalWarmth: 0.75f,
            VocalAir: 0.60f,
            InstrumentEnergy: 0.82f,
            BassPunch: 0.75f,
            DynamicContrast: 0.75f,
            Vibe: AudioMusicVibe.GroovePop,
            RawRms: 0.025f,
            RawPeakSpike: 0.005f);

        var h = AudioHapticsEngine.Evaluate(quietIntro, AudioHapticsMode.AutoAdaptive, 80);
        Assert.Equal(0.0, h.LeftGrip, 3);
        Assert.True(h.RightGrip <= 0.16, $"Expected faint RightGrip <= 0.16 on quiet RawRms, got {h.RightGrip:F4}");
        Assert.True(h.LeftTrigger <= 0.15, $"Expected faint LeftTrigger <= 0.15 on quiet RawRms, got {h.LeftTrigger:F4}");
    }

    [Fact]
    public void AutoDesktopQuietIntro_ResolvesMusicWithoutMenuMuting()
    {
        var engine = new HapticsFusionEngine();
        var tuning = HapticsTuning.DefaultFor(HapticsGenreProfile.Auto);
        var pad = new LivePadInput(0, 0, 0, 0, 0, 0, 0, false, CursorVisible: true, IsIdle: true, IsGameForeground: false);
        var quietMusic = new AudioFrame() with
        {
            Level = 0.72f,
            Bass = 0.45f,
            Mid = 0.65f,
            Treble = 0.48f,
            InstrumentEnergy = 0.62f,
            VocalPresence = 0.55f,
            DynamicContrast = 0.45f,
            RawRms = 0.025f,
            RawPeakSpike = 0.004f,
            Spectrum = new AudioSpectrum8(0.2f, 0.3f, 0.5f, 0.5f, 0.4f, 0.3f, 0.2f, 0.1f),
        };

        HapticsEvaluation eval = default;
        for (int i = 0; i < 15; i++)
            eval = engine.Evaluate(quietMusic, pad, HapticsGenreProfile.Auto, tuning, dtSeconds: 0.02);

        Assert.Equal(HapticsGenreProfile.Music, eval.ResolvedProfile);
        Assert.False(eval.IsMenuMuted);
        Assert.Equal(0.0, eval.Motors.LeftGrip, 3);
        Assert.True(eval.Motors.RightGrip is > 0.008 and <= 0.16, $"Expected faint RightGrip on desktop quiet intro, got {eval.Motors.RightGrip:F4}");
    }

    [Fact]
    public void ActiveGameplayWithVisibleCursor_DoesNotFalseMute()
    {
        var engine = new HapticsFusionEngine();
        var tuning = HapticsTuning.DefaultFor(HapticsGenreProfile.RpgMelee) with { MenuAutoMute = true };
        var cursorMenuPad = new LivePadInput(0, 0, 0, 0, 0, 0, 0, false, CursorVisible: true, IsIdle: false, IsGameForeground: true);
        var calmAudio = new AudioFrame() with
        {
            Level = 0.45f,
            Mid = 0.40f,
            InstrumentEnergy = 0.45f,
            RawRms = 0.04f,
            Spectrum = new AudioSpectrum8(0.2f, 0.2f, 0.3f, 0.3f, 0.2f, 0.2f, 0.1f, 0.1f),
        };

        for (int i = 0; i < 20; i++)
            engine.Evaluate(calmAudio, cursorMenuPad, HapticsGenreProfile.RpgMelee, tuning, dtSeconds: 0.05);

        var activeActionPad = new LivePadInput(0, 0.85, 0, 0, 0, 0, 1, true, CursorVisible: true, IsIdle: false, PressedButtons: PadActionButtons.X, IsGameForeground: true);
        var activeEval = engine.Evaluate(calmAudio, activeActionPad, HapticsGenreProfile.RpgMelee, tuning, dtSeconds: 0.02);

        Assert.False(activeEval.IsMenuMuted);
        Assert.True(activeEval.Motors.RightGrip > 0.01, $"Expected active melee swing RightGrip despite cursor overlay, got {activeEval.Motors.RightGrip:F4}");
    }

    [Fact]
    public void RpgMelee_RbLightAttack_RtHeavyAttack_AndDamageThud_DifferentiateCleanly()
    {
        var engine = new HapticsFusionEngine();
        var tuning = new HapticsTuning { ComfortTier = HapticsComfortTier.Balanced, MasterIntensity = 85, ImpactPunch = 85 };

        var rbPad = new LivePadInput(0, 0.3, 0, 0, 0, 0, 1, true, PressedButtons: PadActionButtons.RB, IsGameForeground: true);
        var rbEval = engine.Evaluate(SilentFrame, rbPad, HapticsGenreProfile.RpgMelee, tuning, dtSeconds: 0.02);
        Assert.True(rbEval.Motors.RightGrip > 0.08, $"Expected RB light attack RightGrip, got {rbEval.Motors.RightGrip:F4}");
        Assert.Equal(0.0, rbEval.Motors.LeftGrip, 3);
        Assert.Equal(0.0, rbEval.Motors.LeftTrigger, 3);
        Assert.Equal(0.0, rbEval.Motors.RightTrigger, 3);

        engine.Reset();
        var rtPad = new LivePadInput(0, 0.3, 0, 0, 0, 0.85, 0, true, IsGameForeground: true);
        var rtEval = engine.Evaluate(SilentFrame, rtPad, HapticsGenreProfile.RpgMelee, tuning, dtSeconds: 0.02);
        Assert.True(rtEval.Motors.RightGrip > rtEval.Motors.LeftGrip && rtEval.Motors.LeftGrip > 0.05,
            $"Expected RT heavy attack RG ({rtEval.Motors.RightGrip:F4}) > LG ({rtEval.Motors.LeftGrip:F4}) > 0.05");
        Assert.Equal(0.0, rtEval.Motors.LeftTrigger, 3);
        Assert.Equal(0.0, rtEval.Motors.RightTrigger, 3);

        engine.Reset();
        var idlePad = new LivePadInput(0, 0, 0, 0, 0, 0, 0, false, IsGameForeground: true);
        var hitAudio = new AudioFrame() with
        {
            Level = 0.72f,
            Bass = 0.65f,
            Mid = 0.55f,
            InstrumentEnergy = 0.30f,
            RawRms = 0.095f,
            RawPeakSpike = 0.085f,
            Spectrum = new AudioSpectrum8(0.4f, 0.6f, 0.5f, 0.3f, 0.2f, 0.2f, 0.1f, 0.1f),
        };
        var dmgEval = engine.Evaluate(hitAudio, idlePad, HapticsGenreProfile.RpgMelee, tuning, dtSeconds: 0.02);
        Assert.True(dmgEval.Motors.RightGrip > 0.08 && dmgEval.Motors.LeftGrip > 0.05,
            $"Expected damage thud feel on grips, got RG={dmgEval.Motors.RightGrip:F4}, LG={dmgEval.Motors.LeftGrip:F4}");
        Assert.Equal(0.0, dmgEval.Motors.LeftTrigger, 3);
        Assert.Equal(0.0, dmgEval.Motors.RightTrigger, 3);
    }

    [Fact]
    public void MusicMode_CustomMotorRoles_AvoidsDoubleLoudnessCrushing()
    {
        var engine = new HapticsFusionEngine();
        var tuning = HapticsTuning.DefaultFor(HapticsGenreProfile.Music) with { MasterIntensity = 85, ComfortTier = HapticsComfortTier.Balanced };
        var roles = new MotorRoleMap(MotorAudioRole.SubBassKick, MotorAudioRole.Instruments, MotorAudioRole.LeadVocals, MotorAudioRole.SnareHiHat);
        var pad = new LivePadInput(0, 0, 0, 0, 0, 0, 0, false);

        var quietAudio = new AudioFrame() with
        {
            Level = 0.80f,
            Bass = 0.75f,
            Mid = 0.75f,
            Treble = 0.60f,
            BeatPulse = 0.60f,
            InstrumentEnergy = 0.80f,
            VocalPresence = 0.75f,
            DynamicContrast = 0.60f,
            RawRms = 0.025f,
            RawPeakSpike = 0.004f,
            Spectrum = new AudioSpectrum8(0.4f, 0.5f, 0.6f, 0.6f, 0.5f, 0.4f, 0.3f, 0.2f),
        };
        var quietEval = engine.Evaluate(quietAudio, pad, HapticsGenreProfile.Music, tuning, roles, dtSeconds: 0.02);
        Assert.Equal(0.0, quietEval.Motors.LeftGrip, 3);
        Assert.True(quietEval.Motors.RightGrip <= 0.16, $"Expected faint custom-role RightGrip <= 0.16 on quiet audio, got {quietEval.Motors.RightGrip:F4}");

        var loudAudio = quietAudio with
        {
            Level = 0.95f,
            Bass = 0.92f,
            BeatPulse = 0.96f,
            BassPunch = 0.95f,
            VocalPresence = 0.90f,
            VocalWarmth = 0.90f,
            DynamicContrast = 0.92f,
            RawRms = 0.24f,
            RawPeakSpike = 0.10f,
        };
        double peakLg = 0, peakLt = 0;
        for (int i = 0; i < 12; i++)
        {
            var e = engine.Evaluate(loudAudio, pad, HapticsGenreProfile.Music, tuning, roles, dtSeconds: 0.02);
            peakLg = Math.Max(peakLg, e.Motors.LeftGrip);
            peakLt = Math.Max(peakLt, e.Motors.LeftTrigger);
        }

        Assert.True(peakLg >= 0.40, $"Expected punchy custom-role LeftGrip >= 0.40 on loud drop, got {peakLg:F4}");
        Assert.True(peakLt >= 0.30, $"Expected expressive custom-role vocal trigger >= 0.30 on loud chorus, got {peakLt:F4}");
    }
}
