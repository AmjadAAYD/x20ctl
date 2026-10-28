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
    private void NexusShowPcMode()
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
            Text = "PC mode",
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
            Text = "Virtual bus routing, XInput emulation, and driver translation layers.",
            FontFamily = NexusKit.Body,
            FontSize = 17,
            Foreground = NexusKit.SubBrush
        };
        Canvas.SetLeft(lead, 70);
        Canvas.SetTop(lead, 192);
        nexusPageHost.Children.Add(lead);

        // 4. Left Rows Column at (68, 250, w=820)
        var rowPanel = new StackPanel { Width = 820 };

        var r1 = NexusKit.RowButton("Enable PC mode", "Translates hardware input via virtual Xbox controller", NexusKit.Toggle(false), focused: false, width: 820);
        rowPanel.Children.Add(r1);

        var r2 = NexusKit.RowButton("Settings for", null, NexusKit.Dropdown("Player 1"), focused: false, width: 820);
        rowPanel.Children.Add(r2);

        var r3 = NexusKit.RowButton("Turbo rate", null, new TextBlock { Text = "20 Hz", FontFamily = NexusKit.Body, FontSize = 17, Foreground = NexusKit.SubBrush, VerticalAlignment = VerticalAlignment.Center }, focused: false, width: 820);
        rowPanel.Children.Add(r3);

        var r4 = NexusKit.RowButton("Game bindings", null, null, focused: false, width: 820);
        rowPanel.Children.Add(r4);

        var r5 = NexusKit.RowButton("I/O configuration", null, null, focused: false, width: 820);
        rowPanel.Children.Add(r5);

        // Focused: Before you begin
        var r6 = NexusKit.RowButton("Before you begin", null, null, focused: true, width: 820);
        rowPanel.Children.Add(r6);

        Canvas.SetLeft(rowPanel, 68);
        Canvas.SetTop(rowPanel, 250);
        nexusPageHost.Children.Add(rowPanel);

        // 5. Right Details Side at (920, 250, w=612)
        var sideStack = new StackPanel { Width = 612 };

        // Visual: Route box
        var routeBox = new Border
        {
            Width = 612, Height = 100,
            CornerRadius = new CornerRadius(8),
            Background = NexusKit.B("#17191c"),
            Padding = new Thickness(20, 16, 20, 16),
            Margin = new Thickness(0, 0, 0, 18)
        };
        var rStack = new StackPanel();
        rStack.Children.Add(new TextBlock
        {
            Text = "Signal Route",
            FontFamily = NexusKit.Body,
            FontSize = 13,
            Foreground = NexusKit.SubBrush,
            Margin = new Thickness(0, 0, 0, 8)
        });

        var chipsStack = new StackPanel { Orientation = Orientation.Horizontal };
        var routeNodes = new[] { "Physical Pad", "HidHide", "X20CTL", "ViGEmBus" };
        for (int i = 0; i < routeNodes.Length; i++)
        {
            var node = new Border
            {
                Padding = new Thickness(14, 6, 14, 6),
                CornerRadius = new CornerRadius(4),
                Background = (i == 0) ? NexusKit.GreenBrush : NexusKit.TileBrush,
                Child = new TextBlock
                {
                    Text = routeNodes[i],
                    FontFamily = NexusKit.Body,
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = (i == 0) ? NexusKit.HouseBrush : NexusKit.WhiteBrush
                }
            };
            chipsStack.Children.Add(node);
            if (i < routeNodes.Length - 1)
            {
                chipsStack.Children.Add(new TextBlock
                {
                    Text = "→",
                    FontFamily = NexusKit.Body,
                    FontSize = 18,
                    Foreground = NexusKit.SubBrush,
                    Margin = new Thickness(8, 2, 8, 0)
                });
            }
        }
        rStack.Children.Add(chipsStack);
        routeBox.Child = rStack;
        sideStack.Children.Add(routeBox);

        // Details card: Before you begin
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
            Text = "Before you begin",
            FontFamily = NexusKit.Display,
            FontSize = 24,
            FontWeight = FontWeights.SemiBold,
            Foreground = NexusKit.WhiteBrush,
            Margin = new Thickness(0, 0, 0, 16)
        });

        var rules = new[]
        {
            "A virtual Xbox 360 controller is created in Windows.",
            "The physical controller is hidden so games only see one pad.",
            "Requires ViGEmBus and HidHide driver components.",
            "Windows may ask for administrator permission during setup."
        };
        for (int i = 0; i < rules.Length; i++)
        {
            var numRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 8) };
            var numBadge = new Border
            {
                Width = 26, Height = 26,
                CornerRadius = new CornerRadius(13),
                Background = NexusKit.InputBrush,
                Margin = new Thickness(0, 0, 12, 0),
                Child = new TextBlock
                {
                    Text = $"{i + 1}",
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 13,
                    FontWeight = FontWeights.Bold,
                    Foreground = NexusKit.WhiteBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            numRow.Children.Add(numBadge);
            numRow.Children.Add(new TextBlock
            {
                Text = rules[i],
                FontFamily = NexusKit.Body,
                FontSize = 15,
                Foreground = NexusKit.B(NexusKit.A(Colors.White, 0.78)),
                VerticalAlignment = VerticalAlignment.Center
            });
            cardStack.Children.Add(numRow);
        }

        var agreeBtn = NexusKit.TheatricalButton("I Understand", primary: true, height: 46, fontSize: 15);
        agreeBtn.Margin = new Thickness(0, 16, 0, 0);
        cardStack.Children.Add(agreeBtn);

        card.Child = cardStack;
        sideStack.Children.Add(card);

        Canvas.SetLeft(sideStack, 920);
        Canvas.SetTop(sideStack, 250);
        nexusPageHost.Children.Add(sideStack);

        // 6. Bottom Hints
        var hints = NexusKit.HintsBar(new[] { ("A", "Change"), ("B", "Back") });
        Canvas.SetRight(hints, 62);
        Canvas.SetTop(hints, 856);
        nexusPageHost.Children.Add(hints);
    }
}
