using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace X20Ctl.Product.Scanner;

/// <summary>Everything one Write scan learned. Holds no Bluetooth address, no PC name and no account: those are not needed to implement a pad.</summary>
public sealed class PadScanSession
{
    public string ChosenModel { get; set; } = "Unknown";
    public PadCheck.Raw? Raw { get; set; }
    public PadVerdict? Verdict { get; set; }
    public List<PadRecord> Records { get; } = [];
    public WriteBackResult? WriteBack { get; set; }
    public List<VisualResult> Visual { get; } = [];
    public X05Result? X05 { get; set; }
    public string? Failure { get; set; }
}

/// <summary>The scan as a research report for X20CTLADMIN (schema 2, kind "write-scan"), and the same files saved locally.</summary>
public static class PadScanReport
{
    public const string Name = "Write scan";
    public const int MaxZipBytes = 4_000_000, MaxFileBytes = 4 * 1024 * 1024;
    static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };
    static string? H(byte[]? b) => b is null ? null : Convert.ToHexString(b);

    public static string Summary(PadScanSession s)
    {
        var t = new StringBuilder();
        t.AppendLine("Write scan · asks the pad itself over Bluetooth");
        t.AppendLine(new string('─', 62));
        t.AppendLine($"  chosen model   {s.ChosenModel}");
        t.AppendLine($"  verdict        {s.Verdict?.Headline ?? "not run"}");
        foreach (var r in s.Verdict?.Reasons ?? []) t.AppendLine("    · " + r);
        if (s.Records.Count > 0) { t.AppendLine("  records read   " + s.Records.Count); t.Append(PadScan.Describe(s.Records)); }
        if (s.WriteBack is { } w)
        {
            t.AppendLine($"  write test     {w.Summary}");
            foreach (var i in w.Items) t.AppendLine($"    · {i.Name}: {i.Detail}");
            t.AppendLine($"  acknowledged   {(w.AllAcknowledged ? "yes, every record" : "not every record")}");
            t.AppendLine($"  identical      {(w.AllIdentical ? "yes, every record" : "no")}");
            t.AppendLine($"  after reconnect {(w.Persisted ? "still identical" : "not confirmed")}");
        }
        else t.AppendLine("  write test     not run");
        foreach (var v in s.Visual) t.AppendLine($"  visual · {v.Test}: {v.Outcome}" + (v.Note.Length > 0 ? " — " + v.Note : ""));
        if (s.X05 is { } x) t.AppendLine($"  X05 channel    {x.Status}; {x.After.Count} reports after the command, {x.ChangedBytes.Count} byte positions changed");
        if (s.Failure is not null) t.AppendLine("  stopped        " + s.Failure);
        return t.ToString();
    }

    public static string RecordsText(PadScanSession s) => string.Join("\n", s.Records.Select(r => $"{r.Name}\tread 0x{r.ReadOp:X2}\twrite 0x{r.WriteOp:X2}\t{PadScan.Hex(r.Bytes)}")) + "\n";

    public static List<(string Path, byte[] Data)> Files(PadScanSession s, string appVersion)
    {
        object device = new { claimedModel = s.ChosenModel, answer = s.Verdict?.Answer.ToString(), modelDetected = s.Verdict is { Answer: PadAnswer.LikeX20 or PadAnswer.LikeX20Pro }, keyLinker = s.Raw?.Identity is not null,
            identity = H(s.Raw?.Identity), capabilityMenu = H(s.Raw?.Menu), updaterId = H(s.Raw?.Guid0), updaterIdContinued = H(s.Raw?.Guid1) };
        object findings = new { verdict = s.Verdict?.Headline, reasons = s.Verdict?.Reasons, records = s.Records.Select(r => new { r.Name, readOp = r.ReadOp, writeOp = r.WriteOp, length = r.Bytes.Length, hex = PadScan.Hex(r.Bytes) }),
            writeTest = s.WriteBack is { } w ? new { w.Summary, acknowledged = w.AllAcknowledged, identical = w.AllIdentical, persisted = w.Persisted, items = w.Items } : null, failure = s.Failure, x05Probe = s.X05 };
        return
        [
            ("SUMMARY.txt", Encoding.UTF8.GetBytes(Summary(s))),
            ("device.json", JsonSerializer.SerializeToUtf8Bytes(device, Indented)),
            ("system.json", JsonSerializer.SerializeToUtf8Bytes(new { reportSchemaVersion = 2, collector = "x20ctl-write-scan", app = "X20CTL " + appVersion, os = Environment.OSVersion.VersionString }, Indented)),
            ("session.json", JsonSerializer.SerializeToUtf8Bytes(new { scan = Name, flow = "identify, read-back, write-back, visual tests", visual = s.Visual }, Indented)),
            ("experiments/write-scan/findings.json", JsonSerializer.SerializeToUtf8Bytes(findings, Indented)),
            ("experiments/write-scan/records.txt", Encoding.UTF8.GetBytes(RecordsText(s))),
        ];
    }

    /// <summary>The manifest lists each file with its size and SHA-256 and records that the owner reviewed and agreed to send it, which the receiver checks.</summary>
    public static byte[] Zip(PadScanSession s, string appVersion, IReadOnlyList<(string Path, byte[] Data)>? attachments = null)
    {
        var files = Files(s, appVersion);
        if (attachments is not null) files.AddRange(attachments);
        foreach (var f in files) if (f.Data.Length > MaxFileBytes) throw new InvalidDataException(f.Path + " is over 4 MB, too big to send.");
        var manifest = new { schemaVersion = 2, kind = "write-scan", consent = true, reviewed = true, rawTrace = false, withReadScan = attachments is { Count: > 0 },
            files = files.Select(f => new { path = f.Path, size = f.Data.Length, sha256 = Convert.ToHexString(SHA256.HashData(f.Data)).ToLowerInvariant() }).ToList() };
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
            foreach (var (path, data) in files.Prepend(("manifest.json", JsonSerializer.SerializeToUtf8Bytes(manifest, Indented))))
            { using var w = zip.CreateEntry(path, CompressionLevel.Optimal).Open(); w.Write(data); }
        if (stream.Length > MaxZipBytes) throw new InvalidDataException("The report is over 4 MB, too big to send.");
        return stream.ToArray();
    }

    /// <summary>Saves the files and the zip under <paramref name="root"/>; returns the folder.</summary>
    public static string Save(PadScanSession s, string root, string appVersion)
    {
        var folder = Path.Combine(root, DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        foreach (var (path, data) in Files(s, appVersion)) { var full = Path.Combine(folder, path.Replace('/', Path.DirectorySeparatorChar)); Directory.CreateDirectory(Path.GetDirectoryName(full)!); File.WriteAllBytes(full, data); }
        File.WriteAllBytes(Path.Combine(folder, "write-scan.zip"), Zip(s, appVersion));
        return folder;
    }
}
