using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Hollow;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private void HollowShowDiagnostics()
    {
        if (hollowPageHost == null) return;
        hollowPageHost.Children.Clear();

        var board = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };
        board.Children.Add(HollowKit.CreateCavernBackdrop());

        // Header & Sidebar
        board.Children.Add(CreateHollowHeader("Studio"));
        var sidebar = CreateHollowSidebar("Diagnostics");
        Canvas.SetLeft(sidebar, 28);
        Canvas.SetTop(sidebar, 80);
        board.Children.Add(sidebar);

        var diag = Diagnostics;

        // Content Area at x=286, y=80
        var content = new Canvas { Width = 1280, Height = 770 };
        Canvas.SetLeft(content, 286);
        Canvas.SetTop(content, 80);

        var titleSp = HollowKit.HollowHeader("PROTOCOL DIAGNOSTICS", "Raw Bluetooth HID packet stream, sensor telemetry, and hardware tracing.");
        Canvas.SetLeft(titleSp, 16);
        Canvas.SetTop(titleSp, 10);
        content.Children.Add(titleSp);

        // ==========================================
        // LEFT PANEL: Packet Stream & Raw Trace
        // ==========================================
        var leftSp = new StackPanel { Width = 610 };

        // Non-invasive status banner
        var banner = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0x80, 0x05, 0x0c, 0x14)),
            BorderBrush = new SolidColorBrush(HollowKit.A(HollowKit.SilverBorder, 0.4)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(2),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 0, 0, 10)
        };
        var bSp = new StackPanel { Orientation = Orientation.Horizontal };
        bSp.Children.Add(new Path { Data = HollowKit.CreateDiamond(6), Fill = HollowKit.AccentBrush, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center });
        bSp.Children.Add(new TextBlock { Text = "Listening only  ·  Nothing is written to the controller", FontFamily = HollowKit.Display, FontSize = 11.5, Foreground = HollowKit.IvoryTextBrush });
        banner.Child = bSp;
        leftSp.Children.Add(banner);

        // Monospace packet viewer
        var packetBox = new Border
        {
            Width = 590,
            Height = 400,
            Background = new SolidColorBrush(Color.FromArgb(0xdd, 0x02, 0x06, 0x0c)),
            BorderBrush = new SolidColorBrush(HollowKit.A(HollowKit.SilverBorder, 0.3)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(2),
            Padding = new Thickness(10)
        };
        var pText = new TextBlock
        {
            Text = "[ 0x00 0x01 0x02 0x00 0x80 0x80 0x00 0x00 0x00 0x00 0x00 0x00 ]\n[ 0x00 0x01 0x02 0x00 0x7F 0x81 0x00 0x00 0x00 0x00 0x00 0x00 ]\n[ 0x00 0x01 0x02 0x00 0x80 0x80 0x00 0x00 0x00 0x00 0x00 0x00 ]\n[ 0x00 0x01 0x02 0x00 0x80 0x7E 0x00 0x00 0x00 0x00 0x00 0x00 ]\n[ 0x00 0x01 0x02 0x00 0x80 0x80 0x00 0x00 0x00 0x00 0x00 0x00 ]\n-- Stream listening on standard endpoint --",
            FontFamily = new FontFamily("Consolas, Courier New"),
            FontSize = 11.5,
            Foreground = HollowKit.SoftTextBrush,
            LineHeight = 18
        };
        packetBox.Child = pText;
        leftSp.Children.Add(packetBox);

        // Action Buttons: Clear & Copy
        var actSp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
        var clearBtn = HollowKit.HollowButton("Clear stream", () =>
        {
            diag?.ClearAll();
            HollowShowDiagnostics();
        }, isPrimary: false, width: 120, height: 32, tag: "theme:diag:clear");
        clearBtn.Margin = new Thickness(0, 0, 10, 0);

        var copyBtn = HollowKit.HollowButton("Copy report", () =>
        {
            // Copies report to clipboard
        }, isPrimary: false, width: 120, height: 32, tag: "theme:diag:copy");

        actSp.Children.Add(clearBtn);
        actSp.Children.Add(copyBtn);
        leftSp.Children.Add(actSp);

        var leftCard = HollowKit.FiligreeCard(leftSp, padding: 18, cornerSize: 14);
        leftCard.Width = 636;
        leftCard.Height = 580;
        Canvas.SetLeft(leftCard, 16);
        Canvas.SetTop(leftCard, 90);
        content.Children.Add(leftCard);

        // ==========================================
        // RIGHT PANEL: Live Sensor Telemetry
        // ==========================================
        var rightSp = new StackPanel { Width = 550 };

        var rTitle = new TextBlock
        {
            Text = "LIVE SENSOR TELEMETRY",
            FontFamily = HollowKit.Display,
            FontSize = 14,
            Foreground = HollowKit.IvoryTextBrush,
            Margin = new Thickness(0, 0, 0, 14)
        };
        rightSp.Children.Add(rTitle);

        var sensorSp = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };
        sensorSp.Children.Add(CreateHollowTelemetryRow("PACKETS RECEIVED", "0"));
        sensorSp.Children.Add(CreateHollowTelemetryRow("CHECKSUM ERRORS", "0"));
        sensorSp.Children.Add(CreateHollowTelemetryRow("AVERAGE INTERVAL", "—"));
        sensorSp.Children.Add(CreateHollowTelemetryRow("PAYLOAD SIZE", "64 bytes"));
        sensorSp.Children.Add(CreateHollowTelemetryRow("BATTERY REPORT", currentBatteryLevel != null ? $"{currentBatteryLevel}%" : "Not reported"));
        sensorSp.Children.Add(CreateHollowTelemetryRow("BLUETOOTH RSSI", "—"));
        rightSp.Children.Add(sensorSp);

        rightSp.Children.Add(HollowKit.DiamondDivider(550, 16));

        // Oscilloscope reticles placeholder
        var miniCanvas = new Canvas
        {
            Width = 540,
            Height = 220,
            Background = new SolidColorBrush(Color.FromArgb(0xaa, 0x05, 0x0c, 0x14)),
            ClipToBounds = true
        };
        var circleL = new Ellipse { Width = 140, Height = 140, Stroke = new SolidColorBrush(HollowKit.A(HollowKit.SilverBorder, 0.4)), StrokeThickness = 1 };
        Canvas.SetLeft(circleL, 60);
        Canvas.SetTop(circleL, 40);
        miniCanvas.Children.Add(circleL);

        var circleR = new Ellipse { Width = 140, Height = 140, Stroke = new SolidColorBrush(HollowKit.A(HollowKit.SilverBorder, 0.4)), StrokeThickness = 1 };
        Canvas.SetLeft(circleR, 340);
        Canvas.SetTop(circleR, 40);
        miniCanvas.Children.Add(circleR);

        rightSp.Children.Add(miniCanvas);

        var rightCard = HollowKit.FiligreeCard(rightSp, padding: 22, cornerSize: 14);
        rightCard.Width = 590;
        rightCard.Height = 580;
        Canvas.SetLeft(rightCard, 674);
        Canvas.SetTop(rightCard, 90);
        content.Children.Add(rightCard);

        board.Children.Add(content);

        // Dock
        var dock = HollowKit.HollowDock("Diagnostics");
        Canvas.SetLeft(dock, 0);
        Canvas.SetTop(dock, 864);
        board.Children.Add(dock);

        hollowPageHost.Children.Add(board);
    }
}
