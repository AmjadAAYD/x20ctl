using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Volt;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private UIElement BuildVoltMacros()
    {
        var content = new Canvas { Width = 970, Height = 560 };

        // Slots on left (width: 440)
        var slotStack = new StackPanel { Orientation = Orientation.Vertical, Width = 440 };
        var slots = new (string Name, string Trig, int Actions, bool Active)[]
        {
            ("M1 Combo Sequence", "Paddle M1", 4, true),
            ("M2 Rapid Evade", "Paddle M2", 3, false),
            ("M3 Reload & Slide", "Unassigned", 2, false),
            ("M4 Jump Shot", "Unassigned", 2, false)
        };

        foreach (var (sName, sTrig, actCount, active) in slots)
        {
            var card = new Border
            {
                Height = 84,
                Background = active ? VoltKit.B("#2a2a2a") : VoltKit.B("#181818"),
                BorderBrush = active ? VoltKit.AmberHotBrush : VoltKit.B("#2b2b2b"),
                BorderThickness = new Thickness(active ? 2 : 1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(20, 0, 20, 0),
                Margin = new Thickness(0, 0, 0, 10),
                Cursor = Cursors.Hand
            };

            var rowGrid = new Grid { VerticalAlignment = VerticalAlignment.Center };
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });

            var infoStack = new StackPanel { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center };
            infoStack.Children.Add(new TextBlock
            {
                Text = sName,
                FontFamily = VoltKit.Display,
                FontSize = 19,
                FontWeight = FontWeights.SemiBold,
                Foreground = VoltKit.WhiteBrush
            });
            infoStack.Children.Add(new TextBlock
            {
                Text = $"{actCount} actions · Recorded sequence",
                FontFamily = VoltKit.Body,
                FontSize = 14,
                Foreground = VoltKit.MuteBrush,
                Margin = new Thickness(0, 3, 0, 0)
            });
            Grid.SetColumn(infoStack, 0);
            rowGrid.Children.Add(infoStack);

            var trigPill = new Border
            {
                Background = active ? VoltKit.AmberHotBrush : VoltKit.B("#252525"),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12, 4, 12, 4),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = sTrig,
                    FontFamily = VoltKit.Display,
                    FontSize = 13,
                    FontWeight = FontWeights.Bold,
                    Foreground = active ? VoltKit.B("#111111") : VoltKit.LightBrush
                }
            };
            Grid.SetColumn(trigPill, 1);
            rowGrid.Children.Add(trigPill);

            card.Child = rowGrid;
            slotStack.Children.Add(card);
        }
        Canvas.SetLeft(slotStack, 0);
        Canvas.SetTop(slotStack, 10);
        content.Children.Add(slotStack);

        // Sequence Timeline Box on right (width: 480)
        var timeBox = new Border
        {
            Width = 480,
            Height = 440,
            Background = VoltKit.B("#181818"),
            BorderBrush = VoltKit.B("#2b2b2b"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(24)
        };
        var tStack = new StackPanel { Orientation = Orientation.Vertical };
        tStack.Children.Add(new TextBlock
        {
            Text = "SEQUENCE TIMELINE",
            FontFamily = VoltKit.Display,
            FontSize = 15,
            FontWeight = FontWeights.Bold,
            Foreground = VoltKit.AmberHotBrush,
            Margin = new Thickness(0, 0, 0, 16)
        });

        // Timeline Action Blocks
        var blockRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 24) };
        var actBlocks = new[] { ("X", "45ms"), ("▸", "10ms"), ("RB", "60ms"), ("▸", "15ms"), ("A", "30ms") };
        foreach (var (txt, dur) in actBlocks)
        {
            if (txt == "▸")
            {
                blockRow.Children.Add(new TextBlock
                {
                    Text = "▸",
                    FontFamily = VoltKit.Display,
                    FontSize = 18,
                    Foreground = VoltKit.MuteBrush,
                    Margin = new Thickness(6, 12, 6, 0)
                });
            }
            else
            {
                var blk = new Border
                {
                    Background = VoltKit.B("#262626"),
                    BorderBrush = VoltKit.AmberHotBrush,
                    BorderThickness = new Thickness(1.5),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(14, 8, 14, 8),
                    Margin = new Thickness(0, 0, 4, 0)
                };
                var bCol = new StackPanel { Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Center };
                bCol.Children.Add(new TextBlock
                {
                    Text = txt,
                    FontFamily = VoltKit.Display,
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    Foreground = VoltKit.WhiteBrush,
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                bCol.Children.Add(new TextBlock
                {
                    Text = dur,
                    FontFamily = VoltKit.Body,
                    FontSize = 12,
                    Foreground = VoltKit.AmberHotBrush,
                    Margin = new Thickness(0, 2, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                blk.Child = bCol;
                blockRow.Children.Add(blk);
            }
        }
        tStack.Children.Add(blockRow);

        // Execution Mode
        tStack.Children.Add(new TextBlock
        {
            Text = "EXECUTION MODE",
            FontFamily = VoltKit.Display,
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = VoltKit.MuteBrush,
            Margin = new Thickness(0, 0, 0, 8)
        });
        var modeCard = new Border
        {
            Background = VoltKit.B("#222222"),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16, 12, 16, 12),
            Margin = new Thickness(0, 0, 0, 20)
        };
        var mCol = new StackPanel { Orientation = Orientation.Vertical };
        mCol.Children.Add(new TextBlock
        {
            Text = "Hold to Loop · 180ms Cycle",
            FontFamily = VoltKit.Display,
            FontSize = 17,
            FontWeight = FontWeights.SemiBold,
            Foreground = VoltKit.WhiteBrush
        });
        mCol.Children.Add(new TextBlock
        {
            Text = "Fires sequence continuously while M1 paddle remains held, then releases all inputs cleanly.",
            FontFamily = VoltKit.Body,
            FontSize = 13,
            Foreground = VoltKit.MuteBrush,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0)
        });
        modeCard.Child = mCol;
        tStack.Children.Add(modeCard);

        timeBox.Child = tStack;
        Canvas.SetLeft(timeBox, 480);
        Canvas.SetTop(timeBox, 10);
        content.Children.Add(timeBox);

        return VoltKit.TwoPaneShell("macros", "Macros & Sequences", "Sequence timing, delay calibration, and trigger binding combinations.", content, VoltSelectPage);
    }
}
