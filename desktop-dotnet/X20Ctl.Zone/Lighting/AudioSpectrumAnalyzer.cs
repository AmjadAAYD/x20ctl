namespace X20Ctl.Zone.Lighting;

public enum AudioMusicVibe
{
    Silent,
    AmbientChill,
    VocalAcoustic,
    GroovePop,
    ElectronicDrop,
    RockMetal,
}

public readonly record struct AudioSpectrum8(
    float Sub = 0f,
    float Kick = 0f,
    float LowMid = 0f,
    float VocalLow = 0f,
    float VocalHigh = 0f,
    float Presence = 0f,
    float Treble = 0f,
    float Air = 0f)
{
    public float this[int index] => index switch
    {
        0 => Sub,
        1 => Kick,
        2 => LowMid,
        3 => VocalLow,
        4 => VocalHigh,
        5 => Presence,
        6 => Treble,
        7 => Air,
        _ => 0f,
    };
}

/// <summary>
/// Live multi-band stereo audio analysis snapshot produced by <see cref="AudioSpectrumAnalyzer"/>.
/// </summary>
public readonly record struct AudioFrame(
    float Level,
    float Bass,
    float Mid,
    float Treble,
    float BeatPulse,
    int BeatCount,
    float StereoPan,
    float LeftLevel = 0f,
    float RightLevel = 0f,
    float SpectralCentroid = 0.5f,
    float Energy = 0f,
    float TrebleTransient = 0f,
    float VocalPresence = 0f,
    float VocalWarmth = 0f,
    float VocalAir = 0f,
    float InstrumentEnergy = 0f,
    float BassPunch = 0f,
    float DynamicContrast = 0f,
    AudioMusicVibe Vibe = AudioMusicVibe.Silent,
    AudioSpectrum8 Spectrum = default,
    float SpectralFlatness = 0f,
    float SpectralFlux = 0f,
    float MidSideCoherence = 0f,
    float CrestFactor = 0f,
    float HighFreqResonance = 0f,
    float CentroidVelocity = 0f,
    float RawRms = 0f,
    float RawPeakSpike = 0f);

/// <summary>
/// Zero-allocation, thread-safe 1024-point Hann-windowed Radix-2 Mid-Side dual-FFT analyzer
/// separating 8 musical frequency bands, coherence-gated harmonic vocals, dynamic punch, and music vibe.
/// </summary>
public sealed class AudioSpectrumAnalyzer
{
    public const int FftSize = 1024;
    public const double IdleSeconds = 0.25;
    public const double BeatCooldownSeconds = 0.16;

    static readonly double MaxLogSpan = Math.Log2(12000.0 / 20.0);
    static readonly float[] HannWindow = BuildHannWindow(FftSize);
    static readonly int[] BitReverseTable = BuildBitReverseTable(FftSize);
    static readonly (float[] Cos, float[] Sin)[] StageTwiddles = BuildStageTwiddles(FftSize);

    readonly TimeProvider time;
    readonly object gate = new();
    readonly float[] monoRing = new float[FftSize];
    readonly float[] sideRing = new float[FftSize];
    readonly float[] fftReal = new float[FftSize];
    readonly float[] fftImag = new float[FftSize];
    readonly double[] midMag = new double[FftSize / 2];
    readonly double[] sideMag = new double[FftSize / 2];
    readonly float[] agcBands = [0.04f, 0.04f, 0.04f, 0.035f, 0.035f, 0.03f, 0.028f, 0.025f];
    readonly float[] smoothBands = new float[8];
    readonly float[] prevBandNorm = new float[8];

    int ringPos;
    long lastProcessTimestamp;
    long lastBeatTimestamp;
    long lastTrebleTimestamp;
    long lastVibeSwitchTimestamp;

    float trackRef = 0.05f;
    float midVocalRef = 0.04f;
    float agcBass = 0.04f;
    float agcMid = 0.04f;
    float agcTreble = 0.04f;
    float agcVocal = 0.03f;
    float agcInst = 0.035f;
    float agcStereo = 0.04f;
    float bassAvg;
    float trebleAvg;
    float bassFloor;
    float levelFloor;
    float prevCenterRms;
    float prevCentroid = 0.5f;

    float smoothLevel;
    float smoothBass;
    float smoothMid;
    float smoothTreble;
    float smoothLeft;
    float smoothRight;
    float smoothCentroid = 0.5f;
    float smoothEnergy;
    float smoothVocal;
    float smoothVocalWarm;
    float smoothVocalAir;
    float smoothInst;
    float smoothBassPunch;
    float smoothDynamic;
    float smoothFlatness;
    float smoothFlux;
    float smoothCoherence;
    float smoothCrest;
    float smoothHighResonance;
    float smoothCentroidVel;
    float smoothRawRms;
    float rawRmsFloor;
    float smoothRawSpike;
    float beatPulse;
    float trebleTransient;
    int beatCount;
    float stereoPan;

    float vibeEnergy;
    float vibeBass;
    float vibeVocal;
    float vibeInst;
    float vibeTreble;
    float beatRateHz;
    AudioMusicVibe currentVibe = AudioMusicVibe.Silent;

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
                    return new AudioFrame(0f, 0f, 0f, 0f, 0f, beatCount, 0f, 0f, 0f, 0.5f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, AudioMusicVibe.Silent);
                return SnapshotLocked();
            }
        }
    }

    AudioSpectrum8 SnapshotSpectrumLocked() => new(
        smoothBands[0],
        smoothBands[1],
        smoothBands[2],
        smoothBands[3],
        smoothBands[4],
        smoothBands[5],
        smoothBands[6],
        smoothBands[7]);

    AudioFrame SnapshotLocked() => new(
        smoothLevel,
        smoothBass,
        smoothMid,
        smoothTreble,
        beatPulse,
        beatCount,
        stereoPan,
        smoothLeft,
        smoothRight,
        smoothCentroid,
        smoothEnergy,
        trebleTransient,
        smoothVocal,
        smoothVocalWarm,
        smoothVocalAir,
        smoothInst,
        smoothBassPunch,
        smoothDynamic,
        currentVibe,
        SnapshotSpectrumLocked(),
        smoothFlatness,
        smoothFlux,
        smoothCoherence,
        smoothCrest,
        smoothHighResonance,
        smoothCentroidVel,
        smoothRawRms,
        smoothRawSpike);

    public void Reset()
    {
        lock (gate)
        {
            Array.Clear(monoRing);
            Array.Clear(sideRing);
            Array.Clear(smoothBands);
            Array.Clear(prevBandNorm);
            agcBands[0] = agcBands[1] = agcBands[2] = 0.04f;
            agcBands[3] = agcBands[4] = 0.035f;
            agcBands[5] = 0.03f;
            agcBands[6] = 0.028f;
            agcBands[7] = 0.025f;
            ringPos = 0;
            lastProcessTimestamp = 0;
            lastBeatTimestamp = 0;
            lastTrebleTimestamp = 0;
            lastVibeSwitchTimestamp = 0;
            trackRef = 0.05f;
            midVocalRef = 0.04f;
            agcBass = 0.04f;
            agcMid = 0.04f;
            agcTreble = 0.04f;
            agcVocal = 0.03f;
            agcInst = 0.035f;
            agcStereo = 0.04f;
            bassAvg = 0f;
            trebleAvg = 0f;
            bassFloor = 0f;
            levelFloor = 0f;
            prevCenterRms = 0f;
            prevCentroid = 0.5f;
            smoothLevel = 0f;
            smoothBass = 0f;
            smoothMid = 0f;
            smoothTreble = 0f;
            smoothLeft = 0f;
            smoothRight = 0f;
            smoothCentroid = 0.5f;
            smoothEnergy = 0f;
            smoothVocal = 0f;
            smoothVocalWarm = 0f;
            smoothVocalAir = 0f;
            smoothInst = 0f;
            smoothBassPunch = 0f;
            smoothDynamic = 0f;
            smoothFlatness = 0f;
            smoothFlux = 0f;
            smoothCoherence = 0f;
            smoothCrest = 0f;
            smoothHighResonance = 0f;
            smoothCentroidVel = 0f;
            smoothRawRms = 0f;
            rawRmsFloor = 0f;
            smoothRawSpike = 0f;
            beatPulse = 0f;
            trebleTransient = 0f;
            beatCount = 0;
            stereoPan = 0f;
            vibeEnergy = 0f;
            vibeBass = 0f;
            vibeVocal = 0f;
            vibeInst = 0f;
            vibeTreble = 0f;
            beatRateHz = 0f;
            currentVibe = AudioMusicVibe.Silent;
        }
    }

    /// <summary>
    /// Advances silence decay immediately when WASAPI loopback yields zero packets, preventing stale frozen bars or haptics.
    /// </summary>
    public AudioFrame TickSilence(double durationSeconds)
    {
        double dt = durationSeconds > 0 && double.IsFinite(durationSeconds)
            ? Math.Clamp(durationSeconds, 0.005, 0.25)
            : 0.025;

        lock (gate)
        {
            long now = time.GetTimestamp();
            lastProcessTimestamp = now;

            static float DecayZero(float v, double dtSec, double tauSec)
            {
                float next = v * (float)Math.Exp(-dtSec / tauSec);
                return next < 0.005f ? 0f : next;
            }

            smoothLevel = DecayZero(smoothLevel, dt, 0.045);
            smoothBass = DecayZero(smoothBass, dt, 0.045);
            smoothMid = DecayZero(smoothMid, dt, 0.045);
            smoothTreble = DecayZero(smoothTreble, dt, 0.040);
            smoothLeft = DecayZero(smoothLeft, dt, 0.045);
            smoothRight = DecayZero(smoothRight, dt, 0.045);
            smoothVocal = DecayZero(smoothVocal, dt, 0.038);
            smoothVocalWarm = DecayZero(smoothVocalWarm, dt, 0.038);
            smoothVocalAir = DecayZero(smoothVocalAir, dt, 0.035);
            smoothInst = DecayZero(smoothInst, dt, 0.045);
            smoothBassPunch = DecayZero(smoothBassPunch, dt, 0.040);
            smoothDynamic = DecayZero(smoothDynamic, dt, 0.045);
            smoothFlatness = DecayZero(smoothFlatness, dt, 0.045);
            smoothFlux = DecayZero(smoothFlux, dt, 0.035);
            smoothCoherence = DecayZero(smoothCoherence, dt, 0.045);
            smoothCrest = DecayZero(smoothCrest, dt, 0.040);
            smoothHighResonance = DecayZero(smoothHighResonance, dt, 0.040);
            smoothCentroidVel = DecayZero(smoothCentroidVel, dt, 0.040);
            smoothRawRms = DecayZero(smoothRawRms, dt, 0.050);
            rawRmsFloor = DecayZero(rawRmsFloor, dt, 0.250);
            smoothRawSpike = DecayZero(smoothRawSpike, dt, 0.045);
            beatPulse = DecayZero(beatPulse, dt, 0.040);
            trebleTransient = DecayZero(trebleTransient, dt, 0.035);
            smoothEnergy = DecayZero(smoothEnergy, dt, 0.22);
            prevCenterRms = DecayZero(prevCenterRms, dt, 0.045);
            stereoPan = DecayZero(stereoPan, dt, 0.08);
            bassFloor = DecayZero(bassFloor, dt, 0.25);
            levelFloor = DecayZero(levelFloor, dt, 0.25);
            beatRateHz = DecayZero(beatRateHz, dt, 0.40);

            for (int i = 0; i < smoothBands.Length; i++)
            {
                smoothBands[i] = DecayZero(smoothBands[i], dt, 0.045);
                prevBandNorm[i] = DecayZero(prevBandNorm[i], dt, 0.045);
            }

            if (smoothLevel < 0.04f)
            {
                vibeEnergy = vibeBass = vibeVocal = vibeInst = vibeTreble = 0f;
                currentVibe = AudioMusicVibe.Silent;
            }
            if (smoothLevel < 0.01f)
            {
                Array.Clear(monoRing);
                Array.Clear(sideRing);
            }

            return SnapshotLocked();
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
            bool firstOrResumed = lastProcessTimestamp == 0 || IsStale(lastProcessTimestamp);
            if (firstOrResumed)
            {
                Array.Clear(smoothBands);
                Array.Clear(prevBandNorm);
                smoothLevel = smoothBass = smoothMid = smoothTreble = 0f;
                smoothLeft = smoothRight = smoothEnergy = beatPulse = trebleTransient = 0f;
                smoothVocal = smoothVocalWarm = smoothVocalAir = smoothInst = smoothBassPunch = smoothDynamic = 0f;
                smoothFlatness = smoothFlux = smoothCoherence = smoothCrest = smoothHighResonance = smoothCentroidVel = 0f;
                bassAvg = trebleAvg = bassFloor = levelFloor = prevCenterRms = 0f;
                prevCentroid = 0.5f;
                vibeEnergy = vibeBass = vibeVocal = vibeInst = vibeTreble = beatRateHz = 0f;
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
                float side = ch >= 2 ? (left - right) * 0.5f : 0f;

                monoRing[ringPos] = mono;
                sideRing[ringPos] = side;
                ringPos = (ringPos + 1) & (FftSize - 1);

                sumMonoSq += mono * mono;
                sumLeftSq += left * left;
                sumRightSq += right * right;
            }

            float rawRms = frameCount > 0 ? (float)Math.Sqrt(sumMonoSq / frameCount) : 0f;
            float leftRms = frameCount > 0 ? (float)Math.Sqrt(sumLeftSq / frameCount) : 0f;
            float rightRms = frameCount > 0 ? (float)Math.Sqrt(sumRightSq / frameCount) : 0f;

            if (ch >= 2 && frameCount > 0)
            {
                float lrSum = leftRms + rightRms;
                float targetPan = lrSum > 0.002f ? Math.Clamp((rightRms - leftRms) / lrSum, -1f, 1f) : 0f;
                stereoPan = LerpFloat(stereoPan, targetPan, dt, 0.09);
            }
            else
            {
                stereoPan = 0f;
            }

            Span<float> rawBands = stackalloc float[8];
            ComputeBands(sr, ch >= 2, out float rawBass, out float rawMid, out float rawTreble, out float rawVocalWarm, out float rawVocalAir, out float rawVocalClarity, out float rawInBandSide, out float rawInst, out float rawCentroid, out float rawFlatness, out float rawCoherence, out float rawCrest, out float rawHighResonance, rawBands);
            UpdateEnvelopesAndBeats(now, dt, rawRms, leftRms, rightRms, rawBass, rawMid, rawTreble, rawVocalWarm, rawVocalAir, rawVocalClarity, rawInBandSide, rawInst, rawCentroid, rawFlatness, rawCoherence, rawCrest, rawHighResonance, rawBands, firstOrResumed);
            return SnapshotLocked();
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
            bool firstOrResumed = lastProcessTimestamp == 0 || IsStale(lastProcessTimestamp);
            if (firstOrResumed)
            {
                Array.Clear(smoothBands);
                Array.Clear(prevBandNorm);
                smoothLevel = smoothBass = smoothMid = smoothTreble = 0f;
                smoothLeft = smoothRight = smoothEnergy = beatPulse = trebleTransient = 0f;
                smoothVocal = smoothVocalWarm = smoothVocalAir = smoothInst = smoothBassPunch = smoothDynamic = 0f;
                smoothFlatness = smoothFlux = smoothCoherence = smoothCrest = smoothHighResonance = smoothCentroidVel = 0f;
                bassAvg = trebleAvg = bassFloor = levelFloor = prevCenterRms = 0f;
                prevCentroid = 0.5f;
                vibeEnergy = vibeBass = vibeVocal = vibeInst = vibeTreble = beatRateHz = 0f;
            }
            lastProcessTimestamp = now;

            float peak = Sanitize(rawPeak);
            Span<float> rawBands = stackalloc float[8];
            rawBands[0] = peak * 0.70f;
            rawBands[1] = peak * 0.85f;
            rawBands[2] = peak * 0.70f;
            rawBands[3] = peak * 0.55f;
            rawBands[4] = peak * 0.45f;
            rawBands[5] = peak * 0.40f;
            rawBands[6] = peak * 0.35f;
            rawBands[7] = peak * 0.25f;
            UpdateEnvelopesAndBeats(now, dt, peak * 0.7f, peak * 0.7f, peak * 0.7f, peak * 0.85f, peak * 0.65f, peak * 0.45f, peak * 0.30f, peak * 0.25f, 0.65f, peak * 0.30f, peak * 0.70f, 0.45f, 0.25f, 0.75f, 0.25f, 0.10f, rawBands, firstOrResumed);
            return SnapshotLocked();
        }
    }

    void RunFft(float[] ring, double[] magOut)
    {
        for (int i = 0; i < FftSize; i++)
        {
            int srcIdx = (ringPos + i) & (FftSize - 1);
            int dst = BitReverseTable[i];
            fftReal[dst] = ring[srcIdx] * HannWindow[i];
            fftImag[dst] = 0f;
        }

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

        double scale = 4.0 / FftSize;
        int maxBin = FftSize / 2;
        for (int bin = 1; bin < maxBin; bin++)
        {
            double re = fftReal[bin] * scale;
            double im = fftImag[bin] * scale;
            magOut[bin] = Math.Sqrt(re * re + im * im);
        }
    }

    void ComputeBands(
        int sampleRate,
        bool isStereo,
        out float bass,
        out float mid,
        out float treble,
        out float vocalWarm,
        out float vocalAir,
        out float vocalClarity,
        out float inBandSide,
        out float instrument,
        out float centroidNorm,
        out float flatnessNorm,
        out float midSideCoherenceNorm,
        out float crestNorm,
        out float highResonanceNorm,
        Span<float> outBands)
    {
        RunFft(monoRing, midMag);
        if (isStereo)
            RunFft(sideRing, sideMag);
        else
            Array.Clear(sideMag);

        double binHz = sampleRate / (double)FftSize;

        double bassSq = 0, midSq = 0, trebleSq = 0;
        double vocalWarmSq = 0, vocalAirSq = 0, centerPowerSq = 0, inBandSideSq = 0;
        double instSideSq = 0, instSparkleSq = 0;
        double vocalSum = 0, vocalPeak = 0, sfmLogSum = 0, sfmArithSum = 0;
        double totalWeightSq = 0, logWeightedSum = 0;
        double fullSfmLogSum = 0, fullSfmArithSum = 0, fullMagSum = 0, fullMagMax = 0;
        double fullMidSum = 0, fullSideSum = 0;
        double hiResSum = 0, hiResMax = 0;
        int bassN = 0, midN = 0, trebleN = 0, vocalWarmN = 0, vocalAirN = 0, instSideN = 0, instSparkleN = 0;
        int fullBinsN = 0, hiResN = 0;

        Span<double> bandSq = stackalloc double[8];
        Span<int> bandN = stackalloc int[8];

        int maxBin = FftSize / 2;
        for (int bin = 1; bin < maxBin; bin++)
        {
            double freq = bin * binHz;
            if (freq < 20 || freq > 14000) continue;

            double mm = midMag[bin];
            double sm = isStereo ? sideMag[bin] : 0.0;
            double magSq = mm * mm + sm * sm;
            double totMag = Math.Sqrt(magSq);

            if (freq <= 12000)
            {
                double logPos = Math.Clamp(Math.Log2(freq / 20.0) / MaxLogSpan, 0.0, 1.0);
                logWeightedSum += logPos * magSq;
                totalWeightSq += magSq;

                fullSfmLogSum += Math.Log(magSq + 1e-10);
                fullSfmArithSum += magSq;
                fullMagSum += totMag;
                if (totMag > fullMagMax) fullMagMax = totMag;
                fullMidSum += mm;
                fullSideSum += sm;
                fullBinsN++;
            }

            if (freq >= 1800 && freq <= 4800)
            {
                hiResSum += totMag;
                if (totMag > hiResMax) hiResMax = totMag;
                hiResN++;
            }

            int bIdx = freq switch
            {
                < 85 => 0,
                < 220 => 1,
                < 450 => 2,
                < 1100 => 3,
                < 2800 => 4,
                < 5500 => 5,
                < 9500 => 6,
                _ => 7,
            };
            bandSq[bIdx] += magSq;
            bandN[bIdx]++;

            if (freq < 240)
            {
                bassSq += magSq;
                bassN++;
            }
            else if (freq < 2200)
            {
                midSq += magSq;
                midN++;
            }
            else
            {
                trebleSq += magSq;
                trebleN++;
            }

            if (freq >= 260 && freq <= 3600)
            {
                // Coherence-weighted soft Mid-Side mask: cancels hard-panned stereo instruments while preserving stereo-reverb vocals.
                double ratio = isStereo ? sm / (mm + 1e-6) : 0.0;
                double centerMask = isStereo ? Math.Clamp(1.0 - ratio * 1.18, 0.0, 1.0) : 1.0;
                double centerMag = mm * centerMask;
                double centerSq = centerMag * centerMag;
                double mmSq = mm * mm;

                // Measure harmonicity on unmasked Mid magnitude so random Mid-Side bin cancellation cannot fake spectral valleys.
                vocalSum += mm;
                if (mm > vocalPeak) vocalPeak = mm;
                centerPowerSq += centerSq;
                inBandSideSq += sm * sm * 2.0;
                sfmLogSum += Math.Log(mmSq + 1e-10);
                sfmArithSum += mmSq;

                if (freq < 1150)
                {
                    vocalWarmSq += centerSq;
                    vocalWarmN++;
                }
                else
                {
                    vocalAirSq += centerSq;
                    vocalAirN++;
                }
            }

            if (freq >= 160 && freq <= 8000 && isStereo)
            {
                instSideSq += sm * sm * 2.0;
                instSideN++;
            }
            if (freq > 3400)
            {
                instSparkleSq += magSq;
                instSparkleN++;
            }
        }

        double[] bandScales = [1.45, 1.65, 1.90, 2.15, 2.40, 2.75, 3.15, 3.60];
        for (int i = 0; i < 8; i++)
            outBands[i] = bandN[i] > 0 ? (float)(Math.Sqrt(bandSq[i] / bandN[i]) * bandScales[i]) : 0f;

        bass = bassN > 0 ? (float)(Math.Sqrt(bassSq / bassN) * 1.45) : 0f;
        mid = midN > 0 ? (float)(Math.Sqrt(midSq / midN) * 1.90) : 0f;
        treble = trebleN > 0 ? (float)(Math.Sqrt(trebleSq / trebleN) * 2.95) : 0f;

        int vocalTotalN = vocalWarmN + vocalAirN;
        double vocalMean = vocalTotalN > 0 ? vocalSum / vocalTotalN : 0.0;
        double crest = vocalMean > 1e-6 ? vocalPeak / vocalMean : 0.0;
        double geomMean = vocalTotalN > 0 ? Math.Exp(sfmLogSum / vocalTotalN) : 0.0;
        double arithMean = vocalTotalN > 0 ? sfmArithSum / vocalTotalN : 0.0;
        double sfm = arithMean > 1e-10 ? Math.Clamp(geomMean / arithMean, 0.0, 1.0) : 1.0;

        // Apply harmonicity & stereo center dominance AFTER normalization so AGC never boosts flat noise into fake singing.
        double harmonicity = Math.Clamp((0.34 - sfm) / 0.24, 0.0, 1.0);
        double crestScore = Math.Clamp((crest - 2.1) / 2.2, 0.0, 1.0);
        double tonality = Math.Clamp(harmonicity * (0.45 + 0.55 * crestScore), 0.0, 1.0);

        double vocalCenterRms = vocalTotalN > 0 ? Math.Sqrt(centerPowerSq / vocalTotalN) : 0.0;
        double inBandSideRms = vocalTotalN > 0 ? Math.Sqrt(inBandSideSq / vocalTotalN) : 0.0;
        double centerDominance = isStereo
            ? Math.Clamp((vocalCenterRms / (vocalCenterRms + inBandSideRms + 1e-6) - 0.34) / 0.40, 0.0, 1.0)
            : 1.0;

        double modBonus = Math.Clamp(1.0 + Math.Abs(vocalCenterRms - prevCenterRms) * 12.0, 1.0, 1.25);
        prevCenterRms = (float)vocalCenterRms;

        double bassSuppression = tonality < 0.35 && bass > mid * 3.5f ? 0.30 : 1.0;
        vocalClarity = (float)Math.Clamp(tonality * centerDominance * bassSuppression * modBonus, 0.0, 1.0);

        // Keep raw vocal band levels un-gated by clarity so agcVocal tracks true center vocal energy.
        vocalWarm = vocalWarmN > 0 ? (float)(Math.Sqrt(vocalWarmSq / vocalWarmN) * 2.55) : 0f;
        vocalAir = vocalAirN > 0 ? (float)(Math.Sqrt(vocalAirSq / vocalAirN) * 2.85) : 0f;
        inBandSide = (float)inBandSideRms;

        double instSideRms = instSideN > 0 ? Math.Sqrt(instSideSq / instSideN) : 0.0;
        double instSparkleRms = instSparkleN > 0 ? Math.Sqrt(instSparkleSq / instSparkleN) : 0.0;
        instrument = (float)Math.Sqrt(instSideRms * instSideRms * 1.65 + bass * bass * 0.25 + instSparkleRms * instSparkleRms * 1.15);

        centroidNorm = totalWeightSq > 1e-8 ? (float)Math.Clamp(logWeightedSum / totalWeightSq, 0.0, 1.0) : smoothCentroid;

        double fullGeom = fullBinsN > 0 ? Math.Exp(fullSfmLogSum / fullBinsN) : 0.0;
        double fullArith = fullBinsN > 0 ? fullSfmArithSum / fullBinsN : 0.0;
        flatnessNorm = fullArith > 1e-10 ? (float)Math.Clamp(fullGeom / fullArith, 0.0, 1.0) : 0f;

        midSideCoherenceNorm = !isStereo
            ? 1f
            : (fullMidSum + fullSideSum > 1e-6 ? (float)Math.Clamp(fullMidSum / (fullMidSum + fullSideSum), 0.0, 1.0) : 0f);

        double fullMeanMag = fullBinsN > 0 ? fullMagSum / fullBinsN : 0.0;
        crestNorm = fullMeanMag > 1e-6 ? (float)Math.Clamp((fullMagMax / fullMeanMag - 2.0) / 14.0, 0.0, 1.0) : 0f;

        double hiResMean = hiResN > 0 ? hiResSum / hiResN : 0.0;
        highResonanceNorm = hiResMean > 1e-6 ? (float)Math.Clamp((hiResMax / hiResMean - 2.5) / 10.0, 0.0, 1.0) : 0f;
    }

    void UpdateEnvelopesAndBeats(
        long now,
        double dt,
        float rawRms,
        float leftRms,
        float rightRms,
        float rawBass,
        float rawMid,
        float rawTreble,
        float rawVocalWarm,
        float rawVocalAir,
        float rawVocalClarity,
        float rawInBandSide,
        float rawInst,
        float rawCentroid,
        float rawFlatness,
        float rawCoherence,
        float rawCrest,
        float rawHighResonance,
        ReadOnlySpan<float> rawBands,
        bool firstOrResumed)
    {
        // Clean noise gate eliminates sub-audible DAC hum from triggering fake spectrum motion.
        bool audible = rawRms >= 0.0022f || leftRms >= 0.0022f || rightRms >= 0.0022f
            || rawBass >= 0.003f || rawMid >= 0.003f || rawTreble >= 0.003f;

        float unAgcRms = audible ? Math.Clamp(Math.Max(rawRms, Math.Max(leftRms, rightRms) * 0.90f), 0f, 1f) : 0f;
        if (firstOrResumed)
        {
            smoothRawRms = unAgcRms;
            rawRmsFloor = unAgcRms;
            smoothRawSpike = 0f;
        }
        else
        {
            smoothRawRms = Smooth(smoothRawRms, unAgcRms, dt, 0.015, 0.080);
            rawRmsFloor = LerpFloat(rawRmsFloor, unAgcRms, dt, 0.65);
            float spike = Math.Clamp(unAgcRms - rawRmsFloor * 1.15f, 0f, 1f);
            smoothRawSpike = Smooth(smoothRawSpike, spike, dt, 0.010, 0.075);
        }

        float rawPeakBand = Math.Max(rawRms * 1.25f, Math.Max(rawBass, Math.Max(rawMid, rawTreble)));
        trackRef = UpdateTrackRef(trackRef, rawPeakBand, dt, audible, 0.05f);
        midVocalRef = UpdateTrackRef(midVocalRef, Math.Max(rawMid, Math.Max(rawVocalWarm, rawVocalAir)), dt, audible, 0.035f);

        agcBass = UpdateAgc(agcBass, rawBass, dt, audible, 0.04f, decaySeconds: 9.0f);
        agcMid = UpdateAgc(agcMid, rawMid, dt, audible, 0.035f, decaySeconds: 9.0f);
        agcTreble = UpdateAgc(agcTreble, rawTreble, dt, audible, 0.03f, decaySeconds: 9.0f);
        agcVocal = UpdateAgc(agcVocal, Math.Max(rawVocalWarm, rawVocalAir), dt, audible, 0.028f, decaySeconds: 7.0f);
        agcInst = UpdateAgc(agcInst, rawInst, dt, audible, 0.035f, decaySeconds: 8.5f);
        agcStereo = UpdateAgc(agcStereo, Math.Max(leftRms, rightRms), dt, audible, 0.035f, decaySeconds: 4.5f);

        float fluxAccum = 0f;
        for (int i = 0; i < 8; i++)
        {
            agcBands[i] = UpdateAgc(agcBands[i], rawBands[i], dt, audible, i <= 2 ? 0.038f : 0.028f, decaySeconds: 8.0f);
            float bDenom = Math.Max(0.025f, trackRef * 0.45f + agcBands[i] * 0.55f);
            float normBand = audible ? Math.Clamp(rawBands[i] / bDenom, 0f, 1f) : 0f;
            float diff = normBand - prevBandNorm[i];
            if (diff > 0f) fluxAccum += diff;
            prevBandNorm[i] = normBand;
            smoothBands[i] = Smooth(smoothBands[i], normBand, dt, 0.020, 0.10);
        }
        float rawFlux = audible && !firstOrResumed ? Math.Clamp(fluxAccum / 2.2f, 0f, 1f) : 0f;
        smoothFlux = Smooth(smoothFlux, rawFlux, dt, 0.012, 0.070);
        smoothFlatness = Smooth(smoothFlatness, audible ? rawFlatness : 0f, dt, 0.025, 0.090);
        smoothCoherence = Smooth(smoothCoherence, audible ? rawCoherence : 0f, dt, 0.030, 0.100);
        smoothCrest = Smooth(smoothCrest, audible ? rawCrest : 0f, dt, 0.020, 0.090);
        smoothHighResonance = Smooth(smoothHighResonance, audible ? rawHighResonance : 0f, dt, 0.018, 0.085);

        float denomBass = Math.Max(0.03f, trackRef * 0.52f + agcBass * 0.48f);
        float denomMid = Math.Max(0.03f, trackRef * 0.52f + agcMid * 0.48f);
        float denomTreble = Math.Max(0.025f, trackRef * 0.48f + agcTreble * 0.52f);
        // Vocal denominator is decoupled from sub-bass trackRef so booming kicks don't swallow singing.
        float denomVocal = Math.Max(0.025f, midVocalRef * 0.42f + agcVocal * 0.58f);
        float denomInst = Math.Max(0.03f, trackRef * 0.50f + agcInst * 0.50f);

        float normBass = audible ? Math.Clamp(rawBass / denomBass, 0f, 1f) : 0f;
        float normMid = audible ? Math.Clamp(rawMid / denomMid, 0f, 1f) : 0f;
        float normTreble = audible ? Math.Clamp(rawTreble / denomTreble, 0f, 1f) : 0f;
        float normVocalWarm = audible ? Math.Clamp(rawVocalWarm / denomVocal, 0f, 1f) * rawVocalClarity : 0f;
        float normVocalAir = audible ? Math.Clamp(rawVocalAir / denomVocal, 0f, 1f) * rawVocalClarity : 0f;
        float normInst = audible ? Math.Clamp(rawInst / denomInst, 0f, 1f) : 0f;
        float normInBandSide = audible ? Math.Clamp(rawInBandSide / Math.Max(0.025f, midVocalRef * 0.50f + agcInst * 0.50f), 0f, 1f) : 0f;
        float normVocalRaw = Math.Max(normVocalWarm, normVocalAir * 0.92f);

        float normLeft = audible ? Math.Clamp(leftRms / agcStereo, 0f, 1f) : 0f;
        float normRight = audible ? Math.Clamp(rightRms / agcStereo, 0f, 1f) : 0f;
        float targetLevel = Math.Clamp(Math.Max(normBass * 0.50f + normMid * 0.30f + normTreble * 0.25f, Math.Max(normBass, normMid * 0.90f)), 0f, 1f);

        bassFloor = LerpFloat(bassFloor, normBass, dt, 0.75);
        levelFloor = LerpFloat(levelFloor, targetLevel, dt, 0.80);

        smoothBass = Smooth(smoothBass, normBass, dt, 0.024, 0.12);
        smoothMid = Smooth(smoothMid, normMid, dt, 0.028, 0.13);
        smoothTreble = Smooth(smoothTreble, normTreble, dt, 0.022, 0.11);
        smoothLeft = Smooth(smoothLeft, normLeft, dt, 0.028, 0.13);
        smoothRight = Smooth(smoothRight, normRight, dt, 0.028, 0.13);
        smoothLevel = Smooth(smoothLevel, targetLevel, dt, 0.028, 0.13);
        smoothEnergy = Smooth(smoothEnergy, targetLevel, dt, 0.45, 1.20);
        if (audible)
        {
            float nextCentroid = Math.Clamp(firstOrResumed ? rawCentroid : LerpFloat(smoothCentroid, rawCentroid, dt, 0.05), 0f, 1f);
            float rawVel = firstOrResumed ? 0f : Math.Clamp((nextCentroid - prevCentroid) / Math.Max(0.015f, (float)dt) * 0.25f, -1f, 1f);
            smoothCentroid = nextCentroid;
            prevCentroid = nextCentroid;
            smoothCentroidVel = LerpFloat(smoothCentroidVel, rawVel, dt, 0.06);
        }
        else
        {
            smoothCentroidVel = LerpFloat(smoothCentroidVel, 0f, dt, 0.06);
        }

        // Clean lyrical vocal gating: strips in-band stereo chord bleed while keeping centered singing vivid over bass.
        float bleed = normInBandSide * 0.22f + 0.04f;
        float cleanWarm = Math.Max(0f, normVocalWarm - bleed);
        float cleanAir = Math.Max(0f, normVocalAir - bleed * 0.90f);
        float cleanVocal = Math.Max(cleanWarm, cleanAir * 0.92f);
        smoothVocalWarm = Smooth(smoothVocalWarm, cleanWarm, dt, 0.020, 0.085);
        smoothVocalAir = Smooth(smoothVocalAir, cleanAir, dt, 0.018, 0.080);
        smoothVocal = Smooth(smoothVocal, cleanVocal, dt, 0.020, 0.085);
        smoothInst = Smooth(smoothInst, normInst, dt, 0.028, 0.12);

        double avgAlpha = 1.0 - Math.Exp(-dt / 0.35);
        double bassThreshold = Math.Max(0.012, bassAvg * 1.38);
        double sinceBeat = lastBeatTimestamp == 0 ? 10.0 : time.GetElapsedTime(lastBeatTimestamp, now).TotalSeconds;
        bool beatFired = false;

        if (audible && rawBass > bassThreshold && sinceBeat >= BeatCooldownSeconds)
        {
            float ratio = (float)(rawBass / bassThreshold);
            beatPulse = firstOrResumed || ratio >= 1.55f ? 1.0f : Math.Clamp(0.42f + (ratio - 1.0f) * 1.05f, 0.42f, 1.0f);
            beatCount++;
            lastBeatTimestamp = now;
            beatFired = true;
        }
        else
        {
            beatPulse *= (float)Math.Exp(-dt / 0.11);
            if (beatPulse < 0.005f) beatPulse = 0f;
        }
        bassAvg = (float)(bassAvg + (rawBass - bassAvg) * avgAlpha);

        float bassExcess = Math.Clamp((normBass - bassFloor * 0.72f) * 2.1f, 0f, 1f);
        smoothBassPunch = Smooth(smoothBassPunch, Math.Max(beatPulse, bassExcess), dt, 0.018, 0.10);

        float levelLift = Math.Max(0f, targetLevel - levelFloor * 0.75f);
        float targetDyn = audible
            ? Math.Clamp(smoothLevel * 0.42f + levelLift * 1.35f + beatPulse * 0.42f, 0f, 1f)
            : 0f;
        smoothDynamic = Smooth(smoothDynamic, targetDyn, dt, 0.024, 0.12);

        double trebleAlpha = 1.0 - Math.Exp(-dt / 0.24);
        double trebleThreshold = Math.Max(0.009, trebleAvg * 1.36);
        double sinceTreble = lastTrebleTimestamp == 0 ? 10.0 : time.GetElapsedTime(lastTrebleTimestamp, now).TotalSeconds;

        if (audible && rawTreble > trebleThreshold && sinceTreble >= 0.09)
        {
            trebleTransient = 1.0f;
            lastTrebleTimestamp = now;
        }
        else
        {
            trebleTransient *= (float)Math.Exp(-dt / 0.085);
            if (trebleTransient < 0.005f) trebleTransient = 0f;
        }
        trebleAvg = (float)(trebleAvg + (rawTreble - trebleAvg) * trebleAlpha);

        UpdateMusicVibe(now, dt, audible, normBass, normTreble, normVocalRaw, normInst, targetLevel, beatFired);
    }

    void UpdateMusicVibe(
        long now,
        double dt,
        bool audible,
        float normBass,
        float normTreble,
        float normVocal,
        float normInst,
        float targetLevel,
        bool beatFired)
    {
        if (!audible && smoothLevel < 0.04f)
        {
            beatRateHz = LerpFloat(beatRateHz, 0f, dt, 0.6);
            currentVibe = AudioMusicVibe.Silent;
            return;
        }

        vibeEnergy = LerpFloat(vibeEnergy, targetLevel, dt, 0.95);
        vibeBass = LerpFloat(vibeBass, normBass, dt, 0.95);
        vibeVocal = LerpFloat(vibeVocal, normVocal, dt, 0.95);
        vibeInst = LerpFloat(vibeInst, normInst, dt, 0.95);
        vibeTreble = LerpFloat(vibeTreble, normTreble, dt, 0.95);

        beatRateHz *= (float)Math.Exp(-dt / 2.2);
        if (beatFired)
            beatRateHz = Math.Min(6.0f, beatRateHz + 0.42f);

        AudioMusicVibe candidate;
        if (vibeEnergy < 0.28f && beatRateHz < 0.85f && vibeVocal < 0.34f)
            candidate = AudioMusicVibe.AmbientChill;
        else if (vibeVocal >= 0.34f && vibeVocal >= vibeBass * 0.78f && beatRateHz < 2.2f)
            candidate = AudioMusicVibe.VocalAcoustic;
        else if (vibeBass >= 0.42f && beatRateHz >= 1.25f && vibeBass >= vibeInst * 0.90f)
            candidate = AudioMusicVibe.ElectronicDrop;
        else if (vibeInst >= 0.40f && vibeEnergy >= 0.44f && (vibeTreble >= 0.30f || beatRateHz >= 1.75f))
            candidate = AudioMusicVibe.RockMetal;
        else
            candidate = AudioMusicVibe.GroovePop;

        if (currentVibe == AudioMusicVibe.Silent)
        {
            currentVibe = candidate;
            lastVibeSwitchTimestamp = now;
            return;
        }

        if (candidate != currentVibe)
        {
            double sinceSwitch = lastVibeSwitchTimestamp == 0 ? 10.0 : time.GetElapsedTime(lastVibeSwitchTimestamp, now).TotalSeconds;
            if (sinceSwitch >= 0.90)
            {
                currentVibe = candidate;
                lastVibeSwitchTimestamp = now;
            }
        }
    }

    static float UpdateTrackRef(float currentRef, float rawPeak, double dt, bool audible, float floor)
    {
        if (rawPeak > currentRef)
            return Smooth(currentRef, rawPeak, dt, 0.15, 0.15);
        float decayTau = audible ? 14.0f : 3.0f;
        return Math.Max(floor, Smooth(currentRef, Math.Max(floor, rawPeak), dt, decayTau, decayTau));
    }

    static float UpdateAgc(float currentAgc, float rawBand, double dt, bool audible, float floor, float decaySeconds = 4.5f)
    {
        if (rawBand > currentAgc)
            return Smooth(currentAgc, rawBand, dt, 0.08, 0.08);
        float decayTau = audible ? decaySeconds : 1.5f;
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
