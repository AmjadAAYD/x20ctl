using System.IO;
using System.Windows;
namespace X20Ctl.Product;

public partial class App : Application
{
    private async void OnStartup(object sender, StartupEventArgs e)
    {
        try
        {
            if(e.Args.Length==2 && e.Args[0]=="--review-device-telemetry")
            {
                ShutdownMode=ShutdownMode.OnExplicitShutdown;
                try{await Development.DeviceTelemetryReview.Run(e.Args[1]);Shutdown(0);}
                catch(Exception failure){Directory.CreateDirectory(e.Args[1]);File.WriteAllText(Path.Combine(e.Args[1],"failure.txt"),failure.ToString());Shutdown(1);}
                return;
            }
            if(e.Args.Length==3 && e.Args[0]=="--review-input-contract")
            {
                ShutdownMode=ShutdownMode.OnExplicitShutdown;
                try{await Development.InputContractReview.Run(e.Args[1],e.Args[2]);Shutdown(0);}
                catch(Exception failure){Directory.CreateDirectory(e.Args[1]);File.WriteAllText(Path.Combine(e.Args[1],"failure.txt"),failure.ToString());Shutdown(1);}
                return;
            }
            if(e.Args.Length==3 && e.Args[0] is "--review-feature-tabs" or "--review-rgb-brightness")
            {
                ShutdownMode=ShutdownMode.OnExplicitShutdown;
                try{await Development.FeatureTabsReview.Run(e.Args[1],e.Args[2],e.Args[0]=="--review-rgb-brightness");Shutdown(0);}
                catch(Exception failure){Directory.CreateDirectory(e.Args[1]);File.WriteAllText(Path.Combine(e.Args[1],"failure.txt"),failure.ToString());Shutdown(1);}
                return;
            }
            if(e.Args.Length == 1 && e.Args[0] == "--lighting")
            {
                MainWindow = new LightingWindow(); MainWindow.Show(); return;
            }
            if (e.Args.Length == 2 && e.Args[0] is "--review-lighting" or "--review-lighting-ui")
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                Directory.CreateDirectory(e.Args[1]);
                try
                {
                    var lightingReviewWindow = new LightingWindow();
                    var visual = (FrameworkElement)lightingReviewWindow.Content;
                    visual.Width = 550; visual.Height = 720;
                    visual.Measure(new Size(550,720)); visual.Arrange(new Rect(0,0,550,720)); visual.UpdateLayout();
                    var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(550,720,96,96,System.Windows.Media.PixelFormats.Pbgra32);
                    bitmap.Render(visual);
                    var png = new System.Windows.Media.Imaging.PngBitmapEncoder(); png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                    using (var file = File.Create(Path.Combine(e.Args[1],"lighting.png"))) png.Save(file);
                    lightingReviewWindow.ShowBattery();
                    visual.Width = 1100; visual.Height = 800;
                    visual.Measure(new Size(1100,800)); visual.Arrange(new Rect(0,0,1100,800)); visual.UpdateLayout();
                    bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(1100,800,96,96,System.Windows.Media.PixelFormats.Pbgra32);
                    bitmap.Render(visual);
                    png = new System.Windows.Media.Imaging.PngBitmapEncoder(); png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                    using (var file = File.Create(Path.Combine(e.Args[1],"lighting-battery.png"))) png.Save(file);
                    if(e.Args[0] == "--review-lighting")
                    {
                        var record = await LightingBleClient.RunAsync();
                        File.WriteAllText(Path.Combine(e.Args[1],"read-only-record.txt"),Convert.ToHexString(record));
                    }
                    Shutdown(0);
                }
                catch(Exception failure) { File.WriteAllText(Path.Combine(e.Args[1],"failure.txt"),failure.ToString()); Shutdown(1); }
                return;
            }
            if (e.Args.Length is 2 or 3 && e.Args[0] == "--review-screen-probe")
            {
                // Real pad: waits for the screen port (hold M while plugging in), reads the flash ID, then asks
                // for the CRCs at the asset and metadata addresses. With a GIF, compares them with our own encoding; upload=<gif> writes it.
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                Directory.CreateDirectory(e.Args[1]);
                string logFile = Path.Combine(e.Args[1], "screen-probe.txt");
                void Log(string line) => File.AppendAllText(logFile, $"{DateTime.Now:HH:mm:ss.fff} {line}\r\n");
                try { if (e.Args.Length == 3 && e.Args[2].StartsWith("upload")) await Development.ScreenProbe.Upload(e.Args[1], e.Args[2] == "upload-test" ? null : e.Args[2]["upload=".Length..], Log); else await Development.ScreenProbe.Run(e.Args[1], e.Args.Length == 3 ? e.Args[2] : null, Log); }
                catch (Exception error) { Log($"FAILED {error.GetType().Name}: {error.Message}"); Environment.ExitCode = 1; }
                Shutdown(); return;
            }
            if (e.Args.Length is 2 or 3 && e.Args[0] == "--review-remap-write")
            {
                // Real pad: writes one mapping such as A=B through the same path as Apply; with no mapping, clears them.
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                Directory.CreateDirectory(e.Args[1]);
                string logFile = Path.Combine(e.Args[1], "remap-write.txt");
                try
                {
                    var map = e.Args.Length == 3 ? new Dictionary<string, string> { [e.Args[2].Split('=')[0]] = e.Args[2].Split('=')[1] } : new();
                    int changed = await LightingBleClient.WriteConfigAsync(held => held with { Remap = X20Ctl.Zone.ControllerRecords.BuildRemap(map) });
                    var seen = await LightingBleClient.ReadConfigAsync();
                    File.AppendAllText(logFile, $"{DateTime.Now:HH:mm:ss} wrote {(e.Args.Length == 3 ? e.Args[2] : "none")} changed {changed} reads {Convert.ToHexString(seen.Remap)}\r\n");
                }
                catch (Exception error) { File.AppendAllText(logFile, $"{DateTime.Now:HH:mm:ss} FAILED {error.GetType().Name}: {error.Message}\r\n"); Environment.ExitCode = 1; }
                Shutdown(); return;
            }
            if (e.Args.Length == 2 && e.Args[0] == "--review-curve-read")
            {
                // Real pad, read only: the stick and trigger curve records as the pad holds them.
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                Directory.CreateDirectory(e.Args[1]);
                try
                {
                    var held = await LightingBleClient.ReadConfigAsync(); var text = new System.Text.StringBuilder();
                    text.AppendLine("sticks " + Convert.ToHexString(held.Sticks)); text.AppendLine("triggers " + Convert.ToHexString(held.Triggers));
                    foreach (var (name, record, trigger) in new[] { ("sticks", held.Sticks, false), ("triggers", held.Triggers, true) })
                        for (int c = 0; c < 2; c++) { var s = X20Ctl.Zone.ControllerRecords.ParseChannel(record, c, trigger); text.AppendLine($"{name}[{c}] preset {s.Preset} inner {s.Inner} outer {s.Outer} p1 {s.Point1} p2 {s.Point2}"); }
                    File.WriteAllText(Path.Combine(e.Args[1], "curve-read.txt"), text.ToString()); Shutdown(0);
                }
                catch (Exception failure) { File.WriteAllText(Path.Combine(e.Args[1], "failure.txt"), failure.ToString()); Shutdown(1); }
                return;
            }
            if (e.Args.Length == 3 && e.Args[0] == "--review-curve-write")
            {
                // Real pad, one pass: raise the left stick's inner dead zone through the same path as Apply, hold it, then put the exact old record back.
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                Directory.CreateDirectory(e.Args[1]);
                var log = new System.Text.StringBuilder();
                try
                {
                    int inner = int.Parse(e.Args[2]);
                    var before = await LightingBleClient.ReadConfigAsync(); log.AppendLine("before sticks " + Convert.ToHexString(before.Sticks));
                    var left = X20Ctl.Zone.ControllerRecords.ParseChannel(before.Sticks, 0, false); var right = X20Ctl.Zone.ControllerRecords.ParseChannel(before.Sticks, 1, false);
                    log.AppendLine($"left inner {left.Inner} outer {left.Outer}; right inner {right.Inner} outer {right.Outer}");
                    int changed = await LightingBleClient.WriteConfigAsync(held => held with { Sticks = X20Ctl.Zone.ControllerRecords.BuildCurves(held.Sticks, left with { Inner = inner }, right, false) });
                    var during = await LightingBleClient.ReadConfigAsync(); log.AppendLine($"wrote inner {inner}, changed {changed}, now {Convert.ToHexString(during.Sticks)} left inner {X20Ctl.Zone.ControllerRecords.ParseChannel(during.Sticks, 0, false).Inner}");
                    await Task.Delay(15000);
                    int back = await LightingBleClient.WriteConfigAsync(held => held with { Sticks = before.Sticks }); var after = await LightingBleClient.ReadConfigAsync();
                    log.AppendLine($"restored changed {back}, now {Convert.ToHexString(after.Sticks)} matches {after.Sticks.SequenceEqual(before.Sticks)}");
                    File.WriteAllText(Path.Combine(e.Args[1], "curve-write.txt"), log.ToString());
                    Shutdown(after.Sticks.SequenceEqual(before.Sticks) ? 0 : 1);
                }
                catch (Exception failure) { File.WriteAllText(Path.Combine(e.Args[1], "failure.txt"), log + failure.ToString()); Shutdown(1); }
                return;
            }
            if (e.Args.Length == 2 && e.Args[0] == "--review-macro-read")
            {
                // Real pad, reads only: every stored macro through the same path as the Read controller button.
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                Directory.CreateDirectory(e.Args[1]);
                var log = new System.Text.StringBuilder();
                for (int slot = 0; slot < 6; slot++)
                    try
                    {
                        var raw = await LightingBleClient.ReadMacroAsync(slot);
                        var events = X20Ctl.Zone.MacroRecord.Parse(raw);
                        log.AppendLine($"M{slot + 1} {Convert.ToHexString(raw)} -> " + string.Join("; ", events.Select(a => $"{string.Join("+", a.Buttons)} L:{a.Left} R:{a.Right} hold {a.HoldMs} pause {a.PauseMs}")));
                    }
                    catch (Exception error) { log.AppendLine($"M{slot + 1} FAILED {error.GetType().Name}: {error.Message}"); }
                File.WriteAllText(Path.Combine(e.Args[1], "macro-read.txt"), log.ToString());
                Shutdown(); return;
            }
            if (e.Args.Length == 2 && e.Args[0] == "--review-mouse-live")
            {
                // Real pad, the owner's saved mouse settings copied aside, on for three seconds; every status line is logged.
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                Directory.CreateDirectory(e.Args[1]);
                string logFile = Path.Combine(e.Args[1], "mouse-live.txt"), saved = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "x20ctl", "mouse-player1-x20_pro.json");
                if (File.Exists(saved)) File.Copy(saved, Path.Combine(e.Args[1], "mouse-player1-x20_pro.json"), true);
                using var mouse = new MouseModeService(Path.Combine(e.Args[1], "mouse.json"));
                void Line() { lock (logFile) File.AppendAllText(logFile, $"{DateTime.Now:HH:mm:ss.fff} source {mouse.Profile.SourceSlot} connected {mouse.Connected} active {mouse.Active}: {mouse.Status}\r\n"); }
                mouse.Changed += Line;
                mouse.TakeControl(0, "x20_pro");
                await Task.Delay(3000); Line();
                mouse.Active = false; await Task.Delay(300); Line();
                Shutdown(); return;
            }
            if (e.Args.Length == 3 && e.Args[0] == "--review-pcmode-live")
            {
                // Real drivers and real pad, on a throwaway A-to-B profile, for a set number of seconds; every status line is logged.
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                Directory.CreateDirectory(e.Args[1]);
                string logFile = Path.Combine(e.Args[1], "pcmode-live.txt");
                var store = new X20Ctl.Zone.PcMode.PcModeStore(Path.Combine(e.Args[1], "pc-mode-probe.json")); store.Load();
                store.Save(X20Ctl.Zone.PcMode.PcModeStore.DefaultKey, new X20Ctl.Zone.PcMode.PcModeProfile { Remap = { ["A"] = "B" } });
                var service = new PcModeService(store);
                void Line(string text) { lock (store) File.AppendAllText(logFile, $"{DateTime.Now:HH:mm:ss.fff} {text}\r\n"); }
                service.Changed += () => Line($"{service.State}: {service.Detail}");
                service.Enabled = true;
                await Task.Delay(TimeSpan.FromSeconds(int.Parse(e.Args[2])));
                Line("routes " + string.Join(",", service.Routes) + " virtual " + PcModeService.VirtualSlot + " applied " + service.Applied);
                await Task.Run(service.Dispose);
                Line("stopped");
                Shutdown(0); return;
            }
            if (e.Args.Length is 3 or 4 && e.Args[0] == "--review-custom-colour")
            {
                // Real pad, one pass: write one non-stock colour in Solid, hold it, put the exact old record back and read it back.
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                Directory.CreateDirectory(e.Args[1]);
                var log = new System.Text.StringBuilder();
                try
                {
                    int rgb = Convert.ToInt32(e.Args[2], 16);
                    var before = await LightingBleClient.RunAsync(); log.AppendLine("before " + Convert.ToHexString(before));
                    byte mode = e.Args.Length == 4 ? byte.Parse(e.Args[3]) : (byte)2;
                    if (mode != 2) { var lead = await LightingBleClient.RunAsync(before[..3], mode); log.AppendLine("effect " + Convert.ToHexString(lead)); await Task.Delay(5000); }
                    var written = await LightingBleClient.RunAsync([(byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb], mode); log.AppendLine("written " + Convert.ToHexString(written));
                    await Task.Delay(8000);
                    var restored = await LightingBleClient.RunAsync(before[..3], before[6]); log.AppendLine("restored " + Convert.ToHexString(restored)); log.AppendLine("matches " + restored.SequenceEqual(before));
                    File.WriteAllText(Path.Combine(e.Args[1], "custom-colour.txt"), log.ToString());
                    Shutdown(restored.SequenceEqual(before) ? 0 : 1);
                }
                catch (Exception failure) { File.WriteAllText(Path.Combine(e.Args[1], "failure.txt"), log + failure.ToString()); Shutdown(1); }
                return;
            }
            if (e.Args.Length == 2 && e.Args[0] == "--review-battery-lighting")
            {
                // Real pad, one pass: read the level, write the default colour for it, read back, put the old colour and effect back.
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                Directory.CreateDirectory(e.Args[1]);
                var log = new System.Text.StringBuilder();
                try
                {
                    var before = await LightingBleClient.RunAsync(); log.AppendLine("before " + Convert.ToHexString(before));
                    var battery = (await NativeBleClient.QueryAsync("x20_pro")).Telemetry?.Battery; log.AppendLine("battery " + battery);
                    int level = X20Ctl.Zone.Lighting.BatteryLighting.ParseLevel(battery) ?? throw new IOException("No battery level.");
                    int colour = X20Ctl.Zone.Lighting.BatteryLighting.Default.ColourFor(level);
                    var written = await LightingBleClient.RunAsync([(byte)(colour >> 16), (byte)(colour >> 8), (byte)colour], 2); log.AppendLine("written " + Convert.ToHexString(written));
                    await Task.Delay(4000);
                    var restored = await LightingBleClient.RunAsync(before[..3], before[6]); log.AppendLine("restored " + Convert.ToHexString(restored)); log.AppendLine("matches " + restored.SequenceEqual(before));
                    File.WriteAllText(Path.Combine(e.Args[1], "battery-lighting.txt"), log.ToString());
                    Shutdown(restored.SequenceEqual(before) ? 0 : 1);
                }
                catch (Exception failure) { File.WriteAllText(Path.Combine(e.Args[1], "failure.txt"), log + failure.ToString()); Shutdown(1); }
                return;
            }
            if (e.Args.Length is 3 or 4 && e.Args[0] == "--review-screen")
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                try { await Development.ScreenReview.Run(e.Args[1],e.Args[2],e.Args.Length==4?e.Args[3]:null); Shutdown(0); }
                catch(Exception failure) { Directory.CreateDirectory(e.Args[1]); File.WriteAllText(Path.Combine(e.Args[1],"failure.txt"),failure.ToString()); Shutdown(1); }
                return;
            }
            if (e.Args.Length == 3 && e.Args[0] == "--review-themes" && e.Args[2] == "baseline-default")
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                try { await Development.DefaultBaselineReview.Run(e.Args[1]); Shutdown(0); }
                catch (Exception failure) { Directory.CreateDirectory(e.Args[1]); File.WriteAllText(Path.Combine(e.Args[1], "failure.txt"), failure.ToString()); Shutdown(1); }
                return;
            }
            if (e.Args.Length == 3 && e.Args[0] == "--review-scanner-replay")
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                await Development.ScannerReplay.Run(e.Args[1], e.Args[2]); Shutdown(0); return;
            }
            if (e.Args.Length == 2 && e.Args[0] == "--review-update-mock")
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                var failures = await Development.UpdateMockReview.Run(e.Args[1]); Shutdown(failures); return;
            }
            if (e.Args.Length == 2 && e.Args[0] == "--review-update-states")
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                Development.UpdateStatesReview.Run(e.Args[1]); Shutdown(0); return;
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
                string sample = " [ { \"friendlyName\" : \"Pad\" , \"devices\" : [\n{ \"present\" : true ,\n\"gamingDevice\" : true ,\n\"deviceInstancePath\" : \"HID\\\\VID_045E&PID_028E&IG_00\\\\7&1\" ,\n\"xusbDeviceInstancePath\" : \"USB\\\\VID_045E&PID_028E\\\\ABC\" ,\n\"baseContainerDeviceCount\" : 2 } ] } ,\n{ \"friendlyName\" : \"Old\" , \"devices\" : [\n{ \"present\" : false ,\n\"deviceInstancePath\" : \"HID\\OLD\" ,\n\"xusbDeviceInstancePath\" : \"\" ,\n\"baseContainerDeviceCount\" : 1 } ] } ] ";
                log.Add("parsed: " + string.Join(" | ", X20Ctl.Zone.PcMode.HidHideDeviceList.Parse(sample)));
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
            if (e.Args.Length == 3 && e.Args[0] == "--review-theme-intro")
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown; IntroView.SoundEnabled = false;
                try { await Development.ThemeIntroReview.Run(e.Args[1], e.Args[2]); }
                catch (Exception failure) { Directory.CreateDirectory(e.Args[1]); File.WriteAllText(Path.Combine(e.Args[1], "failure.txt"), failure.ToString()); Shutdown(1); return; }
                Shutdown(0); return;
            }
            if (e.Args.Length == 3 && e.Args[0] == "--review-themes")
            {
                // windowless: the pages are hosted in a hidden HwndSource and rendered off screen
                ShutdownMode = ShutdownMode.OnExplicitShutdown; IntroView.SoundEnabled = false;
                try { await Development.ThemeReview.Run(e.Args[1], e.Args[2]); } catch (Exception failure) { Directory.CreateDirectory(e.Args[1]); File.WriteAllText(Path.Combine(e.Args[1], "failure.txt"), failure.ToString()); Shutdown(1); return; }
                Shutdown(0); return;
            }
            if (e.Args.Length == 2 && e.Args[0] == "--review-finishes")
            {
                // windowless: every picture is rendered off screen
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                try { await Development.FinishReview.Run(e.Args[1]); } catch (Exception failure) { File.WriteAllText(e.Args[1] + ".failure.txt", failure.ToString()); }
                Shutdown(0); return;
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
            if (e.Args.SequenceEqual(new[] { "--warm-up" }))
            {   // the updater runs a newly installed exe this way once: starting is enough to unpack it, so its first real open is quick
                Shutdown(0); return;
            }
            if (Setup.Installer.Detect(e.Args, Environment.ProcessPath) is var setup && setup != Setup.SetupMode.None)
            {   // the setup, the uninstaller and their silent forms are this same exe
                Setup.SetupApp.Run(this, setup, e.Args); return;
            }
            // the saved theme goes into the shared resources before anything is drawn from them (Tools → Themes)
            ThemeCatalog.Apply(ThemeCatalog.Saved(ProductHost.PreferencesPath));
            Loc.Apply(Loc.Saved(ProductHost.PreferencesPath));
            // the intro film starts opening now, while the window is built, so it plays from the first frame
            if (ProductHost.IntroAtLaunch(e.Args.Contains("--reduced-motion"))) IntroView.PreloadFilm();
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
            BatteryLightingService.Resume();
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


