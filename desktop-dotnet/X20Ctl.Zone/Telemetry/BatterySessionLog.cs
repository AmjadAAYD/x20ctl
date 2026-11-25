using System.Text.Json;

namespace X20Ctl.Zone.Telemetry;

public sealed record BatterySample(
    DateTimeOffset Utc,
    string ModelId,
    int? LevelOf4,
    int PercentEstimate,
    bool Charging,
    bool EcoMode,
    int RgbBrightnessPct,
    bool UniversalHapticsActive,
    string Transport);

public sealed record BatterySessionSummary(
    int SampleCount,
    double SessionMinutes,
    double EcoSharePct,
    double AvgRgbBrightnessPct,
    double HapticsSharePct,
    double? EstimatedDrainPctPerHour,
    string Headline);

/// <summary>
/// Ring-buffered battery & power-draw telemetry log correlating battery % over time
/// with active Eco mode, RGB brightness %, and Universal Haptics load.
/// </summary>
public sealed class BatterySessionLog(string? path = null)
{
    public const int MaxSamples = 500;
    readonly string filePath = string.IsNullOrWhiteSpace(path) ? DefaultPath : Path.GetFullPath(path);
    readonly List<BatterySample> samples = new();
    readonly object gate = new();
    bool loaded;

    public static string DefaultPath => AppDataPaths.Local("telemetry", "battery-log.json");

    public IReadOnlyList<BatterySample> Samples
    {
        get { lock (gate) { EnsureLoaded(); return samples.ToArray(); } }
    }

    public static (int? LevelOf4, int Percent, bool Charging)? ParseLevel(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        string s = text.Trim().ToLowerInvariant();
        if (s is "wired" or "receiver" or "not reported" or "not reported over receiver") return null;
        bool charging = s.Contains("charging");
        if (s.Contains("/4"))
        {
            var idx = s.IndexOf("/4", StringComparison.Ordinal);
            if (idx > 0 && int.TryParse(s[..idx].Split(' ', '(').LastOrDefault(), out int lvl))
            {
                lvl = Math.Clamp(lvl, 0, 4);
                return (lvl, lvl * 25, charging);
            }
        }
        if (s.Contains('%'))
        {
            string digits = new string(s.TakeWhile(c => char.IsDigit(c)).ToArray());
            if (int.TryParse(digits, out int pct))
            {
                pct = Math.Clamp(pct, 0, 100);
                return ((int)Math.Ceiling(pct / 25.0), pct, charging);
            }
        }
        return s switch
        {
            "full" => (4, 100, charging),
            "medium" or "high" => (3, 75, charging),
            "low" => (1, 25, charging),
            "empty" => (0, 5, charging),
            _ => null,
        };
    }

    public void Load()
    {
        lock (gate)
        {
            loaded = true;
            samples.Clear();
            if (!File.Exists(filePath)) return;
            try
            {
                var parsed = JsonSerializer.Deserialize<List<BatterySample>>(File.ReadAllBytes(filePath));
                if (parsed != null)
                    samples.AddRange(parsed.Where(x => x != null).TakeLast(MaxSamples));
            }
            catch (Exception ex) when (ex is JsonException or IOException) { }
        }
    }

    public bool Append(BatterySample sample)
    {
        lock (gate)
        {
            EnsureLoaded();
            if (samples.Count > 0)
            {
                var last = samples[^1];
                if (Math.Abs((sample.Utc - last.Utc).TotalSeconds) < 30
                    && last.PercentEstimate == sample.PercentEstimate
                    && last.EcoMode == sample.EcoMode
                    && last.RgbBrightnessPct == sample.RgbBrightnessPct
                    && last.UniversalHapticsActive == sample.UniversalHapticsActive)
                {
                    return false;
                }
            }
            samples.Add(sample);
            if (samples.Count > MaxSamples)
                samples.RemoveRange(0, samples.Count - MaxSamples);
            WriteLocked();
            return true;
        }
    }

    public void Clear()
    {
        lock (gate)
        {
            loaded = true;
            samples.Clear();
            WriteLocked();
        }
    }

    public BatterySessionSummary Summarize()
    {
        lock (gate)
        {
            EnsureLoaded();
            if (samples.Count == 0)
                return new(0, 0, 0, 0, 0, null, "No battery telemetry samples logged yet.");

            double mins = Math.Max(0, (samples[^1].Utc - samples[0].Utc).TotalMinutes);
            double ecoPct = samples.Count(s => s.EcoMode) * 100.0 / samples.Count;
            double avgRgb = samples.Average(s => s.RgbBrightnessPct);
            double hapPct = samples.Count(s => s.UniversalHapticsActive) * 100.0 / samples.Count;

            double? drainPerHour = null;
            var discharging = samples.Where(s => !s.Charging).ToArray();
            if (discharging.Length >= 2)
            {
                double hours = (discharging[^1].Utc - discharging[0].Utc).TotalHours;
                int drop = discharging[0].PercentEstimate - discharging[^1].PercentEstimate;
                if (hours >= 0.05 && drop > 0)
                    drainPerHour = Math.Round(drop / hours, 1);
            }

            string headline = drainPerHour is { } rate
                ? $"{samples.Count} samples over {mins:0} min · Est. drain {rate:0.1}%/hr · Eco {ecoPct:0}% · Avg RGB {avgRgb:0}%"
                : $"{samples.Count} samples over {mins:0} min · Eco {ecoPct:0}% · Avg RGB {avgRgb:0}% · Haptics {hapPct:0}%";
            return new(samples.Count, mins, ecoPct, avgRgb, hapPct, drainPerHour, headline);
        }
    }

    void EnsureLoaded() { if (!loaded) Load(); }

    void WriteLocked()
    {
        string dir = Path.GetDirectoryName(filePath)!;
        Directory.CreateDirectory(dir);
        string tmp = filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllBytes(tmp, JsonSerializer.SerializeToUtf8Bytes(samples, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(tmp, filePath, true);
        }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }
}
