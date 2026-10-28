using System;
using System.Windows;
using System.Windows.Controls;
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

        // 4 Category tiles in 2x2 grid at (302 + col * 300, 240 + row * 60)
        string[] categories = { "Bug report", "Feature suggestion", "Controller compatibility", "General feedback" };
        for (int i = 0; i < categories.Length; i++)
        {
            double cx = 302 + (i % 2) * 300;
            double cy = 240 + (i / 2) * 60;
            bool on = (i == 0);

            var catTile = new Border
            {
                Width = 286, Height = 52,
                Background = on
                    ? new RadialGradientBrush
                    {
                        Center = new(0.3, 0.5), RadiusX = 0.7, RadiusY = 1.6,
                        GradientStops = { new(SpotlightKit.A(SpotlightKit.C("#ffdcaf"), 0.22), 0.0), new(SpotlightKit.A(SpotlightKit.C("#ffdcaf"), 0.05), 0.7), new(Colors.Transparent, 1.0) }
                    }
                    : Brushes.Transparent,
                BorderBrush = on ? SpotlightKit.GoldBrush : SpotlightKit.B(SpotlightKit.A(SpotlightKit.Gold, 0.12)),
                BorderThickness = new Thickness(on ? 3 : 0, 0, 0, on ? 0 : 1),
                Padding = new Thickness(16, 0, 16, 0),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            var stack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            if (on)
            {
                var lamp = new Path
                {
                    Data = Geometry.Parse("M3 4L13 4L11 11L5 11Z M5 11L1 18H15L11 11"),
                    Fill = SpotlightKit.GoldBrush,
                    Margin = new Thickness(0, 0, 10, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                stack.Children.Add(lamp);
            }
            stack.Children.Add(new TextBlock
            {
                Text = categories[i],
                FontFamily = SpotlightKit.Body,
                FontSize = 18,
                Foreground = on ? SpotlightKit.B("#fff6e6") : SpotlightKit.B("#e2d6c3"),
                VerticalAlignment = VerticalAlignment.Center
            });
            catTile.Child = stack;
            Canvas.SetLeft(catTile, cx);
            Canvas.SetTop(catTile, cy);
            board.Children.Add(catTile);
        }

        // Dropdown fields at (302, 392 + j * 92)
        var dropStack = new StackPanel { Width = 586 };
        foreach (var (lbl, val) in new[] { ("CONTROLLER MODEL", "X20 Pro"), ("CONNECTION TYPE", "USB Cable (Standard)") })
        {
            var fld = new StackPanel { Margin = new Thickness(0, 0, 0, 24) };
            fld.Children.Add(new TextBlock
            {
                Text = lbl,
                FontFamily = SpotlightKit.Display,
                FontSize = 11,
                FontWeight = FontWeights.Light,
                Foreground = SpotlightKit.GoldBrush
            });

            var ddBorder = new Border
            {
                BorderBrush = SpotlightKit.B(SpotlightKit.A(SpotlightKit.Gold, 0.55)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Height = 36,
                Margin = new Thickness(0, 6, 0, 0)
            };
            var ddGrid = new Grid();
            ddGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            ddGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var valText = new TextBlock
            {
                Text = val,
                FontFamily = SpotlightKit.Body,
                FontStyle = FontStyles.Italic,
                FontSize = 16,
                Foreground = SpotlightKit.B("#f3e7d3"),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(valText, 0);
            ddGrid.Children.Add(valText);

            var chev = new TextBlock
            {
                Text = "\uE70D",
                FontFamily = SpotlightKit.Icons,
                FontSize = 9,
                Foreground = SpotlightKit.GoldBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(chev, 1);
            ddGrid.Children.Add(chev);

            ddBorder.Child = ddGrid;
            fld.Children.Add(ddBorder);
            dropStack.Children.Add(fld);
        }
        Canvas.SetLeft(dropStack, 302);
        Canvas.SetTop(dropStack, 392);
        board.Children.Add(dropStack);

        var privacyText = new TextBlock
        {
            Text = "Feedback is transmitted securely to the development team. No hardware identifiers or telemetry logs are included unless explicitly attached.",
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

        // Script paper at (980, 220, w = 540, h = 420, angle = 1.2)
        var paperInner = new StackPanel();
        paperInner.Children.Add(new TextBlock
        {
            Text = "MESSAGE",
            FontFamily = SpotlightKit.Display,
            FontSize = 12,
            FontWeight = FontWeights.Light,
            Foreground = SpotlightKit.B("#2a1a12")
        });
        paperInner.Children.Add(new TextBlock
        {
            Text = "Stick deadzone feels slightly loose on initial travel after firmware update.",
            FontFamily = SpotlightKit.Body,
            FontStyle = FontStyles.Italic,
            FontSize = 28,
            Foreground = SpotlightKit.B("#2a1a12"),
            Margin = new Thickness(0, 20, 0, 0),
            TextWrapping = TextWrapping.Wrap
        });
        paperInner.Children.Add(new TextBlock
        {
            Text = "73 / 1000 characters",
            FontFamily = SpotlightKit.Body,
            FontSize = 14,
            Foreground = SpotlightKit.B(SpotlightKit.A(SpotlightKit.C("#2a1a12"), 0.7)),
            TextAlignment = TextAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0)
        });

        var scriptPaper = SpotlightParts.ScriptPaper(paperInner, 540, 420, 1.2);
        Canvas.SetLeft(scriptPaper, 980);
        Canvas.SetTop(scriptPaper, 220);
        board.Children.Add(scriptPaper);

        // Action buttons at (990, 680)
        var actionsRow = new StackPanel { Orientation = Orientation.Horizontal };
        var sendBtn = SpotlightKit.TheatricalButton("Send feedback", true, "A", height: 44, fontSize: 13);
        actionsRow.Children.Add(sendBtn);

        var cancelBtn = SpotlightKit.TheatricalButton("Cancel", false, "B", onClick: () => back.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)), height: 44, fontSize: 13);
        cancelBtn.Margin = new Thickness(12, 0, 0, 0);
        actionsRow.Children.Add(cancelBtn);

        Canvas.SetLeft(actionsRow, 990);
        Canvas.SetTop(actionsRow, 680);
        board.Children.Add(actionsRow);

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
