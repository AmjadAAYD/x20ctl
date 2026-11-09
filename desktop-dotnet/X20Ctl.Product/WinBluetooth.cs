using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Devices.Radios;

namespace X20Ctl.Product;

/// <summary>
/// The Windows side of the Bluetooth manager, using only Windows' documented Bluetooth and device-enumeration APIs.
/// Windows owns the radio, pairing and the gamepad drivers; X20CTL observes them, opens its own GATT link to the
/// configuration peripheral when asked, and starts Windows' own pairing when the player presses Pair.
/// Nothing here sends a controller command or reads a characteristic value.
/// </summary>
public static class WinBluetooth
{
    static readonly Guid ConfigService = BleDiscovery.ConfigService;

    public enum Radio { On, Off, NoAdapter, NoLowEnergy, Unknown }

    public static async Task<Radio> RadioAsync()
    {
        try
        {
            var adapter = await BluetoothAdapter.GetDefaultAsync();
            if (adapter == null) return Radio.NoAdapter;
            if (!adapter.IsLowEnergySupported) return Radio.NoLowEnergy;
            var radio = await adapter.GetRadioAsync();
            return radio?.State switch { RadioState.On => Radio.On, RadioState.Off or RadioState.Disabled => Radio.Off, _ => Radio.Unknown };
        }
        catch (Exception) { return Radio.Unknown; }
    }

    public static string Describe(Radio r) => r switch
    {
        Radio.On => "On",
        Radio.Off => "Off. Turn Bluetooth on in Windows.",
        Radio.NoAdapter => "No Bluetooth adapter found.",
        Radio.NoLowEnergy => "This adapter has no Bluetooth LE.",
        _ => "Windows didn't say.",
    };

    // ================= the configuration link =================

    public sealed class LinkFactory : IConfigLinkFactory
    {
        public async Task<IConfigLink?> OpenAsync(ulong address, CancellationToken token)
        {
            var device = await BluetoothLEDevice.FromBluetoothAddressAsync(address).AsTask(token);
            if (device == null) return null;
            GattSession? session = null;
            try
            {
                session = await GattSession.FromDeviceIdAsync(device.BluetoothDeviceId).AsTask(token);
                // held on purpose while the player keeps this connection; Disconnect turns it off before letting go
                session.MaintainConnection = true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { session?.Dispose(); session = null; }
            return new Link(device, session);
        }
    }

    sealed class Link : IConfigLink
    {
        readonly BluetoothLEDevice device;
        readonly GattSession? session;
        public event Action<bool>? LinkChanged;
        public event Action<string>? SessionChanged;

        public Link(BluetoothLEDevice device, GattSession? session)
        {
            this.device = device; this.session = session;
            device.ConnectionStatusChanged += OnStatus;
            if (session != null) session.SessionStatusChanged += OnSession;
        }

        void OnStatus(BluetoothLEDevice d, object _) => LinkChanged?.Invoke(d.ConnectionStatus == BluetoothConnectionStatus.Connected);
        void OnSession(GattSession s, GattSessionStatusChangedEventArgs e) =>
            SessionChanged?.Invoke(e.Status + (e.Error == BluetoothError.Success ? "" : $" ({e.Error})"));

        public bool IsUp => device.ConnectionStatus == BluetoothConnectionStatus.Connected;

        public async Task<ServiceProbe> ProbeAsync(CancellationToken token)
        {
            // service discovery only: no characteristic is read, written or subscribed to
            var result = await device.GetGattServicesForUuidAsync(ConfigService, BluetoothCacheMode.Uncached).AsTask(token);
            try
            {
                return result.Status switch
                {
                    GattCommunicationStatus.Success => new(true, result.Services.Count > 0),
                    GattCommunicationStatus.Unreachable => new(false, false, "Unreachable"),
                    _ => new(true, false, result.Status + (result.ProtocolError is { } p ? $" 0x{p:X2}" : "")),
                };
            }
            finally { foreach (var s in result.Services) s.Dispose(); }
        }

        public ValueTask DisposeAsync()
        {
            device.ConnectionStatusChanged -= OnStatus;
            if (session != null)
            {
                session.SessionStatusChanged -= OnSession;
                try { session.MaintainConnection = false; } catch (Exception) { }
                session.Dispose();
            }
            device.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    // ================= Windows' Bluetooth device list (pairing and gameplay) =================

    static readonly string[] Properties =
    [
        "System.Devices.Aep.IsConnected", "System.Devices.Aep.IsPaired", "System.Devices.Aep.CanPair",
        "System.Devices.Aep.Bluetooth.Cod.Major", "System.Devices.Aep.SignalStrength", "System.Devices.Aep.DeviceAddress",
    ];
    static readonly Regex Address = new(@"(?i)(?:[0-9a-f]{2}[:\-]?){5}[0-9a-f]{2}");

    /// <summary>Paired Bluetooth Classic devices, plus unpaired ones found by a Windows inquiry for the given window.
    /// The address is read only to flag EasySMX's 98:B6:EC range and is not kept.</summary>
    public static async Task<IReadOnlyList<BluetoothManager.ClassicDevice>> ClassicAsync(TimeSpan window, BluetoothLog log)
    {
        var found = new Dictionary<string, BluetoothManager.ClassicDevice>();
        var gate = new object();
        var watchers = new List<DeviceWatcher>();
        foreach (bool paired in new[] { true, false })
        {
            var w = DeviceInformation.CreateWatcher(BluetoothDevice.GetDeviceSelectorFromPairingState(paired), Properties, DeviceInformationKind.AssociationEndpoint);
            w.Added += (_, d) => { lock (gate) found[d.Id] = From(d.Id, d.Name, d.Properties, d.Pairing.IsPaired, d.Pairing.CanPair); };
            w.Updated += (_, u) => { lock (gate) if (found.TryGetValue(u.Id, out var old)) found[u.Id] = Update(old, u.Properties); };
            w.Removed += (_, u) => { lock (gate) found.Remove(u.Id); };
            watchers.Add(w);
        }
        try
        {
            foreach (var w in watchers) w.Start();
            await Task.Delay(window);
        }
        catch (Exception ex) { log.Add("Windows device search failed.", BluetoothLog.Code(ex)); }
        finally { foreach (var w in watchers) try { if (w.Status is DeviceWatcherStatus.Started or DeviceWatcherStatus.EnumerationCompleted) w.Stop(); } catch (Exception) { } }
        BluetoothManager.ClassicDevice[] list;
        lock (gate) list = found.Values.ToArray();
        for (int i = 0; i < list.Length; i++)
            if (list[i].Paired && BluetoothManager.IsControllerLike(list[i])) list[i] = list[i] with { GamepadDriver = await GamepadDriverAsync(list[i].Id) };
        return list;
    }

    static BluetoothManager.ClassicDevice From(string id, string name, IReadOnlyDictionary<string, object> p, bool paired, bool canPair)
    {
        bool vendor = p.TryGetValue("System.Devices.Aep.DeviceAddress", out var a) && a is string s && Address.Match(s) is { Success: true } m
            && BluetoothManager.InVendorRange(Regex.Replace(m.Value, "[:-]", ""));
        return new(id, name, paired, Bool(p, "System.Devices.Aep.IsConnected"), canPair, p.TryGetValue("System.Devices.Aep.Bluetooth.Cod.Major", out var c) && c is uint u ? u : null, vendor,
            p.TryGetValue("System.Devices.Aep.SignalStrength", out var r) && r is int i ? i : null);
    }

    static BluetoothManager.ClassicDevice Update(BluetoothManager.ClassicDevice old, IReadOnlyDictionary<string, object> p) => old with
    {
        Connected = Bool(p, "System.Devices.Aep.IsConnected") ?? old.Connected,
        Paired = Bool(p, "System.Devices.Aep.IsPaired") ?? old.Paired,
        Rssi = p.TryGetValue("System.Devices.Aep.SignalStrength", out var r) && r is int i ? i : old.Rssi,
    };

    static bool? Bool(IReadOnlyDictionary<string, object> p, string key) => p.TryGetValue(key, out var v) && v is bool b ? b : null;

    /// <summary>Windows' current view of one device: paired and connected. Null when Windows no longer knows it.</summary>
    public static async Task<(bool Paired, bool? Connected, bool? GamepadDriver)?> RefreshAsync(string id)
    {
        try
        {
            var d = await DeviceInformation.CreateFromIdAsync(id, Properties, DeviceInformationKind.AssociationEndpoint);
            return (d.Pairing.IsPaired, Bool(d.Properties, "System.Devices.Aep.IsConnected"), d.Pairing.IsPaired ? await GamepadDriverAsync(id) : null);
        }
        catch (Exception) { return null; }
    }

    /// <summary>Whether Windows built a gamepad (HID) device under this pairing: the devices sharing its container are
    /// listed and a Bluetooth HID service node (UUID 0x1124) looked for. Null when Windows doesn't say.</summary>
    public static async Task<bool?> GamepadDriverAsync(string id)
    {
        try
        {
            var aep = await DeviceInformation.CreateFromIdAsync(id, ["System.Devices.Aep.ContainerId"], DeviceInformationKind.AssociationEndpoint);
            if (!aep.Properties.TryGetValue("System.Devices.Aep.ContainerId", out var c) || c is not Guid container) return null;
            var nodes = await DeviceInformation.FindAllAsync($"System.Devices.ContainerId:=\"{{{container}}}\"", null, DeviceInformationKind.Device);
            return nodes.Any(n => n.Id.Contains("{00001124-0000-1000-8000-00805F9B34FB}", StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception) { return null; }
    }

    /// <summary>Removes Windows' pairing, only from the guided re-pair the player starts. Never used to disconnect.</summary>
    public static async Task<string> RemovePairingAsync(string id, BluetoothLog log)
    {
        try
        {
            var d = await DeviceInformation.CreateFromIdAsync(id, Properties, DeviceInformationKind.AssociationEndpoint);
            if (!d.Pairing.IsPaired) return "AlreadyUnpaired";
            log.Add("Removing Windows' pairing (re-pair).");
            return (await d.Pairing.UnpairAsync()).Status.ToString();
        }
        catch (Exception ex) { log.Add("Windows unpair call failed.", BluetoothLog.Code(ex)); return "Error"; }
    }

    /// <summary>Windows' own pairing, started by the player's press. Windows shows any consent or PIN prompt itself.
    /// Returns Windows' DevicePairingResultStatus name (BluetoothManager.PairOutcome puts it in words).</summary>
    public static async Task<string> PairAsync(string id, BluetoothLog log)
    {
        try
        {
            var d = await DeviceInformation.CreateFromIdAsync(id, Properties, DeviceInformationKind.AssociationEndpoint);
            if (d.Pairing.IsPaired) return "AlreadyPaired";
            if (!d.Pairing.CanPair) return "NotPairable";
            log.Add("Windows pairing started.");
            var result = await d.Pairing.PairAsync();
            return result.Status.ToString();
        }
        catch (Exception ex) { log.Add("Windows pairing call failed.", BluetoothLog.Code(ex)); return "Error"; }
    }

    /// <summary>Windows' own pairing for a Bluetooth LE gamepad the player picked (not the configuration peripheral,
    /// which X20CTL reaches without pairing).</summary>
    public static async Task<string> PairLeAsync(ulong address, BluetoothLog log)
    {
        try
        {
            using var device = await BluetoothLEDevice.FromBluetoothAddressAsync(address);
            if (device == null) return "NotReadyToPair";
            var pairing = device.DeviceInformation.Pairing;
            if (pairing.IsPaired) return "AlreadyPaired";
            if (!pairing.CanPair) return "NotPairable";
            log.Add("Windows pairing started.");
            return (await pairing.PairAsync()).Status.ToString();
        }
        catch (Exception ex) { log.Add("Windows pairing call failed.", BluetoothLog.Code(ex)); return "Error"; }
    }

    public static void OpenSettings()
    {
        try { Process.Start(new ProcessStartInfo("ms-settings:bluetooth") { UseShellExecute = true }); } catch (Exception) { }
    }

    // ================= XInput, the gameplay side Windows' input stack reports =================

    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")] static extern uint GetState(uint slot, byte[] state);

    public static int XInputSlots()
    {
        int n = 0;
        try { var buffer = new byte[16]; for (uint i = 0; i < 4; i++) if (GetState(i, buffer) == 0) n++; }
        catch (Exception) { }
        return n;
    }
}
