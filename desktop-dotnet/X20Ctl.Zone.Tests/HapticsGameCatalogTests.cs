using X20Ctl.Zone.Lighting;
using Xunit;

namespace X20Ctl.Zone.Tests;

public sealed class HapticsGameCatalogTests
{
    [Fact]
    public void Catalog_ResolvesPopularGamesBrowsersAndIgnoredUtilities()
    {
        var fh5 = HapticsGameCatalog.Resolve(@"C:\Games\ForzaHorizon5.exe", "Forza Horizon 5");
        Assert.Equal(HapticsGenreProfile.Racing, fh5.Profile);
        Assert.False(fh5.IsIgnored);

        var cs2 = HapticsGameCatalog.Resolve("cs2.exe");
        Assert.Equal(HapticsGenreProfile.FpsAction, cs2.Profile);

        var er = HapticsGameCatalog.Resolve("eldenring.exe");
        Assert.Equal(HapticsGenreProfile.RpgMelee, er.Profile);

        var yt = HapticsGameCatalog.Resolve("chrome.exe", "Daft Punk - Discovery - YouTube");
        Assert.Equal(HapticsGenreProfile.Music, yt.Profile);
        Assert.True(yt.IsMediaApp);

        var nf = HapticsGameCatalog.Resolve("msedge.exe", "Cyberpunk Edgerunners - Netflix");
        Assert.Equal(HapticsGenreProfile.Cinema, nf.Profile);

        var code = HapticsGameCatalog.Resolve("Code.exe", "x20ctl");
        Assert.Null(code.Profile);
        Assert.True(code.IsIgnored);

        Assert.True(HapticsGameCatalog.Resolve("WINWORD.EXE").IsIgnored);
        Assert.True(HapticsGameCatalog.Resolve("Telegram.exe").IsIgnored);
        Assert.Equal(HapticsGenreProfile.RpgMelee, HapticsGameCatalog.Resolve("LiesOfP-Win64-Shipping.exe").Profile);
        Assert.Equal(HapticsGenreProfile.Racing, HapticsGameCatalog.Resolve("F1_25.exe").Profile);
        Assert.Equal(HapticsGenreProfile.SportsArcade, HapticsGameCatalog.Resolve("silksong.exe").Profile);

        var unknown = HapticsGameCatalog.Resolve("MyCustomIndieRacer2026.exe", "Indie Game");
        Assert.Null(unknown.Profile);
        Assert.False(unknown.IsIgnored);
    }

    [Fact]
    public void ProfileStore_PersistsBindingsIgnoresAndPerGenreTuning()
    {
        string tmpDir = Path.Combine(Path.GetTempPath(), "x20ctl-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmpDir);
        string path = Path.Combine(tmpDir, "haptics-profiles.json");
        try
        {
            var store = new HapticsProfileStore(path);
            store.BindApp("MyCustomShooter.exe", HapticsGenreProfile.FpsAction);
            store.IgnoreApp("Discord.exe");
            store.SetTuning(HapticsGenreProfile.Racing, new HapticsTuning
            {
                MasterIntensity = 62,
                TextureDetail = 40,
                ImpactPunch = 78,
                TriggerBalance = 91,
                ComfortTier = HapticsComfortTier.Balanced,
                BlendMode = NativeRumbleBlendMode.TriggersOnly,
            });

            var reloaded = new HapticsProfileStore(path);
            reloaded.Load();

            var (prof, unknown, _) = reloaded.ResolveActive("MyCustomShooter.exe");
            Assert.Equal(HapticsGenreProfile.FpsAction, prof);
            Assert.False(unknown);

            var (_, unknownNew, _) = reloaded.ResolveActive("BrandNewUnknownGame.exe");
            Assert.True(unknownNew);
            Assert.True(reloaded.IsAppIgnored("Discord.exe"));
            Assert.False(reloaded.IsAppIgnored("MyCustomShooter.exe"));

            var tuning = reloaded.GetTuning(HapticsGenreProfile.Racing);
            Assert.Equal(62, tuning.MasterIntensity);
            Assert.Equal(HapticsComfortTier.Balanced, tuning.ComfortTier);
            Assert.Equal(NativeRumbleBlendMode.TriggersOnly, tuning.BlendMode);
        }
        finally
        {
            if (Directory.Exists(tmpDir))
                Directory.Delete(tmpDir, recursive: true);
        }
    }

    [Fact]
    public void HapticsProfileStore_Load_AlwaysStartsDisabledEvenIfSavedEnabled()
    {
        string tmpDir = Path.Combine(Path.GetTempPath(), "x20ctl-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmpDir);
        string path = Path.Combine(tmpDir, "haptics-profiles.json");
        try
        {
            var store = new HapticsProfileStore(path);
            store.UpdateSettings(c =>
            {
                c.Enabled = true;
                c.ManualProfile = HapticsGenreProfile.Music;
                c.ComfortTier = HapticsComfortTier.Balanced;
            }, persist: true);

            var reloaded = new HapticsProfileStore(path);
            reloaded.Load();
            var snap = reloaded.Snapshot();

            Assert.False(snap.Enabled);
            Assert.Equal(HapticsGenreProfile.Music, snap.ManualProfile);
            Assert.Equal(HapticsComfortTier.Balanced, snap.ComfortTier);
        }
        finally
        {
            if (Directory.Exists(tmpDir))
                Directory.Delete(tmpDir, recursive: true);
        }
    }

    [Fact]
    public void HapticsPowerSaver_AppliesHalfScaleStrictCapsFloorRateAndIdleCutoff()
    {
        var input = new HapticsFrame(LeftGrip: 0.90, RightGrip: 0.80, LeftTrigger: 1.00, RightTrigger: 0.10);

        var normal = HapticsPowerSaver.Apply(input, powerSavingActive: false);
        Assert.Equal(input, normal);
        Assert.Equal(20, HapticsPowerSaver.TickIntervalMs(false));
        Assert.Equal(4.5, HapticsPowerSaver.IdleCutoffSeconds(false));

        var eco = HapticsPowerSaver.Apply(input, powerSavingActive: true);
        Assert.Equal(40, HapticsPowerSaver.TickIntervalMs(true));
        Assert.Equal(1.5, HapticsPowerSaver.IdleCutoffSeconds(true));
        Assert.Equal(0.35, eco.LeftGrip, 3);     // min(0.45, 0.35) = 0.35
        Assert.Equal(0.40, eco.RightGrip, 3);    // min(0.40, 0.48) = 0.40
        Assert.Equal(0.45, eco.LeftTrigger, 3);  // min(0.50, 0.45) = 0.45
        Assert.Equal(0.0, eco.RightTrigger, 3);  // 0.10 * 0.50 = 0.05 < 0.06 -> gated to 0
    }

    [Fact]
    public void HapticsPowerSaver_LowBatteryTextMatchesExpectedStringsAndPercents()
    {
        Assert.True(HapticsPowerSaver.IsLowBatteryText("low"));
        Assert.True(HapticsPowerSaver.IsLowBatteryText("EMPTY"));
        Assert.True(HapticsPowerSaver.IsLowBatteryText("1/4 (20%)"));
        Assert.True(HapticsPowerSaver.IsLowBatteryText(null, percent: 20));
        Assert.False(HapticsPowerSaver.IsLowBatteryText("3/4", percent: 75));
        Assert.False(HapticsPowerSaver.IsLowBatteryText(null));
    }

    [Fact]
    public void HapticsTuning_ForMusicMood_ReturnsDistinctChillVocalsAndClubPresets()
    {
        var chill = HapticsTuning.ForMusicMood(HapticsMusicMood.Chill);
        var vocals = HapticsTuning.ForMusicMood(HapticsMusicMood.Vocals);
        var club = HapticsTuning.ForMusicMood(HapticsMusicMood.Club);

        Assert.Equal(HapticsComfortTier.Feather, chill.ComfortTier);
        Assert.Equal(HapticsComfortTier.Balanced, vocals.ComfortTier);
        Assert.Equal(HapticsComfortTier.FullPunch, club.ComfortTier);
        Assert.True(vocals.VocalFocus > chill.VocalFocus);
        Assert.True(club.ImpactPunch > vocals.ImpactPunch);
    }

    [Fact]
    public void HapticsProfileStore_PersistsMoodEcoAndPerAppCalibration()
    {
        string tmpDir = Path.Combine(Path.GetTempPath(), "x20ctl-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmpDir);
        string path = Path.Combine(tmpDir, "haptics-profiles.json");
        try
        {
            var store = new HapticsProfileStore(path);
            store.UpdateSettings(c =>
            {
                c.MusicMood = HapticsMusicMood.Club;
                c.PowerSavingMode = true;
                c.AutoPowerSaverOnLowBattery = false;
            }, persist: true);
            store.SetAppTuning("eldenring.exe", new HapticsAppTuning { SurfaceGain = 40, CombatGain = 135 }, persist: true);

            var reloaded = new HapticsProfileStore(path);
            reloaded.Load();
            var snap = reloaded.Snapshot();

            Assert.Equal(HapticsMusicMood.Club, snap.MusicMood);
            Assert.True(snap.PowerSavingMode);
            Assert.False(snap.AutoPowerSaverOnLowBattery);
            Assert.True(reloaded.HasCustomAppTuning("eldenring.exe"));

            var baseTuning = new HapticsTuning { TextureDetail = 50, ImpactPunch = 60 };
            var adjusted = reloaded.ApplyAppTuning(baseTuning, "eldenring.exe");
            Assert.Equal(20, adjusted.TextureDetail); // 50 * 0.40
            Assert.Equal(81, adjusted.ImpactPunch);   // 60 * 1.35

            reloaded.ResetAppTuning("eldenring.exe", persist: true);
            Assert.False(reloaded.HasCustomAppTuning("eldenring.exe"));
        }
        finally
        {
            if (Directory.Exists(tmpDir))
                Directory.Delete(tmpDir, recursive: true);
        }
    }
}
