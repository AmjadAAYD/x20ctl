using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Volt;
using X20Ctl.Zone;

namespace X20Ctl.Product;

/// <summary>
/// Controller Zone and Hero presentation for the Volt theme:
/// "VOLT — the Steam Deck yellow theme. Black sky over a soft, blurred yellow wave; a '?' key and the clock top left...
/// The Silksong page (key art, serif logo, a dark band with an orange action button and square keys)
/// becomes the Zone, the settings pages, Themes and the Hero View."
/// Strictly implements t_volt.py:zone() and t_volt.py:hero().
/// </summary>
public partial class ZoneScene
{
    internal bool IsVolt => ThemeCatalog.Current.Id == "volt";

    private Grid? voltBoard;
    private Canvas? voltZoneBack, voltHeroBack;
    private int voltFocusedIndex = 0;

    // References to dynamically updated Hero elements
    private StackPanel? voltHeroLogoStack;
    private TextBlock? voltHeroBrandText, voltHeroModelText, voltHeroSubText;
    private Canvas? voltHeroArtContainer;
    private StackPanel? voltHeroTelemetryRow;

    private void BuildVolt()
    {
        var original = Surface.Children.OfType<Grid>().First();
        EnvironmentBackdrop.Visibility = Visibility.Collapsed;
        OuterFrame.Background = VoltKit.BlackSkyBrush;
        OuterFrame.BorderBrush = VoltKit.BlackSkyBrush;

        voltBoard = new Grid
        {
            Width = 1600,
            Height = 900,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            ClipToBounds = true
        };

        // 1. Zone Overview Stage
        voltZoneBack = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };

        // Background yellow wave
        voltZoneBack.Children.Add(VoltKit.WaveBackdrop(20));

        // Top status bar
        voltZoneBack.Children.Add(VoltKit.TopBar());

        // Header / Section label at top = 80
        var navStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var titleBlock = new Border
        {
            Padding = new Thickness(24, 8, 24, 8),
            CornerRadius = new CornerRadius(16),
            Background = VoltKit.B("rgba(26, 20, 10, 0.85)"),
            BorderBrush = VoltKit.B("rgba(255, 173, 10, 0.55)"),
            BorderThickness = new Thickness(1.5),
            Effect = new DropShadowEffect { Color = VoltKit.AmberHot, BlurRadius = 16, ShadowDepth = 2, Opacity = 0.35 }
        };
        var tRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var tBeacon = new Ellipse
        {
            Width = 9,
            Height = 9,
            Fill = VoltKit.AmberHotBrush,
            Margin = new Thickness(0, 0, 10, 0),
            Effect = new DropShadowEffect { Color = VoltKit.AmberHot, BlurRadius = 10, Opacity = 1.0, ShadowDepth = 0 }
        };
        tRow.Children.Add(tBeacon);
        tRow.Children.Add(new TextBlock
        {
            Text = "VOLT CONSOLE DECK · QUADRANT DOCKS",
            FontFamily = VoltKit.Display,
            FontSize = 15,
            FontWeight = FontWeights.ExtraBold,
            Foreground = VoltKit.WhiteBrush
        });
        titleBlock.Child = tRow;
        navStack.Children.Add(titleBlock);

        // Active telemetry chip
        var statPill = new Border
        {
            Padding = new Thickness(14, 8, 14, 8),
            CornerRadius = new CornerRadius(16),
            Background = VoltKit.B("rgba(20, 20, 20, 0.8)"),
            BorderBrush = VoltKit.B("rgba(255, 255, 255, 0.12)"),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(12, 0, 0, 0),
            Child = new TextBlock
            {
                Text = "1000HZ ULTRA-POLLING",
                FontFamily = VoltKit.Display,
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = VoltKit.AmberHotBrush
            }
        };

        Canvas.SetLeft(navStack, 540);
        Canvas.SetTop(navStack, 84);
        voltZoneBack.Children.Add(navStack);

        // Bottom bar at top = 836
        var bar = VoltKit.BottomBar(new[]
        {
            ("A", "SELECT / STUDIO"),
            ("X", "HERO VIEW"),
            ("Y", "CHANGE COLOR"),
            ("B", "SETTINGS")
        });
        Canvas.SetLeft(bar, 0);
        Canvas.SetTop(bar, 836);
        voltZoneBack.Children.Add(bar);

        voltBoard.Children.Add(voltZoneBack);

        // 2. Focused / Heroic Stage
        BuildVoltHero();
        voltBoard.Children.Add(voltHeroBack!);

        // 3. Move DockStage onto voltBoard
        Workspace.Children.Remove(DockStage);
        DockStage.ClearValue(Grid.RowProperty);
        DockStage.Margin = new Thickness(0);
        DockStage.HorizontalAlignment = HorizontalAlignment.Left;
        DockStage.VerticalAlignment = VerticalAlignment.Top;
        DockStage.Width = 1600;
        DockStage.Height = 900;
        Workspace.Children.Remove(FeedbackText);
        voltBoard.Children.Add(FeedbackText);
        voltBoard.Children.Add(DockStage);

        foreach (var d in docks) HookVoltDock(d);

        original.Visibility = Visibility.Collapsed;
        Surface.Children.Insert(0, voltBoard);
        SnapVoltLayout();
    }

    private void HookVoltDock(DockView d)
    {
        d.FocusButton.GotKeyboardFocus += (_, _) => { VoltSync(); };
    }

    private void SnapVoltLayout()
    {
        if (!IsVolt) return;
        var bounds = VoltBounds();
        for (int i = 0; i < docks.Length; i++)
        {
            docks[i].SetBounds(bounds[i], false, false);
        }
    }

    private DockBounds[] VoltBounds()
    {
        return new DockBounds[]
        {
            new(90, 155, 695, 310),
            new(815, 155, 695, 310),
            new(90, 480, 695, 310),
            new(815, 480, 695, 310)
        };
    }

    private void BuildVoltHero()
    {
        voltHeroBack = new Canvas { Width = 1600, Height = 900, ClipToBounds = true, Visibility = Visibility.Collapsed };

        // Radial background: #4a5b5e -> #1f2a2c -> #0c1213
        var radBg = new Rectangle
        {
            Width = 1600,
            Height = 900,
            Fill = new RadialGradientBrush
            {
                Center = new Point(0.35, 0.30),
                GradientOrigin = new Point(0.35, 0.30),
                RadiusX = 0.8,
                RadiusY = 0.9,
                GradientStops =
                {
                    new(VoltKit.C("#4a5b5e"), 0.0),
                    new(VoltKit.C("#1f2a2c"), 0.55),
                    new(VoltKit.C("#0c1213"), 1.0)
                }
            }
        };
        voltHeroBack.Children.Add(radBg);

        // Luminous curve lines (SVG arc paths)
        var pathGeo = new StreamGeometry();
        using (var ctx = pathGeo.Open())
        {
            ctx.BeginFigure(new Point(260, 900), false, false);
            ctx.BezierTo(new Point(420, 520), new Point(760, 230), new Point(1240, 120), true, true);
        }
        pathGeo.Freeze();

        var pathLine = new System.Windows.Shapes.Path
        {
            Data = pathGeo,
            Stroke = VoltKit.B("rgba(255,255,255,.55)"),
            StrokeThickness = 3
        };
        voltHeroBack.Children.Add(pathLine);

        // Secondary glowing amber arc
        var amberArcGeo = new StreamGeometry();
        using (var ctx = amberArcGeo.Open())
        {
            ctx.BeginFigure(new Point(300, 900), false, false);
            ctx.BezierTo(new Point(470, 560), new Point(800, 270), new Point(1260, 160), true, true);
        }
        amberArcGeo.Freeze();
        var amberArc = new System.Windows.Shapes.Path
        {
            Data = amberArcGeo,
            Stroke = VoltKit.B("rgba(255,173,10,.45)"),
            StrokeThickness = 8,
            Effect = new BlurEffect { Radius = 10 }
        };
        voltHeroBack.Children.Add(amberArc);

        // Ambient floating golden dust particles
        var rnd = new Random(101);
        for (int i = 0; i < 40; i++)
        {
            double sx = rnd.Next(100, 1500);
            double sy = rnd.Next(40, 700);
            double ssz = rnd.Next(2, 6);
            double sop = 0.25 + rnd.NextDouble() * 0.5;
            var speck = new Ellipse
            {
                Width = ssz,
                Height = ssz,
                Fill = new SolidColorBrush(Color.FromArgb((byte)(sop * 255), 255, 220, 120)),
                Effect = new BlurEffect { Radius = 1.5 }
            };
            Canvas.SetLeft(speck, sx);
            Canvas.SetTop(speck, sy);
            voltHeroBack.Children.Add(speck);
        }

        // Controller art container at (640, 40)
        voltHeroArtContainer = new Canvas { Width = 1040, Height = 700 };
        Canvas.SetLeft(voltHeroArtContainer, 640);
        Canvas.SetTop(voltHeroArtContainer, 40);
        voltHeroBack.Children.Add(voltHeroArtContainer);

        // Dark gradient on left for typography readability
        var leftShade = new Rectangle
        {
            Width = 1600,
            Height = 900,
            Fill = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0.5),
                EndPoint = new Point(1, 0.5),
                GradientStops =
                {
                    new(VoltKit.C("rgba(12,18,19,.85)"), 0.0),
                    new(VoltKit.C("rgba(12,18,19,.40)"), 0.45),
                    new(Colors.Transparent, 0.70)
                }
            }
        };
        voltHeroBack.Children.Add(leftShade);

        // Top bar
        voltHeroBack.Children.Add(VoltKit.TopBar());

        // Giant Serif Logo at (96, 380)
        voltHeroLogoStack = new StackPanel { Orientation = Orientation.Vertical, Width = 460 };
        voltHeroBrandText = new TextBlock
        {
            Text = "— EASYSMX —",
            FontFamily = VoltKit.SerifTitle,
            FontSize = 26,
            FontWeight = FontWeights.Normal,
            Foreground = VoltKit.WhiteBrush,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        voltHeroLogoStack.Children.Add(voltHeroBrandText);

        voltHeroModelText = new TextBlock
        {
            Text = "DUNE",
            FontFamily = VoltKit.SerifTitle,
            FontSize = 138,
            FontWeight = FontWeights.Bold,
            Foreground = VoltKit.WhiteBrush,
            LineHeight = 110,
            HorizontalAlignment = HorizontalAlignment.Center,
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 24, ShadowDepth = 6, Opacity = 0.6 }
        };
        // a long model name ("X20 PRO") shrinks to the column instead of being cut off
        voltHeroLogoStack.Children.Add(new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, Child = voltHeroModelText });

        voltHeroSubText = new TextBlock
        {
            Text = "D15 WIRELESS",
            FontFamily = VoltKit.SerifTitle,
            FontSize = 24,
            Foreground = VoltKit.B("#d0d0d0"),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 4, 0, 0)
        };
        voltHeroLogoStack.Children.Add(voltHeroSubText);

        Canvas.SetLeft(voltHeroLogoStack, 80);
        Canvas.SetTop(voltHeroLogoStack, 380);
        voltHeroBack.Children.Add(voltHeroLogoStack);

        // Full-width dark band across bottom at (0, 706, h: 116)
        var band = new Border
        {
            Width = 1600,
            Height = 116,
            Background = VoltKit.B("rgba(36,36,36,.82)"),
            BorderThickness = new Thickness(0, 1, 0, 1),
            BorderBrush = VoltKit.B("rgba(255,255,255,.08)")
        };
        Canvas.SetLeft(band, 0);
        Canvas.SetTop(band, 706);
        voltHeroBack.Children.Add(band);

        // Solid orange action button at (58, 726, w: 416, h: 76): [▶ Open Studio]
        var openBtn = VoltKit.OrangeActionButton("Open Studio", 416, 76, () => ControllerActionRequested?.Invoke(voltFocusedIndex, false));
        Canvas.SetLeft(openBtn, 58);
        Canvas.SetTop(openBtn, 726);
        voltHeroBack.Children.Add(openBtn);

        // Status telemetry: CURRENT SETUP, CONNECTION
        voltHeroTelemetryRow = new StackPanel { Orientation = Orientation.Horizontal };
        Canvas.SetLeft(voltHeroTelemetryRow, 510);
        Canvas.SetTop(voltHeroTelemetryRow, 734);
        voltHeroBack.Children.Add(voltHeroTelemetryRow);

        // 4 square tool tiles at x = 1196, 1286, 1376, 1466, top: 728
        var heroTools = new[] { "\uE7FC", "\uE71D", "\uE74D", "\uE713" };
        for (int i = 0; i < heroTools.Length; i++)
        {
            var sq = new Border
            {
                Width = 72,
                Height = 72,
                CornerRadius = new CornerRadius(10),
                Background = VoltKit.B("#3a3a3a"),
                Cursor = Cursors.Hand,
                Child = new TextBlock
                {
                    Text = heroTools[i],
                    FontFamily = VoltKit.Icons,
                    FontSize = 30,
                    Foreground = VoltKit.WhiteBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            Canvas.SetLeft(sq, 1196 + i * 90);
            Canvas.SetTop(sq, 728);
            voltHeroBack.Children.Add(sq);
        }

        // Chevrons
        var cL = VoltKit.Chevron(false, async () => await VoltFocus(null));
        Canvas.SetLeft(cL, 28);
        Canvas.SetTop(cL, 452);
        voltHeroBack.Children.Add(cL);

        var cR = VoltKit.Chevron(true, async () => await VoltFocus(null));
        Canvas.SetLeft(cR, 1518);
        Canvas.SetTop(cR, 452);
        voltHeroBack.Children.Add(cR);

        // Bottom bar: (A) SELECT, (B) BACK
        var bar = VoltKit.BottomBar(new[] { ("A", "SELECT"), ("B", "BACK") }, true);
        bar.MouseLeftButtonUp += async (_, _) => await VoltFocus(null);
        Canvas.SetLeft(bar, 0);
        Canvas.SetTop(bar, 844);
        voltHeroBack.Children.Add(bar);
    }

    private void UpdateVoltHero(int index)
    {
        if (voltHeroArtContainer == null || voltHeroModelText == null || voltHeroTelemetryRow == null || voltHeroSubText == null) return;
        voltFocusedIndex = index;

        var player = State.Players.Count > index ? State.Players[index] : null;
        string modKey = index switch
        {
            0 => string.IsNullOrEmpty(player?.ModelId) || player?.ModelId == "x20" ? "dune" : player.ModelId,
            1 => string.IsNullOrEmpty(player?.ModelId) || player?.ModelId == "x15" ? "x20pro" : player.ModelId,
            _ => !string.IsNullOrEmpty(player?.ModelId) ? player.ModelId : "dune"
        };

        string displayName = modKey switch
        {
            "dune" => "DUNE",
            "x20pro" or "x20_pro" => "X20 PRO",
            "x05pro" or "x05_pro" => "X05 PRO",
            "x10" => "X10",
            "x20" => "X20",
            "x15" or "x15-starfield" => "X15",
            _ => (player?.Model ?? "CONTROLLER").Replace("EasySMX ", "").ToUpperInvariant()
        };

        voltHeroModelText.Text = displayName;
        voltHeroSubText.Text = modKey switch
        {
            "dune" => "D15 WIRELESS",
            "x20pro" or "x20_pro" => "PRO ESPORTS",
            _ => "WIRELESS CONTROLLER"
        };

        voltHeroArtContainer.Children.Clear();
        var padArt = VoltKit.ControllerArt(modKey, 1040);
        if (padArt != null)
        {
            padArt.Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 60, ShadowDepth = 30, Opacity = 0.65 };
            voltHeroArtContainer.Children.Add(padArt);
        }

        voltHeroTelemetryRow.Children.Clear();
        var stats = new (string Lab, string Val)[]
        {
            ("CURRENT SETUP", "LOCAL DRAFT"),
            ("CONNECTION", "NOT REPORTED")
        };

        foreach (var (lab, val) in stats)
        {
            var stStack = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 0, 56, 0) };
            stStack.Children.Add(new TextBlock
            {
                Text = lab,
                FontFamily = VoltKit.Display,
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = VoltKit.B("#d2d2d2"),
                Margin = new Thickness(0, 0, 0, 5)
            });
            stStack.Children.Add(new TextBlock
            {
                Text = val,
                FontFamily = VoltKit.Display,
                FontSize = 26,
                FontWeight = FontWeights.SemiBold,
                Foreground = VoltKit.WhiteBrush
            });
            voltHeroTelemetryRow.Children.Add(stStack);
        }
    }

    private async Task VoltFocus(int? index)
    {
        if (index.HasValue)
        {
            voltFocusedIndex = index.Value;
            State.Activate(index.Value);
        }
        else
        {
            State.Collapse();
        }
        VoltSync();
        await Task.CompletedTask;
    }

    private void VoltSync()
    {
        if (!IsVolt || voltBoard == null) return;
        int? focused = State.FocusedPlayer;
        if (focused.HasValue)
        {
            voltZoneBack!.Visibility = Visibility.Collapsed;
            voltHeroBack!.Visibility = Visibility.Visible;
            UpdateVoltHero(focused.Value);
            for (int i = 0; i < docks.Length; i++) docks[i].Visibility = Visibility.Collapsed;
        }
        else
        {
            voltHeroBack!.Visibility = Visibility.Collapsed;
            voltZoneBack!.Visibility = Visibility.Visible;
            var bounds = VoltBounds();
            for (int i = 0; i < docks.Length; i++)
            {
                docks[i].Visibility = Visibility.Visible;
                docks[i].SetBounds(bounds[i], false, false);
            }
        }
    }
}
