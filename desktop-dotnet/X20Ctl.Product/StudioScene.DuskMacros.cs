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
    private void DuskShowMacros()
    {
        duskPageHost!.Children.Clear();

        var hints = new[]
        {
            ("X", "RECORD"),
            ("A", "SELECT"),
            ("B", "BACK")
        };

        var contentCanvas = new Canvas { Width = 1040, Height = 580 };

        // Left list (w: 580)
        var leftStack = new StackPanel { Width = 580, Orientation = Orientation.Vertical };

        leftStack.Children.Add(new TextBlock
        {
            Text = "RECORDED SEQUENCES",
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

        var macroSlots = new (string Name, string Steps, string Trigger, bool Active)[]
        {
            ("M1 Combo Sequence", "4 actions · 180ms", "Paddle M1", true),
            ("M2 Rapid Evade", "3 actions · 95ms", "Paddle M2", false),
            ("M3 Reload & Slide", "5 actions · 240ms", "Unassigned", false),
            ("M4 Jump Shot", "2 actions · 60ms", "Unassigned", false)
        };

        foreach (var (name, steps, trigger, active) in macroSlots)
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
                Text = steps,
                FontFamily = DuskKit.Body,
                FontSize = 13,
                Foreground = DuskKit.MuteBrush,
                Margin = new Thickness(0, 2, 0, 0)
            });
            Grid.SetColumn(nameStack, 0);
            grid.Children.Add(nameStack);

            var trigPill = new Border
            {
                Padding = new Thickness(14, 6, 14, 6),
                CornerRadius = new CornerRadius(14),
                Background = active ? DuskKit.B("#1a9fff") : DuskKit.SlateItemBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            trigPill.Child = new TextBlock
            {
                Text = trigger,
                FontFamily = DuskKit.Display,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = active ? DuskKit.WhiteBrush : DuskKit.LightBrush
            };
            Grid.SetColumn(trigPill, 1);
            grid.Children.Add(trigPill);

            row.Child = grid;
            leftStack.Children.Add(row);
        }

        Canvas.SetLeft(leftStack, 0);
        Canvas.SetTop(leftStack, 0);
        contentCanvas.Children.Add(leftStack);

        // Right side: Timeline sequence visual
        var rightStack = new StackPanel { Width = 380, Orientation = Orientation.Vertical };

        var timeCard = new Border
        {
            Width = 360,
            CornerRadius = new CornerRadius(6),
            Background = DuskKit.B("#141920"),
            BorderBrush = DuskKit.B("rgba(26,159,255,0.35)"),
            BorderThickness = new Thickness(1, 2, 1, 1),
            Padding = new Thickness(20),
            Effect = new DropShadowEffect { Color = DuskKit.CyanNeon, BlurRadius = 14, ShadowDepth = 0, Opacity = 0.2 }
        };
        var timeStack = new StackPanel { Orientation = Orientation.Vertical };

        var timeHead = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 14) };
        timeHead.Children.Add(new TextBlock
        {
            Text = "SEQUENCE TIMELINE",
            FontFamily = DuskKit.Display,
            FontSize = 13,
            FontWeight = FontWeights.ExtraBold,
            Foreground = DuskKit.MuteBrush
        });
        var loopChip = new Border
        {
            Padding = new Thickness(8, 2, 8, 2),
            CornerRadius = new CornerRadius(3),
            Background = DuskKit.B("rgba(89,191,64,0.18)"),
            BorderBrush = DuskKit.B("rgba(89,191,64,0.4)"),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(12, 0, 0, 0),
            Child = new TextBlock
            {
                Text = "LOOP READY · 180MS",
                FontFamily = DuskKit.Display,
                FontSize = 10,
                FontWeight = FontWeights.ExtraBold,
                Foreground = DuskKit.GreenOnlineBrush
            }
        };
        timeHead.Children.Add(loopChip);
        timeStack.Children.Add(timeHead);

        // Sequence blocks with luminous dropshadows
        var blockRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 16) };
        var seqBlocks = new[] { ("X", DuskKit.CyanNeon), ("RB", DuskKit.GreenOnline), ("A", DuskKit.CyanNeon) };
        for (int i = 0; i < seqBlocks.Length; i++)
        {
            var (bName, bCol) = seqBlocks[i];
            var block = new Border
            {
                Padding = new Thickness(14, 8, 14, 8),
                CornerRadius = new CornerRadius(4),
                Background = DuskKit.B(bCol),
                Effect = new DropShadowEffect { Color = bCol, BlurRadius = 10, ShadowDepth = 0, Opacity = 0.7 },
                Child = new TextBlock
                {
                    Text = bName,
                    FontFamily = DuskKit.Display,
                    FontSize = 15,
                    FontWeight = FontWeights.ExtraBold,
                    Foreground = DuskKit.SlateBgBrush
                }
            };
            blockRow.Children.Add(block);

            if (i < seqBlocks.Length - 1)
            {
                var arrow = new TextBlock
                {
                    Text = "→ 45ms →",
                    FontFamily = DuskKit.Display,
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = DuskKit.MuteBrush,
                    Margin = new Thickness(8, 0, 8, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                blockRow.Children.Add(arrow);
            }
        }
        timeStack.Children.Add(blockRow);

        // Timeline ruler with active playhead needle
        var ruler = new Canvas { Width = 320, Height = 36 };
        var rLine = new Line { X1 = 0, Y1 = 15, X2 = 320, Y2 = 15, Stroke = DuskKit.B("#3d4450"), StrokeThickness = 2 };
        ruler.Children.Add(rLine);
        for (int x = 0; x <= 320; x += 80)
        {
            var tick = new Line { X1 = x, Y1 = 10, X2 = x, Y2 = 20, Stroke = DuskKit.B("#8b929a"), StrokeThickness = 1.5 };
            ruler.Children.Add(tick);
            var lbl = new TextBlock { Text = $"{x / 2}ms", FontFamily = DuskKit.Display, FontSize = 10, Foreground = DuskKit.MuteBrush };
            Canvas.SetLeft(lbl, Math.Max(0, x - 10));
            Canvas.SetTop(lbl, 22);
            ruler.Children.Add(lbl);
        }

        // Radiant playhead needle at X = 135
        var needle = new Line { X1 = 135, Y1 = 2, X2 = 135, Y2 = 28, Stroke = DuskKit.CyanNeonBrush, StrokeThickness = 2, Effect = new DropShadowEffect { Color = DuskKit.CyanNeon, BlurRadius = 6, ShadowDepth = 0, Opacity = 0.9 } };
        var needleHead = new Polygon { Points = new PointCollection { new Point(130, 0), new Point(140, 0), new Point(135, 6) }, Fill = DuskKit.CyanNeonBrush };
        ruler.Children.Add(needle);
        ruler.Children.Add(needleHead);

        timeStack.Children.Add(ruler);
        timeCard.Child = timeStack;
        rightStack.Children.Add(timeCard);

        // Detail card with cyan specular border
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
            Text = "EXECUTION MODE",
            FontFamily = DuskKit.Display,
            FontSize = 13,
            FontWeight = FontWeights.ExtraBold,
            Foreground = DuskKit.MuteBrush,
            Margin = new Thickness(0, 0, 0, 4)
        });
        detailStack.Children.Add(new TextBlock
        {
            Text = "Hold to Loop · 180ms Cycle",
            FontFamily = DuskKit.Display,
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            Foreground = DuskKit.WhiteBrush,
            Margin = new Thickness(0, 0, 0, 4)
        });
        detailStack.Children.Add(new TextBlock
        {
            Text = "Fires sequence continuously while M1 paddle remains held, then releases all inputs cleanly.",
            FontFamily = DuskKit.Body,
            FontSize = 14,
            Foreground = DuskKit.LightBrush,
            TextWrapping = TextWrapping.Wrap
        });
        detailCard.Child = detailStack;
        rightStack.Children.Add(detailCard);

        // Action button to test macro
        var testMacroBtn = DuskKit.GreenActionButton("Test Sequence", 360, 48, () => { });
        testMacroBtn.Margin = new Thickness(0, 12, 0, 0);
        rightStack.Children.Add(testMacroBtn);

        Canvas.SetLeft(rightStack, 640);
        Canvas.SetTop(rightStack, 0);
        contentCanvas.Children.Add(rightStack);

        var shell = DuskKit.SettingsShell(
            activeKey: "macros",
            title: "Macros & Sequences",
            lead: "Sequence timing, delay calibration, and trigger binding combinations.",
            content: contentCanvas,
            hints: hints,
            onNav: key => DuskSelectPage(key)
        );

        duskPageHost.Children.Add(shell);
    }
}
