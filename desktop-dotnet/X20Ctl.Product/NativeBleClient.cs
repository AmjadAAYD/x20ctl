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
/// Strictly read-only: reads device info and battery status. Does not write or modify controller settings.
/// </summary>
public static class NativeBleClient
{
    internal static readonly SemaphoreSlim ConfigurationGate=new(1,1);
    private static readonly Guid ServiceUuid = Guid.Parse("d7f010e0-660d-46e9-96c3-19c4148bdab5");
    private static readonly Guid WriteUuid   = Guid.Parse("d7f010e1-660d-46e9-96c3-19c4148bdab5");
    private static readonly Guid NotifyUuid  = Guid.Parse("d7f010e2-660d-46e9-96c3-19c4148bdab5");

    private static string CachePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "x20ctl", "native", "bt_cache.txt");

    public static async Task<PadBridge.Result> QueryAsync(string targetModel = "x20_pro", int timeoutSeconds = 6)
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
            return await QueryGattAsync(address.Value, addressStr, targetModel);
        }
        catch (Exception ex)
        {
            return new(true, false, $"Bluetooth query failed: {ex.Message}");
        }
        finally { ConfigurationGate.Release(); }
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
            bool nameMatch = targetModel switch
            {
                "x20_pro" => name.Equals("EasySMX X20Pro", StringComparison.OrdinalIgnoreCase) ||
                             name.Equals("EasySMX X20 Pro", StringComparison.OrdinalIgnoreCase),
                "x20"     => name.Equals("EasySMX X20", StringComparison.OrdinalIgnoreCase) ||
                             name.Equals("Xpert2", StringComparison.OrdinalIgnoreCase),
                _         => name.StartsWith("EasySMX X20", StringComparison.OrdinalIgnoreCase)
            };

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

        return (null, "Controller not found over Bluetooth. Make sure Bluetooth is enabled and the controller is awake.");
    }

    private static async Task<PadBridge.Result> QueryGattAsync(ulong bluetoothAddress, string addressStr, string targetModel)
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

        async Task<byte[]?> SendQueryAsync(byte opcode, byte length, byte serial, byte[] payload, int timeoutMs = 2500)
        {
            expectedSerial = serial;
            pendingReply = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            byte[] packet = X20Codec.Frame(opcode, length, serial, 0, payload);

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

        try
        {
            // 1. Query Device Info (Op 0x91: READ_VID_PID_VERSION)
            var payInfo = await SendQueryAsync(0x91, 5, 1, []);
            if (payInfo != null && payInfo.Length >= 7 && payInfo[0]==payInfo.Length-1)
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
