using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Sunrise;

namespace X20Ctl.Product.Scanner;

public sealed partial class ControllerCheckView
{
    private void BuildSunriseReadWarning(Panel cards, FrameworkElement systemRow, FrameworkElement choices, FrameworkElement linux)
    {
        var frame = new SunriseToolFrame(
            "BEFORE YOU SCAN",
            "Guided step-by-step diagnostic read scan for buttons, triggers, and sticks.",
            onBack: () => close());

        var listStack = new StackPanel { Width = 720 };
        listStack.Children.Add(new TextBlock
        {
            Text = "READ SCAN INSTRUCTIONS",
            FontFamily = SunriseKit.Display,
            FontWeight = FontWeights.Black,
            FontSize = 16,
            Foreground = SunriseKit.DarkBrush,
            Margin = new Thickness(0, 0, 0, 14)
        });

        string[] steps =
        [
            "Reads input registers only; never writes to controller memory",
            "Verifies face buttons, shoulders, macro paddles, and D-pad",
            "Tests analog stick centering and deadzone travel",
            "Reads battery status over Bluetooth HID"
        ];

        foreach (var s in steps)
        {
            var rowSp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 6) };
            rowSp.Children.Add(new Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = SunriseKit.AccentBrush,
                Margin = new Thickness(0, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            rowSp.Children.Add(new TextBlock
            {
                Text = s,
                FontFamily = SunriseKit.Body,
                FontSize = 13.5,
                Foreground = SunriseKit.DarkBrush
            });
            listStack.Children.Add(rowSp);
        }

        if (choices.Parent is Panel pChoices) pChoices.Children.Remove(choices);
        else if (choices.Parent is ContentControl cChoices) cChoices.Content = null;
        else if (choices.Parent is Border bChoices) bChoices.Child = null;
        else if (choices.Parent is Decorator dChoices) dChoices.Child = null;
        choices.Margin = new Thickness(0, 24, 0, 0);
        listStack.Children.Add(choices);

        var card = SunriseKit.CreateStickerCard(listStack, 780, 400);
        Canvas.SetLeft(card, 410);
        Canvas.SetTop(card, 80);
        frame.ContentArea.Children.Add(card);

        Children.Add(frame);
    }
}
