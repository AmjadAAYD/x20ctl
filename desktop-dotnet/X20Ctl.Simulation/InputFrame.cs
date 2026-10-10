namespace X20Ctl.Simulation;

[Flags]
public enum Buttons : ushort
{
    None = 0, Up = 1, Down = 2, Left = 4, Right = 8,
    Start = 16, Back = 32, L3 = 64, R3 = 128, LB = 256, RB = 512,
    A = 4096, B = 8192, X = 16384, Y = 32768
}

public enum Scenario { Standard, HeldButton, Disconnected, RawInputFallback, FourSlots, SourceChange, Incomplete }
public record InputFrame(int Slot, bool Connected, string SourceKey, double TimeMs, Buttons Buttons,
    short LX = 0, short LY = 0, short RX = 0, short RY = 0, byte LT = 0, byte RT = 0)
{
    public bool IsSimulation => true;
    public double LeftX => NormalizeAxis(LX);
    public double LeftY => NormalizeAxis(LY);
    public double LeftTrigger => LT / 255d;
    public static double NormalizeAxis(short value) => value < 0 ? value / 32768d : value / 32767d;
}

public static class Simulator
{
    public static InputFrame Read(double timeMs, Scenario scenario, int slot = 0)
    {
        if (!double.IsFinite(timeMs) || timeMs < 0) throw new ArgumentOutOfRangeException(nameof(timeMs));
        if (slot is < 0 or > 3) throw new ArgumentOutOfRangeException(nameof(slot));
        bool connected = scenario != Scenario.Disconnected && (slot == 0 || scenario == Scenario.FourSlots);
        if (scenario == Scenario.Incomplete && timeMs >= 1200) connected = false;
        bool raw = scenario == Scenario.RawInputFallback || (scenario == Scenario.SourceChange && timeMs >= 2000);
        string source = raw ? "simulation:raw-input:gamepad-1" : $"simulation:xinput:{slot}";
        if (!connected) return new(slot, false, source, timeMs, Buttons.None);
        double t = timeMs % 4000;
        Buttons buttons = scenario is Scenario.HeldButton or Scenario.Incomplete ? Buttons.A :
            t is >= 250 and < 1000 ? Buttons.A : t is >= 1800 and < 2200 ? Buttons.LB :
            t is >= 2600 and < 3000 ? Buttons.Up | Buttons.Right : Buttons.None;
        static short Axis(double v) => (short)Math.Round(v * (v < 0 ? 32768 : 32767));
        double triggerPhase = timeMs % 2000 / 1000;
        byte lt = (byte)Math.Round(255 * (triggerPhase <= 1 ? triggerPhase : 2 - triggerPhase));
        return new(slot, true, source, timeMs, buttons,
            Axis(Math.Sin(timeMs * Math.PI / 2000)), Axis(Math.Cos(timeMs * Math.PI / 2000) * .75),
            Axis(Math.Sin(timeMs * Math.PI / 2500 + .7) * .55), Axis(Math.Cos(timeMs * Math.PI / 2500 + .7) * .55),
            lt, (byte)(255 - lt));
    }
}
