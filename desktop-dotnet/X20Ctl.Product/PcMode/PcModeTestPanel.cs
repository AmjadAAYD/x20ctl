using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using X20Ctl.Product.Scanner;
using X20Ctl.Zone.PcMode;
namespace X20Ctl.Product;

/// <summary>
/// The PC Mode tester (owner, 6 Nov 2026: the hardware tester could not see the virtual controller): reads what a game would
/// read, the virtual Xbox pad's own XInput slot, and shows it beside the physical pad it came from. Read only. It runs while the
/// tab is shown and stops when it is left.
/// </summary>
public sealed class PcModeTestPanel : StackPanel
{
    readonly TextBlock game = Kit.Text("", 15, "DS.Text"), real = Kit.Text("", 15, "DS.TextSoft"), note = Kit.Text("", 14.5, "DS.TextSoft");
    readonly DispatcherTimer timer = new(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(33) };
    WindowsReader? reader; readonly PcModeService service;
    readonly TextBlock sent = Kit.Text("", 15, "DS.Text"), after = Kit.Text("", 15, "DS.Text");

    public PcModeTestPanel(PcModeService service)
    {
        this.service = service;
        Margin = new(0, 12, 0, 0);
        foreach (var t in new[] { game, real, note }) { t.TextWrapping = TextWrapping.Wrap; t.TextTrimming = TextTrimming.None; t.Margin = new(0, 0, 0, 12); }
        sent.TextWrapping = after.TextWrapping = TextWrapping.Wrap; sent.Margin = after.Margin = new(0, 0, 0, 12);
        Children.Add(note);
        Children.Add(Kit.Text("1. Your controller sends", 16, "DS.Text", FontWeights.SemiBold));
        Children.Add(sent);
        Children.Add(Kit.Text("2. After your settings (turbo, curves, combos)", 16, "DS.Text", FontWeights.SemiBold));
        Children.Add(after);
        Children.Add(Kit.Text("3. What Windows reads from the virtual controller", 16, "DS.Text", FontWeights.SemiBold));
        Children.Add(game);
        Children.Add(Kit.Text("(the physical controller, if Windows can still see it)", 16, "DS.Text", FontWeights.SemiBold).Also(t => t.Margin = new(0, 6, 0, 0)));
        Children.Add(real);
        timer.Tick += (_, _) => Tick();
        Loaded += (_, _) => { try { reader ??= new WindowsReader("XInput1_4.dll"); } catch (Exception) { reader = null; } timer.Start(); };
        Unloaded += (_, _) => { timer.Stop(); reader?.Dispose(); reader = null; };
    }

    static Pad ToPad(PadState s) => new() { Buttons = s.Buttons, LT = s.LeftTrigger, RT = s.RightTrigger, LX = s.LeftX, LY = s.LeftY, RX = s.RightX, RY = s.RightY };

    static string Describe(Pad p)
    {
        var held = PadButtons.Mask.Where(m => (p.Buttons & m.Value) != 0).Select(m => m.Key).ToList();
        return (held.Count == 0 ? "no buttons" : string.Join(" + ", held)) + $"\nLeft stick {p.LX}, {p.LY}   Right stick {p.RX}, {p.RY}\nTriggers {p.LT}, {p.RT}";
    }

    void Tick()
    {
        var snap = service.Latest;
        if (snap == null) { sent.Text = after.Text = "PC mode is not running yet. Switch it on (Status tab) with a controller connected."; }
        else { sent.Text = Describe(ToPad(snap.Input)); after.Text = Describe(ToPad(snap.Output)) + $"\nReports sent to the virtual controller: {snap.Sent}"; }
        if (reader == null) { note.Text = "XInput could not be opened, so nothing can be shown."; return; }
        if (PcModeEntry.Chosen == PcModeEntry.VirtualType.DualShock4 && snap != null)
        {   // a PlayStation 4 pad is not an XInput device, so step 3 cannot be read here: check it in a game or the hardware tester
            note.Text = $"The virtual controller is a PlayStation 4 pad, so Windows' XInput cannot read it here. Steps 1 and 2 show what is sent to it ({snap.Sent} reports). Check the game or hardware tester for step 3.";
            game.Text = "–"; real.Text = "–"; return;
        }
        int virtualSlot = PcModeService.VirtualSlot;
        if (virtualSlot < 0) { note.Text = "No virtual controller yet. Switch PC mode on (Status tab) and keep a controller connected."; game.Text = "–"; real.Text = "–"; return; }
        note.Text = $"Virtual controller found at XInput slot {virtualSlot + 1}. Press buttons and move the sticks.";
        game.Text = reader.Read(virtualSlot, out var v) ? Describe(v.Pad) : "The virtual controller did not answer.";
        int physical = Enumerable.Range(0, 4).FirstOrDefault(s => s != virtualSlot && !PcModeService.IsVirtual(s) && reader.Read(s, out _), -1);
        real.Text = physical < 0 ? "The physical controller is hidden from games by PC mode, so XInput cannot show it here." : reader.Read(physical, out var r) ? Describe(r.Pad) : "–";
    }
}
