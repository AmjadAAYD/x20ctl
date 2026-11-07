using X20Ctl.Zone.Telemetry;
using System.IO;
using System.Text;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Foundation;
using Windows.Storage.Streams;

namespace X20Ctl.Product;

/// <summary>
/// Native Windows Runtime (WinRT) Bluetooth Low Energy telemetry client for KeyLinker controllers.
/// Pure C# WinRT calling Windows.Devices.Bluetooth directly (no Python, no external libraries).
/// Reads device info and battery status. The one write is VibrationTestAsync, which the owner starts by hand on a proven pad.
/// </summary>
public static class NativeBleClient
{
    internal static readonly SemaphoreSlim ConfigurationGate=new(1,1);
    private static readonly Guid ServiceUuid = Guid.Parse("d7f010e0-660d-46e9-96c3-19c4148bdab5");
    private static readonly Guid WriteUuid   = Guid.Parse("d7f010e1-660d-46e9-96c3-19c4148bdab5");
    private static readonly Guid NotifyUuid  = Guid.Parse("d7f010e2-660d-46e9-96c3-19c4148bdab5");

    private static string CachePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "x20ctl", "native", "bt_cache.txt");

    public static async Task<PadBridge.Result> QueryAsync(string targetModel = "x20_pro", int timeoutSeconds = 6, bool check = false)
    {
        await ConfigurationGate.WaitAsync();
        try
        {
            var (address, error) = await FindDeviceAddressAsync(targetModel, timeoutSeconds, requireAdvertisedService: false);
            if (address == null)
            {
                return new(true, false, error ?? "Controller not found over Bluetooth. Make sure Bluetooth is enabled and the controller is awake.");
            }

            string addressStr = FormatMac(address.Value);
            return await QueryGattAsync(address.Value, addressStr, targetModel, check);
        }
        catch (Exception ex)
        {
            return new(true, false, $"Bluetooth query failed: {ex.Message}");
        }
        finally { ConfigurationGate.Release(); }
    }

    /// <summary>The address of the pad last reached here, so a repeat operation can connect at once instead of scanning first.</summary>
    internal static ulong? CachedAddress()
    {
        try { return File.Exists(CachePath) && ulong.TryParse(File.ReadAllText(CachePath).Trim(), System.Globalization.NumberStyles.HexNumber, null, out ulong a) && a != 0 ? a : null; }
        catch (IOException) { return null; }
    }
    internal static void ForgetCachedAddress() => ClearCache();
    internal static void RememberAddress(ulong address)
    {
        try { Directory.CreateDirectory(Path.GetDirectoryName(CachePath)!); File.WriteAllText(CachePath, address.ToString("X")); } catch (IOException) { }
    }

    internal static async Task<(ulong? Address, string? Error)> FindDeviceAddressAsync(string targetModel, int timeoutSeconds, bool requireAdvertisedService = true)
    {
        ulong? cachedAddress = null;
        if (File.Exists(CachePath))
        {
            try
            {
                string text = File.ReadAllText(CachePath).Trim();
                if (ulong.TryParse(text, System.Globalization.NumberStyles.HexNumber, null, out ulong cached) && cached != 0)
                {
                    cachedAddress = cached;
                }
            }
            catch { }
        }

        var tcs = new TaskCompletionSource<ulong>();
        string? scanError = null;

        var watcher = new BluetoothLEAdvertisementWatcher
        {
            ScanningMode = BluetoothLEScanningMode.Active
        };

        var seenServices = new HashSet<ulong>();
        var seenNames = new Dictionary<ulong, string>();

        // Narrow filter: match specifically expected models and require the KeyLinker configuration service
        watcher.Received += (w, args) =>
        {
            ulong addr = args.BluetoothAddress;
            string rawName = (args.Advertisement.LocalName ?? "").Trim();
            if (!string.IsNullOrEmpty(rawName))
            {
                seenNames[addr] = rawName;
            }
            if (args.Advertisement.ServiceUuids.Contains(ServiceUuid))
            {
                seenServices.Add(addr);
            }

            // Connecting requires the KeyLinker service every time, not the name alone
            if (requireAdvertisedService && !seenServices.Contains(addr))
            {
                return;
            }

            string name = seenNames.TryGetValue(addr, out var n) ? n : rawName;
            bool nameMatch = BleDiscovery.MatchesTarget(targetModel, name);

            if (nameMatch || (string.IsNullOrEmpty(name) && cachedAddress == addr))
            {
                try { w.Stop(); } catch { }
                tcs.TrySetResult(addr);
            }
        };

        watcher.Stopped += (w, args) =>
        {
            if (args.Error == BluetoothError.RadioNotAvailable)
            {
                scanError = "Bluetooth is turned off in Windows Settings or no Bluetooth adapter is present.";
            }
            else if (args.Error != BluetoothError.Success)
            {
                scanError = $"Bluetooth scan failed ({args.Error}).";
            }
        };

        try
        {
            watcher.Start();
        }
        catch (Exception ex)
        {
            return (null, $"Could not start Bluetooth scan: {ex.Message}");
        }

        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds));
        var completed = await Task.WhenAny(tcs.Task, timeoutTask);
        try { watcher.Stop(); } catch { }

        if (completed == tcs.Task)
        {
            return (await tcs.Task, null);
        }

        if (scanError != null)
        {
            return (null, scanError);
        }

        return (null, "Controller not found over Bluetooth. Make sure Bluetooth is enabled and the controller is awake. If it still isn't found, switch the controller to Bluetooth mode and try again: some pads only answer settings there.");
    }

    /// <summary>The Part B write test: vibration only, on a proven pad, put back and read back afterwards (see VibrationTest).</summary>
    public static async Task<PadBridge.Result> VibrationTestAsync(string targetModel, int percent, int timeoutSeconds = 6)
    {
        try
        {
            var (address, error) = await FindDeviceAddressAsync(targetModel, timeoutSeconds);
            if (address == null) return new(true, false, error ?? "Controller not found over Bluetooth. Make sure Bluetooth is enabled and the controller is awake.");
            return await QueryGattAsync(address.Value, FormatMac(address.Value), targetModel, false, percent);
        }
        catch (Exception ex)
        {
            return new(true, false, $"Bluetooth write test failed: {ex.Message}");
        }
    }

    private sealed class Link(Func<byte[], byte, Task<byte[]?>> send) : IPadLink
    {
        public Task<byte[]?> SendAsync(byte[] packet, byte serial) => send(packet, serial);
    }

    private static async Task<PadBridge.Result> QueryGattAsync(ulong bluetoothAddress, string addressStr, string targetModel, bool check, int? vibrate = null)
    {
        using var device = await BluetoothLEDevice.FromBluetoothAddressAsync(bluetoothAddress);
        if (device == null)
        {
            ClearCache();
            return new(true, false, $"Could not connect to Bluetooth device {addressStr}.");
        }

        var gatt = await device.GetGattServicesForUuidAsync(ServiceUuid, BluetoothCacheMode.Uncached);
        if (gatt.Status != GattCommunicationStatus.Success || gatt.Services.Count == 0)
        {
            ClearCache();
            return new(true, false, "KeyLinker BLE configuration service not found on this device.");
        }

        // Disposed so the link drops and the pad advertises again for the next operation.
        using var service = gatt.Services[0];
        var charResult = await service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
        if (charResult.Status != GattCommunicationStatus.Success)
        {
            return new(true, false, "Could not enumerate BLE characteristics.");
        }

        GattCharacteristic? writeChar = null;
        GattCharacteristic? notifyChar = null;

        foreach (var c in charResult.Characteristics)
        {
            if (c.Uuid == WriteUuid) writeChar = c;
            else if (c.Uuid == NotifyUuid) notifyChar = c;
        }

        if (writeChar == null || notifyChar == null)
        {
            return new(true, false, "Required BLE characteristics (write/notify) not found.");
        }

        var cccdStatus = await notifyChar.WriteClientCharacteristicConfigurationDescriptorAsync(
            GattClientCharacteristicConfigurationDescriptorValue.Notify);
        if (cccdStatus != GattCommunicationStatus.Success)
        {
            return new(true, false, "Could not enable BLE notifications for telemetry.");
        }

        TaskCompletionSource<byte[]>? pendingReply = null;
        byte expectedSerial = 0;

        TypedEventHandler<GattCharacteristic, GattValueChangedEventArgs> handler = (s, e) =>
        {
            using var reader = DataReader.FromBuffer(e.CharacteristicValue);
            byte[] bytes = new byte[e.CharacteristicValue.Length];
            reader.ReadBytes(bytes);

            var payload=NativeTelemetry.Reply(bytes,expectedSerial);
            if(payload!=null)pendingReply?.TrySetResult(payload);
        };

        notifyChar.ValueChanged += handler;

        Task<byte[]?> SendQueryAsync(byte opcode, byte length, byte serial, byte[] payload) =>
            SendPacketAsync(X20Codec.Frame(opcode, length, serial, 0, payload), serial);

        async Task<byte[]?> SendPacketAsync(byte[] packet, byte serial)
        {
            const int timeoutMs = 2500;
            byte opcode = X20Codec.Unscramble(packet)[0];
            // The controller check asks three read-only questions and nothing else; the write test touches only the motor record.
            if (BleDiscovery.Refusal(check, vibrate != null, opcode) is { } refused) throw new InvalidOperationException(refused);
            expectedSerial = serial;
            pendingReply = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);

            var writer = new DataWriter();
            writer.WriteBytes(packet);
            var writeResult = await writeChar.WriteValueWithResultAsync(writer.DetachBuffer(), GattWriteOption.WriteWithResponse);
            if (writeResult.Status != GattCommunicationStatus.Success)
                return null;

            var timeoutTask = Task.Delay(timeoutMs);
            var finished = await Task.WhenAny(pendingReply.Task, timeoutTask);
            return finished == pendingReply.Task ? await pendingReply.Task : null;
        }

        string? firmware = null;
        string? controller = null;
        string? battery = null;
        string? hardwareId=null;
        string? telemetryNote=null;
        bool charging = false;
        bool enteredMode = false;
        PadCheck.Raw? checkRaw = null;
        PadCheck.Identity? testedId = null;
        VibrationTest.Result? vibration = null;

        try
        {
            if (vibrate is { } percent)
            {
                // Write test: identify and read the capability menu in this session, then let the engine decide whether to write.
                var session = new VibrationTest.Session();
                var link = new Link(SendPacketAsync);
                testedId = PadCheck.ParseIdentity(await VibrationTest.QueryAsync(link, session, 0x91, []));
                var caps = PadCheck.ParseCapabilities(await VibrationTest.QueryAsync(link, session, 0xB0, [0, 1]));
                vibration = await VibrationTest.RunAsync(link, testedId, caps, percent, session);
                string who = testedId is { } t ? $"  identity        vendor {t.Vid}, product {t.Pid}, firmware {t.Firmware}" + (t.Mode is { } m ? $", mode {m}" : "") : "  identity        not answered";
                return new PadBridge.Result(true, vibration.Passed, who + Environment.NewLine + VibrationTest.Describe(vibration));
            }

            // 1. Query Device Info (Op 0x91: READ_VID_PID_VERSION)
            var payInfo = await SendQueryAsync(0x91, 5, 1, []);
            if (check)
            {
                // Controller check: updater id (two chunks at most), then the capability menu. No battery query, so no mode is entered.
                byte[]? guid0 = await SendQueryAsync(0x93, 6, 2, [0]);
                byte[]? guid1 = guid0 is { Length: > 1 } && guid0[0] == 18 && guid0.Length - 1 < 18 ? await SendQueryAsync(0x93, 6, 3, [1]) : null;
                byte[]? menu = await SendQueryAsync(0xB0, 7, 4, [0, 1]);
                checkRaw = new PadCheck.Raw(payInfo, menu, guid0, guid1);
            }
            else if (payInfo != null && payInfo.Length >= 7 && payInfo[0]==payInfo.Length-1)
            {
                string vid = $"{payInfo[1]:x2}{payInfo[2]:x2}";
                string pid = $"{payInfo[3]:x2}{payInfo[4]:x2}";
                firmware = $"{payInfo[5]:x}.{payInfo[6]:x2}";
                controller = (vid == "1020" && pid == "2006") ? "EasySMX X20 Pro" :
                             (vid == "1013" && pid == "2009") ? "EasySMX X20" : "EasySMX Controller";

                // Authorize step: only send the 0x1D live battery query if controller identity is validated
                bool isAuthorized = targetModel=="x20_pro" ? vid=="1020"&&pid=="2006" : targetModel=="x20"&&vid=="1013"&&pid=="2009";
                if (isAuthorized)
                {
                    // Read the captured updater identifier without entering an update mode.
                    var firstGuid=await SendQueryAsync(0x93,6,4,[0]);
                    if(firstGuid is {Length:15} && firstGuid[0]==18)
                    {
                        var lastGuid=await SendQueryAsync(0x93,6,5,[1]);
                        if(lastGuid is {Length:4})
                        {
                            var hardware=NativeTelemetry.ParseHardware([..firstGuid[1..],..lastGuid]);
                            hardwareId=hardware.HardwareId;
                            if(hardware.Firmware!=firmware)telemetryNote="Identity and updater firmware versions differ.";
                        }
                    }
                    // 2. Query Battery (Op 0x1D: SET_MODE with [0x02, 0xDF, 0xAB])
                    enteredMode = true;
                    var payBat = await SendQueryAsync(0x1D, 8, 2, [0x02, 0xDF, 0xAB]);
                    if(payBat!=null)
                    {
                        try
                        {
                            var power=NativeTelemetry.ParsePower(payBat);
                            charging=power.Charging;
                            battery=$"{power.Level}/4"+(charging?" (Charging)":"");
                            telemetryNote=$"Battery status 0x{power.RawStatus:X2}; four-level reading from BLE.";
                        }
                        catch(ArgumentException){telemetryNote="The controller returned an unrecognised battery reply.";}
                    }
                    else telemetryNote="Battery query timed out; identity is still available.";
                }
                else
                {
                    return new(true,false,"Controller identity does not match the selected model.");
                }
            }
        }
        finally
        {
            if (enteredMode && writeChar != null)
            {
                try
                {
                    // 3. Return to normal mode (Op 0x1E: NORMAL_MODE)
                    // Wait for the write to finish with response, exactly like Python client
                    byte[] normalPkt = X20Codec.Frame(0x1E, 8, 3, 0, [0x02, 0xDF, 0xAB]);
                    var normalWriter = new DataWriter();
                    normalWriter.WriteBytes(normalPkt);
                    await writeChar.WriteValueWithResultAsync(normalWriter.DetachBuffer(), GattWriteOption.WriteWithResponse);
                }
                catch { }
            }

            if (notifyChar != null)
            {
                notifyChar.ValueChanged -= handler;
                try
                {
                    await notifyChar.WriteClientCharacteristicConfigurationDescriptorAsync(
                        GattClientCharacteristicConfigurationDescriptorValue.None);
                }
                catch { }
            }
        }

        // Cache the verified working address
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CachePath)!);
            File.WriteAllText(CachePath, bluetoothAddress.ToString("X"));
        }
        catch { }

        if (check)
        {
            var report = PadCheck.Build(checkRaw ?? new PadCheck.Raw(null, null, null, null));
            return new PadBridge.Result(true, report.Identity != null, PadCheck.Describe(report), null, checkRaw);
        }

        var sb = new StringBuilder();
        sb.AppendLine("Controller (Bluetooth Telemetry · Read-Only)");
        sb.AppendLine("──────────────────────────────────────────────────────────────");
        sb.AppendLine($"  controller      {controller ?? "EasySMX Controller"}");
        sb.AppendLine($"  firmware        {firmware ?? "Not reported"}");
        sb.AppendLine($"  address         {addressStr}");
        sb.AppendLine($"  battery         {battery ?? "Not reported"}{(battery != null ? (charging ? " · charging" : " · on battery") : "")}");
        sb.AppendLine("  transport       Bluetooth LE (read-only telemetry query)");

        if(hardwareId!=null)sb.AppendLine($"  hardware id     {hardwareId}");
        if(telemetryNote!=null)sb.AppendLine($"  note            {telemetryNote}");
        if(firmware==null)return new(true,false,"No complete controller identity reply.");
        var telemetry = new TelemetryData(controller ?? "EasySMX Controller", firmware, battery, charging, addressStr, sb.ToString(),hardwareId);
        return new PadBridge.Result(true, true, sb.ToString(), telemetry);
    }

    private static void ClearCache()
    {
        try
        {
            if (File.Exists(CachePath)) File.Delete(CachePath);
        }
        catch { }
    }

    private static string FormatMac(ulong address)
    {
        byte[] bytes = BitConverter.GetBytes(address);
        return $"{bytes[5]:X2}:{bytes[4]:X2}:{bytes[3]:X2}:{bytes[2]:X2}:{bytes[1]:X2}:{bytes[0]:X2}";
    }
}
