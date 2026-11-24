using System.Diagnostics;
using X20Ctl.Zone.Lighting;

namespace X20Ctl.Product;

/// <summary>
/// App-wide 50 Hz Universal Haptics coordinator. Runs independently of RGB effects,
/// fuses live audio classification, controller input, foreground app/game detection,
/// menu cursor silencing, and native PC rumble blending.
/// </summary>
internal static class UniversalHapticsService
{
    static readonly object StateLock = new();
    static readonly object InputLock = new();
    static readonly HapticsFusionEngine Engine = new();
    static readonly HashSet<string> PromptedThisSession = new(StringComparer.OrdinalIgnoreCase);

    static HapticsProfileStore? store;
    static CancellationTokenSource? workerCts;
    static Task? workerTask;
    static volatile bool running;
    static volatile bool manualMotorOverride;

    static double padLx, padLy, padRx, padRy, padLt, padRt;
    static int padHeld;
    static PadActionButtons pendingActionButtons;
    static PadActionButtons lastPolledActionBtns;
    static int lastPolledHeld;
    static DateTimeOffset lastInputUtc = DateTimeOffset.UtcNow;
    static DateTimeOffset lastNewPressUtc = DateTimeOffset.MinValue;
    static DateTimeOffset demoUntilUtc = DateTimeOffset.MinValue;

    static HapticsEvaluation latestEval = new(
        HapticsFrame.Zero,
        default,
        HapticsEventTag.None,
        HapticsEventTag.None,
        HapticsGenreProfile.Auto,
        false,
        0.0);

    static string detectedAppName = "Desktop";
    static string? detectedExe;
    static HapticsGenreProfile activeResolvedProfile = HapticsGenreProfile.Auto;

    public static bool IsRunning => running;
    public static HapticsEvaluation Latest => latestEval;
    public static string DetectedAppName => detectedAppName;
    public static string? DetectedExe => detectedExe;
    public static HapticsGenreProfile ActiveResolvedProfile => activeResolvedProfile;

    public static event Action? Changed;

    public static HapticsProfileStore Store
    {
        get
        {
            if (store != null) return store;
            var s = new HapticsProfileStore(HapticsProfileStore.DefaultPath);
            if (!ReviewSandbox.Active)
            {
                try { s.Load(); } catch { }
            }
            store = s;
            return s;
        }
    }

    public static void ResumeIfEnabled()
    {
        if (ReviewSandbox.Active) return;
        if (Store.Snapshot().Enabled)
            Start();
    }

    public static void Start()
    {
        if (ReviewSandbox.Active) return;
        lock (StateLock)
        {
            Store.UpdateSettings(c => c.Enabled = true, persist: true);
            demoUntilUtc = DateTimeOffset.MinValue;
            StartWorkerLocked();
        }
        Changed?.Invoke();
    }

    public static void Stop()
    {
        CancellationTokenSource? cts;
        Task? task;
        lock (StateLock)
        {
            Store.UpdateSettings(c => c.Enabled = false, persist: true);
            running = false;
            demoUntilUtc = DateTimeOffset.MinValue;
            cts = workerCts;
            task = workerTask;
            workerCts = null;
            workerTask = null;
        }
        StopWorkerAndWait(cts, task);
        lock (InputLock)
        {
            pendingActionButtons = PadActionButtons.None;
            lastPolledActionBtns = PadActionButtons.None;
            lastPolledHeld = 0;
        }
        Engine.Reset();
        SystemAudioLevelSampler.SilenceHapticsNow();
        latestEval = new(HapticsFrame.Zero, default, HapticsEventTag.None, HapticsEventTag.None, Store.Snapshot().ManualProfile, false, 0.0);
        Changed?.Invoke();
    }

    public static void Shutdown()
    {
        CancellationTokenSource? cts;
        Task? task;
        lock (StateLock)
        {
            running = false;
            cts = workerCts;
            task = workerTask;
            workerCts = null;
            workerTask = null;
        }
        StopWorkerAndWait(cts, task);
        SystemAudioLevelSampler.SilenceHapticsNow();
    }

    public static void SetManualMotorOverride(bool active)
    {
        manualMotorOverride = active;
    }

    public static void TriggerDemo(HapticsDemoKind kind)
    {
        Engine.TriggerDemoEffect(kind);
        if (ReviewSandbox.Active)
        {
            var cfg = Store.Snapshot();
            var eval = Engine.Evaluate(default, new LivePadInput(0, 0, 0, 0, 0, 0, 0, false), cfg.ManualProfile, Store.GetTuning(cfg.ManualProfile), dtSeconds: 0.04);
            latestEval = eval;
            Changed?.Invoke();
            return;
        }

        lock (StateLock)
        {
            if (!running)
            {
                demoUntilUtc = DateTimeOffset.UtcNow.AddSeconds(1.65);
                StartWorkerLocked();
            }
        }
        Changed?.Invoke();
    }

    public static void NotifySettingsChanged()
    {
        Changed?.Invoke();
    }

    public static void ObserveLiveInput(double lx, double ly, double rx, double ry, double lt, double rt, int heldCount, bool anyNewPress, PadActionButtons pressedButtons = PadActionButtons.None)
    {
        var now = DateTimeOffset.UtcNow;
        bool active = Math.Abs(lx) > 0.14 || Math.Abs(ly) > 0.14
            || Math.Abs(rx) > 0.14 || Math.Abs(ry) > 0.14
            || lt > 0.10 || rt > 0.10
            || heldCount > 0 || anyNewPress || pressedButtons != PadActionButtons.None;

        lock (InputLock)
        {
            padLx = lx;
            padLy = ly;
            padRx = rx;
            padRy = ry;
            padLt = Math.Clamp(lt, 0.0, 1.0);
            padRt = Math.Clamp(rt, 0.0, 1.0);
            padHeld = heldCount;
            pendingActionButtons |= pressedButtons;
            if (active) lastInputUtc = now;
            if (anyNewPress || pressedButtons != PadActionButtons.None) lastNewPressUtc = now;
        }
    }

    static void StartWorkerLocked()
    {
        if (workerTask is { IsCompleted: false })
        {
            running = true;
            return;
        }
        running = true;
        workerCts = new CancellationTokenSource();
        var ct = workerCts.Token;
        workerTask = Task.Run(() => WorkerLoopAsync(ct));
    }

    static void StopWorkerAndWait(CancellationTokenSource? cts, Task? task)
    {
        if (cts != null)
        {
            try { cts.Cancel(); } catch { }
            cts.Dispose();
        }
        if (task != null)
        {
            try { task.Wait(400); } catch { }
        }
    }

    static async Task WorkerLoopAsync(CancellationToken ct)
    {
        SystemAudioLevelSampler.EnsureStarted();
        var sw = Stopwatch.StartNew();
        double lastSec = sw.Elapsed.TotalSeconds;
        long lastUiNotifyTick = 0;

        while (!ct.IsCancellationRequested)
        {
            var cfg = Store.Snapshot();
            bool temporaryDemo = !cfg.Enabled && DateTimeOffset.UtcNow < demoUntilUtc;
            if (!cfg.Enabled && !temporaryDemo)
            {
                running = false;
                SystemAudioLevelSampler.SilenceHapticsNow();
                break;
            }

            double nowSec = sw.Elapsed.TotalSeconds;
            double dt = Math.Clamp(nowSec - lastSec, 0.008, 0.10);
            lastSec = nowSec;

            SystemAudioLevelSampler.EnsureStarted();
            PollDirectXInput();

            var audio = temporaryDemo ? default : SystemAudioLevelSampler.SampleAudioFrame();
            var rawFg = ForegroundApp.Current;
            var fg = rawFg;
            if (fg == null && ForegroundApp.Last is { } lastApp)
            {
                var lastMatch = HapticsGameCatalog.Resolve(lastApp.Exe, lastApp.Title);
                if (lastMatch.IsMediaApp && lastMatch.Profile != null)
                    fg = lastApp;
            }

            var (profile, unknownGame, dispName) = Store.ResolveActive(fg?.Exe, fg?.Title);
            var catMatch = HapticsGameCatalog.Resolve(fg?.Exe, fg?.Title);
            bool explicitGameProf = profile is HapticsGenreProfile.Racing or HapticsGenreProfile.FpsAction or HapticsGenreProfile.RpgMelee or HapticsGenreProfile.SportsArcade;
            bool isGameFg = explicitGameProf
                || (rawFg != null
                    && !catMatch.IsMediaApp
                    && !catMatch.IsIgnored
                    && !Store.IsAppIgnored(rawFg.Exe)
                    && (catMatch.Profile is HapticsGenreProfile.Racing or HapticsGenreProfile.FpsAction or HapticsGenreProfile.RpgMelee or HapticsGenreProfile.SportsArcade
                        || ForegroundApp.IsForegroundFullscreen()));
            detectedExe = fg?.Exe;
            detectedAppName = string.IsNullOrWhiteSpace(dispName) ? (fg?.Exe ?? "Desktop") : dispName;
            activeResolvedProfile = profile;

            if (unknownGame && cfg.AskOnUnknownGame && !temporaryDemo && rawFg?.Exe is { Length: > 0 } uExe && ForegroundApp.IsForegroundFullscreen())
            {
                bool shouldPrompt;
                lock (PromptedThisSession)
                    shouldPrompt = PromptedThisSession.Add(uExe);

                if (shouldPrompt)
                {
                    string capturedExe = uExe;
                    string capturedName = detectedAppName;
                    GameGenrePromptToast.Show(capturedExe, capturedName, chosen =>
                    {
                        if (chosen is { } picked)
                            Store.BindApp(capturedExe, picked, persist: true);
                        else
                            Store.IgnoreApp(capturedExe, persist: true);
                        Changed?.Invoke();
                    });
                }
            }

            var pad = ReadLiveInput(isGameFg);
            var tuning = Store.GetTuning(profile);
            var roles = LightingEffectsService.CurrentSpec.AudioMotorRoles;
            var eval = Engine.Evaluate(audio, pad, profile, tuning, roles, dt);
            latestEval = eval;

            if (!manualMotorOverride)
            {
                var gameSample = GameRumble.Latest;
                bool freshGame = gameSample != null && (Environment.TickCount64 - gameSample.At) < 280;
                double natL = freshGame ? Math.Clamp(gameSample!.PadLeft > 0 ? gameSample.PadLeft / 65535.0 : gameSample.GameLeft / 255.0, 0.0, 1.0) : 0.0;
                double natR = freshGame ? Math.Clamp(gameSample!.PadRight > 0 ? gameSample.PadRight / 65535.0 : gameSample.GameRight / 255.0, 0.0, 1.0) : 0.0;
                var mixed = HapticsMixer.Mix(eval.Motors, natL, natR, tuning.BlendMode);
                SystemAudioLevelSampler.DispatchExternalHaptics(
                    mixed,
                    lyricalSplit: eval.ResolvedProfile == HapticsGenreProfile.Music,
                    customRoles: !roles.IsAllAuto && eval.ResolvedProfile == HapticsGenreProfile.Music);
            }

            long tickMs = Environment.TickCount64;
            if (tickMs - lastUiNotifyTick >= 80)
            {
                lastUiNotifyTick = tickMs;
                Changed?.Invoke();
            }

            if (temporaryDemo && DateTimeOffset.UtcNow >= demoUntilUtc)
            {
                running = false;
                SystemAudioLevelSampler.SilenceHapticsNow();
                Changed?.Invoke();
                break;
            }

            try { await Task.Delay(20, ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    static void PollDirectXInput()
    {
        if (!ControllerRumble.TryReadLiveState(out double lx, out double ly, out double rx, out double ry, out double lt, out double rt, out int heldCount, out var heldBtns))
            return;

        PadActionButtons newlyPressed;
        bool anyNewPress;
        lock (InputLock)
        {
            newlyPressed = heldBtns & ~lastPolledActionBtns;
            lastPolledActionBtns = heldBtns;
            anyNewPress = newlyPressed != PadActionButtons.None || heldCount > lastPolledHeld;
            lastPolledHeld = heldCount;
        }
        ObserveLiveInput(lx, ly, rx, ry, lt, rt, heldCount, anyNewPress, newlyPressed);
    }

    static LivePadInput ReadLiveInput(bool isGameForeground)
    {
        var now = DateTimeOffset.UtcNow;
        bool cursor = ForegroundApp.IsCursorVisible();
        lock (InputLock)
        {
            double idleSec = (now - lastInputUtc).TotalSeconds;
            bool press = (lastNewPressUtc != DateTimeOffset.MinValue && (now - lastNewPressUtc).TotalMilliseconds < 90)
                || pendingActionButtons != PadActionButtons.None;
            var btns = pendingActionButtons;
            pendingActionButtons = PadActionButtons.None;
            return new LivePadInput(padLx, padLy, padRx, padRy, padLt, padRt, padHeld, press, cursor, idleSec >= 4.5, btns, isGameForeground);
        }
    }
}
