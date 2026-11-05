using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace X20Ctl.Product.Sunrise;

/// <summary>
/// Sunrise Tool Frame: Top retro-TV header bar with back pill, sunshine yellow halftone backdrop,
/// and full 1600x900 content area.
/// </summary>
public sealed class SunriseToolFrame : Canvas
{
    public Canvas ContentArea { get; }

    public SunriseToolFrame(string title, string subtitle, Action onBack)
    {
        Width = 1600;
        Height = 900;
        ClipToBounds = true;

        // Background
        Children.Add(SunriseKit.CreateHalftoneBackdrop());

        // Top Header Bar
        var topBar = new Border
        {
            Width = 1600,
            Height = 64,
            Background = SunriseKit.InkBrush,
            Padding = new Thickness(36, 0, 36, 0)
        };
        var topGrid = new Grid();
        topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Brand
        var brandSp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        brandSp.Children.Add(new TextBlock
        {
            Text = "📺 X20CTL",
            FontFamily = SunriseKit.Display,
            FontWeight = FontWeights.Black,
            FontSize = 18,
            Foreground = SunriseKit.YellowBrush,
            VerticalAlignment = VerticalAlignment.Center
        });
        brandSp.Children.Add(new TextBlock
        {
            Text = $"  ·  {title.ToUpperInvariant()}",
            FontFamily = SunriseKit.Display,
            FontWeight = FontWeights.Bold,
            FontSize = 14,
            Foreground = SunriseKit.CreamBrush,
            VerticalAlignment = VerticalAlignment.Center
        });
        Grid.SetColumn(brandSp, 0);
        topGrid.Children.Add(brandSp);

        // Subtitle
        var subText = new TextBlock
        {
            Text = subtitle,
            FontFamily = SunriseKit.Body,
            FontSize = 12,
            Foreground = SunriseKit.OliveBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(subText, 1);
        topGrid.Children.Add(subText);

        // Back button
        var backBtn = SunriseKit.SunriseButton("← Back", onBack, isPrimary: false, width: 110, height: 36);
        backBtn.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(backBtn, 2);
        topGrid.Children.Add(backBtn);

        topBar.Child = topGrid;
        Canvas.SetLeft(topBar, 0);
        Canvas.SetTop(topBar, 0);
        Children.Add(topBar);

        ContentArea = new Canvas
        {
            Width = 1600,
            Height = 836,
            ClipToBounds = true
        };
        Canvas.SetLeft(ContentArea, 0);
        Canvas.SetTop(ContentArea, 64);
        Children.Add(ContentArea);
    }
}
