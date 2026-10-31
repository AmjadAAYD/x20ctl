using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Rebel;

namespace X20Ctl.Product.Agent;

public sealed partial class AgentPanel
{
    private static Brush HB(string hex) => new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
    private static TextBlock HT(string text, double size, string hex, FontWeight weight) => new() { Text = text, FontSize = size, FontWeight = weight, Foreground = HB(hex), FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI") };
    private void BuildHybridAgent()
    {
        var board = new Canvas { Name = "HybridAgent", Width = 1600, Height = 900, ClipToBounds = true, Background = HB("#211e27") };
        RebelToolFrame.At(board, new Rectangle { Width = 800, Height = 900, Fill = new LinearGradientBrush((Color)ColorConverter.ConvertFromString("#e7dbf7"), (Color)ColorConverter.ConvertFromString("#b89fd1"), 90) }, 0, 0);
        RebelToolFrame.At(board, new Rectangle { Width = 4, Height = 900, Fill = HB("#52dcff") }, 798, 0);
        string logoPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Brand", "themes", "hybrid", "logo.png"); var logo = new Image { Width = 78, Height = 56, Stretch = Stretch.Uniform }; if (System.IO.File.Exists(logoPath)) logo.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(logoPath)); RebelToolFrame.At(board, logo, 44, 30);
        RebelToolFrame.At(board, HT("Agent XCTL", 28, "#1b1622", FontWeights.ExtraBold), 136, 36);
        var settingsText = HT("", 14, "#5c4075", FontWeights.SemiBold); settingsText.SetBinding(TextBlock.TextProperty, new Binding("Text") { Source = settingsToggle });
        var settingsButton = new Button { Content = settingsText, Tag = "hybrid:agent:settings", Background = Brushes.Transparent, BorderThickness = new(0), Height = 34, MinHeight = 0 }; settingsButton.Click += (_, _) => ShowSettingsPage(!SettingsOpen); register(settingsButton); RebelToolFrame.At(board, settingsButton, 136, 72);
        RebelToolFrame.Detach(status); status.Foreground = HB("#9ff0c9"); status.FontSize = 15; status.Margin = new(0); RebelToolFrame.At(board, new Border { Background = HB("#1b1622"), CornerRadius = new(18), Padding = new(12, 5, 12, 5), Child = status }, 420, 40);
        int ax = 840; foreach (var (label, action) in new (string, Action)[] { ("LB  Undo", UndoLast), ("RB  Exit", LeaveAgent), ("X  New", Restart) }) { var b = new Button { Content = label, Width = 150, Height = 40, MinHeight = 0, Background = HB("#3a3542"), Foreground = HB("#f4f1f8"), Margin = new(0) }; b.Click += (_, _) => action(); register(b); RebelToolFrame.At(board, b, ax, 38); ax += 164; }
        foreach (var (label, query, index) in new[] { ("Aim & sticks", "sticks", 0), ("Triggers", "triggers", 1), ("Buttons", "buttons", 2), ("PC mode", "PC mode", 3) }) { var b = new Button { Content = label, Width = 180, Height = 40, MinHeight = 0, Background = HB("#d9c7ed"), Foreground = HB("#1b1622") }; b.Click += (_, _) => { Search.Text = query; ShowView(View.Ideas); }; register(b); RebelToolFrame.At(board, b, 44 + index * 190, 120); }
        RebelToolFrame.Detach(scroller); RebelToolFrame.Detach(ideasView); RebelToolFrame.Detach(checkupView); RebelToolFrame.Detach(settingsBox);
        transcript.LayoutTransform = Transform.Identity; transcript.Margin = new(10, 12, 10, 12);
        var views = new Grid { Width = 1400, Height = 472, Children = { scroller, ideasView, checkupView } }; RebelToolFrame.At(board, views, 100, 180);
        settingsBox.Width = 1320; settingsBox.Height = 560; settingsBox.CornerRadius = new(24); settingsBox.Padding = new(24); settingsBox.Background = HB("#29242f"); RebelToolFrame.At(board, settingsBox, 140, 150); Panel.SetZIndex(settingsBox, 5);
        RebelToolFrame.Detach(Input); Input.Width = 1090; Input.Height = 54; Input.MinHeight = 0; Input.MaxHeight = 54; Input.FontSize = 18; Input.Margin = new(22, 0, 0, 0); Input.Foreground = HB("#1b1622"); Input.CaretBrush = HB("#1b1622"); Input.Background = Brushes.Transparent;
        var input = new Grid { Width = 1360, Height = 64 }; input.Children.Add(new Border { Background = HB("#fbf8ff"), CornerRadius = new(32) }); input.Children.Add(Input);
        RebelToolFrame.Detach(sendRound); sendRound.Content = HT("Send", 17, "#f5f1fa", FontWeights.Bold); sendRound.Width = 135; sendRound.Height = 46; sendRound.HorizontalAlignment = HorizontalAlignment.Right; sendRound.VerticalAlignment = VerticalAlignment.Center; sendRound.Margin = new(0, 0, 10, 0); sendRound.Background = HB("#1b1622"); input.Children.Add(sendRound); RebelToolFrame.At(board, input, 120, 720);
        Body.Child = new Viewbox { Child = board, Stretch = Stretch.Uniform }; Body.Background = HB("#17151c"); Body.RenderTransformOrigin = new(.5, .5);
    }
    private Border HybridBubble(string text, bool mine)
    {
        var content = HT(text, 17, mine ? "#1b1622" : "#f5f1fa", FontWeights.Normal); content.TextWrapping = TextWrapping.Wrap;
        return new Border { Width = mine ? 450 : 700, MaxWidth = mine ? 450 : 700, Padding = new(22, 16, 22, 16), Margin = mine ? new(0, 12, 130, 12) : new(80, 12, 0, 12), HorizontalAlignment = mine ? HorizontalAlignment.Right : HorizontalAlignment.Left, CornerRadius = new(22), Background = HB(mine ? "#d5bff6" : "#29242f"), Child = content };
    }
}
