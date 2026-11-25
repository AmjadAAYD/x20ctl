using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Zone.Diagnostics;
using X20Ctl.Zone.PcMode;

namespace X20Ctl.Product.Diagnostics;

public sealed partial class DiagnosticsWorkbench
{
    readonly NoiseFloorStats leftNoise = new(), rightNoise = new();
    readonly TriggerLinearityStats ltLinear = new(), rtLinear = new();
    readonly TransportBenchmarkStore benchmarkStore = new();

    Border? healthBadge;
    TextBlock? healthScoreText, healthGradeText;
    Expander? labExpander;

    // Raw HID packet sniffer UI
    ComboBox? hidDevicePicker;
    Button? hidPauseBtn, hidCopyHexBtn;
    TextBlock? hidDescriptorText, hidStatusText;
    WrapPanel? hidByteGrid;

    // Jitter & Compass / Trigger linearity UI
    Canvas? jitterCanvas;
    TextBlock? jitterSummaryText, compassSummaryText, triggerLinearityText;

    // Noise floor & zoomed 15% deadzone ring UI
    Canvas? leftNoiseCanvas, rightNoiseCanvas;
    TextBlock? noiseSummaryText, noiseApplyStatusText;

    // Transport comparison UI
    StackPanel? benchmarkRowsPanel;
    TextBlock? benchmarkNoteText;

    void AttachHealthAndLab(StackPanel page, DockPanel head, Action<Button> register, bool compact)
    {
        if (compact) return;

        // Compact Health Score badge next to header title
        healthScoreText = Kit.Text("100", 20, "DS.Text", FontWeights.Bold);
        healthGradeText = Kit.Text("Health · Excellent", 12, "DS.TextSoft", FontWeights.SemiBold);
        healthBadge = new Border
        {
            CornerRadius = new(12),
            BorderThickness = new(1),
            Padding = new(14, 6, 14, 6),
            Margin = new(0, 8, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = new SolidColorBrush(ThemeCatalog.R(Color.FromArgb(95, 8, 22, 46))),
            BorderBrush = new SolidColorBrush(ThemeCatalog.R(Color.FromArgb(120, 61, 139, 255))),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Children =
                {
                    healthScoreText,
                    Kit.Text("/100", 12, "DS.TextMuted").Also(t => { t.Margin = new(3, 4, 10, 0); t.VerticalAlignment = VerticalAlignment.Center; }),
                    healthGradeText,
                }
            }
        };
        if (head.Children.Count > 1 && head.Children[^1] is StackPanel titleCol)
            titleCol.Children.Add(healthBadge);

        var labBody = new StackPanel { Margin = new(0, 12, 0, 0) };

        // Row 1: Raw HID Packet Sniffer + Packet Jitter Histogram
        var row1 = new Grid();
        row1.ColumnDefinitions.Add(new() { Width = new(1.05, GridUnitType.Star) });
        row1.ColumnDefinitions.Add(new() { Width = new(16) });
        row1.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });

        row1.Children.Add(BuildHidSnifferCard(register));
        var jitterCard = BuildJitterCard();
        Grid.SetColumn(jitterCard, 2);
        row1.Children.Add(jitterCard);
        labBody.Children.Add(row1);

        // Row 2: 10s Resting Noise Floor (Zoomed 15% Ring) + Transport Benchmark Comparison
        var row2 = new Grid { Margin = new(0, 14, 0, 0) };
        row2.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        row2.ColumnDefinitions.Add(new() { Width = new(16) });
        row2.ColumnDefinitions.Add(new() { Width = new(1.05, GridUnitType.Star) });

        row2.Children.Add(BuildNoiseFloorCard(register));
        var benchCard = BuildBenchmarkCard(register);
        Grid.SetColumn(benchCard, 2);
        row2.Children.Add(benchCard);
        labBody.Children.Add(row2);

        labExpander = new Expander
        {
            Header = "Hardware Lab & Deep Telemetry (Raw HID, Jitter, Noise Floor, Benchmarks)",
            IsExpanded = false,
            Margin = new(0, 16, 0, 0),
            Foreground = DS.Brush("DS.Text"),
            FontSize = 14.5,
            FontWeight = FontWeights.SemiBold,
            Content = labBody,
        };
        page.Children.Add(labExpander);
        RefreshBenchmarksUi();
    }

    Border BuildHidSnifferCard(Action<Button> register)
    {
        hidDevicePicker = new ComboBox { MinHeight = 34, Width = 250, FontSize = 12.5 };
        hidPauseBtn = Pill("Pause capture");
        hidPauseBtn.Height = 32;
        hidPauseBtn.Padding = new(12, 0, 12, 0);
        hidPauseBtn.Margin = new(8, 0, 0, 0);
        hidPauseBtn.Click += (_, _) =>
        {
            if (raw == null) return;
            raw.Paused = !raw.Paused;
            hidPauseBtn.Content = raw.Paused ? "Resume capture" : "Pause capture";
        };
        register(hidPauseBtn);

        hidCopyHexBtn = Pill("Copy hex");
        hidCopyHexBtn.Height = 32;
        hidCopyHexBtn.Padding = new(12, 0, 12, 0);
        hidCopyHexBtn.Margin = new(8, 0, 0, 0);
        hidCopyHexBtn.Click += (_, _) =>
        {
            var dev = SelectedRawDevice();
            if (dev == null || dev.LatestPacket.Length == 0) return;
            try
            {
                Clipboard.SetText(BitConverter.ToString(dev.LatestPacket).Replace('-', ' '));
                if (hidStatusText != null) hidStatusText.Text = $"Copied {dev.LatestPacket.Length} HID bytes.";
            }
            catch (System.Runtime.InteropServices.ExternalException) { }
        };
        register(hidCopyHexBtn);

        hidDescriptorText = Wrap("Descriptor: waiting for HID reports...", 12.5, "DS.TextSoft", 8);
        hidStatusText = Wrap("Read-only Windows Raw Input listener. Highlights bytes changed in the last 450 ms.", 12, "DS.TextMuted", 4);
        hidByteGrid = new WrapPanel { Margin = new(0, 10, 0, 0) };

        var controls = new WrapPanel { Children = { hidDevicePicker, hidPauseBtn, hidCopyHexBtn } };
        return Kit.Panel("Raw USB / HID Packet Sniffer", "100% read-only report inspector", null,
            new StackPanel { Children = { controls, hidDescriptorText, hidStatusText, hidByteGrid } });
    }

    Border BuildJitterCard()
    {
        jitterCanvas = new Canvas
        {
            Width = 360,
            Height = 96,
            ClipToBounds = true,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new(0, 6, 0, 0),
        };
        jitterSummaryText = Wrap("Interval stats: move a stick or press buttons to collect HID timing samples.", 12.5, "DS.TextSoft", 8);
        compassSummaryText = Wrap("8-point compass symmetry: rotate sticks slowly around edge.", 12, "DS.TextMuted", 6);
        triggerLinearityText = Wrap("Trigger linearity & dead-band: squeeze LT and RT slowly.", 12, "DS.TextMuted", 4);

        return Kit.Panel("Packet Timing & Jitter Histogram", "12-bucket inter-report delta (ms) + compass & trigger linearity", null,
            new StackPanel { Children = { jitterCanvas, jitterSummaryText, compassSummaryText, triggerLinearityText } });
    }

    Border BuildNoiseFloorCard(Action<Button> register)
    {
        leftNoiseCanvas = new Canvas { Width = 140, Height = 140, ClipToBounds = true };
        rightNoiseCanvas = new Canvas { Width = 140, Height = 140, ClipToBounds = true };

        var ringsRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children =
            {
                new StackPanel { Children = { Kit.Text("Left (+/-15%)", 12, "DS.TextSoft"), leftNoiseCanvas } },
                new StackPanel { Margin = new(18, 0, 0, 0), Children = { Kit.Text("Right (+/-15%)", 12, "DS.TextSoft"), rightNoiseCanvas } },
            }
        };

        noiseSummaryText = Wrap("Leave both sticks untouched to measure resting noise floor.", 12.5, "DS.TextSoft", 8);
        noiseApplyStatusText = Wrap("", 12, "DS.TextMuted", 6);

        var applyBtn = Pill("Apply recommended dead zones");
        applyBtn.Height = 32;
        applyBtn.Margin = new(0, 8, 0, 0);
        applyBtn.HorizontalAlignment = HorizontalAlignment.Left;
        applyBtn.IsEnabled = link != null;
        applyBtn.Click += (_, _) => ApplyRecommendedNoiseDeadZones();
        register(applyBtn);

        return Kit.Panel("Resting Noise Floor & Deadzone Zoom", "Magnified +/-15% center scatter ring", null,
            new StackPanel { Children = { ringsRow, noiseSummaryText, applyBtn, noiseApplyStatusText } });
    }

    Border BuildBenchmarkCard(Action<Button> register)
    {
        var actions = new WrapPanel();
        foreach (var label in new[] { "Wired", "2.4 GHz", "Bluetooth" })
        {
            string mode = label;
            var b = Pill($"Save as {mode}");
            b.Height = 32;
            b.Padding = new(12, 0, 12, 0);
            b.Margin = new(0, 0, 8, 6);
            b.Click += (_, _) => CaptureTransportBenchmark(mode);
            register(b);
            actions.Children.Add(b);
        }
        var clear = Pill("Clear");
        clear.Height = 32;
        clear.Padding = new(12, 0, 12, 0);
        clear.Margin = new(0, 0, 0, 6);
        clear.Click += (_, _) =>
        {
            benchmarkStore.Clear();
            RefreshBenchmarksUi();
            if (benchmarkNoteText != null) benchmarkNoteText.Text = "Saved transport benchmarks cleared.";
        };
        register(clear);
        actions.Children.Add(clear);

        benchmarkNoteText = Wrap("Save a snapshot per transport to compare Hz, jitter, circularity, and health score.", 12, "DS.TextMuted", 4);
        benchmarkRowsPanel = new StackPanel { Margin = new(0, 10, 0, 0) };

        return Kit.Panel("Transport Benchmark Comparison", "Side-by-side Wired vs 2.4 GHz vs Bluetooth", null,
            new StackPanel { Children = { actions, benchmarkNoteText, benchmarkRowsPanel } });
    }

    void ApplyRecommendedNoiseDeadZones()
    {
        if (link == null || noiseApplyStatusText == null) return;
        if (link.Preferences is { PcModeNoticeSeen: false })
        {
            noiseApplyStatusText.Text = "Open Curves -> Advanced once first to review the PC mode notice.";
            return;
        }
        double lRec, rRec;
        lock (gate)
        {
            lRec = leftNoise.SampleCount >= 10 ? leftNoise.RecommendedDeadZonePct : 0;
            rRec = rightNoise.SampleCount >= 10 ? rightNoise.RecommendedDeadZonePct : 0;
        }
        if (lRec == 0 && rRec == 0)
        {
            noiseApplyStatusText.Text = "Collect at least 10 resting samples first.";
            return;
        }
        try
        {
            var profile = link.Store.Get(PcModePage.StoreKey);
            if (lRec > 0)
            {
                var ltune = profile.Sticks.GetValueOrDefault("LeftStick") ?? new StickTuning();
                ltune.Inner = lRec; ltune.Raw = false; if (ltune.Outer <= lRec) ltune.Outer = 100;
                profile.Sticks["LeftStick"] = ltune;
            }
            if (rRec > 0)
            {
                var rtune = profile.Sticks.GetValueOrDefault("RightStick") ?? new StickTuning();
                rtune.Inner = rRec; rtune.Raw = false; if (rtune.Outer <= rRec) rtune.Outer = 100;
                profile.Sticks["RightStick"] = rtune;
            }
            profile.Validate();
            link.Store.Save(PcModePage.StoreKey, profile);
            link.Service.Reload();
            noiseApplyStatusText.Text = $"Applied dead zones (L {lRec:0.0}%, R {rRec:0.0}%) to PC mode.";
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            noiseApplyStatusText.Text = "Could not save dead zones: " + ex.Message;
        }
    }

    void CaptureTransportBenchmark(string transportLabel)
    {
        double now = clock.Elapsed.TotalMilliseconds;
        var dev = SelectedRawDevice();
        double avgHz = dev != null && dev.AvgMs > 0 ? Math.Round(1000.0 / dev.AvgMs, 1) : changes.Current(now);
        int peakHz = dev?.Peak ?? changes.Peak;

        double? lc, rc, ln, rn;
        int bounces;
        lock (gate)
        {
            lc = left.CircularityError;
            rc = right.CircularityError;
            ln = leftNoise.SampleCount >= 10 ? leftNoise.PeakRadiusPct : null;
            rn = rightNoise.SampleCount >= 10 ? rightNoise.PeakRadiusPct : null;
            bounces = Buttons.Sum(b => presses[b.Mask].Bounces);
        }
        var health = ControllerHealthScore.Evaluate(lc, rc, ln, rn, bounces, dev?.StdDevMs, null);
        var snap = new TransportBenchmarkSnapshot(
            Guid.NewGuid().ToString("N")[..8],
            DateTimeOffset.UtcNow,
            transportLabel,
            dev?.Name ?? assignedModel,
            avgHz,
            peakHz,
            Math.Round(dev?.AvgMs ?? 0, 2),
            Math.Round(dev?.P99Ms ?? 0, 2),
            Math.Round(dev?.StdDevMs ?? 0, 2),
            lc.HasValue ? Math.Round(lc.Value, 1) : null,
            rc.HasValue ? Math.Round(rc.Value, 1) : null,
            ln.HasValue ? Math.Round(ln.Value, 2) : null,
            rn.HasValue ? Math.Round(rn.Value, 2) : null,
            health.Score);

        benchmarkStore.Add(snap);
        RefreshBenchmarksUi();
        if (benchmarkNoteText != null)
            benchmarkNoteText.Text = $"Saved {transportLabel} snapshot ({snap.AvgHz:0} Hz, Health {snap.HealthScore}/100).";
    }

    void RefreshBenchmarksUi()
    {
        if (benchmarkRowsPanel == null) return;
        benchmarkRowsPanel.Children.Clear();
        var items = benchmarkStore.Items;
        if (items.Count == 0)
        {
            benchmarkRowsPanel.Children.Add(Wrap("No saved snapshots yet.", 12.5, "DS.TextMuted", 2));
            return;
        }
        foreach (var item in items.Take(6))
        {
            string line = $"{item.TransportLabel} · {item.CapturedUtc.ToLocalTime():HH:mm} · {item.AvgHz:0.#} Hz (peak {item.PeakHz}) · {item.AvgIntervalMs:0.00} ms avg / {item.P99Ms:0.00} ms p99 · Jitter {item.JitterStdDevMs:0.00} ms · Score {item.HealthScore}/100";
            if (item.LeftCircularityPct is { } lc) line += $" · L Circ {lc:0.1}%";
            if (item.RightCircularityPct is { } rc) line += $" · R Circ {rc:0.1}%";
            benchmarkRowsPanel.Children.Add(new Border
            {
                CornerRadius = new(8),
                BorderThickness = new(1),
                BorderBrush = Edge(40),
                Background = Dark,
                Padding = new(10, 6, 10, 6),
                Margin = new(0, 0, 0, 6),
                Child = Kit.Text(line, 12, "DS.TextSoft").Also(t => { t.TextWrapping = TextWrapping.Wrap; t.TextTrimming = TextTrimming.None; })
            });
        }
    }

    RawRate.DetailedDevice? SelectedRawDevice()
    {
        var list = raw?.DetailedDevices();
        if (list is not { Count: > 0 }) return null;
        if (hidDevicePicker?.SelectedItem is string picked)
            return list.FirstOrDefault(d => d.Name == picked) ?? list[0];
        return list[0];
    }

    void RefreshLab(IReadOnlyList<RawRate.DetailedDevice>? rawDetailed)
    {
        // Always update header Health Score badge even while expander is collapsed
        double? lc, rc, ln, rn;
        int bounces;
        double[] lCompass, rCompass;
        double lRms, lPeak, lRec, rRms, rPeak, rRec;
        IReadOnlyList<(double X, double Y)> lPts, rPts;
        int lSamples, rSamples;
        double ltStart, ltEnd, ltCov, rtStart, rtEnd, rtCov;
        lock (gate)
        {
            lc = left.CircularityError;
            rc = right.CircularityError;
            lSamples = leftNoise.SampleCount;
            rSamples = rightNoise.SampleCount;
            ln = lSamples >= 10 ? leftNoise.PeakRadiusPct : null;
            rn = rSamples >= 10 ? rightNoise.PeakRadiusPct : null;
            lRms = leftNoise.RmsPct; lPeak = leftNoise.PeakRadiusPct; lRec = leftNoise.RecommendedDeadZonePct;
            rRms = rightNoise.RmsPct; rPeak = rightNoise.PeakRadiusPct; rRec = rightNoise.RecommendedDeadZonePct;
            lPts = leftNoise.Points; rPts = rightNoise.Points;
            bounces = Buttons.Sum(b => presses[b.Mask].Bounces);
            lCompass = left.CompassReach();
            rCompass = right.CompassReach();
            ltStart = ltLinear.StartDeadBandPct; ltEnd = ltLinear.EndDeadBandPct; ltCov = ltLinear.CoveragePct;
            rtStart = rtLinear.StartDeadBandPct; rtEnd = rtLinear.EndDeadBandPct; rtCov = rtLinear.CoveragePct;
        }

        var selectedRaw = rawDetailed is { Count: > 0 }
            ? (hidDevicePicker?.SelectedItem is string p ? rawDetailed.FirstOrDefault(d => d.Name == p) : null) ?? rawDetailed[0]
            : null;
        var health = ControllerHealthScore.Evaluate(lc, rc, ln, rn, bounces, selectedRaw?.StdDevMs, null);
        if (healthScoreText != null && healthGradeText != null && healthBadge != null)
        {
            healthScoreText.Text = health.Score.ToString();
            healthGradeText.Text = $"Health · {health.Grade}";
            healthBadge.ToolTip = string.Join(Environment.NewLine, health.Factors);
        }

        if (labExpander is not { IsExpanded: true }) return;

        // Populate device selector without fighting user selection
        if (hidDevicePicker != null && rawDetailed != null)
        {
            var names = rawDetailed.Select(d => d.Name).ToList();
            if (!names.SequenceEqual(hidDevicePicker.Items.Cast<string>()))
            {
                string? current = hidDevicePicker.SelectedItem as string;
                hidDevicePicker.ItemsSource = names;
                hidDevicePicker.SelectedItem = current != null && names.Contains(current) ? current : names.FirstOrDefault();
            }
        }

        // Raw HID Sniffer
        if (hidDescriptorText != null && hidByteGrid != null && hidStatusText != null)
        {
            if (selectedRaw == null)
            {
                hidDescriptorText.Text = "Descriptor: waiting for HID reports...";
                hidByteGrid.Children.Clear();
            }
            else
            {
                hidDescriptorText.Text = selectedRaw.Descriptor is { } d
                    ? $"VID {d.VendorId:X4} : PID {d.ProductId:X4} · Usage {d.UsagePage:X4}:{d.Usage:X4} · In {d.InputBytes}B / Out {d.OutputBytes}B / Feat {d.FeatureBytes}B · Btns {d.InputButtonCaps} / Vals {d.InputValueCaps}"
                    : $"{selectedRaw.Name} · Report {selectedRaw.ReportSize}B · Total {selectedRaw.TotalPackets:N0} packets";
                hidStatusText.Text = $"Packets: {selectedRaw.TotalPackets:N0} · {selectedRaw.Current}/s (peak {selectedRaw.Peak}/s)";
                hidByteGrid.Children.Clear();
                for (int i = 0; i < selectedRaw.LatestPacket.Length; i++)
                {
                    bool changed = i < selectedRaw.ChangedMask.Length && selectedRaw.ChangedMask[i];
                    hidByteGrid.Children.Add(new Border
                    {
                        Width = 32,
                        Height = 26,
                        Margin = new(0, 0, 4, 4),
                        CornerRadius = new(5),
                        BorderThickness = new(1),
                        Background = changed ? Blue(130) : Dark,
                        BorderBrush = changed ? DS.Brush("DS.AccentHi") : Edge(45),
                        ToolTip = $"Byte [{i}] = 0x{selectedRaw.LatestPacket[i]:X2} ({selectedRaw.LatestPacket[i]})",
                        Child = new TextBlock
                        {
                            Text = $"{selectedRaw.LatestPacket[i]:X2}",
                            FontFamily = new FontFamily("Consolas, Cascadia Mono, Courier New"),
                            FontSize = 11.5,
                            FontWeight = changed ? FontWeights.Bold : FontWeights.Normal,
                            Foreground = changed ? DS.Brush("DS.Text") : DS.Brush("DS.TextSoft"),
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center,
                        }
                    });
                }
            }
        }

        // Jitter Histogram
        if (jitterCanvas != null && jitterSummaryText != null)
        {
            jitterCanvas.Children.Clear();
            if (selectedRaw != null && selectedRaw.Histogram.Length == JitterStats.BucketCount)
            {
                int peakBucket = Math.Max(1, selectedRaw.Histogram.Max());
                double barW = jitterCanvas.Width / JitterStats.BucketCount;
                for (int i = 0; i < JitterStats.BucketCount; i++)
                {
                    double h = Math.Max(3, selectedRaw.Histogram[i] / (double)peakBucket * 72.0);
                    var rect = new Rectangle
                    {
                        Width = Math.Max(6, barW - 6),
                        Height = h,
                        RadiusX = 3,
                        RadiusY = 3,
                        Fill = i >= 8 ? DS.Brush("DS.Warn") : Blue(210),
                        ToolTip = $"{JitterStats.BucketLabels[i]} ms: {selectedRaw.Histogram[i]} intervals",
                    };
                    Canvas.SetLeft(rect, i * barW + 3);
                    Canvas.SetTop(rect, 78 - h);
                    jitterCanvas.Children.Add(rect);
                }
                jitterSummaryText.Text = $"Min {selectedRaw.MinMs:0.00} ms · Avg {selectedRaw.AvgMs:0.00} ms · P99 {selectedRaw.P99Ms:0.00} ms · Max {selectedRaw.MaxMs:0.00} ms · StdDev {selectedRaw.StdDevMs:0.00} ms · Spikes {selectedRaw.SpikeCount}";
            }
            else
            {
                jitterSummaryText.Text = "Interval stats: awaiting Raw Input packets...";
            }
        }

        if (compassSummaryText != null)
            compassSummaryText.Text = $"Compass E/NE/N/NW/W/SW/S/SE · L [{string.Join("/", lCompass.Select(v => v.ToString("0")))}%] · R [{string.Join("/", rCompass.Select(v => v.ToString("0")))}%]";
        if (triggerLinearityText != null)
            triggerLinearityText.Text = $"Triggers · LT start {ltStart:0.0}% / end {ltEnd:0.0}% ({ltCov:0}% span) · RT start {rtStart:0.0}% / end {rtEnd:0.0}% ({rtCov:0}% span)";

        // Resting Noise Floor zoom rings (+/-15%)
        DrawNoiseRing(leftNoiseCanvas, lPts, lRec);
        DrawNoiseRing(rightNoiseCanvas, rPts, rRec);
        if (noiseSummaryText != null)
        {
            noiseSummaryText.Text = lSamples == 0 && rSamples == 0
                ? "Release both sticks to collect resting scatter samples within +/-15%."
                : $"Left ({lSamples} pts): Peak {lPeak:0.2}%, RMS {lRms:0.2}% -> Safe DZ {lRec:0.1}%\nRight ({rSamples} pts): Peak {rPeak:0.2}%, RMS {rRms:0.2}% -> Safe DZ {rRec:0.1}%";
        }
    }

    void DrawNoiseRing(Canvas? canvas, IReadOnlyList<(double X, double Y)> pts, double recPct)
    {
        if (canvas == null) return;
        canvas.Children.Clear();
        const double half = 70, maxPct = 15.0, scale = 58.0 / maxPct;
        canvas.Children.Add(new Ellipse { Width = 116, Height = 116, Stroke = Edge(55), StrokeThickness = 1 }.Also(e => { Canvas.SetLeft(e, half - 58); Canvas.SetTop(e, half - 58); }));
        canvas.Children.Add(new Line { X1 = half, X2 = half, Y1 = half - 58, Y2 = half + 58, Stroke = Edge(35), StrokeThickness = 1 });
        canvas.Children.Add(new Line { X1 = half - 58, X2 = half + 58, Y1 = half, Y2 = half, Stroke = Edge(35), StrokeThickness = 1 });
        if (recPct > 0)
        {
            double r = Math.Min(15.0, recPct) * scale;
            var dz = new Ellipse { Width = r * 2, Height = r * 2, Stroke = DS.Brush("DS.AccentHi"), StrokeThickness = 1.4 };
            Canvas.SetLeft(dz, half - r);
            Canvas.SetTop(dz, half - r);
            canvas.Children.Add(dz);
        }
        foreach (var (x, y) in pts)
        {
            double px = half + Math.Clamp(x, -maxPct, maxPct) * scale;
            double py = half - Math.Clamp(y, -maxPct, maxPct) * scale;
            var dot = new Ellipse { Width = 3, Height = 3, Fill = DS.Brush("DS.Warn") };
            Canvas.SetLeft(dot, px - 1.5);
            Canvas.SetTop(dot, py - 1.5);
            canvas.Children.Add(dot);
        }
    }
}
