using System.IO;
using System.Text.RegularExpressions;

namespace X20Ctl.Product;

public sealed record TelemetryData(string? Controller, string? Firmware, string? Battery, bool Charging, string? Address, string RawText);

/// <summary>
/// Hardware telemetry reader for supported KeyLinker controllers over Bluetooth LE.
/// Communicates natively via WinRT Bluetooth LE directly in C# with zero Python required.
/// Safe and non-intrusive: 2.4 GHz USB receiver gameplay works fully without Bluetooth.
/// </summary>
public static class PadBridge
{
    public sealed record Result(bool Ran, bool Ok, string Text, TelemetryData? Telemetry = null, PadCheck.Raw? Check = null);

    public static async Task<Result> Status(string targetModel = "x20_pro")
    {
#if WINDOWS
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 15063))
        {
            return await NativeBleClient.QueryAsync(targetModel);
        }
        return new(true, false, "Bluetooth LE requires Windows 10 build 15063 or newer.");
#else
        return await Task.FromResult(new Result(false, false, "Bluetooth LE telemetry requires Windows."));
#endif
    }

    /// <summary>The read-only controller check: identity, updater id and capability menu, nothing written and no mode entered.</summary>
    public static async Task<Result> Check(string targetModel = "x20_pro")
    {
#if WINDOWS
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 15063))
        {
            return await NativeBleClient.QueryAsync(targetModel, check: true);
        }
        return new(true, false, "Bluetooth LE requires Windows 10 build 15063 or newer.");
#else
        return await Task.FromResult(new Result(false, false, "The controller check requires Windows."));
#endif
    }

    /// <summary>The write test: both motors to one value, read back, put back, read back again. Proven pads only.</summary>
    public static async Task<Result> TestVibration(string targetModel, int percent)
    {
#if WINDOWS
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 15063))
        {
            return await NativeBleClient.VibrationTestAsync(targetModel, percent);
        }
        return new(true, false, "Bluetooth LE requires Windows 10 build 15063 or newer.");
#else
        return await Task.FromResult(new Result(false, false, "The write test requires Windows."));
#endif
    }

    public static TelemetryData ParseTelemetry(string text)
    {
        string? firmware = null;
        string? battery = null;
        bool charging = false;
        string? address = null;
        string? controller = null;

        var mFw = Regex.Match(text, @"(?m)^\s{1,4}firmware\s+([^\r\n]+)", RegexOptions.IgnoreCase);
        if (mFw.Success) firmware = mFw.Groups[1].Value.Trim();

        var mBat = Regex.Match(text, @"(?m)^\s{1,4}battery\s+([^\r\n]+)", RegexOptions.IgnoreCase);
        if (mBat.Success)
        {
            string rawBat = mBat.Groups[1].Value.Trim();
            charging = rawBat.Contains("charging", StringComparison.OrdinalIgnoreCase);
            var mLevel = Regex.Match(rawBat, @"(\d+/\d+)");
            if (mLevel.Success)
            {
                battery = mLevel.Groups[1].Value + (charging ? " (Charging)" : "");
            }
            else
            {
                battery = rawBat;
            }
        }

        var mAddr = Regex.Match(text, @"(?m)^\s{1,4}address\s+([0-9A-Fa-f:]+)", RegexOptions.IgnoreCase);
        if (mAddr.Success) address = mAddr.Groups[1].Value.Trim();

        var mCtrl = Regex.Match(text, @"(?m)^\s{1,4}controller\s+([^\r\n]+)", RegexOptions.IgnoreCase);
        if (mCtrl.Success) controller = mCtrl.Groups[1].Value.Trim();

        return new TelemetryData(controller, firmware, battery, charging, address, text);
    }
}
