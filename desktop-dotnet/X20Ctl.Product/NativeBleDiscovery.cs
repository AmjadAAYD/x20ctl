using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;

namespace X20Ctl.Product;

/// <summary>
/// The Windows side of BleDiscovery. Two things only, each started by the player:
/// listening to advertisements (no connection and no controller command), and listing the GATT services and
/// characteristic UUIDs of a listed device. The listing connects, reads no value, writes nothing, subscribes to nothing
/// and calls no pairing API. Controller protocol commands live in NativeBleClient and are not reachable from here (a test holds it).
/// </summary>
public static class NativeBleDiscovery
{
    public static async Task<IReadOnlyList<BleDiscovery.Seen>> ListenAsync(TimeSpan window)
    {
        var seen = new Dictionary<ulong, BleDiscovery.Seen>();
        var gate = new object();
        // Active, like the client's own scan: the radio sends the standard scan request so devices that keep their name in the
        // scan response are named. That is link-layer Bluetooth, not a controller command, and it opens no connection.
        var watcher = new BluetoothLEAdvertisementWatcher { ScanningMode = BluetoothLEScanningMode.Active };
        watcher.Received += (_, a) =>
        {
            ushort? appearance = null;
            foreach (var section in a.Advertisement.DataSections)
                if (section.DataType == 0x19 && section.Data.Length >= 2)
                {
                    var b = new byte[2]; using var r = Windows.Storage.Streams.DataReader.FromBuffer(section.Data); r.ReadBytes(b);
                    appearance = (ushort)(b[0] | b[1] << 8);
                }
            var advert = new BleDiscovery.Advert(a.BluetoothAddress, Kind(a), a.Advertisement.LocalName, a.Advertisement.ServiceUuids.ToArray(), appearance, a.RawSignalStrengthInDBm, a.Timestamp);
            lock (gate) BleDiscovery.Merge(seen, advert);
        };
        watcher.Start();
        try { await Task.Delay(window); }
        finally { try { watcher.Stop(); } catch { } }
        lock (gate) return BleDiscovery.Listed(seen.Values);
    }

    /// <summary>Windows does not always say whether an address is public or random; Unknown is kept as Unknown.</summary>
    static BleDiscovery.AddressKind Kind(BluetoothLEAdvertisementReceivedEventArgs a)
    {
        try
        {
            return a.BluetoothAddressType switch
            {
                BluetoothAddressType.Public => BleDiscovery.AddressKind.Public,
                BluetoothAddressType.Random => BleDiscovery.AddressKind.Random,
                _ => BleDiscovery.AddressKind.Unknown,
            };
        }
        catch { return BleDiscovery.AddressKind.Unknown; }
    }

    /// <summary>Service and characteristic UUIDs of one device. Uncached so the answer is the device's, not Windows' memory.</summary>
    public static async Task<BleDiscovery.GattListing> ListServicesAsync(ulong address, TimeSpan timeout)
    {
        var work = ListAsync(address);
        if (await Task.WhenAny(work, Task.Delay(timeout)) != work) return new(null, [], "Did not answer in time.");
        return await work;
    }

    static async Task<BleDiscovery.GattListing> ListAsync(ulong address)
    {
        try
        {
            using var device = await BluetoothLEDevice.FromBluetoothAddressAsync(address);
            if (device == null) return new(null, [], "Could not connect.");
            var result = await device.GetGattServicesAsync(BluetoothCacheMode.Uncached);
            if (result.Status != GattCommunicationStatus.Success) return new(null, [], "Did not list its services.");
            var services = new List<Guid>(); var characteristics = new List<Guid>();
            foreach (var service in result.Services)
                using (service)
                {
                    services.Add(service.Uuid);
                    var chars = await service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
                    if (chars.Status == GattCommunicationStatus.Success) characteristics.AddRange(chars.Characteristics.Select(c => c.Uuid));
                }
            return new(services, characteristics, null);
        }
        // only the error code: Windows exception text can name the device, and this text is shown and saved
        catch (Exception ex) { return new(null, [], $"Could not connect (0x{ex.HResult:X8})."); }
    }
}
