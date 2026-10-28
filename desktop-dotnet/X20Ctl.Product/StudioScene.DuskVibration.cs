using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Dusk;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private void DuskShowVibration()
    {
        duskPageHost!.Children.Clear();

        var hints = new[]
        {
            ("X", "TEST RUMBLE"),
            ("A", "SELECT"),
            ("B", "BACK")
        };

        var contentCanvas = new Canvas { Width = 1040, Height = 580 };

        // Left list (w: 580)
        var leftStack = new StackPanel { Width = 580, Orientation = Orientation.Vertical };

        leftStack.Children.Add(new TextBlock
        {
            Text = "MOTOR INTENSITY",
            FontFamily = DuskKit.Display,
            FontSize = 18,
            FontWeight = FontWeights.ExtraBold,
            Foreground = DuskKit.MuteBrush,
            Margin = new Thickness(0, 0, 0, 4)
        });

        leftStack.Children.Add(new TextBlock
        {
            Text = "LOCAL DRAFT · Not written to controller memory.",
            FontFamily = DuskKit.Body,
            FontSize = 15,
            Foreground = DuskKit.MuteBrush,
            Margin = new Thickness(0, 0, 0, 14)
        });

        var motors = new (string Name, int Val, bool Active)[]
        {
            ("Left Heavy Motor", 80, true),
            ("Right Light Motor", 65, false),
            ("Left Trigger Haptics", 50, false),
            ("Right Trigger Haptics", 50, false)
        };

        foreach (var (name, val, active) in motors)
        {
            var row = new Border
            {
                Height = 60,
                BorderThickness = new Thickness(0, 0, 0, 1),
                BorderBrush = DuskKit.B("rgba(255,255,255,.07)"),
                Background = active ? DuskKit.B("rgba(255,255,255,.05)") : Brushes.Transparent,
                Padding = new Thickness(16, 0, 16, 0),
                CornerRadius = new CornerRadius(4)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var label = new TextBlock
            {
                Text = name,
                FontFamily = DuskKit.Display,
                FontSize = 20,
                FontWeight = FontWeights.SemiBold,
                Foreground = active ? DuskKit.WhiteBrush : DuskKit.LightBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(label, 0);
            grid.Children.Add(label);

            // Slider control
            var ctrlStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            var trackCanvas = new Canvas { Width = 220, Height = 20, VerticalAlignment = VerticalAlignment.Center };
            var bgTrack = new Border
            {
                Width = 220,
                Height = 6,
                CornerRadius = new CornerRadius(3),
                Background = DuskKit.B("#3d4450")
            };
            Canvas.SetLeft(bgTrack, 0);
            Canvas.SetTop(bgTrack, 7);
            trackCanvas.Children.Add(bgTrack);

            double fillW = Math.Max(0, 220.0 * (val / 100.0));
            var fillTrack = new Border
            {
                Width = fillW,
                Height = 6,
                CornerRadius = new CornerRadius(3),
                Background = DuskKit.B("#1a9fff")
            };
            Canvas.SetLeft(fillTrack, 0);
            Canvas.SetTop(fillTrack, 7);
            trackCanvas.Children.Add(fillTrack);

            var knob = new Border
            {
                Width = 20,
                Height = 20,
                CornerRadius = new CornerRadius(10),
                Background = DuskKit.WhiteBrush,
                Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 6, ShadowDepth = 2, Opacity = 0.4 }
            };
            Canvas.SetLeft(knob, Math.Clamp(fillW - 10, 0, 200));
            Canvas.SetTop(knob, 0);
            trackCanvas.Children.Add(knob);

            ctrlStack.Children.Add(trackCanvas);

            var valBlock = new TextBlock
            {
                Text = $"{val}%",
                FontFamily = DuskKit.Display,
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = DuskKit.WhiteBrush,
                Width = 64,
                TextAlignment = TextAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(14, 0, 0, 0)
            };
            ctrlStack.Children.Add(valBlock);

            Grid.SetColumn(ctrlStack, 1);
            grid.Children.Add(ctrlStack);

            row.Child = grid;
            leftStack.Children.Add(row);
        }

        Canvas.SetLeft(leftStack, 0);
        Canvas.SetTop(leftStack, 0);
        contentCanvas.Children.Add(leftStack);

        // Right side: Rumble pad visual + details
        var rightStack = new StackPanel { Width = 380, Orientation = Orientation.Vertical };

        var vibCard = new Border
        {
            Width = 360,
            Height = 240,
            CornerRadius = new CornerRadius(6),
            Background = DuskKit.B("#141920"),
            BorderBrush = DuskKit.B("#232a33"),
            BorderThickness = new Thickness(1),
            ClipToBounds = true
        };
        var vibCanvas = new Canvas { Width = 360, Height = 240 };

        // Theatrical stage floor pedestal beneath controller
        var pedestal = DuskKit.PedestalGlow(280, 70, DuskKit.CyanNeon);
        Canvas.SetLeft(pedestal, 40);
        Canvas.SetTop(pedestal, 160);
        vibCanvas.Children.Add(pedestal);

        string padKey = Model?.Id?.ToLowerInvariant() ?? "x05pro";
        if (padKey.Contains("05")) padKey = "x05pro";
        var padArt = DuskKit.ControllerArt(padKey, 340);
        if (padArt != null)
        {
            padArt.Opacity = 0.90;
            padArt.Effect = new DropShadowEffect { Color = DuskKit.CyanNeon, BlurRadius = 24, ShadowDepth = 0, Opacity = 0.45 };
            Canvas.SetLeft(padArt, 10);
            Canvas.SetTop(padArt, 15);
            vibCanvas.Children.Add(padArt);
        }

        // Dual concentric vibration shockwaves over Left grip (Cyan)
        var waveLeftInner = new Ellipse
        {
            Width = 55,
            Height = 100,
            Stroke = DuskKit.CyanNeonBrush,
            StrokeThickness = 2.0,
            StrokeDashArray = new DoubleCollection { 4, 3 },
            Effect = new DropShadowEffect { Color = DuskKit.CyanNeon, BlurRadius = 10, ShadowDepth = 0, Opacity = 0.85 }
        };
        Canvas.SetLeft(waveLeftInner, 22);
        Canvas.SetTop(waveLeftInner, 70);
        vibCanvas.Children.Add(waveLeftInner);

        var waveLeftOuter = new Ellipse
        {
            Width = 80,
            Height = 135,
            Stroke = DuskKit.CyanNeonBrush,
            StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection { 5, 4 },
            Opacity = 0.65,
            Effect = new DropShadowEffect { Color = DuskKit.CyanNeon, BlurRadius = 16, ShadowDepth = 0, Opacity = 0.65 }
        };
        Canvas.SetLeft(waveLeftOuter, 10);
        Canvas.SetTop(waveLeftOuter, 52);
        vibCanvas.Children.Add(waveLeftOuter);

        // Dual concentric vibration shockwaves over Right grip (Neon Lime)
        var waveRightInner = new Ellipse
        {
            Width = 55,
            Height = 100,
            Stroke = DuskKit.GreenOnlineBrush,
            StrokeThickness = 2.0,
            StrokeDashArray = new DoubleCollection { 4, 3 },
            Effect = new DropShadowEffect { Color = DuskKit.GreenOnline, BlurRadius = 10, ShadowDepth = 0, Opacity = 0.85 }
        };
        Canvas.SetLeft(waveRightInner, 285);
        Canvas.SetTop(waveRightInner, 70);
        vibCanvas.Children.Add(waveRightInner);

        var waveRightOuter = new Ellipse
        {
            Width = 80,
            Height = 135,
            Stroke = DuskKit.GreenOnlineBrush,
            StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection { 5, 4 },
            Opacity = 0.65,
            Effect = new DropShadowEffect { Color = DuskKit.GreenOnline, BlurRadius = 16, ShadowDepth = 0, Opacity = 0.65 }
        };
        Canvas.SetLeft(waveRightOuter, 272);
        Canvas.SetTop(waveRightOuter, 52);
        vibCanvas.Children.Add(waveRightOuter);

        // Live 16-bar haptic frequency spectrum visualizer
        var specStack = new StackPanel { Orientation = Orientation.Horizontal };
        double[] barHeights = { 6, 12, 18, 24, 20, 16, 22, 28, 25, 19, 14, 18, 22, 15, 10, 5 };
        for (int i = 0; i < barHeights.Length; i++)
        {
            Color bCol = i < 8 ? DuskKit.CyanNeon : DuskKit.GreenOnline;
            var bar = new Border
            {
                Width = 10,
                Height = barHeights[i],
                CornerRadius = new CornerRadius(2),
                Background = DuskKit.B(bCol),
                Margin = new Thickness(0, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Bottom,
                Opacity = 0.85,
                Effect = new DropShadowEffect { Color = bCol, BlurRadius = 6, ShadowDepth = 0, Opacity = 0.7 }
            };
            specStack.Children.Add(bar);
        }
        Canvas.SetLeft(specStack, 70);
        Canvas.SetTop(specStack, 205);
        vibCanvas.Children.Add(specStack);

        // Corner brackets on vibCard
        vibCanvas.Children.Add(DuskKit.CornerBrackets(360, 240, DuskKit.CyanNeon, 12, 1.2));

        vibCard.Child = vibCanvas;
        rightStack.Children.Add(vibCard);

        // Details card with cyan highlight border
        var detailCard = new Border
        {
            Background = DuskKit.SlateItemBrush,
            BorderBrush = DuskKit.CyanNeonBrush,
            BorderThickness = new Thickness(3, 1, 1, 1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(20),
            Margin = new Thickness(0, 16, 0, 0),
            Width = 360,
            Effect = new DropShadowEffect { Color = DuskKit.CyanNeon, BlurRadius = 12, ShadowDepth = 0, Opacity = 0.2 }
        };
        var detailStack = new StackPanel { Orientation = Orientation.Vertical };
        detailStack.Children.Add(new TextBlock
        {
            Text = "HAPTIC FEEDBACK",
            FontFamily = DuskKit.Display,
            FontSize = 13,
            FontWeight = FontWeights.ExtraBold,
            Foreground = DuskKit.MuteBrush,
            Margin = new Thickness(0, 0, 0, 4)
        });
        detailStack.Children.Add(new TextBlock
        {
            Text = "Dual Eccentric Weight Rumble",
            FontFamily = DuskKit.Display,
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            Foreground = DuskKit.WhiteBrush,
            Margin = new Thickness(0, 0, 0, 4)
        });
        detailStack.Children.Add(new TextBlock
        {
            Text = "Left motor delivers low-frequency impact rumble; right motor provides high-frequency texture hum.",
            FontFamily = DuskKit.Body,
            FontSize = 14,
            Foreground = DuskKit.LightBrush,
            TextWrapping = TextWrapping.Wrap
        });
        detailCard.Child = detailStack;
        rightStack.Children.Add(detailCard);

        // Action button to test rumble
        var testRumbleBtn = DuskKit.GreenActionButton("Test Rumble", 360, 48, () => { });
        testRumbleBtn.Margin = new Thickness(0, 12, 0, 0);
        rightStack.Children.Add(testRumbleBtn);

        Canvas.SetLeft(rightStack, 640);
        Canvas.SetTop(rightStack, 0);
        contentCanvas.Children.Add(rightStack);

        var shell = DuskKit.SettingsShell(
            activeKey: "vibration",
            title: "Vibration & Haptics",
            lead: "Adjust dual-rumble motor intensity, trigger haptics, and response damping.",
            content: contentCanvas,
            hints: hints,
            onNav: key => DuskSelectPage(key)
        );

        duskPageHost.Children.Add(shell);
    }
}
