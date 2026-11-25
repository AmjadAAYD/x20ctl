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

    /// <summary>8-point compass symmetry reach (E, NE, N, NW, W, SW, S, SE) in percent of full travel.</summary>
    public double[] CompassReach()
    {
        // StickStats sectors map atan2(ny, nx) + PI into [0, 72): index 0 is West (-PI), 18 is South (-PI/2),
        // 36 is East (0), 54 is North (+PI/2). Sample the max in +/- 1 sector around each 45-deg heading.
        static int BucketForAngle(double rad) => ((int)Math.Round(((rad + Math.PI) / (2 * Math.PI)) * Sectors) % Sectors + Sectors) % Sectors;
        double[] headings = [0, Math.PI / 4, Math.PI / 2, 3 * Math.PI / 4, Math.PI, -3 * Math.PI / 4, -Math.PI / 2, -Math.PI / 4];
        var result = new double[8];
        for (int i = 0; i < 8; i++)
        {
            int center = BucketForAngle(headings[i]);
            double peak = 0;
            for (int d = -2; d <= 2; d++)
            {
                int idx = (center + d + Sectors) % Sectors;
                if (reach[idx] > peak) peak = reach[idx];
            }
            result[i] = Math.Round(peak * 100.0, 1);
        }
        return result;
    }

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

/// <summary>Packet arrival interval jitter analyzer (12-bucket histogram + min/avg/p99/max/stddev ms + spike count).</summary>
public sealed class JitterStats
{
    public const int WindowSize = 512;
    public const int BucketCount = 12;
    static readonly double[] BucketEdgesMs = [0.5, 1.0, 1.5, 2.0, 3.0, 4.0, 6.0, 8.0, 12.0, 16.0, 25.0];
    public static readonly string[] BucketLabels = ["<0.5", "0.5-1", "1-1.5", "1.5-2", "2-3", "3-4", "4-6", "6-8", "8-12", "12-16", "16-25", ">=25"];

    readonly Queue<double> window = new(WindowSize);
    readonly int[] histogram = new int[BucketCount];

    public long SampleCount { get; private set; }
    public double LastMs { get; private set; }
    public double MinMs { get; private set; } = double.MaxValue;
    public double MaxMs { get; private set; }
    public int SpikeCount { get; private set; }
    public int[] Histogram => [.. histogram];

    public void AddInterval(double ms)
    {
        if (!double.IsFinite(ms) || ms <= 0.01 || ms > 500.0) return;
        double priorAvg = window.Count >= 20 ? window.Average() : 0;
        LastMs = ms;
        if (ms < MinMs) MinMs = ms;
        if (ms > MaxMs) MaxMs = ms;
        if (window.Count >= WindowSize) window.Dequeue();
        window.Enqueue(ms);
        SampleCount++;

        int bucket = 0;
        while (bucket < BucketEdgesMs.Length && ms >= BucketEdgesMs[bucket]) bucket++;
        histogram[bucket]++;

        if (priorAvg > 0 && ms > Math.Max(priorAvg * 2.0, priorAvg + 4.0))
            SpikeCount++;
    }

    public double AvgMs => window.Count == 0 ? 0 : window.Average();

    public double P99Ms
    {
        get
        {
            if (window.Count == 0) return 0;
            var sorted = window.OrderBy(x => x).ToArray();
            int idx = Math.Clamp((int)Math.Ceiling(sorted.Length * 0.99) - 1, 0, sorted.Length - 1);
            return sorted[idx];
        }
    }

    public double StdDevMs
    {
        get
        {
            if (window.Count < 2) return 0;
            double avg = AvgMs;
            double sum = 0;
            foreach (double v in window) sum += (v - avg) * (v - avg);
            return Math.Sqrt(sum / window.Count);
        }
    }

    public void Reset()
    {
        window.Clear();
        Array.Clear(histogram);
        SampleCount = 0;
        LastMs = 0;
        MinMs = double.MaxValue;
        MaxMs = 0;
        SpikeCount = 0;
    }
}

/// <summary>Measures resting stick micro-jitter near center to recommend the minimum safe inner dead zone.</summary>
public sealed class NoiseFloorStats
{
    public const int MaxPoints = 250;
    readonly Queue<(double X, double Y)> points = new(MaxPoints);
    double sumX, sumY, sumR2;

    public int SampleCount { get; private set; }
    public double PeakRadiusPct { get; private set; }
    public double BiasX => SampleCount == 0 ? 0 : sumX / SampleCount * 100.0;
    public double BiasY => SampleCount == 0 ? 0 : sumY / SampleCount * 100.0;
    public double RmsPct => SampleCount == 0 ? 0 : Math.Sqrt(sumR2 / SampleCount) * 100.0;
    public double RecommendedDeadZonePct => SampleCount == 0 ? 0 : Math.Clamp(Math.Ceiling((PeakRadiusPct + 0.5) * 10.0) / 10.0, 0.5, 40.0);
    public IReadOnlyList<(double X, double Y)> Points { get { return points.ToArray(); } }

    public void Add(short x, short y)
    {
        double nx = x / 32767.0, ny = y / 32767.0;
        double r = Math.Sqrt(nx * nx + ny * ny);
        if (r >= 0.25) return; // Ignore intentional stick sweeps so resting jitter stays pure.
        SampleCount++;
        sumX += nx;
        sumY += ny;
        sumR2 += r * r;
        double pct = r * 100.0;
        if (pct > PeakRadiusPct) PeakRadiusPct = pct;
        if (points.Count >= MaxPoints) points.Dequeue();
        points.Enqueue((nx * 100.0, ny * 100.0));
    }

    public void Reset()
    {
        points.Clear();
        SampleCount = 0;
        PeakRadiusPct = 0;
        sumX = sumY = sumR2 = 0;
    }
}

/// <summary>10-band trigger travel histogram + start/end dead-band estimator.</summary>
public sealed class TriggerLinearityStats
{
    readonly int[] buckets = new int[10];
    byte lowestNonZero = 255;
    byte highestSeen;
    bool any;

    public int[] Buckets => [.. buckets];
    public double StartDeadBandPct => !any || lowestNonZero == 255 ? 0 : lowestNonZero / 2.55;
    public double EndDeadBandPct => !any ? 0 : (255 - highestSeen) / 2.55;
    public double CoveragePct => buckets.Count(c => c > 0) * 10.0;

    public void Add(byte v)
    {
        any = true;
        if (v > 0 && v < lowestNonZero) lowestNonZero = v;
        if (v > highestSeen) highestSeen = v;
        int idx = Math.Clamp(v * 10 / 256, 0, 9);
        buckets[idx]++;
    }

    public void Reset()
    {
        Array.Clear(buckets);
        lowestNonZero = 255;
        highestSeen = 0;
        any = false;
    }
}

public sealed record TransportBenchmarkSnapshot(
    string Id,
    DateTimeOffset CapturedUtc,
    string TransportLabel,
    string DeviceName,
    double AvgHz,
    int PeakHz,
    double AvgIntervalMs,
    double P99Ms,
    double JitterStdDevMs,
    double? LeftCircularityPct,
    double? RightCircularityPct,
    double? LeftNoisePct,
    double? RightNoisePct,
    int HealthScore);

public sealed class TransportBenchmarkStore(string? path = null)
{
    readonly string filePath = string.IsNullOrWhiteSpace(path) ? DefaultPath : Path.GetFullPath(path);
    readonly List<TransportBenchmarkSnapshot> items = new();
    bool loaded;

    public static string DefaultPath => AppDataPaths.Local("telemetry", "transport-benchmarks.json");
    public IReadOnlyList<TransportBenchmarkSnapshot> Items { get { EnsureLoaded(); return items.AsReadOnly(); } }

    public void Load()
    {
        loaded = true;
        items.Clear();
        if (!File.Exists(filePath)) return;
        try
        {
            var parsed = System.Text.Json.JsonSerializer.Deserialize<List<TransportBenchmarkSnapshot>>(File.ReadAllBytes(filePath));
            if (parsed != null) items.AddRange(parsed.Where(x => x != null && !string.IsNullOrWhiteSpace(x.Id)).Take(12));
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or IOException) { }
    }

    public void Add(TransportBenchmarkSnapshot snapshot)
    {
        EnsureLoaded();
        items.Insert(0, snapshot);
        if (items.Count > 12) items.RemoveRange(12, items.Count - 12);
        Save();
    }

    public void Clear()
    {
        EnsureLoaded();
        items.Clear();
        Save();
    }

    void EnsureLoaded() { if (!loaded) Load(); }

    void Save()
    {
        string dir = Path.GetDirectoryName(filePath)!;
        Directory.CreateDirectory(dir);
        string tmp = filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllBytes(tmp, System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(items, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            File.Move(tmp, filePath, true);
        }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }
}

/// <summary>Deterministic 0-100 health summary derived strictly from measured diagnostics.</summary>
public static class ControllerHealthScore
{
    public sealed record HealthResult(int Score, string Grade, IReadOnlyList<string> Factors);

    public static HealthResult Evaluate(
        double? leftCircPct,
        double? rightCircPct,
        double? leftNoisePct,
        double? rightNoisePct,
        int totalBounces,
        double? jitterStdDevMs,
        int? batteryLevelOf4)
    {
        double score = 100.0;
        var factors = new List<string>();

        if (leftCircPct is { } lc && lc > 8.0)
        {
            double pen = Math.Min(18.0, (lc - 8.0) * 1.4);
            score -= pen;
            factors.Add($"Left stick circularity error {lc:0.1}% (-{pen:0})");
        }
        if (rightCircPct is { } rc && rc > 8.0)
        {
            double pen = Math.Min(18.0, (rc - 8.0) * 1.4);
            score -= pen;
            factors.Add($"Right stick circularity error {rc:0.1}% (-{pen:0})");
        }
        if (leftNoisePct is { } ln && ln > 2.5)
        {
            double pen = Math.Min(18.0, (ln - 2.5) * 2.5);
            score -= pen;
            factors.Add($"Left stick resting noise {ln:0.1}% (-{pen:0})");
        }
        if (rightNoisePct is { } rn && rn > 2.5)
        {
            double pen = Math.Min(18.0, (rn - 2.5) * 2.5);
            score -= pen;
            factors.Add($"Right stick resting noise {rn:0.1}% (-{pen:0})");
        }
        if (totalBounces > 0)
        {
            double pen = Math.Min(30.0, totalBounces * 8.0);
            score -= pen;
            factors.Add($"{totalBounces} button bounce sequence(s) flagged (-{pen:0})");
        }
        if (jitterStdDevMs is { } jit && jit > 2.5)
        {
            double pen = Math.Min(14.0, (jit - 2.5) * 3.0);
            score -= pen;
            factors.Add($"Packet timing jitter stddev {jit:0.2} ms (-{pen:0})");
        }
        if (batteryLevelOf4 is <= 1)
            factors.Add("Battery low (1/4): charge soon");

        if (factors.Count == 0)
            factors.Add("All measured sticks, switches, and report intervals look healthy");

        int finalScore = Math.Clamp((int)Math.Round(score), 0, 100);
        string grade = finalScore >= 90 ? "Excellent"
            : finalScore >= 75 ? "Good"
            : finalScore >= 55 ? "Fair"
            : "Needs attention";
        return new HealthResult(finalScore, grade, factors);
    }
}
