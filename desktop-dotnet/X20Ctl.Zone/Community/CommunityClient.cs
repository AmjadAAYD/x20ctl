using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace X20Ctl.Zone.Community;

public sealed class CommunityClient
{
    public const string DefaultBaseUrl = "https://x20ctl-workshop.aaydamjad.workers.dev";
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly CommunityIdentity _identity;
    private readonly CommunityCache _cache;
    private readonly string _hwHash;
    private string? _lastPulseEtag;

    public string BaseUrl { get; }
    public bool OfflineMode { get; set; }
    public CommunityIdentity Identity => _identity;

    public static bool SandboxOfflineGuard { get; set; }
    public bool IsNetworkDisabled => OfflineMode || SandboxOfflineGuard;

    public CommunityClient(
        CommunityIdentity? identity = null,
        CommunityCache? cache = null,
        string? baseUrl = null,
        HttpMessageHandler? handler = null)
    {
        _identity = identity ?? CommunityIdentity.LoadOrCreate();
        _cache = cache ?? new CommunityCache();
        _hwHash = CommunityIdentity.GetHardwareHash();
        BaseUrl = (baseUrl ?? Environment.GetEnvironmentVariable("X20CTL_COMMUNITY_URL") ?? DefaultBaseUrl).TrimEnd('/');
        _http = handler != null ? new HttpClient(handler) : new HttpClient();
        _http.Timeout = TimeSpan.FromSeconds(15);
    }

    private HttpRequestMessage BuildRequest(HttpMethod method, string path, string action, string? jsonBody)
    {
        var req = new HttpRequestMessage(method, $"{BaseUrl}{path}");
        req.Headers.TryAddWithoutValidation("x-x20-uid", _identity.Uid);
        req.Headers.TryAddWithoutValidation("x-x20-hw-hash", _hwHash);
        if (jsonBody != null)
        {
            var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            var sig = _identity.SignCanonical(ts, action, jsonBody);
            req.Headers.TryAddWithoutValidation("x-x20-ts", ts);
            req.Headers.TryAddWithoutValidation("x-x20-sig", sig);
            req.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        }
        return req;
    }

    private async Task<(bool Ok, JsonDocument? Doc, string? Error)> SendJsonAsync(
        HttpMethod method,
        string path,
        string action,
        object? payload,
        CancellationToken ct)
    {
        if (IsNetworkDisabled)
            return (false, null, "Community network disabled in Offline or Review mode.");
        try
        {
            var jsonBody = payload != null ? JsonSerializer.Serialize(payload, JsonOpts) : null;
            using var req = BuildRequest(method, path, action, jsonBody);
            using var res = await _http.SendAsync(req, ct).ConfigureAwait(false);
            var text = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (!res.IsSuccessStatusCode)
            {
                try
                {
                    using var errDoc = JsonDocument.Parse(text);
                    if (errDoc.RootElement.TryGetProperty("error", out var errObj))
                    {
                        var msg = errObj.ValueKind == JsonValueKind.Object && errObj.TryGetProperty("message", out var m)
                            ? m.GetString()
                            : errObj.ToString();
                        return (false, null, msg ?? $"HTTP {(int)res.StatusCode}");
                    }
                }
                catch
                {
                    // Fall back to status code
                }
                return (false, null, $"HTTP {(int)res.StatusCode}");
            }
            return (true, JsonDocument.Parse(text), null);
        }
        catch (Exception ex)
        {
            return (false, null, ex.Message);
        }
    }

    public async Task<ApiResult<PowChallenge>> GetChallengeAsync(string action, CancellationToken ct = default)
    {
        var (ok, doc, err) = await SendJsonAsync(HttpMethod.Get, $"/api/community/challenge?action={Uri.EscapeDataString(action)}", "", null, ct).ConfigureAwait(false);
        if (!ok || doc == null) return ApiResult<PowChallenge>.Fail(err ?? "Challenge failed");
        using (doc)
        {
            var ch = doc.RootElement.GetProperty("challenge").Deserialize<PowChallenge>(JsonOpts)!;
            return ApiResult<PowChallenge>.Success(ch);
        }
    }

    public async Task<ApiResult<CreatorProfile>> RegisterProfileAsync(string handle, string? ownerPassphrase = null, CancellationToken ct = default)
    {
        if (IsNetworkDisabled) return ApiResult<CreatorProfile>.Fail("Offline mode is active.");
        var chRes = await GetChallengeAsync("register", ct).ConfigureAwait(false);
        if (!chRes.Ok || chRes.Data == null) return ApiResult<CreatorProfile>.Fail(chRes.Error ?? "Challenge failed");

        var nonce = CommunityIdentity.SolvePow(chRes.Data.Token, chRes.Data.Difficulty);
        var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        var sig = _identity.SignCanonical(ts, "profile:register", handle);
        var body = new
        {
            uid = _identity.Uid,
            publicKey = _identity.PublicKeyBase64,
            handle,
            timestamp = ts,
            signature = sig,
            pow = new { token = chRes.Data.Token, nonce },
            ownerPassphrase = string.IsNullOrEmpty(ownerPassphrase) ? null : ownerPassphrase
        };
        var (ok, doc, err) = await SendJsonAsync(HttpMethod.Post, "/api/community/profile/register", "profile:register", body, ct).ConfigureAwait(false);
        if (!ok || doc == null) return ApiResult<CreatorProfile>.Fail(err ?? "Registration failed");
        using (doc)
        {
            var creator = doc.RootElement.GetProperty("creator").Deserialize<CreatorProfile>(JsonOpts)!;
            _identity.Handle = creator.Handle;
            _identity.Save();
            return ApiResult<CreatorProfile>.Success(creator);
        }
    }

    public async Task LeavePulseAsync(CancellationToken ct = default)
    {
        if (IsNetworkDisabled) return;
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(2.5));
            using var req = BuildRequest(HttpMethod.Delete, "/api/community/pulse", "", null);
            using var _ = await _http.SendAsync(req, cts.Token).ConfigureAwait(false);
        }
        catch
        {
            // Best-effort disconnect on shutdown
        }
    }

    public async Task<ApiResult<CreatorProfile>> UpdateProfileAsync(
        string? equippedBadge = null,
        byte[]? pfpBytes = null,
        string? pfpDeclaredType = null,
        CancellationToken ct = default)
    {
        var body = new
        {
            equippedBadge,
            pfpBase64 = pfpBytes != null ? Convert.ToBase64String(pfpBytes) : null,
            pfpDeclaredType
        };
        var (ok, doc, err) = await SendJsonAsync(HttpMethod.Post, "/api/community/profile/update", "profile:update", body, ct).ConfigureAwait(false);
        if (!ok || doc == null) return ApiResult<CreatorProfile>.Fail(err ?? "Profile update failed");
        using (doc)
        {
            var creator = doc.RootElement.GetProperty("creator").Deserialize<CreatorProfile>(JsonOpts)!;
            return ApiResult<CreatorProfile>.Success(creator);
        }
    }

    public async Task<ApiResult<(CreatorProfile Creator, List<CommunityItem> Uploads, List<BadgeDef> Badges)>> GetProfileAsync(
        string uid,
        CancellationToken ct = default)
    {
        var full = await GetProfileWithStatsAsync(uid, ct).ConfigureAwait(false);
        return full.Ok
            ? ApiResult<(CreatorProfile, List<CommunityItem>, List<BadgeDef>)>.Success((full.Data.Creator, full.Data.Uploads, full.Data.Badges))
            : ApiResult<(CreatorProfile, List<CommunityItem>, List<BadgeDef>)>.Fail(full.Error ?? "Profile unavailable");
    }

    public async Task<ApiResult<(CreatorProfile Creator, List<CommunityItem> Uploads, List<BadgeDef> Badges, CreatorStats? Stats)>> GetProfileWithStatsAsync(
        string uid,
        CancellationToken ct = default)
    {
        var (ok, doc, err) = await SendJsonAsync(
            HttpMethod.Get,
            $"/api/community/profile/{Uri.EscapeDataString(uid)}?viewerUid={Uri.EscapeDataString(_identity.Uid)}",
            "",
            null,
            ct).ConfigureAwait(false);
        if (!ok || doc == null) return ApiResult<(CreatorProfile, List<CommunityItem>, List<BadgeDef>, CreatorStats?)>.Fail(err ?? "Profile unavailable");
        using (doc)
        {
            var creator = doc.RootElement.GetProperty("creator").Deserialize<CreatorProfile>(JsonOpts)!;
            var uploads = doc.RootElement.GetProperty("uploads").Deserialize<List<CommunityItem>>(JsonOpts) ?? new();
            var badges = doc.RootElement.GetProperty("badgeCatalog").Deserialize<List<BadgeDef>>(JsonOpts) ?? new();
            CreatorStats? stats = doc.RootElement.TryGetProperty("stats", out var statsEl) && statsEl.ValueKind == JsonValueKind.Object
                ? statsEl.Deserialize<CreatorStats>(JsonOpts)
                : null;
            return ApiResult<(CreatorProfile, List<CommunityItem>, List<BadgeDef>, CreatorStats?)>.Success((creator, uploads, badges, stats));
        }
    }

    public async Task<ApiResult<CatalogResponse>> GetCatalogAsync(CatalogQuery query, CancellationToken ct = default)
    {
        if (IsNetworkDisabled)
        {
            var cached = _cache.LoadCatalog();
            return cached != null
                ? ApiResult<CatalogResponse>.Success(cached)
                : ApiResult<CatalogResponse>.Success(new CatalogResponse());
        }

        var parts = new List<string> { $"sort={Uri.EscapeDataString(query.Sort)}" };
        if (!string.IsNullOrWhiteSpace(query.Category)) parts.Add($"category={Uri.EscapeDataString(query.Category)}");
        if (!string.IsNullOrWhiteSpace(query.ModelId)) parts.Add($"modelId={Uri.EscapeDataString(query.ModelId)}");
        if (!string.IsNullOrWhiteSpace(query.ConnectionMode)) parts.Add($"connectionMode={Uri.EscapeDataString(query.ConnectionMode)}");
        if (!string.IsNullOrWhiteSpace(query.MediaType)) parts.Add($"mediaType={Uri.EscapeDataString(query.MediaType)}");
        if (!string.IsNullOrWhiteSpace(query.Search)) parts.Add($"search={Uri.EscapeDataString(query.Search)}");
        if (!string.IsNullOrWhiteSpace(query.GameTag)) parts.Add($"gameTag={Uri.EscapeDataString(query.GameTag)}");
        if (!string.IsNullOrWhiteSpace(query.Uid)) parts.Add($"uid={Uri.EscapeDataString(query.Uid)}");
        if (!string.IsNullOrWhiteSpace(query.WatchingOf)) parts.Add($"watchingOf={Uri.EscapeDataString(query.WatchingOf)}");
        if (!string.IsNullOrWhiteSpace(query.BookmarkedBy)) parts.Add($"bookmarkedBy={Uri.EscapeDataString(query.BookmarkedBy)}");
        if (!string.IsNullOrWhiteSpace(query.Status)) parts.Add($"status={Uri.EscapeDataString(query.Status)}");

        var (ok, doc, err) = await SendJsonAsync(HttpMethod.Get, $"/api/community/catalog?{string.Join("&", parts)}", "", null, ct).ConfigureAwait(false);
        if (!ok || doc == null)
        {
            var cached = _cache.LoadCatalog();
            if (cached != null) return ApiResult<CatalogResponse>.Success(cached);
            return ApiResult<CatalogResponse>.Fail(err ?? "Catalog unavailable");
        }
        using (doc)
        {
            var catalog = doc.RootElement.Deserialize<CatalogResponse>(JsonOpts) ?? new CatalogResponse();
            _cache.SaveCatalog(catalog);
            return ApiResult<CatalogResponse>.Success(catalog);
        }
    }

    public async Task<byte[]?> DownloadAssetAsync(string assetRelativeUrl, string? expectedSha256 = null, CancellationToken ct = default)
    {
        var cacheKey = expectedSha256 ?? assetRelativeUrl;
        var cached = _cache.TryLoadMedia(cacheKey);
        if (cached != null) return cached;
        if (IsNetworkDisabled) return null;

        try
        {
            var url = assetRelativeUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? assetRelativeUrl
                : $"{BaseUrl}{assetRelativeUrl}";
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.TryAddWithoutValidation("x-x20-uid", _identity.Uid);
            req.Headers.TryAddWithoutValidation("x-x20-hw-hash", _hwHash);
            using var res = await _http.SendAsync(req, ct).ConfigureAwait(false);
            if (!res.IsSuccessStatusCode) return null;
            var bytes = await res.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);

            var actualSha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(expectedSha256) &&
                !string.Equals(actualSha, expectedSha256, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
            if (res.Headers.TryGetValues("X-Asset-Sha256", out var headerVals))
            {
                foreach (var hv in headerVals)
                {
                    if (!string.Equals(actualSha, hv, StringComparison.OrdinalIgnoreCase))
                        return null;
                }
            }
            _cache.SaveMedia(cacheKey, bytes);
            return bytes;
        }
        catch
        {
            return null;
        }
    }

    public async Task<ApiResult<CommunityItem>> SubmitItemAsync(
        string title,
        string description,
        string category,
        string modelId,
        string connectionMode,
        object? payload = null,
        string? shareCode = null,
        byte[]? mediaBytes = null,
        string mediaType = "none",
        IReadOnlyList<string>? tags = null,
        IReadOnlyList<string>? gameTags = null,
        string? parentItemId = null,
        string? remixSourceId = null,
        string? fulfillRequestId = null,
        CancellationToken ct = default)
    {
        var chRes = await GetChallengeAsync("submit", ct).ConfigureAwait(false);
        if (!chRes.Ok || chRes.Data == null) return ApiResult<CommunityItem>.Fail(chRes.Error ?? "PoW challenge failed");
        var nonce = CommunityIdentity.SolvePow(chRes.Data.Token, chRes.Data.Difficulty);

        var body = new
        {
            title,
            description,
            category,
            modelId,
            connectionMode,
            mediaType,
            mediaBase64 = mediaBytes != null ? Convert.ToBase64String(mediaBytes) : null,
            payload,
            shareCode,
            tags = tags ?? Array.Empty<string>(),
            gameTags = gameTags ?? Array.Empty<string>(),
            parentItemId,
            remixSourceId,
            fulfillRequestId,
            pow = new { token = chRes.Data.Token, nonce }
        };

        var (ok, doc, err) = await SendJsonAsync(HttpMethod.Post, "/api/community/submit", "item:submit", body, ct).ConfigureAwait(false);
        if (!ok || doc == null) return ApiResult<CommunityItem>.Fail(err ?? "Submit failed");
        using (doc)
        {
            return ApiResult<CommunityItem>.Success(doc.RootElement.GetProperty("item").Deserialize<CommunityItem>(JsonOpts)!);
        }
    }

    public async Task<ApiResult<CommunityItem>> VoteItemAsync(string itemId, int vote, CancellationToken ct = default)
    {
        var (ok, doc, err) = await SendJsonAsync(HttpMethod.Post, $"/api/community/items/{Uri.EscapeDataString(itemId)}/vote", "item:vote", new { vote }, ct).ConfigureAwait(false);
        if (!ok || doc == null) return ApiResult<CommunityItem>.Fail(err ?? "Vote failed");
        using (doc) return ApiResult<CommunityItem>.Success(doc.RootElement.GetProperty("item").Deserialize<CommunityItem>(JsonOpts)!);
    }

    public async Task<ApiResult<bool>> DeleteItemAsync(string itemId, CancellationToken ct = default)
    {
        var (ok, doc, err) = await SendJsonAsync(HttpMethod.Delete, $"/api/community/items/{Uri.EscapeDataString(itemId)}", "item:delete", new { itemId }, ct).ConfigureAwait(false);
        doc?.Dispose();
        return ok ? ApiResult<bool>.Success(true) : ApiResult<bool>.Fail(err ?? "Delete failed");
    }

    public async Task<CommunityItem?> RecordInstallAsync(string itemId, CancellationToken ct = default)
    {
        var (ok, doc, _) = await SendJsonAsync(HttpMethod.Post, "/api/community/install", "", new { itemId }, ct).ConfigureAwait(false);
        if (!ok || doc == null)
        {
            doc?.Dispose();
            return null;
        }
        using (doc)
        {
            return doc.RootElement.TryGetProperty("item", out var itemEl)
                ? itemEl.Deserialize<CommunityItem>(JsonOpts)
                : null;
        }
    }

    public async Task<ApiResult<CommunityItem>> MarkWorksAsync(string itemId, string modelId, CancellationToken ct = default)
    {
        var (ok, doc, err) = await SendJsonAsync(HttpMethod.Post, "/api/community/works", "item:works", new { itemId, modelId }, ct).ConfigureAwait(false);
        if (!ok || doc == null) return ApiResult<CommunityItem>.Fail(err ?? "Works check failed");
        using (doc) return ApiResult<CommunityItem>.Success(doc.RootElement.GetProperty("item").Deserialize<CommunityItem>(JsonOpts)!);
    }

    public async Task<ApiResult<List<CommunityComment>>> GetCommentsAsync(string itemId, CancellationToken ct = default)
    {
        var (ok, doc, err) = await SendJsonAsync(HttpMethod.Get, $"/api/community/comments?itemId={Uri.EscapeDataString(itemId)}", "", null, ct).ConfigureAwait(false);
        if (!ok || doc == null) return ApiResult<List<CommunityComment>>.Fail(err ?? "Failed to load comments");
        using (doc) return ApiResult<List<CommunityComment>>.Success(doc.RootElement.GetProperty("comments").Deserialize<List<CommunityComment>>(JsonOpts) ?? new());
    }

    public async Task<ApiResult<CommunityComment>> AddCommentAsync(string itemId, string bodyText, CancellationToken ct = default)
    {
        var (ok, doc, err) = await SendJsonAsync(HttpMethod.Post, "/api/community/comments", "item:comment", new { itemId, body = bodyText }, ct).ConfigureAwait(false);
        if (!ok || doc == null) return ApiResult<CommunityComment>.Fail(err ?? "Comment failed");
        using (doc) return ApiResult<CommunityComment>.Success(doc.RootElement.GetProperty("comment").Deserialize<CommunityComment>(JsonOpts)!);
    }

    public async Task<ApiResult<bool>> ToggleBookmarkAsync(string itemId, CancellationToken ct = default)
    {
        var (ok, doc, err) = await SendJsonAsync(HttpMethod.Post, "/api/community/bookmark", "item:bookmark", new { itemId }, ct).ConfigureAwait(false);
        if (!ok || doc == null) return ApiResult<bool>.Fail(err ?? "Bookmark failed");
        using (doc) return ApiResult<bool>.Success(doc.RootElement.GetProperty("bookmarked").GetBoolean());
    }

    public async Task<ApiResult<bool>> ReportAsync(string targetType, string targetId, string reason, string details = "", CancellationToken ct = default)
    {
        var (ok, doc, err) = await SendJsonAsync(HttpMethod.Post, "/api/community/report", "item:report", new { targetType, targetId, reason, details }, ct).ConfigureAwait(false);
        doc?.Dispose();
        return ok ? ApiResult<bool>.Success(true) : ApiResult<bool>.Fail(err ?? "Report failed");
    }

    public async Task<ApiResult<(bool Watching, int WatchersCount)>> ToggleWatchAsync(string targetUid, CancellationToken ct = default)
    {
        var (ok, doc, err) = await SendJsonAsync(HttpMethod.Post, "/api/community/watchers", "watchers:toggle", new { targetUid }, ct).ConfigureAwait(false);
        if (!ok || doc == null) return ApiResult<(bool, int)>.Fail(err ?? "Watch toggle failed");
        using (doc)
        {
            return ApiResult<(bool, int)>.Success((
                doc.RootElement.GetProperty("watching").GetBoolean(),
                doc.RootElement.GetProperty("watchersCount").GetInt32()));
        }
    }

    public async Task<ApiResult<(List<CreatorProfile> Watchers, List<CreatorProfile> Watching)>> GetWatchListsAsync(string? uid = null, CancellationToken ct = default)
    {
        var target = uid ?? _identity.Uid;
        var (ok, doc, err) = await SendJsonAsync(HttpMethod.Get, $"/api/community/watchers?uid={Uri.EscapeDataString(target)}", "", null, ct).ConfigureAwait(false);
        if (!ok || doc == null) return ApiResult<(List<CreatorProfile>, List<CreatorProfile>)>.Fail(err ?? "Watch lists unavailable");
        using (doc)
        {
            var watchers = doc.RootElement.GetProperty("watchers").Deserialize<List<CreatorProfile>>(JsonOpts) ?? new();
            var watching = doc.RootElement.GetProperty("watching").Deserialize<List<CreatorProfile>>(JsonOpts) ?? new();
            return ApiResult<(List<CreatorProfile>, List<CreatorProfile>)>.Success((watchers, watching));
        }
    }

    public async Task<ApiResult<List<CommunityRequest>>> GetRequestsAsync(CancellationToken ct = default)
    {
        var (ok, doc, err) = await SendJsonAsync(HttpMethod.Get, "/api/community/requests", "", null, ct).ConfigureAwait(false);
        if (!ok || doc == null) return ApiResult<List<CommunityRequest>>.Fail(err ?? "Requests unavailable");
        using (doc) return ApiResult<List<CommunityRequest>>.Success(doc.RootElement.GetProperty("requests").Deserialize<List<CommunityRequest>>(JsonOpts) ?? new());
    }

    public async Task<ApiResult<CommunityRequest>> CreateRequestAsync(
        string title, string description, string category, string modelId, string connectionMode, string gameTag = "", CancellationToken ct = default)
    {
        var (ok, doc, err) = await SendJsonAsync(HttpMethod.Post, "/api/community/requests", "request:create",
            new { title, description, category, modelId, connectionMode, gameTag }, ct).ConfigureAwait(false);
        if (!ok || doc == null) return ApiResult<CommunityRequest>.Fail(err ?? "Request submit failed");
        using (doc) return ApiResult<CommunityRequest>.Success(doc.RootElement.GetProperty("request").Deserialize<CommunityRequest>(JsonOpts)!);
    }

    public async Task<ApiResult<CommunityRequest>> VoteRequestAsync(string requestId, CancellationToken ct = default)
    {
        var (ok, doc, err) = await SendJsonAsync(HttpMethod.Post, $"/api/community/requests/{Uri.EscapeDataString(requestId)}/vote", "request:vote", new { }, ct).ConfigureAwait(false);
        if (!ok || doc == null) return ApiResult<CommunityRequest>.Fail(err ?? "Vote failed");
        using (doc) return ApiResult<CommunityRequest>.Success(doc.RootElement.GetProperty("request").Deserialize<CommunityRequest>(JsonOpts)!);
    }

    public async Task<ApiResult<ChatChannelResponse>> GetChatAsync(
        string channel = "general",
        string? peerUid = null,
        int limit = 60,
        CancellationToken ct = default)
    {
        var qs = $"channel={Uri.EscapeDataString(channel)}&limit={limit}";
        if (!string.IsNullOrWhiteSpace(peerUid))
            qs += $"&peerUid={Uri.EscapeDataString(peerUid)}";
        var (ok, doc, err) = await SendJsonAsync(HttpMethod.Get, $"/api/community/lounge?{qs}", "", null, ct).ConfigureAwait(false);
        if (!ok || doc == null) return ApiResult<ChatChannelResponse>.Fail(err ?? "Chat unavailable");
        using (doc)
        {
            var resp = doc.RootElement.Deserialize<ChatChannelResponse>(JsonOpts) ?? new ChatChannelResponse();
            return ApiResult<ChatChannelResponse>.Success(resp);
        }
    }

    public async Task<ApiResult<LoungeMessage>> PostChatAsync(
        string bodyText,
        string channel = "general",
        string? recipientUid = null,
        string? replyToId = null,
        CancellationToken ct = default)
    {
        var (ok, doc, err) = await SendJsonAsync(
            HttpMethod.Post,
            "/api/community/lounge",
            "lounge:post",
            new { body = bodyText, channel, recipientUid, replyToId },
            ct).ConfigureAwait(false);
        if (!ok || doc == null) return ApiResult<LoungeMessage>.Fail(err ?? "Message rejected");
        using (doc) return ApiResult<LoungeMessage>.Success(doc.RootElement.GetProperty("message").Deserialize<LoungeMessage>(JsonOpts)!);
    }

    public async Task<ApiResult<LoungeMessage>> ToggleReactionAsync(string messageId, string emoji, CancellationToken ct = default)
    {
        var (ok, doc, err) = await SendJsonAsync(
            HttpMethod.Patch,
            "/api/community/lounge",
            "lounge:react",
            new { messageId, emoji },
            ct).ConfigureAwait(false);
        if (!ok || doc == null) return ApiResult<LoungeMessage>.Fail(err ?? "Reaction failed");
        using (doc) return ApiResult<LoungeMessage>.Success(doc.RootElement.GetProperty("message").Deserialize<LoungeMessage>(JsonOpts)!);
    }

    public async Task<ApiResult<bool>> DeleteChatMessageAsync(string messageId, CancellationToken ct = default)
    {
        var (ok, doc, err) = await SendJsonAsync(
            HttpMethod.Delete,
            "/api/community/lounge",
            "lounge:delete",
            new { messageId },
            ct).ConfigureAwait(false);
        doc?.Dispose();
        return ok ? ApiResult<bool>.Success(true) : ApiResult<bool>.Fail(err ?? "Delete failed");
    }

    public async Task<ApiResult<CommunityMembersResponse>> GetMembersAsync(CancellationToken ct = default)
    {
        var (ok, doc, err) = await SendJsonAsync(HttpMethod.Get, "/api/community/members", "", null, ct).ConfigureAwait(false);
        if (!ok || doc == null) return ApiResult<CommunityMembersResponse>.Fail(err ?? "Members unavailable");
        using (doc)
        {
            var roster = doc.RootElement.Deserialize<CommunityMembersResponse>(JsonOpts) ?? new CommunityMembersResponse();
            return ApiResult<CommunityMembersResponse>.Success(roster);
        }
    }

    public async Task<ApiResult<(List<LoungeMessage> Messages, int SlowmodeSec, bool Paused)>> GetLoungeAsync(CancellationToken ct = default)
    {
        var res = await GetChatAsync("general", null, 50, ct).ConfigureAwait(false);
        if (!res.Ok || res.Data == null) return ApiResult<(List<LoungeMessage>, int, bool)>.Fail(res.Error ?? "Lounge unavailable");
        return ApiResult<(List<LoungeMessage>, int, bool)>.Success((res.Data.Messages, res.Data.SlowmodeSeconds, res.Data.Paused));
    }

    public Task<ApiResult<LoungeMessage>> PostLoungeAsync(string bodyText, CancellationToken ct = default)
        => PostChatAsync(bodyText, "general", null, null, ct);

    public async Task<ApiResult<CommunityPoll>> VotePollAsync(string pollId, int optionIndex, CancellationToken ct = default)
    {
        var (ok, doc, err) = await SendJsonAsync(HttpMethod.Post, $"/api/community/polls/{Uri.EscapeDataString(pollId)}/vote", "poll:vote", new { optionIndex }, ct).ConfigureAwait(false);
        if (!ok || doc == null) return ApiResult<CommunityPoll>.Fail(err ?? "Poll vote failed");
        using (doc) return ApiResult<CommunityPoll>.Success(doc.RootElement.GetProperty("poll").Deserialize<CommunityPoll>(JsonOpts)!);
    }

    public async Task<ApiResult<OwnerNote>> ReactNoteAsync(string noteId, string reaction, CancellationToken ct = default)
    {
        var (ok, doc, err) = await SendJsonAsync(HttpMethod.Post, $"/api/community/notes/{Uri.EscapeDataString(noteId)}/react", "note:react", new { reaction }, ct).ConfigureAwait(false);
        if (!ok || doc == null) return ApiResult<OwnerNote>.Fail(err ?? "Reaction failed");
        using (doc) return ApiResult<OwnerNote>.Success(doc.RootElement.GetProperty("note").Deserialize<OwnerNote>(JsonOpts)!);
    }

    public async Task<ApiResult<(List<CreatorProfile> Makers, List<BadgeDef> Badges)>> GetLeaderboardAsync(string by = "karma", CancellationToken ct = default)
    {
        var (ok, doc, err) = await SendJsonAsync(HttpMethod.Get, $"/api/community/leaderboard?by={Uri.EscapeDataString(by)}", "", null, ct).ConfigureAwait(false);
        if (!ok || doc == null) return ApiResult<(List<CreatorProfile>, List<BadgeDef>)>.Fail(err ?? "Leaderboard unavailable");
        using (doc)
        {
            var makers = doc.RootElement.GetProperty("makers").Deserialize<List<CreatorProfile>>(JsonOpts) ?? new();
            var badges = doc.RootElement.GetProperty("badgeCatalog").Deserialize<List<BadgeDef>>(JsonOpts) ?? new();
            return ApiResult<(List<CreatorProfile>, List<BadgeDef>)>.Success((makers, badges));
        }
    }

    public async Task<ApiResult<PulseResponse>> GetPulseAsync(CancellationToken ct = default)
    {
        if (IsNetworkDisabled)
            return ApiResult<PulseResponse>.Success(new PulseResponse { OnlineCount = 0 });

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/api/community/pulse");
            req.Headers.TryAddWithoutValidation("x-x20-uid", _identity.Uid);
            req.Headers.TryAddWithoutValidation("x-x20-hw-hash", _hwHash);
            if (!string.IsNullOrEmpty(_lastPulseEtag))
                req.Headers.TryAddWithoutValidation("If-None-Match", _lastPulseEtag);

            using var res = await _http.SendAsync(req, ct).ConfigureAwait(false);
            if (res.StatusCode == HttpStatusCode.NotModified)
                return ApiResult<PulseResponse>.Success(new PulseResponse { NotModified = true });
            if (!res.IsSuccessStatusCode)
                return ApiResult<PulseResponse>.Fail($"HTTP {(int)res.StatusCode}");

            if (res.Headers.ETag != null)
                _lastPulseEtag = res.Headers.ETag.Tag;
            var json = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var pulse = JsonSerializer.Deserialize<PulseResponse>(json, JsonOpts) ?? new PulseResponse();
            return ApiResult<PulseResponse>.Success(pulse);
        }
        catch (Exception ex)
        {
            return ApiResult<PulseResponse>.Fail(ex.Message);
        }
    }

    public async Task MarkNotificationsReadAsync(CancellationToken ct = default)
    {
        var (_, doc, _) = await SendJsonAsync(HttpMethod.Post, "/api/community/notifications/read", "notifications:read", new { }, ct).ConfigureAwait(false);
        doc?.Dispose();
    }
}
