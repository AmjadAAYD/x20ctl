using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace X20Ctl.Zone.Capture;

/// <summary>What the owner said they would test.</summary>
public sealed record TestPlan(string Model, string App, string Setting, string OriginalValue, string TestValue);

/// <summary>A step the owner confirmed, with the time this PC saw it.</summary>
public sealed record StepNote(string Event, DateTime Utc);

/// <summary>Writes of the same size to the same place whose bytes differ, for recordings that aren't KeyLinker.</summary>
public sealed record ShapeGroup(string Where, int Length, int Count, IReadOnlyList<string> Varying);

/// <summary>
/// The result of a Write scan: a recording of the official app changing one setting, read back into plain findings.
/// X20CTL sent nothing to the controller; the owner made the change in the official app and only the phone's own
/// Bluetooth log is read here. The same recording can be compared with a second one.
/// </summary>
public sealed class WriteScanReport
{
    public const string Name = "Write scan";

    public TestPlan Plan { get; init; } = new("", "", "", "", "");
    public IReadOnlyList<StepNote> Steps { get; init; } = [];
    public string Source { get; init; } = "";
    public int AclPackets { get; init; }
    public int Messages { get; init; }
    public KeyLinkerAnalysis KeyLinker { get; init; } = new();
    public Fingerprint Fingerprint { get; init; } = new(null, null, new Dictionary<string, IReadOnlyList<int>>());
    public IReadOnlyList<Difference> VersusX20 { get; init; } = [];
    public IReadOnlyList<ShapeGroup> Shapes { get; init; } = [];
    public IReadOnlyList<string> Handles { get; init; } = [];
    public AttTrace Trace { get; init; } = new();
    /// <summary>The recording's ACL packets, kept so the trace can be shared without the HCI events around it.</summary>
    public IReadOnlyList<HciRecord> Records { get; init; } = [];
    /// <summary>True when KeyLinker frames picked the controller's connection; false when it is the phone's busiest
    /// connection, which is a guess.</summary>
    public bool ConnectionByFrames { get; init; }
    /// <summary>Set when the phone's log doesn't cover the moment the owner made the change, by the two clocks.</summary>
    public string? ClockWarning { get; init; }

    public bool IsKeyLinker => KeyLinker.Frames.Count > 0;
    /// <summary>Differences from the X20 in what both sides report; a list only one side holds is not a difference.</summary>
    public IReadOnlyList<Difference> Differences => VersusX20.Where(d => d.Meaning != "missing on one side").ToList();
    /// <summary>The X20's lists this recording doesn't hold: the app didn't ask for them, or asked before the log started.</summary>
    public IReadOnlyList<string> NotRecorded => VersusX20.Where(d => d.Meaning == "missing on one side" && d.B == "—").Select(d => d.Field).ToList();

    public static WriteScanReport Build(TestPlan plan, IEnumerable<StepNote> steps, List<HciRecord> records, string source)
    {
        var all = AttTrace.From(records);
        var (connection, byFrames) = all.ControllerConnection();
        // everything below is about the controller's connection only; the phone's other devices are left out
        var trace = all.Connections.Count > 1 ? all.Only(connection) : all;
        var kl = KeyLinkerAnalysis.From(all);
        var print = kl.ToFingerprint();
        var handles = trace.Messages.Select(m => m.Handle).Distinct().OrderBy(h => h).Select(trace.Name).ToList();
        var noted = steps.ToList();
        return new()
        {
            Plan = plan, Steps = noted, Source = source, AclPackets = all.AclPackets, Messages = trace.Messages.Count,
            KeyLinker = kl, Fingerprint = print, Trace = trace, Handles = handles, Records = records, ConnectionByFrames = byFrames,
            ClockWarning = Clock(records, noted),
            VersusX20 = print.Identity is null && print.Descriptor is null ? [] : FingerprintCompare.Compare(Fingerprint.X20Reference, print),
            Shapes = kl.Frames.Count > 0 ? [] : WriteShapes(trace),
        };
    }

    /// <summary>
    /// When the log carries the phone's own clock, whether it covers the moment the owner said they made the change. A
    /// sysdiagnose can hold an older log, and the PC and phone clocks are both normally set from the network, so a gap
    /// of more than two minutes is worth saying.
    /// </summary>
    static string? Clock(List<HciRecord> records, List<StepNote> steps)
    {
        var change = steps.FirstOrDefault(n => n.Event == "make the change");
        if (change is null || records.Count == 0 || double.IsNaN(records[0].Unix)) return null;
        double at = new DateTimeOffset(DateTime.SpecifyKind(change.Utc, DateTimeKind.Utc)).ToUnixTimeMilliseconds() / 1000.0;
        double from = records[0].Unix, to = records[^1].Unix;
        if (at >= from - 120 && at <= to + 120) return null;
        static string T(double unix) => DateTimeOffset.FromUnixTimeMilliseconds((long)(unix * 1000)).ToLocalTime().ToString("d MMM HH:mm");
        return $"The phone's log runs from {T(from)} to {T(to)}, but the change was made at {T(at)} by this PC's clock. This may be an older log, or one of the clocks is wrong.";
    }

    /// <summary>For a recording that isn't KeyLinker: group the app's writes by place and size, and list which bytes vary between them.</summary>
    public static List<ShapeGroup> WriteShapes(AttTrace trace)
    {
        var groups = new List<ShapeGroup>();
        foreach (var g in trace.Messages.Where(m => m.FromPhone && m.Kind is AttKind.Write or AttKind.WriteCommand).GroupBy(m => (m.Handle, m.Value.Length)))
        {
            var rows = g.ToList();
            if (rows.Count < 2) continue;
            var varying = new List<string>();
            for (int i = 0; i < g.Key.Length; i++)
            {
                var seen = rows.Select(r => r.Value[i]).Distinct().ToList();
                if (seen.Count > 1) varying.Add($"byte {i}: " + string.Join(" ", seen.Take(6).Select(b => b.ToString("x2"))) + (seen.Count > 6 ? " …" : ""));
            }
            groups.Add(new(trace.Name(g.Key.Handle), g.Key.Length, rows.Count, varying));
        }
        return groups.OrderByDescending(g => g.Count).ToList();
    }

    /// <summary>Two recordings of the same app: which decoded messages only one of them holds.</summary>
    public static (List<string> OnlyA, List<string> OnlyB) CompareRecordings(KeyLinkerAnalysis a, KeyLinkerAnalysis b)
    {
        static HashSet<string> Set(KeyLinkerAnalysis x) => x.Frames.Where(f => f.Frame.Opcode != 0x19)
            .Select(f => $"{f.Frame.Opcode:X2} {KeyLinkerAnalysis.Hex(f.Frame.Payload)}").ToHashSet();
        var sa = Set(a); var sb = Set(b);
        string Pretty(string s) => $"{KeyLinkerAnalysis.Describe(Convert.ToByte(s[..2], 16))}  {s[3..]}";
        return (sa.Except(sb).Take(80).Select(Pretty).ToList(), sb.Except(sa).Take(80).Select(Pretty).ToList());
    }

    public string Summary()
    {
        var s = new StringBuilder();
        void H(string text) { s.AppendLine(); s.AppendLine(text.ToUpperInvariant()); }
        s.AppendLine(Name + " — " + DateTime.Now.ToString("d MMM yyyy HH:mm"));
        s.AppendLine("X20CTL sent nothing to the controller. The recording is the phone's own Bluetooth log.");
        H("What was tested");
        s.AppendLine($"  Controller (the owner's claim): {Show(Plan.Model)}");
        s.AppendLine($"  App: {Show(Plan.App)}");
        s.AppendLine($"  Setting: {Show(Plan.Setting)}, from {Show(Plan.OriginalValue)} to {Show(Plan.TestValue)}");
        H("The recording");
        s.AppendLine($"  {Source}: {AclPackets} Bluetooth packets, {Messages} messages, {KeyLinker.Frames.Count} of them KeyLinker frames.");
        if (KeyLinker.OtherConnections > 0)
            s.AppendLine($"  The phone had {KeyLinker.OtherConnections} other Bluetooth connection{(KeyLinker.OtherConnections == 1 ? "" : "s")} open (its other devices). Only the controller's is read, and only it is ever shared.");
        if (ClockWarning is not null) s.AppendLine("  " + ClockWarning);
        if (!IsKeyLinker)
        {
            s.AppendLine("  Nothing in it decodes as the KeyLinker protocol. On its own that is inconclusive: the app may speak another protocol, or the log may be partial or filtered, or taken before or after the test.");
            if (KeyLinker.OtherConnections > 0 && !ConnectionByFrames) s.AppendLine("  The connection read is the one the phone wrote to most; that it is the controller's is a guess.");
            if (Handles.Count > 0) s.AppendLine("  Places the app wrote to or heard from: " + string.Join(", ", Handles.Take(12)));
            foreach (var g in Shapes.Take(12))
                s.AppendLine($"  {g.Count} writes of {g.Length} bytes to {g.Where}" + (g.Varying.Count > 0 ? "; these bytes differ: " + string.Join("; ", g.Varying.Take(8)) : "; all identical"));
            return s.ToString();
        }
        var id = Fingerprint.Identity;
        H("Who the controller says it is");
        if (id is null) s.AppendLine("  The recording doesn't hold the controller's identity reply (the app may have kept it from an earlier connection).");
        else
        {
            s.AppendLine($"  Vendor id {id.Vid}, product id {id.Pid}, firmware {id.Version} (these are the vendor's own ids, not USB ids).");
            s.AppendLine(id.Mode is null ? "  The record has no flags byte, so mode and generation are unknown."
                : $"  Operating mode {id.Mode}, sensor {id.Sensor}, generation {id.RawGeneration} (the app uses {id.EffectiveGeneration}).");
        }
        H("What it says it offers");
        if (KeyLinker.Descriptor is null) s.AppendLine("  The recording doesn't hold the capability reply.");
        else
        {
            s.AppendLine("  " + (Descriptor.Offers(KeyLinker.Descriptor) is { Count: > 0 } o ? string.Join(", ", o) : "nothing named"));
            var u = Descriptor.Unexplained(KeyLinker.Descriptor);
            s.AppendLine("  Set but not yet understood: " + (u.Count > 0 ? string.Join(", ", u) : "none"));
            s.AppendLine("  Raw descriptor: " + KeyLinkerAnalysis.Hex(KeyLinker.Descriptor));
        }
        H("How it compares with the recorded X20");
        if (Differences.Count == 0) s.AppendLine(id is null && KeyLinker.Descriptor is null ? "  Not enough in the recording to compare." : "  No differences in anything both report.");
        foreach (var d in Differences.Take(40)) s.AppendLine($"  [{d.Area}] {d.Field}: X20 {d.A}, this one {d.B}" + (d.Meaning.Length > 0 ? $"  ({d.Meaning})" : ""));
        if (NotRecorded.Count > 0) s.AppendLine("  Not in this recording, so not compared: " + string.Join(", ", NotRecorded) + ".");
        H("What the app changed");
        if (KeyLinker.Changes.Count == 0) s.AppendLine("  The app wrote no settings during this recording.");
        int n = 0;
        foreach (var c in KeyLinker.Changes)
        {
            s.AppendLine($"  {++n}. {c.Setting} at {c.Seconds:0.0} s: " + (c.Bytes.Count > 0
                ? string.Join(", ", c.Bytes.Select(b => $"byte {b.Offset}: {b.Before:x2} → {b.After:x2}")) : "no byte differs"));
            s.AppendLine("     " + c.Note);
            s.AppendLine("     " + (c.Answered ? "The controller replied to it. " : "No reply to it in the recording. ") + c.ReadBack switch
            {
                "matches" => "A later read held what was written: the controller took it.",
                "differs" => "A later read did not hold what was written: the controller may not have kept it.",
                "unclear" => "A later read of it came back, but its layout doesn't line up with the write, so whether it held it isn't known.",
                _ => "The app didn't read it back, so whether the controller kept it isn't known.",
            });
        }
        return s.ToString();
    }

    static string Show(string v) => string.IsNullOrWhiteSpace(v) ? "not given" : v.Trim();

    /// <summary>Saves the findings in a new folder under <paramref name="root"/> and returns its path. The raw recording is copied only if asked.</summary>
    public string Save(string root, byte[]? rawRecording = null, string? second = null, string rawExtension = "btsnoop")
    {
        var folder = Path.Combine(root, "write-scan-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "summary.txt"), Summary() + (second is null ? "" : "\n" + second), Encoding.UTF8);
        File.WriteAllLines(Path.Combine(folder, "timeline.txt"), KeyLinker.Timeline(), Encoding.UTF8);
        File.WriteAllText(Path.Combine(folder, "messages.csv"), MessagesCsv(), Encoding.UTF8);
        File.WriteAllText(Path.Combine(folder, "findings.json"), FindingsJson(), Encoding.UTF8);
        if (rawRecording is not null) File.WriteAllBytes(Path.Combine(folder, "recording." + rawExtension), rawRecording);
        File.WriteAllText(Path.Combine(folder, "README.txt"),
            "Written by X20CTL's Write scan. Nothing here was uploaded.\n" +
            "summary.txt is the plain findings. messages.csv holds every message in full: it can include the controller's name and an id, so look before you share it.\n" +
            (rawRecording is null ? "The raw recording was not copied.\n" : $"recording.{rawExtension} is a copy of the phone's Bluetooth log. It can hold addresses and unrelated traffic.\n"), Encoding.UTF8);
        return folder;
    }

    static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    public string MessagesCsv()
    {
        var csv = new StringBuilder("seconds,direction,kind,place,length,hex\n");
        foreach (var m in Trace.Messages)
            csv.Append(m.Seconds.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)).Append(',').Append(m.FromPhone ? "phone-to-pad" : "pad-to-phone").Append(',')
               .Append(m.Kind).Append(',').Append(Trace.Name(m.Handle)).Append(',').Append(m.Value.Length).Append(',').Append(Convert.ToHexString(m.Value).ToLowerInvariant()).Append('\n');
        return csv.ToString();
    }

    public string FindingsJson() => JsonSerializer.Serialize(new
    {
        scan = Name, plan = Plan, steps = Steps, source = Source, aclPackets = AclPackets, messages = Messages,
        connection = KeyLinker.Connection, connectionByFrames = ConnectionByFrames, otherConnections = KeyLinker.OtherConnections, clockWarning = ClockWarning,
        keyLinkerFrames = KeyLinker.Frames.Count, fingerprint = Fingerprint, versusX20 = VersusX20, notRecorded = NotRecorded, shapes = Shapes,
        changes = KeyLinker.Changes, unexplainedBits = KeyLinker.Descriptor is null ? new List<string>() : Descriptor.Unexplained(KeyLinker.Descriptor),
    }, Indented);

    /// <summary>The receiver's per-file and whole-archive limits for research reports (schema 2).</summary>
    public const int MaxResearchFileBytes = 4 * 1024 * 1024, MaxResearchZipBytes = 8 * 1024 * 1024;

    /// <summary>
    /// The trace that would be shared: the controller's connection only, and only from just before its first KeyLinker
    /// frame to just after its last. No HCI events, so none of the device names and addresses they carry, and none of the
    /// phone's other devices. Re-packing is not anonymising: what the controller and the app said is all still there.
    /// </summary>
    public byte[] SharedTrace() => BtSnoopWriter.Write(SharedRecords());

    public List<HciRecord> SharedRecords()
    {
        if (KeyLinker.Connection < 0) return [];
        var mine = Records.Where(r => r.Connection == KeyLinker.Connection);
        if (KeyLinker.Frames.Count > 0)
        {
            double from = KeyLinker.Frames[0].Seconds - 2, to = KeyLinker.Frames[^1].Seconds + 2;
            mine = mine.Where(r => r.Seconds >= from && r.Seconds <= to);
        }
        return mine.ToList();
    }

    /// <summary>
    /// The Write scan as a research report (X20CTLADMIN schema 2): the summary, the findings, the timeline and every
    /// decoded message, and the trace only when <paramref name="includeTrace"/> is set. A manifest lists each file with its
    /// size and SHA-256 and records that the owner reviewed and agreed to send it, which the receiver checks.
    /// </summary>
    public byte[] ResearchZip(bool includeTrace, string appVersion, string? second = null, IReadOnlyList<(string Path, byte[] Data)>? attachments = null)
    {
        var files = new List<(string Path, byte[] Data)>
        {
            ("SUMMARY.txt", Encoding.UTF8.GetBytes(Summary() + (second is null ? "" : "\n" + second))),
            ("device.json", JsonSerializer.SerializeToUtf8Bytes(new { claimedModel = Plan.Model, modelDetected = false, fingerprint = Fingerprint, keyLinker = IsKeyLinker }, Indented)),
            ("system.json", JsonSerializer.SerializeToUtf8Bytes(new { reportSchemaVersion = 2, collector = "x20ctl-write-scan", app = "X20CTL " + appVersion, os = Environment.OSVersion.VersionString }, Indented)),
            ("session.json", JsonSerializer.SerializeToUtf8Bytes(new { scan = Name, plan = Plan, steps = Steps, source = Source, aclPackets = AclPackets, messages = Messages, keyLinkerFrames = KeyLinker.Frames.Count }, Indented)),
            ("experiments/write-scan/findings.json", Encoding.UTF8.GetBytes(FindingsJson())),
            ("experiments/write-scan/timeline.txt", Encoding.UTF8.GetBytes(string.Join("\n", KeyLinker.Timeline()) + "\n")),
            ("experiments/write-scan/messages.txt", Encoding.UTF8.GetBytes(MessagesCsv())),
        };
        if (includeTrace) files.Add(("ble/recording.btsnoop", SharedTrace()));
        // another report that travels with this one (the Read scan the owner just finished), as plain files under input/
        if (attachments is not null) files.AddRange(attachments);
        foreach (var f in files)
            if (f.Data.Length > MaxResearchFileBytes)
                throw new InvalidDataException(f.Path.StartsWith("ble/") ? "The Bluetooth log is over 4 MB, too big to send. Send without it, or record a shorter session." : f.Path + " is over 4 MB, too big to send.");
        var manifest = new
        {
            schemaVersion = 2, kind = "write-scan", consent = true, reviewed = true, rawTrace = includeTrace, withReadScan = attachments is { Count: > 0 },
            files = files.Select(f => new { path = f.Path, size = f.Data.Length, sha256 = Convert.ToHexString(SHA256.HashData(f.Data)).ToLowerInvariant() }).ToList(),
        };
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
            foreach (var (path, data) in files.Prepend(("manifest.json", JsonSerializer.SerializeToUtf8Bytes(manifest, Indented))))
            {
                using var w = zip.CreateEntry(path, CompressionLevel.Optimal).Open();
                w.Write(data);
            }
        if (stream.Length > MaxResearchZipBytes) throw new InvalidDataException("The report is over 8 MB, too big to send. Send without the Bluetooth log.");
        return stream.ToArray();
    }
}
