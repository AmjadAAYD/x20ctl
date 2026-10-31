using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using X20Ctl.Zone;
namespace X20Ctl.Product;

/// <summary>One look for the whole app: its colours, type and corners, and its own opening film. The pages keep their
/// layout; the shared resources they are drawn from take the theme's values.</summary>
public sealed record AppTheme(string Id, string Name, string Line,
    Color Void, Color Deep, Color Night, Color Accent, Color AccentHi, Color OnAccent,
    Color Text, Color Soft, Color Muted, Color Rule, Color PoolA, Color PoolB,
    string Display, string Body, double Radius)
{
    /// <summary>The theme's opening film and the logo laid over its last frame, beside the app (Brand/themes/id).</summary>
    public IntroSpec? Intro => ThemeCatalog.Intros.TryGetValue(Id, out var spec) ? spec : null;
}

/// <summary>A themed opening: a silent 1920x1080 film that ends on its logo settled, and that logo as an image the intro
/// lays over the last frame and flies to the header. Times in seconds; boxes in film pixels and image pixels.</summary>
public sealed record IntroSpec(string Folder, double FilmEnd, double CoverAt, double FlyAt, Rect FilmLogo, Size Image, Rect Ink)
{
    public string FilmPath => Path.Combine(AppContext.BaseDirectory, "Brand", "themes", Folder, "intro.mp4");
    public string LogoPath => Path.Combine(AppContext.BaseDirectory, "Brand", "themes", Folder, "logo.png");
}

public static class ThemeCatalog
{
    private static Color C(uint rgb) => Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);

    public static readonly AppTheme Default = new("default", "Default", "Space glass, blue",
        C(0x04070F), C(0x070C1B), C(0x0B1430), C(0x3D8BFF), C(0x7DB6FF), C(0xFFFFFF),
        C(0xF4F7FD), C(0xB4C0D8), C(0x76839F), C(0x26375A), C(0x2453C2), C(0x143587),
        "Segoe UI Variable Display, Segoe UI", "Segoe UI Variable Text, Segoe UI", 16);

    public static readonly IReadOnlyList<AppTheme> All = new[]
    {
        Default,
        new AppTheme("pulse", "Pulse", "Harmonic chamber, cyan & prism",
            C(0x03050A), C(0x070D19), C(0x0E1B32), C(0x56B8FF), C(0xA8E0FF), C(0x060E1C),
            C(0xF2F6FC), C(0xC8D8EC), C(0x88A0C0), C(0x2E4870), C(0x56B8FF), C(0xFF78B4),
            "Segoe UI Variable Display, Segoe UI", "Segoe UI Variable Text, Segoe UI", 22),
        new AppTheme("spotlight", "Spotlight", "Velvet, brass and a follow spot",
            C(0x070405), C(0x120908), C(0x1C0F0D), C(0xE8A990), C(0xF5CDB8), C(0x1A0C08),
            C(0xEFE6D8), C(0xC4B6A2), C(0x9B8C7A), C(0x5A4630), C(0x86202E), C(0x6A4A2A),
            "Garamond, Georgia", "Garamond, Georgia", 4),
        new AppTheme("nexus", "Nexus", "Graphite tiles, green light",
            C(0x101113), C(0x17191C), C(0x1D1F23), C(0x6CC644), C(0x9BE07A), C(0x101113),
            C(0xFFFFFF), C(0xC8CACD), C(0x8F9296), C(0x3A3D44), C(0x107C10), C(0x2A2D33),
            "Segoe UI Variable Display, Segoe UI", "Segoe UI Variable Text, Segoe UI", 8),
        new AppTheme("rebel", "Rebel", "Black, bone and red, cut sharp",
            C(0x0B0A0C), C(0x121012), C(0x1A1618), C(0xE3122D), C(0xFF4A5E), C(0xFFFFFF),
            C(0xF7F3EE), C(0xCFC7C9), C(0xA39B9E), C(0x4A4043), C(0x8E0A1C), C(0x2A1418),
            "Franklin Gothic Heavy, Franklin Gothic Medium, Bahnschrift", "Bahnschrift, Segoe UI", 0),
        new AppTheme("tide", "Tide", "Deep blue water, white and red",
            C(0x0A1A7A), C(0x10249E), C(0x1430C8), C(0xFF1F3D), C(0xFF6A7D), C(0xFFFFFF),
            C(0xFFFFFF), C(0xD6DCFF), C(0xA9B4F0), C(0x4D63E0), C(0x3B5BFF), C(0x0B1D8E),
            "Arial Black, Segoe UI Black", "Segoe UI Variable Text, Segoe UI", 0),
        new AppTheme("dusk", "Dusk", "Slate after sunset, blue",
            C(0x0E1218), C(0x171D26), C(0x232A33), C(0x1A9FFF), C(0x66C0FF), C(0xFFFFFF),
            C(0xFFFFFF), C(0xC3C8CF), C(0x8B929A), C(0x3D4450), C(0x1A6FC0), C(0x6A4A2A),
            "Bahnschrift, Segoe UI", "Segoe UI Variable Text, Segoe UI", 6),
        new AppTheme("volt", "Volt", "Black and charged amber",
            C(0x000000), C(0x0E0E0E), C(0x1D1D1D), C(0xFFAD0A), C(0xFFC95A), C(0x111111),
            C(0xFFFFFF), C(0xCFCFCF), C(0xAAAAAA), C(0x3B3B3B), C(0xB06A00), C(0x2A2A2A),
            "Segoe UI Variable Display, Segoe UI", "Segoe UI Variable Text, Segoe UI", 12),
        new AppTheme("blossom", "Yozakura", "Blossoms at night, lantern light",
            C(0x160A16), C(0x200D1E), C(0x2C1328), C(0xFFB56B), C(0xFFD2A0), C(0x4A140F),
            C(0xF6E9EC), C(0xD7C1C8), C(0xA8909B), C(0x5A3550), C(0xC24A7E), C(0x5A2458),
            "Sitka Heading, Georgia", "Sitka Text, Georgia", 8),
    };

    /// <summary>Written by previews/console-concepts/themes/app_films.py: where each film's logo settles.</summary>
    internal static readonly Dictionary<string, IntroSpec> Intros = new()
    {
        ["rebel"] = new("rebel", 2.56, 2.51, 2.66, new(624.0, 225.89, 672.0, 452.17), new(752, 523), new(0, 4, 752, 506)),
        ["pulse"] = new("pulse", 2.74, 2.69, 2.84, new(624.0, 303.77, 672.0, 428.04), new(752, 523), new(0, 24, 752, 479)),
        ["spotlight"] = new("spotlight", 2.7, 2.65, 2.8, new(660.0, 240.15, 600.0, 409.31), new(752, 523), new(0, 3, 752, 513)),
        ["nexus"] = new("nexus", 2.67, 2.62, 2.77, new(624.0, 273.0, 672.0, 453.06), new(752, 523), new(0, 3, 752, 507)),
        ["tide"] = new("tide", 2.67, 2.62, 2.77, new(600.0, 229.63, 720.0, 489.26), new(752, 523), new(0, 0, 752, 511)),
        ["dusk"] = new("dusk", 2.80, 2.74, 2.89, new(624.0, 272.68, 672.0, 459.32), new(752, 523), new(0, 0, 752, 514)),
        ["volt"] = new("volt", 2.67, 2.62, 2.77, new(600.0, 289.63, 720.0, 477.77), new(752, 523), new(0, 0, 752, 499)),
        ["blossom"] = new("blossom", 2.67, 2.62, 2.77, new(600.0, 265.63, 720.0, 488.3), new(752, 523), new(0, 0, 752, 510)),
    };

    /// <summary>The theme in use for this run (set once at startup).</summary>
    public static AppTheme Current { get; private set; } = Default;

    public static AppTheme Find(string? id) => All.FirstOrDefault(t => t.Id == id) ?? Default;

    /// <summary>The saved choice, read before any window is built; a missing or unreadable file is Default.</summary>
    public static AppTheme Saved(string preferencesPath)
    {
        try { var preferences = new ProductPreferences(preferencesPath); preferences.Load(); return Find(preferences.Theme); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException or InvalidOperationException) { return Default; }
    }

    /// <summary>Gives the app this theme, once, before the first window. Styles take their brushes when their dictionary is
    /// read, so the dictionaries are read again from their XAML with every colour recoloured (see <see cref="Recolor"/>),
    /// the fonts swapped and the corners scaled; Default leaves the app exactly as built.</summary>
    public static void Apply(AppTheme theme)
    {
        Current = theme;
        if (theme == Default) return;
        var merged = Application.Current.Resources.MergedDictionaries; var fresh = new List<ResourceDictionary>();
        foreach (string name in new[] { "Colors", "Typography", "Controls", "Design", "ZoneSurfaces" })
            fresh.Add(Load(name));
        merged.Clear(); foreach (var dictionary in fresh) merged.Add(dictionary);
    }

    /// <summary>One theme dictionary, read from the app's own XAML with the theme applied to its text.</summary>
    public static ResourceDictionary Load(string name)
    {
        using var source = typeof(ThemeCatalog).Assembly.GetManifestResourceStream($"ThemeSource/{name}.xaml") ?? throw new FileNotFoundException(name);
        string xaml; using (var reader = new StreamReader(source)) xaml = reader.ReadToEnd();
        var t = Current;
        xaml = Regex.Replace(xaml, "#(?<a>[0-9A-Fa-f]{2})?(?<rgb>[0-9A-Fa-f]{6})(?![0-9A-Fa-f])", m =>
        {
            var old = Color.FromRgb(Convert.ToByte(m.Groups["rgb"].Value[..2], 16), Convert.ToByte(m.Groups["rgb"].Value[2..4], 16), Convert.ToByte(m.Groups["rgb"].Value[4..], 16));
            var next = RecolorRgb(old); string alpha = m.Groups["a"].Success ? m.Groups["a"].Value : "";
            return $"#{alpha}{next.R:X2}{next.G:X2}{next.B:X2}";
        });
        xaml = xaml.Replace("Segoe UI Variable Display, Segoe UI", t.Display).Replace("Segoe UI Variable Text, Segoe UI", t.Body);
        double k = t.Radius / 16;
        xaml = Regex.Replace(xaml, "(<CornerRadius x:Key=\"DS\\.R\\.[A-Za-z]+\">)(\\d+)(</CornerRadius>)", m => m.Groups[1].Value + Math.Round(int.Parse(m.Groups[2].Value) * k) + m.Groups[3].Value);
        // the primary button's text is the theme's own on-accent colour: a light accent would lose white text
        xaml = Regex.Replace(xaml, "(<Style x:Key=\"PrimaryButton\"[^>]*>)", m => m.Groups[1].Value + $"<Setter Property=\"Foreground\" Value=\"#{t.OnAccent.R:X2}{t.OnAccent.G:X2}{t.OnAccent.B:X2}\"/>");
        xaml = xaml.Replace("clr-namespace:X20Ctl.Product\"", "clr-namespace:X20Ctl.Product;assembly=X20Ctl\"");
        var context = new System.Windows.Markup.ParserContext { BaseUri = new Uri($"pack://application:,,,/X20Ctl;component/Themes/{name}.xaml") };
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xaml));
        return (ResourceDictionary)System.Windows.Markup.XamlReader.Load(stream, context);
    }

    /// <summary>Where a colour of the shipped navy-and-blue look lands in the current theme. Every colour is placed by its
    /// lightness on the theme's own ramps: dark surfaces from its void through its night to its rule colour, light text
    /// from its muted through its soft to its text colour, and the blues, wherever they are used, on its accent. Greens,
    /// ambers, reds and violets keep their meaning. Under Default nothing moves.</summary>
    public static Color Recolor(Color c) { if (Current == Default) return c; var o = RecolorRgb(c); return Color.FromArgb(c.A, o.R, o.G, o.B); }
    private static Color RecolorRgb(Color c)
    {
        var t = Current;
        double r = c.R / 255d, g = c.G / 255d, b = c.B / 255d, max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), l = (max + min) / 2, d = max - min;
        double s = d == 0 ? 0 : d / (1 - Math.Abs(2 * l - 1)), h = 0;
        if (d > 0) { h = max == r ? ((g - b) / d) % 6 : max == g ? (b - r) / d + 2 : (r - g) / d + 4; h = (h * 60 + 360) % 360; }
        bool blue = h >= 195 && h <= 265;
        static double Ramp(double x, (double at, Color c)[] stops, out Color result)
        {
            if (x <= stops[0].at) { result = stops[0].c; return 0; }
            for (int i = 1; i < stops.Length; i++) if (x <= stops[i].at) { result = Mix(stops[i - 1].c, stops[i].c, (x - stops[i - 1].at) / (stops[i].at - stops[i - 1].at)); return 0; }
            result = stops[^1].c; return 0;
        }
        Color o;
        if (d >= .28 && l >= .38 && l < .9 && blue)
        {   // the accent: its own colour at mid lightness, its bright form above, deeper below
            if (l >= .6) Ramp(l, new[] { (.6, t.Accent), (.85, t.AccentHi) }, out o); else Ramp(l, new[] { (.38, Mix(t.Accent, t.Void, .45)), (.6, t.Accent) }, out o);
            return o;
        }
        if (l >= .45 && (d < .28 || !blue)) { if (d >= .28 && !blue) return c; Ramp(l, new[] { (.45, t.Muted), (.55, t.Muted), (.72, t.Soft), (.95, t.Text) }, out o); return o; }
        if (d >= .28 && !blue && l >= .2) return c; // a semantic colour: success, warning, error, violet
        Ramp(l, new[] { (0d, t.Void), (.06, t.Deep), (.12, t.Night), (.25, t.Rule), (.45, Mix(t.Rule, t.Muted, .6)) }, out o);
        return o;
    }
    /// <summary>A colour drawn in code, placed in the theme the same way the XAML's are.</summary>
    public static Color R(Color original) => Recolor(original);

    /// <summary>Opens a fresh X20CTL and closes this one, so the chosen theme is built from the start.</summary>
    public static void Restart()
    {
        if (ReviewSandbox.Active || Environment.ProcessPath is not { } exe) return;
        // run from a build folder, the process is dotnet itself and the app is its first argument
        string arguments = Path.GetFileNameWithoutExtension(exe).Equals("dotnet", StringComparison.OrdinalIgnoreCase) ? "\"" + Environment.GetCommandLineArgs()[0] + "\"" : "";
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe, arguments) { UseShellExecute = false, WorkingDirectory = AppContext.BaseDirectory }); }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException) { return; }
        Application.Current.Shutdown();
    }
    public static Color Mix(Color a, Color b, double k) => Color.FromRgb((byte)(a.R + (b.R - a.R) * k), (byte)(a.G + (b.G - a.G) * k), (byte)(a.B + (b.B - a.B) * k));
    public static Color Alpha(Color c, byte a) => Color.FromArgb(a, c.R, c.G, c.B);
    /// <summary>A colour drawn in code: exactly as built under Default, the theme's own under any other.</summary>
    public static Color Tone(Color original, Func<AppTheme, Color> themed) => Current == Default ? original : themed(Current);
    /// <summary>A corner drawn in code, scaled by the theme's corners (Default's card corner is 16).</summary>
    public static CornerRadius Round(double original) => new(original * Current.Radius / 16);
}
