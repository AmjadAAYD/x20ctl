using Android.Bluetooth;
using Android.Bluetooth.LE;
using Android.Content;
using X20Ctl.Mobile.Core;

namespace X20Ctl.Mobile;

/// <summary>A device the phone knows about: from a Bluetooth LE advertisement, or already paired.</summary>
public sealed record SeenDevice(string Address, string? Name, int? Rssi, IReadOnlyList<Guid> Services, bool Paired);

/// <summary>Passive Bluetooth LE scan plus the paired-device list. It listens; it sends nothing to any device.</summary>
public sealed class BleScanner : ScanCallback
{
    public event Action<SeenDevice>? Seen;
    public event Action<string>? Failed;
    BluetoothLeScanner? scanner;

    static BluetoothAdapter? Adapter => (Android.App.Application.Context.GetSystemService(Context.BluetoothService) as BluetoothManager)?.Adapter;

    /// <summary>Starts scanning. Returns why it couldn't, or null.</summary>
    public string? Start()
    {
        var adapter = Adapter;
        if (adapter is null) return "This phone has no Bluetooth.";
        if (!adapter.IsEnabled) return "Turn Bluetooth on, then scan again.";
        foreach (var device in adapter.BondedDevices ?? (ICollection<BluetoothDevice>)Array.Empty<BluetoothDevice>())
            if (device.Type is BluetoothDeviceType.Le or BluetoothDeviceType.Dual)
                Seen?.Invoke(new(device.Address ?? "", device.Name, null, Uuids(device.GetUuids()?.Select(u => u.ToString())), true));
        scanner = adapter.BluetoothLeScanner;
        if (scanner is null) return "Bluetooth LE scanning isn't available.";
        var settings = new ScanSettings.Builder().SetScanMode(Android.Bluetooth.LE.ScanMode.LowLatency)!.Build();
        scanner.StartScan(null, settings, this);
        return null;
    }

    public void Stop()
    {
        try { scanner?.StopScan(this); } catch (Java.Lang.IllegalStateException) { } // Bluetooth was turned off meanwhile
        scanner = null;
    }

    public override void OnScanResult(ScanCallbackType callbackType, ScanResult? result)
    {
        if (result?.Device is not { } device) return;
        var services = Uuids(result.ScanRecord?.ServiceUuids?.Select(u => u.ToString()));
        Seen?.Invoke(new(device.Address ?? "", device.Name ?? result.ScanRecord?.DeviceName, result.Rssi, services, device.BondState == Bond.Bonded));
    }

    public override void OnScanFailed(ScanFailure errorCode) => Failed?.Invoke("Scan failed: " + errorCode);

    static List<Guid> Uuids(IEnumerable<string?>? values) =>
        (values ?? []).Select(v => Guid.TryParse(v, out var g) ? g : Guid.Empty).Where(g => g != Guid.Empty).Distinct().ToList();
}

/// <summary>
/// The read scan: connect, list services, read the allowlisted standard values one at a time, disconnect.
/// Every read goes through <see cref="GattPolicy.CanRead"/>; there is no write call anywhere in this class.
/// </summary>
public sealed class GattReader : BluetoothGattCallback
{
    readonly TaskCompletionSource connected = new(TaskCreationOptions.RunContinuationsAsynchronously);
    readonly TaskCompletionSource<bool> discovered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    TaskCompletionSource<byte[]?>? reading;
    static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(20), StepTimeout = TimeSpan.FromSeconds(8);

    public static async Task<ReadScan> Run(SeenDevice target, CancellationToken cancel)
    {
        var scan = new ReadScan { Name = target.Name ?? "", Address = target.Address };
        var adapter = (Android.App.Application.Context.GetSystemService(Context.BluetoothService) as BluetoothManager)?.Adapter;
        if (adapter is null || !adapter.IsEnabled) { scan.Problem = "Bluetooth is off."; return scan; }
        var callback = new GattReader();
        var gatt = adapter.GetRemoteDevice(target.Address)?.ConnectGatt(Android.App.Application.Context, false, callback, BluetoothTransports.Le);
        if (gatt is null) { scan.Problem = "Android refused to open a connection."; return scan; }
        try
        {
            await callback.connected.Task.WaitAsync(ConnectTimeout, cancel);
            if (!gatt.DiscoverServices()) { scan.Problem = "Android refused to list the services."; return scan; }
            if (!await callback.discovered.Task.WaitAsync(ConnectTimeout, cancel)) { scan.Problem = "Listing the services failed."; return scan; }
            foreach (var service in gatt.Services ?? [])
            {
                var serviceId = Parse(service.Uuid);
                scan.Services.Add(serviceId);
                if (GattPolicy.IsOta(serviceId)) continue; // not even its characteristics are looked at
                foreach (var characteristic in service.Characteristics ?? [])
                {
                    var id = Parse(characteristic.Uuid);
                    string properties = Describe(characteristic.Properties);
                    if (!GattPolicy.CanRead(serviceId, id) || !characteristic.Properties.HasFlag(GattProperty.Read)) { scan.Entries.Add(new(serviceId, id, properties, null)); continue; }
                    callback.reading = new(TaskCreationOptions.RunContinuationsAsynchronously);
                    if (!gatt.ReadCharacteristic(characteristic)) { scan.Entries.Add(new(serviceId, id, properties, null, "Android refused the read")); continue; }
                    try
                    {
                        var value = await callback.reading.Task.WaitAsync(StepTimeout, cancel);
                        scan.Entries.Add(value is null ? new(serviceId, id, properties, null, "the controller answered with an error") : new(serviceId, id, properties, value));
                    }
                    catch (TimeoutException) { scan.Entries.Add(new(serviceId, id, properties, null, "no answer")); }
                }
            }
        }
        catch (TimeoutException) { scan.Problem = "The controller didn't answer. Make sure it's on and close by."; }
        catch (IOException e) { scan.Problem = e.Message; }
        catch (OperationCanceledException) { scan.Problem = "Stopped."; }
        finally { gatt.Disconnect(); gatt.Close(); }
        return scan;
    }

    static Guid Parse(Java.Util.UUID? uuid) => Guid.TryParse(uuid?.ToString(), out var g) ? g : Guid.Empty;

    static string Describe(GattProperty p)
    {
        var names = new List<string>();
        if (p.HasFlag(GattProperty.Read)) names.Add("read");
        if (p.HasFlag(GattProperty.Write)) names.Add("write");
        if (p.HasFlag(GattProperty.WriteNoResponse)) names.Add("write without response");
        if (p.HasFlag(GattProperty.Notify)) names.Add("notify");
        if (p.HasFlag(GattProperty.Indicate)) names.Add("indicate");
        return string.Join(", ", names);
    }

    public override void OnConnectionStateChange(BluetoothGatt? gatt, GattStatus status, ProfileState newState)
    {
        if (newState == ProfileState.Connected && status == GattStatus.Success) { connected.TrySetResult(); return; }
        if (newState != ProfileState.Disconnected && status == GattStatus.Success) return;
        var lost = new IOException($"The connection closed (status {(int)status}).");
        connected.TrySetException(lost); discovered.TrySetException(lost); reading?.TrySetException(lost);
    }

    public override void OnServicesDiscovered(BluetoothGatt? gatt, GattStatus status) => discovered.TrySetResult(status == GattStatus.Success);

    // Android 13 added an overload that passes the value; its default implementation stores the value and calls this one.
#pragma warning disable CS0618, CS0672, CA1422
    public override void OnCharacteristicRead(BluetoothGatt? gatt, BluetoothGattCharacteristic? characteristic, GattStatus status) =>
        reading?.TrySetResult(status == GattStatus.Success ? characteristic?.GetValue() ?? [] : null);
#pragma warning restore CS0618, CS0672, CA1422
}
