using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace X20Ctl.Product.Hybrid;

internal static class HybridToolFrame
{
    internal static Brush Color(string hex) => new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString(hex));
    internal static TextBlock Text(string text, double size, string color, FontWeight weight) => new() { Text = text, FontSize = size, FontWeight = weight, Foreground = Color(color), FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI"), TextWrapping = TextWrapping.Wrap };
    internal static void Detach(FrameworkElement element) { if (element.Parent is Panel p) p.Children.Remove(element); else if (element.Parent is Decorator d) d.Child = null; else if (element.Parent is ContentControl c) c.Content = null; }
    internal static void At(Canvas board, FrameworkElement element, double x, double y) { Detach(element); Canvas.SetLeft(element, x); Canvas.SetTop(element, y); board.Children.Add(element); }
    internal static Button Button(string label, string tag, double width, double height, bool selected = false)
    {
        var b = new Button { Content = label, Tag = tag, Width = width, Height = height, MinHeight = 0, FontSize = 15, FontWeight = FontWeights.SemiBold, Background = Color(selected ? "#d8c6ed" : "#2d2933"), Foreground = Color(selected ? "#1b1622" : "#f4f1f8"), BorderBrush = Color(selected ? "#52dcff" : "#514a58"), BorderThickness = new(selected ? 3 : 1) };
        void Paint() { b.BorderBrush = Color(b.IsKeyboardFocused || b.IsMouseOver || selected ? "#52dcff" : "#514a58"); b.BorderThickness = new(b.IsKeyboardFocused || selected ? 3 : 1); }
        b.GotKeyboardFocus += (_, _) => Paint(); b.LostKeyboardFocus += (_, _) => Paint(); b.MouseEnter += (_, _) => Paint(); b.MouseLeave += (_, _) => Paint(); return b;
    }
    internal static Canvas Create(string name, string title, string lead, Action back, Action<string> navigate, string current = "tools")
    {
        var board = new Canvas { Name = name, Width = 1600, Height = 900, ClipToBounds = true, Background = Color("#17151c") };
        At(board, new Rectangle { Width = 156, Height = 900, Fill = Color("#cbb5e4") }, 0, 0); At(board, new Rectangle { Width = 140, Height = 900, Fill = Color("#29262f") }, 1460, 0);
        string path = System.IO.Path.Combine(AppContext.BaseDirectory, "Brand", "themes", "hybrid", "logo.png"); var logo = new Image { Width = 112, Height = 78, Stretch = Stretch.Uniform }; if (System.IO.File.Exists(path)) logo.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(path)); At(board, logo, 22, 20);
        At(board, Text(title, 38, "#f5f1fa", FontWeights.ExtraBold), 216, 32); var intro = Text(lead, 14, "#bdb4c8", FontWeights.SemiBold); intro.Width = 1168; At(board, intro, 218, 90);
        var pages = new (string label, string key)[] { ("Buttons", "buttons"), ("Curves", "curves"), ("Macros", "macros"), ("Vibration", "vibration"), ("PC mode", "pcmode"), ("Device", "device"), ("Mouse", "mouse"), ("Setups", "setups"), ("Tools", "tools") };
        for (int i = 0; i < pages.Length; i++) { var (label, key) = pages[i]; var button = Button(label, "hybrid:scan:nav:" + key, 120, 50, key == current); button.Click += (_, _) => { if (key == current) back(); else navigate(key); }; At(board, button, 18, 150 + i * 67); }
        return board;
    }
}
