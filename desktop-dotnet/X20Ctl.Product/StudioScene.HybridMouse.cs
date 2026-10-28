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
    private void HybridShowMouse()
    {
        hybridPageHost!.Children.Clear();
        hybridPageHost.Children.Add(BuildHybridMouse());
    }

    private FrameworkElement BuildHybridMouse()
    {
        var root = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };

        var hints = new (string Key, string Label)[]
        {
            ("A", "Remap"),
            ("X", "Toggle mode"),
            ("B", "Back")
        };

        // Handheld rails background
        root.Children.Add(HybridKit.HandheldRails("mouse", hints, "Mouse Emulation", "Control the Windows desktop cursor and mouse clicks using your controller sticks and triggers.", key =>
        {
            HybridSelectPage(key);
        }));

        // Left top settings rows at (216, 146, w: 588)
        var topRows = new StackPanel { Width = 588, Orientation = Orientation.Vertical };

        // Toggle row
        var toggleBorder = new Border
        {
            Height = 52,
            CornerRadius = new CornerRadius(14),
            Background = HybridKit.B("#1f1c25"),
            Margin = new Thickness(0, 0, 0, 8),
            Padding = new Thickness(18, 0, 18, 0)
        };
        var tGrid = new Grid();
        tGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        tGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        tGrid.Children.Add(new TextBlock
        {
            Text = "Mouse Emulation Mode",
            FontFamily = HybridKit.Body,
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Foreground = HybridKit.WhiteBrush,
            VerticalAlignment = VerticalAlignment.Center
        });

        // Toggle pill
        var toggleSwitch = new Border
        {
            Width = 56,
            Height = 28,
            CornerRadius = new CornerRadius(14),
            Background = HybridKit.B("#34303c"),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new Border
            {
                Width = 22,
                Height = 22,
                CornerRadius = new CornerRadius(11),
                Background = HybridKit.MuteBrush,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(3, 0, 0, 0)
            }
        };
        Grid.SetColumn(toggleSwitch, 1);
        tGrid.Children.Add(toggleSwitch);
        toggleBorder.Child = tGrid;
        topRows.Children.Add(toggleBorder);

        // Control Source row
        var srcBorder = new Border
        {
            Height = 52,
            CornerRadius = new CornerRadius(14),
            Background = HybridKit.B("#1f1c25"),
            Padding = new Thickness(18, 0, 18, 0)
        };
        var sGrid = new Grid();
        sGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        sGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        sGrid.Children.Add(new TextBlock
        {
            Text = "Primary Control Stick",
            FontFamily = HybridKit.Body,
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Foreground = HybridKit.WhiteBrush,
            VerticalAlignment = VerticalAlignment.Center
        });

        var srcVal = new TextBlock
        {
            Text = "Right Analog Stick \uE70D",
            FontFamily = HybridKit.Body,
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = HybridKit.CyanBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(srcVal, 1);
        sGrid.Children.Add(srcVal);
        srcBorder.Child = sGrid;
        topRows.Children.Add(srcBorder);

        Canvas.SetLeft(topRows, 216);
        Canvas.SetTop(topRows, 146);
        root.Children.Add(topRows);

        // Label for mapping grid at (216, 280)
        var mapTitle = new TextBlock
        {
            Text = "ASSIGNED ACTION MAPPINGS",
            FontFamily = HybridKit.Display,
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Foreground = HybridKit.B("#8e809f")
        };
        Canvas.SetLeft(mapTitle, 216);
        Canvas.SetTop(mapTitle, 280);
        root.Children.Add(mapTitle);

        // 2-column grid of mapping pills at (216, 310, w: 588)
        var mapGrid = new Grid { Width = 588 };
        mapGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        mapGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var mappings = new (string Key, string Action, int Col, int Row)[]
        {
            ("RS \u2191", "Cursor Up", 0, 0),
            ("RT", "Left Mouse Click", 1, 0),
            ("RS \u2193", "Cursor Down", 0, 1),
            ("LT", "Right Mouse Click", 1, 1),
            ("RS \u2190", "Cursor Left", 0, 2),
            ("RB", "Middle Click (Scroll)", 1, 2),
            ("RS \u2192", "Cursor Right", 0, 3),
            ("LB", "Precision Slowdown", 1, 3)
        };

        for (int r = 0; r < 4; r++) mapGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        foreach (var (k, act, col, row) in mappings)
        {
            var cell = new Border
            {
                Height = 46,
                CornerRadius = new CornerRadius(12),
                Background = HybridKit.B("#1f1c25"),
                Margin = new Thickness(col == 0 ? 0 : 6, row == 0 ? 0 : 8, col == 1 ? 0 : 6, 0),
                Padding = new Thickness(12, 0, 12, 0)
            };
            var cStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            var keyCap = new Border
            {
                Width = 44,
                Height = 28,
                CornerRadius = new CornerRadius(14),
                Background = HybridKit.LavenderBrush,
                Margin = new Thickness(0, 0, 10, 0),
                Child = new TextBlock
                {
                    Text = k,
                    FontFamily = HybridKit.Display,
                    FontSize = 12,
                    FontWeight = FontWeights.Bold,
                    Foreground = HybridKit.InkBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            cStack.Children.Add(keyCap);

            cStack.Children.Add(new TextBlock
            {
                Text = act,
                FontFamily = HybridKit.Body,
                FontSize = 13,
                Foreground = HybridKit.WhiteBrush,
                VerticalAlignment = VerticalAlignment.Center
            });

            cell.Child = cStack;
            Grid.SetColumn(cell, col);
            Grid.SetRow(cell, row);
            mapGrid.Children.Add(cell);
        }

        Canvas.SetLeft(mapGrid, 216);
        Canvas.SetTop(mapGrid, 310);
        root.Children.Add(mapGrid);

        // Seam between mapping and options
        root.Children.Add(HybridKit.Seam(846, 146, 830, 470, true, 0.8));

        // Right side Detail card at (892, 146, w: 492, h: 540)
        var optionsCard = new Border
        {
            Width = 492,
            Height = 540,
            CornerRadius = new CornerRadius(22),
            Background = HybridKit.B("#1f1c25"),
            Padding = new Thickness(26)
        };
        var oStack = new StackPanel { Orientation = Orientation.Vertical };
        oStack.Children.Add(new TextBlock
        {
            Text = "Cursor Dynamics & Sensitivity",
            FontFamily = HybridKit.Display,
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            Foreground = HybridKit.WhiteBrush,
            Margin = new Thickness(0, 0, 0, 18)
        });

        var optRows = new (string Label, string Val)[]
        {
            ("Base Pointer Speed", "1400 DPI (Balanced)"),
            ("Stick Acceleration Curve", "Exponential Dynamic"),
            ("Analog Stick Deadzone", "5% Inner Radius"),
            ("Vertical Scroll Speed", "Normal (3 lines/notch)"),
            ("Precision Snapping", "Snap to UI Elements")
        };

        foreach (var (lbl, val) in optRows)
        {
            var opBorder = new Border
            {
                Height = 48,
                CornerRadius = new CornerRadius(12),
                Background = HybridKit.B("#25212c"),
                Margin = new Thickness(0, 0, 0, 10),
                Padding = new Thickness(16, 0, 16, 0)
            };
            var oGrid = new Grid();
            oGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            oGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            oGrid.Children.Add(new TextBlock
            {
                Text = lbl,
                FontFamily = HybridKit.Body,
                FontSize = 14,
                Foreground = HybridKit.WhiteBrush,
                VerticalAlignment = VerticalAlignment.Center
            });

            var valBlock = new TextBlock
            {
                Text = val,
                FontFamily = HybridKit.Body,
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = HybridKit.CyanBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(valBlock, 1);
            oGrid.Children.Add(valBlock);

            opBorder.Child = oGrid;
            oStack.Children.Add(opBorder);
        }

        optionsCard.Child = oStack;
        Canvas.SetLeft(optionsCard, 892);
        Canvas.SetTop(optionsCard, 146);
        root.Children.Add(optionsCard);

        return root;
    }
}
