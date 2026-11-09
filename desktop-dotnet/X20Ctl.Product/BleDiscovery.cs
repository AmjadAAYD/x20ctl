using System.Security.Cryptography;
using System.Text;

namespace X20Ctl.Product;

/// <summary>
/// Which nearby Bluetooth devices X20CTL lists, and what it may say about each. Pure: no Windows Bluetooth types, no
/// link, no codec, so nothing here can send anything to a controller (a test holds that). Discovery is passive, from
/// advertisements only. The optional service listing (NativeBleDiscovery) reads GATT service and characteristic UUIDs
/// and nothing else. Neither path asks a controller anything in its protocol: identity (0x91, 0x93, 0xB0) is asked only
/// by the existing controller check, for a pad picked by name, and settings writes stay behind VibrationTest and the
/// model gates. The address prefixes are the ones KeyLinker 3.73's LE scan lists; X20CTL uses them as a hint to list a
/// device, never as evidence that it speaks the protocol.
/// </summary>
public static class BleDiscovery
{
    public static readonly Guid ConfigService = Guid.Parse("d7f010e0-660d-46e9-96c3-19c4148bdab5");
    public static readonly Guid ConfigWrite = Guid.Parse("d7f010e1-660d-46e9-96c3-19c4148bdab5");
    public static readonly Guid ConfigNotify = Guid.Parse("d7f010e2-660d-46e9-96c3-19c4148bdab5");
    /// <summary>Bluetooth HID over GATT (0x1812): what an ordinary Bluetooth gamepad advertises.</summary>
    public static readonly Guid HidService = Guid.Parse("00001812-0000-1000-8000-00805f9b34fb");
    /// <summary>GAP appearance values for HID gamepad and joystick.</summary>
    public const ushort AppearanceGamepad = 0x03C4, AppearanceJoystick = 0x03C3;

    /// <summary>The prefixes KeyLinker's LE scan lists (98:B6:E9, 98:B6:ED), as the upper three address bytes. 98:B6:EC is
    /// only a paired classic-Bluetooth hint in KeyLinker; it is not listed here and never rewritten to another address.</summary>
    static readonly uint[] FamilyPrefixes = [0x98B6E9, 0x98B6ED];

    public enum AddressKind { Unknown, Public, Random }

    /// <summary>One advertisement or scan response as Windows reported it.</summary>
    public sealed record Advert(ulong Address, AddressKind Kind, string? Name, IReadOnlyCollection<Guid> Services, ushort? Appearance, short Rssi, DateTimeOffset At);

    /// <summary>Why a device is listed, from advertisements alone.</summary>
    public enum Listing { Ignored, KnownModelName, FamilyCandidate, ServiceAdvertised, GenericController }

    /// <summary>Everything seen from one address during a scan, merged.</summary>
    public sealed class Seen
    {
        public ulong Address { get; init; }
        public AddressKind Kind { get; set; }
        public string? Name { get; set; }
        public HashSet<Guid> Services { get; } = [];
        public ushort? Appearance { get; set; }
        public short Rssi { get; set; }
        public int Adverts { get; set; }
        public DateTimeOffset First { get; set; }
        public DateTimeOffset Last { get; set; }
        public Listing Listing => Classify(this);
        /// <summary>The address type and prefix disagree: 98:B6 cannot be a valid random address (top bits 10 are reserved),
        /// so either Windows misreported the type or the device does not follow the standard. Listed, with a note.</summary>
        public bool AddressConflict => Kind == AddressKind.Random && HasFamilyPrefix(Address);
    }

    /// <summary>The name rules the client has always used to pick an X20 or X20 Pro. Unchanged; moved here so they can be tested.</summary>
    public static bool MatchesTarget(string targetModel, string name) => targetModel switch
    {
        "x20_pro" => name.Equals("EasySMX X20Pro", StringComparison.OrdinalIgnoreCase) ||
                     name.Equals("EasySMX X20 Pro", StringComparison.OrdinalIgnoreCase),
        "x20"     => name.Equals("EasySMX X20", StringComparison.OrdinalIgnoreCase) ||
                     name.Equals("Xpert2", StringComparison.OrdinalIgnoreCase),
        _         => name.StartsWith("EasySMX X20", StringComparison.OrdinalIgnoreCase)
    };

    public static bool HasFamilyPrefix(ulong address) => Array.IndexOf(FamilyPrefixes, (uint)(address >> 24 & 0xFFFFFF)) >= 0;

    /// <summary>Merge one advertisement into the scan. One entry per address; a scan response adds the name to the
    /// advertisement it follows. A pad on a rotating private address can appear under more than one address: nothing here
    /// links them, because nothing in an advertisement proves two addresses are one device.</summary>
    public static void Merge(IDictionary<ulong, Seen> seen, Advert a)
    {
        if (!seen.TryGetValue(a.Address, out var s)) seen[a.Address] = s = new Seen { Address = a.Address, First = a.At };
        if (a.Kind != AddressKind.Unknown) s.Kind = a.Kind;
        var name = a.Name?.Trim();
        if (!string.IsNullOrEmpty(name)) s.Name = name;
        foreach (var g in a.Services) s.Services.Add(g);
        if (a.Appearance is { } ap) s.Appearance = ap;
        s.Rssi = a.Rssi; s.Last = a.At; s.Adverts++;
    }

    public static Listing Classify(Seen s)
    {
        bool named = !string.IsNullOrEmpty(s.Name);
        // every name the client would pick for any target ("EasySMX X20…" covers the X20 Pro names; Xpert2 is the X20's)
        if (named && (MatchesTarget("", s.Name!) || MatchesTarget("x20", s.Name!))) return Listing.KnownModelName;
        if (s.Services.Contains(ConfigService)) return Listing.ServiceAdvertised;
        // KeyLinker lists a prefix match only with a name; an unnamed one is skipped the same way.
        if (named && HasFamilyPrefix(s.Address)) return Listing.FamilyCandidate;
        if (s.Services.Contains(HidService) || s.Appearance is AppearanceGamepad or AppearanceJoystick) return Listing.GenericController;
        return Listing.Ignored;
    }

    /// <summary>Devices worth showing, most likely first. A service listing is offered only for the first three kinds.</summary>
    public static IReadOnlyList<Seen> Listed(IEnumerable<Seen> seen) =>
        seen.Where(s => s.Listing != Listing.Ignored).OrderBy(s => s.Listing switch { Listing.KnownModelName => 0, Listing.ServiceAdvertised => 1, Listing.FamilyCandidate => 2, _ => 3 })
            .ThenByDescending(s => s.Rssi).ToList();

    public static bool MayListServices(Seen s) => s.Listing is Listing.KnownModelName or Listing.ServiceAdvertised or Listing.FamilyCandidate;

    // ------------------------------------------------------------------ what X20CTL may say about a device

    /// <summary>The service listing's result: UUIDs only. Null services means the device could not be reached.</summary>
    public sealed record GattListing(IReadOnlyCollection<Guid>? Services, IReadOnlyCollection<Guid> Characteristics, string? Error)
    {
        public bool Reached => Services != null;
        public bool HasConfigService => Services?.Contains(ConfigService) == true && Characteristics.Contains(ConfigWrite) && Characteristics.Contains(ConfigNotify);
    }

    public enum Status
    {
        /// <summary>Identity, operating mode and firmware are on the proven list. The only state settings may be written in.</summary>
        ProvenConfigurable,
        /// <summary>A recorded model (vendor and product id), but this mode or firmware was never proven: read only.</summary>
        RecordedUnvalidated,
        /// <summary>Answered the identity question with an id nobody has recorded.</summary>
        IdentifiedUnrecorded,
        /// <summary>Answered, and says none of its settings can be changed over this protocol.</summary>
        IdentifiedNothingConfigurable,
        /// <summary>The configuration service is there; what the device is has not been asked or was not answered.</summary>
        ServicePresentIdentityUnverified,
        /// <summary>Seen in advertisements with a known name or address prefix; nothing else known.</summary>
        FamilyCandidate,
        /// <summary>A Bluetooth controller without the configuration service: usable as a PC controller only.</summary>
        GenericController,
        /// <summary>Listed, but could not be reached, or stopped answering.</summary>
        Unresponsive,
    }

    /// <summary>The strongest thing the evidence supports. Only identity replies from the existing check can lift a device above
    /// ServicePresentIdentityUnverified, and only an exact proven identity reaches ProvenConfigurable.</summary>
    public static Status Assess(Listing listing, GattListing? gatt, PadCheck.Report? check)
    {
        if (check?.Identity is { } id)
        {
            if (VibrationTest.ProvenFor(id) is not null) return Status.ProvenConfigurable;
            if (check.Caps is { } caps && NothingConfigurable(caps)) return Status.IdentifiedNothingConfigurable;
            return check.Matches is not null ? Status.RecordedUnvalidated : Status.IdentifiedUnrecorded;
        }
        if (check is not null) return gatt is { Reached: false } ? Status.Unresponsive : Status.ServicePresentIdentityUnverified;
        if (gatt is { Reached: false }) return Status.Unresponsive;
        if (gatt is { HasConfigService: true }) return Status.ServicePresentIdentityUnverified;
        if (gatt is { Reached: true }) return Status.GenericController;
        return listing == Listing.GenericController ? Status.GenericController : Status.FamilyCandidate;
    }

    /// <summary>KeyLinker's own "nothing to configure" rule: the eight feature bytes after the length (sticks to engine) all zero.</summary>
    public static bool NothingConfigurable(PadCheck.Capabilities c) =>
        c.Sticks == 0 && c.Triggers == 0 && c.Motors == 0 && c.Turbo == 0 && c.Macros == 0 && c.ChangeKey == 0 && c.Lighting == 0 && c.Engine == 0;

    public static string Describe(Status s) => s switch
    {
        Status.ProvenConfigurable => "Proven. Settings can be changed.",
        Status.RecordedUnvalidated => "Known model, untested mode or firmware. Read only.",
        Status.IdentifiedUnrecorded => "Answered, but not a recorded model. Read only.",
        Status.IdentifiedNothingConfigurable => "Identified. Nothing configurable over Bluetooth.",
        Status.ServicePresentIdentityUnverified => "Has the configuration service. Not identified.",
        Status.FamilyCandidate => "Possible match. Not identified.",
        Status.GenericController => "Bluetooth controller. No configuration service.",
        _ => "Not reachable.",
    };

    // ------------------------------------------------------------------ privacy

    /// <summary>What may be shown or saved instead of an address: the public company prefix when the address is a family
    /// prefix (it names a vendor, not a device), otherwise nothing of it, plus a tag that tells devices apart within one
    /// session and cannot be turned back into the address.</summary>
    public static string Redact(ulong address, AddressKind kind, byte[] sessionSalt)
    {
        var tag = Convert.ToHexString(HMACSHA256.HashData(sessionSalt, BitConverter.GetBytes(address)).AsSpan(0, 3)).ToLowerInvariant();
        return kind != AddressKind.Random && HasFamilyPrefix(address) ? $"{address >> 40 & 0xFF:X2}:{address >> 32 & 0xFF:X2}:{address >> 24 & 0xFF:X2}:··:··:·· #{tag}" : $"#{tag}";
    }

    public static string Report(IReadOnlyList<Seen> listed, IReadOnlyDictionary<ulong, GattListing> gatt, byte[] salt, TimeSpan window)
    {
        var s = new StringBuilder();
        s.AppendLine($"Nearby Bluetooth controllers · listened {window.TotalSeconds:0} s · nothing sent");
        s.AppendLine(new string('─', 62));
        if (listed.Count == 0) s.AppendLine("  None found. Turn the controller on with Bluetooth and keep it near this PC.");
        foreach (var d in listed)
        {
            gatt.TryGetValue(d.Address, out var g);
            s.AppendLine($"  {d.Name ?? "(no name)"}   {Redact(d.Address, d.Kind, salt)}   {d.Rssi} dBm");
            s.AppendLine($"      {Describe(Assess(d.Listing, g, null))}");
            if (d.AddressConflict) s.AppendLine("      Windows reports a random address with a vendor prefix; the prefix may mean nothing.");
            if (g is { Reached: true, Services: { } svc }) s.AppendLine($"      services: {string.Join(", ", svc.Select(Short))}");
            if (g is { Reached: false, Error: { } e }) s.AppendLine($"      {e}");
        }
        s.AppendLine("  Only the controller check asks a controller what it is, and only an X20 or X20 Pro picked by name.");
        return s.ToString();
    }

    static string Short(Guid g)
    {
        var t = g.ToString();
        return t.EndsWith("-0000-1000-8000-00805f9b34fb") && t.StartsWith("0000") ? "0x" + t[4..8] : t;
    }

    // ------------------------------------------------------------------ the command gate the BLE client applies

    /// <summary>Null when the client may send this opcode in this session, otherwise why not. The controller check sends only
    /// its three questions; the write test only its session opcodes. Moved from NativeBleClient unchanged.</summary>
    public static string? Refusal(bool check, bool vibrationTest, byte opcode)
    {
        if (check && !PadCheck.IsQuery(opcode)) return $"The controller check may not send opcode 0x{opcode:X2}.";
        if (vibrationTest && Array.IndexOf(VibrationTest.SessionOpcodes, opcode) < 0) return $"The write test may not send opcode 0x{opcode:X2}.";
        return null;
    }
}
