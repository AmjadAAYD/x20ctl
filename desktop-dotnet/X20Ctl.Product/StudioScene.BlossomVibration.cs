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
/// Vibration workbench presentation for the Yozakura (Blossom) theme:
/// "The pad sits beside a still pond; the vibration is the lit edge of its grips
/// and the concentric moonlight rings it sends rippling over the water."
/// Strictly implements t_blossom.py:vibration().
/// </summary>
public partial class StudioScene
{
    private void RenderBlossomVibration(Canvas host)
    {
        // 1. Sky & Corner Canopy
        var sky = new Rectangle { Width = 1600, Height = 900, Fill = BlossomKit.CreateSkyBackdrop() };
        host.Children.Add(sky);

        // Corner weeping branch canopy
        host.Children.Add(BlossomKit.CanopyBranch("corner", 6));

        // Drifting petals
        host.Children.Add(BlossomKit.DriftingPetals(new Rect(250, 160, 650, 600), 12, 6));

        // Left Navigation Rail
        RenderBlossomLeftNav(host, "vibration");

        // 2. Title & Lead
        var titleTb = new TextBlock
        {
            Text = "Vibration",
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
        var vibTag = new Border
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
                Text = "DUAL-MOTOR HAPTICS",
                FontFamily = BlossomKit.Small,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = BlossomKit.B("#4a140f")
            }
        };
        leadStack.Children.Add(vibTag);
        leadStack.Children.Add(new TextBlock
        {
            Text = "Asymmetric grip rumble with impulse trigger motors.",
            FontFamily = BlossomKit.Body,
            FontSize = 16,
            Foreground = BlossomKit.TextMuteBrush,
            Margin = new Thickness(14, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        Canvas.SetLeft(leadStack, 234);
        Canvas.SetTop(leadStack, 106);
        host.Children.Add(leadStack);

        // 3. Left Side: Still Night Pond & Pad with Concentric Ripples at (cx=560, cy=430, w=500)
        double cx = 560, cy = 430, w = 500, h = 330;
        double wy = cy + h / 2.0 - 20;

        // Dark still water pool
        var pond = new Ellipse
        {
            Width = 840,
            Height = 300,
            Fill = new RadialGradientBrush
            {
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb(204, 8, 3, 10), 0.0),
                    new GradientStop(Color.FromArgb(140, 12, 5, 14), 0.6),
                    new GradientStop(Colors.Transparent, 1.0)
                }
            }
        };
        Canvas.SetLeft(pond, cx - 420);
        Canvas.SetTop(pond, wy - 40);
        host.Children.Add(pond);

        // Concentric moonlight water ripples from dual motors
        var rippleCanvas = new Canvas { Width = 1600, Height = 900, IsHitTestVisible = false };
        foreach (double gx in new[] { cx - 175, cx + 175 })
        {
            for (int i = 1; i <= 4; i++)
            {
                var rip = new Ellipse
                {
                    Width = (40 + i * 34) * 2,
                    Height = (10 + i * 10) * 2,
                    Stroke = BlossomKit.B($"rgba(255,159,184,{0.55 - i * 0.11:F2})"),
                    StrokeThickness = 2.2 - i * 0.3
                };
                Canvas.SetLeft(rip, gx - (40 + i * 34));
                Canvas.SetTop(rip, wy + 40 - (10 + i * 10));
                rippleCanvas.Children.Add(rip);
            }
        }
        host.Children.Add(rippleCanvas);

        // Controller Art resting by the water with pink motor aura
        var artGlow = new Ellipse
        {
            Width = 520,
            Height = 110,
            Fill = new RadialGradientBrush
            {
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb(100, 255, 159, 184), 0.0),
                    new GradientStop(Colors.Transparent, 1.0)
                }
            },
            Effect = new BlurEffect { Radius = 14 }
        };
        Canvas.SetLeft(artGlow, cx - 260);
        Canvas.SetTop(artGlow, cy + 40);
        host.Children.Add(artGlow);

        var padArt = BlossomKit.ControllerArt(Model.Id, w);
        if (padArt != null)
        {
            padArt.Effect = new DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 26,
                ShadowDepth = 20,
                Opacity = 0.65
            };
            Canvas.SetLeft(padArt, cx - w / 2.0);
            Canvas.SetTop(padArt, cy - h / 2.0);
            host.Children.Add(padArt);
        }

        // Footnote under pad
        var padNoteTb = new TextBlock
        {
            Text = "Both motors respond together; trigger rumble fires on brake and throttle thresholds.",
            FontFamily = BlossomKit.Body,
            FontSize = 14,
            Foreground = BlossomKit.TextMuteBrush,
            TextAlignment = TextAlignment.Center,
            Width = 640
        };
        Canvas.SetLeft(padNoteTb, 250);
        Canvas.SetTop(padNoteTb, 770);
        host.Children.Add(padNoteTb);

        // 4. Right Side: Telemetry & Sliders at (960, 200, width 580)
        var rightStack = new StackPanel { Width = 580 };

        var secTb = new TextBlock
        {
            Text = "OVERALL INTENSITY",
            FontFamily = BlossomKit.Small,
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Foreground = BlossomKit.TextSakuraBrush
        };
        rightStack.Children.Add(secTb);

        var bigValTb = new TextBlock
        {
            Text = "70%",
            FontFamily = BlossomKit.Display,
            FontSize = 132,
            Foreground = BlossomKit.B("#ffd3a8"),
            LineHeight = 120,
            Margin = new Thickness(0, 6, 0, 0),
            Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(0xff, 0x96, 0x5a),
                BlurRadius = 30,
                ShadowDepth = 0,
                Opacity = 0.5
            }
        };
        rightStack.Children.Add(bigValTb);

        // Master Lit Silk Cord Slider
        var masterSlider = BlossomKit.LitCordSlider(70, 520, "70%");
        masterSlider.Margin = new Thickness(0, 16, 0, 0);
        rightStack.Children.Add(masterSlider);

        // Preset chips: Gentle · Balanced · Intense
        var chipStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 22, 0, 20) };
        foreach (var c in new[] { "Gentle", "Balanced", "Intense" })
        {
            bool on = c == "Balanced";
            var chipBorder = new Border
            {
                Height = 34,
                Padding = new Thickness(16, 0, 16, 0),
                CornerRadius = new CornerRadius(17),
                Background = on ? BlossomKit.CreateWarmLanternBrush() : BlossomKit.B("transparent"),
                BorderBrush = on ? BlossomKit.B("transparent") : BlossomKit.B("rgba(255,220,232,0.22)"),
                BorderThickness = new Thickness(1.2),
                Margin = new Thickness(0, 0, 10, 0),
                Child = new TextBlock
                {
                    Text = c,
                    FontFamily = BlossomKit.Body,
                    FontSize = 14,
                    Foreground = on ? BlossomKit.B("#4a140f") : BlossomKit.B("#e3cfd6"),
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            if (on)
            {
                chipBorder.Effect = new DropShadowEffect { Color = Color.FromRgb(0xff, 0x96, 0x5a), BlurRadius = 16, ShadowDepth = 0, Opacity = 0.45 };
            }
            chipStack.Children.Add(chipBorder);
        }
        rightStack.Children.Add(chipStack);

        // Per-Motor Slider Rows
        var motors = new (string Name, double Pct, string Val, bool Active)[]
        {
            ("Left grip motor", 70, "70%", true),
            ("Right grip motor", 70, "70%", false),
            ("Left trigger impulse", 50, "50%", false),
            ("Right trigger impulse", 50, "50%", false)
        };

        foreach (var m in motors)
        {
            var rowBorder = new Border
            {
                Height = 56,
                CornerRadius = new CornerRadius(4),
                Background = m.Active
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
                BorderBrush = m.Active ? BlossomKit.B("#ffb56b") : BlossomKit.B("rgba(255,220,230,0.08)"),
                BorderThickness = m.Active ? new Thickness(3, 0, 0, 0) : new Thickness(0, 0, 0, 1),
                Padding = new Thickness(16, 0, 16, 0)
            };

            var grid_ = new Grid();
            grid_.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid_.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var labelTb = new TextBlock
            {
                Text = m.Name,
                FontFamily = BlossomKit.Body,
                FontSize = 18,
                Foreground = m.Active ? BlossomKit.B("#ffe6cc") : BlossomKit.TextWhiteBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(labelTb, 0);
            grid_.Children.Add(labelTb);

            var sliderElem = BlossomKit.LitCordSlider(m.Pct, 200, m.Val);
            Grid.SetColumn(sliderElem, 1);
            grid_.Children.Add(sliderElem);

            rowBorder.Child = grid_;
            rightStack.Children.Add(rowBorder);
        }

        Canvas.SetLeft(rightStack, 960);
        Canvas.SetTop(rightStack, 200);
        host.Children.Add(rightStack);

        // Hints: [A] Test vibration  [X] Mute all  [B] Back
        var hintsStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        hintsStack.Children.Add(CreateHint("A", "Test vibration"));
        hintsStack.Children.Add(CreateHint("X", "Mute all"));
        hintsStack.Children.Add(CreateHint("B", "Back"));

        Canvas.SetRight(hintsStack, 44);
        Canvas.SetTop(hintsStack, 846);
        host.Children.Add(hintsStack);
    }
}
