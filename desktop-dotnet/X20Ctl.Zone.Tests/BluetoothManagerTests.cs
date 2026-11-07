using System.Text.RegularExpressions;
using X20Ctl.Product;
using Xunit;
using static X20Ctl.Product.BluetoothManager;

namespace X20Ctl.Zone.Tests;

/// <summary>
/// The Bluetooth manager's connection lifecycle, against a fake device layer. Offline only: these prove the logic X20CTL
/// runs around Windows' Bluetooth calls, not how Windows, the radio or any controller behaves. All addresses are synthetic.
/// </summary>
public class BluetoothManagerTests
{
    const ulong Pro = 0x98B6EDA1B2C3, Other = 0x98B6ED0A0B0C;
    static readonly DateTimeOffset T0 = new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);

    sealed class FakeLink : IConfigLink
    {
        public event Action<bool>? LinkChanged;
        public event Action<string>? SessionChanged;
        public bool IsUp { get; set; } = true;
        public ServiceProbe Probe = new(true, true);
        public int Disposed;
        public Task<ServiceProbe> ProbeAsync(CancellationToken token) => Task.FromResult(Probe);
        public ValueTask DisposeAsync() { Disposed++; return default; }
        public void Drop() { IsUp = false; LinkChanged?.Invoke(false); }
        public void Up() { IsUp = true; LinkChanged?.Invoke(true); }
        public void Session(string s) => SessionChanged?.Invoke(s);
    }

    sealed class FakeFactory : IConfigLinkFactory
    {
        public readonly List<FakeLink> Opened = [];
        public readonly List<ulong> Asked = [];
        public Func<ulong, CancellationToken, Task<IConfigLink?>>? Next;
        int inside;
        public int MaxInside;
        public async Task<IConfigLink?> OpenAsync(ulong address, CancellationToken token)
        {
            MaxInside = Math.Max(MaxInside, Interlocked.Increment(ref inside));
            try
            {
                Asked.Add(address);
                await Task.Yield();
                if (Next != null) return await Next(address, token);
                var l = new FakeLink(); Opened.Add(l); return l;
            }
            finally { Interlocked.Decrement(ref inside); }
        }
    }

    sealed class Rig
    {
        public readonly FakeFactory Factory = new();
        public readonly SemaphoreSlim Gate = new(1, 1);
        public readonly BluetoothLog Log = new(() => T0);
        public readonly List<TimeSpan> Waits = [];
        public readonly List<LinkState> States = [];
        public readonly ConfigSession Session;
        public Rig(TimeSpan? busy = null, Task? hold = null)
        {
            Session = new(Factory, Gate, Log, async (t, token) => { Waits.Add(t); if (hold != null) await hold.WaitAsync(token); token.ThrowIfCancellationRequested(); }, busyWait: busy ?? TimeSpan.FromSeconds(2), openTimeout: TimeSpan.FromSeconds(2));
            Session.Changed += () => States.Add(Session.State);
        }
        public string Text => string.Join("\n", Log.Entries.Select(e => e.Text));
    }

    static BleDiscovery.Seen Le(ulong address, string? name, Guid[]? services = null, int second = 0)
    {
        var d = new Dictionary<ulong, BleDiscovery.Seen>();
        BleDiscovery.Merge(d, new(address, BleDiscovery.AddressKind.Public, name, services ?? [], null, -50, T0.AddSeconds(second)));
        return d[address];
    }
    static ClassicDevice Win(string id, string name, bool paired = false, bool? connected = null, uint? cod = 5, bool vendor = false) => new(id, name, paired, connected, !paired, cod, vendor, null);

    // ---------------------------------------------------------------- the list

    [Fact] // 1, 2
    public void Each_device_is_one_card_and_keeps_its_real_name()
    {
        var d = new Dictionary<ulong, BleDiscovery.Seen>();
        foreach (var name in new[] { null, "EasySMX X20Pro", " ", "EasySMX X20Pro" })
            BleDiscovery.Merge(d, new(Pro, BleDiscovery.AddressKind.Public, name, [], null, -50, T0));
        var rows = Rows(d.Values, [Win("w1", "Xbox Wireless Controller"), Win("w1", "Xbox Wireless Controller", paired: true)]);
        Assert.Equal(2, rows.Count);
        var le = Assert.Single(rows, r => r.Le != null);
        Assert.Equal("EasySMX X20Pro", le.Name);
        Assert.Equal("advertised by the device", le.NameSource);
        var win = Assert.Single(rows, r => r.Classic != null);
        Assert.Equal("Windows device name", win.NameSource);
        Assert.True(win.Classic!.Paired); // the later report of the same Windows id wins
    }

    [Fact] // 3
    public void A_configuration_peripheral_and_a_gameplay_device_are_never_merged()
    {
        // same name, EasySMX ranges on both, the EC/ED pairing KeyLinker shows as one: still two entries, two kinds
        var rows = Rows([Le(Pro, "EasySMX X20Pro")], [Win("w1", "EasySMX X20Pro", vendor: true)]);
        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, r => r.Kind == RowKind.ConfigPeripheral && r.Classic == null);
        Assert.Contains(rows, r => r.Kind == RowKind.Gameplay && r.Le == null);
        Assert.Equal(2, rows.Select(r => r.Key).Distinct().Count());
    }

    [Fact] // 5
    public void Unnamed_silent_devices_and_non_controllers_stay_out()
    {
        var rows = Rows([Le(0x5C00000ABCDE, null), Le(0x5C00000ABCDF, "Someone's phone")],
            [Win("h", "SpaceBuds Neo+", cod: 4), Win("m", "Keyboard", cod: null), Win("g", "Wireless Gamepad", cod: null)]);
        var only = Assert.Single(rows);
        Assert.Equal("Wireless Gamepad", only.Name);
        Assert.True(IsControllerLike(Win("x", "Anything", cod: 5)));   // Windows' peripheral class
    }

    // ---------------------------------------------------------------- connecting

    [Fact] // 12
    public async Task Connect_reaches_ready_only_after_the_service_answers()
    {
        var rig = new Rig();
        Assert.True(await rig.Session.ConnectAsync(Pro, "pad"));
        Assert.Equal([LinkState.Connecting, LinkState.Ready], rig.States);
        Assert.True(rig.Session.Held);
        Assert.Equal(1, rig.Gate.CurrentCount); // the shared gate is only held while opening
        Assert.Contains("Nothing was sent", rig.Session.Detail);
    }

    [Fact] // 6
    public async Task A_device_Windows_cannot_reach_fails_and_nothing_is_held()
    {
        var rig = new Rig();
        rig.Factory.Next = (_, _) => Task.FromResult<IConfigLink?>(null);
        Assert.False(await rig.Session.ConnectAsync(Pro, "pad"));
        Assert.Equal(LinkState.Failed, rig.Session.State);
        Assert.Equal(LinkCause.Unreachable, rig.Session.Cause);
        Assert.False(rig.Session.Held);
    }

    [Fact] // 7
    public async Task A_Windows_error_is_reported_with_its_code()
    {
        var rig = new Rig();
        rig.Factory.Next = (_, _) => throw new System.Runtime.InteropServices.COMException("device 98:B6:ED:A1:B2:C3 gone", unchecked((int)0x8007048F));
        Assert.False(await rig.Session.ConnectAsync(Pro, "pad"));
        Assert.Equal(LinkCause.ApiError, rig.Session.Cause);
        var last = rig.Log.Entries[^1];
        Assert.Equal("0x8007048F", last.Code);
        Assert.DoesNotContain("A1", rig.Text); // exception text never reaches the log
    }

    [Fact] // 8
    public async Task A_stale_cached_device_that_does_not_answer_is_released()
    {
        var rig = new Rig();
        var stale = new FakeLink { Probe = new(false, false, "Unreachable") };
        rig.Factory.Next = (_, _) => Task.FromResult<IConfigLink?>(stale);
        Assert.False(await rig.Session.ConnectAsync(Pro, "pad"));
        Assert.Equal(LinkCause.Unreachable, rig.Session.Cause);
        Assert.Equal(1, stale.Disposed);
        Assert.False(rig.Session.Held);
    }

    [Fact] // 10
    public async Task A_device_without_the_configuration_service_is_not_kept()
    {
        var rig = new Rig();
        var plain = new FakeLink { Probe = new(true, false) };
        rig.Factory.Next = (_, _) => Task.FromResult<IConfigLink?>(plain);
        Assert.False(await rig.Session.ConnectAsync(Pro, "pad"));
        Assert.Equal(LinkState.NoService, rig.Session.State);
        Assert.Equal(1, plain.Disposed);
        Assert.False(rig.Session.Held);
    }

    [Fact] // 11
    public async Task Switching_devices_releases_the_first_and_late_events_are_ignored()
    {
        var rig = new Rig();
        await rig.Session.ConnectAsync(Pro, "a");
        var first = rig.Factory.Opened[0];
        await rig.Session.ConnectAsync(Other, "b");
        Assert.Equal(1, first.Disposed);
        first.Drop(); // the released link reporting late
        Assert.Equal(LinkState.Ready, rig.Session.State);
        Assert.Equal(Other, rig.Session.Target);
    }

    [Fact] // 11, 13
    public async Task Disconnect_releases_once_and_never_reconnects()
    {
        var rig = new Rig();
        rig.Session.AutoReconnect = true;
        await rig.Session.ConnectAsync(Pro, "pad");
        var link = rig.Factory.Opened[0];
        await rig.Session.DisconnectAsync();
        Assert.Equal(1, link.Disposed);
        Assert.Equal(LinkCause.YouDisconnected, rig.Session.Cause);
        link.Drop(); link.Up();
        await rig.Session.Reconnecting;
        Assert.Single(rig.Factory.Asked);
        Assert.Equal(LinkState.Disconnected, rig.Session.State);
        Assert.Contains("nothing was unpaired", rig.Text);
    }

    [Fact] // 12
    public async Task A_drop_without_auto_reconnect_reports_no_invented_reason_and_releases()
    {
        var rig = new Rig();
        await rig.Session.ConnectAsync(Pro, "pad");
        var link = rig.Factory.Opened[0];
        link.Session("Closed");
        link.Drop();
        Assert.Equal(LinkState.Disconnected, rig.Session.State);
        Assert.Equal(LinkCause.LinkLost, rig.Session.Cause);
        Assert.Contains("Windows gave no reason", rig.Session.Detail);
        Assert.Contains("GATT session: Closed", rig.Text);
        for (int i = 0; i < 50 && rig.Session.Held; i++) await Task.Delay(10);
        Assert.False(rig.Session.Held);
        Assert.Equal(1, link.Disposed);
        Assert.Single(rig.Factory.Asked);
    }

    [Fact] // 12
    public async Task Windows_bringing_the_link_back_restores_ready()
    {
        // the first wait before our own retry is held open, so Windows (MaintainConnection) gets there first
        var hold = new TaskCompletionSource();
        var rig = new Rig(hold: hold.Task);
        rig.Session.AutoReconnect = true;
        await rig.Session.ConnectAsync(Pro, "pad");
        var link = rig.Factory.Opened[0];
        link.Drop();
        Assert.Equal(LinkCause.LinkLost, rig.Session.Cause);
        link.Up();
        Assert.Equal(LinkState.Ready, rig.Session.State);
        Assert.Equal("The link came back.", rig.Session.Detail);
        hold.SetResult();
        await rig.Session.Reconnecting;
        Assert.Single(rig.Factory.Asked); // no extra connection once Windows restored it
        Assert.Equal(0, link.Disposed);
    }

    [Fact] // 14
    public async Task Reconnect_is_bounded_with_growing_waits()
    {
        var rig = new Rig();
        rig.Session.AutoReconnect = true;
        await rig.Session.ConnectAsync(Pro, "pad");
        rig.Factory.Next = (_, _) => Task.FromResult<IConfigLink?>(null);
        rig.Factory.Opened[0].Drop();
        await rig.Session.Reconnecting;
        Assert.Equal(1 + ConfigSession.DefaultBackoff.Length, rig.Factory.Asked.Count);
        Assert.Equal(ConfigSession.DefaultBackoff, rig.Waits);
        Assert.True(rig.Waits.Zip(rig.Waits.Skip(1)).All(p => p.Second > p.First));
        Assert.Equal(LinkCause.GaveUp, rig.Session.Cause);
        Assert.All(rig.Factory.Asked, a => Assert.Equal(Pro, a)); // never another device
    }

    [Fact] // 14
    public async Task Reconnect_succeeds_and_stops_trying()
    {
        var rig = new Rig();
        rig.Session.AutoReconnect = true;
        await rig.Session.ConnectAsync(Pro, "pad");
        int calls = 0;
        rig.Factory.Next = (_, _) => Task.FromResult<IConfigLink?>(++calls == 1 ? null : new FakeLink());
        rig.Factory.Opened[0].Drop();
        await rig.Session.Reconnecting;
        Assert.Equal(LinkState.Ready, rig.Session.State);
        Assert.Equal(2, calls);
        Assert.Equal(1, rig.Factory.Opened[0].Disposed);
    }

    [Fact] // 14
    public async Task Reconnect_stops_when_the_address_answers_without_the_service()
    {
        var rig = new Rig();
        rig.Session.AutoReconnect = true;
        await rig.Session.ConnectAsync(Pro, "pad");
        rig.Factory.Next = (_, _) => Task.FromResult<IConfigLink?>(new FakeLink { Probe = new(true, false) });
        rig.Factory.Opened[0].Drop();
        await rig.Session.Reconnecting;
        Assert.Equal(LinkState.NoService, rig.Session.State);
        Assert.Equal(2, rig.Factory.Asked.Count);
    }

    [Fact] // 15
    public async Task Operations_never_overlap_and_a_busy_gate_is_reported()
    {
        var rig = new Rig();
        var slow = new TaskCompletionSource<IConfigLink?>();
        rig.Factory.Next = (_, _) => slow.Task;
        var a = rig.Session.ConnectAsync(Pro, "a");
        var b = rig.Session.ConnectAsync(Other, "b");
        slow.SetResult(new FakeLink());
        rig.Factory.Next = null;
        await Task.WhenAll(a, b);
        Assert.Equal(1, rig.Factory.MaxInside);
        Assert.Equal(Other, rig.Session.Target);

        var busy = new Rig(busy: TimeSpan.FromMilliseconds(50));
        await busy.Gate.WaitAsync(); // another X20CTL Bluetooth operation
        Assert.False(await busy.Session.ConnectAsync(Pro, "pad"));
        Assert.Equal(LinkCause.Busy, busy.Session.Cause);
        Assert.Empty(busy.Factory.Asked);
    }

    [Fact] // 16
    public async Task Cancelling_a_connect_leaves_nothing_held()
    {
        var rig = new Rig();
        rig.Factory.Next = async (_, token) => { await Task.Delay(Timeout.Infinite, token); return null; };
        using var cts = new CancellationTokenSource();
        var connecting = rig.Session.ConnectAsync(Pro, "pad", cts.Token);
        cts.Cancel();
        Assert.False(await connecting);
        Assert.Equal(LinkCause.Cancelled, rig.Session.Cause);
        Assert.False(rig.Session.Held);
        Assert.Equal(1, rig.Gate.CurrentCount);
    }

    [Fact] // 16
    public async Task An_open_that_never_returns_times_out()
    {
        var rig = new Rig();
        rig.Factory.Next = async (_, token) => { await Task.Delay(Timeout.Infinite, token); return null; };
        Assert.False(await rig.Session.ConnectAsync(Pro, "pad"));
        Assert.Equal(LinkCause.Timeout, rig.Session.Cause);
    }

    [Fact] // 13, 16
    public async Task Disconnect_during_reconnect_stops_it()
    {
        var rig = new Rig();
        rig.Session.AutoReconnect = true;
        await rig.Session.ConnectAsync(Pro, "pad");
        var hang = new TaskCompletionSource();
        rig.Factory.Next = async (_, token) => { hang.TrySetResult(); await Task.Delay(Timeout.Infinite, token); return null; };
        rig.Factory.Opened[0].Drop();
        await hang.Task;
        await rig.Session.DisconnectAsync();
        await rig.Session.Reconnecting;
        Assert.Equal(LinkCause.YouDisconnected, rig.Session.Cause);
        Assert.Equal(2, rig.Factory.Asked.Count);
        Assert.False(rig.Session.Held);
    }

    // ---------------------------------------------------------------- pairing and gameplay

    [Fact] // 9
    public void Pairing_results_are_said_plainly_and_only_success_counts()
    {
        Assert.True(PairSucceeded("Paired")); Assert.True(PairSucceeded("AlreadyPaired"));
        foreach (var s in new[] { "NotReadyToPair", "AccessDenied", "AuthenticationTimeout", "RequiredHandlerNotRegistered", "Error", "Failed" })
            Assert.False(PairSucceeded(s));
        Assert.Equal("The controller isn't in pairing mode.", PairOutcome("NotReadyToPair"));
        Assert.Contains("Windows needs its own pairing window", PairOutcome("RequiredHandlerNotRegistered"));
        Assert.Equal("Pairing failed (SomethingNew).", PairOutcome("SomethingNew")); // unknown statuses are named, not guessed
    }

    [Fact] // 17
    public void Configuration_and_gameplay_are_reported_apart()
    {
        Assert.DoesNotContain("gameplay", ConfigSession.Describe(LinkState.Ready), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("can be changed", ConfigSession.Describe(LinkState.Ready));
        Assert.Equal("Paired. Windows reports it connected.", Gameplay(Win("w", "Pad", paired: true, connected: true)));
        Assert.Equal("Paired. Windows didn't say whether it is connected.", Gameplay(Win("w", "Pad", paired: true, connected: null)));
        Assert.Contains("isn't known", XInput(1)); // a slot is never pinned on a device
        var source = Code(Source("X20Ctl.Product", "Scanner", "WriteScanView.Bluetooth.cs"));
        Assert.Contains("Not this entry. Gameplay over Bluetooth is a separate Windows device.", source);
    }

    // ---------------------------------------------------------------- privacy and safety

    [Fact] // 18
    public void The_log_and_its_export_never_hold_an_address()
    {
        var log = new BluetoothLog(() => T0);
        log.Add("seen 98:B6:ED:A1:B2:C3 and 98-b6-ed-a1-b2-c3 and 98B6EDA1B2C3");
        string text = log.Export("header 98:B6:ED:A1:B2:C3");
        Assert.DoesNotMatch(new Regex("(?i)([0-9a-f]{2}[:-]){5}[0-9a-f]{2}"), text);
        Assert.DoesNotContain("98B6EDA1B2C3", text, StringComparison.OrdinalIgnoreCase);
        for (int i = 0; i < BluetoothLog.Cap + 20; i++) log.Add("line " + i);
        Assert.Equal(BluetoothLog.Cap, log.Entries.Count); // capped
        foreach (var file in new[] { new[] { "X20Ctl.Product", "WinBluetooth.cs" }, ["X20Ctl.Product", "BluetoothManager.cs"], ["X20Ctl.Product", "Scanner", "WriteScanView.Bluetooth.cs"] })
            Assert.DoesNotContain(".Message", Code(Source(file)));
    }

    [Fact] // 19
    public void The_existing_check_and_selection_are_untouched_by_the_pause()
    {
        var client = Code(Source("X20Ctl.Product", "NativeBleClient.cs"));
        Assert.Contains("if (!check && BluetoothManager.PauseBackground)", client); // the controller check is never paused
        Assert.Contains("bool nameMatch = BleDiscovery.MatchesTarget(targetModel, name);", client);
        Assert.Contains("BleDiscovery.Refusal(check, vibrate != null, opcode)", client);
        var view = Code(Source("X20Ctl.Product", "Scanner", "WriteScanView.Bluetooth.cs"));
        Assert.Contains("BluetoothManager.PauseBackground = false", view); // resumed when the manager closes
    }

    [Fact] // 20
    public void The_manager_cannot_reach_a_controller_command_or_a_write()
    {
        string[] forbidden = ["X20Codec", "SendAsync", "SendQuery", "WriteValue", "WriteClientCharacteristic", "ReadValue", "PadBridge", "VibrationTest", "LightingBleClient", "NativeBleClient.QueryAsync", "PadCheck", "RemoveDevice"];
        foreach (var file in new[] { new[] { "X20Ctl.Product", "WinBluetooth.cs" }, ["X20Ctl.Product", "BluetoothManager.cs"], ["X20Ctl.Product", "Scanner", "WriteScanView.Bluetooth.cs"] })
        {
            var code = Code(Source(file));
            foreach (var token in forbidden) Assert.DoesNotContain(token, code);
        }
        // unpairing exists once, and is reached only from the guided re-pair, never from Disconnect
        var win = Code(Source("X20Ctl.Product", "WinBluetooth.cs"));
        Assert.Single(Regex.Matches(win, @"UnpairAsync\("));
        var view = Code(Source("X20Ctl.Product", "Scanner", "WriteScanView.Bluetooth.cs"));
        Assert.Single(Regex.Matches(view, @"RemovePairingAsync\("));
        Assert.Contains("RemovePairingAsync(", Between(view, "Task BtRepair(", "Task BtAfterPair("));
        Assert.DoesNotContain("Unpair", Between(Code(Source("X20Ctl.Product", "BluetoothManager.cs")), "class ConfigSession", "public static string Describe(LinkState"));
        // the configuration link probes services by UUID only
        Assert.Contains("GetGattServicesForUuidAsync(ConfigService, BluetoothCacheMode.Uncached)", Code(Source("X20Ctl.Product", "WinBluetooth.cs")));
    }

    static string Source(params string[] parts)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            var path = Path.Combine([dir.FullName, .. parts]);
            if (File.Exists(path)) return File.ReadAllText(path);
        }
        throw new FileNotFoundException(string.Join("/", parts));
    }
    [Fact] // 9, 17
    public void Paired_without_a_gamepad_driver_is_named_and_nothing_else_is()
    {
        // what Windows showed on 7 Nov 2026: paired, no services read, no HID device, link gone
        Assert.NotNull(Diagnose(Win("w", "Xbox Wireless Controller", paired: true, connected: false) with { GamepadDriver = false }));
        Assert.Null(Diagnose(Win("w", "Pad", paired: true, connected: false) with { GamepadDriver = true }));
        Assert.Null(Diagnose(Win("w", "Pad", paired: true) with { GamepadDriver = null })); // not read: nothing claimed
        Assert.Null(Diagnose(Win("w", "Pad", paired: false) with { GamepadDriver = false }));
        Assert.True(UnpairSucceeded("Unpaired")); Assert.False(UnpairSucceeded("AccessDenied"));
        Assert.Equal("Couldn't remove the pairing (Weird).", UnpairOutcome("Weird"));
        Assert.True(InVendorRange("98B6EA000001")); Assert.True(InVendorRange("98b6ec000001"));
        Assert.False(InVendorRange("98B6ED000001")); Assert.False(InVendorRange("5C0000ABCDEF"));
    }

    [Fact]
    public void Re_pair_asks_before_it_removes_anything_and_nothing_forces_a_service()
    {
        var view = Code(Source("X20Ctl.Product", "Scanner", "WriteScanView.Bluetooth.cs"));
        // the first button only opens the confirmation; BtRepair (which removes the pairing) is called from the confirm button alone
        Assert.Single(Regex.Matches(view, @"await BtRepair\("));
        Assert.Contains("btConfirmRepair = null; await BtRepair(", view);
        Assert.Contains("repair.Click += (_, _) => { btConfirmRepair = row.Key; BtRender(); }", view);
        Assert.Contains("Cancel", view);
        Assert.Contains("link key", RepairWarning("Pad"));
        Assert.Contains("Nothing on the controller is changed", RepairWarning("Pad"));
        // the old "Turn on gamepad service" control and its Windows call are gone
        foreach (var file in new[] { new[] { "X20Ctl.Product", "WinBluetooth.cs" }, ["X20Ctl.Product", "Scanner", "WriteScanView.Bluetooth.cs"] })
        {
            var code = Code(Source(file));
            Assert.DoesNotContain("BluetoothSetServiceState", code);
            Assert.DoesNotContain("EnableGamepadService", code);
            Assert.DoesNotContain("gamepad service", code);
        }
        // the diagnosis says what Windows shows, not why
        var said = Diagnose(Win("w", "Xbox Wireless Controller", paired: true, connected: false) with { GamepadDriver = false })!;
        Assert.DoesNotContain("no gamepad service", said);
        Assert.Contains("gamepad device", said);
    }

    static string Between(string text, string start, string end)
    {
        int a = text.IndexOf(start, StringComparison.Ordinal), b = text.IndexOf(end, a, StringComparison.Ordinal);
        Assert.True(a >= 0 && b > a, $"{start} … {end}");
        return text[a..b];
    }

    static string Code(string source) => Regex.Replace(Regex.Replace(source, @"//[^\n]*", ""), @"/\*.*?\*/", "", RegexOptions.Singleline);
}
