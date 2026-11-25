using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;

namespace X20Ctl.Product;

/// <summary>
/// Read-only Passive BLE Advertisement Watchdog for KeyLinker controllers.
/// Tracks live RSSI (dBm) sparkline, GATT latency (ms), and advertisement dropouts while playing over 2.4 GHz or USB.
/// Automatically pauses whenever Eco Mode or ReviewSandbox is active.
/// </summary>
internal static class BleLinkWatchdog
{
    static readonly Guid KeyLinkerServiceUuid = Guid.Parse("d7f010e0-660d-46e9-96c3-19c4148bdab5");
    static readonly object Gate = new();
    static readonly Queue<short> rssiHistory = new(60);

    static BluetoothLEAdvertisementWatcher? watcher;
    static bool running;
    static short? latestRssi;
    static int advertCount;
    static int dropCount;
    static DateTimeOffset? lastSeenUtc;
    static DateTimeOffset? lastDropCountedForSeenUtc;
    static double? lastGattLatencyMs;
    static string statusText = "Watchdog stopped (start to monitor live BLE RSSI while on 2.4 GHz or USB).";

    public static event Action? Updated;

    public sealed record Snapshot(
        bool IsRunning,
        bool EcoLocked,
        short? LatestRssiDbm,
        short[] RssiHistory,
        string SignalQuality,
        int AdvertCount,
        int DropCount,
        DateTimeOffset? LastSeenUtc,
        double? LastGattLatencyMs,
        string StatusText);

    static BleLinkWatchdog()
    {
        UniversalHapticsService.Changed += () =>
        {
            if (UniversalHapticsService.IsPowerSavingActive && running)
            {
                Stop("Paused automatically while Eco mode is active.");
            }
        };
    }

    public static void RecordGattLatency(double ms)
    {
        if (!double.IsFinite(ms) || ms < 0) return;
        lock (Gate) lastGattLatencyMs = Math.Round(ms, 1);
        Updated?.Invoke();
    }

    public static bool Start()
    {
        if (ReviewSandbox.Active)
        {
            lock (Gate) statusText = "BLE watchdog disabled in review sandbox.";
            Updated?.Invoke();
            return false;
        }
        if (UniversalHapticsService.IsPowerSavingActive)
        {
            lock (Gate) statusText = "Eco mode active: turn off Eco mode to monitor BLE advertisements.";
            Updated?.Invoke();
            return false;
        }

        lock (Gate)
        {
            if (running) return true;
            try
            {
                var w = new BluetoothLEAdvertisementWatcher
                {
                    ScanningMode = BluetoothLEScanningMode.Passive,
                };
                ulong? cached = NativeBleClient.CachedAddress();
                w.Received += (_, args) => OnAdvert(args, cached);
                w.Stopped += (_, args) =>
                {
                    lock (Gate)
                    {
                        running = false;
                        if (args.Error == BluetoothError.RadioNotAvailable)
                            statusText = "PC Bluetooth radio is off or unavailable.";
                        else if (args.Error != BluetoothError.Success)
                            statusText = $"BLE watchdog stopped ({args.Error}).";
                    }
                    Updated?.Invoke();
                };
                w.Start();
                watcher = w;
                running = true;
                statusText = "Listening passively for KeyLinker BLE adverts...";
            }
            catch (Exception ex)
            {
                running = false;
                watcher = null;
                statusText = "Could not start BLE watchdog: " + ex.Message;
                Updated?.Invoke();
                return false;
            }
        }
        Updated?.Invoke();
        return true;
    }

    public static void Stop(string? reason = null)
    {
        BluetoothLEAdvertisementWatcher? w;
        lock (Gate)
        {
            w = watcher;
            watcher = null;
            running = false;
            statusText = reason ?? "Watchdog stopped.";
        }
        if (w != null)
        {
            try { w.Stop(); } catch { }
        }
        Updated?.Invoke();
    }

    public static Snapshot Read()
    {
        lock (Gate)
        {
            if (running && lastSeenUtc is { } seen && DateTimeOffset.UtcNow - seen > TimeSpan.FromSeconds(15) && lastDropCountedForSeenUtc != seen)
            {
                dropCount++;
                lastDropCountedForSeenUtc = seen;
                statusText = "BLE advert gap detected (>15 s silence). Controller may be asleep.";
            }

            string quality = latestRssi switch
            {
                null => "Waiting",
                >= -60 => "Excellent",
                >= -72 => "Good",
                >= -84 => "Fair",
                _ => "Weak",
            };

            return new Snapshot(
                running,
                UniversalHapticsService.IsPowerSavingActive,
                latestRssi,
                [.. rssiHistory],
                quality,
                advertCount,
                dropCount,
                lastSeenUtc,
                lastGattLatencyMs,
                statusText);
        }
    }

    static void OnAdvert(BluetoothLEAdvertisementReceivedEventArgs args, ulong? cached)
    {
        bool match = args.Advertisement.ServiceUuids.Contains(KeyLinkerServiceUuid)
            || (cached != null && args.BluetoothAddress == cached.Value)
            || (!string.IsNullOrWhiteSpace(args.Advertisement.LocalName)
                && (args.Advertisement.LocalName.Contains("X20", StringComparison.OrdinalIgnoreCase)
                    || args.Advertisement.LocalName.Contains("EasySMX", StringComparison.OrdinalIgnoreCase)));
        if (!match) return;

        lock (Gate)
        {
            if (!running) return;
            latestRssi = args.RawSignalStrengthInDBm;
            advertCount++;
            lastSeenUtc = DateTimeOffset.UtcNow;
            if (rssiHistory.Count >= 60) rssiHistory.Dequeue();
            rssiHistory.Enqueue(args.RawSignalStrengthInDBm);
            statusText = $"Receiving KeyLinker BLE ({args.RawSignalStrengthInDBm} dBm)";
        }
        Updated?.Invoke();
    }
}
