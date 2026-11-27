namespace X20Ctl.Zone;

public enum CurveChannel { LeftStick, RightStick, LeftTrigger, RightTrigger }

[Flags]
public enum CurveHardwareFlags : byte
{
    None = 0,
    InvertX = 0x01,
    InvertY = 0x02,
    SwapSticks = 0x04
}

/// <summary>Frontend draft and hardware channel encoding (bits 0-2 of wire byte 6 hold InvertX, InvertY, SwapSticks).</summary>
public sealed record CurveSetting(string Preset, int Inner, int Outer, (int X,int Y) Point1, (int X,int Y) Point2)
{
    public string BasePreset { get; init; } = Preset;
    public byte Flags { get; init; }
    public static readonly CurveSetting Default = new("default",0,100,(85,85),(170,170));
}
public sealed class CurveDraft(string modelId)
{
    private readonly Dictionary<CurveChannel,CurveSetting> channels = Enum.GetValues<CurveChannel>().ToDictionary(c=>c,_=>CurveSetting.Default);
    // Points the curve runs through (PadProcessor.Through), 0-255; not codec parity. Reshaped 14 Oct 2026: the Python
    // reference's points were Bezier handles, and drawn through they made "smooth" a plateau and "fine" a spike.
    public static readonly IReadOnlyDictionary<string,((int,int) First,(int,int) Second)> Presets =
        new Dictionary<string,((int,int),(int,int))> { ["default"] = ((85,85),(170,170)), ["quick"] = ((64,102),(166,217)), ["slow"] = ((89,51),(191,140)), ["smooth"] = ((77,59),(178,196)), ["fine"] = ((77,89),(178,196)) };
    /// <summary>PC mode applies the draft on the PC, so any model can be edited while it's on.</summary>
    public bool PcMode { get; set; }
    public bool CanEdit => modelId is "x20" or "x20_pro" or "x10" || PcMode;
    public bool CanApplyHardware => false;
    public int ModifiedChannels => channels.Values.Count(c=>c!=CurveSetting.Default);
    public CurveSetting this[CurveChannel channel] => channels[channel];
    public void Set(CurveChannel channel, CurveSetting value)
    {
        if (!CanEdit) throw new InvalidOperationException("Curve configuration is unverified for this model.");
        if(value.Inner<0 || value.Outer>100 || value.Inner>=value.Outer || value.Point1.X>value.Point2.X || new[]{value.Point1.X,value.Point1.Y,value.Point2.X,value.Point2.Y}.Any(v=>v<0||v>255))
            throw new ArgumentException("Invalid local curve travel or control points.");
        channels[channel]=value;
    }
    public void SetPreset(CurveChannel channel,string preset)
    {
        var points=Presets[preset]; Set(channel,this[channel] with {Preset=preset,BasePreset=preset,Point1=points.First,Point2=points.Second});
    }
    public void Reset(CurveChannel channel) { if(CanEdit) channels[channel]=CurveSetting.Default; }
    public void ResetAll() { foreach(var channel in Enum.GetValues<CurveChannel>()) Reset(channel); }
}
