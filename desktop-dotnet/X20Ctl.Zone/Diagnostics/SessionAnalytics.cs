using X20Ctl.Zone.PcMode;

namespace X20Ctl.Zone.Diagnostics;

/// <summary>
/// Tracks live session controller usage: total button presses, rolling 60s Actions Per Minute (APM),
/// cumulative stick travel distance, and 16x16 stick dwell heatmaps for Left & Right sticks.
/// </summary>
public sealed class SessionInputTracker
{
    public const int HeatmapBins = 16;

    readonly object gate = new();
    readonly Dictionary<string, int> buttonCounts = new(StringComparer.Ordinal);
    readonly Queue<long> recentActionTimesMs = new();
    readonly int[,] leftHeatmap = new int[HeatmapBins, HeatmapBins];
    readonly int[,] rightHeatmap = new int[HeatmapBins, HeatmapBins];

    bool initialized;
    ushort lastButtons;
    double lastLx, lastLy, lastRx, lastRy;
    double leftDistance, rightDistance;
    int totalPresses;

    public int TotalButtonPresses { get { lock (gate) return totalPresses; } }
    public double LeftStickDistance { get { lock (gate) return leftDistance; } }
    public double RightStickDistance { get { lock (gate) return rightDistance; } }

    public IReadOnlyDictionary<string, int> ButtonCounts
    {
        get { lock (gate) return new Dictionary<string, int>(buttonCounts, StringComparer.Ordinal); }
    }

    public void Sample(PadState pad, long nowMs)
    {
        double lx = Math.Clamp(pad.LeftX / 32767.0, -1.0, 1.0);
        double ly = Math.Clamp(pad.LeftY / 32767.0, -1.0, 1.0);
        double rx = Math.Clamp(pad.RightX / 32767.0, -1.0, 1.0);
        double ry = Math.Clamp(pad.RightY / 32767.0, -1.0, 1.0);

        lock (gate)
        {
            if (initialized)
            {
                double dl = Math.Sqrt((lx - lastLx) * (lx - lastLx) + (ly - lastLy) * (ly - lastLy));
                double dr = Math.Sqrt((rx - lastRx) * (rx - lastRx) + (ry - lastRy) * (ry - lastRy));
                if (dl > 0.008) leftDistance += dl;
                if (dr > 0.008) rightDistance += dr;

                ushort rising = (ushort)(pad.Buttons & ~lastButtons);
                if (rising != 0)
                {
                    foreach (var (name, mask) in PadButtons.Mask)
                    {
                        if ((rising & mask) != 0)
                        {
                            buttonCounts[name] = buttonCounts.GetValueOrDefault(name) + 1;
                            totalPresses++;
                            recentActionTimesMs.Enqueue(nowMs);
                        }
                    }
                }
            }
            else
            {
                initialized = true;
                if (pad.Buttons != 0)
                {
                    foreach (var (name, mask) in PadButtons.Mask)
                    {
                        if ((pad.Buttons & mask) != 0)
                        {
                            buttonCounts[name] = buttonCounts.GetValueOrDefault(name) + 1;
                            totalPresses++;
                            recentActionTimesMs.Enqueue(nowMs);
                        }
                    }
                }
            }

            IncrementBin(leftHeatmap, lx, ly);
            IncrementBin(rightHeatmap, rx, ry);

            while (recentActionTimesMs.Count > 0 && nowMs - recentActionTimesMs.Peek() > 60_000)
                recentActionTimesMs.Dequeue();

            lastButtons = pad.Buttons;
            lastLx = lx; lastLy = ly; lastRx = rx; lastRy = ry;
        }
    }

    public int ActionsPerMinute(long nowMs)
    {
        lock (gate)
        {
            while (recentActionTimesMs.Count > 0 && nowMs - recentActionTimesMs.Peek() > 60_000)
                recentActionTimesMs.Dequeue();
            return recentActionTimesMs.Count;
        }
    }

    public int[,] CopyLeftHeatmap() { lock (gate) return (int[,])leftHeatmap.Clone(); }
    public int[,] CopyRightHeatmap() { lock (gate) return (int[,])rightHeatmap.Clone(); }

    public IReadOnlyList<(string Button, int Count)> TopButtons(int take = 5)
    {
        lock (gate)
            return buttonCounts.OrderByDescending(kv => kv.Value).Take(Math.Max(1, take)).Select(kv => (kv.Key, kv.Value)).ToList();
    }

    public void Reset()
    {
        lock (gate)
        {
            initialized = false;
            lastButtons = 0;
            lastLx = lastLy = lastRx = lastRy = 0;
            leftDistance = rightDistance = 0;
            totalPresses = 0;
            buttonCounts.Clear();
            recentActionTimesMs.Clear();
            Array.Clear(leftHeatmap);
            Array.Clear(rightHeatmap);
        }
    }

    static void IncrementBin(int[,] grid, double nx, double ny)
    {
        int col = Math.Clamp((int)Math.Floor((nx + 1.0) * 0.5 * HeatmapBins), 0, HeatmapBins - 1);
        // Invert Y so upward stick tilt lands in top rows of heatmap
        int row = Math.Clamp((int)Math.Floor((1.0 - ny) * 0.5 * HeatmapBins), 0, HeatmapBins - 1);
        grid[row, col]++;
    }
}

/// <summary>
/// Pure cross-platform 60 Hz Stick Aim Trainer state machine for testing right-stick curve, sensitivity, and deadzone feel.
/// Coordinates span [-1..1] horizontally and vertically.
/// </summary>
public sealed class AimTrainerSession
{
    readonly Random rng;
    bool prevFire;
    long targetSpawnedMs;
    readonly List<double> reactionMs = [];

    public double CursorX { get; private set; }
    public double CursorY { get; private set; }
    public double TargetX { get; private set; }
    public double TargetY { get; private set; }
    public double TargetRadius { get; set; } = 0.14;
    public double CursorSpeedPerSec { get; set; } = 1.65;
    public int Hits { get; private set; }
    public int Misses { get; private set; }
    public double TimeRemainingSec { get; private set; }
    public bool Running { get; private set; }

    public double AvgReactionMs => reactionMs.Count == 0 ? 0.0 : reactionMs.Average();
    public double HitRatePct => Hits + Misses == 0 ? 0.0 : 100.0 * Hits / (Hits + Misses);

    public AimTrainerSession(int? seed = null)
    {
        rng = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    public void Start(int durationSec = 30, long nowMs = 0)
    {
        Hits = 0;
        Misses = 0;
        CursorX = 0;
        CursorY = 0;
        prevFire = false;
        reactionMs.Clear();
        TimeRemainingSec = Math.Clamp(durationSec, 5, 180);
        Running = true;
        SpawnTarget(nowMs);
    }

    public bool Step(double stickX, double stickY, bool firePressed, double dtSec, long nowMs)
    {
        if (!Running) return false;
        double dt = Math.Clamp(dtSec, 0.001, 0.25);
        TimeRemainingSec = Math.Max(0.0, TimeRemainingSec - dt);
        if (TimeRemainingSec <= 0.0)
        {
            Running = false;
            return false;
        }

        double sx = Math.Clamp(stickX, -1.0, 1.0);
        double sy = Math.Clamp(stickY, -1.0, 1.0);
        CursorX = Math.Clamp(CursorX + sx * CursorSpeedPerSec * dt, -0.96, 0.96);
        CursorY = Math.Clamp(CursorY + sy * CursorSpeedPerSec * dt, -0.96, 0.96);

        bool risingFire = firePressed && !prevFire;
        prevFire = firePressed;
        if (!risingFire) return false;

        double dx = CursorX - TargetX;
        double dy = CursorY - TargetY;
        if (dx * dx + dy * dy <= TargetRadius * TargetRadius)
        {
            Hits++;
            if (nowMs >= targetSpawnedMs)
                reactionMs.Add(Math.Clamp(nowMs - targetSpawnedMs, 50, 5000));
            SpawnTarget(nowMs);
            return true;
        }

        Misses++;
        return false;
    }

    void SpawnTarget(long nowMs)
    {
        targetSpawnedMs = nowMs;
        double nx = (rng.NextDouble() * 1.44) - 0.72;
        double ny = (rng.NextDouble() * 1.32) - 0.66;
        // Avoid spawning directly on top of the current crosshair when possible
        if (Math.Abs(nx - CursorX) < 0.15 && Math.Abs(ny - CursorY) < 0.15)
            nx = CursorX > 0 ? -0.45 : 0.45;
        TargetX = nx;
        TargetY = ny;
    }
}
