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
    private void HybridShowCurves()
    {
        hybridPageHost!.Children.Clear();

        var hints = new[]
        {
            ("A", "Adjust"),
            ("X", "Reset"),
            ("B", "Back")
        };

        var rails = HybridKit.HandheldRails(
            curPage: "curves",
            hints: hints,
            title: "Curves",
            lead: "LOCAL DRAFT · Not written to controller memory.",
            onNav: key => HybridSelectPage(key)
        );
        hybridPageHost.Children.Add(rails);

        // Tab cap bar at (1050, 44)
        var capPanel = new Border
        {
            Height = 42,
            CornerRadius = new CornerRadius(21),
            Background = HybridKit.B("#26232c"),
            Padding = new Thickness(4)
        };
        var capStack = new StackPanel { Orientation = Orientation.Horizontal };
        var tabs = new[] { "Left stick", "Right stick", "Triggers" };
        for (int i = 0; i < tabs.Length; i++)
        {
            bool on = i == 0;
            var tab = new Border
            {
                CornerRadius = new CornerRadius(17),
                Padding = new Thickness(18, 0, 18, 0),
                Background = on ? HybridKit.LavenderBrush : Brushes.Transparent,
                Child = new TextBlock
                {
                    Text = tabs[i],
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
        Canvas.SetLeft(capPanel, 950);
        Canvas.SetTop(capPanel, 44);
        hybridPageHost.Children.Add(capPanel);

        // Helper to construct a Stick Curve Half
        UIElement BuildCurveHalf(double x, string title, bool focus, double opacity, double width = 560)
        {
            var panel = new StackPanel { Width = width, Opacity = opacity };

            // Title row
            var tGrid = new Grid();
            tGrid.Children.Add(new TextBlock
            {
                Text = title,
                FontFamily = HybridKit.Display,
                FontSize = 24,
                FontWeight = FontWeights.ExtraBold,
                Foreground = HybridKit.WhiteBrush
            });
            if (focus)
            {
                tGrid.Children.Add(new TextBlock
                {
                    Text = "Editing",
                    FontFamily = HybridKit.Body,
                    FontSize = 12,
                    FontWeight = FontWeights.Bold,
                    Foreground = HybridKit.LavenderBrush,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center
                });
            }
            panel.Children.Add(tGrid);

            // Curve Graph SVG canvas (300x200)
            var graphCanvas = new Canvas
            {
                Width = 300,
                Height = 200,
                Background = HybridKit.B("#1c1922"),
                Margin = new Thickness((width - 300) / 2, 16, 0, 10),
                ClipToBounds = true
            };

            // Grid lines
            for (int i = 0; i <= 300; i += 30)
            {
                graphCanvas.Children.Add(new Line { X1 = i, Y1 = 0, X2 = i, Y2 = 200, Stroke = HybridKit.B("#2c2834"), StrokeThickness = 1 });
            }
            for (int j = 0; j <= 200; j += 30)
            {
                graphCanvas.Children.Add(new Line { X1 = 0, Y1 = j, X2 = 300, Y2 = j, Stroke = HybridKit.B("#2c2834"), StrokeThickness = 1 });
            }

            // Curve Path
            var curveGeom = new PathGeometry();
            var curveFig = new PathFigure { StartPoint = new Point(0, 200), IsClosed = false };
            curveFig.Segments.Add(new BezierSegment(new Point(100, 180), new Point(180, 80), new Point(300, 0), true));
            curveGeom.Figures.Add(curveFig);

            var curvePath = new System.Windows.Shapes.Path
            {
                Data = curveGeom,
                Stroke = focus ? HybridKit.LavenderBrush : HybridKit.B("#8a8496"),
                StrokeThickness = 4
            };
            if (focus)
            {
                curvePath.Effect = new DropShadowEffect { Color = HybridKit.Lavender, BlurRadius = 12, ShadowDepth = 0, Opacity = 0.8 };
            }
            graphCanvas.Children.Add(curvePath);

            // Curve Dot
            var dot = new Ellipse { Width = 18, Height = 18, Fill = Brushes.White };
            Canvas.SetLeft(dot, 165 - 9);
            Canvas.SetTop(dot, 90 - 9);
            graphCanvas.Children.Add(dot);

            panel.Children.Add(graphCanvas);

            // Sliders list
            var fields = new[] { ("Dead zone", 5), ("Anti-deadzone", 0), ("Linearity", 50), ("Max threshold", 100) };
            for (int f = 0; f < fields.Length; f++)
            {
                var (fName, fVal) = fields[f];
                bool rowFocus = focus && f == 0;

                var row = new Border
                {
                    Height = 47,
                    CornerRadius = new CornerRadius(16),
                    Background = rowFocus ? HybridKit.LavenderBrush : HybridKit.B("#1f1c25"),
                    Margin = new Thickness(0, 0, 0, 4),
                    Padding = new Thickness(22, 0, 20, 0)
                };

                var rGrid = new Grid();
                rGrid.Children.Add(new TextBlock
                {
                    Text = fName,
                    FontFamily = HybridKit.Display,
                    FontSize = 16,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = rowFocus ? HybridKit.InkBrush : HybridKit.WhiteBrush,
                    VerticalAlignment = VerticalAlignment.Center
                });

                var rc = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };

                // LED meter (10 segments)
                var ledMeter = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
                int litCount = (int)Math.Round(fVal / 10.0);
                for (int l = 0; l < 10; l++)
                {
                    bool lit = l < litCount;
                    var seg = new Border
                    {
                        Width = 13,
                        Height = 12,
                        CornerRadius = new CornerRadius(3),
                        Margin = new Thickness(l == 0 ? 0 : 4, 0, 0, 0),
                        Background = lit ? (rowFocus ? HybridKit.MauveBrush : HybridKit.LavenderBrush) : (rowFocus ? HybridKit.B("rgba(27,22,34,.16)") : HybridKit.B("#3b3744"))
                    };
                    ledMeter.Children.Add(seg);
                }
                rc.Children.Add(ledMeter);

                rc.Children.Add(new TextBlock
                {
                    Text = $"{fVal}%",
                    FontFamily = HybridKit.Body,
                    FontSize = 15,
                    Foreground = rowFocus ? HybridKit.B("#5a4b6e") : HybridKit.MuteBrush,
                    Width = 58,
                    TextAlignment = TextAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center
                });

                rGrid.Children.Add(rc);
                row.Child = rGrid;
                panel.Children.Add(row);
            }

            Canvas.SetLeft(panel, x);
            Canvas.SetTop(panel, 160);
            return panel;
        }

        // Left stick half at (222, 160, w: 560)
        hybridPageHost.Children.Add(BuildCurveHalf(222, "Left stick", focus: true, opacity: 1.0, width: 560));

        // Open Seam between sticks at x=846
        var openSeam = HybridKit.Seam(846, 160, 800, 470, locked: false);
        hybridPageHost.Children.Add(openSeam);

        // Right stick half at (898, 160, w: 486)
        hybridPageHost.Children.Add(BuildCurveHalf(898, "Right stick", focus: false, opacity: 0.5, width: 486));

        // Bottom note at (216, 792)
        var botNote = new StackPanel { Orientation = Orientation.Horizontal };
        var kbBtn = new Border
        {
            Height = 40,
            CornerRadius = new CornerRadius(20),
            Background = HybridKit.B("#302c38"),
            Padding = new Thickness(22, 0, 22, 0),
            Margin = new Thickness(0, 0, 16, 0),
            Child = new TextBlock
            {
                Text = "\uE765  Keyboard test",
                FontFamily = HybridKit.Display,
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = HybridKit.WhiteBrush,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        botNote.Children.Add(kbBtn);
        botNote.Children.Add(new TextBlock
        {
            Text = "Connect a controller or use Keyboard test",
            FontFamily = HybridKit.Body,
            FontSize = 15,
            Foreground = HybridKit.MuteBrush,
            VerticalAlignment = VerticalAlignment.Center
        });

        Canvas.SetLeft(botNote, 216);
        Canvas.SetTop(botNote, 792);
        hybridPageHost.Children.Add(botNote);
    }
}
