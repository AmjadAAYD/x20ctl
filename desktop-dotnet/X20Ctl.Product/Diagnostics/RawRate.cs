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
    public sealed record HidDescriptorInfo(uint VendorId, uint ProductId, uint VersionNumber, ushort UsagePage, ushort Usage, ushort InputBytes, ushort OutputBytes, ushort FeatureBytes, ushort InputButtonCaps, ushort InputValueCaps);
    public sealed record DetailedDevice(string Name, int Current, int Peak, double LastIntervalMs, double MinMs, double AvgMs, double P99Ms, double MaxMs, double StdDevMs, int SpikeCount, int[] Histogram, byte[] LatestPacket, bool[] ChangedMask, int ReportSize, long TotalPackets, HidDescriptorInfo? Descriptor);

    sealed class Entry(string name, HidDescriptorInfo? descriptor)
    {
        public readonly string Name = name;
        public readonly HidDescriptorInfo? Descriptor = descriptor;
        public readonly RateMeter Meter = new();
        public readonly JitterStats Jitter = new();
        public double LastPacketMs;
        public byte[] LatestPacket = [];
        public double[] ChangedAtMs = [];
        public int ReportSize;
        public long TotalPackets;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct HidpCaps
    {
        public ushort Usage, UsagePage, InputReportByteLength, OutputReportByteLength, FeatureReportByteLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)] public ushort[] Reserved;
        public ushort NumberLinkCollectionNodes, NumberInputButtonCaps, NumberInputValueCaps, NumberInputDataIndices;
        public ushort NumberOutputButtonCaps, NumberOutputValueCaps, NumberOutputDataIndices;
        public ushort NumberFeatureButtonCaps, NumberFeatureValueCaps, NumberFeatureDataIndices;
    }

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
    [DllImport("hid.dll")] static extern int HidP_GetCaps(IntPtr preparsedData, out HidpCaps capabilities);
    const uint WM_INPUT = 0x00FF, WM_STOP = 0x8001, RID_INPUT = 0x10000003, RIDI_PREPARSEDDATA = 0x20000005, RIDI_DEVICENAME = 0x20000007, RIDI_DEVICEINFO = 0x2000000B, RIDEV_REMOVE = 1, RIDEV_INPUTSINK = 0x100;
    const int HidpStatusSuccess = 0x00110000;
    static readonly ushort[] Usages = [4, 5, 8];
    static readonly IntPtr MessageParent = new(-3);

    readonly object gate = new();
    readonly Dictionary<IntPtr, Entry> devices = new();
    readonly Stopwatch clock = Stopwatch.StartNew();
    readonly Thread worker;
    readonly WindowProc proc; // kept alive for as long as the window exists
    readonly string className = "X20CTL.RawRate." + Guid.NewGuid().ToString("N");
    readonly ManualResetEventSlim ready = new();
    IntPtr window, buffer; uint bufferSize;
    public string? Error { get; private set; }
    public bool Paused { get; set; }

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

    public IReadOnlyList<DetailedDevice> DetailedDevices()
    {
        double now = clock.Elapsed.TotalMilliseconds;
        lock (gate)
        {
            return devices.Values.Select(d =>
            {
                bool[] changed = new bool[d.LatestPacket.Length];
                for (int i = 0; i < changed.Length && i < d.ChangedAtMs.Length; i++)
                    changed[i] = d.ChangedAtMs[i] > 0 && now - d.ChangedAtMs[i] <= 450.0;
                return new DetailedDevice(
                    d.Name,
                    d.Meter.Current(now),
                    d.Meter.Peak,
                    d.Jitter.LastMs,
                    d.Jitter.MinMs == double.MaxValue ? 0 : d.Jitter.MinMs,
                    d.Jitter.AvgMs,
                    d.Jitter.P99Ms,
                    d.Jitter.MaxMs,
                    d.Jitter.StdDevMs,
                    d.Jitter.SpikeCount,
                    d.Jitter.Histogram,
                    [.. d.LatestPacket],
                    changed,
                    d.ReportSize,
                    d.TotalPackets,
                    d.Descriptor);
            }).OrderByDescending(d => d.Peak).ToList();
        }
    }

    public void Reset()
    {
        lock (gate)
        {
            foreach (var d in devices.Values)
            {
                d.Meter.Reset();
                d.Jitter.Reset();
                d.LastPacketMs = 0;
                d.TotalPackets = 0;
            }
        }
    }

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
        IntPtr device = Marshal.ReadIntPtr(buffer, 8);
        int reportSize = Marshal.ReadInt32(buffer, (int)header);
        int reports = Marshal.ReadInt32(buffer, (int)header + 4);
        if (reports is < 1 or > 1024 || reportSize is < 1 or > 2048) return;
        double now = clock.Elapsed.TotalMilliseconds;

        int copyLen = Math.Min(reportSize, 128);
        int packetOffset = (int)header + 8 + (reports - 1) * reportSize;
        byte[]? packet = null;
        if (!Paused && packetOffset + copyLen <= size)
        {
            packet = new byte[copyLen];
            Marshal.Copy(IntPtr.Add(buffer, packetOffset), packet, 0, copyLen);
        }

        lock (gate)
        {
            if (!devices.TryGetValue(device, out var entry))
                devices[device] = entry = new Entry(Name(device), ReadDescriptor(device));
            entry.Meter.Add(now, reports);
            if (entry.LastPacketMs > 0)
            {
                double dt = (now - entry.LastPacketMs) / reports;
                for (int i = 0; i < reports; i++) entry.Jitter.AddInterval(dt);
            }
            entry.LastPacketMs = now;
            entry.ReportSize = reportSize;
            entry.TotalPackets += reports;
            if (packet != null)
            {
                if (entry.ChangedAtMs.Length != packet.Length) entry.ChangedAtMs = new double[packet.Length];
                for (int i = 0; i < packet.Length; i++)
                {
                    if (i < entry.LatestPacket.Length && entry.LatestPacket[i] != packet[i])
                        entry.ChangedAtMs[i] = now;
                }
                entry.LatestPacket = packet;
            }
        }
    }

    static HidDescriptorInfo? ReadDescriptor(IntPtr device)
    {
        try
        {
            uint vid = 0, pid = 0, ver = 0;
            ushort usagePage = 0, usage = 0, inLen = 0, outLen = 0, featLen = 0, inBtns = 0, inVals = 0;
            uint infoSize = 0;
            GetRawInputDeviceInfoW(device, RIDI_DEVICEINFO, IntPtr.Zero, ref infoSize);
            if (infoSize is >= 24 and <= 256)
            {
                IntPtr infoBuf = Marshal.AllocHGlobal((int)infoSize);
                try
                {
                    Marshal.WriteInt32(infoBuf, 0, (int)infoSize);
                    if (GetRawInputDeviceInfoW(device, RIDI_DEVICEINFO, infoBuf, ref infoSize) != uint.MaxValue && Marshal.ReadInt32(infoBuf, 4) == 2)
                    {
                        vid = (uint)Marshal.ReadInt32(infoBuf, 8);
                        pid = (uint)Marshal.ReadInt32(infoBuf, 12);
                        ver = (uint)Marshal.ReadInt32(infoBuf, 16);
                        usagePage = (ushort)Marshal.ReadInt16(infoBuf, 20);
                        usage = (ushort)Marshal.ReadInt16(infoBuf, 22);
                    }
                }
                finally { Marshal.FreeHGlobal(infoBuf); }
            }

            uint prepSize = 0;
            GetRawInputDeviceInfoW(device, RIDI_PREPARSEDDATA, IntPtr.Zero, ref prepSize);
            if (prepSize is > 0 and <= 65536)
            {
                IntPtr prepBuf = Marshal.AllocHGlobal((int)prepSize);
                try
                {
                    if (GetRawInputDeviceInfoW(device, RIDI_PREPARSEDDATA, prepBuf, ref prepSize) != uint.MaxValue
                        && HidP_GetCaps(prepBuf, out var caps) == HidpStatusSuccess)
                    {
                        usage = caps.Usage;
                        usagePage = caps.UsagePage;
                        inLen = caps.InputReportByteLength;
                        outLen = caps.OutputReportByteLength;
                        featLen = caps.FeatureReportByteLength;
                        inBtns = caps.NumberInputButtonCaps;
                        inVals = caps.NumberInputValueCaps;
                    }
                }
                finally { Marshal.FreeHGlobal(prepBuf); }
            }

            return vid == 0 && inLen == 0 ? null : new HidDescriptorInfo(vid, pid, ver, usagePage, usage, inLen, outLen, featLen, inBtns, inVals);
        }
        catch (Exception)
        {
            return null;
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
