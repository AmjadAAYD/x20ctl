using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Hollow;

namespace X20Ctl.Product;

public sealed partial class FeedbackPage
{
    private void BuildHollowFeedback(Button back, Panel kinds)
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

        var frame = new HollowToolFrame(
            "SEND FEEDBACK",
            "Tell X20CTL what's broken, what you'd like, or how your controller behaves.",
            onBack: () => back.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)));

        var formStack = new StackPanel { Width = 720 };
        formStack.Children.Add(new TextBlock
        {
            Text = "CONTROLLER FEEDBACK FORM",
            FontFamily = HollowKit.Display,
            FontSize = 16,
            Foreground = HollowKit.IvoryTextBrush,
            Margin = new Thickness(0, 0, 0, 12)
        });

        formStack.Children.Add(kinds);
        formStack.Children.Add(new TextBlock { Text = "MESSAGE:", FontFamily = HollowKit.Display, FontSize = 12, Foreground = HollowKit.MutedTextBrush, Margin = new Thickness(0, 12, 0, 6) });
        Message.Height = 160;
        Message.Background = new SolidColorBrush(Color.FromArgb(0xdd, 0x05, 0x0c, 0x14));
        Message.Foreground = HollowKit.IvoryTextBrush;
        Message.BorderBrush = HollowKit.IvoryBorderBrush;
        Message.BorderThickness = new Thickness(1);
        formStack.Children.Add(Message);

        var bRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        bRow.Children.Add(counter);
        bRow.Children.Add(SendButton);
        formStack.Children.Add(bRow);
        formStack.Children.Add(status);

        var formCard = HollowKit.FiligreeCard(formStack, padding: 24, cornerSize: 16);
        formCard.Width = 780;
        Canvas.SetLeft(formCard, 410);
        Canvas.SetTop(formCard, 80);
        frame.ContentArea.Children.Add(formCard);

        Child = frame;
    }
}
