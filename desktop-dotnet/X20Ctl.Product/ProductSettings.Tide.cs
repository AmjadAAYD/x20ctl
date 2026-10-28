using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Tide;

namespace X20Ctl.Product;

public sealed partial class ProductSettings
{
    internal FrameworkElement? tideRoot;
    private string tideView = "hub";

    private void BuildTide()
    {
        if (pageGrid != null) pageGrid.Visibility = Visibility.Collapsed;
        hub.Visibility = Visibility.Collapsed;
        about.Visibility = Visibility.Collapsed;
        feedback.Visibility = Visibility.Collapsed;
        pcMode.Visibility = Visibility.Collapsed;
        themes.Visibility = Visibility.Collapsed;
        footer.Visibility = Visibility.Collapsed;

        var canvas = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };
        tideRoot = new Viewbox { Child = canvas, Stretch = Stretch.Uniform };
        Children.Add(tideRoot);

        TideSync();
    }

    internal void TideSync()
    {
        if (tideRoot is not Viewbox box || box.Child is not Canvas board) return;
        board.Children.Clear();

        tideView = pcMode.Visibility == Visibility.Visible ? "pcmode" :
                   feedback.Visibility == Visibility.Visible ? "feedback" :
                   about.Visibility == Visibility.Visible ? "about" :
                   themes.Visibility == Visibility.Visible ? "themes" : "hub";

        switch (tideView)
        {
            case "about":
                RenderTideAbout(board);
                break;
            case "themes":
                RenderTideThemes(board);
                break;
            case "feedback":
                RenderTideFeedback(board);
                break;
            case "pcmode":
                RenderTidePcMode(board);
                break;
            case "hub":
            default:
                RenderTideHub(board);
                break;
        }
    }

    private void RenderTideHub(Canvas board)
    {
        // 1. Sea Background with "TOOLS"
        board.Children.Add(TideKit.SeaBackground(25, "TOOLS"));

        // 2. Character: x15-starfield at (-10, 440, w=600)
        board.Children.Add(TideKit.Character("x15-starfield", -10, 440, 600, -22));

        // 3. Status Box
        board.Children.Add(TideKit.StatusBox("19:24", isBack: true, onBack: () => StepBack()));

        // 4. Top Row of tiles: Band + Round Badge + 4 tiles + Arrow
        board.Children.Add(TideKit.TopBand(34));

        var badgeTile = TideKit.RoundBadge("x15-starfield", 250);
        Canvas.SetLeft(badgeTile, 200);
        Canvas.SetTop(badgeTile, 110);
        Panel.SetZIndex(badgeTile, 2);
        board.Children.Add(badgeTile);

        var tiles = new (string Glyph, string Name, Action Action)[]
        {
            ("\uE721", "READ SCAN", () => Check.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent))),
            ("\uE70F", "WRITE SCAN", () => WriteScan.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent))),
            ("\uE774", "SEND ONLINE", () => { }),
            ("\uE715", "FEEDBACK", () => ShowFeedback(true))
        };

        for (int i = 0; i < tiles.Length; i++)
        {
            var t = tiles[i];
            var tileBorder = new Border
            {
                Width = 250,
                Height = 250,
                Background = TideKit.Sea0Brush,
                BorderBrush = TideKit.WhiteBrush,
                BorderThickness = new Thickness(4),
                CornerRadius = new CornerRadius(6),
                ClipToBounds = true,
                Cursor = System.Windows.Input.Cursors.Hand,
                Effect = new DropShadowEffect { BlurRadius = 18, ShadowDepth = 6, Direction = 270, Opacity = 0.45, Color = Color.FromArgb(255, 0, 0, 40) }
            };
            tileBorder.MouseLeftButtonUp += (_, _) => t.Action();

            var tileCanvas = new Canvas { Width = 250, Height = 250 };
            var iconBlock = new TextBlock
            {
                Text = t.Glyph,
                FontFamily = TideKit.Icons,
                FontSize = 84,
                Foreground = TideKit.WhiteBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Canvas.SetLeft(iconBlock, 83);
            Canvas.SetTop(iconBlock, 60);
            tileCanvas.Children.Add(iconBlock);

            var banner = new Border
            {
                Width = 250,
                Height = 38,
                Background = TideKit.InkBrush,
                Padding = new Thickness(12, 6, 12, 6)
            };
            banner.Child = new TextBlock
            {
                Text = t.Name,
                FontFamily = TideKit.Display,
                FontWeight = FontWeights.Black,
                FontStyle = FontStyles.Italic,
                FontSize = 17,
                Foreground = TideKit.WhiteBrush
            };
            Canvas.SetTop(banner, 212);
            tileCanvas.Children.Add(banner);

            tileBorder.Child = tileCanvas;
            Canvas.SetLeft(tileBorder, 490 + i * 268);
            Canvas.SetTop(tileBorder, 52);
            board.Children.Add(tileBorder);
        }

        // Chevron arrow
        var arrow = new Border
        {
            Width = 58,
            Height = 58,
            CornerRadius = new CornerRadius(29),
            Background = TideKit.InkBrush,
            BorderBrush = TideKit.WhiteBrush,
            BorderThickness = new Thickness(3)
        };
        arrow.Child = new TextBlock
        {
            Text = "\uE76C",
            FontFamily = TideKit.Icons,
            FontSize = 28,
            Foreground = TideKit.WhiteBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        Canvas.SetRight(arrow, 22);
        Canvas.SetTop(arrow, 160);
        board.Children.Add(arrow);

        // 5. Note Sticker at (200, 376, w=250)
        var note = TideKit.NoteSticker("FIX & DIAGNOSE\nController tests and scan records", -4);
        Canvas.SetLeft(note, 200);
        Canvas.SetTop(note, 376);
        board.Children.Add(note);

        // 6. Slanted Menu Words at (660, 712): READ SCAN, WRITE SCAN, SEND ONLINE, FEEDBACK
        for (int i = 0; i < tiles.Length; i++)
        {
            var item = tiles[i];
            var word = TideKit.SlantedWord(item.Name, focused: (i == 0), fs: 52, onClick: item.Action);
            Canvas.SetLeft(word, 660 + i * 140);
            Canvas.SetTop(word, 712 - 52);
            board.Children.Add(word);
        }

        // 7. Tilted Keys at (600, 772): update, intro, sound, about
        string[] tKeys = ["\uE777", "\uE714", "\uE767", "\uE946"];
        for (int i = 0; i < tKeys.Length; i++)
        {
            int kIndex = i;
            var kBtn = TideKit.KeyButton(tKeys[i], focused: (i == 0), rot: -12, onClick: () =>
            {
                if (kIndex == 0) ShowThemes(true);
                else if (kIndex == 3) ShowAbout(true);
            });
            Canvas.SetLeft(kBtn, 600 + i * 132);
            Canvas.SetTop(kBtn, 772 - i * 12);
            board.Children.Add(kBtn);
        }

        // 8. Persona Card at (1226, 470, w=334) rotated 3deg
        var sideCard = TideKit.PersonaCard(334, 340, 3);
        var cardStack = new StackPanel();

        cardStack.Children.Add(new TextBlock
        {
            Text = "SOFTWARE UPDATES",
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontSize = 19,
            Foreground = TideKit.Sea0Brush
        });
        cardStack.Children.Add(new TextBlock
        {
            Text = "Automatic GitHub releases check",
            FontFamily = TideKit.Body,
            FontWeight = FontWeights.Bold,
            FontSize = 13,
            Foreground = TideKit.InkBrush,
            Margin = new Thickness(0, 4, 0, 10)
        });
        cardStack.Children.Add(TideKit.PersonaButton("CHECK NOW", primary: true));

        cardStack.Children.Add(new TextBlock
        {
            Text = "OPENING INTRO",
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontSize = 19,
            Foreground = TideKit.Sea0Brush,
            Margin = new Thickness(0, 16, 0, 0)
        });
        cardStack.Children.Add(new TextBlock
        {
            Text = "Play cinematic opening at launch",
            FontFamily = TideKit.Body,
            FontWeight = FontWeights.Bold,
            FontSize = 13,
            Foreground = TideKit.InkBrush,
            Margin = new Thickness(0, 4, 0, 8)
        });

        var togglesRow = new StackPanel { Orientation = Orientation.Horizontal };
        togglesRow.Children.Add(new TextBlock { Text = "INTRO: ON   SOUND: ON", FontFamily = TideKit.Display, FontWeight = FontWeights.Black, FontSize = 14, Foreground = TideKit.SpikeRedBrush });
        cardStack.Children.Add(togglesRow);

        sideCard.Child = cardStack;
        Canvas.SetLeft(sideCard, 1226);
        Canvas.SetTop(sideCard, 470);
        board.Children.Add(sideCard);

        // 9. Hints
        var hintsBar = TideKit.HintsBar(new[] { "\u24B7 Back", "\u24B6 Open" });
        Canvas.SetRight(hintsBar, 40);
        Canvas.SetBottom(hintsBar, 26);
        board.Children.Add(hintsBar);
    }

    private void RenderTideThemes(Canvas board)
    {
        // 1. Sea Background with "THEMES"
        board.Children.Add(TideKit.SeaBackground(11, "THEMES"));

        // 2. Character: x15-starfield at (-30, 500, w=520)
        board.Children.Add(TideKit.Character("x15-starfield", -30, 500, 520, -22));

        // 3. Status Box
        board.Children.Add(TideKit.StatusBox("19:24", isBack: true, onBack: () => ShowThemes(false)));

        // 4. Top Row: 4 theme tiles (default, tide, hybrid, dusk)
        board.Children.Add(TideKit.TopBand(86));

        string[] themeIds = ["default", "tide", "hybrid", "dusk"];
        for (int i = 0; i < themeIds.Length; i++)
        {
            string id = themeIds[i];
            bool on = (id == "tide");
            var tileBorder = new Border
            {
                Width = 250,
                Height = 250,
                CornerRadius = new CornerRadius(6),
                BorderBrush = on ? TideKit.SpikeRedBrush : TideKit.WhiteBrush,
                BorderThickness = new Thickness(4),
                ClipToBounds = true,
                Effect = on
                    ? new DropShadowEffect { BlurRadius = 0, ShadowDepth = 6, Color = TideKit.SpikeRed }
                    : new DropShadowEffect { BlurRadius = 18, ShadowDepth = 6, Direction = 270, Opacity = 0.45, Color = Color.FromArgb(255, 0, 0, 40) }
            };

            var thumbCanvas = new Canvas { Width = 250, Height = 250 };
            var bgRec = new Rectangle
            {
                Width = 250,
                Height = 250,
                Fill = on ? TideKit.Sea0Brush : TideKit.InkBrush
            };
            thumbCanvas.Children.Add(bgRec);

            var banner = new Border
            {
                Width = 250,
                Height = 38,
                Background = TideKit.InkBrush,
                Padding = new Thickness(12, 6, 12, 6)
            };
            banner.Child = new TextBlock
            {
                Text = id.ToUpperInvariant(),
                FontFamily = TideKit.Display,
                FontWeight = FontWeights.Black,
                FontStyle = FontStyles.Italic,
                FontSize = 17,
                Foreground = on ? TideKit.SpikeRedBrush : TideKit.WhiteBrush
            };
            Canvas.SetTop(banner, 212);
            thumbCanvas.Children.Add(banner);

            tileBorder.Child = thumbCanvas;
            Canvas.SetLeft(tileBorder, 210 + i * 268);
            Canvas.SetTop(tileBorder, 104);
            board.Children.Add(tileBorder);
        }

        // 5. Note Sticker at (1200, 640, w=330)
        var note = TideKit.NoteSticker("TIDE\nYOUR CONTROLLERS AND DRAFTS STAY AS THEY ARE", -3);
        Canvas.SetLeft(note, 1200);
        Canvas.SetTop(note, 640);
        board.Children.Add(note);

        // 6. Slanted Menu Words at (600, 712): THEMES, MOTION, OPENING INTRO, SOUND
        string[] menuWords = ["THEMES", "MOTION", "OPENING INTRO", "SOUND"];
        for (int i = 0; i < menuWords.Length; i++)
        {
            var word = TideKit.SlantedWord(menuWords[i], focused: (i == 0), fs: 64);
            Canvas.SetLeft(word, 600 + i * 130);
            Canvas.SetTop(word, 712 - 64);
            board.Children.Add(word);
        }

        // 7. Tilted Keys at (560, 772): theme, intro, sound, update
        string[] thmKeys = ["\uE771", "\uE714", "\uE767", "\uE777"];
        for (int i = 0; i < thmKeys.Length; i++)
        {
            var kBtn = TideKit.KeyButton(thmKeys[i], focused: (i == 0), rot: -12);
            Canvas.SetLeft(kBtn, 560 + i * 132);
            Canvas.SetTop(kBtn, 772 - i * 12);
            board.Children.Add(kBtn);
        }

        // 8. Hints
        var hintsBar = TideKit.HintsBar(new[] { "\u24B7 Back", "\u24B6 Apply" });
        Canvas.SetRight(hintsBar, 40);
        Canvas.SetBottom(hintsBar, 26);
        board.Children.Add(hintsBar);
    }

    private void RenderTideAbout(Canvas board)
    {
        var frame = new TideToolFrame(
            "ABOUT",
            "Native Controller Studio · V0.016",
            onBack: () => ShowAbout(false),
            hints: new[] { "\u24B7 Back", "\u24B6 Open" },
            seed: 17);

        // Left List: Staircase rows
        var listStack = new StackPanel { Width = 640 };
        listStack.Children.Add(TideKit.PersonaRow("Made By", sub: "Amjad AAYD, who designs and builds X20CTL.", focused: true));
        listStack.Children.Add(TideKit.PersonaRow("Thanks", sub: "Special thanks to Kiin from Team EasySMX for sending me a free X20 Pro controller."));
        listStack.Children.Add(TideKit.PersonaRow("Inspiration", sub: "The console-style look of X20CTL was inspired by ApexSenseBridge by ReynArts. Thank you for showing how good a controller app can feel."));
        listStack.Children.Add(TideKit.PersonaRow("X20CTL on GitHub", ctl: "chev"));
        listStack.Children.Add(TideKit.PersonaRow("ApexSenseBridge by ReynArts", ctl: "chev"));

        var listHost = new Canvas
        {
            Width = 640,
            RenderTransformOrigin = new Point(0, 0),
            RenderTransform = new RotateTransform(-3)
        };
        listHost.Children.Add(listStack);
        frame.SetLeftList(listHost, 176);

        // Right Side: Detail Card only (visual is None)
        var detailCard = TideKit.DetailCard(
            title: "X20CTL",
            sub: "Native Controller Studio · V0.016",
            body: "X20CTL is an independent, open-source controller studio for EasySMX pads. Remap buttons, shape stick and trigger response, build back-paddle macros, set vibration, and check that a controller works, all on your own PC. There is no account, and nothing leaves your PC unless you choose to send a report or feedback.\n\nEvery change stays a local draft until it can be sent to a connected, verified controller. X20CTL never writes to a controller it cannot read back.",
            foot: "Credits · Desktop-control layout inspired by Gopher360 — Tylemagne, GPL-3.0. See the distributed open-source license notices.",
            buttons: new[] { "Check for updates", "Open GitHub" },
            width: 580,
            rot: 2);

        var sideStack = TideKit.SideStack(null, detailCard, 580);
        frame.SetSideStack(sideStack, 176);
        frame.SetNote("Credits");

        board.Children.Add(frame);
    }

    private void RenderTideFeedback(Canvas board)
    {
        var feedbackBack = Pill("\uE72B", "Back to Tools");
        feedbackBack.Click += (_, _) => ShowFeedback(false);
        var form = new FeedbackPage(feedbackBack, Scanner.ControllerCheckView.Models.Where(m => m != "Unknown"));
        board.Children.Add(form);
    }

    private void RenderTidePcMode(Canvas board)
    {
        var pcmodeFrame = new TideToolFrame(
            "PC MODE",
            "High-performance low-latency PC input routing · ViGEmBus virtual controller pipeline.",
            onBack: () => ShowPcMode(false),
            hints: new[] { "\u24B7 Back", "\u24B6 Toggle", "\u24BE Driver Status" },
            seed: 12);
        board.Children.Add(pcmodeFrame);
    }
}
