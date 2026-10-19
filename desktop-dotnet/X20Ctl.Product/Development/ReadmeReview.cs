using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace X20Ctl.Product.Development;

/// <summary>The repository README's screenshots (--review-readme DIRECTORY): every main screen rendered from the real
/// window's own visual tree, never copied from the screen. The game shelf is kept empty so no one's library shows.</summary>
internal static class ReadmeReview
{
    static System.Windows.Controls.ScrollViewer? Scroller(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is System.Windows.Controls.ScrollViewer sv && sv.Visibility == Visibility.Visible && sv.IsVisible && sv.Content is PcModePage) return sv;
            if (Scroller(child) is { } found) return found;
        }
        return null;
    }
    public static async Task Run(string directory)
    {
        Directory.CreateDirectory(directory);
        ReviewSandbox.Active = true; ReviewSandbox.HideLibrary = true;
        var window = new ProductWindow(false, Path.Combine(directory, "assignments.json"));
        try
        {
            window.Show(); window.WindowState = WindowState.Maximized; await Task.Delay(500);
            window.Host.StartIntro(); await Task.Delay(150);
            // the logo alone, without the Skip and Mute buttons
            if (window.Host.Intro is { } intro) { intro.Seek(6.6); intro.Skip.Visibility = intro.Mute.Visibility = Visibility.Hidden; await Shot("01-intro", 120); intro.Finish(); }
            await Task.Delay(600);
            // everything pictured is the X20 Pro, the model the app is shown on; PC mode is on (the sandbox never starts its service)
            // so the Pro's own Buttons, Curves and Macros tabs are live editors
            var catalog = window.Host.Catalog; var pro = catalog.Get("x20_pro");
            window.Scene.AssignController(0, pro); await Shot("02-zone");
            window.Host.OpenSelector(1); await Shot("03-choose-controller"); window.Host.CloseSelector(); await Task.Delay(400);
            var pc = window.Host.PcMode;
            if (pc != null) pc.Enabled = true;
            window.Host.EnterStudio(0); await Task.Delay(1400);
            var s = window.Host.Studio!;
            await Shot("04-dashboard");
            s.ShowButtons(); s.SelectControl("A"); s.SetDraftTarget("B"); await Shot("05-buttons");
            s.Draft.ResetAll();
            s.ShowCurves(true); await Shot("06-curves");
            s.OpenStickTools(); await Task.Delay(400); await Shot("06b-curves-advanced"); s.CloseStickTools(); await Task.Delay(300);
            s.ShowMacros(true); var m = s.Macros!; m.SelectSlot("M1"); m.Add(); m.Add(); m.Add(); m.Select(1); await Shot("07-macros"); m.Clear();
            s.ShowVibration(true); s.Vibration!.Visualizer.SetStrength(70); await Shot("08-vibration");
            s.ShowManagement("PcMode"); await Shot("08b-pc-mode");
            s.ShowManagement("Device"); await Shot("09-device");
            s.ShowManagement("Diagnostics"); await Task.Delay(600); await Shot("09b-diagnostics"); s.ShowManagement("Mouse"); await Task.Delay(400); await Shot("09c-mouse");
            // a few named setups in the review's own fixture file; the first one carries two remaps from the draft
            s.ShowButtons(); s.SelectControl("A"); s.SetDraftTarget("B"); s.SelectControl("X"); s.SetDraftTarget("Y");
            s.ShowManagement("Setups");
            if (s.Setups is { } lib)
            {
                lib.Create(true); if (lib.SelectedId != null) lib.Store.Rename(lib.SelectedId, "Competitive FPS");
                foreach (string name in new[] { "Racing", "Everyday" }) { lib.Create(false); if (lib.SelectedId != null) lib.Store.Rename(lib.SelectedId, name); }
                lib.Refresh(); if (lib.Store.Items.FirstOrDefault() is { } first) lib.Select(first.Id);
            }
            s.Draft.ResetAll();
            s.ShowManagement("Setups"); await Task.Delay(500); if (s.Setups is { } shown && shown.Store.Items.FirstOrDefault() is { } top) shown.Select(top.Id);
            await Shot("10-setups");
            s.ShowManagement("Tester"); await Shot("11-tester");

            window.Host.ShowSettings(); await Shot("13-tools");
            window.Host.Settings!.ShowAbout(true); await Shot("14-about");
            window.Host.Settings!.ShowAbout(false); window.Host.Settings!.ShowFeedback(true);
            window.Host.Settings!.FeedbackForm.Message.Text = "The right trigger on my X20 Pro reads about 5% before I touch it."; await Shot("16-feedback");
            window.Host.Settings!.ShowFeedback(false);
            window.Host.CloseSettings(); await Task.Delay(400);
            window.Host.OpenCheck(); await Shot("15-controller-check", 1400);
            // last: Agent XCTL answering a question (offline, nothing applied, no controller read); it leaves the stage unrenderable
            window.Host.CloseCheck(); await Task.Delay(500); window.Host.EnterStudio(0); await Task.Delay(1400);
            var a = window.Host.Studio!; a.OpenAgent(); await Task.Delay(800); if (a.AgentWindow is { } agent) await agent.Ask("what does the dead zone do"); await Shot("04c-agent", 2500);
            if (pc != null) pc.Enabled = false;
        }
        catch (Exception error) { File.WriteAllText(Path.Combine(directory, "error.txt"), error.ToString()); throw; }
        finally { window.Close(); ReviewSandbox.HideLibrary = false; }

        async Task Shot(string name, int delay = 1000)
        {
            await Task.Delay(delay); window.UpdateLayout();
            var bmp = new RenderTargetBitmap((int)window.Stage.ActualWidth, (int)window.Stage.ActualHeight, 96, 96, PixelFormats.Pbgra32); bmp.Render(window.Stage);
            var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(bmp)); using var f = File.Create(Path.Combine(directory, name + ".png")); enc.Save(f);
        }
    }
}
