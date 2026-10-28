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
/// Diagnostics workbench presentation for the Yozakura (Blossom) theme:
/// "The two sticks are two glowing celestial moons with crater textures and celestial coordinate rings;
/// the triggers are tall cylindrical paper lanterns that fill with fire as they are pulled."
/// Strictly implements t_blossom.py:diagnostics().
/// </summary>
public partial class StudioScene
{
    private void RenderBlossomDiagnostics(Canvas host)
    {
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

        // 3. Top Mode & Filter Chips at (234, 200, width 1310)
        var chipStack = new StackPanel { Orientation = Orientation.Horizontal };
        var chips = new[]
        {
            ("Player input · XInput 1", true),
            ("Raw sensor stream", false),
            ("Deadband filter: Off", false),
            ("Dynamic centering: Active", false),
            ("Buffer depth: 1 frame", false)
        };

        foreach (var (txt, on) in chips)
        {
            var cBorder = new Border
            {
                Height = 36,
                Padding = new Thickness(16, 0, 16, 0),
                CornerRadius = new CornerRadius(18),
                Background = on ? BlossomKit.CreateWarmLanternBrush() : BlossomKit.B("rgba(255,220,232,0.06)"),
                BorderBrush = on ? BlossomKit.B("transparent") : BlossomKit.B("rgba(255,220,232,0.22)"),
                BorderThickness = new Thickness(1.2),
                Margin = new Thickness(0, 0, 10, 0),
                Cursor = Cursors.Hand
            };
            if (on)
            {
                cBorder.Effect = new DropShadowEffect { Color = Color.FromRgb(0xff, 0x96, 0x5a), BlurRadius = 14, ShadowDepth = 0, Opacity = 0.5 };
            }
            cBorder.Child = new TextBlock
            {
                Text = txt,
                FontFamily = BlossomKit.Body,
                FontSize = 14,
                Foreground = on ? BlossomKit.B("#4a140f") : BlossomKit.B("#e3cfd6"),
                VerticalAlignment = VerticalAlignment.Center
            };
            chipStack.Children.Add(cBorder);
        }
        Canvas.SetLeft(chipStack, 234);
        Canvas.SetTop(chipStack, 180);
        host.Children.Add(chipStack);

        // 4. Two Celestial Moon Stick Plots at cx = 470 and cx = 860, cy = 430, r = 140
        host.Children.Add(CreateMoonPlot(470, 430, 140, "Left stick", "X: 0.00  ·  Y: 0.00"));
        host.Children.Add(CreateMoonPlot(860, 430, 140, "Right stick", "X: 0.00  ·  Y: 0.00"));

        // 5. Tall Cylindrical Paper Lantern Triggers at x = 1190 and x = 1370
        var triggers = new[] { ("LT", 1190), ("RT", 1370) };
        foreach (var (tName, tx) in triggers)
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
            trigLabelStack.Children.Add(new TextBlock
            {
                Text = "0% · 0 / 255",
                FontFamily = BlossomKit.Body,
                FontSize = 13,
                Foreground = BlossomKit.TextMuteBrush,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 2, 0, 0)
            });
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
            Text = "POLLING RATE",
            FontFamily = BlossomKit.Small,
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Foreground = BlossomKit.TextSakuraBrush
        });
        rStack.Children.Add(new TextBlock
        {
            Text = "1000 HZ",
            FontFamily = BlossomKit.Display,
            FontSize = 28,
            Foreground = BlossomKit.TextAmberBrush,
            Margin = new Thickness(0, 2, 0, 0)
        });
        rateCard.Child = rStack;
        telemStack.Children.Add(rateCard);

        var statusMsg = new TextBlock
        {
            Text = "Input loop running at 1.00ms interval · Zero packet loss · Hall sensors calibrated",
            FontFamily = BlossomKit.Body,
            FontSize = 16,
            Foreground = BlossomKit.TextMuteBrush,
            Margin = new Thickness(24, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        telemStack.Children.Add(statusMsg);

        Canvas.SetLeft(telemStack, 234);
        Canvas.SetTop(telemStack, 690);
        host.Children.Add(telemStack);

        // Hints
        var hintsStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        hintsStack.Children.Add(CreateHint("A", "Test triggers"));
        hintsStack.Children.Add(CreateHint("X", "Center sticks"));
        hintsStack.Children.Add(CreateHint("B", "Back"));

        Canvas.SetRight(hintsStack, 44);
        Canvas.SetTop(hintsStack, 846);
        host.Children.Add(hintsStack);
    }

    /// <summary>
    /// Builds a stick plot rendered as a luminous celestial moon with craters and celestial coordinate rings.
    /// Strictly implements t_blossom.py:moonplot().
    /// </summary>
    private FrameworkElement CreateMoonPlot(double cx, double cy, double r, string label, string sub)
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
                    new GradientStop(Color.FromRgb(0xff, 0xf8, 0xf2), 0.0),
                    new GradientStop(Color.FromRgb(0xf1, 0xdc, 0xde), 0.7),
                    new GradientStop(Color.FromRgb(0xd9, 0xbc, 0xc4), 1.0)
                }
            }
        };
        Canvas.SetLeft(moon, ocx - r);
        Canvas.SetTop(moon, ocy - r);
        canvas.Children.Add(moon);

        // Moon craters
        var craters = new (double DX, double DY, double CR)[]
        {
            (-50, -40, 22), (40, 30, 16), (-20, 60, 12), (60, -50, 10)
        };
        foreach (var c in craters)
        {
            var crater = new Ellipse
            {
                Width = c.CR * 2,
                Height = c.CR * 2,
                Fill = BlossomKit.B("rgba(160,120,135,0.16)")
            };
            Canvas.SetLeft(crater, ocx + c.DX - c.CR);
            Canvas.SetTop(crater, ocy + c.DY - c.CR);
            canvas.Children.Add(crater);
        }

        // Celestial concentric coordinate rings (33%, 66%, 100%)
        foreach (double f in new[] { 0.33, 0.66, 1.0 })
        {
            var ring = new Ellipse
            {
                Width = r * 2 * f,
                Height = r * 2 * f,
                Stroke = BlossomKit.B(f == 1.0 ? "rgba(70,30,50,0.32)" : "rgba(70,30,50,0.18)"),
                StrokeThickness = 1.4
            };
            Canvas.SetLeft(ring, ocx - r * f);
            Canvas.SetTop(ring, ocy - r * f);
            canvas.Children.Add(ring);
        }

        // Celestial crosshair lines
        canvas.Children.Add(new Line { X1 = ocx, Y1 = ocy - r + 8, X2 = ocx, Y2 = ocy + r - 8, Stroke = BlossomKit.B("rgba(70,30,50,0.22)"), StrokeThickness = 1 });
        canvas.Children.Add(new Line { X1 = ocx - r + 8, Y1 = ocy, X2 = ocx + r - 8, Y2 = ocy, Stroke = BlossomKit.B("rgba(70,30,50,0.22)"), StrokeThickness = 1 });

        // Center stick resting point (cherry seed #6a2a44)
        var stickPoint = new Ellipse
        {
            Width = 18,
            Height = 18,
            Fill = BlossomKit.B("#6a2a44")
        };
        Canvas.SetLeft(stickPoint, ocx - 9);
        Canvas.SetTop(stickPoint, ocy - 9);
        canvas.Children.Add(stickPoint);

        // Labels below
        var labelStack = new StackPanel { Width = 280, HorizontalAlignment = HorizontalAlignment.Center };
        labelStack.Children.Add(new TextBlock
        {
            Text = label,
            FontFamily = BlossomKit.Heading,
            FontSize = 22,
            Foreground = BlossomKit.TextWhiteBrush,
            TextAlignment = TextAlignment.Center
        });
        labelStack.Children.Add(new TextBlock
        {
            Text = sub,
            FontFamily = BlossomKit.Body,
            FontSize = 14,
            Foreground = BlossomKit.TextMuteBrush,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 2, 0, 0)
        });
        Canvas.SetLeft(labelStack, ocx - 140);
        Canvas.SetTop(labelStack, ocy + r + 18);
        canvas.Children.Add(labelStack);

        Canvas.SetLeft(canvas, cx - canvas.Width / 2.0);
        Canvas.SetTop(canvas, cy - ocy);
        return canvas;
    }
}
