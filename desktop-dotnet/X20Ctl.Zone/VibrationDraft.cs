namespace X20Ctl.Zone;
/// <summary>Linked frontend strength percentage. No motor readback or wire encoding.</summary>
public sealed class VibrationDraft(string modelId)
{
    public const int DefaultStrength=70; // Preserved UI profile default, not hardware state.
    public int Strength {get;private set;}=DefaultStrength;
    public bool Modified=>Strength!=DefaultStrength;
    public bool CanEdit=>modelId=="x20";
    public bool CanApplyHardware=>false;
    public bool CanTestHardware=>false;
    public void Set(int percent)
    {
        if(!CanEdit)throw new InvalidOperationException("Vibration configuration is unverified for this model.");
        if(percent is <0 or >100)throw new ArgumentOutOfRangeException(nameof(percent));Strength=percent;
    }
    public void Reset(){if(CanEdit)Strength=DefaultStrength;}
}
