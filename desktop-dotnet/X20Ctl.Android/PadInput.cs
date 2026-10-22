using Android.Views;
using X20Ctl.Linux;

namespace X20Ctl.Droid;

/// <summary>
/// A controller paired with the phone, as Android reports it (key codes and axes), turned into the Studio's button names
/// for <see cref="GamepadReader.External"/>. Ordinary gameplay input, read-only: nothing is sent to the controller.
/// </summary>
internal static class PadInput
{
    public static ExternalPad Pad { get; } = new();
    static readonly HashSet<string> Keys = [];
    static string? hatX, hatY;
    static float lx, ly, rx, ry, lt, rt;
    static string? name;

    static readonly Dictionary<Keycode, string> Names = new()
    {
        [Keycode.ButtonA] = "A", [Keycode.ButtonB] = "B", [Keycode.ButtonX] = "X", [Keycode.ButtonY] = "Y",
        [Keycode.ButtonL1] = "LB", [Keycode.ButtonR1] = "RB", [Keycode.ButtonL2] = "LT", [Keycode.ButtonR2] = "RT",
        [Keycode.ButtonSelect] = "SELECT", [Keycode.ButtonStart] = "START", [Keycode.ButtonMode] = "HOME",
        [Keycode.ButtonThumbl] = "L3", [Keycode.ButtonThumbr] = "R3",
        [Keycode.DpadUp] = "DPAD_UP", [Keycode.DpadDown] = "DPAD_DOWN", [Keycode.DpadLeft] = "DPAD_LEFT", [Keycode.DpadRight] = "DPAD_RIGHT",
    };

    static bool FromPad(InputSourceType source) =>
        (source & InputSourceType.Gamepad) == InputSourceType.Gamepad || (source & InputSourceType.Joystick) == InputSourceType.Joystick;

    public static void Key(KeyEvent e)
    {
        if (!FromPad(e.Source) || e.RepeatCount > 0 || !Names.TryGetValue(e.KeyCode, out var button)) return;
        if (e.Action == KeyEventActions.Down) Keys.Add(button); else if (e.Action == KeyEventActions.Up) Keys.Remove(button); else return;
        name = e.Device?.Name ?? name;
        Push();
    }

    public static bool Motion(MotionEvent e)
    {
        if ((e.Source & InputSourceType.Joystick) != InputSourceType.Joystick || e.Action != MotionEventActions.Move) return false;
        name = e.Device?.Name ?? name;
        // most controllers report the right stick on Z/RZ; a few use RX/RY instead
        bool zStick = e.Device?.GetMotionRange(Axis.Rx, e.Source) is null;
        lx = e.GetAxisValue(Axis.X); ly = e.GetAxisValue(Axis.Y);
        rx = zStick ? e.GetAxisValue(Axis.Z) : e.GetAxisValue(Axis.Rx);
        ry = zStick ? e.GetAxisValue(Axis.Rz) : e.GetAxisValue(Axis.Ry);
        lt = Math.Max(e.GetAxisValue(Axis.Ltrigger), e.GetAxisValue(Axis.Brake));
        rt = Math.Max(e.GetAxisValue(Axis.Rtrigger), e.GetAxisValue(Axis.Gas));
        float hx = e.GetAxisValue(Axis.HatX), hy = e.GetAxisValue(Axis.HatY);
        hatX = hx < -.5f ? "DPAD_LEFT" : hx > .5f ? "DPAD_RIGHT" : null;
        hatY = hy < -.5f ? "DPAD_UP" : hy > .5f ? "DPAD_DOWN" : null;
        Push();
        return true;
    }

    static void Push()
    {
        var down = new HashSet<string>(Keys);
        if (hatX != null) down.Add(hatX);
        if (hatY != null) down.Add(hatY);
        if (lt > .5f) down.Add("LT");
        if (rt > .5f) down.Add("RT");
        // the Studio's readers use the same convention as Linux's joystick interface: up is negative
        Pad.Push(name, down, lx, ly, rx, ry, lt, rt);
    }
}
