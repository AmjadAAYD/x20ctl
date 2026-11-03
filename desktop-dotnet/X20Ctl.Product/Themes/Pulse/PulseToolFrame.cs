using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Pulse;

namespace X20Ctl.Product;

/// <summary>
/// Full 1600×900 stage wrapper for Pulse standalone screens (Tools, About, Feedback, Themes, Read scan, Write scan, Advanced)
/// with the left orbital dock, top header, and bottom footer hint bar matching t_pulse.py frame().
/// </summary>
internal static class PulseToolFrame
{
    public static Grid WrapWithOrbit(
        string activeKey,
        string title,
        string lead,
        string backLabel,
        Action onBack,
        Action<string>? onNavigate,
        Canvas bodyStage,
        (string Key, string Label)[] hints,
        double titleSize = 44)
    {
        var canvas = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };
        canvas.Children.Add(PulseKit.VoidBackdrop());

        // 1. Left Orbit Navigation per t_pulse.py:304
        var orbit = PulseParts.LeftOrbitNav(activeKey, key =>
        {
            if (string.Equals(key, activeKey, StringComparison.OrdinalIgnoreCase)) return;
            onNavigate?.Invoke(key);
        }, out _);
        Canvas.SetLeft(orbit, 22);
        Canvas.SetTop(orbit, 22);
        canvas.Children.Add(orbit);

        // 2. Page Title at (270, 30) per t_pulse.py:325
        if (!string.IsNullOrEmpty(title))
        {
            var titleBlock = PulseParts.Disp(title, titleSize, PulseParts.TextPrimary, FontWeights.ExtraLight);
            Canvas.SetLeft(titleBlock, 270);
            Canvas.SetTop(titleBlock, 30);
            canvas.Children.Add(titleBlock);
        }

        // 3. Lead text at (272, 38 + titleSize) per t_pulse.py:327
        if (!string.IsNullOrEmpty(lead))
        {
            var leadBlock = PulseParts.Body(lead, 15, PulseParts.TextSoft);
            leadBlock.Width = 1000;
            Canvas.SetLeft(leadBlock, 272);
            Canvas.SetTop(leadBlock, 36 + titleSize);
            canvas.Children.Add(leadBlock);
        }

        // 4. Top-Right Status / Back Action at (right: 44, top: 32)
        var rightStack = new StackPanel { Orientation = Orientation.Horizontal };
        var backBtn = PulseParts.PillButton(backLabel, primary: false, onPearl: false, small: true, onClick: onBack);
        rightStack.Children.Add(backBtn);
        Canvas.SetRight(rightStack, 44);
        Canvas.SetTop(rightStack, 32);
        canvas.Children.Add(rightStack);

        // 5. Page Body Stage
        Canvas.SetLeft(bodyStage, 0);
        Canvas.SetTop(bodyStage, 0);
        canvas.Children.Add(bodyStage);

        // 6. Bottom Hints per t_pulse.py:328
        if (hints is { Length: > 0 })
        {
            var hintsHost = new StackPanel { Orientation = Orientation.Horizontal };
            PulseParts.PopulateFooterHints(hintsHost, hints);
            Canvas.SetRight(hintsHost, 44);
            Canvas.SetTop(hintsHost, 848);
            canvas.Children.Add(hintsHost);
        }

        return new Grid
        {
            Background = new SolidColorBrush(PulseParts.VoidColor),
            Children = { new Viewbox { Stretch = Stretch.Uniform, Child = canvas } }
        };
    }
}
