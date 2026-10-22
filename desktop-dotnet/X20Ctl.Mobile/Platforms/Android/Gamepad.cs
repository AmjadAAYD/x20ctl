using System.Diagnostics;
using Android.Views;
using X20Ctl.Mobile.Core;

namespace X20Ctl.Mobile;

/// <summary>Feeds Android's gamepad events into the input tester's <see cref="PadSession"/>. Read-only gameplay input.</summary>
public static class Gamepad
{
    public static PadSession Pad { get; } = new();
    /// <summary>True while the input tester page is on screen.</summary>
    public static bool Active { get; set; }
    static readonly Stopwatch Clock = Stopwatch.StartNew();
    public static double Now => Clock.Elapsed.TotalMilliseconds;

    static bool IsPad(InputSourceType source) =>
        (source & InputSourceType.Gamepad) == InputSourceType.Gamepad || (source & InputSourceType.Joystick) == InputSourceType.Joystick;

    public static bool Handle(KeyEvent e)
    {
        if (!Active || !IsPad(e.Source) || e.Action is not (KeyEventActions.Down or KeyEventActions.Up)) return false;
        if (e.RepeatCount > 0) return PadSession.MaskForKey((int)e.KeyCode) is not null;
        Pad.DeviceName = e.Device?.Name ?? Pad.DeviceName;
        return Pad.Key((int)e.KeyCode, e.Action == KeyEventActions.Down, Now);
    }

    public static bool Handle(MotionEvent e)
    {
        if (!Active || (e.Source & InputSourceType.Joystick) != InputSourceType.Joystick || e.Action != MotionEventActions.Move) return false;
        Pad.DeviceName = e.Device?.Name ?? Pad.DeviceName;
        // Most controllers put the right stick on Z/RZ; a few report it on RX/RY instead.
        bool zStick = e.Device?.GetMotionRange(Axis.Rx, e.Source) is null;
        float rx = zStick ? e.GetAxisValue(Axis.Z) : e.GetAxisValue(Axis.Rx);
        float ry = zStick ? e.GetAxisValue(Axis.Rz) : e.GetAxisValue(Axis.Ry);
        float lt = Math.Max(e.GetAxisValue(Axis.Ltrigger), e.GetAxisValue(Axis.Brake));
        float rt = Math.Max(e.GetAxisValue(Axis.Rtrigger), e.GetAxisValue(Axis.Gas));
        Pad.Motion(e.GetAxisValue(Axis.X), e.GetAxisValue(Axis.Y), rx, ry, lt, rt, e.GetAxisValue(Axis.HatX), e.GetAxisValue(Axis.HatY), Now);
        return true;
    }
}
