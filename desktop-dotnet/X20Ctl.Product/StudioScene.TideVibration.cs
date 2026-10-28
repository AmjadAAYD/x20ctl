using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Tide;

namespace X20Ctl.Product;

public sealed partial class StudioScene
{
    private void TideShowVibration()
    {
        ConsoleSurface.Children.Clear();
        var frame = new TideToolFrame(
            "VIBRATION",
            "Linked grip strength · Local draft",
            onBack: () => TideShowDashboard(),
            hints: new[] { "\u24B7 Back", "\u24B6 Select", "\u24BD Reset", "LT/RT Strength", "LB/RB Tabs" },
            seed: 7);

        // Left List: Staircase rows
        var listStack = new StackPanel { Width = 640 };
        listStack.Children.Add(TideKit.PersonaRow("Strength", value: "70%", focused: true));
        listStack.Children.Add(TideKit.PersonaRow("Preview region", ctl: "dd:All"));
        listStack.Children.Add(TideKit.PersonaRow("Test on controller", value: "Physical output unavailable"));

        var listHost = new Canvas
        {
            Width = 640,
            RenderTransformOrigin = new Point(0, 0),
            RenderTransform = new RotateTransform(-3)
        };
        listHost.Children.Add(listStack);
        frame.SetLeftList(listHost, 176);

        // In Base.settings, when n <= 4, visual takes the room under the list on the left side:
        // y + n * RH + 36 = 176 + 3 * 56 + 36 = 380
        var padContainer = new Canvas { Width = 556, Height = 300 };
        var art = TideKit.ControllerArt("x15-starfield", 440);
        art.RenderTransformOrigin = new Point(0.5, 0.5);
        art.RenderTransform = new RotateTransform(-8);
        Canvas.SetLeft(art, 50);
        Canvas.SetTop(art, 10);
        padContainer.Children.Add(art);

        // Wave arcs around grips
        for (int i = 0; i < 3; i++)
        {
            var arcL = new Ellipse
            {
                Width = 70 + i * 20,
                Height = 100 + i * 26,
                Stroke = TideKit.LightCyanBrush,
                StrokeThickness = 3,
                Opacity = 0.8 - i * 0.25
            };
            Canvas.SetLeft(arcL, 40 - i * 12);
            Canvas.SetTop(arcL, 90 - i * 12);
            padContainer.Children.Add(arcL);

            var arcR = new Ellipse
            {
                Width = 70 + i * 20,
                Height = 100 + i * 26,
                Stroke = TideKit.LightCyanBrush,
                StrokeThickness = 3,
                Opacity = 0.8 - i * 0.25
            };
            Canvas.SetLeft(arcR, 410 - i * 4);
            Canvas.SetTop(arcR, 90 - i * 12);
            padContainer.Children.Add(arcR);
        }

        Canvas.SetLeft(padContainer, 230);
        Canvas.SetTop(padContainer, 380);
        frame.ContentArea.Children.Add(padContainer);

        // Right side: Detail Card only
        var detailCard = TideKit.DetailCard(
            title: "Vibration Preview",
            value: "70%",
            chips: new[] { "Off", "25%", "50%", "75%", "100%" },
            body: "Region selection changes the illustration only. This backend has one linked strength; independent trigger tuning is unavailable.",
            foot: "Preview animation · No motor telemetry or physical vibration is generated",
            buttons: new[] { "Test Vibration", "Done" },
            width: 580,
            rot: 2);

        var sideStack = TideKit.SideStack(null, detailCard, 580);
        frame.SetSideStack(sideStack, 176);
        frame.SetNote("Vibration preview · No local changes");

        ConsoleSurface.Children.Add(frame);
    }
}
