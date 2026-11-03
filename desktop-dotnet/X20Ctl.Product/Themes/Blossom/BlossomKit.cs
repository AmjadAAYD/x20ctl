using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using Path = System.Windows.Shapes.Path;

namespace X20Ctl.Product.Blossom;

/// <summary>
/// Design system primitives, colors, typography, and procedural visual builders for the Yozakura (Blossom) theme:
/// "Cherry blossoms at night, under paper lanterns. Hanami at night is a gathering in the dark, lit only by lanterns,
/// under blossom that will be gone in a week. So X20CTL is an evening under the trees:
///   - the Studio pages are lanterns on a string, and the page you are on is the lit one;
///   - focus is lantern light: a warm glow falls on the focused row, and the focused details are printed on lit paper;
///   - a value is a lit cord with a small lantern for its knob;
///   - the Zone hangs one lantern per player, lit when someone is there; Tools is a row of festival stalls;
///   - macro events float down a river as lanterns, PC mode's route is a path of stone lanterns, the sticks are two
///     moons, Mouse is a wall of named lanterns, Setups are festival fans, and feedback is an ema plaque."
/// Strictly implements t_blossom.py and its 23 concept boards.
/// </summary>
public static class BlossomKit
{
    // Palette Tokens
    public static readonly Color VoidBg = Color.FromRgb(0x16, 0x0a, 0x16);          // #160a16
    public static readonly Color SkyTop = Color.FromRgb(0x2a, 0x18, 0x30);          // #2a1830
    public static readonly Color SkyMid1 = Color.FromRgb(0x1a, 0x0d, 0x1d);         // #1a0d1d
    public static readonly Color SkyMid2 = Color.FromRgb(0x16, 0x0a, 0x16);         // #160a16
    public static readonly Color SkyBot = Color.FromRgb(0x26, 0x11, 0x1f);          // #26111f

    public static readonly Color WarmFire0 = Color.FromRgb(0xff, 0xf4, 0xd8);       // #fff4d8
    public static readonly Color WarmFire1 = Color.FromRgb(0xff, 0xc2, 0x7a);       // #ffc27a
    public static readonly Color WarmFire2 = Color.FromRgb(0xf2, 0x78, 0x4f);       // #f2784f
    public static readonly Color WarmFire3 = Color.FromRgb(0xbb, 0x3f, 0x37);       // #bb3f37

    public static readonly Color ColdPlum0 = Color.FromRgb(0x83, 0x40, 0x5a);       // #83405a
    public static readonly Color ColdPlum1 = Color.FromRgb(0x56, 0x25, 0x40);       // #562540
    public static readonly Color ColdPlum2 = Color.FromRgb(0x3a, 0x1a, 0x2c);       // #3a1a2c

    public static readonly Color LitPaperTop = Color.FromRgb(0xff, 0xf6, 0xe4);     // #fff6e4
    public static readonly Color LitPaperMid = Color.FromRgb(0xff, 0xe2, 0xb8);     // #ffe2b8
    public static readonly Color LitPaperBot = Color.FromRgb(0xf7, 0xc3, 0x89);     // #f7c389
    public static readonly Color LitPaperInk = Color.FromRgb(0x3b, 0x17, 0x12);     // #3b1712
    public static readonly Color LitPaperSub = Color.FromRgb(0x5b, 0x2a, 0x22);     // #5b2a22
    public static readonly Color LitPaperCrimson = Color.FromRgb(0xb4, 0x44, 0x2f); // #b4442f

    public static readonly Color PetalLight = Color.FromRgb(0xff, 0xf0, 0xf4);      // #fff0f4
    public static readonly Color PetalMid = Color.FromRgb(0xff, 0xb0, 0xc4);        // #ffb0c4
    public static readonly Color PetalHot = Color.FromRgb(0xff, 0x5d, 0x8a);        // #ff5d8a
    public static readonly Color PetalShade = Color.FromRgb(0xb5, 0x6a, 0x86);      // #b56a86
    public static readonly Color PetalDark = Color.FromRgb(0x6e, 0x24, 0x41);       // #6e2441

    public static readonly Color WoodDark = Color.FromRgb(0x1c, 0x10, 0x0b);        // #1c100b
    public static readonly Color WoodTimber = Color.FromRgb(0x2e, 0x1b, 0x12);      // #2e1b12
    public static readonly Color WoodMid = Color.FromRgb(0x4a, 0x2e, 0x20);         // #4a2e20
    public static readonly Color WoodRoof = Color.FromRgb(0x6a, 0x44, 0x30);        // #6a4430
    public static readonly Color WoodCounter = Color.FromRgb(0x3d, 0x26, 0x19);     // #3d2619
    public static readonly Color WoodEmaTop = Color.FromRgb(0xec, 0xd0, 0xa4);      // #ecd0a4
    public static readonly Color WoodEmaBot = Color.FromRgb(0xd1, 0xaa, 0x76);      // #d1aa76

    public static readonly Color StoneTop = Color.FromRgb(0x5c, 0x48, 0x54);        // #5c4854
    public static readonly Color StoneBot = Color.FromRgb(0x2a, 0x1d, 0x27);        // #2a1d27

    public static readonly Color TextWhite = Color.FromRgb(0xf6, 0xe9, 0xec);       // #f6e9ec
    public static readonly Color TextSoft = Color.FromRgb(0xd7, 0xc1, 0xc8);        // #d7c1c8
    public static readonly Color TextMute = Color.FromRgb(0xa8, 0x90, 0x9b);        // #a8909b
    public static readonly Color TextAmber = Color.FromRgb(0xff, 0xd3, 0xa8);       // #ffd3a8
    public static readonly Color TextSakura = Color.FromRgb(0xf4, 0xa6, 0xbd);      // #f4a6bd

    public static readonly Color SamuraiVermilion = Color.FromRgb(0xd8, 0x47, 0x3f);// #d8473f

    // Typography
    public static readonly FontFamily Display = new("Sitka Banner, Georgia, Segoe UI");
    public static readonly FontFamily Heading = new("Sitka Heading, Georgia, Segoe UI");
    public static readonly FontFamily Body = new("Sitka Text, Georgia, Segoe UI");
    public static readonly FontFamily Small = new("Sitka Small, Georgia, Segoe UI");
    public static readonly FontFamily Icons = new("Segoe Fluent Icons, Segoe MDL2 Assets");

    // Brushes
    public static readonly SolidColorBrush VoidBgBrush = B(VoidBg);
    public static readonly SolidColorBrush TextWhiteBrush = B(TextWhite);
    public static readonly SolidColorBrush TextSoftBrush = B(TextSoft);
    public static readonly SolidColorBrush TextMuteBrush = B(TextMute);
    public static readonly SolidColorBrush TextAmberBrush = B(TextAmber);
    public static readonly SolidColorBrush TextSakuraBrush = B(TextSakura);
    public static readonly SolidColorBrush SamuraiVermilionBrush = B(SamuraiVermilion);
    public static readonly SolidColorBrush WoodDarkBrush = B(WoodDark);
    public static readonly SolidColorBrush WoodTimberBrush = B(WoodTimber);

    public static SolidColorBrush B(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }
    public static SolidColorBrush B(string hex) => B(C(hex));

    public static Color C(string val)
    {
        val = val.Trim();
        if (val.StartsWith("rgba(", StringComparison.OrdinalIgnoreCase) && val.EndsWith(")"))
        {
            var parts = val.Substring(5, val.Length - 6).Split(',');
            if (parts.Length == 4)
            {
                byte r = byte.Parse(parts[0].Trim());
                byte g = byte.Parse(parts[1].Trim());
                byte b = byte.Parse(parts[2].Trim());
                double a = double.Parse(parts[3].Trim(), System.Globalization.CultureInfo.InvariantCulture);
                return Color.FromArgb((byte)Math.Clamp(a * 255, 0, 255), r, g, b);
            }
        }
        return (Color)ColorConverter.ConvertFromString(val);
    }

    /// <summary>Creates the starry night sky gradient backdrop.</summary>
    public static Brush CreateSkyBackdrop()
    {
        var grad = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1),
            GradientStops = new GradientStopCollection
            {
                new GradientStop(SkyTop, 0.0),
                new GradientStop(SkyMid1, 0.40),
                new GradientStop(SkyMid2, 0.62),
                new GradientStop(SkyBot, 1.0)
            }
        };
        grad.Freeze();
        return grad;
    }

    /// <summary>Creates a warm fire radial gradient for lit lanterns.</summary>
    public static RadialGradientBrush CreateWarmLanternBrush()
    {
        var b = new RadialGradientBrush
        {
            Center = new Point(0.5, 0.45),
            GradientOrigin = new Point(0.5, 0.45),
            RadiusX = 0.6,
            RadiusY = 0.55,
            GradientStops = new GradientStopCollection
            {
                new GradientStop(WarmFire0, 0.0),
                new GradientStop(WarmFire1, 0.40),
                new GradientStop(WarmFire2, 0.78),
                new GradientStop(WarmFire3, 1.0)
            }
        };
        b.Freeze();
        return b;
    }

    /// <summary>Creates an unlit plum radial gradient for cold/dormant lanterns.</summary>
    public static RadialGradientBrush CreateColdLanternBrush()
    {
        var b = new RadialGradientBrush
        {
            Center = new Point(0.5, 0.45),
            GradientOrigin = new Point(0.5, 0.45),
            RadiusX = 0.6,
            RadiusY = 0.55,
            GradientStops = new GradientStopCollection
            {
                new GradientStop(ColdPlum0, 0.0),
                new GradientStop(ColdPlum1, 0.60),
                new GradientStop(ColdPlum2, 1.0)
            }
        };
        b.Freeze();
        return b;
    }

    /// <summary>Creates the illuminated washi paper radial gradient.</summary>
    public static RadialGradientBrush CreateLitPaperBrush()
    {
        var b = new RadialGradientBrush
        {
            Center = new Point(0.5, 0.30),
            GradientOrigin = new Point(0.5, 0.30),
            RadiusX = 0.9,
            RadiusY = 0.8,
            GradientStops = new GradientStopCollection
            {
                new GradientStop(LitPaperTop, 0.0),
                new GradientStop(LitPaperMid, 0.55),
                new GradientStop(LitPaperBot, 1.0)
            }
        };
        b.Freeze();
        return b;
    }

    /// <summary>
    /// Loads controller art image.
    /// </summary>
    public static Image? ControllerArt(string modelId = "x20", double width = 600)
    {
        return ThemeArt.Load(modelId, width);
    }

    /// <summary>
    /// Loads the Blossom theme logo.
    /// </summary>
    public static Image? BlossomLogo(double width = 116)
    {
        string path = System.IO.Path.Combine(AppContext.BaseDirectory, "Brand", "themes", "blossom", "logo.png");
        if (!System.IO.File.Exists(path))
        {
            path = System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Brand", "themes", "blossom", "logo.png");
        }
        if (!System.IO.File.Exists(path)) return null;

        var img = new Image
        {
            Width = width,
            Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(System.IO.Path.GetFullPath(path))),
            Stretch = Stretch.Uniform
        };
        return img;
    }

    /// <summary>
    /// Builds a traditional paper lantern:
    /// lit warm with warm fire gradient and golden aura, or unlit plum;
    /// topped with a black wooden cap and suspension wire, and hung with silk cords.
    /// </summary>
    public static FrameworkElement PaperLantern(
        double w,
        double h,
        bool lit,
        string? icon = null,
        string? text = null,
        double glow = 1.0,
        double cord = 24,
        double? fs = null)
    {
        var container = new Canvas
        {
            Width = w + (lit ? 40 : 10),
            Height = h + cord + h * 0.2 + 20,
            IsHitTestVisible = false
        };

        double cx = container.Width / 2.0;
        double top = cord + 6;

        // Top suspension wire
        if (cord > 0)
        {
            var wire = new Line
            {
                X1 = cx,
                Y1 = top - cord - 6,
                X2 = cx,
                Y2 = top - 6,
                Stroke = B("#3a2430"),
                StrokeThickness = 2
            };
            container.Children.Add(wire);
        }

        // Top wooden cap
        var topCap = new Rectangle
        {
            Width = w * 0.6,
            Height = 8,
            RadiusX = 3,
            RadiusY = 3,
            Fill = B("#160b10")
        };
        Canvas.SetLeft(topCap, cx - w * 0.3);
        Canvas.SetTop(topCap, top - 6);
        container.Children.Add(topCap);

        // Radiant halo glow if lit
        if (lit)
        {
            var halo = new Ellipse
            {
                Width = w * 1.5,
                Height = h * 1.3,
                Fill = new RadialGradientBrush
                {
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(Color.FromArgb((byte)(110 * glow), 255, 160, 90), 0.0),
                        new GradientStop(Color.FromArgb((byte)(30 * glow), 255, 110, 90), 0.65),
                        new GradientStop(Colors.Transparent, 1.0)
                    }
                },
                IsHitTestVisible = false
            };
            Canvas.SetLeft(halo, cx - halo.Width / 2.0);
            Canvas.SetTop(halo, top + h / 2.0 - halo.Height / 2.0);
            container.Children.Add(halo);
        }

        // Main Paper Body
        var body = new Border
        {
            Width = w,
            Height = h,
            CornerRadius = new CornerRadius(w * 0.46, w * 0.46, w * 0.46, w * 0.46),
            Background = lit ? CreateWarmLanternBrush() : CreateColdLanternBrush(),
            BorderBrush = lit ? B("rgba(255,220,180,0.6)") : B("rgba(255,200,215,0.16)"),
            BorderThickness = new Thickness(1)
        };

        if (lit)
        {
            body.Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(0xff, 0xa0, 0x5a),
                BlurRadius = 24 * glow,
                ShadowDepth = 0,
                Opacity = 0.85 * glow
            };
        }

        // Internal ribs overlay
        var ribGrid = new Grid();
        for (int r = 1; r <= 5; r++)
        {
            var rib = new Line
            {
                X1 = 4,
                X2 = w - 4,
                Y1 = h * (r / 6.0),
                Y2 = h * (r / 6.0),
                Stroke = B(lit ? "rgba(70,20,20,0.25)" : "rgba(40,15,30,0.3)"),
                StrokeThickness = 1.2
            };
            ribGrid.Children.Add(rib);
        }

        // Lantern inscription (Icon or Calligraphic Text)
        Color col = lit ? Color.FromRgb(0x4a, 0x14, 0x0f) : Color.FromArgb(0xd0, 0xf6, 0xe9, 0xec);
        if (!string.IsNullOrEmpty(icon))
        {
            var tb = new TextBlock
            {
                Text = icon,
                FontFamily = Icons,
                FontSize = w * 0.36,
                Foreground = B(col),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.SemiBold
            };
            ribGrid.Children.Add(tb);
        }
        else if (!string.IsNullOrEmpty(text))
        {
            var tb = new TextBlock
            {
                Text = text,
                FontFamily = Display,
                FontSize = fs ?? (w * 0.42),
                Foreground = B(col),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.Bold
            };
            ribGrid.Children.Add(tb);
        }

        body.Child = ribGrid;
        Canvas.SetLeft(body, cx - w / 2.0);
        Canvas.SetTop(body, top);
        container.Children.Add(body);

        // Bottom wooden cap
        var botCap = new Rectangle
        {
            Width = w * 0.6,
            Height = 8,
            RadiusX = 3,
            RadiusY = 3,
            Fill = B("#160b10")
        };
        Canvas.SetLeft(botCap, cx - w * 0.3);
        Canvas.SetTop(botCap, top + h - 2);
        container.Children.Add(botCap);

        // Bottom hanging tassel / cord
        var tassel = new Line
        {
            X1 = cx,
            Y1 = top + h + 6,
            X2 = cx,
            Y2 = top + h + 6 + h * 0.22,
            Stroke = lit ? SamuraiVermilionBrush : B("#4a2030"),
            StrokeThickness = 2.4
        };
        container.Children.Add(tassel);

        return container;
    }

    /// <summary>
    /// Builds a five-petal cherry blossom (bloom) geometry.
    /// </summary>
    public static FrameworkElement BlossomFlower(double radius, double rotation = 0, Color? petalColor = null, double opacity = 1.0)
    {
        var canvas = new Canvas
        {
            Width = radius * 2.2,
            Height = radius * 2.2,
            Opacity = opacity,
            IsHitTestVisible = false
        };

        var group = new TransformGroup();
        group.Children.Add(new RotateTransform(rotation, radius * 1.1, radius * 1.1));
        canvas.RenderTransform = group;

        double cx = radius * 1.1;
        double cy = radius * 1.1;

        Color fillBase = petalColor ?? PetalHot;
        var petalBrush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 1),
            EndPoint = new Point(0, 0),
            GradientStops = new GradientStopCollection
            {
                new GradientStop(fillBase, 0.0),
                new GradientStop(PetalMid, 0.45),
                new GradientStop(PetalLight, 1.0)
            }
        };
        petalBrush.Freeze();

        // 5 petals rotated by 72 deg
        for (int i = 0; i < 5; i++)
        {
            double angle = i * 72.0;
            var petal = new Path
            {
                Data = Geometry.Parse(PetalSvgPath(radius)),
                Fill = petalBrush,
                Stroke = B("rgba(255,200,215,0.45)"),
                StrokeThickness = 0.6,
                RenderTransform = new TransformGroup
                {
                    Children = new TransformCollection
                    {
                        new RotateTransform(angle, 0, 0),
                        new TranslateTransform(cx, cy)
                    }
                }
            };
            canvas.Children.Add(petal);
        }

        // Central stamen & pistols
        if (radius >= 11)
        {
            for (int i = 0; i < 8; i++)
            {
                double a = i * 0.785 + 0.2;
                var stamen = new Line
                {
                    X1 = cx,
                    Y1 = cy,
                    X2 = cx + 0.42 * radius * Math.Cos(a),
                    Y2 = cy + 0.42 * radius * Math.Sin(a),
                    Stroke = B("#ffe1e8"),
                    StrokeThickness = 0.8,
                    Opacity = 0.85
                };
                canvas.Children.Add(stamen);

                var tip = new Ellipse
                {
                    Width = Math.Max(1.8, radius * 0.1),
                    Height = Math.Max(1.8, radius * 0.1),
                    Fill = B("#ffe1e8")
                };
                Canvas.SetLeft(tip, cx + 0.46 * radius * Math.Cos(a) - tip.Width / 2.0);
                Canvas.SetTop(tip, cy + 0.46 * radius * Math.Sin(a) - tip.Height / 2.0);
                canvas.Children.Add(tip);
            }
        }

        // Center seed
        var centerDot = new Ellipse
        {
            Width = radius * 0.3,
            Height = radius * 0.3,
            Fill = B("#ff4f7e")
        };
        Canvas.SetLeft(centerDot, cx - centerDot.Width / 2.0);
        Canvas.SetTop(centerDot, cy - centerDot.Height / 2.0);
        canvas.Children.Add(centerDot);

        return canvas;
    }

    /// <summary>
    /// Petal notch path string matching t_blossom.py petal_d(L).
    /// </summary>
    private static string PetalSvgPath(double L)
    {
        return $"M 0 0 C {-0.62 * L:F1} {-0.24 * L:F1} {-0.6 * L:F1} {-0.86 * L:F1} {-0.17 * L:F1} {-L:F1} " +
               $"L 0 {-0.85 * L:F1} L {0.17 * L:F1} {-L:F1} " +
               $"C {0.6 * L:F1} {-0.86 * L:F1} {0.62 * L:F1} {-0.24 * L:F1} 0 0 Z";
    }

    /// <summary>
    /// Generates weeping cherry branches canopy with blooming clusters.
    /// </summary>
    public static FrameworkElement CanopyBranch(string kind = "corner", int seed = 21)
    {
        var canvas = new Canvas
        {
            Width = 1600,
            Height = 900,
            IsHitTestVisible = false
        };

        var rnd = new Random(seed);

        // Branch curve points
        Point[] main;
        Point[][] twigs;
        double trunkW;

        if (kind == "top")
        {
            main = [new(1660, -10), new(1520, 40), new(1380, 24), new(1240, 64), new(1110, 44), new(1000, 70)];
            twigs = [[new(1380, 24), new(1350, 96)], [new(1110, 44), new(1090, 110)]];
            trunkW = 18;
        }
        else if (kind == "side")
        {
            main = [new(1660, 250), new(1560, 290), new(1460, 270), new(1380, 310), new(1320, 296)];
            twigs = [[new(1460, 270), new(1440, 210)]];
            trunkW = 16;
        }
        else if (kind == "home")
        {
            main = [new(1650, 6), new(1480, 80), new(1320, 56), new(1160, 116), new(1000, 94), new(850, 138), new(700, 120), new(600, 150)];
            twigs = [[new(1320, 56), new(1270, 150), new(1296, 226)], [new(1480, 80), new(1510, 170)], [new(1000, 94), new(960, 30)], [new(850, 138), new(820, 214)]];
            trunkW = 28;
        }
        else // "corner"
        {
            main = [new(1650, 30), new(1520, 90), new(1400, 70), new(1290, 110), new(1230, 100)];
            twigs = [[new(1400, 70), new(1380, 10)]];
            trunkW = 20;
        }

        // Draw upper moonlight rim on branches
        var rimPath = new Path
        {
            Data = CreateSmoothGeometry(main),
            Stroke = B("rgba(255,225,235,0.25)"),
            StrokeThickness = trunkW * 0.7,
            RenderTransform = new TranslateTransform(0, -1.6)
        };
        canvas.Children.Add(rimPath);

        // Draw dark ink wood branches
        var branchPath = new Path
        {
            Data = CreateSmoothGeometry(main),
            Stroke = B("#08040a"),
            StrokeThickness = trunkW * 0.65,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        };
        canvas.Children.Add(branchPath);

        // Draw twigs
        foreach (var twig in twigs)
        {
            var twigPath = new Path
            {
                Data = CreateSmoothGeometry(twig),
                Stroke = B("#08040a"),
                StrokeThickness = Math.Max(3, trunkW * 0.3),
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            canvas.Children.Add(twigPath);
        }

        // Cluster blossoms along the branches
        int bloomCount = kind == "home" ? 38 : 22;
        for (int i = 0; i < bloomCount; i++)
        {
            double t = 0.05 + 0.95 * rnd.NextDouble();
            Point pt = EvalAlong(main, t);
            double ox = (rnd.NextDouble() - 0.5) * 60;
            double oy = (rnd.NextDouble() - 0.5) * 50;
            double r = 8 + 14 * rnd.NextDouble();
            double rot = rnd.NextDouble() * 72;

            var flower = BlossomFlower(r, rot, PetalHot, 0.9);
            flower.Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(0xff, 0x8c, 0xaa),
                BlurRadius = 8,
                ShadowDepth = 0,
                Opacity = 0.5
            };
            Canvas.SetLeft(flower, pt.X + ox - r);
            Canvas.SetTop(flower, pt.Y + oy - r);
            canvas.Children.Add(flower);
        }

        return canvas;
    }

    private static PathGeometry CreateSmoothGeometry(Point[] pts)
    {
        var geom = new PathGeometry();
        if (pts.Length < 2) return geom;

        var fig = new PathFigure { StartPoint = pts[0] };
        for (int i = 0; i < pts.Length - 1; i++)
        {
            Point p0 = i > 0 ? pts[i - 1] : pts[i];
            Point p1 = pts[i];
            Point p2 = pts[i + 1];
            Point p3 = (i + 2 < pts.Length) ? pts[i + 2] : p2;

            Point c1 = new(p1.X + (p2.X - p0.X) / 6.0, p1.Y + (p2.Y - p0.Y) / 6.0);
            Point c2 = new(p2.X - (p3.X - p1.X) / 6.0, p2.Y - (p3.Y - p1.Y) / 6.0);
            fig.Segments.Add(new BezierSegment(c1, c2, p2, true));
        }
        geom.Figures.Add(fig);
        return geom;
    }

    private static Point EvalAlong(Point[] pts, double t)
    {
        double totalLen = 0;
        double[] lens = new double[pts.Length - 1];
        for (int i = 0; i < pts.Length - 1; i++)
        {
            lens[i] = (pts[i + 1] - pts[i]).Length;
            totalLen += lens[i];
        }

        double d = t * totalLen;
        for (int i = 0; i < pts.Length - 1; i++)
        {
            if (d <= lens[i] || i == pts.Length - 2)
            {
                double frac = lens[i] > 0 ? d / lens[i] : 0;
                return new Point(pts[i].X + (pts[i + 1].X - pts[i].X) * frac, pts[i].Y + (pts[i + 1].Y - pts[i].Y) * frac);
            }
            d -= lens[i];
        }
        return pts[^1];
    }

    /// <summary>
    /// Renders drifting individual petals and soft blurred bokeh circles across an area.
    /// </summary>
    public static FrameworkElement DriftingPetals(Rect area, int count = 16, int seed = 12)
    {
        var canvas = new Canvas
        {
            Width = 1600,
            Height = 900,
            IsHitTestVisible = false
        };

        var rnd = new Random(seed);

        // Bokeh glowing orbs
        for (int i = 0; i < count / 2; i++)
        {
            double bx = area.Left + rnd.NextDouble() * area.Width;
            double by = area.Top + rnd.NextDouble() * area.Height;
            double br = 6 + rnd.NextDouble() * 18;

            var bokeh = new Ellipse
            {
                Width = br * 2,
                Height = br * 2,
                Fill = B($"rgba(255,180,180,{0.04 + rnd.NextDouble() * 0.08:F2})"),
                Effect = new BlurEffect { Radius = 6 }
            };
            var bokehScale = new ScaleTransform(1.0, 1.0);
            bokeh.RenderTransformOrigin = new Point(0.5, 0.5);
            bokeh.RenderTransform = bokehScale;
            var scaleAnim = new System.Windows.Media.Animation.DoubleAnimation(0.85, 1.15, TimeSpan.FromSeconds(3.0 + rnd.NextDouble() * 3.0))
            {
                AutoReverse = true,
                RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever,
                EasingFunction = new System.Windows.Media.Animation.SineEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut }
            };
            bokehScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
            bokehScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);

            Canvas.SetLeft(bokeh, bx - br);
            Canvas.SetTop(bokeh, by - br);
            canvas.Children.Add(bokeh);
        }

        // Drifting loose notched petals
        for (int i = 0; i < count; i++)
        {
            double px = area.Left + rnd.NextDouble() * area.Width;
            double py = area.Top + rnd.NextDouble() * area.Height;
            double L = 6 + rnd.NextDouble() * 6;
            double rot = rnd.NextDouble() * 360;

            var petalPath = new Path
            {
                Data = Geometry.Parse(PetalSvgPath(L)),
                Fill = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 1),
                    EndPoint = new Point(0, 0),
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(PetalHot, 0.0),
                        new GradientStop(PetalMid, 0.5),
                        new GradientStop(PetalLight, 1.0)
                    }
                },
                Opacity = 0.45 + rnd.NextDouble() * 0.45,
                Effect = new DropShadowEffect
                {
                    Color = Color.FromRgb(0xff, 0x90, 0xb0),
                    BlurRadius = 4,
                    ShadowDepth = 0,
                    Opacity = 0.5
                }
            };
            var rotTrans = new RotateTransform(rot, 0, 0);
            var trans = new TranslateTransform(px, py);
            petalPath.RenderTransform = new TransformGroup
            {
                Children = new TransformCollection { rotTrans, trans }
            };
            var swayAnim = new System.Windows.Media.Animation.DoubleAnimation(px - 18, px + 18, TimeSpan.FromSeconds(3.5 + rnd.NextDouble() * 2.0))
            {
                AutoReverse = true,
                RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever,
                EasingFunction = new System.Windows.Media.Animation.SineEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut }
            };
            var driftAnim = new System.Windows.Media.Animation.DoubleAnimation(py - 12, py + 24, TimeSpan.FromSeconds(4.5 + rnd.NextDouble() * 2.5))
            {
                AutoReverse = true,
                RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever,
                EasingFunction = new System.Windows.Media.Animation.SineEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut }
            };
            var flutterAnim = new System.Windows.Media.Animation.DoubleAnimation(rot - 18, rot + 18, TimeSpan.FromSeconds(2.8 + rnd.NextDouble() * 1.8))
            {
                AutoReverse = true,
                RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever,
                EasingFunction = new System.Windows.Media.Animation.SineEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut }
            };
            trans.BeginAnimation(TranslateTransform.XProperty, swayAnim);
            trans.BeginAnimation(TranslateTransform.YProperty, driftAnim);
            rotTrans.BeginAnimation(RotateTransform.AngleProperty, flutterAnim);
            canvas.Children.Add(petalPath);
        }

        return canvas;
    }

    /// <summary>
    /// Builds the luminous celestial moon.
    /// </summary>
    public static FrameworkElement MoonSphere(double size = 300)
    {
        var moon = new Ellipse
        {
            Width = size,
            Height = size,
            Fill = new RadialGradientBrush
            {
                Center = new Point(0.42, 0.38),
                GradientOrigin = new Point(0.42, 0.38),
                RadiusX = 0.7,
                RadiusY = 0.7,
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(Color.FromRgb(0xff, 0xf8, 0xf2), 0.0),
                    new GradientStop(Color.FromRgb(0xfb, 0xf2, 0xef), 0.34),
                    new GradientStop(Color.FromArgb(76, 251, 242, 239), 0.38),
                    new GradientStop(Color.FromArgb(18, 251, 242, 239), 0.58),
                    new GradientStop(Colors.Transparent, 0.72)
                }
            },
            IsHitTestVisible = false
        };
        return moon;
    }

    /// <summary>
    /// Builds a carved Japanese granite stone lantern (Tōrō).
    /// Window aperture glows with holy golden fire when lit.
    /// </summary>
    public static FrameworkElement StoneLanternToro(double scale = 1.0, bool lit = false)
    {
        var canvas = new Canvas
        {
            Width = 100 * scale,
            Height = 150 * scale,
            IsHitTestVisible = false
        };

        double cx = 50 * scale;
        double b = 140 * scale;

        var stoneBrush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1),
            GradientStops = new GradientStopCollection
            {
                new GradientStop(StoneTop, 0.0),
                new GradientStop(StoneBot, 1.0)
            }
        };
        stoneBrush.Freeze();

        // Golden aura if lit
        if (lit)
        {
            var aura = new Ellipse
            {
                Width = 90 * scale,
                Height = 90 * scale,
                Fill = new RadialGradientBrush
                {
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(Color.FromArgb(140, 255, 180, 100), 0.0),
                        new GradientStop(Colors.Transparent, 1.0)
                    }
                },
                Effect = new BlurEffect { Radius = 16 * scale }
            };
            Canvas.SetLeft(aura, cx - aura.Width / 2.0);
            Canvas.SetTop(aura, b - 110 * scale);
            canvas.Children.Add(aura);
        }

        // Base
        var rBase = new Rectangle { Width = 72 * scale, Height = 14 * scale, RadiusX = 3 * scale, RadiusY = 3 * scale, Fill = stoneBrush };
        Canvas.SetLeft(rBase, cx - 36 * scale);
        Canvas.SetTop(rBase, b - 14 * scale);
        canvas.Children.Add(rBase);

        // Post
        var post = new Rectangle { Width = 22 * scale, Height = 48 * scale, Fill = stoneBrush };
        Canvas.SetLeft(post, cx - 11 * scale);
        Canvas.SetTop(post, b - 62 * scale);
        canvas.Children.Add(post);

        // Platform
        var platform = new Rectangle { Width = 60 * scale, Height = 12 * scale, RadiusX = 2 * scale, RadiusY = 2 * scale, Fill = stoneBrush };
        Canvas.SetLeft(platform, cx - 30 * scale);
        Canvas.SetTop(platform, b - 74 * scale);
        canvas.Children.Add(platform);

        // Light box outer frame
        var lightBox = new Rectangle { Width = 44 * scale, Height = 30 * scale, Fill = stoneBrush };
        Canvas.SetLeft(lightBox, cx - 22 * scale);
        Canvas.SetTop(lightBox, b - 104 * scale);
        canvas.Children.Add(lightBox);

        // Light box window
        var window = new Rectangle
        {
            Width = 24 * scale,
            Height = 18 * scale,
            RadiusX = 2 * scale,
            RadiusY = 2 * scale,
            Fill = lit ? B("#ffd08a") : B("#0d070c")
        };
        if (lit)
        {
            window.Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(0xff, 0xc0, 0x70),
                BlurRadius = 12 * scale,
                ShadowDepth = 0,
                Opacity = 0.95
            };
        }
        Canvas.SetLeft(window, cx - 12 * scale);
        Canvas.SetTop(window, b - 98 * scale);
        canvas.Children.Add(window);

        // Curved pagoda roof
        string roofSvg = $"M {cx - 46 * scale:F1} {b - 104 * scale:F1} " +
                         $"Q {cx - 30 * scale:F1} {b - 110 * scale:F1} {cx - 16 * scale:F1} {b - 126 * scale:F1} " +
                         $"H {cx + 16 * scale:F1} " +
                         $"Q {cx + 30 * scale:F1} {b - 110 * scale:F1} {cx + 46 * scale:F1} {b - 104 * scale:F1} Z";
        var roof = new Path
        {
            Data = Geometry.Parse(roofSvg),
            Fill = stoneBrush,
            Stroke = B("rgba(255,225,235,0.22)"),
            StrokeThickness = 1
        };
        canvas.Children.Add(roof);

        // Finial pearl top
        var finial = new Ellipse { Width = 12 * scale, Height = 12 * scale, Fill = stoneBrush };
        Canvas.SetLeft(finial, cx - 6 * scale);
        Canvas.SetTop(finial, b - 138 * scale);
        canvas.Children.Add(finial);

        return canvas;
    }

    /// <summary>
    /// Builds a lit silk cord slider with a small glowing paper lantern knob.
    /// </summary>
    public static FrameworkElement LitCordSlider(double pct, double width = 220, string? valText = null)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };

        var trackCanvas = new Canvas
        {
            Width = width,
            Height = 26,
            VerticalAlignment = VerticalAlignment.Center
        };

        double x = Math.Clamp(width * pct / 100.0, 0, width);

        // Unlit track line
        var unlitWire = new Rectangle
        {
            Width = width,
            Height = 2,
            Fill = B("rgba(255,220,232,0.16)")
        };
        Canvas.SetLeft(unlitWire, 0);
        Canvas.SetTop(unlitWire, 12);
        trackCanvas.Children.Add(unlitWire);

        // Lit glowing cord
        if (x > 0)
        {
            var litWire = new Rectangle
            {
                Width = x,
                Height = 4,
                RadiusX = 2,
                RadiusY = 2,
                Fill = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0.5),
                    EndPoint = new Point(1, 0.5),
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(WarmFire2, 0.0),
                        new GradientStop(WarmFire1, 1.0)
                    }
                },
                Effect = new DropShadowEffect
                {
                    Color = Color.FromRgb(0xff, 0x96, 0x5a),
                    BlurRadius = 10,
                    ShadowDepth = 0,
                    Opacity = 0.8
                }
            };
            Canvas.SetLeft(litWire, 0);
            Canvas.SetTop(litWire, 11);
            trackCanvas.Children.Add(litWire);
        }

        // Miniature glowing lantern knob
        var knob = new Border
        {
            Width = 16,
            Height = 22,
            CornerRadius = new CornerRadius(7, 7, 7, 7),
            Background = CreateWarmLanternBrush(),
            BorderBrush = B("rgba(255,240,210,0.8)"),
            BorderThickness = new Thickness(1),
            Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(0xff, 0x96, 0x5a),
                BlurRadius = 12,
                ShadowDepth = 0,
                Opacity = 0.85
            }
        };
        Canvas.SetLeft(knob, Math.Max(0, x - 8));
        Canvas.SetTop(knob, 2);
        trackCanvas.Children.Add(knob);

        panel.Children.Add(trackCanvas);

        if (!string.IsNullOrEmpty(valText))
        {
            var valTb = new TextBlock
            {
                Text = valText,
                FontFamily = Body,
                FontSize = 15,
                Foreground = TextSoftBrush,
                MinWidth = 54,
                TextAlignment = TextAlignment.Right,
                Margin = new Thickness(12, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            panel.Children.Add(valTb);
        }

        return panel;
    }

    /// <summary>
    /// Builds a traditional festival fan (Uchiwa).
    /// Bamboo handle, bamboo perimeter rim, and circular washi face with controller cover.
    /// </summary>
    public static FrameworkElement UchiwaFan(double size, string? modelId = null, bool active = false)
    {
        var container = new Canvas
        {
            Width = size,
            Height = size + size * 0.38,
            IsHitTestVisible = false
        };

        double cx = size / 2.0;
        double cy = size / 2.0;

        // Bamboo handle extending downward
        var handle = new Border
        {
            Width = 16,
            Height = size * 0.42,
            CornerRadius = new CornerRadius(8),
            Background = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0.5),
                EndPoint = new Point(1, 0.5),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(B("#4a2e20").Color, 0.0),
                    new GradientStop(B("#8a6448").Color, 0.5),
                    new GradientStop(B("#4a2e20").Color, 1.0)
                }
            }
        };
        Canvas.SetLeft(handle, cx - 8);
        Canvas.SetTop(handle, cy + size * 0.5 - 6);
        container.Children.Add(handle);

        // Circular fan face
        var face = new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(size / 2.0),
            ClipToBounds = true,
            Background = B("#250f1c"),
            BorderBrush = active ? B("#ffb56b") : B("#8a6a48"),
            BorderThickness = new Thickness(active ? 5 : 4)
        };

        if (active)
        {
            face.Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(0xff, 0xa0, 0x5a),
                BlurRadius = 36,
                ShadowDepth = 0,
                Opacity = 0.75
            };
        }
        else
        {
            face.Effect = new DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 18,
                ShadowDepth = 8,
                Opacity = 0.5
            };
        }

        var faceContent = new Grid();

        // Delicate radial ribs watermark
        var ribCanvas = new Canvas { Width = size, Height = size, Opacity = 0.15 };
        for (int i = 0; i < 18; i++)
        {
            double a = i * 20.0 * Math.PI / 180.0;
            var rib = new Line
            {
                X1 = cx,
                Y1 = size,
                X2 = cx + size * Math.Cos(a),
                Y2 = size - size * Math.Sin(a),
                Stroke = B("#ffffff"),
                StrokeThickness = 1.0
            };
            ribCanvas.Children.Add(rib);
        }
        faceContent.Children.Add(ribCanvas);

        if (!string.IsNullOrEmpty(modelId))
        {
            var art = ControllerArt(modelId, size * 0.82);
            if (art != null)
            {
                art.HorizontalAlignment = HorizontalAlignment.Center;
                art.VerticalAlignment = VerticalAlignment.Center;
                faceContent.Children.Add(art);
            }
        }

        face.Child = faceContent;
        Canvas.SetLeft(face, 0);
        Canvas.SetTop(face, 0);
        container.Children.Add(face);

        return container;
    }

    /// <summary>
    /// Builds a traditional Japanese festival stall (Yatai):
    /// Timber roof with eaves, split noren cloth curtains, counter, and stall lantern.
    /// </summary>
    public static FrameworkElement FestivalStall(
        double w,
        double h,
        string icon,
        string name,
        string sub,
        bool active = false,
        FrameworkElement? extra = null)
    {
        var container = new Canvas
        {
            Width = w,
            Height = h,
            IsHitTestVisible = false
        };

        // Two side wooden posts
        var postL = new Rectangle { Width = 10, Height = h - 20, Fill = B("#2a170f") };
        Canvas.SetLeft(postL, 4);
        Canvas.SetTop(postL, 20);
        container.Children.Add(postL);

        var postR = new Rectangle { Width = 10, Height = h - 20, Fill = B("#2a170f") };
        Canvas.SetLeft(postR, w - 14);
        Canvas.SetTop(postR, 20);
        container.Children.Add(postR);

        // Golden aura if active
        if (active)
        {
            var aura = new Border
            {
                Width = w + 40,
                Height = h + 20,
                Background = new RadialGradientBrush
                {
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(Color.FromArgb(70, 255, 170, 110), 0.0),
                        new GradientStop(Colors.Transparent, 0.8)
                    }
                },
                Effect = new BlurEffect { Radius = 20 }
            };
            Canvas.SetLeft(aura, -20);
            Canvas.SetTop(aura, 20);
            container.Children.Add(aura);
        }

        // Slanted wooden roof eaves
        var roof = new Border
        {
            Width = w + 16,
            Height = 28,
            CornerRadius = new CornerRadius(2),
            Background = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(WoodRoof, 0.0),
                    new GradientStop(WoodTimber, 1.0)
                }
            },
            Effect = new DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 14,
                ShadowDepth = 6,
                Opacity = 0.6
            }
        };
        Canvas.SetLeft(roof, -8);
        Canvas.SetTop(roof, 0);
        container.Children.Add(roof);

        // Split Noren cloth curtain
        double nw = w - 44;
        var noren = new Border
        {
            Width = nw,
            Height = 70,
            Background = B(active ? "#8a2a4a" : "#4a1a32"),
            BorderBrush = B("rgba(255,200,215,0.2)"),
            BorderThickness = new Thickness(0, 0, 0, 1)
        };
        if (active)
        {
            noren.Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(0xff, 0x78, 0x8c),
                BlurRadius = 18,
                ShadowDepth = 0,
                Opacity = 0.4
            };
        }

        var norenStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var icTb = new TextBlock
        {
            Text = icon,
            FontFamily = Icons,
            FontSize = 20,
            Foreground = B(active ? "#ffe6cc" : "#f4a6bd"),
            Margin = new Thickness(0, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        var titleTb = new TextBlock
        {
            Text = name,
            FontFamily = Heading,
            FontSize = 20,
            Foreground = B(active ? "#ffe6cc" : "#f6e9ec"),
            VerticalAlignment = VerticalAlignment.Center
        };
        norenStack.Children.Add(icTb);
        norenStack.Children.Add(titleTb);
        noren.Child = norenStack;

        Canvas.SetLeft(noren, 22);
        Canvas.SetTop(noren, 28);
        container.Children.Add(noren);

        // Side small hanging lantern
        var stallLantern = PaperLantern(28, 38, active, null, null, 0.8, 8);
        Canvas.SetLeft(stallLantern, w - 34);
        Canvas.SetTop(stallLantern, 32);
        container.Children.Add(stallLantern);

        // Wooden counter box
        var counter = new Border
        {
            Width = w - 28,
            Height = h - 108,
            Padding = new Thickness(14, 14, 14, 14),
            Background = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(WoodCounter, 0.0),
                    new GradientStop(WoodDark, 1.0)
                }
            },
            BorderBrush = B(active ? "rgba(255,190,130,0.5)" : "rgba(255,200,150,0.18)"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(2)
        };

        if (active)
        {
            counter.Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(0xff, 0xa0, 0x5a),
                BlurRadius = 20,
                ShadowDepth = 0,
                Opacity = 0.35
            };
        }

        var counterStack = new StackPanel();
        var subTb = new TextBlock
        {
            Text = sub,
            FontFamily = Body,
            FontSize = 14,
            LineHeight = 20,
            TextWrapping = TextWrapping.Wrap,
            Foreground = B(active ? "#ffe6cc" : "#e8d2b8")
        };
        counterStack.Children.Add(subTb);

        if (extra != null)
        {
            extra.Margin = new Thickness(0, 10, 0, 0);
            counterStack.Children.Add(extra);
        }

        counter.Child = counterStack;
        Canvas.SetLeft(counter, 14);
        Canvas.SetTop(counter, 104);
        container.Children.Add(counter);

        return container;
    }

    /// <summary>
    /// Builds an authentic Japanese wooden votive tablet (Ema plaque)
    /// hung from a cherry branch by red silk rope.
    /// </summary>
    public static FrameworkElement EmaPlaque(double w, double h, string message, string count)
    {
        var container = new Canvas
        {
            Width = w,
            Height = h,
            IsHitTestVisible = false
        };

        // Red silk hanging ropes
        var ropeLeft = new Line { X1 = w / 2.0 - 90, Y1 = 0, X2 = w / 2.0, Y2 = 50, Stroke = SamuraiVermilionBrush, StrokeThickness = 2.4 };
        var ropeRight = new Line { X1 = w / 2.0 + 90, Y1 = 0, X2 = w / 2.0, Y2 = 50, Stroke = SamuraiVermilionBrush, StrokeThickness = 2.4 };
        container.Children.Add(ropeLeft);
        container.Children.Add(ropeRight);

        // Pentagonal wooden tablet shape: peak at top center
        double topEave = 50;
        string plaqueSvg = $"M 0 {topEave + 50:F1} L {w / 2.0:F1} {topEave:F1} L {w:F1} {topEave + 50:F1} L {w:F1} {h:F1} L 0 {h:F1} Z";

        var plaque = new Path
        {
            Data = Geometry.Parse(plaqueSvg),
            Fill = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(WoodEmaTop, 0.0),
                    new GradientStop(WoodEmaBot, 1.0)
                }
            },
            Stroke = B("#8c663e"),
            StrokeThickness = 2,
            Effect = new DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 28,
                ShadowDepth = 18,
                Opacity = 0.55
            }
        };
        container.Children.Add(plaque);

        // Suspension hole
        var hole = new Ellipse
        {
            Width = 14,
            Height = 14,
            Fill = B("#3b1712")
        };
        Canvas.SetLeft(hole, w / 2.0 - 7);
        Canvas.SetTop(hole, topEave + 24);
        container.Children.Add(hole);

        // Inscribed message
        var msgBorder = new Border
        {
            Width = w - 70,
            Margin = new Thickness(35, topEave + 60, 35, 0)
        };
        var msgStack = new StackPanel();

        var headerTb = new TextBlock
        {
            Text = "YOUR MESSAGE",
            FontFamily = Small,
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Foreground = B(LitPaperCrimson)
        };
        msgStack.Children.Add(headerTb);

        var textTb = new TextBlock
        {
            Text = message,
            FontFamily = Body,
            FontStyle = FontStyles.Italic,
            FontSize = 23,
            LineHeight = 34,
            TextWrapping = TextWrapping.Wrap,
            Foreground = B(LitPaperInk),
            Margin = new Thickness(0, 12, 0, 0)
        };
        msgStack.Children.Add(textTb);

        var countTb = new TextBlock
        {
            Text = count,
            FontFamily = Body,
            FontSize = 13,
            Foreground = B("#7a4a32"),
            TextAlignment = TextAlignment.Right,
            Margin = new Thickness(0, 18, 0, 0)
        };
        msgStack.Children.Add(countTb);

        msgBorder.Child = msgStack;
        Canvas.SetLeft(msgBorder, 0);
        Canvas.SetTop(msgBorder, 0);
        container.Children.Add(msgBorder);

        return container;
    }

    /// <summary>
    /// Builds a carved Japanese samurai vermilion seal stamp (Hanko).
    /// </summary>
    public static FrameworkElement SamuraiHankoSeal(string text, double size = 32)
    {
        var border = new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(3),
            Background = B(SamuraiVermilion),
            BorderBrush = B("#ffd3a8"),
            BorderThickness = new Thickness(1.4),
            Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(0xd8, 0x47, 0x3f),
                BlurRadius = 10,
                ShadowDepth = 0,
                Opacity = 0.6
            }
        };

        var tb = new TextBlock
        {
            Text = text,
            FontFamily = Display,
            FontSize = size * 0.44,
            FontWeight = FontWeights.Bold,
            Foreground = B("#fff4d8"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        border.Child = tb;
        return border;
    }

    /// <summary>
    /// Builds illuminated washi parchment panel (used for focused cards, details, and notice dialogs).
    /// </summary>
    public static Border LitPaperCard(double width, double height, Thickness? padding = null)
    {
        return new Border
        {
            Width = width,
            Height = height,
            CornerRadius = new CornerRadius(4),
            Background = CreateLitPaperBrush(),
            BorderBrush = B("rgba(255,220,180,0.6)"),
            BorderThickness = new Thickness(1),
            Padding = padding ?? new Thickness(24, 24, 24, 24),
            Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(0xff, 0xa0, 0x5a),
                BlurRadius = 38,
                ShadowDepth = 12,
                Opacity = 0.45
            }
        };
    }
}
