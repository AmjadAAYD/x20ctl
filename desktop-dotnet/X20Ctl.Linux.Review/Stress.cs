using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using X20Ctl.Linux;

/// <summary>--stress REPORT: drives the window with real (headless) clicks and keys, often in the middle of an animation:
/// the Zone's glide, screen changes, Studio tabs, the intro played through and skipped, at four window sizes. After each
/// step it lets things settle and checks the window is whole: no screen left behind, nothing stuck faded or scaled, the
/// intro gone and the header back. It only navigates: nothing that saves, sends or opens a link is clicked.</summary>
internal static class Stress
{
    static readonly string[] Tabs = ["Dashboard", "Buttons", "Curves", "Macros", "Vibration", "PC mode", "Device", "Setups", "Diagnostics"];
    static readonly (double W, double H)[] Sizes = [(1600, 900), (1100, 680), (1920, 1080), (1366, 768), (2560, 1440)];

    public static async Task Run(MainWindow window, string report)
    {
        var lines = new List<string>(); var failures = new List<string>(); var random = new Random(20);
        Dispatcher.UIThread.UnhandledException += (_, e) => { failures.Add("unhandled: " + e.Exception); e.Handled = true; };
        // the UI thread's longest stall in each step, from a 60 Hz heartbeat
        var beat = Stopwatch.StartNew(); double worst = 0;
        DispatcherTimer.Run(() => { worst = Math.Max(worst, beat.Elapsed.TotalMilliseconds); beat.Restart(); return true; }, TimeSpan.FromMilliseconds(16));
        var stalls = new List<(string Step, double Ms)>();
        int steps = 0;

        async Task Step(string name, Func<Task> act)
        {
            steps++; worst = 0; beat.Restart();
            try { await act(); }
            catch (Exception e) { failures.Add($"{name}: {e.GetType().Name}: {e.Message}"); }
            stalls.Add((name, worst));
        }
        async Task Settle(string after)
        {
            await Task.Delay(900);
            var stage = (Grid)window.View.Content!;
            var extra = stage.Children.Skip(2).Select(c => c.GetType().Name).ToList();
            if (extra.Count > 0) failures.Add($"{after}: left on the stage: {string.Join(", ", extra)}");
            var root = stage.Children.OfType<Grid>().FirstOrDefault(g => g.Children.OfType<ContentControl>().Count() == 3);
            if (root == null) { failures.Add($"{after}: the screen is missing"); return; }
            if (root.Opacity < .999) failures.Add($"{after}: the screen is stuck at opacity {root.Opacity:0.00}");
            if (root.RenderTransform is ScaleTransform s && Math.Abs(s.ScaleX - 1) > .001) failures.Add($"{after}: the screen is stuck at scale {s.ScaleX:0.000}");
            // the header, the heading and the cards come back after the intro; nothing visible should be left part-faded
            foreach (var c in root.GetVisualDescendants().OfType<Control>())
                if (c.IsEffectivelyVisible && c.Opacity is > .001 and < .999 && c is not Pill && c.GetType().Name is not ("Border" or "TextBlock" or "Path") )
                    failures.Add($"{after}: {c.GetType().Name} stuck at opacity {c.Opacity:0.00}");
            foreach (var c in root.GetVisualDescendants().OfType<Control>())
                if (c.IsEffectivelyVisible && c.Opacity < .999 && c.GetType().FullName is { } n && (n.Contains("ZonePage") || n.EndsWith("Layers")))
                    failures.Add($"{after}: {n} at opacity {c.Opacity:0.00}");
        }
        Control? Find(string text) => window.GetVisualDescendants().OfType<Control>().Where(c => c.IsEffectivelyVisible && c.IsHitTestVisible)
            .FirstOrDefault(c => c is Pill p && p.Text == text || c is TextBlock t && t.Text == text);
        // how long each click's own work takes (building the next screen), apart from drawing it
        var handlers = new Dictionary<string, List<double>>();
        bool Click(string text)
        {
            if (Find(text) is not { } c || c.TranslatePoint(new Point(c.Bounds.Width / 2, c.Bounds.Height / 2), window) is not { } p) return false;
            var took = Stopwatch.StartNew();
            window.MouseMove(p); window.MouseDown(p, MouseButton.Left); window.MouseUp(p, MouseButton.Left);
            (handlers.TryGetValue(text, out var list) ? list : handlers[text] = []).Add(took.Elapsed.TotalMilliseconds);
            return true;
        }
        void Key(Key key) { window.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, null); window.KeyRelease(key, RawInputModifiers.None, PhysicalKey.None, null); }
        Task Jitter() => Task.Delay(random.Next(0, 420));

        foreach (var (w, h) in Sizes)
        {
            window.Width = w; window.Height = h; await Task.Delay(300);
            string at = $"{w}x{h}";
            await Step($"{at} zone keys mid-glide", async () => { for (int i = 0; i < 12; i++) { Key(Avalonia.Input.Key.D1 + random.Next(4)); await Jitter(); } Key(Avalonia.Input.Key.D1); });
            await Settle($"{at} zone keys");
            await Step($"{at} tools and back, interrupted", async () =>
            {
                for (int i = 0; i < 4; i++) { if (!Click("Tools")) failures.Add($"{at}: no Tools button"); await Jitter(); if (!Click("←  Controller Zone")) failures.Add($"{at}: no way back from Tools"); await Jitter(); }
            });
            await Settle($"{at} tools");
            await Step($"{at} about and back", async () => { Click("Tools"); await Task.Delay(400); Click("About"); await Jitter(); Click("←  Back to Tools"); await Jitter(); Click("←  Controller Zone"); });
            await Settle($"{at} about");
            await Step($"{at} chooser and back", async () => { Click("Switch Controller"); await Jitter(); Click("Back"); });
            await Settle($"{at} chooser");
            await Step($"{at} studio tabs, mid-slide", async () =>
            {
                if (!Click("Enter Studio")) failures.Add($"{at}: no Enter Studio");
                await Task.Delay(400);
                for (int i = 0; i < 14; i++) { if (!Click(Tabs[random.Next(Tabs.Length)])) failures.Add($"{at}: a tab was not found"); await Jitter(); }
                for (int i = 0; i < 6; i++) { Key(random.Next(2) == 0 ? Avalonia.Input.Key.PageDown : Avalonia.Input.Key.PageUp); await Jitter(); }
                if (!Click("← Controller Zone")) failures.Add($"{at}: no way back from the Studio");
            });
            await Settle($"{at} studio");
            await Step($"{at} intro skipped part-way", async () =>
            {
                Click("Tools"); await Task.Delay(400);
                if (!Click("Replay Intro")) failures.Add($"{at}: no Replay Intro");
                await Task.Delay(random.Next(300, 7000));
                if (random.Next(2) == 0) Key(Avalonia.Input.Key.Escape); else Click("Skip · Esc");
            });
            await Settle($"{at} intro skipped");
        }
        window.Width = 1600; window.Height = 900; await Task.Delay(300);
        await Step("intro played to the end", async () => { Click("Tools"); await Task.Delay(400); Click("Replay Intro"); await Task.Delay(8600); });
        await Settle("intro played");

        // what each screen costs to build and lay out, in a window of its own: first time, then again
        var bench = new Window { Width = 1600, Height = 900 }; bench.Show();
        var session = new Session(); var studio = session.StudioFor(0)!;
        var build = new List<string>();
        foreach (var (name, make) in new (string, Func<Control>)[]
        {
            ("Zone", () => new ZonePage(session, () => { }, _ => { }, _ => { })),
            ("Chooser", () => new ChooserPage(session, 1, new Border(), _ => { })),
            ("Tools", () => new ToolsPage(new ToolsActions(() => { }, () => { }, () => { }, () => { }, () => { }, () => { }))),
            ("Studio (Dashboard)", () => new StudioView(studio, 0, () => { }, new TextBlock(), null)),
            ("Buttons", () => new ButtonsPage(studio)), ("Curves", () => new CurvesPage(studio)), ("Macros", () => new MacrosPage(studio)),
            ("Vibration", () => new VibrationPage(studio)), ("PC mode", () => new PcModePage(studio, _ => { })), ("Device", () => new DevicePage(studio)),
            ("Setups", () => new SetupsPage(studio)), ("Diagnostics", () => new DiagnosticsPage(studio)),
        })
        {
            var times = new List<double>();
            for (int i = 0; i < 3; i++) { var t = Stopwatch.StartNew(); bench.Content = make(); bench.UpdateLayout(); times.Add(t.Elapsed.TotalMilliseconds); bench.Content = null; }
            build.Add($"  {times[0],6:0} first, {times[1],4:0} / {times[2],4:0} again   {name}");
        }
        bench.Close();
        lines.Add($"{steps} steps at {Sizes.Length} window sizes; failures: {failures.Count}");
        lines.AddRange(failures.Distinct().Select(f => "  FAIL " + f));
        lines.Add("each screen built and laid out, no drawing (ms):"); lines.AddRange(build);
        lines.Add("click to the dispatcher going idle, animations included (ms): median / longest, over n clicks");
        lines.AddRange(handlers.OrderByDescending(h => h.Value.Max()).Select(h => { var v = h.Value.Order().ToList(); return $"  {v[v.Count / 2],6:0} / {v[^1],4:0}  n={v.Count,-3} {h.Key}"; }));
        lines.Add("longest UI stall per step, drawing included (ms; headless Skia draws on the CPU, on this thread):");
        lines.AddRange(stalls.Select(s => $"  {s.Ms,6:0}  {s.Step}"));
        File.WriteAllLines(report, lines);
    }
}
