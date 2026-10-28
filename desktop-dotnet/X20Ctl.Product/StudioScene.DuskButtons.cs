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
    private void DuskShowButtons()
    {
        duskPageHost!.Children.Clear();

        var hints = new[]
        {
            ("Y", "ALL BUTTONS"),
            ("A", "SELECT"),
            ("B", "BACK")
        };

        var contentCanvas = new Canvas { Width = 1040, Height = 580 };

        // Left list (w: 580)
        var listStack = new StackPanel { Width = 580, Orientation = Orientation.Vertical };

        listStack.Children.Add(new TextBlock
        {
            Text = "FACE BUTTONS",
            FontFamily = DuskKit.Display,
            FontSize = 18,
            FontWeight = FontWeights.ExtraBold,
            Foreground = DuskKit.MuteBrush,
            Margin = new Thickness(0, 0, 0, 4)
        });

        listStack.Children.Add(new TextBlock
        {
            Text = "LOCAL DRAFT · Not written to controller memory.",
            FontFamily = DuskKit.Body,
            FontSize = 15,
            Foreground = DuskKit.MuteBrush,
            Margin = new Thickness(0, 0, 0, 16)
        });

        var faceButtons = new[]
        {
            ("A button", "B", true),
            ("B button", "B", false),
            ("X button", "X", false),
            ("Y button", "Y", false)
        };

        foreach (var (btnName, mapping, active) in faceButtons)
        {
            var row = new Border
            {
                Height = 64,
                BorderThickness = new Thickness(0, 0, 0, 1),
                BorderBrush = DuskKit.B("rgba(255,255,255,.07)"),
                Background = active ? DuskKit.B("rgba(255,255,255,.05)") : Brushes.Transparent,
                Padding = new Thickness(16, 0, 16, 0),
                CornerRadius = new CornerRadius(4)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var lStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var glyph = DuskKit.XboxButtonGlyph(btnName.Split(' ')[0], 28);
            glyph.Margin = new Thickness(0, 0, 14, 0);
            lStack.Children.Add(glyph);

            var label = new TextBlock
            {
                Text = btnName,
                FontFamily = DuskKit.Display,
                FontSize = 21,
                FontWeight = FontWeights.SemiBold,
                Foreground = active ? DuskKit.WhiteBrush : DuskKit.LightBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            lStack.Children.Add(label);
            Grid.SetColumn(lStack, 0);
            grid.Children.Add(lStack);

            // Dropdown selector pill (.dd)
            var dd = new Border
            {
                Height = 40,
                Padding = new Thickness(18, 0, 14, 0),
                CornerRadius = new CornerRadius(4),
                Background = DuskKit.SlateItemBrush,
                BorderBrush = active ? DuskKit.B("#1a9fff") : DuskKit.SlateBorderBrush,
                BorderThickness = new Thickness(active ? 2 : 1),
                VerticalAlignment = VerticalAlignment.Center
            };
            var ddStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            ddStack.Children.Add(new TextBlock
            {
                Text = mapping,
                FontFamily = DuskKit.Display,
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = DuskKit.WhiteBrush,
                Margin = new Thickness(0, 0, 10, 0)
            });
            ddStack.Children.Add(new TextBlock
            {
                Text = "\uE70D",
                FontFamily = DuskKit.Icons,
                FontSize = 14,
                Foreground = DuskKit.MuteBrush,
                VerticalAlignment = VerticalAlignment.Center
            });
            dd.Child = ddStack;
            Grid.SetColumn(dd, 1);
            grid.Children.Add(dd);

            row.Child = grid;
            listStack.Children.Add(row);
        }

        // Section: TEST
        listStack.Children.Add(new TextBlock
        {
            Text = "TEST",
            FontFamily = DuskKit.Display,
            FontSize = 18,
            FontWeight = FontWeights.ExtraBold,
            Foreground = DuskKit.MuteBrush,
            Margin = new Thickness(0, 24, 0, 8)
        });

        var testRow = new Border
        {
            Height = 64,
            BorderThickness = new Thickness(0, 0, 0, 1),
            BorderBrush = DuskKit.B("rgba(255,255,255,.07)"),
            Padding = new Thickness(16, 0, 16, 0),
            CornerRadius = new CornerRadius(4)
        };
        var testGrid = new Grid();
        testGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        testGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var testLabel = new TextBlock
        {
            Text = "Keyboard test",
            FontFamily = DuskKit.Display,
            FontSize = 21,
            FontWeight = FontWeights.SemiBold,
            Foreground = DuskKit.LightBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(testLabel, 0);
        testGrid.Children.Add(testLabel);

        var chev = new TextBlock
        {
            Text = "\uE76C",
            FontFamily = DuskKit.Icons,
            FontSize = 18,
            Foreground = DuskKit.MuteBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(chev, 1);
        testGrid.Children.Add(chev);

        testRow.Child = testGrid;
        listStack.Children.Add(testRow);

        Canvas.SetLeft(listStack, 0);
        Canvas.SetTop(listStack, 0);
        contentCanvas.Children.Add(listStack);

        // Right side: Pad callout visual at (620, 0, w: 420)
        var rightPanel = new StackPanel { Width = 420, Orientation = Orientation.Vertical };

        var padCanvas = new Canvas { Width = 420, Height = 280, ClipToBounds = false };

        // Floor pedestal glow beneath controller
        var pedestal = DuskKit.PedestalGlow(360, 80, DuskKit.CyanNeon);
        Canvas.SetLeft(pedestal, 30);
        Canvas.SetTop(pedestal, 200);
        padCanvas.Children.Add(pedestal);

        string padKey = Model?.Id?.ToLowerInvariant() ?? "x05pro";
        if (padKey.Contains("05")) padKey = "x05pro";
        var padArt = DuskKit.ControllerArt(padKey, 430);
        if (padArt != null)
        {
            padArt.Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 24, ShadowDepth = 12, Opacity = 0.5 };
            Canvas.SetLeft(padArt, -5);
            Canvas.SetTop(padArt, 10);
            padCanvas.Children.Add(padArt);
        }

        // Highlight ring on A button with radiant cyan bloom
        var aRing = new Border
        {
            Width = 52,
            Height = 52,
            CornerRadius = new CornerRadius(26),
            BorderBrush = DuskKit.CyanNeonBrush,
            BorderThickness = new Thickness(3.5),
            Background = DuskKit.B("rgba(26,159,255,0.18)"),
            Effect = new DropShadowEffect { Color = DuskKit.CyanNeon, BlurRadius = 22, ShadowDepth = 0, Opacity = 0.95 }
        };
        Canvas.SetLeft(aRing, 309);
        Canvas.SetTop(aRing, 99);
        padCanvas.Children.Add(aRing);

        // Holographic leader line from A button to detail card
        var lineGeom = new PathGeometry();
        var lineFig = new PathFigure { StartPoint = new Point(335, 151) };
        lineFig.Segments.Add(new LineSegment(new Point(370, 200), true));
        lineFig.Segments.Add(new LineSegment(new Point(370, 275), true));
        lineGeom.Figures.Add(lineFig);
        var leaderLine = new System.Windows.Shapes.Path
        {
            Data = lineGeom,
            Stroke = DuskKit.CyanNeonBrush,
            StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection { 4, 3 },
            Opacity = 0.85,
            Effect = new DropShadowEffect { Color = DuskKit.CyanNeon, BlurRadius = 8, ShadowDepth = 0, Opacity = 0.8 }
        };
        padCanvas.Children.Add(leaderLine);

        rightPanel.Children.Add(padCanvas);

        // Detail card below pad (.card2) with cyan specular border
        var detailCard = new Border
        {
            Background = DuskKit.SlateItemBrush,
            BorderBrush = DuskKit.CyanNeonBrush,
            BorderThickness = new Thickness(3, 1, 1, 1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(24),
            Margin = new Thickness(0, 16, 0, 0),
            Effect = new DropShadowEffect { Color = DuskKit.CyanNeon, BlurRadius = 14, ShadowDepth = 0, Opacity = 0.25 }
        };
        var detailStack = new StackPanel { Orientation = Orientation.Vertical };

        var topHead = new StackPanel { Orientation = Orientation.Horizontal };
        topHead.Children.Add(new TextBlock
        {
            Text = "ASSIGNMENT",
            FontFamily = DuskKit.Display,
            FontSize = 14,
            FontWeight = FontWeights.ExtraBold,
            Foreground = DuskKit.MuteBrush
        });
        var pinChip = new Border
        {
            Padding = new Thickness(8, 2, 8, 2),
            CornerRadius = new CornerRadius(3),
            Background = DuskKit.B("rgba(26,159,255,0.18)"),
            BorderBrush = DuskKit.B("rgba(26,159,255,0.4)"),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(12, 0, 0, 0),
            Child = new TextBlock
            {
                Text = "PIN: GPIO_14 · 1000HZ",
                FontFamily = DuskKit.Display,
                FontSize = 10,
                FontWeight = FontWeights.ExtraBold,
                Foreground = DuskKit.CyanNeonBrush
            }
        };
        topHead.Children.Add(pinChip);
        detailStack.Children.Add(topHead);
        detailStack.Children.Add(new TextBlock
        {
            Text = "A Button → B",
            FontFamily = DuskKit.Display,
            FontSize = 24,
            FontWeight = FontWeights.Bold,
            Foreground = DuskKit.WhiteBrush,
            Margin = new Thickness(0, 0, 0, 6)
        });
        detailStack.Children.Add(new TextBlock
        {
            Text = "Direct hardware remap with instant actuation and zero latency penalty.",
            FontFamily = DuskKit.Body,
            FontSize = 15,
            Foreground = DuskKit.LightBrush,
            TextWrapping = TextWrapping.Wrap
        });
        detailCard.Child = detailStack;
        rightPanel.Children.Add(detailCard);

        Canvas.SetLeft(rightPanel, 620);
        Canvas.SetTop(rightPanel, 0);
        contentCanvas.Children.Add(rightPanel);

        var shell = DuskKit.SettingsShell(
            activeKey: "buttons",
            title: "Buttons",
            lead: "Customize button bindings, rapid fire toggles, and turbo profiles.",
            content: contentCanvas,
            hints: hints,
            onNav: key => DuskSelectPage(key)
        );

        duskPageHost.Children.Add(shell);
    }
}
