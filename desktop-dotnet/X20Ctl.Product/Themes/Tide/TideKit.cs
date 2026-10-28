using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace X20Ctl.Product.Tide;

/// <summary>
/// Design system primitives, colors, typography, and controls for the Tide theme:
/// "Deep blue into cyan with sharp light shards, giant vertical word down the left edge,
/// white outlined status box, slanted menu words with red spikes, tilted black keys,
/// and high-contrast Persona 3 aesthetic."
/// Strictly implements t_tide.py specifications.
/// </summary>
public static class TideKit
{
    // Theme Colors
    public static readonly Color Sea0 = Color.FromRgb(0x0b, 0x1d, 0x8e);        // #0b1d8e Deep blue
    public static readonly Color Sea1 = Color.FromRgb(0x18, 0x38, 0xd8);        // #1838d8
    public static readonly Color Sea2 = Color.FromRgb(0x1f, 0x6f, 0xe8);        // #1f6fe8 Sky blue
    public static readonly Color Sea3 = Color.FromRgb(0x28, 0xb6, 0xf0);        // #28b6f0 Cyan
    public static readonly Color LightCyan = Color.FromRgb(0x5f, 0xd3, 0xff);    // #5fd3ff Accent light cyan
    public static readonly Color SpikeRed = Color.FromRgb(0xff, 0x1f, 0x3d);     // #ff1f3d Action red
    public static readonly Color Pink = Color.FromRgb(0xff, 0x2a, 0x7a);         // #ff2a7a Confetti pink
    public static readonly Color Purple = Color.FromRgb(0xc5, 0x1d, 0xff);       // #c51dff Confetti purple
    public static readonly Color Ink = Color.FromRgb(0x0b, 0x0b, 0x16);          // #0b0b16 Dark ink / black
    public static readonly Color CardBg = Colors.White;                          // #ffffff White sticker cards
    public static readonly Color NavyShadow = Color.FromArgb(0x99, 0x0a, 0x14, 0x78); // rgba(10,20,120,.6)
    public static readonly Color TextSub = Color.FromRgb(0x3b, 0x4a, 0x9a);      // #3b4a9a

    // Brushes
    public static readonly SolidColorBrush Sea0Brush = new(Sea0);
    public static readonly SolidColorBrush Sea2Brush = new(Sea2);
    public static readonly SolidColorBrush LightCyanBrush = new(LightCyan);
    public static readonly SolidColorBrush SpikeRedBrush = new(SpikeRed);
    public static readonly SolidColorBrush InkBrush = new(Ink);
    public static readonly SolidColorBrush WhiteBrush = Brushes.White;
    public static readonly SolidColorBrush SubBrush = new(TextSub);
    public static readonly SolidColorBrush ShadowBrush = new(NavyShadow);

    // Fonts
    public static readonly FontFamily Display = new("Arial Black, Segoe UI Black");
    public static readonly FontFamily Body = new("Segoe UI Variable Text, Segoe UI");
    public static readonly FontFamily Icons = new("Segoe Fluent Icons");

    public static Color A(Color c, double a) => Color.FromArgb((byte)(255 * Math.Clamp(a, 0, 1)), c.R, c.G, c.B);
    public static Color C(string hex) => (Color)ColorConverter.ConvertFromString(hex);

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

    // Controller Photography Loader
    public static FrameworkElement ControllerArt(string modelId, double targetWidth)
    {
        string safeModel = modelId.Replace("_", "-").ToLowerInvariant();
        string artKey = safeModel switch
        {
            "x20-pro" or "x20pro" => "x20pro",
            "x05-pro" or "x05pro" or "x05" or "x05a" => "x05pro",
            "x15" or "x15-starfield" or "x15pro" or "x15-wukong" => "x15-starfield",
            "x10" => "x10",
            "d10" or "d10-purple" => "d10-purple",
            "x20" or "x20-black" => "x20",
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
                System.IO.Path.Combine(artDir, "x15-starfield.png"),
                System.IO.Path.Combine(artDir, "x05pro.png"),
                System.IO.Path.Combine(artDir, "x20pro.png"),
                System.IO.Path.Combine(artDir, "x20.png")
            };
            foreach (var p in candidates)
            {
                if (File.Exists(p))
                {
                    try
                    {
                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.UriSource = new Uri(System.IO.Path.GetFullPath(p), UriKind.Absolute);
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.EndInit();
                        bmp.Freeze();

                        var img = new Image { Source = bmp, Width = targetWidth, Stretch = Stretch.Uniform };
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
            var bmp = new BitmapImage(packUri);
            var img = new Image { Source = bmp, Width = targetWidth, Stretch = Stretch.Uniform };
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
            return img;
        }
        catch { }

        try
        {
            var packUri = new Uri("pack://application:,,,/Assets/native/x15/native/front-full.png");
            var bmp = new BitmapImage(packUri);
            var img = new Image { Source = bmp, Width = targetWidth, Stretch = Stretch.Uniform };
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
            return img;
        }
        catch { }

        var outline = new ControllerOutline();
        outline.Show(nativeFolder, false);
        return new Viewbox
        {
            Width = targetWidth,
            Height = targetWidth * (1024.0 / 1536.0),
            Stretch = Stretch.Uniform,
            Child = outline
        };
        return outline;
    }

    /// <summary>
    /// Persona 3 Character: Tilted large controller with high-contrast comic contour and drop shadow.
    /// </summary>
    public static FrameworkElement Character(string key, double x, double y, double w, double rot = -22)
    {
        var pad = ControllerArt(key, w);
        pad.RenderTransformOrigin = new Point(0.5, 0.5);
        pad.RenderTransform = new RotateTransform(rot);

        // Persona 3 deep comic drop shadow
        pad.Effect = new DropShadowEffect
        {
            Color = Color.FromArgb(180, 10, 20, 120),
            BlurRadius = 4,
            ShadowDepth = 18,
            Direction = 315,
            Opacity = 0.6
        };

        Canvas.SetLeft(pad, x);
        Canvas.SetTop(pad, y);
        return pad;
    }

    /// <summary>
    /// Persona 3 Sea background: Gradient + Embossed concentric rings + Shards + Confetti + Vertical Word
    /// </summary>
    public static Canvas SeaBackground(int seed = 1, string word = "DASHBOARD")
    {
        var canvas = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };

        // 1. Sea gradient: linear-gradient(115deg, #0b1d8e 0%, #1838d8 35%, #1f6fe8 62%, #28b6f0 100%)
        var bg = new Rectangle
        {
            Width = 1600,
            Height = 900,
            Fill = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0.2),
                EndPoint = new Point(1, 0.8),
                GradientStops =
                {
                    new(Sea0, 0.0),
                    new(Sea1, 0.35),
                    new(Sea2, 0.62),
                    new(Sea3, 1.0)
                }
            }
        };
        canvas.Children.Add(bg);

        // 2. Embossed concentric rings
        var ring1 = new Border
        {
            Width = 720,
            Height = 720,
            CornerRadius = new CornerRadius(360),
            BorderBrush = new SolidColorBrush(Color.FromArgb((byte)(255 * 0.10), 120, 200, 255)),
            BorderThickness = new Thickness(26)
        };
        Canvas.SetLeft(ring1, 1050);
        Canvas.SetTop(ring1, 260);
        canvas.Children.Add(ring1);

        var ring2 = new Border
        {
            Width = 460,
            Height = 460,
            CornerRadius = new CornerRadius(230),
            BorderBrush = new SolidColorBrush(Color.FromArgb((byte)(255 * 0.10), 120, 200, 255)),
            BorderThickness = new Thickness(14)
        };
        Canvas.SetLeft(ring2, 1180);
        Canvas.SetTop(ring2, 390);
        canvas.Children.Add(ring2);

        // 3. Shards of light
        var rnd = new Random(seed);
        for (int i = 0; i < 9; i++)
        {
            double sx = rnd.Next(300, 1600);
            double sy = rnd.Next(-100, 800);
            double sw = rnd.Next(200, 700);
            double sh = rnd.Next(30, 120);
            double sRot = rnd.Next(-40, -20);
            double op = 0.15 + rnd.NextDouble() * 0.20;

            var shard = new Rectangle
            {
                Width = sw,
                Height = sh,
                Fill = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0.5),
                    EndPoint = new Point(1, 0.5),
                    GradientStops =
                    {
                        new(Color.FromArgb((byte)(255 * op), 150, 230, 255), 0.0),
                        new(Colors.Transparent, 1.0)
                    }
                },
                RenderTransform = new RotateTransform(sRot)
            };
            Canvas.SetLeft(shard, sx);
            Canvas.SetTop(shard, sy);
            canvas.Children.Add(shard);
        }

        // 4. Confetti triangles
        Color[] confettiColors = [Pink, Purple, Colors.White];
        for (int i = 0; i < 14; i++)
        {
            double tx = rnd.Next(200, 900);
            double ty = rnd.Next(150, 800);
            double sz = rnd.Next(14, 40);
            double tRot = rnd.Next(0, 360);
            Color clr = confettiColors[rnd.Next(confettiColors.Length)];

            var polygon = new Polygon
            {
                Points = new PointCollection
                {
                    new Point(sz / 2, 0),
                    new Point(0, sz),
                    new Point(sz, sz)
                },
                Fill = new SolidColorBrush(clr),
                Opacity = 0.85,
                RenderTransform = new RotateTransform(tRot)
            };
            Canvas.SetLeft(polygon, tx);
            Canvas.SetTop(polygon, ty);
            canvas.Children.Add(polygon);
        }

        // 5. Giant Vertical Word down the left edge
        // .vert: left: -26px; top: 900px; transform: rotate(-90deg); transform-origin: left top; font: 900 180px 'Arial Black'
        var vertText = new TextBlock
        {
            Text = word,
            FontFamily = Display,
            FontWeight = FontWeights.Black,
            FontSize = 180,
            Foreground = new SolidColorBrush(Color.FromArgb((byte)(255 * 0.9), 235, 244, 255)),
            RenderTransformOrigin = new Point(0, 0),
            RenderTransform = new RotateTransform(-90)
        };
        Canvas.SetLeft(vertText, -26);
        Canvas.SetTop(vertText, 900);
        canvas.Children.Add(vertText);

        return canvas;
    }

    /// <summary>
    /// White-outlined Persona status box at (190, 22) or Back box.
    /// </summary>
    public static FrameworkElement StatusBox(string text = "19:24", bool isBack = false, Action onBack = null)
    {
        var box = new Border
        {
            Height = 60,
            Padding = new Thickness(24, 0, 24, 0),
            Background = WhiteBrush,
            BorderBrush = InkBrush,
            BorderThickness = new Thickness(3),
            CornerRadius = new CornerRadius(8)
        };

        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        if (isBack)
        {
            row.Children.Add(new TextBlock
            {
                Text = "\uE72B",
                FontFamily = Icons,
                FontSize = 24,
                Foreground = InkBrush,
                Margin = new Thickness(0, 0, 14, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            row.Children.Add(new TextBlock
            {
                Text = "GO BACK",
                FontFamily = Display,
                FontWeight = FontWeights.Black,
                FontSize = 27,
                Foreground = InkBrush,
                VerticalAlignment = VerticalAlignment.Center
            });
            box.MouseLeftButtonUp += (_, _) => onBack?.Invoke();
            box.Cursor = System.Windows.Input.Cursors.Hand;
        }
        else
        {
            row.Children.Add(new TextBlock
            {
                Text = text,
                FontFamily = Display,
                FontWeight = FontWeights.Black,
                FontSize = 27,
                Foreground = InkBrush,
                Margin = new Thickness(0, 0, 16, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            row.Children.Add(new TextBlock
            {
                Text = "\uE701",
                FontFamily = Icons,
                FontSize = 24,
                Foreground = InkBrush,
                Margin = new Thickness(0, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            row.Children.Add(new TextBlock
            {
                Text = "100%",
                FontFamily = Display,
                FontWeight = FontWeights.Black,
                FontSize = 27,
                Foreground = InkBrush,
                Margin = new Thickness(0, 0, 14, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            row.Children.Add(new TextBlock
            {
                Text = "\uEBAA",
                FontFamily = Icons,
                FontSize = 28,
                Foreground = InkBrush,
                VerticalAlignment = VerticalAlignment.Center
            });
        }

        box.Child = row;
        Canvas.SetLeft(box, 190);
        Canvas.SetTop(box, 22);
        return box;
    }

    /// <summary>
    /// Cyan band across top: polygon(0 8%, 100% 0, 100% 92%, 0 100%)
    /// </summary>
    public static FrameworkElement TopBand(double top = 86)
    {
        var polygon = new Polygon
        {
            Points = new PointCollection
            {
                new Point(0, 24),
                new Point(1130, 0),
                new Point(1130, 276),
                new Point(0, 300)
            },
            Fill = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0.5),
                EndPoint = new Point(1, 0.5),
                GradientStops =
                {
                    new(Color.FromArgb((byte)(255 * 0.90), 80, 230, 255), 0.0),
                    new(Color.FromArgb((byte)(255 * 0.55), 80, 230, 255), 1.0)
                }
            }
        };

        var container = new Canvas { Width = 1130, Height = 300 };
        container.Children.Add(polygon);
        Canvas.SetLeft(container, 470);
        Canvas.SetTop(container, top);
        return container;
    }

    /// <summary>
    /// Cover tile: Crops controller art towards focal point with finish background.
    /// </summary>
    public static FrameworkElement CoverTile(string key, double w, double h, string page = "setups", double dark = 0)
    {
        string safe = key.Replace("_", "-").ToLowerInvariant();
        if (safe.Contains("starfield")) safe = "x15-starfield";
        else if (safe.Contains("wukong")) safe = "x15-wukong";
        else if (safe.Contains("purple")) safe = "d10-purple";

        var (c1, c2) = CoverBgs.TryGetValue(safe, out var bgPair) ? bgPair : ("#6870b5", "#1b1f3d");
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
            CornerRadius = new CornerRadius(6),
            BorderBrush = WhiteBrush,
            BorderThickness = new Thickness(4),
            ClipToBounds = true,
            Effect = new DropShadowEffect { BlurRadius = 18, ShadowDepth = 6, Direction = 270, Opacity = 0.45, Color = Color.FromArgb(255, 0, 0, 40) }
        };

        var canvas = new Canvas { Width = w, Height = h };

        var bg = new Rectangle
        {
            Width = w,
            Height = h,
            Fill = new RadialGradientBrush
            {
                Center = new Point(0.5, 0.2),
                RadiusX = 0.7,
                RadiusY = 0.6,
                GradientStops =
                {
                    new(C(c1), 0.0),
                    new(C(c2), 1.0)
                }
            }
        };
        canvas.Children.Add(bg);

        var art = ControllerArt(key, iw);
        Canvas.SetLeft(art, left);
        Canvas.SetTop(art, top);
        canvas.Children.Add(art);

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
                        new(Color.FromArgb((byte)(255 * dark), 0, 0, 0), 0.0),
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
    /// Persona 3 round badge tile at top row.
    /// </summary>
    public static FrameworkElement RoundBadge(string key, double size = 250)
    {
        var badge = new Border
        {
            Width = size,
            Height = size,
            Background = WhiteBrush,
            BorderBrush = WhiteBrush,
            BorderThickness = new Thickness(4),
            CornerRadius = new CornerRadius(6),
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new RotateTransform(-4),
            Effect = new DropShadowEffect { BlurRadius = 18, ShadowDepth = 6, Direction = 270, Opacity = 0.45, Color = Color.FromArgb(255, 0, 0, 40) }
        };

        var circle = new Border
        {
            Width = size - 36,
            Height = size - 36,
            CornerRadius = new CornerRadius((size - 36) / 2),
            BorderBrush = Sea0Brush,
            BorderThickness = new Thickness(4),
            ClipToBounds = true,
            Margin = new Thickness(14)
        };
        circle.Child = CoverTile(key, size - 36, size - 36, "setups");
        badge.Child = circle;
        return badge;
    }

    /// <summary>
    /// Persona 3 TopRow: band + round badge + 4 tiles + right chevron.
    /// </summary>
    public static Canvas TopRow(FrameworkElement[] items, string? firstRound = null, int? focus = null)
    {
        var canvas = new Canvas { Width = 1600, Height = 400 };
        double top = firstRound != null ? 52 : 104;

        canvas.Children.Add(TopBand(top - 18));

        double x = 490;
        if (firstRound != null)
        {
            var badge = RoundBadge(firstRound, 250);
            Canvas.SetLeft(badge, 200);
            Canvas.SetTop(badge, 110);
            Panel.SetZIndex(badge, 2);
            canvas.Children.Add(badge);
        }
        else
        {
            x = 210;
        }

        for (int i = 0; i < items.Length; i++)
        {
            var it = items[i];
            var container = new Border
            {
                Width = 250,
                Height = 250,
                BorderBrush = WhiteBrush,
                BorderThickness = new Thickness(4),
                CornerRadius = new CornerRadius(6),
                ClipToBounds = true,
                Effect = new DropShadowEffect { BlurRadius = 18, ShadowDepth = 6, Direction = 270, Opacity = 0.45, Color = Color.FromArgb(255, 0, 0, 40) }
            };

            if (i == focus)
            {
                container.BorderBrush = SpikeRedBrush;
                container.BorderThickness = new Thickness(6);
            }

            container.Child = it;
            Canvas.SetLeft(container, x);
            Canvas.SetTop(container, top);
            canvas.Children.Add(container);
            x += 268;
        }

        var arrow = new Border
        {
            Width = 58,
            Height = 58,
            CornerRadius = new CornerRadius(29),
            Background = InkBrush,
            BorderBrush = WhiteBrush,
            BorderThickness = new Thickness(3),
            Child = new TextBlock
            {
                Text = "\uE76C",
                FontFamily = Icons,
                FontSize = 26,
                Foreground = WhiteBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        Canvas.SetLeft(arrow, 1520);
        Canvas.SetTop(arrow, 160);
        canvas.Children.Add(arrow);

        return canvas;
    }

    /// <summary>
    /// Slanted word standing on baseline rotated -35deg.
    /// If focused: dark on white slash with red spike running down to key!
    /// </summary>
    public static FrameworkElement SlantedWord(string text, bool focused, double fs = 76, Action onClick = null)
    {
        var canvas = new Canvas
        {
            Width = Math.Max(fs * 2.2, text.Length * fs * 0.65),
            Height = fs * 1.3,
            Cursor = System.Windows.Input.Cursors.Hand,
            RenderTransformOrigin = new Point(0, 1),
            RenderTransform = new RotateTransform(-35)
        };

        if (focused)
        {
            double textW = text.Length * fs * 0.68;
            var slash = new Border
            {
                Width = textW + 60,
                Height = fs * 1.05,
                Background = WhiteBrush,
                RenderTransform = new SkewTransform(-22, 0),
                Effect = new DropShadowEffect { BlurRadius = 0, ShadowDepth = 7, Direction = 315, Opacity = 1.0, Color = Ink }
            };
            Canvas.SetLeft(slash, -18);
            Canvas.SetTop(slash, 4);
            canvas.Children.Add(slash);

            var spike = new Polygon
            {
                Points = new PointCollection
                {
                    new Point(0, 20),
                    new Point(textW * 0.62 + 150, 0),
                    new Point(textW * 0.62 + 150, 20)
                },
                Fill = SpikeRedBrush
            };
            Canvas.SetLeft(spike, -150);
            Canvas.SetTop(spike, fs * 1.05 + 2);
            canvas.Children.Add(spike);
        }

        var tb = new TextBlock
        {
            Text = text,
            FontFamily = Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = fs,
            Foreground = focused ? InkBrush : LightCyanBrush,
            LineHeight = fs,
            LineStackingStrategy = LineStackingStrategy.BlockLineHeight
        };

        if (!focused)
        {
            tb.Effect = new DropShadowEffect
            {
                BlurRadius = 0,
                ShadowDepth = 5,
                Direction = 315,
                Opacity = 0.55,
                Color = Color.FromArgb(140, 10, 20, 120)
            };
        }

        Canvas.SetLeft(tb, 0);
        Canvas.SetTop(tb, 0);
        canvas.Children.Add(tb);

        if (onClick != null)
        {
            canvas.MouseLeftButtonUp += (_, _) => onClick();
        }
        return canvas;
    }

    /// <summary>
    /// Menu helper that generates slanted words on baseline per t_tide.py.
    /// </summary>
    public static Canvas MenuWords(string[] words, int focus = 0, double x0 = 640, double y0 = 700, double step = 128, double fs = 76, Action<int> onSelect = null)
    {
        var canvas = new Canvas { Width = 1600, Height = 900 };
        for (int i = 0; i < words.Length; i++)
        {
            int idx = i;
            var w = SlantedWord(words[i], i == focus, fs, () => onSelect?.Invoke(idx));
            Canvas.SetLeft(w, x0 + i * step);
            Canvas.SetTop(w, y0 - fs);
            canvas.Children.Add(w);
        }
        return canvas;
    }

    /// <summary>
    /// Keys helper that generates tilted keys per t_tide.py.
    /// </summary>
    public static Canvas KeysRow(string[] icons, double x0 = 600, double y0 = 772, int focus = 0, double step = 132, Action<int> onSelect = null)
    {
        var canvas = new Canvas { Width = 1600, Height = 900, IsHitTestVisible = true };
        for (int i = 0; i < icons.Length; i++)
        {
            int idx = i;
            bool on = (i == focus);
            string g = icons[i];
            string glyph = g switch
            {
                "zone" => "\uE701",
                "test" => "\uE768",
                "settings" => "\uE713",
                "add" => "\uE710",
                "tools" => "\uE713",
                "update" => "\uE777",
                "intro" => "\uE714",
                "sound" => "\uE767",
                "about" => "\uE946",
                "reset" => "\uE777",
                "buttons" => "\uE765",
                "setups" => "\uE8B5",
                _ => g.StartsWith("E") ? ((char)Convert.ToInt32(g, 16)).ToString() : "\uE701"
            };

            var btn = KeyButton(glyph, on, -12, () => onSelect?.Invoke(idx));
            Canvas.SetLeft(btn, x0 + i * step);
            Canvas.SetTop(btn, y0 - i * 12 - (on ? 6 : 0));
            canvas.Children.Add(btn);
        }
        return canvas;
    }

    /// <summary>
    /// Avatars 3x2 grid per t_tide.py.
    /// </summary>
    public static Canvas AvatarsGrid(string?[] keys, double x0 = 1250, double y0 = 650)
    {
        var canvas = new Canvas { Width = 1600, Height = 900 };
        for (int i = 0; i < keys.Length; i++)
        {
            double x = x0 + (i % 3) * 92;
            double y = y0 + (i / 3) * 92;
            string? k = keys[i];

            var border = new Border
            {
                Width = 80,
                Height = 80,
                BorderBrush = WhiteBrush,
                BorderThickness = new Thickness(3),
                CornerRadius = new CornerRadius(4),
                ClipToBounds = true
            };

            if (k != null)
            {
                border.Child = CoverTile(k, 80, 80, "buttons");
            }
            else
            {
                var bg = new Canvas { Background = Sea0Brush };
                var sil = ControllerArt("x20", 72);
                sil.Opacity = 0.35;
                Canvas.SetLeft(sil, 4);
                Canvas.SetTop(sil, 18);
                bg.Children.Add(sil);
                border.Child = bg;
            }

            Canvas.SetLeft(border, x);
            Canvas.SetTop(border, y);
            canvas.Children.Add(border);
        }
        return canvas;
    }

    /// <summary>
    /// Tilted black key rotated -12deg.
    /// </summary>
    public static FrameworkElement KeyButton(string glyph, bool focused = false, double rot = -12, Action onClick = null)
    {
        double sz = focused ? 96 : 84;
        var border = new Border
        {
            Width = sz,
            Height = sz,
            Background = focused ? WhiteBrush : InkBrush,
            BorderBrush = focused ? InkBrush : WhiteBrush,
            BorderThickness = new Thickness(3),
            CornerRadius = new CornerRadius(12),
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new RotateTransform(rot),
            Cursor = System.Windows.Input.Cursors.Hand,
            Effect = new DropShadowEffect
            {
                BlurRadius = 0,
                ShadowDepth = focused ? 7 : 5,
                Direction = 315,
                Opacity = 0.6,
                Color = focused ? SpikeRed : Sea0
            }
        };

        var tb = new TextBlock
        {
            Text = glyph,
            FontFamily = Icons,
            FontSize = focused ? 40 : 34,
            Foreground = focused ? InkBrush : WhiteBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        border.Child = tb;
        border.MouseLeftButtonUp += (_, _) => onClick?.Invoke();
        return border;
    }

    /// <summary>
    /// Small grid of avatars at bottom right: 3x2 grid of 80x80 tiles.
    /// </summary>
    public static FrameworkElement AvatarGrid(string[] keys, double x0 = 1250, double y0 = 650)
    {
        var canvas = new Canvas { Width = 300, Height = 200 };
        for (int i = 0; i < keys.Length; i++)
        {
            double x = (i % 3) * 92;
            double y = (i / 3) * 92;
            string k = keys[i];

            var av = new Border
            {
                Width = 80,
                Height = 80,
                BorderBrush = WhiteBrush,
                BorderThickness = new Thickness(3),
                CornerRadius = new CornerRadius(4),
                ClipToBounds = true,
                Background = Sea0Brush
            };

            if (!string.IsNullOrEmpty(k))
            {
                av.Child = CoverTile(k, 80, 80, "buttons");
            }

            Canvas.SetLeft(av, x);
            Canvas.SetTop(av, y);
            canvas.Children.Add(av);
        }

        Canvas.SetLeft(canvas, x0);
        Canvas.SetTop(canvas, y0);
        return canvas;
    }

    /// <summary>
    /// White sticker card with 4px black border and 10px deep navy drop shadow.
    /// </summary>
    public static Border PersonaCard(double w, double h = double.NaN, double rot = 2)
    {
        var b = new Border
        {
            Width = w,
            Background = WhiteBrush,
            BorderBrush = InkBrush,
            BorderThickness = new Thickness(4),
            Padding = new Thickness(20),
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new RotateTransform(rot),
            Effect = new DropShadowEffect
            {
                BlurRadius = 0,
                ShadowDepth = 10,
                Direction = 315,
                Opacity = 0.6,
                Color = Sea0
            }
        };
        if (!double.IsNaN(h)) b.Height = h;
        return b;
    }

    /// <summary>
    /// Persona 3 row for tool staircase lists: focused row has skewed white slash + red spike.
    /// </summary>
    public static FrameworkElement PersonaRow(
        string title,
        string? value = null,
        bool focused = false,
        Action? onClick = null,
        string? sub = null,
        string? ctl = null)
    {
        double h = string.IsNullOrEmpty(sub) ? 54 : 64;
        var rowCanvas = new Canvas
        {
            Width = 600,
            Height = h,
            Cursor = System.Windows.Input.Cursors.Hand
        };

        if (focused)
        {
            var slash = new Border
            {
                Width = 640,
                Height = h,
                Background = WhiteBrush,
                RenderTransform = new SkewTransform(-22, 0),
                Effect = new DropShadowEffect { BlurRadius = 0, ShadowDepth = 7, Direction = 315, Opacity = 1.0, Color = Ink }
            };
            Canvas.SetLeft(slash, -16);
            Canvas.SetTop(slash, 0);
            rowCanvas.Children.Add(slash);

            var spike = new Polygon
            {
                Points = new PointCollection
                {
                    new Point(0, 16),
                    new Point(370, 0),
                    new Point(370, 16)
                },
                Fill = SpikeRedBrush
            };
            Canvas.SetLeft(spike, -150);
            Canvas.SetTop(spike, h - 2);
            rowCanvas.Children.Add(spike);
        }

        var grid = new Grid { Width = 600, Height = h };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var leftStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
        var titleBlock = new TextBlock
        {
            Text = title.ToUpperInvariant(),
            FontFamily = Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 20,
            Foreground = focused ? InkBrush : WhiteBrush
        };
        if (!focused)
        {
            titleBlock.Effect = new DropShadowEffect { BlurRadius = 0, ShadowDepth = 3, Direction = 315, Opacity = 1.0, Color = Sea0 };
        }
        leftStack.Children.Add(titleBlock);

        if (!string.IsNullOrEmpty(sub))
        {
            var subBlock = new TextBlock
            {
                Text = sub,
                FontFamily = Body,
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                Foreground = focused ? Sea0Brush : WhiteBrush,
                Margin = new Thickness(0, 2, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 420
            };
            leftStack.Children.Add(subBlock);
        }
        Grid.SetColumn(leftStack, 0);
        grid.Children.Add(leftStack);

        var rightStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) };

        if (!string.IsNullOrEmpty(value))
        {
            var valBlock = new TextBlock
            {
                Text = value,
                FontFamily = Display,
                FontWeight = FontWeights.Black,
                FontStyle = FontStyles.Italic,
                FontSize = 18,
                Foreground = focused ? Sea0Brush : WhiteBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            if (!focused)
            {
                valBlock.Effect = new DropShadowEffect { BlurRadius = 0, ShadowDepth = 3, Direction = 315, Opacity = 1.0, Color = Sea0 };
            }
            rightStack.Children.Add(valBlock);
        }

        if (ctl == "chev")
        {
            var chev = new TextBlock
            {
                Text = "\uE76C",
                FontFamily = Icons,
                FontSize = 18,
                Foreground = focused ? InkBrush : WhiteBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            rightStack.Children.Add(chev);
        }
        else if (ctl == "toggle")
        {
            var tg = new Border
            {
                Width = 48,
                Height = 24,
                CornerRadius = new CornerRadius(12),
                Background = focused ? LightCyanBrush : Sea0Brush,
                BorderBrush = focused ? InkBrush : WhiteBrush,
                BorderThickness = new Thickness(2),
                RenderTransform = new RotateTransform(-6)
            };
            var knob = new Ellipse
            {
                Width = 14,
                Height = 14,
                Fill = WhiteBrush,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(3, 0, 0, 0)
            };
            tg.Child = knob;
            rightStack.Children.Add(tg);
        }
        else if (ctl != null && (ctl == "btn" || ctl.StartsWith("btn:")))
        {
            string btnText = ctl.StartsWith("btn:") ? ctl.Substring(4) : "ACTION";
            var btn = PersonaButton(btnText, primary: false);
            btn.RenderTransform = new RotateTransform(-2);
            rightStack.Children.Add(btn);
        }
        else if (ctl != null && ctl.StartsWith("slider:"))
        {
            string[] parts = ctl.Substring(7).Split(':');
            double pct = double.TryParse(parts[0], out var p) ? p : 50;
            var slBorder = new Border
            {
                Width = 150,
                Height = 12,
                Background = Sea0Brush,
                BorderBrush = focused ? InkBrush : WhiteBrush,
                BorderThickness = new Thickness(3),
                RenderTransform = new SkewTransform(-20, 0),
                ClipToBounds = false
            };
            var slCanvas = new Canvas { Width = 144, Height = 6 };
            if (pct > 0)
            {
                var fillRect = new Rectangle
                {
                    Width = Math.Max(0, 144 * (pct / 100.0)),
                    Height = 6,
                    Fill = LightCyanBrush
                };
                slCanvas.Children.Add(fillRect);
            }
            var knob = new Border
            {
                Width = 8,
                Height = 20,
                Background = SpikeRedBrush,
                BorderBrush = InkBrush,
                BorderThickness = new Thickness(2)
            };
            Canvas.SetLeft(knob, 144 * (pct / 100.0) - 4);
            Canvas.SetTop(knob, -7);
            slCanvas.Children.Add(knob);
            slBorder.Child = slCanvas;
            rightStack.Children.Add(slBorder);
        }
        else if (ctl != null && ctl.StartsWith("dd:"))
        {
            var ddBorder = new Border
            {
                Background = WhiteBrush,
                BorderBrush = InkBrush,
                BorderThickness = new Thickness(2),
                Padding = new Thickness(8, 2, 8, 2),
                RenderTransform = new RotateTransform(-2)
            };
            ddBorder.Child = new TextBlock
            {
                Text = ctl.Substring(3).ToUpperInvariant(),
                FontFamily = Display,
                FontWeight = FontWeights.Black,
                FontSize = 13,
                Foreground = Sea0Brush
            };
            rightStack.Children.Add(ddBorder);
        }

        Grid.SetColumn(rightStack, 1);
        grid.Children.Add(rightStack);

        rowCanvas.Children.Add(grid);
        if (onClick != null) rowCanvas.MouseLeftButtonUp += (_, _) => onClick();
        return rowCanvas;
    }

    /// <summary>
    /// Section header sticker in staircase list.
    /// </summary>
    public static FrameworkElement SectionHeader(string text)
    {
        var border = new Border
        {
            Background = WhiteBrush,
            BorderBrush = InkBrush,
            BorderThickness = new Thickness(3),
            Padding = new Thickness(12, 3, 12, 3),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 10, 0, 6),
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new RotateTransform(-2),
            Effect = new DropShadowEffect { BlurRadius = 0, ShadowDepth = 4, Direction = 315, Opacity = 0.6, Color = Sea0 }
        };
        border.Child = new TextBlock
        {
            Text = text.ToUpperInvariant(),
            FontFamily = Display,
            FontWeight = FontWeights.Black,
            FontSize = 14,
            Foreground = Sea0Brush
        };
        return border;
    }

    /// <summary>
    /// Left staircase list container with -3deg tilt.
    /// </summary>
    public static FrameworkElement StaircaseList(IEnumerable<FrameworkElement> rows, double width = 640)
    {
        var stack = new StackPanel { Width = width };
        foreach (var r in rows)
        {
            r.Margin = new Thickness(0, 4, 0, 4);
            stack.Children.Add(r);
        }

        var host = new Canvas
        {
            Width = width,
            RenderTransformOrigin = new Point(0, 0),
            RenderTransform = new RotateTransform(-3)
        };
        host.Children.Add(stack);
        return host;
    }

    /// <summary>
    /// Visual Card for right column: white sticker card rotated -2deg.
    /// </summary>
    public static FrameworkElement VisualCard(FrameworkElement visual, double width = 580, double rot = -2)
    {
        var card = new Border
        {
            Width = width,
            Background = WhiteBrush,
            BorderBrush = InkBrush,
            BorderThickness = new Thickness(4),
            Padding = new Thickness(12, 8, 12, 8),
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new RotateTransform(rot),
            Effect = new DropShadowEffect { BlurRadius = 0, ShadowDepth = 10, Direction = 315, Opacity = 0.6, Color = Sea0 }
        };
        card.Child = visual;
        return card;
    }

    /// <summary>
    /// Persona 3 Diamond Face Buttons: Y, X, B, A in diamond with active button highlighted.
    /// </summary>
    public static FrameworkElement FaceButtonsDiamond(string focused = "A", double size = 46)
    {
        double s = size * 1.05;
        double w = 3 * s;
        double h = 2 * s * 0.82 + size;
        var canvas = new Canvas { Width = w, Height = h, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 10, 0, 10) };
        var pos = new Dictionary<string, (double x, double y)>
        {
            ["Y"] = (1, 0),
            ["X"] = (0, 1),
            ["B"] = (2, 1),
            ["A"] = (1, 2)
        };
        foreach (var (c, (px, py)) in pos)
        {
            bool on = string.Equals(c, focused, StringComparison.OrdinalIgnoreCase);
            var circle = new Border
            {
                Width = size,
                Height = size,
                CornerRadius = new CornerRadius(size / 2),
                Background = Sea0Brush,
                BorderBrush = on ? SpikeRedBrush : Brushes.Transparent,
                BorderThickness = on ? new Thickness(3) : new Thickness(0),
                Effect = on ? new DropShadowEffect { BlurRadius = 0, ShadowDepth = 0, Color = SpikeRed } : null
            };
            circle.Child = new TextBlock
            {
                Text = c,
                FontFamily = Body,
                FontWeight = FontWeights.Bold,
                FontSize = 19,
                Foreground = WhiteBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Canvas.SetLeft(circle, px * s);
            Canvas.SetTop(circle, py * s * 0.82);
            canvas.Children.Add(circle);
        }
        return canvas;
    }

    /// <summary>
    /// Persona 3 Character Art: Tilted controller with white outline and deep shadow floating directly on sea background.
    /// </summary>
    public static FrameworkElement CharacterArt(string key = "x15-starfield", double w = 420, double rot = -8)
    {
        double h = w * 0.7;
        var host = new Canvas { Width = 580, Height = h + 30 };
        var art = ControllerArt(key, w);
        art.RenderTransformOrigin = new Point(0.5, 0.5);
        art.RenderTransform = new RotateTransform(rot);
        art.Effect = new DropShadowEffect
        {
            BlurRadius = 0,
            ShadowDepth = 14,
            Direction = 315,
            Opacity = 0.6,
            Color = Sea0
        };
        Canvas.SetLeft(art, (580 - w) / 2);
        Canvas.SetTop(art, 10);
        host.Children.Add(art);
        return host;
    }

    /// <summary>
    /// Detail Card for right column: white sticker card rotated +2deg with title, value, chip, body, buttons.
    /// </summary>
    public static FrameworkElement DetailCard(
        string title,
        string? value = null,
        string? chip = null,
        string? body = null,
        string? foot = null,
        string[]? buttons = null,
        double width = 580,
        double rot = 2,
        string? sub = null,
        string? quote = null,
        string[]? numbered = null,
        string[]? chips = null,
        string? face = null,
        (string cat, string[] opts)[]? options = null,
        string? selectedOption = null)
    {
        var card = PersonaCard(width, double.NaN, rot);
        var stack = new StackPanel();

        if (!string.IsNullOrEmpty(title))
        {
            stack.Children.Add(new TextBlock
            {
                Text = title.ToUpperInvariant(),
                FontFamily = Display,
                FontWeight = FontWeights.Black,
                FontStyle = FontStyles.Italic,
                FontSize = 23,
                Foreground = Sea0Brush
            });
        }

        if (!string.IsNullOrEmpty(sub))
        {
            stack.Children.Add(new TextBlock
            {
                Text = sub,
                FontFamily = Body,
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                Foreground = SubBrush,
                Margin = new Thickness(0, 2, 0, 4)
            });
        }

        if (!string.IsNullOrEmpty(value))
        {
            stack.Children.Add(new TextBlock
            {
                Text = value,
                FontFamily = Display,
                FontWeight = FontWeights.Black,
                FontStyle = FontStyles.Italic,
                FontSize = 46,
                Foreground = SpikeRedBrush,
                Margin = new Thickness(0, 2, 0, 2)
            });
        }

        if (!string.IsNullOrEmpty(quote))
        {
            var quoteBox = new Border
            {
                Background = Sea0Brush,
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 4, 0, 8),
                CornerRadius = new CornerRadius(4)
            };
            quoteBox.Child = new TextBlock
            {
                Text = quote,
                FontFamily = Body,
                FontWeight = FontWeights.Bold,
                FontSize = 15,
                Foreground = WhiteBrush,
                TextWrapping = TextWrapping.Wrap
            };
            stack.Children.Add(quoteBox);
        }

        if (chips != null && chips.Length > 0)
        {
            var chipsRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 6) };
            for (int i = 0; i < chips.Length; i++)
            {
                var chBorder = new Border
                {
                    Background = (i == 0) ? SpikeRedBrush : Sea0Brush,
                    Padding = new Thickness(10, 3, 10, 3),
                    Margin = new Thickness(0, 0, 8, 0)
                };
                chBorder.Child = new TextBlock
                {
                    Text = chips[i].ToUpperInvariant(),
                    FontFamily = Display,
                    FontWeight = FontWeights.Black,
                    FontStyle = FontStyles.Italic,
                    FontSize = 13,
                    Foreground = WhiteBrush
                };
                chipsRow.Children.Add(chBorder);
            }
            stack.Children.Add(chipsRow);
        }
        else if (!string.IsNullOrEmpty(chip))
        {
            var chipBorder = new Border
            {
                Background = Sea0Brush,
                Padding = new Thickness(12, 3, 12, 3),
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 4, 0, 6)
            };
            chipBorder.Child = new TextBlock
            {
                Text = chip.ToUpperInvariant(),
                FontFamily = Display,
                FontWeight = FontWeights.Black,
                FontStyle = FontStyles.Italic,
                FontSize = 13,
                Foreground = WhiteBrush
            };
            stack.Children.Add(chipBorder);
        }

        if (!string.IsNullOrEmpty(face))
        {
            stack.Children.Add(FaceButtonsDiamond(face));
        }

        if (numbered != null && numbered.Length > 0)
        {
            for (int i = 0; i < numbered.Length; i++)
            {
                var numStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };
                var numBox = new Border
                {
                    Width = 28,
                    Height = 28,
                    Background = Sea0Brush,
                    RenderTransformOrigin = new Point(0.5, 0.5),
                    RenderTransform = new RotateTransform(-8),
                    Margin = new Thickness(0, 0, 12, 0),
                    VerticalAlignment = VerticalAlignment.Top
                };
                numBox.Child = new TextBlock
                {
                    Text = $"{i + 1}",
                    FontFamily = Display,
                    FontWeight = FontWeights.Black,
                    FontStyle = FontStyles.Italic,
                    FontSize = 14,
                    Foreground = WhiteBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                numStack.Children.Add(numBox);
                numStack.Children.Add(new TextBlock
                {
                    Text = numbered[i],
                    FontFamily = Body,
                    FontWeight = FontWeights.Bold,
                    FontSize = 14,
                    Foreground = InkBrush,
                    TextWrapping = TextWrapping.Wrap,
                    Width = width - 90
                });
                stack.Children.Add(numStack);
            }
        }

        if (options != null && options.Length > 0)
        {
            foreach (var (cat, opts) in options)
            {
                stack.Children.Add(new TextBlock
                {
                    Text = cat.ToUpperInvariant(),
                    FontFamily = Display,
                    FontWeight = FontWeights.Black,
                    FontStyle = FontStyles.Italic,
                    FontSize = 13,
                    Foreground = SpikeRedBrush,
                    Margin = new Thickness(0, 6, 0, 2)
                });
                foreach (var opt in opts)
                {
                    bool isSel = string.Equals(opt, selectedOption, StringComparison.OrdinalIgnoreCase);
                    var optBorder = new Border
                    {
                        Background = isSel ? Sea0Brush : Brushes.Transparent,
                        Padding = new Thickness(12, 5, 12, 5),
                        Margin = new Thickness(0, 2, 0, 2),
                        RenderTransform = isSel ? new SkewTransform(-14, 0) : null
                    };
                    if (isSel)
                    {
                        optBorder.Effect = new DropShadowEffect { BlurRadius = 0, ShadowDepth = 5, Direction = 315, Opacity = 1.0, Color = SpikeRed };
                    }
                    optBorder.Child = new TextBlock
                    {
                        Text = opt.ToUpperInvariant(),
                        FontFamily = Display,
                        FontWeight = FontWeights.Black,
                        FontStyle = FontStyles.Italic,
                        FontSize = 17,
                        Foreground = isSel ? WhiteBrush : Sea0Brush
                    };
                    stack.Children.Add(optBorder);
                }
            }
        }

        if (!string.IsNullOrEmpty(body))
        {
            stack.Children.Add(new TextBlock
            {
                Text = body,
                FontFamily = Body,
                FontWeight = FontWeights.Bold,
                FontSize = 15,
                Foreground = InkBrush,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 6)
            });
        }

        if (!string.IsNullOrEmpty(foot))
        {
            stack.Children.Add(new TextBlock
            {
                Text = foot,
                FontFamily = Body,
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                Foreground = SubBrush,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 6)
            });
        }

        if (buttons != null && buttons.Length > 0)
        {
            var btnRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
            for (int i = 0; i < buttons.Length; i++)
            {
                var btn = PersonaButton(buttons[i], primary: (i == 0));
                if (i > 0) btn.Margin = new Thickness(12, 0, 0, 0);
                btnRow.Children.Add(btn);
            }
            stack.Children.Add(btnRow);
        }

        card.Child = stack;
        return card;
    }

    /// <summary>
    /// Right side column containing Visual Card + Detail Card.
    /// </summary>
    public static FrameworkElement SideStack(FrameworkElement? visual, FrameworkElement? detail, double width = 600)
    {
        var stack = new StackPanel { Width = width };
        if (visual != null)
        {
            visual.Margin = new Thickness(0, 0, 0, 20);
            stack.Children.Add(visual);
        }
        if (detail != null)
        {
            stack.Children.Add(detail);
        }
        return stack;
    }

    /// <summary>
    /// White note sticker with 3px black border, rotated -3deg.
    /// </summary>
    public static Border NoteSticker(string text, double rot = -3)
    {
        var border = new Border
        {
            Padding = new Thickness(14, 10, 14, 10),
            Background = WhiteBrush,
            BorderBrush = InkBrush,
            BorderThickness = new Thickness(3),
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new RotateTransform(rot),
            Effect = new DropShadowEffect { BlurRadius = 0, ShadowDepth = 6, Direction = 315, Opacity = 0.6, Color = Sea0 }
        };

        border.Child = new TextBlock
        {
            Text = text,
            FontFamily = Display,
            FontWeight = FontWeights.Black,
            FontSize = 16,
            Foreground = InkBrush,
            TextWrapping = TextWrapping.Wrap
        };
        return border;
    }

    /// <summary>
    /// Persona 3 Button: Tilted black button with white border or white button with red shadow.
    /// </summary>
    public static Button PersonaButton(string text, bool primary = false, Action onClick = null)
    {
        var btn = new Button
        {
            Height = 44,
            Padding = new Thickness(20, 0, 20, 0),
            Background = primary ? WhiteBrush : InkBrush,
            Foreground = primary ? InkBrush : WhiteBrush,
            BorderBrush = primary ? InkBrush : WhiteBrush,
            BorderThickness = new Thickness(3),
            FontFamily = Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 15,
            Content = text.ToUpperInvariant(),
            Cursor = System.Windows.Input.Cursors.Hand,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new RotateTransform(-3),
            Effect = new DropShadowEffect
            {
                BlurRadius = 0,
                ShadowDepth = primary ? 6 : 5,
                Direction = 315,
                Opacity = 0.6,
                Color = primary ? SpikeRed : Sea0
            }
        };

        btn.Click += (_, _) => onClick?.Invoke();
        return btn;
    }

    /// <summary>
    /// Persona 3 Bottom Hints Bar at (right: 40, bottom: 26).
    /// </summary>
    public static FrameworkElement HintsBar(IEnumerable<string> items)
    {
        var stack = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var h in items)
        {
            stack.Children.Add(new TextBlock
            {
                Text = h,
                FontFamily = Display,
                FontWeight = FontWeights.Black,
                FontSize = 25,
                Foreground = WhiteBrush,
                Margin = new Thickness(0, 0, 30, 0)
            });
        }
        return stack;
    }
}
