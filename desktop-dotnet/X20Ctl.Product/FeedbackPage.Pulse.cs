using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using X20Ctl.Product.Pulse;

namespace X20Ctl.Product;

public sealed partial class FeedbackPage
{
    private void BuildPulseFeedback(Button back, Panel kinds)
    {
        CornerRadius = new(0);
        BorderThickness = new(0);
        Padding = new(0);
        Background = Brushes.Transparent;
        MaxWidth = double.PositiveInfinity;
        Margin = new(0);

        var content = new Canvas { Width = 1600, Height = 900 };

        // 1. Left Stage: Category & Model Selection (x=272, y=182, w=600) per t_pulse.py:912
        var leftStage = new StackPanel { Width = 600 };
        leftStage.Children.Add(PulseParts.Kicker("FEEDBACK CATEGORY"));
        leftStage.Children.Add(PulseParts.Disp("What is this about?", 22, PulseParts.TextPrimary, FontWeights.SemiBold).Also(t => t.Margin = new(0, 4, 0, 16)));

        if (kinds.Parent is Panel pKinds) pKinds.Children.Remove(kinds);
        var radioGrid = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2, Margin = new(0, 0, 0, 24) };
        foreach (var radio in kinds.Children.OfType<RadioButton>().ToArray())
        {
            kinds.Children.Remove(radio);
            var btnFace = new Border
            {
                CornerRadius = new(16),
                Padding = new(16, 12, 16, 12),
                Margin = new(4),
                Background = PulseInk.B(PulseInk.A(Colors.White, 0.08)),
                BorderBrush = PulseInk.B(PulseInk.A(PulseParts.CyanBright, 0.35)),
                BorderThickness = new(1),
                Child = PulseParts.Body(radio.Content?.ToString() ?? "", 14, PulseParts.TextPrimary, FontWeights.SemiBold)
            };
            radio.Content = btnFace;
            radio.Style = null;
            radioGrid.Children.Add(radio);
        }
        leftStage.Children.Add(radioGrid);

        leftStage.Children.Add(PulseParts.Kicker("CONTROLLER MODEL (OPTIONAL)"));
        if (controller.Parent is Panel pCtrl) pCtrl.Children.Remove(controller);
        controller.Width = 590;
        controller.Height = 44;
        controller.Margin = new(0, 6, 0, 18);
        controller.Background = PulseInk.B(PulseInk.A(PulseInk.C("#0d1b34"), 0.85));
        controller.Foreground = Brushes.White;
        leftStage.Children.Add(controller);

        leftStage.Children.Add(PulseParts.Kicker("CONNECTION METHOD (OPTIONAL)"));
        if (connection.Parent is Panel pConn) pConn.Children.Remove(connection);
        connection.Width = 590;
        connection.Height = 44;
        connection.Margin = new(0, 6, 0, 18);
        connection.Background = PulseInk.B(PulseInk.A(PulseInk.C("#0d1b34"), 0.85));
        connection.Foreground = Brushes.White;
        leftStage.Children.Add(connection);

        var privacy = PulseParts.Body("What's sent: your message, category, controller and connection if chosen, X20CTL version and Windows version. No account, telemetry files, or personal data are collected.", 12.5, PulseParts.TextSoft, wrap: true);
        privacy.Margin = new(0, 10, 0, 0);
        leftStage.Children.Add(privacy);

        Canvas.SetLeft(leftStage, 272);
        Canvas.SetTop(leftStage, 182);
        content.Children.Add(leftStage);

        // 2. Right Stage: Rising Pearl Message Card (x=940, y=196, w=620, h=620) per t_pulse.py:924
        var msgStack = new StackPanel { Margin = new(10) };
        msgStack.Children.Add(PulseParts.Kicker("YOUR MESSAGE", onPearl: true));
        msgStack.Children.Add(PulseParts.Disp("Write to the Team", 24, PulseParts.PearlInk, FontWeights.Bold).Also(t => t.Margin = new(0, 4, 0, 14)));

        if (Message.Parent is Panel pMsg) pMsg.Children.Remove(Message);
        Message.MinHeight = 330;
        Message.MaxHeight = 330;
        Message.Height = 330;
        Message.Width = 560;
        Message.FontFamily = PulseParts.BodyFont;
        Message.FontSize = 18;
        Message.Foreground = PulseInk.B(PulseParts.PearlInk);
        Message.CaretBrush = PulseInk.B(PulseParts.CyanColor);
        Message.Background = Brushes.Transparent;
        Message.BorderThickness = new(0);
        Message.TextWrapping = TextWrapping.Wrap;
        Message.AcceptsReturn = true;

        var boxContainer = new Border
        {
            CornerRadius = new(18),
            Background = PulseInk.B(PulseInk.A(Colors.White, 0.65)),
            BorderBrush = PulseInk.B(PulseInk.A(PulseParts.PearlMid, 0.25)),
            BorderThickness = new(1),
            Padding = new(18),
            Child = Message
        };
        msgStack.Children.Add(boxContainer);

        if (counter.Parent is Panel pCount) pCount.Children.Remove(counter);
        counter.Foreground = PulseInk.B(PulseParts.PearlSoft);
        counter.FontFamily = PulseParts.MonoFont;
        counter.FontSize = 12;
        counter.HorizontalAlignment = HorizontalAlignment.Right;
        counter.Margin = new(0, 6, 0, 16);
        msgStack.Children.Add(counter);

        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        if (SendButton.Parent is Panel pSend) pSend.Children.Remove(SendButton);
        var sendPill = PulseParts.PillButton("Send Feedback", primary: true, onPearl: true, onClick: async () => await Submit());
        actions.Children.Add(sendPill);

        var cancelPill = PulseParts.PillButton("Cancel", primary: false, onPearl: true, onClick: () => back.RaiseEvent(new RoutedEventArgs(Button.ClickEvent))).Also(b => b.Margin = new(12, 0, 0, 0));
        actions.Children.Add(cancelPill);
        msgStack.Children.Add(actions);

        if (status.Parent is Panel pStat) pStat.Children.Remove(status);
        status.FontFamily = PulseParts.BodyFont;
        status.FontSize = 13.5;
        status.Foreground = PulseInk.B(PulseParts.CyanBright);
        status.Margin = new(0, 12, 0, 0);
        msgStack.Children.Add(status);

        var pearl = PulseParts.RisingPearlCard(msgStack, new Thickness(36, 30, 36, 30), topRadius: 30, bottomRadius: 26);
        pearl.Width = 620;
        pearl.Height = 620;
        Canvas.SetLeft(pearl, 940);
        Canvas.SetTop(pearl, 196);
        content.Children.Add(pearl);

        Child = PulseToolFrame.WrapWithOrbit(
            "Tools",
            "Feedback",
            "What you send is held up on the pearl card, in your own words.",
            "Back to Tools",
            () => back.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)),
            null,
            content,
            [("A", "Select"), ("B", "Back")]);
    }
}
