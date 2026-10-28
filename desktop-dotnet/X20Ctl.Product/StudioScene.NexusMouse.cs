using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Nexus;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private void NexusShowMouse()
    {
        nexusPageHost!.Children.Clear();

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
        nexusPageHost.Children.Add(bg);

        // 2. Top bar
        var topBar = NexusKit.TopBar(
            player: Player,
            model: Model.Name,
            status: "Offline",
            onZone: () => BackRequested?.Invoke(),
            onSetups: () => ShowManagement("Setups"),
            onPcMode: () => ShowManagement("PcMode"),
            onTools: () => ToolsRequested?.Invoke()
        );
        Canvas.SetLeft(topBar, 62);
        Canvas.SetTop(topBar, 46);
        nexusPageHost.Children.Add(topBar);

        // 3. Title & Lead
        var title = new TextBlock
        {
            Text = "Mouse",
            FontFamily = NexusKit.Display,
            FontSize = 38,
            FontWeight = FontWeights.SemiBold,
            Foreground = NexusKit.WhiteBrush
        };
        Canvas.SetLeft(title, 68);
        Canvas.SetTop(title, 138);
        nexusPageHost.Children.Add(title);

        var lead = new TextBlock
        {
            Text = $"Player 1 · {Model.Name}",
            FontFamily = NexusKit.Body,
            FontSize = 17,
            Foreground = NexusKit.SubBrush
        };
        Canvas.SetLeft(lead, 70);
        Canvas.SetTop(lead, 192);
        nexusPageHost.Children.Add(lead);

        // 4. Left Rows Column at (68, 250, w=820)
        var rowPanel = new StackPanel { Width = 820 };

        var r0 = NexusKit.RowButton("Mouse control", null, NexusKit.Toggle(false), focused: false, width: 820);
        rowPanel.Children.Add(r0);

        var rSource = NexusKit.RowButton("Input source", null, NexusKit.Dropdown("Left stick"), focused: false, width: 820);
        rowPanel.Children.Add(rSource);

        rowPanel.Children.Add(new TextBlock
        {
            Text = "Button mappings · 12 controls",
            FontFamily = NexusKit.Body,
            FontSize = 15,
            Foreground = NexusKit.SubBrush,
            Margin = new Thickness(2, 10, 0, 10)
        });

        // Focused: A button
        var rA = NexusKit.RowButton("A button", null, new TextBlock { Text = "Left click", FontFamily = NexusKit.Body, FontSize = 17, Foreground = NexusKit.SubBrush, VerticalAlignment = VerticalAlignment.Center }, focused: true, width: 820);
        rowPanel.Children.Add(rA);

        var rB = NexusKit.RowButton("B button", null, new TextBlock { Text = "Right click", FontFamily = NexusKit.Body, FontSize = 17, Foreground = NexusKit.SubBrush, VerticalAlignment = VerticalAlignment.Center }, focused: false, width: 820);
        rowPanel.Children.Add(rB);

        var rX = NexusKit.RowButton("X button", null, new TextBlock { Text = "Middle click", FontFamily = NexusKit.Body, FontSize = 17, Foreground = NexusKit.SubBrush, VerticalAlignment = VerticalAlignment.Center }, focused: false, width: 820);
        rowPanel.Children.Add(rX);

        var rY = NexusKit.RowButton("Y button", null, new TextBlock { Text = "Shortcut reserved", FontFamily = NexusKit.Body, FontSize = 17, Foreground = NexusKit.SubBrush, VerticalAlignment = VerticalAlignment.Center }, focused: false, width: 820);
        rowPanel.Children.Add(rY);

        Canvas.SetLeft(rowPanel, 68);
        Canvas.SetTop(rowPanel, 250);
        nexusPageHost.Children.Add(rowPanel);

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
            Text = "Assign control · A",
            FontFamily = NexusKit.Display,
            FontSize = 24,
            FontWeight = FontWeights.SemiBold,
            Foreground = NexusKit.WhiteBrush
        });
        cardStack.Children.Add(new TextBlock
        {
            Text = "Choose the desktop action this button performs when mouse control is active.",
            FontFamily = NexusKit.Body,
            FontSize = 15,
            Foreground = NexusKit.SubBrush,
            Margin = new Thickness(0, 4, 0, 16)
        });

        // Options
        cardStack.Children.Add(new TextBlock { Text = "Clicks", FontFamily = NexusKit.Body, FontSize = 14, Foreground = NexusKit.SubBrush, Margin = new Thickness(0, 4, 0, 8) });
        foreach (var opt in new[] { "Left click", "Right click", "Middle click" })
        {
            bool isSel = (opt == "Left click");
            var optBox = new Border
            {
                Height = 52,
                Padding = new Thickness(18, 0, 18, 0),
                Margin = new Thickness(0, 0, 0, 8),
                CornerRadius = new CornerRadius(6),
                Background = isSel ? NexusKit.BtnFocusedBrush : NexusKit.BtnBrush
            };
            if (isSel)
            {
                optBox.BorderBrush = NexusKit.GreenBrush;
                optBox.BorderThickness = new Thickness(4);
            }
            optBox.Child = new TextBlock
            {
                Text = opt,
                FontFamily = NexusKit.Body,
                FontSize = 18,
                Foreground = NexusKit.WhiteBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            cardStack.Children.Add(optBox);
        }

        card.Child = cardStack;
        sideStack.Children.Add(card);

        Canvas.SetLeft(sideStack, 920);
        Canvas.SetTop(sideStack, 250);
        nexusPageHost.Children.Add(sideStack);

        // 6. Bottom Hints
        var hints = NexusKit.HintsBar(new[] { ("A", "Assign"), ("B", "Back") });
        Canvas.SetRight(hints, 62);
        Canvas.SetTop(hints, 856);
        nexusPageHost.Children.Add(hints);
    }
}
