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
    private void DuskShowPcMode()
    {
        duskPageHost!.Children.Clear();

        var hints = new[]
        {
            ("A", "SELECT"),
            ("B", "BACK")
        };

        var contentCanvas = new Canvas { Width = 1040, Height = 580 };

        // Left list (w: 580)
        var leftStack = new StackPanel { Width = 580, Orientation = Orientation.Vertical };

        leftStack.Children.Add(new TextBlock
        {
            Text = "PROTOCOL & ROUTING",
            FontFamily = DuskKit.Display,
            FontSize = 18,
            FontWeight = FontWeights.ExtraBold,
            Foreground = DuskKit.MuteBrush,
            Margin = new Thickness(0, 0, 0, 4)
        });

        leftStack.Children.Add(new TextBlock
        {
            Text = "LOCAL DRAFT · Applied on connection handshake.",
            FontFamily = DuskKit.Body,
            FontSize = 15,
            Foreground = DuskKit.MuteBrush,
            Margin = new Thickness(0, 0, 0, 14)
        });

        var protoModes = new (string Name, string Sub, string Val, bool Active)[]
        {
            ("XInput Mode (Xbox standard)", "Full native Windows games support with rumble", "Active", true),
            ("DirectInput Mode", "Legacy retro game and emulator compatibility", "Disabled", false),
            ("Switch Pro Mode", "Gyroscope orientation and Nintendo layout", "Disabled", false),
            ("Keyboard / Mouse Mode", "Desktop media keys and cursor emulation", "Disabled", false)
        };

        foreach (var (name, sub, val, active) in protoModes)
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

            var nameStack = new StackPanel { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center };
            nameStack.Children.Add(new TextBlock
            {
                Text = name,
                FontFamily = DuskKit.Display,
                FontSize = 19,
                FontWeight = FontWeights.SemiBold,
                Foreground = active ? DuskKit.WhiteBrush : DuskKit.LightBrush
            });
            nameStack.Children.Add(new TextBlock
            {
                Text = sub,
                FontFamily = DuskKit.Body,
                FontSize = 13,
                Foreground = DuskKit.MuteBrush,
                Margin = new Thickness(0, 2, 0, 0)
            });
            Grid.SetColumn(nameStack, 0);
            grid.Children.Add(nameStack);

            // Toggle switch (.tg)
            var tg = new Border
            {
                Width = 58,
                Height = 30,
                CornerRadius = new CornerRadius(15),
                Background = active ? DuskKit.B("#1a9fff") : DuskKit.SlateTabOnBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            var tgCanvas = new Canvas { Width = 58, Height = 30 };
            var knob = new Border
            {
                Width = 24,
                Height = 24,
                CornerRadius = new CornerRadius(12),
                Background = DuskKit.WhiteBrush
            };
            Canvas.SetLeft(knob, active ? 31 : 3);
            Canvas.SetTop(knob, 3);
            tgCanvas.Children.Add(knob);
            tg.Child = tgCanvas;
            Grid.SetColumn(tg, 1);
            grid.Children.Add(tg);

            row.Child = grid;
            leftStack.Children.Add(row);
        }

        Canvas.SetLeft(leftStack, 0);
        Canvas.SetTop(leftStack, 0);
        contentCanvas.Children.Add(leftStack);

        // Right side: Signal routing diagram + details
        var rightStack = new StackPanel { Width = 380, Orientation = Orientation.Vertical };

        var routeCard = new Border
        {
            Width = 360,
            CornerRadius = new CornerRadius(6),
            Background = DuskKit.SlateItemBrush,
            BorderBrush = DuskKit.B("rgba(26,159,255,0.35)"),
            BorderThickness = new Thickness(1, 2, 1, 1),
            Padding = new Thickness(20),
            Effect = new DropShadowEffect { Color = DuskKit.CyanNeon, BlurRadius = 14, ShadowDepth = 0, Opacity = 0.2 }
        };
        var routeStack = new StackPanel { Orientation = Orientation.Vertical };

        var rHead = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 14) };
        rHead.Children.Add(new TextBlock
        {
            Text = "SIGNAL PIPELINE",
            FontFamily = DuskKit.Display,
            FontSize = 13,
            FontWeight = FontWeights.ExtraBold,
            Foreground = DuskKit.MuteBrush
        });
        var pipeChip = new Border
        {
            Padding = new Thickness(8, 2, 8, 2),
            CornerRadius = new CornerRadius(3),
            Background = DuskKit.B("rgba(26,159,255,0.18)"),
            BorderBrush = DuskKit.B("rgba(26,159,255,0.4)"),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(12, 0, 0, 0),
            Child = new TextBlock
            {
                Text = "LOW LATENCY · 0.98MS",
                FontFamily = DuskKit.Display,
                FontSize = 10,
                FontWeight = FontWeights.ExtraBold,
                Foreground = DuskKit.CyanNeonBrush
            }
        };
        rHead.Children.Add(pipeChip);
        routeStack.Children.Add(rHead);

        // Cybernetic signal nodes
        var nodes = new (string Name, string Sub, string Icon, Color DotCol, Color BorderCol)[]
        {
            ("Physical Hardware", "Raw USB HID · 1000 Hz", "\uE7FC", DuskKit.GreenOnline, DuskKit.C("#3d4450")),
            ("ViGEmBus Emulation", "Kernel Bus Intercept v1.22", "\uE777", DuskKit.CyanNeon, DuskKit.CyanNeon),
            ("Virtual XInput Pad", "DirectX / Xbox 360 Compatible", "\uE977", DuskKit.GreenOnline, DuskKit.GreenOnline)
        };

        for (int i = 0; i < nodes.Length; i++)
        {
            var (nName, nSub, nIcon, nDot, nBorder) = nodes[i];
            var nodeCard = new Border
            {
                Padding = new Thickness(14, 10, 14, 10),
                CornerRadius = new CornerRadius(4),
                Background = DuskKit.B("#141920"),
                BorderBrush = DuskKit.B(nBorder),
                BorderThickness = new Thickness(1),
                Effect = new DropShadowEffect { Color = nBorder, BlurRadius = 8, ShadowDepth = 0, Opacity = 0.3 }
            };
            var nGrid = new Grid();
            nGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            nGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            nGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var icBox = new Border
            {
                Width = 32,
                Height = 32,
                CornerRadius = new CornerRadius(4),
                Background = DuskKit.B("rgba(255,255,255,0.06)"),
                Margin = new Thickness(0, 0, 12, 0),
                Child = new TextBlock
                {
                    Text = nIcon,
                    FontFamily = DuskKit.Icons,
                    FontSize = 16,
                    Foreground = DuskKit.WhiteBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            Grid.SetColumn(icBox, 0);
            nGrid.Children.Add(icBox);

            var textStack = new StackPanel { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center };
            textStack.Children.Add(new TextBlock { Text = nName, FontFamily = DuskKit.Display, FontSize = 14, FontWeight = FontWeights.Bold, Foreground = DuskKit.WhiteBrush });
            textStack.Children.Add(new TextBlock { Text = nSub, FontFamily = DuskKit.Body, FontSize = 11, Foreground = DuskKit.MuteBrush, Margin = new Thickness(0, 2, 0, 0) });
            Grid.SetColumn(textStack, 1);
            nGrid.Children.Add(textStack);

            var dot = new Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = DuskKit.B(nDot),
                Effect = new DropShadowEffect { Color = nDot, BlurRadius = 6, ShadowDepth = 0, Opacity = 0.9 },
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(dot, 2);
            nGrid.Children.Add(dot);

            nodeCard.Child = nGrid;
            routeStack.Children.Add(nodeCard);

            if (i < nodes.Length - 1)
            {
                var connector = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 4) };
                connector.Children.Add(new TextBlock
                {
                    Text = "↓  1000 HZ USB POLLING · ZERO LOSS  ↓",
                    FontFamily = DuskKit.Display,
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = DuskKit.CyanNeonBrush
                });
                routeStack.Children.Add(connector);
            }
        }
        routeCard.Child = routeStack;
        rightStack.Children.Add(routeCard);

        // Driver card with lime green specular border
        var driverCard = new Border
        {
            Background = DuskKit.SlateItemBrush,
            BorderBrush = DuskKit.GreenOnlineBrush,
            BorderThickness = new Thickness(3, 1, 1, 1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(20),
            Margin = new Thickness(0, 16, 0, 0),
            Width = 360,
            Effect = new DropShadowEffect { Color = DuskKit.GreenOnline, BlurRadius = 12, ShadowDepth = 0, Opacity = 0.2 }
        };
        var dStack = new StackPanel { Orientation = Orientation.Vertical };
        dStack.Children.Add(new TextBlock
        {
            Text = "DRIVER STATUS",
            FontFamily = DuskKit.Display,
            FontSize = 13,
            FontWeight = FontWeights.ExtraBold,
            Foreground = DuskKit.MuteBrush,
            Margin = new Thickness(0, 0, 0, 4)
        });
        dStack.Children.Add(new TextBlock
        {
            Text = "ViGEmBus & HidHide Ready",
            FontFamily = DuskKit.Display,
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            Foreground = DuskKit.GreenOnlineBrush,
            Margin = new Thickness(0, 0, 0, 4)
        });
        dStack.Children.Add(new TextBlock
        {
            Text = "Direct hardware masking active. Physical DirectInput device hidden to eliminate double input glitches.",
            FontFamily = DuskKit.Body,
            FontSize = 14,
            Foreground = DuskKit.LightBrush,
            TextWrapping = TextWrapping.Wrap
        });
        driverCard.Child = dStack;
        rightStack.Children.Add(driverCard);

        Canvas.SetLeft(rightStack, 640);
        Canvas.SetTop(rightStack, 0);
        contentCanvas.Children.Add(rightStack);

        var shell = DuskKit.SettingsShell(
            activeKey: "pcmode",
            title: "PC Mode & Routing",
            lead: "Configure direct USB, Bluetooth, and 2.4G wireless virtual controller routing.",
            content: contentCanvas,
            hints: hints,
            onNav: key => DuskSelectPage(key)
        );

        duskPageHost.Children.Add(shell);
    }
}
