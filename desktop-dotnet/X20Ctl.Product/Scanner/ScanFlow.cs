using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace X20Ctl.Product.Scanner;

/// <summary>One finding of the full scan, in plain words, with how sure it is.</summary>
public sealed record Finding(string Area, string Value, string Detail, string Confidence);

/// <summary>
/// The full "reverse-engineering" scan's interpretation layer (owner direction 10 Oct 2026). It turns what the 2.0.0
/// engine captured, plus the read-only extras (battery, descriptor bit depths, vendor channels, Raw Input report rate),
/// into findings: battery, capabilities, which known controllers it resembles, stick and trigger detail, drift, hold
/// timing, what macro paddles look like to Windows, and whether lighting can be read. It never claims more than was
/// observed; each finding says how it was obtained.
/// </summary>
public static class ScanFlow
{
    /// <summary>Identities recorded in docs/00-findings.md and docs/01-protocol.md. A match is a resemblance, not a model.</summary>
    public static readonly (int Vid, int Pid, string Meaning)[] Known =
    [
        (0x045E, 0x028E, "Xbox 360-compatible XInput identity. Most pads in XInput mode, EasySMX included, use it, so it proves nothing about the model."),
        (0x045E, 0x02E0, "Xbox Wireless (Bluetooth) identity, shared by many pads."),
        (0x0079, 0x181C, "Seen with the EasySMX X20 in DInput mode."),
        (0x1D57, 0xFA60, "Seen with the EasySMX X20's 2.4 GHz receiver (Xenta chip)."),
        (0x1A34, 0xF517, "Reported for the EasySMX X15's 2.4 GHz receiver (research notes, not yet seen here)."),
        (0x2345, 0xE062, "Reported for the EasySMX D10's receiver, a vendor-defined HID device (research notes, not yet seen here)."),
    ];

    public const string Discord = "mistermajid";

    /// <summary>What the scan does and what it will likely find, shown before anything starts.</summary>
    public static readonly (string Title, string Text)[] Expectations =
    [
        ("Reads, never writes", "The scan only listens to what the controller already tells Windows. No settings, lighting, vibration or firmware are touched."),
        ("Buttons, sticks and triggers", "Every button is pressed and held so its exact timing is recorded; sticks are rolled for their full range and their resting drift; triggers are pulled for their full travel."),
        ("Battery", "Windows only reports a battery level for some wireless connections. Wired pads and many receivers will show \"wired\" or \"not reported\"."),
        ("Stick resolution and report rate", "The controller's own description says how many bits each stick uses, and the scan measures how often Windows receives reports. That is a measured rate, not a promise of polling speed."),
        ("Lighting (RGB)", "Expected to be unreadable. Lighting is set inside the controller's firmware and isn't exposed to Windows, so this will most likely say \"not readable\"."),
        ("Macro paddles (M buttons)", "Not tested on purpose. A paddle sends whatever button it was set to (A, B and so on), so Windows can't see it as its own button."),
        ("Not part of this scan", "Gyro and turbo are left out: gyro isn't exposed this way, and turbo is a script inside the firmware, not a button."),
    ];

    public static List<Finding> Interpret(Evidence evidence, Inventory inventory, (string Type, string Level, bool Known) battery, IReadOnlyDictionary<string, double> reportRates, double holdError)
    {
        var f = new List<Finding>();
        f.Add(new("Battery", battery.Known ? battery.Level : battery.Type, battery.Known ? "Reported by Windows (XInput battery information)." : "Windows didn't report a level for this connection. That is normal for wired pads and many receivers.", battery.Known ? "reported" : "unavailable"));
        var caps = evidence.XInputCapabilities.ElementAtOrDefault(evidence.Slot);
        f.Add(new("Capabilities", caps == null ? "not reported" : JsonSerializer.Serialize(caps, Evidence.Json).Contains("unavailable") ? "not reported" : "standard gamepad reported", "Driver-reported XInput capabilities (buttons, triggers, motors). Reported, not physically tested.", "reported"));
        // similar controllers by Windows identity
        var ids = inventory.interfaces.Where(i => i.vid != 0).Select(i => (i.vid, i.pid)).Distinct().ToList();
        var matches = Known.Where(k => ids.Contains((k.Vid, k.Pid))).Select(k => $"{k.Vid:X4}:{k.Pid:X4} · {k.Meaning}").ToList();
        f.Add(new("Similar controllers", matches.Count > 0 ? string.Join("\n", matches) : ids.Count > 0 ? "no known fingerprint" : "devices not listed",
            ids.Count > 0 ? "Windows identities seen: " + string.Join(", ", ids.Select(i => $"{i.vid:X4}:{i.pid:X4}")) + ". Matching IDs or names never prove a model." : "Run \"List Windows devices\" to compare identities.", matches.Count > 0 ? "resemblance" : "none"));
        // stick resolution from the descriptor
        var axes = inventory.interfaces.Where(i => i.usagePage == 1 && i.usage is 4 or 5 or 8).SelectMany(i => i.axes).Where(a => a.TryGetValue("name", out var n) && n is "X" or "Y" or "Rx" or "Ry" or "Z" or "Rz").ToList();
        f.Add(new("Stick resolution", axes.Count == 0 ? "not described" : string.Join(" · ", axes.GroupBy(a => a["bits"]).Select(g => $"{g.Key}-bit ({string.Join("/", g.Select(a => a["name"]))})")),
            axes.Count == 0 ? "The HID description wasn't readable for this interface. XInput itself always scales sticks to 16 bits." : "Bit sizes declared in the controller's HID report description. XInput rescales every stick to 16 bits.", axes.Count == 0 ? "unavailable" : "declared"));
        f.Add(new("Report rate", reportRates.Count == 0 ? "not measured" : string.Join(" · ", reportRates.Select(r => $"{r.Value:0} reports/s")),
            reportRates.Count == 0 ? "Raw Input saw no reports while the sticks moved." : "Reports Windows received per second while the sticks moved (Raw Input). Host-side measurement; not a verified polling rate.", reportRates.Count == 0 ? "unavailable" : "measured"));
        var vendor = inventory.interfaces.Where(i => i.vendorDefined).ToList();
        f.Add(new("Vendor channels", vendor.Count == 0 ? "none visible" : $"{vendor.Count} vendor-defined interface{(vendor.Count == 1 ? "" : "s")}",
            vendor.Count == 0 ? "No vendor-defined HID collection was visible on this connection." : "Collections on vendor usage pages: " + string.Join("; ", vendor.Select(v => $"page 0x{v.usagePage:X4} usage 0x{v.usage:X2}, in {v.inputLength} / out {v.outputLength} / feature {v.featureLength} bytes")) + ". A configuration protocol, if any, would use one of these; nothing was sent to them.", vendor.Count == 0 ? "none" : "observed"));
        // drift and ranges from the captured actions
        if (evidence.Records.TryGetValue("neutral", out var rest) && rest.Count > 0)
        {
            double Off(string x, string y) => rest.Max(r => Math.Sqrt(Math.Pow(r.Get(x) / 32768.0, 2) + Math.Pow(r.Get(y) / 32768.0, 2))) * 100;
            f.Add(new("Resting drift", $"left {Off("lx", "ly"):0.0}% · right {Off("rx", "ry"):0.0}%", "Largest distance from centre while the controller was left alone (Windows deadzone is about 24%).", "measured"));
        }
        foreach (var (stick, x, y) in new[] { ("left_stick", "lx", "ly"), ("right_stick", "rx", "ry") })
            if (evidence.Records.TryGetValue(stick, out var rows) && rows.Count > 0)
                f.Add(new(stick == "left_stick" ? "Left stick range" : "Right stick range", $"X {rows.Min(r => r.Get(x))}…{rows.Max(r => r.Get(x))} · Y {rows.Min(r => r.Get(y))}…{rows.Max(r => r.Get(y))}", "Raw 16-bit extremes reached while rolling the stick.", "measured"));
        foreach (var t in new[] { "LT", "RT" })
            if (evidence.Records.TryGetValue(t, out var rows) && rows.Count > 0)
                f.Add(new(t + " travel", $"{rows.Min(r => r.Get(t.ToLowerInvariant()))}…{rows.Max(r => r.Get(t.ToLowerInvariant()))} of 255", "Lowest and highest trigger values seen while pulling it.", "measured"));
        int tested = evidence.Records.Keys.Count(k => k != "preflight"), seen = evidence.Records.Count(r => r.Key != "preflight" && ((string)Analysis.Summary(r.Key, r.Value)["status"]).EndsWith("_observed") && !((string)Analysis.Summary(r.Key, r.Value)["status"]).StartsWith("inconclusive"));
        f.Add(new("Controls", $"{seen} of {tested} recognised", "Each control pressed, held and released on the chosen slot.", "measured"));
        if (holdError >= 0) f.Add(new("Hold timing", $"±{holdError:0} ms", "How far the measured hold times were from the 2-second holds asked for (includes your own timing).", "measured"));
        f.Add(new("Macro paddles", "seen as ordinary buttons", "M paddles send the button they're set to, so Windows can't tell them apart. Their macros can be recorded on the Macros page.", "by design"));
        f.Add(new("Lighting (RGB)", "not readable", "Lighting lives in the controller's firmware and isn't exposed through Windows input.", "by design"));
        return f;
    }

    /// <summary>The compact report the live X20CTLADMIN receiver accepts today (the established nine-file layout,
    /// 2 MiB): device.json with the controller name and identities, the findings, the per-control results and the
    /// app/system versions. The full evidence ZIP stays on the PC for manual sharing.</summary>
    public static byte[] CompactReport(string claimedModel, string transport, Evidence evidence, Inventory inventory, List<Finding> findings)
    {
        var first = inventory.interfaces.FirstOrDefault(i => i.usagePage == 1 && i.usage is 4 or 5 or 8) ?? inventory.interfaces.FirstOrDefault();
        var files = new Dictionary<string, object>
        {
            ["device.json"] = new { controllerName = claimedModel == "Unknown" ? (first?.product is { Length: > 0 } p ? p : "Unknown controller") : "EasySMX " + claimedModel, claimedModel, connection = transport, vid = first?.vid, pid = first?.pid,
                interfaces = inventory.interfaces.Select(i => new { i.kind, i.vid, i.pid, i.product, i.manufacturer, i.usagePage, i.usage, i.inputLength, i.outputLength, i.featureLength, i.vendorDefined, i.axes }).ToList() },
            ["input-captures.json"] = evidence.Records.Select(r => Analysis.Summary(r.Key, r.Value)).ToList(),
            ["input-mapping.json"] = findings,
            ["system.json"] = new { reportSchemaVersion = 1, collector = "x20ctl-native-controller-check", collectorVersion = "2.0.0-local", app = "X20CTL native", os = Environment.OSVersion.VersionString },
            ["scanner-version.txt"] = "Controller Scanner 2.0.0-local engine (X20CTL native Controller Check)",
        };
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
            foreach (var (name, value) in files)
            {
                var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
                using var w = new StreamWriter(entry.Open(), new UTF8Encoding(false));
                w.Write(value is string s ? s : JsonSerializer.Serialize(value, new JsonSerializerOptions(Evidence.Json) { WriteIndented = true }));
            }
        return stream.ToArray();
    }

    /// <summary>Posts the compact report to X20CTLADMIN exactly as the existing desktop uploader does (multipart:
    /// clientSubmissionId, metadata, report.zip) and returns the receipt. Only ever called after the player chose it.</summary>
    public static async Task<string> Upload(byte[] zip, string controllerName, string transport)
    {
        if (ReviewSandbox.Active) return "CR-SANDBOX";
        if (zip.Length > 2 * 1024 * 1024) throw new InvalidDataException("Report exceeds the receiver's 2 MiB limit.");
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        var metadata = JsonSerializer.Serialize(new { controllerName, appVersion = "native-preview", scannerVersion = "2.0.0-local", clientTimestamp = DateTime.UtcNow.ToString("o"), connectionType = transport });
        using var form = new MultipartFormDataContent("x20ctl-" + Guid.NewGuid().ToString("N"))
        {
            { new StringContent(Guid.NewGuid().ToString()), "clientSubmissionId" },
            { new StringContent(metadata), "metadata" },
        };
        var file = new ByteArrayContent(zip); file.Headers.ContentType = new("application/zip"); form.Add(file, "report", "report.zip");
        using var response = await http.PostAsync("https://x20-admin.vercel.app/api/controller-report", form);
        if ((int)response.StatusCode is not (200 or 201)) throw new HttpRequestException($"Receiver returned HTTP {(int)response.StatusCode}.");
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        string id = doc.RootElement.TryGetProperty("submissionId", out var s) ? s.GetString() ?? "" : "";
        if (!doc.RootElement.TryGetProperty("success", out var ok) || !ok.GetBoolean() || !Regex.IsMatch(id, "^CR-[0-9A-F]{24}$")) throw new InvalidDataException("Receiver returned no valid receipt.");
        return id;
    }
}
