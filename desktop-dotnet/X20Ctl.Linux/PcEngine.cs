using System.Runtime.InteropServices;
using System.Text;
using X20Ctl.Zone.PcMode;
namespace X20Ctl.Linux;

/// <summary>Raw Linux calls the input engine needs: no packages, the kernel's own interfaces.</summary>
internal static partial class Native
{
    [LibraryImport("libc", EntryPoint = "open", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)] public static partial int Open(string path, int flags);
    [LibraryImport("libc", EntryPoint = "close")] public static partial int Close(int fd);
    [LibraryImport("libc", EntryPoint = "read", SetLastError = true)] public static unsafe partial nint Read(int fd, byte* buffer, nuint count);
    [LibraryImport("libc", EntryPoint = "write", SetLastError = true)] public static unsafe partial nint Write(int fd, byte* buffer, nuint count);
    [LibraryImport("libc", EntryPoint = "ioctl", SetLastError = true)] public static unsafe partial int Ioctl(int fd, nuint request, byte* arg);
    [LibraryImport("libc", EntryPoint = "ioctl", SetLastError = true)] public static partial int IoctlInt(int fd, nuint request, int arg);
    [LibraryImport("libc", EntryPoint = "poll", SetLastError = true)] public static partial int Poll(ref PollFd fds, nuint count, int timeoutMs);

    [StructLayout(LayoutKind.Sequential)] public struct PollFd { public int Fd; public short Events; public short Revents; }
    public const int ReadOnly = 0, WriteOnly = 1, NonBlock = 0x800, PollIn = 1;
    public const int EAGAIN = 11;

    // Linux ioctl numbers: direction, size, type, number
    public static nuint Ioc(int dir, char kind, int nr, int size) => (nuint)(uint)((dir << 30) | (size << 16) | (kind << 8) | nr);

    public static unsafe int IoctlBuf(int fd, nuint request, byte[] buffer) { fixed (byte* p = buffer) return Ioctl(fd, request, p); }

    /// <summary>struct input_event: a timeval (two longs), then type, code and value.</summary>
    public static int EventSize => IntPtr.Size * 2 + 8;
    public static unsafe void WriteEvents(int fd, IEnumerable<(ushort Type, ushort Code, int Value)> events)
    {
        int size = EventSize, time = IntPtr.Size * 2; var list = events.ToList(); var buffer = new byte[size * list.Count];
        for (int i = 0; i < list.Count; i++)
        {
            int o = i * size + time;
            BitConverter.TryWriteBytes(buffer.AsSpan(o, 2), list[i].Type); BitConverter.TryWriteBytes(buffer.AsSpan(o + 2, 2), list[i].Code); BitConverter.TryWriteBytes(buffer.AsSpan(o + 4, 4), list[i].Value);
        }
        fixed (byte* p = buffer) { _ = Write(fd, p, (nuint)buffer.Length); }
    }
}

/// <summary>One game controller the kernel exposes through evdev.</summary>
internal sealed record EvDevice(string Path, string Name, ushort Vendor, ushort Product);

/// <summary>PC mode on Linux. Reads the real pad through evdev and takes it exclusively (EVIOCGRAB), runs its state through the same
/// <see cref="PadProcessor"/> as Windows, and feeds the result to a virtual Xbox 360 pad made with uinput. A port of the tested Python engine in
/// tools/linux_controller_check; the C# port itself has not been run against a real pad yet.</summary>
internal sealed class PcEngine
{
    public const string VirtualName = "X20CTL virtual pad (Xbox 360)";
    public const string UdevRule = "KERNEL==\"uinput\", SUBSYSTEM==\"misc\", TAG+=\"uaccess\", OPTIONS+=\"static_node=uinput\"";
    const ushort EvSyn = 0, EvKey = 1, EvAbs = 3;
    const int AbsX = 0, AbsY = 1, AbsZ = 2, AbsRx = 3, AbsRy = 4, AbsRz = 5, AbsHat0X = 0x10, AbsHat0Y = 0x11;
    // the xpad driver's layout, which most pads in XInput mode (EasySMX included) arrive in
    static readonly Dictionary<int, string> Keys = new()
    {
        [0x130] = "A", [0x131] = "B", [0x133] = "X", [0x134] = "Y", [0x136] = "LB", [0x137] = "RB", [0x13A] = "SELECT", [0x13B] = "START", [0x13C] = "HOME",
        [0x13D] = "L3", [0x13E] = "R3", [0x138] = "LT", [0x139] = "RT", [0x220] = "DPAD_UP", [0x221] = "DPAD_DOWN", [0x222] = "DPAD_LEFT", [0x223] = "DPAD_RIGHT"
    };
    static readonly (ushort Mask, int Code)[] OutKeys =
    [
        (PadButtons.A, 0x130), (PadButtons.B, 0x131), (PadButtons.X, 0x133), (PadButtons.Y, 0x134), (PadButtons.LB, 0x136), (PadButtons.RB, 0x137),
        (PadButtons.Back, 0x13A), (PadButtons.Start, 0x13B), (PadButtons.Guide, 0x13C), (PadButtons.L3, 0x13D), (PadButtons.R3, 0x13E)
    ];

    public Func<int, PcModeProfile> ProfileFor { get; set; } = _ => new();
    public event Action? Changed;
    public bool Running => thread is { IsAlive: true };
    /// <summary>What it is doing right now, in words for the PC mode page.</summary>
    public string Detail { get; private set; } = "";
    public string Headline { get; private set; } = "Off";
    public bool Active { get; private set; }
    volatile bool reload = true, stop;
    /// <summary>Set while the controller is the mouse: only one thing can hold the pad.</summary>
    public volatile bool Suspended;
    Thread? thread;

    public static bool Supported => OperatingSystem.IsLinux();
    public static bool CanCreateVirtualPad()
    {
        if (!Supported) return false;
        int fd = Native.Open("/dev/uinput", Native.WriteOnly | Native.NonBlock);
        if (fd < 0) return false; Native.Close(fd); return true;
    }

    public void Start()
    {
        if (!Supported) { Set("Off", "PC mode needs Linux.", false); return; }
        if (Running) return;
        stop = false; reload = true; thread = new Thread(Loop) { IsBackground = true, Name = "x20ctl-pcmode" }; thread.Start();
    }

    public void Stop() { stop = true; thread?.Join(1500); thread = null; Set("Off", "PC mode is off. Your controller goes straight to the game.", false); }
    public void Reload() => reload = true;
    void Set(string headline, string detail, bool active) { if (headline == Headline && detail == Detail && active == Active) return; Headline = headline; Detail = detail; Active = active; Changed?.Invoke(); }

    public static IReadOnlyList<EvDevice> Controllers()
    {
        var found = new List<EvDevice>();
        if (!Directory.Exists("/dev/input")) return found;
        foreach (string path in Directory.EnumerateFiles("/dev/input", "event*").OrderBy(p => int.TryParse(p[(p.LastIndexOf("event") + 5)..], out int n) ? n : 0))
        {
            int fd = Native.Open(path, Native.ReadOnly | Native.NonBlock); if (fd < 0) continue;
            try
            {
                var name = new byte[256]; Native.IoctlBuf(fd, Native.Ioc(2, 'E', 0x06, 256), name);
                var id = new byte[8]; Native.IoctlBuf(fd, Native.Ioc(2, 'E', 0x02, 8), id);
                string n = Encoding.UTF8.GetString(name).Split('\0')[0];
                var keys = Bits(fd, EvKey, 0x300); var axes = Bits(fd, EvAbs, 0x40);
                if (n != VirtualName && axes.Count > 0 && keys.Any(k => k is >= 0x130 and <= 0x13E)) found.Add(new(path, n, BitConverter.ToUInt16(id, 2), BitConverter.ToUInt16(id, 4)));
            }
            finally { Native.Close(fd); }
        }
        return found;
    }

    static HashSet<int> Bits(int fd, int ev, int count)
    {
        var buffer = new byte[(count + 7) / 8]; var set = new HashSet<int>();
        if (Native.IoctlBuf(fd, Native.Ioc(2, 'E', 0x20 + ev, buffer.Length), buffer) < 0) return set;
        for (int i = 0; i < count; i++) if ((buffer[i / 8] >> (i % 8) & 1) != 0) set.Add(i);
        return set;
    }

    // ------------------------------------------------------------------------------------------------ the loop

    sealed class Slot
    {
        public int Player; public EvDevice? Device; public RealPad? Real; public VirtualPad? Virtual; public PadProcessor? Processor; public PcModeProfile Profile = new(); public string Json = "";
        public readonly HoldCombo Combo = new(PadButtons.Back | PadButtons.Start, 1000); public bool Paused; public long Clock; public bool Reported;
    }

    /// <summary>Each connected controller is a player (in the order the kernel lists them) with its own settings and its own virtual Xbox pad.</summary>
    public IReadOnlyList<(int Player, string Name, bool Active)> Routes => routes;
    volatile IReadOnlyList<(int, string, bool)> routes = [];

    void Loop()
    {
        var slots = Enumerable.Range(0, 4).Select(i => new Slot { Player = i }).ToArray(); long lastLook = -10000, lastScan = -10000;
        try
        {
            if (!CanCreateVirtualPad()) { Set("Needs attention", "PC mode can't create the virtual controller: /dev/uinput isn't writable for you. Allow it once, then try again.", false); return; }
            IReadOnlyList<EvDevice> devices = [];
            while (!stop)
            {
                long now = Environment.TickCount64;
                if (Suspended) { foreach (var s in slots) Release(s); Set("Paused", "Paused while the controller is the mouse.", false); Thread.Sleep(150); continue; }
                if (reload || now - lastLook > 750)
                {
                    reload = false; lastLook = now;
                    foreach (var s in slots)
                        try { var p = ProfileFor(s.Player); string j = System.Text.Json.JsonSerializer.Serialize(p); if (j != s.Json) { s.Json = j; s.Profile = p; s.Processor = null; } }
                        catch (Exception e) when (e is InvalidDataException or IOException) { Set("Needs attention", "Settings couldn't be used: " + e.Message, false); }
                }
                if (now - lastScan > 500) { lastScan = now; devices = Controllers(); }
                bool any = false; var lines = new List<string>(); var report = new List<(int, string, bool)>();
                for (int i = 0; i < slots.Length; i++)
                {
                    var s = slots[i]; var device = i < devices.Count ? devices[i] : null;
                    if (s.Real != null && (device == null || device.Path != s.Device?.Path)) { s.Real.Dispose(); s.Real = null; s.Processor = null; }
                    if (device == null || s.Profile.IsNeutral) { if (s.Profile.IsNeutral) Release(s); continue; }
                    if (s.Real == null)
                    {
                        try { s.Real = new RealPad(device); s.Device = device; s.Virtual ??= new VirtualPad(); s.Processor = null; }
                        catch (IOException e) { lines.Add($"Player {i + 1}: couldn't take {device.Name}: {e.Message}. Is another program holding it?"); continue; }
                    }
                    any = true; lines.Add($"Player {i + 1}: {device.Name} is hidden from games; they see one Xbox controller with your settings."); report.Add((i, device.Name, true));
                }
                routes = report;
                var open = slots.Where(s => s.Real != null).ToList();
                if (open.Count == 0)
                {
                    string why = devices.Count == 0 ? "No controller found. Plug one in and it starts by itself." : "Nothing to apply yet. Set a remap, curve, turbo or macro and it starts by itself.";
                    Set(lines.Count > 0 ? "Needs attention" : "Ready", lines.Count > 0 ? string.Join("\n", lines) : why, false); Thread.Sleep(devices.Count == 0 ? 250 : 120); continue;
                }
                Wait(open, 4);
                foreach (var s in open)
                {
                    if (!s.Real!.Read()) { s.Real.Dispose(); s.Real = null; s.Processor = null; continue; }
                    if (s.Processor == null) { s.Processor = new PadProcessor(s.Profile); s.Clock = Environment.TickCount64; }
                    long t = Environment.TickCount64 - s.Clock; var state = s.Real.State();
                    if (s.Combo.Update(state.Buttons, t)) { s.Paused = !s.Paused; lines.Add(s.Paused ? $"Player {s.Player + 1}: your settings are off for now. Hold View + Menu again to switch them on." : $"Player {s.Player + 1}: your settings are back on."); }
                    s.Virtual!.Update(s.Paused ? state : s.Processor.Process(state, t));
                }
                Set("Active", string.Join("\n", lines), any);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Set("Needs attention", e.Message, false); }
        finally { foreach (var s in slots) { s.Real?.Dispose(); s.Virtual?.Dispose(); } routes = []; }
    }

    static void Release(Slot s) { s.Real?.Dispose(); s.Real = null; s.Virtual?.Dispose(); s.Virtual = null; s.Processor = null; }

    static void Wait(List<Slot> open, int ms)
    {
        var fds = open.Select(s => new Native.PollFd { Fd = s.Real!.Fd, Events = Native.PollIn }).ToArray();
        Native.Poll(ref System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(fds), (nuint)fds.Length, ms);
    }

    sealed class HoldCombo(ushort mask, int holdMs)
    {
        long since = -1; bool fired;
        public bool Update(ushort buttons, long now)
        {
            if ((buttons & mask) != mask) { since = -1; fired = false; return false; }
            if (since < 0) since = now;
            if (fired || now - since < holdMs) return false;
            fired = true; return true;
        }
    }

    // ------------------------------------------------------------------------------------------------ the real pad

    internal sealed unsafe class RealPad : IDisposable
    {
        readonly int fd; public int Fd => fd; readonly Dictionary<int, (int Lo, int Hi)> ranges = new(); readonly Dictionary<int, int> raw = new(); readonly HashSet<string> pressed = new(); readonly int[] hat = new int[2];
        public RealPad(EvDevice device, bool grab = true)
        {
            fd = Native.Open(device.Path, Native.ReadOnly | Native.NonBlock);
            if (fd < 0) throw new IOException("open failed (" + Marshal.GetLastPInvokeError() + ")");
            foreach (int code in new[] { AbsX, AbsY, AbsZ, AbsRx, AbsRy, AbsRz, AbsHat0X, AbsHat0Y })
            {
                var buffer = new byte[24];
                if (Native.IoctlBuf(fd, Native.Ioc(2, 'E', 0x40 + code, 24), buffer) == 0) ranges[code] = (BitConverter.ToInt32(buffer, 4), BitConverter.ToInt32(buffer, 8));
            }
            if (grab && !Grab(true)) { int e = Marshal.GetLastPInvokeError(); Native.Close(fd); throw new IOException("the kernel refused exclusive access (" + e + ")"); }
        }

        /// <summary>Takes the pad exclusively (no other program gets its events) or gives it back.</summary>
        public bool Grab(bool on) => Native.IoctlInt(fd, Native.Ioc(1, 'E', 0x90, 4), on ? 1 : 0) >= 0;

        public void Wait(int ms) { var p = new Native.PollFd { Fd = fd, Events = Native.PollIn }; Native.Poll(ref p, 1, ms); }

        /// <summary>Drains pending events. False when the controller went away.</summary>
        public bool Read()
        {
            int size = Native.EventSize, time = IntPtr.Size * 2; var buffer = new byte[size * 64];
            while (true)
            {
                nint n; fixed (byte* p = buffer) n = Native.Read(fd, p, (nuint)buffer.Length);
                if (n < 0) return Marshal.GetLastPInvokeError() == Native.EAGAIN;
                if (n == 0) return false;
                for (int o = 0; o + size <= n; o += size)
                {
                    ushort type = BitConverter.ToUInt16(buffer, o + time), code = BitConverter.ToUInt16(buffer, o + time + 2); int value = BitConverter.ToInt32(buffer, o + time + 4);
                    if (type == EvKey && Keys.TryGetValue(code, out var name)) { if (value != 0) pressed.Add(name); else pressed.Remove(name); }
                    else if (type == EvAbs)
                    {
                        if (code == AbsHat0X) hat[0] = Math.Sign(value); else if (code == AbsHat0Y) hat[1] = Math.Sign(value); else raw[code] = value;
                    }
                }
                if (n < buffer.Length) return true;
            }
        }

        static short Stick(int v, (int Lo, int Hi) r, bool invert)
        {
            if (r.Hi <= r.Lo) return 0;
            double s = (double)(v - r.Lo) / (r.Hi - r.Lo) * 65535 - 32768;
            return (short)Math.Clamp((int)Math.Round(invert ? -s - 1 : s), -32768, 32767);
        }
        static byte Trigger(int v, (int Lo, int Hi) r) => r.Hi <= r.Lo ? (byte)0 : (byte)Math.Clamp((int)Math.Round((double)(v - r.Lo) / (r.Hi - r.Lo) * 255), 0, 255);

        public PadState State()
        {
            ushort buttons = 0;
            foreach (string n in pressed) if (PadButtons.Mask.TryGetValue(n, out ushort m)) buttons |= m;
            if (hat[0] < 0) buttons |= PadButtons.Left; if (hat[0] > 0) buttons |= PadButtons.Right; if (hat[1] < 0) buttons |= PadButtons.Up; if (hat[1] > 0) buttons |= PadButtons.Down;
            (int, int) Range(int c) => ranges.GetValueOrDefault(c);
            int Centre(int c) { var (lo, hi) = Range(c); return raw.TryGetValue(c, out int v) ? v : (lo + hi) / 2; }
            int Low(int c) => raw.TryGetValue(c, out int v) ? v : Range(c).Item1;
            byte lt = pressed.Contains("LT") ? (byte)255 : Trigger(Low(AbsZ), ranges.GetValueOrDefault(AbsZ));
            byte rt = pressed.Contains("RT") ? (byte)255 : Trigger(Low(AbsRz), ranges.GetValueOrDefault(AbsRz));
            return new PadState(buttons, lt, rt, Stick(Centre(AbsX), Range(AbsX), false), Stick(Centre(AbsY), Range(AbsY), true), Stick(Centre(AbsRx), Range(AbsRx), false), Stick(Centre(AbsRy), Range(AbsRy), true));
        }

        public void Dispose() { Native.IoctlInt(fd, Native.Ioc(1, 'E', 0x90, 4), 0); Native.Close(fd); }
    }

    // ------------------------------------------------------------------------------------------------ the virtual pad

    sealed class VirtualPad : IDisposable
    {
        readonly int fd; PadState? last;
        public unsafe VirtualPad()
        {
            fd = Native.Open("/dev/uinput", Native.WriteOnly | Native.NonBlock);
            if (fd < 0) throw new IOException("/dev/uinput couldn't be opened");
            nuint setEv = Native.Ioc(1, 'U', 100, 4), setKey = Native.Ioc(1, 'U', 101, 4), setAbs = Native.Ioc(1, 'U', 103, 4);
            Native.IoctlInt(fd, setEv, EvKey); Native.IoctlInt(fd, setEv, EvAbs);
            foreach (var (_, code) in OutKeys) Native.IoctlInt(fd, setKey, code);
            foreach (int a in new[] { AbsX, AbsY, AbsZ, AbsRx, AbsRy, AbsRz, AbsHat0X, AbsHat0Y }) Native.IoctlInt(fd, setAbs, a);
            // the legacy uinput record (works on every kernel): an Xbox 360 pad's identity and axis ranges, so Steam, SDL and Proton treat it as a normal Xbox controller
            var dev = new byte[80 + 8 + 4 + 64 * 4 * 4];
            Encoding.UTF8.GetBytes(VirtualName).CopyTo(dev, 0);
            BitConverter.TryWriteBytes(dev.AsSpan(80, 2), (ushort)0x03); BitConverter.TryWriteBytes(dev.AsSpan(82, 2), (ushort)0x045E); BitConverter.TryWriteBytes(dev.AsSpan(84, 2), (ushort)0x028E); BitConverter.TryWriteBytes(dev.AsSpan(86, 2), (ushort)0x0110);
            int max = 92, min = max + 256, fuzz = min + 256, flat = fuzz + 256;
            void Put(int table, int axis, int v) => BitConverter.TryWriteBytes(dev.AsSpan(table + axis * 4, 4), v);
            foreach (int a in new[] { AbsX, AbsY, AbsRx, AbsRy }) { Put(min, a, -32768); Put(max, a, 32767); Put(fuzz, a, 16); Put(flat, a, 128); }
            foreach (int a in new[] { AbsZ, AbsRz }) Put(max, a, 255);
            foreach (int a in new[] { AbsHat0X, AbsHat0Y }) { Put(min, a, -1); Put(max, a, 1); }
            fixed (byte* p = dev) { if (Native.Write(fd, p, (nuint)dev.Length) < 0) { Native.Close(fd); throw new IOException("uinput refused the device record"); } }
            if (Native.IoctlInt(fd, Native.Ioc(0, 'U', 1, 0), 0) < 0) { Native.Close(fd); throw new IOException("uinput couldn't create the virtual pad"); }
        }

        public void Update(PadState s)
        {
            if (s == last) return;
            var events = new List<(ushort, ushort, int)>();
            foreach (var (mask, code) in OutKeys) events.Add((EvKey, (ushort)code, (s.Buttons & mask) != 0 ? 1 : 0));
            int hx = ((s.Buttons & PadButtons.Right) != 0 ? 1 : 0) - ((s.Buttons & PadButtons.Left) != 0 ? 1 : 0), hy = ((s.Buttons & PadButtons.Down) != 0 ? 1 : 0) - ((s.Buttons & PadButtons.Up) != 0 ? 1 : 0);
            // Linux sticks are Y-down; the processor's state is Y-up like XInput
            events.Add((EvAbs, AbsX, s.LeftX)); events.Add((EvAbs, AbsY, s.LeftY != 0 ? -s.LeftY - 1 : 0)); events.Add((EvAbs, AbsRx, s.RightX)); events.Add((EvAbs, AbsRy, s.RightY != 0 ? -s.RightY - 1 : 0));
            events.Add((EvAbs, AbsZ, s.LeftTrigger)); events.Add((EvAbs, AbsRz, s.RightTrigger)); events.Add((EvAbs, AbsHat0X, hx)); events.Add((EvAbs, AbsHat0Y, hy)); events.Add((EvSyn, 0, 0));
            Native.WriteEvents(fd, events.Select(e => (e.Item1, e.Item2, e.Item3))); last = s;
        }

        public void Dispose() { Native.IoctlInt(fd, Native.Ioc(0, 'U', 2, 0), 0); Native.Close(fd); }
    }
}
