using System.Diagnostics;
namespace X20Ctl.Linux;

/// <summary>Which games are running and which one is in front, for per-game PC mode profiles. Only the exe name is ever stored, as the profile's key.
/// In front is known on X11 through xprop. Without it (Wayland, or xprop missing) a game's profile applies while that game is running instead.</summary>
internal static class GameApps
{
    public sealed record App(string Exe, string Title);
    static readonly HashSet<string> Noise = new(StringComparer.OrdinalIgnoreCase)
    {
        "wineserver", "services.exe", "winedevice.exe", "plugplay.exe", "svchost.exe", "rpcss.exe", "explorer.exe", "conhost.exe", "steam.exe", "steamwebhelper.exe", "wineboot.exe", "start.exe",
        "winedbg.exe", "tabtip.exe", "crashhandler.exe", "crashhandler64.exe", "x20ctl-studio", "dotnet"
    };
    static readonly int Self = Environment.ProcessId;
    public static bool CanSeeForeground { get; private set; } = Probe();

    static bool Probe() => OperatingSystem.IsLinux() && Environment.GetEnvironmentVariable("DISPLAY") is { Length: > 0 } && Run("xprop", "-root _NET_ACTIVE_WINDOW") is { Length: > 0 };

    static string? Run(string file, string args)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo(file, args) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false });
            if (p == null) return null;
            string output = p.StandardOutput.ReadToEnd(); if (!p.WaitForExit(800)) { try { p.Kill(); } catch (InvalidOperationException) { } return null; }
            return p.ExitCode == 0 ? output : null;
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or IOException) { return null; }
    }

    /// <summary>The exe name a process presents as a game: a Windows exe under Wine or Proton, else its own program name.</summary>
    static string? NameOf(int pid)
    {
        try
        {
            string[] args = File.ReadAllText($"/proc/{pid}/cmdline").Split('\0', StringSplitOptions.RemoveEmptyEntries);
            foreach (string a in args)
                if (a.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return Path.GetFileName(a.Replace('\\', '/')).ToLowerInvariant();
            string? exe = args.Length > 0 ? Path.GetFileName(args[0]) : null;
            return string.IsNullOrEmpty(exe) ? null : exe.ToLowerInvariant();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; }
    }

    /// <summary>Games running now: Windows exes under Wine or Proton, and programs started from a Steam library.</summary>
    public static List<App> Running()
    {
        var found = new Dictionary<string, App>();
        if (!Directory.Exists("/proc")) return [];
        foreach (string dir in Directory.EnumerateDirectories("/proc"))
        {
            if (!int.TryParse(Path.GetFileName(dir), out int pid) || pid == Self) continue;
            try
            {
                string cmd = File.ReadAllText(dir + "/cmdline").Replace('\0', ' ');
                bool wine = cmd.Contains(".exe", StringComparison.OrdinalIgnoreCase), steam = cmd.Contains("/steamapps/common/", StringComparison.Ordinal);
                if (!wine && !steam) continue;
                string? name = NameOf(pid); if (name == null || Noise.Contains(name)) continue;
                found.TryAdd(name, new App(name, Path.GetFileNameWithoutExtension(name)));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
        return [.. found.Values.OrderBy(a => a.Exe)];
    }

    /// <summary>The exe name of the game in front, when it can be told.</summary>
    public static string? Foreground()
    {
        if (!CanSeeForeground) return null;
        string? root = Run("xprop", "-root _NET_ACTIVE_WINDOW"); int at = root?.LastIndexOf("0x", StringComparison.Ordinal) ?? -1;
        if (root == null || at < 0) return null;
        string id = root[at..].Trim().Split(' ', ',')[0];
        string? prop = Run("xprop", $"-id {id} _NET_WM_PID"); int eq = prop?.IndexOf('=') ?? -1;
        if (prop == null || eq < 0 || !int.TryParse(prop[(eq + 1)..].Trim(), out int pid) || pid == Self) return null;
        for (int hops = 0; hops < 6 && pid > 1; hops++)
        {
            string? name = NameOf(pid); if (name != null && !Noise.Contains(name) && (name.EndsWith(".exe") || true)) return name;
            pid = ParentOf(pid);
        }
        return null;
    }

    static int ParentOf(int pid)
    {
        try { foreach (string line in File.ReadLines($"/proc/{pid}/status")) if (line.StartsWith("PPid:")) return int.Parse(line[5..].Trim()); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or FormatException) { }
        return 0;
    }
}
