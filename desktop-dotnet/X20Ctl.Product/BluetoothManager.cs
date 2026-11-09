using System.Text;
using System.Text.RegularExpressions;

namespace X20Ctl.Product;

/// <summary>
/// The Bluetooth manager's logic, kept free of Windows types so it runs in the tests (WinBluetooth is the Windows side).
/// Two independent things are tracked and never merged: the configuration link (a GATT connection to the controller's
/// KeyLinker peripheral, which X20CTL owns) and the gameplay device (a Windows-paired Bluetooth HID device, which Windows
/// owns). Nothing here sends a controller command: connecting checks only that the configuration service is reachable.
/// </summary>
public static class BluetoothManager
{
    /// <summary>While the manager is open the background battery reads stand down, so X20CTL never opens a second
    /// connection to the pad underneath one the player is making.</summary>
    public static volatile bool PauseBackground;

    // ================= gameplay devices (Windows-paired Bluetooth HID), observed, never driven =================

    /// <summary>One Bluetooth device as Windows' device list reports it. Id is Windows' opaque id; it is never shown.</summary>
    /// <summary>GamepadDriver: whether Windows has a HID (gamepad) device under this pairing; null when not read.</summary>
    public sealed record ClassicDevice(string Id, string Name, bool Paired, bool? Connected, bool CanPair, uint? CodMajor, bool VendorPrefix, int? Rssi, bool? GamepadDriver = null);

    /// <summary>EasySMX ranges seen so far: 98:B6:EC in KeyLinker's code, 98:B6:EA on the owner's X20 Pro gameplay side
    /// (7 Nov 2026). A range says nothing about which controller it is.</summary>
    public static bool InVendorRange(string hexAddress) => hexAddress.Length >= 6 && hexAddress[..6].ToUpperInvariant() is "98B6EA" or "98B6EC";

    /// <summary>The state seen on 7 Nov 2026: paired, but Windows has no gamepad (HID) device for it, so no gamepad
    /// driver runs. It says only what Windows shows; the cause is not known from this alone.</summary>
    public static string? Diagnose(ClassicDevice d) => d.Paired && d.GamepadDriver == false
        ? "Paired, but Windows hasn't set up a gamepad device for it, so no gamepad driver is running."
        : null;

    /// <summary>The confirmation shown before Re-pair removes anything. Removing a pairing deletes Windows' link key.</summary>
    public static string RepairWarning(string name) =>
        $"Re-pair removes Windows' pairing for {name}: Windows forgets its link key and the controller has to be paired again. " +
        "Nothing on the controller is changed. Put the controller in pairing mode before you continue.";

    public static bool UnpairSucceeded(string status) => status is "Unpaired" or "AlreadyUnpaired";

    public static string UnpairOutcome(string status) => status switch
    {
        "Unpaired" => "Pairing removed.",
        "AlreadyUnpaired" => "It wasn't paired.",
        "OperationAlreadyInProgress" => "Windows is busy with another pairing.",
        "AccessDenied" => "Windows refused to remove it.",
        _ => $"Couldn't remove the pairing ({status}).",
    };

    static readonly Regex ControllerName = new(@"(?i)controller|gamepad|joy|xbox|easysmx|\bx20|xpert|\bpad\b|\bdune\b");

    /// <summary>Only controller-like entries are listed, so nearby phones, headphones and other people's devices stay out of the list and the log.</summary>
    public static bool IsControllerLike(ClassicDevice d) => d.CodMajor == 5 || ControllerName.IsMatch(d.Name);

    public static string Gameplay(ClassicDevice d) =>
        !d.Paired ? (d.CanPair ? "Not paired. Windows can pair it." : "Not paired.")
        : d.Connected switch { true => "Paired. Windows reports it connected.", false => "Paired. Not connected now.", _ => "Paired. Windows didn't say whether it is connected." };

    /// <summary>XInput slots in use are reported on their own: nothing ties a slot to a particular Bluetooth entry.</summary>
    public static string XInput(int slots) => slots switch
    {
        0 => "No XInput controller is active on this PC.",
        1 => "1 XInput controller is active on this PC. Which device it is isn't known.",
        _ => $"{slots} XInput controllers are active on this PC. Which devices they are isn't known.",
    };

    public static bool PairSucceeded(string status) => status is "Paired" or "AlreadyPaired";

    /// <summary>Windows' DevicePairingResultStatus, in words. Unknown values keep their name rather than being guessed at.</summary>
    public static string PairOutcome(string status) => status switch
    {
        "Paired" => "Paired by Windows.",
        "AlreadyPaired" => "Already paired.",
        "NotReadyToPair" => "The controller isn't in pairing mode.",
        "NotPaired" => "Not paired.",
        "ConnectionRejected" => "The controller refused the connection.",
        "TooManyConnections" => "The controller has too many connections.",
        "HardwareFailure" => "The Bluetooth adapter reported a hardware failure.",
        "AuthenticationTimeout" => "Pairing timed out.",
        "AuthenticationNotAllowed" => "The controller doesn't allow this kind of pairing.",
        "AuthenticationFailure" => "Pairing failed authentication.",
        "NoSupportedProfiles" => "Windows has no driver profile for this device.",
        "ProtectionLevelCouldNotBeMet" => "The required security level couldn't be met.",
        "AccessDenied" => "Windows refused, or the pairing prompt was declined.",
        "InvalidCeremonyData" => "Windows reported invalid pairing data.",
        "PairingCanceled" => "Pairing was cancelled.",
        "OperationAlreadyInProgress" => "Windows is already pairing a device.",
        "RequiredHandlerNotRegistered" or "RejectedByHandler" => "Windows needs its own pairing window for this device.",
        "RemoteDeviceHasAssociation" => "The controller is paired with another host.",
        "NotPairable" => "Windows says this device can't be paired from here.",
        _ => $"Pairing failed ({status}).",
    };

    // ================= the list =================

    public enum RowKind { ConfigPeripheral, BleController, Gameplay }

    /// <summary>One card. BLE entries are keyed by advertised address, Windows entries by Windows' id: the two are never
    /// joined, because nothing a scan sees proves two radios belong to one physical controller.</summary>
    public sealed record Row(string Key, RowKind Kind, string Name, string NameSource, string Transport, int? Rssi, BleDiscovery.Seen? Le, ClassicDevice? Classic);

    public static IReadOnlyList<Row> Rows(IEnumerable<BleDiscovery.Seen> le, IEnumerable<ClassicDevice> classic)
    {
        var rows = new List<Row>();
        foreach (var s in le)
        {
            if (s.Listing == BleDiscovery.Listing.Ignored) continue;
            var kind = s.Listing == BleDiscovery.Listing.GenericController ? RowKind.BleController : RowKind.ConfigPeripheral;
            rows.Add(new("le:" + s.Address.ToString("X12"), kind, string.IsNullOrWhiteSpace(s.Name) ? "Unnamed device" : s.Name!, string.IsNullOrWhiteSpace(s.Name) ? "none advertised" : "advertised by the device", "Bluetooth LE", s.Rssi, s, null));
        }
        foreach (var c in classic.Where(IsControllerLike).GroupBy(c => c.Id).Select(g => g.Last()))
            rows.Add(new("win:" + c.Id, RowKind.Gameplay, string.IsNullOrWhiteSpace(c.Name) ? "Unnamed device" : c.Name, "Windows device name", "Bluetooth (Windows device)", c.Rssi, null, c));
        return rows.OrderBy(r => r.Kind).ThenByDescending(r => r.Rssi ?? short.MinValue).ToList();
    }
}

/// <summary>The manager's diagnostic log: timestamped, capped, and scrubbed of anything shaped like a Bluetooth address.</summary>
public sealed class BluetoothLog(Func<DateTimeOffset>? clock = null)
{
    public sealed record Entry(DateTimeOffset At, string Text, string? Code);
    public const int Cap = 400;
    static readonly Regex Mac = new(@"(?i)\b(?:[0-9a-f]{2}[:\-]){5}[0-9a-f]{2}\b"), Bare = new(@"(?i)\b[0-9a-f]{12}\b");
    readonly List<Entry> entries = [];
    readonly object gate = new();
    readonly Func<DateTimeOffset> now = clock ?? (() => DateTimeOffset.Now);
    public event Action? Added;

    public static string Scrub(string text) => Bare.Replace(Mac.Replace(text, "··:··:··:··:··:··"), "············");
    public static string Code(Exception ex) => $"0x{ex.HResult:X8}";

    public void Add(string text, string? code = null)
    {
        lock (gate) { if (entries.Count >= Cap) entries.RemoveAt(0); entries.Add(new(now(), Scrub(text), code)); }
        Added?.Invoke();
    }
    public IReadOnlyList<Entry> Entries { get { lock (gate) return entries.ToArray(); } }

    public static string Line(Entry e) => $"{e.At:HH:mm:ss.fff}  {e.Text}" + (e.Code is null ? "" : $"  [{e.Code}]");

    /// <summary>The report the player saves by hand. Built only from the log, which never held an address.</summary>
    public string Export(string header)
    {
        var sb = new StringBuilder().AppendLine(Scrub(header)).AppendLine();
        foreach (var e in Entries) sb.AppendLine(Line(e));
        return sb.ToString();
    }
}

public enum LinkState { Idle, Connecting, Ready, NoService, Disconnecting, Disconnected, Failed, Reconnecting }
public enum LinkCause { None, YouDisconnected, LinkLost, Unreachable, NoService, Busy, Timeout, ApiError, Cancelled, GaveUp }

/// <summary>Reachability of the configuration service on an opened device. Reached false = GATT unreachable.</summary>
public sealed record ServiceProbe(bool Reached, bool HasConfigService, string? Code = null);

/// <summary>One opened BLE device, as WinBluetooth holds it. Disposing it is the only way X20CTL lets go.</summary>
public interface IConfigLink : IAsyncDisposable
{
    /// <summary>Windows' own connection status changes: true up, false down. Windows gives no reason for a drop.</summary>
    event Action<bool>? LinkChanged;
    event Action<string>? SessionChanged;
    bool IsUp { get; }
    Task<ServiceProbe> ProbeAsync(CancellationToken token);
}

public interface IConfigLinkFactory
{
    /// <summary>Null when Windows can't resolve the address (off, out of range, not advertising).</summary>
    Task<IConfigLink?> OpenAsync(ulong address, CancellationToken token);
}

/// <summary>
/// The one owner of X20CTL's configuration link. Every change goes through one queue (ops); opening also takes the shared
/// configuration gate, so no other X20CTL Bluetooth operation runs in the middle. A link that drops is reconnected only
/// when the player turned that on, only for a device that reached the configuration service before, and only a few
/// times with growing waits. Disconnect cancels all of that and releases the device; it never unpairs anything.
/// </summary>
public sealed class ConfigSession(IConfigLinkFactory factory, SemaphoreSlim shared, BluetoothLog log, Func<TimeSpan, CancellationToken, Task>? delay = null, TimeSpan[]? backoff = null, TimeSpan? openTimeout = null, TimeSpan? busyWait = null)
{
    public static readonly TimeSpan[] DefaultBackoff = [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15)];
    readonly Func<TimeSpan, CancellationToken, Task> wait = delay ?? Task.Delay;
    readonly TimeSpan[] steps = backoff ?? DefaultBackoff;
    readonly TimeSpan openLimit = openTimeout ?? TimeSpan.FromSeconds(12), busyLimit = busyWait ?? TimeSpan.FromSeconds(15);
    readonly SemaphoreSlim ops = new(1, 1);
    IConfigLink? link;
    int generation;
    CancellationTokenSource? reconnect;
    bool verified;
    Task? reconnecting;

    public ulong? Target { get; private set; }
    public string Label { get; private set; } = "";
    public LinkState State { get; private set; }
    public LinkCause Cause { get; private set; }
    public string? Detail { get; private set; }
    public bool AutoReconnect { get; set; }
    public int Attempt { get; private set; }
    public bool Held => link != null;
    /// <summary>The background reconnect, when one is running (tests wait on it).</summary>
    public Task Reconnecting => reconnecting ?? Task.CompletedTask;
    public event Action? Changed;

    public async Task<bool> ConnectAsync(ulong address, string label, CancellationToken token = default)
    {
        CancelReconnect();
        await ops.WaitAsync(token);
        try
        {
            if (link != null) { log.Add("Released the previous connection first."); await ReleaseAsync(); }
            if (Target != address) verified = false;
            Target = address; Label = label; Attempt = 0;
            return await OpenAsync(token, false);
        }
        finally { ops.Release(); }
    }

    /// <summary>The player's Disconnect. Only X20CTL's own connection is released; Windows pairing is untouched.</summary>
    public async Task DisconnectAsync(string why = "You disconnected.")
    {
        CancelReconnect();
        await ops.WaitAsync();
        try
        {
            if (link == null)
            {
                if (State == LinkState.Idle) return;
                Set(LinkState.Disconnected, LinkCause.YouDisconnected, "Not connected. Nothing to release.");
                log.Add(why + " Nothing was held; any reconnect was stopped.");
                return;
            }
            Set(LinkState.Disconnecting, LinkCause.None, null);
            await ReleaseAsync();
            Set(LinkState.Disconnected, LinkCause.YouDisconnected, "Released. Windows pairing is unchanged.");
            log.Add(why + " X20CTL released its connection; nothing was unpaired.");
        }
        finally { ops.Release(); }
    }

    async Task<bool> OpenAsync(CancellationToken token, bool again)
    {
        Set(again ? LinkState.Reconnecting : LinkState.Connecting, LinkCause.None, again ? $"Try {Attempt} of {steps.Length}." : null);
        log.Add((again ? $"Reconnect try {Attempt} of {steps.Length}: " : "Connecting: ") + Label);
        bool gated;
        try
        {
            gated = await shared.WaitAsync(0, token);
            if (!gated) { log.Add("Waiting for another X20CTL Bluetooth operation to finish."); gated = await shared.WaitAsync(busyLimit, token); }
        }
        catch (OperationCanceledException) { Set(LinkState.Disconnected, LinkCause.Cancelled, "Cancelled."); log.Add("Cancelled before connecting."); return false; }
        if (!gated) return Fail(LinkCause.Busy, "Another X20CTL Bluetooth operation is running. Try again when it ends.");
        IConfigLink? opened = null;
        try
        {
            using var limit = CancellationTokenSource.CreateLinkedTokenSource(token);
            limit.CancelAfter(openLimit);
            try { opened = await factory.OpenAsync(Target!.Value, limit.Token); }
            catch (OperationCanceledException) when (!token.IsCancellationRequested) { return Fail(LinkCause.Timeout, "Windows didn't open the device in time."); }
            if (opened == null) return Fail(LinkCause.Unreachable, "Windows couldn't reach the device. It may be off, out of range, or connected elsewhere.");
            log.Add("Windows opened the device. Listing its services.");
            ServiceProbe probe;
            try { probe = await opened.ProbeAsync(limit.Token); }
            catch (OperationCanceledException) when (!token.IsCancellationRequested) { return Fail(LinkCause.Timeout, "The device didn't answer service discovery in time."); }
            if (!probe.Reached) return Fail(LinkCause.Unreachable, "GATT unreachable: the device didn't answer.", probe.Code);
            if (!probe.HasConfigService)
            {
                Set(LinkState.NoService, LinkCause.NoService, "Reached, but it has no configuration service.");
                log.Add("Reached the device; the configuration service is absent. Released.", probe.Code);
                return false;
            }
            int gen = ++generation;
            link = opened; opened = null;
            link.LinkChanged += up => OnLinkChanged(gen, up);
            link.SessionChanged += s => { if (gen == generation) log.Add("GATT session: " + s); };
            verified = true; Attempt = 0;
            Set(LinkState.Ready, LinkCause.None, "Configuration service reachable. Nothing was sent to the controller.");
            log.Add("Connected. Configuration service reachable.");
            if (!link.IsUp) OnLinkChanged(gen, false);
            return true;
        }
        catch (OperationCanceledException) { Set(LinkState.Disconnected, LinkCause.Cancelled, "Cancelled."); log.Add("Cancelled while connecting."); return false; }
        catch (Exception ex) { return Fail(LinkCause.ApiError, "Windows Bluetooth error.", BluetoothLog.Code(ex)); }
        finally
        {
            if (opened != null) await Drop(opened);
            shared.Release();
        }
    }

    void OnLinkChanged(int gen, bool up)
    {
        if (gen != generation) return; // a released link's late event
        if (up)
        {
            log.Add("Windows reports the link up.");
            if (State is LinkState.Disconnected && Cause == LinkCause.LinkLost && link != null) Set(LinkState.Ready, LinkCause.None, "The link came back.");
            return;
        }
        log.Add("Windows reports the link closed. Windows gave no reason.");
        if (State != LinkState.Ready) return;
        Set(LinkState.Disconnected, LinkCause.LinkLost, "The link dropped. Windows gave no reason.");
        if (AutoReconnect && verified) StartReconnect();
        else _ = ReleaseLostAsync(gen);
    }

    async Task ReleaseLostAsync(int gen)
    {
        await ops.WaitAsync();
        try { if (gen == generation && link != null) { await ReleaseAsync(); log.Add("Released the dropped connection. Press Connect to try again."); } }
        finally { ops.Release(); }
    }

    void StartReconnect()
    {
        CancelReconnect();
        var cts = reconnect = new CancellationTokenSource();
        reconnecting = Task.Run(() => ReconnectLoop(cts.Token));
    }

    async Task ReconnectLoop(CancellationToken token)
    {
        for (int i = 0; i < steps.Length; i++)
        {
            try { await wait(steps[i], token); await ops.WaitAsync(token); }
            catch (OperationCanceledException) { return; }
            try
            {
                if (token.IsCancellationRequested || State == LinkState.Ready) return;
                if (link != null) await ReleaseAsync();
                Attempt = i + 1;
                if (await OpenAsync(token, true)) return;
                if (Cause is LinkCause.NoService or LinkCause.Cancelled) return; // a different device now, or stopped: no more tries
            }
            finally { ops.Release(); }
        }
        if (!token.IsCancellationRequested) Fail(LinkCause.GaveUp, $"Gave up after {steps.Length} tries. Press Connect to try again.");
    }

    void CancelReconnect() { var r = reconnect; reconnect = null; try { r?.Cancel(); } catch (ObjectDisposedException) { } }

    async Task ReleaseAsync()
    {
        generation++;
        var l = link; link = null;
        if (l != null) await Drop(l);
    }

    async Task Drop(IConfigLink l)
    {
        try { await l.DisposeAsync(); }
        catch (Exception ex) { log.Add("Cleanup error while releasing the device.", BluetoothLog.Code(ex)); }
    }

    bool Fail(LinkCause cause, string text, string? code = null)
    {
        Set(LinkState.Failed, cause, text);
        log.Add(text, code);
        return false;
    }

    void Set(LinkState state, LinkCause cause, string? detail)
    {
        State = state; Cause = cause; Detail = detail;
        Changed?.Invoke();
    }

    /// <summary>The state in words for the card. Ready never says more than that the service is reachable.</summary>
    public static string Describe(LinkState state) => state switch
    {
        LinkState.Idle => "Not connected.",
        LinkState.Connecting => "Connecting…",
        LinkState.Ready => "Connected. Configuration service reachable.",
        LinkState.NoService => "Reached, but no configuration service.",
        LinkState.Disconnecting => "Disconnecting…",
        LinkState.Disconnected => "Disconnected.",
        LinkState.Failed => "Connection failed.",
        LinkState.Reconnecting => "Reconnecting…",
        _ => state.ToString(),
    };
}
