using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
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
        if (Tester is not { } tester)
        {
            var empty = new Canvas { Width = 970, Height = 560 };
            empty.Children.Add(new TextBlock { Text = "Controller tester is unavailable.", Foreground = VoltKit.MuteBrush, FontSize = 18, Margin = new Thickness(40) });
            return VoltKit.TwoPaneShell("tester", "Tester", "Hardware input verification, digital button state, and signal latency.", empty, VoltSelectPage);
        }

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

        // Telemetry rows: Source, Pressed, Ownership
        var tRows = new (string Label, TextBlock SourceBlock)[]
        {
            ("Active Input Source", tester.SourceLabel),
            ("Pressed Buttons", tester.PressedBlock),
            ("Input Ownership", tester.OwnershipBlock)
        };

        foreach (var (lbl, srcBlock) in tRows)
        {
            var row = new Border
            {
                MinHeight = 54,
                Background = Brushes.Transparent,
                BorderBrush = VoltKit.B("#222222"),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(16, 6, 16, 6)
            };
            var grid = new Grid { VerticalAlignment = VerticalAlignment.Center };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var label = new TextBlock
            {
                Text = lbl,
                FontFamily = VoltKit.Display,
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Foreground = VoltKit.LightBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(label, 0);
            grid.Children.Add(label);

            var valTb = new TextBlock
            {
                FontFamily = VoltKit.Display,
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = VoltKit.AmberHotBrush,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            valTb.SetBinding(TextBlock.TextProperty, new Binding("Text") { Source = srcBlock });
            Grid.SetColumn(valTb, 1);
            grid.Children.Add(valTb);

            row.Child = grid;
            leftStack.Children.Add(row);
        }

        // Keyboard & mouse test toggle
        var kbRow = new Border
        {
            MinHeight = 56,
            Background = Brushes.Transparent,
            BorderBrush = VoltKit.B("#222222"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(16, 6, 16, 6),
            Cursor = Cursors.Hand
        };
        var kbGrid = new Grid { VerticalAlignment = VerticalAlignment.Center };
        kbGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        kbGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var kbLabels = new StackPanel();
        kbLabels.Children.Add(new TextBlock { Text = "Capture Keyboard Inputs", FontFamily = VoltKit.Display, FontSize = 18, FontWeight = FontWeights.SemiBold, Foreground = VoltKit.LightBrush });
        kbLabels.Children.Add(new TextBlock { Text = "Test mapped keyboard strokes (WASD, Arrows, Q, E)", FontFamily = VoltKit.Body, FontSize = 13, Foreground = VoltKit.MuteBrush });
        kbGrid.Children.Add(kbLabels);

        var kbToggleFace = new Border
        {
            Height = 36,
            Padding = new Thickness(14, 0, 14, 0),
            CornerRadius = new CornerRadius(6),
            Background = tester.KeyboardOn ? VoltKit.AmberHotBrush : VoltKit.B("#262626"),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = tester.KeyboardOn ? "Active" : "Simulate",
                FontFamily = VoltKit.Display,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = tester.KeyboardOn ? VoltKit.B("#111111") : VoltKit.WhiteBrush,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        var kbButton = ThemeAction(kbToggleFace, "theme:tester:keyboard", () =>
        {
            tester.ClickKeyboard();
            VoltLater();
        });
        Grid.SetColumn(kbButton, 1);
        kbGrid.Children.Add(kbButton);
        kbRow.Child = kbGrid;
        leftStack.Children.Add(kbRow);

        // Perspective toggle: Front / Rear
        var perspRow = new Border
        {
            MinHeight = 56,
            Background = Brushes.Transparent,
            BorderBrush = VoltKit.B("#222222"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(16, 6, 16, 6)
        };
        var perspGrid = new Grid { VerticalAlignment = VerticalAlignment.Center };
        perspGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        perspGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        perspGrid.Children.Add(new TextBlock { Text = "Controller Perspective", FontFamily = VoltKit.Display, FontSize = 18, FontWeight = FontWeights.SemiBold, Foreground = VoltKit.LightBrush, VerticalAlignment = VerticalAlignment.Center });

        var perspStack = new StackPanel { Orientation = Orientation.Horizontal };
        var frontBtn = new Border
        {
            Padding = new Thickness(14, 6, 14, 6),
            CornerRadius = new CornerRadius(6),
            Background = VoltKit.AmberHotBrush,
            Margin = new Thickness(0, 0, 8, 0),
            Child = new TextBlock { Text = "Front", FontFamily = VoltKit.Display, FontSize = 14, FontWeight = FontWeights.Bold, Foreground = VoltKit.B("#111111") }
        };
        perspStack.Children.Add(ThemeAction(frontBtn, "theme:tester:front", () => tester.ShowRear(false)));

        var rearBtn = new Border
        {
            Padding = new Thickness(14, 6, 14, 6),
            CornerRadius = new CornerRadius(6),
            Background = VoltKit.B("#262626"),
            Child = new TextBlock { Text = "Rear", FontFamily = VoltKit.Display, FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = VoltKit.WhiteBrush }
        };
        perspStack.Children.Add(ThemeAction(rearBtn, "theme:tester:rear", () => tester.ShowRear(true), HasRearArtwork));
        Grid.SetColumn(perspStack, 1);
        perspGrid.Children.Add(perspStack);
        perspRow.Child = perspGrid;
        leftStack.Children.Add(perspRow);

        // Action Buttons: Exit Tester
        var actRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(16, 18, 0, 0) };
        var exitFace = new Border
        {
            Height = 42,
            Padding = new Thickness(20, 0, 20, 0),
            CornerRadius = new CornerRadius(8),
            Background = VoltKit.B("#262626"),
            Child = new TextBlock { Text = "Exit Tester", FontFamily = VoltKit.Display, FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = VoltKit.WhiteBrush, VerticalAlignment = VerticalAlignment.Center }
        };
        actRow.Children.Add(ThemeAction(exitFace, "theme:tester:exit", () => { tester.ClickExit(); VoltSelectPage("buttons"); }));
        leftStack.Children.Add(actRow);

        Canvas.SetLeft(leftStack, 0);
        Canvas.SetTop(leftStack, 10);
        content.Children.Add(leftStack);

        // 2. Right Details Card: Real-time telemetry readout & mounted stage (width: 410 at x = 550)
        var rightStack = new StackPanel { Orientation = Orientation.Vertical, Width = 410 };

        var artBox = new Border
        {
            Width = 410,
            Height = 270,
            Background = VoltKit.B("#0b0b0b"),
            CornerRadius = new CornerRadius(14),
            ClipToBounds = true
        };
        var aCanvas = new Canvas { Width = 410, Height = 270 };

        var livePad = new Viewbox
        {
            Width = 380,
            Height = 250,
            Stretch = Stretch.Uniform,
            Child = tester.TakeStage()
        };
        Canvas.SetLeft(livePad, 15);
        Canvas.SetTop(livePad, 10);
        aCanvas.Children.Add(livePad);

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
            Text = "INPUT STREAM TELEMETRY",
            FontFamily = VoltKit.Display,
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            Foreground = VoltKit.B("#8a8a8a"),
            Margin = new Thickness(0, 0, 0, 2)
        });

        cStack.Children.Add(new TextBlock
        {
            Text = "Real-Time XInput",
            FontFamily = VoltKit.Display,
            FontSize = 30,
            FontWeight = FontWeights.SemiBold,
            Foreground = VoltKit.AmberHotBrush,
            LineHeight = 34
        });

        cStack.Children.Add(new TextBlock
        {
            Text = "Physical buttons report switch contact closures. Trigger sweep demonstrates direct potentiometer linearity.",
            FontFamily = VoltKit.Body,
            FontSize = 14,
            Foreground = VoltKit.B("#bdbdbd"),
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 20,
            Margin = new Thickness(0, 6, 0, 0)
        });

        card.Child = cStack;
        rightStack.Children.Add(card);

        Canvas.SetLeft(rightStack, 550);
        Canvas.SetTop(rightStack, 10);
        content.Children.Add(rightStack);

        return VoltKit.TwoPaneShell("tester", "Tester", "Hardware input verification, digital button state, and signal latency.", content, VoltSelectPage);
    }
}
