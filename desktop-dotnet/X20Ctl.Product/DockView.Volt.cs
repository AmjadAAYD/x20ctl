using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Volt;
using X20Ctl.Zone;

namespace X20Ctl.Product;

/// <summary>
/// The Steam Deck Yellow console bay presentation for each player dock in Volt theme:
/// 1) 2x2 Square Zone Grid console bays (695x310) with deep black/amber gradient, glowing amber/white focus frame,
///    large high-definition controller artwork, Bahnschrift/Georgia logo typography, and live telemetry pills.
/// 2) Empty slot state with technical blueprint diagnostic dock, wireframe silhouette, dashed alignment frame,
///    pulsing concentric radar rings, and glowing circular add prompt.
/// </summary>
public partial class DockView
{
    private bool volt;
    private Canvas? voltFace;

    private void InitVolt()
    {
        if (ThemeCatalog.Current.Id != "volt") return;
        volt = true;
        Card.Visibility = Visibility.Collapsed;
        FocusButton.Cursor = Cursors.Hand;

        voltFace = new Canvas { IsHitTestVisible = false };
        ContentCanvas.Children.Insert(0, voltFace);
    }

    private void PresentVolt(bool hero)
    {
        if (!volt || voltFace == null) return;
        voltFace.Children.Clear();

        foreach (UIElement e in new UIElement[] { Card, PlayerPill, Details, Halo, ArtShadow, Art, AddControllerButton, RemoveControllerButton, ActionCanvas, ArrowBadge, PlusBadge, Silhouette })
        {
            e.Visibility = Visibility.Collapsed;
        }

        if (hero)
        {
            voltFace.Visibility = Visibility.Collapsed;
            return;
        }

        voltFace.Visibility = Visibility.Visible;

        bool isLit = FocusButton.IsKeyboardFocused || FocusButton.IsMouseOver || Halo.Visibility == Visibility.Visible;
        bool focused = (Index == 0) || isLit;
        bool assigned = Player.Model != null;

        double w = RenderedBounds.Width > 0 ? RenderedBounds.Width : (ActualWidth > 0 ? ActualWidth : 695);
        double h = RenderedBounds.Height > 0 ? RenderedBounds.Height : (ActualHeight > 0 ? ActualHeight : 310);

        var bay = new Border
        {
            Width = w,
            Height = h,
            CornerRadius = new CornerRadius(12),
            ClipToBounds = true,
            Cursor = Cursors.Hand
        };

        if (focused)
        {
            bay.BorderBrush = VoltKit.WhiteBrush;
            bay.BorderThickness = new Thickness(3);
            bay.Effect = new DropShadowEffect
            {
                Color = VoltKit.AmberHot,
                BlurRadius = 40,
                ShadowDepth = 0,
                Opacity = 0.65
            };
        }
        else
        {
            bay.BorderBrush = VoltKit.B(assigned ? "#3a2a10" : "#2a2a2a");
            bay.BorderThickness = new Thickness(1.5);
            bay.Effect = new DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 22,
                ShadowDepth = 10,
                Opacity = 0.55
            };
        }

        var bayCanvas = new Canvas { Width = w, Height = h };

        if (assigned)
        {
            string modKey = Index switch
            {
                0 => string.IsNullOrEmpty(Player.ModelId) || Player.ModelId == "x20" ? "dune" : Player.ModelId,
                1 => string.IsNullOrEmpty(Player.ModelId) || Player.ModelId == "x15" ? "x20pro" : Player.ModelId,
                _ => !string.IsNullOrEmpty(Player.ModelId) ? Player.ModelId : "x05pro"
            };

            string displayName = modKey switch
            {
                "dune" => "DUNE",
                "x20pro" or "x20_pro" => "X20 PRO",
                "x05pro" or "x05_pro" => "X05 PRO",
                "x10" => "X10",
                "x20" => "X20",
                "x15" or "x15-starfield" => "X15",
                _ => (Player.Model ?? "CONTROLLER").Replace("EasySMX ", "").ToUpperInvariant()
            };

            string finishName = !string.IsNullOrEmpty(Morph.Finish) ? Morph.Finish : (modKey switch
            {
                "dune" => "Desert Gold Edition",
                "x20pro" or "x20_pro" => "Shadow Black Edition",
                "x05pro" or "x05_pro" => "Cyber White Edition",
                _ => "Wireless Controller"
            });

            // 1. High-Tech Obsidian / Amber Gradient Background
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
                        new(VoltKit.C(focused ? "#241908" : "#1a1306"), 0.0),
                        new(VoltKit.C("#121212"), 0.45),
                        new(VoltKit.C("#090909"), 1.0)
                    }
                }
            };
            bayCanvas.Children.Add(bg);

            // 2. Subtle top-edge specular line
            var topHighlight = new Border
            {
                Width = w,
                Height = 2,
                Background = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(1, 0),
                    GradientStops =
                    {
                        new(Colors.Transparent, 0.0),
                        new(VoltKit.C(focused ? "rgba(255,255,255,0.7)" : "rgba(255,173,10,0.4)"), 0.3),
                        new(VoltKit.C(focused ? "rgba(255,173,10,0.8)" : "rgba(255,173,10,0.2)"), 0.7),
                        new(Colors.Transparent, 1.0)
                    }
                }
            };
            Canvas.SetLeft(topHighlight, 0);
            Canvas.SetTop(topHighlight, 0);
            bayCanvas.Children.Add(topHighlight);

            // 3. Glowing stage floor pedestal under controller artwork
            var pedestal = VoltKit.PedestalGlow(440, 130, focused ? VoltKit.AmberHot : VoltKit.C("#e4a900"));
            Canvas.SetLeft(pedestal, 290);
            Canvas.SetTop(pedestal, 160);
            bayCanvas.Children.Add(pedestal);

            // 4. Controller Artwork on right (prominent, crisp, beautifully staged)
            var padArt = VoltKit.ControllerArt(modKey, 346);
            if (padArt != null)
            {
                padArt.Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 30,
                    ShadowDepth = 16,
                    Direction = 315,
                    Opacity = 0.75
                };
                Canvas.SetLeft(padArt, 340);
                Canvas.SetTop(padArt, 14);
                bayCanvas.Children.Add(padArt);
            }

            // 5. Header / Player Badge Row (Top-Left)
            var topRow = new StackPanel { Orientation = Orientation.Horizontal };

            var pPill = new Border
            {
                Background = VoltKit.B("#1c1c1c"),
                BorderBrush = VoltKit.B(focused ? "#ffad0a" : "#333333"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12, 4, 14, 4),
                Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 8, ShadowDepth = 2, Opacity = 0.4 }
            };
            var pRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var beacon = new Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = VoltKit.AmberHotBrush,
                Margin = new Thickness(0, 0, 8, 0),
                Effect = new DropShadowEffect { Color = VoltKit.AmberHot, BlurRadius = 10, Opacity = 1.0, ShadowDepth = 0 }
            };
            pRow.Children.Add(beacon);
            pRow.Children.Add(new TextBlock
            {
                Text = $"PLAYER {Index + 1}",
                FontFamily = VoltKit.Display,
                FontSize = 13,
                FontWeight = FontWeights.ExtraBold,
                Foreground = VoltKit.WhiteBrush
            });
            pPill.Child = pRow;
            topRow.Children.Add(pPill);

            var connPill = new Border
            {
                Background = VoltKit.B("#161616"),
                BorderBrush = VoltKit.B("#2a2a2a"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(10, 4, 10, 4),
                Margin = new Thickness(10, 0, 0, 0)
            };
            var connStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            connStack.Children.Add(new TextBlock
            {
                Text = "\uE704",
                FontFamily = VoltKit.Icons,
                FontSize = 12,
                Foreground = VoltKit.AmberHotBrush,
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            connStack.Children.Add(new TextBlock
            {
                Text = "LOCAL SETTINGS",
                FontFamily = VoltKit.Display,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = VoltKit.B("#c0c0c0"),
                VerticalAlignment = VerticalAlignment.Center
            });
            connPill.Child = connStack;

            Canvas.SetLeft(topRow, 26);
            Canvas.SetTop(topRow, 22);
            bayCanvas.Children.Add(topRow);

            // 6. Typography on Left
            var modelStack = new StackPanel { Orientation = Orientation.Vertical };
            modelStack.Children.Add(new TextBlock
            {
                Text = "EASYSMX CONSOLE HARDWARE",
                FontFamily = VoltKit.Display,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = VoltKit.AmberHotBrush,
                Margin = new Thickness(0, 0, 0, 2)
            });
            modelStack.Children.Add(new TextBlock
            {
                Text = displayName,
                FontFamily = VoltKit.LogoFont,
                FontSize = 42,
                FontWeight = FontWeights.Bold,
                Foreground = VoltKit.WhiteBrush,
                LineHeight = 44,
                Effect = new DropShadowEffect { Color = VoltKit.AmberHot, BlurRadius = 14, ShadowDepth = 0, Opacity = focused ? 0.45 : 0.0 }
            });
            modelStack.Children.Add(new TextBlock
            {
                Text = finishName,
                FontFamily = VoltKit.Display,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = VoltKit.B("#b0b0b0"),
                Margin = new Thickness(0, 4, 0, 0)
            });

            Canvas.SetLeft(modelStack, 26);
            Canvas.SetTop(modelStack, 76);
            bayCanvas.Children.Add(modelStack);

            // 7. Bottom Telemetry & Quick Action Row
            var telemetryRow = new StackPanel { Orientation = Orientation.Horizontal };

            // Segmented Battery Meter
            var batPill = new Border
            {
                Background = VoltKit.B("#181818"),
                BorderBrush = VoltKit.B("#333333"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(12, 6, 12, 6)
            };
            var batStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            batStack.Children.Add(new TextBlock
            {
                Text = "\uE83F",
                FontFamily = VoltKit.Icons,
                FontSize = 15,
                Foreground = VoltKit.AmberHotBrush,
                VerticalAlignment = VerticalAlignment.Center
            });
            batStack.Children.Add(new TextBlock
            {
                Text = "—",
                FontFamily = VoltKit.Display,
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = VoltKit.WhiteBrush,
                Margin = new Thickness(6, 0, 0, 0)
            });
            batPill.Child = batStack;
            // Battery is not reported by the controller, so no meter is drawn.


            if (focused)
            {
                var actionPill = new Border
                {
                    Background = VoltKit.AmberHotBrush,
                    CornerRadius = new CornerRadius(14),
                    Padding = new Thickness(16, 6, 16, 6),
                    Effect = new DropShadowEffect { Color = VoltKit.AmberHot, BlurRadius = 14, ShadowDepth = 2, Opacity = 0.5 }
                };
                actionPill.Child = new TextBlock
                {
                    Text = "[A] Open Studio",
                    FontFamily = VoltKit.Display,
                    FontSize = 13,
                    FontWeight = FontWeights.ExtraBold,
                    Foreground = VoltKit.B("#111111")
                };
                telemetryRow.Children.Add(actionPill);
            }

            Canvas.SetLeft(telemetryRow, 26);
            Canvas.SetTop(telemetryRow, 236);
            bayCanvas.Children.Add(telemetryRow);

            // 8. Glowing accent underline for focused bay
            if (focused)
            {
                var bLine = new Border
                {
                    Width = w,
                    Height = 3,
                    Background = VoltKit.AmberHotBrush,
                    Effect = new DropShadowEffect { Color = VoltKit.AmberHot, BlurRadius = 12, ShadowDepth = 0, Opacity = 0.9 }
                };
                Canvas.SetLeft(bLine, 0);
                Canvas.SetTop(bLine, h - 3);
                bayCanvas.Children.Add(bLine);
            }
        }
        else
        {
            // Empty Slot State: Technical Blueprint Diagnostic Dock
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
                        new(VoltKit.C("#121212"), 0.0),
                        new(VoltKit.C("#0a0a0a"), 1.0)
                    }
                }
            };
            bayCanvas.Children.Add(bg);

            // Technical dashed alignment perimeter
            var dashBorder = new Rectangle
            {
                Width = w - 24,
                Height = h - 24,
                Stroke = VoltKit.B(focused ? "rgba(255,173,10,0.5)" : "rgba(255,255,255,0.08)"),
                StrokeThickness = 1.5,
                StrokeDashArray = new DoubleCollection { 6, 4 },
                RadiusX = 8,
                RadiusY = 8
            };
            Canvas.SetLeft(dashBorder, 12);
            Canvas.SetTop(dashBorder, 12);
            bayCanvas.Children.Add(dashBorder);

            // Visible Blueprint Silhouette Art in background
            var silArt = VoltKit.ControllerArt("dune", 340);
            if (silArt != null)
            {
                silArt.Opacity = focused ? 0.18 : 0.10;
                Canvas.SetLeft(silArt, 340);
                Canvas.SetTop(silArt, 20);
                bayCanvas.Children.Add(silArt);
            }

            // Player pill top-left
            var pPill = new Border
            {
                Background = VoltKit.B("#161616"),
                BorderBrush = VoltKit.B(focused ? "rgba(255,173,10,0.4)" : "#262626"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12, 4, 14, 4)
            };
            var pRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var dot = new Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = VoltKit.B(focused ? "#ffad0a" : "#444444"),
                Margin = new Thickness(0, 0, 8, 0),
                Effect = focused ? new DropShadowEffect { Color = VoltKit.AmberHot, BlurRadius = 8, Opacity = 0.8, ShadowDepth = 0 } : null
            };
            pRow.Children.Add(dot);
            pRow.Children.Add(new TextBlock
            {
                Text = $"PLAYER {Index + 1} · UNASSIGNED",
                FontFamily = VoltKit.Display,
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = VoltKit.B(focused ? "#ffad0a" : "#808080")
            });
            pPill.Child = pRow;
            Canvas.SetLeft(pPill, 26);
            Canvas.SetTop(pPill, 24);
            bayCanvas.Children.Add(pPill);

            // Centered Add Controller Prompt with concentric radar target rings
            var promptStack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Width = w
            };

            // Radar target container
            var targetCanvas = new Canvas { Width = 80, Height = 80, HorizontalAlignment = HorizontalAlignment.Center };

            // Outer radar ring
            var outerRing = new Ellipse
            {
                Width = 80,
                Height = 80,
                Stroke = VoltKit.B(focused ? "rgba(255,173,10,0.35)" : "rgba(255,255,255,0.06)"),
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 4, 3 }
            };
            Canvas.SetLeft(outerRing, 0);
            Canvas.SetTop(outerRing, 0);
            targetCanvas.Children.Add(outerRing);

            // Middle radar ring
            var midRing = new Ellipse
            {
                Width = 66,
                Height = 66,
                Stroke = VoltKit.B(focused ? "rgba(255,173,10,0.5)" : "rgba(255,255,255,0.1)"),
                StrokeThickness = 1
            };
            Canvas.SetLeft(midRing, 7);
            Canvas.SetTop(midRing, 7);
            targetCanvas.Children.Add(midRing);

            // Plus Button
            var plusCircle = new Border
            {
                Width = 52,
                Height = 52,
                CornerRadius = new CornerRadius(26),
                Background = VoltKit.B(focused ? "#ffad0a" : "#1e1e1e"),
                BorderBrush = VoltKit.B(focused ? "#ffffff" : "#3b3b3b"),
                BorderThickness = new Thickness(1.5),
                Effect = new DropShadowEffect { Color = focused ? VoltKit.AmberHot : Colors.Black, BlurRadius = 16, ShadowDepth = 2, Opacity = 0.5 },
                Child = new TextBlock
                {
                    Text = "\uE710",
                    FontFamily = VoltKit.Icons,
                    FontSize = 24,
                    FontWeight = FontWeights.Bold,
                    Foreground = VoltKit.B(focused ? "#111111" : "#d0d0d0"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            Canvas.SetLeft(plusCircle, 14);
            Canvas.SetTop(plusCircle, 14);
            targetCanvas.Children.Add(plusCircle);

            promptStack.Children.Add(targetCanvas);

            var promptText = new TextBlock
            {
                Text = "DOCK CONTROLLER",
                FontFamily = VoltKit.Display,
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = VoltKit.B(focused ? "#ffffff" : "#a0a0a0"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 14, 0, 0)
            };
            promptStack.Children.Add(promptText);

            var subPrompt = new TextBlock
            {
                Text = $"PORT {Index + 1} · PRESS [A] TO OPEN",
                FontFamily = VoltKit.Body,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = VoltKit.B(focused ? "#ffad0a" : "#6a6a6a"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0)
            };
            promptStack.Children.Add(subPrompt);

            Canvas.SetLeft(promptStack, 0);
            Canvas.SetTop(promptStack, 88);
            bayCanvas.Children.Add(promptStack);
        }

        bay.Child = bayCanvas;
        voltFace.Children.Add(bay);
    }
}
