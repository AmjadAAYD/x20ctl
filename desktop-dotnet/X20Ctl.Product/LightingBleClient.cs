using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Foundation;
using Windows.Storage.Streams;
using X20Ctl.Zone;
using X20Ctl.Zone.Capture;
using X20Ctl.Zone.Lighting;

namespace X20Ctl.Product;

/// <summary>One explicit operation per connection; no polling or automatic write retries.</summary>
internal static class LightingBleClient
{
    static SemaphoreSlim Gate => NativeBleClient.ConfigurationGate;
    public static async Task<byte[]> RunAsync(byte[]? colour = null, byte? mode = null, byte? brightnessByte = null)
    {
        await using var gate = await NativeBleClient.AcquireConfigGateAsync();
        try { return await ConnectedAsync(colour, mode, brightnessByte); }
        finally { }
    }
    internal static async Task<byte[]> RunWithoutYieldAsync(byte[]? colour = null, byte? mode = null, byte? brightnessByte = null, CancellationToken ct = default)
    {
        await using var gate = await NativeBleClient.AcquireConfigGateAsync(ct, yieldActiveStream: false);
        try { return await ConnectedAsync(colour, mode, brightnessByte); }
        finally { }
    }

    /// <summary>Reads the full 24-byte lighting record, and optionally probes a trailing RGB offset (7..21, or 0 = fill tail 7..23 with 0000FF) before restoring the original record.</summary>
    public static async Task<string> ProbeLightingAsync(int? testOffset = null)
    {
        if (testOffset is { } off && off != 0 && (off < 7 || off > 21))
            throw new ArgumentOutOfRangeException(nameof(testOffset), "Offset must be 0 or between 7 and 21.");

        await using var gate = await NativeBleClient.AcquireConfigGateAsync();
        return await SessionAsync(async (exchange, next) =>
        {
            var sb = new System.Text.StringBuilder();
            var original = await LightingTransaction.ReadAsync((o, p, s, l) => exchange(o, p, s, l), next);
            sb.AppendLine($"Original 24-byte record: {Convert.ToHexString(original)}");
            sb.AppendLine($"  RGB[0..2]       = #{original[0]:X2}{original[1]:X2}{original[2]:X2}");
            sb.AppendLine($"  Byte[3]         = 0x{original[3]:X2}");
            sb.AppendLine($"  Byte[4] (CLR)   = 0x{original[4]:X2}");
            sb.AppendLine($"  Byte[5] (Bright)= 0x{original[5]:X2}");
            sb.AppendLine($"  Byte[6] (Mode)  = 0x{original[6]:X2} ({LightingRecord.ModeName(original[6])})");
            sb.AppendLine($"  Tail[7..23]     = {Convert.ToHexString(original, 7, 17)}");

            if (testOffset is { } offset)
            {
                var target = LightingRecord.WithFrame(original, 0xFF, 0x00, 0x00, 0xFF, LightingRecord.ModeSolid);
                if (offset == 0)
                {
                    for (int i = 7; i + 2 < 24; i += 3)
                    {
                        target[i] = 0x00;
                        target[i + 1] = 0x00;
                        target[i + 2] = 0xFF;
                    }
                    target[22] = 0x00;
                    target[23] = 0xFF;
                    sb.AppendLine();
                    sb.AppendLine("Probing ALL tail bytes [7..23] with Blue (0000FF) while RGB[0..2] = FF0000 (Red)...");
                }
                else
                {
                    target[offset] = 0x00;
                    target[offset + 1] = 0x00;
                    target[offset + 2] = 0xFF;
                    sb.AppendLine();
                    sb.AppendLine($"Probing offset {offset}..{offset + 2} = 0000FF (Blue) while RGB[0..2] = FF0000 (Red)...");
                }
                try
                {
                    await LightingTransaction.WriteRecordAsync((o, p, s, l) => exchange(o, p, s, l), target, TimeSpan.FromMilliseconds(150));
                    var observed = await LightingTransaction.ReadAsync((o, p, s, l) => exchange(o, p, s, l), next);
                    sb.AppendLine($"Read-back record: {Convert.ToHexString(observed)}");
                    sb.AppendLine($"  Tail[7..23]     = {Convert.ToHexString(observed, 7, 17)}");
                    sb.AppendLine(observed.SequenceEqual(target)
                        ? "  Status: Controller ACCEPTED and persisted the trailing bytes! Check LEDs visually."
                        : "  Status: Controller modified or stripped the trailing bytes on read-back.");
                    await Task.Delay(4000);
                }
                finally
                {
                    await LightingTransaction.WriteRecordAsync((o, p, s, l) => exchange(o, p, s, l), original, TimeSpan.FromMilliseconds(150));
                    sb.AppendLine("Restored original 24-byte lighting record.");
                }
            }
            return sb.ToString();
        });
    }

    /// <summary>Reads one stored macro. Asking for the macro limits first makes the record count entries, not bytes.</summary>
    public static async Task<byte[]> ReadMacroAsync(int slot)
    {
        if (slot is < 0 or > 5) throw new ArgumentOutOfRangeException(nameof(slot));
        await using var gate = await NativeBleClient.AcquireConfigGateAsync();
        try
        {
            return await SessionAsync(async (exchange, next) =>
            {
                await Record(exchange, next, 0xb0, [0, 6], first => first[0] + 1);
                return await Record(exchange, next, 0xb5, [0, (byte)slot], first => first[0] == 0 ? 1 : 3 + 5 * first[0]);
            });
        }
        finally { }
    }

    static async Task<byte[]> Record(Exchange exchange, Func<byte> next, byte op, byte[] args, Func<byte[], int> size)
    {
        var first = await exchange(op, args, next(), (byte)(5 + args.Length));
        if (first.Length == 0) throw new IOException("The controller returned an empty record.");
        int expected = size(first);
        var data = new List<byte>(first);
        for (byte index = 1; data.Count < expected; index++)
        {
            if (index >= 16) throw new IOException("The controller's record is too long.");
            data.AddRange(await exchange(op, [index, ..args[1..]], next(), (byte)(5 + args.Length)));
        }
        if (data.Count != expected) throw new IOException("The controller's record has an unexpected length.");
        return [.. data];
    }

    /// <summary>Everything the Studio's Buttons, Curves and Macros tabs edit, as the pad holds it.</summary>
    public static async Task<ControllerSnapshot> ReadConfigAsync()
    {
        await using var gate = await NativeBleClient.AcquireConfigGateAsync();
        try { return await SessionAsync(ReadConfig); }
        finally { }
    }

    /// <summary>Read-only KeyLinker snapshot of all verified config, macro, and lighting blocks + GATT latency.</summary>
    public static async Task<OnboardHardwareSnapshot> ReadDiagnosticSnapshotAsync()
    {
        if (ReviewSandbox.Active) throw new InvalidOperationException("Disabled in review sandbox.");
        if (UniversalHapticsService.IsPowerSavingActive) throw new InvalidOperationException("Turn off Eco mode first.");
        await using var gate = await NativeBleClient.AcquireConfigGateAsync();
        var sw = Stopwatch.StartNew();
        return await SessionAsync(async (exchange, next) =>
        {
            var identity = await exchange(0x91, [], next(), 5);
            var cfg = await ReadConfig(exchange, next);
            var lighting = await LightingTransaction.ReadAsync((o, p, s, l) => exchange(o, p, s, l), next);
            sw.Stop();
            BleLinkWatchdog.RecordGattLatency(sw.Elapsed.TotalMilliseconds);
            return new OnboardHardwareSnapshot(
                DateTimeOffset.UtcNow,
                "x20_pro",
                Convert.ToHexString(identity),
                Convert.ToHexString(cfg.Remap),
                Convert.ToHexString(cfg.Sticks),
                Convert.ToHexString(cfg.Triggers),
                cfg.Macros.Select(Convert.ToHexString).ToArray(),
                Convert.ToHexString(lighting),
                Math.Round(sw.Elapsed.TotalMilliseconds, 1));
        });
    }

    /// <summary>Writes only the records that differ, reads each back, and returns how many changed. No retries: a failure
    /// says where the previous records were saved.</summary>
    public static async Task<int> WriteConfigAsync(Func<ControllerSnapshot, ControllerSnapshot> change)
    {
        await using var gate = await NativeBleClient.AcquireConfigGateAsync();
        try
        {
            return await SessionAsync(async (exchange, next) =>
            {
                // the remap record's order is the pad's own source list
                if (Convert.ToHexString(await Record(exchange, next, 0xb0, [0, 3], first => first[0] + 1)) != "091001880D840B825D82") throw new IOException("This controller lists its buttons differently.");
                var current = await ReadConfig(exchange, next);
                var target = change(current);
                var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "x20ctl", "controller");
                Directory.CreateDirectory(folder);
                var checkpoint = Path.Combine(folder, $"{DateTime.UtcNow:yyyyMMddTHHmmssfff}-before.txt");
                await File.WriteAllLinesAsync(checkpoint, new[] { current.Remap, current.Sticks, current.Triggers }.Concat(current.Macros).Select(Convert.ToHexString));
                int changed = 0;
                try
                {
                    async Task Setting(byte write, byte read, byte[] held, byte[] wanted, bool same)
                    {
                        if (same) return;
                        await Chunks(exchange, write, wanted);
                        var seen = await Record(exchange, next, read, [0], first => first[0] + 1);
                        if (!seen.SequenceEqual(wanted)) throw new IOException("The controller read back something different.");
                        changed++;
                    }
                    await Setting(0x36, 0xb6, current.Remap, target.Remap, ControllerRecords.SameRemap(current.Remap, target.Remap));
                    await Setting(0x31, 0xb1, current.Sticks, target.Sticks, current.Sticks.SequenceEqual(target.Sticks));
                    await Setting(0x32, 0xb2, current.Triggers, target.Triggers, current.Triggers.SequenceEqual(target.Triggers));
                    for (int slot = 0; slot < target.Macros.Length; slot++)
                    {
                        if (current.Macros[slot].SequenceEqual(target.Macros[slot])) continue;
                        var record = target.Macros[slot];
                        for (int i = 0; i * 15 < record.Length; i++)
                        {
                            var chunk = record.Skip(i * 15).Take(15).ToArray();
                            Ack(await exchange(0x35, chunk, (byte)((i << 3) | ((i % 2) << 7)), (byte)((slot << 5) | (chunk.Length + 5))));
                            await Task.Delay(150);
                        }
                        if (!(await Macro(exchange, next, slot)).SequenceEqual(record)) throw new IOException($"M{slot + 1} read back different.");
                        changed++;
                    }
                }
                catch (Exception error) when (error is not OperationCanceledException)
                {
                    throw new IOException($"The controller may be partly changed. Nothing was retried. Previous settings: {checkpoint}. {error.Message}", error);
                }
                return changed;
            });
        }
        finally { }
    }

    /// <summary>Saves what the pad holds, sends the reset once, then reconnects and reads what it holds now. The pad may
    /// not answer the command, so only the read says what happened. Returns how many records changed.</summary>
    public static async Task<int> FactoryResetAsync()
    {
        await using var gate = await NativeBleClient.AcquireConfigGateAsync();
        try
        {
            var before = await SessionAsync(async (exchange, next) =>
            {
                var identity = await exchange(0x91, [], next(), 5);
                var payload = ControllerRecords.FactoryResetPayload(identity[^1]) ?? throw new IOException("This controller reports an unknown protocol generation.");
                var current = await ReadConfig(exchange, next);
                var motors = await Record(exchange, next, 0xb3, [0], first => first[0] + 1);
                var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "x20ctl", "controller");
                Directory.CreateDirectory(folder);
                await File.WriteAllLinesAsync(Path.Combine(folder, $"{DateTime.UtcNow:yyyyMMddTHHmmssfff}-before-reset.txt"), new[] { current.Remap, current.Sticks, current.Triggers }.Concat(current.Macros).Append(motors).Select(Convert.ToHexString));
                try { await exchange(0x16, payload, 1, (byte)(payload.Length + 5)); }
                catch (TimeoutException) { }
                return current;
            });
            await Task.Delay(1500);
            return ControllerRecords.Changes(before, await SessionAsync(ReadConfig));
        }
        finally { }
    }

    static async Task<ControllerSnapshot> ReadConfig(Exchange exchange, Func<byte> next)
    {
        await Record(exchange, next, 0xb0, [0, 6], first => first[0] + 1);
        var remap = await Record(exchange, next, 0xb6, [0], first => first[0] + 1);
        var sticks = await Record(exchange, next, 0xb1, [0], first => first[0] + 1);
        var triggers = await Record(exchange, next, 0xb2, [0], first => first[0] + 1);
        var macros = new byte[6][];
        for (int slot = 0; slot < 6; slot++) macros[slot] = await Macro(exchange, next, slot);
        return new(remap, sticks, triggers, macros);
    }
    static Task<byte[]> Macro(Exchange exchange, Func<byte> next, int slot) => Record(exchange, next, 0xb5, [0, (byte)slot], first => first[0] == 0 ? 1 : 3 + 5 * first[0]);

    static async Task Chunks(Exchange exchange, byte op, byte[] record, TimeSpan? gap = null)
    {
        var delay = gap ?? TimeSpan.FromMilliseconds(150);
        for (int index = 0; index * 15 < record.Length; index++)
        {
            int counter = index + 1;
            var chunk = record.Skip(index * 15).Take(15).ToArray();
            Ack(await exchange(op, chunk, (byte)(((counter & 1) << 7) | (index << 3) | (counter & 7)), (byte)(((counter & 7) << 5) | (chunk.Length + 5))));
            if (delay > TimeSpan.Zero) await Task.Delay(delay);
        }
    }
    static void Ack(byte[] reply) { if (!reply.SequenceEqual(new byte[] { 2, 0xdf, 0xaa })) throw new IOException("Unexpected acknowledgement."); }

    internal delegate Task<byte[]> Exchange(byte op, byte[] payload, byte serial, byte length);

    static Task<byte[]> ConnectedAsync(byte[]? colour, byte? mode, byte? brightnessByte) => SessionAsync((exchange, next) =>
        LightingTransaction.RunAsync((o, p, s, l) => exchange(o, p, s, l), next, colour, mode, brightnessByte,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "x20ctl", "lighting"), TimeSpan.FromMilliseconds(20)));

    /// <summary>Persistent GATT session on the X20 Pro lighting channel for low-latency effect streaming (chunkGap = 0ms).</summary>
    internal sealed class LightingStreamSession : IAsyncDisposable
    {
        readonly BluetoothLEDevice device;
        readonly GattDeviceService service;
        readonly GattCharacteristic notify;
        readonly TypedEventHandler<GattCharacteristic, GattValueChangedEventArgs> handler;
        readonly Exchange exchange;
        readonly byte[] baseline;
        readonly bool ownsGate;
        byte[] lastSent;
        int disposed;

        internal LightingStreamSession(
            BluetoothLEDevice device,
            GattDeviceService service,
            GattCharacteristic notify,
            TypedEventHandler<GattCharacteristic, GattValueChangedEventArgs> handler,
            Exchange exchange,
            byte[] baseline,
            bool ownsGate)
        {
            this.device = device;
            this.service = service;
            this.notify = notify;
            this.handler = handler;
            this.exchange = exchange;
            this.baseline = (byte[])baseline.Clone();
            this.lastSent = (byte[])baseline.Clone();
            this.ownsGate = ownsGate;
        }

        public byte[] Baseline => (byte[])baseline.Clone();
        public int WriteCount { get; private set; }

        public async Task<bool> SendFrameAsync(LightingFrame frame, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            byte[] target = LightingRecord.WithFrame(baseline, frame.R, frame.G, frame.B, frame.Brightness, frame.NativeMode);
            if (target.SequenceEqual(lastSent)) return false;
            await LightingTransaction.WriteRecordAsync((o, p, s, l) => exchange(o, p, s, l), target, TimeSpan.Zero);
            lastSent = target;
            WriteCount++;
            return true;
        }

        public async Task TryRestoreBaselineAsync()
        {
            try
            {
                if (!lastSent.SequenceEqual(baseline))
                {
                    await LightingTransaction.WriteRecordAsync((o, p, s, l) => exchange(o, p, s, l), baseline, TimeSpan.Zero);
                    lastSent = (byte[])baseline.Clone();
                    WriteCount++;
                }
            }
            catch { }
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            notify.ValueChanged -= handler;
            try { await notify.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.None); } catch { }
            try { service.Dispose(); } catch { }
            try { device.Dispose(); } catch { }
            if (ownsGate) Gate.Release();
        }
    }

    internal static async Task<LightingStreamSession> OpenStreamAsync(CancellationToken ct = default)
    {
        if (ReviewSandbox.Active) throw new InvalidOperationException("a review never touches the real controller");
        await Gate.WaitAsync(ct);
        try
        {
            var conn = await ConnectAsync(ct);
            try
            {
                var baseline = await LightingTransaction.ReadAsync((o, p, s, l) => conn.Send(o, p, s, l), conn.Next);
                return new LightingStreamSession(conn.Device, conn.Service, conn.Notify, conn.Handler, conn.Send, baseline, ownsGate: true);
            }
            catch
            {
                await conn.DisposeAsync();
                throw;
            }
        }
        catch
        {
            Gate.Release();
            throw;
        }
    }

    sealed class BleConnection : IAsyncDisposable
    {
        public required BluetoothLEDevice Device;
        public required GattDeviceService Service;
        public required GattCharacteristic Notify;
        public required TypedEventHandler<GattCharacteristic, GattValueChangedEventArgs> Handler;
        public required Exchange Send;
        public required Func<byte> Next;
        public async ValueTask DisposeAsync()
        {
            Notify.ValueChanged -= Handler;
            try { await Notify.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.None); }
            catch { /* Preserve the operation's result if disconnecting fails. */ }
            Service.Dispose();
            Device.Dispose();
        }
    }

    static async Task<BleConnection> ConnectAsync(CancellationToken ct = default)
    {
        // The last address that answered is tried first, so an Apply doesn't wait for a scan; a miss falls back to scanning.
        if (NativeBleClient.CachedAddress() is ulong cached)
        {
            try
            {
                return await ConnectAddressAsync(cached, ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or IOException or TimeoutException or InvalidOperationException)
            {
                NativeBleClient.ForgetCachedAddress();
            }
        }
        var (address, error) = await NativeBleClient.FindDeviceAddressAsync("x20_pro", 8, requireAdvertisedService: false, ct);
        if (address is null) throw new IOException(error);
        return await ConnectAddressAsync(address.Value, ct);
    }

    static async Task<BleConnection> ConnectAddressAsync(ulong address, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var device = await BluetoothLEDevice.FromBluetoothAddressAsync(address).AsTask(ct) ?? throw new IOException("Cannot connect to the controller.");
        GattDeviceServicesResult services;
        try
        {
            services = await device.GetGattServicesForUuidAsync(Guid.Parse("d7f010e0-660d-46e9-96c3-19c4148bdab5"), BluetoothCacheMode.Uncached).AsTask(ct);
        }
        catch
        {
            device.Dispose();
            throw;
        }
        if (services.Status != GattCommunicationStatus.Success || services.Services.Count != 1)
        {
            device.Dispose();
            throw new IOException("Expected one KeyLinker configuration service.");
        }
        NativeBleClient.RememberAddress(address);
        var service = services.Services[0];
        var chars = await service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached).AsTask(ct);
        if (chars.Status != GattCommunicationStatus.Success)
        {
            service.Dispose();
            device.Dispose();
            throw new IOException("Cannot discover lighting connection.");
        }
        var write = chars.Characteristics.Single(c => c.Uuid == Guid.Parse("d7f010e1-660d-46e9-96c3-19c4148bdab5"));
        var notify = chars.Characteristics.Single(c => c.Uuid == Guid.Parse("d7f010e2-660d-46e9-96c3-19c4148bdab5"));
        TaskCompletionSource<byte[]>? pending = null;
        byte expected = 0;
        TypedEventHandler<GattCharacteristic, GattValueChangedEventArgs> handler = (_, e) =>
        {
            using var reader = DataReader.FromBuffer(e.CharacteristicValue);
            var raw = new byte[e.CharacteristicValue.Length]; reader.ReadBytes(raw);
            if (raw.Length is < 5 or > 20) return;
            var f = X20Codec.Decode(raw);
            if (f.CrcValid && (f.Length & 31) == raw.Length && f.Opcode == 0x19 && f.Serial == expected)
                pending?.TrySetResult(f.Payload);
        };
        notify.ValueChanged += handler;
        async Task<byte[]> Send(byte op, byte[] payload, byte serial, byte length)
        {
            ct.ThrowIfCancellationRequested();
            expected = serial;
            pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
            using var writer = new DataWriter();
            writer.WriteBytes(X20Codec.Frame(op, length, serial, (byte)RandomNumberGenerator.GetInt32(256), payload));
            var gattSw = Stopwatch.StartNew();
            var result = await write.WriteValueWithResultAsync(writer.DetachBuffer(), GattWriteOption.WriteWithResponse).AsTask(ct);
            if (result.Status != GattCommunicationStatus.Success) throw new IOException("Bluetooth request failed; no automatic retry.");
            try
            {
                var reply = await pending.Task.WaitAsync(TimeSpan.FromSeconds(4), ct);
                BleLinkWatchdog.RecordGattLatency(gattSw.Elapsed.TotalMilliseconds);
                return reply;
            }
            finally { pending = null; }
        }
        byte serialCounter = 160;
        var conn = new BleConnection
        {
            Device = device,
            Service = service,
            Notify = notify,
            Handler = handler,
            Send = Send,
            Next = () => ++serialCounter,
        };
        try
        {
            if (await notify.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify).AsTask(ct)
                != GattCommunicationStatus.Success) throw new IOException("Cannot subscribe to controller replies.");
            var identity = await Send(0x91, [], ++serialCounter, 5);
            if (identity.Length != 8 || identity[0] != 7 || !identity.AsSpan(1, 6).SequenceEqual(new byte[] { 0x10, 0x20, 0x20, 0x06, 0x01, 0x44 }))
                throw new IOException("Verified only for X20 Pro firmware 1.44.");
            return conn;
        }
        catch
        {
            await conn.DisposeAsync();
            throw;
        }
    }

    /// <summary>One connection: finds the pad, checks it is an X20 Pro on firmware 1.44, runs the work, then lets go.</summary>
    static async Task<T> SessionAsync<T>(Func<Exchange, Func<byte>, Task<T>> work)
    {
        await using var conn = await ConnectAsync();
        return await work(conn.Send, conn.Next);
    }
}
