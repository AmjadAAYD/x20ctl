using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace X20Ctl.Product.Dusk;

/// <summary>
/// Design system primitives, colors, typography, and controls for the Dusk theme:
/// "DUSK — the Steam Deck dark home (Battlefield, deltarune). Dim key art from the focused controller fills the top,
/// a status row with a square avatar and green online dot, one wide hero capsule ringed in white followed by portrait
/// capsules running off the right edge, the title and a grey uppercase line under it, centred pill tabs with a green
/// count badge, the next shelf peeking under them and the black button bar ('X20 MENU', 'A SELECT  B BACK')."
/// Strictly implements t_dusk.py specifications and assets.
/// </summary>
public static class DuskKit
{
    // Dusk Palette Tokens
    public static readonly Color SlateBg = Color.FromRgb(0x0e, 0x12, 0x18);         // #0e1218 (Dark charcoal slate)
    public static readonly Color SlatePanel = Color.FromRgb(0x1b, 0x21, 0x29);      // #1b2129 (Panel background)
    public static readonly Color SlateItem = Color.FromRgb(0x23, 0x2a, 0x33);       // #232a33 (Card & row item)
    public static readonly Color SlateTabOn = Color.FromRgb(0x3d, 0x44, 0x50);      // #3d4450 (Active tab pill)
    public static readonly Color SlateBorder = Color.FromRgb(0x3d, 0x44, 0x50);     // #3d4450
    public static readonly Color GreenOnline = Color.FromRgb(0x59, 0xbf, 0x40);     // #59bf40 (Steam Deck green dot/badge)
    public static readonly Color GreenGrad0 = Color.FromRgb(0x72, 0xd3, 0x3f);      // #72d33f
    public static readonly Color GreenGrad1 = Color.FromRgb(0x46, 0xa5, 0x2a);      // #46a52a
    public static readonly Color CyanNeon = Color.FromRgb(0x1a, 0x9f, 0xff);        // #1a9fff (Electric cyan active accent)
    public static readonly Color TextWhite = Color.FromRgb(0xff, 0xff, 0xff);       // #ffffff
    public static readonly Color TextLight = Color.FromRgb(0xc7, 0xcb, 0xcf);       // #c7cbcf
    public static readonly Color TextMute = Color.FromRgb(0x8b, 0x92, 0x9a);        // #8b929a
    public static readonly Color BottomBarBg = Color.FromRgb(0x0b, 0x0e, 0x13);     // #0b0e13

    // Brushes
    public static readonly SolidColorBrush SlateBgBrush = B(SlateBg);
    public static readonly SolidColorBrush SlatePanelBrush = B(SlatePanel);
    public static readonly SolidColorBrush SlateItemBrush = B(SlateItem);
    public static readonly SolidColorBrush SlateTabOnBrush = B(SlateTabOn);
    public static readonly SolidColorBrush SlateBorderBrush = B(SlateBorder);
    public static readonly SolidColorBrush GreenOnlineBrush = B(GreenOnline);
    public static readonly SolidColorBrush CyanNeonBrush = B(CyanNeon);
    public static readonly LinearGradientBrush GreenActionBrush = CreateGreenActionBrush();
    private static LinearGradientBrush CreateGreenActionBrush()
    {
        var b = new LinearGradientBrush(GreenGrad0, GreenGrad1, new Point(0, 0.5), new Point(1, 0.5));
        b.Freeze();
        return b;
    }
    public static readonly SolidColorBrush WhiteBrush = B(TextWhite);
    public static readonly SolidColorBrush LightBrush = B(TextLight);
    public static readonly SolidColorBrush MuteBrush = B(TextMute);
    public static readonly SolidColorBrush BottomBarBrush = B(BottomBarBg);

    // Typography
    public static readonly FontFamily Display = new("Segoe UI Variable Display, Segoe UI");
    public static readonly FontFamily Body = new("Segoe UI Variable Text, Segoe UI");
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
    /// Loads controller art image.
    /// </summary>
    public static Image? ControllerArt(string modelId = "x05pro", double width = 600)
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

        var img = new Image
        {
            Width = width,
            Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(System.IO.Path.GetFullPath(path))),
            Stretch = Stretch.Uniform
        };
        return img;
    }

    /// <summary>
    /// Steam Deck dimmed key art backdrop filling the top.
    /// <summary>
    /// Radiant theatrical stage pedestal glow to give controllers and cards physical grounding.
    /// </summary>
    public static FrameworkElement PedestalGlow(double width, double height, Color? glowColor = null)
    {
        Color col = glowColor ?? CyanNeon;
        var ellipse = new Ellipse
        {
            Width = width,
            Height = height,
            Fill = new RadialGradientBrush
            {
                Center = new Point(0.5, 0.5),
                GradientOrigin = new Point(0.5, 0.5),
                RadiusX = 0.5,
                RadiusY = 0.5,
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(0x75, col.R, col.G, col.B), 0.0),
                    new GradientStop(Color.FromArgb(0x28, col.R, col.G, col.B), 0.45),
                    new GradientStop(Colors.Transparent, 1.0)
                }
            },
            Effect = new DropShadowEffect
            {
                Color = col,
                BlurRadius = 24,
                ShadowDepth = 0,
                Opacity = 0.75
            },
            IsHitTestVisible = false
        };
        return ellipse;
    }

    /// <summary>
    /// Tactical HUD corner brackets: ┌ ┐ └ ┘ around any rectangular diagnostic bounds.
    /// </summary>
    public static Canvas CornerBrackets(double width, double height, Color? color = null, double bracketLen = 14, double thickness = 1.5)
    {
        Color c = color ?? CyanNeon;
        var brush = B(c);
        var canvas = new Canvas { Width = width, Height = height, IsHitTestVisible = false };

        // Top-left
        canvas.Children.Add(new Line { X1 = 0, Y1 = 0, X2 = bracketLen, Y2 = 0, Stroke = brush, StrokeThickness = thickness });
        canvas.Children.Add(new Line { X1 = 0, Y1 = 0, X2 = 0, Y2 = bracketLen, Stroke = brush, StrokeThickness = thickness });

        // Top-right
        canvas.Children.Add(new Line { X1 = width, Y1 = 0, X2 = width - bracketLen, Y2 = 0, Stroke = brush, StrokeThickness = thickness });
        canvas.Children.Add(new Line { X1 = width, Y1 = 0, X2 = width, Y2 = bracketLen, Stroke = brush, StrokeThickness = thickness });

        // Bottom-left
        canvas.Children.Add(new Line { X1 = 0, Y1 = height, X2 = bracketLen, Y2 = height, Stroke = brush, StrokeThickness = thickness });
        canvas.Children.Add(new Line { X1 = 0, Y1 = height, X2 = 0, Y2 = height - bracketLen, Stroke = brush, StrokeThickness = thickness });

        // Bottom-right
        canvas.Children.Add(new Line { X1 = width, Y1 = height, X2 = width - bracketLen, Y2 = height, Stroke = brush, StrokeThickness = thickness });
        canvas.Children.Add(new Line { X1 = width, Y1 = height, X2 = width, Y2 = height - bracketLen, Stroke = brush, StrokeThickness = thickness });

        return canvas;
    }

    /// <summary>
    /// Xbox button glyph badge (circular A, B, X, Y with authentic jewel coloring).
    /// </summary>
    public static Border XboxButtonGlyph(string button, double size = 28)
    {
        string b = button.Trim().ToUpperInvariant();
        Color bg = b switch
        {
            "A" or "A BUTTON" => Color.FromRgb(0x10, 0x7c, 0x10), // Green
            "B" or "B BUTTON" => Color.FromRgb(0xd8, 0x3b, 0x01), // Red
            "X" or "X BUTTON" => Color.FromRgb(0x00, 0x78, 0xd7), // Blue
            "Y" or "Y BUTTON" => Color.FromRgb(0xe5, 0xa0, 0x0d), // Yellow
            _ => Color.FromRgb(0x3d, 0x44, 0x50)
        };
        string label = b switch
        {
            "A" or "A BUTTON" => "A",
            "B" or "B BUTTON" => "B",
            "X" or "X BUTTON" => "X",
            "Y" or "Y BUTTON" => "Y",
            _ => b
        };

        var glyph = new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(size / 2.0),
            Background = B(bg),
            BorderBrush = WhiteBrush,
            BorderThickness = new Thickness(1),
            Effect = new DropShadowEffect { Color = bg, BlurRadius = 8, ShadowDepth = 0, Opacity = 0.8 },
            Child = new TextBlock
            {
                Text = label,
                FontFamily = Display,
                FontSize = size * 0.55,
                FontWeight = FontWeights.ExtraBold,
                Foreground = WhiteBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        return glyph;
    }

    /// <summary>
    /// Steam Deck dimmed key art backdrop filling the top.
    /// Strictly implements t_dusk.py:44 backdrop().
    /// Enhanced with atmospheric deep tactical navy radial lighting and floating starlight embers.
    /// </summary>
    public static Canvas Backdrop(string modelId = "x05pro", double dim = 0.34)
    {
        var root = new Canvas { Width = 1600, Height = 900, ClipToBounds = true, Background = SlateBgBrush };

        // 1. Deep tactical navy radial ambient glow
        var radNavy = new Rectangle
        {
            Width = 1600,
            Height = 900,
            Fill = new RadialGradientBrush
            {
                Center = new Point(0.5, 0.25),
                GradientOrigin = new Point(0.5, 0.25),
                RadiusX = 0.85,
                RadiusY = 0.70,
                GradientStops =
                {
                    new GradientStop(C("#1c2a3e"), 0.0),
                    new GradientStop(C("#101722"), 0.55),
                    new GradientStop(C("#070a0e"), 1.0)
                }
            }
        };
        root.Children.Add(radNavy);

        // 2. Dim key art from controller
        var padImg = ControllerArt(modelId, 1480);
        if (padImg != null)
        {
            padImg.Opacity = dim;
            padImg.Effect = new BlurEffect { Radius = 4 };
            Canvas.SetLeft(padImg, 160);
            Canvas.SetTop(padImg, -330);
            root.Children.Add(padImg);
        }

        // 3. Shade gradient: 180deg (0.15 -> 0.55 40% -> #0e1218 72%)
        var shade = new Rectangle
        {
            Width = 1600,
            Height = 900,
            Fill = new LinearGradientBrush
            {
                StartPoint = new Point(0.5, 0),
                EndPoint = new Point(0.5, 1),
                GradientStops =
                {
                    new(Color.FromArgb(0x26, 14, 18, 24), 0.0),
                    new(Color.FromArgb(0x8c, 14, 18, 24), 0.40),
                    new(SlateBg, 0.72),
                    new(SlateBg, 1.0)
                }
            }
        };
        root.Children.Add(shade);

        // 4. Center radial vignette
        var rad = new Rectangle
        {
            Width = 1600,
            Height = 900,
            Fill = new RadialGradientBrush
            {
                Center = new Point(0.5, 0.2),
                GradientOrigin = new Point(0.5, 0.2),
                RadiusX = 0.7,
                RadiusY = 0.6,
                GradientStops =
                {
                    new(Colors.Transparent, 0.0),
                    new(Color.FromArgb(0x80, 14, 18, 24), 1.0)
                }
            }
        };
        root.Children.Add(rad);

        // 5. Floating tactical dust/starlight embers across the sky
        var rand = new Random(42);
        for (int i = 0; i < 40; i++)
        {
            double size = 2.0 + rand.NextDouble() * 3.0;
            double x = rand.NextDouble() * 1600;
            double y = rand.NextDouble() * 840;
            double opacity = 0.18 + rand.NextDouble() * 0.45;
            bool isCyan = rand.NextDouble() > 0.45;

            var speck = new Ellipse
            {
                Width = size,
                Height = size,
                Fill = isCyan ? CyanNeonBrush : WhiteBrush,
                Opacity = opacity,
                Effect = new DropShadowEffect
                {
                    Color = isCyan ? CyanNeon : Colors.White,
                    BlurRadius = 4,
                    ShadowDepth = 0,
                    Opacity = 0.6
                },
                IsHitTestVisible = false
            };
            Canvas.SetLeft(speck, x);
            Canvas.SetTop(speck, y);
            root.Children.Add(speck);
        }

        return root;
    }

    /// <summary>
    /// Steam Deck Top Status Row: Square avatar, green online dot, time, battery, wifi, search.
    /// Strictly implements t_dusk.py:50 top().
    /// </summary>
    public static FrameworkElement TopBar(string username = "Player 1", string time = "19:24", int battery = 100)
    {
        var root = new Canvas { Width = 1600, Height = 64, ClipToBounds = false };

        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };

        // Square avatar box (48x48) with controller art inside
        var av = new Border
        {
            Width = 48,
            Height = 48,
            CornerRadius = new CornerRadius(4),
            Background = new RadialGradientBrush
            {
                Center = new Point(0.5, 0.2),
                GradientOrigin = new Point(0.5, 0.2),
                RadiusX = 1.2,
                RadiusY = 0.9,
                GradientStops = { new(C("#34344a"), 0.0), new(C("#0a0a0e"), 1.0) }
            },
            ClipToBounds = true
        };
        var miniPad = ControllerArt("x05pro", 90);
        if (miniPad != null)
        {
            Canvas.SetLeft(miniPad, -20);
            Canvas.SetTop(miniPad, 0);
            var avCanvas = new Canvas { Width = 48, Height = 48 };
            avCanvas.Children.Add(miniPad);
            av.Child = avCanvas;
        }
        panel.Children.Add(av);

        // Green online dot (11x11, #59bf40)
        var dot = new Border
        {
            Width = 11,
            Height = 11,
            CornerRadius = new CornerRadius(5.5),
            Background = GreenOnlineBrush,
            Margin = new Thickness(14, 0, 14, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Effect = new DropShadowEffect { Color = GreenOnline, BlurRadius = 6, ShadowDepth = 0, Opacity = 0.8 }
        };
        panel.Children.Add(dot);

        // Time
        var timeBlock = new TextBlock
        {
            Text = time,
            FontFamily = Display,
            FontSize = 25,
            FontWeight = FontWeights.SemiBold,
            Foreground = WhiteBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
        panel.Children.Add(timeBlock);

        Canvas.SetLeft(panel, 30);
        Canvas.SetTop(panel, 18);
        root.Children.Add(panel);

        // Right side: Search icon, WiFi icon, Battery %, Battery icon
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
            Margin = new Thickness(0, 0, 26, 0),
            VerticalAlignment = VerticalAlignment.Center
        });

        // WiFi icon
        rightStack.Children.Add(new TextBlock
        {
            Text = "\uE704",
            FontFamily = Icons,
            FontSize = 24,
            Foreground = WhiteBrush,
            Margin = new Thickness(0, 0, 20, 0),
            VerticalAlignment = VerticalAlignment.Center
        });

        // Battery %
        rightStack.Children.Add(new TextBlock
        {
            Text = $"{battery}%",
            FontFamily = Display,
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            Foreground = WhiteBrush,
            Margin = new Thickness(0, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Center
        });

        // Battery icon
        rightStack.Children.Add(new TextBlock
        {
            Text = "\uE83F",
            FontFamily = Icons,
            FontSize = 28,
            Foreground = GreenOnlineBrush,
            VerticalAlignment = VerticalAlignment.Center
        });

        Canvas.SetRight(rightStack, 36);
        Canvas.SetTop(rightStack, 18);
        root.Children.Add(rightStack);

        return root;
    }

    /// <summary>
    /// Steam Deck Bottom Black Bar: Left white pill "X20 MENU", right round button badges.
    /// Strictly implements t_dusk.py:56 bar().
    /// </summary>
    public static FrameworkElement BottomBar((string Key, string Label)[] hints)
    {
        var root = new Border
        {
            Width = 1600,
            Height = 56,
            Background = new LinearGradientBrush
            {
                StartPoint = new Point(0.5, 0),
                EndPoint = new Point(0.5, 1),
                GradientStops =
                {
                    new(Color.FromArgb(0xe6, 11, 14, 19), 0.0),
                    new(BottomBarBg, 1.0)
                }
            },
            Padding = new Thickness(30, 0, 30, 0)
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Left pill: [X20] MENU
        var menuStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var pill = new Border
        {
            Padding = new Thickness(13, 3, 13, 3),
            CornerRadius = new CornerRadius(13),
            Background = WhiteBrush,
            Margin = new Thickness(0, 0, 10, 0),
            Child = new TextBlock
            {
                Text = "X20",
                FontFamily = Display,
                FontSize = 14,
                FontWeight = FontWeights.ExtraBold,
                Foreground = SlateBgBrush,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        menuStack.Children.Add(pill);

        menuStack.Children.Add(new TextBlock
        {
            Text = "MENU",
            FontFamily = Display,
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Foreground = LightBrush,
            VerticalAlignment = VerticalAlignment.Center
        });
        Grid.SetColumn(menuStack, 0);
        grid.Children.Add(menuStack);

        // Right hints list
        var rightStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        foreach (var (k, label) in hints)
        {
            var hRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(24, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };

            // Round button badge (.ab)
            var ab = new Border
            {
                Width = 28,
                Height = 28,
                CornerRadius = new CornerRadius(14),
                Background = B("#e9ebec"),
                Margin = new Thickness(0, 0, 8, 0),
                Child = new TextBlock
                {
                    Text = k,
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 15,
                    FontWeight = FontWeights.ExtraBold,
                    Foreground = SlateBgBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            hRow.Children.Add(ab);

            hRow.Children.Add(new TextBlock
            {
                Text = label.ToUpperInvariant(),
                FontFamily = Display,
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = LightBrush,
                VerticalAlignment = VerticalAlignment.Center
            });

            rightStack.Children.Add(hRow);
        }

        Grid.SetColumn(rightStack, 2);
        grid.Children.Add(rightStack);

        root.Child = grid;
        return root;
    }

    /// <summary>
    /// Wide Hero Capsule for the focused controller (790x360).
    /// Features glowing white focus ring, pad art, "EASYSMX" subtitle, and bold Bahnschrift logo.
    /// Strictly implements t_dusk.py:70.
    /// </summary>
    public static Border HeroCapsule(string modelId = "x05pro", string logoText = "X05 PRO", double w = 790, double h = 360, bool active = true, Action? onClick = null)
    {
        var border = new Border
        {
            Width = w,
            Height = h,
            CornerRadius = new CornerRadius(4),
            ClipToBounds = true,
            Cursor = System.Windows.Input.Cursors.Hand
        };

        if (active)
        {
            border.BorderBrush = WhiteBrush;
            border.BorderThickness = new Thickness(3);
            border.Effect = new DropShadowEffect
            {
                Color = Colors.White,
                BlurRadius = 34,
                ShadowDepth = 0,
                Opacity = 0.35
            };
        }
        else
        {
            border.Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 24, ShadowDepth = 12, Opacity = 0.55 };
        }

        var canvas = new Canvas { Width = w, Height = h };

        // Radial background inside capsule
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
                GradientStops = { new(C("#34344a"), 0.0), new(C("#0a0a0e"), 1.0) }
            }
        };
        canvas.Children.Add(bg);

        // Controller artwork
        var padArt = ControllerArt(modelId, 1020);
        if (padArt != null)
        {
            padArt.Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 18, ShadowDepth = 10, Opacity = 0.45 };
            Canvas.SetLeft(padArt, -118);
            Canvas.SetTop(padArt, -92);
            canvas.Children.Add(padArt);
        }

        // Gradient overlay (dark on left for logo readability)
        var shade = new Rectangle
        {
            Width = w,
            Height = h,
            Fill = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0.5),
                EndPoint = new Point(1, 0.5),
                GradientStops =
                {
                    new(Color.FromArgb(0xd1, 8, 10, 14), 0.0),
                    new(Color.FromArgb(0x33, 8, 10, 14), 0.55),
                    new(Colors.Transparent, 1.0)
                }
            }
        };
        canvas.Children.Add(shade);

        // Subtitle + Logo at (44, 92)
        string shortLogo = logoText.StartsWith("EASYSMX ", StringComparison.OrdinalIgnoreCase)
            ? logoText.Substring(8).Trim()
            : (logoText.StartsWith("EASYSMX", StringComparison.OrdinalIgnoreCase) ? logoText.Substring(7).Trim() : logoText);

        var textStack = new StackPanel { Orientation = Orientation.Vertical };
        textStack.Children.Add(new TextBlock
        {
            Text = "EASYSMX",
            FontFamily = Display,
            FontSize = 21,
            FontWeight = FontWeights.Bold,
            Foreground = B("#c9d3dd"),
            Margin = new Thickness(0, 0, 0, 8)
        });

        textStack.Children.Add(new TextBlock
        {
            Text = shortLogo,
            FontFamily = LogoFont,
            FontSize = 104,
            FontWeight = FontWeights.Bold,
            Foreground = WhiteBrush,
            LineHeight = 88,
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 18, ShadowDepth = 4, Opacity = 0.5 }
        });

        Canvas.SetLeft(textStack, 44);
        Canvas.SetTop(textStack, 92);
        canvas.Children.Add(textStack);

        if (active)
        {
            // Tactical HUD micro-brackets & telemetry badge
            canvas.Children.Add(CornerBrackets(w, h, CyanNeon, 20, 1.8));
            canvas.Children.Add(new Rectangle { Width = w, Height = 2, Fill = CyanNeonBrush, IsHitTestVisible = false });

            var proBadge = new Border
            {
                Padding = new Thickness(10, 4, 10, 4),
                Background = B("rgba(14,18,24,0.85)"),
                BorderBrush = B("rgba(26,159,255,0.4)"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Child = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Children =
                    {
                        new Ellipse { Width = 6, Height = 6, Fill = GreenOnlineBrush, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center },
                        new TextBlock
                        {
                            Text = "PRO PROFILE // 1000HZ ULTRA",
                            FontFamily = Display,
                            FontSize = 11,
                            FontWeight = FontWeights.Bold,
                            Foreground = CyanNeonBrush
                        }
                    }
                }
            };
            Canvas.SetRight(proBadge, 24);
            Canvas.SetTop(proBadge, 20);
            canvas.Children.Add(proBadge);
        }

        border.Child = canvas;
        if (onClick != null) border.MouseLeftButtonUp += (_, _) => onClick();
        return border;
    }

    /// <summary>
    /// Portrait Carousel Capsule for other controllers (250x360).
    /// </summary>
    public static Border PortraitCapsule(string modelId, string shortName, Color bgTop, Color bgBottom, Action? onClick = null)
    {
        var border = new Border
        {
            Width = 250,
            Height = 360,
            CornerRadius = new CornerRadius(4),
            ClipToBounds = true,
            Cursor = System.Windows.Input.Cursors.Hand,
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 24, ShadowDepth = 12, Opacity = 0.55 }
        };

        var canvas = new Canvas { Width = 250, Height = 360 };

        var bg = new Rectangle
        {
            Width = 250,
            Height = 360,
            Fill = new RadialGradientBrush
            {
                Center = new Point(0.5, 0.2),
                GradientOrigin = new Point(0.5, 0.2),
                RadiusX = 1.2,
                RadiusY = 0.9,
                GradientStops = { new(bgTop, 0.0), new(bgBottom, 1.0) }
            }
        };
        canvas.Children.Add(bg);

        var padArt = ControllerArt(modelId, 530);
        if (padArt != null)
        {
            padArt.Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 16, ShadowDepth = 8, Opacity = 0.45 };
            Canvas.SetLeft(padArt, -140);
            Canvas.SetTop(padArt, 4);
            canvas.Children.Add(padArt);
        }

        // Bottom shade for logo
        var shade = new Rectangle
        {
            Width = 250,
            Height = 360,
            Fill = new LinearGradientBrush
            {
                StartPoint = new Point(0.5, 0),
                EndPoint = new Point(0.5, 1),
                GradientStops =
                {
                    new(Colors.Transparent, 0.4),
                    new(Color.FromArgb(0xc0, 0, 0, 0), 1.0)
                }
            }
        };
        canvas.Children.Add(shade);

        // Logo bottom left
        var logo = new TextBlock
        {
            Text = shortName,
            FontFamily = LogoFont,
            FontSize = 58,
            FontWeight = FontWeights.Bold,
            Foreground = WhiteBrush,
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 12, ShadowDepth = 3, Opacity = 0.6 }
        };
        Canvas.SetLeft(logo, 18);
        Canvas.SetBottom(logo, 18);
        canvas.Children.Add(logo);

        // Subtle corner brackets for console frame
        canvas.Children.Add(CornerBrackets(250, 360, SlateTabOn, 12, 1.0));

        border.Child = canvas;
        if (onClick != null) border.MouseLeftButtonUp += (_, _) => onClick();
        return border;
    }

    /// <summary>
    /// Carousel Chevron button (50x50 round button with semi-transparent background).
    /// </summary>
    public static Border Chevron(bool right = false, Action? onClick = null)
    {
        var btn = new Border
        {
            Width = 50,
            Height = 50,
            CornerRadius = new CornerRadius(25),
            Background = B("rgba(10,13,18,.85)"),
            Cursor = System.Windows.Input.Cursors.Hand,
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
    /// Steam Deck Green Play / Action Button.
    /// Strictly implements t_dusk.py:40 .play
    /// </summary>
    public static Border GreenActionButton(string text, double w = 400, double h = 74, Action? onClick = null)
    {
        var btn = new Border
        {
            Width = w,
            Height = h,
            CornerRadius = new CornerRadius(4),
            Background = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0.5),
                EndPoint = new Point(1, 0.5),
                GradientStops = { new(GreenGrad0, 0.0), new(GreenGrad1, 1.0) }
            },
            BorderBrush = WhiteBrush,
            BorderThickness = new Thickness(3),
            Cursor = System.Windows.Input.Cursors.Hand,
            Effect = new DropShadowEffect { Color = Colors.White, BlurRadius = 30, ShadowDepth = 0, Opacity = 0.3 }
        };

        var stack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        stack.Children.Add(new TextBlock
        {
            Text = "\uE768", // play icon
            FontFamily = Icons,
            FontSize = 26,
            Foreground = WhiteBrush,
            Margin = new Thickness(0, 0, 16, 0),
            VerticalAlignment = VerticalAlignment.Center
        });

        stack.Children.Add(new TextBlock
        {
            Text = text,
            FontFamily = Display,
            FontSize = 30,
            FontWeight = FontWeights.Bold,
            Foreground = WhiteBrush,
            VerticalAlignment = VerticalAlignment.Center
        });

        btn.Child = stack;
        if (onClick != null) btn.MouseLeftButtonUp += (_, _) => onClick();
        return btn;
    }

    /// <summary>
    /// Steam Deck Settings Shell: Authentic two-pane architecture.
    /// Left sidebar (370px): Navigation items.
    /// Right panel (1134x720): Slate background (#1b2129), header, content.
    /// Bottom bar: Steam Deck button bar.
    /// Strictly implements t_dusk.py:settings() and shell().
    /// </summary>
    public static Canvas SettingsShell(
        string activeKey,
        string title,
        string lead,
        FrameworkElement content,
        (string Key, string Label)[] hints,
        Action<string>? onNav)
    {
        var root = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };
        root.Children.Add(Backdrop("x05pro", 0.18));

        // 1. Top status bar
        root.Children.Add(TopBar());

        // 2. Left Navigation Sidebar at (32, 100, w: 370)
        var navPanel = new StackPanel { Width = 370, Orientation = Orientation.Vertical };
        var pages = new (string Name, string Key, string Icon)[]
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

        foreach (var (name, key, icon) in pages)
        {
            bool active = string.Equals(key, activeKey, StringComparison.OrdinalIgnoreCase);
            var item = new Border
            {
                Height = 52,
                CornerRadius = new CornerRadius(4),
                Background = active ? SlateTabOnBrush : Brushes.Transparent,
                Margin = new Thickness(0, 0, 0, 4),
                Padding = new Thickness(20, 0, 20, 0),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            if (active)
            {
                item.BorderBrush = CyanNeonBrush;
                item.BorderThickness = new Thickness(2);
                item.Effect = new DropShadowEffect { Color = CyanNeon, BlurRadius = 14, ShadowDepth = 0, Opacity = 0.5 };
            }

            var itemStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            itemStack.Children.Add(new TextBlock
            {
                Text = icon,
                FontFamily = Icons,
                FontSize = 20,
                Foreground = active ? CyanNeonBrush : MuteBrush,
                Margin = new Thickness(0, 0, 16, 0),
                VerticalAlignment = VerticalAlignment.Center
            });

            itemStack.Children.Add(new TextBlock
            {
                Text = name,
                FontFamily = Display,
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = active ? WhiteBrush : LightBrush,
                VerticalAlignment = VerticalAlignment.Center
            });

            item.Child = itemStack;
            item.MouseLeftButtonUp += (_, _) => onNav?.Invoke(key);
            navPanel.Children.Add(item);
        }

        Canvas.SetLeft(navPanel, 32);
        Canvas.SetTop(navPanel, 100);
        root.Children.Add(navPanel);

        // 3. Right Slate Panel at (434, 100, w: 1134, h: 720)
        var panel = new Border
        {
            Width = 1134,
            Height = 720,
            CornerRadius = new CornerRadius(8),
            Background = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
                GradientStops =
                {
                    new(C("#1d242e"), 0.0),
                    new(C("#131820"), 1.0)
                }
            },
            BorderBrush = B("rgba(26, 159, 255, 0.25)"),
            BorderThickness = new Thickness(1),
            ClipToBounds = true,
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 40, ShadowDepth = 16, Opacity = 0.55 }
        };

        var panelCanvas = new Canvas { Width = 1134, Height = 720 };

        // Specular cyan top highlight edge
        var topHighlight = new Border
        {
            Width = 1134,
            Height = 1.5,
            Background = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0.5),
                EndPoint = new Point(1, 0.5),
                GradientStops =
                {
                    new(C("rgba(26, 159, 255, 0.8)"), 0.0),
                    new(C("rgba(89, 191, 64, 0.5)"), 0.4),
                    new(Colors.Transparent, 1.0)
                }
            }
        };
        Canvas.SetLeft(topHighlight, 0);
        Canvas.SetTop(topHighlight, 0);
        panelCanvas.Children.Add(topHighlight);

        // Header block at (46, 30)
        var header = new StackPanel { Width = 1040, Orientation = Orientation.Vertical };
        header.Children.Add(new TextBlock
        {
            Text = title,
            FontFamily = Display,
            FontSize = 34,
            FontWeight = FontWeights.ExtraBold,
            Foreground = WhiteBrush
        });

        if (!string.IsNullOrEmpty(lead))
        {
            header.Children.Add(new TextBlock
            {
                Text = lead,
                FontFamily = Body,
                FontSize = 15,
                Foreground = MuteBrush,
                Margin = new Thickness(0, 4, 0, 0),
                TextWrapping = TextWrapping.Wrap
            });
        }
        Canvas.SetLeft(header, 46);
        Canvas.SetTop(header, 30);
        panelCanvas.Children.Add(header);

        // Subpage content placed below header
        Canvas.SetLeft(content, 46);
        Canvas.SetTop(content, 114);
        panelCanvas.Children.Add(content);

        panel.Child = panelCanvas;
        Canvas.SetLeft(panel, 434);
        Canvas.SetTop(panel, 100);
        root.Children.Add(panel);

        // 4. Bottom bar at y = 844
        var bar = BottomBar(hints);
        Canvas.SetLeft(bar, 0);
        Canvas.SetTop(bar, 844);
        root.Children.Add(bar);

        return root;
    }
}
