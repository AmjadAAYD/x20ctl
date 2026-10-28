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

        // 2. Character: x20 at (-10, 440, w=600)
        board.Children.Add(TideKit.Character("x20", -10, 440, 600, -22));

        // 3. Status Box
        board.Children.Add(TideKit.StatusBox("19:24", isBack: true, onBack: () => Cancelled?.Invoke()));

        // 4. Top Row: Band + Round Badge + 4 tiles
        board.Children.Add(TideKit.TopBand(34));

        // Round badge for x20
        var badgeTile = TideKit.RoundBadge("x20", 250);
        Canvas.SetLeft(badgeTile, 200);
        Canvas.SetTop(badgeTile, 110);
        Panel.SetZIndex(badgeTile, 2);
        board.Children.Add(badgeTile);

        // Next 4 tiles: x20pro, x05pro, x10, d10
        for (int i = 1; i < chooseItems.Length; i++)
        {
            var item = chooseItems[i];
            var tile = TideKit.CoverTile(item.ArtKey, 250, 250, "setups");
            Canvas.SetLeft(tile, 490 + (i - 1) * 268);
            Canvas.SetTop(tile, 52);
            board.Children.Add(tile);
        }

        // 5. Slanted Menu Words at (660, 712)
        for (int i = 0; i < chooseItems.Length; i++)
        {
            var item = chooseItems[i];
            string shortName = item.Name.Replace("EasySMX ", "").ToUpperInvariant();
            var word = TideKit.SlantedWord(shortName, focused: (i == 0), fs: 60, onClick: () =>
            {
                if (models.TryGetValue(item.Key, out var mod))
                {
                    Chosen?.Invoke(mod);
                }
            });
            Canvas.SetLeft(word, 660 + i * 100);
            Canvas.SetTop(word, 712 - 60);
            board.Children.Add(word);
        }

        // 6. Tilted Keys at (600, 772): zone, add, settings
        string[] cKeys = ["\uE7FC", "\uE710", "\uE713"];
        for (int i = 0; i < cKeys.Length; i++)
        {
            var kBtn = TideKit.KeyButton(cKeys[i], focused: (i == 0), rot: -12);
            Canvas.SetLeft(kBtn, 600 + i * 132);
            Canvas.SetTop(kBtn, 772 - i * 12);
            board.Children.Add(kBtn);
        }

        // 7. Persona Card at (1216, 420, w=344) rotated 3deg
        var sideCard = TideKit.PersonaCard(344, 320, 3);
        var sideStack = new StackPanel();

        sideStack.Children.Add(new TextBlock
        {
            Text = $"PLAYER {Player + 1} · WHICH CONTROLLER IS THIS?",
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontSize = 14,
            Foreground = TideKit.SpikeRedBrush
        });
        sideStack.Children.Add(new TextBlock
        {
            Text = "EASYSMX X20",
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontSize = 28,
            Foreground = TideKit.Sea0Brush,
            Margin = new Thickness(0, 2, 0, 0)
        });
        sideStack.Children.Add(new TextBlock
        {
            Text = "Hall sticks · Hall triggers · RGB · 4 macro paddles",
            FontFamily = TideKit.Body,
            FontWeight = FontWeights.Bold,
            FontSize = 14,
            Foreground = TideKit.InkBrush,
            Margin = new Thickness(0, 6, 0, 10),
            TextWrapping = TextWrapping.Wrap
        });

        var badge = new Border
        {
            Padding = new Thickness(12, 6, 12, 6),
            Background = TideKit.Sea0Brush,
            CornerRadius = new CornerRadius(4),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        var bRow = new StackPanel { Orientation = Orientation.Horizontal };
        bRow.Children.Add(new TextBlock { Text = "\uE73E ", FontFamily = TideKit.Icons, FontSize = 13, Foreground = TideKit.WhiteBrush });
        bRow.Children.Add(new TextBlock { Text = "FULL CONFIGURATION", FontFamily = TideKit.Display, FontWeight = FontWeights.Black, FontSize = 13, Foreground = TideKit.WhiteBrush });
        badge.Child = bRow;
        sideStack.Children.Add(badge);

        sideStack.Children.Add(new TextBlock
        {
            Text = "Pick the model in your hands. This sets its layout and tools; connecting the hardware is a separate step.",
            FontFamily = TideKit.Body,
            FontWeight = FontWeights.Bold,
            FontSize = 13,
            Foreground = TideKit.SubBrush,
            Margin = new Thickness(0, 10, 0, 0),
            TextWrapping = TextWrapping.Wrap
        });

        sideCard.Child = sideStack;
        Canvas.SetLeft(sideCard, 1216);
        Canvas.SetTop(sideCard, 420);
        board.Children.Add(sideCard);

        // 8. Hints
        var hintsBar = TideKit.HintsBar(new[] { "\u24B6 Choose", "\u24B7 Back", "\u271D Move" });
        Canvas.SetRight(hintsBar, 40);
        Canvas.SetBottom(hintsBar, 26);
        board.Children.Add(hintsBar);

        Children.Clear();
        Children.Add(new Viewbox { Stretch = Stretch.Uniform, Child = board });
    }
}
