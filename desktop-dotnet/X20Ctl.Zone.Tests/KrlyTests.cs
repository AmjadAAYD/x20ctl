using System.IO;
using X20Ctl.Zone.PcMode;
using X20Ctl.Zone.X10;

namespace X20Ctl.Zone.Tests;

/// <summary>KRLY / QMacro protocol, session state machine, and EasySMX X10 bridge tests.</summary>
public class KrlyTests
{
    static byte[] H(string hex) => Convert.FromHexString(hex.Replace(" ", ""));

    [Fact]
    public void Battery_reads_offset_five()
    {
        var b = Krly.ParseBattery(H("08 31 01 00 01 3C 00 00"))!;
        Assert.Equal((60, false, false), (b.Percent, b.Charging, b.Low));
        Assert.Null(Krly.ParseBattery(H("08 31 01 00 01 65 00 00"))); // 101%
        Assert.Null(Krly.ParseBattery(H("08 32 01 00 01 3C 00 00")));
    }

    [Fact]
    public void Version_reads_the_real_reply()
    {
        var v = Krly.ParseVersion(H("12 84 01 00 56 02 87 03 00 40 02 40 71 9A 01 00 A0 02"))!;
        Assert.Equal(("560287", "030040", "0240719A", "0100A0"), (v.Project, v.Protocol, v.Firmware, v.Hardware));
    }

    [Fact]
    public void Settings_read_the_real_two_frames()
    {
        var pages = new Krly.SettingsPages();
        Assert.Null(pages.Accept(H("14 77 02 02 01 00 04 03 61 03 61 09 05 64 00 00 05 64 00 01")));
        var s = pages.Accept(H("0B 77 02 02 02 00 00 02 64 64 01"))!;
        Assert.Equal(new Krly.Range(3, 97), s.LeftTrigger);
        Assert.Equal(new Krly.Range(3, 97), s.RightTrigger);
        Assert.Equal(new Krly.Stick(5, 100, false, false), s.LeftStick);
        Assert.Equal((100, 100), (s.LeftMotor, s.RightMotor));
        Assert.Empty(s.Turbo);
    }

    [Fact]
    public void Settings_pages_out_of_order_fail()
    {
        var pages = new Krly.SettingsPages();
        Assert.Throws<FormatException>(() => pages.Accept(H("0B 77 02 03 03 00 00 02 64 64 01")));
    }

    [Fact]
    public void Mappings_read_paged_and_empty()
    {
        var pages = new Krly.MappingPages();
        Assert.Null(pages.Accept(H("08 50 02 01 06 00 01 09")));
        var map = pages.Accept(H("09 50 02 02 A0 00 A1 A2 0A"))!;
        Assert.Equal("A", map["M1"]); Assert.Equal("X", map["B"]); Assert.Equal(2, map.Count);
        Assert.Empty(new Krly.MappingPages().Accept(H("06 50 01 01 00 00"))!);
    }

    [Fact]
    public void MacroPages_decode_factory_passthrough_disabled_and_multistep_macros()
    {
        var pages = new Krly.MacroPages();
        // Page 1: M1 disabled (7 bytes: 01 01 00 00 00 00 00) + M2 compact factory passthrough to B (02 01 00 00 00 00 01 00 00 00 00 01 A1)
        // Page 2: M3 repeat 2-step macro (A then Y)
        byte[] page1Content = H("01 01 00 00 00 00 00 02 01 00 00 00 00 01 00 00 00 00 01 A1");
        byte[] page2Content = H("03 00 00 00 00 00 02 00 00 28 00 14 01 A0 01 00 50 00 00 01 A3");

        byte[] frame1 = [(byte)(page1Content.Length + 5), Krly.QueryMacros, 2, 1, .. page1Content, 0x01];
        byte[] frame2 = [(byte)(page2Content.Length + 5), Krly.QueryMacros, 2, 2, .. page2Content, 0x02];

        Assert.Null(pages.Accept(frame1));
        var slots = pages.Accept(frame2)!;

        Assert.Equal(Krly.PaddleSlotKind.Disabled, slots["M1"].Kind);
        Assert.Equal(Krly.PaddleSlotKind.Passthrough, slots["M2"].Kind);
        Assert.Equal("B", slots["M2"].TargetKey);
        Assert.Equal(Krly.PaddleSlotKind.Macro, slots["M3"].Kind);
        Assert.True(slots["M3"].Repeat);
        Assert.Equal(2, slots["M3"].Steps.Count);
        Assert.Equal(new Krly.PaddleMacroStep(40, 20, "A"), slots["M3"].Steps[0]);
        Assert.Equal(new Krly.PaddleMacroStep(80, 0, "Y"), slots["M3"].Steps[1]);

        Assert.Throws<FormatException>(() => new Krly.MacroPages().Accept(H("06 79 02 02 00 01")));
    }

    [Fact]
    public void Snapshot_treats_0x79_as_authoritative_over_0x50_for_rear_paddles()
    {
        var snap = X10HardwareSnapshot.FromRaw(
            "EasySMX X10",
            new("560287", "030040", "0240719A", "0100A0"),
            new(80, false, false),
            new(new(3, 97), new(3, 97), new(5, 100, false, false), new(5, 100, false, false), 100, 100, []),
            new Dictionary<string, string> { ["M1"] = "A", ["B"] = "X" },
            new Dictionary<string, Krly.PaddleAssignment> { ["M1"] = new("M1", Krly.PaddleSlotKind.Disabled, null, false, []) });

        Assert.DoesNotContain("M1", snap.FaceMappings.Keys);
        Assert.Equal("X", snap.FaceMappings["B"]);
        Assert.Equal(Krly.PaddleSlotKind.Disabled, snap.Paddles["M1"].Kind);
    }

    [Fact]
    public void Frame_reader_splits_and_joins_notifications()
    {
        var reader = new Krly.FrameReader();
        Assert.Empty(reader.Feed(H("08 31 01")));
        var frames = reader.Feed(H("00 01 3C 00 00 04 3D 00 01"));
        Assert.Equal(2, frames.Count);
        Assert.Equal(60, Krly.ParseBattery(frames[0])!.Percent);
        Assert.True(Krly.IsAck(frames[1], Krly.Mapping));
    }

    [Fact]
    public void Writes_encode_documented_examples_including_clear_motor_reset_and_calibration()
    {
        Assert.Equal(H("0B 3A 01 01 00 00 00 00 00 A0 00"), Krly.Frame(Krly.MacroStep, Krly.PaddlePassthroughPayload("M1", "A"), 0));
        Assert.Equal(H("0A 3A 01 01 00 00 00 00 00 00"), Krly.Frame(Krly.MacroStep, Krly.PaddleClearPayload("M1"), 0));
        Assert.Equal(H("0B 3E 05 5A 01 00 03 5F 00 01 00"), Krly.Frame(Krly.Sticks, Krly.SticksPayload(new(5, 90, true, false), new(3, 95, false, true)), 0));
        Assert.Equal(H("0B 3A 02 00 00 01 2C 03 E8 A2 00"), Krly.Frame(Krly.MacroStep, Krly.MacroStepPayload("M2", true, 0, 300, 1000, "X"), 0));
        Assert.Equal(H("05 3D A1 A0 00"), Krly.Frame(Krly.Mapping, Krly.MappingPayload("B", "A"), 0));
        Assert.Equal(H("04 05 00 00"), Krly.Frame(Krly.FactoryReset, Krly.FactoryResetPayload(), 0));
        Assert.Equal(H("06 45 50 00 01 00"), Krly.Frame(Krly.MotorTest, Krly.MotorTestLeftPayload(80), 0));
        Assert.Equal(H("06 45 00 00 00 00"), Krly.Frame(Krly.MotorTest, Krly.MotorStopPayload(), 0));
        Assert.Equal(H("05 78 01 01 00"), Krly.Frame(Krly.Calibrate, Krly.CalibrationPayload(true, Krly.CalibrationTarget.Sticks), 0));
        Assert.Equal(H("05 78 02 02 00"), Krly.Frame(Krly.Calibrate, Krly.CalibrationPayload(false, Krly.CalibrationTarget.Triggers), 0));
        Assert.Equal(H("03 31 00"), Krly.Frame(Krly.QueryBattery, [], 0));
    }

    [Fact]
    public void Upload_plan_orders_commands_and_never_sends_paddles_on_0x3D()
    {
        var s = new Krly.Settings(new(3, 97), new(4, 96), new(5, 90, true, false), new(6, 95, false, true), 80, 60, [new("A", true, 12)]);
        var paddles = new[]
        {
            new Krly.PaddleAssignment("M1", Krly.PaddleSlotKind.Disabled, null, false, []),
            new Krly.PaddleAssignment("M2", Krly.PaddleSlotKind.Macro, null, true, [new(120, 40, "X")]),
        };
        var face = new Dictionary<string, string> { ["A"] = "B", ["M1"] = "Y" }; // stray M1 in face dict must be ignored on 0x3D

        var plan = Krly.BuildUploadPlan(s, face, paddles);
        Assert.Equal(new byte[] { 0x01, 0x36, 0x3A, 0x3A, 0x37, 0x39, 0x3E, 0x3D, 0x35, 0x34, 0x01 }, plan.Select(p => p.Command));
        Assert.Equal(7, plan[2].Payload.Length); // M1 disabled is 7 bytes
        Assert.Equal(8, plan[3].Payload.Length); // M2 macro step is 8 bytes
        Assert.Equal(H("A0 A1"), plan[7].Payload); // Only A -> B went to 0x3D

        // Convenience ApplyPlan also routes M1/M2 away from 0x3D into 0x3A passthrough
        var compatPlan = Krly.ApplyPlan(s, new Dictionary<string, string> { ["M1"] = "A", ["B"] = "X" });
        Assert.Contains(compatPlan, p => p.Command == Krly.MacroStep && p.Payload.Length == 8);
        Assert.DoesNotContain(compatPlan, p => p.Command == Krly.Mapping && p.Payload[0] == Krly.Keys["M1"]);
    }

    [Fact]
    public async Task Session_roundtrips_read_upload_verify_motor_reset_and_calibration()
    {
        await using var fake = new FakeX10Transport();
        using var session = new X10ProtocolSession(fake, TimeSpan.FromSeconds(2));

        var buttons = new ButtonDraft("x10");
        buttons.Set("A", "B");
        var curves = new CurveDraft("x10");
        curves.Set(CurveChannel.LeftStick, curves[CurveChannel.LeftStick] with { Inner = 8, Outer = 92, Flags = (byte)CurveHardwareFlags.InvertX });
        var macros = new MacroDraft("x10");
        macros.Add("M2", 30);
        macros.Update("M2", 0, macros["M2"][0] with { Buttons = Array.AsReadOnly(["X"]), HoldMs = 150, PauseMs = 30 });
        var vib = new VibrationDraft("x10");
        vib.SetMotors(75, 45, 0);
        var x10 = new X10NativeDraft();
        x10.SetPaddleDisabled("M1");
        x10.SetPaddleMacro("M2", repeat: true);
        x10.SetTurbo("Y", automatic: true, enabled: true, rate: 15);

        var profile = X10ProfileBridge.BuildProfile(buttons, curves, macros, vib, x10);
        var report = await session.UploadAndVerifyAsync(profile);

        Assert.Equal(Krly.VerificationStatus.Matched, report.Status);
        Assert.Empty(report.DifferingGroups);
        Assert.Equal(new byte[] { 0x01, 0x36 }, fake.SentCommands.Take(2).ToArray());
        Assert.Equal(75, report.ReadBack!.Settings.LeftMotor);
        Assert.Equal(Krly.PaddleSlotKind.Disabled, report.ReadBack.Paddles["M1"].Kind);
        Assert.Equal(Krly.PaddleSlotKind.Macro, report.ReadBack.Paddles["M2"].Kind);

        // Bridge load-back preserves edits accurately
        var b2 = new ButtonDraft("x10"); var c2 = new CurveDraft("x10"); var m2 = new MacroDraft("x10"); var v2 = new VibrationDraft("x10"); var x2 = new X10NativeDraft();
        X10ProfileBridge.LoadIntoDrafts(report.ReadBack, b2, c2, m2, v2, x2);
        Assert.Equal("B", b2.Target("A"));
        Assert.Equal((8, 92, (byte)CurveHardwareFlags.InvertX), (c2[CurveChannel.LeftStick].Inner, c2[CurveChannel.LeftStick].Outer, c2[CurveChannel.LeftStick].Flags));
        Assert.Equal((75, 45), (v2.Left, v2.Right));
        Assert.Equal(Krly.PaddleSlotKind.Disabled, x2.PaddleMode("M1"));
        Assert.Equal(Krly.PaddleSlotKind.Macro, x2.PaddleMode("M2"));
        Assert.Single(m2["M2"]);
        Assert.Single(x2.Turbo);

        // Diagnostic motor test sends start (0x45) and stop cleanup (0x45 00 00 00)
        await session.DiagnosticMotorTestAsync(left: true, strength: 65, durationMs: 25);
        Assert.Contains(fake.SentPayloads, p => p.Command == Krly.MotorTest && p.Payload.SequenceEqual(new byte[] { 65, 0, 1 }));
        Assert.Contains(fake.SentPayloads, p => p.Command == Krly.MotorTest && p.Payload.SequenceEqual(new byte[] { 0, 0, 0 }));

        // Stick calibration emits active + succeeded and sends stop (0x78 02 01)
        var phases = new List<Krly.CalibrationPhase>();
        var cal = await session.RunCalibrationAsync(Krly.CalibrationTarget.Sticks, ev => phases.Add(ev.Phase));
        Assert.Equal(Krly.CalibrationPhase.Succeeded, cal.Phase);
        Assert.Equal([Krly.CalibrationPhase.Active, Krly.CalibrationPhase.Succeeded], phases);
        Assert.Contains(fake.SentPayloads, p => p.Command == Krly.Calibrate && p.Payload.SequenceEqual(new byte[] { 0x02, 0x01 }));

        // Self-calibration listens for 0x43/0x49 and sends stop (0x42)
        Assert.True(await session.RunSelfCalibrationAsync());
        Assert.Contains(fake.SentCommands, c => c == Krly.SelfCalStart);
        Assert.Contains(fake.SentCommands, c => c == Krly.SelfCalStop);

        // Factory reset resets device state
        await session.FactoryResetAsync();
        var afterReset = await session.ReadSnapshotAsync();
        Assert.Equal(100, afterReset.Settings.LeftMotor);
        Assert.Empty(afterReset.FaceMappings);
        Assert.Empty(afterReset.Settings.Turbo);
    }

    [Fact]
    public async Task Session_restores_normal_mode_after_mid_upload_rejection_and_detects_mismatch()
    {
        await using var fake = new FakeX10Transport { FailOnCommand = Krly.Motors };
        using var session = new X10ProtocolSession(fake, TimeSpan.FromMilliseconds(400));

        var profile = X10ProfileBridge.BuildProfile(new ButtonDraft("x10"), new CurveDraft("x10"), new MacroDraft("x10"), new VibrationDraft("x10"), new X10NativeDraft());
        await Assert.ThrowsAsync<IOException>(() => session.UploadAndVerifyAsync(profile));
        Assert.Equal((Krly.SetMode, (byte)0x00), (fake.SentPayloads.Last().Command, fake.SentPayloads.Last().Payload[0]));

        fake.FailOnCommand = null;
        fake.ForceMotorMismatchOnReadback = true;
        var mismatchReport = await session.UploadAndVerifyAsync(profile);
        Assert.Equal(Krly.VerificationStatus.Mismatch, mismatchReport.Status);
        Assert.Contains("Motors", mismatchReport.DifferingGroups);

        fake.ForceMotorMismatchOnReadback = false;
        fake.FailOnCommand = Krly.QuerySettings;
        var unconfirmed = await session.UploadAndVerifyAsync(profile);
        Assert.Equal(Krly.VerificationStatus.Unconfirmed, unconfirmed.Status);
    }

    private sealed class FakeX10Transport : IX10WireTransport
    {
        private readonly List<byte> txBuf = [];
        private readonly Dictionary<byte, byte> face = [];
        private readonly Dictionary<byte, List<(bool Repeat, int Hold, int Wait, byte Key)>> macros = new()
        {
            [0x01] = [(false, 0, 1, 0xA0)],
            [0x02] = [(false, 0, 1, 0xA1)],
        };
        private byte[] lt = [3, 97], rt = [3, 97], ls = [5, 100, 0, 0], rs = [5, 100, 0, 0];
        private int lm = 100, rm = 100;
        private readonly List<(byte Key, byte Auto, byte Rate)> turbo = [];

        public List<byte> SentCommands { get; } = [];
        public List<(byte Command, byte[] Payload)> SentPayloads { get; } = [];
        public byte? FailOnCommand { get; set; }
        public bool ForceMotorMismatchOnReadback { get; set; }

        public string DeviceName => "Fake X10";
        public event Action<byte[]>? NotificationReceived;

        public Task WriteChunkAsync(byte[] chunk, CancellationToken cancel)
        {
            lock (txBuf)
            {
                txBuf.AddRange(chunk);
                while (txBuf.Count > 0 && txBuf.Count >= txBuf[0])
                {
                    int len = txBuf[0];
                    byte[] frame = [.. txBuf.GetRange(0, len)];
                    txBuf.RemoveRange(0, len);
                    HandleFrame(frame);
                }
            }
            return Task.CompletedTask;
        }

        private void HandleFrame(byte[] frame)
        {
            byte cmd = frame[1];
            byte[] payload = frame[2..^1];
            byte seq = frame[^1];
            SentCommands.Add(cmd);
            SentPayloads.Add((cmd, payload));

            if (FailOnCommand == cmd)
            {
                Emit([4, cmd, 0x01, seq]);
                return;
            }

            switch (cmd)
            {
                case Krly.QueryVersion:
                    Emit([0x12, Krly.QueryVersion, 0x01, 0x00, 0x56, 0x02, 0x87, 0x03, 0x00, 0x40, 0x02, 0x40, 0x71, 0x9A, 0x01, 0x00, 0xA0, seq]);
                    break;
                case Krly.QueryBattery:
                    Emit([0x08, Krly.QueryBattery, 0x01, 0x00, 0x01, 88, 0x00, seq]);
                    break;
                case Krly.QuerySettings:
                {
                    var body = new List<byte>();
                    body.Add((byte)(turbo.Count * 3));
                    foreach (var t in turbo) { body.Add(t.Key); body.Add(t.Auto); body.Add(t.Rate); }
                    body.Add(4); body.AddRange(lt); body.AddRange(rt);
                    body.Add(9); body.AddRange(ls); body.AddRange(rs); body.Add(0);
                    body.Add(2); body.Add((byte)(ForceMotorMismatchOnReadback ? 12 : lm)); body.Add((byte)rm);
                    Emit([(byte)(body.Count + 6), Krly.QuerySettings, 0x02, 1, 1, .. body, seq]);
                    break;
                }
                case Krly.QueryMappings:
                {
                    var body = new List<byte> { (byte)(face.Count * 3) };
                    foreach (var (s, d) in face) { body.Add(0); body.Add(s); body.Add(d); }
                    Emit([(byte)(body.Count + 5), Krly.QueryMappings, 1, 1, .. body, seq]);
                    break;
                }
                case Krly.QueryMacros:
                {
                    var blob = new List<byte>();
                    foreach (var (trig, list) in macros)
                    {
                        bool rep = list.Count > 0 && list[0].Repeat;
                        blob.AddRange([trig, (byte)(rep ? 0 : 1), 0, 0, 0, 0, (byte)list.Count]);
                        for (int i = 0; i < list.Count; i++)
                        {
                            var st = list[i];
                            blob.AddRange([(byte)i, (byte)(st.Hold >> 8), (byte)st.Hold, (byte)(st.Wait >> 8), (byte)st.Wait, 1, st.Key]);
                        }
                    }
                    Emit([(byte)(blob.Count + 5), Krly.QueryMacros, 1, 1, .. blob, seq]);
                    break;
                }
                case Krly.BeginUpload:
                    turbo.Clear();
                    face.Clear();
                    Emit([4, cmd, 0x00, seq]);
                    break;
                case Krly.Turbo:
                    turbo.Add((payload[0], payload[1], payload[2]));
                    Emit([4, cmd, 0x00, seq]);
                    break;
                case Krly.Triggers:
                    lt = payload[0..2]; rt = payload[2..4];
                    Emit([4, cmd, 0x00, seq]);
                    break;
                case Krly.Sticks:
                    ls = payload[0..4]; rs = payload[4..8];
                    Emit([4, cmd, 0x00, seq]);
                    break;
                case Krly.Mapping:
                    face[payload[0]] = payload[1];
                    Emit([4, cmd, 0x00, seq]);
                    break;
                case Krly.MacroStep:
                {
                    byte trig = payload[0];
                    if (payload.Length == 7)
                    {
                        macros[trig] = [];
                    }
                    else
                    {
                        bool repeat = payload[1] == 0;
                        int stepIndex = payload[2];
                        int hold = (payload[3] << 8) | payload[4];
                        int wait = (payload[5] << 8) | payload[6];
                        byte key = payload[7];
                        if (stepIndex == 0 || !macros.ContainsKey(trig)) macros[trig] = [];
                        macros[trig].Add((repeat, hold, wait, key));
                    }
                    Emit([4, cmd, 0x00, seq]);
                    break;
                }
                case Krly.Motors:
                    lm = payload[0]; rm = payload[1];
                    Emit([4, cmd, 0x00, seq]);
                    break;
                case Krly.FactoryReset:
                    face.Clear(); turbo.Clear();
                    lt = [3, 97]; rt = [3, 97]; ls = [5, 100, 0, 0]; rs = [5, 100, 0, 0]; lm = 100; rm = 100;
                    macros[0x01] = [(false, 0, 1, 0xA0)]; macros[0x02] = [(false, 0, 1, 0xA1)];
                    Emit([4, cmd, 0x00, seq]);
                    break;
                case Krly.Calibrate:
                    if (payload[0] == 1)
                    {
                        _ = Task.Run(async () =>
                        {
                            Emit([5, Krly.Calibrate, 0x01, 0x00, 0x00]);
                            await Task.Delay(15);
                            Emit([5, Krly.Calibrate, 0x03, 0x00, 0x01]);
                        });
                    }
                    break;
                case Krly.SelfCalStart:
                    Emit([4, cmd, 0x00, seq]);
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(15);
                        Emit([4, Krly.SelfCalResultA, 0x00, 0x00]);
                    });
                    break;
                default:
                    Emit([4, cmd, 0x00, seq]);
                    break;
            }
        }

        private void Emit(byte[] frame)
        {
            for (int at = 0; at < frame.Length; at += 11)
            {
                int len = Math.Min(11, frame.Length - at);
                NotificationReceived?.Invoke(frame[at..(at + len)]);
            }
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
