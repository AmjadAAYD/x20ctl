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
    /// Tester workbench for Volt: Real-time gamepad latency monitor, packet rate test, and button input visualization.
    /// Strictly implements t_volt.py:253 Volt and models.py:tester.
    /// </summary>
    private UIElement BuildVoltTester()
    {
        var content = new Canvas { Width = 970, Height = 560 };

        // 1. Left List: Tester controls and options (width: 520)
        var leftStack = new StackPanel { Orientation = Orientation.Vertical, Width = 520 };

        leftStack.Children.Add(new TextBlock
        {
            Text = "CONTROLLER TEST HARNESS",
            FontFamily = VoltKit.Display,
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = VoltKit.B("#8a8a8a"),
            Margin = new Thickness(0, 0, 0, 8)
        });

        var testRows = new (string Label, string? Sub, string CtlType, string CtlVal, bool Active)[]
        {
            ("Active Input Source", null, "val", "Player 1 (Dune)", false),
            ("Packet Polling Benchmark", "Measure response latency and jitter", "btn", "Start Test", true),
            ("View Detailed Input Map", "Inspect digital & analog states", "chev", "", false),
            ("Controller Perspective", null, "dd", "Front View", false),
            ("Dynamic Anti-Jitter Filter", "Filter microsecond noise", "toggle", "true", false),
            ("Continuous Input Log", "Write timestamps to debug ring", "toggle", "false", false)
        };

        foreach (var (lbl, sub, ctlType, ctlVal, active) in testRows)
        {
            var row = new Border
            {
                MinHeight = 58,
                Background = active ? VoltKit.B("#2a2a2a") : Brushes.Transparent,
                BorderBrush = active ? VoltKit.AmberHotBrush : VoltKit.B("#222222"),
                BorderThickness = new Thickness(active ? 2 : 0, active ? 2 : 0, active ? 2 : 0, active ? 2 : 1),
                CornerRadius = active ? new CornerRadius(12) : new CornerRadius(0),
                Padding = new Thickness(16, 8, 16, 8),
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
                FontSize = 19,
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
            if (ctlType == "val")
            {
                ctrl = new TextBlock
                {
                    Text = ctlVal,
                    FontFamily = VoltKit.Display,
                    FontSize = 16,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = VoltKit.AmberHotBrush,
                    VerticalAlignment = VerticalAlignment.Center
                };
            }
            else if (ctlType == "btn")
            {
                ctrl = new Border
                {
                    Height = 38,
                    Padding = new Thickness(18, 0, 18, 0),
                    CornerRadius = new CornerRadius(8),
                    Background = VoltKit.AmberHotBrush,
                    Child = new TextBlock
                    {
                        Text = ctlVal,
                        FontFamily = VoltKit.Display,
                        FontSize = 15,
                        FontWeight = FontWeights.Bold,
                        Foreground = VoltKit.B("#111111"),
                        VerticalAlignment = VerticalAlignment.Center
                    }
                };
            }
            else if (ctlType == "dd")
            {
                ctrl = new Border
                {
                    MinWidth = 150,
                    Height = 38,
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
                                FontSize = 15,
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

        // 2. Right Details Card: Real-time telemetry readout (width: 410 at x = 550)
        var rightStack = new StackPanel { Orientation = Orientation.Vertical, Width = 410 };

        // Real-time pad art preview
        var artBox = new Border
        {
            Width = 410,
            Height = 190,
            Background = VoltKit.B("#0b0b0b"),
            CornerRadius = new CornerRadius(14),
            ClipToBounds = true
        };
        var aCanvas = new Canvas { Width = 410, Height = 190 };

        var padArt = VoltKit.ControllerArt("dune", 320);
        if (padArt != null)
        {
            padArt.Opacity = 0.95;
            padArt.Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 24, ShadowDepth = 12, Opacity = 0.6 };
            Canvas.SetLeft(padArt, 45);
            Canvas.SetTop(padArt, 10);
            aCanvas.Children.Add(padArt);
        }

        // Live listening badge
        var liveBadge = new Border
        {
            Background = VoltKit.AmberHotBrush,
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(10, 4, 10, 4)
        };
        liveBadge.Child = new TextBlock
        {
            Text = "LIVE CAPTURE",
            FontFamily = VoltKit.Display,
            FontSize = 11,
            FontWeight = FontWeights.ExtraBold,
            Foreground = VoltKit.B("#111111")
        };
        Canvas.SetRight(liveBadge, 16);
        Canvas.SetTop(liveBadge, 14);
        aCanvas.Children.Add(liveBadge);

        artBox.Child = aCanvas;
        rightStack.Children.Add(artBox);

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
            Text = "MEASURED PACKET LATENCY",
            FontFamily = VoltKit.Display,
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            Foreground = VoltKit.B("#8a8a8a"),
            Margin = new Thickness(0, 0, 0, 2)
        });

        cStack.Children.Add(new TextBlock
        {
            Text = "0.98 ms",
            FontFamily = VoltKit.Display,
            FontSize = 46,
            FontWeight = FontWeights.SemiBold,
            Foreground = VoltKit.AmberHotBrush,
            LineHeight = 48
        });

        cStack.Children.Add(new TextBlock
        {
            Text = "Active continuous input sampling at 1000 Hz. Jitter is steady at 0.04 ms with zero frame drops across 50,000 sampled packets.",
            FontFamily = VoltKit.Body,
            FontSize = 15,
            Foreground = VoltKit.B("#bdbdbd"),
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 22,
            Margin = new Thickness(0, 8, 0, 16)
        });

        // Action button [▶ Start Polling Stress Test]
        var stressBtn = VoltKit.OrangeActionButton("Start Stress Test", 362, 54);
        cStack.Children.Add(stressBtn);

        card.Child = cStack;
        rightStack.Children.Add(card);

        Canvas.SetLeft(rightStack, 550);
        Canvas.SetTop(rightStack, 10);
        content.Children.Add(rightStack);

        return VoltKit.TwoPaneShell("tester", "Tester", "Real-time controller latency, packet throughput, and input visualizer.", content, VoltSelectPage);
    }
}
