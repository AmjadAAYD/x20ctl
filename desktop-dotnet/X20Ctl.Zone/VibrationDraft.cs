namespace X20Ctl.Zone;
/// <summary>Linked frontend strength percentage and independent left/right motor levels (native on X10 or in PC mode).
/// In PC mode it also shapes a game's own vibration up to 200% with a minimum floor.</summary>
public sealed class VibrationDraft(string modelId)
{
    public const int DefaultStrength = 70; // Preserved UI profile default, not hardware state.
    public int Strength { get; private set; } = DefaultStrength;
    public int Left { get; private set; } = 100;
    public int Right { get; private set; } = 100;
    public int Floor { get; private set; }
    public bool Modified => Strength != DefaultStrength || Left != 100 || Right != 100 || Floor != 0;
    /// <summary>PC mode scales a game's vibration on the PC, so any model can set it while PC mode is on.</summary>
    public bool PcMode { get; set; }
    public bool SupportsNativeMotors => modelId is "x10" or "d10";
    public bool CanEdit => modelId is "x20" or "x20_pro" or "x10" or "d10" || PcMode;
    /// <summary>On the pad itself 100% is the most; PC mode can boost a game's rumble up to 200%.</summary>
    public int Max => PcMode ? X20Ctl.Zone.PcMode.PcModeProfile.MaxVibration : 100;
    public bool CanApplyHardware => modelId is "x10" or "d10" && !PcMode;
    public bool CanTestHardware => modelId is "x10" or "d10" && !PcMode;

    public void Set(int percent)
    {
        if (!CanEdit) throw new InvalidOperationException("Vibration configuration is unverified for this model.");
        if (percent < 0 || percent > Max) throw new ArgumentOutOfRangeException(nameof(percent));
        Strength = percent;
        if (!PcMode && SupportsNativeMotors)
        {
            Left = percent;
            Right = percent;
        }
    }

    /// <summary>Per-motor intensities (X10 native 0..100 or PC mode 0..200) plus PC-mode minimum floor.</summary>
    public void SetMotors(int left, int right, int floor)
    {
        if (!PcMode && !SupportsNativeMotors) throw new InvalidOperationException("Motor settings need PC mode or X10 native.");
        if (left < 0 || left > Max || right < 0 || right > Max) throw new ArgumentOutOfRangeException(nameof(left));
        if (floor < 0 || floor > X20Ctl.Zone.PcMode.PcModeProfile.MaxVibrationFloor) throw new ArgumentOutOfRangeException(nameof(floor));
        Left = left;
        Right = right;
        Floor = PcMode ? floor : 0;
        if (!PcMode && SupportsNativeMotors)
            Strength = (left + right) / 2;
    }

    public void Reset()
    {
        if (!CanEdit) return;
        Strength = DefaultStrength;
        Left = Right = 100;
        Floor = 0;
    }
}
