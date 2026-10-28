using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Blossom;

namespace X20Ctl.Product;

/// <summary>
/// Buttons workbench presentation for the Yozakura (Blossom) theme:
/// "The Studio pages are lanterns on a string, and Buttons is lit.
/// The focused button glows with amber fire; its assignment is printed on a warm parchment callout,
/// and output targets are circular samurai seal hanko stones."
/// Strictly implements t_blossom.py:buttons().
/// </summary>
public partial class StudioScene
{
    private void RenderBlossomButtons(Canvas host)
    {
        // 1. Sky & Canopy Backdrop
        var sky = new Rectangle { Width = 1600, Height = 900, Fill = BlossomKit.CreateSkyBackdrop() };
        host.Children.Add(sky);

        // Moon in top right
        var moon = BlossomKit.MoonSphere(260);
        Canvas.SetLeft(moon, 1330);
        Canvas.SetTop(moon, -40);
        host.Children.Add(moon);

        // Corner weeping branch canopy
        host.Children.Add(BlossomKit.CanopyBranch("corner", 22));

        // Drifting petals
        host.Children.Add(BlossomKit.DriftingPetals(new Rect(880, 150, 680, 650), 18, 22));

        // Left Navigation Rail
        RenderBlossomLeftNav(host, "buttons");

        // 2. Title & Lead
        var titleTb = new TextBlock
        {
            Text = "Buttons",
            FontFamily = BlossomKit.Display,
            FontSize = 52,
            Foreground = BlossomKit.TextWhiteBrush
        };
        Canvas.SetLeft(titleTb, 230);
        Canvas.SetTop(titleTb, 30);
        host.Children.Add(titleTb);

        var leadStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        var draftTag = new Border
        {
            Padding = new Thickness(12, 4, 12, 4),
            CornerRadius = new CornerRadius(3),
            Background = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(BlossomKit.WarmFire1, 0.0),
                    new GradientStop(BlossomKit.WarmFire2, 1.0)
                }
            },
            Effect = new DropShadowEffect { Color = Color.FromRgb(0xff, 0x96, 0x5a), BlurRadius = 14, ShadowDepth = 0, Opacity = 0.5 },
            Child = new TextBlock
            {
                Text = "LOCAL DRAFT",
                FontFamily = BlossomKit.Small,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = BlossomKit.B("#4a140f")
            }
        };
        leadStack.Children.Add(draftTag);
        leadStack.Children.Add(new TextBlock
        {
            Text = "Not written to controller memory.",
            FontFamily = BlossomKit.Body,
            FontSize = 16,
            Foreground = BlossomKit.TextMuteBrush,
            Margin = new Thickness(14, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        Canvas.SetLeft(leadStack, 234);
        Canvas.SetTop(leadStack, 106);
        host.Children.Add(leadStack);

        // 3. Category Tabs at (930, 56)
        var tabsStack = new StackPanel { Orientation = Orientation.Horizontal };
        var tabs = new[] { "Face buttons", "Bumpers & triggers", "D-pad", "Sticks", "Rear paddles" };
        for (int i = 0; i < tabs.Length; i++)
        {
            bool on = i == 0;
            var tabTb = new TextBlock
            {
                Text = tabs[i],
                FontFamily = BlossomKit.Body,
                FontSize = 18,
                Foreground = on ? BlossomKit.TextAmberBrush : BlossomKit.TextMuteBrush,
                Margin = new Thickness(0, 0, 32, 0),
                Cursor = Cursors.Hand
            };
            if (on)
            {
                tabTb.Effect = new DropShadowEffect { Color = Color.FromRgb(0xff, 0x96, 0x5a), BlurRadius = 12, ShadowDepth = 0, Opacity = 0.6 };
            }
            tabsStack.Children.Add(tabTb);
        }
        Canvas.SetLeft(tabsStack, 930);
        Canvas.SetTop(tabsStack, 56);
        host.Children.Add(tabsStack);

        // 4. Left Action / Assignment Rows at (236, 196, width 560)
        var rowContainer = new StackPanel { Width = 560 };
        var rows = new (string From, string To, bool Active, string Sub)[]
        {
            ("A", "B", true, "Modified"),
            ("B", "B", false, ""),
            ("X", "X", false, ""),
            ("Y", "Y", false, "")
        };

        foreach (var r in rows)
        {
            var rowBorder = new Border
            {
                Height = 66,
                CornerRadius = new CornerRadius(4),
                Background = r.Active
                    ? new LinearGradientBrush
                    {
                        StartPoint = new Point(0, 0.5),
                        EndPoint = new Point(1, 0.5),
                        GradientStops = new GradientStopCollection
                        {
                            new GradientStop(Color.FromArgb(80, 255, 176, 110), 0.0),
                            new GradientStop(Color.FromArgb(28, 255, 120, 140), 0.6),
                            new GradientStop(Colors.Transparent, 1.0)
                        }
                    }
                    : BlossomKit.B("transparent"),
                BorderBrush = r.Active ? BlossomKit.B("#ffb56b") : BlossomKit.B("rgba(255,220,230,0.08)"),
                BorderThickness = r.Active ? new Thickness(3, 0, 0, 0) : new Thickness(0, 0, 0, 1),
                Padding = new Thickness(18, 0, 18, 0),
                Cursor = Cursors.Hand
            };

            var grid_ = new Grid();
            grid_.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid_.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid_.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var fromStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            fromStack.Children.Add(CreateKeyBadge(r.From, r.Active));
            fromStack.Children.Add(new TextBlock
            {
                Text = "sends",
                FontFamily = BlossomKit.Body,
                FontStyle = FontStyles.Italic,
                FontSize = 17,
                Foreground = BlossomKit.TextMuteBrush,
                Margin = new Thickness(14, 0, 14, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            fromStack.Children.Add(CreateKeyBadge(r.To, r.Active));
            Grid.SetColumn(fromStack, 0);
            grid_.Children.Add(fromStack);

            if (!string.IsNullOrEmpty(r.Sub))
            {
                var subTb = new TextBlock
                {
                    Text = r.Sub,
                    FontFamily = BlossomKit.Body,
                    FontSize = 15,
                    Foreground = BlossomKit.TextMuteBrush,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(subTb, 2);
                grid_.Children.Add(subTb);
            }

            rowBorder.Child = grid_;
            rowContainer.Children.Add(rowBorder);
        }

        // Reset & Test mapping buttons
        foreach (var (ic, txt) in new[] { ("\uE777", "Reset this control"), ("\uE768", "Test mapping") })
        {
            var btnBorder = new Border
            {
                Height = 66,
                BorderBrush = BlossomKit.B("rgba(255,220,230,0.08)"),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(18, 0, 18, 0),
                Cursor = Cursors.Hand
            };
            var bGrid = new Grid();
            bGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var lStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            lStack.Children.Add(new TextBlock
            {
                Text = ic,
                FontFamily = BlossomKit.Icons,
                FontSize = 16,
                Foreground = BlossomKit.TextMuteBrush,
                Margin = new Thickness(0, 0, 16, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            lStack.Children.Add(new TextBlock
            {
                Text = txt,
                FontFamily = BlossomKit.Body,
                FontSize = 18,
                Foreground = BlossomKit.TextWhiteBrush,
                VerticalAlignment = VerticalAlignment.Center
            });
            Grid.SetColumn(lStack, 0);
            bGrid.Children.Add(lStack);

            var chev = new TextBlock
            {
                Text = "\uE76C",
                FontFamily = BlossomKit.Icons,
                FontSize = 12,
                Foreground = BlossomKit.TextMuteBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(chev, 1);
            bGrid.Children.Add(chev);

            btnBorder.Child = bGrid;
            rowContainer.Children.Add(btnBorder);
        }

        Canvas.SetLeft(rowContainer, 236);
        Canvas.SetTop(rowContainer, 196);
        host.Children.Add(rowContainer);

        // 5. Right Pad Art with Illuminated Focus Ring and Speech Parchment
        var padGlow = new Ellipse
        {
            Width = 520,
            Height = 110,
            Fill = new RadialGradientBrush
            {
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb(80, 255, 150, 110), 0.0),
                    new GradientStop(Colors.Transparent, 1.0)
                }
            },
            Effect = new BlurEffect { Radius = 16 }
        };
        Canvas.SetLeft(padGlow, 940);
        Canvas.SetTop(padGlow, 440);
        host.Children.Add(padGlow);

        var padImg = BlossomKit.ControllerArt(Model.Id, 540);
        if (padImg != null)
        {
            padImg.Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 28, ShadowDepth = 24, Opacity = 0.65 };
            Canvas.SetLeft(padImg, 920);
            Canvas.SetTop(padImg, 170);
            host.Children.Add(padImg);
        }

        // Amber Glowing Ring on Button [A]
        double ax = 1270, ay = 280;
        var focusRing = new Ellipse
        {
            Width = 52,
            Height = 52,
            Stroke = BlossomKit.B("#ffb56b"),
            StrokeThickness = 3,
            Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(0xff, 0x96, 0x5a),
                BlurRadius = 26,
                ShadowDepth = 0,
                Opacity = 0.8
            }
        };
        Canvas.SetLeft(focusRing, ax - 26);
        Canvas.SetTop(focusRing, ay - 26);
        host.Children.Add(focusRing);

        // Speech Callout Parchment Badge
        var callout = new Border
        {
            Padding = new Thickness(16, 8, 16, 8),
            CornerRadius = new CornerRadius(3),
            Background = new RadialGradientBrush
            {
                Center = new Point(0.5, 0.5),
                GradientOrigin = new Point(0.5, 0.5),
                RadiusX = 0.7,
                RadiusY = 0.9,
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(BlossomKit.LitPaperTop, 0.0),
                    new GradientStop(BlossomKit.WarmFire1, 0.6),
                    new GradientStop(BlossomKit.WarmFire2, 1.0)
                }
            },
            Effect = new DropShadowEffect { Color = Color.FromRgb(0xff, 0x96, 0x5a), BlurRadius = 22, ShadowDepth = 0, Opacity = 0.6 },
            Child = new TextBlock
            {
                Text = "A sends B",
                FontFamily = BlossomKit.Heading,
                FontSize = 18,
                Foreground = BlossomKit.B("#4a140f")
            }
        };
        Canvas.SetLeft(callout, ax + 36);
        Canvas.SetTop(callout, ay - 96);
        host.Children.Add(callout);

        // 6. Samurai Seal Output Stones Grid at (930, 580)
        var outHeader = new TextBlock
        {
            Text = "OUTPUT",
            FontFamily = BlossomKit.Small,
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Foreground = BlossomKit.B("#b48a99")
        };
        Canvas.SetLeft(outHeader, 930);
        Canvas.SetTop(outHeader, 580);
        host.Children.Add(outHeader);

        var outputs = new[] { "A", "B", "X", "Y", "LB", "RB", "LT", "RT", "LS", "RS" };
        for (int i = 0; i < outputs.Length; i++)
        {
            string o = outputs[i];
            bool on = o == "B";
            double ox = 930 + (i % 5) * 112;
            double oy = 616 + (i / 5) * 92;

            var stone = new Border
            {
                Width = 66,
                Height = 66,
                CornerRadius = new CornerRadius(33),
                Background = on ? BlossomKit.CreateWarmLanternBrush() : BlossomKit.CreateColdLanternBrush(),
                BorderBrush = on ? BlossomKit.B("rgba(255,220,180,0.8)") : BlossomKit.B("rgba(255,200,215,0.12)"),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand
            };

            if (on)
            {
                stone.Effect = new DropShadowEffect
                {
                    Color = Color.FromRgb(0xff, 0x96, 0x5a),
                    BlurRadius = 26,
                    ShadowDepth = 0,
                    Opacity = 0.75
                };
            }

            var stoneTb = new TextBlock
            {
                Text = o,
                FontFamily = BlossomKit.Display,
                FontSize = 22,
                Foreground = on ? BlossomKit.B("#4a140f") : BlossomKit.B("#e8d3da"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            stone.Child = stoneTb;

            Canvas.SetLeft(stone, ox);
            Canvas.SetTop(stone, oy);
            host.Children.Add(stone);
        }

        // Hints
        var hintsStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        hintsStack.Children.Add(CreateHint("A", "Assign"));
        hintsStack.Children.Add(CreateHint("Y", "All assignments"));
        hintsStack.Children.Add(CreateHint("B", "Back"));

        Canvas.SetRight(hintsStack, 44);
        Canvas.SetTop(hintsStack, 846);
        host.Children.Add(hintsStack);
    }
}
