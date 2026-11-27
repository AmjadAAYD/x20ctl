using System.Buffers.Binary;
using X20Ctl.Zone.D10;
using Xunit;

namespace X20Ctl.Zone.Tests;

public sealed class D10Tests
{
    [Fact]
    public void ParsesEp81XboxInputReportAndConvertsToPadState()
    {
        var report = new byte[D10Wire.Ep81ReportLength];
        report[0] = 0x00;
        report[1] = 0x14;
        BinaryPrimitives.WriteUInt16LittleEndian(report.AsSpan(2, 2), 0x1234);
        report[4] = 180;
        report[5] = 240;
        BinaryPrimitives.WriteInt16LittleEndian(report.AsSpan(6, 2), -12345);
        BinaryPrimitives.WriteInt16LittleEndian(report.AsSpan(8, 2), 23456);
        BinaryPrimitives.WriteInt16LittleEndian(report.AsSpan(10, 2), 512);
        BinaryPrimitives.WriteInt16LittleEndian(report.AsSpan(12, 2), -9000);

        Assert.True(D10Wire.TryParseInputReport(report, out var frame));
        var pad = frame.ToPadState();
        Assert.Equal(0x1234, pad.Buttons);
        Assert.Equal(180, pad.LeftTrigger);
        Assert.Equal(240, pad.RightTrigger);
        Assert.Equal(-12345, pad.LeftX);
        Assert.Equal(23456, pad.LeftY);
        Assert.Equal(512, pad.RightX);
        Assert.Equal(-9000, pad.RightY);

        report[1] = 0x13;
        Assert.False(D10Wire.TryParseInputReport(report, out _));
    }

    [Fact]
    public void ParsesWiredEp83MotionAndRearButtons()
    {
        var report = new byte[D10Wire.Ep83ReportLength];
        report[0] = 0x04;
        report[1] = 0x1B;
        report[2] = 0x74;
        report[3] = 0x06;
        report[5] = 0x03; // M1 + M2
        report[6] = 0x40; // DpadUp
        report[7] = 0x81; // M bit0 + DpadLeft
        report[8] = 0x04; // M bit1
        BinaryPrimitives.WriteInt16LittleEndian(report.AsSpan(15, 2), 466);
        BinaryPrimitives.WriteInt16LittleEndian(report.AsSpan(17, 2), 1371);
        BinaryPrimitives.WriteInt16LittleEndian(report.AsSpan(19, 2), 3948);
        BinaryPrimitives.WriteInt16LittleEndian(report.AsSpan(21, 2), 160);
        BinaryPrimitives.WriteInt16LittleEndian(report.AsSpan(23, 2), -320);
        BinaryPrimitives.WriteInt16LittleEndian(report.AsSpan(25, 2), 480);

        Assert.True(D10Wire.TryParseWiredMotionReport(report, out var sample));
        Assert.True(sample.Extra.M);
        Assert.True(sample.Extra.M1);
        Assert.True(sample.Extra.M2);
        Assert.True(sample.Extra.DpadUp);
        Assert.False(sample.Extra.DpadDown);
        Assert.True(sample.Extra.DpadLeft);
        Assert.False(sample.Extra.DpadRight);
        Assert.Equal(466, sample.Ax);
        Assert.Equal(1371, sample.Ay);
        Assert.Equal(3948, sample.Az);
        Assert.Equal(160, sample.Gx);
        Assert.Equal(-320, sample.Gy);
        Assert.Equal(480, sample.Gz);
    }

    [Fact]
    public void ParsesWirelessMotionAndSpecialReportsPreservingRearState()
    {
        var special = new byte[D10Wire.WirelessSpecialLength];
        special[19] = 0x02; // M2 pressed
        special[21] = 0x80; // M requires both 21:0x80 and 22:0x04
        special[22] = 0x04;
        special[62] = 42;

        Assert.True(D10Wire.TryParseWirelessSpecialReport(special, D10ExtraButtons.None, out var extra, out byte counter));
        Assert.Equal((byte)42, counter);
        Assert.False(extra.M1);
        Assert.True(extra.M2);
        Assert.True(extra.M);

        var motion = new byte[D10Wire.WirelessMotionLength];
        BinaryPrimitives.WriteInt16LittleEndian(motion.AsSpan(0, 2), -128);
        BinaryPrimitives.WriteInt16LittleEndian(motion.AsSpan(2, 2), 256);
        BinaryPrimitives.WriteInt16LittleEndian(motion.AsSpan(4, 2), 640);
        BinaryPrimitives.WriteInt16LittleEndian(motion.AsSpan(6, 2), 1000);
        BinaryPrimitives.WriteInt16LittleEndian(motion.AsSpan(8, 2), -2000);
        BinaryPrimitives.WriteInt16LittleEndian(motion.AsSpan(10, 2), 8192);

        Assert.True(D10Wire.TryParseWirelessMotionReport(motion, extra, out var sample));
        Assert.Equal((short)-128, sample.Gx);
        Assert.Equal((short)256, sample.Gy);
        Assert.Equal((short)640, sample.Gz);
        Assert.Equal((short)1000, sample.Ax);
        Assert.Equal((short)-2000, sample.Ay);
        Assert.Equal((short)8192, sample.Az);
        Assert.True(sample.Extra.M);
        Assert.True(sample.Extra.M2);
    }

    [Fact]
    public void BuildsEightByteEp02RumblePacket()
    {
        byte[] packet = D10Wire.BuildRumblePacket(0xA5, 0x3C);
        Assert.Equal([0x00, 0x08, 0x00, 0xA5, 0x3C, 0x00, 0x00, 0x00], packet);
    }

    [Fact]
    public void CalibratorRejectsShakingAndLearnsStationaryBias()
    {
        var cal = new D10Calibrator();
        var shaken = new D10ImuSample(0, 0, 8192, 800, 0, 0, D10ExtraButtons.None); // >25 dps
        Assert.False(cal.Feed(shaken, D10LinkKind.WirelessReceiver));
        Assert.Equal(1, cal.RejectedCount);
        Assert.Equal(0, cal.AcceptedCount);

        var steady = new D10ImuSample(0, 0, 8192, 32, -16, 48, D10ExtraButtons.None);
        for (int i = 0; i < D10Calibrator.TargetSamples; i++)
        {
            Assert.True(cal.Feed(steady, D10LinkKind.WirelessReceiver));
        }

        Assert.True(cal.IsComplete);
        Assert.Equal(1.0, cal.Progress, 3);
        var data = cal.BuildResult();
        Assert.True(data.IsCalibrated);
        Assert.Equal(32.0, data.GyroBiasX, 3);
        Assert.Equal(-16.0, data.GyroBiasY, 3);
        Assert.Equal(48.0, data.GyroBiasZ, 3);
        Assert.Equal(0.0, data.CenterAngleDeg, 2);
    }

    [Fact]
    public void MotionProcessorFusesTiltAndRespectsDeadzoneAndVibrationGuard()
    {
        var processor = new D10MotionProcessor
        {
            Settings = new D10MotionSettings
            {
                Mode = D10MotionMode.SteerLeftStick,
                FullScaleDegrees = 35.0,
                DeadzoneDegrees = 2.0,
                Sensitivity = 1.0,
                VibrationGuard = true,
                VelocityLead = false,
            },
            Calibration = D10CalibrationData.Default,
        };

        // 30 degrees roll in wireless 8192 counts/g coordinates: ax = sin(30) * 8192 = 4096, az = cos(30) * 8192 = 7094
        var tilted = new D10ImuSample(4096, 0, 7094, 0, 0, 0, D10ExtraButtons.None);
        var telemetry = processor.Update(tilted, D10LinkKind.WirelessReceiver, 0.005, rumbleActive: true);
        Assert.True(telemetry.VibrationDamping);
        Assert.InRange(telemetry.RollDeg, 29.0, 31.0);
        Assert.True(telemetry.OutputX > 0.7);

        processor.Recenter();
        var recentered = processor.Update(tilted, D10LinkKind.WirelessReceiver, 0.005, rumbleActive: false);
        Assert.Equal(0.0, recentered.OutputX, 3);
    }

    [Fact]
    public void SettingsClampUnsafeRangesAndProfileStoreRoundTrips()
    {
        string tempPath = Path.Combine(Path.GetTempPath(), $"x20ctl-d10-{Guid.NewGuid():N}.json");
        try
        {
            var store = new D10ProfileStore(tempPath);
            var saved = new D10SavedConfig
            {
                Settings = new D10MotionSettings
                {
                    Mode = D10MotionMode.GyroMouse,
                    FullScaleDegrees = 5.0,
                    DeadzoneDegrees = 99.0,
                    Sensitivity = 10.0,
                    FilterTauSeconds = 2.0,
                    M1Binding = "INVALID_ACTION",
                    M2Binding = D10PaddleActions.RecenterGyro,
                    MBinding = D10PaddleActions.ToggleMotion,
                },
                Calibration = D10CalibrationData.Default with { CenterAngleDeg = 4.25, IsCalibrated = true }
            };

            store.Save(saved);
            var loaded = store.Load();
            Assert.Equal(D10MotionMode.GyroMouse, loaded.Settings.Mode);
            Assert.Equal(15.0, loaded.Settings.FullScaleDegrees);
            Assert.Equal(10.0, loaded.Settings.DeadzoneDegrees); // Clamped to 15 then reduced below full scale
            Assert.Equal(3.0, loaded.Settings.Sensitivity);
            Assert.Equal(0.80, loaded.Settings.FilterTauSeconds);
            Assert.Equal("A", loaded.Settings.M1Binding);
            Assert.Equal(D10PaddleActions.RecenterGyro, loaded.Settings.M2Binding);
            Assert.True(loaded.Calibration.IsCalibrated);
            Assert.Equal(4.25, loaded.Calibration.CenterAngleDeg, 3);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void MotorHarmonicCancellerAttenuates28HzRumbleWhilePreservingTurn()
    {
        var filter = new D10MotorHarmonicCanceller();
        double dt = 0.005;
        double maxResidualAtRest = 0.0;
        for (int i = 0; i < 400; i++)
        {
            double tone = 25.0 * Math.Sin(2.0 * Math.PI * 28.0 * (i * dt));
            var outRes = filter.Update(tone, dt, active: true);
            if (i > 300)
                maxResidualAtRest = Math.Max(maxResidualAtRest, Math.Abs(outRes.CleanRateDps));
        }
        Assert.True(maxResidualAtRest < 10.0, $"Expected 28Hz tone to attenuate cleanly, got {maxResidualAtRest:0.2}");

        var steadyTurn = filter.Update(45.0, dt, active: true);
        Assert.True(steadyTurn.CleanRateDps > 20.0);
    }

    [Fact]
    public void TrajectoryGateRejectsIsolatedImpulseAndDeferrsDuplicateWirelessSample()
    {
        var gate = new D10TrajectoryGate();
        Assert.False(gate.Update(10.0, freshSample: true, 0.005, enabled: true).Held);
        Assert.False(gate.Update(12.0, freshSample: true, 0.005, enabled: true).Held);

        // Single 180 dps spike is held for 1 frame rather than immediately leaking through.
        var held = gate.Update(194.0, freshSample: true, 0.005, enabled: true);
        Assert.True(held.Held);
        Assert.InRange(held.OutputRateDps, 12.0, 16.0);

        // Repeated duplicate wireless tuple within 12 ms defers confirmation.
        var deferred = gate.Update(194.0, freshSample: false, 0.005, enabled: true);
        Assert.True(deferred.Held);

        // Dropping back to baseline cancels the isolated spike.
        var recovered = gate.Update(13.0, freshSample: true, 0.005, enabled: true);
        Assert.False(recovered.Held);
        Assert.Equal(13.0, recovered.OutputRateDps, 3);

        // Sustained jump releases on second fresh confirming frame.
        Assert.True(gate.Update(190.0, freshSample: true, 0.005, enabled: true).Held);
        var confirmed = gate.Update(192.0, freshSample: true, 0.005, enabled: true);
        Assert.False(confirmed.Held);
        Assert.Equal(192.0, confirmed.OutputRateDps, 3);
    }

    [Fact]
    public void MidRangeBoostIsMonotonicAndClamped()
    {
        double a = D10MidRangeBoost.Apply(0.0, 0.2, 0.08);
        double b = D10MidRangeBoost.Apply(0.5, 0.2, 0.08);
        double c = D10MidRangeBoost.Apply(1.0, 0.2, 0.08);
        Assert.Equal(0.0, a, 5);
        Assert.True(b > 0.5 && b < c);
        Assert.Equal(1.0, c, 5);
    }

    [Fact]
    public void ShortcutDetectorHandlesShortLongAndDiagonalNeutralReset()
    {
        var det = new D10ShortcutDetector();

        // Short M + Up -> release before 3000 ms emits Short Up.
        Assert.Null(det.Update(new D10ExtraButtons(M: true, false, false, DpadUp: true, false, false, false), 100));
        var shortUp = det.Update(new D10ExtraButtons(M: true, false, false, DpadUp: false, false, false, false), 450);
        Assert.Equal(new D10ShortcutGesture(D10ShortcutKind.Short, D10ShortcutDir.Up), shortUp);

        // Release to full neutral before next gesture.
        Assert.Null(det.Update(D10ExtraButtons.None, 500));

        // Long M + Down -> 3000 ms hold emits Long Down once.
        Assert.Null(det.Update(new D10ExtraButtons(M: true, false, false, false, DpadDown: true, false, false), 1000));
        var longDown = det.Update(new D10ExtraButtons(M: true, false, false, false, DpadDown: true, false, false), 4000);
        Assert.Equal(new D10ShortcutGesture(D10ShortcutKind.Long, D10ShortcutDir.Down), longDown);
        Assert.Null(det.Update(new D10ExtraButtons(M: true, false, false, false, DpadDown: true, false, false), 4500));

        // Ambiguous diagonal cancels until neutral.
        Assert.Null(det.Update(D10ExtraButtons.None, 4600));
        Assert.Null(det.Update(new D10ExtraButtons(M: true, false, false, DpadUp: true, false, DpadLeft: true, false), 5000));
        Assert.Null(det.Update(new D10ExtraButtons(M: true, false, false, DpadUp: true, false, false, false), 5100));
        Assert.Null(det.Update(D10ExtraButtons.None, 5200));
    }

    [Fact]
    public void EllipsoidFitterAndMultiPoseCalibratorRecoverAccelOffsetsAndScales()
    {
        // Synthetic unit directions across 15 orientations with known offset (450, 1200, -350) and scales (4100, 4250, 4400)
        var dirs = new (double X, double Y, double Z)[]
        {
            (0, 0, 1), (0, 0, -1), (1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0),
            (0.707, 0.707, 0), (-0.707, 0.707, 0), (0.707, -0.707, 0), (-0.707, -0.707, 0),
            (0.707, 0, 0.707), (-0.707, 0, 0.707), (0, 0.707, 0.707), (0, -0.707, 0.707),
            (0.577, 0.577, 0.577),
        };
        var cal = new D10MultiPoseCalibrator(D10LinkKind.WiredUsb);
        foreach (var d in dirs)
        {
            double norm = Math.Sqrt(d.X * d.X + d.Y * d.Y + d.Z * d.Z);
            short ax = (short)Math.Round(450 + (d.X / norm) * 4100);
            short ay = (short)Math.Round(1200 + (d.Y / norm) * 4250);
            short az = (short)Math.Round(-350 + (d.Z / norm) * 4400);
            var sample = new D10ImuSample(ax, ay, az, 0, 0, 0, D10ExtraButtons.None);
            D10MultiPoseStatus st = default!;
            for (int i = 0; i < D10MultiPoseCalibrator.SamplesPerPose + 2; i++)
                st = cal.Feed(sample);
        }

        Assert.True(cal.IsComplete);
        var fitted = cal.BuildResult();
        Assert.True(fitted.IsCalibrated);
        Assert.InRange(fitted.AccelOffsetX, 445, 455);
        Assert.InRange(fitted.AccelOffsetY, 1195, 1205);
        Assert.InRange(fitted.AccelOffsetZ, -355, -345);
        Assert.InRange(fitted.AccelScaleX, 4090, 4110);
        Assert.InRange(fitted.AccelScaleY, 4240, 4260);
        Assert.InRange(fitted.AccelScaleZ, 4390, 4410);
    }

    [Fact]
    public void MultiProfileStoreSupportsQuickSlotsGameExesAndV1Migration()
    {
        string tempPath = Path.Combine(Path.GetTempPath(), $"x20ctl-d10-v2-{Guid.NewGuid():N}.json");
        try
        {
            // v1 file migration: preserves existing Settings in default Quick Slot 1 profile.
            File.WriteAllText(tempPath, """
            {
              "Version": 1,
              "Settings": {
                "Mode": "SteerLeftStick",
                "FullScaleDegrees": 45
              }
            }
            """);
            var store = new D10ProfileStore(tempPath);
            var migrated = store.Load();
            Assert.Equal(2, migrated.Version);
            Assert.Single(migrated.Profiles);
            Assert.Equal(45.0, migrated.GetActiveProfile().Settings.FullScaleDegrees);
            Assert.Equal(1, migrated.GetActiveProfile().QuickSlot);

            migrated.Profiles.Add(new D10NamedProfile
            {
                Id = "rally",
                Name = "Rally Racing",
                QuickSlot = 2,
                GameExes = ["dirt2.exe", "wrc.exe"],
                Settings = new D10MotionSettings { Mode = D10MotionMode.SteerLeftStick, M1Binding = "K:32" }
            });
            store.Save(migrated);

            var reloaded = store.Load();
            Assert.Equal("rally", reloaded.FindByQuickSlot(2)?.Id);
            Assert.Equal("rally", reloaded.FindByProcessExe("DIRT2.EXE")?.Id);
            Assert.True(D10PaddleActions.IsHostKeyOrMouse("K:32"));
            Assert.True(D10PaddleActions.IsHostKeyOrMouse("M:LeftClick"));
            Assert.False(D10PaddleActions.IsHostKeyOrMouse("A"));
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }
}
