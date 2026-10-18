using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using X20Ctl.Zone;
namespace X20Ctl.Product.Setup;

internal enum SetupMode { None, Install, Uninstall, UninstallRun }

internal sealed record SetupOptions(bool StartMenu = true, bool Desktop = false, bool KeepUpToDate = true, bool Launch = true);

/// <summary>
/// The setup and the uninstaller are this same exe. A copy named *-setup.exe (or started with --setup) installs itself for the
/// current user: X20Ctl.exe in %LOCALAPPDATA%\Programs\X20CTL, a Start menu entry, an optional desktop shortcut and an entry in
/// Windows Apps that runs "X20Ctl.exe --uninstall". Nothing outside the player's own account is touched, so the in-app updater
/// (which swaps the exe beside itself) keeps working without another permission prompt.
/// X20CTL_SETUP_SANDBOX=DIR moves every path and the registry key into DIR for tests; X20CTL_SETUP_PAYLOAD=FILE names the file to
/// install when the running build is a folder build, not the one-file exe.
/// </summary>
internal static class Installer
{
    public const string Name = "X20CTL";
    static string? Sandbox => Environment.GetEnvironmentVariable("X20CTL_SETUP_SANDBOX") is { Length: > 0 } s ? Path.GetFullPath(s) : null;
    public static bool InSandbox => Sandbox != null;
    public static string InstallDir => Sandbox is { } s ? Path.Combine(s, "app") : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", Name);
    public static string ExePath => Path.Combine(InstallDir, "X20Ctl.exe");
    static string StartMenuLink => Path.Combine(Sandbox is { } s ? Path.Combine(s, "start") : Environment.GetFolderPath(Environment.SpecialFolder.Programs), Name + ".lnk");
    static string DesktopLink => Path.Combine(Sandbox is { } s ? Path.Combine(s, "desktop") : Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Name + ".lnk");
    static string RegistryKey => Sandbox != null ? @"Software\X20CTL-SetupTest" : @"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + Name;
    static string DataRoot => Sandbox is { } s ? Path.Combine(s, "appdata") : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "x20ctl");
    static string LocalDataRoot => Sandbox is { } s ? Path.Combine(s, "localdata") : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "x20ctl");
    public static string Version => X20Ctl.Zone.ReleaseVersions.Display(Scanner.ScanFlow.AppVersion);
    public static bool Installed => File.Exists(ExePath);

    public static SetupMode Detect(string[] args, string? processPath)
    {
        if (args.Length >= 3 && args[0] == "--uninstall-run") return SetupMode.UninstallRun;
        if (args.Contains("--uninstall")) return SetupMode.Uninstall;
        if (args.Contains("--setup")) return SetupMode.Install;
        if (processPath != null && Path.GetFileNameWithoutExtension(processPath).EndsWith("-setup", StringComparison.OrdinalIgnoreCase)) return SetupMode.Install;
        return SetupMode.None;
    }

    /// <summary>True for the release's one-file exe, false for a folder build (the dev builds, the ZIP). A one-file exe that extracts its
    /// content at start still reports an assembly path, so the test is whether X20Ctl.dll sits beside the exe, as it does in a folder build.</summary>
    public static bool IsOneFileExe => Environment.ProcessPath is { Length: > 0 } p && Path.GetDirectoryName(p) is { } dir && !File.Exists(Path.Combine(dir, "X20Ctl.dll"));

    /// <summary>The file that gets installed: the running one-file exe, or the test payload.</summary>
    public static string? Payload()
    {
        if (Environment.GetEnvironmentVariable("X20CTL_SETUP_PAYLOAD") is { Length: > 0 } p && Sandbox != null) return File.Exists(p) ? p : null;
        return IsOneFileExe ? Environment.ProcessPath : null;
    }

    public static string NoPayloadMessage => "This copy of X20CTL is a development build, not the one-file release, so it can't install itself. Download the setup from the releases page.";

    public static void Install(SetupOptions options, IProgress<(double Fraction, string Step)> progress)
    {
        string source = Payload() ?? throw new InvalidOperationException(NoPayloadMessage);
        progress.Report((.04, "Making room"));
        Directory.CreateDirectory(InstallDir);
        // a running copy (an update over a working install) can be renamed but not overwritten
        string old = ExePath + ".old";
        try { if (File.Exists(old)) File.Delete(old); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        if (File.Exists(ExePath)) File.Move(ExePath, old, true);
        progress.Report((.08, "Copying X20CTL"));
        try
        {
            using var from = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var to = new FileStream(ExePath, FileMode.Create, FileAccess.Write, FileShare.None);
            var buffer = new byte[1 << 20]; long done = 0, total = Math.Max(1, from.Length); int n;
            while ((n = from.Read(buffer, 0, buffer.Length)) > 0) { to.Write(buffer, 0, n); done += n; progress.Report((.08 + .74 * done / total, "Copying X20CTL")); }
        }
        catch { try { if (File.Exists(ExePath)) File.Delete(ExePath); if (File.Exists(old)) File.Move(old, ExePath); } catch (IOException) { } throw; }
        try { File.Delete(old); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        progress.Report((.84, "Adding shortcuts"));
        if (options.StartMenu) Shortcut(StartMenuLink); else TryDelete(StartMenuLink);
        if (options.Desktop) Shortcut(DesktopLink); else TryDelete(DesktopLink);
        progress.Report((.92, "Registering with Windows"));
        Register();
        progress.Report((.96, "Saving your choices"));
        if (!options.KeepUpToDate) SaveAutoUpdate(false);
        progress.Report((1, "Done"));
    }

    static void SaveAutoUpdate(bool on)
    {
        try { var prefs = new ProductPreferences(Path.Combine(DataRoot, "native", "preferences.v1.json")); prefs.Load(); prefs.SetAutoUpdate(on); }
        catch (Exception e) when (e is IOException or System.Text.Json.JsonException or InvalidOperationException or UnauthorizedAccessException) { }
    }

    static void Shortcut(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var shell = Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("Windows can't make shortcuts here.");
        dynamic sh = Activator.CreateInstance(shell)!; dynamic link = sh.CreateShortcut(path);
        try { link.TargetPath = ExePath; link.WorkingDirectory = InstallDir; link.IconLocation = ExePath + ",0"; link.Description = "X20CTL"; link.Save(); }
        finally { Marshal.FinalReleaseComObject(link); Marshal.FinalReleaseComObject(sh); }
    }

    /// <summary>Writes the Windows Apps entry. Also called at every launch of the installed copy, so the version stays right after the updater swaps the exe.</summary>
    public static void Register()
    {
        using var key = Registry.CurrentUser.CreateSubKey(RegistryKey);
        long kb = File.Exists(ExePath) ? new FileInfo(ExePath).Length / 1024 : 0;
        key.SetValue("DisplayName", Name); key.SetValue("DisplayVersion", Version); key.SetValue("Publisher", Name);
        key.SetValue("DisplayIcon", ExePath); key.SetValue("InstallLocation", InstallDir); key.SetValue("URLInfoAbout", ExternalLinks.Repository);
        key.SetValue("UninstallString", $"\"{ExePath}\" --uninstall"); key.SetValue("QuietUninstallString", $"\"{ExePath}\" --uninstall --silent");
        key.SetValue("NoModify", 1, RegistryValueKind.DWord); key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        key.SetValue("EstimatedSize", (int)Math.Min(int.MaxValue, kb), RegistryValueKind.DWord); key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
    }

    /// <summary>At every normal launch from the installed folder: keep the Apps entry's version current.</summary>
    public static void RefreshIfInstalled()
    {
        try { if (Environment.ProcessPath is { } p && string.Equals(Path.GetFullPath(p), Path.GetFullPath(ExePath), StringComparison.OrdinalIgnoreCase)) Register(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
    }

    /// <summary>First step of removing: the running exe can't delete itself, so a copy in the temp folder does the work.</summary>
    public static bool HandOffUninstall(bool silent, bool purge)
    {
        string self = Environment.ProcessPath ?? "";
        if (self.Length == 0 || !File.Exists(self)) return false;
        string temp = Path.Combine(Path.GetTempPath(), "x20ctl-uninstall-" + Guid.NewGuid().ToString("N") + ".exe");
        File.Copy(self, temp, true);
        var info = new ProcessStartInfo(temp) { UseShellExecute = false, WorkingDirectory = Path.GetTempPath() };
        foreach (string a in new[] { "--uninstall-run", InstallDir, Environment.ProcessId.ToString(), silent ? "--silent" : "--ask", purge ? "--purge" : "--keep" }) info.ArgumentList.Add(a);
        Process.Start(info); return true;
    }

    /// <summary>The copies of X20CTL running from the install folder.</summary>
    public static List<Process> Running()
    {
        var found = new List<Process>(); string mine = Path.GetFullPath(ExePath);
        foreach (var p in Process.GetProcessesByName("X20Ctl"))
        {
            try { if (p.Id != Environment.ProcessId && string.Equals(Path.GetFullPath(p.MainModule?.FileName ?? ""), mine, StringComparison.OrdinalIgnoreCase)) { found.Add(p); continue; } } catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException) { }
            p.Dispose();
        }
        return found;
    }

    public static void Uninstall(bool purge, int parentPid, IProgress<(double Fraction, string Step)> progress)
    {
        progress.Report((.05, "Waiting for X20CTL to close"));
        if (parentPid > 0) try { using var parent = Process.GetProcessById(parentPid); parent.WaitForExit(8000); } catch (ArgumentException) { }
        foreach (var p in Running())
        {
            try { p.CloseMainWindow(); if (!p.WaitForExit(4000)) { p.Kill(); p.WaitForExit(3000); } } catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception) { } finally { p.Dispose(); }
        }
        progress.Report((.3, "Removing shortcuts"));
        TryDelete(StartMenuLink); TryDelete(DesktopLink);
        progress.Report((.5, "Removing X20CTL"));
        // only a folder that really holds X20CTL, and only the one setup made
        if (File.Exists(ExePath)) for (int i = 0; i < 8; i++) { try { Directory.Delete(InstallDir, true); break; } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Thread.Sleep(400); } }
        if (purge) { progress.Report((.75, "Removing your settings")); foreach (string dir in new[] { DataRoot, LocalDataRoot }) try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { } }
        progress.Report((.9, "Removing the entry in Windows Apps"));
        try { Registry.CurrentUser.DeleteSubKeyTree(RegistryKey, false); } catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException) { }
        progress.Report((1, "Done"));
    }

    /// <summary>Last step of the temp copy: delete itself once it has exited.</summary>
    public static void RemoveSelfLater()
    {
        if (Environment.ProcessPath is not { } self || !Path.GetFileName(self).StartsWith("x20ctl-uninstall-", StringComparison.Ordinal)) return;
        Process.Start(new ProcessStartInfo("cmd.exe", $"/c ping 127.0.0.1 -n 3 >nul & del /f /q \"{self}\"") { CreateNoWindow = true, UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden });
    }

    static void TryDelete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { } }

    public static bool HasShortcut(bool desktop) => File.Exists(desktop ? DesktopLink : StartMenuLink);
    public static bool HasRegistration() { using var k = Registry.CurrentUser.OpenSubKey(RegistryKey); return k != null; }
    public static string? RegisteredValue(string name) { using var k = Registry.CurrentUser.OpenSubKey(RegistryKey); return k?.GetValue(name)?.ToString(); }
    public static string SettingsRoot => DataRoot;
}
