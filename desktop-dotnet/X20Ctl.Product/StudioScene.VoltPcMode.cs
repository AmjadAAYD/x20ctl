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
    /// <summary>
    /// PC Mode workbench for Volt: Protocol routing, ViGEmBus status, and virtual peripheral management.
    /// Strictly implements t_volt.py:253 Volt and models.py:pcmode.
    /// </summary>
    private UIElement BuildVoltPcMode()
    {
        var content = new Canvas { Width = 970, Height = 560 };

        // 1. Left List: Protocol and routing options (width: 520)
        var leftStack = new StackPanel { Orientation = Orientation.Vertical, Width = 520 };

        leftStack.Children.Add(new TextBlock
        {
            Text = "PROTOCOL & ROUTING",
            FontFamily = VoltKit.Display,
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = VoltKit.B("#8a8a8a"),
            Margin = new Thickness(0, 0, 0, 8)
        });

        var rows = new (string Label, string? Sub, string CtlType, string CtlVal, bool Active)[]
        {
            ("Virtual Controller Routing", "Emulate XInput through ViGEmBus", "toggle", "true", false),
            ("Settings Profile", null, "dd", "Player 1", false),
            ("Rapid Fire (Turbo)", null, "val", "12 shots/sec", false),
            ("Direct Native HID", "Bypass virtualization layer", "chev", "", false),
            ("Hardware I/O Calibration", null, "chev", "", false),
            ("Before enabling virtual mode", "Important safety requirements", "chev", "", true)
        };

        foreach (var (lbl, sub, ctlType, ctlVal, active) in rows)
        {
            var row = new Border
            {
                MinHeight = 62,
                Background = active ? VoltKit.B("#2a2a2a") : Brushes.Transparent,
                BorderBrush = active ? VoltKit.AmberHotBrush : VoltKit.B("#222222"),
                BorderThickness = new Thickness(active ? 2 : 0, active ? 2 : 0, active ? 2 : 0, active ? 2 : 1),
                CornerRadius = active ? new CornerRadius(12) : new CornerRadius(0),
                Padding = new Thickness(16, 10, 16, 10),
                Margin = new Thickness(0, 0, 0, 2),
                Cursor = Cursors.Hand
            };

            var grid = new Grid { VerticalAlignment = VerticalAlignment.Center };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var lCol = new StackPanel { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center };
            lCol.Children.Add(new TextBlock
            {
                Text = lbl,
                FontFamily = VoltKit.Display,
                FontSize = 20,
                FontWeight = FontWeights.SemiBold,
                Foreground = active ? VoltKit.WhiteBrush : VoltKit.LightBrush
            });
            if (!string.IsNullOrEmpty(sub))
            {
                lCol.Children.Add(new TextBlock
                {
                    Text = sub,
                    FontFamily = VoltKit.Body,
                    FontSize = 14,
                    Foreground = VoltKit.MuteBrush,
                    Margin = new Thickness(0, 2, 0, 0)
                });
            }
            Grid.SetColumn(lCol, 0);
            grid.Children.Add(lCol);

            // Right control element
            UIElement? ctrl = null;
            if (ctlType == "toggle")
            {
                var tg = new Border
                {
                    Width = 54,
                    Height = 30,
                    CornerRadius = new CornerRadius(15),
                    Background = VoltKit.AmberHotBrush,
                    VerticalAlignment = VerticalAlignment.Center
                };
                var knob = new Ellipse
                {
                    Width = 24,
                    Height = 24,
                    Fill = VoltKit.WhiteBrush,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 0, 3, 0)
                };
                tg.Child = knob;
                ctrl = tg;
            }
            else if (ctlType == "dd")
            {
                ctrl = new Border
                {
                    MinWidth = 160,
                    Height = 42,
                    CornerRadius = new CornerRadius(8),
                    Background = VoltKit.B("#262626"),
                    Padding = new Thickness(14, 0, 14, 0),
                    Child = new Grid
                    {
                        Children =
                        {
                            new TextBlock
                            {
                                Text = ctlVal,
                                FontFamily = VoltKit.Display,
                                FontSize = 16,
                                FontWeight = FontWeights.SemiBold,
                                Foreground = VoltKit.WhiteBrush,
                                HorizontalAlignment = HorizontalAlignment.Left,
                                VerticalAlignment = VerticalAlignment.Center
                            },
                            new TextBlock
                            {
                                Text = "\uE70D",
                                FontFamily = VoltKit.Icons,
                                FontSize = 11,
                                Foreground = VoltKit.MuteBrush,
                                HorizontalAlignment = HorizontalAlignment.Right,
                                VerticalAlignment = VerticalAlignment.Center
                            }
                        }
                    }
                };
            }
            else if (ctlType == "val")
            {
                ctrl = new TextBlock
                {
                    Text = ctlVal,
                    FontFamily = VoltKit.Display,
                    FontSize = 18,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = VoltKit.AmberHotBrush,
                    VerticalAlignment = VerticalAlignment.Center
                };
            }
            else if (ctlType == "chev")
            {
                ctrl = new TextBlock
                {
                    Text = "\uE76C",
                    FontFamily = VoltKit.Icons,
                    FontSize = 14,
                    Foreground = VoltKit.MuteBrush,
                    VerticalAlignment = VerticalAlignment.Center
                };
            }

            if (ctrl != null)
            {
                Grid.SetColumn(ctrl, 1);
                grid.Children.Add(ctrl);
            }

            row.Child = grid;
            leftStack.Children.Add(row);
        }

        Canvas.SetLeft(leftStack, 0);
        Canvas.SetTop(leftStack, 10);
        content.Children.Add(leftStack);

        // 2. Right Details Card (width: 410 at x = 550)
        var rightStack = new StackPanel { Orientation = Orientation.Vertical, Width = 410 };

        // ViGEm Route Visual Diagram Box
        var routeBox = new Border
        {
            Width = 410,
            Height = 160,
            Background = VoltKit.B("#0b0b0b"),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(20)
        };
        var rCanvas = new Canvas { Width = 370, Height = 120 };

        // Diagram: [Dune Pad] ───► [ViGEmBus] ───► [Virtual X360]
        var padPill = new Border
        {
            Width = 96,
            Height = 50,
            CornerRadius = new CornerRadius(8),
            Background = VoltKit.B("#1f1f1f"),
            BorderBrush = VoltKit.AmberHotBrush,
            BorderThickness = new Thickness(1.5),
            Child = new TextBlock
            {
                Text = "DUNE PAD",
                FontFamily = VoltKit.Display,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = VoltKit.WhiteBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        Canvas.SetLeft(padPill, 10);
        Canvas.SetTop(padPill, 35);
        rCanvas.Children.Add(padPill);

        var arrow1 = new TextBlock
        {
            Text = "──►",
            FontFamily = VoltKit.Display,
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = VoltKit.AmberHotBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
        Canvas.SetLeft(arrow1, 116);
        Canvas.SetTop(arrow1, 50);
        rCanvas.Children.Add(arrow1);

        var busPill = new Border
        {
            Width = 100,
            Height = 50,
            CornerRadius = new CornerRadius(8),
            Background = VoltKit.AmberHotBrush,
            Child = new TextBlock
            {
                Text = "ViGEmBus\nv1.22.0",
                FontFamily = VoltKit.Display,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = VoltKit.B("#111111"),
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        Canvas.SetLeft(busPill, 156);
        Canvas.SetTop(busPill, 35);
        rCanvas.Children.Add(busPill);

        var arrow2 = new TextBlock
        {
            Text = "──►",
            FontFamily = VoltKit.Display,
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = VoltKit.AmberHotBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
        Canvas.SetLeft(arrow2, 266);
        Canvas.SetTop(arrow2, 50);
        rCanvas.Children.Add(arrow2);

        var outPill = new Border
        {
            Width = 84,
            Height = 50,
            CornerRadius = new CornerRadius(8),
            Background = VoltKit.B("#1f1f1f"),
            BorderBrush = VoltKit.B("#3b3b3b"),
            BorderThickness = new Thickness(1.5),
            Child = new TextBlock
            {
                Text = "X360 P1",
                FontFamily = VoltKit.Display,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = VoltKit.WhiteBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        Canvas.SetLeft(outPill, 300);
        Canvas.SetTop(outPill, 35);
        rCanvas.Children.Add(outPill);

        routeBox.Child = rCanvas;
        rightStack.Children.Add(routeBox);

        // Details card
        var card = new Border
        {
            Width = 410,
            Background = VoltKit.SheetDarkBrush,
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(24, 20, 24, 20),
            Margin = new Thickness(0, 16, 0, 0)
        };
        var cStack = new StackPanel { Orientation = Orientation.Vertical };

        cStack.Children.Add(new TextBlock
        {
            Text = "BEFORE ENABLING VIRTUAL MODE",
            FontFamily = VoltKit.Display,
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = VoltKit.WhiteBrush,
            Margin = new Thickness(0, 0, 0, 12)
        });

        var rules = new[]
        {
            "ViGEmBus creates a high-performance virtual Xbox 360 controller device with zero measurable translation latency.",
            "Direct physical inputs are seamlessly cloaked from games to prevent duplicate trigger and button registrations.",
            "Kernel-level anti-cheat engines (Easy Anti-Cheat, BattlEye) fully recognize ViGEmBus as a certified hardware peripheral."
        };

        for (int i = 0; i < rules.Length; i++)
        {
            var ruleRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            var num = new Border
            {
                Width = 26,
                Height = 26,
                CornerRadius = new CornerRadius(6),
                Background = VoltKit.AmberHotBrush,
                Margin = new Thickness(0, 0, 12, 0),
                Child = new TextBlock
                {
                    Text = (i + 1).ToString(),
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 13,
                    FontWeight = FontWeights.Bold,
                    Foreground = VoltKit.B("#111111"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            ruleRow.Children.Add(num);

            var ruleTxt = new TextBlock
            {
                Text = rules[i],
                FontFamily = VoltKit.Body,
                FontSize = 14,
                Foreground = VoltKit.B("#bdbdbd"),
                TextWrapping = TextWrapping.Wrap,
                Width = 320,
                LineHeight = 19
            };
            ruleRow.Children.Add(ruleTxt);
            cStack.Children.Add(ruleRow);
        }

        // Action button [▶ Agree & Enable Mode]
        var agreeBtn = VoltKit.OrangeActionButton("Agree & Enable Mode", 362, 54);
        agreeBtn.Margin = new Thickness(0, 10, 0, 0);
        cStack.Children.Add(agreeBtn);

        card.Child = cStack;
        rightStack.Children.Add(card);

        Canvas.SetLeft(rightStack, 550);
        Canvas.SetTop(rightStack, 10);
        content.Children.Add(rightStack);

        return VoltKit.TwoPaneShell("pcmode", "PC Mode", "Protocol routing, ViGEm virtual controller emulation, and system input mapping.", content, VoltSelectPage);
    }
}
