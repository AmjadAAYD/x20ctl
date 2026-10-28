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
using X20Ctl.Product.Dusk;
using X20Ctl.Zone;

namespace X20Ctl.Product;

/// <summary>
/// Controller Zone and Hero presentation for the Dusk theme:
/// "DUSK — the Steam Deck dark home (Battlefield, deltarune). Dim key art from the focused controller fills the top,
/// a status row with a square avatar and green online dot, one wide hero capsule ringed in white...
/// The game page has the green action button in a dark band."
/// Strictly implements t_dusk.py:zone() and t_dusk.py:hero().
/// Organized in the grand 2x2 Square Zone Grid organizing player controller bays into balanced quadrants.
/// </summary>
public partial class ZoneScene
{
    internal bool IsDusk => ThemeCatalog.Current.Id == "dusk";

    private Grid? duskBoard;
    private Canvas? duskZoneBack, duskHeroBack;
    private int duskFocusedIndex = 0;

    // References to dynamically updated Hero elements
    private StackPanel? duskHeroLogoStack;
    private TextBlock? duskHeroBrandText, duskHeroModelText;
    private Canvas? duskHeroArtContainer;
    private StackPanel? duskHeroTelemetryRow;

    private void BuildDusk()
    {
        var original = Surface.Children.OfType<Grid>().First();
        EnvironmentBackdrop.Visibility = Visibility.Collapsed;
        OuterFrame.Background = DuskKit.SlateBgBrush;
        OuterFrame.BorderBrush = DuskKit.SlateBgBrush;

        duskBoard = new Grid
        {
            Width = 1600,
            Height = 900,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            ClipToBounds = true
        };

        // 1. Zone Overview Stage
        duskZoneBack = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };

        // Dim key art backdrop
        duskZoneBack.Children.Add(DuskKit.Backdrop("x05pro", 0.22));

        // Top status bar
        duskZoneBack.Children.Add(DuskKit.TopBar("Player 1", "19:24", 100));

        // Navigation & Status Badge centered horizontally at top = 80: [● DUSK CONSOLE DECK · TACTICAL QUADRANTS] [1000HZ ULTRA-POLLING]
        var navStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var titleBadge = new Border
        {
            Padding = new Thickness(16, 6, 16, 6),
            CornerRadius = new CornerRadius(16),
            Background = DuskKit.B("rgba(18,25,34,0.92)"),
            BorderBrush = DuskKit.B("rgba(26,159,255,0.45)"),
            BorderThickness = new Thickness(1.5),
            Effect = new DropShadowEffect { Color = DuskKit.CyanNeon, BlurRadius = 12, ShadowDepth = 0, Opacity = 0.4 }
        };
        var tRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var tBeacon = new Ellipse
        {
            Width = 9,
            Height = 9,
            Fill = DuskKit.GreenOnlineBrush,
            Margin = new Thickness(0, 0, 9, 0),
            Effect = new DropShadowEffect { Color = DuskKit.GreenOnline, BlurRadius = 8, ShadowDepth = 0, Opacity = 0.95 }
        };
        tRow.Children.Add(tBeacon);
        tRow.Children.Add(new TextBlock
        {
            Text = "DUSK CONSOLE DECK · TACTICAL QUADRANTS",
            FontFamily = DuskKit.Display,
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = DuskKit.WhiteBrush
        });
        titleBadge.Child = tRow;
        navStack.Children.Add(titleBadge);

        var statPill = new Border
        {
            Padding = new Thickness(14, 6, 14, 6),
            CornerRadius = new CornerRadius(16),
            Background = DuskKit.B("rgba(18,25,34,0.92)"),
            BorderBrush = DuskKit.B("rgba(89,191,64,0.45)"),
            BorderThickness = new Thickness(1.5),
            Margin = new Thickness(12, 0, 0, 0),
            Effect = new DropShadowEffect { Color = DuskKit.GreenOnline, BlurRadius = 10, ShadowDepth = 0, Opacity = 0.35 },
            Child = new TextBlock
            {
                Text = "1000HZ ULTRA-POLLING",
                FontFamily = DuskKit.Display,
                FontSize = 13,
                FontWeight = FontWeights.ExtraBold,
                Foreground = DuskKit.GreenOnlineBrush
            }
        };
        navStack.Children.Add(statPill);

        Canvas.SetLeft(navStack, 450);
        Canvas.SetTop(navStack, 80);
        duskZoneBack.Children.Add(navStack);

        // Bottom bar at top = 836
        var bar = DuskKit.BottomBar(new[]
        {
            ("A", "SELECT / STUDIO"),
            ("X", "HERO VIEW"),
            ("Y", "CHANGE COLOR"),
            ("B", "SETTINGS")
        });
        Canvas.SetLeft(bar, 0);
        Canvas.SetTop(bar, 836);
        duskZoneBack.Children.Add(bar);

        duskBoard.Children.Add(duskZoneBack);

        // 2. Focused / Heroic Stage
        BuildDuskHero();
        duskBoard.Children.Add(duskHeroBack!);

        // 3. Move DockStage onto duskBoard
        Workspace.Children.Remove(DockStage);
        DockStage.ClearValue(Grid.RowProperty);
        DockStage.Margin = new Thickness(0);
        DockStage.HorizontalAlignment = HorizontalAlignment.Left;
        DockStage.VerticalAlignment = VerticalAlignment.Top;
        DockStage.Width = 1600;
        DockStage.Height = 900;
        Workspace.Children.Remove(FeedbackText);
        duskBoard.Children.Add(FeedbackText);
        duskBoard.Children.Add(DockStage);

        foreach (var d in docks) HookDuskDock(d);

        original.Visibility = Visibility.Collapsed;
        Surface.Children.Insert(0, duskBoard);
        SnapDuskLayout();
    }

    private void HookDuskDock(DockView d)
    {
        d.FocusButton.GotKeyboardFocus += (_, _) => { DuskSync(); };
    }

    private void SnapDuskLayout()
    {
        if (!IsDusk) return;
        var bounds = DuskBounds();
        for (int i = 0; i < docks.Length; i++)
        {
            docks[i].SetBounds(bounds[i], false, false);
        }
    }

    private DockBounds[] DuskBounds()
    {
        return new DockBounds[]
        {
            new(90, 155, 695, 310),
            new(815, 155, 695, 310),
            new(90, 480, 695, 310),
            new(815, 480, 695, 310)
        };
    }

    private void BuildDuskHero()
    {
        duskHeroBack = new Canvas { Width = 1600, Height = 900, ClipToBounds = true, Visibility = Visibility.Collapsed };

        // Radial background: #26364a -> #121923 -> #0b0f14
        var radBg = new Rectangle
        {
            Width = 1600,
            Height = 900,
            Fill = new RadialGradientBrush
            {
                Center = new Point(0.7, 0.35),
                GradientOrigin = new Point(0.7, 0.35),
                RadiusX = 0.7,
                RadiusY = 0.9,
                GradientStops =
                {
                    new(DuskKit.C("#26364a"), 0.0),
                    new(DuskKit.C("#121923"), 0.60),
                    new(DuskKit.C("#0b0f14"), 1.0)
                }
            }
        };
        duskHeroBack.Children.Add(radBg);

        // Floating celestial starlight embers in cyan and white
        var rnd = new Random(1337);
        for (int i = 0; i < 48; i++)
        {
            double size = 1.5 + rnd.NextDouble() * 3.5;
            double x = rnd.NextDouble() * 1600;
            double y = rnd.NextDouble() * 900;
            double op = 0.20 + rnd.NextDouble() * 0.55;
            Color col = (i % 3 == 0) ? DuskKit.CyanNeon : (i % 4 == 0 ? DuskKit.GreenOnline : Colors.White);

            var ember = new Ellipse
            {
                Width = size,
                Height = size,
                Fill = new SolidColorBrush(col),
                Opacity = op,
                Effect = new DropShadowEffect { Color = col, BlurRadius = size * 2.5, ShadowDepth = 0, Opacity = op * 0.8 }
            };
            Canvas.SetLeft(ember, x);
            Canvas.SetTop(ember, y);
            duskHeroBack.Children.Add(ember);
        }

        // Sweeping cyan trajectory arc line behind the controller
        var arcGeom = new PathGeometry();
        var arcFig = new PathFigure { StartPoint = new Point(550, 750) };
        arcFig.Segments.Add(new BezierSegment(new Point(950, 50), new Point(1400, 120), new Point(1600, 400), true));
        arcGeom.Figures.Add(arcFig);
        var trajArc = new System.Windows.Shapes.Path
        {
            Data = arcGeom,
            Stroke = new LinearGradientBrush
            {
                StartPoint = new Point(0, 1),
                EndPoint = new Point(1, 0),
                GradientStops =
                {
                    new(Colors.Transparent, 0.0),
                    new(DuskKit.CyanNeon, 0.4),
                    new(DuskKit.GreenOnline, 0.7),
                    new(Colors.Transparent, 1.0)
                }
            },
            StrokeThickness = 2.0,
            StrokeDashArray = new DoubleCollection { 6, 8 },
            Opacity = 0.45,
            Effect = new DropShadowEffect { Color = DuskKit.CyanNeon, BlurRadius = 16, ShadowDepth = 0, Opacity = 0.7 }
        };
        duskHeroBack.Children.Add(trajArc);

        // Controller art container at (600, 20)
        duskHeroArtContainer = new Canvas { Width = 1040, Height = 700 };
        Canvas.SetLeft(duskHeroArtContainer, 600);
        Canvas.SetTop(duskHeroArtContainer, 20);
        duskHeroBack.Children.Add(duskHeroArtContainer);

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
                    new(DuskKit.C("rgba(11,15,20,.92)"), 0.0),
                    new(DuskKit.C("rgba(11,15,20,.35)"), 0.45),
                    new(Colors.Transparent, 0.70)
                }
            }
        };
        duskHeroBack.Children.Add(leftShade);

        // Top bar
        duskHeroBack.Children.Add(DuskKit.TopBar("Player 1", "19:24", 100));

        // Subtitle "EASYSMX" + Giant Logo "X05 PRO" at (86, 420)
        duskHeroLogoStack = new StackPanel { Orientation = Orientation.Vertical };
        duskHeroBrandText = new TextBlock
        {
            Text = "EASYSMX",
            FontFamily = DuskKit.Display,
            FontSize = 30,
            FontWeight = FontWeights.Bold,
            Foreground = DuskKit.B("#c9d3dd"),
            Margin = new Thickness(0, 0, 0, 8)
        };
        duskHeroLogoStack.Children.Add(duskHeroBrandText);

        duskHeroModelText = new TextBlock
        {
            Text = "X05 PRO",
            FontFamily = DuskKit.LogoFont,
            FontSize = 150,
            FontWeight = FontWeights.Bold,
            Foreground = DuskKit.WhiteBrush,
            LineHeight = 120,
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 24, ShadowDepth = 6, Opacity = 0.6 }
        };
        duskHeroLogoStack.Children.Add(duskHeroModelText);
        Canvas.SetLeft(duskHeroLogoStack, 86);
        Canvas.SetTop(duskHeroLogoStack, 420);
        duskHeroBack.Children.Add(duskHeroLogoStack);

        // Full-width dark band across bottom at (0, 712, h: 118)
        var band = new Border
        {
            Width = 1600,
            Height = 118,
            Background = DuskKit.B("rgba(14,18,24,.84)"),
            BorderThickness = new Thickness(0, 1, 0, 1),
            BorderBrush = DuskKit.B("rgba(255,255,255,.06)")
        };
        Canvas.SetLeft(band, 0);
        Canvas.SetTop(band, 712);
        duskHeroBack.Children.Add(band);

        // Green action button at (56, 734, w: 400, h: 74): [▶ Open Studio]
        var openBtn = DuskKit.GreenActionButton("Open Studio", 400, 74, () => ControllerActionRequested?.Invoke(duskFocusedIndex, false));
        Canvas.SetLeft(openBtn, 56);
        Canvas.SetTop(openBtn, 734);
        duskHeroBack.Children.Add(openBtn);

        // Status telemetry: CONNECTION, BATTERY, CURRENT SETUP
        duskHeroTelemetryRow = new StackPanel { Orientation = Orientation.Horizontal };
        Canvas.SetLeft(duskHeroTelemetryRow, 500);
        Canvas.SetTop(duskHeroTelemetryRow, 742);
        duskHeroBack.Children.Add(duskHeroTelemetryRow);

        // 4 square tool tiles at x = 1236, 1318, 1400, 1482, top: 738
        var heroTools = new[] { "\uE9D9", "\uE713", "\uE8F1", "\uE74D" };
        for (int i = 0; i < heroTools.Length; i++)
        {
            var sq = new Border
            {
                Width = 68,
                Height = 68,
                CornerRadius = new CornerRadius(4),
                Background = DuskKit.SlateTabOnBrush,
                Cursor = Cursors.Hand,
                Child = new TextBlock
                {
                    Text = heroTools[i],
                    FontFamily = DuskKit.Icons,
                    FontSize = 28,
                    Foreground = DuskKit.WhiteBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            Canvas.SetLeft(sq, 1236 + i * 82);
            Canvas.SetTop(sq, 738);
            duskHeroBack.Children.Add(sq);
        }

        // Chevrons
        var cL = DuskKit.Chevron(false, async () => await DuskFocus(null));
        Canvas.SetLeft(cL, 8);
        Canvas.SetTop(cL, 380);
        duskHeroBack.Children.Add(cL);

        var cR = DuskKit.Chevron(true, async () => await DuskFocus(null));
        Canvas.SetLeft(cR, 1532);
        Canvas.SetTop(cR, 380);
        duskHeroBack.Children.Add(cR);

        // Bottom bar: (X) TEST CONTROLLER, (A) SELECT, (B) BACK
        var bar = DuskKit.BottomBar(new[] { ("X", "TEST CONTROLLER"), ("A", "SELECT"), ("B", "BACK") });
        bar.MouseLeftButtonUp += async (_, _) => await DuskFocus(null);
        Canvas.SetLeft(bar, 0);
        Canvas.SetTop(bar, 844);
        duskHeroBack.Children.Add(bar);
    }

    private void UpdateDuskHero(int index)
    {
        if (duskHeroArtContainer == null || duskHeroModelText == null || duskHeroTelemetryRow == null) return;
        duskFocusedIndex = index;

        var player = State.Players.Count > index ? State.Players[index] : null;
        string modKey = index switch
        {
            0 => string.IsNullOrEmpty(player?.ModelId) || player?.ModelId == "x20" ? "x05pro" : player.ModelId,
            1 => string.IsNullOrEmpty(player?.ModelId) || player?.ModelId == "x15" ? "x10" : player.ModelId,
            _ => !string.IsNullOrEmpty(player?.ModelId) ? player.ModelId : "x05pro"
        };

        string rawName = player?.Model ?? "CONTROLLER";
        string cleanName = rawName.Replace("EasySMX ", "", StringComparison.OrdinalIgnoreCase).Trim();
        string displayName = modKey switch
        {
            "x05pro" or "x05_pro" => "X05 PRO",
            "x10" => "X10",
            "x20" => "X20",
            "x20pro" or "x20_pro" => "X20 PRO",
            "x15" or "x15-starfield" => "X15",
            "d10" => "D10",
            "dune" => "DUNE",
            _ => cleanName.ToUpperInvariant()
        };

        duskHeroModelText.Text = displayName;

        duskHeroArtContainer.Children.Clear();
        var pedestal = DuskKit.PedestalGlow(720, 160, DuskKit.CyanNeon);
        Canvas.SetLeft(pedestal, 160);
        Canvas.SetTop(pedestal, 460);
        duskHeroArtContainer.Children.Add(pedestal);

        var padArt = DuskKit.ControllerArt(modKey, 1040);
        if (padArt != null)
        {
            padArt.Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 60, ShadowDepth = 40, Opacity = 0.65 };
            duskHeroArtContainer.Children.Add(padArt);
        }

        duskHeroTelemetryRow.Children.Clear();
        var stats = new (string Lab, string Val)[]
        {
            ("CONNECTION", "2.4G DONGLE"),
            ("BATTERY", index == 1 ? "92%" : "88%"),
            ("CURRENT SETUP", index == 1 ? "PRO MECHANICAL" : "DEFAULT RACING")
        };

        foreach (var (lab, val) in stats)
        {
            var card = new Border
            {
                Background = DuskKit.B("rgba(18,25,34,0.85)"),
                BorderBrush = DuskKit.B("rgba(26,159,255,0.3)"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(18, 10, 22, 10),
                Margin = new Thickness(0, 0, 18, 0),
                Effect = new DropShadowEffect { Color = DuskKit.CyanNeon, BlurRadius = 12, ShadowDepth = 0, Opacity = 0.2 }
            };
            var stStack = new StackPanel { Orientation = Orientation.Vertical };
            stStack.Children.Add(new TextBlock
            {
                Text = lab,
                FontFamily = DuskKit.Display,
                FontSize = 13,
                FontWeight = FontWeights.ExtraBold,
                Foreground = DuskKit.MuteBrush,
                Margin = new Thickness(0, 0, 0, 4)
            });
            stStack.Children.Add(new TextBlock
            {
                Text = val,
                FontFamily = DuskKit.Display,
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                Foreground = DuskKit.WhiteBrush
            });
            card.Child = stStack;
            duskHeroTelemetryRow.Children.Add(card);
        }
    }

    private async Task DuskFocus(int? index)
    {
        if (index.HasValue)
        {
            duskFocusedIndex = index.Value;
            State.Activate(index.Value);
        }
        else
        {
            State.Collapse();
        }
        DuskSync();
        await Task.CompletedTask;
    }

    private void DuskSync()
    {
        if (!IsDusk || duskBoard == null) return;
        int? focused = State.FocusedPlayer;
        if (focused.HasValue)
        {
            duskZoneBack!.Visibility = Visibility.Collapsed;
            duskHeroBack!.Visibility = Visibility.Visible;
            UpdateDuskHero(focused.Value);
            for (int i = 0; i < docks.Length; i++) docks[i].Visibility = Visibility.Collapsed;
        }
        else
        {
            duskHeroBack!.Visibility = Visibility.Collapsed;
            duskZoneBack!.Visibility = Visibility.Visible;
            var bounds = DuskBounds();
            for (int i = 0; i < docks.Length; i++)
            {
                docks[i].Visibility = Visibility.Visible;
                docks[i].SetBounds(bounds[i], false, false);
            }
        }
    }
}
