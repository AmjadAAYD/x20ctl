using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Media.Animation;

namespace X20Ctl.Product.Hollow;

/// <summary>
/// Design system primitives, colors, typography, and controls for the Hollow theme:
/// "Gothic ink-blue cavern, ivory filigree, serif small caps, delicate diamond dividers,
/// quiet restraint and hand-engraved silver borders."
/// Strictly implements 03_HOLLOW_SPEC.txt.
/// </summary>
public static class HollowKit
{
    // Palette (Sampled & Harmonized from refs/hollow.png)
    public static readonly Color Void = Color.FromRgb(0x01, 0x05, 0x0A);          // #01050A Deepest cavern void
    public static readonly Color Deep = Color.FromRgb(0x04, 0x0D, 0x15);          // #040D15 Deep slate ground
    public static readonly Color Night = Color.FromRgb(0x0B, 0x14, 0x1F);         // #0B141F Panel background
    public static readonly Color Panel = Color.FromRgb(0x0B, 0x14, 0x1F);         // #0B141F Framed card surface
    public static readonly Color Raised = Color.FromRgb(0x1E, 0x2B, 0x38);        // #1E2B38 Selected row / raised surface
    public static readonly Color RaisedHi = Color.FromRgb(0x27, 0x34, 0x44);      // #273444 Lifted hover surface
    public static readonly Color CavernMid = Color.FromRgb(0x19, 0x24, 0x31);     // #192431 Mid cavern shade
    public static readonly Color Rule = Color.FromRgb(0x38, 0x41, 0x4E);          // #38414E Subtle dark rule
    public static readonly Color SilverBorder = Color.FromRgb(0x64, 0x6E, 0x7B);  // #646E7B Medium silver border
    public static readonly Color IvoryBorder = Color.FromRgb(0xD0, 0xCE, 0xC5);   // #D0CEC5 Bright ivory edge
    public static readonly Color IvoryText = Color.FromRgb(0xE6, 0xE3, 0xDA);     // #E6E3DA Primary text (pale ivory)
    public static readonly Color SoftText = Color.FromRgb(0xB6, 0xBC, 0xC6);      // #B6BCC6 Secondary text (soft silver)
    public static readonly Color MutedText = Color.FromRgb(0x8D, 0x97, 0xA5);     // #8D97A5 Muted labels & hints
    public static readonly Color Accent = Color.FromRgb(0xB8, 0xCC, 0xE0);        // #B8CCE0 Cold pale blue-white highlight
    public static readonly Color AccentHi = Color.FromRgb(0xE6, 0xEE, 0xF5);      // #E6EEF5 High bright ivory glow
    public static readonly Color DangerBg = Color.FromRgb(0x3A, 0x12, 0x18);      // #3A1218 Deep restrained crimson
    public static readonly Color DangerBorder = Color.FromRgb(0x7A, 0x28, 0x32);  // #7A2832 Danger silver-red

    // Brushes
    public static readonly SolidColorBrush VoidBrush = new(Void);
    public static readonly SolidColorBrush DeepBrush = new(Deep);
    public static readonly SolidColorBrush NightBrush = new(Night);
    public static readonly SolidColorBrush PanelBrush = new(Panel);
    public static readonly SolidColorBrush RaisedBrush = new(Raised);
    public static readonly SolidColorBrush RaisedHiBrush = new(RaisedHi);
    public static readonly SolidColorBrush CavernMidBrush = new(CavernMid);
    public static readonly SolidColorBrush RuleBrush = new(Rule);
    public static readonly SolidColorBrush SilverBorderBrush = new(SilverBorder);
    public static readonly SolidColorBrush IvoryBorderBrush = new(IvoryBorder);
    public static readonly SolidColorBrush IvoryTextBrush = new(IvoryText);
    public static readonly SolidColorBrush SoftTextBrush = new(SoftText);
    public static readonly SolidColorBrush MutedTextBrush = new(MutedText);
    public static readonly SolidColorBrush AccentBrush = new(Accent);
    public static readonly SolidColorBrush AccentHiBrush = new(AccentHi);
    public static readonly SolidColorBrush DangerBgBrush = new(DangerBg);
    public static readonly SolidColorBrush DangerBorderBrush = new(DangerBorder);

    // Fonts
    public static readonly FontFamily Display = new("Georgia, Palatino Linotype, Cambria, Times New Roman");
    public static readonly FontFamily Body = new("Georgia, Palatino Linotype, Segoe UI");

    public static Color A(Color c, double a) => Color.FromArgb((byte)(255 * Math.Clamp(a, 0, 1)), c.R, c.G, c.B);

    // Signature Geometries

    /// <summary>Curled Gothic corner filigree scroll (16x16 standard).</summary>
    public static PathGeometry CreateFiligreeCorner(double size = 16)
    {
        // Smooth ornamental corner scroll
        var geometry = new PathGeometry();
        var figure = new PathFigure { StartPoint = new Point(0, size), IsClosed = false };
        figure.Segments.Add(new ArcSegment(new Point(size, 0), new Size(size, size), 0, false, SweepDirection.Clockwise, true));
        figure.Segments.Add(new LineSegment(new Point(size - 3, 0), true));
        figure.Segments.Add(new ArcSegment(new Point(0, size - 3), new Size(size - 3, size - 3), 0, false, SweepDirection.Counterclockwise, true));
        geometry.Figures.Add(figure);

        // Small inner curled tendril
        var tendril = new PathFigure { StartPoint = new Point(size * 0.45, size * 0.45), IsClosed = true };
        tendril.Segments.Add(new LineSegment(new Point(size * 0.55, size * 0.35), true));
        tendril.Segments.Add(new LineSegment(new Point(size * 0.35, size * 0.55), true));
        geometry.Figures.Add(tendril);

        geometry.Freeze();
        return geometry;
    }

    /// <summary>4-point diamond ornament (width x height).</summary>
    public static PathGeometry CreateDiamond(double size = 8)
    {
        var geom = new PathGeometry();
        double r = size / 2.0;
        var fig = new PathFigure { StartPoint = new Point(0, -r), IsClosed = true };
        fig.Segments.Add(new LineSegment(new Point(r * 0.65, 0), true));
        fig.Segments.Add(new LineSegment(new Point(0, r), true));
        fig.Segments.Add(new LineSegment(new Point(-r * 0.65, 0), true));
        geom.Figures.Add(fig);
        geom.Freeze();
        return geom;
    }

    /// <summary>Delicate silver-ivory divider with a central 4-point diamond.</summary>
    public static FrameworkElement DiamondDivider(double width = double.NaN, double height = 16)
    {
        var canvas = new Canvas
        {
            Height = height,
            Width = double.IsNaN(width) ? double.NaN : width,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ClipToBounds = true
        };

        var leftLine = new Line
        {
            Stroke = new SolidColorBrush(A(SilverBorder, 0.4)),
            StrokeThickness = 1,
            Y1 = height / 2,
            Y2 = height / 2
        };

        var rightLine = new Line
        {
            Stroke = new SolidColorBrush(A(SilverBorder, 0.4)),
            StrokeThickness = 1,
            Y1 = height / 2,
            Y2 = height / 2
        };

        var diamond = new Path
        {
            Data = CreateDiamond(7),
            Fill = IvoryTextBrush,
            Stroke = IvoryBorderBrush,
            StrokeThickness = 0.5
        };

        canvas.SizeChanged += (s, e) =>
        {
            double midX = e.NewSize.Width / 2.0;
            double midY = e.NewSize.Height / 2.0;
            Canvas.SetLeft(diamond, midX);
            Canvas.SetTop(diamond, midY);

            leftLine.X1 = 0;
            leftLine.X2 = Math.Max(0, midX - 8);
            rightLine.X1 = midX + 8;
            rightLine.X2 = e.NewSize.Width;
        };

        canvas.Children.Add(leftLine);
        canvas.Children.Add(rightLine);
        canvas.Children.Add(diamond);
        return canvas;
    }

    /// <summary>Framed panel with 4 ornate filigree corner flourishes.</summary>
    public static Border FiligreeCard(UIElement content, double padding = 16, Brush? background = null, Brush? borderBrush = null, double cornerSize = 14)
    {
        var border = new Border
        {
            Background = background ?? new SolidColorBrush(Night),
            BorderBrush = borderBrush ?? new SolidColorBrush(A(IvoryBorder, 0.45)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(2),
            Padding = new Thickness(padding),
            SnapsToDevicePixels = true
        };

        var grid = new Grid();
        grid.Children.Add(content);

        // Top-Left Filigree
        var tl = new Path
        {
            Data = CreateFiligreeCorner(cornerSize),
            Stroke = borderBrush ?? new SolidColorBrush(A(IvoryBorder, 0.7)),
            StrokeThickness = 1,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(-padding + 2, -padding + 2, 0, 0),
            IsHitTestVisible = false
        };

        // Top-Right Filigree
        var tr = new Path
        {
            Data = CreateFiligreeCorner(cornerSize),
            Stroke = borderBrush ?? new SolidColorBrush(A(IvoryBorder, 0.7)),
            StrokeThickness = 1,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new ScaleTransform(-1, 1),
            Margin = new Thickness(0, -padding + 2, -padding + 2, 0),
            IsHitTestVisible = false
        };

        // Bottom-Left Filigree
        var bl = new Path
        {
            Data = CreateFiligreeCorner(cornerSize),
            Stroke = borderBrush ?? new SolidColorBrush(A(IvoryBorder, 0.7)),
            StrokeThickness = 1,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Bottom,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new ScaleTransform(1, -1),
            Margin = new Thickness(-padding + 2, 0, 0, -padding + 2),
            IsHitTestVisible = false
        };

        // Bottom-Right Filigree
        var br = new Path
        {
            Data = CreateFiligreeCorner(cornerSize),
            Stroke = borderBrush ?? new SolidColorBrush(A(IvoryBorder, 0.7)),
            StrokeThickness = 1,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new ScaleTransform(-1, -1),
            Margin = new Thickness(0, 0, -padding + 2, -padding + 2),
            IsHitTestVisible = false
        };

        grid.Children.Add(tl);
        grid.Children.Add(tr);
        grid.Children.Add(bl);
        grid.Children.Add(br);

        border.Child = grid;
        return border;
    }

    /// <summary>Standard Hollow secondary framed button with thin silver/ivory border and no overlapping ornaments.</summary>
    public static Button HollowButton(string text, Action onClick, bool isPrimary = false, double width = double.NaN, double height = 36, string? tag = null)
    {
        if (isPrimary) return HollowPrimaryApplyButton(text, onClick, width, height, tag);

        var btn = new Button
        {
            Height = height,
            Cursor = System.Windows.Input.Cursors.Hand,
            Tag = tag,
            FocusVisualStyle = null
        };
        if (!double.IsNaN(width)) btn.Width = width;

        var mainBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xbb, 0x09, 0x14, 0x20)),
            BorderBrush = new SolidColorBrush(A(SilverBorder, 0.65)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(2),
            Padding = new Thickness(12, 0, 12, 0),
            SnapsToDevicePixels = true
        };

        var tb = new TextBlock
        {
            Text = text,
            FontFamily = Display,
            FontSize = 12.5,
            FontWeight = FontWeights.Normal,
            Foreground = SoftTextBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        mainBorder.Child = tb;
        btn.Content = mainBorder;

        // Interaction States
        btn.MouseEnter += (_, _) =>
        {
            mainBorder.Background = new SolidColorBrush(Color.FromArgb(0xdd, 0x16, 0x24, 0x34));
            mainBorder.BorderBrush = IvoryTextBrush;
            tb.Foreground = IvoryTextBrush;
        };

        btn.MouseLeave += (_, _) =>
        {
            mainBorder.Background = new SolidColorBrush(Color.FromArgb(0xbb, 0x09, 0x14, 0x20));
            mainBorder.BorderBrush = new SolidColorBrush(A(SilverBorder, 0.65));
            tb.Foreground = SoftTextBrush;
        };

        btn.Click += (_, e) =>
        {
            e.Handled = true;
            onClick();
        };

        return btn;
    }

    /// <summary>Primary Hollow button: wide dark framed button with thin ivory border, flanked by 4-point diamond ornaments matching refs/hollow.png.</summary>
    public static Button HollowPrimaryApplyButton(string text, Action onClick, double width = double.NaN, double height = 44, string? tag = null)
    {
        var btn = new Button
        {
            Height = height,
            Cursor = System.Windows.Input.Cursors.Hand,
            Tag = tag,
            FocusVisualStyle = null,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        if (!double.IsNaN(width)) btn.Width = width;

        var grid = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Center };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });

        var leftDiamond = new Path
        {
            Data = CreateDiamond(11),
            Fill = IvoryTextBrush,
            Stroke = IvoryBorderBrush,
            StrokeThickness = 0.5,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(leftDiamond, 0);

        var box = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xee, 0x14, 0x24, 0x36)),
            BorderBrush = IvoryBorderBrush,
            BorderThickness = new Thickness(1.2),
            CornerRadius = new CornerRadius(2),
            Padding = new Thickness(16, 0, 16, 0),
            Height = height,
            SnapsToDevicePixels = true,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        var tb = new TextBlock
        {
            Text = text,
            FontFamily = Display,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = IvoryTextBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        box.Child = tb;
        Grid.SetColumn(box, 1);

        var rightDiamond = new Path
        {
            Data = CreateDiamond(11),
            Fill = IvoryTextBrush,
            Stroke = IvoryBorderBrush,
            StrokeThickness = 0.5,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(rightDiamond, 2);

        grid.Children.Add(leftDiamond);
        grid.Children.Add(box);
        grid.Children.Add(rightDiamond);

        btn.Content = grid;

        btn.MouseEnter += (_, _) =>
        {
            box.Background = new SolidColorBrush(Color.FromArgb(0xff, 0x1c, 0x30, 0x46));
            box.BorderBrush = AccentHiBrush;
            tb.Foreground = AccentHiBrush;
            leftDiamond.Fill = AccentHiBrush;
            rightDiamond.Fill = AccentHiBrush;
        };

        btn.MouseLeave += (_, _) =>
        {
            box.Background = new SolidColorBrush(Color.FromArgb(0xee, 0x14, 0x24, 0x36));
            box.BorderBrush = IvoryBorderBrush;
            tb.Foreground = IvoryTextBrush;
            leftDiamond.Fill = IvoryTextBrush;
            rightDiamond.Fill = IvoryTextBrush;
        };

        btn.Click += (_, e) =>
        {
            e.Handled = true;
            onClick();
        };

        return btn;
    }

    /// <summary>Hollow navigation item with small diamond indicators when selected.</summary>
    public static Border HollowNavRow(string text, string tag, bool isSelected, Action onSelect, string? icon = null)
    {
        var border = new Border
        {
            Height = 44,
            Background = isSelected ? new SolidColorBrush(Raised) : Brushes.Transparent,
            BorderBrush = isSelected ? new SolidColorBrush(A(IvoryBorder, 0.8)) : Brushes.Transparent,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Cursor = System.Windows.Input.Cursors.Hand,
            Tag = tag,
            Padding = new Thickness(10, 0, 10, 0),
            Margin = new Thickness(0, 2, 0, 2)
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) }); // Left diamond
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });    // Icon
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Text
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) }); // Right diamond

        var leftDiamond = new Path
        {
            Data = CreateDiamond(6),
            Fill = isSelected ? AccentBrush : Brushes.Transparent,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(leftDiamond, 0);

        var rightDiamond = new Path
        {
            Data = CreateDiamond(6),
            Fill = isSelected ? AccentBrush : Brushes.Transparent,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(rightDiamond, 3);

        int textCol = 2;
        if (!string.IsNullOrEmpty(icon))
        {
            var iconBlock = new TextBlock
            {
                Text = icon,
                FontFamily = new FontFamily("Segoe Fluent Icons"),
                FontSize = 13,
                Foreground = isSelected ? IvoryTextBrush : MutedTextBrush,
                Margin = new Thickness(4, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(iconBlock, 1);
            grid.Children.Add(iconBlock);
        }

        var label = new TextBlock
        {
            Text = text,
            FontFamily = Display,
            FontSize = 13,
            Foreground = isSelected ? IvoryTextBrush : SoftTextBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 0, 0)
        };
        Grid.SetColumn(label, textCol);

        grid.Children.Add(leftDiamond);
        grid.Children.Add(label);
        grid.Children.Add(rightDiamond);

        border.Child = grid;

        border.MouseEnter += (_, _) =>
        {
            if (!isSelected)
            {
                border.Background = new SolidColorBrush(A(Raised, 0.45));
                label.Foreground = IvoryTextBrush;
                leftDiamond.Fill = new SolidColorBrush(A(Accent, 0.5));
                rightDiamond.Fill = new SolidColorBrush(A(Accent, 0.5));
            }
        };

        border.MouseLeave += (_, _) =>
        {
            if (!isSelected)
            {
                border.Background = Brushes.Transparent;
                label.Foreground = SoftTextBrush;
                leftDiamond.Fill = Brushes.Transparent;
                rightDiamond.Fill = Brushes.Transparent;
            }
        };

        border.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            onSelect();
        };

        return border;
    }

    /// <summary>Sub-tabs row (Face, Shoulders, Sticks, System, D-Pad, Paddles) with hairline and center diamond.</summary>
    public static FrameworkElement HollowSubTabs(string[] tabs, int selectedIndex, Action<int> onSelect)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        for (int i = 0; i < tabs.Length; i++)
        {
            int idx = i;
            bool isSel = (i == selectedIndex);

            var tabBorder = new Border
            {
                Padding = new Thickness(14, 8, 14, 8),
                Cursor = System.Windows.Input.Cursors.Hand,
                Background = Brushes.Transparent,
                Tag = $"theme:subtab:{tabs[i].ToLowerInvariant()}"
            };

            var sp = new StackPanel { Orientation = Orientation.Vertical };
            var text = new TextBlock
            {
                Text = tabs[i],
                FontFamily = Display,
                FontSize = 13,
                Foreground = isSel ? IvoryTextBrush : MutedTextBrush,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            sp.Children.Add(text);

            if (isSel)
            {
                var indicator = new Canvas { Height = 4, Margin = new Thickness(0, 4, 0, 0) };
                var line = new Line
                {
                    X1 = 0,
                    X2 = 40,
                    Y1 = 2,
                    Y2 = 2,
                    Stroke = IvoryBorderBrush,
                    StrokeThickness = 1
                };
                var d = new Path
                {
                    Data = CreateDiamond(5),
                    Fill = IvoryTextBrush,
                    Stroke = IvoryBorderBrush,
                    StrokeThickness = 0.5
                };
                Canvas.SetLeft(d, 20);
                Canvas.SetTop(d, 2);
                indicator.Children.Add(line);
                indicator.Children.Add(d);
                sp.Children.Add(indicator);
            }

            tabBorder.Child = sp;
            tabBorder.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                onSelect(idx);
            };

            panel.Children.Add(tabBorder);
        }
        return panel;
    }

    /// <summary>Hollow title header with small-caps Georgia font and subtle subtitle.</summary>
    public static FrameworkElement HollowHeader(string title, string? subtitle = null)
    {
        var sp = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };
        var titleBlock = new TextBlock
        {
            Text = title.ToUpperInvariant(),
            FontFamily = Display,
            FontSize = 26,
            FontWeight = FontWeights.Normal,
            Foreground = IvoryTextBrush,
        };
        sp.Children.Add(titleBlock);

        if (!string.IsNullOrEmpty(subtitle))
        {
            var subBlock = new TextBlock
            {
                Text = subtitle,
                FontFamily = Body,
                FontSize = 13.5,
                Foreground = MutedTextBrush,
                Margin = new Thickness(0, 4, 0, 0)
            };
            sp.Children.Add(subBlock);
        }

        return sp;
    }

    /// <summary>Delicate Hollow toggle switch with diamond thumb.</summary>
    public static FrameworkElement HollowToggle(bool isChecked, Action<bool> onToggle, string? tag = null)
    {
        var track = new Border
        {
            Width = 44,
            Height = 22,
            CornerRadius = new CornerRadius(11),
            Background = isChecked ? new SolidColorBrush(Raised) : new SolidColorBrush(Color.FromArgb(0x80, 0x07, 0x10, 0x18)),
            BorderBrush = isChecked ? IvoryBorderBrush : SilverBorderBrush,
            BorderThickness = new Thickness(1),
            Cursor = System.Windows.Input.Cursors.Hand,
            Tag = tag
        };

        var thumbCanvas = new Canvas { Width = 44, Height = 22, IsHitTestVisible = false };
        var diamondThumb = new Path
        {
            Data = CreateDiamond(12),
            Fill = isChecked ? IvoryTextBrush : SoftTextBrush,
            Stroke = IvoryBorderBrush,
            StrokeThickness = 0.5
        };

        double leftPos = isChecked ? 32 : 12;
        Canvas.SetLeft(diamondThumb, leftPos);
        Canvas.SetTop(diamondThumb, 11);
        thumbCanvas.Children.Add(diamondThumb);

        track.Child = thumbCanvas;

        bool current = isChecked;
        track.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            current = !current;
            track.Background = current ? new SolidColorBrush(Raised) : new SolidColorBrush(Color.FromArgb(0x80, 0x07, 0x10, 0x18));
            track.BorderBrush = current ? IvoryBorderBrush : SilverBorderBrush;
            diamondThumb.Fill = current ? IvoryTextBrush : SoftTextBrush;
            Canvas.SetLeft(diamondThumb, current ? 32 : 12);
            onToggle(current);
        };

        return track;
    }

    /// <summary>Rich, visible multi-layered cavern art: stalactites, gothic arches, stone columns, delicate mist gradients.</summary>
    public static FrameworkElement CreateCavernBackdrop()
    {
        var grid = new Grid { IsHitTestVisible = false, ClipToBounds = true };

        // 1. Base void-to-deep cavern gradient
        var baseRect = new Rectangle
        {
            Fill = new LinearGradientBrush(
                new GradientStopCollection
                {
                    new GradientStop(Color.FromRgb(0x01, 0x05, 0x0C), 0.0),
                    new GradientStop(Color.FromRgb(0x04, 0x0E, 0x19), 0.45),
                    new GradientStop(Color.FromRgb(0x08, 0x15, 0x24), 0.75),
                    new GradientStop(Color.FromRgb(0x02, 0x08, 0x12), 1.0)
                },
                new Point(0.5, 0),
                new Point(0.5, 1))
        };
        grid.Children.Add(baseRect);

        // 2. Distant gothic arches & columns in cavern depth (subtle dark silhouette with 45% opacity)
        var backgroundVaults = new Path
        {
            Fill = new SolidColorBrush(Color.FromArgb(0x75, 0x08, 0x16, 0x26)),
            Data = Geometry.Parse("M 0,900 L 0,420 C 60,380 120,380 180,450 C 240,400 320,400 400,480 L 400,900 M 1180,900 L 1180,360 C 1260,260 1360,260 1440,380 L 1440,900 M 1400,900 L 1400,240 C 1480,140 1560,140 1600,220 L 1600,900 Z")
        };
        grid.Children.Add(backgroundVaults);

        // 3. Prominent layered stalactite formations dropping from the cavern ceiling
        // Deep layer stalactites
        var deepStalactites = new Path
        {
            Fill = new LinearGradientBrush(
                new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb(0xa0, 0x06, 0x10, 0x1c), 0.0),
                    new GradientStop(Color.FromArgb(0x40, 0x0a, 0x18, 0x28), 0.8),
                    new GradientStop(Colors.Transparent, 1.0)
                },
                new Point(0.5, 0),
                new Point(0.5, 1)),
            Data = Geometry.Parse("M 0,0 L 1600,0 L 1600,80 L 1540,165 L 1490,75 L 1420,140 L 1360,65 L 1280,185 L 1210,80 L 1120,150 L 1050,55 L 970,170 L 900,70 L 810,195 L 740,65 L 650,160 L 580,60 L 490,175 L 420,70 L 330,150 L 260,50 L 170,135 L 90,60 L 0,85 Z")
        };
        grid.Children.Add(deepStalactites);

        // Sharp foreground stalactites with higher contrast
        var foreStalactites = new Path
        {
            Fill = new LinearGradientBrush(
                new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb(0xcc, 0x0d, 0x1c, 0x2e), 0.0),
                    new GradientStop(Color.FromArgb(0x60, 0x14, 0x28, 0x3e), 0.7),
                    new GradientStop(Colors.Transparent, 1.0)
                },
                new Point(0.5, 0),
                new Point(0.5, 1)),
            Data = Geometry.Parse("M 0,0 L 1600,0 L 1600,40 L 1560,95 L 1510,35 L 1450,115 L 1390,45 L 1310,130 L 1250,50 L 1160,110 L 1090,35 L 1000,120 L 930,45 L 840,140 L 780,50 L 690,125 L 620,40 L 530,135 L 460,45 L 370,110 L 300,35 L 210,105 L 140,40 L 50,90 L 0,45 Z")
        };
        grid.Children.Add(foreStalactites);

        // 4. Grand cavern arch & column on the right
        var rightArch = new Path
        {
            Fill = new LinearGradientBrush(
                new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb(0x95, 0x08, 0x16, 0x26), 0.0),
                    new GradientStop(Color.FromArgb(0x50, 0x10, 0x22, 0x36), 0.6),
                    new GradientStop(Color.FromArgb(0x20, 0x14, 0x28, 0x40), 1.0)
                },
                new Point(1, 0.5),
                new Point(0, 0.5)),
            Data = Geometry.Parse("M 1600,900 L 1320,900 C 1320,600 1390,260 1600,100 Z")
        };
        grid.Children.Add(rightArch);

        // Left cavern flank
        var leftFlank = new Path
        {
            Fill = new LinearGradientBrush(
                new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb(0x80, 0x06, 0x12, 0x20), 0.0),
                    new GradientStop(Color.FromArgb(0x35, 0x0a, 0x1a, 0x2c), 1.0)
                },
                new Point(0, 0.5),
                new Point(1, 0.5)),
            Data = Geometry.Parse("M 0,900 L 220,900 C 220,680 160,340 0,200 Z")
        };
        grid.Children.Add(leftFlank);

        // 5. Floating atmospheric mist bands
        var mist1 = new Ellipse
        {
            Width = 1100,
            Height = 420,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.22,
            Fill = new RadialGradientBrush(Color.FromRgb(0x40, 0x70, 0xA5), Colors.Transparent)
        };
        grid.Children.Add(mist1);

        var floorMist = new Rectangle
        {
            Height = 220,
            VerticalAlignment = VerticalAlignment.Bottom,
            Opacity = 0.18,
            Fill = new LinearGradientBrush(Colors.Transparent, Color.FromRgb(0x35, 0x60, 0x8C), 90)
        };
        grid.Children.Add(floorMist);

        // 6. Perimeter vignette
        var vignette = new Rectangle
        {
            Fill = new RadialGradientBrush(Colors.Transparent, Color.FromArgb(0xb0, 0x01, 0x03, 0x07))
            {
                RadiusX = 0.85,
                RadiusY = 0.85
            }
        };
        grid.Children.Add(vignette);

        return grid;
    }

    /// <summary>Themed Hollow dock / hint bar with keycaps.</summary>
    public static FrameworkElement HollowDock(string activePage)
    {
        var dock = new Border
        {
            Height = 36,
            Background = new SolidColorBrush(Color.FromArgb(0xbb, 0x02, 0x06, 0x0c)),
            BorderBrush = new SolidColorBrush(A(SilverBorder, 0.3)),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(24, 0, 24, 0)
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Left Status Info
        var leftSp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var info1 = new TextBlock { Text = "CONNECTION: ", FontFamily = Display, FontSize = 11, Foreground = MutedTextBrush };
        var val1 = new TextBlock { Text = "Not detected  ·  ", FontFamily = Display, FontSize = 11, Foreground = SoftTextBrush };
        var info2 = new TextBlock { Text = "BATTERY: ", FontFamily = Display, FontSize = 11, Foreground = MutedTextBrush };
        var val2 = new TextBlock { Text = "Not reported  ·  ", FontFamily = Display, FontSize = 11, Foreground = SoftTextBrush };
        var info3 = new TextBlock { Text = "FIRMWARE: ", FontFamily = Display, FontSize = 11, Foreground = MutedTextBrush };
        var val3 = new TextBlock { Text = "Not reported", FontFamily = Display, FontSize = 11, Foreground = SoftTextBrush };

        leftSp.Children.Add(info1);
        leftSp.Children.Add(val1);
        leftSp.Children.Add(info2);
        leftSp.Children.Add(val2);
        leftSp.Children.Add(info3);
        leftSp.Children.Add(val3);
        Grid.SetColumn(leftSp, 0);

        // Right Keycaps
        var rightSp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        rightSp.Children.Add(Keycap("Enter", "Select"));
        rightSp.Children.Add(Keycap("Esc", "Back"));
        Grid.SetColumn(rightSp, 1);

        grid.Children.Add(leftSp);
        grid.Children.Add(rightSp);
        dock.Child = grid;
        return dock;
    }

    private static FrameworkElement Keycap(string key, string action)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        var capBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xdd, 0x0d, 0x17, 0x24)),
            BorderBrush = new SolidColorBrush(A(SilverBorder, 0.6)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(2),
            Padding = new Thickness(6, 1, 6, 1),
            Margin = new Thickness(0, 0, 4, 0)
        };
        capBorder.Child = new TextBlock { Text = key, FontFamily = Display, FontSize = 10.5, Foreground = IvoryTextBrush };

        var actBlock = new TextBlock { Text = action, FontFamily = Display, FontSize = 11, Foreground = MutedTextBrush, VerticalAlignment = VerticalAlignment.Center };
        sp.Children.Add(capBorder);
        sp.Children.Add(actBlock);
        return sp;
    }

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
            Stretch = Stretch.Uniform
        };
    }
}
