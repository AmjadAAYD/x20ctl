using X20Ctl.Mobile.Core;

namespace X20Ctl.Mobile;

/// <summary>Nearby and paired Bluetooth LE devices, and the read scan. Listening and allowlisted reads only.</summary>
public sealed class BluetoothPage : ContentPage
{
    readonly Dictionary<string, SeenDevice> devices = new(StringComparer.OrdinalIgnoreCase);
    readonly VerticalStackLayout list = new() { Spacing = 8 };
    readonly Label status = Ui.Note("Not scanning.");
    readonly Label result = Ui.Mono();
    readonly Button share;
    readonly Switch showAll = new() { IsToggled = false, OnColor = Ui.Accent };
    BleScanner? scanner;
    ReadScan? last;
    CancellationTokenSource? reading;

    public BluetoothPage()
    {
        share = Ui.Action("Share read scan", async (_, _) => { if (last is not null) await Ui.ShareText("x20ctl-read-scan.txt", last.Text()); }, primary: false);
        share.IsVisible = false;
        showAll.Toggled += (_, _) => Redraw();
        Ui.Page(this, "Bluetooth", Ui.Column(
            Ui.Heading("Bluetooth"),
            Ui.Note("Seen here means the phone heard the device. It does not mean X20CTL supports it. A controller that is already connected to the phone as a gamepad may not advertise: it then shows up under paired devices."),
            Ui.Action("Scan", async (_, _) => await Scan()),
            status,
            new HorizontalStackLayout { Spacing = 8, Children = { showAll, Ui.Note("Show every device, not only likely controllers") } },
            list,
            Ui.Card(result),
            share));
        result.Text = "Pick a device and tap Read scan. It connects, lists the services, reads battery and device information, then disconnects.";
    }

    async Task Scan()
    {
        var permission = await Permissions.RequestAsync<Permissions.Bluetooth>();
        if (permission != PermissionStatus.Granted) { status.Text = "X20CTL needs the Nearby devices permission to scan. Allow it in Android settings."; return; }
        StopScan();
        devices.Clear(); Redraw();
        scanner = new BleScanner();
        scanner.Seen += d => MainThread.BeginInvokeOnMainThread(() => { Merge(d); Redraw(); });
        scanner.Failed += m => MainThread.BeginInvokeOnMainThread(() => status.Text = m);
        if (scanner.Start() is { } problem) { status.Text = problem; scanner = null; return; }
        status.Text = "Scanning for 15 seconds…";
        var current = scanner;
        await Task.Delay(TimeSpan.FromSeconds(15));
        if (scanner == current) { StopScan(); status.Text = $"Scan finished: {devices.Count} device(s) heard."; }
    }

    void StopScan() { scanner?.Stop(); scanner = null; }

    void Merge(SeenDevice d)
    {
        if (devices.TryGetValue(d.Address, out var old))
            d = d with { Name = d.Name ?? old.Name, Rssi = d.Rssi ?? old.Rssi, Services = d.Services.Union(old.Services).ToList(), Paired = d.Paired || old.Paired };
        devices[d.Address] = d;
    }

    void Redraw()
    {
        list.Clear();
        var shown = devices.Values.Where(d => showAll.IsToggled || DeviceLabel.LooksRelevant(d.Name, d.Address, d.Services))
            .OrderByDescending(d => DeviceLabel.LooksRelevant(d.Name, d.Address, d.Services)).ThenByDescending(d => d.Rssi ?? -200).ToList();
        foreach (var d in shown)
        {
            var target = d;
            var row = Ui.Card(
                Ui.Body($"{d.Name ?? "(no name)"}{(d.Paired ? " · paired" : "")}{(d.Rssi is { } rssi ? $" · {rssi} dBm" : "")}"),
                Ui.Note(DeviceLabel.Status(d.Address, d.Services)),
                Ui.Action("Read scan", async (_, _) => await Read(target), primary: false));
            list.Add(row);
        }
        if (shown.Count == 0) list.Add(Ui.Note(devices.Count == 0 ? "No devices yet." : $"{devices.Count} device(s) heard, none that look like a controller. Turn on \"Show every device\" to see them."));
    }

    async Task Read(SeenDevice target)
    {
        if (reading is not null) return;
        StopScan();
        reading = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        result.Text = $"Reading {target.Name ?? target.Address}…";
        share.IsVisible = false;
        try
        {
            last = await GattReader.Run(target, reading.Token);
            result.Text = last.Text();
            share.IsVisible = true;
        }
        catch (Exception e) when (e is Java.Lang.SecurityException or Java.Lang.IllegalArgumentException)
        { result.Text = "Couldn't read: " + e.Message; }
        finally { reading.Dispose(); reading = null; }
    }

    protected override void OnDisappearing() { StopScan(); base.OnDisappearing(); }
}
