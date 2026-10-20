using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
namespace X20Ctl.Product.Development;

/// <summary>How long the app takes to show and to open each screen (--review-timing FILE): process start to first frame,
/// then Tools, PC mode, Controller Check and each Studio page, each measured until its first frame is drawn.</summary>
internal static class TimingReview
{
    public static async Task Run(string file)
    {
        ReviewSandbox.Active = true;
        var lines = new List<string>();
        string folder = Path.Combine(Path.GetTempPath(), "x20ctl-timing-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        var sinceStart = DateTime.Now - Process.GetCurrentProcess().StartTime;
        lines.Add($"process start to harness: {sinceStart.TotalMilliseconds:0} ms");
        var clock = Stopwatch.StartNew();
        if (Environment.GetEnvironmentVariable("X20CTL_TIMING_LAUNCH") == "1") { await Launch(file, lines, folder, clock); return; }
        if (Environment.GetEnvironmentVariable("X20CTL_TIMING_FILM") == "1") { await FilmOpen(file, lines); return; }
        if (Environment.GetEnvironmentVariable("X20CTL_TIMING_PARTS") == "1")
        {   // the window's two big parts on their own, cold (so this run's later numbers are warm)
            var scene = new ZoneScene(true); lines.Add($"  ZoneScene alone: {clock.ElapsedMilliseconds} ms");
            var part = Stopwatch.StartNew(); _ = new ProductHost(scene, Path.Combine(folder, "parts.json")); lines.Add($"  ProductHost alone: {part.ElapsedMilliseconds} ms");
            clock.Restart();
        }
        var window = new ProductWindow(true, Path.Combine(folder, "assignments.json"));
        lines.Add($"window constructed: {clock.ElapsedMilliseconds} ms");
        var rendered = new TaskCompletionSource(); window.ContentRendered += (_, _) => rendered.TrySetResult();
        window.Show(); await rendered.Task; await Frame();
        lines.Add($"first frame: {clock.ElapsedMilliseconds} ms (process start to first frame {(DateTime.Now - Process.GetCurrentProcess().StartTime).TotalMilliseconds:0} ms)");
        try
        {
            {
                var animated = new Dictionary<string, int>();
                void Walk(DependencyObject d)
                {
                    if (d is UIElement u && u.HasAnimatedProperties) { string k = d.GetType().Name + (d is FrameworkElement f && f.Name.Length > 0 ? "#" + f.Name : "") + " in " + (System.Windows.Media.VisualTreeHelper.GetParent(d)?.GetType().Name ?? "-"); animated[k] = animated.GetValueOrDefault(k) + 1; }
                    for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(d); i++) Walk(System.Windows.Media.VisualTreeHelper.GetChild(d, i));
                }
                Walk(window); lines.Add("Zone animated: " + string.Join(", ", animated.Select(kv => $"{kv.Key} x{kv.Value}")));
            }
            lines.Add($"render tier {System.Windows.Media.RenderCapability.Tier >> 16}, window {window.ActualWidth}x{window.ActualHeight} at {window.Left},{window.Top}, monitor DPI scale {System.Windows.Media.VisualTreeHelper.GetDpi(window).DpiScaleX}");
            if (Environment.GetEnvironmentVariable("X20CTL_TIMING_HIDE") is { Length: > 0 } hide)
            {
                int hidden = 0;
                void Hide(DependencyObject d)
                {
                    if (d is UIElement u && hide.Split(',').Contains(d.GetType().Name)) { u.Visibility = Visibility.Collapsed; hidden++; return; }
                    for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(d); i++) Hide(System.Windows.Media.VisualTreeHelper.GetChild(d, i));
                }
                Hide(window); lines.Add($"hid {hidden} of {hide}");
            }
            await Cpu("on the Zone, visible");
            await Measure("open Tools", () => window.Host.ShowSettings());
            await Measure("Tools → PC mode", () => window.Host.Settings!.ShowPcMode(true));
            await Measure("close Tools", () => window.Host.CloseSettings());
            await Measure("open Tools again", () => window.Host.ShowSettings());
            window.Host.CloseSettings(); await Task.Delay(400);
            await Measure("open Controller Check", () => window.Host.OpenCheck());
            window.Host.CloseCheck(); await Task.Delay(400);
            window.Scene.AssignController(0, window.Host.Catalog.Get("x20"));
            await Measure("enter Studio (X20)", () => window.Host.EnterStudio(0));
            var s = window.Host.Studio!;
            await Measure("Buttons", () => s.ShowButtons());
            await Measure("Curves", () => s.ShowCurves(true));
            await Measure("Macros", () => s.ShowMacros(true));
            await Measure("Vibration", () => s.ShowVibration(true));
            await Measure("Device", () => s.ShowManagement("Device"));
            await Measure("Setups", () => s.ShowManagement("Setups"));
            await Measure("Dashboard", () => s.ShowManagement("Dashboard"));
            await Measure("back to Zone", () => window.Host.ReturnToZone());
            await Measure("enter Studio again", () => window.Host.EnterStudio(0));
            // what the app costs when nobody is looking: shown on the Studio, then hidden the way the tray hides it
            async Task Cpu(string name)
            {
                await Task.Delay(1500); var p = Process.GetCurrentProcess(); p.Refresh(); var cpu0 = p.TotalProcessorTime; var wall = Stopwatch.StartNew();
                var before = new Dictionary<int, TimeSpan>(); foreach (ProcessThread th in p.Threads) try { before[th.Id] = th.TotalProcessorTime; } catch (Exception) { }
                await Task.Delay(5000); p.Refresh();
                var busy = new List<string>();
                foreach (ProcessThread th in p.Threads) try { var d = th.TotalProcessorTime - before.GetValueOrDefault(th.Id); if (d.TotalMilliseconds > 100) busy.Add($"thread {th.Id} {d.TotalMilliseconds / wall.Elapsed.TotalMilliseconds * 100:0}%{(th.Id == UiThread ? " (UI)" : "")}"); } catch (Exception) { }
                lines.Add("  busiest: " + string.Join(", ", busy));
                lines.Add($"CPU {name}: {(p.TotalProcessorTime - cpu0).TotalMilliseconds / wall.Elapsed.TotalMilliseconds * 100 / Environment.ProcessorCount:0.0}% of the machine ({(p.TotalProcessorTime - cpu0).TotalMilliseconds / wall.Elapsed.TotalMilliseconds * 100:0}% of one core)");
            }
            {
                int frames = 0; void Count(object? s, EventArgs e) => frames++;
                System.Windows.Media.CompositionTarget.Rendering += Count; await Task.Delay(2000); System.Windows.Media.CompositionTarget.Rendering -= Count;
                var animated = new Dictionary<string, int>();
                void Walk(DependencyObject d)
                {
                    if (d is UIElement u && u.HasAnimatedProperties) { string k = d.GetType().Name + (d is FrameworkElement f && f.Name.Length > 0 ? "#" + f.Name : "") + " in " + (System.Windows.Media.VisualTreeHelper.GetParent(d)?.GetType().Name ?? "-"); animated[k] = animated.GetValueOrDefault(k) + 1; }
                    for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(d); i++) Walk(System.Windows.Media.VisualTreeHelper.GetChild(d, i));
                }
                Walk(window);
                lines.Add($"idle Studio: {frames / 2} frames/s; animated elements: " + string.Join(", ", animated.Select(kv => $"{kv.Key} x{kv.Value}")));
            }
            await Cpu("on the Studio, visible");
            window.Host.Studio!.ShowButtons(); await Cpu("on Buttons, visible");
            window.Hide(); await Cpu("hidden to the tray");
            window.Show(); window.WindowState = WindowState.Minimized; await Cpu("minimized");
            window.WindowState = WindowState.Maximized;
            lines.Add($"working set: {Process.GetCurrentProcess().WorkingSet64 / 1048576} MB, managed heap {GC.GetTotalMemory(false) / 1048576} MB, gen0/1/2 GCs {GC.CollectionCount(0)}/{GC.CollectionCount(1)}/{GC.CollectionCount(2)}");
        }
        catch (Exception failure) { lines.Add("FAILED: " + failure); }
        finally { window.Close(); File.WriteAllLines(file, lines); try { Directory.Delete(folder, true); } catch (IOException) { } }

        async Task Measure(string name, Action action)
        {
            await Task.Delay(600); // let the previous screen's animation settle
            var t = Stopwatch.StartNew(); action(); long build = t.ElapsedMilliseconds; await Frame();
            lines.Add($"{name}: {build} ms to build, {t.ElapsedMilliseconds} ms to first frame");
        }
    }

    /// <summary>X20CTL_TIMING_LAUNCH=1: the window as a real launch builds it (the intro film, the native core, the tray icon),
    /// on fixture files, timed from process start until the intro hands over to the Zone.</summary>
    static async Task Launch(string file, List<string> lines, string folder, Stopwatch clock)
    {
        double Since() => (DateTime.Now - Process.GetCurrentProcess().StartTime).TotalMilliseconds;
        ProductWindow? window = null;
        try
        {
            window = new ProductWindow(false, Path.Combine(folder, "assignments.json"), firstRun: true, nativeEngine: true);
            lines.Add($"window constructed: {clock.ElapsedMilliseconds} ms (process start {Since():0} ms)");
            var tray = Stopwatch.StartNew(); new TrayIcon(window).Dispose(); lines.Add($"tray icon: {tray.ElapsedMilliseconds} ms");
            var rendered = new TaskCompletionSource(); window.ContentRendered += (_, _) => rendered.TrySetResult();
            window.Show(); await rendered.Task; await Frame();
            lines.Add($"first frame: process start {Since():0} ms");
            var host = window.Host; bool film = false, engine = false; var limit = Stopwatch.StartNew();
            while (host.Intro != null && limit.Elapsed < TimeSpan.FromSeconds(20))
            {
                if (!film && host.Intro.FilmOpened) { film = true; lines.Add($"intro film opened: process start {Since():0} ms"); }
                if (!engine && host.Engine != null && host.NativeEngineStatus.StartsWith("Native C++")) { engine = true; lines.Add($"native core ready: process start {Since():0} ms"); }
                await Task.Delay(10);
            }
            lines.Add($"intro handed over to the Zone: process start {Since():0} ms");
        }
        catch (Exception failure) { lines.Add("FAILED: " + failure); }
        finally { window?.Close(); File.WriteAllLines(file, lines); try { Directory.Delete(folder, true); } catch (IOException) { } }
    }

    /// <summary>X20CTL_TIMING_FILM=1: no window at all. How long the intro film takes to open on its own, and how long the
    /// UI thread is held while it does.</summary>
    static async Task FilmOpen(string file, List<string> lines)
    {
        double Since() => (DateTime.Now - Process.GetCurrentProcess().StartTime).TotalMilliseconds;
        foreach (int round in new[] { 1, 2 })
        {
            var clock = Stopwatch.StartNew();
            var player = new System.Windows.Media.MediaPlayer { IsMuted = true };
            var opened = new TaskCompletionSource<string>();
            player.MediaOpened += (_, _) => opened.TrySetResult("opened");
            player.MediaFailed += (_, e) => opened.TrySetResult("failed: " + e.ErrorException.Message);
            player.Open(new Uri(Path.Combine(AppContext.BaseDirectory, "Brand", "intro.mp4")));
            lines.Add($"round {round}: Open() held the UI thread {clock.ElapsedMilliseconds} ms (process start {Since():0} ms)");
            TimeSpan longest = TimeSpan.Zero; var limit = Stopwatch.StartNew();
            while (!opened.Task.IsCompleted && limit.Elapsed < TimeSpan.FromSeconds(10)) { var before = clock.Elapsed; await Task.Delay(5); if (clock.Elapsed - before > longest) longest = clock.Elapsed - before; }
            lines.Add($"round {round}: {(opened.Task.IsCompleted ? opened.Task.Result : "timed out")} after {clock.ElapsedMilliseconds} ms, longest UI stall {longest.TotalMilliseconds:0} ms, {player.NaturalVideoWidth}x{player.NaturalVideoHeight}");
            var play = Stopwatch.StartNew(); player.Play(); lines.Add($"round {round}: Play() held the UI thread {play.ElapsedMilliseconds} ms"); await Task.Delay(300);
            lines.Add($"round {round}: position after 300 ms of play: {player.Position.TotalMilliseconds:0} ms");
            player.Close();
        }
        // the launch path: the film opened early is handed to the intro already open, so it can play from the first frame
        IntroView.PreloadFilm(); await Task.Delay(1500);
        var intro = new IntroView(false); lines.Add($"intro built 1.5 s after the early open: film already open {intro.FilmOpened}"); intro.Finish();
        IntroView.PreloadFilm(); IntroView.DiscardPreload(); var plain = new IntroView(false); lines.Add($"after a discarded early open the intro opens its own: open at once {plain.FilmOpened}"); plain.Finish();
        File.WriteAllLines(file, lines);
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll")] static extern int GetCurrentThreadId();
    static readonly int UiThread = GetCurrentThreadId();
    /// <summary>Waits until layout and the next render have run.</summary>
    static async Task Frame()
    {
        await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.Loaded);
        var drawn = new TaskCompletionSource();
        void OnRender(object? s, EventArgs e) { System.Windows.Media.CompositionTarget.Rendering -= OnRender; drawn.TrySetResult(); }
        System.Windows.Media.CompositionTarget.Rendering += OnRender;
        await drawn.Task;
        await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
    }
}
