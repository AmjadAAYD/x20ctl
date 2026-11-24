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
    [DllImport("user32.dll")] static extern bool GetCursorInfo(ref CursorInfo pci);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hWnd, out Rect rect);
    [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint dwFlags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool GetMonitorInfoW(IntPtr hMonitor, ref MonitorInfo lpmi);
    [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr OpenProcess(uint access, bool inherit, uint id);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool QueryFullProcessImageNameW(IntPtr process, uint flags, StringBuilder name, ref uint size);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);

    [StructLayout(LayoutKind.Sequential)]
    struct Point { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    struct CursorInfo
    {
        public uint cbSize;
        public uint flags;
        public IntPtr hCursor;
        public Point ptScreenPos;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct Rect { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct MonitorInfo
    {
        public uint cbSize;
        public Rect rcMonitor;
        public Rect rcWork;
        public uint dwFlags;
    }
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
    /// <summary>The app currently in the foreground, or null when X20CTL or the desktop/shell has focus.</summary>
    public static App? Current { get; private set; }
    /// <summary>The last app in front that wasn't X20CTL or the shell.</summary>
    public static App? Last { get; private set; }

    public static void Start() { if (ReviewSandbox.Active) return; timer ??= new Timer(_ => Look(), null, 0, 1000); }

    /// <summary>True when Windows shows an active mouse cursor (menus, inventory, desktop).</summary>
    public static bool IsCursorVisible()
    {
        if (ReviewSandbox.Active) return false;
        try
        {
            var ci = new CursorInfo { cbSize = (uint)Marshal.SizeOf<CursorInfo>() };
            return GetCursorInfo(ref ci) && (ci.flags & 0x00000001u) != 0;
        }
        catch { return false; }
    }

    /// <summary>True when the current foreground window fills at least 92% of its monitor.</summary>
    public static bool IsForegroundFullscreen()
    {
        if (ReviewSandbox.Active) return false;
        try
        {
            var hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero || !GetWindowRect(hwnd, out var r)) return false;
            var mon = MonitorFromWindow(hwnd, 2u);
            if (mon == IntPtr.Zero) return false;
            var mi = new MonitorInfo { cbSize = (uint)Marshal.SizeOf<MonitorInfo>() };
            if (!GetMonitorInfoW(mon, ref mi)) return false;
            int w = r.Right - r.Left;
            int h = r.Bottom - r.Top;
            int mw = mi.rcMonitor.Right - mi.rcMonitor.Left;
            int mh = mi.rcMonitor.Bottom - mi.rcMonitor.Top;
            return mw > 0 && mh > 0 && w >= mw * 0.92 && h >= mh * 0.92;
        }
        catch { return false; }
    }

    static void Look()
    {
        try
        {
            var app = Read(GetForegroundWindow());
            Current = app;
            exe = app?.Exe; if (app != null) Last = app;
        }
        catch (Exception error) when (error is Win32Exception or InvalidOperationException) { Current = null; exe = null; }
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
