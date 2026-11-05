using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace X20Ctl.Product.Nocturne;

/// <summary>
/// Framed layout for Nocturne tool and setting pages:
/// - City silhouette and electric blue backdrop
/// - Top slanted header with Back button
/// - Central content stage
/// </summary>
public sealed class NocturneToolFrame : Canvas
{
    private readonly Canvas contentCanvas;

    public Canvas ContentArea => contentCanvas;

    public NocturneToolFrame(string title, string lead, Action onBack)
    {
        Width = 1600;
        Height = 900;
        ClipToBounds = true;

        // 1. City & Shards Backdrop
        Children.Add(NocturneKit.CreateCityBackdrop());

        // 2. Top Bar (Dark navy strip with skewed blue accent)
        var topBar = new Border
        {
            Width = 1600,
            Height = 70,
            Background = new SolidColorBrush(Color.FromArgb(0xee, 0x01, 0x12, 0x3e)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x60, 0x00, 0x56, 0xfb)),
            BorderThickness = new Thickness(0, 0, 0, 2),
            Padding = new Thickness(32, 0, 32, 0)
        };

        var topGrid = new Grid();
        topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var backBtn = NocturneKit.NocturneButton("← BACK", onBack, isPrimary: false, width: 110, height: 34, tag: "theme:tool:back");
        Grid.SetColumn(backBtn, 0);

        var titleSp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(24, 0, 0, 0) };
        titleSp.Children.Add(new TextBlock
        {
            Text = title.ToUpperInvariant(),
            FontFamily = NocturneKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 20,
            Foreground = NocturneKit.WhiteBrush,
            VerticalAlignment = VerticalAlignment.Center
        });

        if (!string.IsNullOrEmpty(lead))
        {
            titleSp.Children.Add(new TextBlock
            {
                Text = "  //  " + lead,
                FontFamily = NocturneKit.Body,
                FontStyle = FontStyles.Italic,
                FontSize = 13,
                Foreground = NocturneKit.CoolGreyBrush,
                VerticalAlignment = VerticalAlignment.Center
            });
        }
        Grid.SetColumn(titleSp, 1);

        topGrid.Children.Add(backBtn);
        topGrid.Children.Add(titleSp);
        topBar.Child = topGrid;

        Canvas.SetLeft(topBar, 0);
        Canvas.SetTop(topBar, 0);
        Children.Add(topBar);

        // 3. Central Stage
        contentCanvas = new Canvas
        {
            Width = 1600,
            Height = 830,
            ClipToBounds = true
        };
        Canvas.SetLeft(contentCanvas, 0);
        Canvas.SetTop(contentCanvas, 70);
        Children.Add(contentCanvas);
    }
}
