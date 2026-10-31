using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Tide;
using X20Ctl.Zone;

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

        // 2. Character: x15-starfield at (-10, 440, w=600) with breathing animation
        var padArt = TideKit.Character("x15-starfield", -10, 440, 600, -22);
        TideKit.AnimatePulse(padArt, 0.98, 1.02, 3.5);
        board.Children.Add(padArt);

        // 3. Status Box
        board.Children.Add(TideKit.StatusBox(ThemeClock.Now, isBack: true, onBack: () => Back.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent))));

        // 4. Top Row of tiles: Band + Round Badge + 4 tiles + Arrow
        board.Children.Add(TideKit.TopBand(34));

        // Round badge: Themes & Styles shortcut
        var badgeTile = TideKit.RoundBadge("x15-starfield", 250);
        badgeTile.Cursor = System.Windows.Input.Cursors.Hand;
        badgeTile.MouseLeftButtonUp += (_, _) => ShowThemes(true);
        Canvas.SetLeft(badgeTile, 200);
        Canvas.SetTop(badgeTile, 110);
        Panel.SetZIndex(badgeTile, 2);
        board.Children.Add(badgeTile);

        var badgeLabel = new Border
        {
            Width = 230,
            Height = 36,
            Background = TideKit.Sea0Brush,
            BorderBrush = TideKit.WhiteBrush,
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10, 4, 10, 4)
        };
        badgeLabel.Child = new TextBlock
        {
            Text = "THEMES & STYLES",
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 13,
            Foreground = TideKit.WhiteBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        Canvas.SetLeft(badgeLabel, 210);
        Canvas.SetTop(badgeLabel, 310);
        Panel.SetZIndex(badgeLabel, 3);
        board.Children.Add(badgeLabel);

        var tiles = new (string Glyph, string Name, string Sub, Action Action)[]
        {
            ("\uE721", "READ SCAN", "Hardware scanner", () => Check.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent))),
            ("\uE70F", "WRITE SCAN", "Bluetooth logs", () => WriteScan.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent))),
            ("\uE771", "VISUAL THEMES", "10 console themes", () => ShowThemes(true)),
            ("\uE715", "FEEDBACK", "Submit feedback", () => ShowFeedback(true))
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
                FontSize = 76,
                Foreground = TideKit.WhiteBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Canvas.SetLeft(iconBlock, 87);
            Canvas.SetTop(iconBlock, 55);
            tileCanvas.Children.Add(iconBlock);

            var banner = new Border
            {
                Width = 250,
                Height = 44,
                Background = TideKit.InkBrush,
                Padding = new Thickness(12, 4, 12, 4)
            };
            var bStack = new StackPanel();
            bStack.Children.Add(new TextBlock
            {
                Text = t.Name,
                FontFamily = TideKit.Display,
                FontWeight = FontWeights.Black,
                FontStyle = FontStyles.Italic,
                FontSize = 16,
                Foreground = TideKit.WhiteBrush
            });
            bStack.Children.Add(new TextBlock
            {
                Text = t.Sub,
                FontFamily = TideKit.Body,
                FontWeight = FontWeights.Bold,
                FontSize = 10,
                Foreground = TideKit.LightCyanBrush
            });
            banner.Child = bStack;
            Canvas.SetTop(banner, 206);
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
            BorderThickness = new Thickness(3),
            Cursor = System.Windows.Input.Cursors.Hand
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
        arrow.MouseLeftButtonUp += (_, _) => ShowAbout(true);
        Canvas.SetRight(arrow, 22);
        Canvas.SetTop(arrow, 160);
        board.Children.Add(arrow);

        // 5. Note Sticker at (200, 376, w=250)
        var note = TideKit.NoteSticker("TOOLS & EXTRAS\nController diagnostics and customization", -4);
        Canvas.SetLeft(note, 200);
        Canvas.SetTop(note, 376);
        board.Children.Add(note);

        // 6. Slanted Menu Words at (660, 712): READ SCAN, WRITE SCAN, THEMES, FEEDBACK
        for (int i = 0; i < tiles.Length; i++)
        {
            var item = tiles[i];
            var word = TideKit.SlantedWord(item.Name, focused: (i == 0), fs: 52, onClick: item.Action);
            Canvas.SetLeft(word, 660 + i * 140);
            Canvas.SetTop(word, 712 - 52);
            board.Children.Add(word);
        }

        // 7. Tilted Keys at (600, 772):
        // [0] Themes (\uE771) -> ShowThemes(true)
        // [1] Intro toggle (\uE714) -> toggles intro launch
        // [2] Sound toggle (\uE767) -> toggles intro sound
        // [3] About (\uE946) -> ShowAbout(true)
        string[] tKeys = ["\uE771", "\uE714", "\uE767", "\uE946"];
        for (int i = 0; i < tKeys.Length; i++)
        {
            int kIndex = i;
            var kBtn = TideKit.KeyButton(tKeys[i], focused: (i == 0), rot: -12, onClick: () =>
            {
                if (kIndex == 0) ShowThemes(true);
                else if (kIndex == 1)
                {
                    bool nextLaunch = preferences?.IntroOnLaunch != true;
                    SetIntro(nextLaunch, null);
                    TideSync();
                }
                else if (kIndex == 2)
                {
                    bool nextSound = preferences?.IntroSound != true;
                    SetIntro(null, nextSound);
                    TideSync();
                }
                else if (kIndex == 3) ShowAbout(true);
            });
            Canvas.SetLeft(kBtn, 600 + i * 132);
            Canvas.SetTop(kBtn, 772 - i * 12);
            board.Children.Add(kBtn);
        }

        // 8. Persona Card at (1226, 440, w=334) rotated 3deg
        var sideCard = TideKit.PersonaCard(334, 380, 3);
        var cardStack = new StackPanel { Margin = new Thickness(14, 12, 14, 12) };

        cardStack.Children.Add(new TextBlock
        {
            Text = "SOFTWARE UPDATES",
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontSize = 18,
            Foreground = TideKit.Sea0Brush
        });
        cardStack.Children.Add(new TextBlock
        {
            Text = "Automatic release version check",
            FontFamily = TideKit.Body,
            FontWeight = FontWeights.Bold,
            FontSize = 12,
            Foreground = TideKit.InkBrush,
            Margin = new Thickness(0, 2, 0, 8)
        });

        var checkBtn = TideKit.PersonaButton("CHECK UPDATES", primary: true, onClick: () =>
        {
            try { updateCheck.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)); }
            catch (Exception) { }
        });
        checkBtn.Height = 36;
        cardStack.Children.Add(checkBtn);

        cardStack.Children.Add(new TextBlock
        {
            Text = "OPENING CINEMATIC",
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontSize = 18,
            Foreground = TideKit.Sea0Brush,
            Margin = new Thickness(0, 14, 0, 4)
        });

        bool introOn = preferences?.IntroOnLaunch == true;
        bool soundOn = preferences?.IntroSound == true;

        var togRow = new StackPanel { Orientation = Orientation.Horizontal };
        var introTogBtn = TideKit.PersonaButton(introOn ? "INTRO: ON" : "INTRO: OFF", primary: introOn, onClick: () =>
        {
            SetIntro(!introOn, null);
            TideSync();
        });
        introTogBtn.Height = 34;
        introTogBtn.Margin = new Thickness(0, 0, 8, 0);
        togRow.Children.Add(introTogBtn);

        var soundTogBtn = TideKit.PersonaButton(soundOn ? "SOUND: ON" : "SOUND: OFF", primary: soundOn, onClick: () =>
        {
            SetIntro(null, !soundOn);
            TideSync();
        });
        soundTogBtn.Height = 34;
        togRow.Children.Add(soundTogBtn);
        cardStack.Children.Add(togRow);

        // Ko-fi & GitHub links
        var extRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 14, 0, 0) };
        var kofiBtn = TideKit.PersonaButton("KO-FI", primary: false, onClick: () =>
        {
            try { Support.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)); }
            catch (Exception) { }
        });
        kofiBtn.Height = 32;
        kofiBtn.Margin = new Thickness(0, 0, 8, 0);
        extRow.Children.Add(kofiBtn);

        var gitBtn = TideKit.PersonaButton("GITHUB", primary: false, onClick: () =>
        {
            try { Star.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)); }
            catch (Exception) { }
        });
        gitBtn.Height = 32;
        extRow.Children.Add(gitBtn);
        cardStack.Children.Add(extRow);

        sideCard.Child = cardStack;
        Canvas.SetLeft(sideCard, 1226);
        Canvas.SetTop(sideCard, 430);
        board.Children.Add(sideCard);

        // 9. Hints
        var hintsBar = TideKit.HintsBar(new[] { "\u24B7 Back to Zone", "\u24B6 Select", "\u2295 About & Credits" });
        Canvas.SetRight(hintsBar, 40);
        Canvas.SetBottom(hintsBar, 26);
        board.Children.Add(hintsBar);

        TideKit.AnimateEntrance(board);
    }

    private void RenderTideThemes(Canvas board)
    {
        // 1. Sea Background with "THEMES"
        board.Children.Add(TideKit.SeaBackground(11, "THEMES"));

        // 2. Character: x15-starfield at (-30, 500, w=520)
        var characterPad = TideKit.Character("x15-starfield", -30, 500, 520, -22);
        TideKit.AnimatePulse(characterPad, 0.98, 1.02, 3.5);
        board.Children.Add(characterPad);

        // 3. Status Box with Back
        board.Children.Add(TideKit.StatusBox(ThemeClock.Now, isBack: true, onBack: () => ShowThemes(false)));

        // 4. Header Sticker at (214, 104)
        var leadBox = new Border
        {
            Padding = new Thickness(16, 8, 16, 8),
            Background = TideKit.WhiteBrush,
            BorderBrush = TideKit.InkBrush,
            BorderThickness = new Thickness(3),
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new RotateTransform(-1.5),
            Effect = new DropShadowEffect { BlurRadius = 0, ShadowDepth = 6, Direction = 315, Color = TideKit.Sea0 }
        };
        leadBox.Child = new TextBlock
        {
            Text = "SELECT VISUAL THEME · Instant local transformation · Drafts and models remain intact",
            FontFamily = TideKit.Body,
            FontWeight = FontWeights.Bold,
            FontSize = 14,
            Foreground = TideKit.InkBrush
        };
        Canvas.SetLeft(leadBox, 214);
        Canvas.SetTop(leadBox, 104);
        board.Children.Add(leadBox);

        // 5. Grid of ALL 10 Themes at (220, 164)
        var themesGrid = new Grid { Width = 960 };
        for (int c = 0; c < 5; c++) themesGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (int r = 0; r < 2; r++) themesGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(140) });

        var allThemes = new (string Id, string Name, string Accent, string Sub)[]
        {
            ("default", "DEFAULT", "#2563EB", "Classic Studio"),
            ("pulse", "PULSE", "#00FFCC", "Cyber Waveform"),
            ("spotlight", "SPOTLIGHT", "#EAB308", "Noir Warm Glow"),
            ("nexus", "NEXUS", "#38BDF8", "Holo Matrix"),
            ("rebel", "REBEL", "#F43F5E", "Cyberpunk Edge"),
            ("tide", "TIDE", "#28B6F0", "Persona 3 Blue"),
            ("dusk", "DUSK", "#FB923C", "Twilight Orange"),
            ("volt", "VOLT", "#FACC15", "High Voltage"),
            ("blossom", "YOZAKURA", "#F472B6", "Night Blossom")
        };

        string curTheme = ThemeCatalog.Current.Id;

        for (int i = 0; i < allThemes.Length; i++)
        {
            var t = allThemes[i];
            int row = i / 5;
            int col = i % 5;
            bool isActive = t.Id == curTheme;

            var card = new Border
            {
                Margin = new Thickness(6),
                Background = isActive ? TideKit.WhiteBrush : TideKit.Sea0Brush,
                BorderBrush = isActive ? TideKit.SpikeRedBrush : TideKit.WhiteBrush,
                BorderThickness = new Thickness(isActive ? 4 : 2),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 10, 12, 10),
                Cursor = System.Windows.Input.Cursors.Hand,
                Effect = new DropShadowEffect
                {
                    BlurRadius = 0,
                    ShadowDepth = 5,
                    Direction = 315,
                    Color = isActive ? TideKit.SpikeRed : TideKit.Ink
                }
            };

            var cStack = new StackPanel();

            // Accent swatch bar
            var accentBar = new Border
            {
                Height = 6,
                CornerRadius = new CornerRadius(3),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(t.Accent)),
                Margin = new Thickness(0, 0, 0, 8)
            };
            cStack.Children.Add(accentBar);

            cStack.Children.Add(new TextBlock
            {
                Text = t.Name,
                FontFamily = TideKit.Display,
                FontWeight = FontWeights.Black,
                FontStyle = FontStyles.Italic,
                FontSize = 15,
                Foreground = isActive ? TideKit.Sea0Brush : TideKit.WhiteBrush
            });

            cStack.Children.Add(new TextBlock
            {
                Text = t.Sub,
                FontFamily = TideKit.Body,
                FontWeight = FontWeights.Bold,
                FontSize = 10,
                Foreground = isActive ? TideKit.SubBrush : TideKit.LightCyanBrush,
                Margin = new Thickness(0, 2, 0, 6)
            });

            if (isActive)
            {
                var pill = new Border
                {
                    Padding = new Thickness(6, 2, 6, 2),
                    Background = TideKit.SpikeRedBrush,
                    CornerRadius = new CornerRadius(3),
                    HorizontalAlignment = HorizontalAlignment.Left
                };
                pill.Child = new TextBlock
                {
                    Text = "ACTIVE",
                    FontFamily = TideKit.Display,
                    FontWeight = FontWeights.Black,
                    FontStyle = FontStyles.Italic,
                    FontSize = 9,
                    Foreground = TideKit.WhiteBrush
                };
                cStack.Children.Add(pill);
            }

            card.Child = cStack;
            Grid.SetRow(card, row);
            Grid.SetColumn(card, col);

            string themeId = t.Id;
            card.MouseLeftButtonUp += (_, _) =>
            {
                ThemeCatalog.Apply(ThemeCatalog.Find(themeId));
                preferences?.SetTheme(themeId);
                RequestThemeRestart();
                TideSync();
            };

            themesGrid.Children.Add(card);
        }

        Canvas.SetLeft(themesGrid, 220);
        Canvas.SetTop(themesGrid, 164);
        board.Children.Add(themesGrid);

        // 6. Note Sticker at (1200, 520, w=350)
        var note = TideKit.NoteSticker("THEME SWITCHING\nSelect any theme to transform your studio instantly. Your controller settings stay safe.", -3);
        Canvas.SetLeft(note, 1200);
        Canvas.SetTop(note, 520);
        board.Children.Add(note);

        // 7. Slanted Action Words at (600, 740)
        var actWords = new (string Label, Action Action)[]
        {
            ("RESTART APP", () => RequestThemeRestart()),
            ("OPEN TOOLS", () => ShowThemes(false)),
            ("CONTROLLER ZONE", () => Back.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)))
        };

        for (int i = 0; i < actWords.Length; i++)
        {
            var item = actWords[i];
            var word = TideKit.SlantedWord(item.Label, focused: (i == 0), fs: 52, onClick: item.Action);
            Canvas.SetLeft(word, 600 + i * 160);
            Canvas.SetTop(word, 740 - 52);
            board.Children.Add(word);
        }

        // 8. Hints
        var hintsBar = TideKit.HintsBar(new[] { "\u24B7 Back to Tools", "\u24B6 Apply Theme", "\u2295 Restart" });
        Canvas.SetRight(hintsBar, 40);
        Canvas.SetBottom(hintsBar, 26);
        board.Children.Add(hintsBar);

        TideKit.AnimateEntrance(board);
    }

    private void RenderTideAbout(Canvas board)
    {
        var frame = new TideToolFrame(
            "ABOUT",
            $"Native Controller Studio · {Scanner.ScanFlow.AppVersion} · Created by Amjad AAYD",
            onBack: () => ShowAbout(false),
            hints: new[] { "\u24B7 Back to Tools", "\u24B6 Open Link", "\u2295 Check Updates" },
            seed: 17);

        // Left List: Staircase rows
        var listStack = new StackPanel { Width = 640 };
        listStack.Children.Add(TideKit.PersonaRow("Creator & Lead Developer", sub: "Amjad AAYD — architecture, design and implementation.", focused: true));
        listStack.Children.Add(TideKit.PersonaRow("Special Thanks", sub: "Kiin from Team EasySMX for hardware support."));
        listStack.Children.Add(TideKit.PersonaRow("Visual Concept", sub: "Inspired by console-style user experience and Persona 3 aesthetic."));

        var gitRow = TideKit.PersonaRow("X20CTL on GitHub", sub: "https://github.com/AmjadAAYD/x20ctl", ctl: "chev", onClick: () =>
        {
            try { Star.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)); }
            catch (Exception) { }
        });
        listStack.Children.Add(gitRow);

        var kofiRow = TideKit.PersonaRow("Support on Ko-fi", sub: "Support ongoing development and hardware reverse engineering", ctl: "chev", onClick: () =>
        {
            try { Support.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)); }
            catch (Exception) { }
        });
        listStack.Children.Add(kofiRow);

        var listHost = new Canvas
        {
            Width = 640,
            RenderTransformOrigin = new Point(0, 0),
            RenderTransform = new RotateTransform(-3)
        };
        listHost.Children.Add(listStack);
        frame.SetLeftList(listHost, 176);

        // Right Side: Detail Card
        var detailCard = TideKit.DetailCard(
            title: "X20CTL",
            sub: $"Native Controller Studio · {Scanner.ScanFlow.AppVersion}",
            body: "X20CTL is an independent, open-source controller studio for EasySMX pads.\n\nRemap buttons, shape stick and trigger response, build back-paddle macros, set vibration, and verify controller functionality, all locally on your PC. No cloud account is required, and changes stay guarded local drafts until written to supported hardware.",
            foot: "Desktop-control layout inspired by Gopher360 — GPL-3.0. See distributed open-source licenses.",
            buttons: new[] { "Check for updates", "Open GitHub" },
            width: 580,
            rot: 2);

        var sideStack = TideKit.SideStack(null, detailCard, 580);
        frame.SetSideStack(sideStack, 176);
        frame.SetNote("Credits · Software built for players");

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
            hints: new[] { "\u24B7 Back to Tools", "\u24B6 Toggle", "\u24BE Driver Status" },
            seed: 12);

        if (buildPcMode != null)
        {
            var pcmodeContent = buildPcMode();
            var scroll = new ScrollViewer
            {
                Width = 1200,
                Height = 650,
                Content = pcmodeContent,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            Canvas.SetLeft(scroll, 220);
            Canvas.SetTop(scroll, 170);
            pcmodeFrame.ContentArea.Children.Add(scroll);
        }

        board.Children.Add(pcmodeFrame);
    }
}
