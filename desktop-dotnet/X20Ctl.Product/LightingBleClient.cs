using System.IO;
using System.Security.Cryptography;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Foundation;
using Windows.Storage.Streams;
using X20Ctl.Zone;
using X20Ctl.Zone.Lighting;

namespace X20Ctl.Product;

/// <summary>One explicit operation per connection; no polling or automatic write retries.</summary>
internal static class LightingBleClient
{
    static SemaphoreSlim Gate => NativeBleClient.ConfigurationGate;
    public static async Task<byte[]> RunAsync(byte[]? colour = null, byte? mode = null, byte? brightnessByte = null)
    {
        await Gate.WaitAsync();
        try { return await ConnectedAsync(colour, mode, brightnessByte); }
        finally { Gate.Release(); }
    }

    /// <summary>Reads one stored macro. Asking for the macro limits first makes the record count entries, not bytes.</summary>
    public static async Task<byte[]> ReadMacroAsync(int slot)
    {
        if (slot is < 0 or > 5) throw new ArgumentOutOfRangeException(nameof(slot));
        await Gate.WaitAsync();
        try
        {
            return await SessionAsync(async (exchange, next) =>
            {
                await Record(exchange, next, 0xb0, [0, 6], first => first[0] + 1);
                return await Record(exchange, next, 0xb5, [0, (byte)slot], first => first[0] == 0 ? 1 : 3 + 5 * first[0]);
            });
        }
        finally { Gate.Release(); }
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
        await Gate.WaitAsync();
        try { return await SessionAsync(ReadConfig); }
        finally { Gate.Release(); }
    }

    /// <summary>Writes only the records that differ, reads each back, and returns how many changed. No retries: a failure
    /// says where the previous records were saved.</summary>
    public static async Task<int> WriteConfigAsync(Func<ControllerSnapshot, ControllerSnapshot> change)
    {
        await Gate.WaitAsync();
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
        finally { Gate.Release(); }
    }

    /// <summary>Saves what the pad holds, sends the reset once, then reconnects and reads what it holds now. The pad may
    /// not answer the command, so only the read says what happened. Returns how many records changed.</summary>
    public static async Task<int> FactoryResetAsync()
    {
        await Gate.WaitAsync();
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
        finally { Gate.Release(); }
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

    static async Task Chunks(Exchange exchange, byte op, byte[] record)
    {
        for (int index = 0; index * 15 < record.Length; index++)
        {
            int counter = index + 1;
            var chunk = record.Skip(index * 15).Take(15).ToArray();
            Ack(await exchange(op, chunk, (byte)(((counter & 1) << 7) | (index << 3) | (counter & 7)), (byte)(((counter & 7) << 5) | (chunk.Length + 5))));
            await Task.Delay(150);
        }
    }
    static void Ack(byte[] reply) { if (!reply.SequenceEqual(new byte[] { 2, 0xdf, 0xaa })) throw new IOException("Unexpected acknowledgement."); }

    delegate Task<byte[]> Exchange(byte op, byte[] payload, byte serial, byte length);

    static Task<byte[]> ConnectedAsync(byte[]? colour, byte? mode, byte? brightnessByte) => SessionAsync((exchange, next) =>
        LightingTransaction.RunAsync((o, p, s, l) => exchange(o, p, s, l), next, colour, mode, brightnessByte,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "x20ctl", "lighting"), TimeSpan.FromMilliseconds(150)));

    /// <summary>One connection: finds the pad, checks it is an X20 Pro on firmware 1.44, runs the work, then lets go.</summary>
    static async Task<T> SessionAsync<T>(Func<Exchange, Func<byte>, Task<T>> work)
    {
        var (address, error) = await NativeBleClient.FindDeviceAddressAsync("x20_pro", 8, requireAdvertisedService: false);
        if (address is null) throw new IOException(error);
        using var device = await BluetoothLEDevice.FromBluetoothAddressAsync(address.Value)
            ?? throw new IOException("Cannot connect to the controller.");
        var services = await device.GetGattServicesForUuidAsync(Guid.Parse("d7f010e0-660d-46e9-96c3-19c4148bdab5"), BluetoothCacheMode.Uncached);
        if (services.Status != GattCommunicationStatus.Success || services.Services.Count != 1)
            throw new IOException("Expected one KeyLinker configuration service.");
        using var service = services.Services[0];
        var chars = await service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
        if (chars.Status != GattCommunicationStatus.Success) throw new IOException("Cannot discover lighting connection.");
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
            expected = serial;
            pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
            using var writer = new DataWriter();
            writer.WriteBytes(X20Codec.Frame(op, length, serial, (byte)RandomNumberGenerator.GetInt32(256), payload));
            var result = await write.WriteValueWithResultAsync(writer.DetachBuffer(), GattWriteOption.WriteWithResponse);
            if (result.Status != GattCommunicationStatus.Success) throw new IOException("Bluetooth request failed; no automatic retry.");
            try { return await pending.Task.WaitAsync(TimeSpan.FromSeconds(4)); }
            finally { pending = null; }
        }
        byte serialCounter = 160;
        try
        {
            if (await notify.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify)
                != GattCommunicationStatus.Success) throw new IOException("Cannot subscribe to controller replies.");
            var identity = await Send(0x91, [], ++serialCounter, 5);
            if (identity.Length != 8 || identity[0] != 7 || !identity.AsSpan(1, 6).SequenceEqual(new byte[]{0x10,0x20,0x20,0x06,0x01,0x44}))
                throw new IOException("Verified only for X20 Pro firmware 1.44.");
            return await work(Send, () => ++serialCounter);
        }
        finally
        {
            notify.ValueChanged -= handler;
            try { await notify.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.None); }
            catch { /* Preserve the operation's result if disconnecting fails. */ }
        }
    }
}
