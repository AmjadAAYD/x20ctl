using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace X20Ctl.Product.Tide;

/// <summary>
/// Standard frame for Tide tool pages (implementing t_tide.py Tide.frame):
/// - Sea background with giant vertical word
/// - White status box at top
/// - Lead sticker box at (214, 104) rotated -1.5deg
/// - Standard Left list area (230, 176, w=640)
/// - Standard Right visual area (940, 176, w=600)
/// - Bottom right hints bar
/// </summary>
public sealed class TideToolFrame : Canvas
{
    private readonly Canvas contentCanvas;

    public Canvas ContentArea => contentCanvas;

    public TideToolFrame(
        string title,
        string lead,
        Action onBack,
        IEnumerable<string> hints = null,
        int seed = 1)
    {
        Width = 1600;
        Height = 900;
        ClipToBounds = true;

        string word = title.ToUpperInvariant()
            .Replace("ADVANCED STICK TOOLS", "STICK TOOLS")
            .Replace("CONTROLLER AS MOUSE", "MOUSE");

        // 1. Sea Background
        Children.Add(TideKit.SeaBackground(seed, word));

        // 2. Status Box
        var status = TideKit.StatusBox(isBack: true, onBack: onBack);
        Children.Add(status);

        // 3. Lead Sticker Box at (214, 104) rotated -1.5deg
        var leadBox = new Border
        {
            MaxWidth = 1000,
            Padding = new Thickness(16, 8, 16, 8),
            Background = TideKit.WhiteBrush,
            BorderBrush = TideKit.InkBrush,
            BorderThickness = new Thickness(3),
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new RotateTransform(-1.5),
            Effect = new DropShadowEffect
            {
                BlurRadius = 0,
                ShadowDepth = 6,
                Direction = 315,
                Opacity = 0.6,
                Color = TideKit.Sea0
            }
        };
        leadBox.Child = new TextBlock
        {
            Text = lead,
            FontFamily = TideKit.Body,
            FontWeight = FontWeights.Bold,
            FontSize = 15,
            Foreground = TideKit.InkBrush,
            TextWrapping = TextWrapping.Wrap
        };
        Canvas.SetLeft(leadBox, 214);
        Canvas.SetTop(leadBox, 104);
        Children.Add(leadBox);

        // 4. Content Area
        contentCanvas = new Canvas { Width = 1600, Height = 900 };
        Children.Add(contentCanvas);

        // 5. Hints at bottom right
        var hintsList = hints ?? new[] { "\u24B7 Back", "\u24B6 Select" };
        var hintsBar = TideKit.HintsBar(hintsList);
        Canvas.SetRight(hintsBar, 40);
        Canvas.SetBottom(hintsBar, 26);
        Children.Add(hintsBar);
    }

    public void SetLeftList(FrameworkElement list, double top = 176)
    {
        Canvas.SetLeft(list, 230);
        Canvas.SetTop(list, top);
        contentCanvas.Children.Add(list);
    }

    public void SetSideStack(FrameworkElement side, double top = 182)
    {
        Canvas.SetLeft(side, 940);
        Canvas.SetTop(side, top);
        contentCanvas.Children.Add(side);
    }

    public void SetNote(string text)
    {
        var note = TideKit.NoteSticker(text, -2);
        Canvas.SetLeft(note, 230);
        Canvas.SetTop(note, 720);
        contentCanvas.Children.Add(note);
    }
}
