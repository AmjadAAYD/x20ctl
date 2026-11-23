using System.IO;
using X20Ctl.Zone.Lighting;

namespace X20Ctl.Product;

internal sealed record LightingEffectsStatus(
    bool IsRunning,
    bool IsBleConnected,
    bool IsYielded,
    int SessionWrites,
    int TotalWrites,
    int SkippedDuplicateFrames,
    string? ActiveGameExe,
    string? LastError,
    string Summary);

/// <summary>
/// App-wide coordinator for custom animated & reactive X20 Pro lighting effects over one persistent BLE link,
/// with software simulation in <see cref="ReviewSandbox"/> and deadlock-free yield for Config/Battery/Telemetry BLE ops.
/// </summary>
internal static class LightingEffectsService
{
    static readonly SemaphoreSlim ControlLock = new(1, 1);
    static readonly object InputLock = new();
    static readonly SemaphoreSlim WakeSignal = new(0, 1);

    static LightingPresetsStore? store;
    static LightingEffectSpec currentSpec = new()
    {
        Kind = LightingEffectKind.Solid,
        Palette = ["#FF0000"],
        MaxBrightness = 0x50,
    };
    static DateTimeOffset effectStartUtc = DateTimeOffset.UtcNow;
    static LightingFrame previewFrame = new(0xFF, 0x00, 0x00, 0x50, LightingRecord.ModeSolid);

    // Live XInput/NativeInput state for TriggerGlow / StickHeat / ButtonFlash / IdleSleepDim.
    static double leftMag, rightMag, leftTrig, rightTrig;
    static int heldButtons;
    static DateTimeOffset lastActivityUtc = DateTimeOffset.UtcNow;
    static DateTimeOffset lastButtonPressUtc = DateTimeOffset.MinValue;

    // Stream worker & gate-yield bookkeeping.
    static CancellationTokenSource? workerCts;
    static Task? workerTask;
    static bool wantRunning;
    static volatile bool restoreOnCancel;
    static int yieldDepth;
    static bool isBleConnected;
    static int sessionWrites, totalWrites, skippedDuplicates;
    static string? activeGameExe;
    static string? lastError;
    static string summary = "Ready";

    static LightingEffectsService()
    {
        NativeBleClient.YieldActiveStreamAsync = YieldForExclusiveBleAsync;
    }

    public static event Action? Changed;

    public static LightingPresetsStore Store
    {
        get
        {
            if (store != null) return store;
            var s = new LightingPresetsStore(LightingPresetsStore.DefaultPath);
            try { s.Load(); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or System.Text.Json.JsonException)
            {
                lastError = "Could not load saved presets: " + ex.Message;
            }
            if (s.LastEffect is { } saved)
            {
                currentSpec = saved.Normalized();
                previewFrame = EvaluateCurrent();
            }
            store = s;
            return s;
        }
    }

    public static LightingEffectSpec CurrentSpec => currentSpec;
    public static LightingFrame PreviewFrame => previewFrame;

    public static LightingEffectsStatus Status => new(
        wantRunning || (workerTask != null && !workerTask.IsCompleted),
        isBleConnected,
        yieldDepth > 0,
        sessionWrites,
        totalWrites,
        skippedDuplicates,
        activeGameExe,
        lastError,
        summary);

    public static void ObserveLiveInput(double lx, double ly, double rx, double ry, double lt, double rt, int heldCount, bool anyNewPress)
    {
        var now = DateTimeOffset.UtcNow;
        double lMag = Math.Clamp(Math.Sqrt(lx * lx + ly * ly), 0, 1);
        double rMag = Math.Clamp(Math.Sqrt(rx * rx + ry * ry), 0, 1);
        bool active = lMag > 0.14 || rMag > 0.14 || lt > 0.10 || rt > 0.10 || heldCount > 0 || anyNewPress;
        bool wake = false;

        lock (InputLock)
        {
            bool trigShift = Math.Abs(lt - leftTrig) > 0.08 || Math.Abs(rt - rightTrig) > 0.08;
            bool stickShift = Math.Abs(lMag - leftMag) > 0.10 || Math.Abs(rMag - rightMag) > 0.10;
            bool wasIdle = (now - lastActivityUtc).TotalSeconds >= currentSpec.IdleTimeoutSeconds;

            leftMag = lMag;
            rightMag = rMag;
            leftTrig = Math.Clamp(lt, 0, 1);
            rightTrig = Math.Clamp(rt, 0, 1);
            heldButtons = heldCount;
            if (active) lastActivityUtc = now;
            if (anyNewPress) lastButtonPressUtc = now;

            if (currentSpec.RequiresLiveInput && (anyNewPress || (active && wasIdle) || trigShift || stickShift))
                wake = true;
        }

        if (wake && WakeSignal.CurrentCount == 0)
        {
            try { WakeSignal.Release(); } catch (SemaphoreFullException) { }
        }
    }

    public static LightingFrame EvaluatePreview()
    {
        var spec = currentSpec;
        if (spec.Kind == LightingEffectKind.GameAutoProfile)
        {
            var resolved = ResolveEffectiveGameSpec(spec, out _);
            spec = resolved;
        }
        var (input, pressAge) = ReadInputState();
        var frame = EvaluateFrame(spec, DateTimeOffset.UtcNow - effectStartUtc, input, pressAge);
        previewFrame = frame;
        return frame;
    }

    static LightingFrame EvaluateFrame(LightingEffectSpec spec, TimeSpan elapsed, LightingInputState input, TimeSpan? pressAge)
    {
        if (spec.Kind == LightingEffectKind.ScreenAmbient && !ReviewSandbox.Active)
            return ScreenAmbientSampler.Sample(spec);
        if (spec.Kind == LightingEffectKind.AudioReactive)
        {
            if (!ReviewSandbox.Active)
                return SystemAudioLevelSampler.Sample(spec, elapsed, input, pressAge);
            double t = elapsed.TotalSeconds;
            double beatCycle = (t * 2.0) % 1.0;
            double beatPulse = Math.Exp(-beatCycle * 5.0);
            int beatCount = (int)(t * 2.0);
            double bass = Math.Clamp(0.25 + 0.70 * beatPulse, 0.0, 1.0);
            double mid = Math.Clamp(0.30 + 0.45 * (0.5 + 0.5 * Math.Sin(t * 3.1)), 0.0, 1.0);
            double treble = Math.Clamp(0.20 + 0.55 * (0.5 + 0.5 * Math.Sin(t * 7.4)), 0.0, 1.0);
            double trebleSpike = ((t * 4.0) % 1.0) < 0.25 ? 0.85 : 0.0;
            double centroid = Math.Clamp(0.25 + 0.65 * (0.5 + 0.5 * Math.Sin(t * 1.9)), 0.0, 1.0);
            double level = Math.Clamp(Math.Max(bass * 0.85, Math.Max(mid, treble)), 0.0, 1.0);
            return LightingEffectEngine.Evaluate(spec, elapsed, input with
            {
                AudioLevel = level,
                AudioBass = bass,
                AudioMid = mid,
                AudioTreble = treble,
                AudioBeatPulse = beatPulse,
                AudioBeatCount = beatCount,
                AudioCentroid = centroid,
                AudioTrebleTransient = trebleSpike,
                AudioEnergy = 0.65,
            }, pressAge);
        }
        return LightingEffectEngine.Evaluate(spec, elapsed, input, pressAge);
    }

    public static void SetPreviewDraft(LightingEffectSpec draftSpec)
    {
        if (wantRunning) return;
        currentSpec = draftSpec.Normalized();
        previewFrame = EvaluatePreview();
        Changed?.Invoke();
    }

    public static async Task<LightingEffectsStatus> ApplyAsync(LightingEffectSpec rawSpec, bool remember = true)
    {
        var spec = rawSpec.Normalized();
        LightingFrame frameToWrite;
        bool writeNative = false;
        await ControlLock.WaitAsync();
        try
        {
            await CancelWorkerLockedAsync(restoreBaseline: false);
            currentSpec = spec;
            effectStartUtc = DateTimeOffset.UtcNow;
            lastError = null;
            sessionWrites = 0;
            skippedDuplicates = 0;
            activeGameExe = null;
            BatteryLightingService.Disable();

            if (remember && !ReviewSandbox.Active)
            {
                try { Store.RememberLastEffect(spec); } catch { }
            }

            previewFrame = EvaluatePreview();
            frameToWrite = previewFrame;

            if (spec.IsNativeMode)
            {
                wantRunning = false;
                if (ReviewSandbox.Active)
                {
                    summary = $"{LightingEffectSpec.EffectLabel(spec.Kind)} preview ({previewFrame.Hex}) · sandbox";
                }
                else
                {
                    writeNative = true;
                    summary = $"{LightingEffectSpec.EffectLabel(spec.Kind)} applying over BLE…";
                }
            }
            else
            {
                wantRunning = true;
                StartWorkerLocked();
                summary = ReviewSandbox.Active
                    ? $"{LightingEffectSpec.EffectLabel(spec.Kind)} simulating ({spec.StepIntervalMs} ms)"
                    : $"{LightingEffectSpec.EffectLabel(spec.Kind)} starting on BLE ({spec.StepIntervalMs} ms)…";
            }
        }
        finally
        {
            ControlLock.Release();
        }
        Changed?.Invoke();

        // Run BLE outside ControlLock so YieldForExclusiveBleAsync / StopAsync never deadlock on ControlLock.
        if (writeNative)
        {
            try
            {
                var mode = spec.ToNativeModeByte() ?? LightingRecord.ModeSolid;
                await LightingBleClient.RunWithoutYieldAsync([frameToWrite.R, frameToWrite.G, frameToWrite.B], mode, frameToWrite.Brightness);
                totalWrites++;
                if (currentSpec == spec)
                    summary = $"{LightingEffectSpec.EffectLabel(spec.Kind)} applied ({frameToWrite.Hex} @ 0x{frameToWrite.Brightness:X2})";
            }
            catch (Exception ex)
            {
                if (currentSpec == spec)
                {
                    wantRunning = false;
                    lastError = ex.Message;
                    summary = "Error: " + ex.Message;
                }
            }
            Changed?.Invoke();
        }
        return Status;
    }

    public static async Task StopAsync(bool restoreBaseline = true)
    {
        await ControlLock.WaitAsync();
        try
        {
            wantRunning = false;
            await CancelWorkerLockedAsync(restoreBaseline && !ReviewSandbox.Active);
            isBleConnected = false;
            activeGameExe = null;
            if (lastError == null) summary = "Stopped";
        }
        finally
        {
            ControlLock.Release();
        }
        Changed?.Invoke();
    }

    public static void Shutdown()
    {
        try { StopAsync(restoreBaseline: false).Wait(TimeSpan.FromMilliseconds(1500)); } catch { }
    }

    public static void RecordExternalNativeState(byte r, byte g, byte b, byte brightness, byte mode)
    {
        var kind = mode switch
        {
            LightingRecord.ModeOff => LightingEffectKind.NativeOff,
            LightingRecord.ModeRainbow => LightingEffectKind.NativeRainbow,
            LightingRecord.ModeBreathe => LightingEffectKind.NativeBreathe,
            LightingRecord.ModeCycle => LightingEffectKind.NativeCycle,
            _ => LightingEffectKind.Solid,
        };
        currentSpec = new LightingEffectSpec
        {
            Kind = kind,
            Palette = [$"#{r:X2}{g:X2}{b:X2}"],
            MaxBrightness = brightness,
        }.Normalized();
        previewFrame = new(r, g, b, brightness, mode);
        if (!ReviewSandbox.Active)
        {
            try { Store.RememberLastEffect(currentSpec); } catch { }
        }
        Changed?.Invoke();
    }

    static async Task<IAsyncDisposable?> YieldForExclusiveBleAsync(CancellationToken ct)
    {
        if (ReviewSandbox.Active) return null;
        await ControlLock.WaitAsync(ct);
        try
        {
            if (!wantRunning && workerTask == null) return null;
            yieldDepth++;
            await CancelWorkerLockedAsync(restoreBaseline: false);
            return new YieldToken();
        }
        finally
        {
            ControlLock.Release();
        }
    }

    sealed class YieldToken : IAsyncDisposable
    {
        int released;
        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref released, 1) != 0) return;
            await ControlLock.WaitAsync();
            try
            {
                yieldDepth = Math.Max(0, yieldDepth - 1);
                if (yieldDepth == 0 && wantRunning && workerTask == null)
                    StartWorkerLocked();
            }
            finally
            {
                ControlLock.Release();
            }
        }
    }

    static void StartWorkerLocked()
    {
        workerCts = new CancellationTokenSource();
        var specSnapshot = currentSpec;
        var cts = workerCts;
        workerTask = Task.Run(() => WorkerLoopAsync(specSnapshot, cts.Token));
    }

    static async Task CancelWorkerLockedAsync(bool restoreBaseline)
    {
        restoreOnCancel = restoreBaseline && currentSpec.RestoreOnStop;
        var cts = workerCts;
        var task = workerTask;
        workerCts = null;
        workerTask = null;
        if (cts != null)
        {
            try { cts.Cancel(); } catch { }
            cts.Dispose();
        }
        if (task != null)
        {
            try { await task; } catch { }
        }
        SystemAudioLevelSampler.Reset();
        restoreOnCancel = false;
    }

    static async Task WorkerLoopAsync(LightingEffectSpec rootSpec, CancellationToken ct)
    {
        if (ReviewSandbox.Active)
        {
            while (!ct.IsCancellationRequested)
            {
                EvaluatePreview();
                Changed?.Invoke();
                try { await Task.Delay(rootSpec.StepIntervalMs, ct); } catch (OperationCanceledException) { break; }
            }
            return;
        }

        if (rootSpec.Kind == LightingEffectKind.GameAutoProfile)
        {
            await RunGameAutoLoopAsync(rootSpec, ct);
            return;
        }

        await RunStreamSpecAsync(rootSpec, ct);
    }

    static async Task RunGameAutoLoopAsync(LightingEffectSpec rootSpec, CancellationToken ct)
    {
        string? appliedKey = null;
        CancellationTokenSource? innerCts = null;
        Task? innerTask = null;

        async Task StopInnerAsync()
        {
            if (innerCts != null) { try { innerCts.Cancel(); } catch { } innerCts.Dispose(); innerCts = null; }
            if (innerTask != null) { try { await innerTask; } catch { } innerTask = null; }
        }

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var subSpec = ResolveEffectiveGameSpec(rootSpec, out string? exe);
                string nextKey = (exe ?? "<desktop>") + "|" + subSpec.Kind + "|" + string.Join(",", subSpec.Palette) + "|" + subSpec.MaxBrightness;
                if (nextKey != appliedKey)
                {
                    await StopInnerAsync();
                    appliedKey = nextKey;
                    activeGameExe = exe;
                    if (subSpec.IsNativeMode)
                    {
                        var frame = LightingEffectEngine.Evaluate(subSpec, TimeSpan.Zero);
                        var mode = subSpec.ToNativeModeByte() ?? LightingRecord.ModeSolid;
                        try
                        {
                            await LightingBleClient.RunWithoutYieldAsync([frame.R, frame.G, frame.B], mode, frame.Brightness, ct);
                            totalWrites++;
                            summary = exe == null
                                ? "Game auto: Desktop fallback applied"
                                : $"Game auto ({exe}): {LightingEffectSpec.EffectLabel(subSpec.Kind)}";
                            lastError = null;
                        }
                        catch (Exception ex) { lastError = ex.Message; summary = "Game auto waiting: " + ex.Message; }
                    }
                    else
                    {
                        innerCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                        var token = innerCts.Token;
                        innerTask = Task.Run(() => RunStreamSpecAsync(subSpec, token), ct);
                        summary = exe == null
                            ? $"Game auto: Streaming fallback ({LightingEffectSpec.EffectLabel(subSpec.Kind)})"
                            : $"Game auto ({exe}): Streaming {LightingEffectSpec.EffectLabel(subSpec.Kind)}";
                    }
                    Changed?.Invoke();
                }

                try { await Task.Delay(1000, ct); } catch (OperationCanceledException) { break; }
            }
        }
        finally
        {
            await StopInnerAsync();
        }
    }

    static async Task RunStreamSpecAsync(LightingEffectSpec spec, CancellationToken ct)
    {
        int consecutiveErrors = 0;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var session = await LightingBleClient.OpenStreamAsync(ct);
                isBleConnected = true;
                consecutiveErrors = 0;
                lastError = null;
                summary = $"Streaming {LightingEffectSpec.EffectLabel(spec.Kind)} on BLE";
                Changed?.Invoke();

                var lastSentUtc = DateTimeOffset.MinValue;
                try
                {
                    while (!ct.IsCancellationRequested)
                    {
                        var (input, pressAge) = ReadInputState();
                        var frame = EvaluateFrame(spec, DateTimeOffset.UtcNow - effectStartUtc, input, pressAge);
                        previewFrame = frame;

                        bool sent = await session.SendFrameAsync(frame, ct);
                        if (sent)
                        {
                            lastSentUtc = DateTimeOffset.UtcNow;
                            sessionWrites++;
                            totalWrites++;
                            summary = $"Live BLE · {frame.Hex} · {Math.Round(LightingRecord.BrightnessPercentFor(frame.Brightness))}% (0x{frame.Brightness:X2}) · {sessionWrites} writes";
                        }
                        else
                        {
                            skippedDuplicates++;
                        }
                        Changed?.Invoke();

                        if (spec.MaxSessionWrites > 0 && sessionWrites >= spec.MaxSessionWrites)
                        {
                            wantRunning = false;
                            summary = $"Stopped at write budget ({sessionWrites} writes)";
                            Changed?.Invoke();
                            return;
                        }

                        int waitMs = spec.StepIntervalMs;
                        await Task.WhenAny(
                            Task.Delay(waitMs, ct),
                            WakeSignal.WaitAsync(ct));

                        // Enforce hardware-safe minimum cadence on rapid button bursts.
                        var sinceWrite = DateTimeOffset.UtcNow - lastSentUtc;
                        if (sinceWrite.TotalMilliseconds < LightingEffectSpec.MinStepIntervalMs && !ct.IsCancellationRequested)
                        {
                            int pad = LightingEffectSpec.MinStepIntervalMs - (int)sinceWrite.TotalMilliseconds;
                            if (pad > 0) await Task.Delay(pad, ct);
                        }
                    }
                }
                finally
                {
                    if (restoreOnCancel) await session.TryRestoreBaselineAsync();
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                isBleConnected = false;
                consecutiveErrors++;
                lastError = ex.Message;
                summary = $"BLE stream paused ({ex.Message})";
                Changed?.Invoke();
                if (consecutiveErrors >= 5)
                {
                    wantRunning = false;
                    summary = "Stopped after BLE disconnect: " + ex.Message;
                    Changed?.Invoke();
                    return;
                }
                try { await Task.Delay(TimeSpan.FromSeconds(Math.Min(6, consecutiveErrors * 2)), ct); }
                catch (OperationCanceledException) { break; }
            }
            finally
            {
                isBleConnected = false;
            }
        }
    }

    static LightingEffectSpec ResolveEffectiveGameSpec(LightingEffectSpec rootSpec, out string? exe)
    {
        exe = ForegroundApp.Exe;
        if (!string.IsNullOrWhiteSpace(exe) && Store.ResolveForExe(exe) is { } matched)
            return matched.Spec;
        return new LightingEffectSpec
        {
            Kind = LightingEffectKind.Solid,
            Palette = rootSpec.Palette,
            MinBrightness = rootSpec.MinBrightness,
            MaxBrightness = rootSpec.MaxBrightness,
        }.Normalized();
    }

    static LightingFrame EvaluateCurrent() => LightingEffectEngine.Evaluate(currentSpec, TimeSpan.Zero);

    static (LightingInputState Input, TimeSpan? PressAge) ReadInputState()
    {
        var now = DateTimeOffset.UtcNow;
        lock (InputLock)
        {
            double idle = Math.Max(0, (now - lastActivityUtc).TotalSeconds);
            TimeSpan? age = lastButtonPressUtc == DateTimeOffset.MinValue ? null : now - lastButtonPressUtc;
            return (new LightingInputState(leftMag, rightMag, leftTrig, rightTrig, heldButtons > 0, idle), age);
        }
    }
}
