namespace X20Ctl.Zone;
/// <summary>Linked frontend strength percentage. No motor readback or wire encoding. In PC mode it also shapes a game's
/// own vibration: up to 200%, each motor on its own, and a minimum for faint effects.</summary>
public sealed class VibrationDraft(string modelId)
{
    public const int DefaultStrength=70; // Preserved UI profile default, not hardware state.
    public int Strength {get;private set;}=DefaultStrength;
    public int Left {get;private set;}=100;
    public int Right {get;private set;}=100;
    public int Floor {get;private set;}
    public bool Modified=>Strength!=DefaultStrength||Left!=100||Right!=100||Floor!=0;
    /// <summary>PC mode scales a game's vibration on the PC, so any model can set it while PC mode is on.</summary>
    public bool PcMode {get;set;}
    public bool CanEdit=>modelId is "x20" or "x20_pro" || PcMode;
    /// <summary>On the pad itself 100% is the most; PC mode can boost a game's rumble up to 200%.</summary>
    public int Max=>PcMode?X20Ctl.Zone.PcMode.PcModeProfile.MaxVibration:100;
    public bool CanApplyHardware=>false;
    public bool CanTestHardware=>false;
    public void Set(int percent)
    {
        if(!CanEdit)throw new InvalidOperationException("Vibration configuration is unverified for this model.");
        if(percent<0||percent>Max)throw new ArgumentOutOfRangeException(nameof(percent));Strength=percent;
    }
    /// <summary>PC mode only: each motor's share of the game's rumble, and the minimum for faint effects.</summary>
    public void SetMotors(int left,int right,int floor)
    {
        if(!PcMode)throw new InvalidOperationException("Motor settings need PC mode.");
        if(left<0||left>Max||right<0||right>Max)throw new ArgumentOutOfRangeException(nameof(left));
        if(floor<0||floor>X20Ctl.Zone.PcMode.PcModeProfile.MaxVibrationFloor)throw new ArgumentOutOfRangeException(nameof(floor));
        Left=left;Right=right;Floor=floor;
    }
    public void Reset(){if(!CanEdit)return;Strength=DefaultStrength;Left=Right=100;Floor=0;}
}
