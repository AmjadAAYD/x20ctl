using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Volt;

namespace X20Ctl.Product;

public partial class StudioScene
{
    /// <summary>
    /// Device workbench for Volt: Hardware capabilities, wireless connection telemetry, and firmware verification.
    /// Strictly implements t_volt.py:253 Volt and models.py:device.
    /// </summary>
    private UIElement BuildVoltDevice()
    {
        var content = new Canvas { Width = 970, Height = 560 };

        // 1. Left List: Device specs & capabilities (width: 520)
        var leftStack = new StackPanel { Orientation = Orientation.Vertical, Width = 520 };

        leftStack.Children.Add(new TextBlock
        {
            Text = "DEVICE TELEMETRY",
            FontFamily = VoltKit.Display,
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = VoltKit.B("#8a8a8a"),
            Margin = new Thickness(0, 0, 0, 8)
        });

        var devRows = new (string Label, string Val, bool Active)[]
        {
            ("Connection", "2.4G Wireless Dongle", true),
            ("Firmware Version", "v1.0.4 (Current)", false),
            ("Battery Level", "100% · Normal Discharge", false),
            ("Polling Frequency", "1000 Hz (1.0ms)", false)
        };

        foreach (var (lbl, val, active) in devRows)
        {
            var row = new Border
            {
                MinHeight = 56,
                Background = active ? VoltKit.B("#2a2a2a") : Brushes.Transparent,
                BorderBrush = active ? VoltKit.AmberHotBrush : VoltKit.B("#222222"),
                BorderThickness = new Thickness(active ? 2 : 0, active ? 2 : 0, active ? 2 : 0, active ? 2 : 1),
                CornerRadius = active ? new CornerRadius(12) : new CornerRadius(0),
                Padding = new Thickness(16, 8, 16, 8),
                Margin = new Thickness(0, 0, 0, 2),
                Cursor = Cursors.Hand
            };

            var grid = new Grid { VerticalAlignment = VerticalAlignment.Center };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var label = new TextBlock
            {
                Text = lbl,
                FontFamily = VoltKit.Display,
                FontSize = 19,
                FontWeight = FontWeights.SemiBold,
                Foreground = active ? VoltKit.WhiteBrush : VoltKit.LightBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(label, 0);
            grid.Children.Add(label);

            var valBlock = new TextBlock
            {
                Text = val,
                FontFamily = VoltKit.Display,
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = active ? VoltKit.AmberHotBrush : VoltKit.B("#bdbdbd"),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(valBlock, 1);
            grid.Children.Add(valBlock);

            row.Child = grid;
            leftStack.Children.Add(row);
        }

        leftStack.Children.Add(new TextBlock
        {
            Text = "HARDWARE CAPABILITIES",
            FontFamily = VoltKit.Display,
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = VoltKit.B("#8a8a8a"),
            Margin = new Thickness(0, 16, 0, 8)
        });

        var capRows = new (string Label, string Val)[]
        {
            ("Analog Sticks", "Hall Effect Magnetic Sensing"),
            ("Linear Triggers", "Hall Linear 256-Step"),
            ("Rear Paddles", "M1 · M2 · M3 · M4 Microswitches"),
            ("Rumble Motors", "Dual Asymmetric Eccentric Motors"),
            ("Motion Sensors", "6-Axis High-Rate Gyroscope")
        };

        foreach (var (lbl, val) in capRows)
        {
            var row = new Border
            {
                MinHeight = 52,
                Background = Brushes.Transparent,
                BorderBrush = VoltKit.B("#222222"),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(16, 6, 16, 6),
                Cursor = Cursors.Hand
            };

            var grid = new Grid { VerticalAlignment = VerticalAlignment.Center };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var label = new TextBlock
            {
                Text = lbl,
                FontFamily = VoltKit.Display,
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Foreground = VoltKit.LightBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(label, 0);
            grid.Children.Add(label);

            var valBlock = new TextBlock
            {
                Text = val,
                FontFamily = VoltKit.Display,
                FontSize = 15,
                Foreground = VoltKit.B("#9a9a9a"),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(valBlock, 1);
            grid.Children.Add(valBlock);

            row.Child = grid;
            leftStack.Children.Add(row);
        }

        Canvas.SetLeft(leftStack, 0);
        Canvas.SetTop(leftStack, 10);
        content.Children.Add(leftStack);

        // 2. Right Details Card (width: 410 at x = 550)
        var rightStack = new StackPanel { Orientation = Orientation.Vertical, Width = 410 };

        // Controller Art Preview Box
        var artBox = new Border
        {
            Width = 410,
            Height = 220,
            Background = VoltKit.B("#0b0b0b"),
            CornerRadius = new CornerRadius(14),
            ClipToBounds = true
        };
        var aCanvas = new Canvas { Width = 410, Height = 220 };

        var padArt = VoltKit.ControllerArt("dune", 340);
        if (padArt != null)
        {
            padArt.Opacity = 0.95;
            padArt.Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 24, ShadowDepth = 12, Opacity = 0.6 };
            Canvas.SetLeft(padArt, 35);
            Canvas.SetTop(padArt, 15);
            aCanvas.Children.Add(padArt);
        }

        // Connection badge on art
        var connBadge = new Border
        {
            Background = VoltKit.AmberHotBrush,
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(10, 4, 10, 4)
        };
        connBadge.Child = new TextBlock
        {
            Text = "2.4G RF · ONLINE",
            FontFamily = VoltKit.Display,
            FontSize = 12,
            FontWeight = FontWeights.ExtraBold,
            Foreground = VoltKit.B("#111111")
        };
        Canvas.SetRight(connBadge, 16);
        Canvas.SetTop(connBadge, 16);
        aCanvas.Children.Add(connBadge);

        artBox.Child = aCanvas;
        rightStack.Children.Add(artBox);

        // Details card
        var card = new Border
        {
            Width = 410,
            Background = VoltKit.SheetDarkBrush,
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(24, 20, 24, 20),
            Margin = new Thickness(0, 16, 0, 0)
        };
        var cStack = new StackPanel { Orientation = Orientation.Vertical };

        cStack.Children.Add(new TextBlock
        {
            Text = "CONNECTION TRANSPORT",
            FontFamily = VoltKit.Display,
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            Foreground = VoltKit.B("#8a8a8a"),
            Margin = new Thickness(0, 0, 0, 2)
        });

        cStack.Children.Add(new TextBlock
        {
            Text = "1000 Hz",
            FontFamily = VoltKit.Display,
            FontSize = 46,
            FontWeight = FontWeights.SemiBold,
            Foreground = VoltKit.AmberHotBrush,
            LineHeight = 48
        });

        cStack.Children.Add(new TextBlock
        {
            Text = "Dedicated 2.4 GHz RF wireless dongle. Proprietary anti-interference frequency hopping guarantees steady 1 ms packet delivery under heavy wireless congestion.",
            FontFamily = VoltKit.Body,
            FontSize = 15,
            Foreground = VoltKit.B("#bdbdbd"),
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 22,
            Margin = new Thickness(0, 8, 0, 16)
        });

        // Action button [▶ Run Hardware Diagnostics]
        var diagBtn = VoltKit.OrangeActionButton("Run Diagnostics", 362, 54, () => VoltSelectPage("diagnostics"));
        cStack.Children.Add(diagBtn);

        card.Child = cStack;
        rightStack.Children.Add(card);

        Canvas.SetLeft(rightStack, 550);
        Canvas.SetTop(rightStack, 10);
        content.Children.Add(rightStack);

        return VoltKit.TwoPaneShell("device", "Device", "Hardware identification, firmware management, and transport telemetry.", content, VoltSelectPage);
    }
}
