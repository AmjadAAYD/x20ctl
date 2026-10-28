using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Hybrid;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private Canvas? hybridPageHost;
    private string currentHybridPage = "Dashboard";

    public bool IsHybrid => ThemeCatalog.Current.Id == "hybrid";

    private void BuildHybridStudio()
    {
        if (!IsHybrid || hybridPageHost != null) return;

        RootLayout.Visibility = Visibility.Hidden;

        hybridPageHost = new Canvas
        {
            Width = 1600,
            Height = 900,
            ClipToBounds = true,
            Background = HybridKit.InkBrush
        };
        Canvas.SetLeft(hybridPageHost, 0);
        Canvas.SetTop(hybridPageHost, 0);

        if (StudioShell.Parent is Grid parentGrid)
        {
            if (!parentGrid.Children.Contains(hybridPageHost))
            {
                parentGrid.Children.Add(hybridPageHost);
            }
        }
        else
        {
            StudioShell.Children.Add(hybridPageHost);
        }

        HybridShow();
    }

    private bool hybridQueued;
    public void HybridLater()
    {
        if (!IsHybrid || hybridQueued) return;
        hybridQueued = true;
        Dispatcher.InvokeAsync(() =>
        {
            hybridQueued = false;
            HybridShow();
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    public void HybridSelectPage(string page)
    {
        if (!IsHybrid || hybridPageHost == null) return;
        currentHybridPage = page;

        switch (page.ToLowerInvariant())
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
                currentHybridPage = "Dashboard";
                HybridShowDashboard();
                break;
        }
    }

    private void HybridShow()
    {
        if (!IsHybrid || hybridPageHost == null) return;

        string activePage = CurrentPage;
        currentHybridPage = activePage;
        switch (activePage)
        {
            case "Buttons":
                HybridShowButtons();
                break;
            case "Curves":
                HybridShowCurves();
                break;
            case "Macros":
                HybridShowMacros();
                break;
            case "Vibration":
                HybridShowVibration();
                break;
            case "PcMode":
                HybridShowPcMode();
                break;
            case "Device":
                HybridShowDevice();
                break;
            case "Mouse":
                HybridShowMouse();
                break;
            case "Setups":
                HybridShowSetups();
                break;
            case "Tester":
                HybridShowTester();
                break;
            case "Diagnostics":
                HybridShowDiagnostics();
                break;
            case "Dashboard":
            default:
                currentHybridPage = "Dashboard";
                HybridShowDashboard();
                break;
        }
    }

    /// <summary>
    /// Docked Hybrid Dashboard: identical to concept board out/hybrid/dashboard.png.
    /// Features two halves split at x=800, ny=450 with cyan tab-and-socket seam,
    /// EasySMX D10 Farout Purple controller, Face button diamond cluster on charcoal right,
    /// and unified 9-button dock pill crowned with double ring cyan glow.
    /// </summary>
    private void HybridShowDashboard()
    {
        hybridPageHost!.Children.Clear();

        // 1. Two halves background (.hl + .hr + seam)
        var halves = HybridKit.Halves(800, 450);
        hybridPageHost.Children.Add(halves);

        // 2. Top Brand (X20CTL logo + wordmark)
        var brand = HybridKit.Brand(44, 30, dark: true);
        hybridPageHost.Children.Add(brand);

        // 3. Top Right Player status
        var topRight = HybridKit.TopRight(dark: false, activePlayer: Player);
        Canvas.SetRight(topRight, 0);
        Canvas.SetTop(topRight, 0);
        hybridPageHost.Children.Add(topRight);

        // 4. Controller floor shadow ellipse at (470, 560, w: 660, h: 60)
        var shadow = new Ellipse
        {
            Width = 660,
            Height = 60,
            Fill = new RadialGradientBrush
            {
                Center = new Point(0.5, 0.5),
                GradientOrigin = new Point(0.5, 0.5),
                RadiusX = 0.5,
                RadiusY = 0.5,
                GradientStops =
                {
                    new(Color.FromArgb(0x8c, 0x14, 0x0e, 0x1c), 0.0),
                    new(Colors.Transparent, 1.0)
                }
            }
        };
        Canvas.SetLeft(shadow, 470);
        Canvas.SetTop(shadow, 560);
        hybridPageHost.Children.Add(shadow);

        // 5. Featured Controller: EasySMX D10 Farout Purple (455, 112, w: 690)
        var padArt = HybridKit.ControllerArt("d10-purple", 690);
        if (padArt != null)
        {
            padArt.Effect = new DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 30,
                ShadowDepth = 24,
                Direction = 270,
                Opacity = 0.5
            };
            Canvas.SetLeft(padArt, 455);
            Canvas.SetTop(padArt, 112);
            hybridPageHost.Children.Add(padArt);
        }

        // 6. Left side specs block at (52, 268, w: 380)
        var leftSpecs = new StackPanel { Width = 380 };

        var playerTag = new TextBlock
        {
            Text = $"PLAYER {Player + 1}",
            FontFamily = HybridKit.Body,
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            Foreground = HybridKit.B("#5c4075")
        };
        leftSpecs.Children.Add(playerTag);

        var modelName = new TextBlock
        {
            Text = "EasySMX D10",
            FontFamily = HybridKit.Display,
            FontSize = 40,
            FontWeight = FontWeights.ExtraBold,
            Foreground = HybridKit.InkBrush,
            Margin = new Thickness(0, 6, 0, 0)
        };
        leftSpecs.Children.Add(modelName);

        var finishName = new TextBlock
        {
            Text = "Farout Purple",
            FontFamily = HybridKit.Display,
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            Foreground = HybridKit.B("#5c4075"),
            Margin = new Thickness(0, 2, 0, 0)
        };
        leftSpecs.Children.Add(finishName);

        // 2x2 status grid
        var statusGrid = new Grid { Margin = new Thickness(0, 26, 0, 0) };
        statusGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        statusGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        statusGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        statusGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var statPairs = new (string Title, string Value, int Col, int Row)[]
        {
            ("CURRENT SETUP", "Current working settings", 0, 0),
            ("CONNECTION", "Not detected", 1, 0),
            ("BATTERY", "Not reported", 0, 1),
            ("CHANGES", "No local changes", 1, 1)
        };

        foreach (var (stTitle, stVal, col, row) in statPairs)
        {
            var cell = new StackPanel { Margin = new Thickness(col == 0 ? 0 : 9, row == 0 ? 0 : 16, col == 0 ? 9 : 0, 0) };
            cell.Children.Add(new TextBlock
            {
                Text = stTitle,
                FontFamily = HybridKit.Body,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = HybridKit.B("#6d5786")
            });
            cell.Children.Add(new TextBlock
            {
                Text = stVal,
                FontFamily = HybridKit.Body,
                FontSize = 17,
                FontWeight = FontWeights.SemiBold,
                Foreground = HybridKit.InkBrush,
                Margin = new Thickness(0, 3, 0, 0)
            });
            Grid.SetColumn(cell, col);
            Grid.SetRow(cell, row);
            statusGrid.Children.Add(cell);
        }
        leftSpecs.Children.Add(statusGrid);

        Canvas.SetLeft(leftSpecs, 52);
        Canvas.SetTop(leftSpecs, 268);
        hybridPageHost.Children.Add(leftSpecs);

        // 7. Right side: Face button diamond cluster + action legend
        var rightCluster = HybridKit.FaceButtonCluster(
            cx: 1262,
            cy: 352,
            onSelect: () => ShowButtons(),
            onZone: () => BackRequested?.Invoke(),
            onConfigure: () => ShowCurves(true),
            onRescan: () => ToolsRequested?.Invoke()
        );
        hybridPageHost.Children.Add(rightCluster);

        // 8. Bottom unified dock pill (Buttons selected = index 0)
        var dock = HybridKit.DashboardDock(
            activeIndex: 0,
            onSelect: idx =>
            {
                var keys = new[] { "buttons", "curves", "macros", "vibration", "pcmode", "device", "mouse", "setups", "tools" };
                if (idx >= 0 && idx < keys.Length) HybridSelectPage(keys[idx]);
            }
        );
        hybridPageHost.Children.Add(dock);
    }
}
