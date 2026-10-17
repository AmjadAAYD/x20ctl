using System.Runtime.InteropServices;
namespace X20Ctl.Product.Agent;

/// <summary>
/// The agent answers with the controller itself (owner, 13 Oct 2026: "it must act like a controller"): a short rumble on
/// the real pad when a change is applied. XInput's own vibration call, the same a game makes; nothing is written to the
/// pad's memory. No controller, or no XInput, and it simply doesn't rumble.
/// </summary>
internal static class AgentRumble
{
    [StructLayout(LayoutKind.Sequential)] struct Vibration { public ushort Left, Right; }
    [DllImport("xinput1_4.dll", EntryPoint = "XInputSetState")] static extern uint SetState(uint slot, ref Vibration vibration);

    public static async Task Pulse(int player, int ms = 260)
    {
        try
        {
            int slot = await Task.Run(() => XInputDriftMeter.Slot(player));
            if (slot < 0) return;
            var on = new Vibration { Left = 20000, Right = 32000 };
            SetState((uint)slot, ref on);
            await Task.Delay(ms);
            var off = new Vibration();
            SetState((uint)slot, ref off);
        }
        catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException) { }
    }
}
