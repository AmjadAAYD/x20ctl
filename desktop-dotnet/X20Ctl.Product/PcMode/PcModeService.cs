using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.DualShock4;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using X20Ctl.Product.Scanner;
using X20Ctl.Zone.PcMode;
namespace X20Ctl.Product;

public enum PcModeState { Off, Ready, Active, NeedsAttention }

/// <summary>
/// PC mode: read the real pads, apply the saved profile, and hand the result to games through one virtual Xbox pad per
/// real one while the real ones are hidden. Nothing is ever written to a controller. The virtual pads and the hiding
/// exist only while there is something to apply and a pad is plugged in, and are taken away again on any exit.
/// Each controller has its own settings: the Nth controller Windows sees gets player N's. They follow the game in front:
/// a game with its own settings for that player gets them, everything else gets that player's "Every game".
/// </summary>
public sealed class PcModeService : IDisposable
{
    readonly PcModeStore store;
    Thread? thread; volatile bool stop, reload = true, enabled, paused, suspended, mute;
    PcModeState state = PcModeState.Off; string detail = "PC mode is off. Your controller goes straight to the game.";
    public sealed record Route(int Player,int PhysicalSlot,int VirtualSlot,string Profile);
    Route[] routes=[];
    public IReadOnlyList<Route> Routes => Volatile.Read(ref routes);
    public event Action? Changed;
    /// <summary>Something happened from the controller (the View + Menu hold): a short line for a notification.</summary>
    public event Action<string>? Notice;
    public PcModeState State { get { lock (this) return state; } }
    public string Detail { get { lock (this) return detail; } }
    public int Applied { get; private set; }
    /// <summary>The profile in use right now: "default", or a game's "game:…" key (when any controller has that game's own).</summary>
    public string ActiveKey { get; private set; } = PcModeStore.DefaultKey;
    public int Controllers { get; private set; }
    static int[] virtualSlots = [];
    /// <summary>The XInput slots our virtual pads sit in: other readers in the app (the macro recorder, Diagnostics,
    /// mouse mode) skip them so they read the real controllers, not PC mode's remapped copies.</summary>
    public static bool IsVirtual(int slot) => Volatile.Read(ref virtualSlots).Contains(slot);
    public static int VirtualSlot => Volatile.Read(ref virtualSlots) is [var first, ..] ? first : -1;

    public PcModeService(PcModeStore store) { this.store = store; }

    public bool Enabled
    {
        get => enabled;
        set { enabled = value; if (value && !ReviewSandbox.Active) Start(); }
    }
    /// <summary>Controller Check is open: give the real controller back so the scan reads it and only it (the virtual
    /// pad would show up as a second, remapped controller). Doesn't change the saved on/off choice.</summary>
    public bool Paused { get => paused; set => paused = value; }
    /// <summary>The newest controller input PC mode read and what it sent on to the game, for the Test tab. Null until the first loop.</summary>
    public sealed record Snapshot(PadState Input, PadState Output, long Sent);
    Snapshot? snapshot;
    public Snapshot? Latest => Volatile.Read(ref snapshot);
    /// <summary>Settings switched off from the controller (hold View + Menu): the pads pass through untouched.</summary>
    public bool Suspended { get => suspended; set { suspended = value; Changed?.Invoke(); } }
    /// <summary>The controller is the mouse right now: games get a resting controller, so clicks don't also press A.</summary>
    public bool Mute { get => mute; set => mute = value; }
    /// <summary>The profile was edited: pick it up on the next loop.</summary>
    public void Reload() => reload = true;
    volatile bool rebuild;
    /// <summary>The virtual controller type changed: replace the running virtual pads with the new type.</summary>
    public void Rebuild() => rebuild = true;

    void Set(PcModeState next, string text)
    {
        lock (this) { if (state == next && detail == text) return; state = next; detail = text; }
        Changed?.Invoke();
    }

    void Start()
    {
        if (thread is { IsAlive: true }) return;
        stop = false;
        thread = new Thread(Run) { IsBackground = true, Name = "X20CTL PC mode", Priority = ThreadPriority.AboveNormal };
        thread.Start();
    }

    [DllImport("winmm.dll")] static extern uint timeBeginPeriod(uint period);
    [DllImport("winmm.dll")] static extern uint timeEndPeriod(uint period);

    sealed class Pair(int real, VirtualPad pad)
    {
        public readonly int Real = real; public readonly VirtualPad Pad = pad;
        public PadProcessor? Processor;
        /// <summary>The store key this pad's settings come from: its player's every-game or game settings.</summary>
        public string Key = "";
        public readonly X20Ctl.Zone.Mouse.HoldCombo Toggle = new(PadButtons.Back | PadButtons.Start, 1000);
    }

    /// <summary>The store key for a controller (by its XInput slot, which is its player) with this app in front: the
    /// player's own settings for the game if there are some, otherwise the player's every-game settings.</summary>
    string KeyFor(int slot, string? exe) => exe != null && store.Has(PcModeStore.For(slot, PcModeStore.GameKey(exe))) ? PcModeStore.For(slot, PcModeStore.GameKey(exe)) : PcModeStore.For(slot, PcModeStore.DefaultKey);

    void Run()
    {
        timeBeginPeriod(1);
        var pairs = new List<Pair>(); var hidden = new List<string>(); bool cloakWasOn = false;
        var clock = Stopwatch.StartNew(); bool anything = false; bool broken = false; bool setupAttempted = false; string? exe = null;
        var profiles = new Dictionary<string, PcModeProfile>(); // read once per reload, by store key
        PcModeProfile ProfileOf(string key) { if (!profiles.TryGetValue(key, out var p)) profiles[key] = p = store.Get(key); return p; }
        void Assign()
        {   // which settings each pad gets; a pad whose settings changed starts a fresh processor
            foreach (var pair in pairs) { string key = KeyFor(pair.Real, exe); if (key != pair.Key) { LetGo(pair); pair.Key = key; pair.Processor = null; } }
            string? game = pairs.Select(p => PcModeStore.Split(p.Key).Key).FirstOrDefault(k => k != PcModeStore.DefaultKey);
            ActiveKey = game ?? PcModeStore.DefaultKey;
        }
        long nextLook = 0, nextJoin = 0;
        WindowsReader? reader = null;
        // keys and mouse buttons the Keyboard tab is holding are let go, so nothing stays stuck down
        static void LetGo(Pair pair) { if (pair.Processor is { } processor) foreach (var (action, down) in processor.ReleaseAll()) MouseOutput.Output(action, down); }
        void Teardown()
        {
            foreach (var pair in pairs) { LetGo(pair); try { pair.Pad.Dispose(); } catch (Exception) { } }
            pairs.Clear();Volatile.Write(ref routes,[]); Volatile.Write(ref virtualSlots, []); Controllers = 0;
            if (hidden.Count > 0) { HidHide.Unhide(hidden, restoreCloak: !cloakWasOn); hidden.Clear(); HidHide.ForgetRecovery(); }
        }
        string ActiveText()
        {
            string kind = PcModeEntry.Chosen == PcModeEntry.VirtualType.DualShock4 ? "PlayStation 4" : "Xbox";
            string who = pairs.Count == 1 ? $"Games see one {kind} controller" : $"Games see {pairs.Count} {kind} controllers, one for each of yours";
            var gamePad = pairs.FirstOrDefault(p => PcModeStore.IsGame(p.Key));
            string which = gamePad == null ? "" : $" · {ProfileOf(gamePad.Key).Name ?? ActiveKey[PcModeStore.GamePrefix.Length..]} settings";
            return suspended ? $"Your settings are switched off for now (you held View + Menu). Hold them again for a second to switch them back on."
                : $"Active{which}. {who} with your settings. Close X20CTL and it goes back to normal.";
        }
        try
        {
            HidHide.RecoverFromCrash();
            reader = new WindowsReader("XInput1_4.dll");
            while (!stop)
            {
                if (!enabled) { Teardown(); Set(PcModeState.Off, "PC mode is off. Your controller goes straight to the game."); Thread.Sleep(200); continue; }
                if (paused) { Teardown(); Set(PcModeState.Ready, "Paused while Controller Check runs, so the scan reads your real controller."); Thread.Sleep(200); continue; }
                long now = clock.ElapsedMilliseconds;
                if (now >= nextLook)
                {   // twice a second: which game is in front, and so which profile applies
                    nextLook = now + 500;
                    string? front = ForegroundApp.Exe;
                    if (front != exe) { exe = front; string was = ActiveKey; Assign(); if (ActiveKey != was && pairs.Count > 0) Set(PcModeState.Active, ActiveText()); }
                }
                if (reload)
                {
                    reload = false; broken = false;
                    // settings that fail their checks are never applied: they would otherwise end PC mode for the whole session
                    profiles.Clear();
                    try { foreach (var key in store.Keys) _ = new PadProcessor(ProfileOf(key)); }
                    catch (InvalidDataException) { Teardown(); broken = true; Set(PcModeState.NeedsAttention, "These PC mode settings couldn't be used. Open PC mode, press Reset Player profile, and set them again."); }
                    foreach (var pair in pairs) { LetGo(pair); pair.Processor = null; pair.Key = ""; }
                    Assign();
                    anything = store.AnySetting;
                    if (pairs.Count > 0) Set(PcModeState.Active, ActiveText());
                }
                if (broken) { Thread.Sleep(250); continue; }
                // with nothing to apply anywhere, stay out of the way; a game without settings while another has some
                // keeps the virtual pads (passing straight through) so switching windows doesn't make pads come and go
                // a PlayStation 4 pad is worth having with no settings at all; an Xbox copy of an Xbox pad changes nothing
                if (!anything && PcModeEntry.Chosen != PcModeEntry.VirtualType.DualShock4) { Teardown(); Set(PcModeState.Ready, "On, but there's nothing to apply yet. Change a button, curve, turbo or macro, or pick PlayStation 4, and it starts by itself."); Thread.Sleep(150); continue; }

                if (!PcModeSetup.Ready)
                {
                    if (!setupAttempted)
                    {
                        setupAttempted = true; // cancellation or failure never causes a UAC prompt loop
                        Set(PcModeState.NeedsAttention, "Setting up PC mode. Windows may ask for permission.");
                        try { Set(PcModeState.NeedsAttention, PcModeSetup.Run(automatic: true).GetAwaiter().GetResult()); }
                        catch (Exception error) when (error is System.ComponentModel.Win32Exception or IOException or UnauthorizedAccessException or InvalidOperationException)
                        { Set(PcModeState.NeedsAttention, "Couldn't set up PC mode: " + error.Message); }
                    }
                    if (!PcModeSetup.Ready) { Thread.Sleep(500); continue; }
                }
                if (stop || !enabled || paused) continue;
                // a new controller type: drop the old virtual pads; the block below makes new ones of the chosen type
                if (rebuild) { rebuild = false; if (pairs.Count > 0) Teardown(); }

                if (pairs.Count == 0)
                {
                    var reals = Enumerable.Range(0, 4).Where(slot => reader.Read(slot, out _)).ToList();
                    if (reals.Count == 0) { Set(PcModeState.Ready, "No controller found. Plug one in and it starts by itself."); Thread.Sleep(250); continue; }
                    if (!HidHide.Installed) { Set(PcModeState.NeedsAttention, "The controller-hiding driver isn't installed yet. Press Set up PC mode."); Thread.Sleep(500); continue; }
                    try
                    {
                        cloakWasOn = HidHide.CloakIsOn();
                        HidHide.RegisterSelf();
                        var devices = HidHide.GamingDevices();
                        // each try runs HidHide's command line three times, so wait a few seconds before the next
                        if (devices.Count == 0) { Set(PcModeState.NeedsAttention, "Windows didn't list a controller to hide. Unplug it and plug it back in."); Thread.Sleep(3000); continue; }
                        HidHide.RememberForRecovery(devices);
                        HidHide.Hide(devices); hidden.AddRange(devices);
                        // one virtual pad per real one, in slot order, so player 1 stays player 1
                        foreach (int real in reals) pairs.Add(new(real, new VirtualPad(real, PcModeEntry.Chosen)));
                        Assign();
                        Volatile.Write(ref virtualSlots, pairs.Select(p => p.Pad.Slot).Where(s => s >= 0).ToArray());
                        Controllers = pairs.Count;
                        HidHide.CloakOn();
                    }
                    catch (Exception error) when (error is not OutOfMemoryException)
                    {
                        Teardown();
                        Set(PcModeState.NeedsAttention, error is Nefarius.ViGEm.Client.Exceptions.VigemBusNotFoundException
                            ? "The virtual controller driver isn't installed yet. Press Set up PC mode." : "PC mode couldn't start: " + error.Message);
                        Thread.Sleep(3000); continue;
                    }
                    clock.Restart(); nextLook = 0; nextJoin = 1000;
                    Set(PcModeState.Active, ActiveText());
                }

                bool lost = false;
                foreach (var pair in pairs)
                {
                    if (!reader.Read(pair.Real, out var s)) { lost = true; break; }
                    var input = new PadState(s.Pad.Buttons, s.Pad.LT, s.Pad.RT, s.Pad.LX, s.Pad.LY, s.Pad.RX, s.Pad.RY);
                    if (pair.Toggle.Update(input.Buttons, clock.ElapsedMilliseconds) && !mute) // the mouse owns the controller while muted
                    {
                        suspended = !suspended; Set(PcModeState.Active, ActiveText());
                        Notice?.Invoke(suspended ? "PC mode settings off. Hold View + Menu again to switch them on." : "PC mode settings on.");
                    }
                    PadState output;
                    if (mute) { LetGo(pair); output = default; }
                    else if (suspended) { LetGo(pair); output = input; }
                    else { pair.Processor ??= new PadProcessor(ProfileOf(pair.Key)); output = pair.Processor.Process(input, clock.ElapsedMilliseconds); foreach (var (action, down) in pair.Processor.TakeActions()) MouseOutput.Output(action, down); }
                    try { pair.Pad.Update(output); Applied++; if (pair == pairs[0]) Volatile.Write(ref snapshot, new Snapshot(input, output, Applied)); }
                    catch (Exception error) when (error is not OutOfMemoryException) { Teardown(); Set(PcModeState.NeedsAttention, "The virtual controller stopped answering: " + error.Message); Thread.Sleep(1000); lost = false; break; }
                }
                Volatile.Write(ref routes,pairs.Select(p=>new Route(p.Real,p.Real,p.Pad.Slot,p.Key)).ToArray());
                // a pad went away: start over, so the others carry on and it rejoins when it's back
                if (lost) { Teardown(); Thread.Sleep(100); continue; }
                if (pairs.Count > 0 && clock.ElapsedMilliseconds >= nextJoin)
                {
                    nextJoin = clock.ElapsedMilliseconds + 1000;
                    // ViGEm may only learn a virtual pad's slot a moment after it connects
                    if (pairs.Any(p => !p.Pad.NoSlot && p.Pad.Slot < 0))
                        Volatile.Write(ref virtualSlots, pairs.Select(p => p.Pad.Refresh()).Where(s => s >= 0).ToArray());
                    // a controller plugged in since we started gets its own virtual pad too (only once every slot of
                    // ours is known, so one of our own virtual pads is never mistaken for a new controller)
                    else
                        for (int slot = 0; slot < 4; slot++)
                            if (!pairs.Any(p => p.Real == slot) && !IsVirtual(slot) && reader.Read(slot, out _)) { Teardown(); break; }
                }
                Thread.Sleep(1);
            }
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Set(PcModeState.NeedsAttention, "PC mode stopped: " + error.Message);
        }
        finally { Teardown(); reader?.Dispose(); timeEndPeriod(1); }
    }

    public void Dispose()
    {
        stop = true; enabled = false;
        // long enough for the un-hide commands to finish, so quitting gives the controller back now, not next launch
        if (thread is { IsAlive: true } && Thread.CurrentThread != thread) thread.Join(15000);
    }
}

/// <summary>The virtual pad games see: an Xbox 360 or a DualShock 4 (the owner's choice in the PC Mode window). Needs the ViGEmBus driver.</summary>
sealed class VirtualPad : IDisposable
{
    readonly ViGEmClient client; readonly IXbox360Controller? pad; readonly IDualShock4Controller? ds4; readonly int real;
    public int Slot { get; private set; } = -1;
    /// <summary>A DualShock 4 is not an XInput device, so it never has an XInput slot.</summary>
    public bool NoSlot => ds4 != null;
    public VirtualPad(int real, PcModeEntry.VirtualType type)
    {
        this.real = real;
        client = new ViGEmClient();
        if (type == PcModeEntry.VirtualType.DualShock4)
        {
            ds4 = client.CreateDualShock4Controller(); ds4.AutoSubmitReport = false;
            ds4.FeedbackReceived += (_, e) => ControllerRumble.Set(real, e.LargeMotor * 257, e.SmallMotor * 257);
            ds4.Connect();
        }
        else
        {
            pad = client.CreateXbox360Controller(); pad.AutoSubmitReport = false;
            pad.FeedbackReceived += (_, e) => ControllerRumble.Set(real, e.LargeMotor * 257, e.SmallMotor * 257); // a game's vibration goes on to the real pad
            pad.Connect();
            Refresh();
        }
    }
    /// <summary>Asks ViGEm for the XInput slot again; it may not know it straight after connecting.</summary>
    public int Refresh() { if (pad != null && Slot < 0) try { Slot = pad.UserIndex; } catch (Exception) { Slot = -1; } return Slot; }
    static readonly (ushort Mask, Xbox360Button Button)[] Buttons =
    [
        (PadButtons.Up, Xbox360Button.Up), (PadButtons.Down, Xbox360Button.Down), (PadButtons.Left, Xbox360Button.Left), (PadButtons.Right, Xbox360Button.Right),
        (PadButtons.Start, Xbox360Button.Start), (PadButtons.Back, Xbox360Button.Back), (PadButtons.L3, Xbox360Button.LeftThumb), (PadButtons.R3, Xbox360Button.RightThumb),
        (PadButtons.LB, Xbox360Button.LeftShoulder), (PadButtons.RB, Xbox360Button.RightShoulder), (PadButtons.Guide, Xbox360Button.Guide),
        (PadButtons.A, Xbox360Button.A), (PadButtons.B, Xbox360Button.B), (PadButtons.X, Xbox360Button.X), (PadButtons.Y, Xbox360Button.Y),
    ];
    static readonly (ushort Mask, DualShock4Button Button)[] Ds4Buttons =
    [
        (PadButtons.Start, DualShock4Button.Options), (PadButtons.Back, DualShock4Button.Share), (PadButtons.L3, DualShock4Button.ThumbLeft), (PadButtons.R3, DualShock4Button.ThumbRight),
        (PadButtons.LB, DualShock4Button.ShoulderLeft), (PadButtons.RB, DualShock4Button.ShoulderRight),
        (PadButtons.A, DualShock4Button.Cross), (PadButtons.B, DualShock4Button.Circle), (PadButtons.X, DualShock4Button.Square), (PadButtons.Y, DualShock4Button.Triangle),
    ];
    static byte Axis(short v) => (byte)((v + 32768) >> 8);          // XInput -32768..32767 to 0..255
    static byte AxisUp(short v) => (byte)(255 - Axis(v));           // DualShock 4 has Y increasing downwards
    public void Update(PadState s)
    {
        if (ds4 != null)
        {
            foreach (var (mask, button) in Ds4Buttons) ds4.SetButtonState(button, (s.Buttons & mask) != 0);
            ds4.SetButtonState(DualShock4Button.TriggerLeft, s.LeftTrigger > 30); ds4.SetButtonState(DualShock4Button.TriggerRight, s.RightTrigger > 30);
            ds4.SetButtonState(DualShock4SpecialButton.Ps, (s.Buttons & PadButtons.Guide) != 0);
            bool up = (s.Buttons & PadButtons.Up) != 0, down = (s.Buttons & PadButtons.Down) != 0, left = (s.Buttons & PadButtons.Left) != 0, right = (s.Buttons & PadButtons.Right) != 0;
            ds4.SetDPadDirection(up ? (right ? DualShock4DPadDirection.Northeast : left ? DualShock4DPadDirection.Northwest : DualShock4DPadDirection.North)
                : down ? (right ? DualShock4DPadDirection.Southeast : left ? DualShock4DPadDirection.Southwest : DualShock4DPadDirection.South)
                : right ? DualShock4DPadDirection.East : left ? DualShock4DPadDirection.West : DualShock4DPadDirection.None);
            ds4.SetSliderValue(DualShock4Slider.LeftTrigger, s.LeftTrigger); ds4.SetSliderValue(DualShock4Slider.RightTrigger, s.RightTrigger);
            ds4.SetAxisValue(DualShock4Axis.LeftThumbX, Axis(s.LeftX)); ds4.SetAxisValue(DualShock4Axis.LeftThumbY, AxisUp(s.LeftY));
            ds4.SetAxisValue(DualShock4Axis.RightThumbX, Axis(s.RightX)); ds4.SetAxisValue(DualShock4Axis.RightThumbY, AxisUp(s.RightY));
            ds4.SubmitReport();
            return;
        }
        foreach (var (mask, button) in Buttons) pad!.SetButtonState(button, (s.Buttons & mask) != 0);
        pad!.SetSliderValue(Xbox360Slider.LeftTrigger, s.LeftTrigger); pad.SetSliderValue(Xbox360Slider.RightTrigger, s.RightTrigger);
        pad.SetAxisValue(Xbox360Axis.LeftThumbX, s.LeftX); pad.SetAxisValue(Xbox360Axis.LeftThumbY, s.LeftY);
        pad.SetAxisValue(Xbox360Axis.RightThumbX, s.RightX); pad.SetAxisValue(Xbox360Axis.RightThumbY, s.RightY);
        pad.SubmitReport();
    }
    public void Dispose() { try { pad?.Disconnect(); ds4?.Disconnect(); } catch (Exception) { } client.Dispose(); ControllerRumble.Set(real, 0, 0); }
}

/// <summary>
/// The HidHide command line (Nefarius, BSD): hides the real pad from games and keeps this exe able to see it. Used only
/// by PC mode. Every device it hides is written to a recovery file first, so a crash can't leave a pad hidden for good.
/// </summary>
static class HidHide
{
    static readonly string[] Candidates =
    [
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Nefarius Software Solutions", "HidHide", "x64", "HidHideCLI.exe"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Nefarius Software Solutions", "HidHide", "HidHideCLI.exe"),
    ];
    public static string? Cli => Candidates.FirstOrDefault(File.Exists);
    public static bool Installed => Cli != null;
    static string RecoveryFile => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "x20ctl", "native", "pc-mode-hidden.txt");

    static string Run(params string[] args)
    {
        var info = new ProcessStartInfo(Cli ?? throw new IOException("HidHide isn't installed."), args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true };
        using var process = Process.Start(info) ?? throw new IOException("HidHide didn't start.");
        string output = process.StandardOutput.ReadToEnd();
        if (!process.WaitForExit(10000)) { try { process.Kill(); } catch (Exception) { } throw new IOException("HidHide didn't answer."); }
        return output;
    }

    /// <summary>The instance paths to hide for every gaming device present.</summary>
    public static List<string> GamingDevices() => HidHideDeviceList.WithXInputChildren(HidHideDeviceList.Parse(Run("--dev-gaming")), XInputDevices());

    /// <summary>Every present XInput device with its parent's instance path, read from Windows' device tree.</summary>
    static List<(string Id, string Parent, Guid Class)> XInputDevices()
    {
        var found = new List<(string, string, Guid)>();
        foreach (var cls in HidHideDeviceList.XInputClasses)
        {
            var guid = cls;
            var set = SetupDiGetClassDevsW(ref guid, IntPtr.Zero, IntPtr.Zero, 2 /* DIGCF_PRESENT */);
            if (set == IntPtr.Zero || set == new IntPtr(-1)) continue;
            try
            {
                var data = new SpDevinfoData { Size = (uint)Marshal.SizeOf<SpDevinfoData>() };
                for (uint i = 0; SetupDiEnumDeviceInfo(set, i, ref data); i++)
                    if (DeviceId(data.DevInst) is { } id && CM_Get_Parent(out uint parent, data.DevInst, 0) == 0 && DeviceId(parent) is { } parentId)
                        found.Add((id, parentId, cls));
            }
            finally { SetupDiDestroyDeviceInfoList(set); }
        }
        return found;
    }

    static string? DeviceId(uint devInst)
    {
        var buffer = new char[400];
        return CM_Get_Device_IDW(devInst, buffer, buffer.Length, 0) == 0 ? new string(buffer).TrimEnd('\0') : null;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct SpDevinfoData { public uint Size; public Guid ClassGuid; public uint DevInst; public IntPtr Reserved; }
    [DllImport("setupapi.dll", SetLastError = true)] static extern IntPtr SetupDiGetClassDevsW(ref Guid classGuid, IntPtr enumerator, IntPtr parent, uint flags);
    [DllImport("setupapi.dll", SetLastError = true)] static extern bool SetupDiEnumDeviceInfo(IntPtr set, uint index, ref SpDevinfoData data);
    [DllImport("setupapi.dll")] static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)] static extern int CM_Get_Device_IDW(uint devInst, char[] buffer, int length, uint flags);
    [DllImport("cfgmgr32.dll")] static extern int CM_Get_Parent(out uint parent, uint devInst, uint flags);
    /// <summary>HidHideCLI --cloak-state answers "--cloak-on" or "--cloak-off".</summary>
    public static bool CloakIsOn() { try { return Run("--cloak-state").Contains("--cloak-on", StringComparison.OrdinalIgnoreCase); } catch (Exception) { return false; } }
    public static void RegisterSelf() { if (Environment.ProcessPath is { } exe) Run("--app-reg", exe); }
    public static void Hide(IEnumerable<string> paths) { foreach (var p in paths) Run("--dev-hide", p); }
    public static void CloakOn() => Run("--cloak-on");
    public static void Unhide(IEnumerable<string> paths, bool restoreCloak)
    {
        try { foreach (var p in paths) Run("--dev-unhide", p); if (restoreCloak) Run("--cloak-off"); } catch (Exception) { }
    }
    public static void RememberForRecovery(IEnumerable<string> paths)
    {
        try { Directory.CreateDirectory(Path.GetDirectoryName(RecoveryFile)!); File.WriteAllLines(RecoveryFile, paths); } catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
    }
    public static void ForgetRecovery() { try { File.Delete(RecoveryFile); } catch (Exception error) when (error is IOException or UnauthorizedAccessException) { } }
    /// <summary>A previous run ended with a pad still hidden (a crash or a killed process): give it back.</summary>
    public static void RecoverFromCrash()
    {
        try
        {
            if (!File.Exists(RecoveryFile) || !Installed) return;
            Unhide(File.ReadAllLines(RecoveryFile).Where(l => l.Length > 0), restoreCloak: false);
            ForgetRecovery();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
    }
}
