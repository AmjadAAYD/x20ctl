using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Dusk;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private void DuskShowTester()
    {
        duskPageHost!.Children.Clear();

        var hints = new[]
        {
            ("A", "TEST"),
            ("B", "BACK")
        };

        var contentCanvas = new Canvas { Width = 1040, Height = 580 };

        // Center Pad visual (w: 620)
        var centerStack = new StackPanel { Width = 620, Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Center };

        var padCanvas = new Canvas { Width = 620, Height = 360 };
        var pedestal = DuskKit.PedestalGlow(500, 100, DuskKit.CyanNeon);
        Canvas.SetLeft(pedestal, 60);
        Canvas.SetTop(pedestal, 240);
        padCanvas.Children.Add(pedestal);

        string padKey = Model?.Id?.ToLowerInvariant() ?? "x05pro";
        if (padKey.Contains("05")) padKey = "x05pro";
        var padArt = DuskKit.ControllerArt(padKey, 520);
        if (padArt != null)
        {
            padArt.Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 30, ShadowDepth = 15, Opacity = 0.6 };
            Canvas.SetLeft(padArt, 50);
            Canvas.SetTop(padArt, 0);
            padCanvas.Children.Add(padArt);
        }
        centerStack.Children.Add(padCanvas);

        // View toggle tabs (FRONT / BACK)
        var tabStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 16, 0, 16) };
        var frontTab = new Border
        {
            Padding = new Thickness(24, 8, 24, 8),
            CornerRadius = new CornerRadius(18),
            Background = DuskKit.SlateTabOnBrush,
            Margin = new Thickness(0, 0, 8, 0)
        };
        frontTab.Child = new TextBlock
        {
            Text = "FRONT",
            FontFamily = DuskKit.Display,
            FontSize = 15,
            FontWeight = FontWeights.Bold,
            Foreground = DuskKit.WhiteBrush
        };
        tabStack.Children.Add(frontTab);

        var backTab = new Border
        {
            Padding = new Thickness(24, 8, 24, 8),
            CornerRadius = new CornerRadius(18),
            Background = Brushes.Transparent,
            Cursor = Cursors.Hand
        };
        backTab.Child = new TextBlock
        {
            Text = "BACK",
            FontFamily = DuskKit.Display,
            FontSize = 15,
            FontWeight = FontWeights.Bold,
            Foreground = DuskKit.LightBrush
        };
        tabStack.Children.Add(backTab);
        centerStack.Children.Add(tabStack);

        // Gauges shelf along bottom with specular cyan edge
        var gaugeShelf = new Border
        {
            Width = 620,
            Height = 64,
            CornerRadius = new CornerRadius(4),
            Background = DuskKit.SlateItemBrush,
            BorderBrush = DuskKit.B("rgba(26,159,255,0.35)"),
            BorderThickness = new Thickness(1, 2, 1, 1),
            Padding = new Thickness(16, 0, 16, 0),
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 14, ShadowDepth = 4, Opacity = 0.4 }
        };
        var gGrid = new Grid();
        gGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        gGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        gGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var gItems = new (string Lab, string Val, Color Col)[]
        {
            ("POLLING LATENCY", "0.98 ms", DuskKit.GreenOnline),
            ("INPUT JITTER", "0.04 ms", DuskKit.C("#1a9fff")),
            ("PACKET RATE", "1002 Hz", DuskKit.TextWhite)
        };

        for (int i = 0; i < gItems.Length; i++)
        {
            var (lab, val, col) = gItems[i];
            var colStack = new StackPanel { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            colStack.Children.Add(new TextBlock
            {
                Text = lab,
                FontFamily = DuskKit.Display,
                FontSize = 11,
                FontWeight = FontWeights.ExtraBold,
                Foreground = DuskKit.MuteBrush,
                Margin = new Thickness(0, 0, 0, 2)
            });
            colStack.Children.Add(new TextBlock
            {
                Text = val,
                FontFamily = DuskKit.Display,
                FontSize = 17,
                FontWeight = FontWeights.Bold,
                Foreground = DuskKit.B(col)
            });
            Grid.SetColumn(colStack, i);
            gGrid.Children.Add(colStack);
        }
        gaugeShelf.Child = gGrid;
        centerStack.Children.Add(gaugeShelf);

        Canvas.SetLeft(centerStack, 0);
        Canvas.SetTop(centerStack, 0);
        contentCanvas.Children.Add(centerStack);

        // Right side: Source card & action buttons
        var rightStack = new StackPanel { Width = 380, Orientation = Orientation.Vertical };

        var testCard = new Border
        {
            Width = 360,
            CornerRadius = new CornerRadius(6),
            Background = DuskKit.SlateItemBrush,
            BorderBrush = DuskKit.B("rgba(26,159,255,0.3)"),
            BorderThickness = new Thickness(1, 2, 1, 1),
            Padding = new Thickness(22)
        };
        var tStack = new StackPanel { Orientation = Orientation.Vertical };

        var tBadge = new Border
        {
            Padding = new Thickness(8, 3, 8, 3),
            Background = DuskKit.B("rgba(14,18,24,0.85)"),
            BorderBrush = DuskKit.B("rgba(26,159,255,0.4)"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 0, 8),
            Child = new TextBlock
            {
                Text = "HID PACKET INSPECTOR // 1000HZ",
                FontFamily = DuskKit.Display,
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = DuskKit.CyanNeonBrush
            }
        };
        tStack.Children.Add(tBadge);

        tStack.Children.Add(new TextBlock
        {
            Text = "INPUT SOURCE",
            FontFamily = DuskKit.Display,
            FontSize = 13,
            FontWeight = FontWeights.ExtraBold,
            Foreground = DuskKit.MuteBrush,
            Margin = new Thickness(0, 0, 0, 4)
        });
        tStack.Children.Add(new TextBlock
        {
            Text = "Direct HID Descriptor",
            FontFamily = DuskKit.Display,
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            Foreground = DuskKit.WhiteBrush,
            Margin = new Thickness(0, 0, 0, 4)
        });
        tStack.Children.Add(new TextBlock
        {
            Text = "Direct raw packet stream captures low-level button actuation before OS driver translation.",
            FontFamily = DuskKit.Body,
            FontSize = 14,
            Foreground = DuskKit.LightBrush,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 16)
        });

        var btnStack = new StackPanel { Orientation = Orientation.Horizontal };
        var pBtn = new Border
        {
            Height = 44,
            Padding = new Thickness(20, 0, 20, 0),
            CornerRadius = new CornerRadius(4),
            Background = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0.5),
                EndPoint = new Point(1, 0.5),
                GradientStops = { new(DuskKit.GreenGrad0, 0.0), new(DuskKit.GreenGrad1, 1.0) }
            },
            Cursor = Cursors.Hand,
            Margin = new Thickness(0, 0, 10, 0)
        };
        pBtn.Child = new TextBlock
        {
            Text = "Start Test",
            FontFamily = DuskKit.Display,
            FontSize = 15,
            FontWeight = FontWeights.Bold,
            Foreground = DuskKit.WhiteBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
        btnStack.Children.Add(pBtn);

        var rBtn = new Border
        {
            Height = 44,
            Padding = new Thickness(20, 0, 20, 0),
            CornerRadius = new CornerRadius(4),
            Background = DuskKit.SlateTabOnBrush,
            Cursor = Cursors.Hand
        };
        rBtn.Child = new TextBlock
        {
            Text = "Clear History",
            FontFamily = DuskKit.Display,
            FontSize = 15,
            FontWeight = FontWeights.Bold,
            Foreground = DuskKit.LightBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
        btnStack.Children.Add(rBtn);
        tStack.Children.Add(btnStack);

        testCard.Child = tStack;
        rightStack.Children.Add(testCard);

        Canvas.SetLeft(rightStack, 640);
        Canvas.SetTop(rightStack, 0);
        contentCanvas.Children.Add(rightStack);

        var shell = DuskKit.SettingsShell(
            activeKey: "tester",
            title: "Input Tester",
            lead: "Real-time hardware inspection with raw HID packet verification and latency gauges.",
            content: contentCanvas,
            hints: hints,
            onNav: key => DuskSelectPage(key)
        );

        duskPageHost.Children.Add(shell);
    }
}
