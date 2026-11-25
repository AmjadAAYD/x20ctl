using System.Runtime.InteropServices;

namespace X20Ctl.Product;

/// <summary>
/// Pure Win32 / Core Audio COM helper for focused-app volume control, microphone mute toggle,
/// and instant-replay clip capture hotkeys. Fails gracefully if audio endpoints are unavailable.
/// </summary>
internal static class SystemAudioControl
{
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

    static readonly Guid ClsidMmDeviceEnumerator = new("BCDE0395-E52F-467C-8E3D-C4579291692E");
    static readonly Guid IidAudioEndpointVolume = new("5CDF2C82-841E-4546-9722-0CF74078229A");
    static readonly Guid IidAudioSessionManager2 = new("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F");
    static readonly Guid IidSimpleAudioVolume = new("87CE5498-68D6-44E5-9215-6DA47EF883D8");

    enum EDataFlow { eRender = 0, eCapture = 1, eAll = 2 }
    enum ERole { eConsole = 0, eMultimedia = 1, eCommunications = 2 }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceEnumerator
    {
        int EnumAudioEndpoints(EDataFlow dataFlow, uint dwStateMask, out IntPtr ppDevices);
        int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice ppEndpoint);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDevice
    {
        int Activate(ref Guid iid, uint dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
    }

    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioEndpointVolume
    {
        int RegisterControlChangeNotify(IntPtr pNotify);
        int UnregisterControlChangeNotify(IntPtr pNotify);
        int GetChannelCount(out uint pnChannelCount);
        int SetMasterVolumeLevel(float fLevelDB, ref Guid pguidEventContext);
        int SetMasterVolumeLevelScalar(float fLevel, ref Guid pguidEventContext);
        int GetMasterVolumeLevel(out float pfLevelDB);
        int GetMasterVolumeLevelScalar(out float pfLevel);
        int SetChannelVolumeLevel(uint nChannel, float fLevelDB, ref Guid pguidEventContext);
        int SetChannelVolumeLevelScalar(uint nChannel, float fLevel, ref Guid pguidEventContext);
        int GetChannelVolumeLevel(uint nChannel, out float pfLevelDB);
        int GetChannelVolumeLevelScalar(uint nChannel, out float pfLevel);
        int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, ref Guid pguidEventContext);
        int GetMute([MarshalAs(UnmanagedType.Bool)] out bool pbMute);
    }

    [ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioSessionManager2
    {
        int GetAudioSessionControl(IntPtr AudioSessionGuid, uint StreamFlags, out IntPtr SessionControl);
        int GetSimpleAudioVolume(IntPtr AudioSessionGuid, uint StreamFlags, out IntPtr AudioVolume);
        int GetSessionEnumerator(out IAudioSessionEnumerator SessionEnum);
    }

    [ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioSessionEnumerator
    {
        int GetCount(out int SessionCount);
        int GetSession(int SessionCount, out IAudioSessionControl2 Session);
    }

    [ComImport, Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioSessionControl2
    {
        // IAudioSessionControl base vtable slots (9 methods)
        int GetState(out int pRetVal);
        int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);
        int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string Value, ref Guid EventContext);
        int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);
        int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string Value, ref Guid EventContext);
        int GetGroupingParam(out Guid pRetVal);
        int SetGroupingParam(ref Guid Override, ref Guid EventContext);
        int RegisterAudioSessionNotification(IntPtr NewNotifications);
        int UnregisterAudioSessionNotification(IntPtr NewNotifications);
        // IAudioSessionControl2 methods
        int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);
        int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);
        int GetProcessId(out uint pRetVal);
    }

    [ComImport, Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface ISimpleAudioVolume
    {
        int SetMasterVolume(float fLevel, ref Guid EventContext);
        int GetMasterVolume(out float pfLevel);
        int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, ref Guid EventContext);
        int GetMute([MarshalAs(UnmanagedType.Bool)] out bool pbMute);
    }

    /// <summary>Toggles default capture microphone mute. Returns new muted state or null on error.</summary>
    public static bool? ToggleMicMute()
    {
        if (ReviewSandbox.Active) return null;
        try
        {
            var type = Type.GetTypeFromCLSID(ClsidMmDeviceEnumerator);
            if (type == null) return null;
            var enumerator = (IMMDeviceEnumerator)Activator.CreateInstance(type)!;
            if (enumerator.GetDefaultAudioEndpoint(EDataFlow.eCapture, ERole.eCommunications, out var dev) != 0 || dev == null)
                return null;
            var iid = IidAudioEndpointVolume;
            if (dev.Activate(ref iid, 23 /* CLSCTX_ALL */, IntPtr.Zero, out var obj) != 0 || obj is not IAudioEndpointVolume vol)
                return null;
            vol.GetMute(out bool muted);
            Guid ctx = Guid.Empty;
            bool next = !muted;
            vol.SetMute(next, ref ctx);

            // Optional brief RGB confirmation flash (respecting Eco mode and running effect streams)
            if (!UniversalHapticsService.IsPowerSavingActive && !LightingEffectsService.Status.IsRunning)
            {
                byte[] flash = next ? [0xFF, 0x20, 0x20] : [0x20, 0xFF, 0x70];
                _ = Task.Run(async () =>
                {
                    try { await LightingBleClient.RunWithoutYieldAsync(flash, X20Ctl.Zone.Lighting.LightingRecord.ModeBreathe, 0x70); }
                    catch { }
                });
            }
            return next;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Adjusts audio volume scalar (+/- delta) for the currently focused foreground application.</summary>
    public static float? AdjustFocusedAppVolume(float delta)
    {
        if (ReviewSandbox.Active) return null;
        try
        {
            var hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return null;
            GetWindowThreadProcessId(hwnd, out uint targetPid);
            if (targetPid == 0) return null;

            var type = Type.GetTypeFromCLSID(ClsidMmDeviceEnumerator);
            if (type == null) return null;
            var enumerator = (IMMDeviceEnumerator)Activator.CreateInstance(type)!;
            if (enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out var dev) != 0 || dev == null)
                return null;
            var iid = IidAudioSessionManager2;
            if (dev.Activate(ref iid, 23, IntPtr.Zero, out var obj) != 0 || obj is not IAudioSessionManager2 mgr)
                return null;
            if (mgr.GetSessionEnumerator(out var sessions) != 0 || sessions == null)
                return null;

            sessions.GetCount(out int count);
            float? updated = null;
            Guid ctx = Guid.Empty;
            for (int i = 0; i < count; i++)
            {
                if (sessions.GetSession(i, out var ctl) != 0 || ctl == null) continue;
                ctl.GetProcessId(out uint pid);
                if (pid != targetPid) continue;
                if (ctl is ISimpleAudioVolume sav)
                {
                    sav.GetMasterVolume(out float cur);
                    float next = Math.Clamp(cur + delta, 0.0f, 1.0f);
                    sav.SetMasterVolume(next, ref ctx);
                    updated = next;
                }
            }
            return updated;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Triggers Windows Game Bar / GeForce Experience Instant Replay save clip shortcut (Win+Alt+G).</summary>
    public static void TriggerInstantReplay()
    {
        if (ReviewSandbox.Active) return;
        // Win + Alt + G captures last 30s on Xbox Game Bar cleanly across GPUs
        MouseOutput.Output("K:91e", true);  // LWIN (0x5B)
        MouseOutput.Output("K:164", true);  // LMENU (0xA4)
        MouseOutput.Output("K:71", true);   // G (0x47)
        MouseOutput.Output("K:71", false);
        MouseOutput.Output("K:164", false);
        MouseOutput.Output("K:91e", false);
    }
}
