using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace X20Ctl.Product.Nocturne;

/// <summary>
/// Nocturne modal notice dialogs: white slanted card on a dimmed electric cobalt field,
/// bold italic black title, and electric blue action buttons.
/// </summary>
public sealed class NocturneNotice : Canvas
{
    public NocturneNotice(
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

        // Dimmed city backdrop
        Children.Add(NocturneKit.CreateCityBackdrop());
        var dim = new Rectangle
        {
            Width = 1600,
            Height = 900,
            Fill = new SolidColorBrush(Color.FromArgb(0xcc, 0x01, 0x12, 0x3e))
        };
        Children.Add(dim);

        // Center card content
        var contentStack = new StackPanel { Width = 640 };

        // Title: heavy italic black
        var titleBlock = new TextBlock
        {
            Text = title.ToUpperInvariant(),
            FontFamily = NocturneKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 26,
            Foreground = NocturneKit.InkBrush,
            Margin = new Thickness(0, 4, 0, 8)
        };
        contentStack.Children.Add(titleBlock);

        // Thin navy rule
        var rule = new Rectangle
        {
            Height = 2,
            Fill = new SolidColorBrush(Color.FromArgb(0x40, 0x01, 0x00, 0x42)),
            Margin = new Thickness(0, 0, 0, 16)
        };
        contentStack.Children.Add(rule);

        // Message lines
        var linesStack = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };
        foreach (var line in lines)
        {
            linesStack.Children.Add(new TextBlock
            {
                Text = line,
                FontFamily = NocturneKit.Body,
                FontSize = 14,
                Foreground = NocturneKit.InkBrush,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 2)
            });
        }
        contentStack.Children.Add(linesStack);

        // Optional Key-Value notes
        if (notes != null)
        {
            var noteBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0x18, 0x00, 0x56, 0xfb)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 0x00, 0x56, 0xfb)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(14),
                Margin = new Thickness(0, 0, 0, 20)
            };
            var noteSp = new StackPanel();
            foreach (var (k, v) in notes)
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
                row.Children.Add(new TextBlock
                {
                    Text = k.ToUpperInvariant() + ": ",
                    FontFamily = NocturneKit.Display,
                    FontWeight = FontWeights.Bold,
                    FontSize = 11,
                    Foreground = NocturneKit.CobaltBrush,
                    Width = 100
                });
                row.Children.Add(new TextBlock
                {
                    Text = v,
                    FontFamily = NocturneKit.Body,
                    FontSize = 12,
                    Foreground = NocturneKit.InkBrush
                });
                noteSp.Children.Add(row);
            }
            noteBorder.Child = noteSp;
            contentStack.Children.Add(noteBorder);
        }

        // Action Buttons
        var btnRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 8, 0, 0)
        };

        var noBtn = NocturneKit.NocturneButton(noText, onNo, isPrimary: false, width: 140, height: 38, tag: "notice:no");
        noBtn.Margin = new Thickness(0, 0, 12, 0);

        var yesBtn = NocturneKit.NocturneButton(yesText, onYes, isPrimary: true, width: 180, height: 38, tag: "notice:yes");

        btnRow.Children.Add(noBtn);
        btnRow.Children.Add(yesBtn);
        contentStack.Children.Add(btnRow);

        var card = NocturneKit.NocturneWhiteCard(contentStack, width: 700, height: 420);
        Canvas.SetLeft(card, 450);
        Canvas.SetTop(card, 240);
        Children.Add(card);
    }
}
