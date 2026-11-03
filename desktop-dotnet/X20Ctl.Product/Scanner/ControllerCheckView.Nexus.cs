using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Nexus;

namespace X20Ctl.Product.Scanner;

public sealed partial class ControllerCheckView
{
    private void BuildNexusReadWarning(Panel cards, FrameworkElement systemRow, FrameworkElement choices, FrameworkElement linux)
    {
        var board = new Canvas { Name = "NexusCheck", Width = 1600, Height = 900, ClipToBounds = true };

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
            onTools: () => close()
        );
        Canvas.SetLeft(topBar, 62);
        Canvas.SetTop(topBar, 46);
        board.Children.Add(topBar);

        // 3. Title & Lead
        var title = new TextBlock
        {
            Text = "Before you scan",
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
            Text = "This is a full controller scan; you'll be guided through every button, stick and trigger, one step at a time, while X20CTL records exactly what the controller reports. Here is what it does and what to expect.",
            FontFamily = NexusKit.Body,
            FontSize = 17,
            Foreground = NexusKit.SubBrush,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 1440
        };
        Canvas.SetLeft(lead, 70);
        Canvas.SetTop(lead, 192);
        board.Children.Add(lead);

        // 4. Left Rows Column at (68, 250, w=820)
        var rowPanel = new StackPanel { Width = 820 };

        var rSys = NexusKit.RowButton("System", null, NexusKit.Dropdown("This Windows PC"), focused: false, width: 820);
        rowPanel.Children.Add(rSys);

        rowPanel.Children.Add(new TextBlock
        {
            Text = "What the scan does",
            FontFamily = NexusKit.Body,
            FontSize = 15,
            Foreground = NexusKit.SubBrush,
            Margin = new Thickness(2, 10, 0, 10)
        });

        // Focused: Reads, never writes
        var r1 = NexusKit.RowButton("Reads, never writes", null, null, focused: true, width: 820);
        rowPanel.Children.Add(r1);

        var r2 = NexusKit.RowButton("Buttons, sticks and triggers", null, null, focused: false, width: 820);
        rowPanel.Children.Add(r2);

        var r3 = NexusKit.RowButton("Battery", null, null, focused: false, width: 820);
        rowPanel.Children.Add(r3);

        var r4 = NexusKit.RowButton("Stick resolution and report rate", null, null, focused: false, width: 820);
        rowPanel.Children.Add(r4);

        var r5 = NexusKit.RowButton("Lighting (RGB)", null, null, focused: false, width: 820);
        rowPanel.Children.Add(r5);

        Canvas.SetLeft(rowPanel, 68);
        Canvas.SetTop(rowPanel, 250);
        board.Children.Add(rowPanel);

        // 5. Right Details Side at (920, 250, w=612)
        var sideStack = new StackPanel { Width = 612 };

        // Controller silhouette visual
        var padBox = new Border
        {
            Width = 612, Height = 220,
            CornerRadius = new CornerRadius(8),
            Background = NexusKit.B("#17191c"),
            Margin = new Thickness(0, 0, 0, 18),
            ClipToBounds = true
        };
        var pCanvas = new Canvas { Width = 612, Height = 220 };
        var padArt = NexusKit.ControllerArt("x20", 320);
        if (padArt != null)
        {
            padArt.Opacity = 0.85;
            Canvas.SetLeft(padArt, 146);
            Canvas.SetTop(padArt, 10);
            pCanvas.Children.Add(padArt);
        }
        padBox.Child = pCanvas;
        sideStack.Children.Add(padBox);

        // Details card
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
            Text = "Reads, never writes",
            FontFamily = NexusKit.Display,
            FontSize = 24,
            FontWeight = FontWeights.SemiBold,
            Foreground = NexusKit.WhiteBrush
        });
        cardStack.Children.Add(new TextBlock
        {
            Text = "The scan only listens to what the controller already tells Windows. No settings, lighting, vibration or firmware are touched.",
            FontFamily = NexusKit.Body,
            FontSize = 16,
            Foreground = NexusKit.B(NexusKit.A(Colors.White, 0.78)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 16)
        });
        cardStack.Children.Add(new TextBlock
        {
            Text = "Your report: the full ZIP is always saved to Documents\\X20CTLInputReports. \"Scan and send\" also sends a compact summary (controller names, IDs and results, no personal files) to X20CTLADMIN when the scan ends.",
            FontFamily = NexusKit.Body,
            FontSize = 14,
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

        warning.Child = new Viewbox { Child = board, Stretch = Stretch.Uniform };
    }
}
