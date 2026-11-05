using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Hollow;

namespace X20Ctl.Product;

public sealed partial class StickToolsPanel
{
    public Action<string>? HollowNavigate { get; set; }
    private HollowToolFrame? hollowFrame;
    private Action? hollowCloseAction;
    private Button? hollowResetButton;

    private void BuildHollowAdvanced(Action close, Button reset)
    {
        hollowCloseAction = close;
        hollowResetButton = reset;

        CornerRadius = new(0);
        BorderThickness = new(0);
        Padding = new(0);
        Margin = new(0);
        MaxWidth = double.PositiveInfinity;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        Background = HollowKit.VoidBrush;

        hollowFrame = new HollowToolFrame(
            "ADVANCED STICK CALIBRATION",
            "Fine deadzones, anti-deadzone compensation, and response tensioning",
            onBack: () => close());
        Child = hollowFrame;

        var canvas = hollowFrame.ContentArea;

        var mainSp = new StackPanel { Width = 720 };
        mainSp.Children.Add(new TextBlock
        {
            Text = "ACCESSIBILITY & CALIBRATION TOOLS",
            FontFamily = HollowKit.Display,
            FontSize = 16,
            Foreground = HollowKit.IvoryTextBrush,
            Margin = new Thickness(0, 0, 0, 14)
        });
        mainSp.Children.Add(HollowKit.DiamondDivider(700, 14));

        mainSp.Children.Add(new TextBlock
        {
            Text = "Stick calibrations apply on this PC via the virtual controller driver.\nUse the sensitivity sliders and deadzone fields in Curves to adjust curve behavior.",
            FontFamily = HollowKit.Body,
            FontSize = 13,
            Foreground = HollowKit.SoftTextBrush,
            LineHeight = 20,
            Margin = new Thickness(0, 12, 0, 20)
        });

        var actRow = new StackPanel { Orientation = Orientation.Horizontal };
        var doneBtn = HollowKit.HollowButton("Done", () => close(), isPrimary: true, width: 120, height: 36);
        doneBtn.Margin = new Thickness(0, 0, 12, 0);

        var resetBtn = HollowKit.HollowButton("Reset to defaults", () =>
        {
            reset.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        }, isPrimary: false, width: 150, height: 36);

        actRow.Children.Add(doneBtn);
        actRow.Children.Add(resetBtn);
        mainSp.Children.Add(actRow);

        var card = HollowKit.FiligreeCard(mainSp, padding: 24, cornerSize: 16);
        card.Width = 780;
        Canvas.SetLeft(card, 410);
        Canvas.SetTop(card, 120);
        canvas.Children.Add(card);
    }
}
