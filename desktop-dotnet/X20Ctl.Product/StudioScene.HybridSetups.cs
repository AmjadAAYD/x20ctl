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
    private void HybridShowSetups()
    {
        hybridPageHost!.Children.Clear();
        hybridPageHost.Children.Add(BuildHybridSetups());
    }

    private FrameworkElement BuildHybridSetups()
    {
        var root = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };

        // Docked mode background halves (seam at x=800, ny=470)
        root.Children.Add(HybridKit.Halves(800, 470));
        root.Children.Add(HybridKit.Brand(44, 30, true));
        root.Children.Add(HybridKit.TopRight(false, 0));

        // Title at (160, 104)
        var title = new TextBlock
        {
            Text = "My Setups",
            FontFamily = HybridKit.Display,
            FontSize = 40,
            FontWeight = FontWeights.ExtraBold,
            Foreground = HybridKit.InkBrush
        };
        Canvas.SetLeft(title, 160);
        Canvas.SetTop(title, 104);
        root.Children.Add(title);

        // Subtitle next to title
        var subTitle = new TextBlock
        {
            Text = "· Cloud & Onboard Controller Profiles",
            FontFamily = HybridKit.Body,
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Foreground = HybridKit.MauveBrush
        };
        Canvas.SetLeft(subTitle, 360);
        Canvas.SetTop(subTitle, 122);
        root.Children.Add(subTitle);

        // Open Clamshell Case: Two shells hinged on the seam
        // 1. Left shell (120, 176, w: 664, h: 620)
        var leftShell = new Border
        {
            Width = 664,
            Height = 620,
            CornerRadius = new CornerRadius(44, 10, 10, 44),
            Background = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1),
                GradientStops =
                {
                    new(HybridKit.C("#c4a6ee"), 0.0),
                    new(HybridKit.C("#a785d9"), 1.0)
                }
            },
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 255, 255, 255)),
            BorderThickness = new Thickness(10),
            Effect = new DropShadowEffect { Color = HybridKit.C("#1e1428"), BlurRadius = 40, ShadowDepth = 20, Opacity = 0.35 }
        };
        Canvas.SetLeft(leftShell, 120);
        Canvas.SetTop(leftShell, 176);
        root.Children.Add(leftShell);

        // 2. Right shell (832, 176, w: 648, h: 620)
        var rightShell = new Border
        {
            Width = 648,
            Height = 620,
            CornerRadius = new CornerRadius(10, 44, 44, 10),
            Background = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1),
                GradientStops =
                {
                    new(HybridKit.C("#3a3541"), 0.0),
                    new(HybridKit.C("#25222b"), 1.0)
                }
            },
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x15, 255, 255, 255)),
            BorderThickness = new Thickness(10),
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 40, ShadowDepth = 20, Opacity = 0.45 }
        };
        Canvas.SetLeft(rightShell, 832);
        Canvas.SetTop(rightShell, 176);
        root.Children.Add(rightShell);

        // Seam between shells
        root.Children.Add(HybridKit.Seam(800, 176, 796, 470, true, 0.9));

        // LEFT SHELL CONTENT:
        // Selected setup card at (160, 220, w: 270, h: 340)
        var selCard = new Border
        {
            Width = 270,
            Height = 340,
            CornerRadius = new CornerRadius(22),
            Background = HybridKit.B("#fbf8ff"),
            BorderBrush = HybridKit.CyanBrush,
            BorderThickness = new Thickness(5),
            Effect = new DropShadowEffect { Color = HybridKit.Cyan, BlurRadius = 24, ShadowDepth = 0, Opacity = 0.85 }
        };
        var cardInner = new Canvas { Width = 260, Height = 330, ClipToBounds = true };

        // Controller artwork on card
        var cardPad = HybridKit.ControllerArt("d10-purple", 240);
        if (cardPad != null)
        {
            Canvas.SetLeft(cardPad, 10);
            Canvas.SetTop(cardPad, 20);
            cardInner.Children.Add(cardPad);
        }

        var cardMeta = new StackPanel { Orientation = Orientation.Vertical, Width = 230 };
        cardMeta.Children.Add(new TextBlock
        {
            Text = "Pro Tourney",
            FontFamily = HybridKit.Display,
            FontSize = 22,
            FontWeight = FontWeights.ExtraBold,
            Foreground = HybridKit.InkBrush
        });
        cardMeta.Children.Add(new TextBlock
        {
            Text = "EasySMX D10 · Competitive",
            FontFamily = HybridKit.Body,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = HybridKit.MauveBrush
        });
        Canvas.SetLeft(cardMeta, 16);
        Canvas.SetTop(cardMeta, 260);
        cardInner.Children.Add(cardMeta);

        selCard.Child = cardInner;
        Canvas.SetLeft(selCard, 160);
        Canvas.SetTop(selCard, 220);
        root.Children.Add(selCard);

        // Selected setup detail text on Left shell at (458, 224, w: 300)
        var detailStack = new StackPanel { Width = 300, Orientation = Orientation.Vertical };
        detailStack.Children.Add(new TextBlock
        {
            Text = "ACTIVE PROFILE",
            FontFamily = HybridKit.Display,
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Foreground = HybridKit.B("#5c4075")
        });
        detailStack.Children.Add(new TextBlock
        {
            Text = "Pro Tourney",
            FontFamily = HybridKit.Display,
            FontSize = 32,
            FontWeight = FontWeights.ExtraBold,
            Foreground = HybridKit.InkBrush,
            Margin = new Thickness(0, 2, 0, 10)
        });
        detailStack.Children.Add(new TextBlock
        {
            Text = "Zero deadzone on Hall-effect analog sticks, aggressive linear triggers, rapid fire macro mapped to back paddle M1. Built for low input variance.",
            FontFamily = HybridKit.Body,
            FontSize = 14,
            Foreground = HybridKit.B("#3a2a4c"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 24)
        });

        // Apply button
        var btnApply = new Border
        {
            Height = 44,
            CornerRadius = new CornerRadius(22),
            Background = HybridKit.InkBrush,
            Cursor = System.Windows.Input.Cursors.Hand,
            Padding = new Thickness(20, 0, 20, 0),
            Margin = new Thickness(0, 0, 0, 10),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    new Border
                    {
                        Width = 20,
                        Height = 20,
                        CornerRadius = new CornerRadius(10),
                        Background = HybridKit.WhiteBrush,
                        Margin = new Thickness(0, 0, 8, 0),
                        Child = new TextBlock
                        {
                            Text = "A",
                            FontFamily = new FontFamily("Segoe UI"),
                            FontSize = 11,
                            FontWeight = FontWeights.Bold,
                            Foreground = HybridKit.InkBrush,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    },
                    new TextBlock
                    {
                        Text = "Apply to Player 1",
                        FontFamily = HybridKit.Body,
                        FontSize = 14,
                        FontWeight = FontWeights.Bold,
                        Foreground = HybridKit.WhiteBrush,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                }
            }
        };
        detailStack.Children.Add(btnApply);

        // Duplicate button
        var btnDup = new Border
        {
            Height = 40,
            CornerRadius = new CornerRadius(20),
            Background = new SolidColorBrush(Color.FromArgb(0x33, 92, 64, 117)),
            Cursor = System.Windows.Input.Cursors.Hand,
            Padding = new Thickness(20, 0, 20, 0),
            Child = new TextBlock
            {
                Text = "Duplicate Profile",
                FontFamily = HybridKit.Body,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = HybridKit.InkBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        detailStack.Children.Add(btnDup);

        Canvas.SetLeft(detailStack, 458);
        Canvas.SetTop(detailStack, 224);
        root.Children.Add(detailStack);

        // RIGHT SHELL CONTENT:
        // List of other saved profiles at (870, 220, w: 570)
        var rightStack = new StackPanel { Width = 570, Orientation = Orientation.Vertical };
        rightStack.Children.Add(new TextBlock
        {
            Text = "SAVED PRESETS & TEMPLATES",
            FontFamily = HybridKit.Display,
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            Foreground = HybridKit.B("#8e809f"),
            Margin = new Thickness(0, 0, 0, 16)
        });

        var profiles = new (string Title, string Pad, string Desc)[]
        {
            ("Casual Racing", "EasySMX D10", "Smooth progressive steering curve, analog throttle smoothing."),
            ("Fighting Games", "EasySMX D10", "D-Pad priority diagonals, ultra-short trigger stops."),
            ("Factory Standard", "EasySMX D10", "Default linear analog mapping and standard rumble.")
        };

        foreach (var (pTitle, pPad, pDesc) in profiles)
        {
            var pBorder = new Border
            {
                Height = 110,
                CornerRadius = new CornerRadius(18),
                Background = HybridKit.B("#25212c"),
                Margin = new Thickness(0, 0, 0, 14),
                Padding = new Thickness(20, 16, 20, 16),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            var pGrid = new Grid();
            pGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            pGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var pTextStack = new StackPanel { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center };
            pTextStack.Children.Add(new TextBlock
            {
                Text = pTitle,
                FontFamily = HybridKit.Display,
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = HybridKit.WhiteBrush
            });
            pTextStack.Children.Add(new TextBlock
            {
                Text = $"{pPad} · {pDesc}",
                FontFamily = HybridKit.Body,
                FontSize = 13,
                Foreground = HybridKit.MuteBrush,
                Margin = new Thickness(0, 4, 0, 0)
            });
            Grid.SetColumn(pTextStack, 0);
            pGrid.Children.Add(pTextStack);

            var pBtn = new Border
            {
                Height = 36,
                Padding = new Thickness(14, 0, 14, 0),
                CornerRadius = new CornerRadius(18),
                Background = HybridKit.B("#34303c"),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = "Load",
                    FontFamily = HybridKit.Body,
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = HybridKit.WhiteBrush,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            Grid.SetColumn(pBtn, 1);
            pGrid.Children.Add(pBtn);

            pBorder.Child = pGrid;
            rightStack.Children.Add(pBorder);
        }

        Canvas.SetLeft(rightStack, 870);
        Canvas.SetTop(rightStack, 220);
        root.Children.Add(rightStack);

        return root;
    }
}
