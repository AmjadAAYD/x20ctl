using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
namespace X20Ctl.Product.Scanner;

/// <summary>
/// The battery report Windows.Gaming.Input keeps for each game controller (RawGameController.TryGetBatteryReport): the
/// same source the Xbox Game Bar uses. It is the one place Windows passes on a battery level for an Xbox-style
/// controller on Bluetooth, where the HID battery field is hidden by Windows' own driver. Called through the raw WinRT
/// ABI so the 25 MB Windows SDK projection isn't needed. Read-only: nothing is sent to any controller.
/// </summary>
public static class GameInputBattery
{
    public sealed record Report(ushort Vid, ushort Pid, string Name, string Status, int? Percent, int? RemainingMwh, int? FullMwh, int? ChargeRateMw);

    [DllImport("combase.dll")] static extern int RoInitialize(int type);
    [DllImport("combase.dll")] static extern void RoUninitialize();
    [DllImport("combase.dll")] static extern int RoGetActivationFactory(IntPtr classId, ref Guid iid, out IntPtr factory);
    [DllImport("combase.dll", CharSet = CharSet.Unicode)] static extern int WindowsCreateString(string text, int length, out IntPtr hstring);
    [DllImport("combase.dll")] static extern int WindowsDeleteString(IntPtr hstring);
    [DllImport("combase.dll")] static extern IntPtr WindowsGetStringRawBuffer(IntPtr hstring, out uint length);

    delegate int GetPtr(IntPtr self, out IntPtr value);
    delegate int GetAt(IntPtr self, uint index, out IntPtr value);
    delegate int GetUInt(IntPtr self, out uint value);
    delegate int GetUShort(IntPtr self, out ushort value);
    delegate int GetInt(IntPtr self, out int value);

    static readonly Guid RawStatics = new("eb8d0792-e95a-4b19-afc7-0a59f8bf759e"), RawController = new("7cad6d91-a7e1-4f71-9a78-33e9c5dfea62"),
        RawController2 = new("43c0c035-bb73-4756-a787-3ed6bea617bd"), BatteryInfo = new("dcecc681-3963-4da6-955d-553f3b6f6161"), BatteryReport = new("c9858c3a-4e13-420a-a8d0-24f18f395401");

    // vtable slots count IInspectable's six first (QueryInterface, AddRef, Release, GetIids, GetRuntimeClassName, GetTrustLevel)
    static T Slot<T>(IntPtr obj, int index) where T : Delegate => Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(obj), index * IntPtr.Size));

    /// <summary>Every controller Windows.Gaming.Input knows about, with its battery report. Runs on its own MTA thread and
    /// waits briefly, because Windows fills the controller list a moment after it is first asked.</summary>
    public static List<Report> Read(int settleMs = 1500)
    {
        var found = new List<Report>(); Exception? failure = null;
        var thread = new Thread(() => { try { found = ReadOnThread(settleMs); } catch (Exception e) { failure = e; } }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.MTA); thread.Start();
        if (!thread.Join(settleMs + 6000)) throw new TimeoutException("Windows.Gaming.Input didn't answer.");
        if (failure != null) throw failure;
        return found;
    }

    static List<Report> ReadOnThread(int settleMs)
    {
        int init = RoInitialize(1);
        try
        {
            Marshal.ThrowExceptionForHR(WindowsCreateString("Windows.Gaming.Input.RawGameController", 38, out IntPtr cls));
            IntPtr statics;
            try { var iid = RawStatics; Marshal.ThrowExceptionForHR(RoGetActivationFactory(cls, ref iid, out statics)); }
            finally { WindowsDeleteString(cls); }
            try
            {
                var list = Controllers(statics);
                if (list.Count == 0 && settleMs > 0) { Thread.Sleep(settleMs); list = Controllers(statics); }
                return list;
            }
            finally { Marshal.Release(statics); }
        }
        finally { if (init >= 0) RoUninitialize(); }
    }

    static List<Report> Controllers(IntPtr statics)
    {
        var found = new List<Report>();
        Marshal.ThrowExceptionForHR(Slot<GetPtr>(statics, 10)(statics, out IntPtr view)); // get_RawGameControllers -> IVectorView<RawGameController>
        try
        {
            Marshal.ThrowExceptionForHR(Slot<GetUInt>(view, 7)(view, out uint count));
            for (uint i = 0; i < count; i++)
            {
                if (Slot<GetAt>(view, 6)(view, i, out IntPtr item) < 0) continue;
                try { found.Add(Describe(item)); } finally { Marshal.Release(item); }
            }
        }
        finally { Marshal.Release(view); }
        return found;
    }

    static Report Describe(IntPtr item)
    {
        ushort vid = 0, pid = 0; string name = "";
        if (QI(item, RawController) is var raw && raw != IntPtr.Zero)
            try { Slot<GetUShort>(raw, 9)(raw, out pid); Slot<GetUShort>(raw, 10)(raw, out vid); } finally { Marshal.Release(raw); }
        if (QI(item, RawController2) is var raw2 && raw2 != IntPtr.Zero)
            try { if (Slot<GetPtr>(raw2, 8)(raw2, out IntPtr h) >= 0 && h != IntPtr.Zero) { name = Marshal.PtrToStringUni(WindowsGetStringRawBuffer(h, out uint n), (int)n) ?? ""; WindowsDeleteString(h); } }
            finally { Marshal.Release(raw2); }
        IntPtr info = QI(item, BatteryInfo);
        if (info == IntPtr.Zero) return new(vid, pid, name, "no battery interface", null, null, null, null);
        try
        {
            if (Slot<GetPtr>(info, 6)(info, out IntPtr report) < 0 || report == IntPtr.Zero) return new(vid, pid, name, "no report", null, null, null, null);
            try
            {
                int? rate = Ref(report, 6), full = Ref(report, 8), remaining = Ref(report, 9);
                Slot<GetInt>(report, 10)(report, out int status);
                string s = status switch { 0 => "not present", 1 => "discharging", 2 => "idle", 3 => "charging", _ => "status " + status };
                int? percent = full is > 0 && remaining is >= 0 ? (int)Math.Round(100.0 * remaining.Value / full.Value) : null;
                return new(vid, pid, name, s, percent is > 100 ? 100 : percent, remaining, full, rate);
            }
            finally { Marshal.Release(report); }
        }
        finally { Marshal.Release(info); }
    }

    static IntPtr QI(IntPtr obj, Guid iid) => Marshal.QueryInterface(obj, in iid, out IntPtr p) >= 0 ? p : IntPtr.Zero;
    /// <summary>An IReference&lt;int&gt; property of the battery report; null when Windows has no value for it.</summary>
    static int? Ref(IntPtr report, int slot)
    {
        if (Slot<GetPtr>(report, slot)(report, out IntPtr box) < 0 || box == IntPtr.Zero) return null;
        try { return Slot<GetInt>(box, 6)(box, out int v) >= 0 ? v : null; } finally { Marshal.Release(box); }
    }
}

/// <summary>
/// Listens, without sending anything, to the controller's vendor-defined HID channels for the length of a scan, and keeps
/// what arrives unprompted: how many reports, their first byte (report ID), which byte positions changed, and a few
/// samples. A battery level a dongle or pad pushes on its own would show up here as a slowly changing byte. Only
/// ReadFile is used on these handles; no output, feature or SET request ever reaches the controller.
/// </summary>
public sealed class VendorListener : IDisposable
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern SafeFileHandle CreateFileW(string path, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    const uint GENERIC_READ = 0x80000000, OPEN_EXISTING = 3, FILE_FLAG_OVERLAPPED = 0x40000000;
    const int KeepSamples = 24;

    public sealed class Channel
    {
        public string usagePage = "", product = ""; public int inputLength, reports; public string status = "listening";
        public SortedSet<int> reportIds = new();
        public byte[]? min, max;
        public List<string> first = new(), last = new();
        public double firstAtSeconds = -1;
    }

    readonly CancellationTokenSource stop = new();
    readonly List<(Channel Info, FileStream Stream, Task Loop)> open = new();
    readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
    public readonly List<Channel> Channels = new();

    /// <summary>Opens every vendor collection of the given controller (by VID/PID) for reading.</summary>
    public static VendorListener Start(Inventory inventory, int vid, int pid)
    {
        var l = new VendorListener();
        foreach (var i in inventory.interfaces.Where(i => i.kind == "HID" && i.vendorDefined && i.inputLength > 1 && i.vid == vid && i.pid == pid && i.path.Length > 0))
        {
            var c = new Channel { usagePage = $"0x{i.usagePage:X4}/0x{i.usage:X2}", product = i.product, inputLength = i.inputLength }; l.Channels.Add(c);
            var handle = CreateFileW(i.path, GENERIC_READ, 3, IntPtr.Zero, OPEN_EXISTING, FILE_FLAG_OVERLAPPED, IntPtr.Zero);
            if (handle.IsInvalid) { c.status = $"couldn't open (Windows error {Marshal.GetLastWin32Error()})"; handle.Dispose(); continue; }
            var stream = new FileStream(handle, FileAccess.Read, 0, true);
            l.open.Add((c, stream, l.Loop(c, stream)));
        }
        return l;
    }

    async Task Loop(Channel c, FileStream stream)
    {
        var buffer = new byte[c.inputLength];
        try
        {
            while (!stop.IsCancellationRequested)
            {
                // off the UI thread: Finish waits for these loops, so a UI continuation would stall the window until its timeout
                int n = await stream.ReadAsync(buffer.AsMemory(0, c.inputLength), stop.Token).ConfigureAwait(false);
                if (n <= 0) { c.status = "closed by Windows"; return; }
                lock (c)
                {
                    if (c.reports++ == 0) c.firstAtSeconds = Math.Round(clock.Elapsed.TotalSeconds, 2);
                    c.reportIds.Add(buffer[0]);
                    c.min ??= buffer[..n].ToArray(); c.max ??= buffer[..n].ToArray();
                    for (int k = 0; k < n && k < c.min.Length; k++) { c.min[k] = Math.Min(c.min[k], buffer[k]); c.max[k] = Math.Max(c.max[k], buffer[k]); }
                    string hex = $"{clock.Elapsed.TotalSeconds:0.00}s {Convert.ToHexString(buffer, 0, n)}";
                    if (c.first.Count < KeepSamples) c.first.Add(hex); else { c.last.Add(hex); if (c.last.Count > KeepSamples) c.last.RemoveAt(0); }
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (IOException e) { c.status = "read stopped: " + e.Message; }
        catch (UnauthorizedAccessException e) { c.status = "read refused: " + e.Message; }
        if (c.status == "listening") c.status = "listened";
    }

    /// <summary>Stops listening and returns a plain summary per channel for the report.</summary>
    public List<object> Finish()
    {
        double seconds = Math.Round(clock.Elapsed.TotalSeconds, 1);
        stop.Cancel();
        try { Task.WaitAll(open.Select(o => o.Loop).ToArray(), 2000); } catch (AggregateException) { }
        Dispose();
        return Channels.Select(c => { lock (c) return (object)new {
            channel = c.usagePage, c.product, c.inputLength, c.status, listenedSeconds = seconds, c.reports, c.firstAtSeconds,
            reportIds = c.reportIds.Select(id => $"0x{id:X2}").ToList(),
            changingBytes = c.min == null ? new List<object>() : Enumerable.Range(0, c.min.Length).Where(k => c.min[k] != c.max![k]).Select(k => (object)new { offset = k, min = c.min[k], max = c.max![k] }).ToList(),
            firstReports = c.first, lastReports = c.last }; }).ToList();
    }

    public void Dispose()
    {
        if (!stop.IsCancellationRequested) stop.Cancel();
        foreach (var o in open) o.Stream.Dispose();
        open.Clear();
    }
}
