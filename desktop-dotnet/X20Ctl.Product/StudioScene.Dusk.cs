using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Dusk;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private Canvas? duskPageHost;
    private string currentDuskPage = "Dashboard";

    public bool IsDusk => ThemeCatalog.Current.Id == "dusk";

    private void BuildDuskStudio()
    {
        if (!IsDusk || duskPageHost != null) return;

        RootLayout.Visibility = Visibility.Hidden;

        duskPageHost = new Canvas
        {
            Width = 1600,
            Height = 900,
            ClipToBounds = true,
            Background = DuskKit.SlateBgBrush
        };
        Canvas.SetLeft(duskPageHost, 0);
        Canvas.SetTop(duskPageHost, 0);

        if (StudioShell.Parent is Grid parentGrid)
        {
            if (!parentGrid.Children.Contains(duskPageHost))
            {
                parentGrid.Children.Add(duskPageHost);
            }
        }
        else
        {
            StudioShell.Children.Add(duskPageHost);
        }

        DuskShow();
    }

    private bool duskQueued;
    public void DuskLater()
    {
        if (!IsDusk || duskQueued) return;
        duskQueued = true;
        Dispatcher.InvokeAsync(() =>
        {
            duskQueued = false;
            DuskShow();
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    public void DuskSelectPage(string page)
    {
        if (!IsDusk || duskPageHost == null) return;
        currentDuskPage = page;

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
                currentDuskPage = "Dashboard";
                DuskShowDashboard();
                break;
        }
    }

    private void DuskShow()
    {
        if (!IsDusk || duskPageHost == null) return;

        string activePage = CurrentPage;
        currentDuskPage = activePage;
        switch (activePage)
        {
            case "Buttons":
                DuskShowButtons();
                break;
            case "Curves":
                DuskShowCurves();
                break;
            case "Macros":
                DuskShowMacros();
                break;
            case "Vibration":
                DuskShowVibration();
                break;
            case "PcMode":
                DuskShowPcMode();
                break;
            case "Device":
                DuskShowDevice();
                break;
            case "Mouse":
                DuskShowMouse();
                break;
            case "Setups":
                DuskShowSetups();
                break;
            case "Tester":
                DuskShowTester();
                break;
            case "Diagnostics":
                DuskShowDiagnostics();
                break;
            case "Dashboard":
            default:
                currentDuskPage = "Dashboard";
                DuskShowDashboard();
                break;
        }
    }

    /// <summary>
    /// Steam Deck Dark Home Dashboard: strictly implements t_dusk.py:dashboard().
    /// Dim key art backdrop, status row with square avatar & green online dot,
    /// wide glowing hero capsule (790x360), 3 portrait capsules running off the right edge,
    /// title & local metadata, centered pill tabs, peeking shelves, and Steam Deck button bar.
    /// </summary>
    private void DuskShowDashboard()
    {
        duskPageHost!.Children.Clear();

        string padKey = Model?.Id?.ToLowerInvariant() ?? "x05pro";
        if (padKey.Contains("05")) padKey = "x05pro";
        else if (padKey.Contains("10")) padKey = "x10";
        else if (padKey.Contains("20")) padKey = "x20pro";

        // 1. Dim key art backdrop
        var backdrop = DuskKit.Backdrop(padKey, 0.34);
        duskPageHost.Children.Add(backdrop);

        // 2. Top status row
        var top = DuskKit.TopBar($"Player {Player + 1}", battery: currentBatteryLevel);
        duskPageHost.Children.Add(top);

        // Stage floor pedestal glow under hero capsule
        var pedestal = DuskKit.PedestalGlow(640, 140, DuskKit.CyanNeon);
        Canvas.SetLeft(pedestal, 100);
        Canvas.SetTop(pedestal, 460);
        duskPageHost.Children.Add(pedestal);

        // 3. Wide Hero Capsule (790x360) at (32, 226)
        string rawHero = !string.IsNullOrEmpty(Model?.Name) ? Model.Name.Trim() : "X05 PRO";
        string shortHero = rawHero.StartsWith("EasySMX ", StringComparison.OrdinalIgnoreCase)
            ? rawHero.Substring("EasySMX ".Length).Trim()
            : (rawHero.StartsWith("EASYSMX ", StringComparison.OrdinalIgnoreCase) ? rawHero.Substring("EASYSMX ".Length).Trim() : rawHero);
        if (string.IsNullOrEmpty(shortHero)) shortHero = "X05 PRO";

        var hero = DuskKit.HeroCapsule(padKey, shortHero.ToUpperInvariant(), 790, 360, active: true, onClick: () => DuskSelectPage("buttons"));
        Canvas.SetLeft(hero, 32);
        Canvas.SetTop(hero, 226);
        duskPageHost.Children.Add(hero);

        // 4. Portrait capsules running off right edge
        var carouselPads = new (string Key, string Short, Color Top, Color Bottom)[]
        {
            ("x10", "X10", DuskKit.C("#2a3545"), DuskKit.C("#0d1218")),
            ("x20pro", "X20 PRO", DuskKit.C("#382b48"), DuskKit.C("#0e0d16")),
            ("dune", "DUNE", DuskKit.C("#3c362a"), DuskKit.C("#12110d"))
        };

        for (int i = 0; i < carouselPads.Length; i++)
        {
            var p = carouselPads[i];
            var cap = DuskKit.PortraitCapsule(p.Key, p.Short, p.Top, p.Bottom, onClick: () => DuskSelectPage("buttons"));
            Canvas.SetLeft(cap, 842 + i * 266);
            Canvas.SetTop(cap, 226);
            duskPageHost.Children.Add(cap);
        }

        // 5. Chevrons at left and right
        var leftChev = DuskKit.Chevron(false, () => { });
        Canvas.SetLeft(leftChev, 8);
        Canvas.SetTop(leftChev, 380);
        duskPageHost.Children.Add(leftChev);

        var rightChev = DuskKit.Chevron(true, () => { });
        Canvas.SetLeft(rightChev, 1532);
        Canvas.SetTop(rightChev, 380);
        duskPageHost.Children.Add(rightChev);

        // 6. Controller Title & Meta at (300, 610)
        var titleStack = new StackPanel { Orientation = Orientation.Vertical };
        titleStack.Children.Add(new TextBlock
        {
            Text = $"EASYSMX · {shortHero.ToUpperInvariant()}",
            FontFamily = DuskKit.Display,
            FontSize = 32,
            FontWeight = FontWeights.Bold,
            Foreground = DuskKit.WhiteBrush
        });

        var hudPill = new Border
        {
            Padding = new Thickness(12, 4, 12, 4),
            CornerRadius = new CornerRadius(12),
            Background = DuskKit.B("rgba(18,25,34,0.85)"),
            BorderBrush = DuskKit.B("rgba(26,159,255,0.4)"),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 6, 0, 4),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = new TextBlock
            {
                Text = "X20CTL · LOCAL SETTINGS · NOTHING SENT TO THE CONTROLLER",
                FontFamily = DuskKit.Display,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = DuskKit.CyanNeonBrush
            }
        };

        var metaStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
        metaStack.Children.Add(new TextBlock
        {
            Text = "\uE777",
            FontFamily = DuskKit.Icons,
            FontSize = 16,
            Foreground = DuskKit.MuteBrush,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        metaStack.Children.Add(new TextBlock
        {
            Text = "LOCAL WORKING SETTINGS · HARDWARE UNCHANGED",
            FontFamily = DuskKit.Display,
            FontSize = 17,
            FontWeight = FontWeights.Bold,
            Foreground = DuskKit.MuteBrush,
            VerticalAlignment = VerticalAlignment.Center
        });
        titleStack.Children.Add(metaStack);

        Canvas.SetLeft(titleStack, 300);
        Canvas.SetTop(titleStack, 610);
        duskPageHost.Children.Add(titleStack);

        // 7. Centered Pill Tabs at top = 712
        var tabsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        // Tab: STUDIO (active)
        var tabStudio = new Border
        {
            Padding = new Thickness(24, 10, 24, 10),
            CornerRadius = new CornerRadius(24),
            Background = DuskKit.SlateTabOnBrush,
            Margin = new Thickness(0, 0, 12, 0),
            Child = new TextBlock
            {
                Text = "STUDIO",
                FontFamily = DuskKit.Display,
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = DuskKit.WhiteBrush
            }
        };
        tabsPanel.Children.Add(tabStudio);

        // Tab: PLAYERS with count badge
        var tabPlayers = new Border
        {
            Padding = new Thickness(24, 10, 24, 10),
            CornerRadius = new CornerRadius(24),
            Background = Brushes.Transparent,
            Margin = new Thickness(0, 0, 12, 0),
            Cursor = Cursors.Hand
        };
        var playerStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        playerStack.Children.Add(new TextBlock
        {
            Text = "PLAYERS",
            FontFamily = DuskKit.Display,
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            Foreground = DuskKit.LightBrush,
            Margin = new Thickness(0, 0, 10, 0)
        });
        var pBadge = new Border
        {
            Padding = new Thickness(10, 1, 10, 1),
            CornerRadius = new CornerRadius(11),
            Background = DuskKit.GreenOnlineBrush,
            Child = new TextBlock
            {
                Text = "2",
                FontFamily = DuskKit.Display,
                FontSize = 15,
                FontWeight = FontWeights.ExtraBold,
                Foreground = DuskKit.SlateBgBrush
            }
        };
        // No player count is shown: this page does not know how many pads are connected.
        tabPlayers.Child = playerStack;
        tabPlayers.MouseLeftButtonUp += (_, _) => BackRequested?.Invoke();
        tabsPanel.Children.Add(tabPlayers);

        // Tab: SETUPS
        var tabSetups = new Border
        {
            Padding = new Thickness(24, 10, 24, 10),
            CornerRadius = new CornerRadius(24),
            Background = Brushes.Transparent,
            Cursor = Cursors.Hand,
            Child = new TextBlock
            {
                Text = "SETUPS",
                FontFamily = DuskKit.Display,
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = DuskKit.LightBrush
            }
        };
        tabSetups.MouseLeftButtonUp += (_, _) => DuskSelectPage("setups");
        tabsPanel.Children.Add(tabSetups);

        Canvas.SetLeft(tabsPanel, 540);
        Canvas.SetTop(tabsPanel, 712);
        duskPageHost.Children.Add(tabsPanel);

        // 8. Shelves peeking under tabs
        var sh1 = new TextBlock
        {
            Text = "YOUR PAGES",
            FontFamily = DuskKit.Display,
            FontSize = 16,
            FontWeight = FontWeights.ExtraBold,
            Foreground = DuskKit.MuteBrush
        };
        Canvas.SetLeft(sh1, 32);
        Canvas.SetTop(sh1, 782);
        duskPageHost.Children.Add(sh1);

        var sh2 = new TextBlock
        {
            Text = "PC MODE",
            FontFamily = DuskKit.Display,
            FontSize = 16,
            FontWeight = FontWeights.ExtraBold,
            Foreground = DuskKit.MuteBrush
        };
        Canvas.SetLeft(sh2, 1000);
        Canvas.SetTop(sh2, 782);
        duskPageHost.Children.Add(sh2);

        // 3 mini capsules (300x170) for Buttons, Curves, Macros
        var miniPages = new (string Name, string Key, string Icon)[]
        {
            ("Buttons", "buttons", "\uE7FC"),
            ("Curves", "curves", "\uE9D2"),
            ("Macros", "macros", "\uE945")
        };

        for (int i = 0; i < miniPages.Length; i++)
        {
            var p = miniPages[i];
            var cap = new Border
            {
                Width = 300,
                Height = 170,
                CornerRadius = new CornerRadius(4),
                Background = DuskKit.SlatePanelBrush,
                BorderBrush = DuskKit.B("rgba(26,159,255,0.35)"),
                BorderThickness = new Thickness(1, 2, 1, 1),
                Cursor = Cursors.Hand,
                ClipToBounds = true,
                Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 14, ShadowDepth = 6, Opacity = 0.5 }
            };
            var capCanvas = new Canvas { Width = 300, Height = 170 };

            var art = DuskKit.ControllerArt(padKey, 280);
            if (art != null)
            {
                art.Opacity = 0.25;
                Canvas.SetLeft(art, 20);
                Canvas.SetTop(art, -20);
                capCanvas.Children.Add(art);
            }

            var capText = new StackPanel { Orientation = Orientation.Horizontal };
            capText.Children.Add(new TextBlock
            {
                Text = p.Icon,
                FontFamily = DuskKit.Icons,
                FontSize = 20,
                Foreground = DuskKit.WhiteBrush,
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            capText.Children.Add(new TextBlock
            {
                Text = p.Name,
                FontFamily = DuskKit.Display,
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = DuskKit.WhiteBrush,
                VerticalAlignment = VerticalAlignment.Center
            });
            Canvas.SetLeft(capText, 20);
            Canvas.SetBottom(capText, 16);
            capCanvas.Children.Add(capText);

            cap.Child = capCanvas;
            string targetKey = p.Key;
            cap.MouseLeftButtonUp += (_, _) => DuskSelectPage(targetKey);

            Canvas.SetLeft(cap, 32 + i * 316);
            Canvas.SetTop(cap, 812);
            duskPageHost.Children.Add(cap);
        }

        // PC Mode shelf capsule (520x170) at (1000, 812)
        var pcCap = new Border
        {
            Width = 520,
            Height = 170,
            CornerRadius = new CornerRadius(4),
            Background = DuskKit.SlatePanelBrush,
            BorderBrush = DuskKit.B("rgba(89,191,64,0.35)"),
            BorderThickness = new Thickness(1, 2, 1, 1),
            Cursor = Cursors.Hand,
            ClipToBounds = true,
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 14, ShadowDepth = 6, Opacity = 0.5 }
        };
        var pcCanvas = new Canvas { Width = 520, Height = 170 };
        var pcArt = DuskKit.ControllerArt("x10", 380);
        if (pcArt != null)
        {
            pcArt.Opacity = 0.25;
            Canvas.SetLeft(pcArt, 140);
            Canvas.SetTop(pcArt, -40);
            pcCanvas.Children.Add(pcArt);
        }

        var pcText = new StackPanel { Orientation = Orientation.Horizontal };
        pcText.Children.Add(new TextBlock
        {
            Text = "\uE977",
            FontFamily = DuskKit.Icons,
            FontSize = 20,
            Foreground = DuskKit.WhiteBrush,
            Margin = new Thickness(0, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        pcText.Children.Add(new TextBlock
        {
            Text = "PC Mode & Routing",
            FontFamily = DuskKit.Display,
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            Foreground = DuskKit.WhiteBrush,
            VerticalAlignment = VerticalAlignment.Center
        });
        Canvas.SetLeft(pcText, 24);
        Canvas.SetBottom(pcText, 16);
        pcCanvas.Children.Add(pcText);

        pcCap.Child = pcCanvas;
        pcCap.MouseLeftButtonUp += (_, _) => DuskSelectPage("pcmode");
        Canvas.SetLeft(pcCap, 1000);
        Canvas.SetTop(pcCap, 812);
        duskPageHost.Children.Add(pcCap);

        // 9. Steam Deck Bottom Bar at (0, 844)
        var bar = DuskKit.BottomBar(new[] { ("A", "SELECT"), ("B", "BACK") });
        Canvas.SetLeft(bar, 0);
        Canvas.SetTop(bar, 844);
        duskPageHost.Children.Add(bar);
    }
}
