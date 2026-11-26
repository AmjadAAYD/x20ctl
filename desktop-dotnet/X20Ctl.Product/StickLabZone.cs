using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using X20Ctl.Product.Scanner;
using X20Ctl.Zone;
using X20Ctl.Zone.Diagnostics;
using X20Ctl.Zone.PcMode;

namespace X20Ctl.Product;

/// <summary>
/// Full-screen PS5 slide-down Analog Studio zone: 10-point spline curves, Radial/Axial/Hybrid deadzones,
/// Radial+Axial anti-deadzone, Gate Morph, Rotation, Angle Snap, Velocity Smoothing, Invert/Swap,
/// SOCD, Sniper, Auto-Sprint, Trigger Lab (Rapid-Trigger &amp; Dual-Stage), live Dual-Dot vector scope,
/// axis deflection bars, Snapback peak meter, and X20C1 share codes.
/// </summary>
public sealed class StickLabZone : Border
{
    public enum LabChannel { LeftStick, RightStick, LeftTrigger, RightTrigger, Competitive }

    readonly PcModeLink? link;
    readonly Action close;
    readonly Action? requestEnablePcMode;
    readonly Action<string>? shareRequested;
    readonly ControllerModel model;
    readonly int playerSlot;

    LabChannel channel = LabChannel.LeftStick;
    PcModeProfile profile;
    bool loading;
    int? draggedPointIndex;

    public Button ExitButton { get; }
    readonly TextBlock statusText = Kit.Text("Ready", 11.5, "DS.TextMuted");
    readonly TextBlock pcModeBadge = Kit.Text("", 12, "DS.Text", FontWeights.SemiBold);
    readonly Button enablePcBtn;
    readonly StackPanel channelBar = new() { Orientation = Orientation.Horizontal };
    readonly StackPanel centerContent = new();
    readonly List<Button> channelButtons = [];

    // Column 1: 10-point spline canvas + HUD + node chips
    const double CL = 36, CT = 14, CW = 392, CH = 236;
    readonly Canvas splineCanvas = new() { Width = 446, Height = 278, Background = Brushes.Transparent };
    readonly Polyline splineLine = new()
    {
        Stroke = new SolidColorBrush(Color.FromRgb(96, 188, 255)),
        StrokeThickness = 3.2,
        StrokeLineJoin = PenLineJoin.Round,
    };
    readonly Polygon splineArea = new()
    {
        Fill = new LinearGradientBrush(Color.FromArgb(56, 56, 189, 248), Color.FromArgb(0, 56, 189, 248), 90),
    };
    readonly Ellipse splineLiveDot = new()
    {
        Width = 12, Height = 12,
        Fill = new SolidColorBrush(Color.FromRgb(61, 214, 140)),
        Stroke = Brushes.White, StrokeThickness = 1.5,
        Visibility = Visibility.Collapsed, IsHitTestVisible = false,
    };
    readonly TextBlock splineHudText = Kit.Text("4 nodes · 50% -> 50% (1.00x)", 11, "DS.AccentHi", FontWeights.SemiBold);
    readonly WrapPanel nodeChipPanel = new();

    // Column 3: Live Dual-Dot Vector Scope + 6-Tile Telemetry Matrix + Axis Deflection Bars
    const double VisSize = 252, VisHalf = 126, VisUnit = 98;
    readonly Canvas visCanvas = new() { Width = VisSize, Height = VisSize, ClipToBounds = true };
    readonly Polygon gatePolygon = new()
    {
        Stroke = new SolidColorBrush(Color.FromArgb(165, 116, 200, 255)),
        StrokeThickness = 1.6,
        Fill = new SolidColorBrush(Color.FromArgb(24, 61, 139, 255)),
    };
    readonly Polygon reachTrace = new()
    {
        Stroke = new SolidColorBrush(Color.FromArgb(185, 56, 189, 248)),
        StrokeThickness = 1.4,
        Fill = new SolidColorBrush(Color.FromArgb(28, 56, 189, 248)),
    };
    readonly System.Windows.Shapes.Path axialCrossOverlay = new()
    {
        Stroke = new SolidColorBrush(Color.FromArgb(140, 232, 54, 79)),
        StrokeThickness = 1.4,
        Fill = new SolidColorBrush(Color.FromArgb(32, 232, 54, 79)),
    };
    readonly Ellipse innerDzRing = new()
    {
        Stroke = new SolidColorBrush(Color.FromArgb(160, 232, 54, 79)),
        StrokeThickness = 1.4,
        Fill = new SolidColorBrush(Color.FromArgb(30, 232, 54, 79)),
    };
    readonly Ellipse outerDzRing = new()
    {
        Stroke = new SolidColorBrush(Color.FromArgb(135, 61, 214, 140)),
        StrokeThickness = 1.4,
        StrokeDashArray = [4, 3],
    };
    readonly Ellipse rawDot = new() { Width = 10, Height = 10, Fill = new SolidColorBrush(Color.FromArgb(215, 180, 195, 220)) };
    readonly Ellipse outDot = new()
    {
        Width = 14, Height = 14,
        Fill = new SolidColorBrush(Color.FromRgb(56, 214, 168)),
        Stroke = Brushes.White, StrokeThickness = 1.8,
    };

    readonly TextBlock scopeHeaderTag = Kit.Text("LEFT STICK · STANDBY", 10.5, "DS.AccentHi", FontWeights.Bold);
    TextBlock tileRaw = null!, tileOut = null!, tileGain = null!, tileCirc = null!, tileDrift = null!, tileSnap = null!;
    readonly Dictionary<string, (Border Fill, Border Track, TextBlock Val)> axisMeters = new();
    Button bypassBtn = null!;

    readonly TextBlock shareStatusText = Kit.Text("", 11.5, "DS.TextMuted");
    readonly TextBox codeBox = new()
    {
        Height = 32, FontSize = 12, FontFamily = new FontFamily("Consolas"),
        Padding = new(8, 5, 8, 5), Foreground = Brushes.White,
        Background = new SolidColorBrush(Color.FromRgb(6, 13, 28)),
        BorderBrush = DS.Brush("DS.Hairline"), VerticalContentAlignment = VerticalAlignment.Center,
    };

    readonly DispatcherTimer liveTimer;
    readonly WindowsReader? reader;
    readonly StickStats diagStatsLeft = new(), diagStatsRight = new();
    readonly StickSnapbackDetector snapLeft = new(), snapRight = new();
    double simRx, simRy, simLx, simLy, simLt, simRt;
    bool hasRealInput;

    public StickLabZone(ControllerModel model, int playerSlot, PcModeLink? link, Action close, Action? requestEnablePcMode = null, Action<string>? shareRequested = null)
    {
        this.model = model;
        this.playerSlot = playerSlot;
        this.link = link;
        this.close = close;
        this.requestEnablePcMode = requestEnablePcMode;
        this.shareRequested = shareRequested;

        Background = new LinearGradientBrush(
            ThemeCatalog.R(Color.FromRgb(6, 11, 24)),
            ThemeCatalog.R(Color.FromRgb(3, 6, 16)), 90);
        BorderThickness = new(0);
        Padding = new(22, 16, 22, 16);

        profile = LoadProfile();

        if (!ReviewSandbox.Active)
        {
            try { reader = new WindowsReader(WindowsReader.Libraries[0]); } catch { reader = null; }
        }

        var root = new Grid();
        root.RowDefinitions.Add(new() { Height = GridLength.Auto });
        root.RowDefinitions.Add(new() { Height = new(12) });
        root.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        Child = root;

        // ---- TOP HEADER ----
        var header = new DockPanel();
        ExitButton = ActionBtn("< Return to Curves", close, primary: false);
        ExitButton.MinWidth = 148;
        ExitButton.Height = 34;
        ExitButton.Margin = new(0, 0, 14, 0);
        DockPanel.SetDock(ExitButton, Dock.Left);
        header.Children.Add(ExitButton);

        var rightHeader = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(rightHeader, Dock.Right);
        enablePcBtn = ActionBtn("Enable PC Mode", () =>
        {
            requestEnablePcMode?.Invoke();
            RefreshPcModeBadge();
        }, primary: true);
        enablePcBtn.Height = 32;
        enablePcBtn.Padding = new(12, 0, 12, 0);
        enablePcBtn.Margin = new(8, 0, 0, 0);

        var pcPill = new Border
        {
            CornerRadius = new(9),
            BorderThickness = new(1),
            BorderBrush = DS.Brush("DS.Hairline"),
            Background = new SolidColorBrush(Color.FromArgb(165, 10, 19, 38)),
            Padding = new(11, 6, 11, 6),
            VerticalAlignment = VerticalAlignment.Center,
            Child = pcModeBadge,
        };
        rightHeader.Children.Add(pcPill);
        rightHeader.Children.Add(enablePcBtn);
        header.Children.Add(rightHeader);

        var titleStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        titleStack.Children.Add(new Border
        {
            CornerRadius = new(8),
            Padding = new(10, 4, 10, 4),
            Margin = new(0, 0, 10, 0),
            Background = new SolidColorBrush(Color.FromArgb(55, 56, 189, 248)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(150, 56, 189, 248)),
            BorderThickness = new(1),
            Child = Kit.Text("ANALOG STUDIO", 12.5, "DS.AccentHi", FontWeights.Bold),
        });
        titleStack.Children.Add(Kit.Text(model.Name.Replace("EasySMX ", ""), 15, "DS.Text", FontWeights.SemiBold).Also(t => { t.VerticalAlignment = VerticalAlignment.Center; t.Margin = new(0, 0, 16, 0); }));

        foreach (var (ch, title) in new (LabChannel, string)[]
        {
            (LabChannel.LeftStick, "Left Stick"),
            (LabChannel.RightStick, "Right Stick"),
            (LabChannel.LeftTrigger, "LT"),
            (LabChannel.RightTrigger, "RT"),
            (LabChannel.Competitive, "Competitive"),
        })
        {
            var captured = ch;
            var btn = ActionBtn(title, () => SelectChannel(captured), primary: false);
            btn.Tag = ch;
            btn.Height = 32;
            btn.Margin = new(0, 0, 5, 0);
            btn.Padding = new(12, 0, 12, 0);
            channelButtons.Add(btn);
            channelBar.Children.Add(btn);
        }
        titleStack.Children.Add(channelBar);
        header.Children.Add(titleStack);
        root.Children.Add(header);

        // ---- 3-COLUMN WORKSPACE (Zero Dead Space) ----
        var body = new Grid();
        body.ColumnDefinitions.Add(new() { Width = new(1.04, GridUnitType.Star) });
        body.ColumnDefinitions.Add(new() { Width = new(12) });
        body.ColumnDefinitions.Add(new() { Width = new(1.18, GridUnitType.Star) });
        body.ColumnDefinitions.Add(new() { Width = new(12) });
        body.ColumnDefinitions.Add(new() { Width = new(0.98, GridUnitType.Star) });
        Grid.SetRow(body, 2);
        root.Children.Add(body);

        body.Children.Add(BuildSplineColumn());

        var centerScroll = new ScrollViewer
        {
            Content = centerContent,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Padding = new(0, 0, 4, 0),
        };
        var centerCard = GlassCard("LIVE DSP STUDIO · SIGNAL PIPELINE", centerScroll);
        Grid.SetColumn(centerCard, 2);
        body.Children.Add(centerCard);

        var rightCard = BuildDiagnosticsColumn();
        Grid.SetColumn(rightCard, 4);
        body.Children.Add(rightCard);

        RefreshPcModeBadge();
        SelectChannel(LabChannel.LeftStick);

        liveTimer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(20) };
        liveTimer.Tick += (_, _) => SampleAndRenderLive();
        Loaded += (_, _) => liveTimer.Start();
        Unloaded += (_, _) => { liveTimer.Stop(); reader?.Dispose(); };
    }

    Border BuildSplineColumn()
    {
        // Chart Gridlines
        for (int t = 0; t <= 100; t += 25)
        {
            double gx = CX(t), gy = CY(t);
            splineCanvas.Children.Add(new Line { X1 = gx, X2 = gx, Y1 = CT, Y2 = CT + CH, Stroke = new SolidColorBrush(Color.FromArgb(42, 140, 175, 255)), StrokeThickness = 1 });
            splineCanvas.Children.Add(new Line { X1 = CL, X2 = CL + CW, Y1 = gy, Y2 = gy, Stroke = new SolidColorBrush(Color.FromArgb(42, 140, 175, 255)), StrokeThickness = 1 });
            var tx = new TextBlock { Text = t.ToString(), FontSize = 10.5, Foreground = DS.Brush("DS.TextMuted") };
            Canvas.SetLeft(tx, gx - 7); Canvas.SetTop(tx, CT + CH + 5);
            splineCanvas.Children.Add(tx);
            var ty = new TextBlock { Text = t.ToString(), FontSize = 10.5, Foreground = DS.Brush("DS.TextMuted") };
            Canvas.SetLeft(ty, 6); Canvas.SetTop(ty, gy - 7);
            splineCanvas.Children.Add(ty);
        }
        splineCanvas.Children.Add(new Line
        {
            X1 = CL, Y1 = CT + CH, X2 = CL + CW, Y2 = CT,
            Stroke = new SolidColorBrush(Color.FromArgb(85, 200, 220, 255)),
            StrokeDashArray = [4, 4], StrokeThickness = 1.2,
        });
        splineCanvas.Children.Add(splineArea);
        splineCanvas.Children.Add(splineLine);
        splineCanvas.Children.Add(splineLiveDot);

        splineCanvas.MouseLeftButtonDown += OnSplineCanvasDown;
        splineCanvas.MouseMove += OnSplineCanvasMove;
        splineCanvas.MouseLeftButtonUp += (_, _) => { draggedPointIndex = null; splineCanvas.ReleaseMouseCapture(); };

        var chartContainer = new Grid();
        chartContainer.Children.Add(new Viewbox { Stretch = Stretch.Uniform, Child = splineCanvas });
        var hudBadge = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            CornerRadius = new(7),
            Background = new SolidColorBrush(Color.FromArgb(195, 10, 20, 42)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(85, 56, 189, 248)),
            BorderThickness = new(1),
            Padding = new(8, 3, 8, 3),
            Margin = new(4, 2, 0, 0),
            IsHitTestVisible = false,
            Child = splineHudText
        };
        chartContainer.Children.Add(hudBadge);
        var chartWell = DarkWell(chartContainer, new Thickness(8, 6, 8, 6));

        // 6 Visual Curve Archetype Cards with mini polyline sparklines
        var archetypeGrid = new UniformGrid { Columns = 3, Rows = 2, Margin = new(-2, 0, -2, 0) };
        foreach (var (key, label) in new (string, string)[]
        {
            ("linear", "Linear"),
            ("precision", "Precision"),
            ("aggressive", "Aggressive"),
            ("scurve", "S-Curve"),
            ("dualzone", "Dual-Zone"),
            ("exponential", "Exponential"),
        })
        {
            string k = key;
            var previewPts = StickLabMath.GenerateSmartCurve(k);
            var spark = new Canvas { Width = 54, Height = 20, HorizontalAlignment = HorizontalAlignment.Center, Margin = new(0, 0, 0, 2) };
            var polyPts = new PointCollection();
            for (int s = 0; s <= 12; s++)
            {
                double x = s / 12.0;
                double y = StickLabMath.EvaluateSpline(previewPts, x);
                polyPts.Add(new Point(x * 54.0, 20.0 - y * 20.0));
            }
            spark.Children.Add(new Line { X1 = 0, Y1 = 20, X2 = 54, Y2 = 0, Stroke = new SolidColorBrush(Color.FromArgb(55, 180, 205, 245)), StrokeDashArray = [2, 2], StrokeThickness = 1 });
            spark.Children.Add(new Polyline { Stroke = new SolidColorBrush(Color.FromRgb(96, 188, 255)), StrokeThickness = 1.8, Points = polyPts });

            var b = new Button
            {
                Style = DS.Style("ActionButton"),
                Height = 46, MinHeight = 0,
                Margin = new(2),
                Padding = new(6, 4, 6, 4),
                Content = new StackPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Children =
                    {
                        spark,
                        Kit.Text(label, 11, "DS.Text", FontWeights.SemiBold).Also(t => t.HorizontalAlignment = HorizontalAlignment.Center)
                    }
                }
            };
            b.Click += (_, _) => ApplySmartCurve(k);
            archetypeGrid.Children.Add(b);
        }

        // Control Nodes Tray + 4 Quick Tools
        var toolsRow = new UniformGrid { Columns = 4, Margin = new(-2, 6, -2, 0) };
        Button MiniTool(string txt, Action act)
        {
            var b = ActionBtn(txt, act, primary: false);
            b.Height = 28; b.FontSize = 11; b.Padding = new(4, 0, 4, 0); b.Margin = new(2, 0, 2, 0);
            return b;
        }
        toolsRow.Children.Add(MiniTool("+ Midpoint", AddMidpointNode));
        toolsRow.Children.Add(MiniTool("Smooth", SmoothSplineNodes));
        toolsRow.Children.Add(MiniTool("Mirror L<->R", MirrorSplineToOtherStick));
        toolsRow.Children.Add(MiniTool("Reset Curve", () => ApplySmartCurve("linear")));

        var nodesWell = DarkWell(new StackPanel
        {
            Children =
            {
                new DockPanel
                {
                    Margin = new(0, 0, 0, 5),
                    Children =
                    {
                        Kit.Text("Click graph: add · Drag: shape · Right-click: remove", 10, "DS.TextMuted").Also(t => { DockPanel.SetDock(t, Dock.Right); t.VerticalAlignment = VerticalAlignment.Center; }),
                        Kit.Text("ACTIVE NODES", 10, "DS.AccentHi", FontWeights.Bold).Also(t => t.VerticalAlignment = VerticalAlignment.Center),
                    }
                },
                nodeChipPanel,
                toolsRow
            }
        }, new Thickness(10, 8, 10, 8));

        // Share & Import Code Deck
        var shareBtns = new UniformGrid { Columns = shareRequested != null ? 3 : 2, Margin = new(-2, 6, -2, 0) };
        shareBtns.Children.Add(ActionBtn("Copy Code", CopyShareCode, primary: false).Also(b => { b.Height = 30; b.FontSize = 11.5; b.Margin = new(2, 0, 2, 0); }));
        shareBtns.Children.Add(ActionBtn("Apply Code", ApplyShareCode, primary: false).Also(b => { b.Height = 30; b.FontSize = 11.5; b.Margin = new(2, 0, 2, 0); }));
        if (shareRequested != null)
            shareBtns.Children.Add(ActionBtn("Share Online", () => shareRequested(ExportCurrentShareCode()), primary: true).Also(b => { b.Height = 30; b.FontSize = 11.5; b.Margin = new(2, 0, 2, 0); }));

        var shareWell = DarkWell(new StackPanel
        {
            Children =
            {
                Kit.Text("SHARE & IMPORT CODE (X20C1)", 10, "DS.AccentHi", FontWeights.Bold).Also(t => t.Margin = new(0, 0, 0, 5)),
                codeBox,
                shareBtns,
                shareStatusText.Also(t => t.Margin = new(0, 4, 0, 0))
            }
        }, new Thickness(10, 8, 10, 8));

        var colGrid = new Grid();
        colGrid.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) }); // Spline instrument bay
        colGrid.RowDefinitions.Add(new() { Height = GridLength.Auto });          // Archetypes
        colGrid.RowDefinitions.Add(new() { Height = GridLength.Auto });          // Node tray & tools
        colGrid.RowDefinitions.Add(new() { Height = GridLength.Auto });          // X20C1 Share bar

        colGrid.Children.Add(chartWell);

        var archWrap = new StackPanel { Margin = new(0, 8, 0, 0) };
        archWrap.Children.Add(Kit.Text("CURVE ARCHETYPES", 10, "DS.AccentHi", FontWeights.Bold).Also(t => t.Margin = new(0, 0, 0, 4)));
        archWrap.Children.Add(archetypeGrid);
        Grid.SetRow(archWrap, 1);
        colGrid.Children.Add(archWrap);

        nodesWell.Margin = new(0, 8, 0, 0);
        Grid.SetRow(nodesWell, 2);
        colGrid.Children.Add(nodesWell);

        shareWell.Margin = new(0, 8, 0, 0);
        Grid.SetRow(shareWell, 3);
        colGrid.Children.Add(shareWell);

        return GlassCard("10-POINT SPLINE DESIGNER", colGrid);
    }

    Border BuildDiagnosticsColumn()
    {
        visCanvas.Children.Add(new Ellipse
        {
            Width = VisUnit * 2, Height = VisUnit * 2,
            Stroke = new SolidColorBrush(Color.FromArgb(65, 140, 175, 255)),
            StrokeThickness = 1.2,
        }.Also(e => { Canvas.SetLeft(e, VisHalf - VisUnit); Canvas.SetTop(e, VisHalf - VisUnit); }));
        visCanvas.Children.Add(new Ellipse
        {
            Width = VisUnit, Height = VisUnit,
            Stroke = new SolidColorBrush(Color.FromArgb(35, 140, 175, 255)),
            StrokeThickness = 1, StrokeDashArray = [3, 3]
        }.Also(e => { Canvas.SetLeft(e, VisHalf - VisUnit / 2); Canvas.SetTop(e, VisHalf - VisUnit / 2); }));
        visCanvas.Children.Add(new Line { X1 = VisHalf, X2 = VisHalf, Y1 = VisHalf - VisUnit, Y2 = VisHalf + VisUnit, Stroke = new SolidColorBrush(Color.FromArgb(42, 140, 175, 255)) });
        visCanvas.Children.Add(new Line { Y1 = VisHalf, Y2 = VisHalf, X1 = VisHalf - VisUnit, X2 = VisHalf + VisUnit, Stroke = new SolidColorBrush(Color.FromArgb(42, 140, 175, 255)) });
        visCanvas.Children.Add(gatePolygon);
        visCanvas.Children.Add(reachTrace);
        visCanvas.Children.Add(axialCrossOverlay);
        visCanvas.Children.Add(innerDzRing);
        visCanvas.Children.Add(outerDzRing);
        visCanvas.Children.Add(rawDot);
        visCanvas.Children.Add(outDot);

        var scopeInner = new DockPanel();
        var scopeTop = new DockPanel { Margin = new(0, 0, 0, 4) };
        var legend = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        legend.Children.Add(new Ellipse { Width = 8, Height = 8, Fill = new SolidColorBrush(Color.FromRgb(180, 195, 220)), Margin = new(0, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center });
        legend.Children.Add(Kit.Text("Raw", 10.5, "DS.TextSoft").Also(t => t.Margin = new(0, 0, 10, 0)));
        legend.Children.Add(new Ellipse { Width = 9, Height = 9, Fill = new SolidColorBrush(Color.FromRgb(56, 214, 168)), Margin = new(0, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center });
        legend.Children.Add(Kit.Text("Out", 10.5, "DS.Text"));
        DockPanel.SetDock(legend, Dock.Right);
        scopeTop.Children.Add(legend);
        scopeTop.Children.Add(scopeHeaderTag.Also(t => t.VerticalAlignment = VerticalAlignment.Center));
        DockPanel.SetDock(scopeTop, Dock.Top);
        scopeInner.Children.Add(scopeTop);
        scopeInner.Children.Add(new Viewbox { Stretch = Stretch.Uniform, Child = visCanvas });
        var scopeWell = DarkWell(scopeInner, new Thickness(10, 8, 10, 8));

        // 6-Tile Telemetry Matrix (UniformGrid 2x3)
        var matrix = new UniformGrid { Columns = 2, Rows = 3, Margin = new(-3, 8, -3, 0) };
        matrix.Children.Add(TelemetryTile("RAW INPUT", "0.00, 0.00", out tileRaw));
        matrix.Children.Add(TelemetryTile("PROCESSED OUT", "0.00, 0.00", out tileOut));
        matrix.Children.Add(TelemetryTile("PUSH / GAIN", "0% -> 0% · 1.00x", out tileGain));
        matrix.Children.Add(TelemetryTile("CIRCULARITY", "0% swept", out tileCirc));
        matrix.Children.Add(TelemetryTile("CENTER DRIFT", "Release stick", out tileDrift));
        matrix.Children.Add(TelemetryTile("SNAPBACK PEAK", "0.0% · 0 hits", out tileSnap));

        // Axis & Trigger Deflection Bars
        var barsPanel = new StackPanel();
        barsPanel.Children.Add(Kit.Text("LIVE AXIS & TRIGGER OUTPUT", 10, "DS.AccentHi", FontWeights.Bold).Also(t => t.Margin = new(0, 0, 0, 5)));
        barsPanel.Children.Add(BuildAxisMeter("LX / RX", "X", Color.FromRgb(96, 188, 255)));
        barsPanel.Children.Add(BuildAxisMeter("LY / RY", "Y", Color.FromRgb(56, 214, 168)));
        barsPanel.Children.Add(BuildAxisMeter("LT", "LT", Color.FromRgb(255, 196, 92)));
        barsPanel.Children.Add(BuildAxisMeter("RT", "RT", Color.FromRgb(232, 92, 128)));
        var barsWell = DarkWell(barsPanel, new Thickness(10, 8, 10, 8));
        barsWell.Margin = new(0, 8, 0, 0);

        // 4-Button Calibration & A/B Deck
        var deck = new UniformGrid { Columns = 2, Rows = 2, Margin = new(-2, 8, -2, 0) };
        bypassBtn = ActionBtn("DSP Active (Click A/B)", ToggleBypassCurrentStick, primary: false);
        bypassBtn.Height = 30; bypassBtn.FontSize = 11.5; bypassBtn.Margin = new(2);
        deck.Children.Add(ActionBtn("Use Drift as DZ", UseMeasuredDriftAsDeadzone, primary: false).Also(b => { b.Height = 30; b.FontSize = 11.5; b.Margin = new(2); }));
        deck.Children.Add(bypassBtn);
        deck.Children.Add(ActionBtn("Reset Trace", () =>
        {
            diagStatsLeft.Reset(); diagStatsRight.Reset();
            snapLeft.Reset(); snapRight.Reset();
            UpdateLiveVisualizer(0, 0, 0, 0);
            statusText.Text = "Cleared circularity sweep and snapback history.";
        }, primary: false).Also(b => { b.Height = 30; b.FontSize = 11.5; b.Margin = new(2); }));
        deck.Children.Add(ActionBtn("Reset Channel", ResetActiveChannel, primary: false).Also(b => { b.Height = 30; b.FontSize = 11.5; b.Margin = new(2); }));

        var rightGrid = new Grid();
        rightGrid.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        rightGrid.RowDefinitions.Add(new() { Height = GridLength.Auto });
        rightGrid.RowDefinitions.Add(new() { Height = GridLength.Auto });
        rightGrid.RowDefinitions.Add(new() { Height = GridLength.Auto });
        rightGrid.RowDefinitions.Add(new() { Height = GridLength.Auto });

        rightGrid.Children.Add(scopeWell);
        Grid.SetRow(matrix, 1); rightGrid.Children.Add(matrix);
        Grid.SetRow(barsWell, 2); rightGrid.Children.Add(barsWell);
        Grid.SetRow(deck, 3); rightGrid.Children.Add(deck);
        Grid.SetRow(statusText, 4); statusText.Margin = new(0, 6, 0, 0); statusText.TextWrapping = TextWrapping.Wrap; rightGrid.Children.Add(statusText);

        return GlassCard("LIVE VECTOR SCOPE & TELEMETRY", rightGrid);
    }

    Border TelemetryTile(string label, string initial, out TextBlock valueOut)
    {
        valueOut = Kit.Text(initial, 11.5, "DS.Text", FontWeights.SemiBold).Also(t => t.TextTrimming = TextTrimming.CharacterEllipsis);
        var tile = DarkWell(new StackPanel
        {
            Children =
            {
                Kit.Text(label, 9.5, "DS.AccentHi", FontWeights.Bold),
                valueOut.Also(v => v.Margin = new(0, 2, 0, 0))
            }
        }, new Thickness(9, 6, 9, 6));
        tile.Margin = new(3);
        return tile;
    }

    FrameworkElement BuildAxisMeter(string label, string key, Color accent)
    {
        var track = new Border
        {
            Height = 8,
            CornerRadius = new(4),
            Background = new SolidColorBrush(Color.FromArgb(140, 14, 24, 46)),
            ClipToBounds = true,
            VerticalAlignment = VerticalAlignment.Center
        };
        var fill = new Border
        {
            Height = 8,
            Width = 0,
            HorizontalAlignment = HorizontalAlignment.Left,
            CornerRadius = new(4),
            Background = new SolidColorBrush(accent)
        };
        track.Child = fill;
        var val = Kit.Text("0%", 10.5, "DS.TextSoft", FontWeights.SemiBold).Also(t => { t.Width = 42; t.TextAlignment = TextAlignment.Right; });
        axisMeters[key] = (fill, track, val);

        var row = new DockPanel { Margin = new(0, 2, 0, 2) };
        var lbl = Kit.Text(label, 10.5, "DS.TextMuted", FontWeights.SemiBold).Also(t => { t.Width = 52; t.VerticalAlignment = VerticalAlignment.Center; });
        DockPanel.SetDock(lbl, Dock.Left);
        DockPanel.SetDock(val, Dock.Right);
        row.Children.Add(lbl);
        row.Children.Add(val);
        row.Children.Add(track);
        return row;
    }

    void UpdateAxisMeter(string key, double pct)
    {
        if (!axisMeters.TryGetValue(key, out var m)) return;
        double clamped = Math.Clamp(pct, 0, 100);
        double trackW = m.Track.ActualWidth > 10 ? m.Track.ActualWidth : 160;
        m.Fill.Width = trackW * (clamped / 100.0);
        m.Val.Text = $"{clamped:0}%";
    }

    public void SelectChannel(LabChannel next)
    {
        channel = next;
        foreach (var b in channelButtons)
        {
            bool active = b.Tag is LabChannel c && c == channel;
            b.Background = active ? new SolidColorBrush(Color.FromRgb(42, 108, 212)) : new SolidColorBrush(Color.FromArgb(145, 14, 24, 48));
            b.BorderBrush = active ? Brushes.White : DS.Brush("DS.Hairline");
        }
        RebuildCenterControls();
        RedrawSpline();
        UpdateLiveVisualizer(simLx, simLy, simRx, simRy);
    }

    public void ShowLive(double lx, double ly, double rx, double ry)
    {
        hasRealInput = true;
        simLx = lx; simLy = ly; simRx = rx; simRy = ry;
        UpdateLiveVisualizer(lx, ly, rx, ry);
    }

    public void ClearLive()
    {
        hasRealInput = false;
        simLx = simLy = simRx = simRy = simLt = simRt = 0;
        UpdateLiveVisualizer(0, 0, 0, 0);
    }

    void SampleAndRenderLive()
    {
        if (reader != null)
        {
            int slot = FindReaderSlot();
            if (slot >= 0 && reader.Read(slot, out var state))
            {
                hasRealInput = true;
                var p = state.Pad;
                diagStatsLeft.Add(p.LX, p.LY);
                diagStatsRight.Add(p.RX, p.RY);
                snapLeft.Observe(p.LX, p.LY);
                snapRight.Observe(p.RX, p.RY);
                simLx = p.LX / 32767.0; simLy = p.LY / 32767.0;
                simRx = p.RX / 32767.0; simRy = p.RY / 32767.0;
                simLt = p.LT / 255.0; simRt = p.RT / 255.0;
                UpdateLiveVisualizer(simLx, simLy, simRx, simRy);
                return;
            }
        }
        UpdateLiveVisualizer(simLx, simLy, simRx, simRy);
    }

    int FindReaderSlot()
    {
        if (reader == null) return -1;
        if (playerSlot is >= 0 and < 4 && !PcModeService.IsVirtual(playerSlot) && reader.Read(playerSlot, out _))
            return playerSlot;
        for (int i = 0; i < 4; i++)
            if (!PcModeService.IsVirtual(i) && reader.Read(i, out _)) return i;
        return -1;
    }

    void UpdateLiveVisualizer(double lx, double ly, double rx, double ry)
    {
        bool right = channel == LabChannel.RightStick;
        string stickKey = right ? "RightStick" : "LeftStick";
        var tune = profile.Sticks.GetValueOrDefault(stickKey) ?? new StickTuning();
        var lab = tune.Lab ?? new StickLabSettings();
        var curve = profile.Curves.GetValueOrDefault((right ? CurveChannel.RightStick : CurveChannel.LeftStick).ToString());

        if (bypassBtn != null)
        {
            bypassBtn.Content = tune.Raw ? "Bypass ON (1:1 Raw)" : "DSP Active (Click A/B)";
            bypassBtn.BorderBrush = tune.Raw ? DS.Brush("DS.Warn") : DS.Brush("DS.Hairline");
        }

        double rawX = right ? rx : lx;
        double rawY = right ? ry : ly;
        short inX = ToShort(rawX), inY = ToShort(rawY);
        var (outXs, outYs) = PadProcessor.Stick(inX, inY, curve, tune);
        double outX = outXs / 32767.0, outY = outYs / 32767.0;

        Canvas.SetLeft(rawDot, VisHalf + rawX * VisUnit - 5);
        Canvas.SetTop(rawDot, VisHalf - rawY * VisUnit - 5);
        Canvas.SetLeft(outDot, VisHalf + outX * VisUnit - 7);
        Canvas.SetTop(outDot, VisHalf - outY * VisUnit - 7);

        double morph = tune.SquareGate ? 100.0 : lab.GateMorph;
        var gatePts = new PointCollection(48);
        for (int i = 0; i < 48; i++)
        {
            double a = i / 48.0 * Math.PI * 2.0;
            double ux = Math.Cos(a), uy = Math.Sin(a);
            var (mx, my) = StickLabMath.MorphGate(ux, uy, 1.0, morph);
            gatePts.Add(new Point(VisHalf + mx * VisUnit, VisHalf - my * VisUnit));
        }
        gatePolygon.Points = gatePts;

        double innerR = Math.Clamp(tune.Inner / 100.0, 0, 0.45) * VisUnit;
        double outerR = Math.Clamp(tune.Outer / 100.0, 0.5, 1.0) * VisUnit;
        if (lab.DeadzoneShape == DeadzoneShape.Axial)
        {
            innerDzRing.Visibility = Visibility.Collapsed;
            axialCrossOverlay.Visibility = Visibility.Visible;
            var geom = new GeometryGroup();
            geom.Children.Add(new RectangleGeometry(new Rect(VisHalf - VisUnit, VisHalf - innerR, VisUnit * 2, innerR * 2)));
            geom.Children.Add(new RectangleGeometry(new Rect(VisHalf - innerR, VisHalf - VisUnit, innerR * 2, VisUnit * 2)));
            axialCrossOverlay.Data = geom;
        }
        else
        {
            axialCrossOverlay.Visibility = Visibility.Collapsed;
            innerDzRing.Visibility = Visibility.Visible;
            innerDzRing.Width = innerR * 2; innerDzRing.Height = innerR * 2;
            Canvas.SetLeft(innerDzRing, VisHalf - innerR); Canvas.SetTop(innerDzRing, VisHalf - innerR);
        }
        outerDzRing.Width = outerR * 2; outerDzRing.Height = outerR * 2;
        Canvas.SetLeft(outerDzRing, VisHalf - outerR); Canvas.SetTop(outerDzRing, VisHalf - outerR);

        var st = right ? diagStatsRight : diagStatsLeft;
        var sb = right ? snapRight : snapLeft;
        var rPts = new PointCollection(StickStats.Sectors);
        for (int i = 0; i < StickStats.Sectors; i++)
        {
            double r = Math.Min(st.Reach[i], 1.15) * VisUnit;
            double ang = (i + 0.5) / StickStats.Sectors * Math.PI * 2.0 - Math.PI;
            rPts.Add(new Point(VisHalf + Math.Cos(ang) * r, VisHalf - Math.Sin(ang) * r));
        }
        reachTrace.Points = rPts;

        double rawMag = Math.Min(1.0, Math.Sqrt(rawX * rawX + rawY * rawY));
        double outMag = Math.Min(1.0, Math.Sqrt(outX * outX + outY * outY));
        double gain = rawMag > 0.03 ? outMag / rawMag : 1.0;

        scopeHeaderTag.Text = $"{(right ? "RIGHT STICK" : "LEFT STICK")} · {(hasRealInput ? "LIVE HARDWARE" : "STANDBY")}";
        tileRaw.Text = $"{rawX:+0.00;-0.00;0.00}, {rawY:+0.00;-0.00;0.00}";
        tileOut.Text = $"{outX:+0.00;-0.00;0.00}, {outY:+0.00;-0.00;0.00}";
        tileGain.Text = $"{rawMag * 100:0}% -> {outMag * 100:0}% ({gain:0.00}x)";
        tileCirc.Text = st.CircularityError is { } ce ? $"{ce:0.0}% err" : $"{st.Covered * 100 / StickStats.Sectors}% swept";
        tileDrift.Text = st.Rest is { } rd ? $"{rd.X:+0.0;-0.0;0.0}%, {rd.Y:+0.0;-0.0;0.0}%" : "Release stick";
        tileSnap.Text = $"{sb.PeakBouncePct:0.0}% ({sb.BounceEvents} hits)";

        UpdateAxisMeter("X", Math.Abs(outX) * 100.0);
        UpdateAxisMeter("Y", Math.Abs(outY) * 100.0);
        UpdateAxisMeter("LT", simLt * 100.0);
        UpdateAxisMeter("RT", simRt * 100.0);

        if (rawMag > 0.02)
        {
            double outPct = PadProcessor.Response(curve, tune, rawMag) * 100.0;
            Canvas.SetLeft(splineLiveDot, CX(rawMag * 100) - 6);
            Canvas.SetTop(splineLiveDot, CY(outPct) - 6);
            splineLiveDot.Visibility = Visibility.Visible;
        }
        else
        {
            splineLiveDot.Visibility = Visibility.Collapsed;
        }
    }

    void RebuildCenterControls()
    {
        loading = true;
        centerContent.Children.Clear();

        if (channel is LabChannel.LeftStick or LabChannel.RightStick)
        {
            string key = channel == LabChannel.RightStick ? "RightStick" : "LeftStick";
            string otherKey = channel == LabChannel.RightStick ? "LeftStick" : "RightStick";
            var tune = GetOrCreateStick(key);
            var lab = tune.Lab ??= new StickLabSettings();

            double gateVal = tune.SquareGate ? 100 : lab.GateMorph;
            double radialAnti = lab.RadialAntiDeadzone > 0 ? lab.RadialAntiDeadzone : tune.AntiDeadzone;

            centerContent.Children.Add(BentoModule("DEADZONE & GATE GEOMETRY", "STAGE 1", new StackPanel
            {
                Children =
                {
                    SegmentRow("Deadzone Shape",
                        [("Radial", DeadzoneShape.Radial), ("Axial (Cross)", DeadzoneShape.Axial), ("Hybrid", DeadzoneShape.Hybrid)],
                        lab.DeadzoneShape, shape => { lab.DeadzoneShape = shape; PersistProfile("Deadzone shape: " + shape); }),
                    FineSlider("Gate Shape Morph (Circle <-> Square)", 0, 100, 5, gateVal, v => v >= 99 ? "100% Square" : v <= 1 ? "0% Circle" : $"{v:0}%",
                        v => { lab.GateMorph = v; tune.SquareGate = v >= 99; PersistProfile(); }),
                    TwoCol(
                        FineSlider("Inner Deadzone", 0, 40, 0.1, tune.Inner, v => $"{v:0.0}%",
                            v => { tune.Inner = Math.Round(v, 1); if (tune.Outer <= tune.Inner + 1) tune.Outer = Math.Min(100, tune.Inner + 5); PersistProfile(); RedrawSpline(); }),
                        FineSlider("Outer Full At", 60, 100, 0.5, tune.Outer, v => $"{v:0.0}%",
                            v => { tune.Outer = Math.Max(tune.Inner + 2, Math.Round(v, 1)); PersistProfile(); RedrawSpline(); })),
                    TwoCol(
                        FineSlider("Radial Anti-DZ", 0, 40, 0.5, radialAnti, v => $"{v:0.0}%",
                            v => { lab.RadialAntiDeadzone = tune.AntiDeadzone = Math.Round(v, 1); PersistProfile(); RedrawSpline(); }),
                        FineSlider("Axial Anti-DZ", 0, 30, 0.5, lab.AxialAntiDeadzone, v => $"{v:0.0}%",
                            v => { lab.AxialAntiDeadzone = Math.Round(v, 1); PersistProfile(); })),
                    FineSlider("Maximum Output Cap", 60, 100, 1, tune.MaxOutput, v => $"{v:0}%",
                        v => { tune.MaxOutput = Math.Round(v); PersistProfile(); RedrawSpline(); })
                }
            }));

            centerContent.Children.Add(BentoModule("AXIS TILT, ANGLE SNAP & FEEL", "STAGE 2", new StackPanel
            {
                Children =
                {
                    TwoCol(
                        FineSlider("Thumb Tilt Rotation", -45, 45, 1, lab.RotationDegrees, v => $"{v:+0;-0;0} deg",
                            v => { lab.RotationDegrees = Math.Round(v); PersistProfile(); }),
                        FineSlider("Angle Snap Cone", 0, 15, 0.5, lab.AngleSnapDegrees, v => v <= 0.1 ? "Off" : $"{v:0.0} deg",
                            v => { lab.AngleSnapDegrees = Math.Round(v, 1); PersistProfile(); })),
                    ToggleRow("8-Way Diagonal Angle Snap (vs 4 cardinal)", lab.AngleSnap8Way, on => { lab.AngleSnap8Way = on; PersistProfile(); }),
                    TwoCol(
                        FineSlider("Vertical / Axis Ratio", StickTuning.MinRatio, StickTuning.MaxRatio, 5, tune.AxisRatio, v => $"{v:0}%",
                            v => { tune.AxisRatio = (int)Math.Round(v); PersistProfile(); }),
                        FineSlider("Velocity Smoothing", 0, 100, 5, lab.Smoothing, v => $"{v:0}%",
                            v => { lab.Smoothing = Math.Round(v); PersistProfile(); }))
                }
            }));

            var oriGrid = new UniformGrid { Columns = 2, Margin = new(0, 0, 0, 6) };
            oriGrid.Children.Add(ToggleRow("Invert X Axis", lab.InvertX, on => { lab.InvertX = on; PersistProfile(); }).Also(f => ((FrameworkElement)f).Margin = new(0, 0, 10, 0)));
            oriGrid.Children.Add(ToggleRow("Invert Y Axis", lab.InvertY, on => { lab.InvertY = on; PersistProfile(); }));
            oriGrid.Children.Add(ToggleRow("Swap L & R Sticks", profile.SwapSticks, on => { profile.SwapSticks = on; PersistProfile("Sticks swapped: " + (on ? "On" : "Off")); }).Also(f => ((FrameworkElement)f).Margin = new(0, 0, 10, 0)));
            oriGrid.Children.Add(ToggleRow("Auto-Center Drift (<=6%)", tune.AutoCenterDrift, on => { tune.AutoCenterDrift = on; PersistProfile(); }));

            var copyOther = ActionBtn($"Mirror {key} Settings to {otherKey}", () =>
            {
                var clone = CloneStick(tune);
                profile.Sticks[otherKey] = clone;
                if (profile.Curves.TryGetValue(key, out var sc)) profile.Curves[otherKey] = sc with { };
                PersistProfile($"Copied settings cleanly to {otherKey}");
            }, primary: false);
            copyOther.Height = 32; copyOther.HorizontalAlignment = HorizontalAlignment.Stretch;

            centerContent.Children.Add(BentoModule("ORIENTATION & CHANNEL SYNC", "STAGE 3", new StackPanel
            {
                Children =
                {
                    oriGrid,
                    ToggleRow("Bypass DSP Processing (Raw 1:1 Pass-through)", tune.Raw, on => { tune.Raw = on; PersistProfile(); RedrawSpline(); }),
                    copyOther
                }
            }));
        }
        else if (channel is LabChannel.LeftTrigger or LabChannel.RightTrigger)
        {
            string tKey = channel == LabChannel.LeftTrigger ? "LT" : "RT";
            var tt = profile.Triggers.TryGetValue(tKey, out var existing) ? existing : (profile.Triggers[tKey] = new TriggerTuning());

            centerContent.Children.Add(BentoModule($"{tKey} RANGE & FORCE CURVE", "TRIGGER", new StackPanel
            {
                Children =
                {
                    SegmentRow("Force Preset",
                        [("Linear", TriggerPreset.Linear), ("Hair", TriggerPreset.HairTrigger), ("Racing", TriggerPreset.RacingThrottle), ("Sniper", TriggerPreset.Sniper), ("Digital", TriggerPreset.DigitalClick)],
                        tt.Preset, p => { tt.Preset = p; PersistProfile($"{tKey} preset: {p}"); RedrawSpline(); }),
                    TwoCol(
                        FineSlider("Inner Deadzone", 0, 40, 1, tt.InnerDeadzonePct, v => $"{v:0}%",
                            v => { tt.InnerDeadzonePct = (int)Math.Round(v); if (tt.OuterThresholdPct <= tt.InnerDeadzonePct + 5) tt.OuterThresholdPct = Math.Min(100, tt.InnerDeadzonePct + 10); PersistProfile(); }),
                        FineSlider("Full Output At", 50, 100, 1, tt.OuterThresholdPct, v => $"{v:0}%",
                            v => { tt.OuterThresholdPct = Math.Max(tt.InnerDeadzonePct + 10, (int)Math.Round(v)); PersistProfile(); }))
                }
            }));

            centerContent.Children.Add(BentoModule("RAPID-TRIGGER DYNAMIC RESET", "ESPORTS", new StackPanel
            {
                Children =
                {
                    ToggleRow("Enable Rapid-Trigger (Dynamic Actuation & Release)", tt.RapidTrigger, on => { tt.RapidTrigger = on; PersistProfile($"{tKey} Rapid-Trigger: {(on ? "On" : "Off")}"); }),
                    TwoCol(
                        FineSlider("Actuation Travel", 1, 50, 1, tt.ActuationPct, v => $"{v:0}%",
                            v => { tt.ActuationPct = (int)Math.Round(v); PersistProfile(); }),
                        FineSlider("Release Reset Delta", 1, 35, 1, tt.ResetDeltaPct, v => $"{v:0}%",
                            v => { tt.ResetDeltaPct = (int)Math.Round(v); PersistProfile(); }))
                }
            }));

            centerContent.Children.Add(BentoModule("DUAL-STAGE FULL CLICK OUTPUT", "HYBRID", new StackPanel
            {
                Children =
                {
                    ToggleRow("Enable Dual-Stage Threshold Trigger", tt.DualStage, on => { tt.DualStage = on; PersistProfile(); }),
                    FineSlider("Full Click Threshold", 55, 98, 1, tt.FullPullPct, v => $"{v:0}%",
                        v => { tt.FullPullPct = (int)Math.Round(v); PersistProfile(); }),
                    SegmentRow("Full Click Action",
                        [("Off", ""), ("A", "A"), ("B", "B"), ("X", "X"), ("L3", "L3"), ("R3", "R3"), ("Space", "K:32")],
                        tt.FullPullOutput ?? "", outName => { tt.FullPullOutput = string.IsNullOrEmpty(outName) ? null : outName; PersistProfile(); })
                }
            }));
        }
        else // Competitive
        {
            centerContent.Children.Add(BentoModule("SOCD CLEANER", "STRAFE / FIGHT", new StackPanel
            {
                Children =
                {
                    SegmentRow("D-Pad SOCD Mode",
                        [("Off", SocdMode.Off), ("Neutral", SocdMode.Neutral), ("Last Input", SocdMode.LastInput), ("Up Priority", SocdMode.UpPriority)],
                        profile.DPadSocd, m => { profile.DPadSocd = m; PersistProfile("D-Pad SOCD: " + m); }),
                    SegmentRow("Left Stick SOCD Mode",
                        [("Off", SocdMode.Off), ("Neutral", SocdMode.Neutral), ("Last Input", SocdMode.LastInput)],
                        profile.LeftStickSocd, m => { profile.LeftStickSocd = m; PersistProfile("Stick SOCD: " + m); })
                }
            }));

            var sn = profile.Sniper ??= new SniperModifier();
            centerContent.Children.Add(BentoModule("PRECISION AIM · ADS SNIPER CURVE", "AIM ASSIST", new StackPanel
            {
                Children =
                {
                    ToggleRow("Enable Right Stick Precision Sniper", sn.Enabled, on => { sn.Enabled = on; PersistProfile(); }),
                    ToggleRow("Auto-Engage When Holding LT (ADS Link)", sn.AdsLeftTriggerLink, on => { sn.AdsLeftTriggerLink = on; PersistProfile(); }),
                    SegmentRow("Manual Modifier Button",
                        [("LT", "LT"), ("RT", "RT"), ("LB", "LB"), ("RB", "RB"), ("L3", "L3"), ("R3", "R3")],
                        sn.ModifierButton, btn => { sn.ModifierButton = btn; PersistProfile(); }),
                    FineSlider("Precision Sensitivity Scale", 20, 95, 5, sn.ScalePct, v => $"{v:0}%",
                        v => { sn.ScalePct = (int)Math.Round(v); PersistProfile(); })
                }
            }));

            var sp = profile.AutoSprint ??= new AutoSprintSettings();
            centerContent.Children.Add(BentoModule("OUTER-RING AUTO-SPRINT", "ERGONOMICS", new StackPanel
            {
                Children =
                {
                    ToggleRow("Auto-Trigger L3 at Full Left Stick Push", sp.Enabled, on => { sp.Enabled = on; PersistProfile("Auto-Sprint: " + (on ? "On" : "Off")); }),
                    FineSlider("Outer Ring Threshold", 75, 98, 1, sp.OuterThresholdPct, v => $"{v:0}%",
                        v => { sp.OuterThresholdPct = (int)Math.Round(v); PersistProfile(); })
                }
            }));

            centerContent.Children.Add(BentoModule("VIRTUAL OUTPUT REPORT RATE CAP", "POLLING",
                SegmentRow("Hz Cap",
                    [("Uncapped", 0), ("250 Hz", 250), ("500 Hz", 500), ("750 Hz", 750), ("1000 Hz", 1000)],
                    profile.OutputRateHz, hz => { profile.OutputRateHz = hz; PersistProfile($"Output rate: {(hz == 0 ? "Uncapped" : hz + " Hz")}"); })));
        }

        loading = false;
    }

    void RedrawSpline()
    {
        foreach (var thumb in splineCanvas.Children.OfType<Thumb>().ToList())
            splineCanvas.Children.Remove(thumb);

        var pts = ActiveSplinePoints();
        var line = new PointCollection(101);
        for (int i = 0; i <= 100; i++)
        {
            double xNorm = i / 100.0;
            double yPct = StickLabMath.EvaluateSpline(pts, xNorm) * 100.0;
            line.Add(new Point(CX(i), CY(yPct)));
        }
        splineLine.Points = line;
        var area = new PointCollection(line);
        area.Add(new Point(CX(100), CY(0)));
        area.Add(new Point(CX(0), CY(0)));
        splineArea.Points = area;

        for (int idx = 0; idx < pts.Count; idx++)
        {
            int captured = idx;
            var pt = pts[idx];
            var knob = new Thumb
            {
                Width = 14, Height = 14, Cursor = Cursors.SizeAll,
                Tag = captured,
                Template = NodeTemplate(),
                ToolTip = $"P{idx + 1}: ({pt.X:0}%, {pt.Y:0}%) · Right-click to remove",
            };
            Canvas.SetLeft(knob, CX(pt.X) - 7);
            Canvas.SetTop(knob, CY(pt.Y) - 7);
            knob.DragStarted += (_, _) => draggedPointIndex = captured;
            knob.DragDelta += (_, e) =>
            {
                var list = ActiveSplinePoints();
                if (captured < 0 || captured >= list.Count) return;
                double nextX = Math.Clamp(list[captured].X + e.HorizontalChange / CW * 100.0, captured == 0 ? 0 : list[captured - 1].X + 1, captured == list.Count - 1 ? 100 : list[captured + 1].X - 1);
                double nextY = Math.Clamp(list[captured].Y - e.VerticalChange / CH * 100.0, 0, 100);
                list[captured] = new CurvePoint(Math.Round(nextX, 1), Math.Round(nextY, 1));
                SetActiveSplinePoints(list);
                RedrawSpline();
            };
            knob.DragCompleted += (_, _) => { draggedPointIndex = null; PersistProfile("Spline curve updated"); };
            knob.MouseRightButtonUp += (_, e) =>
            {
                e.Handled = true;
                var list = ActiveSplinePoints();
                if (list.Count <= 2) return;
                list.RemoveAt(captured);
                SetActiveSplinePoints(list);
                PersistProfile("Removed curve node");
                RedrawSpline();
            };
            splineCanvas.Children.Add(knob);
        }

        double midY = StickLabMath.EvaluateSpline(pts, 0.5) * 100.0;
        splineHudText.Text = $"{pts.Count}/10 nodes · 50% -> {midY:0}% ({midY / 50.0:0.00}x)";

        nodeChipPanel.Children.Clear();
        for (int i = 0; i < pts.Count; i++)
        {
            var pt = pts[i];
            nodeChipPanel.Children.Add(new Border
            {
                CornerRadius = new(5),
                Background = new SolidColorBrush(Color.FromArgb(145, 16, 28, 56)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(75, 96, 165, 250)),
                BorderThickness = new(1),
                Padding = new(6, 2, 6, 2),
                Margin = new(0, 0, 4, 4),
                Child = Kit.Text($"P{i + 1} {pt.X:0},{pt.Y:0}", 10, "DS.TextSoft", FontWeights.SemiBold)
            });
        }
    }

    void AddMidpointNode()
    {
        var list = ActiveSplinePoints();
        if (list.Count >= StickLabSettings.MaxCustomPoints)
        {
            statusText.Text = "Maximum 10 control points reached. Right-click a point on the graph to remove one.";
            return;
        }
        int bestIdx = 0;
        double bestGap = -1;
        for (int i = 0; i < list.Count - 1; i++)
        {
            double gap = list[i + 1].X - list[i].X;
            if (gap > bestGap) { bestGap = gap; bestIdx = i; }
        }
        double mx = Math.Round((list[bestIdx].X + list[bestIdx + 1].X) / 2.0, 1);
        double my = Math.Round(StickLabMath.EvaluateSpline(list, mx / 100.0) * 100.0, 1);
        list.Add(new CurvePoint(mx, my));
        list = StickLabMath.NormalizePoints(list);
        SetActiveSplinePoints(list);
        PersistProfile($"Added midpoint node ({mx:0}%, {my:0}%)");
        RedrawSpline();
    }

    void SmoothSplineNodes()
    {
        var list = ActiveSplinePoints();
        if (list.Count <= 2) { statusText.Text = "Need 3+ points to smooth interior nodes."; return; }
        var next = new List<CurvePoint>(list.Count) { list[0] };
        for (int i = 1; i < list.Count - 1; i++)
        {
            double avgY = Math.Round((list[i - 1].Y + list[i].Y * 2.0 + list[i + 1].Y) / 4.0, 1);
            next.Add(new CurvePoint(list[i].X, Math.Clamp(avgY, 0, 100)));
        }
        next.Add(list[^1]);
        SetActiveSplinePoints(next);
        PersistProfile("Smoothed interior spline nodes");
        RedrawSpline();
    }

    void MirrorSplineToOtherStick()
    {
        if (channel is LabChannel.LeftTrigger or LabChannel.RightTrigger)
        {
            string src = channel == LabChannel.LeftTrigger ? "LT" : "RT";
            string dst = channel == LabChannel.LeftTrigger ? "RT" : "LT";
            (profile.Triggers.TryGetValue(dst, out var dt) ? dt : (profile.Triggers[dst] = new TriggerTuning())).CustomPoints = ActiveSplinePoints().ToList();
            PersistProfile($"Mirrored {src} spline to {dst}");
            return;
        }
        string sKey = channel == LabChannel.RightStick ? "RightStick" : "LeftStick";
        string dKey = channel == LabChannel.RightStick ? "LeftStick" : "RightStick";
        var dstTune = GetOrCreateStick(dKey);
        (dstTune.Lab ??= new StickLabSettings()).CustomPoints = ActiveSplinePoints().ToList();
        PersistProfile($"Mirrored {sKey} spline to {dKey}");
    }

    void ToggleBypassCurrentStick()
    {
        string key = channel == LabChannel.RightStick ? "RightStick" : "LeftStick";
        var tune = GetOrCreateStick(key);
        tune.Raw = !tune.Raw;
        PersistProfile(tune.Raw ? $"{key} bypassed (1:1 Raw)" : $"{key} DSP active");
        RebuildCenterControls();
        RedrawSpline();
        UpdateLiveVisualizer(simLx, simLy, simRx, simRy);
    }

    void OnSplineCanvasDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is Thumb) return;
        var pos = e.GetPosition(splineCanvas);
        double pctX = Math.Clamp((pos.X - CL) / CW * 100.0, 1, 99);
        double pctY = Math.Clamp((CT + CH - pos.Y) / CH * 100.0, 0, 100);
        var list = ActiveSplinePoints();
        if (list.Count >= StickLabSettings.MaxCustomPoints)
        {
            statusText.Text = "Maximum 10 control points reached. Right-click a point to remove one.";
            return;
        }
        list.Add(new CurvePoint(Math.Round(pctX, 1), Math.Round(pctY, 1)));
        list = StickLabMath.NormalizePoints(list);
        SetActiveSplinePoints(list);
        PersistProfile($"Added spline node at ({pctX:0}%, {pctY:0}%)");
        RedrawSpline();
    }

    void OnSplineCanvasMove(object sender, MouseEventArgs e) => _ = draggedPointIndex;

    List<CurvePoint> ActiveSplinePoints()
    {
        if (channel is LabChannel.LeftTrigger or LabChannel.RightTrigger)
        {
            string k = channel == LabChannel.LeftTrigger ? "LT" : "RT";
            if (profile.Triggers.TryGetValue(k, out var tt) && tt.CustomPoints is { Count: >= 2 })
                return tt.CustomPoints.ToList();
            return [new(0, 0), new(50, 50), new(100, 100)];
        }
        string stick = channel == LabChannel.RightStick ? "RightStick" : "LeftStick";
        if (profile.Sticks.TryGetValue(stick, out var st) && st.Lab?.CustomPoints is { Count: >= 2 })
            return st.Lab.CustomPoints.ToList();
        return [new(0, 0), new(33, 33), new(67, 67), new(100, 100)];
    }

    void SetActiveSplinePoints(List<CurvePoint> points)
    {
        var clean = StickLabMath.NormalizePoints(points);
        if (channel is LabChannel.LeftTrigger or LabChannel.RightTrigger)
        {
            string k = channel == LabChannel.LeftTrigger ? "LT" : "RT";
            var tt = profile.Triggers.TryGetValue(k, out var ex) ? ex : (profile.Triggers[k] = new TriggerTuning());
            tt.CustomPoints = clean;
            return;
        }
        string stick = channel == LabChannel.RightStick ? "RightStick" : "LeftStick";
        var st = GetOrCreateStick(stick);
        (st.Lab ??= new StickLabSettings()).CustomPoints = clean;
    }

    void ApplySmartCurve(string kind)
    {
        var pts = StickLabMath.GenerateSmartCurve(kind);
        SetActiveSplinePoints(pts);
        PersistProfile($"Applied curve archetype: {kind}");
        RedrawSpline();
    }

    string ExportCurrentShareCode()
    {
        var cc = channel switch
        {
            LabChannel.RightStick => CurveChannel.RightStick,
            LabChannel.LeftTrigger => CurveChannel.LeftTrigger,
            LabChannel.RightTrigger => CurveChannel.RightTrigger,
            _ => CurveChannel.LeftStick,
        };
        var curve = profile.Curves.GetValueOrDefault(cc.ToString()) ?? new SetupCurve("custom", "default", 0, 100, 85, 85, 170, 170);
        StickTuning? st = cc is CurveChannel.LeftStick or CurveChannel.RightStick ? profile.Sticks.GetValueOrDefault(cc.ToString()) : null;
        return StickLabShareCode.Encode(cc, curve, st, profile.DPadSocd, profile.LeftStickSocd);
    }

    void CopyShareCode()
    {
        string code = ExportCurrentShareCode();
        codeBox.Text = code;
        try
        {
            Clipboard.SetText(code);
            shareStatusText.Text = "Copied X20C1 share code to clipboard.";
        }
        catch
        {
            shareStatusText.Text = "Share code generated above (clipboard busy).";
        }
    }

    void ApplyShareCode()
    {
        if (!StickLabShareCode.TryDecode(codeBox.Text, out var decoded, out string err) || decoded == null)
        {
            shareStatusText.Text = err;
            return;
        }
        profile.Curves[decoded.Channel] = decoded.Curve;
        if (decoded.Tuning != null && decoded.Channel is "LeftStick" or "RightStick")
            profile.Sticks[decoded.Channel] = decoded.Tuning;
        profile.DPadSocd = decoded.DPadSocd;
        profile.LeftStickSocd = decoded.LeftStickSocd;
        PersistProfile($"Imported share code into {decoded.Channel}");
        if (Enum.TryParse<LabChannel>(decoded.Channel, out var ch)) SelectChannel(ch);
        else { RebuildCenterControls(); RedrawSpline(); }
        shareStatusText.Text = $"Applied X20C1 share code cleanly ({decoded.Channel}).";
    }

    void UseMeasuredDriftAsDeadzone()
    {
        bool right = channel == LabChannel.RightStick;
        var st = right ? diagStatsRight : diagStatsLeft;
        if (st.Rest is not { } rest)
        {
            statusText.Text = "Release the stick briefly first so center resting drift can be measured.";
            return;
        }
        double mag = Math.Sqrt(rest.X * rest.X + rest.Y * rest.Y);
        double suggested = Math.Clamp(Math.Ceiling((mag + 0.5) * 10.0) / 10.0, 0.5, StickTuning.MaxInner);
        var tune = GetOrCreateStick(right ? "RightStick" : "LeftStick");
        tune.Inner = suggested;
        PersistProfile($"Inner deadzone set to {suggested:0.0}% from measured drift ({mag:0.0}%)");
        RebuildCenterControls();
        RedrawSpline();
    }

    void ResetActiveChannel()
    {
        if (channel is LabChannel.LeftStick or LabChannel.RightStick)
        {
            string k = channel == LabChannel.RightStick ? "RightStick" : "LeftStick";
            profile.Sticks.Remove(k);
            profile.Curves.Remove(k);
        }
        else if (channel is LabChannel.LeftTrigger or LabChannel.RightTrigger)
        {
            string k = channel == LabChannel.LeftTrigger ? "LT" : "RT";
            profile.Triggers.Remove(k);
        }
        else
        {
            profile.DPadSocd = SocdMode.Off;
            profile.LeftStickSocd = SocdMode.Off;
            profile.Sniper = null;
            profile.AutoSprint = null;
            profile.OutputRateHz = 0;
        }
        PersistProfile($"Reset {channel} settings to defaults");
        RebuildCenterControls();
        RedrawSpline();
    }

    StickTuning GetOrCreateStick(string key) => profile.Sticks.TryGetValue(key, out var ex) ? ex : (profile.Sticks[key] = new StickTuning());

    static StickTuning CloneStick(StickTuning t) => new()
    {
        Inner = t.Inner, Outer = t.Outer, SquareGate = t.SquareGate, AxisRatio = t.AxisRatio,
        AntiDeadzone = t.AntiDeadzone, MaxOutput = t.MaxOutput, AutoCenterDrift = t.AutoCenterDrift,
        Raw = t.Raw, Lab = t.Lab?.Clone(),
    };

    PcModeProfile LoadProfile()
    {
        if (link == null) return new PcModeProfile();
        try { return link.Store.Get(PcModePage.StoreKey); }
        catch { return new PcModeProfile(); }
    }

    void PersistProfile(string? note = null)
    {
        if (loading) return;
        if (link == null)
        {
            statusText.Text = (note != null ? note + " · " : "") + "Previewing locally (enable PC Mode to apply live)";
            return;
        }
        try
        {
            profile.Validate();
            if (!profile.IsNeutral && !PcModePage.MaySave(link.Preferences))
            {
                statusText.Text = "Saved draft in Analog Studio · Enable PC Mode notice to apply live";
                return;
            }
            link.Store.Save(PcModePage.StoreKey, profile);
            link.Service.Reload();
            statusText.Text = note ?? (link.Service.Enabled ? "Saved and active in PC Mode right now" : "Saved to PC Mode profile (enable PC Mode to run live)");
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            statusText.Text = "Could not save: " + ex.Message;
        }
        RefreshPcModeBadge();
    }

    void RefreshPcModeBadge()
    {
        bool live = link?.Service.Enabled == true;
        pcModeBadge.Text = live ? "● PC Mode Active (Live)" : "○ PC Mode Off (Draft Preview)";
        pcModeBadge.Foreground = DS.Brush(live ? "DS.Success" : "DS.Warn");
        enablePcBtn.Visibility = !live && requestEnablePcMode != null ? Visibility.Visible : Visibility.Collapsed;
    }

    // ---- Visual Helpers ----
    static double CX(double pct) => CL + Math.Clamp(pct, 0, 100) / 100.0 * CW;
    static double CY(double pct) => CT + CH - Math.Clamp(pct, 0, 100) / 100.0 * CH;
    static short ToShort(double v) => (short)Math.Clamp(Math.Round(v * 32767.0), -32768, 32767);

    static ControlTemplate NodeTemplate()
    {
        var f = new FrameworkElementFactory(typeof(Ellipse));
        f.SetValue(Shape.FillProperty, new SolidColorBrush(Color.FromRgb(116, 200, 255)));
        f.SetValue(Shape.StrokeProperty, Brushes.White);
        f.SetValue(Shape.StrokeThicknessProperty, 2.0);
        return new ControlTemplate(typeof(Thumb)) { VisualTree = f };
    }

    static Border GlassCard(string title, UIElement child) => new()
    {
        CornerRadius = new(14),
        BorderThickness = new(1),
        BorderBrush = new SolidColorBrush(Color.FromArgb(75, 125, 175, 240)),
        Background = new SolidColorBrush(ThemeCatalog.R(Color.FromArgb(195, 10, 19, 38))),
        Padding = new(14, 12, 14, 12),
        Child = new DockPanel
        {
            Children =
            {
                Kit.Text(title, 10.5, "DS.TextMuted", FontWeights.Bold).Also(t => { DockPanel.SetDock(t, Dock.Top); t.Margin = new(0, 0, 0, 8); }),
                child,
            }
        }
    };

    static Border DarkWell(UIElement child, Thickness pad) => new()
    {
        CornerRadius = new(10),
        BorderThickness = new(1),
        BorderBrush = new SolidColorBrush(Color.FromArgb(62, 96, 155, 230)),
        Background = new SolidColorBrush(ThemeCatalog.R(Color.FromRgb(6, 13, 28))),
        Padding = pad,
        Child = child,
    };

    static Border BentoModule(string title, string badge, UIElement content)
    {
        var header = new DockPanel { Margin = new(0, 0, 0, 8) };
        var pill = new Border
        {
            CornerRadius = new(5),
            Background = new SolidColorBrush(Color.FromArgb(48, 56, 189, 248)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(95, 56, 189, 248)),
            BorderThickness = new(1),
            Padding = new(6, 1, 6, 1),
            VerticalAlignment = VerticalAlignment.Center,
            Child = Kit.Text(badge, 9, "DS.AccentHi", FontWeights.Bold)
        };
        DockPanel.SetDock(pill, Dock.Right);
        header.Children.Add(pill);
        header.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new Ellipse { Width = 6, Height = 6, Fill = DS.Brush("DS.AccentHi"), Margin = new(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center },
                Kit.Text(title, 10.5, "DS.Text", FontWeights.Bold)
            }
        });
        return DarkWell(new StackPanel { Children = { header, content } }, new Thickness(12, 10, 12, 10)).Also(b => b.Margin = new(0, 0, 0, 10));
    }

    static Grid TwoCol(UIElement left, UIElement right)
    {
        var g = new Grid();
        g.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new() { Width = new(12) });
        g.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        g.Children.Add(left);
        Grid.SetColumn(right, 2);
        g.Children.Add(right);
        return g;
    }

    static Button ActionBtn(string label, Action onClick, bool primary)
    {
        var b = new Button
        {
            Content = label,
            Style = DS.Style(primary ? "DS.ConsolePrimary" : "ActionButton"),
            Height = 34, MinHeight = 0,
            Padding = new(12, 0, 12, 0),
            FontSize = 12,
            Cursor = Cursors.Hand,
        };
        b.Click += (_, _) => onClick();
        return b;
    }

    FrameworkElement FineSlider(string label, double min, double max, double step, double initial, Func<double, string> fmt, Action<double> onChanged)
    {
        var val = Kit.Text(fmt(initial), 11.5, "DS.Text", FontWeights.SemiBold);
        DockPanel.SetDock(val, Dock.Right);
        var head = new DockPanel { Margin = new(0, 0, 0, 1), Children = { val, Kit.Text(label, 11.5, "DS.TextSoft").Also(t => t.TextTrimming = TextTrimming.CharacterEllipsis) } };
        var sl = new Slider
        {
            Minimum = min, Maximum = max, Value = Math.Clamp(initial, min, max),
            SmallChange = step, LargeChange = step * 5, TickFrequency = step, IsSnapToTickEnabled = true,
            Style = DS.Style("DS.Slider"), Height = 20,
        };
        sl.ValueChanged += (_, _) => { val.Text = fmt(sl.Value); if (!loading) onChanged(sl.Value); };
        return new StackPanel { Margin = new(0, 2, 0, 6), Children = { head, sl } };
    }

    FrameworkElement ToggleRow(string label, bool initial, Action<bool> onChanged)
    {
        var cb = new CheckBox { Style = DS.Style("DS.Toggle"), IsChecked = initial, VerticalAlignment = VerticalAlignment.Center };
        cb.Checked += (_, _) => { if (!loading) onChanged(true); };
        cb.Unchecked += (_, _) => { if (!loading) onChanged(false); };
        var dock = new DockPanel { Margin = new(0, 3, 0, 6) };
        DockPanel.SetDock(cb, Dock.Right);
        dock.Children.Add(cb);
        dock.Children.Add(Kit.Text(label, 11.5, "DS.Text").Also(t => { t.VerticalAlignment = VerticalAlignment.Center; t.TextTrimming = TextTrimming.CharacterEllipsis; t.Margin = new(0, 0, 6, 0); }));
        return dock;
    }

    FrameworkElement SegmentRow<T>(string label, (string Title, T Value)[] options, T current, Action<T> onPicked)
    {
        var wrap = new WrapPanel { Margin = new(0, 3, 0, 6) };
        var buttons = new List<(Button Btn, T Val)>();
        foreach (var (title, value) in options)
        {
            var captured = value;
            bool selected = EqualityComparer<T>.Default.Equals(value, current);
            var btn = new Button
            {
                Content = title, Height = 27, MinHeight = 0, Padding = new(10, 0, 10, 0), Margin = new(0, 0, 5, 4),
                FontSize = 11.5, Style = DS.Style("ActionButton"),
                Background = selected ? new SolidColorBrush(Color.FromRgb(42, 108, 212)) : new SolidColorBrush(Color.FromArgb(145, 14, 24, 48)),
                BorderBrush = selected ? Brushes.White : DS.Brush("DS.Hairline"),
            };
            btn.Click += (_, _) =>
            {
                foreach (var item in buttons)
                {
                    bool act = EqualityComparer<T>.Default.Equals(item.Val, captured);
                    item.Btn.Background = act ? new SolidColorBrush(Color.FromRgb(42, 108, 212)) : new SolidColorBrush(Color.FromArgb(145, 14, 24, 48));
                    item.Btn.BorderBrush = act ? Brushes.White : DS.Brush("DS.Hairline");
                }
                if (!loading) onPicked(captured);
            };
            buttons.Add((btn, captured));
            wrap.Children.Add(btn);
        }
        return new StackPanel { Children = { Kit.Text(label, 11.5, "DS.TextSoft"), wrap } };
    }
}
