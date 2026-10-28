using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Hybrid;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private void HybridShowDiagnostics()
    {
        hybridPageHost!.Children.Clear();
        hybridPageHost.Children.Add(BuildHybridDiagnostics());
    }

    private FrameworkElement BuildHybridDiagnostics()
    {
        var root = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };

        var hints = new (string Key, string Label)[]
        {
            ("A", "Toggle view"),
            ("X", "Reset stats"),
            ("B", "Back")
        };

        // Handheld rails background
        root.Children.Add(HybridKit.HandheldRails("device", hints, "Live Input Diagnostics", "Direct sensor telemetry, raw analog stick reticles, and trigger precision meters.", key =>
        {
            HybridSelectPage(key);
        }));

        // Filter chips row at (216, 150)
        var chipsPanel = new StackPanel { Orientation = Orientation.Horizontal };
        var chips = new (string Label, bool Active)[]
        {
            ("Player 1 · XInput 1", true),
            ("Raw Telemetry", false),
            ("Intervals: 1ms", true),
            ("Auto-Center: ON", true)
        };
        foreach (var (lbl, act) in chips)
        {
            var cBorder = new Border
            {
                Height = 32,
                Padding = new Thickness(14, 0, 14, 0),
                CornerRadius = new CornerRadius(16),
                Background = act ? HybridKit.LavenderBrush : HybridKit.B("#1f1c25"),
                Margin = new Thickness(0, 0, 8, 0),
                Child = new TextBlock
                {
                    Text = lbl,
                    FontFamily = HybridKit.Body,
                    FontSize = 12,
                    FontWeight = FontWeights.Bold,
                    Foreground = act ? HybridKit.InkBrush : HybridKit.WhiteBrush,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            chipsPanel.Children.Add(cBorder);
        }
        Canvas.SetLeft(chipsPanel, 216);
        Canvas.SetTop(chipsPanel, 150);
        root.Children.Add(chipsPanel);

        // Helper to build circular stick reticle
        FrameworkElement BuildStickReticle(string label, double xVal, double yVal, bool lavenderTheme)
        {
            var box = new Border
            {
                Width = 280,
                Height = 310,
                CornerRadius = new CornerRadius(18),
                Background = HybridKit.B(lavenderTheme ? "#241f2d" : "#1b1922"),
                BorderBrush = lavenderTheme ? HybridKit.LavenderBrush : HybridKit.B("#383244"),
                BorderThickness = new Thickness(1.5),
                Padding = new Thickness(16)
            };

            var stack = new StackPanel { Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Center };
            stack.Children.Add(new TextBlock
            {
                Text = label,
                FontFamily = HybridKit.Display,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = HybridKit.WhiteBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 12)
            });

            var reticleCanvas = new Canvas { Width = 200, Height = 200 };

            // Outer ring
            var outer = new Ellipse
            {
                Width = 200,
                Height = 200,
                Stroke = HybridKit.B("#3a3544"),
                StrokeThickness = 2
            };
            reticleCanvas.Children.Add(outer);

            // Mid ring (50% deadzone)
            var mid = new Ellipse
            {
                Width = 100,
                Height = 100,
                Stroke = HybridKit.B("#2d2936"),
                StrokeThickness = 1
            };
            Canvas.SetLeft(mid, 50);
            Canvas.SetTop(mid, 50);
            reticleCanvas.Children.Add(mid);

            // Crosshairs
            var hLine = new Line { X1 = 10, Y1 = 100, X2 = 190, Y2 = 100, Stroke = HybridKit.B("#3a3544"), StrokeThickness = 1 };
            var vLine = new Line { X1 = 100, Y1 = 10, X2 = 100, Y2 = 190, Stroke = HybridKit.B("#3a3544"), StrokeThickness = 1 };
            reticleCanvas.Children.Add(hLine);
            reticleCanvas.Children.Add(vLine);

            // Live dot
            double dotX = 100 + xVal * 90 - 7;
            double dotY = 100 - yVal * 90 - 7;
            var dot = new Ellipse
            {
                Width = 14,
                Height = 14,
                Fill = HybridKit.CyanBrush,
                Effect = new DropShadowEffect { Color = HybridKit.Cyan, BlurRadius = 8, ShadowDepth = 0 }
            };
            Canvas.SetLeft(dot, dotX);
            Canvas.SetTop(dot, dotY);
            reticleCanvas.Children.Add(dot);

            stack.Children.Add(reticleCanvas);

            // Coords label
            stack.Children.Add(new TextBlock
            {
                Text = $"X: {xVal:+0.00;-0.00;0.00}   Y: {yVal:+0.00;-0.00;0.00}",
                FontFamily = HybridKit.Body,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = HybridKit.CyanBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 0)
            });

            box.Child = stack;
            return box;
        }

        // Left Stick Reticle at (216, 204)
        var leftStick = BuildStickReticle("LEFT STICK (LS)", 0.02, -0.01, true);
        Canvas.SetLeft(leftStick, 216);
        Canvas.SetTop(leftStick, 204);
        root.Children.Add(leftStick);

        // Right Stick Reticle at (524, 204)
        var rightStick = BuildStickReticle("RIGHT STICK (RS)", 0.00, 0.00, false);
        Canvas.SetLeft(rightStick, 524);
        Canvas.SetTop(rightStick, 204);
        root.Children.Add(rightStick);

        // Trigger meters at (216, 536, w: 588)
        var triggerBox = new Border
        {
            Width = 588,
            Height = 150,
            CornerRadius = new CornerRadius(18),
            Background = HybridKit.B("#1f1c25"),
            Padding = new Thickness(20)
        };
        var triggerStack = new StackPanel { Orientation = Orientation.Vertical };
        triggerStack.Children.Add(new TextBlock
        {
            Text = "HALL-EFFECT TRIGGER TRAVEL",
            FontFamily = HybridKit.Display,
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            Foreground = HybridKit.B("#8e809f"),
            Margin = new Thickness(0, 0, 0, 14)
        });

        // LT Bar
        var ltRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
        ltRow.Children.Add(new TextBlock { Text = "LT", FontFamily = HybridKit.Display, FontSize = 13, FontWeight = FontWeights.Bold, Foreground = HybridKit.WhiteBrush, Width = 30, VerticalAlignment = VerticalAlignment.Center });
        var ltTrack = new Border { Width = 450, Height = 14, CornerRadius = new CornerRadius(7), Background = HybridKit.B("#2f2b37") };
        var ltFill = new Border { Width = 30, Height = 14, CornerRadius = new CornerRadius(7), Background = HybridKit.CyanBrush, HorizontalAlignment = HorizontalAlignment.Left };
        ltTrack.Child = ltFill;
        ltRow.Children.Add(ltTrack);
        ltRow.Children.Add(new TextBlock { Text = "0.00", FontFamily = HybridKit.Body, FontSize = 12, Foreground = HybridKit.CyanBrush, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
        triggerStack.Children.Add(ltRow);

        // RT Bar
        var rtRow = new StackPanel { Orientation = Orientation.Horizontal };
        rtRow.Children.Add(new TextBlock { Text = "RT", FontFamily = HybridKit.Display, FontSize = 13, FontWeight = FontWeights.Bold, Foreground = HybridKit.WhiteBrush, Width = 30, VerticalAlignment = VerticalAlignment.Center });
        var rtTrack = new Border { Width = 450, Height = 14, CornerRadius = new CornerRadius(7), Background = HybridKit.B("#2f2b37") };
        var rtFill = new Border { Width = 30, Height = 14, CornerRadius = new CornerRadius(7), Background = HybridKit.CyanBrush, HorizontalAlignment = HorizontalAlignment.Left };
        rtTrack.Child = rtFill;
        rtRow.Children.Add(rtTrack);
        rtRow.Children.Add(new TextBlock { Text = "0.00", FontFamily = HybridKit.Body, FontSize = 12, Foreground = HybridKit.CyanBrush, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
        triggerStack.Children.Add(rtRow);

        triggerBox.Child = triggerStack;
        Canvas.SetLeft(triggerBox, 216);
        Canvas.SetTop(triggerBox, 536);
        root.Children.Add(triggerBox);

        // Seam between reticles and right panels
        root.Children.Add(HybridKit.Seam(846, 160, 830, 470, false, 0.8));

        // Right side: Button bitmask grid at (892, 160, w: 492)
        var bitmaskCard = new Border
        {
            Width = 492,
            Height = 360,
            CornerRadius = new CornerRadius(22),
            Background = HybridKit.B("#1f1c25"),
            Padding = new Thickness(22)
        };
        var bitmaskStack = new StackPanel { Orientation = Orientation.Vertical };
        bitmaskStack.Children.Add(new TextBlock
        {
            Text = "DIGITAL BUTTON MATRIX",
            FontFamily = HybridKit.Display,
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            Foreground = HybridKit.B("#8e809f"),
            Margin = new Thickness(0, 0, 0, 14)
        });

        var btnNames = new string[] { "A", "B", "X", "Y", "LB", "RB", "LT", "RT", "VIEW", "MENU", "LSB", "RSB", "UP", "DOWN", "LEFT", "RIGHT" };
        var btnGrid = new UniformGrid { Columns = 4, Rows = 4 };
        foreach (var name in btnNames)
        {
            var btnTile = new Border
            {
                Height = 44,
                CornerRadius = new CornerRadius(10),
                Background = HybridKit.B("#2a2632"),
                Margin = new Thickness(4),
                Child = new TextBlock
                {
                    Text = name,
                    FontFamily = HybridKit.Display,
                    FontSize = 13,
                    FontWeight = FontWeights.Bold,
                    Foreground = HybridKit.B("#9e97a9"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            btnGrid.Children.Add(btnTile);
        }
        bitmaskStack.Children.Add(btnGrid);
        bitmaskCard.Child = bitmaskStack;
        Canvas.SetLeft(bitmaskCard, 892);
        Canvas.SetTop(bitmaskCard, 160);
        root.Children.Add(bitmaskCard);

        // Polling rate summary card at (892, 540, w: 492, h: 146)
        var pollCard = new Border
        {
            Width = 492,
            Height = 146,
            CornerRadius = new CornerRadius(20),
            Background = HybridKit.B("#1f1c25"),
            Padding = new Thickness(22),
            Child = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Children =
                {
                    new TextBlock
                    {
                        Text = "TELEMETRY PERFORMANCE",
                        FontFamily = HybridKit.Display,
                        FontSize = 12,
                        FontWeight = FontWeights.Bold,
                        Foreground = HybridKit.B("#8e809f")
                    },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Margin = new Thickness(0, 8, 0, 0),
                        Children =
                        {
                            new TextBlock { Text = "998 Hz", FontFamily = HybridKit.Display, FontSize = 36, FontWeight = FontWeights.ExtraBold, Foreground = HybridKit.CyanBrush },
                            new TextBlock { Text = " · 1.002 ms interval · 0.08 ms jitter", FontFamily = HybridKit.Body, FontSize = 14, Foreground = HybridKit.WhiteBrush, Margin = new Thickness(10, 14, 0, 0) }
                        }
                    },
                    new TextBlock
                    {
                        Text = "Hardware report stream stable · 0 dropped packets in current session",
                        FontFamily = HybridKit.Body,
                        FontSize = 12,
                        Foreground = HybridKit.MuteBrush,
                        Margin = new Thickness(0, 6, 0, 0)
                    }
                }
            }
        };
        Canvas.SetLeft(pollCard, 892);
        Canvas.SetTop(pollCard, 540);
        root.Children.Add(pollCard);

        return root;
    }
}
