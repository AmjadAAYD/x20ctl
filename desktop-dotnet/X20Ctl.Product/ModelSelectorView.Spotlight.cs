using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Spotlight;
using X20Ctl.Zone;

namespace X20Ctl.Product;

public sealed partial class ModelSelectorView
{
    private void BuildSpotlightSelector(ControllerCatalog catalog, Dictionary<string, Facts> facts)
    {
        var board = new Canvas { Name = "SpotlightChoose", Width = 1600, Height = 900, ClipToBounds = true };

        // 1. Full Theatrical stage at floorY = 640 with left velvet leg (t_spotlight.py:470)
        board.Children.Add(SpotlightKit.TheatricalStage(floorY: 640, legs: false, valance: true, leftLeg: true, leftLegW: 150));

        // 2. Follow spot on chosen center controller at cx = 800, fy = 680, rx = 260
        var spot = SpotlightKit.FollowSpot(800, 680, 260, -60, 0.30);
        board.Children.Add(spot);

        // 3. Header at (150, 90)
        var headStack = new StackPanel { Width = 760 };
        headStack.Children.Add(new TextBlock
        {
            Text = $"PLAYER {Player + 1}",
            FontFamily = SpotlightKit.Display,
            FontSize = 12,
            FontWeight = FontWeights.Light,
            Foreground = SpotlightKit.GoldBrush
        });
        headStack.Children.Add(new TextBlock
        {
            Text = "CHOOSE CONTROLLER",
            FontFamily = SpotlightKit.Display,
            FontSize = 40,
            FontWeight = FontWeights.Light,
            Foreground = SpotlightKit.B("#f3e7d3"),
            Margin = new Thickness(0, 10, 0, 0)
        });
        headStack.Children.Add(new TextBlock
        {
            Text = "Select a controller model to assign to this slot. Settings and button mappings will load automatically.",
            FontFamily = SpotlightKit.Body,
            FontStyle = FontStyles.Italic,
            FontSize = 17,
            Foreground = SpotlightKit.B("#b3a48e"),
            Margin = new Thickness(0, 8, 0, 0),
            TextWrapping = TextWrapping.Wrap
        });
        Canvas.SetLeft(headStack, 150);
        Canvas.SetTop(headStack, 90);
        board.Children.Add(headStack);

        // Status at top-right
        var status = SpotlightParts.StatusHeader(right: 60, top: 82);
        Canvas.SetRight(status, 60);
        Canvas.SetTop(status, 82);
        board.Children.Add(status);

        // 4. Controller line-up on stage marks: xs = [800, 470, 1130, 240, 1360]
        string[] modelKeys = { "x20", "x15", "x20pro", "x10", "d10" };
        string[] modelNames = { "X20 Pro", "X15 Elite", "X20 Series", "X10 Compact", "D10 Fighter" };
        string[] modelSpecs =
        {
            "Dual vibration · Hall-effect sticks · 1000 Hz polling",
            "Linear triggers · Tactile microswitches · USB-C",
            "Hall-effect triggers · 4 rear paddles · Bluetooth 5.3",
            "Compact layout · Mechanical D-pad · 500 Hz",
            "Fightpad 6-button front · Microswitches · Tournament mode"
        };

        int activeIdx = 0;
        double[] xs = { 800, 470, 1130, 240, 1360 };

        var lineupCanvas = new Canvas { Width = 1600, Height = 900 };
        board.Children.Add(lineupCanvas);

        var cueHost = new Border();
        Canvas.SetLeft(cueHost, 420);
        Canvas.SetTop(cueHost, 722);
        board.Children.Add(cueHost);

        void RenderLineup()
        {
            lineupCanvas.Children.Clear();

            for (int i = 0; i < xs.Length; i++)
            {
                double x = xs[i];
                bool on = (i == activeIdx);
                double w = on ? 380 : (230 - Math.Abs(x - 800) / 10.0);

                var mark = SpotlightKit.StageMark(x, 690, on: on, size: 18);
                lineupCanvas.Children.Add(mark);

                var star = SpotlightParts.SpotlightStar(modelKeys[i], x, 650, w, reflection: on ? 0.18 : 0.06);
                if (!on) star.Opacity = 0.55;
                lineupCanvas.Children.Add(star);
            }

            // Cue card at (420, 722, w = 760)
            var cardGrid = new Grid();
            cardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            cardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            infoStack.Children.Add(new TextBlock
            {
                Text = modelNames[activeIdx],
                FontFamily = SpotlightKit.Body,
                FontSize = 27,
                Foreground = SpotlightKit.ChalkBrush
            });
            infoStack.Children.Add(new TextBlock
            {
                Text = modelSpecs[activeIdx],
                FontFamily = SpotlightKit.Body,
                FontStyle = FontStyles.Italic,
                FontSize = 14,
                Foreground = SpotlightKit.MuteBrush,
                Margin = new Thickness(0, 4, 0, 0)
            });
            Grid.SetColumn(infoStack, 0);
            cardGrid.Children.Add(infoStack);

            var tag = SpotlightKit.Tag("Assigned", true);
            Grid.SetColumn(tag, 1);
            cardGrid.Children.Add(tag);

            var cueCard = SpotlightKit.CueCard(cardGrid, 760, padding: new Thickness(32, 22, 32, 22));
            cueHost.Child = cueCard;
        }

        RenderLineup();

        // 5. Hints at bottom-right
        var hints = SpotlightParts.HintsBar(new[] { ("A", "Assign"), ("◄►", "Navigate"), ("B", "Cancel") });
        Canvas.SetRight(hints, 60);
        Canvas.SetTop(hints, 858);
        board.Children.Add(hints);

        // Replace View
        Children.Clear();
        Children.Add(new Viewbox { Stretch = Stretch.Uniform, Child = board });
    }
}
