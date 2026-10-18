using System.Text.RegularExpressions;
namespace X20Ctl.Zone;

/// <summary>
/// X20CTL's version names (owner decision, 18 Oct 2026): V&lt;super&gt;.&lt;release&gt;[.&lt;fix&gt;], the release number always three digits.
/// V0.016 is the 16th release before the first super release (every controller supported), V0.016.1 its first bug fix, V1.000 the super release.
/// New releases are tagged in that form (v0.017, v0.017.1, v1.000), which can never collide with an old tag.
/// Releases published before the change keep their old tags (immutable on GitHub); <see cref="Legacy"/> names each of them. Anything in the old
/// numbering is older than anything in the new one.
/// </summary>
public static class ReleaseVersions
{
    /// <summary>Every tag published under the old numbering, oldest first, with its name in the new one. RELEASES/versions.json holds the same list
    /// for the release tooling; a test keeps the two equal. Tags that never got a release (v1.0.0, v4.1.0-preview.1) are listed so dev builds read right.</summary>
    public static readonly IReadOnlyList<(string Tag, string Name)> Legacy =
    [
        ("v0.1.0", "V0.001"), ("v0.2.0", "V0.002"), ("v0.2.1", "V0.002.1"),
        ("v1.0.0", "V0.003"), ("v1.0.1", "V0.003"), ("v1.1.0", "V0.004"), ("v1.1.1", "V0.004.1"), ("v1.1.2", "V0.004.2"), ("v1.2.0", "V0.005"),
        ("v2.0.0", "V0.006"), ("v2.0.1", "V0.006.1"), ("v3.0.0", "V0.007"), ("v3.1.0", "V0.008"), ("v4.0.0", "V0.009"), ("v4.0.1", "V0.009.1"),
        ("v4.1.0-preview.1", "V0.010"), ("v4.1.0-preview.1-exe", "V0.010"), ("v4.1.0-preview.2", "V0.011"), ("v4.1.0-preview.3", "V0.012"), ("v4.1.0-preview.3-exe", "V0.012"),
        ("v4.1.0-preview.4", "V0.013"), ("v4.1.0-preview.5", "V0.014"), ("v4.1.0-preview.6", "V0.015"),
        // the bridge: the last tag in the old numbering, so copies that only know the old numbering still update to it
        ("v4.1.0-preview.7", "V0.016"),
    ];

    static readonly Regex NewForm = new(@"^[vV]?(\d+)\.(\d{3})(?:\.(\d+))?$", RegexOptions.Compiled);

    /// <summary>A version or tag in the new numbering: 0.016, v0.016.1, V1.000.</summary>
    public static bool IsNew(string version) => NewForm.IsMatch(version.Trim());

    /// <summary>What people see: V0.016 or V0.016.1 for the new numbering, the mapped name for an old release, and the string unchanged for anything else (a dev build).</summary>
    public static string Display(string version)
    {
        string v = version.Trim().Split('+')[0];
        if (NewForm.Match(v) is { Success: true } m) return $"V{int.Parse(m.Groups[1].Value)}.{m.Groups[2].Value}" + (m.Groups[3].Success && int.Parse(m.Groups[3].Value) > 0 ? "." + int.Parse(m.Groups[3].Value) : "");
        string tag = "v" + v.TrimStart('v', 'V');
        foreach (var (t, name) in Legacy) if (t == tag || t == tag + "-exe") return name;
        return v;
    }

    /// <summary>The numeric form .NET needs for AssemblyVersion and the Windows file version: 0.016.1 becomes 0.16.1.</summary>
    public static string Numeric(string version)
    {
        var m = NewForm.Match(version.Trim());
        if (!m.Success) throw new ArgumentException(version + " is not in the V0.016 form.");
        return $"{int.Parse(m.Groups[1].Value)}.{int.Parse(m.Groups[2].Value)}.{(m.Groups[3].Success ? int.Parse(m.Groups[3].Value) : 0)}";
    }

    /// <summary>Orders two versions: the new numbering above all of the old; within the new, super, release, fix; within the old, the old rules
    /// (numbers, then a release above its pre-releases, then pre-release parts). Tags may be passed with their leading v.</summary>
    public static int Compare(string a, string b)
    {
        a = a.Trim().TrimStart('v', 'V').Split('+')[0]; b = b.Trim().TrimStart('v', 'V').Split('+')[0];
        var ma = NewForm.Match(a); var mb = NewForm.Match(b);
        if (ma.Success != mb.Success) return ma.Success ? 1 : -1;
        if (ma.Success)
        {
            int[] Parts(Match m) => [int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), m.Groups[3].Success ? int.Parse(m.Groups[3].Value) : 0];
            var pa = Parts(ma); var pb = Parts(mb);
            for (int i = 0; i < 3; i++) if (pa[i] != pb[i]) return pa[i].CompareTo(pb[i]);
            return 0;
        }
        return CompareOld(a, b);
    }

    /// <summary>The old numbering's rules, as the app used them up to V0.016.</summary>
    static int CompareOld(string a, string b)
    {
        static (int[] Core, string[] Pre) Split(string v)
        {
            v = Regex.Replace(v, "-exe$", ""); int dash = v.IndexOf('-');
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
}
