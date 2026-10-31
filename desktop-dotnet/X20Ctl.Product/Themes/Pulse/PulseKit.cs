using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
namespace X20Ctl.Product.Pulse;

/// <summary>Palette, typography, and frozen brushes for the overhauled Pulse theme:
/// a high-energy Prismatic Harmonic & Bio-Resonance Chamber with live ECG/sine waveforms,
/// 3D cymatic perspective grids, radial spectrum analyzers, and gyroscopic HUD rings.</summary>
internal static class PulseInk
{
    public static Color C(string hex) => (Color)ColorConverter.ConvertFromString(hex);
    public static Color A(Color c, double a) => Color.FromArgb((byte)Math.Clamp(Math.Round(a * 255), 0, 255), c.R, c.G, c.B);
    public static SolidColorBrush B(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }

    public static readonly Color Void0 = C("#03050a");
    public static readonly Color Void1 = C("#070d19");
    public static readonly Color Void2 = C("#0f1e36");
    public static readonly Color Pearl = C("#f2f6fc");
    public static readonly Color Soft = C("#d0daea");
    public static readonly Color Plat = C("#b4c4dc");
    public static readonly Color Sec = C("#96aac8");
    public static readonly Color Mute = C("#8296b4");
    public static readonly Color Sub = C("#7488a8");
    public static readonly Color Ice = C("#a8ccff");
    public static readonly Color Cyan = C("#56b8ff");
    public static readonly Color CyanHot = C("#7CE2FF");
    public static readonly Color IceHi = C("#dce9ff");
    public static readonly Color IceGlow = C("#78aaff");
    public static readonly Color Warm = C("#f0d49c");
    public static readonly Color PrismPink = C("#ff8cc6");
    public static readonly Color Rose = C("#ff7896");
    public static readonly Color RoseText = C("#ff9cb2");

    public static readonly FontFamily Display = new("Segoe UI Variable Display, Segoe UI");
    public static readonly FontFamily Body = new("Segoe UI Variable Text, Segoe UI");
    public static readonly FontFamily Mono = new("Cascadia Mono, Consolas, Segoe UI");
    public static readonly FontFamily Icons = new("Segoe Fluent Icons, Segoe MDL2 Assets");

    // 180deg metallic chrome gradient for the monumental Hero title
    public static readonly LinearGradientBrush LitTitle = Freeze(new LinearGradientBrush { StartPoint = new(0, 0), EndPoint = new(0, 1), GradientStops = { new(C("#ffffff"), 0.00), new(C("#f4f8ff"), 0.38), new(C("#b8d4ff"), 0.72), new(C("#6e98d8"), 1.00) } });

    // Full-spectrum prismatic laser highlight bar (#ff78b4 -> #78c8ff -> #ffe096 -> #a8ccff)
    public static readonly LinearGradientBrush PrismBar = Freeze(new LinearGradientBrush { StartPoint = new(0, 0), EndPoint = new(1, 0), GradientStops = { new(A(Colors.White, 0), 0.00), new(A(C("#ff88c8"), 0.92), 0.22), new(A(C("#88d8ff"), 1.00), 0.50), new(A(C("#ffe098"), 0.92), 0.78), new(A(Colors.White, 0), 1.00) } });

    // Horizontal fade for orbital rails
    public static readonly LinearGradientBrush HorizFade = Freeze(new LinearGradientBrush { StartPoint = new(0, 0), EndPoint = new(1, 0), GradientStops = { new(A(C("#88c8ff"), 0), 0.00), new(A(C("#a8ccff"), 0.45), 0.25), new(A(C("#dce9ff"), 0.75), 0.50), new(A(C("#a8ccff"), 0.45), 0.75), new(A(C("#88c8ff"), 0), 1.00) } });

    // Prismatic glass card fill
    public static readonly LinearGradientBrush PearlCardBg = Freeze(new LinearGradientBrush { StartPoint = new(0, 0), EndPoint = new(1, 1), GradientStops = { new(A(C("#162848"), 0.78), 0.0), new(A(C("#0a1426"), 0.86), 0.55), new(A(C("#060b16"), 0.92), 1.0) } });

    // Frosted action orb fill
    public static readonly RadialGradientBrush OrbFill = Freeze(new RadialGradientBrush
    {
        Center = new(0.32, 0.26),
        GradientOrigin = new(0.32, 0.26),
        RadiusX = 0.78,
        RadiusY = 0.78,
        GradientStops =
        {
            new(A(C("#d8ecff"), 0.28), 0.0),
            new(A(C("#386098"), 0.24), 0.52),
            new(A(C("#0a1324"), 0.82), 1.0)
        }
    });

    // Focused player pod core fill
    public static readonly RadialGradientBrush PodOrbFocused = Freeze(new RadialGradientBrush
    {
        Center = new(0.35, 0.28),
        GradientOrigin = new(0.35, 0.28),
        RadiusX = 0.75,
        RadiusY = 0.75,
        GradientStops =
        {
            new(A(C("#70b0ff"), 0.26), 0.0),
            new(A(C("#1e3c6e"), 0.42), 0.55),
            new(A(C("#070e1c"), 0.88), 1.0)
        }
    });

    // Unfocused assigned player pod core fill
    public static readonly RadialGradientBrush PodOrb = Freeze(new RadialGradientBrush
    {
        Center = new(0.35, 0.30),
        GradientOrigin = new(0.35, 0.30),
        RadiusX = 0.72,
        RadiusY = 0.72,
        GradientStops =
        {
            new(A(C("#a8ccff"), 0.16), 0.0),
            new(A(C("#182c4e"), 0.34), 0.58),
            new(A(C("#070c18"), 0.80), 1.0)
        }
    });

    // Primary reactor pill background
    public static readonly LinearGradientBrush PrimaryBtn = Freeze(new LinearGradientBrush { StartPoint = new(0, 0), EndPoint = new(1, 1), GradientStops = { new(C("#ffffff"), 0.0), new(C("#e4f0ff"), 0.48), new(C("#a8ccff"), 1.0) } });

    public static T Freeze<T>(T f) where T : Freezable { if (f.CanFreeze) f.Freeze(); return f; }

    public static DropShadowEffect Glow(Color c, double radius, double opacity = 0.6) =>
        new() { Color = c, BlurRadius = radius, ShadowDepth = 0, Opacity = opacity };

    public static DropShadowEffect Drop(double dx, double dy, double radius, Color c, double opacity = 0.6) =>
        new() { Color = c, BlurRadius = radius, ShadowDepth = Math.Sqrt(dx * dx + dy * dy), Direction = Math.Atan2(-dy, dx) * 180 / Math.PI, Opacity = opacity };

    public static TextBlock Text(string s, double size, Brush? fg = null, FontWeight? weight = null) => new()
    {
        Text = s,
        FontFamily = Body,
        FontSize = size,
        FontWeight = weight ?? FontWeights.Normal,
        Foreground = fg ?? B(Pearl)
    };

    public static TextBlock Disp(string s, double size, Brush? fg = null, FontWeight? weight = null) => new()
    {
        Text = s,
        FontFamily = Display,
        FontSize = size,
        FontWeight = weight ?? FontWeights.Light,
        Foreground = fg ?? B(Pearl)
    };

    public static TextBlock MonoText(string s, double size, Brush? fg = null, FontWeight? weight = null) => new()
    {
        Text = s,
        FontFamily = Mono,
        FontSize = size,
        FontWeight = weight ?? FontWeights.Medium,
        Foreground = fg ?? B(Ice)
    };

    public static TextBlock Icon(string glyph, double size, Brush? fg = null) => new()
    {
        Text = glyph,
        FontFamily = Icons,
        FontSize = size,
        Foreground = fg ?? B(Pearl),
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center
    };

    public static FrameworkElement Sc(string s, double size = 11.5, Brush? fg = null, double tracking = 0.24, bool center = false, double width = double.NaN)
    {
        var tb = new TextBlock
        {
            FontFamily = Body,
            FontSize = size,
            FontWeight = FontWeights.SemiBold,
            Foreground = fg ?? B(Sec),
            TextAlignment = center ? TextAlignment.Center : TextAlignment.Left
        };
        if (!double.IsNaN(width)) tb.Width = width;
        string upper = s.ToUpperInvariant();
        string spacer = tracking >= 0.28 ? "\u2009\u200A" : "\u2009";
        tb.Text = string.Join(spacer, upper.ToCharArray());
        return tb;
    }
}

/// <summary>Shared geometry, animated ECG/sine waveform ribbons, 3D cymatic perspective grids,
/// radial spectrum analyzers, and gyroscopic HUD rings for the overhauled Pulse theme.</summary>
internal static class PulseKit
{
    public static void Put(FrameworkElement e, double x, double y)
    {
        e.HorizontalAlignment = HorizontalAlignment.Left;
        e.VerticalAlignment = VerticalAlignment.Top;
        e.Margin = new(x, y, 0, 0);
    }

    public static void At(Canvas c, UIElement e, double x, double y)
    {
        Canvas.SetLeft(e, x);
        Canvas.SetTop(e, y);
        c.Children.Add(e);
    }

    public static Brush Radial(double cx, double cy, double rx, double ry, Color c0, Color c1, double stop = 1.0)
    {
        var b = new RadialGradientBrush
        {
            Center = new(cx, cy),
            GradientOrigin = new(cx, cy),
            RadiusX = rx,
            RadiusY = ry
        };
        b.GradientStops.Add(new(c0, 0));
        b.GradientStops.Add(new(c1, stop));
        b.Freeze();
        return b;
    }

    /// <summary>Builds the 1600x900 Prismatic Harmonic Chamber backdrop:
    /// deep obsidian-sapphire void, volumetric prismatic light shafts, 3D perspective cymatic sonar grid,
    /// ambient harmonic blooms, and floating depth-of-field pearl dust motes.</summary>
    public static Canvas VoidBackdrop(bool hero = false)
    {
        var c = new Canvas { Width = 1600, Height = 900, ClipToBounds = true, IsHitTestVisible = false };

        // 1. Base chamber gradient
        var baseGrad = new LinearGradientBrush { StartPoint = new(0.5, 0), EndPoint = new(0.5, 1), GradientStops = { new(PulseInk.C("#091428"), 0.00), new(PulseInk.C("#050b16"), 0.52), new(PulseInk.C("#020409"), 1.00) } };
        baseGrad.Freeze();
        c.Children.Add(new Rectangle { Width = 1600, Height = 900, Fill = baseGrad });

        // 2. Ambient radial blooms (ice cyan, prismatic rose-violet, warm champagne)
        c.Children.Add(new Rectangle
        {
            Width = 1600,
            Height = 900,
            Fill = Radial(hero ? 0.68 : 0.50, hero ? 0.44 : 0.36, 0.58, 0.48,
                PulseInk.A(PulseInk.C("#3888ff"), 0.20), PulseInk.A(PulseInk.C("#3888ff"), 0), 0.68)
        });
        c.Children.Add(new Rectangle
        {
            Width = 1600,
            Height = 900,
            Fill = Radial(0.18, 0.68, 0.45, 0.42,
                PulseInk.A(PulseInk.C("#d878ff"), 0.08), PulseInk.A(PulseInk.C("#d878ff"), 0), 0.65)
        });
        c.Children.Add(new Rectangle
        {
            Width = 1600,
            Height = 900,
            Fill = Radial(0.82, 0.22, 0.40, 0.36,
                PulseInk.A(PulseInk.C("#ffd088"), 0.08), PulseInk.A(PulseInk.C("#ffd088"), 0), 0.65)
        });

        // 3. Volumetric prismatic light shafts descending from top chamber
        c.Children.Add(LightShaft(180, -40, 380, 620, 740, 900, PulseInk.C("#68b0ff"), 0.065));
        c.Children.Add(LightShaft(680, -40, 920, 980, 1280, 900, PulseInk.C("#ff88c8"), 0.045));
        c.Children.Add(LightShaft(1020, -40, 1280, 760, 1520, 900, PulseInk.C("#ffd494"), 0.055));

        // 4. 3D Perspective Cymatic Horizon Grid & Sonar Floor (y = 500..900)
        var gridCanvas = new Canvas { Width = 1600, Height = 900, Opacity = hero ? 0.72 : 0.88 };
        double vx = hero ? 1090 : 800, vy = 470;
        var gridStroke = PulseInk.B(PulseInk.A(PulseInk.C("#68a8ff"), 0.14));
        var gridBright = PulseInk.B(PulseInk.A(PulseInk.C("#a8ccff"), 0.24));

        // Perspective radial rays from vanishing point (vx, vy) to bottom perimeter
        for (int i = -12; i <= 12; i++)
        {
            double bx = vx + i * 135;
            var line = new Line
            {
                X1 = vx + i * 18,
                Y1 = vy + 18,
                X2 = bx,
                Y2 = 900,
                Stroke = i % 3 == 0 ? gridBright : gridStroke,
                StrokeThickness = i % 3 == 0 ? 1.0 : 0.6,
                Opacity = 1.0 - Math.Abs(i) / 14.0
            };
            gridCanvas.Children.Add(line);
        }

        // Perspective horizontal harmonic wave planes (exponential depth spacing)
        for (int r = 1; r <= 8; r++)
        {
            double t = Math.Pow(r / 8.0, 1.65);
            double y = vy + 24 + t * (900 - vy - 24);
            double rx = 220 + t * 1180;
            double ry = 18 + t * 115;
            var el = new Ellipse
            {
                Width = rx * 2,
                Height = ry * 2,
                Stroke = r % 2 == 0 ? gridBright : gridStroke,
                StrokeThickness = r % 2 == 0 ? 1.1 : 0.7,
                StrokeDashArray = r % 3 == 0 ? new DoubleCollection([4, 6]) : null,
                Opacity = 0.25 + 0.65 * (1 - t * 0.5)
            };
            At(gridCanvas, el, vx - rx, y - ry);
        }
        c.Children.Add(gridCanvas);

        // 5. Floating depth-of-field cymatic pearl & champagne motes
        var rng = new Random(42);
        for (int i = 0; i < 42; i++)
        {
            double mx = rng.NextDouble() * 1560 + 20;
            double my = rng.NextDouble() * 820 + 40;
            double sz = 1.6 + rng.NextDouble() * 3.2;
            Color col = (i % 5 == 0) ? PulseInk.Warm : (i % 3 == 0 ? PulseInk.CyanHot : PulseInk.IceHi);
            var dot = new Ellipse
            {
                Width = sz,
                Height = sz,
                Fill = PulseInk.B(PulseInk.A(col, 0.22 + rng.NextDouble() * 0.42))
            };
            At(c, dot, mx, my);
        }

        return c;
    }

    private static Polygon LightShaft(double x0, double y0, double x1, double bx0, double bx1, double y1, Color color, double alpha)
    {
        var brush = new LinearGradientBrush { StartPoint = new(0.5, 0), EndPoint = new(0.5, 1), GradientStops = { new(PulseInk.A(color, alpha), 0.0), new(PulseInk.A(color, alpha * 0.45), 0.55), new(PulseInk.A(color, 0), 1.0) } };
        brush.Freeze();
        return new Polygon
        {
            Fill = brush,
            Points = new PointCollection
            {
                new(x0, y0),
                new(x1, y0),
                new(bx1, y1),
                new(bx0, y1)
            }
        };
    }

    /// <summary>Builds a full-width (1600px) Multi-Sine & Cardiac ECG Waveform Ribbon.
    /// Spikes with a sharp P-Q-R-S-T harmonic burst at active player slots and flatlines under empty slots.</summary>
    public static Canvas WaveformRibbon(double yCenter, double[] spikeXs, bool[] activeSpikes, int primaryIndex, double phase = 1.45)
    {
        var c = new Canvas { Width = 1600, Height = 900, IsHitTestVisible = false };

        // 1. Three phase-shifted harmonic sine carrier waves across the 1600px stage
        c.Children.Add(SineRibbonPath(yCenter, 18, 0.0085, phase, PulseInk.A(PulseInk.PrismPink, 0.32), 1.3));
        c.Children.Add(SineRibbonPath(yCenter, 24, 0.0062, phase + 1.9, PulseInk.A(PulseInk.Warm, 0.28), 1.2));
        c.Children.Add(SineRibbonPath(yCenter, 14, 0.0115, phase + 3.4, PulseInk.A(PulseInk.Cyan, 0.38), 1.5));

        // 2. Primary Cardiac ECG + Acoustic Resonance Trace
        var sg = new StreamGeometry();
        using (var ctx = sg.Open())
        {
            ctx.BeginFigure(new Point(0, yCenter), false, false);
            for (int x = 2; x <= 1600; x += 2)
            {
                double dy = Math.Sin(x * 0.012 + phase) * 4.5 * Math.Sin(x * 0.003 - phase * 0.5);
                for (int i = 0; i < spikeXs.Length; i++)
                {
                    double sx = spikeXs[i];
                    double u = (x - sx) / 54.0;
                    if (Math.Abs(u) < 2.2)
                    {
                        if (activeSpikes[i])
                        {
                            double amp = (i == primaryIndex) ? 58.0 : 32.0;
                            // Authentic P-Q-R-S-T cardiac + high-frequency cymatic packet
                            double pqrst =
                                -0.22 * Math.Exp(-Math.Pow((u + 0.72) / 0.18, 2))   // P wave
                                + 0.32 * Math.Exp(-Math.Pow((u + 0.24) / 0.08, 2))  // Q dip
                                - 1.00 * Math.Exp(-Math.Pow(u / 0.09, 2))           // R spike (tall upward peak)
                                + 0.58 * Math.Exp(-Math.Pow((u - 0.22) / 0.09, 2))  // S dip
                                - 0.34 * Math.Exp(-Math.Pow((u - 0.68) / 0.22, 2))  // T wave
                                + 0.25 * Math.Cos(u * 14.0) * Math.Exp(-u * u * 1.8);
                            dy += amp * pqrst;
                        }
                        else
                        {
                            // Flatline damping under empty slots
                            dy *= 0.15;
                        }
                    }
                }
                ctx.LineTo(new Point(x, yCenter + dy), true, true);
            }
        }
        sg.Freeze();

        // Outer neon glow path
        c.Children.Add(new Path
        {
            Data = sg,
            Stroke = PulseInk.B(PulseInk.A(PulseInk.Cyan, 0.55)),
            StrokeThickness = 4.2,
            StrokeLineJoin = PenLineJoin.Round,
            Effect = new BlurEffect { Radius = 10 }
        });

        // Crisp hot-white/ice core trace
        c.Children.Add(new Path
        {
            Data = sg,
            Stroke = PulseInk.B(PulseInk.IceHi),
            StrokeThickness = 1.8,
            StrokeLineJoin = PenLineJoin.Round
        });

        // Glowing pulse nodes at each active spike peak
        for (int i = 0; i < spikeXs.Length; i++)
        {
            if (!activeSpikes[i]) continue;
            double sx = spikeXs[i];
            double peakY = yCenter - (i == primaryIndex ? 58.0 : 32.0);
            var dot = new Ellipse
            {
                Width = i == primaryIndex ? 10 : 7,
                Height = i == primaryIndex ? 10 : 7,
                Fill = Brushes.White,
                Effect = PulseInk.Glow(PulseInk.CyanHot, 14, 0.95)
            };
            At(c, dot, sx - dot.Width / 2, peakY - dot.Height / 2);
        }

        var waveAnim = new System.Windows.Media.Animation.DoubleAnimation(0.65, 1.0, TimeSpan.FromSeconds(1.3))
        {
            AutoReverse = true,
            RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever
        };
        c.BeginAnimation(UIElement.OpacityProperty, waveAnim);

        return c;
    }

    private static Path SineRibbonPath(double yCenter, double amp, double freq, double phase, Color color, double thickness)
    {
        var sg = new StreamGeometry();
        using (var ctx = sg.Open())
        {
            for (int x = 0; x <= 1600; x += 6)
            {
                double env = Math.Sin(Math.PI * x / 1600.0);
                double y = yCenter + Math.Sin(x * freq + phase) * amp * env
                           + Math.Cos(x * freq * 1.7 - phase * 0.6) * (amp * 0.35) * env;
                if (x == 0) ctx.BeginFigure(new Point(x, y), false, false);
                else ctx.LineTo(new Point(x, y), true, true);
            }
        }
        sg.Freeze();
        return new Path
        {
            Data = sg,
            Stroke = PulseInk.B(color),
            StrokeThickness = thickness
        };
    }

    /// <summary>Builds a circular Radial Audio/Haptic Frequency Spectrum Analyzer around (cx, cy).</summary>
    public static Canvas RadialSpectrum(double cx, double cy, double innerR, int barCount, double minLen, double maxLen, double phase = 0.8)
    {
        var c = new Canvas { Width = 1600, Height = 900, IsHitTestVisible = false };
        for (int i = 0; i < barCount; i++)
        {
            double ang = i * 2 * Math.PI / barCount;
            // Multi-harmonic standing wave envelope (4-fold + 8-fold + 12-fold cymatic symmetry)
            double wave = 0.45 * Math.Abs(Math.Cos(2 * ang + phase))
                        + 0.35 * Math.Abs(Math.Sin(4 * ang - phase * 1.4))
                        + 0.20 * Math.Abs(Math.Cos(6 * ang + phase * 0.7));
            double len = minLen + (maxLen - minLen) * wave;
            double r0 = innerR;
            double r1 = innerR + len;

            Color col = (i % 6 == 0) ? PulseInk.Warm
                      : (i % 3 == 0) ? PulseInk.PrismPink
                      : (wave > 0.68 ? PulseInk.IceHi : PulseInk.Cyan);
            double alpha = 0.32 + 0.58 * wave;

            var line = new Line
            {
                X1 = cx + r0 * Math.Cos(ang),
                Y1 = cy + r0 * Math.Sin(ang),
                X2 = cx + r1 * Math.Cos(ang),
                Y2 = cy + r1 * Math.Sin(ang),
                Stroke = PulseInk.B(PulseInk.A(col, alpha)),
                StrokeThickness = barCount > 60 ? 2.2 : 2.5,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            c.Children.Add(line);
        }
        return c;
    }

    /// <summary>Builds a precision Gyroscopic HUD Ring Assembly around (cx, cy) with degree ticks,
    /// cardinal crosshairs, dashed harmonic rings, and 4-fold cymatic nodal curves.</summary>
    public static Canvas GyroRings(double cx, double cy, double r, bool active, bool showMandala = false)
    {
        var c = new Canvas { Width = 1600, Height = 900, IsHitTestVisible = false };

        // 1. Outer degree-tick ring
        int ticks = active ? 72 : 36;
        for (int i = 0; i < ticks; i++)
        {
            double ang = i * 2 * Math.PI / ticks;
            bool major = i % (ticks / 12) == 0;
            bool cardinal = i % (ticks / 4) == 0;
            double len = cardinal ? 14 : (major ? 8 : 4);
            double rOut = r + (cardinal ? 6 : 0);
            double rIn = rOut - len;
            Color col = cardinal ? PulseInk.Warm : (major ? PulseInk.IceHi : PulseInk.Ice);
            double alpha = active ? (cardinal ? 0.85 : (major ? 0.55 : 0.28)) : (major ? 0.30 : 0.14);

            c.Children.Add(new Line
            {
                X1 = cx + rIn * Math.Cos(ang),
                Y1 = cy + rIn * Math.Sin(ang),
                X2 = cx + rOut * Math.Cos(ang),
                Y2 = cy + rOut * Math.Sin(ang),
                Stroke = PulseInk.B(PulseInk.A(col, alpha)),
                StrokeThickness = cardinal ? 2.0 : (major ? 1.3 : 0.9)
            });
        }

        // 2. Dashed technical orbit ring
        double d2 = (r - 14) * 2;
        var dashed = new Ellipse
        {
            Width = d2,
            Height = d2,
            Stroke = PulseInk.B(PulseInk.A(PulseInk.Cyan, active ? 0.42 : 0.20)),
            StrokeThickness = 1.2,
            StrokeDashArray = new DoubleCollection([6, 4, 2, 4])
        };
        At(c, dashed, cx - d2 / 2, cy - d2 / 2);

        // 3. Optional 4-fold Cymatic Standing-Wave Mandala (for Hero / Focused Pod)
        if (showMandala)
        {
            for (int k = 0; k < 2; k++)
            {
                var sg = new StreamGeometry();
                using (var ctx = sg.Open())
                {
                    int steps = 180;
                    for (int s = 0; s <= steps; s++)
                    {
                        double a = s * 2 * Math.PI / steps;
                        double rm = (r * 0.72) * (1.0 + (k == 0 ? 0.18 : -0.14) * Math.Cos(4 * a) + 0.08 * Math.Cos(8 * a));
                        var pt = new Point(cx + rm * Math.Cos(a), cy + rm * Math.Sin(a));
                        if (s == 0) ctx.BeginFigure(pt, false, true);
                        else ctx.LineTo(pt, true, true);
                    }
                }
                sg.Freeze();
                c.Children.Add(new Path
                {
                    Data = sg,
                    Stroke = PulseInk.B(PulseInk.A(k == 0 ? PulseInk.Cyan : PulseInk.PrismPink, k == 0 ? 0.24 : 0.16)),
                    StrokeThickness = 1.2,
                    StrokeDashArray = k == 1 ? new DoubleCollection([3, 5]) : null
                });
            }
        }

        return c;
    }

    public static StreamGeometry ArcGeo(double cx, double cy, double r, double a0, double a1)
    {
        double r0 = (a0 - 90) * Math.PI / 180.0;
        double r1 = (a1 - 90) * Math.PI / 180.0;
        Point p0 = new(cx + r * Math.Cos(r0), cy + r * Math.Sin(r0));
        Point p1 = new(cx + r * Math.Cos(r1), cy + r * Math.Sin(r1));
        bool large = Math.Abs(a1 - a0) > 180.0;
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(p0, false, false);
            ctx.ArcTo(p1, new Size(r, r), 0, large, SweepDirection.Clockwise, true, false);
        }
        g.Freeze();
        return g;
    }

    /// <summary>Triple-layer sonic-boom resonance wings ((•)) flanking the focused pod or reactor.</summary>
    public static Canvas Resonance(double cx, double cy, double r, int pairs = 3, double span = 30, double op = 0.85)
    {
        var c = new Canvas { Width = 1600, Height = 900, IsHitTestVisible = false };
        for (int i = 0; i < pairs; i++)
        {
            double ri = r + i * 24;
            double a = op * (1.0 - i * 0.26);
            double s = span - i * 4;
            Color col = i == 0 ? PulseInk.IceHi : (i == 1 ? PulseInk.Cyan : PulseInk.PrismPink);
            foreach (double mid in new[] { 90.0, 270.0 })
            {
                var p = new Path
                {
                    Data = ArcGeo(cx, cy, ri, mid - s, mid + s),
                    Stroke = PulseInk.B(PulseInk.A(col, a)),
                    StrokeThickness = i == 0 ? 2.8 : (i == 1 ? 1.8 : 1.2),
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                    Effect = PulseInk.Glow(PulseInk.Cyan, 12, 0.70)
                };
                c.Children.Add(p);
            }
        }
        return c;
    }

    /// <summary>Concentric cymatic shockwave ripples around (cx, cy).</summary>
    public static Canvas Ripples(double cx, double cy, int n = 6, double r0 = 110, double step = 55, double op = 0.14)
    {
        var c = new Canvas { Width = 1600, Height = 900, IsHitTestVisible = false };
        for (int i = 0; i < n; i++)
        {
            double r = r0 + i * step;
            double a = op * (1.0 - (double)i / n * 0.78);
            bool dash = i % 2 == 1;
            var el = new Ellipse
            {
                Width = r * 2,
                Height = r * 2,
                Stroke = PulseInk.B(PulseInk.A(i == 0 ? PulseInk.CyanHot : PulseInk.Ice, a)),
                StrokeThickness = i == 0 ? 1.5 : 1.0
            };
            if (dash)
            {
                el.StrokeDashArray = new DoubleCollection([4, 8]);
                el.StrokeDashCap = PenLineCap.Round;
            }
            At(c, el, cx - r, cy - r);
        }
        return c;
    }

    /// <summary>Soft radial energy aura behind a controller or focused pod.</summary>
    public static Border Aura(double cx, double cy, double rx, double ry, double op = 0.32)
    {
        var b = new RadialGradientBrush
        {
            Center = new(0.5, 0.5),
            GradientOrigin = new(0.5, 0.5),
            RadiusX = 0.5,
            RadiusY = 0.5
        };
        b.GradientStops.Add(new(PulseInk.A(PulseInk.Cyan, op), 0.0));
        b.GradientStops.Add(new(PulseInk.A(PulseInk.PrismPink, op * 0.38), 0.42));
        b.GradientStops.Add(new(PulseInk.A(PulseInk.Warm, op * 0.22), 0.68));
        b.GradientStops.Add(new(PulseInk.A(Colors.Black, 0), 1.0));
        b.Freeze();

        var el = new Border
        {
            Width = rx * 2,
            Height = ry * 2,
            Background = b,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(el, cx - rx);
        Canvas.SetTop(el, cy - ry);
        return el;
    }

    /// <summary>Circular frosted-glass orb with glowing bezel and inner specular rim.</summary>
    public static Border Orb(double d, UIElement? child = null, bool on = false, Brush? customBg = null, Color? accentBorder = null)
    {
        Color borderCol = accentBorder ?? (on ? PulseInk.IceHi : PulseInk.C("#a8ccff"));
        var outer = new Border
        {
            Width = d,
            Height = d,
            CornerRadius = new(d / 2),
            Background = customBg ?? (on ? PulseInk.PodOrbFocused : PulseInk.OrbFill),
            BorderBrush = PulseInk.B(on ? borderCol : PulseInk.A(borderCol, 0.42)),
            BorderThickness = new(on ? 2.0 : 1.2),
            Effect = on
                ? PulseInk.Glow(PulseInk.Cyan, 28, 0.65)
                : PulseInk.Drop(0, 14, 28, Colors.Black, 0.60)
        };
        if (child != null) outer.Child = child;
        return outer;
    }

    /// <summary>52x52 circular Lissajous / Cymatic Harmonic Oscilloscope widget for the command deck.</summary>
    public static Canvas CymaticWidget()
    {
        var c = new Canvas { Width = 52, Height = 52, IsHitTestVisible = false };
        var ring = new Ellipse
        {
            Width = 52,
            Height = 52,
            Fill = PulseInk.B(PulseInk.A(PulseInk.C("#0d1b34"), 0.85)),
            Stroke = PulseInk.B(PulseInk.A(PulseInk.Cyan, 0.55)),
            StrokeThickness = 1.4,
            Effect = PulseInk.Glow(PulseInk.Cyan, 12, 0.45)
        };
        c.Children.Add(ring);

        // Lissajous standing wave (3:4 harmonic figure)
        var sg = new StreamGeometry();
        using (var ctx = sg.Open())
        {
            for (int i = 0; i <= 120; i++)
            {
                double t = i * 2 * Math.PI / 120;
                double x = 26 + 17 * Math.Sin(3 * t + 0.4);
                double y = 26 + 17 * Math.Sin(4 * t);
                if (i == 0) ctx.BeginFigure(new Point(x, y), false, true);
                else ctx.LineTo(new Point(x, y), true, true);
            }
        }
        sg.Freeze();
        c.Children.Add(new Path
        {
            Data = sg,
            Stroke = PulseInk.B(PulseInk.A(PulseInk.CyanHot, 0.85)),
            StrokeThickness = 1.3
        });

        var dot = new Ellipse
        {
            Width = 6,
            Height = 6,
            Fill = PulseInk.B(PulseInk.Warm),
            Effect = PulseInk.Glow(PulseInk.Warm, 8, 0.9)
        };
        At(c, dot, 23, 23);
        return c;
    }

    /// <summary>Prismatic Glass Command Deck card with 2.5px full-spectrum laser highlight bar and corner bracket accents.</summary>
    public static Grid PearlCard(double x, double y, double w, UIElement content, Thickness? padding = null, double h = double.NaN)
    {
        var root = new Grid { Width = w, ClipToBounds = false };
        if (!double.IsNaN(h)) root.Height = h;
        Put(root, x, y);

        var body = new Border
        {
            CornerRadius = new(24),
            Background = PulseInk.PearlCardBg,
            BorderBrush = PulseInk.B(PulseInk.A(PulseInk.C("#a8ccff"), 0.34)),
            BorderThickness = new(1.2),
            Padding = padding ?? new Thickness(32, 22, 32, 22),
            Effect = PulseInk.Drop(0, 24, 52, Colors.Black, 0.72),
            Child = content
        };
        root.Children.Add(body);

        // Full-spectrum prismatic top laser bar
        var topBar = new Border
        {
            Height = 2.5,
            Margin = new(24, 0, 24, 0),
            VerticalAlignment = VerticalAlignment.Top,
            Background = PulseInk.PrismBar,
            Effect = PulseInk.Glow(PulseInk.CyanHot, 10, 0.75),
            IsHitTestVisible = false
        };
        root.Children.Add(topBar);

        return root;
    }

    /// <summary>Circular console key cap (A, B, Y, LB, RB) with glowing cyan/pearl rim.</summary>
    public static Border KeyCap(string k, double minW = 28, double h = 28)
    {
        bool wide = k.Length > 1;
        return new Border
        {
            MinWidth = wide ? 36 : minW,
            Height = h,
            Padding = new(wide ? 8 : 0, 0, wide ? 8 : 0, 0),
            CornerRadius = new(h / 2),
            Background = PulseInk.B(PulseInk.A(PulseInk.C("#14284c"), 0.85)),
            BorderBrush = PulseInk.B(PulseInk.A(PulseInk.CyanHot, 0.65)),
            BorderThickness = new(1.3),
            Effect = PulseInk.Glow(PulseInk.Cyan, 8, 0.35),
            Child = new TextBlock
            {
                Text = k,
                FontFamily = PulseInk.Body,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
    }

    /// <summary>Bottom-right console hint bar.</summary>
    public static StackPanel Hints(params (string key, string label)[] items)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new(0, 836, 56, 0),
            IsHitTestVisible = false
        };
        for (int i = 0; i < items.Length; i++)
        {
            var (k, lbl) = items[i];
            var pair = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new(i == 0 ? 0 : 24, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            pair.Children.Add(KeyCap(k));
            var tb = PulseInk.Text(lbl, 14, PulseInk.B(PulseInk.Soft), FontWeights.Medium);
            tb.Margin = new(9, 0, 0, 0);
            tb.VerticalAlignment = VerticalAlignment.Center;
            pair.Children.Add(tb);
            row.Children.Add(pair);
        }
        return row;
    }

    /// <summary>Top-right Prismatic Telemetry Status Pill: glowing orb + Player 1 + clock.</summary>
    public static Border StatusBar(FrameworkElement clockElement, out TextBlock whoLabel)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };

        var avatar = new Ellipse
        {
            Width = 22,
            Height = 22,
            Fill = new RadialGradientBrush
            {
                Center = new(0.35, 0.30),
                GradientOrigin = new(0.35, 0.30),
                GradientStops =
                {
                    new(Colors.White, 0.0),
                    new(PulseInk.C("#88d4ff"), 0.55),
                    new(PulseInk.C("#2858a8"), 1.0)
                }
            },
            Effect = PulseInk.Glow(PulseInk.CyanHot, 12, 0.80),
            VerticalAlignment = VerticalAlignment.Center
        };
        row.Children.Add(avatar);

        whoLabel = PulseInk.Text("Player 1", 15.5, Brushes.White, FontWeights.SemiBold);
        whoLabel.Margin = new(10, 0, 0, 0);
        whoLabel.VerticalAlignment = VerticalAlignment.Center;
        row.Children.Add(whoLabel);

        var clockWrap = new Border { Margin = new(14, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Child = clockElement };
        row.Children.Add(clockWrap);

        return new Border
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new(0, 26, 48, 0),
            CornerRadius = new(20),
            Padding = new(14, 7, 16, 7),
            Background = PulseInk.B(PulseInk.A(PulseInk.C("#0b162c"), 0.78)),
            BorderBrush = PulseInk.B(PulseInk.A(PulseInk.C("#a8ccff"), 0.32)),
            BorderThickness = new(1),
            Effect = PulseInk.Drop(0, 10, 24, Colors.Black, 0.55),
            Child = row,
            IsHitTestVisible = false
        };
    }

    /// <summary>Prismatic telemetry status capsule with glowing LED indicator dot.</summary>
    public static Border Chip(string text, bool activeLed = false, double h = 34, double fontSize = 13.5)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var led = new Ellipse
        {
            Width = 6,
            Height = 6,
            Margin = new(0, 0, 8, 0),
            Fill = PulseInk.B(activeLed ? PulseInk.CyanHot : PulseInk.Warm),
            Effect = PulseInk.Glow(activeLed ? PulseInk.CyanHot : PulseInk.Warm, 6, 0.8),
            VerticalAlignment = VerticalAlignment.Center
        };
        row.Children.Add(led);
        row.Children.Add(new TextBlock
        {
            Text = text,
            FontFamily = PulseInk.Body,
            FontSize = fontSize,
            FontWeight = FontWeights.Medium,
            Foreground = PulseInk.B(activeLed ? PulseInk.Pearl : PulseInk.Plat),
            VerticalAlignment = VerticalAlignment.Center
        });

        return new Border
        {
            Height = h,
            Padding = new(16, 0, 18, 0),
            CornerRadius = new(h / 2),
            Background = PulseInk.B(PulseInk.A(PulseInk.C("#10203c"), 0.72)),
            BorderBrush = PulseInk.B(PulseInk.A(activeLed ? PulseInk.CyanHot : PulseInk.C("#a8ccff"), activeLed ? 0.50 : 0.28)),
            BorderThickness = new(1),
            Child = row
        };
    }

    public static ControlTemplate PlainButtonTemplate()
    {
        var t = new ControlTemplate(typeof(Button));
        var f = new FrameworkElementFactory(typeof(Border));
        f.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        f.AppendChild(new FrameworkElementFactory(typeof(ContentPresenter)));
        t.VisualTree = f;
        return t;
    }

    /// <summary>Loads the approved Brand/themes/pulse/logo.png with proportional aspect ratio and cyan harmonic bloom.</summary>
    public static Image LogoMark(double width = 52)
    {
        double height = width * 523.0 / 752.0;
        var img = new Image
        {
            Width = width,
            Height = height,
            Stretch = Stretch.Fill,
            Effect = PulseInk.Glow(PulseInk.CyanHot, 14, 0.65),
            VerticalAlignment = VerticalAlignment.Center
        };
        RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
        string logoPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Brand", "themes", "pulse", "logo.png");
        if (System.IO.File.Exists(logoPath))
            img.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(logoPath));
        return img;
    }

    /// <summary>2px full-spectrum prismatic laser highlight bar for the top edge of Studio &amp; Tools glass cards.</summary>
    public static Border PrismTopBar(double marginH = 18) => new()
    {
        Height = 2.2,
        Margin = new(marginH, 0, marginH, 0),
        VerticalAlignment = VerticalAlignment.Top,
        Background = PulseInk.PrismBar,
        Effect = PulseInk.Glow(PulseInk.CyanHot, 8, 0.72),
        IsHitTestVisible = false
    };

    /// <summary>Self-contained Viewbox containing the Gyroscopic HUD Ring, 60-bar Radial Spectrum Analyzer,
    /// 4-fold Cymatic Standing-Wave Mandala, and Sonic-Boom Resonance Wings to frame any Studio controller visual.</summary>
    public static Viewbox ControllerChamberHalo(bool showRipples = false, double opacity = 0.90)
    {
        var c = new Canvas { Width = 960, Height = 680, IsHitTestVisible = false };
        double cx = 480, cy = 340;
        c.Children.Add(Aura(cx, cy, 380, 260, 0.28));
        if (showRipples)
            c.Children.Add(Ripples(cx, cy, 5, 110, 48, 0.16));
        c.Children.Add(RadialSpectrum(cx, cy, 175, 64, 10, 42, 1.35));
        c.Children.Add(GyroRings(cx, cy, 232, active: true, showMandala: true));
        c.Children.Add(Resonance(cx, cy, 248, 3, 28, 0.85));

        return new Viewbox
        {
            Child = c,
            Stretch = Stretch.Uniform,
            Opacity = opacity,
            IsHitTestVisible = false,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    /// <summary>Compact horizontal Cardiac ECG + Multi-Sine harmonic strip for Studio status &amp; telemetry cards.</summary>
    public static Border MiniEcgStrip(string label = "WAVE FIELD")
    {
        var c = new Canvas { Width = 280, Height = 34, ClipToBounds = true, IsHitTestVisible = false };
        // Carrier sine wave
        var sineGeo = new StreamGeometry();
        using (var ctx = sineGeo.Open())
        {
            for (int x = 0; x <= 280; x += 4)
            {
                double y = 17 + Math.Sin(x * 0.055 + 1.2) * 6.5 * Math.Sin(Math.PI * x / 280.0);
                if (x == 0) ctx.BeginFigure(new Point(x, y), false, false);
                else ctx.LineTo(new Point(x, y), true, true);
            }
        }
        sineGeo.Freeze();
        c.Children.Add(new Path { Data = sineGeo, Stroke = PulseInk.B(PulseInk.A(PulseInk.PrismPink, 0.42)), StrokeThickness = 1.2 });

        // Cardiac ECG trace
        var ecgGeo = new StreamGeometry();
        using (var ctx = ecgGeo.Open())
        {
            for (int x = 0; x <= 280; x += 2)
            {
                double dy = Math.Sin(x * 0.04) * 2.0;
                foreach (double sx in new[] { 92.0, 206.0 })
                {
                    double u = (x - sx) / 22.0;
                    if (Math.Abs(u) < 2.0)
                    {
                        double pqrst = -0.22 * Math.Exp(-Math.Pow((u + 0.70) / 0.18, 2))
                                     + 0.30 * Math.Exp(-Math.Pow((u + 0.22) / 0.08, 2))
                                     - 1.00 * Math.Exp(-Math.Pow(u / 0.09, 2))
                                     + 0.55 * Math.Exp(-Math.Pow((u - 0.22) / 0.09, 2))
                                     - 0.30 * Math.Exp(-Math.Pow((u - 0.65) / 0.20, 2));
                        dy += 13.5 * pqrst;
                    }
                }
                if (x == 0) ctx.BeginFigure(new Point(x, 18 + dy), false, false);
                else ctx.LineTo(new Point(x, 18 + dy), true, true);
            }
        }
        ecgGeo.Freeze();
        c.Children.Add(new Path { Data = ecgGeo, Stroke = PulseInk.B(PulseInk.CyanHot), StrokeThickness = 1.6, Effect = PulseInk.Glow(PulseInk.Cyan, 6, 0.75) });

        var dock = new DockPanel();
        var tag = PulseInk.MonoText(label, 10, PulseInk.B(PulseInk.CyanHot));
        tag.VerticalAlignment = VerticalAlignment.Center;
        tag.Margin = new(0, 0, 10, 0);
        DockPanel.SetDock(tag, Dock.Left);
        dock.Children.Add(tag);
        dock.Children.Add(new Viewbox { Child = c, Stretch = Stretch.Uniform, MaxHeight = 32, HorizontalAlignment = HorizontalAlignment.Right });

        return new Border
        {
            CornerRadius = new(10),
            Padding = new(12, 4, 12, 4),
            Margin = new(0, 8, 0, 6),
            Background = PulseInk.B(PulseInk.A(PulseInk.C("#09162b"), 0.78)),
            BorderBrush = PulseInk.B(PulseInk.A(PulseInk.Cyan, 0.28)),
            BorderThickness = new(1),
            Child = dock
        };
    }
}
