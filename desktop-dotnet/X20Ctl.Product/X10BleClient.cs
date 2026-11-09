using System.IO;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;
using X20Ctl.Zone.X10;

namespace X20Ctl.Product;

/// <summary>The X10's own settings over Bluetooth (KRLY / QMacro, decoded by Edgard's EasyGamepad). Read-only: it asks for
/// the version, battery, settings and button mappings, and sends nothing that changes the controller. One request at a
/// time, replies matched by command; a timeout is indeterminate and ends the session (no retry on the same link).</summary>
internal static class X10BleClient
{
    public sealed record Reading(string Name, Krly.Version Version, Krly.Battery? Battery, Krly.Settings Settings, IReadOnlyDictionary<string, string> Mappings);

    static readonly string[] Names = ["x10", "qmacro", "easysmx", "smx"];
    static bool Candidate(string? name) => name != null && Names.Any(n => name.Contains(n, StringComparison.OrdinalIgnoreCase));

    public static async Task<Reading> ReadAsync(CancellationToken cancel = default)
    {
        await NativeBleClient.ConfigurationGate.WaitAsync(cancel);
        try
        {
            string? lastError = null;
            foreach (var address in await FindAsync(cancel))
            {
                try { return await SessionAsync(address, cancel); }
                catch (Exception error) when (error is not OperationCanceledException) { lastError = error.Message; }
            }
            throw new IOException(lastError ?? "No X10 found over Bluetooth. Pair it with Windows in Bluetooth mode, then try again.");
        }
        finally { NativeBleClient.ConfigurationGate.Release(); }
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

    static async Task<Reading> SessionAsync(ulong address, CancellationToken cancel)
    {
        using var device = await BluetoothLEDevice.FromBluetoothAddressAsync(address) ?? throw new IOException("Cannot connect to the controller.");
        var services = await device.GetGattServicesForUuidAsync(Krly.Service, BluetoothCacheMode.Uncached);
        if (services.Status != GattCommunicationStatus.Success || services.Services.Count == 0) throw new IOException($"{device.Name}: no X10 configuration service.");
        using var service = services.Services[0];
        var chars = await service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
        if (chars.Status != GattCommunicationStatus.Success) throw new IOException("Cannot read the X10's configuration channel.");
        var write = chars.Characteristics.FirstOrDefault(c => c.Uuid == Krly.Write) ?? throw new IOException("The X10's command channel is missing.");
        var notify = chars.Characteristics.FirstOrDefault(c => c.Uuid == Krly.Notify) ?? throw new IOException("The X10's reply channel is missing.");
        var option = write.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Write) ? GattWriteOption.WriteWithResponse : GattWriteOption.WriteWithoutResponse;

        var reader = new Krly.FrameReader();
        Func<byte[], bool>? waiting = null; TaskCompletionSource? done = null; Krly.Battery? battery = null;
        void Received(GattCharacteristic _, GattValueChangedEventArgs e)
        {
            using var data = DataReader.FromBuffer(e.CharacteristicValue);
            var raw = new byte[e.CharacteristicValue.Length]; data.ReadBytes(raw);
            foreach (var frame in reader.Feed(raw))
            {
                if (frame[1] == Krly.QueryBattery && Krly.ParseBattery(frame) is { } b) battery = b; // also sent unasked
                try { if (waiting?.Invoke(frame) == true) done?.TrySetResult(); }
                catch (Exception error) { done?.TrySetException(error); }
            }
        }
        byte sequence = 0;
        async Task Ask(byte command, byte[] payload, Func<byte[], bool> accept)
        {
            done = new(TaskCreationOptions.RunContinuationsAsynchronously); waiting = accept;
            var frame = Krly.Frame(command, payload, sequence++);
            for (int at = 0; at < frame.Length; at += Krly.ChunkSize)
            {
                using var writer = new DataWriter(); writer.WriteBytes(frame[at..Math.Min(frame.Length, at + Krly.ChunkSize)]);
                var result = await write.WriteValueWithResultAsync(writer.DetachBuffer(), option);
                if (result.Status != GattCommunicationStatus.Success) throw new IOException("The X10 didn't take the request.");
            }
            try { await done.Task.WaitAsync(TimeSpan.FromSeconds(5), cancel); }
            catch (TimeoutException) { throw new TimeoutException("The X10 didn't answer in time. Nothing was retried."); }
            finally { waiting = null; }
        }

        notify.ValueChanged += Received;
        try
        {
            var subscribe = notify.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Notify) ? GattClientCharacteristicConfigurationDescriptorValue.Notify : GattClientCharacteristicConfigurationDescriptorValue.Indicate;
            if (await notify.WriteClientCharacteristicConfigurationDescriptorAsync(subscribe) != GattCommunicationStatus.Success) throw new IOException("Cannot listen to the X10's replies.");
            Krly.Version? version = null; Krly.Settings? settings = null; IReadOnlyDictionary<string, string>? mappings = null;
            // the version reply proves this is a KRLY controller before anything else is asked
            await Ask(Krly.QueryVersion, [0x01, 0x00], f => f[1] == Krly.QueryVersion && (version = Krly.ParseVersion(f)) != null);
            await Ask(Krly.QueryBattery, [], f => f[1] == Krly.QueryBattery && battery != null);
            var settingsPages = new Krly.SettingsPages();
            await Ask(Krly.QuerySettings, [0x02, 0x01], f => f[1] == Krly.QuerySettings && (settings = settingsPages.Accept(f)) != null);
            var mappingPages = new Krly.MappingPages();
            await Ask(Krly.QueryMappings, [0x01], f => f[1] == Krly.QueryMappings && (mappings = mappingPages.Accept(f)) != null);
            return new(device.Name, version!, battery, settings!, mappings!);
        }
        finally
        {
            notify.ValueChanged -= Received;
            try { await notify.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.None); } catch { }
        }
    }

    /// <summary>The reading as short lines for the Device tab.</summary>
    public static IEnumerable<(string Label, string Value)> Describe(Reading r)
    {
        static string Stick(Krly.Stick s) => $"{s.Min}–{s.Max}%" + (s.InvertX ? " · X inverted" : "") + (s.InvertY ? " · Y inverted" : "");
        yield return ("Controller", r.Name);
        yield return ("Battery", r.Battery is { } b ? $"{b.Percent}%" + (b.Charging ? " · charging" : "") + (b.Low ? " · low" : "") : "Not reported");
        yield return ("Firmware", $"{r.Version.Firmware} · hardware {r.Version.Hardware}");
        yield return ("Triggers", $"Left {r.Settings.LeftTrigger.Min}–{r.Settings.LeftTrigger.Max}% · Right {r.Settings.RightTrigger.Min}–{r.Settings.RightTrigger.Max}%");
        yield return ("Left stick", Stick(r.Settings.LeftStick));
        yield return ("Right stick", Stick(r.Settings.RightStick));
        yield return ("Vibration", $"Left {r.Settings.LeftMotor}% · Right {r.Settings.RightMotor}%");
        yield return ("Turbo", r.Settings.Turbo.Count == 0 ? "None" : string.Join(" · ", r.Settings.Turbo.Select(t => $"{t.Key}{(t.Automatic ? " auto" : "")} rate {t.Rate}")));
        yield return ("Buttons", r.Mappings.Count == 0 ? "Default" : string.Join(" · ", r.Mappings.Select(m => $"{m.Key} → {m.Value}")));
    }
}
