using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
namespace X20Ctl.Linux;

/// <summary>Reads one controller's evdev events without taking it (no grab), with the timing a scan needs. Read-only: nothing is ever written to the pad.</summary>
internal sealed unsafe class EvReader : IDisposable
{
    readonly int fd; readonly Stopwatch clock = Stopwatch.StartNew();
    public Dictionary<int, (int Lo, int Hi)> Ranges { get; } = new();
    public Dictionary<int, int> Value { get; } = new();
    public Dictionary<int, double> Down { get; } = new();
    public List<double> Reports { get; } = new();
    public record Ev(double T, ushort Type, ushort Code, int Value);

    public EvReader(EvDevice device)
    {
        fd = Native.Open(device.Path, Native.ReadOnly | Native.NonBlock);
        if (fd < 0) throw new UnauthorizedAccessException("No permission to read " + device.Path);
        for (int code = 0; code < 0x40; code++)
        {
            var buffer = new byte[24];
            if (Native.IoctlBuf(fd, Native.Ioc(2, 'E', 0x40 + code, 24), buffer) == 0) { Ranges[code] = (BitConverter.ToInt32(buffer, 4), BitConverter.ToInt32(buffer, 8)); Value[code] = BitConverter.ToInt32(buffer, 0); }
        }
    }

    public double Now => clock.Elapsed.TotalSeconds;

    public List<Ev> Poll(int ms)
    {
        var list = new List<Ev>(); var p = new Native.PollFd { Fd = fd, Events = Native.PollIn }; Native.Poll(ref p, 1, ms);
        int size = Native.EventSize, time = IntPtr.Size * 2; var buffer = new byte[size * 64];
        while (true)
        {
            nint n; fixed (byte* b = buffer) n = Native.Read(fd, b, (nuint)buffer.Length);
            if (n <= 0) break;
            double t = Now;
            for (int o = 0; o + size <= n; o += size)
            {
                ushort type = BitConverter.ToUInt16(buffer, o + time), code = BitConverter.ToUInt16(buffer, o + time + 2); int v = BitConverter.ToInt32(buffer, o + time + 4);
                if (type == 0) { Reports.Add(t); continue; }
                if (type == 1) { if (v != 0) Down[code] = t; else Down.Remove(code); }
                else if (type == 3) Value[code] = v;
                list.Add(new Ev(t, type, code, v));
            }
            if (n < buffer.Length) break;
        }
        return list;
    }

    /// <summary>An axis as -1 to 1 around its centre.</summary>
    public double Norm(int code) { if (!Ranges.TryGetValue(code, out var r) || r.Hi <= r.Lo || !Value.TryGetValue(code, out int v)) return 0; return Math.Clamp((v - r.Lo) / (double)(r.Hi - r.Lo) * 2 - 1, -1, 1); }
    public void Dispose() => Native.Close(fd);
}

/// <summary>The guided Controller Check on Linux: every button, stick and trigger is pressed one step at a time while the events are recorded. A port of the Python check
/// in tools/linux_controller_check. Read-only. The in-app version has not been run against a real pad yet.</summary>
internal static class CheckEngine
{
    public const string Version = "2.1.0-studio", Endpoint = "https://x20-admin.vercel.app/api/controller-report", Host = "x20ctl-linux-studio-check";
    public sealed record Step(string Id, string Prompt, string Kind);
    public static readonly Step[] Steps =
    [
        new("neutral", "Put the controller down and don't touch it. This measures stick drift at rest.", "rest"),
        .. new[] { "A", "B", "X", "Y", "LB", "RB" }.Select(b => new Step(b, $"Press and hold {b} for 2 seconds, then let go. Do it twice.", "button")),
        .. new[] { ("dpad_up", "up"), ("dpad_right", "right"), ("dpad_down", "down"), ("dpad_left", "left"), ("dpad_up_right", "up-right (both together)"), ("dpad_down_right", "down-right"), ("dpad_down_left", "down-left"), ("dpad_up_left", "up-left") }
            .Select(d => new Step(d.Item1, $"Press and hold D-pad {d.Item2} for 2 seconds, then let go. Twice.", "dpad")),
        .. new[] { ("Start", "Start / Menu"), ("Back", "Back / View"), ("L3", "the left stick click"), ("R3", "the right stick click") }.Select(b => new Step(b.Item1, $"Press and hold {b.Item2} for 2 seconds, then let go. Twice.", "button")),
        .. new[] { "LT", "RT" }.Select(t => new Step(t, $"Pull {t} slowly all the way in, hold it 2 seconds, let it out. Twice.", "trigger")),
        .. new[] { "left_stick", "right_stick" }.Select(s => new Step(s, $"Roll the {s.Replace('_', ' ')} slowly round its edge, twice, then let it centre.", "stick")),
    ];

    static readonly Dictionary<int, string> KeyNames = new()
    {
        [0x130] = "BTN_SOUTH", [0x131] = "BTN_EAST", [0x132] = "BTN_C", [0x133] = "BTN_NORTH", [0x134] = "BTN_WEST", [0x135] = "BTN_Z", [0x136] = "BTN_TL", [0x137] = "BTN_TR", [0x138] = "BTN_TL2", [0x139] = "BTN_TR2",
        [0x13A] = "BTN_SELECT", [0x13B] = "BTN_START", [0x13C] = "BTN_MODE", [0x13D] = "BTN_THUMBL", [0x13E] = "BTN_THUMBR", [0x220] = "BTN_DPAD_UP", [0x221] = "BTN_DPAD_DOWN", [0x222] = "BTN_DPAD_LEFT", [0x223] = "BTN_DPAD_RIGHT"
    };
    static readonly Dictionary<int, string> AbsNames = new() { [0] = "ABS_X", [1] = "ABS_Y", [2] = "ABS_Z", [3] = "ABS_RX", [4] = "ABS_RY", [5] = "ABS_RZ", [9] = "ABS_GAS", [10] = "ABS_BRAKE", [0x10] = "ABS_HAT0X", [0x11] = "ABS_HAT0Y" };
    static readonly Dictionary<(int, int), string> Known = new()
    {
        [(0x045E, 0x028E)] = "Xbox 360-compatible identity. Most pads in XInput mode, EasySMX included, use it; it proves nothing about the model.",
        [(0x045E, 0x02E0)] = "Xbox Wireless (Bluetooth) identity, shared by many pads.",
        [(0x0079, 0x181C)] = "Seen with the EasySMX X20 in DInput mode.",
        [(0x1D57, 0xFA60)] = "Seen with the EasySMX X20's 2.4 GHz receiver (Xenta chip).",
        [(0x1A34, 0xF517)] = "Reported for the EasySMX X15's 2.4 GHz receiver (research notes, not yet seen here).",
        [(0x2345, 0xE062)] = "Reported for the EasySMX D10's receiver, a vendor-defined HID device (research notes, not yet seen here)."
    };

    public sealed record Result(string Step, string Status, string[] KeysSeen, Dictionary<string, int[]> AxesMoved, Dictionary<string, int[]> HoldsMs, int ReportsPerSecondMax, object[] Events);
    public sealed record Finding(string Area, string Value, string Detail);

    /// <summary>Runs one step: a three-second countdown, then eight seconds of recording. Update gets the phase text and the seconds left.</summary>
    public static Result Run(EvReader pad, Step step, Func<bool> skip, Func<bool> stop, Action<string, string, double> update)
    {
        for (int n = 3; n >= 1; n--)
        {
            update("Get ready…", n.ToString(), n); double end = pad.Now + 1;
            while (pad.Now < end) { pad.Poll(20); if (stop()) throw new OperationCanceledException(); if (skip()) return new(step.Id, "skipped", [], [], [], 0, []); }
        }
        double start = pad.Now; pad.Reports.Clear(); var events = new List<object>(); var holds = new Dictionary<int, List<int>>(); var down = new Dictionary<int, double>(); var ranges = new Dictionary<int, (int Lo, int Hi)>();
        var codes = new SortedSet<string>();
        while (pad.Now - start < 8)
        {
            foreach (var e in pad.Poll(10))
            {
                events.Add(new { t_ms = Math.Round((e.T - start) * 1000, 1), type = e.Type == 1 ? "key" : "abs", code = (int)e.Code, name = e.Type == 1 ? KeyNames.GetValueOrDefault(e.Code) : AbsNames.GetValueOrDefault(e.Code, $"ABS_0x{e.Code:X2}"), value = e.Value });
                if (e.Type == 1) { codes.Add(KeyNames.GetValueOrDefault(e.Code) ?? e.Code.ToString()); if (e.Value != 0) down[e.Code] = e.T; else if (down.Remove(e.Code, out double t0)) { if (!holds.TryGetValue(e.Code, out var l)) holds[e.Code] = l = []; l.Add((int)Math.Round((e.T - t0) * 1000)); } }
                if (e.Type == 3) { var (lo, hi) = ranges.TryGetValue(e.Code, out var r) ? r : (e.Value, e.Value); ranges[e.Code] = (Math.Min(lo, e.Value), Math.Max(hi, e.Value)); }
            }
            if (stop()) throw new OperationCanceledException(); if (skip()) return new(step.Id, "skipped", [.. codes], [], [], 0, [.. events.Take(4000)]);
            string live;
            if (step.Kind is "button" or "dpad" && pad.Down.Count > 0) { var (c, t) = pad.Down.OrderBy(d => d.Value).First(); live = $"Holding {KeyNames.GetValueOrDefault(c, "0x" + c.ToString("X"))} · {pad.Now - t:0.00} s"; }
            else if (step.Kind == "trigger") live = string.Join("  ", new[] { 2, 5, 9, 10 }.Where(pad.Value.ContainsKey).Select(c => $"{AbsNames.GetValueOrDefault(c)} {pad.Value[c]}")) is { Length: > 0 } s ? s : "pull the trigger";
            else if (step.Kind is "stick" or "rest") live = $"left {pad.Norm(0):+0.00;-0.00},{pad.Norm(1):+0.00;-0.00}   right {pad.Norm(3):+0.00;-0.00},{pad.Norm(4):+0.00;-0.00}";
            else live = "waiting for the control…";
            update("Recording", live, 8 - (pad.Now - start));
        }
        int rate = 0; var stamps = pad.Reports; int j = 0;
        for (int k = 0; k < stamps.Count; k++) { while (stamps[k] - stamps[j] > 1) j++; rate = Math.Max(rate, k - j + 1); }
        var moved = ranges.Where(r => r.Value.Hi != r.Value.Lo).ToDictionary(r => AbsNames.GetValueOrDefault(r.Key, $"ABS_0x{r.Key:X2}"), r => new[] { r.Value.Lo, r.Value.Hi });
        bool observed = step.Kind switch { "button" => codes.Count > 0, "dpad" or "trigger" or "stick" => codes.Count > 0 || moved.Count > 0, _ => !events.Any() || new[] { 0, 1, 3, 4 }.Where(pad.Ranges.ContainsKey).All(c => Math.Abs(pad.Norm(c)) < .24) };
        return new(step.Id, observed ? "observed" : "not_observed", [.. codes], moved, holds.ToDictionary(h => KeyNames.GetValueOrDefault(h.Key, "0x" + h.Key.ToString("X")), h => h.Value.ToArray()), rate, [.. events.Take(4000)]);
    }

    static string Read(string path) { try { return File.ReadAllText(path).Trim(); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return ""; } }

    public static (string Capacity, string Status)? Battery()
    {
        if (!Directory.Exists("/sys/class/power_supply")) return null;
        foreach (var dir in Directory.EnumerateDirectories("/sys/class/power_supply"))
            if (Read(dir + "/scope") == "Device" && Read(dir + "/capacity") is { Length: > 0 } cap) return (cap, Read(dir + "/status"));
        return null;
    }

    public static List<Finding> Findings(EvDevice device, ushort bus, EvReader pad, IReadOnlyList<Result> results, string rest)
    {
        var f = new List<Finding>(); var battery = Battery();
        f.Add(new("Battery", battery is { } b ? b.Capacity + "%" : "not reported", battery != null ? "From the kernel's power_supply entry for the controller." : "The kernel exposes no battery for this connection (normal for wired pads and many receivers)."));
        f.Add(new("Similar controllers", Known.GetValueOrDefault((device.Vendor, device.Product), "no known fingerprint"), $"Linux identity {device.Vendor:X4}:{device.Product:X4} over bus 0x{bus:X}. Matching IDs never prove a model."));
        var bits = new[] { 0, 1, 3, 4 }.Where(pad.Ranges.ContainsKey).Select(c => (Name: AbsNames[c], Bits: (int)Math.Round(Math.Log2(Math.Max(2, pad.Ranges[c].Hi - pad.Ranges[c].Lo + 1))))).ToList();
        f.Add(new("Stick resolution", string.Join(" · ", bits.Select(x => $"{x.Bits}-bit {x.Name}").Distinct()) is { Length: > 0 } sr ? sr : "not described", "Size of each stick axis range as the kernel reports it."));
        int vendor = HidrawVendorCollections(device);
        f.Add(new("Vendor channels", vendor > 0 ? $"{vendor} hidraw interface(s) for this controller" : "none visible", "Where a configuration protocol would live; nothing was sent to them."));
        var rates = results.Where(r => r.Step is "left_stick" or "right_stick").Select(r => r.ReportsPerSecondMax).ToList();
        f.Add(new("Report rate", rates.Count > 0 && rates.Max() > 0 ? rates.Max() + " reports/s" : "not measured", "Most reports the kernel delivered in one second while a stick moved. Host-side; not a verified polling rate."));
        if (rest.Length > 0) f.Add(new("Resting drift", rest, "Largest distance from centre while untouched."));
        f.Add(new("Controls", $"{results.Count(r => r.Status == "observed")} of {results.Count} recognised", "Each control pressed and held on this device."));
        f.Add(new("Button codes", string.Join("; ", results.Where(r => r.KeysSeen.Length > 0).Select(r => $"{r.Step} → {string.Join(", ", r.KeysSeen)}")) is { Length: > 0 } codes ? codes : "none", "Which Linux input code each control sent: the controller's mapping as the kernel sees it."));
        f.Add(new("Macro paddles", "seen as ordinary buttons", "M paddles send the button they're set to. Record their macros on the Macros page."));
        f.Add(new("Lighting (RGB)", "not readable", "Lighting lives in the controller's firmware and isn't exposed through Linux input."));
        return f;
    }

    static int HidrawVendorCollections(EvDevice d)
    {
        int n = 0;
        try
        {
            if (!Directory.Exists("/sys/class/hidraw")) return 0;
            foreach (var dir in Directory.EnumerateDirectories("/sys/class/hidraw"))
                if (Regex.Match(Read(dir + "/device/uevent"), @"HID_ID=[0-9A-Fa-f]+:([0-9A-Fa-f]+):([0-9A-Fa-f]+)") is { Success: true } m
                    && Convert.ToInt32(m.Groups[1].Value, 16) == d.Vendor && Convert.ToInt32(m.Groups[2].Value, 16) == d.Product) n++;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        return n;
    }

    static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true };

    /// <summary>Writes the folder and ZIP under ~/X20CTLInputReports and returns the ZIP path.</summary>
    public static string Save(EvDevice device, ushort bus, string model, string transport, bool send, IReadOnlyList<Result> results, IReadOnlyList<Finding> found)
    {
        string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"), folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "X20CTLInputReports", $"input-{stamp}-{Guid.NewGuid():N}"[..40]);
        Directory.CreateDirectory(folder);
        var files = new Dictionary<string, object>
        {
            ["metadata.json"] = new { collectorVersion = Version, collectorHost = Host, claimedModel = model, claimedTransport = transport, modelDetected = false, source = "linux_evdev", device = new { name = device.Name, path = device.Path, bustype = (int)bus, vendor = (int)device.Vendor, product = (int)device.Product },
                configurationWrites = false, automaticUpload = send, hostTimingIsPollingRate = false, os = AppInfo.Os },
            ["steps.json"] = results, ["scan-summary.json"] = found,
            ["README.txt"] = "Local input evidence only (Linux). Model/transport are owner claims. No configuration, vibration, firmware or hidden upload.\n"
        };
        foreach (var (name, value) in files) File.WriteAllText(Path.Combine(folder, name), value as string ?? JsonSerializer.Serialize(value, Pretty), Encoding.UTF8);
        var listed = Directory.GetFiles(folder).OrderBy(p => p, StringComparer.Ordinal).Select(p => new { path = Path.GetFileName(p), size = new FileInfo(p).Length, sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))).ToLowerInvariant() });
        File.WriteAllText(Path.Combine(folder, "manifest.json"), JsonSerializer.Serialize(new { schema = "input-diagnostic/1", files = listed }, Pretty), Encoding.UTF8);
        string archive = folder + ".zip";
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create)) foreach (var p in Directory.GetFiles(folder).OrderBy(p => p, StringComparer.Ordinal)) zip.CreateEntryFromFile(p, Path.GetFileName(p));
        return archive;
    }

    /// <summary>Sends the compact summary to X20CTLADMIN. Returns the CR-… receipt. Only called when the person chose "Scan and send".</summary>
    public static async Task<string> Upload(EvDevice device, string model, string transport, IReadOnlyList<Result> results, IReadOnlyList<Finding> found)
    {
        string name = model == "Unknown" ? device.Name : "EasySMX " + model;
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, true))
        {
            void Add(string n, object v) { var e = zip.CreateEntry(n); using var w = new StreamWriter(e.Open(), Encoding.UTF8); w.Write(v as string ?? JsonSerializer.Serialize(v, Pretty)); }
            Add("device.json", new { controllerName = name, claimedModel = model, connection = transport, vid = (int)device.Vendor, pid = (int)device.Product });
            Add("input-captures.json", results.Select(r => new { step = r.Step, status = r.Status, keysSeen = r.KeysSeen, axesMoved = r.AxesMoved, holdsMs = r.HoldsMs, reportsPerSecondMax = r.ReportsPerSecondMax }));
            Add("input-mapping.json", found); Add("system.json", new { reportSchemaVersion = 1, collector = Host, collectorVersion = Version, os = AppInfo.Os }); Add("scanner-version.txt", "X20CTL Controller Check (Linux Studio) " + Version);
        }
        if (buffer.Length > 2 * 1024 * 1024) throw new InvalidOperationException("The report is over the receiver's 2 MiB limit.");
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(Guid.NewGuid().ToString()), "clientSubmissionId");
        form.Add(new StringContent(JsonSerializer.Serialize(new { controllerName = name, appVersion = AppInfo.Version, scannerVersion = Version, clientTimestamp = DateTime.UtcNow.ToString("o"), connectionType = transport })), "metadata");
        var file = new ByteArrayContent(buffer.ToArray()); file.Headers.ContentType = new("application/zip"); form.Add(file, "report", "report.zip");
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        using var response = await http.PostAsync(Endpoint, form);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        string id = doc.RootElement.TryGetProperty("submissionId", out var s) ? s.GetString() ?? "" : "";
        if (!id.StartsWith("CR-")) throw new InvalidOperationException("The receiver returned no receipt.");
        return id;
    }
}

/// <summary>Tools → Controller not working?: an introduction, then a guided, read-only scan of every control, then the findings.</summary>
internal sealed class CheckPage : UserControl
{
    enum Phase { Intro, Setup, Running, Done }
    readonly Action back, watch; Phase phase = Phase.Intro; bool send;
    IReadOnlyList<EvDevice> pads = []; EvDevice? chosen; string model = "Unknown", transport = "not sure", error = "", sendLine = "";
    int stepIndex; string stepPhase = "", live = "", count = ""; double left; volatile bool skip, stop;
    List<CheckEngine.Result> results = []; List<CheckEngine.Finding> found = []; string archive = "";

    public CheckPage(Action back, Action watch) { this.back = back; this.watch = watch; Build(); }

    void Build()
    {
        var page = phase switch { Phase.Intro => Intro(), Phase.Setup => Setup(), Phase.Running => Running(), _ => Done() };
        Content = new ScrollViewer { Content = page };
    }

    static Control InfoCard(string title, string text) { var c = T.Card(T.Stack(5, T.Label(title, 18, null, FontWeight.SemiBold), T.Label(text, 14.5, T.Soft)), 20, 16); ((TextBlock)((StackPanel)c.Child!).Children[1]).TextWrapping = TextWrapping.Wrap; return c; }

    Control Intro()
    {
        var grid = new UniformGrid { Columns = 2 };
        foreach (var (t, x) in new[]
        {
            ("Reads, never writes", "The scan only listens to what the controller already tells Linux. No settings, lighting, vibration or firmware are touched."),
            ("Buttons, sticks and triggers", "Every button is pressed and held so its exact timing is recorded; sticks are rolled for their full range and their resting drift; triggers are pulled for their full travel."),
            ("Battery", "Read from the kernel's power_supply entry for the controller, when the connection reports one. Nothing is ever sent to it."),
            ("Stick resolution and report rate", "The scan reads how many bits each stick axis uses and measures how often the kernel receives reports. That is a measured rate, not a promise of polling speed."),
            ("Lighting (RGB)", "Expected to be unreadable. Lighting is set inside the controller's firmware and isn't exposed to Linux, so this will most likely say \"not readable\"."),
            ("Macro paddles (M buttons)", "Each paddle gets its own step: press it when asked, and the scan records which button it sends. If your controller has fewer paddles, leave that step alone."),
            ("Every step tells you what to do", "Each step shows what to press and a countdown: hold, let go, hold again, let go. Follow the timer; the scan does the rest. Gyro is left out: it isn't exposed this way."),
            ("Your report", "The full ZIP is always saved to ~/X20CTLInputReports. \"Scan and send\" also sends a compact summary (controller names, IDs and results, no personal files) to X20CTLADMIN, the X20CTL team's report site, when the scan ends. You can send the ZIP to @mistermajid on Discord too.")
        }) { var p = InfoCard(t, x); p.Margin = new Thickness(0, 0, 14, 14); grid.Children.Add(p); }
        var note = !OperatingSystem.IsLinux() ? T.Label("The scan reads Linux input devices, so it can only run on Linux.", 14.5, T.Warn) : T.Label("", 1);
        var buttons = T.Wrap(12, new Pill("➤  Scan and send the report", Pill.Kinds.Primary, () => { send = true; Setup(true); }) { Padding = new Thickness(26, 13) }, new Pill("Scan, keep it on this PC", Pill.Kinds.Ghost, () => { send = false; Setup(true); }) { Padding = new Thickness(26, 13) },
            new Pill("Just watch live input", Pill.Kinds.Ghost, watch) { Padding = new Thickness(26, 13) }, new Pill("←  Back to Tools", Pill.Kinds.Ghost, back) { Padding = new Thickness(26, 13) });
        var title = T.Stack(6, T.Label("Before you scan", 42, null, FontWeight.Bold), T.Label("This is a full controller scan: you'll be guided through every button, stick and trigger, one step at a time, while X20CTL records exactly what the controller reports. Here is what it does and what to expect.", 16.5, T.Soft));
        ((TextBlock)title.Children[1]).TextWrapping = TextWrapping.Wrap;
        return new StackPanel { Margin = new Thickness(60, 16, 60, 20), Spacing = 16, Children = { title, grid, note, buttons } };
    }

    void Setup(bool fromIntro) { phase = Phase.Setup; error = ""; pads = OperatingSystem.IsLinux() ? PcEngine.Controllers() : []; chosen = pads.FirstOrDefault(); Build(); }

    Control Setup()
    {
        var stack = T.Stack(12, T.Label("Which controller?", 36, null, FontWeight.Bold));
        if (pads.Count == 0) stack.Children.Add(T.Label(OperatingSystem.IsLinux() ? "No game controller found. Plug it in or pair it, wake it with Home, close other controller apps, then try again." : "This scan reads Linux input devices, so it can only run on Linux.", 15.5, T.Warn));
        else
        {
            var pick = new ComboBox { ItemsSource = pads.Select(p => $"{p.Name}  ({p.Vendor:X4}:{p.Product:X4})").ToList(), SelectedIndex = Math.Max(0, pads.ToList().IndexOf(chosen!)), MinWidth = 520, FontSize = 15 };
            pick.SelectionChanged += (_, _) => chosen = pads[Math.Max(0, pick.SelectedIndex)];
            var modelBox = new TextBox { Text = model == "Unknown" ? "" : model, Watermark = "Printed model, e.g. X20, X15, D10 (leave empty if unsure)", Width = 520, FontSize = 15 }; modelBox.TextChanged += (_, _) => model = string.IsNullOrWhiteSpace(modelBox.Text) ? "Unknown" : modelBox.Text.Trim();
            var link = new ComboBox { ItemsSource = new[] { "usb", "receiver", "bluetooth", "not sure" }, SelectedItem = transport, MinWidth = 260, FontSize = 15 }; link.SelectionChanged += (_, _) => transport = link.SelectedItem as string ?? "not sure";
            stack.Children.Add(Parts.Caps("CONTROLLER")); stack.Children.Add(pick); stack.Children.Add(Parts.Caps("MODEL")); stack.Children.Add(modelBox); stack.Children.Add(Parts.Caps("CONNECTED BY")); stack.Children.Add(link);
            stack.Children.Add(T.Label(send ? "When the scan ends, a compact summary goes to X20CTLADMIN. The full ZIP is saved on this PC either way." : "Nothing is sent. The full ZIP is saved on this PC.", 14, T.Soft));
        }
        if (error.Length > 0) stack.Children.Add(T.Label(error, 14.5, T.Warn));
        stack.Children.Add(T.Line(12, new Pill("➤  Start the scan", Pill.Kinds.Primary, Start) { Padding = new Thickness(28, 13), Enabled = pads.Count > 0 }, new Pill("←  Back", Pill.Kinds.Ghost, () => { phase = Phase.Intro; Build(); }) { Padding = new Thickness(26, 13) }));
        var card = T.Card(stack, 40, 24); card.MaxWidth = 760; card.HorizontalAlignment = HorizontalAlignment.Left; card.Margin = new Thickness(60, 16, 0, 20); return card;
    }

    void Start()
    {
        if (chosen == null) return;
        EvReader pad; try { pad = new EvReader(chosen); } catch (UnauthorizedAccessException) { error = "No permission to read " + chosen.Path + ". Add yourself to the input group (sudo usermod -aG input $USER, then log in again)."; Build(); return; }
        phase = Phase.Running; stepIndex = 0; results = []; skip = stop = false; Build();
        var device = chosen; string m = model, t = transport; bool doSend = send;
        _ = Task.Run(async () =>
        {
            string rest = ""; ushort bus = 3;
            try
            {
                using (pad)
                    foreach (var step in CheckEngine.Steps)
                    {
                        Dispatcher.UIThread.Post(() => { stepIndex = results.Count; Build(); });
                        skip = false; var r = CheckEngine.Run(pad, step, () => skip, () => stop, (ph, lv, secs) => Dispatcher.UIThread.Post(() => { stepPhase = ph; live = lv; left = secs; Refresh(); }));
                        results.Add(r);
                        if (step.Id == "neutral") rest = $"left {Math.Abs(pad.Norm(0)) * 100:0.0}% · right {Math.Abs(pad.Norm(3)) * 100:0.0}% from centre";
                    }
            }
            catch (OperationCanceledException) { }
            var f = CheckEngine.Findings(device, bus, pad, results, rest);
            string zip = ""; string line = "";
            try { zip = CheckEngine.Save(device, bus, m, t, doSend, results, f); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { line = "Couldn't save the report: " + e.Message; }
            if (doSend)
            {
                try { line = "Sent to X20CTLADMIN · receipt " + await CheckEngine.Upload(device, m, t, results, f); }
                catch (Exception e) when (e is HttpRequestException or TaskCanceledException or InvalidOperationException or JsonException) { line = "Not sent to X20CTLADMIN (" + e.Message + "). The ZIP is saved; share it by hand."; }
            }
            Dispatcher.UIThread.Post(() => { found = f; archive = zip; sendLine = line; phase = Phase.Done; Build(); });
        });
    }

    readonly TextBlock liveLabel = T.Label("", 16, T.Text), timer = T.Label("", 64, null, FontWeight.Bold);
    void Refresh() { liveLabel.Text = live; timer.Text = stepPhase == "Recording" ? left.ToString("0.0") + " s" : count.Length > 0 ? count : left.ToString("0"); if (stepPhase == "Get ready…") timer.Text = left.ToString("0"); }

    Control Running()
    {
        var step = CheckEngine.Steps[Math.Min(stepIndex, CheckEngine.Steps.Length - 1)];
        var prompt = T.Label(step.Prompt, 28, null, FontWeight.SemiBold); prompt.TextWrapping = TextWrapping.Wrap;
        var bar = new Border { Height = 6, CornerRadius = new CornerRadius(3), Background = T.B("#22FFFFFF"), Child = new Border { Width = 640.0 * stepIndex / CheckEngine.Steps.Length, HorizontalAlignment = HorizontalAlignment.Left, CornerRadius = new CornerRadius(3), Background = T.B("#34D399") } }; bar.Width = 640;
        var stack = T.Stack(16, Parts.Caps($"STEP {stepIndex + 1} OF {CheckEngine.Steps.Length}"), prompt, timer, liveLabel, bar,
            T.Line(12, new Pill("Skip", Pill.Kinds.Ghost, () => skip = true) { Padding = new Thickness(26, 12) }, new Pill("Stop and save what we have", Pill.Kinds.Ghost, () => stop = true) { Padding = new Thickness(26, 12) }));
        Refresh();
        var card = T.Card(stack, 44, 24); card.MaxWidth = 760; card.HorizontalAlignment = HorizontalAlignment.Left; card.Margin = new Thickness(60, 16, 0, 20); return card;
    }

    Control Done()
    {
        var stack = T.Stack(10, T.Label("Scan complete", 38, null, FontWeight.Bold));
        foreach (var f in found) { var v = T.Stack(1, T.Label(f.Area, 15.5, null, FontWeight.SemiBold), T.Label(f.Value, 14.5, T.Text), T.Label(f.Detail, 12.5, T.Soft)); foreach (var t in v.Children.OfType<TextBlock>()) t.TextWrapping = TextWrapping.Wrap; stack.Children.Add(v); }
        if (archive.Length > 0) stack.Children.Add(T.Label("Saved: " + archive, 14, T.Soft));
        if (sendLine.Length > 0) stack.Children.Add(T.Label(sendLine, 14.5, sendLine.StartsWith("Sent") ? T.B("#34D399") : T.Warn));
        stack.Children.Add(T.Label("Please send the ZIP to @mistermajid on Discord. It helps add full support for your controller.", 14.5, T.AccentHi));
        stack.Children.Add(T.Line(12, new Pill("←  Back to Tools", Pill.Kinds.Ghost, back) { Padding = new Thickness(26, 12) }, new Pill("Scan again", Pill.Kinds.Ghost, () => { phase = Phase.Intro; Build(); }) { Padding = new Thickness(26, 12) }));
        foreach (var t in stack.Children.OfType<TextBlock>()) t.TextWrapping = TextWrapping.Wrap;
        var card = T.Card(stack, 44, 24); card.MaxWidth = 900; card.HorizontalAlignment = HorizontalAlignment.Left; card.Margin = new Thickness(60, 16, 0, 20); return card;
    }
}
