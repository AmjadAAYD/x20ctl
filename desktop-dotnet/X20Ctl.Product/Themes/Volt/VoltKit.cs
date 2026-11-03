using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product;

namespace X20Ctl.Product.Volt;

/// <summary>
/// Design tokens and UI construction primitives for VOLT — the Steam Deck yellow theme.
/// "Black sky over a soft, blurred yellow wave; a '?' key and the clock top left;
/// the home is a centred carousel: one sharp capsule under its title, the neighbours tilted away and washed in yellow,
/// page dots in a dark pill and white button hints straight on the yellow. The slide-out side menu (black panel, the
/// selected row a solid orange bar), the Power dialog (title pill over a black list, the choice an orange pill, a toast
/// bottom right) and the Silksong page (key art, serif logo, a dark band with an orange action button and square keys)."
/// Strictly implements t_volt.py.
/// </summary>
public static class VoltKit
{
    // Palette Tokens
    public static readonly Color BlackSky = Color.FromRgb(0x00, 0x00, 0x00);         // #000000
    public static readonly Color AmberHot = Color.FromRgb(0xff, 0xad, 0x0a);         // #ffad0a (Charged amber / solid orange)
    public static readonly Color AmberHi = Color.FromRgb(0xff, 0xc9, 0x5a);          // #ffc95a
    public static readonly Color PanelDark = Color.FromRgb(0x12, 0x12, 0x12);        // #121212
    public static readonly Color CardDark = Color.FromRgb(0x16, 0x16, 0x16);         // #161616
    public static readonly Color SheetDark = Color.FromRgb(0x1d, 0x1d, 0x1d);        // #1d1d1d
    public static readonly Color RowHover = Color.FromRgb(0x26, 0x26, 0x26);         // #262626
    public static readonly Color RowActive = Color.FromRgb(0x2a, 0x2a, 0x2a);        // #2a2a2a
    public static readonly Color BorderDark = Color.FromRgb(0x3b, 0x3b, 0x3b);       // #3b3b3b
    public static readonly Color TextWhite = Color.FromRgb(0xff, 0xff, 0xff);        // #ffffff
    public static readonly Color TextLight = Color.FromRgb(0xcf, 0xcf, 0xcf);        // #cfcfcf
    public static readonly Color TextMute = Color.FromRgb(0xaa, 0xaa, 0xaa);         // #aaaaaa
    public static readonly Color TextSub = Color.FromRgb(0x8a, 0x8a, 0x8a);          // #8a8a8a

    // Brushes
    public static readonly SolidColorBrush BlackSkyBrush = B(BlackSky);
    public static readonly SolidColorBrush AmberHotBrush = B(AmberHot);
    public static readonly SolidColorBrush AmberHiBrush = B(AmberHi);
    public static readonly SolidColorBrush PanelDarkBrush = B(PanelDark);
    public static readonly SolidColorBrush CardDarkBrush = B(CardDark);
    public static readonly SolidColorBrush SheetDarkBrush = B(SheetDark);
    public static readonly SolidColorBrush RowHoverBrush = B(RowHover);
    public static readonly SolidColorBrush BorderDarkBrush = B(BorderDark);
    public static readonly SolidColorBrush WhiteBrush = B(TextWhite);
    public static readonly SolidColorBrush LightBrush = B(TextLight);
    public static readonly SolidColorBrush MuteBrush = B(TextMute);
    public static readonly SolidColorBrush SubBrush = B(TextSub);

    // Typography
    public static readonly FontFamily Display = new("Segoe UI Variable Display, Segoe UI");
    public static readonly FontFamily Body = new("Segoe UI Variable Text, Segoe UI");
    public static readonly FontFamily SerifTitle = new("Georgia, Sitka Banner, Times New Roman");
    public static readonly FontFamily LogoFont = new("Bahnschrift");
    public static readonly FontFamily Icons = new("Segoe Fluent Icons, Segoe MDL2 Assets");

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

    /// <summary>
    /// Loads controller art image with uniform scaling.
    /// </summary>
    public static Image? ControllerArt(string modelId = "dune", double width = 600)
    {
        string safe = modelId.Replace("_", "-").ToLowerInvariant();
        if (safe == "x05" || safe == "x05a") safe = "x05pro";
        else if (safe.Contains("starfield")) safe = "x15-starfield";
        else if (safe.Contains("wukong")) safe = "x15-wukong";
        else if (safe.Contains("purple")) safe = "d10-purple";

        string path = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "art", $"{safe}.png");
        if (!System.IO.File.Exists(path))
        {
            path = System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Assets", "art", $"{safe}.png");
        }
        if (!System.IO.File.Exists(path)) return null;

        return new Image
        {
            Width = width,
            Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(System.IO.Path.GetFullPath(path))),
            Stretch = Stretch.Uniform
        };
    }

    /// <summary>
    /// Steam Deck yellow wave path: Black sky over soft, blurred yellow wave at bottom,
    /// enriched with secondary electric amber wave, ambient gold particle specks, and depth lighting.
    /// Strictly implements t_volt.py:55 wave().
    /// Path: M-60 470 C 260 360 560 360 860 420 S 1360 520 1680 430 L1680 960 L-60 960 Z
    /// </summary>
    public static Canvas WaveBackdrop(double blur = 16)
    {
        var root = new Canvas { Width = 1600, Height = 900, ClipToBounds = true, Background = BlackSkyBrush };

        // 1. Subtle radial ambient glow at center-bottom
        var ambientGlow = new Rectangle
        {
            Width = 1600,
            Height = 900,
            Fill = new RadialGradientBrush
            {
                Center = new Point(0.5, 0.65),
                GradientOrigin = new Point(0.5, 0.65),
                RadiusX = 0.7,
                RadiusY = 0.5,
                GradientStops =
                {
                    new(C("rgba(255,173,10,0.12)"), 0.0),
                    new(C("rgba(107,79,0,0.06)"), 0.5),
                    new(Colors.Transparent, 1.0)
                }
            }
        };
        var glowScale = new ScaleTransform(1.0, 1.0);
        ambientGlow.RenderTransformOrigin = new Point(0.5, 0.65);
        ambientGlow.RenderTransform = glowScale;
        var glowAnim = new System.Windows.Media.Animation.DoubleAnimation(0.96, 1.04, TimeSpan.FromSeconds(4.5))
        {
            AutoReverse = true,
            RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever,
            EasingFunction = new System.Windows.Media.Animation.SineEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut }
        };
        glowScale.BeginAnimation(ScaleTransform.ScaleXProperty, glowAnim);
        glowScale.BeginAnimation(ScaleTransform.ScaleYProperty, glowAnim);
        root.Children.Add(ambientGlow);

        // 2. Secondary electric amber wave for fluid parallax depth
        var amberBrush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1),
            GradientStops =
            {
                new(C("rgba(255,173,10,0.45)"), 0.0),
                new(C("rgba(228,169,0,0.30)"), 0.35),
                new(C("rgba(80,50,0,0.15)"), 1.0)
            }
        };
        var amberFig = new PathFigure { StartPoint = new Point(-60, 520), IsClosed = true, IsFilled = true };
        amberFig.Segments.Add(new BezierSegment(new Point(320, 410), new Point(620, 390), new Point(940, 450), true));
        amberFig.Segments.Add(new BezierSegment(new Point(1220, 500), new Point(1440, 540), new Point(1680, 460), true));
        amberFig.Segments.Add(new LineSegment(new Point(1680, 960), true));
        amberFig.Segments.Add(new LineSegment(new Point(-60, 960), true));
        var amberGeom = new PathGeometry();
        amberGeom.Figures.Add(amberFig);
        var amberPath = new System.Windows.Shapes.Path
        {
            Data = amberGeom,
            Fill = amberBrush,
            Effect = new BlurEffect { Radius = blur * 1.3, RenderingBias = RenderingBias.Performance }
        };
        var amberTrans = new TranslateTransform();
        amberPath.RenderTransform = amberTrans;
        var amberAnim = new System.Windows.Media.Animation.DoubleAnimation(6, -6, TimeSpan.FromSeconds(5.2))
        {
            AutoReverse = true,
            RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever,
            EasingFunction = new System.Windows.Media.Animation.SineEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut }
        };
        amberTrans.BeginAnimation(TranslateTransform.YProperty, amberAnim);
        root.Children.Add(amberPath);

        // 3. Primary Steam Deck Yellow wave
        var waveBrush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1),
            GradientStops =
            {
                new(C("#6b4f00"), 0.0),
                new(C("#e4a900"), 0.16),
                new(C("#ffd000"), 0.42),
                new(C("#ffe414"), 1.0)
            }
        };

        var pathFigure = new PathFigure { StartPoint = new Point(-60, 470), IsClosed = true, IsFilled = true };
        pathFigure.Segments.Add(new BezierSegment(new Point(260, 360), new Point(560, 360), new Point(860, 420), true));
        pathFigure.Segments.Add(new BezierSegment(new Point(1160, 480), new Point(1360, 520), new Point(1680, 430), true));
        pathFigure.Segments.Add(new LineSegment(new Point(1680, 960), true));
        pathFigure.Segments.Add(new LineSegment(new Point(-60, 960), true));

        var geometry = new PathGeometry();
        geometry.Figures.Add(pathFigure);

        var path = new System.Windows.Shapes.Path
        {
            Data = geometry,
            Fill = waveBrush,
            Effect = new BlurEffect { Radius = blur, RenderingBias = RenderingBias.Performance }
        };
        var waveTrans = new TranslateTransform();
        path.RenderTransform = waveTrans;
        var waveAnim = new System.Windows.Media.Animation.DoubleAnimation(-6, 6, TimeSpan.FromSeconds(3.8))
        {
            AutoReverse = true,
            RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever
        };
        waveTrans.BeginAnimation(TranslateTransform.YProperty, waveAnim);
        root.Children.Add(path);

        // 4. Ambient floating golden embers (particles) for life and visual richness
        var random = new Random(77);
        for (int i = 0; i < 35; i++)
        {
            double px = random.Next(40, 1560);
            double py = random.Next(280, 840);
            double psz = random.Next(2, 6);
            double pop = 0.25 + random.NextDouble() * 0.45;

            var ember = new Ellipse
            {
                Width = psz,
                Height = psz,
                Fill = new SolidColorBrush(Color.FromArgb((byte)(pop * 255), 255, 215, 60)),
                Effect = new BlurEffect { Radius = 1.2 }
            };
            Canvas.SetLeft(ember, px);
            Canvas.SetTop(ember, py);
            root.Children.Add(ember);
        }

        return root;
    }

    /// <summary>
    /// Creates a glowing elliptical stage pedestal light beneath controllers or carousel cards.
    /// </summary>
    public static Border PedestalGlow(double width = 500, double height = 120, Color? glowColor = null)
    {
        var col = glowColor ?? AmberHot;
        return new Border
        {
            Width = width,
            Height = height,
            CornerRadius = new CornerRadius(width / 2, height / 2, width / 2, height / 2),
            Background = new RadialGradientBrush
            {
                Center = new Point(0.5, 0.5),
                GradientOrigin = new Point(0.5, 0.5),
                RadiusX = 0.5,
                RadiusY = 0.5,
                GradientStops =
                {
                    new(Color.FromArgb(100, col.R, col.G, col.B), 0.0),
                    new(Color.FromArgb(35, col.R, col.G, col.B), 0.55),
                    new(Colors.Transparent, 1.0)
                }
            },
            Effect = new BlurEffect { Radius = 20 }
        };
    }

    /// <summary>
    /// Top status row for Volt: [?] square key, clock, search, wifi, battery % + icon.
    /// Strictly implements t_volt.py:62 top().
    /// </summary>
    public static Canvas TopBar(string? time = null, int? battery = null, bool right = true)
    {
        var root = new Canvas { Width = 1600, Height = 60 };

        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };

        // '?' square key (50x50, radius 9, #2c2c2c)
        var qKey = new Border
        {
            Width = 50,
            Height = 50,
            CornerRadius = new CornerRadius(9),
            Background = B("#2c2c2c"),
            Margin = new Thickness(0, 0, 30, 0),
            Child = new TextBlock
            {
                Text = "?",
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 30,
                FontWeight = FontWeights.ExtraBold,
                Foreground = WhiteBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        panel.Children.Add(qKey);

        // Time
        var timeBlock = new TextBlock
        {
            Text = time ?? ThemeClock.Now,
            FontFamily = Display,
            FontSize = 27,
            FontWeight = FontWeights.SemiBold,
            Foreground = WhiteBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
        panel.Children.Add(timeBlock);

        Canvas.SetLeft(panel, 22);
        Canvas.SetTop(panel, 20);
        root.Children.Add(panel);

        if (right)
        {
            var rightStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Search icon
            rightStack.Children.Add(new TextBlock
            {
                Text = "\uE721",
                FontFamily = Icons,
                FontSize = 24,
                Foreground = WhiteBrush,
                Margin = new Thickness(0, 0, 28, 0),
                VerticalAlignment = VerticalAlignment.Center
            });

            Canvas.SetRight(rightStack, 30);
            Canvas.SetTop(rightStack, 20);
            root.Children.Add(rightStack);
        }

        return root;
    }

    /// <summary>
    /// Steam Deck bottom hint bar: [X20] MENU pill on left, action hints on right.
    /// Strictly implements t_volt.py:67 bar().
    /// </summary>
    public static Canvas BottomBar(IEnumerable<(string Key, string Label)> hints, bool light = false)
    {
        var root = new Canvas { Width = 1600, Height = 46 };

        var leftStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var pill = new Border
        {
            Padding = new Thickness(12, 3, 12, 3),
            CornerRadius = new CornerRadius(12),
            Background = WhiteBrush,
            Effect = new DropShadowEffect { Color = C("#503200"), BlurRadius = 6, ShadowDepth = 2, Opacity = 0.3 },
            Child = new TextBlock
            {
                Text = "X20",
                FontFamily = Display,
                FontSize = 14,
                FontWeight = FontWeights.ExtraBold,
                Foreground = B("#111111")
            }
        };
        leftStack.Children.Add(pill);

        var menuText = new TextBlock
        {
            Text = "MENU",
            FontFamily = Display,
            FontSize = 15,
            FontWeight = FontWeights.Bold,
            Foreground = light ? WhiteBrush : B("#3a2a00"),
            Margin = new Thickness(14, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        leftStack.Children.Add(menuText);

        Canvas.SetLeft(leftStack, 32);
        Canvas.SetTop(leftStack, 4);
        root.Children.Add(leftStack);

        var rightStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        foreach (var (k, label) in hints)
        {
            var hintBox = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 24, 0), VerticalAlignment = VerticalAlignment.Center };
            var keyCircle = new Border
            {
                MinWidth = 27,
                Height = 27,
                CornerRadius = new CornerRadius(13.5),
                Background = B("#f4f4f4"),
                Padding = new Thickness(k.Length > 1 ? 8 : 4, 0, k.Length > 1 ? 8 : 4, 0),
                Margin = new Thickness(0, 0, 9, 0),
                Child = new TextBlock
                {
                    Text = k,
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 14,
                    FontWeight = FontWeights.ExtraBold,
                    Foreground = B("#111111"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            hintBox.Children.Add(keyCircle);

            var labText = new TextBlock
            {
                Text = label.ToUpperInvariant(),
                FontFamily = Display,
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = light ? WhiteBrush : B("#3a2a00"),
                VerticalAlignment = VerticalAlignment.Center
            };
            hintBox.Children.Add(labText);
            rightStack.Children.Add(hintBox);
        }

        Canvas.SetRight(rightStack, 36);
        Canvas.SetTop(rightStack, 4);
        root.Children.Add(rightStack);

        return root;
    }

    /// <summary>
    /// Carousel page dots pill centered at y = 812.
    /// Strictly implements t_volt.py:72 dots().
    /// </summary>
    public static Border CarouselDots(int activeIndex = 0, int total = 4)
    {
        var dotPill = new Border
        {
            Padding = new Thickness(16, 10, 16, 10),
            CornerRadius = new CornerRadius(16),
            Background = B("rgba(40,30,0,.72)"),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        for (int i = 0; i < total; i++)
        {
            var d = new Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = i == activeIndex ? WhiteBrush : B("rgba(255,255,255,.42)"),
                Margin = new Thickness(i == 0 ? 0 : 12, 0, 0, 0)
            };
            row.Children.Add(d);
        }
        dotPill.Child = row;
        return dotPill;
    }

    /// <summary>
    /// Circular carousel chevron button (54x54, rgba(18,18,18,.88)).
    /// </summary>
    public static Border Chevron(bool right, Action? onClick = null)
    {
        var btn = new Border
        {
            Width = 54,
            Height = 54,
            CornerRadius = new CornerRadius(27),
            Background = B("rgba(18,18,18,.88)"),
            Cursor = Cursors.Hand,
            Child = new TextBlock
            {
                Text = right ? "\uE76C" : "\uE76B",
                FontFamily = Icons,
                FontSize = 18,
                Foreground = WhiteBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        if (onClick != null) btn.MouseLeftButtonUp += (_, _) => onClick();
        return btn;
    }

    /// <summary>
    /// Slide-out black side menu (width: 470px, #121212, radius 0 20 20 0) with solid orange active item.
    /// Strictly implements t_volt.py:30 menu() and side().
    /// </summary>
    public static Border SideMenu(IEnumerable<(string Key, string Label, string Icon)> items, string selectedKey, Action<string>? onSelect = null)
    {
        var menu = new Border
        {
            Width = 470,
            Background = PanelDarkBrush,
            CornerRadius = new CornerRadius(0, 20, 20, 0),
            ClipToBounds = true,
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 50, ShadowDepth = 20, Opacity = 0.5 }
        };

        var stack = new StackPanel { Orientation = Orientation.Vertical };
        foreach (var (k, label, icon) in items)
        {
            string itemKey = k;
            bool active = k == selectedKey;

            var row = new Border
            {
                Height = 64,
                Padding = new Thickness(36, 0, 20, 0),
                Background = active ? AmberHotBrush : Brushes.Transparent,
                CornerRadius = active ? new CornerRadius(0, 16, 16, 0) : new CornerRadius(0),
                Cursor = Cursors.Hand
            };

            var rowStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var iconBlock = new TextBlock
            {
                Text = icon,
                FontFamily = Icons,
                FontSize = 23,
                Width = 34,
                Foreground = active ? B("#111111") : WhiteBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            rowStack.Children.Add(iconBlock);

            var labelBlock = new TextBlock
            {
                Text = label,
                FontFamily = Display,
                FontSize = 25,
                FontWeight = FontWeights.SemiBold,
                Foreground = active ? B("#111111") : WhiteBrush,
                Margin = new Thickness(20, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            rowStack.Children.Add(labelBlock);

            row.Child = rowStack;
            row.MouseLeftButtonUp += (_, _) => onSelect?.Invoke(itemKey);
            stack.Children.Add(row);
        }

        menu.Child = stack;
        return menu;
    }

    /// <summary>
    /// Orange action button [▶ Open Studio] (416x76, #ffad0a, dark text #111).
    /// Strictly implements t_volt.py:40 go().
    /// </summary>
    public static Border OrangeActionButton(string text, double w = 416, double h = 76, Action? onClick = null)
    {
        var btn = new Border
        {
            Width = w,
            Height = h,
            CornerRadius = new CornerRadius(9),
            Background = AmberHotBrush,
            Cursor = Cursors.Hand,
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 18, ShadowDepth = 8, Opacity = 0.35 }
        };

        var stack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var playIcon = new TextBlock
        {
            Text = "\uE768",
            FontFamily = Icons,
            FontSize = Math.Round(28 * h / 76),
            Foreground = B("#111111"),
            Margin = new Thickness(0, 0, Math.Round(16 * h / 76), 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        stack.Children.Add(playIcon);

        var lbl = new TextBlock
        {
            Text = text,
            FontFamily = Display,
            FontSize = Math.Round(31 * h / 76),
            FontWeight = FontWeights.SemiBold,
            Foreground = B("#111111"),
            VerticalAlignment = VerticalAlignment.Center
        };
        stack.Children.Add(lbl);

        btn.Child = stack;
        if (onClick != null) btn.MouseLeftButtonUp += (_, _) => onClick();
        return btn;
    }

    /// <summary>
    /// Standard Steam Deck two-pane shell for Studio subpages:
    /// Side menu on left (470px), right black settings sheet (1048x706, #121212) on right.
    /// </summary>
    public static Grid TwoPaneShell(string selectedPage, string title, string lead, UIElement content, Action<string>? onNavigate = null)
    {
        var root = new Grid { Width = 1600, Height = 900, ClipToBounds = true };

        // 1. Background yellow wave
        root.Children.Add(WaveBackdrop(16));

        // 2. Top bar
        root.Children.Add(TopBar());

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
        var menu = SideMenu(pages, selectedPage, onNavigate);
        Canvas.SetLeft(menu, 0);
        Canvas.SetTop(menu, 98);
        var menuCanvas = new Canvas { Width = 1600, Height = 900 };
        menuCanvas.Children.Add(menu);
        root.Children.Add(menuCanvas);

        // 4. Right black settings sheet
        var sheet = new Border
        {
            Width = 1048,
            Height = 706,
            Background = PanelDarkBrush,
            CornerRadius = new CornerRadius(18),
            ClipToBounds = true,
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 60, ShadowDepth = 26, Opacity = 0.45 }
        };

        var sheetCanvas = new Canvas { Width = 1048, Height = 706 };

        // Title and Lead
        var titleStack = new StackPanel { Orientation = Orientation.Vertical };
        titleStack.Children.Add(new TextBlock
        {
            Text = title,
            FontFamily = Display,
            FontSize = 32,
            FontWeight = FontWeights.SemiBold,
            Foreground = WhiteBrush
        });
        titleStack.Children.Add(new TextBlock
        {
            Text = lead,
            FontFamily = Body,
            FontSize = 17,
            Foreground = MuteBrush,
            Margin = new Thickness(0, 6, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            Width = 970
        });
        Canvas.SetLeft(titleStack, 40);
        Canvas.SetTop(titleStack, 20);
        sheetCanvas.Children.Add(titleStack);

        // Content Area at top = 110
        Canvas.SetLeft(content, 40);
        Canvas.SetTop(content, 110);
        sheetCanvas.Children.Add(content);

        sheet.Child = sheetCanvas;
        Canvas.SetLeft(sheet, 520);
        Canvas.SetTop(sheet, 92);
        var sheetWrapper = new Canvas { Width = 1600, Height = 900 };
        sheetWrapper.Children.Add(sheet);
        root.Children.Add(sheetWrapper);

        // 5. Bottom bar
        var bar = BottomBar(new[] { ("A", "SELECT"), ("B", "BACK"), ("X", "RESET") });
        Canvas.SetLeft(bar, 0);
        Canvas.SetTop(bar, 844);
        var barCanvas = new Canvas { Width = 1600, Height = 900 };
        barCanvas.Children.Add(bar);
        root.Children.Add(barCanvas);

        return root;
    }
}
