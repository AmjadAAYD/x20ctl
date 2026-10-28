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
    /// Setups page for Volt: Steam Deck game-page style with amber radial atmosphere,
    /// rotated Dune controller (-8 deg), Georgia serif title (92px), dark bottom band,
    /// solid orange [▶ Apply Setup] button (300x76), telemetry stat blocks, and square action keys.
    /// Strictly implements t_volt.py:309 setups().
    /// </summary>
    private UIElement BuildVoltSetups()
    {
        var root = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };

        // 1. Atmosphere radial gradient backdrop: #5e5a3a -> #2a2818 -> #0f0e08
        var bgRadial = new RadialGradientBrush
        {
            Center = new Point(0.7, 0.3),
            GradientOrigin = new Point(0.7, 0.3),
            RadiusX = 0.8,
            RadiusY = 0.9,
            GradientStops =
            {
                new(VoltKit.C("#5e5a3a"), 0.0),
                new(VoltKit.C("#2a2818"), 0.55),
                new(VoltKit.C("#0f0e08"), 1.0)
            }
        };
        var bgRect = new Rectangle { Width = 1600, Height = 900, Fill = bgRadial };
        root.Children.Add(bgRect);

        // Amber floating dust particles (specks)
        var random = new Random(42);
        for (int i = 0; i < 45; i++)
        {
            double x = random.Next(500, 1550);
            double y = random.Next(40, 700);
            double sz = random.Next(2, 6);
            double op = 0.2 + random.NextDouble() * 0.5;

            var speck = new Ellipse
            {
                Width = sz,
                Height = sz,
                Fill = new SolidColorBrush(Color.FromArgb((byte)(op * 255), 255, 230, 160)),
                Effect = new BlurEffect { Radius = 1.5 }
            };
            Canvas.SetLeft(speck, x);
            Canvas.SetTop(speck, y);
            root.Children.Add(speck);
        }

        // Rotated Dune controller art (-8 deg, width: 900 at x = 700, y = 60)
        var padArt = VoltKit.ControllerArt("dune", 900);
        if (padArt != null)
        {
            padArt.RenderTransformOrigin = new Point(0.5, 0.5);
            padArt.RenderTransform = new RotateTransform(-8);
            padArt.Effect = new DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 60,
                ShadowDepth = 30,
                Direction = 315,
                Opacity = 0.6
            };
            Canvas.SetLeft(padArt, 700);
            Canvas.SetTop(padArt, 60);
            root.Children.Add(padArt);
        }

        // Left-side shadow gradient overlay (linear 90 deg: #0f0e08 85% -> transparent 50%)
        var shadowBrush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 0),
            GradientStops =
            {
                new(VoltKit.C("rgba(15,14,8,.85)"), 0.0),
                new(VoltKit.C("rgba(15,14,8,.4)"), 0.35),
                new(Colors.Transparent, 0.55)
            }
        };
        var shadowOverlay = new Rectangle { Width = 1600, Height = 900, Fill = shadowBrush, IsHitTestVisible = false };
        root.Children.Add(shadowOverlay);

        // 2. Top bar
        root.Children.Add(VoltKit.TopBar());

        // 3. Top setup pills at (58, 98)
        var pillStack = new StackPanel { Orientation = Orientation.Horizontal };
        var setupNames = new[] { ("DEFAULT FPS", true), ("COMPETITIVE", false), ("TACTICAL RACING", false) };
        foreach (var (sName, active) in setupNames)
        {
            var pill = new Border
            {
                Padding = new Thickness(20, 9, 20, 9),
                CornerRadius = new CornerRadius(12),
                Background = active ? VoltKit.AmberHotBrush : VoltKit.B("#1d1d1d"),
                Margin = new Thickness(0, 0, 10, 0),
                Cursor = Cursors.Hand,
                Child = new TextBlock
                {
                    Text = sName,
                    FontFamily = VoltKit.Display,
                    FontSize = 19,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = active ? VoltKit.B("#111111") : VoltKit.WhiteBrush
                }
            };
            pillStack.Children.Add(pill);
        }

        // [+ New Setup] pill
        var addPill = new Border
        {
            Padding = new Thickness(18, 9, 18, 9),
            CornerRadius = new CornerRadius(12),
            Background = VoltKit.B("#1d1d1d"),
            Cursor = Cursors.Hand
        };
        var addStack = new StackPanel { Orientation = Orientation.Horizontal };
        addStack.Children.Add(new TextBlock
        {
            Text = "\uE710",
            FontFamily = VoltKit.Icons,
            FontSize = 14,
            Foreground = VoltKit.WhiteBrush,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        addStack.Children.Add(new TextBlock
        {
            Text = "New Setup",
            FontFamily = VoltKit.Display,
            FontSize = 19,
            FontWeight = FontWeights.SemiBold,
            Foreground = VoltKit.WhiteBrush,
            VerticalAlignment = VerticalAlignment.Center
        });
        addPill.Child = addStack;
        pillStack.Children.Add(addPill);

        Canvas.SetLeft(pillStack, 58);
        Canvas.SetTop(pillStack, 98);
        root.Children.Add(pillStack);

        // 4. Hero typography at (96, 300, width: 620)
        var heroStack = new StackPanel { Orientation = Orientation.Vertical, Width = 620 };

        // Subtitle: "— CURRENT SETUP —"
        heroStack.Children.Add(new TextBlock
        {
            Text = "— CURRENT SETUP —",
            FontFamily = VoltKit.SerifTitle,
            FontSize = 22,
            Foreground = VoltKit.WhiteBrush,
            Opacity = 0.9,
            Margin = new Thickness(0, 0, 0, 8)
        });

        // Title: "DEFAULT FPS" (Georgia 92pt)
        heroStack.Children.Add(new TextBlock
        {
            Text = "DEFAULT FPS",
            FontFamily = VoltKit.SerifTitle,
            FontSize = 92,
            FontWeight = FontWeights.Normal,
            Foreground = VoltKit.WhiteBrush,
            LineHeight = 94,
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 20, ShadowDepth = 4, Opacity = 0.6 }
        });

        // Subtext: "EASYSMX DUNE · Exact Match · Saved to disk"
        var subStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 16, 0, 0) };
        subStack.Children.Add(new TextBlock
        {
            Text = "EASYSMX DUNE · Exact Match · ",
            FontFamily = VoltKit.Body,
            FontSize = 19,
            Foreground = VoltKit.B("#d8d8d8")
        });
        subStack.Children.Add(new TextBlock
        {
            Text = "Saved to disk",
            FontFamily = VoltKit.Body,
            FontSize = 19,
            FontWeight = FontWeights.SemiBold,
            Foreground = VoltKit.AmberHotBrush
        });
        heroStack.Children.Add(subStack);

        Canvas.SetLeft(heroStack, 96);
        Canvas.SetTop(heroStack, 300);
        root.Children.Add(heroStack);

        // 5. Dark translucent band at bottom (top: 706, height: 116)
        var darkBand = new Border
        {
            Width = 1600,
            Height = 116,
            Background = VoltKit.B("rgba(36,36,36,.74)"),
            Effect = new BlurEffect { Radius = 14 }
        };
        Canvas.SetLeft(darkBand, 0);
        Canvas.SetTop(darkBand, 706);
        root.Children.Add(darkBand);

        // 6. Action button: [▶ Apply Setup] (300x76 at x = 58, y = 728)
        var applyBtn = VoltKit.OrangeActionButton("Apply Setup", 300, 76);
        Canvas.SetLeft(applyBtn, 58);
        Canvas.SetTop(applyBtn, 728);
        root.Children.Add(applyBtn);

        // 7. Stat blocks at x = 400, 590, 780; y = 734
        var stats = new (string Label, string Val)[]
        {
            ("RESPONSE", "Linear Standard"),
            ("DEADZONE", "0.0% Inner"),
            ("TRIGGER", "Short Hair 2mm")
        };

        for (int i = 0; i < stats.Length; i++)
        {
            var (sLab, sVal) = stats[i];
            var sCol = new StackPanel { Orientation = Orientation.Vertical };
            sCol.Children.Add(new TextBlock
            {
                Text = sLab,
                FontFamily = VoltKit.Display,
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = VoltKit.B("#d2d2d2"),
                Margin = new Thickness(0, 0, 0, 4)
            });
            sCol.Children.Add(new TextBlock
            {
                Text = sVal,
                FontFamily = VoltKit.Display,
                FontSize = 26,
                FontWeight = FontWeights.SemiBold,
                Foreground = VoltKit.WhiteBrush
            });

            Canvas.SetLeft(sCol, 400 + i * 190);
            Canvas.SetTop(sCol, 734);
            root.Children.Add(sCol);
        }

        // 8. Square action keys at (1240 + i*82, 728, 72x72)
        var sqIcons = new[] { "\uE8C8", "\uE8AC", "\uEDE1", "\uE712" };
        for (int i = 0; i < sqIcons.Length; i++)
        {
            var sq = new Border
            {
                Width = 72,
                Height = 72,
                CornerRadius = new CornerRadius(10),
                Background = VoltKit.B("#3a3a3a"),
                Cursor = Cursors.Hand,
                Child = new TextBlock
                {
                    Text = sqIcons[i],
                    FontFamily = VoltKit.Icons,
                    FontSize = 30,
                    Foreground = VoltKit.WhiteBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            Canvas.SetLeft(sq, 1240 + i * 82);
            Canvas.SetTop(sq, 728);
            root.Children.Add(sq);
        }

        // 9. Bottom bar with light: true
        var hints = new[] { ("A", "APPLY SETUP"), ("B", "BACK"), ("Y", "NEW SETUP") };
        var bBar = VoltKit.BottomBar(hints, light: true);
        Canvas.SetLeft(bBar, 0);
        Canvas.SetTop(bBar, 854);
        root.Children.Add(bBar);

        return root;
    }
}
