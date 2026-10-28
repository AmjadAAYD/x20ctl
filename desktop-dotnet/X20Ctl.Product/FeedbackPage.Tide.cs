using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Tide;

namespace X20Ctl.Product;

public sealed partial class FeedbackPage
{
    private void BuildTideFeedback(Button back, Panel kinds)
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

        var frame = new TideToolFrame(
            "SEND FEEDBACK",
            "Tell X20CTL what's broken, what you'd like, or how your controller behaves. It goes straight to the X20CTL inbox.",
            onBack: () => back.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)),
            hints: new[] { "\u24B7 Back", "\u24B6 Select" },
            seed: 20);

        // Left List: Staircase rows
        var listStack = new StackPanel { Width = 640 };
        listStack.Children.Add(TideKit.PersonaRow("What is it about?", ctl: "dd:Something's broken"));
        listStack.Children.Add(TideKit.PersonaRow("Controller (optional)", ctl: "dd:Not chosen"));
        listStack.Children.Add(TideKit.PersonaRow("Connected by (optional)", ctl: "dd:Not chosen"));
        listStack.Children.Add(TideKit.PersonaRow("Your message", value: "65 / 4000", focused: true));
        listStack.Children.Add(TideKit.PersonaRow("Send to X20CTL", ctl: "btn:Send to X20CTL"));

        var listHost = new Canvas
        {
            Width = 640,
            RenderTransformOrigin = new Point(0, 0),
            RenderTransform = new RotateTransform(-3)
        };
        listHost.Children.Add(listStack);
        frame.SetLeftList(listHost, 176);

        // Right Side: Detail Card only (visual is None)
        var detailCard = TideKit.DetailCard(
            title: "Your message",
            value: "65 / 4000",
            body: "What's sent: your message, what it's about, the controller and connection if you pick them, the X20CTL version and the Windows version. No account, files or personal details.",
            quote: "The right trigger on my X20 Pro reads about 5% before I touch it.",
            buttons: new[] { "Send to X20CTL" },
            width: 580,
            rot: 2);

        var sideStack = TideKit.SideStack(null, detailCard, 580);
        frame.SetSideStack(sideStack, 176);
        frame.SetNote("What's sent");

        Child = frame;
    }
}
