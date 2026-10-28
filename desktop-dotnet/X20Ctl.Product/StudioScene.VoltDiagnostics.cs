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
    /// Diagnostics workbench for Volt: Real-time dual stick reticles, circularity check, and 1000Hz polling rate monitor.
    /// Strictly implements t_volt.py:253 Volt and models.py:diagnostics.
    /// </summary>
    private UIElement BuildVoltDiagnostics()
    {
        var content = new Canvas { Width = 970, Height = 560 };

        // 1. Left List: Diagnostics tools & settings (width: 520)
        var leftStack = new StackPanel { Orientation = Orientation.Vertical, Width = 520 };

        leftStack.Children.Add(new TextBlock
        {
            Text = "LIVE DIAGNOSTICS & TELEMETRY",
            FontFamily = VoltKit.Display,
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = VoltKit.B("#8a8a8a"),
            Margin = new Thickness(0, 0, 0, 8)
        });

        var diagRows = new (string Label, string? Sub, string CtlType, string CtlVal, bool Active)[]
        {
            ("Player Input Stream", null, "dd", "XInput 1", true),
            ("Stick Deadzone Visualization", "Real-time circular deflection", "chev", "", false),
            ("Interleaved Raw Telemetry", "Stream raw sensor packet values", "toggle", "false", false),
            ("Centering Offset Compensation", "Hardware dynamic auto-zero", "toggle", "true", false),
            ("Trigger Linearity Calibration", "Hall linear travel verification", "chev", "", false),
            ("Button Latency Benchmark", "Microsecond contact timing", "chev", "", false)
        };

        foreach (var (lbl, sub, ctlType, ctlVal, active) in diagRows)
        {
            var row = new Border
            {
                MinHeight = 62,
                Background = active ? VoltKit.B("#2a2a2a") : Brushes.Transparent,
                BorderBrush = active ? VoltKit.AmberHotBrush : VoltKit.B("#222222"),
                BorderThickness = new Thickness(active ? 2 : 0, active ? 2 : 0, active ? 2 : 0, active ? 2 : 1),
                CornerRadius = active ? new CornerRadius(12) : new CornerRadius(0),
                Padding = new Thickness(16, 10, 16, 10),
                Margin = new Thickness(0, 0, 0, 2),
                Cursor = Cursors.Hand
            };

            var grid = new Grid { VerticalAlignment = VerticalAlignment.Center };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var lCol = new StackPanel { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center };
            lCol.Children.Add(new TextBlock
            {
                Text = lbl,
                FontFamily = VoltKit.Display,
                FontSize = 20,
                FontWeight = FontWeights.SemiBold,
                Foreground = active ? VoltKit.WhiteBrush : VoltKit.LightBrush
            });
            if (!string.IsNullOrEmpty(sub))
            {
                lCol.Children.Add(new TextBlock
                {
                    Text = sub,
                    FontFamily = VoltKit.Body,
                    FontSize = 14,
                    Foreground = VoltKit.MuteBrush,
                    Margin = new Thickness(0, 2, 0, 0)
                });
            }
            Grid.SetColumn(lCol, 0);
            grid.Children.Add(lCol);

            UIElement? ctrl = null;
            if (ctlType == "dd")
            {
                ctrl = new Border
                {
                    MinWidth = 160,
                    Height = 42,
                    CornerRadius = new CornerRadius(8),
                    Background = VoltKit.B("#262626"),
                    Padding = new Thickness(14, 0, 14, 0),
                    Child = new Grid
                    {
                        Children =
                        {
                            new TextBlock
                            {
                                Text = ctlVal,
                                FontFamily = VoltKit.Display,
                                FontSize = 16,
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
            }
            else if (ctlType == "toggle")
            {
                bool on = ctlVal == "true";
                var tg = new Border
                {
                    Width = 54,
                    Height = 30,
                    CornerRadius = new CornerRadius(15),
                    Background = on ? VoltKit.AmberHotBrush : VoltKit.BorderDarkBrush,
                    VerticalAlignment = VerticalAlignment.Center
                };
                var knob = new Ellipse
                {
                    Width = 24,
                    Height = 24,
                    Fill = VoltKit.WhiteBrush,
                    HorizontalAlignment = on ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                    Margin = new Thickness(on ? 0 : 3, 0, on ? 3 : 0, 0)
                };
                tg.Child = knob;
                ctrl = tg;
            }
            else if (ctlType == "chev")
            {
                ctrl = new TextBlock
                {
                    Text = "\uE76C",
                    FontFamily = VoltKit.Icons,
                    FontSize = 14,
                    Foreground = VoltKit.MuteBrush,
                    VerticalAlignment = VerticalAlignment.Center
                };
            }

            if (ctrl != null)
            {
                Grid.SetColumn(ctrl, 1);
                grid.Children.Add(ctrl);
            }

            row.Child = grid;
            leftStack.Children.Add(row);
        }

        Canvas.SetLeft(leftStack, 0);
        Canvas.SetTop(leftStack, 10);
        content.Children.Add(leftStack);

        // 2. Right Details Card: Dual sticks reticle + polling rate (width: 410 at x = 550)
        var rightStack = new StackPanel { Orientation = Orientation.Vertical, Width = 410 };

        // Sticks Reticle Box (#0b0b0b, 410x190)
        var sticksBox = new Border
        {
            Width = 410,
            Height = 190,
            Background = VoltKit.B("#0b0b0b"),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(16)
        };
        var sCanvas = new Canvas { Width = 378, Height = 158 };

        // Left Stick circular reticle
        var leftStickGroup = CreateStickReticle("LEFT STICK (LS)", 0.0, 0.0);
        Canvas.SetLeft(leftStickGroup, 20);
        Canvas.SetTop(leftStickGroup, 10);
        sCanvas.Children.Add(leftStickGroup);

        // Right Stick circular reticle
        var rightStickGroup = CreateStickReticle("RIGHT STICK (RS)", 0.0, 0.0);
        Canvas.SetLeft(rightStickGroup, 210);
        Canvas.SetTop(rightStickGroup, 10);
        sCanvas.Children.Add(rightStickGroup);

        sticksBox.Child = sCanvas;
        rightStack.Children.Add(sticksBox);

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
            Text = "POLLING UPDATE RATE",
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
            Text = "Live telemetry monitor listening to Player 1 input thread. Measured jitter variance is steady within ±0.04 ms with 0 dropped packets.",
            FontFamily = VoltKit.Body,
            FontSize = 15,
            Foreground = VoltKit.B("#bdbdbd"),
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 22,
            Margin = new Thickness(0, 8, 0, 16)
        });

        // Action button [▶ Calibrate Analog Sticks]
        var calBtn = VoltKit.OrangeActionButton("Calibrate Analog Sticks", 362, 54);
        cStack.Children.Add(calBtn);

        card.Child = cStack;
        rightStack.Children.Add(card);

        Canvas.SetLeft(rightStack, 550);
        Canvas.SetTop(rightStack, 10);
        content.Children.Add(rightStack);

        return VoltKit.TwoPaneShell("diagnostics", "Diagnostics", "Real-time analog stick centering, trigger linear resolution, and button response matrix.", content, VoltSelectPage);
    }

    private static UIElement CreateStickReticle(string title, double xVal, double yVal)
    {
        var root = new Canvas { Width = 148, Height = 138 };

        var label = new TextBlock
        {
            Text = title,
            FontFamily = VoltKit.Display,
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Foreground = VoltKit.B("#8a8a8a"),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        Canvas.SetLeft(label, 20);
        Canvas.SetTop(label, 0);
        root.Children.Add(label);

        // Circular boundary
        var circle = new Ellipse
        {
            Width = 100,
            Height = 100,
            Stroke = VoltKit.B("#2a2a2a"),
            StrokeThickness = 2,
            Fill = VoltKit.B("#121212")
        };
        Canvas.SetLeft(circle, 24);
        Canvas.SetTop(circle, 22);
        root.Children.Add(circle);

        // Crosshairs
        var hLine = new System.Windows.Shapes.Line
        {
            X1 = 24,
            Y1 = 72,
            X2 = 124,
            Y2 = 72,
            Stroke = VoltKit.B("#222222"),
            StrokeThickness = 1
        };
        root.Children.Add(hLine);

        var vLine = new System.Windows.Shapes.Line
        {
            X1 = 74,
            Y1 = 22,
            X2 = 74,
            Y2 = 122,
            Stroke = VoltKit.B("#222222"),
            StrokeThickness = 1
        };
        root.Children.Add(vLine);

        // Center dot in #ffad0a
        var dot = new Ellipse
        {
            Width = 12,
            Height = 12,
            Fill = VoltKit.AmberHotBrush,
            Effect = new DropShadowEffect { Color = VoltKit.AmberHot, BlurRadius = 8, ShadowDepth = 0, Opacity = 0.9 }
        };
        Canvas.SetLeft(dot, 74 - 6 + xVal * 40);
        Canvas.SetTop(dot, 72 - 6 - yVal * 40);
        root.Children.Add(dot);

        return root;
    }
}
