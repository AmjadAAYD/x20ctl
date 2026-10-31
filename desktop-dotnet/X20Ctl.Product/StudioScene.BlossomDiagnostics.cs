using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Blossom;

namespace X20Ctl.Product;

/// <summary>
/// Diagnostics workbench presentation for the Yozakura (Blossom) theme:
/// "The two sticks are two glowing celestial moons with crater textures and celestial coordinate rings;
/// the triggers are tall cylindrical paper lanterns that fill with fire as they are pulled."
/// Strictly implements t_blossom.py:diagnostics().
/// </summary>
public partial class StudioScene
{
    private void RenderBlossomDiagnostics(Canvas host)
    {
        if (Diagnostics is not { } diagnostics) return;

        // 1. Sky & Backdrop
        var sky = new Rectangle { Width = 1600, Height = 900, Fill = BlossomKit.CreateSkyBackdrop() };
        host.Children.Add(sky);

        // Drifting petals
        host.Children.Add(BlossomKit.DriftingPetals(new Rect(250, 260, 1310, 400), 10, 9));

        // Left Navigation Rail
        RenderBlossomLeftNav(host, "diagnostics");

        // 2. Title & Lead
        var titleTb = new TextBlock
        {
            Text = "Diagnostics",
            FontFamily = BlossomKit.Display,
            FontSize = 52,
            Foreground = BlossomKit.TextWhiteBrush
        };
        Canvas.SetLeft(titleTb, 230);
        Canvas.SetTop(titleTb, 30);
        host.Children.Add(titleTb);

        var leadTb = new TextBlock
        {
            Text = "Real-time analog stick centering, trigger fill chambers, and signal packet integrity.",
            FontFamily = BlossomKit.Body,
            FontSize = 16,
            Foreground = BlossomKit.TextMuteBrush
        };
        Canvas.SetLeft(leadTb, 234);
        Canvas.SetTop(leadTb, 102);
        host.Children.Add(leadTb);

        // 3. Top Action Chips at (234, 180, width 1310)
        var chipStack = new StackPanel { Orientation = Orientation.Horizontal };

        var measureChip = new Border
        {
            Height = 36,
            Padding = new Thickness(16, 0, 16, 0),
            CornerRadius = new CornerRadius(18),
            Background = BlossomKit.CreateWarmLanternBrush(),
            Margin = new Thickness(0, 0, 10, 0),
            Effect = new DropShadowEffect { Color = Color.FromRgb(0xff, 0x96, 0x5a), BlurRadius = 14, ShadowDepth = 0, Opacity = 0.5 },
            Child = new TextBlock { Text = "Measure rest drift", FontFamily = BlossomKit.Body, FontSize = 14, Foreground = BlossomKit.B("#4a140f"), VerticalAlignment = VerticalAlignment.Center }
        };
        chipStack.Children.Add(ThemeAction(measureChip, "theme:diagnostics:measure", diagnostics.Measure));

        var clearChip = new Border
        {
            Height = 36,
            Padding = new Thickness(16, 0, 16, 0),
            CornerRadius = new CornerRadius(18),
            Background = BlossomKit.B("rgba(255,220,232,0.06)"),
            BorderBrush = BlossomKit.B("rgba(255,220,232,0.22)"),
            BorderThickness = new Thickness(1.2),
            Margin = new Thickness(0, 0, 10, 0),
            Child = new TextBlock { Text = "Clear counters", FontFamily = BlossomKit.Body, FontSize = 14, Foreground = BlossomKit.B("#e3cfd6"), VerticalAlignment = VerticalAlignment.Center }
        };
        chipStack.Children.Add(ThemeAction(clearChip, "theme:diagnostics:clear", diagnostics.ClearAll));

        var rawChip = new Border
        {
            Height = 36,
            Padding = new Thickness(16, 0, 16, 0),
            CornerRadius = new CornerRadius(18),
            Background = BlossomKit.B("rgba(255,220,232,0.06)"),
            BorderBrush = BlossomKit.B("rgba(255,220,232,0.22)"),
            BorderThickness = new Thickness(1.2),
            Margin = new Thickness(0, 0, 10, 0),
            Child = new TextBlock { Text = "Raw / normalized", FontFamily = BlossomKit.Body, FontSize = 14, Foreground = BlossomKit.B("#e3cfd6"), VerticalAlignment = VerticalAlignment.Center }
        };
        chipStack.Children.Add(ThemeAction(rawChip, "theme:diagnostics:raw", diagnostics.ToggleRaw));

        var copyChip = new Border
        {
            Height = 36,
            Padding = new Thickness(16, 0, 16, 0),
            CornerRadius = new CornerRadius(18),
            Background = BlossomKit.B("rgba(255,220,232,0.06)"),
            BorderBrush = BlossomKit.B("rgba(255,220,232,0.22)"),
            BorderThickness = new Thickness(1.2),
            Child = new TextBlock { Text = "Copy report", FontFamily = BlossomKit.Body, FontSize = 14, Foreground = BlossomKit.B("#e3cfd6"), VerticalAlignment = VerticalAlignment.Center }
        };
        chipStack.Children.Add(ThemeAction(copyChip, "theme:diagnostics:copy", diagnostics.CopyReport));

        Canvas.SetLeft(chipStack, 234);
        Canvas.SetTop(chipStack, 180);
        host.Children.Add(chipStack);

        // 4. Two Celestial Moon Stick Plots at cx = 470 and cx = 860, cy = 430, r = 140
        host.Children.Add(CreateLiveMoonPlot(470, 430, 140, "Left stick", diagnostics.LeftView));
        host.Children.Add(CreateLiveMoonPlot(860, 430, 140, "Right stick", diagnostics.RightView));

        // 5. Tall Cylindrical Paper Lantern Triggers at x = 1190 and x = 1370
        var triggers = new (string Name, TextBlock Stats, int Tx)[]
        {
            ("LT", diagnostics.LtView.Stats, 1190),
            ("RT", diagnostics.RtView.Stats, 1370)
        };

        foreach (var (tName, tStats, tx) in triggers)
        {
            // Suspension wire
            host.Children.Add(new Line { X1 = tx + 30, Y1 = 260, X2 = tx + 30, Y2 = 280, Stroke = BlossomKit.B("#3a2430"), StrokeThickness = 2 });

            // Tall Cylindrical Lantern Body (60x250)
            var trigBody = new Border
            {
                Width = 60,
                Height = 250,
                CornerRadius = new CornerRadius(16, 16, 16, 16),
                Background = BlossomKit.CreateColdLanternBrush(),
                BorderBrush = BlossomKit.B("rgba(255,200,215,0.16)"),
                BorderThickness = new Thickness(1),
                ClipToBounds = true
            };

            // Ribs overlay
            var trigRibs = new Canvas { Width = 60, Height = 250 };
            for (int r = 1; r < 24; r++)
            {
                trigRibs.Children.Add(new Line
                {
                    X1 = 4,
                    X2 = 56,
                    Y1 = r * 10,
                    Y2 = r * 10,
                    Stroke = BlossomKit.B("rgba(70,20,20,0.25)"),
                    StrokeThickness = 1.0
                });
            }
            trigBody.Child = trigRibs;

            Canvas.SetLeft(trigBody, tx);
            Canvas.SetTop(trigBody, 280);
            host.Children.Add(trigBody);

            // Top and Bottom wooden caps
            var topCap = new Rectangle { Width = 40, Height = 8, RadiusX = 3, RadiusY = 3, Fill = BlossomKit.B("#160b10") };
            Canvas.SetLeft(topCap, tx + 10);
            Canvas.SetTop(topCap, 274);
            host.Children.Add(topCap);

            var botCap = new Rectangle { Width = 40, Height = 8, RadiusX = 3, RadiusY = 3, Fill = BlossomKit.B("#160b10") };
            Canvas.SetLeft(botCap, tx + 10);
            Canvas.SetTop(botCap, 528);
            host.Children.Add(botCap);

            // Label Below
            var trigLabelStack = new StackPanel { Width = 140, HorizontalAlignment = HorizontalAlignment.Center };
            trigLabelStack.Children.Add(new TextBlock
            {
                Text = tName,
                FontFamily = BlossomKit.Heading,
                FontSize = 22,
                Foreground = BlossomKit.TextWhiteBrush,
                TextAlignment = TextAlignment.Center
            });
            var valTb = new TextBlock
            {
                FontFamily = BlossomKit.Body,
                FontSize = 13,
                Foreground = BlossomKit.TextMuteBrush,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 2, 0, 0)
            };
            valTb.SetBinding(TextBlock.TextProperty, new Binding("Text") { Source = tStats });
            trigLabelStack.Children.Add(valTb);

            Canvas.SetLeft(trigLabelStack, tx - 40);
            Canvas.SetTop(trigLabelStack, 550);
            host.Children.Add(trigLabelStack);
        }

        // 6. Polling Rate Telemetry at (234, 690)
        var telemStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };

        var rateCard = new Border
        {
            Padding = new Thickness(18, 12, 22, 12),
            CornerRadius = new CornerRadius(4),
            Background = BlossomKit.B("rgba(34,14,30,0.82)"),
            BorderBrush = BlossomKit.B("rgba(255,220,232,0.1)"),
            BorderThickness = new Thickness(1)
        };
        var rStack = new StackPanel();
        rStack.Children.Add(new TextBlock
        {
            Text = "POLLING TELEMETRY",
            FontFamily = BlossomKit.Small,
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Foreground = BlossomKit.TextSakuraBrush
        });
        var rateTb = new TextBlock
        {
            FontFamily = BlossomKit.Display,
            FontSize = 24,
            Foreground = BlossomKit.TextAmberBrush,
            Margin = new Thickness(0, 2, 0, 0)
        };
        rateTb.SetBinding(TextBlock.TextProperty, new Binding("Text") { Source = diagnostics.RateBlock });
        rStack.Children.Add(rateTb);
        rateCard.Child = rStack;
        telemStack.Children.Add(rateCard);

        var statusMsg = new TextBlock
        {
            FontFamily = BlossomKit.Body,
            FontSize = 15,
            Foreground = BlossomKit.TextMuteBrush,
            Margin = new Thickness(24, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 800
        };
        statusMsg.SetBinding(TextBlock.TextProperty, new Binding("Text") { Source = diagnostics.SourceBlock });
        telemStack.Children.Add(statusMsg);

        Canvas.SetLeft(telemStack, 234);
        Canvas.SetTop(telemStack, 690);
        host.Children.Add(telemStack);

        // Hints
        var hintsStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        hintsStack.Children.Add(CreateHint("A", "Test triggers"));
        hintsStack.Children.Add(CreateHint("X", "Measure rest"));
        hintsStack.Children.Add(CreateHint("B", "Back"));

        Canvas.SetRight(hintsStack, 44);
        Canvas.SetTop(hintsStack, 846);
        host.Children.Add(hintsStack);
    }

    /// <summary>
    /// Builds a live stick plot rendered as a luminous celestial moon with coordinate rings and active position reticle.
    /// </summary>
    private FrameworkElement CreateLiveMoonPlot(double cx, double cy, double r, string label, (Polygon Reach, Ellipse Dot, Ellipse Resting, TextBlock Stats) view)
    {
        var canvas = new Canvas
        {
            Width = r * 2 + 60,
            Height = r * 2 + 100,
            IsHitTestVisible = false
        };

        double ocx = canvas.Width / 2.0;
        double ocy = r + 20;

        // Radiant Moon Glow
        var halo = new Ellipse
        {
            Width = (r + 26) * 2,
            Height = (r + 26) * 2,
            Fill = BlossomKit.B("rgba(255,235,240,0.06)"),
            Effect = new BlurEffect { Radius = 10 }
        };
        Canvas.SetLeft(halo, ocx - (r + 26));
        Canvas.SetTop(halo, ocy - (r + 26));
        canvas.Children.Add(halo);

        // Moon Sphere Surface
        var moon = new Ellipse
        {
            Width = r * 2,
            Height = r * 2,
            Fill = new RadialGradientBrush
            {
                Center = new Point(0.42, 0.38),
                GradientOrigin = new Point(0.42, 0.38),
                RadiusX = 0.7,
                RadiusY = 0.7,
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(Color.FromRgb(0x2a, 0x14, 0x22), 0.0),
                    new GradientStop(Color.FromRgb(0x18, 0x0c, 0x14), 1.0)
                }
            },
            Stroke = BlossomKit.B("rgba(255,220,232,0.3)"),
            StrokeThickness = 1.5
        };
        Canvas.SetLeft(moon, ocx - r);
        Canvas.SetTop(moon, ocy - r);
        canvas.Children.Add(moon);

        // Coordinate rings
        foreach (double f in new[] { 0.33, 0.66, 1.0 })
        {
            var ring = new Ellipse
            {
                Width = r * 2 * f,
                Height = r * 2 * f,
                Stroke = BlossomKit.B("rgba(255,220,232,0.15)"),
                StrokeThickness = 1
            };
            Canvas.SetLeft(ring, ocx - r * f);
            Canvas.SetTop(ring, ocy - r * f);
            canvas.Children.Add(ring);
        }

        // Active Reticle Dot
        var dot = new Ellipse
        {
            Width = 14,
            Height = 14,
            Fill = BlossomKit.CreateWarmLanternBrush(),
            Effect = new DropShadowEffect { Color = Color.FromRgb(0xff, 0x96, 0x5a), BlurRadius = 12, ShadowDepth = 0, Opacity = 0.8 }
        };
        Canvas.SetLeft(dot, ocx - 7);
        Canvas.SetTop(dot, ocy - 7);
        canvas.Children.Add(dot);

        canvas.Loaded += (_, _) =>
        {
            double plotSize = global::X20Ctl.Product.Diagnostics.DiagnosticsWorkbench.PlotSize;
            var native = view.Dot;
            dot.Visibility = native.Visibility;
            Canvas.SetLeft(dot, ocx + r * (Canvas.GetLeft(native) + native.Width / 2 - plotSize / 2) / (plotSize / 2) - dot.Width / 2);
            Canvas.SetTop(dot, ocy + r * (Canvas.GetTop(native) + native.Height / 2 - plotSize / 2) / (plotSize / 2) - dot.Height / 2);
        };

        // Label and stats below
        var labelStack = new StackPanel { Width = r * 2 + 40, HorizontalAlignment = HorizontalAlignment.Center };
        labelStack.Children.Add(new TextBlock
        {
            Text = label,
            FontFamily = BlossomKit.Heading,
            FontSize = 20,
            Foreground = BlossomKit.TextWhiteBrush,
            TextAlignment = TextAlignment.Center
        });
        var statsTb = new TextBlock
        {
            FontFamily = BlossomKit.Body,
            FontSize = 13,
            Foreground = BlossomKit.TextAmberBrush,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 2, 0, 0)
        };
        statsTb.SetBinding(TextBlock.TextProperty, new Binding("Text") { Source = view.Stats });
        labelStack.Children.Add(statsTb);

        Canvas.SetLeft(labelStack, ocx - (r + 20));
        Canvas.SetTop(labelStack, ocy + r + 14);
        canvas.Children.Add(labelStack);

        Canvas.SetLeft(canvas, cx - ocx);
        Canvas.SetTop(canvas, cy - ocy);
        return canvas;
    }
}
