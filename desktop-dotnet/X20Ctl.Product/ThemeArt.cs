using System;
using System.IO;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace X20Ctl.Product;

/// <summary>The upright controller art the Blossom and Dusk themes draw: loose files in a dev checkout, the embedded copy everywhere else.</summary>
internal static class ThemeArt
{
    /// <summary>Art file name (without .png) for a model id such as x20, x20_pro, x05_pro, d10, x15 or dune.</summary>
    public static string Key(string modelId)
    {
        string s = modelId.Replace("_", "-").ToLowerInvariant();
        if (s.Contains("starfield")) return "x15-starfield";
        if (s.Contains("wukong")) return "x15-wukong";
        if (s == "x15" || s.StartsWith("x15-")) return "x15-starfield";
        if (s.Contains("d10")) return "d10-purple";
        if (s.StartsWith("x20") && s.Contains("pro")) return "x20pro";
        if (s.StartsWith("x20") && s.Contains("black")) return "x20-black";
        if (s.StartsWith("x05")) return "x05pro";
        return s;
    }

    public static Image? Load(string modelId, double width)
    {
        string key = Key(modelId);
        BitmapImage? bmp = null;
        foreach (string dir in new[] { Path.Combine(AppContext.BaseDirectory, "Assets", "art"), Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Assets", "art") })
        {
            string file = Path.GetFullPath(Path.Combine(dir, key + ".png"));
            if (!File.Exists(file)) continue;
            bmp = Open(new Uri(file, UriKind.Absolute));
            if (bmp != null) break;
        }
        bmp ??= Open(new Uri($"pack://application:,,,/Assets/art/{key}.png", UriKind.Absolute));
        if (bmp == null) return null;
        return new Image { Width = width, Source = bmp, Stretch = Stretch.Uniform };
    }

    private static BitmapImage? Open(Uri uri)
    {
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = uri;
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch { return null; }
    }
}
