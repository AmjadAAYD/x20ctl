using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Tide;
using X20Ctl.Zone;

namespace X20Ctl.Product;

/// <summary>
/// Controller Zone and Hero presentation for the Tide theme:
/// Persona 3 aesthetic with deep blue sea, slanted menu words, tilted black keys,
/// sticker cards, round badge top tile, and comic character pad.
/// Strictly implements t_tide.py:103 zone() and t_tide.py:111 hero().
/// </summary>
public partial class ZoneScene
{
    internal bool IsTide { get; private set; }

    private Grid? tideBoard;
    private Canvas? tideZoneBack, tideHeroBack;

    private void BuildTide()
    {
        IsTide = true;
        EnvironmentBackdrop.Visibility = Visibility.Collapsed;
        OuterFrame.Background = TideKit.Sea0Brush;
        OuterFrame.BorderBrush = TideKit.Sea0Brush;

        tideBoard = new Grid
        {
            Width = 1600,
            Height = 900,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            ClipToBounds = true
        };

        // 1. Zone Overview Stage
        tideZoneBack = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };
        tideZoneBack.Children.Add(TideKit.SeaBackground(3, "CONTROLLER ZONE"));
        tideZoneBack.Children.Add(TideKit.Character("x15-starfield", -10, 440, 600, -22));
        tideZoneBack.Children.Add(TideKit.StatusBox("19:24"));

        // Top Row: Band + Round badge + 4 player tiles + arrow
        tideZoneBack.Children.Add(TideKit.TopBand(34));

        // Round badge for Player 1
        var badgeTile = TideKit.RoundBadge("x15-starfield", 250);
        Canvas.SetLeft(badgeTile, 200);
        Canvas.SetTop(badgeTile, 110);
        Panel.SetZIndex(badgeTile, 2);
        tideZoneBack.Children.Add(badgeTile);

        // Next 4 tiles: x20, dune, d10-purple, x20pro at x = 490, 758, 1026, 1294
        string[] tileModels = ["x20", "dune", "d10-purple", "x20pro"];
        for (int i = 0; i < tileModels.Length; i++)
        {
            var tile = TideKit.CoverTile(tileModels[i], 250, 250, "setups");
            Canvas.SetLeft(tile, 490 + i * 268);
            Canvas.SetTop(tile, 52);
            tideZoneBack.Children.Add(tile);
        }

        // Chevron arrow at (right: 22, top: 160)
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
        tideZoneBack.Children.Add(arrow);

        // Slanted Menu Words at (660, 712): PLAYER 1, PLAYER 2, PLAYER 3, PLAYER 4
        string[] playerWords = ["PLAYER 1", "PLAYER 2", "PLAYER 3", "PLAYER 4"];
        for (int i = 0; i < playerWords.Length; i++)
        {
            int pIndex = i;
            var word = TideKit.SlantedWord(playerWords[i], focused: (i == 0), fs: 76, onClick: () =>
            {
                FocusPlayerAsync(pIndex);
            });
            Canvas.SetLeft(word, 660 + i * 128);
            Canvas.SetTop(word, 712 - 76);
            tideZoneBack.Children.Add(word);
        }

        // Tilted Keys at (600, 772): add, zone, tools
        string[] keys = ["\uE710", "\uE7FC", "\uE713"];
        for (int i = 0; i < keys.Length; i++)
        {
            var kBtn = TideKit.KeyButton(keys[i], focused: (i == 0), rot: -12);
            Canvas.SetLeft(kBtn, 600 + i * 132);
            Canvas.SetTop(kBtn, 772 - i * 12);
            tideZoneBack.Children.Add(kBtn);
        }

        // Avatars bottom right
        tideZoneBack.Children.Add(TideKit.AvatarGrid(["x15-starfield", "x10", null, null], 1300, 650));

        // Hints at bottom right
        var hintsBar = TideKit.HintsBar(new[] { "\u24BE Change", "\u24B6 Select" });
        Canvas.SetRight(hintsBar, 40);
        Canvas.SetBottom(hintsBar, 26);
        tideZoneBack.Children.Add(hintsBar);

        tideBoard.Children.Add(tideZoneBack);

        // 2. Hero Stage
        tideHeroBack = new Canvas { Width = 1600, Height = 900, ClipToBounds = true, Visibility = Visibility.Collapsed };
        tideHeroBack.Children.Add(TideKit.SeaBackground(5, "PLAYER 1"));
        tideHeroBack.Children.Add(TideKit.Character("x15-starfield", 40, 360, 640, -16));

        // Status box with Go Back
        var backBox = TideKit.StatusBox(isBack: true, onBack: () => FocusPlayerAsync(null));
        tideHeroBack.Children.Add(backBox);

        // Header crumb at right: 40, top: 40
        var crumb = new TextBlock
        {
            Text = "CONTROLLER ZONE / PLAYER 1",
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontSize = 22,
            Foreground = TideKit.WhiteBrush
        };
        Canvas.SetRight(crumb, 40);
        Canvas.SetTop(crumb, 40);
        tideHeroBack.Children.Add(crumb);

        // Persona Card at (220, 120) rotated -4deg
        var heroCard = new Border
        {
            Background = TideKit.WhiteBrush,
            BorderBrush = TideKit.InkBrush,
            BorderThickness = new Thickness(4),
            Padding = new Thickness(24, 14, 24, 14),
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new RotateTransform(-4),
            Effect = new DropShadowEffect { BlurRadius = 0, ShadowDepth = 10, Direction = 315, Opacity = 0.6, Color = TideKit.Sea0 }
        };
        var cardStack = new StackPanel();
        cardStack.Children.Add(new TextBlock
        {
            Text = "PLAYER 1",
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontSize = 18,
            Foreground = TideKit.SpikeRedBrush
        });
        cardStack.Children.Add(new TextBlock
        {
            Text = "X15 STARFIELD",
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontSize = 48,
            Foreground = TideKit.Sea0Brush,
            Margin = new Thickness(0, 2, 0, 0)
        });
        cardStack.Children.Add(new TextBlock
        {
            Text = "WIRED · 100% · 1000 HZ · ACTIVE",
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontSize = 16,
            Foreground = TideKit.InkBrush,
            Margin = new Thickness(0, 4, 0, 0)
        });
        heroCard.Child = cardStack;
        Canvas.SetLeft(heroCard, 220);
        Canvas.SetTop(heroCard, 120);
        tideHeroBack.Children.Add(heroCard);

        // Slanted Menu Words for Hero actions at (690, 822)
        string[] heroActions = ["OPEN STUDIO", "CHANGE CONTROLLER", "DISCONNECT", "REMOVE"];
        for (int i = 0; i < heroActions.Length; i++)
        {
            int aIndex = i;
            var word = TideKit.SlantedWord(heroActions[i], focused: (i == 0), fs: 52, onClick: () =>
            {
                if (aIndex == 0) ControllerActionRequested?.Invoke(0, false);
                else if (aIndex == 1) ControllerActionRequested?.Invoke(0, true);
                else FocusPlayerAsync(null);
            });
            Canvas.SetLeft(word, 690 + i * 102);
            Canvas.SetTop(word, 822 - 52);
            tideHeroBack.Children.Add(word);
        }

        // Hints for Hero: Ⓑ Back, Ⓐ Open
        var heroHints = TideKit.HintsBar(new[] { "\u24B7 Back", "\u24B6 Open" });
        Canvas.SetRight(heroHints, 40);
        Canvas.SetBottom(heroHints, 26);
        tideHeroBack.Children.Add(heroHints);

        tideBoard.Children.Add(tideHeroBack);

        OuterFrame.Child = tideBoard;
    }

    private void TideSync()
    {
        if (!IsTide || tideBoard == null) return;

        bool hero = State.FocusedPlayer.HasValue;
        if (hero)
        {
            tideZoneBack!.Visibility = Visibility.Collapsed;
            tideHeroBack!.Visibility = Visibility.Visible;
            for (int i = 0; i < docks.Length; i++) docks[i].Visibility = Visibility.Collapsed;
        }
        else
        {
            tideZoneBack!.Visibility = Visibility.Visible;
            tideHeroBack!.Visibility = Visibility.Collapsed;
            for (int i = 0; i < docks.Length; i++) docks[i].Visibility = Visibility.Visible;
        }
    }

    private async Task TideFocus(int? index)
    {
        if (index.HasValue) State.Activate(index.Value); else State.Collapse();
        TideSync();
        await Task.CompletedTask;
    }
}
