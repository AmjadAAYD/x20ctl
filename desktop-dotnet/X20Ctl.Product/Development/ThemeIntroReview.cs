using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace X20Ctl.Product.Development;

internal static class ThemeIntroReview
{
    internal static async Task Run(string directory, string id)
    {
        Directory.CreateDirectory(directory); ReviewSandbox.Active = true; ReviewSandbox.HideLibrary = true;
        var theme = ThemeCatalog.Find(id);
        if (theme.Id != id || theme.Intro is not {} spec) throw new InvalidOperationException("A registered themed film is required: " + id);
        ThemeCatalog.Apply(theme);
        if (!File.Exists(spec.FilmPath) || !File.Exists(spec.LogoPath)) throw new FileNotFoundException("Themed film/logo is missing for " + id);
        var host = new ProductHost(new ZoneScene(true), Path.Combine(directory, "assignments.json"));
        var stage = new DesignStage { Child = host };
        using var source = new HwndSource(new HwndSourceParameters("theme intro review") { WindowStyle = unchecked((int)0x80000000), Width = 1600, Height = 900, PositionX = -32000, PositionY = -32000 }) { RootVisual = stage };
        await Layout();
        Rect? target = host.Zone.MarkTarget(host);
        var intro = new IntroView(false, true, host.Zone); host.Children.Add(intro); Panel.SetZIndex(intro, 50);
        try
        {
            intro.Seek(0); await Layout();
            for (int i = 0; i < 100 && !intro.FilmOpened; i++) await Task.Delay(100);
            if (!intro.FilmOpened) throw new InvalidOperationException("The native media player did not open " + id);
            var cover = Walk(intro).OfType<Image>().Single(i => i.Source is BitmapSource bitmap && bitmap.PixelWidth == spec.Image.Width && bitmap.PixelHeight == spec.Image.Height);
            Rect InkBounds()
            {
                double scale = cover.ActualWidth / spec.Image.Width;
                return cover.TransformToAncestor(host).TransformBounds(new Rect(spec.Ink.X * scale, spec.Ink.Y * scale, spec.Ink.Width * scale, spec.Ink.Height * scale));
            }
            intro.Seek(spec.CoverAt); await Layout();
            var filmBox = new Rect(spec.FilmLogo.X / 1.2, spec.FilmLogo.Y / 1.2, spec.FilmLogo.Width / 1.2, spec.FilmLogo.Height / 1.2);
            if (!Near(InkBounds(), filmBox)) throw new InvalidOperationException("Logo overlay does not align with film metadata: " + id);
            Save("cover");
            intro.Seek(spec.FlyAt + .8); await Layout();
            if (target is {} landing)
            {
                if (!Near(InkBounds(), landing)) throw new InvalidOperationException("Logo does not land on its header target: " + id);
            }
            else if (!Near(InkBounds(), filmBox) || cover.Opacity > .001)
                throw new InvalidOperationException("A theme without a header target must fade its logo in place: " + id);
            Save("handoff");
            if (intro.Mute.Visibility != Visibility.Collapsed || !intro.Muted) throw new InvalidOperationException("Themed film must remain silent: " + id);
            intro.Finish(); host.Children.Remove(intro);
            var reduced = new IntroView(true, true, host.Zone); host.Children.Add(reduced); reduced.Seek(.4);
            bool reducedDone = reduced.Opacity == 0 && !reduced.FilmOpened;
            reduced.Finish(); host.Children.Remove(reduced);
            if (!reducedDone) throw new InvalidOperationException("Reduced motion opened or retained a film: " + id);
            File.WriteAllText(Path.Combine(directory, "intro.json"), JsonSerializer.Serialize(new { theme = id, windowShown = false, nativeFilmOpened = true, overlayAligned = true, headerTarget = target, fadesInPlace = target == null, muted = true, reducedMotion = true }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally { intro.Finish(); host.Children.Remove(intro); }

        async Task Layout()
        {
            await host.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
            stage.Measure(new Size(1600,900)); stage.Arrange(new Rect(0,0,1600,900)); stage.UpdateLayout();
            await Task.Delay(60);
        }
        void Save(string name)
        {
            var bitmap = new RenderTargetBitmap(1600,900,96,96,PixelFormats.Pbgra32); bitmap.Render(stage);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var file = File.Create(Path.Combine(directory, name + ".png")); encoder.Save(file);
        }
    }
    private static bool Near(Rect a, Rect b) => Math.Abs(a.X-b.X)<.15 && Math.Abs(a.Y-b.Y)<.15 && Math.Abs(a.Width-b.Width)<.15 && Math.Abs(a.Height-b.Height)<.15;
    private static IEnumerable<DependencyObject> Walk(DependencyObject root)
    {
        for (int i=0; i<VisualTreeHelper.GetChildrenCount(root); i++)
            if (VisualTreeHelper.GetChild(root,i) is {} child) { yield return child; foreach (var nested in Walk(child)) yield return nested; }
    }
}
