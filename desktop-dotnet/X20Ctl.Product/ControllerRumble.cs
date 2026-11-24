using System.Numerics;
using System.Runtime.InteropServices;
using X20Ctl.Zone.Lighting;
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

    public static int FindPhysicalSlot()
    {
        try
        {
            var buffer = new byte[16];
            return Enumerable.Range(0, 4).FirstOrDefault(i => !PcModeService.IsVirtual(i) && GetState((uint)i, buffer) == 0, -1);
        }
        catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException)
        {
            return -1;
        }
    }

    /// <summary>
    /// Reads live sticks, triggers, held count, and mapped action buttons directly from the active physical XInput slot
    /// so in-game footsteps, jumps, dashes, attacks, and trigger pulls keep updating even when minimized behind a game.
    /// </summary>
    public static bool TryReadLiveState(
        out double lx,
        out double ly,
        out double rx,
        out double ry,
        out double lt,
        out double rt,
        out int heldCount,
        out PadActionButtons actionButtons)
    {
        lx = ly = rx = ry = lt = rt = 0.0;
        heldCount = 0;
        actionButtons = PadActionButtons.None;
        try
        {
            var buf = new byte[16];
            for (int i = 0; i < 4; i++)
            {
                if (PcModeService.IsVirtual(i) || GetState((uint)i, buf) != 0)
                    continue;

                ushort buttons = BitConverter.ToUInt16(buf, 4);
                lt = buf[6] / 255.0;
                rt = buf[7] / 255.0;
                lx = Math.Clamp(BitConverter.ToInt16(buf, 8) / 32767.0, -1.0, 1.0);
                ly = Math.Clamp(BitConverter.ToInt16(buf, 10) / 32767.0, -1.0, 1.0);
                rx = Math.Clamp(BitConverter.ToInt16(buf, 12) / 32767.0, -1.0, 1.0);
                ry = Math.Clamp(BitConverter.ToInt16(buf, 14) / 32767.0, -1.0, 1.0);
                heldCount = BitOperations.PopCount(buttons);

                PadActionButtons mapped = PadActionButtons.None;
                if ((buttons & 0x1000) != 0) mapped |= PadActionButtons.A;
                if ((buttons & 0x2000) != 0) mapped |= PadActionButtons.B;
                if ((buttons & 0x4000) != 0) mapped |= PadActionButtons.X;
                if ((buttons & 0x8000) != 0) mapped |= PadActionButtons.Y;
                if ((buttons & 0x0100) != 0) mapped |= PadActionButtons.LB;
                if ((buttons & 0x0200) != 0) mapped |= PadActionButtons.RB;
                if ((buttons & 0x0040) != 0) mapped |= PadActionButtons.L3;
                if ((buttons & 0x0080) != 0) mapped |= PadActionButtons.R3;
                actionButtons = mapped;
                return true;
            }
            return false;
        }
        catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException)
        {
            return false;
        }
    }

    /// <summary>Returns whether a controller took the command.</summary>
    public static Task<bool> Test(bool left, bool right, int percent, int ms = 1000) => Test(left ? percent : 0, right ? percent : 0, ms);

    /// <summary>Each grip at its own strength (0..100); returns whether a controller took the command.</summary>
    public static async Task<bool> Test(int leftPercent, int rightPercent, int ms = 1000)
    {
        try
        {
            int slot = FindPhysicalSlot();
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
