using System.IO;
using X20Ctl.Product.Scanner;
using X20Ctl.Zone.Agent;
namespace X20Ctl.Product.Agent;

/// <summary>
/// The agent's drift reading on the real controller : asks the owner to let go, waits a moment for the
/// sticks to settle, then reads them for three seconds through XInput, the same read-only path as the Diagnostics tab.
/// The player's own slot first, never PC mode's virtual pad (which only repeats the real one, after its dead zones).
/// Nothing is written to the controller.
/// </summary>
public sealed class XInputDriftMeter(int player, Action<string, int> progress) : IDriftMeter
{
    public const int SettleSeconds = 2, ReadSeconds = 3;

    public async Task<DriftReport> Measure(IReadOnlyList<string> sticks, CancellationToken cancel)
    {
        using var reader = Open();
        if (reader == null) return new([], "Windows' controller reader (XInput) isn't available on this PC.");
        int slot = Find(reader, player);
        if (slot < 0) return new([], "No controller found. Connect it and ask again.");
        for (int s = SettleSeconds; s > 0; s--) { progress("Let go of the sticks", s + ReadSeconds); await Task.Delay(1000, cancel); }
        var samplers = sticks.ToDictionary(s => s, s => new DriftSampler(s));
        var reading = Task.Run(() =>
        {
            var until = DateTime.UtcNow.AddSeconds(ReadSeconds);
            while (DateTime.UtcNow < until && !cancel.IsCancellationRequested)
            {
                if (reader.Read(slot, out var state))
                    foreach (var (stick, sampler) in samplers)
                        if (stick == "LeftStick") sampler.Add(state.Pad.LX, state.Pad.LY); else sampler.Add(state.Pad.RX, state.Pad.RY);
                Thread.Sleep(1);
            }
        }, cancel);
        for (int s = ReadSeconds; s > 0 && !reading.IsCompleted; s--) { progress("Reading the sticks", s); await Task.WhenAny(reading, Task.Delay(1000, cancel)); }
        await reading;
        if (samplers.Values.Any(s => s.Count == 0)) return new([], "The controller stopped answering while it was being read.");
        if (samplers.Values.Any(s => s.Touched)) return new([], "A stick moved while it was being read, so the reading isn't drift. Let go of both sticks and ask again.");
        return new(samplers.Values.Select(s => s.Result()).ToList(), null);
    }

    static WindowsReader? Open()
    {
        foreach (var library in WindowsReader.Libraries)
            try { return new WindowsReader(library); }
            catch (Exception error) when (error is IOException or ArgumentException) { }
        return null;
    }

    /// <summary>The real controller's XInput slot (the player's own first, never PC mode's virtual pad), or -1.</summary>
    internal static int Slot(int player) { using var reader = Open(); return reader == null ? -1 : Find(reader, player); }

    /// <summary>What Windows reports for the controller's battery: empty, low, medium, full, or wired; null when it isn't
    /// reported (no controller, or a receiver that doesn't say). Read-only.</summary>
    internal static string? BatteryLevel(int player)
    {
        using var reader = Open();
        if (reader == null) return null;
        int slot = Find(reader, player);
        if (slot < 0) return null;
        var (type, level, known) = reader.Battery(slot);
        return type == "wired" ? "wired" : known ? level : null;
    }

    static int Find(WindowsReader reader, int player)
    {
        if (player is >= 0 and < 4 && !PcModeService.IsVirtual(player) && reader.Read(player, out _)) return player;
        for (int i = 0; i < 4; i++) if (!PcModeService.IsVirtual(i) && reader.Read(i, out _)) return i;
        return -1;
    }
}
