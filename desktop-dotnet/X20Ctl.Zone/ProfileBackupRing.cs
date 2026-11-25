using System.Text.Json;

namespace X20Ctl.Zone;

public sealed record ProfileBackupSnapshot(
    string Id,
    DateTimeOffset CreatedUtc,
    string Reason,
    string PcModeJson,
    string? AutomationJson = null,
    string? HapticsJson = null);

public sealed record ProfileBackupEntry(
    string Id,
    DateTimeOffset CreatedUtc,
    string Reason,
    int ByteLength);

/// <summary>
/// Rolling 10-snapshot automatic backup ring stored under <c>backups/</c> (or Portable <c>./data/backups/</c>).
/// Captures atomic pre-edit/pre-reset/pre-import state without losing historical profiles.
/// </summary>
public sealed class ProfileBackupRing
{
    public const int MaxSnapshots = 10;

    static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    readonly string directory;
    readonly object gate = new();

    public ProfileBackupRing(string? directory = null)
    {
        this.directory = string.IsNullOrWhiteSpace(directory)
            ? AppDataPaths.Roaming("backups")
            : Path.GetFullPath(directory);
    }

    public ProfileBackupEntry CreateSnapshot(
        string reason,
        string pcModeJson,
        string? automationJson = null,
        string? hapticsJson = null,
        DateTimeOffset? timestampUtc = null)
    {
        var utc = timestampUtc ?? DateTimeOffset.UtcNow;
        string cleanReason = string.IsNullOrWhiteSpace(reason) ? "Manual snapshot" : reason.Trim();
        string id = $"{utc:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid().ToString("N")[..6]}";
        var snap = new ProfileBackupSnapshot(id, utc, cleanReason, pcModeJson, automationJson, hapticsJson);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(snap, JsonOptions);

        lock (gate)
        {
            Directory.CreateDirectory(directory);
            string target = Path.Combine(directory, id + ".x20bak.json");
            string tmp = target + ".tmp";
            try
            {
                File.WriteAllBytes(tmp, bytes);
                File.Move(tmp, target, true);
            }
            finally
            {
                if (File.Exists(tmp)) File.Delete(tmp);
            }
            PruneLocked();
        }
        return new ProfileBackupEntry(id, utc, cleanReason, bytes.Length);
    }

    public IReadOnlyList<ProfileBackupEntry> ListSnapshots()
    {
        lock (gate)
        {
            if (!Directory.Exists(directory)) return [];
            var entries = new List<ProfileBackupEntry>();
            foreach (string file in Directory.EnumerateFiles(directory, "*.x20bak.json"))
            {
                try
                {
                    var bytes = File.ReadAllBytes(file);
                    var snap = JsonSerializer.Deserialize<ProfileBackupSnapshot>(bytes, JsonOptions);
                    if (snap != null && !string.IsNullOrWhiteSpace(snap.Id))
                        entries.Add(new ProfileBackupEntry(snap.Id, snap.CreatedUtc, snap.Reason, bytes.Length));
                }
                catch (Exception ex) when (ex is JsonException or IOException) { }
            }
            return entries.OrderByDescending(e => e.CreatedUtc).ThenByDescending(e => e.Id, StringComparer.Ordinal).ToList();
        }
    }

    public ProfileBackupSnapshot? ReadSnapshot(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return null;
        string target = Path.Combine(directory, id + ".x20bak.json");
        lock (gate)
        {
            if (!File.Exists(target)) return null;
            try { return JsonSerializer.Deserialize<ProfileBackupSnapshot>(File.ReadAllBytes(target), JsonOptions); }
            catch (Exception ex) when (ex is JsonException or IOException) { return null; }
        }
    }

    public bool DeleteSnapshot(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return false;
        string target = Path.Combine(directory, id + ".x20bak.json");
        lock (gate)
        {
            if (!File.Exists(target)) return false;
            File.Delete(target);
            return true;
        }
    }

    void PruneLocked()
    {
        var files = Directory.GetFiles(directory, "*.x20bak.json")
            .OrderByDescending(f => Path.GetFileName(f), StringComparer.Ordinal)
            .ToArray();
        for (int i = MaxSnapshots; i < files.Length; i++)
        {
            try { File.Delete(files[i]); } catch (IOException) { }
        }
    }
}
