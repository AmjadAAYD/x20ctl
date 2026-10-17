namespace X20Ctl.Zone.Diagnostics;
/// <summary>A bounded set of near-centre samples. No quality rating or hardware inference.</summary>
public sealed class DriftObservation
{
 public int Count {get;private set;}
 public double X {get;private set;}public double Y {get;private set;}
 public double Peak {get;private set;}
 public double Distance=>Math.Sqrt(X*X+Y*Y);
 public bool Stable=>Count>=20 && Peak<=25;
 public double RecommendedInner=>Math.Min(40,Math.Ceiling((Peak+.5)*10)/10);
 public void Add(short x,short y){double nx=x/32768d*100,ny=y/32768d*100;Count++;X+=(nx-X)/Count;Y+=(ny-Y)/Count;Peak=Math.Max(Peak,Math.Sqrt(nx*nx+ny*ny));}
}
