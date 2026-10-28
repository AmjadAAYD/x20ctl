using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Nexus;
using X20Ctl.Zone;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private bool IsNexus => ThemeCatalog.Current.Id == "nexus";

    private Grid? nexusLayer;
    private Canvas? nexusStage;
    private Canvas? nexusPageHost;
    private bool nexusQueued;
    private string currentNexusPage = "Dashboard";

    private void BuildNexusStudio()
    {
        if (!IsNexus || nexusLayer != null) return;

        RootLayout.Visibility = Visibility.Collapsed;
        StudioBackdrop.Visibility = Visibility.Collapsed;

        nexusStage = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };

        nexusPageHost = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };
        Canvas.SetLeft(nexusPageHost, 0);
        Canvas.SetTop(nexusPageHost, 0);
        nexusStage.Children.Add(nexusPageHost);

        nexusLayer = new Grid
        {
            Background = NexusKit.HouseBrush,
            Children = { new Viewbox { Stretch = Stretch.Uniform, Child = nexusStage } }
        };
        Panel.SetZIndex(nexusLayer, 30);
        StudioShell.Children.Add(nexusLayer);

        NexusShow();
    }

    private void NexusLater()
    {
        if (!IsNexus || nexusQueued) return;
        nexusQueued = true;
        Dispatcher.InvokeAsync(() =>
        {
            nexusQueued = false;
            NexusShow();
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    public void NexusSelectPage(string target)
    {
        switch (target.ToLowerInvariant())
        {
            case "buttons":
                ShowButtons();
                break;
            case "curves":
                ShowCurves(true);
                break;
            case "macros":
                ShowMacros(true);
                break;
            case "vibration":
                ShowVibration(true);
                break;
            case "pcmode":
                ShowManagement("PcMode");
                break;
            case "device":
                ShowManagement("Device");
                break;
            case "mouse":
                ShowManagement("Mouse");
                break;
            case "setups":
                ShowManagement("Setups");
                break;
            case "tester":
                ShowManagement("Tester");
                break;
            case "diagnostics":
                ShowManagement("Diagnostics");
                break;
            case "tools":
                ToolsRequested?.Invoke();
                break;
            case "dashboard":
            default:
                currentNexusPage = "Dashboard";
                NexusShowDashboard();
                break;
        }
    }

    private void NexusShow()
    {
        if (!IsNexus || nexusPageHost == null) return;

        string activePage = CurrentPage;
        currentNexusPage = activePage;
        switch (activePage)
        {
            case "Buttons":
                NexusShowButtons();
                break;
            case "Curves":
                NexusShowCurves();
                break;
            case "Macros":
                NexusShowMacros();
                break;
            case "Vibration":
                NexusShowVibration();
                break;
            case "PcMode":
                NexusShowPcMode();
                break;
            case "Device":
                NexusShowDevice();
                break;
            case "Mouse":
                NexusShowMouse();
                break;
            case "Setups":
                NexusShowSetups();
                break;
            case "Tester":
                NexusShowTester();
                break;
            case "Diagnostics":
                NexusShowDiagnostics();
                break;
            case "Dashboard":
            default:
                currentNexusPage = "Dashboard";
                NexusShowDashboard();
                break;
        }
    }

    private void NexusShowDashboard()
    {
        nexusPageHost!.Children.Clear();

        // 1. Key-art band across top 560px
        var keyband = NexusKit.KeyArtBand(560, opacity: 1.0, featModel: "x20pro", secondModel: "x15-starfield");
        nexusPageHost.Children.Add(keyband);

        // 2. Top bar at (left: 62, top: 46)
        var topBar = NexusKit.TopBar(
            player: Player,
            model: "EasySMX X20 Pro",
            status: "Offline",
            onZone: () => BackRequested?.Invoke(),
            onSetups: () => ShowManagement("Setups"),
            onPcMode: () => ShowManagement("PcMode"),
            onTools: () => ToolsRequested?.Invoke()
        );
        Canvas.SetLeft(topBar, 62);
        Canvas.SetTop(topBar, 46);
        nexusPageHost.Children.Add(topBar);

        // 3. Tile Row at y = 515 (Focused tile at 475)
        // Focused Buttons tile: 212x174 at (68, 475) with 4px green border
        var btnCover = NexusKit.CoverTile("x20pro", 212, 174, "buttons", dark: 0.8, radius: 6);
        var focTile = NexusKit.Tile(212, 174, btnCover, "Buttons", focused: true, onClick: () => ShowButtons());
        Canvas.SetLeft(focTile, 68);
        Canvas.SetTop(focTile, 475);
        nexusPageHost.Children.Add(focTile);

        // Neighbor 133x133 tiles
        var neighborTiles = new (string? CoverModel, string? Page, string? Icon, Action Action)[]
        {
            ("x15-starfield", "curves", null, () => ShowCurves(true)),
            ("dune", "macros", null, () => ShowMacros(true)),
            ("x20", "vibration", null, () => ShowVibration(true)),
            ("d10-purple", "pcmode", null, () => ShowManagement("PcMode")),
            (null, null, "\uE7FC", () => ShowManagement("Device")),
            (null, null, "\uE962", () => ShowManagement("Mouse")),
            (null, null, "\uE8B5", () => ShowManagement("Setups")),
            (null, null, "\uE713", () => ToolsRequested?.Invoke())
        };

        double nx = 298;
        foreach (var (cModel, cPage, cIcon, cAct) in neighborTiles)
        {
            UIElement content;
            if (cModel != null && cPage != null)
            {
                content = NexusKit.CoverTile(cModel, 133, 133, cPage, radius: 6);
            }
            else
            {
                var iconBox = new Grid();
                iconBox.Children.Add(new TextBlock
                {
                    Text = cIcon ?? "",
                    FontFamily = NexusKit.Icons,
                    FontSize = 46,
                    Foreground = NexusKit.WhiteBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                });
                content = iconBox;
            }

            var tile = NexusKit.Tile(133, 133, content, null, focused: false, onClick: cAct);
            Canvas.SetLeft(tile, nx);
            Canvas.SetTop(tile, 515);
            nexusPageHost.Children.Add(tile);
            nx += 141;
        }

        // 4. Wide Tiles at y = 676 (368x192) at x = 68 + i*384
        // Tile 0: Browse your controllers
        var t0Canvas = new Canvas { Width = 368, Height = 192, ClipToBounds = true };
        var collageModels = new[] { "x20", "dune", "x15-wukong", "d10-purple" };
        for (int j = 0; j < collageModels.Length; j++)
        {
            var cTile = NexusKit.CoverTile(collageModels[j], 92, 92, "dashboard");
            Canvas.SetLeft(cTile, (j % 2) * 92);
            Canvas.SetTop(cTile, (j / 2) * 96);
            t0Canvas.Children.Add(cTile);
        }
        var t0Right = NexusKit.CoverTile("x10", 184, 192, "dashboard");
        Canvas.SetLeft(t0Right, 184);
        Canvas.SetTop(t0Right, 0);
        t0Canvas.Children.Add(t0Right);

        var t0 = NexusKit.Tile(368, 192, t0Canvas, "Browse your controllers", focused: false, onClick: () => BackRequested?.Invoke());
        Canvas.SetLeft(t0, 68);
        Canvas.SetTop(t0, 676);
        nexusPageHost.Children.Add(t0);

        // Tile 1: Customize your home
        var t1Grid = new Grid
        {
            Background = new LinearGradientBrush
            {
                StartPoint = new(0, 0), EndPoint = new(1, 1),
                GradientStops = { new(NexusKit.C("#1f3a3a"), 0.0), new(NexusKit.C("#162426"), 1.0) }
            }
        };
        t1Grid.Children.Add(new TextBlock
        {
            Text = "\uE771", // Palette
            FontFamily = NexusKit.Icons,
            FontSize = 52,
            Foreground = NexusKit.WhiteBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        });
        var t1 = NexusKit.Tile(368, 192, t1Grid, "Customize your home", focused: false, onClick: () => ToolsRequested?.Invoke());
        Canvas.SetLeft(t1, 68 + 384);
        Canvas.SetTop(t1, 676);
        nexusPageHost.Children.Add(t1);

        // Tile 2: Setups
        var t2Canvas = new Canvas { Width = 368, Height = 192, ClipToBounds = true };
        var t2Cover = NexusKit.CoverTile("x05pro", 368, 192, "setups", dark: 0.7);
        t2Canvas.Children.Add(t2Cover);
        var t2Badge = new Border
        {
            Background = NexusKit.WhiteBrush,
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(8, 3, 8, 3),
            Child = new TextBlock
            {
                Text = "LOCAL",
                FontFamily = NexusKit.Body,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = NexusKit.B("#111111")
            }
        };
        Canvas.SetLeft(t2Badge, 12);
        Canvas.SetTop(t2Badge, 12);
        t2Canvas.Children.Add(t2Badge);

        var t2 = NexusKit.Tile(368, 192, t2Canvas, "Setups", focused: false, onClick: () => ShowManagement("Setups"));
        Canvas.SetLeft(t2, 68 + 384 * 2);
        Canvas.SetTop(t2, 676);
        nexusPageHost.Children.Add(t2);

        // Tile 3: Test controller
        var t3Canvas = new Canvas { Width = 368, Height = 192, ClipToBounds = true };
        var t3Bg = new Rectangle { Width = 368, Height = 192, Fill = NexusKit.B("#1b1c1f") };
        t3Canvas.Children.Add(t3Bg);
        var t3Art = NexusKit.ControllerArt("x20-black", 220);
        if (t3Art != null)
        {
            t3Art.Effect = new DropShadowEffect { BlurRadius = 16, ShadowDepth = 12, Direction = 270, Opacity = 0.8, Color = Colors.Black };
            Canvas.SetLeft(t3Art, 150);
            Canvas.SetTop(t3Art, 30);
            t3Canvas.Children.Add(t3Art);
        }
        var t3 = NexusKit.Tile(368, 192, t3Canvas, "Test controller", focused: false, onClick: () => ShowManagement("Tester"));
        Canvas.SetLeft(t3, 68 + 384 * 3);
        Canvas.SetTop(t3, 676);
        nexusPageHost.Children.Add(t3);
    }

    private void NexusShowButtons()
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

        // 3. Title & Tabs
        var title = new TextBlock
        {
            Text = "Buttons",
            FontFamily = NexusKit.Display,
            FontSize = 38,
            FontWeight = FontWeights.SemiBold,
            Foreground = NexusKit.WhiteBrush
        };
        Canvas.SetLeft(title, 68);
        Canvas.SetTop(title, 140);
        nexusPageHost.Children.Add(title);

        var tabsStack = new StackPanel { Orientation = Orientation.Horizontal };
        string[] tabs = { "Face", "D-pad", "Bumpers", "Triggers", "Rear", "Thumbsticks" };
        for (int i = 0; i < tabs.Length; i++)
        {
            string t = tabs[i];
            bool on = (i == 0);
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
        Canvas.SetLeft(tabsStack, 68);
        Canvas.SetTop(tabsStack, 200);
        nexusPageHost.Children.Add(tabsStack);

        // 4. Left Panel at (68, 264, w=560, h=560)
        var panel = new Border
        {
            Width = 560,
            Height = 560,
            Padding = new Thickness(30, 26, 30, 26),
            CornerRadius = new CornerRadius(8),
            Background = NexusKit.PanelBrush
        };
        var pStack = new StackPanel();
        pStack.Children.Add(new TextBlock
        {
            Text = "Button mapping",
            FontFamily = NexusKit.Body,
            FontSize = 15,
            Foreground = NexusKit.SubBrush,
            Margin = new Thickness(0, 0, 0, 16)
        });

        // 4 mapping rows: A, B, X, Y
        var rows = new (string Key, string Target)[]
        {
            ("A", "B"),
            ("B", "B"),
            ("X", "X"),
            ("Y", "Y")
        };

        for (int i = 0; i < rows.Length; i++)
        {
            var r = rows[i];
            bool isFirst = (i == 0);

            var rowGrid = new Grid { Height = 74 };
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Bottom border divider
            var divider = new Rectangle
            {
                Height = 1,
                Fill = NexusKit.B(NexusKit.A(Colors.White, 0.06)),
                VerticalAlignment = VerticalAlignment.Bottom
            };
            Grid.SetColumnSpan(divider, 2);
            rowGrid.Children.Add(divider);

            var keyLabel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var badge = new Border
            {
                Width = 34,
                Height = 34,
                CornerRadius = new CornerRadius(17),
                Background = NexusKit.InputBrush,
                Margin = new Thickness(0, 0, 14, 0),
                Child = new TextBlock
                {
                    Text = r.Key,
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 15,
                    FontWeight = FontWeights.Bold,
                    Foreground = NexusKit.WhiteBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            keyLabel.Children.Add(badge);
            keyLabel.Children.Add(new TextBlock
            {
                Text = $"{r.Key} button",
                FontFamily = NexusKit.Body,
                FontSize = 19,
                Foreground = NexusKit.WhiteBrush,
                VerticalAlignment = VerticalAlignment.Center
            });
            Grid.SetColumn(keyLabel, 0);
            rowGrid.Children.Add(keyLabel);

            var dd = NexusKit.Dropdown(r.Target, minWidth: 230, focused: isFirst);
            Grid.SetColumn(dd, 1);
            rowGrid.Children.Add(dd);

            pStack.Children.Add(rowGrid);
        }

        // Bottom buttons in panel: Reset this control, Test mapping
        var bottomBtns = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 30, 0, 0) };
        var resetBtn = NexusKit.TheatricalButton("Reset this control", primary: false, height: 50, fontSize: 16);
        resetBtn.Width = 244;
        resetBtn.Margin = new Thickness(0, 0, 12, 0);
        bottomBtns.Children.Add(resetBtn);

        var testBtn = NexusKit.TheatricalButton("Test mapping", primary: false, height: 50, fontSize: 16);
        testBtn.Width = 244;
        bottomBtns.Children.Add(testBtn);

        pStack.Children.Add(bottomBtns);
        panel.Child = pStack;

        Canvas.SetLeft(panel, 68);
        Canvas.SetTop(panel, 264);
        nexusPageHost.Children.Add(panel);

        // 5. Right Controller Art at (700, 300, w = 820)
        double cx = 700, cy = 300, cw = 820;
        string btnModel = (Model.Id == "x20" || string.IsNullOrEmpty(Model.Id)) ? "x20pro" : Model.Id;
        var padArt = NexusKit.ControllerArt(btnModel, cw);
        if (padArt != null)
        {
            padArt.Effect = new DropShadowEffect { BlurRadius = 40, ShadowDepth = 30, Direction = 270, Opacity = 0.6, Color = Colors.Black };
            Canvas.SetLeft(padArt, cx);
            Canvas.SetTop(padArt, cy);
            nexusPageHost.Children.Add(padArt);
        }

        // Glowing A-button circle at (ax - 30, ay - 30, 60, 60)
        // a_button(FEAT, 700, 300, 820) -> (1324.8, 483.2)
        double ax = 1325;
        double ay = 483;

        var ring = new Border
        {
            Width = 60,
            Height = 60,
            CornerRadius = new CornerRadius(30),
            BorderBrush = NexusKit.GreenBrush,
            BorderThickness = new Thickness(4),
            Effect = new DropShadowEffect
            {
                BlurRadius = 24,
                ShadowDepth = 0,
                Color = NexusKit.Green,
                Opacity = 0.7
            }
        };
        Canvas.SetLeft(ring, ax - 30);
        Canvas.SetTop(ring, ay - 30);
        nexusPageHost.Children.Add(ring);

        // Note: Local draft · Not written to controller memory.
        var draftNote = new TextBlock
        {
            Text = "Local draft · Not written to controller memory.",
            FontFamily = NexusKit.Body,
            FontSize = 15,
            Foreground = NexusKit.SubBrush
        };
        Canvas.SetLeft(draftNote, 700);
        Canvas.SetTop(draftNote, 820);
        nexusPageHost.Children.Add(draftNote);

        // 6. Bottom Hints
        var hints = NexusKit.HintsBar(new[] { ("A", "Select"), ("B", "Back"), ("X", "Clear") });
        Canvas.SetRight(hints, 62);
        Canvas.SetTop(hints, 856);
        nexusPageHost.Children.Add(hints);
    }
}
