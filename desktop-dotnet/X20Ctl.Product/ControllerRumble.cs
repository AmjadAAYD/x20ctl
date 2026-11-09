using System.Runtime.InteropServices;
namespace X20Ctl.Product;

/// <summary>A one-second buzz on the real pad through XInput's own vibration call, the same a game makes. Writes nothing to
/// the pad's memory. XInput has one left and one right motor command, so this can't choose a trigger.</summary>
internal static class ControllerRumble
{
    [StructLayout(LayoutKind.Sequential)] struct Vibration { public ushort Left, Right; }
    [DllImport("xinput1_4.dll", EntryPoint = "XInputSetState")] static extern uint SetState(uint slot, ref Vibration vibration);
    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")] static extern uint GetState(uint slot, byte[] state);

    public static void Set(int slot, int left, int right)
    {
        try { var v = new Vibration { Left = (ushort)left, Right = (ushort)right }; SetState((uint)slot, ref v); }
        catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException) { }
    }

    /// <summary>Returns whether a controller took the command.</summary>
    public static Task<bool> Test(bool left, bool right, int percent, int ms = 1000) => Test(left ? percent : 0, right ? percent : 0, ms);

    /// <summary>Each grip at its own strength (0..100); returns whether a controller took the command.</summary>
    public static async Task<bool> Test(int leftPercent, int rightPercent, int ms = 1000)
    {
        try
        {
            var buffer = new byte[16];
            int slot = Enumerable.Range(0, 4).FirstOrDefault(i => !PcModeService.IsVirtual(i) && GetState((uint)i, buffer) == 0, -1);
            if (slot < 0) return false;
            static ushort Level(int percent) => (ushort)(Math.Clamp(percent, 0, 100) * 65535 / 100);
            var on = new Vibration { Left = Level(leftPercent), Right = Level(rightPercent) };
            if (SetState((uint)slot, ref on) != 0) return false;
            await Task.Delay(ms);
            var off = new Vibration();
            SetState((uint)slot, ref off);
            return true;
        }
        catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException) { return false; }
    }
}
