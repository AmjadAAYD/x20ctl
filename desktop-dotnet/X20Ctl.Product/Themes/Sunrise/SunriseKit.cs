using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace X20Ctl.Product.Sunrise;

/// <summary>
/// Design system primitives, colors, typography, and controls for the Sunrise theme:
/// "Warm, bright, retro-TV pop: sunshine yellow, cream sticker cards, thick black display type, orange accents, concentric rings and halftone dots."
/// Strictly implements 05_SUNRISE_SPEC.txt.
/// </summary>
public static class SunriseKit
{
    // Palette
    public static readonly Color Yellow = Color.FromRgb(0xFE, 0xDB, 0x0C);        // #FEDB0C Sunshine yellow
    public static readonly Color Cream = Color.FromRgb(0xFD, 0xF3, 0xD2);         // #FDF3D2 Cream sticker card
    public static readonly Color Ink = Color.FromRgb(0x1E, 0x1D, 0x1A);           // #1E1D1A Near-black ink
    public static readonly Color Orange = Color.FromRgb(0xF8, 0x7B, 0x09);        // #F87B09 Vivid orange accent
    public static readonly Color Gold = Color.FromRgb(0xFD, 0xC7, 0x13);          // #FDC713 Deep gold
    public static readonly Color Olive = Color.FromRgb(0x7D, 0x76, 0x52);         // #7D7652 Olive support
    public static readonly Color Danger = Color.FromRgb(0xE8, 0x45, 0x1E);        // #E8451E Red-orange danger

    // Brushes
    public static readonly SolidColorBrush YellowBrush = new(Yellow);
    public static readonly SolidColorBrush CreamBrush = new(Cream);
    public static readonly SolidColorBrush InkBrush = new(Ink);
    public static readonly SolidColorBrush OrangeBrush = new(Orange);
    public static readonly SolidColorBrush GoldBrush = new(Gold);
    public static readonly SolidColorBrush OliveBrush = new(Olive);
    public static readonly SolidColorBrush DangerBrush = new(Danger);

    // Aliases for compatibility
    public static readonly SolidColorBrush PrimaryBrush = YellowBrush;
    public static readonly SolidColorBrush CardBrush = CreamBrush;
    public static readonly SolidColorBrush DarkBrush = InkBrush;
    public static readonly SolidColorBrush MutedBrush = OliveBrush;
    public static readonly SolidColorBrush VoidBrush = YellowBrush;
    public static readonly SolidColorBrush DeepBrush = CreamBrush;
    public static readonly SolidColorBrush NightBrush = CreamBrush;
    public static readonly SolidColorBrush AccentBrush = OrangeBrush;
    public static readonly SolidColorBrush RuleBrush = InkBrush;
    public static readonly SolidColorBrush SoftBrush = OliveBrush;
    public static readonly SolidColorBrush VoidTextBrush = InkBrush;
    public static readonly SolidColorBrush WhiteBrush = CreamBrush;

    // Fonts
    public static readonly FontFamily Display = new("Bahnschrift, Arial Black, Segoe UI Black, sans-serif");
    public static readonly FontFamily Body = new("Segoe UI Variable Text, Segoe UI, sans-serif");
    public static readonly FontFamily Mono = new("Consolas, Courier New, monospace");
    public static readonly FontFamily Icons = new("Segoe Fluent Icons, Segoe UI Symbol, sans-serif");

    public static Color A(Color c, double alpha) => Color.FromArgb((byte)Math.Clamp((int)(alpha * 255), 0, 255), c.R, c.G, c.B);

    /// <summary>
    /// Background: Sunshine yellow field with halftone dots and soft concentric sun rings.
    /// Pure procedural WPF vectors.
    /// </summary>
    public static Canvas CreateHalftoneBackdrop(double width = 1600, double height = 900)
    {
        var canvas = new Canvas { Width = width, Height = height, ClipToBounds = true };

        // Sunshine yellow base
        var baseRect = new Rectangle
        {
            Width = width,
            Height = height,
            Fill = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1),
                GradientStops =
                {
                    new GradientStop(Color.FromRgb(0xFF, 0xEB, 0x0D), 0.0),
                    new GradientStop(Color.FromRgb(0xFE, 0xDB, 0x0C), 0.5),
                    new GradientStop(Color.FromRgb(0xFD, 0xC7, 0x13), 1.0)
                }
            }
        };
        canvas.Children.Add(baseRect);

        // Soft concentric sun rings in top-right
        double sunCx = width - 200;
        double sunCy = 140;
        for (int r = 100; r <= 600; r += 70)
        {
            var ring = new Ellipse
            {
                Width = r * 2,
                Height = r * 2,
                Stroke = new SolidColorBrush(Color.FromArgb(0x28, 0xF8, 0x7B, 0x09)),
                StrokeThickness = 2.5
            };
            Canvas.SetLeft(ring, sunCx - r);
            Canvas.SetTop(ring, sunCy - r);
            canvas.Children.Add(ring);
        }

        // Halftone dot pattern across right half
        var dotsCanvas = new Canvas { Width = width, Height = height, IsHitTestVisible = false };
        int step = 28;
        for (int x = 600; x < width; x += step)
        {
            for (int y = 0; y < height; y += step)
            {
                double dist = Math.Sqrt((x - width) * (x - width) + y * y);
                double radius = Math.Max(1.0, (1.0 - (dist / 1400.0)) * 6.0);
                if (radius > 1.2)
                {
                    var dot = new Ellipse
                    {
                        Width = radius * 2,
                        Height = radius * 2,
                        Fill = new SolidColorBrush(Color.FromArgb(0x30, 0xF8, 0x7B, 0x09))
                    };
                    Canvas.SetLeft(dot, x - radius);
                    Canvas.SetTop(dot, y - radius);
                    dotsCanvas.Children.Add(dot);
                }
            }
        }
        canvas.Children.Add(dotsCanvas);

        return canvas;
    }

    /// <summary>
    /// TV-shaped action panel: thick black rounded border, cream screen interior,
    /// and two twin antenna lines with ball tips rising from the top right.
    /// </summary>
    public static FrameworkElement CreateTvScreenCard(FrameworkElement child, double width, double height, bool withAntennae = true)
    {
        var container = new Canvas { Width = width, Height = height + (withAntennae ? 60 : 0) };

        if (withAntennae)
        {
            // Twin antennae rising from top right of the TV
            double ax1 = width - 180;
            double ay1 = 55;
            double ax2 = width - 110;
            double ay2 = 55;

            // Left antenna: slanted left with tip ball
            var antL = new Line { X1 = ax1, Y1 = ay1, X2 = ax1 - 40, Y2 = 8, Stroke = InkBrush, StrokeThickness = 3.5, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
            var ballL = new Ellipse { Width = 14, Height = 14, Fill = OrangeBrush, Stroke = InkBrush, StrokeThickness = 2.5 };
            Canvas.SetLeft(ballL, ax1 - 47);
            Canvas.SetTop(ballL, 1);
            container.Children.Add(antL);
            container.Children.Add(ballL);

            // Right antenna: slanted right with tip ball
            var antR = new Line { X1 = ax2, Y1 = ay2, X2 = ax2 + 45, Y2 = 2, Stroke = InkBrush, StrokeThickness = 3.5, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
            var ballR = new Ellipse { Width = 14, Height = 14, Fill = OrangeBrush, Stroke = InkBrush, StrokeThickness = 2.5 };
            Canvas.SetLeft(ballR, ax2 + 38);
            Canvas.SetTop(ballR, -5);
            container.Children.Add(antR);
            container.Children.Add(ballR);
        }

        var tvOuter = new Border
        {
            Width = width,
            Height = height,
            CornerRadius = new CornerRadius(24),
            Background = InkBrush,
            Padding = new Thickness(8),
            Effect = new DropShadowEffect
            {
                Color = Color.FromArgb(0x80, 0x1E, 0x1D, 0x1A),
                BlurRadius = 16,
                ShadowDepth = 6,
                Direction = 315,
                Opacity = 0.5
            }
        };

        var tvScreen = new Border
        {
            CornerRadius = new CornerRadius(18),
            Background = CreamBrush,
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 0x1E, 0x1D, 0x1A)),
            BorderThickness = new Thickness(1.5),
            Padding = new Thickness(14, 12, 14, 12),
            Child = child
        };
        tvOuter.Child = tvScreen;

        Canvas.SetLeft(tvOuter, 0);
        Canvas.SetTop(tvOuter, withAntennae ? 55 : 0);
        container.Children.Add(tvOuter);

        return container;
    }

    /// <summary>
    /// Rounded sticker card: cream surface, thick dark outline, soft shadow.
    /// </summary>
    public static Border CreateStickerCard(FrameworkElement child, double width, double height, double cornerRadius = 18)
    {
        var card = new Border
        {
            Width = width,
            Height = height,
            CornerRadius = new CornerRadius(cornerRadius),
            Background = CreamBrush,
            BorderBrush = InkBrush,
            BorderThickness = new Thickness(2.5),
            Padding = new Thickness(16),
            Effect = new DropShadowEffect
            {
                Color = Color.FromArgb(0x80, 0x1E, 0x1D, 0x1A),
                BlurRadius = 12,
                ShadowDepth = 5,
                Direction = 315,
                Opacity = 0.4
            },
            Child = child
        };
        return card;
    }

    /// <summary>
    /// Sunrise primary button (black pill with white text & orange arrow circle)
    /// or secondary button (cream pill with dark outline).
    /// </summary>
    public static Button SunriseButton(
        string label,
        Action onClick,
        bool isPrimary = false,
        bool isDanger = false,
        double? width = 160,
        double height = 40,
        string? tag = null)
    {
        var btn = new Button
        {
            Height = height,
            Tag = tag,
            Cursor = Cursors.Hand,
            FocusVisualStyle = null
        };
        if (width.HasValue) btn.Width = width.Value;
        else btn.HorizontalAlignment = HorizontalAlignment.Stretch;

        var border = new Border
        {
            CornerRadius = new CornerRadius(height / 2),
            Background = isDanger ? DangerBrush : (isPrimary ? InkBrush : CreamBrush),
            BorderBrush = isDanger ? DangerBrush : InkBrush,
            BorderThickness = new Thickness(isPrimary ? 0 : 2),
            Padding = new Thickness(isPrimary ? 16 : 14, 0, isPrimary ? 10 : 14, 0),
            Effect = new DropShadowEffect
            {
                Color = Color.FromArgb(0x60, 0x1E, 0x1D, 0x1A),
                BlurRadius = 8,
                ShadowDepth = 3,
                Direction = 315,
                Opacity = 0.35
            }
        };

        var sp = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var tb = new TextBlock
        {
            Text = label.ToUpperInvariant(),
            FontFamily = Display,
            FontWeight = FontWeights.Bold,
            FontSize = 12,
            Foreground = isPrimary || isDanger ? CreamBrush : InkBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
        sp.Children.Add(tb);

        if (isPrimary)
        {
            // Circular orange arrow accent at the right end
            var arrowCircle = new Border
            {
                Width = 22,
                Height = 22,
                CornerRadius = new CornerRadius(11),
                Background = OrangeBrush,
                Margin = new Thickness(10, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            arrowCircle.Child = new TextBlock
            {
                Text = "➔",
                FontSize = 11,
                FontWeight = FontWeights.Black,
                Foreground = CreamBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(1, 0, 0, 0)
            };
            sp.Children.Add(arrowCircle);
        }

        border.Child = sp;
        btn.Template = new ControlTemplate(typeof(Button)) { VisualTree = new FrameworkElementFactory(typeof(ContentPresenter)) };
        btn.Content = border;

        btn.Click += (_, e) =>
        {
            e.Handled = true;
            onClick();
        };

        // Hover lift
        border.MouseEnter += (_, _) =>
        {
            border.Background = isDanger ? new SolidColorBrush(Color.FromRgb(0xff, 0x5a, 0x33)) : (isPrimary ? new SolidColorBrush(Color.FromRgb(0x35, 0x34, 0x2f)) : YellowBrush);
        };
        border.MouseLeave += (_, _) =>
        {
            border.Background = isDanger ? DangerBrush : (isPrimary ? InkBrush : CreamBrush);
        };

        return btn;
    }

    /// <summary>
    /// Rounded rectangle chip for sub-tabs and toggles
    /// </summary>
    public static Border SunriseChip(string label, bool isSelected, Action? onClick = null, string? tag = null)
    {
        var chip = new Border
        {
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(14, 6, 14, 6),
            Margin = new Thickness(0, 0, 8, 0),
            Background = isSelected ? InkBrush : OrangeBrush,
            BorderBrush = InkBrush,
            BorderThickness = new Thickness(isSelected ? 0 : 1.5),
            Cursor = onClick != null ? Cursors.Hand : Cursors.Arrow,
            Tag = tag,
            Effect = new DropShadowEffect
            {
                Color = Color.FromArgb(0x40, 0x1E, 0x1D, 0x1A),
                BlurRadius = 4,
                ShadowDepth = 2,
                Direction = 315,
                Opacity = 0.25
            }
        };

        var sp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var tb = new TextBlock
        {
            Text = label.ToUpperInvariant(),
            FontFamily = Display,
            FontWeight = FontWeights.Bold,
            FontSize = 11.5,
            Foreground = isSelected ? CreamBrush : InkBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
        sp.Children.Add(tb);

        if (isSelected)
        {
            var dot = new Ellipse
            {
                Width = 6,
                Height = 6,
                Fill = OrangeBrush,
                Margin = new Thickness(6, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            sp.Children.Add(dot);
        }

        chip.Child = sp;

        if (onClick != null)
        {
            chip.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                onClick();
            };
        }

        return chip;
    }

    /// <summary>
    /// Sunrise navigation item: cream card with orange vertical tab;
    /// Selected: black card with white text and orange dot at right.
    /// </summary>
    public static Border SunriseNavItem(string icon, string label, bool isSelected, Action onClick)
    {
        var card = new Border
        {
            Width = 236,
            Height = 44,
            CornerRadius = new CornerRadius(12),
            Background = isSelected ? InkBrush : CreamBrush,
            BorderBrush = InkBrush,
            BorderThickness = new Thickness(isSelected ? 0 : 2),
            Margin = new Thickness(0, 0, 0, 8),
            Cursor = Cursors.Hand,
            Effect = new DropShadowEffect
            {
                Color = Color.FromArgb(0x50, 0x1E, 0x1D, 0x1A),
                BlurRadius = 6,
                ShadowDepth = 3,
                Direction = 315,
                Opacity = 0.3
            }
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });

        // Left orange tab on unselected cards
        if (!isSelected)
        {
            var orangeTab = new Border
            {
                Width = 4,
                Height = 22,
                CornerRadius = new CornerRadius(2),
                Background = OrangeBrush,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(orangeTab, 0);
            grid.Children.Add(orangeTab);
        }

        var ic = new TextBlock
        {
            Text = icon,
            FontFamily = Icons,
            FontSize = 14,
            Foreground = isSelected ? CreamBrush : InkBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(ic, 1);
        grid.Children.Add(ic);

        var lb = new TextBlock
        {
            Text = label.ToUpperInvariant(),
            FontFamily = Display,
            FontWeight = FontWeights.Bold,
            FontSize = 12,
            Foreground = isSelected ? CreamBrush : InkBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(lb, 2);
        grid.Children.Add(lb);

        // Right orange dot on selected cards
        if (isSelected)
        {
            var dot = new Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = OrangeBrush,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            Grid.SetColumn(dot, 3);
            grid.Children.Add(dot);
        }

        card.Child = grid;

        card.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            onClick();
        };

        return card;
    }

    /// <summary>
    /// Sunrise Title block
    /// </summary>
    public static StackPanel SunriseTitle(string title, string subtitle)
    {
        var sp = new StackPanel();
        var tb = new TextBlock
        {
            Text = title.ToUpperInvariant(),
            FontFamily = Display,
            FontWeight = FontWeights.Black,
            FontSize = 42,
            Foreground = InkBrush,
            Margin = new Thickness(0, 0, 0, 4)
        };
        sp.Children.Add(tb);

        var sub = new TextBlock
        {
            Text = subtitle,
            FontFamily = Body,
            FontWeight = FontWeights.SemiBold,
            FontSize = 13,
            Foreground = OliveBrush
        };
        sp.Children.Add(sub);

        return sp;
    }

    /// <summary>
    /// Controller vector art with concentric sun rings behind it on a cream background
    /// </summary>
    public static FrameworkElement ControllerArt(string model = "x20", double width = 380)
    {
        double height = width * 0.7;
        var canvas = new Canvas { Width = width, Height = height, ClipToBounds = true };

        // Concentric sun ripples behind pad
        double cx = width / 2.0;
        double cy = height / 2.0;
        for (int r = 40; r <= width / 2; r += 32)
        {
            var ring = new Ellipse
            {
                Width = r * 2,
                Height = r * 2,
                Stroke = new SolidColorBrush(Color.FromArgb(0x40, 0xF8, 0x7B, 0x09)),
                StrokeThickness = 2.0
            };
            Canvas.SetLeft(ring, cx - r);
            Canvas.SetTop(ring, cy - r);
            canvas.Children.Add(ring);
        }

        // Cream pedestal ellipse
        var pedestal = new Ellipse
        {
            Width = width * 0.85,
            Height = height * 0.35,
            Fill = new SolidColorBrush(Color.FromArgb(0x60, 0xF6, 0xE7, 0xD0)),
            Stroke = new SolidColorBrush(Color.FromArgb(0x80, 0x1E, 0x1D, 0x1A)),
            StrokeThickness = 1.5
        };
        Canvas.SetLeft(pedestal, (width - (width * 0.85)) / 2.0);
        Canvas.SetTop(pedestal, height * 0.55);
        canvas.Children.Add(pedestal);

        // Real controller image
        string safe = (model ?? "x20").Replace("_", "-").ToLowerInvariant();
        if (safe.Contains("starfield")) safe = "x15-starfield";
        else if (safe.Contains("wukong")) safe = "x15-wukong";
        else if (safe.Contains("purple") || safe.Contains("d10")) safe = "d10-purple";
        else if (safe.Contains("x05")) safe = "x05pro";
        else if (safe.Contains("x10")) safe = "x10";
        else if (safe.Contains("x15")) safe = "x15-starfield";
        else if (safe.Contains("x20pro") || safe.Contains("x20-pro")) safe = "x20pro";
        else if (safe.Contains("x20")) safe = "x20";
        else if (safe.Contains("dune")) safe = "dune";
        else safe = "x20";

        string path = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "art", $"{safe}.png");
        if (!System.IO.File.Exists(path))
        {
            path = System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Assets", "art", $"{safe}.png");
        }

        ImageSource? src = null;
        if (System.IO.File.Exists(path))
        {
            try
            {
                src = new BitmapImage(new Uri(System.IO.Path.GetFullPath(path)));
            }
            catch { }
        }

        var padImg = new Image
        {
            Width = width * 0.88,
            Height = height * 0.88,
            Source = src,
            Stretch = Stretch.Uniform,
            Effect = new DropShadowEffect
            {
                Color = Color.FromArgb(0x80, 0x1E, 0x1D, 0x1A),
                BlurRadius = 14,
                ShadowDepth = 6,
                Direction = 315,
                Opacity = 0.4
            }
        };
        Canvas.SetLeft(padImg, (width - (width * 0.88)) / 2.0);
        Canvas.SetTop(padImg, (height - (height * 0.88)) / 2.0);
        canvas.Children.Add(padImg);

        return canvas;
    }
}
