using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using X20Ctl.Product.Scanner;
using X20Ctl.Zone;
using X20Ctl.Zone.Lighting;

namespace X20Ctl.Product;

/// <summary>
/// Unified PS5-console Vibration Studio:
/// Left 1.4* column renders the controller silhouette with live 4-motor edge lighting and real-time output bars;
/// Right 1* column flows smoothly in one scrollable view through Motors &amp; Test, Custom Pattern Composer, and Per-Motor Audio Roles.
/// </summary>
public sealed class VibrationWorkbench
{
    public Grid Body { get; } = new();
    public Grid Shelf { get; } = new();
    public VibrationDraft Draft { get; }
    public Slider Strength { get; } = new() { Minimum = 0, Maximum = 100, TickFrequency = 1, IsSnapToTickEnabled = true };
    public Button Test { get; }
    public TextBlock Readback { get; } = new() { FontSize = 13.5, Foreground = DS.Brush("DS.TextSoft"), TextWrapping = TextWrapping.Wrap };
    public VibrationVisual Visualizer { get; }
    public IEnumerable<Button> PresetButtons => presets;
    public event Action? Changed;

    readonly List<Button> presets = new();
    readonly ScrollViewer inspectorScroll = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new(0, 0, 6, 0) };
    readonly StackPanel unifiedStack = new() { Margin = new(0, 2, 0, 12) };
    readonly Slider testAllStrength = new() { Minimum = 0, Maximum = 100, Value = 100, TickFrequency = 5, IsSnapToTickEnabled = true };
    public string PreviewRegion { get; private set; } = "All";
    public void SelectRegion(string region) { PreviewRegion = region; Visualizer.SetRegion(region); }
    readonly TextBlock value = new() { FontSize = 23, FontWeight = FontWeights.SemiBold };
    bool updating, testing;
    int previewStrength;

    /// <summary>The pad has trigger motors (X20 Pro, Dune): offers four zones, else the two grips.</summary>
    public bool HasTriggers { get; private set; }

    public enum Zone { LeftTrigger, RightTrigger, LeftGrip, RightGrip }

    static readonly (Zone Zone, string Name)[] zoneNames =
    [
        (Zone.LeftTrigger, "Left trigger"),
        (Zone.RightTrigger, "Right trigger"),
        (Zone.LeftGrip, "Left grip"),
        (Zone.RightGrip, "Right grip"),
    ];

    readonly Dictionary<Zone, (Button Face, Slider Slider, TextBlock Value, TextBlock RoleTag)> zones = new();
    public int ZoneStrength(Zone zone) => zones.TryGetValue(zone, out var z)
        ? (Draft.PcMode && !IsTrigger(zone) ? Math.Min(100, (int)z.Slider.Value * previewStrength / 100) : (int)z.Slider.Value)
        : 0;
    public Button? ZoneButton(Zone zone) => zones.TryGetValue(zone, out var z) ? z.Face : null;
    public Slider? ZoneSlider(Zone zone) => zones.TryGetValue(zone, out var z) ? z.Slider : null;
    static bool IsTrigger(Zone zone) => zone is Zone.LeftTrigger or Zone.RightTrigger;

    readonly FrameworkElement motorsSection;
    readonly FrameworkElement universalHapticsSection;
    readonly FrameworkElement patternsSection;
    readonly FrameworkElement audioRolesSection;

    // Universal Haptics UI state (pre-allocated brushes, zero GC churn in live loop)
    Button? toggleUniversalBtn;
    readonly TextBlock universalStatusText = Kit.Text("Ready · Feather battery-saver comfort mode", 12, "DS.TextSoft").Also(t => t.TextWrapping = TextWrapping.Wrap);
    readonly Dictionary<HapticsGenreProfile, Button> universalProfileBtns = new();
    readonly Dictionary<HapticsComfortTier, Button> comfortTierBtns = new();
    readonly Dictionary<NativeRumbleBlendMode, Button> blendModeBtns = new();
    Button? autoDetectToggleBtn;
    Button? askUnknownToggleBtn;
    Button? menuMuteToggleBtn;
    Slider? uniMasterSlider, uniTextureSlider, uniImpactSlider, uniTriggerSlider;
    TextBlock? uniMasterVal, uniTextureVal, uniImpactVal, uniTriggerVal;
    readonly Dictionary<HapticsEventTag, Border> eventChipBorders = new();
    bool syncingUniversalUi;

    static readonly SolidColorBrush EventIdleBrush = FrozenBrush(90, 20, 32, 56);
    static readonly SolidColorBrush EventActiveBrush = FrozenBrush(230, 56, 134, 255);

    // Left-column compact 4-motor live telemetry dock (GPU ScaleTransform meters)
    readonly ScaleTransform?[] dockMeterScales = new ScaleTransform?[4];
    readonly TextBlock dockStatusText = Kit.Text("Ready", 12, "DS.TextSoft").Also(t => t.TextWrapping = TextWrapping.Wrap);
    readonly Button? dockTriggerSetupBtn;

    // Section 1: Custom Vibration Pattern Lab (clean rectangular hardware sequencer cells)
    readonly VibrationPatternStore patternStore = new(VibrationPatternStore.DefaultPath);
    readonly VibrationPatternPlayer patternPlayer = new();
    readonly System.Windows.Controls.Primitives.UniformGrid patternLibraryGrid = new() { Columns = 2, Margin = new(-3, 0, -3, 10) };
    readonly Button[,] matrixButtons = new Button[4, VibrationPattern.DefaultSteps];
    readonly Border[,] matrixFillBars = new Border[4, VibrationPattern.DefaultSteps];
    readonly Border[] stepColumnHeaders = new Border[VibrationPattern.DefaultSteps];
    readonly TextBlock[] stepHeaderTexts = new TextBlock[VibrationPattern.DefaultSteps];
    readonly List<(string Id, Button Card)> patternCardButtons = new();
    readonly Slider patternSpeedSlider = new() { Minimum = 50, Maximum = 400, Value = 115, SmallChange = 5, LargeChange = 25, TickFrequency = 5, IsSnapToTickEnabled = true, Style = DS.Style("DS.Slider") };
    readonly Slider patternStrengthSlider = new() { Minimum = 10, Maximum = 100, Value = 85, SmallChange = 5, LargeChange = 15, TickFrequency = 5, IsSnapToTickEnabled = true, Style = DS.Style("DS.Slider") };
    readonly TextBlock patternNameText = Kit.Text("", 13.5, "DS.Text", FontWeights.SemiBold);
    readonly TextBlock patternDescText = Kit.Text("", 12, "DS.TextMuted").Also(t => t.TextWrapping = TextWrapping.Wrap);
    readonly TextBlock patternSpeedLabel = Kit.Text("", 12.5, "DS.TextSoft");
    readonly TextBlock patternStrengthLabel = Kit.Text("", 12.5, "DS.TextSoft");
    Button? patternLoopBtn;
    Button? patternRampBtn;
    Button? patternPlayBtn;
    VibrationPattern composerPattern = VibrationPatternEngine.BuiltIns[0].Normalized();
    bool patternSyncing;
    int activePlayheadStep = -1;

    // Section 2: Per-Motor Audio Roles & 8-Band Spectrum (GPU transforms)
    readonly ScaleTransform?[] roleSpectrumScales = new ScaleTransform?[8];
    ScaleTransform? roleVocalScale;
    Border? roleBeatBadge;
    TextBlock? roleVibeText;
    readonly (TextBlock Label, TextBlock Desc, ScaleTransform Scale)[] roleCardUi = new (TextBlock, TextBlock, ScaleTransform)[4];
    readonly Dictionary<int, Button> audioHapStrengthBtns = new();
    Button? toggleAudioHapBtn;
    readonly DispatcherTimer studioTimer = new() { Interval = TimeSpan.FromMilliseconds(60) };

    static readonly SolidColorBrush BeatActiveBrush = FrozenBrush(220, 0, 229, 255);
    static readonly SolidColorBrush BeatIdleBrush = FrozenBrush(40, 0, 229, 255);
    static readonly SolidColorBrush PlayheadActiveBrush = FrozenBrush(145, 0, 229, 255);
    static readonly SolidColorBrush CellBgBrush = FrozenBrush(185, 13, 21, 38);
    static readonly SolidColorBrush CellHoverBgBrush = FrozenBrush(215, 24, 38, 66);
    static readonly SolidColorBrush CellHairlineBrush = FrozenBrush(90, 56, 76, 112);
    static readonly ControlTemplate SeqCellTemplate = BuildSequencerCellTemplate();

    static readonly Color[] MotorColors =
    [
        Color.FromRgb(0xFF, 0x3B, 0x7F), // Left Grip
        Color.FromRgb(0x38, 0xBD, 0xF8), // Right Grip
        Color.FromRgb(0xFB, 0x92, 0x3C), // Left Trigger
        Color.FromRgb(0xA7, 0x8B, 0xFA), // Right Trigger
    ];

    static SolidColorBrush FrozenBrush(byte a, byte r, byte g, byte b)
    {
        var br = new SolidColorBrush(Color.FromArgb(a, r, g, b));
        br.Freeze();
        return br;
    }

    static ControlTemplate BuildSequencerCellTemplate()
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.Name = "CellBd";
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        border.SetValue(Border.BackgroundProperty, CellBgBrush);
        border.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding(Control.BorderBrushProperty.Name)
        {
            RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent),
        });
        border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        border.SetValue(Border.ClipToBoundsProperty, true);
        var cp = new FrameworkElementFactory(typeof(ContentPresenter));
        cp.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
        cp.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Stretch);
        border.AppendChild(cp);

        var tpl = new ControlTemplate(typeof(Button)) { VisualTree = border };
        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Border.BackgroundProperty, CellHoverBgBrush, "CellBd"));
        tpl.Triggers.Add(hover);
        return tpl;
    }

    public VibrationWorkbench(X20Ctl.Zone.VibrationDraft draft, FrameworkElement resources, Action<Button> register, bool animations, string model = "x20")
    {
        Draft = draft;
        previewStrength = draft.Strength;
        HasTriggers = ModelProfiles.For(model).Motors.Contains("triggers");
        try { patternStore.Load(); } catch { }

        Body.ColumnDefinitions.Add(new() { Width = new(1.4, GridUnitType.Star) });
        Body.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });

        Visualizer = new((Geometry)resources.FindResource("ControllerOutline"), animations, model);

        Button MakeButton(string text, Action action, string styleKey = "DS.ConsoleBtn")
        {
            var b = new Button
            {
                Content = text,
                Style = DS.Style(styleKey),
                MinHeight = 34,
                Height = double.NaN,
                Padding = new(12, 6, 12, 6),
                Margin = new(0, 3, 6, 3),
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            b.Click += (_, _) => action();
            register(b);
            return b;
        }

        Test = MakeButton(HasTriggers ? "Test all four" : "Test both grips", () => _ = RunAll(), "DS.MotorTile");
        Test.IsEnabled = !ReviewSandbox.Active;
        Test.Height = 46;
        Test.HorizontalAlignment = HorizontalAlignment.Stretch;
        Test.Margin = new(0, 4, 0, 8);

        if (HasTriggers)
        {
            dockTriggerSetupBtn = MakeButton("Trigger Setup (M + Back)", OpenTriggerGuide);
            dockTriggerSetupBtn.Height = 30;
            dockTriggerSetupBtn.MinHeight = 28;
            dockTriggerSetupBtn.FontSize = 11.5;
            dockTriggerSetupBtn.Padding = new(10, 4, 10, 4);
            dockTriggerSetupBtn.Margin = new(0);
        }

        // Left hardware stage (artwork + live 4-motor telemetry dock)
        var leftDock = new DockPanel();
        var telemetryCard = BuildLeftTelemetryDock();
        DockPanel.SetDock(telemetryCard, Dock.Bottom);
        leftDock.Children.Add(telemetryCard);
        leftDock.Children.Add(new Viewbox { Child = Visualizer, Stretch = Stretch.Uniform });
        var artPanel = Kit.Panel(null, null, null, leftDock);
        artPanel.Margin = new(0, 0, 16, 0);
        Body.Children.Add(artPanel);

        // Unified single-scroll PS5 console inspector on the right (no sub-tabs)
        motorsSection = BuildMotorsSection(MakeButton);
        universalHapticsSection = BuildUniversalHapticsSection(MakeButton);
        patternsSection = BuildPatternsSection(MakeButton);
        audioRolesSection = BuildAudioRolesSection(MakeButton);

        unifiedStack.Children.Add(motorsSection);
        unifiedStack.Children.Add(Kit.Rule().Also(r => r.Margin = new(0, 14, 0, 14)));
        unifiedStack.Children.Add(universalHapticsSection);
        unifiedStack.Children.Add(Kit.Rule().Also(r => r.Margin = new(0, 14, 0, 14)));
        unifiedStack.Children.Add(patternsSection);
        unifiedStack.Children.Add(Kit.Rule().Also(r => r.Margin = new(0, 14, 0, 14)));
        unifiedStack.Children.Add(audioRolesSection);

        inspectorScroll.Content = unifiedStack;
        var scrollHost = ConsoleUiHelpers.WrapWithScrollHint(inspectorScroll, "Scroll down for Universal Haptics, Patterns & Roles");
        var controlPanel = Kit.Panel(null, null, null, scrollHost);
        Grid.SetColumn(controlPanel, 1);
        Body.Children.Add(controlPanel);

        Shelf.Children.Add(Copy("Test motors, run Universal Game/Music Haptics, or sculpt custom waves in one scrolling Studio", 13.5));

        Strength.ValueChanged += (_, _) => { if (!updating) Set((int)Strength.Value); };

        patternPlayer.FrameUpdated += (hf, step, playing) =>
        {
            Body.Dispatcher.BeginInvoke(() => OnPatternFrame(hf, step, playing));
        };
        studioTimer.Tick += (_, _) => RefreshLiveStudioTelemetry();

        void OnLightingChanged() => Body.Dispatcher.BeginInvoke(SyncRoleAndAudioUi);
        void OnUniversalChanged() => Body.Dispatcher.BeginInvoke(SyncUniversalHapticsUi);
        void OnTriggerArmChanged() => Body.Dispatcher.BeginInvoke(ShowTriggerState);

        Body.Loaded += (_, _) =>
        {
            LightingEffectsService.Changed -= OnLightingChanged;
            LightingEffectsService.Changed += OnLightingChanged;
            UniversalHapticsService.Changed -= OnUniversalChanged;
            UniversalHapticsService.Changed += OnUniversalChanged;
            if (HasTriggers && Diagnostics.BrowserTriggerRumble.Shared is { } shared)
            {
                shared.ArmedChanged -= OnTriggerArmChanged;
                shared.ArmedChanged += OnTriggerArmChanged;
                shared.StateChanged -= OnTriggerArmChanged;
                shared.StateChanged += OnTriggerArmChanged;
                _ = shared.Ready();
            }
            SyncRoleAndAudioUi();
            SyncUniversalHapticsUi();
            ShowTriggerState();
            SyncStudioTimer();
            if (Draft.PcMode && !ReviewSandbox.Active) live.Start();
        };

        Body.Unloaded += (_, _) =>
        {
            studioTimer.Stop();
            live.Stop();
            StopPattern();
            UniversalHapticsService.SetManualMotorOverride(false);
            LightingEffectsService.Changed -= OnLightingChanged;
            UniversalHapticsService.Changed -= OnUniversalChanged;
            if (HasTriggers && Diagnostics.BrowserTriggerRumble.Shared is { } shared)
            {
                shared.ArmedChanged -= OnTriggerArmChanged;
                shared.StateChanged -= OnTriggerArmChanged;
            }
        };

        Refresh();
        SelectRegion("All");
        LoadPatternIntoComposer(VibrationPatternEngine.BuiltIns[0]);
        SyncRoleAndAudioUi();
        SyncUniversalHapticsUi();
        ShowTriggerState();
    }

    public void SelectSection(int index)
    {
        int clamped = Math.Clamp(index, 0, 3);
        FrameworkElement target = clamped switch
        {
            1 => patternsSection,
            2 => audioRolesSection,
            3 => universalHapticsSection,
            _ => motorsSection,
        };
        if (clamped == 0)
        {
            inspectorScroll.ScrollToTop();
        }
        else if (unifiedStack.IsAncestorOf(target))
        {
            unifiedStack.UpdateLayout();
            var pt = target.TransformToAncestor(unifiedStack).Transform(new Point(0, 0));
            inspectorScroll.ScrollToVerticalOffset(Math.Max(0, pt.Y - 4));
        }
    }

    public void StopPattern()
    {
        patternPlayer.Stop();
        activePlayheadStep = -1;
        Visualizer.ClearLiveMotors();
        UpdatePatternUiState(HapticsFrame.Zero, -1, false);
    }

    void OpenTriggerGuide()
    {
        TriggerEnableGuideDialog.Show(Window.GetWindow(Body), force: true);
        if (!ReviewSandbox.Active && Diagnostics.BrowserTriggerRumble.Shared is { } shared)
            _ = shared.Listen();
    }

    FrameworkElement BuildLeftTelemetryDock()
    {
        var box = new Border
        {
            CornerRadius = ThemeCatalog.Round(8),
            BorderBrush = DS.Brush("DS.Hairline"),
            BorderThickness = new(1),
            Padding = new(12, 10, 12, 10),
            Margin = new(0, 10, 0, 0),
            Background = new SolidColorBrush(ThemeCatalog.Alpha(ThemeCatalog.Current.Night, 195)),
        };

        var stack = new StackPanel();
        var topRow = new DockPanel { Margin = new(0, 0, 0, 8) };
        if (dockTriggerSetupBtn != null)
        {
            DockPanel.SetDock(dockTriggerSetupBtn, Dock.Right);
            topRow.Children.Add(dockTriggerSetupBtn);
        }
        topRow.Children.Add(dockStatusText);
        stack.Children.Add(topRow);

        int count = HasTriggers ? 4 : 2;
        var grid = new System.Windows.Controls.Primitives.UniformGrid { Columns = count, Margin = new(-3, 0, -3, 0) };
        (string Tag, Color Col)[] defs =
        [
            ("L GRIP", MotorColors[0]),
            ("R GRIP", MotorColors[1]),
            ("L TRIG", MotorColors[2]),
            ("R TRIG", MotorColors[3]),
        ];
        for (int i = 0; i < count; i++)
        {
            var sp = new StackPanel { Margin = new(3, 0, 3, 0) };
            sp.Children.Add(new TextBlock
            {
                Text = defs[i].Tag,
                FontFamily = LightingWindow.MonoFont,
                FontSize = 10,
                Foreground = DS.Brush("DS.TextMuted"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new(0, 0, 0, 3),
            });
            var mScale = new ScaleTransform(0.04, 1.0);
            var barFill = new Border
            {
                Background = new SolidColorBrush(defs[i].Col),
                CornerRadius = new(3),
                RenderTransformOrigin = new(0.0, 0.5),
                RenderTransform = mScale,
            };
            sp.Children.Add(new Border
            {
                Height = 7,
                ClipToBounds = true,
                CornerRadius = new(3),
                Background = new SolidColorBrush(Color.FromArgb(90, 24, 34, 54)),
                BorderBrush = DS.Brush("DS.Hairline"),
                BorderThickness = new(0.5),
                Child = barFill,
            });
            dockMeterScales[i] = mScale;
            grid.Children.Add(sp);
        }
        stack.Children.Add(grid);
        box.Child = stack;
        return box;
    }

    FrameworkElement BuildMotorsSection(Func<string, Action, string, Button> makeButton)
    {
        var controls = new StackPanel();
        controls.Children.Add(Kit.Section("MOTORS & TEST", 0));
        controls.Children.Add(Copy("Select a motor tile to feel it, drag sliders to set strength, or configure PC game boost.", 13));
        controls.Children.Add(BuildZones(makeButton));
        controls.Children.Add(Test);

        Readback.Margin = new(0, 2, 0, 6);
        controls.Children.Add(Readback);

        Strength.Maximum = Draft.Max;
        Strength.Style = DS.Style("DS.Slider");
        Strength.Margin = new(0, 4, 0, 10);

        if (Draft.PcMode)
        {
            controls.Children.Add(Kit.Section("GAME RUMBLE BOOST (PC MODE)", 4));
            controls.Children.Add(value);
            controls.Children.Add(Strength);
            var choices = new WrapPanel { Margin = new(-2, 0, -2, 6) };
            foreach (int percent in new[] { 0, 50, 100, 150, 200 })
            {
                int p = percent;
                var b = makeButton(percent == 0 ? "Off" : percent + "%", () => Set(p), "DS.ConsoleBtn");
                b.Margin = new(2);
                b.MinHeight = 30;
                b.Padding = new(11, 4, 11, 4);
                b.Tag = percent;
                presets.Add(b);
                choices.Children.Add(b);
            }
            controls.Children.Add(choices);
            controls.Children.Add(BuildGame());
        }
        else
        {
            var label = Copy("Test-all strength · 100%", 13.5);
            testAllStrength.Style = DS.Style("DS.Slider");
            testAllStrength.ValueChanged += (_, _) => label.Text = $"Test-all strength · {testAllStrength.Value:0}%";
            System.Windows.Automation.AutomationProperties.SetName(testAllStrength, "Test-all strength");
            controls.Children.Add(label);
            controls.Children.Add(testAllStrength);
        }

        return controls;
    }

    UIElement BuildZones(Func<string, Action, string, Button> button)
    {
        const double tileSize = 136;
        var grid = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2, Margin = new(0, 2, 0, 4) };
        foreach (var (zone, name) in zoneNames.Where(n => HasTriggers || !IsTrigger(n.Zone)))
        {
            var z = zone;
            bool right = z is Zone.RightTrigger or Zone.RightGrip;
            var pair = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new(3, 0, 3, 10) };

            var face = button(name, () => _ = RunZone(z), "DS.MotorTile");
            face.Width = face.Height = tileSize;
            face.Padding = new(8, 0, 8, 0);
            face.Margin = new(0);
            face.IsEnabled = !ReviewSandbox.Active;
            System.Windows.Automation.AutomationProperties.SetName(face, "Test " + name.ToLowerInvariant());

            var nameLabel = new TextBlock { Text = name, FontSize = 14, FontWeight = FontWeights.SemiBold, TextAlignment = TextAlignment.Center };
            var pctLabel = new TextBlock { FontSize = 24, FontWeight = FontWeights.SemiBold, TextAlignment = TextAlignment.Center, Margin = new(0, 3, 0, 2) };
            var roleTag = new TextBlock
            {
                Text = "Role: Auto",
                FontFamily = LightingWindow.MonoFont,
                FontSize = 10.5,
                Foreground = DS.Brush("DS.AccentHi"),
                TextAlignment = TextAlignment.Center,
                Opacity = 0.88,
            };
            face.Content = new StackPanel { Children = { nameLabel, pctLabel, roleTag } };

            bool share = Draft.PcMode && !IsTrigger(z);
            var slider = new Slider
            {
                Minimum = 0,
                Maximum = share ? Draft.Max : 100,
                TickFrequency = 5,
                SmallChange = 5,
                LargeChange = 10,
                IsSnapToTickEnabled = true,
                Value = share ? (z == Zone.LeftGrip ? Draft.Left : Draft.Right) : 75,
                Style = DS.Style("DS.MotorSlider"),
                Height = tileSize,
                Margin = right ? new(0, 0, 8, 0) : new(8, 0, 0, 0),
            };
            System.Windows.Automation.AutomationProperties.SetName(slider, name + " strength");
            slider.ValueChanged += (_, _) =>
            {
                pctLabel.Text = (int)slider.Value + "%";
                if (share && !updating)
                {
                    int v = (int)slider.Value;
                    if (z == Zone.LeftGrip) Draft.SetMotors(v, Draft.Right, Draft.Floor);
                    else Draft.SetMotors(Draft.Left, v, Draft.Floor);
                    Changed?.Invoke();
                }
            };
            pctLabel.Text = (int)slider.Value + "%";

            if (right) pair.Children.Add(slider);
            pair.Children.Add(face);
            if (!right) pair.Children.Add(slider);
            grid.Children.Add(pair);
            zones[z] = (face, slider, pctLabel, roleTag);
        }
        return grid;
    }

    FrameworkElement BuildUniversalHapticsSection(Func<string, Action, string, Button> makeButton)
    {
        var stack = new StackPanel();
        stack.Children.Add(Kit.Section("UNIVERSAL HAPTICS ENGINE", 0));
        stack.Children.Add(Copy("Smart 4-motor feel for games, racing dirt/ABS, gunshots, movies, and music with menu auto-mute.", 12.5));

        toggleUniversalBtn = makeButton("Enable Universal Haptics", () => _ = ToggleUniversalHapticsAsync(), "DS.ConsolePrimary");
        toggleUniversalBtn.HorizontalAlignment = HorizontalAlignment.Stretch;
        toggleUniversalBtn.MinHeight = 40;
        toggleUniversalBtn.Margin = new(0, 2, 0, 6);
        stack.Children.Add(toggleUniversalBtn);

        universalStatusText.Margin = new(0, 0, 0, 10);
        stack.Children.Add(universalStatusText);

        stack.Children.Add(new TextBlock
        {
            Text = "Active Mode / Game Profile",
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = DS.Brush("DS.TextMuted"),
            Margin = new(0, 0, 0, 6),
        });

        var profileGrid = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2, Margin = new(-3, 0, -3, 10) };
        (HapticsGenreProfile Profile, string Title, string Sub)[] profileDefs =
        [
            (HapticsGenreProfile.Auto, "Auto Hybrid", "Follows active game or music"),
            (HapticsGenreProfile.Racing, "Racing & Drift", "Gravel, curbs, boost & ABS"),
            (HapticsGenreProfile.FpsAction, "FPS & Action", "Recoil kicks, blasts & light steps"),
            (HapticsGenreProfile.Music, "Music & Vocals", "Vocal triggers + breathing bass"),
            (HapticsGenreProfile.Cinema, "Cinema & Film", "LFE rumble + dialogue suppression"),
            (HapticsGenreProfile.RpgMelee, "RPG & Melee", "Parry clangs & boss impacts"),
            (HapticsGenreProfile.SportsArcade, "Sports & Arcade", "Crisp hits & clean stadium pulse"),
            (HapticsGenreProfile.Off, "Off", "Disable background fusion"),
        ];

        foreach (var (prof, title, sub) in profileDefs)
        {
            var p = prof;
            var cardContent = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            cardContent.Children.Add(new TextBlock { Text = title, FontSize = 12.5, FontWeight = FontWeights.SemiBold, Foreground = DS.Brush("DS.Text") });
            cardContent.Children.Add(new TextBlock { Text = sub, FontSize = 10.5, Foreground = DS.Brush("DS.TextSoft"), TextTrimming = TextTrimming.CharacterEllipsis });

            var card = makeButton("", () => SelectUniversalProfile(p), "DS.ConsoleCard");
            card.Content = cardContent;
            card.HorizontalAlignment = HorizontalAlignment.Stretch;
            card.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            card.MinHeight = 46;
            card.Padding = new(10, 6, 10, 6);
            card.Margin = new(3);
            universalProfileBtns[p] = card;
            profileGrid.Children.Add(card);
        }
        stack.Children.Add(profileGrid);

        // Comfort tier + battery guard selector
        stack.Children.Add(new TextBlock
        {
            Text = "Hand Comfort & Battery Guard",
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = DS.Brush("DS.TextMuted"),
            Margin = new(0, 2, 0, 5),
        });
        var comfortGrid = new System.Windows.Controls.Primitives.UniformGrid { Columns = 3, Margin = new(-3, 0, -3, 10) };
        (HapticsComfortTier Tier, string Label)[] tiers =
        [
            (HapticsComfortTier.Feather, "Feather (Battery)"),
            (HapticsComfortTier.Balanced, "Balanced"),
            (HapticsComfortTier.FullPunch, "Full Punch"),
        ];
        foreach (var (tier, lbl) in tiers)
        {
            var t = tier;
            var b = makeButton(lbl, () => SelectComfortTier(t), "DS.ConsoleBtn");
            b.HorizontalAlignment = HorizontalAlignment.Stretch;
            b.Margin = new(3);
            b.MinHeight = 32;
            b.Padding = new(6, 5, 6, 5);
            b.FontSize = 11.5;
            comfortTierBtns[t] = b;
            comfortGrid.Children.Add(b);
        }
        stack.Children.Add(comfortGrid);

        // Native rumble blend mode selector
        stack.Children.Add(new TextBlock
        {
            Text = "PC Game Rumble Blending",
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = DS.Brush("DS.TextMuted"),
            Margin = new(0, 0, 0, 5),
        });
        var blendGrid = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2, Margin = new(-3, 0, -3, 10) };
        (NativeRumbleBlendMode Mode, string Label)[] blends =
        [
            (NativeRumbleBlendMode.SmartBlend, "Smart Blend (Recommended)"),
            (NativeRumbleBlendMode.TriggersOnly, "Triggers Only (Keep Game Grips)"),
            (NativeRumbleBlendMode.FullOverride, "Full X20Ctl Control"),
            (NativeRumbleBlendMode.NativePriority, "Duck When Game Vibrates"),
        ];
        foreach (var (mode, lbl) in blends)
        {
            var m = mode;
            var b = makeButton(lbl, () => SelectBlendMode(m), "DS.ConsoleBtn");
            b.HorizontalAlignment = HorizontalAlignment.Stretch;
            b.Margin = new(3);
            b.MinHeight = 32;
            b.Padding = new(8, 5, 8, 5);
            b.FontSize = 11.5;
            blendModeBtns[m] = b;
            blendGrid.Children.Add(b);
        }
        stack.Children.Add(blendGrid);

        // Smart automation toggles
        var autoGrid = new System.Windows.Controls.Primitives.UniformGrid { Columns = 3, Margin = new(-3, 0, -3, 10) };
        autoDetectToggleBtn = makeButton("Auto-detect games", () => MutateStore(c => c.AutoDetectGames = !c.AutoDetectGames), "DS.ConsoleBtn");
        askUnknownToggleBtn = makeButton("Ask on new game", () => MutateStore(c => c.AskOnUnknownGame = !c.AskOnUnknownGame), "DS.ConsoleBtn");
        menuMuteToggleBtn = makeButton("Mute in menus", () => MutateStore(c => c.MenuAutoMute = !c.MenuAutoMute), "DS.ConsoleBtn");
        foreach (var tb in new[] { autoDetectToggleBtn, askUnknownToggleBtn, menuMuteToggleBtn })
        {
            tb.HorizontalAlignment = HorizontalAlignment.Stretch;
            tb.Margin = new(3);
            tb.MinHeight = 32;
            tb.Padding = new(6, 5, 6, 5);
            tb.FontSize = 11.5;
            autoGrid.Children.Add(tb);
        }
        stack.Children.Add(autoGrid);

        // Live Event Telemetry Strip (pre-allocated Borders switched via frozen brushes)
        stack.Children.Add(new TextBlock
        {
            Text = "Live Detected Haptic Events",
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = DS.Brush("DS.TextMuted"),
            Margin = new(0, 0, 0, 5),
        });
        var chipWrap = new WrapPanel { Margin = new(-2, 0, -2, 10) };
        HapticsEventTag[] chipTags =
        [
            HapticsEventTag.Gunshot, HapticsEventTag.Explosion, HapticsEventTag.GravelLeft, HapticsEventTag.GravelRight,
            HapticsEventTag.BoostSurge, HapticsEventTag.GearShift, HapticsEventTag.AbsBrake, HapticsEventTag.Footstep,
            HapticsEventTag.MeleeClash, HapticsEventTag.Vocal, HapticsEventTag.BassDrop, HapticsEventTag.MenuMuted,
        ];
        foreach (var tag in chipTags)
        {
            var chip = new Border
            {
                CornerRadius = new CornerRadius(4),
                BorderBrush = CellHairlineBrush,
                BorderThickness = new Thickness(1),
                Background = EventIdleBrush,
                Padding = new Thickness(7, 3, 7, 3),
                Margin = new Thickness(2),
                Child = new TextBlock
                {
                    Text = HapticsProfileLabels.EventLabel(tag),
                    FontFamily = LightingWindow.MonoFont,
                    FontSize = 9.5,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = DS.Brush("DS.Text"),
                },
            };
            eventChipBorders[tag] = chip;
            chipWrap.Children.Add(chip);
        }
        stack.Children.Add(chipWrap);

        // Per-genre Feel Sliders
        (Slider Slider, TextBlock Value) AddFeelSlider(string title, int initVal, Action<HapticsTuning, int> apply)
        {
            var head = new DockPanel { Margin = new(0, 4, 0, 2) };
            var valLbl = Kit.Text(initVal + "%", 12, "DS.Text", FontWeights.SemiBold);
            DockPanel.SetDock(valLbl, Dock.Right);
            head.Children.Add(valLbl);
            head.Children.Add(Kit.Text(title, 12, "DS.TextSoft"));
            stack.Children.Add(head);

            var sl = new Slider { Minimum = 0, Maximum = 100, TickFrequency = 5, IsSnapToTickEnabled = true, Value = initVal, Style = DS.Style("DS.Slider"), Margin = new(0, 0, 0, 6) };
            sl.ValueChanged += (_, _) =>
            {
                int v = (int)Math.Round(sl.Value);
                valLbl.Text = v + "%";
                if (syncingUniversalUi) return;
                var activeProf = UniversalHapticsService.Store.Snapshot().ManualProfile;
                var cur = UniversalHapticsService.Store.GetTuning(activeProf);
                var updated = cur;
                // Preserve clean immutable record copy via local helper
                if (ReferenceEquals(sl, uniMasterSlider)) updated = cur with { MasterIntensity = v };
                else if (ReferenceEquals(sl, uniTextureSlider)) updated = cur with { TextureDetail = v };
                else if (ReferenceEquals(sl, uniImpactSlider)) updated = cur with { ImpactPunch = v };
                else if (ReferenceEquals(sl, uniTriggerSlider)) updated = cur with { TriggerBalance = v };
                UniversalHapticsService.Store.SetTuning(activeProf, updated, persist: true);
                UniversalHapticsService.NotifySettingsChanged();
            };
            stack.Children.Add(sl);
            return (sl, valLbl);
        }

        var dflt = UniversalHapticsService.Store.GetTuning(UniversalHapticsService.Store.Snapshot().ManualProfile);
        (uniMasterSlider, uniMasterVal) = AddFeelSlider("Master Intensity", dflt.MasterIntensity, static (_, _) => { });
        (uniTextureSlider, uniTextureVal) = AddFeelSlider("Texture Detail (Dirt, Curbs, Footsteps)", dflt.TextureDetail, static (_, _) => { });
        (uniImpactSlider, uniImpactVal) = AddFeelSlider("Impact Punch (Gunshots, Explosions, Bass)", dflt.ImpactPunch, static (_, _) => { });
        (uniTriggerSlider, uniTriggerVal) = AddFeelSlider("Trigger Emphasis (LT / RT)", dflt.TriggerBalance, static (_, _) => { });

        // One-Click Signature Demo Effect Triggers
        stack.Children.Add(new TextBlock
        {
            Text = "Instant Effect Preview (Click to Feel)",
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = DS.Brush("DS.TextMuted"),
            Margin = new(0, 4, 0, 6),
        });
        var demoGrid = new System.Windows.Controls.Primitives.UniformGrid { Columns = 3, Margin = new(-3, 0, -3, 2) };
        (HapticsDemoKind Kind, string Label)[] demos =
        [
            (HapticsDemoKind.DirtRoad, "Test Dirt Road"),
            (HapticsDemoKind.GunshotBurst, "Test Gunshot"),
            (HapticsDemoKind.NitroBoost, "Test Nitro Boost"),
            (HapticsDemoKind.AbsBrake, "Test ABS Brake"),
            (HapticsDemoKind.ExplosionWave, "Test Explosion"),
            (HapticsDemoKind.BassDrop, "Test Bass Drop"),
        ];
        foreach (var (kind, lbl) in demos)
        {
            var k = kind;
            var b = makeButton(lbl, () => UniversalHapticsService.TriggerDemo(k), "DS.ConsoleBtn");
            b.HorizontalAlignment = HorizontalAlignment.Stretch;
            b.Margin = new(3);
            b.MinHeight = 32;
            b.Padding = new(6, 5, 6, 5);
            b.FontSize = 11.5;
            demoGrid.Children.Add(b);
        }
        stack.Children.Add(demoGrid);

        return stack;
    }

    async Task ToggleUniversalHapticsAsync()
    {
        if (ReviewSandbox.Active) return;
        StopPattern();
        if (UniversalHapticsService.IsRunning)
        {
            UniversalHapticsService.Stop();
            ShowTriggerState();
            return;
        }
        if (!TriggerEnableGuideDialog.ShownThisSession)
        {
            TriggerEnableGuideDialog.Show(Window.GetWindow(Body), force: false);
            if (Diagnostics.BrowserTriggerRumble.Shared is { Armed: false, Failure: null } shared)
                await shared.Listen();
        }
        UniversalHapticsService.Start();
        if (Diagnostics.BrowserTriggerRumble.Shared is { Armed: true } armedShared)
            armedShared.EnsureFocusForAudioTriggers();
        SyncUniversalHapticsUi();
        SyncStudioTimer();
    }

    void SelectUniversalProfile(HapticsGenreProfile profile)
    {
        UniversalHapticsService.Store.UpdateSettings(c => c.ManualProfile = profile, persist: true);
        UniversalHapticsService.NotifySettingsChanged();
    }

    void SelectComfortTier(HapticsComfortTier tier)
    {
        UniversalHapticsService.Store.UpdateSettings(c => c.ComfortTier = tier, persist: true);
        UniversalHapticsService.NotifySettingsChanged();
    }

    void SelectBlendMode(NativeRumbleBlendMode mode)
    {
        UniversalHapticsService.Store.UpdateSettings(c => c.BlendMode = mode, persist: true);
        UniversalHapticsService.NotifySettingsChanged();
    }

    void MutateStore(Action<HapticsProfileConfig> edit)
    {
        UniversalHapticsService.Store.UpdateSettings(edit, persist: true);
        UniversalHapticsService.NotifySettingsChanged();
    }

    void SyncUniversalHapticsUi()
    {
        var cfg = UniversalHapticsService.Store.Snapshot();
        var tuning = UniversalHapticsService.Store.GetTuning(cfg.ManualProfile);

        syncingUniversalUi = true;
        foreach (var (prof, btn) in universalProfileBtns)
            LightingWindow.SetSelectedState(btn, cfg.ManualProfile == prof);
        foreach (var (tier, btn) in comfortTierBtns)
            LightingWindow.SetSelectedState(btn, cfg.ComfortTier == tier);
        foreach (var (mode, btn) in blendModeBtns)
            LightingWindow.SetSelectedState(btn, cfg.BlendMode == mode);

        if (autoDetectToggleBtn != null) LightingWindow.SetSelectedState(autoDetectToggleBtn, cfg.AutoDetectGames);
        if (askUnknownToggleBtn != null) LightingWindow.SetSelectedState(askUnknownToggleBtn, cfg.AskOnUnknownGame);
        if (menuMuteToggleBtn != null) LightingWindow.SetSelectedState(menuMuteToggleBtn, cfg.MenuAutoMute);

        if (toggleUniversalBtn != null)
        {
            bool on = UniversalHapticsService.IsRunning;
            LightingWindow.SetSelectedState(toggleUniversalBtn, on);
            toggleUniversalBtn.Content = on ? "Universal Haptics Active (Click to Stop)" : "Enable Universal Haptics";
        }

        if (uniMasterSlider != null && uniMasterVal != null)
        {
            uniMasterSlider.Value = tuning.MasterIntensity;
            uniMasterVal.Text = tuning.MasterIntensity + "%";
        }
        if (uniTextureSlider != null && uniTextureVal != null)
        {
            uniTextureSlider.Value = tuning.TextureDetail;
            uniTextureVal.Text = tuning.TextureDetail + "%";
        }
        if (uniImpactSlider != null && uniImpactVal != null)
        {
            uniImpactSlider.Value = tuning.ImpactPunch;
            uniImpactVal.Text = tuning.ImpactPunch + "%";
        }
        if (uniTriggerSlider != null && uniTriggerVal != null)
        {
            uniTriggerSlider.Value = tuning.TriggerBalance;
            uniTriggerVal.Text = tuning.TriggerBalance + "%";
        }
        syncingUniversalUi = false;

        if (UniversalHapticsService.IsRunning)
        {
            var ev = UniversalHapticsService.Latest;
            string app = UniversalHapticsService.DetectedAppName;
            string prof = HapticsProfileLabels.ProfileName(ev.ResolvedProfile);
            string muteTag = ev.IsMenuMuted ? " · Menu Muted" : "";
            universalStatusText.Text = $"Live · {app} -> {prof} · Avg load {Math.Round(ev.AverageDutyCycle * 100):0}%{muteTag}";
        }
        else
        {
            universalStatusText.Text = $"Ready · {HapticsProfileLabels.ComfortName(cfg.ComfortTier)} · {HapticsProfileLabels.BlendName(cfg.BlendMode)}";
        }
    }

    FrameworkElement BuildPatternsSection(Func<string, Action, string, Button> makeButton)
    {
        var stack = new StackPanel();
        stack.Children.Add(Kit.Section("SIGNATURE & CUSTOM PATTERNS", 0));
        stack.Children.Add(Copy("Choose a unique haptic waveform or sculpt your own 8-step 4-motor beat.", 12.5));

        RefreshPatternLibraryCards();
        stack.Children.Add(patternLibraryGrid);

        var composerBox = new Border
        {
            CornerRadius = ThemeCatalog.Round(8),
            BorderBrush = DS.Brush("DS.Hairline"),
            BorderThickness = new(1),
            Padding = new(12, 10, 12, 10),
            Background = new SolidColorBrush(ThemeCatalog.Alpha(ThemeCatalog.Current.Night, 195)),
        };
        var body = new StackPanel();
        body.Children.Add(patternNameText);
        patternDescText.Margin = new(0, 2, 0, 10);
        body.Children.Add(patternDescText);

        // 8-Step x 4-Motor rectangular hardware sequencer matrix (zero pills)
        int rowCount = HasTriggers ? 4 : 2;
        var matrixGrid = new Grid { Margin = new(0, 0, 0, 10) };
        matrixGrid.ColumnDefinitions.Add(new() { Width = new(56) });
        for (int c = 0; c < VibrationPattern.DefaultSteps; c++)
            matrixGrid.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });

        matrixGrid.RowDefinitions.Add(new() { Height = GridLength.Auto });
        matrixGrid.Children.Add(new TextBlock
        {
            Text = "MOTOR",
            FontFamily = LightingWindow.MonoFont,
            FontSize = 9.5,
            Foreground = DS.Brush("DS.TextMuted"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new(0, 0, 4, 4),
        });
        for (int s = 0; s < VibrationPattern.DefaultSteps; s++)
        {
            var txt = new TextBlock
            {
                Text = (s + 1).ToString(),
                FontFamily = LightingWindow.MonoFont,
                FontSize = 10,
                Foreground = DS.Brush("DS.TextMuted"),
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            var hdr = new Border
            {
                CornerRadius = new(4),
                Padding = new(0, 2, 0, 2),
                Margin = new(2, 0, 2, 4),
                Child = txt,
            };
            Grid.SetRow(hdr, 0);
            Grid.SetColumn(hdr, s + 1);
            matrixGrid.Children.Add(hdr);
            stepColumnHeaders[s] = hdr;
            stepHeaderTexts[s] = txt;
        }

        string[] rowNames = ["L GRIP", "R GRIP", "L TRIG", "R TRIG"];
        for (int r = 0; r < rowCount; r++)
        {
            matrixGrid.RowDefinitions.Add(new() { Height = GridLength.Auto });
            int motorIdx = r;
            var label = new TextBlock
            {
                Text = rowNames[r],
                FontFamily = LightingWindow.MonoFont,
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(MotorColors[r]),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new(0, 0, 4, 4),
            };
            Grid.SetRow(label, r + 1);
            matrixGrid.Children.Add(label);

            for (int s = 0; s < VibrationPattern.DefaultSteps; s++)
            {
                int stepIdx = s;
                var fillBar = new Border
                {
                    CornerRadius = new(2),
                    Height = 0,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Background = new SolidColorBrush(MotorColors[r]),
                };
                var cellBtn = new Button
                {
                    Template = SeqCellTemplate,
                    Height = 28,
                    MinHeight = 28,
                    Padding = new(0),
                    Margin = new(2, 0, 2, 4),
                    BorderBrush = CellHairlineBrush,
                    Cursor = Cursors.Hand,
                    Content = fillBar,
                    ToolTip = $"Step {s + 1} · {rowNames[r]} (Click cycles 0/35/70/100%, Right-click clears)",
                };
                cellBtn.Click += (_, _) => CycleMatrixCell(motorIdx, stepIdx, clearOnly: false);
                cellBtn.MouseRightButtonUp += (_, e) => { e.Handled = true; CycleMatrixCell(motorIdx, stepIdx, clearOnly: true); };

                Grid.SetRow(cellBtn, r + 1);
                Grid.SetColumn(cellBtn, s + 1);
                matrixGrid.Children.Add(cellBtn);
                matrixButtons[r, s] = cellBtn;
                matrixFillBars[r, s] = fillBar;
            }
        }
        body.Children.Add(matrixGrid);

        patternSpeedSlider.ValueChanged += (_, _) =>
        {
            patternSpeedLabel.Text = $"Step speed · {patternSpeedSlider.Value:0} ms";
            if (!patternSyncing)
            {
                composerPattern = (composerPattern with { StepMs = (int)Math.Round(patternSpeedSlider.Value) }).Normalized();
                if (patternPlayer.IsPlaying && !ReviewSandbox.Active)
                    patternPlayer.Play(composerPattern, (int)Math.Round(patternStrengthSlider.Value));
            }
        };
        patternStrengthSlider.ValueChanged += (_, _) =>
        {
            patternStrengthLabel.Text = $"Pattern intensity · {patternStrengthSlider.Value:0}%";
            if (!patternSyncing && patternPlayer.IsPlaying)
                patternPlayer.UpdateMasterStrength((int)Math.Round(patternStrengthSlider.Value));
        };

        body.Children.Add(patternSpeedLabel);
        body.Children.Add(patternSpeedSlider.Also(s => s.Margin = new(0, 2, 0, 8)));
        body.Children.Add(patternStrengthLabel);
        body.Children.Add(patternStrengthSlider.Also(s => s.Margin = new(0, 2, 0, 10)));

        var optionsRow = new WrapPanel { Margin = new(-2, 0, -2, 6) };
        patternLoopBtn = makeButton("Loop: On", () =>
        {
            composerPattern = composerPattern with { Loop = !composerPattern.Loop };
            UpdatePatternOptionButtons();
            if (patternPlayer.IsPlaying && !ReviewSandbox.Active)
                patternPlayer.Play(composerPattern, (int)Math.Round(patternStrengthSlider.Value));
        }, "DS.ConsoleBtn");
        patternLoopBtn.Margin = new(2);
        patternLoopBtn.Height = 32; patternLoopBtn.MinHeight = 30; patternLoopBtn.FontSize = 11.5;

        patternRampBtn = makeButton("Shape: Crisp", () =>
        {
            composerPattern = composerPattern with { SmoothRamp = !composerPattern.SmoothRamp };
            UpdatePatternOptionButtons();
            if (patternPlayer.IsPlaying && !ReviewSandbox.Active)
                patternPlayer.Play(composerPattern, (int)Math.Round(patternStrengthSlider.Value));
        }, "DS.ConsoleBtn");
        patternRampBtn.Margin = new(2);
        patternRampBtn.Height = 32; patternRampBtn.MinHeight = 30; patternRampBtn.FontSize = 11.5;

        optionsRow.Children.Add(patternLoopBtn);
        optionsRow.Children.Add(patternRampBtn);
        body.Children.Add(optionsRow);

        var transportRow = new WrapPanel { Margin = new(-2, 4, -2, 0) };
        patternPlayBtn = makeButton("Play Pattern", TogglePlayComposerPattern, "DS.ConsolePrimary");
        patternPlayBtn.Height = 38;
        patternPlayBtn.MinHeight = 36;
        patternPlayBtn.MinWidth = 122;
        patternPlayBtn.Margin = new(2);
        patternPlayBtn.IsEnabled = !ReviewSandbox.Active;
        transportRow.Children.Add(patternPlayBtn);

        var randBtn = makeButton("Randomize", RandomizeComposerPattern, "DS.ConsoleBtn");
        randBtn.Height = 38; randBtn.Margin = new(2);
        var clearBtn = makeButton("Clear", ClearComposerPattern, "DS.ConsoleBtn");
        clearBtn.Height = 38; clearBtn.Margin = new(2);
        var saveBtn = makeButton("+ Save Pattern", SaveComposerPatternWithPrompt, "DS.ConsoleBtn");
        saveBtn.Height = 38; saveBtn.Margin = new(2);
        transportRow.Children.Add(randBtn);
        transportRow.Children.Add(clearBtn);
        transportRow.Children.Add(saveBtn);
        body.Children.Add(transportRow);

        composerBox.Child = body;
        stack.Children.Add(composerBox);
        return stack;
    }

    FrameworkElement BuildAudioRolesSection(Func<string, Action, string, Button> makeButton)
    {
        var stack = new StackPanel();

        var head = new DockPanel { Margin = new(0, 0, 0, 8) };
        roleBeatBadge = new Border
        {
            CornerRadius = new(4),
            Padding = new(7, 2, 7, 2),
            Background = BeatIdleBrush,
            BorderBrush = new SolidColorBrush(Color.FromArgb(110, 0, 229, 255)),
            BorderThickness = new(1),
            Child = Kit.Text("BEAT", 10.5, "DS.Text", FontWeights.Bold),
        };
        DockPanel.SetDock(roleBeatBadge, Dock.Right);
        head.Children.Add(roleBeatBadge);

        roleVibeText = Kit.Text("IDLE", 10.5, "DS.AccentHi", FontWeights.Bold);
        var vibeBadge = new Border
        {
            CornerRadius = new(4),
            Padding = new(7, 2, 7, 2),
            Margin = new(0, 0, 6, 0),
            Background = new SolidColorBrush(Color.FromArgb(36, 167, 139, 250)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(110, 167, 139, 250)),
            BorderThickness = new(1),
            Child = roleVibeText,
        };
        DockPanel.SetDock(vibeBadge, Dock.Right);
        head.Children.Add(vibeBadge);
        head.Children.Add(Kit.Section("LIVE SPECTRUM · 1024-PT FFT", 0));
        stack.Children.Add(head);

        // GPU-transformed 8-band real-time FFT bar chart
        var specCard = new Border
        {
            CornerRadius = ThemeCatalog.Round(8),
            BorderBrush = DS.Brush("DS.Hairline"),
            BorderThickness = new(1),
            Padding = new(12, 10, 12, 10),
            Margin = new(0, 0, 0, 10),
            Background = new SolidColorBrush(ThemeCatalog.Alpha(ThemeCatalog.Current.Night, 195)),
        };
        var specBody = new StackPanel();
        string[] labels = ["SUB", "KCK", "LMD", "VX1", "VX2", "PRS", "TRB", "AIR"];
        Color[] cols =
        [
            Color.FromRgb(0xFF, 0x2E, 0x63),
            Color.FromRgb(0xFF, 0x5E, 0x3A),
            Color.FromRgb(0xFF, 0xB8, 0x00),
            Color.FromRgb(0xFB, 0xBF, 0x24),
            Color.FromRgb(0x22, 0xD3, 0xEE),
            Color.FromRgb(0x38, 0xBD, 0xF8),
            Color.FromRgb(0x81, 0x8C, 0xF8),
            Color.FromRgb(0xC0, 0x84, 0xFC),
        ];
        var eqGrid = new System.Windows.Controls.Primitives.UniformGrid { Columns = 8, Margin = new(-2, 0, -2, 8) };
        for (int b = 0; b < 8; b++)
        {
            var colSp = new StackPanel { Margin = new(2, 0, 2, 0) };
            var barBox = new Border
            {
                Height = 42,
                ClipToBounds = true,
                CornerRadius = new(4),
                Background = new SolidColorBrush(Color.FromArgb(90, 24, 34, 54)),
                BorderBrush = DS.Brush("DS.Hairline"),
                BorderThickness = new(0.5),
            };
            var sScale = new ScaleTransform(1.0, 0.05);
            var fillBar = new Border
            {
                Background = new SolidColorBrush(cols[b]),
                CornerRadius = new(3),
                RenderTransformOrigin = new(0.5, 1.0),
                RenderTransform = sScale,
            };
            barBox.Child = fillBar;
            colSp.Children.Add(barBox);
            colSp.Children.Add(new TextBlock
            {
                Text = labels[b],
                FontFamily = LightingWindow.MonoFont,
                FontSize = 9,
                Foreground = DS.Brush("DS.TextMuted"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new(0, 3, 0, 0),
            });
            roleSpectrumScales[b] = sScale;
            eqGrid.Children.Add(colSp);
        }
        specBody.Children.Add(eqGrid);

        var vocalRow = new Grid();
        vocalRow.ColumnDefinitions.Add(new() { Width = new(56) });
        vocalRow.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        vocalRow.Children.Add(new TextBlock
        {
            Text = "VOCAL",
            FontFamily = LightingWindow.MonoFont,
            FontSize = 10.5,
            Foreground = DS.Brush("DS.TextSoft"),
            VerticalAlignment = VerticalAlignment.Center,
        });
        roleVocalScale = new ScaleTransform(0.04, 1.0);
        var vFill = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0xFB, 0xBF, 0x24)),
            CornerRadius = new(3),
            RenderTransformOrigin = new(0.0, 0.5),
            RenderTransform = roleVocalScale,
        };
        var vTrack = new Border
        {
            Height = 8,
            ClipToBounds = true,
            CornerRadius = new(3),
            Background = new SolidColorBrush(Color.FromArgb(90, 24, 34, 54)),
            BorderBrush = DS.Brush("DS.Hairline"),
            BorderThickness = new(0.5),
            Child = vFill,
        };
        Grid.SetColumn(vTrack, 1);
        vocalRow.Children.Add(vTrack);
        specBody.Children.Add(vocalRow);
        specCard.Child = specBody;
        stack.Children.Add(specCard);

        stack.Children.Add(Kit.Section("PER-MOTOR AUDIO ROLES", 2));
        stack.Children.Add(Copy("Give each grip and trigger its own dedicated audio element.", 12.5));

        var quickRow = new WrapPanel { Margin = new(-2, 0, -2, 8) };
        foreach (var (presetName, map) in new[]
        {
            ("Auto", MotorRoleMap.Default),
            ("Lyrical Stage", MotorRoleMap.LyricalStage),
            ("Drums & Snap", MotorRoleMap.PercussionPro),
            ("Stereo 3D", MotorRoleMap.StereoConcert),
            ("Bass + Vocals", MotorRoleMap.BassAndVocals),
        })
        {
            var captured = map;
            var pb = makeButton(presetName, () => SetMotorRoles(captured), "DS.ConsoleBtn");
            pb.Margin = new(2);
            pb.Height = 30; pb.MinHeight = 28; pb.FontSize = 11.5; pb.Padding = new(10, 4, 10, 4);
            quickRow.Children.Add(pb);
        }
        stack.Children.Add(quickRow);

        (string Name, Color Col)[] motorDefs =
        [
            ("Left Grip", MotorColors[0]),
            ("Right Grip", MotorColors[1]),
            ("Left Trigger", MotorColors[2]),
            ("Right Trigger", MotorColors[3]),
        ];
        int visibleMotors = HasTriggers ? 4 : 2;
        for (int i = 0; i < visibleMotors; i++)
        {
            int idx = i;
            var card = new Border
            {
                CornerRadius = ThemeCatalog.Round(8),
                BorderBrush = DS.Brush("DS.Hairline"),
                BorderThickness = new(1),
                Padding = new(11, 8, 11, 8),
                Margin = new(0, 0, 0, 7),
                Background = new SolidColorBrush(ThemeCatalog.Alpha(ThemeCatalog.Current.Night, 185)),
            };

            var cardGrid = new Grid();
            cardGrid.RowDefinitions.Add(new() { Height = GridLength.Auto });
            cardGrid.RowDefinitions.Add(new() { Height = GridLength.Auto });

            var headerRow = new DockPanel { Margin = new(0, 0, 0, 4) };
            var cycleControls = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var prevBtn = makeButton("<", () => CycleSingleMotorRole(idx, -1), "DS.ConsoleStepBtn");
            prevBtn.Margin = new(0, 0, 4, 0);
            var roleVal = Kit.Text("Auto (Vibe)", 12.5, "DS.AccentHi", FontWeights.SemiBold).Also(t =>
            {
                t.VerticalAlignment = VerticalAlignment.Center;
                t.MinWidth = 112;
                t.TextAlignment = TextAlignment.Center;
            });
            var nextBtn = makeButton(">", () => CycleSingleMotorRole(idx, 1), "DS.ConsoleStepBtn");
            nextBtn.Margin = new(4, 0, 0, 0);
            cycleControls.Children.Add(prevBtn);
            cycleControls.Children.Add(roleVal);
            cycleControls.Children.Add(nextBtn);
            DockPanel.SetDock(cycleControls, Dock.Right);
            headerRow.Children.Add(cycleControls);

            var titleRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            titleRow.Children.Add(new Border
            {
                Width = 9,
                Height = 9,
                CornerRadius = new(5),
                Background = new SolidColorBrush(motorDefs[idx].Col),
                Margin = new(0, 0, 7, 0),
                VerticalAlignment = VerticalAlignment.Center,
            });
            titleRow.Children.Add(Kit.Text(motorDefs[idx].Name, 13.5, "DS.Text", FontWeights.SemiBold));
            headerRow.Children.Add(titleRow);
            cardGrid.Children.Add(headerRow);

            var subGrid = new Grid();
            subGrid.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
            subGrid.ColumnDefinitions.Add(new() { Width = new(82) });
            var descText = Kit.Text("", 11.5, "DS.TextMuted").Also(t => { t.TextWrapping = TextWrapping.Wrap; t.Margin = new(0, 0, 8, 0); });
            subGrid.Children.Add(descText);

            var mScale = new ScaleTransform(0.04, 1.0);
            var mFill = new Border
            {
                Background = new SolidColorBrush(motorDefs[idx].Col),
                CornerRadius = new(3),
                RenderTransformOrigin = new(0.0, 0.5),
                RenderTransform = mScale,
            };
            var mTrack = new Border
            {
                Height = 6,
                ClipToBounds = true,
                CornerRadius = new(3),
                Background = new SolidColorBrush(Color.FromArgb(90, 24, 34, 54)),
                Child = mFill,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(mTrack, 1);
            subGrid.Children.Add(mTrack);

            Grid.SetRow(subGrid, 1);
            cardGrid.Children.Add(subGrid);
            card.Child = cardGrid;
            stack.Children.Add(card);
            roleCardUi[idx] = (roleVal, descText, mScale);
        }

        stack.Children.Add(Kit.Section("AUDIO HAPTICS OUTPUT", 4));
        var hapChoicesWrap = new WrapPanel { Margin = new(-2, 0, -2, 8) };
        foreach (var (pct, label) in new (int, string)[] { (0, "Off"), (35, "Light 35%"), (65, "Punch 65%"), (100, "Max 100%") })
        {
            int captured = pct;
            var hb = makeButton(label, async () => await SetAudioHapticsStrengthAsync(captured), "DS.ConsoleBtn");
            hb.Height = 32; hb.MinHeight = 30; hb.FontSize = 11.5; hb.Margin = new(2);
            audioHapStrengthBtns[captured] = hb;
            hapChoicesWrap.Children.Add(hb);
        }
        stack.Children.Add(hapChoicesWrap);

        var hapActionRow = new WrapPanel { Margin = new(-2, 4, -2, 0) };
        toggleAudioHapBtn = makeButton("Start Live Audio Haptics", async () =>
        {
            if (LightingEffectsService.Status.IsRunning && LightingEffectsService.CurrentSpec.Kind == LightingEffectKind.AudioReactive)
                await LightingEffectsService.StopAsync(restoreBaseline: true);
            else
                await StartAudioHapticsWithCurrentRolesAsync();
        }, "DS.ConsolePrimary");
        toggleAudioHapBtn.Height = 38;
        toggleAudioHapBtn.MinHeight = 36;
        toggleAudioHapBtn.Margin = new(2);
        toggleAudioHapBtn.IsEnabled = !ReviewSandbox.Active;
        hapActionRow.Children.Add(toggleAudioHapBtn);
        stack.Children.Add(hapActionRow);

        return stack;
    }

    void RefreshPatternLibraryCards()
    {
        patternLibraryGrid.Children.Clear();
        patternCardButtons.Clear();

        IEnumerable<VibrationPattern> all = VibrationPatternEngine.BuiltIns.Concat(patternStore.Patterns);
        foreach (var pat in all)
        {
            var captured = pat;
            var title = Kit.Text(pat.Name, 12.5, "DS.Text", FontWeights.SemiBold).Also(t => t.TextTrimming = TextTrimming.CharacterEllipsis);
            var sub = Kit.Text($"{pat.Category} · {pat.StepMs} ms{(pat.Loop ? " · Loop" : "")}", 10.5, "DS.TextMuted");
            var sp = new StackPanel { Margin = new(6, 0, 0, 0), Children = { title, sub } };

            var cardGrid = new Grid();
            cardGrid.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
            cardGrid.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
            cardGrid.Children.Add(sp);

            var faceBtn = new Button
            {
                Style = DS.Style("DS.ConsoleCard"),
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Padding = new(10, 8, 10, 8),
                MinHeight = 46,
                Height = double.NaN,
                Margin = new(3),
                Content = cardGrid,
                ToolTip = pat.Description,
            };

            if (!pat.BuiltIn)
            {
                var delBtn = new Button
                {
                    Content = "×",
                    Style = DS.Style("DS.ConsoleStepBtn"),
                    FontSize = 12,
                    Padding = new(5, 0, 5, 0),
                    MinHeight = 22,
                    Height = 22,
                    MinWidth = 22,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                delBtn.Click += (_, e) =>
                {
                    e.Handled = true;
                    if (patternStore.DeletePattern(captured.Id))
                        RefreshPatternLibraryCards();
                };
                Grid.SetColumn(delBtn, 1);
                cardGrid.Children.Add(delBtn);
            }

            faceBtn.Click += (_, _) =>
            {
                bool sameAndPlaying = patternPlayer.IsPlaying && patternPlayer.ActivePattern?.Id == captured.Id;
                LoadPatternIntoComposer(captured);
                if (!ReviewSandbox.Active)
                {
                    if (sameAndPlaying) StopPattern();
                    else StartPlayComposerPattern();
                }
            };

            patternLibraryGrid.Children.Add(faceBtn);
            LightingWindow.SetSelectedState(faceBtn, composerPattern.Id == pat.Id);
            patternCardButtons.Add((pat.Id, faceBtn));
        }
    }

    void LoadPatternIntoComposer(VibrationPattern pattern)
    {
        composerPattern = pattern.Normalized();
        patternSyncing = true;
        try
        {
            patternSpeedSlider.Value = Math.Clamp(composerPattern.StepMs, (int)patternSpeedSlider.Minimum, (int)patternSpeedSlider.Maximum);
            patternSpeedLabel.Text = $"Step speed · {patternSpeedSlider.Value:0} ms";
            patternStrengthLabel.Text = $"Pattern intensity · {patternStrengthSlider.Value:0}%";
            patternNameText.Text = composerPattern.Name;
            patternDescText.Text = composerPattern.Description;
            UpdatePatternOptionButtons();
            RenderComposerMatrix();
        }
        finally
        {
            patternSyncing = false;
        }
        foreach (var (id, btn) in patternCardButtons)
            LightingWindow.SetSelectedState(btn, id == composerPattern.Id);
    }

    void UpdatePatternOptionButtons()
    {
        if (patternLoopBtn != null)
        {
            patternLoopBtn.Content = composerPattern.Loop ? "Loop: On" : "Loop: One-shot";
            LightingWindow.SetSelectedState(patternLoopBtn, composerPattern.Loop);
        }
        if (patternRampBtn != null)
        {
            patternRampBtn.Content = composerPattern.SmoothRamp ? "Shape: Smooth Wave" : "Shape: Crisp Pulse";
            LightingWindow.SetSelectedState(patternRampBtn, composerPattern.SmoothRamp);
        }
        if (patternPlayBtn != null)
            patternPlayBtn.Content = patternPlayer.IsPlaying ? "Stop Pattern" : "Play Pattern";
    }

    void CycleMatrixCell(int motorIdx, int stepIdx, bool clearOnly)
    {
        var steps = composerPattern.Steps.ToArray();
        var s = steps[stepIdx];
        int current = motorIdx switch { 0 => s.LeftGrip, 1 => s.RightGrip, 2 => s.LeftTrigger, _ => s.RightTrigger };
        int next = clearOnly ? 0 : current switch
        {
            0 => 35,
            < 55 => 70,
            < 90 => 100,
            _ => 0,
        };
        steps[stepIdx] = motorIdx switch
        {
            0 => s with { LeftGrip = next },
            1 => s with { RightGrip = next },
            2 => s with { LeftTrigger = next },
            _ => s with { RightTrigger = next },
        };
        composerPattern = (composerPattern with { Steps = steps }).Normalized();
        RenderComposerMatrix();
        if (patternPlayer.IsPlaying && !ReviewSandbox.Active)
            patternPlayer.Play(composerPattern, (int)Math.Round(patternStrengthSlider.Value));
    }

    void RenderComposerMatrix()
    {
        int rowCount = HasTriggers ? 4 : 2;
        var steps = composerPattern.Steps;
        for (int s = 0; s < VibrationPattern.DefaultSteps; s++)
        {
            bool activeCol = s == activePlayheadStep;
            if (stepColumnHeaders[s] != null)
                stepColumnHeaders[s].Background = activeCol ? PlayheadActiveBrush : Brushes.Transparent;
            if (stepHeaderTexts[s] != null)
                stepHeaderTexts[s].Foreground = activeCol ? DS.Brush("DS.Text") : DS.Brush("DS.TextMuted");
        }

        for (int r = 0; r < rowCount; r++)
        {
            for (int s = 0; s < VibrationPattern.DefaultSteps && s < steps.Length; s++)
            {
                int val = r switch
                {
                    0 => steps[s].LeftGrip,
                    1 => steps[s].RightGrip,
                    2 => steps[s].LeftTrigger,
                    _ => steps[s].RightTrigger,
                };
                if (matrixFillBars[r, s] is { } bar)
                {
                    bar.Height = val <= 0 ? 0 : Math.Max(4, val / 100.0 * 24.0);
                    bar.Opacity = val <= 0 ? 0.0 : (0.50 + 0.50 * val / 100.0);
                }
                if (matrixButtons[r, s] is { } btn)
                {
                    btn.BorderBrush = s == activePlayheadStep
                        ? DS.Brush("DS.AccentHi")
                        : (val > 0 ? new SolidColorBrush(Color.FromArgb(145, MotorColors[r].R, MotorColors[r].G, MotorColors[r].B)) : CellHairlineBrush);
                }
            }
        }
    }

    void TogglePlayComposerPattern()
    {
        if (ReviewSandbox.Active) return;
        if (patternPlayer.IsPlaying)
            StopPattern();
        else
            StartPlayComposerPattern();
    }

    void StartPlayComposerPattern()
    {
        if (ReviewSandbox.Active) return;

        if (LightingEffectsService.Status.IsRunning &&
            LightingEffectsService.CurrentSpec is { Kind: LightingEffectKind.AudioReactive, AudioHapticsStrength: > 0 } runningSpec)
        {
            LightingEffectsService.UpdateRunningSpec(runningSpec with { AudioHapticsStrength = 0 }, remember: false);
        }

        bool hasTriggerSteps = HasTriggers && composerPattern.Steps.Any(s => s.LeftTrigger > 0 || s.RightTrigger > 0);
        if (hasTriggerSteps && !TriggerEnableGuideDialog.ShownThisSession)
        {
            TriggerEnableGuideDialog.Show(Window.GetWindow(Body), force: false);
            if (Diagnostics.BrowserTriggerRumble.Shared is { Armed: false, Failure: null } shared)
                _ = shared.Listen();
        }

        int master = (int)Math.Round(patternStrengthSlider.Value);
        patternPlayer.Play(composerPattern, master);
        Readback.Text = $"Playing custom pattern: {composerPattern.Name} ({composerPattern.StepMs} ms/step).";
        UpdatePatternOptionButtons();
        SyncStudioTimer();
    }

    void OnPatternFrame(HapticsFrame hf, int step, bool playing)
    {
        UniversalHapticsService.SetManualMotorOverride(playing || testing);
        activePlayheadStep = playing ? step : -1;
        UpdatePatternUiState(hf, activePlayheadStep, playing);
    }

    void UpdatePatternUiState(HapticsFrame hf, int step, bool playing)
    {
        activePlayheadStep = playing ? step : -1;
        RenderComposerMatrix();
        UpdatePatternOptionButtons();
        if (playing)
        {
            SetDockBars(hf);
            Visualizer.SetLiveMotors(hf.LeftGrip, hf.RightGrip, hf.LeftTrigger, hf.RightTrigger);
            dockStatusText.Text = $"Pattern: {composerPattern.Name} (Step {Math.Max(1, step + 1)}/{composerPattern.Steps.Length})";
        }
        else if (!testing)
        {
            Visualizer.ClearLiveMotors();
            SetDockBars(HapticsFrame.Zero);
            ShowTriggerState();
        }
    }

    void RandomizeComposerPattern()
    {
        var rng = Random.Shared;
        int[] paletteLevels = [0, 0, 35, 70, 100];
        var nextSteps = new VibrationPatternStep[VibrationPattern.DefaultSteps];
        for (int i = 0; i < nextSteps.Length; i++)
        {
            nextSteps[i] = new VibrationPatternStep(
                paletteLevels[rng.Next(paletteLevels.Length)],
                paletteLevels[rng.Next(paletteLevels.Length)],
                HasTriggers ? paletteLevels[rng.Next(paletteLevels.Length)] : 0,
                HasTriggers ? paletteLevels[rng.Next(paletteLevels.Length)] : 0);
        }
        nextSteps[0] = new(90, 40, HasTriggers ? 80 : 0, 0);
        composerPattern = (composerPattern with
        {
            Id = "custom-draft",
            Name = "Custom Wave",
            BuiltIn = false,
            Description = "Generated custom 4-motor wave.",
            Steps = nextSteps,
        }).Normalized();
        LoadPatternIntoComposer(composerPattern);
        if (patternPlayer.IsPlaying && !ReviewSandbox.Active)
            patternPlayer.Play(composerPattern, (int)Math.Round(patternStrengthSlider.Value));
    }

    void ClearComposerPattern()
    {
        composerPattern = (composerPattern with
        {
            Steps = Enumerable.Repeat(VibrationPatternStep.Zero, VibrationPattern.DefaultSteps).ToArray(),
        }).Normalized();
        RenderComposerMatrix();
        if (patternPlayer.IsPlaying)
            StopPattern();
    }

    void SaveComposerPatternWithPrompt()
    {
        if (LightingEffectsSection.PromptText("Save Custom Vibration Pattern", "Pattern name:", composerPattern.Name) is not { } name)
            return;
        try
        {
            string? overwriteId = !composerPattern.BuiltIn && composerPattern.Id.StartsWith("user-", StringComparison.Ordinal)
                ? composerPattern.Id
                : null;
            var saved = patternStore.SavePattern(name, composerPattern, overwriteId);
            LoadPatternIntoComposer(saved);
            RefreshPatternLibraryCards();
            LoadPatternIntoComposer(saved);
            Readback.Text = $"Saved custom vibration pattern \"{saved.Name}\".";
        }
        catch { }
    }

    void SetMotorRoles(MotorRoleMap nextRoles)
    {
        var baseSpec = LightingEffectsService.CurrentSpec;
        var updated = baseSpec with { AudioMotorRoles = nextRoles };
        bool didHotUpdate = LightingEffectsService.UpdateRunningSpec(updated, remember: true);
        if (!didHotUpdate)
        {
            if (!ReviewSandbox.Active)
            {
                try { LightingEffectsService.Store.RememberLastEffect(updated); } catch { }
            }
            LightingEffectsService.SetPreviewDraft(updated);
        }
        SyncRoleAndAudioUi();
    }

    void CycleSingleMotorRole(int motorIndex, int delta)
    {
        var r = LightingEffectsService.CurrentSpec.AudioMotorRoles;
        MotorRoleMap next = motorIndex switch
        {
            0 => r with { LeftGrip = AudioHapticsEngine.CycleRole(r.LeftGrip, delta) },
            1 => r with { RightGrip = AudioHapticsEngine.CycleRole(r.RightGrip, delta) },
            2 => r with { LeftTrigger = AudioHapticsEngine.CycleRole(r.LeftTrigger, delta) },
            _ => r with { RightTrigger = AudioHapticsEngine.CycleRole(r.RightTrigger, delta) },
        };
        SetMotorRoles(next);
    }

    async Task SetAudioHapticsStrengthAsync(int strength)
    {
        var cur = LightingEffectsService.CurrentSpec;
        bool enabling = strength > 0 && cur.AudioHapticsStrength == 0;
        var next = cur with { AudioHapticsStrength = strength };

        if (enabling && !ReviewSandbox.Active)
        {
            TriggerEnableGuideDialog.Show(Window.GetWindow(Body), force: false);
            if (Diagnostics.BrowserTriggerRumble.Shared is { Armed: false, Failure: null } shared)
                await shared.Listen();
        }

        if (LightingEffectsService.Status.IsRunning && cur.Kind == LightingEffectKind.AudioReactive)
        {
            LightingEffectsService.UpdateRunningSpec(next, remember: true);
        }
        else if (strength > 0)
        {
            await StartAudioHapticsWithCurrentRolesAsync(overrideStrength: strength);
        }
        else
        {
            if (!ReviewSandbox.Active)
                try { LightingEffectsService.Store.RememberLastEffect(next); } catch { }
            LightingEffectsService.SetPreviewDraft(next);
        }
        SyncRoleAndAudioUi();
    }

    async Task StartAudioHapticsWithCurrentRolesAsync(int? overrideStrength = null)
    {
        if (ReviewSandbox.Active) return;
        StopPattern();
        var cur = LightingEffectsService.CurrentSpec;
        int strength = overrideStrength ?? (cur.AudioHapticsStrength > 0 ? cur.AudioHapticsStrength : 65);
        string[] palette = cur.Palette is { Length: > 1 }
            ? cur.Palette
            : ["#FF0040", "#00F5D4", "#3D8BFF", "#FFB800", "#A855F7"];

        var spec = cur with
        {
            Kind = LightingEffectKind.AudioReactive,
            Palette = palette,
            AudioHapticsStrength = strength,
            StepIntervalMs = Math.Min(cur.StepIntervalMs, 65),
        };

        if (!TriggerEnableGuideDialog.ShownThisSession)
        {
            TriggerEnableGuideDialog.Show(Window.GetWindow(Body), force: false);
            if (Diagnostics.BrowserTriggerRumble.Shared is { Armed: false, Failure: null } shared)
                await shared.Listen();
        }

        await LightingEffectsService.ApplyAsync(spec);
        if (Diagnostics.BrowserTriggerRumble.Shared is { Armed: true } armedShared)
            armedShared.EnsureFocusForAudioTriggers();
        SyncRoleAndAudioUi();
        SyncStudioTimer();
    }

    void SyncRoleAndAudioUi()
    {
        var spec = LightingEffectsService.CurrentSpec;
        var roles = spec.AudioMotorRoles;
        MotorAudioRole[] roleVals = [roles.LeftGrip, roles.RightGrip, roles.LeftTrigger, roles.RightTrigger];

        foreach (var (z, name) in zoneNames)
        {
            if (!zones.TryGetValue(z, out var ui)) continue;
            var r = z switch
            {
                Zone.LeftGrip => roles.LeftGrip,
                Zone.RightGrip => roles.RightGrip,
                Zone.LeftTrigger => roles.LeftTrigger,
                _ => roles.RightTrigger,
            };
            ui.RoleTag.Text = $"Role: {AudioHapticsEngine.RoleShortLabel(r)}";
        }

        for (int i = 0; i < (HasTriggers ? 4 : 2); i++)
        {
            if (roleCardUi[i].Label == null) continue;
            roleCardUi[i].Label.Text = AudioHapticsEngine.RoleLabel(roleVals[i]);
            roleCardUi[i].Desc.Text = AudioHapticsEngine.RoleDescription(roleVals[i]);
        }

        foreach (var (pct, btn) in audioHapStrengthBtns)
            LightingWindow.SetSelectedState(btn, spec.AudioHapticsStrength == pct);

        if (toggleAudioHapBtn != null)
        {
            bool audioRunning = LightingEffectsService.Status.IsRunning && spec.Kind == LightingEffectKind.AudioReactive;
            toggleAudioHapBtn.Content = audioRunning ? "Stop Live Audio Haptics" : "Start Live Audio Haptics";
        }
    }

    void SyncStudioTimer()
    {
        bool needTimer = Body.IsLoaded && !ReviewSandbox.Active;
        if (needTimer)
        {
            if (!studioTimer.IsEnabled) studioTimer.Start();
        }
        else if (studioTimer.IsEnabled)
        {
            studioTimer.Stop();
        }
    }

    void RefreshLiveStudioTelemetry()
    {
        if (ReviewSandbox.Active || !Body.IsVisible) return;

        var spec = LightingEffectsService.CurrentSpec;
        bool runningUniversal = UniversalHapticsService.IsRunning;
        bool runningAudio = LightingEffectsService.Status.IsRunning && spec.Kind == LightingEffectKind.AudioReactive;
        bool audioSectionVisible = inspectorScroll.VerticalOffset >= 260;
        AudioFrame af;
        if (runningAudio || runningUniversal)
        {
            af = SystemAudioLevelSampler.LatestFrame;
        }
        else if (audioSectionVisible)
        {
            SystemAudioLevelSampler.EnsureStarted();
            af = SystemAudioLevelSampler.SampleAudioFrame();
        }
        else
        {
            af = default;
        }

        var uniEval = UniversalHapticsService.Latest;
        HapticsFrame hf = runningUniversal
            ? uniEval.Motors
            : (runningAudio && spec.AudioHapticsStrength > 0
                ? SystemAudioLevelSampler.LatestHaptics
                : AudioHapticsEngine.Evaluate(af, spec.AudioHapticsStyle, Math.Max(spec.AudioHapticsStrength, audioSectionVisible ? 75 : 0), spec.AudioMotorRoles, 0.0));

        for (int i = 0; i < 8; i++)
        {
            if (roleSpectrumScales[i] is { } st)
                st.ScaleY = Math.Clamp(af.Spectrum[i], 0.04, 0.98);
        }
        if (roleVocalScale != null)
            roleVocalScale.ScaleX = Math.Clamp(af.VocalPresence, 0.04, 0.98);
        if (roleBeatBadge != null)
        {
            var nextBeat = af.BeatPulse > 0.30f ? BeatActiveBrush : BeatIdleBrush;
            if (!ReferenceEquals(roleBeatBadge.Background, nextBeat))
                roleBeatBadge.Background = nextBeat;
        }
        if (roleVibeText != null)
        {
            string vibeLabel = af.Vibe switch
            {
                AudioMusicVibe.AmbientChill => "AUTO: CHILL",
                AudioMusicVibe.VocalAcoustic => "AUTO: VOCAL",
                AudioMusicVibe.ElectronicDrop => "AUTO: DROP",
                AudioMusicVibe.RockMetal => "AUTO: ROCK",
                AudioMusicVibe.GroovePop => "AUTO: GROOVE",
                _ => "IDLE",
            };
            if (roleVibeText.Text != vibeLabel)
                roleVibeText.Text = vibeLabel;
        }

        foreach (var (tag, border) in eventChipBorders)
        {
            bool lit = runningUniversal && (uniEval.PrimaryEvent == tag || uniEval.SecondaryEvent == tag || (tag == HapticsEventTag.MenuMuted && uniEval.IsMenuMuted));
            var targetBg = lit ? EventActiveBrush : EventIdleBrush;
            if (!ReferenceEquals(border.Background, targetBg))
                border.Background = targetBg;
        }

        if (runningUniversal)
        {
            string app = UniversalHapticsService.DetectedAppName;
            string prof = HapticsProfileLabels.ProfileName(uniEval.ResolvedProfile);
            string muteTag = uniEval.IsMenuMuted ? " · Menu Muted" : "";
            string nextStatus = $"Live · {app} -> {prof} · Avg load {Math.Round(uniEval.AverageDutyCycle * 100):0}%{muteTag}";
            if (universalStatusText.Text != nextStatus)
                universalStatusText.Text = nextStatus;
        }

        double[] mVals = [hf.LeftGrip, hf.RightGrip, hf.LeftTrigger, hf.RightTrigger];
        for (int i = 0; i < (HasTriggers ? 4 : 2); i++)
        {
            if (roleCardUi[i].Scale is { } ms)
                ms.ScaleX = Math.Clamp(mVals[i], 0.04, 0.98);
        }

        if (!patternPlayer.IsPlaying && !testing && (runningUniversal || (runningAudio && spec.AudioHapticsStrength > 0)))
        {
            SetDockBars(hf);
            dockStatusText.Text = runningUniversal ? universalStatusText.Text : SystemAudioLevelSampler.HapticsPathSummary;
            Visualizer.SetLiveMotors(hf.LeftGrip, hf.RightGrip, hf.LeftTrigger, hf.RightTrigger);
        }
    }

    void SetDockBars(HapticsFrame hf)
    {
        double[] vals = [hf.LeftGrip, hf.RightGrip, hf.LeftTrigger, hf.RightTrigger];
        int count = HasTriggers ? 4 : 2;
        for (int i = 0; i < count; i++)
        {
            if (dockMeterScales[i] is { } st)
                st.ScaleX = Math.Clamp(vals[i], 0.04, 0.98);
        }
    }

    async Task RunZone(Zone zone)
    {
        if (testing || ReviewSandbox.Active) return;
        StopPattern();
        int percent = ZoneStrength(zone);
        string name = zoneNames.First(n => n.Zone == zone).Name;
        if (zone is Zone.LeftGrip or Zone.RightGrip) SelectRegion(name);
        if (percent == 0) { Readback.Text = name + " is at 0%."; return; }
        var face = zones[zone].Face;
        SetTesting(face, true);
        double levelNorm = percent / 100.0;
        var previewHf = zone switch
        {
            Zone.LeftGrip => new HapticsFrame(levelNorm, 0, 0, 0),
            Zone.RightGrip => new HapticsFrame(0, levelNorm, 0, 0),
            Zone.LeftTrigger => new HapticsFrame(0, 0, levelNorm, 0),
            _ => new HapticsFrame(0, 0, 0, levelNorm),
        };
        SetDockBars(previewHf);
        Visualizer.SetLiveMotors(previewHf.LeftGrip, previewHf.RightGrip, previewHf.LeftTrigger, previewHf.RightTrigger);

        try
        {
            if (IsTrigger(zone))
            {
                var shared = Diagnostics.BrowserTriggerRumble.Shared;
                int leftLink = zone == Zone.LeftTrigger ? percent : 0;
                int rightLink = zone == Zone.RightTrigger ? percent : 0;
                if (shared == null)
                {
                    Readback.Text = $"Buzzing the {name.ToLowerInvariant()} at {percent}% (2.4G/USB linked channel)...";
                    bool linked = await ControllerRumble.Test(leftLink, rightLink, 1000);
                    Readback.Text = linked ? $"{name} pulsed at {percent}% via 2.4G/USB linked channel (press M + BACK on pad if trigger vibration is off)." : "Trigger rumble isn't available.";
                    return;
                }
                if (!shared.Armed)
                {
                    TriggerEnableGuideDialog.Show(Window.GetWindow(Body), force: false);
                    await shared.Listen();
                    if (shared.Failure != null)
                    {
                        bool linked = await ControllerRumble.Test(leftLink, rightLink, 1000);
                        Readback.Text = linked ? $"{name} pulsed at {percent}% via 2.4G/USB linked channel (press M + BACK on pad if trigger vibration is off). {shared.Failure}" : shared.Failure;
                        return;
                    }
                    Readback.Text = $"Buzzing the {name.ToLowerInvariant()} at {percent}% (2.4G/USB linked channel)...";
                    bool fallbackSent = await ControllerRumble.Test(leftLink, rightLink, 1000);
                    Readback.Text = fallbackSent
                        ? $"{name} pulsed at {percent}% via 2.4G/USB linked channel (press M + BACK on pad if trigger vibration is off). Press any controller button once to detect independent Bluetooth triggers."
                        : "Press any button on the controller to turn on trigger rumble, then click the trigger again.";
                    return;
                }
                if (shared is { Armed: true, SupportsTriggerRumble: true, Failure: null })
                {
                    Readback.Text = $"Buzzing the {name.ToLowerInvariant()} at {percent}%...";
                    bool sent = await shared.Play(zone == Zone.LeftTrigger ? percent / 100.0 : 0, zone == Zone.RightTrigger ? percent / 100.0 : 0, 1000);
                    if (sent)
                    {
                        await Task.Delay(1000);
                        Readback.Text = $"{name} buzzed at {percent}%.";
                    }
                    else
                    {
                        bool linked = await ControllerRumble.Test(leftLink, rightLink, 1000);
                        Readback.Text = linked
                            ? $"{name} pulsed at {percent}% via linked channel (press M + BACK on pad if trigger vibration is off, or use Bluetooth mode for independent triggers)."
                            : "The controller isn't reachable for trigger rumble right now.";
                    }
                }
                else
                {
                    Readback.Text = $"Buzzing the {name.ToLowerInvariant()} at {percent}% (2.4G/USB linked channel)...";
                    bool sent = await ControllerRumble.Test(leftLink, rightLink, 1000);
                    Readback.Text = sent
                        ? $"{name} pulsed at {percent}% via 2.4G/USB linked channel. Press M + BACK on the controller if trigger vibration is off, or use Bluetooth mode for independent triggers."
                        : "No controller found.";
                }
            }
            else
            {
                Readback.Text = $"Buzzing the {name.ToLowerInvariant()} at {percent}%...";
                bool sent = await ControllerRumble.Test(zone == Zone.LeftGrip ? percent : 0, zone == Zone.RightGrip ? percent : 0);
                Readback.Text = sent ? $"{name} buzzed at {percent}%." : "No controller found.";
            }
        }
        finally
        {
            Visualizer.ClearLiveMotors();
            SetDockBars(HapticsFrame.Zero);
            SetTesting(face, false);
        }
    }

    /// <summary>All four at once, each at its own strength (independent 4-motor over Bluetooth Classic, or linked channels on 2.4G/USB).</summary>
    async Task RunAll()
    {
        if (testing || ReviewSandbox.Active) return;
        StopPattern();
        int Level(Zone zone) => ZoneStrength(zone) * (int)testAllStrength.Value / 100;
        if (zones.Keys.All(z => Level(z) == 0)) { Readback.Text = "All motors are at 0%."; return; }
        SelectRegion("All");
        SetTesting(Test, true);
        Readback.Text = HasTriggers ? "Buzzing all four..." : "Buzzing both grips...";
        var previewHf = new HapticsFrame(
            Level(Zone.LeftGrip) / 100.0,
            Level(Zone.RightGrip) / 100.0,
            Level(Zone.LeftTrigger) / 100.0,
            Level(Zone.RightTrigger) / 100.0);
        SetDockBars(previewHf);
        Visualizer.SetLiveMotors(previewHf.LeftGrip, previewHf.RightGrip, previewHf.LeftTrigger, previewHf.RightTrigger);

        try
        {
            var shared = Diagnostics.BrowserTriggerRumble.Shared;
            if (HasTriggers && shared is { Armed: true, SupportsTriggerRumble: true, Failure: null })
            {
                bool sent4 = await shared.Play4(previewHf.LeftGrip, previewHf.RightGrip, previewHf.LeftTrigger, previewHf.RightTrigger, 1000);
                if (sent4)
                {
                    await Task.Delay(1000);
                    Readback.Text = "4-motor grip and trigger test sent.";
                    return;
                }
            }
            int leftLevel = HasTriggers ? Math.Max(Level(Zone.LeftGrip), Level(Zone.LeftTrigger)) : Level(Zone.LeftGrip);
            int rightLevel = HasTriggers ? Math.Max(Level(Zone.RightGrip), Level(Zone.RightTrigger)) : Level(Zone.RightGrip);
            bool grips = await ControllerRumble.Test(leftLevel, rightLevel, 1000);
            Readback.Text = grips
                ? (HasTriggers
                    ? "2.4G/USB test sent (linked channels) · Press M + BACK on the controller if LT/RT triggers are off, or use Bluetooth mode for independent triggers."
                    : "Grip test sent.")
                : "No controller found.";
        }
        finally
        {
            Visualizer.ClearLiveMotors();
            SetDockBars(HapticsFrame.Zero);
            SetTesting(Test, false);
        }
    }

    void SetTesting(Button active, bool running)
    {
        testing = running;
        UniversalHapticsService.SetManualMotorOverride(running || patternPlayer.IsPlaying);
        active.Tag = running ? "Testing" : null;
        Test.IsEnabled = !running;
        foreach (var item in zones.Values) item.Face.IsEnabled = !running;
    }

    void ShowTriggerState()
    {
        if (!HasTriggers)
        {
            dockStatusText.Text = ReviewSandbox.Active ? "Sandbox preview" : "Grips ready (XInput)";
            return;
        }
        var shared = Diagnostics.BrowserTriggerRumble.Shared;
        bool armed = shared?.Armed == true;
        bool independent = shared is { Armed: true, SupportsTriggerRumble: true };
        string? tip = independent
            ? null
            : armed
                ? "2.4G/USB Linked Mode: pulses linked XInput channel (press M + BACK on pad to toggle LT/RT triggers, or use Bluetooth mode for independent triggers)"
                : "Trigger rumble: press M + BACK on pad on 2.4G/USB, or press one button on the controller in Bluetooth mode for independent triggers";
        foreach (var zone in new[] { Zone.LeftTrigger, Zone.RightTrigger })
        {
            if (zones.TryGetValue(zone, out var ui))
                ui.Face.ToolTip = tip;
        }
        if (ReviewSandbox.Active)
            dockStatusText.Text = "Sandbox preview · 4-motor layout";
        else if (independent)
            dockStatusText.Text = "4-Motor Armed (Grips + Independent Triggers)";
        else if (armed)
            dockStatusText.Text = "2.4G/USB Linked · Hold M + Back on pad for triggers";
        else
            dockStatusText.Text = "Click Trigger Setup (hold M + Back on pad for triggers)";

        if (armed && Readback.Text.StartsWith("Press any button on the controller"))
        {
            Readback.Text = independent
                ? "Trigger rumble is on. Click a trigger."
                : "2.4G/USB Linked Mode detected. Click a trigger to pulse its linked channel (press M + BACK on pad if trigger vibration is off, or use Bluetooth mode for independent triggers).";
        }
    }

    // Game vibration (PC mode): each motor's share of the game's rumble, a minimum for faint effects, and a live readout
    readonly List<(Slider Slider, TextBlock Label, Func<int> Get, string Unit)> motors = new();
    readonly DispatcherTimer live = new() { Interval = TimeSpan.FromMilliseconds(100) };
    public TextBlock LiveText { get; } = new() { FontSize = 14, Foreground = DS.Brush("DS.TextSoft"), TextWrapping = TextWrapping.Wrap, Margin = new(0, 4, 0, 8) };
    readonly Border[] liveBars = new Border[4];
    public IReadOnlyList<Slider> MotorSliders => motors.Select(m => m.Slider).ToList();

    UIElement BuildGame()
    {
        var panel = new StackPanel { Margin = new(0, 14, 0, 0) };
        panel.Children.Add(Kit.Section("GAME VIBRATION", 0));
        panel.Children.Add(Copy("What the game asks for, and what your controller gets.", 13));
        string[] names = ["Left motor · game", "Left motor · controller", "Right motor · game", "Right motor · controller"];
        for (int i = 0; i < 4; i++)
        {
            var row = new Grid { Margin = new(0, 3, 0, 3) };
            row.ColumnDefinitions.Add(new() { Width = new(170) });
            row.ColumnDefinitions.Add(new());
            row.Children.Add(new TextBlock { Text = names[i], FontSize = 13, Foreground = DS.Brush("DS.TextSoft"), VerticalAlignment = VerticalAlignment.Center });
            var track = new Border { Height = 10, CornerRadius = new(5), Background = DS.Brush("Control.ButtonFill"), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(track, 1);
            var bar = new Border { Height = 10, CornerRadius = new(5), Width = 0, HorizontalAlignment = HorizontalAlignment.Left, Background = i % 2 == 0 ? DS.Brush("DS.AccentHi") : DS.Brush("DS.Accent") };
            track.Child = bar;
            liveBars[i] = bar;
            row.Children.Add(track);
            panel.Children.Add(row);
        }
        panel.Children.Add(LiveText);
        Motor(panel, "Minimum for faint effects", 0, X20Ctl.Zone.PcMode.PcModeProfile.MaxVibrationFloor, "%", () => Draft.Floor, v => Draft.SetMotors(Draft.Left, Draft.Right, v));
        panel.Children.Add(Copy("Above 100% weak effects get stronger; the strongest stay at full power.", 12.5));
        live.Tick += (_, _) => ShowLive();
        ShowLive();
        return panel;
    }

    void Motor(Panel panel, string name, int min, int max, string unit, Func<int> get, Action<int> set)
    {
        var head = new Grid { Margin = new(0, 10, 0, 0) };
        head.ColumnDefinitions.Add(new());
        head.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        head.Children.Add(new TextBlock { Text = name, FontSize = 13.5, Foreground = DS.Brush("DS.TextSoft") });
        var label = new TextBlock { FontSize = 13.5, FontWeight = FontWeights.SemiBold, Foreground = DS.Brush("DS.Text") };
        Grid.SetColumn(label, 1);
        head.Children.Add(label);
        panel.Children.Add(head);
        var slider = new Slider { Minimum = min, Maximum = max, TickFrequency = 1, IsSnapToTickEnabled = true, Style = DS.Style("DS.Slider"), Margin = new(0, 6, 0, 0), Value = get() };
        slider.ValueChanged += (_, _) => { if (updating) return; set((int)slider.Value); label.Text = get() + unit; Changed?.Invoke(); };
        label.Text = get() + unit;
        motors.Add((slider, label, get, unit));
        panel.Children.Add(slider);
    }

    /// <summary>Shows the newest game vibration; after two quiet seconds it says so.</summary>
    public void ShowLive()
    {
        var s = GameRumble.Latest;
        bool fresh = s != null && Environment.TickCount64 - s.At < 2000 && (s.GameLeft > 0 || s.GameRight > 0);
        int[] percent = fresh ? new[] { s!.GameLeft * 100 / 255, s.PadLeft * 100 / 65535, s.GameRight * 100 / 255, s.PadRight * 100 / 65535 } : new int[4];
        for (int i = 0; i < 4; i++)
            if (liveBars[i]?.Parent is Border track)
                liveBars[i].Width = Math.Max(0, track.ActualWidth) * percent[i] / 100;
        LiveText.Text = fresh ? $"Game {percent[0]}% / {percent[2]}% → controller {percent[1]}% / {percent[3]}%" : "Play a game with PC mode on and its vibration shows here.";
    }

    static TextBlock Copy(string text, double size) => new() { Text = text, FontSize = size, Foreground = DS.Brush("DS.TextSoft"), TextWrapping = TextWrapping.Wrap, Margin = new(0, 6, 0, 6) };

    public void Set(int percent)
    {
        previewStrength = Math.Clamp(percent, 0, Draft.Max);
        if (Draft.CanEdit) Draft.Set(previewStrength);
        Refresh();
        Changed?.Invoke();
    }

    public void Reset()
    {
        if (Draft.CanEdit) Draft.Reset();
        previewStrength = Draft.Strength;
        Refresh();
        Changed?.Invoke();
    }

    public void Refresh()
    {
        updating = true;
        if (Draft.CanEdit) previewStrength = Draft.Strength;
        value.Text = previewStrength + "%";
        Strength.Value = previewStrength;
        Visualizer.SetStrength(Math.Min(100, previewStrength));
        foreach (var (slider, label, get, unit) in motors)
        {
            slider.Value = get();
            label.Text = get() + unit;
        }
        if (Draft.PcMode)
        {
            if (ZoneSlider(Zone.LeftGrip) is { } l) l.Value = Draft.Left;
            if (ZoneSlider(Zone.RightGrip) is { } r) r.Value = Draft.Right;
        }
        foreach (var b in presets)
            LightingWindow.SetSelectedState(b, (int)b.Tag == previewStrength);
        updating = false;
    }

    public void FocusPreset() => (presets.OrderBy(b => Math.Abs((int)b.Tag - previewStrength)).FirstOrDefault() ?? ZoneButton(Zone.LeftGrip))?.Focus();

    public bool HandleKey(KeyEventArgs e)
    {
        if (Keyboard.FocusedElement != Strength || e.Key is not (Key.Left or Key.Right)) return false;
        Set(previewStrength + (e.Key == Key.Right ? 1 : -1) * ((Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? 5 : 1));
        e.Handled = true;
        return true;
    }
}
