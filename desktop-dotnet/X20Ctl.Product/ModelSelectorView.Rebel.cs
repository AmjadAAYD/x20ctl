using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Rebel;
using X20Ctl.Zone;
namespace X20Ctl.Product;

public sealed partial class ModelSelectorView
{
    private void BuildRebelSelector(ControllerCatalog catalog, Dictionary<string, Facts> facts)
    {
        var oldChrome = Children.OfType<DockPanel>().FirstOrDefault();
        var models = catalog.Models.ToDictionary(model => model.Id);
        string[] order = ["x20_pro", "x05_pro", "x10", "x20", "d10", "x15", "dune"];
        var board = new Canvas { Name = "RebelChoose", Width = 1600, Height = 900, ClipToBounds = true, Background = Ink.B(Ink.Black) };
        Children.Clear(); Children.Add(new Viewbox { Child = board, Stretch = Stretch.Uniform });
        void At(FrameworkElement element, double x, double y)
        { Canvas.SetLeft(element, x); Canvas.SetTop(element, y); board.Children.Add(element); }
        At(Ink.Label($"Player {Player + 1}"), 110, 28);
        var title = new Viewbox { Child = Ransom.Make("Which controller is this?", 44), MaxWidth = 820, MaxHeight = 72, StretchDirection = StretchDirection.DownOnly };
        At(title, 110, 48);
        var lead = Ink.Text("Pick the model in your hands. This sets its layout and tools; connecting the hardware is a separate step.", 17, Ink.B(Ink.Dim), false);
        lead.Width = 880; lead.TextWrapping = TextWrapping.Wrap; At(lead, 110, 122);
        At(new Rectangle { Width = 1460, Height = 470, Fill = Ink.B("#141214") }, 70, 246);
        for (int line = 0; line <= 15; line++) board.Children.Add(new Line { X1 = 70, X2 = 1530, Y1 = 246 + line * 30, Y2 = 246 + line * 30, Stroke = Ink.B(Ink.A(Ink.White, line % 4 == 0 ? .22 : .08)), StrokeThickness = line % 4 == 0 ? 3 : 1, IsHitTestVisible = false });
        At(new Rectangle { Width = 1600, Height = 184, Fill = Ink.B("#0e0d0f") }, 0, 716);
        At(new Rectangle { Width = 1600, Height = 5, Fill = Ink.B(Ink.Red) }, 0, 716);
        var cone = new Polygon { Fill = new LinearGradientBrush(Ink.A(Ink.Red, .55), Ink.A(Ink.Red, .12), 90), IsHitTestVisible = false };
        board.Children.Insert(20, cone);
        var file = new Grid { Width = 420, Height = 176, IsHitTestVisible = false, RenderTransformOrigin = new(.5, .5), RenderTransform = new RotateTransform(2) };
        file.Children.Add(new TornPaper { Seed = 8, Sheet = Ink.B(Ink.White), UnderSheet = Ink.B(Ink.Red) });
        var body = new StackPanel { Margin = new(24, 20, 24, 16) }; file.Children.Add(body);
        var name = Ink.Text("", 28, Ink.B(Ink.Black)); var detail = Ink.Text("", 13.5, Ink.B(Ink.Black), false); detail.TextWrapping = TextWrapping.Wrap; detail.Margin = new(0, 6, 0, 12);
        var support = Ink.Label("", 12, Ink.B(Ink.White)); body.Children.Add(name); body.Children.Add(detail); body.Children.Add(new Border { Child = support, Background = Ink.B(Ink.Red), Padding = new(12, 4, 12, 4), HorizontalAlignment = HorizontalAlignment.Left });
        var entries = new List<(string Id, Button Button, Viewbox Art, Slab Label, double Center)>();
        double[] centers = [160, 352, 544, 800, 1056, 1248, 1440];
        for (int i = 0; i < order.Length; i++)
        {
            string id = order[i]; if (!choices.TryGetValue(id, out var button)) continue;
            if (button.Parent is Panel parent) parent.Children.Remove(button);
            var blend = blends.Single(item => item.ModelId == id);
            if (blend.Parent is Decorator decorator) decorator.Child = null;
            else if (blend.Parent is Panel blendParent) blendParent.Children.Remove(blend);
            var face = new Canvas { Height = 548 }; var art = new Viewbox { Child = blend, Stretch = Stretch.Uniform, Effect = Ink.Shadow(8, 10, 0, Ink.Black, 1) }; face.Children.Add(art);
            var text = Ink.Text($"{i + 1}  {models[id].Name.Replace("EasySMX ", "")}", 17); text.HorizontalAlignment = HorizontalAlignment.Center; text.VerticalAlignment = VerticalAlignment.Center;
            var label = new Slab { Width = 124, Height = 62, Seed = 140 + i, Rot = i % 2 == 0 ? 2 : -3, Line = Ink.B(Ink.A(Ink.White, .26)), LineWidth = 1.4 }; label.Children.Add(text); face.Children.Add(label); Canvas.SetTop(label, 486);
            button.Content = face; button.Margin = new(0); button.Height = 548; button.Tag = "choose:" + id;
            button.GotKeyboardFocus += (_, _) => Select(id); button.MouseEnter += (_, _) => Select(id);
            entries.Add((id, button, art, label, centers[i])); At(button, centers[i] - 89, 246);
        }
        At(file, 1010, 266);
        if (oldChrome?.Children.OfType<StackPanel>().FirstOrDefault() is { } chrome)
        { oldChrome.Children.Remove(chrome); At(RebelChrome.Fit(chrome, 580), 1010, 0); }
        var back = new SlabButton("Back", 110, 36, 87, -2, 16, Ink.White); back.Click += (_, _) => Cancelled?.Invoke(); At(back, 70, 836);
        At(Parts.Hints(("A", "Choose"), ("B", "Back"), ("✥", "Move")), 1150, 836);
        Select("x20");

        void Select(string selected)
        {
            foreach (var entry in entries)
            {
                bool active = entry.Id == selected; double width = active ? 330 : 178, center = Math.Clamp(entry.Center, width / 2, 1600 - width / 2);
                entry.Button.Width = width; Canvas.SetLeft(entry.Button, center - width / 2);
                entry.Art.Width = width; entry.Art.Height = width * 2 / 3; entry.Art.Opacity = active ? 1 : .62;
                Canvas.SetTop(entry.Art, 462 - entry.Art.Height); Canvas.SetLeft(entry.Label, (width - 124) / 2);
                entry.Label.Fill = Ink.B(active ? Ink.Red : Ink.Panel); entry.Label.Line = Ink.B(Ink.A(Ink.White, active ? 1 : .26)); entry.Label.InvalidateVisual();
                if (active) cone.Points = new PointCollection([new(center - 30, 246), new(center + 30, 246), new(center + 200, 716), new(center - 200, 716)]);
            }
            if (!models.TryGetValue(selected, out var model)) return;
            name.Text = model.Name; detail.Text = facts.TryGetValue(selected, out var value) ? value.Description : model.Summary;
            support.Text = facts.TryGetValue(selected, out value) ? value.SupportLine : model.Summary;
        }
    }
}
