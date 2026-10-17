using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
namespace X20Ctl.Product.Scanner;

/// <summary>
/// What a finished Read scan hands on to the Write scan (owner request 13 Oct 2026: after the Read scan, go on to the
/// Write scan, and send both reports online together). It is the compact report the Read scan already offers to send
/// (controller names, ids and results, no personal files), so the Write scan's send adds nothing the player hasn't
/// already seen described. The full evidence ZIP stays on this PC.
/// </summary>
public sealed record ReadReport(byte[] CompactZip, string ControllerName, string Transport, string? SavedTo, DateTime At)
{
    /// <summary>Where the files sit inside the Write scan's research ZIP (X20CTLADMIN schema 2 takes input/...).</summary>
    public const string Folder = "input/read-scan/";

    /// <summary>The report's files, ready to join the research ZIP. A file over the research limit is left out.</summary>
    public IReadOnlyList<(string Path, byte[] Data)> Files(int maxFileBytes)
    {
        var files = new List<(string, byte[])>();
        using var archive = new ZipArchive(new MemoryStream(CompactZip), ZipArchiveMode.Read);
        foreach (var entry in archive.Entries)
        {
            if (entry.Length == 0 || entry.Length > maxFileBytes || !Regex.IsMatch(entry.Name, @"^[A-Za-z0-9_.-]{1,80}\.(json|txt)$")) continue;
            using var stream = entry.Open(); using var copy = new MemoryStream(); stream.CopyTo(copy);
            files.Add((Folder + entry.Name, copy.ToArray()));
        }
        return files;
    }

    public string Describe() => $"{(string.IsNullOrWhiteSpace(ControllerName) ? "a controller" : ControllerName)} over {(string.IsNullOrWhiteSpace(Transport) ? "an unknown connection" : Transport)}, scanned {At.ToLocalTime():HH:mm}";
}
