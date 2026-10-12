using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;
using X20Ctl.Zone.Diagnostics;
namespace X20Ctl.Product.Diagnostics;

/// <summary>
/// How many HID reports a second each game controller really sends, counted from Windows Raw Input (Generic Desktop
/// joystick, gamepad and multi-axis usages) on its own thread with a message-only window, so it keeps counting at
/// 8000 a second and while X20CTL is behind another window. Read-only: it only listens. Raw Input registration is one
/// per process per usage, so this must not run while Controller Check (which registers the same usages) is open.
/// </summary>
public sealed class RawRate : IDisposable
{
    public sealed record Device(string Name, int Current, int Peak);
    delegate IntPtr WindowProc(IntPtr window, uint message, IntPtr w, IntPtr l);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct WindowClass { public uint size, style; public WindowProc proc; public int classExtra, windowExtra; public IntPtr instance, icon, cursor, background; public string? menu; public string name; public IntPtr smallIcon; }
    [StructLayout(LayoutKind.Sequential)] struct Message { public IntPtr window; public uint message; public IntPtr w, l; public uint time; public int x, y; }
    [StructLayout(LayoutKind.Sequential)] struct Registration { public ushort page, usage; public uint flags; public IntPtr target; }
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern ushort RegisterClassExW(ref WindowClass c);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool UnregisterClassW(string name, IntPtr instance);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern IntPtr CreateWindowExW(uint exStyle, string className, string title, uint style, int x, int y, int w, int h, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);
    [DllImport("user32.dll")] static extern bool DestroyWindow(IntPtr window);
    [DllImport("user32.dll")] static extern IntPtr DefWindowProcW(IntPtr window, uint message, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] static extern int GetMessageW(out Message m, IntPtr window, uint min, uint max);
    [DllImport("user32.dll")] static extern IntPtr DispatchMessageW(ref Message m);
    [DllImport("user32.dll")] static extern bool PostMessageW(IntPtr window, uint message, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] static extern void PostQuitMessage(int code);
    [DllImport("user32.dll", SetLastError = true)] static extern bool RegisterRawInputDevices(Registration[] devices, uint count, uint size);
    [DllImport("user32.dll")] static extern uint GetRawInputData(IntPtr handle, uint command, IntPtr data, ref uint size, uint headerSize);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern uint GetRawInputDeviceInfoW(IntPtr handle, uint command, IntPtr data, ref uint size);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern IntPtr GetModuleHandleW(string? name);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern SafeFileHandle CreateFileW(string path, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
    [DllImport("hid.dll", CharSet = CharSet.Unicode)] static extern bool HidD_GetProductString(SafeFileHandle device, char[] buffer, uint length);
    const uint WM_INPUT = 0x00FF, WM_STOP = 0x8001, RID_INPUT = 0x10000003, RIDI_DEVICENAME = 0x20000007, RIDEV_REMOVE = 1, RIDEV_INPUTSINK = 0x100;
    static readonly ushort[] Usages = [4, 5, 8];
    static readonly IntPtr MessageParent = new(-3);

    readonly object gate = new();
    readonly Dictionary<IntPtr, (string Name, RateMeter Meter)> devices = new();
    readonly Stopwatch clock = Stopwatch.StartNew();
    readonly Thread worker;
    readonly WindowProc proc; // kept alive for as long as the window exists
    readonly string className = "X20CTL.RawRate." + Guid.NewGuid().ToString("N");
    readonly ManualResetEventSlim ready = new();
    IntPtr window, buffer; uint bufferSize;
    public string? Error { get; private set; }

    public RawRate()
    {
        proc = Proc;
        worker = new Thread(Run) { IsBackground = true, Name = "X20CTL report rate" };
        worker.Start();
        ready.Wait(2000);
    }

    public IReadOnlyList<Device> Devices()
    {
        double now = clock.Elapsed.TotalMilliseconds;
        lock (gate) return devices.Values.Select(d => new Device(d.Name, d.Meter.Current(now), d.Meter.Peak)).OrderByDescending(d => d.Peak).ToList();
    }

    public void Reset() { lock (gate) foreach (var d in devices.Values) d.Meter.Reset(); }

    void Run()
    {
        var instance = GetModuleHandleW(null);
        var c = new WindowClass { size = (uint)Marshal.SizeOf<WindowClass>(), proc = proc, instance = instance, name = className };
        try
        {
            if (RegisterClassExW(ref c) == 0) { Error = "Windows wouldn't start the report counter."; return; }
            window = CreateWindowExW(0, className, "", 0, 0, 0, 0, 0, MessageParent, IntPtr.Zero, instance, IntPtr.Zero);
            if (window == IntPtr.Zero) { Error = "Windows wouldn't start the report counter."; return; }
            var items = Usages.Select(u => new Registration { page = 1, usage = u, flags = RIDEV_INPUTSINK, target = window }).ToArray();
            if (!RegisterRawInputDevices(items, (uint)items.Length, (uint)Marshal.SizeOf<Registration>())) { Error = "Windows wouldn't share controller reports."; DestroyWindow(window); window = IntPtr.Zero; return; }
        }
        finally { ready.Set(); }
        while (GetMessageW(out var m, IntPtr.Zero, 0, 0) > 0) DispatchMessageW(ref m);
        UnregisterClassW(className, instance);
        if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer);
    }

    IntPtr Proc(IntPtr hwnd, uint message, IntPtr w, IntPtr l)
    {
        if (message == WM_INPUT) Count(l);
        else if (message == WM_STOP)
        {
            var items = Usages.Select(u => new Registration { page = 1, usage = u, flags = RIDEV_REMOVE, target = IntPtr.Zero }).ToArray();
            RegisterRawInputDevices(items, (uint)items.Length, (uint)Marshal.SizeOf<Registration>());
            DestroyWindow(hwnd); PostQuitMessage(0); return IntPtr.Zero;
        }
        return DefWindowProcW(hwnd, message, w, l);
    }

    void Count(IntPtr handle)
    {
        uint header = (uint)(8 + IntPtr.Size * 2), size = 0;
        if (GetRawInputData(handle, RID_INPUT, IntPtr.Zero, ref size, header) == uint.MaxValue || size < header + 8 || size > 65536) return;
        if (size > bufferSize) { if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer); buffer = Marshal.AllocHGlobal((int)size); bufferSize = size; }
        if (GetRawInputData(handle, RID_INPUT, buffer, ref size, header) == uint.MaxValue || Marshal.ReadInt32(buffer) != 2) return; // RIM_TYPEHID
        IntPtr device = Marshal.ReadIntPtr(buffer, 8); int reports = Marshal.ReadInt32(buffer, (int)header + 4);
        if (reports is < 1 or > 1024) return;
        double now = clock.Elapsed.TotalMilliseconds;
        lock (gate)
        {
            if (!devices.TryGetValue(device, out var entry)) devices[device] = entry = (Name(device), new RateMeter());
            entry.Meter.Add(now, reports);
        }
    }

    /// <summary>The name the controller gives itself, read with no access rights (nothing is sent), plus its USB ids.</summary>
    static string Name(IntPtr device)
    {
        uint chars = 0; GetRawInputDeviceInfoW(device, RIDI_DEVICENAME, IntPtr.Zero, ref chars);
        if (chars is 0 or > 32768) return "Game controller";
        IntPtr text = Marshal.AllocHGlobal((int)(chars + 1) * 2); string path;
        try { if (GetRawInputDeviceInfoW(device, RIDI_DEVICENAME, text, ref chars) == uint.MaxValue) return "Game controller"; path = Marshal.PtrToStringUni(text) ?? ""; }
        finally { Marshal.FreeHGlobal(text); }
        string? product = null;
        using (var file = CreateFileW(path, 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero))
            if (!file.IsInvalid) { var name = new char[127]; if (HidD_GetProductString(file, name, (uint)name.Length * 2)) product = new string(name).TrimEnd('\0').Trim(); }
        var ids = Regex.Match(path, @"VID[_&](?:[0-9A-F]{4})?([0-9A-F]{4}).{0,8}?PID[_&]([0-9A-F]{4})", RegexOptions.IgnoreCase);
        string tail = ids.Success ? $" ({ids.Groups[1].Value.ToUpperInvariant()}:{ids.Groups[2].Value.ToUpperInvariant()})" : "";
        return (string.IsNullOrWhiteSpace(product) ? "Game controller" : product) + tail;
    }

    public void Dispose()
    {
        if (window != IntPtr.Zero) PostMessageW(window, WM_STOP, IntPtr.Zero, IntPtr.Zero);
        worker.Join(1000); ready.Dispose();
    }
}
