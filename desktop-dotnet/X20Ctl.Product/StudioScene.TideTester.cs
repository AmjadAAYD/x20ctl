using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Tide;

namespace X20Ctl.Product;

public sealed partial class StudioScene
{
    private void TideShowTester()
    {
        ConsoleSurface.Children.Clear();
        var frame = new TideToolFrame(
            "TESTER",
            "Testing inputs for EasySMX X15",
            onBack: () => TideShowDashboard(),
            hints: new[] { "\u24B7 Back", "\u24B6 Clear Log", "\u2295 Switch View" },
            seed: 15);

        // 1. Controller visual at (230, 176, w=560) rotated -10deg
        var padContainer = new Canvas { Width = 640, Height = 420 };
        var art = TideKit.Character("x15-starfield", 30, 20, 560, -10);
        padContainer.Children.Add(art);
        Canvas.SetLeft(padContainer, 230);
        Canvas.SetTop(padContainer, 176);
        frame.ContentArea.Children.Add(padContainer);

        // 2. Tabs at (230, 606): Front, Back, Triggers, Raw
        var tabsRow = new StackPanel { Orientation = Orientation.Horizontal };
        string[] views = ["FRONT", "BACK", "TRIGGERS", "RAW"];
        for (int i = 0; i < views.Length; i++)
        {
            var tabBtn = TideKit.PersonaButton(views[i], primary: (i == 0));
            tabBtn.Margin = new Thickness(0, 0, 12, 0);
            tabsRow.Children.Add(tabBtn);
        }
        Canvas.SetLeft(tabsRow, 230);
        Canvas.SetTop(tabsRow, 606);
        frame.ContentArea.Children.Add(tabsRow);

        // 3. Bottom Gauges Shelf at (230, 698, w=1310) rotated -1deg
        var gaugesCard = TideKit.PersonaCard(1310, 72, -1);
        var gaugesGrid = new Grid();
        gaugesGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        gaugesGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        gaugesGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        gaugesGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        string[] gauges = ["LEFT STICK: (0.00, 0.00)", "RIGHT STICK: (0.00, 0.00)", "LT: 0.0% · RT: 0.0%", "INPUT BUFFER: 1000 HZ"];
        for (int i = 0; i < gauges.Length; i++)
        {
            var gText = new TextBlock
            {
                Text = gauges[i],
                FontFamily = TideKit.Display,
                FontWeight = FontWeights.Black,
                FontSize = 14,
                Foreground = (i == 3) ? TideKit.SpikeRedBrush : TideKit.Sea0Brush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(gText, i);
            gaugesGrid.Children.Add(gText);
        }
        gaugesCard.Child = gaugesGrid;
        Canvas.SetLeft(gaugesCard, 230);
        Canvas.SetTop(gaugesCard, 698);
        frame.ContentArea.Children.Add(gaugesCard);

        // 4. Side Detail Card at (940, 182, w=580)
        var detailCard = TideKit.DetailCard(
            title: "XInput 1",
            sub: "DirectInput / XInput",
            body: "Waiting for test inputs... Squeeze triggers, rotate sticks, or press buttons to verify response.",
            foot: "DirectInput / XInput active · Polling at 1000 Hz",
            buttons: new[] { "Clear Log", "Done" },
            width: 580,
            rot: 2);

        Canvas.SetLeft(detailCard, 940);
        Canvas.SetTop(detailCard, 182);
        frame.ContentArea.Children.Add(detailCard);

        ConsoleSurface.Children.Add(frame);
    }
}
