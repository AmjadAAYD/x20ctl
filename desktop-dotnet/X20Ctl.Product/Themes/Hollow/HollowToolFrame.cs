using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace X20Ctl.Product.Hollow;

/// <summary>
/// Framed layout for Hollow tool and setting pages:
/// - Cavern mist backdrop
/// - Top filigree header with Back button
/// - Central content stage
/// - Bottom info dock
/// </summary>
public sealed class HollowToolFrame : Canvas
{
    private readonly Canvas contentCanvas;

    public Canvas ContentArea => contentCanvas;

    public HollowToolFrame(string title, string lead, Action onBack)
    {
        Width = 1600;
        Height = 900;
        ClipToBounds = true;

        // 1. Cavern Backdrop
        Children.Add(HollowKit.CreateCavernBackdrop());

        // 2. Top Bar
        var topBar = new Border
        {
            Width = 1600,
            Height = 70,
            Background = new SolidColorBrush(Color.FromArgb(0xd0, 0x01, 0x05, 0x0a)),
            BorderBrush = new SolidColorBrush(HollowKit.A(HollowKit.SilverBorder, 0.3)),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(28, 0, 28, 0)
        };

        var topGrid = new Grid();
        topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var backBtn = HollowKit.HollowButton("← BACK", onBack, isPrimary: false, width: 100, height: 32, tag: "theme:tool:back");
        Grid.SetColumn(backBtn, 0);

        var titleSp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(20, 0, 0, 0) };
        titleSp.Children.Add(new TextBlock
        {
            Text = title.ToUpperInvariant(),
            FontFamily = HollowKit.Display,
            FontSize = 17,
            Foreground = HollowKit.IvoryTextBrush,
            VerticalAlignment = VerticalAlignment.Center,
        });
        titleSp.Children.Add(new TextBlock
        {
            Text = "  ·  " + lead,
            FontFamily = HollowKit.Body,
            FontSize = 13,
            Foreground = HollowKit.MutedTextBrush,
            VerticalAlignment = VerticalAlignment.Center
        });
        Grid.SetColumn(titleSp, 1);

        topGrid.Children.Add(backBtn);
        topGrid.Children.Add(titleSp);
        topBar.Child = topGrid;

        Canvas.SetLeft(topBar, 0);
        Canvas.SetTop(topBar, 0);
        Children.Add(topBar);

        // 3. Content Area
        contentCanvas = new Canvas
        {
            Width = 1600,
            Height = 794
        };
        Canvas.SetLeft(contentCanvas, 0);
        Canvas.SetTop(contentCanvas, 70);
        Children.Add(contentCanvas);

        // 4. Bottom Dock
        var dock = HollowKit.HollowDock(title);
        Canvas.SetLeft(dock, 0);
        Canvas.SetTop(dock, 864);
        Children.Add(dock);
    }
}
