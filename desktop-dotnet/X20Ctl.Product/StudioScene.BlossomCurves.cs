using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Blossom;

namespace X20Ctl.Product;

/// <summary>
/// Curves workbench presentation for the Yozakura (Blossom) theme:
/// "The response is a string of festival lights: dark inside the dead zone, brighter as output rises.
/// A calibration lantern hangs at the current stick input point."
/// Strictly implements t_blossom.py:curves().
/// </summary>
public partial class StudioScene
{
    private void RenderBlossomCurves(Canvas host)
    {
        // 1. Sky & Top Branch Backdrop
        var sky = new Rectangle { Width = 1600, Height = 900, Fill = BlossomKit.CreateSkyBackdrop() };
        host.Children.Add(sky);

        // Top weeping cherry canopy
        host.Children.Add(BlossomKit.CanopyBranch("top", 2));

        // Drifting petals
        host.Children.Add(BlossomKit.DriftingPetals(new Rect(960, 650, 600, 200), 10, 2));

        // Left Navigation Rail
        RenderBlossomLeftNav(host, "curves");

        // 2. Title & Lead
        var titleTb = new TextBlock
        {
            Text = "Curves",
            FontFamily = BlossomKit.Display,
            FontSize = 52,
            Foreground = BlossomKit.TextWhiteBrush
        };
        Canvas.SetLeft(titleTb, 230);
        Canvas.SetTop(titleTb, 30);
        host.Children.Add(titleTb);

        var leadStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        var draftTag = new Border
        {
            Padding = new Thickness(12, 4, 12, 4),
            CornerRadius = new CornerRadius(3),
            Background = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(BlossomKit.WarmFire1, 0.0),
                    new GradientStop(BlossomKit.WarmFire2, 1.0)
                }
            },
            Effect = new DropShadowEffect { Color = Color.FromRgb(0xff, 0x96, 0x5a), BlurRadius = 14, ShadowDepth = 0, Opacity = 0.5 },
            Child = new TextBlock
            {
                Text = "LOCAL DRAFT",
                FontFamily = BlossomKit.Small,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = BlossomKit.B("#4a140f")
            }
        };
        leadStack.Children.Add(draftTag);
        leadStack.Children.Add(new TextBlock
        {
            Text = "Not written to controller memory.",
            FontFamily = BlossomKit.Body,
            FontSize = 16,
            Foreground = BlossomKit.TextMuteBrush,
            Margin = new Thickness(14, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        Canvas.SetLeft(leadStack, 234);
        Canvas.SetTop(leadStack, 106);
        host.Children.Add(leadStack);

        // 3. Category Tabs at (234, 162)
        var tabsStack = new StackPanel { Orientation = Orientation.Horizontal };
        var tabs = new[] { "Left stick", "Right stick", "Left trigger", "Right trigger" };
        for (int i = 0; i < tabs.Length; i++)
        {
            bool on = i == 0;
            var tabTb = new TextBlock
            {
                Text = tabs[i],
                FontFamily = BlossomKit.Body,
                FontSize = 17,
                Foreground = on ? BlossomKit.TextAmberBrush : BlossomKit.TextMuteBrush,
                Margin = new Thickness(0, 0, 30, 0),
                Cursor = Cursors.Hand
            };
            if (on)
            {
                tabTb.Effect = new DropShadowEffect { Color = Color.FromRgb(0xff, 0x96, 0x5a), BlurRadius = 12, ShadowDepth = 0, Opacity = 0.6 };
            }
            tabsStack.Children.Add(tabTb);
        }
        Canvas.SetLeft(tabsStack, 234);
        Canvas.SetTop(tabsStack, 162);
        host.Children.Add(tabsStack);

        // 4. Large Response Curve Pane at (300, 250, width 560, height 400)
        double gx = 300, gy = 250, gw = 560, gh = 400;

        var paneBack = new Border
        {
            Width = gw + 92,
            Height = gh + 96,
            CornerRadius = new CornerRadius(6),
            Background = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb(204, 46, 19, 40), 0.0),
                    new GradientStop(Color.FromArgb(204, 26, 10, 24), 1.0)
                }
            },
            BorderBrush = BlossomKit.B("rgba(255,210,225,0.10)"),
            BorderThickness = new Thickness(1),
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 44, ShadowDepth = 24, Opacity = 0.45 }
        };
        Canvas.SetLeft(paneBack, gx - 48);
        Canvas.SetTop(paneBack, gy - 36);
        host.Children.Add(paneBack);

        var graphCanvas = new Canvas { Width = gw, Height = gh };

        // Grid lines
        for (int i = 1; i < 10; i++)
        {
            double lx = gw * i / 10.0;
            double ly = gh * i / 10.0;
            graphCanvas.Children.Add(new Line { X1 = lx, Y1 = 0, X2 = lx, Y2 = gh, Stroke = BlossomKit.B("rgba(255,220,232,0.05)"), StrokeThickness = 1 });
            graphCanvas.Children.Add(new Line { X1 = 0, Y1 = ly, X2 = gw, Y2 = ly, Stroke = BlossomKit.B("rgba(255,220,232,0.05)"), StrokeThickness = 1 });
        }

        // Axes
        var axisPath = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse($"M 0 0 V {gh:F1} H {gw:F1}"),
            Stroke = BlossomKit.B("rgba(255,220,232,0.28)"),
            StrokeThickness = 1.4
        };
        graphCanvas.Children.Add(axisPath);

        // Deadzone shaded band (4%)
        double dzWidth = gw * 0.04;
        var dzShade = new Rectangle
        {
            Width = dzWidth,
            Height = gh,
            Fill = BlossomKit.B("rgba(255,160,190,0.08)")
        };
        Canvas.SetLeft(dzShade, 0);
        Canvas.SetTop(dzShade, 0);
        graphCanvas.Children.Add(dzShade);

        // Response curve path
        var curveGeom = new PathGeometry();
        var curveFig = new PathFigure { StartPoint = new Point(0, gh) };
        curveFig.Segments.Add(new BezierSegment(new Point(gw * 0.35, gh * 0.75), new Point(gw * 0.65, gh * 0.25), new Point(gw, 0), true));
        curveGeom.Figures.Add(curveFig);

        var curveLine = new System.Windows.Shapes.Path
        {
            Data = curveGeom,
            Stroke = BlossomKit.B("rgba(255,220,232,0.30)"),
            StrokeThickness = 1.6
        };
        graphCanvas.Children.Add(curveLine);

        // String of festival light beads along the curve
        for (int i = 0; i <= 36; i++)
        {
            double t = i / 36.0;
            // Bezier formula for curve
            double bx = Math.Pow(1 - t, 3) * 0 + 3 * Math.Pow(1 - t, 2) * t * (gw * 0.35) + 3 * (1 - t) * Math.Pow(t, 2) * (gw * 0.65) + Math.Pow(t, 3) * gw;
            double by = Math.Pow(1 - t, 3) * gh + 3 * Math.Pow(1 - t, 2) * t * (gh * 0.75) + 3 * (1 - t) * Math.Pow(t, 2) * (gh * 0.25) + Math.Pow(t, 3) * 0;
            double outVal = 1 - by / gh;

            if (t <= 0.04)
            {
                var darkBead = new Ellipse
                {
                    Width = 6.4,
                    Height = 6.4,
                    Fill = BlossomKit.B("#4a2434"),
                    Stroke = BlossomKit.B("rgba(255,200,215,0.25)"),
                    StrokeThickness = 0.8
                };
                Canvas.SetLeft(darkBead, bx - 3.2);
                Canvas.SetTop(darkBead, by - 3.2);
                graphCanvas.Children.Add(darkBead);
            }
            else
            {
                double r = 3 + 2.6 * outVal;
                var litBead = new Ellipse
                {
                    Width = r * 2,
                    Height = r * 2,
                    Fill = BlossomKit.B("#ffd9a0"),
                    Opacity = 0.5 + 0.5 * outVal,
                    Effect = new DropShadowEffect
                    {
                        Color = Color.FromRgb(0xff, 0x96, 0x5a),
                        BlurRadius = 10,
                        ShadowDepth = 0,
                        Opacity = 0.85
                    }
                };
                Canvas.SetLeft(litBead, bx - r);
                Canvas.SetTop(litBead, by - r);
                graphCanvas.Children.Add(litBead);
            }
        }

        // Calibration Hanging Paper Lantern Bead at input 55%
        double calibT = 0.55;
        double cx_ = Math.Pow(1 - calibT, 3) * 0 + 3 * Math.Pow(1 - calibT, 2) * calibT * (gw * 0.35) + 3 * (1 - calibT) * Math.Pow(calibT, 2) * (gw * 0.65) + Math.Pow(calibT, 3) * gw;
        double cy_ = Math.Pow(1 - calibT, 3) * gh + 3 * Math.Pow(1 - calibT, 2) * calibT * (gh * 0.75) + 3 * (1 - calibT) * Math.Pow(calibT, 2) * (gh * 0.25) + Math.Pow(calibT, 3) * 0;

        var calibLantern = BlossomKit.PaperLantern(30, 40, true, null, null, 0.8, 10);
        Canvas.SetLeft(calibLantern, cx_ - calibLantern.Width / 2.0);
        Canvas.SetTop(calibLantern, cy_ - 24);
        graphCanvas.Children.Add(calibLantern);

        Canvas.SetLeft(graphCanvas, gx);
        Canvas.SetTop(graphCanvas, gy);
        host.Children.Add(graphCanvas);

        // Curve Graph Labels
        var inLabel = new TextBlock { Text = "Input %", FontFamily = BlossomKit.Body, FontSize = 13, Foreground = BlossomKit.TextMuteBrush };
        Canvas.SetLeft(inLabel, gx);
        Canvas.SetTop(inLabel, gy + gh + 18);
        host.Children.Add(inLabel);

        var outLabel = new TextBlock { Text = "Output % ↑", FontFamily = BlossomKit.Body, FontSize = 13, Foreground = BlossomKit.TextMuteBrush };
        Canvas.SetLeft(outLabel, gx + gw - 70);
        Canvas.SetTop(outLabel, gy + gh + 18);
        host.Children.Add(outLabel);

        var dzLabel = new TextBlock { Text = "4%", FontFamily = BlossomKit.Body, FontSize = 13, Foreground = BlossomKit.TextMuteBrush };
        Canvas.SetLeft(dzLabel, gx + 6);
        Canvas.SetTop(dzLabel, gy - 26);
        host.Children.Add(dzLabel);

        // 5. Right Configuration Sliders at (980, 236, width 560)
        var rightStack = new StackPanel { Width = 560 };
        rightStack.Children.Add(new TextBlock
        {
            Text = "Left stick",
            FontFamily = BlossomKit.Display,
            FontSize = 30,
            Foreground = BlossomKit.TextWhiteBrush,
            Margin = new Thickness(0, 0, 0, 24)
        });

        var sliders = new (string Name, double Pct, string Val, bool Active)[]
        {
            ("Dead zone", 4, "4%", true),
            ("Sensitivity", 100, "100%", false),
            ("Max zone", 100, "100%", false),
            ("Dynamic smoothing", 0, "0%", false)
        };

        foreach (var s in sliders)
        {
            var rowBorder = new Border
            {
                Height = 60,
                CornerRadius = new CornerRadius(4),
                Background = s.Active
                    ? new LinearGradientBrush
                    {
                        StartPoint = new Point(0, 0.5),
                        EndPoint = new Point(1, 0.5),
                        GradientStops = new GradientStopCollection
                        {
                            new GradientStop(Color.FromArgb(80, 255, 176, 110), 0.0),
                            new GradientStop(Color.FromArgb(28, 255, 120, 140), 0.6),
                            new GradientStop(Colors.Transparent, 1.0)
                        }
                    }
                    : BlossomKit.B("transparent"),
                BorderBrush = s.Active ? BlossomKit.B("#ffb56b") : BlossomKit.B("rgba(255,220,230,0.08)"),
                BorderThickness = s.Active ? new Thickness(3, 0, 0, 0) : new Thickness(0, 0, 0, 1),
                Padding = new Thickness(18, 0, 18, 0),
                Cursor = Cursors.Hand
            };

            var grid_ = new Grid();
            grid_.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid_.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var labelTb = new TextBlock
            {
                Text = s.Name,
                FontFamily = BlossomKit.Body,
                FontSize = 19,
                Foreground = s.Active ? BlossomKit.B("#ffe6cc") : BlossomKit.TextWhiteBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(labelTb, 0);
            grid_.Children.Add(labelTb);

            var sliderElem = BlossomKit.LitCordSlider(s.Pct, 220, s.Val);
            Grid.SetColumn(sliderElem, 1);
            grid_.Children.Add(sliderElem);

            rowBorder.Child = grid_;
            rightStack.Children.Add(rowBorder);
        }

        // Keyboard Test action button
        var actionRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 30, 0, 0)
        };
        var btnBorder = new Border
        {
            Height = 46,
            Padding = new Thickness(22, 0, 22, 0),
            CornerRadius = new CornerRadius(23),
            Background = BlossomKit.B("rgba(255,220,232,0.06)"),
            BorderBrush = BlossomKit.B("rgba(246,233,236,0.5)"),
            BorderThickness = new Thickness(1.5),
            Cursor = Cursors.Hand,
            Child = new TextBlock
            {
                Text = "Keyboard test",
                FontFamily = BlossomKit.Body,
                FontSize = 17,
                Foreground = BlossomKit.TextWhiteBrush,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        actionRow.Children.Add(btnBorder);
        actionRow.Children.Add(new TextBlock
        {
            Text = "Connect a controller or use Keyboard test",
            FontFamily = BlossomKit.Body,
            FontSize = 15,
            Foreground = BlossomKit.TextMuteBrush,
            Margin = new Thickness(18, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        rightStack.Children.Add(actionRow);

        Canvas.SetLeft(rightStack, 980);
        Canvas.SetTop(rightStack, 236);
        host.Children.Add(rightStack);

        // Hints: [A] Adjust  [X] Reset  [B] Back
        var hintsStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        hintsStack.Children.Add(CreateHint("A", "Adjust"));
        hintsStack.Children.Add(CreateHint("X", "Reset"));
        hintsStack.Children.Add(CreateHint("B", "Back"));

        Canvas.SetRight(hintsStack, 44);
        Canvas.SetTop(hintsStack, 846);
        host.Children.Add(hintsStack);
    }
}
