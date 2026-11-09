using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace X20Ctl.Product.Development;

/// <summary>Offscreen check (owner, 8 Nov 2026: "connected using USB and Bluetooth, yet it says it isn't connected"): with
/// no XInput input (the cable in Switch mode) but a reading from the controller over Bluetooth, the studio says connected
/// over Bluetooth; with neither, it still says offline. The Bluetooth reading is a fixture
/// here: the review never touches the real controller.</summary>
internal static class BluetoothLinkReview
{
    public static async Task Run(string directory)
    {
        ReviewSandbox.Active = true; ReviewSandbox.HideLibrary = true; IntroView.SoundEnabled = false;
        Directory.CreateDirectory(directory);
        var host = new ProductHost(new ZoneScene(true), Path.Combine(directory, "assignments.json"));
        var stage = new DesignStage { Child = host };
        using var source = new HwndSource(new HwndSourceParameters("Bluetooth link offscreen review") { WindowStyle = unchecked((int)0x80000000), Width = 1600, Height = 900, PositionX = -32000, PositionY = -32000 }) { RootVisual = stage };
        host.Zone.AssignController(0, host.Catalog.Get("x20_pro")); host.EnterStudio(0);
        var studio = host.Studio!; studio.ShowManagement("Device");
        var log = new List<string>();
        async Task Settle() { await host.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle); stage.Measure(new Size(1600, 900)); stage.Arrange(new Rect(0, 0, 1600, 900)); stage.UpdateLayout(); }
        void Expect(bool ok, string what) { log.Add((ok ? "PASS " : "FAIL ") + what); if (!ok) throw new InvalidOperationException(what + "\n" + string.Join("\n", log)); }

        // 1. no XInput input, no Bluetooth reading: offline
        studio.SetGameplayConnected(false); await Settle();
        Expect(studio.StatusNote.Text == "Controller offline", $"offline with nothing: status '{studio.StatusNote.Text}'");
        Expect(studio.Device!.ConnectionText == "Not detected", $"offline with nothing: device '{studio.Device.ConnectionText}'");
        Expect(host.FreshBluetoothBattery(0) == null, "no Bluetooth battery before a reading");

        // 2. still no XInput input, but the controller answered over Bluetooth: connected over Bluetooth, with its battery
        studio.ApplyTelemetry(new TelemetryData("EasySMX X20 Pro", "fixture", "3/4 (Charging)", true, null, "fixture")); await Settle();
        Expect(studio.StatusNote.Text == "Connected over Bluetooth", $"Bluetooth only: status '{studio.StatusNote.Text}'");
        Expect(studio.Device.ConnectionText == "Connected over Bluetooth", $"Bluetooth only: device '{studio.Device.ConnectionText}'");
        Expect(host.FreshBluetoothBattery(0) == "3/4 (Charging)", $"Bluetooth battery '{host.FreshBluetoothBattery(0)}'");
        var header = Find<HeaderBattery>(studio);
        string headerText = header == null ? "(none)" : System.Windows.Automation.AutomationProperties.GetName(header);
        Expect(header != null && headerText.Contains("3/4"), $"header battery '{headerText}'");

        // 3. gameplay input as well: plain connected
        studio.SetGameplayConnected(true); await Settle();
        Expect(studio.StatusNote.Text == "Connected", $"input and Bluetooth: status '{studio.StatusNote.Text}'");
        Expect(studio.Device.ConnectionText == "Gameplay input detected", $"input and Bluetooth: device '{studio.Device.ConnectionText}'");

        File.WriteAllText(Path.Combine(directory, "result.txt"), "PASS\n" + string.Join("\n", log));
    }

    static T? Find<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent is T hit) return hit;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) if (Find<T>(VisualTreeHelper.GetChild(parent, i)) is { } found) return found;
        return null;
    }
}
