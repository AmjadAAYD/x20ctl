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
/// Hardware Tester workbench presentation for the Yozakura (Blossom) theme:
/// "The pad rests in a sacred ring of 18 paper lanterns that illuminate as each control is engaged;
/// celestial moon dials display stick position and polling latency."
/// Strictly implements t_blossom.py:tester().
/// </summary>
public partial class StudioScene
{
    private void RenderBlossomTester(Canvas host)
    {
        // 1. Sky & Backdrop
        var sky = new Rectangle { Width = 1600, Height = 900, Fill = BlossomKit.CreateSkyBackdrop() };
        host.Children.Add(sky);

        // Drifting petals
        host.Children.Add(BlossomKit.DriftingPetals(new Rect(240, 160, 680, 500), 8, 12));

        // Left Navigation Rail
        RenderBlossomLeftNav(host, "tester");

        // 2. Title & Lead
        var titleTb = new TextBlock
        {
            Text = "Tester",
            FontFamily = BlossomKit.Display,
            FontSize = 52,
            Foreground = BlossomKit.TextWhiteBrush
        };
        Canvas.SetLeft(titleTb, 230);
        Canvas.SetTop(titleTb, 30);
        host.Children.Add(titleTb);

        var leadTb = new TextBlock
        {
            Text = "Hardware button switch verification, trigger sweep linearity, and latency inspection.",
            FontFamily = BlossomKit.Body,
            FontSize = 16,
            Foreground = BlossomKit.TextMuteBrush
        };
        Canvas.SetLeft(leadTb, 234);
        Canvas.SetTop(leadTb, 102);
        host.Children.Add(leadTb);

        // 3. Left Side: Controller encircled by Ring of 18 Paper Lanterns at (cx=566, cy=410)
        double cx = 566, cy = 410;

        for (int i = 0; i < 18; i++)
        {
            double a = i / 18.0 * 2 * Math.PI;
            double lx = cx + 320 * Math.Cos(a);
            double ly = cy + 210 * Math.Sin(a);

            var lantern = new Border
            {
                Width = 22,
                Height = 28,
                CornerRadius = new CornerRadius(10, 10, 10, 10),
                Background = BlossomKit.CreateColdLanternBrush(),
                BorderBrush = BlossomKit.B("rgba(255,200,215,0.18)"),
                BorderThickness = new Thickness(1)
            };
            Canvas.SetLeft(lantern, lx - 11);
            Canvas.SetTop(lantern, ly - 14);
            host.Children.Add(lantern);
        }

        // Center Controller Pad
        var padImg = BlossomKit.ControllerArt(Model.Id, 460);
        if (padImg != null)
        {
            padImg.Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 24, ShadowDepth = 20, Opacity = 0.65 };
            Canvas.SetLeft(padImg, cx - 230);
            Canvas.SetTop(padImg, cy - 150);
            host.Children.Add(padImg);
        }

        // Mode chips below pad
        var modeStack = new StackPanel { Orientation = Orientation.Horizontal };
        var rawChip = new Border
        {
            Height = 34,
            Padding = new Thickness(14, 0, 14, 0),
            CornerRadius = new CornerRadius(17),
            Background = BlossomKit.CreateWarmLanternBrush(),
            Effect = new DropShadowEffect { Color = Color.FromRgb(0xff, 0x96, 0x5a), BlurRadius = 14, ShadowDepth = 0, Opacity = 0.5 },
            Child = new TextBlock { Text = "Raw input", FontFamily = BlossomKit.Body, FontSize = 14, Foreground = BlossomKit.B("#4a140f"), VerticalAlignment = VerticalAlignment.Center }
        };
        modeStack.Children.Add(rawChip);

        var dbChip = new Border
        {
            Height = 34,
            Padding = new Thickness(14, 0, 14, 0),
            CornerRadius = new CornerRadius(17),
            Background = BlossomKit.B("transparent"),
            BorderBrush = BlossomKit.B("rgba(255,220,232,0.22)"),
            BorderThickness = new Thickness(1.2),
            Margin = new Thickness(10, 0, 14, 0),
            Child = new TextBlock { Text = "Deadzones applied", FontFamily = BlossomKit.Body, FontSize = 14, Foreground = BlossomKit.B("#e3cfd6"), VerticalAlignment = VerticalAlignment.Center }
        };
        modeStack.Children.Add(dbChip);

        var playerTag = new Border
        {
            Padding = new Thickness(10, 4, 10, 4),
            CornerRadius = new CornerRadius(3),
            BorderBrush = BlossomKit.B("rgba(255,190,130,0.7)"),
            BorderThickness = new Thickness(1.2),
            Child = new TextBlock { Text = "PLAYER 1", FontFamily = BlossomKit.Small, FontSize = 11, FontWeight = FontWeights.Bold, Foreground = BlossomKit.TextAmberBrush }
        };
        modeStack.Children.Add(playerTag);

        Canvas.SetLeft(modeStack, 384);
        Canvas.SetTop(modeStack, 646);
        host.Children.Add(modeStack);

        // 4. Right Side: Illuminated Washi Parchment Card at (940, 150, width 604, height 520)
        var litCard = BlossomKit.LitPaperCard(604, 520, new Thickness(32, 28, 32, 28));
        var cardStack = new StackPanel();

        cardStack.Children.Add(new TextBlock
        {
            Text = "SIGNAL MONITOR",
            FontFamily = BlossomKit.Small,
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Foreground = BlossomKit.B(BlossomKit.LitPaperCrimson)
        });
        cardStack.Children.Add(new TextBlock
        {
            Text = "Direct XInput Capture",
            FontFamily = BlossomKit.Display,
            FontSize = 28,
            Foreground = BlossomKit.B(BlossomKit.LitPaperInk),
            Margin = new Thickness(0, 2, 0, 10)
        });
        cardStack.Children.Add(new TextBlock
        {
            Text = "Physical buttons report true switch contact closures with zero software debouncing lag.",
            FontFamily = BlossomKit.Body,
            FontSize = 15,
            LineHeight = 22,
            Foreground = BlossomKit.B(BlossomKit.LitPaperSub),
            Margin = new Thickness(0, 0, 0, 8)
        });
        cardStack.Children.Add(new TextBlock
        {
            Text = "Waiting for input: Press any button, move analog thumbsticks, or depress analog triggers.",
            FontFamily = BlossomKit.Body,
            FontSize = 15,
            LineHeight = 22,
            Foreground = BlossomKit.B(BlossomKit.LitPaperSub)
        });

        // Action Buttons
        var btnStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 24, 0, 14) };
        var exitBtn = new Border
        {
            Height = 46,
            Padding = new Thickness(22, 0, 22, 0),
            CornerRadius = new CornerRadius(23),
            Background = BlossomKit.B(BlossomKit.LitPaperInk),
            Margin = new Thickness(0, 0, 12, 0),
            Cursor = Cursors.Hand
        };
        var eSt = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        eSt.Children.Add(CreateKeyBadge("B", true));
        eSt.Children.Add(new TextBlock { Text = "Exit tester", FontFamily = BlossomKit.Body, FontSize = 17, Foreground = BlossomKit.B("#ffe2b8"), Margin = new Thickness(10, 0, 0, 0) });
        exitBtn.Child = eSt;
        btnStack.Children.Add(exitBtn);

        var clearBtn = new Border
        {
            Height = 46,
            Padding = new Thickness(20, 0, 20, 0),
            CornerRadius = new CornerRadius(23),
            BorderBrush = BlossomKit.B("rgba(59,23,18,0.55)"),
            BorderThickness = new Thickness(1.5),
            Cursor = Cursors.Hand,
            Child = new TextBlock { Text = "Clear buffer", FontFamily = BlossomKit.Body, FontSize = 15, Foreground = BlossomKit.B(BlossomKit.LitPaperInk), VerticalAlignment = VerticalAlignment.Center }
        };
        btnStack.Children.Add(clearBtn);
        cardStack.Children.Add(btnStack);

        cardStack.Children.Add(new TextBlock
        {
            Text = "Zero frame desync · Direct Windows Raw Input API dispatch",
            FontFamily = BlossomKit.Body,
            FontSize = 13,
            Foreground = BlossomKit.B(BlossomKit.LitPaperSub)
        });

        litCard.Child = cardStack;
        Canvas.SetLeft(litCard, 940);
        Canvas.SetTop(litCard, 150);
        host.Children.Add(litCard);

        // 5. Bottom Gauges Row at (236, 716, width 1308)
        var gaugeGrid = new Grid { Width = 1308 };
        for (int i = 0; i < 4; i++) gaugeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var gauges = new (string Name, string Val, bool IsStick)[]
        {
            ("Left Stick", "X: 0.00 · Y: 0.00", true),
            ("Right Stick", "X: 0.00 · Y: 0.00", true),
            ("Left Trigger", "0% · 0/255", false),
            ("Right Trigger", "0% · 0/255", false)
        };

        for (int i = 0; i < 4; i++)
        {
            var g = gauges[i];
            var gStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            if (g.IsStick)
            {
                var stickIcon = new Canvas { Width = 56, Height = 56, Margin = new Thickness(0, 0, 16, 0) };
                var moon = new Ellipse { Width = 52, Height = 52, Fill = BlossomKit.B("#f1dcde"), Opacity = 0.85 };
                Canvas.SetLeft(moon, 2);
                Canvas.SetTop(moon, 2);
                stickIcon.Children.Add(moon);
                stickIcon.Children.Add(new Line { X1 = 28, Y1 = 6, X2 = 28, Y2 = 50, Stroke = BlossomKit.B("rgba(70,30,50,0.25)") });
                stickIcon.Children.Add(new Line { X1 = 6, Y1 = 28, X2 = 50, Y2 = 28, Stroke = BlossomKit.B("rgba(70,30,50,0.25)") });
                var dot = new Ellipse { Width = 8, Height = 8, Fill = BlossomKit.B("#6a2a44") };
                Canvas.SetLeft(dot, 24);
                Canvas.SetTop(dot, 24);
                stickIcon.Children.Add(dot);
                gStack.Children.Add(stickIcon);
            }
            else
            {
                var trigIcon = new Border
                {
                    Width = 24,
                    Height = 56,
                    CornerRadius = new CornerRadius(12, 12, 12, 12),
                    Background = BlossomKit.CreateColdLanternBrush(),
                    BorderBrush = BlossomKit.B("rgba(255,200,215,0.18)"),
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(0, 0, 16, 0)
                };
                gStack.Children.Add(trigIcon);
            }

            var txtStack = new StackPanel();
            txtStack.Children.Add(new TextBlock { Text = g.Name, FontFamily = BlossomKit.Heading, FontSize = 20, Foreground = BlossomKit.TextWhiteBrush });
            txtStack.Children.Add(new TextBlock { Text = g.Val, FontFamily = BlossomKit.Body, FontSize = 14, Foreground = BlossomKit.TextMuteBrush, Margin = new Thickness(0, 2, 0, 0) });
            gStack.Children.Add(txtStack);

            Grid.SetColumn(gStack, i);
            gaugeGrid.Children.Add(gStack);
        }

        Canvas.SetLeft(gaugeGrid, 236);
        Canvas.SetTop(gaugeGrid, 716);
        host.Children.Add(gaugeGrid);

        // Hints
        var hintsStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        hintsStack.Children.Add(CreateHint("A", "Test buttons"));
        hintsStack.Children.Add(CreateHint("B", "Back"));

        Canvas.SetRight(hintsStack, 44);
        Canvas.SetTop(hintsStack, 846);
        host.Children.Add(hintsStack);
    }
}
