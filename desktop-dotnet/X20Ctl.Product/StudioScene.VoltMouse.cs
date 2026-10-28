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
    /// Mouse workbench for Volt: Gyroscope air mouse emulation, cursor sensitivity, and button mapping options.
    /// Strictly implements t_volt.py:253 Volt and models.py:mouse.
    /// </summary>
    private UIElement BuildVoltMouse()
    {
        var content = new Canvas { Width = 970, Height = 560 };

        // 1. Left List: Mouse options and button mappings (width: 520)
        var leftStack = new StackPanel { Orientation = Orientation.Vertical, Width = 520 };

        leftStack.Children.Add(new TextBlock
        {
            Text = "MOUSE EMULATION SETTINGS",
            FontFamily = VoltKit.Display,
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = VoltKit.B("#8a8a8a"),
            Margin = new Thickness(0, 0, 0, 8)
        });

        // Top controls: Toggle & Tracking source
        var topRows = new (string Label, string CtlType, string CtlVal)[]
        {
            ("Mouse Emulation Mode", "toggle", "false"),
            ("Tracking Source Input", "dd", "Right Stick + Gyro Assist")
        };

        foreach (var (lbl, ctlType, ctlVal) in topRows)
        {
            var row = new Border
            {
                MinHeight = 56,
                Background = Brushes.Transparent,
                BorderBrush = VoltKit.B("#222222"),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(16, 6, 16, 6),
                Cursor = Cursors.Hand
            };
            var grid = new Grid { VerticalAlignment = VerticalAlignment.Center };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var label = new TextBlock
            {
                Text = lbl,
                FontFamily = VoltKit.Display,
                FontSize = 19,
                FontWeight = FontWeights.SemiBold,
                Foreground = VoltKit.LightBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(label, 0);
            grid.Children.Add(label);

            UIElement? ctrl = null;
            if (ctlType == "toggle")
            {
                var tg = new Border
                {
                    Width = 54,
                    Height = 30,
                    CornerRadius = new CornerRadius(15),
                    Background = VoltKit.BorderDarkBrush,
                    VerticalAlignment = VerticalAlignment.Center
                };
                var knob = new Ellipse
                {
                    Width = 24,
                    Height = 24,
                    Fill = VoltKit.WhiteBrush,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(3, 0, 0, 0)
                };
                tg.Child = knob;
                ctrl = tg;
            }
            else if (ctlType == "dd")
            {
                ctrl = new Border
                {
                    MinWidth = 200,
                    Height = 40,
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
                                FontSize = 15,
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

            if (ctrl != null)
            {
                Grid.SetColumn(ctrl, 1);
                grid.Children.Add(ctrl);
            }

            row.Child = grid;
            leftStack.Children.Add(row);
        }

        leftStack.Children.Add(new TextBlock
        {
            Text = "BUTTON MAPPINGS · 8 ASSIGNMENTS",
            FontFamily = VoltKit.Display,
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = VoltKit.B("#8a8a8a"),
            Margin = new Thickness(0, 14, 0, 8)
        });

        var mapRows = new (string KeyName, string ActionName, bool Active)[]
        {
            ("A", "Left click", true),
            ("B", "Right click", false),
            ("X", "Middle click", false),
            ("Y", "Shortcut reserved", false),
            ("LB", "Browser Back", false),
            ("RB", "Browser Forward", false),
            ("LT", "Precision Snipe (0.5x)", false),
            ("RT", "Left click", false)
        };

        foreach (var (kName, actName, active) in mapRows)
        {
            var row = new Border
            {
                MinHeight = 48,
                Background = active ? VoltKit.B("#2a2a2a") : Brushes.Transparent,
                BorderBrush = active ? VoltKit.AmberHotBrush : VoltKit.B("#222222"),
                BorderThickness = new Thickness(active ? 2 : 0, active ? 2 : 0, active ? 2 : 0, active ? 2 : 1),
                CornerRadius = active ? new CornerRadius(10) : new CornerRadius(0),
                Padding = new Thickness(16, 4, 16, 4),
                Margin = new Thickness(0, 0, 0, 2),
                Cursor = Cursors.Hand
            };

            var grid = new Grid { VerticalAlignment = VerticalAlignment.Center };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var keyBadge = new Border
            {
                MinWidth = 28,
                Height = 28,
                CornerRadius = new CornerRadius(14),
                Background = active ? VoltKit.AmberHotBrush : VoltKit.B("#262626"),
                Padding = new Thickness(6, 0, 6, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = kName,
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 13,
                    FontWeight = FontWeights.ExtraBold,
                    Foreground = active ? VoltKit.B("#111111") : VoltKit.WhiteBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            Grid.SetColumn(keyBadge, 0);
            grid.Children.Add(keyBadge);

            var actionLabel = new TextBlock
            {
                Text = actName,
                FontFamily = VoltKit.Display,
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = active ? VoltKit.AmberHotBrush : VoltKit.B("#bdbdbd"),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(actionLabel, 1);
            grid.Children.Add(actionLabel);

            row.Child = grid;
            leftStack.Children.Add(row);
        }

        Canvas.SetLeft(leftStack, 0);
        Canvas.SetTop(leftStack, 10);
        content.Children.Add(leftStack);

        // 2. Right Details Card: Button assignment selector (width: 410 at x = 550)
        var rightStack = new StackPanel { Orientation = Orientation.Vertical, Width = 410 };

        var card = new Border
        {
            Width = 410,
            Background = VoltKit.SheetDarkBrush,
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(24, 20, 24, 20)
        };
        var cStack = new StackPanel { Orientation = Orientation.Vertical };

        cStack.Children.Add(new TextBlock
        {
            Text = "BUTTON A ASSIGNMENT",
            FontFamily = VoltKit.Display,
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            Foreground = VoltKit.B("#8a8a8a"),
            Margin = new Thickness(0, 0, 0, 2)
        });

        cStack.Children.Add(new TextBlock
        {
            Text = "Left Click",
            FontFamily = VoltKit.Display,
            FontSize = 32,
            FontWeight = FontWeights.SemiBold,
            Foreground = VoltKit.AmberHotBrush,
            LineHeight = 36
        });

        cStack.Children.Add(new TextBlock
        {
            Text = "Standard primary mouse click. Pressing Button A dispatches a standard Windows WM_LBUTTONDOWN event.",
            FontFamily = VoltKit.Body,
            FontSize = 14,
            Foreground = VoltKit.B("#bdbdbd"),
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 20,
            Margin = new Thickness(0, 6, 0, 16)
        });

        cStack.Children.Add(new TextBlock
        {
            Text = "AVAILABLE ACTIONS",
            FontFamily = VoltKit.Display,
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Foreground = VoltKit.B("#8a8a8a"),
            Margin = new Thickness(0, 0, 0, 8)
        });

        var options = new[] { ("Left Click", true), ("Right Click", false), ("Middle Click", false), ("Double Click", false), ("Wheel Scroll Up", false) };
        foreach (var (opt, on) in options)
        {
            var optRow = new Border
            {
                Height = 44,
                Background = on ? VoltKit.AmberHotBrush : VoltKit.B("#222222"),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16, 0, 16, 0),
                Margin = new Thickness(0, 0, 0, 6),
                Cursor = Cursors.Hand,
                Child = new TextBlock
                {
                    Text = opt,
                    FontFamily = VoltKit.Display,
                    FontSize = 16,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = on ? VoltKit.B("#111111") : VoltKit.WhiteBrush,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            cStack.Children.Add(optRow);
        }

        // Action button [▶ Test Mouse Controls]
        var testBtn = VoltKit.OrangeActionButton("Test Mouse Controls", 362, 52);
        testBtn.Margin = new Thickness(0, 12, 0, 0);
        cStack.Children.Add(testBtn);

        card.Child = cStack;
        rightStack.Children.Add(card);

        Canvas.SetLeft(rightStack, 550);
        Canvas.SetTop(rightStack, 10);
        content.Children.Add(rightStack);

        return VoltKit.TwoPaneShell("mouse", "Mouse", "Gyroscope air mouse, trackpad emulation, and cursor speed mapping.", content, VoltSelectPage);
    }
}
