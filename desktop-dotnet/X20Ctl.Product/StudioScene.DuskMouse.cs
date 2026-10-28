using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Dusk;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private void DuskShowMouse()
    {
        duskPageHost!.Children.Clear();

        var hints = new[]
        {
            ("X", "DEFAULTS"),
            ("A", "SELECT"),
            ("B", "BACK")
        };

        var contentCanvas = new Canvas { Width = 1040, Height = 580 };

        // Left list (w: 580)
        var leftStack = new StackPanel { Width = 580, Orientation = Orientation.Vertical };

        leftStack.Children.Add(new TextBlock
        {
            Text = "MOUSE EMULATION",
            FontFamily = DuskKit.Display,
            FontSize = 18,
            FontWeight = FontWeights.ExtraBold,
            Foreground = DuskKit.MuteBrush,
            Margin = new Thickness(0, 0, 0, 4)
        });

        leftStack.Children.Add(new TextBlock
        {
            Text = "Map joystick deflection to high-precision desktop cursor movement.",
            FontFamily = DuskKit.Body,
            FontSize = 15,
            Foreground = DuskKit.MuteBrush,
            Margin = new Thickness(0, 0, 0, 14)
        });

        var mouseFields = new (string Name, int Val, bool Active)[]
        {
            ("Cursor Sensitivity", 65, true),
            ("Acceleration Curve", 40, false),
            ("Scroll Wheel Speed", 50, false),
            ("Cursor Deadzone", 8, false)
        };

        foreach (var (name, val, active) in mouseFields)
        {
            var row = new Border
            {
                Height = 60,
                BorderThickness = new Thickness(0, 0, 0, 1),
                BorderBrush = DuskKit.B("rgba(255,255,255,.07)"),
                Background = active ? DuskKit.B("rgba(255,255,255,.05)") : Brushes.Transparent,
                Padding = new Thickness(16, 0, 16, 0),
                CornerRadius = new CornerRadius(4)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var label = new TextBlock
            {
                Text = name,
                FontFamily = DuskKit.Display,
                FontSize = 20,
                FontWeight = FontWeights.SemiBold,
                Foreground = active ? DuskKit.WhiteBrush : DuskKit.LightBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(label, 0);
            grid.Children.Add(label);

            // Slider control
            var ctrlStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            var trackCanvas = new Canvas { Width = 220, Height = 20, VerticalAlignment = VerticalAlignment.Center };
            var bgTrack = new Border
            {
                Width = 220,
                Height = 6,
                CornerRadius = new CornerRadius(3),
                Background = DuskKit.B("#3d4450")
            };
            Canvas.SetLeft(bgTrack, 0);
            Canvas.SetTop(bgTrack, 7);
            trackCanvas.Children.Add(bgTrack);

            double fillW = Math.Max(0, 220.0 * (val / 100.0));
            var fillTrack = new Border
            {
                Width = fillW,
                Height = 6,
                CornerRadius = new CornerRadius(3),
                Background = DuskKit.B("#1a9fff")
            };
            Canvas.SetLeft(fillTrack, 0);
            Canvas.SetTop(fillTrack, 7);
            trackCanvas.Children.Add(fillTrack);

            var knob = new Border
            {
                Width = 20,
                Height = 20,
                CornerRadius = new CornerRadius(10),
                Background = DuskKit.WhiteBrush,
                Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 6, ShadowDepth = 2, Opacity = 0.4 }
            };
            Canvas.SetLeft(knob, Math.Clamp(fillW - 10, 0, 200));
            Canvas.SetTop(knob, 0);
            trackCanvas.Children.Add(knob);

            ctrlStack.Children.Add(trackCanvas);

            var valBlock = new TextBlock
            {
                Text = $"{val}%",
                FontFamily = DuskKit.Display,
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = DuskKit.WhiteBrush,
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

        Canvas.SetLeft(leftStack, 0);
        Canvas.SetTop(leftStack, 0);
        contentCanvas.Children.Add(leftStack);

        // Right side: 8-way directional mapping grid
        var rightStack = new StackPanel { Width = 380, Orientation = Orientation.Vertical };

        var mapCard = new Border
        {
            Width = 360,
            CornerRadius = new CornerRadius(6),
            Background = DuskKit.SlateItemBrush,
            Padding = new Thickness(22)
        };
        var mapStack = new StackPanel { Orientation = Orientation.Vertical };
        mapStack.Children.Add(new TextBlock
        {
            Text = "DIRECTIONAL MAPPING",
            FontFamily = DuskKit.Display,
            FontSize = 13,
            FontWeight = FontWeights.ExtraBold,
            Foreground = DuskKit.MuteBrush,
            Margin = new Thickness(0, 0, 0, 14)
        });

        var grid8 = new Grid { Width = 280, Height = 140, HorizontalAlignment = HorizontalAlignment.Center };
        for (int i = 0; i < 3; i++) grid8.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (int j = 0; j < 3; j++) grid8.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var dirs = new (int r, int c, string sym)[]
        {
            (0, 1, "▲ Up"),
            (1, 0, "◀ Left"),
            (1, 1, "● Center"),
            (1, 2, "Right ▶"),
            (2, 1, "▼ Down")
        };
        foreach (var (r, c, sym) in dirs)
        {
            var cell = new Border
            {
                Background = (r == 1 && c == 1) ? DuskKit.B("#1a9fff") : DuskKit.SlateTabOnBrush,
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(2)
            };
            cell.Child = new TextBlock
            {
                Text = sym,
                FontFamily = DuskKit.Display,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = DuskKit.WhiteBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(cell, r);
            Grid.SetColumn(cell, c);
            grid8.Children.Add(cell);
        }
        mapStack.Children.Add(grid8);
        mapCard.Child = mapStack;
        rightStack.Children.Add(mapCard);

        // Detail card
        var detailCard = new Border
        {
            Background = DuskKit.SlateItemBrush,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(20),
            Margin = new Thickness(0, 16, 0, 0),
            Width = 360
        };
        var dStack = new StackPanel { Orientation = Orientation.Vertical };
        dStack.Children.Add(new TextBlock
        {
            Text = "TRACKING BEHAVIOR",
            FontFamily = DuskKit.Display,
            FontSize = 13,
            FontWeight = FontWeights.ExtraBold,
            Foreground = DuskKit.MuteBrush,
            Margin = new Thickness(0, 0, 0, 4)
        });
        dStack.Children.Add(new TextBlock
        {
            Text = "Ballistic Curve with Micro-Damping",
            FontFamily = DuskKit.Display,
            FontSize = 19,
            FontWeight = FontWeights.Bold,
            Foreground = DuskKit.WhiteBrush,
            Margin = new Thickness(0, 0, 0, 4)
        });
        dStack.Children.Add(new TextBlock
        {
            Text = "Provides pixel-level accuracy for desktop UI navigation, cursor snapping, and window targeting.",
            FontFamily = DuskKit.Body,
            FontSize = 14,
            Foreground = DuskKit.LightBrush,
            TextWrapping = TextWrapping.Wrap
        });
        detailCard.Child = dStack;
        rightStack.Children.Add(detailCard);

        Canvas.SetLeft(rightStack, 640);
        Canvas.SetTop(rightStack, 0);
        contentCanvas.Children.Add(rightStack);

        var shell = DuskKit.SettingsShell(
            activeKey: "mouse",
            title: "Mouse Emulation",
            lead: "Map joystick deflection to high-precision cursor movement and desktop navigation.",
            content: contentCanvas,
            hints: hints,
            onNav: key => DuskSelectPage(key)
        );

        duskPageHost.Children.Add(shell);
    }
}
