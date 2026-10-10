namespace X20Ctl.Zone;

public enum StickHeading { Neutral, Up, UpRight, Right, DownRight, Down, DownLeft, Left, UpLeft }
/// <summary>Local UI event, not a wire mask. Neutral sticks are explicit.</summary>
public sealed record MacroEvent(Guid Id,IReadOnlyList<string> Buttons,StickHeading Left,StickHeading Right,int HoldMs,int PauseMs);
public sealed class MacroDraft(string modelId)
{
    public static readonly string[] Slots=["M1","M2","M3","M4"];
    public static readonly string[] Inputs=["A","B","X","Y","LB","RB","LT","RT","L3","R3","DPAD_UP","DPAD_DOWN","DPAD_LEFT","DPAD_RIGHT","SELECT","START"];
    private readonly Dictionary<string,List<MacroEvent>> sequences=Slots.ToDictionary(s=>s,_=>new List<MacroEvent>());
    public bool CanEdit=>modelId=="x20";
    public bool CanApplyHardware=>false;
    public int ModifiedSlots=>sequences.Values.Count(s=>s.Count>0);
    public IReadOnlyList<MacroEvent> this[string slot]=>sequences[slot].AsReadOnly();
    public int EntryCount(string slot)=>sequences[slot].Sum(e=>1+(e.PauseMs>0?1:0));
    public int Duration(string slot)=>sequences[slot].Sum(e=>e.HoldMs+e.PauseMs);
    public void Add(string slot,int pauseMs=20)=>Commit(slot,[..sequences[slot],new(Guid.NewGuid(),Array.AsReadOnly(new[]{"A"}),StickHeading.Neutral,StickHeading.Neutral,40,pauseMs)]);
    public void Update(string slot,int index,MacroEvent value) {var next=sequences[slot].ToList();next[index]=value;Commit(slot,next);}
    public void Remove(string slot,int index) {var next=sequences[slot].ToList();next.RemoveAt(index);Commit(slot,next);}
    public void Move(string slot,int from,int to)
    {
        var next=sequences[slot].ToList();var item=next[from];next.RemoveAt(from);next.Insert(Math.Clamp(to,0,next.Count),item);Commit(slot,next);
    }
    public void Clear(string slot)=>Commit(slot,[]);
    public void Duplicate(string slot,int index){var next=sequences[slot].ToList();next.Insert(index+1,next[index] with {Id=Guid.NewGuid()});Commit(slot,next);}
    private void Commit(string slot,List<MacroEvent> next)
    {
        if(!CanEdit)throw new InvalidOperationException("Macro configuration is unverified for this model.");
        if(next.Sum(e=>1+(e.PauseMs>0?1:0))>47)throw new InvalidOperationException("This local sequence reaches the reference's 47-entry limit. Reduce a pause or remove an event.");
        if(next.Select(e=>e.Id).Distinct().Count()!=next.Count || next.Any(e=>e.Buttons.Distinct().Count()!=e.Buttons.Count || e.Buttons.Any(b=>!Inputs.Contains(b)) || !Enum.IsDefined(e.Left) || !Enum.IsDefined(e.Right) || new[]{e.HoldMs,e.PauseMs}.Any(ms=>ms<0||ms>327675||ms%5!=0)))
            throw new ArgumentException("Use known macro inputs, valid directions and 5 ms timing increments.");
        sequences[slot]=next.Select(e=>e with {Buttons=Array.AsReadOnly(e.Buttons.ToArray())}).ToList();
    }
}
