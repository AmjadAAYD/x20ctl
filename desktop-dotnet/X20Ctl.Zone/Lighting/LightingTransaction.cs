namespace X20Ctl.Zone.Lighting;

/// <summary>The lighting read, two-chunk write and read-back as one transaction over any request/reply link.</summary>
public static class LightingTransaction
{
    public delegate Task<byte[]> Exchange(byte op, byte[] payload, byte serial, byte length);
    static readonly byte[] Ack = [2, 0xdf, 0xaa];

    public static async Task<byte[]> ReadAsync(Exchange exchange, Func<byte> next)
    {
        var first = await exchange(0xb7, [0], next(), 6);
        if (first.Length != 15 || first[0] != 24) throw new IOException("Unexpected lighting record header.");
        var last = await exchange(0xb7, [1], next(), 6);
        if (last.Length != 10) throw new IOException("Incomplete lighting record.");
        return [..first[1..], ..last];
    }

    /// <summary>Returns the record as read back. Nothing is sent when the target equals the stored record.</summary>
    public static async Task<byte[]> RunAsync(Exchange exchange, Func<byte> next, byte[]? colour, byte? mode, byte? brightnessByte, string checkpointFolder, TimeSpan chunkGap)
    {
        var original = await ReadAsync(exchange, next);
        if (colour is null && mode is null && brightnessByte is null) return original;
        if (colour is not null && colour.Length != 3) throw new ArgumentException("Expected RGB bytes.");
        var target = colour is not null && mode.HasValue ? LightingRecord.WithSelection(original, colour[0], colour[1], colour[2], mode.Value)
            : mode.HasValue ? LightingRecord.WithMode(original, mode.Value)
            : colour is not null ? LightingRecord.WithColour(original, colour[0], colour[1], colour[2])
            : original;
        if (brightnessByte.HasValue) target = LightingRecord.WithBrightnessByte(target, brightnessByte.Value);
        if (original.SequenceEqual(target)) return original;
        Directory.CreateDirectory(checkpointFolder);
        var checkpoint = Path.Combine(checkpointFolder, $"{DateTime.UtcNow:yyyyMMddTHHmmssfff}-before.txt");
        await File.WriteAllTextAsync(checkpoint, Convert.ToHexString(original));
        byte[] payload = [24, ..target];
        try
        {
            for (int index = 0; index < 2; index++)
            {
                int counter = index + 1;
                byte[] chunk = payload.Skip(index * 15).Take(15).ToArray();
                byte serial = (byte)(((counter & 1) << 7) | (index << 3) | (counter & 7));
                byte length = (byte)(((counter & 7) << 5) | (chunk.Length + 5));
                var ack = await exchange(0x37, chunk, serial, length);
                if (!ack.SequenceEqual(Ack)) throw new IOException("Unexpected lighting acknowledgement.");
                await Task.Delay(chunkGap);
            }
            var observed = await ReadAsync(exchange, next);
            if (!observed.SequenceEqual(target)) throw new IOException("Lighting read-back differs from the requested record.");
            return observed;
        }
        catch (Exception ex)
        {
            throw new IOException($"Lighting state may be partially changed. No retry was sent. Original record: {checkpoint}. {ex.Message}", ex);
        }
    }
}
