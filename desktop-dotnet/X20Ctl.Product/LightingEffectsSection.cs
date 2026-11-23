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

    private readonly TextBlock maxBrightLabel = Kit.Text("", 13, "DS.TextSoft");
    private readonly TextBlock periodLabel = Kit.Text("", 13, "DS.TextSoft");
    private readonly TextBlock minBrightLabel = Kit.Text("", 12.5, "DS.TextSoft");
    private readonly TextBlock intervalLabel = Kit.Text("", 12.5, "DS.TextSoft");
    private readonly TextBlock idleLabel = Kit.Text("", 12.5, "DS.TextSoft");
    private readonly TextBlock selectedGameRuleHint = Kit.Text("", 13, "DS.AccentHi").Also(t => { t.TextWrapping = TextWrapping.Wrap; t.Margin = new(0, 0, 0, 6); });

    private readonly Dictionary<LightingEffectKind, Button> kindButtons = [];
    private readonly Dictionary<int, Button> capButtons = [];
    private readonly List<string> palette = ["#00FFFF", "#FF00AA", "#3A6CFF"];

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

    private LightingEffectKind selectedKind = LightingEffectKind.SmoothHueWave;
    private int selectedWriteCap;
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
            palette.Clear();
            palette.AddRange(spec.Palette);
            minBrightSlider.Value = spec.MinBrightness;
            maxBrightSlider.Value = spec.MaxBrightness;
            intervalSlider.Value = spec.StepIntervalMs;
            periodSlider.Value = spec.CyclePeriodMs;
            idleSlider.Value = spec.IdleTimeoutSeconds;
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
            var content = new StackPanel { Children = { title, sub } };
            var btn = new Button
            {
                Content = content,
                Style = DS.Style("ActionButton"),
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
                else if (kind == LightingEffectKind.AudioReactive && intervalSlider.Value > 120)
                    intervalSlider.Value = 65;
                UpdateSelectionButtons();
                UpdateSliderLabels();
                SyncPreviewDraft();
                await LightingEffectsService.ApplyAsync(BuildCurrentEffectSpec());
            };
            registerFocus?.Invoke(btn);
            kindButtons[kind] = btn;
            return btn;
        }

        var heroGrid = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2, Margin = new(-3, 0, -3, 3) };
        heroGrid.Children.Add(MakeEffectTile(LightingEffectKind.ScreenAmbient, hero: true));
        heroGrid.Children.Add(MakeEffectTile(LightingEffectKind.AudioReactive, hero: true));
        stack.Children.Add(heroGrid);

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
            paletteActions.Children.Add(SmallButton(name, () =>
            {
                palette.Clear();
                palette.AddRange(stops);
                RenderPaletteSwatches();
                SyncPreviewDraft();
                if (LightingEffectsService.Status.IsRunning)
                    _ = LightingEffectsService.ApplyAsync(BuildCurrentEffectSpec());
            }).Also(b => b.Margin = new(0, 0, 6, 6)));
        }
        stack.Children.Add(paletteActions);

        minBrightSlider.ValueChanged += (_, _) =>
        {
            if (minBrightSlider.Value > maxBrightSlider.Value && !syncing) maxBrightSlider.Value = Math.Max(1, minBrightSlider.Value);
            UpdateSliderLabels();
            SyncPreviewDraft();
        };
        maxBrightSlider.ValueChanged += (_, _) =>
        {
            if (maxBrightSlider.Value < minBrightSlider.Value && !syncing) minBrightSlider.Value = maxBrightSlider.Value;
            UpdateSliderLabels();
            SyncPreviewDraft();
        };
        intervalSlider.ValueChanged += (_, _) => UpdateSliderLabels();
        periodSlider.ValueChanged += (_, _) => { UpdateSliderLabels(); SyncPreviewDraft(); };
        idleSlider.ValueChanged += (_, _) => UpdateSliderLabels();

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

        var capsPanel = new WrapPanel();
        foreach (var (cap, label) in WriteCapChoices)
        {
            int captured = cap;
            var btn = new Button
            {
                Content = label,
                Padding = new(10, 5, 10, 5),
                FontSize = 12,
                MinHeight = 30,
                Height = double.NaN,
            };
            btn.Click += (_, _) => { selectedWriteCap = captured; UpdateSelectionButtons(); };
            registerFocus?.Invoke(btn);
            capButtons[captured] = btn;
            capsPanel.Children.Add(btn);
        }
        advanced.Children.Add(Kit.Segments(capsPanel));

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
                    Style = DS.Style("ActionButton"),
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
            runningAppsPanel.Children.Add(Kit.Text("No foreground apps detected — use '+ Add .exe…'.", 12, "DS.TextMuted"));
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

    private static string? PromptText(string title, string label, string initial)
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
        var ok = new Button { Content = "Save", Style = DS.Style("DS.Primary"), Padding = new(16, 7, 16, 7), Margin = new(0, 0, 8, 0), IsDefault = true };
        ok.Click += (_, _) => { if (!string.IsNullOrWhiteSpace(input.Text)) { result = input.Text.Trim(); win.DialogResult = true; } };
        var cancel = new Button { Content = "Cancel", Style = DS.Style("ActionButton"), Padding = new(14, 7, 14, 7), IsCancel = true };
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
            Style = DS.Style("ActionButton"),
            Padding = new(10, 5, 10, 5),
            FontSize = 12,
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
        LightingEffectKind.AudioReactive => "Reacts live to PC music & game audio",
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
