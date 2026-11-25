using X20Ctl.Zone;
using X20Ctl.Zone.Diagnostics;
using X20Ctl.Zone.Lighting;
using X20Ctl.Zone.PcMode;

namespace X20Ctl.Zone.Tests;

public class Phase4ToolsTests
{
    [Fact]
    public void ShareBundleRoundTripsFullSetupAndRejectsCorruptCodes()
    {
        var bundle = new FullSetupBundle
        {
            Label = "Ranked FPS",
            PcMode = new PcModeProfile
            {
                DPadSocd = SocdMode.LastInput,
                OutputRateHz = 1000,
                Remap = { ["A"] = "RB" },
            },
            Lighting = new LightingEffectSpec
            {
                Kind = LightingEffectKind.SmoothHueWave,
                Palette = ["#00F0FF", "#FF00AA"],
                MaxBrightness = 0xE0,
            },
            HapticsGenre = HapticsGenreProfile.FpsAction,
            Haptics = new HapticsTuning
            {
                MasterIntensity = 88,
                RecoilPreset = WeaponRecoilPreset.Sniper,
                AccessibilityStereoCues = true,
            },
        };

        string code = ShareBundle.ToCode(bundle);
        Assert.StartsWith(ShareBundle.CodePrefix, code);

        var decoded = ShareBundle.FromCode(code);
        Assert.Equal("Ranked FPS", decoded.Label);
        Assert.Equal(SocdMode.LastInput, decoded.PcMode!.DPadSocd);
        Assert.Equal(1000, decoded.PcMode.OutputRateHz);
        Assert.Equal("RB", decoded.PcMode.Remap["A"]);
        Assert.Equal(LightingEffectKind.SmoothHueWave, decoded.Lighting!.Kind);
        Assert.Equal(HapticsGenreProfile.FpsAction, decoded.HapticsGenre);
        Assert.Equal(WeaponRecoilPreset.Sniper, decoded.Haptics!.RecoilPreset);
        Assert.True(decoded.Haptics.AccessibilityStereoCues);

        Assert.Throws<InvalidDataException>(() => ShareBundle.FromCode("X20PC1.notabundle"));
        Assert.Throws<InvalidDataException>(() => ShareBundle.FromCode(ShareBundle.CodePrefix + "%%%bad%%%"));
    }

    [Fact]
    public void HapticsShareRoundTripsJsonAndCodeSafely()
    {
        var payload = new HapticsSharePayload
        {
            Name = "Tactical Footsteps",
            GameExe = "valorant",
            Genre = HapticsGenreProfile.FpsAction,
            Tuning = new HapticsTuning { MasterIntensity = 80, ImpactPunch = 95, RecoilPreset = WeaponRecoilPreset.Pistol, AccessibilityStereoCues = true },
            AppCalibration = new HapticsAppTuning { SurfaceGain = 125, CombatGain = 90 },
        };

        string json = HapticsShare.ToJson(payload);
        var fromJson = HapticsShare.FromJson(json);
        Assert.Equal("valorant.exe", fromJson.GameExe);
        Assert.Equal(WeaponRecoilPreset.Pistol, fromJson.Tuning.RecoilPreset);
        Assert.Equal(125, fromJson.AppCalibration!.SurfaceGain);

        string code = HapticsShare.ToCode(payload);
        Assert.StartsWith(HapticsShare.CodePrefix, code);
        var fromCode = HapticsShare.FromCode(code);
        Assert.Equal("Tactical Footsteps", fromCode.Name);

        Assert.Throws<InvalidDataException>(() => HapticsShare.FromJson("{not-valid}"));
        Assert.Throws<InvalidDataException>(() => HapticsShare.FromCode("BAD.123"));
    }

    [Fact]
    public void ProfileBackupRingMaintainsMax10SnapshotsAndRestores()
    {
        string dir = Path.Combine(Path.GetTempPath(), "x20ctl-bak-" + Guid.NewGuid().ToString("N"));
        try
        {
            var ring = new ProfileBackupRing(dir);
            var t0 = new DateTimeOffset(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);
            for (int i = 0; i < 12; i++)
                ring.CreateSnapshot($"Auto #{i}", $"{{\"step\":{i}}}", null, null, t0.AddSeconds(i));

            var list = ring.ListSnapshots();
            Assert.Equal(ProfileBackupRing.MaxSnapshots, list.Count);
            Assert.Equal("Auto #11", list[0].Reason);
            Assert.Equal("Auto #2", list[^1].Reason);

            var snap = ring.ReadSnapshot(list[0].Id);
            Assert.NotNull(snap);
            Assert.Equal("{\"step\":11}", snap!.PcModeJson);

            Assert.True(ring.DeleteSnapshot(list[^1].Id));
            Assert.Equal(ProfileBackupRing.MaxSnapshots - 1, ring.ListSnapshots().Count);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void SessionInputTrackerCountsEdgesApmDistanceAndHeatmap()
    {
        var tracker = new SessionInputTracker();
        tracker.Sample(new PadState(0, 0, 0, 0, 0, 0, 0), 1000);
        tracker.Sample(new PadState(PadButtons.A | PadButtons.X, 0, 0, 32767, 0, 0, -32767), 1500);
        tracker.Sample(new PadState(PadButtons.A, 0, 0, 32767, 0, 0, 0), 2000);

        Assert.Equal(2, tracker.TotalButtonPresses);
        Assert.Equal(1, tracker.ButtonCounts["A"]);
        Assert.Equal(1, tracker.ButtonCounts["X"]);
        Assert.Equal(2, tracker.ActionsPerMinute(5000));
        Assert.True(tracker.LeftStickDistance >= 0.99);
        Assert.True(tracker.RightStickDistance >= 0.99);

        var leftMap = tracker.CopyLeftHeatmap();
        int totalLeftSamples = leftMap.Cast<int>().Sum();
        Assert.Equal(3, totalLeftSamples);

        tracker.Reset();
        Assert.Equal(0, tracker.TotalButtonPresses);
        Assert.Equal(0.0, tracker.LeftStickDistance);
    }

    [Fact]
    public void AimTrainerSessionTracksHitsMissesAndExpiryDeterministically()
    {
        var aim = new AimTrainerSession(seed: 42);
        aim.Start(durationSec: 10, nowMs: 1000);
        Assert.True(aim.Running);

        // Fire away from target -> 1 miss
        aim.Step(0, 0, true, 0.016, 1100);
        aim.Step(0, 0, false, 0.016, 1120);
        Assert.Equal(0, aim.Hits);
        Assert.Equal(1, aim.Misses);

        // Steer cursor onto TargetX/TargetY
        for (int i = 0; i < 80; i++)
        {
            double dx = aim.TargetX - aim.CursorX;
            double dy = aim.TargetY - aim.CursorY;
            if (dx * dx + dy * dy < 0.005) break;
            aim.Step(Math.Sign(dx), Math.Sign(dy), false, 0.02, 1200 + i * 20);
        }
        bool scored = aim.Step(0, 0, true, 0.02, 2000);
        Assert.True(scored);
        Assert.Equal(1, aim.Hits);
        Assert.True(aim.AvgReactionMs > 0);

        // Advance past remaining timer using valid per-frame dt steps -> stops cleanly
        for (int i = 0; i < 50 && aim.Running; i++)
            aim.Step(0, 0, false, 0.25, 3000 + i * 250);
        Assert.False(aim.Running);
        Assert.Equal(0.0, aim.TimeRemainingSec);
    }

    [Fact]
    public void HardwareQuirksCatalogCoversSupportedHardwareWithoutEmDashes()
    {
        Assert.True(HardwareQuirksCatalog.Entries.Count >= 5);
        Assert.Contains(HardwareQuirksCatalog.Entries, e => e.Model.Contains("X20 Pro", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(HardwareQuirksCatalog.Entries, e => e.Model.Contains("X20", StringComparison.OrdinalIgnoreCase));
        foreach (var entry in HardwareQuirksCatalog.Entries)
        {
            Assert.DoesNotContain('\u2014', entry.Model);
            Assert.DoesNotContain('\u2014', entry.Transport);
            Assert.DoesNotContain('\u2014', entry.Summary);
            Assert.DoesNotContain('\u2014', entry.Recommendation);
        }
    }
}
