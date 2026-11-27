using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using X20Ctl.Zone;
using X20Ctl.Zone.PcMode;
using X20Ctl.Zone.X10;

namespace X20Ctl.Product;

/// <summary>
/// Dedicated native Bluetooth hardware workbench for EasySMX X10:
/// rear paddles (M1/M2 passthrough, macro mode, explicit disable), hardware turbo,
/// dual motor intensities + live test, stick/trigger axis inversion, calibration, and full read-back verification.
/// </summary>
public sealed class X10HardwareWorkbench
{
    public Grid Body { get; } = new();

    private readonly ButtonDraft buttons;
    private readonly CurveDraft curves;
    private readonly MacroDraft macros;
    private readonly VibrationDraft vibration;
    private readonly X10NativeDraft x10;
    private readonly Action openCurves;
    private readonly Action<string> openMacros;
    private readonly Action onChanged;

    private readonly TextBlock bannerStatus = ManagementVisuals.Copy("Connect EasySMX X10 in Bluetooth mode to read, write, test, or calibrate hardware.", 13.5);
    private readonly TextBlock verifyBadge = new() { FontSize = 12, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
    private readonly Border verifyPill;
    private readonly TextBlock infoReadout = ManagementVisuals.Copy("No Bluetooth snapshot read yet. Local changes can still be prepared and applied once paired.", 13);

    private readonly Button readButton, applyButton, factoryResetAsk, factoryResetConfirmBtn, factoryResetCancelBtn;
    private readonly StackPanel factoryResetConfirmBar;
    private readonly Dictionary<string, (ComboBox ModeBox, ComboBox TargetBox, CheckBox RepeatBox, Button EditMacroBtn)> paddleUi = new(StringComparer.Ordinal);
    private readonly CheckBox lsInvX, lsInvY, rsInvX, rsInvY;
    private readonly TextBlock rangesSummary = ManagementVisuals.Copy("", 13);

    private readonly ComboBox turboKeyBox;
    private readonly CheckBox turboAutoBox;
    private readonly Slider turboRateSlider;
    private readonly TextBlock turboRateLabel = Kit.Text("Rate 10 / 20", 13, "DS.TextSoft");
    private readonly WrapPanel turboListPanel = new() { Margin = new(0, 8, 0, 0) };

    private readonly Slider leftMotorSlider, rightMotorSlider;
    private readonly TextBlock leftMotorVal = Kit.Text("100%", 13, "DS.Text", FontWeights.SemiBold);
    private readonly TextBlock rightMotorVal = Kit.Text("100%", 13, "DS.Text", FontWeights.SemiBold);
    private readonly Button testLeftBtn, testRightBtn, stopMotorsBtn;

    private readonly Button calSticksBtn, calTriggersBtn, calSelfBtn, calStopBtn;
    private readonly TextBlock calStatus = ManagementVisuals.Copy("Idle. Calibration guides sticks/triggers through full hardware range.", 13);

    private CancellationTokenSource? activeCalibrationCts;
    private bool syncing;

    public X10HardwareWorkbench(
        FrameworkElement resources,
        Action<Button> register,
        ButtonDraft buttons,
        CurveDraft curves,
        MacroDraft macros,
        VibrationDraft vibration,
        X10NativeDraft x10,
        Func<Action<string>, Task> readAction,
        Func<Action<string>, Task> applyAction,
        Func<Action<string>, Task> resetAction,
        Action openCurves,
        Action<string> openMacros,
        Action onChanged)
    {
        this.buttons = buttons;
        this.curves = curves;
        this.macros = macros;
        this.vibration = vibration;
        this.x10 = x10;
        this.openCurves = openCurves;
        this.openMacros = openMacros;
        this.onChanged = onChanged;

        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new(0, 0, 6, 8) };
        var root = new StackPanel();
        scroll.Content = root;
        Body.Children.Add(scroll);

        // Top hardware command banner
        verifyPill = new Border
        {
            CornerRadius = ThemeCatalog.Round(6),
            Padding = new(10, 4, 10, 4),
            BorderThickness = new(1),
            BorderBrush = DS.Brush("DS.Hairline"),
            Background = new SolidColorBrush(Color.FromArgb(90, 20, 34, 60)),
            Margin = new(0, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = verifyBadge,
        };
        SetVerificationVisual(Krly.VerificationStatus.Unconfirmed, "Not read yet");

        readButton = FlatAction("Read from X10", () => _ = RunBusyAsync(readButton!, readAction), register);
        applyButton = FlatAction("Apply All to X10", () => _ = RunBusyAsync(applyButton!, applyAction), register, primary: true);
        applyButton.Margin = new(8, 0, 0, 0);

        factoryResetConfirmBtn = FlatAction("Confirm reset", () => { }, register);
        factoryResetCancelBtn = FlatAction("Cancel", () => { }, register);
        factoryResetCancelBtn.Margin = new(8, 0, 0, 0);
        factoryResetConfirmBar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Visibility = Visibility.Collapsed,
            Margin = new(0, 8, 0, 0),
            Children = { factoryResetConfirmBtn, factoryResetCancelBtn }
        };
        factoryResetAsk = FlatAction("Factory reset", () =>
        {
            factoryResetConfirmBar.Visibility = Visibility.Visible;
            bannerStatus.Text = "Reset all settings saved on the X10? This cannot be undone.";
        }, register);
        factoryResetAsk.Margin = new(8, 0, 0, 0);
        factoryResetCancelBtn.Click += (_, _) =>
        {
            factoryResetConfirmBar.Visibility = Visibility.Collapsed;
            bannerStatus.Text = "Factory reset cancelled.";
        };
        factoryResetConfirmBtn.Click += async (_, _) =>
        {
            factoryResetConfirmBar.Visibility = Visibility.Collapsed;
            await RunBusyAsync(factoryResetAsk, resetAction);
        };

        readButton.IsEnabled = applyButton.IsEnabled = factoryResetAsk.IsEnabled = !ReviewSandbox.Active;

        var topBar = new DockPanel();
        var topButtons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { verifyPill, readButton, applyButton, factoryResetAsk }
        };
        DockPanel.SetDock(topButtons, Dock.Right);
        topBar.Children.Add(topButtons);
        var topTitle = new StackPanel
        {
            Children =
            {
                Kit.Text("X10 NATIVE HARDWARE", 12, "DS.AccentHi", FontWeights.SemiBold),
                bannerStatus.Also(t => t.Margin = new(0, 4, 12, 0)),
            }
        };
        topBar.Children.Add(topTitle);

        var headerStack = new StackPanel { Children = { topBar, infoReadout.Also(t => t.Margin = new(0, 10, 0, 0)), factoryResetConfirmBar } };
        var topPanel = Kit.Panel(null, null, null, headerStack, new Thickness(18, 14, 18, 14));
        topPanel.Margin = new(0, 0, 0, 12);
        root.Children.Add(topPanel);

        // 2-Column Workspace
        var columns = new Grid();
        columns.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        columns.ColumnDefinitions.Add(new() { Width = new(14) });
        columns.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        root.Children.Add(columns);

        var leftCol = new StackPanel();
        var rightCol = new StackPanel();
        Grid.SetColumn(rightCol, 2);
        columns.Children.Add(leftCol);
        columns.Children.Add(rightCol);

        // Left 1: Rear Paddles (M1 & M2 via 0x3A)
        var paddlesBody = new StackPanel();
        foreach (string slot in X10NativeDraft.PaddleSlots)
        {
            paddlesBody.Children.Add(BuildPaddleCard(resources, register, slot));
        }
        paddlesBody.Children.Add(ManagementVisuals.Copy("Rear paddles are stored via command 0x3A (passthrough remap, multi-step macro, or explicit 7-byte disable) so they never duplicate face-button output.", 12));
        var paddlesPanel = Kit.Panel("Rear Paddles (M1 / M2)", "Direct controller slot assignment over Bluetooth", null, paddlesBody, new Thickness(16));
        paddlesPanel.Margin = new(0, 0, 0, 12);
        leftCol.Children.Add(paddlesPanel);

        // Left 2: Stick & Trigger Hardware Polarity & Ranges
        var sticksBody = new StackPanel();
        lsInvX = MakeCheck("Invert Left Stick X");
        lsInvY = MakeCheck("Invert Left Stick Y");
        rsInvX = MakeCheck("Invert Right Stick X");
        rsInvY = MakeCheck("Invert Right Stick Y");
        foreach (var cb in new[] { lsInvX, lsInvY, rsInvX, rsInvY })
            cb.Checked += (_, _) => CommitAxisFlags();
        foreach (var cb in new[] { lsInvX, lsInvY, rsInvX, rsInvY })
            cb.Unchecked += (_, _) => CommitAxisFlags();

        var invGrid = new UniformGrid { Columns = 2, Margin = new(0, 4, 0, 10) };
        invGrid.Children.Add(lsInvX);
        invGrid.Children.Add(rsInvX);
        invGrid.Children.Add(lsInvY);
        invGrid.Children.Add(rsInvY);
        sticksBody.Children.Add(invGrid);
        sticksBody.Children.Add(rangesSummary.Also(t => t.Margin = new(0, 0, 0, 10)));
        var editRangesBtn = FlatAction("Open Curves (Inner / Outer Travel)", openCurves, register);
        editRangesBtn.HorizontalAlignment = HorizontalAlignment.Left;
        sticksBody.Children.Add(editRangesBtn);
        leftCol.Children.Add(Kit.Panel("Sticks & Triggers Polarity", "Hardware deadzones come from Curves; axis inversion writes directly via 0x3E", null, sticksBody, new Thickness(16)));

        // Right 1: Hardware Turbo (0x37)
        var turboBody = new StackPanel();
        var turboAddRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        turboKeyBox = new ComboBox { Width = 128, MinHeight = 32, Margin = new(0, 0, 8, 0), ItemsSource = X10NativeDraft.TurboKeys };
        turboKeyBox.SelectedIndex = 0;
        turboAutoBox = new CheckBox { Content = "Auto-fire", Foreground = DS.Brush("DS.Text"), VerticalAlignment = VerticalAlignment.Center, Margin = new(0, 0, 10, 0) };
        var turboAddBtn = FlatAction("Set Turbo", AddOrUpdateTurbo, register);
        turboAddBtn.MinHeight = 32;
        turboAddBtn.Padding = new(12, 4, 12, 4);
        turboAddRow.Children.Add(turboKeyBox);
        turboAddRow.Children.Add(turboAutoBox);
        turboAddRow.Children.Add(turboAddBtn);
        turboBody.Children.Add(turboAddRow);

        var rateHead = new DockPanel { Margin = new(0, 10, 0, 2) };
        rateHead.Children.Add(turboRateLabel);
        turboRateSlider = new Slider { Minimum = 1, Maximum = 20, Value = 10, TickFrequency = 1, IsSnapToTickEnabled = true, Style = DS.Style("DS.Slider") };
        turboRateSlider.ValueChanged += (_, _) => turboRateLabel.Text = $"Rate {(int)turboRateSlider.Value} / 20";
        turboBody.Children.Add(rateHead);
        turboBody.Children.Add(turboRateSlider);
        turboBody.Children.Add(turboListPanel);

        var clearTurboBtn = FlatAction("Clear all turbo", () =>
        {
            x10.ClearTurbo();
            RefreshTurboList();
            onChanged();
        }, register);
        clearTurboBtn.HorizontalAlignment = HorizontalAlignment.Left;
        clearTurboBtn.Margin = new(0, 10, 0, 0);
        turboBody.Children.Add(clearTurboBtn);

        var turboPanel = Kit.Panel("Hardware Turbo (0x37)", "Onboard rapid fire per input button (rates 1 to 20)", null, turboBody, new Thickness(16));
        turboPanel.Margin = new(0, 0, 0, 12);
        rightCol.Children.Add(turboPanel);

        // Right 2: Dual Motor Intensity & Live Test (0x35 / 0x45)
        var motorBody = new StackPanel();
        leftMotorSlider = BuildMotorSlider(motorBody, "Left grip intensity", leftMotorVal, () => CommitMotors());
        rightMotorSlider = BuildMotorSlider(motorBody, "Right grip intensity", rightMotorVal, () => CommitMotors());
        testLeftBtn = FlatAction("Pulse Left (500 ms)", () => _ = PulseMotorAsync(true), register);
        testRightBtn = FlatAction("Pulse Right (500 ms)", () => _ = PulseMotorAsync(false), register);
        stopMotorsBtn = FlatAction("Stop", () => _ = StopMotorTestAsync(), register);
        testRightBtn.Margin = new(8, 0, 8, 0);
        testLeftBtn.IsEnabled = testRightBtn.IsEnabled = stopMotorsBtn.IsEnabled = !ReviewSandbox.Active;
        motorBody.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 10, 0, 0), Children = { testLeftBtn, testRightBtn, stopMotorsBtn } });

        var motorPanel = Kit.Panel("Dual Motor Levels & Live Test", "Persisted intensities (0x35) and immediate pulse check (0x45)", null, motorBody, new Thickness(16));
        motorPanel.Margin = new(0, 0, 0, 12);
        rightCol.Children.Add(motorPanel);

        // Right 3: Hardware Calibration (0x78 / 0x41-0x42)
        var calBody = new StackPanel();
        calSticksBtn = FlatAction("Calibrate Sticks", () => _ = StartCalibrationAsync(Krly.CalibrationTarget.Sticks), register);
        calTriggersBtn = FlatAction("Calibrate Triggers", () => _ = StartCalibrationAsync(Krly.CalibrationTarget.Triggers), register);
        calSelfBtn = FlatAction("Full Self-Calibration", () => _ = StartSelfCalibrationAsync(), register);
        calStopBtn = FlatAction("Stop", StopActiveCalibration, register);
        calTriggersBtn.Margin = new(8, 0, 0, 0);
        calSelfBtn.Margin = new(8, 0, 8, 0);
        calStopBtn.IsEnabled = false;
        calSticksBtn.IsEnabled = calTriggersBtn.IsEnabled = calSelfBtn.IsEnabled = !ReviewSandbox.Active;

        calBody.Children.Add(new WrapPanel { Children = { calSticksBtn, calTriggersBtn, calSelfBtn, calStopBtn } });
        calBody.Children.Add(calStatus.Also(t => t.Margin = new(0, 10, 0, 0)));
        rightCol.Children.Add(Kit.Panel("Hardware Calibration", "Interactive stick/trigger range (0x78) and sensor self-calibration (0x41)", null, calBody, new Thickness(16)));

        RefreshFromDrafts();
    }

    private Border BuildPaddleCard(FrameworkElement resources, Action<Button> register, string slot)
    {
        var row = new Border
        {
            CornerRadius = ThemeCatalog.Round(6),
            BorderThickness = new(1),
            BorderBrush = DS.Brush("DS.Hairline"),
            Background = new SolidColorBrush(Color.FromArgb(85, 10, 20, 42)),
            Padding = new(12, 10, 12, 10),
            Margin = new(0, 0, 0, 8),
        };
        var stack = new StackPanel();
        var top = new DockPanel();
        var modeBox = new ComboBox
        {
            Width = 182,
            MinHeight = 32,
            ItemsSource = new[] { "Passthrough remap", "Macro sequence", "Disabled" },
        };
        var targetBox = new ComboBox
        {
            Width = 112,
            MinHeight = 32,
            Margin = new(8, 0, 0, 0),
            ItemsSource = ButtonDraft.Targets,
        };
        var repeatBox = new CheckBox
        {
            Content = "Repeat while held",
            Foreground = DS.Brush("DS.Text"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new(0, 8, 10, 0),
        };
        var editMacroBtn = FlatAction($"Edit {slot} in Macros", () => openMacros(slot), register);
        editMacroBtn.MinHeight = 30;
        editMacroBtn.Padding = new(10, 4, 10, 4);
        editMacroBtn.Margin = new(0, 6, 0, 0);

        modeBox.SelectionChanged += (_, _) =>
        {
            if (syncing) return;
            ApplyPaddleUi(slot);
        };
        targetBox.SelectionChanged += (_, _) =>
        {
            if (syncing) return;
            ApplyPaddleUi(slot);
        };
        repeatBox.Checked += (_, _) => { if (!syncing) ApplyPaddleUi(slot); };
        repeatBox.Unchecked += (_, _) => { if (!syncing) ApplyPaddleUi(slot); };

        var controls = new StackPanel { Orientation = Orientation.Horizontal, Children = { modeBox, targetBox } };
        DockPanel.SetDock(controls, Dock.Right);
        top.Children.Add(controls);
        top.Children.Add(Kit.Text($"{slot} Rear Paddle", 14.5, "DS.Text", FontWeights.SemiBold).Also(t => t.VerticalAlignment = VerticalAlignment.Center));

        var subRow = new StackPanel { Orientation = Orientation.Horizontal, Children = { repeatBox, editMacroBtn } };
        stack.Children.Add(top);
        stack.Children.Add(subRow);
        row.Child = stack;

        paddleUi[slot] = (modeBox, targetBox, repeatBox, editMacroBtn);
        return row;
    }

    private void ApplyPaddleUi(string slot)
    {
        if (!paddleUi.TryGetValue(slot, out var ui)) return;
        int idx = ui.ModeBox.SelectedIndex;
        if (idx == 2)
        {
            x10.SetPaddleDisabled(slot);
        }
        else if (idx == 1)
        {
            x10.SetPaddleMacro(slot, ui.RepeatBox.IsChecked == true);
        }
        else
        {
            string target = ui.TargetBox.SelectedItem as string ?? (slot == "M2" ? "B" : "A");
            x10.SetPaddlePassthrough(slot, target);
        }
        UpdatePaddleVisibility(slot);
        onChanged();
    }

    private void UpdatePaddleVisibility(string slot)
    {
        if (!paddleUi.TryGetValue(slot, out var ui)) return;
        int idx = ui.ModeBox.SelectedIndex;
        ui.TargetBox.Visibility = idx == 0 ? Visibility.Visible : Visibility.Collapsed;
        ui.RepeatBox.Visibility = idx == 1 ? Visibility.Visible : Visibility.Collapsed;
        ui.EditMacroBtn.Visibility = idx == 1 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void CommitAxisFlags()
    {
        if (syncing) return;
        void ApplyStick(CurveChannel ch, bool invX, bool invY)
        {
            byte f = (byte)((invX ? (byte)CurveHardwareFlags.InvertX : 0) | (invY ? (byte)CurveHardwareFlags.InvertY : 0));
            curves.Set(ch, curves[ch] with { Flags = f });
        }
        ApplyStick(CurveChannel.LeftStick, lsInvX.IsChecked == true, lsInvY.IsChecked == true);
        ApplyStick(CurveChannel.RightStick, rsInvX.IsChecked == true, rsInvY.IsChecked == true);
        onChanged();
    }

    private void AddOrUpdateTurbo()
    {
        if (turboKeyBox.SelectedItem is not string key) return;
        x10.SetTurbo(key, enabled: true, automatic: turboAutoBox.IsChecked == true, rate: (int)turboRateSlider.Value);
        RefreshTurboList();
        onChanged();
    }

    private void CommitMotors()
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
    }

    private Slider BuildMotorSlider(Panel parent, string label, TextBlock readout, Action changed)
    {
        var head = new DockPanel { Margin = new(0, 6, 0, 2) };
        DockPanel.SetDock(readout, Dock.Right);
        head.Children.Add(readout);
        head.Children.Add(Kit.Text(label, 13.5, "DS.TextSoft"));
        var slider = new Slider { Minimum = 0, Maximum = 100, TickFrequency = 5, IsSnapToTickEnabled = true, Value = 100, Style = DS.Style("DS.Slider") };
        slider.ValueChanged += (_, _) => changed();
        parent.Children.Add(head);
        parent.Children.Add(slider);
        return slider;
    }

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

    private CheckBox MakeCheck(string text) => new()
    {
        Content = text,
        Foreground = DS.Brush("DS.Text"),
        FontSize = 13,
        Margin = new(0, 4, 8, 4),
    };

    public void RefreshFromDrafts()
    {
        syncing = true;
        try
        {
            foreach (string slot in X10NativeDraft.PaddleSlots)
            {
                if (!paddleUi.TryGetValue(slot, out var ui)) continue;
                var mode = x10.PaddleMode(slot);
                if (mode != Krly.PaddleSlotKind.Disabled && macros[slot].Count > 0 && mode == Krly.PaddleSlotKind.Passthrough)
                {
                    mode = Krly.PaddleSlotKind.Macro;
                    x10.SetPaddleMacro(slot, macros.Options(slot).Mode != MacroPlayMode.Once);
                }
                ui.ModeBox.SelectedIndex = mode switch
                {
                    Krly.PaddleSlotKind.Macro => 1,
                    Krly.PaddleSlotKind.Disabled => 2,
                    _ => 0,
                };
                ui.TargetBox.SelectedItem = x10.PaddleTarget(slot);
                ui.RepeatBox.IsChecked = x10.PaddleRepeat(slot);
                UpdatePaddleVisibility(slot);
            }

            var ls = curves[CurveChannel.LeftStick];
            var rs = curves[CurveChannel.RightStick];
            var lt = curves[CurveChannel.LeftTrigger];
            var rt = curves[CurveChannel.RightTrigger];
            lsInvX.IsChecked = (ls.Flags & (byte)CurveHardwareFlags.InvertX) != 0;
            lsInvY.IsChecked = (ls.Flags & (byte)CurveHardwareFlags.InvertY) != 0;
            rsInvX.IsChecked = (rs.Flags & (byte)CurveHardwareFlags.InvertX) != 0;
            rsInvY.IsChecked = (rs.Flags & (byte)CurveHardwareFlags.InvertY) != 0;
            rangesSummary.Text =
                $"Left Stick: {ls.Inner}-{ls.Outer}%  |  Right Stick: {rs.Inner}-{rs.Outer}%\n" +
                $"Left Trigger: {lt.Inner}-{lt.Outer}%  |  Right Trigger: {rt.Inner}-{rt.Outer}%";

            leftMotorSlider.Value = vibration.SupportsNativeMotors ? vibration.Left : vibration.Strength;
            rightMotorSlider.Value = vibration.SupportsNativeMotors ? vibration.Right : vibration.Strength;
            leftMotorVal.Text = ((int)leftMotorSlider.Value) + "%";
            rightMotorVal.Text = ((int)rightMotorSlider.Value) + "%";

            RefreshTurboList();
        }
        finally
        {
            syncing = false;
        }
    }

    private void RefreshTurboList()
    {
        turboListPanel.Children.Clear();
        var entries = x10.Turbo;
        if (entries.Count == 0)
        {
            turboListPanel.Children.Add(Kit.Text("No turbo keys configured.", 12.5, "DS.TextSoft"));
            return;
        }
        foreach (var entry in entries)
        {
            string key = entry.Key;
            var card = new Border
            {
                CornerRadius = ThemeCatalog.Round(6),
                BorderThickness = new(1),
                BorderBrush = DS.Brush("DS.Hairline"),
                Background = new SolidColorBrush(Color.FromArgb(110, 18, 36, 68)),
                Padding = new(10, 5, 8, 5),
                Margin = new(0, 0, 8, 6),
            };
            var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            row.Children.Add(Kit.Text($"{entry.Key} ({(entry.Automatic ? "Auto" : "Hold")} r{entry.Rate})", 12.5, "DS.Text", FontWeights.SemiBold).Also(t => t.VerticalAlignment = VerticalAlignment.Center));
            var remove = new Button
            {
                Content = "x",
                Width = 22,
                Height = 22,
                Margin = new(8, 0, 0, 0),
                Padding = new(0),
                FontSize = 11.5,
                Style = DS.Style("DS.ConsoleStepBtn"),
                ToolTip = $"Remove turbo on {entry.Key}",
            };
            remove.Click += (_, _) =>
            {
                x10.SetTurbo(key, enabled: false, automatic: false, rate: 10);
                RefreshTurboList();
                onChanged();
            };
            row.Children.Add(remove);
            card.Child = row;
            turboListPanel.Children.Add(card);
        }
    }

    public void SetStatus(string text) => bannerStatus.Text = text;

    public void SetSnapshot(X10HardwareSnapshot snapshot)
    {
        infoReadout.Text = string.Join("   |   ",
            new[]
            {
                snapshot.DeviceName,
                $"FW {snapshot.Version.Firmware}",
                snapshot.Battery is { } b ? $"Battery {b.Percent}%" : "Battery N/A",
                $"Motors L{snapshot.Settings.LeftMotor}% / R{snapshot.Settings.RightMotor}%",
            });
        RefreshFromDrafts();
    }

    public void SetSyncReport(X10SyncReport report)
    {
        string text = report.Status switch
        {
            Krly.VerificationStatus.Matched => "Read-back Matched",
            Krly.VerificationStatus.Mismatch => "Mismatch: " + string.Join(", ", report.DifferingGroups),
            _ => "Unconfirmed",
        };
        SetVerificationVisual(report.Status, text);
        if (report.ReadBack != null)
            SetSnapshot(report.ReadBack);
    }

    private void SetVerificationVisual(Krly.VerificationStatus status, string text)
    {
        verifyBadge.Text = text;
        verifyBadge.Foreground = status switch
        {
            Krly.VerificationStatus.Matched => new SolidColorBrush(Color.FromRgb(110, 231, 183)),
            Krly.VerificationStatus.Mismatch => new SolidColorBrush(Color.FromRgb(251, 146, 60)),
            _ => DS.Brush("DS.TextSoft"),
        };
    }

    private async Task RunBusyAsync(Button trigger, Func<Action<string>, Task> work)
    {
        readButton.IsEnabled = applyButton.IsEnabled = factoryResetAsk.IsEnabled = false;
        try
        {
            await work(msg => bannerStatus.Text = msg);
        }
        finally
        {
            bool enabled = !ReviewSandbox.Active;
            readButton.IsEnabled = applyButton.IsEnabled = factoryResetAsk.IsEnabled = enabled;
        }
    }

    private async Task PulseMotorAsync(bool left)
    {
        int strength = Math.Clamp((int)Math.Round(left ? leftMotorSlider.Value : rightMotorSlider.Value), 0, 100);
        if (strength == 0) strength = 50;
        testLeftBtn.IsEnabled = testRightBtn.IsEnabled = false;
        bannerStatus.Text = $"Pulsing {(left ? "left" : "right")} motor at {strength}%...";
        try
        {
            await X10BleClient.TestMotorAsync(left, strength);
            bannerStatus.Text = $"{(left ? "Left" : "Right")} motor pulse complete.";
        }
        catch (Exception error)
        {
            bannerStatus.Text = "Motor pulse failed: " + error.Message;
        }
        finally
        {
            testLeftBtn.IsEnabled = testRightBtn.IsEnabled = !ReviewSandbox.Active;
        }
    }

    private async Task StopMotorTestAsync()
    {
        try
        {
            await X10BleClient.StopMotorsAsync();
            bannerStatus.Text = "Motors stopped.";
        }
        catch (Exception error)
        {
            bannerStatus.Text = "Stop motors failed: " + error.Message;
        }
    }

    private async Task StartCalibrationAsync(Krly.CalibrationTarget target)
    {
        StopActiveCalibration();
        var cts = new CancellationTokenSource();
        activeCalibrationCts = cts;
        SetCalibrationBusy(true);
        calStatus.Text = target == Krly.CalibrationTarget.Sticks
            ? "Stick calibration started: slowly rotate both sticks in full circles until finished, or press Stop."
            : "Trigger calibration started: slowly pull and release LT and RT fully until finished, or press Stop.";
        try
        {
            var result = await X10BleClient.RunCalibrationAsync(target, ev =>
            {
                Body.Dispatcher.BeginInvoke(() =>
                {
                    calStatus.Text = ev.Phase switch
                    {
                        Krly.CalibrationPhase.Active => $"Calibrating {target} (in progress, code {ev.Detail})...",
                        Krly.CalibrationPhase.Retry => $"Please repeat full {target} movement slowly (code {ev.Detail})...",
                        Krly.CalibrationPhase.Succeeded => $"{target} calibration succeeded (code {ev.Detail}).",
                        _ => $"{target} calibration event {ev.Phase} ({ev.Detail}).",
                    };
                });
            }, cts.Token);
            calStatus.Text = $"{target} calibration finished cleanly (code {result.Detail}).";
        }
        catch (OperationCanceledException)
        {
            calStatus.Text = $"{target} calibration stopped.";
        }
        catch (Exception error)
        {
            calStatus.Text = $"{target} calibration failed: {error.Message}";
        }
        finally
        {
            if (ReferenceEquals(activeCalibrationCts, cts)) activeCalibrationCts = null;
            cts.Dispose();
            SetCalibrationBusy(false);
        }
    }

    private async Task StartSelfCalibrationAsync()
    {
        StopActiveCalibration();
        var cts = new CancellationTokenSource();
        activeCalibrationCts = cts;
        SetCalibrationBusy(true);
        calStatus.Text = "Self-calibration active: keep the controller still on a flat surface...";
        try
        {
            bool ok = await X10BleClient.RunSelfCalibrationAsync(cts.Token);
            calStatus.Text = ok ? "Full self-calibration succeeded." : "Full self-calibration reported failure.";
        }
        catch (OperationCanceledException)
        {
            calStatus.Text = "Self-calibration stopped.";
        }
        catch (Exception error)
        {
            calStatus.Text = "Self-calibration failed: " + error.Message;
        }
        finally
        {
            if (ReferenceEquals(activeCalibrationCts, cts)) activeCalibrationCts = null;
            cts.Dispose();
            SetCalibrationBusy(false);
        }
    }

    public void StopActiveCalibration()
    {
        try { activeCalibrationCts?.Cancel(); }
        catch { }
    }

    private void SetCalibrationBusy(bool busy)
    {
        bool idle = !busy && !ReviewSandbox.Active;
        calSticksBtn.IsEnabled = calTriggersBtn.IsEnabled = calSelfBtn.IsEnabled = idle;
        calStopBtn.IsEnabled = busy && !ReviewSandbox.Active;
    }
}
