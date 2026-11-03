using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Nexus;

namespace X20Ctl.Product;

public sealed partial class FeedbackPage
{
    private void BuildNexusFeedback(Button back, Panel kinds)
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

        var board = new Canvas { Name = "NexusFeedback", Width = 1600, Height = 900, ClipToBounds = true };

        // 1. Background gradient
        var bg = new Rectangle
        {
            Width = 1600, Height = 900,
            Fill = new LinearGradientBrush
            {
                StartPoint = new(0.5, 0), EndPoint = new(0.5, 1),
                GradientStops = { new(NexusKit.C("#1a1c20"), 0.0), new(NexusKit.House, 1.0) }
            }
        };
        board.Children.Add(bg);

        // 2. Top bar
        var topBar = NexusKit.TopBar(
            player: 0,
            model: "EasySMX X20 Pro",
            status: "Offline",
            onTools: () => back.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent))
        );
        Canvas.SetLeft(topBar, 62);
        Canvas.SetTop(topBar, 46);
        board.Children.Add(topBar);

        // 3. Title & Lead
        var title = new TextBlock
        {
            Text = "Send feedback",
            FontFamily = NexusKit.Display,
            FontSize = 38,
            FontWeight = FontWeights.SemiBold,
            Foreground = NexusKit.WhiteBrush
        };
        Canvas.SetLeft(title, 68);
        Canvas.SetTop(title, 138);
        board.Children.Add(title);

        var lead = new TextBlock
        {
            Text = "Tell X20CTL what's broken, what you'd like, or how your controller behaves. It goes straight to the X20CTL inbox.",
            FontFamily = NexusKit.Body,
            FontSize = 17,
            Foreground = NexusKit.SubBrush
        };
        Canvas.SetLeft(lead, 70);
        Canvas.SetTop(lead, 192);
        board.Children.Add(lead);

        // 4. Left Column at (68, 250, w=820)
        var rowPanel = new StackPanel { Width = 820 };

        var r1 = NexusKit.RowButton("What is it about?", null, NexusKit.Dropdown("Something's broken"), focused: false, width: 820);
        rowPanel.Children.Add(r1);

        var r2 = NexusKit.RowButton("Controller (optional)", null, NexusKit.Dropdown("Not chosen"), focused: false, width: 820);
        rowPanel.Children.Add(r2);

        var r3 = NexusKit.RowButton("Connected by (optional)", null, NexusKit.Dropdown("Not chosen"), focused: false, width: 820);
        rowPanel.Children.Add(r3);

        // Focused: Your message
        var r4 = NexusKit.RowButton("Your message", "65 / 4000", null, focused: true, width: 820);
        rowPanel.Children.Add(r4);

        var sendBtn = NexusKit.TheatricalButton("Send to X20CTL", primary: true, height: 48, fontSize: 16);
        sendBtn.MouseLeftButtonDown += (_, _) => SendButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        rowPanel.Children.Add(sendBtn);

        Canvas.SetLeft(rowPanel, 68);
        Canvas.SetTop(rowPanel, 250);
        board.Children.Add(rowPanel);

        // 5. Right Details Side at (920, 250, w=612)
        var sideStack = new StackPanel { Width = 612 };

        var card = new Border
        {
            Width = 612,
            Padding = new Thickness(28, 26, 28, 26),
            CornerRadius = new CornerRadius(8),
            Background = NexusKit.PanelBrush
        };
        var cardStack = new StackPanel();
        cardStack.Children.Add(new TextBlock
        {
            Text = "Your message",
            FontFamily = NexusKit.Display,
            FontSize = 24,
            FontWeight = FontWeights.SemiBold,
            Foreground = NexusKit.WhiteBrush
        });

        // Quote box with green ring
        var quoteBox = new Border
        {
            Padding = new Thickness(18, 14, 18, 14),
            Margin = new Thickness(0, 12, 0, 12),
            CornerRadius = new CornerRadius(4),
            Background = NexusKit.InputBrush,
            BorderBrush = NexusKit.GreenBrush,
            BorderThickness = new Thickness(3),
            Child = new TextBlock
            {
                Text = "The right trigger on my X20 Pro reads about 5% before I touch it.",
                FontFamily = NexusKit.Body,
                FontSize = 18,
                Foreground = NexusKit.WhiteBrush,
                TextWrapping = TextWrapping.Wrap
            }
        };
        cardStack.Children.Add(quoteBox);

        cardStack.Children.Add(new TextBlock
        {
            Text = "65 / 4000",
            FontFamily = NexusKit.Display,
            FontSize = 46,
            FontWeight = FontWeights.SemiBold,
            Foreground = NexusKit.WhiteBrush,
            Margin = new Thickness(0, 0, 0, 16)
        });

        cardStack.Children.Add(new TextBlock
        {
            Text = "What's sent: your message, what it's about, the controller and connection if you pick them, the X20CTL version and the Windows version. No account, files or personal details.",
            FontFamily = NexusKit.Body,
            FontSize = 15,
            Foreground = NexusKit.SubBrush,
            TextWrapping = TextWrapping.Wrap
        });

        card.Child = cardStack;
        sideStack.Children.Add(card);

        Canvas.SetLeft(sideStack, 920);
        Canvas.SetTop(sideStack, 250);
        board.Children.Add(sideStack);

        // 6. Bottom Hints
        var hints = NexusKit.HintsBar(new[] { ("A", "Select"), ("B", "Back") });
        Canvas.SetRight(hints, 62);
        Canvas.SetTop(hints, 856);
        board.Children.Add(hints);

        Child = new Viewbox { Child = board, Stretch = Stretch.Uniform };
    }
}
