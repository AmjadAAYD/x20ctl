using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Pulse;

namespace X20Ctl.Product;

/// <summary>
/// Pulse theme architectural primitives translated from <c>t_pulse.py</c>:
/// Left orbital dock (<c>orb_nav</c>), rising alabaster pearl sheets (<c>.pearl</c>),
/// frosted harmonic glass rows (<c>.row</c>, <c>.row.c</c>), concentric ring dials (<c>ring_dial</c>),
/// and harmonic wave curves (<c>wave_line</c>).
/// </summary>
internal static class PulseParts
{
    public static readonly FontFamily DispFont = PulseInk.Display;
    public static readonly FontFamily BodyFont = PulseInk.Body;
    public static readonly FontFamily MonoFont = PulseInk.Mono;

    public static readonly Color VoidColor = PulseInk.C("#060711");
    public static readonly Color DeepColor = PulseInk.C("#0b0f22");
    public static readonly Color CyanColor = PulseInk.C("#62d0ff");
    public static readonly Color CyanBright = PulseInk.C("#8ce8ff");
    public static readonly Color VioletColor = PulseInk.C("#b36bff");
    public static readonly Color MintColor = PulseInk.C("#5ef2c2");
    public static readonly Color RoseColor = PulseInk.C("#ff6b9d");
    public static readonly Color AmberColor = PulseInk.C("#ffc864");
    public static readonly Color TextPrimary = PulseInk.C("#f4f6ff");
    public static readonly Color TextSoft = PulseInk.C("#9aa6c6");
    public static readonly Color TextDim = PulseInk.C("#5f6b8a");
    public static readonly Color PearlInk = PulseInk.C("#f4f6ff");
    public static readonly Color PearlMid = PulseInk.C("#c3ccda");
    public static readonly Color PearlSoft = PulseInk.C("#8a94a6");
    public static readonly Color PearlColor = PulseInk.C("#eef2f8");
    public static readonly Brush PearlWhiteBrush = new LinearGradientBrush(Colors.White, PulseInk.C("#dde5f1"), 135);

    public static readonly (string Key, string Icon, string Label)[] OrbitItems =
    [
        ("Dashboard", "◎", "Home"),
        ("Buttons", "◉", "Map"),
        ("Curves", "∿", "Curves"),
        ("Macros", "⟡", "Macros"),
        ("Vibration", "≋", "Pulse"),
        ("PcMode", "⊞", "PC"),
        ("Device", "◈", "Device"),
        ("Mouse", "⌖", "Mouse"),
        ("Setups", "❖", "Setups"),
        ("Tester", "◬", "Test"),
        ("Agent", "✦", "XCTL"),
        ("Tools", "⚙", "Tools"),
    ];

    public static ControlTemplate BareTemplate()
    {
        var t = new ControlTemplate(typeof(Button)) { VisualTree = new FrameworkElementFactory(typeof(ContentPresenter)) };
        t.Seal();
        return t;
    }

    public static TextBlock Disp(string text, double size, Color? color = null, FontWeight? weight = null) => new()
    {
        Text = text,
        FontFamily = DispFont,
        FontSize = size,
        FontWeight = weight ?? FontWeights.SemiBold,
        Foreground = PulseInk.B(color ?? TextPrimary),
        TextTrimming = TextTrimming.CharacterEllipsis
    };

    public static TextBlock Body(string text, double size, Color? color = null, FontWeight? weight = null, bool wrap = false) => new()
    {
        Text = text,
        FontFamily = BodyFont,
        FontSize = size,
        FontWeight = weight ?? FontWeights.Normal,
        Foreground = PulseInk.B(color ?? TextSoft),
        TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
        TextTrimming = wrap ? TextTrimming.None : TextTrimming.CharacterEllipsis,
        LineHeight = wrap ? size * 1.42 : double.NaN
    };

    public static TextBlock Mono(string text, double size, Color? color = null, FontWeight? weight = null) => new()
    {
        Text = text,
        FontFamily = MonoFont,
        FontSize = size,
        FontWeight = weight ?? FontWeights.SemiBold,
        Foreground = PulseInk.B(color ?? CyanBright),
        TextTrimming = TextTrimming.CharacterEllipsis
    };

    public static TextBlock Kicker(string text, bool onPearl = false, Color? overrideColor = null) => new()
    {
        Text = text.ToUpperInvariant(),
        FontFamily = MonoFont,
        FontSize = 11,
        FontWeight = FontWeights.SemiBold,
        Foreground = PulseInk.B(overrideColor ?? (onPearl ? PulseInk.C("#3558b8") : CyanBright))
    };

    /// <summary>Creates the 1600×900 Pulse acoustic void canvas with concentric resonance field rings.</summary>
    public static Canvas StageCanvas(bool hero = false)
    {
        var c = PulseKit.VoidBackdrop(hero);
        // Add the concentric ring-field from t_pulse.py centered around (800, 440)
        double cx = 800, cy = 440;
        foreach (double r in new[] { 150.0, 260.0, 380.0, 520.0, 680.0 })
        {
            var ring = new Ellipse
            {
                Width = r * 2,
                Height = r * 2,
                Stroke = PulseInk.B(PulseInk.A(PulseInk.C("#8cc8ff"), r <= 260 ? 0.12 : 0.07)),
                StrokeThickness = 1,
                IsHitTestVisible = false
            };
            if (r is 260 or 520) ring.StrokeDashArray = new DoubleCollection([3, 7]);
            Canvas.SetLeft(ring, cx - r);
            Canvas.SetTop(ring, cy - r);
            c.Children.Add(ring);
        }
        return c;
    }

    /// <summary>Concentric Pulse logo mark for headers, dashboard, and about view.</summary>
    public static Canvas LogoMark(double width = 86)
    {
        double height = width;
        var c = new Canvas { Width = width, Height = height };
        double r = width / 2;
        var outer = new Ellipse
        {
            Width = width - 4,
            Height = height - 4,
            Stroke = new LinearGradientBrush(PulseInk.C("#78b4ff"), PulseInk.C("#c8dcff"), 45),
            StrokeThickness = 2.2,
            Effect = new DropShadowEffect { Color = PulseInk.C("#78aaff"), BlurRadius = 14, ShadowDepth = 0, Opacity = 0.55 }
        };
        Canvas.SetLeft(outer, 2); Canvas.SetTop(outer, 2);

        var mid = new Ellipse
        {
            Width = width * 0.58,
            Height = height * 0.58,
            Stroke = new LinearGradientBrush(PulseInk.C("#a8ccff"), Colors.White, 45),
            StrokeThickness = 1.8,
            Opacity = 0.85
        };
        Canvas.SetLeft(mid, r - width * 0.29); Canvas.SetTop(mid, r - width * 0.29);

        var core = new Ellipse
        {
            Width = width * 0.22,
            Height = height * 0.22,
            Fill = PulseInk.B(Colors.White),
            Effect = new DropShadowEffect { Color = PulseInk.C("#a8ccff"), BlurRadius = 12, ShadowDepth = 0, Opacity = 0.90 }
        };
        Canvas.SetLeft(core, r - width * 0.11); Canvas.SetTop(core, r - width * 0.11);

        c.Children.Add(outer); c.Children.Add(mid); c.Children.Add(core);
        return c;
    }

    /// <summary>Pulse key badge (`.k` / `.k.f` in `t_pulse.py:48`).</summary>
    public static Border KeyBadge(string key, bool focused = false) => new()
    {
        MinWidth = 28,
        Height = 28,
        CornerRadius = new(14),
        Padding = new(8, 0, 8, 0),
        Background = focused ? PulseInk.B(PulseInk.C("#eef2f8")) : PulseInk.B(PulseInk.A(Colors.White, 0.10)),
        BorderBrush = PulseInk.B(PulseInk.A(PulseInk.C("#eef2f8"), focused ? 0.90 : 0.70)),
        BorderThickness = new(1.5),
        Effect = focused ? new DropShadowEffect { Color = PulseInk.C("#a8ccff"), BlurRadius = 16, ShadowDepth = 0, Opacity = 0.60 } : null,
        Child = Mono(key, 13, focused ? Color.FromRgb(10, 16, 32) : Color.FromRgb(238, 242, 248), FontWeights.Bold).Also(t => { t.HorizontalAlignment = HorizontalAlignment.Center; t.VerticalAlignment = VerticalAlignment.Center; })
    };

    /// <summary>Pearl orb key badge for the bottom rising pearl card (`t_pulse.py:467`).</summary>
    public static Border PearlKeyBadge(string key, double size = 62, double fontSize = 26) => new()
    {
        Width = size,
        Height = size,
        CornerRadius = new(size / 2),
        Background = new LinearGradientBrush(Colors.White, Color.FromRgb(221, 229, 241), 90),
        BorderBrush = PulseInk.B(PulseInk.A(Colors.White, 0.85)),
        BorderThickness = new(1.5),
        Effect = new DropShadowEffect { Color = PulseInk.C("#a8ccff"), BlurRadius = 24, ShadowDepth = 0, Opacity = 0.55 },
        Child = Mono(key, fontSize, Color.FromRgb(10, 16, 32), FontWeights.Bold).Also(t => { t.HorizontalAlignment = HorizontalAlignment.Center; t.VerticalAlignment = VerticalAlignment.Center; })
    };

    /// <summary>Animated wave with arrow for the bottom pearl card (`t_pulse.py:468`).</summary>
    public static Canvas WaveArrow(double width = 110, double height = 30)
    {
        var c = new Canvas { Width = width, Height = height, ClipToBounds = false };
        var pts = new PointCollection();
        double baseLine = height / 2;
        for (int x = 0; x <= 98; x += 2)
        {
            double env = 9 * Math.Exp(-Math.Pow((x - 52) / 20.0, 2));
            double y = baseLine + env * Math.Sin(x * 0.12);
            pts.Add(new Point(x, y));
        }
        var line = new Polyline
        {
            Points = pts,
            Stroke = PulseInk.B(PulseInk.C("#a8ccff")),
            StrokeThickness = 2,
            Effect = new DropShadowEffect { Color = PulseInk.C("#a8ccff"), BlurRadius = 6, ShadowDepth = 0, Opacity = 0.6 }
        };
        c.Children.Add(line);
        var arrow = new Polyline
        {
            Points = new PointCollection([new Point(96, baseLine - 6), new Point(106, baseLine), new Point(96, baseLine + 6)]),
            Stroke = PulseInk.B(PulseInk.C("#a8ccff")),
            StrokeThickness = 2,
            StrokeLineJoin = PenLineJoin.Round
        };
        c.Children.Add(arrow);
        return c;
    }

    /// <summary>Circular dial widget for telemetry and status rows (`dial` in `t_pulse.py:186`).</summary>
    public static Canvas DialMini(double pct, double diameter = 30, double strokeWidth = 2.6, Color? accent = null)
    {
        var c = new Canvas { Width = diameter, Height = diameter };
        double r = diameter / 2 - strokeWidth;
        double cx = diameter / 2, cy = diameter / 2;
        var track = CreateArcPath(cx, cy, r, 135, 405, Color.FromArgb(36, 255, 255, 255), strokeWidth);
        c.Children.Add(track);
        if (pct > 0.001)
        {
            double sweep = 270.0 * Math.Clamp(pct, 0.0, 1.0);
            var activeArc = CreateArcPath(cx, cy, r, 135, 135 + sweep, accent ?? PulseParts.CyanBright, strokeWidth);
            activeArc.Effect = new DropShadowEffect { Color = accent ?? PulseParts.CyanBright, BlurRadius = 6, ShadowDepth = 0, Opacity = 0.8 };
            c.Children.Add(activeArc);

            double knobRad = (135 + sweep) * Math.PI / 180.0;
            double kx = cx + r * Math.Cos(knobRad);
            double ky = cy + r * Math.Sin(knobRad);
            var knob = new Ellipse
            {
                Width = strokeWidth * 2.3,
                Height = strokeWidth * 2.3,
                Fill = Brushes.White
            };
            Canvas.SetLeft(knob, kx - strokeWidth * 1.15);
            Canvas.SetTop(knob, ky - strokeWidth * 1.15);
            c.Children.Add(knob);
        }
        return c;
    }

    /// <summary>Resonance mark (`RM` in `t_pulse.py:239`).</summary>
    public static Canvas ResonanceMark()
    {
        var c = new Canvas { Width = 14, Height = 14, VerticalAlignment = VerticalAlignment.Center, Margin = new(0, 0, 10, 0) };
        var dot = new Ellipse
        {
            Width = 6.4,
            Height = 6.4,
            Fill = Brushes.White,
            Effect = new DropShadowEffect { Color = PulseInk.C("#a8ccff"), BlurRadius = 4, ShadowDepth = 0, Opacity = 0.8 }
        };
        Canvas.SetLeft(dot, 3.8); Canvas.SetTop(dot, 3.8);
        var leftArc = new Path
        {
            Stroke = PulseInk.B(PulseInk.C("#a8ccff")),
            StrokeThickness = 1.4,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            Data = Geometry.Parse("M 1.6 2.4 A 6.6 6.6 0 0 0 1.6 11.6")
        };
        var rightArc = new Path
        {
            Stroke = PulseInk.B(PulseInk.C("#a8ccff")),
            StrokeThickness = 1.4,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            Data = Geometry.Parse("M 12.4 2.4 A 6.6 6.6 0 0 1 12.4 11.6")
        };
        c.Children.Add(dot);
        c.Children.Add(leftArc);
        c.Children.Add(rightArc);
        return c;
    }

    /// <summary>Resonance mark around radius r.</summary>
    public static Canvas ResonanceMark(double r) => PulseKit.Resonance(r, r, r, pairs: 2, span: 26, op: 0.85);

    /// <summary>Tab button with pearl dot when active (`tabs` in `t_pulse.py:76-78`).</summary>
    public static Button TabButton(string label, bool active, Action onClick)
    {
        var stack = new StackPanel { Orientation = Orientation.Vertical, Margin = new(0, 0, 34, 0) };
        var text = Disp(label, 18, active ? Colors.White : Color.FromRgb(127, 138, 160), active ? FontWeights.SemiBold : FontWeights.Normal);
        stack.Children.Add(text);
        if (active)
        {
            var dot = new Ellipse
            {
                Width = 6,
                Height = 6,
                Fill = PulseInk.B(PulseInk.C("#eef2f8")),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new(0, 6, 0, 0),
                Effect = new DropShadowEffect { Color = PulseInk.C("#a8ccff"), BlurRadius = 10, ShadowDepth = 0, Opacity = 0.8 }
            };
            stack.Children.Add(dot);
        }
        else
        {
            stack.Children.Add(new Rectangle { Height = 12 });
        }
        var btn = new Button
        {
            Content = stack,
            Cursor = Cursors.Hand,
            FocusVisualStyle = null,
            Template = BareTemplate()
        };
        System.Windows.Automation.AutomationProperties.SetName(btn, label);
        btn.Click += (_, _) => onClick();
        return btn;
    }

    /// <summary>Draws an elliptical arc between startAngle and endAngle.</summary>
    public static Path CreateEllipseArc(double ox, double oy, double rx, double ry, double startAngle, double endAngle, Color strokeColor, double strokeThickness)
    {
        double startRad = startAngle * Math.PI / 180.0;
        double endRad = endAngle * Math.PI / 180.0;
        double x0 = ox + rx * Math.Cos(startRad);
        double y0 = oy + ry * Math.Sin(startRad);
        double x1 = ox + rx * Math.Cos(endRad);
        double y1 = oy + ry * Math.Sin(endRad);

        double sweep = endAngle - startAngle;
        bool isLarge = Math.Abs(sweep) > 180;
        SweepDirection dir = sweep > 0 ? SweepDirection.Clockwise : SweepDirection.Counterclockwise;

        var geom = new PathGeometry
        {
            Figures =
            {
                new PathFigure
                {
                    StartPoint = new Point(x0, y0),
                    Segments = { new ArcSegment(new Point(x1, y1), new Size(rx, ry), 0, isLarge, dir, true) }
                }
            }
        };
        return new Path
        {
            Data = geom,
            Stroke = PulseInk.B(strokeColor),
            StrokeThickness = strokeThickness,
            Fill = Brushes.Transparent
        };
    }

    /// <summary>Small concentric Pulse logo mark for the top header bar.</summary>
    public static Canvas LogoSmall(double size = 30)
    {
        var c = new Canvas { Width = size, Height = size };
        double r = size / 2;
        var outer = new Ellipse
        {
            Width = size - 2,
            Height = size - 2,
            Stroke = new LinearGradientBrush(CyanColor, VioletColor, 45),
            StrokeThickness = 1.6
        };
        Canvas.SetLeft(outer, 1); Canvas.SetTop(outer, 1);
        var mid = new Ellipse
        {
            Width = size * 0.56,
            Height = size * 0.56,
            Stroke = new LinearGradientBrush(CyanBright, MintColor, 45),
            StrokeThickness = 1.3,
            Opacity = 0.78
        };
        Canvas.SetLeft(mid, r - size * 0.28); Canvas.SetTop(mid, r - size * 0.28);
        var core = new Ellipse
        {
            Width = 7,
            Height = 7,
            Fill = PulseInk.B(PulseInk.C("#9be8ff")),
            Effect = new DropShadowEffect { Color = CyanColor, BlurRadius = 8, ShadowDepth = 0, Opacity = 0.9 }
        };
        Canvas.SetLeft(core, r - 3.5); Canvas.SetTop(core, r - 3.5);
        c.Children.Add(outer); c.Children.Add(mid); c.Children.Add(core);
        return c;
    }

    /// <summary>Top status bar (`x=124, y=22, w=1440, h=44`) with breadcrumb, telemetry pill, and Zone/Back button.</summary>
    public static DockPanel TopHeader(string crumb, string statusText, string backLabel, Action onBack, out TextBlock crumbBlock, out TextBlock statusBlock, out Button backButton, bool showBreadcrumbs = true)
    {
        var bar = new DockPanel { Width = 1440, Height = 44, LastChildFill = false };

        var left = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        if (!showBreadcrumbs) left.Visibility = Visibility.Collapsed;
        left.Children.Add(LogoSmall(30));
        left.Children.Add(Disp("X20CTL", 15, TextPrimary, FontWeights.Bold).Also(t => t.Margin = new(12, 0, 10, 0)));
        left.Children.Add(Body("/", 14, PulseInk.A(TextPrimary, 0.28)).Also(t => t.Margin = new(0, 0, 10, 0)));
        left.Children.Add(Mono("PULSE", 12, CyanBright, FontWeights.SemiBold).Also(t => t.Margin = new(0, 0, 10, 0)));
        left.Children.Add(Body("/", 14, PulseInk.A(TextPrimary, 0.28)).Also(t => t.Margin = new(0, 0, 10, 0)));
        crumbBlock = Disp(crumb, 14, PulseInk.C("#dfe8ff"), FontWeights.SemiBold);
        crumbBlock.VerticalAlignment = VerticalAlignment.Center;
        left.Children.Add(crumbBlock);
        DockPanel.SetDock(left, Dock.Left);
        bar.Children.Add(left);

        var right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var dot = new Ellipse
        {
            Width = 8,
            Height = 8,
            Fill = PulseInk.B(MintColor),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new(0, 0, 9, 0),
            Effect = new DropShadowEffect { Color = MintColor, BlurRadius = 10, ShadowDepth = 0, Opacity = 0.9 }
        };
        statusBlock = Body(statusText, 12.5, TextPrimary, FontWeights.Medium);
        statusBlock.VerticalAlignment = VerticalAlignment.Center;
        var pill = new Border
        {
            CornerRadius = new(16),
            Padding = new(14, 7, 15, 7),
            Margin = new(0, 0, 10, 0),
            Background = PulseInk.B(PulseInk.A(Colors.White, 0.08)),
            BorderBrush = PulseInk.B(PulseInk.A(PulseInk.C("#bedcff"), 0.22)),
            BorderThickness = new(1),
            Child = new StackPanel { Orientation = Orientation.Horizontal, Children = { dot, statusBlock } }
        };
        right.Children.Add(pill);

        backButton = PillButton(backLabel, primary: false, onPearl: false, small: true, onClick: onBack);
        right.Children.Add(backButton);
        DockPanel.SetDock(right, Dock.Right);
        bar.Children.Add(right);
        return bar;
    }

    /// <summary>Bottom hint bar (`x=124, y=850, w=1440, h=34`) with harmonic caption and controller glyph hints.</summary>
    public static DockPanel BottomFooter((string Key, string Label)[] hints, out StackPanel hintsHost, string leftCaption = "WAVE FIELD · HARMONIC UI · LOCAL DRAFTS NEVER TOUCH FIRMWARE")
    {
        var foot = new DockPanel { Width = 1440, Height = 34, LastChildFill = false };
        var left = Mono(leftCaption, 11, TextSoft, FontWeights.Medium);
        left.VerticalAlignment = VerticalAlignment.Center;
        DockPanel.SetDock(left, Dock.Left);
        foot.Children.Add(left);

        hintsHost = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        PopulateFooterHints(hintsHost, hints);
        DockPanel.SetDock(hintsHost, Dock.Right);
        foot.Children.Add(hintsHost);
        return foot;
    }

    public static void PopulateFooterHints(Panel host, IEnumerable<(string Key, string Label)> hints)
    {
        host.Children.Clear();
        foreach (var (key, label) in hints)
        {
            var item = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(16, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            var k = new Border
            {
                MinWidth = 22,
                Height = 22,
                CornerRadius = new(11),
                Padding = new(6, 0, 6, 0),
                Margin = new(0, 0, 6, 0),
                Background = PulseInk.B(PulseInk.A(Colors.White, 0.12)),
                BorderBrush = PulseInk.B(PulseInk.A(Colors.White, 0.24)),
                BorderThickness = new(1),
                Child = Mono(key, 11, TextPrimary, FontWeights.Bold).Also(t => { t.HorizontalAlignment = HorizontalAlignment.Center; t.VerticalAlignment = VerticalAlignment.Center; })
            };
            item.Children.Add(k);
            item.Children.Add(Body(label, 12, TextSoft, FontWeights.Medium).Also(t => t.VerticalAlignment = VerticalAlignment.Center));
            host.Children.Add(item);
        }
    }

    /// <summary>Studio pages orbital navigation arc down the left edge (`t_pulse.py:295`).</summary>
    public static FrameworkElement LeftOrbitNav(string activeKey, Action<string> onSelect, out Dictionary<string, Button> buttons)
    {
        var map = new Dictionary<string, Button>(StringComparer.OrdinalIgnoreCase);
        var canvas = new Canvas { Width = 260, Height = 900, ClipToBounds = false };

        // 1. Logo at top left (28, 22)
        var logo = LogoSmall(36);
        Canvas.SetLeft(logo, 28);
        Canvas.SetTop(logo, 22);
        canvas.Children.Add(logo);

        // 2. Arc path down left: NAV_C = (-560, 450), NAV_R = 700
        double cX = -560, cY = 450, R = 700;
        double yTop = 100, yBot = 800;
        double dyTop = yTop - cY, dyBot = yBot - cY;
        double xTop = cX + Math.Sqrt(Math.Max(0, R * R - dyTop * dyTop));
        double xBot = cX + Math.Sqrt(Math.Max(0, R * R - dyBot * dyBot));

        var arcGeom = new PathGeometry();
        var fig = new PathFigure { StartPoint = new Point(xTop, yTop) };
        fig.Segments.Add(new ArcSegment(new Point(xBot, yBot), new Size(R, R), 0, false, SweepDirection.Clockwise, true));
        arcGeom.Figures.Add(fig);

        var arcPath = new Path
        {
            Data = arcGeom,
            Stroke = new LinearGradientBrush
            {
                StartPoint = new(0, 0), EndPoint = new(0, 1),
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(0, 168, 204, 255), 0.0),
                    new GradientStop(Color.FromArgb(120, 168, 204, 255), 0.5),
                    new GradientStop(Color.FromArgb(0, 168, 204, 255), 1.0)
                }
            },
            StrokeThickness = 1.4
        };
        canvas.Children.Add(arcPath);

        // 3. Orbs along the arc
        var pages = new (string Key, string Icon, string Label)[]
        {
            ("Buttons", "◉", "Buttons"),
            ("Curves", "∿", "Curves"),
            ("Macros", "⟡", "Macros"),
            ("Vibration", "≋", "Vibration"),
            ("PcMode", "⊞", "PC mode"),
            ("Device", "◈", "Device"),
            ("Mouse", "⬡", "Mouse"),
            ("Setups", "▤", "Setups"),
            ("Tools", "⚙", "Tools")
        };

        for (int i = 0; i < pages.Length; i++)
        {
            var (key, icon, label) = pages[i];
            double y = 150 + i * 75.0;
            double dy = y - cY;
            double x = cX + Math.Sqrt(Math.Max(0, R * R - dy * dy));

            bool active = string.Equals(key, activeKey, StringComparison.OrdinalIgnoreCase);
            var btn = CreateNavOrbButton(key, icon, label, active, () => onSelect(key));
            map[key] = btn;

            Canvas.SetLeft(btn, x - 26);
            Canvas.SetTop(btn, y - 26);
            canvas.Children.Add(btn);
        }

        buttons = map;
        return canvas;
    }

    public static void StyleOrbButton(Button btn, bool active)
    {
        if (btn.Content is not Grid root || root.Children.Count < 3) return;
        var halo = (Ellipse)root.Children[0];
        var core = (Border)root.Children[1];
        var lbl = (TextBlock)root.Children[2];
        var glyph = (TextBlock)core.Child;

        halo.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
        lbl.Visibility = active ? Visibility.Visible : Visibility.Collapsed;

        core.Width = active ? 52 : 40;
        core.Height = active ? 52 : 40;
        core.CornerRadius = new(active ? 26 : 20);

        core.Background = active
            ? new LinearGradientBrush(Colors.White, Color.FromRgb(221, 229, 241), 90)
            : PulseInk.B(PulseInk.A(Colors.White, 0.05));

        core.BorderBrush = active
            ? PulseInk.B(PulseInk.A(Colors.White, 0.85))
            : PulseInk.B(PulseInk.A(PulseInk.C("#bedcff"), 0.22));

        core.Effect = active
            ? new DropShadowEffect { Color = PulseInk.C("#a8ccff"), BlurRadius = 28, ShadowDepth = 0, Opacity = 0.55 }
            : null;

        glyph.FontSize = active ? 20 : 16;
        glyph.Foreground = PulseInk.B(active ? Color.FromRgb(10, 16, 32) : Color.FromRgb(195, 204, 218));
    }

    private static Button CreateNavOrbButton(string key, string icon, string label, bool active, Action onClick)
    {
        var halo = new Ellipse
        {
            Width = 64,
            Height = 64,
            Stroke = PulseInk.B(PulseInk.A(PulseInk.C("#a8ccff"), 0.50)),
            StrokeThickness = 1.2,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = active ? Visibility.Visible : Visibility.Collapsed
        };
        var glyph = new TextBlock
        {
            Text = icon,
            FontFamily = DispFont,
            FontSize = active ? 20 : 16,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var core = new Border
        {
            Width = active ? 52 : 40,
            Height = active ? 52 : 40,
            CornerRadius = new(active ? 26 : 20),
            BorderThickness = new(1),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = glyph
        };
        var nameLabel = new TextBlock
        {
            Text = label,
            FontFamily = DispFont,
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new(146, 0, 0, 0),
            Effect = new DropShadowEffect { Color = PulseInk.C("#a8ccff"), BlurRadius = 12, ShadowDepth = 0, Opacity = 0.65 },
            Visibility = active ? Visibility.Visible : Visibility.Collapsed
        };
        var grid = new Grid
        {
            Width = 220,
            Height = 64,
            ClipToBounds = false,
            Children = { halo, core, nameLabel }
        };
        var btn = new Button
        {
            Content = grid,
            Tag = key,
            ToolTip = label,
            Cursor = Cursors.Hand,
            FocusVisualStyle = null,
            Template = BareTemplate()
        };
        System.Windows.Automation.AutomationProperties.SetName(btn, label);
        StyleOrbButton(btn, active);
        btn.Click += (_, _) => onClick();
        return btn;
    }

    /// <summary>Frosted harmonic glass card (`.glass` in `t_pulse.py`).</summary>
    public static Border GlassCard(UIElement child, Thickness? padding = null, double radius = 26, bool active = false) => new()
    {
        CornerRadius = new(radius),
        Padding = padding ?? new Thickness(22),
        Background = active
            ? new LinearGradientBrush(PulseInk.A(Colors.White, 0.15), PulseInk.A(Colors.White, 0.05), 90)
            : new LinearGradientBrush(PulseInk.A(Colors.White, 0.10), PulseInk.A(PulseInk.C("#8cb4ff"), 0.04), 35),
        BorderBrush = PulseInk.B(active ? PulseInk.A(PulseInk.C("#eef2f8"), 0.85) : PulseInk.A(PulseInk.C("#bedcff"), 0.18)),
        BorderThickness = new(1),
        Effect = new DropShadowEffect
        {
            Color = active ? PulseInk.C("#a8ccff") : Colors.Black,
            BlurRadius = active ? 24 : 30,
            ShadowDepth = active ? 0 : 10,
            Opacity = active ? 0.35 : 0.42
        },
        Child = child
    };

    /// <summary>Rising pearl glass bottom card (`.pearl` in `t_pulse.py`).</summary>
    public static Border RisingPearlCard(UIElement child, Thickness? padding = null, double topRadius = 30, double bottomRadius = 24)
    {
        var bg = new LinearGradientBrush { StartPoint = new(0, 0), EndPoint = new(0, 1) };
        bg.GradientStops.Add(new GradientStop(Color.FromArgb(42, 236, 242, 252), 0.0));
        bg.GradientStops.Add(new GradientStop(Color.FromArgb(20, 190, 206, 232), 0.45));
        bg.GradientStops.Add(new GradientStop(Color.FromArgb(14, 150, 170, 210), 1.0));
        return new Border
        {
            CornerRadius = new CornerRadius(topRadius, topRadius, bottomRadius, bottomRadius),
            Padding = padding ?? new Thickness(28, 24, 28, 24),
            Background = bg,
            BorderBrush = new LinearGradientBrush(
                new GradientStopCollection
                {
                    new(Color.FromArgb(200, 255, 255, 255), 0.0),
                    new(Color.FromArgb(50, 220, 232, 252), 0.5),
                    new(Color.FromArgb(25, 150, 170, 210), 1.0)
                },
                new Point(0, 0),
                new Point(0, 1)),
            BorderThickness = new(1),
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 40, ShadowDepth = 12, Opacity = 0.55 },
            Child = child
        };
    }

    /// <summary>Interactive glass row (`.row` or `.row.c` in `t_pulse.py`).</summary>
    public static Border GlassRow(UIElement left, UIElement? right = null, bool active = false, bool compact = false, Action? onClick = null)
    {
        var dock = new DockPanel { LastChildFill = true };
        if (right != null)
        {
            DockPanel.SetDock(right, Dock.Right);
            dock.Children.Add(right);
        }
        dock.Children.Add(left);

        var border = new Border
        {
            CornerRadius = new(compact ? 16 : 20),
            Padding = compact ? new Thickness(16, 10, 16, 10) : new Thickness(20, 14, 20, 14),
            Margin = new(0, 0, 0, compact ? 8 : 10),
            Background = active
                ? new LinearGradientBrush(PulseInk.A(Colors.White, 0.15), PulseInk.A(Colors.White, 0.05), 90)
                : new LinearGradientBrush(PulseInk.A(Colors.White, 0.10), PulseInk.A(PulseInk.C("#8cb4ff"), 0.04), 35),
            BorderBrush = PulseInk.B(active ? PulseInk.A(PulseInk.C("#eef2f8"), 0.85) : PulseInk.A(PulseInk.C("#bedcff"), 0.18)),
            BorderThickness = new(1),
            Effect = active ? new DropShadowEffect { Color = PulseInk.C("#a8ccff"), BlurRadius = 24, ShadowDepth = 0, Opacity = 0.35 } : null,
            Child = dock
        };
        if (onClick != null)
        {
            border.Cursor = Cursors.Hand;
            border.MouseLeftButtonDown += (_, e) => { onClick(); e.Handled = true; };
        }
        return border;
    }

    /// <summary>Creates a focusable Button styled as a Pulse `.row` or `.row.c`.</summary>
    public static Button RowButton(string name, string? desc, UIElement? right, bool active = false, bool compact = false, Action? onClick = null)
    {
        var left = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        left.Children.Add(Disp(name, compact ? 14 : 15, TextPrimary, FontWeights.SemiBold));
        if (!string.IsNullOrEmpty(desc))
            left.Children.Add(Body(desc, 12, TextSoft).Also(t => t.Margin = new(0, 2, 0, 0)));

        var row = GlassRow(left, right, active, compact);
        var btn = new Button
        {
            Content = row,
            Cursor = Cursors.Hand,
            FocusVisualStyle = null,
            Template = BareTemplate(),
            HorizontalContentAlignment = HorizontalAlignment.Stretch
        };
        System.Windows.Automation.AutomationProperties.SetName(btn, name);
        if (onClick != null) btn.Click += (_, _) => onClick();
        btn.MouseEnter += (_, _) => { if (!active) row.BorderBrush = PulseInk.B(PulseInk.A(CyanBright, 0.55)); };
        btn.MouseLeave += (_, _) => { if (!active) row.BorderBrush = PulseInk.B(PulseInk.A(PulseInk.C("#bedcff"), 0.18)); };
        btn.GotKeyboardFocus += (_, _) => row.BorderBrush = PulseInk.B(Colors.White);
        btn.LostKeyboardFocus += (_, _) => row.BorderBrush = PulseInk.B(active ? PulseInk.A(CyanBright, 0.68) : PulseInk.A(PulseInk.C("#bedcff"), 0.18));
        return btn;
    }

    /// <summary>Pill action button (`.bt`, `.bt.p`, `.btn.danger` in `t_pulse.py`).
    /// Primary uses an alabaster pearl white gradient with deep ink text and soft ice-blue halo (no neon purple).
    /// Secondary uses frosted glass with ice-tinted border.</summary>
    public static Button PillButton(string text, bool primary = false, bool onPearl = false, bool danger = false, bool small = false, Action? onClick = null)
    {
        Brush bg = primary
            ? new LinearGradientBrush(Colors.White, Color.FromRgb(221, 229, 241), 90)
            : danger
                ? PulseInk.B(PulseInk.A(RoseColor, 0.16))
                : onPearl
                    ? PulseInk.B(PulseInk.A(Colors.White, 0.10))
                    : PulseInk.B(PulseInk.A(Colors.White, 0.08));

        Brush border = primary
            ? PulseInk.B(PulseInk.A(Colors.White, 0.75))
            : danger
                ? PulseInk.B(PulseInk.A(RoseColor, 0.45))
                : onPearl
                    ? PulseInk.B(PulseInk.A(PulseInk.C("#c8d7f0"), 0.28))
                    : PulseInk.B(PulseInk.A(PulseInk.C("#c8d7f0"), 0.22));

        Color fg = primary
            ? Color.FromRgb(10, 16, 32)
            : danger
                ? RoseColor
                : onPearl
                    ? Color.FromRgb(238, 242, 248)
                    : TextPrimary;

        var face = new Border
        {
            CornerRadius = new(small ? 16 : 22),
            Padding = small ? new Thickness(16, 7, 16, 7) : new Thickness(24, 11, 24, 11),
            Background = bg,
            BorderBrush = border,
            BorderThickness = new(1),
            Effect = primary ? new DropShadowEffect { Color = PulseInk.C("#a8ccff"), BlurRadius = 22, ShadowDepth = 0, Opacity = 0.40 } : null,
            Child = Body(text, small ? 12.5 : 14.5, fg, primary ? FontWeights.SemiBold : FontWeights.Normal).Also(t => { t.HorizontalAlignment = HorizontalAlignment.Center; t.VerticalAlignment = VerticalAlignment.Center; })
        };
        var btn = new Button
        {
            Content = face,
            Cursor = Cursors.Hand,
            FocusVisualStyle = null,
            Template = BareTemplate()
        };
        System.Windows.Automation.AutomationProperties.SetName(btn, text);
        if (onClick != null) btn.Click += (_, _) => onClick();
        btn.MouseEnter += (_, _) => face.BorderBrush = PulseInk.B(Colors.White);
        btn.MouseLeave += (_, _) => face.BorderBrush = border;
        btn.GotKeyboardFocus += (_, _) => face.BorderBrush = PulseInk.B(Colors.White);
        btn.LostKeyboardFocus += (_, _) => face.BorderBrush = border;
        return btn;
    }

    /// <summary>Status or filter chip (`.ch`, `.ch.on` in `t_pulse.py`).</summary>
    public static Border Chip(string text, string tone = "default", bool onPearl = false)
    {
        Color fg = tone switch
        {
            "on" => Color.FromRgb(10, 16, 32),
            "ok" => MintColor,
            "warn" => AmberColor,
            "rose" => RoseColor,
            _ => onPearl ? Color.FromRgb(223, 229, 238) : TextSoft
        };
        Brush bg = tone switch
        {
            "on" => PulseInk.B(PulseInk.C("#eef2f8")),
            "ok" => PulseInk.B(PulseInk.A(MintColor, 0.14)),
            "warn" => PulseInk.B(PulseInk.A(AmberColor, 0.14)),
            "rose" => PulseInk.B(PulseInk.A(RoseColor, 0.15)),
            _ => PulseInk.B(PulseInk.A(Colors.White, 0.07))
        };
        Brush br = tone switch
        {
            "on" => PulseInk.B(PulseInk.A(Colors.White, 0.85)),
            "ok" => PulseInk.B(PulseInk.A(MintColor, 0.42)),
            "warn" => PulseInk.B(PulseInk.A(AmberColor, 0.42)),
            "rose" => PulseInk.B(PulseInk.A(RoseColor, 0.42)),
            _ => PulseInk.B(PulseInk.A(PulseInk.C("#c8d7f0"), 0.18))
        };

        return new Border
        {
            CornerRadius = new(16),
            Padding = new(14, 5, 14, 5),
            Background = bg,
            BorderBrush = br,
            BorderThickness = new(1),
            VerticalAlignment = VerticalAlignment.Center,
            Effect = tone == "on" ? new DropShadowEffect { Color = PulseInk.C("#a8ccff"), BlurRadius = 16, ShadowDepth = 0, Opacity = 0.45 } : null,
            Child = Body(text, 12.5, fg, tone == "on" ? FontWeights.SemiBold : FontWeights.Normal)
        };
    }

    /// <summary>Interactive chip button for presets, tabs, or channel selection.</summary>
    public static Button ChipButton(string text, bool active, Action onClick, bool onPearl = false)
    {
        var chip = Chip(text, active ? "on" : "default", onPearl);
        var btn = new Button
        {
            Content = chip,
            Cursor = Cursors.Hand,
            FocusVisualStyle = null,
            Template = BareTemplate(),
            Margin = new(0, 0, 6, 0)
        };
        System.Windows.Automation.AutomationProperties.SetName(btn, text);
        btn.Click += (_, _) => onClick();
        return btn;
    }

    /// <summary>Visual toggle pill (`toggle` in `t_pulse.py`) that can wrap or sync with a real CheckBox.</summary>
    public static Border VisualToggle(bool isOn, Action? onToggle = null)
    {
        var knob = new Ellipse
        {
            Width = 22,
            Height = 22,
            Fill = Brushes.White,
            HorizontalAlignment = isOn ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 6, ShadowDepth = 1, Opacity = 0.35 }
        };
        var pill = new Border
        {
            Width = 50,
            Height = 28,
            CornerRadius = new(14),
            Padding = new(3, 0, 3, 0),
            Background = isOn
                ? new LinearGradientBrush(PulseInk.C("#3f7fe0"), PulseInk.C("#8cbcff"), 90)
                : PulseInk.B(PulseInk.A(Colors.White, 0.14)),
            BorderBrush = PulseInk.B(isOn ? PulseInk.A(Colors.White, 0.85) : PulseInk.A(Colors.White, 0.24)),
            BorderThickness = new(1),
            VerticalAlignment = VerticalAlignment.Center,
            Child = knob
        };
        if (onToggle != null)
        {
            pill.Cursor = Cursors.Hand;
            pill.MouseLeftButtonDown += (_, e) => { onToggle(); e.Handled = true; };
        }
        return pill;
    }

    /// <summary>Concentric circular progress dial (`ring_dial` in `t_pulse.py`).</summary>
    public static Grid RingDial(double pct, string label, string sub, double size = 180, Color? accent = null)
    {
        Color col = accent ?? CyanColor;
        double r = size / 2 - 14;
        double cx = size / 2, cy = size / 2;
        var canvas = new Canvas { Width = size, Height = size };

        var track = new Ellipse
        {
            Width = r * 2,
            Height = r * 2,
            Stroke = PulseInk.B(PulseInk.A(Colors.White, 0.10)),
            StrokeThickness = 8
        };
        Canvas.SetLeft(track, cx - r);
        Canvas.SetTop(track, cy - r);
        canvas.Children.Add(track);

        var innerDashed = new Ellipse
        {
            Width = (r - 14) * 2,
            Height = (r - 14) * 2,
            Stroke = PulseInk.B(PulseInk.A(col, 0.24)),
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection([3, 5])
        };
        Canvas.SetLeft(innerDashed, cx - (r - 14));
        Canvas.SetTop(innerDashed, cy - (r - 14));
        canvas.Children.Add(innerDashed);

        double clamped = Math.Clamp(pct, 0.0, 0.999);
        if (clamped > 0.005)
        {
            var arc = CreateArcPath(cx, cy, r, -90, -90 + clamped * 360, col, 8);
            canvas.Children.Add(arc);

            double endRad = (-90 + clamped * 360) * Math.PI / 180.0;
            double ex = cx + r * Math.Cos(endRad);
            double ey = cy + r * Math.Sin(endRad);
            var dot = new Ellipse
            {
                Width = 11,
                Height = 11,
                Fill = Brushes.White,
                Stroke = PulseInk.B(col),
                StrokeThickness = 2,
                Effect = new DropShadowEffect { Color = col, BlurRadius = 10, ShadowDepth = 0, Opacity = 0.9 }
            };
            Canvas.SetLeft(dot, ex - 5.5);
            Canvas.SetTop(dot, ey - 5.5);
            canvas.Children.Add(dot);
        }

        var center = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        center.Children.Add(Mono(label, size > 130 ? 25 : 16, TextPrimary, FontWeights.Bold).Also(t => t.HorizontalAlignment = HorizontalAlignment.Center));
        if (!string.IsNullOrEmpty(sub))
            center.Children.Add(Mono(sub.ToUpperInvariant(), 9.5, TextSoft, FontWeights.SemiBold).Also(t => { t.HorizontalAlignment = HorizontalAlignment.Center; t.Margin = new(0, 3, 0, 0); }));

        return new Grid { Width = size, Height = size, Children = { canvas, center } };
    }

    public static System.Windows.Shapes.Path CreateArcPath(double cx, double cy, double r, double startDeg, double endDeg, Color strokeColor, double thickness)
    {
        double startRad = startDeg * Math.PI / 180.0;
        double endRad = endDeg * Math.PI / 180.0;
        Point start = new(cx + r * Math.Cos(startRad), cy + r * Math.Sin(startRad));
        Point end = new(cx + r * Math.Cos(endRad), cy + r * Math.Sin(endRad));
        bool largeArc = Math.Abs(endDeg - startDeg) > 180.0;

        var fig = new PathFigure { StartPoint = start, IsClosed = false };
        fig.Segments.Add(new ArcSegment
        {
            Point = end,
            Size = new Size(r, r),
            IsLargeArc = largeArc,
            SweepDirection = SweepDirection.Clockwise
        });
        var geom = new PathGeometry();
        geom.Figures.Add(fig);
        return new System.Windows.Shapes.Path
        {
            Data = geom,
            Stroke = new LinearGradientBrush(CyanBright, strokeColor, 35),
            StrokeThickness = thickness,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            Effect = new DropShadowEffect { Color = strokeColor, BlurRadius = 12, ShadowDepth = 0, Opacity = 0.55 }
        };
    }

    /// <summary>Harmonic sine wave visual (`wave_line` in `t_pulse.py`).</summary>
    public static Canvas WaveLine(double w = 520, double h = 110, double amp = 28, double freq = 3, bool fill = true)
    {
        var canvas = new Canvas { Width = w, Height = h, ClipToBounds = true };
        double mid = h / 2.0;

        var baseLine = new Line
        {
            X1 = 0,
            Y1 = mid,
            X2 = w,
            Y2 = mid,
            Stroke = PulseInk.B(PulseInk.A(Colors.White, 0.10)),
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection([3, 5])
        };
        canvas.Children.Add(baseLine);

        var pts = new PointCollection();
        for (int i = 0; i <= 80; i++)
        {
            double x = i * (w / 80.0);
            double y = mid - Math.Sin(i / 80.0 * Math.PI * freq) * amp * Math.Sin(i / 80.0 * Math.PI);
            pts.Add(new Point(x, y));
        }

        if (fill)
        {
            var fig = new PathFigure { StartPoint = new Point(0, mid), IsClosed = true };
            foreach (var p in pts) fig.Segments.Add(new LineSegment(p, true));
            fig.Segments.Add(new LineSegment(new Point(w, mid), true));
            var geom = new PathGeometry();
            geom.Figures.Add(fig);
            var area = new System.Windows.Shapes.Path
            {
                Data = geom,
                Fill = new LinearGradientBrush(PulseInk.A(CyanColor, 0.28), PulseInk.A(VioletColor, 0.02), 90)
            };
            canvas.Children.Add(area);
        }

        var lineGrad = new LinearGradientBrush { StartPoint = new(0, 0), EndPoint = new(1, 0) };
        lineGrad.GradientStops.Add(new GradientStop(CyanColor, 0.0));
        lineGrad.GradientStops.Add(new GradientStop(VioletColor, 0.55));
        lineGrad.GradientStops.Add(new GradientStop(MintColor, 1.0));

        var poly = new Polyline
        {
            Points = pts,
            Stroke = lineGrad,
            StrokeThickness = 2.6,
            Effect = new DropShadowEffect { Color = CyanColor, BlurRadius = 10, ShadowDepth = 0, Opacity = 0.55 }
        };
        canvas.Children.Add(poly);

        foreach (int idx in new[] { 16, 40, 64 })
        {
            var pt = pts[idx];
            var dot = new Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = Brushes.White,
                Stroke = PulseInk.B(CyanBright),
                StrokeThickness = 2
            };
            Canvas.SetLeft(dot, pt.X - 4);
            Canvas.SetTop(dot, pt.Y - 4);
            canvas.Children.Add(dot);
        }
        return canvas;
    }

    /// <summary>Controller artwork on a glowing harmonic pedestal with optional callout rings.
    /// Uses Viewbox uniform scaling so the 1536x1024 controller canvas is rendered in full, crisp and unclipped.</summary>
    public static Canvas ControllerPedestal(string modelId, bool rear = false, double width = 460, double height = 300, bool showRipples = true, string? finish = null)
    {
        var c = new Canvas { Width = width, Height = height, ClipToBounds = false };
        double cx = width / 2, cy = height / 2;

        if (showRipples)
        {
            foreach (var (rx, ry, alpha, dash) in new[]
            {
                (width * 0.44, height * 0.36, 0.22, false),
                (width * 0.52, height * 0.44, 0.12, true),
                (width * 0.36, height * 0.28, 0.28, false)
            })
            {
                var ring = new Ellipse
                {
                    Width = rx * 2,
                    Height = ry * 2,
                    Stroke = PulseInk.B(PulseInk.A(PulseInk.C("#a8ccff"), alpha)),
                    StrokeThickness = 1.2
                };
                if (dash) ring.StrokeDashArray = new DoubleCollection([4, 6]);
                Canvas.SetLeft(ring, cx - rx);
                Canvas.SetTop(ring, cy - ry + 14);
                c.Children.Add(ring);
            }
        }

        var shadow = new Ellipse
        {
            Width = width * 0.70,
            Height = 32,
            Fill = new RadialGradientBrush(Color.FromArgb(140, 0, 0, 0), Colors.Transparent)
        };
        Canvas.SetLeft(shadow, cx - width * 0.35);
        Canvas.SetTop(shadow, height - 34);
        c.Children.Add(shadow);

        var pad = new LiveController { Smooth = false };
        pad.Show(modelId, rear, finish);

        var box = new Viewbox
        {
            Width = width * 0.90,
            Height = height * 0.88,
            Stretch = Stretch.Uniform,
            Child = pad,
            Effect = new DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 36,
                ShadowDepth = 18,
                Direction = 270,
                Opacity = 0.55
            }
        };
        Canvas.SetLeft(box, (width - width * 0.90) / 2);
        Canvas.SetTop(box, (height - height * 0.88) / 2 - 4);
        c.Children.Add(box);
        return c;
    }
}
