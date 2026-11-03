using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Dusk;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private bool duskDeviceWired;
    private void DuskShowDevice()
    {
        duskPageHost!.Children.Clear();
        if (!duskDeviceWired && Device != null) { duskDeviceWired = true; Device.Watch(DuskLater); }

        var hints = new[]
        {
            ("X", "CALIBRATE"),
            ("A", "SELECT"),
            ("B", "BACK")
        };

        var contentCanvas = new Canvas { Width = 1040, Height = 580 };

        // Left list (w: 580)
        var leftStack = new StackPanel { Width = 580, Orientation = Orientation.Vertical };

        leftStack.Children.Add(new TextBlock
        {
            Text = "HARDWARE TELEMETRY",
            FontFamily = DuskKit.Display,
            FontSize = 18,
            FontWeight = FontWeights.ExtraBold,
            Foreground = DuskKit.MuteBrush,
            Margin = new Thickness(0, 0, 0, 4)
        });

        leftStack.Children.Add(new TextBlock
        {
            Text = "Only values reported by the active device are shown.",
            FontFamily = DuskKit.Body,
            FontSize = 15,
            Foreground = DuskKit.MuteBrush,
            Margin = new Thickness(0, 0, 0, 14)
        });

        var deviceSpecs = new (string Label, string Val, bool Active)[]
        {
            ("Firmware", Device?.FirmwareText ?? "Not reported", true),
            ("Hardware ID", Device?.RevisionText ?? "Not reported", false),
            ("Battery", Device?.BatteryText ?? "Not reported", false),
            ("Connection", Device?.ConnectionText ?? "Not detected", false),
            ("Transport", Device?.TransportText ?? "Not reported", false)
        };

        foreach (var (label, val, active) in deviceSpecs)
        {
            var row = new Border
            {
                Height = 60,
                BorderThickness = new Thickness(0, 0, 0, 1),
                BorderBrush = DuskKit.B("rgba(255,255,255,.07)"),
                Background = active ? DuskKit.B("rgba(255,255,255,.05)") : Brushes.Transparent,
                Padding = new Thickness(16, 0, 16, 0),
                CornerRadius = new CornerRadius(4)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var lbl = new TextBlock
            {
                Text = label,
                FontFamily = DuskKit.Display,
                FontSize = 20,
                FontWeight = FontWeights.SemiBold,
                Foreground = active ? DuskKit.WhiteBrush : DuskKit.LightBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(lbl, 0);
            grid.Children.Add(lbl);

            var valBlock = new TextBlock
            {
                Text = val,
                FontFamily = DuskKit.Display,
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = active ? DuskKit.WhiteBrush : DuskKit.MuteBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(valBlock, 1);
            grid.Children.Add(valBlock);

            row.Child = grid;
            leftStack.Children.Add(row);
        }

        Canvas.SetLeft(leftStack, 0);
        Canvas.SetTop(leftStack, 0);
        contentCanvas.Children.Add(leftStack);

        // Right side: Calibration action card + specifications
        var rightStack = new StackPanel { Width = 380, Orientation = Orientation.Vertical };

        var calCard = new Border
        {
            Width = 360,
            CornerRadius = new CornerRadius(6),
            Background = DuskKit.SlateItemBrush,
            Padding = new Thickness(22)
        };
        var calStack = new StackPanel { Orientation = Orientation.Vertical };
        calStack.Children.Add(new TextBlock
        {
            Text = "DEVICE TOOLS",
            FontFamily = DuskKit.Display,
            FontSize = 13,
            FontWeight = FontWeights.ExtraBold,
            Foreground = DuskKit.MuteBrush,
            Margin = new Thickness(0, 0, 0, 14)
        });

        var calSticksBtn = new Border
        {
            Height = 48,
            CornerRadius = new CornerRadius(4),
            Background = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0.5),
                EndPoint = new Point(1, 0.5),
                GradientStops = { new(DuskKit.GreenGrad0, 0.0), new(DuskKit.GreenGrad1, 1.0) }
            },
            Cursor = Cursors.Hand,
            Margin = new Thickness(0, 0, 0, 10)
        };
        calSticksBtn.Child = new TextBlock
        {
            Text = "Open Tester",
            FontFamily = DuskKit.Display,
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Foreground = DuskKit.WhiteBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        calStack.Children.Add(ThemeAction(calSticksBtn, "theme:device:tester", () => ShowManagement("Tester")));

        var calTrigBtn = new Border
        {
            Height = 48,
            CornerRadius = new CornerRadius(4),
            Background = DuskKit.SlateTabOnBrush,
            Cursor = Cursors.Hand
        };
        calTrigBtn.Child = new TextBlock
        {
            Text = "Saved setups",
            FontFamily = DuskKit.Display,
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Foreground = DuskKit.WhiteBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        calStack.Children.Add(ThemeAction(calTrigBtn, "theme:device:setups", () => ShowManagement("Setups")));

        calCard.Child = calStack;
        rightStack.Children.Add(calCard);

        // Warranty & identity card
        var identCard = new Border
        {
            Background = DuskKit.SlateItemBrush,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(20),
            Margin = new Thickness(0, 16, 0, 0),
            Width = 360
        };
        var iStack = new StackPanel { Orientation = Orientation.Vertical };
        iStack.Children.Add(new TextBlock
        {
            Text = "FACTORY IDENTITY",
            FontFamily = DuskKit.Display,
            FontSize = 13,
            FontWeight = FontWeights.ExtraBold,
            Foreground = DuskKit.MuteBrush,
            Margin = new Thickness(0, 0, 0, 4)
        });
        iStack.Children.Add(new TextBlock
        {
            Text = Model.Name,
            FontFamily = DuskKit.LogoFont,
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            Foreground = DuskKit.WhiteBrush,
            Margin = new Thickness(0, 0, 0, 4)
        });
        iStack.Children.Add(new TextBlock
        {
            Text = "Native configuration writes and calibration commands remain locked.",
            FontFamily = DuskKit.Body,
            FontSize = 14,
            Foreground = DuskKit.LightBrush,
            TextWrapping = TextWrapping.Wrap
        });
        identCard.Child = iStack;
        rightStack.Children.Add(identCard);

        Canvas.SetLeft(rightStack, 640);
        Canvas.SetTop(rightStack, 0);
        contentCanvas.Children.Add(rightStack);

        var shell = DuskKit.SettingsShell(
            activeKey: "device",
            title: "Device Information",
            lead: "Reported hardware state and local device tools.",
            content: contentCanvas,
            hints: hints,
            onNav: key => DuskSelectPage(key)
        );

        duskPageHost.Children.Add(shell);
    }
}
