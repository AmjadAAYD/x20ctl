using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Zone.Capture;
using X20Ctl.Zone.PcMode;
using X20Ctl.Zone.Telemetry;

namespace X20Ctl.Product;

public sealed partial class DeviceWorkbench
{
    readonly OnboardSnapshotStore snapshotStore = new();
    OnboardHardwareSnapshot? latestOnboard;

    TextBlock? bleWatchdogSummary, onboardExplorerText, batteryLogSummary;
    Polyline? rssiSparkline, batterySparkline;
    Button? bleWatchdogBtn, onboardReadBtn, onboardSaveBtn, onboardDiffSavedBtn, onboardDiffPcBtn;

    void AttachInternalsExpander(StackPanel overview, string modelId, FrameworkElement resources, Action<Button> register)
    {
        var stack = new StackPanel { Margin = new(0, 10, 0, 0) };

        // 1. BLE Dual-Link Watchdog
        bleWatchdogSummary = ManagementVisuals.Copy("Watchdog idle. Click Start to monitor live KeyLinker BLE RSSI and GATT latency.", 13);
        rssiSparkline = new Polyline
        {
            Stroke = DS.Brush("DS.AccentHi"),
            StrokeThickness = 1.8,
            Height = 44,
            Width = 320,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        bleWatchdogBtn = ManagementVisuals.Action(resources, "Start BLE Watchdog", ToggleBleWatchdog, register);
        bleWatchdogBtn.Height = 32;
        bleWatchdogBtn.MinHeight = 0;
        var bleBody = new StackPanel
        {
            Children =
            {
                bleWatchdogSummary,
                new Border
                {
                    CornerRadius = new(8),
                    Padding = new(8, 6, 8, 6),
                    Margin = new(0, 8, 0, 8),
                    Background = new SolidColorBrush(ThemeCatalog.R(Color.FromArgb(75, 4, 10, 24))),
                    Child = rssiSparkline,
                },
                bleWatchdogBtn,
            }
        };
        stack.Children.Add(Kit.Panel("BLE Dual-Link Watchdog", "Live RSSI (dBm) & GATT latency while on 2.4 GHz or USB", null, bleBody));

        // 2. Read-Only KeyLinker Config Explorer
        onboardExplorerText = Kit.Text(
            modelId == "x20_pro"
                ? "Read-only KeyLinker explorer (0x91, 0xB6, 0xB1, 0xB2, 0xB5 M1-M6, 0xB7 RGB). Never modifies controller memory."
                : "Read-only KeyLinker explorer is enabled on verified X20 Pro firmware 1.44.",
            12.5,
            "DS.TextSoft").Also(t =>
        {
            t.TextWrapping = TextWrapping.Wrap;
            t.FontFamily = new FontFamily("Consolas, Cascadia Mono, Courier New");
            t.Margin = new(0, 6, 0, 0);
        });

        onboardReadBtn = ManagementVisuals.Action(resources, "Read Onboard Memory", async () => await ReadOnboardSnapshotAsync(), register);
        onboardSaveBtn = ManagementVisuals.Action(resources, "Save Snapshot", SaveLatestSnapshot, register);
        onboardDiffSavedBtn = ManagementVisuals.Action(resources, "Diff vs Saved", DiffAgainstSaved, register);
        onboardDiffPcBtn = ManagementVisuals.Action(resources, "Diff vs PC Mode", DiffAgainstPcMode, register);
        foreach (var b in new[] { onboardReadBtn, onboardSaveBtn, onboardDiffSavedBtn, onboardDiffPcBtn })
        {
            b.Height = 30;
            b.MinHeight = 0;
            b.FontSize = 12;
            b.Margin = new(0, 0, 6, 6);
            b.IsEnabled = modelId == "x20_pro" && !ReviewSandbox.Active;
        }
        onboardSaveBtn.IsEnabled = false;
        onboardDiffSavedBtn.IsEnabled = false;
        onboardDiffPcBtn.IsEnabled = false;

        var explorerPanel = Kit.Panel(
            "Onboard KeyLinker Explorer (Read-Only)",
            "Inspect & diff controller blocks without writing",
            null,
            new StackPanel
            {
                Children =
                {
                    new WrapPanel { Children = { onboardReadBtn, onboardSaveBtn, onboardDiffSavedBtn, onboardDiffPcBtn } },
                    onboardExplorerText,
                }
            });
        explorerPanel.Margin = new(0, 12, 0, 0);
        stack.Children.Add(explorerPanel);

        // 3. Battery & Power-Draw Session Log
        batteryLogSummary = ManagementVisuals.Copy("Loading battery session telemetry...", 13);
        batterySparkline = new Polyline
        {
            Stroke = DS.Brush("DS.Success"),
            StrokeThickness = 1.8,
            Height = 40,
            Width = 320,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        var clearLog = ManagementVisuals.Action(resources, "Clear battery log", () =>
        {
            ProductHost.BatteryLog.Clear();
            RefreshBatteryLogUi();
        }, register);
        clearLog.Height = 30;
        clearLog.MinHeight = 0;
        clearLog.Margin = new(0, 8, 0, 0);

        var batPanel = Kit.Panel(
            "Battery & Power-Draw Log",
            "Correlates battery % with Eco mode, RGB brightness, and Haptics",
            null,
            new StackPanel
            {
                Children =
                {
                    batteryLogSummary,
                    new Border
                    {
                        CornerRadius = new(8),
                        Padding = new(8, 6, 8, 6),
                        Margin = new(0, 8, 0, 0),
                        Background = new SolidColorBrush(ThemeCatalog.R(Color.FromArgb(75, 4, 10, 24))),
                        Child = batterySparkline,
                    },
                    clearLog,
                }
            });
        batPanel.Margin = new(0, 12, 0, 0);
        stack.Children.Add(batPanel);

        var expander = new Expander
        {
            Header = "Hardware Internals & Watchdog",
            IsExpanded = false,
            Margin = new(0, 12, 0, 0),
            Foreground = DS.Brush("DS.Text"),
            FontSize = 14.5,
            FontWeight = FontWeights.SemiBold,
            Content = stack,
        };
        overview.Children.Add(expander);

        void OnWatchdog() => Body.Dispatcher.BeginInvoke(RefreshBleWatchdogUi);
        Body.Loaded += (_, _) =>
        {
            BleLinkWatchdog.Updated -= OnWatchdog;
            BleLinkWatchdog.Updated += OnWatchdog;
            RefreshBleWatchdogUi();
            RefreshBatteryLogUi();
        };
        Body.Unloaded += (_, _) => BleLinkWatchdog.Updated -= OnWatchdog;
    }

    void ToggleBleWatchdog()
    {
        var snap = BleLinkWatchdog.Read();
        if (snap.IsRunning) BleLinkWatchdog.Stop();
        else BleLinkWatchdog.Start();
        RefreshBleWatchdogUi();
    }

    void RefreshBleWatchdogUi()
    {
        if (bleWatchdogSummary == null || bleWatchdogBtn == null || rssiSparkline == null) return;
        var snap = BleLinkWatchdog.Read();
        bleWatchdogBtn.Content = snap.IsRunning ? "Stop BLE Watchdog" : "Start BLE Watchdog";
        bleWatchdogBtn.IsEnabled = !snap.EcoLocked && !ReviewSandbox.Active;
        string rssi = snap.LatestRssiDbm is { } dbm ? $"{dbm} dBm ({snap.SignalQuality})" : "no signal";
        string gatt = snap.LastGattLatencyMs is { } ms ? $"{ms:0.0} ms GATT" : "no GATT sample";
        bleWatchdogSummary.Text = $"{snap.StatusText}\nRSSI {rssi} · {gatt} · Adverts {snap.AdvertCount} · Gaps {snap.DropCount}";

        var pts = new PointCollection();
        if (snap.RssiHistory.Length > 0)
        {
            double stepX = snap.RssiHistory.Length == 1 ? 0 : 310.0 / (snap.RssiHistory.Length - 1);
            for (int i = 0; i < snap.RssiHistory.Length; i++)
            {
                double norm = Math.Clamp((snap.RssiHistory[i] + 100.0) / 60.0, 0.0, 1.0); // -100 dBm -> 0, -40 dBm -> 1
                pts.Add(new Point(4 + i * stepX, 40 - norm * 36));
            }
        }
        rssiSparkline.Points = pts;
    }

    async Task ReadOnboardSnapshotAsync()
    {
        if (onboardExplorerText == null || onboardReadBtn == null) return;
        onboardReadBtn.IsEnabled = false;
        onboardExplorerText.Text = "Reading verified KeyLinker blocks over BLE (read-only)...";
        try
        {
            latestOnboard = await LightingBleClient.ReadDiagnosticSnapshotAsync();
            onboardExplorerText.Text = string.Join(Environment.NewLine, OnboardSnapshotDiff.DecodeSummary(latestOnboard));
            if (onboardSaveBtn != null) onboardSaveBtn.IsEnabled = true;
            if (onboardDiffSavedBtn != null) onboardDiffSavedBtn.IsEnabled = snapshotStore.Items.Count > 0;
            if (onboardDiffPcBtn != null) onboardDiffPcBtn.IsEnabled = true;
        }
        catch (Exception ex)
        {
            onboardExplorerText.Text = "Read failed: " + ex.Message;
        }
        finally
        {
            onboardReadBtn.IsEnabled = !ReviewSandbox.Active;
        }
    }

    void SaveLatestSnapshot()
    {
        if (latestOnboard == null || onboardExplorerText == null) return;
        snapshotStore.SaveSnapshot(latestOnboard);
        if (onboardDiffSavedBtn != null) onboardDiffSavedBtn.IsEnabled = true;
        onboardExplorerText.Text += Environment.NewLine + $"Saved baseline snapshot ({snapshotStore.Items.Count} stored).";
    }

    void DiffAgainstSaved()
    {
        if (latestOnboard == null || onboardExplorerText == null) return;
        var saved = snapshotStore.Items.FirstOrDefault();
        if (saved == null) { onboardExplorerText.Text = "Save a snapshot first to diff."; return; }
        var diff = OnboardSnapshotDiff.Diff(saved, latestOnboard);
        onboardExplorerText.Text = diff.Count == 0
            ? $"No byte differences vs saved snapshot ({saved.CapturedUtc.ToLocalTime():HH:mm:ss})."
            : $"Differences vs {saved.CapturedUtc.ToLocalTime():HH:mm:ss} ({diff.Count} bytes):" + Environment.NewLine
              + string.Join(Environment.NewLine, diff.Take(24).Select(d => $"  {d.BlockName} [+{d.ByteIndex}]: 0x{d.Before:X2} -> 0x{d.After:X2}"));
    }

    void DiffAgainstPcMode()
    {
        if (latestOnboard == null || onboardExplorerText == null) return;
        try
        {
            var store = new PcModeStore(PcModeStore.DefaultPath);
            store.Load();
            var profile = store.Get(PcModePage.StoreKey);
            var lines = OnboardSnapshotDiff.CompareWithPcMode(latestOnboard, profile);
            onboardExplorerText.Text = "Onboard Memory vs PC Mode Profile:" + Environment.NewLine + string.Join(Environment.NewLine, lines);
        }
        catch (Exception ex)
        {
            onboardExplorerText.Text = "Comparison unavailable: " + ex.Message;
        }
    }

    internal void RefreshBatteryLogUi()
    {
        if (batteryLogSummary == null || batterySparkline == null) return;
        var summary = ProductHost.BatteryLog.Summarize();
        batteryLogSummary.Text = summary.Headline;
        var list = ProductHost.BatteryLog.Samples;
        var pts = new PointCollection();
        if (list.Count > 0)
        {
            var tail = list.TakeLast(40).ToArray();
            double stepX = tail.Length == 1 ? 0 : 310.0 / (tail.Length - 1);
            for (int i = 0; i < tail.Length; i++)
            {
                double y = 36 - (tail[i].PercentEstimate / 100.0) * 32;
                pts.Add(new Point(4 + i * stepX, y));
            }
        }
        batterySparkline.Points = pts;
    }
}
