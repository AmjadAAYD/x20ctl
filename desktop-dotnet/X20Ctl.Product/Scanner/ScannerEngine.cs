using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;
namespace X20Ctl.Product.Scanner;

// Controller Scanner 2.0.0-local, carried into X20CTL as its "Controller not working?" check (10 Oct 2026).
// The capture engine is the preserved 2.0.0 one (artifacts/scanner-2.0.0-preservation-20261008T185956Z, see
// docs/plans/scanner-2.0.0-preservation.md): documented XInput on all four slots, the allowlisted reader DLLs,
// press/release tracking, the guided-action analysis, SetupAPI/HID inventory, the Raw Input fallback and the
// hashed local export. Only the hosting changed (WPF instead of WinForms, System.Text.Json instead of
// JavaScriptSerializer). Everything stays read-only: no XInputSetState, no writes, no upload.

public sealed class PressEdge
{
    public string control = ""; public bool down; public double duration_ms;
}

public sealed class PressTracker
{
    readonly Dictionary<string, double> started = new(), last = new();
    public string HeldText = "None", LastRelease = "No completed press yet";
    public IReadOnlyCollection<string> Held => started.Keys;
    public List<PressEdge> Update(State state, double now)
    {
        var pressed = new HashSet<string>(Analysis.Masks.Where(x => (state.Pad.Buttons & x.Value) == x.Value).Select(x => x.Key));
        if (state.Pad.LT > 0) pressed.Add("LT"); if (state.Pad.RT > 0) pressed.Add("RT");
        var edges = new List<PressEdge>();
        foreach (string key in started.Keys.ToArray()) if (!pressed.Contains(key))
        {
            double duration = Math.Max(0, now - started[key]); started.Remove(key); last[key] = duration;
            edges.Add(new PressEdge { control = key, down = false, duration_ms = duration });
            LastRelease = key + " released after " + (duration / 1000).ToString("0.00") + " s";
        }
        foreach (string key in pressed.OrderBy(x => x)) if (!started.ContainsKey(key)) { started[key] = now; edges.Add(new PressEdge { control = key, down = true, duration_ms = 0 }); }
        HeldText = started.Count == 0 ? "None" : string.Join("  ", started.Select(x => x.Key + " " + ((now - x.Value) / 1000).ToString("0.00") + " s"));
        return edges;
    }
    /// <summary>How long a control has been held right now, or null when it is up.</summary>
    public double? HeldFor(string control, double now) => started.TryGetValue(control, out var t) ? Math.Max(0, now - t) : null;
    /// <summary>The duration of the last completed press of a control, if any.</summary>
    public double? LastFor(string control) => last.TryGetValue(control, out var d) ? d : null;
    public void Disconnect() { started.Clear(); HeldText = "Disconnected"; LastRelease = "Connection lost; unfinished holds are inconclusive"; }
    public string Duration(string control, double now) =>
        started.ContainsKey(control) ? "PRESSED  " + Math.Max(0, now - started[control]).ToString("0") + " ms" :
        last.ContainsKey(control) ? "Last: " + last[control].ToString("0") + " ms" : "Released";
}

[StructLayout(LayoutKind.Sequential)] public struct Pad { public ushort Buttons; public byte LT, RT; public short LX, LY, RX, RY; }
[StructLayout(LayoutKind.Sequential)] public struct State { public uint Packet; public Pad Pad; }
[StructLayout(LayoutKind.Sequential)] public struct Capabilities { public byte type, subtype; public ushort flags; public Pad pad; public ushort leftMotor, rightMotor; }

public sealed class Sample
{
    public string timestamp, action, source = "xinput_state";
    public double elapsed_ms;
    public Dictionary<string, object> values;
    public Sample(string label, int slot, State state, double elapsed)
    {
        timestamp = DateTime.UtcNow.ToString("o"); action = label; elapsed_ms = elapsed;
        values = new() { { "slot", slot }, { "packet", state.Packet }, { "buttons", state.Pad.Buttons }, { "lt", state.Pad.LT }, { "rt", state.Pad.RT },
            { "lx", state.Pad.LX }, { "ly", state.Pad.LY }, { "rx", state.Pad.RX }, { "ry", state.Pad.RY } };
    }
    public int Get(string key) => Convert.ToInt32(values[key]);
}

public static class Analysis
{
    public static readonly Dictionary<string, int> Masks = new()
    {
        { "A", 4096 }, { "B", 8192 }, { "X", 16384 }, { "Y", 32768 }, { "LB", 256 }, { "RB", 512 },
        { "Start", 16 }, { "Back", 32 }, { "L3", 64 }, { "R3", 128 }, { "dpad_up", 1 }, { "dpad_down", 2 },
        { "dpad_left", 4 }, { "dpad_right", 8 }, { "dpad_up_right", 9 }, { "dpad_down_right", 10 },
        { "dpad_down_left", 6 }, { "dpad_up_left", 5 }
    };
    public static bool Cycle(List<Sample> rows, string key, int mask)
    {
        bool off = false, on = false;
        foreach (var row in rows)
        {
            int v = row.Get(key); bool active = mask == 0 ? v > 30 : (v & mask) == mask;
            bool released = mask == 0 ? v == 0 : (v & mask) == 0;
            if (released) { if (on) return true; off = true; }
            else if (active && off) on = true;
        }
        return false;
    }
    public static bool Verify(List<Sample> rows) =>
        rows.Count >= 3 && rows.Select(x => x.Get("slot")).Distinct().Count() == 1
        && rows.Any(x => x.Get("buttons") == 0 && x.Get("lt") == 0 && x.Get("rt") == 0)
        && Cycle(rows, "buttons", 4096) && Cycle(rows, "lt", 0) && Cycle(rows, "rt", 0);
    public static Dictionary<string, object> Summary(string action, List<Sample> rows)
    {
        var result = new Dictionary<string, object> { { "action", action }, { "samples", rows.Count }, { "status", "no_samples" }, { "configuration_support_verified", false } };
        if (rows.Count == 0) return result;
        if (rows.Select(x => x.Get("slot")).Distinct().Count() != 1) { result["status"] = "inconclusive_source_changed"; return result; }
        var ranges = new Dictionary<string, object>();
        foreach (string key in new[] { "buttons", "lt", "rt", "lx", "ly", "rx", "ry" }) ranges[key] = new { min = rows.Min(x => x.Get(key)), max = rows.Max(x => x.Get(key)) };
        result["ranges"] = ranges;
        var intervals = rows.Zip(rows.Skip(1), (a, b) => b.elapsed_ms - a.elapsed_ms).Where(x => x > 0).OrderBy(x => x).ToArray();
        if (intervals.Length > 0) result["hostSampleIntervalMs"] = new { min = intervals[0], median = intervals[intervals.Length / 2], max = intervals[^1], hardwarePollingRateVerified = false };
        bool observed;
        if (action == "preflight") observed = Verify(rows);
        else if (action == "neutral") observed = rows.All(x => x.Get("buttons") == 0 && x.Get("lt") == 0 && x.Get("rt") == 0);
        else if (Masks.ContainsKey(action)) observed = Cycle(rows, "buttons", Masks[action]);
        else if (action is "LT" or "RT") observed = Cycle(rows, action.ToLowerInvariant(), 0);
        else if (action is "rear_left" or "rear_right" or "turbo") observed = Masks.Values.Any(mask => Cycle(rows, "buttons", mask)) || Cycle(rows, "lt", 0) || Cycle(rows, "rt", 0);
        else if (action is "left_stick" or "right_stick")
        {
            string x = action == "left_stick" ? "lx" : "rx", y = action == "left_stick" ? "ly" : "ry";
            // Above both SDK stick deadzones; small centre noise is not a stick sweep.
            observed = rows.Any(v => Math.Abs(v.Get(x)) > 8689 || Math.Abs(v.Get(y)) > 8689);
        }
        else observed = false;
        result["status"] = observed ? "requested_control_observed" : "inconclusive_requested_control_not_observed";
        if (action is "rear_left" or "rear_right" or "turbo") { result["status"] = observed ? "ordinary_output_observed" : "inconclusive_no_ordinary_output"; result["independent_rear_control_verified"] = false; }
        result["interpretation"] = "Standard logical input observed in this run; not a sensor, calibration, physical model or configuration verdict.";
        return result;
    }
}

public interface IReader : IDisposable { bool Read(int slot, out State state); string Identity { get; } }

public sealed class WindowsReader : IReader
{
    public static readonly string[] Libraries = ["XInput1_4.dll", "XInput9_1_0.dll", "XInput1_3.dll"];
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate uint GetState(uint slot, out State state);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate uint GetCapabilities(uint slot, uint flags, out Capabilities caps);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate uint GetBattery(uint slot, byte devType, out BatteryInformation info);
    [StructLayout(LayoutKind.Sequential)] public struct BatteryInformation { public byte type, level; }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern IntPtr LoadLibraryW(string path);
    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)] static extern IntPtr GetProcAddress(IntPtr module, string name);
    [DllImport("kernel32.dll")] static extern bool FreeLibrary(IntPtr module);
    IntPtr module; readonly GetState function; readonly GetCapabilities? capabilities; readonly GetBattery? battery;
    public string Identity { get; }
    public WindowsReader(string library)
    {
        if (!Libraries.Contains(library)) throw new ArgumentException("Unknown Windows input reader");
        Identity = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), library);
        module = LoadLibraryW(Identity);
        if (module == IntPtr.Zero) throw new IOException("This Windows input reader is unavailable. Choose another reader.");
        IntPtr address = GetProcAddress(module, "XInputGetState");
        if (address == IntPtr.Zero) { Dispose(); throw new IOException("Documented input function unavailable."); }
        function = Marshal.GetDelegateForFunctionPointer<GetState>(address);
        address = GetProcAddress(module, "XInputGetCapabilities");
        if (address != IntPtr.Zero) capabilities = Marshal.GetDelegateForFunctionPointer<GetCapabilities>(address);
        address = GetProcAddress(module, "XInputGetBatteryInformation");
        if (address != IntPtr.Zero) battery = Marshal.GetDelegateForFunctionPointer<GetBattery>(address);
    }
    /// <summary>Documented, read-only XInputGetBatteryInformation for the gamepad: what the driver reports, not a
    /// measured voltage. Wired pads and many receivers report "wired" or "unknown".</summary>
    public (string Type, string Level, bool Known) Battery(int slot)
    {
        if (battery == null || module == IntPtr.Zero) return ("unavailable in this reader", "unavailable", false);
        if (battery((uint)slot, 0, out var info) != 0) return ("not reported", "not reported", false);
        string type = info.type switch { 0 => "disconnected", 1 => "wired", 2 => "alkaline", 3 => "NiMH", 0xFF => "unknown", _ => "type " + info.type };
        string level = info.type is 1 ? "wired (no battery level)" : info.level switch { 0 => "empty", 1 => "low", 2 => "medium", 3 => "full", _ => "level " + info.level };
        return (type, level, info.type is 2 or 3);
    }
    public bool Read(int slot, out State state) { if (module == IntPtr.Zero) { state = default; return false; } return function((uint)slot, out state) == 0; }
    public object ReportedCapabilities(int slot)
    {
        if (capabilities == null || capabilities((uint)slot, 1, out var caps) != 0) return new { slot, status = "unavailable" };
        return new { slot, status = "driver_reported_not_physical_acceptance", type = caps.type, subtype = caps.subtype, flags = caps.flags,
            standardButtons = caps.pad.Buttons, leftTrigger = caps.pad.LT, rightTrigger = caps.pad.RT, leftMotor = caps.leftMotor, rightMotor = caps.rightMotor,
            motorResponseVerified = false, configurationProtocolVerified = false };
    }
    public void Dispose() { if (module != IntPtr.Zero) { FreeLibrary(module); module = IntPtr.Zero; } }
}

public sealed class Evidence
{
    public static readonly JsonSerializerOptions Json = new() { IncludeFields = true };
    public readonly Dictionary<string, List<Sample>> Records = new();
    public readonly List<object> Events = new();
    public string Model = "Unknown", Transport = "unknown", Reader = "unknown"; public int Slot;
    public Inventory Inventory = new();
    public readonly List<object> XInputCapabilities = new();
    public readonly List<object> RawRecords = new();
    int rawBytes;
    public bool AddRaw(object row)
    {
        int size;
        try { size = Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(row, Json)); } catch (NotSupportedException) { return false; }
        if (RawRecords.Count >= 10000 || rawBytes + size > 4 * 1024 * 1024) return false;
        RawRecords.Add(row); rawBytes += size; return true;
    }
    public bool Verified;
    /// <summary>Extra files written into the report folder (and so into its manifest), e.g. the full scan's findings.</summary>
    public readonly Dictionary<string, string> Extra = new();
    public void Add(string action, Sample row)
    {
        if (!Records.ContainsKey(action)) Records[action] = new();
        if (Records.Values.Sum(x => x.Count) >= 30000) throw new IOException("Capture limit reached; save these results.");
        Records[action].Add(row);
    }
    public void Event(string status, string reason) => Events.Add(new { timestamp = DateTime.UtcNow.ToString("o"), status, reason });
    public string Export(string parent)
    {
        string id = "input-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..8];
        string folder = Path.Combine(parent, id); Directory.CreateDirectory(folder);
        var utf8 = new UTF8Encoding(false);
        foreach (var entry in Records)
        {
            if (!new[] { "preflight", "neutral", "LT", "RT", "left_stick", "right_stick", "rear_left", "rear_right", "turbo" }.Contains(entry.Key) && !Analysis.Masks.ContainsKey(entry.Key))
                throw new IOException("Unknown evidence action");
            File.WriteAllLines(Path.Combine(folder, entry.Key + ".jsonl"), entry.Value.Select(x => JsonSerializer.Serialize(x, Json)), utf8);
        }
        File.WriteAllText(Path.Combine(folder, "metadata.json"), JsonSerializer.Serialize(new
        {
            collectorVersion = "2.0.0-local", collectorHost = "x20ctl-native-controller-check", claimedModel = Model, claimedTransport = Transport,
            modelDetected = false, source = "xinput_state", slot = Slot, reader = Reader, preflightPassed = Verified,
            configurationWrites = false, automaticUpload = false, hostTimingIsPollingRate = false,
            sessions = Records.Select(x => Analysis.Summary(x.Key, x.Value)).ToList(), events = Events,
            rawInputReports = RawRecords.Count, rawInputAssociationWithSelectedSlot = "unverified"
        }, Json), utf8);
        if (XInputCapabilities.Count > 0) File.WriteAllText(Path.Combine(folder, "xinput-capabilities.json"), JsonSerializer.Serialize(XInputCapabilities, Json), utf8);
        File.WriteAllText(Path.Combine(folder, "windows-inventory.json"), JsonSerializer.Serialize(Inventory, Json), utf8);
        if (RawRecords.Count > 0) File.WriteAllLines(Path.Combine(folder, "raw-input.jsonl"), RawRecords.Select(x => JsonSerializer.Serialize(x, Json)), utf8);
        foreach (var (name, text) in Extra) if (Regex.IsMatch(name, @"^[a-z0-9-]+\.json$")) File.WriteAllText(Path.Combine(folder, name), text, utf8);
        File.WriteAllText(Path.Combine(folder, "README.txt"),
            "Local input evidence only. Model/transport are owner claims. No HID decoding, configuration, vibration, firmware, or upload.\r\n"
            + "Incomplete checks are retained. Review files before sharing. Host samples are not polling-rate or latency measurements.\r\n");
        var files = Directory.GetFiles(folder).OrderBy(x => x, StringComparer.Ordinal).Select(path => new { path = Path.GetFileName(path), size = new FileInfo(path).Length, sha256 = Hash(File.ReadAllBytes(path)) }).ToArray();
        File.WriteAllText(Path.Combine(folder, "manifest.json"), JsonSerializer.Serialize(new { schema = "input-diagnostic/1", files }, Json), utf8);
        string archive = folder + ".zip"; ZipFile.CreateFromDirectory(folder, archive); return archive;
    }
    public static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}

public sealed class InterfaceInfo
{
    public string kind = "", path = "", container = "", product = "", manufacturer = "", hardwareIds = "", status = "", serial = "Not collected";
    public int vid, pid, usagePage, usage, inputLength, outputLength, featureLength;
    /// <summary>Vendor-defined collections (usage page 0xFF00 and up) are where a configuration channel would live.</summary>
    public bool vendorDefined;
    /// <summary>Each declared input value (stick, trigger, hat): its usage, bit size and logical range, from the parsed descriptor.</summary>
    public List<Dictionary<string, object>> axes = new();
}

public sealed class Inventory
{
    public List<InterfaceInfo> interfaces = new();
    public List<object> rawInput = new();
    public List<string> errors = new();
    public string transportEvidence = "Unknown - bus visibility does not establish cable versus receiver";
    public string originalReportDescriptors = "Unavailable - parsed HID capabilities are not original descriptor bytes";
    public string directInput = "Not enumerated separately; no DirectInput capability claim";
    public string ble = "Not queried; GATT service discovery is an optional separate research capture";
    public static List<List<InterfaceInfo>> Group(IEnumerable<InterfaceInfo> rows) =>
        rows.GroupBy(x => string.IsNullOrEmpty(x.container) ? "interface:" + x.path : "container:" + x.container).Select(x => x.ToList()).ToList();
    public static async Task<Inventory> Bound(Task<Inventory> work, int timeoutMs)
    {
        if (await Task.WhenAny(work, Task.Delay(timeoutMs)) != work)
        {
            var result = new Inventory(); result.errors.Add("Windows enumeration exceeded its time limit. Live XInput testing remains available; full inventory is unavailable in this run."); return result;
        }
        return await work;
    }
}

public static class WindowsInventory
{
    [StructLayout(LayoutKind.Sequential)] struct DevInfo { public int size; public Guid guid; public uint instance; public IntPtr reserved; }
    [StructLayout(LayoutKind.Sequential)] struct DevInterface { public int size; public Guid guid; public int flags; public IntPtr reserved; }
    [StructLayout(LayoutKind.Sequential)] struct PropertyKey { public Guid format; public uint id; }
    [StructLayout(LayoutKind.Sequential)] struct Attributes { public int size; public ushort vid, pid, version; }
    [StructLayout(LayoutKind.Sequential)] struct Caps { public ushort usage, page, input, output, feature; [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)] public ushort[] reserved; [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)] public ushort[] counts; }
    [StructLayout(LayoutKind.Sequential)] struct RawDevice { public IntPtr handle; public uint type; }
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern IntPtr SetupDiGetClassDevsW(ref Guid guid, IntPtr enumerator, IntPtr hwnd, uint flags);
    [DllImport("setupapi.dll", SetLastError = true)] static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr info, ref Guid guid, uint index, ref DevInterface item);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool SetupDiGetDeviceInterfaceDetailW(IntPtr set, ref DevInterface item, IntPtr detail, uint size, out uint needed, ref DevInfo info);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool SetupDiGetDeviceRegistryPropertyW(IntPtr set, ref DevInfo info, uint property, out uint type, byte[] value, uint size, out uint required);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool SetupDiGetDevicePropertyW(IntPtr set, ref DevInfo info, ref PropertyKey key, out uint type, byte[] value, uint size, out uint required, uint flags);
    [DllImport("setupapi.dll")] static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern SafeFileHandle CreateFileW(string path, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("hid.dll")] static extern bool HidD_GetAttributes(SafeFileHandle handle, ref Attributes attrs);
    [DllImport("hid.dll")] static extern bool HidD_GetPreparsedData(SafeFileHandle handle, out IntPtr data);
    [DllImport("hid.dll")] static extern bool HidD_FreePreparsedData(IntPtr data);
    [DllImport("hid.dll")] static extern int HidP_GetCaps(IntPtr data, out Caps caps);
    [DllImport("hid.dll")] static extern int HidP_GetValueCaps(int type, [Out] byte[] caps, ref ushort length, IntPtr data);
    static readonly Dictionary<int, string> AxisNames = new() { [0x30] = "X", [0x31] = "Y", [0x32] = "Z", [0x33] = "Rx", [0x34] = "Ry", [0x35] = "Rz", [0x36] = "Slider", [0x37] = "Dial", [0x39] = "Hat switch" };
    [DllImport("hid.dll")] static extern bool HidD_GetProductString(SafeFileHandle handle, byte[] buffer, int length);
    [DllImport("hid.dll")] static extern bool HidD_GetManufacturerString(SafeFileHandle handle, byte[] buffer, int length);
    [DllImport("hid.dll")] static extern bool HidD_GetSerialNumberString(SafeFileHandle handle, byte[] buffer, int length);
    [DllImport("user32.dll")] static extern uint GetRawInputDeviceList([Out] RawDevice[]? devices, ref uint count, uint size);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern uint GetRawInputDeviceInfoW(IntPtr handle, uint command, IntPtr data, ref uint size);
    static string Text(byte[] bytes) => Encoding.Unicode.GetString(bytes).TrimEnd('\0');
    static string Property(IntPtr set, ref DevInfo info, uint property)
    {
        var bytes = new byte[8192];
        return SetupDiGetDeviceRegistryPropertyW(set, ref info, property, out _, bytes, (uint)bytes.Length, out uint size) ? Encoding.Unicode.GetString(bytes, 0, (int)Math.Min(size, (uint)bytes.Length)).TrimEnd('\0') : "Unknown";
    }
    public static Inventory Read(bool includeSerial = false)
    {
        var result = new Inventory();
        foreach (var (kind, id) in new[] { ("HID", "4d1e55b2-f16f-11cf-88cb-001111000030"), ("USB", "a5dcbf10-6530-11d2-901f-00a0c906bed8") })
        {
            Guid guid = new(id); IntPtr set = SetupDiGetClassDevsW(ref guid, IntPtr.Zero, IntPtr.Zero, 18);
            if (set == new IntPtr(-1)) { result.errors.Add(kind + " inventory unavailable"); continue; }
            try
            {
                for (uint index = 0; index < 1024; index++)
                {
                    var item = new DevInterface { size = Marshal.SizeOf<DevInterface>() };
                    if (!SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref guid, index, ref item)) break;
                    var info = new DevInfo { size = Marshal.SizeOf<DevInfo>() };
                    SetupDiGetDeviceInterfaceDetailW(set, ref item, IntPtr.Zero, 0, out uint size, ref info);
                    if (size < 8 || size > 65536) continue;
                    IntPtr buffer = Marshal.AllocHGlobal((int)size);
                    try
                    {
                        Marshal.WriteInt32(buffer, IntPtr.Size == 8 ? 8 : 6);
                        if (!SetupDiGetDeviceInterfaceDetailW(set, ref item, buffer, size, out size, ref info)) continue;
                        string path = Marshal.PtrToStringUni(IntPtr.Add(buffer, 4)) ?? "";
                        var row = new InterfaceInfo { kind = kind, path = path, status = "metadata_only", product = Property(set, ref info, 12), manufacturer = Property(set, ref info, 11), hardwareIds = Property(set, ref info, 1) };
                        if (row.product == "Unknown") row.product = Property(set, ref info, 0);
                        var key = new PropertyKey { format = new Guid("8c7ed206-3f8a-4827-b3ab-ae9e1faefc6c"), id = 2 }; var bytes = new byte[16];
                        if (SetupDiGetDevicePropertyW(set, ref info, ref key, out _, bytes, 16, out uint needed, 0) && needed == 16) { Guid container = new(bytes); if (container != Guid.Empty) row.container = container.ToString(); }
                        // zero requested access and shared handles: metadata only
                        if (kind == "HID") using (var handle = CreateFileW(path, 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero))
                        {
                            if (!handle.IsInvalid)
                            {
                                var attrs = new Attributes { size = Marshal.SizeOf<Attributes>() };
                                if (HidD_GetAttributes(handle, ref attrs)) { row.vid = attrs.vid; row.pid = attrs.pid; }
                                var text = new byte[512]; if (HidD_GetProductString(handle, text, text.Length)) row.product = Text(text);
                                text = new byte[512]; if (HidD_GetManufacturerString(handle, text, text.Length)) row.manufacturer = Text(text);
                                if (includeSerial) { text = new byte[512]; row.serial = HidD_GetSerialNumberString(handle, text, text.Length) ? Text(text) : "Unknown"; }
                                if (HidD_GetPreparsedData(handle, out IntPtr data)) try
                                {
                                    if (HidP_GetCaps(data, out Caps caps) >= 0)
                                    {
                                        row.usagePage = caps.page; row.usage = caps.usage; row.inputLength = caps.input; row.outputLength = caps.output; row.featureLength = caps.feature; row.status = "parsed_caps";
                                        row.vendorDefined = caps.page >= 0xFF00;
                                        // HIDP_VALUE_CAPS is 72 bytes: usage page @0, bit size @18, report count @20, logical min/max @40/44, usage @56
                                        ushort count = caps.counts is { Length: > 2 } ? caps.counts[2] : (ushort)0;
                                        if (count is > 0 and < 128)
                                        {
                                            var valueCaps = new byte[72 * count];
                                            if (HidP_GetValueCaps(0, valueCaps, ref count, data) == 0x110000)
                                                for (int v = 0; v < count; v++)
                                                {
                                                    int at = v * 72, page = BitConverter.ToUInt16(valueCaps, at), usage = BitConverter.ToUInt16(valueCaps, at + 56);
                                                    row.axes.Add(new() { ["usagePage"] = page, ["usage"] = usage, ["name"] = page == 1 && AxisNames.TryGetValue(usage, out var n) ? n : $"0x{page:X4}/0x{usage:X2}",
                                                        ["bits"] = BitConverter.ToUInt16(valueCaps, at + 18), ["reportCount"] = BitConverter.ToUInt16(valueCaps, at + 20), ["logicalMin"] = BitConverter.ToInt32(valueCaps, at + 40), ["logicalMax"] = BitConverter.ToInt32(valueCaps, at + 44) });
                                                }
                                        }
                                    }
                                } finally { HidD_FreePreparsedData(data); }
                            }
                            else row.status = "metadata_access_unavailable";
                        }
                        var ids = Regex.Match(row.hardwareIds ?? "", @"VID_([0-9A-F]{4}).*PID_([0-9A-F]{4})", RegexOptions.IgnoreCase);
                        if (ids.Success && row.vid == 0) { row.vid = Convert.ToInt32(ids.Groups[1].Value, 16); row.pid = Convert.ToInt32(ids.Groups[2].Value, 16); }
                        result.interfaces.Add(row);
                    }
                    finally { Marshal.FreeHGlobal(buffer); }
                }
            }
            finally { SetupDiDestroyDeviceInfoList(set); }
        }
        // Retain only controller-related HID/USB interfaces, never keyboard/mouse input.
        var controllerContainers = new HashSet<string>(result.interfaces.Where(x =>
            (x.kind == "HID" && x.usagePage == 1 && new[] { 4, 5, 8 }.Contains(x.usage)) ||
            (x.product ?? "").Contains("Controller", StringComparison.OrdinalIgnoreCase)).Select(x => x.container).Where(x => !string.IsNullOrEmpty(x)));
        result.interfaces = result.interfaces.Where(x => controllerContainers.Contains(x.container ?? "") ||
            (x.kind == "HID" && x.usagePage == 1 && new[] { 4, 5, 8 }.Contains(x.usage)) ||
            (x.product ?? "").Contains("Controller", StringComparison.OrdinalIgnoreCase)).ToList();
        // A shared Windows container groups interfaces; it does not associate an XInput slot or verify the printed model.
        ReadRaw(result);
        return result;
    }
    static void ReadRaw(Inventory result)
    {
        uint count = 0, entrySize = (uint)Marshal.SizeOf<RawDevice>();
        if (GetRawInputDeviceList(null, ref count, entrySize) == uint.MaxValue || count > 4096) { result.errors.Add("Raw Input inventory unavailable"); return; }
        var entries = new RawDevice[count]; uint found = GetRawInputDeviceList(entries, ref count, entrySize);
        if (found == uint.MaxValue) { result.errors.Add("Raw Input inventory changed; retry detection"); return; }
        foreach (var entry in entries.Take((int)found).Where(x => x.type == 2))
        {
            uint length = 0; GetRawInputDeviceInfoW(entry.handle, 0x20000007, IntPtr.Zero, ref length);
            if (length == 0 || length > 32768) continue;
            IntPtr name = Marshal.AllocHGlobal((int)(length + 1) * 2), detail = Marshal.AllocHGlobal(32);
            try
            {
                if (GetRawInputDeviceInfoW(entry.handle, 0x20000007, name, ref length) == uint.MaxValue) continue;
                uint bytes = 32; Marshal.WriteInt32(detail, 32);
                if (GetRawInputDeviceInfoW(entry.handle, 0x2000000b, detail, ref bytes) == uint.MaxValue) continue;
                int page = (ushort)Marshal.ReadInt16(detail, 20), usage = (ushort)Marshal.ReadInt16(detail, 22);
                if (page != 1 || !new[] { 4, 5, 8 }.Contains(usage)) continue;
                result.rawInput.Add(new { path = Marshal.PtrToStringUni(name), vid = Marshal.ReadInt32(detail, 8), pid = Marshal.ReadInt32(detail, 12), usagePage = page, usage, status = "enumerated_not_linked_to_xinput_slot" });
            }
            finally { Marshal.FreeHGlobal(name); Marshal.FreeHGlobal(detail); }
        }
    }
}

public sealed class RawDecoded
{
    public string source = "windows_raw_input", path = "", status = "";
    public List<int> buttons = new(); public Dictionary<string, uint> axes = new();
    public string? report_hex;
}

/// <summary>The focused, explicitly started Raw Input stream (Generic Desktop 4/5/8 only), fed by a WPF window hook.</summary>
public sealed class RawObserver : IDisposable
{
    [StructLayout(LayoutKind.Sequential)] struct Registration { public ushort page, usage; public uint flags; public IntPtr target; }
    [DllImport("user32.dll", SetLastError = true)] static extern bool RegisterRawInputDevices(Registration[] devices, uint count, uint size);
    [DllImport("user32.dll", SetLastError = true)] static extern uint GetRawInputData(IntPtr handle, uint command, IntPtr data, ref uint size, uint headerSize);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern uint GetRawInputDeviceInfoW(IntPtr handle, uint command, IntPtr data, ref uint size);
    [DllImport("hid.dll")] static extern int HidP_GetUsages(int type, ushort page, ushort link, [Out] ushort[] usages, ref uint count, IntPtr preparsed, byte[] report, uint length);
    [DllImport("hid.dll")] static extern int HidP_GetUsageValue(int type, ushort page, ushort link, ushort usage, out uint value, IntPtr preparsed, byte[] report, uint length);
    bool registered;
    public static bool ValidReports(int stride, int count, uint length, uint header) =>
        stride >= 1 && stride <= 4096 && count >= 1 && count <= 1024 && length >= header + 8 && (long)stride * count <= length - header - 8;
    public RawObserver(IntPtr target)
    {
        var items = new ushort[] { 4, 5, 8 }.Select(x => new Registration { page = 1, usage = x, target = target }).ToArray();
        if (!RegisterRawInputDevices(items, (uint)items.Length, (uint)Marshal.SizeOf<Registration>())) throw new IOException("Raw Input registration unavailable."); registered = true;
    }
    public List<RawDecoded> Read(IntPtr message, bool includeBytes)
    {
        uint length = 0, header = (uint)(8 + IntPtr.Size * 2);
        if (GetRawInputData(message, 0x10000003, IntPtr.Zero, ref length, header) == uint.MaxValue || length < header + 8 || length > 65536) return new();
        IntPtr buffer = Marshal.AllocHGlobal((int)length);
        try
        {
            if (GetRawInputData(message, 0x10000003, buffer, ref length, header) == uint.MaxValue || Marshal.ReadInt32(buffer) != 2) return new();
            IntPtr device = Marshal.ReadIntPtr(buffer, 8); int stride = Marshal.ReadInt32(buffer, (int)header), count = Marshal.ReadInt32(buffer, (int)header + 4);
            if (!ValidReports(stride, count, length, header)) return new();
            uint chars = 0; GetRawInputDeviceInfoW(device, 0x20000007, IntPtr.Zero, ref chars); if (chars == 0 || chars > 32768) return new();
            IntPtr name = Marshal.AllocHGlobal((int)(chars + 1) * 2); string path;
            try { if (GetRawInputDeviceInfoW(device, 0x20000007, name, ref chars) == uint.MaxValue) return new(); path = Marshal.PtrToStringUni(name) ?? ""; } finally { Marshal.FreeHGlobal(name); }
            uint capSize = 0; GetRawInputDeviceInfoW(device, 0x20000005, IntPtr.Zero, ref capSize);
            if (capSize == 0 || capSize > 65536) return new();
            IntPtr caps = Marshal.AllocHGlobal((int)capSize);
            try
            {
                if (GetRawInputDeviceInfoW(device, 0x20000005, caps, ref capSize) == uint.MaxValue) return new();
                var results = new List<RawDecoded>();
                for (int i = 0; i < count; i++)
                {
                    byte[] bytes = new byte[stride]; Marshal.Copy(IntPtr.Add(buffer, (int)header + 8 + i * stride), bytes, 0, stride);
                    var row = new RawDecoded { path = path, status = "descriptor_declared_usages_only", report_hex = includeBytes ? Convert.ToHexString(bytes).ToLowerInvariant() : null };
                    uint usagesCount = 128; var usages = new ushort[128];
                    if (HidP_GetUsages(0, 9, 0, usages, ref usagesCount, caps, bytes, (uint)stride) == 0x110000) row.buttons = usages.Take((int)Math.Min(usagesCount, 128)).Select(x => (int)x).ToList();
                    foreach (ushort usage in new ushort[] { 0x30, 0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x39 })
                        if (HidP_GetUsageValue(0, 1, 0, usage, out uint value, caps, bytes, (uint)stride) == 0x110000) row.axes["usage_" + usage.ToString("X2")] = value;
                    results.Add(row);
                }
                return results;
            }
            finally { Marshal.FreeHGlobal(caps); }
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }
    public void Dispose()
    {
        if (!registered) return;
        var items = new ushort[] { 4, 5, 8 }.Select(x => new Registration { page = 1, usage = x, flags = 1, target = IntPtr.Zero }).ToArray();
        RegisterRawInputDevices(items, (uint)items.Length, (uint)Marshal.SizeOf<Registration>()); registered = false;
    }
}
