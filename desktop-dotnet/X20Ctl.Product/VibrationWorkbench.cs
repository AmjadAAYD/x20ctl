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
/// Reworked Studio Vibration workspace:
/// Left 1.4* column renders the controller silhouette with live 4-motor edge lighting and real-time output bars;
/// Right 1* column switches cleanly between Motors &amp; Test, Custom Pattern Lab, and Per-Motor Audio Roles.
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
    readonly ScrollViewer inspectorScroll = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new(0, 0, 4, 0) };
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

    // Right-column Studio section navigation
    readonly Button[] sectionButtons = new Button[3];
    readonly Border tabHost = new();
    readonly FrameworkElement motorsView;
    readonly FrameworkElement patternsView;
    readonly FrameworkElement audioRolesView;
    int activeSection;

    // Left-column compact 4-motor live telemetry dock
    readonly (ColumnDefinition Fill, ColumnDefinition Empty, string Label)[] dockMeters = new (ColumnDefinition, ColumnDefinition, string)[4];
    readonly TextBlock dockStatusText = Kit.Text("Ready", 12, "DS.TextSoft").Also(t => t.TextWrapping = TextWrapping.Wrap);
    readonly Button? dockTriggerSetupBtn;

    // Tab 1: Custom Vibration Pattern Lab
    readonly VibrationPatternStore patternStore = new(VibrationPatternStore.DefaultPath);
    readonly VibrationPatternPlayer patternPlayer = new();
    readonly System.Windows.Controls.Primitives.UniformGrid patternLibraryGrid = new() { Columns = 2, Margin = new(-3, 0, -3, 10) };
    readonly Button[,] matrixButtons = new Button[4, VibrationPattern.DefaultSteps];
    readonly Border[,] matrixFillBars = new Border[4, VibrationPattern.DefaultSteps];
    readonly Border[] stepColumnHeaders = new Border[VibrationPattern.DefaultSteps];
    readonly TextBlock[] stepHeaderTexts = new TextBlock[VibrationPattern.DefaultSteps];
    readonly Dictionary<string, Border> patternCardBorders = new();
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

    // Tab 2: Per-Motor Audio Roles & 8-Band Spectrum
    readonly (RowDefinition Empty, RowDefinition Fill)[] roleSpectrumBars = new (RowDefinition, RowDefinition)[8];
    ColumnDefinition? roleVocalFill, roleVocalEmpty;
    Border? roleBeatBadge;
    TextBlock? roleVibeText;
    readonly (TextBlock Label, TextBlock Desc, ColumnDefinition Fill, ColumnDefinition Empty)[] roleCardUi = new (TextBlock, TextBlock, ColumnDefinition, ColumnDefinition)[4];
    readonly Dictionary<int, Button> audioHapStrengthBtns = new();
    Button? toggleAudioHapBtn;
    readonly DispatcherTimer studioTimer = new() { Interval = TimeSpan.FromMilliseconds(60) };

    static readonly Color[] MotorColors =
    [
        Color.FromRgb(0xFF, 0x3B, 0x7F), // Left Grip
        Color.FromRgb(0x38, 0xBD, 0xF8), // Right Grip
        Color.FromRgb(0xFB, 0x92, 0x3C), // Left Trigger
        Color.FromRgb(0xA7, 0x8B, 0xFA), // Right Trigger
    ];

    public VibrationWorkbench(X20Ctl.Zone.VibrationDraft draft, FrameworkElement resources, Action<Button> register, bool animations, string model = "x20")
    {
        Draft = draft;
        previewStrength = draft.Strength;
        HasTriggers = ModelProfiles.For(model).Motors.Contains("triggers");
        try { patternStore.Load(); } catch { }

        Body.ColumnDefinitions.Add(new() { Width = new(1.4, GridUnitType.Star) });
        Body.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });

        Visualizer = new((Geometry)resources.FindResource("ControllerOutline"), animations, model);

        Button MakeButton(string text, Action action)
        {
            var b = new Button
            {
                Content = text,
                Style = DS.Style("ActionButton"),
                Height = 40,
                MinHeight = 36,
                Padding = new(12, 0, 12, 0),
                Margin = new(0, 4, 6, 4),
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            b.Click += (_, _) => action();
            register(b);
            return b;
        }

        Test = MakeButton(HasTriggers ? "Test all four" : "Test both grips", () => _ = RunAll());
        Test.Style = DS.Style("DS.MotorTile");
        Test.IsEnabled = !ReviewSandbox.Active;
        Test.Height = 48;
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

        // Build all 3 inspector tabs
        motorsView = BuildMotorsTab(MakeButton);
        patternsView = BuildPatternsTab(MakeButton);
        audioRolesView = BuildAudioRolesTab(MakeButton);

        var sectionPanel = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var (index, label) in new[] { (0, "Motors"), (1, "Patterns"), (2, "Audio Roles") })
        {
            int idx = index;
            var sb = new Button { Content = label, MinWidth = 86 };
            sb.Click += (_, _) => SelectSection(idx);
            register(sb);
            sectionButtons[idx] = sb;
            sectionPanel.Children.Add(sb);
        }
        var header = new StackPanel { Margin = new(0, 0, 0, 12) };
        header.Children.Add(Kit.Segments(sectionPanel));

        var rightDock = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        rightDock.Children.Add(header);
        inspectorScroll.Content = tabHost;
        rightDock.Children.Add(inspectorScroll);

        var controlPanel = Kit.Panel(null, null, null, rightDock);
        Grid.SetColumn(controlPanel, 1);
        Body.Children.Add(controlPanel);

        Shelf.Children.Add(Copy("Test motors, sculpt custom 4-motor wave patterns, or assign per-motor audio roles", 13.5));

        Strength.ValueChanged += (_, _) => { if (!updating) Set((int)Strength.Value); };

        patternPlayer.FrameUpdated += (hf, step, playing) =>
        {
            Body.Dispatcher.BeginInvoke(() => OnPatternFrame(hf, step, playing));
        };
        studioTimer.Tick += (_, _) => RefreshLiveStudioTelemetry();

        void OnLightingChanged() => Body.Dispatcher.BeginInvoke(SyncRoleAndAudioUi);
        void OnTriggerArmChanged() => Body.Dispatcher.BeginInvoke(ShowTriggerState);

        Body.Loaded += (_, _) =>
        {
            LightingEffectsService.Changed -= OnLightingChanged;
            LightingEffectsService.Changed += OnLightingChanged;
            if (HasTriggers && Diagnostics.BrowserTriggerRumble.Shared is { } shared)
            {
                shared.ArmedChanged -= OnTriggerArmChanged;
                shared.ArmedChanged += OnTriggerArmChanged;
                shared.StateChanged -= OnTriggerArmChanged;
                shared.StateChanged += OnTriggerArmChanged;
                _ = shared.Ready();
            }
            SyncRoleAndAudioUi();
            ShowTriggerState();
            SyncStudioTimer();
            if (Draft.PcMode && !ReviewSandbox.Active) live.Start();
        };

        Body.Unloaded += (_, _) =>
        {
            studioTimer.Stop();
            live.Stop();
            StopPattern();
            LightingEffectsService.Changed -= OnLightingChanged;
            if (HasTriggers && Diagnostics.BrowserTriggerRumble.Shared is { } shared)
            {
                shared.ArmedChanged -= OnTriggerArmChanged;
                shared.StateChanged -= OnTriggerArmChanged;
            }
        };

        SelectSection(0);
        Refresh();
        SelectRegion("All");
        LoadPatternIntoComposer(VibrationPatternEngine.BuiltIns[0]);
        SyncRoleAndAudioUi();
        ShowTriggerState();
    }

    public void SelectSection(int index)
    {
        activeSection = Math.Clamp(index, 0, 2);
        tabHost.Child = activeSection switch
        {
            1 => patternsView,
            2 => audioRolesView,
            _ => motorsView,
        };
        for (int i = 0; i < sectionButtons.Length; i++)
            LightingWindow.SetSelectedState(sectionButtons[i], i == activeSection);
        SyncStudioTimer();
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
            var barGrid = new Grid { Height = 7 };
            var cFill = new ColumnDefinition { Width = new(0.04, GridUnitType.Star) };
            var cEmpty = new ColumnDefinition { Width = new(0.96, GridUnitType.Star) };
            barGrid.ColumnDefinitions.Add(cFill);
            barGrid.ColumnDefinitions.Add(cEmpty);
            barGrid.Children.Add(new Border { Background = new SolidColorBrush(defs[i].Col), CornerRadius = new(3) });
            sp.Children.Add(new Border
            {
                CornerRadius = new(3),
                Background = new SolidColorBrush(Color.FromArgb(90, 24, 34, 54)),
                BorderBrush = DS.Brush("DS.Hairline"),
                BorderThickness = new(0.5),
                Child = barGrid,
            });
            dockMeters[i] = (cFill, cEmpty, defs[i].Tag);
            grid.Children.Add(sp);
        }
        stack.Children.Add(grid);
        box.Child = stack;
        return box;
    }

    FrameworkElement BuildMotorsTab(Func<string, Action, Button> makeButton)
    {
        var controls = new StackPanel { Margin = new(0, 2, 0, 8) };
        controls.Children.Add(Kit.Section("MOTORS & TEST", 0));
        controls.Children.Add(Copy("Select a motor tile to feel it, adjust individual strength, or configure PC game rumble.", 13));
        controls.Children.Add(BuildZones(makeButton));
        controls.Children.Add(Test);

        Readback.Margin = new(0, 2, 0, 6);
        controls.Children.Add(Readback);

        Strength.Maximum = Draft.Max;
        Strength.Style = DS.Style("DS.Slider");
        Strength.Margin = new(0, 4, 0, 10);

        if (Draft.PcMode)
        {
            controls.Children.Add(Kit.Section("GAME RUMBLE BOOST (PC MODE)", 6));
            controls.Children.Add(value);
            controls.Children.Add(Strength);
            var choices = new WrapPanel { Margin = new(0, 0, 0, 4) };
            foreach (int percent in new[] { 0, 50, 100, 150, 200 })
            {
                int p = percent;
                var b = makeButton(percent == 0 ? "Off" : percent + "%", () => Set(p));
                b.Tag = percent;
                presets.Add(b);
                choices.Children.Add(b);
            }
            controls.Children.Add(Kit.Segments(choices));
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

    UIElement BuildZones(Func<string, Action, Button> button)
    {
        const double tileSize = 136;
        var grid = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2, Margin = new(0, 2, 0, 4) };
        foreach (var (zone, name) in zoneNames.Where(n => HasTriggers || !IsTrigger(n.Zone)))
        {
            var z = zone;
            bool right = z is Zone.RightTrigger or Zone.RightGrip;
            var pair = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new(3, 0, 3, 10) };

            var face = button(name, () => _ = RunZone(z));
            face.Style = DS.Style("DS.MotorTile");
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

    FrameworkElement BuildPatternsTab(Func<string, Action, Button> makeButton)
    {
        var stack = new StackPanel { Margin = new(0, 2, 0, 12) };
        stack.Children.Add(Kit.Section("SIGNATURE PATTERNS", 0));
        stack.Children.Add(Copy("Choose a unique haptic waveform or sculpt your own 8-step 4-motor beat below.", 12.5));

        RefreshPatternLibraryCards();
        stack.Children.Add(patternLibraryGrid);

        stack.Children.Add(Kit.Section("CUSTOM PATTERN COMPOSER", 6));
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

        // 8-Step x 4-Motor interactive tactile matrix
        int rowCount = HasTriggers ? 4 : 2;
        var matrixGrid = new Grid { Margin = new(0, 0, 0, 10) };
        matrixGrid.ColumnDefinitions.Add(new() { Width = new(56) });
        for (int c = 0; c < VibrationPattern.DefaultSteps; c++)
            matrixGrid.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });

        // Step index row
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
                var cellGrid = new Grid();
                var emptyTrack = new Border
                {
                    CornerRadius = new(4),
                    Background = new SolidColorBrush(Color.FromArgb(85, 22, 32, 52)),
                };
                var fillBar = new Border
                {
                    CornerRadius = new(3),
                    Height = 0,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Background = new SolidColorBrush(MotorColors[r]),
                };
                cellGrid.Children.Add(emptyTrack);
                cellGrid.Children.Add(fillBar);

                var cellBtn = new Button
                {
                    Height = 28,
                    MinHeight = 28,
                    Padding = new(0),
                    Margin = new(2, 0, 2, 4),
                    BorderThickness = new(1),
                    BorderBrush = DS.Brush("DS.Hairline"),
                    Background = Brushes.Transparent,
                    Content = cellGrid,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    VerticalContentAlignment = VerticalAlignment.Stretch,
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

        var optionsRow = new WrapPanel { Margin = new(0, 0, 0, 8) };
        patternLoopBtn = makeButton("Loop: On", () =>
        {
            composerPattern = composerPattern with { Loop = !composerPattern.Loop };
            UpdatePatternOptionButtons();
            if (patternPlayer.IsPlaying && !ReviewSandbox.Active)
                patternPlayer.Play(composerPattern, (int)Math.Round(patternStrengthSlider.Value));
        });
        patternLoopBtn.Height = 32; patternLoopBtn.MinHeight = 30; patternLoopBtn.FontSize = 12;

        patternRampBtn = makeButton("Shape: Crisp", () =>
        {
            composerPattern = composerPattern with { SmoothRamp = !composerPattern.SmoothRamp };
            UpdatePatternOptionButtons();
            if (patternPlayer.IsPlaying && !ReviewSandbox.Active)
                patternPlayer.Play(composerPattern, (int)Math.Round(patternStrengthSlider.Value));
        });
        patternRampBtn.Height = 32; patternRampBtn.MinHeight = 30; patternRampBtn.FontSize = 12;

        optionsRow.Children.Add(patternLoopBtn);
        optionsRow.Children.Add(patternRampBtn);
        body.Children.Add(optionsRow);

        var transportRow = new WrapPanel { Margin = new(0, 2, 0, 2) };
        patternPlayBtn = makeButton("Play Pattern", TogglePlayComposerPattern);
        patternPlayBtn.Style = DS.Style("DS.Primary");
        patternPlayBtn.Height = 38;
        patternPlayBtn.MinWidth = 124;
        patternPlayBtn.IsEnabled = !ReviewSandbox.Active;
        transportRow.Children.Add(patternPlayBtn);

        var randBtn = makeButton("Randomize", RandomizeComposerPattern);
        randBtn.Height = 38;
        var clearBtn = makeButton("Clear", ClearComposerPattern);
        clearBtn.Height = 38;
        var saveBtn = makeButton("+ Save Pattern", SaveComposerPatternWithPrompt);
        saveBtn.Height = 38;
        transportRow.Children.Add(randBtn);
        transportRow.Children.Add(clearBtn);
        transportRow.Children.Add(saveBtn);
        body.Children.Add(transportRow);

        composerBox.Child = body;
        stack.Children.Add(composerBox);
        return stack;
    }

    FrameworkElement BuildAudioRolesTab(Func<string, Action, Button> makeButton)
    {
        var stack = new StackPanel { Margin = new(0, 2, 0, 12) };

        var head = new DockPanel { Margin = new(0, 0, 0, 8) };
        roleBeatBadge = new Border
        {
            CornerRadius = ThemeCatalog.Round(4),
            Padding = new(7, 2, 7, 2),
            Background = new SolidColorBrush(Color.FromArgb(40, 0, 229, 255)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(110, 0, 229, 255)),
            BorderThickness = new(1),
            Child = Kit.Text("BEAT", 10.5, "DS.Text", FontWeights.Bold),
        };
        DockPanel.SetDock(roleBeatBadge, Dock.Right);
        head.Children.Add(roleBeatBadge);

        roleVibeText = Kit.Text("IDLE", 10.5, "DS.AccentHi", FontWeights.Bold);
        var vibeBadge = new Border
        {
            CornerRadius = ThemeCatalog.Round(4),
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

        // 8-band real-time FFT bar chart
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
                CornerRadius = new(4),
                Background = new SolidColorBrush(Color.FromArgb(90, 24, 34, 54)),
                BorderBrush = DS.Brush("DS.Hairline"),
                BorderThickness = new(0.5),
            };
            var vGrid = new Grid();
            var rEmpty = new RowDefinition { Height = new(0.92, GridUnitType.Star) };
            var rFill = new RowDefinition { Height = new(0.08, GridUnitType.Star) };
            vGrid.RowDefinitions.Add(rEmpty);
            vGrid.RowDefinitions.Add(rFill);
            var fillBar = new Border { Background = new SolidColorBrush(cols[b]), CornerRadius = new(3) };
            Grid.SetRow(fillBar, 1);
            vGrid.Children.Add(fillBar);
            barBox.Child = vGrid;
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
            roleSpectrumBars[b] = (rEmpty, rFill);
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
        var vTrackGrid = new Grid { Height = 8 };
        roleVocalFill = new ColumnDefinition { Width = new(0.04, GridUnitType.Star) };
        roleVocalEmpty = new ColumnDefinition { Width = new(0.96, GridUnitType.Star) };
        vTrackGrid.ColumnDefinitions.Add(roleVocalFill);
        vTrackGrid.ColumnDefinitions.Add(roleVocalEmpty);
        vTrackGrid.Children.Add(new Border { Background = new SolidColorBrush(Color.FromRgb(0xFB, 0xBF, 0x24)), CornerRadius = new(3) });
        var vTrack = new Border
        {
            CornerRadius = new(3),
            Background = new SolidColorBrush(Color.FromArgb(90, 24, 34, 54)),
            BorderBrush = DS.Brush("DS.Hairline"),
            BorderThickness = new(0.5),
            Child = vTrackGrid,
        };
        Grid.SetColumn(vTrack, 1);
        vocalRow.Children.Add(vTrack);
        specBody.Children.Add(vocalRow);
        specCard.Child = specBody;
        stack.Children.Add(specCard);

        stack.Children.Add(Kit.Section("PER-MOTOR AUDIO ROLES", 2));
        stack.Children.Add(Copy("Give each grip and trigger its own dedicated audio element.", 12.5));

        // 1-click quick role templates
        var quickRow = new WrapPanel { Margin = new(0, 0, 0, 8) };
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
            var pb = makeButton(presetName, () => SetMotorRoles(captured));
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
            var prevBtn = makeButton("<", () => CycleSingleMotorRole(idx, -1));
            prevBtn.Height = 28; prevBtn.MinHeight = 26; prevBtn.Padding = new(9, 2, 9, 2); prevBtn.Margin = new(0, 0, 4, 0);
            var roleVal = Kit.Text("Auto (Vibe)", 12.5, "DS.AccentHi", FontWeights.SemiBold).Also(t =>
            {
                t.VerticalAlignment = VerticalAlignment.Center;
                t.MinWidth = 112;
                t.TextAlignment = TextAlignment.Center;
            });
            var nextBtn = makeButton(">", () => CycleSingleMotorRole(idx, 1));
            nextBtn.Height = 28; nextBtn.MinHeight = 26; nextBtn.Padding = new(9, 2, 9, 2); nextBtn.Margin = new(4, 0, 0, 0);
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

            var mGrid = new Grid { Height = 6, VerticalAlignment = VerticalAlignment.Center };
            var mFill = new ColumnDefinition { Width = new(0.04, GridUnitType.Star) };
            var mEmpty = new ColumnDefinition { Width = new(0.96, GridUnitType.Star) };
            mGrid.ColumnDefinitions.Add(mFill);
            mGrid.ColumnDefinitions.Add(mEmpty);
            mGrid.Children.Add(new Border { Background = new SolidColorBrush(motorDefs[idx].Col), CornerRadius = new(3) });
            var mTrack = new Border
            {
                CornerRadius = new(3),
                Background = new SolidColorBrush(Color.FromArgb(90, 24, 34, 54)),
                Child = mGrid,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(mTrack, 1);
            subGrid.Children.Add(mTrack);

            Grid.SetRow(subGrid, 1);
            cardGrid.Children.Add(subGrid);
            card.Child = cardGrid;
            stack.Children.Add(card);
            roleCardUi[idx] = (roleVal, descText, mFill, mEmpty);
        }

        // Quick Audio Haptics power selector right inside Audio Roles tab
        stack.Children.Add(Kit.Section("AUDIO HAPTICS OUTPUT", 4));
        var hapChoicesWrap = new WrapPanel { Margin = new(0, 0, 0, 8) };
        foreach (var (pct, label) in new (int, string)[] { (0, "Off"), (35, "Light 35%"), (65, "Punch 65%"), (100, "Max 100%") })
        {
            int captured = pct;
            var hb = makeButton(label, async () => await SetAudioHapticsStrengthAsync(captured));
            hb.Height = 32; hb.MinHeight = 30; hb.FontSize = 11.5; hb.Margin = new(0);
            audioHapStrengthBtns[captured] = hb;
            hapChoicesWrap.Children.Add(hb);
        }
        stack.Children.Add(Kit.Segments(hapChoicesWrap));

        var hapActionRow = new WrapPanel { Margin = new(0, 8, 0, 0) };
        toggleAudioHapBtn = makeButton("Start Live Audio Haptics", async () =>
        {
            if (LightingEffectsService.Status.IsRunning && LightingEffectsService.CurrentSpec.Kind == LightingEffectKind.AudioReactive)
                await LightingEffectsService.StopAsync(restoreBaseline: true);
            else
                await StartAudioHapticsWithCurrentRolesAsync();
        });
        toggleAudioHapBtn.Style = DS.Style("DS.Primary");
        toggleAudioHapBtn.Height = 36;
        toggleAudioHapBtn.IsEnabled = !ReviewSandbox.Active;
        hapActionRow.Children.Add(toggleAudioHapBtn);

        if (HasTriggers)
        {
            var setupBtn = makeButton("Trigger Setup (M + Back)", OpenTriggerGuide);
            setupBtn.Height = 36;
            hapActionRow.Children.Add(setupBtn);
        }
        stack.Children.Add(hapActionRow);

        return stack;
    }

    void RefreshPatternLibraryCards()
    {
        patternLibraryGrid.Children.Clear();
        patternCardBorders.Clear();

        IEnumerable<VibrationPattern> all = VibrationPatternEngine.BuiltIns.Concat(patternStore.Patterns);
        foreach (var pat in all)
        {
            var captured = pat;
            var title = Kit.Text(pat.Name, 12.5, "DS.Text", FontWeights.SemiBold).Also(t => t.TextTrimming = TextTrimming.CharacterEllipsis);
            var sub = Kit.Text($"{pat.Category} · {pat.StepMs} ms{(pat.Loop ? " · Loop" : "")}", 10.5, "DS.TextMuted");
            var sp = new StackPanel { Children = { title, sub } };

            var cardGrid = new Grid();
            cardGrid.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
            cardGrid.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
            cardGrid.Children.Add(sp);

            var faceBtn = new Button
            {
                Style = DS.Style("ActionButton"),
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Padding = new(10, 7, 10, 7),
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
                    Style = DS.Style("ActionButton"),
                    FontSize = 12,
                    Padding = new(5, 0, 5, 0),
                    MinHeight = 20,
                    Height = 20,
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
            if (faceBtn.BorderBrush != null) { }
            LightingWindow.SetSelectedState(faceBtn, composerPattern.Id == pat.Id);
            patternCardBorders[pat.Id] = new Border { Tag = faceBtn };
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
        foreach (var (id, holder) in patternCardBorders)
        {
            if (holder.Tag is Button btn)
                LightingWindow.SetSelectedState(btn, id == composerPattern.Id);
        }
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
                stepColumnHeaders[s].Background = activeCol
                    ? new SolidColorBrush(Color.FromArgb(140, 0, 229, 255))
                    : Brushes.Transparent;
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
                    bar.Opacity = val <= 0 ? 0.0 : (0.45 + 0.55 * val / 100.0);
                }
                if (matrixButtons[r, s] is { } btn)
                {
                    btn.BorderBrush = s == activePlayheadStep
                        ? DS.Brush("DS.AccentHi")
                        : (val > 0 ? new SolidColorBrush(Color.FromArgb(140, MotorColors[r].R, MotorColors[r].G, MotorColors[r].B)) : DS.Brush("DS.Hairline"));
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

        // If live Audio Haptics was actively driving rumble, pause its rumble stream cleanly so pattern doesn't fight it.
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
        // Guarantee at least one vibrant signature step so Randomize never yields all zeros.
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

        // Update tile badges in Motors tab
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

        // Update Audio Roles tab cards
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
        bool needTimer = Body.IsLoaded && (activeSection == 2 || (LightingEffectsService.Status.IsRunning && LightingEffectsService.CurrentSpec.Kind == LightingEffectKind.AudioReactive));
        if (needTimer)
        {
            if (activeSection == 2 && !ReviewSandbox.Active)
                SystemAudioLevelSampler.EnsureStarted();
            if (!studioTimer.IsEnabled) studioTimer.Start();
        }
        else if (studioTimer.IsEnabled)
        {
            studioTimer.Stop();
        }
    }

    void RefreshLiveStudioTelemetry()
    {
        AudioFrame af;
        HapticsFrame hf;
        var spec = LightingEffectsService.CurrentSpec;

        if (ReviewSandbox.Active)
        {
            double t = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
            float beatCycle = (float)((t * 2.0) % 1.0);
            float pulse = MathF.Exp(-beatCycle * 5.0f);
            float b = Math.Clamp(0.20f + 0.75f * pulse, 0.03f, 1.0f);
            float m = Math.Clamp(0.25f + 0.55f * (0.5f + 0.5f * MathF.Sin((float)t * 3.1f)), 0.03f, 1.0f);
            float tr = Math.Clamp(0.15f + 0.65f * (0.5f + 0.5f * MathF.Sin((float)t * 7.4f)), 0.03f, 1.0f);
            float voc = Math.Clamp(0.45f + 0.40f * MathF.Sin((float)t * 2.1f), 0.0f, 1.0f);
            var s8 = new AudioSpectrum8(b, Math.Clamp(b * 0.85f, 0.04f, 1f), Math.Clamp(m * 0.82f, 0.04f, 1f), voc, Math.Clamp(voc * 0.88f, 0.04f, 1f), Math.Clamp(tr * 0.8f, 0.04f, 1f), tr, Math.Clamp(tr * 0.65f, 0.04f, 1f));
            af = new AudioFrame(Math.Max(b, m), b, m, tr, pulse, (int)(t * 2.0), 0f, b, tr, 0.5f, 0.65f, 0.4f, voc, voc, voc * 0.85f, m, Math.Max(pulse, b * 0.7f), Math.Max(b, m), AudioMusicVibe.GroovePop, s8);
            hf = AudioHapticsEngine.Evaluate(af, spec.AudioHapticsStyle, Math.Max(65, spec.AudioHapticsStrength), spec.AudioMotorRoles, t);
        }
        else
        {
            af = activeSection == 2 ? SystemAudioLevelSampler.SampleAudioFrame() : SystemAudioLevelSampler.LatestFrame;
            hf = LightingEffectsService.Status.IsRunning && spec.Kind == LightingEffectKind.AudioReactive && spec.AudioHapticsStrength > 0
                ? SystemAudioLevelSampler.LatestHaptics
                : AudioHapticsEngine.Evaluate(af, spec.AudioHapticsStyle, Math.Max(spec.AudioHapticsStrength, activeSection == 2 ? 75 : 0), spec.AudioMotorRoles, 0.0);
        }

        for (int i = 0; i < 8; i++)
        {
            if (roleSpectrumBars[i].Fill == null || roleSpectrumBars[i].Empty == null) continue;
            double v = Math.Clamp(af.Spectrum[i], 0.04, 0.98);
            roleSpectrumBars[i].Empty.Height = new(1.0 - v, GridUnitType.Star);
            roleSpectrumBars[i].Fill.Height = new(v, GridUnitType.Star);
        }
        if (roleVocalFill != null && roleVocalEmpty != null)
        {
            double v = Math.Clamp(af.VocalPresence, 0.04, 0.98);
            roleVocalFill.Width = new(v, GridUnitType.Star);
            roleVocalEmpty.Width = new(1.0 - v, GridUnitType.Star);
        }
        if (roleBeatBadge != null)
        {
            byte alpha = (byte)(af.BeatPulse > 0.30f ? 220 : 40);
            roleBeatBadge.Background = new SolidColorBrush(Color.FromArgb(alpha, 0, 229, 255));
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

        double[] mVals = [hf.LeftGrip, hf.RightGrip, hf.LeftTrigger, hf.RightTrigger];
        for (int i = 0; i < (HasTriggers ? 4 : 2); i++)
        {
            if (roleCardUi[i].Fill == null || roleCardUi[i].Empty == null) continue;
            double mv = Math.Clamp(mVals[i], 0.04, 0.98);
            roleCardUi[i].Fill.Width = new(mv, GridUnitType.Star);
            roleCardUi[i].Empty.Width = new(1.0 - mv, GridUnitType.Star);
        }

        if (!patternPlayer.IsPlaying && !testing && !ReviewSandbox.Active)
        {
            if (LightingEffectsService.Status.IsRunning && spec.Kind == LightingEffectKind.AudioReactive && spec.AudioHapticsStrength > 0)
            {
                SetDockBars(hf);
                dockStatusText.Text = SystemAudioLevelSampler.HapticsPathSummary;
                Visualizer.SetLiveMotors(hf.LeftGrip, hf.RightGrip, hf.LeftTrigger, hf.RightTrigger);
            }
            else if (activeSection == 2)
            {
                SetDockBars(hf);
            }
        }
    }

    void SetDockBars(HapticsFrame hf)
    {
        double[] vals = [hf.LeftGrip, hf.RightGrip, hf.LeftTrigger, hf.RightTrigger];
        int count = HasTriggers ? 4 : 2;
        for (int i = 0; i < count; i++)
        {
            if (dockMeters[i].Fill == null || dockMeters[i].Empty == null) continue;
            double v = Math.Clamp(vals[i], 0.04, 0.98);
            dockMeters[i].Fill.Width = new(v, GridUnitType.Star);
            dockMeters[i].Empty.Width = new(1.0 - v, GridUnitType.Star);
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
        var panel = new StackPanel { Margin = new(0, 18, 0, 0) };
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
