namespace X20Ctl.Zone.PcMode;

/// <summary>Rules for the PC Mode entry (rework plan, 6 Nov 2026): which pads get it, which virtual pad types exist, and the warning shown before enabling.</summary>
public static class PcModeEntry
{
    /// <summary>The X20 and X20 Pro keep their settings on the pad, so they have no PC Mode. Every other controller is
    /// configured through PC mode (owner, 7 Nov 2026: "it must be universal ... except X20 Pro and X20").</summary>
    // Lab (owner, 8 Nov 2026: "bring back PC mode"): the X20 Pro has PC mode again, as it did before 7 Nov. With PC mode
    // off it still saves to the controller itself; with it on, its tabs edit PC mode. Only the X20 is left out.
    public static bool IsAvailable(string modelId) => modelId is not "x20";

    public enum VirtualType { Xbox360, DualShock4 }

    /// <summary>Types the driver can emulate (ViGEmBus: Xbox 360 and DualShock 4). Only types whose output is written are offered; a PS5 pad is not listed because no emulation of it is known.</summary>
    public static IReadOnlyList<VirtualType> Offered { get; } = [VirtualType.Xbox360, VirtualType.DualShock4];

    static volatile VirtualType chosen = VirtualType.Xbox360;
    /// <summary>The virtual pad type the owner picked in the PC Mode window. New virtual pads use it; it applies the next time they are created.</summary>
    public static VirtualType Chosen { get => chosen; set => chosen = value; }

    /// <summary>How the choice is saved in the preferences file; anything unknown or missing is the Xbox 360 pad.</summary>
    public static string SavedId(VirtualType type) => type == VirtualType.DualShock4 ? "dualshock4" : "xbox360";
    public static VirtualType FromSaved(string? id) => id == "dualshock4" ? VirtualType.DualShock4 : VirtualType.Xbox360;

    public static string DisplayName(VirtualType type) => type switch
    {
        VirtualType.Xbox360 => "Xbox",
        VirtualType.DualShock4 => "PlayStation 4",
        _ => type.ToString(),
    };

    /// <summary>Shown every time before PC Mode is enabled. The owner's wording: it may be bannable, use at your own risk.</summary>
    public const string Warning =
        "PC Mode presents a virtual controller to games. Some games and anti-cheat systems may treat this as cheating and could ban your account. " +
        "Use at your own risk. X20CTL is not responsible for how it is used.";
}
