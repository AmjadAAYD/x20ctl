using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Tide;

namespace X20Ctl.Product;

public sealed partial class StudioScene
{
    private void TideShowTester()
    {
        if (tidePageHost == null) return;
        tidePageHost.Children.Clear();

        var board = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };

        // 1. Cinematic Sea Background
        board.Children.Add(TideKit.SeaBackground(15, "LIVE TESTER"));

        var tester = Tester;

        // 2. Top Header Bar: Logo, Title, Actions, Back
        var topBar = new Canvas { Width = 1520, Height = 64 };
        Canvas.SetLeft(topBar, 40);
        Canvas.SetTop(topBar, 20);

        // Title
        var titleStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var moonIcon = new Border
        {
            Width = 32,
            Height = 32,
            CornerRadius = new CornerRadius(16),
            Background = new RadialGradientBrush
            {
                Center = new Point(0.4, 0.4),
                GradientStops =
                {
                    new(Colors.White, 0.0),
                    new(Color.FromRgb(0x9f, 0xf6, 0xff), 0.7),
                    new(Color.FromRgb(0x3a, 0xa5, 0xff), 1.0)
                }
            },
            Margin = new Thickness(0, 0, 12, 0),
            Effect = new DropShadowEffect { Color = Color.FromRgb(0x9f, 0xe0, 0xff), BlurRadius = 14, ShadowDepth = 0 }
        };
        titleStack.Children.Add(moonIcon);
        titleStack.Children.Add(new TextBlock
        {
            Text = "TIDE // LIVE HARDWARE TESTER",
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 20,
            Foreground = TideKit.WhiteBrush,
            VerticalAlignment = VerticalAlignment.Center
        });
        topBar.Children.Add(titleStack);
        Canvas.SetLeft(titleStack, 0);
        Canvas.SetTop(titleStack, 14);

        // Header Actions: Keyboard mode & Exit to Dashboard
        var hActions = new StackPanel { Orientation = Orientation.Horizontal };

        var kbBtn = TideKit.OceanButton(tester?.KeyboardText ?? "KEYBOARD TEST", icon: "\uE765", primary: tester?.KeyboardOn == true, onClick: () =>
        {
            tester?.ClickKeyboard();
            TideShowTester();
        });
        kbBtn.Margin = new Thickness(0, 0, 10, 0);
        hActions.Children.Add(kbBtn);

        var exitBtn = TideKit.OceanButton("DASHBOARD", icon: "\uE72B", onClick: () =>
        {
            tester?.ClickExit();
            TideShowDashboard();
        });
        hActions.Children.Add(exitBtn);

        topBar.Children.Add(hActions);
        Canvas.SetRight(hActions, 0);
        Canvas.SetTop(hActions, 10);

        board.Children.Add(topBar);

        // 3. Center-Left Stage — Dual Analog Stick Radar Visualizer
        var radarCard = TideKit.OceanGlassCard(w: 660, h: 680);
        Canvas.SetLeft(radarCard, 60);
        Canvas.SetTop(radarCard, 100);

        var radarStack = new StackPanel();

        // Header
        var rHead = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 16) };
        rHead.Children.Add(new TextBlock
        {
            Text = "\uE9D9",
            FontFamily = TideKit.Icons,
            FontSize = 18,
            Foreground = TideKit.CyanFoamBrush,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        rHead.Children.Add(new TextBlock
        {
            Text = "ANALOG RADAR OSCILLOSCOPES",
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 16,
            Foreground = TideKit.WhiteBrush,
            VerticalAlignment = VerticalAlignment.Center
        });
        radarStack.Children.Add(rHead);

        // Dual Radars Row (Left Stick & Right Stick)
        var dualRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 20) };

        // Helper to build a stick circular radar
        Border BuildStickRadar(string name, double? sx, double? sy, string driftText)
        {
            var rBox = new Border
            {
                Width = 290,
                Height = 310,
                CornerRadius = new CornerRadius(12),
                Background = new SolidColorBrush(Color.FromArgb(0xaa, 0x04, 0x12, 0x34)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x60, 0x9f, 0xf6, 0xff)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(6),
                Padding = new Thickness(12)
            };

            var rContent = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            rContent.Children.Add(new TextBlock
            {
                Text = name,
                FontFamily = TideKit.Display,
                FontWeight = FontWeights.Black,
                FontStyle = FontStyles.Italic,
                FontSize = 13,
                Foreground = TideKit.WhiteBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 8)
            });

            // Circular Scope (200x200)
            var cScope = new Canvas
            {
                Width = 200,
                Height = 200,
                Background = new RadialGradientBrush
                {
                    Center = new Point(0.5, 0.5),
                    GradientStops =
                    {
                        new(Color.FromArgb(0xe0, 0x02, 0x08, 0x1e), 0.0),
                        new(Color.FromArgb(0xf8, 0x01, 0x04, 0x12), 1.0)
                    }
                },
                ClipToBounds = true
            };

            // Outer Circle Ring
            var ring = new Ellipse
            {
                Width = 196,
                Height = 196,
                Stroke = new SolidColorBrush(Color.FromArgb(0x50, 0x9f, 0xf6, 0xff)),
                StrokeThickness = 1.5
            };
            Canvas.SetLeft(ring, 2);
            Canvas.SetTop(ring, 2);
            cScope.Children.Add(ring);

            // Crosshair lines
            var crossX = new Line { X1 = 0, Y1 = 100, X2 = 200, Y2 = 100, Stroke = new SolidColorBrush(Color.FromArgb(0x30, 0x9f, 0xf6, 0xff)), StrokeThickness = 1 };
            var crossY = new Line { X1 = 100, Y1 = 0, X2 = 100, Y2 = 200, Stroke = new SolidColorBrush(Color.FromArgb(0x30, 0x9f, 0xf6, 0xff)), StrokeThickness = 1 };
            cScope.Children.Add(crossX);
            cScope.Children.Add(crossY);

            // Position Dot
            double nx = (sx ?? 0);
            double ny = (sy ?? 0);
            double px = 100 + nx * 85 - 8;
            double py = 100 - ny * 85 - 8;

            var posDot = new Ellipse
            {
                Width = 16,
                Height = 16,
                Fill = TideKit.HarpoonRedBrush,
                Effect = new DropShadowEffect { Color = TideKit.HarpoonRed, BlurRadius = 14, ShadowDepth = 0 }
            };
            Canvas.SetLeft(posDot, px);
            Canvas.SetTop(posDot, py);
            cScope.Children.Add(posDot);

            rContent.Children.Add(cScope);

            // Coordinates Readout
            rContent.Children.Add(new TextBlock
            {
                Text = $"X: {nx:+0.00;-0.00;0.00}  Y: {ny:+0.00;-0.00;0.00}",
                FontFamily = TideKit.Display,
                FontWeight = FontWeights.Black,
                FontStyle = FontStyles.Italic,
                FontSize = 12,
                Foreground = TideKit.CyanFoamBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 8, 0, 2)
            });

            // Drift score
            rContent.Children.Add(new TextBlock
            {
                Text = driftText,
                FontFamily = TideKit.Body,
                FontWeight = FontWeights.Bold,
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromArgb(0xaa, 0xc0, 0xdc, 0xf8)),
                HorizontalAlignment = HorizontalAlignment.Center
            });

            rBox.Child = rContent;
            return rBox;
        }

        dualRow.Children.Add(BuildStickRadar("LEFT ANALOG STICK", tester?.LeftX, tester?.LeftY, tester?.LeftX is double lx && tester?.LeftY is double ly ? $"X {lx:0.00} · Y {ly:0.00}" : "WAITING FOR INPUT"));
        dualRow.Children.Add(BuildStickRadar("RIGHT ANALOG STICK", tester?.RightX, tester?.RightY, tester?.RightX is double rx && tester?.RightY is double ry ? $"X {rx:0.00} · Y {ry:0.00}" : "WAITING FOR INPUT"));
        radarStack.Children.Add(dualRow);

        // Analog Triggers Progress (LT / RT)
        var triggerBox = new Border
        {
            CornerRadius = new CornerRadius(10),
            Background = new SolidColorBrush(Color.FromArgb(0x80, 0x04, 0x12, 0x34)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x60, 0x9f, 0xf6, 0xff)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(16, 12, 16, 12)
        };
        var tGrid = new Grid();
        tGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        tGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // LT Bar
        double ltVal = Math.Clamp(tester?.LeftTrigger ?? 0.0, 0.0, 1.0);
        var ltStack = new StackPanel { Margin = new Thickness(0, 0, 10, 0) };
        ltStack.Children.Add(new TextBlock
        {
            Text = $"LEFT TRIGGER (LT): {(int)(ltVal * 100)}%",
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 11,
            Foreground = TideKit.WhiteBrush,
            Margin = new Thickness(0, 0, 0, 6)
        });
        var ltTrack = new Border { Height = 10, CornerRadius = new CornerRadius(5), Background = new SolidColorBrush(Color.FromArgb(0x80, 0x02, 0x08, 0x1c)), ClipToBounds = true };
        ltTrack.Child = new Rectangle { HorizontalAlignment = HorizontalAlignment.Left, Width = 280 * ltVal, Fill = TideKit.HarpoonRedBrush };
        ltStack.Children.Add(ltTrack);
        Grid.SetColumn(ltStack, 0);
        tGrid.Children.Add(ltStack);

        // RT Bar
        double rtVal = Math.Clamp(tester?.RightTrigger ?? 0.0, 0.0, 1.0);
        var rtStack = new StackPanel { Margin = new Thickness(10, 0, 0, 0) };
        rtStack.Children.Add(new TextBlock
        {
            Text = $"RIGHT TRIGGER (RT): {(int)(rtVal * 100)}%",
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 11,
            Foreground = TideKit.WhiteBrush,
            Margin = new Thickness(0, 0, 0, 6)
        });
        var rtTrack = new Border { Height = 10, CornerRadius = new CornerRadius(5), Background = new SolidColorBrush(Color.FromArgb(0x80, 0x02, 0x08, 0x1c)), ClipToBounds = true };
        rtTrack.Child = new Rectangle { HorizontalAlignment = HorizontalAlignment.Left, Width = 280 * rtVal, Fill = TideKit.HarpoonRedBrush };
        rtStack.Children.Add(rtTrack);
        Grid.SetColumn(rtStack, 1);
        tGrid.Children.Add(rtStack);

        triggerBox.Child = tGrid;
        radarStack.Children.Add(triggerBox);

        radarCard.Child = radarStack;
        board.Children.Add(radarCard);

        // 4. Center-Right Stage — Digital Inputs & Polling Benchmark
        var consoleCard = TideKit.OceanGlassCard(w: 780, h: 680);
        Canvas.SetLeft(consoleCard, 760);
        Canvas.SetTop(consoleCard, 100);

        var cStack = new StackPanel();

        // Source Plaque
        var plaque = new Border
        {
            CornerRadius = new CornerRadius(12),
            Background = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 0),
                GradientStops =
                {
                    new(Color.FromArgb(0xcc, 0x18, 0x5a, 0xe0), 0.0),
                    new(Color.FromArgb(0xee, 0x0c, 0x32, 0x96), 1.0)
                }
            },
            BorderBrush = TideKit.CyanFoamBrush,
            BorderThickness = new Thickness(1.5),
            Padding = new Thickness(18, 14, 18, 14),
            Margin = new Thickness(0, 0, 0, 18),
            Effect = new DropShadowEffect { Color = Color.FromRgb(0x1a, 0x5a, 0xd8), BlurRadius = 18, ShadowDepth = 4 }
        };

        var pStack = new StackPanel();
        pStack.Children.Add(new TextBlock
        {
            Text = $"BUS: {tester?.SourceText ?? "CONNECTED HARDWARE"}",
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 22,
            Foreground = TideKit.WhiteBrush
        });
        pStack.Children.Add(new TextBlock
        {
            Text = $"LIVE PACKET OWNERSHIP: {tester?.OwnershipText ?? "DIRECT LOW-LATENCY ROUTE"}",
            FontFamily = TideKit.Body,
            FontWeight = FontWeights.Bold,
            FontSize = 12,
            Foreground = TideKit.CyanFoamBrush,
            Margin = new Thickness(0, 4, 0, 0)
        });
        plaque.Child = pStack;
        cStack.Children.Add(plaque);

        // Live Pressed Buttons Monitor
        cStack.Children.Add(new TextBlock
        {
            Text = "LIVE PRESSED INPUTS",
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 13,
            Foreground = TideKit.CyanFoamBrush,
            Margin = new Thickness(0, 0, 0, 8)
        });

        var pressedBox = new Border
        {
            CornerRadius = new CornerRadius(10),
            Background = new SolidColorBrush(Color.FromArgb(0x80, 0x04, 0x12, 0x34)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x60, 0x9f, 0xf6, 0xff)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(16),
            Margin = new Thickness(0, 0, 0, 20)
        };

        string pStr = tester?.PressedText ?? "NONE";
        var pressedContent = new StackPanel { Orientation = Orientation.Horizontal };
        pressedContent.Children.Add(new TextBlock
        {
            Text = "\uE765",
            FontFamily = TideKit.Icons,
            FontSize = 18,
            Foreground = TideKit.HarpoonRedBrush,
            Margin = new Thickness(0, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        pressedContent.Children.Add(new TextBlock
        {
            Text = string.IsNullOrEmpty(pStr) ? "NO BUTTONS PRESSED" : pStr,
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 16,
            Foreground = string.IsNullOrEmpty(pStr) || pStr == "NONE" ? new SolidColorBrush(Color.FromArgb(0xaa, 0xc0, 0xdc, 0xf8)) : TideKit.HarpoonRedBrush,
            VerticalAlignment = VerticalAlignment.Center
        });
        pressedBox.Child = pressedContent;
        cStack.Children.Add(pressedBox);

        consoleCard.Child = cStack;
        board.Children.Add(consoleCard);

        // 5. Bottom Navigation Dock
        var navDock = TideKit.OceanDock("Tester", TideSelectPage);
        Canvas.SetLeft(navDock, 230);
        Canvas.SetTop(navDock, 820);
        board.Children.Add(navDock);

        tidePageHost.Children.Add(board);
    }
}
