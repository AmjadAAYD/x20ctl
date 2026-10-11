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
            // the logo alone, without the Skip hint
            if (window.Host.Intro is { } intro) { intro.Seek(3.7); intro.Skip.Visibility = Visibility.Hidden; await Shot("01-intro", 120); intro.Finish(); }
            await Task.Delay(600);
            var catalog = window.Host.Catalog;
            window.Scene.AssignController(0, catalog.Get("x20")); window.Scene.AssignController(1, catalog.Get("x20_pro"));
            window.Scene.AssignController(2, catalog.Get("dune")); await Shot("02-zone");
            window.Host.OpenSelector(3); await Shot("03-choose-controller"); window.Host.CloseSelector(); await Task.Delay(400);

            window.Host.EnterStudio(0); await Task.Delay(1400);
            var s = window.Host.Studio!;
            await Shot("04-dashboard");
            s.ShowButtons(); s.SelectControl("A"); s.SetDraftTarget("B"); await Shot("05-buttons");
            s.Draft.ResetAll();
            s.ShowCurves(true); await Shot("06-curves");
            s.OpenStickTools(); await Task.Delay(400); await Shot("06b-curves-advanced"); s.CloseStickTools(); await Task.Delay(300);
            s.ShowMacros(true); var m = s.Macros!; m.Add(); m.Add(); m.Add(); m.Select(1); await Shot("07-macros"); m.Clear();
            s.ShowVibration(true); s.Vibration!.Visualizer.SetStrength(70); await Shot("08-vibration");
            s.ShowManagement("Device"); await Shot("09-device");
            s.ShowManagement("Diagnostics"); await Task.Delay(600); await Shot("09b-diagnostics"); s.ShowManagement("Mouse"); await Task.Delay(400); await Shot("09c-mouse"); s.ShowManagement("Device");
            // a few named setups in the review's own fixture file (ProductHost keeps review stores beside the assignments file);
            // the first one carries two remaps from the draft
            s.ShowButtons(); s.SelectControl("A"); s.SetDraftTarget("B"); s.SelectControl("X"); s.SetDraftTarget("Y");
            s.ShowManagement("Setups");
            if (s.Setups is { } lib)
            {
                lib.Create(true); lib.Store.Rename(lib.SelectedId!, "Competitive FPS");
                foreach (string name in new[] { "Racing", "Everyday" }) { lib.Create(false); lib.Store.Rename(lib.SelectedId!, name); }
                lib.Refresh(); lib.Select(lib.Store.Items.First().Id);
            }
            s.Draft.ResetAll();
            await Shot("10-setups");
            s.ShowManagement("Tester"); await Shot("11-tester");

            window.Host.ReturnToZone(); await Task.Delay(700);
            window.Host.EnterStudio(2); await Task.Delay(1400);
            window.Host.Studio!.ShowManagement("Device"); await Shot("12-dune-device");
            // PC mode on (the sandbox never starts the service): the Dune's own tabs become PC mode's editor
            if (window.Host.PcMode is { } pc)
            {
                pc.Enabled = true; window.Host.EnterStudio(2); await Task.Delay(1400);
                var d = window.Host.Studio!;
                d.ShowManagement("PcMode"); await Shot("20-studio-pc-mode");
                d.ShowButtons(); d.SelectControl("A"); d.SetDraftTarget("B"); d.SelectControl("RB"); d.SetDraftTarget("RT"); d.SelectControl("A"); await Shot("21-pc-mode-buttons");
                d.ShowMacros(true); var dm = d.Macros!; dm.SelectSlot("M1"); dm.Add(); dm.Add(); dm.Select(0); d.Macros!.Draft.SetTrigger("M1", "L3"); dm.Refresh(); await Shot("22-pc-mode-macros");
                dm.Clear(); d.Macros!.Draft.SetTrigger("M1", null); d.Draft.ResetAll();
                pc.Enabled = false; window.Host.EnterStudio(2); await Task.Delay(600);
            }

            window.Host.ShowSettings(); await Shot("13-tools");
            window.Host.Settings!.ShowAbout(true); await Shot("14-about");
            window.Host.Settings!.ShowAbout(false); window.Host.Settings!.ShowFeedback(true);
            window.Host.Settings!.FeedbackForm.Message.Text = "The right trigger on my X20 Pro reads about 5% before I touch it."; await Shot("16-feedback");
            window.Host.Settings!.ShowFeedback(false);
            window.Host.Settings!.ShowPcMode(true); await Shot("17-pc-mode");
            if (Scroller(window.Host.Settings!) is { } sv) { sv.ScrollToVerticalOffset(900); await Task.Delay(200); await Shot("18-pc-mode-editor"); sv.ScrollToEnd(); await Task.Delay(200); await Shot("19-pc-mode-end"); sv.ScrollToTop(); }
            window.Host.Settings!.ShowPcMode(false);
            window.Host.CloseSettings(); await Task.Delay(400);
            window.Host.OpenCheck(); await Shot("15-controller-check", 1400);
        }
        finally { window.Close(); ReviewSandbox.HideLibrary = false; }

        async Task Shot(string name, int delay = 1000)
        {
            await Task.Delay(delay); window.UpdateLayout();
            var bmp = new RenderTargetBitmap((int)window.Stage.ActualWidth, (int)window.Stage.ActualHeight, 96, 96, PixelFormats.Pbgra32); bmp.Render(window.Stage);
            var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(bmp)); using var f = File.Create(Path.Combine(directory, name + ".png")); enc.Save(f);
        }
    }
}
