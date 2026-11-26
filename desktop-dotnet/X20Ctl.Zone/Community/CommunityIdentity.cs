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

    public string Uid { get; }
    public string PublicKeyBase64 { get; }
    public string? Handle { get; set; }

    private CommunityIdentity(ECDsa ecdsa, string? handle = null)
    {
        _ecdsa = ecdsa;
        var spki = ecdsa.ExportSubjectPublicKeyInfo();
        PublicKeyBase64 = Convert.ToBase64String(spki);
        Uid = DeriveUidFromSpki(spki);
        Handle = handle;
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

    public static CommunityIdentity CreateNew(string? handle = null)
    {
        var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return new CommunityIdentity(ecdsa, handle);
    }

    public static CommunityIdentity LoadOrCreate(string? path = null)
    {
        path ??= GetDefaultPath();
        if (File.Exists(path))
        {
            try
            {
                var json = File.ReadAllText(path, Encoding.UTF8);
                var data = JsonSerializer.Deserialize<CommunityIdentityData>(json);
                if (data != null && !string.IsNullOrWhiteSpace(data.PrivateKeyPkcs8Base64))
                {
                    var ecdsa = ECDsa.Create();
                    ecdsa.ImportPkcs8PrivateKey(Convert.FromBase64String(data.PrivateKeyPkcs8Base64), out _);
                    return new CommunityIdentity(ecdsa, data.Handle);
                }
            }
            catch
            {
                // Regenerate cleanly if corrupted
            }
        }
        var created = CreateNew();
        created.Save(path);
        return created;
    }

    public void Save(string? path = null)
    {
        path ??= GetDefaultPath();
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var data = new CommunityIdentityData
        {
            Uid = Uid,
            PublicKeyBase64 = PublicKeyBase64,
            PrivateKeyPkcs8Base64 = Convert.ToBase64String(_ecdsa.ExportPkcs8PrivateKey()),
            Handle = Handle
        };
        File.WriteAllText(path, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);
    }

    public string ExportRecoveryKey()
    {
        var pkcs8 = _ecdsa.ExportPkcs8PrivateKey();
        return "X20K1." + Base64UrlEncode(pkcs8);
    }

    public static CommunityIdentity RestoreFromRecoveryKey(string recoveryKey, string? savePath = null)
    {
        var trimmed = (recoveryKey ?? string.Empty).Trim();
        if (!trimmed.StartsWith("X20K1.", StringComparison.Ordinal))
            throw new FormatException("Recovery key must start with X20K1.");
        var bytes = Base64UrlDecode(trimmed[6..]);
        var ecdsa = ECDsa.Create();
        ecdsa.ImportPkcs8PrivateKey(bytes, out _);
        var id = new CommunityIdentity(ecdsa);
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
