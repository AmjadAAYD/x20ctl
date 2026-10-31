using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace X20Ctl.Product.Spotlight;

/// <summary>
/// Reusable UI layout elements for Spotlight:
/// The Programme navigation rail, controller pedestal/star with reflection,
/// page frames, header status, script paper, and hints.
/// </summary>
internal static class SpotlightParts
{
    public static readonly (string Name, string Key)[] Pages =
    {
        ("Buttons", "buttons"),
        ("Curves", "curves"),
        ("Macros", "macros"),
        ("Vibration", "vibration"),
        ("PC mode", "pcmode"),
        ("Device", "device"),
        ("Mouse", "mouse"),
        ("Setups", "setups"),
        ("Tools", "tools")
    };

    public static BitmapImage? GetThemeLogo()
    {
        try
        {
            var logoPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Brand", "themes", "spotlight", "logo.png");
            if (File.Exists(logoPath))
            {
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.UriSource = new Uri(logoPath, UriKind.Absolute);
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.EndInit();
                return bi;
            }
        }
        catch { }
        return null;
    }

    /// <summary>
    /// Left Programme rail: act numbers I to IX, Spotlight logo, and warm spotlight on active act.
    /// </summary>
    public static Canvas ProgrammeRail(string currentKey, Action<string>? onNavigate = null)
    {
        var rail = new Canvas { Width = 250, Height = 900, ClipToBounds = true };

        // Sidebar background
        var bg = new Rectangle
        {
            Width = 250, Height = 900,
            Fill = new LinearGradientBrush
            {
                StartPoint = new(0, 0), EndPoint = new(1, 0),
                GradientStops = { new(SpotlightKit.A(SpotlightKit.C("#0a0505"), 0.94), 0.0), new(SpotlightKit.A(SpotlightKit.C("#0a0505"), 0.82), 1.0) }
            }
        };
        rail.Children.Add(bg);

        // Right gold edge line
        var edge = new Rectangle
        {
            Width = 1, Height = 900,
            Fill = SpotlightKit.B(SpotlightKit.A(SpotlightKit.Gold, 0.28))
        };
        Canvas.SetLeft(edge, 249);
        Canvas.SetTop(edge, 0);
        rail.Children.Add(edge);

        // Logo at (38, 48) width 92px
        var logoImg = GetThemeLogo();
        if (logoImg != null)
        {
            var logo = new Image
            {
                Source = logoImg,
                Width = 92,
                Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 10, ShadowDepth = 6, Opacity = 0.7 }
            };
            Canvas.SetLeft(logo, 38);
            Canvas.SetTop(logo, 48);
            rail.Children.Add(logo);
        }

        // --- DASHBOARD BUTTON (Premier item at top of sidebar) ---
        bool dashOn = string.Equals(currentKey, "dashboard", StringComparison.OrdinalIgnoreCase);
        double dashY = 120;
        if (dashOn)
        {
            var activeGlow = new Ellipse
            {
                Width = 240, Height = 56,
                Fill = new RadialGradientBrush
                {
                    Center = new(0.40, 0.55), RadiusX = 0.60, RadiusY = 0.60,
                    GradientStops = { new(SpotlightKit.A(SpotlightKit.C("#ffdcaf"), 0.28), 0.0), new(Colors.Transparent, 0.75) }
                }
            };
            Canvas.SetLeft(activeGlow, 10);
            Canvas.SetTop(activeGlow, dashY - 14);
            rail.Children.Add(activeGlow);

            var bar = new Rectangle
            {
                Width = 3, Height = 36,
                Fill = SpotlightKit.GoldBrush,
                Effect = new DropShadowEffect { Color = SpotlightKit.C("#ffb860"), BlurRadius = 10, ShadowDepth = 0, Opacity = 0.8 }
            };
            Canvas.SetLeft(bar, 0);
            Canvas.SetTop(bar, dashY - 4);
            rail.Children.Add(bar);
        }

        var dashPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Cursor = System.Windows.Input.Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center
        };
        var starGlyph = new TextBlock
        {
            Text = "★",
            FontFamily = SpotlightKit.Display,
            FontSize = 14,
            Width = 34,
            Foreground = dashOn ? SpotlightKit.GoldHiBrush : SpotlightKit.B("#7a6c5a"),
            VerticalAlignment = VerticalAlignment.Center
        };
        dashPanel.Children.Add(starGlyph);

        var dashLabel = new TextBlock
        {
            Text = "DASHBOARD",
            FontFamily = SpotlightKit.Display,
            FontSize = dashOn ? 14.5 : 13.5,
            FontWeight = FontWeights.Light,
            Foreground = dashOn ? SpotlightKit.B("#fff6e6") : SpotlightKit.B("#a4957f"),
            VerticalAlignment = VerticalAlignment.Center
        };
        if (dashOn)
        {
            dashLabel.Effect = new DropShadowEffect { Color = SpotlightKit.C("#ffc878"), BlurRadius = 14, ShadowDepth = 0, Opacity = 0.6 };
        }
        dashPanel.Children.Add(dashLabel);

        if (onNavigate != null)
        {
            dashPanel.MouseLeftButtonDown += (_, _) => onNavigate("dashboard");
        }
        Canvas.SetLeft(dashPanel, 40);
        Canvas.SetTop(dashPanel, dashY);
        rail.Children.Add(dashPanel);

        // "PROGRAMME · ACTS" header at (40, 172)
        var progHeader = new TextBlock
        {
            Text = "PROGRAMME · ACTS",
            FontFamily = SpotlightKit.Display,
            FontSize = 10,
            FontWeight = FontWeights.Light,
            Foreground = SpotlightKit.MuteBrush
        };
        Canvas.SetLeft(progHeader, 40);
        Canvas.SetTop(progHeader, 172);
        rail.Children.Add(progHeader);

        // Gold accent line at (40, 192)
        var progLine = new Rectangle
        {
            Width = 170, Height = 1,
            Fill = new LinearGradientBrush
            {
                StartPoint = new(0, 0), EndPoint = new(1, 0),
                GradientStops = { new(SpotlightKit.Gold, 0.0), new(Colors.Transparent, 1.0) }
            }
        };
        Canvas.SetLeft(progLine, 40);
        Canvas.SetTop(progLine, 192);
        rail.Children.Add(progLine);

        // Act items I through IX at y = 216 + i * 54
        for (int i = 0; i < Pages.Length; i++)
        {
            var (name, key) = Pages[i];
            double y = 216 + i * 54;
            bool on = string.Equals(key, currentKey, StringComparison.OrdinalIgnoreCase);

            if (on)
            {
                // Warm spot glow behind active act
                var activeGlow = new Ellipse
                {
                    Width = 240, Height = 56,
                    Fill = new RadialGradientBrush
                    {
                        Center = new(0.40, 0.55), RadiusX = 0.60, RadiusY = 0.60,
                        GradientStops = { new(SpotlightKit.A(SpotlightKit.C("#ffdcaf"), 0.28), 0.0), new(Colors.Transparent, 0.75) }
                    }
                };
                Canvas.SetLeft(activeGlow, 10);
                Canvas.SetTop(activeGlow, y - 14);
                rail.Children.Add(activeGlow);

                // Gold left indicator bar
                var bar = new Rectangle
                {
                    Width = 3, Height = 36,
                    Fill = SpotlightKit.GoldBrush,
                    Effect = new DropShadowEffect { Color = SpotlightKit.C("#ffb860"), BlurRadius = 10, ShadowDepth = 0, Opacity = 0.8 }
                };
                Canvas.SetLeft(bar, 0);
                Canvas.SetTop(bar, y - 4);
                rail.Children.Add(bar);
            }

            var itemPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Cursor = System.Windows.Input.Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Roman numeral
            var num = new TextBlock
            {
                Text = SpotlightKit.Roman(i),
                FontFamily = SpotlightKit.Bodoni,
                FontSize = 14.5,
                Width = 34,
                Foreground = on ? SpotlightKit.GoldHiBrush : SpotlightKit.B("#7a6c5a")
            };
            itemPanel.Children.Add(num);

            // Page Name
            var label = new TextBlock
            {
                Text = name.ToUpperInvariant(),
                FontFamily = SpotlightKit.Display,
                FontSize = on ? 14 : 12.5,
                FontWeight = FontWeights.Light,
                Foreground = on ? SpotlightKit.B("#fff6e6") : SpotlightKit.B("#a4957f")
            };
            if (on)
            {
                label.Effect = new DropShadowEffect { Color = SpotlightKit.C("#ffc878"), BlurRadius = 14, ShadowDepth = 0, Opacity = 0.6 };
            }
            itemPanel.Children.Add(label);

            if (onNavigate != null)
            {
                string targetKey = key;
                itemPanel.MouseLeftButtonDown += (_, _) => onNavigate(targetKey);
            }

            Canvas.SetLeft(itemPanel, 40);
            Canvas.SetTop(itemPanel, y);
            rail.Children.Add(itemPanel);
        }

        return rail;
    }

    /// <summary>
    /// Controller star on stage with pedestal ring, deep contact shadow, and inverted reflection on the polished floor.
    /// </summary>
    public static Canvas SpotlightStar(string modelId, double cx, double baseFloor, double width, double reflection = 0.18, UIElement? customPad = null)
    {
        var canvas = new Canvas { Width = 1600, Height = 900, IsHitTestVisible = false };

        // Controller height roughly 0.64 of width
        double height = width * 0.64;
        double x = cx - width / 2;
        double y = baseFloor - height;

        // Stage Pedestal Mark / Floor Ring
        var pedestal = new Ellipse
        {
            Width = width * 0.88,
            Height = 36,
            Stroke = SpotlightKit.B(SpotlightKit.A(SpotlightKit.Gold, 0.40)),
            StrokeThickness = 1.5,
            Fill = new RadialGradientBrush
            {
                Center = new(0.5, 0.5), RadiusX = 0.5, RadiusY = 0.5,
                GradientStops =
                {
                    new(SpotlightKit.A(SpotlightKit.C("#ffe6c4"), 0.35), 0.0),
                    new(SpotlightKit.A(SpotlightKit.C("#d9b77a"), 0.15), 0.60),
                    new(Colors.Transparent, 1.0)
                }
            }
        };
        Canvas.SetLeft(pedestal, cx - (width * 0.88) / 2);
        Canvas.SetTop(pedestal, baseFloor - 18);
        canvas.Children.Add(pedestal);

        // Deep contact floor shadow
        var contactShadow = new Ellipse
        {
            Width = width * 0.72,
            Height = 16,
            Fill = new RadialGradientBrush
            {
                Center = new(0.5, 0.5), RadiusX = 0.5, RadiusY = 0.5,
                GradientStops = { new(SpotlightKit.A(Colors.Black, 0.80), 0.0), new(Colors.Transparent, 1.0) }
            }
        };
        Canvas.SetLeft(contactShadow, cx - (width * 0.72) / 2);
        Canvas.SetTop(contactShadow, baseFloor - 8);
        canvas.Children.Add(contactShadow);

        // Inverted floor reflection
        if (reflection > 0)
        {
            var reflElement = customPad != null ? CloneVisual(customPad) : CreateControllerArt(modelId, width, height);
            reflElement.Opacity = reflection;
            reflElement.RenderTransformOrigin = new Point(0.5, 0);
            reflElement.RenderTransform = new ScaleTransform(1, -0.6);
            reflElement.Effect = new BlurEffect { Radius = 3.5 };

            // Mask fading to transparent downwards
            reflElement.OpacityMask = new LinearGradientBrush
            {
                StartPoint = new(0, 0), EndPoint = new(0, 1),
                GradientStops = { new(Colors.Black, 0.0), new(Colors.Black, 0.35), new(Colors.Transparent, 1.0) }
            };

            Canvas.SetLeft(reflElement, x);
            Canvas.SetTop(reflElement, baseFloor + 2);
            canvas.Children.Add(reflElement);
        }

        // Upright controller with warm champagne gold rim aura + drop shadow
        var padContainer = new Grid { Width = width, Height = height };

        // Check for dedicated rim image
        var rimBmp = GetControllerRimBitmap(modelId);
        if (rimBmp != null)
        {
            var rimImg = new Image
            {
                Source = rimBmp,
                Width = width,
                Height = height,
                Stretch = Stretch.Uniform,
                Opacity = 0.80,
                Effect = new DropShadowEffect
                {
                    Color = SpotlightKit.C("#ffd27a"),
                    BlurRadius = 18,
                    ShadowDepth = 0,
                    Opacity = 0.70
                }
            };
            RenderOptions.SetBitmapScalingMode(rimImg, BitmapScalingMode.HighQuality);
            padContainer.Children.Add(rimImg);
        }

        var pad = customPad ?? CreateControllerArt(modelId, width, height);
        pad.Effect = new DropShadowEffect
        {
            Color = Colors.Black,
            BlurRadius = 26,
            ShadowDepth = 20,
            Opacity = 0.70
        };
        padContainer.Children.Add(pad);

        Canvas.SetLeft(padContainer, x);
        Canvas.SetTop(padContainer, y);
        canvas.Children.Add(padContainer);

        return canvas;
    }

    public static BitmapImage? GetControllerBitmap(string modelId)
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

        var searchDirs = new List<string>();
        string appDir = AppContext.BaseDirectory;
        searchDirs.Add(System.IO.Path.Combine(appDir, "Assets", "art"));
        searchDirs.Add(System.IO.Path.Combine(appDir, "previews", "console-concepts", "art"));

        for (int up = 1; up <= 8; up++)
        {
            string upSteps = string.Join(System.IO.Path.DirectorySeparatorChar.ToString(), Enumerable.Repeat("..", up));
            string candidateRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(appDir, upSteps));
            searchDirs.Add(System.IO.Path.Combine(candidateRoot, "desktop-dotnet", "X20Ctl.Product", "Assets", "art"));
            searchDirs.Add(System.IO.Path.Combine(candidateRoot, "worktree-gemini", "desktop-dotnet", "X20Ctl.Product", "Assets", "art"));
            searchDirs.Add(System.IO.Path.Combine(candidateRoot, "previews", "console-concepts", "art"));
            searchDirs.Add(System.IO.Path.Combine(candidateRoot, "worktree-gemini", "previews", "console-concepts", "art"));
        }

        string[] filenames = { $"{artKey}.png", $"{safeModel}.png", $"{nativeFolder}.png", "x20.png", "x15-starfield.png", "x20pro.png" };

        foreach (var dir in searchDirs)
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var fn in filenames)
            {
                string full = System.IO.Path.Combine(dir, fn);
                if (File.Exists(full))
                {
                    try
                    {
                        var bi = new BitmapImage();
                        bi.BeginInit();
                        bi.UriSource = new Uri(System.IO.Path.GetFullPath(full), UriKind.Absolute);
                        bi.CacheOption = BitmapCacheOption.OnLoad;
                        bi.EndInit();
                        bi.Freeze();
                        return bi;
                    }
                    catch { }
                }
            }
        }

        // Try pack URIs
        try
        {
            var packUri = new Uri($"pack://application:,,,/Assets/native/{nativeFolder}/native/front-full.png");
            var bi = new BitmapImage(packUri);
            bi.Freeze();
            return bi;
        }
        catch { }

        try
        {
            var packUri = new Uri("pack://application:,,,/Assets/native/x20/native/front-full.png");
            var bi = new BitmapImage(packUri);
            bi.Freeze();
            return bi;
        }
        catch { }

        return null;
    }

    public static BitmapImage? GetControllerRimBitmap(string modelId)
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

        var searchDirs = new List<string>();
        string appDir = AppContext.BaseDirectory;
        searchDirs.Add(System.IO.Path.Combine(appDir, "Assets", "art"));
        searchDirs.Add(System.IO.Path.Combine(appDir, "previews", "console-concepts", "art"));

        for (int up = 1; up <= 8; up++)
        {
            string upSteps = string.Join(System.IO.Path.DirectorySeparatorChar.ToString(), Enumerable.Repeat("..", up));
            string candidateRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(appDir, upSteps));
            searchDirs.Add(System.IO.Path.Combine(candidateRoot, "desktop-dotnet", "X20Ctl.Product", "Assets", "art"));
            searchDirs.Add(System.IO.Path.Combine(candidateRoot, "worktree-gemini", "desktop-dotnet", "X20Ctl.Product", "Assets", "art"));
            searchDirs.Add(System.IO.Path.Combine(candidateRoot, "previews", "console-concepts", "art"));
            searchDirs.Add(System.IO.Path.Combine(candidateRoot, "worktree-gemini", "previews", "console-concepts", "art"));
        }

        string[] filenames = { $"{artKey}-rim.png", $"{safeModel}-rim.png", "x20-rim.png", "x15-starfield-rim.png" };

        foreach (var dir in searchDirs)
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var fn in filenames)
            {
                string full = System.IO.Path.Combine(dir, fn);
                if (File.Exists(full))
                {
                    try
                    {
                        var bi = new BitmapImage();
                        bi.BeginInit();
                        bi.UriSource = new Uri(System.IO.Path.GetFullPath(full), UriKind.Absolute);
                        bi.CacheOption = BitmapCacheOption.OnLoad;
                        bi.EndInit();
                        bi.Freeze();
                        return bi;
                    }
                    catch { }
                }
            }
        }

        return null;
    }

    public static UIElement CreateControllerArt(string modelId, double width, double height)
    {
        var bi = GetControllerBitmap(modelId);
        if (bi != null)
        {
            var img = new Image
            {
                Source = bi,
                Width = width,
                Height = height,
                Stretch = Stretch.Uniform
            };
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
            return img;
        }

        // Fallback: Viewbox-wrapped ControllerOutline
        string safeModel = modelId.Replace("_", "-").ToLowerInvariant();
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

        var outline = new ControllerOutline();
        outline.Show(nativeFolder, false);
        return new Viewbox
        {
            Width = width,
            Height = height,
            Stretch = Stretch.Uniform,
            Child = outline
        };
    }

    private static UIElement CloneVisual(UIElement element)
    {
        if (element is Image img && img.Source != null)
        {
            return new Image { Source = img.Source, Width = img.Width, Height = img.Height, Stretch = img.Stretch };
        }
        return new Rectangle { Width = 300, Height = 190, Fill = SpotlightKit.B(SpotlightKit.A(SpotlightKit.Gold, 0.2)) };
    }

    /// <summary>
    /// Top-right status readout: Player 1 · 19:24 · Battery icon.
    /// </summary>
    public static StackPanel StatusHeader(string player = "Player 1", string? time = null, double right = 60, double top = 82)
    {
        var stack = new StackPanel { Orientation = Orientation.Horizontal };

        // Player badge
        var playerText = new TextBlock
        {
            Text = player.ToUpperInvariant(),
            FontFamily = SpotlightKit.Display,
            FontSize = 12,
            FontWeight = FontWeights.Light,
            Foreground = SpotlightKit.GoldBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 22, 0)
        };
        stack.Children.Add(playerText);

        // Time
        var timeText = new TextBlock
        {
            Text = time ?? ThemeClock.Now,
            FontFamily = SpotlightKit.Body,
            FontSize = 17,
            Foreground = SpotlightKit.ChalkDimBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0)
        };
        stack.Children.Add(timeText);

        return stack;
    }

    /// <summary>
    /// Bottom-right hints bar with brass keys and Garamond labels.
    /// </summary>
    public static StackPanel HintsBar(IEnumerable<(string Key, string Label)> hints)
    {
        var stack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        foreach (var (k, text) in hints)
        {
            var item = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 26, 0), VerticalAlignment = VerticalAlignment.Center };
            var keyEl = SpotlightKit.BrassKey(k, false, 24);
            keyEl.Margin = new Thickness(0, 0, 9, 0);
            item.Children.Add(keyEl);

            var labelEl = new TextBlock
            {
                Text = text,
                FontFamily = SpotlightKit.Body,
                FontSize = 16,
                Foreground = SpotlightKit.ChalkDimBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            item.Children.Add(labelEl);
            stack.Children.Add(item);
        }
        return stack;
    }

    /// <summary>
    /// A Button Highlight: glowing tungsten circle ring over the chosen control.
    /// </summary>
    public static Canvas AButtonHighlight(double cx, double cy, double r = 22)
    {
        var canvas = new Canvas { Width = r * 4, Height = r * 4, IsHitTestVisible = false };
        Canvas.SetLeft(canvas, cx - r * 2);
        Canvas.SetTop(canvas, cy - r * 2);

        // Outer radial light
        var glow = new Ellipse
        {
            Width = r * 4, Height = r * 4,
            Fill = new RadialGradientBrush
            {
                Center = new(0.5, 0.5), RadiusX = 0.5, RadiusY = 0.5,
                GradientStops =
                {
                    new(SpotlightKit.A(SpotlightKit.C("#fff0d2"), 0.70), 0.0),
                    new(SpotlightKit.A(SpotlightKit.C("#ffc878"), 0.25), 0.60),
                    new(Colors.Transparent, 1.0)
                }
            }
        };
        canvas.Children.Add(glow);

        // Sharp highlight ring
        var ring = new Ellipse
        {
            Width = r * 2, Height = r * 2,
            Stroke = SpotlightKit.GoldHiBrush,
            StrokeThickness = 1.5,
            Effect = new DropShadowEffect { Color = SpotlightKit.C("#ffb860"), BlurRadius = 6, ShadowDepth = 0, Opacity = 0.8 }
        };
        Canvas.SetLeft(ring, r);
        Canvas.SetTop(ring, r);
        canvas.Children.Add(ring);

        return canvas;
    }

    /// <summary>
    /// Cream script paper (used by Agent XCTL and Feedback) with slight theatrical tilt.
    /// </summary>
    public static Border ScriptPaper(UIElement content, double width = 760, double height = 470, double angle = -0.6)
    {
        var border = new Border
        {
            Width = width, Height = height,
            Background = new LinearGradientBrush
            {
                StartPoint = new(0, 0), EndPoint = new(0, 1),
                GradientStops = { new(SpotlightKit.C("#efe4cf"), 0.0), new(SpotlightKit.C("#e2d4b8"), 1.0) }
            },
            BorderThickness = new Thickness(0),
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 60, ShadowDepth = 30, Opacity = 0.7 },
            RenderTransform = new RotateTransform(angle),
            RenderTransformOrigin = new Point(0.5, 0.5),
            Padding = new Thickness(40, 36, 40, 36),
            Child = content
        };
        return border;
    }
}
