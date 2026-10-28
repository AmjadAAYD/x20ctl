using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Hybrid;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private void HybridShowPcMode()
    {
        hybridPageHost!.Children.Clear();
        hybridPageHost.Children.Add(BuildHybridPcMode());
    }

    private FrameworkElement BuildHybridPcMode()
    {
        var root = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };

        var hints = new (string Key, string Label)[]
        {
            ("A", "Apply mode"),
            ("X", "Calibrate"),
            ("B", "Back")
        };

        // Handheld rails background
        root.Children.Add(HybridKit.HandheldRails("pcmode", hints, "PC Mode & Protocol", "Switch between XInput, DirectInput, and Nintendo Switch communication profiles.", key =>
        {
            HybridSelectPage(key);
        }));

        // Snap block chain for mode selection at (216, 150)
        var modes = new (string Name, string Sub, bool Active)[]
        {
            ("DirectInput", "Legacy PC", false),
            ("XInput", "Standard Xbox", true),
            ("Nintendo Switch", "Console / Pro", false)
        };

        double bx = 216;
        for (int i = 0; i < modes.Length; i++)
        {
            var (name, sub, active) = modes[i];
            var block = new Border
            {
                Width = 186,
                Height = 64,
                CornerRadius = new CornerRadius(14),
                Background = active ? HybridKit.MauveBrush : HybridKit.B("#1f1c25"),
                BorderBrush = active ? HybridKit.CyanBrush : HybridKit.B("#34303c"),
                BorderThickness = new Thickness(active ? 2 : 1),
                Cursor = System.Windows.Input.Cursors.Hand,
                Padding = new Thickness(14, 10, 14, 10),
                Child = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    VerticalAlignment = VerticalAlignment.Center,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = name,
                            FontFamily = HybridKit.Display,
                            FontSize = 15,
                            FontWeight = FontWeights.Bold,
                            Foreground = active ? HybridKit.WhiteBrush : HybridKit.B("#d5d0df")
                        },
                        new TextBlock
                        {
                            Text = sub,
                            FontFamily = HybridKit.Body,
                            FontSize = 12,
                            Foreground = active ? HybridKit.CyanBrush : HybridKit.MuteBrush
                        }
                    }
                }
            };
            if (active)
            {
                block.Effect = new DropShadowEffect { Color = HybridKit.Cyan, BlurRadius = 12, ShadowDepth = 0, Opacity = 0.6 };
            }

            Canvas.SetLeft(block, bx);
            Canvas.SetTop(block, 150);
            root.Children.Add(block);

            // Tab connector
            if (i < modes.Length - 1)
            {
                var connector = new Border
                {
                    Width = 14,
                    Height = 20,
                    Background = active ? HybridKit.CyanBrush : HybridKit.B("#34303c"),
                    CornerRadius = new CornerRadius(0, 4, 4, 0)
                };
                Canvas.SetLeft(connector, bx + 186);
                Canvas.SetTop(connector, 172);
                root.Children.Add(connector);
            }

            bx += 196;
        }

        // Active indicator line at (216, 226)
        var statusLine = new StackPanel { Orientation = Orientation.Horizontal };
        for (int i = 0; i < 4; i++)
        {
            var dash = new Border
            {
                Width = 16,
                Height = 5,
                CornerRadius = new CornerRadius(2),
                Margin = new Thickness(i == 0 ? 0 : 4, 0, 0, 0),
                Background = i == 0 ? HybridKit.CyanBrush : HybridKit.B("#4d4758")
            };
            statusLine.Children.Add(dash);
        }
        statusLine.Children.Add(new TextBlock
        {
            Text = "PLAYER 1 · XInput protocol · 1000 Hz polling · Zero latency buffer",
            FontFamily = HybridKit.Body,
            FontSize = 13,
            Foreground = HybridKit.MuteBrush,
            Margin = new Thickness(12, -2, 0, 0)
        });
        Canvas.SetLeft(statusLine, 216);
        Canvas.SetTop(statusLine, 226);
        root.Children.Add(statusLine);

        // Protocol options rows at (216, 278, w: 588)
        var rowsPanel = new StackPanel { Width = 588, Orientation = Orientation.Vertical };
        var options = new (string Label, string Value)[]
        {
            ("USB Polling Rate", "1000 Hz (1.0 ms)"),
            ("Gyroscope Emulation", "Disabled (Pass-through)"),
            ("Deadzone Pass-through", "Raw Hall-Effect"),
            ("Trigger Response Curve", "Linear Standard"),
            ("Vibration Haptic Mode", "Native Dual-Rumble"),
            ("Analog Stick Resolution", "12-bit (4096 levels)")
        };

        foreach (var (lbl, val) in options)
        {
            var rBorder = new Border
            {
                Height = 48,
                CornerRadius = new CornerRadius(12),
                Background = HybridKit.B("#1f1c25"),
                Margin = new Thickness(0, 0, 0, 8),
                Padding = new Thickness(18, 0, 18, 0)
            };
            var rGrid = new Grid();
            rGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var t1 = new TextBlock
            {
                Text = lbl,
                FontFamily = HybridKit.Body,
                FontSize = 14,
                Foreground = HybridKit.WhiteBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(t1, 0);
            rGrid.Children.Add(t1);

            var t2 = new TextBlock
            {
                Text = val,
                FontFamily = HybridKit.Body,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = HybridKit.CyanBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(t2, 1);
            rGrid.Children.Add(t2);

            rBorder.Child = rGrid;
            rowsPanel.Children.Add(rBorder);
        }
        Canvas.SetLeft(rowsPanel, 216);
        Canvas.SetTop(rowsPanel, 278);
        root.Children.Add(rowsPanel);

        // Seam between left and right cards
        root.Children.Add(HybridKit.Seam(846, 278, 830, 560, true, 0.8));

        // Right side Detail card at (892, 278, w: 492, h: 548)
        var detailCard = new Border
        {
            Width = 492,
            Height = 548,
            CornerRadius = new CornerRadius(22),
            Background = HybridKit.B("#1f1c25"),
            Padding = new Thickness(28),
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 16, ShadowDepth = 6, Opacity = 0.3 }
        };

        var detailStack = new StackPanel { Orientation = Orientation.Vertical };
        detailStack.Children.Add(new TextBlock
        {
            Text = "Switching to Nintendo Switch Mode",
            FontFamily = HybridKit.Display,
            FontSize = 22,
            FontWeight = FontWeights.ExtraBold,
            Foreground = HybridKit.WhiteBrush,
            Margin = new Thickness(0, 0, 0, 18)
        });

        var steps = new string[]
        {
            "Hold HOME + B for 3 seconds until the central ring flashes.",
            "Wait for the player status LEDs to sweep purple across the bar.",
            "Pair via Bluetooth 5.0 or place into the USB-C dock station.",
            "Confirm button layout remapping (A/B and X/Y position swap)."
        };

        for (int i = 0; i < steps.Length; i++)
        {
            var stepRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 16) };
            var numPill = new Border
            {
                Width = 30,
                Height = 30,
                CornerRadius = new CornerRadius(15),
                Background = HybridKit.LavenderBrush,
                Margin = new Thickness(0, 0, 14, 0),
                Child = new TextBlock
                {
                    Text = (i + 1).ToString(),
                    FontFamily = HybridKit.Display,
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    Foreground = HybridKit.InkBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            stepRow.Children.Add(numPill);

            stepRow.Children.Add(new TextBlock
            {
                Text = steps[i],
                FontFamily = HybridKit.Body,
                FontSize = 15,
                Foreground = HybridKit.B("#d9d3e3"),
                Width = 380,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            });

            detailStack.Children.Add(stepRow);
        }

        // Action button at bottom
        var btnApply = new Border
        {
            Height = 48,
            CornerRadius = new CornerRadius(24),
            Background = HybridKit.LavenderBrush,
            Cursor = System.Windows.Input.Cursors.Hand,
            Margin = new Thickness(0, 24, 0, 0),
            Padding = new Thickness(24, 0, 24, 0),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    new Border
                    {
                        Width = 22,
                        Height = 22,
                        CornerRadius = new CornerRadius(11),
                        Background = HybridKit.InkBrush,
                        Margin = new Thickness(0, 0, 10, 0),
                        Child = new TextBlock
                        {
                            Text = "A",
                            FontFamily = new FontFamily("Segoe UI"),
                            FontSize = 12,
                            FontWeight = FontWeights.Bold,
                            Foreground = Brushes.White,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    },
                    new TextBlock
                    {
                        Text = "Switch to XInput (Default)",
                        FontFamily = HybridKit.Body,
                        FontSize = 15,
                        FontWeight = FontWeights.Bold,
                        Foreground = HybridKit.InkBrush,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                }
            }
        };
        detailStack.Children.Add(btnApply);

        detailCard.Child = detailStack;
        Canvas.SetLeft(detailCard, 892);
        Canvas.SetTop(detailCard, 278);
        root.Children.Add(detailCard);

        return root;
    }
}
