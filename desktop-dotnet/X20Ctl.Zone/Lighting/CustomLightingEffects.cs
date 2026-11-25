using System.Globalization;

namespace X20Ctl.Zone.Lighting;

public enum LightingEffectKind
{
    Solid,
    NativeRainbow,
    NativeBreathe,
    NativeCycle,
    NativeOff,
    SmoothHueWave,
    PaletteFade,
    PaletteStep,
    MoonlightPulse,
    DualStrobe,
    EmberShimmer,
    TriggerGlow,
    StickHeat,
    ButtonFlash,
    IdleSleepDim,
    GameAutoProfile,
    ScreenAmbient,
    BatteryLevel,
    AudioReactive,
}

public enum AudioReactiveStyle
{
    AutoAdaptive,
    SpectrumFlow,
    BeatShift,
    BassFire,
    AuroraDrift,
    DropStrobe,
}

public readonly record struct LightingFrame(byte R, byte G, byte B, byte Brightness, byte NativeMode = LightingRecord.ModeSolid)
{
    public string Hex => $"#{R:X2}{G:X2}{B:X2}";
    public int ColorInt => (R << 16) | (G << 8) | B;
    public bool VisuallySameAs(LightingFrame other) =>
        R == other.R && G == other.G && B == other.B && Brightness == other.Brightness && NativeMode == other.NativeMode;
}

public readonly record struct LightingInputState(
    double LeftMag,
    double RightMag,
    double LeftTrigger,
    double RightTrigger,
    bool AnyButtonPressed,
    double IdleSeconds,
    double AudioLevel = 0.0,
    double AudioBass = 0.0,
    double AudioMid = 0.0,
    double AudioTreble = 0.0,
    double AudioBeatPulse = 0.0,
    int AudioBeatCount = 0,
    double AudioCentroid = 0.5,
    double AudioTrebleTransient = 0.0,
    double AudioEnergy = 0.0,
    double AudioVocal = 0.0,
    double AudioInstrument = 0.0,
    double AudioBassPunch = 0.0,
    double AudioDynamic = 0.0,
    AudioMusicVibe AudioVibe = AudioMusicVibe.Silent);

public sealed record LightingEffectSpec
{
    public const int MinStepIntervalMs = 60;
    public const int MaxStepIntervalMs = 5000;
    public const int MinCyclePeriodMs = 800;
    public const int MaxCyclePeriodMs = 30000;

    public LightingEffectKind Kind { get; init; } = LightingEffectKind.SmoothHueWave;
    public string[] Palette { get; init; } = ["#00C8FF", "#FF00AA", "#FFB400"];
    public byte MaxBrightness { get; init; } = 0xB0;
    public byte MinBrightness { get; init; } = LightingRecord.MoonlightBrightnessByte;
    public int StepIntervalMs { get; init; } = 85;
    public int CyclePeriodMs { get; init; } = 4000;
    public int IdleTimeoutSeconds { get; init; } = 60;
    public bool RestoreOnStop { get; init; } = true;
    public int MaxSessionWrites { get; init; } = 0;
    public int AudioSensitivity { get; init; } = 65;
    public AudioReactiveStyle AudioStyle { get; init; } = AudioReactiveStyle.AutoAdaptive;
    public int AudioHapticsStrength { get; init; } = 0;
    public AudioHapticsMode AudioHapticsStyle { get; init; } = AudioHapticsMode.AutoAdaptive;
    public MotorRoleMap AudioMotorRoles { get; init; } = MotorRoleMap.Default;
    public bool BeatFlashBoost { get; init; }

    public bool IsNativeMode => Kind is LightingEffectKind.Solid or LightingEffectKind.NativeRainbow
        or LightingEffectKind.NativeBreathe or LightingEffectKind.NativeCycle or LightingEffectKind.NativeOff;

    public bool IsStreamedSoftwareEffect => !IsNativeMode;

    public bool RequiresLiveInput => Kind is LightingEffectKind.TriggerGlow
        or LightingEffectKind.StickHeat or LightingEffectKind.ButtonFlash or LightingEffectKind.IdleSleepDim;

    public bool RequiresForegroundWatcher => Kind == LightingEffectKind.GameAutoProfile;

    public LightingEffectSpec Normalized()
    {
        var clean = (Palette ?? [])
            .Select(NormalizeHexOrNull)
            .Where(h => h != null)
            .Select(h => h!)
            .Take(8)
            .ToArray();
        if (clean.Length == 0) clean = ["#00C8FF"];
        byte maxB = MaxBrightness;
        byte minB = Math.Min(MinBrightness, maxB);
        return this with
        {
            Palette = clean,
            MaxBrightness = maxB,
            MinBrightness = minB,
            StepIntervalMs = Math.Clamp(StepIntervalMs, MinStepIntervalMs, MaxStepIntervalMs),
            CyclePeriodMs = Math.Clamp(CyclePeriodMs, MinCyclePeriodMs, MaxCyclePeriodMs),
            IdleTimeoutSeconds = Math.Clamp(IdleTimeoutSeconds, 5, 1800),
            MaxSessionWrites = Math.Clamp(MaxSessionWrites, 0, 50000),
            AudioSensitivity = Math.Clamp(AudioSensitivity, 0, 100),
            AudioHapticsStrength = Math.Clamp(AudioHapticsStrength, 0, 100),
        };
    }

    public byte? ToNativeModeByte() => Kind switch
    {
        LightingEffectKind.NativeOff => LightingRecord.ModeOff,
        LightingEffectKind.NativeRainbow => LightingRecord.ModeRainbow,
        LightingEffectKind.Solid => LightingRecord.ModeSolid,
        LightingEffectKind.NativeBreathe => LightingRecord.ModeBreathe,
        LightingEffectKind.NativeCycle => LightingRecord.ModeCycle,
        _ => null,
    };

    public static string? NormalizeHexOrNull(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        string s = raw.Trim().TrimStart('#');
        return s.Length == 6 && int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb)
            ? $"#{(rgb >> 16) & 0xff:X2}{(rgb >> 8) & 0xff:X2}{rgb & 0xff:X2}"
            : null;
    }

    public static (byte R, byte G, byte B) ParseRgb(string hex)
    {
        string norm = NormalizeHexOrNull(hex) ?? "#FF0000";
        int rgb = int.Parse(norm.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return ((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
    }

    public static string EffectLabel(LightingEffectKind kind) => kind switch
    {
        LightingEffectKind.Solid => "Solid colour",
        LightingEffectKind.NativeRainbow => "Rainbow (Hardware 0x01)",
        LightingEffectKind.NativeBreathe => "Breathe (Hardware 0x03)",
        LightingEffectKind.NativeCycle => "Cycle (Hardware 0x04)",
        LightingEffectKind.NativeOff => "LEDs Off (Hardware 0x00)",
        LightingEffectKind.SmoothHueWave => "Smooth Hue Sweep",
        LightingEffectKind.PaletteFade => "Multi-Color Fade",
        LightingEffectKind.PaletteStep => "Multi-Color Jump",
        LightingEffectKind.MoonlightPulse => "Moonlight Pulse (down to 0x01)",
        LightingEffectKind.DualStrobe => "Dual Flash / Strobe",
        LightingEffectKind.EmberShimmer => "Fire / Ember Shimmer",
        LightingEffectKind.TriggerGlow => "Trigger Pressure Glow",
        LightingEffectKind.StickHeat => "Stick Deflection Heat",
        LightingEffectKind.ButtonFlash => "Button Action Flash",
        LightingEffectKind.IdleSleepDim => "Idle Auto-Dim / Nightlight",
        LightingEffectKind.GameAutoProfile => "Auto by Foreground Game",
        LightingEffectKind.ScreenAmbient => "Ambient Screen Sync",
        LightingEffectKind.BatteryLevel => "Battery Level Indicator",
        LightingEffectKind.AudioReactive => "Audio Reactive RGB",
        _ => kind.ToString(),
    };
}

public sealed record LightingPreset(
    string Id,
    string Name,
    LightingEffectSpec Spec,
    DateTimeOffset UpdatedUtc);

public sealed record GameLightingRule(string ExeName, string PresetId);

/// <summary>
/// Pure deterministic frame synthesizer for native and custom BLE-streamed X20 Pro lighting effects.
/// Hardware note: EasySMX X20 Pro exposes two physically linked side LED bars driven uniformly by bytes 0..2, 5, and 6.
/// </summary>
public static class LightingEffectEngine
{
    public static IReadOnlyList<LightingPreset> BuiltInPresets { get; } =
    [
        BuiltIn("builtin-rainbow", "Hardware Rainbow (0x01)", new LightingEffectSpec
        {
            Kind = LightingEffectKind.NativeRainbow,
            Palette = ["#FF0000"],
            MaxBrightness = 0xB0,
        }),
        BuiltIn("builtin-moonlight-pulse", "Moonlight Pulse (1% Floor)", new LightingEffectSpec
        {
            Kind = LightingEffectKind.MoonlightPulse,
            Palette = ["#6EC8FF", "#9E7BFF"],
            MinBrightness = LightingRecord.MoonlightBrightnessByte,
            MaxBrightness = 0x58,
            CyclePeriodMs = 4800,
            StepIntervalMs = 95,
        }),
        BuiltIn("builtin-moonlight-dim", "Ultra-Dim Moonlight Cyan (0x01)", new LightingEffectSpec
        {
            Kind = LightingEffectKind.Solid,
            Palette = ["#00E5FF"],
            MaxBrightness = LightingRecord.MoonlightBrightnessByte,
        }),
        BuiltIn("builtin-hue-sweep", "Smooth Hue Sweep", new LightingEffectSpec
        {
            Kind = LightingEffectKind.SmoothHueWave,
            MaxBrightness = 0xC8,
            CyclePeriodMs = 6000,
            StepIntervalMs = 80,
        }),
        BuiltIn("builtin-cyber-fade", "Cyberpunk Cyan/Magenta Fade", new LightingEffectSpec
        {
            Kind = LightingEffectKind.PaletteFade,
            Palette = ["#00FFFF", "#FF00FF", "#3A6CFF"],
            MaxBrightness = 0xD8,
            CyclePeriodMs = 4500,
            StepIntervalMs = 90,
        }),
        BuiltIn("builtin-police-strobe", "Dual Red/Blue Strobe", new LightingEffectSpec
        {
            Kind = LightingEffectKind.DualStrobe,
            Palette = ["#FF0020", "#0055FF"],
            MaxBrightness = 0xFF,
            CyclePeriodMs = 900,
            StepIntervalMs = 180,
        }),
        BuiltIn("builtin-lava-ember", "Lava & Ember Shimmer", new LightingEffectSpec
        {
            Kind = LightingEffectKind.EmberShimmer,
            Palette = ["#FF2A00", "#FF8C00", "#FF5500"],
            MinBrightness = 0x35,
            MaxBrightness = 0xE8,
            CyclePeriodMs = 3200,
            StepIntervalMs = 90,
        }),
        BuiltIn("builtin-trigger-glow", "Trigger Pull Glow (LT/RT)", new LightingEffectSpec
        {
            Kind = LightingEffectKind.TriggerGlow,
            Palette = ["#1B5CFF", "#FF2E00"],
            MinBrightness = 0x24,
            MaxBrightness = 0xFF,
            StepIntervalMs = 75,
        }),
        BuiltIn("builtin-stick-heat", "Stick Deflection Heat", new LightingEffectSpec
        {
            Kind = LightingEffectKind.StickHeat,
            Palette = ["#00E0FF", "#FFAA00", "#FF1E56"],
            MinBrightness = 0x28,
            MaxBrightness = 0xFF,
            StepIntervalMs = 75,
        }),
        BuiltIn("builtin-button-flash", "Button Press Flash", new LightingEffectSpec
        {
            Kind = LightingEffectKind.ButtonFlash,
            Palette = ["#1D3557", "#00F5D4"],
            MinBrightness = 0x20,
            MaxBrightness = 0xFF,
            StepIntervalMs = 75,
        }),
        BuiltIn("builtin-idle-nightlight", "Idle Auto-Dim (Moonlight)", new LightingEffectSpec
        {
            Kind = LightingEffectKind.IdleSleepDim,
            Palette = ["#4CC9F0", "#4CC9F0"],
            MinBrightness = LightingRecord.MoonlightBrightnessByte,
            MaxBrightness = 0xA0,
            IdleTimeoutSeconds = 30,
            StepIntervalMs = 250,
        }),
        BuiltIn("builtin-screen-ambient", "Ambient Screen Sync", new LightingEffectSpec
        {
            Kind = LightingEffectKind.ScreenAmbient,
            Palette = ["#1E88E5"],
            MaxBrightness = 0xE8,
            StepIntervalMs = 70,
        }),
        BuiltIn("builtin-audio-reactive", "Audio Reactive · Spectrum Flow", new LightingEffectSpec
        {
            Kind = LightingEffectKind.AudioReactive,
            AudioStyle = AudioReactiveStyle.SpectrumFlow,
            Palette = ["#FF0040", "#00F5D4", "#3D8BFF", "#FFB800", "#A855F7"],
            MinBrightness = 0x18,
            MaxBrightness = 0xFF,
            StepIntervalMs = 65,
            AudioSensitivity = 65,
        }),
        BuiltIn("builtin-audio-beat-shift", "Audio Reactive · Beat Shift", new LightingEffectSpec
        {
            Kind = LightingEffectKind.AudioReactive,
            AudioStyle = AudioReactiveStyle.BeatShift,
            Palette = ["#00FFFF", "#FF00AA", "#FFAA00", "#00FF9D", "#7C5CFF"],
            MinBrightness = 0x20,
            MaxBrightness = 0xFF,
            StepIntervalMs = 65,
            AudioSensitivity = 65,
        }),
        BuiltIn("builtin-off", "LEDs Off (0x00)", new LightingEffectSpec
        {
            Kind = LightingEffectKind.NativeOff,
            Palette = ["#000000"],
            MaxBrightness = 0x00,
        }),
    ];

    static LightingPreset BuiltIn(string id, string name, LightingEffectSpec spec) =>
        new(id, name, spec.Normalized(), DateTimeOffset.UnixEpoch);

    public static LightingFrame Evaluate(
        LightingEffectSpec rawSpec,
        TimeSpan elapsed,
        LightingInputState input = default,
        TimeSpan? sinceLastButtonPress = null)
    {
        var spec = rawSpec.Normalized();
        var (r0, g0, b0) = LightingEffectSpec.ParseRgb(spec.Palette[0]);
        byte maxB = spec.MaxBrightness;
        byte minB = spec.MinBrightness;

        if (spec.ToNativeModeByte() is { } nativeMode)
        {
            byte level = nativeMode == LightingRecord.ModeOff ? LightingRecord.MinBrightnessByte : maxB;
            return new(r0, g0, b0, level, nativeMode);
        }

        double sec = Math.Max(0, elapsed.TotalSeconds);
        double periodSec = spec.CyclePeriodMs / 1000.0;
        double phase = (sec % periodSec) / periodSec;

        switch (spec.Kind)
        {
            case LightingEffectKind.SmoothHueWave:
            {
                var (r, g, b) = HsvToRgb(phase * 360.0, 1.0, 1.0);
                return new(r, g, b, maxB);
            }
            case LightingEffectKind.PaletteFade:
            {
                var (r, g, b) = SamplePaletteGradient(spec.Palette, phase);
                return new(r, g, b, maxB);
            }
            case LightingEffectKind.PaletteStep:
            {
                int index = ((int)(sec * 1000.0 / spec.StepIntervalMs)) % spec.Palette.Length;
                var (r, g, b) = LightingEffectSpec.ParseRgb(spec.Palette[index]);
                return new(r, g, b, maxB);
            }
            case LightingEffectKind.MoonlightPulse:
            {
                double wave = 0.5 - 0.5 * Math.Cos(phase * 2.0 * Math.PI);
                var (r, g, b) = spec.Palette.Length > 1
                    ? SamplePaletteGradient(spec.Palette, phase)
                    : (r0, g0, b0);
                byte level = LerpByte(minB, maxB, wave);
                return new(r, g, b, level);
            }
            case LightingEffectKind.DualStrobe:
            {
                string c1 = spec.Palette[0];
                string c2 = spec.Palette.Length > 1 ? spec.Palette[1] : "#0055FF";
                int toggle = ((int)(sec * 1000.0 / spec.StepIntervalMs)) & 1;
                var (r, g, b) = LightingEffectSpec.ParseRgb(toggle == 0 ? c1 : c2);
                return new(r, g, b, maxB);
            }
            case LightingEffectKind.EmberShimmer:
            {
                double flicker = 0.5 + 0.32 * Math.Sin(sec * 5.2) + 0.18 * Math.Sin(sec * 11.7);
                flicker = Math.Clamp(flicker, 0.0, 1.0);
                var stops = spec.Palette.Length >= 2 ? spec.Palette : ["#FF2200", "#FF8C00", "#FFB800"];
                var (r, g, b) = SamplePaletteGradient(stops, flicker * 0.85);
                byte level = LerpByte(minB, maxB, 0.35 + 0.65 * flicker);
                return new(r, g, b, level);
            }
            case LightingEffectKind.TriggerGlow:
            {
                double pull = Math.Clamp(Math.Max(input.LeftTrigger, input.RightTrigger), 0.0, 1.0);
                string endHex = spec.Palette.Length > 1 ? spec.Palette[1] : "#FF2800";
                var (r1, g1, b1) = LightingEffectSpec.ParseRgb(endHex);
                var (r, g, b) = LerpRgb((r0, g0, b0), (r1, g1, b1), pull);
                return new(r, g, b, LerpByte(minB, maxB, pull));
            }
            case LightingEffectKind.StickHeat:
            {
                double deflection = Math.Clamp(Math.Max(input.LeftMag, input.RightMag), 0.0, 1.0);
                var stops = spec.Palette.Length >= 2 ? spec.Palette : ["#00E0FF", "#FFAA00", "#FF1E56"];
                var (r, g, b) = SamplePaletteGradient(stops, deflection * 0.999);
                return new(r, g, b, LerpByte(minB, maxB, 0.15 + 0.85 * deflection));
            }
            case LightingEffectKind.ButtonFlash:
            {
                bool active = input.AnyButtonPressed
                    || (sinceLastButtonPress is { } age && age >= TimeSpan.Zero && age.TotalMilliseconds <= 550);
                string flashHex = spec.Palette.Length > 1 ? spec.Palette[1] : "#00F5D4";
                var (r, g, b) = active ? LightingEffectSpec.ParseRgb(flashHex) : (r0, g0, b0);
                return new(r, g, b, active ? maxB : minB);
            }
            case LightingEffectKind.IdleSleepDim:
            {
                bool idle = input.IdleSeconds >= spec.IdleTimeoutSeconds;
                double fade = idle
                    ? Math.Clamp((input.IdleSeconds - spec.IdleTimeoutSeconds) / 3.0, 0.0, 1.0)
                    : 0.0;
                return new(r0, g0, b0, LerpByte(maxB, minB, fade));
            }
            case LightingEffectKind.AudioReactive:
            {
                return EvaluateAudioReactive(spec, sec, input, minB, maxB);
            }
            default:
                return new(r0, g0, b0, maxB);
        }
    }

    public static (byte R, byte G, byte B) HsvToRgb(double hueDeg, double sat, double val)
    {
        double h = ((hueDeg % 360.0) + 360.0) % 360.0 / 60.0;
        double c = Math.Clamp(val, 0, 1) * Math.Clamp(sat, 0, 1);
        double x = c * (1.0 - Math.Abs(h % 2.0 - 1.0));
        double m = Math.Clamp(val, 0, 1) - c;
        (double r1, double g1, double b1) = (int)h switch
        {
            0 => (c, x, 0.0),
            1 => (x, c, 0.0),
            2 => (0.0, c, x),
            3 => (0.0, x, c),
            4 => (x, 0.0, c),
            _ => (c, 0.0, x),
        };
        return (ClampByte((r1 + m) * 255.0), ClampByte((g1 + m) * 255.0), ClampByte((b1 + m) * 255.0));
    }

    static (byte R, byte G, byte B) SamplePaletteGradient(string[] palette, double t)
    {
        if (palette.Length == 1) return LightingEffectSpec.ParseRgb(palette[0]);
        double wrapped = ((t % 1.0) + 1.0) % 1.0;
        double scaled = wrapped * palette.Length;
        int i0 = ((int)Math.Floor(scaled)) % palette.Length;
        int i1 = (i0 + 1) % palette.Length;
        double frac = scaled - Math.Floor(scaled);
        return LerpRgb(LightingEffectSpec.ParseRgb(palette[i0]), LightingEffectSpec.ParseRgb(palette[i1]), frac);
    }

    static (byte R, byte G, byte B) LerpRgb((byte R, byte G, byte B) a, (byte R, byte G, byte B) b, double t)
    {
        double k = Math.Clamp(t, 0.0, 1.0);
        return (LerpByte(a.R, b.R, k), LerpByte(a.G, b.G, k), LerpByte(a.B, b.B, k));
    }

    static byte LerpByte(byte a, byte b, double t) =>
        ClampByte(a + (b - a) * Math.Clamp(t, 0.0, 1.0));

    static byte ClampByte(double value) => (byte)Math.Clamp(Math.Round(value), 0, 255);

    static LightingFrame EvaluateAudioReactive(
        LightingEffectSpec spec,
        double sec,
        LightingInputState input,
        byte minB,
        byte maxB)
    {
        double sensNorm = Math.Clamp(spec.AudioSensitivity, 0, 100) / 100.0;
        double legacyGain = 0.5 + sensNorm * 3.5;
        bool hasBands = input.AudioBass > 0.0 || input.AudioMid > 0.0 || input.AudioTreble > 0.0 || input.AudioBeatPulse > 0.0;

        if (!hasBands && spec.AudioStyle is AudioReactiveStyle.SpectrumFlow or AudioReactiveStyle.AutoAdaptive)
        {
            double levelLegacy = Math.Clamp(input.AudioLevel * legacyGain, 0.0, 1.0);
            var legC0 = LightingEffectSpec.ParseRgb(spec.Palette[0]);
            var legC1 = spec.Palette.Length > 1 ? LightingEffectSpec.ParseRgb(spec.Palette[1]) : legC0;
            var legC2 = spec.Palette.Length > 2 ? LightingEffectSpec.ParseRgb(spec.Palette[2]) : legC1;
            var (r, g, b) = spec.Palette.Length switch
            {
                1 => legC0,
                2 => LerpRgb(legC0, legC1, levelLegacy),
                _ => levelLegacy <= 0.5
                    ? LerpRgb(legC0, legC1, levelLegacy / 0.5)
                    : LerpRgb(legC1, legC2, (levelLegacy - 0.5) / 0.5),
            };
            return new(r, g, b, LerpByte(minB, maxB, levelLegacy));
        }

        // Calibrated gain prevents multi-band FFT levels from pinning permanently at maxB.
        double gain = 0.50 + sensNorm * 1.05;
        double level = Math.Clamp(input.AudioLevel * gain, 0.0, 1.0);
        double bass = hasBands ? Math.Clamp(input.AudioBass * gain, 0.0, 1.0) : level;
        double mid = hasBands ? Math.Clamp(input.AudioMid * gain, 0.0, 1.0) : level * 0.7;
        double treble = hasBands ? Math.Clamp(input.AudioTreble * gain, 0.0, 1.0) : level * 0.45;
        double beat = Math.Clamp(input.AudioBeatPulse, 0.0, 1.0);
        double transient = Math.Clamp(input.AudioTrebleTransient, 0.0, 1.0);
        double centroid = Math.Clamp(input.AudioCentroid, 0.0, 1.0);
        double energy = Math.Clamp(Math.Max(input.AudioEnergy, level), 0.0, 1.0);
        double vocal = Math.Clamp(input.AudioVocal * (0.85 + sensNorm * 0.45), 0.0, 1.0);
        double bassPunch = input.AudioBassPunch > 0.0
            ? Math.Clamp(input.AudioBassPunch * (0.80 + sensNorm * 0.35), 0.0, 1.0)
            : Math.Max(beat, Math.Pow(bass, 1.45));
        double dynFactor = input.AudioDynamic > 0.0
            ? Math.Clamp(input.AudioDynamic * (0.72 + sensNorm * 0.45), 0.0, 1.0)
            : Math.Clamp(Math.Pow(level, 1.25) * 0.70 + beat * 0.50, 0.0, 1.0);

        var c0 = LightingEffectSpec.ParseRgb(spec.Palette[0]);
        var c1 = spec.Palette.Length > 1 ? LightingEffectSpec.ParseRgb(spec.Palette[1]) : c0;
        var c2 = spec.Palette.Length > 2 ? LightingEffectSpec.ParseRgb(spec.Palette[2]) : c1;
        var cLast = LightingEffectSpec.ParseRgb(spec.Palette[^1]);

        // Hold resting baseline cleanly with zero synthetic LFO motion during true silence.
        if (input.AudioVibe == AudioMusicVibe.Silent && level <= 0.025 && bass <= 0.025 && mid <= 0.025 && treble <= 0.025 && beat <= 0.025 && transient <= 0.025)
            return new(c0.R, c0.G, c0.B, minB);

        LightingFrame frame = spec.AudioStyle switch
        {
            AudioReactiveStyle.AutoAdaptive => EvaluateAutoAdaptive(spec, sec, input, minB, maxB, c0, c1, c2, cLast, bass, mid, treble, beat, transient, centroid, energy, vocal, bassPunch, dynFactor, level),
            AudioReactiveStyle.BeatShift => EvaluateBeatShift(spec, sec, input, minB, maxB, c0, bass, mid, beat, transient, dynFactor),
            AudioReactiveStyle.BassFire => EvaluateBassFire(spec, sec, minB, maxB, c0, bassPunch, bass, beat, treble, transient, dynFactor),
            AudioReactiveStyle.AuroraDrift => EvaluateAuroraDrift(sec, input, minB, maxB, bass, mid, treble, beat, centroid, dynFactor),
            AudioReactiveStyle.DropStrobe => EvaluateDropStrobe(spec, sec, maxB, minB, c0, c1, c2, cLast, mid, beat, centroid, bass, treble, dynFactor),
            _ => EvaluateSpectrumFlow(spec, sec, input, minB, maxB, bass, mid, treble, beat, transient, centroid, energy, dynFactor),
        };
        if (spec.BeatFlashBoost && (beat > 0.55 || transient > 0.55))
        {
            var flashCol = spec.Palette.Length > 1 ? c1 : (R: (byte)255, G: (byte)255, B: (byte)255);
            var boosted = LerpRgb((frame.R, frame.G, frame.B), flashCol, 0.45);
            return new(boosted.R, boosted.G, boosted.B, maxB);
        }
        return frame;

        static LightingFrame EvaluateDropStrobe(LightingEffectSpec spec, double sec, byte maxB, byte minB, (byte R, byte G, byte B) c0, (byte R, byte G, byte B) c1, (byte R, byte G, byte B) c2, (byte R, byte G, byte B) cLast, double mid, double beat, double centroid, double bass, double treble, double dynFactor)
        {
            bool drop = beat > 0.36 || (bass + treble) > 1.15;
            int toggle = ((int)(sec * 1000.0 / Math.Max(60, spec.StepIntervalMs))) & 1;
            (byte R, byte G, byte B) col = drop
                ? (toggle == 0 ? cLast : c1)
                : LerpHsv(c0, c2, Math.Clamp(mid * 0.7 + centroid * 0.3, 0.0, 1.0));
            double bri = drop ? 1.0 : Math.Clamp(dynFactor * 0.44, 0.0, 1.0);
            return new(col.R, col.G, col.B, LerpByte(minB, maxB, bri));
        }
    }

    static LightingFrame EvaluateAutoAdaptive(
        LightingEffectSpec spec,
        double sec,
        LightingInputState input,
        byte minB,
        byte maxB,
        (byte R, byte G, byte B) c0,
        (byte R, byte G, byte B) c1,
        (byte R, byte G, byte B) c2,
        (byte R, byte G, byte B) cLast,
        double bass,
        double mid,
        double treble,
        double beat,
        double transient,
        double centroid,
        double energy,
        double vocal,
        double bassPunch,
        double dynFactor,
        double level)
    {
        switch (input.AudioVibe)
        {
            case AudioMusicVibe.Silent:
                if (level <= 0.02 && beat <= 0.02 && vocal <= 0.02)
                    return new(c0.R, c0.G, c0.B, minB);
                return EvaluateSpectrumFlow(spec, sec, input, minB, maxB, bass, mid, treble, beat, transient, centroid, energy, dynFactor);

            case AudioMusicVibe.AmbientChill:
            {
                // Silky soft aurora drift anchored on lower brightness headroom until a real swell arrives.
                var softMax = LerpByte(minB, maxB, Math.Clamp(0.25 + dynFactor * 0.45, 0.0, 0.72));
                return EvaluateAuroraDrift(sec, input, minB, Math.Max(minB, softMax), bass, mid, treble, beat, centroid, dynFactor * 0.72);
            }

            case AudioMusicVibe.VocalAcoustic:
            {
                // Lyrical spotlight: warm saturated golden/rose/aqua sweeps led by vocal presence and centroid.
                double hue = (22.0 + centroid * 185.0 + vocal * 55.0 + sec * 10.0) % 360.0;
                var lyricalCol = spec.Palette.Length >= 3
                    ? SamplePaletteHsv(spec.Palette, Math.Clamp(0.15 + vocal * 0.65 + centroid * 0.25, 0.0, 1.0))
                    : HsvToRgb(hue, 0.96, 1.0);
                double lyricBri = Math.Clamp(Math.Max(vocal * 0.92, Math.Pow(dynFactor, 1.25) * 0.72 + beat * 0.22), 0.0, 1.0);
                return new(lyricalCol.R, lyricalCol.G, lyricalCol.B, LerpByte(minB, maxB, lyricBri));
            }

            case AudioMusicVibe.ElectronicDrop:
            {
                // Club drop precision: crisp palette jumps on kicks, complementary hi-hat sparkles, deep valleys between kicks.
                return EvaluateBeatShift(spec, sec, input, minB, maxB, c0, bass, mid, beat, transient, Math.Max(dynFactor, beat));
            }

            case AudioMusicVibe.RockMetal:
            {
                // Transient-reactive ember & flare: dark glow on sustain, blazing flash on drum & riff impacts.
                return EvaluateBassFire(spec, sec, minB, maxB, c0, bassPunch, bass, beat, treble, transient, dynFactor);
            }

            case AudioMusicVibe.GroovePop:
            default:
                return EvaluateSpectrumFlow(spec, sec, input, minB, maxB, bass, mid, treble, beat, transient, centroid, energy, dynFactor);
        }
    }

    static LightingFrame EvaluateBeatShift(
        LightingEffectSpec spec,
        double sec,
        LightingInputState input,
        byte minB,
        byte maxB,
        (byte R, byte G, byte B) c0,
        double bass,
        double mid,
        double beat,
        double transient,
        double dynFactor)
    {
        (byte R, byte G, byte B) baseCol;
        if (spec.Palette.Length > 1)
        {
            int idx = Math.Abs(input.AudioBeatCount) % spec.Palette.Length;
            baseCol = LightingEffectSpec.ParseRgb(spec.Palette[idx]);
        }
        else
        {
            var shifted = HsvToRgb((input.AudioBeatCount * 115.0 + sec * 10.0) % 360.0, 1.0, 1.0);
            baseCol = input.AudioBeatCount == 0 ? c0 : shifted;
        }
        var col = transient > 0.58 && beat < 0.30
            ? HsvToRgb((RgbToHue(baseCol) + 150.0) % 360.0, 1.0, 1.0)
            : baseCol;
        double bassDrive = Math.Pow(bass, 1.45) * 0.72 + mid * 0.30;
        double punchEnergy = Math.Clamp(Math.Max(beat, bassDrive * (0.35 + 0.65 * dynFactor)), 0.0, 1.0);
        return new(col.R, col.G, col.B, LerpByte(minB, maxB, punchEnergy));
    }

    static LightingFrame EvaluateBassFire(
        LightingEffectSpec spec,
        double sec,
        byte minB,
        byte maxB,
        (byte R, byte G, byte B) c0,
        double bassPunch,
        double bass,
        double beat,
        double treble,
        double transient,
        double dynFactor)
    {
        (byte R, byte G, byte B) deepEmber = (120, 10, 0);
        var cool = spec.Palette.Length > 1 ? c0 : deepEmber;
        (byte R, byte G, byte B) flame = (255, 72, 0);
        (byte R, byte G, byte B) flare = (255, 210, 16);
        double heat = Math.Clamp(Math.Pow(Math.Max(bassPunch, bass * 0.75), 1.25) * 0.78 + beat * 0.45, 0.0, 1.0);
        var ember = heat <= 0.55
            ? LerpHsv(cool, flame, heat / 0.55)
            : LerpHsv(flame, flare, (heat - 0.55) / 0.45);
        double crackle = Math.Clamp((heat * 0.78 + Math.Max(treble * 0.45, transient * 0.85) * 0.30 * (0.5 + 0.5 * Math.Sin(sec * 24.0))) * (0.28 + 0.72 * Math.Max(dynFactor, beat)), 0.0, 1.0);
        return new(ember.R, ember.G, ember.B, LerpByte(minB, maxB, crackle));
    }

    static LightingFrame EvaluateAuroraDrift(
        double sec,
        LightingInputState input,
        byte minB,
        byte maxB,
        double bass,
        double mid,
        double treble,
        double beat,
        double centroid,
        double dynFactor)
    {
        double hue = (sec * 22.0 + input.AudioBeatCount * 28.0 + centroid * 140.0 + mid * 85.0) % 360.0;
        double hue2 = (hue + 72.0 + treble * 95.0) % 360.0;
        var a = HsvToRgb(hue, 0.96, 1.0);
        var b = HsvToRgb(hue2, 0.95, 1.0);
        double mix = Math.Clamp(0.20 + treble * 0.80, 0.0, 1.0);
        var aurora = LerpHsv(a, b, mix);
        double baseBloom = Math.Max(Math.Pow(bass, 1.3) * 0.78, Math.Max(Math.Pow(mid, 1.2) * 0.65, beat * 0.92));
        double bloom = Math.Clamp(baseBloom * (0.28 + 0.72 * dynFactor), 0.04, 1.0);
        return new(aurora.R, aurora.G, aurora.B, LerpByte(minB, maxB, bloom));
    }

    static LightingFrame EvaluateSpectrumFlow(
        LightingEffectSpec spec,
        double sec,
        LightingInputState input,
        byte minB,
        byte maxB,
        double bass,
        double mid,
        double treble,
        double beat,
        double transient,
        double centroid,
        double energy,
        double dynFactor)
    {
        // Single unified 2-LED hardware strip: never average RGB into muddy pink.
        // 1) Sharpened winner-takes-all dominance across Bass / Mid / Treble in saturated HSV space.
        double wB = Math.Pow(bass + 0.04, 2.6);
        double wM = Math.Pow(mid + 0.04, 2.6) * 1.18;
        double wT = Math.Pow(treble + 0.04, 2.6) * 1.32;
        double wSum = wB + wM + wT;

        double domPos = wSum > 1e-6 ? (wM * 0.5 + wT * 1.0) / wSum : 0.5;
        double specPos = Math.Clamp(domPos * 0.72 + centroid * 0.28, 0.0, 1.0);

        (byte R, byte G, byte B) mainCol;
        if (spec.Palette.Length >= 3)
        {
            double shift = energy > 0.55 ? (Math.Abs(input.AudioBeatCount) % spec.Palette.Length) * 0.14 : 0.0;
            mainCol = SamplePaletteHsv(spec.Palette, (specPos + shift) % 1.0);
        }
        else
        {
            double calmBase = 195.0;
            double dynamicHue = (calmBase + specPos * 165.0 + sec * (energy * 14.0) + input.AudioBeatCount * 22.0) % 360.0;
            mainCol = HsvToRgb(dynamicHue, 1.0, 1.0);
        }

        // 2) Temporal instrument separation: flash sharp hi-hat/treble hits into contrasting pure hue right between bass kicks.
        if (transient > 0.46 && treble > bass * 0.65)
        {
            double sparkleHue = (RgbToHue(mainCol) + 135.0) % 360.0;
            mainCol = HsvToRgb(sparkleHue, 1.0, 1.0);
        }

        // 3) High-contrast dynamics so quiet sections dim cleanly and drops punch vividly.
        double shapedBody = Math.Max(Math.Pow(bass, 1.45) * 0.85, Math.Pow(mid, 1.35) * 0.72) * (0.28 + 0.72 * dynFactor);
        double kickEnergy = Math.Clamp(Math.Max(beat * 0.98, Math.Max(shapedBody, transient * 0.90)), 0.0, 1.0);
        return new(mainCol.R, mainCol.G, mainCol.B, LerpByte(minB, maxB, kickEnergy));
    }

    static double RgbToHue((byte R, byte G, byte B) rgb)
    {
        double r = rgb.R / 255.0, g = rgb.G / 255.0, b = rgb.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double d = max - min;
        if (d < 1e-5) return 0.0;
        double h = max == r ? (g - b) / d + (g < b ? 6.0 : 0.0)
            : max == g ? (b - r) / d + 2.0
            : (r - g) / d + 4.0;
        return h * 60.0;
    }

    static (byte R, byte G, byte B) LerpHsv((byte R, byte G, byte B) a, (byte R, byte G, byte B) b, double t)
    {
        double k = Math.Clamp(t, 0.0, 1.0);
        double ha = RgbToHue(a), hb = RgbToHue(b);
        double dh = hb - ha;
        if (dh > 180.0) dh -= 360.0;
        else if (dh < -180.0) dh += 360.0;
        double h = (ha + dh * k + 360.0) % 360.0;

        double maxA = Math.Max(a.R, Math.Max(a.G, a.B)) / 255.0;
        double minA = Math.Min(a.R, Math.Min(a.G, a.B)) / 255.0;
        double satA = maxA > 1e-4 ? (maxA - minA) / maxA : 0.0;

        double maxB = Math.Max(b.R, Math.Max(b.G, b.B)) / 255.0;
        double minB = Math.Min(b.R, Math.Min(b.G, b.B)) / 255.0;
        double satB = maxB > 1e-4 ? (maxB - minB) / maxB : 0.0;

        double sat = satA + (satB - satA) * k;
        double val = maxA + (maxB - maxA) * k;
        return HsvToRgb(h, sat, val);
    }

    static (byte R, byte G, byte B) SamplePaletteHsv(string[] palette, double t)
    {
        if (palette.Length == 1) return LightingEffectSpec.ParseRgb(palette[0]);
        double wrapped = ((t % 1.0) + 1.0) % 1.0;
        double scaled = wrapped * (palette.Length - 1);
        int i0 = Math.Clamp((int)Math.Floor(scaled), 0, palette.Length - 1);
        int i1 = Math.Clamp(i0 + 1, 0, palette.Length - 1);
        double frac = scaled - Math.Floor(scaled);
        return LerpHsv(LightingEffectSpec.ParseRgb(palette[i0]), LightingEffectSpec.ParseRgb(palette[i1]), frac);
    }
}
