using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Tide;
using X20Ctl.Zone;

namespace X20Ctl.Product;

public sealed partial class ModelSelectorView
{
    private void BuildTideSelector(ControllerCatalog catalog, Dictionary<string, Facts> facts)
    {
        var models = catalog.Models.ToDictionary(model => model.Id);

        var chooseItems = new (string Key, string ArtKey, string Name, string Specs, string Status)[]
        {
            ("x20", "x20", "EasySMX X20", "Hall sticks · Hall triggers · RGB · 4 macro paddles", "Full configuration"),
            ("x20_pro", "x20pro", "EasySMX X20 Pro", "TMR sticks · Hall / microswitch triggers · RGB · screen · trigger haptics · 6 macro paddles", "Full configuration"),
            ("x05_pro", "x05pro", "EasySMX X05 Pro", "Hall sticks · Hall / microswitch triggers · RGB · trigger haptics · 2 macro paddles", "Not available yet"),
            ("x10", "x10", "EasySMX X10", "Hall sticks · Hall triggers · 2 macro paddles", "Input testing · experimental"),
            ("d10", "d10-purple", "EasySMX D10", "TMR sticks · Hall / microswitch triggers · RGB · 2 macro paddles", "Input testing · experimental")
        };

        var board = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };

        // 1. Sea Background with "WHO ARE YOU"
        board.Children.Add(TideKit.SeaBackground(21, "WHO ARE YOU"));

        // Dynamic preview character on left
        int selectedIndex = 0;
        var padArtHost = new Canvas { Width = 600, Height = 600 };
        Canvas.SetLeft(padArtHost, -10);
        Canvas.SetTop(padArtHost, 440);
        board.Children.Add(padArtHost);

        void UpdateCharacter(string artKey)
        {
            padArtHost.Children.Clear();
            var art = TideKit.Character(artKey, 0, 0, 600, -22);
            TideKit.AnimatePulse(art, 0.98, 1.02, 3.5);
            padArtHost.Children.Add(art);
        }
        UpdateCharacter("x20");

        // 3. Status Box
        board.Children.Add(TideKit.StatusBox(ThemeClock.Now, isBack: true, onBack: () => Cancelled?.Invoke()));

        // 4. Top Row: Band + Round Badge + 4 tiles
        board.Children.Add(TideKit.TopBand(34));

        void SelectModel(int idx)
        {
            selectedIndex = idx;
            var it = chooseItems[idx];
            UpdateCharacter(it.ArtKey);
            if (models.TryGetValue(it.Key, out var mod))
            {
                Chosen?.Invoke(mod);
            }
        }

        // Round badge for x20 (Player 1 choice)
        var badgeTile = TideKit.RoundBadge("x20", 250);
        badgeTile.Cursor = System.Windows.Input.Cursors.Hand;
        badgeTile.MouseLeftButtonUp += (_, _) => SelectModel(0);
        Canvas.SetLeft(badgeTile, 200);
        Canvas.SetTop(badgeTile, 110);
        Panel.SetZIndex(badgeTile, 2);
        board.Children.Add(badgeTile);

        // Next 4 tiles: x20pro, x05pro, x10, d10
        for (int i = 1; i < chooseItems.Length; i++)
        {
            int mIdx = i;
            var item = chooseItems[i];
            var tile = TideKit.CoverTile(item.ArtKey, 250, 250, "setups");
            tile.Cursor = System.Windows.Input.Cursors.Hand;
            tile.MouseLeftButtonUp += (_, _) => SelectModel(mIdx);
            Canvas.SetLeft(tile, 490 + (i - 1) * 268);
            Canvas.SetTop(tile, 52);
            board.Children.Add(tile);
        }

        // 5. Slanted Menu Words at (660, 712)
        for (int i = 0; i < chooseItems.Length; i++)
        {
            int mIdx = i;
            var item = chooseItems[i];
            string shortName = item.Name.Replace("EasySMX ", "").ToUpperInvariant();
            var word = TideKit.SlantedWord(shortName, focused: (i == 0), fs: 60, onClick: () => SelectModel(mIdx));
            Canvas.SetLeft(word, 660 + i * 100);
            Canvas.SetTop(word, 712 - 60);
            board.Children.Add(word);
        }

        // 6. Tilted Keys at (600, 772):
        // [0] Select (\uE7FC) -> selects current model
        // [1] Cycle (\uE710) -> cycles model
        // [2] Cancel (\uE72B) -> cancels
        string[] cKeys = ["\uE7FC", "\uE710", "\uE72B"];
        for (int i = 0; i < cKeys.Length; i++)
        {
            int kIndex = i;
            var kBtn = TideKit.KeyButton(cKeys[i], focused: (i == 0), rot: -12, onClick: () =>
            {
                if (kIndex == 0) SelectModel(selectedIndex);
                else if (kIndex == 1) SelectModel((selectedIndex + 1) % chooseItems.Length);
                else if (kIndex == 2) Cancelled?.Invoke();
            });
            Canvas.SetLeft(kBtn, 600 + i * 132);
            Canvas.SetTop(kBtn, 772 - i * 12);
            board.Children.Add(kBtn);
        }

        // 7. Persona Card at (1216, 420, w=344) rotated 3deg
        var sideCard = TideKit.PersonaCard(344, 320, 3);
        var sideStack = new StackPanel { Margin = new Thickness(16, 12, 16, 12) };

        sideStack.Children.Add(new TextBlock
        {
            Text = $"PLAYER {Player + 1} · CONTROLLER ASSIGNMENT",
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontSize = 13,
            Foreground = TideKit.SpikeRedBrush
        });

        sideStack.Children.Add(new TextBlock
        {
            Text = "CHOOSE YOUR MODEL",
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontSize = 26,
            Foreground = TideKit.Sea0Brush,
            Margin = new Thickness(0, 2, 0, 0)
        });

        sideStack.Children.Add(new TextBlock
        {
            Text = "Select the physical controller you have connected. X20CTL will tailor remaps, stick response, and macro storage specifically for this hardware.",
            FontFamily = TideKit.Body,
            FontWeight = FontWeights.Bold,
            FontSize = 13,
            Foreground = TideKit.InkBrush,
            Margin = new Thickness(0, 6, 0, 10),
            TextWrapping = TextWrapping.Wrap
        });

        var badge = new Border
        {
            Padding = new Thickness(10, 4, 10, 4),
            Background = TideKit.Sea0Brush,
            CornerRadius = new CornerRadius(4),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        var bRow = new StackPanel { Orientation = Orientation.Horizontal };
        bRow.Children.Add(new TextBlock { Text = "\uE73E ", FontFamily = TideKit.Icons, FontSize = 12, Foreground = TideKit.WhiteBrush });
        bRow.Children.Add(new TextBlock { Text = "HARDWARE PROTECTED", FontFamily = TideKit.Display, FontWeight = FontWeights.Black, FontSize = 12, Foreground = TideKit.WhiteBrush });
        badge.Child = bRow;
        sideStack.Children.Add(badge);

        sideCard.Child = sideStack;
        Canvas.SetLeft(sideCard, 1216);
        Canvas.SetTop(sideCard, 420);
        board.Children.Add(sideCard);

        // 8. Hints
        var hintsBar = TideKit.HintsBar(new[] { "\u24B7 Cancel", "\u24B6 Assign Controller" });
        Canvas.SetRight(hintsBar, 40);
        Canvas.SetBottom(hintsBar, 26);
        board.Children.Add(hintsBar);

        TideKit.AnimateEntrance(board);
        Children.Clear();
        Children.Add(new Viewbox { Child = board, Stretch = Stretch.Uniform });
    }
}
