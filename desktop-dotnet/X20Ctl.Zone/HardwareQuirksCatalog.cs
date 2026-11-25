namespace X20Ctl.Zone;

public sealed record HardwareQuirkEntry(
    string Model,
    string Transport,
    string Summary,
    string Recommendation);

/// <summary>
/// Curated, hardware-verified protocol and transport compatibility guide derived from X20CTL lab findings.
/// Never contains speculative opcodes or AI/generic filler.
/// </summary>
public static class HardwareQuirksCatalog
{
    public static IReadOnlyList<HardwareQuirkEntry> Entries { get; } =
    [
        new(
            "EasySMX X20 Pro",
            "2.4 GHz Dongle (413D:2131) + Simultaneous BLE",
            "Over 2.4 GHz XInput works at full polling rate while onboard RGB/config can still be reached over unpaired BLE GATT from Windows.",
            "Keep PC Bluetooth enabled when using the 2.4 GHz dongle if you want live X20CTL RGB changes without re-pairing."),
        new(
            "EasySMX X20 Pro",
            "160x80 LCD Screen USB Serial (White Flash Mode)",
            "The built-in 160x80 TFT does not listen for pixel/GIF frames during normal gameplay modes.",
            "Turn the controller off, hold the M button, and plug in USB (White flash mode) before uploading images or animated templates."),
        new(
            "EasySMX X20 Pro",
            "4-Motor Triggers (LT / RT Rumble)",
            "Over 2.4 GHz / wired XInput, LT and RT triggers mirror grip rumble unless linked on-pad; Bluetooth exposes independent 4-motor trigger impulse channels.",
            "Hold M + Back on the controller to toggle linked trigger vibration on dongle/USB, or switch to Bluetooth mode for independent triggers."),
        new(
            "EasySMX X20 & X10",
            "KeyLinker BLE Config Blocks (0x91, 0xB1-0xB7)",
            "Firmware differences across pad revisions change which KeyLinker GATT opcodes are supported; X20CTL only writes verified read-back blocks.",
            "Use Device -> Hardware Internals to snapshot live BLE GATT config blocks read-only, and use PC mode for advanced SOCD/Rapid-Trigger/macros."),
        new(
            "Generic XInput Clones",
            "045E:028E & DirectInput 0079:181C Traps",
            "Several third-party dongles reuse standard Microsoft 045E:028E XInput IDs or DragonRise 0079:181C DirectInput descriptors.",
            "Controller Verify separates verified EasySMX descriptors from generic IDs so incompatible KeyLinker writes are never sent."),
    ];
}
