using X20Ctl.Zone.Diagnostics;

namespace X20Ctl.Mobile.Core;

/// <summary>
/// The input tester's measurements. Android reports a paired controller as a gamepad: key codes for buttons and
/// floats for axes. They are turned into the XInput layout so the same Zone statistics as the desktop apply.
/// This is ordinary gameplay input, read-only, and says nothing about the controller's configuration.
/// </summary>
public sealed class PadSession
{
    // XInput button masks
    public const ushort Up = 0x0001, Down = 0x0002, Left = 0x0004, Right = 0x0008, Start = 0x0010, Back = 0x0020,
        LeftStick = 0x0040, RightStick = 0x0080, LB = 0x0100, RB = 0x0200, Guide = 0x0400, A = 0x1000, B = 0x2000, X = 0x4000, Y = 0x8000;

    public static readonly (ushort Mask, string Name)[] Buttons =
    [
        (A, "A"), (B, "B"), (X, "X"), (Y, "Y"), (LB, "LB"), (RB, "RB"), (Back, "View"), (Start, "Menu"), (Guide, "Home"),
        (LeftStick, "L3"), (RightStick, "R3"), (Up, "Up"), (Down, "Down"), (Left, "Left"), (Right, "Right"),
    ];

    // android.view.KeyEvent key codes
    static readonly Dictionary<int, ushort> Keys = new()
    {
        [96] = A, [97] = B, [99] = X, [100] = Y, [102] = LB, [103] = RB, [106] = LeftStick, [107] = RightStick,
        [108] = Start, [109] = Back, [110] = Guide, [19] = Up, [20] = Down, [21] = Left, [22] = Right,
    };

    public static ushort? MaskForKey(int keyCode) => Keys.TryGetValue(keyCode, out var mask) ? mask : null;

    public StickStats LeftStats { get; } = new();
    public StickStats RightStats { get; } = new();
    public TriggerStats LeftTrigger { get; } = new();
    public TriggerStats RightTrigger { get; } = new();
    public ButtonStats Presses { get; } = new();
    public RateMeter Rate { get; } = new();
    public DriftObservation LeftDrift { get; private set; } = new();
    public DriftObservation RightDrift { get; private set; } = new();
    public ushort State { get; private set; }
    public string? DeviceName { get; set; }

    /// <summary>A button went down or up.</summary>
    public bool Key(int keyCode, bool down, double nowMs)
    {
        if (MaskForKey(keyCode) is not { } mask) return false;
        keys = down ? (ushort)(keys | mask) : (ushort)(keys & ~mask);
        Apply((ushort)(keys | hat), nowMs);
        return true;
    }

    /// <summary>
    /// One motion event. Android axes run -1..1 with Y pointing down; triggers 0..1. A hat (d-pad on axes) is folded
    /// into the d-pad buttons. Android reports the right stick on Z/RZ for most controllers.
    /// </summary>
    public void Motion(float lx, float ly, float rx, float ry, float lt, float rt, float hatX, float hatY, double nowMs)
    {
        short sx(float v) => (short)Math.Clamp(Math.Round(v * 32767), -32767, 32767);
        byte tr(float v) => (byte)Math.Clamp(Math.Round(v * 255), 0, 255);
        short lX = sx(lx), lY = sx(-ly), rX = sx(rx), rY = sx(-ry);
        LeftStats.Add(lX, lY); RightStats.Add(rX, rY);
        if (Math.Abs(lX) < 4915 && Math.Abs(lY) < 4915) LeftDrift.Add(lX, lY);
        if (Math.Abs(rX) < 4915 && Math.Abs(rY) < 4915) RightDrift.Add(rX, rY);
        LeftTrigger.Add(tr(lt)); RightTrigger.Add(tr(rt));
        ushort h = 0;
        if (hatX < -.5f) h |= Left; else if (hatX > .5f) h |= Right;
        if (hatY < -.5f) h |= Up; else if (hatY > .5f) h |= Down;
        hat = h;
        Apply((ushort)(keys | hat), nowMs);
        Rate.Add(nowMs);
    }

    // buttons held according to key events, and the d-pad according to the hat axes (controllers use one or the other)
    ushort keys, hat;

    void Apply(ushort state, double nowMs)
    {
        Presses.Add(state, nowMs, Buttons.Select(b => b.Mask));
        State = state;
    }

    public void Reset()
    {
        LeftStats.Reset(); RightStats.Reset(); LeftTrigger.Reset(); RightTrigger.Reset(); Presses.Reset(); Rate.Reset();
        LeftDrift = new(); RightDrift = new(); State = 0; keys = hat = 0;
    }

    public string Summary(double nowMs)
    {
        var s = new System.Text.StringBuilder();
        s.AppendLine($"Controller: {DeviceName ?? "none yet: press a button"}");
        s.AppendLine($"Input events: {Rate.Current(nowMs)}/s now, {Rate.Peak}/s peak (Android event rate, not the controller's report rate)");
        Stick("Left stick", LeftStats, LeftDrift); Stick("Right stick", RightStats, RightDrift);
        s.AppendLine($"Triggers: LT {LeftTrigger.Min}–{LeftTrigger.Max}{(LeftTrigger.FullTravel ? " full travel" : "")}, RT {RightTrigger.Min}–{RightTrigger.Max}{(RightTrigger.FullTravel ? " full travel" : "")}");
        var pressed = Buttons.Where(b => Presses[b.Mask].Presses > 0).Select(b => $"{b.Name}×{Presses[b.Mask].Presses}" + (Presses[b.Mask].Bounces > 0 ? $" ({Presses[b.Mask].Bounces} fast repeats)" : ""));
        s.AppendLine("Presses: " + (pressed.Any() ? string.Join(", ", pressed) : "none yet"));
        return s.ToString();

        void Stick(string name, StickStats st, DriftObservation drift)
        {
            if (!st.HasSamples) { s.AppendLine($"{name}: no movement yet"); return; }
            string rest = st.Rest is { } r ? $"rests at {r.X:0.0}%, {r.Y:0.0}%" : "rest not measured yet";
            string circle = st.CircularityError is { } c ? $", circularity error {c:0.0}%" : $", {st.Covered}/{StickStats.Sectors} directions covered";
            string dz = drift.Stable ? $", suggested inner deadzone {drift.RecommendedInner:0.0}%" : "";
            s.AppendLine($"{name}: {rest}{circle}{dz}");
        }
    }
}
