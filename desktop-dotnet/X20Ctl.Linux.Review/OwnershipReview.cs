using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System.Reflection;
using System.Text.Json;
using X20Ctl.Linux;
using X20Ctl.Zone;

internal static class OwnershipReview
{
    public static async Task Run(MainWindow window, string report)
    {
        var checks = new List<string>();
        void Check(bool ok, string name) { if (!ok) throw new InvalidOperationException(name); checks.Add(name); }
        async Task Pump() { await Task.Delay(30); await Dispatcher.UIThread.InvokeAsync(() => window.UpdateLayout(), DispatcherPriority.Background); }
        var catalog = new ControllerCatalog(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "catalog.json")));
        var state = new StudioState(catalog.Get("x20"));
        var bench = new Window { Width = 1600, Height = 900 }; bench.Show(); // Avalonia headless platform only.
        void Save(string name)
        {
            string folder = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(report))!, "input-ownership-renders"); Directory.CreateDirectory(folder);
            bench.UpdateLayout(); using var bitmap = new RenderTargetBitmap(new PixelSize(1600,900)); bitmap.Render(bench); bitmap.Save(Path.Combine(folder,name+".png"), PngBitmapEncoderOptions.Default);
        }
        try
        {
            var source = new EvDevice("/dev/input/event-a", "Synthetic A", 1, 2, Instance: "instance-a");
            Check(InputSources.Select(0, source), "explicit source selected");
            Check(!InputSources.Select(1, source), "duplicate source rejected");
            Check(InputSources.Key(source) != InputSources.Key(source with { Instance = "replacement" }), "reused event path does not preserve instance identity");
            InputSources.RemoveMissing([source with { Instance = "replacement" }]);
            Check(InputSources.Source(0) == null, "replacement instance clears ownership without fallback");
            var reader = new GamepadReader(); int callbacks = 0; reader.Frame += _ => callbacks++;
            reader.Start(0);
            var generation = typeof(GamepadReader).GetField("generation", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var publish = typeof(GamepadReader).GetMethod("Publish", BindingFlags.Instance | BindingFlags.NonPublic)!;
            publish.Invoke(reader, [new PadFrame(new HashSet<string> { "A" }, 0, 0, 0, 0, 0, 0), (int)generation.GetValue(reader)!, true, "synthetic"]);
            Check(!reader.Connected, "first report cannot arm UI navigation before its queued delivery");
            reader.Stop(); int stopped = callbacks; await Pump();
            Check(callbacks == stopped && !reader.Connected && reader.Snapshot().Down.Count == 0, "queued old generation cannot deliver after stop");
            reader.Start(0); var frames = new List<string>(); reader.Frame += f => frames.Add(string.Join(",", f.Down));
            int fresh = (int)generation.GetValue(reader)!;
            publish.Invoke(reader, [new PadFrame(new HashSet<string> { "LB" }, 0, 0, 0, 0, 0, 0), fresh, true, "synthetic"]);
            publish.Invoke(reader, [PadFrame.Empty, fresh, true, "synthetic"]); await Pump();
            Check(frames.Count == 2 && frames[0] == "LB" && frames[1] == "", "down and up reports survive before the UI consumes them: " + JsonSerializer.Serialize(frames) + " generation=" + generation.GetValue(reader) + " expected=" + fresh + " pending=" + typeof(GamepadReader).GetField("pending", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(reader));
            frames.Clear(); for (int i=0;i<257;i++) publish.Invoke(reader, [PadFrame.Empty, fresh, true, "synthetic"]); await Pump();
            Check(frames.All(f => f.Length == 0) && !reader.Connected, "queue overflow invalidates old generation and neutralizes instead of losing edges silently");
            reader.Stop(); reader.Dispose(); Check(true, "reader can start stop and restart without disposed cancellation token");

            var page = new ButtonsPage(state); bench.Content = page; await Pump();
            void Click(string text) => page.GetVisualDescendants().OfType<Pill>().Single(p => p.Text == text).Invoke();
            void Feed(params string[] keys) => page.CaptureFrame(new(new HashSet<string>(keys), 0, 0, 0, 0, 0, 0));
            Click("Listen for source"); Check(!page.OwnsInput, "capture cannot arm without a selected source");
            var caps = new PadCaps(PadLayouts.StandardKeys.Keys.ToHashSet(), new Dictionary<int,AbsInfo> { [0]=new(-32768,32767,0),[1]=new(-32768,32767,0),[3]=new(-32768,32767,0),[4]=new(-32768,32767,0),[2]=new(0,255,0),[5]=new(0,255,0),[16]=new(-1,1,0),[17]=new(-1,1,0) });
            InputSources.Select(0, source with { Caps = caps }); await Pump();
            Click("Listen for source"); await Pump(); Save("buttons-listening"); Feed("A");
            Check(page.OwnsInput && state.Buttons.UnsentChanges == 0, "activation hold cannot assign");
            Feed(); Feed("A", "B"); Check(state.Buttons.UnsentChanges == 0, "simultaneous source does not assign");
            Feed(); Feed("LB"); Feed("LB", "B"); Check(state.Buttons.Target("LB") == "LB", "source release barrier preserves mapping");
            Feed(); Feed("B"); Check(state.Buttons.Target("LB") == "B" && !page.OwnsInput, "LB to B updates draft without navigation");
            await Pump(); Save("buttons-LB-to-B-draft"); Click("Listen for target"); Feed(); page.CancelInput(); Feed("X");
            Check(state.Buttons.Target("LB") == "B", "cancel preserves existing draft");
            await Pump(); Click("Listen for source"); Feed("START", "SELECT");
            Check(!page.OwnsInput && state.Buttons.Target("LB") == "B", "explicit capture cancel chord preserves draft");

            var studio = new StudioView(state, 0, () => { }, new TextBlock(), null); bench.Content = studio; await Pump();
            studio.ShowTab("Buttons"); await Pump();
            studio.GetVisualDescendants().OfType<Pill>().Single(p => p.Text == "Listen for source").Invoke();
            studio.StepTab(1); Check(studio.CurrentPage == "Buttons", "exclusive capture suppresses bumper tab command");
            studio.ResetInput(); studio.StepTab(1); Check(studio.CurrentPage == "Curves", "browse bumper advances actual visible tab catalog");
            studio.ShowTab("Tester"); await Pump(); studio.StepTab(1); Check(studio.CurrentPage == "Tester", "tester owns controller buttons");
            studio.ShowTab("Curves"); await Pump(); studio.ShowTab("Dashboard"); await Pump(); studio.ShowTab("Curves"); await Pump();
            Check(studio.CurrentPage == "Curves", "cached reader page reattaches without lifecycle exception");
            ReviewMode.Pads = [source]; InputSources.Select(0, source); studio.ShowTab("PC mode"); await Pump(); Save("player-input-selection");
            Check(typeof(MainWindow).Assembly.GetType("X20Ctl.Linux.IntroSound") == null, "no audio playback class compiled into Linux app");
        }
        finally { bench.Close(); ReviewMode.Pads = null; InputSources.Select(0, null); InputSources.Select(1, null); }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(report))!);
        await File.WriteAllTextAsync(report, JsonSerializer.Serialize(new { passed = checks.Count, checks, physicalHardware = false, linuxRuntime = false, platform = "Avalonia headless on Windows" }, new JsonSerializerOptions { WriteIndented = true }));
        window.Close();
    }
}
