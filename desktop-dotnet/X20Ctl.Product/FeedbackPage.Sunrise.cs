using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Sunrise;

namespace X20Ctl.Product;

public sealed partial class FeedbackPage
{
    private void BuildSunriseFeedback(Button back, Panel kinds)
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

        var frame = new SunriseToolFrame(
            "SEND FEEDBACK",
            "Tell X20CTL what's broken, what you'd like, or how your controller behaves.",
            onBack: () => back.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)));

        var formStack = new StackPanel { Width = 720 };
        formStack.Children.Add(new TextBlock
        {
            Text = "CONTROLLER FEEDBACK FORM",
            FontFamily = SunriseKit.Display,
            FontWeight = FontWeights.Black,
            FontSize = 16,
            Foreground = SunriseKit.InkBrush,
            Margin = new Thickness(0, 0, 0, 12)
        });

        formStack.Children.Add(kinds);
        formStack.Children.Add(new TextBlock { Text = "MESSAGE:", FontFamily = SunriseKit.Display, FontWeight = FontWeights.Bold, FontSize = 12, Foreground = SunriseKit.OliveBrush, Margin = new Thickness(0, 12, 0, 6) });
        Message.Height = 160;
        Message.Background = SunriseKit.CreamBrush;
        Message.Foreground = SunriseKit.InkBrush;
        Message.BorderBrush = SunriseKit.InkBrush;
        Message.BorderThickness = new Thickness(2);
        formStack.Children.Add(Message);

        var bRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        bRow.Children.Add(counter);
        bRow.Children.Add(SendButton);
        formStack.Children.Add(bRow);
        formStack.Children.Add(status);

        var formCard = SunriseKit.CreateStickerCard(formStack, 780, 420, cornerRadius: 22);
        Canvas.SetLeft(formCard, 410);
        Canvas.SetTop(formCard, 80);
        frame.ContentArea.Children.Add(formCard);

        Child = frame;
    }
}
