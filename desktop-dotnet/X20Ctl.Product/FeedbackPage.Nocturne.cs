using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Nocturne;

namespace X20Ctl.Product;

public sealed partial class FeedbackPage
{
    private void BuildNocturneFeedback(Button back, Panel kinds)
    {
        CornerRadius = new(0);
        BorderThickness = new(0);
        Padding = new(0);
        Background = Brushes.Transparent;
        BorderBrush = Brushes.Transparent;
        MaxWidth = double.PositiveInfinity;
        Margin = new(0);
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Top;
        Width = 1600;
        Height = 900;

        Detach(kinds);
        Detach(controller);
        Detach(connection);
        Detach(Message);
        Detach(counter);
        Detach(SendButton);
        Detach(back);
        Detach(status);

        var frame = new NocturneToolFrame(
            "SEND FEEDBACK",
            "Tell X20CTL what's broken, what you'd like, or how your controller behaves.",
            onBack: () => back.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)));

        var formStack = new StackPanel { Width = 720 };
        formStack.Children.Add(new TextBlock
        {
            Text = "CONTROLLER FEEDBACK FORM",
            FontFamily = NocturneKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 16,
            Foreground = NocturneKit.VoidTextBrush,
            Margin = new Thickness(0, 0, 0, 12)
        });

        formStack.Children.Add(kinds);
        formStack.Children.Add(new TextBlock { Text = "MESSAGE:", FontFamily = NocturneKit.Display, FontWeight = FontWeights.Bold, FontStyle = FontStyles.Italic, FontSize = 12, Foreground = NocturneKit.SoftBrush, Margin = new Thickness(0, 12, 0, 6) });
        Message.Height = 160;
        Message.Background = NocturneKit.NightBrush;
        Message.Foreground = NocturneKit.WhiteBrush;
        Message.BorderBrush = NocturneKit.AccentBrush;
        Message.BorderThickness = new Thickness(1);
        formStack.Children.Add(Message);

        var bRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        bRow.Children.Add(counter);
        bRow.Children.Add(SendButton);
        formStack.Children.Add(bRow);
        formStack.Children.Add(status);

        var formCard = NocturneKit.NocturneWhiteCard(formStack, 780, 420);
        Canvas.SetLeft(formCard, 410);
        Canvas.SetTop(formCard, 80);
        frame.ContentArea.Children.Add(formCard);

        Child = frame;
    }
}
