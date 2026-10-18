using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using X20Ctl.Product.Setup;
namespace X20Ctl.Product.Development;

/// <summary>--review-setup DIRECTORY: renders every page of the setup and the uninstaller (visuals only, no screen capture) and runs the real
/// install, update and removal against a sandbox inside DIRECTORY, writing what it found to results.txt. Nothing outside DIRECTORY and
/// one throwaway registry key is touched.</summary>
internal static class SetupReview
{
    public static async Task Run(string directory)
    {
        Directory.CreateDirectory(directory);
        string sandbox = Path.Combine(Path.GetFullPath(directory), "sandbox"); if (Directory.Exists(sandbox)) Directory.Delete(sandbox, true);
        Environment.SetEnvironmentVariable("X20CTL_SETUP_SANDBOX", sandbox);
        string payload = Path.Combine(Path.GetFullPath(directory), "payload.exe"); File.WriteAllBytes(payload, new byte[3_500_000]); Environment.SetEnvironmentVariable("X20CTL_SETUP_PAYLOAD", payload);
        var log = new List<string>();
        void Check(string what, bool ok) => log.Add((ok ? "PASS " : "FAIL ") + what);

        // ---- pages
        async Task Shots(SetupMode mode, string prefix, params (string Name, Action<SetupWindow> Show)[] pages)
        {
            var window = new SetupWindow(mode); window.Show(); await Task.Delay(300);
            foreach (var (name, show) in pages)
            {
                show(window); await Task.Delay(500); window.UpdateLayout();
                var bmp = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32); bmp.Render((Visual)window.Content);
                var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(bmp)); using var s = File.Create(Path.Combine(directory, prefix + name + ".png")); enc.Save(s);
            }
            window.Close();
        }
        await Shots(SetupMode.Install, "setup-",
            ("1-welcome", w => w.Preview(0)), ("2-license", w => w.Preview(1)), ("2-license-accepted", w => w.Preview(1, accept: true)), ("2-notice", w => w.Preview(1, accept: true, tab: "Notice")),
            ("3-options", w => w.Preview(2)), ("4-installing", w => w.Preview(3, .62)), ("5-finish", w => w.Preview(4)), ("5-failed", w => w.Preview(4, fail: "Access to the path is denied.")));
        await Shots(SetupMode.UninstallRun, "remove-", ("1-confirm", w => w.Preview(0)), ("2-removing", w => w.Preview(1, .55)), ("3-done", w => w.Preview(1, 1)));

        // ---- the mode switch
        Check("a *-setup.exe name installs", Installer.Detect([], @"C:\x\X20CTL-4.1.0-windows-x64-setup.exe") == SetupMode.Install);
        Check("the release exe name does not", Installer.Detect([], @"C:\x\X20CTL-4.1.0-windows-x64.exe") == SetupMode.None);
        Check("--setup installs", Installer.Detect(["--setup"], @"C:\x\X20Ctl.exe") == SetupMode.Install);
        Check("--uninstall removes", Installer.Detect(["--uninstall"], @"C:\x\X20Ctl.exe") == SetupMode.Uninstall);
        Check("--uninstall-run needs its arguments", Installer.Detect(["--uninstall-run", "d", "1", "--ask", "--keep"], @"C:\t\x20ctl-uninstall-1.exe") == SetupMode.UninstallRun && Installer.Detect(["--uninstall-run"], @"C:\t\a.exe") == SetupMode.None);
        Check("--reduced-motion alone is the normal app", Installer.Detect(["--reduced-motion"], @"C:\x\X20Ctl.exe") == SetupMode.None);

        // ---- a real install into the sandbox
        var steps = new List<string>();
        Installer.Install(new SetupOptions(true, true, false, false), new SyncProgress(p => steps.Add($"{p.Fraction:0.00} {p.Step}")));
        Check("exe copied byte for byte", File.Exists(Installer.ExePath) && new FileInfo(Installer.ExePath).Length == 3_500_000);
        Check("Start menu shortcut", Installer.HasShortcut(false)); Check("desktop shortcut when asked", Installer.HasShortcut(true));
        Check("Windows Apps entry", Installer.HasRegistration());
        Check("uninstall command points at the installed exe", Installer.RegisteredValue("UninstallString") == $"\"{Installer.ExePath}\" --uninstall");
        Check("display version recorded", Installer.RegisteredValue("DisplayVersion") == Installer.Version);
        string prefs = Path.Combine(Installer.SettingsRoot, "native", "preferences.v1.json");
        Check("'keep up to date' off is saved", File.Exists(prefs) && File.ReadAllText(prefs).Contains("\"autoUpdate\":false"));
        Check("progress ends at 100%", steps.Count > 3 && steps[^1].StartsWith("1.00"));
        Check("progress never goes backwards", steps.Zip(steps.Skip(1), (a, b) => string.CompareOrdinal(a[..4], b[..4]) <= 0).All(x => x));

        // ---- an update over the installed copy (the old exe may be running, so it is renamed first)
        File.WriteAllBytes(payload, new byte[3_600_000]);
        Installer.Install(new SetupOptions(false, false, true, false), new SyncProgress(_ => { }));
        Check("update replaces the exe", new FileInfo(Installer.ExePath).Length == 3_600_000 && !File.Exists(Installer.ExePath + ".old"));
        Check("update can drop the Start menu shortcut and the desktop one", !Installer.HasShortcut(false) && !Installer.HasShortcut(true));

        // ---- removal that keeps settings, then one that deletes them
        Installer.Install(new SetupOptions(true, false, true, false), new SyncProgress(_ => { }));
        Installer.Uninstall(false, 0, new SyncProgress(_ => { }));
        Check("removal deletes the install folder", !Directory.Exists(Installer.InstallDir)); Check("removal deletes the shortcut", !Installer.HasShortcut(false)); Check("removal deletes the Apps entry", !Installer.HasRegistration());
        Check("removal keeps settings by default", File.Exists(prefs));
        Installer.Install(new SetupOptions(true, false, true, false), new SyncProgress(_ => { }));
        Installer.Uninstall(true, 0, new SyncProgress(_ => { }));
        Check("removal with 'delete my settings' deletes them", !File.Exists(prefs) && !Directory.Exists(Installer.SettingsRoot));
        Check("a folder that is not X20CTL's is never deleted", SafetyHolds(sandbox));

        File.WriteAllLines(Path.Combine(directory, "results.txt"), log.Concat(["", .. steps.Take(0)]));
        try { Directory.Delete(sandbox, true); File.Delete(payload); } catch (IOException) { }
    }

    /// <summary>Removal only deletes the install folder when X20Ctl.exe is really inside it.</summary>
    static bool SafetyHolds(string sandbox)
    {
        string dir = Installer.InstallDir; Directory.CreateDirectory(dir); string stranger = Path.Combine(dir, "someone-elses.txt"); File.WriteAllText(stranger, "keep");
        Installer.Uninstall(false, 0, new SyncProgress(_ => { }));
        bool kept = File.Exists(stranger); Directory.Delete(dir, true); return kept;
    }

    sealed class SyncProgress(Action<(double Fraction, string Step)> report) : IProgress<(double Fraction, string Step)> { public void Report((double Fraction, string Step) value) => report(value); }
}
