using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using X20Ctl.Zone.Community;
using Xunit;

namespace X20Ctl.Zone.Tests;

public sealed class CommunityTests
{
    private sealed class MockHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(responder(request));
        }
    }

    [Fact]
    public void Capabilities_EnforceStrictHardwareAndModeMatrix()
    {
        var x20ProNative = CommunityCapabilities.GetCapabilitySet("x20_pro", "native");
        Assert.Equal("x20-pro", x20ProNative.ModelId);
        Assert.Equal("native", x20ProNative.ConnectionMode);
        Assert.True(x20ProNative.AllowsCategory("screen"));
        Assert.True(x20ProNative.AllowsCategory("rgb"));
        Assert.True(x20ProNative.AllowsCategory("vibration"));

        var x20Native = CommunityCapabilities.GetCapabilitySet("x20", "native");
        Assert.Equal("native", x20Native.ConnectionMode);
        Assert.False(x20Native.AllowsCategory("screen"));
        Assert.False(x20Native.AllowsCategory("rgb"));
        Assert.False(x20Native.AllowsCategory("vibration"));
        Assert.True(x20Native.AllowsCategory("curve"));
        Assert.True(x20Native.AllowsCategory("macro"));

        var x10Native = CommunityCapabilities.GetCapabilitySet("x10", "native");
        Assert.Equal("native", x10Native.ConnectionMode);
        Assert.False(x10Native.AllowsCategory("screen"));
        Assert.False(x10Native.AllowsCategory("rgb"));
        Assert.True(x10Native.AllowsCategory("vibration"));
        Assert.True(x10Native.AllowsCategory("curve"));
        Assert.True(x10Native.AllowsCategory("macro"));

        var d10Native = CommunityCapabilities.GetCapabilitySet("d10", "native");
        Assert.Equal("native", d10Native.ConnectionMode);
        Assert.True(d10Native.Supports("gyros"));
        Assert.True(d10Native.AllowsCategory("vibration"));
        Assert.True(d10Native.AllowsCategory("curve"));
        Assert.True(d10Native.AllowsCategory("macro"));
        Assert.False(d10Native.AllowsCategory("screen"));
        Assert.False(d10Native.AllowsCategory("rgb"));

        var duneNativeRequested = CommunityCapabilities.GetCapabilitySet("dune-8k", "native");
        Assert.Equal("pc-mode", duneNativeRequested.ConnectionMode);
        Assert.True(duneNativeRequested.Supports("keyboard"));
        Assert.True(duneNativeRequested.Supports("mouse"));
        Assert.False(duneNativeRequested.Supports("rgb"));
        Assert.False(duneNativeRequested.AllowsCategory("screen"));
    }

    [Fact]
    public void Identity_SignsP256_DerivesUid_RecoversKey_AndSolvesPow()
    {
        using var id = CommunityIdentity.CreateNew("AimPro");
        Assert.Matches(new Regex("^X20-[A-F0-9]{4}-[A-F0-9]{4}$"), id.Uid);

        var spkiBytes = Convert.FromBase64String(id.PublicKeyBase64);
        Assert.Equal(id.Uid, CommunityIdentity.DeriveUidFromSpki(spkiBytes));

        var ts = "1795000000000";
        var payload = "{\"ok\":true}";
        var sigB64 = id.SignCanonical(ts, "item:submit", payload);
        var sigBytes = Convert.FromBase64String(sigB64);
        Assert.Equal(64, sigBytes.Length);

        using var verifier = ECDsa.Create();
        verifier.ImportSubjectPublicKeyInfo(spkiBytes, out _);
        Assert.True(verifier.VerifyData(
            Encoding.UTF8.GetBytes($"{id.Uid}:{ts}:item:submit:{payload}"),
            sigBytes,
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation));

        var recovery = id.ExportRecoveryKey();
        Assert.StartsWith("X20K1.", recovery, StringComparison.Ordinal);
        using var restored = CommunityIdentity.RestoreFromRecoveryKey(recovery);
        Assert.Equal(id.Uid, restored.Uid);
        Assert.Equal(id.PublicKeyBase64, restored.PublicKeyBase64);

        var token = "test.pow.token";
        var nonce = CommunityIdentity.SolvePow(token, 2);
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{token}:{nonce}"))).ToLowerInvariant();
        Assert.StartsWith("00", digest, StringComparison.Ordinal);

        var hw = CommunityIdentity.GetHardwareHash();
        Assert.Equal(64, hw.Length);
        Assert.Equal(hw, CommunityIdentity.GetHardwareHash());
    }

    [Fact]
    public async Task Client_BlocksWhenGuarded_SupportsEtag_AndRejectsTamperedAssets()
    {
        var prevGuard = CommunityClient.SandboxOfflineGuard;
        try
        {
            using var id = CommunityIdentity.CreateNew("Tester");
            var cacheDir = Path.Combine(Path.GetTempPath(), "x20-cache-" + Guid.NewGuid().ToString("N"));
            var cache = new CommunityCache(cacheDir);
            var handler = new MockHandler(req =>
            {
                if (req.RequestUri!.AbsolutePath.EndsWith("/pulse", StringComparison.Ordinal))
                {
                    if (req.Headers.IfNoneMatch.ToString() == "\"v1\"")
                        return new HttpResponseMessage(HttpStatusCode.NotModified);

                    var msg = new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("{\"onlineCount\":42,\"unreadCount\":2,\"notifications\":[]}", Encoding.UTF8, "application/json")
                    };
                    msg.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue("\"v1\"");
                    return msg;
                }
                if (req.RequestUri.AbsolutePath.Contains("/asset/", StringComparison.Ordinal))
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 })
                    };
                }
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            });

            var client = new CommunityClient(id, cache, "https://example.invalid", handler);
            CommunityClient.SandboxOfflineGuard = true;
            var guarded = await client.GetPulseAsync();
            Assert.True(guarded.Ok);
            Assert.Equal(0, handler.CallCount);

            CommunityClient.SandboxOfflineGuard = false;
            var first = await client.GetPulseAsync();
            Assert.True(first.Ok);
            Assert.Equal(42, first.Data!.OnlineCount);
            Assert.Equal(2, first.Data.UnreadCount);

            var second = await client.GetPulseAsync();
            Assert.True(second.Ok);
            Assert.True(second.Data!.NotModified);

            var tampered = await client.DownloadAssetAsync("/api/community/asset/ITEM-BAD", "0000000000000000000000000000000000000000000000000000000000000000");
            Assert.Null(tampered);

            if (Directory.Exists(cacheDir)) Directory.Delete(cacheDir, true);
        }
        finally
        {
            CommunityClient.SandboxOfflineGuard = prevGuard;
        }
    }

    [Fact]
    public void SetupStore_RoundTripsCommunityMetadata_AndLoadsLegacyJson()
    {
        var path = Path.Combine(Path.GetTempPath(), "x20-setup-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var store = new SetupStore(path);
            store.Load();
            var snap = SetupSnapshot.Capture("x20_pro", new("x20_pro"), new("x20_pro"), new("x20_pro"), new("x20_pro"));
            var created = store.Create("Apex Linear", snap);
            store.UpdateCommunityMetadata(
                created.Id,
                "Zero deadzone linear look",
                new() { "fps", "linear" },
                "AmjadXCTL",
                "X20-1234-ABCD",
                "ITEM-99",
                2,
                "ITEM-12");

            var reloaded = new SetupStore(path);
            reloaded.Load();
            var item = Assert.Single(reloaded.Items);
            Assert.Equal("AmjadXCTL", item.AuthorHandle);
            Assert.Equal(2, item.CommunityVersion);
            Assert.Equal("ITEM-99", item.CommunityItemId);
            Assert.Contains("fps", item.Tags!);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void IdentityVault_RestoresHandleWhenLocalFileDeleted()
    {
        var prevRead = CommunityIdentity.PlatformVaultRead;
        var prevWrite = CommunityIdentity.PlatformVaultWrite;
        var defaultPath = CommunityIdentity.GetDefaultPath();
        var backupPath = CommunityIdentity.GetBackupVaultPath();
        var savedPrimary = File.Exists(defaultPath) ? File.ReadAllText(defaultPath) : null;
        var savedBackup = File.Exists(backupPath) ? File.ReadAllText(backupPath) : null;

        CommunityIdentityData? inMemoryVault = null;
        try
        {
            CommunityIdentity.PlatformVaultRead = () => inMemoryVault;
            CommunityIdentity.PlatformVaultWrite = d => inMemoryVault = d;

            using var created = CommunityIdentity.CreateNew("AmjadVaultTest");
            created.Save();
            Assert.NotNull(inMemoryVault);
            Assert.Equal("AmjadVaultTest", inMemoryVault!.Handle);

            if (File.Exists(defaultPath)) File.Delete(defaultPath);
            if (File.Exists(backupPath)) File.Delete(backupPath);

            using var recovered = CommunityIdentity.LoadOrCreate();
            Assert.Equal(created.Uid, recovered.Uid);
            Assert.Equal(created.PublicKeyBase64, recovered.PublicKeyBase64);
            Assert.Equal("AmjadVaultTest", recovered.Handle);
            Assert.True(File.Exists(defaultPath));
        }
        finally
        {
            CommunityIdentity.PlatformVaultRead = prevRead;
            CommunityIdentity.PlatformVaultWrite = prevWrite;
            if (savedPrimary != null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(defaultPath)!);
                File.WriteAllText(defaultPath, savedPrimary);
            }
            else if (File.Exists(defaultPath))
            {
                File.Delete(defaultPath);
            }
            if (savedBackup != null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
                File.WriteAllText(backupPath, savedBackup);
            }
            else if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }
        }
    }

    [Fact]
    public async Task Client_SupportsMembersRosterAndDiscordChatApis()
    {
        var prevGuard = CommunityClient.SandboxOfflineGuard;
        try
        {
            CommunityClient.SandboxOfflineGuard = false;
            using var id = CommunityIdentity.CreateNew("ChatTester");
            var cacheDir = Path.Combine(Path.GetTempPath(), "x20-chat-cache-" + Guid.NewGuid().ToString("N"));
            var cache = new CommunityCache(cacheDir);
            var handler = new MockHandler(req =>
            {
                var path = req.RequestUri!.AbsolutePath;
                if (path == "/api/community/members")
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(
                            "{\"success\":true,\"onlineCount\":1,\"totalMembers\":2,\"online\":[{\"uid\":\"X20-AAAA-1111\",\"handle\":\"SupporterOne\",\"isSupporter\":true,\"canUseGifPfp\":true,\"instantApproval\":true,\"roleColor\":\"#C084FC\",\"roleTitle\":\"Supporter\",\"isOnline\":true}],\"offline\":[{\"uid\":\"X20-BBBB-2222\",\"handle\":\"OfflineUser\",\"isOnline\":false}]}",
                            Encoding.UTF8,
                            "application/json")
                    };
                }
                if (path == "/api/community/lounge" && req.Method == HttpMethod.Get)
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(
                            "{\"success\":true,\"channel\":\"general\",\"channels\":[{\"id\":\"general\",\"name\":\"general\",\"topic\":\"Main\"}],\"dmPeers\":[],\"messages\":[{\"id\":\"MSG-1\",\"uid\":\"X20-AAAA-1111\",\"body\":\"hello discord\",\"channel\":\"general\",\"reactions\":{\"🔥\":[\"X20-AAAA-1111\"]},\"pinned\":false,\"createdAt\":\"2026-11-28T12:00:00Z\"}],\"slowmodeSeconds\":15,\"paused\":false}",
                            Encoding.UTF8,
                            "application/json")
                    };
                }
                if (path == "/api/community/lounge" && (req.Method == HttpMethod.Post || req.Method == HttpMethod.Patch))
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(
                            "{\"success\":true,\"message\":{\"id\":\"MSG-2\",\"uid\":\"X20-AAAA-1111\",\"body\":\"yo\",\"channel\":\"general\",\"reactions\":{\"❤️\":[\"X20-AAAA-1111\"]},\"createdAt\":\"2026-11-28T12:01:00Z\"}}",
                            Encoding.UTF8,
                            "application/json")
                    };
                }
                if (path == "/api/community/lounge" && req.Method == HttpMethod.Delete)
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("{\"success\":true}", Encoding.UTF8, "application/json")
                    };
                }
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            });

            var client = new CommunityClient(id, cache, "https://example.invalid", handler);
            var members = await client.GetMembersAsync();
            Assert.True(members.Ok);
            Assert.Equal(1, members.Data!.OnlineCount);
            Assert.Single(members.Data.Online);
            Assert.True(members.Data.Online[0].CanUseGifPfp);
            Assert.True(members.Data.Online[0].InstantApproval);
            Assert.Equal("#C084FC", members.Data.Online[0].RoleColor);
            Assert.Single(members.Data.Offline);

            var chat = await client.GetChatAsync("general");
            Assert.True(chat.Ok);
            Assert.Single(chat.Data!.Messages);
            Assert.Equal("hello discord", chat.Data.Messages[0].Body);
            Assert.True(chat.Data.Messages[0].Reactions.ContainsKey("🔥"));

            var posted = await client.PostChatAsync("yo", "general");
            Assert.True(posted.Ok);
            Assert.Equal("MSG-2", posted.Data!.Id);

            var reacted = await client.ToggleReactionAsync("MSG-2", "❤️");
            Assert.True(reacted.Ok);
            Assert.True(reacted.Data!.Reactions.ContainsKey("❤️"));

            var deleted = await client.DeleteChatMessageAsync("MSG-2");
            Assert.True(deleted.Ok);

            if (Directory.Exists(cacheDir)) Directory.Delete(cacheDir, true);
        }
        finally
        {
            CommunityClient.SandboxOfflineGuard = prevGuard;
        }
    }
}
