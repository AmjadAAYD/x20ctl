using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Tide;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private bool IsTide => ThemeCatalog.Current.Id == "tide";

    private Grid? tideLayer;
    private Canvas? tideStage;
    private Canvas? tidePageHost;
    private bool tideQueued;
    private bool tideAdvanced;
    private string currentTidePage = "Dashboard";

    private Canvas ConsoleSurface
    {
        get
        {
            if (tideLayer == null) BuildTideStudio();
            return tidePageHost!;
        }
    }

    private void BuildTideStudio()
    {
        if (!IsTide || tideLayer != null) return;

        RootLayout.Visibility = Visibility.Collapsed;
        StudioBackdrop.Visibility = Visibility.Collapsed;

        tideStage = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };

        tidePageHost = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };
        Canvas.SetLeft(tidePageHost, 0);
        Canvas.SetTop(tidePageHost, 0);
        tideStage.Children.Add(tidePageHost);

        tideLayer = new Grid
        {
            Background = TideKit.Sea0Brush,
            Children = { new Viewbox { Stretch = Stretch.Uniform, Child = tideStage } }
        };
        Panel.SetZIndex(tideLayer, 30);
        StudioShell.Children.Add(tideLayer);

        TideShow();
    }

    private void TideLater()
    {
        if (!IsTide || tideQueued) return;
        tideQueued = true;
        Dispatcher.InvokeAsync(() =>
        {
            tideQueued = false;
            TideShow();
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    public void TideSelectPage(string target)
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
                currentTidePage = "Dashboard";
                TideShowDashboard();
                break;
        }
    }

    private void TideShow()
    {
        if (!IsTide || tidePageHost == null) return;
        if (tideAdvanced)
        {
            TideShowAdvanced();
            return;
        }

        string activePage = CurrentPage;
        currentTidePage = activePage;
        switch (activePage)
        {
            case "Buttons":
                TideShowButtons();
                break;
            case "Curves":
                TideShowCurves();
                break;
            case "Macros":
                TideShowMacros();
                break;
            case "Vibration":
                TideShowVibration();
                break;
            case "PcMode":
                TideShowPcMode();
                break;
            case "Device":
                TideShowDevice();
                break;
            case "Mouse":
                TideShowMouse();
                break;
            case "Setups":
                TideShowSetups();
                break;
            case "Tester":
                TideShowTester();
                break;
            case "Diagnostics":
                TideShowDiagnostics();
                break;
            case "Dashboard":
            default:
                currentTidePage = "Dashboard";
                TideShowDashboard();
                break;
        }
    }

    private void TideShowDashboard()
    {
        if (tidePageHost == null) return;
        tidePageHost.Children.Clear();
        var board = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };

        // 1. Sea Background with "DASHBOARD"
        board.Children.Add(TideKit.SeaBackground(1, "DASHBOARD"));

        // 2. Character: x15-starfield at (-10, 430, w=600)
        board.Children.Add(TideKit.Character("x15-starfield", -10, 430, 600, -22));

        // 3. Status Box
        board.Children.Add(TideKit.StatusBox("19:24", isBack: true, onBack: () => BackRequested?.Invoke()));

        // 4. Top Row of tiles: Band + Round Badge + 4 tiles + Arrow
        board.Children.Add(TideKit.TopBand(34));

        // Round badge for Featured Pad
        var badgeTile = TideKit.RoundBadge("x15-starfield", 250);
        Canvas.SetLeft(badgeTile, 200);
        Canvas.SetTop(badgeTile, 110);
        Panel.SetZIndex(badgeTile, 2);
        board.Children.Add(badgeTile);

        // 4 feature tiles: (FEAT, "macros"), (SECOND, "vibration"), ("x20", "pcmode"), ("dune", "device")
        var tiles = new (string Key, string Page, Action Action)[]
        {
            ("x15-starfield", "macros", () => ShowMacros(true)),
            ("x10", "vibration", () => ShowVibration(true)),
            ("x20", "pcmode", () => ShowManagement("PcMode")),
            ("dune", "device", () => ShowManagement("Device"))
        };

        for (int i = 0; i < tiles.Length; i++)
        {
            var t = tiles[i];
            var tileBorder = TideKit.CoverTile(t.Key, 250, 250, t.Page);
            tileBorder.Cursor = System.Windows.Input.Cursors.Hand;
            tileBorder.MouseLeftButtonUp += (_, _) => t.Action();
            Canvas.SetLeft(tileBorder, 490 + i * 268);
            Canvas.SetTop(tileBorder, 52);
            board.Children.Add(tileBorder);
        }

        // Chevron arrow
        var arrow = new Border
        {
            Width = 58,
            Height = 58,
            CornerRadius = new CornerRadius(29),
            Background = TideKit.InkBrush,
            BorderBrush = TideKit.WhiteBrush,
            BorderThickness = new Thickness(3)
        };
        arrow.Child = new TextBlock
        {
            Text = "\uE76C",
            FontFamily = TideKit.Icons,
            FontSize = 28,
            Foreground = TideKit.WhiteBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        Canvas.SetRight(arrow, 22);
        Canvas.SetTop(arrow, 160);
        board.Children.Add(arrow);

        // 5. Slanted Menu Words at (660, 712): BUTTONS, CURVES, SETUPS, TOOLS
        var menuItems = new (string Word, Action Action)[]
        {
            ("BUTTONS", () => ShowButtons()),
            ("CURVES", () => ShowCurves(true)),
            ("SETUPS", () => ShowManagement("Setups")),
            ("TOOLS", () => ShowManagement("Device"))
        };

        for (int i = 0; i < menuItems.Length; i++)
        {
            var item = menuItems[i];
            var word = TideKit.SlantedWord(item.Word, focused: (i == 0), fs: 76, onClick: item.Action);
            Canvas.SetLeft(word, 660 + i * 128);
            Canvas.SetTop(word, 712 - 76);
            board.Children.Add(word);
        }

        // 6. Tilted Keys at (600, 772): zone, test, settings, E7E8
        string[] glyphs = ["\uE7FC", "\uE768", "\uE713", "\uE7E8"];
        for (int i = 0; i < glyphs.Length; i++)
        {
            int kIndex = i;
            var keyBtn = TideKit.KeyButton(glyphs[i], focused: (i == 0), rot: -12, onClick: () =>
            {
                if (kIndex == 0) BackRequested?.Invoke();
                else if (kIndex == 1) ShowManagement("Tester");
                else if (kIndex == 2) ShowManagement("Device");
            });
            Canvas.SetLeft(keyBtn, 600 + i * 132);
            Canvas.SetTop(keyBtn, 772 - i * 12);
            board.Children.Add(keyBtn);
        }

        // 7. Avatar Grid bottom right
        board.Children.Add(TideKit.AvatarGrid(["x15-starfield", "x10", "x20", "dune", "d10-purple", "x20pro"], 1250, 650));

        // 8. Hints
        var hintsBar = TideKit.HintsBar(new[] { "\u2295 Options", "\u24B6 Start" });
        Canvas.SetRight(hintsBar, 40);
        Canvas.SetBottom(hintsBar, 26);
        board.Children.Add(hintsBar);

        tidePageHost.Children.Add(board);
    }

    private void TideShowButtons()
    {
        if (tidePageHost == null) return;
        tidePageHost.Children.Clear();
        var board = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };

        // 1. Sea Background with "BUTTONS"
        board.Children.Add(TideKit.SeaBackground(7, "BUTTONS"));

        // 2. Character: x15-starfield at (40, 330, w=700) rotated 0deg
        board.Children.Add(TideKit.Character("x15-starfield", 40, 330, 700, 0));

        // Glowing A-button at pad focal point (525, 490)
        var glowingA = new Border
        {
            Width = 72,
            Height = 72,
            CornerRadius = new CornerRadius(36),
            BorderBrush = TideKit.SpikeRedBrush,
            BorderThickness = new Thickness(7),
            Effect = new DropShadowEffect { BlurRadius = 0, ShadowDepth = 0, Color = TideKit.Ink }
        };
        Canvas.SetLeft(glowingA, 525);
        Canvas.SetTop(glowingA, 490);
        board.Children.Add(glowingA);

        // 3. Status Box
        board.Children.Add(TideKit.StatusBox("19:24", isBack: true, onBack: () => TideShowDashboard()));

        // 4. Top Row: Groups (Face, D-pad, Bumpers, Triggers, Rear, Sticks, System)
        board.Children.Add(TideKit.TopBand(86));

        string[] groups = ["Face", "D-pad", "Bumpers", "Triggers", "Rear", "Sticks", "System"];
        for (int i = 0; i < 4; i++)
        {
            string g = groups[i];
            bool on = (i == 0);
            var tile = new Border
            {
                Width = 250,
                Height = 250,
                Background = on ? TideKit.WhiteBrush : TideKit.Sea0Brush,
                BorderBrush = TideKit.WhiteBrush,
                BorderThickness = new Thickness(4),
                CornerRadius = new CornerRadius(6),
                Effect = new DropShadowEffect { BlurRadius = 18, ShadowDepth = 6, Direction = 270, Opacity = 0.45, Color = Color.FromArgb(255, 0, 0, 40) }
            };

            tile.Child = new TextBlock
            {
                Text = g.ToUpperInvariant(),
                FontFamily = TideKit.Display,
                FontWeight = FontWeights.Black,
                FontStyle = FontStyles.Italic,
                FontSize = 34,
                Foreground = on ? TideKit.Sea0Brush : TideKit.WhiteBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            Canvas.SetLeft(tile, 210 + i * 268);
            Canvas.SetTop(tile, 104);
            board.Children.Add(tile);
        }

        // Chevron arrow
        var arrow = new Border
        {
            Width = 58,
            Height = 58,
            CornerRadius = new CornerRadius(29),
            Background = TideKit.InkBrush,
            BorderBrush = TideKit.WhiteBrush,
            BorderThickness = new Thickness(3)
        };
        arrow.Child = new TextBlock
        {
            Text = "\uE76C",
            FontFamily = TideKit.Icons,
            FontSize = 28,
            Foreground = TideKit.WhiteBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        Canvas.SetRight(arrow, 22);
        Canvas.SetTop(arrow, 160);
        board.Children.Add(arrow);

        // 5. Note Sticker: LOCAL DRAFT at (1230, 420)
        var note = TideKit.NoteSticker("LOCAL DRAFT\nNOT WRITTEN TO CONTROLLER MEMORY", -3);
        Canvas.SetLeft(note, 1230);
        Canvas.SetTop(note, 420);
        board.Children.Add(note);

        // 6. Slanted Menu Words at (840, 760): A ▸ B, B ▸ B, X ▸ X, Y ▸ Y
        string[] mapWords = ["A \u25B8 B", "B \u25B8 B", "X \u25B8 X", "Y \u25B8 Y"];
        for (int i = 0; i < mapWords.Length; i++)
        {
            var word = TideKit.SlantedWord(mapWords[i], focused: (i == 0), fs: 88);
            Canvas.SetLeft(word, 840 + i * 160);
            Canvas.SetTop(word, 760 - 88);
            board.Children.Add(word);
        }

        // 7. Tilted Keys at (820, 800): reset, test, buttons
        string[] btnKeys = ["\uE777", "\uE768", "\uE7FC"];
        for (int i = 0; i < btnKeys.Length; i++)
        {
            var kBtn = TideKit.KeyButton(btnKeys[i], focused: (i == 0), rot: -12);
            Canvas.SetLeft(kBtn, 820 + i * 132);
            Canvas.SetTop(kBtn, 800 - i * 12);
            board.Children.Add(kBtn);
        }

        // 8. Hints
        var hintsBar = TideKit.HintsBar(new[] { "\u24BE All", "\u24B6 Assign" });
        Canvas.SetRight(hintsBar, 40);
        Canvas.SetBottom(hintsBar, 26);
        board.Children.Add(hintsBar);

        tidePageHost.Children.Add(board);
    }
}
