using X20Ctl.Product;

namespace X20Ctl.Zone.Tests;

public class IllustratedThemeTests
{
    [Fact] public void EveryThemeHasEighteenDistinctWalkthroughAssets()
    {
        Assert.Equal(5, IllustratedThemeSpec.All.Count);
        Assert.Equal(5, IllustratedThemeSpec.All.Select(t => t.Id).Distinct().Count());
        foreach (var theme in IllustratedThemeSpec.All)
        {
            var paths = IllustratedThemeSpec.Pages.Select(p => theme.Wallpaper(p.Name)).ToArray();
            Assert.Equal(18, paths.Length); Assert.Equal(18, paths.Distinct().Count());
            Assert.All(paths, path => Assert.DoesNotContain("..", path));
            Assert.EndsWith("05-buttons.jpg", theme.Wallpaper("Buttons"));
            Assert.EndsWith("09-pc-mode.jpg", theme.Wallpaper("PcMode"));
            Assert.EndsWith("18-agent.jpg", theme.Wallpaper("Agent XCTL"));
        }
    }
    [Fact] public void UnrecognizedPagesAndThemesDoNotSilentlyReuseArtwork()
    {
        Assert.Null(IllustratedThemeSpec.Find("default"));
        Assert.Null(IllustratedThemeSpec.Find("nexus"));
        Assert.Null(IllustratedThemeSpec.Find("not-a-theme"));
        Assert.Throws<ArgumentException>(() => IllustratedThemeSpec.All[0].Wallpaper("not-a-page"));
    }
    [Fact] public void CapabilityTabsKeepTheirWalkthroughPositions()
    {
        var theme=IllustratedThemeSpec.All[0];
        Assert.EndsWith("10-rgb.jpg",theme.Wallpaper("RGB"));
        Assert.EndsWith("11-screen.jpg",theme.Wallpaper("Screen"));
        Assert.EndsWith("15-diagnostics.jpg",theme.Wallpaper("Diagnostics"));
        Assert.EndsWith("16-tester.jpg",theme.Wallpaper("Tester"));
        Assert.Equal(theme.Wallpaper("Tools"),theme.Wallpaper("Themes"));
    }
}
