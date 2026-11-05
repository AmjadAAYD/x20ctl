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

    public static readonly IReadOnlyList<AppTheme> All = new[] { Default }.Concat(IllustratedThemeUI.Catalog()).ToArray();

    /// <summary>Written by previews/console-concepts/themes/app_films.py: where each film's logo settles.</summary>
    internal static readonly Dictionary<string, IntroSpec> Intros = new()
    {
        ["nexus"] = new("nexus", 2.67, 2.62, 2.77, new(624.0, 273.0, 672.0, 453.06), new(752, 523), new(0, 3, 752, 507)),
        ["dusk"] = new("dusk", 2.80, 2.74, 2.89, new(624.0, 272.68, 672.0, 459.32), new(752, 523), new(0, 0, 752, 514)),
        ["hollow"] = new("hollow", 2.67, 2.62, 2.77, new(806.81, 366.35, 307.34, 294.89), new(752, 523), new(216, 80, 321, 308)),
        ["nocturne"] = new("nocturne", 2.67, 2.62, 2.77, new(806.81, 366.35, 307.34, 294.89), new(752, 523), new(216, 80, 321, 308)),
        ["sunrise"] = new("sunrise", 2.67, 2.62, 2.77, new(806.81, 366.35, 307.34, 294.89), new(752, 523), new(216, 80, 321, 308)),
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
        IllustratedThemeUI.Install();
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
