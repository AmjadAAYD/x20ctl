using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using X20Ctl.Zone;
using X20Ctl.Zone.D10;
using X20Ctl.Zone.PcMode;

namespace X20Ctl.Product;

public sealed record D10ProbeInfo(bool HasWinUsbDevice, D10LinkKind DetectedLink, string? InstanceId, string Summary);

internal static class D10UsbClient
{
    public static D10ProbeInfo ProbeStatus()
    {
        try
        {
            var candidates = D10DirectSession.D10WinUsbNative.EnumeratePaths();
            if (candidates.Count == 0)
                return new(false, D10LinkKind.Disconnected, null, "No Direct USB (WinUSB) D10 device active. Standard XInput remains available.");

            foreach (var c in candidates)
            {
                try
                {
                    using var handle = D10DirectSession.D10WinUsbHandle.Open(c.DevicePath);
                    return new(true, handle.LinkKind, c.InstanceId, $"Detected D10 via {handle.LinkKind} ({c.InstanceId}).");
                }
                catch { }
            }
            return new(false, D10LinkKind.Disconnected, candidates[0].InstanceId, "D10 path present but WinUSB handshake did not match wired EP83 or 2.4G C0/5A+5C.");
        }
        catch (Exception ex)
        {
            return new(false, D10LinkKind.Disconnected, null, "Probe failed: " + ex.Message);
        }
    }

    public static D10DirectSession StartSession(
        D10MotionSettings settings,
        D10CalibrationData calibration,
        ButtonDraft buttons,
        CurveDraft curves,
        MacroDraft macros,
        VibrationDraft vibration) =>
        D10DirectSession.Open(settings, calibration, buttons, curves, macros, vibration);

    public static async Task TryQuickPulseAsync(bool left, int percent, CancellationToken cancel = default)
    {
        int clamped = Math.Clamp(percent, 0, 100);
        byte strength = (byte)(clamped * 255 / 100);
        var candidates = D10DirectSession.D10WinUsbNative.EnumeratePaths();
        if (candidates.Count > 0)
        {
            foreach (var c in candidates)
            {
                try
                {
                    using var dev = D10DirectSession.D10WinUsbHandle.Open(c.DevicePath);
                    dev.WriteRumble(left ? strength : (byte)0, left ? (byte)0 : strength);
                    try { await Task.Delay(450, cancel); }
                    finally
                    {
                        try { dev.WriteRumble(0, 0); } catch { }
                    }
                    return;
                }
                catch when (!cancel.IsCancellationRequested) { }
            }
        }

        // Standard XInput fallback when the controller is running on default Windows drivers.
        bool sent = await ControllerRumble.Test(left ? clamped : 0, left ? 0 : clamped, 450);
        if (!sent)
            throw new IOException("No D10 Direct USB or XInput controller found.");
    }

    public static Task TryQuickStopAsync()
    {
        foreach (var c in D10DirectSession.D10WinUsbNative.EnumeratePaths())
        {
            try
            {
                using var dev = D10DirectSession.D10WinUsbHandle.Open(c.DevicePath);
                dev.WriteRumble(0, 0);
                return Task.CompletedTask;
            }
            catch { }
        }
        _ = ControllerRumble.Test(0, 0, 50);
        return Task.CompletedTask;
    }
}

public sealed class D10DirectSession : IDisposable
{
    private readonly D10WinUsbHandle usb;
    private readonly D10MotionProcessor fusion = new();
    private readonly object draftSync = new();
    private readonly SemaphoreSlim calGate = new(1, 1);
    private readonly CancellationTokenSource stop = new();

    private ViGEmClient? vigem;
    private IXbox360Controller? vpad;
    private PadProcessor? processor;
    private volatile D10MotionSettings motionSettings;
    private volatile bool motionRuntimeActive = true;
    private volatile int leftScalePct = 100, rightScalePct = 100;
    private volatile bool rumbleCurrentlyActive;

    private D10Calibrator? activeCalibrator;
    private TaskCompletionSource<D10CalibrationData>? calCompletion;
    private D10MultiPoseCalibrator? activeMultiCalibrator;
    private TaskCompletionSource<D10CalibrationData>? multiCalCompletion;
    private D10MultiPoseStatus? lastMultiStatus;

    private readonly D10ShortcutDetector shortcutDetector = new();
    private readonly D10MacroPlayer macroPlayer = new();
    private readonly Dictionary<string, string?> heldHostTokens = new() { ["M1"] = null, ["M2"] = null, ["M"] = null };
    private readonly Dictionary<string, MacroEvent[]> compiledMacros = new() { ["M1"] = [], ["M2"] = [] };

    private D10InputFrame latestInput;
    private D10ExtraButtons latestExtra = D10ExtraButtons.None;
    private bool prevToggleDown, prevRecenterDown, prevMacroM1Down, prevMacroM2Down;
    private (short Gx, short Gy, short Gz) prevRawGyro;
    private bool hasPrevGyro;
    private long lastInputMs, lastMotionMs, nonZeroRumbleStartMs;
    private volatile bool shortcutPulseActive;

    private Thread? inputThread;
    private Thread? motionThread;
    private bool disposed;

    public D10LinkKind LinkKind => usb.LinkKind;
    public string DeviceDescription { get; }

    public event Action<D10LiveTelemetry>? TelemetryUpdated;
    public event Action<string>? StatusChanged;
    public event Action<string>? Faulted;
    public event Action<int>? QuickSlotRequested;
    public event Action<D10CalibrationData>? CalibrationUpdated;
    public event Action? QuickCalibrateRequested;

    private D10DirectSession(D10WinUsbHandle usb, string desc, D10MotionSettings settings, D10CalibrationData cal)
    {
        this.usb = usb;
        DeviceDescription = desc;
        motionSettings = settings.Clone().Validated();
        fusion.Settings = motionSettings;
        fusion.Calibration = cal;
    }

    internal static D10DirectSession Open(
        D10MotionSettings settings,
        D10CalibrationData calibration,
        ButtonDraft buttons,
        CurveDraft curves,
        MacroDraft macros,
        VibrationDraft vibration)
    {
        var candidates = D10WinUsbNative.EnumeratePaths();
        Exception? lastErr = null;
        foreach (var c in candidates)
        {
            D10WinUsbHandle? handle = null;
            try
            {
                handle = D10WinUsbHandle.Open(c.DevicePath);
                var session = new D10DirectSession(handle, c.InstanceId, settings, calibration);
                session.UpdateDrafts(buttons, curves, macros, vibration);
                session.Start();
                handle = null;
                return session;
            }
            catch (Exception ex)
            {
                lastErr = ex;
                handle?.Dispose();
            }
        }
        throw new IOException(lastErr?.Message ?? "No EasySMX D10 WinUSB device could be opened.");
    }

    private void Start()
    {
        try
        {
            vigem = new ViGEmClient();
            vpad = vigem.CreateXbox360Controller();
            vpad.AutoSubmitReport = false;
            vpad.FeedbackReceived += (_, e) =>
            {
                if (UniversalHapticsService.IsPowerSavingActive)
                {
                    nonZeroRumbleStartMs = 0;
                    rumbleCurrentlyActive = false;
                    try { usb.WriteRumble(0, 0); } catch { }
                    return;
                }
                byte l = (byte)Math.Clamp(e.LargeMotor * leftScalePct / 100, 0, 255);
                byte r = (byte)Math.Clamp(e.SmallMotor * rightScalePct / 100, 0, 255);
                bool active = l > 0 || r > 0;
                rumbleCurrentlyActive = active;
                if (active)
                {
                    if (Volatile.Read(ref nonZeroRumbleStartMs) == 0)
                        Volatile.Write(ref nonZeroRumbleStartMs, Environment.TickCount64);
                }
                else
                {
                    Volatile.Write(ref nonZeroRumbleStartMs, 0);
                }
                if (shortcutPulseActive) return;
                try { usb.WriteRumble(l, r); } catch { }
            };
            vpad.Connect();
        }
        catch
        {
            vpad = null;
            vigem?.Dispose();
            vigem = null;
            StatusChanged?.Invoke("Direct USB IMU active (install ViGEmBus in PC mode to output virtual gamepad).");
        }

        inputThread = new Thread(InputLoop) { IsBackground = true, Name = "X20CTL D10 EP81", Priority = ThreadPriority.AboveNormal };
        motionThread = new Thread(MotionLoop) { IsBackground = true, Name = "X20CTL D10 Motion", Priority = ThreadPriority.AboveNormal };
        inputThread.Start();
        motionThread.Start();
    }

    public void UpdateMotionSettings(D10MotionSettings next)
    {
        var copy = (next ?? new()).Clone().Validated();
        motionSettings = copy;
        fusion.Settings = copy;
    }

    public void UpdateCalibration(D10CalibrationData next)
    {
        fusion.Calibration = next;
    }

    public void UpdateDrafts(ButtonDraft buttons, CurveDraft curves, MacroDraft macros, VibrationDraft vibration)
    {
        var profile = new PcModeProfile();
        PcModeDrafts.Merge(profile, buttons, curves, macros, vibration);
        lock (draftSync)
        {
            processor = new PadProcessor(profile);
            leftScalePct = vibration.SupportsNativeMotors ? vibration.Left : vibration.Strength;
            rightScalePct = vibration.SupportsNativeMotors ? vibration.Right : vibration.Strength;
            compiledMacros["M1"] = macros["M1"].ToArray();
            compiledMacros["M2"] = macros["M2"].ToArray();
        }
    }

    public D10CalibrationData Recenter() => fusion.Recenter();

    public async Task PulseMotorAsync(bool left, int percent, CancellationToken cancel = default)
    {
        byte s = (byte)(Math.Clamp(percent, 0, 100) * 255 / 100);
        usb.WriteRumble(left ? s : (byte)0, left ? (byte)0 : s);
        try { await Task.Delay(450, cancel); }
        finally { try { usb.WriteRumble(0, 0); } catch { } }
    }

    public Task StopMotorsAsync()
    {
        Volatile.Write(ref nonZeroRumbleStartMs, 0);
        rumbleCurrentlyActive = false;
        usb.WriteRumble(0, 0);
        return Task.CompletedTask;
    }

    public async Task<D10CalibrationData> CalibrateAsync(Action<double> onProgress, CancellationToken cancel)
    {
        await calGate.WaitAsync(cancel);
        try
        {
            var tcs = new TaskCompletionSource<D10CalibrationData>(TaskCreationOptions.RunContinuationsAsynchronously);
            var cal = new D10Calibrator(fusion.Calibration);
            activeCalibrator = cal;
            calCompletion = tcs;

            using var reg = cancel.Register(() => tcs.TrySetCanceled(cancel));
            while (!tcs.Task.IsCompleted)
            {
                onProgress(cal.Progress);
                await Task.WhenAny(tcs.Task, Task.Delay(60, cancel));
            }
            var result = await tcs.Task;
            fusion.Calibration = result;
            return result;
        }
        finally
        {
            activeCalibrator = null;
            calCompletion = null;
            calGate.Release();
        }
    }

    public async Task<D10CalibrationData> CalibrateMultiPoseAsync(
        Action<D10MultiPoseStatus> onProgress,
        CancellationToken cancel)
    {
        await calGate.WaitAsync(cancel);
        try
        {
            var tcs = new TaskCompletionSource<D10CalibrationData>(TaskCreationOptions.RunContinuationsAsynchronously);
            var cal = new D10MultiPoseCalibrator(usb.LinkKind, fusion.Calibration);
            lastMultiStatus = new D10MultiPoseStatus(0, D10MultiPoseCalibrator.DefaultTargetPoses, false, 0, "Place controller in Pose 1 and hold steady...", false);
            activeMultiCalibrator = cal;
            multiCalCompletion = tcs;

            using var reg = cancel.Register(() => tcs.TrySetCanceled(cancel));
            while (!tcs.Task.IsCompleted)
            {
                if (lastMultiStatus is { } st) onProgress(st);
                await Task.WhenAny(tcs.Task, Task.Delay(80, cancel));
            }
            var result = await tcs.Task;
            fusion.Calibration = result;
            return result;
        }
        finally
        {
            activeMultiCalibrator = null;
            multiCalCompletion = null;
            lastMultiStatus = null;
            calGate.Release();
        }
    }

    private void InputLoop()
    {
        byte[] buf = new byte[D10Wire.Ep81ReportLength];
        try
        {
            while (!stop.IsCancellationRequested)
            {
                int read = usb.ReadPipe(D10Wire.Ep81PipeIn, buf);
                if (read >= D10Wire.Ep81ReportLength && D10Wire.TryParseInputReport(buf.AsSpan(0, read), out var frame))
                {
                    Volatile.Write(ref lastInputMs, Environment.TickCount64);
                    latestInput = frame;
                    SubmitFrame(null);
                }
            }
        }
        catch (Exception ex) when (!stop.IsCancellationRequested)
        {
            Faulted?.Invoke("D10 EP81 input stopped: " + ex.Message);
        }
    }

    private void MotionLoop()
    {
        var sw = Stopwatch.StartNew();
        double prevSec = 0;
        byte[] ep83Buf = new byte[D10Wire.Ep83ReportLength];
        int slot = 0;

        try
        {
            while (!stop.IsCancellationRequested)
            {
                CheckWatchdogs();

                D10ImuSample sample;
                if (usb.LinkKind == D10LinkKind.WiredUsb)
                {
                    int read = usb.ReadPipe(D10Wire.Ep83PipeIn, ep83Buf);
                    if (read != D10Wire.Ep83ReportLength || !D10Wire.TryParseWiredMotionReport(ep83Buf, out sample))
                        continue;
                    latestExtra = sample.Extra;
                }
                else
                {
                    // 3:2 wireless schedule across motion (0x5A) and special M/M1/M2 (0x5C) at ~330 Hz.
                    bool readSpecial = slot is 1 or 3;
                    slot = (slot + 1) % 5;
                    if (readSpecial)
                    {
                        byte[] sp = usb.ReadVendorControl(D10Wire.VendorReqSpecial, D10Wire.WirelessSpecialLength);
                        if (D10Wire.TryParseWirelessSpecialReport(sp, latestExtra, out var extra, out _))
                            latestExtra = extra;
                        Thread.Sleep(3);
                        continue;
                    }
                    byte[] mot = usb.ReadVendorControl(D10Wire.VendorReqMotion, D10Wire.WirelessMotionLength);
                    if (!D10Wire.TryParseWirelessMotionReport(mot, latestExtra, out sample))
                    {
                        Thread.Sleep(3);
                        continue;
                    }
                    Thread.Sleep(3);
                }

                long nowTicksMs = Environment.TickCount64;
                Volatile.Write(ref lastMotionMs, nowTicksMs);

                double nowSec = sw.Elapsed.TotalSeconds;
                double dt = nowSec - prevSec;
                prevSec = nowSec;

                if (activeCalibrator is { } cal && calCompletion is { } tcs && !tcs.Task.IsCompleted)
                {
                    cal.Feed(sample, usb.LinkKind);
                    if (cal.IsComplete)
                        tcs.TrySetResult(cal.BuildResult(fusion.Calibration));
                }

                if (activeMultiCalibrator is { } mcal && multiCalCompletion is { } mtcs && !mtcs.Task.IsCompleted)
                {
                    var st = mcal.Feed(sample);
                    lastMultiStatus = st;
                    if (mcal.IsComplete)
                    {
                        try { mtcs.TrySetResult(mcal.BuildResult()); }
                        catch (Exception fitErr) { mtcs.TrySetException(fitErr); }
                    }
                }

                bool freshGyro = !hasPrevGyro ||
                    sample.Gx != prevRawGyro.Gx || sample.Gy != prevRawGyro.Gy || sample.Gz != prevRawGyro.Gz;
                prevRawGyro = (sample.Gx, sample.Gy, sample.Gz);
                hasPrevGyro = true;

                // Merge Xbox EP81 D-pad bits so M+D-pad shortcuts work consistently over wired and 2.4 GHz.
                var inputSnap = latestInput;
                var mergedExtra = sample.Extra with
                {
                    DpadUp = sample.Extra.DpadUp || (inputSnap.Buttons & PadButtons.Up) != 0,
                    DpadDown = sample.Extra.DpadDown || (inputSnap.Buttons & PadButtons.Down) != 0,
                    DpadLeft = sample.Extra.DpadLeft || (inputSnap.Buttons & PadButtons.Left) != 0,
                    DpadRight = sample.Extra.DpadRight || (inputSnap.Buttons & PadButtons.Right) != 0,
                };
                sample = sample with { Extra = mergedExtra };

                var telemetry = fusion.Update(sample, usb.LinkKind, dt, rumbleCurrentlyActive, freshGyro);
                HandleShortcutActions(telemetry.Extra, nowTicksMs);
                SubmitFrame(telemetry);
                TelemetryUpdated?.Invoke(telemetry);
            }
        }
        catch (Exception ex) when (!stop.IsCancellationRequested)
        {
            Faulted?.Invoke("D10 motion stream stopped: " + ex.Message);
        }
    }

    private void CheckWatchdogs()
    {
        long now = Environment.TickCount64;
        long rStart = Volatile.Read(ref nonZeroRumbleStartMs);
        if (rumbleCurrentlyActive && rStart > 0 && (now - rStart) > 30_000)
        {
            Volatile.Write(ref nonZeroRumbleStartMs, 0);
            rumbleCurrentlyActive = false;
            try { usb.WriteRumble(0, 0); } catch { }
            StatusChanged?.Invoke("30s continuous rumble safety cutoff triggered.");
        }
    }

    private void HandleShortcutActions(D10ExtraButtons extra, long nowMs)
    {
        var cfg = motionSettings;
        bool IsActionPressed(string target) =>
            (extra.M1 && cfg.M1Binding == target) ||
            (extra.M2 && cfg.M2Binding == target) ||
            (extra.M && cfg.MBinding == target);

        bool toggleNow = IsActionPressed(D10PaddleActions.ToggleMotion);
        if (toggleNow && !prevToggleDown)
        {
            motionRuntimeActive = !motionRuntimeActive;
            StatusChanged?.Invoke(motionRuntimeActive ? "D10 6-axis motion resumed." : "D10 6-axis motion paused via shortcut.");
            if (cfg.ShortcutRumbleFeedback) _ = PlayConfirmationPulseAsync(doublePulse: motionRuntimeActive);
        }
        prevToggleDown = toggleNow;

        bool recenterNow = IsActionPressed(D10PaddleActions.RecenterGyro);
        if (recenterNow && !prevRecenterDown)
        {
            var cal = fusion.Recenter();
            CalibrationUpdated?.Invoke(cal);
            StatusChanged?.Invoke($"Gyro recentered at {cal.CenterAngleDeg:0.1} deg.");
            if (cfg.ShortcutRumbleFeedback) _ = PlayConfirmationPulseAsync(doublePulse: false, longMs: 260);
        }
        prevRecenterDown = recenterNow;

        bool macroM1Now = IsActionPressed(D10PaddleActions.MacroM1);
        if (macroM1Now && !prevMacroM1Down)
        {
            MacroEvent[] steps;
            lock (draftSync) steps = compiledMacros["M1"];
            macroPlayer.Trigger(steps, nowMs);
        }
        prevMacroM1Down = macroM1Now;

        bool macroM2Now = IsActionPressed(D10PaddleActions.MacroM2);
        if (macroM2Now && !prevMacroM2Down)
        {
            MacroEvent[] steps;
            lock (draftSync) steps = compiledMacros["M2"];
            macroPlayer.Trigger(steps, nowMs);
        }
        prevMacroM2Down = macroM2Now;

        if (cfg.ShortcutsEnabled && shortcutDetector.Update(extra, nowMs) is { } gesture)
            DispatchHardwareShortcut(gesture);
    }

    private void DispatchHardwareShortcut(D10ShortcutGesture gesture)
    {
        bool feedback = motionSettings.ShortcutRumbleFeedback;
        if (gesture.Mode == D10ShortcutKind.Short)
        {
            int slot = gesture.Direction switch
            {
                D10ShortcutDir.Up => 1,
                D10ShortcutDir.Right => 2,
                D10ShortcutDir.Down => 3,
                D10ShortcutDir.Left => 4,
                _ => 1,
            };
            QuickSlotRequested?.Invoke(slot);
            if (feedback) _ = PlayConfirmationPulseAsync(doublePulse: false, longMs: 110);
            return;
        }

        switch (gesture.Direction)
        {
            case D10ShortcutDir.Up:
                motionRuntimeActive = !motionRuntimeActive;
                StatusChanged?.Invoke(motionRuntimeActive ? "M+Up (3s): 6-axis motion enabled." : "M+Up (3s): 6-axis motion paused.");
                if (feedback) _ = PlayConfirmationPulseAsync(doublePulse: true);
                break;
            case D10ShortcutDir.Down:
                var cal = fusion.Recenter();
                CalibrationUpdated?.Invoke(cal);
                StatusChanged?.Invoke($"M+Down (3s): Gyro recentered to {cal.CenterAngleDeg:0.1} deg.");
                if (feedback) _ = PlayConfirmationPulseAsync(doublePulse: false, longMs: 320);
                break;
            case D10ShortcutDir.Right:
                QuickCalibrateRequested?.Invoke();
                if (feedback) _ = PlayConfirmationPulseAsync(doublePulse: true);
                break;
        }
    }

    private async Task PlayConfirmationPulseAsync(bool doublePulse, int longMs = 110)
    {
        if (UniversalHapticsService.IsPowerSavingActive || disposed) return;
        shortcutPulseActive = true;
        try
        {
            if (doublePulse)
            {
                usb.WriteRumble(80, 0);
                await Task.Delay(90, stop.Token);
                usb.WriteRumble(0, 0);
                await Task.Delay(55, stop.Token);
                usb.WriteRumble(0, 80);
                await Task.Delay(90, stop.Token);
                usb.WriteRumble(0, 0);
            }
            else
            {
                usb.WriteRumble(72, 72);
                await Task.Delay(longMs, stop.Token);
                usb.WriteRumble(0, 0);
            }
        }
        catch { }
        finally
        {
            shortcutPulseActive = false;
        }
    }

    private void SubmitFrame(D10LiveTelemetry? telemetry)
    {
        var pad = vpad;
        long nowMs = Environment.TickCount64;
        var cfg = motionSettings;
        var extra = telemetry?.Extra ?? latestExtra;

        UpdateHostOutput("M1", extra.M1, cfg.M1Binding);
        UpdateHostOutput("M2", extra.M2, cfg.M2Binding);
        UpdateHostOutput("M", extra.M, cfg.MBinding);

        long ageInput = Volatile.Read(ref lastInputMs);
        bool inputTimedOut = ageInput > 0 && (nowMs - ageInput) > cfg.Ep81NeutralTimeoutMs;
        PadState baseState = inputTimedOut ? default : latestInput.ToPadState();

        ushort extraMask = 0;
        ApplyPaddleMask(ref extraMask, extra.M1, cfg.M1Binding);
        ApplyPaddleMask(ref extraMask, extra.M2, cfg.M2Binding);
        ApplyPaddleMask(ref extraMask, extra.M, cfg.MBinding);
        baseState = baseState with { Buttons = (ushort)(baseState.Buttons | extraMask) };

        baseState = macroPlayer.MixInto(baseState, nowMs);

        PadState shaped;
        lock (draftSync)
        {
            shaped = processor?.Process(baseState, nowMs) ?? baseState;
        }

        long ageMotion = Volatile.Read(ref lastMotionMs);
        bool motionFresh = ageMotion == 0 || (nowMs - ageMotion) <= cfg.Ep83MotionTimeoutMs;
        if (telemetry is { } t && motionRuntimeActive && motionFresh)
        {
            if (cfg.Mode == D10MotionMode.SteerLeftStick)
                shaped = shaped with { LeftX = MixAxis(shaped.LeftX, t.OutputX) };
            else if (cfg.Mode == D10MotionMode.AimRightStick)
                shaped = shaped with { RightX = MixAxis(shaped.RightX, t.OutputX), RightY = MixAxis(shaped.RightY, t.OutputY) };
            else if (cfg.Mode == D10MotionMode.GyroMouse && (t.OutputX != 0 || t.OutputY != 0))
                MoveRelativeMouse(t.OutputX * 14.0, t.OutputY * 14.0);
        }

        if (pad == null) return;

        pad.SetButtonState(Xbox360Button.A, (shaped.Buttons & PadButtons.A) != 0);
        pad.SetButtonState(Xbox360Button.B, (shaped.Buttons & PadButtons.B) != 0);
        pad.SetButtonState(Xbox360Button.X, (shaped.Buttons & PadButtons.X) != 0);
        pad.SetButtonState(Xbox360Button.Y, (shaped.Buttons & PadButtons.Y) != 0);
        pad.SetButtonState(Xbox360Button.LeftShoulder, (shaped.Buttons & PadButtons.LB) != 0);
        pad.SetButtonState(Xbox360Button.RightShoulder, (shaped.Buttons & PadButtons.RB) != 0);
        pad.SetButtonState(Xbox360Button.LeftThumb, (shaped.Buttons & PadButtons.L3) != 0);
        pad.SetButtonState(Xbox360Button.RightThumb, (shaped.Buttons & PadButtons.R3) != 0);
        pad.SetButtonState(Xbox360Button.Back, (shaped.Buttons & PadButtons.Back) != 0);
        pad.SetButtonState(Xbox360Button.Start, (shaped.Buttons & PadButtons.Start) != 0);
        pad.SetButtonState(Xbox360Button.Up, (shaped.Buttons & PadButtons.Up) != 0);
        pad.SetButtonState(Xbox360Button.Down, (shaped.Buttons & PadButtons.Down) != 0);
        pad.SetButtonState(Xbox360Button.Left, (shaped.Buttons & PadButtons.Left) != 0);
        pad.SetButtonState(Xbox360Button.Right, (shaped.Buttons & PadButtons.Right) != 0);
        pad.SetSliderValue(Xbox360Slider.LeftTrigger, shaped.LeftTrigger);
        pad.SetSliderValue(Xbox360Slider.RightTrigger, shaped.RightTrigger);
        pad.SetAxisValue(Xbox360Axis.LeftThumbX, shaped.LeftX);
        pad.SetAxisValue(Xbox360Axis.LeftThumbY, shaped.LeftY);
        pad.SetAxisValue(Xbox360Axis.RightThumbX, shaped.RightX);
        pad.SetAxisValue(Xbox360Axis.RightThumbY, shaped.RightY);
        try { pad.SubmitReport(); } catch { }
    }

    private static void ApplyPaddleMask(ref ushort mask, bool pressed, string binding)
    {
        if (!pressed || string.IsNullOrEmpty(binding)) return;
        if (PadButtons.Mask.TryGetValue(binding, out ushort bit)) mask |= bit;
    }

    private static short MixAxis(short stick, double motionNormalized)
    {
        int combined = stick + (int)Math.Round(motionNormalized * 32767.0);
        return (short)Math.Clamp(combined, -32768, 32767);
    }

    private static void MoveRelativeMouse(double dx, double dy)
    {
        int mx = (int)Math.Round(dx);
        int my = (int)Math.Round(dy);
        if (mx != 0 || my != 0)
            D10WinUsbNative.mouse_event(0x0001, mx, my, 0, UIntPtr.Zero);
    }

    private void UpdateHostOutput(string slot, bool pressed, string binding)
    {
        string? desired = pressed && D10PaddleActions.IsHostKeyOrMouse(binding) ? binding : null;
        string? previous = heldHostTokens[slot];
        if (string.Equals(previous, desired, StringComparison.Ordinal)) return;

        bool heldElsewhereBefore = desired != null && heldHostTokens
            .Where(kv => kv.Key != slot)
            .Any(kv => string.Equals(kv.Value, desired, StringComparison.Ordinal));

        heldHostTokens[slot] = desired;
        if (previous != null && !heldHostTokens.Values.Any(v => string.Equals(v, previous, StringComparison.Ordinal)))
        {
            try { MouseOutput.Output(previous, false); } catch { }
        }
        if (desired != null && !heldElsewhereBefore)
        {
            try { MouseOutput.Output(desired, true); } catch { }
        }
    }

    private void ReleaseAllHostOutputs()
    {
        foreach (string slot in new[] { "M1", "M2", "M" })
        {
            string? tok = heldHostTokens[slot];
            heldHostTokens[slot] = null;
            if (tok != null)
            {
                try { MouseOutput.Output(tok, false); } catch { }
            }
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        stop.Cancel();
        ReleaseAllHostOutputs();
        try { usb.WriteRumble(0, 0); } catch { }
        usb.CancelIo();
        if (inputThread is { IsAlive: true } && Thread.CurrentThread != inputThread) inputThread.Join(1200);
        if (motionThread is { IsAlive: true } && Thread.CurrentThread != motionThread) motionThread.Join(1200);
        try { vpad?.Disconnect(); } catch { }
        vigem?.Dispose();
        usb.Dispose();
        stop.Dispose();
        calGate.Dispose();
    }

    private sealed class D10MacroPlayer
    {
        private readonly object sync = new();
        private MacroEvent[] steps = [];
        private int index;
        private long stepStartMs;
        private bool active;

        public void Trigger(MacroEvent[] sequence, long nowMs)
        {
            lock (sync)
            {
                if (sequence.Length == 0) { active = false; return; }
                steps = sequence;
                index = 0;
                stepStartMs = nowMs;
                active = true;
            }
        }

        public PadState MixInto(PadState state, long nowMs)
        {
            lock (sync)
            {
                if (!active || steps.Length == 0) return state;
                while (index < steps.Length)
                {
                    var step = steps[index];
                    long elapsed = nowMs - stepStartMs;
                    long total = step.HoldMs + step.PauseMs;
                    if (elapsed < step.HoldMs)
                    {
                        ushort mask = state.Buttons;
                        byte lt = state.LeftTrigger;
                        byte rt = state.RightTrigger;
                        foreach (string b in step.Buttons)
                        {
                            if (b == "LT") lt = 255;
                            else if (b == "RT") rt = 255;
                            else if (PadButtons.Mask.TryGetValue(b, out ushort bit)) mask |= bit;
                        }
                        var (lx, ly) = HeadingToStick(step.Left);
                        var (rx, ry) = HeadingToStick(step.Right);
                        return new PadState(
                            mask, lt, rt,
                            lx != 0 ? lx : state.LeftX,
                            ly != 0 ? ly : state.LeftY,
                            rx != 0 ? rx : state.RightX,
                            ry != 0 ? ry : state.RightY);
                    }
                    if (elapsed < total) return state;
                    stepStartMs += Math.Max(1, total);
                    index++;
                }
                active = false;
                return state;
            }
        }

        private static (short X, short Y) HeadingToStick(StickHeading h) => h switch
        {
            StickHeading.Up => (0, 32767),
            StickHeading.UpRight => (23170, 23170),
            StickHeading.Right => (32767, 0),
            StickHeading.DownRight => (23170, -23170),
            StickHeading.Down => (0, -32767),
            StickHeading.DownLeft => (-23170, -23170),
            StickHeading.Left => (-32767, 0),
            StickHeading.UpLeft => (-23170, 23170),
            _ => (0, 0),
        };
    }

    internal sealed class D10WinUsbHandle : IDisposable
    {
        private readonly SafeFileHandle fileHandle;
        private readonly List<IntPtr> interfaces;
        private readonly IntPtr defaultInterface;
        private readonly IntPtr ep81Interface, ep83Interface, ep02Interface;
        private readonly object ctrlLock = new(), writeLock = new();
        private bool closed;

        public D10LinkKind LinkKind { get; }

        private D10WinUsbHandle(
            SafeFileHandle fileHandle,
            List<IntPtr> interfaces,
            IntPtr defaultInterface,
            IntPtr ep81Interface,
            IntPtr ep83Interface,
            IntPtr ep02Interface,
            D10LinkKind linkKind)
        {
            this.fileHandle = fileHandle;
            this.interfaces = interfaces;
            this.defaultInterface = defaultInterface;
            this.ep81Interface = ep81Interface;
            this.ep83Interface = ep83Interface;
            this.ep02Interface = ep02Interface;
            LinkKind = linkKind;
        }

        public static D10WinUsbHandle Open(string devicePath)
        {
            SafeFileHandle fh = D10WinUsbNative.CreateFileW(
                devicePath,
                D10WinUsbNative.GENERIC_READ | D10WinUsbNative.GENERIC_WRITE,
                D10WinUsbNative.FILE_SHARE_READ | D10WinUsbNative.FILE_SHARE_WRITE | D10WinUsbNative.FILE_SHARE_DELETE,
                IntPtr.Zero,
                D10WinUsbNative.OPEN_EXISTING,
                D10WinUsbNative.FILE_ATTRIBUTE_NORMAL,
                IntPtr.Zero);
            if (fh.IsInvalid)
            {
                int err = Marshal.GetLastWin32Error();
                fh.Dispose();
                throw new Win32Exception(err, $"CreateFile failed ({err}).");
            }

            if (!D10WinUsbNative.WinUsb_Initialize(fh, out IntPtr defIf))
            {
                int err = Marshal.GetLastWin32Error();
                fh.Dispose();
                throw new Win32Exception(err, $"WinUsb_Initialize failed ({err}).");
            }

            var allIf = new List<IntPtr> { defIf };
            try
            {
                for (byte idx = 0; idx < 16; idx++)
                {
                    if (D10WinUsbNative.WinUsb_GetAssociatedInterface(defIf, idx, out IntPtr assoc))
                        allIf.Add(assoc);
                    else
                        break;
                }

                IntPtr ep81If = IntPtr.Zero, ep02If = IntPtr.Zero, ep83If = IntPtr.Zero;
                foreach (IntPtr iface in allIf)
                {
                    if (!D10WinUsbNative.WinUsb_QueryInterfaceSettings(iface, 0, out var desc)) continue;
                    for (byte p = 0; p < desc.NumEndpoints; p++)
                    {
                        if (!D10WinUsbNative.WinUsb_QueryPipe(iface, 0, p, out var pipe)) continue;
                        uint timeoutMs = 400;
                        D10WinUsbNative.WinUsb_SetPipePolicy(iface, pipe.PipeId, 3, sizeof(uint), ref timeoutMs);
                        if (desc.InterfaceNumber == 0 && pipe.PipeId == D10Wire.Ep81PipeIn) ep81If = iface;
                        if (desc.InterfaceNumber == 0 && pipe.PipeId == D10Wire.Ep02PipeOut) ep02If = iface;
                        if (desc.InterfaceNumber == 2 && pipe.PipeId == D10Wire.Ep83PipeIn) ep83If = iface;
                    }
                }

                if (ep81If == IntPtr.Zero || ep02If == IntPtr.Zero)
                    throw new IOException("Expected D10 IF0/EP81 and IF0/EP02 pipes were not exposed.");

                var handle = new D10WinUsbHandle(
                    fh, allIf, defIf, ep81If, ep83If, ep02If,
                    ep83If != IntPtr.Zero ? D10LinkKind.WiredUsb : D10LinkKind.WirelessReceiver);

                if (handle.LinkKind == D10LinkKind.WirelessReceiver)
                {
                    byte[] mot = handle.ReadVendorControl(D10Wire.VendorReqMotion, D10Wire.WirelessMotionLength);
                    byte[] sp = handle.ReadVendorControl(D10Wire.VendorReqSpecial, D10Wire.WirelessSpecialLength);
                    if (!D10Wire.TryParseWirelessMotionReport(mot, D10ExtraButtons.None, out _) ||
                        !D10Wire.TryParseWirelessSpecialReport(sp, D10ExtraButtons.None, out _, out _))
                    {
                        throw new IOException("2.4 GHz receiver does not expose C0/5A + C0/5C motion endpoints.");
                    }
                }

                return handle;
            }
            catch
            {
                foreach (var h in allIf.AsEnumerable().Reverse()) D10WinUsbNative.WinUsb_Free(h);
                fh.Dispose();
                throw;
            }
        }

        public int ReadPipe(byte pipeId, byte[] buffer)
        {
            IntPtr iface = pipeId == D10Wire.Ep83PipeIn ? ep83Interface : ep81Interface;
            if (!D10WinUsbNative.WinUsb_ReadPipe(iface, pipeId, buffer, (uint)buffer.Length, out uint transferred, IntPtr.Zero))
            {
                int err = Marshal.GetLastWin32Error();
                if (err == 121) return 0; // timeout
                throw new Win32Exception(err, $"WinUsb_ReadPipe 0x{pipeId:X2} failed ({err}).");
            }
            return (int)transferred;
        }

        public byte[] ReadVendorControl(byte request, int len)
        {
            var pkt = new D10WinUsbNative.WINUSB_SETUP_PACKET
            {
                RequestType = 0xC0,
                Request = request,
                Value = 0,
                Index = 0,
                Length = (ushort)len,
            };
            byte[] buf = new byte[len];
            lock (ctrlLock)
            {
                if (!D10WinUsbNative.WinUsb_ControlTransfer(defaultInterface, pkt, buf, (uint)buf.Length, out uint transferred, IntPtr.Zero))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), $"Control transfer C0/{request:X2} failed.");
                if (transferred != len)
                    throw new IOException($"C0/{request:X2} returned {transferred}/{len} bytes.");
            }
            return buf;
        }

        public void WriteRumble(byte left, byte right)
        {
            byte[] pkt = D10Wire.BuildRumblePacket(left, right);
            lock (writeLock)
            {
                if (!D10WinUsbNative.WinUsb_WritePipe(ep02Interface, D10Wire.Ep02PipeOut, pkt, (uint)pkt.Length, out _, IntPtr.Zero))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "EP02 rumble write failed.");
            }
        }

        public void CancelIo()
        {
            D10WinUsbNative.CancelIoEx(fileHandle, IntPtr.Zero);
        }

        public void Dispose()
        {
            if (closed) return;
            closed = true;
            CancelIo();
            foreach (var h in interfaces.AsEnumerable().Reverse()) D10WinUsbNative.WinUsb_Free(h);
            fileHandle.Dispose();
        }
    }

    internal static class D10WinUsbNative
    {
        public const uint GENERIC_READ = 0x80000000;
        public const uint GENERIC_WRITE = 0x40000000;
        public const uint FILE_SHARE_READ = 1, FILE_SHARE_WRITE = 2, FILE_SHARE_DELETE = 4;
        public const uint OPEN_EXISTING = 3;
        public const uint FILE_ATTRIBUTE_NORMAL = 0x80;

        public sealed record DeviceCandidate(string DevicePath, string InstanceId);

        public static IReadOnlyList<DeviceCandidate> EnumeratePaths()
        {
            var list = new List<DeviceCandidate>();
            Guid guid = D10Wire.WinUsbInterfaceGuid;
            IntPtr set = SetupDiGetClassDevsW(ref guid, null, IntPtr.Zero, 0x02 | 0x10);
            if (set == new IntPtr(-1)) return list;
            try
            {
                for (uint i = 0; ; i++)
                {
                    var ifData = new SP_DEVICE_INTERFACE_DATA { cbSize = Marshal.SizeOf<SP_DEVICE_INTERFACE_DATA>() };
                    if (!SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref guid, i, ref ifData))
                    {
                        if (Marshal.GetLastWin32Error() == 259) break;
                        continue;
                    }
                    var devInfo = new SP_DEVINFO_DATA { cbSize = Marshal.SizeOf<SP_DEVINFO_DATA>() };
                    SetupDiGetDeviceInterfaceDetailW(set, ref ifData, IntPtr.Zero, 0, out uint needed, ref devInfo);
                    if (needed == 0) continue;
                    IntPtr mem = Marshal.AllocHGlobal((int)needed);
                    try
                    {
                        Marshal.WriteInt32(mem, IntPtr.Size == 8 ? 8 : 6);
                        if (!SetupDiGetDeviceInterfaceDetailW(set, ref ifData, mem, needed, out _, ref devInfo)) continue;
                        string path = Marshal.PtrToStringUni(IntPtr.Add(mem, 4)) ?? "";
                        var sb = new StringBuilder(512);
                        string id = SetupDiGetDeviceInstanceIdW(set, ref devInfo, sb, sb.Capacity, out _) ? sb.ToString() : "";
                        if (path.Contains("vid_045e&pid_028e", StringComparison.OrdinalIgnoreCase) ||
                            id.StartsWith("USB\\VID_045E&PID_028E\\", StringComparison.OrdinalIgnoreCase) ||
                            path.Contains("vid_2345&pid_e062", StringComparison.OrdinalIgnoreCase) ||
                            id.StartsWith("USB\\VID_2345&PID_E062\\", StringComparison.OrdinalIgnoreCase))
                        {
                            list.Add(new(path, id));
                        }
                    }
                    finally { Marshal.FreeHGlobal(mem); }
                }
            }
            finally { SetupDiDestroyDeviceInfoList(set); }
            return list;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SP_DEVINFO_DATA { public int cbSize; public Guid ClassGuid; public int DevInst; public IntPtr Reserved; }

        [StructLayout(LayoutKind.Sequential)]
        public struct SP_DEVICE_INTERFACE_DATA { public int cbSize; public Guid InterfaceClassGuid; public int Flags; public UIntPtr Reserved; }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct USB_INTERFACE_DESCRIPTOR
        {
            public byte Length, DescriptorType, InterfaceNumber, AlternateSetting, NumEndpoints, InterfaceClass, InterfaceSubClass, InterfaceProtocol, Interface;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct WINUSB_PIPE_INFORMATION { public int PipeType; public byte PipeId; public ushort MaximumPacketSize; public byte Interval; }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct WINUSB_SETUP_PACKET { public byte RequestType; public byte Request; public ushort Value; public ushort Index; public ushort Length; }

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr SetupDiGetClassDevsW(ref Guid classGuid, string? enumerator, IntPtr hwndParent, uint flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        public static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr data, ref Guid guid, uint index, ref SP_DEVICE_INTERFACE_DATA ifData);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool SetupDiGetDeviceInterfaceDetailW(IntPtr set, ref SP_DEVICE_INTERFACE_DATA ifData, IntPtr detail, uint size, out uint required, ref SP_DEVINFO_DATA devInfo);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool SetupDiGetDeviceInstanceIdW(IntPtr set, ref SP_DEVINFO_DATA devInfo, StringBuilder buf, int size, out int required);

        [DllImport("setupapi.dll")]
        public static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern SafeFileHandle CreateFileW(string name, uint access, uint share, IntPtr sa, uint disp, uint attr, IntPtr template);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CancelIoEx(SafeFileHandle handle, IntPtr overlapped);

        [DllImport("winusb.dll", SetLastError = true)]
        public static extern bool WinUsb_Initialize(SafeFileHandle deviceHandle, out IntPtr ifaceHandle);

        [DllImport("winusb.dll", SetLastError = true)]
        public static extern bool WinUsb_Free(IntPtr ifaceHandle);

        [DllImport("winusb.dll", SetLastError = true)]
        public static extern bool WinUsb_GetAssociatedInterface(IntPtr iface, byte idx, out IntPtr assoc);

        [DllImport("winusb.dll", SetLastError = true)]
        public static extern bool WinUsb_QueryInterfaceSettings(IntPtr iface, byte alt, out USB_INTERFACE_DESCRIPTOR desc);

        [DllImport("winusb.dll", SetLastError = true)]
        public static extern bool WinUsb_QueryPipe(IntPtr iface, byte alt, byte idx, out WINUSB_PIPE_INFORMATION pipe);

        [DllImport("winusb.dll", SetLastError = true)]
        public static extern bool WinUsb_SetPipePolicy(IntPtr iface, byte pipeId, uint policyType, uint valueLen, ref uint value);

        [DllImport("winusb.dll", SetLastError = true)]
        public static extern bool WinUsb_ReadPipe(IntPtr iface, byte pipeId, [Out] byte[] buf, uint len, out uint transferred, IntPtr overlapped);

        [DllImport("winusb.dll", SetLastError = true)]
        public static extern bool WinUsb_WritePipe(IntPtr iface, byte pipeId, [In] byte[] buf, uint len, out uint transferred, IntPtr overlapped);

        [DllImport("winusb.dll", SetLastError = true)]
        public static extern bool WinUsb_ControlTransfer(IntPtr iface, WINUSB_SETUP_PACKET pkt, [Out] byte[] buf, uint len, out uint transferred, IntPtr overlapped);

        [DllImport("user32.dll")]
        public static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, UIntPtr extra);
    }
}
