using System.Diagnostics;
using System.Runtime.InteropServices;
namespace X20Ctl.Product.Scanner;

/// <summary>
/// The turbo function test (a tester's request, 10 Oct 2026: "nah like the function itself"). Turbo is a script inside
/// the controller, so it has no button of its own to see; what can be measured is its effect. The player turns turbo on
/// for A and holds A, and this samples A about once a millisecond (the scan's 20 ms timer is too slow for 10–30 Hz
/// turbo), recording every press and release. Then turbo goes off and A is held again, which should stay steady.
/// Read-only like the rest of the scan: XInput state reads only.
/// </summary>
public sealed class TurboSampler
{
    [DllImport("winmm.dll")] static extern uint timeBeginPeriod(uint ms);
    [DllImport("winmm.dll")] static extern uint timeEndPeriod(uint ms);
    public const int Mask = 4096; // A
    readonly List<(double T, bool Down)> edges = new();
    readonly Thread thread; volatile bool stop; int reads;

    /// <summary>Starts sampling; <paramref name="stepTime"/> gives the step's own time in seconds (after its countdown).</summary>
    public TurboSampler(WindowsReader reader, int slot, Func<double> stepTime)
    {
        thread = new Thread(() =>
        {
            timeBeginPeriod(1);
            try
            {
                bool last = false;
                while (!stop)
                {
                    if (reader.Read(slot, out var s))
                    {
                        reads++; bool down = (s.Pad.Buttons & Mask) == Mask;
                        if (down != last) { lock (edges) edges.Add((stepTime(), down)); last = down; }
                    }
                    Thread.Sleep(1);
                }
            }
            finally { timeEndPeriod(1); }
        }) { IsBackground = true, Priority = ThreadPriority.AboveNormal };
        thread.Start();
    }

    /// <summary>Stops sampling and measures the two hold windows (step seconds): turbo on, then turbo off.</summary>
    public Dictionary<string, object> Finish((double From, double To) turboOn, (double From, double To) turboOff, double stepSeconds)
    {
        stop = true; thread.Join(500);
        List<(double T, bool Down)> all; lock (edges) all = edges.ToList();
        var on = Window(all, turboOn); var off = Window(all, turboOff);
        bool repeats = (int)on["presses"] >= 3, steadyAfter = (int)off["presses"] <= 1 && (double)off["downShare"] > .5;
        string status = repeats ? (steadyAfter ? "turbo_observed_then_off" : (int)off["presses"] >= 3 ? "turbo_observed_still_on_after" : "turbo_observed")
            : (double)on["downShare"] > .5 ? "held_without_repeat" : "a_not_held";
        return new() {
            ["action"] = "turbo", ["status"] = status, ["button"] = "A", ["samplesPerSecond"] = stepSeconds > 0 ? Math.Round(reads / stepSeconds) : 0,
            ["turboOn"] = on, ["turboOff"] = off, ["edgeCount"] = all.Count,
            ["edges"] = all.Take(400).Select(e => new { t = Math.Round(e.T, 4), down = e.Down }).ToList(),
            ["interpretation"] = "Measured from XInput state about once a millisecond. Rates near the controller's own report rate can't be told apart; this is the effect of turbo, not its firmware setting."
        };
    }

    static Dictionary<string, object> Window(List<(double T, bool Down)> all, (double From, double To) w)
    {
        // the state at the window's start, then each change inside it
        bool down = all.LastOrDefault(e => e.T < w.From).Down;
        var inside = all.Where(e => e.T >= w.From && e.T < w.To).ToList();
        var rises = inside.Where(e => e.Down).Select(e => e.T).ToList();
        double downTime = 0, t = w.From;
        foreach (var e in inside) { if (down) downTime += e.T - t; t = e.T; down = e.Down; }
        if (down) downTime += w.To - t;
        double? hz = rises.Count >= 3 ? (rises.Count - 1) / (rises[^1] - rises[0]) : null;
        var periods = rises.Zip(rises.Skip(1), (a, b) => (b - a) * 1000).ToList();
        return new() {
            ["fromSeconds"] = w.From, ["toSeconds"] = w.To, ["presses"] = rises.Count,
            ["pressesPerSecond"] = hz is { } h ? Math.Round(h, 2) : 0,
            ["periodMsMin"] = periods.Count > 0 ? Math.Round(periods.Min(), 1) : 0, ["periodMsMax"] = periods.Count > 0 ? Math.Round(periods.Max(), 1) : 0,
            ["downShare"] = Math.Round(downTime / Math.Max(.001, w.To - w.From), 3)
        };
    }
}
