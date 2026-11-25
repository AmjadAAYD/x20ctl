using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using X20Ctl.Zone;
using X20Ctl.Zone.PcMode;

namespace X20Ctl.Product;

public sealed partial class StickToolsPanel
{
    readonly ComboBox dpadSocd = new() { MinHeight = 34, Width = 190, FontSize = 13.5 };
    readonly ComboBox stickSocd = new() { MinHeight = 34, Width = 190, FontSize = 13.5 };
    readonly CheckBox sniperEnabled = new() { Style = DS.Style("DS.Toggle"), VerticalAlignment = VerticalAlignment.Center };
    readonly ComboBox sniperButton = new() { MinHeight = 34, Width = 120, FontSize = 13.5 };
    readonly Slider sniperScale = new() { Style = DS.Style("DS.Slider"), Minimum = 20, Maximum = 95, Value = 55, Width = 150, TickFrequency = 5, IsSnapToTickEnabled = true, VerticalAlignment = VerticalAlignment.Center };
    readonly TextBlock sniperScaleText = Kit.Text("55%", 13, "DS.TextSoft").Also(t => t.Margin = new(8, 0, 0, 0));

    readonly ComboBox trigChannel = new() { MinHeight = 34, Width = 130, FontSize = 13.5, ItemsSource = new[] { "Left (LT)", "Right (RT)" }, SelectedIndex = 0 };
    readonly ComboBox trigPreset = new() { MinHeight = 34, Width = 175, FontSize = 13.5 };
    readonly CheckBox rapidTrigger = new() { Style = DS.Style("DS.Toggle"), VerticalAlignment = VerticalAlignment.Center };
    readonly Slider rapidActuation = new() { Style = DS.Style("DS.Slider"), Minimum = 2, Maximum = 80, Value = 10, Width = 120, TickFrequency = 1, IsSnapToTickEnabled = true, VerticalAlignment = VerticalAlignment.Center };
    readonly TextBlock rapidActuationText = Kit.Text("10%", 12.5, "DS.TextSoft").Also(t => t.Margin = new(6, 0, 14, 0));
    readonly Slider rapidReset = new() { Style = DS.Style("DS.Slider"), Minimum = 2, Maximum = 40, Value = 5, Width = 120, TickFrequency = 1, IsSnapToTickEnabled = true, VerticalAlignment = VerticalAlignment.Center };
    readonly TextBlock rapidResetText = Kit.Text("5%", 12.5, "DS.TextSoft").Also(t => t.Margin = new(6, 0, 0, 0));

    readonly CheckBox dualStage = new() { Style = DS.Style("DS.Toggle"), VerticalAlignment = VerticalAlignment.Center };
    readonly ComboBox dualOutput = new() { MinHeight = 34, Width = 140, FontSize = 13.5 };
    readonly Dictionary<string, TriggerTuning> triggerEdits = new();

    // A/B Curve Blind Test snapshots (Right Stick)
    (SetupCurve? Curve, StickTuning? Tuning)? slotA, slotB;
    bool blindUsingB;
    TextBlock? abStatusText;
    Button? abToggleBtn, abRevealBtn;

    void AttachCompetitiveLab(StackPanel stack)
    {
        var socdModes = new[] { "Off", "Last Input Priority", "Neutral" };
        dpadSocd.ItemsSource = socdModes; dpadSocd.SelectedIndex = 0;
        stickSocd.ItemsSource = socdModes; stickSocd.SelectedIndex = 0;
        dpadSocd.SelectionChanged += (_, _) => Changed();
        stickSocd.SelectionChanged += (_, _) => Changed();

        sniperButton.ItemsSource = new[] { "LT", "LB", "RB", "L3", "R3", "SELECT", "A", "B" };
        sniperButton.SelectedItem = "LT";
        sniperEnabled.Checked += (_, _) => Changed();
        sniperEnabled.Unchecked += (_, _) => Changed();
        sniperButton.SelectionChanged += (_, _) => Changed();
        sniperScale.ValueChanged += (_, _) => { sniperScaleText.Text = $"{sniperScale.Value:0}%"; Changed(); };

        trigPreset.ItemsSource = new[] { "Linear", "Hair Trigger", "Racing Throttle", "Sniper", "Digital Click" };
        trigPreset.SelectedIndex = 0;
        dualOutput.ItemsSource = new[] { "None", "L3", "R3", "LB", "RB", "A", "B", "X", "Y" };
        dualOutput.SelectedIndex = 0;

        trigChannel.SelectionChanged += (_, _) => LoadTriggerFields();
        void TriggerFieldEdited()
        {
            if (loading) return;
            string key = trigChannel.SelectedIndex == 1 ? "RT" : "LT";
            var tt = new TriggerTuning
            {
                Preset = (TriggerPreset)Math.Clamp(trigPreset.SelectedIndex, 0, 4),
                RapidTrigger = rapidTrigger.IsChecked == true,
                ActuationPct = (int)Math.Round(rapidActuation.Value),
                ResetDeltaPct = (int)Math.Round(rapidReset.Value),
                DualStage = dualStage.IsChecked == true,
                HalfPullPct = 45,
                FullPullPct = 92,
                FullPullOutput = dualOutput.SelectedIndex > 0 ? dualOutput.SelectedItem as string : null,
            };
            if (tt.IsDefault) triggerEdits.Remove(key);
            else triggerEdits[key] = tt;
            Changed();
        }
        trigPreset.SelectionChanged += (_, _) => TriggerFieldEdited();
        rapidTrigger.Checked += (_, _) => TriggerFieldEdited();
        rapidTrigger.Unchecked += (_, _) => TriggerFieldEdited();
        rapidActuation.ValueChanged += (_, _) => { rapidActuationText.Text = $"{rapidActuation.Value:0}%"; TriggerFieldEdited(); };
        rapidReset.ValueChanged += (_, _) => { rapidResetText.Text = $"{rapidReset.Value:0}%"; TriggerFieldEdited(); };
        dualStage.Checked += (_, _) => TriggerFieldEdited();
        dualStage.Unchecked += (_, _) => TriggerFieldEdited();
        dualOutput.SelectionChanged += (_, _) => TriggerFieldEdited();

        var snapA = Pill("Save as Sample A"); snapA.Height = 32; snapA.Padding = new(12, 0, 12, 0); snapA.Margin = new(0, 0, 8, 6);
        var snapB = Pill("Save as Sample B"); snapB.Height = 32; snapB.Padding = new(12, 0, 12, 0); snapB.Margin = new(0, 0, 8, 6);
        abToggleBtn = Pill("Switch A / B"); abToggleBtn.Height = 32; abToggleBtn.Padding = new(12, 0, 12, 0); abToggleBtn.Margin = new(0, 0, 8, 6);
        abRevealBtn = Pill("Reveal A vs B"); abRevealBtn.Height = 32; abRevealBtn.Padding = new(12, 0, 12, 0); abRevealBtn.Margin = new(0, 0, 0, 6);
        abStatusText = Wrap("Save two right-stick curves as A and B, then toggle blind while testing.", 12.5, "DS.TextMuted", 4);

        snapA.Click += (_, _) => CaptureAbSlot(isB: false);
        snapB.Click += (_, _) => CaptureAbSlot(isB: true);
        abToggleBtn.Click += (_, _) => ToggleAbBlind();
        abRevealBtn.Click += (_, _) => RevealAb();

        var lab = new StackPanel { Margin = new(0, 12, 0, 0) };
        lab.Children.Add(Wrap("Note: SOCD, Rapid-Trigger, and macros can be restricted in some competitive online games.", 12.5, "DS.Warn", 0));

        // 1. Snap Tap / SOCD Cleaning
        lab.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new(0, 12, 0, 0),
            Children =
            {
                Label("D-Pad SOCD"), dpadSocd,
                Label("Left Stick SOCD", 20), stickSocd,
            }
        });

        // 2. Hold-to-Aim Sniper Curve
        lab.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new(0, 12, 0, 0),
            Children =
            {
                sniperEnabled, Label("Hold-to-Aim Sniper Curve", 8),
                Label("While holding", 14), sniperButton,
                Label("Speed", 14), sniperScale, sniperScaleText,
            }
        });

        // 3. Rapid-Trigger & Dual-Stage Triggers
        lab.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new(0, 14, 0, 0),
            Children =
            {
                Label("Trigger"), trigChannel,
                Label("Force Curve", 14), trigPreset,
            }
        });
        lab.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new(0, 10, 0, 0),
            Children =
            {
                rapidTrigger, Label("Rapid-Trigger", 8),
                Label("Actuate", 12), rapidActuation, rapidActuationText,
                Label("Reset delta", 4), rapidReset, rapidResetText,
            }
        });
        lab.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new(0, 10, 0, 0),
            Children =
            {
                dualStage, Label("Dual-Stage full pull", 8),
                Label("Secondary action", 12), dualOutput,
            }
        });

        // 4. A/B Blind Test
        lab.Children.Add(Kit.Text("A/B CURVE BLIND TEST (RIGHT STICK)", 12, "DS.TextSoft", FontWeights.SemiBold).Also(t => t.Margin = new(0, 16, 0, 6)));
        lab.Children.Add(new WrapPanel { Children = { snapA, snapB, abToggleBtn, abRevealBtn } });
        lab.Children.Add(abStatusText);

        var expander = new Expander
        {
            Header = "Competitive & Trigger Lab (SOCD, Sniper, A/B Test, Rapid-Trigger)",
            IsExpanded = false,
            Margin = new(0, 16, 0, 0),
            Foreground = DS.Brush("DS.Text"),
            FontSize = 14.5,
            FontWeight = FontWeights.SemiBold,
            Content = lab,
        };
        stack.Children.Add(expander);
    }

    void LoadAdvancedLab(PcModeProfile p)
    {
        dpadSocd.SelectedIndex = (int)p.DPadSocd;
        stickSocd.SelectedIndex = (int)p.LeftStickSocd;
        sniperEnabled.IsChecked = p.Sniper?.Enabled == true;
        sniperButton.SelectedItem = p.Sniper?.ModifierButton ?? "LT";
        sniperScale.Value = p.Sniper?.ScalePct ?? 55;
        sniperScaleText.Text = $"{sniperScale.Value:0}%";

        triggerEdits.Clear();
        foreach (var (k, v) in p.Triggers)
        {
            triggerEdits[k] = new TriggerTuning
            {
                Preset = v.Preset,
                RapidTrigger = v.RapidTrigger,
                ActuationPct = v.ActuationPct,
                ResetDeltaPct = v.ResetDeltaPct,
                DualStage = v.DualStage,
                HalfPullPct = v.HalfPullPct,
                FullPullPct = v.FullPullPct,
                FullPullOutput = v.FullPullOutput,
            };
        }
        LoadTriggerFields();
    }

    void LoadTriggerFields()
    {
        bool was = loading;
        loading = true;
        string key = trigChannel.SelectedIndex == 1 ? "RT" : "LT";
        var tt = triggerEdits.GetValueOrDefault(key) ?? new TriggerTuning();
        trigPreset.SelectedIndex = Math.Clamp((int)tt.Preset, 0, 4);
        rapidTrigger.IsChecked = tt.RapidTrigger;
        rapidActuation.Value = Math.Clamp(tt.ActuationPct, 2, 80);
        rapidActuationText.Text = $"{rapidActuation.Value:0}%";
        rapidReset.Value = Math.Clamp(tt.ResetDeltaPct, 2, 40);
        rapidResetText.Text = $"{rapidReset.Value:0}%";
        dualStage.IsChecked = tt.DualStage;
        dualOutput.SelectedItem = string.IsNullOrEmpty(tt.FullPullOutput) ? "None" : tt.FullPullOutput;
        loading = was;
    }

    void SaveAdvancedLab(PcModeProfile p)
    {
        p.DPadSocd = (SocdMode)Math.Clamp(dpadSocd.SelectedIndex, 0, 2);
        p.LeftStickSocd = (SocdMode)Math.Clamp(stickSocd.SelectedIndex, 0, 2);
        if (sniperEnabled.IsChecked == true)
        {
            p.Sniper = new SniperModifier
            {
                Enabled = true,
                ModifierButton = sniperButton.SelectedItem as string ?? "LT",
                ScalePct = (int)Math.Round(sniperScale.Value),
            };
        }
        else
        {
            p.Sniper = null;
        }
        p.Triggers = triggerEdits.Where(kv => !kv.Value.IsDefault).ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    void CaptureAbSlot(bool isB)
    {
        var p = link.Store.Get(PcModePage.StoreKey);
        var entry = (p.Curves.GetValueOrDefault("RightStick"), p.Sticks.GetValueOrDefault("RightStick"));
        if (isB) slotB = entry; else slotA = entry;
        if (abStatusText != null)
            abStatusText.Text = $"Captured Sample {(isB ? "B" : "A")}. Switch A / B to test blind.";
    }

    void ToggleAbBlind()
    {
        if (slotA == null || slotB == null)
        {
            if (abStatusText != null) abStatusText.Text = "Capture both Sample A and Sample B first.";
            return;
        }
        blindUsingB = !blindUsingB;
        var chosen = blindUsingB ? slotB.Value : slotA.Value;
        var p = link.Store.Get(PcModePage.StoreKey);
        if (chosen.Curve != null) p.Curves["RightStick"] = chosen.Curve; else p.Curves.Remove("RightStick");
        if (chosen.Tuning != null) p.Sticks["RightStick"] = chosen.Tuning; else p.Sticks.Remove("RightStick");
        Save(p);
        Load();
        if (abStatusText != null) abStatusText.Text = "Blind test active (label hidden). Keep testing or press Reveal A vs B.";
    }

    void RevealAb()
    {
        if (abStatusText == null) return;
        string name = blindUsingB ? "Sample B" : "Sample A";
        string aDesc = slotA is { Tuning: { } ta } ? $"DZ {ta.Inner:0.0}% / Outer {ta.Outer:0.0}%" : "Default 1:1";
        string bDesc = slotB is { Tuning: { } tb } ? $"DZ {tb.Inner:0.0}% / Outer {tb.Outer:0.0}%" : "Default 1:1";
        abStatusText.Text = $"Currently active: {name} | A: {aDesc} | B: {bDesc}";
    }
}
