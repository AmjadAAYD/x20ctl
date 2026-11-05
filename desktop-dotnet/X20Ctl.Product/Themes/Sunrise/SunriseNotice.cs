using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace X20Ctl.Product.Sunrise;

/// <summary>
/// Sunrise modal notice dialog: cream sticker card on dimmed halftone yellow field,
/// thick black border, bold dark title, and black / cream action pills.
/// </summary>
public sealed class SunriseNotice : Canvas
{
    public SunriseNotice(
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

        // Dimmed backdrop
        Children.Add(SunriseKit.CreateHalftoneBackdrop());
        var dim = new Rectangle
        {
            Width = 1600,
            Height = 900,
            Fill = new SolidColorBrush(Color.FromArgb(0xbb, 0x1E, 0x1D, 0x1A))
        };
        Children.Add(dim);

        // Center card content
        var contentStack = new StackPanel { Width = 640 };

        // Title
        var titleBlock = new TextBlock
        {
            Text = title.ToUpperInvariant(),
            FontFamily = SunriseKit.Display,
            FontWeight = FontWeights.Black,
            FontSize = 26,
            Foreground = SunriseKit.InkBrush,
            Margin = new Thickness(0, 0, 0, 16)
        };
        contentStack.Children.Add(titleBlock);

        // Divider
        contentStack.Children.Add(new Rectangle { Height = 2.5, Fill = SunriseKit.InkBrush, Margin = new Thickness(0, 0, 0, 16) });

        // Description lines
        foreach (var line in lines)
        {
            var lBlock = new TextBlock
            {
                Text = line,
                FontFamily = SunriseKit.Body,
                FontSize = 14,
                Foreground = SunriseKit.InkBrush,
                LineHeight = 22,
                Margin = new Thickness(0, 0, 0, 6)
            };
            contentStack.Children.Add(lBlock);
        }

        // Notes list if any
        if (notes != null)
        {
            var notesPanel = new StackPanel { Margin = new Thickness(0, 14, 0, 0) };
            foreach (var note in notes)
            {
                var row = new Grid { Margin = new Thickness(0, 3, 0, 3) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var l = new TextBlock { Text = note.Label.ToUpperInvariant(), FontFamily = SunriseKit.Display, FontWeight = FontWeights.Bold, FontSize = 11, Foreground = SunriseKit.OrangeBrush };
                var v = new TextBlock { Text = note.Text, FontFamily = SunriseKit.Body, FontSize = 12.5, Foreground = SunriseKit.OliveBrush };
                Grid.SetColumn(l, 0);
                Grid.SetColumn(v, 1);
                row.Children.Add(l);
                row.Children.Add(v);
                notesPanel.Children.Add(row);
            }
            contentStack.Children.Add(notesPanel);
        }

        // Action Buttons Row (Notice buttons with notice:yes / notice:no tags for ThemeReview accessibility)
        var btnRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 24, 0, 0)
        };

        var noBtn = SunriseKit.SunriseButton(noText, onNo, isPrimary: false, width: 140, height: 38, tag: "notice:no");
        noBtn.Margin = new Thickness(0, 0, 12, 0);

        var yesBtn = SunriseKit.SunriseButton(yesText, onYes, isPrimary: true, width: 160, height: 38, tag: "notice:yes");

        btnRow.Children.Add(noBtn);
        btnRow.Children.Add(yesBtn);
        contentStack.Children.Add(btnRow);

        var card = SunriseKit.CreateStickerCard(contentStack, 700, 420, cornerRadius: 22);
        Canvas.SetLeft(card, 450);
        Canvas.SetTop(card, 240);
        Children.Add(card);
    }
}
