using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace X20Ctl.Product.Hybrid;

/// <summary>
/// Design system primitives, colors, typography, and controls for the Hybrid theme:
/// "Two halves, one machine. The Switch is two controllers that slide onto a screen and click."
/// Strictly implements t_hybrid.py CSS and specifications across Docked, Handheld, and Tabletop modes.
/// </summary>
public static class HybridKit
{
    // Hybrid Palette Tokens
    public static readonly Color Lavender = Color.FromRgb(0xd5, 0xbf, 0xf6);       // #d5bff6
    public static readonly Color Lavender2 = Color.FromRgb(0xbb, 0x9c, 0xe9);      // #bb9ce9
    public static readonly Color LavenderDeep = Color.FromRgb(0x8a, 0x66, 0xb5);  // #8a66b5
    public static readonly Color Mauve = Color.FromRgb(0x5c, 0x40, 0x75);         // #5c4075
    public static readonly Color Charcoal = Color.FromRgb(0x2b, 0x28, 0x31);      // #2b2831
    public static readonly Color ScreenDark = Color.FromRgb(0x14, 0x12, 0x18);     // #141218
    public static readonly Color Ink = Color.FromRgb(0x1b, 0x16, 0x22);            // #1b1622
    public static readonly Color Cyan = Color.FromRgb(0x52, 0xdc, 0xff);           // #52dcff (Seam & Focus glow)
    public static readonly Color TextWhite = Color.FromRgb(0xf4, 0xf1, 0xf8);      // #f4f1f8
    public static readonly Color TextMute = Color.FromRgb(0xa7, 0xa0, 0xb4);       // #a7a0b4
    public static readonly Color TextMute2 = Color.FromRgb(0x6d, 0x5e, 0x82);      // #6d5e82
    public static readonly Color PanelBg = Color.FromRgb(0x1f, 0x1c, 0x25);        // #1f1c25
    public static readonly Color LineColor = Color.FromRgb(0x32, 0x2e, 0x3a);      // #322e3a
    public static readonly Color LineColor2 = Color.FromRgb(0x4a, 0x45, 0x55);     // #4a4555
    public static readonly Color ChipBg = Color.FromRgb(0x2f, 0x2b, 0x37);         // #2f2b37
    public static readonly Color TrackBg = Color.FromRgb(0x2f, 0x2b, 0x37);        // #2f2b37
    public static readonly Color KeyBg = Color.FromRgb(0x34, 0x30, 0x3c);          // #34303c

    // Brushes
    public static readonly SolidColorBrush LavenderBrush = B(Lavender);
    public static readonly SolidColorBrush LavenderDeepBrush = B(LavenderDeep);
    public static readonly SolidColorBrush MauveBrush = B(Mauve);
    public static readonly SolidColorBrush CharcoalBrush = B(Charcoal);
    public static readonly SolidColorBrush ScreenDarkBrush = B(ScreenDark);
    public static readonly SolidColorBrush InkBrush = B(Ink);
    public static readonly SolidColorBrush CyanBrush = B(Cyan);
    public static readonly SolidColorBrush WhiteBrush = B(TextWhite);
    public static readonly SolidColorBrush MuteBrush = B(TextMute);
    public static readonly SolidColorBrush Mute2Brush = B(TextMute2);
    public static readonly SolidColorBrush PanelBrush = B(PanelBg);
    public static readonly SolidColorBrush LineBrush = B(LineColor);
    public static readonly SolidColorBrush ChipBrush = B(ChipBg);

    // Typography
    public static readonly FontFamily Display = new("Segoe UI Variable Display, Segoe UI");
    public static readonly FontFamily Body = new("Segoe UI Variable Text, Segoe UI");
    public static readonly FontFamily Icons = new("Segoe Fluent Icons, Segoe MDL2 Assets");

    public static SolidColorBrush B(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }
    public static SolidColorBrush B(string val) => B(C(val));
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
    public static Color A(Color c, double op) => Color.FromArgb((byte)Math.Clamp(op * 255, 0, 255), c.R, c.G, c.B);

    /// <summary>
    /// Cyan Tab-and-Socket Seam path with glowing drop shadow.
    /// Strictly implements t_hybrid.py:108 seam().
    /// Path: M x,y0 V (ny-34) H (x+16) V (ny+34) H x V y1
    /// </summary>
    public static UIElement Seam(double x = 800, double y0 = 0, double y1 = 900, double ny = 450, bool locked = true, double op = 1.0)
    {
        var canvas = new Canvas { Width = 1600, Height = 900, IsHitTestVisible = false, Opacity = op };
        var geom = new PathGeometry();
        var fig = new PathFigure { StartPoint = new Point(x, y0), IsClosed = false };

        if (locked)
        {
            fig.Segments.Add(new LineSegment(new Point(x, ny - 34), true));
            fig.Segments.Add(new LineSegment(new Point(x + 16, ny - 34), true));
            fig.Segments.Add(new LineSegment(new Point(x + 16, ny + 34), true));
            fig.Segments.Add(new LineSegment(new Point(x, ny + 34), true));
            fig.Segments.Add(new LineSegment(new Point(x, y1), true));
            geom.Figures.Add(fig);

            var path = new System.Windows.Shapes.Path
            {
                Data = geom,
                Stroke = CyanBrush,
                StrokeThickness = 3,
                Effect = new DropShadowEffect
                {
                    Color = Cyan,
                    BlurRadius = 12,
                    ShadowDepth = 0,
                    Opacity = 0.95
                }
            };
            canvas.Children.Add(path);
        }
        else
        {
            // Open seam: parts at the tab and goes grey (#57515f)
            fig.Segments.Add(new LineSegment(new Point(x, ny - 34), true));
            fig.Segments.Add(new LineSegment(new Point(x + 16, ny - 34), true));
            fig.Segments.Add(new LineSegment(new Point(x + 16, ny - 8), true));
            geom.Figures.Add(fig);

            var fig2 = new PathFigure { StartPoint = new Point(x + 16, ny + 8), IsClosed = false };
            fig2.Segments.Add(new LineSegment(new Point(x + 16, ny + 34), true));
            fig2.Segments.Add(new LineSegment(new Point(x, ny + 34), true));
            fig2.Segments.Add(new LineSegment(new Point(x, y1), true));
            geom.Figures.Add(fig2);

            var path = new System.Windows.Shapes.Path
            {
                Data = geom,
                Stroke = B("#57515f"),
                StrokeThickness = 3
            };
            canvas.Children.Add(path);
        }

        return canvas;
    }

    /// <summary>
    /// Docked mode two-halves background:
    /// Left half: radial gradient (#e8dafc -> #cbb0f2 52% -> #a989da)
    /// Right half: radial gradient (#3b3542 -> #26222c 52% -> #18161c) clipped along the notched seam.
    /// Strictly implements t_hybrid.py:118 halves().
    /// </summary>
    public static Canvas Halves(double x = 800, double ny = 450)
    {
        var root = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };

        // 1. Left Half (.hl) - full 1600x900 base
        var hl = new Rectangle
        {
            Width = 1600,
            Height = 900,
            Fill = new RadialGradientBrush
            {
                Center = new Point(0.18, 0.08),
                GradientOrigin = new Point(0.18, 0.08),
                RadiusX = 1.1,
                RadiusY = 0.9,
                GradientStops =
                {
                    new(C("#e8dafc"), 0.0),
                    new(C("#cbb0f2"), 0.52),
                    new(C("#a989da"), 1.0)
                }
            }
        };
        root.Children.Add(hl);

        // 2. Right Half (.hr) clipped with the tab notch
        // Polygon: (x, 0) -> (1600, 0) -> (1600, 900) -> (x, 900) -> (x, ny+34) -> (x+16, ny+34) -> (x+16, ny-34) -> (x, ny-34)
        var clipGeom = new PathGeometry();
        var fig = new PathFigure { StartPoint = new Point(x, 0), IsClosed = true, IsFilled = true };
        fig.Segments.Add(new LineSegment(new Point(1600, 0), true));
        fig.Segments.Add(new LineSegment(new Point(1600, 900), true));
        fig.Segments.Add(new LineSegment(new Point(x, 900), true));
        fig.Segments.Add(new LineSegment(new Point(x, ny + 34), true));
        fig.Segments.Add(new LineSegment(new Point(x + 16, ny + 34), true));
        fig.Segments.Add(new LineSegment(new Point(x + 16, ny - 34), true));
        fig.Segments.Add(new LineSegment(new Point(x, ny - 34), true));
        clipGeom.Figures.Add(fig);

        var hr = new Rectangle
        {
            Width = 1600,
            Height = 900,
            Clip = clipGeom,
            Fill = new RadialGradientBrush
            {
                Center = new Point(0.88, 0.0),
                GradientOrigin = new Point(0.88, 0.0),
                RadiusX = 1.2,
                RadiusY = 1.0,
                GradientStops =
                {
                    new(C("#3b3542"), 0.0),
                    new(C("#26222c"), 0.52),
                    new(C("#18161c"), 1.0)
                }
            }
        };
        root.Children.Add(hr);

        // 3. The glowing cyan seam path
        root.Children.Add(Seam(x, 0, 900, ny, true));

        return root;
    }

    /// <summary>
    /// Top Brand element: Hybrid winged logo + bold "X20CTL".
    /// </summary>
    public static Canvas Brand(double x = 44, double y = 30, bool dark = true)
    {
        var canvas = new Canvas { Width = 300, Height = 70, IsHitTestVisible = false };
        string logoPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Brand", "themes", "hybrid", "logo.png");
        if (System.IO.File.Exists(logoPath))
        {
            var img = new Image
            {
                Width = 92,
                Height = 64,
                Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(System.IO.Path.GetFullPath(logoPath)))
            };
            Canvas.SetLeft(img, x);
            Canvas.SetTop(img, y);
            canvas.Children.Add(img);
        }

        var text = new TextBlock
        {
            Text = "X20CTL",
            FontFamily = Display,
            FontSize = 30,
            FontWeight = FontWeights.ExtraBold,
            Foreground = dark ? InkBrush : WhiteBrush
        };
        Canvas.SetLeft(text, x + 104);
        Canvas.SetTop(text, y + 16);
        canvas.Children.Add(text);

        return canvas;
    }

    /// <summary>
    /// Top Right status: 4 cyan tick dashes, "Player 1", time, battery.
    /// </summary>
    public static FrameworkElement TopRight(bool dark = false, int activePlayer = 0)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };

        // 4 Player LED dashes
        var ledsPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 16, 0), VerticalAlignment = VerticalAlignment.Center };
        for (int i = 0; i < 4; i++)
        {
            bool on = i == activePlayer;
            var dash = new Border
            {
                Width = 19,
                Height = 6,
                CornerRadius = new CornerRadius(3),
                Margin = new Thickness(i == 0 ? 0 : 5, 0, 0, 0),
                Background = on ? CyanBrush : (dark ? B("rgba(27,22,34,.22)") : B("#4d4758"))
            };
            if (on)
            {
                dash.Effect = new DropShadowEffect { Color = Cyan, BlurRadius = 9, ShadowDepth = 0, Opacity = 0.9 };
            }
            ledsPanel.Children.Add(dash);
        }
        panel.Children.Add(ledsPanel);

        // Player name
        panel.Children.Add(new TextBlock
        {
            Text = $"Player {activePlayer + 1}",
            FontFamily = Body,
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = dark ? InkBrush : WhiteBrush,
            Margin = new Thickness(0, 0, 16, 0),
            VerticalAlignment = VerticalAlignment.Center
        });

        // Time
        panel.Children.Add(new TextBlock
        {
            Text = ThemeClock.Now,
            FontFamily = Body,
            FontSize = 16,
            FontWeight = FontWeights.Normal,
            Foreground = dark ? InkBrush : WhiteBrush,
            Opacity = 0.7,
            Margin = new Thickness(0, 0, 16, 0),
            VerticalAlignment = VerticalAlignment.Center
        });

        // Battery Icon
        panel.Children.Add(new TextBlock
        {
            Text = "\uE83F",
            FontFamily = Icons,
            FontSize = 20,
            Foreground = dark ? InkBrush : WhiteBrush,
            VerticalAlignment = VerticalAlignment.Center
        });

        var host = new Canvas { Width = 350, Height = 40, IsHitTestVisible = false };
        Canvas.SetRight(panel, 52);
        Canvas.SetTop(panel, 44);
        host.Children.Add(panel);
        return host;
    }

    /// <summary>
    /// Physical Face Button cluster: diamond arrangement of 4 round circular button caps (Y, X, B, A),
    /// plus the vertical action legend (Select, Controller Zone, Configure, Rescan games).
    /// Strictly implements t_hybrid.py:253.
    /// </summary>
    public static Canvas FaceButtonCluster(double cx = 1262, double cy = 352, Action? onSelect = null, Action? onZone = null, Action? onConfigure = null, Action? onRescan = null)
    {
        var canvas = new Canvas { Width = 1600, Height = 900 };

        // Diamond buttons: Y (top), X (left), B (right), A (bottom)
        var buttons = new (string Key, double Dx, double Dy, Action? Action)[]
        {
            ("Y", 0, -62, onRescan),
            ("X", -62, 0, onConfigure),
            ("B", 62, 0, onZone),
            ("A", 0, 62, onSelect)
        };

        foreach (var (k, dx, dy, act) in buttons)
        {
            var btnBorder = new Border
            {
                Width = 52,
                Height = 52,
                CornerRadius = new CornerRadius(26),
                Background = WhiteBrush,
                Cursor = System.Windows.Input.Cursors.Hand,
                Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 8, ShadowDepth = 3, Opacity = 0.4 },
                Child = new TextBlock
                {
                    Text = k,
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 20,
                    FontWeight = FontWeights.Bold,
                    Foreground = InkBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            if (act != null) btnBorder.MouseLeftButtonUp += (_, _) => act();
            Canvas.SetLeft(btnBorder, cx + dx - 26);
            Canvas.SetTop(btnBorder, cy + dy - 26);
            canvas.Children.Add(btnBorder);
        }

        // Action Legend at (1366, 262)
        var legend = new StackPanel { Orientation = Orientation.Vertical };
        var actions = new (string Key, string Label, Action? Action)[]
        {
            ("A", "Select", onSelect),
            ("B", "Controller Zone", onZone),
            ("X", "Configure", onConfigure),
            ("Y", "Rescan games", onRescan)
        };

        foreach (var (k, label, act) in actions)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 20), Cursor = System.Windows.Input.Cursors.Hand };
            if (act != null) row.MouseLeftButtonUp += (_, _) => act();

            // Keycap pill
            var keycap = new Border
            {
                Width = 24,
                Height = 24,
                CornerRadius = new CornerRadius(12),
                Background = WhiteBrush,
                Margin = new Thickness(0, 0, 10, 0),
                Child = new TextBlock
                {
                    Text = k,
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 12,
                    FontWeight = FontWeights.Bold,
                    Foreground = InkBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            row.Children.Add(keycap);

            // Label
            row.Children.Add(new TextBlock
            {
                Text = label,
                FontFamily = Body,
                FontSize = 17,
                FontWeight = FontWeights.SemiBold,
                Foreground = WhiteBrush,
                VerticalAlignment = VerticalAlignment.Center
            });

            legend.Children.Add(row);
        }

        Canvas.SetLeft(legend, 1366);
        Canvas.SetTop(legend, 262);
        canvas.Children.Add(legend);

        return canvas;
    }

    /// <summary>
    /// Unified bottom dock pill across both halves (y = 676, h = 128) with LB / RB bumper pills,
    /// 9 circular subsystem icon buttons, and glowing cyan double ring for the active item.
    /// Strictly implements t_hybrid.py:260.
    /// </summary>
    public static Canvas DashboardDock(int activeIndex = 0, Action<int>? onSelect = null)
    {
        var root = new Canvas { Width = 1600, Height = 900 };

        // 1. Left lavender half of dock pill (236, 676, w: 564, h: 128)
        var pillL = new Border
        {
            Width = 564,
            Height = 128,
            CornerRadius = new CornerRadius(64, 0, 0, 64),
            Background = B("rgba(92,64,117,.16)")
        };
        Canvas.SetLeft(pillL, 236);
        Canvas.SetTop(pillL, 676);
        root.Children.Add(pillL);

        // 2. Right charcoal half of dock pill (816, 676, w: 548, h: 128)
        var pillR = new Border
        {
            Width = 548,
            Height = 128,
            CornerRadius = new CornerRadius(0, 64, 64, 0),
            Background = B("rgba(255,255,255,.06)")
        };
        Canvas.SetLeft(pillR, 816);
        Canvas.SetTop(pillR, 676);
        root.Children.Add(pillR);

        // 3. LB & RB bumper keycaps
        var lb = new Border
        {
            Height = 30,
            Padding = new Thickness(12, 0, 12, 0),
            CornerRadius = new CornerRadius(12),
            Background = InkBrush,
            Child = new TextBlock
            {
                Text = "LB",
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        Canvas.SetLeft(lb, 184);
        Canvas.SetTop(lb, 726);
        root.Children.Add(lb);

        var rb = new Border
        {
            Height = 30,
            Padding = new Thickness(12, 0, 12, 0),
            CornerRadius = new CornerRadius(12),
            Background = WhiteBrush,
            Child = new TextBlock
            {
                Text = "RB",
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = InkBrush,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        Canvas.SetLeft(rb, 1376);
        Canvas.SetTop(rb, 726);
        root.Children.Add(rb);

        // 4. 9 circular subsystem icon buttons
        var pages = new (string Name, string Icon)[]
        {
            ("Buttons", "\uE7FC"),      // Gamepad
            ("Curves", "\uE9D2"),       // Wave curve
            ("Macros", "\uE945"),       // Lightning
            ("Vibration", "\uE877"),    // Phone vibration
            ("PC mode", "\uE977"),      // Monitor
            ("Device", "\uE772"),       // Controller hardware
            ("Mouse", "\uE962"),        // Mouse
            ("Setups", "\uE8F1"),       // Cards library
            ("Tools", "\uE90F")         // Wrench
        };

        for (int i = 0; i < pages.Length; i++)
        {
            int idx = i;
            var (name, icon) = pages[i];
            double cx = 800 + (i - 4) * 124;
            bool on = i == activeIndex;
            double d = on ? 98 : 78;
            bool leftSide = cx < 800;

            Color bg = on ? Mauve : (leftSide ? C("#fbf8ff") : C("#3d3846"));
            Color fg = on ? Colors.White : (leftSide ? Mauve : C("#f4f1f8"));

            var btnBorder = new Border
            {
                Width = d,
                Height = d,
                CornerRadius = new CornerRadius(d / 2),
                Background = B(bg),
                Cursor = System.Windows.Input.Cursors.Hand,
                ClipToBounds = false
            };

            if (on)
            {
                // Double ring: 5px white + 9px cyan + deep shadow
                btnBorder.BorderBrush = WhiteBrush;
                btnBorder.BorderThickness = new Thickness(5);
                btnBorder.Effect = new DropShadowEffect
                {
                    Color = Cyan,
                    BlurRadius = 18,
                    ShadowDepth = 0,
                    Opacity = 1.0
                };

                // Active title above at (cx - 80, 628)
                var title = new TextBlock
                {
                    Text = name,
                    FontFamily = Display,
                    FontSize = 18,
                    FontWeight = FontWeights.ExtraBold,
                    Foreground = InkBrush,
                    Width = 160,
                    TextAlignment = TextAlignment.Center
                };
                Canvas.SetLeft(title, cx - 80);
                Canvas.SetTop(title, 628);
                root.Children.Add(title);
            }

            var iconBlock = new TextBlock
            {
                Text = icon,
                FontFamily = Icons,
                FontSize = on ? 30 : 26,
                Foreground = B(fg),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            btnBorder.Child = iconBlock;

            btnBorder.MouseLeftButtonUp += (_, _) => onSelect?.Invoke(idx);

            Canvas.SetLeft(btnBorder, cx - d / 2);
            Canvas.SetTop(btnBorder, 740 - d / 2);
            root.Children.Add(btnBorder);
        }

        return root;
    }

    /// <summary>
    /// Loads controller art image.
    /// </summary>
    public static Image? ControllerArt(string modelId = "d10-purple", double width = 690)
    {
        return ThemeArt.Load(modelId, width);
    }

    /// <summary>
    /// Tabletop perspective glass surface: 1460x190 trapezoid with top highlight edge.
    /// Strictly implements t_hybrid.py:360 table().
    /// </summary>
    public static UIElement TabletopStage(double y = 676)
    {
        var geom = new PathGeometry();
        var fig = new PathFigure { StartPoint = new Point(143, 0), IsClosed = true, IsFilled = true };
        fig.Segments.Add(new LineSegment(new Point(1387, 0), true));
        fig.Segments.Add(new LineSegment(new Point(1460, 190), true));
        fig.Segments.Add(new LineSegment(new Point(0, 190), true));
        geom.Figures.Add(fig);

        var path = new System.Windows.Shapes.Path
        {
            Width = 1460,
            Height = 190,
            Data = geom,
            Fill = new LinearGradientBrush
            {
                StartPoint = new Point(0.5, 0),
                EndPoint = new Point(0.5, 1),
                GradientStops =
                {
                    new(Color.FromArgb(0x57, 255, 255, 255), 0.0),
                    new(Color.FromArgb(0x10, 255, 255, 255), 0.70),
                    new(Colors.Transparent, 1.0)
                }
            },
            Stroke = new SolidColorBrush(Color.FromArgb(0x8c, 255, 255, 255)),
            StrokeThickness = 2
        };

        var container = new Canvas { Width = 1600, Height = 900, IsHitTestVisible = false };
        Canvas.SetLeft(path, 70);
        Canvas.SetTop(path, y);
        container.Children.Add(path);
        return container;
    }

    /// <summary>
    /// Tabletop Stand card: upright card standing on the perspective table with bottom elliptical shadow,
    /// upward lift (-26px) and glowing cyan top tab when selected.
    /// Strictly implements t_hybrid.py:364 stand().
    /// </summary>
    public static FrameworkElement StandCard(double x, double y, double w, double h, UIElement inner, bool on = false, Action? onClick = null)
    {
        var container = new Canvas { Width = w, Height = h + 60, ClipToBounds = false };
        double lift = on ? -26 : 0;

        // 1. Bottom elliptical floor shadow
        var shadow = new Ellipse
        {
            Width = w - 36,
            Height = 30,
            Fill = new RadialGradientBrush
            {
                GradientOrigin = new Point(0.5, 0.5),
                Center = new Point(0.5, 0.5),
                RadiusX = 0.5,
                RadiusY = 0.5,
                GradientStops =
                {
                    new(Color.FromArgb(0x73, 0x14, 0x0e, 0x1c), 0.0),
                    new(Colors.Transparent, 1.0)
                }
            }
        };
        Canvas.SetLeft(shadow, 18);
        Canvas.SetTop(shadow, h - 4);
        container.Children.Add(shadow);

        // 2. Upright card border
        var card = new Border
        {
            Width = w,
            Height = h,
            CornerRadius = new CornerRadius(26),
            ClipToBounds = true,
            Child = inner,
            Cursor = System.Windows.Input.Cursors.Hand
        };

        if (on)
        {
            card.BorderBrush = CyanBrush;
            card.BorderThickness = new Thickness(5);
            card.Effect = new DropShadowEffect
            {
                Color = Cyan,
                BlurRadius = 24,
                ShadowDepth = 0,
                Opacity = 0.8
            };

            // Top cyan tab: (w/2 - 12, lift - 13, 24x13)
            var tab = new Border
            {
                Width = 24,
                Height = 13,
                CornerRadius = new CornerRadius(6, 6, 0, 0),
                Background = CyanBrush
            };
            Canvas.SetLeft(tab, w / 2 - 12);
            Canvas.SetTop(tab, lift - 13);
            container.Children.Add(tab);
        }
        else
        {
            card.Effect = new DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 20,
                ShadowDepth = 12,
                Opacity = 0.4
            };
        }

        if (onClick != null) card.MouseLeftButtonUp += (_, _) => onClick();

        Canvas.SetLeft(card, 0);
        Canvas.SetTop(card, lift);
        container.Children.Add(card);

        Canvas.SetLeft(container, x);
        Canvas.SetTop(container, y);
        return container;
    }

    /// <summary>
    /// Handheld mode rails: Left lavender curved rail (rl), right charcoal rail (rr),
    /// and central screen (scr).
    /// Strictly implements t_hybrid.py:194 rails().
    /// </summary>
    public static Canvas HandheldRails(string curPage, (string Key, string Label)[] hints, string title, string lead = "", Action<string>? onNav = null)
    {
        var root = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };

        // 1. Central Screen (.scr)
        var scr = new Border
        {
            Width = 1264,
            Height = 900,
            Background = new RadialGradientBrush
            {
                Center = new Point(0.5, 0.0),
                GradientOrigin = new Point(0.5, 0.0),
                RadiusX = 0.9,
                RadiusY = 0.7,
                GradientStops = { new(C("#1c1922"), 0.0), new(C("#131116"), 0.7) }
            },
            BorderBrush = B("#2c2833"),
            BorderThickness = new Thickness(10)
        };
        Canvas.SetLeft(scr, 168);
        Canvas.SetTop(scr, 0);
        root.Children.Add(scr);

        // 2. Left Lavender Rail (.rl) (0, 0, 168x900)
        var rl = new Border
        {
            Width = 168,
            Height = 900,
            CornerRadius = new CornerRadius(84, 0, 0, 84),
            Background = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0.5),
                EndPoint = new Point(1, 0.5),
                GradientStops =
                {
                    new(C("#c3a4ef"), 0.0),
                    new(C("#dccaf9"), 0.46),
                    new(C("#c9acf2"), 1.0)
                }
            },
            BorderBrush = CyanBrush,
            BorderThickness = new Thickness(0, 0, 3, 0)
        };
        Canvas.SetLeft(rl, 0);
        Canvas.SetTop(rl, 0);
        root.Children.Add(rl);

        // Hybrid logo on Left rail at (28, 30, w: 112)
        string logoPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Brand", "themes", "hybrid", "logo.png");
        if (System.IO.File.Exists(logoPath))
        {
            var logo = new Image
            {
                Width = 112,
                Height = 78,
                Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(System.IO.Path.GetFullPath(logoPath)))
            };
            Canvas.SetLeft(logo, 28);
            Canvas.SetTop(logo, 30);
            root.Children.Add(logo);
        }

        // Left rail navigation buttons (.nk)
        var navItems = new (string Name, string Key, string Icon)[]
        {
            ("Buttons", "buttons", "\uE7FC"),
            ("Curves", "curves", "\uE9D2"),
            ("Macros", "macros", "\uE945"),
            ("Vibration", "vibration", "\uE877"),
            ("PC mode", "pcmode", "\uE977"),
            ("Device", "device", "\uE772"),
            ("Mouse", "mouse", "\uE962"),
            ("Setups", "setups", "\uE8F1"),
            ("Tools", "tools", "\uE90F")
        };

        double ny = 140;
        foreach (var (name, key, icon) in navItems)
        {
            bool on = string.Equals(key, curPage, StringComparison.OrdinalIgnoreCase);
            var nk = new Border
            {
                Width = 54,
                Height = 54,
                CornerRadius = new CornerRadius(27),
                Background = on ? MauveBrush : B("#efe5fc"),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            if (on)
            {
                nk.BorderBrush = WhiteBrush;
                nk.BorderThickness = new Thickness(4);
                nk.Effect = new DropShadowEffect { Color = Cyan, BlurRadius = 14, ShadowDepth = 0, Opacity = 0.9 };
            }
            else
            {
                nk.Effect = new DropShadowEffect { Color = Color.FromArgb(0x59, 92, 64, 117), BlurRadius = 3, ShadowDepth = 3, Direction = 270 };
            }

            nk.Child = new TextBlock
            {
                Text = icon,
                FontFamily = Icons,
                FontSize = 21,
                Foreground = on ? Brushes.White : MauveBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            nk.MouseLeftButtonUp += (_, _) => onNav?.Invoke(key);

            Canvas.SetLeft(nk, 57);
            Canvas.SetTop(nk, ny);
            root.Children.Add(nk);

            if (on)
            {
                var label = new TextBlock
                {
                    Text = name,
                    FontFamily = Display,
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    Foreground = B("#3e2c52"),
                    Width = 168,
                    TextAlignment = TextAlignment.Center
                };
                Canvas.SetLeft(label, 0);
                Canvas.SetTop(label, ny + 62);
                root.Children.Add(label);
                ny += 22;
            }

            ny += 69;
        }

        // 3. Right Charcoal Rail (.rr) (1432, 0, 168x900)
        var rr = new Border
        {
            Width = 168,
            Height = 900,
            CornerRadius = new CornerRadius(0, 84, 84, 0),
            Background = new LinearGradientBrush
            {
                StartPoint = new Point(1, 0.5),
                EndPoint = new Point(0, 0.5),
                GradientStops =
                {
                    new(C("#24212a"), 0.0),
                    new(C("#3a3541"), 0.50),
                    new(C("#2a2730"), 1.0)
                }
            },
            BorderBrush = CyanBrush,
            BorderThickness = new Thickness(3, 0, 0, 0)
        };
        Canvas.SetLeft(rr, 1432);
        Canvas.SetTop(rr, 0);
        root.Children.Add(rr);

        // Top player indicator on right rail
        var ledsPanel = new StackPanel { Orientation = Orientation.Horizontal };
        for (int i = 0; i < 4; i++)
        {
            var dash = new Border
            {
                Width = 19,
                Height = 6,
                CornerRadius = new CornerRadius(3),
                Margin = new Thickness(i == 0 ? 0 : 5, 0, 0, 0),
                Background = i == 0 ? CyanBrush : B("#4d4758")
            };
            if (i == 0) dash.Effect = new DropShadowEffect { Color = Cyan, BlurRadius = 9, ShadowDepth = 0 };
            ledsPanel.Children.Add(dash);
        }
        Canvas.SetLeft(ledsPanel, 1468);
        Canvas.SetTop(ledsPanel, 40);
        root.Children.Add(ledsPanel);

        var pName = new TextBlock
        {
            Text = "Player 1",
            FontFamily = Body,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = B("#d8d2e2"),
            Width = 168,
            TextAlignment = TextAlignment.Center
        };
        Canvas.SetLeft(pName, 1432);
        Canvas.SetTop(pName, 56);
        root.Children.Add(pName);

        var pTime = new TextBlock
        {
            Text = ThemeClock.Now,
            FontFamily = Body,
            FontSize = 14,
            Foreground = B("#9a93a6"),
            Width = 168,
            TextAlignment = TextAlignment.Center
        };
        Canvas.SetLeft(pTime, 1432);
        Canvas.SetTop(pTime, 80);
        root.Children.Add(pTime);

        // Right Rail Face Button Cluster (.cluster)
        double cx = 1516;
        double cyCluster = 250;
        var clusterButtons = new (string Key, double Dx, double Dy)[]
        {
            ("Y", 0, -56),
            ("X", -56, 0),
            ("B", 56, 0),
            ("A", 0, 56)
        };

        var usedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (k, _) in hints) usedKeys.Add(k);

        foreach (var (k, dx, dy) in clusterButtons)
        {
            bool used = usedKeys.Contains(k);
            var btn = new Border
            {
                Width = 44,
                Height = 44,
                CornerRadius = new CornerRadius(22),
                Background = used ? WhiteBrush : B("#3d3846"),
                Child = new TextBlock
                {
                    Text = k,
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    Foreground = used ? InkBrush : B("#7d7689"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            Canvas.SetLeft(btn, cx + dx - 22);
            Canvas.SetTop(btn, cyCluster + 56 + dy - 22);
            root.Children.Add(btn);
        }

        // Hint list under diamond cluster
        var hintsPanel = new StackPanel { Orientation = Orientation.Vertical, Width = 142 };
        foreach (var (k, text) in hints)
        {
            var hRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
            var keycap = new Border
            {
                Width = 24,
                Height = 24,
                CornerRadius = new CornerRadius(12),
                Background = WhiteBrush,
                Margin = new Thickness(0, 0, 9, 0),
                Child = new TextBlock
                {
                    Text = k,
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 12,
                    FontWeight = FontWeights.Bold,
                    Foreground = InkBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            hRow.Children.Add(keycap);
            hRow.Children.Add(new TextBlock
            {
                Text = text,
                FontFamily = Body,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = B("#e6e1ee"),
                VerticalAlignment = VerticalAlignment.Center
            });
            hintsPanel.Children.Add(hRow);
        }
        Canvas.SetLeft(hintsPanel, 1446);
        Canvas.SetTop(hintsPanel, cyCluster + 150);
        root.Children.Add(hintsPanel);

        // Title and Lead on Screen
        var titleBlock = new TextBlock
        {
            Text = title,
            FontFamily = Display,
            FontSize = 38,
            FontWeight = FontWeights.ExtraBold,
            Foreground = WhiteBrush
        };
        Canvas.SetLeft(titleBlock, 216);
        Canvas.SetTop(titleBlock, 38);
        root.Children.Add(titleBlock);

        if (!string.IsNullOrEmpty(lead))
        {
            var leadBlock = new TextBlock
            {
                Text = lead,
                FontFamily = Body,
                FontSize = 16,
                Foreground = MuteBrush,
                Width = 1020,
                TextWrapping = TextWrapping.Wrap
            };
            Canvas.SetLeft(leadBlock, 216);
            Canvas.SetTop(leadBlock, 94);
            root.Children.Add(leadBlock);
        }

        return root;
    }
}
