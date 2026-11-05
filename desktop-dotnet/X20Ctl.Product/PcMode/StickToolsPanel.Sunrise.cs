using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Sunrise;

namespace X20Ctl.Product;

public sealed partial class StickToolsPanel
{
    public Action<string>? SunriseNavigate { get; set; }
    private SunriseToolFrame? sunriseFrame;
    private Action? sunriseCloseAction;
    private Button? sunriseResetButton;

    private void BuildSunriseAdvanced(Action close, Button reset)
    {
        sunriseCloseAction = close;
        sunriseResetButton = reset;

        CornerRadius = new(0);
        BorderThickness = new(0);
        Padding = new(0);
        Margin = new(0);
        MaxWidth = double.PositiveInfinity;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        Background = SunriseKit.PrimaryBrush;

        sunriseFrame = new SunriseToolFrame(
            "ADVANCED STICK CALIBRATION",
            "Fine deadzones, anti-deadzone compensation, and response tensioning",
            onBack: () => close());
        Child = sunriseFrame;

        var canvas = sunriseFrame.ContentArea;

        var mainSp = new StackPanel { Width = 720 };
        mainSp.Children.Add(new TextBlock
        {
            Text = "ACCESSIBILITY & CALIBRATION TOOLS",
            FontFamily = SunriseKit.Display,
            FontWeight = FontWeights.Black,
            FontSize = 16,
            Foreground = SunriseKit.DarkBrush,
            Margin = new Thickness(0, 0, 0, 14)
        });

        mainSp.Children.Add(new TextBlock
        {
            Text = "Stick calibrations apply on this PC via the virtual controller driver.\nUse the sensitivity sliders and deadzone fields in Curves to adjust curve behavior.",
            FontFamily = SunriseKit.Body,
            FontSize = 13.5,
            Foreground = SunriseKit.DarkBrush,
            LineHeight = 20,
            Margin = new Thickness(0, 12, 0, 20)
        });

        var actRow = new StackPanel { Orientation = Orientation.Horizontal };
        var doneBtn = SunriseKit.SunriseButton("Done", () => close(), isPrimary: true, width: 140, height: 38);
        doneBtn.Margin = new Thickness(0, 0, 12, 0);

        var resetBtn = SunriseKit.SunriseButton("Reset to defaults", () =>
        {
            reset.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        }, isPrimary: false, width: 160, height: 38);

        actRow.Children.Add(doneBtn);
        actRow.Children.Add(resetBtn);
        mainSp.Children.Add(actRow);

        var card = SunriseKit.CreateStickerCard(mainSp, 780, 360);
        Canvas.SetLeft(card, 410);
        Canvas.SetTop(card, 120);
        canvas.Children.Add(card);
    }
}
