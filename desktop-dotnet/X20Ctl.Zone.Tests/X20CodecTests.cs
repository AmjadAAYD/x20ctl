using X20Ctl.Product;
using Xunit;

namespace X20Ctl.Zone.Tests;

public class X20CodecTests
{
    [Fact]
    public void BuildsReadVidPidPacketIdenticalToPython()
    {
        byte[] expected = [87, 203, 129, 199, 94];
        byte[] actual = X20Codec.Frame(0x91, 5, 1, 0, []);

        Assert.Equal(expected, actual);

        var (opcode, length, serial, nonce, payload, crcValid) = X20Codec.Decode(actual);
        Assert.Equal(0x91, opcode);
        Assert.Equal(5, length);
        Assert.Equal(1, serial);
        Assert.Equal(0, nonce);
        Assert.Empty(payload);
        Assert.True(crcValid);
    }

    [Fact]
    public void BuildsPowerQueryPacketIdenticalToPython()
    {
        byte[] expected = [212, 199, 128, 199, 56, 201, 56, 230];
        byte[] payload = [0x02, 0xDF, 0xAB];
        byte[] actual = X20Codec.Frame(0x1D, 8, 2, 0, payload);

        Assert.Equal(expected, actual);

        var (opcode, length, serial, nonce, decodedPayload, crcValid) = X20Codec.Decode(actual);
        Assert.Equal(0x1D, opcode);
        Assert.Equal(8, length);
        Assert.Equal(2, serial);
        Assert.Equal(payload, decodedPayload);
        Assert.True(crcValid);
    }

    [Fact]
    public void DecodesHardwareDeviceInfoReply()
    {
        // Real reply packet captured from physical EasySMX X20 Pro over BLE
        byte[] rawReply = [0xCA, 0xF8, 0x8A, 0xB2, 0x2B, 0x30, 0xB9, 0x3C, 0xEC, 0xAD, 0x66, 0x37, 0x9A];
        var (opcode, length, serial, nonce, payload, crcValid) = X20Codec.Decode(rawReply);

        Assert.True(crcValid);
        Assert.Equal(1, serial);
        Assert.Equal(11, nonce);
        Assert.Equal(8, payload.Length);
        Assert.Equal(0x07, payload[0]); // record size
        Assert.Equal(0x10, payload[1]); // VID high
        Assert.Equal(0x20, payload[2]); // VID low -> "1020"
        Assert.Equal(0x20, payload[3]); // PID high
        Assert.Equal(0x06, payload[4]); // PID low -> "2006"
        Assert.Equal(0x01, payload[5]); // FW major -> 1
        Assert.Equal(0x44, payload[6]); // FW minor -> 44
    }

    [Fact]
    public void DecodesHardwarePowerReply()
    {
        // Real reply packet captured from physical EasySMX X20 Pro over BLE
        byte[] rawReply = [0x9D, 0x6D, 0xE8, 0x5C, 0xE7, 0xF1, 0x33, 0x0A, 0x11, 0x04, 0xDD, 0xF2, 0x13, 0xCF, 0x7E];
        var (opcode, length, serial, nonce, payload, crcValid) = X20Codec.Decode(rawReply);

        Assert.True(crcValid);
        Assert.Equal(2, serial);
        Assert.Equal(10, payload.Length);
        byte status = payload[4];
        bool charging = (status >> 4 & 1) != 0;
        int level = (status >> 7 & 1) != 0 ? 4 :
                    (status >> 6 & 1) != 0 ? 3 :
                    (status >> 5 & 1) != 0 ? 2 : 1;

        Assert.True(charging);
        Assert.Equal(4, level);
    }
}
