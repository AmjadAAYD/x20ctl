using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Volt;

namespace X20Ctl.Product;

public partial class StudioScene
{
    /// <summary>
    /// Diagnostics workbench for Volt: Hall sensor drift testing, stick circularity, and trigger potentiometer verification.
    /// Strictly implements t_volt.py:253 Volt and models.py:diagnostics.
    /// </summary>
    private UIElement BuildVoltDiagnostics()
    {
        if (Diagnostics is not { } diagnostics)
        {
            var empty = new Canvas { Width = 970, Height = 560 };
            empty.Children.Add(new TextBlock { Text = "Sensor diagnostics unavailable.", Foreground = VoltKit.MuteBrush, FontSize = 18, Margin = new Thickness(40) });
            return VoltKit.TwoPaneShell("diagnostics", "Diagnostics", "Sensor telemetry, analog stick center offset, and hardware jitter.", empty, VoltSelectPage);
        }

        var content = new Canvas { Width = 970, Height = 560 };

        // 1. Left List: Live telemetry rows (width: 520)
        var leftStack = new StackPanel { Orientation = Orientation.Vertical, Width = 520 };

        leftStack.Children.Add(new TextBlock
        {
            Text = "LIVE SENSOR TELEMETRY",
            FontFamily = VoltKit.Display,
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = VoltKit.B("#8a8a8a"),
            Margin = new Thickness(0, 0, 0, 8)
        });

        var diagRows = new (string Label, TextBlock StatsBlock)[]
        {
            ("Left Analog Stick", diagnostics.LeftView.Stats),
            ("Right Analog Stick", diagnostics.RightView.Stats),
            ("Left Trigger (LT)", diagnostics.LtView.Stats),
            ("Right Trigger (RT)", diagnostics.RtView.Stats),
            ("State Changes / Polling", diagnostics.RateBlock)
        };

        foreach (var (lbl, blk) in diagRows)
        {
            var row = new Border
            {
                MinHeight = 52,
                Background = Brushes.Transparent,
                BorderBrush = VoltKit.B("#222222"),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(16, 6, 16, 6)
            };
            var grid = new Grid { VerticalAlignment = VerticalAlignment.Center };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

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

            var valTb = new TextBlock
            {
                FontFamily = VoltKit.Display,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = VoltKit.AmberHotBrush,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            valTb.SetBinding(TextBlock.TextProperty, new Binding("Text") { Source = blk });
            Grid.SetColumn(valTb, 1);
            grid.Children.Add(valTb);

            row.Child = grid;
            leftStack.Children.Add(row);
        }

        // Action Buttons: Measure Rest, Clear Counters, Toggle Raw
        var actRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(16, 18, 0, 0) };

        var measureBtn = new Border
        {
            Height = 42,
            Padding = new Thickness(16, 0, 16, 0),
            CornerRadius = new CornerRadius(8),
            Background = VoltKit.AmberHotBrush,
            Margin = new Thickness(0, 0, 10, 0),
            Child = new TextBlock { Text = "Measure Rest", FontFamily = VoltKit.Display, FontSize = 14, FontWeight = FontWeights.Bold, Foreground = VoltKit.B("#111111"), VerticalAlignment = VerticalAlignment.Center }
        };
        actRow.Children.Add(ThemeAction(measureBtn, "theme:diagnostics:measure", diagnostics.Measure));

        var clearBtn = new Border
        {
            Height = 42,
            Padding = new Thickness(16, 0, 16, 0),
            CornerRadius = new CornerRadius(8),
            Background = VoltKit.B("#262626"),
            Margin = new Thickness(0, 0, 10, 0),
            Child = new TextBlock { Text = "Clear Counters", FontFamily = VoltKit.Display, FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = VoltKit.WhiteBrush, VerticalAlignment = VerticalAlignment.Center }
        };
        actRow.Children.Add(ThemeAction(clearBtn, "theme:diagnostics:clear", diagnostics.ClearAll));

        var rawBtn = new Border
        {
            Height = 42,
            Padding = new Thickness(16, 0, 16, 0),
            CornerRadius = new CornerRadius(8),
            Background = VoltKit.B("#262626"),
            Child = new TextBlock { Text = "Toggle Raw Mode", FontFamily = VoltKit.Display, FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = VoltKit.LightBrush, VerticalAlignment = VerticalAlignment.Center }
        };
        actRow.Children.Add(ThemeAction(rawBtn, "theme:diagnostics:raw", diagnostics.ToggleRaw));

        leftStack.Children.Add(actRow);

        Canvas.SetLeft(leftStack, 0);
        Canvas.SetTop(leftStack, 10);
        content.Children.Add(leftStack);

        // 2. Right Details Card: Real Stick Coordinate Reticles (width: 410 at x = 550)
        var rightStack = new StackPanel { Orientation = Orientation.Vertical, Width = 410 };

        var plotCard = new Border
        {
            Width = 410,
            Background = VoltKit.SheetDarkBrush,
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(20)
        };
        var pStack = new StackPanel { Orientation = Orientation.Vertical };

        pStack.Children.Add(new TextBlock
        {
            Text = "STICK SENSOR TELEMETRY RETICLES",
            FontFamily = VoltKit.Display,
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            Foreground = VoltKit.B("#8a8a8a"),
            Margin = new Thickness(0, 0, 0, 12)
        });

        var reticlesCanvas = new Canvas { Width = 370, Height = 180, ClipToBounds = true };
        // Left Reticle
        var leftRing = new Ellipse { Width = 140, Height = 140, Stroke = VoltKit.B("#333333"), StrokeThickness = 1.5 };
        Canvas.SetLeft(leftRing, 20);
        Canvas.SetTop(leftRing, 20);
        reticlesCanvas.Children.Add(leftRing);

        var ptL = new Ellipse { Width = 12, Height = 12, Fill = VoltKit.AmberHotBrush, Effect = new DropShadowEffect { Color = VoltKit.AmberHot, BlurRadius = 8, ShadowDepth = 0, Opacity = 0.8 } };
        Canvas.SetLeft(ptL, 84);
        Canvas.SetTop(ptL, 84);
        reticlesCanvas.Children.Add(ptL);

        // Right Reticle
        var rightRing = new Ellipse { Width = 140, Height = 140, Stroke = VoltKit.B("#333333"), StrokeThickness = 1.5 };
        Canvas.SetLeft(rightRing, 210);
        Canvas.SetTop(rightRing, 20);
        reticlesCanvas.Children.Add(rightRing);

        var ptR = new Ellipse { Width = 12, Height = 12, Fill = VoltKit.AmberHotBrush, Effect = new DropShadowEffect { Color = VoltKit.AmberHot, BlurRadius = 8, ShadowDepth = 0, Opacity = 0.8 } };
        Canvas.SetLeft(ptR, 274);
        Canvas.SetTop(ptR, 84);
        reticlesCanvas.Children.Add(ptR);

        reticlesCanvas.Loaded += (_, _) =>
        {
            double plotSize = global::X20Ctl.Product.Diagnostics.DiagnosticsWorkbench.PlotSize;
            foreach (var (dot, native, x) in new[] { (ptL, diagnostics.LeftView.Dot, 20.0), (ptR, diagnostics.RightView.Dot, 210.0) })
            {
                dot.Visibility = native.Visibility;
                Canvas.SetLeft(dot, x + 140 * (Canvas.GetLeft(native) + native.Width / 2) / plotSize - dot.Width / 2);
                Canvas.SetTop(dot, 20 + 140 * (Canvas.GetTop(native) + native.Height / 2) / plotSize - dot.Height / 2);
            }
        };

        pStack.Children.Add(reticlesCanvas);

        pStack.Children.Add(new TextBlock
        {
            Text = "Direct stick polling captures circular gate deflection. Roll sticks along boundary to test for outer deadzone clipping.",
            FontFamily = VoltKit.Body,
            FontSize = 13,
            Foreground = VoltKit.B("#bdbdbd"),
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 18,
            Margin = new Thickness(0, 14, 0, 0)
        });

        plotCard.Child = pStack;
        rightStack.Children.Add(plotCard);

        Canvas.SetLeft(rightStack, 550);
        Canvas.SetTop(rightStack, 10);
        content.Children.Add(rightStack);

        return VoltKit.TwoPaneShell("diagnostics", "Diagnostics", "Sensor telemetry, analog stick center offset, and hardware jitter.", content, VoltSelectPage);
    }
}
