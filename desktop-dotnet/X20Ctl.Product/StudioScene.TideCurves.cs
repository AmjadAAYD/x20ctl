using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Tide;

namespace X20Ctl.Product;

public sealed partial class StudioScene
{
    private void TideShowCurves()
    {
        tideAdvanced = false;
        ConsoleSurface.Children.Clear();
        var board = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };

        // 1. Sea Background with "CURVES"
        board.Children.Add(TideKit.SeaBackground(9, "CURVES"));

        // 2. Character: x15-starfield at (-40, 500, w=500)
        board.Children.Add(TideKit.Character("x15-starfield", -40, 500, 500, -22));

        // 3. Status Box
        board.Children.Add(TideKit.StatusBox("19:24", isBack: true, onBack: () => TideShowDashboard()));

        // 4. Top Row of Tabs
        board.Children.Add(TideKit.TopBand(86));

        string[] curveTabs = ["Left stick", "Right stick", "Left trigger", "Right trigger"];
        for (int i = 0; i < curveTabs.Length; i++)
        {
            bool on = (i == 0);
            var tile = new Border
            {
                Width = 250,
                Height = 250,
                Background = on ? TideKit.WhiteBrush : TideKit.Sea0Brush,
                BorderBrush = TideKit.WhiteBrush,
                BorderThickness = new Thickness(4),
                CornerRadius = new CornerRadius(6),
                Effect = new DropShadowEffect { BlurRadius = 18, ShadowDepth = 6, Direction = 270, Opacity = 0.45, Color = Color.FromArgb(255, 0, 0, 40) }
            };

            tile.Child = new TextBlock
            {
                Text = curveTabs[i].ToUpperInvariant(),
                FontFamily = TideKit.Display,
                FontWeight = FontWeights.Black,
                FontStyle = FontStyles.Italic,
                FontSize = 30,
                Foreground = on ? TideKit.Sea0Brush : TideKit.WhiteBrush,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            Canvas.SetLeft(tile, 210 + i * 268);
            Canvas.SetTop(tile, 104);
            board.Children.Add(tile);
        }

        // Chevron arrow
        var arrow = new Border
        {
            Width = 58,
            Height = 58,
            CornerRadius = new CornerRadius(29),
            Background = TideKit.InkBrush,
            BorderBrush = TideKit.WhiteBrush,
            BorderThickness = new Thickness(3)
        };
        arrow.Child = new TextBlock
        {
            Text = "\uE76C",
            FontFamily = TideKit.Icons,
            FontSize = 28,
            Foreground = TideKit.WhiteBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        Canvas.SetRight(arrow, 22);
        Canvas.SetTop(arrow, 160);
        board.Children.Add(arrow);

        // 5. Persona Card with Curve Plot at (1250, 380, w=330, h=330) rotated 3deg
        var curveCard = TideKit.PersonaCard(330, 330, 3);
        var plotCanvas = new Canvas { Width = 290, Height = 290 };

        // Grid lines
        for (int i = 0; i <= 290; i += 29)
        {
            plotCanvas.Children.Add(new Line { X1 = i, Y1 = 0, X2 = i, Y2 = 290, Stroke = new SolidColorBrush(Color.FromArgb((byte)(255 * 0.15), 11, 29, 142)), StrokeThickness = 1 });
            plotCanvas.Children.Add(new Line { X1 = 0, Y1 = i, X2 = 290, Y2 = i, Stroke = new SolidColorBrush(Color.FromArgb((byte)(255 * 0.15), 11, 29, 142)), StrokeThickness = 1 });
        }

        // Curved response path
        var curveGeom = new PathGeometry();
        var curveFig = new PathFigure { StartPoint = new Point(0, 290) };
        curveFig.Segments.Add(new BezierSegment(new Point(100, 290), new Point(190, 160), new Point(290, 0), true));
        curveGeom.Figures.Add(curveFig);
        plotCanvas.Children.Add(new System.Windows.Shapes.Path
        {
            Data = curveGeom,
            Stroke = TideKit.Sea0Brush,
            StrokeThickness = 7
        });

        // Current point dot at (160, 130)
        var dot = new Ellipse
        {
            Width = 22,
            Height = 22,
            Fill = TideKit.SpikeRedBrush,
            Stroke = TideKit.InkBrush,
            StrokeThickness = 4
        };
        Canvas.SetLeft(dot, 160 - 11);
        Canvas.SetTop(dot, 130 - 11);
        plotCanvas.Children.Add(dot);

        curveCard.Child = plotCanvas;
        Canvas.SetLeft(curveCard, 1250);
        Canvas.SetTop(curveCard, 380);
        board.Children.Add(curveCard);

        // 6. Slanted Menu Words at (450, 846): DEAD ZONE 0%, FULL OUTPUT 100%, CURVE 0%
        string[] curveFields = ["DEAD ZONE 0%", "FULL OUTPUT 100%", "CURVE 0%"];
        for (int i = 0; i < curveFields.Length; i++)
        {
            var word = TideKit.SlantedWord(curveFields[i], focused: (i == 0), fs: 50);
            Canvas.SetLeft(word, 450 + i * 220);
            Canvas.SetTop(word, 846 - 50);
            board.Children.Add(word);
        }

        // 7. Hints
        var hintsBar = TideKit.HintsBar(new[] { "\u24BD Reset", "\u24B6 Adjust" });
        Canvas.SetRight(hintsBar, 40);
        Canvas.SetBottom(hintsBar, 26);
        board.Children.Add(hintsBar);

        ConsoleSurface.Children.Add(board);
    }

    private void TideShowAdvanced()
    {
        tideAdvanced = true;
        ConsoleSurface.Children.Clear();
        var frame = new TideToolFrame(
            "STICK TOOLS",
            "PC-side processing for accessible analog sticks",
            onBack: () => TideShowCurves(),
            hints: new[] { "\u24B7 Back", "\u24B6 Adjust", "\u24BD Reset curve", "\u24BE Next preset" },
            seed: 12);

        // Left List: Tabs + Staircase rows
        var listStack = new StackPanel { Width = 640 };
        var tabsRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
        tabsRow.Children.Add(TideKit.PersonaButton("LEFT STICK", primary: true));
        var tab2 = TideKit.PersonaButton("RIGHT STICK", primary: false);
        tab2.Margin = new Thickness(14, 0, 0, 0);
        tabsRow.Children.Add(tab2);
        listStack.Children.Add(tabsRow);

        listStack.Children.Add(TideKit.PersonaRow("Link stick settings", ctl: "toggle"));
        listStack.Children.Add(TideKit.PersonaRow("Bypass X20CTL processing", ctl: "toggle"));

        listStack.Children.Add(TideKit.SectionHeader("Input range"));
        listStack.Children.Add(TideKit.PersonaRow("Inner dead zone", value: "0.0%", ctl: "slider:0", focused: true));
        listStack.Children.Add(TideKit.PersonaRow("Full output at", value: "100.0%", ctl: "slider:100"));

        listStack.Children.Add(TideKit.SectionHeader("Output range"));
        listStack.Children.Add(TideKit.PersonaRow("Minimum output", value: "0.0%", ctl: "slider:0"));
        listStack.Children.Add(TideKit.PersonaRow("Maximum output", value: "100.0%", ctl: "slider:100"));

        listStack.Children.Add(TideKit.PersonaRow("Gate: \u25CB Circular / \u25A1 Square", ctl: "toggle"));
        listStack.Children.Add(TideKit.PersonaRow("Vertical sensitivity", value: "100%", ctl: "slider:50"));
        listStack.Children.Add(TideKit.PersonaRow("Reset range to 1:1", ctl: "chev"));
        listStack.Children.Add(TideKit.PersonaRow("Measure drift", ctl: "chev"));
        listStack.Children.Add(TideKit.PersonaRow("Reset selected advanced settings", ctl: "chev"));

        var listHost = new Canvas
        {
            Width = 640,
            RenderTransformOrigin = new Point(0, 0),
            RenderTransform = new RotateTransform(-3)
        };
        listHost.Children.Add(listStack);
        frame.SetLeftList(listHost, 176);

        // Right side: Visual Card + Detail Card
        var plotCanvas = new Canvas { Width = 556, Height = 335 };
        for (int i = 0; i <= 556; i += 51)
        {
            plotCanvas.Children.Add(new Line { X1 = i, Y1 = 10, X2 = i, Y2 = 305, Stroke = new SolidColorBrush(Color.FromArgb((byte)(255 * 0.15), 11, 29, 142)), StrokeThickness = 1 });
        }
        for (int i = 10; i <= 305; i += 29)
        {
            plotCanvas.Children.Add(new Line { X1 = 20, Y1 = i, X2 = 536, Y2 = i, Stroke = new SolidColorBrush(Color.FromArgb((byte)(255 * 0.15), 11, 29, 142)), StrokeThickness = 1 });
        }
        var lineGeom = new LineGeometry(new Point(20, 305), new Point(536, 10));
        plotCanvas.Children.Add(new System.Windows.Shapes.Path { Data = lineGeom, Stroke = TideKit.Sea0Brush, StrokeThickness = 7 });
        var stickDot = new Ellipse { Width = 18, Height = 18, Fill = TideKit.SpikeRedBrush };
        Canvas.SetLeft(stickDot, 20 + 516 * 0.55 - 9);
        Canvas.SetTop(stickDot, 10 + 295 * 0.45 - 9);
        plotCanvas.Children.Add(stickDot);

        var inLbl = new TextBlock { Text = "Input %", FontFamily = TideKit.Body, FontWeight = FontWeights.Bold, FontSize = 12, Foreground = TideKit.SubBrush };
        Canvas.SetLeft(inLbl, 20); Canvas.SetTop(inLbl, 310);
        plotCanvas.Children.Add(inLbl);

        var outLbl = new TextBlock { Text = "Output % \u2191", FontFamily = TideKit.Body, FontWeight = FontWeights.Bold, FontSize = 12, Foreground = TideKit.SubBrush };
        Canvas.SetRight(outLbl, 20); Canvas.SetTop(outLbl, 310);
        plotCanvas.Children.Add(outLbl);

        var visCard = TideKit.VisualCard(plotCanvas, 580, -2);

        var detailCard = TideKit.DetailCard(
            title: "Inner dead zone",
            value: "0.0%",
            chips: new[] { "0%", "3%", "5%" },
            body: "Nothing is written to controller memory. Settings save locally as you edit; sliders adjust in 0.1% steps.",
            foot: "PC routing inactive · Changes save locally",
            buttons: new[] { "Copy to right stick", "Done" },
            width: 580,
            rot: 2);

        var sideStack = TideKit.SideStack(visCard, detailCard, 580);
        frame.SetSideStack(sideStack, 176);
        frame.SetNote("PC routing inactive · Changes save locally");

        ConsoleSurface.Children.Add(frame);
    }
}
