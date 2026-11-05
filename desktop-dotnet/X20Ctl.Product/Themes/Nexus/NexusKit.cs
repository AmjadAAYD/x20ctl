using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace X20Ctl.Product.Nexus;

/// <summary>
/// Design system primitives, colors, typography, and controls for the Nexus theme:
/// "The Xbox home: key-art band, top bar, dark graphite tiles, and green focus."
/// Strictly implements t_nexus.py CSS and specifications.
/// </summary>
public static class NexusKit
{
    // Theme Colors
    public static readonly Color Green = Color.FromRgb(0x6c, 0xc6, 0x44);       // #6cc644 Bright Xbox Green focus
    public static readonly Color GreenDeep = Color.FromRgb(0x10, 0x7c, 0x10);   // #107c10 Xbox Green fill
    public static readonly Color GreenHot = Color.FromRgb(0x9b, 0xe0, 0x7a);    // #9be07a Light highlight green
    public static readonly Color House = Color.FromRgb(0x10, 0x11, 0x13);       // #101113 Background
    public static readonly Color PanelBg = Color.FromRgb(0x1d, 0x1f, 0x23);     // #1d1f23 Panels
    public static readonly Color PanelDeep = Color.FromRgb(0x17, 0x19, 0x1c);   // #17191c Deep panel
    public static readonly Color TileBg = Color.FromRgb(0x24, 0x27, 0x2c);      // #24272c Tile background
    public static readonly Color BtnBg = Color.FromArgb(0xdc, 0x28, 0x2b, 0x30);// rgba(40,43,48,.86)
    public static readonly Color BtnFocused = Color.FromArgb(0xf2, 0x3c, 0x40, 0x46); // rgba(60,64,70,.95)
    public static readonly Color InputBg = Color.FromRgb(0x2d, 0x30, 0x36);     // #2d3036 Dropdowns/keys
    public static readonly Color TrackBg = Color.FromRgb(0x3a, 0x3d, 0x44);     // #3a3d44 Slider track
    public static readonly Color TextWhite = Colors.White;
    public static readonly Color TextSub = Color.FromArgb(0x99, 0xff, 0xff, 0xff); // rgba(255,255,255,.6)
    public static readonly Color TextMute = Color.FromArgb(0x66, 0xff, 0xff, 0xff);// rgba(255,255,255,.4)

    // Face Button Colors
    public static readonly Color FaceA = Color.FromRgb(0x6c, 0xc6, 0x44);       // Green
    public static readonly Color FaceB = Color.FromRgb(0xe3, 0x36, 0x3a);       // Red
    public static readonly Color FaceX = Color.FromRgb(0x2a, 0x7f, 0xde);       // Blue
    public static readonly Color FaceY = Color.FromRgb(0xf2, 0xc3, 0x2a);       // Yellow

    // Brushes
    public static readonly SolidColorBrush GreenBrush = new(Green);
    public static readonly SolidColorBrush GreenDeepBrush = new(GreenDeep);
    public static readonly SolidColorBrush HouseBrush = new(House);
    public static readonly SolidColorBrush PanelBrush = new(PanelBg);
    public static readonly SolidColorBrush TileBrush = new(TileBg);
    public static readonly SolidColorBrush BtnBrush = new(BtnBg);
    public static readonly SolidColorBrush BtnFocusedBrush = new(BtnFocused);
    public static readonly SolidColorBrush InputBrush = new(InputBg);
    public static readonly SolidColorBrush TrackBrush = new(TrackBg);
    public static readonly SolidColorBrush WhiteBrush = Brushes.White;
    public static readonly SolidColorBrush SubBrush = new(TextSub);
    public static readonly SolidColorBrush SliderTrackBrush = new(Color.FromRgb(0x5A, 0x60, 0x6B));
    public static readonly SolidColorBrush MuteBrush = new(TextMute);

    // Fonts
    public static readonly FontFamily Display = new("Segoe UI Variable Display, Segoe UI");
    public static readonly FontFamily Body = new("Segoe UI Variable Text, Segoe UI");
    public static readonly FontFamily Icons = new("Segoe Fluent Icons, Segoe MDL2 Assets");

    static NexusKit()
    {
        GreenBrush.Freeze();
        GreenDeepBrush.Freeze();
        HouseBrush.Freeze();
        PanelBrush.Freeze();
        TileBrush.Freeze();
        BtnBrush.Freeze();
        BtnFocusedBrush.Freeze();
        InputBrush.Freeze();
        TrackBrush.Freeze();
        SubBrush.Freeze();
        MuteBrush.Freeze();
    }

    public static SolidColorBrush B(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }
    public static SolidColorBrush B(string hex) => B((Color)ColorConverter.ConvertFromString(hex));
    public static Color C(string hex) => (Color)ColorConverter.ConvertFromString(hex);
    public static Color A(Color c, double op) => Color.FromArgb((byte)Math.Clamp(op * 255, 0, 255), c.R, c.G, c.B);

    private static readonly Dictionary<string, (string a, string b)> CoverBgs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["x20"] = ("#a7afc2", "#3a4053"),
        ["x20-black"] = ("#3a2f5c", "#0d0c14"),
        ["x20pro"] = ("#56606f", "#14171d"),
        ["x15-wukong"] = ("#e2c27c", "#3a2a12"),
        ["x15-starfield"] = ("#f1f1f5", "#c1222c"),
        ["x10"] = ("#6870b5", "#1b1f3d"),
        ["x05pro"] = ("#34344a", "#0a0a0e"),
        ["dune"] = ("#f4bd2a", "#5a3a05"),
        ["d10-purple"] = ("#d6b6f6", "#4b2a7a")
    };

    private static readonly Dictionary<string, (double fx, double fy, double z)> Focals = new(StringComparer.OrdinalIgnoreCase)
    {
        ["buttons"] = (0.74, 0.36, 2.6),
        ["curves"] = (0.28, 0.30, 2.5),
        ["macros"] = (0.5, 0.80, 1.9),
        ["vibration"] = (0.13, 0.72, 2.2),
        ["pcmode"] = (0.5, 0.42, 1.3),
        ["device"] = (0.5, 0.13, 2.4),
        ["mouse"] = (0.62, 0.58, 2.6),
        ["setups"] = (0.5, 0.5, 1.05),
        ["tools"] = (0.36, 0.56, 2.6),
        ["dashboard"] = (0.5, 0.5, 1.0)
    };

    /// <summary>
    /// Cover tile: Crops controller art towards focal point with finish radial background.
    /// Strictly implements kit.py:152 cover().
    /// </summary>
    public static FrameworkElement CoverTile(string key, double w, double h, string page = "setups", double dark = 0, double radius = 0)
    {
        string safe = key.Replace("_", "-").ToLowerInvariant();
        if (safe.Contains("starfield")) safe = "x15-starfield";
        else if (safe.Contains("wukong")) safe = "x15-wukong";
        else if (safe.Contains("purple")) safe = "d10-purple";
        else if (safe == "x05" || safe == "x05a") safe = "x05pro";

        var (c1, c2) = CoverBgs.TryGetValue(safe, out var bgPair) ? bgPair : ("#56606f", "#14171d");
        var (fx, fy, z) = Focals.TryGetValue(page, out var foc) ? foc : (0.5, 0.5, 1.0);

        if (w / h < 0.9 && (page == "setups" || page == "dashboard"))
            z = Math.Max(z, 1.55);

        double iw = (w / h < 1.2) ? (h * 0.95 * z) : (w * z);
        double ih = iw * 0.65;
        double left = w / 2 - fx * iw;
        double top = h / 2 - fy * ih;
        left = Math.Min(0, Math.Max(w - iw, left));
        top = Math.Min(h * 0.1, Math.Max(h - ih, top));

        var border = new Border
        {
            Width = w,
            Height = h,
            CornerRadius = new CornerRadius(radius),
            ClipToBounds = true
        };

        var canvas = new Canvas { Width = w, Height = h };

        var bg = new Rectangle
        {
            Width = w,
            Height = h,
            Fill = new RadialGradientBrush
            {
                Center = new Point(0.5, 0.2),
                GradientOrigin = new Point(0.5, 0.2),
                RadiusX = 1.2,
                RadiusY = 0.9,
                GradientStops =
                {
                    new(C(c1), 0.0),
                    new(C(c2), 1.0)
                }
            }
        };
        canvas.Children.Add(bg);

        var art = ControllerArt(safe, iw);
        if (art != null)
        {
            art.Effect = new DropShadowEffect { BlurRadius = 18, ShadowDepth = 10, Direction = 270, Opacity = 0.45, Color = Colors.Black };
            Canvas.SetLeft(art, left);
            Canvas.SetTop(art, top);
            canvas.Children.Add(art);
        }

        if (dark > 0)
        {
            var shade = new Rectangle
            {
                Width = w,
                Height = h,
                Fill = new LinearGradientBrush
                {
                    StartPoint = new Point(0.5, 1),
                    EndPoint = new Point(0.5, 0),
                    GradientStops =
                    {
                        new(A(Colors.Black, dark), 0.0),
                        new(Colors.Transparent, 0.6)
                    }
                }
            };
            canvas.Children.Add(shade);
        }

        border.Child = canvas;
        return border;
    }

    /// <summary>
    /// Nexus Key-Art Band across the top 560px with diagonal perspective sheen lines,
    /// controller heroes in perspective, and gradient fade down to #101113.
    /// Strictly implements t_nexus.py:25 keyband().
    /// </summary>
    public static Canvas KeyArtBand(double h = 560, double opacity = 1.0, string featModel = "x20pro", string secondModel = "x15-starfield")
    {
        var canvas = new Canvas { Width = 1600, Height = h, ClipToBounds = true, Opacity = opacity };

        // 1. Perspective Sky Gradient (175deg linear-gradient)
        var sky = new Rectangle
        {
            Width = 1600,
            Height = h,
            Fill = new LinearGradientBrush
            {
                StartPoint = new Point(0.481, -0.12),
                EndPoint = new Point(0.519, 1.12),
                GradientStops =
                {
                    new(C("#dfe5ec"), 0.0),
                    new(C("#aab4bf"), 0.38),
                    new(C("#4d5560"), 0.70),
                    new(House, 1.0)
                }
            }
        };
        canvas.Children.Add(sky);

        // 2. 14 diagonal perspective sheen lines rotated -8deg
        for (int i = 0; i < 14; i++)
        {
            double y = 170 + i * 26;
            double x = -200 + i * 30;
            double lineH = 2 + (i % 3);
            double lineOp = 0.3 + (i % 4) * 0.1;

            var line = new Rectangle
            {
                Width = 1000,
                Height = lineH,
                Fill = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0.5),
                    EndPoint = new Point(1, 0.5),
                    GradientStops =
                    {
                        new(Colors.Transparent, 0.0),
                        new(A(Colors.White, 0.55), 0.5),
                        new(Colors.Transparent, 1.0)
                    }
                },
                Opacity = lineOp,
                RenderTransform = new RotateTransform(-8)
            };
            Canvas.SetLeft(line, x);
            Canvas.SetTop(line, y);
            canvas.Children.Add(line);
        }

        // 3. Ground perspective dark band
        var ground = new Rectangle
        {
            Width = 1900,
            Height = 180,
            Fill = new LinearGradientBrush
            {
                StartPoint = new Point(0.5, 0),
                EndPoint = new Point(0.5, 1),
                GradientStops =
                {
                    new(A(C("#282c32"), 0.0), 0.0),
                    new(A(C("#282c32"), 0.75), 1.0)
                }
            },
            RenderTransform = new RotateTransform(-4)
        };
        Canvas.SetLeft(ground, -100);
        Canvas.SetTop(ground, 440);
        canvas.Children.Add(ground);

        // 4. Secondary controller: left side rotated -14deg at (140, 70, w=640)
        var secondArt = ControllerArt(secondModel, 640);
        if (secondArt != null)
        {
            secondArt.RenderTransformOrigin = new Point(0.5, 0.5);
            secondArt.RenderTransform = new RotateTransform(-14);
            secondArt.Effect = new DropShadowEffect { BlurRadius = 30, ShadowDepth = 30, Direction = 270, Opacity = 0.45, Color = Colors.Black };
            Canvas.SetLeft(secondArt, 140);
            Canvas.SetTop(secondArt, 70);
            canvas.Children.Add(secondArt);
        }

        // 5. Featured controller: center-right rotated 8deg at (700, 120, w=780)
        var featArt = ControllerArt(featModel, 780);
        if (featArt != null)
        {
            featArt.RenderTransformOrigin = new Point(0.5, 0.5);
            featArt.RenderTransform = new RotateTransform(8);
            featArt.Effect = new DropShadowEffect { BlurRadius = 40, ShadowDepth = 40, Direction = 270, Opacity = 0.55, Color = Colors.Black };
            Canvas.SetLeft(featArt, 700);
            Canvas.SetTop(featArt, 120);
            canvas.Children.Add(featArt);
        }

        // 6. Bottom dark fade gradient to seamless #101113
        var fade = new Rectangle
        {
            Width = 1600,
            Height = 240,
            Fill = new LinearGradientBrush
            {
                StartPoint = new Point(0.5, 1),
                EndPoint = new Point(0.5, 0),
                GradientStops =
                {
                    new(House, 0.0),
                    new(A(House, 0.0), 1.0)
                }
            }
        };
        Canvas.SetLeft(fade, 0);
        Canvas.SetTop(fade, h - 240);
        canvas.Children.Add(fade);

        return canvas;
    }

    /// <summary>
    /// Nexus Top Bar: avatar + player info left, round buttons center, status & clock right.
    /// Strictly implements t_nexus.py:36 top().
    /// </summary>
    public static FrameworkElement TopBar(
        int player = 0,
        string model = "EasySMX X20 Pro",
        string status = "Offline",
        Action? onZone = null,
        Action? onSetups = null,
        Action? onPcMode = null,
        Action? onTools = null)
    {
        var bar = new Canvas { Width = 1476, Height = 60 };

        // 1. Left Avatar (58x58 circle with 2px white ring) + Player & Model Text
        var leftStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        
        var avatarBorder = new Border
        {
            Width = 58,
            Height = 58,
            CornerRadius = new CornerRadius(29),
            BorderBrush = B(A(Colors.White, 0.4)),
            BorderThickness = new Thickness(2),
            Background = B("#1a1c20"),
            ClipToBounds = true,
            Margin = new Thickness(0, 0, 16, 0)
        };
        avatarBorder.Child = CoverTile("x20pro", 58, 58, "buttons", radius: 29);
        leftStack.Children.Add(avatarBorder);

        var playerTextStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var playerRow = new StackPanel { Orientation = Orientation.Horizontal };
        playerRow.Children.Add(new TextBlock
        {
            Text = $"Player {player + 1}",
            FontFamily = Body,
            FontSize = 19,
            FontWeight = FontWeights.SemiBold,
            Foreground = WhiteBrush
        });
        var localBadge = new Border
        {
            Background = WhiteBrush,
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(6, 1, 6, 1),
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = "LOCAL",
                FontFamily = Body,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = B("#111111")
            }
        };
        playerRow.Children.Add(localBadge);
        playerTextStack.Children.Add(playerRow);

        playerTextStack.Children.Add(new TextBlock
        {
            Text = $"{model} · {status}",
            FontFamily = Body,
            FontSize = 15,
            Foreground = SubBrush,
            Margin = new Thickness(0, 2, 0, 0)
        });
        leftStack.Children.Add(playerTextStack);

        Canvas.SetLeft(leftStack, 0);
        Canvas.SetTop(leftStack, 0);
        bar.Children.Add(leftStack);

        // 2. Center round buttons (46x46 rb): setups, zone, pcmode, search, settings
        var midStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var icons = new (string Icon, Action? Act, string Tip)[]
        {
            ("\uE8B5", onSetups, "Setups"),
            ("\uE7FC", onZone, "Controller Zone"),
            ("\uE9A6", onPcMode, "PC Mode"),
            ("\uE721", null, "Search"),
            ("\uE713", onTools, "Tools & Settings")
        };
        foreach (var (ic, act, tip) in icons)
        {
            var btn = new Border
            {
                Width = 46,
                Height = 46,
                CornerRadius = new CornerRadius(23),
                Background = B(A(C("#1e2024"), 0.7)),
                Margin = new Thickness(11, 0, 11, 0),
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = tip,
                Child = new TextBlock
                {
                    Text = ic,
                    FontFamily = Icons,
                    FontSize = 19,
                    Foreground = WhiteBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            if (act != null) btn.MouseLeftButtonDown += (_, _) => act();
            midStack.Children.Add(btn);
        }

        Canvas.SetLeft(midStack, (1476 - 340) / 2.0);
        Canvas.SetTop(midStack, 7);
        bar.Children.Add(midStack);

        // 3. Right Status: sound, battery, clock
        var rightStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        rightStack.Children.Add(new TextBlock
        {
            Text = "\uE767", // Volume
            FontFamily = Icons,
            FontSize = 19,
            Foreground = WhiteBrush,
            Margin = new Thickness(0, 0, 26, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        rightStack.Children.Add(new TextBlock
        {
            Text = "\uE83F", // Battery
            FontFamily = Icons,
            FontSize = 19,
            Foreground = WhiteBrush,
            Margin = new Thickness(0, 0, 26, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        rightStack.Children.Add(new TextBlock
        {
            Text = ReviewSandbox.Active ? "19:24" : DateTime.Now.ToString("HH:mm"),
            FontFamily = Body,
            FontSize = 19,
            Foreground = WhiteBrush,
            VerticalAlignment = VerticalAlignment.Center
        });

        Canvas.SetRight(rightStack, 0);
        Canvas.SetTop(rightStack, 16);
        bar.Children.Add(rightStack);

        return bar;
    }

    /// <summary>
    /// Nexus Tile with 6px rounded corners, label at bottom-left, and 4px green focus ring.
    /// Strictly implements t_nexus.py:14 .tile.
    /// </summary>
    public static Border Tile(
        double w, double h,
        UIElement? content,
        string? label = null,
        bool focused = false,
        Action? onClick = null,
        double cornerRadius = 6)
    {
        var tile = new Border
        {
            Width = w,
            Height = h,
            CornerRadius = new CornerRadius(cornerRadius),
            Background = TileBrush,
            ClipToBounds = true,
            Cursor = System.Windows.Input.Cursors.Hand
        };

        if (focused)
        {
            tile.BorderBrush = GreenBrush;
            tile.BorderThickness = new Thickness(4);
            tile.Effect = new DropShadowEffect
            {
                BlurRadius = 30,
                ShadowDepth = 10,
                Direction = 270,
                Opacity = 0.6,
                Color = Colors.Black
            };
        }

        var grid = new Grid();
        if (content != null) grid.Children.Add(content);

        if (!string.IsNullOrEmpty(label))
        {
            var labelText = new TextBlock
            {
                Text = label,
                FontFamily = Body,
                FontSize = 17,
                FontWeight = FontWeights.SemiBold,
                Foreground = WhiteBrush,
                Margin = new Thickness(14, 0, 0, 12),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Bottom,
                Effect = new DropShadowEffect { BlurRadius = 8, ShadowDepth = 1, Direction = 270, Opacity = 0.8, Color = Colors.Black }
            };
            grid.Children.Add(labelText);
        }

        tile.Child = grid;
        if (onClick != null) tile.MouseLeftButtonDown += (_, _) => onClick();
        return tile;
    }

    /// <summary>
    /// Nexus Row Button: min-height 62px, 6px rounded corners, dark background, 4px green outline when focused.
    /// Strictly implements t_nexus.py:162 .z-row.
    /// </summary>
    public static Border RowButton(
        string title,
        string? subtitle = null,
        UIElement? rightContent = null,
        bool focused = false,
        Action? onClick = null,
        double width = 820)
    {
        var row = new Border
        {
            Width = width,
            MinHeight = 62,
            Padding = new Thickness(22, 8, 22, 8),
            Margin = new Thickness(0, 0, 0, 8),
            CornerRadius = new CornerRadius(6),
            Background = focused ? BtnFocusedBrush : BtnBrush,
            Cursor = System.Windows.Input.Cursors.Hand
        };

        if (focused)
        {
            row.BorderBrush = GreenBrush;
            row.BorderThickness = new Thickness(4);
        }

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        textStack.Children.Add(new TextBlock
        {
            Text = title,
            FontFamily = Body,
            FontSize = 19,
            Foreground = WhiteBrush
        });
        if (!string.IsNullOrEmpty(subtitle))
        {
            textStack.Children.Add(new TextBlock
            {
                Text = subtitle,
                FontFamily = Body,
                FontSize = 14,
                Foreground = SubBrush,
                Margin = new Thickness(0, 3, 0, 0)
            });
        }
        Grid.SetColumn(textStack, 0);
        grid.Children.Add(textStack);

        if (rightContent != null)
        {
            Grid.SetColumn(rightContent as FrameworkElement ?? new Border(), 1);
            grid.Children.Add(rightContent);
        }

        row.Child = grid;
        if (onClick != null) row.MouseLeftButtonDown += (_, _) => onClick();
        return row;
    }

    /// <summary>
    /// Nexus Xbox Toggle Switch: 50x24 pill, green when on.
    /// Strictly implements t_nexus.py:167 .z-tg.
    /// </summary>
    public static Border Toggle(bool on, Action? onToggle = null)
    {
        var track = new Border
        {
            Width = 50,
            Height = 24,
            CornerRadius = new CornerRadius(12),
            Background = on ? GreenBrush : Brushes.Transparent,
            BorderBrush = on ? GreenBrush : B(A(Colors.White, 0.75)),
            BorderThickness = new Thickness(2),
            Cursor = System.Windows.Input.Cursors.Hand
        };

        var thumb = new Border
        {
            Width = 12,
            Height = 12,
            CornerRadius = new CornerRadius(6),
            Background = on ? HouseBrush : WhiteBrush,
            HorizontalAlignment = on ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            Margin = new Thickness(on ? 0 : 4, 0, on ? 4 : 0, 0)
        };
        track.Child = thumb;

        if (onToggle != null) track.MouseLeftButtonDown += (_, _) => onToggle();
        return track;
    }

    /// <summary>
    /// Nexus Horizontal Slider: 210px width, 4px track #3a3d44, green fill #6cc644, white rectangular thumb.
    /// Strictly implements t_nexus.py:170 .z-sl.
    /// </summary>
    public static FrameworkElement Slider(double value, double min = 0, double max = 100, double width = 210)
    {
        var canvas = new Canvas { Width = width, Height = 24 };

        double pct = Math.Clamp((value - min) / (max - min), 0, 1);

        var track = new Rectangle
        {
            Width = width,
            Height = 4,
            Fill = TrackBrush
        };
        Canvas.SetLeft(track, 0);
        Canvas.SetTop(track, 10);
        canvas.Children.Add(track);

        var fill = new Rectangle
        {
            Width = width * pct,
            Height = 4,
            Fill = GreenBrush
        };
        Canvas.SetLeft(fill, 0);
        Canvas.SetTop(fill, 10);
        canvas.Children.Add(fill);

        var thumb = new Border
        {
            Width = 12,
            Height = 24,
            CornerRadius = new CornerRadius(3),
            Background = WhiteBrush
        };
        Canvas.SetLeft(thumb, width * pct - 6);
        Canvas.SetTop(thumb, 0);
        canvas.Children.Add(thumb);

        return canvas;
    }

    /// <summary>
    /// Nexus Dropdown Box: 44px height #2d3036 box with chevron.
    /// Strictly implements t_nexus.py:172 .z-dd.
    /// </summary>
    public static Border Dropdown(string text, double minWidth = 200, bool focused = false)
    {
        var dd = new Border
        {
            MinWidth = minWidth,
            Height = 44,
            Padding = new Thickness(16, 0, 16, 0),
            CornerRadius = new CornerRadius(4),
            Background = InputBrush,
            Cursor = System.Windows.Input.Cursors.Hand
        };

        if (focused)
        {
            dd.BorderBrush = GreenBrush;
            dd.BorderThickness = new Thickness(3);
        }

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var label = new TextBlock
        {
            Text = text,
            FontFamily = Body,
            FontSize = 16,
            Foreground = WhiteBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(label, 0);
        grid.Children.Add(label);

        var chev = new TextBlock
        {
            Text = "▾",
            FontFamily = Body,
            FontSize = 14,
            Foreground = SubBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0)
        };
        Grid.SetColumn(chev, 1);
        grid.Children.Add(chev);

        dd.Child = grid;
        return dd;
    }

    /// <summary>
    /// Nexus Button: 50px height, 4px corner radius, #2d3036 or #3c4046 with 3px green ring when primary.
    /// Strictly implements t_nexus.py:173 .z-btn.
    /// </summary>
    public static Border TheatricalButton(string text, bool primary = false, string? key = null, Action? onClick = null, double height = 50, double fontSize = 16)
    {
        var btn = new Border
        {
            Height = height,
            Padding = new Thickness(26, 0, 26, 0),
            CornerRadius = new CornerRadius(4),
            Background = primary ? B("#3c4046") : InputBrush,
            Cursor = System.Windows.Input.Cursors.Hand
        };

        if (primary)
        {
            btn.BorderBrush = GreenBrush;
            btn.BorderThickness = new Thickness(3);
        }

        var stack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        if (!string.IsNullOrEmpty(key))
        {
            stack.Children.Add(FaceKey(key));
            var spacer = new Border { Width = 10 };
            stack.Children.Add(spacer);
        }

        stack.Children.Add(new TextBlock
        {
            Text = text,
            FontFamily = Body,
            FontSize = fontSize,
            FontWeight = FontWeights.SemiBold,
            Foreground = WhiteBrush,
            VerticalAlignment = VerticalAlignment.Center
        });

        btn.Child = stack;
        if (onClick != null) btn.MouseLeftButtonDown += (_, _) => onClick();
        return btn;
    }

    /// <summary>
    /// Circular colored face button: A (green #6cc644), B (red #e3363a), X (blue #2a7fde), Y (yellow #f2c32a).
    /// Strictly implements t_nexus.py:205 k(c).
    /// </summary>
    public static Border FaceKey(string letter)
    {
        Color bg = letter switch
        {
            "A" => FaceA,
            "B" => FaceB,
            "X" => FaceX,
            "Y" => FaceY,
            _ => InputBg
        };
        bool isFace = letter is "A" or "B" or "X" or "Y";

        return new Border
        {
            MinWidth = 28,
            Height = 28,
            Padding = new Thickness(6, 0, 6, 0),
            CornerRadius = new CornerRadius(14),
            Background = B(bg),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = letter,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = isFace ? HouseBrush : WhiteBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
    }

    /// <summary>
    /// Nexus Footer Hints Bar with colored face buttons.
    /// Strictly implements t_nexus.py:208 keys(items).
    /// </summary>
    public static FrameworkElement HintsBar(IEnumerable<(string Key, string Label)> hints)
    {
        var stack = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var (k, l) in hints)
        {
            var item = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 24, 0), VerticalAlignment = VerticalAlignment.Center };
            item.Children.Add(FaceKey(k));
            item.Children.Add(new TextBlock
            {
                Text = l,
                FontFamily = Body,
                FontSize = 16,
                Foreground = WhiteBrush,
                Margin = new Thickness(9, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            stack.Children.Add(item);
        }
        return stack;
    }

    /// <summary>
    /// Controller artwork loader.
    /// </summary>
    public static FrameworkElement ControllerArt(string modelId, double width)
    {
        if (ThemeArt.Load(modelId, width) is { } sharedArt) return sharedArt;
        string safeModel = modelId.Replace("_", "-").ToLowerInvariant();
        string artKey = safeModel switch
        {
            "x20-pro" or "x20pro" => "x20pro",
            "x05-pro" or "x05pro" or "x05" or "x05a" => "x05pro",
            "x15-wukong" => "x15-wukong",
            "x15-starfield" => "x15-starfield",
            "x15" or "x15pro" => "x15-starfield",
            "x10" => "x10",
            "d10-purple" or "d10" => "d10-purple",
            "x20-black" => "x20-black",
            "x20" => "x20",
            "dune" => "dune",
            _ => safeModel
        };

        string nativeFolder = safeModel switch
        {
            "x05" or "x05a" or "x05pro" or "x05-pro" => "x05_pro",
            "x20pro" or "x20-pro" => "x20_pro",
            "x15" or "x15-starfield" or "x15pro" or "x15-wukong" => "x15",
            "x10" => "x10",
            "d10" or "d10-purple" => "d10",
            "x20" or "x20-black" => "x20",
            "dune" => "dune",
            _ => safeModel.Replace("-", "_")
        };

        string appDir = AppContext.BaseDirectory;
        string? artDir = null;
        for (int up = 2; up <= 8; up++)
        {
            string upSteps = string.Join(System.IO.Path.DirectorySeparatorChar.ToString(), Enumerable.Repeat("..", up));
            string candidateRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(appDir, upSteps));
            string cand = System.IO.Path.Combine(candidateRoot, "previews", "console-concepts", "art");
            if (Directory.Exists(cand)) { artDir = cand; break; }
            cand = System.IO.Path.Combine(candidateRoot, "worktree-gemini", "previews", "console-concepts", "art");
            if (Directory.Exists(cand)) { artDir = cand; break; }
        }

        if (artDir != null)
        {
            string[] candidates =
            {
                System.IO.Path.Combine(artDir, $"{artKey}.png"),
                System.IO.Path.Combine(artDir, $"{safeModel}.png"),
                System.IO.Path.Combine(artDir, $"{nativeFolder}.png"),
                System.IO.Path.Combine(artDir, "x20pro.png"),
                System.IO.Path.Combine(artDir, "x05pro.png"),
                System.IO.Path.Combine(artDir, "x15-starfield.png"),
                System.IO.Path.Combine(artDir, "x20.png")
            };

            foreach (var p in candidates)
            {
                if (File.Exists(p))
                {
                    try
                    {
                        var bmp = new System.Windows.Media.Imaging.BitmapImage();
                        bmp.BeginInit();
                        bmp.UriSource = new Uri(System.IO.Path.GetFullPath(p), UriKind.Absolute);
                        bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                        bmp.EndInit();
                        bmp.Freeze();

                        var img = new Image { Source = bmp, Width = width, Stretch = Stretch.Uniform };
                        RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                        return img;
                    }
                    catch { }
                }
            }
        }

        // Try loading embedded pack resource
        try
        {
            var packUri = new Uri($"pack://application:,,,/Assets/native/{nativeFolder}/native/front-full.png");
            var bmp = new System.Windows.Media.Imaging.BitmapImage(packUri);
            var img = new Image { Source = bmp, Width = width, Stretch = Stretch.Uniform };
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
            return img;
        }
        catch { }

        try
        {
            var packUri = new Uri("pack://application:,,,/Assets/native/x20/native/front-full.png");
            var bmp = new System.Windows.Media.Imaging.BitmapImage(packUri);
            var img = new Image { Source = bmp, Width = width, Stretch = Stretch.Uniform };
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
            return img;
        }
        catch { }

        // Scaled fallback in case no image can be loaded
        var outline = new ControllerOutline();
        outline.Show(nativeFolder, false);
        return new Viewbox
        {
            Width = width,
            Height = width * (1024.0 / 1536.0),
            Stretch = Stretch.Uniform,
            Child = outline
        };
    }

    /// <summary>
    /// Loads a theme dashboard preview thumbnail for the theme selector tiles.
    /// Strictly matches t_nexus.py:148 tools() thumbs[k].
    /// </summary>
    public static FrameworkElement ThemeThumb(string themeKey, double width, double height)
    {
        string safeKey = themeKey.ToLowerInvariant();

        string appDir = AppContext.BaseDirectory;
        string? foundPath = null;

        for (int up = 2; up <= 8; up++)
        {
            string upSteps = string.Join(System.IO.Path.DirectorySeparatorChar.ToString(), Enumerable.Repeat("..", up));
            string candidateRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(appDir, upSteps));
            
            string cand = System.IO.Path.Combine(candidateRoot, "previews", "console-concepts", "themes", "SEND-TO-CHATGPT", "boards", safeKey, "dashboard.png");
            if (File.Exists(cand)) { foundPath = cand; break; }

            cand = System.IO.Path.Combine(candidateRoot, "worktree-gemini", "previews", "console-concepts", "themes", "SEND-TO-CHATGPT", "boards", safeKey, "dashboard.png");
            if (File.Exists(cand)) { foundPath = cand; break; }
        }

        if (foundPath != null)
        {
            try
            {
                var bmp = new System.Windows.Media.Imaging.BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(System.IO.Path.GetFullPath(foundPath), UriKind.Absolute);
                bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();

                var img = new Image
                {
                    Source = bmp,
                    Width = width,
                    Height = height,
                    Stretch = Stretch.UniformToFill
                };
                RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                return img;
            }
            catch { }
        }

        return new Rectangle
        {
            Width = width,
            Height = height,
            Fill = TileBrush
        };
    }
}
