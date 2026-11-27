using System.Buffers.Binary;
using X20Ctl.Zone.PcMode;

namespace X20Ctl.Zone.D10;

public enum D10LinkKind
{
    Disconnected,
    WiredUsb,
    WirelessReceiver
}

public readonly record struct D10InputFrame(
    ushort Buttons,
    byte LeftTrigger,
    byte RightTrigger,
    short LeftX,
    short LeftY,
    short RightX,
    short RightY)
{
    public PadState ToPadState() =>
        new(Buttons, LeftTrigger, RightTrigger, LeftX, LeftY, RightX, RightY);
}

public readonly record struct D10ExtraButtons(
    bool M,
    bool M1,
    bool M2,
    bool DpadUp,
    bool DpadDown,
    bool DpadLeft,
    bool DpadRight)
{
    public static readonly D10ExtraButtons None = default;
}

public readonly record struct D10ImuSample(
    short Ax,
    short Ay,
    short Az,
    short Gx,
    short Gy,
    short Gz,
    D10ExtraButtons Extra);

/// <summary>EasySMX D10 Direct USB / 2.4 GHz WinUSB wire reports and EP02 rumble builder.</summary>
public static class D10Wire
{
    public const int UsbVendorId = 0x045E;
    public const int UsbProductId = 0x028E;
    public const int ReceiverVendorId = 0x2345;
    public const int ReceiverProductId = 0xE062;

    public static readonly Guid WinUsbInterfaceGuid = Guid.Parse("B4A83950-1347-42CE-847E-5543B3718BC8");

    public const byte Ep81PipeIn = 0x81;
    public const byte Ep02PipeOut = 0x02;
    public const byte Ep83PipeIn = 0x83;
    public const byte VendorReqMotion = 0x5A;
    public const byte VendorReqSpecial = 0x5C;

    public const int Ep81ReportLength = 20;
    public const int Ep83ReportLength = 64;
    public const int WirelessMotionLength = 12;
    public const int WirelessSpecialLength = 64;
    public const int Ep02RumbleLength = 8;

    private static ReadOnlySpan<byte> Ep83Header => [0x04, 0x1B, 0x74, 0x06];

    public static bool TryParseInputReport(ReadOnlySpan<byte> report, out D10InputFrame frame)
    {
        frame = default;
        if (report.Length < Ep81ReportLength || report[0] != 0x00 || report[1] != 0x14)
            return false;

        frame = new D10InputFrame(
            BinaryPrimitives.ReadUInt16LittleEndian(report.Slice(2, 2)),
            report[4],
            report[5],
            BinaryPrimitives.ReadInt16LittleEndian(report.Slice(6, 2)),
            BinaryPrimitives.ReadInt16LittleEndian(report.Slice(8, 2)),
            BinaryPrimitives.ReadInt16LittleEndian(report.Slice(10, 2)),
            BinaryPrimitives.ReadInt16LittleEndian(report.Slice(12, 2)));
        return true;
    }

    public static bool TryParseWiredMotionReport(ReadOnlySpan<byte> report, out D10ImuSample sample)
    {
        sample = default;
        if (report.Length != Ep83ReportLength || !report[..4].SequenceEqual(Ep83Header))
            return false;

        var extra = new D10ExtraButtons(
            M: (report[7] & 0x80) != 0 && (report[8] & 0x04) != 0,
            M1: (report[5] & 0x01) != 0,
            M2: (report[5] & 0x02) != 0,
            DpadUp: (report[6] & 0x40) != 0,
            DpadDown: (report[6] & 0x80) != 0,
            DpadLeft: (report[7] & 0x01) != 0,
            DpadRight: (report[7] & 0x02) != 0);

        sample = new D10ImuSample(
            BinaryPrimitives.ReadInt16LittleEndian(report.Slice(15, 2)),
            BinaryPrimitives.ReadInt16LittleEndian(report.Slice(17, 2)),
            BinaryPrimitives.ReadInt16LittleEndian(report.Slice(19, 2)),
            BinaryPrimitives.ReadInt16LittleEndian(report.Slice(21, 2)),
            BinaryPrimitives.ReadInt16LittleEndian(report.Slice(23, 2)),
            BinaryPrimitives.ReadInt16LittleEndian(report.Slice(25, 2)),
            extra);
        return true;
    }

    public static bool TryParseWirelessMotionReport(
        ReadOnlySpan<byte> report,
        D10ExtraButtons extra,
        out D10ImuSample sample)
    {
        sample = default;
        if (report.Length != WirelessMotionLength)
            return false;

        // Wireless C0/5A delivers Gyro X/Y/Z first, followed by Accel X/Y/Z.
        short gx = BinaryPrimitives.ReadInt16LittleEndian(report.Slice(0, 2));
        short gy = BinaryPrimitives.ReadInt16LittleEndian(report.Slice(2, 2));
        short gz = BinaryPrimitives.ReadInt16LittleEndian(report.Slice(4, 2));
        short ax = BinaryPrimitives.ReadInt16LittleEndian(report.Slice(6, 2));
        short ay = BinaryPrimitives.ReadInt16LittleEndian(report.Slice(8, 2));
        short az = BinaryPrimitives.ReadInt16LittleEndian(report.Slice(10, 2));

        sample = new D10ImuSample(ax, ay, az, gx, gy, gz, extra);
        return true;
    }

    public static bool TryParseWirelessSpecialReport(
        ReadOnlySpan<byte> report,
        D10ExtraButtons previous,
        out D10ExtraButtons extra,
        out byte rollingCounter)
    {
        extra = previous;
        rollingCounter = 0;
        if (report.Length != WirelessSpecialLength)
            return false;

        extra = previous with
        {
            M = (report[21] & 0x80) != 0 && (report[22] & 0x04) != 0,
            M1 = (report[19] & 0x01) != 0,
            M2 = (report[19] & 0x02) != 0,
        };
        rollingCounter = report[62];
        return true;
    }

    public static byte[] BuildRumblePacket(byte leftMotor, byte rightMotor) =>
        [0x00, 0x08, 0x00, leftMotor, rightMotor, 0x00, 0x00, 0x00];
}
