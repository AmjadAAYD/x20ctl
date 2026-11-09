using System.IO;
using System.Security.Cryptography;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Foundation;
using Windows.Storage.Streams;
using X20Ctl.Zone.Capture;

namespace X20Ctl.Product.Scanner;

/// <summary>
/// One Bluetooth session for the Write scan. Unlike the Studio's writer it does not demand a recorded model: it connects to the
/// pad the tester chose, asks, and learns from the replies. What a session may send is fixed by its <see cref="ScanStage"/>:
/// identify and read-back can never write. No retries, one operation per connection.
/// </summary>
internal static class PadScanBle
{
    static readonly Guid ServiceUuid = Guid.Parse("d7f010e0-660d-46e9-96c3-19c4148bdab5"), WriteUuid = Guid.Parse("d7f010e1-660d-46e9-96c3-19c4148bdab5"), NotifyUuid = Guid.Parse("d7f010e2-660d-46e9-96c3-19c4148bdab5");
    static readonly SemaphoreSlim Gate = NativeBleClient.ConfigurationGate;

    /// <summary>Name fragments that pick the chosen model's advertisement. "Unknown" accepts any pad that advertises the KeyLinker service.</summary>
    internal static string[] Hints(string model) => model switch
    {
        "X20 Pro" => ["EasySMX X20Pro", "EasySMX X20 Pro"],
        "X20" => ["EasySMX X20", "Xpert2"],
        "Dune / D15" => ["Dune", "D15"],
        "Unknown" => [],
        _ => [model.Split(' ')[0]],
    };

    static async Task<(ulong? Address, string? Error, bool HasService)> Find(string model, int seconds)
    {
        var hints = Hints(model); var tcs = new TaskCompletionSource<ulong>(); var services = new HashSet<ulong>(); var names = new Dictionary<ulong, string>(); string? scanError = null;
        var watcher = new BluetoothLEAdvertisementWatcher { ScanningMode = BluetoothLEScanningMode.Active };
        watcher.Received += (w, a) =>
        {
            var addr = a.BluetoothAddress; var raw = (a.Advertisement.LocalName ?? "").Trim();
            if (raw.Length > 0) names[addr] = raw;
            bool service = a.Advertisement.ServiceUuids.Contains(ServiceUuid); if (service) services.Add(addr);
            string name = names.TryGetValue(addr, out var n) ? n : raw;
            bool byName = hints.Length > 0 && name.Length > 0 && !(model == "X20" && name.Contains("Pro", StringComparison.OrdinalIgnoreCase)) && hints.Any(h => name.StartsWith(h, StringComparison.OrdinalIgnoreCase));
            if (byName || (hints.Length == 0 && services.Contains(addr))) { try { w.Stop(); } catch { } tcs.TrySetResult(addr); }
        };
        watcher.Stopped += (_, a) => { if (a.Error == BluetoothError.RadioNotAvailable) scanError = "Bluetooth is turned off in Windows Settings or there is no Bluetooth adapter."; else if (a.Error != BluetoothError.Success) scanError = $"Bluetooth scan failed ({a.Error})."; };
        try { watcher.Start(); } catch (Exception e) { return (null, "Could not start the Bluetooth scan: " + e.Message, false); }
        var done = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(seconds)));
        try { watcher.Stop(); } catch { }
        if (done == tcs.Task) { var found = await tcs.Task; return (found, null, services.Contains(found)); }
        return (null, scanError ?? "No matching controller was heard over Bluetooth. Wake it, put it in Bluetooth mode and try again.", false);
    }

    /// <summary>Runs <paramref name="work"/> on one connection. Returns the work's result, or a reason the pad could not be asked at all.</summary>
    public static async Task<(T? Value, string? Problem)> RunAsync<T>(string model, ScanStage stage, Func<PadExchange, Func<byte>, Task<T>> work)
    {
        await Gate.WaitAsync();
        try
        {
            var (address, error, _) = await Find(model, 8);
            if (address is null) return (default, error);
            using var device = await BluetoothLEDevice.FromBluetoothAddressAsync(address.Value);
            if (device is null) return (default, "Windows could not connect to that controller over Bluetooth.");
            var services = await device.GetGattServicesForUuidAsync(ServiceUuid, BluetoothCacheMode.Uncached);
            if (services.Status != GattCommunicationStatus.Success || services.Services.Count == 0)
                return (default, "The pad is there but has no KeyLinker configuration service, so it cannot be asked. That is the answer for this pad over Bluetooth.");
            using var service = services.Services[0];
            var chars = await service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
            var write = chars.Characteristics.FirstOrDefault(c => c.Uuid == WriteUuid); var notify = chars.Characteristics.FirstOrDefault(c => c.Uuid == NotifyUuid);
            if (chars.Status != GattCommunicationStatus.Success || write is null || notify is null) return (default, "The configuration service is missing its write or notify channel.");

            TaskCompletionSource<byte[]>? pending = null; byte expected = 0;
            TypedEventHandler<GattCharacteristic, GattValueChangedEventArgs> handler = (_, e) =>
            {
                using var reader = DataReader.FromBuffer(e.CharacteristicValue); var raw = new byte[e.CharacteristicValue.Length]; reader.ReadBytes(raw);
                if (raw.Length is < 5 or > 20) return;
                var f = X20Codec.Decode(raw);
                if (f.CrcValid && (f.Length & 31) == raw.Length && f.Opcode == 0x19 && f.Serial == expected) pending?.TrySetResult(f.Payload);
            };
            notify.ValueChanged += handler;
            async Task<byte[]> Send(byte op, byte[] payload, byte serial, byte length)
            {
                if (!PadScan.Allows(stage, op)) throw new InvalidOperationException($"The {stage} stage may not send opcode 0x{op:X2}.");
                expected = serial; pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
                using var writer = new DataWriter(); writer.WriteBytes(X20Codec.Frame(op, length, serial, (byte)RandomNumberGenerator.GetInt32(256), payload));
                var result = await write.WriteValueWithResultAsync(writer.DetachBuffer(), GattWriteOption.WriteWithResponse);
                if (result.Status != GattCommunicationStatus.Success) throw new IOException("Bluetooth refused the request; nothing was retried.");
                try { return await pending.Task.WaitAsync(TimeSpan.FromSeconds(3)); }
                catch (TimeoutException) { throw; }
                finally { pending = null; }
            }
            byte counter = 160;
            try
            {
                if (await notify.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify) != GattCommunicationStatus.Success)
                    return (default, "Could not subscribe to the pad's replies.");
                return (await work(Send, () => ++counter), null);
            }
            finally
            {
                notify.ValueChanged -= handler;
                try { await notify.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.None); } catch { }
            }
        }
        catch (Exception e) when (e is IOException or TimeoutException or InvalidOperationException or System.Runtime.InteropServices.COMException)
        { return (default, e.Message); }
        finally { Gate.Release(); }
    }
}
