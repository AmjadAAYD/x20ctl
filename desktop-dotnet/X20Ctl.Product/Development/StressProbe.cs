using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace X20Ctl.Product.Development;

/// <summary>Scratch probe for one finding at a time (--review-probe DIRECTORY).</summary>
internal static class StressProbe
{
    public static async Task Run(string directory)
    {
        Directory.CreateDirectory(directory); var log = new List<string>(); ReviewSandbox.Active = true;
        var window = new ProductWindow(false, null);
        try
        {
            window.Show(); window.WindowState = WindowState.Maximized; await Task.Delay(600); window.Host.Intro?.Finish(); await Task.Delay(500);
            log.Add("assigned at launch: " + window.Scene.State.Players.Count(p => p.Model != null)); Shot("1-zone-empty");
            window.Scene.AssignController(0, window.Host.Catalog.Get("x20")); window.Scene.AssignController(1, window.Host.Catalog.Get("x15")); window.Scene.AssignController(2, window.Host.Catalog.Get("x05_pro"));
            await window.Scene.FocusPlayerAsync(null); await Task.Delay(700); Shot("2-zone-grid");
            await window.Scene.FocusPlayerAsync(0); await Task.Delay(700); Shot("3-zone-hero");
            window.Host.EnterStudio(0); await Task.Delay(150); Shot("4-studio-entering"); await Task.Delay(900); Shot("5-dashboard");
            var s = window.Host.Studio!; s.ShowManagement("Device"); await Task.Delay(700); Shot("6-device");
            s.ShowVibration(true); s.Vibration!.Set(100); await Task.Delay(700); Shot("7-vibration-a"); await Task.Delay(330); Shot("7-vibration-b");
            s.ShowMacros(true); await Task.Delay(700); Shot("8-macros");
            window.Host.ReturnToZone(); await Task.Delay(120); Shot("9-zone-returning"); await Task.Delay(700);
            window.Host.ShowSettings(); await Task.Delay(700); window.Host.OpenCheck(); await Task.Delay(800); Shot("10-check-warning");
            var linuxChoice = Find(window.Stage, "A Linux PC"); if (linuxChoice != null) { linuxChoice.IsChecked = true; await Task.Delay(400); Shot("11-check-linux"); } else log.Add("no linux choice");
            window.Host.CloseCheck(); await Task.Delay(500); Shot("12-tools");
            // the owner's report: Back to Tools in the Read scan's title bar does nothing
            window.Host.OpenCheck(); await Task.Delay(800);
            ReviewSandbox.Active = false; Click(window.Stage, "Scan, keep it on this PC", log); await Task.Delay(6000);
            log.Add("warning visible: " + window.Host.Check!.Children.OfType<System.Windows.Controls.Border>().Select(x => x.Visibility.ToString()).Aggregate((x, y) => x + "," + y)); Click(window.Stage, "Start check", log); await Task.Delay(1500); Shot("12a-check-live");
            var titleBack = Buttons(window.Host.Check!, "Back to Tools").ToList(); log.Add("back buttons in check: " + titleBack.Count + " visible: " + string.Join(",", titleBack.Select(b => b.IsVisible + "/" + b.IsEnabled)));
            if (titleBack.Count > 0)
            {
                var tb = titleBack[0]; var centre = tb.TranslatePoint(new Point(tb.ActualWidth / 2, tb.ActualHeight / 2), window.Stage);
                var hit = window.Stage.InputHitTest(centre) as DependencyObject; var chain = new List<string>();
                for (var d = hit; d != null && chain.Count < 14; d = VisualTreeHelper.GetParent(d)) chain.Add(d.GetType().Name + (d is FrameworkElement fe && fe.Name.Length > 0 ? "#" + fe.Name : "") + (d is UIElement ue ? $"(z{System.Windows.Controls.Panel.GetZIndex(ue)})" : ""));
                log.Add("hit at back centre " + centre + ": " + string.Join(" < ", chain));
                bool isBack = false; for (var d = hit; d != null; d = VisualTreeHelper.GetParent(d)) if (d == tb) isBack = true;
                log.Add("hit reaches the back button: " + isBack);
            }
            foreach (var b in titleBack) { b.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)); break; }
            await Task.Delay(800); log.Add("after back: Check is " + (window.Host.Check == null ? "null" : "still open")); ReviewSandbox.Active = true; if (window.Host.Check != null) log.Add("outcome: " + string.Join(" / ", Texts(window.Host.Check).Where(t => t.Contains("Back") || t.Contains("ave") || t.Contains("Scan")).Distinct().Take(12))); Shot("12b-after-back");
            if (window.Host.Check != null) { window.Host.CloseCheck(); await Task.Delay(800); log.Add("after second back: " + (window.Host.Check == null ? "null" : "still open")); }
            window.Host.OpenWriteScan(); await Task.Delay(800); Shot("13-write-warning");
            var w = window.Host.WriteScan!; var plan = new X20Ctl.Zone.Capture.TestPlan("X20", "KeyLinker", "Left motor strength", "50", "80");
            w.ReviewOpen(0, plan); await Task.Delay(400); Shot("14-write-plan");
            w.ReviewOpen(1); await Task.Delay(400); Shot("15-write-phone");
            w.ReviewOpen(2); await Task.Delay(400); Shot("16-write-test");
            log.Add("guided test ends on step " + await w.ReviewWalkTest() + " (3 is the recording)");
            w.ReviewOpen(3); await Task.Delay(400); w.ReviewOpen(2); w.ReviewOpen(3); await Task.Delay(400); Shot("17-write-recording");
            var sample = Path.Combine(directory, "write-sample.btsnoop");
            if (File.Exists(sample)) { await w.ReviewRead(sample); await Task.Delay(600); Shot("18-write-results"); await w.ReviewSend(); await Task.Delay(400); Shot("19-write-sent"); await w.ReviewRead(sample); await Task.Delay(400); log.Add("results shown twice without a crash"); } else log.Add("no sample recording");
            // a Filtered log: nothing learned, and the way back to setting up the phone
            var filtered = Path.Combine(directory, "write-filtered.btsnoop");
            if (File.Exists(filtered)) { await w.ReviewRead(filtered); await Task.Delay(600); Shot("18b-write-filtered"); }
            w.ReviewPhone(true); w.ReviewOpen(1); await Task.Delay(400); Shot("20-write-iphone-phone");
            w.ReviewOpen(3); await Task.Delay(400); Shot("21-write-iphone-recording");
            // the step-by-step guides: every Samsung step, the help panel, and the other phones' odd ones out
            w.ReviewPhone(X20Ctl.Product.Scanner.PhoneKind.Samsung);
            for (int i = 0; i < X20Ctl.Product.Scanner.PhoneGuides.Prepare(X20Ctl.Product.Scanner.PhoneKind.Samsung).Length; i++) { w.ReviewGuide(i, 0); w.ReviewOpen(1); await Task.Delay(300); Shot($"30-samsung-prepare-{i + 1}"); }
            w.ReviewGuide(1, 0); w.ReviewOpen(1); w.ReviewStuck(); await Task.Delay(300); Shot("31-samsung-prepare-2-stuck");
            for (int i = 0; i < X20Ctl.Product.Scanner.PhoneGuides.Bring(X20Ctl.Product.Scanner.PhoneKind.Samsung).Length; i++) { w.ReviewGuide(0, i); w.ReviewOpen(3); await Task.Delay(300); Shot($"32-samsung-bring-{i + 1}"); }
            w.ReviewPhone(X20Ctl.Product.Scanner.PhoneKind.IPhone); w.ReviewGuide(0, 0); w.ReviewOpen(3); await Task.Delay(300); Shot("33-iphone-bring-1");
            w.ReviewPhone(X20Ctl.Product.Scanner.PhoneKind.Android); w.ReviewGuide(2, 0); w.ReviewOpen(1); await Task.Delay(300); Shot("34-android-prepare-3");
            w.ReviewOpen(2); await Task.Delay(300); Shot("35-test");
            log.Add("guides drawn");
            // a bad file dropped on the recording guide's first step: its error must be on screen
            var junk = Path.Combine(directory, "not-a-recording.bin"); File.WriteAllBytes(junk, new byte[64]);
            w.ReviewPhone(X20Ctl.Product.Scanner.PhoneKind.Samsung); w.ReviewGuide(0, 0); w.ReviewOpen(3); await Task.Delay(300);
            await w.ReviewRead(junk); await Task.Delay(300); window.UpdateLayout();
            var st = w.ReviewStatus(); log.Add($"bad drop on step 1: guide at {st.Guide}, status shown {st.Shown}: {st.Text}"); Shot("36-bad-drop");
            // a second test starts the recording guide from its first step again
            await w.ReviewWalkTest(); await Task.Delay(300); log.Add($"after a new test the recording guide is at step {w.ReviewStatus().Guide + 1}, status '{w.ReviewStatus().Text}'");
        }
        catch (Exception e) { log.Add(e.ToString()); }
        finally { File.WriteAllLines(Path.Combine(directory, "probe.txt"), log); ReviewSandbox.Active = false; window.Close(); }
        void Shot(string name)
        {
            window.UpdateLayout(); var bmp = new RenderTargetBitmap((int)window.Stage.ActualWidth, (int)window.Stage.ActualHeight, 96, 96, PixelFormats.Pbgra32); bmp.Render(window.Stage);
            var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(bmp)); using var f = File.Create(Path.Combine(directory, name + ".png")); enc.Save(f);
        }
    }
    private static IEnumerable<System.Windows.Controls.Button> Buttons(DependencyObject root, string text)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var c = VisualTreeHelper.GetChild(root, i);
            if (c is System.Windows.Controls.Button b && b.Content is System.Windows.Controls.StackPanel sp && sp.Children.OfType<System.Windows.Controls.TextBlock>().Any(t => t.Text == text)) yield return b;
            foreach (var f in Buttons(c, text)) yield return f;
        }
    }
    private static IEnumerable<string> Texts(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) { var c = VisualTreeHelper.GetChild(root, i); if (c is System.Windows.Controls.TextBlock t && t.IsVisible && t.Text.Length > 0) yield return t.Text; foreach (var x in Texts(c)) yield return x; }
    }
    private static void Click(DependencyObject root, string text, List<string> log)
    {
        var b = Buttons(root, text).FirstOrDefault();
        if (b == null) { log.Add("no visible button: " + text); return; }
        b.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
    }
    private static System.Windows.Controls.RadioButton? Find(DependencyObject root, string content)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) { var c = VisualTreeHelper.GetChild(root, i); if (c is System.Windows.Controls.RadioButton r && Equals(r.Content, content)) return r; if (Find(c, content) is { } f) return f; }
        return null;
    }
}