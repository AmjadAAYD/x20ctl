namespace X20Ctl.Zone;

public enum CurveChannel { LeftStick, RightStick, LeftTrigger, RightTrigger }
/// <summary>Frontend draft only. Percent deadzones are not wire bytes or a readback.</summary>
public sealed record CurveSetting(string Preset, int Inner, int Outer, (int X,int Y) Point1, (int X,int Y) Point2)
{
    public string BasePreset { get; init; } = Preset;
    public static readonly CurveSetting Default = new("default",0,100,(85,85),(170,170));
}
public sealed class CurveDraft(string modelId)
{
    private readonly Dictionary<CurveChannel,CurveSetting> channels = Enum.GetValues<CurveChannel>().ToDictionary(c=>c,_=>CurveSetting.Default);
    // UI preset coordinates from the preserved Python reference; this is not codec parity.
    public static readonly IReadOnlyDictionary<string,((int,int) First,(int,int) Second)> Presets =
        new Dictionary<string,((int,int),(int,int))> { ["default"] = ((85,85),(170,170)), ["quick"] = ((35,35),(136,204)), ["slow"] = ((35,35),(219,145)), ["smooth"] = ((142,142),(220,157)), ["fine"] = ((67,67),(72,106)) };
    public bool CanEdit => modelId == "x20";
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
