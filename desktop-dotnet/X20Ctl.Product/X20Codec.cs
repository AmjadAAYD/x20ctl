namespace X20Ctl.Product;

public static class X20Codec
{
    private static readonly byte[] DataKey = [51, 99, 157, 121, 242, 219, 162, 26, 170, 33, 139, 232, 116, 211, 88];
    private static readonly byte[] SerialKey = [
        161, 82, 213, 163, 245, 137, 246, 143, 240, 157, 72, 147, 234, 52, 49, 186, 195, 77, 198, 235,
        73, 96, 216, 163, 218, 42, 83, 141, 244, 97, 24, 191, 174, 215, 111, 81, 228, 160, 217, 146
    ];

    public static byte Crc8(ReadOnlySpan<byte> bytes)
    {
        byte crc = 0;
        foreach (var value in bytes)
        {
            crc ^= value;
            for (int bit = 0; bit < 8; ++bit)
                crc = (byte)((crc >> 1) ^ ((crc & 1) != 0 ? 0xeb : 0));
        }
        return crc;
    }

    public static byte[] Scramble(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 5 || bytes.Length > 20)
            throw new ArgumentOutOfRangeException(nameof(bytes), "packet must contain 5-20 bytes");

        byte[] output = bytes.ToArray();
        int b2 = bytes[2], b3 = bytes[3];
        output[0] = (byte)(bytes[0] ^ ((b2 + b3) - 154));
        output[1] = (byte)(bytes[1] ^ ((b2 + b3) + 155));
        output[2] = (byte)((b2 - 173) ^ b3);
        output[3] = (byte)((b3 + 191) ^ 219);
        for (int i = 4; i + 1 < bytes.Length; ++i)
            output[i] = (byte)(bytes[i] ^ ((b2 + b3) - DataKey[i - 4]));

        int offset = (output[3] & 2) * 10;
        for (int i = 0; i < output.Length; ++i)
            output[i] ^= SerialKey[offset + i];

        return output;
    }

    public static byte[] Unscramble(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 5 || bytes.Length > 20)
            throw new ArgumentOutOfRangeException(nameof(bytes), "packet must contain 5-20 bytes");

        byte[] output = bytes.ToArray();
        int offset = ((bytes[3] ^ SerialKey[3]) & 2) != 0 ? 20 : 0;
        for (int i = 0; i < output.Length; ++i)
            output[i] ^= SerialKey[offset + i];

        int b3 = (byte)((output[3] ^ 219) - 191);
        output[3] = (byte)b3;
        int b2 = (byte)((output[2] ^ b3) + 173);
        output[2] = (byte)b2;
        output[1] = (byte)(output[1] ^ ((b2 + b3) + 155));
        output[0] = (byte)(output[0] ^ ((b2 + b3) - 154));
        for (int i = 4; i + 1 < output.Length; ++i)
            output[i] = (byte)(output[i] ^ ((b2 + b3) - DataKey[i - 4]));

        return output;
    }

    public static byte[] Frame(byte opcode, byte length, byte serial, byte nonce, ReadOnlySpan<byte> payload)
    {
        byte[] data = new byte[payload.Length + 5];
        data[0] = opcode;
        data[1] = length;
        data[2] = serial;
        data[3] = nonce;
        payload.CopyTo(data.AsSpan(4));
        data[^1] = Crc8(data.AsSpan(0, data.Length - 1));
        return Scramble(data);
    }

    public static (byte Opcode, byte Length, byte Serial, byte Nonce, byte[] Payload, bool CrcValid) Decode(ReadOnlySpan<byte> bytes)
    {
        byte[] data = Unscramble(bytes);
        bool crcValid = Crc8(data.AsSpan(0, data.Length - 1)) == data[^1];
        byte[] payload = data.Length > 5 ? data[4..^1] : [];
        return (data[0], data[1], data[2], data[3], payload, crcValid);
    }
}
