using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace X20Ctl.Product;

public sealed partial class FeedbackPage
{
    private void BuildHybridFeedback(Button back, Panel kinds)
    {
        CornerRadius = new(0); BorderThickness = new(0); Padding = new(0); Background = Brushes.Transparent; MaxWidth = double.PositiveInfinity; Width = 1600; Height = 900; Margin = new(0);
        var board = new Canvas { Name = "HybridFeedback", Width = 1600, Height = 900, Background = ColorBrush("#17151c") }; Child = board;
        void Put(FrameworkElement element, double x, double y)
        {
            if (element.Parent is Panel parent) parent.Children.Remove(element); else if (element.Parent is Decorator decorator) decorator.Child = null;
            Canvas.SetLeft(element, x); Canvas.SetTop(element, y); board.Children.Add(element);
        }
        TextBlock LabelHere(string text, double size = 14) => new() { Text = text, FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI"), FontSize = size, FontWeight = FontWeights.SemiBold, Foreground = ColorBrush("#c8bdd1") };
        Put(new Rectangle { Width = 156, Height = 900, Fill = ColorBrush("#cbb5e4") }, 0, 0); Put(new Rectangle { Width = 140, Height = 900, Fill = ColorBrush("#29262f") }, 1460, 0); Put(new Rectangle { Width = 3, Height = 664, Fill = ColorBrush("#52dcff") }, 846, 166);
        var title = LabelHere("Send feedback", 38); title.FontWeight = FontWeights.ExtraBold; title.Foreground = ColorBrush("#f5f1fa"); Put(title, 216, 38);
        Put(LabelHere("Tell X20CTL what is broken or what you would like."), 218, 99); Put(LabelHere("WHAT IS IT ABOUT?"), 216, 166);
        var choices = kinds.Children.OfType<RadioButton>().ToArray();
        for (int i = 0; i < choices.Length; i++) { var choice = choices[i]; choice.Width = 280; choice.Height = 52; choice.MinWidth = 0; choice.Margin = new(0); Put(choice, 216 + i % 2 * 294, 195 + i / 2 * 62); }
        Put(LabelHere("CONTROLLER (OPTIONAL)"), 216, 350); controller.Width = 588; controller.Height = 52; Put(controller, 216, 380);
        Put(LabelHere("CONNECTED BY (OPTIONAL)"), 216, 470); connection.Width = 588; connection.Height = 52; Put(connection, 216, 500);
        Put(LabelHere("YOUR MESSAGE"), 892, 166);
        Message.Width = 492; Message.MinHeight = 0; Message.MaxHeight = double.PositiveInfinity; Message.Height = 250; Message.FontSize = 17; Message.Foreground = Message.CaretBrush = ColorBrush("#1b1622"); Message.Background = ColorBrush("#fbf8ff"); Message.BorderBrush = ColorBrush("#52dcff"); Message.BorderThickness = new(3); Put(Message, 892, 195);
        counter.Foreground = ColorBrush("#bdb4c8"); Put(counter, 1260, 452);
        var disclosure = LabelHere("What's sent: your message, category, controller, connection, X20CTL version and Windows version."); disclosure.Width = 492; disclosure.TextWrapping = TextWrapping.Wrap; Put(disclosure, 892, 490);
        SendButton.Width = 220; SendButton.Height = 48; SendButton.Margin = new(0); Put(SendButton, 892, 570);
        back.Width = 200; back.Height = 48; back.Margin = new(0); Put(back, 1125, 570);
        status.Width = 492; status.TextWrapping = TextWrapping.Wrap; Put(status, 892, 640);
    }
    private static Brush ColorBrush(string hex) => new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
}
