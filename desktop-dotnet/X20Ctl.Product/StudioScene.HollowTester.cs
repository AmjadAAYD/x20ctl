using System.Windows.Controls.Primitives;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Hollow;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private void HollowShowTester()
    {
        if (hollowPageHost == null) return;
        hollowPageHost.Children.Clear();

        var board = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };
        board.Children.Add(HollowKit.CreateCavernBackdrop());

        // Header & Sidebar
        board.Children.Add(CreateHollowHeader("Studio"));
        var sidebar = CreateHollowSidebar("Tester");
        Canvas.SetLeft(sidebar, 28);
        Canvas.SetTop(sidebar, 80);
        board.Children.Add(sidebar);

        // Content Area at x=286, y=80
        var content = new Canvas { Width = 1280, Height = 770 };
        Canvas.SetLeft(content, 286);
        Canvas.SetTop(content, 80);

        var titleSp = HollowKit.HollowHeader("LIVE INPUT TESTER", "Diagnostic visualizer for analog reticles, triggers, and physical switches.");
        Canvas.SetLeft(titleSp, 16);
        Canvas.SetTop(titleSp, 10);
        content.Children.Add(titleSp);

        // ==========================================
        // LEFT PANEL: Dual Reticles & Trigger Gauges
        // ==========================================
        var leftSp = new StackPanel { Width = 610 };

        var scopeTitle = new TextBlock
        {
            Text = "ANALOG RETICLES & TRIGGERS",
            FontFamily = HollowKit.Display,
            FontSize = 13,
            Foreground = HollowKit.IvoryTextBrush,
            Margin = new Thickness(0, 0, 0, 10)
        };
        leftSp.Children.Add(scopeTitle);

        var scopeCanvas = new Canvas
        {
            Width = 590,
            Height = 360,
            Background = new SolidColorBrush(Color.FromArgb(0xaa, 0x05, 0x0c, 0x14)),
            ClipToBounds = true
        };

        // Left Stick Reticle
        var lsBorder = new Ellipse
        {
            Width = 200,
            Height = 200,
            Stroke = new SolidColorBrush(HollowKit.A(HollowKit.SilverBorder, 0.5)),
            StrokeThickness = 1
        };
        Canvas.SetLeft(lsBorder, 40);
        Canvas.SetTop(lsBorder, 50);
        scopeCanvas.Children.Add(lsBorder);

        var lsCrossH = new Line { X1 = 40, Y1 = 150, X2 = 240, Y2 = 150, Stroke = new SolidColorBrush(Color.FromArgb(0x30, 0xb8, 0xcc, 0xe0)), StrokeThickness = 1 };
        var lsCrossV = new Line { X1 = 140, Y1 = 50, X2 = 140, Y2 = 250, Stroke = new SolidColorBrush(Color.FromArgb(0x30, 0xb8, 0xcc, 0xe0)), StrokeThickness = 1 };
        scopeCanvas.Children.Add(lsCrossH);
        scopeCanvas.Children.Add(lsCrossV);

        var lsDot = new Path { Data = HollowKit.CreateDiamond(10), Fill = HollowKit.IvoryTextBrush, Stroke = HollowKit.IvoryBorderBrush };
        Canvas.SetLeft(lsDot, 140);
        Canvas.SetTop(lsDot, 150);
        scopeCanvas.Children.Add(lsDot);

        var lsLabel = new TextBlock { Text = "LEFT STICK (0, 0)", FontFamily = HollowKit.Display, FontSize = 11, Foreground = HollowKit.SoftTextBrush, HorizontalAlignment = HorizontalAlignment.Center };
        Canvas.SetLeft(lsLabel, 85);
        Canvas.SetTop(lsLabel, 260);
        scopeCanvas.Children.Add(lsLabel);

        // Right Stick Reticle
        var rsBorder = new Ellipse
        {
            Width = 200,
            Height = 200,
            Stroke = new SolidColorBrush(HollowKit.A(HollowKit.SilverBorder, 0.5)),
            StrokeThickness = 1
        };
        Canvas.SetLeft(rsBorder, 320);
        Canvas.SetTop(rsBorder, 50);
        scopeCanvas.Children.Add(rsBorder);

        var rsCrossH = new Line { X1 = 320, Y1 = 150, X2 = 520, Y2 = 150, Stroke = new SolidColorBrush(Color.FromArgb(0x30, 0xb8, 0xcc, 0xe0)), StrokeThickness = 1 };
        var rsCrossV = new Line { X1 = 420, Y1 = 50, X2 = 420, Y2 = 250, Stroke = new SolidColorBrush(Color.FromArgb(0x30, 0xb8, 0xcc, 0xe0)), StrokeThickness = 1 };
        scopeCanvas.Children.Add(rsCrossH);
        scopeCanvas.Children.Add(rsCrossV);

        var rsDot = new Path { Data = HollowKit.CreateDiamond(10), Fill = HollowKit.IvoryTextBrush, Stroke = HollowKit.IvoryBorderBrush };
        Canvas.SetLeft(rsDot, 420);
        Canvas.SetTop(rsDot, 150);
        scopeCanvas.Children.Add(rsDot);

        var rsLabel = new TextBlock { Text = "RIGHT STICK (0, 0)", FontFamily = HollowKit.Display, FontSize = 11, Foreground = HollowKit.SoftTextBrush, HorizontalAlignment = HorizontalAlignment.Center };
        Canvas.SetLeft(rsLabel, 360);
        Canvas.SetTop(rsLabel, 260);
        scopeCanvas.Children.Add(rsLabel);

        // Trigger vertical gauges at far right
        var ltGauge = new Border
        {
            Width = 14,
            Height = 160,
            Background = new SolidColorBrush(Color.FromArgb(0x60, 0x05, 0x0c, 0x14)),
            BorderBrush = new SolidColorBrush(HollowKit.A(HollowKit.SilverBorder, 0.5)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(1)
        };
        Canvas.SetLeft(ltGauge, 545);
        Canvas.SetTop(ltGauge, 70);
        scopeCanvas.Children.Add(ltGauge);

        var rtGauge = new Border
        {
            Width = 14,
            Height = 160,
            Background = new SolidColorBrush(Color.FromArgb(0x60, 0x05, 0x0c, 0x14)),
            BorderBrush = new SolidColorBrush(HollowKit.A(HollowKit.SilverBorder, 0.5)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(1)
        };
        Canvas.SetLeft(rtGauge, 565);
        Canvas.SetTop(rtGauge, 70);
        scopeCanvas.Children.Add(rtGauge);

        leftSp.Children.Add(scopeCanvas);

        var leftCard = HollowKit.FiligreeCard(leftSp, padding: 18, cornerSize: 14);
        leftCard.Width = 636;
        leftCard.Height = 580;
        Canvas.SetLeft(leftCard, 16);
        Canvas.SetTop(leftCard, 90);
        content.Children.Add(leftCard);

        // ==========================================
        // RIGHT PANEL: Digital Switches & Telemetry Placeholders
        // ==========================================
        var rightSp = new StackPanel { Width = 550 };

        var swTitle = new TextBlock
        {
            Text = "DIGITAL BUTTON MATRIX",
            FontFamily = HollowKit.Display,
            FontSize = 14,
            Foreground = HollowKit.IvoryTextBrush,
            Margin = new Thickness(0, 0, 0, 10)
        };
        rightSp.Children.Add(swTitle);

        // Grid of 16 buttons
        string[] testKeys = ["A", "B", "X", "Y", "LB", "RB", "LT", "RT", "View", "Menu", "L3", "R3", "Up", "Down", "Left", "Right"];
        var btnGrid = new UniformGrid { Columns = 4, Rows = 4, Margin = new Thickness(0, 0, 0, 14) };
        foreach (var k in testKeys)
        {
            var bBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xaa, 0x09, 0x12, 0x1c)),
                BorderBrush = new SolidColorBrush(HollowKit.A(HollowKit.SilverBorder, 0.4)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(8, 10, 8, 10),
                Margin = new Thickness(3)
            };
            bBorder.Child = new TextBlock { Text = k, FontFamily = HollowKit.Display, FontSize = 12, Foreground = HollowKit.SoftTextBrush, HorizontalAlignment = HorizontalAlignment.Center };
            btnGrid.Children.Add(bBorder);
        }
        rightSp.Children.Add(btnGrid);

        rightSp.Children.Add(HollowKit.DiamondDivider(550, 16));

        // Honest Telemetry Placeholders (Rule 2.1 & 5.11: no fabricated benchmarks)
        var teleSp = new StackPanel { Margin = new Thickness(0, 8, 0, 14) };
        teleSp.Children.Add(CreateHollowTelemetryRow("POLLING RATE", "—"));
        teleSp.Children.Add(CreateHollowTelemetryRow("LAST INPUT", "—"));
        teleSp.Children.Add(CreateHollowTelemetryRow("BUTTONS DETECTED", "0 / 17"));
        teleSp.Children.Add(CreateHollowTelemetryRow("STICK TRAVEL", "0.0%"));
        rightSp.Children.Add(teleSp);

        // Reset telemetry button
        var resetBtn = HollowKit.HollowButton("Reset test stats", () =>
        {
            HollowShowTester();
        }, isPrimary: false, height: 34, tag: "theme:tester:reset");
        rightSp.Children.Add(resetBtn);

        var rightCard = HollowKit.FiligreeCard(rightSp, padding: 22, cornerSize: 14);
        rightCard.Width = 590;
        rightCard.Height = 580;
        Canvas.SetLeft(rightCard, 674);
        Canvas.SetTop(rightCard, 90);
        content.Children.Add(rightCard);

        board.Children.Add(content);

        // Dock
        var dock = HollowKit.HollowDock("Tester");
        Canvas.SetLeft(dock, 0);
        Canvas.SetTop(dock, 864);
        board.Children.Add(dock);

        hollowPageHost.Children.Add(board);
    }

    private FrameworkElement CreateHollowTelemetryRow(string label, string value)
    {
        var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var lBlock = new TextBlock { Text = label, FontFamily = HollowKit.Display, FontSize = 11, Foreground = HollowKit.MutedTextBrush };
        var vBlock = new TextBlock { Text = value, FontFamily = HollowKit.Display, FontSize = 11, Foreground = HollowKit.IvoryTextBrush };
        Grid.SetColumn(lBlock, 0);
        Grid.SetColumn(vBlock, 1);

        grid.Children.Add(lBlock);
        grid.Children.Add(vBlock);
        return grid;
    }
}
