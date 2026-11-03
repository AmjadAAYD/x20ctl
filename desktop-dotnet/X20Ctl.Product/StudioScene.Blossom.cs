using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Blossom;

namespace X20Ctl.Product;

/// <summary>
/// Studio presentation coordinator for the Yozakura (Blossom) theme:
/// "Hanami at night is a gathering in the dark, lit only by lanterns, under blossom that will be gone in a week.
/// So X20CTL is an evening under the trees:
///   - the Studio pages are lanterns on a string, and the page you are on is the lit one;
///   - focus is lantern light: a warm glow falls on the focused row, and the focused details are printed on lit paper."
/// Strictly implements t_blossom.py specifications.
/// </summary>
public partial class StudioScene
{
    private Canvas? blossomPageHost;
    private string currentBlossomPage = "Dashboard";

    public bool IsBlossom => ThemeCatalog.Current.Id == "blossom";

    private void BuildBlossomStudio()
    {
        if (!IsBlossom || blossomPageHost != null) return;

        RootLayout.Visibility = Visibility.Hidden;

        blossomPageHost = new Canvas
        {
            Width = 1600,
            Height = 900,
            ClipToBounds = true,
            Background = BlossomKit.VoidBgBrush
        };
        Canvas.SetLeft(blossomPageHost, 0);
        Canvas.SetTop(blossomPageHost, 0);

        if (StudioShell.Parent is Grid parentGrid)
        {
            if (!parentGrid.Children.Contains(blossomPageHost))
            {
                parentGrid.Children.Add(blossomPageHost);
            }
        }
        else
        {
            StudioShell.Children.Add(blossomPageHost);
        }

        currentBlossomPage = CurrentPage;
        BlossomShow();
    }

    private bool blossomQueued;
    public void BlossomLater()
    {
        if (!IsBlossom || blossomQueued) return;
        blossomQueued = true;
        Dispatcher.InvokeAsync(() =>
        {
            blossomQueued = false;
            currentBlossomPage = CurrentPage;
            BlossomShow();
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    public void BlossomSelectPage(string page)
    {
        if (!IsBlossom || blossomPageHost == null) return;
        currentBlossomPage = page;

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
            case "diagnostics":
                ShowManagement("Diagnostics");
                break;
            case "tester":
                ShowManagement("Tester");
                break;
            case "dashboard":
                ShowManagement("Dashboard");
                break;
            default:
                break;
        }

        BlossomShow();
    }

    public void BlossomShow()
    {
        if (!IsBlossom || blossomPageHost == null) return;
        blossomPageHost.Children.Clear();

        string page = currentBlossomPage;

        switch (page.ToLowerInvariant())
        {
            case "buttons":
                RenderBlossomButtons(blossomPageHost);
                break;
            case "curves":
                RenderBlossomCurves(blossomPageHost);
                break;
            case "macros":
                RenderBlossomMacros(blossomPageHost);
                break;
            case "vibration":
                RenderBlossomVibration(blossomPageHost);
                break;
            case "pcmode":
                RenderBlossomPcMode(blossomPageHost);
                break;
            case "device":
                RenderBlossomDevice(blossomPageHost);
                break;
            case "diagnostics":
                RenderBlossomDiagnostics(blossomPageHost);
                break;
            case "mouse":
                RenderBlossomMouse(blossomPageHost);
                break;
            case "setups":
                RenderBlossomSetups(blossomPageHost);
                break;
            case "tester":
                RenderBlossomTester(blossomPageHost);
                break;
            default:
                RenderBlossomDashboard(blossomPageHost);
                break;
        }
    }

    /// <summary>
    /// Builds the left-rail vertical string of lanterns for Studio sub-page navigation.
    /// Strictly matches t_blossom.py:nav(cur).
    /// </summary>
    private void RenderBlossomLeftNav(Canvas host, string activePage)
    {
        // Blossom Logo at top
        var logo = BlossomKit.BlossomLogo(116);
        if (logo != null)
        {
            logo.Cursor = Cursors.Hand;
            logo.MouseLeftButtonUp += (_, _) =>
            {
                currentBlossomPage = "dashboard";
                BlossomShow();
            };
            Canvas.SetLeft(logo, 30);
            Canvas.SetTop(logo, 16);
            host.Children.Add(logo);
        }

        // Vertical suspension cord
        var wire = new Line
        {
            X1 = 88,
            Y1 = 96,
            X2 = 88,
            Y2 = 880,
            Stroke = BlossomKit.B("#3a2430"),
            StrokeThickness = 2.0
        };
        host.Children.Add(wire);

        var pages = new (string Name, string Key, string Icon)[]
        {
            ("Buttons", "buttons", "\uE8B7"),
            ("Curves", "curves", "\uE9D9"),
            ("Macros", "macros", "\uE7C8"),
            ("Vibration", "vibration", "\uE83F"),
            ("PC Mode", "pcmode", "\uE7F4"),
            ("Device", "device", "\uE964"),
            ("Diagnostics", "diagnostics", "\uE9D9"),
            ("Mouse", "mouse", "\uE962"),
            ("Setups", "setups", "\uE71D"),
            ("Tester", "tester", "\uE768")
        };

        for (int i = 0; i < pages.Length; i++)
        {
            var p = pages[i];
            bool lit = string.Equals(p.Key, activePage, StringComparison.OrdinalIgnoreCase);
            double y = 130 + i * 74;

            double lanternW = lit ? 50 : 42;
            double lanternH = lit ? 62 : 54;

            var lanternElem = BlossomKit.PaperLantern(
                lanternW,
                lanternH,
                lit,
                p.Icon,
                null,
                lit ? 1.0 : 0.6,
                14);

            lanternElem.Cursor = Cursors.Hand;
            lanternElem.MouseLeftButtonUp += (_, _) => BlossomSelectPage(p.Key);

            Canvas.SetLeft(lanternElem, 88 - lanternElem.Width / 2.0);
            Canvas.SetTop(lanternElem, y);
            host.Children.Add(lanternElem);

            if (lit)
            {
                var labelTb = new TextBlock
                {
                    Text = p.Name,
                    FontFamily = BlossomKit.Display,
                    FontSize = 20,
                    Foreground = BlossomKit.B("#ffd3a8"),
                    Cursor = Cursors.Hand,
                    Effect = new DropShadowEffect
                    {
                        Color = Color.FromRgb(0xff, 0x96, 0x5a),
                        BlurRadius = 14,
                        ShadowDepth = 0,
                        Opacity = 0.7
                    }
                };
                labelTb.MouseLeftButtonUp += (_, _) => BlossomSelectPage(p.Key);
                Canvas.SetLeft(labelTb, 122);
                Canvas.SetTop(labelTb, y + 16);
                host.Children.Add(labelTb);
            }
        }
    }

    /// <summary>
    /// Renders the grand Hanami Dashboard (Home) page.
    /// Strictly implements t_blossom.py:dashboard().
    /// </summary>
    private void RenderBlossomDashboard(Canvas host)
    {
        // 1. Starry Night Sky Backdrop
        var sky = new Rectangle { Width = 1600, Height = 900, Fill = BlossomKit.CreateSkyBackdrop() };
        host.Children.Add(sky);

        // 2. Full Moon in top right
        var moon = BlossomKit.MoonSphere(300);
        Canvas.SetLeft(moon, 1160);
        Canvas.SetTop(moon, 20);
        host.Children.Add(moon);

        // 3. Ground Mist Glow
        var groundMist = new Border
        {
            Width = 1600,
            Height = 340,
            Background = new RadialGradientBrush
            {
                Center = new Point(0.5, 0.6),
                GradientOrigin = new Point(0.5, 0.6),
                RadiusX = 0.6,
                RadiusY = 0.5,
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb(28, 255, 170, 150), 0.0),
                    new GradientStop(Colors.Transparent, 0.7)
                }
            }
        };
        Canvas.SetLeft(groundMist, 0);
        Canvas.SetTop(groundMist, 560);
        host.Children.Add(groundMist);

        // 4. Weeping Cherry Branch Canopy across top
        host.Children.Add(BlossomKit.CanopyBranch("home", 12));

        // 5. Drifting Sakura Petals
        host.Children.Add(BlossomKit.DriftingPetals(new Rect(80, 160, 1460, 600), 34, 12));

        // 6. Header: Logo & Title
        var logo = BlossomKit.BlossomLogo(100);
        if (logo != null)
        {
            logo.Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(0xff, 0x78, 0xa0),
                BlurRadius = 14,
                ShadowDepth = 0,
                Opacity = 0.45
            };
            Canvas.SetLeft(logo, 44);
            Canvas.SetTop(logo, 28);
            host.Children.Add(logo);
        }

        var headerTitle = new TextBlock
        {
            Text = "X20CTL",
            FontFamily = BlossomKit.Display,
            FontSize = 32,
            Foreground = BlossomKit.TextWhiteBrush
        };
        Canvas.SetLeft(headerTitle, 156);
        Canvas.SetTop(headerTitle, 50);
        host.Children.Add(headerTitle);

        // Status Header
        var statusStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        statusStack.Children.Add(new TextBlock
        {
            Text = DateTime.Now.ToString("HH:mm"),
            FontFamily = BlossomKit.Body,
            FontSize = 16,
            Foreground = BlossomKit.TextMuteBrush
        });
        Canvas.SetLeft(statusStack, 360);
        Canvas.SetTop(statusStack, 58);
        host.Children.Add(statusStack);

        // 7. Left Player & Controller Telemetry Stack
        var infoStack = new StackPanel { Width = 420 };
        infoStack.Children.Add(new TextBlock
        {
            Text = "CONTROLLER",
            FontFamily = BlossomKit.Small,
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Foreground = BlossomKit.B("#b48a99")
        });
        infoStack.Children.Add(new TextBlock
        {
            Text = Model.Name,
            FontFamily = BlossomKit.Display,
            FontSize = 54,
            Foreground = BlossomKit.TextWhiteBrush,
            Margin = new Thickness(0, 8, 0, 0)
        });
        infoStack.Children.Add(new TextBlock
        {
            Text = Model.Id.Contains("black") ? "Obsidian" : "Cherry Blossom",
            FontFamily = BlossomKit.Heading,
            FontStyle = FontStyles.Italic,
            FontSize = 24,
            Foreground = BlossomKit.TextSakuraBrush,
            Margin = new Thickness(0, 4, 0, 0)
        });


        Canvas.SetLeft(infoStack, 58);
        Canvas.SetTop(infoStack, 196);
        host.Children.Add(infoStack);

        // 8. Controller Artwork with Warm Stage Glow
        var padGlow = new Ellipse
        {
            Width = 560,
            Height = 120,
            Fill = new RadialGradientBrush
            {
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb(80, 255, 150, 110), 0.0),
                    new GradientStop(Colors.Transparent, 1.0)
                }
            },
            Effect = new BlurEffect { Radius = 16 }
        };
        Canvas.SetLeft(padGlow, 820);
        Canvas.SetTop(padGlow, 520);
        host.Children.Add(padGlow);

        var padArt = BlossomKit.ControllerArt(Model.Id, 560);
        if (padArt != null)
        {
            padArt.Effect = new DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 30,
                ShadowDepth = 26,
                Opacity = 0.65
            };
            Canvas.SetLeft(padArt, 790);
            Canvas.SetTop(padArt, 200);
            host.Children.Add(padArt);
        }

        // 9. Curved Festival Wire across bottom with Hanging Lanterns for Pages
        double Ry(double x) => 616 + 92 * (1 - Math.Pow((x - 800) / 860.0, 2));

        var festivalWireGeom = new PathGeometry();
        var wireFig = new PathFigure { StartPoint = new Point(-20, Ry(-20)) };
        wireFig.Segments.Add(new BezierSegment(new Point(800, 2 * Ry(800) - Ry(-20)), new Point(800, 2 * Ry(800) - Ry(-20)), new Point(1620, Ry(1620)), true));
        festivalWireGeom.Figures.Add(wireFig);

        var festivalWire = new System.Windows.Shapes.Path
        {
            Data = festivalWireGeom,
            Stroke = BlossomKit.B("#4a2c38"),
            StrokeThickness = 2.4
        };
        host.Children.Add(festivalWire);

        var subPages = new (string Name, string Key, string Icon)[]
        {
            ("Buttons", "buttons", "\uE8B7"),
            ("Curves", "curves", "\uE9D9"),
            ("Macros", "macros", "\uE7C8"),
            ("Vibration", "vibration", "\uE83F"),
            ("PC Mode", "pcmode", "\uE7F4"),
            ("Device", "device", "\uE964"),
            ("Diagnostics", "diagnostics", "\uE9D9"),
            ("Mouse", "mouse", "\uE962"),
            ("Setups", "setups", "\uE71D")
        };

        for (int i = 0; i < subPages.Length; i++)
        {
            double x = 200 + i * 150;
            bool lit = i == 0;
            double lanternW = lit ? 64 : 56;
            double lanternH = lit ? 84 : 74;

            var lantern = BlossomKit.PaperLantern(
                lanternW,
                lanternH,
                lit,
                subPages[i].Icon,
                null,
                lit ? 1.2 : 0.6,
                24);

            lantern.Cursor = Cursors.Hand;
            string targetKey = subPages[i].Key;
            lantern.MouseLeftButtonUp += (_, _) => BlossomSelectPage(targetKey);

            Canvas.SetLeft(lantern, x - lantern.Width / 2.0);
            Canvas.SetTop(lantern, Ry(x) + 24);
            host.Children.Add(lantern);

            if (lit)
            {
                var pageTitleTb = new TextBlock
                {
                    Text = subPages[i].Name,
                    FontFamily = BlossomKit.Display,
                    FontSize = 24,
                    Foreground = BlossomKit.B("#ffd3a8"),
                    TextAlignment = TextAlignment.Center,
                    Width = 160,
                    Cursor = Cursors.Hand,
                    Effect = new DropShadowEffect
                    {
                        Color = Color.FromRgb(0xff, 0x96, 0x5a),
                        BlurRadius = 18,
                        ShadowDepth = 0,
                        Opacity = 0.7
                    }
                };
                pageTitleTb.MouseLeftButtonUp += (_, _) => BlossomSelectPage(targetKey);
                Canvas.SetLeft(pageTitleTb, x - 80);
                Canvas.SetTop(pageTitleTb, Ry(x) + 140);
                host.Children.Add(pageTitleTb);
            }
        }

        // Bumper badges
        var lb = CreateKeyBadge("LB");
        Canvas.SetLeft(lb, 36);
        Canvas.SetTop(lb, 600);
        host.Children.Add(lb);

        var rb = CreateKeyBadge("RB");
        Canvas.SetLeft(rb, 1536);
        Canvas.SetTop(rb, 600);
        host.Children.Add(rb);

        // Footer Hints: [A] Select  [B] Controller Zone  [X] Configure  [Y] Rescan games
        var hintsStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        hintsStack.Children.Add(CreateHint("A", "Select"));
        hintsStack.Children.Add(CreateHint("B", "Controller Zone"));
        hintsStack.Children.Add(CreateHint("X", "Configure"));
        hintsStack.Children.Add(CreateHint("Y", "Rescan games"));

        Canvas.SetRight(hintsStack, 44);
        Canvas.SetTop(hintsStack, 846);
        host.Children.Add(hintsStack);
    }

    private FrameworkElement CreateKeyBadge(string key, bool active = false)
    {
        var border = new Border
        {
            MinWidth = 28,
            Height = 28,
            Padding = new Thickness(6, 0, 6, 0),
            CornerRadius = new CornerRadius(14),
            Background = active ? BlossomKit.CreateWarmLanternBrush() : BlossomKit.B("transparent"),
            BorderBrush = active ? BlossomKit.B("transparent") : BlossomKit.B("rgba(246,233,236,0.65)"),
            BorderThickness = new Thickness(1.5),
            VerticalAlignment = VerticalAlignment.Center
        };
        if (active)
        {
            border.Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(0xff, 0x96, 0x5a),
                BlurRadius = 14,
                ShadowDepth = 0,
                Opacity = 0.6
            };
        }
        var tb = new TextBlock
        {
            Text = key,
            FontFamily = BlossomKit.Display,
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = active ? BlossomKit.B("#4a140f") : BlossomKit.TextWhiteBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        border.Child = tb;
        return border;
    }

    private FrameworkElement CreateHint(string key, string text)
    {
        var stack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 26, 0)
        };
        stack.Children.Add(CreateKeyBadge(key));
        var tb = new TextBlock
        {
            Text = text,
            FontFamily = BlossomKit.Body,
            FontSize = 17,
            Foreground = BlossomKit.B("#e3cfd6"),
            Margin = new Thickness(9, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        stack.Children.Add(tb);
        return stack;
    }
}
