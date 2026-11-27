using System;
using System.Collections.Generic;
using System.Linq;

namespace X20Ctl.Zone.Community;

public sealed record CommunityModelInfo(string Id, string DisplayName, bool SupportsNative);

public sealed record CommunityCapabilitySet(
    string ModelId,
    string ModelName,
    string ConnectionMode,
    IReadOnlyList<string> Features,
    IReadOnlyList<string> Categories)
{
    public bool Supports(string feature) => Features.Contains(feature, StringComparer.OrdinalIgnoreCase);
    public bool AllowsCategory(string category) => Categories.Contains(category, StringComparer.OrdinalIgnoreCase);
}

public static class CommunityCapabilities
{
    public static IReadOnlyList<CommunityModelInfo> Models { get; } = new[]
    {
        new CommunityModelInfo("x20-pro", "X20 Pro", true),
        new CommunityModelInfo("x20", "X20", true),
        new CommunityModelInfo("x05-pro", "X05 Pro", false),
        new CommunityModelInfo("x10", "X10", true),
        new CommunityModelInfo("x15", "X15", false),
        new CommunityModelInfo("d10", "D10", true),
        new CommunityModelInfo("dune-8k", "Dune 8K", false),
        new CommunityModelInfo("generic-xinput", "Generic XInput", false)
    };

    private static readonly string[] NativeX20ProFeatures =
        { "buttons", "curves", "keyboard", "mouse", "macros", "rgb", "vibration", "screen", "gyros", "battery" };
    private static readonly string[] NativeX20Features =
        { "buttons", "curves", "macros" };
    private static readonly string[] NativeX10Features =
        { "buttons", "curves", "macros", "vibration" };
    private static readonly string[] NativeD10Features =
        { "buttons", "curves", "macros", "vibration", "gyros" };
    private static readonly string[] PcModeFeatures =
        { "buttons", "curves", "keyboard", "mouse", "macros" };

    private static readonly string[] NativeX20ProCategories =
        { "setup", "curve", "screen", "rgb", "vibration", "macro", "pack" };
    private static readonly string[] NativeX10Categories =
        { "setup", "curve", "vibration", "macro", "pack" };
    private static readonly string[] LimitedCategories =
        { "setup", "curve", "macro", "pack" };

    public static string NormalizeModelId(string? modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId)) return "x20-pro";
        return modelId.Trim().ToLowerInvariant() switch
        {
            "x20_pro" or "x20pro" or "x20-pro" => "x20-pro",
            "x20" => "x20",
            "x05_pro" or "x05-pro" => "x05-pro",
            "x10" => "x10",
            "x15" => "x15",
            "d10" => "d10",
            "dune_8k" or "dune-8k" => "dune-8k",
            _ when Models.Any(m => m.Id == modelId.Trim().ToLowerInvariant()) => modelId.Trim().ToLowerInvariant(),
            _ => "generic-xinput"
        };
    }

    public static CommunityModelInfo? GetModel(string? modelId)
    {
        var norm = NormalizeModelId(modelId);
        return Models.FirstOrDefault(m => m.Id == norm);
    }

    public static CommunityCapabilitySet GetCapabilitySet(string? modelId, string? connectionMode)
    {
        var model = GetModel(modelId) ?? Models[0];
        var mode = string.Equals(connectionMode, "native", StringComparison.OrdinalIgnoreCase) && model.SupportsNative
            ? "native"
            : "pc-mode";

        if (mode == "native" && model.Id == "x20-pro")
        {
            return new CommunityCapabilitySet(model.Id, model.DisplayName, mode, NativeX20ProFeatures, NativeX20ProCategories);
        }
        if (mode == "native" && model.Id == "x20")
        {
            return new CommunityCapabilitySet(model.Id, model.DisplayName, mode, NativeX20Features, LimitedCategories);
        }
        if (mode == "native" && model.Id == "x10")
        {
            return new CommunityCapabilitySet(model.Id, model.DisplayName, mode, NativeX10Features, NativeX10Categories);
        }
        if (mode == "native" && model.Id == "d10")
        {
            return new CommunityCapabilitySet(model.Id, model.DisplayName, mode, NativeD10Features, NativeX10Categories);
        }
        return new CommunityCapabilitySet(model.Id, model.DisplayName, "pc-mode", PcModeFeatures, LimitedCategories);
    }
}
