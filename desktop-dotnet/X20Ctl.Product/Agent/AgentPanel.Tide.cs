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
    private void BuildTideAgent()
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
        string[] chips = ["AIM & STICKS", "TRIGGERS", "BUTTONS", "PC MODE"];
        string[] topics = ["sticks", "triggers", "buttons", "PC mode"];
        for (int i = 0; i < chips.Length; i++)
        {
            string topic = topics[i];
            var tabBtn = ChipButton(TideKit.PersonaButton(chips[i], primary: (i == 0)), () => { Search.Text = topic; ShowView(View.Ideas); });
            tabBtn.Margin = new Thickness(0, 0, 14, 0);
            tabsRow.Children.Add(tabBtn);
        }
        Canvas.SetLeft(tabsRow, 220);
        Canvas.SetTop(tabsRow, 104);
        board.Children.Add(tabsRow);

        // 5. The real chat, ideas, check-up and settings, and 6. the real input
        board.Children.Add(new Border { Width = 1000, Height = 540, Background = new SolidColorBrush(Color.FromArgb(0xb3, 0x00, 0x00, 0x00)), BorderBrush = TideKit.WhiteBrush, BorderThickness = new Thickness(3), Margin = new Thickness(220, 180, 0, 0) });
        MountRealViews(board, 226, 186, 988, 528, new SolidColorBrush(Color.FromArgb(0xff, 0x08, 0x10, 0x1c)), TideKit.WhiteBrush);
        StyleRealInput(TideKit.Body, 17, TideKit.InkBrush);
        Input.FontWeight = FontWeights.Bold;
        var inputBorder = new Border
        {
            Width = 760, Height = 56, Background = TideKit.WhiteBrush, Padding = new Thickness(18, 8, 18, 8),
            RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = new RotateTransform(-3),
            Effect = new DropShadowEffect { BlurRadius = 0, ShadowDepth = 7, Direction = 315, Opacity = 1.0, Color = TideKit.Ink }
        };
        var inputGrid = new Grid();
        inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(Input, 0); inputGrid.Children.Add(Input);
        var sendBtn = ChipButton(TideKit.PersonaButton("\uE724", primary: true), ClickSend);
        Grid.SetColumn(sendBtn, 1); inputGrid.Children.Add(sendBtn);
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
