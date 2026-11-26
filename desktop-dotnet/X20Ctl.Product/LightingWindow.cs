using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using X20Ctl.Zone.Lighting;

namespace X20Ctl.Product;

/// <summary>
/// Studio-native RGB workspace matching <see cref="VibrationWorkbench"/>'s 1.4* : 1* hardware/inspector layout:
/// left column renders the real X20 Pro hardware photo with dual stick ring + grip LED glow; right column holds
/// Colour, Effects, and Auto tabs feeding a single Apply action.
/// </summary>
internal sealed class LightingWindow : Window
{
    internal static readonly FontFamily MonoFont = new("Consolas, Cascadia Mono, Courier New");

    readonly LightingVisual visual;
    readonly RgbStripsVisual stripsVisual;
    readonly ColourField square = new(false), hueStrip = new(true);
    readonly Border chipPreview = new() { Width = 28, Height = 28, BorderBrush = DS.Brush("DS.Hairline"), BorderThickness = new(1), VerticalAlignment = VerticalAlignment.Center };
    readonly TextBox hex = new() { MaxLength = 7, Width = 104, Padding = new(10, 6, 10, 6), FontFamily = MonoFont, FontSize = 13 };
    readonly TextBlock rgb = new() { FontFamily = MonoFont, FontSize = 12.5, Foreground = DS.Brush("DS.TextSoft"), VerticalAlignment = VerticalAlignment.Center, Margin = new(10, 0, 0, 0) };
    readonly ComboBox modes = new() { ItemsSource = new[] { "Solid", "Rainbow", "Breathe", "Cycle", "Off", "Battery" }, SelectedIndex = 0, MinWidth = 122, MinHeight = 36, Padding = new(10, 6, 10, 6) };
    readonly Slider brightness = new() { Minimum = 0, Maximum = 100, Value = 100, SmallChange = 1, LargeChange = 10, TickFrequency = 1, IsSnapToTickEnabled = true, Style = DS.Style("DS.Slider"), Margin = new(0, 2, 0, 8) };
    readonly TextBlock brightnessValue = Kit.Text("100%", 13, "DS.Text", FontWeights.SemiBold);

    readonly TextBlock liveReadout = Kit.Text("", 13.5, "DS.TextSoft", FontWeights.SemiBold);
    readonly TextBlock status = Kit.Text("Ready · Select a colour, live effect, or audio mode, then click Apply.", 12.5, "DS.TextMuted");
    readonly Button read = new() { Content = "Read controller", Style = DS.Style("DS.ConsoleBtn"), Padding = new(13, 6, 13, 6), FontSize = 12.5, MinHeight = 32, Height = double.NaN };
    readonly Button stopEffect = new() { Content = "Stop effect", Style = DS.Style("DS.ConsoleBtn"), Padding = new(13, 6, 13, 6), FontSize = 12.5, MinHeight = 32, Height = double.NaN, Margin = new(6, 0, 0, 0), Visibility = Visibility.Collapsed };
    readonly Button apply = new() { Content = "Apply", Style = DS.Style("DS.ConsolePrimary"), HorizontalAlignment = HorizontalAlignment.Stretch, Height = 44 };
    public event Action? ShareCommunityRequested;
    public int SelectedRgb => selected;
    public int SelectedModeIndex => modes.SelectedIndex;
    public byte SelectedLevelByte => level;

    readonly List<(Button Button, int Rgb)> swatches = [];
    readonly List<(byte ByteLevel, Button Button)> brightnessStops = [];
    readonly StackPanel batteryPanel = new() { Visibility = Visibility.Collapsed, Margin = new(0, 6, 0, 0) };
    readonly Button[] levelButtons = new Button[4];
    readonly int[] levelColours = (int[])BatteryLightingService.Load().Colours.Clone();

    readonly ScrollViewer rightScroll = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new(0, 0, 8, 0) };
    readonly StackPanel unifiedStack = new();
    Button? advancedToggleBtn;
    bool advancedOpen;
    readonly Border advRule = new() { Visibility = Visibility.Collapsed };
    readonly GradientStop ambientCenterStop = new(Color.FromArgb(75, 255, 0, 0), 0.0);
    readonly FrameworkElement colourView;
    readonly LightingEffectsSection effectsSection;
    readonly Action<Button>? registerFocus;

    // Native X20 Pro modes mapped to ComboBox indices 0..4; index 5 is host Battery gauge.
    static readonly byte[] ModeValues = [LightingRecord.ModeSolid, LightingRecord.ModeRainbow, LightingRecord.ModeBreathe, LightingRecord.ModeCycle, LightingRecord.ModeOff];
    const int BatteryMode = 5;

    static readonly (string Name, int Rgb)[] CuratedSwatches =
    [
        ("Pure White", 0xFFFFFF),
        ("Crimson", 0xFF0020),
        ("Ember", 0xFF5500),
        ("Amber", 0xFFB800),
        ("Neon Lime", 0x80FF00),
        ("Cyber Cyan", 0x00F0FF),
        ("Studio Blue", 0x3D8BFF),
        ("Electric Indigo", 0x6D4AFF),
        ("Magenta", 0xFF00AA),
        ("Ice", 0xA8D4FF),
    ];

    int selected = 0xff0000, editingLevel = 3, activeSection, playerColorIndex;
    double hue, saturation = 1, value = 1;
    bool busy, loadingLevel, syncingBrightness;
    byte level = 0x50;

    bool BatterySelected => modes.SelectedIndex == BatteryMode;

    public LightingWindow() : this(null, animate: true) { }

    internal LightingWindow(Action<Button>? registerFocus = null, bool animate = true)
    {
        this.registerFocus = registerFocus;
        visual = new(animate);
        stripsVisual = new(animate);

        Title = "X20 Pro · RGB";
        Width = 1140;
        Height = 760;
        MinWidth = 860;
        MinHeight = 640;
        Background = DS.Brush("DS.Deep");
        Foreground = DS.Brush("DS.Text");
        FontFamily = DS.Body;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var host = new Grid();
        var root = new Grid();
        root.ColumnDefinitions.Add(new() { Width = new(1.55, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new() { Width = new(0.82, GridUnitType.Star) });
        Content = host;

        // LEFT (1.55*): Single scrollable RGB workspace (Color & Mode -> Live Effects, plus Advanced toggle)
        colourView = BuildColourView();
        effectsSection = new LightingEffectsSection(
            () => selected,
            rgbVal => { activeSection = 0; SetColour(rgbVal); },
            () => Math.Max((byte)0x08, level),
            registerFocus,
            () =>
            {
                if (effectsSection is null) return;
                activeSection = 1;
                UpdateStatusAndVisual();
            });

        unifiedStack.Children.Add(colourView);
        unifiedStack.Children.Add(Kit.Rule().Also(r => r.Margin = new(0, 14, 0, 14)));
        unifiedStack.Children.Add(effectsSection.EffectsView);

        advRule.Height = 1;
        advRule.Background = DS.Brush("DS.Hairline");
        advRule.Margin = new(0, 14, 0, 14);
        unifiedStack.Children.Add(advRule);
        effectsSection.AutoView.Visibility = Visibility.Collapsed;
        unifiedStack.Children.Add(effectsSection.AutoView);
        rightScroll.Content = unifiedStack;

        var leftHeader = new DockPanel { Margin = new(0, 0, 0, 10) };
        advancedToggleBtn = new Button
        {
            Content = "Advanced",
            Style = DS.Style("DS.ConsoleBtn"),
            MinHeight = 30,
            Height = 30,
            Padding = new(12, 4, 12, 4),
            FontSize = 11.5,
            ToolTip = "Show or hide saved presets and automatic game switching",
        };
        advancedToggleBtn.Click += (_, _) => SetAdvanced(!advancedOpen);
        registerFocus?.Invoke(advancedToggleBtn);
        DockPanel.SetDock(advancedToggleBtn, Dock.Right);
        leftHeader.Children.Add(advancedToggleBtn);
        leftHeader.Children.Add(Kit.Section("RGB & LIVE EFFECTS", 0).Also(s => s.VerticalAlignment = VerticalAlignment.Center));

        var leftEditorDock = new DockPanel();
        DockPanel.SetDock(leftHeader, Dock.Top);
        leftEditorDock.Children.Add(leftHeader);
        leftEditorDock.Children.Add(rightScroll);

        var leftEditorPanel = Kit.Panel(null, null, null, leftEditorDock, new Thickness(18));
        leftEditorPanel.Margin = new(0, 0, 14, 0);
        root.Children.Add(leftEditorPanel);

        // RIGHT (0.82*): Top Controller preview + Bottom synchronized LED Strips + Apply footer
        var rightGrid = new Grid();
        rightGrid.RowDefinitions.Add(new() { Height = new(1.05, GridUnitType.Star) });
        rightGrid.RowDefinitions.Add(new() { Height = new(0.85, GridUnitType.Star) });
        rightGrid.RowDefinitions.Add(new() { Height = GridLength.Auto });

        var ambientBrush = new RadialGradientBrush
        {
            GradientOrigin = new(0.5, 0.5),
            Center = new(0.5, 0.5),
            RadiusX = 0.55,
            RadiusY = 0.52,
        };
        ambientBrush.GradientStops.Add(ambientCenterStop);
        ambientBrush.GradientStops.Add(new GradientStop(Colors.Transparent, 1.0));

        var controllerStage = new Border
        {
            CornerRadius = ThemeCatalog.Round(6),
            BorderBrush = DS.Brush("DS.Hairline"),
            BorderThickness = new(1),
            Padding = new(10),
            Margin = new(0, 0, 0, 10),
            MinHeight = 190,
            Background = new SolidColorBrush(ThemeCatalog.Alpha(ThemeCatalog.Current.Void, 165)),
            Child = new Grid
            {
                Children =
                {
                    new System.Windows.Shapes.Ellipse { Fill = ambientBrush, IsHitTestVisible = false },
                    new Viewbox
                    {
                        Child = visual,
                        Stretch = Stretch.Uniform,
                        MaxWidth = 290,
                        MaxHeight = 185,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                    },
                },
            },
        };
        rightGrid.Children.Add(controllerStage);

        var stripsCard = new Border
        {
            CornerRadius = ThemeCatalog.Round(6),
            BorderBrush = DS.Brush("DS.Hairline"),
            BorderThickness = new(1),
            Padding = new(8),
            Margin = new(0, 0, 0, 10),
            MinHeight = 152,
            Background = new SolidColorBrush(ThemeCatalog.Alpha(ThemeCatalog.Current.Void, 165)),
            Child = stripsVisual,
        };
        Grid.SetRow(stripsCard, 1);
        rightGrid.Children.Add(stripsCard);

        var telemetryCard = new Border
        {
            CornerRadius = ThemeCatalog.Round(6),
            BorderBrush = DS.Brush("DS.Hairline"),
            BorderThickness = new(1),
            Padding = new(12, 10, 12, 10),
            Background = new SolidColorBrush(ThemeCatalog.Alpha(ThemeCatalog.Current.Night, 195)),
        };
        var telemetryStack = new StackPanel();
        telemetryStack.Children.Add(liveReadout);
        status.TextWrapping = TextWrapping.Wrap;
        status.Margin = new(0, 3, 0, 8);
        telemetryStack.Children.Add(status);

        var utilityRow = new UniformGrid { Columns = 2, Margin = new(-3, 0, -3, 8) };
        read.HorizontalAlignment = HorizontalAlignment.Stretch;
        read.Margin = new(3, 0, 3, 0);
        stopEffect.HorizontalAlignment = HorizontalAlignment.Stretch;
        stopEffect.Margin = new(3, 0, 3, 0);
        utilityRow.Children.Add(read);
        utilityRow.Children.Add(stopEffect);
        telemetryStack.Children.Add(utilityRow);
        var shareRgbBtn = new Button { Content = "Share RGB to Community", Style = DS.Style("DS.ConsoleBtn"), Padding = new(10, 4, 10, 4), FontSize = 11.5, MinHeight = 28, Height = double.NaN, Margin = new(0, 0, 0, 8), HorizontalAlignment = HorizontalAlignment.Stretch };
        shareRgbBtn.Click += (_, _) => ShareCommunityRequested?.Invoke();
        telemetryStack.Children.Add(shareRgbBtn);
        telemetryStack.Children.Add(apply);
        telemetryCard.Child = telemetryStack;

        Grid.SetRow(telemetryCard, 2);
        rightGrid.Children.Add(telemetryCard);

        var rightVisualPanel = Kit.Panel(null, null, null, rightGrid, new Thickness(16));
        Grid.SetColumn(rightVisualPanel, 1);
        root.Children.Add(rightVisualPanel);

        // Wire actions & events
        read.Click += async (_, _) => await Run(write: false);
        stopEffect.Click += async (_, _) => { await LightingEffectsService.StopAsync(restoreBaseline: true); UpdateStatusAndVisual(); };
        apply.Click += async (_, _) => await ApplyCurrentTabAsync();
        modes.SelectionChanged += (_, _) =>
        {
            activeSection = 0;
            apply.IsEnabled = !busy && modes.SelectedIndex >= 0;
            batteryPanel.Visibility = BatterySelected ? Visibility.Visible : Visibility.Collapsed;
            if (BatterySelected) EditLevel(editingLevel);
            UpdateStatusAndVisual();
        };
        registerFocus?.Invoke(read);
        registerFocus?.Invoke(stopEffect);
        registerFocus?.Invoke(apply);

        var (ecoOverlay, syncEco) = Kit.EcoOverlay("RGB lighting", root);
        host.Children.Add(root);
        host.Children.Add(ecoOverlay);

        void OnServiceChanged()
        {
            rightScroll.Dispatcher.BeginInvoke(UpdateStatusAndVisual, DispatcherPriority.Background);
        }
        void OnEcoChanged()
        {
            rightScroll.Dispatcher.BeginInvoke(() => { syncEco(); UpdateStatusAndVisual(); }, DispatcherPriority.Background);
        }
        host.Loaded += (_, _) =>
        {
            LightingEffectsService.Changed -= OnServiceChanged;
            LightingEffectsService.Changed += OnServiceChanged;
            UniversalHapticsService.Changed -= OnEcoChanged;
            UniversalHapticsService.Changed += OnEcoChanged;
            syncEco();
            UpdateStatusAndVisual();
        };
        host.Unloaded += (_, _) =>
        {
            LightingEffectsService.Changed -= OnServiceChanged;
            UniversalHapticsService.Changed -= OnEcoChanged;
        };

        SetColour(0xff0000);
        ShowLevel(0x50);
        if (BatteryLightingService.Load().Enabled) modes.SelectedIndex = BatteryMode;
        else if (LightingEffectsService.Status.IsRunning) SelectSection(1);
        else SelectSection(0);
        syncEco();
    }

    private FrameworkElement BuildColourView()
    {
        var stack = new StackPanel();
        stack.Children.Add(Kit.Section("COLOR & MODE", 0));

        // 2-Column Color Picker (Left: 2D HSV + Hue | Right: Hex, Swatches, Mode)
        var pickerCols = new Grid { Margin = new(0, 4, 0, 12) };
        pickerCols.ColumnDefinitions.Add(new() { Width = new(1.05, GridUnitType.Star) });
        pickerCols.ColumnDefinitions.Add(new() { Width = new(1.15, GridUnitType.Star) });

        var spectrumGrid = new Grid { Height = 196, Margin = new(0, 0, 12, 0) };
        spectrumGrid.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        spectrumGrid.ColumnDefinitions.Add(new() { Width = new(24) });

        square.ToolTip = "Saturation and value · arrow keys adjust";
        hueStrip.ToolTip = "Hue · up/down keys adjust";
        var squareFrame = new Border
        {
            CornerRadius = ThemeCatalog.Round(6),
            BorderBrush = DS.Brush("DS.Hairline"),
            BorderThickness = new(1),
            ClipToBounds = true,
            Margin = new(0, 0, 8, 0),
            Child = square,
        };
        var hueFrame = new Border
        {
            CornerRadius = ThemeCatalog.Round(6),
            BorderBrush = DS.Brush("DS.Hairline"),
            BorderThickness = new(1),
            ClipToBounds = true,
            Child = hueStrip,
        };
        Grid.SetColumn(hueFrame, 1);
        spectrumGrid.Children.Add(squareFrame);
        spectrumGrid.Children.Add(hueFrame);
        pickerCols.Children.Add(spectrumGrid);

        var colorSideCard = new Border
        {
            CornerRadius = ThemeCatalog.Round(6),
            BorderBrush = DS.Brush("DS.Hairline"),
            BorderThickness = new(1),
            Padding = new(12, 10, 12, 10),
            Background = new SolidColorBrush(ThemeCatalog.Alpha(ThemeCatalog.Current.Night, 185)),
        };
        var sideStack = new StackPanel();

        // Hex + preview swatch + RGB triplet row
        var hexRow = new Grid { Margin = new(0, 0, 0, 10) };
        hexRow.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        hexRow.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        hexRow.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        chipPreview.CornerRadius = new(4);
        chipPreview.Margin = new(0, 0, 8, 0);
        hexRow.Children.Add(chipPreview);
        Grid.SetColumn(hex, 1);
        hexRow.Children.Add(hex);
        Grid.SetColumn(rgb, 2);
        hexRow.Children.Add(rgb);
        sideStack.Children.Add(hexRow);

        sideStack.Children.Add(Kit.Text("Quick Swatches", 11.5, "DS.TextMuted").Also(t => t.Margin = new(0, 0, 0, 5)));
        var swatchRow = new UniformGrid { Columns = 5, Margin = new(-2, 0, -2, 10) };
        foreach (var (name, colorRgb) in CuratedSwatches)
        {
            var btn = new Button
            {
                Height = 28,
                MinHeight = 28,
                Margin = new(2),
                Background = new SolidColorBrush(ToColour(colorRgb)),
                BorderBrush = DS.Brush("DS.Hairline"),
                BorderThickness = new(1),
                ToolTip = $"{name} · #{colorRgb:X6}",
                Template = SwatchTemplate(Math.Min(4, ThemeCatalog.Current.Radius * 0.25)),
            };
            System.Windows.Automation.AutomationProperties.SetName(btn, $"{name}, #{colorRgb:X6}");
            btn.Click += (_, _) => { if (!busy) SetColour(colorRgb); };
            registerFocus?.Invoke(btn);
            swatchRow.Children.Add(btn);
            swatches.Add((btn, colorRgb));
        }
        sideStack.Children.Add(swatchRow);

        var quickColorTools = new UniformGrid { Columns = 2, Margin = new(-2, 0, -2, 10) };
        var themeSyncBtn = new Button
        {
            Content = "Match App Theme",
            Style = DS.Style("DS.ConsoleBtn"),
            Margin = new(2),
            Padding = new(8, 5, 8, 5),
            MinHeight = 30,
            FontSize = 11.5,
            ToolTip = "Set controller LEDs to match the active X20CTL theme accent",
        };
        themeSyncBtn.Click += (_, _) =>
        {
            if (busy) return;
            var accent = ThemeCatalog.Current.Accent;
            SetColour((accent.R << 16) | (accent.G << 8) | accent.B);
        };
        registerFocus?.Invoke(themeSyncBtn);
        quickColorTools.Children.Add(themeSyncBtn);

        int[] playerColors = [0x0088FF, 0xFF2233, 0x10E860, 0xA855F7];
        string[] playerLabels = ["P1 Blue", "P2 Red", "P3 Green", "P4 Purple"];
        Button? playerColorBtn = null;
        playerColorBtn = new Button
        {
            Content = "Player Color (P1)",
            Style = DS.Style("DS.ConsoleBtn"),
            Margin = new(2),
            Padding = new(8, 5, 8, 5),
            MinHeight = 30,
            FontSize = 11.5,
            ToolTip = "Cycle Player 1 to 4 LED color coding",
        };
        playerColorBtn.Click += (_, _) =>
        {
            if (busy) return;
            int idx = playerColorIndex % playerColors.Length;
            playerColorIndex = (idx + 1) % playerColors.Length;
            playerColorBtn.Content = $"Player ({playerLabels[idx]})";
            SetColour(playerColors[idx]);
        };
        registerFocus?.Invoke(playerColorBtn);
        quickColorTools.Children.Add(playerColorBtn);
        sideStack.Children.Add(quickColorTools);

        sideStack.Children.Add(Kit.Text("Hardware Lighting Mode", 11.5, "DS.TextMuted").Also(t => t.Margin = new(0, 0, 0, 5)));
        modes.HorizontalAlignment = HorizontalAlignment.Stretch;
        sideStack.Children.Add(modes);

        colorSideCard.Child = sideStack;
        Grid.SetColumn(colorSideCard, 1);
        pickerCols.Children.Add(colorSideCard);
        stack.Children.Add(pickerCols);

        // Brightness Card + Quick Stops
        var brightCard = new Border
        {
            CornerRadius = ThemeCatalog.Round(6),
            BorderBrush = DS.Brush("DS.Hairline"),
            BorderThickness = new(1),
            Padding = new(12, 10, 12, 10),
            Margin = new(0, 0, 0, 8),
            Background = new SolidColorBrush(ThemeCatalog.Alpha(ThemeCatalog.Current.Night, 185)),
        };
        var brightStack = new StackPanel();
        var brightHead = new DockPanel { Margin = new(0, 0, 0, 4) };
        DockPanel.SetDock(brightnessValue, Dock.Right);
        brightHead.Children.Add(brightnessValue);
        brightHead.Children.Add(Kit.Text("LED Brightness", 12.5, "DS.Text", FontWeights.SemiBold));
        brightStack.Children.Add(brightHead);

        System.Windows.Automation.AutomationProperties.SetName(brightness, "RGB brightness");
        brightness.ValueChanged += (_, _) =>
        {
            if (syncingBrightness || busy) return;
            level = LightingRecord.BrightnessByteFor(brightness.Value);
            UpdateBrightnessReadout();
            UpdateBrightnessStopButtons();
            UpdateStatusAndVisual();
        };
        brightStack.Children.Add(brightness);

        var stopsRow = new UniformGrid { Columns = 5, Margin = new(-3, 2, -3, 0) };
        foreach (var (byteLevel, label) in new (byte, string)[]
        {
            (0x00, "Off"),
            (0x01, "1% Night"),
            (0x40, "25%"),
            (0x80, "50%"),
            (0xff, "100%"),
        })
        {
            byte captured = byteLevel;
            var btn = new Button
            {
                Content = label,
                Style = DS.Style("DS.ConsoleBtn"),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                MinHeight = 30,
                Height = 30,
                Padding = new(6, 4, 6, 4),
                Margin = new(3, 0, 3, 0),
                FontSize = 11.5,
            };
            btn.Click += (_, _) => { if (!busy) { activeSection = 0; ShowLevel(captured); UpdateStatusAndVisual(); } };
            registerFocus?.Invoke(btn);
            stopsRow.Children.Add(btn);
            brightnessStops.Add((captured, btn));
        }
        brightStack.Children.Add(stopsRow);
        brightCard.Child = brightStack;
        stack.Children.Add(brightCard);

        // Conditional Battery 1/4..4/4 threshold mapper
        batteryPanel.Children.Add(Kit.Text("Battery level colours (1/4 to 4/4)", 12.5, "DS.TextSoft").Also(t => t.Margin = new(0, 2, 0, 6)));
        var gauge = new UniformGrid { Columns = 4, Margin = new(-2, 0, -2, 0) };
        for (int i = 0; i < 4; i++)
        {
            int idx = i;
            string name = $"{i + 1}/4";
            var b = new Button
            {
                Height = 34,
                MinHeight = 34,
                Margin = new(2),
                Template = SwatchTemplate(Math.Min(4, ThemeCatalog.Current.Radius * 0.25)),
                ToolTip = $"Battery {name}",
            };
            b.Click += (_, _) => { if (!busy) EditLevel(idx); };
            registerFocus?.Invoke(b);
            levelButtons[i] = b;
            var cell = new StackPanel();
            cell.Children.Add(b);
            cell.Children.Add(Kit.Text(name, 12, "DS.TextSoft").Also(t => { t.HorizontalAlignment = HorizontalAlignment.Center; t.Margin = new(0, 3, 0, 0); }));
            gauge.Children.Add(cell);
        }
        batteryPanel.Children.Add(gauge);
        stack.Children.Add(batteryPanel);

        square.Changed += (x, y) => { activeSection = 0; saturation = x; value = 1 - y; Refresh(); };
        hueStrip.Changed += (_, y) => { activeSection = 0; hue = y * 360; Refresh(); };
        hex.LostFocus += (_, _) => CommitHex();
        hex.KeyDown += (_, e) => { if (e.Key == Key.Enter) { CommitHex(); e.Handled = true; } };

        return stack;
    }

    internal FrameworkElement DetachBody()
    {
        var body = (FrameworkElement)Content;
        Content = null;
        body.HorizontalAlignment = HorizontalAlignment.Stretch;
        return body;
    }

    /// <summary>Reads current stored hardware state when first opened.</summary>
    internal void ReadOnOpen() { if (!ReviewSandbox.Active) _ = Run(false); }

    internal void ShowBattery()
    {
        SelectSection(0);
        modes.SelectedIndex = BatteryMode;
    }

    void SetAdvanced(bool open)
    {
        advancedOpen = open;
        advRule.Visibility = effectsSection.AutoView.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        if (advancedToggleBtn != null)
        {
            SetSelectedState(advancedToggleBtn, open);
            advancedToggleBtn.Content = open ? "Advanced: On" : "Advanced";
        }
    }

    internal void SelectSection(int index)
    {
        activeSection = Math.Clamp(index, 0, 2);
        if (activeSection == 1) effectsSection.PreviewCurrentDraftIfIdle();
        if (activeSection == 2) SetAdvanced(true);

        if (activeSection == 0)
        {
            rightScroll.ScrollToTop();
        }
        else
        {
            FrameworkElement target = activeSection == 1 ? effectsSection.EffectsView : effectsSection.AutoView;
            if (unifiedStack.IsAncestorOf(target))
            {
                unifiedStack.UpdateLayout();
                var pt = target.TransformToAncestor(unifiedStack).Transform(new Point(0, 0));
                rightScroll.ScrollToVerticalOffset(Math.Max(0, pt.Y - 4));
            }
        }

        UpdateStatusAndVisual();
    }

    internal static void SetSelectedState(Button button, bool selected)
    {
        ConsoleUiHelpers.SetIsSelected(button, selected);
        var btnStyle = button.Style;
        if (IllustratedThemeUI.Current == null
            && btnStyle != null
            && (ReferenceEquals(btnStyle, DS.Style("DS.ConsoleBtn"))
                || ReferenceEquals(btnStyle, DS.Style("DS.ConsoleCard"))
                || ReferenceEquals(btnStyle, DS.Style("DS.ConsoleStepBtn"))
                || ReferenceEquals(btnStyle, DS.Style("DS.ConsolePrimary"))
                || ReferenceEquals(btnStyle, DS.Style("DS.ConsoleTab"))))
        {
            button.ClearValue(Control.BackgroundProperty);
            button.ClearValue(Control.ForegroundProperty);
            return;
        }
        if (selected)
        {
            button.Background = new SolidColorBrush(ThemeCatalog.Current.Accent);
            button.Foreground = new SolidColorBrush(ThemeCatalog.Current.OnAccent);
        }
        else
        {
            button.ClearValue(Control.BackgroundProperty);
            button.ClearValue(Control.ForegroundProperty);
        }
    }

    internal static ControlTemplate SwatchTemplate(double radius)
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius));
        foreach (var prop in new[] { Border.BackgroundProperty, Border.BorderBrushProperty, Border.BorderThicknessProperty })
            border.SetBinding(prop, new System.Windows.Data.Binding(prop.Name)
            {
                RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent),
            });
        return new ControlTemplate(typeof(Button)) { VisualTree = border };
    }

    private async Task ApplyCurrentTabAsync()
    {
        if (activeSection == 0)
        {
            if (!CommitHex() || modes.SelectedIndex < 0) return;
            await LightingEffectsService.StopAsync(restoreBaseline: false);
            if (BatterySelected) await ApplyBattery();
            else await Run(write: true, ModeValues[modes.SelectedIndex]);
        }
        else if (activeSection == 1)
        {
            var spec = effectsSection.BuildCurrentEffectSpec();
            status.Text = $"Starting {LightingEffectsSection.ShortEffectName(spec.Kind)}…";
            await LightingEffectsService.ApplyAsync(spec);
            UpdateStatusAndVisual();
        }
        else
        {
            var auto = effectsSection.BuildGameAutoSpec();
            status.Text = "Enabling Game Auto-Switch…";
            await LightingEffectsService.ApplyAsync(auto);
            UpdateStatusAndVisual();
        }
    }

    private void EditLevel(int index)
    {
        editingLevel = index;
        loadingLevel = true;
        SetColour(levelColours[index]);
        loadingLevel = false;
        RefreshLevels();
    }

    private void RefreshLevels()
    {
        for (int i = 0; i < 4; i++)
        {
            var b = levelButtons[i];
            b.Background = new SolidColorBrush(ToColour(levelColours[i]));
            b.BorderBrush = i == editingLevel ? DS.Brush("DS.AccentHi") : DS.Brush("DS.Hairline");
            b.BorderThickness = new(i == editingLevel ? 2.5 : 1);
            System.Windows.Automation.AutomationProperties.SetName(b, $"Battery {i + 1} of 4, #{levelColours[i]:X6}");
        }
    }

    private bool CommitHex()
    {
        var text = hex.Text.Trim().TrimStart('#');
        if (text.Length != 6 || !int.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int colour))
        {
            status.Text = "Enter a six-digit hex colour such as #FF8000.";
            return false;
        }
        SetColour(colour);
        return true;
    }

    private void SetColour(int colour)
    {
        var c = ToColour(colour);
        double r = c.R / 255d, g = c.G / 255d, b = c.B / 255d;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), delta = max - min;
        if (delta > 0) hue = ((max == r ? (g - b) / delta : max == g ? (b - r) / delta + 2 : (r - g) / delta + 4) * 60 + 360) % 360;
        if (delta > 0 || max > 0) saturation = max == 0 ? saturation : delta / max;
        value = max;
        Refresh();
    }

    private void Refresh()
    {
        selected = ColourPalette.FromHsv(hue, saturation, value);
        var c = ToColour(selected);
        chipPreview.Background = new SolidColorBrush(c);
        hex.Text = $"#{selected:X6}";
        rgb.Text = $"{c.R} · {c.G} · {c.B}";
        square.Set(hue, saturation, 1 - value);
        hueStrip.Set(hue, 0, hue / 360);
        foreach (var (button, colour) in swatches)
        {
            bool match = colour == selected;
            button.BorderBrush = match ? DS.Brush("DS.AccentHi") : DS.Brush("DS.Hairline");
            button.BorderThickness = new(match ? 2.5 : 1);
        }
        if (BatterySelected && !loadingLevel) { levelColours[editingLevel] = selected; RefreshLevels(); }
        UpdateStatusAndVisual();
    }

    private void UpdateStatusAndVisual()
    {
        var st = LightingEffectsService.Status;
        bool streamOn = st.IsRunning || activeSection == 1;
        stopEffect.Visibility = st.IsRunning ? Visibility.Visible : Visibility.Collapsed;

        if (activeSection == 0 && !st.IsRunning)
        {
            byte nativeMode = BatterySelected ? LightingRecord.ModeSolid : ModeValues[Math.Clamp(modes.SelectedIndex, 0, ModeValues.Length - 1)];
            var c = ToColour(selected);
            visual.SetEffectMode(false);
            visual.SetStaticFrame(c.R, c.G, c.B, level, nativeMode);
            stripsVisual.SetState(c.R, c.G, c.B, level, nativeMode, false, null);
            byte glowAlpha = nativeMode == LightingRecord.ModeOff ? (byte)0 : (byte)Math.Clamp(18 + level * 0.42, 0, 125);
            ambientCenterStop.Color = Color.FromArgb(glowAlpha, c.R, c.G, c.B);
            string modeLabel = BatterySelected ? "Battery gauge" : (modes.SelectedItem as string ?? "Solid");
            liveReadout.Text = $"#{selected:X6} · {Math.Round(LightingRecord.BrightnessPercentFor(level)):0}% · {modeLabel}";
            return;
        }

        var activeKind = activeSection == 1 && effectsSection is not null
            ? effectsSection.SelectedKind
            : LightingEffectsService.CurrentSpec.Kind;
        visual.SetEffectMode(true, activeKind);
        var frame = LightingEffectsService.EvaluatePreview();
        stripsVisual.SetState(frame.R, frame.G, frame.B, frame.Brightness, LightingRecord.ModeSolid, true, activeKind);
        ambientCenterStop.Color = Color.FromArgb((byte)Math.Clamp(22 + frame.Brightness * 0.42, 18, 125), frame.R, frame.G, frame.B);
        string modeName = activeSection == 1 && !st.IsRunning && effectsSection is not null
            ? LightingEffectsSection.ShortEffectName(effectsSection.SelectedKind)
            : LightingEffectsSection.ShortEffectName(LightingEffectsService.CurrentSpec.Kind);
        string stateTag = st.IsRunning
            ? (ReviewSandbox.Active ? "Simulated" : st.IsYielded ? "Yielded" : st.IsBleConnected ? "Live" : "Connecting")
            : "Preview";
        liveReadout.Text = $"{modeName} · {frame.Hex} · {Math.Round(LightingRecord.BrightnessPercentFor(frame.Brightness)):0}% ({stateTag})";
        if (st.IsRunning && !string.IsNullOrEmpty(st.Summary))
            status.Text = st.ActiveGameExe != null ? $"{st.Summary} · {st.ActiveGameExe}" : st.Summary;
    }

    private void Lock(bool locked)
    {
        busy = locked;
        read.IsEnabled = modes.IsEnabled = batteryPanel.IsEnabled = brightness.IsEnabled = !locked;
        apply.IsEnabled = !locked && modes.SelectedIndex >= 0;
        foreach (var (button, _) in swatches) button.IsEnabled = !locked;
        foreach (var (_, button) in brightnessStops) button.IsEnabled = !locked;
    }

    private async Task Run(bool write, byte? mode = null)
    {
        if (busy) return;
        Lock(true);
        var c = ToColour(selected);
        byte[] colour = [c.R, c.G, c.B];
        status.Text = write ? "Applying lighting…" : "Reading controller…";
        try
        {
            if (write) BatteryLightingService.Disable();
            var record = await LightingBleClient.RunAsync(write ? colour : null, mode, write ? level : null);
            ShowLevel(record[5]);
            if (!BatterySelected)
            {
                SetColour((record[0] << 16) | (record[1] << 8) | record[2]);
                int modeIdx = Array.IndexOf(ModeValues, record[6]);
                if (modeIdx >= 0) modes.SelectedIndex = modeIdx;
            }
            if (write) LightingEffectsService.RecordExternalNativeState(record[0], record[1], record[2], record[5], record[6]);
            status.Text = write || mode.HasValue ? "Applied. Controller read-back matches." : "Controller colour read.";
        }
        catch (Exception ex) { status.Text = ex.Message; }
        finally { Lock(false); UpdateStatusAndVisual(); }
    }

    private void ShowLevel(byte stored)
    {
        level = stored;
        syncingBrightness = true;
        try { brightness.Value = Math.Round(LightingRecord.BrightnessPercentFor(stored)); }
        finally { syncingBrightness = false; }
        UpdateBrightnessReadout();
        UpdateBrightnessStopButtons();
    }

    private void UpdateBrightnessReadout()
    {
        brightnessValue.Text = level == LightingRecord.MoonlightBrightnessByte
            ? "1% Moonlight"
            : $"{brightness.Value:0}%";
    }

    private void UpdateBrightnessStopButtons()
    {
        foreach (var (byteLevel, btn) in brightnessStops)
            SetSelectedState(btn, byteLevel == level);
    }

    private async Task ApplyBattery()
    {
        if (busy) return;
        Lock(true);
        status.Text = "Applying battery lighting…";
        try
        {
            int batLevel = await BatteryLightingService.ApplyAsync((int[])levelColours.Clone());
            status.Text = $"Battery {batLevel}/4 active.";
        }
        catch (Exception ex) { status.Text = ex.Message; }
        finally { Lock(false); UpdateStatusAndVisual(); }
    }

    private static Color ToColour(int colour) => Color.FromRgb((byte)(colour >> 16), (byte)(colour >> 8), (byte)colour);

    private sealed class ColourField(bool hueOnly) : FrameworkElement
    {
        double hue, x, y;
        public event Action<double, double>? Changed;
        public void Set(double h, double px, double py) { hue = h; x = px; y = py; InvalidateVisual(); }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            Focusable = true;
            Cursor = Cursors.Cross;
            System.Windows.Automation.AutomationProperties.SetName(this, hueOnly ? "Hue" : "Saturation and brightness");
        }

        protected override void OnRender(DrawingContext dc)
        {
            var bounds = new Rect(0, 0, ActualWidth, ActualHeight);
            if (hueOnly)
            {
                var brush = new LinearGradientBrush { StartPoint = new(0, 0), EndPoint = new(0, 1) };
                for (int i = 0; i <= 6; i++)
                    brush.GradientStops.Add(new(ToColour(ColourPalette.FromHsv(i * 60, 1, 1)), i / 6d));
                brush.Freeze();
                dc.DrawRectangle(brush, null, bounds);
                var pWhite = new Pen(Brushes.White, 2); pWhite.Freeze();
                dc.DrawRectangle(null, pWhite, new Rect(0, Math.Clamp(y * ActualHeight - 3, 0, Math.Max(0, ActualHeight - 6)), ActualWidth, 6));
            }
            else
            {
                var b1 = new LinearGradientBrush(Colors.White, ToColour(ColourPalette.FromHsv(hue, 1, 1)), 0); b1.Freeze();
                var b2 = new LinearGradientBrush(Colors.Transparent, Colors.Black, 90); b2.Freeze();
                var pBlack = new Pen(Brushes.Black, 3); pBlack.Freeze();
                var pWhite = new Pen(Brushes.White, 1.5); pWhite.Freeze();
                dc.DrawRectangle(b1, null, bounds);
                dc.DrawRectangle(b2, null, bounds);
                var pt = new Point(x * ActualWidth, y * ActualHeight);
                dc.DrawEllipse(null, pBlack, pt, 5, 5);
                dc.DrawEllipse(null, pWhite, pt, 5, 5);
            }
            if (IsKeyboardFocused)
            {
                var pf = new Pen(Brushes.White, 1.5); pf.Freeze();
                dc.DrawRectangle(null, pf, bounds);
            }
        }

        void Pick(Point p)
        {
            x = Math.Clamp(p.X / Math.Max(1, ActualWidth), 0, 1);
            y = Math.Clamp(p.Y / Math.Max(1, ActualHeight), 0, 1);
            Changed?.Invoke(x, y);
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e) { Focus(); CaptureMouse(); Pick(e.GetPosition(this)); e.Handled = true; }
        protected override void OnMouseMove(MouseEventArgs e) { if (IsMouseCaptured) Pick(e.GetPosition(this)); }
        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) { if (IsMouseCaptured) { Pick(e.GetPosition(this)); ReleaseMouseCapture(); } e.Handled = true; }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            double step = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? .05 : .005;
            if (e.Key == Key.Left && !hueOnly) x -= step;
            else if (e.Key == Key.Right && !hueOnly) x += step;
            else if (e.Key == Key.Up) y -= step;
            else if (e.Key == Key.Down) y += step;
            else { base.OnKeyDown(e); return; }
            x = Math.Clamp(x, 0, 1);
            y = Math.Clamp(y, 0, 1);
            Changed?.Invoke(x, y);
            e.Handled = true;
        }
    }
}
