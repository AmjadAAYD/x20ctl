using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Tide;

namespace X20Ctl.Product;

public sealed partial class StudioScene
{
    private void TideShowDiagnostics()
    {
        ConsoleSurface.Children.Clear();
        var frame = new TideToolFrame(
            "DIAGNOSTICS",
            "Roll each stick slowly around its edge a few times, squeeze both triggers all the way and press every button. X20CTL only listens; nothing is sent to the controller. All controller buttons are captured for testing. Use Esc or the mouse to leave.",
            onBack: () => TideShowDashboard(),
            hints: new[] { "Esc Exit capture" },
            seed: 16);

        // Left List: Staircase rows
        var listStack = new StackPanel { Width = 640 };
        listStack.Children.Add(TideKit.PersonaRow("Player input", ctl: "dd:XInput 1", focused: true));
        listStack.Children.Add(TideKit.PersonaRow("Retry player input", ctl: "chev"));
        listStack.Children.Add(TideKit.PersonaRow("Show integer XInput values", ctl: "toggle"));
        listStack.Children.Add(TideKit.PersonaRow("Measure centre deviation", ctl: "toggle"));
        listStack.Children.Add(TideKit.PersonaRow("Clear measurements", ctl: "chev"));
        listStack.Children.Add(TideKit.PersonaRow("Copy report", ctl: "chev"));

        var listHost = new Canvas
        {
            Width = 640,
            RenderTransformOrigin = new Point(0, 0),
            RenderTransform = new RotateTransform(-3)
        };
        listHost.Children.Add(listStack);
        frame.SetLeftList(listHost, 176);

        // Right side: Sticks Visual Card (vis.sticks: w=556, h=220, d=150)
        var sticksCanvas = new Canvas { Width = 556, Height = 220 };

        // 1. Left stick radar at X=16, d=150
        var lRadar = new Canvas { Width = 150, Height = 150 };
        lRadar.Children.Add(new Ellipse { Width = 150, Height = 150, Stroke = TideKit.Sea0Brush, StrokeThickness = 2 });
        var lInner = new Ellipse { Width = 90, Height = 90, Stroke = TideKit.Sea0Brush, StrokeThickness = 2, Opacity = 0.5 };
        Canvas.SetLeft(lInner, 30); Canvas.SetTop(lInner, 30);
        lRadar.Children.Add(lInner);

        var lAxisY = new Line { X1 = 75, Y1 = 9, X2 = 75, Y2 = 141, Stroke = new SolidColorBrush(Color.FromArgb((byte)(255 * 0.15), 11, 29, 142)), StrokeThickness = 1 };
        var lAxisX = new Line { X1 = 9, Y1 = 75, X2 = 141, Y2 = 75, Stroke = new SolidColorBrush(Color.FromArgb((byte)(255 * 0.15), 11, 29, 142)), StrokeThickness = 1 };
        lRadar.Children.Add(lAxisY);
        lRadar.Children.Add(lAxisX);

        var lDot = new Ellipse { Width = 14, Height = 14, Fill = TideKit.SubBrush };
        Canvas.SetLeft(lDot, 68); Canvas.SetTop(lDot, 68);
        lRadar.Children.Add(lDot);
        Canvas.SetLeft(lRadar, 16); Canvas.SetTop(lRadar, 6);
        sticksCanvas.Children.Add(lRadar);

        var lStack = new StackPanel { Width = 150 };
        lStack.Children.Add(new TextBlock { Text = "Left stick", FontFamily = TideKit.Display, FontWeight = FontWeights.Black, FontStyle = FontStyles.Italic, FontSize = 15, Foreground = TideKit.Sea0Brush, TextAlignment = TextAlignment.Center });
        lStack.Children.Add(new TextBlock { Text = "X 0 · Y 0", FontFamily = TideKit.Body, FontWeight = FontWeights.Bold, FontSize = 13, Foreground = TideKit.SubBrush, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 2, 0, 0) });
        Canvas.SetLeft(lStack, 16); Canvas.SetTop(lStack, 166);
        sticksCanvas.Children.Add(lStack);

        // 2. Right stick radar at X=180, d=150
        var rRadar = new Canvas { Width = 150, Height = 150 };
        rRadar.Children.Add(new Ellipse { Width = 150, Height = 150, Stroke = TideKit.Sea0Brush, StrokeThickness = 2 });
        var rInner = new Ellipse { Width = 90, Height = 90, Stroke = TideKit.Sea0Brush, StrokeThickness = 2, Opacity = 0.5 };
        Canvas.SetLeft(rInner, 30); Canvas.SetTop(rInner, 30);
        rRadar.Children.Add(rInner);

        var rAxisY = new Line { X1 = 75, Y1 = 9, X2 = 75, Y2 = 141, Stroke = new SolidColorBrush(Color.FromArgb((byte)(255 * 0.15), 11, 29, 142)), StrokeThickness = 1 };
        var rAxisX = new Line { X1 = 9, Y1 = 75, X2 = 141, Y2 = 75, Stroke = new SolidColorBrush(Color.FromArgb((byte)(255 * 0.15), 11, 29, 142)), StrokeThickness = 1 };
        rRadar.Children.Add(rAxisY);
        rRadar.Children.Add(rAxisX);

        var rDot = new Ellipse { Width = 14, Height = 14, Fill = TideKit.SubBrush };
        Canvas.SetLeft(rDot, 68); Canvas.SetTop(rDot, 68);
        rRadar.Children.Add(rDot);
        Canvas.SetLeft(rRadar, 180); Canvas.SetTop(rRadar, 6);
        sticksCanvas.Children.Add(rRadar);

        var rStack = new StackPanel { Width = 150 };
        rStack.Children.Add(new TextBlock { Text = "Right stick", FontFamily = TideKit.Display, FontWeight = FontWeights.Black, FontStyle = FontStyles.Italic, FontSize = 15, Foreground = TideKit.Sea0Brush, TextAlignment = TextAlignment.Center });
        rStack.Children.Add(new TextBlock { Text = "X 0 · Y 0", FontFamily = TideKit.Body, FontWeight = FontWeights.Bold, FontSize = 13, Foreground = TideKit.SubBrush, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 2, 0, 0) });
        Canvas.SetLeft(rStack, 180); Canvas.SetTop(rStack, 166);
        sticksCanvas.Children.Add(rStack);

        // 3. LT Trigger Bar at X=364, width=34, height=150
        var ltBar = new Border { Width = 34, Height = 150, Background = TideKit.Sea0Brush };
        Canvas.SetLeft(ltBar, 364); Canvas.SetTop(ltBar, 6);
        sticksCanvas.Children.Add(ltBar);

        var ltStack = new StackPanel { Width = 64 };
        ltStack.Children.Add(new TextBlock { Text = "LT", FontFamily = TideKit.Display, FontWeight = FontWeights.Black, FontStyle = FontStyles.Italic, FontSize = 15, Foreground = TideKit.Sea0Brush, TextAlignment = TextAlignment.Center });
        ltStack.Children.Add(new TextBlock { Text = "0 / 255", FontFamily = TideKit.Body, FontWeight = FontWeights.Bold, FontSize = 13, Foreground = TideKit.SubBrush, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 2, 0, 0) });
        Canvas.SetLeft(ltStack, 349); Canvas.SetTop(ltStack, 166);
        sticksCanvas.Children.Add(ltStack);

        // 4. RT Trigger Bar at X=464, width=34, height=150
        var rtBar = new Border { Width = 34, Height = 150, Background = TideKit.Sea0Brush };
        Canvas.SetLeft(rtBar, 464); Canvas.SetTop(rtBar, 6);
        sticksCanvas.Children.Add(rtBar);

        var rtStack = new StackPanel { Width = 64 };
        rtStack.Children.Add(new TextBlock { Text = "RT", FontFamily = TideKit.Display, FontWeight = FontWeights.Black, FontStyle = FontStyles.Italic, FontSize = 15, Foreground = TideKit.Sea0Brush, TextAlignment = TextAlignment.Center });
        rtStack.Children.Add(new TextBlock { Text = "0 / 255", FontFamily = TideKit.Body, FontWeight = FontWeights.Bold, FontSize = 13, Foreground = TideKit.SubBrush, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 2, 0, 0) });
        Canvas.SetLeft(rtStack, 449); Canvas.SetTop(rtStack, 166);
        sticksCanvas.Children.Add(rtStack);

        var visCard = TideKit.VisualCard(sticksCanvas, 580, -2);

        var detailCard = TideKit.DetailCard(
            title: "Observed input updates",
            value: "No input data · Rate unavailable",
            body: "Waiting for the selected player input. Connect it or choose an XInput index.",
            foot: "Player 1 · EasySMX X15 (assigned model) · Gameplay API: XInput · Transport and hardware identity unverified",
            width: 580,
            rot: 2);

        var sideStack = TideKit.SideStack(visCard, detailCard, 580);
        frame.SetSideStack(sideStack, 176);

        ConsoleSurface.Children.Add(frame);
    }
}
