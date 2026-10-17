namespace X20Ctl.Zone.Diagnostics;

/// <summary>
/// What the Studio's Diagnostics tab measures from a stream of stick readings: how far it reaches in each direction
/// (the range trace and its circularity), where it sits when left alone (drift), and the smallest step it moves in
/// (an estimate of the stick's real resolution, since XInput always reports 16 bits). Pure logic, tested directly.
/// </summary>
public sealed class StickStats
{
    public const int Sectors = 72; // 5° each
    readonly double[] reach = new double[Sectors];
    short lastX, lastY; bool any;
    int stepX = int.MaxValue, stepY = int.MaxValue;
    double restX, restY; int restSamples;

    public bool HasSamples => any;
    public short MinX { get; private set; } = short.MaxValue;
    public short MaxX { get; private set; } = short.MinValue;
    public short MinY { get; private set; } = short.MaxValue;
    public short MaxY { get; private set; } = short.MinValue;
    public double? SmallestXDelta => stepX==int.MaxValue ? null : stepX/32767d;
    public double? SmallestYDelta => stepY==int.MaxValue ? null : stepY/32767d;
    public double? MaximumRadialError => Covered < Sectors*.8 ? null : reach.Where(r=>r>=.5).Max(r=>Math.Abs(1-r))*100;
    public short X { get; private set; }
    public short Y { get; private set; }
    public IReadOnlyList<double> Reach => reach;

    public void Add(short x, short y)
    {
        if (any)
        {
            int dx = Math.Abs(x - lastX), dy = Math.Abs(y - lastY);
            if (dx > 0) stepX = Math.Min(stepX, dx);
            if (dy > 0) stepY = Math.Min(stepY, dy);
        }
        MinX=Math.Min(MinX,x);MaxX=Math.Max(MaxX,x);MinY=Math.Min(MinY,y);MaxY=Math.Max(MaxY,y);
        lastX = x; lastY = y; any = true; X = x; Y = y;
        double nx = x / 32767.0, ny = y / 32767.0, m = Math.Sqrt(nx * nx + ny * ny);
        if (m > .05)
        {
            int sector = (int)((Math.Atan2(ny, nx) + Math.PI) / (2 * Math.PI) * Sectors) % Sectors;
            reach[sector] = Math.Max(reach[sector], m);
        }
        // the resting point: a slow average of readings near the centre, so a hand on the stick barely moves it
        if (m < .15) { double k = restSamples < 200 ? 1.0 / ++restSamples : .005; restX += (nx - restX) * k; restY += (ny - restY) * k; }
    }

    /// <summary>How many of the 72 directions have been pushed to at least half way.</summary>
    public int Covered => reach.Count(r => r >= .5);
    /// <summary>Average distance from a perfect circle, in percent of full travel, once most directions are covered.
    /// A round-gated stick that reaches its edge everywhere scores near 0; corners reaching past 100% raise it.</summary>
    public double? CircularityError => Covered < Sectors * 0.8 ? null : reach.Where(r => r >= .5).Average(r => Math.Abs(1 - Math.Min(r, 1.5))) * 100;
    /// <summary>Where the stick rests, in percent of full travel per axis (null until it has been seen near the centre).</summary>
    public (double X, double Y)? Rest => restSamples < 20 ? null : (restX * 100, restY * 100);
    /// <summary>The smallest step seen on either axis, as bits of resolution (16 for a true 16-bit stick).</summary>
    public int? ResolutionBits => Bits(Math.Min(stepX, stepY), 16);

    internal static int? Bits(int step, int total) => step is <= 0 or int.MaxValue ? null : Math.Clamp(total - (int)Math.Round(Math.Log2(step)), 1, total);

    public void Reset()
    {
        Array.Clear(reach); any = false; X=Y=0;MinX=MinY=short.MaxValue;MaxX=MaxY=short.MinValue; stepX = stepY = int.MaxValue; restX = restY = 0; restSamples = 0;
    }
}

/// <summary>A trigger's travel: lowest and highest seen and its smallest step (XInput triggers are 8 bits).</summary>
public sealed class TriggerStats
{
    byte last; bool any; int step = int.MaxValue;
    public byte Value { get; private set; }
    public byte Min { get; private set; } = 255;
    public byte Max { get; private set; }
    public void Add(byte v)
    {
        if (any && v != last) step = Math.Min(step, Math.Abs(v - last));
        last = v; any = true; Value = v; Min = Math.Min(Min, v); Max = Math.Max(Max, v);
    }
    public bool FullTravel => Max >= 250 && Min <= 5;
    public int? ResolutionBits => StickStats.Bits(step, 8);
    public void Reset() { Value=0;any = false; step = int.MaxValue; Min = 255; Max = 0; }
}

/// <summary>
/// Each button's presses and how long they last. Repeated transitions within a short window are flagged for review. They do not prove hardware damage.
/// </summary>
public sealed class ButtonStats
{
    public const double BounceMs = 15;
    public sealed class Button { public int Presses, Bounces; public double LastMs, LongestMs; internal Queue<double> Transitions=new(); public double ShortestMs = double.MaxValue; internal double DownAt = -1; public bool Down => DownAt >= 0; }
    readonly Dictionary<ushort, Button> buttons = new();
    ushort previous;
    public Button this[ushort mask] => buttons.TryGetValue(mask, out var b) ? b : buttons[mask] = new();

    public void Add(ushort state, double nowMs, IEnumerable<ushort> masks)
    {
        foreach (var mask in masks)
        {
            bool down = (state & mask) != 0, was = (previous & mask) != 0;
            var b = this[mask];
            if(down!=was) {
                b.Transitions.Enqueue(nowMs);while(b.Transitions.Count>0 && nowMs-b.Transitions.Peek()>BounceMs)b.Transitions.Dequeue();
                if(b.Transitions.Count==3){b.Bounces++;b.Transitions.Clear();}
            }
            if (down && !was) { b.Presses++; b.DownAt = nowMs; }
            else if (!down && was && b.DownAt >= 0)
            {
                double held = nowMs - b.DownAt; b.DownAt = -1;
                b.ShortestMs = Math.Min(b.ShortestMs, held);
                b.LastMs=held;b.LongestMs=Math.Max(b.LongestMs,held);
            }
        }
        previous = state;
    }
    public void Reset() { buttons.Clear(); previous = 0; }
}

/// <summary>Reports per second over a sliding one-second window, and the highest one-second count seen.</summary>
public sealed class RateMeter
{
    readonly Queue<double> times = new();
    public int Peak { get; private set; }
    public void Add(double nowMs, int count = 1)
    {
        for (int i = 0; i < count; i++) times.Enqueue(nowMs);
        Trim(nowMs);
        if (times.Count > Peak) Peak = times.Count;
    }
    public int Current(double nowMs) { Trim(nowMs); return times.Count; }
    void Trim(double nowMs) { while (times.Count > 0 && nowMs - times.Peek() > 1000) times.Dequeue(); }
    public void Reset() { times.Clear(); Peak = 0; }
}
