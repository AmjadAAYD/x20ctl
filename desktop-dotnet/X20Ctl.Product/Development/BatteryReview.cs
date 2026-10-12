using System.IO;
using System.Text.Json;
using X20Ctl.Product.Scanner;
namespace X20Ctl.Product.Development;

/// <summary>Runs every read-only battery source on this PC (--review-battery OUT.json): XInput on all four slots,
/// Windows.Gaming.Input, the paired-device property, and a short listen on each controller's vendor channels.
/// Nothing is sent to any controller.</summary>
internal static class BatteryReview
{
    public static async Task Run(string output)
    {
        var xinput = new List<object>();
        using (var reader = new WindowsReader("XInput1_4.dll"))
            for (int i = 0; i < 4; i++) { var b = reader.Battery(i); xinput.Add(new { slot = i, connected = reader.Read(i, out _), b.Type, b.Level, homeReadable = reader.HomeReadable }); }
        object gaming; try { gaming = GameInputBattery.Read(); } catch (Exception e) { gaming = "failed: " + e; }
        var inventory = await Task.Run(() => WindowsInventory.Read(false));
        var property = BluetoothBattery.Read(inventory.interfaces.SelectMany(i => new[] { i.product, i.manufacturer }));
        var listens = new List<object>();
        foreach (var pad in inventory.interfaces.Where(i => i.usagePage == 1 && i.usage is 4 or 5).GroupBy(i => (i.vid, i.pid)).Select(g => g.First()))
        {
            if (!inventory.interfaces.Any(v => v.vendorDefined && v.vid == pad.vid && v.pid == pad.pid)) continue;
            var l = VendorListener.Start(inventory, pad.vid, pad.pid); await Task.Delay(6000);
            listens.Add(new { controller = $"{pad.vid:X4}:{pad.pid:X4} {pad.product}", channels = l.Finish() });
        }
        File.WriteAllText(output, JsonSerializer.Serialize(new { xinput, gaming, property = property.Select(p => new { p.Name, p.Percent }), listens,
            pads = inventory.interfaces.Where(i => i.usagePage == 1 && i.usage is 4 or 5 || i.vendorDefined).Select(i => new { i.vid, i.pid, i.product, i.manufacturer, page = $"0x{i.usagePage:X4}/0x{i.usage:X2}", i.inputLength }) },
            new JsonSerializerOptions(Evidence.Json) { WriteIndented = true }));
    }
}
