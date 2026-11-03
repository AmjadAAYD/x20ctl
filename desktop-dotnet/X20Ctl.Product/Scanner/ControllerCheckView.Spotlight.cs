using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Spotlight;

namespace X20Ctl.Product.Scanner;

public sealed partial class ControllerCheckView
{
    public Action<string>? SpotlightNavigate { get; set; }

    private void BuildSpotlightReadWarning(Panel cards, FrameworkElement systemRow, FrameworkElement choices, FrameworkElement linux)
    {
        var contentCanvas = new Canvas { Width = 1600, Height = 900 };

        // System selector at (302, 206)
        var sysStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        sysStack.Children.Add(new TextBlock
        {
            Text = "SYSTEM",
            FontFamily = SpotlightKit.Display,
            FontSize = 11,
            FontWeight = FontWeights.Light,
            Foreground = SpotlightKit.GoldBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 16, 0)
        });
        sysStack.Children.Add(SpotlightKit.Chip("Windows", true));
        sysStack.Children.Add(new Border { Width = 8 });
        sysStack.Children.Add(SpotlightKit.Chip("Linux", false));
        sysStack.Children.Add(new Border { Width = 8 });
        sysStack.Children.Add(SpotlightKit.Chip("Android", false));
        Canvas.SetLeft(sysStack, 302);
        Canvas.SetTop(sysStack, 206);
        contentCanvas.Children.Add(sysStack);

        // 5 Scene cards across at (300 + i * 252, 270, w = 236, h = 84)
        string[] scenes = { "Before you scan", "Plug in or connect", "Physical test", "Packet analysis", "Summary report" };
        for (int i = 0; i < scenes.Length; i++)
        {
            double sx = 300 + i * 252;
            bool on = (i == 0);

            var sBorder = new Border
            {
                Width = 236, Height = 84,
                Background = on
                    ? new RadialGradientBrush
                    {
                        Center = new(0.3, 0.4), RadiusX = 0.8, RadiusY = 1.2,
                        GradientStops = { new(SpotlightKit.A(SpotlightKit.C("#ffdcaf"), 0.22), 0.0), new(Colors.Transparent, 1.0) }
                    }
                    : Brushes.Transparent,
                BorderBrush = on ? SpotlightKit.GoldBrush : SpotlightKit.B(SpotlightKit.A(SpotlightKit.Gold, 0.18)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(18, 14, 18, 14)
            };

            var sStack = new StackPanel();
            sStack.Children.Add(new TextBlock
            {
                Text = $"Scene {SpotlightKit.Roman(i)}",
                FontFamily = SpotlightKit.Bodoni,
                FontSize = 15,
                Foreground = on ? SpotlightKit.GoldHiBrush : SpotlightKit.B("#7a6c5a")
            });
            sStack.Children.Add(new TextBlock
            {
                Text = scenes[i],
                FontFamily = SpotlightKit.Body,
                FontSize = 18,
                Foreground = on ? SpotlightKit.B("#fff6e6") : SpotlightKit.B("#b3a48e"),
                Margin = new Thickness(0, 4, 0, 0)
            });
            sBorder.Child = sStack;
            Canvas.SetLeft(sBorder, sx);
            Canvas.SetTop(sBorder, 270);
            contentCanvas.Children.Add(sBorder);
        }

        // Cue card below at (292, 390, w = 1260)
        var cardInner = new StackPanel();

        var colsGrid = new Grid();
        colsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(500) });
        colsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
        colsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var firstScene = new StackPanel();
        firstScene.Children.Add(new TextBlock { Text = "Scene I", FontFamily = SpotlightKit.Display, FontSize = 11, FontWeight = FontWeights.Light, Foreground = SpotlightKit.GoldBrush });
        firstScene.Children.Add(new TextBlock { Text = "Before you scan", FontFamily = SpotlightKit.Body, FontSize = 27, Foreground = SpotlightKit.ChalkBrush, Margin = new Thickness(0, 6, 0, 0) });
        firstScene.Children.Add(new TextBlock
        {
            Text = "Close Steam, DS4Windows, and browser controller tabs so only X20CTL reads the device without interference.",
            FontFamily = SpotlightKit.Body,
            FontSize = 17,
            Foreground = SpotlightKit.ChalkDimBrush,
            Margin = new Thickness(0, 8, 0, 0),
            TextWrapping = TextWrapping.Wrap
        });
        var revTag = SpotlightKit.Tag("Read only · Reversible", false);
        revTag.HorizontalAlignment = HorizontalAlignment.Left;
        revTag.Margin = new Thickness(0, 14, 0, 0);
        firstScene.Children.Add(revTag);
        Grid.SetColumn(firstScene, 0);
        colsGrid.Children.Add(firstScene);

        var restStack = new StackPanel { Margin = new Thickness(0, -6, 0, 0) };
        for (int i = 1; i < scenes.Length; i++)
        {
            var row = new Border
            {
                Height = 38,
                BorderBrush = SpotlightKit.B(SpotlightKit.A(SpotlightKit.Gold, 0.12)),
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
            var rg = new Grid();
            rg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var left = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            left.Children.Add(new TextBlock { Text = $"{i + 1}", FontFamily = SpotlightKit.Bodoni, FontSize = 15, Width = 22, Foreground = SpotlightKit.GoldBrush });
            left.Children.Add(new TextBlock { Text = scenes[i], FontFamily = SpotlightKit.Body, FontSize = 16, Foreground = SpotlightKit.ChalkDimBrush });
            Grid.SetColumn(left, 0);
            rg.Children.Add(left);

            var chev = new TextBlock { Text = "\uE76C", FontFamily = SpotlightKit.Icons, FontSize = 10, Foreground = SpotlightKit.MuteBrush, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(chev, 1);
            rg.Children.Add(chev);

            row.Child = rg;
            restStack.Children.Add(row);
        }
        Grid.SetColumn(restStack, 2);
        colsGrid.Children.Add(restStack);
        cardInner.Children.Add(colsGrid);

        cardInner.Children.Add(new TextBlock
        {
            Text = "Diagnostic reports can be reviewed locally or exported to a text file before saving.",
            FontFamily = SpotlightKit.Body,
            FontStyle = FontStyles.Italic,
            FontSize = 13,
            Foreground = SpotlightKit.MuteBrush,
            Margin = new Thickness(0, 14, 0, 0)
        });

        var cueCard = SpotlightKit.CueCard(cardInner, 1260, padding: new Thickness(30, 26, 30, 26));
        Canvas.SetLeft(cueCard, 292);
        Canvas.SetTop(cueCard, 390);
        contentCanvas.Children.Add(cueCard);

        var board = SpotlightToolFrame.WrapWithProgramme(
            currentKey: "tools",
            title: "Read scan",
            lead: "Capture and verify raw controller packet telemetry across standard input endpoints.",
            backLabel: "Back to Tools",
            onBack: () => close(),
            onNavigate: target => SpotlightNavigate?.Invoke(target),
            bodyStage: contentCanvas,
            hints: new[] { ("A", "Select"), ("B", "Back") },
            leadW: 1240);

        warning.Background = SpotlightKit.HouseBrush;
        warning.Child = board;
    }

    private void BuildSpotlightReadFlow(Grid originalPage)
    {
        originalPage.Background = SpotlightKit.HouseBrush;
    }
}
