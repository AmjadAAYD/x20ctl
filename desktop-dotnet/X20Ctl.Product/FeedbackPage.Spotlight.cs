using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Spotlight;

namespace X20Ctl.Product;

public sealed partial class FeedbackPage
{
    private void BuildSpotlightFeedback(Button back, Panel kinds)
    {
        CornerRadius = new(0);
        BorderThickness = new(0);
        Padding = new(0);
        Background = Brushes.Transparent;
        BorderBrush = Brushes.Transparent;
        MaxWidth = double.PositiveInfinity;
        Margin = new(0);
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Top;
        Width = 1600;
        Height = 900;

        var board = new Canvas { Name = "SpotlightFeedback", Width = 1600, Height = 900, ClipToBounds = true };

        // Category header at (302, 210)
        var catHead = new TextBlock
        {
            Text = "CATEGORY",
            FontFamily = SpotlightKit.Display,
            FontSize = 11,
            FontWeight = FontWeights.Light,
            Foreground = SpotlightKit.GoldBrush
        };
        Canvas.SetLeft(catHead, 302);
        Canvas.SetTop(catHead, 210);
        board.Children.Add(catHead);

        // Real controls from the shared page, laid out on the stage
        Detach(kinds); Detach(controller); Detach(connection); Detach(Message); Detach(counter); Detach(SendButton); Detach(status);

        if (kinds is UniformGrid kindGrid) { kindGrid.Rows = 2; kindGrid.Columns = 2; }
        foreach (var child in kinds.Children) if (child is RadioButton radio) radio.MinWidth = 280;
        Canvas.SetLeft(kinds, 302);
        Canvas.SetTop(kinds, 240);
        board.Children.Add(kinds);

        var dropStack = new StackPanel { Width = 586 };
        foreach (var (lbl, box) in new[] { ("CONTROLLER (OPTIONAL)", controller), ("CONNECTED BY (OPTIONAL)", connection) })
        {
            var fld = new StackPanel { Margin = new Thickness(0, 0, 0, 24) };
            fld.Children.Add(new TextBlock
            {
                Text = lbl,
                FontFamily = SpotlightKit.Display,
                FontSize = 11,
                FontWeight = FontWeights.Light,
                Foreground = SpotlightKit.GoldBrush,
                Margin = new Thickness(0, 0, 0, 6)
            });
            box.Width = double.NaN;
            box.HorizontalAlignment = HorizontalAlignment.Stretch;
            fld.Children.Add(box);
            dropStack.Children.Add(fld);
        }
        Canvas.SetLeft(dropStack, 302);
        Canvas.SetTop(dropStack, 392);
        board.Children.Add(dropStack);

        var privacyText = new TextBlock
        {
            Text = "What's sent: your message, what it's about, the controller and connection if you pick them, the X20CTL version and the Windows version. No account, files or personal details.",
            FontFamily = SpotlightKit.Body,
            FontStyle = FontStyles.Italic,
            FontSize = 14,
            Foreground = SpotlightKit.MuteBrush,
            Width = 590,
            TextWrapping = TextWrapping.Wrap
        };
        Canvas.SetLeft(privacyText, 302);
        Canvas.SetTop(privacyText, 590);
        board.Children.Add(privacyText);

        // Soft spotlight on paper at (1250, 760)
        var spot = SpotlightKit.FollowSpot(1250, 760, 260, -60, 0.16, pool: false);
        board.Children.Add(spot);

        // Script paper holds the real message box
        var ink = SpotlightKit.B("#2a1a12");
        Message.Background = Brushes.Transparent;
        Message.BorderThickness = new Thickness(0);
        Message.Foreground = ink;
        Message.CaretBrush = ink;
        Message.FontFamily = SpotlightKit.Body;
        Message.FontStyle = FontStyles.Italic;
        Message.FontSize = 24;
        Message.Height = 290;
        Message.MaxHeight = 290;
        Message.Margin = new Thickness(0, 16, 0, 0);
        counter.Foreground = SpotlightKit.B(SpotlightKit.A(SpotlightKit.C("#2a1a12"), 0.7));
        counter.FontFamily = SpotlightKit.Body;
        counter.FontSize = 14;
        counter.HorizontalAlignment = HorizontalAlignment.Right;

        var paperInner = new StackPanel();
        paperInner.Children.Add(new TextBlock
        {
            Text = "MESSAGE",
            FontFamily = SpotlightKit.Display,
            FontSize = 12,
            FontWeight = FontWeights.Light,
            Foreground = ink
        });
        paperInner.Children.Add(Message);
        paperInner.Children.Add(counter);

        var scriptPaper = SpotlightParts.ScriptPaper(paperInner, 540, 420, 1.2);
        Canvas.SetLeft(scriptPaper, 980);
        Canvas.SetTop(scriptPaper, 220);
        board.Children.Add(scriptPaper);

        // Action buttons at (990, 680)
        var actionsRow = new StackPanel { Orientation = Orientation.Horizontal };
        var sendBtn = SpotlightKit.TheatricalButton("Send feedback", true, "A", onClick: () => _ = Submit(), height: 44, fontSize: 13);
        sendBtn.SetBinding(UIElement.IsEnabledProperty, new System.Windows.Data.Binding("IsEnabled") { Source = SendButton });
        actionsRow.Children.Add(sendBtn);

        var cancelBtn = SpotlightKit.TheatricalButton("Cancel", false, "B", onClick: () => back.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)), height: 44, fontSize: 13);
        cancelBtn.Margin = new Thickness(12, 0, 0, 0);
        actionsRow.Children.Add(cancelBtn);

        Canvas.SetLeft(actionsRow, 990);
        Canvas.SetTop(actionsRow, 680);
        board.Children.Add(actionsRow);

        status.Width = 540;
        status.FontFamily = SpotlightKit.Body;
        Canvas.SetLeft(status, 990);
        Canvas.SetTop(status, 740);
        board.Children.Add(status);

        var frame = SpotlightToolFrame.WrapWithProgramme(
            currentKey: "tools",
            title: "Feedback",
            lead: "Share diagnostic findings, report defects or propose new controller capabilities.",
            backLabel: "Back",
            onBack: () => back.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)),
            onNavigate: null,
            bodyStage: board,
            hints: new[] { ("A", "Send"), ("B", "Back") },
            leadW: 640);

        Child = frame;
    }
}
