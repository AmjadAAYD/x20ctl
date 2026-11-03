using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Spotlight;

namespace X20Ctl.Product.Agent;

public partial class AgentPanel
{
    private void BuildSpotlightAgent()
    {
        var board = new Canvas { Name = "SpotlightAgent", Width = 1600, Height = 900, ClipToBounds = true };

        // 1. Stage background
        board.Children.Add(SpotlightKit.TheatricalStage(floorY: 900, legs: false, valance: true, leftLeg: true, leftLegW: 150));

        // Soft spotlight
        var spot = SpotlightKit.FollowSpot(330, 900, 300, -60, 0.12, pool: false);
        board.Children.Add(spot);

        // 2. Logo at (190, 100), width 84
        var logoImg = SpotlightParts.GetThemeLogo();
        if (logoImg != null)
        {
            var logo = new Image
            {
                Source = logoImg, Width = 84,
                Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 10, ShadowDepth = 6, Opacity = 0.7 }
            };
            Canvas.SetLeft(logo, 190);
            Canvas.SetTop(logo, 100);
            board.Children.Add(logo);
        }

        // Header at (296, 104)
        var headStack = new StackPanel();
        headStack.Children.Add(new TextBlock
        {
            Text = "AGENT XCTL",
            FontFamily = SpotlightKit.Display,
            FontSize = 34,
            FontWeight = FontWeights.Light,
            Foreground = SpotlightKit.B("#f3e7d3")
        });
        headStack.Children.Add(new TextBlock
        {
            Text = "Settings & voice options",
            FontFamily = SpotlightKit.Body,
            FontStyle = FontStyles.Italic,
            FontSize = 14,
            Foreground = SpotlightKit.MuteBrush,
            Margin = new Thickness(0, 2, 0, 0)
        });
        Canvas.SetLeft(headStack, 296);
        Canvas.SetTop(headStack, 104);
        board.Children.Add(headStack);

        // Tag: Ready at (700, 116)
        var readyTag = SpotlightKit.Tag("Ready", true);
        Canvas.SetLeft(readyTag, 700);
        Canvas.SetTop(readyTag, 116);
        board.Children.Add(readyTag);

        // Shell actions at (1150, 112)
        var shellStack = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var (k, label, act) in new (string, string, Action)[] { ("X", "New", Restart), ("Y", "Settings", () => ShowSettingsPage(!SettingsOpen)), ("B", "Close", LeaveAgent) })
        {
            var item = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 22, 0), VerticalAlignment = VerticalAlignment.Center };
            item.Children.Add(SpotlightKit.BrassKey(k, false, 24));
            item.Children.Add(new TextBlock
            {
                Text = label,
                FontFamily = SpotlightKit.Body,
                FontSize = 16,
                Foreground = SpotlightKit.ChalkDimBrush,
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            shellStack.Children.Add(ChipButton(item, act));
        }
        Canvas.SetLeft(shellStack, 1150);
        Canvas.SetTop(shellStack, 112);
        board.Children.Add(shellStack);

        // Topic chips at (296, 186)
        var topicRow = new StackPanel { Orientation = Orientation.Horizontal };
        string[] topics = { "Dead zone", "Drift check", "Turbo", "Remap" };
        for (int i = 0; i < topics.Length; i++)
        {
            var chip = SpotlightKit.Chip(topics[i], i == 0);
            chip.Margin = new Thickness(0, 0, 10, 0);
            string topic = new[] { "dead zone", "drift", "turbo", "remap" }[i];
            topicRow.Children.Add(ChipButton(chip, () => { Search.Text = topic; ShowView(View.Ideas); }));
        }
        Canvas.SetLeft(topicRow, 296);
        Canvas.SetTop(topicRow, 186);
        board.Children.Add(topicRow);

        // 3. The real chat, ideas, check-up and settings on a dark stage card (cream script paper made the real light text unreadable)
        var stageCard = new Border { Width = 1260, Height = 480, CornerRadius = new CornerRadius(4), Background = SpotlightKit.B("#d9120a0c"), BorderBrush = SpotlightKit.GoldBrush, BorderThickness = new Thickness(1) };
        Canvas.SetLeft(stageCard, 296); Canvas.SetTop(stageCard, 240);
        board.Children.Add(stageCard);
        MountRealViews(board, 296, 240, 1260, 480, SpotlightKit.B("#ff120a0c"), SpotlightKit.GoldBrush);

        // 4. Cue Card Input Row at (296, 740, w = 1260)
        var inputInner = new Grid();
        inputInner.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        inputInner.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        inputInner.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var micIcon = new TextBlock
        {
            Text = "\uEE56", FontFamily = SpotlightKit.Icons, FontSize = 19, Foreground = SpotlightKit.GoldBrush,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 18, 0)
        };
        Grid.SetColumn(micIcon, 0); inputInner.Children.Add(micIcon);
        StyleRealInput(SpotlightKit.Body, 19, SpotlightKit.B("#f3e7d3"));
        Grid.SetColumn(Input, 1); inputInner.Children.Add(Input);
        var sendBtn = ChipButton(SpotlightKit.TheatricalButton("Send", true, "A", height: 40, fontSize: 12), ClickSend);
        Grid.SetColumn(sendBtn, 2); inputInner.Children.Add(sendBtn);
        var cueCard = SpotlightKit.CueCard(inputInner, 1260, padding: new Thickness(26, 16, 26, 16));
        Canvas.SetLeft(cueCard, 296);
        Canvas.SetTop(cueCard, 740);
        board.Children.Add(cueCard);

        // 5. Hints at (right: 60, top: 862)
        var hints = SpotlightParts.HintsBar(new[] { ("Enter", "Send"), ("Esc", "Back"), ("X", "New") });
        Canvas.SetRight(hints, 60);
        Canvas.SetTop(hints, 862);
        board.Children.Add(hints);

        // Replace agent content
        Body.Child = new Viewbox { Child = board, Stretch = Stretch.Uniform };
        Body.Background = SpotlightKit.HouseBrush;
    }
}
