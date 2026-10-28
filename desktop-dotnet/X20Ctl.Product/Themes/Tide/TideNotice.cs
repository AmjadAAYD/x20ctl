using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace X20Ctl.Product.Tide;

/// <summary>
/// Tide modal dialogs and stacked notice cards (strictly matching t_tide.py notices()).
/// </summary>
public sealed class TideNotice : Canvas
{
    public TideNotice(
        string title,
        string[] lines,
        string yesText,
        string noText,
        Action onYes,
        Action onNo,
        IEnumerable<(string Label, string Text)> notes = null)
    {
        Width = 1600;
        Height = 900;
        ClipToBounds = true;

        // 1. Dimmed Backdrop: Sea background with dark gradient overlay
        Children.Add(TideKit.SeaBackground(29, "NOTICE"));
        var dim = new Rectangle
        {
            Width = 1600,
            Height = 900,
            Fill = new LinearGradientBrush
            {
                StartPoint = new Point(0.5, 1),
                EndPoint = new Point(0.5, 0),
                GradientStops =
                {
                    new(Color.FromArgb((byte)(255 * 1.0), 8, 20, 110), 0.09),
                    new(Color.FromArgb((byte)(255 * 0.86), 8, 20, 110), 0.20),
                    new(Color.FromArgb((byte)(255 * 0.60), 8, 20, 110), 1.0)
                }
            }
        };
        Children.Add(dim);

        // 2. Main Persona Dialog Card at (130, 130, w=760) rotated -2deg
        var card = new Border
        {
            Width = 760,
            Padding = new Thickness(28, 22, 28, 26),
            Background = TideKit.WhiteBrush,
            BorderBrush = TideKit.InkBrush,
            BorderThickness = new Thickness(4),
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new RotateTransform(-2),
            Effect = new DropShadowEffect
            {
                BlurRadius = 0,
                ShadowDepth = 10,
                Direction = 315,
                Opacity = 0.6,
                Color = TideKit.Sea0
            }
        };

        var cardStack = new StackPanel();
        cardStack.Children.Add(new TextBlock
        {
            Text = title.ToUpperInvariant(),
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 28,
            Foreground = TideKit.Sea0Brush
        });

        var bodyStack = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
        if (lines != null && lines.Length > 0)
        {
            bodyStack.Children.Add(new TextBlock
            {
                Text = lines[0],
                FontFamily = TideKit.Body,
                FontWeight = FontWeights.Bold,
                FontSize = 16,
                Foreground = TideKit.InkBrush,
                TextWrapping = TextWrapping.Wrap
            });

            for (int i = 1; i < lines.Length && i < 3; i++)
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
                row.Children.Add(new TextBlock
                {
                    Text = "▸ ",
                    FontFamily = TideKit.Display,
                    FontWeight = FontWeights.Black,
                    FontSize = 16,
                    Foreground = TideKit.SpikeRedBrush
                });
                row.Children.Add(new TextBlock
                {
                    Text = lines[i],
                    FontFamily = TideKit.Body,
                    FontWeight = FontWeights.Bold,
                    FontSize = 16,
                    Foreground = TideKit.InkBrush,
                    TextWrapping = TextWrapping.Wrap
                });
                bodyStack.Children.Add(row);
            }

            if (lines.Length > 3)
            {
                bodyStack.Children.Add(new TextBlock
                {
                    Text = lines[3],
                    FontFamily = TideKit.Body,
                    FontWeight = FontWeights.Bold,
                    FontSize = 16,
                    Foreground = TideKit.SubBrush,
                    Margin = new Thickness(0, 8, 0, 0),
                    TextWrapping = TextWrapping.Wrap
                });
            }

            if (lines.Length > 4)
            {
                bodyStack.Children.Add(new TextBlock
                {
                    Text = lines[4].ToUpperInvariant(),
                    FontFamily = TideKit.Display,
                    FontWeight = FontWeights.Black,
                    FontStyle = FontStyles.Italic,
                    FontSize = 22,
                    Foreground = TideKit.SpikeRedBrush,
                    Margin = new Thickness(0, 12, 0, 0)
                });
            }
        }
        cardStack.Children.Add(bodyStack);
        card.Child = cardStack;
        Canvas.SetLeft(card, 130);
        Canvas.SetTop(card, 130);
        Children.Add(card);

        // 3. Action Words at (190, 730): Yes (focused) & No
        var wordYes = TideKit.SlantedWord(yesText.ToUpperInvariant(), focused: true, fs: 72, onClick: onYes);
        Canvas.SetLeft(wordYes, 190);
        Canvas.SetTop(wordYes, 650);
        Children.Add(wordYes);

        var wordNo = TideKit.SlantedWord(noText.ToUpperInvariant(), focused: false, fs: 72, onClick: onNo);
        Canvas.SetLeft(wordNo, 390);
        Canvas.SetTop(wordNo, 650);
        Children.Add(wordNo);

        // 4. Right side notes at (990, 120, w=560)
        var notesContainer = new StackPanel { Width = 560 };
        var defaultNotes = notes ?? new[]
        {
            ("UPDATE", "X20CTL 1.2 · 22 MB download"),
            ("WHILE IT INSTALLS", "Downloads and verifies locally. Your controller mappings and curves stay as they are."),
            ("AFTER YES", "The installer runs and X20CTL restarts."),
            ("AFTER NO", "Nothing changes. You can update later from Tools.")
        };

        int noteIndex = 0;
        foreach (var (lbl, txt) in defaultNotes)
        {
            double rot = -3.0 + noteIndex * 1.5;
            var noteBorder = new Border
            {
                Padding = new Thickness(16, 12, 16, 12),
                Margin = new Thickness(0, 0, 0, 14),
                Background = TideKit.WhiteBrush,
                BorderBrush = TideKit.InkBrush,
                BorderThickness = new Thickness(3),
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new RotateTransform(rot),
                Effect = new DropShadowEffect { BlurRadius = 0, ShadowDepth = 6, Direction = 315, Opacity = 0.6, Color = TideKit.Sea0 }
            };
            var noteStack = new StackPanel();
            noteStack.Children.Add(new TextBlock
            {
                Text = lbl,
                FontFamily = TideKit.Display,
                FontWeight = FontWeights.Black,
                FontSize = 14,
                Foreground = TideKit.SpikeRedBrush
            });
            noteStack.Children.Add(new TextBlock
            {
                Text = txt,
                FontFamily = TideKit.Body,
                FontWeight = FontWeights.Bold,
                FontSize = 15,
                Foreground = TideKit.InkBrush,
                Margin = new Thickness(0, 4, 0, 0),
                TextWrapping = TextWrapping.Wrap
            });
            noteBorder.Child = noteStack;
            notesContainer.Children.Add(noteBorder);
            noteIndex++;
        }
        Canvas.SetLeft(notesContainer, 990);
        Canvas.SetTop(notesContainer, 120);
        Children.Add(notesContainer);

        // 5. Hints at bottom right
        var hintsBar = TideKit.HintsBar(new[] { $"\u24B7 {noText}", $"\u24B6 {yesText}" });
        Canvas.SetRight(hintsBar, 40);
        Canvas.SetBottom(hintsBar, 26);
        Children.Add(hintsBar);
    }
}
