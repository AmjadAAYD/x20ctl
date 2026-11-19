namespace X20Ctl.Zone.Lighting;

/// <summary>Only the first colour triplet is hardware verified on X20 Pro 1.44.</summary>
public static class LightingRecord
{
    public const byte ModeOff = 0, ModeRainbow = 1, ModeSolid = 2, ModeBreathe = 3, ModeCycle = 4;

    public static string ModeName(byte mode) => mode switch
    {
        ModeOff => "Off",
        ModeRainbow => "Rainbow",
        ModeSolid => "Solid",
        ModeBreathe => "Breathe",
        ModeCycle => "Cycle",
        _ => $"Mode 0x{mode:X2}",
    };

    public static byte[] WithSelection(byte[] record,byte red,byte green,byte blue,byte mode)
    {
        var solid=WithMode(record,ModeSolid);
        // Hardware-button Rainbow can leave byte 4 non-zero; solid colour selection resets it cleanly.
        solid[4]=0;
        var colour=WithColour(solid,red,green,blue);
        return WithMode(colour,mode);
    }
    public static byte[] WithMode(byte[] record, byte mode)
    {
        if (record.Length != 24) throw new ArgumentException("Expected a 24-byte lighting record.", nameof(record));
        if (mode is not (ModeOff or ModeRainbow or ModeSolid or ModeBreathe or ModeCycle)) throw new ArgumentOutOfRangeException(nameof(mode), "Unmapped native effect.");
        var result = (byte[])record.Clone();
        result[6] = mode;
        return result;
    }

    public static byte[] WithColour(byte[] record, byte red, byte green, byte blue)
    {
        if (record.Length != 24) throw new ArgumentException("Expected a 24-byte lighting record.", nameof(record));
        if (record[4] != 0 || record[6] != 2)
            throw new InvalidOperationException("Choose Solid and a single RGB CLR colour on the controller first.");
        var result = (byte[])record.Clone();
        result[0] = red; result[1] = green; result[2] = blue;
        return result;
    }

    /// <summary>Offset 5 is hardware verified on X20 Pro 1.44 across 0x00..0xFF: 0x00 turns all LEDs off while keeping RGB/mode,
    /// 0x01 produces an ultra-faint moonlight glow below the stock software floor (0x10), up to 0xFF maximum brightness.</summary>
    public const byte MinBrightnessByte = 0x00, MoonlightBrightnessByte = 0x01, MaxBrightnessByte = 0xff;
    /// <summary>Builds a complete 24-byte lighting frame (colour, brightness 0x00..0xFF, and native mode) on top of a baseline record.</summary>
    public static byte[] WithFrame(byte[] record, byte red, byte green, byte blue, byte brightnessByte, byte mode = ModeSolid) =>
        WithBrightnessByte(WithSelection(record, red, green, blue, mode), brightnessByte);
    /// <summary>0 to 100 % over the verified full hardware range 0x00 to 0xFF.</summary>
    public static byte BrightnessByteFor(double percent) => (byte)(MinBrightnessByte + Math.Round(Math.Clamp(percent, 0, 100) / 100 * (MaxBrightnessByte - MinBrightnessByte)));
    public static double BrightnessPercentFor(byte level) => Math.Clamp((level - MinBrightnessByte) * 100d / (MaxBrightnessByte - MinBrightnessByte), 0, 100);
    public static byte[] WithBrightnessByte(byte[] record, byte level)
    {
        if (record.Length != 24) throw new ArgumentException("Expected a 24-byte lighting record.", nameof(record));
        var result = (byte[])record.Clone();
        result[5] = level;
        return result;
    }
}
