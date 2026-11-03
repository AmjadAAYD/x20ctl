using System.Windows.Data;
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
    private FrameworkElement? duskTesterView;
    private void DuskShowTester()
    {
        if (Tester is not {} tester) return;
        duskPageHost!.Children.Clear();
        if (duskTesterView != null) { duskPageHost.Children.Add(duskTesterView); return; }

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

        var livePad = new Viewbox { Width = 520, Height = 350, Stretch = Stretch.Uniform, Child = tester.TakeStage() }; Canvas.SetLeft(livePad, 50); Canvas.SetTop(livePad, 0); padCanvas.Children.Add(livePad);
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
        tabStack.Children.Add(ThemeAction(frontTab, "theme:tester:front", () => tester.ShowRear(false)));

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
        tabStack.Children.Add(ThemeAction(backTab, "theme:tester:rear", () => tester.ShowRear(true), HasRearArtwork));
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

        var gItems = new (string Lab, TextBlock Val, Color Col)[]
        {
            ("SOURCE", tester.SourceLabel, DuskKit.GreenOnline),
            ("PRESSED", tester.PressedBlock, DuskKit.CyanNeon),
            ("INPUT OWNERSHIP", tester.OwnershipBlock, DuskKit.TextWhite)
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
                Text = val.Text,
                FontFamily = DuskKit.Display,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 190,
                Foreground = DuskKit.B(col)
            });
            ((TextBlock)colStack.Children[1]).SetBinding(TextBlock.TextProperty, new Binding("Text") { Source = val });
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
                Text = "LIVE INPUT · SELECTED SOURCE",
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
            Text = tester.SourceText,
            FontFamily = DuskKit.Display,
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            Foreground = DuskKit.WhiteBrush,
            Margin = new Thickness(0, 0, 0, 4)
        });
        tStack.Children.Add(new TextBlock
        {
            Text = "Use real controller input or an explicitly labelled keyboard simulation.",
            FontFamily = DuskKit.Body,
            FontSize = 14,
            Foreground = DuskKit.LightBrush,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 16)
        });

        ((TextBlock)tStack.Children[2]).SetBinding(TextBlock.TextProperty, new Binding("Text") { Source = tester.SourceLabel });
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
        ((TextBlock)pBtn.Child).SetBinding(TextBlock.TextProperty, new Binding("Content") { Source = tester.KeyboardMode }); btnStack.Children.Add(ThemeAction(pBtn, "theme:tester:keyboard", tester.ClickKeyboard));

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
            Text = "Leave Tester",
            FontFamily = DuskKit.Display,
            FontSize = 15,
            FontWeight = FontWeights.Bold,
            Foreground = DuskKit.LightBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
        btnStack.Children.Add(ThemeAction(rBtn, "theme:tester:exit", tester.ClickExit));
        tStack.Children.Add(btnStack);

        testCard.Child = tStack;
        rightStack.Children.Add(testCard);

        Canvas.SetLeft(rightStack, 640);
        Canvas.SetTop(rightStack, 0);
        contentCanvas.Children.Add(rightStack);

        var shell = DuskKit.SettingsShell(
            activeKey: "tester",
            title: "Input Tester",
            lead: "Live input from the selected source. Keyboard simulation is labelled separately.",
            content: contentCanvas,
            hints: hints,
            onNav: key => DuskSelectPage(key)
        );

        duskTesterView = shell; duskPageHost.Children.Add(shell);
    }
}
