using System.Reflection;
using System.Text.Json;

namespace X20Ctl.Zone.Lighting;

public readonly record struct AudioClassification(
    float Vocal,
    float Bass,
    float Drums,
    float Instruments,
    float Gunshot,
    float Explosion,
    float GravelSurface,
    float EngineRumble,
    float BoostSurge,
    float TireScreech,
    float MeleeClash,
    float Dialogue);

/// <summary>
/// Zero-allocation pure C# inference engine for embedded <c>HapticsModelWeights.json</c>.
/// Evaluates the 28-feature vector from <see cref="AudioSpectrumAnalyzer"/> in under 0.2 ms
/// with asymmetric per-head temporal smoothing so transients hit crisply without jitter.
/// </summary>
public sealed class HapticsAudioClassifier
{
    public const int FeatureCount = 28;
    public const int HeadCount = 12;
    const int MaxHidden = 128;

    sealed class LoadedWeights
    {
        public required int FeatureCount { get; init; }
        public required int HiddenUnits { get; init; }
        public required float[] W1Flat { get; init; }
        public required float[] B1 { get; init; }
        public required float[] W2Flat { get; init; }
        public required float[] B2 { get; init; }
    }

    static readonly Lazy<LoadedWeights?> EmbeddedWeights = new(LoadEmbeddedWeights);

    readonly LoadedWeights? weights;
    readonly object gate = new();
    readonly float[] smoothHeads = new float[HeadCount];

    // Fast transient attacks for impacts; calmer hysteresis for surface & dialogue states.
    static readonly double[] AttackTau = [0.022, 0.025, 0.014, 0.030, 0.008, 0.015, 0.045, 0.065, 0.040, 0.030, 0.010, 0.075];
    static readonly double[] ReleaseTau = [0.095, 0.130, 0.085, 0.125, 0.065, 0.240, 0.110, 0.180, 0.160, 0.105, 0.085, 0.260];

    public HapticsAudioClassifier()
    {
        weights = EmbeddedWeights.Value;
    }

    public bool UsingEmbeddedWeights => weights != null;

    public static void ExtractFeatures(AudioFrame frame, Span<float> dst)
    {
        if (dst.Length < FeatureCount)
            throw new ArgumentException($"Destination span must have length >= {FeatureCount}.", nameof(dst));

        dst[0] = Clamp01(frame.Spectrum.Sub);
        dst[1] = Clamp01(frame.Spectrum.Kick);
        dst[2] = Clamp01(frame.Spectrum.LowMid);
        dst[3] = Clamp01(frame.Spectrum.VocalLow);
        dst[4] = Clamp01(frame.Spectrum.VocalHigh);
        dst[5] = Clamp01(frame.Spectrum.Presence);
        dst[6] = Clamp01(frame.Spectrum.Treble);
        dst[7] = Clamp01(frame.Spectrum.Air);
        dst[8] = Clamp01(frame.Level);
        dst[9] = Clamp01(frame.Bass);
        dst[10] = Clamp01(frame.Mid);
        dst[11] = Clamp01(frame.Treble);
        dst[12] = Clamp01(frame.BeatPulse);
        dst[13] = Clamp01(frame.TrebleTransient);
        dst[14] = Clamp01(frame.SpectralCentroid);
        dst[15] = Clamp01(frame.Energy);
        dst[16] = Clamp01(frame.VocalPresence);
        dst[17] = Clamp01(frame.VocalWarmth);
        dst[18] = Clamp01(frame.VocalAir);
        dst[19] = Clamp01(frame.InstrumentEnergy);
        dst[20] = Clamp01(frame.BassPunch);
        dst[21] = Clamp01(frame.DynamicContrast);
        dst[22] = Clamp01(frame.SpectralFlatness);
        dst[23] = Clamp01(frame.SpectralFlux);
        dst[24] = Clamp01(frame.MidSideCoherence);
        dst[25] = Clamp01(frame.HighFreqResonance);
        dst[26] = Clamp01(frame.CrestFactor);
        dst[27] = Clamp01(frame.CentroidVelocity * 0.5f + 0.5f);
    }

    public void Reset()
    {
        lock (gate)
            Array.Clear(smoothHeads);
    }

    public AudioClassification Classify(AudioFrame frame, double dtSeconds = 0.020)
    {
        double dt = double.IsFinite(dtSeconds) && dtSeconds > 0 ? Math.Clamp(dtSeconds, 0.002, 0.25) : 0.020;

        Span<float> feat = stackalloc float[FeatureCount];
        ExtractFeatures(frame, feat);

        Span<float> raw = stackalloc float[HeadCount];
        EvaluateRaw(feat, frame, raw);

        lock (gate)
        {
            for (int i = 0; i < HeadCount; i++)
            {
                double tau = raw[i] > smoothHeads[i] ? AttackTau[i] : ReleaseTau[i];
                double alpha = 1.0 - Math.Exp(-dt / tau);
                float next = (float)(smoothHeads[i] + (raw[i] - smoothHeads[i]) * alpha);
                smoothHeads[i] = next < 0.005f ? 0f : Math.Clamp(next, 0f, 1f);
            }

            return new AudioClassification(
                Vocal: smoothHeads[0],
                Bass: smoothHeads[1],
                Drums: smoothHeads[2],
                Instruments: smoothHeads[3],
                Gunshot: smoothHeads[4],
                Explosion: smoothHeads[5],
                GravelSurface: smoothHeads[6],
                EngineRumble: smoothHeads[7],
                BoostSurge: smoothHeads[8],
                TireScreech: smoothHeads[9],
                MeleeClash: smoothHeads[10],
                Dialogue: smoothHeads[11]);
        }
    }

    void EvaluateRaw(ReadOnlySpan<float> feat, AudioFrame frame, Span<float> rawOut)
    {
        // Hard silence floor so idle soundcards never produce phantom probabilities.
        if (feat[8] < 0.015f)
        {
            rawOut.Clear();
            return;
        }

        if (weights is { } w && w.FeatureCount == FeatureCount && w.HiddenUnits is > 0 and <= MaxHidden)
        {
            int H = w.HiddenUnits;
            Span<float> hidden = stackalloc float[MaxHidden];
            for (int h = 0; h < H; h++)
            {
                float sum = w.B1[h];
                int row = h * FeatureCount;
                for (int f = 0; f < FeatureCount; f++)
                    sum += w.W1Flat[row + f] * feat[f];
                hidden[h] = sum > 0f ? sum : 0f;
            }

            for (int o = 0; o < HeadCount; o++)
            {
                float logit = w.B2[o];
                int row = o * H;
                for (int h = 0; h < H; h++)
                    logit += w.W2Flat[row + h] * hidden[h];
                rawOut[o] = Sigmoid(logit);
            }
            return;
        }

        EvaluateFallbackHeuristic(frame, rawOut);
    }

    static void EvaluateFallbackHeuristic(AudioFrame f, Span<float> dst)
    {
        dst[0] = Clamp01(f.VocalPresence * 0.9f);
        dst[1] = Clamp01(f.Bass * 0.85f);
        dst[2] = Clamp01(Math.Max(f.BeatPulse, f.TrebleTransient * 0.8f));
        dst[3] = Clamp01(f.InstrumentEnergy);
        dst[4] = Clamp01(f.SpectralFlux * 0.65f + f.CrestFactor * 0.45f);
        dst[5] = Clamp01(f.Spectrum.Sub * 0.65f + f.SpectralFlatness * 0.35f);
        dst[6] = Clamp01(f.SpectralFlatness * 0.75f + f.Mid * 0.25f);
        dst[7] = Clamp01(f.Spectrum.LowMid * 0.7f * f.MidSideCoherence);
        dst[8] = Clamp01(Math.Max(0f, f.CentroidVelocity) * 0.8f + f.Spectrum.Sub * 0.25f);
        dst[9] = Clamp01(f.HighFreqResonance * 0.9f);
        dst[10] = Clamp01(f.HighFreqResonance * 0.5f + f.TrebleTransient * 0.5f);
        dst[11] = Clamp01(f.VocalPresence * (1f - f.BeatPulse) * 0.7f);
    }

    static float Sigmoid(float x)
    {
        float clamped = Math.Clamp(x, -40f, 40f);
        return 1f / (1f + MathF.Exp(-clamped));
    }

    static float Clamp01(float v) => float.IsFinite(v) ? Math.Clamp(v, 0f, 1f) : 0f;

    sealed class WeightsJsonDto
    {
        public int version { get; set; }
        public int featureCount { get; set; }
        public int hiddenUnits { get; set; }
        public float[][]? w1 { get; set; }
        public float[]? b1 { get; set; }
        public float[][]? w2 { get; set; }
        public float[]? b2 { get; set; }
    }

    static LoadedWeights? LoadEmbeddedWeights()
    {
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            using var stream = asm.GetManifestResourceStream("X20Ctl.Zone.Lighting.HapticsModelWeights.json");
            if (stream == null) return null;

            var dto = JsonSerializer.Deserialize<WeightsJsonDto>(stream);
            if (dto?.w1 == null || dto.b1 == null || dto.w2 == null || dto.b2 == null)
                return null;
            if (dto.featureCount != FeatureCount || dto.hiddenUnits <= 0 || dto.hiddenUnits > MaxHidden)
                return null;
            if (dto.w1.Length != dto.hiddenUnits || dto.b1.Length != dto.hiddenUnits
                || dto.w2.Length != HeadCount || dto.b2.Length != HeadCount)
                return null;

            var w1Flat = new float[dto.hiddenUnits * FeatureCount];
            for (int h = 0; h < dto.hiddenUnits; h++)
            {
                if (dto.w1[h].Length != FeatureCount) return null;
                Array.Copy(dto.w1[h], 0, w1Flat, h * FeatureCount, FeatureCount);
            }

            var w2Flat = new float[HeadCount * dto.hiddenUnits];
            for (int o = 0; o < HeadCount; o++)
            {
                if (dto.w2[o].Length != dto.hiddenUnits) return null;
                Array.Copy(dto.w2[o], 0, w2Flat, o * dto.hiddenUnits, dto.hiddenUnits);
            }

            return new LoadedWeights
            {
                FeatureCount = FeatureCount,
                HiddenUnits = dto.hiddenUnits,
                W1Flat = w1Flat,
                B1 = dto.b1,
                W2Flat = w2Flat,
                B2 = dto.b2,
            };
        }
        catch (Exception)
        {
            return null;
        }
    }
}
