using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace X20Ctl.Product.Spotlight;

/// <summary>
/// Implements t_spotlight.py frame(cur, title, lead, hint, lead_w, floor, size):
/// Theatrical stage with floor, right velvet leg, top valance, left Programme rail (acts I to IX),
/// display title at (300, 84), lead subtitle at (302, 96 + size), top-right status, and bottom-right hints.
/// </summary>
internal static class SpotlightToolFrame
{
    public static Grid WrapWithProgramme(
        string currentKey,
        string title,
        string lead,
        string backLabel,
        Action onBack,
        Action<string>? onNavigate,
        Canvas bodyStage,
        (string Key, string Label)[]? hints = null,
        double titleSize = 40,
        double leadW = 1000,
        double floorY = 760)
    {
        var grid = new Grid { Width = 1600, Height = 900, ClipToBounds = true };
        var canvas = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };

        // 1. Theatrical stage: floor, right leg, top valance
        canvas.Children.Add(SpotlightKit.TheatricalStage(floorY: floorY, legs: true, valance: true, leftLeg: false));

        // 2. Programme rail down the left (I to IX)
        var rail = SpotlightParts.ProgrammeRail(currentKey, onNavigate);
        canvas.Children.Add(rail);

        // 3. Title at (300, 84) in Perpetua Titling MT uppercase
        if (!string.IsNullOrEmpty(title))
        {
            var titleEl = new TextBlock
            {
                Text = title.ToUpperInvariant(),
                FontFamily = SpotlightKit.Display,
                FontSize = titleSize,
                FontWeight = FontWeights.Light,
                Foreground = SpotlightKit.B("#f3e7d3")
            };
            Canvas.SetLeft(titleEl, 300);
            Canvas.SetTop(titleEl, 84);
            canvas.Children.Add(titleEl);
        }

        // 4. Lead text at (302, 96 + titleSize) in Garamond italic
        if (!string.IsNullOrEmpty(lead))
        {
            var leadEl = new TextBlock
            {
                Text = lead,
                FontFamily = SpotlightKit.Body,
                FontStyle = FontStyles.Italic,
                FontSize = 17,
                Foreground = SpotlightKit.B("#b3a48e"),
                Width = leadW,
                TextWrapping = TextWrapping.Wrap
            };
            Canvas.SetLeft(leadEl, 302);
            Canvas.SetTop(leadEl, 96 + titleSize);
            canvas.Children.Add(leadEl);
        }

        // 5. Status / Back action at (right: 60, top: 82)
        var rightStack = new StackPanel { Orientation = Orientation.Horizontal };
        if (!string.IsNullOrEmpty(backLabel))
        {
            var backBtn = SpotlightKit.TheatricalButton(backLabel, primary: false, key: "B", onClick: onBack, height: 38, fontSize: 11);
            backBtn.Margin = new Thickness(0, 0, 18, 0);
            rightStack.Children.Add(backBtn);
        }
        rightStack.Children.Add(SpotlightParts.StatusHeader());
        Canvas.SetRight(rightStack, 60);
        Canvas.SetTop(rightStack, 82);
        canvas.Children.Add(rightStack);

        // 6. Body Stage
        Canvas.SetLeft(bodyStage, 0);
        Canvas.SetTop(bodyStage, 0);
        canvas.Children.Add(bodyStage);

        // 7. Hints bar at (right: 60, top: 852)
        if (hints is { Length: > 0 })
        {
            var hintsEl = SpotlightParts.HintsBar(hints);
            Canvas.SetRight(hintsEl, 60);
            Canvas.SetTop(hintsEl, 852);
            canvas.Children.Add(hintsEl);
        }

        grid.Children.Add(canvas);
        return grid;
    }
}
