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
using X20Ctl.Product.Nocturne;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private bool IsNocturne => ThemeCatalog.Current.Id == "nocturne";

    private Grid? nocturneLayer;
    private Canvas? nocturneStage;
    private Canvas? nocturnePageHost;
    private bool nocturneQueued;
    private string currentNocturnePage = "Dashboard";

    private void BuildNocturneStudio()
    {
        if (!IsNocturne || nocturneLayer != null) return;

        RootLayout.Visibility = Visibility.Collapsed;
        StudioBackdrop.Visibility = Visibility.Collapsed;

        nocturneStage = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };

        nocturnePageHost = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };
        Canvas.SetLeft(nocturnePageHost, 0);
        Canvas.SetTop(nocturnePageHost, 0);
        nocturneStage.Children.Add(nocturnePageHost);

        nocturneLayer = new Grid
        {
            Background = NocturneKit.VoidBrush,
            Children = { new Viewbox { Stretch = Stretch.Uniform, Child = nocturneStage } }
        };
        Panel.SetZIndex(nocturneLayer, 30);
        StudioShell.Children.Add(nocturneLayer);

        NocturneShow();
    }

    private void NocturneLater()
    {
        if (!IsNocturne || nocturneQueued) return;
        nocturneQueued = true;
        Dispatcher.InvokeAsync(() =>
        {
            nocturneQueued = false;
            NocturneShow();
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    public void NocturneSelectPage(string target)
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
                currentNocturnePage = "Dashboard";
                NocturneShowDashboard();
                break;
        }
    }

    private void NocturneShow()
    {
        if (!IsNocturne || nocturnePageHost == null) return;

        string activePage = CurrentPage;
        currentNocturnePage = activePage;
        switch (activePage)
        {
            case "Buttons":
                NocturneShowButtons();
                break;
            case "Curves":
                NocturneShowCurves();
                break;
            case "Macros":
                NocturneShowMacros();
                break;
            case "Vibration":
                NocturneShowVibration();
                break;
            case "PcMode":
                NocturneShowPcMode();
                break;
            case "Device":
                NocturneShowDevice();
                break;
            case "Mouse":
                NocturneShowMouse();
                break;
            case "Setups":
                NocturneShowSetups();
                break;
            case "Tester":
                NocturneShowTester();
                break;
            case "Diagnostics":
                NocturneShowDiagnostics();
                break;
            case "Lighting":
                NocturneShowLighting();
                break;
            case "Screen":
                NocturneShowScreen();
                break;
            case "Dashboard":
            default:
                NocturneShowDashboard();
                break;
        }
    }

    private UIElement CreateNocturneHeader(string activeTab = "Studio")
    {
        var topBar = new Canvas
        {
            Width = 1600,
            Height = 54,
            Background = new SolidColorBrush(Color.FromArgb(0xf5, 0x01, 0x12, 0x3e)),
            ClipToBounds = true
        };

        // Brand logo
        var brandSp = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            RenderTransform = NocturneKit.CreateSlant(-12),
            RenderTransformOrigin = new Point(0, 0.5)
        };
        brandSp.Children.Add(new TextBlock
        {
            Text = "X20CTL",
            FontFamily = NocturneKit.Display,
            FontWeight = FontWeights.Black,
            FontStyle = FontStyles.Italic,
            FontSize = 20,
            Foreground = NocturneKit.WhiteBrush,
            VerticalAlignment = VerticalAlignment.Center
        });
        Canvas.SetLeft(brandSp, 36);
        Canvas.SetTop(brandSp, 14);
        topBar.Children.Add(brandSp);

        // Center Tabs: Controller Zone, Studio, Agent XCTL, Tools
        var centerTabs = new StackPanel { Orientation = Orientation.Horizontal };
        var tabs = new[] { "Controller Zone", "Studio", "Agent XCTL", "Tools" };

        foreach (var tab in tabs)
        {
            bool isCurrent = (tab == activeTab);
            var tabBorder = new Border
            {
                Padding = new Thickness(16, 6, 16, 6),
                Margin = new Thickness(0, 0, 8, 0),
                Background = isCurrent ? NocturneKit.ActiveBlueBrush : Brushes.Transparent,
                BorderBrush = isCurrent ? NocturneKit.LightBlueBrush : Brushes.Transparent,
                BorderThickness = new Thickness(isCurrent ? 1 : 0),
                Cursor = Cursors.Hand,
                RenderTransform = NocturneKit.CreateSlant(-12),
                RenderTransformOrigin = new Point(0.5, 0.5),
                Tag = $"theme:tab:{tab.ToLowerInvariant().Replace(' ', '-')}"
            };

            var tb = new TextBlock
            {
                Text = tab.ToUpperInvariant(),
                FontFamily = NocturneKit.Display,
                FontWeight = FontWeights.Bold,
                FontStyle = FontStyles.Italic,
                FontSize = 12,
                Foreground = NocturneKit.WhiteBrush,
                RenderTransform = NocturneKit.CreateSlant(12),
                RenderTransformOrigin = new Point(0.5, 0.5)
            };
            tabBorder.Child = tb;

            tabBorder.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                if (tab == "Controller Zone") BackRequested?.Invoke();
                else if (tab == "Tools") ToolsRequested?.Invoke();
                else if (tab == "Agent XCTL") NocturneSelectPage("Dashboard");
                else NocturneSelectPage("Buttons");
            };
            centerTabs.Children.Add(tabBorder);
        }
        Canvas.SetLeft(centerTabs, 380);
        Canvas.SetTop(centerTabs, 10);
        topBar.Children.Add(centerTabs);

        // Right Info: Player 1 / EasySMX X20 · Local editor
        var rightSp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        rightSp.Children.Add(new TextBlock
        {
            Text = $"PLAYER {Player + 1} / {Model.Name.ToUpperInvariant()}  ·  LOCAL DRAFT",
            FontFamily = NocturneKit.Body,
            FontStyle = FontStyles.Italic,
            FontSize = 12,
            Foreground = NocturneKit.CoolGreyBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 24, 0)
        });
        Canvas.SetRight(rightSp, 36);
        Canvas.SetTop(rightSp, 16);
        topBar.Children.Add(rightSp);

        return topBar;
    }

    private UIElement CreateNocturneSidebar(string activePage = "Buttons")
    {
        var navCanvas = new Canvas { Width = 260, Height = 780 };

        var navSp = new StackPanel { Width = 250 };
        var navItems = new (string Name, string Glyph, string Tag)[]
        {
            ("Dashboard", "", "theme:nav:dashboard"),
            ("Buttons", "", "theme:nav:buttons"),
            ("Curves", "", "theme:nav:curves"),
            ("Macros", "", "theme:nav:macros"),
            ("Vibration", "", "theme:nav:vibration"),
            ("PC Mode", "", "theme:nav:pcmode"),
            ("Device", "", "theme:nav:device"),
            ("Mouse", "", "theme:nav:mouse"),
            ("Setups", "", "theme:nav:setups"),
            ("Tester", "", "theme:nav:tester"),
            ("Diagnostics", "", "theme:nav:diagnostics"),
            ("Lighting", "", "theme:nav:lighting"),
            ("Screen", "", "theme:nav:screen")
        };

        foreach (var item in navItems)
        {
            bool isCurrent = string.Equals(item.Name, activePage, StringComparison.OrdinalIgnoreCase);
            var navBtn = NocturneKit.NocturneNavItem(item.Name, item.Glyph, isCurrent, () =>
            {
                NocturneSelectPage(item.Name);
            }, tag: item.Tag);
            navSp.Children.Add(navBtn);
        }

        // Divider
        var div = new Rectangle
        {
            Height = 1,
            Fill = new SolidColorBrush(Color.FromArgb(0x40, 0x5c, 0x80, 0xc8)),
            Margin = new Thickness(14, 10, 14, 10)
        };
        navSp.Children.Add(div);

        // Back to Zone button
        var backBtn = NocturneKit.NocturneNavItem("Back to Zone", "", false, () =>
        {
            BackRequested?.Invoke();
        }, tag: "theme:nav:back");
        navSp.Children.Add(backBtn);

        Canvas.SetLeft(navSp, 0);
        Canvas.SetTop(navSp, 0);
        navCanvas.Children.Add(navSp);

        return navCanvas;
    }

    private UIElement CreateNocturneBottomBar()
    {
        var bar = new Canvas
        {
            Width = 1600,
            Height = 44,
            Background = new SolidColorBrush(Color.FromArgb(0xfa, 0x01, 0x12, 0x3e)),
            ClipToBounds = true
        };

        // Left telemetry chips
        var leftSp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        string connStr = Device?.ConnectionText ?? (gameplayConnected == true ? "Gameplay input detected" : "Not detected");
        string battStr = Device?.BatteryText ?? (currentBatteryLevel != null ? $"{currentBatteryLevel}%" : "Not reported");
        string fwStr = Device?.FirmwareText ?? (currentFirmware ?? "Not reported");

        leftSp.Children.Add(new TextBlock
        {
            Text = $"CONNECTION: {connStr.ToUpperInvariant()}  ·  BATTERY: {battStr.ToUpperInvariant()}  ·  FIRMWARE: {fwStr.ToUpperInvariant()}",
            FontFamily = NocturneKit.Body,
            FontStyle = FontStyles.Italic,
            FontSize = 11.5,
            Foreground = NocturneKit.CoolGreyBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(36, 12, 0, 0)
        });
        bar.Children.Add(leftSp);

        // Right keycaps hints
        var rightSp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        rightSp.Children.Add(Keycap("Enter", "Select"));
        rightSp.Children.Add(Keycap("Esc", "Back"));
        rightSp.Children.Add(Keycap("F1", "Help"));
        Canvas.SetRight(rightSp, 36);
        Canvas.SetTop(rightSp, 8);
        bar.Children.Add(rightSp);

        return bar;

        FrameworkElement Keycap(string key, string act)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            var kb = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xdd, 0x01, 0x1d, 0x69)),
                BorderBrush = NocturneKit.LightBlueBrush,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6, 1, 6, 1),
                RenderTransform = NocturneKit.CreateSlant(-12),
                RenderTransformOrigin = new Point(0.5, 0.5)
            };
            kb.Child = new TextBlock
            {
                Text = key.ToUpperInvariant(),
                FontFamily = NocturneKit.Display,
                FontWeight = FontWeights.Bold,
                FontSize = 10,
                Foreground = NocturneKit.WhiteBrush,
                RenderTransform = NocturneKit.CreateSlant(12),
                RenderTransformOrigin = new Point(0.5, 0.5)
            };
            var ab = new TextBlock
            {
                Text = act.ToUpperInvariant(),
                FontFamily = NocturneKit.Body,
                FontSize = 11,
                Foreground = NocturneKit.CoolGreyBrush,
                Margin = new Thickness(6, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            sp.Children.Add(kb);
            sp.Children.Add(ab);
            return sp;
        }
    }

    private void NocturneShowDashboard()
    {
        if (nocturnePageHost == null) return;
        nocturnePageHost.Children.Clear();

        var board = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };
        board.Children.Add(NocturneKit.CreateCityBackdrop());

        // Header & Sidebar
        board.Children.Add(CreateNocturneHeader("Studio"));
        var sidebar = CreateNocturneSidebar("Dashboard");
        Canvas.SetLeft(sidebar, 24);
        Canvas.SetTop(sidebar, 74);
        board.Children.Add(sidebar);

        // Content Area at x=310, y=74
        var content = new Canvas { Width = 1260, Height = 780 };
        Canvas.SetLeft(content, 310);
        Canvas.SetTop(content, 74);

        // Big two-line title
        var title = NocturneKit.NocturneTitle("NOCTURNE", "DASHBOARD", "Overview of active gamepad calibrations and hardware modules.");
        Canvas.SetLeft(title, 10);
        Canvas.SetTop(title, 10);
        content.Children.Add(title);

        // Center controller visual on cobalt field
        var padArt = NocturneKit.ControllerArt(Model.Id, 480);
        Canvas.SetLeft(padArt, 30);
        Canvas.SetTop(padArt, 180);
        content.Children.Add(padArt);

        // 4 Skewed Quick Tiles
        var tiles = new (string Name, string Val, string Action, Action Click)[]
        {
            ("BUTTONS MAPPED", "17 / 17", "Configure Buttons", () => NocturneSelectPage("Buttons")),
            ("RESPONSE CURVES", "LINEAR 1:1", "Tune Curves", () => NocturneSelectPage("Curves")),
            ("MACRO SEQUENCES", "4 SLOTS", "Manage Macros", () => NocturneSelectPage("Macros")),
            ("MOTOR VIBRATION", "75% ACTIVE", "Calibrate Haptics", () => NocturneSelectPage("Vibration"))
        };

        double tileW = 280;
        double tileH = 110;
        for (int i = 0; i < tiles.Length; i++)
        {
            var t = tiles[i];
            var tileBorder = new Border
            {
                Width = tileW,
                Height = tileH,
                Background = new SolidColorBrush(Color.FromArgb(0xdd, 0x01, 0x1d, 0x69)),
                BorderBrush = NocturneKit.LightBlueBrush,
                BorderThickness = new Thickness(1.5),
                Cursor = Cursors.Hand,
                RenderTransform = NocturneKit.CreateSlant(-12),
                RenderTransformOrigin = new Point(0.5, 0.5),
                Padding = new Thickness(14),
                Effect = new DropShadowEffect { Color = NocturneKit.Cobalt, BlurRadius = 0, ShadowDepth = 6, Direction = 315, Opacity = 0.7 }
            };

            var tStack = new StackPanel
            {
                RenderTransform = NocturneKit.CreateSlant(12),
                RenderTransformOrigin = new Point(0.5, 0.5)
            };
            tStack.Children.Add(new TextBlock { Text = t.Name, FontFamily = NocturneKit.Display, FontWeight = FontWeights.Bold, FontSize = 11.5, Foreground = NocturneKit.CoolGreyBrush });
            tStack.Children.Add(new TextBlock { Text = t.Val, FontFamily = NocturneKit.Display, FontWeight = FontWeights.Black, FontStyle = FontStyles.Italic, FontSize = 22, Foreground = NocturneKit.WhiteBrush, Margin = new Thickness(0, 4, 0, 4) });
            tStack.Children.Add(new TextBlock { Text = "▸ " + t.Action.ToUpperInvariant(), FontFamily = NocturneKit.Display, FontWeight = FontWeights.Bold, FontSize = 11, Foreground = NocturneKit.CobaltBrush });
            tileBorder.Child = tStack;

            tileBorder.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                t.Click();
            };

            Canvas.SetLeft(tileBorder, 600 + (i % 2) * 310);
            Canvas.SetTop(tileBorder, 180 + (i / 2) * 130);
            content.Children.Add(tileBorder);
        }

        board.Children.Add(content);

        // Bottom Bar
        var bottomBar = CreateNocturneBottomBar();
        Canvas.SetLeft(bottomBar, 0);
        Canvas.SetTop(bottomBar, 856);
        board.Children.Add(bottomBar);

        nocturnePageHost.Children.Add(board);
    }
}
