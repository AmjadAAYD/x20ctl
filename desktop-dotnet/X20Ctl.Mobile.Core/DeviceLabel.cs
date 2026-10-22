namespace X20Ctl.Mobile.Core;

/// <summary>
/// What a Bluetooth advertisement tells us, and nothing more. A MAC prefix or a name is a hint about the device family,
/// not proof of the model and never a sign that its settings are supported.
/// </summary>
public static class DeviceLabel
{
    // MAC prefixes from the official app's AgreementConfig (docs/01-protocol.md)
    static readonly (string Prefix, string Meaning)[] Prefixes =
    [
        ("98:B6:E9:", "KeyLinker-family prefix: standard gamepad"),
        ("98:B6:ED:", "KeyLinker-family prefix: Switch-mode device"),
        ("98:B6:EC:", "KeyLinker-family prefix: two-in-one device"),
    ];

    public static string? PrefixHint(string? address)
    {
        if (string.IsNullOrEmpty(address)) return null;
        foreach (var (prefix, meaning) in Prefixes)
            if (address.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return meaning;
        return null;
    }

    public static bool LooksRelevant(string? name, string? address, IEnumerable<Guid> services) =>
        PrefixHint(address) is not null || services.Contains(GattPolicy.KeyLinkerService) ||
        (name ?? "").Contains("X20", StringComparison.OrdinalIgnoreCase) || (name ?? "").Contains("EasySMX", StringComparison.OrdinalIgnoreCase);

    /// <summary>The one-line status shown under a device. Always "seen", never "supported".</summary>
    public static string Status(string? address, IEnumerable<Guid> services)
    {
        var parts = new List<string> { "Seen" };
        if (PrefixHint(address) is { } hint) parts.Add(hint);
        if (services.Contains(GattPolicy.KeyLinkerService)) parts.Add("advertises the KeyLinker service");
        return string.Join(" · ", parts);
    }
}
