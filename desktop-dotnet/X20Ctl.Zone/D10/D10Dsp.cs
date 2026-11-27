namespace X20Ctl.Zone.D10;

public enum D10ShortcutKind
{
    Short,
    Long
}

public enum D10ShortcutDir
{
    Up,
    Right,
    Down,
    Left
}

public readonly record struct D10ShortcutGesture(D10ShortcutKind Mode, D10ShortcutDir Direction);

public readonly record struct D10HarmonicFilterOutput(double CleanRateDps, double RemovedDps, double Confidence);

public readonly record struct D10TrajectoryGateOutput(double OutputRateDps, bool Rejected, bool Held);

public sealed record D10PoseMean(int PoseIndex, double MeanAx, double MeanAy, double MeanAz, int SampleCount);

public sealed record D10MultiPoseStatus(
    int CompletedPoses,
    int TargetPoses,
    bool WaitingForMove,
    double PoseProgress,
    string Message,
    bool IsComplete);

/// <summary>
/// Causal two-tone oscillator bank over 18-45 Hz (0.5 Hz spacing) that learns and subtracts
/// periodic motor vibration from gyro rate while pausing adaptation during active hand turns.
/// </summary>
public sealed class D10MotorHarmonicCanceller
{
    private const double MinFreqHz = 18.0;
    private const double MaxFreqHz = 45.0;
    private const double FreqStepHz = 0.5;
    private const double HighPassTauSec = 0.020;
    private const double MotionRefTauSec = 0.120;
    private const double CorrTauSec = 0.250;
    private const double MotionPauseDps = 5.0;
    private const double StartConfDps = 2.5;
    private const double FullConfDps = 8.0;
    private const double MaxRemovalDps = 120.0;
    private const double MinToneSpacingHz = 2.5;

    private readonly double[] freqs;
    private readonly double[] phases;
    private readonly double[] cosCorr;
    private readonly double[] sinCorr;
    private readonly double[] energy;
    private double highPassRef;
    private double motionRef;

    public D10MotorHarmonicCanceller()
    {
        int count = (int)Math.Round((MaxFreqHz - MinFreqHz) / FreqStepHz) + 1;
        freqs = new double[count];
        phases = new double[count];
        cosCorr = new double[count];
        sinCorr = new double[count];
        energy = new double[count];
        for (int i = 0; i < count; i++)
            freqs[i] = MinFreqHz + (i * FreqStepHz);
    }

    public void Reset()
    {
        Array.Clear(phases);
        Array.Clear(cosCorr);
        Array.Clear(sinCorr);
        Array.Clear(energy);
        highPassRef = 0.0;
        motionRef = 0.0;
    }

    public D10HarmonicFilterOutput Update(double rateDps, double dtSeconds, bool active)
    {
        double dt = double.IsFinite(dtSeconds) ? Math.Clamp(dtSeconds, 0.0005, 0.030) : 0.005;
        double motionAlpha = dt / (MotionRefTauSec + dt);
        motionRef += motionAlpha * (rateDps - motionRef);

        if (!active)
            return new(rateDps, 0.0, 0.0);

        double hpAlpha = dt / (HighPassTauSec + dt);
        highPassRef += hpAlpha * (rateDps - highPassRef);
        double residual = rateDps - highPassRef;
        bool learning = Math.Abs(motionRef) <= MotionPauseDps;
        double corrAlpha = dt / (CorrTauSec + dt);

        int best1 = -1;
        double e1 = 0.0;
        for (int i = 0; i < freqs.Length; i++)
        {
            phases[i] += 2.0 * Math.PI * freqs[i] * dt;
            if (phases[i] > 2.0 * Math.PI)
                phases[i] %= 2.0 * Math.PI;

            double c = Math.Cos(phases[i]);
            double s = Math.Sin(phases[i]);
            if (learning)
            {
                cosCorr[i] += corrAlpha * ((2.0 * residual * c) - cosCorr[i]);
                sinCorr[i] += corrAlpha * ((2.0 * residual * s) - sinCorr[i]);
                energy[i] = (cosCorr[i] * cosCorr[i]) + (sinCorr[i] * sinCorr[i]);
            }

            if (energy[i] > e1)
            {
                e1 = energy[i];
                best1 = i;
            }
        }

        int best2 = -1;
        double e2 = 0.0;
        if (best1 >= 0)
        {
            for (int i = 0; i < freqs.Length; i++)
            {
                if (Math.Abs(freqs[i] - freqs[best1]) < MinToneSpacingHz)
                    continue;
                if (energy[i] > e2)
                {
                    e2 = energy[i];
                    best2 = i;
                }
            }
        }

        double peakAmp = Math.Sqrt(Math.Max(0.0, e1));
        double conf = Math.Clamp((peakAmp - StartConfDps) / Math.Max(0.1, FullConfDps - StartConfDps), 0.0, 1.0);
        if (conf <= 0.0 || best1 < 0)
            return new(rateDps, 0.0, 0.0);

        double est = (cosCorr[best1] * Math.Cos(phases[best1])) + (sinCorr[best1] * Math.Sin(phases[best1]));
        if (best2 >= 0)
            est += (cosCorr[best2] * Math.Cos(phases[best2])) + (sinCorr[best2] * Math.Sin(phases[best2]));

        double removed = Math.Clamp(est * conf, -MaxRemovalDps, MaxRemovalDps);
        return new(rateDps - removed, removed, conf);
    }
}

/// <summary>
/// Causal 1-frame trajectory confirmation gate rejecting isolated high-rate spikes
/// while passing continuous steering motion immediately on confirmation.
/// </summary>
public sealed class D10TrajectoryGate
{
    private const double HardMaxRateDps = 600.0;
    private const double ResidualThresholdDps = 85.0;
    private const double MinContinuationRatio = 0.30;
    private const double MaxDuplicateDeferSec = 0.012;

    private double prevAccepted;
    private double lastAccepted;
    private int historyCount;
    private bool pending;
    private double pendingRaw;
    private double pendingPrediction;
    private double pendingAgeSec;

    public void Reset()
    {
        prevAccepted = 0.0;
        lastAccepted = 0.0;
        historyCount = 0;
        pending = false;
        pendingRaw = 0.0;
        pendingPrediction = 0.0;
        pendingAgeSec = 0.0;
    }

    public D10TrajectoryGateOutput Update(double rawRateDps, bool freshSample, double dtSeconds, bool enabled)
    {
        if (pending && double.IsFinite(dtSeconds) && dtSeconds > 0.0)
            pendingAgeSec += dtSeconds;

        double prediction = historyCount >= 2
            ? Math.Clamp((2.0 * lastAccepted) - prevAccepted, -HardMaxRateDps, HardMaxRateDps)
            : lastAccepted;

        if (!double.IsFinite(rawRateDps) || Math.Abs(rawRateDps) > HardMaxRateDps)
        {
            pending = false;
            pendingAgeSec = 0.0;
            return new(prediction, Rejected: true, Held: false);
        }

        if (pending)
        {
            if (!freshSample && pendingAgeSec < MaxDuplicateDeferSec)
                return new(pendingPrediction, Rejected: false, Held: true);

            double pendingDelta = pendingRaw - pendingPrediction;
            double currentDelta = rawRateDps - pendingPrediction;
            bool sameDirection = Math.Sign(pendingDelta) == Math.Sign(currentDelta);
            bool continues = sameDirection &&
                             Math.Abs(pendingDelta) > 1e-6 &&
                             (Math.Abs(currentDelta) / Math.Abs(pendingDelta)) >= MinContinuationRatio;

            pending = false;
            pendingAgeSec = 0.0;
            if (continues)
            {
                Accept(rawRateDps);
                return new(rawRateDps, Rejected: false, Held: false);
            }
        }

        if (!enabled || historyCount < 2)
        {
            Accept(rawRateDps);
            return new(rawRateDps, Rejected: false, Held: false);
        }

        double residual = Math.Abs(rawRateDps - prediction);
        if (residual >= ResidualThresholdDps)
        {
            pending = true;
            pendingRaw = rawRateDps;
            pendingPrediction = prediction;
            pendingAgeSec = 0.0;
            return new(prediction, Rejected: false, Held: true);
        }

        Accept(rawRateDps);
        return new(rawRateDps, Rejected: false, Held: false);
    }

    private void Accept(double rate)
    {
        prevAccepted = lastAccepted;
        lastAccepted = rate;
        historyCount++;
    }
}

/// <summary>Smooth monotonic shoulder curve boost across the mid-range steering band.</summary>
public static class D10MidRangeBoost
{
    public static double Apply(double normalizedMag, double startFraction, double strength)
    {
        double m = Math.Clamp(normalizedMag, 0.0, 1.0);
        double start = Math.Clamp(startFraction, 0.05, 0.65);
        double gain = Math.Clamp(strength, 0.0, 0.45);
        if (gain <= 0.0 || m <= start || m >= 1.0)
            return m;

        double t = (m - start) / (1.0 - start);
        double bump = gain * Math.Sin(Math.PI * t) * t;
        return Math.Clamp(m + bump, 0.0, 1.0);
    }
}

/// <summary>
/// Detects held M + single D-pad chords. Releasing before 3 seconds emits Short;
/// holding steady for 3 seconds emits Long once; diagonal or shifting chords require neutral reset.
/// </summary>
public sealed class D10ShortcutDetector
{
    public const long LongPressMs = 3000;

    private bool active;
    private bool blockedUntilNeutral;
    private D10ShortcutDir dir;
    private long startedMs;

    public void Reset()
    {
        active = false;
        blockedUntilNeutral = false;
        startedMs = 0;
    }

    public D10ShortcutGesture? Update(D10ExtraButtons extra, long timestampMs)
    {
        int dirCount = (extra.DpadUp ? 1 : 0) + (extra.DpadRight ? 1 : 0) +
                       (extra.DpadDown ? 1 : 0) + (extra.DpadLeft ? 1 : 0);

        if (blockedUntilNeutral)
        {
            if (!extra.M && dirCount == 0)
            {
                blockedUntilNeutral = false;
                active = false;
            }
            return null;
        }

        if (!extra.M || dirCount == 0)
        {
            if (active)
            {
                var fired = new D10ShortcutGesture(D10ShortcutKind.Short, dir);
                blockedUntilNeutral = extra.M || dirCount > 0;
                active = false;
                return fired;
            }
            return null;
        }

        if (dirCount > 1)
        {
            blockedUntilNeutral = true;
            active = false;
            return null;
        }

        D10ShortcutDir current = extra.DpadUp ? D10ShortcutDir.Up
            : extra.DpadRight ? D10ShortcutDir.Right
            : extra.DpadDown ? D10ShortcutDir.Down
            : D10ShortcutDir.Left;

        if (!active)
        {
            active = true;
            dir = current;
            startedMs = timestampMs;
            return null;
        }

        if (current != dir)
        {
            blockedUntilNeutral = true;
            active = false;
            return null;
        }

        if (timestampMs - startedMs >= LongPressMs)
        {
            blockedUntilNeutral = true;
            active = false;
            return new D10ShortcutGesture(D10ShortcutKind.Long, dir);
        }

        return null;
    }
}

/// <summary>Captures stationary poses across 15 orientations for 3-axis accelerometer fitting.</summary>
public sealed class D10MultiPoseCalibrator
{
    public const int DefaultTargetPoses = 15;
    public const int SamplesPerPose = 150;
    public const double MaxAxisSpreadG = 0.035;
    public const double MaxGyroRateDps = 20.0;
    public const double MinMoveDistanceG = 0.10;

    private readonly D10LinkKind link;
    private readonly D10CalibrationData baseline;
    private readonly int targetPoses;
    private readonly List<D10PoseMean> poses = new();
    private readonly List<(double Ax, double Ay, double Az, double AxG, double AyG, double AzG)> window = new();

    private (double AxG, double AyG, double AzG)? lastAcceptedG;
    private bool waitingForMove;

    public D10MultiPoseCalibrator(D10LinkKind link, D10CalibrationData? baseline = null, int targetPoses = DefaultTargetPoses)
    {
        this.link = link;
        this.baseline = baseline ?? D10CalibrationData.Default;
        this.targetPoses = Math.Clamp(targetPoses, D10EllipsoidFitter.MinPoseCount, 30);
    }

    public IReadOnlyList<D10PoseMean> Poses => poses;
    public bool IsComplete => poses.Count >= targetPoses;

    public D10MultiPoseStatus Feed(D10ImuSample sample)
    {
        if (IsComplete)
        {
            return new(poses.Count, targetPoses, false, 1.0, "All poses captured. Fitting 3-axis ellipsoid...", true);
        }

        double sx = link == D10LinkKind.WirelessReceiver ? baseline.WirelessCountsPerG : baseline.AccelScaleX;
        double sy = link == D10LinkKind.WirelessReceiver ? baseline.WirelessCountsPerG : baseline.AccelScaleY;
        double sz = link == D10LinkKind.WirelessReceiver ? baseline.WirelessCountsPerG : baseline.AccelScaleZ;
        double axG = sample.Ax / (sx > 0 ? sx : 4200.0);
        double ayG = sample.Ay / (sy > 0 ? sy : 4200.0);
        double azG = sample.Az / (sz > 0 ? sz : 4200.0);

        double gyroMag = Math.Sqrt(
            (sample.Gx * sample.Gx) + (sample.Gy * sample.Gy) + (sample.Gz * sample.Gz)) /
            Math.Max(1.0, baseline.GyroCountsPerDps);

        if (lastAcceptedG is { } prior)
        {
            double moveDist = Math.Sqrt(
                Math.Pow(axG - prior.AxG, 2) +
                Math.Pow(ayG - prior.AyG, 2) +
                Math.Pow(azG - prior.AzG, 2));

            bool returningLast = poses.Count == targetPoses - 1 && poses.Count > 1;
            if (waitingForMove)
            {
                if (!returningLast && moveDist < MinMoveDistanceG)
                    return new(poses.Count, targetPoses, true, 0.0, $"Pose {poses.Count}/{targetPoses} saved. Tilt or rotate to next pose...", false);

                if (returningLast)
                {
                    double distToFirst = Math.Sqrt(
                        Math.Pow(axG - (poses[0].MeanAx / sx), 2) +
                        Math.Pow(ayG - (poses[0].MeanAy / sy), 2) +
                        Math.Pow(azG - (poses[0].MeanAz / sz), 2));
                    if (moveDist < MinMoveDistanceG && distToFirst > 0.25)
                        return new(poses.Count, targetPoses, true, 0.0, $"Pose {poses.Count}/{targetPoses} saved. Return near Pose 1...", false);
                }

                waitingForMove = false;
                window.Clear();
            }
        }

        if (gyroMag > MaxGyroRateDps)
        {
            window.Clear();
            return new(poses.Count, targetPoses, false, 0.0, $"Hold still for Pose {poses.Count + 1}/{targetPoses}...", false);
        }

        window.Add((sample.Ax, sample.Ay, sample.Az, axG, ayG, azG));
        double minX = double.PositiveInfinity, maxX = double.NegativeInfinity;
        double minY = double.PositiveInfinity, maxY = double.NegativeInfinity;
        double minZ = double.PositiveInfinity, maxZ = double.NegativeInfinity;
        foreach (var s in window)
        {
            if (s.AxG < minX) minX = s.AxG; if (s.AxG > maxX) maxX = s.AxG;
            if (s.AyG < minY) minY = s.AyG; if (s.AyG > maxY) maxY = s.AyG;
            if (s.AzG < minZ) minZ = s.AzG; if (s.AzG > maxZ) maxZ = s.AzG;
        }

        if ((maxX - minX) > MaxAxisSpreadG || (maxY - minY) > MaxAxisSpreadG || (maxZ - minZ) > MaxAxisSpreadG)
        {
            window.Clear();
            return new(poses.Count, targetPoses, false, 0.0, $"Stabilizing Pose {poses.Count + 1}/{targetPoses}...", false);
        }

        if (window.Count < SamplesPerPose)
        {
            double prog = (double)window.Count / SamplesPerPose;
            return new(poses.Count, targetPoses, false, prog, $"Holding Pose {poses.Count + 1}/{targetPoses} ({prog * 100:0}%)...", false);
        }

        double meanAx = window.Average(x => x.Ax);
        double meanAy = window.Average(x => x.Ay);
        double meanAz = window.Average(x => x.Az);
        double meanGx = window.Average(x => x.AxG);
        double meanGy = window.Average(x => x.AyG);
        double meanGz = window.Average(x => x.AzG);

        poses.Add(new D10PoseMean(poses.Count + 1, meanAx, meanAy, meanAz, window.Count));
        lastAcceptedG = (meanGx, meanGy, meanGz);
        window.Clear();
        waitingForMove = !IsComplete;

        return IsComplete
            ? new(poses.Count, targetPoses, false, 1.0, "Captured 15/15 poses.", true)
            : new(poses.Count, targetPoses, true, 0.0, $"Pose {poses.Count}/{targetPoses} locked. Move controller to new orientation...", false);
    }

    public D10CalibrationData BuildResult() =>
        D10EllipsoidFitter.Fit(poses, link, baseline);
}

/// <summary>Fits axis-aligned 3D accelerometer offset and scale parameters across multi-pose gravity vectors.</summary>
public static class D10EllipsoidFitter
{
    public const int MinPoseCount = 9;
    public const int MinDistinctDirections = 8;
    public const double MinSeparationDeg = 10.0;

    public static D10CalibrationData Fit(
        IReadOnlyList<D10PoseMean> poses,
        D10LinkKind link,
        D10CalibrationData? baseline = null)
    {
        ArgumentNullException.ThrowIfNull(poses);
        if (poses.Count < MinPoseCount)
            throw new InvalidOperationException($"At least {MinPoseCount} stationary poses are needed (got {poses.Count}).");

        double meanX = poses.Average(p => p.MeanAx);
        double meanY = poses.Average(p => p.MeanAy);
        double meanZ = poses.Average(p => p.MeanAz);
        double spanX = poses.Max(p => p.MeanAx) - poses.Min(p => p.MeanAx);
        double spanY = poses.Max(p => p.MeanAy) - poses.Min(p => p.MeanAy);
        double spanZ = poses.Max(p => p.MeanAz) - poses.Min(p => p.MeanAz);
        double normScale = Math.Max(1.0, (spanX + spanY + spanZ) / 6.0);

        double[,] normal = new double[6, 6];
        double[] rhs = new double[6];
        foreach (var p in poses)
        {
            double x = (p.MeanAx - meanX) / normScale;
            double y = (p.MeanAy - meanY) / normScale;
            double z = (p.MeanAz - meanZ) / normScale;
            double[] row = [x * x, y * y, z * z, x, y, z];
            for (int r = 0; r < 6; r++)
            {
                rhs[r] += row[r];
                for (int c = 0; c < 6; c++)
                    normal[r, c] += row[r] * row[c];
            }
        }

        double[] q = SolveLinear(normal, rhs);
        double a = q[0], b = q[1], cCoeff = q[2], d = q[3], e = q[4], f = q[5];
        if (a <= 0 || b <= 0 || cCoeff <= 0 || !double.IsFinite(a + b + cCoeff))
            throw new InvalidOperationException("Ellipsoid fit did not converge. Cover more 3D orientations.");

        double cx = -d / (2.0 * a);
        double cy = -e / (2.0 * b);
        double cz = -f / (2.0 * cCoeff);
        double k = 1.0 + (a * cx * cx) + (b * cy * cy) + (cCoeff * cz * cz);
        if (k <= 0 || !double.IsFinite(k))
            throw new InvalidOperationException("Ellipsoid center solution is invalid.");

        double ox = meanX + (normScale * cx);
        double oy = meanY + (normScale * cy);
        double oz = meanZ + (normScale * cz);
        double sx = normScale * Math.Sqrt(k / a);
        double sy = normScale * Math.Sqrt(k / b);
        double sz = normScale * Math.Sqrt(k / cCoeff);
        if (sx <= 0 || sy <= 0 || sz <= 0 || !double.IsFinite(sx + sy + sz))
            throw new InvalidOperationException("Ellipsoid scale solution is invalid.");

        var dirs = new List<(double X, double Y, double Z)>();
        double maxDot = Math.Cos(MinSeparationDeg * Math.PI / 180.0);
        foreach (var p in poses)
        {
            double ux = (p.MeanAx - ox) / sx;
            double uy = (p.MeanAy - oy) / sy;
            double uz = (p.MeanAz - oz) / sz;
            double mag = Math.Sqrt((ux * ux) + (uy * uy) + (uz * uz));
            if (mag <= 0 || !double.IsFinite(mag))
                throw new InvalidOperationException("Normalized vector became invalid after fit.");
            var u = (X: ux / mag, Y: uy / mag, Z: uz / mag);
            if (!dirs.Any(prev => (prev.X * u.X) + (prev.Y * u.Y) + (prev.Z * u.Z) > maxDot))
                dirs.Add(u);
        }

        if (dirs.Count < MinDistinctDirections)
            throw new InvalidOperationException($"Orientation coverage too narrow ({dirs.Count}/{MinDistinctDirections} distinct directions).");

        var baseCal = baseline ?? D10CalibrationData.Default;
        if (link == D10LinkKind.WirelessReceiver)
        {
            double meanRadius = (sx + sy + sz) / 3.0;
            return baseCal with { WirelessCountsPerG = meanRadius, IsCalibrated = true };
        }

        return baseCal with
        {
            AccelOffsetX = ox,
            AccelOffsetY = oy,
            AccelOffsetZ = oz,
            AccelScaleX = sx,
            AccelScaleY = sy,
            AccelScaleZ = sz,
            IsCalibrated = true,
        };
    }

    private static double[] SolveLinear(double[,] matrix, double[] rhs)
    {
        int n = rhs.Length;
        double[,] a = (double[,])matrix.Clone();
        double[] b = (double[])rhs.Clone();

        for (int col = 0; col < n; col++)
        {
            int pivot = col;
            double best = Math.Abs(a[col, col]);
            for (int row = col + 1; row < n; row++)
            {
                double val = Math.Abs(a[row, col]);
                if (val > best)
                {
                    best = val;
                    pivot = row;
                }
            }

            if (!double.IsFinite(best) || best < 1e-12)
                throw new InvalidOperationException("Calibration matrix is singular; collect distinct tilt/roll/invert poses.");

            if (pivot != col)
            {
                for (int c = 0; c < n; c++)
                    (a[col, c], a[pivot, c]) = (a[pivot, c], a[col, c]);
                (b[col], b[pivot]) = (b[pivot], b[col]);
            }

            double div = a[col, col];
            for (int c = col; c < n; c++) a[col, c] /= div;
            b[col] /= div;

            for (int row = 0; row < n; row++)
            {
                if (row == col) continue;
                double factor = a[row, col];
                for (int c = col; c < n; c++) a[row, c] -= factor * a[col, c];
                b[row] -= factor * b[col];
            }
        }

        return b;
    }
}
