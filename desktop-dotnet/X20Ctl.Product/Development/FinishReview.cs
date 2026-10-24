using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
namespace X20Ctl.Product.Development;

/// <summary>
/// Every colour of every model, rendered without a window (nothing appears on screen): the live layers front and back
/// with each control shape and stick pivot drawn over them, the sticks and triggers pushed, the Venom change frozen
/// part-way, the Zone's hero card with Change color, and the Choose a controller cards. Colours picked here go to a
/// fixture file in the output folder, never the real one.
/// </summary>
internal static class FinishReview
{
    public static async Task Run(string directory)
    {
        Directory.CreateDirectory(directory); Output = directory;
        PadColours.Use(System.IO.Path.Combine(directory, "pad-colours-fixture.json"));
        using var shapes = JsonDocument.Parse(File.ReadAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "Data/control-shapes.json")));
        var report = new List<string>();
        foreach (string model in new[] { "x20", "x20_pro", "x05_pro", "x10", "d10", "x15", "dune" })
            foreach (var finish in PadColours.Catalog.For(model))
            {
                foreach (bool rear in new[] { false, true })
                {
                    var live = new LiveController { Smooth = false }; live.Show(model, rear, finish.Id);
                    var stage = new Grid { Width = 1536, Height = 1024, Background = new SolidColorBrush(Color.FromRgb(23, 26, 33)), Children = { live, Overlay(shapes, model, rear) } };
                    Save(stage, $"{model}-{finish.Id}-{(rear ? "rear" : "front")}-shapes.png", .5);
                    live.SetInput(.9, .9, -.9, -.9, 1, 1);
                    stage.Children.Clear();
                    var pushed = new Grid { Width = 1536, Height = 1024, Background = new SolidColorBrush(Color.FromRgb(23, 26, 33)), Children = { live } };
                    Save(pushed, $"{model}-{finish.Id}-{(rear ? "rear" : "front")}-pushed.png", .5);
                }
                report.Add($"{model}/{finish.Id} {finish.Name}");
            }

        // the vibration outline of every colour, as the Vibration page draws it for a pad shown in that colour
        PadColours.ActivePlayer = 0;
        foreach (string model in new[] { "x20", "x20_pro", "x05_pro", "x10", "d10", "x15", "dune" })
            foreach (var finish in PadColours.Catalog.For(model))
            {
                PadColours.Set(0, model, finish.Id);
                var visual = new VibrationVisual(Geometry.Empty, false, model); visual.SetStrength(75);
                Save(new Grid { Width = 1536, Height = 1024, Background = new SolidColorBrush(Color.FromRgb(23, 26, 33)), Children = { visual } }, $"vibration-{model}-{finish.Id}.png", .5);
            }
        foreach (string model in new[] { "x20", "x20_pro", "x05_pro", "x10", "d10", "x15", "dune" }) PadColours.Set(0, model, X20Ctl.Zone.FinishCatalog.Original);
        PadColours.ActivePlayer = null;
        report.Add("paddles labelled: " + string.Join(", ", new[] { "x20", "x20_pro", "x05_pro", "x10", "d10", "x15", "dune" }.Select(m => $"{m} {string.Join("/", LiveController.Paddles(m).Select(p => p.Slot))}")));

        // the change, frozen part-way, for a light-to-dark and a dark-to-light pair
        foreach (var (model, from, to) in new[] { ("x20_pro", "original", "white"), ("x20", "original", "black"), ("x15", "original", "wukong"), ("d10", "purple", "green") })
        {
            var picture = new Image { Width = 1536, Height = 1024, Stretch = Stretch.Fill };
            var morph = new FinishMorph(picture); morph.Show(model, from);
            var stage = new Grid { Width = 1536, Height = 1024, Background = new SolidColorBrush(Color.FromRgb(23, 26, 33)), Children = { picture, morph } };
            foreach (double v in new[] { .12, .3, .5, .7, .9, 1 }) { morph.Preview(to, v); Save(stage, $"morph-{model}-{to}-{v * 100:00}.png", .5); }
        }

        // the Zone's hero card: Change color beside Switch Controller; a second player keeps its own colour
        var catalog = new X20Ctl.Zone.ControllerCatalog(File.ReadAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "Data", "catalog.json")));
        var hero = new DockView(0, new X20Ctl.Zone.PlayerDock(1, "EasySMX X20 Pro", false, "x20_pro"), reducedMotion: true);
        var heroStage = new Canvas { Width = 1100, Height = 760, Background = new SolidColorBrush(Color.FromRgb(16, 20, 28)), Children = { hero } };
        hero.SetBounds(new(0, 0, 1100, 760), true, false);
        Save(heroStage, "zone-hero-x20_pro-original.png", 1);
        await hero.ChangeColour();
        Save(heroStage, "zone-hero-x20_pro-white.png", 1);
        var second = new DockView(1, new X20Ctl.Zone.PlayerDock(2, "EasySMX X20 Pro", false, "x20_pro"), reducedMotion: true);
        var secondStage = new Canvas { Width = 1100, Height = 760, Background = new SolidColorBrush(Color.FromRgb(16, 20, 28)), Children = { second } };
        second.SetBounds(new(0, 0, 1100, 760), true, false);
        Save(secondStage, "zone-hero-player2-x20_pro.png", 1);
        report.Add($"remembered: player 1 x20_pro = {PadColours.For(0, "x20_pro")}, player 2 x20_pro = {PadColours.For(1, "x20_pro")}");
        var reloaded = new X20Ctl.Zone.PadColourStore(System.IO.Path.Combine(directory, "pad-colours-fixture.json")); reloaded.Load();
        report.Add($"reloaded from file: player 1 x20_pro = {reloaded.Get(0, "x20_pro")}");

        // Choose a controller: every card in its original colour, then one card turned part-way
        var selector = new ModelSelectorView(catalog, 0);
        var selectorStage = new Grid { Width = 1600, Height = 1000, Children = { selector } };
        Save(selectorStage, "selector.png", .75);
        var turning = selector.Morphs.First(m => m.ModelId == "x20_pro"); turning.Preview("white", .5);
        Save(selectorStage, "selector-x20_pro-turning.png", .75);
        double cost = selector.Morphs.Sum(m => FinishMorph.PaintCost(m, 120));
        report.Add($"one frame of every card changing at once: {cost:0.00} ms of work ({selector.Morphs.Count} cards)");

        File.WriteAllLines(System.IO.Path.Combine(directory, "finishes.txt"), report);
    }

    private static Canvas Overlay(JsonDocument shapes, string model, bool rear)
    {
        var canvas = new Canvas { Width = 1536, Height = 1024 };
        if (shapes.RootElement.GetProperty("models").TryGetProperty(model, out var m) && m.TryGetProperty(rear ? "back" : "front", out var view))
            foreach (var c in view.EnumerateObject())
                canvas.Children.Add(new System.Windows.Shapes.Path { Data = Geometry.Parse(c.Value.GetProperty("path").GetString()!), Stroke = new SolidColorBrush(Color.FromArgb(230, 26, 159, 255)), StrokeThickness = 3 });
        return canvas;
    }

    private static void Save(FrameworkElement element, string name, double scale)
    {
        element.Measure(new Size(element.Width, element.Height)); element.Arrange(new Rect(0, 0, element.Width, element.Height)); element.UpdateLayout();
        var brush = new VisualBrush(element);
        var visual = new DrawingVisual(); using (var dc = visual.RenderOpen()) dc.DrawRectangle(brush, null, new Rect(0, 0, element.Width * scale, element.Height * scale));
        var bitmap = new RenderTargetBitmap((int)(element.Width * scale), (int)(element.Height * scale), 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(System.IO.Path.Combine(Output, name)); encoder.Save(file);
    }
    private static string Output = "";
}
