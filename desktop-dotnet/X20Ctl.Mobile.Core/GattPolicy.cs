namespace X20Ctl.Mobile.Core;

/// <summary>
/// What the phone app may do over Bluetooth LE. Reads are limited to standard, non-identifying characteristics
/// (battery level and the Device Information strings other than the serial number). Nothing is ever written: no
/// characteristic writes, no descriptor writes (so no notification subscriptions), and the firmware (OTA) service is
/// never touched. The X20 configuration service is only reported as present or absent; talking to it needs writes.
/// </summary>
public static class GattPolicy
{
    public static readonly Guid BatteryService = Standard(0x180F);
    public static readonly Guid DeviceInformationService = Standard(0x180A);
    /// <summary>KeyLinker configuration service (docs/01-protocol.md). Presence is reported; it is never opened.</summary>
    public static readonly Guid KeyLinkerService = Guid.Parse("d7f010e0-660d-46e9-96c3-19c4148bdab5");
    /// <summary>Firmware update service. Never read, written or subscribed to.</summary>
    public static readonly Guid OtaService = Guid.Parse("2de516f0-50f5-45d5-a1b2-c3565de543ae");

    /// <summary>Sending to the controller is locked in this version of the phone app.</summary>
    public const bool WritesAllowed = false;

    static readonly Dictionary<Guid, string> Readable = new()
    {
        [Standard(0x2A19)] = "Battery level",
        [Standard(0x2A29)] = "Manufacturer",
        [Standard(0x2A24)] = "Model number",
        [Standard(0x2A26)] = "Firmware revision",
        [Standard(0x2A27)] = "Hardware revision",
        [Standard(0x2A28)] = "Software revision",
    };

    public static Guid Standard(ushort id) => Guid.Parse($"0000{id:x4}-0000-1000-8000-00805f9b34fb");

    public static bool IsOta(Guid service) => service == OtaService || service.ToString("D").StartsWith("2de516f", StringComparison.OrdinalIgnoreCase);

    /// <summary>True only for an allowlisted characteristic inside the service it belongs to.</summary>
    public static bool CanRead(Guid service, Guid characteristic)
    {
        if (IsOta(service) || !Readable.ContainsKey(characteristic)) return false;
        return characteristic == Standard(0x2A19) ? service == BatteryService : service == DeviceInformationService;
    }

    public static bool CanWrite(Guid service, Guid characteristic) => WritesAllowed && !IsOta(service);

    public static string? NameOf(Guid characteristic) => Readable.TryGetValue(characteristic, out var name) ? name : null;

    public static string ServiceName(Guid service) =>
        service == BatteryService ? "Battery" :
        service == DeviceInformationService ? "Device Information" :
        service == KeyLinkerService ? "KeyLinker configuration (not opened)" :
        IsOta(service) ? "Firmware update (never touched)" :
        service == Standard(0x1800) ? "Generic Access" :
        service == Standard(0x1801) ? "Generic Attribute" :
        service == Standard(0x1812) ? "Human Interface Device" :
        "Unknown service";
}
