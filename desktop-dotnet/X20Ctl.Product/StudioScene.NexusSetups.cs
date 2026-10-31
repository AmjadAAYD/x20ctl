using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Nexus;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private void NexusShowSetups()
    {
        nexusPageHost!.Children.Clear();

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
        string bgModel = (Model.Id == "x20" || string.IsNullOrEmpty(Model.Id)) ? "x20pro" : Model.Id;
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
            Text = " Go Back  ·  My setups",
            FontFamily = NexusKit.Icons,
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
            Text = "Competitive FPS",
            FontFamily = NexusKit.Display,
            FontSize = 54,
            FontWeight = FontWeights.SemiBold,
            Foreground = NexusKit.WhiteBrush
        });
        titleStack.Children.Add(new TextBlock
        {
            Text = "EasySMX X20 Pro · Matches assigned model · Saved locally",
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
        var actions = new (string Text, string Icon, bool Focused)[]
        {
            ("Open", "\uE768", true),
            ("Duplicate", "\uE8C8", false),
            ("Rename", "\uE8AC", false),
            ("Export Setup", "\uEDE1", false),
            ("More ▾", "\uE712", false)
        };
        foreach (var (t, ic, foc) in actions)
        {
            var btn = NexusKit.TheatricalButton(t, primary: foc, height: 50, fontSize: 16, key: ic);
            btn.Margin = new Thickness(0, 0, 12, 0);
            actRow.Children.Add(btn);
        }
        Canvas.SetLeft(actRow, 68);
        Canvas.SetTop(actRow, 376);
        nexusPageHost.Children.Add(actRow);

        // 7. Stats panels at (68, 478)
        var statRow = new StackPanel { Orientation = Orientation.Horizontal };
        var stats = new (string Label, string Value)[]
        {
            ("Buttons", $"{Draft.UnsentChanges} changed"),
            ("Curves", $"{Curves.Draft.ModifiedChannels} changed"),
            ("Macros", $"{Macros?.Draft.ModifiedSlots ?? 0} sequences"),
            ("Vibration", $"{vibrationDraft.Strength}%")
        };
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
        var setupCards = new (string Title, string Page)[]
        {
            ("Competitive FPS", "setups"),
            ("Racing", "curves"),
            ("Everyday", "vibration")
        };
        for (int i = 0; i < setupCards.Length; i++)
        {
            var (sc, pageKey) = setupCards[i];
            bool foc = (i == 0);

            var tCanvas = new Canvas { Width = 300, Height = 190, ClipToBounds = true };
            var cover = NexusKit.CoverTile("x20pro", 300, 190, pageKey, dark: 0.8, radius: 6);
            tCanvas.Children.Add(cover);

            var localBadge = new Border
            {
                Background = NexusKit.WhiteBrush,
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(8, 3, 8, 3),
                Child = new TextBlock { Text = "LOCAL", FontFamily = NexusKit.Body, FontSize = 12, FontWeight = FontWeights.Bold, Foreground = NexusKit.B("#111111") }
            };
            Canvas.SetLeft(localBadge, 12);
            Canvas.SetTop(localBadge, 12);
            tCanvas.Children.Add(localBadge);

            var tile = NexusKit.Tile(300, 190, tCanvas, sc, focused: foc);
            Canvas.SetLeft(tile, tx);
            Canvas.SetTop(tile, 0);
            tileRow.Children.Add(tile);
            tx += 316;
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
        var addTile = NexusKit.Tile(190, 190, addGrid, "New setup", focused: false);
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
        var impTile = NexusKit.Tile(190, 190, impGrid, "Import Setup", focused: false);
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

    private void NexusShowTester()
    {
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
            Text = "Working Draft · No input · Gameplay tester",
            FontFamily = NexusKit.Body,
            FontSize = 17,
            Foreground = NexusKit.SubBrush
        };
        Canvas.SetLeft(lead, 70);
        Canvas.SetTop(lead, 192);
        nexusPageHost.Children.Add(lead);

        // 4. Center Controller Art at (68, 250, w=820)
        var padArt = NexusKit.ControllerArt(Model.Id, 560);
        if (padArt != null)
        {
            padArt.Effect = new DropShadowEffect { BlurRadius = 40, ShadowDepth = 25, Direction = 270, Opacity = 0.55, Color = Colors.Black };
            Canvas.SetLeft(padArt, 198);
            Canvas.SetTop(padArt, 260);
            nexusPageHost.Children.Add(padArt);
        }

        // Subtabs under controller: Front, Back
        var tabsStack = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var (t, on) in new[] { ("Front", true), ("Back", false) })
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
        cardStack.Children.Add(new TextBlock
        {
            Text = "Gameplay input unavailable",
            FontFamily = NexusKit.Display,
            FontSize = 24,
            FontWeight = FontWeights.SemiBold,
            Foreground = NexusKit.WhiteBrush
        });
        cardStack.Children.Add(new TextBlock
        {
            Text = "Gameplay source",
            FontFamily = NexusKit.Body,
            FontSize = 15,
            Foreground = NexusKit.SubBrush,
            Margin = new Thickness(0, 4, 0, 16)
        });

        cardStack.Children.Add(new TextBlock
        {
            Text = "Tester owns gameplay input. UI navigation is paused.\n\nWaiting for native gameplay input.\n\nHardware configuration remains separate.",
            FontFamily = NexusKit.Body,
            FontSize = 15,
            Foreground = NexusKit.B(NexusKit.A(Colors.White, 0.78)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 16)
        });

        var btnStack = new StackPanel { Orientation = Orientation.Horizontal };
        var exitBtn = NexusKit.TheatricalButton("Exit Tester", primary: true, height: 46, fontSize: 15, onClick: () => ShowButtons());
        exitBtn.Margin = new Thickness(0, 0, 10, 0);
        btnStack.Children.Add(exitBtn);

        var kbBtn = NexusKit.TheatricalButton("Keyboard Test", primary: false, height: 46, fontSize: 15);
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

        // 6. Bottom Gauges Shelf at (68, 740, w=1464)
        var gaugeShelf = new Border
        {
            Width = 1464, Height = 64,
            Padding = new Thickness(24, 0, 24, 0),
            CornerRadius = new CornerRadius(8),
            Background = NexusKit.PanelBrush
        };
        var gGrid = new Grid();
        gGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        gGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        gGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        gGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var gauges = new (string Name, string Val)[]
        {
            ("Left Stick", "X —  Y —"),
            ("Right Stick", "X —  Y —"),
            ("LT", "Unavailable"),
            ("RT", "Unavailable")
        };
        for (int i = 0; i < gauges.Length; i++)
        {
            var (gn, gv) = gauges[i];
            var gStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var iconCircle = new Border
            {
                Width = 40, Height = 40,
                CornerRadius = new CornerRadius(20),
                BorderBrush = NexusKit.B(NexusKit.A(Colors.White, 0.2)),
                BorderThickness = new Thickness(2),
                Margin = new Thickness(0, 0, 12, 0)
            };
            gStack.Children.Add(iconCircle);

            var tStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            tStack.Children.Add(new TextBlock { Text = gn, FontFamily = NexusKit.Body, FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = NexusKit.WhiteBrush });
            tStack.Children.Add(new TextBlock { Text = gv, FontFamily = NexusKit.Body, FontSize = 12, Foreground = NexusKit.SubBrush });
            gStack.Children.Add(tStack);

            Grid.SetColumn(gStack, i);
            gGrid.Children.Add(gStack);
        }
        gaugeShelf.Child = gGrid;
        Canvas.SetLeft(gaugeShelf, 68);
        Canvas.SetTop(gaugeShelf, 740);
        nexusPageHost.Children.Add(gaugeShelf);

        // 7. Bottom Hints
        var hints = NexusKit.HintsBar(new[] { ("B", "Hold to exit") });
        Canvas.SetRight(hints, 62);
        Canvas.SetTop(hints, 856);
        nexusPageHost.Children.Add(hints);
    }
}
