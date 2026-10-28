using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Volt;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private UIElement BuildVoltCurves()
    {
        var root = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };

        // 1. Yellow wave backdrop
        root.Children.Add(VoltKit.WaveBackdrop(16));

        // 2. Top bar
        root.Children.Add(VoltKit.TopBar());

        // 3. Left side menu
        var pages = new (string Key, string Label, string Icon)[]
        {
            ("buttons", "Buttons", "\uE765"),
            ("curves", "Curves", "\uE9D9"),
            ("macros", "Macros", "\uE7C3"),
            ("vibration", "Vibration", "\uE8F1"),
            ("pcmode", "PC Mode", "\uE7F4"),
            ("device", "Device", "\uE770"),
            ("diagnostics", "Diagnostics", "\uE9E9"),
            ("mouse", "Mouse", "\uE962"),
            ("setups", "Setups", "\uE71D"),
            ("tester", "Tester", "\uE7FC"),
            ("tools", "Tools", "\uE713")
        };
        var menu = VoltKit.SideMenu(pages, "curves", VoltSelectPage);
        Canvas.SetLeft(menu, 0);
        Canvas.SetTop(menu, 98);
        root.Children.Add(menu);

        // 4. Tabs at (540, 110)
        var tabRow = new StackPanel { Orientation = Orientation.Horizontal };
        var tabs = new[] { ("Left stick", true), ("Right stick", false), ("Left trigger", false), ("Right trigger", false) };
        foreach (var (tName, active) in tabs)
        {
            var tab = new Border
            {
                Padding = new Thickness(20, 9, 20, 9),
                CornerRadius = new CornerRadius(12),
                Background = active ? VoltKit.AmberHotBrush : VoltKit.B("#1d1d1d"),
                Margin = new Thickness(0, 0, 10, 0),
                Cursor = Cursors.Hand,
                Child = new TextBlock
                {
                    Text = tName,
                    FontFamily = VoltKit.Display,
                    FontSize = 18,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = active ? VoltKit.B("#111111") : VoltKit.WhiteBrush
                }
            };
            tabRow.Children.Add(tab);
        }
        Canvas.SetLeft(tabRow, 540);
        Canvas.SetTop(tabRow, 110);
        root.Children.Add(tabRow);

        // 5. Main Card at (540, 186, w: 1020, h: 570)
        var card = new Border
        {
            Width = 1020,
            Height = 570,
            Background = VoltKit.CardDarkBrush,
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(40, 34, 40, 34),
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 60, ShadowDepth = 26, Opacity = 0.45 }
        };

        var cardCanvas = new Canvas { Width = 940, Height = 502 };

        // Headers
        var hdrStack = new StackPanel { Orientation = Orientation.Vertical };
        hdrStack.Children.Add(new TextBlock
        {
            Text = "Left stick response",
            FontFamily = VoltKit.Display,
            FontSize = 30,
            FontWeight = FontWeights.SemiBold,
            Foreground = VoltKit.WhiteBrush
        });
        hdrStack.Children.Add(new TextBlock
        {
            Text = "LOCAL DRAFT · Not written to controller memory.",
            FontFamily = VoltKit.Body,
            FontSize = 16,
            Foreground = VoltKit.MuteBrush,
            Margin = new Thickness(0, 6, 0, 24)
        });
        Canvas.SetLeft(hdrStack, 0);
        Canvas.SetTop(hdrStack, 0);
        cardCanvas.Children.Add(hdrStack);

        // Sliders on left (width: 480)
        var sliderStack = new StackPanel { Orientation = Orientation.Vertical, Width = 480 };
        var fields = new (string Name, int Val, bool Active)[]
        {
            ("Anti-deadzone", 0, false),
            ("Max range", 100, false),
            ("Linearity", 55, true),
            ("Smoothing", 15, false)
        };

        foreach (var (fName, val, active) in fields)
        {
            var row = new Border
            {
                Height = 74,
                Background = active ? VoltKit.B("#262626") : Brushes.Transparent,
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(16, 0, 16, 0),
                Margin = new Thickness(0, 0, 0, 4)
            };

            var rStack = new Grid { VerticalAlignment = VerticalAlignment.Center };
            rStack.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
            rStack.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rStack.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });

            var lbl = new TextBlock
            {
                Text = fName,
                FontFamily = VoltKit.Display,
                FontSize = 21,
                FontWeight = FontWeights.SemiBold,
                Foreground = VoltKit.WhiteBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(lbl, 0);
            rStack.Children.Add(lbl);

            // Slider track
            var track = new Canvas { Height = 10, VerticalAlignment = VerticalAlignment.Center };
            var baseLine = new Border
            {
                Width = 200,
                Height = 8,
                CornerRadius = new CornerRadius(4),
                Background = VoltKit.B("#3b3b3b")
            };
            track.Children.Add(baseLine);

            double fillW = 200 * (val / 100.0);
            var fillLine = new Border
            {
                Width = fillW,
                Height = 8,
                CornerRadius = new CornerRadius(4),
                Background = VoltKit.AmberHotBrush
            };
            track.Children.Add(fillLine);

            var knob = new Border
            {
                Width = 26,
                Height = 26,
                CornerRadius = new CornerRadius(13),
                Background = VoltKit.WhiteBrush,
                Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 8, ShadowDepth = 2, Opacity = 0.5 }
            };
            Canvas.SetLeft(knob, Math.Max(0, fillW - 13));
            Canvas.SetTop(knob, -9);
            track.Children.Add(knob);

            Grid.SetColumn(track, 1);
            rStack.Children.Add(track);

            var valBlock = new TextBlock
            {
                Text = $"{val}%",
                FontFamily = VoltKit.Display,
                FontSize = 20,
                FontWeight = FontWeights.SemiBold,
                Foreground = active ? VoltKit.AmberHotBrush : VoltKit.WhiteBrush,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(valBlock, 2);
            rStack.Children.Add(valBlock);

            row.Child = rStack;
            sliderStack.Children.Add(row);
        }
        Canvas.SetLeft(sliderStack, 0);
        Canvas.SetTop(sliderStack, 84);
        cardCanvas.Children.Add(sliderStack);

        // 6. Response Curve Graph on right at (540, 40, w: 380, h: 380)
        double gw = 380, gh = 380;
        var graphBox = new Border
        {
            Width = gw,
            Height = gh,
            Background = VoltKit.B("#0b0b0b"),
            CornerRadius = new CornerRadius(14),
            ClipToBounds = true,
            BorderBrush = VoltKit.B("#222222"),
            BorderThickness = new Thickness(1)
        };
        var graphCanvas = new Canvas { Width = gw, Height = gh };

        // Grid lines
        for (int i = 1; i <= 5; i++)
        {
            double step = gw * (i / 6.0);
            var vLine = new System.Windows.Shapes.Line
            {
                X1 = step,
                Y1 = 0,
                X2 = step,
                Y2 = gh,
                Stroke = VoltKit.B("rgba(255,255,255,.06)"),
                StrokeThickness = 1
            };
            graphCanvas.Children.Add(vLine);

            var hLine = new System.Windows.Shapes.Line
            {
                X1 = 0,
                Y1 = step,
                X2 = gw,
                Y2 = step,
                Stroke = VoltKit.B("rgba(255,255,255,.06)"),
                StrokeThickness = 1
            };
            graphCanvas.Children.Add(hLine);
        }

        // Response curve path
        var curveGeo = new StreamGeometry();
        using (var ctx = curveGeo.Open())
        {
            ctx.BeginFigure(new Point(0, gh), false, false);
            ctx.BezierTo(new Point(gw * 0.40, gh * 0.70), new Point(gw * 0.60, gh * 0.25), new Point(gw, 0), true, true);
        }
        curveGeo.Freeze();

        var curveLine = new System.Windows.Shapes.Path
        {
            Data = curveGeo,
            Stroke = VoltKit.AmberHotBrush,
            StrokeThickness = 5,
            StrokeEndLineCap = PenLineCap.Round,
            Effect = new DropShadowEffect { Color = VoltKit.AmberHot, BlurRadius = 14, ShadowDepth = 0, Opacity = 0.85 }
        };
        graphCanvas.Children.Add(curveLine);

        // Digital HUD readout badge
        var hudPill = new Border
        {
            Background = VoltKit.B("rgba(20,20,20,0.85)"),
            BorderBrush = VoltKit.B("rgba(255,173,10,0.4)"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 4, 10, 4)
        };
        hudPill.Child = new TextBlock
        {
            Text = "IN: 55%  OUT: 58%  ·  DYNAMIC S-CURVE",
            FontFamily = VoltKit.Display,
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Foreground = VoltKit.AmberHotBrush
        };
        Canvas.SetRight(hudPill, 12);
        Canvas.SetTop(hudPill, 12);
        graphCanvas.Children.Add(hudPill);

        // Radar concentric pulse ring around active dot
        var radarRing = new Ellipse
        {
            Width = 36,
            Height = 36,
            Stroke = VoltKit.AmberHotBrush,
            StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection { 3, 2 },
            Effect = new DropShadowEffect { Color = VoltKit.AmberHot, BlurRadius = 8, ShadowDepth = 0, Opacity = 0.9 }
        };
        Canvas.SetLeft(radarRing, gw * 0.55 - 18);
        Canvas.SetTop(radarRing, gh * 0.42 - 18);
        graphCanvas.Children.Add(radarRing);

        // Active control point
        var activeDot = new Border
        {
            Width = 20,
            Height = 20,
            CornerRadius = new CornerRadius(10),
            Background = VoltKit.WhiteBrush,
            BorderBrush = VoltKit.AmberHotBrush,
            BorderThickness = new Thickness(3),
            Effect = new DropShadowEffect { Color = VoltKit.AmberHot, BlurRadius = 16, ShadowDepth = 0, Opacity = 0.95 }
        };
        Canvas.SetLeft(activeDot, gw * 0.55 - 10);
        Canvas.SetTop(activeDot, gh * 0.42 - 10);
        graphCanvas.Children.Add(activeDot);

        graphBox.Child = graphCanvas;
        Canvas.SetLeft(graphBox, 530);
        Canvas.SetTop(graphBox, 60);
        cardCanvas.Children.Add(graphBox);

        card.Child = cardCanvas;
        Canvas.SetLeft(card, 540);
        Canvas.SetTop(card, 186);
        root.Children.Add(card);

        // 7. Dots + Bottom Bar
        var dots = VoltKit.CarouselDots(2, 4);
        Canvas.SetLeft(dots, 760);
        Canvas.SetTop(dots, 812);
        root.Children.Add(dots);

        var bar = VoltKit.BottomBar(new[] { ("X", "RESET CURVE"), ("A", "SELECT"), ("B", "BACK") });
        Canvas.SetLeft(bar, 0);
        Canvas.SetTop(bar, 844);
        root.Children.Add(bar);

        return root;
    }
}
