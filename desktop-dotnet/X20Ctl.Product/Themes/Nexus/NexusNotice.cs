using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace X20Ctl.Product.Nexus;

/// <summary>
/// Themed Modal Notices & Toasts for the Nexus theme:
/// Left Xbox Guide modal dialog at (150, 170, w=760) with primary button green border (#6cc644),
/// and stacked toast notifications at (980, 170, w=560) with colored status circles.
/// Strictly implements t_nexus.py:310 notices().
/// </summary>
public static class NexusNotice
{
    public static Grid BuildNotice(
        string title,
        string[] bodyLines,
        string primaryAction,
        string cancelAction,
        Action onPrimary,
        Action onCancel,
        IEnumerable<(string Label, string Message, string Icon, string ColorHex)>? toasts = null)
    {
        var grid = new Grid { Width = 1600, Height = 900, ClipToBounds = true };

        // 1. Dimmed background: dashboard backdrop + gradient overlay matching t_nexus.py:311
        var thumb = NexusKit.ThemeThumb("nexus", 1600, 900);
        grid.Children.Add(thumb);

        var dimRec = new Rectangle
        {
            Width = 1600,
            Height = 900,
            Fill = new LinearGradientBrush
            {
                StartPoint = new Point(0.5, 1.0),
                EndPoint = new Point(0.5, 0.0),
                GradientStops =
                {
                    new(Color.FromArgb((byte)(255 * 0.97), 10, 11, 13), 0.0),
                    new(Color.FromArgb((byte)(255 * 0.72), 10, 11, 13), 0.18),
                    new(Color.FromArgb((byte)(255 * 0.72), 10, 11, 13), 1.0)
                }
            }
        };
        grid.Children.Add(dimRec);

        var canvas = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };

        // 2. Left Guide dialog at (150, 170, w = 760)
        var dialog = new Border
        {
            Width = 760,
            Padding = new Thickness(40, 38, 40, 38),
            CornerRadius = new CornerRadius(8),
            Background = NexusKit.B(Color.FromArgb(0xf7, 0x16, 0x18, 0x1b)),
            Effect = new DropShadowEffect
            {
                BlurRadius = 80,
                ShadowDepth = 30,
                Direction = 270,
                Opacity = 0.6,
                Color = Colors.Black
            }
        };

        var dialogStack = new StackPanel();
        dialogStack.Children.Add(new TextBlock
        {
            Text = title,
            FontFamily = NexusKit.Display,
            FontSize = 28,
            FontWeight = FontWeights.SemiBold,
            Foreground = NexusKit.WhiteBrush
        });

        var bodyStack = new StackPanel { Margin = new Thickness(0, 18, 0, 0) };
        for (int i = 0; i < bodyLines.Length; i++)
        {
            string line = bodyLines[i];
            if (i is 1 or 2)
            {
                var checkRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };
                checkRow.Children.Add(new TextBlock
                {
                    Text = "\uE73E",
                    FontFamily = NexusKit.Icons,
                    FontSize = 15,
                    Foreground = NexusKit.GreenBrush,
                    Margin = new Thickness(0, 2, 10, 0)
                });
                checkRow.Children.Add(new TextBlock
                {
                    Text = line,
                    FontFamily = NexusKit.Body,
                    FontSize = 17,
                    Foreground = NexusKit.WhiteBrush
                });
                bodyStack.Children.Add(checkRow);
            }
            else if (i == 3)
            {
                bodyStack.Children.Add(new TextBlock
                {
                    Text = line,
                    FontFamily = NexusKit.Body,
                    FontSize = 16,
                    Foreground = NexusKit.SubBrush,
                    Margin = new Thickness(0, 6, 0, 6),
                    TextWrapping = TextWrapping.Wrap
                });
            }
            else
            {
                bodyStack.Children.Add(new TextBlock
                {
                    Text = line,
                    FontFamily = NexusKit.Body,
                    FontSize = i == 4 ? 19 : 17,
                    FontWeight = i == 4 ? FontWeights.SemiBold : FontWeights.Normal,
                    Foreground = NexusKit.WhiteBrush,
                    Margin = new Thickness(0, 4, 0, 4),
                    TextWrapping = TextWrapping.Wrap
                });
            }
        }
        dialogStack.Children.Add(bodyStack);

        // Buttons Stack
        var btnStack = new StackPanel { Width = 320, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 26, 0, 0) };
        var yesBtn = NexusKit.RowButton(primaryAction, null, null, focused: true, onClick: null, width: 320);
        var noBtn = NexusKit.RowButton(cancelAction, null, null, focused: false, onClick: null, width: 320);
        yesBtn.Tag = "notice:yes";
        btnStack.Children.Add(StudioScene.ThemeAction(yesBtn, "notice:yes", onPrimary));
        noBtn.Tag = "notice:no";
        btnStack.Children.Add(StudioScene.ThemeAction(noBtn, "notice:no", onCancel));
        dialogStack.Children.Add(btnStack);

        dialog.Child = dialogStack;
        Canvas.SetLeft(dialog, 150);
        Canvas.SetTop(dialog, 170);
        canvas.Children.Add(dialog);

        // 3. Right Toasts Stack at (980, 170, w = 560)
        var defaultToasts = toasts ?? new[]
        {
            ("Update", "Driver setup · ViGEmBus and HidHide ready to install", "\uE777", "#107c10"),
            ("While it installs", "Windows may ask for administrator approval", "\uE896", "#2d6fd0"),
            ("After Yes", "Ready for any game · Virtual controller active", "\uE73E", "#107c10"),
            ("After No", "Virtual controller disabled · Local mode only", "\uE711", "#5a5d63")
        };

        var toastStack = new StackPanel { Width = 560 };
        foreach (var (lab, txt, ic, col) in defaultToasts)
        {
            toastStack.Children.Add(new TextBlock
            {
                Text = lab,
                FontFamily = NexusKit.Body,
                FontSize = 14,
                Foreground = NexusKit.SubBrush,
                Margin = new Thickness(0, 8, 0, 4)
            });

            var toast = new Border
            {
                CornerRadius = new CornerRadius(28),
                Background = NexusKit.InputBrush,
                Padding = new Thickness(14, 14, 22, 14),
                Effect = new DropShadowEffect { BlurRadius = 40, ShadowDepth = 14, Direction = 270, Opacity = 0.5, Color = Colors.Black }
            };
            var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var iconCircle = new Border
            {
                Width = 48,
                Height = 48,
                CornerRadius = new CornerRadius(24),
                Background = NexusKit.B(col),
                Margin = new Thickness(0, 0, 16, 0),
                Child = new TextBlock
                {
                    Text = ic,
                    FontFamily = NexusKit.Icons,
                    FontSize = 20,
                    Foreground = NexusKit.WhiteBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            row.Children.Add(iconCircle);

            row.Children.Add(new TextBlock
            {
                Text = txt,
                FontFamily = NexusKit.Body,
                FontSize = 16,
                Foreground = NexusKit.WhiteBrush,
                VerticalAlignment = VerticalAlignment.Center,
                MaxWidth = 450,
                TextWrapping = TextWrapping.Wrap
            });

            toast.Child = row;
            toastStack.Children.Add(toast);
        }

        Canvas.SetLeft(toastStack, 980);
        Canvas.SetTop(toastStack, 170);
        canvas.Children.Add(toastStack);

        // 4. Hints at bottom right
        var hints = NexusKit.HintsBar(new[] { ("A", "Select"), ("B", "Back") });
        Canvas.SetRight(hints, 62);
        Canvas.SetBottom(hints, 32);
        canvas.Children.Add(hints);

        grid.Children.Add(canvas);
        return grid;
    }
}
