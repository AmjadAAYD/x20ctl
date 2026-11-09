using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
namespace X20Ctl.Product.Scanner;

/// <summary>
/// The Bluetooth manager, reached from Tools and the connect screen: scan, pick a controller, connect X20CTL's configuration link,
/// pair a gameplay device through Windows, and read what happened. The configuration link and the gameplay device are
/// shown apart and never merged. Leaving the manager releases X20CTL's connection; nothing is ever unpaired.
/// </summary>
public sealed partial class WriteScanView
{
    private readonly Border bluetoothView = new() { Visibility = Visibility.Collapsed };
    private readonly BluetoothLog btLog = new();
    private ConfigSession? btSession;
    private readonly TextBlock btAdapter = Wrap("", 15, "DS.TextSoft"), btStatus = Wrap("", 14.5, "DS.TextSoft");
    private readonly StackPanel btList = new(), btDetails = new();
    private readonly TextBox btLogText = new() { IsReadOnly = true, FontFamily = new FontFamily("Consolas"), FontSize = 12.5, TextWrapping = TextWrapping.Wrap, Padding = new(14), BorderThickness = new(0), Height = 190, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = new SolidColorBrush(Color.FromArgb(70, 0, 0, 0)), Foreground = DS.Brush("DS.Text") };
    private readonly Button btScan = Action("", "Scan", "PrimaryButton");
    private readonly CheckBox btAuto = new() { Style = DS.Style("DS.Toggle"), VerticalAlignment = VerticalAlignment.Center };
    private readonly DispatcherTimer btWatch = new() { Interval = TimeSpan.FromSeconds(3) };
    private IReadOnlyList<BleDiscovery.Seen> btLe = [];
    private IReadOnlyList<BluetoothManager.ClassicDevice> btClassic = [];
    private readonly Dictionary<ulong, BleDiscovery.GattListing> btServices = [];
    private readonly Dictionary<string, string> btLastError = [];
    private string? btSelected;
    private string? btConfirmRepair; // the row whose Re-pair is waiting for confirmation
    private bool btBusy;
    private int btSlots = -1;
    private static readonly TimeSpan BtListen = TimeSpan.FromSeconds(8), BtInquiry = TimeSpan.FromSeconds(10);
    private bool bluetoothOnly; // opened straight from Tools or the connect screen: Back closes the whole view
    private readonly byte[] nearbySalt = System.Security.Cryptography.RandomNumberGenerator.GetBytes(16); // per view: addresses are shown redacted

    /// <summary>The manager on its own, for the connect screen's Bluetooth card.</summary>
    public static WriteScanView BluetoothManagerOnly(Action close)
    {
        var view = new WriteScanView(close) { bluetoothOnly = true };
        view.Loaded += async (_, _) => { if (view.bluetoothView.Visibility != Visibility.Visible) await view.OpenBluetooth(); };
        return view;
    }

    private void BuildBluetooth()
    {
        bluetoothView.Background = new SolidColorBrush(Color.FromArgb(250, 5, 9, 20));
        btScan.Click += async (_, _) => await BtScan();
        var settings = Action("", "Windows Bluetooth settings", "ActionButton"); settings.Click += (_, _) => { btLog.Add("Opened Windows Bluetooth settings."); WinBluetooth.OpenSettings(); };
        var back = Action("", "Back", "ActionButton"); back.Click += async (_, _) => await CloseBluetooth();
        var save = Action("", "Save diagnostics", "ActionButton"); save.Click += (_, _) => BtSave();
        btAuto.Checked += (_, _) => { if (btSession != null) btSession.AutoReconnect = true; btLog.Add("Reconnect automatically: on (up to 3 tries)."); };
        btAuto.Unchecked += (_, _) => { if (btSession != null) btSession.AutoReconnect = false; btLog.Add("Reconnect automatically: off."); };
        btLog.Added += () => Dispatcher.BeginInvoke(BtRenderLog);
        btWatch.Tick += async (_, _) => await BtWatchTick();

        var columns = new Grid { Margin = new(0, 20, 0, 0) };
        columns.ColumnDefinitions.Add(new() { Width = new(5, GridUnitType.Star) }); columns.ColumnDefinitions.Add(new() { Width = new(24) }); columns.ColumnDefinitions.Add(new() { Width = new(6, GridUnitType.Star) });
        columns.Children.Add(new StackPanel { Children = { Kit.Text("Nearby controllers", 20, "DS.Text", FontWeights.Bold).Also(t => t.Margin = new(0, 0, 0, 12)), btList } });
        var detailCard = new Border { CornerRadius = new(14), Padding = new(22, 18, 22, 20), BorderThickness = new(1), BorderBrush = new SolidColorBrush(ThemeCatalog.R(Color.FromArgb(46, 140, 180, 255))), Background = new SolidColorBrush(ThemeCatalog.R(Color.FromArgb(120, 14, 24, 50))), Child = btDetails };
        Grid.SetColumn(detailCard, 2); columns.Children.Add(detailCard);

        var panel = new StackPanel { MaxWidth = 1180, HorizontalAlignment = HorizontalAlignment.Center, Margin = new(40, 36, 40, 30), Children = {
            Kit.Text("Bluetooth", 40, "DS.Text", FontWeights.Bold),
            Wrap("Find a controller, connect, and see what Windows reports. Settings and gameplay use separate Bluetooth links.", 17, "DS.TextSoft").Also(t => t.Margin = new(0, 8, 0, 0)),
            btAdapter.Also(t => t.Margin = new(0, 14, 0, 0)),
            new WrapPanel { Margin = new(0, 16, 0, 0), Children = { btScan, settings.Also(b => b.Margin = new(12, 0, 0, 0)), back.Also(b => b.Margin = new(12, 0, 0, 0)) } },
            btStatus.Also(t => t.Margin = new(0, 14, 0, 0)),
            columns,
            new DockPanel { Margin = new(0, 26, 0, 8), Children = { save.Also(b => { DockPanel.SetDock(b, Dock.Right); b.Height = 38; }), Kit.Text("Diagnostics", 20, "DS.Text", FontWeights.Bold).Also(t => t.VerticalAlignment = VerticalAlignment.Center) } },
            btLogText,
            Wrap("Addresses never appear here. Saved only when you press Save diagnostics.", 13, "DS.TextMuted").Also(t => t.Margin = new(0, 8, 0, 0)) } };
        bluetoothView.Child = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Unloaded += async (_, _) => await CloseBluetooth(leaveView: false);
    }

    private async Task OpenBluetooth()
    {
        BluetoothManager.PauseBackground = true;
        btSession ??= new ConfigSession(new WinBluetooth.LinkFactory(), NativeBleClient.ConfigurationGate, btLog);
        btSession.AutoReconnect = btAuto.IsChecked == true;
        btSession.Changed += BtSessionChanged;
        bluetoothView.Visibility = Visibility.Visible;
        btLog.Add("Bluetooth manager opened. Timed battery reads paused.");
        var radio = await WinBluetooth.RadioAsync();
        btAdapter.Text = "Bluetooth adapter: " + WinBluetooth.Describe(radio);
        btLog.Add("Adapter: " + WinBluetooth.Describe(radio));
        BtRender();
        btWatch.Start();
        if (radio == WinBluetooth.Radio.On && btLe.Count == 0 && btClassic.Count == 0) await BtScan();
    }

    /// <summary>Leaving releases X20CTL's own connection, so no session outlives the screen that made it.</summary>
    private async Task CloseBluetooth(bool leaveView = true)
    {
        btWatch.Stop();
        if (btSession != null)
        {
            btSession.Changed -= BtSessionChanged;
            if (btSession.Held || btSession.State is LinkState.Connecting or LinkState.Reconnecting) await btSession.DisconnectAsync("Left the Bluetooth manager.");
        }
        if (BluetoothManager.PauseBackground) { BluetoothManager.PauseBackground = false; btLog.Add("Timed battery reads resumed."); }
        if (leaveView && bluetoothOnly) { close(); return; }
        if (leaveView) bluetoothView.Visibility = Visibility.Collapsed;
    }

    private void BtSessionChanged() => Dispatcher.BeginInvoke(() => { BtRenderList(); BtRenderDetails(); });

    private async Task BtScan()
    {
        if (btBusy) return;
        btBusy = true; btScan.IsEnabled = false;
        btStatus.Text = "Scanning… Nothing is sent to the devices.";
        btLog.Add($"Scan started: listening {BtListen.TotalSeconds:0} s, Windows device search {BtInquiry.TotalSeconds:0} s.");
        try
        {
            var le = Task.Run(() => NativeBleDiscovery.ListenAsync(BtListen));
            var classic = Task.Run(() => WinBluetooth.ClassicAsync(BtInquiry, btLog));
            await Task.WhenAll(le, classic);
            var gone = btLe.Where(o => le.Result.All(n => n.Address != o.Address)).ToList();
            btLe = le.Result; btClassic = classic.Result;
            var rows = BluetoothManager.Rows(btLe, btClassic);
            foreach (var g in gone.Where(g => g.Listing != BleDiscovery.Listing.Ignored)) btLog.Add($"No longer advertising: {Label(g)}.");
            foreach (var r in rows) btLog.Add($"Found: {r.Name} · {r.Transport}" + (r.Rssi is { } s ? $" · {s} dBm" : "") + (r.Classic is { } c ? (c.Paired ? " · paired" : " · not paired") : ""));
            btStatus.Text = rows.Count == 0 ? "No controllers found. Wake the controller and scan again." : $"Found {rows.Count}.";
            btLog.Add($"Scan finished: {rows.Count} controller entries.");
            if (btSelected != null && rows.All(r => r.Key != btSelected) && btSession?.Held != true) btSelected = null;
            btSelected ??= rows.FirstOrDefault()?.Key;
        }
        catch (Exception ex) { btStatus.Text = $"Scan failed (0x{ex.HResult:X8}). Is Bluetooth on?"; btLog.Add("Scan failed.", BluetoothLog.Code(ex)); }
        finally { btBusy = false; btScan.IsEnabled = true; BtRender(); }
    }

    private string Label(BleDiscovery.Seen s) => (string.IsNullOrWhiteSpace(s.Name) ? "unnamed device" : s.Name) + " " + BleDiscovery.Redact(s.Address, s.Kind, nearbySalt);

    private IReadOnlyList<BluetoothManager.Row> BtRows() => BluetoothManager.Rows(btLe, btClassic);

    private void BtRender() { BtRenderList(); BtRenderDetails(); BtRenderLog(); }

    private void BtRenderLog()
    {
        var lines = btLog.Entries.TakeLast(120).Select(BluetoothLog.Line);
        btLogText.Text = string.Join(Environment.NewLine, lines);
        btLogText.ScrollToEnd();
    }

    private void BtRenderList()
    {
        btList.Children.Clear();
        var rows = BtRows();
        if (rows.Count == 0) { btList.Children.Add(Wrap(btBusy ? "Scanning…" : "Nothing yet. Press Scan.", 15, "DS.TextMuted")); return; }
        foreach (var r in rows)
        {
            bool selected = r.Key == btSelected;
            var card = new Button { Style = DS.Style("ActionButton"), HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new(18, 12, 18, 12), Margin = new(0, 0, 0, 10), MinHeight = 0, BorderThickness = new(selected ? 2 : 1),
                Content = new StackPanel { Children = {
                    Kit.Text(r.Name, 16.5, "DS.Text", FontWeights.SemiBold),
                    Wrap(Kind(r), 13.5, "DS.TextSoft").Also(t => t.Margin = new(0, 3, 0, 0)),
                    Wrap(RowStatus(r) + (r.Rssi is { } s ? $" · {s} dBm" : ""), 13.5, "DS.TextMuted").Also(t => t.Margin = new(0, 2, 0, 0)) } } };
            if (selected) card.BorderBrush = new SolidColorBrush(Kit.Blue);
            string key = r.Key;
            card.Click += (_, _) => { btSelected = key; btLog.Add("Selected: " + r.Name + "."); BtRender(); };
            btList.Children.Add(card);
        }
    }

    private static string Kind(BluetoothManager.Row r) => r.Kind switch
    {
        BluetoothManager.RowKind.ConfigPeripheral => "Configuration link candidate · Bluetooth LE",
        BluetoothManager.RowKind.BleController => "Bluetooth LE controller · no configuration service advertised",
        _ => "Gameplay device · Windows" + (r.Classic?.VendorPrefix == true ? " · EasySMX address range" : ""),
    };

    private string RowStatus(BluetoothManager.Row r)
    {
        if (r.Le is { } s && btSession is { } session && session.Target == s.Address && session.State != LinkState.Idle) return ConfigSession.Describe(session.State);
        if (r.Classic is { } c) return BluetoothManager.Gameplay(c);
        return "Discovered";
    }

    private void BtRenderDetails()
    {
        btDetails.Children.Clear();
        var row = BtRows().FirstOrDefault(r => r.Key == btSelected);
        if (row == null) { btDetails.Children.Add(Wrap("Pick a controller to see its details.", 15, "DS.TextMuted")); return; }
        btDetails.Children.Add(Kit.Text(row.Name, 24, "DS.Text", FontWeights.Bold));
        void Line(string label, string value) => btDetails.Children.Add(new DockPanel { Margin = new(0, 8, 0, 0), Children = {
            Kit.Text(label, 13.5, "DS.TextMuted", FontWeights.SemiBold).Also(t => { t.Width = 150; DockPanel.SetDock(t, Dock.Left); }), Wrap(value, 14.5, "DS.Text") } });
        Line("Name from", row.NameSource);
        Line("Transport", row.Transport);
        if (row.Rssi is { } rssi) Line("Signal", $"{rssi} dBm");
        var actions = new WrapPanel { Margin = new(0, 18, 0, 0) };
        if (row.Le is { } s)
        {
            Line("Address", BleDiscovery.Redact(s.Address, s.Kind, nearbySalt) + (s.AddressConflict ? " · Windows reports a random address in a public range" : ""));
            btServices.TryGetValue(s.Address, out var gatt);
            Line("Protocol", BleDiscovery.Describe(BleDiscovery.Assess(s.Listing, gatt, null)));
            if (gatt?.Services is { } list) Line("Services", string.Join(", ", list.Select(g => g == BleDiscovery.ConfigService ? "configuration" : g.ToString()[..8])));
            bool mine = btSession is { } session && session.Target == s.Address;
            Line("X20CTL link", mine ? ConfigSession.Describe(btSession!.State) + (btSession.Detail is { } d ? " " + d : "") : "Not connected.");
            Line("Gameplay", "Not this entry. Gameplay over Bluetooth is a separate Windows device.");
            if (btLastError.TryGetValue(row.Key, out var err)) Line("Last error", err);
            if (BleDiscovery.MayListServices(s))
            {
                bool held = mine && btSession!.State is LinkState.Ready or LinkState.Connecting or LinkState.Reconnecting;
                var connect = Action(held ? "" : "", held ? "Disconnect" : "Connect", held ? "ActionButton" : "PrimaryButton");
                connect.IsEnabled = !btBusy;
                connect.Click += async (_, _) => { if (held) await BtDisconnect(); else await BtConnect(row, s); };
                actions.Children.Add(connect);
                var inspect = Action("", "Device details", "ActionButton").Also(b => b.Margin = new(12, 0, 0, 0)); inspect.IsEnabled = !btBusy;
                inspect.Click += async (_, _) => await BtInspect(row, s);
                actions.Children.Add(inspect);
                btDetails.Children.Add(actions);
                btDetails.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 14, 0, 0), Children = { btAuto, Wrap("Reconnect automatically (up to 3 tries)", 14, "DS.Text").Also(t => { t.VerticalAlignment = VerticalAlignment.Center; t.Margin = new(10, 0, 0, 0); }) } });
                btDetails.Children.Add(Wrap("Connect checks only that the configuration service answers. Nothing is sent to the controller. While connected here, a phone can't connect to it.", 13, "DS.TextMuted").Also(t => t.Margin = new(0, 12, 0, 0)));
            }
            else
            {
                var pair = Action("", "Pair in Windows", "ActionButton"); pair.IsEnabled = !btBusy;
                pair.Click += async (_, _) => await BtPairLe(row, s);
                actions.Children.Add(pair); btDetails.Children.Add(actions);
                btDetails.Children.Add(Wrap("No configuration service. X20CTL doesn't talk to it; Windows handles it as a gamepad.", 13, "DS.TextMuted").Also(t => t.Margin = new(0, 12, 0, 0)));
            }
        }
        else if (row.Classic is { } c)
        {
            Line("Windows", BluetoothManager.Gameplay(c));
            Line("XInput", btSlots < 0 ? "Not read yet." : BluetoothManager.XInput(btSlots));
            Line("Protocol", "Not checked. X20CTL configures controllers only over their own Bluetooth LE link.");
            if (c.VendorPrefix) Line("Note", "In an EasySMX address range. That alone doesn't show which controller it is.");
            if (c.Paired && c.GamepadDriver is { } driver) Line("Gamepad driver", driver ? "Loaded by Windows." : "None. Windows has no gamepad device for this pairing.");
            if (btLastError.TryGetValue(row.Key, out var err)) Line("Last error", err);
            if (BluetoothManager.Diagnose(c) is { } problem)
            {
                Line("Problem", problem);
                if (btConfirmRepair == row.Key)
                {
                    btDetails.Children.Add(Wrap(BluetoothManager.RepairWarning(c.Name), 14, "DS.TextSoft").Also(t => t.Margin = new(0, 16, 0, 0)));
                    var go = Action("", "Remove and pair again", "PrimaryButton"); go.IsEnabled = !btBusy;
                    go.Click += async (_, _) => { btConfirmRepair = null; await BtRepair(row, c); };
                    var cancel = Action("", "Cancel", "ActionButton").Also(b => b.Margin = new(12, 0, 0, 0));
                    cancel.Click += (_, _) => { btConfirmRepair = null; btLog.Add("Re-pair cancelled. Nothing was removed."); BtRender(); };
                    actions.Children.Add(go); actions.Children.Add(cancel); btDetails.Children.Add(actions);
                }
                else
                {
                    var repair = Action("", "Re-pair it…", "PrimaryButton"); repair.IsEnabled = !btBusy;
                    repair.Click += (_, _) => { btConfirmRepair = row.Key; BtRender(); }; // asks first; removes nothing
                    actions.Children.Add(repair); btDetails.Children.Add(actions);
                }
            }
            else if (!c.Paired)
            {
                var pair = Action("", "Pair in Windows", "PrimaryButton"); pair.IsEnabled = !btBusy && c.CanPair;
                pair.Click += async (_, _) => await BtPair(row, c);
                actions.Children.Add(pair); btDetails.Children.Add(actions);
                btDetails.Children.Add(Wrap("Windows runs the pairing and shows any prompt itself. Put the controller in pairing mode first.", 13, "DS.TextMuted").Also(t => t.Margin = new(0, 12, 0, 0)));
            }
            else if (c.Connected != true)
                btDetails.Children.Add(Wrap("Paired. Press the controller's Home button: Windows reconnects paired controllers itself. This updates when it does.", 14, "DS.TextSoft").Also(t => t.Margin = new(0, 16, 0, 0)));
        }
    }

    private async Task BtConnect(BluetoothManager.Row row, BleDiscovery.Seen s)
    {
        if (btBusy || btSession == null) return;
        btBusy = true; btStatus.Text = "Connecting… Nothing is sent to the controller."; BtRender();
        try
        {
            var session = btSession; string label = Label(s);
            bool ok = await Task.Run(() => session.ConnectAsync(s.Address, label)); // off the screen's thread: Windows Bluetooth calls can block
            btStatus.Text = ok ? "Connected." : (btSession.Detail ?? "Connection failed.");
            if (!ok) btLastError[row.Key] = btSession.Detail ?? "Connection failed."; else btLastError.Remove(row.Key);
        }
        catch (Exception ex) { btStatus.Text = $"Connection failed (0x{ex.HResult:X8})."; btLog.Add("Connect failed.", BluetoothLog.Code(ex)); }
        finally { btBusy = false; BtRender(); }
    }

    private async Task BtDisconnect()
    {
        if (btSession == null) return;
        btBusy = true; BtRender();
        try { var session = btSession; await Task.Run(() => session.DisconnectAsync()); btStatus.Text = "Disconnected. Nothing was unpaired."; }
        finally { btBusy = false; BtRender(); }
    }

    private async Task BtInspect(BluetoothManager.Row row, BleDiscovery.Seen s)
    {
        if (btBusy) return;
        btBusy = true; btStatus.Text = "Listing services. Nothing is written."; BtRender();
        btLog.Add("Listing services: " + Label(s));
        try
        {
            var gatt = await Task.Run(() => NativeBleDiscovery.ListServicesAsync(s.Address, TimeSpan.FromSeconds(10)));
            btServices[s.Address] = gatt;
            if (gatt.Error is { } e) { btLastError[row.Key] = e; btLog.Add("Service listing: " + e); }
            else btLog.Add($"Service listing: {gatt.Services!.Count} services" + (gatt.HasConfigService ? ", configuration service present." : ", no configuration service."));
            btStatus.Text = "Done. Nothing was changed.";
        }
        catch (Exception ex) { btStatus.Text = $"Couldn't list services (0x{ex.HResult:X8})."; btLog.Add("Service listing failed.", BluetoothLog.Code(ex)); }
        finally { btBusy = false; BtRender(); }
    }

    private async Task BtPair(BluetoothManager.Row row, BluetoothManager.ClassicDevice c)
    {
        if (btBusy) return;
        btBusy = true; btStatus.Text = "Windows is pairing. Answer any Windows prompt."; BtRender();
        try
        {
            string status = await WinBluetooth.PairAsync(c.Id, btLog);
            string said = BluetoothManager.PairOutcome(status);
            btLog.Add("Windows pairing result: " + said, status);
            btStatus.Text = said;
            if (BluetoothManager.PairSucceeded(status)) { btLastError.Remove(row.Key); await BtAfterPair(c.Id); }
            else btLastError[row.Key] = said;
        }
        finally { btBusy = false; BtRender(); }
    }

    /// <summary>The guided re-pair, only from its own button: remove Windows' pairing, find the controller in pairing
    /// mode again, pair through Windows, then watch. Each step stops the flow if it fails.</summary>
    private async Task BtRepair(BluetoothManager.Row row, BluetoothManager.ClassicDevice c)
    {
        if (btBusy) return;
        btBusy = true; BtRender();
        try
        {
            btLog.Add($"Re-pair started: {c.Name}.");
            btStatus.Text = "Removing Windows' pairing…";
            string removed = await WinBluetooth.RemovePairingAsync(c.Id, btLog);
            btLog.Add("Remove pairing: " + BluetoothManager.UnpairOutcome(removed), removed);
            if (!BluetoothManager.UnpairSucceeded(removed)) { btStatus.Text = BluetoothManager.UnpairOutcome(removed); btLastError[row.Key] = btStatus.Text; return; }

            btStatus.Text = "Looking for the controller in pairing mode…";
            BluetoothManager.ClassicDevice? again = null;
            for (int i = 0; i < 3 && again == null; i++)
            {
                var found = await Task.Run(() => WinBluetooth.ClassicAsync(BtInquiry, btLog));
                again = found.FirstOrDefault(d => d.Id == c.Id && !d.Paired) ?? found.FirstOrDefault(d => !d.Paired && d.Name == c.Name && d.VendorPrefix == c.VendorPrefix);
                btClassic = btClassic.Where(d => found.All(f => f.Id != d.Id)).Concat(found).ToList();
                if (again == null) btLog.Add($"Not found in pairing mode yet (search {i + 1} of 3).");
            }
            if (again == null) { btStatus.Text = "Couldn't find it in pairing mode. Put it in pairing mode and press Scan, then Pair in Windows."; btLastError[row.Key] = btStatus.Text; return; }
            btSelected = "win:" + again.Id;

            btStatus.Text = "Windows is pairing. Answer any Windows prompt.";
            string status = await WinBluetooth.PairAsync(again.Id, btLog);
            string said = BluetoothManager.PairOutcome(status);
            btLog.Add("Windows pairing result: " + said, status);
            btStatus.Text = said;
            if (BluetoothManager.PairSucceeded(status)) await BtAfterPair(again.Id);
            else btLastError["win:" + again.Id] = said;
        }
        finally { btBusy = false; BtRender(); }
    }

    /// <summary>After Windows says paired: watch for 20 seconds whether it connects, gets a gamepad driver, and stays.</summary>
    private async Task BtAfterPair(string id)
    {
        btStatus.Text = "Paired. Watching the connection for 20 seconds…";
        for (int s = 0; s < 20; s++)
        {
            await BtRefreshClassic(id);
            int slots = WinBluetooth.XInputSlots();
            if (slots != btSlots) { btLog.Add(BluetoothManager.XInput(slots)); btSlots = slots; }
            BtRenderList(); BtRenderDetails();
            await Task.Delay(1000);
        }
        var d = btClassic.FirstOrDefault(x => x.Id == id);
        string verdict = d == null ? "Windows no longer lists it."
            : BluetoothManager.Diagnose(d) ?? (d.Connected == true && d.GamepadDriver == true ? "Connected, with a gamepad driver." : BluetoothManager.Gameplay(d));
        btLog.Add("After pairing: " + verdict);
        btStatus.Text = verdict;
    }

    private async Task BtPairLe(BluetoothManager.Row row, BleDiscovery.Seen s)
    {
        if (btBusy) return;
        btBusy = true; btStatus.Text = "Windows is pairing. Answer any Windows prompt."; BtRender();
        try
        {
            string status = await WinBluetooth.PairLeAsync(s.Address, btLog);
            string said = BluetoothManager.PairOutcome(status);
            btLog.Add("Windows pairing result: " + said, status);
            btStatus.Text = said;
            if (!BluetoothManager.PairSucceeded(status)) btLastError[row.Key] = said; else btLastError.Remove(row.Key);
        }
        finally { btBusy = false; BtRender(); }
    }

    private async Task BtRefreshClassic(string id)
    {
        var now = await WinBluetooth.RefreshAsync(id);
        var old = btClassic.FirstOrDefault(c => c.Id == id);
        if (old == null) return;
        if (now == null) { btLog.Add($"Windows no longer lists {old.Name}."); return; }
        var updated = old with { Paired = now.Value.Paired, Connected = now.Value.Connected ?? old.Connected, GamepadDriver = now.Value.Paired ? now.Value.GamepadDriver ?? old.GamepadDriver : null };
        if (updated.Paired != old.Paired || updated.Connected != old.Connected) btLog.Add($"{old.Name}: {BluetoothManager.Gameplay(updated)}");
        if (updated.GamepadDriver != old.GamepadDriver && updated.GamepadDriver is { } g) btLog.Add($"{old.Name}: gamepad driver {(g ? "loaded" : "absent")}.");
        btClassic = btClassic.Select(c => c.Id == id ? updated : c).ToList();
    }

    /// <summary>Every few seconds while the manager shows: Windows' view of the picked gameplay device and the XInput count.
    /// Both are reads of what Windows already knows; neither touches the radio.</summary>
    private async Task BtWatchTick()
    {
        if (bluetoothView.Visibility != Visibility.Visible) return;
        int slots = WinBluetooth.XInputSlots();
        if (slots != btSlots) { if (btSlots >= 0) btLog.Add(BluetoothManager.XInput(slots)); btSlots = slots; }
        if (BtRows().FirstOrDefault(r => r.Key == btSelected)?.Classic is { } c) await BtRefreshClassic(c.Id);
        if (!btBusy) { BtRenderList(); BtRenderDetails(); }
    }

    private void BtSave()
    {
        try
        {
            string folder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "X20CTLBluetooth");
            Directory.CreateDirectory(folder);
            string path = System.IO.Path.Combine(folder, $"bluetooth-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
            var header = $"X20CTL Bluetooth diagnostics · {DateTime.Now:yyyy-MM-dd HH:mm}{Environment.NewLine}{btAdapter.Text}{Environment.NewLine}" +
                $"X20CTL link: {(btSession is { } s ? ConfigSession.Describe(s.State) + " " + s.Detail : "none")}{Environment.NewLine}" +
                string.Join(Environment.NewLine, BtRows().Select(r => $"  {r.Name} · {r.Transport} · {RowStatus(r)}"));
            File.WriteAllText(path, btLog.Export(header));
            btStatus.Text = "Saved to " + path;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { btStatus.Text = $"Couldn't save (0x{ex.HResult:X8})."; }
    }
}
