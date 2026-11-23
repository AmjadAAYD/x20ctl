using X20Ctl.Zone.Lighting;

namespace X20Ctl.Zone.Tests;

public sealed class CustomLightingEffectsTests
{
    [Fact]
    public void WithSelection_ClearsHardwareRainbowByte4_AndAppliesFrame()
    {
        var hardwareRainbow = Convert.FromHexString("FF00000003FFB001001080400000FF408080808080A080FF");
        var frame = LightingRecord.WithFrame(hardwareRainbow, 0x12, 0x34, 0x56, LightingRecord.MoonlightBrightnessByte, LightingRecord.ModeSolid);

        Assert.Equal(0x12, frame[0]);
        Assert.Equal(0x34, frame[1]);
        Assert.Equal(0x56, frame[2]);
        Assert.Equal(0x00, frame[4]);
        Assert.Equal(0x01, frame[5]);
        Assert.Equal(LightingRecord.ModeSolid, frame[6]);
        Assert.Equal(hardwareRainbow[7..], frame[7..]);
    }

    [Fact]
    public void RainbowMode_RoundTripsThroughWithModeAndWithSelection()
    {
        var red = Convert.FromHexString("FF00000000FFB002001080400000FF408080808080A080FF");
        var rainbow = LightingRecord.WithSelection(red, 0x00, 0xFF, 0xCC, LightingRecord.ModeRainbow);
        Assert.Equal(LightingRecord.ModeRainbow, rainbow[6]);
        Assert.Equal(0x00, rainbow[0]);
        Assert.Equal(0xFF, rainbow[1]);
        Assert.Equal(0xCC, rainbow[2]);
        Assert.Equal("Rainbow", LightingRecord.ModeName(LightingRecord.ModeRainbow));
    }

    [Fact]
    public void EffectSpec_NormalizesIntervalFloorBrightnessAndPalette()
    {
        var raw = new LightingEffectSpec
        {
            Kind = LightingEffectKind.PaletteFade,
            Palette = ["invalid", "00ccff", "#ff00aa"],
            MinBrightness = 0x90,
            MaxBrightness = 0x20,
            StepIntervalMs = 50,
            CyclePeriodMs = 100,
        };

        var norm = raw.Normalized();
        Assert.Equal(["#00CCFF", "#FF00AA"], norm.Palette);
        Assert.Equal(0x20, norm.MaxBrightness);
        Assert.Equal(0x20, norm.MinBrightness);
        Assert.Equal(LightingEffectSpec.MinStepIntervalMs, norm.StepIntervalMs);
        Assert.Equal(LightingEffectSpec.MinCyclePeriodMs, norm.CyclePeriodMs);
        Assert.True(norm.IsStreamedSoftwareEffect);
    }

    [Fact]
    public void MoonlightPulse_HitsMoonlightMinAndPeakMax()
    {
        var spec = new LightingEffectSpec
        {
            Kind = LightingEffectKind.MoonlightPulse,
            Palette = ["#00FFFF"],
            MinBrightness = LightingRecord.MoonlightBrightnessByte,
            MaxBrightness = 0x81,
            CyclePeriodMs = 4000,
        };

        var atStart = LightingEffectEngine.Evaluate(spec, TimeSpan.Zero);
        var atHalf = LightingEffectEngine.Evaluate(spec, TimeSpan.FromMilliseconds(2000));

        Assert.Equal(0x01, atStart.Brightness);
        Assert.Equal(0x81, atHalf.Brightness);
        Assert.Equal("#00FFFF", atHalf.Hex);
        Assert.Equal(LightingRecord.ModeSolid, atHalf.NativeMode);
    }

    [Fact]
    public void SmoothHueWave_SweepsDistinctHuesOverPeriod()
    {
        var spec = new LightingEffectSpec
        {
            Kind = LightingEffectKind.SmoothHueWave,
            CyclePeriodMs = 3000,
            MaxBrightness = 0xE0,
        };

        var f0 = LightingEffectEngine.Evaluate(spec, TimeSpan.Zero);
        var f1 = LightingEffectEngine.Evaluate(spec, TimeSpan.FromMilliseconds(1000));
        var f2 = LightingEffectEngine.Evaluate(spec, TimeSpan.FromMilliseconds(2000));

        Assert.Equal((255, 0, 0), (f0.R, f0.G, f0.B));
        Assert.Equal((0, 255, 0), (f1.R, f1.G, f1.B));
        Assert.Equal((0, 0, 255), (f2.R, f2.G, f2.B));
        Assert.Equal(0xE0, f1.Brightness);
    }

    [Fact]
    public void PaletteFadeAndDualStrobe_ProduceExpectedFrames()
    {
        var fade = new LightingEffectSpec
        {
            Kind = LightingEffectKind.PaletteFade,
            Palette = ["#FF0000", "#0000FF"],
            CyclePeriodMs = 2000,
            MaxBrightness = 0xFF,
        };
        var midFade = LightingEffectEngine.Evaluate(fade, TimeSpan.FromMilliseconds(500));
        Assert.InRange(midFade.R, 126, 129);
        Assert.InRange(midFade.B, 126, 129);
        Assert.Equal(0, midFade.G);

        var strobe = new LightingEffectSpec
        {
            Kind = LightingEffectKind.DualStrobe,
            Palette = ["#FF0020", "#0055FF"],
            StepIntervalMs = 400,
            MaxBrightness = 0xFF,
        };
        Assert.Equal("#FF0020", LightingEffectEngine.Evaluate(strobe, TimeSpan.FromMilliseconds(0)).Hex);
        Assert.Equal("#0055FF", LightingEffectEngine.Evaluate(strobe, TimeSpan.FromMilliseconds(450)).Hex);
    }

    [Fact]
    public void ReactiveModes_ReactToTriggerStickButtonAndIdle()
    {
        var triggerSpec = new LightingEffectSpec
        {
            Kind = LightingEffectKind.TriggerGlow,
            Palette = ["#0000FF", "#FF0000"],
            MinBrightness = 0x10,
            MaxBrightness = 0xFF,
        };
        var restTrigger = LightingEffectEngine.Evaluate(triggerSpec, TimeSpan.Zero, new LightingInputState(0, 0, 0, 0, false, 0));
        var fullTrigger = LightingEffectEngine.Evaluate(triggerSpec, TimeSpan.Zero, new LightingInputState(0, 0, 0, 1.0, false, 0));
        Assert.Equal("#0000FF", restTrigger.Hex);
        Assert.Equal(0x10, restTrigger.Brightness);
        Assert.Equal("#FF0000", fullTrigger.Hex);
        Assert.Equal(0xFF, fullTrigger.Brightness);

        var flashSpec = new LightingEffectSpec
        {
            Kind = LightingEffectKind.ButtonFlash,
            Palette = ["#102030", "#00FFAA"],
            MinBrightness = 0x18,
            MaxBrightness = 0xFF,
        };
        var flashOn = LightingEffectEngine.Evaluate(flashSpec, TimeSpan.Zero, default, TimeSpan.FromMilliseconds(120));
        var flashOff = LightingEffectEngine.Evaluate(flashSpec, TimeSpan.Zero, default, TimeSpan.FromSeconds(2));
        Assert.Equal("#00FFAA", flashOn.Hex);
        Assert.Equal(0xFF, flashOn.Brightness);
        Assert.Equal("#102030", flashOff.Hex);
        Assert.Equal(0x18, flashOff.Brightness);

        var idleSpec = new LightingEffectSpec
        {
            Kind = LightingEffectKind.IdleSleepDim,
            Palette = ["#FFFFFF"],
            MinBrightness = LightingRecord.MoonlightBrightnessByte,
            MaxBrightness = 0xC0,
            IdleTimeoutSeconds = 30,
        };
        var active = LightingEffectEngine.Evaluate(idleSpec, TimeSpan.Zero, new LightingInputState(0, 0, 0, 0, false, 5));
        var asleep = LightingEffectEngine.Evaluate(idleSpec, TimeSpan.Zero, new LightingInputState(0, 0, 0, 0, false, 35));
        Assert.Equal(0xC0, active.Brightness);
        Assert.Equal(0x01, asleep.Brightness);
    }

    [Fact]
    public void PresetsStore_SavesLoadsAndResolvesForegroundGameRules()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "x20ctl-lighting-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            string file = Path.Combine(tempDir, "presets.json");
            var store = new LightingPresetsStore(file);
            store.Load();

            var preset = store.SavePreset("Elden Glow", new LightingEffectSpec
            {
                Kind = LightingEffectKind.EmberShimmer,
                Palette = ["#FFAA00", "#FF3300"],
                MaxBrightness = 0xC0,
            });
            store.SetGameRule("eldenring", preset.Id);
            store.RememberLastEffect(preset.Spec);

            var reloaded = new LightingPresetsStore(file);
            reloaded.Load();
            Assert.Single(reloaded.Presets);
            Assert.Equal("Elden Glow", reloaded.Presets[0].Name);
            Assert.Equal(LightingEffectKind.EmberShimmer, reloaded.LastEffect?.Kind);

            var matched = reloaded.ResolveForExe("EldenRing.exe");
            Assert.NotNull(matched);
            Assert.Equal(preset.Id, matched!.Id);

            Assert.True(reloaded.DeletePreset(preset.Id));
            Assert.Empty(reloaded.Presets);
            Assert.Empty(reloaded.GameRules);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Theory]
    [InlineData("rainbow lights", LightingEffectKind.NativeRainbow)]
    [InlineData("turn off the lights", LightingEffectKind.NativeOff)]
    [InlineData("moonlight pulse", LightingEffectKind.MoonlightPulse)]
    [InlineData("lava shimmer", LightingEffectKind.EmberShimmer)]
    [InlineData("trigger glow", LightingEffectKind.TriggerGlow)]
    [InlineData("stick heat", LightingEffectKind.StickHeat)]
    [InlineData("hue sweep lights", LightingEffectKind.SmoothHueWave)]
    [InlineData("police strobe lights", LightingEffectKind.DualStrobe)]
    public void AgentDirectAndApplier_HandleLightingEffects(string sentence, LightingEffectKind expectedKind)
    {
        var proposal = X20Ctl.Zone.Agent.AgentDirect.Resolve(sentence);
        Assert.NotNull(proposal);
        var verdict = X20Ctl.Zone.Agent.ProposalValidator.Check(X20Ctl.Zone.Agent.AgentClarification.Json(proposal!), "x20_pro");
        Assert.True(verdict.Valid, verdict.StructureError ?? string.Join("; ", verdict.Checks.Select(c => c.Message)));
        var outcome = X20Ctl.Zone.Agent.AgentApplier.Apply(new X20Ctl.Zone.PcMode.PcModeProfile(), verdict);
        Assert.NotNull(outcome.LightingEffect);
        Assert.Equal(expectedKind, outcome.LightingEffect!.Kind);
    }

    [Fact]
    public void SetupSnapshot_LightingRoundTripsWithoutBreakingLegacySnapshots()
    {
        string dir = Path.Combine(Path.GetTempPath(), "x20ctl-setup-lit-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var store = new SetupStore(Path.Combine(dir, "setups.json"));
            store.Load();
            var fx = new LightingEffectSpec
            {
                Kind = LightingEffectKind.PaletteFade,
                Palette = ["#00FFFF", "#FF00AA"],
                MaxBrightness = 0xD0,
            };
            var snap = SetupSnapshot.Capture("x20_pro", new("x20_pro"), new("x20_pro"), new("x20_pro"), new("x20_pro"))
                .WithLighting(fx);
            var item = store.Create("Cyberpunk Setup", snap);

            var reloaded = new SetupStore(Path.Combine(dir, "setups.json"));
            reloaded.Load();
            var loaded = reloaded.NativeSnapshot(item.Id);
            Assert.NotNull(loaded.Lighting);
            Assert.Equal(LightingEffectKind.PaletteFade, loaded.Lighting!.Kind);
            Assert.Equal(["#00FFFF", "#FF00AA"], loaded.Lighting.Palette);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task LightingTransaction_WriteRecordAsync_MatchesRunAsyncExactWireChunks()
    {
        byte[] baseline = Convert.FromHexString("FFFFFF0000FF0201001080400000FF408080808080A080FF");
        byte[] target = LightingRecord.WithFrame(baseline, 0x1B, 0x5C, 0xFF, 0x80, LightingRecord.ModeSolid);

        var runPackets = new List<(byte Op, byte[] Payload, byte Serial, byte Length)>();
        var streamPackets = new List<(byte Op, byte[] Payload, byte Serial, byte Length)>();

        string ckpt = Path.Combine(Path.GetTempPath(), "x20ctl-tx-wire-" + Guid.NewGuid().ToString("N"));
        try
        {
            byte[] afterRun = baseline;
            byte seq1 = 0;
            await LightingTransaction.RunAsync((op, payload, serial, length) =>
            {
                if (op == 0x37)
                {
                    runPackets.Add((op, (byte[])payload.Clone(), serial, length));
                    if (payload.Length == 15 && payload[0] == 24)
                        afterRun = [.. payload[1..], .. afterRun[14..]];
                    else if (payload.Length == 10)
                        afterRun = [.. afterRun[..14], .. payload];
                    return Task.FromResult(new byte[] { 2, 0xdf, 0xaa });
                }
                // 0xB7 readback
                return Task.FromResult(payload[0] == 0 ? [24, .. afterRun[..14]] : afterRun[14..]);
            }, () => ++seq1, [0x1B, 0x5C, 0xFF], LightingRecord.ModeSolid, 0x80, ckpt, TimeSpan.Zero);

            await LightingTransaction.WriteRecordAsync((op, payload, serial, length) =>
            {
                streamPackets.Add((op, (byte[])payload.Clone(), serial, length));
                return Task.FromResult(new byte[] { 2, 0xdf, 0xaa });
            }, target, TimeSpan.Zero);

            Assert.Equal(2, runPackets.Count);
            Assert.Equal(2, streamPackets.Count);
            Assert.Equal(15, streamPackets[0].Payload.Length);
            Assert.Equal(24, streamPackets[0].Payload[0]);
            Assert.Equal(10, streamPackets[1].Payload.Length);
            for (int i = 0; i < 2; i++)
            {
                Assert.Equal(runPackets[i].Op, streamPackets[i].Op);
                Assert.Equal(runPackets[i].Serial, streamPackets[i].Serial);
                Assert.Equal(runPackets[i].Length, streamPackets[i].Length);
                Assert.Equal(runPackets[i].Payload, streamPackets[i].Payload);
            }
        }
        finally
        {
            if (Directory.Exists(ckpt)) Directory.Delete(ckpt, true);
        }
    }

    [Fact]
    public void AudioLevelMeter_AttackReleaseClampingAndIdleDecay()
    {
        var clock = new SteppingTimeProvider();
        var meter = new AudioLevelMeter(clock);

        Assert.Equal(0f, meter.Level);

        meter.Process([0.2f, -0.8f], 0.06);
        float afterRise = meter.Level;
        Assert.InRange(afterRise, 0.45f, 0.60f);

        // NaN samples are ignored safely; release decays slower than attack.
        clock.Advance(TimeSpan.FromMilliseconds(50));
        meter.Process([float.NaN, 0f], 0.05);
        float afterDecay = meter.Level;
        Assert.InRange(afterDecay, 0.25f, afterRise);

        // Idle timeout (> 0.50 s) drops stale level cleanly to zero.
        clock.Advance(TimeSpan.FromMilliseconds(600));
        Assert.Equal(0f, meter.Level);

        meter.ProcessPeak(5.0f, 0.50);
        Assert.InRange(meter.Level, 0.99f, 1.0f);
        meter.Reset();
        Assert.Equal(0f, meter.Level);
    }

    [Fact]
    public void AudioReactive_EvaluatesPaletteStopsGainAndBrightness()
    {
        var threeStop = new LightingEffectSpec
        {
            Kind = LightingEffectKind.AudioReactive,
            Palette = ["#0000FF", "#00FF00", "#FF0000"],
            MinBrightness = 0x20,
            MaxBrightness = 0xFF,
            AudioSensitivity = 100, // gain = 0.5 + 3.5 = 4.0
        };

        var quiet = LightingEffectEngine.Evaluate(threeStop, TimeSpan.Zero, new LightingInputState(0, 0, 0, 0, false, 0, AudioLevel: 0.0));
        var mid = LightingEffectEngine.Evaluate(threeStop, TimeSpan.Zero, new LightingInputState(0, 0, 0, 0, false, 0, AudioLevel: 0.125)); // 0.125 * 4 = 0.5
        var peak = LightingEffectEngine.Evaluate(threeStop, TimeSpan.Zero, new LightingInputState(0, 0, 0, 0, false, 0, AudioLevel: 0.5)); // clamped to 1.0

        Assert.Equal("#0000FF", quiet.Hex);
        Assert.Equal(0x20, quiet.Brightness);
        Assert.Equal("#00FF00", mid.Hex);
        Assert.Equal("#FF0000", peak.Hex);
        Assert.Equal(0xFF, peak.Brightness);

        var twoStop = threeStop with { Palette = ["#102030", "#FFFFFF"], AudioSensitivity = 0 }; // gain = 0.5
        var twoPeak = LightingEffectEngine.Evaluate(twoStop, TimeSpan.Zero, new LightingInputState(0, 0, 0, 0, false, 0, AudioLevel: 1.0)); // 0.5
        Assert.InRange(twoPeak.R, 130, 140);
        Assert.Equal(LightingRecord.ModeSolid, twoPeak.NativeMode);
    }

    [Fact]
    public void AudioSpectrumAnalyzer_SeparatesBassAndTrebleDetectsBeatsAndDecaysOnIdle()
    {
        static float[] MakeStereoSine(double freqHz, int sampleRate, int frames, float amp)
        {
            var buf = new float[frames * 2];
            for (int i = 0; i < frames; i++)
            {
                float s = (float)(amp * Math.Sin(2.0 * Math.PI * freqHz * i / sampleRate));
                buf[i * 2] = s;
                buf[i * 2 + 1] = s;
            }
            return buf;
        }

        var clock = new SteppingTimeProvider();
        var analyzer = new AudioSpectrumAnalyzer(clock);

        var bassBuf = MakeStereoSine(100.0, 48000, 1024, 0.5f);
        var bassFrame = analyzer.ProcessInterleaved(bassBuf, 2, 48000, 1024.0 / 48000.0);
        Assert.True(bassFrame.Bass > bassFrame.Treble * 3f, $"Expected Bass >> Treble, got Bass={bassFrame.Bass}, Treble={bassFrame.Treble}");
        Assert.True(bassFrame.BeatCount >= 1);
        Assert.True(bassFrame.BeatPulse > 0.7f);

        analyzer.Reset();
        var trebleBuf = MakeStereoSine(6000.0, 48000, 1024, 0.5f);
        var trebleFrame = analyzer.ProcessInterleaved(trebleBuf, 2, 48000, 1024.0 / 48000.0);
        Assert.True(trebleFrame.Treble > trebleFrame.Bass * 3f, $"Expected Treble >> Bass, got Treble={trebleFrame.Treble}, Bass={trebleFrame.Bass}");

        clock.Advance(TimeSpan.FromMilliseconds(600));
        var idle = analyzer.CurrentFrame;
        Assert.Equal(0f, idle.Level);
        Assert.Equal(0f, idle.Bass);
        Assert.Equal(0f, idle.Treble);
    }

    [Fact]
    public void AudioReactiveStyles_ProduceDistinctAnimatedResponses()
    {
        var palette = new[] { "#FF0000", "#00FF00", "#0000FF" };

        // SpectrumFlow blends Bass (Red) vs Treble (Blue) dynamically.
        var specFlow = new LightingEffectSpec { Kind = LightingEffectKind.AudioReactive, AudioStyle = AudioReactiveStyle.SpectrumFlow, Palette = palette, AudioSensitivity = 65 };
        var bassHit = LightingEffectEngine.Evaluate(specFlow, TimeSpan.Zero, new LightingInputState(0, 0, 0, 0, false, 0, AudioLevel: 0.7, AudioBass: 0.9, AudioMid: 0.05, AudioTreble: 0.05));
        var hiHit = LightingEffectEngine.Evaluate(specFlow, TimeSpan.Zero, new LightingInputState(0, 0, 0, 0, false, 0, AudioLevel: 0.7, AudioBass: 0.05, AudioMid: 0.05, AudioTreble: 0.9));
        Assert.True(bassHit.R > bassHit.B);
        Assert.True(hiHit.B > hiHit.R);

        // BeatShift cycles palette stops per AudioBeatCount.
        var beatShift = specFlow with { AudioStyle = AudioReactiveStyle.BeatShift };
        var b0 = LightingEffectEngine.Evaluate(beatShift, TimeSpan.Zero, new LightingInputState(0, 0, 0, 0, false, 0, AudioLevel: 0.6, AudioBeatPulse: 0.9, AudioBeatCount: 0));
        var b1 = LightingEffectEngine.Evaluate(beatShift, TimeSpan.Zero, new LightingInputState(0, 0, 0, 0, false, 0, AudioLevel: 0.6, AudioBeatPulse: 0.9, AudioBeatCount: 1));
        var b2 = LightingEffectEngine.Evaluate(beatShift, TimeSpan.Zero, new LightingInputState(0, 0, 0, 0, false, 0, AudioLevel: 0.6, AudioBeatPulse: 0.9, AudioBeatCount: 2));
        Assert.True(b0.R > b0.G && b0.R > b0.B);
        Assert.True(b1.G > b1.R && b1.G > b1.B);
        Assert.True(b2.B > b2.R && b2.B > b2.G);

        // BassFire bursts hot orange/gold on bass kicks.
        var bassFire = specFlow with { AudioStyle = AudioReactiveStyle.BassFire };
        var fireHot = LightingEffectEngine.Evaluate(bassFire, TimeSpan.Zero, new LightingInputState(0, 0, 0, 0, false, 0, AudioLevel: 0.9, AudioBass: 0.95, AudioBeatPulse: 0.9));
        Assert.True(fireHot.R > fireHot.B);

        // AuroraDrift moves smoothly in hue over time + mid energy.
        var aurora = specFlow with { AudioStyle = AudioReactiveStyle.AuroraDrift };
        var a0 = LightingEffectEngine.Evaluate(aurora, TimeSpan.Zero, new LightingInputState(0, 0, 0, 0, false, 0, AudioLevel: 0.5, AudioMid: 0.3));
        var a1 = LightingEffectEngine.Evaluate(aurora, TimeSpan.FromSeconds(3.5), new LightingInputState(0, 0, 0, 0, false, 0, AudioLevel: 0.5, AudioMid: 0.8));
        Assert.NotEqual(a0.Hex, a1.Hex);

        // DropStrobe hits full MaxBrightness on heavy drops.
        var dropStrobe = specFlow with { AudioStyle = AudioReactiveStyle.DropStrobe, MaxBrightness = 0xFF };
        var dropped = LightingEffectEngine.Evaluate(dropStrobe, TimeSpan.Zero, new LightingInputState(0, 0, 0, 0, false, 0, AudioLevel: 0.9, AudioBass: 0.9, AudioBeatPulse: 0.85));
        Assert.Equal(0xFF, dropped.Brightness);
    }

    [Fact]
    public void AudioSpectrumAnalyzer_TracksStereoPanCentroidAndTrebleTransients()
    {
        var clock = new SteppingTimeProvider();
        var analyzer = new AudioSpectrumAnalyzer(clock);

        // Pure left-channel 100 Hz bass sine
        var leftOnly = new float[1024 * 2];
        for (int i = 0; i < 1024; i++)
            leftOnly[i * 2] = (float)(0.6 * Math.Sin(2.0 * Math.PI * 100.0 * i / 48000.0));

        var fLeft = analyzer.ProcessInterleaved(leftOnly, 2, 48000, 1024.0 / 48000.0);
        Assert.True(fLeft.LeftLevel > fLeft.RightLevel * 3f, $"Expected Left >> Right, got L={fLeft.LeftLevel}, R={fLeft.RightLevel}");
        Assert.InRange(fLeft.SpectralCentroid, 0f, 0.45f);

        // Pure right-channel 6000 Hz treble spike
        analyzer.Reset();
        var rightTreble = new float[1024 * 2];
        for (int i = 0; i < 1024; i++)
            rightTreble[i * 2 + 1] = (float)(0.6 * Math.Sin(2.0 * Math.PI * 6000.0 * i / 48000.0));

        clock.Advance(TimeSpan.FromMilliseconds(25));
        var fRight = analyzer.ProcessInterleaved(rightTreble, 2, 48000, 1024.0 / 48000.0);
        Assert.True(fRight.RightLevel > fRight.LeftLevel * 3f, $"Expected Right >> Left, got R={fRight.RightLevel}, L={fRight.LeftLevel}");
        Assert.True(fRight.SpectralCentroid > 0.70f, $"Expected high centroid for 6 kHz, got {fRight.SpectralCentroid}");
        Assert.True(fRight.TrebleTransient > 0.70f, $"Expected sharp treble onset spike, got {fRight.TrebleTransient}");
    }

    [Fact]
    public void AudioHapticsEngine_DrivesAllFourMotorsAcrossFiveModesAndZerosWhenSilent()
    {
        Assert.True(AudioHapticsEngine.Evaluate(new AudioFrame(0.9f, 0.9f, 0.5f, 0.9f, 1f, 1, 0f), AudioHapticsMode.QuadConcert, 0).IsSilent);
        Assert.True(AudioHapticsEngine.Evaluate(new AudioFrame(0f, 0f, 0f, 0f, 0f, 0, 0f), AudioHapticsMode.QuadConcert, 100).IsSilent);

        var leftHeavy = new AudioFrame(0.8f, 0.85f, 0.4f, 0.9f, 0.9f, 2, -0.8f, LeftLevel: 0.95f, RightLevel: 0.05f, Energy: 0.8f, TrebleTransient: 0.95f);
        var qc = AudioHapticsEngine.Evaluate(leftHeavy, AudioHapticsMode.QuadConcert, 100);
        Assert.True(qc.LeftGrip > 0.7 && qc.LeftTrigger > 0.7 && qc.RightTrigger > 0.7);

        var tp = AudioHapticsEngine.Evaluate(leftHeavy with { Bass = 0.2f, BeatPulse = 0.1f, Treble = 0.9f, TrebleTransient = 1.0f }, AudioHapticsMode.TriggerPulse, 100);
        Assert.True(tp.RightTrigger > tp.LeftGrip);

        var sw = AudioHapticsEngine.Evaluate(leftHeavy, AudioHapticsMode.StereoWave, 100);
        Assert.True(sw.LeftGrip > sw.RightGrip, $"Expected LeftGrip > RightGrip on left-panned audio, got LG={sw.LeftGrip}, RG={sw.RightGrip}");

        var calmVerse = new AudioFrame(0.25f, 0.25f, 0.35f, 0.40f, 0.0f, 0, 0f, Energy: 0.20f);
        var dropCalm = AudioHapticsEngine.Evaluate(calmVerse, AudioHapticsMode.SubwooferDrop, 100);
        Assert.Equal(0.0, dropCalm.LeftGrip);
        Assert.True(dropCalm.RightTrigger > 0.0);

        // Heartbeat Lub (triggers first at BeatPulse peak) then Dub (grips in decay).
        var lub = AudioHapticsEngine.Evaluate(leftHeavy with { BeatPulse = 0.90f }, AudioHapticsMode.Heartbeat, 100);
        var dub = AudioHapticsEngine.Evaluate(leftHeavy with { BeatPulse = 0.35f, Bass = 0.7f, TrebleTransient = 0f }, AudioHapticsMode.Heartbeat, 100);
        Assert.True(lub.LeftTrigger > lub.LeftGrip);
        Assert.True(dub.LeftGrip > dub.LeftTrigger);
    }

    [Fact]
    public void SpectrumFlowHsv_StaysVividSaturatedAcrossDominantBandsAndFlashesOnTrebleSparkle()
    {
        var spec = new LightingEffectSpec
        {
            Kind = LightingEffectKind.AudioReactive,
            AudioStyle = AudioReactiveStyle.SpectrumFlow,
            Palette = ["#FF0040", "#00F5D4", "#FFB800"],
            AudioSensitivity = 65,
        };

        var frame = LightingEffectEngine.Evaluate(spec, TimeSpan.Zero, new LightingInputState(
            0, 0, 0, 0, false, 0,
            AudioLevel: 0.8, AudioBass: 0.85, AudioMid: 0.80, AudioTreble: 0.80,
            AudioBeatPulse: 0.9, AudioBeatCount: 1, AudioCentroid: 0.5));
        byte minChannel = Math.Min(frame.R, Math.Min(frame.G, frame.B));
        byte maxChannel = Math.Max(frame.R, Math.Max(frame.G, frame.B));
        // HSV mixing keeps a high saturation contrast (no muddy washed-out pink/white).
        Assert.True(maxChannel - minChannel >= 120, $"Expected vivid saturated output, got {frame.Hex}");

        var sparkle = LightingEffectEngine.Evaluate(spec, TimeSpan.Zero, new LightingInputState(
            0, 0, 0, 0, false, 0,
            AudioLevel: 0.75, AudioBass: 0.3, AudioMid: 0.4, AudioTreble: 0.95,
            AudioBeatPulse: 0.1, AudioBeatCount: 1, AudioCentroid: 0.85, AudioTrebleTransient: 0.92));
        Assert.NotEqual(frame.Hex, sparkle.Hex);
    }

    sealed class SteppingTimeProvider : TimeProvider
    {
        long ticks = TimeSpan.TicksPerSecond;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => ticks;
        public void Advance(TimeSpan delta) => ticks += delta.Ticks;
    }
}
