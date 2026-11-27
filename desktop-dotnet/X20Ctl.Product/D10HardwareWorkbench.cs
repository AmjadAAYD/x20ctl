using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using X20Ctl.Zone;
using X20Ctl.Zone.D10;

namespace X20Ctl.Product;

/// <summary>
/// Dedicated EasySMX D10 native workbench:
/// 6-axis IMU motion steering / aiming / gyro mouse, independent M1/M2/M paddle actions,
/// direct EP02 dual motor intensity and pulse testing, resting bias calibration, and live telemetry.
/// </summary>
public sealed class D10HardwareWorkbench
{
    public Grid Body { get; } = new();

    private readonly ButtonDraft buttons;
    private readonly CurveDraft curves;
    private readonly MacroDraft macros;
    private readonly VibrationDraft vibration;
    private readonly D10MotionSettings settings;
    private readonly Action<D10MotionSettings, D10CalibrationData> saveConfig;
    private readonly Func<Action<string>, Task> applyToController;
    private readonly Action<string> openMacro;
    private readonly Action onChanged;

    private D10CalibrationData calibration;
    private D10DirectSession? session;

    private readonly TextBlock statusBanner = ManagementVisuals.Copy("Connect EasySMX D10 via Direct USB (WinUSB) for 6-axis motion and independent M1/M2 paddles, or test standard vibration.", 13.5);
    private readonly Border statePill;
    private readonly TextBlock stateText = new() { FontSize = 12, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
    private readonly Button connectBtn, recenterBtn, saveApplyBtn;

    private readonly ComboBox modeBox;
    private readonly Slider fullScaleSlider, deadzoneSlider, sensSlider, tauSlider;
    private readonly TextBlock fullScaleVal, deadzoneVal, sensVal, tauVal;
    private readonly CheckBox invertBox, vibGuardBox, leadBox;

    private readonly Border steerBarFill;
    private readonly TextBlock telemetryText = ManagementVisuals.Copy("Roll: 0.0 deg   |   Pitch: 0.0 deg   |   Gyro Z: 0.0 dps   |   Rate: 0 Hz", 13, true);
    private readonly Border m1Indicator, m2Indicator, mIndicator;

    private readonly ComboBox m1Box, m2Box, mBox;
    private readonly Slider leftMotorSlider, rightMotorSlider;
    private readonly TextBlock leftMotorVal = Kit.Text("100%", 13, "DS.Text", FontWeights.SemiBold);
    private readonly TextBlock rightMotorVal = Kit.Text("100%", 13, "DS.Text", FontWeights.SemiBold);
    private readonly Button pulseLeftBtn, pulseRightBtn, stopMotorsBtn;

    private readonly Button calibrateBtn, resetCalBtn;
    private readonly TextBlock calStatusText = ManagementVisuals.Copy("Place controller on a flat desk and run 1.5s resting calibration.", 13);

    private CancellationTokenSource? calCts;
    private bool syncing;

    public D10HardwareWorkbench(
        FrameworkElement resources,
        Action<Button> register,
        ButtonDraft buttons,
        CurveDraft curves,
        MacroDraft macros,
        VibrationDraft vibration,
        D10SavedConfig initialConfig,
        Action<D10MotionSettings, D10CalibrationData> saveConfig,
        Func<Action<string>, Task> applyToController,
        Action<string> openMacro,
        Action onChanged)
    {
        this.buttons = buttons;
        this.curves = curves;
        this.macros = macros;
        this.vibration = vibration;
        this.settings = initialConfig.Settings.Clone().Validated();
        this.calibration = initialConfig.Calibration;
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
                calibration = session.Recenter();
                PersistConfig();
                statusBanner.Text = $"Gyro center updated to {calibration.CenterAngleDeg:0.1} deg.";
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

        // Left 1: 6-Axis Motion Tuning
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
        vibGuardBox = MakeCheck("Rumble Vibration Guard");
        leadBox = MakeCheck("Velocity Lead Compensation");
        foreach (var cb in new[] { invertBox, vibGuardBox, leadBox })
        {
            cb.Checked += (_, _) => CommitMotionSettings();
            cb.Unchecked += (_, _) => CommitMotionSettings();
        }
        motionBody.Children.Add(new WrapPanel { Margin = new(0, 6, 0, 0), Children = { invertBox, vibGuardBox, leadBox } });
        var motionPanel = Kit.Panel("6-Axis IMU Control", "Fused complementary gyro + accelerometer pipeline", null, motionBody, new Thickness(16));
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
        leftCol.Children.Add(Kit.Panel("Live 6-Axis & Paddle Telemetry", "Real-time Direct USB sensor stream and M/M1/M2 physical state", null, teleBody, new Thickness(16)));

        // Right 1: Independent Rear Paddles
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
        var paddlesPanel = Kit.Panel("Independent M1 / M2 / M Controls", "Remap hardware paddles, toggle gyro, or fire X20CTL macros", null, paddlesBody, new Thickness(16));
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

        // Right 3: Calibration
        var calBody = new StackPanel();
        calibrateBtn = FlatAction("Calibrate Center & Gyro Bias", () => _ = RunCalibrationAsync(), register);
        resetCalBtn = FlatAction("Reset Calibration", () =>
        {
            calibration = D10CalibrationData.Default;
            if (session != null) session.UpdateCalibration(calibration);
            PersistConfig();
            calStatusText.Text = "Reset to factory D10 baseline calibration.";
        }, register);
        resetCalBtn.Margin = new(8, 0, 0, 0);
        calibrateBtn.IsEnabled = resetCalBtn.IsEnabled = !ReviewSandbox.Active;
        calBody.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Children = { calibrateBtn, resetCalBtn } });
        calBody.Children.Add(calStatusText.Also(t => t.Margin = new(0, 10, 0, 0)));
        calBody.Children.Add(ManagementVisuals.Copy("Connects non-destructively to Direct USB (WinUSB) interfaces when present. Controller and receiver firmware are never modified.", 12).Also(t => t.Margin = new(0, 8, 0, 0)));
        rightCol.Children.Add(Kit.Panel("Sensor Calibration & Driver Info", "Stationary resting bias and tilt zeroing", null, calBody, new Thickness(16)));

        RefreshFromDrafts();
    }

    private ComboBox BuildPaddleRow(Panel parent, string label, KeyValuePair<string, string>[] choices)
    {
        var row = new DockPanel { Margin = new(0, 0, 0, 8) };
        var box = new ComboBox
        {
            Width = 200,
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
        FontSize = 13,
        Margin = new(0, 4, 14, 4),
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

    private void CommitMotionSettings()
    {
        if (syncing) return;
        settings.Mode = (D10MotionMode)Math.Clamp(modeBox.SelectedIndex, 0, 3);
        settings.FullScaleDegrees = fullScaleSlider.Value;
        settings.DeadzoneDegrees = deadzoneSlider.Value;
        settings.Sensitivity = sensSlider.Value;
        settings.FilterTauSeconds = tauSlider.Value;
        settings.InvertAxis = invertBox.IsChecked == true;
        settings.VibrationGuard = vibGuardBox.IsChecked == true;
        settings.VelocityLead = leadBox.IsChecked == true;
        settings.Validated();
        session?.UpdateMotionSettings(settings);
        onChanged();
    }

    private void CommitPaddleBindings()
    {
        if (syncing) return;
        if (m1Box.SelectedValue is string m1) settings.M1Binding = m1;
        if (m2Box.SelectedValue is string m2) settings.M2Binding = m2;
        if (mBox.SelectedValue is string mb) settings.MBinding = mb;
        settings.Validated();
        session?.UpdateMotionSettings(settings);
        onChanged();
    }

    public void RefreshFromDrafts()
    {
        syncing = true;
        try
        {
            modeBox.SelectedIndex = (int)settings.Mode;
            fullScaleSlider.Value = settings.FullScaleDegrees;
            deadzoneSlider.Value = settings.DeadzoneDegrees;
            sensSlider.Value = settings.Sensitivity;
            tauSlider.Value = settings.FilterTauSeconds;
            fullScaleVal.Text = $"{settings.FullScaleDegrees:0.##} deg";
            deadzoneVal.Text = $"{settings.DeadzoneDegrees:0.##} deg";
            sensVal.Text = $"{settings.Sensitivity:0.##} x";
            tauVal.Text = $"{settings.FilterTauSeconds:0.##} s";
            invertBox.IsChecked = settings.InvertAxis;
            vibGuardBox.IsChecked = settings.VibrationGuard;
            leadBox.IsChecked = settings.VelocityLead;

            m1Box.SelectedValue = settings.M1Binding;
            m2Box.SelectedValue = settings.M2Binding;
            mBox.SelectedValue = settings.MBinding;

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

    public void PersistConfig() => saveConfig(settings, calibration);

    public void SetStatus(string message) => statusBanner.Text = message;

    private async Task SaveAndApplyAsync()
    {
        PersistConfig();
        session?.UpdateDrafts(buttons, curves, macros, vibration);
        session?.UpdateMotionSettings(settings);
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

            var created = D10UsbClient.StartSession(settings, calibration, buttons, curves, macros, vibration);
            created.TelemetryUpdated += t => Body.Dispatcher.BeginInvoke(() => RenderTelemetry(t));
            created.StatusChanged += s => Body.Dispatcher.BeginInvoke(() => statusBanner.Text = s);
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
            D10LinkKind.WirelessReceiver => "2.4G Direct (V0.5ae)",
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

    private async Task RunCalibrationAsync()
    {
        if (session == null)
        {
            calStatusText.Text = "Connect Direct USB first so stationary IMU samples can be captured.";
            return;
        }
        calCts?.Cancel();
        calCts?.Dispose();
        calCts = new CancellationTokenSource();
        calibrateBtn.IsEnabled = false;
        calStatusText.Text = "Keep controller still on a flat surface (calibrating 0%)...";
        try
        {
            var res = await session.CalibrateAsync(p => Body.Dispatcher.BeginInvoke(() => calStatusText.Text = $"Calibrating stationary gyro bias ({p * 100:0}%)..."), calCts.Token);
            calibration = res;
            PersistConfig();
            calStatusText.Text = $"Calibrated cleanly: Center {res.CenterAngleDeg:0.2} deg, Gyro bias ({res.GyroBiasX:0.1}, {res.GyroBiasY:0.1}, {res.GyroBiasZ:0.1}).";
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
            calibrateBtn.IsEnabled = !ReviewSandbox.Active;
        }
    }

    public void ShutdownSession()
    {
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
