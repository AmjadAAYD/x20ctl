using System.IO;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;
using X20Ctl.Zone.X10;

namespace X20Ctl.Product;

/// <summary>Bidirectional Windows BLE GATT client for EasySMX X10 (KRLY / QMacro protocol).</summary>
internal static class X10BleClient
{
    static readonly string[] Names = ["x10", "qmacro", "easysmx", "smx"];
    static bool Candidate(string? name) => name != null && Names.Any(n => name.Contains(n, StringComparison.OrdinalIgnoreCase));

    public static async Task<X10HardwareSnapshot> ReadAsync(CancellationToken cancel = default)
    {
        if (ReviewSandbox.Active) throw new IOException("Hardware access is disabled during sandbox review.");
        return await WithSessionAsync(s => s.ReadSnapshotAsync(cancel), cancel);
    }

    public static async Task<X10SyncReport> ApplyAsync(
        X10HardwareProfile profile,
        Action<string>? progress = null,
        CancellationToken cancel = default)
    {
        if (ReviewSandbox.Active) throw new IOException("Hardware access is disabled during sandbox review.");
        return await WithSessionAsync(s => s.UploadAndVerifyAsync(profile, progress, cancel), cancel);
    }

    public static async Task TestMotorAsync(bool left, int strength, CancellationToken cancel = default)
    {
        if (ReviewSandbox.Active) return;
        await WithSessionAsync(s => s.DiagnosticMotorTestAsync(left, Math.Clamp(strength, 0, 100), 500, cancel), cancel);
    }

    public static async Task StopMotorsAsync(CancellationToken cancel = default)
    {
        if (ReviewSandbox.Active) return;
        await WithSessionAsync(s => s.SendMotorPreviewAsync(0, 0, 0, cancel), cancel);
    }

    public static async Task<X10HardwareSnapshot?> FactoryResetAsync(CancellationToken cancel = default)
    {
        if (ReviewSandbox.Active) throw new IOException("Hardware access is disabled during sandbox review.");
        return await WithSessionAsync(async s =>
        {
            await s.FactoryResetAsync(cancel);
            await Task.Delay(350, cancel);
            try { return await s.ReadSnapshotAsync(cancel); }
            catch (Exception) when (!cancel.IsCancellationRequested) { return null; }
        }, cancel);
    }

    public static async Task<Krly.CalibrationEvent> RunCalibrationAsync(
        Krly.CalibrationTarget target,
        Action<Krly.CalibrationEvent>? onEvent,
        CancellationToken cancel = default)
    {
        if (ReviewSandbox.Active) throw new IOException("Hardware access is disabled during sandbox review.");
        return await WithSessionAsync(s => s.RunCalibrationAsync(target, onEvent, TimeSpan.FromSeconds(90), cancel), cancel);
    }

    public static async Task<bool> RunSelfCalibrationAsync(CancellationToken cancel = default)
    {
        if (ReviewSandbox.Active) throw new IOException("Hardware access is disabled during sandbox review.");
        return await WithSessionAsync(s => s.RunSelfCalibrationAsync(TimeSpan.FromSeconds(45), cancel), cancel);
    }

    static async Task WithSessionAsync(Func<X10ProtocolSession, Task> action, CancellationToken cancel)
    {
        await WithSessionAsync<bool>(async s => { await action(s); return true; }, cancel);
    }

    static async Task<T> WithSessionAsync<T>(Func<X10ProtocolSession, Task<T>> action, CancellationToken cancel)
    {
        await NativeBleClient.ConfigurationGate.WaitAsync(cancel);
        try
        {
            string? lastError = null;
            foreach (var address in await FindAsync(cancel))
            {
                WinRtX10Transport? transport = null;
                try
                {
                    transport = await WinRtX10Transport.ConnectAsync(address, cancel);
                    using var session = new X10ProtocolSession(transport);
                    return await action(session);
                }
                catch (Exception error) when (error is not OperationCanceledException)
                {
                    lastError = error.Message;
                }
                finally
                {
                    if (transport != null) await transport.DisposeAsync();
                }
            }
            throw new IOException(lastError ?? "No X10 found over Bluetooth. Pair it with Windows in Bluetooth mode, then try again.");
        }
        finally
        {
            NativeBleClient.ConfigurationGate.Release();
        }
    }

    /// <summary>Paired Bluetooth LE devices with an X10-like name first, then any advertising one nearby (6 s).</summary>
    static async Task<List<ulong>> FindAsync(CancellationToken cancel)
    {
        var found = new List<ulong>();
        var paired = await DeviceInformation.FindAllAsync(BluetoothLEDevice.GetDeviceSelectorFromPairingState(true));
        foreach (var info in paired.Where(i => Candidate(i.Name)))
        {
            using var device = await BluetoothLEDevice.FromIdAsync(info.Id);
            if (device != null && !found.Contains(device.BluetoothAddress)) found.Add(device.BluetoothAddress);
        }
        var watcher = new BluetoothLEAdvertisementWatcher { ScanningMode = BluetoothLEScanningMode.Active };
        watcher.Received += (_, e) =>
        {
            if (Candidate(e.Advertisement.LocalName) || e.Advertisement.ServiceUuids.Contains(Krly.Service))
                lock (found) if (!found.Contains(e.BluetoothAddress)) found.Add(e.BluetoothAddress);
        };
        watcher.Start();
        try { await Task.Delay(found.Count > 0 ? 1500 : 6000, cancel); }
        finally { watcher.Stop(); }
        lock (found) return [.. found];
    }

    private sealed class WinRtX10Transport : IX10WireTransport
    {
        private readonly BluetoothLEDevice device;
        private readonly GattDeviceService service;
        private readonly GattCharacteristic write;
        private readonly GattCharacteristic notify;
        private readonly GattWriteOption writeOption;

        public string DeviceName => string.IsNullOrWhiteSpace(device.Name) ? "EasySMX X10" : device.Name;
        public event Action<byte[]>? NotificationReceived;

        private WinRtX10Transport(
            BluetoothLEDevice device,
            GattDeviceService service,
            GattCharacteristic write,
            GattCharacteristic notify,
            GattWriteOption writeOption)
        {
            this.device = device;
            this.service = service;
            this.write = write;
            this.notify = notify;
            this.writeOption = writeOption;
            this.notify.ValueChanged += OnValueChanged;
        }

        public static async Task<WinRtX10Transport> ConnectAsync(ulong address, CancellationToken cancel)
        {
            BluetoothLEDevice? device = null;
            GattDeviceService? service = null;
            try
            {
                device = await BluetoothLEDevice.FromBluetoothAddressAsync(address).AsTask(cancel)
                    ?? throw new IOException("Cannot connect to the controller.");
                var services = await device.GetGattServicesForUuidAsync(Krly.Service, BluetoothCacheMode.Uncached).AsTask(cancel);
                if (services.Status != GattCommunicationStatus.Success || services.Services.Count == 0)
                    throw new IOException($"{device.Name}: no X10 configuration service.");
                service = services.Services[0];
                var chars = await service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached).AsTask(cancel);
                if (chars.Status != GattCommunicationStatus.Success)
                    throw new IOException("Cannot read the X10's configuration channel.");
                var write = chars.Characteristics.FirstOrDefault(c => c.Uuid == Krly.Write)
                    ?? throw new IOException("The X10's command channel is missing.");
                var notify = chars.Characteristics.FirstOrDefault(c => c.Uuid == Krly.Notify)
                    ?? throw new IOException("The X10's reply channel is missing.");

                var subscribe = notify.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Notify)
                    ? GattClientCharacteristicConfigurationDescriptorValue.Notify
                    : GattClientCharacteristicConfigurationDescriptorValue.Indicate;
                if (await notify.WriteClientCharacteristicConfigurationDescriptorAsync(subscribe).AsTask(cancel) != GattCommunicationStatus.Success)
                    throw new IOException("Cannot listen to the X10's replies.");

                var option = write.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Write)
                    ? GattWriteOption.WriteWithResponse
                    : GattWriteOption.WriteWithoutResponse;

                var result = new WinRtX10Transport(device, service, write, notify, option);
                device = null;
                service = null;
                return result;
            }
            finally
            {
                service?.Dispose();
                device?.Dispose();
            }
        }

        private void OnValueChanged(GattCharacteristic _, GattValueChangedEventArgs e)
        {
            using var data = DataReader.FromBuffer(e.CharacteristicValue);
            var raw = new byte[e.CharacteristicValue.Length];
            data.ReadBytes(raw);
            NotificationReceived?.Invoke(raw);
        }

        public async Task WriteChunkAsync(byte[] chunk, CancellationToken cancel)
        {
            using var writer = new DataWriter();
            writer.WriteBytes(chunk);
            var result = await write.WriteValueWithResultAsync(writer.DetachBuffer(), writeOption).AsTask(cancel);
            if (result.Status != GattCommunicationStatus.Success)
                throw new IOException("The X10 didn't take the request.");
        }

        public async ValueTask DisposeAsync()
        {
            notify.ValueChanged -= OnValueChanged;
            try { await notify.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.None); }
            catch { }
            service.Dispose();
            device.Dispose();
        }
    }

    /// <summary>Formats an X10 hardware snapshot as short lines for Device / X10 Native views.</summary>
    public static IEnumerable<(string Label, string Value)> Describe(X10HardwareSnapshot r)
    {
        static string Stick(Krly.Stick s) => $"{s.Min}-{s.Max}%" + (s.InvertX ? " | X inverted" : "") + (s.InvertY ? " | Y inverted" : "");
        static string Paddle(string slot, IReadOnlyDictionary<string, Krly.PaddleAssignment> paddles)
        {
            if (!paddles.TryGetValue(slot, out var p)) return $"{slot}: Factory";
            return p.Kind switch
            {
                Krly.PaddleSlotKind.Disabled => $"{slot}: Disabled",
                Krly.PaddleSlotKind.Passthrough => $"{slot} -> {p.TargetKey}",
                Krly.PaddleSlotKind.Macro => $"{slot}: Macro ({p.Steps.Count} steps{(p.Repeat ? ", repeat" : ", once")})",
                _ => $"{slot}: Factory",
            };
        }

        yield return ("Controller", r.DeviceName);
        yield return ("Battery", r.Battery is { } b ? $"{b.Percent}%" + (b.Charging ? " | charging" : "") + (b.Low ? " | low" : "") : "Not reported");
        yield return ("Firmware", $"{r.Version.Firmware} | hardware {r.Version.Hardware}");
        yield return ("Triggers", $"Left {r.Settings.LeftTrigger.Min}-{r.Settings.LeftTrigger.Max}% | Right {r.Settings.RightTrigger.Min}-{r.Settings.RightTrigger.Max}%");
        yield return ("Left stick", Stick(r.Settings.LeftStick));
        yield return ("Right stick", Stick(r.Settings.RightStick));
        yield return ("Vibration", $"Left {r.Settings.LeftMotor}% | Right {r.Settings.RightMotor}%");
        yield return ("Turbo", r.Settings.Turbo.Count == 0 ? "None" : string.Join(" | ", r.Settings.Turbo.Select(t => $"{t.Key}{(t.Automatic ? " auto" : "")} rate {t.Rate}")));
        yield return ("Buttons", r.FaceMappings.Count == 0 ? "Default" : string.Join(" | ", r.FaceMappings.Select(m => $"{m.Key} -> {m.Value}")));
        yield return ("Paddles", $"{Paddle("M1", r.Paddles)} | {Paddle("M2", r.Paddles)}");
    }
}
