namespace X20Ctl.Zone.Lighting;

/// <summary>
/// Smoothed peak audio envelope tracker (0..1) for audio-reactive RGB lighting.
/// Fast attack (~60 ms) captures transients while smooth release (~200 ms) avoids harsh flicker;
/// decays to zero if no sample arrives within the idle timeout (500 ms).
/// Inspired by DualSenseClient's AudioLevelMeter (shazzaam7, GPL-3.0).
/// </summary>
public sealed class AudioLevelMeter
{
    public const double AttackSeconds = 0.06;
    public const double ReleaseSeconds = 0.20;
    public const double IdleSeconds = 0.50;

    readonly TimeProvider time;
    volatile float level;
    long lastProcessTimestamp;

    public AudioLevelMeter(TimeProvider? timeProvider = null)
    {
        time = timeProvider ?? TimeProvider.System;
    }

    public float Level => IsStale(Volatile.Read(ref lastProcessTimestamp)) ? 0f : level;

    public void Reset()
    {
        level = 0f;
        Volatile.Write(ref lastProcessTimestamp, 0);
    }

    public void Process(ReadOnlySpan<float> interleavedSamples, double durationSeconds)
    {
        float peak = 0f;
        foreach (float sample in interleavedSamples)
        {
            if (!float.IsFinite(sample)) continue;
            float abs = MathF.Abs(sample);
            if (abs > peak) peak = abs;
        }
        ProcessPeak(peak, durationSeconds);
    }

    public void ProcessPeak(float rawPeak, double durationSeconds)
    {
        long prev = Volatile.Read(ref lastProcessTimestamp);
        if (IsStale(prev)) level = 0f;

        Volatile.Write(ref lastProcessTimestamp, time.GetTimestamp());

        float peak = float.IsFinite(rawPeak) ? Math.Clamp(rawPeak, 0f, 1f) : 0f;
        double dt = durationSeconds > 0 && double.IsFinite(durationSeconds) ? durationSeconds : 0.02;
        double tau = peak > level ? AttackSeconds : ReleaseSeconds;
        double coefficient = 1.0 - Math.Exp(-dt / tau);
        double next = level + (peak - level) * coefficient;
        level = (float)Math.Clamp(next, 0.0, 1.0);
    }

    bool IsStale(long lastTimestamp) =>
        lastTimestamp != 0 && time.GetElapsedTime(lastTimestamp).TotalSeconds > IdleSeconds;
}
