using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using X20Ctl.Zone;
namespace X20Ctl.Product.Development;

/// <summary>The Mouse and Keyboard pages laid out off screen and saved as pictures, top to bottom. No window is shown and
/// nothing talks to a controller (owner, 20 Oct 2026: never open the app on his screen).</summary>
internal static class InputsReview
{
    public static async Task Run(string directory)
    {
        Directory.CreateDirectory(directory);
        ReviewSandbox.Active = true; ReviewSandbox.HideLibrary = true; // PC mode's switch then never starts the service
        var report = new List<string>();
        var window = new ProductWindow(true, Path.Combine(directory, "assignments-fixture.json"));
        window.Scene.AssignController(0, window.Host.Catalog.Get("x20_pro")); window.Host.EnterStudio(0);
        var studio = window.Host.Studio!;
        var root = (FrameworkElement)window.Content;
        async Task Shoot(string name)
        {
            root.Measure(new Size(1920, 1150)); root.Arrange(new Rect(0, 0, 1920, 1150)); root.UpdateLayout(); await Task.Delay(700); root.UpdateLayout();
            var viewer = Find<ScrollViewer>(studio.ManagementBodyHostForReview); viewer?.ScrollToTop(); root.UpdateLayout();
            for (int i = 0; ; i++)
            {
                var shot = new RenderTargetBitmap(1920, 1150, 96, 96, PixelFormats.Pbgra32); shot.Render(root);
                var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(shot));
                using (var file = File.Create(Path.Combine(directory, $"{name}-{i}.png"))) png.Save(file);
                if (viewer == null || viewer.VerticalOffset >= viewer.ScrollableHeight - 1 || i >= 4) break;
                viewer.ScrollToVerticalOffset(viewer.VerticalOffset + viewer.ViewportHeight * .85); root.UpdateLayout(); await Task.Delay(200); root.UpdateLayout();
            }
        }
        report.Add("tabs, PC mode off: " + string.Join(", ", studio.TabOrderForReview));
        studio.ShowManagement("Mouse"); await Shoot("mouse");
        if (studio.Mouse is { } mouse) { mouse.Select("X"); await Shoot("mouse-x"); }

        // PC mode on (a fixture file; the sandbox keeps the service from starting): Keyboard replaces Macros
        var link = studio.PcMode!;
        var sample = new X20Ctl.Zone.PcMode.PcModeProfile();
        sample.Keys["X"] = "K:87"; sample.Keys["LB"] = "K:160"; sample.Keys["RB"] = "M:Back"; sample.Keys["DPAD_UP"] = "K:49";
        sample.Custom["Y"] = [new() { Output = "K:81", HoldMs = 50, PauseMs = 30 }, new() { Output = "K:69", HoldMs = 50, PauseMs = 0 }];
        link.Store.Save(PcModePage.StoreKey, sample);
        link.Service.Enabled = true;
        await window.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
        var navigationFailures = new List<string>();
        studio = window.Host.Studio!;
        if (!studio.Draft.PcMode || !studio.TabOrderForReview.Contains("Keyboard"))
        {
            navigationFailures.Add("Turning on PC Mode left the existing Studio in native mode.");
            window.Host.EnterStudio(0); studio = window.Host.Studio!;
        }
        var strip = (FrameworkElement)studio.FindName("NavigationLayer");
        strip.Width = 890;
        root.Measure(new Size(1920, 1150)); root.Arrange(new Rect(0, 0, 1920, 1150)); root.UpdateLayout();
        await window.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
        var mouseTab = (RadioButton)studio.FindName("MouseNav");
        if (studio.KeyboardNav?.Visibility != Visibility.Visible || mouseTab.Visibility != Visibility.Visible)
            navigationFailures.Add("The compact strip hid Keyboard or Mouse.");
        var more = (Button)studio.FindName("NavigationMore");
        var track = (FrameworkElement)studio.FindName("NavigationTrack");
        more.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        if (more.Visibility != Visibility.Visible || more.DesiredSize.Height > track.ActualHeight)
            navigationFailures.Add("The overflow button does not fit inside the tab strip.");
        if (navigationFailures.Count > 0) throw new InvalidOperationException(string.Join(" ", navigationFailures));
        studio.GoToTab("Keyboard");
        if (studio.KeyboardPage == null) throw new InvalidOperationException("The Keyboard tab did not open its editor.");
        studio.StepTab(1);
        if (studio.ManagementPage != "Mouse" || studio.InputContext != InputOwner.UiNavigation)
            throw new InvalidOperationException("Idle Mouse settings blocked tab navigation.");
        studio.StepTab(-1);
        if (studio.ManagementPage != "Keyboard") throw new InvalidOperationException("Could not return from Mouse to Keyboard.");
        await Shoot("pc-tabs-compact");
        strip.Width = double.NaN;
        report.Add("tabs, PC mode on: " + string.Join(", ", studio.TabOrderForReview));
        studio.ShowManagement("Keyboard"); await Shoot("keyboard");
        if (studio.KeyboardPage is { } keys) { keys.Select("Y"); await Shoot("keyboard-custom"); }
        studio.ShowManagement("Mouse"); await Shoot("mouse-pc");
        window.Host.MouseMode!.TakeControl(0, "x20_pro", preview:true);
        if (studio.InputContext != InputOwner.TesterCapture) throw new InvalidOperationException("Mouse testing did not capture input.");
        window.Host.MouseMode.Active = window.Host.MouseMode.Previewing = false;
        link.Service.Enabled = false;
        await window.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
        studio = window.Host.Studio!;
        if (studio.Draft.PcMode || studio.TabOrderForReview.Contains("Keyboard")) throw new InvalidOperationException("Turning PC Mode off left its Keyboard editor active.");
        report.Add("tabs, PC mode off again: " + string.Join(", ", studio.TabOrderForReview));

        // leaving and coming back shows the same settings and tab
        studio.Draft.Set("A", "B"); studio.GoToTab("Curves"); window.Host.ReturnToZone(); window.Host.EnterStudio(0); studio = window.Host.Studio!;
        report.Add($"re-entered: A -> {studio.Draft.Target("A")}, page {studio.CurrentPage}");

        // the controller cards, then the scan's verdicts
        window.Host.ReturnToZone(); window.Host.OpenSelector(1);
        report.Add("card lines: " + string.Join(" | ", window.Host.Selector!.Cards.Select(c => Texts(c).LastOrDefault() ?? "?")));
        root.Measure(new Size(1920, 1150)); root.Arrange(new Rect(0, 0, 1920, 1150)); root.UpdateLayout(); await Task.Delay(900); root.UpdateLayout();
        Save(root, Path.Combine(directory, "selector.png"));
        foreach (var outcome in new[] { VerifyOutcome.Match, VerifyOutcome.Unconfirmed })
        {
            var view = new ControllerVerifyView("x10", "X10", false);
            view.ShowResult(new(outcome, outcome == VerifyOutcome.Match ? "x10" : null, outcome == VerifyOutcome.Match ? "EasySMX X10" : null, "", [new("EasySMX X10", outcome == VerifyOutcome.Match ? "x10" : null, "HID · 045E:028E")], "USB"), "X10", null, () => { }, _ => { }, () => { });
            view.Measure(new Size(1600, 700)); view.Arrange(new Rect(0, 0, 1600, 700)); view.UpdateLayout();
            Save(view, Path.Combine(directory, $"verify-{outcome}.png"));
            report.Add($"verify {outcome}: " + string.Join(" / ", Texts(view).Where(t => t.Contains("Studio") || t.Contains("found") || t.Contains("·"))));
        }
        // a Dune: with PC mode off its tabs point to PC mode; with it on, Vibration sets how strongly games vibrate it
        window.Host.CloseSelector(); window.Scene.AssignController(2, window.Host.Catalog.Get("dune")); window.Host.EnterStudio(2); studio = window.Host.Studio!;
        studio.ShowManagement("Dashboard"); studio.GoToTab("Buttons"); await Shoot("dune-buttons-off");
        report.Add("dune, PC mode off: tabs " + string.Join(", ", studio.TabOrderForReview) + " · " + studio.ChangeCountForReview);
        link.Service.Enabled = true; window.Host.EnterStudio(2); studio = window.Host.Studio!;
        studio.ShowVibration(true); studio.Vibration!.Set(40); await Shoot("dune-vibration-pc");
        report.Add("dune vibration, PC mode on: " + string.Join(" / ", Texts(studio).Where(t => t.Contains("PC mode") || t.Contains("games vibrate"))));
        // an X10: PC mode on, and its Device tab reads its own settings over Bluetooth (read only)
        window.Scene.AssignController(3, window.Host.Catalog.Get("x10")); window.Host.EnterStudio(3); studio = window.Host.Studio!;
        studio.ShowManagement("Device"); await Shoot("x10-device-pc");
        report.Add("x10 device, PC mode on: tabs " + string.Join(", ", studio.TabOrderForReview) + " · " + string.Join(" / ", Texts(studio).Where(t => t.Contains("X10") || t.Contains("controller"))));
        link.Service.Enabled = false;
        // an X20 Pro with PC mode off: four zones, no main slider; settings stay on the controller
        window.Scene.AssignController(1, window.Host.Catalog.Get("x20_pro")); window.Host.EnterStudio(1); studio = window.Host.Studio!;
        studio.ShowVibration(true); await Shoot("x20pro-vibration-off");
        report.Add("x20 pro vibration, PC mode off: " + string.Join(" / ", Texts(studio).Where(t => t.Contains("ZONES") || t.Contains("grip") || t.Contains("trigger") || t.Contains("Test"))));
        foreach (var theme in ThemeCatalog.All.Skip(1))
        {
            ThemeCatalog.Apply(theme);
            var host = new ProductHost(new ZoneScene(true), Path.Combine(directory, theme.Id + "-assignments.json"));
            host.Zone.AssignController(0, host.Catalog.Get("x20_pro"));
            host.PcMode!.Enabled = true;
            host.EnterStudio(0);
            var themed = host.Studio!;
            var stage = new DesignStage { Child = host };
            using var source = new System.Windows.Interop.HwndSource(new System.Windows.Interop.HwndSourceParameters("Themed input review")
            {
                WindowStyle = unchecked((int)0x80000000), Width = 1600, Height = 900,
                PositionX = -32000, PositionY = -32000,
            }) { RootVisual = stage };
            themed.GoToTab("Keyboard");
            await themed.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
            stage.Measure(new Size(1600, 900)); stage.Arrange(new Rect(0, 0, 1600, 900)); stage.UpdateLayout();
            if (themed.KeyboardPage?.Body.IsVisible != true || themed.KeyboardNav?.Visibility != Visibility.Visible ||
                ((RadioButton)themed.FindName("MouseNav")).Visibility != Visibility.Visible ||
                Find<HeaderBattery>(themed)?.Visibility != Visibility.Collapsed)
                throw new InvalidOperationException(theme.Id + ": Keyboard, Mouse or battery visibility is incorrect.");
            Save(stage, Path.Combine(directory, theme.Id + "-keyboard.png"));
            themed.GoToTab("Vibration");
            await themed.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
            stage.UpdateLayout();
            if (themed.Vibration?.ZoneButton(VibrationWorkbench.Zone.RightTrigger)?.IsVisible != true)
                throw new InvalidOperationException(theme.Id + ": Vibration did not show its trigger motor tiles.");
            report.Add(theme.Name + ": Keyboard and Mouse tabs, hidden battery and motor tiles verified.");
        }
        File.WriteAllLines(Path.Combine(directory, "inputs.txt"), report);
    }

    static void Save(FrameworkElement element, string file)
    {
        var shot = new RenderTargetBitmap((int)element.ActualWidth, (int)element.ActualHeight, 96, 96, PixelFormats.Pbgra32); shot.Render(element);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(shot)); using var stream = File.Create(file); png.Save(stream);
    }
    static IEnumerable<string> Texts(DependencyObject node)
    {
        if (node is TextBlock t && !string.IsNullOrWhiteSpace(t.Text)) yield return t.Text;
        if (node is ContentControl { Content: string s }) yield return s;
        int n = node is Visual ? VisualTreeHelper.GetChildrenCount(node) : 0;
        if (n == 0 && node is Decorator { Child: { } only }) foreach (var x in Texts(only)) yield return x;
        if (n == 0 && node is Panel panel) foreach (UIElement child in panel.Children) foreach (var x in Texts(child)) yield return x;
        if (n == 0 && node is ContentControl { Content: DependencyObject inner }) foreach (var x in Texts(inner)) yield return x;
        for (int i = 0; i < n; i++) foreach (var x in Texts(VisualTreeHelper.GetChild(node, i))) yield return x;
    }

    static T? Find<T>(DependencyObject? node) where T : DependencyObject
    {
        if (node == null) return null;
        if (node is T hit) return hit;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) if (Find<T>(VisualTreeHelper.GetChild(node, i)) is { } found) return found;
        return null;
    }
}
