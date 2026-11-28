using System;
using Microsoft.Win32;
using X20Ctl.Zone.Community;

namespace X20Ctl.Product.Community;

/// <summary>
/// Persists the player's ECDSA handle identity in HKCU\Software\X20CTL\IdentityVault so
/// updates, uninstalls, and redownloads never lose their claimed handle.
/// </summary>
internal static class WindowsIdentityVault
{
    private const string VaultSubKey = @"Software\X20CTL\IdentityVault";
    private static bool _installed;

    public static void Install()
    {
        if (_installed || !OperatingSystem.IsWindows()) return;
        _installed = true;
        CommunityIdentity.PlatformVaultRead = Read;
        CommunityIdentity.PlatformVaultWrite = Write;
    }

    public static CommunityIdentityData? Read()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(VaultSubKey);
            if (key == null) return null;
            var priv = key.GetValue("PrivateKeyPkcs8Base64")?.ToString();
            if (string.IsNullOrWhiteSpace(priv)) return null;
            return new CommunityIdentityData
            {
                Uid = key.GetValue("Uid")?.ToString() ?? string.Empty,
                PublicKeyBase64 = key.GetValue("PublicKeyBase64")?.ToString() ?? string.Empty,
                PrivateKeyPkcs8Base64 = priv,
                Handle = key.GetValue("Handle")?.ToString()
            };
        }
        catch
        {
            return null;
        }
    }

    public static void Write(CommunityIdentityData data)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(VaultSubKey);
            key.SetValue("Uid", data.Uid ?? string.Empty);
            key.SetValue("PublicKeyBase64", data.PublicKeyBase64 ?? string.Empty);
            key.SetValue("PrivateKeyPkcs8Base64", data.PrivateKeyPkcs8Base64 ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(data.Handle))
                key.SetValue("Handle", data.Handle);
        }
        catch
        {
            // Ignore restricted registry environments
        }
    }
}
