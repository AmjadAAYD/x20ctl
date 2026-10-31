using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Spotlight;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private void SpotlightShowSetups()
    {
        spotlightPageHost!.Children.Clear();
        var canvas = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };

        // Top actions: + New setup, Import, Export at (1050, 150)
        var topActions = new StackPanel { Orientation = Orientation.Horizontal };
        topActions.Children.Add(SpotlightKit.TheatricalButton("New setup", false, null, height: 40, fontSize: 11));
        var impBtn = SpotlightKit.TheatricalButton("Import", false, null, height: 40, fontSize: 11);
        impBtn.Margin = new Thickness(10, 0, 0, 0);
        topActions.Children.Add(impBtn);
        var expBtn = SpotlightKit.TheatricalButton("Export", false, null, height: 40, fontSize: 11);
        expBtn.Margin = new Thickness(10, 0, 0, 0);
        topActions.Children.Add(expBtn);
        Canvas.SetLeft(topActions, 1050);
        Canvas.SetTop(topActions, 150);
        canvas.Children.Add(topActions);

        // Follow spot on selected setup poster at (450, 600)
        var spot = SpotlightKit.FollowSpot(450, 600, 200, -60, 0.24, pool: false);
        canvas.Children.Add(spot);

        // Selected Setup Poster at (305, 220, w = 290, h = 360)
        var posterBorder = new Border
        {
            Width = 290, Height = 360,
            Background = new LinearGradientBrush
            {
                StartPoint = new(0, 0), EndPoint = new(0, 1),
                GradientStops = { new(SpotlightKit.C("#241410"), 0.0), new(SpotlightKit.C("#100806"), 1.0) }
            },
            BorderBrush = SpotlightKit.GoldBrush,
            BorderThickness = new Thickness(2),
            ClipToBounds = true
        };

        var posterCanvas = new Canvas { Width = 290, Height = 360 };
        var posterArt = SpotlightParts.SpotlightStar(Model.Id, 145, 260, 220, reflection: 0);
        posterCanvas.Children.Add(posterArt);

        var posterGrad = new Rectangle
        {
            Width = 290, Height = 360,
            Fill = new LinearGradientBrush
            {
                StartPoint = new(0, 0), EndPoint = new(0, 1),
                GradientStops = { new(Colors.Transparent, 0.45), new(SpotlightKit.A(SpotlightKit.C("#0e0706"), 0.92), 1.0) }
            }
        };
        posterCanvas.Children.Add(posterGrad);

        var posterText = new StackPanel { Width = 250 };
        posterText.Children.Add(new TextBlock
        {
            Text = "LOCAL SETUP",
            FontFamily = SpotlightKit.Display,
            FontSize = 10,
            FontWeight = FontWeights.Light,
            Foreground = SpotlightKit.GoldBrush
        });
        posterText.Children.Add(new TextBlock
        {
            Text = "Default Profile",
            FontFamily = SpotlightKit.Body,
            FontSize = 27,
            Foreground = SpotlightKit.ChalkBrush,
            Margin = new Thickness(0, 4, 0, 0)
        });
        Canvas.SetLeft(posterText, 20);
        Canvas.SetTop(posterText, 280);
        posterCanvas.Children.Add(posterText);

        posterBorder.Child = posterCanvas;
        Canvas.SetLeft(posterBorder, 305);
        Canvas.SetTop(posterBorder, 220);
        canvas.Children.Add(posterBorder);

        // Repertoire Carousel: 4 other posters at px = 680 + i * 178
        string[] otherNames = { "FPS Pro", "Racing", "Arcade", "Custom" };
        for (int i = 0; i < 4; i++)
        {
            double px = 680 + i * 178;
            double pw = 140, ph = 180;

            if (i < 2)
            {
                var sidePoster = new Border
                {
                    Width = pw, Height = ph,
                    Background = new LinearGradientBrush
                    {
                        StartPoint = new(0, 0), EndPoint = new(0, 1),
                        GradientStops = { new(SpotlightKit.C("#1c100c"), 0.0), new(SpotlightKit.C("#0d0705"), 1.0) }
                    },
                    BorderBrush = SpotlightKit.B(SpotlightKit.A(SpotlightKit.Gold, 0.35)),
                    BorderThickness = new Thickness(1),
                    Opacity = 0.75,
                    ClipToBounds = true
                };

                var spCanvas = new Canvas { Width = pw, Height = ph };
                var padMin = SpotlightParts.SpotlightStar(Model.Id, 70, 130, 110, reflection: 0);
                spCanvas.Children.Add(padMin);
                sidePoster.Child = spCanvas;

                Canvas.SetLeft(sidePoster, px);
                Canvas.SetTop(sidePoster, 300);
                canvas.Children.Add(sidePoster);

                var spText = new StackPanel { Width = 160 };
                spText.Children.Add(new TextBlock
                {
                    Text = otherNames[i],
                    FontFamily = SpotlightKit.Body,
                    FontSize = 16,
                    Foreground = SpotlightKit.ChalkDimBrush,
                    TextAlignment = TextAlignment.Center
                });
                spText.Children.Add(new TextBlock
                {
                    Text = "SAVED",
                    FontFamily = SpotlightKit.Display,
                    FontSize = 9.5,
                    FontWeight = FontWeights.Light,
                    Foreground = SpotlightKit.B("#7a6c5a"),
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 2, 0, 0)
                });
                Canvas.SetLeft(spText, px - 10);
                Canvas.SetTop(spText, 496);
                canvas.Children.Add(spText);
            }
            else
            {
                var dashed = new Border
                {
                    Width = pw, Height = ph,
                    BorderBrush = SpotlightKit.B(SpotlightKit.A(SpotlightKit.Gold, 0.25)),
                    BorderThickness = new Thickness(1),
                    Background = Brushes.Transparent
                };
                Canvas.SetLeft(dashed, px);
                Canvas.SetTop(dashed, 300);
                canvas.Children.Add(dashed);
            }
        }

        // Cue card at bottom at (292, 620, w = 1260)
        var cardInner = new StackPanel();

        var topRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        topRow.Children.Add(new TextBlock { Text = "SELECTED SETUP", FontFamily = SpotlightKit.Display, FontSize = 11, FontWeight = FontWeights.Light, Foreground = SpotlightKit.GoldBrush });
        var savedTag = SpotlightKit.Tag("Saved", false);
        savedTag.Margin = new Thickness(16, 0, 16, 0);
        topRow.Children.Add(savedTag);
        topRow.Children.Add(new TextBlock { Text = "Saved on this PC", FontFamily = SpotlightKit.Body, FontStyle = FontStyles.Italic, FontSize = 14, Foreground = SpotlightKit.MuteBrush });
        cardInner.Children.Add(topRow);

        var midRow = new Grid { Margin = new Thickness(0, 10, 0, 0) };
        midRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(290) });
        midRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var setupTitle = new TextBlock { Text = "Default Profile", FontFamily = SpotlightKit.Body, FontSize = 28, Foreground = SpotlightKit.ChalkBrush };
        Grid.SetColumn(setupTitle, 0);
        midRow.Children.Add(setupTitle);

        var statsRow = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var (k, v) in new[] { ("BUTTONS", "0 modified"), ("CURVES", "Linear"), ("MACROS", "None") })
        {
            var stat = new StackPanel { Width = 180 };
            stat.Children.Add(new TextBlock { Text = k, FontFamily = SpotlightKit.Display, FontSize = 10, FontWeight = FontWeights.Light, Foreground = SpotlightKit.MuteBrush });
            stat.Children.Add(new TextBlock { Text = v, FontFamily = SpotlightKit.Bodoni, FontSize = 22, Foreground = SpotlightKit.B("#f3e7d3"), Margin = new Thickness(0, 4, 0, 0) });
            statsRow.Children.Add(stat);
        }
        Grid.SetColumn(statsRow, 1);
        midRow.Children.Add(statsRow);
        cardInner.Children.Add(midRow);

        var actionBtns = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 18, 0, 0) };
        actionBtns.Children.Add(SpotlightKit.TheatricalButton("Load in local editor", true, "A", height: 40, fontSize: 12));
        var renBtn = SpotlightKit.TheatricalButton("Rename", false, null, height: 40, fontSize: 11);
        renBtn.Margin = new Thickness(10, 0, 0, 0);
        actionBtns.Children.Add(renBtn);
        var dupBtn = SpotlightKit.TheatricalButton("Duplicate", false, null, height: 40, fontSize: 11);
        dupBtn.Margin = new Thickness(10, 0, 0, 0);
        actionBtns.Children.Add(dupBtn);
        var delBtn = SpotlightKit.TheatricalButton("Delete", false, null, height: 40, fontSize: 11);
        delBtn.Margin = new Thickness(10, 0, 0, 0);
        actionBtns.Children.Add(delBtn);
        cardInner.Children.Add(actionBtns);

        var cueCard = SpotlightKit.CueCard(cardInner, 1260, padding: new Thickness(26, 20, 26, 20));
        Canvas.SetLeft(cueCard, 292);
        Canvas.SetTop(cueCard, 620);
        canvas.Children.Add(cueCard);

        var frame = SpotlightToolFrame.WrapWithProgramme(
            currentKey: "setups",
            title: "Saved setups",
            lead: "Manage local setups and game profiles.",
            backLabel: "",
            onBack: () => SpotlightSelectPage("Dashboard"),
            onNavigate: SpotlightSelectPage,
            bodyStage: canvas,
            hints: new[] { ("A", "Load setup"), ("X", "Manage"), ("B", "Back") });

        spotlightPageHost.Children.Add(frame);
    }

    private void SpotlightShowTester()
    {
        spotlightPageHost!.Children.Clear();
        var canvas = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };

        // Controller in follow spot at cx = 600, base = 600, w = 500
        double cx = 600, baseFloor = 600, w = 500;
        var spot = SpotlightKit.FollowSpot(cx, 640, 260, -60, 0.26);
        canvas.Children.Add(spot);

        var star = SpotlightParts.SpotlightStar(Model.Id, cx, baseFloor, w, reflection: 0.18);
        canvas.Children.Add(star);

        // Dashed targeting reticles over sticks and buttons
        double height = w * 0.64;
        double x0 = cx - w / 2, y0 = baseFloor - height;
        (double fx, double fy, double r)[] reticles =
        {
            (0.27, 0.42, 42), (0.62, 0.63, 42), (0.37, 0.66, 38),
            (0.755, 0.39, 48), (0.2, 0.06, 28), (0.8, 0.06, 28)
        };
        foreach (var (rfx, rfy, rr) in reticles)
        {
            var reticle = new Ellipse
            {
                Width = rr * 2, Height = rr * 2,
                Stroke = SpotlightKit.B(SpotlightKit.A(SpotlightKit.GoldHi, 0.40)),
                StrokeThickness = 1.2,
                StrokeDashArray = new DoubleCollection { 2, 6 }
            };
            Canvas.SetLeft(reticle, x0 + rfx * w - rr);
            Canvas.SetTop(reticle, y0 + rfy * height - rr);
            canvas.Children.Add(reticle);
        }

        // View chips below controller at (cx - 160, 660)
        var viewChips = new StackPanel { Orientation = Orientation.Horizontal, Width = 320, HorizontalAlignment = HorizontalAlignment.Center };
        viewChips.Children.Add(SpotlightKit.Chip("Standard", true));
        viewChips.Children.Add(new Border { Width = 8 });
        viewChips.Children.Add(SpotlightKit.Chip("Raw Input", false));
        viewChips.Children.Add(new Border { Width = 8 });
        viewChips.Children.Add(SpotlightKit.Tag("Player 1", false));
        Canvas.SetLeft(viewChips, cx - 160);
        Canvas.SetTop(viewChips, 660);
        canvas.Children.Add(viewChips);

        // Mini horizontal gauge faders at (302, 760)
        var gaugeRow = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var (name, val) in new[] { ("LT", "0%"), ("RT", "0%"), ("LX", "0"), ("LY", "0"), ("RX", "0"), ("RY", "0") })
        {
            var cell = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 36, 0), VerticalAlignment = VerticalAlignment.Center };
            cell.Children.Add(SpotlightKit.HorizontalFader(0, 60));
            var textStack = new StackPanel { Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            textStack.Children.Add(new TextBlock { Text = name, FontFamily = SpotlightKit.Body, FontSize = 17, Foreground = SpotlightKit.ChalkBrush });
            textStack.Children.Add(new TextBlock { Text = val, FontFamily = SpotlightKit.Body, FontStyle = FontStyles.Italic, FontSize = 13, Foreground = SpotlightKit.MuteBrush });
            cell.Children.Add(textStack);
            gaugeRow.Children.Add(cell);
        }
        Canvas.SetLeft(gaugeRow, 302);
        Canvas.SetTop(gaugeRow, 760);
        canvas.Children.Add(gaugeRow);

        // Cue card at (1000, 200, w = 560, label = "Stage manager")
        var cardInner = new StackPanel();
        cardInner.Children.Add(new TextBlock { Text = "INPUT SOURCE", FontFamily = SpotlightKit.Display, FontSize = 11, FontWeight = FontWeights.Light, Foreground = SpotlightKit.GoldBrush });
        cardInner.Children.Add(new TextBlock { Text = "XInput Player 1", FontFamily = SpotlightKit.Body, FontSize = 27, Foreground = SpotlightKit.ChalkBrush, Margin = new Thickness(0, 4, 0, 0) });
        cardInner.Children.Add(new TextBlock { Text = "Active hardware capture session owned by X20CTL.", FontFamily = SpotlightKit.Body, FontSize = 16, Foreground = SpotlightKit.ChalkDimBrush, Margin = new Thickness(0, 8, 0, 0) });
        cardInner.Children.Add(new TextBlock { Text = "Actuate any button, stick or trigger to verify physical response.", FontFamily = SpotlightKit.Body, FontStyle = FontStyles.Italic, FontSize = 14, Foreground = SpotlightKit.MuteBrush, Margin = new Thickness(0, 4, 0, 0) });

        var actionBtns = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 18, 0, 0) };
        actionBtns.Children.Add(SpotlightKit.TheatricalButton("Stop test", true, "B", height: 40, fontSize: 12));
        var resetBtn = SpotlightKit.TheatricalButton("Reset min/max", false, null, height: 40, fontSize: 11);
        resetBtn.Margin = new Thickness(10, 0, 0, 0);
        actionBtns.Children.Add(resetBtn);
        cardInner.Children.Add(actionBtns);

        var cueCard = SpotlightKit.CueCard(cardInner, 560, label: "Stage manager", padding: new Thickness(30, 24, 30, 24));
        Canvas.SetLeft(cueCard, 1000);
        Canvas.SetTop(cueCard, 200);
        canvas.Children.Add(cueCard);

        var frame = SpotlightToolFrame.WrapWithProgramme(
            currentKey: "device",
            title: "Tester",
            lead: "Verify button responsiveness, analog sticks and trigger travel.",
            backLabel: "",
            onBack: () => SpotlightSelectPage("Device"),
            onNavigate: SpotlightSelectPage,
            bodyStage: canvas,
            hints: new[] { ("A", "Keyboard test"), ("B", "Stop test") },
            leadW: 640);

        spotlightPageHost.Children.Add(frame);
    }
}
