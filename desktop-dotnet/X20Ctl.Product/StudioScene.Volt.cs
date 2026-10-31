using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Volt;

namespace X20Ctl.Product;

/// <summary>
/// Studio Scene implementation for the Volt theme (Steam Deck Yellow).
/// Strictly implements t_volt.py:dashboard(), t_volt.py:side(), and Steam Deck settings sheets.
/// </summary>
public partial class StudioScene
{
    private Canvas? voltPageHost;
    private string currentVoltPage = "Dashboard";

    public bool IsVolt => ThemeCatalog.Current.Id == "volt";

    private void BuildVoltStudio()
    {
        if (!IsVolt || voltPageHost != null) return;

        RootLayout.Visibility = Visibility.Hidden;

        voltPageHost = new Canvas
        {
            Width = 1600,
            Height = 900,
            ClipToBounds = true,
            Background = VoltKit.BlackSkyBrush
        };
        Canvas.SetLeft(voltPageHost, 0);
        Canvas.SetTop(voltPageHost, 0);

        if (StudioShell.Parent is Grid parentGrid)
        {
            if (!parentGrid.Children.Contains(voltPageHost))
            {
                parentGrid.Children.Add(voltPageHost);
            }
        }
        else
        {
            StudioShell.Children.Add(voltPageHost);
        }

        VoltShow();
    }

    private bool voltQueued;
    public void VoltLater()
    {
        if (!IsVolt || voltQueued) return;
        voltQueued = true;
        Dispatcher.InvokeAsync(() =>
        {
            voltQueued = false;
            currentVoltPage = CurrentPage;
            VoltShow();
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    public void VoltSelectPage(string page)
    {
        if (!IsVolt || voltPageHost == null) return;
        currentVoltPage = page;

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
                currentVoltPage = "Dashboard";
                break;
        }

        VoltShow();
    }

    public void VoltShow()
    {
        if (!IsVolt || voltPageHost == null) return;
        voltPageHost.Children.Clear();

        string page = currentVoltPage.ToLowerInvariant();
        UIElement pageContent = page switch
        {
            "buttons" => BuildVoltButtons(),
            "curves" => BuildVoltCurves(),
            "macros" => BuildVoltMacros(),
            "vibration" => BuildVoltVibration(),
            "pcmode" => BuildVoltPcMode(),
            "device" => BuildVoltDevice(),
            "diagnostics" => BuildVoltDiagnostics(),
            "mouse" => BuildVoltMouse(),
            "setups" => BuildVoltSetups(),
            "tester" => BuildVoltTester(),
            _ => BuildVoltDashboard()
        };

        voltPageHost.Children.Add(pageContent);
    }

    /// <summary>
    /// Dashboard for Volt: Black sky over yellow wave, top status bar, centered 3D carousel with Dune as featured card,
    /// tilted neighbour cards with yellow wash, chevrons, dots, and bottom hint bar.
    /// Strictly implements t_volt.py:97 dashboard().
    /// </summary>
    private UIElement BuildVoltDashboard()
    {
        var root = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };

        // 1. Soft blurred yellow wave backdrop
        root.Children.Add(VoltKit.WaveBackdrop(16));

        // 2. Top status bar
        root.Children.Add(VoltKit.TopBar());

        // 3. Title typography
        string modelName = Model?.Name ?? "EASYSMX DUNE";
        var titleStack = new StackPanel { Orientation = Orientation.Vertical, Width = 1600 };

        var badgeRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 8) };
        var telemetryBadge = new Border
        {
            Padding = new Thickness(14, 4, 14, 4),
            CornerRadius = new CornerRadius(12),
            Background = VoltKit.B("rgba(30,22,10,0.85)"),
            BorderBrush = VoltKit.B("rgba(255,173,10,0.5)"),
            BorderThickness = new Thickness(1),
            Child = new TextBlock
            {
                Text = "X20CTL · LOCAL SETTINGS",
                FontFamily = VoltKit.Display,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = VoltKit.AmberHotBrush
            }
        };

        titleStack.Children.Add(new TextBlock
        {
            Text = modelName.ToUpperInvariant(),
            FontFamily = VoltKit.Display,
            FontSize = 36,
            FontWeight = FontWeights.Bold,
            Foreground = VoltKit.WhiteBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 14, ShadowDepth = 4, Opacity = 0.5 }
        });

        var localPill = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Padding = new Thickness(16, 5, 16, 5),
            CornerRadius = new CornerRadius(12),
            Background = VoltKit.B("#1c1c1c"),
            BorderBrush = VoltKit.B("#333333"),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 8, 0, 0)
        };
        var localStack = new StackPanel { Orientation = Orientation.Horizontal };
        localStack.Children.Add(new TextBlock
        {
            Text = "\uE777",
            FontFamily = VoltKit.Icons,
            FontSize = 13,
            Foreground = VoltKit.AmberHotBrush,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        localStack.Children.Add(new TextBlock
        {
            Text = "LOCAL WORKING SETTINGS",
            FontFamily = VoltKit.Display,
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = VoltKit.B("#e0e0e0"),
            VerticalAlignment = VerticalAlignment.Center
        });
        localPill.Child = localStack;
        titleStack.Children.Add(localPill);

        Canvas.SetLeft(titleStack, 0);
        Canvas.SetTop(titleStack, 310);
        root.Children.Add(titleStack);

        // Stage Floor Pedestal Glow beneath Carousel
        var floorGlow = VoltKit.PedestalGlow(600, 160, VoltKit.AmberHot);
        Canvas.SetLeft(floorGlow, 500);
        Canvas.SetTop(floorGlow, 590);
        root.Children.Add(floorGlow);

        // 4. Centered 3D Carousel (y = 426)
        var carouselCanvas = new Canvas { Width = 1600, Height = 450 };

        var sideCards = new (string Page, string Label, int Offset)[]
        {
            ("buttons", "BUTTONS", -3),
            ("curves", "CURVES", -2),
            ("macros", "MACROS", -1),
            ("vibration", "VIBRATION", 1),
            ("pcmode", "PC MODE", 2),
            ("setups", "SETUPS", 3)
        };

        foreach (var (pName, pLabel, d) in sideCards)
        {
            double x = 675 + d * 262 + (d > 0 ? 12 : -12);
            double rot = d < 0 ? 18 : -18;

            var cap = new Border
            {
                Width = 236,
                Height = 340,
                CornerRadius = new CornerRadius(12),
                Background = VoltKit.CardDarkBrush,
                ClipToBounds = true,
                Opacity = 0.62,
                Cursor = Cursors.Hand
            };

            var capGrid = new Grid();
            string padModel = Math.Abs(d) == 2 ? "x20pro" : "dune";
            var art = VoltKit.ControllerArt(padModel, 236);
            if (art != null)
            {
                Canvas.SetLeft(art, -20);
                Canvas.SetTop(art, 20);
                var c = new Canvas { Width = 236, Height = 340 };
                c.Children.Add(art);
                capGrid.Children.Add(c);
            }

            // Yellow wash overlay
            var wash = new Rectangle
            {
                Fill = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(0, 1),
                    GradientStops =
                    {
                        new(VoltKit.C("rgba(255,214,10,.42)"), 0.0),
                        new(VoltKit.C("rgba(255,200,0,.62)"), 1.0)
                    }
                }
            };
            capGrid.Children.Add(wash);

            var label = new TextBlock
            {
                Text = pLabel,
                FontFamily = VoltKit.LogoFont,
                FontSize = 46,
                FontWeight = FontWeights.Bold,
                Foreground = VoltKit.WhiteBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 22),
                Effect = new DropShadowEffect { Color = VoltKit.C("#785000"), BlurRadius = 10, ShadowDepth = 2, Opacity = 0.5 }
            };
            capGrid.Children.Add(label);

            cap.Child = capGrid;
            cap.MouseLeftButtonUp += (_, _) => VoltSelectPage(pName);

            Canvas.SetLeft(cap, x);
            Canvas.SetTop(cap, 448);
            carouselCanvas.Children.Add(cap);
        }

        // Featured Center Card: DUNE (675, 426, w: 250, h: 372)
        var centerCap = new Border
        {
            Width = 250,
            Height = 372,
            CornerRadius = new CornerRadius(12),
            ClipToBounds = true,
            Cursor = Cursors.Hand,
            BorderBrush = VoltKit.WhiteBrush,
            BorderThickness = new Thickness(3),
            Effect = new DropShadowEffect
            {
                Color = VoltKit.C("#ffd000"),
                BlurRadius = 40,
                ShadowDepth = 0,
                Opacity = 0.55
            }
        };

        var centerGrid = new Grid();
        var centerBg = new Rectangle
        {
            Fill = new RadialGradientBrush
            {
                Center = new Point(0.5, 0.2),
                GradientOrigin = new Point(0.5, 0.2),
                RadiusX = 1.2,
                RadiusY = 0.9,
                GradientStops =
                {
                    new(VoltKit.C("#6b4f00"), 0.0),
                    new(VoltKit.C("#1d1500"), 1.0)
                }
            }
        };
        centerGrid.Children.Add(centerBg);

        var centerArt = new Image { Width = 374, Source = ControllerArt.Front(Model.Id, PadColours.For(Player, Model.Id)), Stretch = Stretch.Uniform };
        if (centerArt != null)
        {
            centerArt.Effect = new DropShadowEffect { Color = VoltKit.C("#3c1e00"), BlurRadius = 18, ShadowDepth = 12, Opacity = 0.5 };
            var artCanvas = new Canvas { Width = 250, Height = 372 };
            Canvas.SetLeft(centerArt, -62);
            Canvas.SetTop(centerArt, 58);
            artCanvas.Children.Add(centerArt);
            centerGrid.Children.Add(artCanvas);
        }

        var centerLabel = new TextBlock
        {
            Text = Model.Name.Replace("EasySMX ", "", StringComparison.OrdinalIgnoreCase).ToUpperInvariant(),
            FontFamily = VoltKit.LogoFont,
            FontSize = 56,
            FontWeight = FontWeights.Bold,
            Foreground = VoltKit.WhiteBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 22),
            Effect = new DropShadowEffect { Color = VoltKit.C("#5a3200"), BlurRadius = 12, ShadowDepth = 2, Opacity = 0.6 }
        };
        centerGrid.Children.Add(centerLabel);

        centerCap.Child = centerGrid;
        centerCap.MouseLeftButtonUp += (_, _) => VoltSelectPage("buttons");

        Canvas.SetLeft(centerCap, 675);
        Canvas.SetTop(centerCap, 426);
        carouselCanvas.Children.Add(centerCap);

        root.Children.Add(carouselCanvas);

        // 5. Chevrons
        var cL = VoltKit.Chevron(false, () => VoltSelectPage("setups"));
        Canvas.SetLeft(cL, 28);
        Canvas.SetTop(cL, 452);
        root.Children.Add(cL);

        var cR = VoltKit.Chevron(true, () => VoltSelectPage("buttons"));
        Canvas.SetLeft(cR, 1518);
        Canvas.SetTop(cR, 452);
        root.Children.Add(cR);

        // 6. Page Dots
        var dots = VoltKit.CarouselDots(0, 4);
        Canvas.SetLeft(dots, 760);
        Canvas.SetTop(dots, 812);
        root.Children.Add(dots);

        // 7. Bottom Bar
        var bar = VoltKit.BottomBar(new[] { ("\u2261", "OPTIONS"), ("A", "SELECT"), ("B", "BACK") });
        Canvas.SetLeft(bar, 0);
        Canvas.SetTop(bar, 844);
        root.Children.Add(bar);

        return root;
    }
}
