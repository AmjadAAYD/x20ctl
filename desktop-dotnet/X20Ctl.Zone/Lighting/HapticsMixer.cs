namespace X20Ctl.Zone.Lighting;

/// <summary>
/// Mixes <see cref="HapticsFusionEngine"/> 4-motor output cleanly with native PC/XInput game rumble
/// so triggers, textures, and explosions complement the game without overpowering or muddying grip motors.
/// </summary>
public static class HapticsMixer
{
    public static HapticsFrame Mix(
        HapticsFrame smart,
        double nativeLeftGrip,
        double nativeRightGrip,
        NativeRumbleBlendMode mode)
    {
        double natL = Math.Clamp(nativeLeftGrip, 0.0, 1.0);
        double natR = Math.Clamp(nativeRightGrip, 0.0, 1.0);
        double natPeak = Math.Max(natL, natR);

        return mode switch
        {
            NativeRumbleBlendMode.FullOverride => smart,

            NativeRumbleBlendMode.TriggersOnly => new HapticsFrame(
                natL,
                natR,
                Math.Clamp(smart.LeftTrigger, 0.0, 1.0),
                Math.Clamp(smart.RightTrigger, 0.0, 1.0)),

            NativeRumbleBlendMode.NativePriority => natPeak > 0.04
                ? new HapticsFrame(natL, natR, 0.0, 0.0)
                : smart,

            // SmartBlend (default): preserves native game grips while adding soft-knee texture accents and crisp triggers.
            _ => new HapticsFrame(
                BlendGrip(natL, smart.LeftGrip),
                BlendGrip(natR, smart.RightGrip),
                Math.Clamp(smart.LeftTrigger, 0.0, 1.0),
                Math.Clamp(smart.RightTrigger, 0.0, 1.0)),
        };
    }

    static double BlendGrip(double nativeGrip, double smartGrip)
    {
        double nat = Math.Clamp(nativeGrip, 0.0, 1.0);
        double smt = Math.Clamp(smartGrip, 0.0, 1.0);
        if (nat <= 0.03) return smt;
        if (smt <= 0.01) return nat;

        // Avoid additive clipping when a game already drives heavy rumble: blend smoothly with soft knee.
        double headroom = Math.Max(0.0, 1.0 - nat);
        double layered = nat + smt * headroom * 0.55;
        return Math.Clamp(Math.Max(nat, layered), 0.0, 1.0);
    }
}
