using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Nocturne;

namespace X20Ctl.Product.Agent;

public partial class AgentPanel
{
    private void BuildNocturneAgent()
    {
        var board = new Canvas { Name = "NocturneAgent", Width = 1600, Height = 900, ClipToBounds = true };
        board.Children.Add(NocturneKit.CreateCityBackdrop());

        var frame = new NocturneToolFrame(
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
            FontFamily = NocturneKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 14,
            Foreground = NocturneKit.VoidTextBrush,
            Margin = new Thickness(0, 0, 0, 6)
        });
        msgSp.Children.Add(new TextBlock
        {
            Text = "I can inspect your current controller mapping and suggest calibration profiles for your games. What feels off with your inputs?",
            FontFamily = NocturneKit.Body,
            FontSize = 13,
            Foreground = NocturneKit.SoftBrush,
            LineHeight = 20,
            Margin = new Thickness(0, 8, 0, 0)
        });

        var card = NocturneKit.NocturneWhiteCard(msgSp, 920, 120);
        card.Margin = new Thickness(0, 0, 0, 16);
        chatSp.Children.Add(card);

        // Prompt Input Box
        var inputBorder = new Border
        {
            Background = NocturneKit.NightBrush,
            BorderBrush = NocturneKit.AccentBrush,
            BorderThickness = new Thickness(1.5),
            Padding = new Thickness(14, 10, 14, 10),
            RenderTransform = NocturneKit.CreateSlant(-12),
            RenderTransformOrigin = new Point(0.5, 0.5)
        };
        var inGrid = new Grid();
        inGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        inGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var promptBox = new TextBox
        {
            Background = Brushes.Transparent,
            Foreground = NocturneKit.WhiteBrush,
            BorderThickness = new Thickness(0),
            FontFamily = NocturneKit.Body,
            FontSize = 13.5,
            Text = "Recommend stick sensitivity for fast arcade racing"
        };
        Grid.SetColumn(promptBox, 0);

        var sendBtn = NocturneKit.NocturneButton("Ask Agent", () => { }, isPrimary: true, width: 140, height: 34);
        Grid.SetColumn(sendBtn, 1);

        inGrid.Children.Add(promptBox);
        inGrid.Children.Add(sendBtn);
        inputBorder.Child = inGrid;
        chatSp.Children.Add(inputBorder);

        var chatCard = NocturneKit.NocturneWhiteCard(chatSp, 1020, 360);
        Canvas.SetLeft(chatCard, 290);
        Canvas.SetTop(chatCard, 80);
        canvas.Children.Add(chatCard);

        board.Children.Add(frame);
        Body.Child = new Viewbox { Child = board, Stretch = Stretch.Uniform };
        Body.Background = NocturneKit.VoidBrush;
    }
}
