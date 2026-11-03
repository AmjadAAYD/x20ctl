using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Nexus;

namespace X20Ctl.Product.Scanner;

public sealed partial class WriteScanView
{
    private void BuildNexusWriteWarning(Panel cards, Button go, Button leave)
    {
        var board = new Canvas { Name = "NexusWriteScan", Width = 1600, Height = 900, ClipToBounds = true };

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
            onTools: () => leave.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent))
        );
        Canvas.SetLeft(topBar, 62);
        Canvas.SetTop(topBar, 46);
        board.Children.Add(topBar);

        // 3. Title & Lead
        var title = new TextBlock
        {
            Text = "Write scan",
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
            Text = "You'll change one setting in KeyLinker while your phone records its Bluetooth traffic. X20CTL then shows which bytes the app sent for that change. One recording teaches one setting.",
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

        rowPanel.Children.Add(new TextBlock
        {
            Text = "Before you start the Write scan",
            FontFamily = NexusKit.Body,
            FontSize = 15,
            Foreground = NexusKit.SubBrush,
            Margin = new Thickness(2, 6, 0, 10)
        });

        // Focused: This can take a long time
        var r1 = NexusKit.RowButton("This can take a long time", null, null, focused: true, width: 820);
        rowPanel.Children.Add(r1);

        var r2 = NexusKit.RowButton("You need a phone", null, null, focused: false, width: 820);
        rowPanel.Children.Add(r2);

        var r3 = NexusKit.RowButton("X20CTL sends nothing", null, null, focused: false, width: 820);
        rowPanel.Children.Add(r3);

        var r4 = NexusKit.RowButton("Read scan or Write scan?", null, null, focused: false, width: 820);
        rowPanel.Children.Add(r4);

        var startBtn = NexusKit.TheatricalButton("I have the time, start", primary: true, height: 48, fontSize: 16);
        startBtn.MouseLeftButtonDown += (_, _) => go.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        rowPanel.Children.Add(startBtn);

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
            Text = "This can take a long time",
            FontFamily = NexusKit.Display,
            FontSize = 24,
            FontWeight = FontWeights.SemiBold,
            Foreground = NexusKit.WhiteBrush
        });
        cardStack.Children.Add(new TextBlock
        {
            Text = "Plan on 20 to 40 minutes the first time: turning on a hidden phone setting, one small test, then getting a file from the phone to this PC. A bug report alone can take a few minutes to make.",
            FontFamily = NexusKit.Body,
            FontSize = 16,
            Foreground = NexusKit.B(NexusKit.A(Colors.White, 0.78)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 16)
        });
        cardStack.Children.Add(new TextBlock
        {
            Text = "Nothing leaves this PC until you press Send online at the end, and you see what goes first. The findings are saved to Documents\\X20CTLWriteScans only when you press Save.",
            FontFamily = NexusKit.Body,
            FontSize = 14,
            Foreground = NexusKit.SubBrush,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 20)
        });

        var btnStack = new StackPanel { Orientation = Orientation.Horizontal };
        var b1 = NexusKit.TheatricalButton("I have the time, start", primary: true, height: 46, fontSize: 15);
        b1.Margin = new Thickness(0, 0, 10, 0);
        b1.MouseLeftButtonDown += (_, _) => go.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        btnStack.Children.Add(b1);

        var b2 = NexusKit.TheatricalButton("Back to Tools", primary: false, height: 46, fontSize: 15);
        b2.MouseLeftButtonDown += (_, _) => leave.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        btnStack.Children.Add(b2);
        cardStack.Children.Add(btnStack);

        card.Child = cardStack;
        sideStack.Children.Add(card);

        Canvas.SetLeft(sideStack, 920);
        Canvas.SetTop(sideStack, 250);
        board.Children.Add(sideStack);

        // 6. Bottom Hints
        var hints = NexusKit.HintsBar(new[] { ("A", "Start"), ("B", "Back") });
        Canvas.SetRight(hints, 62);
        Canvas.SetTop(hints, 856);
        board.Children.Add(hints);

        warning.Child = new Viewbox { Child = board, Stretch = Stretch.Uniform };
    }
}
