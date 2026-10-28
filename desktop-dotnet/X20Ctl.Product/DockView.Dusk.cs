using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Dusk;
using X20Ctl.Zone;

namespace X20Ctl.Product;

/// <summary>
/// The Steam Deck console bay presentation for each player dock in Dusk theme:
/// 1) 2x2 Square Zone Grid console bays (695x310) with dark slate gradient, glowing white focus frame,
///    large high-definition controller artwork, Bahnschrift logo typography, and live telemetry pills.
/// 2) Empty slot state with blueprint silhouette, dashed frame, and circular add prompt.
/// </summary>
public partial class DockView
{
    private bool dusk;
    private Canvas? duskFace;

    private void InitDusk()
    {
        if (ThemeCatalog.Current.Id != "dusk") return;
        dusk = true;
        Card.Visibility = Visibility.Collapsed;
        FocusButton.Cursor = Cursors.Hand;

        duskFace = new Canvas { IsHitTestVisible = false };
        ContentCanvas.Children.Insert(0, duskFace);
    }

    private void PresentDusk(bool hero)
    {
        if (!dusk || duskFace == null) return;
        duskFace.Children.Clear();

        foreach (UIElement e in new UIElement[] { Card, PlayerPill, Details, Halo, ArtShadow, Art, AddControllerButton, RemoveControllerButton, ActionCanvas, ArrowBadge, PlusBadge, Silhouette })
        {
            e.Visibility = Visibility.Collapsed;
        }

        if (hero)
        {
            duskFace.Visibility = Visibility.Collapsed;
            return;
        }

        duskFace.Visibility = Visibility.Visible;

        bool isLit = FocusButton.IsKeyboardFocused || FocusButton.IsMouseOver || Halo.Visibility == Visibility.Visible;
        bool focused = (Index == 0) || isLit;
        bool assigned = Player.Model != null;

        double w = RenderedBounds.Width > 0 ? RenderedBounds.Width : (ActualWidth > 0 ? ActualWidth : 695);
        double h = RenderedBounds.Height > 0 ? RenderedBounds.Height : (ActualHeight > 0 ? ActualHeight : 310);

        var bay = new Border
        {
            Width = w,
            Height = h,
            CornerRadius = new CornerRadius(8),
            ClipToBounds = true,
            Cursor = Cursors.Hand
        };

        if (focused)
        {
            bay.BorderBrush = DuskKit.WhiteBrush;
            bay.BorderThickness = new Thickness(3);
            bay.Effect = new DropShadowEffect
            {
                Color = Colors.White,
                BlurRadius = 32,
                ShadowDepth = 0,
                Opacity = 0.45
            };
        }
        else
        {
            bay.BorderBrush = DuskKit.B("#283444");
            bay.BorderThickness = new Thickness(1.5);
            bay.Effect = new DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 18,
                ShadowDepth = 8,
                Opacity = 0.45
            };
        }

        var bayCanvas = new Canvas { Width = w, Height = h };

        if (assigned)
        {
            string modKey = Index switch
            {
                0 => string.IsNullOrEmpty(Player.ModelId) || Player.ModelId == "x20" ? "x05pro" : Player.ModelId,
                1 => string.IsNullOrEmpty(Player.ModelId) || Player.ModelId == "x15" ? "x10" : Player.ModelId,
                _ => !string.IsNullOrEmpty(Player.ModelId) ? Player.ModelId : "x20pro"
            };

            string displayName = modKey switch
            {
                "x05pro" or "x05_pro" => "X05 PRO",
                "x10" => "X10",
                "x20" => "X20",
                "x20pro" or "x20_pro" => "X20 PRO",
                "x15" or "x15-starfield" => "X15",
                "d10" => "D10",
                "dune" => "DUNE",
                _ => (Player.Model ?? "CONTROLLER").Replace("EasySMX ", "").ToUpperInvariant()
            };

            string finishName = !string.IsNullOrEmpty(Morph.Finish) ? Morph.Finish : (modKey switch
            {
                "x05pro" or "x05_pro" => "Cyber White Edition",
                "x10" => "Mechanical Silver Edition",
                "x15" or "x15-starfield" => "Starfield Limited Edition",
                _ => "Wireless Controller"
            });

            // 1. Dark Slate Gradient Background
            var bg = new Rectangle
            {
                Width = w,
                Height = h,
                Fill = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(1, 1),
                    GradientStops =
                    {
                        new(DuskKit.C("#1c2532"), 0.0),
                        new(DuskKit.C("#121922"), 0.5),
                        new(DuskKit.C("#0a0e14"), 1.0)
                    }
                }
            };
            bayCanvas.Children.Add(bg);

            // Specular cyan top highlight edge
            var topHighlight = new Border
            {
                Width = w,
                Height = 1.5,
                Background = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0.5),
                    EndPoint = new Point(1, 0.5),
                    GradientStops =
                    {
                        new(DuskKit.C("rgba(26, 159, 255, 0.85)"), 0.0),
                        new(DuskKit.C("rgba(89, 191, 64, 0.5)"), 0.45),
                        new(Colors.Transparent, 1.0)
                    }
                }
            };
            Canvas.SetLeft(topHighlight, 0);
            Canvas.SetTop(topHighlight, 0);
            bayCanvas.Children.Add(topHighlight);

            // Corner brackets & channel HUD badge
            bayCanvas.Children.Add(DuskKit.CornerBrackets(w, h, DuskKit.CyanNeon, 16, 1.5));

            var chBadge = new Border
            {
                Padding = new Thickness(8, 3, 8, 3),
                CornerRadius = new CornerRadius(3),
                Background = DuskKit.B("rgba(14, 18, 24, 0.85)"),
                BorderBrush = DuskKit.B("rgba(26, 159, 255, 0.45)"),
                BorderThickness = new Thickness(1),
                Child = new TextBlock
                {
                    Text = $"CH-0{Index + 1} // ACTIVE",
                    FontFamily = DuskKit.Display,
                    FontSize = 10,
                    FontWeight = FontWeights.ExtraBold,
                    Foreground = DuskKit.CyanNeonBrush
                }
            };
            Canvas.SetRight(chBadge, 16);
            Canvas.SetTop(chBadge, 14);
            bayCanvas.Children.Add(chBadge);

            // 2. Theatrical Stage Pedestal Glow under artwork
            var pedestal = DuskKit.PedestalGlow(320, 85, DuskKit.CyanNeon);
            Canvas.SetLeft(pedestal, 355);
            Canvas.SetTop(pedestal, 185);
            bayCanvas.Children.Add(pedestal);

            // 3. Controller Artwork on the right
            var padArt = DuskKit.ControllerArt(modKey, 340);
            if (padArt != null)
            {
                padArt.Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 24,
                    ShadowDepth = 12,
                    Opacity = 0.55
                };
                Canvas.SetLeft(padArt, 345);
                Canvas.SetTop(padArt, 15);
                bayCanvas.Children.Add(padArt);
            }

            // 4. Header / Player Badge Row (Top-Left)
            var topRow = new StackPanel { Orientation = Orientation.Horizontal };

            var pPill = new Border
            {
                Background = DuskKit.B("#222c38"),
                BorderBrush = DuskKit.B("#313e4f"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(10, 4, 12, 4)
            };
            var pRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var beacon = new Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = DuskKit.B("#59bf40"),
                Margin = new Thickness(0, 0, 8, 0),
                Effect = new DropShadowEffect { Color = DuskKit.C("#59bf40"), BlurRadius = 8, Opacity = 0.8, ShadowDepth = 0 }
            };
            pRow.Children.Add(beacon);
            pRow.Children.Add(new TextBlock
            {
                Text = $"PLAYER {Index + 1}",
                FontFamily = DuskKit.Display,
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = DuskKit.WhiteBrush
            });
            pPill.Child = pRow;
            topRow.Children.Add(pPill);

            var connPill = new Border
            {
                Background = DuskKit.B("#171f28"),
                BorderBrush = DuskKit.B("#273342"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(10, 4, 10, 4),
                Margin = new Thickness(10, 0, 0, 0)
            };
            connPill.Child = new TextBlock
            {
                Text = "2.4G WIRELESS",
                FontFamily = DuskKit.Display,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = DuskKit.B("#8c97a5")
            };
            topRow.Children.Add(connPill);

            Canvas.SetLeft(topRow, 26);
            Canvas.SetTop(topRow, 22);
            bayCanvas.Children.Add(topRow);

            // 5. Typography on Left
            var modelStack = new StackPanel { Orientation = Orientation.Vertical };
            modelStack.Children.Add(new TextBlock
            {
                Text = "EASYSMX",
                FontFamily = DuskKit.Display,
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = DuskKit.B("#7f8c9b")
            });
            modelStack.Children.Add(new TextBlock
            {
                Text = displayName,
                FontFamily = DuskKit.LogoFont,
                FontSize = 38,
                FontWeight = FontWeights.Bold,
                Foreground = DuskKit.WhiteBrush,
                Margin = new Thickness(0, 2, 0, 0)
            });
            modelStack.Children.Add(new TextBlock
            {
                Text = finishName,
                FontFamily = DuskKit.Display,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = DuskKit.B("#a6b5c5"),
                Margin = new Thickness(0, 4, 0, 0)
            });

            Canvas.SetLeft(modelStack, 26);
            Canvas.SetTop(modelStack, 76);
            bayCanvas.Children.Add(modelStack);

            // 6. Bottom Telemetry & Quick Action Row
            var telemetryRow = new StackPanel { Orientation = Orientation.Horizontal };

            var batPill = new Border
            {
                Background = DuskKit.B("#161e27"),
                BorderBrush = DuskKit.B("#2b3847"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(12, 6, 12, 6)
            };
            var batStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            
            // 4-bar segmented battery gauge
            var batFrame = new Border
            {
                Width = 28,
                Height = 13,
                BorderBrush = DuskKit.B("#4b6075"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2.5),
                Padding = new Thickness(1.5),
                Margin = new Thickness(0, 0, 7, 0)
            };
            var cellGrid = new Grid();
            for (int b = 0; b < 4; b++)
                cellGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            for (int b = 0; b < 4; b++)
            {
                var cell = new Border
                {
                    Background = DuskKit.GreenOnlineBrush,
                    CornerRadius = new CornerRadius(1),
                    Margin = new Thickness(0.5, 0, 0.5, 0)
                };
                Grid.SetColumn(cell, b);
                cellGrid.Children.Add(cell);
            }
            batFrame.Child = cellGrid;
            batStack.Children.Add(batFrame);

            batStack.Children.Add(new TextBlock
            {
                Text = "100%",
                FontFamily = DuskKit.Display,
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = DuskKit.WhiteBrush
            });
            batPill.Child = batStack;
            telemetryRow.Children.Add(batPill);

            var ratePill = new Border
            {
                Background = DuskKit.B("#161e27"),
                BorderBrush = DuskKit.B("#2b3847"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(12, 6, 12, 6),
                Margin = new Thickness(10, 0, 0, 0)
            };
            var rateStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var rDot = new Ellipse
            {
                Width = 6,
                Height = 6,
                Fill = DuskKit.GreenOnlineBrush,
                Margin = new Thickness(0, 0, 6, 0)
            };
            rateStack.Children.Add(rDot);
            rateStack.Children.Add(new TextBlock
            {
                Text = "0.98ms Latency",
                FontFamily = DuskKit.Display,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = DuskKit.B("#b8c4cf")
            });
            ratePill.Child = rateStack;
            telemetryRow.Children.Add(ratePill);

            if (focused)
            {
                var actionPill = new Border
                {
                    Background = DuskKit.GreenActionBrush,
                    CornerRadius = new CornerRadius(14),
                    Padding = new Thickness(14, 6, 14, 6),
                    Margin = new Thickness(12, 0, 0, 0),
                    Effect = new DropShadowEffect { Color = DuskKit.GreenOnline, BlurRadius = 14, ShadowDepth = 0, Opacity = 0.5 }
                };
                actionPill.Child = new TextBlock
                {
                    Text = "[A] Open Studio",
                    FontFamily = DuskKit.Display,
                    FontSize = 13,
                    FontWeight = FontWeights.ExtraBold,
                    Foreground = DuskKit.WhiteBrush
                };
                telemetryRow.Children.Add(actionPill);
            }

            Canvas.SetLeft(telemetryRow, 26);
            Canvas.SetTop(telemetryRow, 236);
            bayCanvas.Children.Add(telemetryRow);

            if (focused)
            {
                var bLine = new Border
                {
                    Width = w,
                    Height = 3,
                    Background = new LinearGradientBrush
                    {
                        StartPoint = new Point(0, 0.5),
                        EndPoint = new Point(1, 0.5),
                        GradientStops =
                        {
                            new(DuskKit.CyanNeon, 0.0),
                            new(DuskKit.GreenOnline, 0.4),
                            new(Colors.Transparent, 1.0)
                        }
                    },
                    Effect = new DropShadowEffect { Color = DuskKit.CyanNeon, BlurRadius = 12, ShadowDepth = 0, Opacity = 0.9 }
                };
                Canvas.SetLeft(bLine, 0);
                Canvas.SetTop(bLine, h - 3);
                bayCanvas.Children.Add(bLine);
            }
        }
        else
        {
            // Empty Slot State: Tactical Blueprint Diagnostic Dock
            var bg = new Rectangle
            {
                Width = w,
                Height = h,
                Fill = DuskKit.B("#0c1118")
            };
            bayCanvas.Children.Add(bg);

            // Dashed tactical blueprint perimeter
            var dashBorder = new Rectangle
            {
                Width = w - 4,
                Height = h - 4,
                RadiusX = 8,
                RadiusY = 8,
                Stroke = focused ? DuskKit.CyanNeonBrush : DuskKit.B("#1e2c3c"),
                StrokeThickness = focused ? 2 : 1.5,
                StrokeDashArray = new DoubleCollection { 6, 4 },
                Fill = Brushes.Transparent
            };
            Canvas.SetLeft(dashBorder, 2);
            Canvas.SetTop(dashBorder, 2);
            bayCanvas.Children.Add(dashBorder);

            // Tactical corner brackets & standby channel HUD badge
            bayCanvas.Children.Add(DuskKit.CornerBrackets(w, h, DuskKit.C("rgba(26, 159, 255, 0.45)"), 16, 1.5));

            var chStandby = new Border
            {
                Padding = new Thickness(8, 3, 8, 3),
                CornerRadius = new CornerRadius(3),
                Background = DuskKit.B("rgba(14, 18, 24, 0.85)"),
                BorderBrush = DuskKit.B("rgba(255, 255, 255, 0.15)"),
                BorderThickness = new Thickness(1),
                Child = new TextBlock
                {
                    Text = $"CH-0{Index + 1} // STANDBY",
                    FontFamily = DuskKit.Display,
                    FontSize = 10,
                    FontWeight = FontWeights.ExtraBold,
                    Foreground = DuskKit.MuteBrush
                }
            };
            Canvas.SetRight(chStandby, 16);
            Canvas.SetTop(chStandby, 14);
            bayCanvas.Children.Add(chStandby);

            // Coordinate ticks
            var markTL = new TextBlock { Text = "+", FontFamily = DuskKit.Display, FontSize = 12, FontWeight = FontWeights.Bold, Foreground = DuskKit.B("rgba(26, 159, 255, 0.35)") };
            Canvas.SetLeft(markTL, 24);
            Canvas.SetTop(markTL, 20);
            bayCanvas.Children.Add(markTL);

            var markBR = new TextBlock { Text = "+", FontFamily = DuskKit.Display, FontSize = 12, FontWeight = FontWeights.Bold, Foreground = DuskKit.B("rgba(26, 159, 255, 0.35)") };
            Canvas.SetRight(markBR, 24);
            Canvas.SetBottom(markBR, 20);
            bayCanvas.Children.Add(markBR);

            // Silhouette art in background (visible 18% opacity wireframe blueprint)
            var silArt = DuskKit.ControllerArt("x05pro", 360);
            if (silArt != null)
            {
                silArt.Opacity = 0.18;
                Canvas.SetLeft(silArt, 330);
                Canvas.SetTop(silArt, 15);
                bayCanvas.Children.Add(silArt);
            }

            // Player pill top-left
            var pPill = new Border
            {
                Background = DuskKit.B("#141c26"),
                BorderBrush = DuskKit.B("#233142"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(10, 4, 12, 4)
            };
            var pRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var dot = new Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = DuskKit.B("#3d4f63"),
                Margin = new Thickness(0, 0, 8, 0)
            };
            pRow.Children.Add(dot);
            pRow.Children.Add(new TextBlock
            {
                Text = $"PLAYER {Index + 1} · UNASSIGNED",
                FontFamily = DuskKit.Display,
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = DuskKit.B("#718498")
            });
            pPill.Child = pRow;
            Canvas.SetLeft(pPill, 26);
            Canvas.SetTop(pPill, 22);
            bayCanvas.Children.Add(pPill);

            // Centered Add Controller Prompt with concentric tactical radar rings
            var promptStack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Width = w
            };

            var targetCanvas = new Canvas { Width = 130, Height = 130, HorizontalAlignment = HorizontalAlignment.Center };

            // Outer dashed radar ring
            var outerRing = new Ellipse
            {
                Width = 130,
                Height = 130,
                Stroke = DuskKit.B(focused ? "rgba(26,159,255,0.4)" : "rgba(26,159,255,0.2)"),
                StrokeThickness = 1.5,
                StrokeDashArray = new DoubleCollection { 4, 3 }
            };
            Canvas.SetLeft(outerRing, 0);
            Canvas.SetTop(outerRing, 0);
            targetCanvas.Children.Add(outerRing);

            // Middle radar ring
            var midRing = new Ellipse
            {
                Width = 90,
                Height = 90,
                Stroke = DuskKit.B(focused ? "rgba(26,159,255,0.6)" : "rgba(26,159,255,0.3)"),
                StrokeThickness = 1.5,
                StrokeDashArray = new DoubleCollection { 3, 2 }
            };
            Canvas.SetLeft(midRing, 20);
            Canvas.SetTop(midRing, 20);
            targetCanvas.Children.Add(midRing);

            // Radiant cyan plus button at center
            var plusCircle = new Border
            {
                Width = 56,
                Height = 56,
                CornerRadius = new CornerRadius(28),
                Background = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(1, 1),
                    GradientStops =
                    {
                        new(DuskKit.CyanNeon, 0.0),
                        new(DuskKit.C("#0d70b5"), 1.0)
                    }
                },
                Effect = new DropShadowEffect
                {
                    Color = DuskKit.CyanNeon,
                    BlurRadius = 18,
                    ShadowDepth = 0,
                    Opacity = focused ? 0.95 : 0.75
                },
                Child = new TextBlock
                {
                    Text = "\uE710",
                    FontFamily = DuskKit.Icons,
                    FontSize = 26,
                    Foreground = DuskKit.WhiteBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            Canvas.SetLeft(plusCircle, 37);
            Canvas.SetTop(plusCircle, 37);
            targetCanvas.Children.Add(plusCircle);

            promptStack.Children.Add(targetCanvas);

            var promptText = new TextBlock
            {
                Text = "DOCK CONTROLLER",
                FontFamily = DuskKit.Display,
                FontSize = 17,
                FontWeight = FontWeights.Bold,
                Foreground = DuskKit.WhiteBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 14, 0, 0)
            };
            promptStack.Children.Add(promptText);

            var subPrompt = new TextBlock
            {
                Text = $"PORT {Index + 1} · PRESS [A] TO PAIR 2.4G RF OR USB-C",
                FontFamily = DuskKit.Display,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = focused ? DuskKit.CyanNeonBrush : DuskKit.B("#6b8095"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0)
            };
            promptStack.Children.Add(subPrompt);

            Canvas.SetLeft(promptStack, 0);
            Canvas.SetTop(promptStack, 75);
            bayCanvas.Children.Add(promptStack);
        }

        bay.Child = bayCanvas;
        duskFace.Children.Add(bay);
    }
}
