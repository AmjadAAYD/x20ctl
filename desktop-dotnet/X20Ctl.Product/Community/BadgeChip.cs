using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Zone.Community;

namespace X20Ctl.Product.Community;

public static class BadgeChip
{
    public static readonly Color OwnerOrange = Color.FromRgb(0xFF, 0x7A, 0x18);

    public static FrameworkElement CreateOwnerCheckmark(double size = 15)
    {
        var check = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M 4,8.5 L 6.8,11.2 L 12.2,5.2"),
            Stroke = Brushes.White,
            StrokeThickness = 1.9,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            Width = size,
            Height = size,
            Stretch = Stretch.Uniform
        };
        return new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(4),
            Background = new SolidColorBrush(OwnerOrange),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xB3, 0x6B)),
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(5, 0, 0, 0),
            ToolTip = "AmjadXCTL - Verified X20CTL Creator & Owner",
            Child = check
        };
    }

    public static Border Create(BadgeDef def, bool compact = false)
    {
        var bg = ParseColor(def.BgHex, Color.FromRgb(0x1B, 0x22, 0x34));
        var border = ParseColor(def.BorderHex, Color.FromRgb(0x60, 0xA5, 0xFA));
        var text = ParseColor(def.TextHex, Color.FromRgb(0xDB, 0xEA, 0xFE));

        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var marker = new Rectangle
        {
            Width = compact ? 3 : 4,
            Height = compact ? 10 : 12,
            RadiusX = 1,
            RadiusY = 1,
            Fill = new SolidColorBrush(border),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 5, 0)
        };
        row.Children.Add(marker);
        row.Children.Add(new TextBlock
        {
            Text = def.Name,
            FontSize = compact ? 10.5 : 11.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(text),
            VerticalAlignment = VerticalAlignment.Center
        });

        return new Border
        {
            CornerRadius = new CornerRadius(4),
            Background = new SolidColorBrush(bg),
            BorderBrush = new SolidColorBrush(border),
            BorderThickness = new Thickness(1),
            Padding = compact ? new Thickness(6, 2, 7, 2) : new Thickness(8, 3, 9, 3),
            Margin = new Thickness(0, 0, 5, 4),
            ToolTip = $"{def.Name} ({def.Rarity})\n{def.Description}",
            Child = row
        };
    }

    private static Color ParseColor(string? hex, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(hex)) return fallback;
        try
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }
        catch
        {
            return fallback;
        }
    }
}
