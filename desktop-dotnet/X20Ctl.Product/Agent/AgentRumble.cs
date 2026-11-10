using System.Runtime.InteropServices;
using System.Windows;
namespace X20Ctl.Product.Agent;

/// <summary>
/// The agent answers with the controller itself (owner, 13 Oct 2026: "it must act like a controller"): a short rumble on
/// the real pad when a change is applied. Nothing is written to the pad's memory. No controller, and it simply doesn't rumble.
///  - The grips: XInput, the same call a game makes.
///  - The triggers (owner, 9 Nov 2026): through X20CTL's tiny built-in browser, the web Gamepad API's "trigger-rumble",
///    the one route that reached the X20 Pro's trigger motors (Windows.Gaming.Input and GameInput calls from X20CTL were
///    accepted and dropped). It works once a button on the controller has been pressed since X20CTL opened; before that,
///    only the grips rumble.
/// </summary>
internal static class AgentRumble
{
    [StructLayout(LayoutKind.Sequential)] struct Vibration { public ushort Left, Right; }
    [DllImport("xinput1_4.dll", EntryPoint = "XInputSetState")] static extern uint SetState(uint slot, ref Vibration vibration);

    /// <summary>A review's record of the rumbles asked for (when, how long, how strong): a review never touches the real
    /// controller, so in one nothing is sent and each pulse is only noted here.</summary>
    internal static Action<int, ushort, ushort>? ReviewPulse;

    /// <summary>One rumble: the grips at left/right, and the triggers at <paramref name="triggers"/> (0..1) once the
    /// built-in browser can reach them.</summary>
    public static async Task Pulse(int player, int ms = 260, ushort left = 20000, ushort right = 32000, double triggers = 0)
    {
        if (ReviewSandbox.Active) { ReviewPulse?.Invoke(ms, left, right); await Task.Delay(ms); return; }
        if (triggers > 0 && Diagnostics.BrowserTriggerRumble.Shared is { Armed: true } browser)
            Application.Current?.Dispatcher.BeginInvoke(() => _ = browser.Play(triggers, triggers, ms));
        if (left == 0 && right == 0) { await Task.Delay(ms); return; }
        try
        {
            int slot = await Task.Run(() => XInputDriftMeter.Slot(player));
            if (slot < 0) return;
            var on = new Vibration { Left = left, Right = right };
            SetState((uint)slot, ref on);
            await Task.Delay(ms);
            var off = new Vibration();
            SetState((uint)slot, ref off);
        }
        catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException) { }
    }
}
