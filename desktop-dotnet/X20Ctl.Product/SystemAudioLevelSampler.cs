using System.Diagnostics;
using System.Runtime.InteropServices;
using X20Ctl.Zone.Lighting;

namespace X20Ctl.Product;

/// <summary>
/// Reads live system audio output peaks via Windows Core Audio (<c>IAudioMeterInformation</c>)
/// with zero third-party dependencies and feeds <see cref="AudioLevelMeter"/> for Audio Reactive RGB.
/// Handles default audio device switches safely without throwing.
/// </summary>
internal static class SystemAudioLevelSampler
{
    static readonly Guid ClsidMmDeviceEnumerator = new("BCDE0395-E52F-467C-8E3D-C4579291692E");
    static readonly Guid IidAudioMeterInformation = new("C02216F6-8C67-4B5B-9D00-D008E73E0064");

    const uint ClsCtxAll = 0x17;
    const int DataFlowRender = 0;
    const int RoleConsole = 0;
    const int RoleMultimedia = 1;

    static readonly object Gate = new();
    static readonly AudioLevelMeter Meter = new();
    static IAudioMeterInformation? meterInfo;
    static long lastTickTimestamp;

    public static LightingFrame Sample(
        LightingEffectSpec spec,
        TimeSpan elapsed,
        LightingInputState input,
        TimeSpan? pressAge)
    {
        float smoothedLevel = SampleLevel();
        return LightingEffectEngine.Evaluate(spec, elapsed, input with { AudioLevel = smoothedLevel }, pressAge);
    }

    public static float SampleLevel()
    {
        lock (Gate)
        {
            long now = Stopwatch.GetTimestamp();
            double dt = lastTickTimestamp == 0
                ? 0.065
                : Math.Clamp(Stopwatch.GetElapsedTime(lastTickTimestamp, now).TotalSeconds, 0.005, 0.50);
            lastTickTimestamp = now;

            try
            {
                meterInfo ??= AcquireDefaultRenderMeter();
                if (meterInfo != null && meterInfo.GetPeakValue(out float peak) == 0)
                {
                    Meter.ProcessPeak(peak, dt);
                    return Meter.Level;
                }
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

            Meter.ProcessPeak(0f, dt);
            return Meter.Level;
        }
    }

    public static void Reset()
    {
        lock (Gate)
        {
            ReleaseMeterLocked();
            Meter.Reset();
            lastTickTimestamp = 0;
        }
    }

    static IAudioMeterInformation? AcquireDefaultRenderMeter()
    {
        var enumeratorType = Type.GetTypeFromCLSID(ClsidMmDeviceEnumerator, throwOnError: false);
        if (enumeratorType == null) return null;

        object? rawEnum = Activator.CreateInstance(enumeratorType);
        if (rawEnum is not IMMDeviceEnumerator enumerator) return null;
        try
        {
            IMMDevice? device = null;
            int hr = enumerator.GetDefaultAudioEndpoint(DataFlowRender, RoleMultimedia, out device);
            if (hr != 0 || device == null)
                hr = enumerator.GetDefaultAudioEndpoint(DataFlowRender, RoleConsole, out device);
            if (hr != 0 || device == null) return null;

            try
            {
                Guid iid = IidAudioMeterInformation;
                if (device.Activate(ref iid, ClsCtxAll, IntPtr.Zero, out object? activated) != 0)
                    return null;
                return activated as IAudioMeterInformation;
            }
            finally
            {
                if (Marshal.IsComObject(device))
                    Marshal.ReleaseComObject(device);
            }
        }
        finally
        {
            if (Marshal.IsComObject(rawEnum))
                Marshal.ReleaseComObject(rawEnum);
        }
    }

    static void ReleaseMeterLocked()
    {
        if (meterInfo != null)
        {
            try
            {
                if (Marshal.IsComObject(meterInfo))
                    Marshal.ReleaseComObject(meterInfo);
            }
            catch { }
            meterInfo = null;
        }
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
}
