using System.Text.RegularExpressions;
using X20Ctl.Product;
using Xunit;
using static X20Ctl.Product.BleDiscovery;

namespace X20Ctl.Zone.Tests;

/// <summary>
/// Broader Bluetooth discovery: what is listed, what may be said about it, and what must stay impossible. Offline only:
/// nothing here proves how any real device behaves over the air. Identity records marked "captured" are the X20 Pro's own
/// (owner's unit, 2 Nov 2026: flags 0x52 with gameplay on the cable, 0x56 on the 2.4 GHz dongle) and the X20 reply from
/// tests/test_protocol.py; everything else is synthetic.
/// </summary>
public class BleDiscoveryTests
{
    static readonly DateTimeOffset T0 = new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);
    static readonly byte[] Salt = Enumerable.Range(1, 16).Select(i => (byte)i).ToArray();
    static readonly byte[] ProMenu = Convert.FromHexString("0a030303013f030002c000");      // captured, X20 Pro (KeyLinker session)
    static readonly byte[] X20Menu = Convert.FromHexString("0a030303000f010002c000");      // tests/test_protocol.py
    // synthetic addresses only: no real device's address belongs in this repository
    const ulong ProAddress = 0x98B6EDA1B2C3, E9Address = 0x98B6E9112233, ECAddress = 0x98B6EC112233, EAAddress = 0x98B6EA112233, PhoneAddress = 0x5C00000ABCDE;

    static Advert Ad(ulong address, string? name = null, AddressKind kind = AddressKind.Public, Guid[]? services = null, ushort? appearance = null, short rssi = -60, int second = 0) =>
        new(address, kind, name, services ?? [], appearance, rssi, T0.AddSeconds(second));

    static Seen One(Advert a) { var d = new Dictionary<ulong, Seen>(); Merge(d, a); return d[a.Address]; }

    // ---------------------------------------------------------------- X20 / X20 Pro regression

    /// <summary>The selection switch exactly as NativeBleClient had it before the move, kept here as the reference.</summary>
    static bool LegacyMatch(string targetModel, string name) => targetModel switch
    {
        "x20_pro" => name.Equals("EasySMX X20Pro", StringComparison.OrdinalIgnoreCase) || name.Equals("EasySMX X20 Pro", StringComparison.OrdinalIgnoreCase),
        "x20" => name.Equals("EasySMX X20", StringComparison.OrdinalIgnoreCase) || name.Equals("Xpert2", StringComparison.OrdinalIgnoreCase),
        _ => name.StartsWith("EasySMX X20", StringComparison.OrdinalIgnoreCase)
    };

    [Fact] // 1
    public void Selection_by_name_is_unchanged_for_every_target()
    {
        foreach (var target in new[] { "x20_pro", "x20", "", "dune" })
            foreach (var name in new[] { "EasySMX X20Pro", "EasySMX X20 Pro", "easysmx x20pro", "EasySMX X20", "Xpert2", "xpert2", "EasySMX X20 Lite", "", "Xbox Wireless Controller", "EasySMX Dune" })
                Assert.Equal(LegacyMatch(target, name), MatchesTarget(target, name));
    }

    [Fact] // 2
    public void Advertised_service_rule_and_its_callers_are_unchanged()
    {
        var client = Source("X20Ctl.Product", "NativeBleClient.cs");
        Assert.Contains("bool requireAdvertisedService = true", client);
        Assert.Contains("if (requireAdvertisedService && !seenServices.Contains(addr))", client);
        Assert.Contains("FindDeviceAddressAsync(targetModel, timeoutSeconds, requireAdvertisedService: false)", client); // QueryAsync
        Assert.Contains("FindDeviceAddressAsync(targetModel, timeoutSeconds);", client);                                   // VibrationTestAsync keeps the default
        Assert.Contains("(string.IsNullOrEmpty(name) && cachedAddress == addr)", client);
        Assert.Contains("requireAdvertisedService: false", Source("X20Ctl.Product", "LightingBleClient.cs"));
    }

    [Fact] // 3
    public void The_x20_pro_is_proven_only_in_the_mode_that_was_proven()
    {
        var cable = PadCheck.Build(new(Convert.FromHexString("0710202006014452"), ProMenu, null, null));   // captured, mode 2
        var dongle = PadCheck.Build(new(Convert.FromHexString("0710202006014456"), ProMenu, null, null));  // captured, mode 6
        Assert.Equal(2, cable.Identity!.Mode); Assert.Equal(6, dongle.Identity!.Mode);
        Assert.Equal(Status.RecordedUnvalidated, Assess(Listing.KnownModelName, null, cable));
        Assert.Equal(Status.ProvenConfigurable, Assess(Listing.KnownModelName, null, dongle));
        Assert.False(VibrationTest.MayRun(cable.Identity, cable.Caps));
    }

    [Fact] // 4
    public void The_recorded_x20_reply_is_proven()
    {
        var x20 = PadCheck.Build(new(Convert.FromHexString("0710132009012352"), X20Menu, null, null));
        Assert.Equal(Status.ProvenConfigurable, Assess(Listing.KnownModelName, null, x20));
        Assert.Equal(Listing.KnownModelName, One(Ad(0x98B6ED0A0B0C, "EasySMX X20")).Listing);
        Assert.Equal(Listing.KnownModelName, One(Ad(PhoneAddress, "Xpert2")).Listing); // names are matched wherever the address is
    }

    // ---------------------------------------------------------------- unknown devices

    [Fact] // 5
    public void Family_prefixes_need_a_name_and_ec_and_ea_are_not_family_prefixes()
    {
        Assert.Equal(Listing.FamilyCandidate, One(Ad(E9Address, "Gamepad")).Listing);
        Assert.Equal(Listing.FamilyCandidate, One(Ad(ProAddress, "Anything")).Listing);
        Assert.Equal(Listing.Ignored, One(Ad(ProAddress)).Listing);            // KeyLinker skips unnamed devices too
        Assert.Equal(Listing.Ignored, One(Ad(ECAddress, "Gamepad")).Listing);  // a paired-classic hint in KeyLinker, not an LE prefix
        Assert.Equal(Listing.Ignored, One(Ad(EAAddress, "Gamepad")).Listing);
        Assert.Equal(Listing.Ignored, One(Ad(PhoneAddress, "Someone's phone")).Listing);
    }

    [Fact] // 6
    public void An_advertised_configuration_service_is_listed_whatever_the_name_or_address()
    {
        Assert.Equal(Listing.ServiceAdvertised, One(Ad(PhoneAddress, null, AddressKind.Random, [ConfigService])).Listing);
    }

    [Fact] // 7
    public void Ordinary_bluetooth_gamepads_are_generic_controllers_only()
    {
        var xbox = One(Ad(0x5CBA37000001, "Xbox Wireless Controller", services: [HidService]));
        Assert.Equal(Listing.GenericController, xbox.Listing);
        Assert.Equal(Listing.GenericController, One(Ad(0x5CBA37000002, null, appearance: AppearanceGamepad)).Listing);
        Assert.False(MayListServices(xbox));
        Assert.Equal(Status.GenericController, Assess(xbox.Listing, null, null));
    }

    [Fact] // 8
    public void Every_status_needs_its_own_evidence()
    {
        var unknownId = PadCheck.Build(new(Convert.FromHexString("0712342345010052"), X20Menu, null, null));          // synthetic
        var nothing = PadCheck.Build(new(Convert.FromHexString("0712342345010052"), Convert.FromHexString("0a00000000000000000000"), null, null)); // synthetic
        var silent = PadCheck.Build(new(null, null, null, null));
        var reached = new GattListing([ConfigService, HidService], [ConfigWrite, ConfigNotify], null);
        var noConfig = new GattListing([HidService], [], null);
        var halfConfig = new GattListing([ConfigService], [ConfigWrite], null);
        var gone = new GattListing(null, [], "Did not answer in time.");

        Assert.Equal(Status.IdentifiedUnrecorded, Assess(Listing.FamilyCandidate, reached, unknownId));
        Assert.Equal(Status.IdentifiedNothingConfigurable, Assess(Listing.FamilyCandidate, reached, nothing));
        Assert.Equal(Status.ServicePresentIdentityUnverified, Assess(Listing.FamilyCandidate, reached, silent));
        Assert.Equal(Status.ServicePresentIdentityUnverified, Assess(Listing.FamilyCandidate, reached, null));
        Assert.Equal(Status.GenericController, Assess(Listing.FamilyCandidate, noConfig, null));
        Assert.Equal(Status.GenericController, Assess(Listing.FamilyCandidate, halfConfig, null)); // service without both characteristics
        Assert.Equal(Status.Unresponsive, Assess(Listing.FamilyCandidate, gone, null));
        Assert.Equal(Status.FamilyCandidate, Assess(Listing.FamilyCandidate, null, null));
        Assert.Equal(Status.FamilyCandidate, Assess(Listing.ServiceAdvertised, null, null));
        foreach (var s in Enum.GetValues<Status>().Where(s => s != Status.ProvenConfigurable))
            Assert.DoesNotContain("can be changed", Describe(s));
    }

    [Fact] // 9
    public void A_six_byte_identity_record_is_kept_without_a_mode_and_never_proven()
    {
        var shortId = PadCheck.Build(new(Convert.FromHexString("06102020060144"), ProMenu, null, null)); // synthetic: no flags byte
        Assert.Equal(new PadCheck.Identity("1020", "2006", "1.44", null), shortId.Identity);
        Assert.Equal(Status.RecordedUnvalidated, Assess(Listing.KnownModelName, null, shortId));
    }

    // ---------------------------------------------------------------- addresses and privacy

    [Fact] // 10
    public void Address_types_are_recorded_not_trusted()
    {
        var random = One(Ad(ProAddress, "Gamepad", AddressKind.Random));
        Assert.Equal(Listing.FamilyCandidate, random.Listing);
        Assert.True(random.AddressConflict);
        var unknown = One(Ad(ProAddress, "Gamepad", AddressKind.Unknown));
        Assert.Equal(Listing.FamilyCandidate, unknown.Listing);
        Assert.False(unknown.AddressConflict);
        // an Unknown report later never erases a type seen earlier
        var d = new Dictionary<ulong, Seen>(); Merge(d, Ad(ProAddress, "Gamepad", AddressKind.Public)); Merge(d, Ad(ProAddress, null, AddressKind.Unknown));
        Assert.Equal(AddressKind.Public, d[ProAddress].Kind);
    }

    [Fact] // 11
    public void Redacted_addresses_hide_the_device_part_and_are_stable_per_session()
    {
        string a = Redact(ProAddress, AddressKind.Public, Salt);
        Assert.StartsWith("98:B6:ED:··:··:·· #", a);
        Assert.DoesNotContain("A1:B2:C3", a, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(a, Redact(ProAddress, AddressKind.Public, Salt));
        Assert.NotEqual(a, Redact(ProAddress, AddressKind.Public, new byte[16]));
        Assert.NotEqual(a[^7..], Redact(E9Address, AddressKind.Public, Salt)[^7..]); // different devices, different tags
        Assert.StartsWith("#", Redact(PhoneAddress, AddressKind.Public, Salt));  // not a family prefix: nothing of it shown
        Assert.StartsWith("#", Redact(ProAddress, AddressKind.Random, Salt));
    }

    [Fact] // 12
    public void The_report_never_contains_a_full_address()
    {
        var d = new Dictionary<ulong, Seen>();
        Merge(d, Ad(ProAddress, "EasySMX X20Pro")); Merge(d, Ad(E9Address, "Gamepad")); Merge(d, Ad(0x5CBA37000001, "Xbox Wireless Controller", services: [HidService]));
        var text = Report(Listed(d.Values), new Dictionary<ulong, GattListing> { [ProAddress] = new([ConfigService], [ConfigWrite, ConfigNotify], null) }, Salt, TimeSpan.FromSeconds(8));
        Assert.DoesNotMatch(new Regex("([0-9A-F]{2}:){5}[0-9A-F]{2}", RegexOptions.IgnoreCase), text);
        Assert.DoesNotContain("98B6", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("nothing sent", text);
        Assert.Contains("Has the configuration service. Not identified.", text);
        Assert.Contains("Possible match. Not identified.", text);
    }

    // ---------------------------------------------------------------- duplicates

    [Fact] // 13
    public void An_advertisement_and_its_scan_response_are_one_device()
    {
        var d = new Dictionary<ulong, Seen>();
        Merge(d, Ad(ProAddress, null, services: [ConfigService]));
        Merge(d, Ad(ProAddress, "EasySMX X20Pro", second: 1));
        Assert.Single(d);
        Assert.Equal("EasySMX X20Pro", d[ProAddress].Name);
        Assert.Contains(ConfigService, d[ProAddress].Services);
        Merge(d, Ad(ProAddress, "  ", second: 2));
        Assert.Equal("EasySMX X20Pro", d[ProAddress].Name); // a blank name never erases a real one
    }

    [Fact] // 14
    public void Repeats_update_the_entry_instead_of_adding_one()
    {
        var d = new Dictionary<ulong, Seen>();
        for (int i = 0; i < 100; i++) Merge(d, Ad(ProAddress, "EasySMX X20Pro", rssi: (short)(-40 - i % 7), second: i));
        var s = Assert.Single(d).Value;
        Assert.Equal(100, s.Adverts);
        Assert.Equal(T0, s.First); Assert.Equal(T0.AddSeconds(99), s.Last);
        Assert.Equal((short)(-40 - 99 % 7), s.Rssi);
    }

    [Fact] // 15
    public void Two_pads_with_one_name_stay_two_and_ec_is_never_rewritten()
    {
        var d = new Dictionary<ulong, Seen>();
        Merge(d, Ad(ProAddress, "EasySMX X20Pro")); Merge(d, Ad(0x98B6ED000001, "EasySMX X20Pro"));
        Merge(d, Ad(ECAddress, "Gamepad")); Merge(d, Ad(0x98B6ED112233, "Gamepad"));   // EC and ED sharing the last three bytes
        Assert.Equal(4, d.Count);
        Assert.True(d.ContainsKey(ECAddress));
        Assert.Equal(3, Listed(d.Values).Count); // EC is not listed by prefix, and not folded into its ED look-alike
    }

    // ---------------------------------------------------------------- write gating and command paths

    [Fact] // 16
    public void Only_a_proven_identity_ever_allows_a_write()
    {
        var caps = PadCheck.ParseCapabilities(X20Menu)!;
        foreach (var vid in new[] { "1013", "1020", "1234" })
            foreach (var pid in new[] { "2009", "2006", "2345" })
                foreach (var fw in new[] { "1.23", "1.44", "1.00" })
                    foreach (int? mode in new int?[] { null, 0, 2, 6, 7, 8 })
                    {
                        var id = new PadCheck.Identity(vid, pid, fw, mode);
                        var report = new PadCheck.Report(id, caps, null, null, [], []);
                        bool proven = Assess(Listing.FamilyCandidate, null, report) == Status.ProvenConfigurable;
                        Assert.Equal(VibrationTest.ProvenFor(id) is not null, proven);
                        if (VibrationTest.MayRun(id, caps)) Assert.True(proven);
                    }
        GattListing?[] listings = [null, new(null, [], "x"), new([HidService], [], null), new([ConfigService], [ConfigWrite, ConfigNotify], null)];
        foreach (var listing in Enum.GetValues<Listing>()) // advertisements and a service listing alone never reach the proven state
            foreach (var gatt in listings)
                Assert.NotEqual(Status.ProvenConfigurable, Assess(listing, gatt, null));
    }

    [Fact] // 17
    public void The_command_gate_lets_each_session_send_only_its_own_opcodes()
    {
        for (int op = 0; op < 256; op++)
        {
            byte b = (byte)op;
            Assert.Equal(PadCheck.IsQuery(b), Refusal(true, false, b) is null);
            Assert.Equal(Array.IndexOf(VibrationTest.SessionOpcodes, b) >= 0, Refusal(false, true, b) is null);
            Assert.Null(Refusal(false, false, b)); // the telemetry path is gated by identity, as before
        }
        Assert.Contains("BleDiscovery.Refusal(check, vibrate != null, opcode)", Source("X20Ctl.Product", "NativeBleClient.cs"));
    }

    [Fact] // 18
    public void Discovery_code_cannot_send_pair_or_read_values()
    {
        string[] forbidden = ["X20Codec", "IPadLink", "SendAsync", "SendQuery", "WriteValue", "WriteClientCharacteristic", "ReadValue", "Pair", "PadBridge", "NativeBleClient."];
        foreach (var file in new[] { "BleDiscovery.cs", "NativeBleDiscovery.cs" })
        {
            var code = Code(Source("X20Ctl.Product", file));
            foreach (var token in forbidden) Assert.DoesNotContain(token, code);
        }
        Assert.DoesNotContain("PadCheck", Code(Source("X20Ctl.Product", "NativeBleDiscovery.cs")));
        // exception text can carry a device address; discovery shows error codes only
        Assert.DoesNotContain(".Message", Code(Source("X20Ctl.Product", "NativeBleDiscovery.cs")));
        var view = Code(Source("X20Ctl.Product", "Scanner", "WriteScanView.cs"));
        int from = view.IndexOf("Task FindNearby()"), to = view.IndexOf("Task RunWriteTest()");
        Assert.DoesNotContain(".Message", view[from..to]);
    }

    [Fact] // 19
    public void Service_listing_is_offered_only_for_possible_matches()
    {
        Assert.True(MayListServices(One(Ad(ProAddress, "EasySMX X20Pro"))));
        Assert.True(MayListServices(One(Ad(E9Address, "Gamepad"))));
        Assert.True(MayListServices(One(Ad(PhoneAddress, null, services: [ConfigService]))));
        Assert.False(MayListServices(One(Ad(PhoneAddress, "Someone's phone"))));
        Assert.False(MayListServices(One(Ad(0x5CBA37000001, "Xbox Wireless Controller", services: [HidService]))));
    }

    [Fact] // 20
    public void The_scan_view_asks_nothing_of_found_devices()
    {
        var view = Source("X20Ctl.Product", "Scanner", "WriteScanView.cs");
        var find = Between(view, "private async Task FindNearby()", "private async Task RunWriteTest()");
        foreach (var token in new[] { "PadBridge", "NativeBleClient", "TestVibration", "X20Codec" }) Assert.DoesNotContain(token, find);
        Assert.Contains("VibrationTest.MayRun(PadCheck.ParseIdentity(raw.Identity), PadCheck.ParseCapabilities(raw.Menu))", view); // write-test gate as before
    }

    // ---------------------------------------------------------------- helpers

    static string Source(params string[] parts)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            var path = Path.Combine([dir.FullName, .. parts]);
            if (File.Exists(path)) return File.ReadAllText(path);
        }
        throw new FileNotFoundException(string.Join("/", parts));
    }

    /// <summary>Code without comments, so a word in an explanation is not mistaken for a call.</summary>
    static string Code(string source) => Regex.Replace(Regex.Replace(source, @"//[^\n]*", ""), @"/\*.*?\*/", "", RegexOptions.Singleline);

    static string Between(string text, string start, string end)
    {
        int a = text.IndexOf(start, StringComparison.Ordinal), b = text.IndexOf(end, a, StringComparison.Ordinal);
        Assert.True(a >= 0 && b > a);
        return text[a..b];
    }
}
