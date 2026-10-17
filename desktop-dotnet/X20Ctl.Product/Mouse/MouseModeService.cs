using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using X20Ctl.Product.Scanner;
using X20Ctl.Zone.Mouse;
using X20Ctl.Zone.PcMode;
namespace X20Ctl.Product;

/// <summary>
/// Controller as a mouse (the Studio's Mouse tab). A background thread reads the controller (documented XInput, read
/// only) and sends pointer moves, scrolling, clicks and keys through Windows' SendInput. Off by default and off at every
/// launch; hold View + Y for a second to switch it from the controller. While it's on, PC mode feeds games a resting
/// controller and X20CTL's own controller navigation pauses, so one press never does two things.
/// </summary>
public sealed class MouseModeService : IDisposable
{
    readonly string path;
    readonly Thread? thread;
    readonly object gate = new();
    MouseProfile profile;
    volatile bool active, stop, reload;
    public event Action? Changed;
    /// <summary>Switched from the controller: a short line for a notification.</summary>
    public event Action<string>? Notice;
    public PcModeService? PcMode { get; set; }
    public string Status { get; private set; } = "Off.";
    public int OwnerPlayer { get; private set; } = -1;
    public string OwnerModel { get; private set; } = "";
    public bool Previewing { get; set; }
    public bool Connected { get; private set; }
    PadState latest;
    public PadState Latest { get { lock(gate) return latest; } }
    string ContextPath(int player,string model) => Path.Combine(Path.GetDirectoryName(path)!, $"mouse-player{player + 1}-{model}.json");
    public MouseProfile ProfileFor(int player,string model) => MouseProfile.Load(ContextPath(player,model));
    public void ObserveFor(int player,string model)
    {
        if(active || Previewing)return;
        var next=ProfileFor(player,model);
        if(next.SourceSlot<0)return;
        lock(gate){OwnerPlayer=player;OwnerModel=model;profile=next;latest=default;}
        reload=true;Connected=false;
    }
    public void SaveFor(int player,string model,MouseProfile next)
    {
        next.Save(ContextPath(player,model));
        if(OwnerPlayer==player && OwnerModel==model) { lock(gate) profile=next.Clone(); reload=true; Changed?.Invoke(); }
    }
    public void TakeControl(int player,string model,bool preview=false)
    {
        var next=ProfileFor(player,model);
        if(next.SourceSlot<0) throw new InvalidOperationException("Choose a physical XInput source first.");
        Active=false; Previewing=preview; lock(gate) { OwnerPlayer=player;OwnerModel=model;profile=next;latest=default; }
        reload=true; Connected=false; Active=true;
    }

    public MouseModeService(string path, bool listen = true)
    {
        this.path = path; profile = MouseProfile.Load(path);
        if (ReviewSandbox.Active || !listen) return;
        thread = new Thread(Run) { IsBackground = true, Name = "X20CTL mouse", Priority = ThreadPriority.AboveNormal };
        thread.Start();
    }

    public bool Active
    {
        get => active;
        set { if(value && Profile.SourceSlot<0) return; if (active == value) return; active = value; if (PcMode != null) PcMode.Mute = value; Changed?.Invoke(); }
    }

    public MouseProfile Profile { get { lock (gate) return profile.Clone(); } }
    public void Save(MouseProfile next)
    {
        next.Validate(); next.Save(path);
        lock (gate) profile = next.Clone();
        reload = true; Changed?.Invoke();
    }

    [DllImport("winmm.dll")] static extern uint timeBeginPeriod(uint period);
    [DllImport("winmm.dll")] static extern uint timeEndPeriod(uint period);

    void Run()
    {
        timeBeginPeriod(1);
        WindowsReader? reader = null; MouseMapper? mapper = null; MouseProfile p = Profile;
        var clock = Stopwatch.StartNew(); double last = 0; int slot = -1;
        var combo = new HoldCombo(p.ToggleMask, p.HoldMilliseconds);
        var repeat = new Dictionary<MouseAction, double>(); // held keys and when they next repeat
        void Release()
        {
            if (mapper != null) foreach (var (action, _) in mapper.Release()) MouseOutput.Act(action, false);
            mapper = null; repeat.Clear();
        }
        try
        {
            reader = new WindowsReader(WindowsReader.Libraries[0]);
            while (!stop)
            {
                if (reload) { reload = false; p = Profile; Release(); slot=-1; combo=new(p.ToggleMask,p.HoldMilliseconds); }
                double now = clock.Elapsed.TotalMilliseconds, dt = (now - last) / 1000; last = now;
                if (slot < 0 || PcModeService.IsVirtual(slot) || !reader.Read(slot, out var state))
                {
                    slot = -1;
                    if(p.SourceSlot>=0 && !PcModeService.IsVirtual(p.SourceSlot) && reader.Read(p.SourceSlot,out _)) slot=p.SourceSlot;
                    Connected=false; lock(gate) latest=default;
                    if (slot < 0) { Release(); SetStatus(active ? $"Waiting for Player {OwnerPlayer+1}'s selected input source." : "Off."); Thread.Sleep(250); continue; }
                    continue;
                }
                var pad = new PadState(state.Pad.Buttons, state.Pad.LT, state.Pad.RT, state.Pad.LX, state.Pad.LY, state.Pad.RX, state.Pad.RY);
                Connected=true; lock(gate) latest=pad;
                if (p.Combo && combo.Update(pad.Buttons, (long)now))
                {
                    Active = !active;
                    Notice?.Invoke(active ? "Mouse Mode enabled. Hold the configured shortcut to turn it off." : "Controller is a controller again.");
                }
                if (!active || Previewing) { Release(); SetStatus(Previewing ? "Pointer preview · Windows output paused." : "Off."); Thread.Sleep(15); continue; }
                SetStatus($"On. The {p.PointerStick.ToLowerInvariant()} stick moves the pointer.");
                mapper ??= new MouseMapper(p);
                var frame = mapper.Step(pad, dt);
                if (frame.Dx != 0 || frame.Dy != 0) MouseOutput.Move(frame.Dx, frame.Dy);
                if (frame.Wheel != 0) MouseOutput.Wheel(frame.Wheel, false);
                if (frame.HWheel != 0) MouseOutput.Wheel(frame.HWheel, true);
                foreach (var (action, down) in frame.Edges)
                {
                    MouseOutput.Act(action, down);
                    if (down && MouseOutput.Repeats(action)) repeat[action] = now + 400; else repeat.Remove(action);
                }
                // keys held down repeat like a held keyboard key: after 0.4 s, about 16 times a second
                foreach (var (action, at) in repeat.ToList()) if (now >= at) { MouseOutput.Act(action, true); repeat[action] = now + 60; }
                Thread.Sleep(4);
            }
        }
        catch (Exception error) when (error is IOException or ArgumentException) { SetStatus("Can't read controllers here: " + error.Message); }
        finally { Release(); reader?.Dispose(); timeEndPeriod(1); }
    }

    void SetStatus(string text) { if (Status == text) return; Status = text; Changed?.Invoke(); }

    public void Dispose() { stop = true; Active = false; thread?.Join(500); }
}

/// <summary>Windows' SendInput: relative pointer moves, the wheel, the five mouse buttons and a few keys.</summary>
static class MouseOutput
{
    [StructLayout(LayoutKind.Sequential)] struct Input { public uint Type; public Union U; }
    [StructLayout(LayoutKind.Explicit)] struct Union { [FieldOffset(0)] public MouseInput Mouse; [FieldOffset(0)] public KeyInput Key; }
    [StructLayout(LayoutKind.Sequential)] struct MouseInput { public int Dx, Dy; public uint Data, Flags, Time; public IntPtr Extra; }
    [StructLayout(LayoutKind.Sequential)] struct KeyInput { public ushort Vk, Scan; public uint Flags, Time; public IntPtr Extra; }
    [DllImport("user32.dll", SetLastError = true)] static extern uint SendInput(uint count, Input[] inputs, int size);
    const uint MOVE = 0x1, LEFTDOWN = 0x2, LEFTUP = 0x4, RIGHTDOWN = 0x8, RIGHTUP = 0x10, MIDDLEDOWN = 0x20, MIDDLEUP = 0x40, XDOWN = 0x80, XUP = 0x100, WHEEL = 0x800, HWHEEL = 0x1000;
    const uint KEYUP = 0x2, EXTENDED = 0x1;

    static void Send(params Input[] inputs) => SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
    static Input Mouse(uint flags, int dx = 0, int dy = 0, uint data = 0) => new() { Type = 0, U = new() { Mouse = new() { Dx = dx, Dy = dy, Flags = flags, Data = data } } };
    static Input Key(ushort vk, bool down) => new() { Type = 1, U = new() { Key = new() { Vk = vk, Flags = (down ? 0 : KEYUP) | (Extended(vk) ? EXTENDED : 0) } } };
    static bool Extended(ushort vk) => vk is 0x21 or 0x22 or 0x25 or 0x26 or 0x27 or 0x28 or 0x5B;

    public static void Move(int dx, int dy) => Send(Mouse(MOVE, dx, dy));
    public static void Wheel(int amount, bool horizontal) => Send(Mouse(horizontal ? HWHEEL : WHEEL, data: unchecked((uint)amount)));

    public static bool Repeats(MouseAction a) => a is MouseAction.Up or MouseAction.Down or MouseAction.Left or MouseAction.Right or MouseAction.Backspace
        or MouseAction.PageUp or MouseAction.PageDown or MouseAction.VolumeUp or MouseAction.VolumeDown or MouseAction.Space;

    public static void Act(MouseAction action, bool down)
    {
        switch (action)
        {
            case MouseAction.LeftClick: Send(Mouse(down ? LEFTDOWN : LEFTUP)); break;
            case MouseAction.RightClick: Send(Mouse(down ? RIGHTDOWN : RIGHTUP)); break;
            case MouseAction.MiddleClick: Send(Mouse(down ? MIDDLEDOWN : MIDDLEUP)); break;
            case MouseAction.Back: Send(Mouse(down ? XDOWN : XUP, data: 1)); break;
            case MouseAction.Forward: Send(Mouse(down ? XDOWN : XUP, data: 2)); break;
            case MouseAction.TaskView: if (down) Send(Key(0x5B, true), Key(0x09, true)); else Send(Key(0x09, false), Key(0x5B, false)); break;
            case MouseAction.OnScreenKeyboard:
                if (down) try { Process.Start(new ProcessStartInfo("osk.exe") { UseShellExecute = true })?.Dispose(); } catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException) { }
                break;
            default:
                if (VirtualKey(action) is { } vk) Send(Key(vk, down));
                break;
        }
    }

    static ushort? VirtualKey(MouseAction a) => a switch
    {
        MouseAction.Enter => 0x0D, MouseAction.Escape => 0x1B, MouseAction.Tab => 0x09, MouseAction.Space => 0x20, MouseAction.Backspace => 0x08,
        MouseAction.Windows => 0x5B, MouseAction.Up => 0x26, MouseAction.Down => 0x28, MouseAction.Left => 0x25, MouseAction.Right => 0x27,
        MouseAction.PageUp => 0x21, MouseAction.PageDown => 0x22, MouseAction.VolumeUp => 0xAF, MouseAction.VolumeDown => 0xAE,
        MouseAction.Mute => 0xAD, MouseAction.PlayPause => 0xB3, _ => null,
    };
}
