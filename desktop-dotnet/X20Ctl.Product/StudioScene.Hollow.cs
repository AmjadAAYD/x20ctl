using System.Windows.Controls.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Hollow;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private bool IsHollow => ThemeCatalog.Current.Id == "hollow";

    private Grid? hollowLayer;
    private Canvas? hollowStage;
    private Canvas? hollowPageHost;
    private bool hollowQueued;
    private string currentHollowPage = "Dashboard";

    private void BuildHollowStudio()
    {
        if (!IsHollow || hollowLayer != null) return;

        RootLayout.Visibility = Visibility.Collapsed;
        StudioBackdrop.Visibility = Visibility.Collapsed;

        hollowStage = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };

        hollowPageHost = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };
        Canvas.SetLeft(hollowPageHost, 0);
        Canvas.SetTop(hollowPageHost, 0);
        hollowStage.Children.Add(hollowPageHost);

        hollowLayer = new Grid
        {
            Background = HollowKit.VoidBrush,
            Children = { new Viewbox { Stretch = Stretch.Uniform, Child = hollowStage } }
        };
        Panel.SetZIndex(hollowLayer, 30);
        StudioShell.Children.Add(hollowLayer);

        HollowShow();
    }

    private void HollowLater()
    {
        if (!IsHollow || hollowQueued) return;
        hollowQueued = true;
        Dispatcher.InvokeAsync(() =>
        {
            hollowQueued = false;
            HollowShow();
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    public void HollowSelectPage(string target)
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
                currentHollowPage = "Dashboard";
                HollowShowDashboard();
                break;
        }
    }

    private void HollowShow()
    {
        if (!IsHollow || hollowPageHost == null) return;

        string activePage = CurrentPage;
        currentHollowPage = activePage;
        switch (activePage)
        {
            case "Buttons":
                HollowShowButtons();
                break;
            case "Curves":
                HollowShowCurves();
                break;
            case "Macros":
                HollowShowMacros();
                break;
            case "Vibration":
                HollowShowVibration();
                break;
            case "PcMode":
                HollowShowPcMode();
                break;
            case "Device":
                HollowShowDevice();
                break;
            case "Mouse":
                HollowShowMouse();
                break;
            case "Setups":
                HollowShowSetups();
                break;
            case "Tester":
                HollowShowTester();
                break;
            case "Diagnostics":
                HollowShowDiagnostics();
                break;
            case "Lighting":
            case "RGB":
                HollowShowLighting();
                break;
            case "Screen":
                HollowShowScreen();
                break;
            case "Dashboard":
            default:
                currentHollowPage = "Dashboard";
                HollowShowDashboard();
                break;
        }
    }

    /// <summary>Standard Hollow sidebar navigation component.</summary>
    private FrameworkElement CreateHollowSidebar(string selectedItem)
    {
        var navPanel = new StackPanel { Width = 210 };

        navPanel.Children.Add(HollowKit.HollowNavRow("Dashboard", "theme:nav:dashboard", selectedItem == "Dashboard", () => HollowSelectPage("Dashboard"), ""));
        navPanel.Children.Add(HollowKit.HollowNavRow("Buttons", "theme:nav:buttons", selectedItem == "Buttons", () => HollowSelectPage("Buttons"), ""));
        navPanel.Children.Add(HollowKit.HollowNavRow("Curves", "theme:nav:curves", selectedItem == "Curves", () => HollowSelectPage("Curves"), ""));
        navPanel.Children.Add(HollowKit.HollowNavRow("Macros", "theme:nav:macros", selectedItem == "Macros", () => HollowSelectPage("Macros"), ""));
        navPanel.Children.Add(HollowKit.HollowNavRow("Vibration", "theme:nav:vibration", selectedItem == "Vibration", () => HollowSelectPage("Vibration"), ""));
        navPanel.Children.Add(HollowKit.HollowNavRow("PC mode", "theme:nav:pcmode", selectedItem == "PcMode", () => HollowSelectPage("PcMode"), ""));
        navPanel.Children.Add(HollowKit.HollowNavRow("Device", "theme:nav:device", selectedItem == "Device", () => HollowSelectPage("Device"), ""));
        navPanel.Children.Add(HollowKit.HollowNavRow("Mouse", "theme:nav:mouse", selectedItem == "Mouse", () => HollowSelectPage("Mouse"), ""));
        navPanel.Children.Add(HollowKit.HollowNavRow("Setups", "theme:nav:setups", selectedItem == "Setups", () => HollowSelectPage("Setups"), ""));

        navPanel.Children.Add(HollowKit.DiamondDivider(190, 16));

        navPanel.Children.Add(HollowKit.HollowNavRow("Tester", "theme:nav:tester", selectedItem == "Tester", () => HollowSelectPage("Tester"), ""));
        navPanel.Children.Add(HollowKit.HollowNavRow("Diagnostics", "theme:nav:diagnostics", selectedItem == "Diagnostics", () => HollowSelectPage("Diagnostics"), ""));

        navPanel.Children.Add(HollowKit.DiamondDivider(190, 16));

        navPanel.Children.Add(HollowKit.HollowNavRow("Back to Zone", "theme:nav:zone", false, () => BackRequested?.Invoke(), ""));

        var framed = HollowKit.FiligreeCard(navPanel, padding: 12, cornerSize: 12);
        framed.Width = 234;
        framed.Height = 770;
        return framed;
    }

    /// <summary>Standard Hollow top header bar: logo, centered tabs, player pill.</summary>
    private FrameworkElement CreateHollowHeader(string activeTab = "Studio")
    {
        var topBar = new Canvas { Width = 1600, Height = 64 };

        // Brand logo at left
        var brandSp = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        var leafEmblem = new Path
        {
            Data = HollowKit.CreateDiamond(14),
            Fill = HollowKit.IvoryTextBrush,
            Stroke = HollowKit.IvoryBorderBrush,
            StrokeThickness = 0.5,
            Margin = new Thickness(0, 0, 10, 0)
        };
        var logoText = new TextBlock
        {
            Text = "X20CTL",
            FontFamily = HollowKit.Display,
            FontSize = 18,
            FontWeight = FontWeights.Normal,
            Foreground = HollowKit.IvoryTextBrush,
        };
        brandSp.Children.Add(leafEmblem);
        brandSp.Children.Add(logoText);
        Canvas.SetLeft(brandSp, 34);
        Canvas.SetTop(brandSp, 20);
        topBar.Children.Add(brandSp);

        // Centered Tabs
        var centerTabs = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        string[] tabs = ["Controller Zone", "Studio", "Agent XCTL", "Tools"];
        foreach (var tab in tabs)
        {
            bool isSel = (tab == activeTab);
            var tabBorder = new Border
            {
                Padding = new Thickness(16, 6, 16, 6),
                Cursor = System.Windows.Input.Cursors.Hand,
                Background = Brushes.Transparent
            };
            var sp = new StackPanel { Orientation = Orientation.Vertical };
            sp.Children.Add(new TextBlock
            {
                Text = tab,
                FontFamily = HollowKit.Display,
                FontSize = 13,
                Foreground = isSel ? HollowKit.IvoryTextBrush : HollowKit.MutedTextBrush
            });
            if (isSel)
            {
                var underLine = new Canvas { Height = 4, Margin = new Thickness(0, 3, 0, 0) };
                var l = new Line { X1 = 0, X2 = 45, Y1 = 2, Y2 = 2, Stroke = HollowKit.IvoryBorderBrush, StrokeThickness = 1 };
                var d = new Path { Data = HollowKit.CreateDiamond(5), Fill = HollowKit.IvoryTextBrush, Stroke = HollowKit.IvoryBorderBrush };
                Canvas.SetLeft(d, 22.5);
                Canvas.SetTop(d, 2);
                underLine.Children.Add(l);
                underLine.Children.Add(d);
                sp.Children.Add(underLine);
            }
            tabBorder.Child = sp;
            tabBorder.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                if (tab == "Controller Zone") BackRequested?.Invoke();
                else if (tab == "Tools") ToolsRequested?.Invoke();
                else if (tab == "Agent XCTL") HollowSelectPage("Dashboard");
            };
            centerTabs.Children.Add(tabBorder);
        }
        Canvas.SetLeft(centerTabs, 560);
        Canvas.SetTop(centerTabs, 16);
        topBar.Children.Add(centerTabs);

        // Right player status pill
        var pill = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xdd, 0x09, 0x12, 0x1c)),
            BorderBrush = new SolidColorBrush(HollowKit.A(HollowKit.SilverBorder, 0.5)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(14, 5, 14, 5)
        };
        var pillText = new TextBlock
        {
            Text = $"Player {Player + 1}  ·  {Model.Name}  ·  Local editor",
            FontFamily = HollowKit.Display,
            FontSize = 11.5,
            Foreground = HollowKit.SoftTextBrush
        };
        pill.Child = pillText;
        Canvas.SetLeft(pill, 1260);
        Canvas.SetTop(pill, 18);
        topBar.Children.Add(pill);

        return topBar;
    }

    private void HollowShowDashboard()
    {
        if (hollowPageHost == null) return;
        hollowPageHost.Children.Clear();

        var board = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };
        board.Children.Add(HollowKit.CreateCavernBackdrop());

        // Header & Sidebar
        board.Children.Add(CreateHollowHeader("Studio"));
        var sidebar = CreateHollowSidebar("Dashboard");
        Canvas.SetLeft(sidebar, 28);
        Canvas.SetTop(sidebar, 80);
        board.Children.Add(sidebar);

        // Center Content Area (x=286, y=80, w=1280, h=770)
        var centerGrid = new Canvas { Width = 1280, Height = 770 };
        Canvas.SetLeft(centerGrid, 286);
        Canvas.SetTop(centerGrid, 80);

        // Title Block
        var titleSp = HollowKit.HollowHeader("STUDIO DASHBOARD", "Select a feature to configure your controller profile.");
        Canvas.SetLeft(titleSp, 16);
        Canvas.SetTop(titleSp, 10);
        centerGrid.Children.Add(titleSp);

        // Tiles Grid: 6 plaques
        var tileGrid = new UniformGrid { Columns = 3, Rows = 2, Width = 1240, Height = 580 };
        tileGrid.Children.Add(CreateHollowDashboardPlaque("BUTTONS", "REMAP FACE & TRIGGERS", "", () => HollowSelectPage("Buttons"), "theme:dash:buttons"));
        tileGrid.Children.Add(CreateHollowDashboardPlaque("CURVES", "STICK RESPONSE & DEADZONES", "", () => HollowSelectPage("Curves"), "theme:dash:curves"));
        tileGrid.Children.Add(CreateHollowDashboardPlaque("MACROS", "AUTOMATE SEQUENCES & COMBOS", "", () => HollowSelectPage("Macros"), "theme:dash:macros"));
        tileGrid.Children.Add(CreateHollowDashboardPlaque("VIBRATION", "GRIP FORCE & MOTOR TESTING", "", () => HollowSelectPage("Vibration"), "theme:dash:vibration"));
        tileGrid.Children.Add(CreateHollowDashboardPlaque("PC MODE", "INPUT EMULATION & DRIVERS", "", () => HollowSelectPage("PcMode"), "theme:dash:pcmode"));
        tileGrid.Children.Add(CreateHollowDashboardPlaque("DEVICE", "HARDWARE DETAILS & RESET", "", () => HollowSelectPage("Device"), "theme:dash:device"));

        Canvas.SetLeft(tileGrid, 16);
        Canvas.SetTop(tileGrid, 90);
        centerGrid.Children.Add(tileGrid);

        board.Children.Add(centerGrid);

        // Dock
        var dock = HollowKit.HollowDock("Dashboard");
        Canvas.SetLeft(dock, 0);
        Canvas.SetTop(dock, 864);
        board.Children.Add(dock);

        hollowPageHost.Children.Add(board);
    }

    private FrameworkElement CreateHollowDashboardPlaque(string title, string subtitle, string icon, Action onClick, string tag)
    {
        var sp = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var iconBlock = new TextBlock
        {
            Text = icon,
            FontFamily = new FontFamily("Segoe Fluent Icons"),
            FontSize = 32,
            Foreground = HollowKit.IvoryTextBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 14)
        };
        var titleBlock = new TextBlock
        {
            Text = title,
            FontFamily = HollowKit.Display,
            FontSize = 16,
            FontWeight = FontWeights.Normal,
            Foreground = HollowKit.IvoryTextBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        var subBlock = new TextBlock
        {
            Text = subtitle,
            FontFamily = HollowKit.Body,
            FontSize = 11.5,
            Foreground = HollowKit.MutedTextBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 6, 0, 14)
        };

        sp.Children.Add(iconBlock);
        sp.Children.Add(titleBlock);
        sp.Children.Add(subBlock);
        sp.Children.Add(HollowKit.DiamondDivider(160, 10));

        var card = HollowKit.FiligreeCard(sp, padding: 18, cornerSize: 14);
        card.Margin = new Thickness(8);
        card.Cursor = System.Windows.Input.Cursors.Hand;
        card.Tag = tag;

        card.MouseEnter += (_, _) =>
        {
            card.Background = HollowKit.RaisedBrush;
            card.BorderBrush = HollowKit.IvoryBorderBrush;
        };
        card.MouseLeave += (_, _) =>
        {
            card.Background = HollowKit.NightBrush;
            card.BorderBrush = new SolidColorBrush(HollowKit.A(HollowKit.IvoryBorder, 0.45));
        };
        card.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            onClick();
        };

        return card;
    }
}
