using System.Text;

namespace X20Ctl.Mobile.Core;

/// <summary>One characteristic the read scan looked at. Value is null when it was not read (not allowlisted) or the read failed.</summary>
public sealed record ReadScanEntry(Guid Service, Guid Characteristic, string Properties, byte[]? Value, string? Error = null);

/// <summary>
/// The phone's read scan: the services a controller exposes and the few standard values it is allowed to read.
/// Everything here was read, not written. It shows what the controller reports, not what it supports.
/// </summary>
public sealed class ReadScan
{
    public string Name { get; init; } = "";
    public string Address { get; init; } = "";
    public DateTime Utc { get; init; } = DateTime.UtcNow;
    public List<Guid> Services { get; } = new();
    public List<ReadScanEntry> Entries { get; } = new();
    public string? Problem { get; set; }

    public bool HasKeyLinkerService => Services.Contains(GattPolicy.KeyLinkerService);
    public bool HasOtaService => Services.Any(GattPolicy.IsOta);

    /// <summary>Decodes a value the way its characteristic defines it: a percentage for battery, UTF-8 text for Device Information.</summary>
    public static string Show(Guid characteristic, byte[] value)
    {
        if (characteristic == GattPolicy.Standard(0x2A19)) return value.Length == 1 && value[0] <= 100 ? $"{value[0]}%" : "unexpected: " + Convert.ToHexString(value);
        var text = Encoding.UTF8.GetString(value).TrimEnd('\0').Trim();
        return text.Length > 0 && text.All(c => !char.IsControl(c)) ? text : Convert.ToHexString(value);
    }

    public string Text()
    {
        var s = new StringBuilder();
        s.AppendLine("X20CTL phone read scan");
        s.AppendLine($"Time (UTC): {Utc:yyyy-MM-dd HH:mm:ss}");
        s.AppendLine($"Device: {(Name.Length > 0 ? Name : "(no name)")}");
        s.AppendLine($"Status: {DeviceLabel.Status(Address, Services)}");
        s.AppendLine("Nothing was written to the controller. Only allowlisted standard values were read.");
        if (Problem is not null) s.AppendLine("Problem: " + Problem);
        s.AppendLine();
        s.AppendLine("Services:");
        foreach (var service in Services)
        {
            s.AppendLine($"  {GattPolicy.ServiceName(service)}  {service}");
            foreach (var e in Entries.Where(e => e.Service == service))
            {
                string name = GattPolicy.NameOf(e.Characteristic) ?? "characteristic";
                string value = e.Value is { } v ? Show(e.Characteristic, v) : e.Error is { } err ? "read failed: " + err : "not read";
                s.AppendLine($"    {name} {e.Characteristic} [{e.Properties}]: {value}");
            }
        }
        if (Services.Count == 0) s.AppendLine("  none discovered");
        s.AppendLine();
        s.AppendLine(HasKeyLinkerService
            ? "The KeyLinker configuration service is present. That is the family the X20 uses; it does not prove the model or that its settings work with X20CTL."
            : "The KeyLinker configuration service was not found.");
        return s.ToString();
    }
}
