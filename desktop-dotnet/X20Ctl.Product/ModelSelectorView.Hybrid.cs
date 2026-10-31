using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Zone;

namespace X20Ctl.Product;

public sealed partial class ModelSelectorView
{
    private static Brush HColor(string color) => new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
    private static TextBlock HLabel(string text, double size, string color, FontWeight weight) => new() { Text = text, FontSize = size, FontWeight = weight, Foreground = HColor(color), FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI") };
    private void BuildHybridSelector(ControllerCatalog catalog, Dictionary<string, Facts> facts)
    {
        var oldChrome = Children.OfType<DockPanel>().FirstOrDefault();
        var board = new Canvas { Name = "HybridChoose", Width = 1600, Height = 900, ClipToBounds = true, Background = HColor("#211e27") };
        Children.Clear(); Children.Add(new Viewbox { Child = board, Stretch = Stretch.Uniform });
        void At(UIElement element, double x, double y) { Canvas.SetLeft(element, x); Canvas.SetTop(element, y); board.Children.Add(element); }
        At(new Rectangle { Width = 800, Height = 900, Fill = new LinearGradientBrush((Color)ColorConverter.ConvertFromString("#e7dbf7"), (Color)ColorConverter.ConvertFromString("#b89fd1"), 90) }, 0, 0);
        At(new Rectangle { Width = 4, Height = 900, Fill = HColor("#52dcff") }, 798, 0);
        At(HLabel($"PLAYER {Player + 1}", 14, "#5c4075", FontWeights.Bold), 150, 112);
        At(HLabel("Which controller is this?", 44, "#1b1622", FontWeights.ExtraBold), 150, 140);
        var lead = HLabel("Pick the model in your hands. Its layout and tools are separate from a hardware connection.", 16, "#cfc8da", FontWeights.Normal); lead.Width = 640; lead.TextWrapping = TextWrapping.Wrap; At(lead, 836, 136);
        At(new Rectangle { Width = 1460, Height = 170, Fill = new LinearGradientBrush(Color.FromArgb(85,255,255,255), Colors.Transparent, 90) }, 70, 690);
        var row = new StackPanel { Orientation = Orientation.Horizontal }; var scroller = new ScrollViewer { Width = 1420, Height = 465, Content = row, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled }; At(scroller, 90, 265);
        var detail = HLabel("", 15, "#efe9f7", FontWeights.Normal); detail.Width = 720; detail.TextWrapping = TextWrapping.Wrap; At(detail, 820, 750);
        var labels = new Dictionary<string, Border>();
        foreach (var model in catalog.Models)
        {
            if (!choices.TryGetValue(model.Id, out var button)) continue;
            if (button.Parent is Panel parent) parent.Children.Remove(button);
            var blend = blends.Single(item => item.ModelId == model.Id); if (blend.Parent is Decorator decorator) decorator.Child = null; else if (blend.Parent is Panel blendParent) blendParent.Children.Remove(blend);
            var card = new Grid { Width = 230, Height = 350 }; var frame = new Border { Background = HColor("#f7f1ff"), BorderBrush = HColor("#a99bb9"), BorderThickness = new(2), CornerRadius = new(22) }; card.Children.Add(frame);
            var content = new StackPanel { Margin = new(12, 16, 12, 12) }; content.Children.Add(new Viewbox { Width = 200, Height = 230, Stretch = Stretch.Uniform, Child = blend }); content.Children.Add(HLabel(model.Name.Replace("EasySMX ", ""), 19, "#1b1622", FontWeights.Bold)); card.Children.Add(content);
            button.Content = card; button.Width = 230; button.Height = 350; button.Margin = new(0, 0, 24, 0); button.Tag = "hybrid:choose:" + model.Id; labels[model.Id] = frame;
            void Highlight() { foreach (var (id, border) in labels) { bool on = id == model.Id; border.BorderBrush = HColor(on ? "#52dcff" : "#a99bb9"); border.BorderThickness = new(on ? 5 : 2); } detail.Text = facts.TryGetValue(model.Id, out var info) ? info.Description + " · " + info.SupportLine : model.Summary; button.BringIntoView(); }
            button.GotKeyboardFocus += (_, _) => Highlight(); button.MouseEnter += (_, _) => Highlight(); row.Children.Add(button);
        }
        var back = new Button { Content = "Back", Width = 130, Height = 42, MinHeight = 0, Background = HColor("#1b1622"), Foreground = Brushes.White, Tag = "hybrid:choose:back" }; back.Click += (_, _) => Cancelled?.Invoke(); At(back, 110, 816);
        if (oldChrome?.Children.OfType<StackPanel>().FirstOrDefault() is { } chrome) { oldChrome.Children.Remove(chrome); At(chrome, 1040, 8); }
    }
}
