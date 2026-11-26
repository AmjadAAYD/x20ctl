using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Media.Animation;
using X20Ctl.Zone;
using X20Ctl.Zone.PcMode;
namespace X20Ctl.Product;

/// <summary>
/// The Curves page's graph and editor. The plotted line matches what games get (<see cref="PadProcessor.Response"/>):
/// inner dead zone, full-at threshold, anti-dead zone and max output from PC mode's stick settings, then the curve
/// through its two points. Every zone and control point is draggable on the graph and adjustable via sliders.
/// </summary>
public sealed class CurveWorkbench
{
    internal FrameworkElement TakeThemeGraph()
    {
        if (chart.Parent is Viewbox view) view.Child = null;
        else if (chart.Parent is Panel panel) panel.Children.Remove(chart);
        return chart;
    }
    public Grid Inspector { get; } = new();
    public Grid Shelf { get; } = new();
    public WrapPanel Toolbar { get; } = new() { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top };
    public CurveDraft Draft { get; }
    public CurveChannel Channel { get; private set; }
    public IEnumerable<Button> Buttons => channelButtons.Concat(presetButtons);
    public IEnumerable<Thumb> PointHandles => new[] { first, second };
    public IEnumerable<Point> PreviewPoints => response.Points;
    public Rect PlotBounds => chart.TransformToAncestor(Inspector).TransformBounds(new Rect(L, T, PW, PH));
    public int? SelectedPoint { get; private set; }
    public string PresetDescription => summary.Text;
    public TextBlock OutputAxisLabel { get; } = new() { Text = "OUTPUT %", FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = Brushes.LightSteelBlue };
    const string Hint = "";
    public TextBlock PreviewNotice { get; } = new() { Text = Hint, FontSize = 11.5, Foreground = Brushes.LightSteelBlue, TextWrapping = TextWrapping.Wrap };
    public TextBlock ActivePointReadout { get; } = new() { FontSize = 12.5, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
    /// <summary>PC mode's stick settings for a stick (null: none saved, or PC mode isn't reachable). Set by the Studio.</summary>
    public Func<CurveChannel, StickTuning?>? Tuning { get; set; }
    /// <summary>Saves a stick's settings to PC mode; returns why not, or null. Null when they can't be saved from here.</summary>
    public Func<CurveChannel, StickTuning, string?>? SaveTuning { get; set; }
    public Func<IReadOnlyList<CurveChannel>, string?>? ResetResponse { get; set; }
    public bool CanReset => Draft.CanEdit && IsModified(Channel);
    public bool CanResetAll => Draft.CanEdit && Enum.GetValues<CurveChannel>().Any(IsModified);
    bool IsModified(CurveChannel channel) => Draft[channel] != CurveSetting.Default || Tuning?.Invoke(channel) is { IsDefault: false };
    private readonly TextBlock editHint = new() { Text = "Arrows: 1% step · Shift: 5% step", FontSize = 11.5, Foreground = Brushes.LightSteelBlue, VerticalAlignment = VerticalAlignment.Center };
    public event Action<CurveChannel>? ChannelSelected;
    public event Action? Changed;
    public event Action? ShareCurveRequested;
    private readonly List<Button> channelButtons = new(), presetButtons = new();
    // the plot: 408 x 240 inside a 480 x 300 canvas
    private const double L = 38, T = 16, PW = 408, PH = 240;
    private readonly Canvas chart = new() { Width = 480, Height = 300 };
    private readonly Polyline responseGlow = new() { Stroke = new SolidColorBrush(ThemeCatalog.R(Color.FromArgb(52, 70, 150, 255))), StrokeThickness = 8.0, StrokeLineJoin = PenLineJoin.Round, IsHitTestVisible = false };
    private readonly Polyline response = new() { Stroke = new SolidColorBrush(ThemeCatalog.R(Color.FromRgb(116, 200, 255))), StrokeThickness = 3.5, StrokeLineJoin = PenLineJoin.Round };
    private readonly Polygon area = new() { Fill = new LinearGradientBrush(ThemeCatalog.R(Color.FromArgb(42, 70, 150, 255)), ThemeCatalog.R(Color.FromArgb(0, 70, 150, 255)), 90) };
    private readonly Rectangle innerBand = new() { Fill = new SolidColorBrush(ThemeCatalog.R(Color.FromArgb(46, 232, 54, 79))) }, outerBand = new() { Fill = new SolidColorBrush(ThemeCatalog.R(Color.FromArgb(40, 61, 214, 140))) };
    private readonly Line floorLine = Guide(ThemeCatalog.R(Color.FromArgb(150, 255, 196, 92))), ceilingLine = Guide(ThemeCatalog.R(Color.FromArgb(150, 255, 196, 92))), liveGuide = Guide(ThemeCatalog.R(Color.FromArgb(90, 169, 207, 255)));
    private readonly Thumb first = new(), second = new(), deadHandle = new(), fullHandle = new(), floorHandle = new(), ceilingHandle = new();
    private readonly TextBlock deadTag = Tag(), fullTag = Tag(), floorTag = Tag(), ceilingTag = Tag(), halfTag = Tag(), liveTag = Tag();
    private readonly Ellipse halfDot = new() { Width = 10, Height = 10, Fill = Brushes.White, IsHitTestVisible = false }, liveDot = new() { Width = 16, Height = 16, Fill = new SolidColorBrush(ThemeCatalog.R(Color.FromRgb(61, 214, 140))), Stroke = Brushes.White, StrokeThickness = 2, IsHitTestVisible = false, Visibility = Visibility.Collapsed };
    public bool NativeHardwareMode { get; set; }
    public bool DesktopBoostActive { get; set; }
    private readonly TextBlock title = new() { FontSize = 24, FontWeight = FontWeights.Bold }, summary = new() { FontSize = 12.5, Foreground = Brushes.LightSteelBlue }, coordinates = new() { FontSize = 12, Foreground = Brushes.LightSteelBlue };
    public Slider Inner { get; } = new() { Minimum = 0, Maximum = 40, TickFrequency = 1, IsSnapToTickEnabled = true };
    public Slider Outer { get; } = new() { Minimum = 60, Maximum = 100, Value = 100, TickFrequency = 1, IsSnapToTickEnabled = true };
    public Slider Floor { get; } = new() { Minimum = 0, Maximum = 40, TickFrequency = 1, IsSnapToTickEnabled = true };
    public Slider Ceiling { get; } = new() { Minimum = 60, Maximum = 100, Value = 100, TickFrequency = 1, IsSnapToTickEnabled = true };
    private readonly TextBlock innerText = new() { FontSize = 12.5 }, outerText = new() { FontSize = 12.5 }, floorText = new() { FontSize = 12.5 }, ceilingText = new() { FontSize = 12.5 };
    private readonly StackPanel innerPanel = new(), outerPanel = new(), floorPanel = new(), ceilingPanel = new();
    private readonly TextBox pointInput = new() { Width = 68, MinHeight = 28, Padding = new(6, 2, 6, 2), Margin = new(5, 0, 10, 0), FontSize = 12 }, pointOutput = new() { Width = 68, MinHeight = 28, Padding = new(6, 2, 6, 2), Margin = new(5, 0, 0, 0), FontSize = 12 };
    private readonly StackPanel pointEditor = new() { Orientation = Orientation.Horizontal, Visibility = Visibility.Collapsed };
    private readonly TextBlock liveReadout = new() { FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = Brushes.LightSteelBlue, Text = "Live trace ready" };
    private readonly TextBlock zoneFooterBadge = Kit.Text("Curve P1/P2 + Deadzone bounds synced", 11.5, "DS.TextMuted");
    private bool updating;
    private double liveAt = -1;
    public bool AnimationsEnabled { get; }

    static Line Guide(Color c) => new() { Stroke = new SolidColorBrush(c), StrokeThickness = 1.5, StrokeDashArray = new() { 4, 3 }, IsHitTestVisible = false };
    static TextBlock Tag() => new() { FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, IsHitTestVisible = false };
    static double X(double percent) => L + percent / 100 * PW;
    static double Y(double percent) => T + PH - percent / 100 * PH;

    public CurveWorkbench(CurveDraft draft, FrameworkElement resources, bool animations = true)
    {
        Draft = draft;
        AnimationsEnabled = animations;

        Inspector.RowDefinitions.Add(new() { Height = GridLength.Auto }); // Row 0: Header + Channel/Preset bars
        Inspector.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) }); // Row 1: Chart instrument well
        Inspector.RowDefinitions.Add(new() { Height = GridLength.Auto }); // Row 2: Inline node/live telemetry strip

        var heading = new StackPanel();

        // Single sleek top row: Title + Summary left, Toolbar right
        var headerRow = new DockPanel { Margin = new(0, 0, 0, 8) };
        DockPanel.SetDock(Toolbar, Dock.Right);
        headerRow.Children.Add(Toolbar);
        var titleBlock = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        titleBlock.Children.Add(title);
        titleBlock.Children.Add(summary.Also(t => t.Margin = new(0, 1, 0, 0)));
        headerRow.Children.Add(titleBlock);
        heading.Children.Add(headerRow);

        var shareBtn = new Button
        {
            Content = "Share Curve",
            Style = (Style)resources.FindResource("ActionButton"),
            Height = 32, MinHeight = 0,
            Padding = new(12, 0, 12, 0),
            Margin = new(0, 0, 6, 0),
            FontSize = 12,
            ToolTip = "Submit this curve to the X20CTL Community Browser",
        };
        shareBtn.Click += (_, _) => ShareCurveRequested?.Invoke();
        Toolbar.Children.Add(shareBtn);

        // Full-width Channel Bar
        var channels = new UniformGrid { Columns = 4, Rows = 1 };
        foreach (var channel in Enum.GetValues<CurveChannel>())
        {
            var button = new Button
            {
                Content = ShortName(channel),
                Tag = channel,
                Height = 32,
                MinHeight = 0,
                Style = (Style)resources.FindResource("ActionButton"),
                Margin = new(2),
                Padding = new(8, 0, 8, 0),
                FontSize = 12.5,
                ToolTip = Name(channel)
            };
            button.Click += (_, _) => SelectChannel(channel);
            channelButtons.Add(button);
            channels.Children.Add(button);
        }
        heading.Children.Add(new Border
        {
            CornerRadius = new(10),
            Background = new SolidColorBrush(Color.FromArgb(145, 7, 14, 30)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(65, 110, 165, 240)),
            BorderThickness = new(1),
            Padding = new(3),
            Margin = new(0, 0, 0, 6),
            Child = channels
        });

        // Full-width Preset Bar with mini waveform sparklines
        var presets = new UniformGrid { Columns = CurveDraft.Presets.Count, Rows = 1, Margin = new(-2, 0, -2, 8) };
        foreach (string preset in CurveDraft.Presets.Keys)
        {
            var button = new Button
            {
                Tag = preset,
                Style = (Style)resources.FindResource("ActionButton"),
                Height = 34,
                MinHeight = 0,
                Padding = new(6, 0, 6, 0),
                Margin = new(2, 0, 2, 0),
                IsEnabled = Draft.CanEdit
            };
            var mini = new Canvas { Width = 26, Height = 15, Margin = new(0, 0, 5, 0), VerticalAlignment = VerticalAlignment.Center };
            var points = CurveDraft.Presets[preset];
            mini.Children.Add(new Polyline
            {
                Stroke = Brushes.LightSkyBlue,
                StrokeThickness = 1.6,
                Points = new PointCollection
                {
                    new(0, 15),
                    new(points.First.Item1 / 255d * 26, 15 - points.First.Item2 / 255d * 15),
                    new(points.Second.Item1 / 255d * 26, 15 - points.Second.Item2 / 255d * 15),
                    new(26, 0)
                }
            });
            button.Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Children =
                {
                    mini,
                    new TextBlock
                    {
                        Text = char.ToUpper(preset[0]) + preset[1..],
                        FontSize = 11.5,
                        VerticalAlignment = VerticalAlignment.Center
                    }.Also(t => t.SetBinding(TextBlock.ForegroundProperty, new System.Windows.Data.Binding("Foreground") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.FindAncestor, typeof(Button), 1) }))
                }
            };
            button.ToolTip = "Select " + preset + " response";
            button.Click += (_, _) => SetPreset(preset);
            presetButtons.Add(button);
            presets.Children.Add(button);
        }
        heading.Children.Add(presets);
        Inspector.Children.Add(heading);

        // Chart build
        foreach (var band in new[] { innerBand, outerBand }) { band.Height = PH; Canvas.SetTop(band, T); chart.Children.Add(band); }
        foreach (int tick in new[] { 0, 25, 50, 75, 100 })
        {
            double x = X(tick), y = Y(tick);
            chart.Children.Add(new Line { X1 = x, X2 = x, Y1 = T, Y2 = T + PH, Stroke = new SolidColorBrush(ThemeCatalog.R(Color.FromArgb(42, 132, 160, 202))), StrokeThickness = 1 });
            chart.Children.Add(new Line { X1 = L, X2 = L + PW, Y1 = y, Y2 = y, Stroke = new SolidColorBrush(ThemeCatalog.R(Color.FromArgb(42, 132, 160, 202))), StrokeThickness = 1 });
            var horizontal = new TextBlock { Text = tick.ToString(), FontSize = 12, Foreground = Brushes.LightSteelBlue }; Canvas.SetLeft(horizontal, x - 7); Canvas.SetTop(horizontal, T + PH + 6); chart.Children.Add(horizontal);
            var vertical = new TextBlock { Text = tick.ToString(), FontSize = 12, Foreground = Brushes.LightSteelBlue }; Canvas.SetLeft(vertical, 4); Canvas.SetTop(vertical, y - 8); chart.Children.Add(vertical);
        }
        chart.Children.Add(new Line { X1 = L, Y1 = T + PH, X2 = L + PW, Y2 = T, Stroke = new SolidColorBrush(ThemeCatalog.R(Color.FromArgb(110, 200, 216, 244))), StrokeDashArray = new() { 4, 4 } });
        chart.Children.Add(area); chart.Children.Add(floorLine); chart.Children.Add(ceilingLine); chart.Children.Add(liveGuide); chart.Children.Add(responseGlow); chart.Children.Add(response);
        chart.Children.Add(halfDot); chart.Children.Add(halfTag);
        Canvas.SetLeft(OutputAxisLabel, L); Canvas.SetTop(OutputAxisLabel, 0); chart.Children.Add(OutputAxisLabel);
        var input = new TextBlock { Text = "INPUT %", FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = Brushes.LightSteelBlue }; Canvas.SetLeft(input, 206); Canvas.SetTop(input, 282); chart.Children.Add(input);

        for (int index = 0; index < 2; index++)
        {
            Thumb thumb = index == 0 ? first : second;
            thumb.Width = thumb.Height = 18; thumb.Tag = index; thumb.Focusable = true; thumb.FocusVisualStyle = null; thumb.Cursor = Cursors.SizeAll;
            thumb.Template = Knob(index == 0 ? Brushes.LightSkyBlue : new SolidColorBrush(ThemeCatalog.R(Color.FromRgb(232, 238, 248))), true);
            System.Windows.Automation.AutomationProperties.SetName(thumb, $"Curve point {index + 1}, arrow keys adjust input and output");
            int pointIndex = index;
            thumb.GotKeyboardFocus += (_, _) => SelectPoint(pointIndex);
            thumb.PreviewMouseLeftButtonDown += (_, _) => { thumb.Focus(); SelectPoint(pointIndex); };
            thumb.LostKeyboardFocus += (_, e) => { if (e.NewFocus != first && e.NewFocus != second && e.NewFocus != pointInput && e.NewFocus != pointOutput) { SelectedPoint = null; RefreshPointReadout(); } };
            thumb.DragDelta += (_, e) =>
            {
                var z = Zones();
                var point = pointIndex == 0 ? Draft[Channel].Point1 : Draft[Channel].Point2;
                double sx = Math.Max(1, z.Outer - z.Inner) / 100 * PW, sy = Math.Max(1, z.Ceiling - z.Floor) / 100 * PH;
                SetPoint(pointIndex, point.X + (int)Math.Round(e.HorizontalChange / sx * 255), point.Y - (int)Math.Round(e.VerticalChange / sy * 255));
            };
            chart.Children.Add(thumb);
        }
        foreach (var (thumb, tag, name) in new[] { (deadHandle, deadTag, "Dead zone"), (fullHandle, fullTag, "Full at"), (floorHandle, floorTag, "Anti-dead zone"), (ceilingHandle, ceilingTag, "Max output") })
        {
            bool across = thumb == deadHandle || thumb == fullHandle;
            thumb.Width = across ? 8 : 16; thumb.Height = across ? 16 : 8;
            var said = tag; said.Visibility = Visibility.Collapsed;
            void Say(bool on) => said.Visibility = on || thumb.IsDragging || thumb.IsKeyboardFocused || thumb.IsMouseOver ? Visibility.Visible : Visibility.Collapsed;
            thumb.MouseEnter += (_, _) => Say(true); thumb.MouseLeave += (_, _) => Say(false); thumb.GotKeyboardFocus += (_, _) => Say(true); thumb.LostKeyboardFocus += (_, _) => Say(false);
            thumb.DragStarted += (_, _) => Say(true); thumb.DragCompleted += (_, _) => Say(false); thumb.Focusable = true; thumb.FocusVisualStyle = null; thumb.Cursor = across ? Cursors.SizeWE : Cursors.SizeNS; thumb.ToolTip = name;
            thumb.Template = Knob(new SolidColorBrush(thumb == deadHandle ? ThemeCatalog.R(Color.FromRgb(232, 54, 79)) : thumb == fullHandle ? ThemeCatalog.R(Color.FromRgb(61, 214, 140)) : ThemeCatalog.R(Color.FromRgb(255, 196, 92))), false);
            System.Windows.Automation.AutomationProperties.SetName(thumb, name + ", arrow keys adjust");
            var me = thumb;
            thumb.DragDelta += (_, e) => { var z = Zones(); if (me == deadHandle) SetZone(0, z.Inner + e.HorizontalChange / PW * 100); else if (me == fullHandle) SetZone(1, z.Outer + e.HorizontalChange / PW * 100); else if (me == floorHandle) SetZone(2, z.Floor - e.VerticalChange / PH * 100); else SetZone(3, z.Ceiling - e.VerticalChange / PH * 100); };
            chart.Children.Add(thumb); chart.Children.Add(tag);
        }
        chart.Children.Add(liveDot); chart.Children.Add(liveTag);

        var chartWell = new Border
        {
            CornerRadius = new(12),
            Background = new SolidColorBrush(ThemeCatalog.R(Color.FromRgb(6, 13, 28))),
            BorderBrush = new SolidColorBrush(Color.FromArgb(65, 96, 155, 230)),
            BorderThickness = new(1),
            Padding = new(8, 6, 8, 6),
            Child = new Viewbox { Child = chart, Stretch = Stretch.Uniform }
        };
        Grid.SetRow(chartWell, 1);
        Inspector.Children.Add(chartWell);

        // Row 2 of Inspector: Compact Node & Live Readout Telemetry Strip (zero overflow, full width)
        pointEditor.Children.Add(new TextBlock { Text = "In %", FontSize = 11.5, Foreground = DS.Brush("DS.TextSoft"), VerticalAlignment = VerticalAlignment.Center });
        pointEditor.Children.Add(pointInput);
        pointEditor.Children.Add(new TextBlock { Text = "Out %", FontSize = 11.5, Foreground = DS.Brush("DS.TextSoft"), VerticalAlignment = VerticalAlignment.Center });
        pointEditor.Children.Add(pointOutput);
        foreach (var box in new[] { pointInput, pointOutput })
        {
            box.IsEnabled = Draft.CanEdit;
            box.LostKeyboardFocus += (_, _) => CommitPoint();
            box.PreviewKeyDown += (_, e) => { if (e.Key == Key.Enter) { CommitPoint(); e.Handled = true; } };
        }

        var noteRow = new DockPanel();
        var rightTelemetry = new Border
        {
            CornerRadius = new(8),
            Background = new SolidColorBrush(Color.FromArgb(75, 16, 36, 68)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(80, 56, 189, 248)),
            BorderThickness = new(1),
            Padding = new(10, 4, 10, 4),
            VerticalAlignment = VerticalAlignment.Center,
            Child = liveReadout
        };
        DockPanel.SetDock(rightTelemetry, Dock.Right);
        noteRow.Children.Add(rightTelemetry);
        var leftTelemetry = new StackPanel { Margin = new(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
        leftTelemetry.Children.Add(coordinates);
        leftTelemetry.Children.Add(ActivePointReadout);
        leftTelemetry.Children.Add(pointEditor);
        leftTelemetry.Children.Add(PreviewNotice);
        noteRow.Children.Add(leftTelemetry);

        var telemetryStrip = new Border
        {
            CornerRadius = new(10),
            Background = new SolidColorBrush(Color.FromArgb(145, 7, 14, 30)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(65, 110, 165, 240)),
            BorderThickness = new(1),
            Padding = new(10, 6, 10, 6),
            Margin = new(0, 8, 0, 0),
            Child = noteRow
        };
        Grid.SetRow(telemetryStrip, 2);
        Inspector.Children.Add(telemetryStrip);
        Inspector.SizeChanged += (_, _) => RefreshPointReadout();

        // Shelf: Full-width Precision Zone Sliders (no empty 360px right void!)
        Shelf.RowDefinitions.Add(new() { Height = GridLength.Auto });
        Shelf.RowDefinitions.Add(new() { Height = GridLength.Auto });

        var zones = new Grid { Margin = new(0, 8, 0, 0) };
        zones.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        zones.ColumnDefinitions.Add(new() { Width = new(12) });
        zones.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        zones.RowDefinitions.Add(new() { Height = GridLength.Auto });
        zones.RowDefinitions.Add(new() { Height = GridLength.Auto });

        var zoneSpecs = new (StackPanel Panel, Slider Slider, TextBlock Text, string Label, int Kind)[]
        {
            (innerPanel, Inner, innerText, "Dead zone percent", 0),
            (outerPanel, Outer, outerText, "Full at percent", 1),
            (floorPanel, Floor, floorText, "Anti-dead zone percent", 2),
            (ceilingPanel, Ceiling, ceilingText, "Max output percent", 3),
        };
        foreach (var (panel, slider, text, label, k) in zoneSpecs)
        {
            panel.Children.Clear();
            panel.Margin = new(0, 0, 0, 6);
            text.FontSize = 12;
            text.Margin = new(0, 0, 0, 2);
            panel.Children.Add(text);
            slider.IsEnabled = Draft.CanEdit;
            slider.Style = (Style)resources.FindResource("ConsoleCurveSlider");
            slider.Height = 20;
            slider.Margin = new(0);
            panel.Children.Add(slider);
            System.Windows.Automation.AutomationProperties.SetName(slider, label);
            slider.ToolTip = "Arrows adjust 1 percentage point. Hold Shift for 5-point steps.";
            int captured = k;
            slider.ValueChanged += (_, _) => { if (!updating) SetZone(captured, slider.Value); };
            Grid.SetColumn(panel, k % 2 == 0 ? 0 : 2);
            Grid.SetRow(panel, k / 2);
            zones.Children.Add(panel);
        }
        Shelf.Children.Add(zones);

        var zoneFoot = new DockPanel { Margin = new(0, 2, 0, 0) };
        DockPanel.SetDock(editHint, Dock.Right);
        zoneFoot.Children.Add(editHint);
        zoneFoot.Children.Add(zoneFooterBadge);
        Grid.SetRow(zoneFoot, 1);
        Shelf.Children.Add(zoneFoot);

        if (IllustratedThemeUI.Current != null) { response.Effect = null; liveDot.Effect = null; }
        Refresh();
    }

    static ControlTemplate Knob(Brush fill, bool round)
    {
        var factory = new FrameworkElementFactory(round ? typeof(Ellipse) : typeof(Rectangle));
        factory.SetValue(Shape.FillProperty, fill); factory.SetValue(Shape.StrokeProperty, Brushes.White); factory.SetValue(Shape.StrokeThicknessProperty, 2d);
        if (!round) { factory.SetValue(Rectangle.RadiusXProperty, 4d); factory.SetValue(Rectangle.RadiusYProperty, 4d); }
        return new ControlTemplate(typeof(Thumb)) { VisualTree = factory };
    }

    bool IsStick => Channel is CurveChannel.LeftStick or CurveChannel.RightStick;
    bool TuningOwnsZones => IsStick && SaveTuning != null;
    StickTuning? CurrentTuning => IsStick ? Tuning?.Invoke(Channel) : null;
    SetupCurve Setup(CurveSetting s) => new(s.Preset, s.BasePreset, s.Inner, s.Outer, s.Point1.X, s.Point1.Y, s.Point2.X, s.Point2.Y);
    (double Inner, double Outer, double Floor, double Ceiling) Zones()
    {
        var s = Draft[Channel]; var t = CurrentTuning;
        return t == null ? (s.Inner, s.Outer, 0, 100) : t.Raw ? (0, 100, 0, 100) : (t.Inner, t.Outer, t.AntiDeadzone, t.MaxOutput);
    }
    public double Output(double pushed) => PadProcessor.Response(Setup(Draft[Channel]), CurrentTuning, pushed);

    private void SetZone(int kind, double value)
    {
        if (!Draft.CanEdit) return;
        value = Math.Round(value);
        if (TuningOwnsZones)
        {
            var s = Draft[Channel]; var t = Copy(CurrentTuning) ?? new StickTuning { Inner = s.Inner, Outer = s.Outer };
            switch (kind)
            {
                case 0: t.Inner = Math.Clamp(value, 0, Math.Min(StickTuning.MaxInner, t.Outer - 1)); break;
                case 1: t.Outer = Math.Clamp(value, Math.Max(StickTuning.MinOuter, t.Inner + 1), 100); break;
                case 2: t.AntiDeadzone = Math.Clamp(value, 0, Math.Min(StickTuning.MaxAnti, t.MaxOutput - 1)); break;
                default: t.MaxOutput = Math.Clamp(value, Math.Max(60, t.AntiDeadzone + 1), 100); break;
            }
            t.Raw = false;
            PreviewNotice.Text = SaveTuning!(Channel, t) ?? Hint;
        }
        else if (kind < 2)
        {
            var s = Draft[Channel];
            Draft.Set(Channel, kind == 0 ? s with { Preset = "custom", Inner = (int)Math.Clamp(value, 0, s.Outer - 1) } : s with { Preset = "custom", Outer = (int)Math.Clamp(value, s.Inner + 1, 100) });
        }
        else return;
        Refresh(); Changed?.Invoke();
    }
    static StickTuning? Copy(StickTuning? t) => t == null ? null : new() { Inner = t.Inner, Outer = t.Outer, SquareGate = t.SquareGate, AxisRatio = t.AxisRatio, AntiDeadzone = t.AntiDeadzone, MaxOutput = t.MaxOutput, Raw = t.Raw };

    public void SelectChannel(CurveChannel channel) { Channel = channel; SelectedPoint = null; liveAt = -1; Refresh(); ChannelSelected?.Invoke(channel); }
    private void SelectPoint(int index) { SelectedPoint = index; RefreshPointReadout(); }
    public void SetPreset(string preset) { Draft.SetPreset(Channel, preset); Refresh(); Changed?.Invoke(); }
    public void Reset() => ResetChannels([Channel]);
    public void ResetAll() => ResetChannels(Enum.GetValues<CurveChannel>());
    void ResetChannels(IReadOnlyList<CurveChannel> channels)
    {
        if (!Draft.CanEdit) return;
        if (ResetResponse?.Invoke(channels) is { } error) { PreviewNotice.Text = error; return; }
        foreach (var channel in channels) Draft.Reset(channel);
        PreviewNotice.Text = Hint; Refresh(); Changed?.Invoke();
    }
    public void SetPoint(int index, int x, int y)
    {
        if (!Draft.CanEdit) return; var setting = Draft[Channel];
        x = Math.Clamp(x, index == 0 ? 1 : setting.Point1.X + 1, index == 0 ? setting.Point2.X - 1 : 254); y = Math.Clamp(y, 0, 255);
        Draft.Set(Channel, index == 0 ? setting with { Preset = "custom", Point1 = (x, y) } : setting with { Preset = "custom", Point2 = (x, y) }); Refresh(); Changed?.Invoke();
    }
    public bool HandleKey(KeyEventArgs e)
    {
        int step = (Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? 5 : 1;
        if (Keyboard.FocusedElement is Thumb thumb && (thumb == first || thumb == second) && thumb.Tag is int index && Draft.CanEdit)
        {
            var p = index == 0 ? Draft[Channel].Point1 : Draft[Channel].Point2; step = (int)Math.Round(step * 2.55);
            if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down) { SetPoint(index, p.X + (e.Key == Key.Right ? step : e.Key == Key.Left ? -step : 0), p.Y + (e.Key == Key.Up ? step : e.Key == Key.Down ? -step : 0)); e.Handled = true; return true; }
        }
        if (Keyboard.FocusedElement is Thumb zone && Draft.CanEdit && e.Key is Key.Left or Key.Right or Key.Up or Key.Down)
        {
            int sign = e.Key is Key.Right or Key.Up ? 1 : -1; var z = Zones();
            if (zone == deadHandle) { SetZone(0, z.Inner + sign * step); e.Handled = true; return true; }
            if (zone == fullHandle) { SetZone(1, z.Outer + sign * step); e.Handled = true; return true; }
            if (zone == floorHandle) { SetZone(2, z.Floor + sign * step); e.Handled = true; return true; }
            if (zone == ceilingHandle) { SetZone(3, z.Ceiling + sign * step); e.Handled = true; return true; }
        }
        if (Keyboard.FocusedElement is Slider slider && (slider == Inner || slider == Outer || slider == Floor || slider == Ceiling) && slider.IsEnabled && e.Key is Key.Left or Key.Right) { slider.Value += e.Key == Key.Right ? step : -step; e.Handled = true; return true; }
        return false;
    }

    public void ShowLive(double pushed)
    {
        if (pushed < 0) { liveReadout.Text = "Live trace ready"; liveDot.Visibility = liveTag.Visibility = liveGuide.Visibility = Visibility.Collapsed; liveAt = -1; return; }
        pushed = Math.Clamp(pushed, 0, 1); liveReadout.Text = $"In {pushed:P0} -> Out {Output(pushed):P0}";
        if (pushed < .02) { if (liveAt >= 0) { liveAt = -1; liveDot.Visibility = liveTag.Visibility = liveGuide.Visibility = Visibility.Collapsed; } return; }
        if (Math.Abs(pushed - liveAt) < .004) return; liveAt = pushed;
        double o = Output(pushed) * 100, x = X(pushed * 100), y = Y(o);
        Canvas.SetLeft(liveDot, x - 8); Canvas.SetTop(liveDot, y - 8);
        liveGuide.X1 = liveGuide.X2 = x; liveGuide.Y1 = T; liveGuide.Y2 = T + PH;
        liveTag.Text = $"{pushed * 100:0}% -> {o:0}%"; Canvas.SetLeft(liveTag, Math.Min(x + 12, L + PW - 70)); Canvas.SetTop(liveTag, Math.Max(T, y - 26));
        liveDot.Visibility = liveTag.Visibility = liveGuide.Visibility = Visibility.Visible;
    }

    public void Refresh()
    {
        updating = true; var setting = Draft[Channel]; title.Text = Name(Channel); string basis = char.ToUpper(setting.BasePreset[0]) + setting.BasePreset[1..];
        var tuning = CurrentTuning; var z = Zones(); bool modified = setting != CurveSetting.Default || tuning is { IsDefault: false };
        summary.Text = setting.Preset == "custom" ? $"Custom curve · based on {basis}" : $"{basis} curve · {(modified ? "Modified" : "Unmodified")}";
        if (tuning?.Raw == true) summary.Text += " · Raw 1:1 bypass active";
        Inner.Value = z.Inner; Outer.Value = z.Outer; Floor.Value = z.Floor; Ceiling.Value = z.Ceiling;
        innerText.Text = $"{(IsStick ? "Inner deadzone" : "Start threshold")} · {z.Inner:0.#}%";
        outerText.Text = $"{(IsStick ? "Outer full output" : "Full press point")} · {z.Outer:0.#}%";
        floorText.Text = $"Anti-deadzone · {z.Floor:0.#}%";
        ceilingText.Text = $"Max output cap · {z.Ceiling:0.#}%";
        bool limits = TuningOwnsZones;
        Inner.Maximum = limits ? StickTuning.MaxInner : 99; Outer.Minimum = limits ? StickTuning.MinOuter : 1;
        floorPanel.Visibility = ceilingPanel.Visibility = limits ? Visibility.Visible : Visibility.Collapsed;
        // When anti-deadzone & max-cap are hidden (native / trigger), stretch Inner & Outer to full 100% width
        if (limits)
        {
            Grid.SetRow(innerPanel, 0); Grid.SetColumn(innerPanel, 0); Grid.SetColumnSpan(innerPanel, 1);
            Grid.SetRow(outerPanel, 0); Grid.SetColumn(outerPanel, 2); Grid.SetColumnSpan(outerPanel, 1);
        }
        else
        {
            Grid.SetRow(innerPanel, 0); Grid.SetColumn(innerPanel, 0); Grid.SetColumnSpan(innerPanel, 3);
            Grid.SetRow(outerPanel, 1); Grid.SetColumn(outerPanel, 0); Grid.SetColumnSpan(outerPanel, 3);
        }
        floorHandle.Visibility = ceilingHandle.Visibility = limits ? Visibility.Visible : Visibility.Collapsed;
        if (!limits) floorTag.Visibility = ceilingTag.Visibility = Visibility.Collapsed;
        floorLine.Visibility = limits && z.Floor > 0 ? Visibility.Visible : Visibility.Collapsed; ceilingLine.Visibility = limits && z.Ceiling < 100 ? Visibility.Visible : Visibility.Collapsed;
        foreach (var c in new UIElement[] { deadHandle, fullHandle, floorHandle, ceilingHandle, first, second, Inner, Outer, Floor, Ceiling }) c.IsEnabled = Draft.CanEdit;

        innerBand.Width = z.Inner / 100 * PW; Canvas.SetLeft(innerBand, L); outerBand.Width = (100 - z.Outer) / 100 * PW; Canvas.SetLeft(outerBand, X(z.Outer));
        Place(deadHandle, X(z.Inner), T + PH); Place(fullHandle, X(z.Outer), T + PH);
        Label(deadTag, $"Dead zone {z.Inner:0.#}%", Math.Max(L, X(z.Inner) - 30), T + PH - 30); Label(fullTag, $"Full at {z.Outer:0.#}%", Math.Min(X(z.Outer) - 40, L + PW - 90), T + PH - 30);
        floorLine.X1 = X(z.Inner); floorLine.X2 = X(z.Outer); floorLine.Y1 = floorLine.Y2 = Y(z.Floor);
        ceilingLine.X1 = X(z.Inner); ceilingLine.X2 = X(z.Outer); ceilingLine.Y1 = ceilingLine.Y2 = Y(z.Ceiling);
        Place(floorHandle, L + PW + 14, Y(z.Floor)); Place(ceilingHandle, L + PW + 14, Y(z.Ceiling));
        Label(floorTag, $"Anti-dead zone {z.Floor:0.#}%", L + PW - 130, Y(z.Floor) - 24); Label(ceilingTag, $"Max output {z.Ceiling:0.#}%", L + PW - 120, Y(z.Ceiling) + 6);

        var line = new PointCollection(101);
        for (int i = 0; i <= 100; i++)
            line.Add(new Point(X(i), Y(Output(i / 100d) * 100)));
        line.Freeze();
        responseGlow.Points = line;
        response.Points = line;
        var polyPts = new PointCollection(line.Count + 2);
        foreach (var pt in line) polyPts.Add(pt);
        polyPts.Add(new Point(L + PW, T + PH));
        polyPts.Add(new Point(L, T + PH));
        polyPts.Freeze();
        area.Points = polyPts;
        halfDot.Visibility = halfTag.Visibility = Visibility.Collapsed;
        foreach (var (thumb, p) in new[] { (first, setting.Point1), (second, setting.Point2) })
        {
            thumb.BeginAnimation(Canvas.LeftProperty, null); thumb.BeginAnimation(Canvas.TopProperty, null);
            double px = z.Inner + (z.Outer - z.Inner) * p.X / 255d, py = z.Floor + (z.Ceiling - z.Floor) * p.Y / 255d;
            double x = X(px) - 9, y = Y(py) - 9; Canvas.SetLeft(thumb, x); Canvas.SetTop(thumb, y);
        }
        if (liveAt >= 0) { double keep = liveAt; liveAt = -1; ShowLive(keep); }

        double midOut = Output(0.5) * 100.0;
        coordinates.Text = $"P1 ({setting.Point1.X / 2.55:0}% -> {setting.Point1.Y / 2.55:0}%)  ·  P2 ({setting.Point2.X / 2.55:0}% -> {setting.Point2.Y / 2.55:0}%)  ·  50% Push = {midOut:0}% Out";
        coordinates.ToolTip = $"Stored P1: {setting.Point1.X}/255, {setting.Point1.Y}/255. Stored P2: {setting.Point2.X}/255, {setting.Point2.Y}/255.";
        coordinates.TextWrapping = TextWrapping.Wrap;

        string modeLabel = NativeHardwareMode
            ? (DesktopBoostActive ? "Native HW + Boost active" : "Native Hardware Curve")
            : "PC Mode DSP active";
        zoneFooterBadge.Text = limits
            ? $"{modeLabel}  ·  DZ {z.Inner:0.#}%..{z.Outer:0.#}%  ·  Floor {z.Floor:0.#}% / Cap {z.Ceiling:0.#}%"
            : (IsStick ? $"Native Hardware Curve  ·  DZ {z.Inner:0.#}%..{z.Outer:0.#}%" : $"Trigger range {z.Inner:0.#}% -> {z.Outer:0.#}%");

        RefreshPointReadout();
        foreach (var button in channelButtons) button.Background = (CurveChannel)button.Tag == Channel ? Brushes.White : DS.Brush("Control.ButtonFill");
        foreach (var button in channelButtons) button.Foreground = (CurveChannel)button.Tag == Channel ? new SolidColorBrush(ThemeCatalog.R(Color.FromRgb(17, 24, 39))) : Brushes.LightSteelBlue;
        foreach (var button in presetButtons) button.Foreground = (string)button.Tag == setting.Preset ? new SolidColorBrush(ThemeCatalog.R(Color.FromRgb(17, 24, 39))) : Brushes.LightSteelBlue;
        foreach (var button in presetButtons) button.Background = (string)button.Tag == setting.Preset ? Brushes.White : DS.Brush("Control.ButtonFill");
        updating = false;
    }
    bool Dragging => new[] { first, second, deadHandle, fullHandle, floorHandle, ceilingHandle }.Any(t => t.IsDragging);
    static void Place(Thumb t, double x, double y) { Canvas.SetLeft(t, x - t.Width / 2); Canvas.SetTop(t, y - t.Height / 2); }
    static void Label(TextBlock t, string text, double x, double y) { t.Text = text; Canvas.SetLeft(t, x); Canvas.SetTop(t, y); }
    private void RefreshPointReadout()
    {
        bool active = SelectedPoint.HasValue;
        coordinates.Visibility = active ? Visibility.Collapsed : Visibility.Visible;
        pointEditor.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
        ActivePointReadout.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
        PreviewNotice.Visibility = string.IsNullOrWhiteSpace(PreviewNotice.Text) ? Visibility.Collapsed : Visibility.Visible;
        editHint.Text = active ? $"P{SelectedPoint + 1}: Left/Right Input · Up/Down Output" : "Drag graph nodes or sliders";
        if (active)
        {
            var point = SelectedPoint == 0 ? Draft[Channel].Point1 : Draft[Channel].Point2;
            ActivePointReadout.Text = $"POINT {SelectedPoint + 1} · Input {point.X / 2.55:0.0}% -> Output {point.Y / 2.55:0.0}%";
            if (!pointInput.IsKeyboardFocused) pointInput.Text = (point.X / 2.55).ToString("0.0");
            if (!pointOutput.IsKeyboardFocused) pointOutput.Text = (point.Y / 2.55).ToString("0.0");
            ActivePointReadout.ToolTip = $"Stored point coordinates: input {point.X}/255, output {point.Y}/255.";
        }
    }
    void CommitPoint()
    {
        if (SelectedPoint is not int index || !Draft.CanEdit) return;
        if (double.TryParse(pointInput.Text, out double x) && double.TryParse(pointOutput.Text, out double y) && double.IsFinite(x) && double.IsFinite(y)) SetPoint(index, (int)Math.Round(Math.Clamp(x, 0, 100) * 2.55), (int)Math.Round(Math.Clamp(y, 0, 100) * 2.55));
        else PreviewNotice.Text = "Enter percentages between 0 and 100.";
    }
    public static string Name(CurveChannel channel) => channel switch { CurveChannel.LeftStick => "Left stick", CurveChannel.RightStick => "Right stick", CurveChannel.LeftTrigger => "Left trigger · LT", _ => "Right trigger · RT" };
    private static string ShortName(CurveChannel channel) => channel switch { CurveChannel.LeftStick => "L Stick", CurveChannel.RightStick => "R Stick", CurveChannel.LeftTrigger => "LT", _ => "RT" };
}

internal sealed class CurvePointMorph : AnimationTimeline
{
    private readonly PointCollection origin, destination;
    public CurvePointMorph(PointCollection from, PointCollection to)
    {
        origin = from.Clone(); destination = to.Clone(); origin.Freeze(); destination.Freeze(); Duration = new(TimeSpan.FromMilliseconds(160)); FillBehavior = FillBehavior.Stop;
    }
    public override Type TargetPropertyType => typeof(PointCollection);
    protected override Freezable CreateInstanceCore() => new CurvePointMorph(origin, destination);
    public override object GetCurrentValue(object defaultOriginValue, object defaultDestinationValue, AnimationClock clock)
    {
        double t = clock.CurrentProgress ?? 1; t = 1 - Math.Pow(1 - t, 3);
        return new PointCollection(origin.Select((p, i) => new Point(p.X + (destination[i].X - p.X) * t, p.Y + (destination[i].Y - p.Y) * t)));
    }
}
