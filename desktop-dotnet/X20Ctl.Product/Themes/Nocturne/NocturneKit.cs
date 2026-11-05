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

namespace X20Ctl.Product.Nocturne;

/// <summary>
/// Design system primitives, colors, typography, and controls for the Nocturne theme:
/// "Electric blue field, hard diagonal shards, huge italic type, crisp white cards. Confident, graphic, fast."
/// Strictly implements 04_NOCTURNE_SPEC.txt.
/// </summary>
public static class NocturneKit
{
    // Palette
    public static readonly Color Void = Color.FromRgb(0x01, 0x12, 0x3E);          // #01123E Navy ground
    public static readonly Color Deep = Color.FromRgb(0x01, 0x15, 0x4A);          // #01154A Deep blue ground
    public static readonly Color Night = Color.FromRgb(0x01, 0x1D, 0x69);         // #011D69 Dark blue surface
    public static readonly Color Cobalt = Color.FromRgb(0x00, 0x56, 0xFB);        // #0056FB Electric / Cobalt field
    public static readonly Color ActiveBlue = Color.FromRgb(0x1B, 0x23, 0xFA);    // #1B23FA Selected tab vivid blue
    public static readonly Color LightBlue = Color.FromRgb(0x5C, 0x80, 0xC8);     // #5C80C8 Light blue accent
    public static readonly Color White = Color.FromRgb(0xF2, 0xF8, 0xFE);         // #F2F8FE Crisp white card surface
    public static readonly Color CoolGrey = Color.FromRgb(0xCC, 0xD3, 0xE7);      // #CCD3E7 Cool grey text/accents
    public static readonly Color Ink = Color.FromRgb(0x01, 0x00, 0x42);           // #010042 Navy-black ink on white
    public static readonly Color Danger = Color.FromRgb(0xD1, 0x00, 0x2F);        // #D1002F Distinct crimson for danger

    // Brushes
    public static readonly SolidColorBrush VoidBrush = new(Void);
    public static readonly SolidColorBrush DeepBrush = new(Deep);
    public static readonly SolidColorBrush NightBrush = new(Night);
    public static readonly SolidColorBrush CobaltBrush = new(Cobalt);
    public static readonly SolidColorBrush ActiveBlueBrush = new(ActiveBlue);
    public static readonly SolidColorBrush LightBlueBrush = new(LightBlue);
    public static readonly SolidColorBrush WhiteBrush = new(White);
    public static readonly SolidColorBrush CoolGreyBrush = new(CoolGrey);
    public static readonly SolidColorBrush InkBrush = new(Ink);
    public static readonly SolidColorBrush DangerBrush = new(Danger);
    public static readonly SolidColorBrush AccentBrush = CobaltBrush;
    public static readonly SolidColorBrush RuleBrush = LightBlueBrush;
    public static readonly SolidColorBrush SoftBrush = CoolGreyBrush;
    public static readonly SolidColorBrush VoidTextBrush = InkBrush;

    // Fonts
    public static readonly FontFamily Display = new("Bahnschrift, Impact, Segoe UI Black, Arial Black, sans-serif");
    public static readonly FontFamily Body = new("Segoe UI Variable Text, Segoe UI, sans-serif");
    public static readonly FontFamily Icons = new("Segoe Fluent Icons, Segoe UI Symbol, sans-serif");

    // Slant
    public static Transform CreateSlant(double angle = -12) => new SkewTransform(angle, 0);

    public static Color A(Color c, double alpha) => Color.FromArgb((byte)Math.Clamp((int)(alpha * 255), 0, 255), c.R, c.G, c.B);

    /// <summary>
    /// Background: Electric blue field with faint city silhouette bottom-left and hard diagonal shards.
    /// Vector Path geometries only.
    /// </summary>
    public static Canvas CreateCityBackdrop(double width = 1600, double height = 900)
    {
        var canvas = new Canvas { Width = width, Height = height, ClipToBounds = true };

        // Base Navy Gradient
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
                    new(Void, 0.0),
                    new(Deep, 0.4),
                    new(Night, 1.0)
                }
            }
        };
        canvas.Children.Add(baseRect);

        // Electric Cobalt Diagonal Field behind controller
        var cobaltField = new Path
        {
            Data = Geometry.Parse("M 380,0 L 1180,0 L 980,900 L 180,900 Z"),
            Fill = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1),
                GradientStops =
                {
                    new(Color.FromArgb(0xdd, 0x00, 0x56, 0xfb), 0.0),
                    new(Color.FromArgb(0xbb, 0x01, 0x1d, 0x69), 1.0)
                }
            }
        };
        canvas.Children.Add(cobaltField);

        // White Shard 1 (Top Center to Right)
        var whiteShard1 = new Path
        {
            Data = Geometry.Parse("M 860,0 L 980,0 L 890,400 L 770,400 Z"),
            Fill = new SolidColorBrush(Color.FromArgb(0x40, 0xf2, 0xf8, 0xfe))
        };
        canvas.Children.Add(whiteShard1);

        // White Shard 2 (Right edge streak)
        var whiteShard2 = new Path
        {
            Data = Geometry.Parse("M 1340,0 L 1520,0 L 1380,900 L 1200,900 Z"),
            Fill = new SolidColorBrush(Color.FromArgb(0x25, 0xf2, 0xf8, 0xfe))
        };
        canvas.Children.Add(whiteShard2);

        // City Skyline Silhouette on bottom-left (Vector)
        var skylineGeom = new PathGeometry();
        var skylineFig = new PathFigure { StartPoint = new Point(0, height) };
        skylineFig.Segments.Add(new LineSegment(new Point(0, 720), true));
        skylineFig.Segments.Add(new LineSegment(new Point(40, 720), true));
        skylineFig.Segments.Add(new LineSegment(new Point(40, 680), true));
        skylineFig.Segments.Add(new LineSegment(new Point(70, 680), true));
        skylineFig.Segments.Add(new LineSegment(new Point(70, 740), true));
        skylineFig.Segments.Add(new LineSegment(new Point(110, 740), true));
        skylineFig.Segments.Add(new LineSegment(new Point(110, 640), true));
        skylineFig.Segments.Add(new LineSegment(new Point(135, 620), true)); // Spire
        skylineFig.Segments.Add(new LineSegment(new Point(160, 640), true));
        skylineFig.Segments.Add(new LineSegment(new Point(160, 750), true));
        skylineFig.Segments.Add(new LineSegment(new Point(210, 750), true));
        skylineFig.Segments.Add(new LineSegment(new Point(210, 700), true));
        skylineFig.Segments.Add(new LineSegment(new Point(260, 700), true));
        skylineFig.Segments.Add(new LineSegment(new Point(260, 770), true));
        skylineFig.Segments.Add(new LineSegment(new Point(340, 770), true));
        skylineFig.Segments.Add(new LineSegment(new Point(340, height), true));
        skylineGeom.Figures.Add(skylineFig);

        var skylinePath = new Path
        {
            Data = skylineGeom,
            Fill = new SolidColorBrush(Color.FromArgb(0x90, 0x01, 0x0a, 0x24))
        };
        canvas.Children.Add(skylinePath);

        return canvas;
    }

    /// <summary>
    /// Right Action Card: Large white slanted panel holding forms and settings.
    /// Left edge angled by ~12 degrees, heavy black italic title, thin navy rules.
    /// </summary>
    public static Border NocturneWhiteCard(UIElement content, double width = 560, double height = 690)
    {
        // Safe inset accounting for -12 deg slant so top-left text (e.g. FACE / A) is never cut off
        double slantPad = Math.Max(34.0, (height * Math.Tan(12.0 * Math.PI / 180.0) * 0.5) + 16.0);
        var card = new Border
        {
            Width = width,
            Height = height,
            Background = WhiteBrush,
            Padding = new Thickness(slantPad, 20, 20, 20),
            ClipToBounds = false,
            Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(0x01, 0x08, 0x20),
                BlurRadius = 0,
                ShadowDepth = 12,
                Direction = 315,
                Opacity = 0.8
            }
        };

        // Skew container
        card.RenderTransform = CreateSlant(-12);
        card.RenderTransformOrigin = new Point(0.5, 0.5);

        // Counter-skew inner content so text and buttons remain upright and readable
        var innerHost = new Grid
        {
            RenderTransform = CreateSlant(12),
            RenderTransformOrigin = new Point(0.5, 0.5),
            ClipToBounds = false
        };
        innerHost.Children.Add(content);
        card.Child = innerHost;

        return card;
    }

    /// <summary>
    /// Slanted Action Button: Vivid blue block with play triangle icon and white bold italic text.
    /// </summary>
    public static Button NocturneButton(string label, Action onClick, bool isPrimary = true, bool isDanger = false, double? width = null, double height = 40, string? tag = null)
    {
        var btn = new Button
        {
            Height = height,
            Cursor = Cursors.Hand,
            Tag = tag,
            FocusVisualStyle = null
        };
        if (width.HasValue) btn.Width = width.Value;

        var border = new Border
        {
            Height = height,
            Background = isDanger ? DangerBrush : (isPrimary ? ActiveBlueBrush : new SolidColorBrush(Color.FromArgb(0xcc, 0x01, 0x1d, 0x69))),
            BorderBrush = isDanger ? new SolidColorBrush(Color.FromRgb(0xff, 0x60, 0x70)) : (isPrimary ? LightBlueBrush : new SolidColorBrush(Color.FromArgb(0x80, 0x5c, 0x80, 0xc8))),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(18, 0, 18, 0),
            RenderTransform = CreateSlant(-12),
            RenderTransformOrigin = new Point(0.5, 0.5),
            Effect = isPrimary ? new DropShadowEffect
            {
                Color = isDanger ? Danger : Cobalt,
                BlurRadius = 0,
                ShadowDepth = 4,
                Direction = 315,
                Opacity = 0.9
            } : null
        };

        var sp = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            RenderTransform = CreateSlant(12),
            RenderTransformOrigin = new Point(0.5, 0.5)
        };

        // Play triangle icon
        var tri = new Path
        {
            Data = Geometry.Parse("M 0,0 L 8,5 L 0,10 Z"),
            Fill = WhiteBrush,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        sp.Children.Add(tri);

        var tb = new TextBlock
        {
            Text = label.ToUpperInvariant(),
            FontFamily = Display,
            FontWeight = FontWeights.Bold,
            FontStyle = FontStyles.Italic,
            FontSize = 13,
            Foreground = WhiteBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
        sp.Children.Add(tb);
        border.Child = sp;

        btn.Template = new ControlTemplate(typeof(Button))
        {
            VisualTree = new FrameworkElementFactory(typeof(ContentPresenter))
        };
        btn.Content = border;

        btn.Click += (_, e) =>
        {
            e.Handled = true;
            onClick();
        };

        // Hover animation
        btn.MouseEnter += (_, _) =>
        {
            border.Background = isDanger ? new SolidColorBrush(Color.FromRgb(0xe8, 0x10, 0x40)) : (isPrimary ? CobaltBrush : new SolidColorBrush(Color.FromArgb(0xee, 0x05, 0x25, 0x80)));
        };
        btn.MouseLeave += (_, _) =>
        {
            border.Background = isDanger ? DangerBrush : (isPrimary ? ActiveBlueBrush : new SolidColorBrush(Color.FromArgb(0xcc, 0x01, 0x1d, 0x69)));
        };

        return btn;
    }

    /// <summary>
    /// Skewed Chip for Sub-tabs and Toggles
    /// </summary>
    public static Border NocturneChip(string label, bool isSelected, Action? onClick = null, string? tag = null)
    {
        var chip = new Border
        {
            Padding = new Thickness(14, 6, 14, 6),
            Margin = new Thickness(0, 0, 8, 0),
            Background = isSelected ? WhiteBrush : new SolidColorBrush(Color.FromArgb(0xcc, 0x01, 0x1d, 0x69)),
            BorderBrush = isSelected ? WhiteBrush : new SolidColorBrush(Color.FromArgb(0x60, 0x5c, 0x80, 0xc8)),
            BorderThickness = new Thickness(1),
            Cursor = onClick != null ? Cursors.Hand : Cursors.Arrow,
            Tag = tag,
            RenderTransform = CreateSlant(-12),
            RenderTransformOrigin = new Point(0.5, 0.5)
        };

        var tb = new TextBlock
        {
            Text = label.ToUpperInvariant(),
            FontFamily = Display,
            FontWeight = FontWeights.Bold,
            FontStyle = FontStyles.Italic,
            FontSize = 11.5,
            Foreground = isSelected ? InkBrush : WhiteBrush,
            RenderTransform = CreateSlant(12),
            RenderTransformOrigin = new Point(0.5, 0.5)
        };
        chip.Child = tb;

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
    /// Page Title: Huge two-line italic ALL-CAPS letters with subtle dark navy drop shadow.
    /// </summary>
    public static StackPanel NocturneTitle(string line1, string line2, string subtitle)
    {
        var sp = new StackPanel();

        var titleBlock = new StackPanel
        {
            RenderTransform = CreateSlant(-12),
            RenderTransformOrigin = new Point(0, 0.5)
        };

        var shadow = new DropShadowEffect
        {
            Color = Ink,
            BlurRadius = 0,
            ShadowDepth = 5,
            Direction = 315,
            Opacity = 0.95
        };

        var t1 = new TextBlock
        {
            Text = line1.ToUpperInvariant(),
            FontFamily = Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 54,
            Foreground = WhiteBrush,
            LineHeight = 50,
            LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
            Effect = shadow
        };
        titleBlock.Children.Add(t1);

        if (!string.IsNullOrEmpty(line2))
        {
            var t2 = new TextBlock
            {
                Text = line2.ToUpperInvariant(),
                FontFamily = Display,
                FontWeight = FontWeights.Black,
                FontStyle = FontStyles.Italic,
                FontSize = 54,
                Foreground = WhiteBrush,
                LineHeight = 50,
                LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
                Effect = shadow
            };
            titleBlock.Children.Add(t2);
        }
        sp.Children.Add(titleBlock);

        if (!string.IsNullOrEmpty(subtitle))
        {
            var sub = new TextBlock
            {
                Text = subtitle,
                FontFamily = Body,
                FontStyle = FontStyles.Italic,
                FontSize = 14,
                Foreground = WhiteBrush,
                Margin = new Thickness(4, 10, 0, 0)
            };
            sp.Children.Add(sub);
        }

        return sp;
    }

    /// <summary>
    /// Left Navigation Strip item with Arrow-Pointer tab for selected items.
    /// </summary>
    public static Border NocturneNavItem(string label, string glyph, bool isSelected, Action onClick, string? tag = null)
    {
        var border = new Border
        {
            Height = 44,
            Width = 240,
            Background = isSelected ? WhiteBrush : Brushes.Transparent,
            Cursor = Cursors.Hand,
            Tag = tag,
            RenderTransform = CreateSlant(-12),
            RenderTransformOrigin = new Point(0.5, 0.5),
            Margin = new Thickness(0, 2, 0, 2)
        };

        var grid = new Grid
        {
            RenderTransform = CreateSlant(12),
            RenderTransformOrigin = new Point(0.5, 0.5),
            Margin = new Thickness(14, 0, 0, 0)
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var iconBlock = new TextBlock
        {
            Text = glyph,
            FontFamily = Icons,
            FontSize = 14,
            Foreground = isSelected ? InkBrush : WhiteBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(iconBlock, 0);
        grid.Children.Add(iconBlock);

        var textBlock = new TextBlock
        {
            Text = label.ToUpperInvariant(),
            FontFamily = Display,
            FontWeight = FontWeights.Bold,
            FontStyle = FontStyles.Italic,
            FontSize = 13.5,
            Foreground = isSelected ? InkBrush : WhiteBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(textBlock, 1);
        grid.Children.Add(textBlock);

        if (isSelected)
        {
            // Right-pointing solid blue triangle arrow tab
            var arrow = new Path
            {
                Data = Geometry.Parse("M 0,0 L 10,8 L 0,16 Z"),
                Fill = CobaltBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            Grid.SetColumn(arrow, 2);
            grid.Children.Add(arrow);
        }

        border.Child = grid;
        border.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            onClick();
        };

        return border;
    }

    /// <summary>
    /// Controller visualizer for Nocturne
    /// </summary>
    public static FrameworkElement ControllerArt(string modelId = "x20", double width = 600)
    {
        string safe = (modelId ?? "x20").Replace("_", "-").ToLowerInvariant();
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

        return new Image
        {
            Width = width,
            Source = src,
            Stretch = Stretch.Uniform,
            Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(0x00, 0x56, 0xfb),
                BlurRadius = 24,
                ShadowDepth = 8,
                Opacity = 0.75
            }
        };
    }
}
