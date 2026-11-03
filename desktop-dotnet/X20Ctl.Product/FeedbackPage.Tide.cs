using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
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

        Detach(kinds);
        Detach(controller);
        Detach(connection);
        Detach(Message);
        Detach(counter);
        Detach(SendButton);
        Detach(back);
        Detach(status);

        var frame = new TideToolFrame(
            "SEND FEEDBACK",
            "Tell X20CTL what's broken, what you'd like, or how your controller behaves. It goes straight to the X20CTL inbox.",
            onBack: () => back.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)),
            hints: new[] { "\u24B7 Back to Tools", "\u24B6 Submit Feedback" },
            seed: 20);

        // Left Area: Interactive Feedback Form in Persona 3 Frame (230, 160, w=720)
        var formCard = TideKit.PersonaCard(720, 680, -1);
        var formStack = new StackPanel { Margin = new Thickness(24, 20, 24, 20) };

        formStack.Children.Add(new TextBlock
        {
            Text = "WHAT IS THIS ABOUT?",
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 14,
            Foreground = TideKit.SpikeRedBrush,
            Margin = new Thickness(0, 0, 0, 8)
        });

        // Embed the actual kinds radio buttons panel
        if (kinds is System.Windows.Controls.Primitives.UniformGrid kindGrid) { kindGrid.Rows = 2; kindGrid.Columns = 2; }
        kinds.Margin = new Thickness(0, 0, 0, 16);
        formStack.Children.Add(kinds);

        var dropGrid = new Grid { Margin = new Thickness(0, 0, 0, 14) };
        dropGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        dropGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        dropGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var cStack = new StackPanel();
        cStack.Children.Add(new TextBlock
        {
            Text = "CONTROLLER (OPTIONAL)",
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 12,
            Foreground = TideKit.SubBrush,
            Margin = new Thickness(0, 0, 0, 4)
        });
        controller.Width = double.NaN;
        controller.HorizontalAlignment = HorizontalAlignment.Stretch;
        cStack.Children.Add(controller);
        Grid.SetColumn(cStack, 0);
        dropGrid.Children.Add(cStack);

        var connStack = new StackPanel();
        connStack.Children.Add(new TextBlock
        {
            Text = "CONNECTION (OPTIONAL)",
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 12,
            Foreground = TideKit.SubBrush,
            Margin = new Thickness(0, 0, 0, 4)
        });
        connection.Width = double.NaN;
        connection.HorizontalAlignment = HorizontalAlignment.Stretch;
        connStack.Children.Add(connection);
        Grid.SetColumn(connStack, 2);
        dropGrid.Children.Add(connStack);

        formStack.Children.Add(dropGrid);

        formStack.Children.Add(new TextBlock
        {
            Text = "YOUR MESSAGE",
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 14,
            Foreground = TideKit.SpikeRedBrush,
            Margin = new Thickness(0, 4, 0, 6)
        });

        // Real Message Box styled for Persona 3
        Message.Height = 160;
        Message.Background = TideKit.Sea0Brush;
        Message.Foreground = TideKit.WhiteBrush;
        Message.CaretBrush = TideKit.WhiteBrush;
        Message.BorderBrush = TideKit.WhiteBrush;
        Message.BorderThickness = new Thickness(2);
        Message.FontFamily = TideKit.Body;
        Message.FontSize = 15;
        formStack.Children.Add(Message);

        counter.Foreground = TideKit.SubBrush;
        counter.Margin = new Thickness(0, 4, 0, 14);
        formStack.Children.Add(counter);

        // Actions Row: [ Send to X20CTL ] [ Back ]
        var actRow = new StackPanel { Orientation = Orientation.Horizontal };
        SendButton.Height = 44;
        SendButton.Margin = new Thickness(0, 0, 12, 0);
        actRow.Children.Add(SendButton);

        back.Height = 44;
        actRow.Children.Add(back);
        formStack.Children.Add(actRow);

        status.Foreground = TideKit.SpikeRedBrush;
        status.Margin = new Thickness(0, 10, 0, 0);
        formStack.Children.Add(status);

        formCard.Child = formStack;
        Canvas.SetLeft(formCard, 230);
        Canvas.SetTop(formCard, 160);
        frame.ContentArea.Children.Add(formCard);

        // Right Side: Persona 3 Privacy & Submission Info Card (990, 160, w=540)
        var infoCard = TideKit.PersonaCard(540, 520, 2);
        var infoStack = new StackPanel { Margin = new Thickness(24, 20, 24, 20) };

        infoStack.Children.Add(new TextBlock
        {
            Text = "PRIVACY & TRANSMISSION",
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 14,
            Foreground = TideKit.SpikeRedBrush
        });

        infoStack.Children.Add(new TextBlock
        {
            Text = "WHAT GETS SENT",
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 24,
            Foreground = TideKit.Sea0Brush,
            Margin = new Thickness(0, 2, 0, 10)
        });

        string[] privacyBullets = [
            "Your written message and chosen topic.",
            "Controller model and transport type if selected.",
            "Running X20CTL version and Windows OS build number.",
            "No account is required and no files are uploaded."
        ];

        foreach (var b in privacyBullets)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            row.Children.Add(new TextBlock { Text = "▸ ", FontFamily = TideKit.Display, FontWeight = FontWeights.Black, FontSize = 16, Foreground = TideKit.SpikeRedBrush });
            row.Children.Add(new TextBlock { Text = b, FontFamily = TideKit.Body, FontWeight = FontWeights.SemiBold, FontSize = 13, Foreground = TideKit.InkBrush, TextWrapping = TextWrapping.Wrap, Width = 460 });
            infoStack.Children.Add(row);
        }

        var sticker = TideKit.NoteSticker("DIRECT DEVELOPER INBOX\nGoes directly to Amjad AAYD", -2);
        infoStack.Children.Add(sticker);

        infoCard.Child = infoStack;
        Canvas.SetLeft(infoCard, 990);
        Canvas.SetTop(infoCard, 160);
        frame.ContentArea.Children.Add(infoCard);

        Child = frame;
    }

    internal static void Detach(FrameworkElement? element)
    {
        if (element == null) return;
        if (element.Parent is Panel p) p.Children.Remove(element);
        else if (element.Parent is ContentControl c) c.Content = null;
        else if (element.Parent is Border b) b.Child = null;
        else if (element.Parent is Decorator d) d.Child = null;
    }
}
