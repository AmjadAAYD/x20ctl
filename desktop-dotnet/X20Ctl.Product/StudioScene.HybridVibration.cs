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
    private void HybridShowVibration()
    {
        hybridPageHost!.Children.Clear();
        hybridPageHost.Children.Add(BuildHybridVibration());
    }

    private FrameworkElement BuildHybridVibration()
    {
        var root = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };

        var hints = new (string Key, string Label)[]
        {
            ("A", "Toggle link"),
            ("X", "Test pulse"),
            ("B", "Back")
        };

        // Handheld rails background
        root.Children.Add(HybridKit.HandheldRails("vibration", hints, "Vibration", "Tune grip rumble motors and trigger haptic motors for tactile game feedback.", key =>
        {
            HybridSelectPage(key);
        }));

        double w = 560;
        double cx = 240 + w / 2;

        // Seam down the center of the left vibration visual
        root.Children.Add(HybridKit.Seam(cx, 150, 622, 186, true, 0.9));

        // Controller vibration visual on the left
        var padArt = HybridKit.ControllerArt("d10-purple", w);
        if (padArt != null)
        {
            Canvas.SetLeft(padArt, 240);
            Canvas.SetTop(padArt, 210);
            root.Children.Add(padArt);

            // Glowing cyan vibration pulse rings on left & right grips
            var leftRumble = new Ellipse
            {
                Width = 140,
                Height = 140,
                Stroke = HybridKit.CyanBrush,
                StrokeThickness = 3,
                Effect = new DropShadowEffect { Color = HybridKit.Cyan, BlurRadius = 16, ShadowDepth = 0 },
                Opacity = 0.8
            };
            Canvas.SetLeft(leftRumble, 240 + 60);
            Canvas.SetTop(leftRumble, 210 + 130);
            root.Children.Add(leftRumble);

            var rightRumble = new Ellipse
            {
                Width = 140,
                Height = 140,
                Stroke = HybridKit.CyanBrush,
                StrokeThickness = 3,
                Effect = new DropShadowEffect { Color = HybridKit.Cyan, BlurRadius = 16, ShadowDepth = 0 },
                Opacity = 0.8
            };
            Canvas.SetLeft(rightRumble, 240 + w - 200);
            Canvas.SetTop(rightRumble, 210 + 130);
            root.Children.Add(rightRumble);
        }

        // Link pill: [link icon] Left and right motors linked
        var linkPill = new Border
        {
            Height = 34,
            Padding = new Thickness(14, 0, 14, 0),
            CornerRadius = new CornerRadius(17),
            Background = HybridKit.B("#1b1622"),
            BorderBrush = HybridKit.CyanBrush,
            BorderThickness = new Thickness(1),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    new TextBlock
                    {
                        Text = "\uE71B", // link icon
                        FontFamily = HybridKit.Icons,
                        FontSize = 13,
                        Foreground = HybridKit.B("#9feaff"),
                        Margin = new Thickness(0, 0, 6, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    },
                    new TextBlock
                    {
                        Text = "Left and right motors linked",
                        FontFamily = HybridKit.Body,
                        FontSize = 13,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = HybridKit.B("#9feaff"),
                        VerticalAlignment = VerticalAlignment.Center
                    }
                }
            }
        };
        Canvas.SetLeft(linkPill, cx + 30);
        Canvas.SetTop(linkPill, 156);
        root.Children.Add(linkPill);

        // Note below controller visual
        var note = new TextBlock
        {
            Text = "Grip rumble motors deliver independent low-frequency heavy thud and high-frequency buzz.",
            FontFamily = HybridKit.Body,
            FontSize = 14,
            Foreground = HybridKit.MuteBrush,
            Width = w,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        };
        Canvas.SetLeft(note, 240);
        Canvas.SetTop(note, 640);
        root.Children.Add(note);

        // Right side master controls at (900, 170, w: 484)
        var rightPanel = new StackPanel { Width = 484, Orientation = Orientation.Vertical };

        rightPanel.Children.Add(new TextBlock
        {
            Text = "MASTER STRENGTH",
            FontFamily = HybridKit.Body,
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            Foreground = HybridKit.B("#8e809f"),
            Margin = new Thickness(0, 0, 0, 4)
        });

        rightPanel.Children.Add(new TextBlock
        {
            Text = "70%",
            FontFamily = HybridKit.Display,
            FontSize = 100,
            FontWeight = FontWeights.ExtraBold,
            Foreground = HybridKit.WhiteBrush,
            Margin = new Thickness(0, -10, 0, 10)
        });

        // 10-segment LED strength meter
        var ledMeter = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 14) };
        for (int i = 0; i < 10; i++)
        {
            bool filled = i < 7;
            var seg = new Border
            {
                Width = 42,
                Height = 22,
                CornerRadius = new CornerRadius(6),
                Background = filled ? HybridKit.LavenderBrush : HybridKit.B("#2f2b37"),
                Margin = new Thickness(i == 0 ? 0 : 6, 0, 0, 0)
            };
            if (filled)
            {
                seg.Effect = new DropShadowEffect { Color = HybridKit.Lavender, BlurRadius = 6, ShadowDepth = 0, Opacity = 0.5 };
            }
            ledMeter.Children.Add(seg);
        }
        rightPanel.Children.Add(ledMeter);

        // Presets pills: 0% Off, 30% Gentle, 70% Balanced, 100% Intense
        var presets = new (string Label, bool Active)[]
        {
            ("0% Off", false),
            ("30% Gentle", false),
            ("70% Balanced", true),
            ("100% Intense", false)
        };
        var presetsPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 26) };
        foreach (var (lbl, active) in presets)
        {
            var pBorder = new Border
            {
                Height = 34,
                Padding = new Thickness(14, 0, 14, 0),
                CornerRadius = new CornerRadius(17),
                Background = active ? HybridKit.LavenderBrush : HybridKit.B("#2f2b37"),
                Margin = new Thickness(0, 0, 8, 0),
                Child = new TextBlock
                {
                    Text = lbl,
                    FontFamily = HybridKit.Body,
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = active ? HybridKit.InkBrush : HybridKit.WhiteBrush,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            presetsPanel.Children.Add(pBorder);
        }
        rightPanel.Children.Add(presetsPanel);

        // Settings rows
        var rows = new (string Label, string Value, bool Toggle)[]
        {
            ("Left grip motor (heavy rumble)", "70%", true),
            ("Right grip motor (light rumble)", "70%", true),
            ("Trigger force feedback haptics", "Enabled", true),
            ("Vibration sleep timeout", "5 min", false)
        };

        foreach (var (label, val, tog) in rows)
        {
            var rowBorder = new Border
            {
                Height = 48,
                CornerRadius = new CornerRadius(12),
                Background = HybridKit.B("#1f1c25"),
                Margin = new Thickness(0, 0, 0, 8),
                Padding = new Thickness(16, 0, 16, 0)
            };
            var rowGrid = new Grid();
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var rowText = new TextBlock
            {
                Text = label,
                FontFamily = HybridKit.Body,
                FontSize = 14,
                Foreground = HybridKit.WhiteBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(rowText, 0);
            rowGrid.Children.Add(rowText);

            var rowVal = new TextBlock
            {
                Text = val,
                FontFamily = HybridKit.Body,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = HybridKit.CyanBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(rowVal, 1);
            rowGrid.Children.Add(rowVal);

            rowBorder.Child = rowGrid;
            rightPanel.Children.Add(rowBorder);
        }

        rightPanel.Children.Add(new TextBlock
        {
            Text = "Pulsing subtly on current test pattern · Press X to preview rumble pulse.",
            FontFamily = HybridKit.Body,
            FontSize = 13,
            Foreground = HybridKit.MuteBrush,
            Margin = new Thickness(0, 14, 0, 0)
        });

        Canvas.SetLeft(rightPanel, 900);
        Canvas.SetTop(rightPanel, 170);
        root.Children.Add(rightPanel);

        return root;
    }
}
