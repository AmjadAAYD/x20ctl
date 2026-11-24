namespace X20Ctl.Zone.Lighting;

/// <summary>
/// Real battery-saving policy for wireless controllers: halves USB/2.4G haptic HID rate (25 Hz),
/// tightens idle cutoff (1.5 s), trims continuous motor power/caps, and gates out sub-6% micro-buzz.
/// </summary>
public static class HapticsPowerSaver
{
    public static int TickIntervalMs(bool powerSavingActive) => powerSavingActive ? 40 : 20;

    public static double IdleCutoffSeconds(bool powerSavingActive) => powerSavingActive ? 1.5 : 4.5;

    public static bool IsLowBatteryText(string? levelText, int? percent = null)
    {
        if (percent is >= 0 and <= 20)
            return true;
        if (string.IsNullOrWhiteSpace(levelText))
            return false;

        string norm = levelText.Trim();
        return norm.Equals("low", StringComparison.OrdinalIgnoreCase)
            || norm.Equals("empty", StringComparison.OrdinalIgnoreCase)
            || norm.StartsWith("1/4", StringComparison.OrdinalIgnoreCase);
    }

    public static HapticsFrame Apply(HapticsFrame frame, bool powerSavingActive)
    {
        if (!powerSavingActive)
            return frame;

        static double EcoChannel(double value, double cap)
        {
            double scaled = Math.Min(Math.Clamp(value, 0.0, 1.0) * 0.50, cap);
            return scaled < 0.06 ? 0.0 : scaled;
        }

        return new HapticsFrame(
            LeftGrip: EcoChannel(frame.LeftGrip, 0.35),
            RightGrip: EcoChannel(frame.RightGrip, 0.48),
            LeftTrigger: EcoChannel(frame.LeftTrigger, 0.45),
            RightTrigger: EcoChannel(frame.RightTrigger, 0.45));
    }
}
