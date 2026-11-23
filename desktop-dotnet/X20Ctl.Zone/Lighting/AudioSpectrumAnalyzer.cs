namespace X20Ctl.Zone.Lighting;

/// <summary>
/// Live multi-band audio analysis snapshot produced by <see cref="AudioSpectrumAnalyzer"/>.
/// </summary>
public readonly record struct AudioFrame(
    float Level,
    float Bass,
    float Mid,
    float Treble,
    float BeatPulse,
    int BeatCount,
    float StereoPan);

/// <summary>
/// Zero-allocation, thread-safe 512-point Hann-windowed Radix-2 FFT spectral analyzer
/// splitting live audio into Bass (20-280 Hz), Mid (280-2000 Hz), and Treble (2000-14000 Hz),
/// with adaptive auto-gain normalization (AGC) and spectral-flux beat onset detection.
/// </summary>
public sealed class AudioSpectrumAnalyzer
{
    public const int FftSize = 512;
    public const double IdleSeconds = 0.50;
    public const double BeatCooldownSeconds = 0.16;

    static readonly float[] HannWindow = BuildHannWindow(FftSize);
    static readonly int[] BitReverseTable = BuildBitReverseTable(FftSize);
    static readonly (float[] Cos, float[] Sin)[] StageTwiddles = BuildStageTwiddles(FftSize);

    readonly TimeProvider time;
    readonly object gate = new();
    readonly float[] monoRing = new float[FftSize];
    readonly float[] fftReal = new float[FftSize];
    readonly float[] fftImag = new float[FftSize];

    int ringPos;
    long lastProcessTimestamp;
    long lastBeatTimestamp;

    float agcBass = 0.04f;
    float agcMid = 0.04f;
    float agcTreble = 0.04f;
    float bassAvg;

    float smoothLevel;
    float smoothBass;
    float smoothMid;
    float smoothTreble;
    float beatPulse;
    int beatCount;
    float stereoPan;

    public AudioSpectrumAnalyzer(TimeProvider? timeProvider = null)
    {
        time = timeProvider ?? TimeProvider.System;
    }

    public AudioFrame CurrentFrame
    {
        get
        {
            lock (gate)
            {
                if (IsStale(lastProcessTimestamp))
                    return new AudioFrame(0f, 0f, 0f, 0f, 0f, beatCount, 0f);
                return new AudioFrame(smoothLevel, smoothBass, smoothMid, smoothTreble, beatPulse, beatCount, stereoPan);
            }
        }
    }

    public void Reset()
    {
        lock (gate)
        {
            Array.Clear(monoRing);
            ringPos = 0;
            lastProcessTimestamp = 0;
            lastBeatTimestamp = 0;
            agcBass = 0.04f;
            agcMid = 0.04f;
            agcTreble = 0.04f;
            bassAvg = 0f;
            smoothLevel = 0f;
            smoothBass = 0f;
            smoothMid = 0f;
            smoothTreble = 0f;
            beatPulse = 0f;
            beatCount = 0;
            stereoPan = 0f;
        }
    }

    public AudioFrame ProcessInterleaved(ReadOnlySpan<float> interleaved, int channels, int sampleRate, double durationSeconds)
    {
        int ch = Math.Max(1, channels);
        int sr = Math.Clamp(sampleRate, 8000, 384000);
        double dt = durationSeconds > 0 && double.IsFinite(durationSeconds)
            ? Math.Clamp(durationSeconds, 0.002, 0.25)
            : Math.Clamp((double)(interleaved.Length / ch) / sr, 0.004, 0.25);

        lock (gate)
        {
            long now = time.GetTimestamp();
            if (IsStale(lastProcessTimestamp))
            {
                smoothLevel = smoothBass = smoothMid = smoothTreble = beatPulse = 0f;
                bassAvg = 0f;
            }
            lastProcessTimestamp = now;

            int frameCount = interleaved.Length / ch;
            double sumMonoSq = 0;
            double sumLeftSq = 0;
            double sumRightSq = 0;

            for (int f = 0; f < frameCount; f++)
            {
                int baseIdx = f * ch;
                float left = Sanitize(interleaved[baseIdx]);
                float right = ch >= 2 ? Sanitize(interleaved[baseIdx + 1]) : left;

                double sum = 0;
                for (int c = 0; c < ch; c++)
                    sum += Sanitize(interleaved[baseIdx + c]);
                float mono = (float)(sum / ch);

                monoRing[ringPos] = mono;
                ringPos = (ringPos + 1) & (FftSize - 1);

                sumMonoSq += mono * mono;
                sumLeftSq += left * left;
                sumRightSq += right * right;
            }

            float rawRms = frameCount > 0 ? (float)Math.Sqrt(sumMonoSq / frameCount) : 0f;
            if (ch >= 2 && frameCount > 0)
            {
                float lRms = (float)Math.Sqrt(sumLeftSq / frameCount);
                float rRms = (float)Math.Sqrt(sumRightSq / frameCount);
                float lrSum = lRms + rRms;
                float targetPan = lrSum > 0.001f ? Math.Clamp((rRms - lRms) / lrSum, -1f, 1f) : 0f;
                stereoPan = LerpFloat(stereoPan, targetPan, dt, 0.12);
            }
            else
            {
                stereoPan = 0f;
            }

            ComputeBands(sr, out float rawBass, out float rawMid, out float rawTreble);
            UpdateEnvelopesAndBeats(now, dt, rawRms, rawBass, rawMid, rawTreble);

            return new AudioFrame(smoothLevel, smoothBass, smoothMid, smoothTreble, beatPulse, beatCount, stereoPan);
        }
    }

    public AudioFrame ProcessFallbackPeak(float rawPeak, double durationSeconds)
    {
        double dt = durationSeconds > 0 && double.IsFinite(durationSeconds)
            ? Math.Clamp(durationSeconds, 0.005, 0.50)
            : 0.065;

        lock (gate)
        {
            long now = time.GetTimestamp();
            if (IsStale(lastProcessTimestamp))
            {
                smoothLevel = smoothBass = smoothMid = smoothTreble = beatPulse = 0f;
                bassAvg = 0f;
            }
            lastProcessTimestamp = now;

            float peak = Sanitize(rawPeak);
            UpdateEnvelopesAndBeats(now, dt, peak * 0.7f, peak * 0.85f, peak * 0.65f, peak * 0.45f);
            return new AudioFrame(smoothLevel, smoothBass, smoothMid, smoothTreble, beatPulse, beatCount, 0f);
        }
    }

    void ComputeBands(int sampleRate, out float bass, out float mid, out float treble)
    {
        for (int i = 0; i < FftSize; i++)
        {
            int srcIdx = (ringPos + i) & (FftSize - 1);
            int dst = BitReverseTable[i];
            fftReal[dst] = monoRing[srcIdx] * HannWindow[i];
            fftImag[dst] = 0f;
        }

        // In-place iterative Cooley-Tukey Radix-2 FFT.
        for (int stage = 0; stage < StageTwiddles.Length; stage++)
        {
            int half = 1 << stage;
            int step = half << 1;
            float[] cosTable = StageTwiddles[stage].Cos;
            float[] sinTable = StageTwiddles[stage].Sin;

            for (int k = 0; k < FftSize; k += step)
            {
                for (int j = 0; j < half; j++)
                {
                    int even = k + j;
                    int odd = even + half;
                    float wr = cosTable[j];
                    float wi = sinTable[j];
                    float tr = wr * fftReal[odd] - wi * fftImag[odd];
                    float ti = wr * fftImag[odd] + wi * fftReal[odd];

                    fftReal[odd] = fftReal[even] - tr;
                    fftImag[odd] = fftImag[even] - ti;
                    fftReal[even] += tr;
                    fftImag[even] += ti;
                }
            }
        }

        double binHz = sampleRate / (double)FftSize;
        double scale = 4.0 / FftSize;

        double bassSq = 0, midSq = 0, trebleSq = 0;
        int bassN = 0, midN = 0, trebleN = 0;

        int maxBin = FftSize / 2;
        for (int bin = 1; bin < maxBin; bin++)
        {
            double freq = bin * binHz;
            if (freq < 20 || freq > 14000) continue;

            double re = fftReal[bin] * scale;
            double im = fftImag[bin] * scale;
            double magSq = re * re + im * im;

            if (freq < 280)
            {
                bassSq += magSq;
                bassN++;
            }
            else if (freq < 2000)
            {
                midSq += magSq;
                midN++;
            }
            else
            {
                trebleSq += magSq;
                trebleN++;
            }
        }

        // Scale per-band RMS into well-behaved 0..1 dynamic range before AGC.
        bass = bassN > 0 ? (float)Math.Sqrt(bassSq / bassN) : 0f;
        mid = midN > 0 ? (float)(Math.Sqrt(midSq / midN) * 1.4) : 0f;
        treble = trebleN > 0 ? (float)(Math.Sqrt(trebleSq / trebleN) * 2.2) : 0f;
    }

    void UpdateEnvelopesAndBeats(long now, double dt, float rawRms, float rawBass, float rawMid, float rawTreble)
    {
        bool audible = rawRms >= 0.0005f || rawBass >= 0.001f || rawMid >= 0.001f || rawTreble >= 0.001f;

        agcBass = UpdateAgc(agcBass, rawBass, dt, audible, 0.04f);
        agcMid = UpdateAgc(agcMid, rawMid, dt, audible, 0.035f);
        agcTreble = UpdateAgc(agcTreble, rawTreble, dt, audible, 0.03f);

        float normBass = audible ? Math.Clamp(rawBass / agcBass, 0f, 1f) : 0f;
        float normMid = audible ? Math.Clamp(rawMid / agcMid, 0f, 1f) : 0f;
        float normTreble = audible ? Math.Clamp(rawTreble / agcTreble, 0f, 1f) : 0f;
        float targetLevel = Math.Clamp(Math.Max(normBass * 0.55f + normMid * 0.30f + normTreble * 0.25f, Math.Max(normBass, normMid * 0.9f)), 0f, 1f);

        smoothBass = Smooth(smoothBass, normBass, dt, 0.032, 0.16);
        smoothMid = Smooth(smoothMid, normMid, dt, 0.040, 0.17);
        smoothTreble = Smooth(smoothTreble, normTreble, dt, 0.030, 0.14);
        smoothLevel = Smooth(smoothLevel, targetLevel, dt, 0.040, 0.18);

        double avgAlpha = 1.0 - Math.Exp(-dt / 0.35);
        double threshold = Math.Max(0.012, bassAvg * 1.38);
        double sinceBeat = lastBeatTimestamp == 0 ? 10.0 : time.GetElapsedTime(lastBeatTimestamp, now).TotalSeconds;

        if (audible && rawBass > threshold && sinceBeat >= BeatCooldownSeconds)
        {
            beatPulse = 1.0f;
            beatCount++;
            lastBeatTimestamp = now;
        }
        else
        {
            beatPulse *= (float)Math.Exp(-dt / 0.13);
            if (beatPulse < 0.005f) beatPulse = 0f;
        }

        bassAvg = (float)(bassAvg + (rawBass - bassAvg) * avgAlpha);
    }

    static float UpdateAgc(float currentAgc, float rawBand, double dt, bool audible, float floor)
    {
        if (rawBand > currentAgc)
            return Smooth(currentAgc, rawBand, dt, 0.08, 0.08);
        float decayTau = audible ? 4.5f : 1.5f;
        return Math.Max(floor, Smooth(currentAgc, Math.Max(floor, rawBand), dt, decayTau, decayTau));
    }

    static float Smooth(float current, float target, double dt, double attackSec, double releaseSec)
    {
        double tau = target > current ? attackSec : releaseSec;
        double alpha = 1.0 - Math.Exp(-dt / Math.Max(0.005, tau));
        return Math.Clamp((float)(current + (target - current) * alpha), 0f, 1f);
    }

    static float LerpFloat(float current, float target, double dt, double tau)
    {
        double alpha = 1.0 - Math.Exp(-dt / Math.Max(0.005, tau));
        return (float)(current + (target - current) * alpha);
    }

    static float Sanitize(float v) => float.IsFinite(v) ? Math.Clamp(v, -1f, 1f) : 0f;

    bool IsStale(long lastTimestamp) =>
        lastTimestamp != 0 && time.GetElapsedTime(lastTimestamp).TotalSeconds > IdleSeconds;

    static float[] BuildHannWindow(int n)
    {
        var w = new float[n];
        for (int i = 0; i < n; i++)
            w[i] = (float)(0.5 * (1.0 - Math.Cos(2.0 * Math.PI * i / (n - 1))));
        return w;
    }

    static int[] BuildBitReverseTable(int n)
    {
        int bits = (int)Math.Log2(n);
        var rev = new int[n];
        for (int i = 0; i < n; i++)
        {
            int x = i;
            int y = 0;
            for (int b = 0; b < bits; b++)
            {
                y = (y << 1) | (x & 1);
                x >>= 1;
            }
            rev[i] = y;
        }
        return rev;
    }

    static (float[] Cos, float[] Sin)[] BuildStageTwiddles(int n)
    {
        int stages = (int)Math.Log2(n);
        var table = new (float[] Cos, float[] Sin)[stages];
        for (int s = 0; s < stages; s++)
        {
            int half = 1 << s;
            int step = half << 1;
            var cos = new float[half];
            var sin = new float[half];
            for (int j = 0; j < half; j++)
            {
                double angle = -2.0 * Math.PI * j / step;
                cos[j] = (float)Math.Cos(angle);
                sin[j] = (float)Math.Sin(angle);
            }
            table[s] = (cos, sin);
        }
        return table;
    }
}
