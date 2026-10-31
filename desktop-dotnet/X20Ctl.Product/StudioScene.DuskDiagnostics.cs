using System.Windows.Data;
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
    private FrameworkElement? duskDiagnosticsView;
    private void DuskShowDiagnostics()
    {
        if (Diagnostics is not {} diagnostics) return;
        duskPageHost!.Children.Clear();
        if (duskDiagnosticsView != null) { duskPageHost.Children.Add(duskDiagnosticsView); return; }

        var hints = new[]
        {
            ("X", "RE-CENTER"),
            ("A", "SELECT"),
            ("B", "BACK")
        };

        var contentCanvas = new Canvas { Width = 1040, Height = 580 };

        // Left list (w: 580)
        var leftStack = new StackPanel { Width = 580, Orientation = Orientation.Vertical };

        leftStack.Children.Add(new TextBlock
        {
            Text = "LIVE SENSOR TELEMETRY",
            FontFamily = DuskKit.Display,
            FontSize = 18,
            FontWeight = FontWeights.ExtraBold,
            Foreground = DuskKit.MuteBrush,
            Margin = new Thickness(0, 0, 0, 4)
        });

        leftStack.Children.Add(new TextBlock
        {
            Text = "Selected-source XInput measurements. Hardware polling rate is separate.",
            FontFamily = DuskKit.Body,
            FontSize = 15,
            Foreground = DuskKit.MuteBrush,
            Margin = new Thickness(0, 0, 0, 14)
        });

        var diagRows = new (string Label, TextBlock Val, bool Active)[]
        {
            ("Left stick", diagnostics.LeftView.Stats, true),
            ("Right stick", diagnostics.RightView.Stats, false),
            ("LT", diagnostics.LtView.Stats, false),
            ("RT", diagnostics.RtView.Stats, false),
            ("XInput state changes", diagnostics.RateBlock, false)
        };

        foreach (var (label, val, active) in diagRows)
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

            var lbl = new TextBlock
            {
                Text = label,
                FontFamily = DuskKit.Display,
                FontSize = 20,
                FontWeight = FontWeights.SemiBold,
                Foreground = active ? DuskKit.WhiteBrush : DuskKit.LightBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(lbl, 0);
            grid.Children.Add(lbl);

            var valBlock = new TextBlock
            {
                Text = val.Text,
                FontFamily = DuskKit.Display,
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = active ? DuskKit.WhiteBrush : DuskKit.GreenOnlineBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            valBlock.MaxWidth = 300; valBlock.TextWrapping = TextWrapping.Wrap; valBlock.FontSize = 14; valBlock.SetBinding(TextBlock.TextProperty, new Binding("Text") { Source = val });
            Grid.SetColumn(valBlock, 1);
            grid.Children.Add(valBlock);

            row.Child = grid;
            leftStack.Children.Add(row);
        }

        var slots = new WrapPanel { Margin = new(0,12,0,8) };
        for (int i = 0; i < 4; i++) { int slot = i; slots.Children.Add(DuskMacroAction("Player " + (i + 1), "diagnostics:slot:" + i, () => diagnostics.ChooseSlot(slot))); }
        leftStack.Children.Add(slots);
        var actions = new WrapPanel();
        foreach (var (text, tag, action) in new (string,string,Action)[] { ("Measure rest", "measure", diagnostics.Measure), ("Clear", "clear", diagnostics.ClearAll), ("Raw / normalized", "raw", diagnostics.ToggleRaw), ("Copy report", "copy", diagnostics.CopyReport) }) actions.Children.Add(DuskMacroAction(text, "diagnostics:" + tag, action));
        leftStack.Children.Add(actions);
        var source = new TextBlock { Foreground = DuskKit.LightBrush, TextWrapping = TextWrapping.Wrap, Margin = new(0,12,0,0), FontSize = 13 }; source.SetBinding(TextBlock.TextProperty, new Binding("Text") { Source = diagnostics.SourceBlock }); leftStack.Children.Add(source);
        Canvas.SetLeft(leftStack, 0);
        Canvas.SetTop(leftStack, 0);
        contentCanvas.Children.Add(leftStack);

        // Right side: Dual stick reticles visual
        var rightStack = new StackPanel { Width = 380, Orientation = Orientation.Vertical };

        var sticksBorder = new Border
        {
            Width = 360,
            Height = 220,
            CornerRadius = new CornerRadius(6),
            Background = DuskKit.B("#141920"),
            BorderBrush = DuskKit.B("rgba(26,159,255,0.35)"),
            BorderThickness = new Thickness(1, 2, 1, 1),
            Effect = new DropShadowEffect { Color = DuskKit.CyanNeon, BlurRadius = 14, ShadowDepth = 0, Opacity = 0.25 }
        };
        var sticksCanvas = new Canvas { Width = 360, Height = 220 };

        // Left Reticle (Center at 100, 110, Radius 70)
        var leftCircle = new Ellipse
        {
            Width = 140,
            Height = 140,
            Stroke = DuskKit.B("#3d4450"),
            StrokeThickness = 2
        };
        Canvas.SetLeft(leftCircle, 30);
        Canvas.SetTop(leftCircle, 40);
        sticksCanvas.Children.Add(leftCircle);

        var crossLH = new Line { X1 = 30, Y1 = 110, X2 = 170, Y2 = 110, Stroke = DuskKit.B("rgba(255,255,255,.15)"), StrokeThickness = 1 };
        var crossLV = new Line { X1 = 100, Y1 = 40, X2 = 100, Y2 = 180, Stroke = DuskKit.B("rgba(255,255,255,.15)"), StrokeThickness = 1 };
        sticksCanvas.Children.Add(crossLH);
        sticksCanvas.Children.Add(crossLV);

        var ptL = new Ellipse
        {
            Width = 12,
            Height = 12,
            Fill = DuskKit.B("#1a9fff"),
            Effect = new DropShadowEffect { Color = DuskKit.C("#1a9fff"), BlurRadius = 8, ShadowDepth = 0, Opacity = 0.9 }
        };
        Canvas.SetLeft(ptL, 94);
        Canvas.SetTop(ptL, 104);
        sticksCanvas.Children.Add(ptL);

        var lblL = new TextBlock
        {
            Text = "LS (X: 0, Y: 0)",
            FontFamily = DuskKit.Display,
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Foreground = DuskKit.MuteBrush
        };
        Canvas.SetLeft(lblL, 55);
        Canvas.SetTop(lblL, 190);
        sticksCanvas.Children.Add(lblL);

        // Right Reticle (Center at 260, 110, Radius 70)
        var rightCircle = new Ellipse
        {
            Width = 140,
            Height = 140,
            Stroke = DuskKit.B("#3d4450"),
            StrokeThickness = 2
        };
        Canvas.SetLeft(rightCircle, 190);
        Canvas.SetTop(rightCircle, 40);
        sticksCanvas.Children.Add(rightCircle);

        var crossRH = new Line { X1 = 190, Y1 = 110, X2 = 330, Y2 = 110, Stroke = DuskKit.B("rgba(255,255,255,.15)"), StrokeThickness = 1 };
        var crossRV = new Line { X1 = 260, Y1 = 40, X2 = 260, Y2 = 180, Stroke = DuskKit.B("rgba(255,255,255,.15)"), StrokeThickness = 1 };
        sticksCanvas.Children.Add(crossRH);
        sticksCanvas.Children.Add(crossRV);

        var ptR = new Ellipse
        {
            Width = 12,
            Height = 12,
            Fill = DuskKit.B("#59bf40"),
            Effect = new DropShadowEffect { Color = DuskKit.C("#59bf40"), BlurRadius = 8, ShadowDepth = 0, Opacity = 0.9 }
        };
        Canvas.SetLeft(ptR, 254);
        Canvas.SetTop(ptR, 104);
        sticksCanvas.Children.Add(ptR);

        var lblR = new TextBlock
        {
            Text = "RS (X: 0, Y: 0)",
            FontFamily = DuskKit.Display,
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Foreground = DuskKit.MuteBrush
        };
        Canvas.SetLeft(lblR, 215);
        Canvas.SetTop(lblR, 190);
        sticksCanvas.Children.Add(lblR);

        // Tactical HUD corner brackets & dual hall badge
        sticksCanvas.Children.Add(DuskKit.CornerBrackets(360, 220, DuskKit.CyanNeon, 14, 1.2));

        var stickChip = new Border
        {
            Padding = new Thickness(8, 2, 8, 2),
            Background = DuskKit.B("rgba(14,18,24,0.85)"),
            BorderBrush = DuskKit.B("rgba(26,159,255,0.4)"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Child = new TextBlock
            {
                Text = "SELECTED SOURCE · XINPUT",
                FontFamily = DuskKit.Display,
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                Foreground = DuskKit.CyanNeonBrush
            }
        };
        Canvas.SetRight(stickChip, 12);
        Canvas.SetTop(stickChip, 10);
        sticksCanvas.Children.Add(stickChip);

        sticksBorder.Child = sticksCanvas;
        rightStack.Children.Add(sticksBorder);

        // Diagnostic summary card
        var card = new Border
        {
            Background = DuskKit.SlateItemBrush,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(20),
            Margin = new Thickness(0, 16, 0, 0),
            Width = 360
        };
        var cStack = new StackPanel { Orientation = Orientation.Vertical };
        cStack.Children.Add(new TextBlock
        {
            Text = "CIRCULARITY CHECK",
            FontFamily = DuskKit.Display,
            FontSize = 13,
            FontWeight = FontWeights.ExtraBold,
            Foreground = DuskKit.MuteBrush,
            Margin = new Thickness(0, 0, 0, 4)
        });
        cStack.Children.Add(new TextBlock
        {
            Text = "Measurements",
            FontFamily = DuskKit.Display,
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            Foreground = DuskKit.GreenOnlineBrush,
            Margin = new Thickness(0, 0, 0, 4)
        });
        cStack.Children.Add(new TextBlock
        {
            Text = "Move the selected controller to collect measurements.",
            FontFamily = DuskKit.Body,
            FontSize = 14,
            Foreground = DuskKit.LightBrush,
            TextWrapping = TextWrapping.Wrap
        });
        ((TextBlock)cStack.Children[2]).SetBinding(TextBlock.TextProperty, new Binding("Text") { Source = diagnostics.DriftBlock });
        void UpdatePlots()
        {
            foreach (var (dot, native, x) in new[] { (ptL, diagnostics.LeftView.Dot, 30.0), (ptR, diagnostics.RightView.Dot, 190.0) })
            {
                dot.Visibility = native.Visibility;
                Canvas.SetLeft(dot, x + 140 * (Canvas.GetLeft(native) + native.Width / 2) / X20Ctl.Product.Diagnostics.DiagnosticsWorkbench.PlotSize - dot.Width / 2);
                Canvas.SetTop(dot, 40 + 140 * (Canvas.GetTop(native) + native.Height / 2) / X20Ctl.Product.Diagnostics.DiagnosticsWorkbench.PlotSize - dot.Height / 2);
            }
        }
        diagnostics.Refreshed += UpdatePlots; UpdatePlots();
        lblL.SetBinding(TextBlock.TextProperty, new Binding("Text") { Source = diagnostics.LeftView.Stats });
        lblR.SetBinding(TextBlock.TextProperty, new Binding("Text") { Source = diagnostics.RightView.Stats });
        card.Child = cStack;
        rightStack.Children.Add(card);

        Canvas.SetLeft(rightStack, 640);
        Canvas.SetTop(rightStack, 0);
        contentCanvas.Children.Add(rightStack);

        var shell = DuskKit.SettingsShell(
            activeKey: "diagnostics",
            title: "Hardware Diagnostics",
            lead: "Real-time input telemetry, circularity checks, and live stick coordinate reticles.",
            content: contentCanvas,
            hints: hints,
            onNav: key => DuskSelectPage(key)
        );

        duskDiagnosticsView = shell; duskPageHost.Children.Add(shell);
    }
}
