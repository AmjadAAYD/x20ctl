namespace X20Ctl.Zone.Lighting;

public enum AudioHapticsMode
{
    AutoAdaptive,
    LyricalMusical,
    QuadConcert,
    TriggerPulse,
    StereoWave,
    SubwooferDrop,
    Heartbeat,
}

public readonly record struct HapticsFrame(
    double LeftGrip,
    double RightGrip,
    double LeftTrigger,
    double RightTrigger)
{
    public static HapticsFrame Zero => new(0, 0, 0, 0);
    public bool IsSilent => LeftGrip <= 0.001 && RightGrip <= 0.001 && LeftTrigger <= 0.001 && RightTrigger <= 0.001;
}

/// <summary>
/// Pure 4-motor audio haptics choreography engine mapping live stereo FFT bands, Mid-Side vocal stems,
/// dynamic contrast, and detected music vibe onto Left Grip, Right Grip, Left Trigger, and Right Trigger.
/// </summary>
public static class AudioHapticsEngine
{
    public static HapticsFrame Evaluate(AudioFrame audio, AudioHapticsMode mode, int strengthPercent, double elapsedSeconds = 0.0)
    {
        double scale = Math.Clamp(strengthPercent, 0, 100) / 100.0;
        if (scale <= 0.0) return HapticsFrame.Zero;

        double bass = Math.Clamp(audio.Bass, 0.0, 1.0);
        double mid = Math.Clamp(audio.Mid, 0.0, 1.0);
        double treble = Math.Clamp(audio.Treble, 0.0, 1.0);
        double beat = Math.Clamp(audio.BeatPulse, 0.0, 1.0);
        double transient = Math.Clamp(audio.TrebleTransient, 0.0, 1.0);
        double energy = Math.Clamp(audio.Energy, 0.0, 1.0);
        double level = Math.Clamp(audio.Level, 0.0, 1.0);

        double vocal = Math.Clamp(audio.VocalPresence, 0.0, 1.0);
        double vocalWarm = Math.Clamp(audio.VocalWarmth > 0 ? audio.VocalWarmth : vocal, 0.0, 1.0);
        double vocalAir = Math.Clamp(audio.VocalAir > 0 ? audio.VocalAir : vocal * 0.85, 0.0, 1.0);
        double effBassPunch = audio.BassPunch > 0 ? Math.Clamp(audio.BassPunch, 0.0, 1.0) : Math.Max(beat, Math.Pow(bass, 1.45));
        double effInst = audio.InstrumentEnergy > 0 ? Math.Clamp(audio.InstrumentEnergy, 0.0, 1.0) : Math.Max(mid * 0.75, treble * 0.55);
        double effDyn = audio.DynamicContrast > 0
            ? Math.Clamp(audio.DynamicContrast, 0.0, 1.0)
            : Math.Clamp(Math.Max(beat * 0.80, Math.Pow(Math.Max(level, bass), 1.30)), 0.12, 1.0);
        double dynCurve = 0.25 + 0.75 * effDyn;
        bool hasEnrichedStems = audio.VocalPresence > 0f || audio.InstrumentEnergy > 0f || audio.BassPunch > 0f || audio.Vibe != AudioMusicVibe.Silent;

        // Extract left/right weights even when stereo channels aren't populated separately.
        double leftBal = audio.LeftLevel > 0.0 || audio.RightLevel > 0.0
            ? Math.Clamp(audio.LeftLevel, 0.0, 1.0)
            : Math.Clamp(level * (1.0 - Math.Max(0.0, audio.StereoPan) * 0.65), 0.0, 1.0);
        double rightBal = audio.LeftLevel > 0.0 || audio.RightLevel > 0.0
            ? Math.Clamp(audio.RightLevel, 0.0, 1.0)
            : Math.Clamp(level * (1.0 - Math.Max(0.0, -audio.StereoPan) * 0.65), 0.0, 1.0);

        double lg = 0, rg = 0, lt = 0, rt = 0;

        switch (mode)
        {
            case AudioHapticsMode.AutoAdaptive:
                if (audio.Vibe == AudioMusicVibe.Silent && level <= 0.02 && beat <= 0.02 && vocal <= 0.02)
                    return HapticsFrame.Zero;

                switch (audio.Vibe)
                {
                    case AudioMusicVibe.AmbientChill:
                        // Sparse breathing swells: grips sigh softly on warm chords; triggers shimmer only on real vocals.
                        lg = Math.Pow(effBassPunch, 1.6) * 0.42 * dynCurve;
                        rg = Math.Pow(effInst, 1.7) * 0.35 * dynCurve;
                        lt = vocal >= 0.06 ? Math.Pow(vocalWarm, 1.2) * 0.68 : 0.0;
                        rt = vocal >= 0.06 ? Math.Pow(vocalAir, 1.2) * 0.62 : 0.0;
                        break;

                    case AudioMusicVibe.VocalAcoustic:
                        // Lyrical spotlight: triggers follow vocal phrasing dynamically; grips give gentle rhythmic support.
                        lg = Math.Max(beat * 0.55, Math.Pow(effBassPunch, 1.6) * 0.45) * dynCurve;
                        rg = Math.Pow(effInst, 1.55) * 0.40 * dynCurve;
                        lt = vocal >= 0.05 ? Math.Clamp(Math.Pow(vocalWarm, 1.08), 0.0, 1.0) : 0.0;
                        rt = vocal >= 0.05 ? Math.Clamp(Math.Pow(vocalAir, 1.12), 0.0, 1.0) : 0.0;
                        break;

                    case AudioMusicVibe.ElectronicDrop:
                        // Surgical EDM punch: grips slam kicks with silent valleys between hits; triggers ride vocals + drop bursts.
                        lg = Math.Max(beat * 1.0, Math.Pow(effBassPunch, 1.55) * 0.85) * (0.20 + 0.80 * effDyn);
                        rg = Math.Max(transient * 0.92, Math.Pow(effInst, 1.65) * 0.68) * (0.20 + 0.80 * effDyn);
                        if (vocal >= 0.07)
                        {
                            lt = Math.Max(Math.Pow(vocalWarm, 1.12), beat * 0.42);
                            rt = Math.Max(Math.Pow(vocalAir, 1.15), transient * 0.65);
                        }
                        else
                        {
                            bool bigDrop = beat > 0.55 && energy > 0.52;
                            lt = bigDrop ? beat * 0.85 : 0.0;
                            rt = bigDrop || transient > 0.58 ? Math.Max(transient * 0.82, beat * 0.65) : 0.0;
                        }
                        break;

                    case AudioMusicVibe.RockMetal:
                        // Transient-first punch: ignore steady distorted guitar drone, punch grips on kick/snare/riff spikes, triggers lead vocal.
                        lg = Math.Max(beat * 0.96, Math.Pow(effBassPunch, 1.45) * 0.88) * dynCurve;
                        rg = Math.Max(transient * 0.92, Math.Pow(treble, 1.75) * 0.72) * dynCurve;
                        lt = vocal >= 0.06 ? Math.Clamp(Math.Pow(vocalWarm, 1.10), 0.0, 1.0) : 0.0;
                        rt = vocal >= 0.06 ? Math.Clamp(Math.Pow(vocalAir, 1.15), 0.0, 1.0) : 0.0;
                        break;

                    case AudioMusicVibe.GroovePop:
                    default:
                        EvaluateLyricalMusical(bass, beat, transient, effBassPunch, effInst, dynCurve, vocal, vocalWarm, vocalAir, out lg, out rg, out lt, out rt);
                        break;
                }
                break;

            case AudioHapticsMode.LyricalMusical:
                EvaluateLyricalMusical(bass, beat, transient, effBassPunch, effInst, dynCurve, vocal, vocalWarm, vocalAir, out lg, out rg, out lt, out rt);
                break;

            case AudioHapticsMode.TriggerPulse:
                // Fingertip percussion: triggers snap crisply to kicks & hi-hats; grips add punchy bass cushion.
                lg = Math.Max(beat * 0.52, Math.Pow(bass, 1.45) * 0.55) * (hasEnrichedStems ? dynCurve : 1.0);
                rg = Math.Pow(mid, 1.45) * 0.40 * (hasEnrichedStems ? dynCurve : 1.0);
                lt = Math.Max(beat * 1.0, Math.Pow(bass, 1.20) * 0.90);
                rt = Math.Max(transient * 1.0, Math.Max(treble * 0.95, mid * 0.55));
                break;

            case AudioHapticsMode.StereoWave:
            {
                // 3D Stereo stage + alternating LT/RT ping-pong on beats.
                bool pingLeft = (Math.Abs(audio.BeatCount) & 1) == 0;
                double kickBounce = beat * 0.88;
                double waveScale = hasEnrichedStems ? dynCurve : 1.0;
                lg = Math.Clamp((Math.Pow(leftBal, 1.2) * 0.82 + Math.Pow(effBassPunch, 1.3) * 0.35) * waveScale, 0.0, 1.0);
                rg = Math.Clamp((Math.Pow(rightBal, 1.2) * 0.82 + Math.Pow(mid, 1.3) * 0.35) * waveScale, 0.0, 1.0);
                lt = Math.Clamp(leftBal * 0.55 + (pingLeft ? kickBounce : transient * 0.45), 0.0, 1.0);
                rt = Math.Clamp(rightBal * 0.55 + (!pingLeft ? kickBounce : transient * 0.45), 0.0, 1.0);
                break;
            }

            case AudioHapticsMode.SubwooferDrop:
                // Calm verses tickle triggers lightly; heavy chorus/drops slam all 4 motors like cannons.
                if (beat > 0.36 || energy >= 0.55 || effBassPunch > 0.72)
                {
                    double slam = Math.Clamp(Math.Max(beat, Math.Max(bass * 0.95, energy * 0.88)), 0.0, 1.0);
                    lg = slam;
                    rg = Math.Clamp(slam * 0.88 + treble * 0.25, 0.0, 1.0);
                    lt = slam;
                    rt = Math.Clamp(Math.Max(slam * 0.92, transient), 0.0, 1.0);
                }
                else
                {
                    lg = 0;
                    rg = 0;
                    lt = vocal >= 0.08 ? vocal * 0.50 : bass * 0.30;
                    rt = Math.Max(treble * 0.35, mid * 0.25);
                }
                break;

            case AudioHapticsMode.Heartbeat:
                // Lub-Dub double pulse per beat: crisp triggers first ("lub"), then deep grips ("DUB").
                if (beat >= 0.55)
                {
                    lg = beat * 0.25;
                    rg = beat * 0.20;
                    lt = beat;
                    rt = beat * 0.92;
                }
                else if (beat >= 0.16)
                {
                    double dub = Math.Clamp((beat - 0.16) / 0.39, 0.0, 1.0);
                    lg = Math.Max(0.45 + 0.55 * dub, bass * 0.75);
                    rg = Math.Max(0.35 + 0.45 * dub, mid * 0.55);
                    lt = 0;
                    rt = transient * 0.5;
                }
                else
                {
                    double quietBreath = 0.05 * (0.5 + 0.5 * Math.Sin(elapsedSeconds * 3.0)) * Math.Max(bass, mid);
                    lg = quietBreath;
                    rg = 0;
                    lt = vocal >= 0.12 ? vocal * 0.32 : (treble > 0.35 ? treble * 0.28 : 0);
                    rt = treble > 0.35 ? treble * 0.28 : 0;
                }
                break;

            case AudioHapticsMode.QuadConcert:
            default:
                // 4D Concert Stage with dynamic breathing contrast.
                if (hasEnrichedStems)
                {
                    lg = Math.Max(beat * 0.95, Math.Pow(effBassPunch, 1.4) * 0.90) * dynCurve;
                    rg = Math.Max(transient * 0.82, Math.Pow(effInst, 1.45) * 0.85) * dynCurve;
                    lt = vocal >= 0.06
                        ? Math.Clamp(Math.Max(Math.Pow(vocalWarm, 1.1), beat * 0.45), 0.0, 1.0)
                        : (beat > 0.52 ? beat * 0.75 : 0.0);
                    rt = vocal >= 0.06
                        ? Math.Clamp(Math.Max(Math.Pow(vocalAir, 1.1), transient * 0.65), 0.0, 1.0)
                        : Math.Max(transient * 0.92, Math.Pow(treble, 1.65) * 0.75);
                }
                else
                {
                    lg = Math.Max(bass * 0.95, beat * 0.92);
                    rg = Math.Max(mid * 0.88, treble * 0.45);
                    lt = Math.Max(beat * 0.96, bass * leftBal * 0.90);
                    rt = Math.Max(transient * 0.98, Math.Max(treble * 0.88, rightBal * mid * 0.65));
                }
                break;
        }

        static double GateGrip(double v, double gain)
        {
            double scaled = Math.Clamp(v * gain, 0.0, 1.0);
            return scaled >= 0.075 ? scaled : 0.0;
        }

        static double GateTrigger(double v, double gain)
        {
            double scaled = Math.Clamp(v * gain, 0.0, 1.0);
            if (scaled < 0.075) return 0.0;
            double shaped = Math.Pow((scaled - 0.075) / 0.925, 1.22);
            return Math.Clamp(0.24 + 0.76 * shaped, 0.0, 1.0);
        }

        return new HapticsFrame(
            GateGrip(lg, scale),
            GateGrip(rg, scale),
            GateTrigger(lt, scale),
            GateTrigger(rt, scale));
    }

    static void EvaluateLyricalMusical(
        double bass,
        double beat,
        double transient,
        double bassPunch,
        double instrument,
        double dynCurve,
        double vocal,
        double vocalWarm,
        double vocalAir,
        out double lg,
        out double rg,
        out double lt,
        out double rt)
    {
        // Grips follow the musical backbone (kick, bass groove, stereo instruments) with full dynamic contrast.
        lg = Math.Clamp(Math.Max(beat * 0.94, Math.Pow(bassPunch, 1.38) * 0.86 + Math.Pow(bass, 2.2) * 0.22) * dynCurve, 0.0, 1.0);
        rg = Math.Clamp(Math.Max(transient * 0.84, Math.Pow(instrument, 1.45) * 0.80) * dynCurve, 0.0, 1.0);

        // Triggers are purely lyrical (centered vocal chest warmth + vocal air); silent during pure instrumentals.
        if (vocal < 0.075)
        {
            lt = 0.0;
            rt = 0.0;
        }
        else
        {
            lt = Math.Clamp(Math.Pow(Math.Max(vocalWarm, vocal * 0.92), 1.12), 0.0, 1.0);
            rt = Math.Clamp(Math.Pow(Math.Max(vocalAir, vocal * 0.80), 1.18), 0.0, 1.0);
        }
    }
}
