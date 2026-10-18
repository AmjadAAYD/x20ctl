using System.Text.Json;
using X20Ctl.Zone;
using Xunit;

namespace X20Ctl.Zone.Tests;

public class ReleaseVersionsTests
{
    static string Repo() { var d = new DirectoryInfo(AppContext.BaseDirectory); while (d != null && !File.Exists(Path.Combine(d.FullName, "README.md"))) d = d.Parent; return d!.FullName; }

    /// <summary>Updater.Compare exactly as it shipped up to 4.1.0-preview.6: what every copy already out there uses to decide whether to update.</summary>
    static int ShippedCompare(string a, string b)
    {
        static (int[] Core, string[] Pre) Split(string v)
        {
            v = v.Split('+')[0]; int dash = v.IndexOf('-');
            string core = dash < 0 ? v : v[..dash]; string[] pre = dash < 0 ? [] : v[(dash + 1)..].Split('.');
            return (core.Split('.').Select(p => int.TryParse(p, out int n) ? n : 0).Concat([0, 0, 0]).Take(3).ToArray(), pre);
        }
        var (ca, pa) = Split(a); var (cb, pb) = Split(b);
        for (int i = 0; i < 3; i++) if (ca[i] != cb[i]) return ca[i].CompareTo(cb[i]);
        if (pa.Length == 0 || pb.Length == 0) return pb.Length.CompareTo(pa.Length);
        for (int i = 0; i < Math.Min(pa.Length, pb.Length); i++)
        {
            bool na = int.TryParse(pa[i], out int x), nb = int.TryParse(pb[i], out int y);
            int c = na && nb ? x.CompareTo(y) : na ? -1 : nb ? 1 : string.CompareOrdinal(pa[i], pb[i]);
            if (c != 0) return c;
        }
        return pa.Length.CompareTo(pb.Length);
    }

    [Theory]
    [InlineData("0.016", "V0.016")]
    [InlineData("v0.016", "V0.016")]
    [InlineData("0.016.0", "V0.016")]
    [InlineData("0.016.1", "V0.016.1")]
    [InlineData("v1.000", "V1.000")]
    [InlineData("0.017+abc123", "V0.017")]
    [InlineData("4.1.0-preview.6", "V0.015")]
    [InlineData("v4.1.0-preview.7", "V0.016")]
    [InlineData("4.1.0-preview.3", "V0.012")]
    [InlineData("4.0.1", "V0.009.1")]
    [InlineData("0.2.1", "V0.002.1")]
    [InlineData("native-preview", "native-preview")]
    public void Display_names_new_and_old_versions(string version, string shown) => Assert.Equal(shown, ReleaseVersions.Display(version));

    [Theory]
    [InlineData("0.016", "0.16.0")]
    [InlineData("v0.017.1", "0.17.1")]
    [InlineData("1.000", "1.0.0")]
    public void Numeric_is_what_dotnet_accepts(string version, string numeric)
    {
        Assert.Equal(numeric, ReleaseVersions.Numeric(version));
        Assert.True(Version.TryParse(numeric, out _));
    }

    [Fact]
    public void Numeric_refuses_the_old_numbering() => Assert.Throws<ArgumentException>(() => ReleaseVersions.Numeric("4.1.0-preview.7"));

    [Theory]
    [InlineData("0.017", "4.1.0-preview.7")]
    [InlineData("0.017", "4.1.0")]
    [InlineData("0.001", "99.0.0")]
    [InlineData("0.017.1", "0.017")]
    [InlineData("0.018", "0.017.9")]
    [InlineData("1.000", "0.999.3")]
    [InlineData("v0.017", "0.016")]
    [InlineData("4.1.0-preview.7", "4.1.0-preview.6")]
    [InlineData("4.1.0-preview.10", "4.1.0-preview.9")]
    [InlineData("4.1.0", "4.1.0-preview.9")]
    public void Newer_beats_older(string newer, string older)
    {
        Assert.True(ReleaseVersions.Compare(newer, older) > 0);
        Assert.True(ReleaseVersions.Compare(older, newer) < 0);
    }

    [Theory]
    [InlineData("0.017", "0.017.0")]
    [InlineData("v0.017", "0.017")]
    [InlineData("4.1.0-preview.3", "4.1.0-preview.3-exe")]
    public void Same_version_compares_equal(string a, string b) => Assert.Equal(0, ReleaseVersions.Compare(a, b));

    [Fact]
    public void The_new_code_orders_old_versions_as_the_shipped_code_did()
    {
        string[] old = ["0.1.0", "0.2.1", "1.0.1", "1.2.0", "2.0.1", "4.0.1", "4.1.0-preview.1", "4.1.0-preview.6", "4.1.0-preview.7", "4.1.0", "4.1.0-rc.1"];
        foreach (var a in old) foreach (var b in old)
            Assert.Equal(Math.Sign(ShippedCompare(a, b)), Math.Sign(ReleaseVersions.Compare(a, b)));
    }

    [Fact]
    public void Copies_already_out_there_see_the_bridge_as_an_update()
    {   // why V0.016 is tagged v4.1.0-preview.7: copies running preview.6 or earlier only know the old numbering
        foreach (var installed in new[] { "4.1.0-preview.6", "4.1.0-preview.5", "4.1.0-preview.4" })
            Assert.True(ShippedCompare("4.1.0-preview.7", installed) > 0);
        // and they would never see a v0.017 tag as newer, which is the reason for the bridge
        Assert.True(ShippedCompare("0.017", "4.1.0-preview.6") < 0);
    }

    [Fact]
    public void Old_tags_are_listed_oldest_first_and_their_names_agree()
    {
        var legacy = ReleaseVersions.Legacy;
        for (int i = 1; i < legacy.Count; i++)
        {
            string a = legacy[i - 1].Tag.TrimStart('v'), b = legacy[i].Tag.TrimStart('v');
            Assert.True(ReleaseVersions.Compare(a, b) <= 0, $"{legacy[i - 1].Tag} should not be above {legacy[i].Tag}");
            Assert.True(ReleaseVersions.Compare(legacy[i - 1].Name, legacy[i].Name) <= 0, $"{legacy[i - 1].Name} should not be above {legacy[i].Name}");
            Assert.True(ReleaseVersions.IsNew(legacy[i].Name));
        }
    }

    [Fact]
    public void The_release_tooling_uses_the_same_list()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(Repo(), "RELEASES", "versions.json")));
        var json = doc.RootElement.GetProperty("legacy").EnumerateArray().Select(e => (e.GetProperty("tag").GetString()!, e.GetProperty("name").GetString()!)).ToList();
        Assert.Equal(ReleaseVersions.Legacy, json);
    }
}
