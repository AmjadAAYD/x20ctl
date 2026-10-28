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
    private void DuskShowCurves()
    {
        duskPageHost!.Children.Clear();

        var hints = new[]
        {
            ("X", "RESET"),
            ("A", "SELECT"),
            ("B", "BACK")
        };

        var contentCanvas = new Canvas { Width = 1040, Height = 580 };

        // Subtabs across top
        var tabsStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 16) };
        var tabs = new[] { "LEFT STICK", "RIGHT STICK", "LT", "RT" };
        for (int i = 0; i < tabs.Length; i++)
        {
            bool active = i == 0;
            var tab = new Border
            {
                Padding = new Thickness(18, 8, 18, 8),
                CornerRadius = new CornerRadius(18),
                Background = active ? DuskKit.SlateTabOnBrush : Brushes.Transparent,
                Margin = new Thickness(0, 0, 8, 0),
                Cursor = Cursors.Hand
            };
            tab.Child = new TextBlock
            {
                Text = tabs[i],
                FontFamily = DuskKit.Display,
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = active ? DuskKit.WhiteBrush : DuskKit.LightBrush
            };
            tabsStack.Children.Add(tab);
        }

        // Left list (w: 580)
        var leftStack = new StackPanel { Width = 580, Orientation = Orientation.Vertical };
        leftStack.Children.Add(tabsStack);

        leftStack.Children.Add(new TextBlock
        {
            Text = "LEFT STICK",
            FontFamily = DuskKit.Display,
            FontSize = 18,
            FontWeight = FontWeights.ExtraBold,
            Foreground = DuskKit.MuteBrush,
            Margin = new Thickness(0, 0, 0, 4)
        });

        leftStack.Children.Add(new TextBlock
        {
            Text = "LOCAL DRAFT · Not written to controller memory.",
            FontFamily = DuskKit.Body,
            FontSize = 15,
            Foreground = DuskKit.MuteBrush,
            Margin = new Thickness(0, 0, 0, 14)
        });

        var curveFields = new (string Name, int Val, bool Active)[]
        {
            ("Anti-deadzone", 0, false),
            ("Max range", 100, false),
            ("Linearity", 55, true),
            ("Smoothing", 15, false)
        };

        foreach (var (name, val, active) in curveFields)
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

            var label = new TextBlock
            {
                Text = name,
                FontFamily = DuskKit.Display,
                FontSize = 20,
                FontWeight = FontWeights.SemiBold,
                Foreground = active ? DuskKit.WhiteBrush : DuskKit.LightBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(label, 0);
            grid.Children.Add(label);

            // Slider control
            var ctrlStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            var trackCanvas = new Canvas { Width = 220, Height = 20, VerticalAlignment = VerticalAlignment.Center };
            var bgTrack = new Border
            {
                Width = 220,
                Height = 6,
                CornerRadius = new CornerRadius(3),
                Background = DuskKit.B("#3d4450")
            };
            Canvas.SetLeft(bgTrack, 0);
            Canvas.SetTop(bgTrack, 7);
            trackCanvas.Children.Add(bgTrack);

            double fillW = Math.Max(0, 220.0 * (val / 100.0));
            var fillTrack = new Border
            {
                Width = fillW,
                Height = 6,
                CornerRadius = new CornerRadius(3),
                Background = DuskKit.B("#1a9fff")
            };
            Canvas.SetLeft(fillTrack, 0);
            Canvas.SetTop(fillTrack, 7);
            trackCanvas.Children.Add(fillTrack);

            var knob = new Border
            {
                Width = 20,
                Height = 20,
                CornerRadius = new CornerRadius(10),
                Background = DuskKit.WhiteBrush,
                Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 6, ShadowDepth = 2, Opacity = 0.4 }
            };
            Canvas.SetLeft(knob, Math.Clamp(fillW - 10, 0, 200));
            Canvas.SetTop(knob, 0);
            trackCanvas.Children.Add(knob);

            ctrlStack.Children.Add(trackCanvas);

            var valBlock = new TextBlock
            {
                Text = $"{val}%",
                FontFamily = DuskKit.Display,
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = DuskKit.WhiteBrush,
                Width = 64,
                TextAlignment = TextAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(14, 0, 0, 0)
            };
            ctrlStack.Children.Add(valBlock);

            Grid.SetColumn(ctrlStack, 1);
            grid.Children.Add(ctrlStack);

            row.Child = grid;
            leftStack.Children.Add(row);
        }

        Canvas.SetLeft(leftStack, 0);
        Canvas.SetTop(leftStack, 0);
        contentCanvas.Children.Add(leftStack);

        // Right side: Graph + details
        var rightStack = new StackPanel { Width = 380, Orientation = Orientation.Vertical };

        var graphBorder = new Border
        {
            Width = 340,
            Height = 300,
            CornerRadius = new CornerRadius(6),
            Background = DuskKit.B("#141920"),
            BorderBrush = DuskKit.B("#232a33"),
            BorderThickness = new Thickness(1),
            ClipToBounds = true
        };
        var graphCanvas = new Canvas { Width = 340, Height = 300 };

        // Grid lines
        for (int x = 34; x < 340; x += 34)
        {
            var line = new Line { X1 = x, Y1 = 0, X2 = x, Y2 = 300, Stroke = DuskKit.B("rgba(255,255,255,.06)"), StrokeThickness = 1 };
            graphCanvas.Children.Add(line);
        }
        for (int y = 30; y < 300; y += 30)
        {
            var line = new Line { X1 = 0, Y1 = y, X2 = 340, Y2 = y, Stroke = DuskKit.B("rgba(255,255,255,.06)"), StrokeThickness = 1 };
            graphCanvas.Children.Add(line);
        }

        // Diagonal baseline
        var baseLine = new Line { X1 = 0, Y1 = 300, X2 = 340, Y2 = 0, Stroke = DuskKit.B("rgba(255,255,255,.15)"), StrokeThickness = 1.5, StrokeDashArray = new DoubleCollection { 4, 4 } };
        graphCanvas.Children.Add(baseLine);

        // Underlay curve illumination fill
        var fillGeom = new PathGeometry();
        var fillFig = new PathFigure { StartPoint = new Point(0, 300) };
        fillFig.Segments.Add(new BezierSegment(new Point(120, 280), new Point(190, 80), new Point(340, 0), true));
        fillFig.Segments.Add(new LineSegment(new Point(340, 300), true));
        fillFig.IsClosed = true;
        fillGeom.Figures.Add(fillFig);
        var underlay = new Path
        {
            Data = fillGeom,
            Fill = new LinearGradientBrush
            {
                StartPoint = new Point(0.5, 0),
                EndPoint = new Point(0.5, 1),
                GradientStops =
                {
                    new(DuskKit.C("rgba(26,159,255,0.28)"), 0.0),
                    new(DuskKit.C("rgba(26,159,255,0.03)"), 0.8),
                    new(Colors.Transparent, 1.0)
                }
            }
        };
        graphCanvas.Children.Add(underlay);

        // S-curve path with radiant bloom
        var pathGeom = new PathGeometry();
        var fig = new PathFigure { StartPoint = new Point(0, 300) };
        fig.Segments.Add(new BezierSegment(new Point(120, 280), new Point(190, 80), new Point(340, 0), true));
        pathGeom.Figures.Add(fig);

        var curvePath = new Path
        {
            Data = pathGeom,
            Stroke = DuskKit.CyanNeonBrush,
            StrokeThickness = 4,
            Effect = new DropShadowEffect { Color = DuskKit.CyanNeon, BlurRadius = 14, ShadowDepth = 0, Opacity = 0.85 }
        };
        graphCanvas.Children.Add(curvePath);

        // Coordinate projection drop lines
        var dropH = new Line
        {
            X1 = 0, Y1 = 117, X2 = 187, Y2 = 117,
            Stroke = DuskKit.CyanNeonBrush,
            StrokeThickness = 1.0,
            StrokeDashArray = new DoubleCollection { 3, 3 },
            Opacity = 0.70
        };
        graphCanvas.Children.Add(dropH);

        var dropV = new Line
        {
            X1 = 187, Y1 = 117, X2 = 187, Y2 = 300,
            Stroke = DuskKit.CyanNeonBrush,
            StrokeThickness = 1.0,
            StrokeDashArray = new DoubleCollection { 3, 3 },
            Opacity = 0.70
        };
        graphCanvas.Children.Add(dropV);

        // Axis projection readout badges
        var lblOut = new Border
        {
            Padding = new Thickness(6, 2, 6, 2),
            CornerRadius = new CornerRadius(3),
            Background = DuskKit.B("rgba(14,18,24,0.92)"),
            BorderBrush = DuskKit.CyanNeonBrush,
            BorderThickness = new Thickness(1),
            Child = new TextBlock { Text = "OUT: 58%", FontFamily = DuskKit.Display, FontSize = 10, FontWeight = FontWeights.Bold, Foreground = DuskKit.CyanNeonBrush }
        };
        Canvas.SetLeft(lblOut, 6);
        Canvas.SetTop(lblOut, 102);
        graphCanvas.Children.Add(lblOut);

        var lblIn = new Border
        {
            Padding = new Thickness(6, 2, 6, 2),
            CornerRadius = new CornerRadius(3),
            Background = DuskKit.B("rgba(14,18,24,0.92)"),
            BorderBrush = DuskKit.CyanNeonBrush,
            BorderThickness = new Thickness(1),
            Child = new TextBlock { Text = "IN: 55%", FontFamily = DuskKit.Display, FontSize = 10, FontWeight = FontWeights.Bold, Foreground = DuskKit.CyanNeonBrush }
        };
        Canvas.SetLeft(lblIn, 160);
        Canvas.SetTop(lblIn, 276);
        graphCanvas.Children.Add(lblIn);

        // Active point radar pulse ring
        var ptPulse = new Ellipse
        {
            Width = 28,
            Height = 28,
            Stroke = DuskKit.CyanNeonBrush,
            StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection { 3, 2 },
            Opacity = 0.65,
            Effect = new DropShadowEffect { Color = DuskKit.CyanNeon, BlurRadius = 8, ShadowDepth = 0, Opacity = 0.7 }
        };
        Canvas.SetLeft(ptPulse, 173);
        Canvas.SetTop(ptPulse, 103);
        graphCanvas.Children.Add(ptPulse);

        // Active point
        var pt = new Ellipse
        {
            Width = 14,
            Height = 14,
            Fill = DuskKit.WhiteBrush,
            Effect = new DropShadowEffect { Color = DuskKit.CyanNeon, BlurRadius = 10, ShadowDepth = 0, Opacity = 0.95 }
        };
        Canvas.SetLeft(pt, 180);
        Canvas.SetTop(pt, 110);
        graphCanvas.Children.Add(pt);

        // Digital HUD telemetry pill
        var hudTelemetry = new Border
        {
            Padding = new Thickness(10, 4, 10, 4),
            CornerRadius = new CornerRadius(10),
            Background = DuskKit.B("rgba(18,25,34,0.85)"),
            BorderBrush = DuskKit.B("rgba(26,159,255,0.4)"),
            BorderThickness = new Thickness(1),
            Child = new TextBlock
            {
                Text = "IN: 55%  OUT: 58% · DYNAMIC S-CURVE",
                FontFamily = DuskKit.Display,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = DuskKit.CyanNeonBrush
            }
        };
        Canvas.SetLeft(hudTelemetry, 12);
        Canvas.SetTop(hudTelemetry, 12);
        graphCanvas.Children.Add(hudTelemetry);

        // Corner brackets on graph
        graphCanvas.Children.Add(DuskKit.CornerBrackets(340, 300, DuskKit.CyanNeon, 12, 1.2));

        graphBorder.Child = graphCanvas;
        rightStack.Children.Add(graphBorder);

        // Details card with cyan specular border
        var detailCard = new Border
        {
            Background = DuskKit.SlateItemBrush,
            BorderBrush = DuskKit.CyanNeonBrush,
            BorderThickness = new Thickness(3, 1, 1, 1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(20),
            Margin = new Thickness(0, 16, 0, 0),
            Width = 340,
            Effect = new DropShadowEffect { Color = DuskKit.CyanNeon, BlurRadius = 12, ShadowDepth = 0, Opacity = 0.2 }
        };
        var detailStack = new StackPanel { Orientation = Orientation.Vertical };
        detailStack.Children.Add(new TextBlock
        {
            Text = "RESPONSE CURVE",
            FontFamily = DuskKit.Display,
            FontSize = 13,
            FontWeight = FontWeights.ExtraBold,
            Foreground = DuskKit.MuteBrush,
            Margin = new Thickness(0, 0, 0, 4)
        });
        detailStack.Children.Add(new TextBlock
        {
            Text = "Linearity 55% · S-Curve",
            FontFamily = DuskKit.Display,
            FontSize = 21,
            FontWeight = FontWeights.Bold,
            Foreground = DuskKit.WhiteBrush,
            Margin = new Thickness(0, 0, 0, 4)
        });
        detailStack.Children.Add(new TextBlock
        {
            Text = "Gentle center slope for precision sniper aiming, fast edge transition for instant 180° turns.",
            FontFamily = DuskKit.Body,
            FontSize = 14,
            Foreground = DuskKit.LightBrush,
            TextWrapping = TextWrapping.Wrap
        });
        detailCard.Child = detailStack;
        rightStack.Children.Add(detailCard);

        Canvas.SetLeft(rightStack, 640);
        Canvas.SetTop(rightStack, 0);
        contentCanvas.Children.Add(rightStack);

        var shell = DuskKit.SettingsShell(
            activeKey: "curves",
            title: "Stick & Trigger Curves",
            lead: "Tune analog deadzones, response curve linearity, and anti-deadzone.",
            content: contentCanvas,
            hints: hints,
            onNav: key => DuskSelectPage(key)
        );

        duskPageHost.Children.Add(shell);
    }
}
