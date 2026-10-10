namespace X20Ctl.Zone;
public sealed record DeviceOverview(string ModelId,int Player)
{
    public string GameplayStatus=>"Offline";
    public string ConfigurationStatus=>"Offline";
    public int? BatteryPercent=>null;
    public string? Firmware=>null;
    public string? HardwareRevision=>null;
    public string? Transport=>null;
    public bool CanWriteHardware=>false;
}
public sealed record TesterFrame(string ModelId,string Source,bool IsSimulation,long Sequence,IReadOnlyList<string> ButtonsDown,IReadOnlyList<string> ExposedControls,double LeftX,double LeftY,double RightX,double RightY,double LT,double RT);
public sealed class TesterState(string modelId)
{
    public bool OwnsInput {get;private set;}
    public TesterFrame? Frame {get;private set;}
    public bool CanNavigateGameplay=>!OwnsInput;
    public void Enter(){OwnsInput=true;Frame=null;}
    public void Exit(){OwnsInput=false;Frame=null;}
    public bool Accept(TesterFrame frame)
    {
        if(new[]{frame.LeftX,frame.LeftY,frame.RightX,frame.RightY}.Any(v=>!double.IsFinite(v)||Math.Abs(v)>1) || new[]{frame.LT,frame.RT}.Any(v=>!double.IsFinite(v)||v<0||v>1))throw new ArgumentException("Invalid input frame ranges.");
        // Engine bridge is not connected. Only labelled review fixtures have an ingestion path.
        if(!OwnsInput || frame.ModelId!=modelId || !frame.IsSimulation || frame.Sequence<0 || frame.Sequence<=(Frame?.Sequence??-1))return false;
        Frame=frame with {ButtonsDown=Array.AsReadOnly(frame.ButtonsDown.ToArray()),ExposedControls=Array.AsReadOnly(frame.ExposedControls.ToArray())};return true;
    }
    public bool Pressed(string control)=>Frame!=null && Frame.ExposedControls.Contains(control) && Frame.ButtonsDown.Contains(control);
    public void ObserveNativeGameplay(string source,long sequence,IReadOnlyList<string> buttons,double lx,double ly,double rx,double ry,double lt,double rt)
    {
        if(!OwnsInput)return;if(new[]{lx,ly,rx,ry}.Any(v=>!double.IsFinite(v)||Math.Abs(v)>1)||new[]{lt,rt}.Any(v=>!double.IsFinite(v)||v<0||v>1))throw new ArgumentException("Invalid native gameplay frame.");
        // This is an input-source observation, not evidence of model/configuration identity.
        Frame=new(modelId,source,false,sequence,Array.AsReadOnly(buttons.ToArray()),Array.AsReadOnly(new[]{"A","B","X","Y","LB","RB","L3","R3","START","SELECT","DPAD_UP","DPAD_DOWN","DPAD_LEFT","DPAD_RIGHT","LT","RT","LSTICK_ANALOG","RSTICK_ANALOG"}),lx,ly,rx,ry,lt,rt);
    }
}
