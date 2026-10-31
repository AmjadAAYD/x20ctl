using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Nexus;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private SetupsWorkbench? nexusSetupsBench;

    private void NexusShowSetups()
    {
        nexusPageHost!.Children.Clear();
        var bench = Setups;
        if (bench != null && !ReferenceEquals(bench, nexusSetupsBench))
        {
            nexusSetupsBench = bench;
            bench.Watch(() => Dispatcher.BeginInvoke(new Action(() => { if (CurrentPage == "Setups" && ReferenceEquals(nexusSetupsBench, Setups)) NexusShowSetups(); })));
        }
        var cards = bench?.Cards ?? new List<(string Id, string Name, string State)>();
        string selectedId = bench?.SelectedId ?? (cards.Count > 0 ? cards[0].Id : "");
        var selected = cards.FirstOrDefault(c => c.Id == selectedId);

        // 1. Radial background
        var bg = new Rectangle
        {
            Width = 1600, Height = 900,
            Fill = new RadialGradientBrush
            {
                Center = new Point(0.7, 0.3),
                RadiusX = 0.7,
                RadiusY = 0.8,
                GradientStops =
                {
                    new(NexusKit.C("#4b5562"), 0.0),
                    new(NexusKit.C("#1a1d22"), 0.6),
                    new(NexusKit.House, 1.0)
                }
            }
        };
        nexusPageHost.Children.Add(bg);

        // 2. Featured Controller angled in background: (820, 90, w=720), rotated -6 deg
        string bgModel = string.IsNullOrEmpty(Model.Id) ? "x20" : Model.Id;
        var padArt = NexusKit.ControllerArt(bgModel, 720);
        if (padArt != null)
        {
            padArt.RenderTransformOrigin = new Point(0.5, 0.5);
            padArt.RenderTransform = new RotateTransform(-6);
            padArt.Effect = new DropShadowEffect { BlurRadius = 40, ShadowDepth = 40, Direction = 270, Opacity = 0.6, Color = Colors.Black };
            Canvas.SetLeft(padArt, 820);
            Canvas.SetTop(padArt, 90);
            nexusPageHost.Children.Add(padArt);
        }

        // Dark gradients over controller
        var dimGrad1 = new Rectangle
        {
            Width = 1600, Height = 900,
            Fill = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0.5), EndPoint = new Point(1, 0.5),
                GradientStops =
                {
                    new(NexusKit.House, 0.2),
                    new(NexusKit.A(NexusKit.House, 0.3), 0.6),
                    new(Colors.Transparent, 1.0)
                }
            }
        };
        nexusPageHost.Children.Add(dimGrad1);

        var dimGrad2 = new Rectangle
        {
            Width = 1600, Height = 900,
            Fill = new LinearGradientBrush
            {
                StartPoint = new Point(0.5, 1), EndPoint = new Point(0.5, 0),
                GradientStops =
                {
                    new(NexusKit.House, 0.12),
                    new(Colors.Transparent, 0.40)
                }
            }
        };
        nexusPageHost.Children.Add(dimGrad2);

        // 3. Top bar
        var topBar = NexusKit.TopBar(
            player: Player,
            model: Model.Name,
            status: "Offline",
            onZone: () => BackRequested?.Invoke(),
            onSetups: () => ShowManagement("Setups"),
            onPcMode: () => ShowManagement("PcMode"),
            onTools: () => ToolsRequested?.Invoke()
        );
        Canvas.SetLeft(topBar, 62);
        Canvas.SetTop(topBar, 46);
        nexusPageHost.Children.Add(topBar);

        // 4. Breadcrumb at (62, 136)
        var crumb = new TextBlock
        {
            Text = "← Back  ·  My setups",
            FontFamily = NexusKit.Body,
            FontSize = 18,
            Foreground = NexusKit.WhiteBrush,
            Opacity = 0.9,
            Cursor = System.Windows.Input.Cursors.Hand
        };
        crumb.MouseLeftButtonDown += (_, _) => BackRequested?.Invoke();
        Canvas.SetLeft(crumb, 62);
        Canvas.SetTop(crumb, 136);
        nexusPageHost.Children.Add(crumb);

        // 5. Selected Setup Title Block at (68, 200)
        var titleStack = new StackPanel { Width = 680 };
        titleStack.Children.Add(new TextBlock
        {
            Text = "SELECTED SETUP",
            FontFamily = NexusKit.Body,
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = NexusKit.GreenBrush,
            Margin = new Thickness(0, 0, 0, 4)
        });
        titleStack.Children.Add(new TextBlock
        {
            Text = selected.Name ?? "No setups yet",
            FontFamily = NexusKit.Display,
            FontSize = 54,
            FontWeight = FontWeights.SemiBold,
            Foreground = NexusKit.WhiteBrush
        });
        titleStack.Children.Add(new TextBlock
        {
            Text = selected.Name == null ? "Create one from what you have now" : $"{Model.Name} · {selected.State}",
            FontFamily = NexusKit.Body,
            FontSize = 17,
            Foreground = NexusKit.SubBrush,
            Margin = new Thickness(0, 8, 0, 0)
        });
        Canvas.SetLeft(titleStack, 68);
        Canvas.SetTop(titleStack, 200);
        nexusPageHost.Children.Add(titleStack);

        // 6. Action buttons row at (68, 376)
        var actRow = new StackPanel { Orientation = Orientation.Horizontal };
        var actions = new (string Text, string Which, bool Focused)[]
        {
            ("Open", "open", true),
            ("Duplicate", "duplicate", false),
            ("Rename", "rename", false),
            ("Export", "export", false),
            ("Delete", "delete", false)
        };
        foreach (var (label, which, foc) in actions)
        {
            string act = which;
            var btn = NexusKit.TheatricalButton(label, primary: foc, height: 50, fontSize: 16, onClick: () => bench?.Click(act));
            btn.IsEnabled = bench?.Can(act) == true;
            btn.Margin = new Thickness(0, 0, 12, 0);
            actRow.Children.Add(btn);
        }
        Canvas.SetLeft(actRow, 68);
        Canvas.SetTop(actRow, 376);
        nexusPageHost.Children.Add(actRow);

        // 7. Stats panels at (68, 478)
        var statRow = new StackPanel { Orientation = Orientation.Horizontal };
        var stats = bench?.Metrics ?? new List<(string Label, string Value)>();
        foreach (var (lbl, val) in stats)
        {
            var panel = new Border
            {
                Width = 200,
                Padding = new Thickness(20, 16, 20, 16),
                Margin = new Thickness(0, 0, 12, 0),
                CornerRadius = new CornerRadius(8),
                Background = NexusKit.PanelBrush
            };
            var pStack = new StackPanel();
            pStack.Children.Add(new TextBlock { Text = lbl, FontFamily = NexusKit.Body, FontSize = 14, Foreground = NexusKit.SubBrush });
            pStack.Children.Add(new TextBlock { Text = val, FontFamily = NexusKit.Body, FontSize = 21, FontWeight = FontWeights.SemiBold, Foreground = NexusKit.WhiteBrush, Margin = new Thickness(0, 4, 0, 0) });
            panel.Child = pStack;
            statRow.Children.Add(panel);
        }
        Canvas.SetLeft(statRow, 68);
        Canvas.SetTop(statRow, 478);
        nexusPageHost.Children.Add(statRow);

        // 8. Saved setups header at (70, 596)
        var setupsLbl = new TextBlock
        {
            Text = "My setups",
            FontFamily = NexusKit.Body,
            FontSize = 17,
            Foreground = NexusKit.SubBrush
        };
        Canvas.SetLeft(setupsLbl, 70);
        Canvas.SetTop(setupsLbl, 596);
        nexusPageHost.Children.Add(setupsLbl);

        // 9. Setup tiles at y = 630
        var tileRow = new Canvas { Width = 1464, Height = 190 };
        double tx = 0;
        string[] coverPages = { "setups", "curves", "vibration" };
        for (int i = 0; i < cards.Count; i++)
        {
            var card = cards[i];
            string pageKey = coverPages[i % coverPages.Length];
            bool foc = card.Id == selectedId;

            var tCanvas = new Canvas { Width = 300, Height = 190, ClipToBounds = true };
            var cover = NexusKit.CoverTile(string.IsNullOrEmpty(Model.Id) ? "x20" : Model.Id, 300, 190, pageKey, dark: 0.8, radius: 6);
            tCanvas.Children.Add(cover);

            var localBadge = new Border
            {
                Background = NexusKit.WhiteBrush,
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(8, 3, 8, 3),
                Child = new TextBlock { Text = card.State, FontFamily = NexusKit.Body, FontSize = 12, FontWeight = FontWeights.Bold, Foreground = NexusKit.B("#111111") }
            };
            Canvas.SetLeft(localBadge, 12);
            Canvas.SetTop(localBadge, 12);
            tCanvas.Children.Add(localBadge);

            string pickId = card.Id;
            var tile = NexusKit.Tile(300, 190, tCanvas, card.Name, focused: foc, onClick: () => { bench?.Select(pickId); NexusShowSetups(); });
            Canvas.SetLeft(tile, tx);
            Canvas.SetTop(tile, 0);
            tileRow.Children.Add(tile);
            tx += 316;
            if (tx > 1000) break;
        }

        // New setup tile
        var addGrid = new Grid();
        addGrid.Children.Add(new TextBlock
        {
            Text = "\uE710",
            FontFamily = NexusKit.Icons,
            FontSize = 46,
            Foreground = NexusKit.WhiteBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        });
        var addTile = NexusKit.Tile(190, 190, addGrid, "New setup", focused: false, onClick: () => bench?.Click("new"));
        Canvas.SetLeft(addTile, tx);
        Canvas.SetTop(addTile, 0);
        tileRow.Children.Add(addTile);
        tx += 206;

        // Import tile
        var impGrid = new Grid();
        impGrid.Children.Add(new TextBlock
        {
            Text = "\uE8B5",
            FontFamily = NexusKit.Icons,
            FontSize = 46,
            Foreground = NexusKit.WhiteBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        });
        var impTile = NexusKit.Tile(190, 190, impGrid, "Import", focused: false, onClick: () => bench?.Click("import"));
        Canvas.SetLeft(impTile, tx);
        Canvas.SetTop(impTile, 0);
        tileRow.Children.Add(impTile);

        Canvas.SetLeft(tileRow, 68);
        Canvas.SetTop(tileRow, 630);
        nexusPageHost.Children.Add(tileRow);

        // 10. Bottom Hints
        var hints = NexusKit.HintsBar(new[] { ("A", "Open"), ("B", "Back"), ("Y", "New setup") });
        Canvas.SetRight(hints, 62);
        Canvas.SetTop(hints, 856);
        nexusPageHost.Children.Add(hints);
    }

    private bool nexusTesterRear;

    private void NexusShowTester()
    {
        if (Tester is not { } tester) return;
        nexusPageHost!.Children.Clear();

        // 1. Background gradient
        var bg = new Rectangle
        {
            Width = 1600, Height = 900,
            Fill = new LinearGradientBrush
            {
                StartPoint = new(0.5, 0), EndPoint = new(0.5, 1),
                GradientStops = { new(NexusKit.C("#1a1c20"), 0.0), new(NexusKit.House, 1.0) }
            }
        };
        nexusPageHost.Children.Add(bg);

        // 2. Top bar
        var topBar = NexusKit.TopBar(
            player: Player,
            model: Model.Name,
            status: "Offline",
            onZone: () => BackRequested?.Invoke(),
            onSetups: () => ShowManagement("Setups"),
            onPcMode: () => ShowManagement("PcMode"),
            onTools: () => ToolsRequested?.Invoke()
        );
        Canvas.SetLeft(topBar, 62);
        Canvas.SetTop(topBar, 46);
        nexusPageHost.Children.Add(topBar);

        // 3. Title & Lead
        var title = new TextBlock
        {
            Text = "Tester",
            FontFamily = NexusKit.Display,
            FontSize = 38,
            FontWeight = FontWeights.SemiBold,
            Foreground = NexusKit.WhiteBrush
        };
        Canvas.SetLeft(title, 68);
        Canvas.SetTop(title, 138);
        nexusPageHost.Children.Add(title);

        var lead = new TextBlock
        {
            Text = "Live view of what this PC reads from the controller",
            FontFamily = NexusKit.Body,
            FontSize = 17,
            Foreground = NexusKit.SubBrush
        };
        Canvas.SetLeft(lead, 70);
        Canvas.SetTop(lead, 192);
        nexusPageHost.Children.Add(lead);

        // 4. Center Controller Art at (68, 250, w=820)
        var livePad = new Viewbox { Width = 560, Height = 360, Stretch = Stretch.Uniform, Child = tester.TakeStage() };
        Canvas.SetLeft(livePad, 198);
        Canvas.SetTop(livePad, 260);
        nexusPageHost.Children.Add(livePad);

        // Subtabs under controller: Front, Back
        var tabsStack = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var (t, on) in new[] { ("Front", !nexusTesterRear), ("Back", nexusTesterRear) })
        {
            var tabBtn = new Border
            {
                Padding = new Thickness(18, 8, 18, 8),
                Margin = new Thickness(0, 0, 10, 0),
                CornerRadius = new CornerRadius(4),
                Background = on ? NexusKit.WhiteBrush : NexusKit.TileBrush,
                Child = new TextBlock
                {
                    Text = t,
                    FontFamily = NexusKit.Body,
                    FontSize = 16,
                    FontWeight = on ? FontWeights.SemiBold : FontWeights.Normal,
                    Foreground = on ? NexusKit.B("#111111") : NexusKit.WhiteBrush
                }
            };
            bool rear = t == "Back";
            tabBtn.Cursor = System.Windows.Input.Cursors.Hand;
            tabBtn.MouseLeftButtonDown += (_, _) => { if (!rear || HasRearArtwork) { nexusTesterRear = rear; tester.ShowRear(rear); Dispatcher.BeginInvoke(new Action(NexusShowTester)); } };
            tabsStack.Children.Add(tabBtn);
        }
        Canvas.SetLeft(tabsStack, 395);
        Canvas.SetTop(tabsStack, 640);
        nexusPageHost.Children.Add(tabsStack);

        // 5. Right Details Side at (920, 250, w=612)
        var sideStack = new StackPanel { Width = 612 };

        var card = new Border
        {
            Width = 612,
            Padding = new Thickness(28, 26, 28, 26),
            CornerRadius = new CornerRadius(8),
            Background = NexusKit.PanelBrush
        };
        var cardStack = new StackPanel();
        var sourceTitle = new TextBlock { FontFamily = NexusKit.Display, FontSize = 24, FontWeight = FontWeights.SemiBold, Foreground = NexusKit.WhiteBrush, TextWrapping = TextWrapping.Wrap };
        sourceTitle.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Text") { Source = tester.SourceLabel });
        cardStack.Children.Add(sourceTitle);
        foreach (var (lbl, blk) in new[] { ("Pressed", tester.PressedBlock), ("Input ownership", tester.OwnershipBlock) })
        {
            cardStack.Children.Add(new TextBlock { Text = lbl, FontFamily = NexusKit.Body, FontSize = 14, Foreground = NexusKit.SubBrush, Margin = new Thickness(0, 14, 0, 2) });
            var val = new TextBlock { FontFamily = NexusKit.Body, FontSize = 15, Foreground = NexusKit.B(NexusKit.A(Colors.White, 0.78)), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 2) };
            val.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Text") { Source = blk });
            cardStack.Children.Add(val);
        }
        cardStack.Children.Add(new Border { Height = 14 });

        var btnStack = new StackPanel { Orientation = Orientation.Horizontal };
        var exitBtn = NexusKit.TheatricalButton("Exit Tester", primary: true, height: 46, fontSize: 15, onClick: () => { tester.ClickExit(); ShowButtons(); });
        exitBtn.Margin = new Thickness(0, 0, 10, 0);
        btnStack.Children.Add(exitBtn);

        var kbBtn = NexusKit.TheatricalButton(tester.KeyboardOn ? "Keyboard test: on" : "Keyboard test", primary: false, height: 46, fontSize: 15, onClick: () => { tester.ClickKeyboard(); Dispatcher.BeginInvoke(new Action(NexusShowTester)); });
        btnStack.Children.Add(kbBtn);
        cardStack.Children.Add(btnStack);

        cardStack.Children.Add(new TextBlock
        {
            Text = "WASD · Left stick    Arrows · Right stick    Q · Hold LT    E · Hold RT",
            FontFamily = NexusKit.Body,
            FontSize = 13,
            Foreground = NexusKit.SubBrush,
            Margin = new Thickness(0, 16, 0, 0)
        });

        card.Child = cardStack;
        sideStack.Children.Add(card);

        Canvas.SetLeft(sideStack, 920);
        Canvas.SetTop(sideStack, 250);
        nexusPageHost.Children.Add(sideStack);

        // 7. Bottom Hints
        var hints = NexusKit.HintsBar(new[] { ("B", "Exit") });
        Canvas.SetRight(hints, 62);
        Canvas.SetTop(hints, 856);
        nexusPageHost.Children.Add(hints);
    }
}
