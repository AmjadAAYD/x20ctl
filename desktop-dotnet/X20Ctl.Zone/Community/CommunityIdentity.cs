using System;
using System.IO;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace X20Ctl.Zone.Community;

public sealed class CommunityIdentityData
{
    public string Uid { get; set; } = string.Empty;
    public string PublicKeyBase64 { get; set; } = string.Empty;
    public string PrivateKeyPkcs8Base64 { get; set; } = string.Empty;
    public string? Handle { get; set; }
}

public sealed class CommunityIdentity : IDisposable
{
    private readonly ECDsa _ecdsa;
    private string? _storagePath;
    private bool _isDefaultStorage;

    public string Uid { get; }
    public string PublicKeyBase64 { get; }
    public string? Handle { get; set; }

    /// <summary>Optional OS-level vault readers/writers (e.g. Windows HKCU Registry) registered by the desktop host.</summary>
    public static Func<CommunityIdentityData?>? PlatformVaultRead { get; set; }
    public static Action<CommunityIdentityData>? PlatformVaultWrite { get; set; }

    private CommunityIdentity(ECDsa ecdsa, string? handle = null, string? storagePath = null, bool isDefaultStorage = false)
    {
        _ecdsa = ecdsa;
        var spki = ecdsa.ExportSubjectPublicKeyInfo();
        PublicKeyBase64 = Convert.ToBase64String(spki);
        Uid = DeriveUidFromSpki(spki);
        Handle = handle;
        _storagePath = storagePath;
        _isDefaultStorage = isDefaultStorage;
    }

    public static string DeriveUidFromSpki(byte[] spkiBytes)
    {
        var hex = Convert.ToHexString(SHA256.HashData(spkiBytes)).ToUpperInvariant();
        return $"X20-{hex[..4]}-{hex[4..8]}";
    }

    public static string GetDefaultPath()
    {
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(baseDir))
            baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
        return Path.Combine(baseDir, "X20Ctl", "community-identity.json");
    }

    /// <summary>Secondary persistent vault outside the app's install/purge folders so updates and reinstalls never wipe claimed handles.</summary>
    public static string GetBackupVaultPath()
    {
        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(roaming))
            roaming = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        return Path.Combine(roaming, "X20CTL-Vault", "community-identity.v1.json");
    }

    private static bool IsDefaultIdentityPath(string path)
    {
        try
        {
            return string.Equals(Path.GetFullPath(path), Path.GetFullPath(GetDefaultPath()), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static CommunityIdentityData? TryReadIdentityDataFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) return null;
            var json = File.ReadAllText(filePath, Encoding.UTF8);
            var data = JsonSerializer.Deserialize<CommunityIdentityData>(json);
            return IsValidIdentityData(data) ? data : null;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsValidIdentityData(CommunityIdentityData? data)
    {
        if (data == null || string.IsNullOrWhiteSpace(data.PrivateKeyPkcs8Base64)) return false;
        try
        {
            using var test = ECDsa.Create();
            test.ImportPkcs8PrivateKey(Convert.FromBase64String(data.PrivateKeyPkcs8Base64), out _);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static CommunityIdentity FromIdentityData(CommunityIdentityData data, string storagePath, bool isDefaultStorage)
    {
        var ecdsa = ECDsa.Create();
        ecdsa.ImportPkcs8PrivateKey(Convert.FromBase64String(data.PrivateKeyPkcs8Base64), out _);
        return new CommunityIdentity(ecdsa, data.Handle, storagePath, isDefaultStorage);
    }

    public static CommunityIdentity CreateNew(string? handle = null)
    {
        var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return new CommunityIdentity(ecdsa, handle);
    }

    public static CommunityIdentity LoadOrCreate(string? path = null)
    {
        bool useDefaultVault = string.IsNullOrWhiteSpace(path) || IsDefaultIdentityPath(path!);
        string targetPath = string.IsNullOrWhiteSpace(path) ? GetDefaultPath() : path!;

        var primaryData = TryReadIdentityDataFile(targetPath);

        if (!useDefaultVault)
        {
            if (primaryData != null)
                return FromIdentityData(primaryData, targetPath, isDefaultStorage: false);
            var isolated = CreateNew();
            isolated.Save(targetPath);
            return isolated;
        }

        // 3-layer permanent recovery for the real desktop identity:
        // 1) Primary LocalAppData file
        // 2) Roaming X20CTL-Vault backup file
        // 3) Platform registry vault (HKCU\Software\X20CTL\IdentityVault)
        CommunityIdentityData? backupData = TryReadIdentityDataFile(GetBackupVaultPath());
        CommunityIdentityData? platformData = null;
        try
        {
            var candidate = PlatformVaultRead?.Invoke();
            if (IsValidIdentityData(candidate)) platformData = candidate;
        }
        catch
        {
            // Ignore restricted registry reads
        }

        static bool HasClaimedHandle(CommunityIdentityData? d) =>
            d != null && !string.IsNullOrWhiteSpace(d.Handle);

        // Prefer whichever valid copy actually holds a claimed handle if the primary is missing or unclaimed
        CommunityIdentityData? chosen = primaryData;
        if (!HasClaimedHandle(chosen))
        {
            if (HasClaimedHandle(platformData)) chosen = platformData;
            else if (HasClaimedHandle(backupData)) chosen = backupData;
            else chosen ??= platformData ?? backupData;
        }

        if (chosen != null)
        {
            var loaded = FromIdentityData(chosen, targetPath, isDefaultStorage: true);
            // Heal any missing/stale vault layers immediately so all 3 stay synchronized
            bool needsSync = primaryData == null
                || backupData == null
                || platformData == null
                || !string.Equals(primaryData.Uid, loaded.Uid, StringComparison.Ordinal)
                || !string.Equals(primaryData.Handle ?? "", loaded.Handle ?? "", StringComparison.Ordinal)
                || !string.Equals(backupData.Uid, loaded.Uid, StringComparison.Ordinal)
                || !string.Equals(backupData.Handle ?? "", loaded.Handle ?? "", StringComparison.Ordinal)
                || !string.Equals(platformData.Uid, loaded.Uid, StringComparison.Ordinal)
                || !string.Equals(platformData.Handle ?? "", loaded.Handle ?? "", StringComparison.Ordinal);
            if (needsSync) loaded.Save(targetPath);
            return loaded;
        }

        var created = CreateNew();
        created._isDefaultStorage = true;
        created.Save(targetPath);
        return created;
    }

    public void Save(string? path = null)
    {
        var targetPath = string.IsNullOrWhiteSpace(path) ? (_storagePath ?? GetDefaultPath()) : path;
        if (!string.IsNullOrWhiteSpace(path))
            _isDefaultStorage = IsDefaultIdentityPath(targetPath);
        else if (_storagePath == null)
            _isDefaultStorage = true;

        _storagePath = targetPath;
        var dir = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var data = new CommunityIdentityData
        {
            Uid = Uid,
            PublicKeyBase64 = PublicKeyBase64,
            PrivateKeyPkcs8Base64 = Convert.ToBase64String(_ecdsa.ExportPkcs8PrivateKey()),
            Handle = Handle
        };
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(targetPath, json, Encoding.UTF8);

        if (_isDefaultStorage)
        {
            try
            {
                var vaultPath = GetBackupVaultPath();
                var vaultDir = Path.GetDirectoryName(vaultPath);
                if (!string.IsNullOrEmpty(vaultDir)) Directory.CreateDirectory(vaultDir);
                File.WriteAllText(vaultPath, json, Encoding.UTF8);
            }
            catch
            {
                // Best-effort secondary file mirror
            }
            try
            {
                PlatformVaultWrite?.Invoke(data);
            }
            catch
            {
                // Best-effort OS vault mirror
            }
        }
    }

    public string ExportRecoveryKey()
    {
        var pkcs8 = _ecdsa.ExportPkcs8PrivateKey();
        return "X20K1." + Base64UrlEncode(pkcs8);
    }

    public static CommunityIdentity RestoreFromRecoveryKey(string recoveryKey, string? savePath = null, string? handle = null)
    {
        var trimmed = (recoveryKey ?? string.Empty).Trim();
        if (!trimmed.StartsWith("X20K1.", StringComparison.Ordinal))
            throw new FormatException("Recovery key must start with X20K1.");
        var bytes = Base64UrlDecode(trimmed[6..]);
        var ecdsa = ECDsa.Create();
        ecdsa.ImportPkcs8PrivateKey(bytes, out _);
        bool isDefault = string.IsNullOrWhiteSpace(savePath) ? false : IsDefaultIdentityPath(savePath!);
        var id = new CommunityIdentity(ecdsa, handle: handle, storagePath: savePath, isDefaultStorage: isDefault);
        if (!string.IsNullOrWhiteSpace(savePath))
            id.Save(savePath);
        return id;
    }

    public string SignCanonical(string timestampMs, string action, string bodyText)
    {
        var canonical = $"{Uid}:{timestampMs}:{action}:{bodyText}";
        var sig = _ecdsa.SignData(
            Encoding.UTF8.GetBytes(canonical),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return Convert.ToBase64String(sig);
    }

    public static string SolvePow(string token, int difficulty)
    {
        var prefix = new string('0', Math.Clamp(difficulty, 1, 6));
        long nonce = 0;
        while (true)
        {
            var input = Encoding.UTF8.GetBytes($"{token}:{nonce}");
            var hex = Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant();
            if (hex.StartsWith(prefix, StringComparison.Ordinal))
                return nonce.ToString();
            nonce++;
        }
    }

    public static string GetHardwareHash()
    {
        var sb = new StringBuilder();
        sb.Append(Environment.MachineName).Append('|');
        sb.Append(RuntimeInformation.OSArchitecture).Append('|');
        sb.Append(RuntimeInformation.OSDescription).Append('|');
        if (OperatingSystem.IsLinux() && File.Exists("/etc/machine-id"))
        {
            sb.Append(File.ReadAllText("/etc/machine-id").Trim()).Append('|');
        }
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                var mac = nic.GetPhysicalAddress().ToString();
                if (!string.IsNullOrEmpty(mac))
                {
                    sb.Append(mac);
                    break;
                }
            }
        }
        catch
        {
            // Ignore restricted NIC inspection
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()))).ToLowerInvariant();
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string s)
    {
        var padded = s.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }
        return Convert.FromBase64String(padded);
    }

    public void Dispose() => _ecdsa.Dispose();
}
