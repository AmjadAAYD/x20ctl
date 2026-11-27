using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using X20Ctl.Zone.D10;

namespace X20Ctl.Product;

public enum D10DriverState
{
    NotFound,
    OfficialXbox,
    WinUsbReady,
    PartialWinUsb,
    Ambiguous,
    Error
}

public sealed record D10DriverProbe(
    D10DriverState State,
    string? InstanceId,
    string? ServiceName,
    string? InfLeaf,
    int ProblemCode,
    bool HasInterfaceGuid,
    string Summary);

public sealed record D10DriverResult(bool Success, bool NeedReboot, string Message);

/// <summary>
/// Non-firmware Windows Plug and Play driver switcher between the official Xbox 360 driver
/// and Windows inbox WinUSB with the D10 custom device interface GUID.
/// </summary>
internal static class D10DriverManager
{
    private const string MutexName = @"Global\X20Ctl.D10DriverSwitch";
    private const uint DigcfPresent = 0x00000002;
    private const uint DigcfAllClasses = 0x00000004;
    private const uint SpdrpHardwareId = 0x00000001;
    private const uint SpdrpService = 0x00000004;
    private const uint DicsFlagGlobal = 0x00000001;
    private const uint DiregDev = 0x00000001;
    private const int KeyRead = 0x20019;
    private const int KeyReadWrite = 0x0002001F;
    private const int ErrorNoMoreItems = 259;

    private const uint DiEnumSingleInf = 0x00010000;
    private const uint DiQuietInstall = 0x00800000;
    private const uint DiFlagsExAllowExcludedDrivers = 0x00000800;
    private const uint SpditClassDriver = 0x00000001;
    private const uint SpditCompatDriver = 0x00000002;
    private const uint DiodInheritClassDrivers = 0x00000002;

    private static readonly IntPtr InvalidHandle = new(-1);
    private static readonly string TargetGuidBrace = D10Wire.WinUsbInterfaceGuid.ToString("B").ToUpperInvariant();

    private static readonly DevPropKey DevKeyDriverInfPath = new()
    {
        FmtId = Guid.Parse("A8B865DD-2E3D-4094-AD97-E593A70C75D6"),
        Pid = 5
    };

    private sealed record FoundDevice(
        string InstanceId,
        string[] HardwareIds,
        string Service,
        string InfLeaf,
        int ProblemCode,
        bool HasInterfaceGuid);

    public static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static D10DriverProbe ProbeDevice()
    {
        try
        {
            var found = EnumerateTargetDevices();
            if (found.Count == 0)
            {
                return new(
                    D10DriverState.NotFound,
                    null, null, null, 0, false,
                    "No EasySMX D10 USB controller or receiver detected.");
            }

            if (found.Count > 1)
            {
                return new(
                    D10DriverState.Ambiguous,
                    null, null, null, 0, false,
                    "Multiple D10 USB paths are connected. Connect only wired USB or the 2.4 GHz receiver before switching drivers.");
            }

            var d = found[0];
            bool winUsbService = d.Service.Equals("WinUSB", StringComparison.OrdinalIgnoreCase);
            bool winUsbInf = d.InfLeaf.Equals("winusb.inf", StringComparison.OrdinalIgnoreCase);
            bool officialInf = IsOfficialXboxInf(d.InfLeaf);

            if (winUsbService && winUsbInf && d.HasInterfaceGuid && d.ProblemCode == 0)
            {
                return new(
                    D10DriverState.WinUsbReady,
                    d.InstanceId, d.Service, d.InfLeaf, d.ProblemCode, d.HasInterfaceGuid,
                    $"Direct USB ready (WinUSB / {d.InfLeaf}) on {d.InstanceId}.");
            }

            if (winUsbService || winUsbInf || d.HasInterfaceGuid)
            {
                return new(
                    D10DriverState.PartialWinUsb,
                    d.InstanceId, d.Service, d.InfLeaf, d.ProblemCode, d.HasInterfaceGuid,
                    $"Partial WinUSB state (Service={d.Service}, Inf={d.InfLeaf}, Guid={d.HasInterfaceGuid}, Code={d.ProblemCode}). Use Switch or Recovery.");
            }

            string desc = officialInf || d.Service.StartsWith("xusb", StringComparison.OrdinalIgnoreCase)
                ? $"Official Xbox 360 driver ({d.Service} / {d.InfLeaf}). Standard XInput active."
                : $"USB driver ({d.Service} / {d.InfLeaf}). Switch to Direct USB (WinUSB) to unlock 6-axis IMU & rear paddles.";

            return new(
                D10DriverState.OfficialXbox,
                d.InstanceId, d.Service, d.InfLeaf, d.ProblemCode, d.HasInterfaceGuid,
                desc);
        }
        catch (Exception ex)
        {
            return new(D10DriverState.Error, null, null, null, -1, false, "Driver probe failed: " + ex.Message);
        }
    }

    public static async Task<D10DriverResult> RunElevatedActionAsync(
        bool switchToWinUsb,
        string instanceId,
        CancellationToken cancel = default)
    {
        if (IsAdministrator())
            return ExecuteElevatedSwitch(switchToWinUsb, instanceId);

        string exePath = Environment.ProcessPath ?? "";
        if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
            return new(false, false, "Executable path unavailable for elevation.");

        string resultFile = Path.Combine(Path.GetTempPath(), $"x20ctl-d10-drv-{Guid.NewGuid():N}.json");
        string modeArg = switchToWinUsb ? "winusb" : "official";
        try
        {
            var psi = new ProcessStartInfo(exePath)
            {
                UseShellExecute = true,
                Verb = "runas",
            };
            psi.ArgumentList.Add("--d10-driver-switch");
            psi.ArgumentList.Add(modeArg);
            psi.ArgumentList.Add(instanceId);
            psi.ArgumentList.Add(resultFile);

            using var proc = Process.Start(psi);
            if (proc == null)
                return new(false, false, "Windows elevation prompt did not start.");

            await proc.WaitForExitAsync(cancel);
            if (File.Exists(resultFile))
            {
                var res = JsonSerializer.Deserialize<D10DriverResult>(await File.ReadAllTextAsync(resultFile, cancel));
                if (res != null) return res;
            }

            return new(proc.ExitCode == 0, false,
                proc.ExitCode == 0 ? "Driver operation completed." : $"Driver helper exited with code {proc.ExitCode}.");
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return new(false, false, "Administrator permission prompt was cancelled.");
        }
        catch (Exception ex)
        {
            return new(false, false, "Elevated driver switch failed: " + ex.Message);
        }
        finally
        {
            try { if (File.Exists(resultFile)) File.Delete(resultFile); } catch { }
        }
    }

    public static D10DriverResult ExecuteElevatedSwitch(bool switchToWinUsb, string instanceId)
    {
        if (!IsAdministrator())
            return new(false, false, "Administrator privileges are required to switch USB drivers.");
        if (string.IsNullOrWhiteSpace(instanceId) || !IsTopLevelTarget(instanceId))
            return new(false, false, "Invalid or composite USB instance ID; select top-level D10 device.");

        Mutex? mutex = null;
        try
        {
            mutex = new Mutex(false, MutexName, out bool created);
            if (!created)
                return new(false, false, "Another driver switch operation is already in progress.");

            return switchToWinUsb
                ? SwitchToWinUsbInternal(instanceId)
                : RestoreOfficialInternal(instanceId);
        }
        catch (Exception ex)
        {
            return new(false, false, ex.Message);
        }
        finally
        {
            mutex?.Dispose();
        }
    }

    private static D10DriverResult SwitchToWinUsbInternal(string instanceId)
    {
        var existing = EnumerateTargetDevices().FirstOrDefault(d => d.InstanceId.Equals(instanceId, StringComparison.OrdinalIgnoreCase));
        if (existing == null)
            return new(false, false, "Target D10 device is no longer connected.");

        bool alreadyBound = existing.Service.Equals("WinUSB", StringComparison.OrdinalIgnoreCase) &&
                            existing.InfLeaf.Equals("winusb.inf", StringComparison.OrdinalIgnoreCase) &&
                            existing.ProblemCode == 0;

        bool needReboot = false;
        if (!alreadyBound)
        {
            string winUsbInf = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "INF", "winusb.inf");
            if (!File.Exists(winUsbInf))
                return new(false, false, "Windows system winusb.inf was not found.");

            needReboot = InstallInboxWinUsb(instanceId, winUsbInf);
        }

        UpdateInterfaceGuid(instanceId, add: true);

        if (needReboot)
            return new(true, true, "Windows bound WinUSB and requests a reboot to finalize Direct USB mode.");

        RestartAndRescan(instanceId);
        if (WaitForState(instanceId, D10DriverState.WinUsbReady, TimeSpan.FromSeconds(20)))
            return new(true, false, "Switched to Direct USB (WinUSB) successfully. Click Connect Direct USB.");

        return new(false, false, "WinUSB was installed, but device did not report ready within 20s. Try unplugging and replugging.");
    }

    private static D10DriverResult RestoreOfficialInternal(string instanceId)
    {
        var existing = EnumerateTargetDevices().FirstOrDefault(d => d.InstanceId.Equals(instanceId, StringComparison.OrdinalIgnoreCase));
        if (existing == null)
            return new(false, false, "Target D10 device is no longer connected.");

        bool needReboot = false;
        bool alreadyOfficial = existing.ProblemCode == 0 && IsOfficialXboxInf(existing.InfLeaf);
        if (!alreadyOfficial)
        {
            string inf = FindOfficialXboxInf();
            needReboot = InstallPublishedInf(instanceId, inf);
        }

        UpdateInterfaceGuid(instanceId, add: false);

        if (needReboot)
            return new(true, true, "Windows restored official Xbox 360 driver and requests a reboot.");

        RestartAndRescan(instanceId);
        if (WaitForState(instanceId, D10DriverState.OfficialXbox, TimeSpan.FromSeconds(20)))
            return new(true, false, "Restored official Xbox 360 driver cleanly.");

        return new(false, false, "Official driver restore ran, but Windows did not re-enumerate in time. Replug controller or reboot.");
    }

    private static bool WaitForState(string instanceId, D10DriverState targetState, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            var probe = ProbeDevice();
            if (probe.State == targetState &&
                string.Equals(probe.InstanceId, instanceId, StringComparison.OrdinalIgnoreCase))
                return true;
            Thread.Sleep(300);
        }
        return false;
    }

    private static List<FoundDevice> EnumerateTargetDevices()
    {
        var list = new List<FoundDevice>();
        IntPtr set = SetupDiGetClassDevsW(IntPtr.Zero, null, IntPtr.Zero, DigcfPresent | DigcfAllClasses);
        if (set == InvalidHandle) return list;

        try
        {
            for (uint i = 0; ; i++)
            {
                var dev = new SpDevinfoData { cbSize = Marshal.SizeOf<SpDevinfoData>() };
                if (!SetupDiEnumDeviceInfo(set, i, ref dev))
                {
                    if (Marshal.GetLastWin32Error() == ErrorNoMoreItems) break;
                    continue;
                }

                string instanceId = GetDeviceInstanceId(set, ref dev);
                if (!IsTopLevelTarget(instanceId)) continue;

                string[] hwIds = GetMultiStringProperty(set, ref dev, SpdrpHardwareId);
                if (!hwIds.Any(IsTargetHardwareId)) continue;

                string service = GetStringProperty(set, ref dev, SpdrpService);
                string infLeaf = Path.GetFileName(GetInfPathProperty(set, ref dev));
                int problemCode = GetProblemCode(dev.DevInst);
                bool hasGuid = HasCustomGuid(set, ref dev);

                list.Add(new FoundDevice(instanceId, hwIds, service, infLeaf, problemCode, hasGuid));
            }
        }
        finally
        {
            _ = SetupDiDestroyDeviceInfoList(set);
        }

        return list;
    }

    private static bool IsTopLevelTarget(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId)) return false;
        if (!instanceId.StartsWith(@"USB\", StringComparison.OrdinalIgnoreCase)) return false;
        if (instanceId.Contains("&MI_", StringComparison.OrdinalIgnoreCase)) return false;
        return instanceId.Contains("VID_045E&PID_028E", StringComparison.OrdinalIgnoreCase) ||
               instanceId.Contains("VID_2345&PID_E062", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTargetHardwareId(string hwId) =>
        hwId.Equals(@"USB\VID_045E&PID_028E", StringComparison.OrdinalIgnoreCase) ||
        hwId.Equals(@"USB\VID_2345&PID_E062", StringComparison.OrdinalIgnoreCase);

    private static bool IsOfficialXboxInf(string infLeaf) =>
        !string.IsNullOrWhiteSpace(infLeaf) &&
        Regex.IsMatch(infLeaf, @"^(xbox_360.*controller.*|xusb22)\.inf$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static string FindOfficialXboxInf()
    {
        string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "INF");
        foreach (string name in new[] { "xusb22.inf", "xbox_360_controller.inf" })
        {
            string path = Path.Combine(root, name);
            if (!File.Exists(path)) continue;
            string text = File.ReadAllText(path);
            if (text.Contains("VID_045E&PID_028E", StringComparison.OrdinalIgnoreCase))
                return Path.GetFullPath(path);
        }
        throw new FileNotFoundException("Inbox Xbox 360 controller INF (xusb22.inf) was not found in Windows\\INF.");
    }

    private static bool InstallInboxWinUsb(string instanceId, string winUsbInfPath)
    {
        IntPtr set = SetupDiCreateDeviceInfoList(IntPtr.Zero, IntPtr.Zero);
        if (set == InvalidHandle)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "SetupDiCreateDeviceInfoList failed.");

        var dev = new SpDevinfoData { cbSize = Marshal.SizeOf<SpDevinfoData>() };
        bool opened = false;
        try
        {
            var globalInstall = new SpDevinstallParams { cbSize = Marshal.SizeOf<SpDevinstallParams>(), DriverPath = "" };
            Check(SetupDiGetDeviceInstallParamsW(set, IntPtr.Zero, ref globalInstall), "SetupDiGetDeviceInstallParams");
            globalInstall.Flags |= DiEnumSingleInf | DiQuietInstall;
            globalInstall.FlagsEx |= DiFlagsExAllowExcludedDrivers;
            globalInstall.DriverPath = winUsbInfPath;
            Check(SetupDiSetDeviceInstallParamsW(set, IntPtr.Zero, ref globalInstall), "SetupDiSetDeviceInstallParams");
            Check(SetupDiBuildDriverInfoList(set, IntPtr.Zero, SpditClassDriver), "SetupDiBuildDriverInfoList(winusb)");

            SpDrvinfoData? selected = null;
            for (uint idx = 0; ; idx++)
            {
                var drv = NewDrvInfo();
                if (!SetupDiEnumDriverInfoW(set, IntPtr.Zero, SpditClassDriver, idx, ref drv))
                {
                    if (Marshal.GetLastWin32Error() == ErrorNoMoreItems) break;
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "SetupDiEnumDriverInfo");
                }
                var detail = GetDriverDetail(set, IntPtr.Zero, ref drv);
                if (drv.ProviderName.StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase) &&
                    detail.SectionName.Equals("WINUSB", StringComparison.OrdinalIgnoreCase) &&
                    detail.HardwareId.Equals(@"USB\MS_COMP_WINUSB", StringComparison.OrdinalIgnoreCase))
                {
                    selected = drv;
                    break;
                }
            }

            if (selected is not { } winUsbDrv)
                throw new InvalidOperationException("Could not locate standard Microsoft WinUSB driver entry in winusb.inf.");

            Check(SetupDiSetSelectedDriverW(set, IntPtr.Zero, ref winUsbDrv), "SetupDiSetSelectedDriver(global)");
            Check(SetupDiOpenDeviceInfoW(set, instanceId, IntPtr.Zero, DiodInheritClassDrivers, ref dev), "SetupDiOpenDeviceInfo");
            opened = true;

            var devInstall = new SpDevinstallParams { cbSize = Marshal.SizeOf<SpDevinstallParams>(), DriverPath = "" };
            Check(SetupDiGetDeviceInstallParamsForDeviceW(set, ref dev, ref devInstall), "SetupDiGetDeviceInstallParams(dev)");
            devInstall.Flags |= DiQuietInstall;
            Check(SetupDiSetDeviceInstallParamsForDeviceW(set, ref dev, ref devInstall), "SetupDiSetDeviceInstallParams(dev)");

            var inherited = NewDrvInfo();
            Check(SetupDiGetSelectedDriverForDeviceW(set, ref dev, ref inherited), "SetupDiGetSelectedDriver(dev)");
            Check(SetupDiSetSelectedDriverForDeviceW(set, ref dev, ref inherited), "SetupDiSetSelectedDriver(dev)");
            Check(DiInstallDevice(IntPtr.Zero, set, ref dev, ref inherited, 0, out bool needReboot), "DiInstallDevice(WinUSB)");
            return needReboot;
        }
        finally
        {
            if (opened) _ = SetupDiDestroyDriverInfoListForDevice(set, ref dev, SpditClassDriver);
            _ = SetupDiDestroyDriverInfoList(set, IntPtr.Zero, SpditClassDriver);
            _ = SetupDiDestroyDeviceInfoList(set);
        }
    }

    private static bool InstallPublishedInf(string instanceId, string publishedInfPath)
    {
        IntPtr set = SetupDiCreateDeviceInfoList(IntPtr.Zero, IntPtr.Zero);
        if (set == InvalidHandle)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "SetupDiCreateDeviceInfoList(restore) failed.");

        var dev = new SpDevinfoData { cbSize = Marshal.SizeOf<SpDevinfoData>() };
        bool listBuilt = false;
        try
        {
            Check(SetupDiOpenDeviceInfoW(set, instanceId, IntPtr.Zero, 0, ref dev), "SetupDiOpenDeviceInfo(restore)");
            Check(SetupDiSetSelectedDevice(set, ref dev), "SetupDiSetSelectedDevice(restore)");

            var install = new SpDevinstallParams { cbSize = Marshal.SizeOf<SpDevinstallParams>(), DriverPath = "" };
            Check(SetupDiGetDeviceInstallParamsForDeviceW(set, ref dev, ref install), "SetupDiGetDeviceInstallParams(restore)");
            install.Flags |= DiEnumSingleInf | DiQuietInstall;
            install.DriverPath = publishedInfPath;
            Check(SetupDiSetDeviceInstallParamsForDeviceW(set, ref dev, ref install), "SetupDiSetDeviceInstallParams(restore)");
            Check(SetupDiBuildDriverInfoListForDevice(set, ref dev, SpditCompatDriver), "SetupDiBuildDriverInfoList(restore)");
            listBuilt = true;

            var drv = NewDrvInfo();
            Check(SetupDiEnumDriverInfoForDeviceW(set, ref dev, SpditCompatDriver, 0, ref drv), "SetupDiEnumDriverInfo(restore)");
            Check(SetupDiSetSelectedDriverForDeviceW(set, ref dev, ref drv), "SetupDiSetSelectedDriver(restore)");
            Check(DiInstallDevice(IntPtr.Zero, set, ref dev, ref drv, 0, out bool needReboot), "DiInstallDevice(restore)");
            return needReboot;
        }
        finally
        {
            if (listBuilt) _ = SetupDiDestroyDriverInfoListForDevice(set, ref dev, SpditCompatDriver);
            _ = SetupDiDestroyDeviceInfoList(set);
        }
    }

    private static void UpdateInterfaceGuid(string instanceId, bool add)
    {
        IntPtr set = SetupDiCreateDeviceInfoList(IntPtr.Zero, IntPtr.Zero);
        if (set == InvalidHandle) throw new Win32Exception(Marshal.GetLastWin32Error(), "SetupDiCreateDeviceInfoList(reg) failed.");
        var dev = new SpDevinfoData { cbSize = Marshal.SizeOf<SpDevinfoData>() };
        try
        {
            Check(SetupDiOpenDeviceInfoW(set, instanceId, IntPtr.Zero, 0, ref dev), "SetupDiOpenDeviceInfo(reg)");
            IntPtr rawKey = SetupDiOpenDevRegKey(set, ref dev, DicsFlagGlobal, 0, DiregDev, KeyReadWrite);
            if (rawKey == InvalidHandle)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "SetupDiOpenDevRegKey failed.");

            using var safe = new SafeRegistryHandle(rawKey, ownsHandle: true);
            using var key = RegistryKey.FromHandle(safe);
            string[] existing = ReadGuids(key);
            string[] others = existing.Where(x => !x.Equals(TargetGuidBrace, StringComparison.OrdinalIgnoreCase)).ToArray();
            string[] next = add ? others.Append(TargetGuidBrace).ToArray() : others;

            if (next.Length == 0)
                key.DeleteValue("DeviceInterfaceGUIDs", throwOnMissingValue: false);
            else
                key.SetValue("DeviceInterfaceGUIDs", next, RegistryValueKind.MultiString);

            key.Flush();
        }
        finally
        {
            _ = SetupDiDestroyDeviceInfoList(set);
        }
    }

    private static void RestartAndRescan(string instanceId)
    {
        string pnputil = Path.Combine(Environment.SystemDirectory, "pnputil.exe");
        RunPnp(pnputil, new[] { "/restart-device", instanceId });
        RunPnp(pnputil, new[] { "/scan-devices" });
    }

    private static void RunPnp(string exe, string[] args)
    {
        var psi = new ProcessStartInfo(exe)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (string a in args) psi.ArgumentList.Add(a);
        using var proc = Process.Start(psi);
        proc?.WaitForExit(15000);
    }

    private static string[] ReadGuids(RegistryKey key)
    {
        object? val = key.GetValue("DeviceInterfaceGUIDs", null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        return val as string[] ?? Array.Empty<string>();
    }

    private static bool HasCustomGuid(IntPtr set, ref SpDevinfoData dev)
    {
        IntPtr rawKey = SetupDiOpenDevRegKey(set, ref dev, DicsFlagGlobal, 0, DiregDev, KeyRead);
        if (rawKey == InvalidHandle) return false;
        using var safe = new SafeRegistryHandle(rawKey, ownsHandle: true);
        using var key = RegistryKey.FromHandle(safe);
        return ReadGuids(key).Any(x => x.Equals(TargetGuidBrace, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetDeviceInstanceId(IntPtr set, ref SpDevinfoData dev)
    {
        var sb = new StringBuilder(512);
        return SetupDiGetDeviceInstanceIdW(set, ref dev, sb, sb.Capacity, out _) ? sb.ToString() : "";
    }

    private static string GetStringProperty(IntPtr set, ref SpDevinfoData dev, uint property)
    {
        byte[] buf = new byte[512];
        if (!SetupDiGetDeviceRegistryPropertyW(set, ref dev, property, out _, buf, (uint)buf.Length, out _))
            return "";
        return Encoding.Unicode.GetString(buf).TrimEnd('\0');
    }

    private static string[] GetMultiStringProperty(IntPtr set, ref SpDevinfoData dev, uint property)
    {
        byte[] buf = new byte[2048];
        if (!SetupDiGetDeviceRegistryPropertyW(set, ref dev, property, out _, buf, (uint)buf.Length, out _))
            return Array.Empty<string>();
        return Encoding.Unicode.GetString(buf).Split('\0', StringSplitOptions.RemoveEmptyEntries);
    }

    private static string GetInfPathProperty(IntPtr set, ref SpDevinfoData dev)
    {
        var key = DevKeyDriverInfPath;
        byte[] buf = new byte[512];
        if (!SetupDiGetDevicePropertyW(set, ref dev, ref key, out _, buf, (uint)buf.Length, out _, 0))
            return "";
        return Encoding.Unicode.GetString(buf).TrimEnd('\0');
    }

    private static int GetProblemCode(uint devInst)
    {
        int cr = CM_Get_DevNode_Status(out _, out uint prob, devInst, 0);
        return cr == 0 ? (int)prob : -1;
    }

    private static (string SectionName, string HardwareId) GetDriverDetail(IntPtr set, IntPtr dev, ref SpDrvinfoData drv)
    {
        _ = SetupDiGetDriverInfoDetailW(set, dev, ref drv, IntPtr.Zero, 0, out uint req);
        if (req == 0) return ("", "");
        IntPtr mem = Marshal.AllocHGlobal((int)req);
        try
        {
            for (int i = 0; i < (int)req; i++) Marshal.WriteByte(mem, i, 0);
            Marshal.WriteInt32(mem, Marshal.SizeOf<SpDrvinfoDetailData>());
            if (!SetupDiGetDriverInfoDetailW(set, dev, ref drv, mem, req, out _)) return ("", "");
            var detail = Marshal.PtrToStructure<SpDrvinfoDetailData>(mem);
            int hwOffset = Marshal.OffsetOf<SpDrvinfoDetailData>(nameof(SpDrvinfoDetailData.HardwareID)).ToInt32();
            string hw = Marshal.PtrToStringUni(IntPtr.Add(mem, hwOffset)) ?? "";
            return (detail.SectionName ?? "", hw);
        }
        finally
        {
            Marshal.FreeHGlobal(mem);
        }
    }

    private static SpDrvinfoData NewDrvInfo() => new()
    {
        cbSize = Marshal.SizeOf<SpDrvinfoData>(),
        Description = "",
        MfgName = "",
        ProviderName = ""
    };

    private static void Check(bool ok, string op)
    {
        if (!ok) throw new Win32Exception(Marshal.GetLastWin32Error(), op + " failed.");
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DevPropKey { public Guid FmtId; public uint Pid; }

    [StructLayout(LayoutKind.Sequential)]
    private struct SpDevinfoData
    {
        public int cbSize;
        public Guid ClassGuid;
        public uint DevInst;
        public IntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SpDevinstallParams
    {
        public int cbSize;
        public uint Flags;
        public uint FlagsEx;
        public IntPtr hwndParent;
        public IntPtr InstallMsgHandler;
        public IntPtr InstallMsgHandlerContext;
        public IntPtr FileQueue;
        public IntPtr ClassInstallReserved;
        public uint Reserved;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string DriverPath;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SpDrvinfoData
    {
        public int cbSize;
        public uint DriverType;
        public IntPtr Reserved;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Description;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string MfgName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string ProviderName;
        public FILETIME DriverDate;
        public ulong DriverVersion;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SpDrvinfoDetailData
    {
        public int cbSize;
        public FILETIME InfDate;
        public uint CompatIDsOffset;
        public uint CompatIDsLength;
        public IntPtr Reserved;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string SectionName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string InfFileName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string DrvDescription;
        public char HardwareID;
    }

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SetupDiGetClassDevsW(IntPtr classGuid, string? enumerator, IntPtr hwndParent, uint flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiEnumDeviceInfo(IntPtr set, uint memberIndex, ref SpDevinfoData devInfo);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiGetDeviceInstanceIdW(IntPtr set, ref SpDevinfoData devInfo, StringBuilder id, int size, out int requiredSize);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiGetDeviceRegistryPropertyW(IntPtr set, ref SpDevinfoData devInfo, uint property, out uint regDataType, byte[] propertyBuffer, uint bufferSize, out uint requiredSize);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiGetDevicePropertyW(IntPtr set, ref SpDevinfoData devInfo, ref DevPropKey propKey, out uint propType, byte[] buffer, uint bufferSize, out uint requiredSize, uint flags);

    [DllImport("cfgmgr32.dll")]
    private static extern int CM_Get_DevNode_Status(out uint status, out uint problemNumber, uint devInst, uint flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern IntPtr SetupDiCreateDeviceInfoList(IntPtr classGuid, IntPtr hwndParent);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiOpenDeviceInfoW(IntPtr set, string instanceId, IntPtr hwndParent, uint openFlags, ref SpDevinfoData devInfo);

    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiSetSelectedDevice(IntPtr set, ref SpDevinfoData devInfo);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern IntPtr SetupDiOpenDevRegKey(IntPtr set, ref SpDevinfoData devInfo, uint scope, uint hwProfile, uint keyType, int samDesired);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiGetDeviceInstallParamsW(IntPtr set, IntPtr devInfo, ref SpDevinstallParams install);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "SetupDiGetDeviceInstallParamsW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiGetDeviceInstallParamsForDeviceW(IntPtr set, ref SpDevinfoData devInfo, ref SpDevinstallParams install);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiSetDeviceInstallParamsW(IntPtr set, IntPtr devInfo, ref SpDevinstallParams install);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "SetupDiSetDeviceInstallParamsW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiSetDeviceInstallParamsForDeviceW(IntPtr set, ref SpDevinfoData devInfo, ref SpDevinstallParams install);

    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiBuildDriverInfoList(IntPtr set, IntPtr devInfo, uint driverType);

    [DllImport("setupapi.dll", SetLastError = true, EntryPoint = "SetupDiBuildDriverInfoList")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiBuildDriverInfoListForDevice(IntPtr set, ref SpDevinfoData devInfo, uint driverType);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiEnumDriverInfoW(IntPtr set, IntPtr devInfo, uint driverType, uint index, ref SpDrvinfoData driver);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "SetupDiEnumDriverInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiEnumDriverInfoForDeviceW(IntPtr set, ref SpDevinfoData devInfo, uint driverType, uint index, ref SpDrvinfoData driver);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiGetDriverInfoDetailW(IntPtr set, IntPtr devInfo, ref SpDrvinfoData driver, IntPtr buf, uint bufSize, out uint req);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiSetSelectedDriverW(IntPtr set, IntPtr devInfo, ref SpDrvinfoData driver);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "SetupDiSetSelectedDriverW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiSetSelectedDriverForDeviceW(IntPtr set, ref SpDevinfoData devInfo, ref SpDrvinfoData driver);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "SetupDiGetSelectedDriverW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiGetSelectedDriverForDeviceW(IntPtr set, ref SpDevinfoData devInfo, ref SpDrvinfoData driver);

    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiDestroyDriverInfoList(IntPtr set, IntPtr devInfo, uint driverType);

    [DllImport("setupapi.dll", SetLastError = true, EntryPoint = "SetupDiDestroyDriverInfoList")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiDestroyDriverInfoListForDevice(IntPtr set, ref SpDevinfoData devInfo, uint driverType);

    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);

    [DllImport("newdev.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DiInstallDevice(IntPtr hwndParent, IntPtr set, ref SpDevinfoData devInfo, ref SpDrvinfoData driver, uint flags, [MarshalAs(UnmanagedType.Bool)] out bool needReboot);
}
