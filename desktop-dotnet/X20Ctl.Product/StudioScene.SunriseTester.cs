using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Sunrise;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private void SunriseShowTester()
    {
        if (sunrisePageHost == null) return;
        sunrisePageHost.Children.Clear();

        var board = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };
        board.Children.Add(SunriseKit.CreateHalftoneBackdrop());

        // Header & Sidebar
        board.Children.Add(CreateSunriseHeader("Studio"));
        var sidebar = CreateSunriseSidebar("Tester");
        Canvas.SetLeft(sidebar, 16);
        Canvas.SetTop(sidebar, 80);
        board.Children.Add(sidebar);

        // Content Area
        var content = new Canvas { Width = 1300, Height = 770 };
        Canvas.SetLeft(content, 272);
        Canvas.SetTop(content, 80);

        var titleSp = SunriseKit.SunriseTitle("HARDWARE TESTER", "Live visualizer for analog thumbsticks, triggers, and digital switches.");
        Canvas.SetLeft(titleSp, 10);
        Canvas.SetTop(titleSp, 10);
        content.Children.Add(titleSp);

        // ==========================================
        // LEFT STICKER CARD: Analog Reticles & Triggers
        // ==========================================
        var leftSp = new StackPanel { Width = 560 };

        var scopeTitle = new TextBlock
        {
            Text = "ANALOG RETICLES & TRIGGER GAUGES",
            FontFamily = SunriseKit.Display,
            FontWeight = FontWeights.Bold,
            FontSize = 12.5,
            Foreground = SunriseKit.InkBrush,
            Margin = new Thickness(0, 0, 0, 10)
        };
        leftSp.Children.Add(scopeTitle);

        var scopeCanvas = new Canvas
        {
            Width = 540,
            Height = 260,
            Background = SunriseKit.CreamBrush,
            ClipToBounds = true
        };

        // Left Stick Reticle
        var lsBorder = new Ellipse
        {
            Width = 160,
            Height = 160,
            Stroke = SunriseKit.InkBrush,
            StrokeThickness = 2
        };
        Canvas.SetLeft(lsBorder, 40);
        Canvas.SetTop(lsBorder, 40);
        scopeCanvas.Children.Add(lsBorder);

        var lsCrossH = new Line { X1 = 40, Y1 = 120, X2 = 200, Y2 = 120, Stroke = new SolidColorBrush(Color.FromArgb(0x40, 0x1E, 0x1D, 0x1A)), StrokeThickness = 1 };
        var lsCrossV = new Line { X1 = 120, Y1 = 40, X2 = 120, Y2 = 200, Stroke = new SolidColorBrush(Color.FromArgb(0x40, 0x1E, 0x1D, 0x1A)), StrokeThickness = 1 };
        scopeCanvas.Children.Add(lsCrossH);
        scopeCanvas.Children.Add(lsCrossV);

        var lsDot = new Ellipse { Width = 14, Height = 14, Fill = SunriseKit.OrangeBrush, Stroke = SunriseKit.InkBrush, StrokeThickness = 2 };
        Canvas.SetLeft(lsDot, 113);
        Canvas.SetTop(lsDot, 113);
        scopeCanvas.Children.Add(lsDot);

        var lsLabel = new TextBlock
        {
            Text = "LEFT STICK (0, 0)",
            FontFamily = SunriseKit.Display,
            FontWeight = FontWeights.Bold,
            FontSize = 11,
            Foreground = SunriseKit.InkBrush,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        Canvas.SetLeft(lsLabel, 75);
        Canvas.SetTop(lsLabel, 215);
        scopeCanvas.Children.Add(lsLabel);

        // Right Stick Reticle
        var rsBorder = new Ellipse
        {
            Width = 160,
            Height = 160,
            Stroke = SunriseKit.InkBrush,
            StrokeThickness = 2
        };
        Canvas.SetLeft(rsBorder, 320);
        Canvas.SetTop(rsBorder, 40);
        scopeCanvas.Children.Add(rsBorder);

        var rsCrossH = new Line { X1 = 320, Y1 = 120, X2 = 480, Y2 = 120, Stroke = new SolidColorBrush(Color.FromArgb(0x40, 0x1E, 0x1D, 0x1A)), StrokeThickness = 1 };
        var rsCrossV = new Line { X1 = 400, Y1 = 40, X2 = 400, Y2 = 200, Stroke = new SolidColorBrush(Color.FromArgb(0x40, 0x1E, 0x1D, 0x1A)), StrokeThickness = 1 };
        scopeCanvas.Children.Add(rsCrossH);
        scopeCanvas.Children.Add(rsCrossV);

        var rsDot = new Ellipse { Width = 14, Height = 14, Fill = SunriseKit.OrangeBrush, Stroke = SunriseKit.InkBrush, StrokeThickness = 2 };
        Canvas.SetLeft(rsDot, 393);
        Canvas.SetTop(rsDot, 113);
        scopeCanvas.Children.Add(rsDot);

        var rsLabel = new TextBlock
        {
            Text = "RIGHT STICK (0, 0)",
            FontFamily = SunriseKit.Display,
            FontWeight = FontWeights.Bold,
            FontSize = 11,
            Foreground = SunriseKit.InkBrush,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        Canvas.SetLeft(rsLabel, 355);
        Canvas.SetTop(rsLabel, 215);
        scopeCanvas.Children.Add(rsLabel);

        leftSp.Children.Add(scopeCanvas);

        // Triggers
        var trigSp = new StackPanel { Margin = new Thickness(0, 14, 0, 0) };
        trigSp.Children.Add(new TextBlock { Text = "HALL EFFECT ANALOG TRIGGERS", FontFamily = SunriseKit.Display, FontWeight = FontWeights.Bold, FontSize = 11.5, Foreground = SunriseKit.InkBrush, Margin = new Thickness(0, 0, 0, 8) });

        // LT
        var ltRow = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        ltRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
        ltRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ltRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
        ltRow.Children.Add(new TextBlock { Text = "LT", FontFamily = SunriseKit.Display, FontWeight = FontWeights.Bold, Foreground = SunriseKit.InkBrush });

        var ltTrack = new Border { Height = 14, CornerRadius = new CornerRadius(7), Background = SunriseKit.CreamBrush, BorderBrush = SunriseKit.InkBrush, BorderThickness = new Thickness(1.5), ClipToBounds = true };
        var ltFill = new Border { Width = 0, HorizontalAlignment = HorizontalAlignment.Left, Background = SunriseKit.OrangeBrush };
        ltTrack.Child = ltFill;
        Grid.SetColumn(ltTrack, 1);
        ltRow.Children.Add(ltTrack);

        var ltVal = new TextBlock { Text = "0%", FontFamily = SunriseKit.Body, FontSize = 12, Foreground = SunriseKit.InkBrush, HorizontalAlignment = HorizontalAlignment.Right };
        Grid.SetColumn(ltVal, 2);
        ltRow.Children.Add(ltVal);
        trigSp.Children.Add(ltRow);

        // RT
        var rtRow = new Grid();
        rtRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
        rtRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        rtRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
        rtRow.Children.Add(new TextBlock { Text = "RT", FontFamily = SunriseKit.Display, FontWeight = FontWeights.Bold, Foreground = SunriseKit.InkBrush });

        var rtTrack = new Border { Height = 14, CornerRadius = new CornerRadius(7), Background = SunriseKit.CreamBrush, BorderBrush = SunriseKit.InkBrush, BorderThickness = new Thickness(1.5), ClipToBounds = true };
        var rtFill = new Border { Width = 0, HorizontalAlignment = HorizontalAlignment.Left, Background = SunriseKit.OrangeBrush };
        rtTrack.Child = rtFill;
        Grid.SetColumn(rtTrack, 1);
        rtRow.Children.Add(rtTrack);

        var rtVal = new TextBlock { Text = "0%", FontFamily = SunriseKit.Body, FontSize = 12, Foreground = SunriseKit.InkBrush, HorizontalAlignment = HorizontalAlignment.Right };
        Grid.SetColumn(rtVal, 2);
        rtRow.Children.Add(rtVal);
        trigSp.Children.Add(rtRow);

        leftSp.Children.Add(trigSp);

        var leftCard = SunriseKit.CreateStickerCard(leftSp, 590, 520, cornerRadius: 22);
        Canvas.SetLeft(leftCard, 10);
        Canvas.SetTop(leftCard, 100);
        content.Children.Add(leftCard);

        // ==========================================
        // RIGHT TV SCREEN: Digital Switch Matrix
        // ==========================================
        var rightSp = new StackPanel { Width = 580 };

        var btnTitle = new TextBlock
        {
            Text = "DIGITAL SWITCH MATRIX",
            FontFamily = SunriseKit.Display,
            FontWeight = FontWeights.Black,
            FontSize = 13,
            Foreground = SunriseKit.InkBrush,
            Margin = new Thickness(0, 0, 0, 14)
        };
        rightSp.Children.Add(btnTitle);

        var btnGrid = new WrapPanel { Width = 560 };
        var buttonKeys = new[] { "A", "B", "X", "Y", "LB", "RB", "DPAD UP", "DPAD DOWN", "DPAD LEFT", "DPAD RIGHT", "START", "BACK", "GUIDE", "L3", "R3", "M1", "M2", "M3", "M4" };

        foreach (var bKey in buttonKeys)
        {
            var btnBorder = new Border
            {
                Width = 98,
                Height = 42,
                Background = SunriseKit.CreamBrush,
                BorderBrush = SunriseKit.InkBrush,
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(10),
                Margin = new Thickness(4)
            };
            btnBorder.Child = new TextBlock
            {
                Text = bKey,
                FontFamily = SunriseKit.Display,
                FontWeight = FontWeights.Bold,
                FontSize = 11.5,
                Foreground = SunriseKit.InkBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            btnGrid.Children.Add(btnBorder);
        }
        rightSp.Children.Add(btnGrid);

        var resetBtn = SunriseKit.SunriseButton("Reset Test Matrix", () =>
        {
            SunriseShowTester();
        }, isPrimary: true, width: 200, height: 40);
        resetBtn.HorizontalAlignment = HorizontalAlignment.Right;
        resetBtn.Margin = new Thickness(0, 20, 4, 0);
        rightSp.Children.Add(resetBtn);

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
