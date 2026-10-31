using System.Windows.Input;
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
        // Tide direct navigation

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

    private string lastUsedTideSection = "Buttons";

    private void TideShowDashboard()
    {
        if (tidePageHost == null) return;
        tidePageHost.Children.Clear();
        var board = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };

        // 1. Cinematic Night Sky & Breaking Wave Swell Background (Single Source of Truth)
        board.Children.Add(TideKit.SeaBackground(1));

        // 2. Top Navigation & Controller Telemetry Bar
        var topBar = new Canvas { Width = 1504, Height = 64 };
        Canvas.SetLeft(topBar, 48);
        Canvas.SetTop(topBar, 20);

        // Theme Emblem & Title
        var titleStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var moonIcon = new Border
        {
            Width = 28,
            Height = 28,
            CornerRadius = new CornerRadius(14),
            Background = new RadialGradientBrush
            {
                Center = new Point(0.4, 0.4),
                GradientStops =
                {
                    new(Colors.White, 0.0),
                    new(TideKit.CyanFoam, 0.7),
                    new(TideKit.CyanWave, 1.0)
                }
            },
            Margin = new Thickness(0, 0, 12, 0),
            Effect = new DropShadowEffect { Color = Color.FromRgb(0x9f, 0xe0, 0xff), BlurRadius = 14, ShadowDepth = 0, Opacity = 0.9 }
        };
        titleStack.Children.Add(moonIcon);
        titleStack.Children.Add(new TextBlock
        {
            Text = "TIDE // CENTRAL COMMAND",
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 20,
            Foreground = TideKit.WhiteBrush,
            VerticalAlignment = VerticalAlignment.Center
        });
        topBar.Children.Add(titleStack);
        Canvas.SetLeft(titleStack, 0);
        Canvas.SetTop(titleStack, 14);

        // Honest Active Controller Telemetry Plaque (Rule 6.0.C: No invented 1000 HZ / 100% battery)
        var statusCard = TideKit.OceanGlassCard(radius: new CornerRadius(16));
        statusCard.Padding = new Thickness(18, 6, 18, 6);
        var spStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        spStack.Children.Add(new TextBlock
        {
            Text = $"PLAYER {Player + 1} · {Model.Name.ToUpperInvariant()}",
            FontFamily = TideKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 13,
            Foreground = TideKit.CyanFoamBrush
        });
        spStack.Children.Add(new TextBlock
        {
            Text = " · LOCAL DRAFT READY",
            FontFamily = TideKit.Body,
            FontWeight = FontWeights.Bold,
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromArgb(0xaa, 0xc0, 0xdc, 0xf8)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(6, 0, 0, 0)
        });
        statusCard.Child = spStack;
        topBar.Children.Add(statusCard);
        Canvas.SetLeft(statusCard, 500);
        Canvas.SetTop(statusCard, 8);

        // Top-Right Quick Actions
        var rightStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var zoneBtn = TideKit.OceanButton("CONTROLLER ZONE", icon: "\uE701", onClick: () => BackRequested?.Invoke());
        zoneBtn.Margin = new Thickness(0, 0, 10, 0);
        rightStack.Children.Add(zoneBtn);

        var toolsBtn = TideKit.OceanButton("TOOLS HUB", icon: "\uE713", onClick: () => ToolsRequested?.Invoke());
        rightStack.Children.Add(toolsBtn);

        topBar.Children.Add(rightStack);
        Canvas.SetRight(rightStack, 0);
        Canvas.SetTop(rightStack, 10);

        board.Children.Add(topBar);

        // 3. Left Section — Controller Spotlight Hero Stage
        var heroStage = new Canvas { Width = 560, Height = 700 };
        Canvas.SetLeft(heroStage, 48);
        Canvas.SetTop(heroStage, 95);

        // Pedestal Ripples beneath controller
        var ripples = TideKit.PedestalRipples(480, 120);
        Canvas.SetLeft(ripples, 40);
        Canvas.SetTop(ripples, 380);
        heroStage.Children.Add(ripples);

        // Active Controller Art
        var controllerArt = TideKit.ControllerArt(Model.Id, 480);
        Canvas.SetLeft(controllerArt, 40);
        Canvas.SetTop(controllerArt, 90);
        heroStage.Children.Add(controllerArt);
        TideKit.AnimateFloat(controllerArt, 0, 6, 4.5);

        // Telemetry Badges below Controller (Section 5.1: keep only real values; drop Hall sensors chip)
        var badgeStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        string[] badges =
        {
            "PROFILE: DEFAULT DRAFT",
            $"REMAPS: {Draft.Changes.Count} CUSTOM"
        };

        foreach (var bText in badges)
        {
            var badgePill = new Border
            {
                CornerRadius = new CornerRadius(12),
                Background = new SolidColorBrush(Color.FromArgb(0x80, 0x06, 0x16, 0x40)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x60, 0x9f, 0xf6, 0xff)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(14, 5, 14, 5),
                Margin = new Thickness(5, 0, 5, 0)
            };
            badgePill.Child = new TextBlock
            {
                Text = bText,
                FontFamily = TideKit.Body,
                FontWeight = FontWeights.Bold,
                FontSize = 11,
                Foreground = TideKit.CyanFoamBrush
            };
            badgeStack.Children.Add(badgePill);
        }
        Canvas.SetLeft(badgeStack, 120);
        Canvas.SetTop(badgeStack, 480);
        heroStage.Children.Add(badgeStack);

        board.Children.Add(heroStage);

        // 4. Right Section — 6 Interactive Workbench Feature Decks (2x3 Grid)
        var gridCanvas = new Canvas { Width = 920, Height = 700 };
        Canvas.SetLeft(gridCanvas, 630);
        Canvas.SetTop(gridCanvas, 95);

        var decks = new (string Key, string Icon, string Title, string Status, string Sub, string BtnText, Action Action, string Tag)[]
        {
            (
                "Buttons",
                "\uE765",
                "BUTTON REMAPPING",
                $"{Draft.Changes.Count} CUSTOM ASSIGNMENTS",
                "Independent digital mappings, turbo fire and macro hotkeys for all physical inputs.",
                "OPEN REMAP STUDIO ➔",
                () => { lastUsedTideSection = "Buttons"; ShowButtons(); },
                "theme:tide:dash:buttons"
            ),
            (
                "Curves",
                "\uE9D9",
                "RESPONSE CURVES",
                "STICK & TRIGGER CURVES",
                "Dual hall-effect stick tuning, trigger throw thresholds and dynamic deadzone contours.",
                "TUNE CURVES ➔",
                () => { lastUsedTideSection = "Curves"; ShowCurves(true); },
                "theme:tide:dash:curves"
            ),
            (
                "Macros",
                "\uE7C3",
                "MACRO SEQUENCES",
                "4 PADDLE SLOTS READY",
                "Record complex combo chains, rapid-fire trigger loops and millisecond timed actions.",
                "MACRO STUDIO ➔",
                () => { lastUsedTideSection = "Macros"; ShowMacros(true); },
                "theme:tide:dash:macros"
            ),
            (
                "Vibration",
                "\uEC42",
                "HAPTIC VIBRATION",
                "GRIP MOTOR STRENGTH",
                "Independent asymmetric grip motor tuning and hall trigger impulse rumble mixer.",
                "HAPTIC MIXER ➔",
                () => { lastUsedTideSection = "Vibration"; ShowVibration(true); },
                "theme:tide:dash:vibration"
            ),
            (
                "PcMode",
                "\uE7F8",
                "PC MODE & ROUTING",
                "VIRTUAL XINPUT ROUTING",
                "Virtual Xbox device emulation for full game compatibility without hardware writes.",
                "MANAGE ROUTING ➔",
                () => { lastUsedTideSection = "PcMode"; ShowManagement("PcMode"); },
                "theme:tide:dash:pcmode"
            ),
            (
                "Tester",
                "\uE768",
                "HARDWARE TESTER",
                "LIVE OSCILLOSCOPE RADAR",
                "Interactive visualizer for stick centering, circularity benchmarks and latency.",
                "LAUNCH TESTER ➔",
                () => { lastUsedTideSection = "Tester"; ShowManagement("Tester"); },
                "theme:tide:dash:tester"
            )
        };

        for (int i = 0; i < decks.Length; i++)
        {
            var d = decks[i];
            double col = (i % 2) * 455;
            double row = (i / 2) * 228;
            bool isPrimary = string.Equals(d.Key, lastUsedTideSection, StringComparison.OrdinalIgnoreCase);

            var card = TideKit.OceanGlassCard(w: 435, h: 215);

            var cardStack = new StackPanel();

            // Header Row: 28px Icon + Title
            var hStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            hStack.Children.Add(new TextBlock
            {
                Text = d.Icon,
                FontFamily = TideKit.Icons,
                FontSize = 20,
                Foreground = isPrimary ? TideKit.CyanFoamBrush : new SolidColorBrush(Color.FromArgb(0xaa, 0x9f, 0xf6, 0xff)),
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            hStack.Children.Add(new TextBlock
            {
                Text = d.Title,
                FontFamily = TideKit.Display,
                FontWeight = FontWeights.Black,
                FontStyle = FontStyles.Italic,
                FontSize = 14,
                Foreground = TideKit.WhiteBrush,
                VerticalAlignment = VerticalAlignment.Center
            });
            cardStack.Children.Add(hStack);

            // Status Pill
            var statusPill = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                CornerRadius = new CornerRadius(10),
                Background = new SolidColorBrush(Color.FromArgb(0x40, 0x18, 0x5a, 0xe0)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x50, 0x9f, 0xf6, 0xff)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 3, 10, 3),
                Margin = new Thickness(0, 0, 0, 8)
            };
            statusPill.Child = new TextBlock
            {
                Text = d.Status,
                FontFamily = TideKit.Body,
                FontWeight = FontWeights.Bold,
                FontSize = 11,
                Foreground = TideKit.CyanFoamBrush
            };
            cardStack.Children.Add(statusPill);

            // Subtitle Description
            cardStack.Children.Add(new TextBlock
            {
                Text = d.Sub,
                FontFamily = TideKit.Body,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromArgb(0xbb, 0xc0, 0xdc, 0xf8)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 12),
                Height = 34
            });

            // Action Button
            FrameworkElement actionBtn = isPrimary
                ? TideKit.SparkButton(d.BtnText, onClick: d.Action)
                : TideKit.OceanButton(d.BtnText, primary: false, onClick: d.Action);
            actionBtn.HorizontalAlignment = HorizontalAlignment.Right;
            cardStack.Children.Add(actionBtn);

            card.Child = cardStack;
            Canvas.SetLeft(card, col);
            Canvas.SetTop(card, row);

            Action act = d.Action;
            gridCanvas.Children.Add(ThemeAction(card, d.Tag, act));
        }

        board.Children.Add(gridCanvas);

        // 5. Bottom Navigation Dock
        var navDock = TideKit.OceanDock("Dashboard", TideSelectPage);
        Canvas.SetLeft(navDock, 230);
        Canvas.SetTop(navDock, 820);
        board.Children.Add(navDock);

        tidePageHost.Children.Add(board);
    }

}