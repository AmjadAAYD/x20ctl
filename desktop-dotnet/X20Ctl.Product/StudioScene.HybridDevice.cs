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
    private void HybridShowDevice()
    {
        hybridPageHost!.Children.Clear();
        hybridPageHost.Children.Add(BuildHybridDevice());
    }

    private FrameworkElement BuildHybridDevice()
    {
        var root = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };

        var hints = new (string Key, string Label)[]
        {
            ("A", "Configure"),
            ("X", "Calibrate"),
            ("Y", "Firmware"),
            ("B", "Back")
        };

        // Handheld rails background
        root.Children.Add(HybridKit.HandheldRails("device", hints, "Device Overview", "Hardware specifications, firmware status, and onboard peripheral settings for EasySMX D10.", key =>
        {
            HybridSelectPage(key);
        }));

        // Controller artwork on the left at (230, 160, w: 540)
        var padArt = HybridKit.ControllerArt("d10-purple", 540);
        if (padArt != null)
        {
            Canvas.SetLeft(padArt, 230);
            Canvas.SetTop(padArt, 160);
            root.Children.Add(padArt);
        }

        // LEDs + Name + Assigned pill at (230, 530)
        var padStatus = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
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
            if (i == 0) dash.Effect = new DropShadowEffect { Color = HybridKit.Cyan, BlurRadius = 8, ShadowDepth = 0 };
            padStatus.Children.Add(dash);
        }

        padStatus.Children.Add(new TextBlock
        {
            Text = "PLAYER 1 · EASYSMX D10 PURPLE",
            FontFamily = HybridKit.Display,
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Foreground = HybridKit.WhiteBrush,
            Margin = new Thickness(14, 0, 14, 0),
            VerticalAlignment = VerticalAlignment.Center
        });

        var assignPill = new Border
        {
            Height = 28,
            Padding = new Thickness(12, 0, 12, 0),
            CornerRadius = new CornerRadius(14),
            Background = HybridKit.B("#2f2b37"),
            Child = new TextBlock
            {
                Text = "Connected · 2.4 GHz",
                FontFamily = HybridKit.Body,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = HybridKit.CyanBrush,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        padStatus.Children.Add(assignPill);

        Canvas.SetLeft(padStatus, 230);
        Canvas.SetTop(padStatus, 530);
        root.Children.Add(padStatus);

        // Hardware specs 2x2 grid at (216, 582, w: 588)
        var specsGrid = new Grid { Width = 588 };
        specsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        specsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        specsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        specsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var specs = new (string Label, string Value, int Col, int Row)[]
        {
            ("Firmware Version", "v1.24 (Latest)", 0, 0),
            ("Battery Level", "92% · 18h remaining", 1, 0),
            ("Serial Number", "D10-8842-PUR-REV3", 0, 1),
            ("Connection Interface", "2.4 GHz Wireless USB", 1, 1)
        };

        foreach (var (lbl, val, col, row) in specs)
        {
            var card = new Border
            {
                Height = 60,
                CornerRadius = new CornerRadius(14),
                Background = HybridKit.B("#1f1c25"),
                Margin = new Thickness(col == 0 ? 0 : 5, row == 0 ? 0 : 8, col == 1 ? 0 : 5, 0),
                Padding = new Thickness(16, 8, 16, 8),
                Child = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    VerticalAlignment = VerticalAlignment.Center,
                    Children =
                    {
                        new TextBlock { Text = lbl, FontFamily = HybridKit.Body, FontSize = 12, Foreground = HybridKit.MuteBrush },
                        new TextBlock { Text = val, FontFamily = HybridKit.Display, FontSize = 15, FontWeight = FontWeights.Bold, Foreground = HybridKit.WhiteBrush }
                    }
                }
            };
            Grid.SetColumn(card, col);
            Grid.SetRow(card, row);
            specsGrid.Children.Add(card);
        }

        Canvas.SetLeft(specsGrid, 216);
        Canvas.SetTop(specsGrid, 582);
        root.Children.Add(specsGrid);

        // Seam between pad area and settings
        root.Children.Add(HybridKit.Seam(846, 160, 830, 470, false, 0.8));

        // Right side device settings list at (892, 160, w: 492)
        var settingsPanel = new StackPanel { Width = 492, Orientation = Orientation.Vertical };
        var devSettings = new (string Label, string Value, bool Active)[]
        {
            ("RGB Halo Light Effect", "Breathing (Purple / Cyan)", true),
            ("RGB Brightness", "75% Moderate", true),
            ("Auto-Sleep Timeout", "10 minutes of inactivity", true),
            ("Zero-Latency Hall Sensors", "Active (Calibrated)", true),
            ("Back Paddle Remapping", "Active profile: Default", false)
        };

        foreach (var (lbl, val, act) in devSettings)
        {
            var sBorder = new Border
            {
                Height = 48,
                CornerRadius = new CornerRadius(12),
                Background = HybridKit.B("#1f1c25"),
                Margin = new Thickness(0, 0, 0, 8),
                Padding = new Thickness(18, 0, 18, 0)
            };
            var sGrid = new Grid();
            sGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            sGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var stText = new TextBlock
            {
                Text = lbl,
                FontFamily = HybridKit.Body,
                FontSize = 14,
                Foreground = HybridKit.WhiteBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(stText, 0);
            sGrid.Children.Add(stText);

            var stVal = new TextBlock
            {
                Text = val,
                FontFamily = HybridKit.Body,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = act ? HybridKit.CyanBrush : HybridKit.MuteBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(stVal, 1);
            sGrid.Children.Add(stVal);

            sBorder.Child = sGrid;
            settingsPanel.Children.Add(sBorder);
        }

        Canvas.SetLeft(settingsPanel, 892);
        Canvas.SetTop(settingsPanel, 160);
        root.Children.Add(settingsPanel);

        // Right side detail card at (892, 450, w: 492, h: 370)
        var calCard = new Border
        {
            Width = 492,
            Height = 370,
            CornerRadius = new CornerRadius(22),
            Background = HybridKit.B("#1f1c25"),
            Padding = new Thickness(24),
            Child = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Children =
                {
                    new TextBlock
                    {
                        Text = "Calibration & Diagnostics",
                        FontFamily = HybridKit.Display,
                        FontSize = 20,
                        FontWeight = FontWeights.Bold,
                        Foreground = HybridKit.WhiteBrush,
                        Margin = new Thickness(0, 0, 0, 10)
                    },
                    new TextBlock
                    {
                        Text = "Recalibrate the Hall-effect analog sticks and trigger sensors if you observe any centering drift or unexpected stick input variance.",
                        FontFamily = HybridKit.Body,
                        FontSize = 14,
                        Foreground = HybridKit.MuteBrush,
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 0, 0, 20)
                    },
                    new Border
                    {
                        Height = 44,
                        CornerRadius = new CornerRadius(22),
                        Background = HybridKit.LavenderBrush,
                        Cursor = System.Windows.Input.Cursors.Hand,
                        Padding = new Thickness(20, 0, 20, 0),
                        Child = new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center,
                            Children =
                            {
                                new TextBlock
                                {
                                    Text = "\uE90F", // wrench
                                    FontFamily = HybridKit.Icons,
                                    FontSize = 14,
                                    Foreground = HybridKit.InkBrush,
                                    Margin = new Thickness(0, 0, 8, 0),
                                    VerticalAlignment = VerticalAlignment.Center
                                },
                                new TextBlock
                                {
                                    Text = "Run Full Calibration Wizard",
                                    FontFamily = HybridKit.Body,
                                    FontSize = 14,
                                    FontWeight = FontWeights.Bold,
                                    Foreground = HybridKit.InkBrush,
                                    VerticalAlignment = VerticalAlignment.Center
                                }
                            }
                        }
                    }
                }
            }
        };
        Canvas.SetLeft(calCard, 892);
        Canvas.SetTop(calCard, 450);
        root.Children.Add(calCard);

        return root;
    }
}
