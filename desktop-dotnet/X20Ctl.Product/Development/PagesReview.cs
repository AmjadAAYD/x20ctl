using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace X20Ctl.Product.Development;

/// <summary>Opens every Studio page for the X20 in a real fullscreen window and captures it at the design resolution
/// (--review-pages DIRECTORY): Dashboard, Buttons (unchanged, remapped, and with its "…" menu open), Curves,
/// Macros (empty and with a sequence), Vibration, Device, Setups.</summary>
internal static class PagesReview
{
    public static async Task Run(string directory)
    {
        Directory.CreateDirectory(directory);
        var window = new ProductWindow(false, Path.Combine(directory, "assignments.json"));
        try
        {
            window.Show(); window.WindowState = WindowState.Maximized; await Task.Delay(500);
            window.Scene.AssignController(0, window.Host.Catalog.Get("x20")); window.Host.EnterStudio(0); await Task.Delay(1400);
            var s = window.Host.Studio!;
            await Shot("1-dashboard");
            s.ShowButtons(); s.SelectControl("A"); await Shot("2-buttons");
            s.SetDraftTarget("B"); await Shot("2b-buttons-remapped");
            var more = Find<Button>(window.Stage, b => b.ContextMenu != null && b.IsVisible && Equals(b.ToolTip, "Reset options"));
            if (more?.ContextMenu is { } menu) { menu.PlacementTarget = more; menu.IsOpen = true; await Task.Delay(400); Popup(menu, "2c-buttons-menu"); menu.IsOpen = false; }
            s.Draft.ResetAll();
            s.ShowCurves(true); await Shot("3-curves");
            s.ShowMacros(true); await Shot("4-macros");
            var m = s.Macros!; m.Add(); m.Add(); m.Add(); m.Select(1); await Shot("4b-macros-sequence");
            m.ShowInputKind(true); m.ShowEditor("Inputs"); await Shot("4c-macros-sticks");
            m.Clear();
            s.ShowVibration(true); await Shot("5-vibration");
            s.ShowManagement("Device"); await Shot("6-device");
            s.ShowManagement("Setups"); await Shot("7-setups");
            // a model that is not verified: every page opens as a read-only placeholder
            window.Host.ReturnToZone(); await Task.Delay(700);
            window.Scene.AssignController(1, window.Host.Catalog.Get("x20_pro")); window.Host.EnterStudio(1); await Task.Delay(1400);
            s = window.Host.Studio!;
            s.ShowCurves(true); await Shot("8-pro-curves");
            s.ShowMacros(true); await Shot("8b-pro-macros");
            s.ShowVibration(true); s.Vibration!.Visualizer.SetStrength(100); await Shot("8c-pro-vibration");
            s.ShowManagement("Device"); await Shot("8d-pro-device");
            s.ShowManagement("Setups"); await Shot("8e-pro-setups");
        }
        finally { window.Close(); }

        async Task Shot(string name)
        {
            await Task.Delay(900); window.UpdateLayout();
            var bmp = new RenderTargetBitmap((int)window.Stage.ActualWidth, (int)window.Stage.ActualHeight, 96, 96, PixelFormats.Pbgra32); bmp.Render(window.Stage);
            Save(bmp, name);
        }
        // popups (menus, tooltips) live in their own windows: render the popup's own root, never the screen
        void Popup(FrameworkElement popup, string name)
        {
            if (PresentationSource.FromVisual(popup)?.RootVisual is not FrameworkElement root) return;
            root.UpdateLayout(); var bmp = new RenderTargetBitmap(Math.Max(1, (int)root.ActualWidth), Math.Max(1, (int)root.ActualHeight), 96, 96, PixelFormats.Pbgra32); bmp.Render(root); Save(bmp, name);
        }
        void Save(BitmapSource bmp, string name)
        {
            var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(bmp)); using var f = File.Create(Path.Combine(directory, name + ".png")); enc.Save(f);
        }
    }
    private static T? Find<T>(DependencyObject root, Func<T, bool> match) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T t && match(t)) return t;
            if (Find(child, match) is { } found) return found;
        }
        return null;
    }
}
