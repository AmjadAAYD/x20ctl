using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
namespace X20Ctl.Product.Development;

/// <summary>
/// A monkey test for the whole app (--review-stress DIRECTORY). In a sandbox (no browser, no game launch, no file
/// dialogs, setups and assignments in DIRECTORY), it clicks every visible button, tab and menu item on the Zone, the
/// picker, Settings and every Studio page for four different controllers; fires random keys from the one input map;
/// overlaps Zone swaps and page changes; and flips fullscreen / windowed in the middle of transitions. Every
/// exception is recorded with what was being done; report.json lists them, failure.txt exists only if any occurred.
/// </summary>
internal static class StressReview
{
    private static readonly string[] Pages = ["Dashboard", "Buttons", "Curves", "Macros", "Vibration", "Device", "Setups"];
    private static readonly Key[] Keys =
    [
        Key.Left, Key.Right, Key.Up, Key.Down, Key.Enter, Key.Space, Key.Escape, Key.Tab, Key.A, Key.B, Key.X, Key.Y,
        Key.PageUp, Key.PageDown, InputMap.SectionPrevious, InputMap.SectionNext, InputMap.Settings, InputMap.ControllerZone, InputMap.Fullscreen,
        Key.W, Key.S, Key.D, Key.Q, Key.E, Key.F6, Key.Delete, Key.Home, Key.Back, Key.D1, Key.OemPlus, Key.OemMinus
    ];
    private static readonly MethodInfo OnClick = typeof(ButtonBase).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!;

    public static async Task Run(string directory)
    {
        Directory.CreateDirectory(directory);
        ReviewSandbox.Active = true;
        var errors = new List<object>(); var broken = new HashSet<string>(); var visited = new SortedSet<string>(); string context = "launch"; int clicks = 0, menuItems = 0, keys = 0, toggles = 0;
        var rng = new Random(20261010);
        void Record(Exception error) { var inner = error is TargetInvocationException { InnerException: { } i } ? i : error; errors.Add(new { context, type = inner.GetType().Name, inner.Message, stack = inner.StackTrace }); }
        Application.Current.DispatcherUnhandledException += (_, e) => { Record(e.Exception); e.Handled = true; };
        TaskScheduler.UnobservedTaskException += (_, e) => { Record(e.Exception); e.SetObserved(); };
        var window = new ProductWindow(false, Path.Combine(directory, "assignments.json"), openSupport: _ => { });
        bool done = false; window.Closing += (_, e) => { if (!done) { e.Cancel = true; Record(new InvalidOperationException("Something tried to close the window")); } };
        var started = DateTime.Now;
        // each phase runs on its own, so one failure is recorded and the rest of the run still happens
        async Task Phase(string name, Func<Task> body) { try { await body(); } catch (Exception error) { context = name + " (phase aborted)"; Record(error); } }
        try
        {
            window.Show(); await Idle(900);
            if (window.Host.Intro != null) { context = "intro skip"; window.Host.Intro.Finish(); await Idle(600); }

            // 1. the Zone: four different controllers, overlapping swaps, every button
            await Phase("zone", async () =>
            {
                context = "zone assign"; string[] models = ["x20", "x20_pro", "x15", "d10"];
                for (int i = 0; i < 4; i++) window.Scene.AssignController(i, window.Host.Catalog.Get(models[i]));
                await Idle(400);
                context = "zone overlapping swaps";
                for (int n = 0; n < 16; n++) { int p = rng.Next(-1, 4); _ = window.Scene.FocusPlayerAsync(p < 0 ? null : p); await Idle(rng.Next(10, 380)); if (n % 5 == 4) { ToggleWindow(); toggles++; } }
                await Idle(900); Normalize();
                await ClickEverything("zone", () => { Normalize(); return Task.CompletedTask; });
            });

            // 2. the picker and Settings
            await Phase("picker", async () =>
            {
                context = "picker"; window.Host.OpenSelector(1); await Idle(500);
                await ClickEverything("picker", () => { if (window.Host.Selector == null) window.Host.OpenSelector(1); return Task.CompletedTask; }, max: 12);
                window.Host.CloseSelector(); window.Scene.AssignController(1, window.Host.Catalog.Get("x20_pro")); await Idle(300);
            });
            await Phase("settings", async () =>
            {
                context = "settings"; window.Host.ShowSettings(); await Idle(500);
                await ClickEverything("settings", () => { if (window.Host.Intro != null) window.Host.Intro.Finish(); if (window.Host.Settings == null) window.Host.ShowSettings(); return Task.CompletedTask; });
                window.Host.CloseSettings(); await Idle(300);
            });

            // 3. every Studio page for each controller, every button and menu item on it
            for (int player = 0; player < 4; player++)
            {
                int who = player; string model = window.Scene.State.Players[player].Model ?? "?";
                foreach (string page in Pages)
                    await Phase($"{model} {page}", async () =>
                    {
                        context = $"enter {model} {page}"; await Enter(who, page);
                        await ClickEverything($"{model} {page}", () => Enter(who, page));
                        if (who == 0 && page == "Macros")
                        {   // fill a sequence to the limit, then run its editor through every tab (always the live page's workbench)
                            MacroWorkbench Live() => window.Host.Studio!.Macros!;
                            context = "macros fill"; await Enter(who, page); for (int i = 0; i < 50; i++) Live().Add(); Live().Select(0); await Idle(200);
                            await ClickEverything($"{model} Macros (full)", () => Enter(who, page));
                            context = "macros preview"; await Enter(who, page); for (int i = 0; i < 6; i++) Live().Add(); Live().Preview(); await Idle(400); ToggleWindow(); toggles++; await Idle(400); Live().StopPreview(); Live().Clear();
                        }
                    });
            }

            // 4. random keys from the input map, from wherever the app happens to be
            await Phase("keys", async () =>
            {
                for (int n = 0; n < 600; n++)
                {
                    if (n % 60 == 0) { int p = rng.Next(0, 4); context = $"keys reset to player {p}"; await Enter(p, Pages[rng.Next(Pages.Length)]); }
                    var key = Keys[rng.Next(Keys.Length)];
                    context = $"key {key} on {Where()}";
                    try { Press(key); keys++; } catch (Exception error) { Record(error); }
                    // now and then a mouse press anywhere: the Studio drops controller-navigation mode on it (the path of the 10 Oct crash)
                    if (rng.Next(8) == 0) { context = $"mouse down on {Where()}"; try { (Keyboard.FocusedElement as UIElement ?? window).RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseDownEvent }); } catch (Exception error) { Record(error); } }
                    if (key == InputMap.Fullscreen) toggles++;
                    await Idle(rng.Next(5, 60));
                    if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Maximized;
                }
                await Idle(600);
            });

            // 5. page changes and Zone round trips overlapping their own animations
            await Phase("rapid", async () =>
            {
                for (int n = 0; n < 40; n++)
                {
                    context = $"rapid {n}"; int p = rng.Next(0, 4);
                    if (rng.Next(3) == 0) { window.Host.ReturnToZone(); _ = window.Scene.FocusPlayerAsync(p); }
                    else await Enter(p, Pages[rng.Next(Pages.Length)], settle: false);
                    if (rng.Next(6) == 0) { ToggleWindow(); toggles++; }
                    await Idle(rng.Next(0, 90));
                    if (n % 8 == 7) { await Idle(700); Shot($"rapid-{n:00}-{Where().Replace(" ", "-")}"); }
                }
                await Idle(1200); Check("end"); Shot("end");
            });
        }
        catch (Exception error) { Record(error); }
        finally
        {
            done = true;
            var report = new { seconds = (DateTime.Now - started).TotalSeconds, clicks, menuItems, keys, windowToggles = toggles, controls = visited.Count, errors };
            File.WriteAllText(Path.Combine(directory, "report.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            File.WriteAllLines(Path.Combine(directory, "controls.txt"), visited);
            if (errors.Count > 0) File.WriteAllText(Path.Combine(directory, "failure.txt"), JsonSerializer.Serialize(errors, new JsonSerializerOptions { WriteIndented = true }));
            ReviewSandbox.Active = false; window.Close();
        }

        // ----- helpers -----
        void Shot(string name)
        {
            window.UpdateLayout(); if (window.Stage.ActualWidth < 1) return;
            var bmp = new System.Windows.Media.Imaging.RenderTargetBitmap((int)window.Stage.ActualWidth, (int)window.Stage.ActualHeight, 96, 96, PixelFormats.Pbgra32); bmp.Render(window.Stage);
            var enc = new System.Windows.Media.Imaging.PngBitmapEncoder(); enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bmp));
            using var file = File.Create(Path.Combine(directory, string.Concat(name.Split(Path.GetInvalidFileNameChars())) + ".png")); enc.Save(file);
        }
        async Task Idle(int ms)
        {
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            if (ms > 0) await Task.Delay(ms);
        }
        void Normalize()
        {
            if (window.Host.Intro != null) window.Host.Intro.Finish();
            window.Host.CloseSettings(); window.Host.CloseSelector();
            if (window.Host.Studio != null) window.Host.ReturnToZone();
            if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Maximized;
        }
        async Task Enter(int player, string page, bool settle = true)
        {
            bool changed = window.Host.Intro != null || window.Host.Settings != null || window.Host.Selector != null;
            if (window.Host.Intro != null) window.Host.Intro.Finish();
            window.Host.CloseSettings(); window.Host.CloseSelector();
            if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Maximized;
            if (window.Host.Studio == null || window.Host.Studio.Player != player) { window.Host.ReturnToZone(); window.Host.EnterStudio(player); changed = true; }
            var s = window.Host.Studio!;
            if (s.CurrentPage != page)
            {
                changed = true;
                if (page is "Dashboard" or "Device" or "Setups") s.ShowManagement(page);
                else if (page == "Buttons") s.ShowButtons();
                else if (page == "Curves") s.ShowCurves(true);
                else if (page == "Macros") s.ShowMacros(true);
                else s.ShowVibration(true);
            }
            if (settle && changed) await Idle(260);
            if (settle) Check($"after entering {page}");
        }
        // what must always be true when the app is at rest: the right page showing, its tab lit, nothing stuck
        void Check(string when)
        {
            var problems = new List<string>();
            var h = window.Host;
            if (h.Selector == null && h.Settings == null && h.Intro == null && h.Studio == null && !h.Zone.IsEnabled) problems.Add("Zone left disabled with nothing over it");
            if (h.Studio is { } s && h.Selector == null && h.Settings == null)
            {
                string page = s.CurrentPage;
                var tabs = new (string Page, RadioButton Tab)[] { ("Dashboard", s.DashboardNav), ("Buttons", s.ButtonsNav), ("Curves", s.CurvesNav), ("Macros", s.MacrosNav), ("Vibration", s.VibrationNav), ("Device", s.DeviceNav), ("Setups", s.SetupsNav) };
                var lit = tabs.Where(t => t.Tab.IsChecked == true).Select(t => t.Page).ToList();
                if (lit.Count != 1 || lit[0] != page) problems.Add($"tab lit [{string.Join(",", lit)}] but page is {page}");
                bool Shown(FrameworkElement e) => e.Visibility == Visibility.Visible;
                var hosts = new (string Name, bool On)[] { ("buttons", Shown(s.ButtonsPageHost)), ("classic", Shown(s.HardwareStage)), ("macros", Shown(s.MacroBodyHost)), ("vibration", Shown(s.VibrationBodyHost)), ("management", Shown(s.ManagementBodyHost)) };
                string want = page switch { "Buttons" => "buttons", "Curves" => "classic", "Macros" => "macros", "Vibration" => "vibration", _ => "management" };
                var on = hosts.Where(x => x.On).Select(x => x.Name).ToList();
                if (on.Count != 1 || on[0] != want) problems.Add($"page {page} shows [{string.Join(",", on)}]");
                if (page == "Curves" && !Shown(s.CurveInspectorHost)) problems.Add("Curves without its graph");
                if (page == "Macros" && Grid.GetRowSpan(s.StudioBody) != 2) problems.Add("Macros not spanning the shelf row");
                if (page is "Buttons" or "Curves" or "Vibration" or "Device" && Grid.GetRowSpan(s.StudioBody) != 1) problems.Add($"{page} spanning the shelf row");
            }
            foreach (var p in problems) if (broken.Add($"{when}: {p}")) errors.Add(new { context = $"{when} on {Where()}", type = "Invariant", Message = p, stack = "" });
        }
        string Where() => window.Host.Intro != null ? "intro" : window.Host.Settings != null ? "settings" : window.Host.Selector != null ? "picker" : window.Host.Studio is { } s ? $"{s.Model.Id} {s.CurrentPage}" : "zone";
        void ToggleWindow() => window.WindowState = window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        void Press(Key key)
        {
            var source = PresentationSource.FromVisual(window); if (source == null) return;
            var target = Keyboard.FocusedElement as IInputElement ?? window;
            // as Windows routes a real press: the bubbling event only runs when the tunnelling one was not handled
            foreach (var (preview, bubble) in new[] { (Keyboard.PreviewKeyDownEvent, Keyboard.KeyDownEvent), (Keyboard.PreviewKeyUpEvent, Keyboard.KeyUpEvent) })
            {
                var first = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key) { RoutedEvent = preview }; target.RaiseEvent(first);
                if (!first.Handled) target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key) { RoutedEvent = bubble });
                target = Keyboard.FocusedElement as IInputElement ?? window;
            }
        }
        // controls are found again before every click (a click may rebuild the page), matched by name and order
        List<(string Key, ButtonBase Button)> Keyed()
        {
            var list = new List<ButtonBase>(); Collect(window, list); var seen = new Dictionary<string, int>(); var result = new List<(string, ButtonBase)>();
            foreach (var b in list) { if (!b.IsVisible) continue; string d = Describe(b); int n = seen.GetValueOrDefault(d); seen[d] = n + 1; result.Add(($"{d}#{n}", b)); }
            return result;
        }
        async Task ClickEverything(string where, Func<Task> restore, int max = 400)
        {
            var order = Keyed().Select(k => k.Key).ToList();
            int count = 0;
            foreach (var key in order)
            {
                if (count++ >= max) break;
                var b = Keyed().FirstOrDefault(k => k.Key == key).Button;
                if (b == null || !b.IsVisible || !b.IsEnabled || PresentationSource.FromVisual(b) == null) continue;
                string name = Describe(b);
                if (name.Contains("Close", StringComparison.OrdinalIgnoreCase) && b.Parent is not MenuItem || name.Contains("Minimi", StringComparison.OrdinalIgnoreCase)) continue;
                visited.Add($"{where} · {name}");
                context = $"{where} · click {name}";
                try { if (b.Focusable) b.Focus(); OnClick.Invoke(b, null); clicks++; } catch (Exception error) { Record(error); }
                await Idle(rng.Next(10, 70));
                // a click that opened a menu: choose every item in it, one after another
                foreach (var menu in OpenMenus())
                {
                    var items = menu.Items.OfType<MenuItem>().Where(i => i.IsEnabled && i.IsVisible).ToList();
                    foreach (var item in items)
                    {
                        context = $"{where} · menu {item.Header}"; visited.Add($"{where} · menu {item.Header}");
                        try { menu.IsOpen = true; await Idle(20); typeof(MenuItem).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(item, null); menuItems++; } catch (Exception error) { Record(error); }
                        await Idle(30);
                    }
                    menu.IsOpen = false;
                }
                if (window.Host.Intro != null) window.Host.Intro.Finish();
                if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Maximized;
                await Idle(0); Check($"{where} · after {name}");
                try { await restore(); } catch (Exception error) { Record(error); }
            }
            await Fuzz(where);
        }
        // sliders to random values, text fields to junk and back, curve points anywhere (out of range included)
        async Task Fuzz(string where)
        {
            var sliders = new List<Slider>(); var boxes = new List<TextBox>(); Gather(window, sliders, boxes);
            foreach (var slider in sliders.Where(s => s.IsVisible && s.IsEnabled))
            {
                context = $"{where} · slider {System.Windows.Automation.AutomationProperties.GetName(slider)}";
                try { for (int i = 0; i < 6; i++) { slider.Value = slider.Minimum + rng.NextDouble() * (slider.Maximum - slider.Minimum); await Idle(5); } slider.Value = slider.Minimum; slider.Value = slider.Maximum; } catch (Exception error) { Record(error); }
            }
            foreach (var box in boxes.Where(b => b.IsVisible && b.IsEnabled))
            {
                foreach (string text in new[] { "", "abc", "-5", "7", "40", "999999999", "12.5", " 35 ", "0" })
                {
                    context = $"{where} · text {System.Windows.Automation.AutomationProperties.GetName(box)} = \"{text}\"";
                    try { box.Focus(); box.Text = text; Keyboard.ClearFocus(); window.Focus(); await Idle(5); } catch (Exception error) { Record(error); }
                }
            }
            if (window.Host.Studio is { IsCurves: true } s)
                for (int i = 0; i < 40; i++) { context = $"{where} · curve point"; try { s.Curves.SetPoint(rng.Next(2), rng.Next(-40, 300), rng.Next(-40, 300)); } catch (Exception error) { Record(error); } }
            await Idle(50);
        }
        IEnumerable<ContextMenu> OpenMenus()
        {
            var menus = new List<ContextMenu>(); var list = new List<ButtonBase>();
            void Walk(DependencyObject d) { if (d is FrameworkElement { ContextMenu: { IsOpen: true } m }) menus.Add(m); for (int i = 0; i < VisualTreeHelper.GetChildrenCount(d); i++) Walk(VisualTreeHelper.GetChild(d, i)); }
            Walk(window); return menus.Distinct().ToList();
        }
    }
    private static void Gather(DependencyObject root, List<Slider> sliders, List<TextBox> boxes)
    {
        if (root is Slider s) sliders.Add(s); else if (root is TextBox t) boxes.Add(t);
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) Gather(VisualTreeHelper.GetChild(root, i), sliders, boxes);
    }
    private static void Collect(DependencyObject root, List<ButtonBase> into)
    {
        if (root is ButtonBase b && b is not RepeatButton && !into.Contains(b)) into.Add(b);
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) Collect(VisualTreeHelper.GetChild(root, i), into);
    }
    private static string Describe(ButtonBase b)
    {
        string? name = System.Windows.Automation.AutomationProperties.GetName(b);
        if (string.IsNullOrWhiteSpace(name)) name = b.Content as string;
        if (string.IsNullOrWhiteSpace(name)) name = b.ToolTip as string;
        if (string.IsNullOrWhiteSpace(name)) name = FirstText(b);
        if (string.IsNullOrWhiteSpace(name) && b.Tag != null) name = b.Tag.ToString();
        return $"{b.GetType().Name} \"{(name ?? "?").Replace('\n', ' ')}\"";
    }
    private static string? FirstText(DependencyObject d)
    {
        if (d is TextBlock { Text.Length: > 0 } t && t.Text.Any(char.IsLetterOrDigit)) return t.Text;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(d); i++) if (FirstText(VisualTreeHelper.GetChild(d, i)) is { } s) return s;
        return null;
    }
}
