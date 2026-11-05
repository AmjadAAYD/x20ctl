using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Nocturne;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private void NocturneShowTester()
    {
        if (nocturnePageHost == null) return;
        nocturnePageHost.Children.Clear();

        var board = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };
        board.Children.Add(NocturneKit.CreateCityBackdrop());

        // Header & Sidebar
        board.Children.Add(CreateNocturneHeader("Studio"));
        var sidebar = CreateNocturneSidebar("Tester");
        Canvas.SetLeft(sidebar, 24);
        Canvas.SetTop(sidebar, 80);
        board.Children.Add(sidebar);

        // Content Area
        var content = new Canvas { Width = 1280, Height = 770 };
        Canvas.SetLeft(content, 286);
        Canvas.SetTop(content, 80);

        var titleSp = NocturneKit.NocturneTitle("HARDWARE", "LIVE TESTER", "Interactive hardware visualizer for analog sticks, triggers, and digital inputs.");
        Canvas.SetLeft(titleSp, 20);
        Canvas.SetTop(titleSp, 10);
        content.Children.Add(titleSp);

        // ==========================================
        // LEFT CARD: Analog Sticks & Triggers
        // ==========================================
        var leftSp = new StackPanel { Width = 590 };

        var scopeTitle = new TextBlock
        {
            Text = "ANALOG RETICLES & TRIGGER GAUGES",
            FontFamily = NocturneKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 13,
            Foreground = NocturneKit.VoidTextBrush,
            Margin = new Thickness(0, 0, 0, 10)
        };
        leftSp.Children.Add(scopeTitle);

        var scopeCanvas = new Canvas
        {
            Width = 570,
            Height = 280,
            Background = NocturneKit.NightBrush,
            ClipToBounds = true
        };

        // Left Stick Reticle
        var lsBorder = new Ellipse
        {
            Width = 180,
            Height = 180,
            Stroke = new SolidColorBrush(Color.FromArgb(0x80, 0x5c, 0x80, 0xc8)),
            StrokeThickness = 1.5
        };
        Canvas.SetLeft(lsBorder, 40);
        Canvas.SetTop(lsBorder, 40);
        scopeCanvas.Children.Add(lsBorder);

        var lsCrossH = new Line { X1 = 40, Y1 = 130, X2 = 220, Y2 = 130, Stroke = new SolidColorBrush(Color.FromArgb(0x40, 0x5c, 0x80, 0xc8)), StrokeThickness = 1 };
        var lsCrossV = new Line { X1 = 130, Y1 = 40, X2 = 130, Y2 = 220, Stroke = new SolidColorBrush(Color.FromArgb(0x40, 0x5c, 0x80, 0xc8)), StrokeThickness = 1 };
        scopeCanvas.Children.Add(lsCrossH);
        scopeCanvas.Children.Add(lsCrossV);

        var lsDot = new Rectangle { Width = 14, Height = 14, Fill = NocturneKit.AccentBrush, Stroke = NocturneKit.WhiteBrush, StrokeThickness = 2 };
        Canvas.SetLeft(lsDot, 123);
        Canvas.SetTop(lsDot, 123);
        scopeCanvas.Children.Add(lsDot);

        var lsLabel = new TextBlock
        {
            Text = "LEFT STICK (0, 0)",
            FontFamily = NocturneKit.Display,
            FontWeight = FontWeights.Bold,
            FontStyle = FontStyles.Italic,
            FontSize = 11,
            Foreground = NocturneKit.WhiteBrush,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        Canvas.SetLeft(lsLabel, 80);
        Canvas.SetTop(lsLabel, 235);
        scopeCanvas.Children.Add(lsLabel);

        // Right Stick Reticle
        var rsBorder = new Ellipse
        {
            Width = 180,
            Height = 180,
            Stroke = new SolidColorBrush(Color.FromArgb(0x80, 0x5c, 0x80, 0xc8)),
            StrokeThickness = 1.5
        };
        Canvas.SetLeft(rsBorder, 320);
        Canvas.SetTop(rsBorder, 40);
        scopeCanvas.Children.Add(rsBorder);

        var rsCrossH = new Line { X1 = 320, Y1 = 130, X2 = 500, Y2 = 130, Stroke = new SolidColorBrush(Color.FromArgb(0x40, 0x5c, 0x80, 0xc8)), StrokeThickness = 1 };
        var rsCrossV = new Line { X1 = 410, Y1 = 40, X2 = 410, Y2 = 220, Stroke = new SolidColorBrush(Color.FromArgb(0x40, 0x5c, 0x80, 0xc8)), StrokeThickness = 1 };
        scopeCanvas.Children.Add(rsCrossH);
        scopeCanvas.Children.Add(rsCrossV);

        var rsDot = new Rectangle { Width = 14, Height = 14, Fill = NocturneKit.AccentBrush, Stroke = NocturneKit.WhiteBrush, StrokeThickness = 2 };
        Canvas.SetLeft(rsDot, 403);
        Canvas.SetTop(rsDot, 123);
        scopeCanvas.Children.Add(rsDot);

        var rsLabel = new TextBlock
        {
            Text = "RIGHT STICK (0, 0)",
            FontFamily = NocturneKit.Display,
            FontWeight = FontWeights.Bold,
            FontStyle = FontStyles.Italic,
            FontSize = 11,
            Foreground = NocturneKit.WhiteBrush,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        Canvas.SetLeft(rsLabel, 355);
        Canvas.SetTop(rsLabel, 235);
        scopeCanvas.Children.Add(rsLabel);

        leftSp.Children.Add(scopeCanvas);

        // Trigger bars
        var trigSp = new StackPanel { Margin = new Thickness(0, 16, 0, 0) };
        trigSp.Children.Add(new TextBlock
        {
            Text = "HALL EFFECT TRIGGERS",
            FontFamily = NocturneKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 12,
            Foreground = NocturneKit.VoidTextBrush,
            Margin = new Thickness(0, 0, 0, 8)
        });

        // LT
        var ltRow = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        ltRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
        ltRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ltRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });

        var ltLbl = new TextBlock { Text = "LT", FontFamily = NocturneKit.Display, FontWeight = FontWeights.Bold, Foreground = NocturneKit.VoidTextBrush, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(ltLbl, 0);
        ltRow.Children.Add(ltLbl);

        var ltTrack = new Border { Height = 14, Background = NocturneKit.NightBrush, ClipToBounds = true };
        var ltFill = new Border { Width = 0, HorizontalAlignment = HorizontalAlignment.Left, Background = NocturneKit.AccentBrush };
        ltTrack.Child = ltFill;
        Grid.SetColumn(ltTrack, 1);
        ltRow.Children.Add(ltTrack);

        var ltVal = new TextBlock { Text = "0%", FontFamily = NocturneKit.Body, FontSize = 12, Foreground = NocturneKit.VoidTextBrush, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(ltVal, 2);
        ltRow.Children.Add(ltVal);
        trigSp.Children.Add(ltRow);

        // RT
        var rtRow = new Grid();
        rtRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
        rtRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        rtRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });

        var rtLbl = new TextBlock { Text = "RT", FontFamily = NocturneKit.Display, FontWeight = FontWeights.Bold, Foreground = NocturneKit.VoidTextBrush, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(rtLbl, 0);
        rtRow.Children.Add(rtLbl);

        var rtTrack = new Border { Height = 14, Background = NocturneKit.NightBrush, ClipToBounds = true };
        var rtFill = new Border { Width = 0, HorizontalAlignment = HorizontalAlignment.Left, Background = NocturneKit.AccentBrush };
        rtTrack.Child = rtFill;
        Grid.SetColumn(rtTrack, 1);
        rtRow.Children.Add(rtTrack);

        var rtVal = new TextBlock { Text = "0%", FontFamily = NocturneKit.Body, FontSize = 12, Foreground = NocturneKit.VoidTextBrush, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(rtVal, 2);
        rtRow.Children.Add(rtVal);
        trigSp.Children.Add(rtRow);

        leftSp.Children.Add(trigSp);

        var leftCard = NocturneKit.NocturneWhiteCard(leftSp, 610, 520);
        Canvas.SetLeft(leftCard, 20);
        Canvas.SetTop(leftCard, 130);
        content.Children.Add(leftCard);

        // ==========================================
        // RIGHT CARD: Digital Switch Grid & Actions
        // ==========================================
        var rightSp = new StackPanel { Width = 590 };

        var btnTitle = new TextBlock
        {
            Text = "DIGITAL SWITCH MATRIX",
            FontFamily = NocturneKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 13,
            Foreground = NocturneKit.VoidTextBrush,
            Margin = new Thickness(0, 0, 0, 12)
        };
        rightSp.Children.Add(btnTitle);

        var btnGrid = new WrapPanel { Width = 570 };
        var buttonKeys = new[] { "A", "B", "X", "Y", "LB", "RB", "DPAD UP", "DPAD DOWN", "DPAD LEFT", "DPAD RIGHT", "START", "BACK", "GUIDE", "L3", "R3", "M1", "M2", "M3", "M4" };

        foreach (var bKey in buttonKeys)
        {
            var btnBorder = new Border
            {
                Width = 100,
                Height = 44,
                Background = NocturneKit.NightBrush,
                BorderBrush = NocturneKit.RuleBrush,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(4),
                RenderTransform = NocturneKit.CreateSlant(-12),
                RenderTransformOrigin = new Point(0.5, 0.5)
            };
            var btnText = new TextBlock
            {
                Text = bKey,
                FontFamily = NocturneKit.Display,
                FontWeight = FontWeights.Bold,
                FontStyle = FontStyles.Italic,
                FontSize = 12,
                Foreground = NocturneKit.WhiteBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            btnBorder.Child = btnText;
            btnGrid.Children.Add(btnBorder);
        }
        rightSp.Children.Add(btnGrid);

        var hintBox = new Border
        {
            Background = NocturneKit.NightBrush,
            Margin = new Thickness(4, 24, 4, 16),
            Padding = new Thickness(12),
            RenderTransform = NocturneKit.CreateSlant(-12),
            RenderTransformOrigin = new Point(0.5, 0.5)
        };
        hintBox.Child = new TextBlock
        {
            Text = "Press any button or actuate any stick on the controller to verify response latency and switch contact.",
            FontFamily = NocturneKit.Body,
            FontSize = 12,
            Foreground = NocturneKit.SoftBrush,
            TextWrapping = TextWrapping.Wrap
        };
        rightSp.Children.Add(hintBox);

        var resetBtn = NocturneKit.NocturneButton("Reset Test Matrix", () =>
        {
            NocturneShowTester();
        }, isPrimary: false, width: 200, height: 40);
        resetBtn.HorizontalAlignment = HorizontalAlignment.Right;
        resetBtn.Margin = new Thickness(0, 10, 4, 0);
        rightSp.Children.Add(resetBtn);

        var rightCard = NocturneKit.NocturneWhiteCard(rightSp, 610, 520);
        Canvas.SetLeft(rightCard, 650);
        Canvas.SetTop(rightCard, 130);
        content.Children.Add(rightCard);

        board.Children.Add(content);
        nocturnePageHost.Children.Add(board);
    }
}
