using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Nocturne;

namespace X20Ctl.Product;

public sealed partial class StickToolsPanel
{
    public Action<string>? NocturneNavigate { get; set; }
    private NocturneToolFrame? nocturneFrame;
    private Action? nocturneCloseAction;
    private Button? nocturneResetButton;

    private void BuildNocturneAdvanced(Action close, Button reset)
    {
        nocturneCloseAction = close;
        nocturneResetButton = reset;

        CornerRadius = new(0);
        BorderThickness = new(0);
        Padding = new(0);
        Margin = new(0);
        MaxWidth = double.PositiveInfinity;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        Background = NocturneKit.VoidBrush;

        nocturneFrame = new NocturneToolFrame(
            "ADVANCED STICK CALIBRATION",
            "Fine deadzones, anti-deadzone compensation, and response tensioning",
            onBack: () => close());
        Child = nocturneFrame;

        var canvas = nocturneFrame.ContentArea;

        var mainSp = new StackPanel { Width = 720 };
        mainSp.Children.Add(new TextBlock
        {
            Text = "ACCESSIBILITY & CALIBRATION TOOLS",
            FontFamily = NocturneKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 16,
            Foreground = NocturneKit.VoidTextBrush,
            Margin = new Thickness(0, 0, 0, 14)
        });

        mainSp.Children.Add(new TextBlock
        {
            Text = "Stick calibrations apply on this PC via the virtual controller driver.\nUse the sensitivity sliders and deadzone fields in Curves to adjust curve behavior.",
            FontFamily = NocturneKit.Body,
            FontSize = 13,
            Foreground = NocturneKit.SoftBrush,
            LineHeight = 20,
            Margin = new Thickness(0, 12, 0, 20)
        });

        var actRow = new StackPanel { Orientation = Orientation.Horizontal };
        var doneBtn = NocturneKit.NocturneButton("Done", () => close(), isPrimary: true, width: 140, height: 38);
        doneBtn.Margin = new Thickness(0, 0, 12, 0);

        var resetBtn = NocturneKit.NocturneButton("Reset to defaults", () =>
        {
            reset.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        }, isPrimary: false, width: 160, height: 38);

        actRow.Children.Add(doneBtn);
        actRow.Children.Add(resetBtn);
        mainSp.Children.Add(actRow);

        var card = NocturneKit.NocturneWhiteCard(mainSp, 780, 360);
        Canvas.SetLeft(card, 410);
        Canvas.SetTop(card, 120);
        canvas.Children.Add(card);
    }
}
