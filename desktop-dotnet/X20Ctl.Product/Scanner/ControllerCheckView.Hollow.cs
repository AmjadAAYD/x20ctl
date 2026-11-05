using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Hollow;

namespace X20Ctl.Product.Scanner;

public sealed partial class ControllerCheckView
{
    private void BuildHollowReadWarning(Panel cards, FrameworkElement systemRow, FrameworkElement choices, FrameworkElement linux)
    {
        var frame = new HollowToolFrame(
            "BEFORE YOU SCAN",
            "Guided step-by-step diagnostic read scan for buttons, triggers, and sticks.",
            onBack: () => close());

        var listStack = new StackPanel { Width = 720 };
        listStack.Children.Add(new TextBlock
        {
            Text = "READ SCAN INSTRUCTIONS",
            FontFamily = HollowKit.Display,
            FontSize = 16,
            Foreground = HollowKit.IvoryTextBrush,
            Margin = new Thickness(0, 0, 0, 12)
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
            var rowSp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };
            rowSp.Children.Add(new Path { Data = HollowKit.CreateDiamond(6), Fill = HollowKit.IvoryTextBrush, Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center });
            rowSp.Children.Add(new TextBlock { Text = s, FontFamily = HollowKit.Body, FontSize = 13, Foreground = HollowKit.SoftTextBrush });
            listStack.Children.Add(rowSp);
        }

        if (choices.Parent is Panel pChoices) pChoices.Children.Remove(choices);
        else if (choices.Parent is ContentControl cChoices) cChoices.Content = null;
        else if (choices.Parent is Border bChoices) bChoices.Child = null;
        else if (choices.Parent is Decorator dChoices) dChoices.Child = null;
        choices.Margin = new Thickness(0, 20, 0, 0);
        listStack.Children.Add(choices);

        var card = HollowKit.FiligreeCard(listStack, padding: 24, cornerSize: 16);
        card.Width = 780;
        Canvas.SetLeft(card, 410);
        Canvas.SetTop(card, 80);
        frame.ContentArea.Children.Add(card);

        Children.Add(frame);
    }
}
