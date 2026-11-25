using X20Ctl.Zone;
using X20Ctl.Zone.Capture;
using X20Ctl.Zone.Diagnostics;
using X20Ctl.Zone.PcMode;
using X20Ctl.Zone.Telemetry;

namespace X20Ctl.Zone.Tests;

public class DiagnosticsLabTests
{
    [Fact]
    public void PortableAppDataPathsSwitchesWhenMarkerPresent()
    {
        string temp = Path.Combine(Path.GetTempPath(), "x20ctl-portable-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            AppDataPaths.OverrideBaseDirectory = temp;
            Assert.False(AppDataPaths.IsPortable);

            File.WriteAllText(Path.Combine(temp, "portable.txt"), "1");
            Assert.True(AppDataPaths.IsPortable);
            Assert.Equal(Path.Combine(temp, "data", "roaming", "pc-mode.json"), PcModeStore.DefaultPath);
            Assert.Equal(Path.Combine(temp, "data", "local", "telemetry", "battery-log.json"), BatterySessionLog.DefaultPath);
        }
        finally
        {
            AppDataPaths.OverrideBaseDirectory = null;
            if (Directory.Exists(temp)) Directory.Delete(temp, true);
        }
    }

    [Fact]
    public void JitterStatsComputesDistributionSpikesAndPercentiles()
    {
        var jitter = new JitterStats();
        for (int i = 0; i < 40; i++) jitter.AddInterval(1.0);
        jitter.AddInterval(12.5);

        Assert.Equal(41, jitter.SampleCount);
        Assert.Equal(1.0, jitter.MinMs, 2);
        Assert.Equal(12.5, jitter.MaxMs, 2);
        Assert.Equal(12.5, jitter.P99Ms, 2);
        Assert.Equal(1, jitter.SpikeCount);
        Assert.True(jitter.StdDevMs > 1.0);
        Assert.Equal(40, jitter.Histogram[2]); // 1-1.5 ms bucket
        Assert.Equal(1, jitter.Histogram[9]);  // 12-16 ms bucket
    }

    [Fact]
    public void NoiseFloorIgnoresSweepsAndRecommendsSafeDeadzone()
    {
        var noise = new NoiseFloorStats();
        for (int i = 0; i < 30; i++)
            noise.Add((short)(i % 2 == 0 ? 900 : -900), 400); // ~3.0% radius
        noise.Add(30000, 0); // full tilt sweep must be excluded

        Assert.Equal(30, noise.SampleCount);
        Assert.InRange(noise.PeakRadiusPct, 2.9, 3.2);
        Assert.InRange(noise.RecommendedDeadZonePct, 3.5, 4.0);
    }

    [Fact]
    public void StickCompassAndTriggerLinearityTrackTravel()
    {
        var stick = new StickStats();
        stick.Add(32767, 0);
        stick.Add(0, 32767);
        stick.Add(-32767, 0);
        stick.Add(0, -32767);
        var compass = stick.CompassReach();
        Assert.Equal(8, compass.Length);
        Assert.True(compass[0] >= 99.0); // East
        Assert.True(compass[2] >= 99.0); // North

        var trig = new TriggerLinearityStats();
        trig.Add(0);
        trig.Add(15);
        trig.Add(128);
        trig.Add(255);
        Assert.InRange(trig.StartDeadBandPct, 5.0, 6.5);
        Assert.Equal(0, trig.EndDeadBandPct, 2);
        Assert.Equal(30.0, trig.CoveragePct);
    }

    [Fact]
    public void ControllerHealthScorePenalizesChatterAndNoise()
    {
        var clean = ControllerHealthScore.Evaluate(4.2, 5.0, 1.1, 1.4, 0, 0.4, 4);
        Assert.Equal(100, clean.Score);
        Assert.Equal("Excellent", clean.Grade);

        var worn = ControllerHealthScore.Evaluate(15.0, 12.0, 6.0, 2.0, 3, 4.0, 1);
        Assert.True(worn.Score < 75);
        Assert.Contains(worn.Factors, f => f.Contains("Battery low"));
    }

    [Fact]
    public void TransportBenchmarkStorePersistsSnapshots()
    {
        string path = Path.Combine(Path.GetTempPath(), $"bench-{Guid.NewGuid():N}.json");
        try
        {
            var store = new TransportBenchmarkStore(path);
            store.Add(new("b1", DateTimeOffset.UtcNow, "Wired", "X20 Pro", 1000, 1005, 1.0, 1.4, 0.2, 4.1, 4.3, 1.1, 1.2, 99));
            store.Add(new("b2", DateTimeOffset.UtcNow, "Bluetooth", "X20 Pro", 125, 140, 8.0, 14.5, 2.1, 4.2, 4.5, 1.1, 1.2, 96));

            var reloaded = new TransportBenchmarkStore(path);
            Assert.Equal(2, reloaded.Items.Count);
            Assert.Equal("Bluetooth", reloaded.Items[0].TransportLabel);
            Assert.Equal("Wired", reloaded.Items[1].TransportLabel);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void OnboardSnapshotDiffDetectsChangedBytesAndPcDifferences()
    {
        var remapA = ControllerRecords.BuildRemap(new Dictionary<string, string>());
        var remapB = ControllerRecords.BuildRemap(new Dictionary<string, string> { ["A"] = "B" });
        byte[] sticks = new byte[15]; sticks[0] = 14; sticks[3] = 85; sticks[4] = 85; sticks[5] = 170; sticks[6] = 170;
        sticks[10] = 85; sticks[11] = 85; sticks[12] = 170; sticks[13] = 170;
        var led = new byte[24]; led[0] = 0x3D; led[1] = 0x8B; led[2] = 0xFF; led[5] = 0x80; led[6] = 2;

        var snapA = new OnboardHardwareSnapshot(DateTimeOffset.UtcNow, "x20_pro", "07102020060144", Convert.ToHexString(remapA), Convert.ToHexString(sticks), "0E", ["00", "00", "00", "00", "00", "00"], Convert.ToHexString(led), 18.2);
        var snapB = snapA with { RemapHex = Convert.ToHexString(remapB) };

        var diff = OnboardSnapshotDiff.Diff(snapA, snapB);
        Assert.Single(diff);
        Assert.Equal("0xB6 Remap", diff[0].BlockName);

        var summary = OnboardSnapshotDiff.DecodeSummary(snapB);
        Assert.Contains(summary, s => s.Contains("A->B"));
        Assert.Contains(summary, s => s.Contains("#3D8BFF"));

        var pc = new PcModeProfile();
        var cmp = OnboardSnapshotDiff.CompareWithPcMode(snapB, pc);
        Assert.Contains(cmp, l => l.Contains("Remap differs"));
    }

    [Fact]
    public void BatterySessionLogTracksDrainAndPowerFeatures()
    {
        string path = Path.Combine(Path.GetTempPath(), $"bat-{Guid.NewGuid():N}.json");
        try
        {
            var log = new BatterySessionLog(path);
            var t0 = DateTimeOffset.UtcNow.AddHours(-2);
            log.Append(new(t0, "x20_pro", 4, 100, false, false, 80, true, "2.4 GHz receiver"));
            log.Append(new(t0.AddHours(1), "x20_pro", 3, 75, false, true, 0, false, "2.4 GHz receiver"));
            log.Append(new(t0.AddHours(2), "x20_pro", 2, 50, false, true, 0, false, "2.4 GHz receiver"));

            var summary = log.Summarize();
            Assert.Equal(3, summary.SampleCount);
            Assert.InRange(summary.SessionMinutes, 119, 121);
            Assert.InRange(summary.EcoSharePct, 65, 68);
            Assert.Equal(25.0, summary.EstimatedDrainPctPerHour);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
