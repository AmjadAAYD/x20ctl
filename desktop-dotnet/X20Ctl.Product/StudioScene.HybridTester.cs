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
    private void HybridShowTester()
    {
        hybridPageHost!.Children.Clear();
        hybridPageHost.Children.Add(BuildHybridTester());
    }

    private FrameworkElement BuildHybridTester()
    {
        var root = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };

        var hints = new (string Key, string Label)[]
        {
            ("A", "Test rumble"),
            ("B", "Calibrate"),
            ("X", "Reset"),
            ("Y", "Swap view")
        };

        // Handheld rails background
        root.Children.Add(HybridKit.HandheldRails("device", hints, "Input Tester & Latency", "Interactive hardware button verification and real-time report latency monitor.", key =>
        {
            HybridSelectPage(key);
        }));

        // Controller artwork on left at (236, 168, w: 560)
        var padArt = HybridKit.ControllerArt("d10-purple", 560);
        if (padArt != null)
        {
            Canvas.SetLeft(padArt, 236);
            Canvas.SetTop(padArt, 168);
            root.Children.Add(padArt);
        }

        // View selector caps at (236, 560, w: 560)
        var viewStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        var views = new (string Name, bool Active)[]
        {
            ("Front View", true),
            ("Back Paddles", false),
            ("Analog Sensors", false)
        };
        foreach (var (vName, act) in views)
        {
            var capBorder = new Border
            {
                Height = 34,
                Padding = new Thickness(16, 0, 16, 0),
                CornerRadius = new CornerRadius(17),
                Background = act ? HybridKit.LavenderBrush : HybridKit.B("#1f1c25"),
                Margin = new Thickness(0, 0, 8, 0),
                Child = new TextBlock
                {
                    Text = vName,
                    FontFamily = HybridKit.Body,
                    FontSize = 13,
                    FontWeight = FontWeights.Bold,
                    Foreground = act ? HybridKit.InkBrush : HybridKit.WhiteBrush,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            viewStack.Children.Add(capBorder);
        }

        // LED dashes next to caps
        for (int i = 0; i < 4; i++)
        {
            var dash = new Border
            {
                Width = 14,
                Height = 5,
                CornerRadius = new CornerRadius(2),
                Margin = new Thickness(i == 0 ? 12 : 4, 14, 0, 0),
                Background = i == 0 ? HybridKit.CyanBrush : HybridKit.B("#4d4758")
            };
            viewStack.Children.Add(dash);
        }

        Canvas.SetLeft(viewStack, 310);
        Canvas.SetTop(viewStack, 560);
        root.Children.Add(viewStack);

        // Seam between pad and test card
        root.Children.Add(HybridKit.Seam(846, 160, 640, 400, false, 0.8));

        // Right side Test summary card at (892, 160, w: 492, h: 480)
        var testCard = new Border
        {
            Width = 492,
            Height = 480,
            CornerRadius = new CornerRadius(22),
            Background = HybridKit.B("#1f1c25"),
            Padding = new Thickness(26),
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 16, ShadowDepth = 6, Opacity = 0.3 }
        };
        var tStack = new StackPanel { Orientation = Orientation.Vertical };
        tStack.Children.Add(new TextBlock
        {
            Text = "DEVICE INPUT SOURCE",
            FontFamily = HybridKit.Display,
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Foreground = HybridKit.B("#8e809f")
        });
        tStack.Children.Add(new TextBlock
        {
            Text = "XInput Controller 1",
            FontFamily = HybridKit.Display,
            FontSize = 26,
            FontWeight = FontWeights.ExtraBold,
            Foreground = HybridKit.WhiteBrush,
            Margin = new Thickness(0, 2, 0, 14)
        });

        var metrics = new (string Key, string Val)[]
        {
            ("Input Report Rate", "1000 reports / second"),
            ("Measured Latency", "1.1 ms avg (0.9 ms min)"),
            ("Packet Error Rate", "0.00% (No dropped reports)"),
            ("Hardware Model", "EasySMX D10 Purple Edition")
        };

        foreach (var (k, v) in metrics)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            row.Children.Add(new TextBlock { Text = k + ":", FontFamily = HybridKit.Body, FontSize = 14, Foreground = HybridKit.MuteBrush, Width = 180 });
            row.Children.Add(new TextBlock { Text = v, FontFamily = HybridKit.Body, FontSize = 14, FontWeight = FontWeights.Bold, Foreground = HybridKit.CyanBrush });
            tStack.Children.Add(row);
        }

        // Action buttons
        var btnsStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 20, 0, 0) };
        var bCal = new Border
        {
            Height = 44,
            CornerRadius = new CornerRadius(22),
            Background = HybridKit.LavenderBrush,
            Cursor = System.Windows.Input.Cursors.Hand,
            Padding = new Thickness(20, 0, 20, 0),
            Margin = new Thickness(0, 0, 12, 0),
            Child = new TextBlock
            {
                Text = "Calibrate Sticks (B)",
                FontFamily = HybridKit.Body,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = HybridKit.InkBrush,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        btnsStack.Children.Add(bCal);

        var bReset = new Border
        {
            Height = 44,
            CornerRadius = new CornerRadius(22),
            Background = HybridKit.B("#2f2b37"),
            Cursor = System.Windows.Input.Cursors.Hand,
            Padding = new Thickness(20, 0, 20, 0),
            Child = new TextBlock
            {
                Text = "Reset Stats",
                FontFamily = HybridKit.Body,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = HybridKit.WhiteBrush,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        btnsStack.Children.Add(bReset);
        tStack.Children.Add(btnsStack);

        testCard.Child = tStack;
        Canvas.SetLeft(testCard, 892);
        Canvas.SetTop(testCard, 160);
        root.Children.Add(testCard);

        // Bottom Gauge shelf at (216, 680, w: 1168)
        var shelf = new Border
        {
            Width = 1168,
            Height = 120,
            CornerRadius = new CornerRadius(20),
            Background = HybridKit.B("#18151e"),
            BorderBrush = HybridKit.B("#2c2836"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(24, 14, 24, 14)
        };
        var shelfStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };

        var gauges = new (string Name, string Val)[]
        {
            ("Left Stick", "Center (0.00, 0.00)"),
            ("Right Stick", "Center (0.00, 0.00)"),
            ("Left Trigger", "Rest (0.00)"),
            ("Right Trigger", "Rest (0.00)")
        };

        foreach (var (gName, gVal) in gauges)
        {
            var gBox = new StackPanel { Orientation = Orientation.Vertical, Width = 260 };
            gBox.Children.Add(new TextBlock
            {
                Text = gName,
                FontFamily = HybridKit.Display,
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = HybridKit.WhiteBrush
            });
            gBox.Children.Add(new TextBlock
            {
                Text = gVal,
                FontFamily = HybridKit.Body,
                FontSize = 12,
                Foreground = HybridKit.CyanBrush,
                Margin = new Thickness(0, 4, 0, 8)
            });

            // Mini meter bar
            var bar = new Border
            {
                Height = 8,
                CornerRadius = new CornerRadius(4),
                Background = HybridKit.B("#2d2936"),
                Child = new Border
                {
                    Width = 40,
                    Height = 8,
                    CornerRadius = new CornerRadius(4),
                    Background = HybridKit.LavenderBrush,
                    HorizontalAlignment = HorizontalAlignment.Left
                }
            };
            gBox.Children.Add(bar);

            shelfStack.Children.Add(gBox);
        }

        shelf.Child = shelfStack;
        Canvas.SetLeft(shelf, 216);
        Canvas.SetTop(shelf, 680);
        root.Children.Add(shelf);

        return root;
    }
}
