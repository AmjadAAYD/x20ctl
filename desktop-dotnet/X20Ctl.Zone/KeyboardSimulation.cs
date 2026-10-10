namespace X20Ctl.Zone;
public enum SimulationKey {W,A,S,D,Up,Down,Left,Right,Q,E}
public sealed class KeyboardSimulation(string model)
{
    private readonly HashSet<SimulationKey> held=new();private long sequence;
    public bool Enabled {get;private set;}
    public IReadOnlyCollection<SimulationKey> Held=>held.ToArray();
    public void Enable(){Enabled=true;held.Clear();sequence=0;}
    public void Disable(){Enabled=false;held.Clear();}
    public void Clear()=>held.Clear();
    public bool Press(SimulationKey key)=>Enabled&&held.Add(key);
    public bool Release(SimulationKey key)=>held.Remove(key);
    public TesterFrame Frame()
    {
        double Axis(SimulationKey positive,SimulationKey negative)=>(held.Contains(positive)?1:0)-(held.Contains(negative)?1:0);
        var left=(X:Axis(SimulationKey.D,SimulationKey.A),Y:Axis(SimulationKey.W,SimulationKey.S));var right=(X:Axis(SimulationKey.Right,SimulationKey.Left),Y:Axis(SimulationKey.Up,SimulationKey.Down));
        static (double X,double Y) Normalize((double X,double Y) value){double magnitude=Math.Sqrt(value.X*value.X+value.Y*value.Y);return magnitude>1?(value.X/magnitude,value.Y/magnitude):value;}
        left=Normalize(left);right=Normalize(right);
        return new(model,"Keyboard simulation",true,++sequence,[],["LSTICK_ANALOG","RSTICK_ANALOG","LT","RT"],left.X,left.Y,right.X,right.Y,held.Contains(SimulationKey.Q)?1:0,held.Contains(SimulationKey.E)?1:0);
    }
}
