using System.Linq;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Spotlight;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private SetupsWorkbench? spotlightSetupsBench;

    private void SpotlightShowSetups()
    {
        spotlightPageHost!.Children.Clear();
        var bench = Setups;
        if (bench != null && !ReferenceEquals(bench, spotlightSetupsBench))
        {
            spotlightSetupsBench = bench;
            bench.Watch(() => Dispatcher.BeginInvoke(new Action(() => { if (CurrentPage == "Setups" && ReferenceEquals(spotlightSetupsBench, Setups)) SpotlightShowSetups(); })));
        }
        var cards = bench?.Cards ?? new List<(string Id, string Name, string State)>();
        string selectedId = bench?.SelectedId ?? (cards.Count > 0 ? cards[0].Id : "");
        var selected = cards.FirstOrDefault(c => c.Id == selectedId);
        var others = cards.Where(c => c.Id != selectedId).Take(4).ToList();
        var canvas = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };

        // Top actions: + New setup, Import, Export at (1050, 150)
        var topActions = new StackPanel { Orientation = Orientation.Horizontal };
        topActions.Children.Add(SpotlightKit.TheatricalButton("New setup", false, null, onClick: () => bench?.Click("new"), height: 40, fontSize: 11));
        var impBtn = SpotlightKit.TheatricalButton("Import", false, null, onClick: () => bench?.Click("import"), height: 40, fontSize: 11);
        impBtn.Margin = new Thickness(10, 0, 0, 0);
        topActions.Children.Add(impBtn);
        var expBtn = SpotlightKit.TheatricalButton("Export", false, null, onClick: () => bench?.Click("export"), height: 40, fontSize: 11);
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
            Text = selected.Name == null ? "NO SETUPS YET" : selected.State,
            FontFamily = SpotlightKit.Display,
            FontSize = 10,
            FontWeight = FontWeights.Light,
            Foreground = SpotlightKit.GoldBrush
        });
        posterText.Children.Add(new TextBlock
        {
            Text = selected.Name ?? "—",
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
        for (int i = 0; i < 4; i++)
        {
            double px = 680 + i * 178;
            double pw = 140, ph = 180;

            if (i < others.Count)
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

                string pickId = others[i].Id;
                sidePoster.Cursor = System.Windows.Input.Cursors.Hand;
                sidePoster.MouseLeftButtonDown += (_, _) => { bench?.Select(pickId); SpotlightShowSetups(); };
                Canvas.SetLeft(sidePoster, px);
                Canvas.SetTop(sidePoster, 300);
                canvas.Children.Add(sidePoster);

                var spText = new StackPanel { Width = 160 };
                spText.Children.Add(new TextBlock
                {
                    Text = others[i].Name,
                    FontFamily = SpotlightKit.Body,
                    FontSize = 16,
                    Foreground = SpotlightKit.ChalkDimBrush,
                    TextAlignment = TextAlignment.Center
                });
                spText.Children.Add(new TextBlock
                {
                    Text = others[i].State,
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
        var savedTag = SpotlightKit.Tag(selected.State ?? "Empty", false);
        savedTag.Margin = new Thickness(16, 0, 16, 0);
        topRow.Children.Add(savedTag);
        topRow.Children.Add(new TextBlock { Text = "Saved on this PC", FontFamily = SpotlightKit.Body, FontStyle = FontStyles.Italic, FontSize = 14, Foreground = SpotlightKit.MuteBrush });
        cardInner.Children.Add(topRow);

        var midRow = new Grid { Margin = new Thickness(0, 10, 0, 0) };
        midRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(290) });
        midRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var setupTitle = new TextBlock { Text = selected.Name ?? "No setups yet", FontFamily = SpotlightKit.Body, FontSize = 28, Foreground = SpotlightKit.ChalkBrush };
        Grid.SetColumn(setupTitle, 0);
        midRow.Children.Add(setupTitle);

        var statsRow = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var (k0, v) in bench?.Metrics ?? new List<(string Label, string Value)>())
        {
            var stat = new StackPanel { Width = 160 };
            stat.Children.Add(new TextBlock { Text = k0.ToUpperInvariant(), FontFamily = SpotlightKit.Display, FontSize = 10, FontWeight = FontWeights.Light, Foreground = SpotlightKit.MuteBrush });
            stat.Children.Add(new TextBlock { Text = v, FontFamily = SpotlightKit.Bodoni, FontSize = 22, Foreground = SpotlightKit.B("#f3e7d3"), Margin = new Thickness(0, 4, 0, 0) });
            statsRow.Children.Add(stat);
        }
        Grid.SetColumn(statsRow, 1);
        midRow.Children.Add(statsRow);
        cardInner.Children.Add(midRow);

        var actionBtns = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 18, 0, 0) };
        actionBtns.Children.Add(SpotlightKit.TheatricalButton("Load in local editor", true, "A", onClick: () => bench?.Click("open"), height: 40, fontSize: 12));
        var renBtn = SpotlightKit.TheatricalButton("Rename", false, null, onClick: () => bench?.Click("rename"), height: 40, fontSize: 11);
        renBtn.Margin = new Thickness(10, 0, 0, 0);
        actionBtns.Children.Add(renBtn);
        var dupBtn = SpotlightKit.TheatricalButton("Duplicate", false, null, onClick: () => bench?.Click("duplicate"), height: 40, fontSize: 11);
        dupBtn.Margin = new Thickness(10, 0, 0, 0);
        actionBtns.Children.Add(dupBtn);
        var delBtn = SpotlightKit.TheatricalButton("Delete", false, null, onClick: () => bench?.Click("delete"), height: 40, fontSize: 11);
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

    private bool spotlightTesterRear;

    private void SpotlightShowTester()
    {
        if (Tester is not { } tester) return;
        spotlightPageHost!.Children.Clear();
        var canvas = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };

        // Controller in follow spot at cx = 600, base = 600, w = 500
        double cx = 600, baseFloor = 600, w = 500;
        var spot = SpotlightKit.FollowSpot(cx, 640, 260, -60, 0.26);
        canvas.Children.Add(spot);

        var livePad = new System.Windows.Controls.Viewbox { Width = w, Height = 330, Stretch = Stretch.Uniform, Child = tester.TakeStage() };
        Canvas.SetLeft(livePad, cx - w / 2);
        Canvas.SetTop(livePad, baseFloor - 330);
        canvas.Children.Add(livePad);

        // View chips below controller at (cx - 160, 660)
        var viewChips = new StackPanel { Orientation = Orientation.Horizontal, Width = 320, HorizontalAlignment = HorizontalAlignment.Center };
        var frontChip = SpotlightKit.Chip("Front", !spotlightTesterRear);
        frontChip.Cursor = System.Windows.Input.Cursors.Hand;
        frontChip.MouseLeftButtonDown += (_, _) => { spotlightTesterRear = false; tester.ShowRear(false); Dispatcher.BeginInvoke(new Action(SpotlightShowTester)); };
        viewChips.Children.Add(frontChip);
        viewChips.Children.Add(new Border { Width = 8 });
        var rearChip = SpotlightKit.Chip("Back", spotlightTesterRear);
        rearChip.Cursor = System.Windows.Input.Cursors.Hand;
        rearChip.MouseLeftButtonDown += (_, _) => { if (!HasRearArtwork) return; spotlightTesterRear = true; tester.ShowRear(true); Dispatcher.BeginInvoke(new Action(SpotlightShowTester)); };
        viewChips.Children.Add(rearChip);
        Canvas.SetLeft(viewChips, cx - 160);
        Canvas.SetTop(viewChips, 660);
        canvas.Children.Add(viewChips);

        // Cue card at (1000, 200, w = 560, label = "Stage manager")
        var cardInner = new StackPanel();
        cardInner.Children.Add(new TextBlock { Text = "INPUT SOURCE", FontFamily = SpotlightKit.Display, FontSize = 11, FontWeight = FontWeights.Light, Foreground = SpotlightKit.GoldBrush });
        var srcTitle = new TextBlock { FontFamily = SpotlightKit.Body, FontSize = 27, Foreground = SpotlightKit.ChalkBrush, Margin = new Thickness(0, 4, 0, 0), TextWrapping = TextWrapping.Wrap };
        srcTitle.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Text") { Source = tester.SourceLabel });
        cardInner.Children.Add(srcTitle);
        foreach (var (lbl, blk) in new[] { ("PRESSED", tester.PressedBlock), ("INPUT OWNERSHIP", tester.OwnershipBlock) })
        {
            cardInner.Children.Add(new TextBlock { Text = lbl, FontFamily = SpotlightKit.Display, FontSize = 11, FontWeight = FontWeights.Light, Foreground = SpotlightKit.GoldBrush, Margin = new Thickness(0, 14, 0, 2) });
            var val = new TextBlock { FontFamily = SpotlightKit.Body, FontSize = 16, Foreground = SpotlightKit.ChalkDimBrush, TextWrapping = TextWrapping.Wrap };
            val.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Text") { Source = blk });
            cardInner.Children.Add(val);
        }

        var actionBtns = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 18, 0, 0) };
        actionBtns.Children.Add(SpotlightKit.TheatricalButton("Leave tester", true, "B", onClick: () => { tester.ClickExit(); SpotlightSelectPage("Device"); }, height: 40, fontSize: 12));
        var kbBtn = SpotlightKit.TheatricalButton(tester.KeyboardOn ? "Keyboard test: on" : "Keyboard test", false, null, onClick: () => { tester.ClickKeyboard(); Dispatcher.BeginInvoke(new Action(SpotlightShowTester)); }, height: 40, fontSize: 11);
        kbBtn.Margin = new Thickness(10, 0, 0, 0);
        actionBtns.Children.Add(kbBtn);
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
            hints: new[] { ("B", "Back") },
            leadW: 640);

        spotlightPageHost.Children.Add(frame);
    }
}
