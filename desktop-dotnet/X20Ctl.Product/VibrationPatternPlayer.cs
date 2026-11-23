using System.Diagnostics;
using X20Ctl.Zone;
using X20Ctl.Zone.Lighting;

namespace X20Ctl.Product;

/// <summary>
/// Live ~30 Hz player executing <see cref="VibrationPattern"/> sequences across all 4 motors
/// (WebView2 trigger-rumble when BT independent mode is armed, or XInput linked fallback on 2.4G/USB).
/// Never issues hardware I/O when <see cref="ReviewSandbox.Active"/> is true.
/// </summary>
internal sealed class VibrationPatternPlayer
{
    readonly object gate = new();
    CancellationTokenSource? runCts;
    Task? runTask;
    int masterStrength = 85;
    int lastSlot = -1;
    bool xinputActive;
    bool streamActive;

    public bool IsPlaying { get; private set; }
    public VibrationPattern? ActivePattern { get; private set; }

    public event Action<HapticsFrame, int, bool>? FrameUpdated;

    public void UpdateMasterStrength(int percent)
    {
        Volatile.Write(ref masterStrength, Math.Clamp(percent, 0, 100));
    }

    public void Play(VibrationPattern pattern, int strengthPercent)
    {
        Stop();
        var norm = pattern.Normalized();
        Volatile.Write(ref masterStrength, Math.Clamp(strengthPercent, 0, 100));

        CancellationToken token;
        lock (gate)
        {
            runCts = new CancellationTokenSource();
            token = runCts.Token;
            ActivePattern = norm;
            IsPlaying = true;
        }

        runTask = Task.Run(() => LoopAsync(norm, token));
    }

    public void Stop()
    {
        CancellationTokenSource? cts;
        Task? task;
        lock (gate)
        {
            cts = runCts;
            task = runTask;
            runCts = null;
            runTask = null;
            IsPlaying = false;
            ActivePattern = null;
        }
        if (cts != null)
        {
            try { cts.Cancel(); } catch { }
            cts.Dispose();
        }
        if (task != null && Task.CurrentId != task.Id)
        {
            try { task.Wait(150); } catch { }
        }
        SilenceHardware();
        FrameUpdated?.Invoke(HapticsFrame.Zero, -1, false);
    }

    async Task LoopAsync(VibrationPattern pattern, CancellationToken ct)
    {
        long startTick = Stopwatch.GetTimestamp();
        try
        {
            while (!ct.IsCancellationRequested)
            {
                double elapsedMs = Stopwatch.GetElapsedTime(startTick).TotalMilliseconds;
                int strength = Volatile.Read(ref masterStrength);
                var hf = VibrationPatternEngine.EvaluateAt(pattern, elapsedMs, strength, out int stepIndex, out bool finished);

                if (!ReviewSandbox.Active)
                    DispatchHardware(hf);

                FrameUpdated?.Invoke(hf, finished ? -1 : stepIndex, !finished);

                if (finished)
                {
                    lock (gate)
                    {
                        IsPlaying = false;
                        ActivePattern = null;
                    }
                    SilenceHardware();
                    FrameUpdated?.Invoke(HapticsFrame.Zero, -1, false);
                    return;
                }

                await Task.Delay(33, ct);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            SilenceHardware();
        }
    }

    void DispatchHardware(HapticsFrame hf)
    {
        if (hf.IsSilent)
        {
            SilenceHardware();
            return;
        }

        var browser = Diagnostics.BrowserTriggerRumble.Shared;
        bool canUse4Motor = browser is { Armed: true, Failure: null, SupportsTriggerRumble: true, IsHostWindowActive: true };
        if (canUse4Motor && browser != null)
        {
            if (xinputActive && lastSlot >= 0)
            {
                ControllerRumble.Set(lastSlot, 0, 0);
                xinputActive = false;
            }
            browser.Stream4(hf.LeftGrip, hf.RightGrip, hf.LeftTrigger, hf.RightTrigger, 120);
            streamActive = true;
            return;
        }

        if (streamActive)
        {
            streamActive = false;
            browser?.StopStream();
        }

        int slot = ControllerRumble.FindPhysicalSlot();
        if (slot < 0)
        {
            xinputActive = false;
            return;
        }
        if (xinputActive && lastSlot >= 0 && lastSlot != slot)
            ControllerRumble.Set(lastSlot, 0, 0);

        int l = (int)Math.Clamp(Math.Round(Math.Max(hf.LeftGrip, hf.LeftTrigger) * 65535.0), 0, 65535);
        int r = (int)Math.Clamp(Math.Round(Math.Max(hf.RightGrip, hf.RightTrigger) * 65535.0), 0, 65535);
        ControllerRumble.Set(slot, l, r);
        lastSlot = slot;
        xinputActive = l > 0 || r > 0;
    }

    void SilenceHardware()
    {
        if (ReviewSandbox.Active) return;
        if (streamActive)
        {
            streamActive = false;
            Diagnostics.BrowserTriggerRumble.Shared?.StopStream();
        }
        if (xinputActive && lastSlot >= 0)
        {
            ControllerRumble.Set(lastSlot, 0, 0);
            xinputActive = false;
        }
    }
}
