using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
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
        if (Tester is not { } tester) return;

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

        // Center Controller Pad mounted stage
        var livePad = new Viewbox
        {
            Width = 460,
            Height = 320,
            Stretch = Stretch.Uniform,
            Child = tester.TakeStage()
        };
        Canvas.SetLeft(livePad, cx - 230);
        Canvas.SetTop(livePad, cy - 160);
        host.Children.Add(livePad);

        // Mode chips below pad
        var modeStack = new StackPanel { Orientation = Orientation.Horizontal };

        var kbFace = new Border
        {
            Height = 34,
            Padding = new Thickness(14, 0, 14, 0),
            CornerRadius = new CornerRadius(17),
            Background = tester.KeyboardOn ? BlossomKit.CreateWarmLanternBrush() : BlossomKit.B("rgba(255,220,232,0.08)"),
            BorderBrush = BlossomKit.B("rgba(255,220,232,0.3)"),
            BorderThickness = new Thickness(1.2),
            Margin = new Thickness(0, 0, 8, 0),
            Child = new TextBlock { Text = tester.KeyboardOn ? "Keyboard Test: On" : "Keyboard Test: Off", FontFamily = BlossomKit.Body, FontSize = 14, Foreground = tester.KeyboardOn ? BlossomKit.B("#4a140f") : BlossomKit.TextWhiteBrush, VerticalAlignment = VerticalAlignment.Center }
        };
        modeStack.Children.Add(ThemeAction(kbFace, "theme:tester:keyboard", () => { tester.ClickKeyboard(); BlossomLater(); }));

        var frontFace = new Border
        {
            Height = 34,
            Padding = new Thickness(14, 0, 14, 0),
            CornerRadius = new CornerRadius(17),
            Background = BlossomKit.B("rgba(255,220,232,0.08)"),
            BorderBrush = BlossomKit.B("rgba(255,220,232,0.3)"),
            BorderThickness = new Thickness(1.2),
            Margin = new Thickness(0, 0, 8, 0),
            Child = new TextBlock { Text = "Front View", FontFamily = BlossomKit.Body, FontSize = 14, Foreground = BlossomKit.TextWhiteBrush, VerticalAlignment = VerticalAlignment.Center }
        };
        modeStack.Children.Add(ThemeAction(frontFace, "theme:tester:front", () => tester.ShowRear(false)));

        var rearFace = new Border
        {
            Height = 34,
            Padding = new Thickness(14, 0, 14, 0),
            CornerRadius = new CornerRadius(17),
            Background = BlossomKit.B("rgba(255,220,232,0.08)"),
            BorderBrush = BlossomKit.B("rgba(255,220,232,0.3)"),
            BorderThickness = new Thickness(1.2),
            Child = new TextBlock { Text = "Back View", FontFamily = BlossomKit.Body, FontSize = 14, Foreground = BlossomKit.TextWhiteBrush, VerticalAlignment = VerticalAlignment.Center }
        };
        modeStack.Children.Add(ThemeAction(rearFace, "theme:tester:rear", () => tester.ShowRear(true), HasRearArtwork));

        Canvas.SetLeft(modeStack, 360);
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
            Text = "Reads what Windows reports for the selected controller.",
            FontFamily = BlossomKit.Body,
            FontSize = 15,
            LineHeight = 22,
            Foreground = BlossomKit.B(BlossomKit.LitPaperSub),
            Margin = new Thickness(0, 0, 0, 12)
        });

        // Telemetry readout rows
        var rows = new (string Label, TextBlock Blk)[]
        {
            ("ACTIVE SOURCE", tester.SourceLabel),
            ("PRESSED BUTTONS", tester.PressedBlock),
            ("INPUT OWNERSHIP", tester.OwnershipBlock)
        };

        foreach (var (lbl, blk) in rows)
        {
            var rBorder = new Border
            {
                BorderBrush = BlossomKit.B("rgba(59,23,18,0.12)"),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(0, 8, 0, 8)
            };
            var rg = new Grid();
            rg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
            rg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            rg.Children.Add(new TextBlock
            {
                Text = lbl,
                FontFamily = BlossomKit.Small,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = BlossomKit.B(BlossomKit.LitPaperCrimson),
                VerticalAlignment = VerticalAlignment.Center
            });

            var valTb = new TextBlock
            {
                FontFamily = BlossomKit.Body,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = BlossomKit.B(BlossomKit.LitPaperInk),
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            };
            valTb.SetBinding(TextBlock.TextProperty, new Binding("Text") { Source = blk });
            Grid.SetColumn(valTb, 1);
            rg.Children.Add(valTb);

            rBorder.Child = rg;
            cardStack.Children.Add(rBorder);
        }

        // Action Buttons: Exit tester
        var btnStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 24, 0, 0) };
        var exitBtn = new Border
        {
            Height = 44,
            Padding = new Thickness(22, 0, 22, 0),
            CornerRadius = new CornerRadius(22),
            Background = BlossomKit.B(BlossomKit.LitPaperInk),
            Margin = new Thickness(0, 0, 12, 0),
            Cursor = Cursors.Hand
        };
        var eSt = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        eSt.Children.Add(CreateKeyBadge("B", true));
        eSt.Children.Add(new TextBlock { Text = "Exit tester", FontFamily = BlossomKit.Body, FontSize = 16, Foreground = BlossomKit.B("#ffe2b8"), Margin = new Thickness(10, 0, 0, 0) });
        exitBtn.Child = eSt;
        btnStack.Children.Add(ThemeAction(exitBtn, "theme:tester:exit", () => { tester.ClickExit(); BlossomSelectPage("buttons"); }));
        cardStack.Children.Add(btnStack);

        litCard.Child = cardStack;
        Canvas.SetLeft(litCard, 940);
        Canvas.SetTop(litCard, 150);
        host.Children.Add(litCard);

        // Hints
        var hintsStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        hintsStack.Children.Add(CreateHint("A", "Test buttons"));
        hintsStack.Children.Add(CreateHint("X", "Keyboard test"));
        hintsStack.Children.Add(CreateHint("B", "Exit"));

        Canvas.SetRight(hintsStack, 44);
        Canvas.SetTop(hintsStack, 846);
        host.Children.Add(hintsStack);
    }
}
