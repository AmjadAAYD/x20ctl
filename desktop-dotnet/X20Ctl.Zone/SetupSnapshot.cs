using System.Text.Json;
using System.Text.Json.Serialization;
namespace X20Ctl.Zone;
public sealed record SetupCurve(string Preset,string BasePreset,int Inner,int Outer,int X1,int Y1,int X2,int Y2);
public sealed record SetupMacro(Guid Id,string[] Buttons,StickHeading Left,StickHeading Right,int HoldMs,int PauseMs);
public sealed class SetupSnapshot
{
    public string ModelId {get;set;}="x20";
    public Dictionary<string,string> Mappings {get;set;}=new();
    public Dictionary<string,SetupCurve> Curves {get;set;}=new();
    public Dictionary<string,SetupMacro[]> Macros {get;set;}=new();
    public int Vibration {get;set;}=70;
    /// <summary>PC mode's per-motor shares and minimum (absent in older setups).</summary>
    public int[]? Motors {get;set;}
    /// <summary>PC mode: the button that plays each macro slot (absent in older setups).</summary>
    public Dictionary<string,string?>? Triggers {get;set;}
    [JsonExtensionData]public Dictionary<string,JsonElement>? Extra {get;set;}
    public static SetupSnapshot Capture(string model,ButtonDraft buttons,CurveDraft curves,MacroDraft macros,VibrationDraft vibration)=>new()
    {
        ModelId=model,Mappings=ButtonDraft.Sources.ToDictionary(k=>k,buttons.Target),
        Curves=Enum.GetValues<CurveChannel>().ToDictionary(c=>c.ToString(),c=>{var v=curves[c];return new SetupCurve(v.Preset,v.BasePreset,v.Inner,v.Outer,v.Point1.X,v.Point1.Y,v.Point2.X,v.Point2.Y);}),
        Macros=macros.SlotNames.ToDictionary(s=>s,s=>macros[s].Select(v=>new SetupMacro(v.Id,v.Buttons.ToArray(),v.Left,v.Right,v.HoldMs,v.PauseMs)).ToArray()),Vibration=vibration.Strength,Motors=vibration.PcMode?[vibration.Left,vibration.Right,vibration.Floor]:null,
        Triggers=macros.PcMode?macros.SlotNames.ToDictionary(s=>s,macros.Trigger):null
    };
    public void Validate()
    {
        bool pc=ModelId is not ("x20" or "x20_pro") && X20Ctl.Zone.PcMode.PcModeEntry.IsAvailable(ModelId);
        if(ModelId is not ("x20" or "x20_pro") && !pc || Mappings==null || Curves==null || Macros==null || Curves.Values.Any(v=>v==null) || Macros.Values.Any(v=>v==null || v.Any(e=>e==null||e.Buttons==null)) || !Mappings.Keys.Order().SequenceEqual(ButtonDraft.Sources.Order()) || !Curves.Keys.Order().SequenceEqual(Enum.GetNames<CurveChannel>().Order()) || !Macros.Keys.Order().SequenceEqual(new MacroDraft(ModelId).SlotNames.Order()))throw new InvalidDataException("Unsupported native setup model or incomplete fields.");
        var buttons=new ButtonDraft(ModelId){PcMode=pc};foreach(var (key,value) in Mappings)buttons.Set(key,value);
        var curves=new CurveDraft(ModelId){PcMode=pc};foreach(var (key,value) in Curves)curves.Set(Enum.Parse<CurveChannel>(key),new(value.Preset,value.Inner,value.Outer,(value.X1,value.Y1),(value.X2,value.Y2)){BasePreset=value.BasePreset});
        var macros=new MacroDraft(ModelId){PcMode=pc};foreach(var (slot,events) in Macros)foreach(var step in events){macros.Add(slot,step.PauseMs);macros.Update(slot,macros[slot].Count-1,new(step.Id,step.Buttons,step.Left,step.Right,step.HoldMs,step.PauseMs));}
        var v=new VibrationDraft(ModelId){PcMode=pc};v.Set(Vibration);if(pc&&Motors is {Length:3})v.SetMotors(Motors[0],Motors[1],Motors[2]);
    }
    public void LoadInto(ButtonDraft buttons,CurveDraft curves,MacroDraft macros,VibrationDraft vibration)
    {
        Validate();buttons.ResetAll();foreach(var (key,value) in Mappings)buttons.Set(key,value);
        foreach(var (key,value) in Curves)curves.Set(Enum.Parse<CurveChannel>(key),new(value.Preset,value.Inner,value.Outer,(value.X1,value.Y1),(value.X2,value.Y2)){BasePreset=value.BasePreset});
        foreach(var slot in macros.SlotNames){macros.Clear(slot);foreach(var step in Macros[slot]){macros.Add(slot,step.PauseMs);macros.Update(slot,macros[slot].Count-1,new(step.Id,step.Buttons,step.Left,step.Right,step.HoldMs,step.PauseMs));}}
        if(Triggers!=null&&macros.PcMode)foreach(var (slot,button) in Triggers)if(macros.SlotNames.Contains(slot))macros.SetTrigger(slot,button);
        vibration.Set(Vibration);
        if(vibration.PcMode&&Motors is {Length:3})vibration.SetMotors(Motors[0],Motors[1],Motors[2]);
    }
}
