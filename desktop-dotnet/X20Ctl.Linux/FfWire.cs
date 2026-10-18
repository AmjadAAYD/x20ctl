namespace X20Ctl.Linux;

/// <summary>The bytes of Linux force feedback: struct input_event, struct ff_effect and uinput's upload record, laid out as the kernel headers define them
/// for a given pointer size (8 on 64-bit, 4 on 32-bit). Pure, so it can be tested without a kernel.</summary>
internal static class FfWire
{
    public const ushort EvFf = 0x15, EvUinput = 0x101, FfRumble = 0x50, FfGain = 0x60;

    /// <summary>sizeof(struct ff_effect): a 16-byte header, then a union whose largest member (periodic) ends in a pointer.</summary>
    public static int EffectSize(int pointer) => 16 + (pointer == 8 ? 32 : 28);
    /// <summary>sizeof(struct uinput_ff_upload): request id and return value, then the new effect and the old one.</summary>
    public static int UploadSize(int pointer) => 8 + 2 * EffectSize(pointer);
    public static int EventSize(int pointer) => pointer * 2 + 8;

    /// <summary>A rumble effect to hand to EVIOCSFF. An id of -1 asks the kernel for a new one.</summary>
    public static byte[] RumbleEffect(int pointer, short id, ushort strong, ushort weak, ushort length)
    {
        var e = new byte[EffectSize(pointer)];
        BitConverter.TryWriteBytes(e.AsSpan(0, 2), FfRumble); BitConverter.TryWriteBytes(e.AsSpan(2, 2), id); BitConverter.TryWriteBytes(e.AsSpan(10, 2), length);
        BitConverter.TryWriteBytes(e.AsSpan(16, 2), strong); BitConverter.TryWriteBytes(e.AsSpan(18, 2), weak);
        return e;
    }

    /// <summary>The new effect inside an upload record the kernel filled in. Strong and weak are zero unless it is a rumble effect.</summary>
    public static (ushort Type, short Id, ushort Strong, ushort Weak) NewEffect(byte[] upload) =>
        (BitConverter.ToUInt16(upload, 8), BitConverter.ToInt16(upload, 10), BitConverter.ToUInt16(upload, 24), BitConverter.ToUInt16(upload, 26));

    /// <summary>Splits a read() of input_event records into (type, code, value).</summary>
    public static List<(ushort Type, ushort Code, int Value)> Events(byte[] buffer, int length, int pointer)
    {
        int size = EventSize(pointer), time = pointer * 2; var list = new List<(ushort, ushort, int)>();
        for (int o = 0; o + size <= length; o += size) list.Add((BitConverter.ToUInt16(buffer, o + time), BitConverter.ToUInt16(buffer, o + time + 2), BitConverter.ToInt32(buffer, o + time + 4)));
        return list;
    }

    /// <summary>The strongest of each motor across the effects that are playing.</summary>
    public static (ushort Strong, ushort Weak) Mix(IReadOnlyDictionary<int, (ushort Strong, ushort Weak)> effects, IEnumerable<int> playing)
    {
        ushort strong = 0, weak = 0;
        foreach (int id in playing) if (effects.TryGetValue(id, out var m)) { strong = Math.Max(strong, m.Strong); weak = Math.Max(weak, m.Weak); }
        return (strong, weak);
    }
}
