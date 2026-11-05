using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Sunrise;

namespace X20Ctl.Product.Agent;

public partial class AgentPanel
{
    private void BuildSunriseAgent()
    {
        var board = new Canvas { Name = "SunriseAgent", Width = 1600, Height = 900, ClipToBounds = true };
        board.Children.Add(SunriseKit.CreateHalftoneBackdrop());

        var frame = new SunriseToolFrame(
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
            FontFamily = SunriseKit.Display,
            FontWeight = FontWeights.Black,
            FontSize = 15,
            Foreground = SunriseKit.DarkBrush,
            Margin = new Thickness(0, 0, 0, 6)
        });
        msgSp.Children.Add(new TextBlock
        {
            Text = "I can inspect your current controller mapping and suggest calibration profiles for your games. What feels off with your inputs?",
            FontFamily = SunriseKit.Body,
            FontSize = 13.5,
            Foreground = SunriseKit.DarkBrush,
            LineHeight = 20,
            Margin = new Thickness(0, 8, 0, 0)
        });

        var card = SunriseKit.CreateStickerCard(msgSp, 920, 120);
        card.Margin = new Thickness(0, 0, 0, 16);
        chatSp.Children.Add(card);

        // Prompt Input Box inside a TV screen card
        var inputBorder = new Border
        {
            Background = SunriseKit.CardBrush,
            BorderBrush = SunriseKit.DarkBrush,
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(14, 10, 14, 10)
        };
        var inGrid = new Grid();
        inGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        inGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var promptBox = new TextBox
        {
            Background = Brushes.Transparent,
            Foreground = SunriseKit.DarkBrush,
            BorderThickness = new Thickness(0),
            FontFamily = SunriseKit.Body,
            FontSize = 13.5,
            Text = "Recommend stick sensitivity for fast arcade racing"
        };
        Grid.SetColumn(promptBox, 0);

        var sendBtn = SunriseKit.SunriseButton("Ask Agent", () => { }, isPrimary: true, width: 140, height: 34);
        Grid.SetColumn(sendBtn, 1);

        inGrid.Children.Add(promptBox);
        inGrid.Children.Add(sendBtn);
        inputBorder.Child = inGrid;
        chatSp.Children.Add(inputBorder);

        var chatCard = SunriseKit.CreateStickerCard(chatSp, 1020, 360);
        Canvas.SetLeft(chatCard, 290);
        Canvas.SetTop(chatCard, 80);
        canvas.Children.Add(chatCard);

        board.Children.Add(frame);
        Body.Child = new Viewbox { Child = board, Stretch = Stretch.Uniform };
        Body.Background = SunriseKit.PrimaryBrush;
    }
}
