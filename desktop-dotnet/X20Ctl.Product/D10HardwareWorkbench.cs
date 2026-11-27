using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using X20Ctl.Zone;
using X20Ctl.Zone.D10;

namespace X20Ctl.Product;

/// <summary>
/// Dedicated EasySMX D10 native workbench:
/// 6-axis IMU motion steering / aiming / gyro mouse with DSP filters,
/// M+D-pad quick profiles and game auto-switching, independent M1/M2/M paddle/key/macro routing,
/// direct EP02 dual motor intensity and pulse testing, resting + 15-pose 3-axis calibration,
/// and Direct USB (WinUSB) driver switching/recovery.
/// </summary>
public sealed class D10HardwareWorkbench
{
    public Grid Body { get; } = new();

    private readonly ButtonDraft buttons;
    private readonly CurveDraft curves;
    private readonly MacroDraft macros;
    private readonly VibrationDraft vibration;
    private readonly D10SavedConfig config;
    private readonly Action<D10SavedConfig> saveConfig;
    private readonly Func<Action<string>, Task> applyToController;
    private readonly Action<string> openMacro;
    private readonly Action onChanged;

    private D10DirectSession? session;
    private DispatcherTimer? gameWatchTimer;
    private string? lastAutoSwitchedExe;

    private readonly TextBlock statusBanner = ManagementVisuals.Copy("Connect EasySMX D10 via Direct USB (WinUSB) for 6-axis motion and independent M1/M2 paddles, or test standard vibration.", 13.5);
    private readonly Border statePill;
    private readonly TextBlock stateText = new() { FontSize = 12, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
    private readonly Button connectBtn, recenterBtn, saveApplyBtn;

    private readonly ComboBox modeBox;
    private readonly Slider fullScaleSlider, deadzoneSlider, sensSlider, tauSlider;
    private readonly TextBlock fullScaleVal, deadzoneVal, sensVal, tauVal;
    private readonly CheckBox invertBox, vibGuardBox, leadBox, trajGateBox, harmonicBox, boostBox, shortcutsBox;

    private readonly Border steerBarFill;
    private readonly TextBlock telemetryText = ManagementVisuals.Copy("Roll: 0.0 deg   |   Pitch: 0.0 deg   |   Gyro Z: 0.0 dps   |   Rate: 0 Hz", 13, true);
    private readonly Border m1Indicator, m2Indicator, mIndicator;

    private readonly ComboBox profileBox, quickSlotBox;
    private readonly Button newProfileBtn, dupProfileBtn, delProfileBtn, useLastGameBtn;
    private readonly TextBox gameExesBox;
    private readonly CheckBox autoSwitchBox;

    private readonly ComboBox m1Box, m2Box, mBox;
    private readonly Slider leftMotorSlider, rightMotorSlider;
    private readonly TextBlock leftMotorVal = Kit.Text("100%", 13, "DS.Text", FontWeights.SemiBold);
    private readonly TextBlock rightMotorVal = Kit.Text("100%", 13, "DS.Text", FontWeights.SemiBold);
    private readonly Button pulseLeftBtn, pulseRightBtn, stopMotorsBtn;

    private readonly Button calibrateBtn, multiCalBtn, resetCalBtn;
    private readonly TextBlock calStatusText = ManagementVisuals.Copy("Place controller on a flat desk for 1.5s resting cal, or run 15-pose 3-axis calibration.", 12.5);

    private readonly Button refreshDriverBtn, switchWinUsbBtn, restoreXboxBtn;
    private readonly TextBlock driverStatusText = ManagementVisuals.Copy("Probing Windows USB driver state...", 12.5);
    private D10DriverProbe? lastDriverProbe;

    private CancellationTokenSource? calCts;
    private bool calRunningMulti;
    private bool syncing;

    private D10MotionSettings ActiveSettings => config.GetActiveProfile().Settings;

    public D10HardwareWorkbench(
        FrameworkElement resources,
        Action<Button> register,
        ButtonDraft buttons,
        CurveDraft curves,
        MacroDraft macros,
        VibrationDraft vibration,
        D10SavedConfig initialConfig,
        Action<D10SavedConfig> saveConfig,
        Func<Action<string>, Task> applyToController,
        Action<string> openMacro,
        Action onChanged)
    {
        this.buttons = buttons;
        this.curves = curves;
        this.macros = macros;
        this.vibration = vibration;
        this.config = (initialConfig ?? new D10SavedConfig()).EnsureDefaults();
        this.saveConfig = saveConfig;
        this.applyToController = applyToController;
        this.openMacro = openMacro;
        this.onChanged = onChanged;

        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new(0, 0, 6, 8) };
        var root = new StackPanel();
        scroll.Content = root;
        Body.Children.Add(scroll);

        statePill = new Border
        {
            CornerRadius = ThemeCatalog.Round(6),
            Padding = new(10, 4, 10, 4),
            BorderThickness = new(1),
            BorderBrush = DS.Brush("DS.Hairline"),
            Background = new SolidColorBrush(Color.FromArgb(90, 20, 34, 60)),
            Margin = new(0, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = stateText,
        };
        UpdateConnectionBadge(D10LinkKind.Disconnected);

        connectBtn = FlatAction("Connect Direct USB", ToggleConnection, register);
        recenterBtn = FlatAction("Recenter Gyro", () =>
        {
            if (session != null)
            {
                config.Calibration = session.Recenter();
                PersistConfig();
                statusBanner.Text = $"Gyro center updated to {config.Calibration.CenterAngleDeg:0.1} deg.";
            }
            else
            {
                statusBanner.Text = "Connect Direct USB first to recenter live gyro angle.";
            }
        }, register);
        recenterBtn.Margin = new(8, 0, 8, 0);
        saveApplyBtn = FlatAction("Save & Apply", () => _ = SaveAndApplyAsync(), register, primary: true);
        connectBtn.IsEnabled = recenterBtn.IsEnabled = saveApplyBtn.IsEnabled = !ReviewSandbox.Active;

        var topDock = new DockPanel();
        var topActions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Children = { statePill, connectBtn, recenterBtn, saveApplyBtn } };
        DockPanel.SetDock(topActions, Dock.Right);
        topDock.Children.Add(topActions);
        topDock.Children.Add(new StackPanel
        {
            Children =
            {
                Kit.Text("EASYSMX D10 NATIVE & 6-AXIS MOTION", 12, "DS.AccentHi", FontWeights.SemiBold),
                statusBanner.Also(t => t.Margin = new(0, 4, 12, 0)),
            }
        });
        var bannerPanel = Kit.Panel(null, null, null, topDock, new Thickness(18, 14, 18, 14));
        bannerPanel.Margin = new(0, 0, 0, 12);
        root.Children.Add(bannerPanel);

        var cols = new Grid();
        cols.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        cols.ColumnDefinitions.Add(new() { Width = new(14) });
        cols.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        root.Children.Add(cols);

        var leftCol = new StackPanel();
        var rightCol = new StackPanel();
        Grid.SetColumn(rightCol, 2);
        cols.Children.Add(leftCol);
        cols.Children.Add(rightCol);

        // Left 1: 6-Axis Motion Tuning & DSP
        var motionBody = new StackPanel();
        var modeRow = new DockPanel { Margin = new(0, 0, 0, 10) };
        modeBox = new ComboBox
        {
            Width = 200,
            MinHeight = 32,
            ItemsSource = new[]
            {
                "Off (Standard Pad)",
                "Steer Left Stick (Racing)",
                "Aim Right Stick (Shooters)",
                "Gyro Mouse Look",
            }
        };
        modeBox.SelectionChanged += (_, _) => CommitMotionSettings();
        DockPanel.SetDock(modeBox, Dock.Right);
        modeRow.Children.Add(modeBox);
        modeRow.Children.Add(Kit.Text("Motion Output Mode", 14, "DS.Text", FontWeights.SemiBold).Also(t => t.VerticalAlignment = VerticalAlignment.Center));
        motionBody.Children.Add(modeRow);

        (fullScaleSlider, fullScaleVal) = BuildSliderRow(motionBody, "Full-Scale Tilt Angle", 15, 90, 1, 35, "deg", CommitMotionSettings);
        (deadzoneSlider, deadzoneVal) = BuildSliderRow(motionBody, "Center Deadzone", 0, 15, 0.5, 1.5, "deg", CommitMotionSettings);
        (sensSlider, sensVal) = BuildSliderRow(motionBody, "Response Sensitivity", 0.2, 3.0, 0.05, 1.0, "x", CommitMotionSettings);
        (tauSlider, tauVal) = BuildSliderRow(motionBody, "Complementary Filter Tau", 0.05, 0.80, 0.01, 0.20, "s", CommitMotionSettings);

        invertBox = MakeCheck("Invert Roll Axis");
        vibGuardBox = MakeCheck("Vibration Guard");
        leadBox = MakeCheck("Velocity Lead");
        trajGateBox = MakeCheck("Trajectory Gate");
        harmonicBox = MakeCheck("18-45 Hz Motor Filter");
        boostBox = MakeCheck("Mid-Range Boost");
        shortcutsBox = MakeCheck("M + D-Pad Shortcuts");
        foreach (var cb in new[] { invertBox, vibGuardBox, leadBox, trajGateBox, harmonicBox, boostBox, shortcutsBox })
        {
            cb.Checked += (_, _) => CommitMotionSettings();
            cb.Unchecked += (_, _) => CommitMotionSettings();
        }
        motionBody.Children.Add(new WrapPanel
        {
            Margin = new(0, 6, 0, 0),
            Children = { invertBox, vibGuardBox, leadBox, trajGateBox, harmonicBox, boostBox, shortcutsBox }
        });
        var motionPanel = Kit.Panel("6-Axis IMU Control", "Fused complementary gyro + accelerometer pipeline and rumble DSP", null, motionBody, new Thickness(16));
        motionPanel.Margin = new(0, 0, 0, 12);
        leftCol.Children.Add(motionPanel);

        // Left 2: Live Telemetry & Rear Paddle Indicators
        var teleBody = new StackPanel();
        steerBarFill = new Border
        {
            Width = 10,
            Height = 14,
            CornerRadius = ThemeCatalog.Round(3),
            Background = DS.Brush("DS.AccentHi"),
            HorizontalAlignment = HorizontalAlignment.Center,
            RenderTransform = new TranslateTransform(0, 0),
        };
        var steerTrack = new Border
        {
            Height = 20,
            CornerRadius = ThemeCatalog.Round(5),
            BorderThickness = new(1),
            BorderBrush = DS.Brush("DS.Hairline"),
            Background = new SolidColorBrush(Color.FromArgb(120, 8, 16, 34)),
            Padding = new(4, 2, 4, 2),
            Child = steerBarFill,
        };
        teleBody.Children.Add(steerTrack);
        teleBody.Children.Add(telemetryText.Also(t => t.Margin = new(0, 10, 0, 10)));

        var btnStrip = new StackPanel { Orientation = Orientation.Horizontal };
        m1Indicator = BuildInputTile("M1");
        m2Indicator = BuildInputTile("M2");
        mIndicator = BuildInputTile("M Button");
        btnStrip.Children.Add(m1Indicator);
        btnStrip.Children.Add(m2Indicator);
        btnStrip.Children.Add(mIndicator);
        teleBody.Children.Add(btnStrip);
        teleBody.Children.Add(ManagementVisuals.Copy("Shortcuts: M+D-Pad tap = Slots 1-4  |  Hold 3s M+Up = Toggle Gyro  |  Hold 3s M+Down = Recenter", 11.5).Also(t => t.Margin = new(0, 8, 0, 0)));
        var telePanel = Kit.Panel("Live 6-Axis & Paddle Telemetry", "Real-time Direct USB sensor stream and M/M1/M2 physical state", null, teleBody, new Thickness(16));
        telePanel.Margin = new(0, 0, 0, 12);
        leftCol.Children.Add(telePanel);

        // Left 3: Motion Profiles, Quick Slots & Foreground Game Auto-Switching
        var profBody = new StackPanel();
        var profSelectRow = new DockPanel { Margin = new(0, 0, 0, 8) };
        profileBox = new ComboBox
        {
            Width = 220,
            MinHeight = 32,
            DisplayMemberPath = "Name",
            SelectedValuePath = "Id",
        };
        profileBox.SelectionChanged += (_, _) =>
        {
            if (syncing || profileBox.SelectedValue is not string pid) return;
            ActivateProfileById(pid, "Loaded motion profile");
        };
        DockPanel.SetDock(profileBox, Dock.Right);
        profSelectRow.Children.Add(profileBox);
        profSelectRow.Children.Add(Kit.Text("Active Profile", 13.5, "DS.Text", FontWeights.SemiBold).Also(t => t.VerticalAlignment = VerticalAlignment.Center));
        profBody.Children.Add(profSelectRow);

        newProfileBtn = FlatAction("New", CreateNewProfile, register);
        dupProfileBtn = FlatAction("Duplicate", DuplicateCurrentProfile, register);
        delProfileBtn = FlatAction("Delete", DeleteCurrentProfile, register);
        dupProfileBtn.Margin = new(8, 0, 8, 0);
        profBody.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 0, 0, 10), Children = { newProfileBtn, dupProfileBtn, delProfileBtn } });

        var slotRow = new DockPanel { Margin = new(0, 0, 0, 8) };
        quickSlotBox = new ComboBox
        {
            Width = 220,
            MinHeight = 32,
            DisplayMemberPath = "Value",
            SelectedValuePath = "Key",
            ItemsSource = new[]
            {
                new KeyValuePair<int, string>(0, "No Quick Slot"),
                new KeyValuePair<int, string>(1, "Slot 1 (M + D-Pad Up)"),
                new KeyValuePair<int, string>(2, "Slot 2 (M + D-Pad Right)"),
                new KeyValuePair<int, string>(3, "Slot 3 (M + D-Pad Down)"),
                new KeyValuePair<int, string>(4, "Slot 4 (M + D-Pad Left)"),
            }
        };
        quickSlotBox.SelectionChanged += (_, _) => CommitProfileMetadata();
        DockPanel.SetDock(quickSlotBox, Dock.Right);
        slotRow.Children.Add(quickSlotBox);
        slotRow.Children.Add(Kit.Text("Quick Slot", 13.5, "DS.Text", FontWeights.SemiBold).Also(t => t.VerticalAlignment = VerticalAlignment.Center));
        profBody.Children.Add(slotRow);

        var exeRow = new DockPanel { Margin = new(0, 0, 0, 8) };
        useLastGameBtn = FlatAction("Use Last Game", UseLastForegroundGame, register);
        useLastGameBtn.MinHeight = 30;
        useLastGameBtn.Padding = new(10, 4, 10, 4);
        useLastGameBtn.Margin = new(8, 0, 0, 0);
        DockPanel.SetDock(useLastGameBtn, Dock.Right);
        gameExesBox = new TextBox
        {
            MinHeight = 30,
            Padding = new(8, 4, 8, 4),
            Background = new SolidColorBrush(Color.FromRgb(14, 24, 48)),
            Foreground = DS.Brush("DS.Text"),
            BorderBrush = DS.Brush("DS.Hairline"),
            BorderThickness = new(1),
            ToolTip = "Comma-separated process names (e.g. forza_gaming.exe, assettocorsa.exe)",
        };
        gameExesBox.LostFocus += (_, _) => CommitProfileMetadata();
        exeRow.Children.Add(useLastGameBtn);
        exeRow.Children.Add(gameExesBox);
        profBody.Children.Add(Kit.Text("Linked Game Executables (.exe)", 12.5, "DS.TextSoft").Also(t => t.Margin = new(0, 0, 0, 4)));
        profBody.Children.Add(exeRow);

        autoSwitchBox = MakeCheck("Auto-switch profile when linked game is in foreground");
        autoSwitchBox.Checked += (_, _) => CommitAutoSwitch();
        autoSwitchBox.Unchecked += (_, _) => CommitAutoSwitch();
        profBody.Children.Add(autoSwitchBox);
        leftCol.Children.Add(Kit.Panel("Motion Profiles & Auto-Switch", "Manage quick slots (M + D-Pad) and per-game automatic presets", null, profBody, new Thickness(16)));

        // Right 1: Independent Rear Paddles, Macros & Keyboard/Mouse
        var paddlesBody = new StackPanel();
        var actionChoices = D10PaddleActions.All.Select(k => new KeyValuePair<string, string>(k, D10PaddleActions.Label(k))).ToArray();
        m1Box = BuildPaddleRow(paddlesBody, "M1 Paddle", actionChoices);
        m2Box = BuildPaddleRow(paddlesBody, "M2 Paddle", actionChoices);
        mBox = BuildPaddleRow(paddlesBody, "M Mode Button", actionChoices);
        var macroBtnRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 8, 0, 0) };
        var editM1 = FlatAction("Edit M1 in Macros", () => openMacro("M1"), register);
        var editM2 = FlatAction("Edit M2 in Macros", () => openMacro("M2"), register);
        editM2.Margin = new(8, 0, 0, 0);
        macroBtnRow.Children.Add(editM1);
        macroBtnRow.Children.Add(editM2);
        paddlesBody.Children.Add(macroBtnRow);
        var paddlesPanel = Kit.Panel("Independent M1 / M2 / M Controls", "Remap hardware paddles to Xbox inputs, keys, mouse clicks, or macros", null, paddlesBody, new Thickness(16));
        paddlesPanel.Margin = new(0, 0, 0, 12);
        rightCol.Children.Add(paddlesPanel);

        // Right 2: Direct EP02 Motor Test
        var motorBody = new StackPanel();
        leftMotorSlider = BuildSimpleMotorSlider(motorBody, "Left grip intensity", leftMotorVal);
        rightMotorSlider = BuildSimpleMotorSlider(motorBody, "Right grip intensity", rightMotorVal);
        pulseLeftBtn = FlatAction("Pulse Left (450 ms)", () => _ = PulseAsync(true), register);
        pulseRightBtn = FlatAction("Pulse Right (450 ms)", () => _ = PulseAsync(false), register);
        stopMotorsBtn = FlatAction("Stop", () => _ = StopAsync(), register);
        pulseRightBtn.Margin = new(8, 0, 8, 0);
        pulseLeftBtn.IsEnabled = pulseRightBtn.IsEnabled = stopMotorsBtn.IsEnabled = !ReviewSandbox.Active;
        motorBody.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 10, 0, 0), Children = { pulseLeftBtn, pulseRightBtn, stopMotorsBtn } });
        var motorPanel = Kit.Panel("Dual Motor Levels & EP02 Test", "Direct USB motor packet (0x00 0x08) with standard XInput fallback", null, motorBody, new Thickness(16));
        motorPanel.Margin = new(0, 0, 0, 12);
        rightCol.Children.Add(motorPanel);

        // Right 3: Calibration & Direct USB Driver Switcher
        var calBody = new StackPanel();
        calibrateBtn = FlatAction("Quick Desk Cal (1.5s)", () => _ = RunCalibrationAsync(multiPose: false), register);
        multiCalBtn = FlatAction("15-Pose 3-Axis Cal", () =>
        {
            if (calRunningMulti && calCts != null) { calCts.Cancel(); return; }
            _ = RunCalibrationAsync(multiPose: true);
        }, register);
        resetCalBtn = FlatAction("Reset", () =>
        {
            config.Calibration = D10CalibrationData.Default;
            if (session != null) session.UpdateCalibration(config.Calibration);
            PersistConfig();
            calStatusText.Text = "Reset to factory D10 baseline calibration.";
        }, register);
        multiCalBtn.Margin = new(8, 0, 8, 0);
        calibrateBtn.IsEnabled = multiCalBtn.IsEnabled = resetCalBtn.IsEnabled = !ReviewSandbox.Active;
        calBody.Children.Add(new WrapPanel { Children = { calibrateBtn, multiCalBtn, resetCalBtn } });
        calBody.Children.Add(calStatusText.Also(t => t.Margin = new(0, 8, 0, 10)));

        calBody.Children.Add(new Border { Height = 1, Background = DS.Brush("DS.Hairline"), Margin = new(0, 2, 0, 10) });
        calBody.Children.Add(Kit.Text("Windows Direct USB Driver Mode (No Firmware Modification)", 13, "DS.Text", FontWeights.SemiBold));
        calBody.Children.Add(driverStatusText.Also(t => t.Margin = new(0, 4, 0, 8)));

        refreshDriverBtn = FlatAction("Refresh Driver", RefreshDriverStatus, register);
        switchWinUsbBtn = FlatAction("Switch to Direct USB", () => _ = SwitchDriverAsync(toWinUsb: true), register);
        restoreXboxBtn = FlatAction("Restore Xbox / Recovery", () => _ = SwitchDriverAsync(toWinUsb: false), register);
        switchWinUsbBtn.Margin = new(8, 0, 8, 0);
        calBody.Children.Add(new WrapPanel { Children = { refreshDriverBtn, switchWinUsbBtn, restoreXboxBtn } });
        rightCol.Children.Add(Kit.Panel("Calibration & Direct USB Driver", "Gyro zeroing, 15-pose 3-axis accelerometer fit, and WinUSB switching", null, calBody, new Thickness(16)));

        RefreshDriverStatus();
        RefreshFromDrafts();
        StartForegroundAutoSwitch();
    }

    private void StartForegroundAutoSwitch()
    {
        if (ReviewSandbox.Active) return;
        gameWatchTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(1200),
        };
        gameWatchTimer.Tick += (_, _) => CheckForegroundGameSwitch();
        gameWatchTimer.Start();
    }

    private void CheckForegroundGameSwitch()
    {
        if (!config.AutoSwitchByGame || ReviewSandbox.Active) return;
        string? exe = ForegroundApp.Exe;
        if (string.IsNullOrWhiteSpace(exe))
        {
            lastAutoSwitchedExe = null;
            return;
        }
        if (string.Equals(exe, lastAutoSwitchedExe, StringComparison.OrdinalIgnoreCase)) return;

        var matched = config.FindByProcessExe(exe);
        lastAutoSwitchedExe = exe;
        if (matched == null || string.Equals(matched.Id, config.ActiveProfileId, StringComparison.OrdinalIgnoreCase))
            return;

        ActivateProfileById(matched.Id, $"Auto-switched to \"{matched.Name}\" for {exe}");
    }

    private ComboBox BuildPaddleRow(Panel parent, string label, KeyValuePair<string, string>[] choices)
    {
        var row = new DockPanel { Margin = new(0, 0, 0, 8) };
        var box = new ComboBox
        {
            Width = 215,
            MinHeight = 32,
            ItemsSource = choices,
            DisplayMemberPath = "Value",
            SelectedValuePath = "Key",
        };
        box.SelectionChanged += (_, _) => CommitPaddleBindings();
        DockPanel.SetDock(box, Dock.Right);
        row.Children.Add(box);
        row.Children.Add(Kit.Text(label, 14, "DS.Text", FontWeights.SemiBold).Also(t => t.VerticalAlignment = VerticalAlignment.Center));
        parent.Children.Add(row);
        return box;
    }

    private (Slider, TextBlock) BuildSliderRow(Panel parent, string label, double min, double max, double step, double def, string unit, Action changed)
    {
        var head = new DockPanel { Margin = new(0, 4, 0, 2) };
        var val = Kit.Text($"{def:0.##} {unit}", 12.5, "DS.Text", FontWeights.SemiBold);
        DockPanel.SetDock(val, Dock.Right);
        head.Children.Add(val);
        head.Children.Add(Kit.Text(label, 13, "DS.TextSoft"));
        var slider = new Slider
        {
            Minimum = min,
            Maximum = max,
            SmallChange = step,
            LargeChange = step * 4,
            TickFrequency = step,
            IsSnapToTickEnabled = true,
            Value = def,
            Style = DS.Style("DS.Slider"),
        };
        slider.ValueChanged += (_, _) =>
        {
            val.Text = $"{slider.Value:0.##} {unit}";
            changed();
        };
        parent.Children.Add(head);
        parent.Children.Add(slider);
        return (slider, val);
    }

    private Slider BuildSimpleMotorSlider(Panel parent, string label, TextBlock readout)
    {
        var head = new DockPanel { Margin = new(0, 4, 0, 2) };
        DockPanel.SetDock(readout, Dock.Right);
        head.Children.Add(readout);
        head.Children.Add(Kit.Text(label, 13, "DS.TextSoft"));
        var slider = new Slider { Minimum = 0, Maximum = 100, TickFrequency = 5, IsSnapToTickEnabled = true, Value = 100, Style = DS.Style("DS.Slider") };
        slider.ValueChanged += (_, _) =>
        {
            if (syncing) return;
            int left = (int)Math.Round(leftMotorSlider.Value);
            int right = (int)Math.Round(rightMotorSlider.Value);
            leftMotorVal.Text = left + "%";
            rightMotorVal.Text = right + "%";
            if (vibration.SupportsNativeMotors && !vibration.PcMode)
            {
                vibration.SetMotors(left, right, 0);
                onChanged();
            }
        };
        parent.Children.Add(head);
        parent.Children.Add(slider);
        return slider;
    }

    private static Border BuildInputTile(string text) => new()
    {
        CornerRadius = ThemeCatalog.Round(5),
        BorderThickness = new(1),
        BorderBrush = DS.Brush("DS.Hairline"),
        Background = new SolidColorBrush(Color.FromArgb(80, 14, 24, 48)),
        Padding = new(12, 6, 12, 6),
        Margin = new(0, 0, 8, 0),
        Child = Kit.Text(text, 12.5, "DS.Text", FontWeights.SemiBold),
    };

    private CheckBox MakeCheck(string text) => new()
    {
        Content = text,
        Foreground = DS.Brush("DS.Text"),
        FontSize = 12.5,
        Margin = new(0, 4, 12, 4),
    };

    private static Button FlatAction(string text, Action onClick, Action<Button> register, bool primary = false)
    {
        var button = new Button
        {
            Content = text,
            Style = DS.Style(primary ? "DS.ConsolePrimary" : "DS.ConsoleBtn"),
            MinHeight = 36,
            Padding = new(14, 6, 14, 6),
        };
        button.Click += (_, _) => onClick();
        register(button);
        return button;
    }

    private void CreateNewProfile()
    {
        int n = config.Profiles.Count + 1;
        int? freeSlot = Enumerable.Range(1, 4).FirstOrDefault(s => config.FindByQuickSlot(s) == null);
        var created = new D10NamedProfile
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            Name = $"Motion Profile {n}",
            QuickSlot = freeSlot is >= 1 and <= 4 ? freeSlot : null,
            Settings = ActiveSettings.Clone().Validated(),
        };
        config.Profiles.Add(created);
        ActivateProfileById(created.Id, $"Created \"{created.Name}\"");
    }

    private void DuplicateCurrentProfile()
    {
        var cur = config.GetActiveProfile();
        var copy = new D10NamedProfile
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            Name = cur.Name + " Copy",
            QuickSlot = null,
            Settings = cur.Settings.Clone().Validated(),
        };
        config.Profiles.Add(copy);
        ActivateProfileById(copy.Id, $"Duplicated \"{cur.Name}\"");
    }

    private void DeleteCurrentProfile()
    {
        if (config.Profiles.Count <= 1)
        {
            statusBanner.Text = "At least one D10 motion profile is required.";
            return;
        }
        var cur = config.GetActiveProfile();
        config.Profiles.Remove(cur);
        config.EnsureDefaults();
        PersistConfig();
        RefreshFromDrafts();
        session?.UpdateMotionSettings(ActiveSettings);
        statusBanner.Text = $"Deleted \"{cur.Name}\". Active: \"{config.GetActiveProfile().Name}\".";
    }

    private void UseLastForegroundGame()
    {
        string? exe = ForegroundApp.Last?.Exe;
        if (string.IsNullOrWhiteSpace(exe))
        {
            statusBanner.Text = "No recent non-X20CTL game window detected yet. Tab into your game once, then return.";
            return;
        }
        var existing = gameExesBox.Text
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        if (!existing.Any(x => string.Equals(x, exe, StringComparison.OrdinalIgnoreCase)))
            existing.Add(exe);
        gameExesBox.Text = string.Join(", ", existing);
        CommitProfileMetadata();
        statusBanner.Text = $"Linked {exe} to \"{config.GetActiveProfile().Name}\".";
    }

    private void CommitProfileMetadata()
    {
        if (syncing) return;
        var active = config.GetActiveProfile();
        int chosenSlot = quickSlotBox.SelectedValue is int s ? s : 0;
        if (chosenSlot is >= 1 and <= 4)
        {
            foreach (var p in config.Profiles)
                if (p.Id != active.Id && p.QuickSlot == chosenSlot) p.QuickSlot = null;
            active.QuickSlot = chosenSlot;
        }
        else
        {
            active.QuickSlot = null;
        }

        active.GameExes = gameExesBox.Text
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        PersistConfig();
        RefreshProfileControls();
    }

    private void CommitAutoSwitch()
    {
        if (syncing) return;
        config.AutoSwitchByGame = autoSwitchBox.IsChecked == true;
        PersistConfig();
    }

    private void ActivateProfileById(string profileId, string reason)
    {
        var active = config.SetActiveProfile(profileId);
        PersistConfig();
        RefreshFromDrafts();
        session?.UpdateMotionSettings(active.Settings);
        statusBanner.Text = $"{reason}: {active.Name}";
    }

    private void CommitMotionSettings()
    {
        if (syncing) return;
        var s = ActiveSettings;
        s.Mode = (D10MotionMode)Math.Clamp(modeBox.SelectedIndex, 0, 3);
        s.FullScaleDegrees = fullScaleSlider.Value;
        s.DeadzoneDegrees = deadzoneSlider.Value;
        s.Sensitivity = sensSlider.Value;
        s.FilterTauSeconds = tauSlider.Value;
        s.InvertAxis = invertBox.IsChecked == true;
        s.VibrationGuard = vibGuardBox.IsChecked == true;
        s.VelocityLead = leadBox.IsChecked == true;
        s.TrajectoryGate = trajGateBox.IsChecked == true;
        s.MotorHarmonicFilter = harmonicBox.IsChecked == true;
        s.MidRangeBoost = boostBox.IsChecked == true;
        s.ShortcutsEnabled = shortcutsBox.IsChecked == true;
        s.Validated();
        config.Settings = s.Clone();
        session?.UpdateMotionSettings(s);
        onChanged();
    }

    private void CommitPaddleBindings()
    {
        if (syncing) return;
        var s = ActiveSettings;
        if (m1Box.SelectedValue is string m1) s.M1Binding = m1;
        if (m2Box.SelectedValue is string m2) s.M2Binding = m2;
        if (mBox.SelectedValue is string mb) s.MBinding = mb;
        s.Validated();
        config.Settings = s.Clone();
        session?.UpdateMotionSettings(s);
        onChanged();
    }

    private void RefreshProfileControls()
    {
        bool prevSync = syncing;
        syncing = true;
        try
        {
            var active = config.GetActiveProfile();
            profileBox.ItemsSource = config.Profiles.ToArray();
            profileBox.SelectedValue = active.Id;
            quickSlotBox.SelectedValue = active.QuickSlot ?? 0;
            gameExesBox.Text = string.Join(", ", active.GameExes);
            autoSwitchBox.IsChecked = config.AutoSwitchByGame;
            delProfileBtn.IsEnabled = !ReviewSandbox.Active && config.Profiles.Count > 1;
            newProfileBtn.IsEnabled = dupProfileBtn.IsEnabled = useLastGameBtn.IsEnabled = !ReviewSandbox.Active;
        }
        finally
        {
            syncing = prevSync;
        }
    }

    public void RefreshFromDrafts()
    {
        syncing = true;
        try
        {
            RefreshProfileControls();
            var s = ActiveSettings;
            modeBox.SelectedIndex = (int)s.Mode;
            fullScaleSlider.Value = s.FullScaleDegrees;
            deadzoneSlider.Value = s.DeadzoneDegrees;
            sensSlider.Value = s.Sensitivity;
            tauSlider.Value = s.FilterTauSeconds;
            fullScaleVal.Text = $"{s.FullScaleDegrees:0.##} deg";
            deadzoneVal.Text = $"{s.DeadzoneDegrees:0.##} deg";
            sensVal.Text = $"{s.Sensitivity:0.##} x";
            tauVal.Text = $"{s.FilterTauSeconds:0.##} s";
            invertBox.IsChecked = s.InvertAxis;
            vibGuardBox.IsChecked = s.VibrationGuard;
            leadBox.IsChecked = s.VelocityLead;
            trajGateBox.IsChecked = s.TrajectoryGate;
            harmonicBox.IsChecked = s.MotorHarmonicFilter;
            boostBox.IsChecked = s.MidRangeBoost;
            shortcutsBox.IsChecked = s.ShortcutsEnabled;

            m1Box.SelectedValue = s.M1Binding;
            m2Box.SelectedValue = s.M2Binding;
            mBox.SelectedValue = s.MBinding;

            leftMotorSlider.Value = vibration.SupportsNativeMotors ? vibration.Left : vibration.Strength;
            rightMotorSlider.Value = vibration.SupportsNativeMotors ? vibration.Right : vibration.Strength;
            leftMotorVal.Text = ((int)leftMotorSlider.Value) + "%";
            rightMotorVal.Text = ((int)rightMotorSlider.Value) + "%";

            session?.UpdateDrafts(buttons, curves, macros, vibration);
        }
        finally
        {
            syncing = false;
        }
    }

    public void PersistConfig()
    {
        config.Settings = ActiveSettings.Clone();
        saveConfig(config);
    }

    public void SetStatus(string message) => statusBanner.Text = message;

    private void RefreshDriverStatus()
    {
        if (ReviewSandbox.Active)
        {
            driverStatusText.Text = "Driver probe paused in preview sandbox.";
            refreshDriverBtn.IsEnabled = switchWinUsbBtn.IsEnabled = restoreXboxBtn.IsEnabled = false;
            return;
        }

        lastDriverProbe = D10DriverManager.ProbeDevice();
        driverStatusText.Text = lastDriverProbe.Summary;
        refreshDriverBtn.IsEnabled = true;
        switchWinUsbBtn.IsEnabled = lastDriverProbe.InstanceId != null &&
                                    lastDriverProbe.State is D10DriverState.OfficialXbox or D10DriverState.PartialWinUsb;
        restoreXboxBtn.IsEnabled = lastDriverProbe.InstanceId != null &&
                                   lastDriverProbe.State is D10DriverState.WinUsbReady or D10DriverState.PartialWinUsb;
    }

    private async Task SwitchDriverAsync(bool toWinUsb)
    {
        if (ReviewSandbox.Active) return;
        RefreshDriverStatus();
        if (lastDriverProbe?.InstanceId is not string instanceId)
        {
            statusBanner.Text = lastDriverProbe?.Summary ?? "Connect a single EasySMX D10 device first.";
            return;
        }

        string title = toWinUsb ? "Switch D10 to Direct USB (WinUSB)" : "Restore Official Xbox 360 Driver";
        string question = toWinUsb
            ? $"Switch {instanceId} to Windows inbox WinUSB driver so X20CTL can read 6-axis IMU and rear paddles?\n\nYour controller and receiver firmware are NOT modified."
            : $"Restore Windows official Xbox 360 driver on {instanceId} and remove Direct USB WinUSB bindings?";

        if (MessageBox.Show(question, title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        if (session != null) ShutdownSession();

        refreshDriverBtn.IsEnabled = switchWinUsbBtn.IsEnabled = restoreXboxBtn.IsEnabled = false;
        driverStatusText.Text = toWinUsb ? "Installing inbox WinUSB binding..." : "Restoring official Xbox 360 driver...";
        statusBanner.Text = driverStatusText.Text;

        try
        {
            var res = await D10DriverManager.RunElevatedActionAsync(toWinUsb, instanceId);
            statusBanner.Text = res.Message;
            driverStatusText.Text = res.Message;
        }
        catch (Exception ex)
        {
            statusBanner.Text = "Driver switch failed: " + ex.Message;
            driverStatusText.Text = statusBanner.Text;
        }
        finally
        {
            RefreshDriverStatus();
        }
    }

    private async Task SaveAndApplyAsync()
    {
        CommitProfileMetadata();
        PersistConfig();
        session?.UpdateDrafts(buttons, curves, macros, vibration);
        session?.UpdateMotionSettings(ActiveSettings);
        await applyToController(msg => statusBanner.Text = msg);
    }

    private void ToggleConnection()
    {
        if (ReviewSandbox.Active) return;
        if (session != null)
        {
            ShutdownSession();
            statusBanner.Text = "Disconnected from Direct USB session.";
            return;
        }

        try
        {
            var probe = D10UsbClient.ProbeStatus();
            if (!probe.HasWinUsbDevice)
            {
                statusBanner.Text = probe.Summary;
                UpdateConnectionBadge(D10LinkKind.Disconnected);
                return;
            }

            var created = D10UsbClient.StartSession(ActiveSettings, config.Calibration, buttons, curves, macros, vibration);
            created.TelemetryUpdated += t => Body.Dispatcher.BeginInvoke(() => RenderTelemetry(t));
            created.StatusChanged += s => Body.Dispatcher.BeginInvoke(() => statusBanner.Text = s);
            created.QuickSlotRequested += slot => Body.Dispatcher.BeginInvoke(() =>
            {
                var found = config.FindByQuickSlot(slot);
                if (found != null) ActivateProfileById(found.Id, $"M+D-Pad Slot {slot}");
                else statusBanner.Text = $"No D10 profile assigned to Quick Slot {slot}.";
            });
            created.CalibrationUpdated += cal => Body.Dispatcher.BeginInvoke(() =>
            {
                config.Calibration = cal;
                PersistConfig();
            });
            created.QuickCalibrateRequested += () => Body.Dispatcher.BeginInvoke(() => _ = RunCalibrationAsync(multiPose: false));
            created.Faulted += err => Body.Dispatcher.BeginInvoke(() =>
            {
                statusBanner.Text = err;
                ShutdownSession();
            });
            session = created;
            connectBtn.Content = "Disconnect Direct USB";
            UpdateConnectionBadge(created.LinkKind);
            statusBanner.Text = $"Connected via {created.LinkKind} ({created.DeviceDescription}).";
        }
        catch (Exception error)
        {
            statusBanner.Text = "Could not open D10 Direct USB: " + error.Message;
            UpdateConnectionBadge(D10LinkKind.Disconnected);
        }
    }

    private void RenderTelemetry(D10LiveTelemetry t)
    {
        telemetryText.Text =
            $"Roll: {t.RollDeg:+0.0;-0.0;0.0} deg   |   Pitch: {t.PitchDeg:+0.0;-0.0;0.0} deg   |   Gyro Z: {t.GyroZDps:+0.0;-0.0;0.0} dps   |   " +
            $"Rate: {t.SampleRateHz:0} Hz" + (t.VibrationDamping ? "   |   VIB GUARD" : "");
        if (steerBarFill.RenderTransform is TranslateTransform tt)
            tt.X = Math.Clamp(t.OutputX, -1.0, 1.0) * 160.0;

        PaintTile(m1Indicator, t.Extra.M1);
        PaintTile(m2Indicator, t.Extra.M2);
        PaintTile(mIndicator, t.Extra.M);
    }

    private static void PaintTile(Border tile, bool pressed)
    {
        tile.Background = new SolidColorBrush(pressed ? Color.FromArgb(190, 56, 189, 248) : Color.FromArgb(80, 14, 24, 48));
        tile.BorderBrush = pressed ? Brushes.White : DS.Brush("DS.Hairline");
    }

    private void UpdateConnectionBadge(D10LinkKind kind)
    {
        stateText.Text = kind switch
        {
            D10LinkKind.WiredUsb => "Wired USB (EP83)",
            D10LinkKind.WirelessReceiver => "2.4G Direct",
            _ => "Disconnected",
        };
        stateText.Foreground = kind == D10LinkKind.Disconnected
            ? DS.Brush("DS.TextSoft")
            : new SolidColorBrush(Color.FromRgb(110, 231, 183));
    }

    private async Task PulseAsync(bool left)
    {
        int strength = Math.Clamp((int)Math.Round(left ? leftMotorSlider.Value : rightMotorSlider.Value), 0, 100);
        if (strength == 0) strength = 50;
        pulseLeftBtn.IsEnabled = pulseRightBtn.IsEnabled = false;
        statusBanner.Text = $"Pulsing {(left ? "left" : "right")} motor at {strength}%...";
        try
        {
            if (session != null)
                await session.PulseMotorAsync(left, strength);
            else
                await D10UsbClient.TryQuickPulseAsync(left, strength);
            statusBanner.Text = $"{(left ? "Left" : "Right")} motor pulse complete.";
        }
        catch (Exception error)
        {
            statusBanner.Text = "Motor pulse failed: " + error.Message;
        }
        finally
        {
            pulseLeftBtn.IsEnabled = pulseRightBtn.IsEnabled = !ReviewSandbox.Active;
        }
    }

    private async Task StopAsync()
    {
        try
        {
            if (session != null) await session.StopMotorsAsync();
            else await D10UsbClient.TryQuickStopAsync();
            statusBanner.Text = "Motors stopped.";
        }
        catch (Exception error)
        {
            statusBanner.Text = "Stop failed: " + error.Message;
        }
    }

    private async Task RunCalibrationAsync(bool multiPose)
    {
        if (session == null)
        {
            calStatusText.Text = "Connect Direct USB first so stationary IMU samples can be captured.";
            return;
        }
        calCts?.Cancel();
        calCts?.Dispose();
        calCts = new CancellationTokenSource();
        calRunningMulti = multiPose;
        calibrateBtn.IsEnabled = false;
        multiCalBtn.Content = multiPose ? "Cancel 15-Pose" : "15-Pose 3-Axis Cal";
        multiCalBtn.IsEnabled = multiPose;
        calStatusText.Text = multiPose
            ? "Starting 15-pose 3-axis capture: hold Pose 1 steady..."
            : "Keep controller still on a flat surface (calibrating 0%)...";
        try
        {
            D10CalibrationData res = multiPose
                ? await session.CalibrateMultiPoseAsync(
                    st => Body.Dispatcher.BeginInvoke(() => calStatusText.Text = st.Message),
                    calCts.Token)
                : await session.CalibrateAsync(
                    p => Body.Dispatcher.BeginInvoke(() => calStatusText.Text = $"Calibrating stationary gyro bias ({p * 100:0}%)..."),
                    calCts.Token);

            config.Calibration = res;
            PersistConfig();
            calStatusText.Text = multiPose
                ? $"15-Pose calibrated: Offsets ({res.AccelOffsetX:0}, {res.AccelOffsetY:0}, {res.AccelOffsetZ:0}), Scales ({res.AccelScaleX:0}, {res.AccelScaleY:0}, {res.AccelScaleZ:0})."
                : $"Calibrated cleanly: Center {res.CenterAngleDeg:0.2} deg, Gyro bias ({res.GyroBiasX:0.1}, {res.GyroBiasY:0.1}, {res.GyroBiasZ:0.1}).";
        }
        catch (OperationCanceledException)
        {
            calStatusText.Text = "Calibration cancelled.";
        }
        catch (Exception error)
        {
            calStatusText.Text = "Calibration failed: " + error.Message;
        }
        finally
        {
            calRunningMulti = false;
            multiCalBtn.Content = "15-Pose 3-Axis Cal";
            calibrateBtn.IsEnabled = multiCalBtn.IsEnabled = !ReviewSandbox.Active;
        }
    }

    public void ShutdownSession()
    {
        try { gameWatchTimer?.Stop(); } catch { }
        try { calCts?.Cancel(); } catch { }
        calCts?.Dispose();
        calCts = null;
        if (session != null)
        {
            session.Dispose();
            session = null;
        }
        connectBtn.Content = "Connect Direct USB";
        UpdateConnectionBadge(D10LinkKind.Disconnected);
    }
}
