namespace X20Ctl.Zone.Lighting;

/// <summary>Only the first colour triplet is hardware verified on X20 Pro 1.44.</summary>
public static class LightingRecord
{
    public static byte[] WithSelection(byte[] record,byte red,byte green,byte blue,byte mode)
    {
        var solid=WithMode(record,2);
        var colour=WithColour(solid,red,green,blue);
        return WithMode(colour,mode);
    }
    public static byte[] WithMode(byte[] record, byte mode)
    {
        if (record.Length != 24) throw new ArgumentException("Expected a 24-byte lighting record.", nameof(record));
        if (mode is not (0 or 2 or 3 or 4)) throw new ArgumentOutOfRangeException(nameof(mode), "Unmapped native effect.");
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
}
