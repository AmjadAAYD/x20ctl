using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Nexus;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private void NexusShowCurves()
    {
        nexusPageHost!.Children.Clear();

        // 1. Background gradient
        var bg = new Rectangle
        {
            Width = 1600, Height = 900,
            Fill = new LinearGradientBrush
            {
                StartPoint = new(0.5, 0), EndPoint = new(0.5, 1),
                GradientStops = { new(NexusKit.C("#1a1c20"), 0.0), new(NexusKit.House, 1.0) }
            }
        };
        nexusPageHost.Children.Add(bg);

        // 2. Top bar
        var topBar = NexusKit.TopBar(
            player: Player,
            model: Model.Name,
            status: "Offline",
            onZone: () => BackRequested?.Invoke(),
            onSetups: () => ShowManagement("Setups"),
            onPcMode: () => ShowManagement("PcMode"),
            onTools: () => ToolsRequested?.Invoke()
        );
        Canvas.SetLeft(topBar, 62);
        Canvas.SetTop(topBar, 46);
        nexusPageHost.Children.Add(topBar);

        // 3. Title & Tabs
        var title = new TextBlock
        {
            Text = "Curves",
            FontFamily = NexusKit.Display,
            FontSize = 38,
            FontWeight = FontWeights.SemiBold,
            Foreground = NexusKit.WhiteBrush
        };
        Canvas.SetLeft(title, 68);
        Canvas.SetTop(title, 140);
        nexusPageHost.Children.Add(title);

        var tabsStack = new StackPanel { Orientation = Orientation.Horizontal };
        string[] tabs = { "Left stick", "Right stick", "Left trigger · LT", "Right trigger · RT" };
        for (int i = 0; i < tabs.Length; i++)
        {
            string t = tabs[i];
            bool on = (i == 0);
            var tabBtn = new Border
            {
                Padding = new Thickness(18, 8, 18, 8),
                Margin = new Thickness(0, 0, 10, 0),
                CornerRadius = new CornerRadius(4),
                Background = on ? NexusKit.WhiteBrush : NexusKit.TileBrush,
                Child = new TextBlock
                {
                    Text = t,
                    FontFamily = NexusKit.Body,
                    FontSize = 16,
                    FontWeight = on ? FontWeights.SemiBold : FontWeights.Normal,
                    Foreground = on ? NexusKit.B("#111111") : NexusKit.WhiteBrush
                }
            };
            tabsStack.Children.Add(tabBtn);
        }
        Canvas.SetLeft(tabsStack, 68);
        Canvas.SetTop(tabsStack, 200);
        nexusPageHost.Children.Add(tabsStack);

        // 4. Left Graph Panel at (68, 264, w=860, h=580)
        var graphPanel = new Border
        {
            Width = 860, Height = 580,
            CornerRadius = new CornerRadius(8),
            Background = NexusKit.PanelBrush
        };
        Canvas.SetLeft(graphPanel, 68);
        Canvas.SetTop(graphPanel, 264);
        nexusPageHost.Children.Add(graphPanel);

        // SVG Canvas: gw=760, gh=500 at (118, 300)
        double gw = 760, gh = 500;
        var graphCanvas = new Canvas { Width = gw, Height = gh, ClipToBounds = true };

        // Grid lines every 50px
        var gridBrush = NexusKit.B(NexusKit.A(Colors.White, 0.06));
        for (double gx = 0; gx <= gw; gx += 50)
        {
            var vl = new Line { X1 = gx, Y1 = 0, X2 = gx, Y2 = gh, Stroke = gridBrush, StrokeThickness = 1 };
            graphCanvas.Children.Add(vl);
        }
        for (double gy = 0; gy <= gh; gy += 50)
        {
            var hl = new Line { X1 = 0, Y1 = gy, X2 = gw, Y2 = gy, Stroke = gridBrush, StrokeThickness = 1 };
            graphCanvas.Children.Add(hl);
        }

        // Curve Path: dz=0.04, full=0.96, gamma=1.6
        Point PointOnCurve(double t)
        {
            double dz = 0.04, full = 0.96, gamma = 1.6;
            double y = t <= dz ? 0.0 : (t >= full ? 1.0 : Math.Pow((t - dz) / (full - dz), gamma));
            return new Point(t * gw, gh - y * gh);
        }

        var curveGeom = new StreamGeometry();
        using (var ctx = curveGeom.Open())
        {
            Point start = PointOnCurve(0);
            ctx.BeginFigure(start, false, false);
            for (int s = 1; s <= 60; s++)
            {
                ctx.LineTo(PointOnCurve(s / 60.0), true, true);
            }
        }
        curveGeom.Freeze();

        var curvePath = new System.Windows.Shapes.Path
        {
            Data = curveGeom,
            Stroke = NexusKit.GreenBrush,
            StrokeThickness = 4
        };
        graphCanvas.Children.Add(curvePath);

        // Control Points at t = 0.2, 0.45, 0.7
        foreach (double t in new[] { 0.2, 0.45, 0.7 })
        {
            var pt = PointOnCurve(t);
            var dot = new Ellipse
            {
                Width = 18, Height = 18,
                Fill = NexusKit.HouseBrush,
                Stroke = NexusKit.WhiteBrush,
                StrokeThickness = 3
            };
            Canvas.SetLeft(dot, pt.X - 9);
            Canvas.SetTop(dot, pt.Y - 9);
            graphCanvas.Children.Add(dot);
        }

        // Active point at t = 0.55
        var apt = PointOnCurve(0.55);
        var activeDot = new Ellipse
        {
            Width = 14, Height = 14,
            Fill = NexusKit.GreenBrush
        };
        Canvas.SetLeft(activeDot, apt.X - 7);
        Canvas.SetTop(activeDot, apt.Y - 7);
        graphCanvas.Children.Add(activeDot);

        Canvas.SetLeft(graphCanvas, 118);
        Canvas.SetTop(graphCanvas, 300);
        nexusPageHost.Children.Add(graphCanvas);

        var axesLabel = new TextBlock
        {
            Text = "Input %   ·   Output % ↑",
            FontFamily = NexusKit.Body,
            FontSize = 15,
            Foreground = NexusKit.SubBrush
        };
        Canvas.SetLeft(axesLabel, 118);
        Canvas.SetTop(axesLabel, 808);
        nexusPageHost.Children.Add(axesLabel);

        // 5. Right Faders Panel at (952, 264, w=580, h=580)
        var faderPanel = new Border
        {
            Width = 580, Height = 580,
            Padding = new Thickness(30, 28, 30, 28),
            CornerRadius = new CornerRadius(8),
            Background = NexusKit.PanelBrush
        };
        var fStack = new StackPanel();

        var fields = new (string Name, int Val)[]
        {
            ("Inner dead zone", 4),
            ("Anti-dead zone", 0),
            ("Full output at", 96),
            ("Max output", 100)
        };

        for (int i = 0; i < fields.Length; i++)
        {
            var f = fields[i];
            bool isFocused = (i == 0);

            var fieldBox = new Border
            {
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 24),
                CornerRadius = new CornerRadius(4),
                Background = Brushes.Transparent
            };
            if (isFocused)
            {
                fieldBox.BorderBrush = NexusKit.GreenBrush;
                fieldBox.BorderThickness = new Thickness(3);
            }

            var itemStack = new StackPanel();
            var textRow = new Grid();
            textRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            textRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var nameText = new TextBlock
            {
                Text = f.Name,
                FontFamily = NexusKit.Body,
                FontSize = 18,
                Foreground = NexusKit.WhiteBrush
            };
            Grid.SetColumn(nameText, 0);
            textRow.Children.Add(nameText);

            var valText = new TextBlock
            {
                Text = $"{f.Val}%",
                FontFamily = NexusKit.Body,
                FontSize = 18,
                Foreground = NexusKit.WhiteBrush
            };
            Grid.SetColumn(valText, 1);
            textRow.Children.Add(valText);
            itemStack.Children.Add(textRow);

            // Slider
            var slider = NexusKit.Slider(f.Val, 0, 100, width: 500);
            slider.Margin = new Thickness(0, 16, 0, 0);
            itemStack.Children.Add(slider);

            fieldBox.Child = itemStack;
            fStack.Children.Add(fieldBox);
        }

        var connectNote = new TextBlock
        {
            Text = "Connect a controller or use Keyboard test",
            FontFamily = NexusKit.Body,
            FontSize = 15,
            Foreground = NexusKit.SubBrush,
            Margin = new Thickness(0, 24, 0, 0)
        };
        fStack.Children.Add(connectNote);

        faderPanel.Child = fStack;
        Canvas.SetLeft(faderPanel, 952);
        Canvas.SetTop(faderPanel, 264);
        nexusPageHost.Children.Add(faderPanel);

        // 6. Bottom Hints
        var hints = NexusKit.HintsBar(new[] { ("A", "Adjust"), ("B", "Back") });
        Canvas.SetRight(hints, 62);
        Canvas.SetTop(hints, 856);
        nexusPageHost.Children.Add(hints);
    }

    private void NexusShowAdvanced()
    {
        nexusPageHost!.Children.Clear();

        // 1. Background gradient
        var bg = new Rectangle
        {
            Width = 1600, Height = 900,
            Fill = new LinearGradientBrush
            {
                StartPoint = new(0.5, 0), EndPoint = new(0.5, 1),
                GradientStops = { new(NexusKit.C("#1a1c20"), 0.0), new(NexusKit.House, 1.0) }
            }
        };
        nexusPageHost.Children.Add(bg);

        // 2. Top bar
        var topBar = NexusKit.TopBar(
            player: Player,
            model: Model.Name,
            status: "Offline",
            onZone: () => BackRequested?.Invoke(),
            onSetups: () => ShowManagement("Setups"),
            onPcMode: () => ShowManagement("PcMode"),
            onTools: () => ToolsRequested?.Invoke()
        );
        Canvas.SetLeft(topBar, 62);
        Canvas.SetTop(topBar, 46);
        nexusPageHost.Children.Add(topBar);

        // 3. Title & Lead
        var title = new TextBlock
        {
            Text = "Advanced stick tools",
            FontFamily = NexusKit.Display,
            FontSize = 38,
            FontWeight = FontWeights.SemiBold,
            Foreground = NexusKit.WhiteBrush
        };
        Canvas.SetLeft(title, 68);
        Canvas.SetTop(title, 138);
        nexusPageHost.Children.Add(title);

        var lead = new TextBlock
        {
            Text = "Dead zones, tension trim and response smoothing for hardware-level precision.",
            FontFamily = NexusKit.Body,
            FontSize = 17,
            Foreground = NexusKit.SubBrush
        };
        Canvas.SetLeft(lead, 70);
        Canvas.SetTop(lead, 192);
        nexusPageHost.Children.Add(lead);

        // Tabs: Left stick, Right stick
        var tabsStack = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var (t, on) in new[] { ("Left stick", true), ("Right stick", false) })
        {
            var tabBtn = new Border
            {
                Padding = new Thickness(18, 8, 18, 8),
                Margin = new Thickness(0, 0, 10, 0),
                CornerRadius = new CornerRadius(4),
                Background = on ? NexusKit.WhiteBrush : NexusKit.TileBrush,
                Child = new TextBlock
                {
                    Text = t,
                    FontFamily = NexusKit.Body,
                    FontSize = 16,
                    FontWeight = on ? FontWeights.SemiBold : FontWeights.Normal,
                    Foreground = on ? NexusKit.B("#111111") : NexusKit.WhiteBrush
                }
            };
            tabsStack.Children.Add(tabBtn);
        }
        Canvas.SetLeft(tabsStack, 68);
        Canvas.SetTop(tabsStack, 230);
        nexusPageHost.Children.Add(tabsStack);

        // 4. Left Rows Column at (68, 280, w=820, h=520)
        var rowPanel = new StackPanel { Width = 820 };

        // Input range
        rowPanel.Children.Add(new TextBlock
        {
            Text = "Input range",
            FontFamily = NexusKit.Body,
            FontSize = 15,
            Foreground = NexusKit.SubBrush,
            Margin = new Thickness(2, 6, 0, 10)
        });

        // Focused: Inner dead zone
        var r1 = NexusKit.RowButton("Inner dead zone", null, NexusKit.Slider(0, 0, 100, 210), focused: true, width: 820);
        rowPanel.Children.Add(r1);

        var r2 = NexusKit.RowButton("Full output at", null, NexusKit.Slider(100, 0, 100, 210), focused: false, width: 820);
        rowPanel.Children.Add(r2);

        // Output range
        rowPanel.Children.Add(new TextBlock
        {
            Text = "Output range",
            FontFamily = NexusKit.Body,
            FontSize = 15,
            Foreground = NexusKit.SubBrush,
            Margin = new Thickness(2, 12, 0, 10)
        });

        var r3 = NexusKit.RowButton("Minimum output", null, NexusKit.Slider(0, 0, 100, 210), focused: false, width: 820);
        rowPanel.Children.Add(r3);

        var r4 = NexusKit.RowButton("Maximum output", null, NexusKit.Slider(100, 0, 100, 210), focused: false, width: 820);
        rowPanel.Children.Add(r4);

        var r5 = NexusKit.RowButton("Gate circularity", null, NexusKit.Toggle(false), focused: false, width: 820);
        rowPanel.Children.Add(r5);

        Canvas.SetLeft(rowPanel, 68);
        Canvas.SetTop(rowPanel, 280);
        nexusPageHost.Children.Add(rowPanel);

        // 5. Right Details Side at (920, 280, w=612)
        var sideStack = new StackPanel { Width = 612 };

        // Visual 1:1 curve
        var curveBox = new Border
        {
            Width = 612, Height = 200,
            CornerRadius = new CornerRadius(8),
            Background = NexusKit.B("#17191c"),
            Margin = new Thickness(0, 0, 0, 18)
        };
        var cCanvas = new Canvas { Width = 572, Height = 170, Margin = new Thickness(20, 15, 20, 15) };
        var straightLine = new Line { X1 = 0, Y1 = 170, X2 = 572, Y2 = 0, Stroke = NexusKit.GreenBrush, StrokeThickness = 4 };
        cCanvas.Children.Add(straightLine);
        var cDot = new Ellipse { Width = 14, Height = 14, Fill = NexusKit.WhiteBrush };
        Canvas.SetLeft(cDot, 572 * 0.55 - 7);
        Canvas.SetTop(cDot, 170 * 0.45 - 7);
        cCanvas.Children.Add(cDot);
        curveBox.Child = cCanvas;
        sideStack.Children.Add(curveBox);

        // Details card
        var card = new Border
        {
            Width = 612,
            Padding = new Thickness(28, 26, 28, 26),
            CornerRadius = new CornerRadius(8),
            Background = NexusKit.PanelBrush
        };
        var cardStack = new StackPanel();
        cardStack.Children.Add(new TextBlock
        {
            Text = "Inner dead zone",
            FontFamily = NexusKit.Display,
            FontSize = 24,
            FontWeight = FontWeights.SemiBold,
            Foreground = NexusKit.WhiteBrush
        });
        cardStack.Children.Add(new TextBlock
        {
            Text = "0.0%",
            FontFamily = NexusKit.Display,
            FontSize = 46,
            FontWeight = FontWeights.SemiBold,
            Foreground = NexusKit.WhiteBrush,
            Margin = new Thickness(0, 4, 0, 0)
        });

        // Chips
        var chips = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 12) };
        foreach (var (ch, on) in new[] { ("Linear", true), ("Smooth", false), ("Aggressive", false) })
        {
            var chip = new Border
            {
                Padding = new Thickness(16, 6, 16, 6),
                Margin = new Thickness(0, 0, 8, 0),
                CornerRadius = new CornerRadius(4),
                Background = on ? NexusKit.WhiteBrush : NexusKit.TileBrush,
                Child = new TextBlock
                {
                    Text = ch,
                    FontFamily = NexusKit.Body,
                    FontSize = 14,
                    FontWeight = on ? FontWeights.SemiBold : FontWeights.Normal,
                    Foreground = on ? NexusKit.B("#111111") : NexusKit.WhiteBrush
                }
            };
            chips.Children.Add(chip);
        }
        cardStack.Children.Add(chips);

        cardStack.Children.Add(new TextBlock
        {
            Text = "Minimum stick deflection before output begins. Keep low for reactive aim, raise if experiencing stick drift.",
            FontFamily = NexusKit.Body,
            FontSize = 16,
            Foreground = NexusKit.B(NexusKit.A(Colors.White, 0.78)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 16)
        });

        var doneBtn = NexusKit.TheatricalButton("Done", primary: true, height: 48, fontSize: 16);
        cardStack.Children.Add(doneBtn);

        card.Child = cardStack;
        sideStack.Children.Add(card);

        Canvas.SetLeft(sideStack, 920);
        Canvas.SetTop(sideStack, 280);
        nexusPageHost.Children.Add(sideStack);

        // 6. Bottom Hints
        var hints = NexusKit.HintsBar(new[] { ("A", "Adjust"), ("B", "Back"), ("X", "Reset curve"), ("Y", "Next preset") });
        Canvas.SetRight(hints, 62);
        Canvas.SetTop(hints, 856);
        nexusPageHost.Children.Add(hints);
    }
}
