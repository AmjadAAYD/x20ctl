using System.Text.Json;
using System.Text.Json.Serialization;
using X20Ctl.Zone.Lighting;

namespace X20Ctl.Zone;

public readonly record struct VibrationPatternStep(int LeftGrip, int RightGrip, int LeftTrigger, int RightTrigger)
{
    public static VibrationPatternStep Zero => new(0, 0, 0, 0);

    public VibrationPatternStep Normalized() => new(
        Math.Clamp(LeftGrip, 0, 100),
        Math.Clamp(RightGrip, 0, 100),
        Math.Clamp(LeftTrigger, 0, 100),
        Math.Clamp(RightTrigger, 0, 100));
}

public sealed record VibrationPattern
{
    public const int DefaultSteps = 8;
    public const int MinStepMs = 50;
    public const int MaxStepMs = 500;

    public string Id { get; init; } = "custom-draft";
    public string Name { get; init; } = "Custom Pattern";
    public string Category { get; init; } = "Custom";
    public string Description { get; init; } = "Unique 4-motor haptic choreography";
    public int StepMs { get; init; } = 120;
    public bool Loop { get; init; } = true;
    public bool SmoothRamp { get; init; }
    public VibrationPatternStep[] Steps { get; init; } = new VibrationPatternStep[DefaultSteps];
    public bool BuiltIn { get; init; }

    public double TotalDurationMs => Math.Max(1, (Steps?.Length ?? DefaultSteps)) * Math.Clamp(StepMs, MinStepMs, MaxStepMs);

    public VibrationPattern Normalized()
    {
        var raw = Steps ?? [];
        VibrationPatternStep[] clean;
        if (raw.Length == 0)
        {
            clean = new VibrationPatternStep[DefaultSteps];
        }
        else
        {
            int count = Math.Clamp(raw.Length, 4, 16);
            clean = new VibrationPatternStep[count];
            for (int i = 0; i < count; i++)
                clean[i] = raw[i].Normalized();
        }

        string cleanName = string.IsNullOrWhiteSpace(Name) ? "Custom Pattern" : Name.Trim();
        if (cleanName.Length > 48) cleanName = cleanName[..48];

        return this with
        {
            Id = string.IsNullOrWhiteSpace(Id) ? Guid.NewGuid().ToString("N") : Id.Trim(),
            Name = cleanName,
            Category = string.IsNullOrWhiteSpace(Category) ? "Custom" : Category.Trim(),
            Description = Description ?? string.Empty,
            StepMs = Math.Clamp(StepMs, MinStepMs, MaxStepMs),
            Steps = clean,
        };
    }
}

/// <summary>
/// Pure deterministic 4-motor custom vibration pattern library and interpolator for Left/Right Grips and Triggers.
/// </summary>
public static class VibrationPatternEngine
{
    public static IReadOnlyList<VibrationPattern> BuiltIns { get; } =
    [
        new VibrationPattern
        {
            Id = "builtin-heartbeat",
            Name = "Heartbeat Pulse",
            Category = "Signature",
            Description = "Crisp trigger lub followed by deep chest grip dub and silent rest.",
            StepMs = 105,
            Loop = true,
            SmoothRamp = false,
            BuiltIn = true,
            Steps =
            [
                new(15, 10, 92, 85),
                new(95, 78, 0, 0),
                new(28, 18, 0, 0),
                new(0, 0, 0, 0),
                new(12, 8, 72, 65),
                new(76, 60, 0, 0),
                new(0, 0, 0, 0),
                new(0, 0, 0, 0),
            ],
        },
        new VibrationPattern
        {
            Id = "builtin-engine-rev",
            Name = "Engine Rev",
            Category = "Racing",
            Description = "Torque climb through the grips, crisp gearshift cutout, then redline trigger purr.",
            StepMs = 120,
            Loop = true,
            SmoothRamp = true,
            BuiltIn = true,
            Steps =
            [
                new(24, 18, 22, 16),
                new(46, 36, 40, 32),
                new(72, 58, 62, 54),
                new(96, 86, 88, 92),
                new(20, 15, 95, 20),
                new(58, 52, 54, 48),
                new(88, 82, 82, 88),
                new(34, 24, 28, 22),
            ],
        },
        new VibrationPattern
        {
            Id = "builtin-rainstorm",
            Name = "Rainstorm Ripple",
            Category = "Ambient",
            Description = "Alternating fingertip raindrops on LT/RT over distant rolling grip thunder.",
            StepMs = 135,
            Loop = true,
            SmoothRamp = false,
            BuiltIn = true,
            Steps =
            [
                new(18, 0, 58, 0),
                new(0, 22, 0, 68),
                new(0, 0, 48, 0),
                new(44, 32, 0, 56),
                new(66, 52, 64, 0),
                new(34, 48, 0, 52),
                new(0, 16, 46, 0),
                new(0, 0, 0, 62),
            ],
        },
        new VibrationPattern
        {
            Id = "builtin-sword-parry",
            Name = "Sword Parry",
            Category = "Combat",
            Description = "Razor-sharp dual trigger steel impact decaying through the grips.",
            StepMs = 75,
            Loop = false,
            SmoothRamp = false,
            BuiltIn = true,
            Steps =
            [
                new(40, 40, 100, 100),
                new(20, 92, 68, 95),
                new(86, 56, 38, 42),
                new(52, 28, 0, 25),
                new(24, 12, 0, 0),
                new(0, 0, 0, 0),
                new(0, 0, 0, 0),
                new(0, 0, 0, 0),
            ],
        },
        new VibrationPattern
        {
            Id = "builtin-cinema-drop",
            Name = "Cinema Bass Drop",
            Category = "Cinema",
            Description = "Rising trigger flutter, dramatic dead stop, then a full 4-motor cannon drop.",
            StepMs = 115,
            Loop = false,
            SmoothRamp = true,
            BuiltIn = true,
            Steps =
            [
                new(14, 14, 35, 35),
                new(26, 26, 58, 58),
                new(44, 44, 88, 88),
                new(0, 0, 0, 0),
                new(100, 100, 100, 100),
                new(88, 84, 78, 78),
                new(52, 48, 42, 42),
                new(18, 16, 0, 0),
            ],
        },
        new VibrationPattern
        {
            Id = "builtin-spatial-sweep",
            Name = "360 Spatial Sweep",
            Category = "Motion",
            Description = "Orbiting wave circling Left Grip -> Left Trigger -> Right Trigger -> Right Grip.",
            StepMs = 110,
            Loop = true,
            SmoothRamp = true,
            BuiltIn = true,
            Steps =
            [
                new(92, 10, 22, 0),
                new(54, 0, 88, 18),
                new(12, 0, 92, 58),
                new(0, 18, 30, 94),
                new(0, 68, 0, 84),
                new(12, 94, 0, 24),
                new(48, 62, 0, 0),
                new(82, 28, 16, 0),
            ],
        },
        new VibrationPattern
        {
            Id = "builtin-mech-reload",
            Name = "Mech Reload",
            Category = "Tactical",
            Description = "Crisp mag eject click, slide pull, trigger lock, and chamber thud.",
            StepMs = 85,
            Loop = false,
            SmoothRamp = false,
            BuiltIn = true,
            Steps =
            [
                new(0, 24, 88, 0),
                new(34, 0, 0, 0),
                new(0, 0, 0, 0),
                new(56, 38, 48, 0),
                new(0, 0, 0, 92),
                new(90, 84, 78, 82),
                new(32, 24, 0, 0),
                new(0, 0, 0, 0),
            ],
        },
        new VibrationPattern
        {
            Id = "builtin-zen-breath",
            Name = "Zen Breath",
            Category = "Calm",
            Description = "Gentle slow breathing wave rising softly across grips and triggers to rest.",
            StepMs = 210,
            Loop = true,
            SmoothRamp = true,
            BuiltIn = true,
            Steps =
            [
                new(0, 0, 0, 0),
                new(22, 20, 26, 26),
                new(48, 44, 52, 52),
                new(64, 58, 66, 66),
                new(46, 42, 48, 48),
                new(20, 18, 22, 22),
                new(0, 0, 0, 0),
                new(0, 0, 0, 0),
            ],
        },
    ];

    public static VibrationPattern CreateBlankDraft() => new VibrationPattern
    {
        Id = "custom-draft",
        Name = "My Custom Wave",
        Category = "Custom",
        Description = "Click cells to sculpt each motor step.",
        StepMs = 115,
        Loop = true,
        SmoothRamp = false,
        BuiltIn = false,
        Steps =
        [
            new(70, 20, 40, 0),
            new(35, 70, 0, 40),
            new(0, 35, 80, 0),
            new(80, 80, 0, 80),
            new(40, 0, 60, 60),
            new(0, 40, 0, 90),
            new(90, 90, 70, 70),
            new(0, 0, 0, 0),
        ],
    }.Normalized();

    public static HapticsFrame EvaluateAt(
        VibrationPattern rawPattern,
        double elapsedMs,
        int masterStrengthPercent,
        out int stepIndex,
        out bool finished)
    {
        var pattern = rawPattern.Normalized();
        double gain = Math.Clamp(masterStrengthPercent, 0, 100) / 100.0;
        if (gain <= 0.0 || elapsedMs < 0.0)
        {
            stepIndex = 0;
            finished = false;
            return HapticsFrame.Zero;
        }

        int count = pattern.Steps.Length;
        double stepDur = pattern.StepMs;
        double totalDur = count * stepDur;

        if (!pattern.Loop && elapsedMs >= totalDur)
        {
            stepIndex = count - 1;
            finished = true;
            return HapticsFrame.Zero;
        }

        finished = false;
        double localMs = pattern.Loop ? elapsedMs % totalDur : elapsedMs;
        double pos = localMs / stepDur;
        int i0 = Math.Clamp((int)Math.Floor(pos), 0, count - 1);
        double frac = pos - i0;
        stepIndex = i0;

        var s0 = pattern.Steps[i0];
        double lg, rg, lt, rt;
        if (pattern.SmoothRamp)
        {
            var s1 = i0 + 1 < count
                ? pattern.Steps[i0 + 1]
                : (pattern.Loop ? pattern.Steps[0] : VibrationPatternStep.Zero);
            lg = Lerp(s0.LeftGrip, s1.LeftGrip, frac);
            rg = Lerp(s0.RightGrip, s1.RightGrip, frac);
            lt = Lerp(s0.LeftTrigger, s1.LeftTrigger, frac);
            rt = Lerp(s0.RightTrigger, s1.RightTrigger, frac);
        }
        else
        {
            lg = s0.LeftGrip;
            rg = s0.RightGrip;
            lt = s0.LeftTrigger;
            rt = s0.RightTrigger;
        }

        static double ScaleGrip(double rawPct, double master)
        {
            double v = Math.Clamp(rawPct / 100.0 * master, 0.0, 1.0);
            return v >= 0.04 ? v : 0.0;
        }

        static double ScaleTrigger(double rawPct, double master)
        {
            double v = Math.Clamp(rawPct / 100.0 * master, 0.0, 1.0);
            if (v < 0.04) return 0.0;
            // Overcome trigger ERM stiction so deliberate subtle trigger taps still spin cleanly.
            return Math.Clamp(0.24 + 0.76 * v, 0.0, 1.0);
        }

        return new HapticsFrame(
            ScaleGrip(lg, gain),
            ScaleGrip(rg, gain),
            ScaleTrigger(lt, gain),
            ScaleTrigger(rt, gain));
    }

    static double Lerp(int a, int b, double t) => a + (b - a) * Math.Clamp(t, 0.0, 1.0);
}

/// <summary>Atomic local JSON persistence for user-created 4-motor custom vibration patterns.</summary>
public sealed class VibrationPatternStore
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private sealed class Library
    {
        public int Version { get; set; } = 1;
        public List<VibrationPattern> Patterns { get; set; } = [];
    }

    private readonly string path;
    private Library data = new();
    private bool loaded;

    public VibrationPatternStore(string path) => this.path = Path.GetFullPath(path);

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "x20ctl", "vibration", "patterns.json");

    public IReadOnlyList<VibrationPattern> Patterns
    {
        get
        {
            EnsureLoaded();
            return data.Patterns.AsReadOnly();
        }
    }

    public void Load()
    {
        loaded = true;
        if (!File.Exists(path)) { data = new(); return; }
        var parsed = JsonSerializer.Deserialize<Library>(File.ReadAllBytes(path), JsonOptions);
        if (parsed == null || parsed.Version != 1)
            throw new InvalidDataException("Unsupported vibration pattern library version.");
        data = new Library
        {
            Version = 1,
            Patterns = (parsed.Patterns ?? [])
                .Where(p => p != null && !string.IsNullOrWhiteSpace(p.Id) && !string.IsNullOrWhiteSpace(p.Name))
                .Select(p => (p with { BuiltIn = false }).Normalized())
                .Take(64)
                .ToList(),
        };
    }

    public VibrationPattern SavePattern(string name, VibrationPattern pattern, string? existingId = null)
    {
        EnsureLoaded();
        string cleanName = NormalizeName(name);
        var norm = (pattern with { Name = cleanName, BuiltIn = false, Category = "Custom" }).Normalized();
        VibrationPattern saved;
        if (!string.IsNullOrWhiteSpace(existingId) && data.Patterns.FindIndex(p => p.Id == existingId) is >= 0 and int idx)
        {
            saved = norm with { Id = existingId };
            data.Patterns[idx] = saved;
        }
        else
        {
            if (data.Patterns.Count >= 64) throw new InvalidOperationException("Custom pattern limit (64) reached.");
            saved = norm with { Id = "user-" + Guid.NewGuid().ToString("N")[..10] };
            data.Patterns.Add(saved);
        }
        Save();
        return saved;
    }

    public bool DeletePattern(string id)
    {
        EnsureLoaded();
        int removed = data.Patterns.RemoveAll(p => p.Id == id);
        if (removed == 0) return false;
        Save();
        return true;
    }

    public VibrationPattern? FindPattern(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        EnsureLoaded();
        return data.Patterns.FirstOrDefault(p => p.Id == id)
            ?? VibrationPatternEngine.BuiltIns.FirstOrDefault(p => p.Id == id);
    }

    static string NormalizeName(string name)
    {
        if (name == null) throw new InvalidDataException("Pattern name is missing.");
        string trimmed = name.Trim();
        if (trimmed.Length is < 1 or > 48 || trimmed.Any(char.IsControl))
            throw new ArgumentException("Use a pattern name of 1-48 printable characters.");
        return trimmed;
    }

    void EnsureLoaded() { if (!loaded) Load(); }

    void Save()
    {
        string dir = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(dir);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(data, JsonOptions);
        string temp = Path.Combine(dir, ".vibration-patterns-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(true);
            }
            File.Move(temp, path, true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }
}
