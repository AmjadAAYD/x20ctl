using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace X20Ctl.Product.Nexus;

/// <summary>
/// Standard Nexus Frame:
/// Background gradient linear-gradient(180deg,#1a1c20,#101113), top bar,
/// display title at (68, 138), lead subtitle at (70, 192), optional tabs at (68, 200),
/// body host canvas, and bottom-right hints at (right: 62, bottom: 32).
/// Strictly implements t_nexus.py:216 Nexus.frame().
/// </summary>
public static class NexusToolFrame
{
    public static Grid WrapFrame(
        string title,
        string? lead,
        Canvas bodyStage,
        IEnumerable<(string Key, string Label)>? hints = null,
        string[]? tabs = null,
        string? currentTab = null,
        Action<string>? onTabSelect = null,
        Action? onBack = null)
    {
        var grid = new Grid { Width = 1600, Height = 900, ClipToBounds = true };
        var canvas = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };

        // 1. Background gradient
        var bg = new Rectangle
        {
            Width = 1600,
            Height = 900,
            Fill = new LinearGradientBrush
            {
                StartPoint = new Point(0.5, 0),
                EndPoint = new Point(0.5, 1),
                GradientStops =
                {
                    new(NexusKit.C("#1a1c20"), 0.0),
                    new(NexusKit.House, 1.0)
                }
            }
        };
        canvas.Children.Add(bg);

        // 2. Top bar at (left: 62, right: 62, top: 46)
        var topBar = NexusKit.TopBar(onTools: onBack);
        Canvas.SetLeft(topBar, 62);
        Canvas.SetTop(topBar, 46);
        canvas.Children.Add(topBar);

        // 3. Title at (68, 138) in Segoe UI Variable Display 38px
        if (!string.IsNullOrEmpty(title))
        {
            var titleEl = new TextBlock
            {
                Text = title,
                FontFamily = NexusKit.Display,
                FontSize = 38,
                FontWeight = FontWeights.SemiBold,
                Foreground = NexusKit.WhiteBrush
            };
            Canvas.SetLeft(titleEl, 68);
            Canvas.SetTop(titleEl, 138);
            canvas.Children.Add(titleEl);
        }

        // 4. Lead subtitle at (70, 192) in Segoe UI Variable Text 17px
        if (!string.IsNullOrEmpty(lead))
        {
            var leadEl = new TextBlock
            {
                Text = lead,
                FontFamily = NexusKit.Body,
                FontSize = 17,
                Foreground = NexusKit.SubBrush,
                MaxWidth = 1440,
                TextWrapping = TextWrapping.Wrap
            };
            Canvas.SetLeft(leadEl, 70);
            Canvas.SetTop(leadEl, 192);
            canvas.Children.Add(leadEl);
        }

        // 5. Tabs at (68, 200)
        if (tabs != null && tabs.Length > 0)
        {
            var tabStack = new StackPanel { Orientation = Orientation.Horizontal };
            foreach (var t in tabs)
            {
                bool on = string.Equals(t, currentTab, StringComparison.OrdinalIgnoreCase);
                var tabBorder = new Border
                {
                    Padding = new Thickness(18, 8, 18, 8),
                    Margin = new Thickness(0, 0, 10, 0),
                    CornerRadius = new CornerRadius(4),
                    Background = on ? NexusKit.WhiteBrush : NexusKit.TileBrush,
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Child = new TextBlock
                    {
                        Text = t,
                        FontFamily = NexusKit.Body,
                        FontSize = 16,
                        FontWeight = on ? FontWeights.SemiBold : FontWeights.Normal,
                        Foreground = on ? NexusKit.B("#111111") : NexusKit.WhiteBrush
                    }
                };
                if (onTabSelect != null)
                {
                    string tabName = t;
                    tabBorder.MouseLeftButtonDown += (_, _) => onTabSelect(tabName);
                }
                tabStack.Children.Add(tabBorder);
            }
            Canvas.SetLeft(tabStack, 68);
            Canvas.SetTop(tabStack, 200);
            canvas.Children.Add(tabStack);
        }

        // 6. Body Stage Host
        canvas.Children.Add(bodyStage);

        // 7. Hints bar at bottom right (right: 62, bottom: 32)
        if (hints != null)
        {
            var hintsEl = NexusKit.HintsBar(hints);
            Canvas.SetRight(hintsEl, 62);
            Canvas.SetBottom(hintsEl, 32);
            canvas.Children.Add(hintsEl);
        }

        grid.Children.Add(canvas);
        return grid;
    }
}
