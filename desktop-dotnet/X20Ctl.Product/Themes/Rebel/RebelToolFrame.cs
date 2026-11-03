using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace X20Ctl.Product.Rebel;

internal static class RebelToolFrame
{
    internal static Canvas Create(string name, string title, string lead, Action back, Action<string> navigate, double titleSize = 52, string current = "tools")
    {
        var board = new Canvas { Name = name, Width = 1600, Height = 900, ClipToBounds = true, Background = Ink.B(Ink.Black) };
        if (current == "curves") board.Children.Add(new System.Windows.Shapes.Polygon { Points = new PointCollection([new(1488, 0), new(1600, 0), new(1600, 900), new(1188, 900)]), Fill = Ink.B("#130709"), IsHitTestVisible = false });
        At(board, new Border { Width = 4, Height = 23, Background = Ink.B(Ink.Red) }, 266, 112);
        At(board, Pieces.Logo(110), 26, 20);
        At(board, new Viewbox { Child = Ransom.Make(title, titleSize), MaxWidth = 1280, MaxHeight = 86, StretchDirection = StretchDirection.DownOnly }, 262, 24);
        var text = Ink.Text(lead, 16, Ink.B(Ink.Dim), false); text.Width = 1180; text.TextWrapping = TextWrapping.Wrap; At(board, text, 286, 114);
        for (int i = 0; i < StudioScene.RebelPages.Length; i++)
        {
            var (caption, glyph, key) = StudioScene.RebelPages[i];
            var button = new PageSlab(caption, glyph, 62, 52, i + 3, -5 + i % 3, new(0, -4, -152, -4), false, 21, 22, 22, 22) { ShowName = false, On = key == current, Tag = "tool-nav:" + key };
            button.Click += (_, _) => { if (key == "tools" && current == "tools") back(); else navigate(key); }; At(board, button, 22 + i * 4, 128 + i * 77);
        }
        At(board, Parts.Hints(("A", "Select"), ("B", "Back")), 1250, 836); return board;
    }

    internal static void Detach(FrameworkElement element)
    { if (element.Parent is Panel p) p.Children.Remove(element); else if (element.Parent is Decorator d) d.Child = null; else if (element.Parent is ContentControl c) c.Content = null; }
    internal static void At(Canvas board, FrameworkElement element, double x, double y)
    { Detach(element); Canvas.SetLeft(element, x); Canvas.SetTop(element, y); board.Children.Add(element); }
    internal static void InkText(DependencyObject root, Color color)
    {
        if (root is TextBlock text) { text.Foreground = Ink.B(color); text.FontFamily = Ink.Body; text.TextWrapping = TextWrapping.Wrap; }
        foreach (object child in LogicalTreeHelper.GetChildren(root)) if (child is DependencyObject nested) InkText(nested, color);
    }
}
