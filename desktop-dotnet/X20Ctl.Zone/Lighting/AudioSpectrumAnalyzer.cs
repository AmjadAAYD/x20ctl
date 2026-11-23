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
    AudioMusicVibe Vibe = AudioMusicVibe.Silent);

/// <summary>
/// Zero-allocation, thread-safe 512-point Hann-windowed Radix-2 Mid-Side dual-FFT analyzer
/// separating Bass/Instruments, centered Lyrical Vocals, dynamic punch, and detected music vibe.
/// </summary>
public sealed class AudioSpectrumAnalyzer
{
    public const int FftSize = 512;
    public const double IdleSeconds = 0.50;
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

    int ringPos;
    long lastProcessTimestamp;
    long lastBeatTimestamp;
    long lastTrebleTimestamp;
    long lastVibeSwitchTimestamp;

    float trackRef = 0.05f;
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
        currentVibe);

    public void Reset()
    {
        lock (gate)
        {
            Array.Clear(monoRing);
            Array.Clear(sideRing);
            ringPos = 0;
            lastProcessTimestamp = 0;
            lastBeatTimestamp = 0;
            lastTrebleTimestamp = 0;
            lastVibeSwitchTimestamp = 0;
            trackRef = 0.05f;
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
                smoothLevel = smoothBass = smoothMid = smoothTreble = 0f;
                smoothLeft = smoothRight = smoothEnergy = beatPulse = trebleTransient = 0f;
                smoothVocal = smoothVocalWarm = smoothVocalAir = smoothInst = smoothBassPunch = smoothDynamic = 0f;
                bassAvg = trebleAvg = bassFloor = levelFloor = 0f;
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
                float targetPan = lrSum > 0.001f ? Math.Clamp((rightRms - leftRms) / lrSum, -1f, 1f) : 0f;
                stereoPan = LerpFloat(stereoPan, targetPan, dt, 0.09);
            }
            else
            {
                stereoPan = 0f;
            }

            ComputeBands(sr, ch >= 2, out float rawBass, out float rawMid, out float rawTreble, out float rawVocalWarm, out float rawVocalAir, out float rawInst, out float rawCentroid);
            UpdateEnvelopesAndBeats(now, dt, rawRms, leftRms, rightRms, rawBass, rawMid, rawTreble, rawVocalWarm, rawVocalAir, rawInst, rawCentroid, firstOrResumed);
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
                smoothLevel = smoothBass = smoothMid = smoothTreble = 0f;
                smoothLeft = smoothRight = smoothEnergy = beatPulse = trebleTransient = 0f;
                smoothVocal = smoothVocalWarm = smoothVocalAir = smoothInst = smoothBassPunch = smoothDynamic = 0f;
                bassAvg = trebleAvg = bassFloor = levelFloor = 0f;
                vibeEnergy = vibeBass = vibeVocal = vibeInst = vibeTreble = beatRateHz = 0f;
            }
            lastProcessTimestamp = now;

            float peak = Sanitize(rawPeak);
            UpdateEnvelopesAndBeats(now, dt, peak * 0.7f, peak * 0.7f, peak * 0.7f, peak * 0.85f, peak * 0.65f, peak * 0.45f, peak * 0.30f, peak * 0.25f, peak * 0.70f, 0.45f, firstOrResumed);
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
        out float instrument,
        out float centroidNorm)
    {
        RunFft(monoRing, midMag);
        if (isStereo)
            RunFft(sideRing, sideMag);
        else
            Array.Clear(sideMag);

        double binHz = sampleRate / (double)FftSize;

        double bassSq = 0, midSq = 0, trebleSq = 0;
        double vocalWarmSq = 0, vocalAirSq = 0, instSideSq = 0, instSparkleSq = 0;
        double vocalSum = 0, vocalPeak = 0;
        double totalWeightSq = 0, logWeightedSum = 0;
        int bassN = 0, midN = 0, trebleN = 0, vocalWarmN = 0, vocalAirN = 0, instSideN = 0, instSparkleN = 0;

        int maxBin = FftSize / 2;
        for (int bin = 1; bin < maxBin; bin++)
        {
            double freq = bin * binHz;
            if (freq < 20 || freq > 14000) continue;

            double mm = midMag[bin];
            double sm = isStereo ? sideMag[bin] : 0.0;
            double magSq = mm * mm + sm * sm;

            if (freq <= 12000)
            {
                double logPos = Math.Clamp(Math.Log2(freq / 20.0) / MaxLogSpan, 0.0, 1.0);
                logWeightedSum += logPos * magSq;
                totalWeightSq += magSq;
            }

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

            if (freq >= 300 && freq <= 3400)
            {
                double centerMag = isStereo ? Math.Max(0.0, mm - 1.10 * sm) : mm;
                double centerSq = centerMag * centerMag;
                vocalSum += centerMag;
                if (centerMag > vocalPeak) vocalPeak = centerMag;
                if (freq < 1100)
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

        bass = bassN > 0 ? (float)Math.Sqrt(bassSq / bassN) : 0f;
        mid = midN > 0 ? (float)(Math.Sqrt(midSq / midN) * 1.4) : 0f;
        treble = trebleN > 0 ? (float)(Math.Sqrt(trebleSq / trebleN) * 2.2) : 0f;

        int vocalTotalN = vocalWarmN + vocalAirN;
        double vocalMean = vocalTotalN > 0 ? vocalSum / vocalTotalN : 0.0;
        double crest = vocalMean > 1e-6 ? vocalPeak / vocalMean : 0.0;
        double tonality = Math.Clamp((crest - 1.35) / 1.4, 0.18, 1.0);
        double bassSuppression = bass > mid * 2.8f ? 0.28 : 1.0;
        double vScale = tonality * bassSuppression * 1.75;

        vocalWarm = vocalWarmN > 0 ? (float)(Math.Sqrt(vocalWarmSq / vocalWarmN) * vScale) : 0f;
        vocalAir = vocalAirN > 0 ? (float)(Math.Sqrt(vocalAirSq / vocalAirN) * vScale * 1.15) : 0f;

        double instSideRms = instSideN > 0 ? Math.Sqrt(instSideSq / instSideN) : 0.0;
        double instSparkleRms = instSparkleN > 0 ? Math.Sqrt(instSparkleSq / instSparkleN) : 0.0;
        instrument = (float)Math.Sqrt(instSideRms * instSideRms + bass * bass * 0.30 + instSparkleRms * instSparkleRms * 0.72);

        centroidNorm = totalWeightSq > 1e-8 ? (float)Math.Clamp(logWeightedSum / totalWeightSq, 0.0, 1.0) : smoothCentroid;
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
        float rawInst,
        float rawCentroid,
        bool firstOrResumed)
    {
        bool audible = rawRms >= 0.0005f || leftRms >= 0.0005f || rightRms >= 0.0005f || rawBass >= 0.001f || rawMid >= 0.001f || rawTreble >= 0.001f;

        float rawPeakBand = Math.Max(rawRms * 1.25f, Math.Max(rawBass, Math.Max(rawMid, rawTreble)));
        trackRef = UpdateTrackRef(trackRef, rawPeakBand, dt, audible, 0.05f);

        agcBass = UpdateAgc(agcBass, rawBass, dt, audible, 0.04f, decaySeconds: 9.0f);
        agcMid = UpdateAgc(agcMid, rawMid, dt, audible, 0.035f, decaySeconds: 9.0f);
        agcTreble = UpdateAgc(agcTreble, rawTreble, dt, audible, 0.03f, decaySeconds: 9.0f);
        agcVocal = UpdateAgc(agcVocal, Math.Max(rawVocalWarm, rawVocalAir), dt, audible, 0.03f, decaySeconds: 7.5f);
        agcInst = UpdateAgc(agcInst, rawInst, dt, audible, 0.035f, decaySeconds: 8.5f);
        agcStereo = UpdateAgc(agcStereo, Math.Max(leftRms, rightRms), dt, audible, 0.035f, decaySeconds: 4.5f);

        float denomBass = Math.Max(0.03f, trackRef * 0.52f + agcBass * 0.48f);
        float denomMid = Math.Max(0.03f, trackRef * 0.52f + agcMid * 0.48f);
        float denomTreble = Math.Max(0.025f, trackRef * 0.48f + agcTreble * 0.52f);
        float denomVocal = Math.Max(0.025f, trackRef * 0.45f + agcVocal * 0.55f);
        float denomInst = Math.Max(0.03f, trackRef * 0.50f + agcInst * 0.50f);

        float normBass = audible ? Math.Clamp(rawBass / denomBass, 0f, 1f) : 0f;
        float normMid = audible ? Math.Clamp(rawMid / denomMid, 0f, 1f) : 0f;
        float normTreble = audible ? Math.Clamp(rawTreble / denomTreble, 0f, 1f) : 0f;
        float normVocalWarm = audible ? Math.Clamp(rawVocalWarm / denomVocal, 0f, 1f) : 0f;
        float normVocalAir = audible ? Math.Clamp(rawVocalAir / denomVocal, 0f, 1f) : 0f;
        float normInst = audible ? Math.Clamp(rawInst / denomInst, 0f, 1f) : 0f;
        float normVocalRaw = Math.Max(normVocalWarm, normVocalAir * 0.92f);

        float normLeft = audible ? Math.Clamp(leftRms / agcStereo, 0f, 1f) : 0f;
        float normRight = audible ? Math.Clamp(rightRms / agcStereo, 0f, 1f) : 0f;
        float targetLevel = Math.Clamp(Math.Max(normBass * 0.50f + normMid * 0.30f + normTreble * 0.25f, Math.Max(normBass, normMid * 0.90f)), 0f, 1f);

        bassFloor = LerpFloat(bassFloor, normBass, dt, 0.75);
        levelFloor = LerpFloat(levelFloor, targetLevel, dt, 0.80);

        smoothBass = Smooth(smoothBass, normBass, dt, 0.028, 0.14);
        smoothMid = Smooth(smoothMid, normMid, dt, 0.035, 0.15);
        smoothTreble = Smooth(smoothTreble, normTreble, dt, 0.025, 0.12);
        smoothLeft = Smooth(smoothLeft, normLeft, dt, 0.030, 0.15);
        smoothRight = Smooth(smoothRight, normRight, dt, 0.030, 0.15);
        smoothLevel = Smooth(smoothLevel, targetLevel, dt, 0.035, 0.16);
        smoothEnergy = Smooth(smoothEnergy, targetLevel, dt, 0.55, 1.60);
        if (audible)
            smoothCentroid = Math.Clamp(firstOrResumed ? rawCentroid : LerpFloat(smoothCentroid, rawCentroid, dt, 0.05), 0f, 1f);

        // Clean lyrical vocal gating: strips residual background chord bleed so triggers silence between vocal phrases.
        float bleed = normInst * 0.24f + 0.05f;
        float cleanWarm = Math.Max(0f, normVocalWarm - bleed);
        float cleanAir = Math.Max(0f, normVocalAir - bleed * 0.90f);
        float cleanVocal = Math.Max(cleanWarm, cleanAir * 0.92f);
        smoothVocalWarm = Smooth(smoothVocalWarm, cleanWarm, dt, 0.024, 0.095);
        smoothVocalAir = Smooth(smoothVocalAir, cleanAir, dt, 0.022, 0.085);
        smoothVocal = Smooth(smoothVocal, cleanVocal, dt, 0.024, 0.095);
        smoothInst = Smooth(smoothInst, normInst, dt, 0.035, 0.14);

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
        smoothBassPunch = Smooth(smoothBassPunch, Math.Max(beatPulse, bassExcess), dt, 0.020, 0.11);

        float levelLift = Math.Max(0f, targetLevel - levelFloor * 0.75f);
        float targetDyn = audible
            ? Math.Clamp(smoothLevel * 0.42f + levelLift * 1.35f + beatPulse * 0.42f, 0f, 1f)
            : 0f;
        smoothDynamic = Smooth(smoothDynamic, targetDyn, dt, 0.028, 0.14);

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
        if (!audible && smoothLevel < 0.03f)
        {
            beatRateHz = LerpFloat(beatRateHz, 0f, dt, 1.2);
            currentVibe = AudioMusicVibe.Silent;
            return;
        }

        vibeEnergy = LerpFloat(vibeEnergy, targetLevel, dt, 1.8);
        vibeBass = LerpFloat(vibeBass, normBass, dt, 1.8);
        vibeVocal = LerpFloat(vibeVocal, normVocal, dt, 1.8);
        vibeInst = LerpFloat(vibeInst, normInst, dt, 1.8);
        vibeTreble = LerpFloat(vibeTreble, normTreble, dt, 1.8);

        beatRateHz *= (float)Math.Exp(-dt / 2.5);
        if (beatFired)
            beatRateHz = Math.Min(6.0f, beatRateHz + 0.42f);

        AudioMusicVibe candidate;
        if (vibeEnergy < 0.28f && beatRateHz < 0.85f && vibeVocal < 0.34f)
            candidate = AudioMusicVibe.AmbientChill;
        else if (vibeVocal >= 0.35f && vibeVocal >= vibeBass * 0.82f && beatRateHz < 2.2f)
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
            if (sinceSwitch >= 1.4)
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
