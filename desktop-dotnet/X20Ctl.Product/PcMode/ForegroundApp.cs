using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Win32Exception = System.ComponentModel.Win32Exception;
namespace X20Ctl.Product;

/// <summary>
/// Which app is in front, for per-game PC mode profiles. Looked at once a second: the exe name and window title of the
/// foreground window, and the last one that wasn't X20CTL or the Windows shell, so "Add the game you were just in"
/// works after pressing Home to bring X20CTL forward. Only the exe name is ever stored, as the profile's key.
/// </summary>
public static class ForegroundApp
{
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowTextW(IntPtr window, StringBuilder text, int max);
    [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr OpenProcess(uint access, bool inherit, uint id);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool QueryFullProcessImageNameW(IntPtr process, uint flags, StringBuilder name, ref uint size);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
    static readonly HashSet<string> Shell = new(StringComparer.OrdinalIgnoreCase)
    {
        "explorer.exe", "searchhost.exe", "startmenuexperiencehost.exe", "shellexperiencehost.exe", "lockapp.exe", "textinputhost.exe",
        "applicationframehost.exe", "taskmgr.exe", "osk.exe", "tabtip.exe", "systemsettings.exe", "searchapp.exe",
    };
    static readonly int self = Environment.ProcessId;
    static Timer? timer;
    static volatile string? exe;
    public sealed record App(string Exe, string Title);
    /// <summary>The exe name of the app in front right now ("eldenring.exe"), or null for X20CTL, the shell or nothing.</summary>
    public static string? Exe => exe;
    /// <summary>The last app in front that wasn't X20CTL or the shell.</summary>
    public static App? Last { get; private set; }

    public static void Start() { if (ReviewSandbox.Active) return; timer ??= new Timer(_ => Look(), null, 0, 1000); }

    static void Look()
    {
        try
        {
            var app = Read(GetForegroundWindow());
            exe = app?.Exe; if (app != null) Last = app;
        }
        catch (Exception error) when (error is Win32Exception or InvalidOperationException) { exe = null; }
    }

    static App? Read(IntPtr window)
    {
        if (window == IntPtr.Zero) return null;
        GetWindowThreadProcessId(window, out uint pid);
        if (pid == 0 || pid == self) return null;
        string? path = ImagePath(pid); if (path == null) return null;
        string name = Path.GetFileName(path);
        if (Shell.Contains(name)) return null;
        var title = new StringBuilder(256); GetWindowTextW(window, title, title.Capacity);
        return new(name.ToLowerInvariant(), title.Length > 0 ? title.ToString() : Path.GetFileNameWithoutExtension(name));
    }

    static string? ImagePath(uint pid)
    {
        IntPtr process = OpenProcess(0x1000, false, pid); // PROCESS_QUERY_LIMITED_INFORMATION
        if (process == IntPtr.Zero) return null;
        try { var name = new StringBuilder(1024); uint size = (uint)name.Capacity; return QueryFullProcessImageNameW(process, 0, name, ref size) ? name.ToString() : null; }
        finally { CloseHandle(process); }
    }

    /// <summary>Apps with a window open now, for picking a game to give its own settings. The last one in front comes first.</summary>
    public static IReadOnlyList<App> Running()
    {
        var apps = new List<App>();
        if (Last is { } last) apps.Add(last);
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                if (p.Id == self || p.MainWindowHandle == IntPtr.Zero || string.IsNullOrWhiteSpace(p.MainWindowTitle)) continue;
                if (ImagePath((uint)p.Id) is not { } path || Shell.Contains(Path.GetFileName(path))) continue;
                string name = Path.GetFileName(path).ToLowerInvariant();
                if (!apps.Any(a => a.Exe == name)) apps.Add(new(name, p.MainWindowTitle));
            }
            catch (Exception error) when (error is InvalidOperationException or Win32Exception) { }
            finally { p.Dispose(); }
        }
        return apps;
    }
}
