using System.Diagnostics;
using System.Text;
using System.Text.Json;
using X20Ctl.Zone.Mouse;
using X20Ctl.Zone.PcMode;
namespace X20Ctl.Linux;

/// <summary>The controller as a mouse on Linux: one stick moves the pointer, the other scrolls, buttons click or press keys. A uinput mouse with a few keyboard keys,
/// driven by the same <see cref="MouseMapper"/> as Windows. A port of the Python mouse in tools/linux_controller_check; not run against a real pad yet.</summary>
internal sealed class MouseEngine
{
    const ushort EvSyn = 0, EvKey = 1, EvRel = 2;
    const int RelX = 0, RelY = 1, RelHWheel = 6, RelWheel = 8, RelWheelHiRes = 0x0B, RelHWheelHiRes = 0x0C;
    static readonly Dictionary<MouseAction, int> Buttons = new() { [MouseAction.LeftClick] = 0x110, [MouseAction.RightClick] = 0x111, [MouseAction.MiddleClick] = 0x112, [MouseAction.Back] = 0x113, [MouseAction.Forward] = 0x114 };
    static readonly Dictionary<MouseAction, int> KeyCodes = new()
    {
        [MouseAction.Enter] = 28, [MouseAction.Escape] = 1, [MouseAction.Tab] = 15, [MouseAction.Space] = 57, [MouseAction.Backspace] = 14, [MouseAction.Windows] = 125, [MouseAction.TaskView] = 125,
        [MouseAction.Up] = 103, [MouseAction.Down] = 108, [MouseAction.Left] = 105, [MouseAction.Right] = 106, [MouseAction.PageUp] = 104, [MouseAction.PageDown] = 109,
        [MouseAction.VolumeUp] = 115, [MouseAction.VolumeDown] = 114, [MouseAction.Mute] = 113, [MouseAction.PlayPause] = 164
    };
    static readonly HashSet<MouseAction> Repeats = [MouseAction.Up, MouseAction.Down, MouseAction.Left, MouseAction.Right, MouseAction.Backspace, MouseAction.PageUp, MouseAction.PageDown, MouseAction.VolumeUp, MouseAction.VolumeDown, MouseAction.Space];

    public Func<MouseProfile> Profile { get; set; } = () => new();
    public event Action? Changed;
    public string Detail { get; private set; } = "";
    public string Headline { get; private set; } = "Off";
    /// <summary>The mouse is working right now (armed and not switched off by the controller shortcut).</summary>
    public bool Pointing { get; private set; }
    public bool Running => thread is { IsAlive: true };
    volatile bool stop, reload = true; Thread? thread;
    public void Reload() => reload = true;
    void Set(string headline, string detail, bool pointing) { if (headline == Headline && detail == Detail && pointing == Pointing) return; Headline = headline; Detail = detail; Pointing = pointing; Changed?.Invoke(); }

    public void Start()
    {
        if (!PcEngine.Supported) { Set("Not available", "The mouse needs Linux: it uses the kernel's evdev and uinput.", false); return; }
        if (Running) return; stop = false; reload = true;
        thread = new Thread(Loop) { IsBackground = true, Name = "x20ctl-mouse" }; thread.Start();
    }
    public void Stop() { stop = true; thread?.Join(1500); thread = null; Set("Off", "Off", false); }

    /// <summary>One controller driving the pointer: its own mapper and shortcut, so every connected pad can move the pointer and any pad's shortcut switches only that pad.</summary>
    sealed class Pad(EvDevice device, PcEngine.RealPad real)
    {
        public EvDevice Device = device; public PcEngine.RealPad Real = real; public MouseMapper? Mapper; public HoldCombo? Combo; public bool Active = true; public long Last;
    }

    void Loop()
    {
        var pads = new Dictionary<string, Pad>(); Mouse? mouse = null; MouseProfile profile = new();
        var clock = Stopwatch.StartNew(); long scanned = -1000; var held = new Dictionary<MouseAction, long>();
        try
        {
            if (!PcEngine.CanCreateVirtualPad()) { Set("Needs attention", "Can't create the virtual mouse: /dev/uinput isn't writable for you. The PC mode tab shows how to allow it.", false); return; }
            mouse = new Mouse(); string problem = "";
            while (!stop)
            {
                if (reload)
                {
                    reload = false;
                    try { profile = Profile(); } catch (InvalidDataException) { profile = new(); }
                    foreach (var pad in pads.Values) { pad.Mapper = new MouseMapper(profile); pad.Combo = profile.Combo ? new HoldCombo(profile.ToggleMask, profile.HoldMilliseconds) : null; }
                    scanned = -1000;
                }
                long now = clock.ElapsedMilliseconds;
                if (now - scanned >= 500)
                {
                    scanned = now; var present = PcEngine.Controllers().ToList(); problem = "";
                    foreach (var gone in pads.Keys.Where(k => present.All(d => d.Path != k)).ToList()) { pads[gone].Real.Dispose(); pads.Remove(gone); }
                    foreach (var device in present.Where(d => !pads.ContainsKey(d.Path)))
                    {
                        try
                        {
                            var pad = new Pad(device, new PcEngine.RealPad(device, grab: true)) { Last = now, Mapper = new MouseMapper(profile), Combo = profile.Combo ? new HoldCombo(profile.ToggleMask, profile.HoldMilliseconds) : null };
                            pads[device.Path] = pad;
                        }
                        catch (IOException e) { problem = $"Couldn't take {device.Name}: {e.Message}."; }
                    }
                    if (pads.Count == 0) Set(problem.Length > 0 ? "Needs attention" : "Waiting", problem.Length > 0 ? problem : "No controller found. Plug one in and it starts by itself.", false);
                    else Set("Active", (pads.Count == 1 ? pads.Values.First().Device.Name + " moves" : pads.Count + " controllers move") + " the pointer. Hold the shortcut to switch one off.", pads.Values.Any(p => p.Active));
                }
                if (pads.Count == 0) { Thread.Sleep(100); continue; }
                var fds = pads.Values.Select(p => new Native.PollFd { Fd = p.Real.Fd, Events = Native.PollIn }).ToArray();
                Native.Poll(ref System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(fds), (nuint)fds.Length, 8);
                now = clock.ElapsedMilliseconds;
                foreach (var (path, pad) in pads.ToList())
                {
                    if (!pad.Real.Read()) { pad.Real.Dispose(); pads.Remove(path); pad.Mapper?.Release(); scanned = -1000; continue; }
                    double seconds = (now - pad.Last) / 1000.0; pad.Last = now; var state = pad.Real.State();
                    if (pad.Combo != null && pad.Combo.Update(state.Buttons, now))
                    {
                        pad.Active = !pad.Active; pad.Real.Grab(pad.Active);
                        if (!pad.Active) foreach (var (act, _) in pad.Mapper!.Release()) mouse.Act(act, 0);
                        Set("Active", pad.Active ? "The mouse is on." : "A controller's mouse is off for now: it works as it normally does. Hold the shortcut again to switch it on.", pads.Values.Any(p => p.Active));
                    }
                    if (!pad.Active || pad.Mapper == null) continue;
                    var frame = pad.Mapper.Step(state, seconds);
                    mouse.Move(frame.Dx, frame.Dy, frame.Wheel, frame.HWheel);
                    foreach (var (action, down) in frame.Edges)
                    {
                        if (action == MouseAction.OnScreenKeyboard) { if (down) Keyboard(); continue; }
                        mouse.Act(action, down ? 1 : 0); if (down && Repeats.Contains(action)) held[action] = now + 400; else held.Remove(action);
                    }
                }
                foreach (var action in held.Keys.ToList()) if (now >= held[action]) { mouse.Act(action, 2); held[action] = now + 40; }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Set("Needs attention", e.Message, false); }
        finally { foreach (var pad in pads.Values) pad.Real.Dispose(); mouse?.Dispose(); }
    }

    static void Keyboard()
    {
        foreach (string app in new[] { "onboard", "wvkbd-mobintl", "squeekboard" })
            try { Process.Start(new ProcessStartInfo(app) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true }); return; }
            catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException) { }
    }

    sealed unsafe class Mouse : IDisposable
    {
        readonly int fd; readonly int[] notch = new int[2];
        public Mouse()
        {
            fd = Native.Open("/dev/uinput", Native.WriteOnly | Native.NonBlock);
            if (fd < 0) throw new IOException("/dev/uinput couldn't be opened");
            nuint setEv = Native.Ioc(1, 'U', 100, 4), setKey = Native.Ioc(1, 'U', 101, 4), setRel = Native.Ioc(1, 'U', 102, 4);
            Native.IoctlInt(fd, setEv, EvKey); Native.IoctlInt(fd, setEv, EvRel);
            foreach (int code in Buttons.Values.Concat(KeyCodes.Values).Distinct()) Native.IoctlInt(fd, setKey, code);
            foreach (int code in new[] { RelX, RelY, RelWheel, RelHWheel, RelWheelHiRes, RelHWheelHiRes }) Native.IoctlInt(fd, setRel, code);
            var dev = new byte[80 + 8 + 4 + 64 * 4 * 4]; Encoding.UTF8.GetBytes("X20CTL controller mouse").CopyTo(dev, 0);
            BitConverter.TryWriteBytes(dev.AsSpan(80, 2), (ushort)0x03); BitConverter.TryWriteBytes(dev.AsSpan(86, 2), (ushort)1);
            fixed (byte* p = dev) { if (Native.Write(fd, p, (nuint)dev.Length) < 0) { Native.Close(fd); throw new IOException("uinput refused the device record"); } }
            if (Native.IoctlInt(fd, Native.Ioc(0, 'U', 1, 0), 0) < 0) { Native.Close(fd); throw new IOException("uinput couldn't create the virtual mouse"); }
        }

        public void Move(int dx, int dy, int wheel, int hwheel)
        {
            var events = new List<(ushort, ushort, int)>();
            if (dx != 0) events.Add((EvRel, RelX, dx)); if (dy != 0) events.Add((EvRel, RelY, dy));
            int i = 0;
            foreach (var (amount, hi, lo) in new[] { (wheel, RelWheelHiRes, RelWheel), (hwheel, RelHWheelHiRes, RelHWheel) })
            {
                if (amount != 0)
                {
                    events.Add((EvRel, (ushort)hi, amount)); notch[i] += amount; int whole = notch[i] / 120;
                    if (whole != 0) { events.Add((EvRel, (ushort)lo, whole)); notch[i] -= whole * 120; }
                }
                i++;
            }
            if (events.Count > 0) { events.Add((EvSyn, 0, 0)); Native.WriteEvents(fd, events); }
        }

        /// <summary>value: 1 down, 0 up, 2 a held key repeating.</summary>
        public void Act(MouseAction action, int value)
        {
            if (Buttons.TryGetValue(action, out int b)) Native.WriteEvents(fd, [(EvKey, (ushort)b, Math.Min(value, 1)), (EvSyn, 0, 0)]);
            else if (KeyCodes.TryGetValue(action, out int k)) Native.WriteEvents(fd, [(EvKey, (ushort)k, value), (EvSyn, 0, 0)]);
        }

        public void Dispose() { Native.IoctlInt(fd, Native.Ioc(0, 'U', 2, 0), 0); Native.Close(fd); }
    }
}

/// <summary>The mouse settings and whether it is switched on. While it is on, PC mode steps aside: only one thing can hold the pad.</summary>
internal sealed class MouseHub
{
    public static MouseHub Shared { get; } = new();
    readonly string statePath = Path.Combine(Path.GetDirectoryName(MouseProfile.DefaultPath)!, "mouse-state.json");
    public MouseProfile Profile { get; private set; } = MouseProfile.Load(MouseProfile.DefaultPath);
    public MouseEngine Engine { get; } = new();
    public bool Enabled { get; private set; }
    public string Status { get; private set; } = "Saved on this PC";
    public event Action? Changed;
    sealed record Saved(bool Enabled);

    MouseHub()
    {
        try { if (File.Exists(statePath) && JsonSerializer.Deserialize<Saved>(File.ReadAllBytes(statePath)) is { } s) Enabled = s.Enabled; }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { }
        Engine.Profile = () => Profile; Engine.Changed += () => Changed?.Invoke();
        if (Enabled && PcEngine.Supported) Begin();
    }

    void Begin() { PcModeHub.Shared.Engine.Suspended = true; Engine.Start(); }

    public void SetEnabled(bool on)
    {
        Enabled = on;
        try { Directory.CreateDirectory(Path.GetDirectoryName(statePath)!); File.WriteAllBytes(statePath, JsonSerializer.SerializeToUtf8Bytes(new Saved(on))); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Status = "Couldn't remember this: " + e.Message; }
        if (on) Begin(); else { Engine.Stop(); PcModeHub.Shared.Engine.Suspended = false; }
        Changed?.Invoke();
    }

    public void Save(MouseProfile next)
    {
        try { next.Save(MouseProfile.DefaultPath); Profile = next; Engine.Reload(); Status = "Saved on this PC · Changes apply immediately"; }
        catch (Exception e) when (e is InvalidDataException or IOException or UnauthorizedAccessException) { Status = "Not saved: " + e.Message; }
        Changed?.Invoke();
    }
}
