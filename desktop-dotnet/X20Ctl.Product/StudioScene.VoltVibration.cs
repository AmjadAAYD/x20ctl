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
    /// Vibration workbench for Volt: Steam Deck two-pane settings sheet with dual-motor sliders,
    /// solid orange fill tracks, controller art with vibrating shockwaves, and 46px #ffad0a intensity gauge.
    /// Strictly implements t_volt.py:253 Volt and models.py:vibration.
    /// </summary>
    private UIElement BuildVoltVibration()
    {
        var content = new Canvas { Width = 970, Height = 560 };

        // 1. Left List: Motor intensity sliders (width: 520)
        var leftStack = new StackPanel { Orientation = Orientation.Vertical, Width = 520 };

        leftStack.Children.Add(new TextBlock
        {
            Text = "MOTOR INTENSITY",
            FontFamily = VoltKit.Display,
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = VoltKit.B("#8a8a8a"),
            Margin = new Thickness(0, 0, 0, 8)
        });

        var motors = new (string Name, int Val, bool Active)[]
        {
            ("Left Heavy Motor", 80, true),
            ("Right Light Motor", 65, false),
            ("Left Trigger Haptics", 50, false),
            ("Right Trigger Haptics", 50, false)
        };

        foreach (var (name, val, active) in motors)
        {
            var row = new Border
            {
                Height = 64,
                Background = active ? VoltKit.B("#2a2a2a") : Brushes.Transparent,
                BorderBrush = active ? VoltKit.AmberHotBrush : VoltKit.B("#222222"),
                BorderThickness = new Thickness(active ? 2 : 0, active ? 2 : 0, active ? 2 : 0, active ? 2 : 1),
                CornerRadius = active ? new CornerRadius(12) : new CornerRadius(0),
                Padding = new Thickness(16, 0, 16, 0),
                Cursor = Cursors.Hand
            };

            var grid = new Grid { VerticalAlignment = VerticalAlignment.Center };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var label = new TextBlock
            {
                Text = name,
                FontFamily = VoltKit.Display,
                FontSize = 21,
                FontWeight = FontWeights.SemiBold,
                Foreground = active ? VoltKit.WhiteBrush : VoltKit.LightBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(label, 0);
            grid.Children.Add(label);

            // Slider control: 170px width, #3b3b3b track, #ffad0a fill, 26px white knob
            var ctrlStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            var trackCanvas = new Canvas { Width = 170, Height = 26, VerticalAlignment = VerticalAlignment.Center };
            var bgTrack = new Border
            {
                Width = 170,
                Height = 8,
                CornerRadius = new CornerRadius(4),
                Background = VoltKit.BorderDarkBrush
            };
            Canvas.SetLeft(bgTrack, 0);
            Canvas.SetTop(bgTrack, 9);
            trackCanvas.Children.Add(bgTrack);

            double fillW = Math.Max(0, 170.0 * (val / 100.0));
            var fillTrack = new Border
            {
                Width = fillW,
                Height = 8,
                CornerRadius = new CornerRadius(4),
                Background = VoltKit.AmberHotBrush
            };
            Canvas.SetLeft(fillTrack, 0);
            Canvas.SetTop(fillTrack, 9);
            trackCanvas.Children.Add(fillTrack);

            var knob = new Border
            {
                Width = 26,
                Height = 26,
                CornerRadius = new CornerRadius(13),
                Background = VoltKit.WhiteBrush,
                Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 8, ShadowDepth = 2, Opacity = 0.5 }
            };
            Canvas.SetLeft(knob, Math.Clamp(fillW - 13, 0, 144));
            Canvas.SetTop(knob, 0);
            trackCanvas.Children.Add(knob);

            ctrlStack.Children.Add(trackCanvas);

            var valBlock = new TextBlock
            {
                Text = $"{val}%",
                FontFamily = VoltKit.Display,
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Foreground = active ? VoltKit.AmberHotBrush : VoltKit.LightBrush,
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

        // Response damping options row
        var dampRow = new Border
        {
            Height = 64,
            Background = Brushes.Transparent,
            BorderBrush = VoltKit.B("#222222"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(16, 0, 16, 0),
            Cursor = Cursors.Hand
        };
        var dGrid = new Grid { VerticalAlignment = VerticalAlignment.Center };
        dGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        dGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var dLabel = new TextBlock
        {
            Text = "Response Damping Curve",
            FontFamily = VoltKit.Display,
            FontSize = 21,
            FontWeight = FontWeights.SemiBold,
            Foreground = VoltKit.LightBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(dLabel, 0);
        dGrid.Children.Add(dLabel);

        var dDropdown = new Border
        {
            MinWidth = 170,
            Height = 44,
            CornerRadius = new CornerRadius(8),
            Background = VoltKit.B("#262626"),
            Padding = new Thickness(16, 0, 16, 0),
            Child = new Grid
            {
                Children =
                {
                    new TextBlock
                    {
                        Text = "Logarithmic Smooth",
                        FontFamily = VoltKit.Display,
                        FontSize = 17,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = VoltKit.WhiteBrush,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Center
                    },
                    new TextBlock
                    {
                        Text = "\uE70D",
                        FontFamily = VoltKit.Icons,
                        FontSize = 11,
                        Foreground = VoltKit.MuteBrush,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                }
            }
        };
        Grid.SetColumn(dDropdown, 1);
        dGrid.Children.Add(dDropdown);
        dampRow.Child = dGrid;
        leftStack.Children.Add(dampRow);

        Canvas.SetLeft(leftStack, 0);
        Canvas.SetTop(leftStack, 10);
        content.Children.Add(leftStack);

        // 2. Right Details Card (width: 410 at x = 550)
        var rightStack = new StackPanel { Orientation = Orientation.Vertical, Width = 410 };

        // Controller Rumble Illustration Box (#0b0b0b)
        var visualBox = new Border
        {
            Width = 410,
            Height = 220,
            Background = VoltKit.B("#0b0b0b"),
            CornerRadius = new CornerRadius(14),
            ClipToBounds = true
        };
        var vCanvas = new Canvas { Width = 410, Height = 220 };

        var padArt = VoltKit.ControllerArt("dune", 340);
        if (padArt != null)
        {
            var pGlow = VoltKit.PedestalGlow(280, 80, VoltKit.AmberHot);
            Canvas.SetLeft(pGlow, 65);
            Canvas.SetTop(pGlow, 120);
            vCanvas.Children.Add(pGlow);

            padArt.Opacity = 0.95;
            padArt.Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 24, ShadowDepth = 12, Opacity = 0.6 };
            Canvas.SetLeft(padArt, 35);
            Canvas.SetTop(padArt, 15);
            vCanvas.Children.Add(padArt);
        }

        // Inner & outer shockwaves radiating from left heavy grip
        var leftShockOuter = new Ellipse
        {
            Width = 86,
            Height = 146,
            Stroke = VoltKit.B("rgba(255,173,10,0.4)"),
            StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection { 5, 4 },
            Effect = new DropShadowEffect { Color = VoltKit.AmberHot, BlurRadius = 16, ShadowDepth = 0, Opacity = 0.6 }
        };
        Canvas.SetLeft(leftShockOuter, 16);
        Canvas.SetTop(leftShockOuter, 38);
        vCanvas.Children.Add(leftShockOuter);

        var leftShock = new Ellipse
        {
            Width = 60,
            Height = 110,
            Stroke = VoltKit.AmberHotBrush,
            StrokeThickness = 2.5,
            StrokeDashArray = new DoubleCollection { 4, 3 },
            Effect = new DropShadowEffect { Color = VoltKit.AmberHot, BlurRadius = 14, ShadowDepth = 0, Opacity = 0.9 }
        };
        Canvas.SetLeft(leftShock, 30);
        Canvas.SetTop(leftShock, 55);
        vCanvas.Children.Add(leftShock);

        // Inner & outer shockwaves radiating from right light grip
        var rightShockOuter = new Ellipse
        {
            Width = 86,
            Height = 146,
            Stroke = VoltKit.B("rgba(255,228,20,0.35)"),
            StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection { 5, 4 },
            Effect = new DropShadowEffect { Color = VoltKit.C("#ffe414"), BlurRadius = 16, ShadowDepth = 0, Opacity = 0.6 }
        };
        Canvas.SetLeft(rightShockOuter, 306);
        Canvas.SetTop(rightShockOuter, 38);
        vCanvas.Children.Add(rightShockOuter);

        var rightShock = new Ellipse
        {
            Width = 60,
            Height = 110,
            Stroke = VoltKit.B("#ffe414"),
            StrokeThickness = 2.5,
            StrokeDashArray = new DoubleCollection { 4, 3 },
            Effect = new DropShadowEffect { Color = VoltKit.C("#ffe414"), BlurRadius = 14, ShadowDepth = 0, Opacity = 0.9 }
        };
        Canvas.SetLeft(rightShock, 320);
        Canvas.SetTop(rightShock, 55);
        vCanvas.Children.Add(rightShock);

        visualBox.Child = vCanvas;
        rightStack.Children.Add(visualBox);

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
            Text = "HEAVY MOTOR INTENSITY",
            FontFamily = VoltKit.Display,
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            Foreground = VoltKit.B("#8a8a8a"),
            Margin = new Thickness(0, 0, 0, 2)
        });

        cStack.Children.Add(new TextBlock
        {
            Text = "80%",
            FontFamily = VoltKit.Display,
            FontSize = 46,
            FontWeight = FontWeights.SemiBold,
            Foreground = VoltKit.AmberHotBrush,
            LineHeight = 48
        });

        cStack.Children.Add(new TextBlock
        {
            Text = "Independent asymmetric dual-motor rumble. The high-torque left motor delivers deep mechanical explosion pulses, while the high-frequency right motor renders fine terrain texture vibrations.",
            FontFamily = VoltKit.Body,
            FontSize = 15,
            Foreground = VoltKit.B("#bdbdbd"),
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 22,
            Margin = new Thickness(0, 8, 0, 16)
        });

        // Action button [▶ Test Rumble]
        var testBtn = VoltKit.OrangeActionButton("Test Rumble", 362, 54);
        cStack.Children.Add(testBtn);

        card.Child = cStack;
        rightStack.Children.Add(card);

        Canvas.SetLeft(rightStack, 550);
        Canvas.SetTop(rightStack, 10);
        content.Children.Add(rightStack);

        return VoltKit.TwoPaneShell("vibration", "Vibration", "Adjust dual-rumble motor intensity and feedback curves.", content, VoltSelectPage);
    }
}
