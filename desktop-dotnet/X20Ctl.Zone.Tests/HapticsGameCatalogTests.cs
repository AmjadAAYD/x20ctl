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
}
