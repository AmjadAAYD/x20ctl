namespace X20Ctl.Zone.Telemetry;

/// <summary>Session opening and connection edges trigger reads; repeated input frames do not.</summary>
public sealed class TelemetryRefreshPolicy
{
    readonly HashSet<string> opened=[];
    readonly Dictionary<string,bool> connected=[];
    static bool Supported(string target)=>target.EndsWith(":x20_pro",StringComparison.Ordinal)||target.EndsWith(":x20",StringComparison.Ordinal);
    public bool OnOpen(string target)=>Supported(target)&&opened.Add(target);
    public bool OnConnection(string target,bool value)
    {
        if(!Supported(target))return false;
        bool known=connected.TryGetValue(target,out bool previous);connected[target]=value;
        if(!known)return value&&OnOpen(target);
        return value&&!previous;
    }
}
