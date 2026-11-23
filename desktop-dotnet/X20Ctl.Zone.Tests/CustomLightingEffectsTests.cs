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

    sealed class SteppingTimeProvider : TimeProvider
    {
        long ticks = TimeSpan.TicksPerSecond;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => ticks;
        public void Advance(TimeSpan delta) => ticks += delta.Ticks;
    }
}
