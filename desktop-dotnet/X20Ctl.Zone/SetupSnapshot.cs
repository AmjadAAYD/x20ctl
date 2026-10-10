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
    [JsonExtensionData]public Dictionary<string,JsonElement>? Extra {get;set;}
    public static SetupSnapshot Capture(string model,ButtonDraft buttons,CurveDraft curves,MacroDraft macros,VibrationDraft vibration)=>new()
    {
        ModelId=model,Mappings=ButtonDraft.Sources.ToDictionary(k=>k,buttons.Target),
        Curves=Enum.GetValues<CurveChannel>().ToDictionary(c=>c.ToString(),c=>{var v=curves[c];return new SetupCurve(v.Preset,v.BasePreset,v.Inner,v.Outer,v.Point1.X,v.Point1.Y,v.Point2.X,v.Point2.Y);}),
        Macros=MacroDraft.Slots.ToDictionary(s=>s,s=>macros[s].Select(v=>new SetupMacro(v.Id,v.Buttons.ToArray(),v.Left,v.Right,v.HoldMs,v.PauseMs)).ToArray()),Vibration=vibration.Strength
    };
    public void Validate()
    {
        if(ModelId!="x20" || Mappings==null || Curves==null || Macros==null || Curves.Values.Any(v=>v==null) || Macros.Values.Any(v=>v==null || v.Any(e=>e==null||e.Buttons==null)) || !Mappings.Keys.Order().SequenceEqual(ButtonDraft.Sources.Order()) || !Curves.Keys.Order().SequenceEqual(Enum.GetNames<CurveChannel>().Order()) || !Macros.Keys.Order().SequenceEqual(MacroDraft.Slots.Order()))throw new InvalidDataException("Unsupported native setup model or incomplete fields.");
        var buttons=new ButtonDraft(ModelId);foreach(var (key,value) in Mappings)buttons.Set(key,value);
        var curves=new CurveDraft(ModelId);foreach(var (key,value) in Curves)curves.Set(Enum.Parse<CurveChannel>(key),new(value.Preset,value.Inner,value.Outer,(value.X1,value.Y1),(value.X2,value.Y2)){BasePreset=value.BasePreset});
        var macros=new MacroDraft(ModelId);foreach(var (slot,events) in Macros)foreach(var step in events){macros.Add(slot,step.PauseMs);macros.Update(slot,macros[slot].Count-1,new(step.Id,step.Buttons,step.Left,step.Right,step.HoldMs,step.PauseMs));}
        new VibrationDraft(ModelId).Set(Vibration);
    }
    public void LoadInto(ButtonDraft buttons,CurveDraft curves,MacroDraft macros,VibrationDraft vibration)
    {
        Validate();buttons.ResetAll();foreach(var (key,value) in Mappings)buttons.Set(key,value);
        foreach(var (key,value) in Curves)curves.Set(Enum.Parse<CurveChannel>(key),new(value.Preset,value.Inner,value.Outer,(value.X1,value.Y1),(value.X2,value.Y2)){BasePreset=value.BasePreset});
        foreach(var slot in MacroDraft.Slots){macros.Clear(slot);foreach(var step in Macros[slot]){macros.Add(slot,step.PauseMs);macros.Update(slot,macros[slot].Count-1,new(step.Id,step.Buttons,step.Left,step.Right,step.HoldMs,step.PauseMs));}}
        vibration.Set(Vibration);
    }
}
