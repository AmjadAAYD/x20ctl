using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace X20Ctl.Product.Spotlight;

/// <summary>
/// Notice/Dialog presentation for Spotlight matching t_spotlight.py notices():
/// The house lights dim, two cables lower an announcement cue card from the flies
/// into the follow spot, with Roman numeral steps, Perpetua Titling callouts, and theatrical buttons.
/// </summary>
internal static class SpotlightNotice
{
    public static Grid CreateNotice(
        string pill,
        string title,
        string lead,
        string[] numberedSteps,
        string actionNote,
        string callout,
        string noText,
        string yesText,
        Action onNo,
        Action onYes,
        (string Label, string Text)[]? footerStates = null)
    {
        var root = new Grid { Width = 1600, Height = 900, ClipToBounds = true };
        var canvas = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };

        // 1. Dimmed house background
        var bg = new Rectangle
        {
            Width = 1600, Height = 900,
            Fill = new RadialGradientBrush
            {
                Center = new(0.5, 0.45), RadiusX = 0.60, RadiusY = 0.70,
                GradientStops = { new(SpotlightKit.A(SpotlightKit.C("#070405"), 0.40), 0.0), new(SpotlightKit.A(SpotlightKit.C("#070405"), 0.95), 1.0) }
            }
        };
        canvas.Children.Add(bg);

        // 2. Wide soft follow spot from overhead
        var spot = SpotlightKit.FollowSpot(cx: 800, fy: 860, rx: 520, top: -80, opacity: 0.14, pool: false);
        canvas.Children.Add(spot);

        // 3. Two cables lowered from the flies at x = 740 and x = 858
        var cableBrush = new LinearGradientBrush
        {
            StartPoint = new(0, 0), EndPoint = new(0, 1),
            GradientStops = { new(SpotlightKit.C("#5a4a3a"), 0.0), new(SpotlightKit.C("#9b8c7a"), 1.0) }
        };
        var cableL = new Line { X1 = 740, Y1 = 0, X2 = 740, Y2 = 150, Stroke = cableBrush, StrokeThickness = 2 };
        var cableR = new Line { X1 = 858, Y1 = 0, X2 = 858, Y2 = 150, Stroke = cableBrush, StrokeThickness = 2 };
        canvas.Children.Add(cableL);
        canvas.Children.Add(cableR);

        // 4. Main Announcement Cue Card at (260, 150), width 1080
        var cardInner = new StackPanel();

        // Top Header: Logo + Pill + Title
        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var logoImg = SpotlightParts.GetThemeLogo();
        if (logoImg != null)
        {
            var logo = new Image { Source = logoImg, Width = 90, Margin = new Thickness(0, 0, 20, 0), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(logo, 0);
            headerGrid.Children.Add(logo);
        }

        var headerText = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        headerText.Children.Add(new TextBlock
        {
            Text = pill.ToUpperInvariant(),
            FontFamily = SpotlightKit.Display,
            FontSize = 11,
            FontWeight = FontWeights.Light,
            Foreground = SpotlightKit.GoldBrush
        });
        headerText.Children.Add(new TextBlock
        {
            Text = title,
            FontFamily = SpotlightKit.Body,
            FontSize = 28,
            Foreground = SpotlightKit.ChalkBrush,
            Margin = new Thickness(0, 4, 0, 0)
        });
        Grid.SetColumn(headerText, 1);
        headerGrid.Children.Add(headerText);
        cardInner.Children.Add(headerGrid);

        // Gold divider rule
        var rule = new Rectangle
        {
            Height = 1, Margin = new Thickness(0, 20, 0, 20),
            Fill = new LinearGradientBrush
            {
                StartPoint = new(0, 0), EndPoint = new(1, 0),
                GradientStops = { new(SpotlightKit.Gold, 0.0), new(Colors.Transparent, 1.0) }
            }
        };
        cardInner.Children.Add(rule);

        // Content split: Left column (Steps) + Right column (Callout & Actions)
        var columnsGrid = new Grid();
        columnsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(470) });
        columnsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) }); // gap
        columnsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Left column
        var leftCol = new StackPanel();
        if (!string.IsNullOrEmpty(lead))
        {
            leftCol.Children.Add(new TextBlock
            {
                Text = lead,
                FontFamily = SpotlightKit.Body,
                FontSize = 21,
                Foreground = SpotlightKit.ChalkBrush,
                Margin = new Thickness(0, 0, 0, 10)
            });
        }
        for (int i = 0; i < numberedSteps.Length; i++)
        {
            var stepRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
            stepRow.Children.Add(new TextBlock
            {
                Text = $"{SpotlightKit.Roman(i)}.",
                FontFamily = SpotlightKit.Bodoni,
                FontSize = 16,
                Width = 26,
                Foreground = SpotlightKit.GoldBrush
            });
            stepRow.Children.Add(new TextBlock
            {
                Text = numberedSteps[i],
                FontFamily = SpotlightKit.Body,
                FontSize = 16,
                Foreground = SpotlightKit.ChalkDimBrush,
                TextWrapping = TextWrapping.Wrap,
                Width = 430
            });
            leftCol.Children.Add(stepRow);
        }
        Grid.SetColumn(leftCol, 0);
        columnsGrid.Children.Add(leftCol);

        // Right column
        var rightCol = new StackPanel();
        if (!string.IsNullOrEmpty(actionNote))
        {
            rightCol.Children.Add(new TextBlock
            {
                Text = actionNote,
                FontFamily = SpotlightKit.Body,
                FontSize = 16,
                Foreground = SpotlightKit.ChalkDimBrush
            });
        }
        if (!string.IsNullOrEmpty(callout))
        {
            rightCol.Children.Add(new TextBlock
            {
                Text = callout.ToUpperInvariant(),
                FontFamily = SpotlightKit.Display,
                FontSize = 30,
                FontWeight = FontWeights.Light,
                Foreground = SpotlightKit.B("#fff6e6"),
                Margin = new Thickness(0, 16, 0, 0)
            });
        }

        // Action buttons
        var buttonsRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 20, 0, 0) };
        var noBtn = SpotlightKit.TheatricalButton(noText, primary: false, key: "B", onClick: onNo, height: 44, fontSize: 13);
        noBtn.Margin = new Thickness(0, 0, 12, 0);
        noBtn.Tag = "notice:no";
        buttonsRow.Children.Add(noBtn);

        var yesBtn = SpotlightKit.TheatricalButton(yesText, primary: true, key: "A", onClick: onYes, height: 44, fontSize: 13);
        yesBtn.Tag = "notice:yes";
        buttonsRow.Children.Add(yesBtn);
        rightCol.Children.Add(buttonsRow);

        Grid.SetColumn(rightCol, 2);
        columnsGrid.Children.Add(rightCol);
        cardInner.Children.Add(columnsGrid);

        var cueCard = SpotlightKit.CueCard(cardInner, 1080, padding: new Thickness(40, 36, 40, 36));
        Canvas.SetLeft(cueCard, 260);
        Canvas.SetTop(cueCard, 150);
        canvas.Children.Add(cueCard);

        // 5. Status / lifecycle states footer at (260, 690)
        if (footerStates is { Length: > 0 })
        {
            var footerBorder = new Border
            {
                Width = 1080,
                BorderBrush = SpotlightKit.B(SpotlightKit.A(SpotlightKit.Gold, 0.30)),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(0, 16, 0, 0)
            };
            var footerGrid = new Grid();
            foreach (var state in footerStates)
            {
                footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }
            for (int i = 0; i < footerStates.Length; i++)
            {
                var cell = new StackPanel();
                cell.Children.Add(new TextBlock
                {
                    Text = footerStates[i].Label.ToUpperInvariant(),
                    FontFamily = SpotlightKit.Display,
                    FontSize = 10,
                    FontWeight = FontWeights.Light,
                    Foreground = SpotlightKit.MuteBrush
                });
                cell.Children.Add(new TextBlock
                {
                    Text = footerStates[i].Text,
                    FontFamily = SpotlightKit.Body,
                    FontSize = 14.5,
                    Foreground = SpotlightKit.ChalkDimBrush,
                    Margin = new Thickness(0, 4, 0, 0)
                });
                Grid.SetColumn(cell, i);
                footerGrid.Children.Add(cell);
            }
            footerBorder.Child = footerGrid;
            Canvas.SetLeft(footerBorder, 260);
            Canvas.SetTop(footerBorder, 690);
            canvas.Children.Add(footerBorder);
        }

        root.Children.Add(canvas);
        return root;
    }
}
