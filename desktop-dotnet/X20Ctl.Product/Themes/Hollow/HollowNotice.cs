using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace X20Ctl.Product.Hollow;

/// <summary>
/// Hollow modal notice dialogs: framed cavern parchment card with corner flourishes,
/// diamond divider, serif typography, and yes/no options.
/// </summary>
public sealed class HollowNotice : Canvas
{
    public HollowNotice(
        string title,
        string[] lines,
        string yesText,
        string noText,
        Action onYes,
        Action onNo,
        IEnumerable<(string Label, string Text)>? notes = null)
    {
        Width = 1600;
        Height = 900;
        ClipToBounds = true;

        // Dimmed cavern backdrop
        Children.Add(HollowKit.CreateCavernBackdrop());
        var dim = new Rectangle
        {
            Width = 1600,
            Height = 900,
            Fill = new SolidColorBrush(Color.FromArgb(0xbd, 0x01, 0x05, 0x0a))
        };
        Children.Add(dim);

        // Center card (w=680)
        var contentStack = new StackPanel();

        // Title
        var titleBlock = new TextBlock
        {
            Text = title.ToUpperInvariant(),
            FontFamily = HollowKit.Display,
            FontSize = 20,
            Foreground = HollowKit.IvoryTextBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 4, 0, 10)
        };
        contentStack.Children.Add(titleBlock);

        // Diamond divider
        contentStack.Children.Add(HollowKit.DiamondDivider(600, 14));

        // Message lines
        var linesStack = new StackPanel { Margin = new Thickness(0, 14, 0, 16) };
        foreach (var line in lines)
        {
            linesStack.Children.Add(new TextBlock
            {
                Text = line,
                FontFamily = HollowKit.Body,
                FontSize = 14,
                Foreground = HollowKit.SoftTextBrush,
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 2),
                TextAlignment = TextAlignment.Center
            });
        }
        contentStack.Children.Add(linesStack);

        // Optional notes
        if (notes != null)
        {
            foreach (var (lbl, val) in notes)
            {
                var noteBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(0x80, 0x05, 0x0d, 0x16)),
                    BorderBrush = new SolidColorBrush(HollowKit.A(HollowKit.SilverBorder, 0.4)),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(10, 6, 10, 6),
                    Margin = new Thickness(0, 4, 0, 8),
                    CornerRadius = new CornerRadius(2)
                };
                var sp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
                sp.Children.Add(new TextBlock { Text = lbl + ": ", FontFamily = HollowKit.Display, FontSize = 12, Foreground = HollowKit.MutedTextBrush });
                sp.Children.Add(new TextBlock { Text = val, FontFamily = HollowKit.Body, FontSize = 12, Foreground = HollowKit.IvoryTextBrush });
                noteBorder.Child = sp;
                contentStack.Children.Add(noteBorder);
            }
        }

        // Action Buttons Row
        var btnRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 12, 0, 4)
        };

        var noBtn = HollowKit.HollowButton(noText, onNo, isPrimary: false, width: 140, height: 36, tag: "notice:no");
        noBtn.Margin = new Thickness(0, 0, 16, 0);

        var yesBtn = HollowKit.HollowButton(yesText, onYes, isPrimary: true, width: 150, height: 36, tag: "notice:yes");

        btnRow.Children.Add(noBtn);
        btnRow.Children.Add(yesBtn);
        contentStack.Children.Add(btnRow);

        var card = HollowKit.FiligreeCard(contentStack, padding: 26, background: new SolidColorBrush(HollowKit.Night), borderBrush: HollowKit.IvoryBorderBrush, cornerSize: 18);
        card.Width = 680;
        card.Effect = new DropShadowEffect
        {
            Color = Colors.Black,
            BlurRadius = 32,
            ShadowDepth = 8,
            Opacity = 0.85
        };

        Canvas.SetLeft(card, (1600 - 680) / 2.0);
        Canvas.SetTop(card, 240);
        Children.Add(card);
    }
}
