using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using X20Ctl.Product.Scanner;
using X20Ctl.Zone;
using X20Ctl.Zone.Diagnostics;
using X20Ctl.Zone.PcMode;

namespace X20Ctl.Product;

/// <summary>
/// Full-screen PS5 slide-down Stick Lab zone: 10-point spline curves, Radial/Axial/Hybrid deadzones,
/// Radial+Axial anti-deadzone, Gate Morph, Rotation, Angle Snap, Velocity Smoothing, Invert/Swap,
/// SOCD, Sniper, Auto-Sprint, Trigger Lab (Rapid-Trigger &amp; Dual-Stage), live Dual-Dot diagnostics,
/// Snapback peak meter, and X20C1 share codes.
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
    readonly TextBlock statusText = Kit.Text("", 13, "DS.TextSoft");
    readonly TextBlock pcModeBadge = Kit.Text("", 12.5, "DS.Text", FontWeights.SemiBold);
    readonly Button enablePcBtn;
    readonly StackPanel channelBar = new() { Orientation = Orientation.Horizontal };
    readonly StackPanel centerContent = new();
    readonly List<Button> channelButtons = [];

    // 10-point spline canvas
    const double CL = 40, CT = 18, CW = 380, CH = 250;
    readonly Canvas splineCanvas = new() { Width = 446, Height = 300, Background = Brushes.Transparent };
    readonly Polyline splineLine = new()
    {
        Stroke = new SolidColorBrush(Color.FromRgb(96, 188, 255)),
        StrokeThickness = 3.2,
        StrokeLineJoin = PenLineJoin.Round,
    };
    readonly Polygon splineArea = new()
    {
        Fill = new LinearGradientBrush(Color.FromArgb(48, 61, 139, 255), Color.FromArgb(0, 61, 139, 255), 90),
    };
    readonly Ellipse splineLiveDot = new()
    {
        Width = 12, Height = 12,
        Fill = new SolidColorBrush(Color.FromRgb(61, 214, 140)),
        Stroke = Brushes.White, StrokeThickness = 1.5,
        Visibility = Visibility.Collapsed, IsHitTestVisible = false,
    };
    readonly TextBlock pointsSummaryText = Kit.Text("", 12, "DS.TextSoft");

    // Live Dual-Dot Diagnostics Visualizer
    const double VisSize = 236, VisHalf = 118, VisUnit = 92;
    readonly Canvas visCanvas = new() { Width = VisSize, Height = VisSize, ClipToBounds = true };
    readonly Polygon gatePolygon = new()
    {
        Stroke = new SolidColorBrush(Color.FromArgb(145, 116, 200, 255)),
        StrokeThickness = 1.6,
        Fill = new SolidColorBrush(Color.FromArgb(20, 61, 139, 255)),
    };
    readonly Polygon reachTrace = new()
    {
        Stroke = new SolidColorBrush(Color.FromArgb(170, 56, 189, 248)),
        StrokeThickness = 1.4,
        Fill = new SolidColorBrush(Color.FromArgb(24, 56, 189, 248)),
    };
    readonly Shape deadzoneOverlay = new Ellipse();
    readonly System.Windows.Shapes.Path axialCrossOverlay = new()
    {
        Stroke = new SolidColorBrush(Color.FromArgb(130, 232, 54, 79)),
        StrokeThickness = 1.4,
        Fill = new SolidColorBrush(Color.FromArgb(30, 232, 54, 79)),
    };
    readonly Ellipse innerDzRing = new()
    {
        Stroke = new SolidColorBrush(Color.FromArgb(150, 232, 54, 79)),
        StrokeThickness = 1.4,
        Fill = new SolidColorBrush(Color.FromArgb(28, 232, 54, 79)),
    };
    readonly Ellipse outerDzRing = new()
    {
        Stroke = new SolidColorBrush(Color.FromArgb(120, 61, 214, 140)),
        StrokeThickness = 1.4,
        StrokeDashArray = [4, 3],
    };
    readonly Ellipse rawDot = new() { Width = 10, Height = 10, Fill = new SolidColorBrush(Color.FromArgb(200, 180, 195, 220)) };
    readonly Ellipse outDot = new()
    {
        Width = 14, Height = 14,
        Fill = new SolidColorBrush(Color.FromRgb(56, 214, 168)),
        Stroke = Brushes.White, StrokeThickness = 1.8,
    };
    readonly TextBlock diagMetricsText = Kit.Text("", 12.5, "DS.TextSoft");
    readonly TextBlock shareStatusText = Kit.Text("", 12, "DS.TextMuted");
    readonly TextBox codeBox = new()
    {
        Height = 32, MinWidth = 180, MaxWidth = 260, FontSize = 12,
        Padding = new(8, 5, 8, 5), Foreground = Brushes.White,
        Background = new SolidColorBrush(Color.FromArgb(180, 10, 18, 34)),
        BorderBrush = DS.Brush("DS.Hairline"), VerticalContentAlignment = VerticalAlignment.Center,
    };

    readonly DispatcherTimer liveTimer;
    readonly WindowsReader? reader;
    readonly StickStats diagStatsLeft = new(), diagStatsRight = new();
    readonly StickSnapbackDetector snapLeft = new(), snapRight = new();
    double simRx, simRy, simLx, simLy;
    bool hasRealInput;
    PadState lastPad;

    public StickLabZone(ControllerModel model, int playerSlot, PcModeLink? link, Action close, Action? requestEnablePcMode = null, Action<string>? shareRequested = null)
    {
        this.model = model;
        this.playerSlot = playerSlot;
        this.link = link;
        this.close = close;
        this.requestEnablePcMode = requestEnablePcMode;
        this.shareRequested = shareRequested;

        Background = new LinearGradientBrush(
            ThemeCatalog.R(Color.FromRgb(7, 13, 28)),
            ThemeCatalog.R(Color.FromRgb(4, 8, 20)), 90);
        BorderThickness = new(0);
        Padding = new(24, 18, 24, 18);

        profile = LoadProfile();

        if (!ReviewSandbox.Active)
        {
            try { reader = new WindowsReader(WindowsReader.Libraries[0]); } catch { reader = null; }
        }

        var root = new Grid();
        root.RowDefinitions.Add(new() { Height = GridLength.Auto });
        root.RowDefinitions.Add(new() { Height = new(14) });
        root.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        Child = root;

        // ---- TOP HEADER ----
        var header = new DockPanel();
        ExitButton = ActionBtn("< Return to Curves", close, primary: false);
        ExitButton.MinWidth = 156;
        ExitButton.Margin = new(0, 0, 16, 0);
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
            CornerRadius = new(10),
            BorderThickness = new(1),
            BorderBrush = DS.Brush("DS.Hairline"),
            Background = new SolidColorBrush(Color.FromArgb(140, 14, 24, 46)),
            Padding = new(12, 6, 12, 6),
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
            Margin = new(0, 0, 12, 0),
            Background = new SolidColorBrush(Color.FromArgb(75, 56, 189, 248)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(140, 56, 189, 248)),
            BorderThickness = new(1),
            Child = Kit.Text("STICK LAB", 13, "DS.Text", FontWeights.Bold),
        });
        titleStack.Children.Add(Kit.Text(model.Name.Replace("EasySMX ", ""), 16, "DS.Text", FontWeights.SemiBold).Also(t => { t.VerticalAlignment = VerticalAlignment.Center; t.Margin = new(0, 0, 18, 0); }));

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
            btn.Height = 34;
            btn.Margin = new(0, 0, 6, 0);
            btn.Padding = new(12, 0, 12, 0);
            channelButtons.Add(btn);
            channelBar.Children.Add(btn);
        }
        titleStack.Children.Add(channelBar);
        header.Children.Add(titleStack);
        root.Children.Add(header);

        // ---- 3-COLUMN WORKSPACE ----
        var body = new Grid();
        body.ColumnDefinitions.Add(new() { Width = new(1.02, GridUnitType.Star) });
        body.ColumnDefinitions.Add(new() { Width = new(14) });
        body.ColumnDefinitions.Add(new() { Width = new(1.18, GridUnitType.Star) });
        body.ColumnDefinitions.Add(new() { Width = new(14) });
        body.ColumnDefinitions.Add(new() { Width = new(0.95, GridUnitType.Star) });
        Grid.SetRow(body, 2);
        root.Children.Add(body);

        // Column 1: 10-Point Spline Curve Editor + Smart Curve Maker + Share Code Bar
        body.Children.Add(BuildSplineColumn());

        // Column 2: Scrollable Complete Parameters Studio
        var centerScroll = new ScrollViewer
        {
            Content = centerContent,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Padding = new(0, 0, 6, 0),
        };
        var centerCard = GlassCard("TUNING PARAMETERS · LIVE DSP", centerScroll);
        Grid.SetColumn(centerCard, 2);
        body.Children.Add(centerCard);

        // Column 3: Live Dual-Dot Visualizer + Circularity + Snapback Meter
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
        var stack = new StackPanel();

        // Build chart gridlines once
        for (int t = 0; t <= 100; t += 25)
        {
            double gx = CX(t), gy = CY(t);
            splineCanvas.Children.Add(new Line { X1 = gx, X2 = gx, Y1 = CT, Y2 = CT + CH, Stroke = new SolidColorBrush(Color.FromArgb(40, 140, 175, 255)), StrokeThickness = 1 });
            splineCanvas.Children.Add(new Line { X1 = CL, X2 = CL + CW, Y1 = gy, Y2 = gy, Stroke = new SolidColorBrush(Color.FromArgb(40, 140, 175, 255)), StrokeThickness = 1 });
            var tx = new TextBlock { Text = t.ToString(), FontSize = 11, Foreground = DS.Brush("DS.TextMuted") };
            Canvas.SetLeft(tx, gx - 8); Canvas.SetTop(tx, CT + CH + 6);
            splineCanvas.Children.Add(tx);
            var ty = new TextBlock { Text = t.ToString(), FontSize = 11, Foreground = DS.Brush("DS.TextMuted") };
            Canvas.SetLeft(ty, 8); Canvas.SetTop(ty, gy - 8);
            splineCanvas.Children.Add(ty);
        }
        splineCanvas.Children.Add(new Line
        {
            X1 = CL, Y1 = CT + CH, X2 = CL + CW, Y2 = CT,
            Stroke = new SolidColorBrush(Color.FromArgb(80, 200, 220, 255)),
            StrokeDashArray = [4, 4], StrokeThickness = 1.2,
        });
        splineCanvas.Children.Add(splineArea);
        splineCanvas.Children.Add(splineLine);
        splineCanvas.Children.Add(splineLiveDot);

        splineCanvas.MouseLeftButtonDown += OnSplineCanvasDown;
        splineCanvas.MouseMove += OnSplineCanvasMove;
        splineCanvas.MouseLeftButtonUp += (_, _) => { draggedPointIndex = null; splineCanvas.ReleaseMouseCapture(); };

        stack.Children.Add(new Viewbox { MaxHeight = 310, Stretch = Stretch.Uniform, Child = splineCanvas });
        stack.Children.Add(Kit.Text("Click graph to add point (up to 10) · Drag to shape · Right-click a node to remove", 11.5, "DS.TextMuted").Also(t => { t.Margin = new(0, 4, 0, 8); t.TextWrapping = TextWrapping.Wrap; }));

        stack.Children.Add(Kit.Text("SMART CURVE MAKER", 10.5, "DS.TextMuted", FontWeights.SemiBold).Also(t => t.Margin = new(0, 0, 0, 6)));
        var makerGrid = new UniformGrid { Columns = 3, Margin = new(-2, 0, -2, 8) };
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
            var b = ActionBtn(label, () => ApplySmartCurve(k), primary: false);
            b.Height = 30; b.FontSize = 11.5; b.Margin = new(2);
            makerGrid.Children.Add(b);
        }
        stack.Children.Add(makerGrid);
        stack.Children.Add(pointsSummaryText.Also(t => { t.TextWrapping = TextWrapping.Wrap; t.Margin = new(0, 0, 0, 10); }));

        // Share code bar
        stack.Children.Add(Kit.Rule().Also(r => r.Margin = new(0, 4, 0, 10)));
        stack.Children.Add(Kit.Text("CURVE SHARE CODE (X20C1)", 10.5, "DS.TextMuted", FontWeights.SemiBold).Also(t => t.Margin = new(0, 0, 0, 6)));
        var codeRow = new WrapPanel { Margin = new(0, 0, 0, 4) };
        codeRow.Children.Add(codeBox.Also(b => b.Margin = new(0, 0, 6, 4)));
        codeRow.Children.Add(ActionBtn("Copy", CopyShareCode, primary: false).Also(b => { b.Height = 32; b.Margin = new(0, 0, 6, 4); }));
        codeRow.Children.Add(ActionBtn("Apply Code", ApplyShareCode, primary: false).Also(b => { b.Height = 32; b.Margin = new(0, 0, 6, 4); }));
        if (shareRequested != null)
            codeRow.Children.Add(ActionBtn("Share Online", () => shareRequested(ExportCurrentShareCode()), primary: true).Also(b => { b.Height = 32; b.Margin = new(0, 0, 0, 4); }));
        stack.Children.Add(codeRow);
        stack.Children.Add(shareStatusText);

        return GlassCard("10-POINT SPLINE CURVE DESIGNER", stack);
    }

    Border BuildDiagnosticsColumn()
    {
        var stack = new StackPanel();
        visCanvas.Children.Add(new Ellipse
        {
            Width = VisUnit * 2, Height = VisUnit * 2,
            Stroke = new SolidColorBrush(Color.FromArgb(60, 140, 175, 255)),
            StrokeThickness = 1.2,
        }.Also(e => { Canvas.SetLeft(e, VisHalf - VisUnit); Canvas.SetTop(e, VisHalf - VisUnit); }));
        visCanvas.Children.Add(new Line { X1 = VisHalf, X2 = VisHalf, Y1 = VisHalf - VisUnit, Y2 = VisHalf + VisUnit, Stroke = new SolidColorBrush(Color.FromArgb(40, 140, 175, 255)) });
        visCanvas.Children.Add(new Line { Y1 = VisHalf, Y2 = VisHalf, X1 = VisHalf - VisUnit, X2 = VisHalf + VisUnit, Stroke = new SolidColorBrush(Color.FromArgb(40, 140, 175, 255)) });
        visCanvas.Children.Add(gatePolygon);
        visCanvas.Children.Add(reachTrace);
        visCanvas.Children.Add(axialCrossOverlay);
        visCanvas.Children.Add(innerDzRing);
        visCanvas.Children.Add(outerDzRing);
        visCanvas.Children.Add(rawDot);
        visCanvas.Children.Add(outDot);

        stack.Children.Add(new Viewbox { MaxHeight = 250, Child = visCanvas, Margin = new(0, 0, 0, 8) });

        var legend = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new(0, 0, 0, 10) };
        legend.Children.Add(new Ellipse { Width = 9, Height = 9, Fill = new SolidColorBrush(Color.FromRgb(180, 195, 220)), Margin = new(0, 0, 5, 0) });
        legend.Children.Add(Kit.Text("Raw Input", 11.5, "DS.TextSoft").Also(t => t.Margin = new(0, 0, 14, 0)));
        legend.Children.Add(new Ellipse { Width = 10, Height = 10, Fill = new SolidColorBrush(Color.FromRgb(56, 214, 168)), Margin = new(0, 0, 5, 0) });
        legend.Children.Add(Kit.Text("Game Output", 11.5, "DS.Text"));
        stack.Children.Add(legend);

        stack.Children.Add(diagMetricsText.Also(t => { t.TextWrapping = TextWrapping.Wrap; t.LineHeight = 20; t.Margin = new(0, 0, 0, 10); }));

        var diagActions = new WrapPanel();
        diagActions.Children.Add(ActionBtn("Use Drift as Deadzone", UseMeasuredDriftAsDeadzone, primary: false).Also(b => { b.Height = 32; b.Margin = new(0, 0, 6, 6); }));
        diagActions.Children.Add(ActionBtn("Reset Trace", () =>
        {
            diagStatsLeft.Reset(); diagStatsRight.Reset();
            snapLeft.Reset(); snapRight.Reset();
            UpdateLiveVisualizer(0, 0, 0, 0);
        }, primary: false).Also(b => { b.Height = 32; b.Margin = new(0, 0, 6, 6); }));
        diagActions.Children.Add(ActionBtn("Reset Channel", ResetActiveChannel, primary: false).Also(b => { b.Height = 32; b.Margin = new(0, 0, 0, 6); }));
        stack.Children.Add(diagActions);
        stack.Children.Add(statusText.Also(t => { t.TextWrapping = TextWrapping.Wrap; t.Margin = new(0, 8, 0, 0); }));

        return GlassCard("LIVE DIAGNOSTICS · RAW VS PROCESSED", stack);
    }

    public void SelectChannel(LabChannel next)
    {
        channel = next;
        foreach (var b in channelButtons)
        {
            bool active = b.Tag is LabChannel c && c == channel;
            b.Background = active ? new SolidColorBrush(Color.FromRgb(56, 128, 232)) : new SolidColorBrush(Color.FromArgb(140, 18, 30, 58));
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
        lastPad = new PadState(0, 0, 0, ToShort(lx), ToShort(ly), ToShort(rx), ToShort(ry));
        UpdateLiveVisualizer(lx, ly, rx, ry);
    }

    public void ClearLive()
    {
        hasRealInput = false;
        simLx = simLy = simRx = simRy = 0;
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
                lastPad = new PadState(p.Buttons, p.LT, p.RT, p.LX, p.LY, p.RX, p.RY);
                diagStatsLeft.Add(p.LX, p.LY);
                diagStatsRight.Add(p.RX, p.RY);
                snapLeft.Observe(p.LX, p.LY);
                snapRight.Observe(p.RX, p.RY);
                simLx = p.LX / 32767.0; simLy = p.LY / 32767.0;
                simRx = p.RX / 32767.0; simRy = p.RY / 32767.0;
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

        double rawX = right ? rx : lx;
        double rawY = right ? ry : ly;
        short inX = ToShort(rawX), inY = ToShort(rawY);
        var (outXs, outYs) = PadProcessor.Stick(inX, inY, curve, tune);
        double outX = outXs / 32767.0, outY = outYs / 32767.0;

        Canvas.SetLeft(rawDot, VisHalf + rawX * VisUnit - 5);
        Canvas.SetTop(rawDot, VisHalf - rawY * VisUnit - 5);
        Canvas.SetLeft(outDot, VisHalf + outX * VisUnit - 7);
        Canvas.SetTop(outDot, VisHalf - outY * VisUnit - 7);

        // Draw Gate Morph outline (0% circle -> 100% square)
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

        // Deadzone shape overlay
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

        // Reach trace from active stick
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

        // Update metrics readout
        double rawMag = Math.Min(1.0, Math.Sqrt(rawX * rawX + rawY * rawY));
        double outMag = Math.Min(1.0, Math.Sqrt(outX * outX + outY * outY));
        string circ = st.CircularityError is { } ce ? $"{ce:0.0}%" : $"{st.Covered * 100 / StickStats.Sectors}% swept";
        string rest = st.Rest is { } rd ? $"{rd.X:+0.0;-0.0;0.0}%, {rd.Y:+0.0;-0.0;0.0}%" : "Release stick to measure";
        string bits = st.ResolutionBits is { } bt ? $"{bt}-bit" : "Move slowly";
        diagMetricsText.Text =
            $"{(right ? "Right Stick" : "Left Stick")} · {(hasRealInput ? "Live Hardware" : "Idle / Ready")}\n" +
            $"Raw ({rawX:0.00}, {rawY:0.00}) -> Output ({outX:0.00}, {outY:0.00}) [{outMag:P0}]\n" +
            $"Circularity: {circ}  ·  Resolution: {bits}\n" +
            $"Resting center: {rest}\n" +
            $"Snapback peak: {sb.PeakBouncePct:0.0}% ({sb.BounceEvents} bounces)";

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

            centerContent.Children.Add(SectionLabel("DEADZONE & ANTI-DEADZONE"));
            centerContent.Children.Add(SegmentRow("Shape",
                [("Radial", DeadzoneShape.Radial), ("Axial (Cross)", DeadzoneShape.Axial), ("Hybrid", DeadzoneShape.Hybrid)],
                lab.DeadzoneShape, shape => { lab.DeadzoneShape = shape; PersistProfile("Deadzone shape: " + shape); }));

            centerContent.Children.Add(FineSlider("Inner Deadzone", 0, 40, 0.1, tune.Inner, v => $"{v:0.0}%",
                v => { tune.Inner = Math.Round(v, 1); if (tune.Outer <= tune.Inner + 1) tune.Outer = Math.Min(100, tune.Inner + 5); PersistProfile(); RedrawSpline(); }));
            centerContent.Children.Add(FineSlider("Outer Threshold (Full At)", 60, 100, 0.5, tune.Outer, v => $"{v:0.0}%",
                v => { tune.Outer = Math.Max(tune.Inner + 2, Math.Round(v, 1)); PersistProfile(); RedrawSpline(); }));
            double radialAnti = lab.RadialAntiDeadzone > 0 ? lab.RadialAntiDeadzone : tune.AntiDeadzone;
            centerContent.Children.Add(FineSlider("Radial Anti-Deadzone", 0, 40, 0.5, radialAnti, v => $"{v:0.0}%",
                v => { lab.RadialAntiDeadzone = tune.AntiDeadzone = Math.Round(v, 1); PersistProfile(); RedrawSpline(); }));
            centerContent.Children.Add(FineSlider("Axial Anti-Deadzone (Cross Floor)", 0, 30, 0.5, lab.AxialAntiDeadzone, v => $"{v:0.0}%",
                v => { lab.AxialAntiDeadzone = Math.Round(v, 1); PersistProfile(); }));
            centerContent.Children.Add(FineSlider("Maximum Output Cap", 60, 100, 1, tune.MaxOutput, v => $"{v:0}%",
                v => { tune.MaxOutput = Math.Round(v); PersistProfile(); RedrawSpline(); }));

            centerContent.Children.Add(SectionLabel("GEOMETRY, TILT & RESPONSE FEEL"));
            double gateVal = tune.SquareGate ? 100 : lab.GateMorph;
            centerContent.Children.Add(FineSlider("Gate Shape Morph (Circle <-> Square)", 0, 100, 5, gateVal, v => v >= 99 ? "100% Square" : v <= 1 ? "0% Circle" : $"{v:0}%",
                v => { lab.GateMorph = v; tune.SquareGate = v >= 99; PersistProfile(); }));
            centerContent.Children.Add(FineSlider("Stick Rotation / Thumb Tilt", -45, 45, 1, lab.RotationDegrees, v => $"{v:+0;-0;0} deg",
                v => { lab.RotationDegrees = Math.Round(v); PersistProfile(); }));
            centerContent.Children.Add(FineSlider("Cardinal / 8-Way Angle Snap", 0, 15, 0.5, lab.AngleSnapDegrees, v => v <= 0.1 ? "Off" : $"{v:0.0} deg",
                v => { lab.AngleSnapDegrees = Math.Round(v, 1); PersistProfile(); }));
            centerContent.Children.Add(ToggleRow("8-Way Diagonal Angle Snap (instead of 4 cardinal)", lab.AngleSnap8Way, on => { lab.AngleSnap8Way = on; PersistProfile(); }));
            centerContent.Children.Add(FineSlider("Vertical / Axis Ratio", StickTuning.MinRatio, StickTuning.MaxRatio, 5, tune.AxisRatio, v => $"{v:0}%",
                v => { tune.AxisRatio = (int)Math.Round(v); PersistProfile(); }));
            centerContent.Children.Add(FineSlider("Velocity-Aware Smoothing (Noise Filter)", 0, 100, 5, lab.Smoothing, v => $"{v:0}%",
                v => { lab.Smoothing = Math.Round(v); PersistProfile(); }));

            centerContent.Children.Add(SectionLabel("ORIENTATION & UTILITIES"));
            centerContent.Children.Add(ToggleRow("Invert X Axis", lab.InvertX, on => { lab.InvertX = on; PersistProfile(); }));
            centerContent.Children.Add(ToggleRow("Invert Y Axis", lab.InvertY, on => { lab.InvertY = on; PersistProfile(); }));
            centerContent.Children.Add(ToggleRow("Swap Left & Right Sticks", profile.SwapSticks, on => { profile.SwapSticks = on; PersistProfile("Sticks swapped: " + (on ? "On" : "Off")); }));
            centerContent.Children.Add(ToggleRow("Auto-Center Resting Drift (<= 6%)", tune.AutoCenterDrift, on => { tune.AutoCenterDrift = on; PersistProfile(); }));
            centerContent.Children.Add(ToggleRow("Bypass X20CTL Stick Processing (Raw 1:1)", tune.Raw, on => { tune.Raw = on; PersistProfile(); RedrawSpline(); }));

            var copyOther = ActionBtn($"Copy {key} Settings to {otherKey}", () =>
            {
                var clone = CloneStick(tune);
                profile.Sticks[otherKey] = clone;
                if (profile.Curves.TryGetValue(key, out var sc)) profile.Curves[otherKey] = sc with { };
                PersistProfile($"Copied settings cleanly to {otherKey}");
            }, primary: false);
            copyOther.Margin = new(0, 8, 0, 4);
            centerContent.Children.Add(copyOther);
        }
        else if (channel is LabChannel.LeftTrigger or LabChannel.RightTrigger)
        {
            string tKey = channel == LabChannel.LeftTrigger ? "LT" : "RT";
            var tt = profile.Triggers.TryGetValue(tKey, out var existing) ? existing : (profile.Triggers[tKey] = new TriggerTuning());

            centerContent.Children.Add(SectionLabel($"{tKey} TRIGGER RANGE & RESPONSE"));
            centerContent.Children.Add(SegmentRow("Force Preset",
                [("Linear", TriggerPreset.Linear), ("Hair", TriggerPreset.HairTrigger), ("Racing", TriggerPreset.RacingThrottle), ("Sniper", TriggerPreset.Sniper), ("Digital", TriggerPreset.DigitalClick)],
                tt.Preset, p => { tt.Preset = p; PersistProfile($"{tKey} preset: {p}"); RedrawSpline(); }));

            centerContent.Children.Add(FineSlider("Trigger Inner Deadzone", 0, 40, 1, tt.InnerDeadzonePct, v => $"{v:0}%",
                v => { tt.InnerDeadzonePct = (int)Math.Round(v); if (tt.OuterThresholdPct <= tt.InnerDeadzonePct + 5) tt.OuterThresholdPct = Math.Min(100, tt.InnerDeadzonePct + 10); PersistProfile(); }));
            centerContent.Children.Add(FineSlider("Trigger Full Output At", 50, 100, 1, tt.OuterThresholdPct, v => $"{v:0}%",
                v => { tt.OuterThresholdPct = Math.Max(tt.InnerDeadzonePct + 10, (int)Math.Round(v)); PersistProfile(); }));

            centerContent.Children.Add(SectionLabel("RAPID-TRIGGER (WOOTING-STYLE DYNAMIC RESET)"));
            centerContent.Children.Add(ToggleRow("Enable Rapid-Trigger", tt.RapidTrigger, on => { tt.RapidTrigger = on; PersistProfile($"{tKey} Rapid-Trigger: {(on ? "On" : "Off")}"); }));
            centerContent.Children.Add(FineSlider("Actuation Travel Sensitivity", 1, 50, 1, tt.ActuationPct, v => $"{v:0}%",
                v => { tt.ActuationPct = (int)Math.Round(v); PersistProfile(); }));
            centerContent.Children.Add(FineSlider("Release Reset Hysteresis", 1, 35, 1, tt.ResetDeltaPct, v => $"{v:0}%",
                v => { tt.ResetDeltaPct = (int)Math.Round(v); PersistProfile(); }));

            centerContent.Children.Add(SectionLabel("DUAL-STAGE TRIGGER (SOFT PULL VS FULL CLICK)"));
            centerContent.Children.Add(ToggleRow("Enable Dual-Stage Output", tt.DualStage, on => { tt.DualStage = on; PersistProfile(); }));
            centerContent.Children.Add(FineSlider("Full Click Threshold", 55, 98, 1, tt.FullPullPct, v => $"{v:0}%",
                v => { tt.FullPullPct = (int)Math.Round(v); PersistProfile(); }));
            centerContent.Children.Add(SegmentRow("Full Click Output Action",
                [("Off", ""), ("A", "A"), ("B", "B"), ("X", "X"), ("L3", "L3"), ("R3", "R3"), ("Space", "K:32")],
                tt.FullPullOutput ?? "", outName => { tt.FullPullOutput = string.IsNullOrEmpty(outName) ? null : outName; PersistProfile(); }));
        }
        else // Competitive
        {
            centerContent.Children.Add(SectionLabel("SOCD CLEANER (FIGHTING & STRAFE PRECISION)"));
            centerContent.Children.Add(SegmentRow("D-Pad SOCD Mode",
                [("Off", SocdMode.Off), ("Neutral", SocdMode.Neutral), ("Last Input", SocdMode.LastInput), ("Up Priority", SocdMode.UpPriority)],
                profile.DPadSocd, m => { profile.DPadSocd = m; PersistProfile("D-Pad SOCD: " + m); }));
            centerContent.Children.Add(SegmentRow("Left Stick SOCD Mode",
                [("Off", SocdMode.Off), ("Neutral", SocdMode.Neutral), ("Last Input", SocdMode.LastInput)],
                profile.LeftStickSocd, m => { profile.LeftStickSocd = m; PersistProfile("Stick SOCD: " + m); }));

            centerContent.Children.Add(SectionLabel("PRECISION AIM (SNIPER & ADS AUTO-LINK)"));
            var sn = profile.Sniper ??= new SniperModifier();
            centerContent.Children.Add(ToggleRow("Enable Right Stick Precision Sniper", sn.Enabled, on => { sn.Enabled = on; PersistProfile(); }));
            centerContent.Children.Add(ToggleRow("Auto-Engage When Holding LT (ADS Link)", sn.AdsLeftTriggerLink, on => { sn.AdsLeftTriggerLink = on; PersistProfile(); }));
            centerContent.Children.Add(SegmentRow("Manual Modifier Button",
                [("LT", "LT"), ("RT", "RT"), ("LB", "LB"), ("RB", "RB"), ("L3", "L3"), ("R3", "R3")],
                sn.ModifierButton, btn => { sn.ModifierButton = btn; PersistProfile(); }));
            centerContent.Children.Add(FineSlider("Precision Sensitivity Scale", 20, 95, 5, sn.ScalePct, v => $"{v:0}%",
                v => { sn.ScalePct = (int)Math.Round(v); PersistProfile(); }));

            centerContent.Children.Add(SectionLabel("OUTER-RING AUTO-SPRINT (SAVES L3 STICK CLICK)"));
            var sp = profile.AutoSprint ??= new AutoSprintSettings();
            centerContent.Children.Add(ToggleRow("Auto-Trigger L3 at Full Left Stick Push", sp.Enabled, on => { sp.Enabled = on; PersistProfile("Auto-Sprint: " + (on ? "On" : "Off")); }));
            centerContent.Children.Add(FineSlider("Outer Ring Threshold", 75, 98, 1, sp.OuterThresholdPct, v => $"{v:0}%",
                v => { sp.OuterThresholdPct = (int)Math.Round(v); PersistProfile(); }));

            centerContent.Children.Add(SectionLabel("VIRTUAL OUTPUT REPORT RATE CAP"));
            centerContent.Children.Add(SegmentRow("Hz Cap",
                [("Uncapped", 0), ("250 Hz", 250), ("500 Hz", 500), ("750 Hz", 750), ("1000 Hz", 1000)],
                profile.OutputRateHz, hz => { profile.OutputRateHz = hz; PersistProfile($"Output rate: {(hz == 0 ? "Uncapped" : hz + " Hz")}"); }));
        }

        loading = false;
    }

    void RedrawSpline()
    {
        // Remove previous node thumbs
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

        pointsSummaryText.Text = $"{pts.Count}/10 points: " + string.Join("  ", pts.Select(p => $"({p.X:0},{p.Y:0})"));
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

    void OnSplineCanvasMove(object sender, MouseEventArgs e)
    {
        _ = draggedPointIndex;
    }

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
        PersistProfile($"Applied Smart Curve: {kind}");
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
            statusText.Text = (note != null ? note + " · " : "") + "Previewing in local sandbox (assign in PC Mode to apply to games)";
            return;
        }
        try
        {
            profile.Validate();
            if (!profile.IsNeutral && !PcModePage.MaySave(link.Preferences))
            {
                statusText.Text = "Saved draft in Stick Lab · Enable PC Mode notice to apply live";
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
        Padding = new(16, 14, 16, 14),
        Child = new DockPanel
        {
            Children =
            {
                Kit.Text(title, 11, "DS.TextMuted", FontWeights.SemiBold).Also(t => { DockPanel.SetDock(t, Dock.Top); t.Margin = new(0, 0, 0, 10); }),
                child,
            }
        }
    };

    static TextBlock SectionLabel(string text) => Kit.Text(text, 11, "DS.AccentHi", FontWeights.SemiBold).Also(t => t.Margin = new(0, 10, 0, 6));

    static Button ActionBtn(string label, Action onClick, bool primary)
    {
        var b = new Button
        {
            Content = label,
            Style = DS.Style(primary ? "DS.ConsolePrimary" : "ActionButton"),
            Height = 36, MinHeight = 0,
            Padding = new(14, 0, 14, 0),
            FontSize = 12.5,
            Cursor = Cursors.Hand,
        };
        b.Click += (_, _) => onClick();
        return b;
    }

    FrameworkElement FineSlider(string label, double min, double max, double step, double initial, Func<double, string> fmt, Action<double> onChanged)
    {
        var val = Kit.Text(fmt(initial), 12, "DS.Text", FontWeights.SemiBold);
        DockPanel.SetDock(val, Dock.Right);
        var head = new DockPanel { Margin = new(0, 0, 0, 2), Children = { val, Kit.Text(label, 12.5, "DS.TextSoft") } };
        var sl = new Slider
        {
            Minimum = min, Maximum = max, Value = Math.Clamp(initial, min, max),
            SmallChange = step, LargeChange = step * 5, TickFrequency = step, IsSnapToTickEnabled = true,
            Style = DS.Style("DS.Slider"), Height = 22,
        };
        sl.ValueChanged += (_, _) => { val.Text = fmt(sl.Value); if (!loading) onChanged(sl.Value); };
        return new StackPanel { Margin = new(0, 2, 0, 8), Children = { head, sl } };
    }

    FrameworkElement ToggleRow(string label, bool initial, Action<bool> onChanged)
    {
        var cb = new CheckBox { Style = DS.Style("DS.Toggle"), IsChecked = initial, VerticalAlignment = VerticalAlignment.Center };
        cb.Checked += (_, _) => { if (!loading) onChanged(true); };
        cb.Unchecked += (_, _) => { if (!loading) onChanged(false); };
        var dock = new DockPanel { Margin = new(0, 4, 0, 8) };
        DockPanel.SetDock(cb, Dock.Right);
        dock.Children.Add(cb);
        dock.Children.Add(Kit.Text(label, 12.5, "DS.Text").Also(t => t.VerticalAlignment = VerticalAlignment.Center));
        return dock;
    }

    FrameworkElement SegmentRow<T>(string label, (string Title, T Value)[] options, T current, Action<T> onPicked)
    {
        var wrap = new WrapPanel { Margin = new(0, 4, 0, 8) };
        var buttons = new List<(Button Btn, T Val)>();
        foreach (var (title, value) in options)
        {
            var captured = value;
            bool selected = EqualityComparer<T>.Default.Equals(value, current);
            var btn = new Button
            {
                Content = title, Height = 28, MinHeight = 0, Padding = new(10, 0, 10, 0), Margin = new(0, 0, 6, 4),
                FontSize = 11.5, Style = DS.Style("ActionButton"),
                Background = selected ? new SolidColorBrush(Color.FromRgb(56, 128, 232)) : new SolidColorBrush(Color.FromArgb(140, 18, 30, 58)),
                BorderBrush = selected ? Brushes.White : DS.Brush("DS.Hairline"),
            };
            btn.Click += (_, _) =>
            {
                foreach (var item in buttons)
                {
                    bool act = EqualityComparer<T>.Default.Equals(item.Val, captured);
                    item.Btn.Background = act ? new SolidColorBrush(Color.FromRgb(56, 128, 232)) : new SolidColorBrush(Color.FromArgb(140, 18, 30, 58));
                    item.Btn.BorderBrush = act ? Brushes.White : DS.Brush("DS.Hairline");
                }
                if (!loading) onPicked(captured);
            };
            buttons.Add((btn, captured));
            wrap.Children.Add(btn);
        }
        return new StackPanel { Children = { Kit.Text(label, 12, "DS.TextSoft"), wrap } };
    }
}
