using X20Ctl.Zone;
using X20Ctl.Zone.Lighting;
using X20Ctl.Zone.Mouse;
using X20Ctl.Zone.PcMode;
using X20Ctl.Zone.Screen;

namespace X20Ctl.Zone.Tests;

public class Phase3FeaturesTests
{
    [Fact]
    public void GameAutomationStoreNormalizesAndPersistsRules()
    {
        string dir = Path.Combine(Path.GetTempPath(), "x20ctl-p3-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string path = Path.Combine(dir, "game-automation.json");
            var store = new GameAutomationStore(path);
            store.Upsert(new GameAutomationRule
            {
                ExeName = "vlC",
                ForceEcoMode = true,
                RgbBrightnessPct = 15,
                RgbPresetId = "builtin-hue-sweep",
                HapticsProfile = HapticsGenreProfile.Cinema,
            });

            var reloaded = new GameAutomationStore(path);
            var rule = reloaded.FindForExe("vlc.exe");
            Assert.NotNull(rule);
            Assert.Equal("vlc.exe", rule!.ExeName);
            Assert.True(rule.ForceEcoMode);
            Assert.Equal(15, rule.RgbBrightnessPct);
            Assert.Equal(HapticsGenreProfile.Cinema, rule.HapticsProfile);

            Assert.True(reloaded.Remove("vlc"));
            Assert.Null(reloaded.FindForExe("vlc.exe"));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void RadialMenuConfigValidatesAndRoundTrips()
    {
        string dir = Path.Combine(Path.GetTempPath(), "x20ctl-rad-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string path = Path.Combine(dir, "radial-menu.json");
            var cfg = new RadialMenuConfig();
            cfg.Validate();
            Assert.Equal(8, cfg.Slices.Count);

            cfg.HoldMs = 250;
            cfg.Save(path);
            var loaded = RadialMenuConfig.Load(path);
            Assert.Equal(250, loaded.HoldMs);
            Assert.Equal(8, loaded.Slices.Count);

            cfg.Slices.RemoveAt(0);
            Assert.Throws<InvalidDataException>(() => cfg.Validate());
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void MouseMapperScrollAccelerationScalesWheelDelta()
    {
        var linearProfile = new MouseProfile { ScrollAcceleration = 1.0, ScrollSpeed = 12, Deadzone = 8 };
        var accelProfile = new MouseProfile { ScrollAcceleration = 3.5, ScrollSpeed = 12, Deadzone = 8 };
        var midTiltPad = new PadState(0, 0, 0, 0, 0, 0, 16000);
        var fullTiltPad = new PadState(0, 0, 0, 0, 0, 0, 32767);

        var mLinMid = new MouseMapper(linearProfile);
        var mAccMid = new MouseMapper(accelProfile);
        var mLinFull = new MouseMapper(linearProfile);
        var mAccFull = new MouseMapper(accelProfile);

        int midLin = 0, midAcc = 0, fullLin = 0, fullAcc = 0;
        for (int i = 0; i < 40; i++)
        {
            midLin += Math.Abs(mLinMid.Step(midTiltPad, 0.016).Wheel);
            midAcc += Math.Abs(mAccMid.Step(midTiltPad, 0.016).Wheel);
            fullLin += Math.Abs(mLinFull.Step(fullTiltPad, 0.016).Wheel);
            fullAcc += Math.Abs(mAccFull.Step(fullTiltPad, 0.016).Wheel);
        }
        Assert.True(midAcc < midLin, $"Expected fine mid-tilt scroll ({midAcc}) < linear ({midLin}).");
        Assert.Equal(fullLin, fullAcc);
        Assert.True(fullAcc > midAcc);
    }

    [Fact]
    public void LightingBeatFlashBoostBrightensTransientPeaks()
    {
        var baseSpec = new LightingEffectSpec
        {
            Kind = LightingEffectKind.AudioReactive,
            AudioStyle = AudioReactiveStyle.BeatShift,
            Palette = ["#102080"],
            MinBrightness = 0x10,
            MaxBrightness = 0x90,
            BeatFlashBoost = false,
        };
        var boostedSpec = baseSpec with { BeatFlashBoost = true };

        var input = new LightingInputState(
            LeftMag: 0,
            RightMag: 0,
            LeftTrigger: 0,
            RightTrigger: 0,
            AnyButtonPressed: false,
            IdleSeconds: 0,
            AudioLevel: 0.35,
            AudioBass: 0.40,
            AudioMid: 0.25,
            AudioTreble: 0.25,
            AudioBeatPulse: 0.92,
            AudioTrebleTransient: 0.70,
            AudioVibe: AudioMusicVibe.ElectronicDrop);

        var normal = LightingEffectEngine.Evaluate(baseSpec, TimeSpan.FromSeconds(1.2), input);
        var boosted = LightingEffectEngine.Evaluate(boostedSpec, TimeSpan.FromSeconds(1.2), input);

        Assert.Equal(boostedSpec.MaxBrightness, boosted.Brightness);
        Assert.True(boosted.R + boosted.G + boosted.B > normal.R + normal.G + normal.B);
    }

    [Fact]
    public void HapticsWeaponRecoilPresetsAndStereoCuesStayBounded()
    {
        var audio = new AudioFrame(
            Level: 0.65f,
            Bass: 0.75f,
            Mid: 0.50f,
            Treble: 0.60f,
            BeatPulse: 0.80f,
            BeatCount: 4,
            StereoPan: 0.45f,
            LeftLevel: 0.35f,
            RightLevel: 0.78f,
            SpectralCentroid: 0.62f,
            Energy: 0.70f,
            TrebleTransient: 0.75f,
            VocalPresence: 0.05f,
            VocalWarmth: 0.15f,
            VocalAir: 0.10f,
            InstrumentEnergy: 0.40f,
            BassPunch: 0.80f,
            DynamicContrast: 0.80f,
            Vibe: AudioMusicVibe.RockMetal,
            Spectrum: new AudioSpectrum8(0.6f, 0.7f, 0.5f, 0.4f, 0.3f, 0.4f, 0.6f, 0.5f),
            SpectralFlux: 0.70f,
            RawRms: 0.42f,
            RawPeakSpike: 0.55f);
        var input = new LivePadInput(0.1, 0.0, 0.4, 0.0, 0.0, 0.85, 0, false, false, false, PadActionButtons.None, true);

        foreach (var preset in Enum.GetValues<WeaponRecoilPreset>())
        {
            var engine = new HapticsFusionEngine();
            var tuning = new HapticsTuning
            {
                MasterIntensity = 85,
                ImpactPunch = 90,
                TriggerBalance = 90,
                RecoilPreset = preset,
                AccessibilityStereoCues = true,
            };
            var eval = engine.Evaluate(audio, input, HapticsGenreProfile.FpsAction, tuning);
            Assert.InRange(eval.Motors.LeftGrip, 0.0, 1.0);
            Assert.InRange(eval.Motors.RightGrip, 0.0, 1.0);
            Assert.InRange(eval.Motors.LeftTrigger, 0.0, 1.0);
            Assert.InRange(eval.Motors.RightTrigger, 0.0, 1.0);
        }
    }

    [Fact]
    public void ScreenTemplatesEncodeCleanlyWithinAssetLimit()
    {
        foreach (var kind in Enum.GetValues<ScreenTemplateKind>())
        {
            var frames = ScreenTemplates.Generate(kind, "PLAYER 1", 0x3D8BFF, 12);
            Assert.Equal(12, frames.Length);
            Assert.All(frames, f => Assert.Equal(160 * 80 * 4, f.Length));

            var encoded = ScreenCodec.EncodeAnimationFitting(frames, 120, out int usedFrames, out int usedDelay);
            Assert.InRange(usedFrames, 1, 12);
            Assert.True(usedDelay >= 120);
            Assert.True(encoded.Length <= ScreenCodec.AssetLimit);
        }
    }
}
