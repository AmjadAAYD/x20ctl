using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using X20Ctl.Product.Nexus;
using X20Ctl.Zone;

namespace X20Ctl.Product;

/// <summary>
/// Controller Zone and Hero presentation for the Nexus theme:
/// "The Xbox home: key-art band, top bar, dark graphite tiles, and green focus."
/// Strictly implements t_nexus.py:64 zone() and t_nexus.py:80 hero().
/// </summary>
public partial class ZoneScene
{
    internal bool IsNexus { get; private set; }
    private static readonly double[] NexusXs = [68, 440, 812, 1184];

    private Grid? nexusBoard;
    private Canvas? nexusZoneBack, nexusHeroBack, nexusOverlay;

    private void BuildNexus()
    {
        IsNexus = true;
        var original = Surface.Children.OfType<Grid>().First();
        EnvironmentBackdrop.Visibility = Visibility.Collapsed;
        OuterFrame.Background = NexusKit.HouseBrush;
        OuterFrame.BorderBrush = NexusKit.HouseBrush;

        nexusBoard = new Grid
        {
            Width = 1600,
            Height = 900,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            ClipToBounds = true
        };

        // 1. Zone Overview Stage
        nexusZoneBack = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };
        nexusZoneBack.Children.Add(NexusKit.KeyArtBand(560, 0.55, featModel: "x20pro", secondModel: "x15-starfield"));

        // Dark gradient tint over keyart
        var tint = new Rectangle
        {
            Width = 1600,
            Height = 900,
            Fill = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0.5),
                EndPoint = new Point(1, 0.5),
                GradientStops =
                {
                    new(NexusKit.A(NexusKit.House, 0.90), 0.0),
                    new(NexusKit.A(NexusKit.House, 0.35), 0.5),
                    new(NexusKit.A(NexusKit.House, 0.60), 1.0)
                }
            }
        };
        nexusZoneBack.Children.Add(tint);

        // Top bar
        var topBar = NexusKit.TopBar(onTools: () => SettingsRequested?.Invoke());
        Canvas.SetLeft(topBar, 62);
        Canvas.SetTop(topBar, 46);
        nexusZoneBack.Children.Add(topBar);

        // Titles at (68, 150)
        var titleEl = new TextBlock
        {
            Text = "Controller Zone",
            FontFamily = NexusKit.Display,
            FontSize = 40,
            FontWeight = FontWeights.SemiBold,
            Foreground = NexusKit.WhiteBrush
        };
        Canvas.SetLeft(titleEl, 68);
        Canvas.SetTop(titleEl, 150);
        nexusZoneBack.Children.Add(titleEl);

        var leadEl = new TextBlock
        {
            Text = "Select player · connection checked separately",
            FontFamily = NexusKit.Body,
            FontSize = 18,
            Foreground = NexusKit.SubBrush
        };
        Canvas.SetLeft(leadEl, 70);
        Canvas.SetTop(leadEl, 204);
        nexusZoneBack.Children.Add(leadEl);

        // Bottom action buttons at (68, 700)
        var actionsRow = new StackPanel { Orientation = Orientation.Horizontal };
        var openBtn = NexusKit.TheatricalButton("Open Studio", primary: true, key: "A", onClick: () =>
        {
            int slot = State.FocusedPlayer ?? 0;
            ControllerActionRequested?.Invoke(slot, false);
        }, height: 62, fontSize: 19);
        openBtn.Width = 260;
        actionsRow.Children.Add(openBtn);

        var fourBtn = NexusKit.TheatricalButton("Show all four", primary: false, key: "X", onClick: () => { }, height: 62, fontSize: 19);
        fourBtn.Width = 260;
        fourBtn.Margin = new Thickness(16, 0, 0, 0);
        actionsRow.Children.Add(fourBtn);

        var toolsBtn = NexusKit.TheatricalButton("Tools", primary: false, key: "☰", onClick: () => SettingsRequested?.Invoke(), height: 62, fontSize: 19);
        toolsBtn.Width = 260;
        toolsBtn.Margin = new Thickness(16, 0, 0, 0);
        actionsRow.Children.Add(toolsBtn);

        Canvas.SetLeft(actionsRow, 68);
        Canvas.SetTop(actionsRow, 700);
        nexusZoneBack.Children.Add(actionsRow);

        nexusBoard.Children.Add(nexusZoneBack);

        // 2. Hero Stage
        nexusHeroBack = new Canvas { Width = 1600, Height = 900, ClipToBounds = true, Visibility = Visibility.Collapsed };
        BuildNexusHeroUI();
        nexusBoard.Children.Add(nexusHeroBack);

        // 3. Move DockStage onto nexusBoard
        Workspace.Children.Remove(DockStage);
        DockStage.ClearValue(Grid.RowProperty);
        DockStage.Margin = new Thickness(0);
        DockStage.HorizontalAlignment = HorizontalAlignment.Left;
        DockStage.VerticalAlignment = VerticalAlignment.Top;
        DockStage.Width = 1600;
        DockStage.Height = 900;
        Workspace.Children.Remove(FeedbackText);
        nexusBoard.Children.Add(FeedbackText);
        nexusBoard.Children.Add(DockStage);

        foreach (var d in docks) HookNexusDock(d);

        original.Visibility = Visibility.Collapsed;
        Surface.Children.Insert(0, nexusBoard);
        SnapNexusLayout();
    }

    private void HookNexusDock(DockView d)
    {
        d.FocusButton.GotKeyboardFocus += (_, _) => { NexusSync(); };
        d.BackRequested += () => _ = FocusPlayerAsync(null);
        d.PageRequested += page => StudioPageRequested?.Invoke(d.Index, page);
    }

    private void BuildNexusHeroUI()
    {
        if (nexusHeroBack == null) return;
        nexusHeroBack.Children.Clear();

        // 1. Radial gradient backdrop
        var heroBg = new Rectangle
        {
            Width = 1600,
            Height = 900,
            Fill = new RadialGradientBrush
            {
                Center = new Point(0.55, 0.35),
                RadiusX = 0.7,
                RadiusY = 0.8,
                GradientStops =
                {
                    new(NexusKit.C("#4b5562"), 0.0),
                    new(NexusKit.C("#1a1d22"), 0.60),
                    new(NexusKit.House, 1.0)
                }
            }
        };
        nexusHeroBack.Children.Add(heroBg);

        // Floor drop shadow ellipse at (420, 700, 760x70)
        var shadow = new Ellipse
        {
            Width = 760,
            Height = 70,
            Fill = new RadialGradientBrush
            {
                Center = new Point(0.5, 0.5),
                RadiusX = 0.5,
                RadiusY = 0.5,
                GradientStops =
                {
                    new(NexusKit.A(Colors.Black, 0.6), 0.0),
                    new(Colors.Transparent, 1.0)
                }
            }
        };
        Canvas.SetLeft(shadow, 420);
        Canvas.SetTop(shadow, 700);
        nexusHeroBack.Children.Add(shadow);

        // Hero Controller Art at (440, 220, w=720)
        var heroArt = NexusKit.ControllerArt("x20pro", 720);
        if (heroArt != null)
        {
            heroArt.Effect = new DropShadowEffect { BlurRadius = 40, ShadowDepth = 40, Direction = 270, Opacity = 0.6, Color = Colors.Black };
            Canvas.SetLeft(heroArt, 440);
            Canvas.SetTop(heroArt, 220);
            nexusHeroBack.Children.Add(heroArt);
        }

        // Top bar
        var topBar = NexusKit.TopBar(onTools: () => SettingsRequested?.Invoke());
        Canvas.SetLeft(topBar, 62);
        Canvas.SetTop(topBar, 46);
        nexusHeroBack.Children.Add(topBar);

        // Breadcrumb at (62, 136)
        var crumb = new TextBlock
        {
            Text = "\uE72B  Go Back  ·  Controller Zone / Player 1",
            FontFamily = NexusKit.Body,
            FontSize = 18,
            Foreground = NexusKit.WhiteBrush,
            Opacity = 0.9,
            Cursor = System.Windows.Input.Cursors.Hand
        };
        crumb.MouseLeftButtonDown += (_, _) => _ = FocusPlayerAsync(null);
        Canvas.SetLeft(crumb, 62);
        Canvas.SetTop(crumb, 136);
        nexusHeroBack.Children.Add(crumb);

        // Left Column at (68, 230, w = 330)
        var leftCol = new StackPanel { Width = 330 };
        leftCol.Children.Add(new TextBlock
        {
            Text = "PLAYER 1",
            FontFamily = NexusKit.Body,
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = NexusKit.GreenBrush
        });
        leftCol.Children.Add(new TextBlock
        {
            Text = "X20 Pro",
            FontFamily = NexusKit.Display,
            FontSize = 44,
            FontWeight = FontWeights.SemiBold,
            Foreground = NexusKit.WhiteBrush,
            Margin = new Thickness(0, 4, 0, 4)
        });
        leftCol.Children.Add(new TextBlock
        {
            Text = "Offline",
            FontFamily = NexusKit.Body,
            FontSize = 18,
            Foreground = NexusKit.SubBrush,
            Margin = new Thickness(0, 0, 0, 24)
        });

        // Status rows
        var statusItems = new[]
        {
            ("Current setup", "Current working settings"),
            ("Connection", "Not detected"),
            ("Battery", "Not reported"),
            ("Changes", "No local changes")
        };
        foreach (var (k, v) in statusItems)
        {
            var row = new Grid { Height = 46 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var keyEl = new TextBlock { Text = k, FontFamily = NexusKit.Body, FontSize = 16, Foreground = NexusKit.SubBrush, VerticalAlignment = VerticalAlignment.Center };
            var valEl = new TextBlock { Text = v, FontFamily = NexusKit.Body, FontSize = 16, Foreground = NexusKit.WhiteBrush, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(keyEl, 0); Grid.SetColumn(valEl, 1);
            row.Children.Add(keyEl); row.Children.Add(valEl);

            var line = new Rectangle { Height = 1, Fill = NexusKit.B(NexusKit.A(Colors.White, 0.08)), VerticalAlignment = VerticalAlignment.Bottom };
            Grid.SetColumnSpan(line, 2);
            row.Children.Add(line);

            leftCol.Children.Add(row);
        }

        Canvas.SetLeft(leftCol, 68);
        Canvas.SetTop(leftCol, 230);
        nexusHeroBack.Children.Add(leftCol);

        // Right Column at (1190, 230, w = 340)
        var rightCol = new StackPanel { Width = 340 };
        var actions = new (string Label, string Icon, bool Focused, bool Destructive, Action Act)[]
        {
            ("Open Studio", "\uE7FC", true, false, () => ControllerActionRequested?.Invoke(State.FocusedPlayer ?? 0, false)),
            ("Test controller", "\uE768", false, false, () => StudioPageRequested?.Invoke(State.FocusedPlayer ?? 0, "tester")),
            ("Manage setups", "\uE8B5", false, false, () => StudioPageRequested?.Invoke(State.FocusedPlayer ?? 0, "setups")),
            ("Controller settings", "\uE713", false, false, () => StudioPageRequested?.Invoke(State.FocusedPlayer ?? 0, "device")),
            ("Remove from slot", "\uE74D", false, true, () => { int p = State.FocusedPlayer ?? 0; RemoveController(p); _ = FocusPlayerAsync(null); })
        };

        foreach (var (l, ic, foc, dest, act) in actions)
        {
            var btn = NexusKit.RowButton(l, null, null, focused: foc, onClick: act, width: 340);
            if (dest)
            {
                var tb = btn.Child as Grid;
                var sp = tb?.Children.OfType<StackPanel>().FirstOrDefault();
                var text = sp?.Children.OfType<TextBlock>().FirstOrDefault();
                if (text != null) text.Foreground = NexusKit.B("#ff8a80");
            }
            rightCol.Children.Add(btn);
        }

        Canvas.SetLeft(rightCol, 1190);
        Canvas.SetTop(rightCol, 230);
        nexusHeroBack.Children.Add(rightCol);

        // Bottom Hints
        var hints = NexusKit.HintsBar(new[] { ("A", "Open Studio"), ("B", "Back") });
        Canvas.SetRight(hints, 62);
        Canvas.SetBottom(hints, 32);
        nexusHeroBack.Children.Add(hints);
    }

    private void SnapNexusLayout()
    {
        if (!IsNexus) return;
        var bounds = NexusBounds();
        for (int i = 0; i < docks.Length; i++)
        {
            docks[i].SetBounds(bounds[i], false, false);
        }
    }

    private DockBounds[] NexusBounds()
    {
        var result = new DockBounds[4];
        for (int i = 0; i < 4; i++)
        {
            double x = NexusXs[i];
            result[i] = new(x, 300, 352, 360);
        }
        return result;
    }

    private void NexusSync()
    {
        if (!IsNexus || nexusBoard == null) return;
        int? focused = State.FocusedPlayer;
        if (focused.HasValue)
        {
            nexusZoneBack!.Visibility = Visibility.Collapsed;
            nexusHeroBack!.Visibility = Visibility.Visible;
            for (int i = 0; i < docks.Length; i++) docks[i].Visibility = Visibility.Collapsed;
        }
        else
        {
            nexusHeroBack!.Visibility = Visibility.Collapsed;
            nexusZoneBack!.Visibility = Visibility.Visible;
            var bounds = NexusBounds();
            for (int i = 0; i < docks.Length; i++)
            {
                docks[i].Visibility = Visibility.Visible;
                docks[i].SetBounds(bounds[i], false, false);
            }
        }
    }

    private async Task NexusFocus(int? index)
    {
        if (index.HasValue) State.Activate(index.Value); else State.Collapse();
        NexusSync();
        await Task.CompletedTask;
    }
}
