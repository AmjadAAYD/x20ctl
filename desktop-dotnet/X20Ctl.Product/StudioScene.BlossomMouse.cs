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
/// Mouse emulation workbench presentation for the Yozakura (Blossom) theme:
/// "The button map is a shrine's wooden wall of lanterns, one per controller button,
/// with its mapped mouse action painted on a plaque below."
/// Strictly implements t_blossom.py:mouse().
/// </summary>
public partial class StudioScene
{
    private void RenderBlossomMouse(Canvas host)
    {
        // 1. Sky & Backdrop
        var sky = new Rectangle { Width = 1600, Height = 900, Fill = BlossomKit.CreateSkyBackdrop() };
        host.Children.Add(sky);

        // Drifting petals
        host.Children.Add(BlossomKit.DriftingPetals(new Rect(240, 140, 660, 200), 8, 10));

        // Left Navigation Rail
        RenderBlossomLeftNav(host, "mouse");

        // 2. Title & Lead
        var titleTb = new TextBlock
        {
            Text = "Mouse",
            FontFamily = BlossomKit.Display,
            FontSize = 52,
            Foreground = BlossomKit.TextWhiteBrush
        };
        Canvas.SetLeft(titleTb, 230);
        Canvas.SetTop(titleTb, 30);
        host.Children.Add(titleTb);

        var leadTb = new TextBlock
        {
            Text = "Map controller buttons and thumbsticks to direct Windows mouse cursor motions and clicks.",
            FontFamily = BlossomKit.Body,
            FontSize = 16,
            Foreground = BlossomKit.TextMuteBrush
        };
        Canvas.SetLeft(leadTb, 234);
        Canvas.SetTop(leadTb, 102);
        host.Children.Add(leadTb);

        // 3. Top Controls at (236, 142, width 640)
        var topControls = new StackPanel { Width = 640 };

        var rToggle = new Border
        {
            Height = 54,
            BorderBrush = BlossomKit.B("rgba(255,220,230,0.08)"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(12, 0, 12, 0)
        };
        var gTog = new Grid();
        gTog.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        gTog.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        gTog.Children.Add(new TextBlock { Text = "Mouse emulation", FontFamily = BlossomKit.Body, FontSize = 18, Foreground = BlossomKit.TextWhiteBrush, VerticalAlignment = VerticalAlignment.Center });
        var togBorder = new Border
        {
            Width = 50,
            Height = 26,
            CornerRadius = new CornerRadius(13),
            Background = BlossomKit.B("rgba(255,220,232,0.08)"),
            BorderBrush = BlossomKit.B("rgba(255,220,232,0.3)"),
            BorderThickness = new Thickness(1.4)
        };
        togBorder.Child = new Ellipse { Width = 18, Height = 18, Fill = BlossomKit.B("#83405a"), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(4, 0, 0, 0) };
        Grid.SetColumn(togBorder, 1);
        gTog.Children.Add(togBorder);
        rToggle.Child = gTog;
        topControls.Children.Add(rToggle);

        var rSource = new Border
        {
            Height = 54,
            BorderBrush = BlossomKit.B("rgba(255,220,230,0.08)"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(12, 0, 12, 0)
        };
        var gSrc = new Grid();
        gSrc.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        gSrc.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        gSrc.Children.Add(new TextBlock { Text = "Stick motion source", FontFamily = BlossomKit.Body, FontSize = 18, Foreground = BlossomKit.TextWhiteBrush, VerticalAlignment = VerticalAlignment.Center });
        var pillSrc = new Border
        {
            Padding = new Thickness(14, 6, 14, 6),
            CornerRadius = new CornerRadius(18),
            Background = BlossomKit.B("rgba(255,220,232,0.06)"),
            BorderBrush = BlossomKit.B("rgba(255,220,232,0.2)"),
            BorderThickness = new Thickness(1),
            Child = new TextBlock { Text = "Right Stick ▾", FontFamily = BlossomKit.Body, FontSize = 15, Foreground = BlossomKit.TextWhiteBrush }
        };
        Grid.SetColumn(pillSrc, 1);
        gSrc.Children.Add(pillSrc);
        rSource.Child = gSrc;
        topControls.Children.Add(rSource);

        Canvas.SetLeft(topControls, 236);
        Canvas.SetTop(topControls, 142);
        host.Children.Add(topControls);

        // 4. Shrine Wall of Lanterns at (232, 280, width 660)
        var shrineHeader = new TextBlock
        {
            Text = "BUTTON ASSIGNMENTS",
            FontFamily = BlossomKit.Small,
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Foreground = BlossomKit.TextSakuraBrush
        };
        Canvas.SetLeft(shrineHeader, 240);
        Canvas.SetTop(shrineHeader, 280);
        host.Children.Add(shrineHeader);

        // Wooden shelving beams (horizontal)
        foreach (double yb in new[] { 334.0, 578.0 })
        {
            var beam = new Border
            {
                Width = 660,
                Height = 14,
                CornerRadius = new CornerRadius(3),
                Background = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(0, 1),
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(Color.FromRgb(0x5a, 0x3a, 0x28), 0.0),
                        new GradientStop(Color.FromRgb(0x2a, 0x18, 0x10), 1.0)
                    }
                },
                Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 12, ShadowDepth = 8, Opacity = 0.45 }
            };
            Canvas.SetLeft(beam, 232);
            Canvas.SetTop(beam, yb);
            host.Children.Add(beam);
        }

        // Wooden vertical pillars
        foreach (double xp in new[] { 240.0, 874.0 })
        {
            var pillar = new Rectangle { Width = 10, Height = 480, Fill = BlossomKit.B("#2a1810") };
            Canvas.SetLeft(pillar, xp);
            Canvas.SetTop(pillar, 334);
            host.Children.Add(pillar);
        }

        // 12 Hanging Paper Lanterns on Shrine Wall
        var buttons = new (string Key, string Act, bool Active)[]
        {
            ("LS", "Cursor Left", false),
            ("RS", "Cursor Right", false),
            ("LT", "Slow speed", false),
            ("RT", "Fast speed", false),
            ("A", "Left click", true),
            ("B", "Right click", false),
            ("X", "Middle click", false),
            ("Y", "Double click", false),
            ("LB", "Wheel up", false),
            ("RB", "Wheel down", false),
            ("M1", "Back", false),
            ("M2", "Forward", false)
        };

        for (int i = 0; i < buttons.Length; i++)
        {
            var b = buttons[i];
            double lx = 312 + (i % 6) * 100;
            double ly = 372 + (i / 6) * 244;

            var lantern = BlossomKit.PaperLantern(70, 92, b.Active, null, b.Key, b.Active ? 1.0 : 0.5, 24, b.Key.Length > 2 ? 20 : 30);
            lantern.Cursor = Cursors.Hand;
            Canvas.SetLeft(lantern, lx - lantern.Width / 2.0);
            Canvas.SetTop(lantern, ly - 24);
            host.Children.Add(lantern);

            // Plaque Below with mapped action
            var plaqueTb = new TextBlock
            {
                Text = b.Act,
                FontFamily = BlossomKit.Body,
                FontSize = 13,
                Foreground = b.Active ? BlossomKit.TextAmberBrush : BlossomKit.B("#c9b3bc"),
                TextAlignment = TextAlignment.Center,
                Width = 96
            };
            Canvas.SetLeft(plaqueTb, lx - 48);
            Canvas.SetTop(plaqueTb, ly + 104);
            host.Children.Add(plaqueTb);
        }

        // 5. Right Side: Illuminated Washi Parchment Detail Panel at (960, 150, width 584, height 676)
        var litCard = BlossomKit.LitPaperCard(584, 676, new Thickness(32, 28, 32, 24));
        var cardStack = new StackPanel();

        cardStack.Children.Add(new TextBlock
        {
            Text = "Button A Mapping",
            FontFamily = BlossomKit.Display,
            FontSize = 30,
            Foreground = BlossomKit.B(BlossomKit.LitPaperInk)
        });
        cardStack.Children.Add(new TextBlock
        {
            Text = "Assigned to primary Windows mouse event.",
            FontFamily = BlossomKit.Body,
            FontSize = 15,
            Foreground = BlossomKit.B(BlossomKit.LitPaperSub),
            Margin = new Thickness(0, 2, 0, 16)
        });

        var optionSections = new (string Section, string[] Options, string Selected)[]
        {
            ("CLICKS", ["Left click", "Right click", "Middle click", "Double click"], "Left click"),
            ("CURSOR MOVEMENT", ["Analog speed", "Precision slow (20%)", "Turbo fast (200%)"], "Analog speed"),
            ("SCROLL WHEEL", ["Wheel up", "Wheel down", "Horizontal scroll"], "")
        };

        foreach (var sec in optionSections)
        {
            cardStack.Children.Add(new TextBlock
            {
                Text = sec.Section,
                FontFamily = BlossomKit.Small,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = BlossomKit.B(BlossomKit.LitPaperCrimson),
                Margin = new Thickness(0, 10, 0, 6)
            });

            foreach (var opt in sec.Options)
            {
                bool on = opt == sec.Selected;
                var optBorder = new Border
                {
                    Height = 36,
                    CornerRadius = new CornerRadius(3),
                    Background = on ? BlossomKit.B(BlossomKit.LitPaperInk) : BlossomKit.B("transparent"),
                    BorderBrush = on ? BlossomKit.B("transparent") : BlossomKit.B("rgba(59,23,18,0.12)"),
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Padding = new Thickness(14, 0, 14, 0),
                    Cursor = Cursors.Hand
                };
                var optTb = new TextBlock
                {
                    Text = opt,
                    FontFamily = BlossomKit.Body,
                    FontSize = 15,
                    Foreground = on ? BlossomKit.B("#ffe2b8") : BlossomKit.B(BlossomKit.LitPaperInk),
                    VerticalAlignment = VerticalAlignment.Center
                };
                optBorder.Child = optTb;
                cardStack.Children.Add(optBorder);
            }
        }

        var footNote = new TextBlock
        {
            Text = "Virtual mouse driver operates with sub-millisecond precision.",
            FontFamily = BlossomKit.Body,
            FontSize = 13,
            Foreground = BlossomKit.B(BlossomKit.LitPaperSub),
            Margin = new Thickness(0, 16, 0, 0)
        };
        cardStack.Children.Add(footNote);

        litCard.Child = cardStack;
        Canvas.SetLeft(litCard, 960);
        Canvas.SetTop(litCard, 150);
        host.Children.Add(litCard);

        // Hints
        var hintsStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        hintsStack.Children.Add(CreateHint("A", "Assign action"));
        hintsStack.Children.Add(CreateHint("X", "Toggle mouse"));
        hintsStack.Children.Add(CreateHint("B", "Back"));

        Canvas.SetRight(hintsStack, 44);
        Canvas.SetTop(hintsStack, 846);
        host.Children.Add(hintsStack);
    }
}
