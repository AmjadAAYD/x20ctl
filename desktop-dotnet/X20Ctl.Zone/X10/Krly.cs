// SPDX-License-Identifier: GPL-3.0-or-later
// The X10's configuration protocol (KRLY / QMacro), ported to C# from Edgard's EasyGamepad (GPL-3.0-or-later,
// github.com/edgardmessias/easygamepad, docs/protocol.md and protocol/KrlyCodec.kt, ControllerSettings.kt). Credit to
// Edgard for decoding it. KRLY is not KeyLinker: no CRC, no scrambling, different service.
namespace X20Ctl.Zone.X10;

/// <summary>One KRLY frame: [total length, command, payload..., sequence].</summary>
public static class Krly
{
    public static readonly Guid Service = Guid.Parse("0000ff00-0000-1000-8000-00805f9b34fb");
    public static readonly Guid Write = Guid.Parse("0000ff01-0000-1000-8000-00805f9b34fb");
    public static readonly Guid Notify = Guid.Parse("0000ff02-0000-1000-8000-00805f9b34fb");
    /// <summary>Writes go out in chunks of at most this many bytes, whatever the MTU.</summary>
    public const int ChunkSize = 20;

    public const byte SetMode = 0x01, FactoryReset = 0x05, QueryBattery = 0x31, EndUpload = 0x34, Motors = 0x35,
        BeginUpload = 0x36, Turbo = 0x37, Triggers = 0x39, MacroStep = 0x3A, Mapping = 0x3D, Sticks = 0x3E,
        QueryMappings = 0x50, QuerySettings = 0x77, QueryVersion = 0x84;

    public static byte[] Frame(byte command, byte[] payload, byte sequence)
    {
        if (payload.Length > 252) throw new ArgumentException("A KRLY payload is at most 252 bytes.", nameof(payload));
        return [(byte)(payload.Length + 3), command, .. payload, sequence];
    }

    /// <summary>The vendor key codes, by X20CTL's button names.</summary>
    public static readonly IReadOnlyDictionary<string, byte> Keys = new Dictionary<string, byte>
    {
        ["A"] = 0xA0, ["B"] = 0xA1, ["X"] = 0xA2, ["Y"] = 0xA3, ["LB"] = 0xA4, ["LT"] = 0xA5, ["L3"] = 0xA6,
        ["RB"] = 0xA7, ["RT"] = 0xA8, ["R3"] = 0xA9, ["SELECT"] = 0xAA, ["START"] = 0xAB,
        ["DPAD_LEFT"] = 0xD1, ["DPAD_RIGHT"] = 0xD2, ["DPAD_UP"] = 0xD3, ["DPAD_DOWN"] = 0xD4,
        ["M1"] = 0x01, ["M2"] = 0x02, ["M3"] = 0x03, ["M4"] = 0x04,
    };
    public static string? KeyName(byte code) => Keys.FirstOrDefault(k => k.Value == code).Key;
    static byte Key(string name) => Keys.TryGetValue(name, out var code) ? code : throw new ArgumentException($"The X10 has no key '{name}'.");

    public sealed record Battery(int Percent, bool Charging, bool Low);
    /// <summary>Command 0x31, solicited or not: offset 5 is the percentage (offsets 2 and 4 are display fields, not it).</summary>
    public static Battery? ParseBattery(byte[] raw) =>
        raw.Length >= 7 && raw[1] == QueryBattery && raw[5] <= 100 ? new(raw[5], raw[3] != 0, raw[6] != 0) : null;

    public sealed record Version(string Project, string Protocol, string Firmware, string Hardware);
    /// <summary>Command 0x84 (and the older 0x09): hex strings, not version numbers.</summary>
    public static Version? ParseVersion(byte[] raw)
    {
        if (raw.Length < 2) return null;
        string Part(int start, int end) => Convert.ToHexString(raw, start, end - start);
        return raw[1] switch
        {
            0x84 when raw.Length >= 17 => new(Part(4, 7), Part(7, 10), Part(10, 14), Part(14, 17)),
            0x09 when raw.Length >= 11 => new(Part(2, 5), Part(5, 8), Part(8, 11), raw.Length >= 15 ? Part(11, 14) : "—"),
            _ => null,
        };
    }

    public sealed record Range(int Min, int Max)
    {
        public void Validate() { if (Min is < 0 or > 99 || Max is < 1 or > 100 || Min >= Max) throw new FormatException($"Range {Min}..{Max} is out of bounds."); }
    }
    public sealed record Stick(int Min, int Max, bool InvertX, bool InvertY)
    {
        public void Validate() => new Range(Min, Max).Validate();
    }
    public sealed record TurboEntry(string Key, bool Automatic, int Rate);
    public sealed record Settings(Range LeftTrigger, Range RightTrigger, Stick LeftStick, Stick RightStick, int LeftMotor, int RightMotor, IReadOnlyList<TurboEntry> Turbo)
    {
        public void Validate()
        {
            LeftTrigger.Validate(); RightTrigger.Validate(); LeftStick.Validate(); RightStick.Validate();
            if (LeftMotor is < 0 or > 100 || RightMotor is < 0 or > 100) throw new FormatException("Motor strength is 0 to 100.");
            if (Turbo.Any(t => t.Rate is < 1 or > 20)) throw new FormatException("Turbo rate is 1 to 20.");
            if (Turbo.Select(t => t.Key).Distinct().Count() != Turbo.Count) throw new FormatException("Each turbo key appears once.");
        }

        /// <summary>The settings content (four length-prefixed sections: turbo, triggers, sticks, motors).</summary>
        public static Settings Decode(byte[] content)
        {
            int offset = 0;
            byte[] Section()
            {
                if (offset >= content.Length) throw new FormatException("A settings section is missing.");
                int length = content[offset++];
                if (offset + length > content.Length) throw new FormatException("A settings section is cut short.");
                var part = content[offset..(offset + length)]; offset += length; return part;
            }
            var turbo = Section(); var triggers = Section(); var sticks = Section(); var motors = Section();
            if (offset != content.Length || turbo.Length % 3 != 0 || triggers.Length != 4 || sticks.Length != 9 || motors.Length != 2) throw new FormatException("The settings have an unexpected shape.");
            if (new[] { sticks[2], sticks[3], sticks[6], sticks[7] }.Any(b => b > 1)) throw new FormatException("A stick inversion flag is not 0 or 1.");
            if (sticks[8] != 0) throw new FormatException("The controller uses a stick swap X20CTL doesn't edit.");
            var entries = turbo.Chunk(3).Select(t => t[1] > 1 ? throw new FormatException("A turbo flag is not 0 or 1.")
                : new TurboEntry(KeyName(t[0]) ?? throw new FormatException($"Unknown turbo key {t[0]:X2}."), t[1] == 1, t[2])).ToList();
            var settings = new Settings(new(triggers[0], triggers[1]), new(triggers[2], triggers[3]),
                new(sticks[0], sticks[1], sticks[2] == 1, sticks[3] == 1), new(sticks[4], sticks[5], sticks[6] == 1, sticks[7] == 1),
                motors[0], motors[1], entries);
            settings.Validate();
            return settings;
        }
    }

    /// <summary>Collects the pages of a settings reply (0x77, selector 02) until the last one.</summary>
    public sealed class SettingsPages
    {
        int? next, total; readonly List<byte> content = [];
        public Settings? Accept(byte[] raw)
        {
            if (raw.Length < 6 || raw[0] != raw.Length || raw[1] != QuerySettings || raw[2] != 2) throw new FormatException("Not a settings page.");
            int pages = raw[3], page = raw[4];
            if (pages > 64 || page > pages || (next == null ? page > 1 : page != next) || (total != null && total != pages)) throw new FormatException("Settings pages arrived out of order.");
            total = pages; next = page + 1;
            content.AddRange(raw[5..^1]);
            if (content.Count > 4096) throw new FormatException("The settings are too long.");
            return page == pages ? Settings.Decode([.. content]) : null;
        }
    }

    /// <summary>Collects the pages of a mapping reply (0x50): a byte count, then (kind, source, target) triples.</summary>
    public sealed class MappingPages
    {
        int? next, total; readonly List<byte> content = [];
        public IReadOnlyDictionary<string, string>? Accept(byte[] raw)
        {
            if (raw.Length < 5 || raw[1] != QueryMappings) throw new FormatException("Not a mapping page.");
            int pages = raw[2], page = raw[3];
            if (pages > 64 || page > pages || (next == null ? page > 1 : page != next) || (total != null && total != pages)) throw new FormatException("Mapping pages arrived out of order.");
            total = pages; next = page + 1;
            content.AddRange(raw[4..^1]);
            if (content.Count > 1024) throw new FormatException("The mappings are too long.");
            if (page < pages) return null;
            if (content.Count == 0) throw new FormatException("The mapping reply is empty.");
            int count = content[0];
            if (count % 3 != 0 || content.Count < count + 1) throw new FormatException("The mapping list is cut short.");
            var map = new Dictionary<string, string>();
            for (int i = 1; i <= count; i += 3)
                if (content[i] == 0 && KeyName(content[i + 1]) is { } source && KeyName(content[i + 2]) is { } target) map[source] = target;
            return map;
        }
    }

    /// <summary>Splits a byte stream of notifications into whole frames (a notification may hold part of one, or several).</summary>
    public sealed class FrameReader
    {
        readonly List<byte> buffer = [];
        public IReadOnlyList<byte[]> Feed(byte[] chunk)
        {
            var frames = new List<byte[]>();
            lock (buffer)
            {
                buffer.AddRange(chunk);
                while (buffer.Count > 0)
                {
                    int length = buffer[0];
                    if (length < 3) { buffer.RemoveAt(0); continue; }
                    if (buffer.Count < length) break;
                    frames.Add([.. buffer.GetRange(0, length)]); buffer.RemoveRange(0, length);
                }
            }
            return frames;
        }
    }

    // ---- writes: encoded and tested, NOT sent. They stay locked until a real X10 confirms ACK, read-back and persistence.

    public static byte[] TriggersPayload(Range left, Range right) { left.Validate(); right.Validate(); return [(byte)left.Min, (byte)left.Max, (byte)right.Min, (byte)right.Max]; }
    public static byte[] SticksPayload(Stick left, Stick right)
    {
        left.Validate(); right.Validate();
        return [(byte)left.Min, (byte)left.Max, (byte)(left.InvertX ? 1 : 0), (byte)(left.InvertY ? 1 : 0), (byte)right.Min, (byte)right.Max, (byte)(right.InvertX ? 1 : 0), (byte)(right.InvertY ? 1 : 0)];
    }
    public static byte[] MotorsPayload(int left, int right) =>
        left is < 0 or > 100 || right is < 0 or > 100 ? throw new ArgumentOutOfRangeException(nameof(left)) : [(byte)left, (byte)right];
    public static byte[] MappingPayload(string source, string target) => [Key(source), Key(target)];
    public static byte[] TurboPayload(TurboEntry t) => t.Rate is < 1 or > 20 ? throw new ArgumentOutOfRangeException(nameof(t)) : [Key(t.Key), (byte)(t.Automatic ? 1 : 0), (byte)t.Rate];
    /// <summary>One macro step; durations are big-endian; the repeat byte is inverted (0 = repeat).</summary>
    public static byte[] MacroStepPayload(string trigger, bool repeat, int index, int holdMs, int waitMs, string key)
    {
        if (index is < 0 or > 31 || holdMs is < 1 or > 65535 || waitMs is < 0 or > 65535) throw new ArgumentOutOfRangeException(nameof(index));
        return [Key(trigger), (byte)(repeat ? 0 : 1), (byte)index, (byte)(holdMs >> 8), (byte)holdMs, (byte)(waitMs >> 8), (byte)waitMs, Key(key)];
    }

    /// <summary>The order EasyGamepad applies a profile in, each step awaiting an ACK (byte 2 == 0): editing mode, begin,
    /// triggers, sticks, mappings, motors, end, normal mode. Macros and turbo are left out here.</summary>
    public static IReadOnlyList<(byte Command, byte[] Payload)> ApplyPlan(Settings settings, IReadOnlyDictionary<string, string> mappings)
    {
        settings.Validate();
        var plan = new List<(byte, byte[])> { (SetMode, [0x10]), (BeginUpload, [0, 0]) };
        plan.AddRange(settings.Turbo.Select(t => (Turbo, TurboPayload(t))));
        plan.Add((Triggers, TriggersPayload(settings.LeftTrigger, settings.RightTrigger)));
        plan.Add((Sticks, SticksPayload(settings.LeftStick, settings.RightStick)));
        plan.AddRange(mappings.Select(m => (Mapping, MappingPayload(m.Key, m.Value))));
        plan.Add((Motors, MotorsPayload(settings.LeftMotor, settings.RightMotor)));
        plan.Add((EndUpload, []));
        plan.Add((SetMode, [0x00]));
        return plan;
    }
    public static bool IsAck(byte[] raw, byte command) => raw.Length >= 3 && raw[1] == command && raw[2] == 0;
}
