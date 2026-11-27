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
}
