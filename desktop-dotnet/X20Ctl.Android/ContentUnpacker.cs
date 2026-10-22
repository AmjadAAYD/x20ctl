using Android.Content;

namespace X20Ctl.Droid;

/// <summary>
/// The desktop build keeps Data/ and Brand/ next to the program, and the shared screens read them as files. Here they
/// travel inside the APK as assets and are copied into the app's own folder once per install or update.
/// </summary>
internal static class ContentUnpacker
{
    static readonly string[] Files = ["Data/catalog.json", "Data/framing.json", "Data/vibration-outlines.json", "Brand/intro-film.frames", "Brand/intro-film.wav"];

    public static string Unpack(Context context)
    {
        string root = Path.Combine(context.FilesDir!.AbsolutePath, "content");
        string marker = Path.Combine(root, ".installed"), stamp = Stamp(context);
        try
        {
            if (File.Exists(marker) && File.ReadAllText(marker) == stamp) return root;
            foreach (string name in Files)
            {
                string target = Path.Combine(root, name);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                using var input = context.Assets!.Open(name);
                using var output = File.Create(target + ".part");
                input.CopyTo(output);
                output.Close();
                File.Move(target + ".part", target, overwrite: true);
            }
            File.WriteAllText(marker, stamp);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or Java.IO.IOException)
        {
            // the screens that need a missing file show their own "not available" state
            global::Android.Util.Log.Warn("X20CTL", "Couldn't unpack the app's data: " + e.Message);
        }
        return root;
    }

    // a new install or update replaces the files; the same install reuses them
    static string Stamp(Context context)
    {
        try
        {
#pragma warning disable CA1422, CS0618 // the flags overload is the one that works on every supported version
            var info = context.PackageManager!.GetPackageInfo(context.PackageName!, 0)!;
#pragma warning restore CA1422, CS0618
            return info.LastUpdateTime.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (global::Android.Content.PM.PackageManager.NameNotFoundException) { return "unknown"; }
    }
}
