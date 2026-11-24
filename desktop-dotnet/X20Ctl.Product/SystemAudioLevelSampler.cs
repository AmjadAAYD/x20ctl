using System.Diagnostics;
using System.Runtime.InteropServices;
using X20Ctl.Zone.Lighting;

namespace X20Ctl.Product;

/// <summary>
/// Real-time Windows WASAPI Loopback PCM capture + 1024-point FFT analyzer for Audio Reactive RGB
/// and 4-motor Audio Haptics (Left Grip, Right Grip, Left Trigger, Right Trigger), with WebView2
/// trigger-rumble streaming and automatic XInput background failover.
/// </summary>
internal static class SystemAudioLevelSampler
{
    static readonly Guid ClsidMmDeviceEnumerator = new("BCDE0395-E52F-467C-8E3D-C4579291692E");
    static readonly Guid IidAudioClient = new("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2");
    static readonly Guid IidAudioCaptureClient = new("C8ADBD64-E71E-48a0-A4DE-185C395CD317");
    static readonly Guid IidAudioMeterInformation = new("C02216F6-8C67-4B5B-9D00-D008E73E0064");
    static readonly Guid SubtypeIeeeFloat = new("00000003-0000-0010-8000-00aa00389b71");
    static readonly Guid SubtypePcm = new("00000001-0000-0010-8000-00aa00389b71");

    const uint ClsCtxAll = 0x17;
    const int DataFlowRender = 0;
    const int RoleConsole = 0;
    const int RoleMultimedia = 1;
    const int AudioShareModeShared = 0;
    const uint AudioStreamFlagsLoopback = 0x00020000;
    const uint AudioBufferFlagSilent = 0x2;

    static readonly object LoopbackLock = new();
    static readonly object MeterLock = new();
    static readonly object HapticsLock = new();

    static readonly AudioSpectrumAnalyzer Analyzer = new();

    static Thread? captureThread;
    static volatile bool stopRequested;
    static volatile bool loopbackActive;
    static long lastLoopbackRetryTick;

    static IAudioMeterInformation? meterFallbackInfo;
    static long lastMeterTick;

    static int activeRumbleSlot = -1;
    static int lastKnownSlot = -1;
    static bool rumbleActive;
    static bool triggerStreamActive;
    static volatile bool triggersStreamingLive;
    static long lastTriggerDispatchTick;
    static double lastSentLt, lastSentRt;

    public static AudioFrame LatestFrame => Analyzer.CurrentFrame;
    public static HapticsFrame LatestHaptics { get; private set; }
    public static int ActiveXInputSlot => Volatile.Read(ref lastKnownSlot);
    public static bool TriggersStreaming => triggersStreamingLive;
    public static string HapticsPathSummary { get; private set; } = "Haptics idle";

    public static void SilenceHapticsNow() => SilenceHaptics();

    public static void EnsureStarted()
    {
        lock (LoopbackLock)
        {
            if (captureThread is { IsAlive: true }) return;
            long now = Stopwatch.GetTimestamp();
            if (lastLoopbackRetryTick != 0 && Stopwatch.GetElapsedTime(lastLoopbackRetryTick, now).TotalSeconds < 3.0)
                return;

            stopRequested = false;
            lastLoopbackRetryTick = now;
            captureThread = new Thread(CaptureLoopThreadProc)
            {
                IsBackground = true,
                Name = "X20CTL.WasapiLoopback",
            };
            captureThread.Start();
        }
    }

    public static LightingFrame Sample(
        LightingEffectSpec spec,
        TimeSpan elapsed,
        LightingInputState input,
        TimeSpan? pressAge,
        bool driveHaptics = true)
    {
        EnsureStarted();
        var frame = SampleAudioFrame();
        if (driveHaptics && !UniversalHapticsService.IsRunning)
            UpdateAudioHaptics(spec, frame, elapsed.TotalSeconds);

        var enriched = input with
        {
            AudioLevel = frame.Level,
            AudioBass = frame.Bass,
            AudioMid = frame.Mid,
            AudioTreble = frame.Treble,
            AudioBeatPulse = frame.BeatPulse,
            AudioBeatCount = frame.BeatCount,
            AudioCentroid = frame.SpectralCentroid,
            AudioTrebleTransient = frame.TrebleTransient,
            AudioEnergy = frame.Energy,
            AudioVocal = frame.VocalPresence,
            AudioInstrument = frame.InstrumentEnergy,
            AudioBassPunch = frame.BassPunch,
            AudioDynamic = frame.DynamicContrast,
            AudioVibe = frame.Vibe,
        };
        return LightingEffectEngine.Evaluate(spec, elapsed, enriched, pressAge);
    }

    public static AudioFrame SampleAudioFrame()
    {
        if (loopbackActive)
            return Analyzer.CurrentFrame;

        lock (MeterLock)
        {
            long now = Stopwatch.GetTimestamp();
            double dt = lastMeterTick == 0
                ? 0.065
                : Math.Clamp(Stopwatch.GetElapsedTime(lastMeterTick, now).TotalSeconds, 0.005, 0.50);
            lastMeterTick = now;

            try
            {
                meterFallbackInfo ??= AcquireFallbackMeter();
                if (meterFallbackInfo != null && meterFallbackInfo.GetPeakValue(out float peak) == 0)
                    return Analyzer.ProcessFallbackPeak(peak, dt);
            }
            catch (COMException)
            {
                ReleaseMeterLocked();
            }
            catch (InvalidCastException)
            {
                ReleaseMeterLocked();
            }
            catch (Exception)
            {
                ReleaseMeterLocked();
            }

            return Analyzer.ProcessFallbackPeak(0f, dt);
        }
    }

    public static void Reset()
    {
        Thread? toJoin;
        lock (LoopbackLock)
        {
            stopRequested = true;
            loopbackActive = false;
            lastLoopbackRetryTick = 0;
            toJoin = captureThread;
            captureThread = null;
        }
        if (toJoin is { IsAlive: true } && toJoin != Thread.CurrentThread)
        {
            try { toJoin.Join(300); } catch { }
        }

        lock (MeterLock)
        {
            ReleaseMeterLocked();
            lastMeterTick = 0;
        }

        SilenceHaptics();
        Analyzer.Reset();
    }

    public static void DispatchExternalHaptics(HapticsFrame hf, bool lyricalSplit, bool customRoles = false)
    {
        if (ReviewSandbox.Active) return;
        LatestHaptics = hf;
        DispatchFrameCore(hf, lyricalSplit, customRoles);
    }

    static void UpdateAudioHaptics(LightingEffectSpec spec, AudioFrame frame, double elapsedSec)
    {
        if (spec.AudioHapticsStrength <= 0)
        {
            SilenceHaptics();
            HapticsPathSummary = "Audio Haptics off · pick Light, Punch, or Max";
            return;
        }

        var hf = AudioHapticsEngine.Evaluate(frame, spec.AudioHapticsStyle, spec.AudioHapticsStrength, spec.AudioMotorRoles, elapsedSec);
        LatestHaptics = hf;
        bool lyricalSplit = spec.AudioHapticsStyle is AudioHapticsMode.LyricalMusical or AudioHapticsMode.AutoAdaptive;
        DispatchFrameCore(hf, lyricalSplit, !spec.AudioMotorRoles.IsAllAuto);
    }

    static void DispatchFrameCore(HapticsFrame hf, bool lyricalSplit, bool customRoles)
    {
        var browser = Diagnostics.BrowserTriggerRumble.Shared;
        bool canAttemptTriggerStream = browser is { Armed: true, Failure: null, SupportsTriggerRumble: true, IsHostWindowActive: true };
        bool streamConfirmedLive = canAttemptTriggerStream && browser!.StreamLive && browser.HasDocumentFocus;

        if (hf.IsSilent)
        {
            SilenceHaptics();
            int probedSlot = ControllerRumble.FindPhysicalSlot();
            Volatile.Write(ref lastKnownSlot, probedSlot);
            UpdateHapticsSummary(probedSlot, browser, streamConfirmedLive);
            return;
        }

        if (canAttemptTriggerStream)
        {
            long now = Environment.TickCount64;
            long elapsedSinceDispatch = lastTriggerDispatchTick == 0 ? 1000 : now - lastTriggerDispatchTick;
            bool sharpOnset = (hf.LeftTrigger - lastSentLt >= 0.18 || hf.RightTrigger - lastSentRt >= 0.18) && elapsedSinceDispatch >= 35;

            if (!triggerStreamActive || elapsedSinceDispatch >= 65 || sharpOnset)
            {
                browser!.Stream4(hf.LeftGrip, hf.RightGrip, hf.LeftTrigger, hf.RightTrigger, 160);
                triggerStreamActive = true;
                lastTriggerDispatchTick = now;
                lastSentLt = hf.LeftTrigger;
                lastSentRt = hf.RightTrigger;
            }
            streamConfirmedLive = browser!.StreamLive && browser.HasDocumentFocus;
            triggersStreamingLive = streamConfirmedLive;
        }
        else if (triggerStreamActive)
        {
            triggerStreamActive = false;
            triggersStreamingLive = false;
            lastSentLt = 0;
            lastSentRt = 0;
            browser?.StopStream();
        }
        else
        {
            triggersStreamingLive = false;
        }

        int activeSlot;
        if (streamConfirmedLive)
        {
            // When 4-motor Bluetooth Classic trigger-rumble is actively streaming via WebView2, do NOT call XInputSetState
            // concurrently because XInputSetState sends LT=0, RT=0 and wipes out trigger vibration.
            ClearGripXInputOnly();
            activeSlot = ControllerRumble.FindPhysicalSlot();
            Volatile.Write(ref lastKnownSlot, activeSlot);
        }
        else
        {
            // On 2.4G/USB: map independent trigger sensations cleanly onto XInput motors while preserving stereo split.
            double gripL;
            double gripR;
            if (customRoles)
            {
                gripL = Math.Max(hf.LeftGrip, hf.LeftTrigger);
                gripR = Math.Max(hf.RightGrip, hf.RightTrigger);
            }
            else if (lyricalSplit)
            {
                // Preserve clean Left-vs-Right grip separation: never bleed faint Right Grip onto the heavy Left Grip motor.
                gripL = hf.LeftGrip;
                double vocalAccent = Math.Max(hf.LeftTrigger, hf.RightTrigger);
                gripR = Math.Clamp(Math.Max(hf.RightGrip, vocalAccent * 0.65), 0.0, 1.0);
            }
            else
            {
                gripL = Math.Max(hf.LeftGrip, hf.LeftTrigger * 0.90);
                gripR = Math.Max(hf.RightGrip, hf.RightTrigger * 0.95);
            }
            int leftMotor = (int)Math.Clamp(Math.Round(gripL * 65535.0), 0, 65535);
            int rightMotor = (int)Math.Clamp(Math.Round(gripR * 65535.0), 0, 65535);
            activeSlot = ApplyXInputGrips(leftMotor, rightMotor);
        }

        UpdateHapticsSummary(activeSlot, browser, streamConfirmedLive);
    }

    static void UpdateHapticsSummary(int slot, Diagnostics.BrowserTriggerRumble? browser, bool streamConfirmedLive)
    {
        if (slot < 0 && browser is not { Armed: true, SupportsTriggerRumble: true })
        {
            HapticsPathSummary = "No XInput pad found · check 2.4G dongle, USB cable, or BT";
            return;
        }
        if (browser == null)
        {
            HapticsPathSummary = $"Grips live (XInput Slot {slot})";
            return;
        }
        if (browser.Failure != null)
        {
            HapticsPathSummary = $"Grips live (Slot {slot}) · {browser.Failure}";
            return;
        }
        if (browser is { Armed: true, SupportsTriggerRumble: false })
        {
            HapticsPathSummary = $"2.4G/USB Linked Mode (Slot {slot}) · Press M + BACK on pad to toggle LT/RT triggers ON (or use BT mode for independent 4-motor)";
            return;
        }
        if (!browser.Armed)
        {
            HapticsPathSummary = browser.WaitingToArm
                ? $"2.4G/USB Linked (Slot {slot}, M + BACK for LT/RT) · Press any pad button now to detect BT 4-motor"
                : $"2.4G/USB Linked Mode (Slot {slot}) · Press M + BACK on pad to toggle LT/RT triggers ON (or click Arm Triggers in BT mode)";
            return;
        }
        if (!browser.IsHostWindowActive)
        {
            HapticsPathSummary = $"Grips live (Slot {slot}) · BT 4-motor triggers paused in background";
            return;
        }
        if (streamConfirmedLive || browser.StreamLive)
        {
            string slotTag = slot >= 0 ? $"Slot {slot} " : "";
            HapticsPathSummary = $"4-Motor Live ({slotTag}Grips + LT/RT Triggers)";
            return;
        }
        HapticsPathSummary = $"Grips live (Slot {slot}) · Trigger state: {browser.LastStreamState}";
    }

    static int ApplyXInputGrips(int leftMotor, int rightMotor)
    {
        lock (HapticsLock)
        {
            int slot = ControllerRumble.FindPhysicalSlot();
            Volatile.Write(ref lastKnownSlot, slot);
            if (slot < 0)
            {
                rumbleActive = false;
                activeRumbleSlot = -1;
                return -1;
            }
            if (rumbleActive && activeRumbleSlot >= 0 && activeRumbleSlot != slot)
                ControllerRumble.Set(activeRumbleSlot, 0, 0);

            if (leftMotor <= 0 && rightMotor <= 0)
            {
                if (rumbleActive)
                {
                    ControllerRumble.Set(slot, 0, 0);
                    rumbleActive = false;
                }
                activeRumbleSlot = slot;
                return slot;
            }

            ControllerRumble.Set(slot, leftMotor, rightMotor);
            activeRumbleSlot = slot;
            rumbleActive = true;
            return slot;
        }
    }

    static void ClearGripXInputOnly()
    {
        lock (HapticsLock)
        {
            if (rumbleActive && activeRumbleSlot >= 0)
                ControllerRumble.Set(activeRumbleSlot, 0, 0);
            rumbleActive = false;
        }
    }

    static void SilenceHaptics()
    {
        LatestHaptics = HapticsFrame.Zero;
        triggersStreamingLive = false;
        lastSentLt = 0;
        lastSentRt = 0;
        lastTriggerDispatchTick = 0;
        if (triggerStreamActive)
        {
            triggerStreamActive = false;
            Diagnostics.BrowserTriggerRumble.Shared?.StopStream();
        }
        ClearGripXInputOnly();
    }

    static void CaptureLoopThreadProc()
    {
        float[] floatScratch = [];
        short[] shortScratch = [];
        int consecutiveFailures = 0;

        while (!stopRequested)
        {
            IAudioClient? audioClient = null;
            IAudioCaptureClient? captureClient = null;
            IntPtr mixFormatPtr = IntPtr.Zero;

            try
            {
                var device = AcquireDefaultRenderDevice();
                if (device == null)
                {
                    loopbackActive = false;
                    if (++consecutiveFailures >= 3) break;
                    Thread.Sleep(250);
                    continue;
                }

                try
                {
                    Guid iidClient = IidAudioClient;
                    if (device.Activate(ref iidClient, ClsCtxAll, IntPtr.Zero, out object? rawClient) != 0 ||
                        rawClient is not IAudioClient client)
                    {
                        loopbackActive = false;
                        if (++consecutiveFailures >= 3) break;
                        Thread.Sleep(250);
                        continue;
                    }
                    audioClient = client;
                }
                finally
                {
                    ReleaseCom(device);
                }

                if (audioClient.GetMixFormat(out mixFormatPtr) != 0 || mixFormatPtr == IntPtr.Zero)
                    throw new COMException("Could not read mix format.");

                if (!TryReadWaveFormat(mixFormatPtr, out int channels, out int sampleRate, out int bitsPerSample, out bool isFloat))
                    throw new NotSupportedException("Unsupported WASAPI mix format.");

                const long hnsRequestedDuration = 1_000_000;
                int hrInit = audioClient.Initialize(
                    AudioShareModeShared,
                    AudioStreamFlagsLoopback,
                    hnsRequestedDuration,
                    0,
                    mixFormatPtr,
                    IntPtr.Zero);
                if (hrInit != 0) throw new COMException("AudioClient.Initialize loopback failed.", hrInit);

                Guid iidCapture = IidAudioCaptureClient;
                if (audioClient.GetService(ref iidCapture, out object? rawCapture) != 0 ||
                    rawCapture is not IAudioCaptureClient cap)
                    throw new COMException("Could not activate IAudioCaptureClient.");
                captureClient = cap;

                if (audioClient.Start() != 0)
                    throw new COMException("Could not start WASAPI loopback stream.");

                consecutiveFailures = 0;
                loopbackActive = true;
                long lastPacketTick = Stopwatch.GetTimestamp();
                long lastSilenceTick = lastPacketTick;

                while (!stopRequested)
                {
                    int hrNext = captureClient.GetNextPacketSize(out uint packetFrames);
                    if (hrNext != 0) throw new COMException("GetNextPacketSize failed.", hrNext);

                    bool gotPacket = false;
                    while (!stopRequested && packetFrames > 0)
                    {
                        gotPacket = true;
                        int hrBuf = captureClient.GetBuffer(
                            out IntPtr dataPtr,
                            out uint framesToRead,
                            out uint flags,
                            out _,
                            out _);
                        if (hrBuf != 0) throw new COMException("GetBuffer failed.", hrBuf);

                        try
                        {
                            int nFrames = (int)framesToRead;
                            int totalSamples = nFrames * channels;
                            if (nFrames > 0)
                            {
                                double durSec = (double)nFrames / sampleRate;
                                if (floatScratch.Length < totalSamples)
                                    floatScratch = new float[totalSamples];

                                if ((flags & AudioBufferFlagSilent) != 0 || dataPtr == IntPtr.Zero)
                                {
                                    Array.Clear(floatScratch, 0, totalSamples);
                                    Analyzer.ProcessInterleaved(floatScratch.AsSpan(0, totalSamples), channels, sampleRate, durSec);
                                }
                                else if (isFloat && bitsPerSample == 32)
                                {
                                    Marshal.Copy(dataPtr, floatScratch, 0, totalSamples);
                                    Analyzer.ProcessInterleaved(floatScratch.AsSpan(0, totalSamples), channels, sampleRate, durSec);
                                }
                                else if (!isFloat && bitsPerSample == 16)
                                {
                                    if (shortScratch.Length < totalSamples)
                                        shortScratch = new short[totalSamples];
                                    Marshal.Copy(dataPtr, shortScratch, 0, totalSamples);
                                    for (int i = 0; i < totalSamples; i++)
                                        floatScratch[i] = shortScratch[i] / 32768f;
                                    Analyzer.ProcessInterleaved(floatScratch.AsSpan(0, totalSamples), channels, sampleRate, durSec);
                                }
                            }
                        }
                        finally
                        {
                            captureClient.ReleaseBuffer(framesToRead);
                        }

                        hrNext = captureClient.GetNextPacketSize(out packetFrames);
                        if (hrNext != 0) throw new COMException("GetNextPacketSize failed.", hrNext);
                    }

                    long nowTick = Stopwatch.GetTimestamp();
                    if (gotPacket)
                    {
                        lastPacketTick = nowTick;
                        lastSilenceTick = nowTick;
                    }
                    else if (Stopwatch.GetElapsedTime(lastPacketTick, nowTick).TotalMilliseconds >= 18.0)
                    {
                        double silenceDt = Stopwatch.GetElapsedTime(lastSilenceTick, nowTick).TotalSeconds;
                        Analyzer.TickSilence(silenceDt);
                        lastSilenceTick = nowTick;
                    }

                    Thread.Sleep(12);
                }
            }
            catch (Exception)
            {
                loopbackActive = false;
                if (++consecutiveFailures >= 3) break;
                if (!stopRequested) Thread.Sleep(250);
            }
            finally
            {
                loopbackActive = false;
                try { audioClient?.Stop(); } catch { }
                ReleaseCom(captureClient);
                ReleaseCom(audioClient);
                if (mixFormatPtr != IntPtr.Zero) Marshal.FreeCoTaskMem(mixFormatPtr);
            }
        }
    }

    static bool TryReadWaveFormat(
        IntPtr ptr,
        out int channels,
        out int sampleRate,
        out int bitsPerSample,
        out bool isFloat)
    {
        ushort tag = (ushort)Marshal.ReadInt16(ptr, 0);
        channels = Math.Max(1, (int)(ushort)Marshal.ReadInt16(ptr, 2));
        sampleRate = Math.Clamp(Marshal.ReadInt32(ptr, 4), 8000, 384000);
        bitsPerSample = (ushort)Marshal.ReadInt16(ptr, 14);
        ushort cbSize = (ushort)Marshal.ReadInt16(ptr, 16);

        if (tag == 3)
        {
            isFloat = true;
            return bitsPerSample == 32;
        }
        if (tag == 1)
        {
            isFloat = false;
            return bitsPerSample == 16;
        }
        if (tag == 0xFFFE && cbSize >= 22)
        {
            byte[] guidBytes = new byte[16];
            Marshal.Copy(IntPtr.Add(ptr, 24), guidBytes, 0, 16);
            var sub = new Guid(guidBytes);
            if (sub == SubtypeIeeeFloat && bitsPerSample == 32)
            {
                isFloat = true;
                return true;
            }
            if (sub == SubtypePcm && bitsPerSample == 16)
            {
                isFloat = false;
                return true;
            }
        }

        isFloat = false;
        return false;
    }

    static IMMDevice? AcquireDefaultRenderDevice()
    {
        var enumeratorType = Type.GetTypeFromCLSID(ClsidMmDeviceEnumerator, throwOnError: false);
        if (enumeratorType == null) return null;

        object? rawEnum = Activator.CreateInstance(enumeratorType);
        if (rawEnum is not IMMDeviceEnumerator enumerator) return null;
        try
        {
            int hr = enumerator.GetDefaultAudioEndpoint(DataFlowRender, RoleMultimedia, out var device);
            if (hr != 0 || device == null)
                hr = enumerator.GetDefaultAudioEndpoint(DataFlowRender, RoleConsole, out device);
            return hr == 0 ? device : null;
        }
        finally
        {
            ReleaseCom(rawEnum);
        }
    }

    static IAudioMeterInformation? AcquireFallbackMeter()
    {
        var device = AcquireDefaultRenderDevice();
        if (device == null) return null;
        try
        {
            Guid iid = IidAudioMeterInformation;
            if (device.Activate(ref iid, ClsCtxAll, IntPtr.Zero, out object? activated) != 0)
                return null;
            return activated as IAudioMeterInformation;
        }
        finally
        {
            ReleaseCom(device);
        }
    }

    static void ReleaseMeterLocked()
    {
        ReleaseCom(meterFallbackInfo);
        meterFallbackInfo = null;
    }

    static void ReleaseCom(object? obj)
    {
        if (obj == null) return;
        try
        {
            if (Marshal.IsComObject(obj))
                Marshal.ReleaseComObject(obj);
        }
        catch { }
    }

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceEnumerator
    {
        [PreserveSig]
        int EnumAudioEndpoints(int dataFlow, int dwStateMask, out IntPtr ppDevices);

        [PreserveSig]
        int GetDefaultAudioEndpoint(int dataFlow, int role, [MarshalAs(UnmanagedType.Interface)] out IMMDevice? ppEndpoint);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDevice
    {
        [PreserveSig]
        int Activate(ref Guid iid, uint dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object? ppInterface);
    }

    [ComImport]
    [Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioMeterInformation
    {
        [PreserveSig]
        int GetPeakValue(out float pfPeak);
    }

    [ComImport]
    [Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioClient
    {
        [PreserveSig]
        int Initialize(int shareMode, uint streamFlags, long hnsBufferDuration, long hnsPeriodicity, IntPtr pFormat, IntPtr audioSessionGuid);

        [PreserveSig]
        int GetBufferSize(out uint pNumBufferFrames);

        [PreserveSig]
        int GetStreamLatency(out long phnsLatency);

        [PreserveSig]
        int GetCurrentPadding(out uint pNumPaddingFrames);

        [PreserveSig]
        int IsFormatSupported(int shareMode, IntPtr pFormat, out IntPtr ppClosestMatch);

        [PreserveSig]
        int GetMixFormat(out IntPtr ppDeviceFormat);

        [PreserveSig]
        int GetDevicePeriod(out long phnsDefaultDevicePeriod, out long phnsMinimumDevicePeriod);

        [PreserveSig]
        int Start();

        [PreserveSig]
        int Stop();

        [PreserveSig]
        int Reset();

        [PreserveSig]
        int SetEventHandle(IntPtr eventHandle);

        [PreserveSig]
        int GetService(ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object? ppv);
    }

    [ComImport]
    [Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioCaptureClient
    {
        [PreserveSig]
        int GetBuffer(out IntPtr ppData, out uint pNumFramesToRead, out uint pdwFlags, out ulong pu64DevicePosition, out ulong pu64QPCPosition);

        [PreserveSig]
        int ReleaseBuffer(uint numFramesRead);

        [PreserveSig]
        int GetNextPacketSize(out uint pNumFramesInNextPacket);
    }
}
