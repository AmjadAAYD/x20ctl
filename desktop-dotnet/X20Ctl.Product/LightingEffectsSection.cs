using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using X20Ctl.Zone.Lighting;

namespace X20Ctl.Product;

/// <summary>
/// Theme-native Effects and Auto-Profile tabs hosted inside <see cref="LightingWindow"/>'s right column.
/// Avoids inline <see cref="TextBox"/> / <see cref="ComboBox"/> controls and avoids buttons prefixed with "Apply"
/// so the studio footer owns the single primary Apply action and UI review contracts stay exact.
/// </summary>
internal sealed class LightingEffectsSection
{
    private readonly Func<int> getPickerRgb;
    private readonly Action<int> setPickerRgb;
    private readonly Func<byte> getPickerBrightness;
    private readonly Action<Button>? registerFocus;
    private readonly Action? visualChanged;

    private readonly WrapPanel palettePanel = new() { Margin = new(0, 4, 0, 8) };
    private readonly StackPanel savedPresetsList = new() { Margin = new(0, 4, 0, 8) };
    private readonly StackPanel builtInPresetsList = new() { Margin = new(0, 4, 0, 4) };
    private readonly WrapPanel runningAppsPanel = new() { Margin = new(0, 4, 0, 8) };
    private readonly StackPanel gameRulesList = new() { Margin = new(0, 4, 0, 4) };

    private readonly Slider maxBrightSlider = new() { Minimum = 1, Maximum = 255, Value = 224, SmallChange = 1, LargeChange = 16, TickFrequency = 1, IsSnapToTickEnabled = true, Style = DS.Style("DS.Slider"), Margin = new(0, 2, 0, 8) };
    private readonly Slider periodSlider = new() { Minimum = 800, Maximum = 15000, Value = 4200, SmallChange = 200, LargeChange = 1000, TickFrequency = 100, IsSnapToTickEnabled = true, Style = DS.Style("DS.Slider"), Margin = new(0, 2, 0, 8) };
    private readonly Slider minBrightSlider = new() { Minimum = 0, Maximum = 255, Value = 1, SmallChange = 1, LargeChange = 16, TickFrequency = 1, IsSnapToTickEnabled = true, Style = DS.Style("DS.Slider"), Margin = new(0, 2, 0, 6) };
    private readonly Slider intervalSlider = new() { Minimum = 60, Maximum = 1500, Value = 80, SmallChange = 10, LargeChange = 50, TickFrequency = 10, IsSnapToTickEnabled = true, Style = DS.Style("DS.Slider"), Margin = new(0, 2, 0, 8) };
    private readonly Slider idleSlider = new() { Minimum = 5, Maximum = 300, Value = 60, SmallChange = 5, LargeChange = 15, TickFrequency = 5, IsSnapToTickEnabled = true, Style = DS.Style("DS.Slider"), Margin = new(0, 2, 0, 6) };
    private readonly Slider audioSensSlider = new() { Minimum = 10, Maximum = 100, Value = 65, SmallChange = 5, LargeChange = 15, TickFrequency = 5, IsSnapToTickEnabled = true, Style = DS.Style("DS.Slider"), Margin = new(0, 2, 0, 8) };

    private readonly TextBlock maxBrightLabel = Kit.Text("", 13, "DS.TextSoft");
    private readonly TextBlock periodLabel = Kit.Text("", 13, "DS.TextSoft");
    private readonly TextBlock minBrightLabel = Kit.Text("", 12.5, "DS.TextSoft");
    private readonly TextBlock intervalLabel = Kit.Text("", 12.5, "DS.TextSoft");
    private readonly TextBlock idleLabel = Kit.Text("", 12.5, "DS.TextSoft");
    private readonly TextBlock audioSensLabel = Kit.Text("", 12.5, "DS.TextSoft");
    private readonly TextBlock selectedGameRuleHint = Kit.Text("", 13, "DS.AccentHi").Also(t => { t.TextWrapping = TextWrapping.Wrap; t.Margin = new(0, 0, 0, 6); });

    private readonly Dictionary<LightingEffectKind, Button> kindButtons = [];
    private readonly Dictionary<int, Button> capButtons = [];
    private readonly Dictionary<AudioReactiveStyle, Button> audioStyleButtons = [];
    private readonly Dictionary<AudioHapticsMode, Button> audioHapticsModeButtons = [];
    private readonly Dictionary<int, Button> audioHapticsButtons = [];
    private readonly List<string> palette = ["#00FFFF", "#FF00AA", "#3A6CFF"];

    private Border? audioStudioPanel;
    private ScaleTransform? bassScale, midScale, trebleScale, vocalScale;
    private readonly ScaleTransform?[] spectrumScales = new ScaleTransform?[8];
    private ScaleTransform? lgScale, rgScale, ltScale, rtScale;
    private Button? roleLgBtn, roleRgBtn, roleLtBtn, roleRtBtn, roleResetBtn;
    private Border? beatBadge;
    private static readonly SolidColorBrush BeatActiveBrush = CreateFrozenBrush(220, 0, 229, 255);
    private static readonly SolidColorBrush BeatIdleBrush = CreateFrozenBrush(40, 0, 229, 255);
    private TextBlock? vibeBadgeText;

    private static SolidColorBrush CreateFrozenBrush(byte a, byte r, byte g, byte b)
    {
        var br = new SolidColorBrush(Color.FromArgb(a, r, g, b));
        br.Freeze();
        return br;
    }
    private Button? armTriggersButton;
    private Button? test4MotorButton;
    private TextBlock? triggerArmStatusText;
    private string? testStatusText;
    private long testStatusUntilTick;
    private readonly DispatcherTimer audioUiTimer = new() { Interval = TimeSpan.FromMilliseconds(60) };
    private MotorRoleMap selectedMotorRoles = MotorRoleMap.Default;

    private static readonly LightingEffectKind[] GridKinds =
    [
        LightingEffectKind.SmoothHueWave,
        LightingEffectKind.PaletteFade,
        LightingEffectKind.PaletteStep,
        LightingEffectKind.MoonlightPulse,
        LightingEffectKind.EmberShimmer,
        LightingEffectKind.DualStrobe,
        LightingEffectKind.TriggerGlow,
        LightingEffectKind.StickHeat,
        LightingEffectKind.ButtonFlash,
        LightingEffectKind.IdleSleepDim,
    ];

    private static readonly (int Cap, string Label)[] WriteCapChoices =
    [
        (0, "Unlimited"),
        (50, "50"),
        (150, "150"),
        (400, "400"),
        (1000, "1000"),
    ];

    private static readonly (AudioReactiveStyle Style, string Label)[] AudioStyleChoices =
    [
        (AudioReactiveStyle.AutoAdaptive, "Auto"),
        (AudioReactiveStyle.SpectrumFlow, "Spectrum"),
        (AudioReactiveStyle.BeatShift, "Beat Shift"),
        (AudioReactiveStyle.BassFire, "Bass Fire"),
        (AudioReactiveStyle.AuroraDrift, "Aurora"),
        (AudioReactiveStyle.DropStrobe, "Drop Strobe"),
    ];

    private static readonly (AudioHapticsMode Mode, string Label)[] AudioHapticsModeChoices =
    [
        (AudioHapticsMode.AutoAdaptive, "Auto"),
        (AudioHapticsMode.LyricalMusical, "Lyrical"),
        (AudioHapticsMode.QuadConcert, "4D Concert"),
        (AudioHapticsMode.TriggerPulse, "Trigger Pulse"),
        (AudioHapticsMode.StereoWave, "Stereo Wave"),
        (AudioHapticsMode.SubwooferDrop, "Sub Drop"),
        (AudioHapticsMode.Heartbeat, "Heartbeat"),
    ];

    private static readonly (int Strength, string Label)[] AudioHapticsChoices =
    [
        (0, "Off"),
        (35, "Light 35%"),
        (65, "Punch 65%"),
        (100, "Max 100%"),
    ];

    private LightingEffectKind selectedKind = LightingEffectKind.SmoothHueWave;
    private int selectedWriteCap;
    private AudioReactiveStyle selectedAudioStyle = AudioReactiveStyle.AutoAdaptive;
    private AudioHapticsMode selectedAudioHapticsMode = AudioHapticsMode.AutoAdaptive;
    private int selectedAudioHaptics;
    private string selectedRulePresetId = "builtin-hue-sweep";
    private bool syncing;

    public FrameworkElement EffectsView { get; }
    public FrameworkElement AutoView { get; }

    public LightingEffectsSection(
        Func<int> getPickerRgb,
        Action<int> setPickerRgb,
        Func<byte> getPickerBrightness,
        Action<Button>? registerFocus = null,
        Action? visualChanged = null)
    {
        this.getPickerRgb = getPickerRgb;
        this.setPickerRgb = setPickerRgb;
        this.getPickerBrightness = getPickerBrightness;
        this.registerFocus = registerFocus;
        this.visualChanged = visualChanged;

        EffectsView = BuildEffectsView();
        AutoView = BuildAutoView();

        LoadFromSpec(LightingEffectsService.CurrentSpec);
        RefreshSavedPresets();
        RefreshRunningApps();
        RefreshGameRules();
    }

    public LightingEffectSpec BuildCurrentEffectSpec() => new LightingEffectSpec
    {
        Kind = selectedKind,
        Palette = palette.Count > 0 ? [.. palette] : [$"#{getPickerRgb() & 0xffffff:X6}"],
        MinBrightness = (byte)Math.Clamp((int)Math.Round(minBrightSlider.Value), 0, 255),
        MaxBrightness = (byte)Math.Clamp((int)Math.Round(maxBrightSlider.Value), 1, 255),
        StepIntervalMs = (int)Math.Round(intervalSlider.Value),
        CyclePeriodMs = (int)Math.Round(periodSlider.Value),
        IdleTimeoutSeconds = (int)Math.Round(idleSlider.Value),
        MaxSessionWrites = selectedWriteCap,
        AudioSensitivity = (int)Math.Round(audioSensSlider.Value),
        AudioStyle = selectedAudioStyle,
        AudioHapticsStrength = selectedAudioHaptics,
        AudioHapticsStyle = selectedAudioHapticsMode,
        AudioMotorRoles = selectedMotorRoles,
        RestoreOnStop = true,
    }.Normalized();

    public LightingEffectSpec BuildGameAutoSpec() => new LightingEffectSpec
    {
        Kind = LightingEffectKind.GameAutoProfile,
        Palette = [$"#{getPickerRgb() & 0xffffff:X6}"],
        MaxBrightness = getPickerBrightness(),
    }.Normalized();

    public LightingEffectKind SelectedKind => selectedKind;

    public void LoadFromSpec(LightingEffectSpec raw)
    {
        var spec = raw.Normalized();
        syncing = true;
        try
        {
            if (spec.Kind == LightingEffectKind.ScreenAmbient || spec.Kind == LightingEffectKind.AudioReactive || Array.IndexOf(GridKinds, spec.Kind) >= 0)
                selectedKind = spec.Kind;
            else
                selectedKind = LightingEffectKind.SmoothHueWave;
            selectedWriteCap = Array.Exists(WriteCapChoices, c => c.Cap == spec.MaxSessionWrites) ? spec.MaxSessionWrites : 0;
            selectedAudioStyle = spec.AudioStyle;
            selectedAudioHapticsMode = spec.AudioHapticsStyle;
            selectedAudioHaptics = Array.Exists(AudioHapticsChoices, c => c.Strength == spec.AudioHapticsStrength) ? spec.AudioHapticsStrength : 0;
            selectedMotorRoles = spec.AudioMotorRoles;
            palette.Clear();
            palette.AddRange(spec.Palette);
            minBrightSlider.Value = spec.MinBrightness;
            maxBrightSlider.Value = spec.MaxBrightness;
            intervalSlider.Value = spec.StepIntervalMs;
            periodSlider.Value = spec.CyclePeriodMs;
            idleSlider.Value = spec.IdleTimeoutSeconds;
            audioSensSlider.Value = spec.AudioSensitivity;
        }
        finally
        {
            syncing = false;
        }
        UpdateSelectionButtons();
        RenderPaletteSwatches();
        UpdateSliderLabels();
    }

    private FrameworkElement BuildEffectsView()
    {
        var stack = new StackPanel();
        stack.Children.Add(Kit.Section("LIVE EFFECTS", 0));

        Button MakeEffectTile(LightingEffectKind kind, bool hero = false)
        {
            var title = new TextBlock
            {
                Text = ShortEffectName(kind),
                FontFamily = DS.Display,
                FontSize = hero ? 14 : 13.5,
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            var sub = new TextBlock
            {
                Text = EffectSubtitle(kind),
                FontFamily = DS.Body,
                FontSize = 11.5,
                Opacity = 0.74,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new(0, 1, 0, 0),
            };
            var content = new StackPanel { Margin = new(6, 0, 0, 0), Children = { title, sub } };
            var btn = new Button
            {
                Content = content,
                Style = DS.Style("DS.ConsoleCard"),
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Padding = hero ? new Thickness(12, 10, 12, 10) : new Thickness(11, 8, 11, 8),
                Margin = new Thickness(3),
                MinHeight = hero ? 52 : 48,
                Height = double.NaN,
            };
            btn.Click += async (_, _) =>
            {
                selectedKind = kind;
                if (kind == LightingEffectKind.ScreenAmbient && intervalSlider.Value > 120)
                    intervalSlider.Value = 70;
                else if (kind == LightingEffectKind.AudioReactive)
                {
                    if (intervalSlider.Value > 120) intervalSlider.Value = 65;
                    // Upgrade single-colour solid red drafts into a vivid multi-stop Audio concert palette.
                    if (palette.Count <= 1)
                    {
                        palette.Clear();
                        palette.AddRange(["#FF0040", "#00F5D4", "#3D8BFF", "#FFB800", "#A855F7"]);
                        RenderPaletteSwatches();
                    }
                }
                UpdateSelectionButtons();
                UpdateSliderLabels();
                SyncPreviewDraft();
                await LightingEffectsService.ApplyAsync(BuildCurrentEffectSpec());
                if (!ReviewSandbox.Active &&
                    kind == LightingEffectKind.AudioReactive &&
                    selectedAudioHaptics > 0 &&
                    Diagnostics.BrowserTriggerRumble.Shared is { Armed: true } shared)
                {
                    shared.EnsureFocusForAudioTriggers();
                }
            };
            registerFocus?.Invoke(btn);
            kindButtons[kind] = btn;
            return btn;
        }

        var heroGrid = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2, Margin = new(-3, 0, -3, 3) };
        heroGrid.Children.Add(MakeEffectTile(LightingEffectKind.ScreenAmbient, hero: true));
        heroGrid.Children.Add(MakeEffectTile(LightingEffectKind.AudioReactive, hero: true));
        stack.Children.Add(heroGrid);

        audioStudioPanel = BuildAudioStudioPanel();
        stack.Children.Add(audioStudioPanel);
        audioUiTimer.Tick += (_, _) => RefreshLiveAudioMeters();
        void OnTriggerArmChanged()
        {
            stack.Dispatcher.BeginInvoke(() =>
            {
                if (Diagnostics.BrowserTriggerRumble.Shared is { Armed: true })
                    testStatusUntilTick = 0;
                RefreshTriggerArmState();
            });
        }
        void OnServiceChanged()
        {
            stack.Dispatcher.BeginInvoke(() =>
            {
                var svcSpec = LightingEffectsService.CurrentSpec;
                if (svcSpec.AudioMotorRoles != selectedMotorRoles || svcSpec.AudioHapticsStrength != selectedAudioHaptics)
                {
                    syncing = true;
                    selectedMotorRoles = svcSpec.AudioMotorRoles;
                    selectedAudioHaptics = Array.Exists(AudioHapticsChoices, c => c.Strength == svcSpec.AudioHapticsStrength) ? svcSpec.AudioHapticsStrength : selectedAudioHaptics;
                    syncing = false;
                    UpdateSelectionButtons();
                }
            });
        }
        stack.Loaded += (_, _) =>
        {
            LightingEffectsService.Changed -= OnServiceChanged;
            LightingEffectsService.Changed += OnServiceChanged;
            OnServiceChanged();
            if (Diagnostics.BrowserTriggerRumble.Shared is { } shared)
            {
                shared.ArmedChanged -= OnTriggerArmChanged;
                shared.ArmedChanged += OnTriggerArmChanged;
                shared.StateChanged -= OnTriggerArmChanged;
                shared.StateChanged += OnTriggerArmChanged;
                _ = shared.Ready();
            }
            RefreshTriggerArmState();
            SyncAudioTimer();
        };
        stack.Unloaded += (_, _) =>
        {
            audioUiTimer.Stop();
            LightingEffectsService.Changed -= OnServiceChanged;
            if (Diagnostics.BrowserTriggerRumble.Shared is { } shared)
            {
                shared.ArmedChanged -= OnTriggerArmChanged;
                shared.StateChanged -= OnTriggerArmChanged;
                shared.CancelListen();
            }
            if (!LightingEffectsService.Status.IsRunning)
                SystemAudioLevelSampler.Reset();
        };

        var grid = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2, Margin = new(-3, 0, -3, 12) };
        foreach (var kind in GridKinds) grid.Children.Add(MakeEffectTile(kind));
        stack.Children.Add(grid);

        stack.Children.Add(Kit.Text("Effect colours", 13.5, "DS.Text", FontWeights.SemiBold));
        stack.Children.Add(palettePanel);

        var paletteActions = new WrapPanel { Margin = new(0, 0, 0, 12) };
        paletteActions.Children.Add(SmallButton("+ Add colour", () =>
        {
            if (palette.Count >= 8) return;
            palette.Add($"#{getPickerRgb() & 0xffffff:X6}");
            RenderPaletteSwatches();
            SyncPreviewDraft();
        }).Also(b => b.Margin = new(0, 0, 6, 6)));

        foreach (var (name, stops) in new (string, string[])[]
        {
            ("Cyberpunk", ["#00FFFF", "#FF00FF", "#3A6CFF"]),
            ("Fire", ["#FF2200", "#FF8C00", "#FFB800"]),
            ("Police", ["#FF0020", "#0055FF"]),
            ("Aurora", ["#00FF9D", "#00B8FF", "#9D4EDD"]),
        })
        {
            paletteActions.Children.Add(SmallButton(name, async () =>
            {
                palette.Clear();
                palette.AddRange(stops);
                RenderPaletteSwatches();
                SyncPreviewDraft();
                await ApplyOrHotUpdateAudioAsync();
            }).Also(b => b.Margin = new(0, 0, 6, 6)));
        }
        stack.Children.Add(paletteActions);

        minBrightSlider.ValueChanged += (_, _) =>
        {
            if (minBrightSlider.Value > maxBrightSlider.Value && !syncing) maxBrightSlider.Value = Math.Max(1, minBrightSlider.Value);
            UpdateSliderLabels();
            SyncPreviewDraft();
            if (!syncing && selectedKind == LightingEffectKind.AudioReactive && LightingEffectsService.Status.IsRunning)
                LightingEffectsService.UpdateRunningSpec(BuildCurrentEffectSpec(), remember: false);
        };
        maxBrightSlider.ValueChanged += (_, _) =>
        {
            if (maxBrightSlider.Value < minBrightSlider.Value && !syncing) minBrightSlider.Value = maxBrightSlider.Value;
            UpdateSliderLabels();
            SyncPreviewDraft();
            if (!syncing && selectedKind == LightingEffectKind.AudioReactive && LightingEffectsService.Status.IsRunning)
                LightingEffectsService.UpdateRunningSpec(BuildCurrentEffectSpec(), remember: false);
        };
        intervalSlider.ValueChanged += (_, _) => UpdateSliderLabels();
        periodSlider.ValueChanged += (_, _) => { UpdateSliderLabels(); SyncPreviewDraft(); };
        idleSlider.ValueChanged += (_, _) => UpdateSliderLabels();
        audioSensSlider.ValueChanged += (_, _) =>
        {
            UpdateSliderLabels();
            SyncPreviewDraft();
            if (!syncing && selectedKind == LightingEffectKind.AudioReactive && LightingEffectsService.Status.IsRunning)
                LightingEffectsService.UpdateRunningSpec(BuildCurrentEffectSpec(), remember: false);
        };
        audioSensSlider.LostMouseCapture += (_, _) =>
        {
            if (!syncing && selectedKind == LightingEffectKind.AudioReactive && LightingEffectsService.Status.IsRunning)
                LightingEffectsService.UpdateRunningSpec(BuildCurrentEffectSpec(), remember: true);
        };

        stack.Children.Add(maxBrightLabel);
        stack.Children.Add(maxBrightSlider);
        stack.Children.Add(periodLabel);
        stack.Children.Add(periodSlider);

        var advanced = new StackPanel { Margin = new(0, 6, 0, 4) };
        advanced.Children.Add(minBrightLabel);
        advanced.Children.Add(minBrightSlider);
        var floorChoices = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 0, 0, 8) };
        floorChoices.Children.Add(SmallButton("Moonlight (1%)", () => minBrightSlider.Value = LightingRecord.MoonlightBrightnessByte).Also(b => b.Margin = new(0, 0, 6, 0)));
        floorChoices.Children.Add(SmallButton("Off floor (0%)", () => minBrightSlider.Value = LightingRecord.MinBrightnessByte));
        advanced.Children.Add(floorChoices);

        advanced.Children.Add(intervalLabel);
        advanced.Children.Add(intervalSlider);
        advanced.Children.Add(idleLabel);
        advanced.Children.Add(idleSlider);
        advanced.Children.Add(Kit.Text("Session write cap", 12.5, "DS.TextSoft").Also(t => t.Margin = new(0, 2, 0, 4)));

        var capsPanel = new WrapPanel { Margin = new(-2, 0, -2, 0) };
        foreach (var (cap, label) in WriteCapChoices)
        {
            int captured = cap;
            var btn = new Button
            {
                Content = label,
                Style = DS.Style("DS.ConsoleBtn"),
                Padding = new(10, 5, 10, 5),
                Margin = new(2),
                FontSize = 11.5,
                MinHeight = 30,
                Height = double.NaN,
            };
            btn.Click += (_, _) => { selectedWriteCap = captured; UpdateSelectionButtons(); };
            registerFocus?.Invoke(btn);
            capButtons[captured] = btn;
            capsPanel.Children.Add(btn);
        }
        advanced.Children.Add(capsPanel);

        stack.Children.Add(new Expander
        {
            Header = Kit.Text("Advanced timing & floor", 13, "DS.TextSoft"),
            Foreground = DS.Brush("DS.TextSoft"),
            Margin = new(0, 4, 0, 0),
            Content = advanced,
        });

        return stack;
    }

    private FrameworkElement BuildAutoView()
    {
        var stack = new StackPanel();
        stack.Children.Add(Kit.Section("SAVED PRESETS", 0));

        var saveRow = new WrapPanel { Margin = new(0, 0, 0, 8) };
        saveRow.Children.Add(SmallButton("+ Save Effect…", () => SaveWithPrompt(asSolid: false)).Also(b => b.Margin = new(0, 0, 6, 6)));
        saveRow.Children.Add(SmallButton("+ Quick Save", () => SaveQuick(asSolid: false)).Also(b => b.Margin = new(0, 0, 6, 6)));
        saveRow.Children.Add(SmallButton("+ Save Colour", () => SaveQuick(asSolid: true)).Also(b => b.Margin = new(0, 0, 6, 6)));
        stack.Children.Add(saveRow);

        stack.Children.Add(savedPresetsList);
        stack.Children.Add(Kit.Text("Built-in presets", 13, "DS.TextSoft").Also(t => t.Margin = new(0, 4, 0, 0)));
        stack.Children.Add(builtInPresetsList);

        stack.Children.Add(Kit.Section("GAME AUTO-SWITCH", 10));
        stack.Children.Add(selectedGameRuleHint);

        var gameActions = new WrapPanel { Margin = new(0, 0, 0, 6) };
        gameActions.Children.Add(SmallButton("+ Add .exe…", BindCustomExeWithPrompt).Also(b => b.Margin = new(0, 0, 6, 6)));
        gameActions.Children.Add(SmallButton("Refresh apps", RefreshRunningApps).Also(b => b.Margin = new(0, 0, 6, 6)));
        gameActions.Children.Add(SmallButton("Enable Game Auto", async () => await LightingEffectsService.ApplyAsync(BuildGameAutoSpec())).Also(b => b.Margin = new(0, 0, 0, 6)));
        stack.Children.Add(gameActions);

        stack.Children.Add(runningAppsPanel);
        stack.Children.Add(Kit.Text("Active app rules", 13, "DS.TextSoft").Also(t => t.Margin = new(0, 4, 0, 0)));
        stack.Children.Add(gameRulesList);

        return stack;
    }

    internal void PreviewCurrentDraftIfIdle()
    {
        if (!LightingEffectsService.Status.IsRunning)
            LightingEffectsService.SetPreviewDraft(BuildCurrentEffectSpec());
    }

    private void SyncPreviewDraft()
    {
        if (!syncing)
            LightingEffectsService.SetPreviewDraft(BuildCurrentEffectSpec());
        visualChanged?.Invoke();
    }

    private void UpdateSelectionButtons()
    {
        foreach (var (kind, btn) in kindButtons)
            LightingWindow.SetSelectedState(btn, kind == selectedKind);
        foreach (var (cap, btn) in capButtons)
            LightingWindow.SetSelectedState(btn, cap == selectedWriteCap);
        foreach (var (st, btn) in audioStyleButtons)
            LightingWindow.SetSelectedState(btn, st == selectedAudioStyle);
        foreach (var (hm, btn) in audioHapticsModeButtons)
            LightingWindow.SetSelectedState(btn, hm == selectedAudioHapticsMode);
        foreach (var (h, btn) in audioHapticsButtons)
            LightingWindow.SetSelectedState(btn, h == selectedAudioHaptics);
        UpdateRoleButtons();
        if (audioStudioPanel != null)
            audioStudioPanel.Visibility = selectedKind == LightingEffectKind.AudioReactive ? Visibility.Visible : Visibility.Collapsed;
        RefreshTriggerArmState();
        SyncAudioTimer();
    }

    private void UpdateRoleButtons()
    {
        void SetBtn(Button? b, string motorTag, MotorAudioRole role)
        {
            if (b == null) return;
            b.Content = $"{motorTag}: {AudioHapticsEngine.RoleShortLabel(role)}";
            LightingWindow.SetSelectedState(b, role != MotorAudioRole.Auto);
        }
        SetBtn(roleLgBtn, "LG", selectedMotorRoles.LeftGrip);
        SetBtn(roleRgBtn, "RG", selectedMotorRoles.RightGrip);
        SetBtn(roleLtBtn, "LT", selectedMotorRoles.LeftTrigger);
        SetBtn(roleRtBtn, "RT", selectedMotorRoles.RightTrigger);
        if (roleResetBtn != null)
            roleResetBtn.IsEnabled = !selectedMotorRoles.IsAllAuto;
    }

    private Border BuildAudioStudioPanel()
    {
        var box = new Border
        {
            CornerRadius = ThemeCatalog.Round(8),
            BorderBrush = DS.Brush("DS.Hairline"),
            BorderThickness = new(1),
            Padding = new(12, 10, 12, 10),
            Margin = new(0, 4, 0, 10),
            Background = new SolidColorBrush(ThemeCatalog.Alpha(ThemeCatalog.Current.Night, 195)),
            Visibility = Visibility.Collapsed,
        };
        var body = new StackPanel();

        var headRow = new DockPanel { Margin = new(0, 0, 0, 8) };
        beatBadge = new Border
        {
            CornerRadius = ThemeCatalog.Round(4),
            Padding = new(7, 2, 7, 2),
            Background = new SolidColorBrush(Color.FromArgb(40, 0, 229, 255)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(110, 0, 229, 255)),
            BorderThickness = new(1),
            Child = Kit.Text("BEAT", 10.5, "DS.Text", FontWeights.Bold),
        };
        DockPanel.SetDock(beatBadge, Dock.Right);
        headRow.Children.Add(beatBadge);

        vibeBadgeText = Kit.Text("IDLE", 10.5, "DS.AccentHi", FontWeights.Bold);
        var vibeBadge = new Border
        {
            CornerRadius = ThemeCatalog.Round(4),
            Padding = new(7, 2, 7, 2),
            Margin = new(0, 0, 6, 0),
            Background = new SolidColorBrush(Color.FromArgb(36, 167, 139, 250)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(110, 167, 139, 250)),
            BorderThickness = new(1),
            Child = vibeBadgeText,
        };
        DockPanel.SetDock(vibeBadge, Dock.Right);
        headRow.Children.Add(vibeBadge);
        headRow.Children.Add(Kit.Text("AUDIO STUDIO · 1024-PT FFT", 12, "DS.AccentHi", FontWeights.SemiBold));
        body.Children.Add(headRow);

        // Live 8-band real-time FFT spectrum strip (Sub..Air)
        string[] bandLabels = ["SUB", "KCK", "LMD", "VX1", "VX2", "PRS", "TRB", "AIR"];
        Color[] bandColors =
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
                Height = 28,
                CornerRadius = new(3),
                ClipToBounds = true,
                Background = new SolidColorBrush(Color.FromArgb(90, 24, 34, 54)),
                BorderBrush = DS.Brush("DS.Hairline"),
                BorderThickness = new(0.5),
            };
            var barScale = new ScaleTransform(1.0, 0.05);
            var fillBar = new Border
            {
                Background = new SolidColorBrush(bandColors[b]),
                CornerRadius = new(2),
                VerticalAlignment = VerticalAlignment.Stretch,
                RenderTransformOrigin = new(0.5, 1.0),
                RenderTransform = barScale,
            };
            barBox.Child = fillBar;
            colSp.Children.Add(barBox);
            colSp.Children.Add(new TextBlock
            {
                Text = bandLabels[b],
                FontFamily = LightingWindow.MonoFont,
                FontSize = 8.5,
                Foreground = DS.Brush("DS.TextMuted"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new(0, 2, 0, 0),
            });
            spectrumScales[b] = barScale;
            eqGrid.Children.Add(colSp);
        }
        body.Children.Add(eqGrid);

        (ScaleTransform scale, Grid row) MakeBandMeter(string label, Color fillColor)
        {
            var g = new Grid { Margin = new(0, 0, 0, 5) };
            g.ColumnDefinitions.Add(new() { Width = new(54) });
            g.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });

            g.Children.Add(new TextBlock
            {
                Text = label,
                FontFamily = LightingWindow.MonoFont,
                FontSize = 10.5,
                Foreground = DS.Brush("DS.TextSoft"),
                VerticalAlignment = VerticalAlignment.Center,
            });

            var sTransform = new ScaleTransform(0.04, 1.0);
            var fillBar = new Border
            {
                Background = new SolidColorBrush(fillColor),
                CornerRadius = new(3),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                RenderTransformOrigin = new(0.0, 0.5),
                RenderTransform = sTransform,
            };

            var track = new Border
            {
                Height = 8,
                ClipToBounds = true,
                VerticalAlignment = VerticalAlignment.Center,
                CornerRadius = new(3),
                Background = new SolidColorBrush(Color.FromArgb(90, 24, 34, 54)),
                BorderBrush = DS.Brush("DS.Hairline"),
                BorderThickness = new(0.5),
                Child = fillBar,
            };
            Grid.SetColumn(track, 1);
            g.Children.Add(track);
            return (sTransform, g);
        }

        (bassScale, var bassRow) = MakeBandMeter("BASS", Color.FromRgb(0xFF, 0x33, 0x77));
        (midScale, var midRow) = MakeBandMeter("MID", Color.FromRgb(0x00, 0xE5, 0xFF));
        (trebleScale, var trebleRow) = MakeBandMeter("TREBLE", Color.FromRgb(0xA7, 0x8B, 0xFA));
        (vocalScale, var vocalRow) = MakeBandMeter("VOCAL", Color.FromRgb(0xFB, 0xBF, 0x24));
        body.Children.Add(bassRow);
        body.Children.Add(midRow);
        body.Children.Add(trebleRow);
        body.Children.Add(vocalRow);

        body.Children.Add(Kit.Text("Reaction style", 12.5, "DS.TextSoft").Also(t => t.Margin = new(0, 4, 0, 5)));
        var styleWrap = new WrapPanel { Margin = new(-2, 0, -2, 8) };
        foreach (var (styleVal, label) in AudioStyleChoices)
        {
            var captured = styleVal;
            var btn = new Button
            {
                Content = label,
                Style = DS.Style("DS.ConsoleBtn"),
                Padding = new(10, 5, 10, 5),
                Margin = new(2),
                FontSize = 11.5,
                MinHeight = 30,
                Height = double.NaN,
            };
            btn.Click += async (_, _) =>
            {
                selectedAudioStyle = captured;
                if (captured == AudioReactiveStyle.BeatShift && palette.Count < 2)
                {
                    palette.Clear();
                    palette.AddRange(["#00FFFF", "#FF00AA", "#FFAA00", "#00FF9D", "#7C5CFF"]);
                    RenderPaletteSwatches();
                }
                UpdateSelectionButtons();
                SyncPreviewDraft();
                await ApplyOrHotUpdateAudioAsync();
            };
            registerFocus?.Invoke(btn);
            audioStyleButtons[captured] = btn;
            styleWrap.Children.Add(btn);
        }
        body.Children.Add(styleWrap);

        body.Children.Add(audioSensLabel);
        body.Children.Add(audioSensSlider);

        body.Children.Add(Kit.Text("4-Motor Haptics (Grips + Triggers)", 12.5, "DS.TextSoft").Also(t => t.Margin = new(0, 0, 0, 5)));
        var hapPanel = new WrapPanel { Margin = new(-2, 0, -2, 6) };
        foreach (var (strength, label) in AudioHapticsChoices)
        {
            int captured = strength;
            var btn = new Button
            {
                Content = label,
                Style = DS.Style("DS.ConsoleBtn"),
                Padding = new(10, 5, 10, 5),
                Margin = new(2),
                FontSize = 11.5,
                MinHeight = 30,
                Height = double.NaN,
            };
            btn.Click += async (_, _) =>
            {
                bool enabling = captured > 0 && selectedAudioHaptics == 0;
                selectedAudioHaptics = captured;
                UpdateSelectionButtons();
                SyncPreviewDraft();
                if (!ReviewSandbox.Active && (enabling || (captured > 0 && !TriggerEnableGuideDialog.ShownThisSession)))
                {
                    TriggerEnableGuideDialog.Show(Window.GetWindow(EffectsView), force: false);
                    if (Diagnostics.BrowserTriggerRumble.Shared is { Armed: false, Failure: null } shared)
                        await shared.Listen();
                }
                await ApplyOrHotUpdateAudioAsync(autoStartIfStopped: captured > 0);
            };
            registerFocus?.Invoke(btn);
            audioHapticsButtons[captured] = btn;
            hapPanel.Children.Add(btn);
        }
        body.Children.Add(hapPanel);

        var hapModesWrap = new WrapPanel { Margin = new(-2, 0, -2, 8) };
        foreach (var (modeVal, label) in AudioHapticsModeChoices)
        {
            var captured = modeVal;
            var btn = new Button
            {
                Content = label,
                Style = DS.Style("DS.ConsoleBtn"),
                Padding = new(9, 4, 9, 4),
                Margin = new(2),
                FontSize = 11,
                MinHeight = 28,
                Height = double.NaN,
            };
            btn.Click += async (_, _) =>
            {
                selectedAudioHapticsMode = captured;
                UpdateSelectionButtons();
                SyncPreviewDraft();
                await ApplyOrHotUpdateAudioAsync();
            };
            registerFocus?.Invoke(btn);
            audioHapticsModeButtons[captured] = btn;
            hapModesWrap.Children.Add(btn);
        }
        body.Children.Add(hapModesWrap);

        // Live 4-motor output telemetry (LG, RG, LT, RT) using GPU transforms
        var motorsRow = new System.Windows.Controls.Primitives.UniformGrid { Columns = 4, Margin = new(-2, 0, -2, 8) };
        (ScaleTransform scale, FrameworkElement cell) MakeMotorCell(string label, Color col)
        {
            var sp = new StackPanel { Margin = new(2, 0, 2, 0) };
            sp.Children.Add(new TextBlock
            {
                Text = label,
                FontFamily = LightingWindow.MonoFont,
                FontSize = 10,
                Foreground = DS.Brush("DS.TextMuted"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new(0, 0, 0, 2),
            });
            var mScale = new ScaleTransform(0.04, 1.0);
            var barFill = new Border
            {
                Background = new SolidColorBrush(col),
                CornerRadius = new(3),
                RenderTransformOrigin = new(0.0, 0.5),
                RenderTransform = mScale,
            };
            sp.Children.Add(new Border
            {
                Height = 6,
                ClipToBounds = true,
                CornerRadius = new(3),
                Background = new SolidColorBrush(Color.FromArgb(90, 24, 34, 54)),
                BorderBrush = DS.Brush("DS.Hairline"),
                BorderThickness = new(0.5),
                Child = barFill,
            });
            return (mScale, sp);
        }
        (lgScale, var lgCell) = MakeMotorCell("L GRIP", Color.FromRgb(0xFF, 0x3B, 0x7F));
        (rgScale, var rgCell) = MakeMotorCell("R GRIP", Color.FromRgb(0x38, 0xBD, 0xF8));
        (ltScale, var ltCell) = MakeMotorCell("L TRIG", Color.FromRgb(0xFB, 0x92, 0x3C));
        (rtScale, var rtCell) = MakeMotorCell("R TRIG", Color.FromRgb(0xA7, 0x8B, 0xFA));
        motorsRow.Children.Add(lgCell);
        motorsRow.Children.Add(rgCell);
        motorsRow.Children.Add(ltCell);
        motorsRow.Children.Add(rtCell);
        body.Children.Add(motorsRow);

        var roleHead = new DockPanel { Margin = new(0, 0, 0, 4) };
        roleResetBtn = SmallButton("Reset Auto", async () =>
        {
            selectedMotorRoles = MotorRoleMap.Default;
            UpdateSelectionButtons();
            SyncPreviewDraft();
            await ApplyOrHotUpdateAudioAsync();
        }).Also(b => { b.Padding = new(8, 2, 8, 2); b.FontSize = 10.5; b.MinHeight = 24; });
        DockPanel.SetDock(roleResetBtn, Dock.Right);
        roleHead.Children.Add(roleResetBtn);
        roleHead.Children.Add(Kit.Text("Motor roles (click to assign)", 11.5, "DS.TextSoft").Also(t => t.VerticalAlignment = VerticalAlignment.Center));
        body.Children.Add(roleHead);

        Button MakeRoleBtn(string motorName, Func<MotorAudioRole> getter, Func<MotorAudioRole, MotorRoleMap> updater)
        {
            var btn = new Button
            {
                Style = DS.Style("DS.ConsoleBtn"),
                Padding = new(8, 4, 8, 4),
                Margin = new(2),
                FontSize = 11,
                MinHeight = 28,
                Height = double.NaN,
                ToolTip = $"Click to cycle custom musical role for {motorName}",
            };
            btn.Click += async (_, _) =>
            {
                selectedMotorRoles = updater(AudioHapticsEngine.CycleRole(getter(), 1));
                UpdateSelectionButtons();
                SyncPreviewDraft();
                await ApplyOrHotUpdateAudioAsync();
            };
            registerFocus?.Invoke(btn);
            return btn;
        }
        roleLgBtn = MakeRoleBtn("Left Grip", () => selectedMotorRoles.LeftGrip, r => selectedMotorRoles with { LeftGrip = r });
        roleRgBtn = MakeRoleBtn("Right Grip", () => selectedMotorRoles.RightGrip, r => selectedMotorRoles with { RightGrip = r });
        roleLtBtn = MakeRoleBtn("Left Trigger", () => selectedMotorRoles.LeftTrigger, r => selectedMotorRoles with { LeftTrigger = r });
        roleRtBtn = MakeRoleBtn("Right Trigger", () => selectedMotorRoles.RightTrigger, r => selectedMotorRoles with { RightTrigger = r });
        var roleGrid = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2, Margin = new(-2, 0, -2, 8) };
        roleGrid.Children.Add(roleLgBtn);
        roleGrid.Children.Add(roleRgBtn);
        roleGrid.Children.Add(roleLtBtn);
        roleGrid.Children.Add(roleRtBtn);
        body.Children.Add(roleGrid);

        var armButtons = new WrapPanel { Margin = new(0, 2, 0, 4) };
        armTriggersButton = new Button
        {
            Content = "Enable Triggers",
            Style = DS.Style("DS.ConsoleBtn"),
            Padding = new(11, 5, 11, 5),
            FontSize = 11.5,
            MinHeight = 30,
            Height = double.NaN,
            Margin = new(0, 0, 6, 4),
        };
        armTriggersButton.Click += async (_, _) =>
        {
            TriggerEnableGuideDialog.Show(Window.GetWindow(EffectsView), force: true);
            var shared = Diagnostics.BrowserTriggerRumble.Shared;
            if (shared == null)
            {
                testStatusText = "Press M + Back on your pad to toggle 2.4G/USB trigger vibration.";
                testStatusUntilTick = Environment.TickCount64 + 4500;
                RefreshTriggerArmState();
                return;
            }
            await shared.Listen();
            if (!shared.Armed && shared.Failure == null)
            {
                testStatusText = "Press any controller button now, then hold M + Back.";
                testStatusUntilTick = Environment.TickCount64 + 20_000;
            }
            else
            {
                testStatusUntilTick = 0;
            }
            RefreshTriggerArmState();
        };
        registerFocus?.Invoke(armTriggersButton);
        armButtons.Children.Add(armTriggersButton);

        test4MotorButton = new Button
        {
            Content = "Test 4-Motor",
            Style = DS.Style("DS.ConsoleBtn"),
            Padding = new(11, 5, 11, 5),
            FontSize = 11.5,
            MinHeight = 30,
            Height = double.NaN,
            Margin = new(0, 0, 0, 4),
        };
        test4MotorButton.Click += async (_, _) =>
        {
            test4MotorButton.IsEnabled = false;
            try
            {
                int slot = ControllerRumble.FindPhysicalSlot();
                var shared = Diagnostics.BrowserTriggerRumble.Shared;
                bool canUseIndependentTriggers = shared is { Armed: true, SupportsTriggerRumble: true, Failure: null };
                testStatusText = canUseIndependentTriggers ? "Pulsing all 4 motors..." : "Pulsing 2.4G/USB linked motors...";
                testStatusUntilTick = Environment.TickCount64 + 3500;
                RefreshTriggerArmState();

                bool trigOk = false;
                bool gripsOk = false;
                if (canUseIndependentTriggers)
                {
                    trigOk = await shared!.Play4(0.75, 0.75, 0.95, 0.95, 650);
                    if (trigOk)
                        await Task.Delay(650);
                    else
                        gripsOk = await ControllerRumble.Test(85, 85, 650);
                }
                else
                {
                    gripsOk = await ControllerRumble.Test(85, 85, 650);
                }

                if (trigOk)
                {
                    string slotTag = slot >= 0 ? $"Slot {slot} " : "";
                    testStatusText = $"4-Motor pulse sent ({slotTag}Grips + LT/RT Triggers)";
                }
                else if (gripsOk)
                {
                    testStatusText = $"2.4G/USB pulse sent (Slot {slot}) · Press M + BACK on pad if LT/RT triggers are off (BT mode = independent 4-motor)";
                }
                else
                {
                    testStatusText = "No XInput controller found (check 2.4G dongle, USB cable, or BT)";
                }
                testStatusUntilTick = Environment.TickCount64 + 4500;
                if (canUseIndependentTriggers && selectedKind == LightingEffectKind.AudioReactive && selectedAudioHaptics > 0)
                    shared!.EnsureFocusForAudioTriggers();
                RefreshTriggerArmState();
            }
            finally
            {
                test4MotorButton.IsEnabled = true;
            }
        };
        registerFocus?.Invoke(test4MotorButton);
        armButtons.Children.Add(test4MotorButton);
        body.Children.Add(armButtons);

        triggerArmStatusText = new TextBlock
        {
            FontSize = 11,
            Foreground = DS.Brush("DS.TextMuted"),
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
        };
        body.Children.Add(triggerArmStatusText);

        box.Child = body;
        return box;
    }

    private async Task ApplyOrHotUpdateAudioAsync(bool autoStartIfStopped = false)
    {
        var spec = BuildCurrentEffectSpec();
        bool updated = LightingEffectsService.UpdateRunningSpec(spec);
        if (!updated && (autoStartIfStopped || LightingEffectsService.Status.IsRunning))
            await LightingEffectsService.ApplyAsync(spec);

        if (!ReviewSandbox.Active &&
            selectedKind == LightingEffectKind.AudioReactive &&
            selectedAudioHaptics > 0 &&
            Diagnostics.BrowserTriggerRumble.Shared is { Armed: true } shared)
        {
            shared.EnsureFocusForAudioTriggers();
        }
    }

    private void RefreshTriggerArmState()
    {
        if (armTriggersButton == null || triggerArmStatusText == null) return;
        if (ReviewSandbox.Active)
        {
            armTriggersButton.Visibility = Visibility.Collapsed;
            if (test4MotorButton != null) test4MotorButton.Visibility = Visibility.Collapsed;
            if (triggerArmStatusText.Text != "Sandbox mode: simulating all 4 motors")
                triggerArmStatusText.Text = "Sandbox mode: simulating all 4 motors";
            return;
        }

        if (test4MotorButton != null)
            test4MotorButton.Visibility = Visibility.Visible;

        var shared = Diagnostics.BrowserTriggerRumble.Shared;
        shared?.SyncHostWindowActiveOnUiThread();
        bool armed = shared is { Armed: true };
        bool independentTriggers = shared is { Armed: true, SupportsTriggerRumble: true };
        armTriggersButton.Visibility = Visibility.Visible;
        if (shared == null || shared.Failure != null)
        {
            armTriggersButton.Content = "Trigger Setup";
            armTriggersButton.IsEnabled = true;
        }
        else if (armed)
        {
            armTriggersButton.Content = "Trigger Setup";
            armTriggersButton.IsEnabled = true;
        }
        else
        {
            armTriggersButton.Content = "Enable Triggers";
            armTriggersButton.IsEnabled = true;
        }

        string nextText;
        Brush nextColor;
        if (testStatusUntilTick != 0 && Environment.TickCount64 < testStatusUntilTick && !string.IsNullOrEmpty(testStatusText))
        {
            nextText = testStatusText;
            nextColor = DS.Brush("DS.AccentHi");
        }
        else if (LightingEffectsService.Status.IsRunning && selectedKind == LightingEffectKind.AudioReactive)
        {
            nextText = SystemAudioLevelSampler.HapticsPathSummary;
            nextColor = SystemAudioLevelSampler.TriggersStreaming ? DS.Brush("DS.AccentHi") : DS.Brush("DS.TextSoft");
        }
        else if (shared == null)
        {
            nextText = "Grips active via XInput · Press M + BACK on pad to link LT/RT triggers";
            nextColor = DS.Brush("DS.TextMuted");
        }
        else if (shared.Failure != null)
        {
            nextText = shared.Failure;
            nextColor = DS.Brush("DS.TextMuted");
        }
        else if (independentTriggers)
        {
            nextText = "4-Motor Armed · Pick Light/Punch/Max to feel Grips + LT/RT";
            nextColor = DS.Brush("DS.AccentHi");
        }
        else if (armed)
        {
            nextText = "2.4G/USB Linked Mode · Press M + BACK on pad to enable LT/RT triggers (BT mode = independent 4-motor)";
            nextColor = DS.Brush("DS.TextSoft");
        }
        else
        {
            nextText = shared.WaitingToArm
                ? "Press any pad button now to unlock LT/RT triggers (on 2.4G/USB press M + BACK on pad)."
                : "2.4G/USB: press M + BACK on pad for LT/RT · BT mode: click Arm Triggers & press a pad button";
            nextColor = DS.Brush("DS.TextMuted");
        }

        if (!Equals(triggerArmStatusText.Foreground, nextColor))
            triggerArmStatusText.Foreground = nextColor;
        if (triggerArmStatusText.Text != nextText)
            triggerArmStatusText.Text = nextText;
    }

    private void SyncAudioTimer()
    {
        if (selectedKind == LightingEffectKind.AudioReactive && EffectsView is FrameworkElement fe && fe.IsLoaded)
        {
            if (!ReviewSandbox.Active) SystemAudioLevelSampler.EnsureStarted();
            if (!audioUiTimer.IsEnabled) audioUiTimer.Start();
        }
        else if (audioUiTimer.IsEnabled)
        {
            audioUiTimer.Stop();
        }
    }

    private void RefreshLiveAudioMeters()
    {
        if (selectedKind != LightingEffectKind.AudioReactive) return;
        AudioFrame af;
        HapticsFrame hf;
        if (ReviewSandbox.Active)
        {
            double t = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
            float beatCycle = (float)((t * 2.0) % 1.0);
            float pulse = MathF.Exp(-beatCycle * 5.0f);
            float b = Math.Clamp(0.20f + 0.75f * pulse, 0.03f, 1.0f);
            float m = Math.Clamp(0.25f + 0.55f * (0.5f + 0.5f * MathF.Sin((float)t * 3.1f)), 0.03f, 1.0f);
            float tr = Math.Clamp(0.15f + 0.65f * (0.5f + 0.5f * MathF.Sin((float)t * 7.4f)), 0.03f, 1.0f);
            float trSpike = ((t * 4.0) % 1.0) < 0.25 ? 0.85f : 0f;
            float voc = Math.Clamp(0.45f + 0.40f * MathF.Sin((float)t * 2.1f), 0.0f, 1.0f);
            var spec8 = new AudioSpectrum8(
                b,
                Math.Clamp(b * 0.85f, 0.04f, 1.0f),
                Math.Clamp(m * 0.82f, 0.04f, 1.0f),
                voc,
                Math.Clamp(voc * 0.88f, 0.04f, 1.0f),
                Math.Clamp(tr * 0.80f, 0.04f, 1.0f),
                tr,
                Math.Clamp(tr * 0.65f, 0.04f, 1.0f));
            af = new AudioFrame(Math.Max(b, m), b, m, tr, pulse, (int)(t * 2.0), 0f, b, tr, 0.5f, 0.65f, trSpike, voc, voc, voc * 0.85f, m, Math.Max(pulse, b * 0.7f), Math.Max(b, m), AudioMusicVibe.GroovePop, spec8);
            hf = AudioHapticsEngine.Evaluate(af, selectedAudioHapticsMode, selectedAudioHaptics, selectedMotorRoles, t);
        }
        else
        {
            af = SystemAudioLevelSampler.LatestFrame;
            hf = selectedAudioHaptics > 0 && LightingEffectsService.Status.IsRunning
                ? SystemAudioLevelSampler.LatestHaptics
                : AudioHapticsEngine.Evaluate(af, selectedAudioHapticsMode, selectedAudioHaptics, selectedMotorRoles, 0.0);
            RefreshTriggerArmState();
        }

        for (int i = 0; i < 8; i++)
        {
            if (spectrumScales[i] is { } st)
                st.ScaleY = Math.Clamp(af.Spectrum[i], 0.04, 0.98);
        }

        static void SetScale(ScaleTransform? st, double v)
        {
            if (st != null) st.ScaleX = Math.Clamp(v, 0.02, 0.99);
        }

        SetScale(bassScale, af.Bass);
        SetScale(midScale, af.Mid);
        SetScale(trebleScale, af.Treble);
        SetScale(vocalScale, af.VocalPresence);
        SetScale(lgScale, hf.LeftGrip);
        SetScale(rgScale, hf.RightGrip);
        SetScale(ltScale, hf.LeftTrigger);
        SetScale(rtScale, hf.RightTrigger);

        if (beatBadge != null)
        {
            var nextBeat = af.BeatPulse > 0.30f ? BeatActiveBrush : BeatIdleBrush;
            if (!ReferenceEquals(beatBadge.Background, nextBeat))
                beatBadge.Background = nextBeat;
        }

        if (vibeBadgeText != null)
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
            if (vibeBadgeText.Text != vibeLabel)
                vibeBadgeText.Text = vibeLabel;
        }
    }

    private void RenderPaletteSwatches()
    {
        palettePanel.Children.Clear();
        for (int i = 0; i < palette.Count; i++)
        {
            int idx = i;
            string hex = palette[i];
            var (r, g, b) = LightingEffectSpec.ParseRgb(hex);

            var chip = new Border
            {
                BorderBrush = DS.Brush("DS.Hairline"),
                BorderThickness = new(1),
                CornerRadius = ThemeCatalog.Round(6),
                Margin = new(0, 0, 6, 6),
                Padding = new(5, 3, 5, 3),
                Background = new SolidColorBrush(ThemeCatalog.Alpha(ThemeCatalog.Current.Night, 190)),
            };
            var inner = new StackPanel { Orientation = Orientation.Horizontal };
            var swatch = new Button
            {
                Width = 18,
                Height = 18,
                MinHeight = 18,
                Padding = new(0),
                Background = new SolidColorBrush(Color.FromRgb(r, g, b)),
                BorderBrush = DS.Brush("DS.Hairline"),
                BorderThickness = new(1),
                ToolTip = $"Load {hex} into colour picker",
                Margin = new(0, 0, 6, 0),
                Template = LightingWindow.SwatchTemplate(4),
            };
            swatch.Click += (_, _) => setPickerRgb((r << 16) | (g << 8) | b);
            registerFocus?.Invoke(swatch);
            inner.Children.Add(swatch);
            inner.Children.Add(new TextBlock
            {
                Text = hex,
                FontFamily = LightingWindow.MonoFont,
                Foreground = DS.Brush("DS.Text"),
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 11.5,
                Margin = new(0, 0, palette.Count > 1 ? 6 : 2, 0),
            });
            if (palette.Count > 1)
            {
                var del = new Button
                {
                    Content = "×",
                    Padding = new(5, 0, 5, 0),
                    FontSize = 12,
                    Style = DS.Style("DS.ConsoleStepBtn"),
                    MinHeight = 20,
                    Height = 20,
                };
                del.Click += (_, _) => { palette.RemoveAt(idx); RenderPaletteSwatches(); SyncPreviewDraft(); };
                registerFocus?.Invoke(del);
                inner.Children.Add(del);
            }
            chip.Child = inner;
            palettePanel.Children.Add(chip);
        }
    }

    private void RefreshSavedPresets()
    {
        savedPresetsList.Children.Clear();
        builtInPresetsList.Children.Clear();

        var custom = LightingEffectsService.Store.Presets;
        if (custom.Count == 0)
            savedPresetsList.Children.Add(Kit.Text("No custom presets saved yet.", 12.5, "DS.TextMuted").Also(t => t.Margin = new(0, 2, 0, 2)));

        foreach (var preset in custom)
            savedPresetsList.Children.Add(BuildPresetRow(preset, removable: true));
        foreach (var preset in LightingEffectEngine.BuiltInPresets)
            builtInPresetsList.Children.Add(BuildPresetRow(preset, removable: false));

        UpdateSelectedRuleHint();
    }

    private FrameworkElement BuildPresetRow(LightingPreset preset, bool removable)
    {
        var (r, g, b) = LightingEffectSpec.ParseRgb(preset.Spec.Palette[0]);
        bool isTarget = preset.Id == selectedRulePresetId;
        var row = new Border
        {
            BorderBrush = isTarget ? DS.Brush("DS.Accent") : DS.Brush("DS.Hairline"),
            BorderThickness = new(isTarget ? 1.5 : 1),
            CornerRadius = ThemeCatalog.Round(6),
            Padding = new(9, 6, 9, 6),
            Margin = new(0, 0, 0, 5),
            Background = new SolidColorBrush(ThemeCatalog.Alpha(ThemeCatalog.Current.Night, 175)),
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new() { Width = GridLength.Auto });

        grid.Children.Add(new Border
        {
            Width = 16,
            Height = 16,
            CornerRadius = ThemeCatalog.Round(4),
            Background = new SolidColorBrush(Color.FromRgb(r, g, b)),
            BorderBrush = DS.Brush("DS.Hairline"),
            BorderThickness = new(1),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new(0, 0, 9, 0),
        });

        var title = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        title.Children.Add(new TextBlock
        {
            Text = preset.Name,
            Foreground = DS.Brush("DS.Text"),
            FontWeight = FontWeights.SemiBold,
            FontSize = 12.5,
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        title.Children.Add(new TextBlock
        {
            Text = $"{ShortEffectName(preset.Spec.Kind)} · {Math.Round(LightingRecord.BrightnessPercentFor(preset.Spec.MaxBrightness)):0}%",
            Foreground = DS.Brush("DS.TextMuted"),
            FontSize = 11,
        });
        Grid.SetColumn(title, 1);
        grid.Children.Add(title);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var targetBtn = SmallButton(isTarget ? "Target" : "Set rule", () =>
        {
            selectedRulePresetId = preset.Id;
            RefreshSavedPresets();
        });
        if (isTarget) LightingWindow.SetSelectedState(targetBtn, true);
        targetBtn.Margin = new(4, 0, 5, 0);
        actions.Children.Add(targetBtn);

        actions.Children.Add(SmallButton("Use", async () =>
        {
            LoadFromSpec(preset.Spec);
            setPickerRgb((r << 16) | (g << 8) | b);
            await LightingEffectsService.ApplyAsync(preset.Spec);
        }));

        if (removable)
        {
            actions.Children.Add(SmallButton("×", () =>
            {
                LightingEffectsService.Store.DeletePreset(preset.Id);
                if (selectedRulePresetId == preset.Id) selectedRulePresetId = "builtin-hue-sweep";
                RefreshSavedPresets();
                RefreshGameRules();
            }).Also(btn => btn.Margin = new(5, 0, 0, 0)));
        }

        Grid.SetColumn(actions, 2);
        grid.Children.Add(actions);
        row.Child = grid;
        return row;
    }

    private void RefreshRunningApps()
    {
        runningAppsPanel.Children.Clear();
        var apps = new List<ForegroundApp.App>();
        try { apps.AddRange(ForegroundApp.Running().Take(6)); } catch { }
        if (apps.Count == 0)
        {
            runningAppsPanel.Children.Add(Kit.Text("No foreground apps detected - use '+ Add .exe...'.", 12, "DS.TextMuted"));
            return;
        }
        foreach (var app in apps)
        {
            var captured = app;
            runningAppsPanel.Children.Add(SmallButton($"+ {captured.Exe}", () =>
            {
                LightingEffectsService.Store.SetGameRule(captured.Exe, selectedRulePresetId);
                RefreshGameRules();
            }).Also(b => { b.Margin = new(0, 0, 6, 6); b.ToolTip = captured.Title; }));
        }
    }

    private void RefreshGameRules()
    {
        gameRulesList.Children.Clear();
        var rules = LightingEffectsService.Store.GameRules;
        if (rules.Count == 0)
        {
            gameRulesList.Children.Add(Kit.Text("No app rules configured.", 12.5, "DS.TextMuted").Also(t => t.Margin = new(0, 2, 0, 2)));
            return;
        }
        foreach (var rule in rules)
        {
            var target = LightingEffectsService.Store.FindPreset(rule.PresetId)?.Name ?? "(missing)";
            var row = new DockPanel { Margin = new(0, 0, 0, 5) };
            var del = SmallButton("Remove", () => { LightingEffectsService.Store.RemoveGameRule(rule.ExeName); RefreshGameRules(); });
            DockPanel.SetDock(del, Dock.Right);
            row.Children.Add(del);
            row.Children.Add(new TextBlock
            {
                Text = $"{rule.ExeName} → {target}",
                Foreground = DS.Brush("DS.Text"),
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 12.5,
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
            gameRulesList.Children.Add(row);
        }
    }

    private void UpdateSelectedRuleHint()
    {
        var name = LightingEffectsService.Store.FindPreset(selectedRulePresetId)?.Name ?? selectedRulePresetId;
        selectedGameRuleHint.Text = $"Target preset: {name}";
    }

    private void UpdateSliderLabels()
    {
        byte minB = (byte)Math.Clamp((int)Math.Round(minBrightSlider.Value), 0, 255);
        byte maxB = (byte)Math.Clamp((int)Math.Round(maxBrightSlider.Value), 1, 255);
        double minPct = LightingRecord.BrightnessPercentFor(minB);
        double maxPct = LightingRecord.BrightnessPercentFor(maxB);
        maxBrightLabel.Text = $"Peak brightness · {maxPct:0}%";
        periodLabel.Text = $"Cycle speed · {periodSlider.Value / 1000.0:0.0} s";
        minBrightLabel.Text = $"Brightness floor · {minPct:0}% (0x{minB:X2}{(minB == 1 ? " Moonlight" : minB == 0 ? " Off" : "")})";
        intervalLabel.Text = $"Update rate · {intervalSlider.Value:0} ms";
        idleLabel.Text = $"Idle auto-dim · {idleSlider.Value:0} s";
        audioSensLabel.Text = $"Audio sensitivity · {audioSensSlider.Value:0}%";
    }

    private void SaveQuick(bool asSolid)
    {
        var spec = asSolid
            ? new LightingEffectSpec
            {
                Kind = LightingEffectKind.Solid,
                Palette = [$"#{getPickerRgb() & 0xffffff:X6}"],
                MaxBrightness = getPickerBrightness(),
            }.Normalized()
            : BuildCurrentEffectSpec();
        string name = asSolid
            ? $"Solid #{getPickerRgb() & 0xffffff:X6}"
            : $"{ShortEffectName(spec.Kind)} · {DateTime.Now:HH:mm}";
        try
        {
            var saved = LightingEffectsService.Store.SavePreset(name, spec);
            selectedRulePresetId = saved.Id;
            RefreshSavedPresets();
        }
        catch { }
    }

    private void SaveWithPrompt(bool asSolid)
    {
        string defaultName = ShortEffectName(selectedKind);
        if (PromptText("Save Lighting Preset", "Preset name:", defaultName) is not { } entered) return;
        var spec = asSolid
            ? new LightingEffectSpec
            {
                Kind = LightingEffectKind.Solid,
                Palette = [$"#{getPickerRgb() & 0xffffff:X6}"],
                MaxBrightness = getPickerBrightness(),
            }.Normalized()
            : BuildCurrentEffectSpec();
        try
        {
            var saved = LightingEffectsService.Store.SavePreset(entered, spec);
            selectedRulePresetId = saved.Id;
            RefreshSavedPresets();
        }
        catch { }
    }

    private void BindCustomExeWithPrompt()
    {
        string suggested = ForegroundApp.Last?.Exe ?? "eldenring.exe";
        if (PromptText("Add Foreground App Rule", "Executable filename (e.g. game.exe):", suggested) is not { } exe) return;
        try
        {
            LightingEffectsService.Store.SetGameRule(exe, selectedRulePresetId);
            RefreshGameRules();
        }
        catch { }
    }

    internal static string? PromptText(string title, string label, string initial)
    {
        string? result = null;
        var win = new Window
        {
            Title = title,
            Width = 380,
            Height = 185,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            ResizeMode = ResizeMode.NoResize,
            Background = DS.Brush("DS.Deep"),
            Foreground = DS.Brush("DS.Text"),
            FontFamily = DS.Body,
        };
        var input = new TextBox { Text = initial, Padding = new(10, 7, 10, 7), Margin = new(0, 6, 0, 14) };
        input.SelectAll();
        var ok = new Button { Content = "Save", Style = DS.Style("DS.ConsolePrimary"), MinHeight = 34, Padding = new(16, 6, 16, 6), Margin = new(0, 0, 8, 0), IsDefault = true };
        ok.Click += (_, _) => { if (!string.IsNullOrWhiteSpace(input.Text)) { result = input.Text.Trim(); win.DialogResult = true; } };
        var cancel = new Button { Content = "Cancel", Style = DS.Style("DS.ConsoleBtn"), Padding = new(14, 6, 14, 6), IsCancel = true };
        win.Content = new StackPanel
        {
            Margin = new(18),
            Children =
            {
                Kit.Text(label, 13, "DS.TextSoft"),
                input,
                new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Children = { ok, cancel } },
            },
        };
        return win.ShowDialog() == true ? result : null;
    }

    private Button SmallButton(string text, Action click)
    {
        var btn = new Button
        {
            Content = text,
            Style = DS.Style("DS.ConsoleBtn"),
            Padding = new(10, 5, 10, 5),
            FontSize = 11.5,
            MinHeight = 30,
            Height = double.NaN,
        };
        btn.Click += (_, _) => click();
        registerFocus?.Invoke(btn);
        return btn;
    }

    internal static string ShortEffectName(LightingEffectKind kind) => kind switch
    {
        LightingEffectKind.ScreenAmbient => "Ambient Screen Sync",
        LightingEffectKind.AudioReactive => "Audio Reactive RGB",
        LightingEffectKind.SmoothHueWave => "Rainbow Wave",
        LightingEffectKind.PaletteFade => "Color Fade",
        LightingEffectKind.PaletteStep => "Color Jump",
        LightingEffectKind.MoonlightPulse => "Moonlight Pulse",
        LightingEffectKind.EmberShimmer => "Lava Ember",
        LightingEffectKind.DualStrobe => "Dual Strobe",
        LightingEffectKind.TriggerGlow => "Trigger Glow",
        LightingEffectKind.StickHeat => "Stick Heat",
        LightingEffectKind.ButtonFlash => "Button Flash",
        LightingEffectKind.IdleSleepDim => "Idle Nightlight",
        LightingEffectKind.GameAutoProfile => "Game Auto",
        _ => LightingEffectSpec.EffectLabel(kind),
    };

    private static string EffectSubtitle(LightingEffectKind kind) => kind switch
    {
        LightingEffectKind.ScreenAmbient => "Matches colours on your screen",
        LightingEffectKind.AudioReactive => "FFT Bass/Mid/Treble, Beat & Haptics",
        LightingEffectKind.SmoothHueWave => "Continuous 360° sweep",
        LightingEffectKind.PaletteFade => "Smooth multi-stop blend",
        LightingEffectKind.PaletteStep => "Clean palette jumps",
        LightingEffectKind.MoonlightPulse => "Gentle 1% breathing",
        LightingEffectKind.EmberShimmer => "Warm shifting glow",
        LightingEffectKind.DualStrobe => "Alternating two-tone flash",
        LightingEffectKind.TriggerGlow => "Tracks LT / RT triggers",
        LightingEffectKind.StickHeat => "Tracks thumbstick motion",
        LightingEffectKind.ButtonFlash => "Flashes on button press",
        LightingEffectKind.IdleSleepDim => "Dims after idle timeout",
        _ => "Live RGB effect",
    };
}
