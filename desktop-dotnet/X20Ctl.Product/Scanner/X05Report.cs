namespace X20Ctl.Product.Scanner;

/// <summary>What the X05 Pro D-Input probe saw. Hex strings of whole input reports; nothing is decoded.</summary>
public sealed record X05Result(string Status, string Sent, IReadOnlyList<string> Before, IReadOnlyList<string> After, IReadOnlyList<int> ChangedBytes);

/// <summary>
/// The EasySMX X05 Pro in D-Input mode (hold Back + Start): USB 2345:E084, a vendor channel on usage page 0xFFA0 with a 64-byte
/// input report (ID 0x04) and a 64-byte output report (ID 0x05). Reported by SalimKr in issue #64, not yet seen here. The probe
/// sends the one command he reported, 05 01 00…, once. A changed input stream shows the pad listens, not what the command means.
/// </summary>
public static class X05Report
{
    public const int Vid = 0x2345, Pid = 0xE084, UsagePage = 0xFFA0, ReportLength = 64;

    /// <summary>Output report 0x05, then 01, then zeros: the command from the issue, as is.</summary>
    public static byte[] Command() { var b = new byte[ReportLength]; b[0] = 0x05; b[1] = 0x01; return b; }

    /// <summary>The byte positions that differ between the last report before the command and any report after it.</summary>
    public static IReadOnlyList<int> Changed(byte[]? before, IReadOnlyList<byte[]> after)
    {
        if (before is null) return [];
        var set = new SortedSet<int>();
        foreach (var report in after) for (int i = 0; i < Math.Min(before.Length, report.Length); i++) if (before[i] != report[i]) set.Add(i);
        return [.. set];
    }

    public static X05Result Summarize(IReadOnlyList<byte[]> before, IReadOnlyList<byte[]> after, string status) =>
        new(status, Convert.ToHexString(Command()), before.Select(Convert.ToHexString).ToList(), after.Select(Convert.ToHexString).ToList(), Changed(before.LastOrDefault(), after));
}
