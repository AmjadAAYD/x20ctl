using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Hollow;

namespace X20Ctl.Product.Agent;

public partial class AgentPanel
{
    private void BuildHollowAgent()
    {
        var board = new Canvas { Name = "HollowAgent", Width = 1600, Height = 900, ClipToBounds = true };
        board.Children.Add(HollowKit.CreateCavernBackdrop());

        var frame = new HollowToolFrame(
            "AGENT XCTL",
            "Hardware advice, automatic curve calibration, and game setup assistance",
            onBack: () => CloseRequested?.Invoke());

        var canvas = frame.ContentArea;

        var chatSp = new StackPanel { Width = 960 };

        // Assistant Greeting Card
        var msgSp = new StackPanel();
        msgSp.Children.Add(new TextBlock
        {
            Text = "AGENT XCTL",
            FontFamily = HollowKit.Display,
            FontSize = 13.5,
            Foreground = HollowKit.IvoryTextBrush,
            Margin = new Thickness(0, 0, 0, 6)
        });
        msgSp.Children.Add(HollowKit.DiamondDivider(900, 10));
        msgSp.Children.Add(new TextBlock
        {
            Text = "I can inspect your current controller mapping and suggest calibration profiles for your games. What feels off with your inputs?",
            FontFamily = HollowKit.Body,
            FontSize = 13,
            Foreground = HollowKit.SoftTextBrush,
            LineHeight = 20,
            Margin = new Thickness(0, 8, 0, 0)
        });

        var card = HollowKit.FiligreeCard(msgSp, padding: 18, cornerSize: 14);
        card.Margin = new Thickness(0, 0, 0, 16);
        chatSp.Children.Add(card);

        // Prompt Input Box
        var inputBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xdd, 0x05, 0x0c, 0x14)),
            BorderBrush = HollowKit.IvoryBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(2),
            Padding = new Thickness(14, 10, 14, 10)
        };
        var inGrid = new Grid();
        inGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        inGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var promptBox = new TextBox
        {
            Background = Brushes.Transparent,
            Foreground = HollowKit.IvoryTextBrush,
            BorderThickness = new Thickness(0),
            FontFamily = HollowKit.Body,
            FontSize = 13.5,
            Text = "Recommend stick sensitivity for first-person shooters"
        };
        Grid.SetColumn(promptBox, 0);

        var sendBtn = HollowKit.HollowButton("Ask Agent", () => { }, isPrimary: true, width: 120, height: 32);
        Grid.SetColumn(sendBtn, 1);

        inGrid.Children.Add(promptBox);
        inGrid.Children.Add(sendBtn);
        inputBorder.Child = inGrid;
        chatSp.Children.Add(inputBorder);

        var chatCard = HollowKit.FiligreeCard(chatSp, padding: 24, cornerSize: 16);
        chatCard.Width = 1020;
        Canvas.SetLeft(chatCard, 290);
        Canvas.SetTop(chatCard, 80);
        canvas.Children.Add(chatCard);

        board.Children.Add(frame);
        Body.Child = new Viewbox { Child = board, Stretch = Stretch.Uniform };
        Body.Background = HollowKit.VoidBrush;
    }
}
