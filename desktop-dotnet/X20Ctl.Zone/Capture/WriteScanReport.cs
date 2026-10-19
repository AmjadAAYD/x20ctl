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

/// <summary>One thing the verdict rests on, good or bad, in plain words.</summary>
public sealed record VerdictCheck(bool Passed, string Text);

/// <summary>
/// What a Write scan learned, and how sure it is. "High" needs the app to have changed one setting and changed it back,
/// with a second sign that it was the owner's change: the bytes hold the values they typed, or the two clocks put the
/// writes on the steps where they made them. Fixes say how to get a better recording when it falls short.
/// </summary>
public sealed record WriteScanVerdict(string Level, string Headline, IReadOnlyList<VerdictCheck> Checks, IReadOnlyList<string> Fixes);

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
    /// <summary>Seconds to take off the phone's clock to get this PC's (0, or the time zone when the phone logged local
    /// time); NaN when the log has no clock or it doesn't line up with the test.</summary>
    public double ClockShift { get; init; } = double.NaN;
    /// <summary>Packets the phone saved only the start of. Any at all means the log is Android's "Filtered" kind, which
    /// cuts the app's messages short so none of them can be decoded.</summary>
    public int CutPackets { get; init; }

    public bool IsKeyLinker => KeyLinker.Frames.Count > 0;
    public bool Filtered => CutPackets > 0 || Source.Contains(".filtered", StringComparison.OrdinalIgnoreCase);
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
        var (warning, shift) = Clock(records, noted, kl);
        return new()
        {
            Plan = plan, Steps = noted, Source = source, AclPackets = all.AclPackets, Messages = trace.Messages.Count,
            KeyLinker = kl, Fingerprint = print, Trace = trace, Handles = handles, Records = records, ConnectionByFrames = byFrames,
            ClockWarning = warning, ClockShift = shift, CutPackets = records.Count(r => r.Cut),
            VersusX20 = print.Identity is null && print.Descriptor is null ? [] : FingerprintCompare.Compare(Fingerprint.X20Reference, print),
            Shapes = kl.Frames.Count > 0 ? [] : WriteShapes(trace),
        };
    }

    static double Unix(DateTime utc) => new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeMilliseconds() / 1000.0;

    /// <summary>
    /// When the log carries the phone's own clock, whether it covers the moment the owner said they made the change. A
    /// bug report or sysdiagnose can hold an older log, and the PC and phone clocks are both normally set from the
    /// network, so a gap of more than two minutes is worth saying. Some phones log local time rather than UTC, so the
    /// PC's own time zone is tried too (the phone is almost always in the same one); of the two, the one that puts more
    /// of the app's writes inside the test wins.
    /// </summary>
    static (string? Warning, double Shift) Clock(List<HciRecord> records, List<StepNote> steps, KeyLinkerAnalysis kl)
    {
        var change = steps.FirstOrDefault(n => n.Event == "make the change");
        if (change is null || records.Count == 0 || double.IsNaN(records[0].Unix) || double.IsNaN(records[^1].Unix)) return (null, double.NaN);
        double at = Unix(change.Utc), from = records[0].Unix, to = records[^1].Unix, zero = records[0].Unix - records[0].Seconds;
        double begin = Unix(steps[0].Utc) - 10, end = Unix(steps[^1].Utc) + 10;
        double local = TimeZoneInfo.Local.GetUtcOffset(change.Utc).TotalSeconds;
        var fits = (local == 0 ? new[] { 0.0 } : [0.0, local])
            .Where(s => at >= from - s - 120 && at <= to - s + 120)
            .Select(s => (Shift: s, Inside: kl.Changes.Count(c => zero - s + c.Seconds is var t && t >= begin && t <= end)))
            .OrderByDescending(x => x.Inside).ToList();
        if (fits.Count > 0) return (null, fits[0].Shift);
        static string T(double unix) => DateTimeOffset.FromUnixTimeMilliseconds((long)(unix * 1000)).ToLocalTime().ToString("d MMM HH:mm");
        return ($"The phone's log runs from {T(from)} to {T(to)}, but the change was made at {T(at)} by this PC's clock. This may be an older log, or one of the clocks is wrong.", double.NaN);
    }

    /// <summary>The step of the test the owner was on when the phone logged this moment (seconds into the log), or null
    /// when the clocks can't say. A step's note is made when the owner confirms it, so a moment belongs to the first step
    /// confirmed after it; three seconds of slack allow for the two clocks differing and the app saving just after a tap.</summary>
    public string? StepAt(double seconds)
    {
        if (double.IsNaN(ClockShift) || Records.Count == 0 || Steps.Count == 0) return null;
        double t = Records[0].Unix - Records[0].Seconds - ClockShift + seconds;
        if (t < Unix(Steps[0].Utc) - 3) return "before the test";
        var step = Steps.FirstOrDefault(n => Unix(n.Utc) >= t - 3);
        return step is null ? "after the test" : step.Event;
    }

    /// <summary>
    /// The verdict, worked out from the recording alone. First the recording itself (whole packets, the right time,
    /// KeyLinker in it), then the change: the strongest sign is a round trip, the app writing a setting and then writing
    /// it back the way it was, which is what the test asks the owner to do. The bytes that differ between those two
    /// writes are the setting.
    /// </summary>
    public WriteScanVerdict Verdict()
    {
        var checks = new List<VerdictCheck>(); var fixes = new List<string>();
        void Good(string text) => checks.Add(new(true, text));
        void Bad(string text, string? fix = null) { checks.Add(new(false, text)); if (fix is not null && !fixes.Contains(fix)) fixes.Add(fix); }
        const string Again = "Do the test again, then make a new bug report straight after it.";

        // ---- the recording ----
        if (Filtered)
            Bad(CutPackets > 0 ? $"{CutPackets} of {AclPackets} packets were saved cut short. This is the phone's Filtered log: it keeps only the start of each message."
                    : "This is the phone's Filtered log, which keeps only the start of each message.",
                "In Developer options, set \"Bluetooth HCI snoop log\" to Enabled (or Full), not Filtered. Switch Bluetooth off and on, then do the test again.");
        else Good("Every packet was saved whole.");
        if (ClockWarning is not null) Bad(ClockWarning, Again);
        else if (!double.IsNaN(ClockShift)) Good("The log covers the moment you made the change.");
        // a Filtered log's messages are cut short and so dropped; that says nothing more about the app
        if (Filtered) { }
        else if (Messages == 0) Bad("No app talked to any Bluetooth device during this log.", "Turn the snoop log on, then switch Bluetooth off and on before the test: the log only starts when Bluetooth does.");
        else if (!IsKeyLinker) Bad("Nothing in it decodes as KeyLinker. The app may speak another protocol, or the controller wasn't connected through the app during the log.",
            "Make sure the controller connects in the app during the test (step 1), after the log was turned on.");
        if (IsKeyLinker) Good($"{KeyLinker.Frames.Count} KeyLinker messages were decoded.");

        // ---- the change ----
        var real = KeyLinker.Changes.Where(c => c.Opcode is >= 0x31 and <= 0x37).ToList();
        if (IsKeyLinker && real.Count == 0)
            Bad("The app wrote no settings during the log.", "Make sure the change is saved: some apps write only after Save or Apply, or when you leave the page.");
        if (!IsKeyLinker)
        {
            string head = Filtered ? "Nothing learned: the log is the cut-down Filtered kind" : ClockWarning is not null ? "Nothing learned: this log is from another time"
                : Messages == 0 ? "Nothing learned: the log holds no app traffic" : Shapes.Count > 0 ? $"Not KeyLinker: {Shapes.Count} kinds of write found, listed below" : "Nothing learned from this recording";
            return new(Shapes.Count > 0 && !Filtered && ClockWarning is null ? "Low" : "None", head, checks, fixes);
        }
        if (real.Count == 0) return new("None", "Nothing learned: the app wrote no settings", checks, fixes);

        // a round trip: a write, then a later write of the same setting that differs from it, best when it puts back
        // what the controller held before the first
        var trip = (from i in Enumerable.Range(0, real.Count)
                    from j in Enumerable.Range(i + 1, real.Count - i - 1)
                    let a = real[i] let b = real[j]
                    where a.Setting == b.Setting && a.After != b.After && a.After.Length == b.After.Length
                    select ((SettingChange A, SettingChange B, bool Restored)?)(a, b, a.Before.Length > 0 && Restores(Convert.FromHexString(b.After), Convert.FromHexString(a.Before))))
            .OrderByDescending(p => p!.Value.Restored).FirstOrDefault();
        var first = trip?.A ?? real.FirstOrDefault(c => c.Bytes.Count > 0) ?? real[0];
        // the bytes learned: against the controller's own record when the recording read it, else between the two writes
        var learned = first.Bytes.Count > 0 ? first.Bytes.ToList() : trip is { } t0 ? Between(t0.A, t0.B) : [];
        var others = real.Where(c => c.Setting != first.Setting && c.Bytes.Count > 0).Select(c => c.Setting).Distinct().ToList();

        if (trip is { } t)
        {
            if (t.Restored) Good($"The app changed {t.A.Setting} and then put it back exactly as it was: the change and the change back.");
            else Good($"The app wrote {t.A.Setting} twice with different values: the change and the change back.");
        }
        else Bad("There is no change back in the log, only " + (real.Count == 1 ? "one write" : "writes that don't undo each other") + ".",
            "Do every step of the test, including Change it back: the change back is what proves which bytes are the setting.");

        bool values = learned.Count == 1 && Number(Plan.OriginalValue) is int was && Number(Plan.TestValue) is int now && learned[0].Before == was && learned[0].After == now;
        if (values) Good($"The byte holds the very values you typed: {learned[0].Before} then {learned[0].After}.");

        bool onSteps = false;
        if (StepAt(first.Seconds) is { } at)
        {
            string? back = trip is { } tb ? StepAt(tb.B.Seconds) : null;
            onSteps = at == "make the change" && (trip is null || back == "change it back");
            if (onSteps) Good(trip is null ? "The write came while you were on Make the change." : "The writes came while you were on Make the change and Change it back.");
            else Bad("By the two clocks " + (back is null ? $"the write came {Where(at)}" : back == at ? $"both writes came {Where(at)}" : $"the write came {Where(at)} and the change back {Where(back)}")
                    + ", not on the steps where you made the change.",
                "Wait for each countdown before the next step, so each change stands apart.");
        }
        if (first.Answered) Good("The controller replied to the write.");
        if (first.ReadBack == "matches") Good("A later read held what was written: the controller took it.");
        if (others.Count > 0) Bad("Other settings changed too: " + string.Join(", ", others) + ".", "Change only one setting per recording.");
        if (learned.Count == 0) Bad("The writes hold the same bytes as the controller's record, so no byte stands out.", "Pick a value far from the current one, for example 20 to 80, so the bytes differ clearly.");

        // a log from another time may be an earlier try, or someone else's test: never more than Medium
        string level = learned.Count == 0 ? "Low"
            : trip is not null && (values || onSteps) && others.Count == 0 && ClockWarning is null ? "High"
            : trip is not null || values || onSteps || first.ReadBack == "matches" ? "Medium" : "Low";
        if (level != "High" && fixes.Count == 0) fixes.Add("Compare with a second recording of the same test: the bytes that change in both are the setting.");
        string bytes = string.Join(", ", learned.Take(4).Select(b => $"byte {b.Offset}: {b.Before} → {b.After}")) + (learned.Count > 4 ? $" and {learned.Count - 4} more" : "");
        return new(level, learned.Count == 0 ? $"{first.Setting} was written, but no byte changed" : $"{first.Setting}, {bytes}", checks, fixes);
    }

    static string Where(string step) => step is "before the test" or "after the test" ? step : step.EndsWith(" ready") ? "while you prepared the phone" : $"on {char.ToUpperInvariant(step[0])}{step[1..]}";
    static int? Number(string text) => System.Text.RegularExpressions.Regex.Match(text, @"\d+") is { Success: true } m && int.TryParse(m.Value, out int n) && n <= 255 ? n : null;

    /// <summary>Whether a write puts back a record the controller reported, allowing for the size byte one of them carries.</summary>
    static bool Restores(byte[] written, byte[] record) =>
        written.Length == record.Length ? written.AsSpan().SequenceEqual(record)
        : written.Length == record.Length + 1 ? written.AsSpan(1).SequenceEqual(record)
        : written.Length + 1 == record.Length && written.AsSpan().SequenceEqual(record.AsSpan(1));

    /// <summary>The bytes that differ between two writes of the same length, in the write's own layout.</summary>
    static List<ByteChange> Between(SettingChange a, SettingChange b)
    {
        byte[] x = Convert.FromHexString(a.After), y = Convert.FromHexString(b.After);
        return Enumerable.Range(0, x.Length).Where(i => x[i] != y[i]).Select(i => new ByteChange(i, y[i], x[i])).ToList();
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
        var verdict = Verdict();
        H("Verdict");
        s.AppendLine(verdict.Level == "None" ? "  " + verdict.Headline + "." : $"  {verdict.Level} confidence. {verdict.Headline}.");
        foreach (var c in verdict.Checks) s.AppendLine($"  {(c.Passed ? "✓" : "✕")} {c.Text}");
        if (verdict.Fixes.Count > 0) { s.AppendLine("  For a better recording:"); foreach (var f in verdict.Fixes) s.AppendLine("  - " + f); }
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
            s.AppendLine($"  {++n}. {c.Setting} at {c.Seconds:0.0} s" + (StepAt(c.Seconds) is { } step ? $" ({Where(step)})" : "") + ": " + (c.Bytes.Count > 0
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
        clockShift = double.IsNaN(ClockShift) ? (double?)null : ClockShift, cutPackets = CutPackets, verdict = Verdict(),
        keyLinkerFrames = KeyLinker.Frames.Count, fingerprint = Fingerprint, versusX20 = VersusX20, notRecorded = NotRecorded, shapes = Shapes,
        changes = KeyLinker.Changes, changeSteps = KeyLinker.Changes.Select(c => StepAt(c.Seconds)).ToList(),
        unexplainedBits = KeyLinker.Descriptor is null ? new List<string>() : Descriptor.Unexplained(KeyLinker.Descriptor),
    }, Indented);

    /// <summary>The receiver's per-file and whole-archive limits for research reports (schema 2).</summary>
    public const int MaxResearchFileBytes = 4 * 1024 * 1024, MaxResearchZipBytes = 4_000_000;

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
        if (stream.Length > MaxResearchZipBytes) throw new InvalidDataException("The report is over 4 MB, too big to send. Send without the Bluetooth log.");
        return stream.ToArray();
    }
}
