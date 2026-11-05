using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Sunrise;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private void SunriseShowDiagnostics()
    {
        if (sunrisePageHost == null) return;
        sunrisePageHost.Children.Clear();

        var board = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };
        board.Children.Add(SunriseKit.CreateHalftoneBackdrop());

        // Header & Sidebar
        board.Children.Add(CreateSunriseHeader("Studio"));
        var sidebar = CreateSunriseSidebar("Diagnostics");
        Canvas.SetLeft(sidebar, 16);
        Canvas.SetTop(sidebar, 80);
        board.Children.Add(sidebar);

        var diag = Diagnostics;

        // Content Area
        var content = new Canvas { Width = 1300, Height = 770 };
        Canvas.SetLeft(content, 272);
        Canvas.SetTop(content, 80);

        var titleSp = SunriseKit.SunriseTitle("PROTOCOL DIAGNOSTICS", "Raw Bluetooth HID packet telemetry, bus latency, and sensor inspection.");
        Canvas.SetLeft(titleSp, 10);
        Canvas.SetTop(titleSp, 10);
        content.Children.Add(titleSp);

        // ==========================================
        // LEFT STICKER CARD: Packet Stream
        // ==========================================
        var leftSp = new StackPanel { Width = 560 };

        // Banner
        var banner = new Border
        {
            Background = SunriseKit.InkBrush,
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 0, 0, 10)
        };
        var bSp = new StackPanel { Orientation = Orientation.Horizontal };
        bSp.Children.Add(new Ellipse { Width = 8, Height = 8, Fill = SunriseKit.OrangeBrush, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center });
        bSp.Children.Add(new TextBlock { Text = "LISTENING ONLY  ·  NOTHING IS WRITTEN TO CONTROLLER", FontFamily = SunriseKit.Display, FontWeight = FontWeights.Bold, FontSize = 11, Foreground = SunriseKit.CreamBrush });
        banner.Child = bSp;
        leftSp.Children.Add(banner);

        // Monospace packet box
        var packetBox = new Border
        {
            Width = 540,
            Height = 350,
            Background = SunriseKit.CreamBrush,
            BorderBrush = SunriseKit.InkBrush,
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(10)
        };
        var pText = new TextBlock
        {
            Text = "[ 0x00 0x01 0x02 0x00 0x80 0x80 0x00 0x00 0x00 0x00 0x00 0x00 ]\n[ 0x00 0x01 0x02 0x00 0x7F 0x81 0x00 0x00 0x00 0x00 0x00 0x00 ]\n[ 0x00 0x01 0x02 0x00 0x80 0x80 0x00 0x00 0x00 0x00 0x00 0x00 ]\n[ 0x00 0x01 0x02 0x00 0x80 0x7E 0x00 0x00 0x00 0x00 0x00 0x00 ]\n[ 0x00 0x01 0x02 0x00 0x80 0x80 0x00 0x00 0x00 0x00 0x00 0x00 ]\n-- Stream listening on standard endpoint --",
            FontFamily = new FontFamily("Consolas, Courier New"),
            FontSize = 11.5,
            Foreground = SunriseKit.InkBrush,
            LineHeight = 18
        };
        packetBox.Child = pText;
        leftSp.Children.Add(packetBox);

        // Action Buttons: Clear & Copy
        var actSp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
        var clearBtn = SunriseKit.SunriseButton("Clear stream", () =>
        {
            diag?.ClearAll();
            SunriseShowDiagnostics();
        }, isPrimary: false, width: 140, height: 36);
        clearBtn.Margin = new Thickness(0, 0, 10, 0);

        var copyBtn = SunriseKit.SunriseButton("Copy report", () =>
        {
        }, isPrimary: false, width: 140, height: 36);

        actSp.Children.Add(clearBtn);
        actSp.Children.Add(copyBtn);
        leftSp.Children.Add(actSp);

        var leftCard = SunriseKit.CreateStickerCard(leftSp, 590, 520, cornerRadius: 22);
        Canvas.SetLeft(leftCard, 10);
        Canvas.SetTop(leftCard, 100);
        content.Children.Add(leftCard);

        // ==========================================
        // RIGHT TV SCREEN: Telemetry & Link Status
        // ==========================================
        var rightSp = new StackPanel { Width = 580 };

        var telemetryTitle = new TextBlock
        {
            Text = "BUS & SENSOR TELEMETRY",
            FontFamily = SunriseKit.Display,
            FontWeight = FontWeights.Black,
            FontSize = 13,
            Foreground = SunriseKit.InkBrush,
            Margin = new Thickness(0, 0, 0, 14)
        };
        rightSp.Children.Add(telemetryTitle);

        var statCards = new (string Label, string Value, string Note)[]
        {
            ("CONTROLLER MODEL", Model.Name.ToUpperInvariant(), "Assigned input hardware"),
            ("LINK PROTOCOL", Device?.TransportText ?? "Standard HID", "Endpoint connection mode"),
            ("PACKET INTEGRITY", "Listening", "Continuous HID report stream"),
            ("BATTERY REPORT", Device?.BatteryText ?? "Not reported", "Device power state")
        };

        foreach (var stat in statCards)
        {
            var statCard = new Border
            {
                Background = SunriseKit.CreamBrush,
                BorderBrush = SunriseKit.InkBrush,
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 0, 0, 10)
            };
            var scGrid = new Grid();
            scGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            scGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });

            var scLeft = new StackPanel();
            scLeft.Children.Add(new TextBlock { Text = stat.Label, FontFamily = SunriseKit.Display, FontWeight = FontWeights.Bold, FontSize = 12, Foreground = SunriseKit.OrangeBrush });
            scLeft.Children.Add(new TextBlock { Text = stat.Note, FontFamily = SunriseKit.Body, FontSize = 11, Foreground = SunriseKit.OliveBrush, Margin = new Thickness(0, 2, 0, 0) });
            Grid.SetColumn(scLeft, 0);
            scGrid.Children.Add(scLeft);

            var scVal = new TextBlock
            {
                Text = stat.Value,
                FontFamily = SunriseKit.Display,
                FontWeight = FontWeights.Black,
                FontSize = 18,
                Foreground = SunriseKit.InkBrush,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(scVal, 1);
            scGrid.Children.Add(scVal);

            statCard.Child = scGrid;
            rightSp.Children.Add(statCard);
        }

        var tvCard = SunriseKit.CreateTvScreenCard(rightSp, 610, 520, withAntennae: true);
        Canvas.SetLeft(tvCard, 630);
        Canvas.SetTop(tvCard, 50);
        content.Children.Add(tvCard);

        board.Children.Add(content);

        // Bottom Bar
        var bBar = CreateSunriseBottomBar();
        Canvas.SetLeft(bBar, 0);
        Canvas.SetTop(bBar, 856);
        board.Children.Add(bBar);

        sunrisePageHost.Children.Add(board);
    }
}
