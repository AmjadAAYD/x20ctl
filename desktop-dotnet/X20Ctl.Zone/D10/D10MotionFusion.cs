namespace X20Ctl.Zone.D10;

public enum D10MotionMode
{
    Off,
    SteerLeftStick,
    AimRightStick,
    GyroMouse
}

public static class D10PaddleActions
{
    public const string None = "NONE";
    public const string ToggleMotion = "TOGGLE_MOTION";
    public const string RecenterGyro = "RECENTER_GYRO";
    public const string MacroM1 = "MACRO_M1";
    public const string MacroM2 = "MACRO_M2";

    public static IReadOnlyList<string> All { get; } =
        [None, ToggleMotion, RecenterGyro, MacroM1, MacroM2, .. ButtonDraft.Targets];

    public static string Label(string action) => action switch
    {
        None => "None (Disabled)",
        ToggleMotion => "Toggle 6-Axis Motion",
        RecenterGyro => "Recenter Gyro",
        MacroM1 => "Run M1 Macro",
        MacroM2 => "Run M2 Macro",
        "DPAD_UP" => "D-Pad Up",
        "DPAD_DOWN" => "D-Pad Down",
        "DPAD_LEFT" => "D-Pad Left",
        "DPAD_RIGHT" => "D-Pad Right",
        "SELECT" => "View / Back",
        "START" => "Menu / Start",
        _ => action,
    };
}

public sealed class D10MotionSettings
{
    public D10MotionMode Mode { get; set; } = D10MotionMode.Off;
    public double FullScaleDegrees { get; set; } = 35.0;
    public double DeadzoneDegrees { get; set; } = 1.5;
    public double Sensitivity { get; set; } = 1.0;
    public double FilterTauSeconds { get; set; } = 0.20;
    public bool InvertAxis { get; set; }
    public bool VibrationGuard { get; set; } = true;
    public bool VelocityLead { get; set; } = true;
    public string M1Binding { get; set; } = "A";
    public string M2Binding { get; set; } = "B";
    public string MBinding { get; set; } = D10PaddleActions.ToggleMotion;

    public D10MotionSettings Validated()
    {
        FullScaleDegrees = Math.Clamp(double.IsFinite(FullScaleDegrees) ? FullScaleDegrees : 35.0, 15.0, 90.0);
        DeadzoneDegrees = Math.Clamp(double.IsFinite(DeadzoneDegrees) ? DeadzoneDegrees : 1.5, 0.0, 15.0);
        if (DeadzoneDegrees >= FullScaleDegrees) DeadzoneDegrees = Math.Max(0.0, FullScaleDegrees - 5.0);
        Sensitivity = Math.Clamp(double.IsFinite(Sensitivity) ? Sensitivity : 1.0, 0.2, 3.0);
        FilterTauSeconds = Math.Clamp(double.IsFinite(FilterTauSeconds) ? FilterTauSeconds : 0.20, 0.05, 0.80);
        if (!D10PaddleActions.All.Contains(M1Binding)) M1Binding = "A";
        if (!D10PaddleActions.All.Contains(M2Binding)) M2Binding = "B";
        if (!D10PaddleActions.All.Contains(MBinding)) MBinding = D10PaddleActions.ToggleMotion;
        return this;
    }

    public D10MotionSettings Clone() => new()
    {
        Mode = Mode,
        FullScaleDegrees = FullScaleDegrees,
        DeadzoneDegrees = DeadzoneDegrees,
        Sensitivity = Sensitivity,
        FilterTauSeconds = FilterTauSeconds,
        InvertAxis = InvertAxis,
        VibrationGuard = VibrationGuard,
        VelocityLead = VelocityLead,
        M1Binding = M1Binding,
        M2Binding = M2Binding,
        MBinding = MBinding,
    };
}

public sealed record D10CalibrationData(
    double CenterAngleDeg = 0.0,
    double GyroBiasX = 0.0,
    double GyroBiasY = 0.0,
    double GyroBiasZ = 0.0,
    double AccelOffsetX = 466.0,
    double AccelOffsetY = 1371.0,
    double AccelOffsetZ = -408.0,
    double AccelScaleX = 4165.0,
    double AccelScaleY = 4217.0,
    double AccelScaleZ = 4356.0,
    double WirelessCountsPerG = 8192.0,
    double GyroCountsPerDps = 16.0,
    bool IsCalibrated = false)
{
    public static readonly D10CalibrationData Default = new();
}

public readonly record struct D10LiveTelemetry(
    double RollDeg,
    double PitchDeg,
    double GyroXDps,
    double GyroYDps,
    double GyroZDps,
    double AccelG,
    double OutputX,
    double OutputY,
    bool VibrationDamping,
    D10ExtraButtons Extra,
    double SampleRateHz);

/// <summary>Collects stationary D10 IMU samples to compute gyro zero-rate bias and resting roll center.</summary>
public sealed class D10Calibrator
{
    public const int TargetSamples = 150;
    private readonly double accelOffsetX, accelOffsetY, accelOffsetZ;
    private readonly double accelScaleX, accelScaleY, accelScaleZ;
    private readonly double wirelessCountsPerG, gyroCountsPerDps;

    private int accepted, rejected;
    private double sumGx, sumGy, sumGz, sumRoll;

    public D10Calibrator(D10CalibrationData? baseline = null)
    {
        var b = baseline ?? D10CalibrationData.Default;
        accelOffsetX = b.AccelOffsetX; accelOffsetY = b.AccelOffsetY; accelOffsetZ = b.AccelOffsetZ;
        accelScaleX = b.AccelScaleX; accelScaleY = b.AccelScaleY; accelScaleZ = b.AccelScaleZ;
        wirelessCountsPerG = b.WirelessCountsPerG;
        gyroCountsPerDps = b.GyroCountsPerDps;
    }

    public int AcceptedCount => accepted;
    public int RejectedCount => rejected;
    public double Progress => Math.Clamp((double)accepted / TargetSamples, 0.0, 1.0);
    public bool IsComplete => accepted >= TargetSamples;

    public bool Feed(D10ImuSample sample, D10LinkKind link)
    {
        if (IsComplete) return true;

        var (axG, ayG, azG) = D10MotionProcessor.NormalizeAccel(
            sample, link, accelOffsetX, accelOffsetY, accelOffsetZ, accelScaleX, accelScaleY, accelScaleZ, wirelessCountsPerG);
        double mag = Math.Sqrt(axG * axG + ayG * ayG + azG * azG);

        double gxDps = sample.Gx / gyroCountsPerDps;
        double gyDps = sample.Gy / gyroCountsPerDps;
        double gzDps = sample.Gz / gyroCountsPerDps;
        double gyroMag = Math.Sqrt(gxDps * gxDps + gyDps * gyDps + gzDps * gzDps);

        if (mag is < 0.55 or > 1.60 || gyroMag > 25.0)
        {
            rejected++;
            return false;
        }

        double rollDeg = Math.Atan2(axG, Math.Sqrt(ayG * ayG + azG * azG)) * 180.0 / Math.PI;
        sumGx += sample.Gx;
        sumGy += sample.Gy;
        sumGz += sample.Gz;
        sumRoll += rollDeg;
        accepted++;
        return true;
    }

    public D10CalibrationData BuildResult(D10CalibrationData? existing = null)
    {
        if (accepted == 0)
            throw new InvalidOperationException("No valid stationary samples collected for D10 calibration.");

        var baseCal = existing ?? D10CalibrationData.Default;
        return baseCal with
        {
            CenterAngleDeg = sumRoll / accepted,
            GyroBiasX = sumGx / accepted,
            GyroBiasY = sumGy / accepted,
            GyroBiasZ = sumGz / accepted,
            IsCalibrated = true,
        };
    }
}

/// <summary>Complementary 6-axis fusion with acceleration gating, motor rumble damping, and steering/mouse output mapping.</summary>
public sealed class D10MotionProcessor
{
    private readonly object sync = new();
    private D10MotionSettings settings = new();
    private D10CalibrationData calibration = D10CalibrationData.Default;

    private bool initialized;
    private double rollDeg, pitchDeg;
    private double prevGz, highFreqRms;

    public D10MotionSettings Settings
    {
        get { lock (sync) return settings.Clone(); }
        set { lock (sync) settings = (value ?? new()).Clone().Validated(); }
    }

    public D10CalibrationData Calibration
    {
        get { lock (sync) return calibration; }
        set { lock (sync) calibration = value ?? D10CalibrationData.Default; }
    }

    public void Reset()
    {
        lock (sync)
        {
            initialized = false;
            rollDeg = pitchDeg = 0;
            prevGz = highFreqRms = 0;
        }
    }

    public D10CalibrationData Recenter()
    {
        lock (sync)
        {
            calibration = calibration with { CenterAngleDeg = rollDeg, IsCalibrated = true };
            return calibration;
        }
    }

    public D10LiveTelemetry Update(D10ImuSample sample, D10LinkKind link, double rawDtSeconds, bool rumbleActive = false)
    {
        lock (sync)
        {
            double dt = Math.Clamp(double.IsFinite(rawDtSeconds) && rawDtSeconds > 0 ? rawDtSeconds : 0.005, 0.0005, 0.025);
            double rateHz = 1.0 / dt;

            var cal = calibration;
            var cfg = settings;

            var (axG, ayG, azG) = NormalizeAccel(
                sample, link,
                cal.AccelOffsetX, cal.AccelOffsetY, cal.AccelOffsetZ,
                cal.AccelScaleX, cal.AccelScaleY, cal.AccelScaleZ,
                cal.WirelessCountsPerG);

            double accelMag = Math.Sqrt(axG * axG + ayG * ayG + azG * azG);
            double accelRollDeg = Math.Atan2(axG, Math.Sqrt(ayG * ayG + azG * azG)) * 180.0 / Math.PI;
            double accelPitchDeg = Math.Atan2(-ayG, Math.Sqrt(axG * axG + azG * azG)) * 180.0 / Math.PI;

            double gx = Math.Clamp((sample.Gx - cal.GyroBiasX) / cal.GyroCountsPerDps, -600.0, 600.0);
            double gy = Math.Clamp((sample.Gy - cal.GyroBiasY) / cal.GyroCountsPerDps, -600.0, 600.0);
            double gz = Math.Clamp((sample.Gz - cal.GyroBiasZ) / cal.GyroCountsPerDps, -600.0, 600.0);

            // Estimate high-frequency motor chatter on Gyro Z so rumble feedback does not shake steering.
            double deltaGz = gz - prevGz;
            prevGz = gz;
            double rmsAlpha = Math.Clamp(dt / 0.12, 0.01, 0.5);
            highFreqRms = Math.Sqrt((1.0 - rmsAlpha) * (highFreqRms * highFreqRms) + rmsAlpha * (deltaGz * deltaGz));
            bool damping = cfg.VibrationGuard && (rumbleActive || highFreqRms > 12.0);
            double effectiveGz = damping ? gz * 0.45 : gz;
            double effectiveGy = damping ? gy * 0.45 : gy;

            if (!initialized && accelMag is >= 0.55 and <= 1.60)
            {
                rollDeg = accelRollDeg;
                pitchDeg = accelPitchDeg;
                initialized = true;
            }
            else
            {
                double predictedRoll = rollDeg + effectiveGz * dt;
                double predictedPitch = pitchDeg + effectiveGy * dt;
                bool accelTrusted = accelMag is >= 0.55 and <= 1.60 && Math.Abs(accelRollDeg - predictedRoll) <= 20.0;
                if (accelTrusted)
                {
                    double alpha = cfg.FilterTauSeconds / (cfg.FilterTauSeconds + dt);
                    rollDeg = alpha * predictedRoll + (1.0 - alpha) * accelRollDeg;
                    pitchDeg = alpha * predictedPitch + (1.0 - alpha) * accelPitchDeg;
                }
                else
                {
                    rollDeg = predictedRoll;
                    pitchDeg = predictedPitch;
                }
            }

            double leadDeg = cfg.VelocityLead ? Math.Clamp(effectiveGz * 0.06, -4.0, 4.0) : 0.0;
            double relativeRoll = rollDeg + leadDeg - cal.CenterAngleDeg;
            double sign = cfg.InvertAxis ? -1.0 : 1.0;

            double outX = 0.0;
            double outY = 0.0;
            if (cfg.Mode is D10MotionMode.SteerLeftStick or D10MotionMode.AimRightStick)
            {
                double mag = Math.Abs(relativeRoll);
                if (mag > cfg.DeadzoneDegrees)
                {
                    double span = Math.Max(1.0, cfg.FullScaleDegrees - cfg.DeadzoneDegrees);
                    double norm = Math.Clamp((mag - cfg.DeadzoneDegrees) / span * cfg.Sensitivity, 0.0, 1.0);
                    outX = Math.Sign(relativeRoll) * norm * sign;
                }
            }
            else if (cfg.Mode == D10MotionMode.GyroMouse)
            {
                double gyroDeadband = 1.5;
                double dx = Math.Abs(effectiveGz) > gyroDeadband ? effectiveGz * cfg.Sensitivity * sign : 0.0;
                double dy = Math.Abs(effectiveGy) > gyroDeadband ? -effectiveGy * cfg.Sensitivity : 0.0;
                outX = Math.Clamp(dx / 150.0, -1.0, 1.0);
                outY = Math.Clamp(dy / 150.0, -1.0, 1.0);
            }

            return new D10LiveTelemetry(
                rollDeg,
                pitchDeg,
                gx,
                gy,
                gz,
                accelMag,
                outX,
                outY,
                damping,
                sample.Extra,
                rateHz);
        }
    }

    internal static (double AxG, double AyG, double AzG) NormalizeAccel(
        D10ImuSample sample,
        D10LinkKind link,
        double offX, double offY, double offZ,
        double scaleX, double scaleY, double scaleZ,
        double wirelessCountsPerG)
    {
        if (link == D10LinkKind.WirelessReceiver)
        {
            double g = wirelessCountsPerG > 0 ? wirelessCountsPerG : 8192.0;
            return (sample.Ax / g, sample.Ay / g, sample.Az / g);
        }

        double sx = scaleX != 0 ? scaleX : 4165.0;
        double sy = scaleY != 0 ? scaleY : 4217.0;
        double sz = scaleZ != 0 ? scaleZ : 4356.0;
        return ((sample.Ax - offX) / sx, (sample.Ay - offY) / sy, (sample.Az - offZ) / sz);
    }
}
