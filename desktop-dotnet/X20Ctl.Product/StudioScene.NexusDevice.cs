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
    private void NexusShowDevice()
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
            Text = "Device",
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
            Text = "Hardware specifications, firmware status, and physical controller profile.",
            FontFamily = NexusKit.Body,
            FontSize = 17,
            Foreground = NexusKit.SubBrush
        };
        Canvas.SetLeft(lead, 70);
        Canvas.SetTop(lead, 192);
        nexusPageHost.Children.Add(lead);

        // 4. Left Rows Column at (68, 250, w=820)
        var rowPanel = new StackPanel { Width = 820 };

        // Focused: Connection
        var r1 = NexusKit.RowButton("Connection", "2.4GHz Wireless USB Dongle", null, focused: true, width: 820);
        rowPanel.Children.Add(r1);

        var r2 = NexusKit.RowButton("Firmware version", "V1.04 · Up to date", null, focused: false, width: 820);
        rowPanel.Children.Add(r2);

        var r3 = NexusKit.RowButton("Battery level", "84% · Discharging", null, focused: false, width: 820);
        rowPanel.Children.Add(r3);

        rowPanel.Children.Add(new TextBlock
        {
            Text = "Hardware profile",
            FontFamily = NexusKit.Body,
            FontSize = 15,
            Foreground = NexusKit.SubBrush,
            Margin = new Thickness(2, 10, 0, 10)
        });

        var r4 = NexusKit.RowButton("Sticks", "TMR, adjustable tension", null, focused: false, width: 820);
        rowPanel.Children.Add(r4);

        var r5 = NexusKit.RowButton("Triggers", "Hall / microswitch dual stage", null, focused: false, width: 820);
        rowPanel.Children.Add(r5);

        var r6 = NexusKit.RowButton("Rear controls", "M1 · M2 · M3 · M4 · M5 · M6", null, focused: false, width: 820);
        rowPanel.Children.Add(r6);

        Canvas.SetLeft(rowPanel, 68);
        Canvas.SetTop(rowPanel, 250);
        nexusPageHost.Children.Add(rowPanel);

        // 5. Right Details Side at (920, 250, w=612)
        var sideStack = new StackPanel { Width = 612 };

        // Visual: Controller Image
        var padBox = new Border
        {
            Width = 612, Height = 280,
            CornerRadius = new CornerRadius(8),
            Background = NexusKit.B("#17191c"),
            Margin = new Thickness(0, 0, 0, 18),
            ClipToBounds = true
        };
        var pCanvas = new Canvas { Width = 612, Height = 280 };
        var padArt = NexusKit.ControllerArt(Model.Id, 380);
        if (padArt != null)
        {
            padArt.Effect = new DropShadowEffect { BlurRadius = 30, ShadowDepth = 20, Direction = 270, Opacity = 0.55, Color = Colors.Black };
            Canvas.SetLeft(padArt, 116);
            Canvas.SetTop(padArt, 20);
            pCanvas.Children.Add(padArt);
        }
        padBox.Child = pCanvas;
        sideStack.Children.Add(padBox);

        // Details card: Connection
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
            Text = "Connection",
            FontFamily = NexusKit.Display,
            FontSize = 24,
            FontWeight = FontWeights.SemiBold,
            Foreground = NexusKit.WhiteBrush
        });
        cardStack.Children.Add(new TextBlock
        {
            Text = "2.4GHz Wireless",
            FontFamily = NexusKit.Display,
            FontSize = 46,
            FontWeight = FontWeights.SemiBold,
            Foreground = NexusKit.WhiteBrush,
            Margin = new Thickness(0, 4, 0, 8)
        });
        cardStack.Children.Add(new TextBlock
        {
            Text = "Low-latency dongle connection active. Polling rate: 1000 Hz. Verified hardware match.",
            FontFamily = NexusKit.Body,
            FontSize = 16,
            Foreground = NexusKit.B(NexusKit.A(Colors.White, 0.78)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 16)
        });

        var diagBtn = NexusKit.TheatricalButton("Run diagnostics", primary: true, height: 46, fontSize: 15, onClick: () => ShowManagement("Diagnostics"));
        cardStack.Children.Add(diagBtn);

        card.Child = cardStack;
        sideStack.Children.Add(card);

        Canvas.SetLeft(sideStack, 920);
        Canvas.SetTop(sideStack, 250);
        nexusPageHost.Children.Add(sideStack);

        // 6. Bottom Hints
        var hints = NexusKit.HintsBar(new[] { ("A", "Details"), ("B", "Back"), ("Y", "Recalibrate") });
        Canvas.SetRight(hints, 62);
        Canvas.SetTop(hints, 856);
        nexusPageHost.Children.Add(hints);
    }
}
