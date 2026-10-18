using System.IO;
using System.Windows;
namespace X20Ctl.Product;

public partial class App : Application
{
    private async void OnStartup(object sender, StartupEventArgs e)
    {
        try
        {
            if (e.Args.Length == 3 && e.Args[0] == "--review-scanner-replay")
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                await Development.ScannerReplay.Run(e.Args[1], e.Args[2]); Shutdown(0); return;
            }
            if (e.Args.Length == 2 && e.Args[0] == "--review-update")
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                var lines = new List<string> { "current " + Updater.Current + " canInstall " + Updater.CanInstall };
                foreach (var (a, b) in new[] { ("4.1.0-preview.5", "4.1.0-preview.4"), ("4.1.0", "4.1.0-preview.9"), ("4.1.0-preview.10", "4.1.0-preview.9"), ("4.0.1", "4.1.0-preview.1"), ("4.1.0-preview.3", "4.1.0-preview.3"), ("4.1.0-preview.7", "4.1.0-preview.6"), ("0.017", "4.1.0-preview.7"), ("0.017.1", "0.017"), ("1.000", "0.999") })
                    lines.Add($"{a} vs {b}: {Updater.Compare(a, b)}");
                try { var r = await Updater.Latest(); lines.Add("latest newer: " + (r?.ToString() ?? "none")); } catch (Exception failure) { lines.Add("latest failed: " + failure.Message); }
                File.WriteAllLines(e.Args[1], lines); Shutdown(0); return;
            }
            if (e.Args.Length == 2 && e.Args[0] == "--review-pcmode")
            {   // the PC mode service's state walk on a machine with no drivers (and maybe no pad): nothing is installed or hidden
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                var log = new List<string>();
                string file = Path.Combine(Path.GetTempPath(), "x20ctl-review-pcmode-" + Guid.NewGuid().ToString("N") + ".json");
                try {
                var store = new X20Ctl.Zone.PcMode.PcModeStore(file); store.Load();
                using var service = new PcModeService(store);
                void Note(string step) => log.Add($"{step}: {service.State} | {service.Detail}");
                log.Add("bus installed " + PcModeSetup.BusInstalled + ", hider installed " + HidHide.Installed);
                // HidHideCLI --dev-gaming's exact shape (HidHideCLI/src/Commands.cpp): one present Xbox-style pad, one unplugged
                string sample = " [ { \"friendlyName\" : \"X05\" , \"devices\" : [\n{ \"present\" : true ,\n\"gamingDevice\" : true ,\n\"deviceInstancePath\" : \"HID\\\\VID_045E&PID_028E&IG_00\\\\7&1\" ,\n\"xusbDeviceInstancePath\" : \"USB\\\\VID_045E&PID_028E\\\\ABC\" ,\n\"baseContainerDeviceCount\" : 2 } ] } ,\n{ \"friendlyName\" : \"Old\" , \"devices\" : [\n{ \"present\" : false ,\n\"deviceInstancePath\" : \"HID\\OLD\" ,\n\"xusbDeviceInstancePath\" : \"\" ,\n\"baseContainerDeviceCount\" : 1 } ] } ] ";
                log.Add("parsed: " + string.Join(" | ", HidHide.ParseGamingDevices(sample)));
                service.Enabled = true; await Task.Delay(600); Note("enabled, neutral profile");
                store.Save("default", new X20Ctl.Zone.PcMode.PcModeProfile { Remap = { ["A"] = "B" } }); service.Reload(); await Task.Delay(800); Note("A to B saved");
                service.Enabled = false; await Task.Delay(500); Note("switched off");
                } catch (Exception failure) { log.Add("FAILED: " + failure); }
                File.WriteAllLines(e.Args[1], log); try { File.Delete(file); } catch (IOException) { }
                Shutdown(0); return;
            }
            if (e.Args.Length == 2 && e.Args[0] == "--review-timing")
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown; IntroView.SoundEnabled = false;
                await Development.TimingReview.Run(e.Args[1]); Shutdown(0); return;
            }
            if (e.Args.Length == 2 && e.Args[0] == "--review-battery")
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                try { await Development.BatteryReview.Run(e.Args[1]); } catch (Exception failure) { File.WriteAllText(e.Args[1] + ".failure.txt", failure.ToString()); }
                Shutdown(0); return;
            }
            if (e.Args.Length == 2 && e.Args[0] is "--review-zone" or "--review-studio" or "--review-console" or "--review-management" or "--review-product" or "--review-models" or "--review-live" or "--review-design" or "--review-showcase" or "--review-dashboard" or "--review-input" or "--review-intro" or "--review-zone-motion" or "--review-windowed" or "--review-pages" or "--review-stress" or "--review-probe" or "--review-readme" or "--review-talk" or "--agent-bakeoff" or "--agent-drift" or "--agent-m3" or "--agent-phrasebook" or "--agent-m5" or "--agent-phrasebook-clean" or "--review-scanflow" or "--review-agent-look")
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown; IntroView.SoundEnabled = false;
                if(e.Args[0]=="--review-readme")await Development.ReadmeReview.Run(e.Args[1]); else if(e.Args[0]=="--review-scanflow")await Development.ScanFlowReview.Run(e.Args[1]); else if(e.Args[0]=="--review-agent-look")await Development.AgentLookReview.Run(e.Args[1]);
                else if(e.Args[0]=="--review-talk")await Development.TalkReview.Run(e.Args[1]);
            else if(e.Args[0]=="--review-models")await Development.ModelAudit.Run(e.Args[1]);
                else if(e.Args[0]=="--review-live")await Development.LiveReview.Run(e.Args[1]);
                else if(e.Args[0]=="--review-design")await Development.DesignReview.Run(e.Args[1]);
                else if(e.Args[0]=="--review-showcase")await Development.ShowcaseReview.Run(e.Args[1]);
                else if(e.Args[0]=="--review-dashboard")await Development.DashboardReview.Run(e.Args[1]);
                else if(e.Args[0]=="--review-input")await Development.InputReview.Run(e.Args[1]);
                else if(e.Args[0]=="--review-intro")await Development.IntroReview.Run(e.Args[1]);
                else if(e.Args[0]=="--review-zone-motion")await Development.ZoneMotionReview.Run(e.Args[1]);
                else if(e.Args[0]=="--review-windowed")await Development.WindowedReview.Run(e.Args[1]);
                else if(e.Args[0]=="--review-pages")await Development.PagesReview.Run(e.Args[1]);
                else if(e.Args[0]=="--review-stress")await Development.StressReview.Run(e.Args[1]);
                else if(e.Args[0]=="--review-probe")await Development.StressProbe.Run(e.Args[1]);
                else if(e.Args[0]=="--review-product")await Development.ProductReview.Run(e.Args[1]);
                else if(e.Args[0]=="--review-management")await Development.ManagementReview.Run(e.Args[1]);
                else if(e.Args[0]=="--review-console")await Development.ConsoleReview.Run(e.Args[1]);
                else if (e.Args[0] == "--review-studio") await Development.StudioReview.Run(e.Args[1]);
                else await Development.ZoneReview.Run(e.Args[1]);
                Shutdown(0); return;
            }
            if (e.Args.Length == 2 && e.Args[0] == "--review-setup")
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                await Development.SetupReview.Run(e.Args[1]); Shutdown(0); return;
            }
            if (Setup.Installer.Detect(e.Args, Environment.ProcessPath) is var setup && setup != Setup.SetupMode.None)
            {   // the setup, the uninstaller and their silent forms are this same exe
                Setup.SetupApp.Run(this, setup, e.Args); return;
            }
            Setup.Installer.RefreshIfInstalled();
            if (e.Args.Length > 0 && !e.Args.SequenceEqual(new[] { "--reduced-motion" }))
                throw new ArgumentException("Use --reduced-motion, --setup, --uninstall, --review-zone DIRECTORY, or --review-studio DIRECTORY.");
            // a slip in the UI is logged and survived, never a closed app: the log names what happened for the next fix
            DispatcherUnhandledException += (_, failure) =>
            {
                try
                {
                    string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "x20ctl", "native"); Directory.CreateDirectory(folder);
                    File.AppendAllText(Path.Combine(folder, "crash.log"), $"==== {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n{failure.Exception}\n\n");
                }
                catch (IOException) { } catch (UnauthorizedAccessException) { }
                failure.Handled = true;
            };
            var window = new ProductWindow(e.Args.Contains("--reduced-motion"),firstRun:true,nativeEngine:true,fullscreen:false,trayIcon:true);
            MainWindow = window; window.Show();
            // HidHide's hide list outlives the app (it survives a reboot): every way out un-hides the controller, not only
            // the window closing — Windows signing out or shutting down, and a crash on any thread
            SessionEnding += (_, _) => { window.Host.MouseMode?.Dispose(); window.Host.PcMode?.Dispose(); };
            Exit += (_, _) => { window.Host.MouseMode?.Dispose(); window.Host.PcMode?.Dispose(); };
            ForegroundApp.Start();
            AppDomain.CurrentDomain.UnhandledException += (_, _) => { try { window.Host.MouseMode?.Dispose(); window.Host.PcMode?.Dispose(); } catch (Exception) { } };
        }
        catch (Exception error)
        {
            if (e.Args.Length == 2 && e.Args[0] is "--review-zone" or "--review-studio" or "--review-console" or "--review-management" or "--review-product" or "--review-models" or "--review-live" or "--review-design" or "--review-showcase" or "--review-dashboard" or "--review-input" or "--review-intro" or "--review-zone-motion" or "--review-windowed" or "--review-pages" or "--review-stress" or "--review-probe" or "--review-readme" or "--review-talk" or "--agent-bakeoff" or "--agent-drift" or "--agent-m3" or "--agent-phrasebook" or "--agent-m5" or "--agent-phrasebook-clean" or "--review-scanflow" or "--review-agent-look")
            {
                Directory.CreateDirectory(e.Args[1]);
                File.WriteAllText(Path.Combine(e.Args[1], "failure.txt"), error.ToString());
            }
            else MessageBox.Show(error.Message, "X20CTL development preview");
            Shutdown(1);
        }
    }
}


