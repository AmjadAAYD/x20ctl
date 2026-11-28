using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace X20Ctl.Zone.Community;

public sealed class BadgeDef
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string IconSvg { get; set; } = "star";
    public string BgHex { get; set; } = "#1B2234";
    public string BorderHex { get; set; } = "#60A5FA";
    public string TextHex { get; set; } = "#DBEAFE";
    public string GlowHex { get; set; } = "#3B82F6";
    public string Rarity { get; set; } = "common";
    public string Mode { get; set; } = "auto";
    public string? RuleMetric { get; set; }
    public int? RuleThreshold { get; set; }
    public string? IconKey { get; set; }
    public string? ImageUrl { get; set; }
}

public sealed class CreatorStats
{
    public int ApprovedCount { get; set; }
    public int CurveCount { get; set; }
    public int ScreenCount { get; set; }
    public int MacroCount { get; set; }
    public int LightVibeCount { get; set; }
    public int PackCount { get; set; }
    public int InstallsReceived { get; set; }
    public int Karma { get; set; }
    public int WatchersCount { get; set; }
    public int RemixedByOthers { get; set; }
    public int RemixesPublished { get; set; }
    public int WorksConfirmed { get; set; }
    public int RequestsFulfilled { get; set; }
    public int NightOwl { get; set; }
    public int LoungePosts { get; set; }
    public int ZeroDeadzoneCount { get; set; }

    public int ReadMetric(string? metric) => metric switch
    {
        "approvedCount" => ApprovedCount,
        "curveCount" => CurveCount,
        "screenCount" => ScreenCount,
        "macroCount" => MacroCount,
        "lightVibeCount" => LightVibeCount,
        "packCount" => PackCount,
        "installsReceived" => InstallsReceived,
        "karma" => Karma,
        "watchersCount" => WatchersCount,
        "remixedByOthers" => RemixedByOthers,
        "remixesPublished" => RemixesPublished,
        "worksConfirmed" => WorksConfirmed,
        "requestsFulfilled" => RequestsFulfilled,
        "nightOwl" => NightOwl,
        "loungePosts" => LoungePosts,
        "zeroDeadzoneCount" => ZeroDeadzoneCount,
        _ => 0
    };
}

public sealed class CreatorProfile
{
    public string Uid { get; set; } = string.Empty;
    public string Handle { get; set; } = string.Empty;
    public bool IsOwner { get; set; }
    public bool IsSupporter { get; set; }
    public string RoleColor { get; set; } = "#E2E8F0";
    public string RoleTitle { get; set; } = "Member";
    public bool CanUseGifPfp { get; set; }
    public bool InstantApproval { get; set; }
    public bool IsOnline { get; set; }
    public List<string> Badges { get; set; } = new();
    public string? EquippedBadge { get; set; }
    public int Karma { get; set; }
    public int WatchersCount { get; set; }
    public int WatchingCount { get; set; }
    public int InstallsReceived { get; set; }
    public int ApprovedCount { get; set; }
    public bool HasPfp { get; set; }
    public string? PfpUrl { get; set; }
    public string TrustTier { get; set; } = "new";
    public bool IsWatching { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
}

public sealed class CommunityItem
{
    public string Id { get; set; } = string.Empty;
    public string Uid { get; set; } = string.Empty;
    public string Category { get; set; } = "setup";
    public string MediaType { get; set; } = "none";
    public string ModelId { get; set; } = "x20-pro";
    public string ConnectionMode { get; set; } = "native";
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public List<string> GameTags { get; set; } = new();
    public JsonElement? Payload { get; set; }
    public string? ShareCode { get; set; }
    public string? AssetUrl { get; set; }
    public string? AssetSha256 { get; set; }
    public int AssetBytes { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int FrameCount { get; set; }
    public string VtStatus { get; set; } = "skipped";
    public string? VtPermalink { get; set; }
    public string Status { get; set; } = "approved";
    public int Version { get; set; } = 1;
    public string? ParentItemId { get; set; }
    public string? RemixSourceId { get; set; }
    public int RemixCount { get; set; }
    public int Upvotes { get; set; }
    public int Downvotes { get; set; }
    public int Score { get; set; }
    public int Installs { get; set; }
    public int WorksCount { get; set; }
    public int CommentsCount { get; set; }
    public int FeaturedRank { get; set; }
    public CreatorProfile? Creator { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
    public string UpdatedAt { get; set; } = string.Empty;
}

public sealed class CommunityComment
{
    public string Id { get; set; } = string.Empty;
    public string ItemId { get; set; } = string.Empty;
    public string Uid { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
    public CreatorProfile? Creator { get; set; }
}

public sealed class CommunityRequest
{
    public string Id { get; set; } = string.Empty;
    public string Uid { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = "setup";
    public string ModelId { get; set; } = "x20-pro";
    public string ConnectionMode { get; set; } = "native";
    public string GameTag { get; set; } = string.Empty;
    public int Upvotes { get; set; }
    public string? FulfilledItemId { get; set; }
    public string Status { get; set; } = "open";
    public string CreatedAt { get; set; } = string.Empty;
    public CreatorProfile? Creator { get; set; }
}

public sealed class OwnerNote
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool Pinned { get; set; }
    public int HeartsCount { get; set; }
    public int ThumbsCount { get; set; }
    public int FireCount { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
}

public sealed class PollOption
{
    public string Label { get; set; } = string.Empty;
    public int Votes { get; set; }
}

public sealed class CommunityPoll
{
    public string Id { get; set; } = string.Empty;
    public string Question { get; set; } = string.Empty;
    public List<PollOption> Options { get; set; } = new();
    public bool Closed { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
}

public sealed class CommunityChallenge
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = "screen";
    public string? EndsAt { get; set; }
    public string? WinnerItemId { get; set; }
    public string Status { get; set; } = "active";
}

public sealed class LoungeMessage
{
    public string Id { get; set; } = string.Empty;
    public string Uid { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Channel { get; set; } = "general";
    public string? RecipientUid { get; set; }
    public string? ReplyToId { get; set; }
    public string? ReplyToHandle { get; set; }
    public string? ReplyToBody { get; set; }
    public Dictionary<string, List<string>> Reactions { get; set; } = new();
    public bool Pinned { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
    public CreatorProfile? Creator { get; set; }
}

public sealed class ChatChannelInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public bool OwnerOnly { get; set; }
}

public sealed class ChatChannelResponse
{
    public string Channel { get; set; } = "general";
    public string? PeerUid { get; set; }
    public List<ChatChannelInfo> Channels { get; set; } = new();
    public List<CreatorProfile> DmPeers { get; set; } = new();
    public List<LoungeMessage> Messages { get; set; } = new();
    public int SlowmodeSeconds { get; set; } = 15;
    public bool Paused { get; set; }
}

public sealed class CommunityMembersResponse
{
    public int OnlineCount { get; set; }
    public int TotalMembers { get; set; }
    public List<CreatorProfile> Online { get; set; } = new();
    public List<CreatorProfile> Offline { get; set; } = new();
}

public sealed class NotificationItem
{
    public string Id { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? RefId { get; set; }
    public bool IsRead { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
}

public sealed class PulseResponse
{
    public int OnlineCount { get; set; }
    public int UnreadCount { get; set; }
    public List<NotificationItem> Notifications { get; set; } = new();
    public string? LatestNoteId { get; set; }
    public string LatestItemAt { get; set; } = string.Empty;
    public bool OgV0026Open { get; set; } = true;
    public int FeaturedCount { get; set; }
    [JsonIgnore] public bool NotModified { get; set; }
}

public sealed class CatalogResponse
{
    public List<CommunityItem> Items { get; set; } = new();
    public List<OwnerNote> Notes { get; set; } = new();
    public List<CommunityPoll> Polls { get; set; } = new();
    public List<CommunityChallenge> Challenges { get; set; } = new();
    public List<BadgeDef> BadgeCatalog { get; set; } = new();
    public bool OgV0026Open { get; set; } = true;
    public int OnlineCount { get; set; }
}

public sealed class CatalogQuery
{
    public string? Category { get; set; }
    public string? ModelId { get; set; }
    public string? ConnectionMode { get; set; }
    public string? MediaType { get; set; }
    public string? Search { get; set; }
    public string Sort { get; set; } = "trending";
    public string? GameTag { get; set; }
    public string? Uid { get; set; }
    public string? WatchingOf { get; set; }
    public string? BookmarkedBy { get; set; }
    public string? Status { get; set; }
}

public sealed class PowChallenge
{
    public string Token { get; set; } = string.Empty;
    public int Difficulty { get; set; } = 3;
    public string Action { get; set; } = string.Empty;
}

public sealed class ApiResult<T>
{
    public bool Ok { get; init; }
    public T? Data { get; init; }
    public string? Error { get; init; }

    public static ApiResult<T> Success(T data) => new() { Ok = true, Data = data };
    public static ApiResult<T> Fail(string error) => new() { Ok = false, Error = error };
}
