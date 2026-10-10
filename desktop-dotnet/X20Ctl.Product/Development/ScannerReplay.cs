using System.IO;
using System.IO.Compression;
using System.Text.Json;
using X20Ctl.Product.Scanner;
namespace X20Ctl.Product.Development;

/// <summary>Replays a saved Controller Check report through today's scanner analysis (--review-scanner-replay ZIP OUT.json):
/// each step's recorded XInput samples get a fresh verdict, plus the model the controller names itself as and any
/// Bluetooth battery levels on this PC. Reads the ZIP only; nothing touches a controller.</summary>
internal static class ScannerReplay
{
    public static Task Run(string zipPath, string output)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        var steps = new List<object>(); var evidence = new Evidence();
        foreach (var entry in zip.Entries.Where(e => e.Name.EndsWith(".jsonl") && e.Name != "raw-input.jsonl").OrderBy(e => e.Name))
        {
            string action = Path.GetFileNameWithoutExtension(entry.Name); var rows = new List<Sample>();
            using var reader = new StreamReader(entry.Open());
            while (reader.ReadLine() is { } line)
            {
                if (line.Length == 0) continue;
                using var doc = JsonDocument.Parse(line); var v = doc.RootElement.GetProperty("values");
                short S(string k) => (short)v.GetProperty(k).GetInt32();
                var state = new State { Packet = v.GetProperty("packet").GetUInt32(), Pad = new Pad { Buttons = (ushort)v.GetProperty("buttons").GetInt32(), LT = (byte)v.GetProperty("lt").GetInt32(), RT = (byte)v.GetProperty("rt").GetInt32(), LX = S("lx"), LY = S("ly"), RX = S("rx"), RY = S("ry") } };
                rows.Add(new Sample(action, v.GetProperty("slot").GetInt32(), state, doc.RootElement.GetProperty("elapsed_ms").GetDouble()));
            }
            evidence.Records[action] = rows;
            var summary = Analysis.Summary(action, rows);
            steps.Add(new { action, status = summary["status"], seen = Analysis.Seen((string)summary["status"]), fired = Analysis.Fired(rows) });
        }
        Inventory inventory = new();
        if (zip.GetEntry("windows-inventory.json") is { } inv)
        {
            using var s = new StreamReader(inv.Open());
            inventory = JsonSerializer.Deserialize<Inventory>(s.ReadToEnd(), Evidence.Json) ?? new();
        }
        var findings = ScanFlow.Interpret(evidence, inventory, ("wired", "wired (no battery level)", false), new Dictionary<string, double>(), -1,
            BluetoothBattery.Read(inventory.interfaces.SelectMany(i => new[] { i.product, i.manufacturer })));
        File.WriteAllText(output, JsonSerializer.Serialize(new { steps, detected = ScanFlow.DetectModel(inventory)?.ToString(), transport = ScanFlow.DetectTransport(inventory), findings }, new JsonSerializerOptions(Evidence.Json) { WriteIndented = true }));
        return Task.CompletedTask;
    }
}
