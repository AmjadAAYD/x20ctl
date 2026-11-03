using System.Buffers.Binary;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using X20Ctl.Zone.Screen;

namespace X20Ctl.Product.Development;

/// <summary>Reads what the pad's screen chip reports; sends no data or erase packet.</summary>
internal static class ScreenProbe
{

    public static async Task Run(string directory, string? gif, Action<string> log)
    {
        byte[]? asset = null, metadata = null;
        if (gif != null)
        {
            asset = Encode(gif, out int frames);
            metadata = ScreenCodec.Metadata(asset, true, 30);
            log($"our encoding: {frames} frames, asset {asset.Length} bytes crc {ScreenCodec.Crc(asset):X8}, metadata crc {ScreenCodec.Crc(metadata):X8} (30 ms)");
        }
        log("waiting for the screen port: switch the pad off, hold M, plug it in");
        string? port = null;
        for (var until = DateTime.UtcNow.AddMinutes(10); port == null && DateTime.UtcNow < until; await Task.Delay(50)) port = ScreenSerialTransport.FindScreenPort();
        if (port == null) throw new TimeoutException("No screen port appeared in 10 minutes.");
        log($"found {port}");
        await Task.Delay(1500);
        if (ScreenSerialTransport.FindScreenPort() != port) throw new IOException($"{port} went away while settling.");
        ScreenSerialTransport? transport = null;
        byte[]? identity = null;
        foreach (bool dtr in new[] { false, true })
        {
            transport?.Dispose();
            transport = new ScreenSerialTransport(port, tracePath: Path.Combine(directory, "screen-probe-trace.jsonl"), dtr: dtr);
            try { identity = await transport.ExchangeAsync(ScreenProtocol.FlashQuery(), CancellationToken.None); log($"answered with DTR {(dtr ? "on" : "off")}"); break; }
            catch (TimeoutException) { log($"no answer with DTR {(dtr ? "on" : "off")}"); }
        }
        using var _ = transport;
        if (identity == null) throw new TimeoutException("The screen chip didn't answer either way.");
        log($"flash reply {Convert.ToHexString(identity)}");
        if (identity[0] != 0x7d) throw new InvalidDataException("Unexpected flash identity reply.");
        uint id = BinaryPrimitives.ReadUInt32LittleEndian(identity.AsSpan(1));
        log($"flash id 0x{id:X6}{(id == ScreenProtocol.FlashId ? " (the one the sender expects)" : " (NOT the one the sender expects)")}");
        if (asset == null || metadata == null) return;
        foreach (var (name, address, data) in new[] { ("asset", ScreenProtocol.AssetAddress, asset), ("metadata", ScreenProtocol.MetadataAddress, metadata) })
        {
            var reply = await transport.ExchangeAsync(ScreenProtocol.CrcQuery(address, data), CancellationToken.None);
            uint seen = BinaryPrimitives.ReadUInt32LittleEndian(reply.AsSpan(2));
            log($"{name} at 0x{address:X6}: reply {Convert.ToHexString(reply.AsSpan(0, 8))}, pad crc {seen:X8}, ours {ScreenCodec.Crc(data):X8} -> {(reply[0] == 0x7e && seen == ScreenCodec.Crc(data) ? "MATCH" : "differs")}");
        }
        // The CRC query takes any length, so one more byte per query, tried against all 256 values, reads the flash back.
        foreach (var (name, address, count) in new[] { ("metadata", ScreenProtocol.MetadataAddress, 48), ("asset", ScreenProtocol.AssetAddress, 64) })
        {
            var known = new List<byte>();
            for (int i = 0; i < count; i++)
            {
                var probe = new byte[i + 1];
                var reply = await transport.ExchangeAsync(ScreenProtocol.CrcQuery(address, probe), CancellationToken.None);
                uint seen = BinaryPrimitives.ReadUInt32LittleEndian(reply.AsSpan(2));
                known.Add(0);
                int found = -1;
                for (int value = 0; value < 256 && found < 0; value++) { known[i] = (byte)value; if (ScreenCodec.Crc(known.ToArray()) == seen) found = value; }
                if (found < 0) { log($"{name} byte {i}: no value fits crc {seen:X8}"); known.RemoveAt(i); break; }
            }
            var ours = name == "asset" ? asset : metadata;
            log($"{name} on pad: {Convert.ToHexString(known.ToArray())}");
            log($"{name} ours:   {Convert.ToHexString(ours.AsSpan(0, known.Count))}");
        }
    }

    /// <summary>Real pad: uploads the built-in test pattern as a still through the Screen tab's sender, which reads both
    /// CRCs back itself; then a fresh connection checks them again.</summary>
    public static async Task Upload(string directory, string? gif, Action<string> log)
    {
        int frames = 1, delay = gif == null ? 100 : 30; // 30 ms is what DIYTool used for the same GIF
        var asset = gif == null ? ScreenCodec.EncodeStill(ScreenArtwork.TestImage()) : Encode(gif, out frames);
        bool animation = gif != null;
        log($"{(gif == null ? "test pattern" : $"{frames}-frame animation at {delay} ms")}: {asset.Length} bytes crc {ScreenCodec.Crc(asset):X8}");
        if (ScreenSerialTransport.FindScreenPort() != null) log("unplug the pad first; waiting for it to go");
        while (ScreenSerialTransport.FindScreenPort() != null) await Task.Delay(100); // a fresh plug-in starts the chip at 115200
        log("waiting for the screen port: switch the pad off, hold M, plug it in");
        string? port = null;
        for (var until = DateTime.UtcNow.AddMinutes(10); port == null && DateTime.UtcNow < until; await Task.Delay(50)) port = ScreenSerialTransport.FindScreenPort();
        if (port == null) throw new TimeoutException("No screen port appeared in 10 minutes.");
        log($"found {port}");
        await Task.Delay(3000);
        if (ScreenSerialTransport.FindScreenPort() != port) throw new IOException($"{port} went away while settling.");
        var trace = Path.Combine(directory, "screen-upload-trace.jsonl");
        // The chip often misses the first handshake after plug-in; only the opening is retried, never data.
        ScreenSerialTransport? transport = null;
        for (int attempt = 1; ; attempt++)
        {
            transport?.Dispose();
            transport = new ScreenSerialTransport(port, tracePath: trace, dtr: attempt > 1);
            try { await transport.ExchangeAsync(ScreenProtocol.FlashQuery(), CancellationToken.None); log($"connected on try {attempt}"); break; }
            catch (TimeoutException) when (attempt < 3) { log($"no answer on try {attempt}"); }
        }
        using (transport)
        {
            int last = -1;
            await new ScreenSender(transport).UploadAsync(asset, animation, delay, new Progress<ScreenProgress>(p => { int step = p.Total == 0 ? 0 : p.Sent * 4 / p.Total; if (step != last) { last = step; log($"{p.State} {p.Sent}/{p.Total}"); } }), CancellationToken.None);
        }
        log("upload finished; sender read both CRCs back");
        using var check = new ScreenSerialTransport(port, tracePath: trace);
        log($"fresh connection check: {(await new ScreenSender(check).VerifyAsync(asset, animation, delay, CancellationToken.None) ? "MATCH" : "differs")}");
    }

    static byte[] Encode(string path, out int count)
    {
        using var stream = File.OpenRead(path);
        var decoded = ScreenMedia.Decode(new GifBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad));
        var frames = decoded.Frames.Select(frame =>
        {
            if (frame.PixelWidth != 160 || frame.PixelHeight != 80) throw new ArgumentException("The GIF must already be 160×80.");
            var bytes = new byte[51200];
            new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0).CopyPixels(bytes, 640, 0);
            for (int i = 0; i < bytes.Length; i += 4) (bytes[i], bytes[i + 2]) = (bytes[i + 2], bytes[i]);
            return bytes;
        }).ToList();
        count = frames.Count;
        return ScreenCodec.EncodeAnimation(frames);
    }
}
