using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Spotlight;

namespace X20Ctl.Product.Scanner;

public sealed partial class WriteScanView
{
    public Action<string>? SpotlightNavigate { get; set; }

    private void BuildSpotlightWriteWarning(Panel cards, Button go, Button leave)
    {
        var contentCanvas = new Canvas { Width = 1600, Height = 900 };

        // Step cards list at (292, 222, w = 780)
        var stepsStack = new StackPanel { Width = 780 };
        string[] titles = { "Baseline calibration", "KeyLinker mobile profile", "Parameter validation" };
        string[] subs =
        {
            "Record initial controller registers and calibration offsets prior to modification.",
            "Capture mobile application Bluetooth handshake and configuration packets.",
            "Compare captured write sequences against verified register maps."
        };

        for (int i = 0; i < titles.Length; i++)
        {
            bool on = (i == 0);
            var r = new Border
            {
                Padding = new Thickness(20, 14, 20, 14),
                Background = on
                    ? new RadialGradientBrush
                    {
                        Center = new(0.3, 0.5), RadiusX = 0.7, RadiusY = 1.6,
                        GradientStops = { new(SpotlightKit.A(SpotlightKit.C("#ffdcaf"), 0.20), 0.0), new(Colors.Transparent, 1.0) }
                    }
                    : Brushes.Transparent,
                BorderBrush = on ? SpotlightKit.GoldBrush : Brushes.Transparent,
                BorderThickness = new Thickness(on ? 3 : 0, 0, 0, 0),
                Margin = new Thickness(0, 0, 0, 8)
            };

            var rowGrid = new Grid();
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var num = new TextBlock
            {
                Text = $"{SpotlightKit.Roman(i)}.",
                FontFamily = SpotlightKit.Bodoni,
                FontSize = 20,
                Foreground = on ? SpotlightKit.GoldHiBrush : SpotlightKit.GoldBrush
            };
            Grid.SetColumn(num, 0);
            rowGrid.Children.Add(num);

            var textStack = new StackPanel();
            textStack.Children.Add(new TextBlock
            {
                Text = titles[i],
                FontFamily = SpotlightKit.Body,
                FontSize = 19,
                Foreground = on ? SpotlightKit.B("#fff6e6") : SpotlightKit.ChalkBrush
            });
            textStack.Children.Add(new TextBlock
            {
                Text = subs[i],
                FontFamily = SpotlightKit.Body,
                FontSize = 14,
                Foreground = SpotlightKit.MuteBrush,
                Margin = new Thickness(0, 2, 0, 0)
            });
            Grid.SetColumn(textStack, 1);
            rowGrid.Children.Add(textStack);

            r.Child = rowGrid;
            stepsStack.Children.Add(r);
        }
        Canvas.SetLeft(stepsStack, 292);
        Canvas.SetTop(stepsStack, 222);
        contentCanvas.Children.Add(stepsStack);

        // Soft spotlight on running time card at (1330, 640)
        var spot = SpotlightKit.FollowSpot(1330, 640, 200, -60, 0.16, pool: false);
        contentCanvas.Children.Add(spot);

        // Running time cue card at (1140, 240, w = 380)
        var rtInner = new StackPanel();
        rtInner.Children.Add(new TextBlock
        {
            Text = "RUNNING TIME",
            FontFamily = SpotlightKit.Display,
            FontSize = 11,
            FontWeight = FontWeights.Light,
            Foreground = SpotlightKit.GoldBrush,
            TextAlignment = TextAlignment.Center
        });
        rtInner.Children.Add(new Rectangle
        {
            Width = 50, Height = 1,
            Fill = SpotlightKit.GoldBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 14, 0, 14)
        });
        rtInner.Children.Add(new TextBlock
        {
            Text = "20–40",
            FontFamily = SpotlightKit.Bodoni,
            FontSize = 76,
            Foreground = SpotlightKit.GildBrush,
            TextAlignment = TextAlignment.Center
        });
        rtInner.Children.Add(new TextBlock
        {
            Text = "MIN",
            FontFamily = SpotlightKit.Display,
            FontSize = 15,
            FontWeight = FontWeights.Light,
            Foreground = SpotlightKit.ChalkDimBrush,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 8, 0, 16)
        });
        rtInner.Children.Add(new Rectangle
        {
            Width = 50, Height = 1,
            Fill = SpotlightKit.GoldBrush,
            HorizontalAlignment = HorizontalAlignment.Center
        });

        var rtCard = SpotlightKit.CueCard(rtInner, 380, padding: new Thickness(40, 30, 40, 36));
        Canvas.SetLeft(rtCard, 1140);
        Canvas.SetTop(rtCard, 240);
        contentCanvas.Children.Add(rtCard);

        // Cue card at bottom at (292, 712, w = 1260)
        var botInner = new Grid();
        botInner.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        botInner.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var noticeText = new TextBlock
        {
            Text = "Write scan is non-destructive. Controller settings can be restored from the baseline snapshot at any time.",
            FontFamily = SpotlightKit.Body,
            FontStyle = FontStyles.Italic,
            FontSize = 15.5,
            Foreground = SpotlightKit.ChalkDimBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(noticeText, 0);
        botInner.Children.Add(noticeText);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var startBtn = SpotlightKit.TheatricalButton("Start write scan", true, "A", onClick: () => go.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)), height: 44, fontSize: 12);
        actions.Children.Add(startBtn);

        var backBtn = SpotlightKit.TheatricalButton("Cancel", false, "B", onClick: () => leave.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)), height: 44, fontSize: 12);
        backBtn.Margin = new Thickness(12, 0, 0, 0);
        actions.Children.Add(backBtn);
        Grid.SetColumn(actions, 1);
        botInner.Children.Add(actions);

        var botCard = SpotlightKit.CueCard(botInner, 1260, padding: new Thickness(28, 18, 28, 18));
        Canvas.SetLeft(botCard, 292);
        Canvas.SetTop(botCard, 712);
        contentCanvas.Children.Add(botCard);

        var board = SpotlightToolFrame.WrapWithProgramme(
            currentKey: "tools",
            title: "Write scan",
            lead: "Profile memory changes and capture configuration payloads during active calibration.",
            backLabel: "Back to Tools",
            onBack: () => leave.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)),
            onNavigate: target => SpotlightNavigate?.Invoke(target),
            bodyStage: contentCanvas,
            hints: new[] { ("A", "Start"), ("B", "Back") },
            leadW: 1180,
            titleSize: 34);

        warning.Background = SpotlightKit.HouseBrush;
        warning.Child = board;
    }

    private void BuildSpotlightWriteFlow(Grid originalPage)
    {
        originalPage.Background = SpotlightKit.HouseBrush;
    }
}
