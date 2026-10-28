using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Tide;

namespace X20Ctl.Product;

public sealed partial class StudioScene
{
    private void TideShowSetups()
    {
        ConsoleSurface.Children.Clear();
        var board = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };

        // 1. Sea Background with "SETUPS"
        board.Children.Add(TideKit.SeaBackground(23, "SETUPS"));

        // 2. Character: x15-starfield at (-10, 440, w=600)
        board.Children.Add(TideKit.Character("x15-starfield", -10, 440, 600, -22));

        // 3. Status Box
        board.Children.Add(TideKit.StatusBox("19:24", isBack: true, onBack: () => TideShowDashboard()));

        // 4. Top Row of Setup Cards
        board.Children.Add(TideKit.TopBand(34));

        // Round badge
        var badgeTile = TideKit.RoundBadge("x15-starfield", 250);
        Canvas.SetLeft(badgeTile, 200);
        Canvas.SetTop(badgeTile, 110);
        Panel.SetZIndex(badgeTile, 2);
        board.Children.Add(badgeTile);

        // Setup Cards
        var setupCards = new (string Title, string Page)[]
        {
            ("DEFAULT PROFILE", "setups"),
            ("RACING RIG", "curves"),
            ("FPS TACTICAL", "vibration")
        };

        for (int i = 0; i < setupCards.Length; i++)
        {
            var card = setupCards[i];
            bool focused = (i == 0);
            var tileBorder = new Border
            {
                Width = 250,
                Height = 250,
                CornerRadius = new CornerRadius(6),
                BorderBrush = TideKit.WhiteBrush,
                BorderThickness = new Thickness(4),
                ClipToBounds = true,
                Effect = focused
                    ? new DropShadowEffect { BlurRadius = 0, ShadowDepth = 6, Color = TideKit.SpikeRed }
                    : new DropShadowEffect { BlurRadius = 18, ShadowDepth = 6, Direction = 270, Opacity = 0.45, Color = Color.FromArgb(255, 0, 0, 40) }
            };

            var innerCanvas = new Canvas { Width = 250, Height = 250 };
            var bg = new Rectangle
            {
                Width = 250,
                Height = 250,
                Fill = new RadialGradientBrush
                {
                    Center = new Point(0.5, 0.2),
                    RadiusX = 0.8,
                    RadiusY = 0.8,
                    GradientStops =
                    {
                        new(Color.FromRgb(0x68, 0x70, 0xb5), 0.0),
                        new(Color.FromRgb(0x1b, 0x1f, 0x3d), 1.0)
                    }
                }
            };
            innerCanvas.Children.Add(bg);

            var art = TideKit.ControllerArt("x15-starfield", 360);
            Canvas.SetLeft(art, -50);
            Canvas.SetTop(art, -30);
            innerCanvas.Children.Add(art);

            var titleBanner = new Border
            {
                Width = 250,
                Height = 36,
                Background = TideKit.InkBrush,
                Padding = new Thickness(12, 6, 12, 6)
            };
            titleBanner.Child = new TextBlock
            {
                Text = card.Title,
                FontFamily = TideKit.Display,
                FontWeight = FontWeights.Black,
                FontStyle = FontStyles.Italic,
                FontSize = 16,
                Foreground = TideKit.WhiteBrush
            };
            Canvas.SetTop(titleBanner, 214);
            innerCanvas.Children.Add(titleBanner);

            tileBorder.Child = innerCanvas;
            Canvas.SetLeft(tileBorder, 490 + i * 268);
            Canvas.SetTop(tileBorder, 52);
            board.Children.Add(tileBorder);
        }

        // Add Setup tile
        var addTile = new Border
        {
            Width = 250,
            Height = 250,
            CornerRadius = new CornerRadius(6),
            BorderBrush = TideKit.WhiteBrush,
            BorderThickness = new Thickness(4),
            ClipToBounds = true,
            Background = TideKit.Sea0Brush,
            Effect = new DropShadowEffect { BlurRadius = 18, ShadowDepth = 6, Direction = 270, Opacity = 0.45, Color = Color.FromArgb(255, 0, 0, 40) }
        };
        var addCanvas = new Canvas { Width = 250, Height = 250 };
        var addIcon = new TextBlock
        {
            Text = "\uE710",
            FontFamily = TideKit.Icons,
            FontSize = 80,
            Foreground = TideKit.WhiteBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        Canvas.SetLeft(addIcon, 85);
        Canvas.SetTop(addIcon, 60);
        addCanvas.Children.Add(addIcon);

        var addBanner = new Border
        {
            Width = 250,
            Height = 36,
            Background = TideKit.InkBrush,
            Padding = new Thickness(12, 6, 12, 6)
        };
        addBanner.Child = new TextBlock
        {
            Text = "NEW SETUP",
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 16,
            Foreground = TideKit.WhiteBrush
        };
        Canvas.SetTop(addBanner, 214);
        addCanvas.Children.Add(addBanner);
        addTile.Child = addCanvas;
        Canvas.SetLeft(addTile, 490 + 3 * 268);
        Canvas.SetTop(addTile, 52);
        board.Children.Add(addTile);

        // 5. Slanted Menu Words at (660, 712)
        string[] setupActions = ["APPLY", "RENAME", "DUPLICATE", "DELETE"];
        for (int i = 0; i < setupActions.Length; i++)
        {
            var word = TideKit.SlantedWord(setupActions[i], focused: (i == 0), fs: 64);
            Canvas.SetLeft(word, 660 + i * 118);
            Canvas.SetTop(word, 712 - 64);
            board.Children.Add(word);
        }

        // 6. Tilted Keys at (600, 772): setups, E8B5, add
        string[] sKeys = ["\uE8B5", "\uE74E", "\uE710"];
        for (int i = 0; i < sKeys.Length; i++)
        {
            var kBtn = TideKit.KeyButton(sKeys[i], focused: (i == 0), rot: -12);
            Canvas.SetLeft(kBtn, 600 + i * 132);
            Canvas.SetTop(kBtn, 772 - i * 12);
            board.Children.Add(kBtn);
        }

        // 7. Persona Card with Setup Details at (1200, 440, w=360) rotated 3deg
        var detailCard = TideKit.PersonaCard(360, 360, 3);
        var detailStack = new StackPanel();

        detailStack.Children.Add(new TextBlock
        {
            Text = "ACTIVE PROFILE",
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontSize = 15,
            Foreground = TideKit.SpikeRedBrush
        });
        detailStack.Children.Add(new TextBlock
        {
            Text = "DEFAULT PROFILE",
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontSize = 28,
            Foreground = TideKit.Sea0Brush,
            Margin = new Thickness(0, 2, 0, 0)
        });
        detailStack.Children.Add(new TextBlock
        {
            Text = "EasySMX X15 · Matching controller · Saved to disk",
            FontFamily = TideKit.Body,
            FontWeight = FontWeights.Bold,
            FontSize = 14,
            Foreground = TideKit.InkBrush,
            Margin = new Thickness(0, 4, 0, 12)
        });

        var stats = new (string Label, string Value)[]
        {
            ("BUTTON REMAPS", "4 ACTIVE"),
            ("STICK RESPONSE", "1:1 LINEAR"),
            ("VIBRATION LEVEL", "80% BALANCED"),
            ("POLLING RATE", "1000 HZ")
        };

        foreach (var (lbl, val) in stats)
        {
            var statRow = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            statRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            statRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var lBlock = new TextBlock
            {
                Text = lbl,
                FontFamily = TideKit.Display,
                FontWeight = FontWeights.Black,
                FontSize = 14,
                Foreground = TideKit.InkBrush
            };
            Grid.SetColumn(lBlock, 0);
            statRow.Children.Add(lBlock);

            var vBlock = new TextBlock
            {
                Text = val,
                FontFamily = TideKit.Display,
                FontWeight = FontWeights.Black,
                FontSize = 14,
                Foreground = TideKit.SpikeRedBrush
            };
            Grid.SetColumn(vBlock, 1);
            statRow.Children.Add(vBlock);

            detailStack.Children.Add(statRow);
        }

        detailCard.Child = detailStack;
        Canvas.SetLeft(detailCard, 1200);
        Canvas.SetTop(detailCard, 440);
        board.Children.Add(detailCard);

        // 8. Hints
        var hintsBar = TideKit.HintsBar(new[] { "\u24B7 Back", "\u24B6 Select", "\u24BE New Setup" });
        Canvas.SetRight(hintsBar, 40);
        Canvas.SetBottom(hintsBar, 26);
        board.Children.Add(hintsBar);

        ConsoleSurface.Children.Add(new Viewbox { Stretch = Stretch.Uniform, Child = board });
    }
}
