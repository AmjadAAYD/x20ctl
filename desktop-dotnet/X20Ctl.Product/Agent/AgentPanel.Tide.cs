using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Tide;

namespace X20Ctl.Product.Agent;

public partial class AgentPanel
{
    private void BuildTideAgent(string query = "", string answer = "")
    {
        var board = new Canvas { Name = "TideAgent", Width = 1600, Height = 900, ClipToBounds = true };

        // 1. Sea Background with "AGENT XCTL"
        board.Children.Add(TideKit.SeaBackground(27, "AGENT XCTL"));

        // 2. Character: x15-starfield at (1120, 440, w=420) rotated 14deg
        board.Children.Add(TideKit.Character("x15-starfield", 1120, 440, 420, 14));

        // 3. Status Box
        board.Children.Add(TideKit.StatusBox(ThemeClock.Now, isBack: true, onBack: () => CloseRequested?.Invoke()));

        // 4. Tabs at (220, 104)
        var tabsRow = new StackPanel { Orientation = Orientation.Horizontal };
        string[] chips = ["ASK X20CTL", "COMMUNITY PROFILES", "HARDWARE ADVICE"];
        for (int i = 0; i < chips.Length; i++)
        {
            var tabBtn = TideKit.PersonaButton(chips[i], primary: (i == 0));
            tabBtn.Margin = new Thickness(0, 0, 14, 0);
            tabsRow.Children.Add(tabBtn);
        }
        Canvas.SetLeft(tabsRow, 220);
        Canvas.SetTop(tabsRow, 104);
        board.Children.Add(tabsRow);

        // 5. Chat Bubbles at (220, 190, w=1000)
        var chatStack = new StackPanel { Width = 1000 };

        // Hello bubble
        var bub1 = new Border
        {
            MaxWidth = 640,
            HorizontalAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(18, 12, 18, 12),
            Background = TideKit.WhiteBrush,
            BorderBrush = TideKit.InkBrush,
            BorderThickness = new Thickness(4),
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new RotateTransform(-1.5),
            Effect = new DropShadowEffect { BlurRadius = 0, ShadowDepth = 8, Direction = 315, Opacity = 0.6, Color = TideKit.Sea0 }
        };
        var b1Stack = new StackPanel();
        b1Stack.Children.Add(new TextBlock
        {
            Text = "X15",
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 14,
            Foreground = TideKit.SpikeRedBrush
        });
        b1Stack.Children.Add(new TextBlock
        {
            Text = "I can inspect controller configuration, test inputs, and adjust dead zones. Ask me anything about X15.",
            FontFamily = TideKit.Body,
            FontWeight = FontWeights.Bold,
            FontSize = 16,
            Foreground = TideKit.InkBrush,
            Margin = new Thickness(0, 4, 0, 0),
            TextWrapping = TextWrapping.Wrap
        });
        bub1.Child = b1Stack;
        chatStack.Children.Add(bub1);

        // User query bubble
        string qText = !string.IsNullOrWhiteSpace(query) ? query : "what does the dead zone do";
        var bub2 = new Border
        {
            MaxWidth = 640,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 20, 180, 0),
            Padding = new Thickness(18, 12, 18, 12),
            Background = TideKit.Sea0Brush,
            BorderBrush = TideKit.WhiteBrush,
            BorderThickness = new Thickness(4),
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new RotateTransform(1.5),
            Effect = new DropShadowEffect { BlurRadius = 0, ShadowDepth = 8, Direction = 315, Opacity = 0.6, Color = TideKit.Ink }
        };
        var b2Stack = new StackPanel();
        b2Stack.Children.Add(new TextBlock
        {
            Text = "YOU",
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 14,
            Foreground = TideKit.LightCyanBrush
        });
        b2Stack.Children.Add(new TextBlock
        {
            Text = qText,
            FontFamily = TideKit.Body,
            FontWeight = FontWeights.Bold,
            FontSize = 16,
            Foreground = TideKit.WhiteBrush,
            Margin = new Thickness(0, 4, 0, 0),
            TextWrapping = TextWrapping.Wrap
        });
        bub2.Child = b2Stack;
        chatStack.Children.Add(bub2);

        // Answer bubble
        string aText = !string.IsNullOrWhiteSpace(answer)
            ? answer
            : "The dead zone ignores small movements near stick center to prevent unintended drift. A 4% to 6% dead zone gives competitive stability while preserving instant responsiveness.";

        var bub3 = new Border
        {
            MaxWidth = 860,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 20, 0, 0),
            Padding = new Thickness(18, 14, 18, 14),
            Background = TideKit.WhiteBrush,
            BorderBrush = TideKit.InkBrush,
            BorderThickness = new Thickness(4),
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new RotateTransform(-1.0),
            Effect = new DropShadowEffect { BlurRadius = 0, ShadowDepth = 8, Direction = 315, Opacity = 0.6, Color = TideKit.Sea0 }
        };
        var b3Stack = new StackPanel();
        b3Stack.Children.Add(new TextBlock
        {
            Text = "DEAD ZONE EXPLAINED",
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 16,
            Foreground = TideKit.Sea0Brush,
            Margin = new Thickness(0, 0, 0, 4)
        });
        b3Stack.Children.Add(new TextBlock
        {
            Text = aText,
            FontFamily = TideKit.Body,
            FontWeight = FontWeights.Bold,
            FontSize = 15,
            Foreground = TideKit.InkBrush,
            TextWrapping = TextWrapping.Wrap
        });
        b3Stack.Children.Add(new TextBlock
        {
            Text = "No controller memory changes made · Settings remain in local draft",
            FontFamily = TideKit.Body,
            FontWeight = FontWeights.Bold,
            FontSize = 13,
            Foreground = TideKit.SubBrush,
            Margin = new Thickness(0, 8, 0, 0)
        });
        bub3.Child = b3Stack;
        chatStack.Children.Add(bub3);

        Canvas.SetLeft(chatStack, 220);
        Canvas.SetTop(chatStack, 190);
        board.Children.Add(chatStack);

        // 6. Slanted Input Row at (240, 770, w=760) rotated -3deg
        var inputBorder = new Border
        {
            Width = 760,
            Height = 56,
            Background = TideKit.WhiteBrush,
            Padding = new Thickness(18, 8, 18, 8),
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new RotateTransform(-3),
            Effect = new DropShadowEffect { BlurRadius = 0, ShadowDepth = 7, Direction = 315, Opacity = 1.0, Color = TideKit.Ink }
        };
        var inputGrid = new Grid();
        inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var inputPrompt = new TextBlock
        {
            Text = "Type a question or command...",
            FontFamily = TideKit.Body,
            FontWeight = FontWeights.Bold,
            FontSize = 17,
            Foreground = TideKit.InkBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(inputPrompt, 0);
        inputGrid.Children.Add(inputPrompt);

        var sendBtn = TideKit.PersonaButton("\uE724", primary: true);
        Grid.SetColumn(sendBtn, 1);
        inputGrid.Children.Add(sendBtn);

        inputBorder.Child = inputGrid;
        Canvas.SetLeft(inputBorder, 240);
        Canvas.SetTop(inputBorder, 770);
        board.Children.Add(inputBorder);

        // 7. Hints
        var hintsBar = TideKit.HintsBar(new[] { "LB Undo", "RB Exit", "\u24B6 Send", "\u24B7 Back" });
        Canvas.SetRight(hintsBar, 40);
        Canvas.SetBottom(hintsBar, 26);
        board.Children.Add(hintsBar);

        Body.Child = new Viewbox { Child = board, Stretch = Stretch.Uniform };
        Body.Background = TideKit.Sea0Brush;
    }
}
