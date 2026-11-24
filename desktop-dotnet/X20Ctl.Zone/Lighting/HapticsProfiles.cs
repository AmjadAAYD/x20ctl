namespace X20Ctl.Zone.Lighting;

public enum HapticsGenreProfile
{
    Auto,
    Racing,
    FpsAction,
    Music,
    Cinema,
    RpgMelee,
    SportsArcade,
    Off,
}

public enum HapticsComfortTier
{
    Feather,
    Balanced,
    FullPunch,
}

public enum NativeRumbleBlendMode
{
    SmartBlend,
    TriggersOnly,
    FullOverride,
    NativePriority,
}

public enum HapticsEventTag
{
    None,
    Gunshot,
    Explosion,
    GravelLeft,
    GravelRight,
    BoostSurge,
    GearShift,
    AbsBrake,
    Footstep,
    MeleeClash,
    Vocal,
    BassDrop,
    MenuMuted,
    DialogueDuck,
}

public enum HapticsDemoKind
{
    DirtRoad,
    GunshotBurst,
    NitroBoost,
    AbsBrake,
    ExplosionWave,
    BassDrop,
}

public sealed record HapticsTuning
{
    public int MasterIntensity { get; init; } = 75;
    public int TextureDetail { get; init; } = 45;
    public int ImpactPunch { get; init; } = 70;
    public int DynamicContrast { get; init; } = 75;
    public int VocalFocus { get; init; } = 80;
    public int TriggerBalance { get; init; } = 85;
    public HapticsComfortTier ComfortTier { get; init; } = HapticsComfortTier.Feather;
    public NativeRumbleBlendMode BlendMode { get; init; } = NativeRumbleBlendMode.SmartBlend;
    public bool MenuAutoMute { get; init; } = true;

    public HapticsTuning Normalized() => this with
    {
        MasterIntensity = Math.Clamp(MasterIntensity, 0, 100),
        TextureDetail = Math.Clamp(TextureDetail, 0, 100),
        ImpactPunch = Math.Clamp(ImpactPunch, 0, 100),
        DynamicContrast = Math.Clamp(DynamicContrast, 0, 100),
        VocalFocus = Math.Clamp(VocalFocus, 0, 100),
        TriggerBalance = Math.Clamp(TriggerBalance, 0, 100),
    };

    public static HapticsTuning DefaultFor(HapticsGenreProfile profile) => profile switch
    {
        HapticsGenreProfile.Racing => new()
        {
            MasterIntensity = 78,
            TextureDetail = 45,
            ImpactPunch = 72,
            DynamicContrast = 80,
            TriggerBalance = 90,
            ComfortTier = HapticsComfortTier.Feather,
        },
        HapticsGenreProfile.FpsAction => new()
        {
            MasterIntensity = 75,
            TextureDetail = 35,
            ImpactPunch = 78,
            DynamicContrast = 85,
            TriggerBalance = 92,
            ComfortTier = HapticsComfortTier.Feather,
        },
        HapticsGenreProfile.Music => new()
        {
            MasterIntensity = 80,
            TextureDetail = 55,
            ImpactPunch = 75,
            DynamicContrast = 82,
            VocalFocus = 85,
            TriggerBalance = 85,
            ComfortTier = HapticsComfortTier.Balanced,
        },
        HapticsGenreProfile.Cinema => new()
        {
            MasterIntensity = 72,
            TextureDetail = 35,
            ImpactPunch = 76,
            DynamicContrast = 85,
            TriggerBalance = 75,
            ComfortTier = HapticsComfortTier.Feather,
        },
        HapticsGenreProfile.RpgMelee => new()
        {
            MasterIntensity = 74,
            TextureDetail = 40,
            ImpactPunch = 74,
            DynamicContrast = 80,
            TriggerBalance = 85,
            ComfortTier = HapticsComfortTier.Feather,
        },
        HapticsGenreProfile.SportsArcade => new()
        {
            MasterIntensity = 72,
            TextureDetail = 38,
            ImpactPunch = 70,
            DynamicContrast = 75,
            TriggerBalance = 82,
            ComfortTier = HapticsComfortTier.Feather,
        },
        _ => new(),
    };
}

[Flags]
public enum PadActionButtons
{
    None = 0,
    A = 1 << 0,
    B = 1 << 1,
    X = 1 << 2,
    Y = 1 << 3,
    LB = 1 << 4,
    RB = 1 << 5,
    L3 = 1 << 6,
    R3 = 1 << 7,
}

public readonly record struct LivePadInput(
    double LeftStickX,
    double LeftStickY,
    double RightStickX,
    double RightStickY,
    double LeftTrigger,
    double RightTrigger,
    int HeldButtons,
    bool AnyNewPress,
    bool CursorVisible = false,
    bool IsIdle = false,
    PadActionButtons PressedButtons = PadActionButtons.None,
    bool IsGameForeground = false)
{
    public double LeftStickMagnitude => Math.Clamp(Math.Sqrt(LeftStickX * LeftStickX + LeftStickY * LeftStickY), 0.0, 1.0);
    public double RightStickMagnitude => Math.Clamp(Math.Sqrt(RightStickX * RightStickX + RightStickY * RightStickY), 0.0, 1.0);
}

public readonly record struct HapticsEvaluation(
    HapticsFrame Motors,
    AudioClassification Classification,
    HapticsEventTag PrimaryEvent,
    HapticsEventTag SecondaryEvent,
    HapticsGenreProfile ResolvedProfile,
    bool IsMenuMuted,
    double AverageDutyCycle);

public static class HapticsProfileLabels
{
    public static string ProfileName(HapticsGenreProfile p) => p switch
    {
        HapticsGenreProfile.Auto => "Auto Hybrid",
        HapticsGenreProfile.Racing => "Racing & Drift",
        HapticsGenreProfile.FpsAction => "FPS & Action",
        HapticsGenreProfile.Music => "Music & Vocals",
        HapticsGenreProfile.Cinema => "Cinema & Film",
        HapticsGenreProfile.RpgMelee => "RPG & Melee",
        HapticsGenreProfile.SportsArcade => "Sports & Arcade",
        HapticsGenreProfile.Off => "Off",
        _ => p.ToString(),
    };

    public static string ComfortName(HapticsComfortTier tier) => tier switch
    {
        HapticsComfortTier.Feather => "Feather (Battery Saver)",
        HapticsComfortTier.Balanced => "Balanced",
        HapticsComfortTier.FullPunch => "Full Punch",
        _ => tier.ToString(),
    };

    public static string BlendName(NativeRumbleBlendMode mode) => mode switch
    {
        NativeRumbleBlendMode.SmartBlend => "Smart Blend",
        NativeRumbleBlendMode.TriggersOnly => "Triggers Only",
        NativeRumbleBlendMode.FullOverride => "Full X20Ctl",
        NativeRumbleBlendMode.NativePriority => "Native Priority",
        _ => mode.ToString(),
    };

    public static string EventLabel(HapticsEventTag tag) => tag switch
    {
        HapticsEventTag.Gunshot => "GUNSHOT",
        HapticsEventTag.Explosion => "EXPLOSION",
        HapticsEventTag.GravelLeft => "GRAVEL L",
        HapticsEventTag.GravelRight => "GRAVEL R",
        HapticsEventTag.BoostSurge => "BOOST",
        HapticsEventTag.GearShift => "GEAR SHIFT",
        HapticsEventTag.AbsBrake => "ABS BRAKE",
        HapticsEventTag.Footstep => "FOOTSTEP",
        HapticsEventTag.MeleeClash => "PARRY",
        HapticsEventTag.Vocal => "VOCAL",
        HapticsEventTag.BassDrop => "BASS DROP",
        HapticsEventTag.MenuMuted => "MENU MUTE",
        HapticsEventTag.DialogueDuck => "DIALOGUE",
        _ => "IDLE",
    };
}
