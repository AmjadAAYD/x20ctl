using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Sunrise;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private bool IsSunrise => ThemeCatalog.Current.Id == "sunrise";

    private Grid? sunriseLayer;
    private Canvas? sunriseStage;
    private Canvas? sunrisePageHost;
    private bool sunriseQueued;
    private string currentSunrisePage = "Dashboard";

    private void BuildSunriseStudio()
    {
        if (!IsSunrise || sunriseLayer != null) return;

        RootLayout.Visibility = Visibility.Collapsed;
        StudioBackdrop.Visibility = Visibility.Collapsed;

        sunriseStage = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };

        sunrisePageHost = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };
        Canvas.SetLeft(sunrisePageHost, 0);
        Canvas.SetTop(sunrisePageHost, 0);
        sunriseStage.Children.Add(sunrisePageHost);

        sunriseLayer = new Grid
        {
            Background = SunriseKit.YellowBrush,
            Children = { new Viewbox { Stretch = Stretch.Uniform, Child = sunriseStage } }
        };
        Panel.SetZIndex(sunriseLayer, 30);
        StudioShell.Children.Add(sunriseLayer);

        SunriseShow();
    }

    private void SunriseLater()
    {
        if (!IsSunrise || sunriseQueued) return;
        sunriseQueued = true;
        Dispatcher.InvokeAsync(() =>
        {
            sunriseQueued = false;
            SunriseShow();
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    public void SunriseSelectPage(string target)
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
            case "rgb":
            case "lighting":
                ShowManagement("RGB");
                break;
            case "screen":
                ShowManagement("Screen");
                break;
            case "tools":
                ToolsRequested?.Invoke();
                break;
            case "dashboard":
            default:
                currentSunrisePage = "Dashboard";
                SunriseShowDashboard();
                break;
        }
    }

    private void SunriseShow()
    {
        if (!IsSunrise || sunrisePageHost == null) return;

        string activePage = CurrentPage;
        currentSunrisePage = activePage;
        switch (activePage)
        {
            case "Buttons":
                SunriseShowButtons();
                break;
            case "Curves":
                SunriseShowCurves();
                break;
            case "Macros":
                SunriseShowMacros();
                break;
            case "Vibration":
                SunriseShowVibration();
                break;
            case "PcMode":
                SunriseShowPcMode();
                break;
            case "Device":
                SunriseShowDevice();
                break;
            case "Mouse":
                SunriseShowMouse();
                break;
            case "Setups":
                SunriseShowSetups();
                break;
            case "Tester":
                SunriseShowTester();
                break;
            case "Diagnostics":
                SunriseShowDiagnostics();
                break;
            case "Lighting":
                SunriseShowLighting();
                break;
            case "Screen":
                SunriseShowScreen();
                break;
            case "Dashboard":
            default:
                SunriseShowDashboard();
                break;
        }
    }

    private UIElement CreateSunriseHeader(string activeTab = "Studio")
    {
        var topBar = new Canvas
        {
            Width = 1600,
            Height = 60,
            Background = SunriseKit.YellowBrush,
            ClipToBounds = true
        };

        // Brand logo
        var brandSp = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        brandSp.Children.Add(new TextBlock
        {
            Text = "📺 X20CTL",
            FontFamily = SunriseKit.Display,
            FontWeight = FontWeights.Black,
            FontSize = 20,
            Foreground = SunriseKit.InkBrush,
            VerticalAlignment = VerticalAlignment.Center
        });
        Canvas.SetLeft(brandSp, 25);
        Canvas.SetTop(brandSp, 16);
        topBar.Children.Add(brandSp);

        // Center Tabs: Controller Zone, Studio, Agent XCTL, Tools
        var centerTabs = new StackPanel { Orientation = Orientation.Horizontal };
        var tabs = new[] { "Controller Zone", "Studio", "Agent XCTL", "Tools" };

        foreach (var tab in tabs)
        {
            bool isCurrent = (tab == activeTab);
            var tabBtn = new Border
            {
                CornerRadius = new CornerRadius(16),
                Background = isCurrent ? SunriseKit.InkBrush : SunriseKit.OrangeBrush,
                BorderBrush = SunriseKit.InkBrush,
                BorderThickness = new Thickness(isCurrent ? 0 : 1.5),
                Padding = new Thickness(16, 7, 16, 7),
                Margin = new Thickness(0, 0, 10, 0),
                Cursor = Cursors.Hand,
                Effect = new DropShadowEffect
                {
                    Color = Color.FromArgb(0x40, 0x1E, 0x1D, 0x1A),
                    BlurRadius = 4,
                    ShadowDepth = 2,
                    Direction = 315,
                    Opacity = 0.25
                }
            };
            var tabText = new TextBlock
            {
                Text = tab.ToUpperInvariant(),
                FontFamily = SunriseKit.Display,
                FontWeight = FontWeights.Bold,
                FontSize = 11.5,
                Foreground = isCurrent ? SunriseKit.CreamBrush : SunriseKit.InkBrush
            };
            tabBtn.Child = tabText;

            string target = tab;
            tabBtn.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                if (target == "Controller Zone") BackRequested?.Invoke();
                else if (target == "Agent XCTL") SunriseSelectPage("Dashboard");
                else if (target == "Tools") ToolsRequested?.Invoke();
                else SunriseSelectPage("Dashboard");
            };

            centerTabs.Children.Add(tabBtn);
        }

        Canvas.SetLeft(centerTabs, 265);
        Canvas.SetTop(centerTabs, 13);
        topBar.Children.Add(centerTabs);

        // Right Info Block
        var rightSp = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        rightSp.Children.Add(new TextBlock
        {
            Text = $"Player {Player + 1}  /  {Model.Name}  ·  Local editor",
            FontFamily = SunriseKit.Body,
            FontWeight = FontWeights.SemiBold,
            FontSize = 12.5,
            Foreground = SunriseKit.InkBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 20, 0)
        });

        // Window Controls (Near-black squares)
        var winControls = new StackPanel { Orientation = Orientation.Horizontal };
        var minBtn = new Border { Width = 26, Height = 26, CornerRadius = new CornerRadius(6), Background = SunriseKit.InkBrush, Margin = new Thickness(0, 0, 6, 0) };
        minBtn.Child = new TextBlock { Text = "–", FontFamily = SunriseKit.Display, FontWeight = FontWeights.Bold, FontSize = 12, Foreground = SunriseKit.CreamBrush, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var closeBtn = new Border { Width = 26, Height = 26, CornerRadius = new CornerRadius(6), Background = SunriseKit.InkBrush };
        closeBtn.Child = new TextBlock { Text = "✕", FontFamily = SunriseKit.Display, FontWeight = FontWeights.Bold, FontSize = 10, Foreground = SunriseKit.CreamBrush, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        winControls.Children.Add(minBtn);
        winControls.Children.Add(closeBtn);
        rightSp.Children.Add(winControls);

        Canvas.SetRight(rightSp, 36);
        Canvas.SetTop(rightSp, 14);
        topBar.Children.Add(rightSp);

        return topBar;
    }

    private UIElement CreateSunriseSidebar(string activeItem = "Dashboard")
    {
        var navSp = new StackPanel { Width = 236 };

        var navItems = new (string Icon, string Label, string Target)[]
        {
            ("", "Dashboard", "Dashboard"),
            ("", "Buttons", "Buttons"),
            ("", "Curves", "Curves"),
            ("", "Macros", "Macros"),
            ("", "Vibration", "Vibration"),
            ("", "PC Mode", "PcMode"),
            ("", "Device", "Device"),
            ("", "Mouse", "Mouse"),
            ("", "Setups", "Setups"),
            ("", "Tester", "Tester"),
            ("", "Diagnostics", "Diagnostics"),
            ("", "RGB lighting", "Lighting"),
            ("", "Screen artwork", "Screen")
        };

        foreach (var item in navItems)
        {
            bool isSel = (item.Target.Equals(activeItem, StringComparison.OrdinalIgnoreCase));
            string targetPage = item.Target;
            var navCard = SunriseKit.SunriseNavItem(item.Icon, item.Label, isSel, () =>
            {
                SunriseSelectPage(targetPage);
            });
            navSp.Children.Add(navCard);
        }

        // Back to Zone button at bottom
        var backZonePill = SunriseKit.SunriseButton("← Back to Zone", () => BackRequested?.Invoke(), isPrimary: false, width: 236, height: 40);
        backZonePill.Margin = new Thickness(0, 10, 0, 0);
        navSp.Children.Add(backZonePill);

        return navSp;
    }

    private UIElement CreateSunriseBottomBar()
    {
        var bottomBar = new Border
        {
            Width = 1600,
            Height = 44,
            Background = SunriseKit.InkBrush,
            Padding = new Thickness(24, 0, 24, 0)
        };

        var bGrid = new Grid();
        bGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        bGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        bGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Left Telemetry (Dynamic real device telemetry)
        var telemSp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        string conn = Device?.TransportText ?? (Device != null ? "CONNECTED" : "STANDBY");
        telemSp.Children.Add(new TextBlock { Text = $"● {conn.ToUpperInvariant()}", FontFamily = SunriseKit.Display, FontWeight = FontWeights.Bold, FontSize = 11, Foreground = SunriseKit.OrangeBrush, Margin = new Thickness(0, 0, 16, 0) });
        if (!string.IsNullOrEmpty(Device?.BatteryText))
        {
            telemSp.Children.Add(new TextBlock { Text = $"BATTERY: {Device.BatteryText.ToUpperInvariant()}", FontFamily = SunriseKit.Display, FontWeight = FontWeights.Bold, FontSize = 11, Foreground = SunriseKit.CreamBrush, Margin = new Thickness(0, 0, 16, 0) });
        }
        if (!string.IsNullOrEmpty(Device?.FirmwareText))
        {
            telemSp.Children.Add(new TextBlock { Text = $"FIRMWARE: {Device.FirmwareText.ToUpperInvariant()}", FontFamily = SunriseKit.Display, FontWeight = FontWeights.Bold, FontSize = 11, Foreground = SunriseKit.OliveBrush });
        }
        Grid.SetColumn(telemSp, 0);
        bGrid.Children.Add(telemSp);

        // Center Hint
        var hintTb = new TextBlock
        {
            Text = "Changes apply to the local controller profile draft.",
            FontFamily = SunriseKit.Body,
            FontSize = 11.5,
            Foreground = SunriseKit.OliveBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(hintTb, 1);
        bGrid.Children.Add(hintTb);

        // Right Keycaps
        var keysSp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        keysSp.Children.Add(new Border
        {
            Background = SunriseKit.OrangeBrush,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 2, 6, 2),
            Margin = new Thickness(0, 0, 4, 0),
            Child = new TextBlock { Text = "ENTER", FontFamily = SunriseKit.Display, FontWeight = FontWeights.Bold, FontSize = 10, Foreground = SunriseKit.CreamBrush }
        });
        keysSp.Children.Add(new TextBlock { Text = "Select   ", FontFamily = SunriseKit.Body, FontSize = 11, Foreground = SunriseKit.CreamBrush, VerticalAlignment = VerticalAlignment.Center });
        keysSp.Children.Add(new Border
        {
            Background = SunriseKit.OrangeBrush,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 2, 6, 2),
            Margin = new Thickness(0, 0, 4, 0),
            Child = new TextBlock { Text = "ESC", FontFamily = SunriseKit.Display, FontWeight = FontWeights.Bold, FontSize = 10, Foreground = SunriseKit.CreamBrush }
        });
        keysSp.Children.Add(new TextBlock { Text = "Back", FontFamily = SunriseKit.Body, FontSize = 11, Foreground = SunriseKit.CreamBrush, VerticalAlignment = VerticalAlignment.Center });
        Grid.SetColumn(keysSp, 2);
        bGrid.Children.Add(keysSp);

        bottomBar.Child = bGrid;
        return bottomBar;
    }

    private void SunriseShowDashboard()
    {
        if (sunrisePageHost == null) return;
        sunrisePageHost.Children.Clear();

        var board = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };
        board.Children.Add(SunriseKit.CreateHalftoneBackdrop());

        // Top bar & Sidebar
        board.Children.Add(CreateSunriseHeader("Studio"));
        var sidebar = CreateSunriseSidebar("Dashboard");
        Canvas.SetLeft(sidebar, 16);
        Canvas.SetTop(sidebar, 80);
        board.Children.Add(sidebar);

        // Content Area
        var content = new Canvas { Width = 1300, Height = 770 };
        Canvas.SetLeft(content, 272);
        Canvas.SetTop(content, 80);

        var titleSp = SunriseKit.SunriseTitle("CONTROLLER DASHBOARD", "Select any feature tile to inspect or reconfigure gamepad hardware.");
        Canvas.SetLeft(titleSp, 10);
        Canvas.SetTop(titleSp, 10);
        content.Children.Add(titleSp);

        // Left Pad Hero Sticker Card
        var padCard = new StackPanel { Width = 520, HorizontalAlignment = HorizontalAlignment.Center };
        var padArt = SunriseKit.ControllerArt(Model.Id, 480);
        padCard.Children.Add(padArt);

        var padInfo = new StackPanel { Margin = new Thickness(0, 14, 0, 0) };
        padInfo.Children.Add(new TextBlock { Text = Model.Name.ToUpperInvariant(), FontFamily = SunriseKit.Display, FontWeight = FontWeights.Black, FontSize = 20, Foreground = SunriseKit.InkBrush, HorizontalAlignment = HorizontalAlignment.Center });
        padInfo.Children.Add(new TextBlock { Text = "CALIBRATION READY · PROFILE ACTIVE", FontFamily = SunriseKit.Display, FontWeight = FontWeights.Bold, FontSize = 12, Foreground = SunriseKit.OrangeBrush, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 14) });

        var openBtn = SunriseKit.SunriseButton("Configure Buttons", () => ShowButtons(), isPrimary: true, width: 220, height: 42);
        openBtn.HorizontalAlignment = HorizontalAlignment.Center;
        padInfo.Children.Add(openBtn);
        padCard.Children.Add(padInfo);

        var heroCard = SunriseKit.CreateStickerCard(padCard, 560, 520, cornerRadius: 24);
        Canvas.SetLeft(heroCard, 10);
        Canvas.SetTop(heroCard, 90);
        content.Children.Add(heroCard);

        // Right 6 Feature Sticker Tiles
        var tiles = new (string Title, string Val, string ActionText, Action Click)[]
        {
            ("ANALOG CURVES", "LINEAR 1:1", "Adjust Curves", () => ShowCurves(true)),
            ("MACRO PADDLES", "4 SLOTS READY", "Edit Sequences", () => ShowMacros(true)),
            ("VIBRATION MOTOR", "100% INTENSITY", "Tune Rumble", () => ShowVibration(true)),
            ("PC MODE DRIVERS", "X360 VIRTUAL", "Configure Bus", () => ShowManagement("PcMode")),
            ("HARDWARE TESTER", "INPUT VERIFIED", "Open Visualizer", () => ShowManagement("Tester")),
            ("SAVED PROFILES", "LOCAL SETUP", "Manage Setups", () => ShowManagement("Setups"))
        };

        for (int i = 0; i < tiles.Length; i++)
        {
            var t = tiles[i];
            var tileSp = new StackPanel { Width = 280, VerticalAlignment = VerticalAlignment.Center };
            tileSp.Children.Add(new TextBlock { Text = t.Title, FontFamily = SunriseKit.Display, FontWeight = FontWeights.Bold, FontSize = 11, Foreground = SunriseKit.OliveBrush });
            tileSp.Children.Add(new TextBlock { Text = t.Val, FontFamily = SunriseKit.Display, FontWeight = FontWeights.Black, FontSize = 18, Foreground = SunriseKit.InkBrush, Margin = new Thickness(0, 4, 0, 4) });
            tileSp.Children.Add(new TextBlock { Text = "▸ " + t.ActionText.ToUpperInvariant(), FontFamily = SunriseKit.Display, FontWeight = FontWeights.Bold, FontSize = 11, Foreground = SunriseKit.OrangeBrush });

            var tileCard = SunriseKit.CreateStickerCard(tileSp, 310, 110, cornerRadius: 18);
            tileCard.Cursor = Cursors.Hand;
            tileCard.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                t.Click();
            };

            Canvas.SetLeft(tileCard, 600 + (i % 2) * 330);
            Canvas.SetTop(tileCard, 130 + (i / 2) * 130);
            content.Children.Add(tileCard);
        }

        board.Children.Add(content);

        // Bottom Bar
        var bBar = CreateSunriseBottomBar();
        Canvas.SetLeft(bBar, 0);
        Canvas.SetTop(bBar, 856);
        board.Children.Add(bBar);

        sunrisePageHost.Children.Add(board);
    }
}
