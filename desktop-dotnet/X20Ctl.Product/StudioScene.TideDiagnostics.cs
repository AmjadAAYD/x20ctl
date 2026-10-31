using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Tide;

namespace X20Ctl.Product;

public sealed partial class StudioScene
{
    private void TideShowDiagnostics()
    {
        if (tidePageHost == null) return;
        tidePageHost.Children.Clear();

        if (Diagnostics is not { } diagnostics)
        {
            var emptyFrame = new TideToolFrame(
                "DIAGNOSTICS",
                "Sensor diagnostics unavailable.",
                onBack: () => TideShowDashboard(),
                hints: new[] { "\u24B7 Back" },
                seed: 16);
            ConsoleSurface.Children.Add(emptyFrame);
            return;
        }

        var frame = new TideToolFrame(
            "DIAGNOSTICS",
            "Roll sticks along outer edge, squeeze triggers and press buttons. Only listens; no writes.",
            onBack: () => TideShowDashboard(),
            hints: new[] { "\u24B7 Exit Capture", "\u24B6 Measure", "\u24BD Clear", "\u2295 Copy Report" },
            seed: 16);

        TideKit.AnimateEntrance(frame);

        // 1. Left List: Live Telemetry Rows (230, 160, w=580)
        var leftCol = new StackPanel { Width = 580 };

        leftCol.Children.Add(TideKit.SectionHeader("LIVE SENSOR TELEMETRY"));

        var diagRows = new (string Label, TextBlock StatsBlock)[]
        {
            ("Left Analog Stick", diagnostics.LeftView.Stats),
            ("Right Analog Stick", diagnostics.RightView.Stats),
            ("Left Trigger (LT)", diagnostics.LtView.Stats),
            ("Right Trigger (RT)", diagnostics.RtView.Stats),
            ("State Changes / Polling", diagnostics.RateBlock)
        };

        foreach (var (lbl, blk) in diagRows)
        {
            var rowBorder = new Border
            {
                Height = 56,
                Background = TideKit.Sea0Brush,
                BorderBrush = TideKit.WhiteBrush,
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(14, 0, 14, 0),
                Margin = new Thickness(0, 0, 0, 8),
                Effect = new DropShadowEffect { BlurRadius = 0, ShadowDepth = 4, Direction = 315, Color = TideKit.Ink }
            };

            var grid = new Grid { VerticalAlignment = VerticalAlignment.Center };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var label = new TextBlock
            {
                Text = lbl.ToUpperInvariant(),
                FontFamily = TideKit.Display,
                FontWeight = FontWeights.Black,
                FontStyle = FontStyles.Italic,
                FontSize = 13,
                Foreground = TideKit.WhiteBrush,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(label, 0);
            grid.Children.Add(label);

            var valTb = new TextBlock
            {
                FontFamily = TideKit.Display,
                FontWeight = FontWeights.Black,
                FontStyle = FontStyles.Italic,
                FontSize = 12,
                Foreground = TideKit.LightCyanBrush,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            valTb.SetBinding(TextBlock.TextProperty, new Binding("Text") { Source = blk });
            Grid.SetColumn(valTb, 1);
            grid.Children.Add(valTb);

            rowBorder.Child = grid;
            leftCol.Children.Add(rowBorder);
        }

        // Action Buttons: Measure Rest, Clear Counters, Copy Report
        var actRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };

        var measureBtn = TideKit.PersonaButton("MEASURE REST", primary: true, onClick: diagnostics.Measure);
        measureBtn.Height = 38;
        actRow.Children.Add(ThemeAction(measureBtn, "theme:diagnostics:measure", diagnostics.Measure));

        var clearBtn = TideKit.PersonaButton("CLEAR", primary: false, onClick: diagnostics.ClearAll);
        clearBtn.Height = 38;
        clearBtn.Margin = new Thickness(8, 0, 0, 0);
        actRow.Children.Add(ThemeAction(clearBtn, "theme:diagnostics:clear", diagnostics.ClearAll));

        var copyBtn = TideKit.PersonaButton("COPY REPORT", primary: false, onClick: diagnostics.CopyReport);
        copyBtn.Height = 38;
        copyBtn.Margin = new Thickness(8, 0, 0, 0);
        actRow.Children.Add(ThemeAction(copyBtn, "theme:diagnostics:copy", diagnostics.CopyReport));

        var rawBtn = TideKit.PersonaButton("RAW MODE", primary: false, onClick: diagnostics.ToggleRaw);
        rawBtn.Height = 38;
        rawBtn.Margin = new Thickness(8, 0, 0, 0);
        actRow.Children.Add(ThemeAction(rawBtn, "theme:diagnostics:raw", diagnostics.ToggleRaw));

        leftCol.Children.Add(actRow);

        var listHost = new Canvas
        {
            Width = 580,
            RenderTransformOrigin = new Point(0, 0),
            RenderTransform = new RotateTransform(-2)
        };
        listHost.Children.Add(leftCol);
        frame.SetLeftList(listHost, 160);

        // 2. Right Side: Persona Radar Reticles Card (880, 160, w=660, h=520)
        var radarCard = TideKit.PersonaCard(660, 520, 2);
        var cardInner = new Canvas { Width = 624, Height = 484 };

        // 1. Left stick radar at (30, 20, size 180)
        var lRadar = new Canvas { Width = 180, Height = 180 };
        lRadar.Children.Add(new Ellipse { Width = 180, Height = 180, Stroke = TideKit.Sea0Brush, StrokeThickness = 3 });
        var lInner = new Ellipse { Width = 110, Height = 110, Stroke = TideKit.Sea0Brush, StrokeThickness = 2, Opacity = 0.5 };
        Canvas.SetLeft(lInner, 35); Canvas.SetTop(lInner, 35);
        lRadar.Children.Add(lInner);

        var lAxisY = new Line { X1 = 90, Y1 = 10, X2 = 90, Y2 = 170, Stroke = TideKit.Sea0Brush, StrokeThickness = 1.5, Opacity = 0.4 };
        var lAxisX = new Line { X1 = 10, Y1 = 90, X2 = 170, Y2 = 90, Stroke = TideKit.Sea0Brush, StrokeThickness = 1.5, Opacity = 0.4 };
        lRadar.Children.Add(lAxisY);
        lRadar.Children.Add(lAxisX);

        var lSweep = new Line { X1 = 90, Y1 = 90, X2 = 170, Y2 = 90, Stroke = TideKit.SpikeRedBrush, StrokeThickness = 2.5 };
        lSweep.RenderTransformOrigin = new Point(0, 0);

        Canvas.SetLeft(lRadar, 40);
        Canvas.SetTop(lRadar, 30);
        cardInner.Children.Add(lRadar);

        var lTitle = new TextBlock
        {
            Text = "LEFT STICK RADAR",
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 14,
            Foreground = TideKit.Sea0Brush,
            TextAlignment = TextAlignment.Center,
            Width = 180
        };
        Canvas.SetLeft(lTitle, 40);
        Canvas.SetTop(lTitle, 220);
        cardInner.Children.Add(lTitle);

        // 2. Right stick radar at (260, 20, size 180)
        var rRadar = new Canvas { Width = 180, Height = 180 };
        rRadar.Children.Add(new Ellipse { Width = 180, Height = 180, Stroke = TideKit.Sea0Brush, StrokeThickness = 3 });
        var rInner = new Ellipse { Width = 110, Height = 110, Stroke = TideKit.Sea0Brush, StrokeThickness = 2, Opacity = 0.5 };
        Canvas.SetLeft(rInner, 35); Canvas.SetTop(rInner, 35);
        rRadar.Children.Add(rInner);

        var rAxisY = new Line { X1 = 90, Y1 = 10, X2 = 90, Y2 = 170, Stroke = TideKit.Sea0Brush, StrokeThickness = 1.5, Opacity = 0.4 };
        var rAxisX = new Line { X1 = 10, Y1 = 90, X2 = 170, Y2 = 90, Stroke = TideKit.Sea0Brush, StrokeThickness = 1.5, Opacity = 0.4 };
        rRadar.Children.Add(rAxisY);
        rRadar.Children.Add(rAxisX);

        var rSweep = new Line { X1 = 90, Y1 = 90, X2 = 170, Y2 = 90, Stroke = TideKit.LightCyanBrush, StrokeThickness = 2.5 };
        rSweep.RenderTransformOrigin = new Point(0, 0);

        Canvas.SetLeft(rRadar, 260);
        Canvas.SetTop(rRadar, 30);
        cardInner.Children.Add(rRadar);

        var rTitle = new TextBlock
        {
            Text = "RIGHT STICK RADAR",
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 14,
            Foreground = TideKit.Sea0Brush,
            TextAlignment = TextAlignment.Center,
            Width = 180
        };
        Canvas.SetLeft(rTitle, 260);
        Canvas.SetTop(rTitle, 220);
        cardInner.Children.Add(rTitle);

        // 3. Trigger Meters (LT and RT)
        var ltMeter = new Border
        {
            Width = 40,
            Height = 180,
            Background = TideKit.Sea0Brush,
            BorderBrush = TideKit.InkBrush,
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(4)
        };
        Canvas.SetLeft(ltMeter, 480);
        Canvas.SetTop(ltMeter, 30);
        cardInner.Children.Add(ltMeter);

        var ltTxt = new TextBlock
        {
            Text = "LT",
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 14,
            Foreground = TideKit.Sea0Brush,
            TextAlignment = TextAlignment.Center,
            Width = 40
        };
        Canvas.SetLeft(ltTxt, 480);
        Canvas.SetTop(ltTxt, 220);
        cardInner.Children.Add(ltTxt);

        var rtMeter = new Border
        {
            Width = 40,
            Height = 180,
            Background = TideKit.Sea0Brush,
            BorderBrush = TideKit.InkBrush,
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(4)
        };
        Canvas.SetLeft(rtMeter, 540);
        Canvas.SetTop(rtMeter, 30);
        cardInner.Children.Add(rtMeter);

        var rtTxt = new TextBlock
        {
            Text = "RT",
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 14,
            Foreground = TideKit.Sea0Brush,
            TextAlignment = TextAlignment.Center,
            Width = 40
        };
        Canvas.SetLeft(rtTxt, 540);
        Canvas.SetTop(rtTxt, 220);
        cardInner.Children.Add(rtTxt);

        // Status banner: the live source line, never a claimed polling rate.
        var sourceTxt = new TextBlock
        {
            FontFamily = TideKit.Display, FontWeight = FontWeights.Black, FontStyle = FontStyles.Italic,
            FontSize = 14, Foreground = TideKit.Sea0Brush, Width = 560, TextWrapping = TextWrapping.Wrap
        };
        sourceTxt.SetBinding(TextBlock.TextProperty, new Binding("Text") { Source = diagnostics.SourceBlock });
        Canvas.SetLeft(sourceTxt, 30);
        Canvas.SetTop(sourceTxt, 330);
        cardInner.Children.Add(sourceTxt);

        radarCard.Child = cardInner;
        Canvas.SetLeft(radarCard, 880);
        Canvas.SetTop(radarCard, 160);
        frame.ContentArea.Children.Add(radarCard);

        ConsoleSurface.Children.Add(frame);
    }
}
