using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Hybrid;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private void HybridShowMacros()
    {
        hybridPageHost!.Children.Clear();

        var hints = new[]
        {
            ("A", "Edit step"),
            ("X", "Record"),
            ("Y", "Preview"),
            ("B", "Back")
        };

        var rails = HybridKit.HandheldRails(
            curPage: "macros",
            hints: hints,
            title: "Macros",
            lead: "LOCAL DRAFT · Not written to controller memory.",
            onNav: key => HybridSelectPage(key)
        );
        hybridPageHost.Children.Add(rails);

        // Sequence header at (216, 160)
        var seqHeader = new TextBlock
        {
            Text = "SEQUENCE · 4 EVENTS",
            FontFamily = HybridKit.Body,
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            Foreground = HybridKit.MuteBrush
        };
        Canvas.SetLeft(seqHeader, 216);
        Canvas.SetTop(seqHeader, 160);
        hybridPageHost.Children.Add(seqHeader);

        // Snap block chain (.blk) across (216, 196)
        var chainPanel = new StackPanel { Orientation = Orientation.Horizontal };
        var events = new[]
        {
            ("Press A", "A", false),
            ("Wait 50ms", "50", true),
            ("Press B", "B", false),
            ("Release", "—", false)
        };

        for (int i = 0; i < events.Length; i++)
        {
            var (eName, eKey, on) = events[i];
            var block = new Border
            {
                Width = 262,
                Height = 92,
                CornerRadius = new CornerRadius(16),
                Background = on ? HybridKit.LavenderBrush : HybridKit.B("#2b2831"),
                Margin = new Thickness(0, 0, 8, 0),
                Padding = new Thickness(26, 0, 18, 0)
            };
            if (on)
            {
                block.BorderBrush = HybridKit.CyanBrush;
                block.BorderThickness = new Thickness(2);
                block.Effect = new DropShadowEffect { Color = HybridKit.Cyan, BlurRadius = 12, ShadowDepth = 0 };
            }

            var bGrid = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var cap = new Border
            {
                Width = 40,
                Height = 40,
                CornerRadius = new CornerRadius(20),
                Background = on ? HybridKit.InkBrush : HybridKit.B("#3a3542"),
                Margin = new Thickness(0, 0, 14, 0),
                Child = new TextBlock
                {
                    Text = eKey,
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 17,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            bGrid.Children.Add(cap);

            var tStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            tStack.Children.Add(new TextBlock
            {
                Text = eName,
                FontFamily = HybridKit.Display,
                FontSize = 15,
                FontWeight = FontWeights.ExtraBold,
                Foreground = on ? HybridKit.InkBrush : Brushes.White
            });
            tStack.Children.Add(new TextBlock
            {
                Text = "Step duration",
                FontFamily = HybridKit.Body,
                FontSize = 13,
                Foreground = on ? HybridKit.B("#5a4b6e") : HybridKit.MuteBrush
            });
            bGrid.Children.Add(tStack);

            block.Child = bGrid;
            chainPanel.Children.Add(block);
        }

        Canvas.SetLeft(chainPanel, 216);
        Canvas.SetTop(chainPanel, 196);
        hybridPageHost.Children.Add(chainPanel);

        // Timeline visualization canvas at (216, 330, w: 640, h: 150)
        var timeline = new Border
        {
            Width = 640,
            Height = 150,
            CornerRadius = new CornerRadius(16),
            Background = HybridKit.B("#1c1922"),
            BorderBrush = HybridKit.B("#2c2834"),
            BorderThickness = new Thickness(1)
        };
        var tlCanvas = new Canvas { Width = 640, Height = 150 };
        for (int x = 40; x < 600; x += 60)
        {
            tlCanvas.Children.Add(new Line { X1 = x, Y1 = 0, X2 = x, Y2 = 150, Stroke = HybridKit.B("#2c2834"), StrokeThickness = 1 });
        }
        var bar1 = new Border { Width = 90, Height = 36, CornerRadius = new CornerRadius(6), Background = HybridKit.MauveBrush };
        Canvas.SetLeft(bar1, 40); Canvas.SetTop(bar1, 30); tlCanvas.Children.Add(bar1);
        var bar2 = new Border { Width = 60, Height = 36, CornerRadius = new CornerRadius(6), Background = HybridKit.LavenderBrush };
        Canvas.SetLeft(bar2, 160); Canvas.SetTop(bar2, 30); tlCanvas.Children.Add(bar2);
        var bar3 = new Border { Width = 110, Height = 36, CornerRadius = new CornerRadius(6), Background = HybridKit.MauveBrush };
        Canvas.SetLeft(bar3, 250); Canvas.SetTop(bar3, 30); tlCanvas.Children.Add(bar3);
        timeline.Child = tlCanvas;

        Canvas.SetLeft(timeline, 216);
        Canvas.SetTop(timeline, 330);
        hybridPageHost.Children.Add(timeline);

        // Controls below timeline at (216, 506)
        var ctrlPanel = new StackPanel { Width = 640 };
        var triggerRow = new Border
        {
            Height = 58,
            CornerRadius = new CornerRadius(16),
            Background = HybridKit.B("#1f1c25"),
            Padding = new Thickness(22, 0, 20, 0),
            Child = new Grid
            {
                Children =
                {
                    new TextBlock { Text = "Played by paddle", FontFamily = HybridKit.Display, FontSize = 18, FontWeight = FontWeights.SemiBold, Foreground = HybridKit.WhiteBrush, VerticalAlignment = VerticalAlignment.Center },
                    new Border
                    {
                        Height = 36, CornerRadius = new CornerRadius(18), Background = HybridKit.B("#302c38"), Padding = new Thickness(16, 0, 14, 0),
                        HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center,
                        Child = new TextBlock { Text = "M1 Paddle  \uE70D", FontFamily = HybridKit.Body, FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = HybridKit.WhiteBrush, VerticalAlignment = VerticalAlignment.Center }
                    }
                }
            }
        };
        ctrlPanel.Children.Add(triggerRow);

        var btnRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 14, 0, 0) };
        var tools = new[] { ("\uE7C8", "Record"), ("\uE896", "Clear"), ("\uE768", "Play") };
        foreach (var (icon, label) in tools)
        {
            var btn = new Border
            {
                Height = 44,
                CornerRadius = new CornerRadius(22),
                Background = HybridKit.B("#302c38"),
                Padding = new Thickness(22, 0, 22, 0),
                Margin = new Thickness(0, 0, 10, 0),
                Cursor = System.Windows.Input.Cursors.Hand,
                Child = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Center,
                    Children =
                    {
                        new TextBlock { Text = icon, FontFamily = HybridKit.Icons, FontSize = 15, Foreground = HybridKit.WhiteBrush, Margin = new Thickness(0, 0, 8, 0) },
                        new TextBlock { Text = label, FontFamily = HybridKit.Display, FontSize = 15, FontWeight = FontWeights.Bold, Foreground = HybridKit.WhiteBrush }
                    }
                }
            };
            btnRow.Children.Add(btn);
        }
        ctrlPanel.Children.Add(btnRow);

        Canvas.SetLeft(ctrlPanel, 216);
        Canvas.SetTop(ctrlPanel, 506);
        hybridPageHost.Children.Add(ctrlPanel);

        // Seam at x=888
        var seam = HybridKit.Seam(888, 320, 830, 560, locked: true);
        hybridPageHost.Children.Add(seam);

        // Detail card on right at (930, 330, w: 454, h: 494)
        var detailCard = new Border
        {
            Width = 454,
            Height = 494,
            CornerRadius = new CornerRadius(26),
            Background = HybridKit.B("#1f1c25"),
            BorderBrush = HybridKit.B("#302c39"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(28, 24, 28, 24)
        };
        var dStack = new StackPanel();
        dStack.Children.Add(new TextBlock { Text = "Step 2: Wait 50ms", FontFamily = HybridKit.Display, FontSize = 24, FontWeight = FontWeights.ExtraBold, Foreground = HybridKit.WhiteBrush });
        dStack.Children.Add(new TextBlock { Text = "Pause execution before sending the next command.", FontFamily = HybridKit.Body, FontSize = 15, Foreground = HybridKit.MuteBrush, Margin = new Thickness(0, 4, 0, 16) });

        var segStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 20) };
        foreach (var dur in new[] { "25ms", "50ms", "100ms", "250ms" })
        {
            bool sel = dur == "50ms";
            var chip = new Border
            {
                Height = 34,
                CornerRadius = new CornerRadius(17),
                Padding = new Thickness(16, 0, 16, 0),
                Background = sel ? HybridKit.LavenderBrush : HybridKit.B("#2b2831"),
                Margin = new Thickness(0, 0, 6, 0),
                Child = new TextBlock { Text = dur, FontFamily = HybridKit.Body, FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = sel ? HybridKit.InkBrush : HybridKit.WhiteBrush, VerticalAlignment = VerticalAlignment.Center }
            };
            segStack.Children.Add(chip);
        }
        dStack.Children.Add(segStack);

        var aCircle = new Border
        {
            Width = 54,
            Height = 54,
            CornerRadius = new CornerRadius(27),
            Background = HybridKit.B("#3a3542"),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 22, 0, 14),
            Child = new TextBlock { Text = "A", FontFamily = new FontFamily("Segoe UI"), FontSize = 22, FontWeight = FontWeights.Bold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
        };
        dStack.Children.Add(aCircle);

        dStack.Children.Add(new TextBlock
        {
            Text = "Pressing A initiates the primary action sequence.",
            FontFamily = HybridKit.Body,
            FontSize = 15,
            Foreground = HybridKit.MuteBrush,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center
        });

        detailCard.Child = dStack;
        Canvas.SetLeft(detailCard, 930);
        Canvas.SetTop(detailCard, 330);
        hybridPageHost.Children.Add(detailCard);
    }
}
