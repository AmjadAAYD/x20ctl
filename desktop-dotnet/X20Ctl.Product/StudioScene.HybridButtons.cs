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
    private void HybridShowButtons()
    {
        hybridPageHost!.Children.Clear();

        var hints = new[]
        {
            ("A", "Assign"),
            ("Y", "All assignments"),
            ("B", "Back")
        };

        // Handheld rails + central screen
        var rails = HybridKit.HandheldRails(
            curPage: "buttons",
            hints: hints,
            title: "Buttons",
            lead: "LOCAL DRAFT · Not written to controller memory.",
            onNav: key => HybridSelectPage(key)
        );
        hybridPageHost.Children.Add(rails);

        // Group selector pill at (950, 44)
        var capPanel = new Border
        {
            Height = 42,
            CornerRadius = new CornerRadius(21),
            Background = HybridKit.B("#26232c"),
            Padding = new Thickness(4)
        };
        var capStack = new StackPanel { Orientation = Orientation.Horizontal };
        var groups = new[] { "Face & Bumpers", "D-pad & Sticks", "Paddles" };
        for (int i = 0; i < groups.Length; i++)
        {
            bool on = i == 0;
            var tab = new Border
            {
                CornerRadius = new CornerRadius(17),
                Padding = new Thickness(18, 0, 18, 0),
                Background = on ? HybridKit.LavenderBrush : Brushes.Transparent,
                Child = new TextBlock
                {
                    Text = groups[i],
                    FontFamily = HybridKit.Display,
                    FontSize = 15,
                    FontWeight = FontWeights.Bold,
                    Foreground = on ? HybridKit.InkBrush : HybridKit.MuteBrush,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            capStack.Children.Add(tab);
        }
        capPanel.Child = capStack;
        Canvas.SetLeft(capPanel, 920);
        Canvas.SetTop(capPanel, 44);
        hybridPageHost.Children.Add(capPanel);

        // Left assignments list at (216, 168, w: 588)
        var listPanel = new StackPanel { Width = 588 };
        var pairs = new[]
        {
            ("A", "B", true),
            ("B", "B", false),
            ("X", "X", false),
            ("Y", "Y", false)
        };

        foreach (var (fromKey, toKey, mod) in pairs)
        {
            var row = new Border
            {
                Height = 66,
                CornerRadius = new CornerRadius(16),
                Background = mod ? HybridKit.LavenderBrush : HybridKit.B("#1f1c25"),
                Margin = new Thickness(0, 0, 0, 4),
                Padding = new Thickness(22, 0, 20, 0)
            };

            var rowGrid = new Grid();
            var leftStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            // From Keycap
            var fromCap = new Border
            {
                Width = 40,
                Height = 40,
                CornerRadius = new CornerRadius(20),
                Background = mod ? HybridKit.InkBrush : HybridKit.B("#3a3542"),
                Child = new TextBlock
                {
                    Text = fromKey,
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 17,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            leftStack.Children.Add(fromCap);

            leftStack.Children.Add(new TextBlock
            {
                Text = "sends",
                FontFamily = HybridKit.Body,
                FontSize = 18,
                FontWeight = FontWeights.Normal,
                Foreground = mod ? HybridKit.InkBrush : HybridKit.WhiteBrush,
                Margin = new Thickness(14, 0, 14, 0),
                VerticalAlignment = VerticalAlignment.Center
            });

            // To Keycap
            var toCap = new Border
            {
                Width = 40,
                Height = 40,
                CornerRadius = new CornerRadius(20),
                Background = mod ? HybridKit.InkBrush : HybridKit.B("#3a3542"),
                Child = new TextBlock
                {
                    Text = toKey,
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 17,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            leftStack.Children.Add(toCap);
            rowGrid.Children.Add(leftStack);

            if (mod)
            {
                var modTag = new TextBlock
                {
                    Text = "Modified",
                    FontFamily = HybridKit.Body,
                    FontSize = 15,
                    Foreground = HybridKit.B("#5a4b6e"),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center
                };
                rowGrid.Children.Add(modTag);
            }

            row.Child = rowGrid;
            listPanel.Children.Add(row);
        }

        // Action rows: Reset this control, Test mapping
        var actions = new[] { ("\uE777", "Reset this control"), ("\uE7FC", "Test mapping") };
        foreach (var (icon, label) in actions)
        {
            var aRow = new Border
            {
                Height = 58,
                CornerRadius = new CornerRadius(16),
                Background = HybridKit.B("#1f1c25"),
                Margin = new Thickness(0, 0, 0, 4),
                Padding = new Thickness(22, 0, 20, 0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            var aGrid = new Grid();
            var aLeft = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            aLeft.Children.Add(new TextBlock { Text = icon, FontFamily = HybridKit.Icons, FontSize = 17, Foreground = HybridKit.WhiteBrush, Margin = new Thickness(0, 0, 14, 0) });
            aLeft.Children.Add(new TextBlock { Text = label, FontFamily = HybridKit.Display, FontSize = 18, FontWeight = FontWeights.SemiBold, Foreground = HybridKit.WhiteBrush });
            aGrid.Children.Add(aLeft);

            var chev = new TextBlock { Text = "\uE76C", FontFamily = HybridKit.Icons, FontSize = 13, Foreground = HybridKit.WhiteBrush, Opacity = 0.7, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            aGrid.Children.Add(chev);

            aRow.Child = aGrid;
            listPanel.Children.Add(aRow);
        }

        Canvas.SetLeft(listPanel, 216);
        Canvas.SetTop(listPanel, 168);
        hybridPageHost.Children.Add(listPanel);

        // Seam between list and controller at x=846
        var seam = HybridKit.Seam(846, 160, 830, 470, locked: true);
        hybridPageHost.Children.Add(seam);

        // Controller Graphic at (900, 160, w: 470)
        var padArt = HybridKit.ControllerArt("d10-purple", 470);
        if (padArt != null)
        {
            padArt.Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 24, ShadowDepth = 18, Direction = 270, Opacity = 0.5 };
            Canvas.SetLeft(padArt, 900);
            Canvas.SetTop(padArt, 160);
            hybridPageHost.Children.Add(padArt);
        }

        // Glowing callout ring on A button at (1240, 310)
        var aRing = new Ellipse
        {
            Width = 44,
            Height = 44,
            Stroke = HybridKit.CyanBrush,
            StrokeThickness = 4,
            Effect = new DropShadowEffect { Color = HybridKit.Cyan, BlurRadius = 22, ShadowDepth = 0, Opacity = 1.0 }
        };
        Canvas.SetLeft(aRing, 1222);
        Canvas.SetTop(aRing, 290);
        hybridPageHost.Children.Add(aRing);

        var calloutPill = new Border
        {
            Background = Brushes.White,
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(14, 8, 14, 8),
            Child = new TextBlock
            {
                Text = "A sends B",
                FontFamily = HybridKit.Display,
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = HybridKit.InkBrush
            }
        };
        Canvas.SetLeft(calloutPill, 1260);
        Canvas.SetTop(calloutPill, 230);
        hybridPageHost.Children.Add(calloutPill);

        // Output grid at (900, 520, w: 484)
        var outBlock = new StackPanel { Width = 484 };
        outBlock.Children.Add(new TextBlock
        {
            Text = "OUTPUT",
            FontFamily = HybridKit.Body,
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Foreground = HybridKit.MuteBrush,
            Margin = new Thickness(0, 0, 0, 14)
        });

        var outGrid = new Grid();
        for (int c = 0; c < 5; c++) outGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        outGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        outGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var outputs = new[] { "A", "B", "X", "Y", "LB", "RB", "LT", "RT", "LS", "RS" };
        for (int i = 0; i < outputs.Length; i++)
        {
            string outName = outputs[i];
            bool sel = outName == "B";
            var cap = new Border
            {
                Width = 72,
                Height = 72,
                CornerRadius = new CornerRadius(36),
                Background = sel ? HybridKit.LavenderBrush : HybridKit.B("#3a3542"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, i < 5 ? 0 : 14, 0, 0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            if (sel)
            {
                cap.BorderBrush = HybridKit.CyanBrush;
                cap.BorderThickness = new Thickness(3);
                cap.Effect = new DropShadowEffect { Color = HybridKit.Cyan, BlurRadius = 12, ShadowDepth = 0 };
            }

            cap.Child = new TextBlock
            {
                Text = outName,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                Foreground = sel ? HybridKit.InkBrush : Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(cap, i % 5);
            Grid.SetRow(cap, i / 5);
            outGrid.Children.Add(cap);
        }
        outBlock.Children.Add(outGrid);

        Canvas.SetLeft(outBlock, 900);
        Canvas.SetTop(outBlock, 520);
        hybridPageHost.Children.Add(outBlock);
    }
}
