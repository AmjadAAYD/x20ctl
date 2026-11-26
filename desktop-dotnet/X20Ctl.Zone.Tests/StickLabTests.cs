using X20Ctl.Zone.PcMode;
using Xunit;

namespace X20Ctl.Zone.Tests;

public class StickLabTests
{
    [Fact]
    public void Spline10PointsIsMonotonicBoundedAndHitsControlNodes()
    {
        var points = new List<CurvePoint>
        {
            new(0, 0), new(10, 5), new(20, 12), new(30, 22), new(40, 35),
            new(50, 50), new(65, 72), new(80, 88), new(90, 96), new(100, 100),
        };
        double prev = -1;
        for (int i = 0; i <= 100; i++)
        {
            double y = StickLabMath.EvaluateSpline(points, i / 100.0);
            Assert.InRange(y, 0.0, 1.0);
            Assert.True(y >= prev - 1e-6, $"Non-monotonic at {i}%");
            prev = y;
        }
        Assert.Equal(0.50, StickLabMath.EvaluateSpline(points, 0.50), 2);
        Assert.Equal(0.88, StickLabMath.EvaluateSpline(points, 0.80), 2);
    }

    [Fact]
    public void SmartCurvePresetsProduceValidPointCounts()
    {
        foreach (string preset in new[] { "linear", "precision", "aggressive", "scurve", "dualzone", "exponential" })
        {
            var pts = StickLabMath.GenerateSmartCurve(preset);
            Assert.InRange(pts.Count, 2, StickLabSettings.MaxCustomPoints);
            Assert.Equal(0, pts[0].X);
            Assert.Equal(100, pts[^1].X);
        }
    }

    [Fact]
    public void DeadzoneModesBehaveDistinctlyAndAccurately()
    {
        // Axial deadzone zeros an axis inside inner band even when the other axis is pushed far
        var axialTune = new StickTuning
        {
            Inner = 10,
            Outer = 100,
            Lab = new StickLabSettings { DeadzoneShape = DeadzoneShape.Axial },
        };
        var (ax, ay) = PadProcessor.Stick(26000, 1600, null, axialTune); // Y ~ 4.9% < 10%
        Assert.True(ax > 20000);
        Assert.Equal(0, ay);

        // Radial deadzone keeps diagonal direction intact when radius > 10%
        var radialTune = new StickTuning
        {
            Inner = 10,
            Outer = 100,
            Lab = new StickLabSettings { DeadzoneShape = DeadzoneShape.Radial },
        };
        var (rx, ry) = PadProcessor.Stick(26000, 1600, null, radialTune);
        Assert.True(rx > 20000);
        Assert.NotEqual(0, ry);
    }

    [Fact]
    public void RotationInvertAngleSnapAndGateMorphApplyCorrectly()
    {
        // 90 deg rotation? Wait max is 45 deg: 45 deg rotates (1, 0) into (+0.707, +0.707)
        var (rx, ry) = StickLabMath.Rotate(1.0, 0.0, 45.0);
        Assert.Equal(Math.Sqrt(0.5), rx, 3);
        Assert.Equal(Math.Sqrt(0.5), ry, 3);

        // Invert X/Y flips sign cleanly
        var invTune = new StickTuning { Lab = new StickLabSettings { InvertX = true, InvertY = true } };
        var (ix, iy) = PadProcessor.Stick(16384, -16384, null, invTune);
        Assert.True(ix < -15000);
        Assert.True(iy > 15000);

        // Angle snap snaps 6 degrees off East onto pure East (1, 0) when snap cone is 10 degrees
        var ang = 6.0 * Math.PI / 180.0;
        var (sx, sy) = StickLabMath.AngleSnap(Math.Cos(ang), Math.Sin(ang), 10.0, false);
        Assert.Equal(1.0, sx, 4);
        Assert.Equal(0.0, sy, 4);

        // Continuous gate morph 0% keeps diagonal magnitude ~1 (ux ~0.707), 100% expands to full square corner (1,1)
        double d = Math.Sqrt(0.5);
        var (cx, cy) = StickLabMath.MorphGate(d, d, 1.0, 0);
        var (mx, my) = StickLabMath.MorphGate(d, d, 1.0, 100);
        Assert.Equal(d, cx, 3);
        Assert.Equal(1.0, mx, 3);
        Assert.Equal(1.0, my, 3);
    }

    [Fact]
    public void UpPrioritySocdAutoSprintSwapSticksAndAdsSniperWork()
    {
        var profile = new PcModeProfile
        {
            DPadSocd = SocdMode.UpPriority,
            SwapSticks = true,
            AutoSprint = new AutoSprintSettings { Enabled = true, OuterThresholdPct = 88, OutputButton = "L3" },
            Sniper = new SniperModifier { Enabled = true, AdsLeftTriggerLink = true, ScalePct = 50 },
        };
        var proc = new PadProcessor(profile);

        // Opposing Up+Down chooses Up under UpPriority; swapped sticks route physical Right -> L and Left -> R
        // Left input becomes Right (subject to LT-linked 50% Sniper scale); Right input >= 90% triggers AutoSprint L3!
        var output = proc.Process(new PadState(
            Buttons: (ushort)(PadButtons.Up | PadButtons.Down),
            LeftTrigger: 200,
            RightTrigger: 0,
            LeftX: 20000,
            LeftY: 0,
            RightX: 31000,
            RightY: 0), 10);

        Assert.NotEqual(0, output.Buttons & PadButtons.Up);
        Assert.Equal(0, output.Buttons & PadButtons.Down);
        Assert.NotEqual(0, output.Buttons & PadButtons.L3);
        Assert.InRange(output.LeftX, 29000, 32767);
        Assert.InRange(output.RightX, 9000, 11000);
    }

    [Fact]
    public void TriggerLabCustomDeadzoneAndSplineWork()
    {
        var profile = new PcModeProfile
        {
            Triggers =
            {
                ["RT"] = new TriggerTuning
                {
                    InnerDeadzonePct = 10,
                    OuterThresholdPct = 80,
                    CustomPoints = [new(0, 0), new(50, 75), new(100, 100)],
                }
            }
        };
        var proc = new PadProcessor(profile);
        var belowDead = proc.Process(new PadState(0, 0, 20, 0, 0, 0, 0), 10); // ~7.8% < 10%
        var midPull = proc.Process(new PadState(0, 0, 115, 0, 0, 0, 0), 20);  // ~45% -> ~50% mapped -> ~75% output
        var fullAt80 = proc.Process(new PadState(0, 0, 210, 0, 0, 0, 0), 30); // > 80% -> 255

        Assert.Equal(0, belowDead.RightTrigger);
        Assert.InRange(midPull.RightTrigger, 175, 205);
        Assert.Equal(255, fullAt80.RightTrigger);
    }

    [Fact]
    public void SnapbackDetectorFlagsFastOppositeReleaseBounce()
    {
        var sb = new StickSnapbackDetector();
        sb.Observe(32000, 0);
        sb.Observe(12000, 0);
        sb.Observe(-6500, 0); // ~19.8% opposite bounce
        Assert.Equal(1, sb.BounceEvents);
        Assert.True(sb.PeakBouncePct >= 19.0);
    }

    [Fact]
    public void ShareCodeRoundTripsAndRejectsTampering()
    {
        var curve = new SetupCurve("custom", "fine", 4, 92, 70, 50, 180, 170);
        var tune = new StickTuning
        {
            Inner = 4.5,
            Outer = 92,
            AxisRatio = 115,
            Lab = new StickLabSettings { DeadzoneShape = DeadzoneShape.Hybrid, GateMorph = 50, RotationDegrees = -6 },
        };
        string code = StickLabShareCode.Encode(CurveChannel.RightStick, curve, tune, SocdMode.UpPriority, SocdMode.Neutral);
        Assert.StartsWith("X20C1.", code);

        Assert.True(StickLabShareCode.TryDecode(code, out var decoded, out string err), err);
        Assert.NotNull(decoded);
        Assert.Equal("RightStick", decoded!.Channel);
        Assert.Equal(4.5, decoded.Tuning!.Inner);
        Assert.Equal(DeadzoneShape.Hybrid, decoded.Tuning.Lab!.DeadzoneShape);
        Assert.Equal(SocdMode.UpPriority, decoded.DPadSocd);

        // Corrupt checksum fails cleanly
        string corrupted = code[..^2] + (code.EndsWith("00", StringComparison.Ordinal) ? "FF" : "00");
        Assert.False(StickLabShareCode.TryDecode(corrupted, out _, out _));
    }
}
