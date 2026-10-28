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
using X20Ctl.Product.Blossom;
using X20Ctl.Zone;

namespace X20Ctl.Product;

/// <summary>
/// Controller Zone and Hero presentation for the Yozakura (Blossom) theme:
/// "One lantern per player hangs over the garden; it is lit when a controller sits in its light.
/// Hero view: giant full moon rising over the trees, calligraphic warrior mark, shoji-framed action rows,
/// and the pad resting on a glowing moonlight pedestal under weeping cherry branches."
/// Strictly implements t_blossom.py:zone() and t_blossom.py:hero().
/// </summary>
public partial class ZoneScene
{
    internal bool IsBlossom => ThemeCatalog.Current.Id == "blossom";

    private Grid? blossomBoard;
    private Canvas? blossomZoneBack, blossomHeroBack;
    private int blossomFocusedIndex = 0;

    // References to dynamically updated Hero elements
    private TextBlock? blossomHeroShortText, blossomHeroNameText, blossomHeroFinishText;
    private Canvas? blossomHeroArtContainer;
    private StackPanel? blossomHeroActionList;

    private void BuildBlossom()
    {
        var original = Surface.Children.OfType<Grid>().First();
        EnvironmentBackdrop.Visibility = Visibility.Collapsed;
        OuterFrame.Background = BlossomKit.VoidBgBrush;
        OuterFrame.BorderBrush = BlossomKit.VoidBgBrush;

        blossomBoard = new Grid
        {
            Width = 1600,
            Height = 900,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            ClipToBounds = true
        };

        // -------------------------------------------------------------
        // 1. Zone Overview Stage
        // -------------------------------------------------------------
        blossomZoneBack = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };

        // Starry night sky gradient
        var sky = new Rectangle { Width = 1600, Height = 900, Fill = BlossomKit.CreateSkyBackdrop() };
        blossomZoneBack.Children.Add(sky);

        // Top weeping cherry canopy branches
        blossomZoneBack.Children.Add(BlossomKit.CanopyBranch("top", 31));

        // Drifting sakura petals
        blossomZoneBack.Children.Add(BlossomKit.DriftingPetals(new Rect(100, 120, 1440, 660), 24, 33));

        // Festival lantern suspension wire
        var wireGeom = new PathGeometry();
        var wireFig = new PathFigure { StartPoint = new Point(-20, 150) };
        wireFig.Segments.Add(new BezierSegment(new Point(400, 194), new Point(1200, 194), new Point(1620, 150), true));
        wireGeom.Figures.Add(wireFig);
        var wirePath = new System.Windows.Shapes.Path
        {
            Data = wireGeom,
            Stroke = BlossomKit.B("#4a2c38"),
            StrokeThickness = 2.4
        };
        blossomZoneBack.Children.Add(wirePath);

        // Top Title "Controller Zone"
        var titleTb = new TextBlock
        {
            Text = "Controller Zone",
            FontFamily = BlossomKit.Display,
            FontSize = 48,
            Foreground = BlossomKit.TextWhiteBrush
        };
        Canvas.SetLeft(titleTb, 80);
        Canvas.SetTop(titleTb, 34);
        blossomZoneBack.Children.Add(titleTb);

        // Status Header
        var statusStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        statusStack.Children.Add(new TextBlock
        {
            Text = "Player 1",
            FontFamily = BlossomKit.Body,
            FontSize = 16,
            Foreground = BlossomKit.B("#e3cfd6"),
            Margin = new Thickness(0, 0, 14, 0)
        });
        statusStack.Children.Add(new TextBlock
        {
            Text = "19:24",
            FontFamily = BlossomKit.Body,
            FontSize = 16,
            Foreground = BlossomKit.TextMuteBrush,
            Margin = new Thickness(0, 0, 14, 0)
        });
        statusStack.Children.Add(new TextBlock
        {
            Text = "\uE83F",
            FontFamily = BlossomKit.Icons,
            FontSize = 18,
            Foreground = BlossomKit.B("#e3cfd6")
        });
        Canvas.SetLeft(statusStack, 700);
        Canvas.SetTop(statusStack, 52);
        blossomZoneBack.Children.Add(statusStack);

        // Bottom Selected Player Banner
        var banner = new Border
        {
            Width = 540,
            Height = 52,
            CornerRadius = new CornerRadius(4),
            Background = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0.5),
                EndPoint = new Point(1, 0.5),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb(90, 255, 176, 110), 0.0),
                    new GradientStop(Color.FromArgb(30, 255, 120, 140), 0.6),
                    new GradientStop(Colors.Transparent, 1.0)
                }
            },
            BorderBrush = BlossomKit.B("#ffb56b"),
            BorderThickness = new Thickness(3, 0, 0, 0),
            Padding = new Thickness(22, 0, 18, 0)
        };
        var bannerTb = new TextBlock
        {
            Text = "Player 1 · EasySMX X20 Pro · Obsidian",
            FontFamily = BlossomKit.Body,
            FontSize = 19,
            Foreground = BlossomKit.B("#ffe6cc"),
            VerticalAlignment = VerticalAlignment.Center
        };
        banner.Child = bannerTb;
        Canvas.SetLeft(banner, 80);
        Canvas.SetTop(banner, 772);
        blossomZoneBack.Children.Add(banner);

        // -------------------------------------------------------------
        // 2. Hero Presentation Stage
        // -------------------------------------------------------------
        blossomHeroBack = new Canvas { Width = 1600, Height = 900, ClipToBounds = true, Visibility = Visibility.Collapsed };

        // Sky backdrop
        var heroSky = new Rectangle { Width = 1600, Height = 900, Fill = BlossomKit.CreateSkyBackdrop() };
        blossomHeroBack.Children.Add(heroSky);

        // Full Moon in top right
        var moon = BlossomKit.MoonSphere(300);
        Canvas.SetLeft(moon, 1280);
        Canvas.SetTop(moon, -60);
        blossomHeroBack.Children.Add(moon);

        // Weeping cherry canopy in corner
        blossomHeroBack.Children.Add(BlossomKit.CanopyBranch("corner", 41));

        // Drifting petals
        blossomHeroBack.Children.Add(BlossomKit.DriftingPetals(new Rect(600, 140, 960, 680), 20, 43));

        // Breadcrumb Navigation
        var breadcrumb = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Cursor = Cursors.Hand
        };
        breadcrumb.Children.Add(new TextBlock
        {
            Text = "\uE72B",
            FontFamily = BlossomKit.Icons,
            FontSize = 14,
            Foreground = BlossomKit.B("#e3cfd6"),
            Margin = new Thickness(0, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        breadcrumb.Children.Add(new TextBlock
        {
            Text = "Controller Zone / Player 1",
            FontFamily = BlossomKit.Body,
            FontSize = 17,
            Foreground = BlossomKit.B("#e3cfd6"),
            VerticalAlignment = VerticalAlignment.Center
        });
        breadcrumb.MouseLeftButtonUp += async (_, _) => await BlossomFocus(null);
        Canvas.SetLeft(breadcrumb, 80);
        Canvas.SetTop(breadcrumb, 40);
        blossomHeroBack.Children.Add(breadcrumb);

        // Controller Art container on right
        blossomHeroArtContainer = new Canvas { Width = 800, Height = 700 };
        Canvas.SetLeft(blossomHeroArtContainer, 740);
        Canvas.SetTop(blossomHeroArtContainer, 100);
        blossomHeroBack.Children.Add(blossomHeroArtContainer);

        // Hero Left Calligraphic Title Stack
        var heroTitleStack = new StackPanel
        {
            Width = 520,
            Orientation = Orientation.Vertical
        };

        var playerTag = new TextBlock
        {
            Text = "PLAYER 1",
            FontFamily = BlossomKit.Small,
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Foreground = BlossomKit.TextSakuraBrush
        };
        heroTitleStack.Children.Add(playerTag);

        blossomHeroShortText = new TextBlock
        {
            Text = "X20",
            FontFamily = BlossomKit.Display,
            FontSize = 210,
            FontWeight = FontWeights.Normal,
            Foreground = BlossomKit.B("#ffe6cc"),
            LineHeight = 180,
            Margin = new Thickness(0, -10, 0, 10),
            Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(0xff, 0x8c, 0xaa),
                BlurRadius = 40,
                ShadowDepth = 0,
                Opacity = 0.45
            }
        };
        heroTitleStack.Children.Add(blossomHeroShortText);

        var modelLine = new StackPanel { Orientation = Orientation.Horizontal };
        blossomHeroNameText = new TextBlock
        {
            Text = "EasySMX X20 Pro · ",
            FontFamily = BlossomKit.Heading,
            FontSize = 26,
            Foreground = BlossomKit.TextWhiteBrush
        };
        blossomHeroFinishText = new TextBlock
        {
            Text = "Obsidian",
            FontFamily = BlossomKit.Heading,
            FontStyle = FontStyles.Italic,
            FontSize = 26,
            Foreground = BlossomKit.TextSakuraBrush
        };
        modelLine.Children.Add(blossomHeroNameText);
        modelLine.Children.Add(blossomHeroFinishText);
        heroTitleStack.Children.Add(modelLine);

        var telemetryTb = new TextBlock
        {
            Text = "Not detected · Battery not reported · No local changes",
            FontFamily = BlossomKit.Body,
            FontSize = 15,
            Foreground = BlossomKit.TextMuteBrush,
            Margin = new Thickness(0, 6, 0, 0)
        };
        heroTitleStack.Children.Add(telemetryTb);

        Canvas.SetLeft(heroTitleStack, 80);
        Canvas.SetTop(heroTitleStack, 120);
        blossomHeroBack.Children.Add(heroTitleStack);

        // Shoji Action List
        blossomHeroActionList = new StackPanel
        {
            Width = 460,
            Orientation = Orientation.Vertical
        };
        Canvas.SetLeft(blossomHeroActionList, 80);
        Canvas.SetTop(blossomHeroActionList, 478);
        blossomHeroBack.Children.Add(blossomHeroActionList);

        // Add both stages into board
        blossomBoard.Children.Add(blossomZoneBack);
        blossomBoard.Children.Add(blossomHeroBack);

        // Move DockStage onto blossomBoard
        Workspace.Children.Remove(DockStage);
        DockStage.ClearValue(Grid.RowProperty);
        DockStage.Margin = new Thickness(0);
        DockStage.HorizontalAlignment = HorizontalAlignment.Left;
        DockStage.VerticalAlignment = VerticalAlignment.Top;
        DockStage.Width = 1600;
        DockStage.Height = 900;
        Workspace.Children.Remove(FeedbackText);
        blossomBoard.Children.Add(FeedbackText);
        blossomBoard.Children.Add(DockStage);

        original.Visibility = Visibility.Collapsed;
        Surface.Children.Insert(0, blossomBoard);
    }

    private void UpdateBlossomHero(int index)
    {
        if (blossomHeroArtContainer == null || blossomHeroActionList == null) return;

        var player = State.Players.ElementAtOrDefault(index);
        string modKey = player?.ModelId ?? "x20";
        string shortName = modKey.ToUpperInvariant().Replace("_PRO", "").Replace("PRO", "");
        if (shortName.StartsWith("X")) { } else shortName = "X20";

        if (blossomHeroShortText != null) blossomHeroShortText.Text = shortName;
        if (blossomHeroNameText != null) blossomHeroNameText.Text = $"{player?.Model ?? "EasySMX X20 Pro"} · ";
        if (blossomHeroFinishText != null) blossomHeroFinishText.Text = modKey.Contains("black") ? "Obsidian" : "Cherry Blossom";

        // Controller Art on right with warm moonlight glow
        blossomHeroArtContainer.Children.Clear();

        var pedestalGlow = new Ellipse
        {
            Width = 720,
            Height = 140,
            Fill = new RadialGradientBrush
            {
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb(90, 255, 150, 110), 0.0),
                    new GradientStop(Colors.Transparent, 1.0)
                }
            },
            Effect = new BlurEffect { Radius = 16 }
        };
        Canvas.SetLeft(pedestalGlow, 40);
        Canvas.SetTop(pedestalGlow, 440);
        blossomHeroArtContainer.Children.Add(pedestalGlow);

        var art = BlossomKit.ControllerArt(modKey, 760);
        if (art != null)
        {
            art.Effect = new DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 34,
                ShadowDepth = 26,
                Opacity = 0.65
            };
            art.RenderTransform = new RotateTransform(-6);
            Canvas.SetLeft(art, 20);
            Canvas.SetTop(art, 80);
            blossomHeroArtContainer.Children.Add(art);
        }

        // Action Rows
        blossomHeroActionList.Children.Clear();
        var actions = new (string Icon, string Label, bool Primary, bool Destructive, Action Act)[]
        {
            ("A", "Open Studio", true, false, () => ControllerActionRequested?.Invoke(index, false)),
            ("\uE8AB", "Switch controller", false, false, () => ControllerActionRequested?.Invoke(index, true)),
            ("\uE790", "Change color", false, false, () => { }),
            ("\uE9D9", "Diagnostics", false, false, () => StudioPageRequested?.Invoke(index, "diagnostics")),
            ("\uE74D", "Remove controller", false, true, () => ControllerRemoved?.Invoke(index))
        };

        foreach (var act in actions)
        {
            var rowBorder = new Border
            {
                Height = 60,
                CornerRadius = new CornerRadius(4),
                Background = act.Primary
                    ? new LinearGradientBrush
                    {
                        StartPoint = new Point(0, 0.5),
                        EndPoint = new Point(1, 0.5),
                        GradientStops = new GradientStopCollection
                        {
                            new GradientStop(Color.FromArgb(80, 255, 176, 110), 0.0),
                            new GradientStop(Color.FromArgb(28, 255, 120, 140), 0.6),
                            new GradientStop(Colors.Transparent, 1.0)
                        }
                    }
                    : BlossomKit.B("transparent"),
                BorderBrush = act.Primary ? BlossomKit.B("#ffb56b") : BlossomKit.B("rgba(255,220,230,0.08)"),
                BorderThickness = act.Primary ? new Thickness(3, 0, 0, 0) : new Thickness(0, 0, 0, 1),
                Padding = new Thickness(16, 0, 18, 0),
                Cursor = Cursors.Hand
            };

            var rowStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            if (act.Primary)
            {
                var keyBadge = new Border
                {
                    Width = 28,
                    Height = 28,
                    CornerRadius = new CornerRadius(14),
                    Background = BlossomKit.CreateWarmLanternBrush(),
                    Margin = new Thickness(0, 0, 16, 0),
                    Effect = new DropShadowEffect { Color = Color.FromRgb(0xff, 0x96, 0x5a), BlurRadius = 14, ShadowDepth = 0, Opacity = 0.6 },
                    Child = new TextBlock
                    {
                        Text = "A",
                        FontFamily = BlossomKit.Display,
                        FontSize = 14,
                        FontWeight = FontWeights.Bold,
                        Foreground = BlossomKit.B("#4a140f"),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                };
                rowStack.Children.Add(keyBadge);
            }
            else
            {
                var iconTb = new TextBlock
                {
                    Text = act.Icon,
                    FontFamily = BlossomKit.Icons,
                    FontSize = 17,
                    Foreground = act.Destructive ? BlossomKit.B("#ff8f8f") : BlossomKit.TextMuteBrush,
                    Width = 28,
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 0, 16, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                rowStack.Children.Add(iconTb);
            }

            var labelTb = new TextBlock
            {
                Text = act.Label,
                FontFamily = BlossomKit.Body,
                FontSize = act.Primary ? 22 : 19,
                Foreground = act.Destructive ? BlossomKit.B("#ff8f8f") : (act.Primary ? BlossomKit.B("#ffe6cc") : BlossomKit.TextWhiteBrush),
                VerticalAlignment = VerticalAlignment.Center
            };
            rowStack.Children.Add(labelTb);

            rowBorder.Child = rowStack;
            rowBorder.MouseLeftButtonUp += (_, _) => act.Act();
            blossomHeroActionList.Children.Add(rowBorder);
        }
    }

    private DockBounds[] BlossomBounds()
    {
        return
        [
            new(80, 140, 320, 600),
            new(440, 140, 320, 600),
            new(800, 140, 320, 600),
            new(1160, 140, 320, 600)
        ];
    }

    private async Task BlossomFocus(int? index)
    {
        if (index.HasValue)
        {
            blossomFocusedIndex = index.Value;
            State.Activate(index.Value);
        }
        else
        {
            State.Collapse();
        }
        BlossomSync();
        await Task.CompletedTask;
    }

    private void BlossomSync()
    {
        if (!IsBlossom || blossomBoard == null) return;
        int? focused = State.FocusedPlayer;
        if (focused.HasValue)
        {
            blossomZoneBack!.Visibility = Visibility.Collapsed;
            blossomHeroBack!.Visibility = Visibility.Visible;
            UpdateBlossomHero(focused.Value);
            for (int i = 0; i < docks.Length; i++) docks[i].Visibility = Visibility.Collapsed;
        }
        else
        {
            blossomHeroBack!.Visibility = Visibility.Collapsed;
            blossomZoneBack!.Visibility = Visibility.Visible;
            var bounds = BlossomBounds();
            for (int i = 0; i < docks.Length; i++)
            {
                docks[i].Visibility = Visibility.Visible;
                docks[i].SetBounds(bounds[i], false, false);
            }
        }
    }
}
